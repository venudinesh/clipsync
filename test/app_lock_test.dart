import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hive_ce/hive.dart';

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

  group('the lock is a route above everything', () {
    late Directory dir;

    setUp(() async {
      dir = await Directory.systemTemp.createTemp('clipsync-lock');
      Hive.init(dir.path);
      await Hive.openBox(AppDefaults.hiveSettingsBox);
      Hive.box(AppDefaults.hiveSettingsBox).put('appPin', '1234');
    });

    tearDown(() async {
      await Hive.close();
      try {
        await dir.delete(recursive: true);
      } catch (_) {}
    });

    testWidgets('covers a pushed page and returns to it after unlocking',
        (tester) async {
      final nav = GlobalKey<NavigatorState>();
      await tester.pumpWidget(
        MaterialApp(
          navigatorKey: nav,
          home: const Scaffold(body: Text('HOME')),
        ),
      );

      // Stand in for the clip reader or a sheet, which used to sit above the
      // in-tree lock overlay.
      nav.currentState!.push(
        MaterialPageRoute<void>(
          builder: (_) => const Scaffold(body: Text('PUSHED PAGE')),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text('PUSHED PAGE'), findsOneWidget);

      Route<void>? lock;
      lock = appLockRoute(
        onUnlock: () => nav.currentState!.removeRoute(lock!),
        biometricOffered: false,
        onBiometric: () async => false,
      );
      nav.currentState!.push(lock);
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 20));

      expect(find.byType(AppLockScreen), findsOneWidget);
      expect(find.text('PUSHED PAGE'), findsNothing);

      await tester.enterText(find.byType(TextField), '1234');
      await tester.tap(find.text('Unlock'));
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 20));

      expect(find.byType(AppLockScreen), findsNothing);
      expect(find.text('PUSHED PAGE'), findsOneWidget);
    });
  });
}
