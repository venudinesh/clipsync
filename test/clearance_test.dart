import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:clip_sync_ai/ui/design_tokens.dart';

/// What has to stay true at the bottom edge of the screen, where the floating
/// nav capsule, a page's own scrollable and a toast all compete for the same
/// band of pixels.
void main() {
  group('navBarClearance', () {
    /// Reads the clearance from inside a `MediaQuery` that mimics a phone with
    /// a gesture bar, optionally under a [KeyboardVisibility] saying the
    /// keyboard is up.
    Future<double> measure(
      WidgetTester tester, {
      required bool keyboard,
      double systemInset = 24,
      double extra = 0,
    }) async {
      late double result;
      await tester.pumpWidget(
        MediaQuery(
          data: MediaQueryData(
            padding: EdgeInsets.only(bottom: systemInset),
          ),
          child: KeyboardVisibility(
            visible: keyboard,
            child: Builder(
              builder: (context) {
                result = navBarClearance(context, extra: extra);
                return const SizedBox();
              },
            ),
          ),
        ),
      );
      return result;
    }

    testWidgets('reserves the capsule plus the system inset when idle',
        (tester) async {
      expect(
        await measure(tester, keyboard: false),
        Space.navBar + 24,
        reason: 'the capsule floats inside the gesture inset, so the inset is '
            'added to its height rather than replacing it',
      );
    });

    testWidgets('collapses to a breath once the keyboard is up',
        (tester) async {
      // The capsule has already slid off screen by then, so holding its height
      // back would strand a composer in a band of dead space above the keys.
      expect(await measure(tester, keyboard: true), Space.md);
      expect(
        await measure(tester, keyboard: true),
        lessThan(await measure(tester, keyboard: false)),
      );
    });

    testWidgets('carries `extra` through either branch', (tester) async {
      expect(await measure(tester, keyboard: false, extra: 8), Space.navBar + 32);
      expect(await measure(tester, keyboard: true, extra: 8), Space.md + 8);
    });

    testWidgets('defaults to idle with no KeyboardVisibility above it',
        (tester) async {
      late double result;
      await tester.pumpWidget(
        MediaQuery(
          data: const MediaQueryData(padding: EdgeInsets.only(bottom: 24)),
          child: Builder(
            builder: (context) {
              result = navBarClearance(context);
              return const SizedBox();
            },
          ),
        ),
      );
      expect(result, Space.navBar + 24, reason: 'no shell, no keyboard');
    });
  });

  group('Space.snackBar', () {
    // A floating SnackBar is wrapped in SafeArea(bottom: false) by the
    // framework, so its inset is measured from the true bottom of the screen
    // and has to cover the gesture bar itself.
    const capsuleSlot = 92.6; // _barHeight at barSize 116, plus Space.md + 16

    test('clears the capsule over a gesture inset', () {
      expect(Space.snackBar, greaterThan(capsuleSlot + 24));
    });

    test('clears the capsule over a three-button inset', () {
      expect(Space.snackBar, greaterThan(capsuleSlot + 40));
    });

    test('sits above the room a page leaves for the capsule', () {
      expect(Space.snackBar, greaterThan(Space.navBar));
    });
  });
}
