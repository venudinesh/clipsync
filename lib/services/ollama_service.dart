import 'dart:async';
import 'dart:convert';

import 'package:http/http.dart' as http;

/// Comprehensive Ollama REST API client implementing all endpoints
/// from https://github.com/ollama/ollama/blob/main/docs/api.md
class OllamaService {
  String _baseUrl;
  http.Client _client;

  OllamaService({
    String baseUrl = 'http://127.0.0.1:11434',
    http.Client? client,
  })  : _baseUrl = baseUrl,
        _client = client ?? http.Client();

  // ── Configuration ──────────────────────────────────────────────────────

  String get baseUrl => _baseUrl;
  set baseUrl(String url) {
    _baseUrl = url.endsWith('/') ? url.substring(0, url.length - 1) : url;
  }

  void setClient(http.Client client) => _client = client;

  /// Splits a byte/string stream into complete newline-delimited lines,
  /// buffering partial lines that span chunk boundaries. Without this, a JSON
  /// line split across two decoder chunks would be dropped, truncating
  /// streamed LLM responses / progress events.
  Stream<String> _jsonLines(Stream<List<int>> byteStream) async* {
    var buffer = StringBuffer();
    await for (final chunk in byteStream.transform(utf8.decoder)) {
      buffer.write(chunk);
      var text = buffer.toString();
      var idx = text.indexOf('\n');
      while (idx >= 0) {
        final line = text.substring(0, idx);
        buffer.clear();
        buffer.write(text.substring(idx + 1));
        if (line.trim().isNotEmpty) yield line;
        text = buffer.toString();
        idx = text.indexOf('\n');
      }
    }
    // Flush any trailing partial line if the stream ends without a newline.
    if (buffer.isNotEmpty && buffer.toString().trim().isNotEmpty) {
      yield buffer.toString();
    }
  }

  // ── Version ────────────────────────────────────────────────────────────

  /// GET / — returns the Ollama version
  Future<String?> getVersion() async {
    try {
      final resp = await _client.get(Uri.parse('$_baseUrl/')).timeout(
            const Duration(seconds: 5),
          );
      if (resp.statusCode == 200) {
        final data = jsonDecode(resp.body);
        return data['version'] as String?;
      }
    } catch (_) {}
    return null;
  }

  // ── Health Check ───────────────────────────────────────────────────────

  /// Quick health check by hitting the tags endpoint
  Future<bool> isAlive() async {
    try {
      final resp = await _client
          .get(Uri.parse('$_baseUrl/api/tags'))
          .timeout(const Duration(seconds: 3));
      return resp.statusCode == 200;
    } catch (_) {
      return false;
    }
  }

  // ── List Local Models ──────────────────────────────────────────────────

  /// GET /api/tags — list all locally available models
  Future<OllamaTagsResponse> listModels() async {
    final resp = await _get('/api/tags');
    return OllamaTagsResponse.fromJson(resp);
  }

  // ── Show Model Information ─────────────────────────────────────────────

  /// POST /api/show — get details about a model
  Future<OllamaShowResponse> showModel(String name) async {
    final resp = await _post('/api/show', {'name': name});
    return OllamaShowResponse.fromJson(resp);
  }

  // ── Pull a Model ───────────────────────────────────────────────────────

  /// POST /api/pull — download a model (streaming progress)
  /// Returns a stream of progress updates.
  Stream<OllamaPullProgress> pullModel(String name, {bool insecure = false}) async* {
    final body = {'name': name, 'insecure': insecure};
    final request = http.Request('POST', Uri.parse('$_baseUrl/api/pull'));
    request.headers['Content-Type'] = 'application/json';
    request.body = jsonEncode(body);

    final streamedResp = await _client.send(request);
    if (streamedResp.statusCode != 200) {
      throw OllamaException('Pull failed with status ${streamedResp.statusCode}');
    }

    await for (final line in _jsonLines(streamedResp.stream)) {
      try {
        final data = jsonDecode(line);
        yield OllamaPullProgress.fromJson(data);
      } catch (_) {}
    }
  }

  // ── Delete a Model ─────────────────────────────────────────────────────

  /// DELETE /api/delete — remove a local model
  Future<void> deleteModel(String name) async {
    final request = http.Request('DELETE', Uri.parse('$_baseUrl/api/delete'));
    request.headers['Content-Type'] = 'application/json';
    request.body = jsonEncode({'name': name});
    final resp = await _client.send(request);
    if (resp.statusCode != 200) {
      throw OllamaException('Delete failed with status ${resp.statusCode}');
    }
  }

  // ── Copy a Model ───────────────────────────────────────────────────────

