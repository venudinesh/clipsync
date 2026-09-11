import 'dart:async';
import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;
import 'package:path_provider/path_provider.dart';

import 'package:llamadart/llamadart.dart';

// ─────────────────────────────────────────────────────────────────────────────
// On-Device LLM Service
//
// Wraps llamadart (which wraps llama.cpp — the same engine Ollama uses) to
// provide fully offline, in-app LLM inference. No server process required.
//
// Supports:
//   • GGUF model loading from local file
//   • Streaming token generation
//   • Chat with system/user/assistant roles
//   • Model lifecycle: load, generate, dispose
//   • Automatic model directory management
// ─────────────────────────────────────────────────────────────────────────────

/// Curated list of small GGUF models suitable for mobile devices.
class ModelCatalogEntry {
  final String id;
  final String displayName;
  final String description;
  final String hfRepo;
  final String hfFile;
  final int sizeBytes;
  final String quantization;
  final int parameterCount; // in millions
  final String category; // general, coding, conversation
  final int recommendedRamMB;
  final double estimatedTokPerSec;

  const ModelCatalogEntry({
    required this.id,
    required this.displayName,
    required this.description,
    required this.hfRepo,
    required this.hfFile,
    required this.sizeBytes,
    required this.quantization,
    required this.parameterCount,
    required this.category,
    required this.recommendedRamMB,
    required this.estimatedTokPerSec,
  });

  String get hfUrl => 'https://huggingface.co/$hfRepo/resolve/main/$hfFile';
  String get sizeLabel {
    if (sizeBytes < 1024 * 1024) return '${(sizeBytes / 1024).toStringAsFixed(0)} KB';
    if (sizeBytes < 1024 * 1024 * 1024) return '${(sizeBytes / (1024 * 1024)).toStringAsFixed(0)} MB';
    return '${(sizeBytes / (1024 * 1024 * 1024)).toStringAsFixed(1)} GB';
  }
  String get paramLabel => parameterCount >= 1000
      ? '${(parameterCount / 1000).toStringAsFixed(1)}B'
      : '${parameterCount}M';
}

