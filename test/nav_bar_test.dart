import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:clip_sync_ai/main.dart' show NavBarConfig;
import 'package:clip_sync_ai/ui/design_tokens.dart';
import 'package:clip_sync_ai/ui/glass_panel.dart';
import 'package:clip_sync_ai/ui/nav_bar.dart';

void main() {
  setUp(() {
    appGlassMode.value = true;
  });

  Widget harness({
    required bool holdToSwipe,
    ValueChanged<int>? onSelect,
    // Mirrors the shipped default. `defaults are the shipped values` below is
    // what pins that, so this only has to name the case under test.
    bool invertSwipe = true,
  }) {
    var selected = 0;
    List<LiquidNavDestination> destinations() => const [
          LiquidNavDestination(icon: Icons.copy, selectedIcon: Icons.copy, label: 'Clips'),
          LiquidNavDestination(icon: Icons.chat, selectedIcon: Icons.chat, label: 'Chat'),
          LiquidNavDestination(icon: Icons.notes, selectedIcon: Icons.notes, label: 'Notes'),
        ];
    return MaterialApp(
      home: Scaffold(
        body: StatefulBuilder(
          builder: (context, setState) {
            return LiquidGlassNavBar(
              selectedIndex: selected,
              onDestinationSelected: (i) {
                setState(() => selected = i);
                onSelect?.call(i);
              },
              destinations: destinations(),
              holdToSwipe: holdToSwipe,
              invertSwipe: invertSwipe,
            );
          },
        ),
      ),
    );
  }

  testWidgets('holdToSwipe ON: a horizontal swipe switches tabs', (tester) async {
    final taps = <int>[];
    await tester.pumpWidget(harness(holdToSwipe: true, onSelect: taps.add));
    final nav = find.byType(LiquidGlassNavBar);

    // Shipped default is inverted, the direct-manipulation mapping: the
    // highlight travels with the finger, so a fling right selects the tab to
    // the right.
    await tester.fling(nav, const Offset(300, 0), 800);
    await tester.pumpAndSettle();
    expect(taps, [1], reason: 'swiping should change from Clips to Chat');

    await tester.fling(nav, const Offset(300, 0), 800);
    await tester.pumpAndSettle();
    expect(taps, [1, 2], reason: 'swiping again should move to Notes');
  });

  testWidgets('invertSwipe OFF reverses the mapping', (tester) async {
    final taps = <int>[];
    await tester.pumpWidget(harness(
      holdToSwipe: true,
      invertSwipe: false,
      onSelect: taps.add,
    ));
    final nav = find.byType(LiquidGlassNavBar);

    // Un-inverted, the strip moves under the finger instead: left is forward.
    await tester.fling(nav, const Offset(-300, 0), 800);
    await tester.pumpAndSettle();
    expect(taps, [1], reason: 'un-inverted, a fling left should move forward');

    // And the far edge holds: at slot 0 a backward fling has nowhere to go.
    await tester.fling(nav, const Offset(300, 0), 800);
    await tester.pumpAndSettle();
    expect(taps, [1, 0], reason: 'a fling right should move back to Clips');
    await tester.fling(nav, const Offset(300, 0), 800);
    await tester.pumpAndSettle();
    expect(taps, [1, 0], reason: 'no move is reported past the first tab');
  });

  testWidgets('holdToSwipe OFF: a horizontal swipe does NOT switch tabs', (tester) async {
    final taps = <int>[];
    await tester.pumpWidget(harness(holdToSwipe: false, onSelect: taps.add));
    final nav = find.byType(LiquidGlassNavBar);

    await tester.fling(nav, const Offset(-300, 0), 800);
    await tester.pumpAndSettle();
    expect(taps, isEmpty, reason: 'swiping should be ignored when holdToSwipe is OFF');
  });

  testWidgets('holdToSwipe OFF: tapping a tab still switches', (tester) async {
    final taps = <int>[];
    await tester.pumpWidget(harness(holdToSwipe: false, onSelect: taps.add));

    await tester.tap(find.text('Chat'));
    await tester.pumpAndSettle();
    expect(taps, [1], reason: 'tapping a tab should switch even when swiping is off');
  });

  group('defaults are the shipped values', () {
    // The bar's own fallbacks and NavBarConfig's have to agree, or a screen
    // that builds the widget directly looks different from one that reads the
    // stored config.
    const cfg = NavBarConfig();

    test('NavBarConfig ships clear glass carried by the lens rim', () {
      expect(cfg.barWidth, 89);
      expect(cfg.opacity, 0, reason: 'no tint');
      expect(cfg.blurSigma, 0, reason: 'no frost');
      expect(cfg.refractionDepth, 100);
      expect(cfg.refractionStrength, 100);
      expect(cfg.chromaticSplit, 0, reason: 'no colour fringe');
      expect(cfg.invertSwipe, isTrue, reason: 'the highlight follows the finger');
      expect(cfg.enabled, isTrue);
      expect(cfg.holdToSwipe, isTrue);
    });

    testWidgets('LiquidGlassNavBar agrees with NavBarConfig', (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: LiquidGlassNavBar(
              selectedIndex: 0,
              onDestinationSelected: (_) {},
              destinations: const [
                LiquidNavDestination(
                    icon: Icons.copy, selectedIcon: Icons.copy, label: 'Clips'),
              ],
            ),
          ),
        ),
      );
      final bar = tester.widget<LiquidGlassNavBar>(
        find.byType(LiquidGlassNavBar),
      );
      expect(bar.barWidth, cfg.barWidth);
      expect(bar.opacity, cfg.opacity);
      expect(bar.blurSigma, cfg.blurSigma);
      expect(bar.refractionDepth, cfg.refractionDepth);
      expect(bar.refractionStrength, cfg.refractionStrength);
      expect(bar.chromaticSplit, cfg.chromaticSplit);
      expect(bar.invertSwipe, cfg.invertSwipe);
      expect(bar.holdToSwipe, cfg.holdToSwipe);
      expect(bar.barSize, cfg.barSize);
      expect(bar.position, cfg.position);
      expect(bar.cornerRoundness, cfg.cornerRoundness);
      expect(bar.swipeSensitivity, cfg.swipeSensitivity);
      expect(bar.useIOSGlassMode, cfg.useIOSGlassMode);
    });

    testWidgets('a zero blur skips the backdrop filter entirely', (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: LiquidGlassNavBar(
              selectedIndex: 0,
              onDestinationSelected: (_) {},
              destinations: const [
                LiquidNavDestination(
                    icon: Icons.copy, selectedIcon: Icons.copy, label: 'Clips'),
              ],
            ),
          ),
        ),
      );
      // The body pass is gone at sigma 0; the lens rim's own filter is the only
      // one left, and it is what draws clear glass.
      expect(find.byType(BackdropFilter), findsOneWidget);
    });
  });

  /// The shipped defaults ask for no tint and no frost, which leaves the tabs
  /// sitting on whatever the page has scrolled underneath them. On a long
  /// Settings list that was two layers of type in the same pixels. The row has
  /// to carry its own contrast, and only in that case.
  group('a clear capsule lifts its own labels', () {
    Future<void> bar(
      WidgetTester tester, {
      double opacity = 0,
      double blurSigma = 0,
    }) =>
        tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: LiquidGlassNavBar(
                selectedIndex: 0,
                onDestinationSelected: (_) {},
                opacity: opacity,
                blurSigma: blurSigma,
                destinations: const [
                  LiquidNavDestination(
                      icon: Icons.copy, selectedIcon: Icons.copy, label: 'Clips'),
                  LiquidNavDestination(
                      icon: Icons.chat, selectedIcon: Icons.chat, label: 'Chat'),
                ],
              ),
            ),
          ),
        );

    List<Shadow>? labelHalo(WidgetTester tester, String label) =>
        tester.widget<Text>(find.text(label)).style?.shadows;
    List<Shadow>? iconHalo(WidgetTester tester, IconData icon) =>
        tester.widget<Icon>(find.byIcon(icon)).shadows;
    double idleAlpha(WidgetTester tester) =>
        tester.widget<Text>(find.text('Chat')).style!.color!.a;

    testWidgets('at the shipped values every glyph carries a halo', (
      tester,
    ) async {
      await bar(tester);
      // The active tab and an idle one alike: page text passes behind all five.
      expect(labelHalo(tester, 'Clips'), isNotEmpty);
      expect(labelHalo(tester, 'Chat'), isNotEmpty);
      expect(iconHalo(tester, Icons.copy), isNotEmpty);
      expect(iconHalo(tester, Icons.chat), isNotEmpty);
    });

    testWidgets('a tint alone is surface enough to drop it', (tester) async {
      await bar(tester, opacity: 30);
      expect(labelHalo(tester, 'Chat'), isEmpty);
      expect(iconHalo(tester, Icons.chat), isEmpty);
    });

    testWidgets('so is a frost alone', (tester) async {
      await bar(tester, blurSigma: 18);
      expect(labelHalo(tester, 'Chat'), isEmpty);
    });

    testWidgets('Material mode never gets one', (tester) async {
      // The M3 capsule is opaque, so a halo there would only read as a smudge.
      appGlassMode.value = false;
      await bar(tester);
      expect(labelHalo(tester, 'Chat'), isEmpty);
      expect(iconHalo(tester, Icons.chat), isEmpty);
    });

    testWidgets('the idle ink is heavier on clear glass than on a tint', (
      tester,
    ) async {
      await bar(tester);
      final clear = idleAlpha(tester);
      await bar(tester, opacity: 30);
      expect(clear, greaterThan(idleAlpha(tester)));
    });
  });

  /// Raising the labels' contrast fixes the labels. The sentence they are sitting
  /// on top of is a separate problem, and this is what answers it.
  group('the content fade behind the capsule', () {
    Future<void> edge(WidgetTester tester, {double systemInset = 24}) =>
        tester.pumpWidget(
          MaterialApp(
            home: MediaQuery(
              data: MediaQueryData(
                padding: EdgeInsets.only(bottom: systemInset),
              ),
              child: const Scaffold(
                body: Stack(
                  children: [
                    Positioned(
                      left: 0,
                      right: 0,
                      bottom: 0,
                      child: NavBarScrollEdge(),
                    ),
                  ],
                ),
              ),
            ),
          ),
        );

    LinearGradient gradient(WidgetTester tester) =>
        (tester
                    .widget<DecoratedBox>(find.descendant(
                      of: find.byType(NavBarScrollEdge),
                      matching: find.byType(DecoratedBox),
                    ))
                    .decoration
                as BoxDecoration)
            .gradient! as LinearGradient;

    testWidgets('is exactly the room every page already reserves', (
      tester,
    ) async {
      // Tied to the clearance rather than a number of its own, so a composer or
      // a last row that correctly cleared the capsule sits at the zero stop and
      // cannot be dimmed. A stray literal here would quietly grey them out.
      await edge(tester);
      expect(tester.getSize(find.byType(NavBarScrollEdge)).height,
          Space.navBar + 24);
    });

    testWidgets('follows the system inset the capsule floats in', (
      tester,
    ) async {
      await edge(tester, systemInset: 0);
      expect(
          tester.getSize(find.byType(NavBarScrollEdge)).height, Space.navBar);
    });

    testWidgets('starts at nothing and never finishes opaque', (tester) async {
      await edge(tester);
      final stops = gradient(tester).colors;
      expect(stops.first.a, 0, reason: 'a fade with a visible top edge is a bar');
      expect(stops.last.a, lessThan(1),
          reason: 'a hint of what is behind the glass is why it is glass');
      expect(stops.last.a, greaterThan(0.9), reason: 'but only a hint');
      // Monotonic, or the fade reads as a band rather than a gradient.
      for (var i = 1; i < stops.length; i++) {
        expect(stops[i].a, greaterThan(stops[i - 1].a));
      }
    });

    testWidgets('cannot take a tap from the page under it', (tester) async {
      await edge(tester);
      expect(
        find.ancestor(
          of: find.byType(DecoratedBox),
          matching: find.byType(IgnorePointer),
        ),
        findsWidgets,
      );
    });

    testWidgets('collapses with the keyboard, like the clearance it mirrors', (
      tester,
    ) async {
      await tester.pumpWidget(
        MaterialApp(
          home: MediaQuery(
            data: const MediaQueryData(
              padding: EdgeInsets.only(bottom: 24),
            ),
            child: const KeyboardVisibility(
              visible: true,
              child: Scaffold(body: NavBarScrollEdge()),
            ),
          ),
        ),
      );
      expect(tester.getSize(find.byType(NavBarScrollEdge)).height, Space.md);
    });
  });
}