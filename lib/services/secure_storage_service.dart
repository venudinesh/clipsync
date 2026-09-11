import 'dart:convert';
import 'dart:math';
import 'dart:typed_data';

import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:hive_flutter/hive_flutter.dart';

/// Manages encryption keys for Hive boxes using Android Keystore /
/// iOS Keychain via flutter_secure_storage.
///
/// Flow:
/// 1. On first launch → generate a random 256-bit key → store in SecureStorage.
/// 2. On subsequent launches → read the key from SecureStorage.
/// 3. Open each Hive box with the key (AES-256-CBC under the hood).
class SecureStorageService {
  static const _clipKeyLabel = 'clipSync_hive_clip_key';
  static const _noteKeyLabel = 'clipSync_hive_note_key';
  static const _settingsKeyLabel = 'clipSync_hive_settings_key';

  /// Chat transcripts get their own key rather than riding on the notes key.
  /// A conversation with the local model can contain anything the user pasted
  /// into it, so it is treated as sensitive as the clipboard itself.
  static const _chatKeyLabel = 'clipSync_hive_chat_key';

  final FlutterSecureStorage _secureStorage;

  SecureStorageService({FlutterSecureStorage? secureStorage})
      : _secureStorage = secureStorage ??
            const FlutterSecureStorage(
              aOptions: AndroidOptions(encryptedSharedPreferences: true),
              iOptions: IOSOptions(
                accessibility: KeychainAccessibility.first_unlock_this_device,
              ),
            );

  // ── Key Management ──────────────────────────────────────────────────────

  /// Retrieve an existing key or generate + persist a new one.
  Future<Uint8List> _getOrCreateKey(String label) async {
    final existing = await _secureStorage.read(key: label);
    if (existing != null && existing.isNotEmpty) {
      return base64Url.decode(existing);
    }
    return _createKey(label);
  }

  /// Generate a 256-bit key and persist it. Split out from [_getOrCreateKey] so
  /// a caller that has already established the key is absent does not pay for a
  /// second read to be told so again.
  Future<Uint8List> _createKey(String label) async {
    final key = _generateRandomKey();
    await _secureStorage.write(key: label, value: base64Url.encode(key));
    return key;
  }

  Uint8List _generateRandomKey() {
    final rng = Random.secure();
    return Uint8List.fromList(
      List<int>.generate(32, (_) => rng.nextInt(256)),
    );
  }

  Future<Uint8List> get clipKey => _getOrCreateKey(_clipKeyLabel);
  Future<Uint8List> get noteKey => _getOrCreateKey(_noteKeyLabel);
  Future<Uint8List> get settingsKey => _getOrCreateKey(_settingsKeyLabel);
  Future<Uint8List> get chatKey => _getOrCreateKey(_chatKeyLabel);

  // ── Box Helpers ─────────────────────────────────────────────────────────

  /// Open an encrypted Hive box.
  Future<Box<T>> openEncryptedBox<T>(String name, Uint8List key) async {
    return Hive.openBox<T>(
      name,
      encryptionCipher: HiveAesCipher(key),
    );
  }

  /// Open an unencrypted Hive box.
  Future<Box<T>> openPlainBox<T>(String name) async {
    return Hive.openBox<T>(name);
  }

  /// Open a box, trying encrypted first. Only falls back to an unencrypted box
  /// when no encryption key exists yet (genuine first run). If a key exists but
  /// the encrypted open fails, we rethrow instead of silently writing plaintext,
  /// which would leak sensitive clipboard/notes data at rest.
  ///
  /// [existing] lets a caller that has already read [keyLabel] hand the value
  /// over: an empty string means "looked, and it was not there", and null means
  /// "not looked yet", in which case this reads it. Null is the safe default,
  /// because an absent key is what permits the plaintext fallback and a failed
  /// read must never be mistaken for one.
  Future<Box<T>> openBoxWithFallback<T>(
    String name,
    String keyLabel, {
    String? existing,
  }) async {
    final stored = existing ?? await _secureStorage.read(key: keyLabel) ?? '';
    final keyExisted = stored.isNotEmpty;

    try {
      final key =
          keyExisted ? base64Url.decode(stored) : await _createKey(keyLabel);
      return await openEncryptedBox<T>(name, key);
    } catch (e) {
      // First launch with no pre-existing key: allow opening a plaintext box so
      // a box left over from an older unencrypted build still loads.
      if (!keyExisted) {
        debugPrint(
            '[SecureStorage] No key existed; opening plaintext box "$name"');
        return openPlainBox<T>(name);
      }
      // A key existed but the encrypted box failed to open (corruption, a key
      // mismatch, or a corrupt stored key). Do NOT silently downgrade.
      rethrow;
    }
  }

  /// Convenience: open all app boxes concurrently, off one secure-storage round
  /// trip.
  ///
  /// The four boxes used to cost eight reads between them, because each asked
  /// whether its key existed and then asked for the key itself. One [readAll]
  /// answers both questions for all four, and only a genuine first run touches
  /// the store again, to write the keys it had to generate.
  Future<Map<String, Box>> openAllBoxes() async {
    const boxes = <String, String>{
      'clip_history': _clipKeyLabel,
      'notes': _noteKeyLabel,
      'settings': _settingsKeyLabel,
      'chat_sessions': _chatKeyLabel,
    };

    Map<String, String>? stored;
    try {
      stored = await _secureStorage.readAll();
    } catch (e) {
      // Not fatal, just slower: each box falls back to reading its own key.
      debugPrint('[SecureStorage] readAll failed, reading keys singly: $e');
    }

    final results = await Future.wait<MapEntry<String, Box>>([
      for (final entry in boxes.entries)
        openBoxWithFallback(
          entry.key,
          entry.value,
          existing: stored == null ? null : stored[entry.value] ?? '',
        ).then((b) => MapEntry(entry.key, b)),
    ]);
    return Map.fromEntries(results);
  }

  // ── Diagnostics ─────────────────────────────────────────────────────────

  /// Check if encryption keys already exist (i.e., first-run detection).
  Future<bool> get isInitialized async {
    final existing = await _secureStorage.read(key: _clipKeyLabel);
    return existing != null && existing.isNotEmpty;
  }

  /// Wipe all keys (for debug reset — destroys encrypted data).
  Future<void> deleteAllKeys() async {
    await _secureStorage.delete(key: _clipKeyLabel);
    await _secureStorage.delete(key: _noteKeyLabel);
    await _secureStorage.delete(key: _settingsKeyLabel);
    await _secureStorage.delete(key: _chatKeyLabel);
  }

  /// Secure wipe: overwrite with random data before deletion.
  Future<void> secureWipeAllKeys() async {
    for (final label in const [
      _clipKeyLabel,
      _noteKeyLabel,
      _settingsKeyLabel,
      _chatKeyLabel,
    ]) {
      // A fresh random value per label, so one dummy read cannot reveal that
      // several keys were overwritten with the same bytes.
      await _secureStorage.write(
          key: label, value: base64Url.encode(_generateRandomKey()));
      await _secureStorage.delete(key: label);
    }
  }
}
