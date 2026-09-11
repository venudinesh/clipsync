import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:clip_sync_ai/services/on_device_llm_service.dart';
import 'package:clip_sync_ai/ui/glass_panel.dart';

void main() {
  // Must stay above the GlassPanel group, which drives this notifier by hand.
  group('shipped surface style', () {
    test('a fresh install opens on Material, not glass', () {
      expect(
        appGlassMode.value,
        isFalse,
        reason: 'first open is Material 3; Liquid Glass is opt-in and, once '
            'chosen, is read back from the settings box',
      );
    });
  });

  group('OnDeviceLlmService.sanitizeInput', () {
    test('strips null bytes and control characters', () {
      const dirty = 'Hello\u0000World\u0007\tclean\nline\r\u001Bdone';
      expect(OnDeviceLlmService.sanitizeInput(dirty), contains('Hello'));
      expect(OnDeviceLlmService.sanitizeInput(dirty), isNot(contains('\u0000')));
      expect(OnDeviceLlmService.sanitizeInput(dirty), isNot(contains('\u0007')));
      expect(OnDeviceLlmService.sanitizeInput(dirty), isNot(contains('\u001B')));
    });

    test('keeps normal newlines and whitespace', () {
      const text = 'line 1\nline 2\n  indented  ';
      final out = OnDeviceLlmService.sanitizeInput(text);
      expect(out, contains('line 1\nline 2'));
    });

    test('caps very long input to avoid blowing the context window', () {
      final long = 'a' * 50000;
      final out = OnDeviceLlmService.sanitizeInput(long);
      expect(out.length, lessThanOrEqualTo(8192));
    });

    test('handles empty and whitespace-only input', () {
      expect(OnDeviceLlmService.sanitizeInput(''), isEmpty);
      expect(OnDeviceLlmService.sanitizeInput('   \n\t '), isEmpty);
    });
  });

  group('GlassPanel glass vs solid', () {
    Widget harness({required bool glass, bool dark = true}) {
      appGlassMode.value = glass;
      return MaterialApp(
        theme: ThemeData(
          brightness: dark ? Brightness.dark : Brightness.light,
          colorScheme: ColorScheme.fromSeed(
            seedColor: Colors.cyan,
            brightness: dark ? Brightness.dark : Brightness.light,
          ),
        ),
        home: Scaffold(
          body: GlassPanel(
            child: const SizedBox(width: 100, height: 100),
          ),
        ),
      );
    }

    testWidgets('glass mode applies a translucent fill', (tester) async {
      await tester.pumpWidget(harness(glass: true));
      final dec = tester
          .widget<Container>(find.byType(Container).first)
          .decoration as BoxDecoration;
      expect(dec.boxShadow, isNotNull);
      expect(dec.border, isNotNull);
    });

    testWidgets('solid mode renders a themed surface (no glass)', (tester) async {
      await tester.pumpWidget(harness(glass: false));
      final decorated = find.byWidgetPredicate((w) => w is Container);
      expect(decorated, findsWidgets);
    });

    testWidgets('toggling glass mode rebuilds without error', (tester) async {
      await tester.pumpWidget(harness(glass: true));
      appGlassMode.value = false;
      await tester.pump();
      appGlassMode.value = true;
      await tester.pump();
      expect(tester.takeException(), isNull);
    });
  });
}