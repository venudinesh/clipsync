import 'dart:async';
import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:hive_flutter/hive_flutter.dart';
import 'package:http/http.dart' as http;

import 'on_device_llm_service.dart';

/// A connection the chat UI can use in addition to the embedded GGUF runtime.
/// Ollama uses its native REST API; every other provider here speaks the widely
/// supported OpenAI-compatible chat-completions API, so one client handles them
/// all and the differences are just the endpoint presets.
enum AiConnectionKind { ollama, openAiCompatible }

/// Provider presets: a label, a sensible default server, and whether the
/// provider talks the native Ollama protocol or an OpenAI-compatible one.
/// Ollama stays in the list as it is the one deliberately local option.
enum AiProvider {
  ollama('ollama', 'Local Ollama', 'http://127.0.0.1:11434',
      usesOpenAiApi: false),
  openAi('openai', 'OpenAI', 'https://api.openai.com/v1'),
  openRouter('openrouter', 'OpenRouter', 'https://openrouter.ai/api/v1'),
  groq('groq', 'Groq', 'https://api.groq.com/openai/v1'),
  deepSeek('deepseek', 'DeepSeek', 'https://api.deepseek.com'),
  mistral('mistral', 'Mistral', 'https://api.mistral.ai/v1'),
  xAi('xai', 'xAI (Grok)', 'https://api.x.ai/v1'),
  together('together', 'Together AI', 'https://api.together.xyz/v1'),
  lmStudio('lmstudio', 'LM Studio', 'http://localhost:1234/v1'),
  customGateway(
      'gateway', 'Custom gateway', '', hint: 'http://your-gateway:3000/v1');

  const AiProvider(this.id, this.label, this.defaultUrl,
      {this.hint, this.usesOpenAiApi = true});

  final String id;
  final String label;

  /// Server the provider documents; empty for the custom gateway, which is
  /// whatever the user points it at.
  final String defaultUrl;
  final String? hint;
  final bool usesOpenAiApi;

  AiConnectionKind get kind =>
      usesOpenAiApi ? AiConnectionKind.openAiCompatible : AiConnectionKind.ollama;
}

class AiConnectionConfig {
  const AiConnectionConfig({
    this.provider = AiProvider.ollama,
    this.endpoint = 'http://127.0.0.1:11434',
    this.model = '',
    this.displayName = 'Local Ollama',
  });

  final AiProvider provider;
  final String endpoint;
  final String model;
  final String displayName;

  AiConnectionKind get kind => provider.kind;

  bool get isConfigured => Uri.tryParse(endpoint)?.hasScheme == true && model.trim().isNotEmpty;

  AiConnectionConfig copyWith({
    AiProvider? provider,
    String? endpoint,
    String? model,
    String? displayName,
  }) => AiConnectionConfig(
    provider: provider ?? this.provider,
    endpoint: endpoint ?? this.endpoint,
    model: model ?? this.model,
    displayName: displayName ?? this.displayName,
  );

  Map<String, dynamic> toJson() => {
        'provider': provider.name,
        'endpoint': endpoint,
        'model': model,
        'displayName': displayName,
      };

  factory AiConnectionConfig.fromJson(Map<dynamic, dynamic>? value) {
    final providerName = value?['provider'] as String?;
    // Configs saved by older builds only had `kind`; map those onto the closest
    // preset so nothing saved so far is lost.
    final provider = providerName == null
        ? (value?['kind'] == AiConnectionKind.openAiCompatible.name
            ? AiProvider.customGateway
            : AiProvider.ollama)
        : AiProvider.values.firstWhere(
            (p) => p.name == providerName,
            orElse: () => AiProvider.customGateway,
          );
    return AiConnectionConfig(
      provider: provider,
      endpoint: value?['endpoint'] as String? ?? provider.defaultUrl,
      model: value?['model'] as String? ?? '',
      displayName: value?['displayName'] as String? ?? provider.label,
    );
  }
}

class AiConnectionService {
  static const _configKey = 'aiConnectionConfig';
  static const _apiKeyLabel = 'clipSync_ai_connection_api_key';

  AiConnectionService({http.Client? client, FlutterSecureStorage? storage})
      : _client = client ?? http.Client(),
        _storage = storage ?? const FlutterSecureStorage();

  final http.Client _client;
  final FlutterSecureStorage _storage;
  final ValueNotifier<AiConnectionConfig> config =
      ValueNotifier(const AiConnectionConfig());

  Future<void> load() async {
    final box = Hive.box('settings');
    config.value = AiConnectionConfig.fromJson(box.get(_configKey) as Map?);
  }

  Future<void> save(AiConnectionConfig value, {String? apiKey}) async {
    // The provider decides the protocol; never let the two drift apart.
    final normalized = value.copyWith(
      endpoint: _normaliseEndpoint(value.endpoint),
      provider: value.provider,
    );
    await Hive.box('settings').put(_configKey, normalized.toJson());
    if (apiKey != null && apiKey.trim().isNotEmpty) {
      await _storage.write(key: _apiKeyLabel, value: apiKey.trim());
    }
    config.value = normalized;
  }

