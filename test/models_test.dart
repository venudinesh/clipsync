import 'dart:ui';

import 'package:flutter/painting.dart' show HSLColor;
import 'package:flutter_test/flutter_test.dart';
import 'package:clip_sync_ai/main.dart';
import 'package:clip_sync_ai/ui/ui.dart';

void main() {
  // ───────────────────────────────────────────────────────────────────────────
  // ClipEntry MODEL
  // ───────────────────────────────────────────────────────────────────────────

  group('ClipEntry', () {
    test('creates with required fields and generates id + timestamp', () {
      final entry = ClipEntry(
        rawText: 'hello',
        processedMarkdown: '**hello**',
      );

      expect(entry.id, isNotEmpty);
      expect(entry.rawText, 'hello');
      expect(entry.processedMarkdown, '**hello**');
      expect(entry.isPinned, isFalse);
      expect(entry.isChecklist, isFalse);
      expect(entry.timestamp, isA<DateTime>());
    });

    test('creates with all fields specified', () {
      final now = DateTime(2025, 6, 15, 10, 30);
      final entry = ClipEntry(
        id: 'test-id-123',
        rawText: 'raw',
        processedMarkdown: 'md',
        timestamp: now,
        isPinned: true,
        isChecklist: true,
      );

      expect(entry.id, 'test-id-123');
      expect(entry.timestamp, now);
      expect(entry.isPinned, isTrue);
      expect(entry.isChecklist, isTrue);
    });

    test('toMap and fromMap round-trip', () {
      final now = DateTime(2025, 6, 15, 10, 30, 45, 123);
      final entry = ClipEntry(
        id: 'rt-id',
        rawText: 'test raw',
        processedMarkdown: '# Test\n- item 1\n- item 2',
        timestamp: now,
        isPinned: true,
        isChecklist: false,
      );

      final map = entry.toMap();
      final restored = ClipEntry.fromMap(map);

      expect(restored.id, entry.id);
      expect(restored.rawText, entry.rawText);
      expect(restored.processedMarkdown, entry.processedMarkdown);
      expect(restored.timestamp.toIso8601String(), entry.timestamp.toIso8601String());
      expect(restored.isPinned, entry.isPinned);
      expect(restored.isChecklist, entry.isChecklist);
    });

    test('copyWith preserves unmodified fields', () {
      final entry = ClipEntry(
        id: 'cw-id',
        rawText: 'original',
        processedMarkdown: 'processed',
        isPinned: false,
        isChecklist: false,
      );

      final modified = entry.copyWith(isPinned: true, isChecklist: true);

      expect(modified.id, 'cw-id');
      expect(modified.rawText, 'original');
      expect(modified.processedMarkdown, 'processed');
      expect(modified.isPinned, isTrue);
      expect(modified.isChecklist, isTrue);
      expect(modified.timestamp, entry.timestamp); // Same timestamp
    });

    test('copyWith can change text fields', () {
      final entry = ClipEntry(
        id: 'id',
        rawText: 'old raw',
        processedMarkdown: 'old md',
      );

      final modified = entry.copyWith(
        rawText: 'new raw',
        processedMarkdown: 'new md',
      );

      expect(modified.rawText, 'new raw');
      expect(modified.processedMarkdown, 'new md');
      expect(modified.id, 'id'); // ID unchanged
    });

    test('fromMap handles missing optional fields gracefully', () {
      final map = {
        'id': 'minimal',
        'rawText': '',
        'processedMarkdown': '',
        'timestamp': DateTime.now().toIso8601String(),
      };

      final entry = ClipEntry.fromMap(map);
      expect(entry.id, 'minimal');
      expect(entry.isPinned, isFalse);
      expect(entry.isChecklist, isFalse);
    });
  });

  // ───────────────────────────────────────────────────────────────────────────
  // Note MODEL
  // ───────────────────────────────────────────────────────────────────────────

  group('Note', () {
    test('creates with defaults', () {
      final note = Note();

      expect(note.id, isNotEmpty);
      expect(note.title, isEmpty);
      expect(note.content, isEmpty);
      expect(note.tags, isEmpty);
      expect(note.isPinned, isFalse);
      expect(note.createdAt, isA<DateTime>());
      expect(note.updatedAt, isA<DateTime>());
    });

    test('creates with all fields', () {
      final now = DateTime(2025, 6, 15);
      final note = Note(
        id: 'note-1',
        title: 'My Note',
        content: '# Heading\n\nSome content',
        tags: ['Tasks', 'Work'],
        isPinned: true,
        createdAt: now,
        updatedAt: now,
      );

      expect(note.id, 'note-1');
      expect(note.title, 'My Note');
      expect(note.content, '# Heading\n\nSome content');
      expect(note.tags, ['Tasks', 'Work']);
      expect(note.isPinned, isTrue);
    });

    test('toMap and fromMap round-trip', () {
      final now = DateTime(2025, 6, 15, 14, 30);
      final note = Note(
        id: 'note-rt',
        title: 'Round Trip',
        content: '**Bold** text',
        tags: ['Idea', 'OCR'],
        isPinned: false,
        createdAt: now,
        updatedAt: now,
      );

      final map = note.toMap();
      final restored = Note.fromMap(map);

      expect(restored.id, note.id);
      expect(restored.title, note.title);
      expect(restored.content, note.content);
      expect(restored.tags, note.tags);
      expect(restored.isPinned, note.isPinned);
      expect(restored.createdAt.toIso8601String(), note.createdAt.toIso8601String());
      expect(restored.updatedAt.toIso8601String(), note.updatedAt.toIso8601String());
    });

    test('fields are mutable', () {
      final note = Note(title: 'Old', content: 'Old content');
      note.title = 'New';
      note.content = 'New content';
      note.isPinned = true;
      note.tags.add('NewTag');

      expect(note.title, 'New');
      expect(note.content, 'New content');
      expect(note.isPinned, isTrue);
      expect(note.tags, ['NewTag']);
    });

    test('fromMap handles empty tags list', () {
      final map = {
        'id': 'note-notags',
        'title': 'No Tags',
        'content': 'content',
        'tags': [],
        'isPinned': false,
        'createdAt': DateTime.now().toIso8601String(),
        'updatedAt': DateTime.now().toIso8601String(),
      };

      final note = Note.fromMap(map);
      expect(note.tags, isEmpty);
    });

    test('fromMap handles missing tags', () {
      final map = {
        'id': 'note-notags2',
        'title': 'Missing Tags Key',
        'content': '',
        'createdAt': DateTime.now().toIso8601String(),
        'updatedAt': DateTime.now().toIso8601String(),
      };

      final note = Note.fromMap(map);
      expect(note.tags, isEmpty);
    });
  });

  // ───────────────────────────────────────────────────────────────────────────
  // AppColors CONSTANTS
  // ───────────────────────────────────────────────────────────────────────────

  group('AppColors', () {
    test('canvas is off black, not pure black', () {
      // Pure #000000 is reserved for the AMOLED toggle. Everywhere else the
      // canvas has to be light enough for a shadow to land on it.
      expect(AppColors.canvas, DarkSurface.canvas);
      expect(AppColors.canvas, isNot(const Color(0xFF000000)));
    });

    test('primaryAccent follows the global accent notifier', () {
      expect(AppColors.primaryAccent, appAccentColor.value);
      expect(AppColors.primaryAccent, kDefaultAccent);
    });

    test('secondaryAccent is derived from the accent, not fixed', () {
      expect(AppColors.secondaryAccent, accentCompanion(appAccentColor.value));
    });

    test('accent swatches are soft, never fully saturated', () {
      for (final swatch in kAccents) {
        final hsl = HSLColor.fromColor(swatch.color);
        expect(hsl.saturation, lessThan(0.8),
            reason: '${swatch.name} is too saturated');
      }
    });

    test('all color constants are defined', () {
      expect(AppColors.canvas, isNotNull);
      expect(AppColors.primaryAccent, isNotNull);
      expect(AppColors.secondaryAccent, isNotNull);
      expect(AppColors.textPrimary, isNotNull);
      expect(AppColors.textSecondary, isNotNull);
      expect(AppColors.textTertiary, isNotNull);
      expect(AppColors.danger, isNotNull);
      expect(AppColors.success, isNotNull);
      expect(AppColors.warning, isNotNull);
    });
  });

  // ───────────────────────────────────────────────────────────────────────────
  // AppDefaults CONSTANTS
  // ───────────────────────────────────────────────────────────────────────────

  group('AppDefaults', () {
    test('default model is defined', () {
      expect(AppDefaults.defaultModel, isNotEmpty);
    });

    test('clipboard debounce is 2 seconds', () {
      expect(AppDefaults.clipboardDebounce, const Duration(seconds: 2));
    });

    test('API timeout is reasonable', () {
      expect(AppDefaults.apiTimeout.inSeconds, greaterThanOrEqualTo(10));
      expect(AppDefaults.apiTimeout.inSeconds, lessThanOrEqualTo(60));
    });

    test('Hive box names are non-empty', () {
      expect(AppDefaults.hiveClipBox, isNotEmpty);
      expect(AppDefaults.hiveNoteBox, isNotEmpty);
      expect(AppDefaults.hiveSettingsBox, isNotEmpty);
    });
  });
}