/// Curated catalog of small, mobile-friendly GGUF models.
const List<ModelCatalogEntry> kModelCatalog = [
  // ── Ultra-Light (< 500 MB) ────────────────────────────────────────────
  ModelCatalogEntry(
    id: 'smollm2-135m',
    displayName: 'SmolLM2 135M',
    description: 'Ultra-light model for quick text tasks. Runs on any device.',
    hfRepo: 'unsloth/SmolLM2-135M-Instruct-GGUF',
    hfFile: 'SmolLM2-135M-Instruct-Q4_K_M.gguf',
    sizeBytes: 107374182, // ~102 MB
    quantization: 'Q4_K_M',
    parameterCount: 135,
    category: 'general',
    recommendedRamMB: 512,
    estimatedTokPerSec: 60.0,
  ),
  ModelCatalogEntry(
    id: 'qwen2.5-0.5b',
    displayName: 'Qwen2.5 0.5B',
    description: 'Compact model from Alibaba. Good at instructions and coding.',
    hfRepo: 'Qwen/Qwen2.5-0.5B-Instruct-GGUF',
    hfFile: 'qwen2.5-0.5b-instruct-q4_k_m.gguf',
    sizeBytes: 387973120, // ~370 MB
    quantization: 'Q4_K_M',
    parameterCount: 500,
    category: 'general',
    recommendedRamMB: 768,
    estimatedTokPerSec: 45.0,
  ),
  ModelCatalogEntry(
    id: 'phi-3.5-mini',
    displayName: 'Phi-3.5 Mini',
    description: 'Microsoft compact model. Strong reasoning for its size.',
    hfRepo: 'unsloth/phi-3.5-mini-instruct-GGUF',
    hfFile: 'phi-3.5-mini-instruct-Q4_K_M.gguf',
    sizeBytes: 234881024, // ~224 MB
    quantization: 'Q4_K_M',
    parameterCount: 3800,
    category: 'general',
    recommendedRamMB: 1024,
    estimatedTokPerSec: 30.0,
  ),

  // ── Small (500 MB – 2 GB) ─────────────────────────────────────────────
  ModelCatalogEntry(
    id: 'llama3.2-1b',
    displayName: 'Llama 3.2 1B',
    description: 'Meta lightweight. Excellent balance of quality and speed.',
    hfRepo: 'unsloth/Llama-3.2-1B-Instruct-GGUF',
    hfFile: 'Llama-3.2-1B-Instruct-Q4_K_M.gguf',
    sizeBytes: 721420288, // ~688 MB
    quantization: 'Q4_K_M',
    parameterCount: 1200,
    category: 'general',
    recommendedRamMB: 1536,
    estimatedTokPerSec: 40.0,
  ),
  ModelCatalogEntry(
    id: 'gemma2-2b',
    displayName: 'Gemma 2 2B',
    description: 'Google compact model. Good general knowledge.',
    hfRepo: 'unsloth/gemma-2-2b-it-GGUF',
    hfFile: 'gemma-2-2b-it-Q4_K_M.gguf',
    sizeBytes: 1610612736, // ~1.5 GB
    quantization: 'Q4_K_M',
    parameterCount: 2600,
    category: 'general',
    recommendedRamMB: 2048,
    estimatedTokPerSec: 30.0,
  ),
  ModelCatalogEntry(
    id: 'codellama-3b',
    displayName: 'CodeLlama 3B',
    description: 'Meta coding model. Good for code generation and review.',
    hfRepo: 'unsloth/codellama-3b-instruct-GGUF',
    hfFile: 'codellama-3b-instruct-Q4_K_M.gguf',
    sizeBytes: 1929379840, // ~1.8 GB
    quantization: 'Q4_K_M',
    parameterCount: 3300,
    category: 'coding',
    recommendedRamMB: 2560,
    estimatedTokPerSec: 25.0,
  ),

  // ── Medium (2 GB+) ────────────────────────────────────────────────────
  ModelCatalogEntry(
    id: 'llama3.2-3b',
    displayName: 'Llama 3.2 3B',
    description: 'Meta mid-range. Strong general and instruction following.',
    hfRepo: 'unsloth/Llama-3.2-3B-Instruct-GGUF',
    hfFile: 'Llama-3.2-3B-Instruct-Q4_K_M.gguf',
    sizeBytes: 2147483648, // ~2.0 GB
    quantization: 'Q4_K_M',
    parameterCount: 3200,
    category: 'general',
    recommendedRamMB: 3072,
    estimatedTokPerSec: 25.0,
  ),
  ModelCatalogEntry(
    id: 'qwen2.5-3b',
    displayName: 'Qwen2.5 3B',
    description: 'Alibaba strong mid-range. Excellent multilingual support.',
    hfRepo: 'Qwen/Qwen2.5-3B-Instruct-GGUF',
    hfFile: 'qwen2.5-3b-instruct-q4_k_m.gguf',
    sizeBytes: 2040109465, // ~1.9 GB
    quantization: 'Q4_K_M',
    parameterCount: 3000,
    category: 'general',
    recommendedRamMB: 3072,
    estimatedTokPerSec: 28.0,
  ),
];

/// Model download progress callback.
typedef DownloadProgress = void Function(double progress, int received, int total);

/// Live state of an in-progress model download.
class DownloadState {
  final String catalogId;
  final String modelName;
  final double progress; // 0.0 – 1.0
  final int receivedBytes;
  final int totalBytes;
  final String status; // downloading, done, error, cancelled
  final String? error;

  const DownloadState({
    required this.catalogId,
    required this.modelName,
    required this.progress,
    required this.receivedBytes,
    required this.totalBytes,
    required this.status,
    this.error,
  });

  DownloadState copyWith({
    double? progress,
    int? receivedBytes,
    int? totalBytes,
    String? status,
    String? error,
  }) {
    return DownloadState(
      catalogId: catalogId,
      modelName: modelName,
      progress: progress ?? this.progress,
      receivedBytes: receivedBytes ?? this.receivedBytes,
      totalBytes: totalBytes ?? this.totalBytes,
      status: status ?? this.status,
      error: error ?? this.error,
    );
  }
}

/// Manages on-device LLM inference using llama.cpp via llamadart.
class OnDeviceLlmService {
  // ── Singleton ────────────────────────────────────────────────────────────
  static final OnDeviceLlmService _instance = OnDeviceLlmService._internal();
  factory OnDeviceLlmService() => _instance;
OnDeviceLlmService._internal();

