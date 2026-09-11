import 'dart:io';

import 'package:clip_sync_ai/main.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hive/hive.dart';

void main() {
  group('clipSimilarity', () {
    test('identical texts score 1', () {
      expect(clipSimilarity('hello world', 'hello world'), 1);
    });

    test('case and whitespace do not matter', () {
      expect(clipSimilarity('  Hello   WORLD ', 'hello world'), 1);
    });

    test('disjoint texts score 0', () {
      expect(clipSimilarity('cats purr softly', 'quantum field theory'), 0);
    });

    test('partial overlap lands between', () {
      final s = clipSimilarity(
        'the quarterly planning notes for june',
        'quarterly planning notes draft',
      );
      expect(s, greaterThan(0.4));
      expect(s, lessThan(1));
    });
  });

  group('findSimilarClip', () {
    ClipEntry clip(String raw, {DateTime? at}) => ClipEntry(
          rawText: raw,
          processedMarkdown: raw,
          timestamp: at ?? DateTime(2026, 1, 1),
        );

    test('finds a normalized-equal clip', () {
      final kept = clip('Deploy   Friday');
      final found = findSimilarClip(
        'deploy friday',
        [kept, clip('something else entirely different here')],
      );
      expect(found?.id, kept.id);
    });

    test('finds a near-duplicate above threshold', () {
      final kept = clip(
        'quarterly planning notes for june with action items and owners',
      );
      final found = findSimilarClip(
        'quarterly planning notes for june with action items, owners and dates',
        [kept],
      );
      expect(found?.id, kept.id);
    });

    test('ignores unrelated clips', () {
      final found = findSimilarClip(
        'buy milk and eggs on the way home tonight',
        [clip('the kubernetes deployment pipeline failed again this morning')],
      );
      expect(found, isNull);
    });

    test('short texts only match exactly', () {
      final found = findSimilarClip('ok', [clip('ok sure')]);
      expect(found, isNull);
    });
  });

  group('rankClips', () {
    ClipEntry clip(
      String raw, {
      String title = '',
      List<String> tags = const [],
      DateTime? at,
    }) =>
        ClipEntry(
          rawText: raw,
          processedMarkdown: raw,
          title: title,
          tags: tags,
          timestamp: at ?? DateTime(2026, 1, 1),
        );

    test('a title hit outranks a body hit', () {
      final titled = clip('weekly sync notes and updates', title: 'Q3 planning');
      final bodied = clip('some notes about q3 planning deadlines');
      final ranked = rankClips('planning', [bodied, titled]);
      expect(ranked.first.id, titled.id);
    });

    test('no match means no rows, not every row', () {
      final ranked = rankClips('zebra', [clip('plain meeting notes here')]);
      expect(ranked, isEmpty);
    });

    test('empty query keeps feed order', () {
      final a = clip('aaa');
      final b = clip('bbb');
      expect(rankClips('', [a, b]).map((e) => e.id), [a.id, b.id]);
    });
  });

  group('titles and tags', () {
    test('heuristic takes the first line and frequent words', () async {
      final meta = await titleAndTags(
        'Q3 planning notes\nq3 goals, q3 budget, q3 hiring plan',
        OllamaClipProcessor(),
      );
      expect(meta.title, 'Q3 planning notes');
      expect(meta.tags, contains('planning'));
    });

    test('model-format answers parse', () {
      final parsed = parseTitleTags('TITLE: Weekend shopping\nTAGS: home, food, x');
      expect(parsed.title, 'Weekend shopping');
      expect(parsed.tags, ['home', 'food', 'x']);
    });
  });

  group('retention and auto-redact', () {
    late Directory dir;

    setUpAll(() async {
      dir = Directory.systemTemp.createTempSync('smart_clips');
      Hive.init(dir.path);
      await Hive.openBox(AppDefaults.hiveSettingsBox);
      await Hive.openBox(AppDefaults.hiveClipBox);
    });

    tearDownAll(() async {
      await Hive.box(AppDefaults.hiveClipBox).clear();
      await Hive.box(AppDefaults.hiveSettingsBox).clear();
      await Hive.close();
      dir.deleteSync(recursive: true);
    });

    setUp(() async {
      await Hive.box(AppDefaults.hiveClipBox).clear();
      await Hive.box(AppDefaults.hiveSettingsBox).clear();
    });

    test('purge burns old clips, keeps fresh and pinned ones', () async {
      final box = Hive.box(AppDefaults.hiveClipBox);
      final settings = Hive.box(AppDefaults.hiveSettingsBox);
      await settings.put('clipRetentionDays', 7);
      final now = DateTime.now();
      final old = ClipEntry(
        rawText: 'old',
        processedMarkdown: 'old',
        timestamp: now.subtract(const Duration(days: 30)),
      );
      final fresh = ClipEntry(
        rawText: 'fresh',
        processedMarkdown: 'fresh',
        timestamp: now,
      );
      final pinnedOld = ClipEntry(
        rawText: 'pinned',
        processedMarkdown: 'pinned',
        timestamp: now.subtract(const Duration(days: 30)),
        isPinned: true,
      );
      for (final e in [old, fresh, pinnedOld]) {
        await box.put(e.id, e.toMap());
      }
      final burned = await purgeExpiredClips();
      expect(burned, 1);
      expect(box.containsKey(old.id), isFalse);
      expect(box.containsKey(fresh.id), isTrue);
      expect(box.containsKey(pinnedOld.id), isTrue);
    });

    test('zero retention keeps everything', () async {
      final box = Hive.box(AppDefaults.hiveClipBox);
      final ancient = ClipEntry(
        rawText: 'ancient',
        processedMarkdown: 'ancient',
        timestamp: DateTime(2020, 1, 1),
      );
      await box.put(ancient.id, ancient.toMap());
      expect(await purgeExpiredClips(), 0);
      expect(box.containsKey(ancient.id), isTrue);
    });

    test('auto-redact is off unless switched on', () async {
      const secret = 'key sk-abcdefghij1234567890xyz';
      expect(applyAutoRedact(secret), secret);
      await Hive.box(AppDefaults.hiveSettingsBox).put('autoRedact', true);
      expect(applyAutoRedact(secret), contains('[redacted:openai-key]'));
    });
  });
}
