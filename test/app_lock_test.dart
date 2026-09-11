import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:clip_sync_ai/main.dart';

/// The lock screen's biometric shortcut: offered only when enabled, and a
/// successful check opens the app without touching the PIN field.
void main() {
  Widget harness({
    bool biometricOffered = true,
    Future<bool> Function()? onBiometric,
    required VoidCallback onUnlock,
  }) {
    return MaterialApp(
      home: Scaffold(
        body: AppLockScreen(
          onUnlock: onUnlock,
          biometricOffered: biometricOffered,
          onBiometric: onBiometric,
        ),
      ),
    );
  }

  testWidgets('fingerprint button unlocks on a passing check',
      (tester) async {
    var unlocked = false;
    await tester.pumpWidget(harness(
      onBiometric: () async => true,
      onUnlock: () => unlocked = true,
    ));
    expect(find.byIcon(Icons.fingerprint_rounded), findsOneWidget);
    await tester.tap(find.byIcon(Icons.fingerprint_rounded));
    await tester.pumpAndSettle();
    expect(unlocked, isTrue);
  });

  testWidgets('a failing check stays locked with a hint', (tester) async {
    var unlocked = false;
    await tester.pumpWidget(harness(
      onBiometric: () async => false,
      onUnlock: () => unlocked = true,
    ));
    await tester.tap(find.byIcon(Icons.fingerprint_rounded));
    await tester.pumpAndSettle();
    expect(unlocked, isFalse);
    expect(find.textContaining('Biometric check failed'), findsOneWidget);
  });

  testWidgets('no fingerprint button when biometrics are off',
      (tester) async {
    await tester.pumpWidget(harness(
      biometricOffered: false,
      onUnlock: () {},
    ));
    expect(find.byIcon(Icons.fingerprint_rounded), findsNothing);
    // The PIN way in is still there.
    expect(find.text('Unlock'), findsOneWidget);
  });
}