  /// Maximum characters a single prompt/OCR input is allowed to contribute.
  static const int _maxPromptChars = 8192;

  // ── State ────────────────────────────────────────────────────────────────
  LlamaEngine? _engine;
  ChatSession? _session;
  bool _isLoaded = false;
  bool _isGenerating = false;
  String? _loadedModelPath;
  String? _loadedModelId;

  /// Sanitizes raw user text (e.g. OCR output) before it reaches llama.cpp.
  ///
  /// OCR output frequently contains null bytes, control characters and
  /// over-long single lines that can crash the native tokenizer/engine.
  /// We strip invalid chars (keeping common whitespace/newlines) and cap the
  /// length so the model never receives malformed or unbounded context.
  static String sanitizeInput(String input) {
    if (input.isEmpty) return input;
    final buffer = StringBuffer();
    var count = 0;
    for (var i = 0; i < input.length; i++) {
      if (count >= _maxPromptChars) break;
      final code = input.codeUnitAt(i);
      final isNullOrControl =
          code == 0 || (code < 9) || (code > 13 && code < 32) || code == 127;
      if (isNullOrControl) continue;
      buffer.writeCharCode(code);
      count++;
    }
    var result = buffer.toString().trim();
    if (result.length > _maxPromptChars) {
      result = result.substring(0, _maxPromptChars);
    }
    return result;
  }

  // ── Getters ──────────────────────────────────────────────────────────────
  bool get isLoaded => _isLoaded;
  bool get isGenerating => _isGenerating;
  String? get loadedModelId => _loadedModelId;
  String? get loadedModelPath => _loadedModelPath;

  /// Gets the models directory for storing downloaded GGUF files.
  Future<Directory> getModelsDirectory() async {
    final appDir = await getApplicationDocumentsDirectory();
    final modelsDir = Directory('${appDir.path}/models');
    if (!await modelsDir.exists()) {
      await modelsDir.create(recursive: true);
    }
    return modelsDir;
  }

  /// Lists all downloaded GGUF model files.
  Future<List<FileSystemEntity>> listDownloadedModels() async {
    final modelsDir = await getModelsDirectory();
    return modelsDir
        .listSync()
        .where((f) => f.path.endsWith('.gguf'))
        .toList();
  }

  /// Checks if a catalog model is already downloaded.
  /// Requires the file to exist AND be close to the expected size, so a
  /// truncated/partial download is not shown as "Downloaded" (a corrupt file
  /// would otherwise fail to load later).
  Future<bool> isModelDownloaded(ModelCatalogEntry catalog) async {
    final modelsDir = await getModelsDirectory();
    final file = File('${modelsDir.path}/${catalog.hfFile}');
    if (!await file.exists()) return false;
    final length = await file.length();
    return length >= catalog.sizeBytes * 0.95;
  }

  /// Gets the local path for a catalog model.
  Future<String> getLocalModelPath(ModelCatalogEntry catalog) async {
    final modelsDir = await getModelsDirectory();
    return '${modelsDir.path}/${catalog.hfFile}';
  }

  // ── Model Download ───────────────────────────────────────────────────────

  /// Global, app-lifetime map of active/past downloads. Keyed by catalog id.
  /// Any screen (Settings, a notification, a model browser) can watch this via
  /// [ValueListenable] and reflect live progress even if the download was
  /// started on a different screen (i.e. "backgrounded").
  static final ValueNotifier<Map<String, DownloadState>> _downloads =
      ValueNotifier({});
  static final Map<String, bool> _activeTasks = {};
  /// Tracks cancelled downloads so in-flight fetch() loops stop writing and
  /// avoid corrupting fragments when a download is restarted.
  static final Set<String> _cancelled = {};

  /// Listenable snapshot of all model downloads.
  ValueListenable<Map<String, DownloadState>> get downloadStates => _downloads;

  /// True if [catalogId] is currently downloading.
  bool isDownloading(String catalogId) => _activeTasks[catalogId] ?? false;

  /// Simple sequential download (kept for callers that want a blocking await).
  /// Prefer [startDownload] for the UI so downloads survive screen changes.
  Future<String> downloadModel(
    ModelCatalogEntry catalog, {
    DownloadProgress? onProgress,
  }) async {
    return startDownload(catalog, onProgress: onProgress);
  }

