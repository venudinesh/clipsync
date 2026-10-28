import 'dart:convert';
import 'dart:math';

import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:hive_ce_flutter/hive_flutter.dart';

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
              // v9 is deliberate. The v10 migration from this plugin's v9
              // EncryptedSharedPreferences store fails on real devices ("bad
              // base-64"), which would orphan every existing Hive key. Stay on
              // 9 until that migration is fixed upstream and tested on a
              // device that already holds keys.
              aOptions: AndroidOptions(encryptedSharedPreferences: true),
              iOptions: IOSOptions(
                accessibility: KeychainAccessibility.first_unlock_this_device,
              ),
            );

  // Key Management

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

  // Box Helpers

  /// Open an encrypted Hive box.
  Future<Box<T>> openEncryptedBox<T>(String name, Uint8List key) async {
    return Hive.openBox<T>(
      name,
      encryptionCipher: HiveAesCipher(key),
    );
  }

  /// Opens a box with its stored key, or creates a key on a genuine first run.
  ///
  /// If a box file already exists but its key is gone (a restored backup, a
  /// wiped keystore), this throws instead of minting a replacement key. A new
  /// key would open an empty box over the old file and leave the real data
  /// permanently unreadable.
  Future<Box<T>> openBoxWithFallback<T>(
    String name,
    String keyLabel, {
    String? existing,
  }) async {
    final stored = existing ?? await _secureStorage.read(key: keyLabel) ?? '';
    final keyExisted = stored.isNotEmpty;

    // A restored backup or a wiped keystore leaves the box file but not the
    // key. Never mint a replacement over it: the new key would open an empty
    // box and make the real data permanently unreadable.
    if (!keyExisted && await Hive.boxExists(name)) {
      throw StateError(
        'The encrypted box "$name" already exists but its key is missing. '
        'Refusing to replace the key and orphan the data.',
      );
    }

    final key =
        keyExisted ? base64Url.decode(stored) : await _createKey(keyLabel);
    return openEncryptedBox<T>(name, key);
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

  // Diagnostics

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
