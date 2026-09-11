import 'dart:io';
import 'dart:math';

import 'package:llamadart/llamadart.dart';

import 'on_device_llm_service.dart';

/// The indexing model: small, mean-pooled, downloaded like any other model
/// but never used for chat. Meaning vectors only ever come from this model,
/// so a chat-model swap can never silently corrupt the index.
const ModelCatalogEntry kEmbeddingModel = ModelCatalogEntry(
  id: 'nomic-embed-text-v1.5-q8',
  displayName: 'Nomic Embed 1.5 (indexing)',
  description:
      'Turns clips into meaning vectors for semantic search. Not a chat model.',
  hfRepo: 'nomic-ai/nomic-embed-text-v1.5-GGUF',
  hfFile: 'nomic-embed-text-v1.5.Q8_0.gguf',
  sizeBytes: 150994944, // ~144 MB, approximate; the real size is HEADed.
  quantization: 'Q8_0',
  parameterCount: 137,
  category: 'embedding',
  recommendedRamMB: 512,
  estimatedTokPerSec: 0,
);

/// Cosine similarity of two vectors: 1 for the same direction, 0 for
/// unrelated ones. Mismatched, empty or zero vectors score 0, never NaN.
double cosineSimilarity(List<double> a, List<double> b) {
  if (a.length != b.length || a.isEmpty) return 0;
  var dot = 0.0;
  var na = 0.0;
  var nb = 0.0;
  for (var i = 0; i < a.length; i++) {
    dot += a[i] * b[i];
    na += a[i] * a[i];
    nb += b[i] * b[i];
  }
  if (na == 0 || nb == 0) return 0;
  return (dot / (sqrt(na) * sqrt(nb))).clamp(-1.0, 1.0);
}

/// A dedicated llama.cpp engine for meaning vectors. Separate from the chat
/// engine on purpose: loading the indexer must never unload the model the
/// user is talking to, and the indexer wants a small context, not 4K.
class EmbeddingService {
  LlamaEngine? _engine;
  String? _modelPath;
  String? _modelId;

  bool get isReady => _engine != null;
  String? get modelId => _modelId;
  String? get modelPath => _modelPath;

  Future<void> load(String modelPath, {String? modelId}) async {
    await unload();
    final engine = LlamaEngine(LlamaBackend());
    await engine.setLogLevel(LlamaLogLevel.warn);
    await engine.loadModel(
      modelPath,
      modelParams: const ModelParams(
        gpuLayers: 0, // CPU-only, like the chat engine.
        contextSize: 512, // Embeddings need room for one clip, not a chat.
      ),
    );
    _engine = engine;
    _modelPath = modelPath;
    _modelId = modelId ?? modelPath.split(Platform.pathSeparator).last;
  }

  Future<void> unload() async {
    final engine = _engine;
    _engine = null;
    _modelPath = null;
    _modelId = null;
    if (engine != null) {
      try {
        await engine.dispose();
      } catch (_) {}
    }
  }

  /// A meaning vector for [text], or null when no indexing model is loaded
  /// or the text embeds to nothing. Never throws: search treats null as
  /// "fall back to keywords".
  Future<List<double>?> embed(String text) async {
    final engine = _engine;
    if (engine == null || text.trim().isEmpty) return null;
    try {
      final vec = await engine.embed(text);
      if (vec.isEmpty) return null;
      return vec;
    } catch (_) {
      return null;
    }
  }
}