  /// POST /api/copy — duplicate a model
  Future<void> copyModel(String source, String destination) async {
    await _post('/api/copy', {'source': source, 'destination': destination});
  }

  // ── List Running Models ────────────────────────────────────────────────

  /// GET /api/ps — list currently loaded models in memory
  Future<OllamaPsResponse> listRunning() async {
    final resp = await _get('/api/ps');
    return OllamaPsResponse.fromJson(resp);
  }

  // ── Generate (non-streaming) ───────────────────────────────────────────

  /// POST /api/generate — generate a completion (non-streaming)
  Future<OllamaGenerateResponse> generate({
    required String model,
    String? prompt,
    String? suffix,
    List<String>? images,
    Map<String, dynamic>? options,
    String? system,
    String? template,
    String? format,
    bool raw = false,
    int keepAlive = 300,
  }) async {
    final body = <String, dynamic>{
      'model': model,
      'stream': false,
      'keep_alive': keepAlive,
    };
    if (prompt != null) body['prompt'] = prompt;
    if (suffix != null) body['suffix'] = suffix;
    if (images != null) body['images'] = images;
    if (options != null) body['options'] = options;
    if (system != null) body['system'] = system;
    if (template != null) body['template'] = template;
    if (format != null) body['format'] = format;
    if (raw) body['raw'] = true;

    final resp = await _post('/api/generate', body);
    return OllamaGenerateResponse.fromJson(resp);
  }

  // ── Generate (streaming) ───────────────────────────────────────────────

  /// POST /api/generate — stream a completion token by token
  Stream<OllamaGenerateStreamChunk> generateStream({
    required String model,
    String? prompt,
    String? suffix,
    List<String>? images,
    Map<String, dynamic>? options,
    String? system,
    String? template,
    String? format,
    bool raw = false,
    int keepAlive = 300,
  }) async* {
    final body = <String, dynamic>{
      'model': model,
      'stream': true,
      'keep_alive': keepAlive,
    };
    if (prompt != null) body['prompt'] = prompt;
    if (suffix != null) body['suffix'] = suffix;
    if (images != null) body['images'] = images;
    if (options != null) body['options'] = options;
    if (system != null) body['system'] = system;
    if (template != null) body['template'] = template;
    if (format != null) body['format'] = format;
    if (raw) body['raw'] = true;

    final request = http.Request('POST', Uri.parse('$_baseUrl/api/generate'));
    request.headers['Content-Type'] = 'application/json';
    request.body = jsonEncode(body);

    final streamedResp = await _client.send(request);
    if (streamedResp.statusCode != 200) {
      throw OllamaException('Generate failed with status ${streamedResp.statusCode}');
    }

    await for (final line in _jsonLines(streamedResp.stream)) {
      try {
        final data = jsonDecode(line);
        yield OllamaGenerateStreamChunk.fromJson(data);
      } catch (_) {}
    }
  }

  // ── Chat (non-streaming) ──────────────────────────────────────────────

  /// POST /api/chat — chat completion (non-streaming)
  Future<OllamaChatResponse> chat({
    required String model,
    required List<OllamaMessage> messages,
    Map<String, dynamic>? options,
    String? format,
    int keepAlive = 300,
  }) async {
    final body = <String, dynamic>{
      'model': model,
      'messages': messages.map((m) => m.toJson()).toList(),
      'stream': false,
      'keep_alive': keepAlive,
    };
    if (options != null) body['options'] = options;
    if (format != null) body['format'] = format;

    final resp = await _post('/api/chat', body);
    return OllamaChatResponse.fromJson(resp);
  }

  // ── Chat (streaming) ──────────────────────────────────────────────────

  /// POST /api/chat — stream a chat completion
  Stream<OllamaChatStreamChunk> chatStream({
    required String model,
    required List<OllamaMessage> messages,
    Map<String, dynamic>? options,
    String? format,
    int keepAlive = 300,
  }) async* {
    final body = <String, dynamic>{
      'model': model,
      'messages': messages.map((m) => m.toJson()).toList(),
      'stream': true,
      'keep_alive': keepAlive,
    };
    if (options != null) body['options'] = options;
    if (format != null) body['format'] = format;

    final request = http.Request('POST', Uri.parse('$_baseUrl/api/chat'));
    request.headers['Content-Type'] = 'application/json';
    request.body = jsonEncode(body);

    final streamedResp = await _client.send(request);
    if (streamedResp.statusCode != 200) {
      throw OllamaException('Chat failed with status ${streamedResp.statusCode}');
    }

    await for (final line in _jsonLines(streamedResp.stream)) {
      try {
        final data = jsonDecode(line);
        yield OllamaChatStreamChunk.fromJson(data);
      } catch (_) {}
    }
  }

  // ── Internal helpers ───────────────────────────────────────────────────