  Future<bool> hasApiKey() async => (await _storage.read(key: _apiKeyLabel))?.isNotEmpty == true;

  Future<void> clearApiKey() => _storage.delete(key: _apiKeyLabel);

  /// Checks the configured server and returns its models. A successful result
  /// is deliberately useful for both the Test button and model picker.
  Future<List<String>> discoverModels(AiConnectionConfig value, {String? apiKey}) async {
    final endpoint = _normaliseEndpoint(value.endpoint);
    if (value.provider == AiProvider.ollama) {
      final response = await _client
          .get(Uri.parse('$endpoint/api/tags'))
          .timeout(const Duration(seconds: 20));
      if (response.statusCode != 200) throw AiConnectionException(_error(response));
      final json = jsonDecode(response.body) as Map<String, dynamic>;
      return (json['models'] as List<dynamic>? ?? const [])
          .map((model) => (model as Map<String, dynamic>)['name'] as String? ?? '')
          .where((name) => name.isNotEmpty)
          .toList();
    }

    final response = await _client
        .get(Uri.parse('${_openAiBase(endpoint)}/models'), headers: await _authHeaders(apiKey: apiKey))
        .timeout(const Duration(seconds: 20));
    if (response.statusCode != 200) throw AiConnectionException(_error(response));
    final json = jsonDecode(response.body) as Map<String, dynamic>;
    return (json['data'] as List<dynamic>? ?? const [])
        .map((model) => (model as Map<String, dynamic>)['id'] as String? ?? '')
        .where((name) => name.isNotEmpty)
        .toList();
  }

  Stream<String> chatStream(List<ChatMessage> messages, {double temperature = .7}) async* {
    final active = config.value;
    if (!active.isConfigured) {
      throw AiConnectionException('Choose a model in Settings → AI → Connections first.');
    }
    final endpoint = _normaliseEndpoint(active.endpoint);
    final request = http.Request(
      'POST',
      Uri.parse(active.kind == AiConnectionKind.ollama
          ? '$endpoint/api/chat'
          : '${_openAiBase(endpoint)}/chat/completions'),
    );
    request.headers.addAll({
      'Content-Type': 'application/json',
      ...await _authHeaders(onlyForCustom: true),
    });
    request.body = jsonEncode(active.kind == AiConnectionKind.ollama
        ? {
            'model': active.model,
            'messages': messages.map((m) => {'role': m.role, 'content': m.content}).toList(),
            'stream': true,
            'options': {'temperature': temperature},
          }
        : {
            'model': active.model,
            'messages': messages.map((m) => {'role': m.role, 'content': m.content}).toList(),
            'stream': true,
            'temperature': temperature,
          });

    final response = await _client.send(request).timeout(const Duration(seconds: 90));
    if (response.statusCode != 200) throw AiConnectionException(await _streamError(response));
    var remainder = '';
    await for (final chunk in response.stream.transform(utf8.decoder)) {
      remainder += chunk;
      final lines = remainder.split('\n');
      remainder = lines.removeLast();
      for (final raw in lines) {
        final line = raw.trim();
        if (line.isEmpty || line.startsWith(':')) continue;
        final payload = line.startsWith('data:') ? line.substring(5).trim() : line;
        if (payload == '[DONE]') return;
        try {
          final json = jsonDecode(payload) as Map<String, dynamic>;
          final token = active.kind == AiConnectionKind.ollama
              ? ((json['message'] as Map?)?['content'] as String? ?? '')
              : (((json['choices'] as List?)?.firstOrNull as Map?)?['delta'] as Map?)?['content'] as String? ?? '';
          if (token.isNotEmpty) yield token;
        } on FormatException {
          // Keep the stream resilient to proxy keep-alives and blank frames.
        }
      }
    }
  }

  Future<Map<String, String>> _authHeaders({bool onlyForCustom = false, String? apiKey}) async {
    if (onlyForCustom && config.value.kind == AiConnectionKind.ollama) return const {};
    final key = apiKey?.trim().isNotEmpty == true ? apiKey!.trim() : await _storage.read(key: _apiKeyLabel);
    return key == null || key.isEmpty ? const {} : {'Authorization': 'Bearer $key'};
  }

  static String _normaliseEndpoint(String value) => value.trim().replaceFirst(RegExp(r'/+$'), '');
  /// Accept both a server root and the API base convention used by OpenAI SDKs
  /// (for example, `https://api.example.com/v1`).
  static String _openAiBase(String endpoint) => endpoint.endsWith('/v1') ? endpoint : '$endpoint/v1';
  static String _error(http.Response response) => 'Server returned ${response.statusCode}: ${response.body.length > 180 ? response.body.substring(0, 180) : response.body}';
  static Future<String> _streamError(http.StreamedResponse response) async {
    final body = await response.stream.bytesToString();
    return 'Server returned ${response.statusCode}: ${body.length > 180 ? body.substring(0, 180) : body}';
  }

  void dispose() {
    config.dispose();
    _client.close();
  }
}

class AiConnectionException implements Exception {
  AiConnectionException(this.message);
  final String message;
  @override
  String toString() => message;
}
