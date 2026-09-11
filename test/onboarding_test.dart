import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:clip_sync_ai/onboarding/hello_screen.dart';
import 'package:clip_sync_ai/onboarding/onboarding_flow.dart';
import 'package:clip_sync_ai/onboarding/onboarding_gate.dart';

void main() {
  group('OnboardingFlow', () {
    testWidgets('opens on step one with the advance and the way out', (
      tester,
    ) async {
      await tester.pumpWidget(
        MaterialApp(home: OnboardingFlow(onComplete: () {})),
      );
      expect(find.text('Copy anything'), findsOneWidget);
      expect(find.text('Next'), findsOneWidget);
      expect(find.text('Skip'), findsOneWidget);
      // The progress rule states where you are, so the zero-padded count that
      // used to sit above it is gone rather than saying it a second time.
      expect(find.text('01 / 04'), findsNothing);
    });

    testWidgets('the action walks through all four pages', (tester) async {
      var completed = false;
      await tester.pumpWidget(
        MaterialApp(home: OnboardingFlow(onComplete: () => completed = true)),
      );
      const firstThree = <String>[
        'Copy anything',
        'The AI tidies it',
        'Talk, or snap it',
      ];
      for (var i = 0; i < firstThree.length; i++) {
        expect(find.text(firstThree[i]), findsOneWidget);
        await tester.tap(find.text('Next'));
        await tester.pumpAndSettle();
      }
      // The last page swaps the label and fills the ring.
      expect(find.text("You're set"), findsOneWidget);
      expect(find.text('Get started'), findsOneWidget);
      // Nothing left to skip on the last page, so Skip has faded off.
      expect(
        tester
            .widget<AnimatedOpacity>(
              find.ancestor(
                of: find.text('Skip'),
                matching: find.byType(AnimatedOpacity),
              ),
            )
            .opacity,
        0,
      );
      await tester.tap(find.text('Get started'));
      await tester.pumpAndSettle();
      expect(completed, isTrue);
    });

    testWidgets('a swipe advances too, since the greeting taught it', (
      tester,
    ) async {
      await tester.pumpWidget(
        MaterialApp(home: OnboardingFlow(onComplete: () {})),
      );
      await tester.fling(
        find.text('Copy anything'),
        const Offset(-300, 0),
        1000,
      );
      await tester.pumpAndSettle();
      expect(find.text('The AI tidies it'), findsOneWidget);
    });

    testWidgets('Skip completes immediately', (tester) async {
      var completed = false;
      await tester.pumpWidget(
        MaterialApp(home: OnboardingFlow(onComplete: () => completed = true)),
      );
      await tester.tap(find.text('Skip'));
      await tester.pumpAndSettle();
      expect(completed, isTrue);
    });
  });

  group('OnboardingGate', () {
    // The greeting rewrites itself on a loop, so nothing here can wait on
    // pumpAndSettle until a touch has stopped it.
    testWidgets('opens on the greeting, tutorial staged behind it', (
      tester,
    ) async {
      await tester.pumpWidget(const MaterialApp(home: OnboardingGate()));
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 2611));
      expect(find.byType(HelloScreen), findsOneWidget);
      expect(find.text('Swipe up to open'), findsOneWidget);
      // The tutorial is already mounted underneath, so the swipe uncovers it
      // rather than cutting to it.
      expect(find.byType(OnboardingFlow), findsOneWidget);
      expect(find.text('Copy anything'), findsOneWidget);
    });

    testWidgets('the greeting keeps writing until it is touched', (
      tester,
    ) async {
      await tester.pumpWidget(const MaterialApp(home: OnboardingGate()));
      await tester.pump();
      // Several cycles in, the pen is still going.
      await tester.pump(const Duration(seconds: 12));
      expect(tester.hasRunningAnimations, isTrue);
      // A tap settles it on the finished word without opening the app — the
      // hint still tells the truth afterwards.
      await tester.tap(find.byType(HelloScreen));
      await tester.pumpAndSettle();
      expect(tester.hasRunningAnimations, isFalse);
      expect(find.byType(HelloScreen), findsOneWidget);
      expect(find.text('Swipe up to open'), findsOneWidget);
    });

    testWidgets('a swipe up dismisses the greeting', (tester) async {
      await tester.pumpWidget(const MaterialApp(home: OnboardingGate()));
      await tester.pump();
      // Driven as a real finger — the recognizer folds the slop-crossing move
      // into the drag start, so the lift has to come from a later move.
      final gesture = await tester.startGesture(
        tester.getCenter(find.byType(HelloScreen)),
      );
      await gesture.moveBy(
        const Offset(0, -60),
        timeStamp: const Duration(milliseconds: 40),
      );
      await tester.pump();
      await gesture.moveBy(
        const Offset(0, -140),
        timeStamp: const Duration(milliseconds: 80),
      );
      await tester.pump();
      await gesture.up();
      await tester.pumpAndSettle();
      expect(find.byType(HelloScreen), findsNothing);
      // The page behind was held muted while the greeting was up, so it inks
      // itself on now rather than having played unseen.
      expect(find.text('Copy anything'), findsOneWidget);
    });

    testWidgets('a short drag springs back', (tester) async {
      await tester.pumpWidget(const MaterialApp(home: OnboardingGate()));
      await tester.pump();
      final gesture = await tester.startGesture(
        tester.getCenter(find.byType(HelloScreen)),
      );
      await gesture.moveBy(
        const Offset(0, -20),
        timeStamp: const Duration(milliseconds: 60),
      );
      await tester.pump();
      await gesture.moveBy(
        const Offset(0, -20),
        timeStamp: const Duration(milliseconds: 140),
      );
      await tester.pump();
      await gesture.up();
      await tester.pumpAndSettle();
      expect(find.byType(HelloScreen), findsOneWidget);
      expect(find.text('Swipe up to open'), findsOneWidget);
    });
  });
}
