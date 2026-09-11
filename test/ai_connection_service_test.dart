import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:clip_sync_ai/services/ai_connection_service.dart';

void main() {
  group('AiProvider catalog', () {
    test('covers the expected providers plus the custom gateway', () {
      final ids = AiProvider.values.map((p) => p.id).toSet();
      expect(
        ids,
        containsAll(<String>{
          'ollama',
          'openai',
          'openrouter',
          'groq',
          'deepseek',
          'mistral',
          'xai',
          'together',
          'lmstudio',
          'gateway',
        }),
      );
      expect(ids.length, AiProvider.values.length, reason: 'no duplicate ids');
    });

    test('only Ollama speaks the native protocol', () {
      for (final provider in AiProvider.values) {
        if (provider == AiProvider.ollama) {
          expect(provider.kind, AiConnectionKind.ollama);
        } else {
          expect(provider.kind, AiConnectionKind.openAiCompatible);
        }
      }
    });

    test('custom gateway has no preset server and a useful hint', () {
      expect(AiProvider.customGateway.defaultUrl, isEmpty);
      expect(AiProvider.customGateway.hint, contains('://'));
    });

    test('every preset ships a default server', () {
      for (final provider in AiProvider.values) {
        if (provider == AiProvider.customGateway) continue;
        expect(provider.defaultUrl, isNotEmpty,
            reason: '${provider.id} needs a default endpoint');
      }
    });
  });

  group('AiConnectionConfig persistence', () {
    test('round-trips provider, endpoint, model and name', () {
      const config = AiConnectionConfig(
        provider: AiProvider.openRouter,
        endpoint: 'https://openrouter.ai/api/v1',
        model: 'openrouter/auto',
        displayName: 'Router',
      );
      final restored = AiConnectionConfig.fromJson(config.toJson());
      expect(restored.provider, AiProvider.openRouter);
      expect(restored.endpoint, 'https://openrouter.ai/api/v1');
      expect(restored.model, 'openrouter/auto');
      expect(restored.displayName, 'Router');
      expect(restored.kind, AiConnectionKind.openAiCompatible);
    });

    test('migrates a legacy OpenAI-style record onto the custom gateway', () {
      final restored = AiConnectionConfig.fromJson({
        'kind': 'openAiCompatible',
        'endpoint': 'http://192.168.1.50:3000/v1',
        'model': 'gpt-4o-mini',
        'displayName': 'My API',
      });
      expect(restored.provider, AiProvider.customGateway);
      expect(restored.endpoint, 'http://192.168.1.50:3000/v1');
      expect(restored.model, 'gpt-4o-mini');
    });

    test('migrates a legacy Ollama record onto the ollama preset', () {
      final restored = AiConnectionConfig.fromJson({
        'kind': 'ollama',
        'endpoint': 'http://127.0.0.1:11434',
        'model': 'llama3.2',
        'displayName': 'Local Ollama',
      });
      expect(restored.provider, AiProvider.ollama);
      expect(restored.kind, AiConnectionKind.ollama);
    });

    test('an unknown provider name falls back to the custom gateway', () {
      final restored = AiConnectionConfig.fromJson({
        'provider': 'not-a-provider',
        'endpoint': 'http://example.com/v1',
        'model': 'm',
        'displayName': 'n',
      });
      expect(restored.provider, AiProvider.customGateway);
    });
  });

  testWidgets('AlertDialog honours an explicit maxWidth constraint',
      (tester) async {
    const contentWidth = 420.0;
    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(
          body: Center(
            child: AlertDialog(
              constraints: BoxConstraints(maxWidth: contentWidth),
              content: SizedBox(
                width: contentWidth,
                child: TextField(),
              ),
            ),
          ),
        ),
      ),
    );
    final dialog = find.byType(AlertDialog);
    final width = tester.getSize(dialog).width;
    expect(width, greaterThan(360),
        reason: 'the dialog should escape Material\'s 280 default');
  });
}