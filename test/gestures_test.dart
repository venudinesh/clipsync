import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hive/hive.dart';

import 'package:clip_sync_ai/main.dart';
import 'package:clip_sync_ai/ui/editorial.dart';

/// Every feed in the app — clips, notes, chat history — answers the same three
/// gestures through [MarginEntry], so the contract is tested once here rather
/// than three times through three different screens. The clip reader's own two
/// gestures are pinned at the bottom of the file.
void main() {
  /// A one row feed that actually drops the row when a dismissing act fires,
  /// which is what the real feeds do and what [Dismissible] requires.
  Widget feed({
    VoidCallback? onTap,
    VoidCallback? onLongPress,
    SwipeAct? right,
    SwipeAct? left,
    bool removeOnLeft = true,
  }) {
    bool present = true;
    return MaterialApp(
      home: Scaffold(
        body: StatefulBuilder(
          builder: (context, setState) {
            return ListView(
              children: [
                if (present)
                  MarginEntry(
                    margin: '09:41',
                    swipeId: 'row-1',
                    onTap: onTap,
                    onLongPress: onLongPress,
                    swipeRight: right,
                    swipeLeft: left == null
                        ? null
                        : SwipeAct(
                            icon: left.icon,
                            label: left.label,
                            tone: left.tone,
                            dismiss: left.dismiss,
                            onAct: () {
                              left.onAct();
                              if (removeOnLeft) setState(() => present = false);
                            },
                          ),
                    child: const Text('A clip in the feed'),
                  ),
              ],
            );
          },
        ),
      ),
    );
  }

  testWidgets('tap and long press are separate verbs', (tester) async {
    var taps = 0;
    var holds = 0;
    await tester.pumpWidget(
      feed(onTap: () => taps++, onLongPress: () => holds++),
    );

    await tester.tap(find.text('A clip in the feed'));
    await tester.pumpAndSettle();
    expect(taps, 1, reason: 'a tap opens the row');
    expect(holds, 0, reason: 'a tap must not also fire the hold');

    await tester.longPress(find.text('A clip in the feed'));
    await tester.pumpAndSettle();
    expect(holds, 1, reason: 'a hold reaches the row options');
    expect(taps, 1, reason: 'a hold must not also fire the tap');
  });

  testWidgets('dragging right runs the act and keeps the row', (tester) async {
    var pins = 0;
    await tester.pumpWidget(
      feed(
        onTap: () {},
        right: SwipeAct(
          icon: Icons.push_pin_outlined,
          label: 'Pin',
          onAct: () => pins++,
        ),
      ),
    );

    await tester.drag(find.text('A clip in the feed'), const Offset(400, 0));
    await tester.pumpAndSettle();

    expect(pins, 1, reason: 'past the threshold the act fires');
    expect(
      find.text('A clip in the feed'),
      findsOneWidget,
      reason: 'a pin is not a removal, so the row springs back',
    );
  });

  testWidgets('dragging left carries the row off', (tester) async {
    var deletes = 0;
    await tester.pumpWidget(
      feed(
        onTap: () {},
        left: SwipeAct(
          icon: Icons.delete_outline_rounded,
          label: 'Delete',
          dismiss: true,
          onAct: () => deletes++,
        ),
      ),
    );

    await tester.drag(find.text('A clip in the feed'), const Offset(-400, 0));
    await tester.pumpAndSettle();

    expect(deletes, 1);
    expect(find.text('A clip in the feed'), findsNothing);
  });

  testWidgets('a short drag does nothing at all', (tester) async {
    var pins = 0;
    var deletes = 0;
    await tester.pumpWidget(
      feed(
        onTap: () {},
        right: SwipeAct(
          icon: Icons.push_pin_outlined,
          label: 'Pin',
          onAct: () => pins++,
        ),
        left: SwipeAct(
          icon: Icons.delete_outline_rounded,
          label: 'Delete',
          dismiss: true,
          onAct: () => deletes++,
        ),
      ),
    );

    await tester.drag(find.text('A clip in the feed'), const Offset(-40, 0));
    await tester.pumpAndSettle();
    await tester.drag(find.text('A clip in the feed'), const Offset(40, 0));
    await tester.pumpAndSettle();

    expect(pins, 0);
    expect(deletes, 0);
    expect(find.text('A clip in the feed'), findsOneWidget);
  });

  test('a swipeable row without an id is a programming error', () {
    expect(
      () => MarginEntry(
        margin: '09:41',
        swipeLeft: SwipeAct(
          icon: Icons.delete_outline_rounded,
          label: 'Delete',
          onAct: () {},
        ),
        child: const Text('no id'),
      ),
      throwsAssertionError,
    );
  });

  /// The reader's two gestures. Both are here because one of them shipped
  /// broken: the drag that puts the reader away counted an
  /// [OverscrollNotification], and bouncing physics never sends one — it lets
  /// the position travel out of range and reports an ordinary scroll update. It
  /// cost a phone to notice, so it costs a test to keep.
  group('the clip reader', () {
    late Directory dir;
    late Box box;
    late List<String> ids;

    setUpAll(() async {
      dir = Directory.systemTemp.createTempSync('clip_reader_gestures');
      Hive.init(dir.path);
      box = await Hive.openBox('reader_clips');
      final clips = [
        ClipEntry(
          rawText: 'The first clip',
          processedMarkdown: 'The first clip',
          timestamp: DateTime(2026, 9, 5, 9, 41),
        ),
        ClipEntry(
          rawText: 'The second clip',
          processedMarkdown: 'The second clip',
          timestamp: DateTime(2026, 9, 5, 9, 42),
        ),
      ];
      for (final clip in clips) {
        await box.put(clip.id, clip.toMap());
      }
      ids = clips.map((c) => c.id).toList();
    });

    tearDownAll(() async {
      await Hive.close();
      dir.deleteSync(recursive: true);
    });

    /// The reader on a route of its own, so closing it has something to pop
    /// back to. Animations are turned off: the ambient backdrop loops forever
    /// otherwise and nothing would ever settle.
    Future<void> openReader(WidgetTester tester, {int at = 0}) async {
      await tester.pumpWidget(
        MaterialApp(
          builder: (context, child) => MediaQuery(
            data: MediaQuery.of(context).copyWith(disableAnimations: true),
            child: child!,
          ),
          home: Scaffold(
            body: Builder(
              builder: (context) => TextButton(
                onPressed: () => Navigator.of(context).push(
                  MaterialPageRoute(
                    builder: (_) => ClipReader(
                      ids: ids,
                      startIndex: at,
                      box: box,
                      onOptions: (_) async {},
                      onCopy: (_) {},
                    ),
                  ),
                ),
                child: const Text('the feed'),
              ),
            ),
          ),
        ),
      );
      await tester.tap(find.text('the feed'));
      await tester.pumpAndSettle();
    }

    testWidgets('dragging the body down puts the reader away', (tester) async {
      await openReader(tester);
      expect(find.textContaining('1 of 2'), findsOneWidget);

      await tester.drag(find.byType(PageView), const Offset(0, 400));
      await tester.pumpAndSettle();

      expect(
        find.text('the feed'),
        findsOneWidget,
        reason: 'dragging past the top of the text is how the reader is closed',
      );
    });

    testWidgets('a small drag down keeps the reader open', (tester) async {
      await openReader(tester);

      await tester.drag(find.byType(PageView), const Offset(0, 40));
      await tester.pumpAndSettle();

      expect(
        find.textContaining('1 of 2'),
        findsOneWidget,
        reason: 'a nudge is not a request to leave',
      );
    });

    testWidgets('holding the text still selects it', (tester) async {
      await openReader(tester);

      await tester.longPress(find.text('The first clip'));
      await tester.pumpAndSettle();

      expect(
        find.text('Copy'),
        findsOneWidget,
        reason: 'moving selection out to the pager must not have cost the hold '
            'that selects a word',
      );
    });

    testWidgets('dragging sideways reaches the next clip', (tester) async {
      await openReader(tester);
      expect(find.textContaining('1 of 2'), findsOneWidget);

      await tester.drag(find.byType(PageView), const Offset(-500, 0));
      await tester.pumpAndSettle();

      expect(
        find.textContaining('2 of 2'),
        findsOneWidget,
        reason: 'the printed position is the only hint the page turns, so it '
            'has to be the thing that moves',
      );
      expect(
        find.textContaining('The second clip', findRichText: true),
        findsWidgets,
        reason: 'selection must not have claimed the horizontal drag',
      );
    });
  });
}