  /// Starts a fast, resumable, parallel (ranged) download that keeps running
  /// in the background (app-level state) and reports progress through the
  /// global [downloadStates] notifier. Avoiding a single long-lived HTTP
  /// stream makes downloads noticeably faster on mobile networks and far more
  /// tolerant of drops (each chunk is a separate request and retries on its own).
  Future<String> startDownload(
    ModelCatalogEntry catalog, {
    int parallel = 4,
    DownloadProgress? onProgress,
  }) async {
    final id = catalog.id;

    // Already fully downloaded.
    if (await isModelDownloaded(catalog) &&
        await File(await getLocalModelPath(catalog)).exists()) {
      _publish(DownloadState(
        catalogId: id,
        modelName: catalog.displayName,
        progress: 1.0,
        receivedBytes: catalog.sizeBytes,
        totalBytes: catalog.sizeBytes,
        status: 'done',
      ));
      return getLocalModelPath(catalog);
    }

    // If a task is already active, don't double-start; just await its result.
    if (_activeTasks[id] == true) {
      return getLocalModelPath(catalog);
    }
    _activeTasks[id] = true;
    _cancelled.remove(id); // clear any prior cancellation signal

    final modelsDir = await getModelsDirectory();
    final destPath = '${modelsDir.path}/${catalog.hfFile}';
    const maxRetries = 3;

    try {
      _publish(DownloadState(
        catalogId: id,
        modelName: catalog.displayName,
        progress: 0.0,
        receivedBytes: 0,
        totalBytes: catalog.sizeBytes,
        status: 'downloading',
      ));

      final total = await _resolveTotalSize(catalog);

      // Recover any previously-completed fragment files.
      final fragments = await _recoverFragments(destPath, total, parallel);

      final downloadedSoFar = fragments.fold<int>(
          0, (sum, f) => sum + (f.lengthSync()));

      _publish(DownloadState(
        catalogId: id,
        modelName: catalog.displayName,
        progress: downloadedSoFar / total,
        receivedBytes: downloadedSoFar,
        totalBytes: total,
        status: 'downloading',
      ));

      // Download any missing byte-ranges concurrently.
      await _downloadRanges(
        catalog,
        destPath,
        total,
        downloadedSoFar,
        parallel,
        maxRetries: maxRetries,
      );

      // Reassemble fragments into a single .gguf.
      final finalPath = await _assemble(destPath, total, parallel);

      // Verify integrity.
      final size = await File(finalPath).length();
      if (size < catalog.sizeBytes * 0.95) {
        File(finalPath).deleteSync();
        throw Exception(
          'Incomplete download: got $size bytes, expected ~${catalog.sizeBytes}. '
          'Please try again.',
        );
      }

      _publish(DownloadState(
        catalogId: id,
        modelName: catalog.displayName,
        progress: 1.0,
        receivedBytes: total,
        totalBytes: total,
        status: 'done',
      ));

      onProgress?.call(1.0, total, total);
      debugPrint('[OnDeviceLLM] Download complete: $finalPath ($size bytes)');
      return finalPath;
    } catch (e, st) {
      debugPrint('[OnDeviceLLM] Download failed: $e\n$st');
      _publish(DownloadState(
        catalogId: id,
        modelName: catalog.displayName,
        progress: 0.0,
        receivedBytes: 0,
        totalBytes: catalog.sizeBytes,
        status: 'error',
        error: e.toString(),
      ));
      rethrow;
    } finally {
      _activeTasks[id] = false;
    }
  }

  /// Announces a state change to any listening screens.
  void _publish(DownloadState s) {
    final next = Map<String, DownloadState>.from(_downloads.value);
    next[s.catalogId] = s;
    _downloads.value = next;
  }

  /// GET HEAD (or a tiny range GET) to learn the exact content length.
  Future<int> _resolveTotalSize(ModelCatalogEntry catalog) async {
    final client = http.Client();
    try {
      final r = await client.head(Uri.parse(catalog.hfUrl)).timeout(
            const Duration(seconds: 20),
          );
      final len = r.contentLength;
      if (len != null && len > 0) return len;
    } catch (_) {/* fall back to catalog size */}
    finally {
      client.close();
    }
    return catalog.sizeBytes;
  }

