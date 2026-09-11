import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:clip_sync_ai/ui/editorial.dart';

/// Two layout facts that only a phone caught, and that a screenshot is the only
/// other way to notice: where a feed row's control sits in the margin, and
/// whether a field that is meant to be a line of type quietly grows a box.
void main() {
  group('the margin is one right-aligned column', () {
    /// A feed row with a time in the margin and the row's own control under it.
    Future<void> row(WidgetTester tester, {AlignmentGeometry? align}) =>
        tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: MarginEntry(
                margin: '09:41',
                marginAction: IconAction(
                  icon: Icons.more_horiz,
                  tooltip: 'Clip options',
                  size: 17,
                  dense: true,
                  alignment: align ?? Alignment.centerRight,
                  onPressed: () {},
                ),
                child: const Text('A clip in the feed'),
              ),
            ),
          ),
        );

    testWidgets('the control hangs on the same edge as the time', (
      tester,
    ) async {
      await row(tester);
      expect(
        tester.getTopRight(find.byIcon(Icons.more_horiz)).dx,
        moreOrLessEquals(tester.getTopRight(find.text('09:41')).dx, epsilon: 0.5),
        reason: 'a glyph centred in a hit box wider than itself stops short of '
            'the margin edge and reads as dropped rather than filed',
      );
    });

    testWidgets('centring is still the default, for a row of controls', (
      tester,
    ) async {
      // Where controls sit side by side each one's slack is what spaces it from
      // the next, so the reply strip in chat must keep the centred box.
      await row(tester, align: Alignment.center);
      expect(
        tester.getTopRight(find.byIcon(Icons.more_horiz)).dx,
        lessThan(tester.getTopRight(find.text('09:41')).dx),
      );
    });

    testWidgets('moving the glyph does not shrink the target', (tester) async {
      var taps = 0;
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: MarginEntry(
              margin: '09:41',
              marginAction: IconAction(
                icon: Icons.more_horiz,
                tooltip: 'Clip options',
                size: 17,
                dense: true,
                alignment: Alignment.centerRight,
                onPressed: () => taps++,
              ),
              child: const Text('A clip in the feed'),
            ),
          ),
        ),
      );
      // The dense box is 40x36 and the glyph is 17 across: the target has to be
      // the box, not the glyph, or the dots become a thumb-sized miss.
      final box = tester.getSize(
        find.ancestor(
          of: find.byIcon(Icons.more_horiz),
          matching: find.byType(Align),
        ).first,
      );
      expect(box.width, 40);
      expect(box.height, 36);

      await tester.tap(find.byIcon(Icons.more_horiz));
      await tester.pumpAndSettle();
      expect(taps, 1);
    });
  });

  group('a field that is not a box', () {
    /// The app's own input theme: it describes a *boxed* field, because one
    /// dialog in the app uses one. Every other field in the app is a line of
    /// type, and `InputDecoration` inherits `filled` from the theme unless it
    /// says otherwise — which is how a tinted plate ended up hugging the type
    /// inside the clips composer.
    final boxed = ThemeData(
      brightness: Brightness.dark,
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: Colors.white.withValues(alpha: 0.05),
        border: const OutlineInputBorder(),
      ),
    );

    /// What the framework will actually use, once the theme has filled in every
    /// value the widget left null. Asserting the resolved value rather than the
    /// declared one is what makes this a test of the interaction and not of a
    /// literal.
    bool? resolvedFill(WidgetTester tester) {
      final field = tester.widget<TextField>(find.byType(TextField));
      return field.decoration!
          .applyDefaults(boxed.inputDecorationTheme)
          .filled;
    }

    testWidgets('the composer keeps the panel as its only surface', (
      tester,
    ) async {
      await tester.pumpWidget(
        MaterialApp(
          theme: boxed,
          home: Scaffold(
            body: Composer(
              controller: TextEditingController(),
              hint: 'Paste or type something worth keeping',
              onSubmit: () {},
            ),
          ),
        ),
      );
      expect(resolvedFill(tester), isFalse);
    });

    testWidgets('an inline field stays a line on a rule', (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          theme: boxed,
          home: Scaffold(
            body: InlineField(
              controller: TextEditingController(),
              hint: 'Search',
            ),
          ),
        ),
      );
      expect(resolvedFill(tester), isFalse);
    });
  });
}
