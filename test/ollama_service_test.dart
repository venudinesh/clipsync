import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart';
import 'package:http/testing.dart';
import 'package:clip_sync_ai/services/ollama_service.dart';

void main() {
  // Helper to create a mock client that returns JSON for a given path
  MockClient mockClient(String path, int statusCode, Map<String, dynamic> body) {
    return MockClient((request) async {
      if (request.url.path == path || request.url.path.endsWith(path)) {
        return Response(jsonEncode(body), statusCode);
      }
      return Response('Not Found', 404);
    });
  }

  group('OllamaService', () {
    test('isAlive returns true when server responds 200', () async {
      final client = mockClient('/api/tags', 200, {'models': []});
      final service = OllamaService(client: client);
      expect(await service.isAlive(), isTrue);
    });

    test('isAlive returns false when server is down', () async {
      final client = MockClient((_) async => throw Exception('connection refused'));
      final service = OllamaService(client: client);
      expect(await service.isAlive(), isFalse);
    });

    test('getVersion returns version string', () async {
      final client = mockClient('/', 200, {'version': '0.9.0'});
      final service = OllamaService(client: client);
      final version = await service.getVersion();
      expect(version, '0.9.0');
    });

    test('getVersion returns null on failure', () async {
      final client = MockClient((_) async => throw Exception('fail'));
      final service = OllamaService(client: client);
      expect(await service.getVersion(), isNull);
    });

    test('baseUrl setter strips trailing slash', () {
      final service = OllamaService();
      service.baseUrl = 'http://localhost:11434/';
      expect(service.baseUrl, 'http://localhost:11434');
    });

    test('baseUrl getter returns current URL', () {
      final service = OllamaService(baseUrl: 'http://custom:9999');
      expect(service.baseUrl, 'http://custom:9999');
    });
  });

  group('listModels', () {
    test('parses model list correctly', () async {
      final tagsResp = {
        'models': [
          {
            'name': 'llama3.2:1b',
            'model': 'llama3.2:1b',
            'modified_at': '2025-01-15T10:30:00Z',
            'size': 1342177280,
            'digest': 'abc123',
          },
          {
            'name': 'gemma:2b',
            'model': 'gemma:2b',
            'modified_at': '2025-02-20T14:00:00Z',
            'size': 2147483648,
            'digest': 'def456',
          },
        ]
      };
      final client = mockClient('/api/tags', 200, tagsResp);
      final service = OllamaService(client: client);

      final resp = await service.listModels();
      expect(resp.models.length, 2);
      expect(resp.models[0].name, 'llama3.2:1b');
      expect(resp.models[0].displayName, 'llama3.2:1b');
      expect(resp.models[0].formattedSize, contains('GB'));
      expect(resp.models[1].name, 'gemma:2b');
    });

    test('handles empty model list', () async {
      final client = mockClient('/api/tags', 200, {'models': []});
      final service = OllamaService(client: client);

      final resp = await service.listModels();
      expect(resp.models, isEmpty);
    });

    test('throws on non-200 response', () async {
      final client = mockClient('/api/tags', 500, {'error': 'server error'});
      final service = OllamaService(client: client);

      expect(() => service.listModels(), throwsA(isA<OllamaException>()));
    });
  });

  group('showModel', () {
    test('parses model details', () async {
      final showResp = {
        'modelfile': 'FROM llama3.2:1b',
        'parameters': 'temperature 0.3',
        'template': '{{ .Prompt }}',
        'system': 'You are a helpful assistant.',
        'details': {'format': 'gguf', 'family': 'llama'},
        'model_info': {'general.architecture': 'llama'},
      };
      final client = mockClient('/api/show', 200, showResp);
      final service = OllamaService(client: client);

      final resp = await service.showModel('llama3.2:1b');
      expect(resp.modelfile, contains('llama3.2'));
      expect(resp.parameters, 'temperature 0.3');
      expect(resp.template, '{{ .Prompt }}');
      expect(resp.system, 'You are a helpful assistant.');
      expect(resp.details?['format'], 'gguf');
      expect(resp.modelInfo?['general.architecture'], 'llama');
    });
  });

  group('generate', () {
    test('non-streaming generate returns response', () async {
      final genResp = {
        'model': 'llama3.2:1b',
        'response': 'The sky is blue due to Rayleigh scattering.',
        'done': true,
        'done_reason': 'stop',
        'total_duration': 5000000000,
        'prompt_eval_count': 26,
        'eval_count': 30,
      };
      final client = mockClient('/api/generate', 200, genResp);
      final service = OllamaService(client: client);

      final resp = await service.generate(
        model: 'llama3.2:1b',
        prompt: 'Why is the sky blue?',
      );
      expect(resp.model, 'llama3.2:1b');
      expect(resp.response, contains('Rayleigh'));
      expect(resp.done, isTrue);
      expect(resp.doneReason, 'stop');
      expect(resp.totalDuration, 5000000000);
      expect(resp.promptEvalCount, 26);
      expect(resp.evalCount, 30);
    });

    test('generate with all options', () async {
      final genResp = {
        'model': 'codellama',
        'response': 'def hello():\n  print("hi")',
        'done': true,
      };
      final client = mockClient('/api/generate', 200, genResp);
      final service = OllamaService(client: client);

      final resp = await service.generate(
        model: 'codellama',
        prompt: 'def hello():',
        suffix: ' return result',
        system: 'You are a code assistant.',
        template: '{{ .Prompt }}',
        format: 'json',
        raw: true,
        keepAlive: 600,
        options: {'temperature': 0, 'num_predict': 256},
      );
      expect(resp.response, contains('def hello'));
    });

    test('throws on server error', () async {
      final client = mockClient('/api/generate', 500, {'error': 'model not found'});
      final service = OllamaService(client: client);

      expect(
        () => service.generate(model: 'nonexistent', prompt: 'test'),
        throwsA(isA<OllamaException>()),
      );
    });
  });

  group('chat', () {
    test('non-streaming chat returns message', () async {
      final chatResp = {
        'model': 'llama3.2',
        'message': {
          'role': 'assistant',
          'content': 'Hello! How can I help?',
        },
        'done': true,
        'done_reason': 'stop',
        'total_duration': 4000000000,
        'prompt_eval_count': 30,
        'eval_count': 15,
      };
      final client = mockClient('/api/chat', 200, chatResp);
      final service = OllamaService(client: client);

      final resp = await service.chat(
        model: 'llama3.2',
        messages: [OllamaMessage(role: 'user', content: 'Hi there!')],
      );
      expect(resp.model, 'llama3.2');
      expect(resp.message?.role, 'assistant');
      expect(resp.message?.content, contains('Hello'));
      expect(resp.done, isTrue);
    });

    test('chat with history', () async {
      final chatResp = {
        'model': 'llama3.2',
        'message': {'role': 'assistant', 'content': 'Blue because of scattering.'},
        'done': true,
      };
      final client = mockClient('/api/chat', 200, chatResp);
      final service = OllamaService(client: client);

      final resp = await service.chat(
        model: 'llama3.2',
        messages: [
          OllamaMessage(role: 'user', content: 'Why is the sky blue?'),
          OllamaMessage(role: 'assistant', content: 'Due to Rayleigh scattering.'),
          OllamaMessage(role: 'user', content: 'How does that work?'),
        ],
        options: {'temperature': 0.5},
      );
      expect(resp.message?.content, contains('Blue'));
    });

    test('chat message serialization round-trip', () {
      final msg = OllamaMessage(role: 'user', content: 'test message');
      final json = msg.toJson();
      final restored = OllamaMessage.fromJson(json);
      expect(restored.role, 'user');
      expect(restored.content, 'test message');
      expect(restored.images, isNull);
    });
  });

  group('deleteModel', () {
    test('succeeds with 200', () async {
      final client = MockClient((request) async {
        if (request.method == 'DELETE') {
          return Response('{}', 200);
        }
        return Response('Not Found', 404);
      });
      final service = OllamaService(client: client);

      // Should not throw
      await service.deleteModel('old-model:latest');
    });

    test('throws on failure', () async {
      final client = MockClient((_) async => Response('error', 500));
      final service = OllamaService(client: client);

      expect(
        () => service.deleteModel('bad-model'),
        throwsA(isA<OllamaException>()),
      );
    });
  });

  group('copyModel', () {
    test('succeeds with 200', () async {
      final client = mockClient('/api/copy', 200, {});
      final service = OllamaService(client: client);

      // Should not throw
      await service.copyModel('source:model', 'dest:model');
    });
  });

  group('listRunning', () {
    test('parses running models', () async {
      final psResp = {
        'models': [
          {
            'name': 'llama3.2:1b',
            'model': 'llama3.2:1b',
            'size': 1342177280,
            'size_vram': 1342177280,
            'digest': 'abc123',
            'expires_at': '2025-06-15T12:00:00Z',
          }
        ]
      };
      final client = mockClient('/api/ps', 200, psResp);
      final service = OllamaService(client: client);

      final resp = await service.listRunning();
      expect(resp.models.length, 1);
      expect(resp.models[0].name, 'llama3.2:1b');
      expect(resp.models[0].sizeVram, 1342177280);
      expect(resp.models[0].expiresAt, isA<DateTime>());
    });

    test('handles empty running list', () async {
      final client = mockClient('/api/ps', 200, {'models': []});
      final service = OllamaService(client: client);

      final resp = await service.listRunning();
      expect(resp.models, isEmpty);
    });
  });

  group('OllamaModel helpers', () {
    test('displayName strips :latest', () {
      final model = OllamaModel(name: 'llama3.2:latest');
      expect(model.displayName, 'llama3.2');
    });

    test('displayName preserves custom tag', () {
      final model = OllamaModel(name: 'llama3.2:1b');
      expect(model.displayName, 'llama3.2:1b');
    });

    test('formattedSize returns MB for small models', () {
      final model = OllamaModel(name: 'tiny', size: 524288000); // ~500MB
      expect(model.formattedSize, '500 MB');
    });

    test('formattedSize returns GB for large models', () {
      final model = OllamaModel(name: 'large', size: 4294967296); // 4GB
      expect(model.formattedSize, contains('GB'));
    });

    test('formattedSize returns Unknown when size is null', () {
      final model = OllamaModel(name: 'unknown');
      expect(model.formattedSize, 'Unknown');
    });
  });

  group('OllamaPullProgress', () {
    test('progress calculates percentage', () {
      final progress = OllamaPullProgress(
        status: 'downloading',
        total: 1000,
        completed: 500,
      );
      expect(progress.progress, 0.5);
    });

    test('progress returns 0 when total is null', () {
      final progress = OllamaPullProgress(status: 'starting');
      expect(progress.progress, 0);
    });

    test('progress returns 0 when total is 0', () {
      final progress = OllamaPullProgress(total: 0, completed: 0);
      expect(progress.progress, 0);
    });
  });

  group('OllamaException', () {
    test('toString returns formatted message', () {
      final ex = OllamaException('test error');
      expect(ex.toString(), 'OllamaException: test error');
    });
  });

  group('OllamaMessage', () {
    test('serializes with images', () {
      final msg = OllamaMessage(
        role: 'user',
        content: 'What is this?',
        images: ['base64data'],
      );
      final json = msg.toJson();
      expect(json['images'], ['base64data']);
    });

    test('serializes without images', () {
      final msg = OllamaMessage(role: 'assistant', content: 'Hello');
      final json = msg.toJson();
      expect(json.containsKey('images'), isFalse);
    });
  });
}