  /// Discovers existing `.partN` fragments so a restarted download resumes.
  Future<List<File>> _recoverFragments(String destPath, int total, int parts) async {
    final frags = <File>[];
    for (var i = 0; i < parts; i++) {
      final f = File('$destPath.part$i');
      if (await f.exists()) frags.add(f);
    }
    return frags;
  }

  /// Downloads the byte ranges that are still missing, split into [parallel]
  /// independent ranged requests. Each request retries up to [maxRetries].
  Future<void> _downloadRanges(
    ModelCatalogEntry catalog,
    String destPath,
    int total,
    int alreadyDownloaded,
    int parallel, {
    required int maxRetries,
  }) async {
    final parts = parallel;
    final chunkSize = (total / parts).ceil();

    Future<void> fetch(int partIdx) async {
      // Bail out early if this download was cancelled.
      if (_cancelled.contains(catalog.id)) return;

      final start = partIdx * chunkSize;
      final end = ((partIdx + 1) * chunkSize - 1).clamp(0, total - 1);
      if (start >= total) return;
      final rangeLength = end - start + 1;

      // Skip already-written parts.
      final partFile = File('$destPath.part$partIdx');
      if (await partFile.exists() &&
          await partFile.length() >= rangeLength) {
        return;
      }

      var attempts = 0;
      while (attempts <= maxRetries) {
        if (_cancelled.contains(catalog.id)) return;
        try {
          final client = http.Client();
          try {
            final req = http.Request('GET', Uri.parse(catalog.hfUrl));
            req.headers['Range'] = 'bytes=$start-$end';

            final res = await client.send(req).timeout(const Duration(minutes: 5));
            if (res.statusCode == 200 || res.statusCode == 206) {
              final sink = partFile.openWrite();
              try {
                await for (final chunk in res.stream) {
                  if (_cancelled.contains(catalog.id)) break;
                  sink.add(chunk);
                  _publishProgress(catalog, total, alreadyDownloaded);
                }
              } finally {
                await sink.close();
              }
              if (await partFile.length() >= rangeLength) {
                return;
              }
              // Short read -> retry.
            }
          } finally {
            client.close();
          }
        } catch (_) {
          // fall through to retry
        }
        attempts++;
        await Future<void>.delayed(Duration(milliseconds: 500 * attempts));
      }
      debugPrint('[OnDeviceLLM] Part $partIdx failed after retries');
    }

    await Future.wait(List.generate(parts, fetch));
  }

  /// Publishes aggregate progress across all fragments (best-effort).
  void _publishProgress(ModelCatalogEntry catalog, int total, int baseline) {
    final modelsDirFuture = getModelsDirectory();
    modelsDirFuture.then((dir) async {
      int got = baseline;
      for (final f in (await dir.listSync().toList())) {
        final name = f.path.split(Platform.pathSeparator).last;
        if (name.startsWith('${catalog.hfFile}.part')) {
          got += await File(f.path).length();
        }
      }
      final p = total > 0 ? (got / total).clamp(0.0, 1.0) : 0.0;
      _publish(DownloadState(
        catalogId: catalog.id,
        modelName: catalog.displayName,
        progress: p,
        receivedBytes: got,
        totalBytes: total,
        status: 'downloading',
      ));
    });
  }

  /// Merges all `.partN` fragments into the final `.gguf` in order.
  /// Streams the data instead of buffering whole files in memory (avoids OOM
  /// on large GGUF models).
  Future<String> _assemble(String destPath, int total, int parts) async {
    final sink = File(destPath).openWrite();
    try {
      for (var i = 0; i < parts; i++) {
        final f = File('$destPath.part$i');
        if (await f.exists()) {
          await sink.addStream(f.openRead());
        }
      }
      await sink.close();
    } catch (e) {
      await sink.close();
      rethrow;
    }
    // Clean up fragments.
    for (var i = 0; i < parts; i++) {
      final f = File('$destPath.part$i');
      if (await f.exists()) await f.delete();
    }
    return destPath;
  }

  /// Cancels an in-progress download for a catalog model.
  Future<void> cancelDownload(String catalogId) async {
    _cancelled.add(catalogId); // signal running fetch() loops to stop
    _activeTasks[catalogId] = false;
    final current = _downloads.value[catalogId];
    if (current != null) {
      _publish(current.copyWith(status: 'cancelled', progress: 0.0));
    }
  }

