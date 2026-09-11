import 'package:clip_sync_ai/main.dart';
import 'package:clip_sync_ai/services/embedding_service.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('cosineSimilarity', () {
    test('same direction scores 1', () {
      expect(cosineSimilarity([1, 0, 0], [1, 0, 0]), 1);
    });

    test('orthogonal scores 0', () {
      expect(cosineSimilarity([1, 0], [0, 1]), 0);
    });

    test('opposite scores -1', () {
      expect(cosineSimilarity([1, 1], [-1, -1]), closeTo(-1, 1e-9));
    });

    test('mismatched, empty and zero vectors score 0, never NaN', () {
      expect(cosineSimilarity([1, 2], [1, 2, 3]), 0);
      expect(cosineSimilarity([], []), 0);
      expect(cosineSimilarity([0, 0], [1, 2]), 0);
      for (final v in [
        cosineSimilarity([1, 2], [3]),
        cosineSimilarity([], []),
      ]) {
        expect(v.isNaN, isFalse);
      }
    });
  });

  group('rankClipsVector', () {
    ClipEntry clip(
      String raw, {
      List<double>? vec,
      String vecModel = 'test-model',
    }) =>
        ClipEntry(
          rawText: raw,
          processedMarkdown: raw,
          embedding: vec,
          embeddingModel: vec == null ? '' : vecModel,
        );

    test('orders by meaning, closest first', () {
      final near = clip('aaa', vec: [0.9, 0.1]);
      final far = clip('bbb', vec: [0.1, 0.9]);
      final ranked = rankClipsVector(
        'q',
        [1.0, 0.0],
        [far, near],
        modelId: 'test-model',
      );
      expect(ranked.map((e) => e.rawText), ['aaa', 'bbb']);
    });

    test('vectors from another model are never compared', () {
      final foreign = clip('foreign', vec: [1.0, 0.0], vecModel: 'other');
      final local = clip('local', vec: [0.0, 1.0]);
      final ranked = rankClipsVector(
        'q',
        [1.0, 0.0],
        [foreign, local],
        modelId: 'test-model',
      );
      // foreign drops to the keyword-ordered tail, local leads by cosine.
      expect(ranked.first.rawText, 'local');
      expect(ranked.map((e) => e.rawText), contains('foreign'));
    });

    test('length alone never qualifies a vector', () {
      final foreign = clip('foreign', vec: [1.0, 0.0], vecModel: 'other');
      final ranked = rankClipsVector(
        'q',
        [1.0, 0.0],
        [foreign],
        modelId: 'test-model',
      );
      // Same length, wrong model: keyword order, not a cosine of 1.
      expect(ranked, hasLength(1));
    });

    test('unindexed clips keep keyword order underneath', () {
      final indexed = clip('indexed', vec: [1.0, 0.0]);
      final plain = clip('plain notes here');
      final ranked = rankClipsVector(
        'notes',
        [0.0, 1.0],
        [indexed, plain],
        modelId: 'test-model',
      );
      expect(ranked.first.rawText, 'indexed');
      expect(ranked.last.rawText, 'plain notes here');
    });
  });

  group('clip vectors survive the box', () {
    test('toMap/fromMap round-trip', () {
      final e = ClipEntry(
        rawText: 'r',
        processedMarkdown: 'p',
        embedding: [0.5, -0.25, 0.0],
        embeddingModel: 'test-model',
      );
      final back = ClipEntry.fromMap(e.toMap());
      expect(back.embedding, [0.5, -0.25, 0.0]);
      expect(back.embeddingModel, 'test-model');
    });

    test('clips without vectors read back clean', () {
      final back = ClipEntry.fromMap(
        ClipEntry(rawText: 'r', processedMarkdown: 'p').toMap(),
      );
      expect(back.embedding, isNull);
      expect(back.embeddingModel, isEmpty);
    });
  });

  group('EmbeddingService', () {
    test('starts cold: not ready, embed answers null', () async {
      final svc = EmbeddingService();
      expect(svc.isReady, isFalse);
      expect(svc.modelId, isNull);
      expect(await svc.embed('hello'), isNull);
    });
  });
}