  Future<Map<String, dynamic>> _get(String path) async {
    final resp = await _client
        .get(Uri.parse('$_baseUrl$path'))
        .timeout(const Duration(seconds: 10));
    if (resp.statusCode != 200) {
      throw OllamaException('GET $path failed: ${resp.statusCode}');
    }
    return jsonDecode(resp.body);
  }

  Future<Map<String, dynamic>> _post(String path, Map<String, dynamic> body) async {
    final resp = await _client
        .post(
          Uri.parse('$_baseUrl$path'),
          headers: {'Content-Type': 'application/json'},
          body: jsonEncode(body),
        )
        .timeout(const Duration(seconds: 60));
    if (resp.statusCode != 200) {
      throw OllamaException('POST $path failed: ${resp.statusCode} ${resp.body}');
    }
    return jsonDecode(resp.body);
  }

  void dispose() {
    _client.close();
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// EXCEPTIONS
// ─────────────────────────────────────────────────────────────────────────────

class OllamaException implements Exception {
  final String message;
  OllamaException(this.message);
  @override
  String toString() => 'OllamaException: $message';
}

// ─────────────────────────────────────────────────────────────────────────────
// DATA MODELS — Tags
// ─────────────────────────────────────────────────────────────────────────────

class OllamaTagsResponse {
  final List<OllamaModel> models;
  OllamaTagsResponse({required this.models});

  factory OllamaTagsResponse.fromJson(Map<String, dynamic> json) {
    return OllamaTagsResponse(
      models: (json['models'] as List<dynamic>?)
              ?.map((m) => OllamaModel.fromJson(m))
              .toList() ??
          [],
    );
  }
}

class OllamaModel {
  final String name;
  final String? model;
  final DateTime? modifiedAt;
  final int? size;
  final String? digest;
  final List<OllamaModelDetail>? details;

  OllamaModel({
    required this.name,
    this.model,
    this.modifiedAt,
    this.size,
    this.digest,
    this.details,
  });

  factory OllamaModel.fromJson(Map<String, dynamic> json) {
    return OllamaModel(
      name: json['name'] ?? '',
      model: json['model'],
      modifiedAt: json['modified_at'] != null
          ? DateTime.tryParse(json['modified_at'])
          : null,
      size: json['size'],
      digest: json['digest'],
      details: (json['details'] as List<dynamic>?)
          ?.map((d) => OllamaModelDetail.fromJson(d))
          .toList(),
    );
  }

  /// Friendly display name (strips :latest tag)
  String get displayName {
    if (name.endsWith(':latest')) return name.substring(0, name.length - 7);
    return name;
  }

  /// Human-readable file size
  String get formattedSize {
    if (size == null) return 'Unknown';
    final mb = size! / (1024 * 1024);
    if (mb >= 1024) return '${(mb / 1024).toStringAsFixed(1)} GB';
    return '${mb.toStringAsFixed(0)} MB';
  }
}

class OllamaModelDetail {
  final String? format;
  final String? family;
  final int? parameterSize;
  final String? quantizationLevel;

  OllamaModelDetail({
    this.format,
    this.family,
    this.parameterSize,
    this.quantizationLevel,
  });

  factory OllamaModelDetail.fromJson(Map<String, dynamic> json) {
    return OllamaModelDetail(
      format: json['format'],
      family: json['family'],
      parameterSize: json['parameter_size'],
      quantizationLevel: json['quantization_level'],
    );
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// DATA MODELS — Show
// ─────────────────────────────────────────────────────────────────────────────

class OllamaShowResponse {
  final String? modelfile;
  final String? parameters;
  final String? template;
  final String? system;
  final Map<String, dynamic>? details;
  final Map<String, dynamic>? modelInfo;

  OllamaShowResponse({
    this.modelfile,
    this.parameters,
    this.template,
    this.system,
    this.details,
    this.modelInfo,
  });

  factory OllamaShowResponse.fromJson(Map<String, dynamic> json) {
    return OllamaShowResponse(
      modelfile: json['modelfile'],
      parameters: json['parameters'],
      template: json['template'],
      system: json['system'],
      details: json['details'] as Map<String, dynamic>?,
      modelInfo: json['model_info'] as Map<String, dynamic>?,
    );
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// DATA MODELS — Pull Progress
// ─────────────────────────────────────────────────────────────────────────────

class OllamaPullProgress {
  final String? status;
  final int? total;
  final int? completed;

  OllamaPullProgress({this.status, this.total, this.completed});

  factory OllamaPullProgress.fromJson(Map<String, dynamic> json) {
    return OllamaPullProgress(
      status: json['status'],
      total: json['total'],
      completed: json['completed'],
    );
  }

  double get progress {
    if (total == null || total == 0 || completed == null) return 0;
    return completed! / total!;
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// DATA MODELS — Running Models
// ─────────────────────────────────────────────────────────────────────────────

class OllamaPsResponse {
  final List<OllamaRunningModel> models;
  OllamaPsResponse({required this.models});

  factory OllamaPsResponse.fromJson(Map<String, dynamic> json) {
    return OllamaPsResponse(
      models: (json['models'] as List<dynamic>?)
              ?.map((m) => OllamaRunningModel.fromJson(m))
              .toList() ??
          [],
    );
  }
}

class OllamaRunningModel {
  final String name;
  final String? model;
  final int? size;
  final int? sizeVram;
  final String? digest;
  final DateTime? expiresAt;

  OllamaRunningModel({
    required this.name,
    this.model,
    this.size,
    this.sizeVram,
    this.digest,
    this.expiresAt,
  });

  factory OllamaRunningModel.fromJson(Map<String, dynamic> json) {
    return OllamaRunningModel(
      name: json['name'] ?? '',
      model: json['model'],
      size: json['size'],
      sizeVram: json['size_vram'],
      digest: json['digest'],
      expiresAt: json['expires_at'] != null
          ? DateTime.tryParse(json['expires_at'])
          : null,
    );
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// DATA MODELS — Generate
// ─────────────────────────────────────────────────────────────────────────────

class OllamaGenerateResponse {
  final String model;
  final String? response;
  final bool done;
  final String? doneReason;
  final int? totalDuration;
  final int? promptEvalCount;
  final int? evalCount;

  OllamaGenerateResponse({
    required this.model,
    this.response,
    required this.done,
    this.doneReason,
    this.totalDuration,
    this.promptEvalCount,
    this.evalCount,
  });

  factory OllamaGenerateResponse.fromJson(Map<String, dynamic> json) {
    return OllamaGenerateResponse(
      model: json['model'] ?? '',
      response: json['response'],
      done: json['done'] ?? false,
      doneReason: json['done_reason'],
      totalDuration: json['total_duration'],
      promptEvalCount: json['prompt_eval_count'],
      evalCount: json['eval_count'],
    );
  }
}

class OllamaGenerateStreamChunk {
  final String model;
  final String? response;
  final bool done;

  OllamaGenerateStreamChunk({
    required this.model,
    this.response,
    required this.done,
  });

  factory OllamaGenerateStreamChunk.fromJson(Map<String, dynamic> json) {
    return OllamaGenerateStreamChunk(
      model: json['model'] ?? '',
      response: json['response'],
      done: json['done'] ?? false,
    );
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// DATA MODELS — Chat
// ─────────────────────────────────────────────────────────────────────────────

class OllamaMessage {
  final String role;
  final String content;
  final List<String>? images;

  OllamaMessage({
    required this.role,
    required this.content,
    this.images,
  });

  Map<String, dynamic> toJson() {
    final json = <String, dynamic>{
      'role': role,
      'content': content,
    };
    if (images != null) json['images'] = images;
    return json;
  }

  factory OllamaMessage.fromJson(Map<String, dynamic> json) {
    return OllamaMessage(
      role: json['role'] ?? 'user',
      content: json['content'] ?? '',
      images: json['images'] != null
          ? List<String>.from(json['images'])
          : null,
    );
  }
}

class OllamaChatResponse {
  final String model;
  final OllamaMessage? message;
  final bool done;
  final String? doneReason;
  final int? totalDuration;
  final int? promptEvalCount;
  final int? evalCount;

  OllamaChatResponse({
    required this.model,
    this.message,
    required this.done,
    this.doneReason,
    this.totalDuration,
    this.promptEvalCount,
    this.evalCount,
  });

  factory OllamaChatResponse.fromJson(Map<String, dynamic> json) {
    return OllamaChatResponse(
      model: json['model'] ?? '',
      message: json['message'] != null
          ? OllamaMessage.fromJson(json['message'])
          : null,
      done: json['done'] ?? false,
      doneReason: json['done_reason'],
      totalDuration: json['total_duration'],
      promptEvalCount: json['prompt_eval_count'],
      evalCount: json['eval_count'],
    );
  }
}

class OllamaChatStreamChunk {
  final String model;
  final OllamaMessage? message;
  final bool done;

  OllamaChatStreamChunk({
    required this.model,
    this.message,
    required this.done,
  });

  factory OllamaChatStreamChunk.fromJson(Map<String, dynamic> json) {
    return OllamaChatStreamChunk(
      model: json['model'] ?? '',
      message: json['message'] != null
          ? OllamaMessage.fromJson(json['message'])
          : null,
      done: json['done'] ?? false,
    );
  }
}