  /// Deletes a downloaded model file.
  Future<void> deleteModel(String filePath) async {
    final file = File(filePath);
    if (await file.exists()) {
      if (_loadedModelPath == filePath) {
        await disposeEngine();
      }
      await file.delete();
      debugPrint('[OnDeviceLLM] Deleted model: $filePath');
    }
  }

  // ── Engine Lifecycle ─────────────────────────────────────────────────────

  /// Loads a GGUF model file and initializes the llama.cpp engine.
  /// Uses engine.loadModel(path) — the correct llamadart API.
  Future<void> loadModel(
    String modelPath, {
    String? modelId,
    int contextSize = 4096,
  }) async {
    await disposeEngine();

    debugPrint('[OnDeviceLLM] Loading model: $modelPath');

    // Pre-flight sanity check: reject obviously-truncated files with a clear
    // message before llama.cpp tries (and fails) to parse them.
    final modelFile = File(modelPath);
    if (!await modelFile.exists()) {
      throw Exception('Model file not found: $modelPath');
    }
    final fileSize = await modelFile.length();
    if (fileSize < 1024 * 1024) {
      throw Exception(
        'Model file is too small (${(fileSize / 1024).toStringAsFixed(0)} KB). '
        'The file is likely corrupt or truncated. Try downloading it again.',
      );
    }
    debugPrint('[OnDeviceLLM] File size: $fileSize bytes');

    try {
      _engine = LlamaEngine(LlamaBackend());

      // Debug level in a debug build, so a failed load leaves the native
      // llama.cpp error in logcat. Not in release: llama.cpp is chatty enough
      // at debug level that the FFI logging alone measurably slows the load,
      // and a warning is all a shipped build has any use for.
      await _engine!.setLogLevel(
        kDebugMode ? LlamaLogLevel.debug : LlamaLogLevel.warn,
      );

      // loadModel takes a plain string path (NOT ModelSource)
      await _engine!.loadModel(
        modelPath,
        modelParams: ModelParams(
          gpuLayers: 0, // CPU-only for reliability
          contextSize: contextSize,
        ),
      );

      // Create a ChatSession for convenient multi-turn chat
      _session = ChatSession(_engine!);

      _loadedModelPath = modelPath;
      _loadedModelId = modelId ?? modelPath.split(Platform.pathSeparator).last;
      _isLoaded = true;

      debugPrint('[OnDeviceLLM] ✅ Model loaded successfully: $_loadedModelId');
    } catch (e, stackTrace) {
      _isLoaded = false;
      _engine = null;
      _session = null;
      debugPrint('[OnDeviceLLM] ❌ Failed to load model: $e');
      debugPrint('[OnDeviceLLM] Stack trace: $stackTrace');
      rethrow;
    }
  }

  /// Disposes the current engine and frees resources.
  Future<void> disposeEngine() async {
    if (_engine != null) {
      try {
        await _engine!.dispose();
        debugPrint('[OnDeviceLLM] Engine disposed');
      } catch (e) {
        debugPrint('[OnDeviceLLM] Error disposing engine: $e');
      }
      _engine = null;
      _session = null;
      _isLoaded = false;
      _isGenerating = false;
      _loadedModelPath = null;
      _loadedModelId = null;
    }
  }

  // ── Text Generation ──────────────────────────────────────────────────────

  /// Generates text using a prompt with streaming output.
  /// Uses engine.create() directly with LlamaChatMessage list.
  Stream<String> generateStream(
    String prompt, {
    int maxTokens = 512,
    double temperature = 0.7,
    double topP = 0.9,
  }) async* {
    if (!_isLoaded || _engine == null) {
      throw StateError('No model loaded. Call loadModel() first.');
    }

    _isGenerating = true;

    try {
      final messages = [
        LlamaChatMessage(
          role: 'user',
          content: sanitizeInput(prompt),
        ),
      ];

      final stream = _engine!.create(
        messages,
        params: GenerationParams(
          maxTokens: maxTokens,
          temp: temperature,
          topP: topP,
        ),
      );

      await for (final chunk in stream) {
        final content = chunk.choices.first.delta.content;
        if (content != null && content.isNotEmpty) {
          yield content;
        }
      }
    } catch (e, stackTrace) {
      debugPrint('[OnDeviceLLM] Generation error: $e');
      debugPrint('[OnDeviceLLM] Stack trace: $stackTrace');
      rethrow;
    } finally {
      _isGenerating = false;
    }
  }

