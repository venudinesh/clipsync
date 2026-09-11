import 'package:clip_sync_ai/main.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('joinClips', () {
    ClipEntry clip(
      String raw,
      String formatted, {
      DateTime? at,
      bool checklist = false,
    }) =>
        ClipEntry(
          rawText: raw,
          processedMarkdown: formatted,
          timestamp: at ?? DateTime(2026, 1, 1),
          isChecklist: checklist,
        );

    test('orders oldest first regardless of pick order', () {
      final newer = clip('second', 'Second', at: DateTime(2026, 3, 2));
      final older = clip('first', 'First', at: DateTime(2026, 3, 1));
      final joined = joinClips([newer, older]);
      expect(joined.rawText, 'first${kClipJoinSeparator}second');
      expect(joined.processedMarkdown, 'First${kClipJoinSeparator}Second');
    });

    test('falls back to whichever body is non-empty', () {
      final a = clip('', 'Only formatted', at: DateTime(2026, 2, 1));
      final b = clip('Only raw', '', at: DateTime(2026, 2, 2));
      final joined = joinClips([a, b]);
      expect(joined.rawText, 'Only formatted${kClipJoinSeparator}Only raw');
      expect(
        joined.processedMarkdown,
        'Only formatted${kClipJoinSeparator}Only raw',
      );
    });

    test('isChecklist only when every source is a checklist', () {
      final a = clip('a', 'A', checklist: true);
      final b = clip('b', 'B', checklist: true);
      expect(joinClips([a, b]).isChecklist, isTrue);

      final c = clip('c', 'C', checklist: false);
      expect(joinClips([a, c]).isChecklist, isFalse);
    });

    test('joined clip gets a fresh id and timestamp', () {
      final before = DateTime.now().subtract(const Duration(seconds: 1));
      final joined = joinClips([clip('a', 'A'), clip('b', 'B')]);
      expect(joined.id, isNotEmpty);
      expect(joined.timestamp.isAfter(before), isTrue);
      expect(joined.timestamp.isBefore(DateTime.now().add(const Duration(seconds: 5))), isTrue);
    });

    test('three clips join with a rule between each', () {
      final joined = joinClips([
        clip('one', 'One', at: DateTime(2026, 1, 3)),
        clip('two', 'Two', at: DateTime(2026, 1, 1)),
        clip('three', 'Three', at: DateTime(2026, 1, 2)),
      ]);
      expect(
        joined.processedMarkdown,
        'Two${kClipJoinSeparator}Three${kClipJoinSeparator}One',
      );
    });
  });
}