  /// Generates text and returns the complete result.
  Future<String> generate(
    String prompt, {
    int maxTokens = 512,
    double temperature = 0.7,
    double topP = 0.9,
  }) async {
    final buffer = StringBuffer();
    await for (final token in generateStream(
      prompt,
      maxTokens: maxTokens,
      temperature: temperature,
      topP: topP,
    )) {
      buffer.write(token);
    }
    return buffer.toString();
  }

  /// Generates a chat response with system prompt and conversation history.
  Stream<String> chatStream(
    List<ChatMessage> messages, {
    int maxTokens = 512,
    double temperature = 0.7,
    double topP = 0.9,
  }) async* {
    if (!_isLoaded || _engine == null) {
      throw StateError('No model loaded. Call loadModel() first.');
    }

    _isGenerating = true;

    try {
      final llamaMessages = messages.map((m) {
        return LlamaChatMessage(
          role: m.role,
          content: sanitizeInput(m.content),
        );
      }).toList();

      final stream = _engine!.create(
        llamaMessages,
        params: GenerationParams(
          maxTokens: maxTokens,
          temp: temperature,
          topP: topP,
        ),
      );

      await for (final chunk in stream) {
        final content = chunk.choices.first.delta.content;
        if (content != null && content.isNotEmpty) {
          yield content;
        }
      }
    } catch (e, stackTrace) {
      debugPrint('[OnDeviceLLM] Chat generation error: $e');
      debugPrint('[OnDeviceLLM] Stack trace: $stackTrace');
      rethrow;
    } finally {
      _isGenerating = false;
    }
  }

  // ── Convenience Methods for ClipSync ─────────────────────────────────────

  /// Processes raw clipboard text into structured Markdown.
  Future<String> processClipboardText(String rawText) async {
    if (!_isLoaded || _engine == null) return '';

    final prompt = '''Convert the following raw text into clean, well-structured Markdown:
- Add bullet points for lists
- Format dates with 📅 emoji
- Make URLs clickable [link](url)
- Extract key information
- Preserve code blocks
- Add headers where appropriate
- Be concise and clear

Raw text:
$rawText''';

    return generate(prompt, maxTokens: 1024, temperature: 0.3);
  }

  /// Summarizes text using the local model.
  Future<String> summarizeText(String text) async {
    if (!_isLoaded || _engine == null) return '[Model not loaded]';
    return generate('Summarize the following text in 2-3 concise bullet points:\n\n$text',
        maxTokens: 256, temperature: 0.3);
  }

  /// Extracts action items from text.
  Future<String> extractActionItems(String text) async {
    if (!_isLoaded || _engine == null) return '[Model not loaded]';
    return generate('Extract action items from the following text as a checklist:\n\n$text',
        maxTokens: 512, temperature: 0.3);
  }

  /// Expands abbreviated points into fuller explanations.
  Future<String> expandPoints(String text) async {
    if (!_isLoaded || _engine == null) return '[Model not loaded]';
    return generate(
        'Expand the following abbreviated notes into fuller, more detailed explanations:\n\n$text',
        maxTokens: 1024, temperature: 0.5);
  }

  // ── Diagnostics ──────────────────────────────────────────────────────────

  Map<String, dynamic> getDiagnostics() {
    return {
      'isLoaded': _isLoaded,
      'isGenerating': _isGenerating,
      'loadedModelId': _loadedModelId,
      'loadedModelPath': _loadedModelPath,
      'engineType': 'llama.cpp (via llamadart)',
      'inferenceMode': _isLoaded ? 'on-device llama.cpp' : 'regex-fallback',
    };
  }

  /// Clears conversation history (call when starting a new chat).
  void clearHistory() {
    _session = _engine != null ? ChatSession(_engine!) : null;
    debugPrint('[OnDeviceLLM] History cleared');
  }
}

/// Simple chat message for the chat API.
class ChatMessage {
  final String role; // system, user, assistant
  final String content;

  const ChatMessage({required this.role, required this.content});
}
