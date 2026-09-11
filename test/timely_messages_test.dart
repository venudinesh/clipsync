import 'dart:math';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:clip_sync_ai/features/timely_messages.dart';

/// The greeting strip is copy shipped in the binary, so the interesting tests
/// are the ones that hold every line to the app's own house rules, plus the
/// clock arithmetic that decides which pool a line comes from.
void main() {
  group('dayPartAt', () {
    DayPart at(int hour) => dayPartAt(DateTime(2026, 9, 4, hour));

    test('classifies each boundary hour into the part it opens', () {
      expect(at(0), DayPart.midnight);
      expect(at(5), DayPart.morning);
      expect(at(12), DayPart.afternoon);
      expect(at(17), DayPart.earlyEvening);
      expect(at(19), DayPart.evening);
      expect(at(22), DayPart.night);
    });

    test('classifies the hour before each boundary into the earlier part', () {
      expect(at(4), DayPart.midnight);
      expect(at(11), DayPart.morning);
      expect(at(16), DayPart.afternoon);
      expect(at(18), DayPart.earlyEvening);
      expect(at(21), DayPart.evening);
      expect(at(23), DayPart.night);
    });

    test('every hour of the day lands somewhere', () {
      for (var hour = 0; hour < 24; hour++) {
        expect(() => at(hour), returnsNormally);
      }
    });
  });

  group('greetingFor', () {
    test('gives every part a greeting that ends in punctuation', () {
      for (final part in DayPart.values) {
        final greeting = greetingFor(part);
        expect(greeting, isNotEmpty);
        expect(
          '.!?'.contains(greeting[greeting.length - 1]),
          isTrue,
          reason: '$part greeting does not end in punctuation',
        );
      }
    });

    test('the two evening parts share one greeting', () {
      expect(greetingFor(DayPart.earlyEvening), 'Good evening.');
      expect(greetingFor(DayPart.evening), 'Good evening.');
    });
  });

  group('messagesFor', () {
    test('no pool is empty', () {
      for (final part in DayPart.values) {
        expect(messagesFor(part), isNotEmpty, reason: '$part has no messages');
      }
    });

    test('night and midnight share a pool', () {
      expect(messagesFor(DayPart.night), same(messagesFor(DayPart.midnight)));
    });
  });

  group('message copy', () {
    test('nothing is blank or carries stray whitespace', () {
      for (final message in allTimelyMessages) {
        expect(message, isNotEmpty);
        expect(message, message.trim());
      }
    });

    test('no em dashes or en dashes, per the house style', () {
      for (final message in allTimelyMessages) {
        expect(message.contains('—'), isFalse, reason: message);
        expect(message.contains('–'), isFalse, reason: message);
      }
    });

    test('nothing names a ROM, a launcher or another product', () {
      // The source strings were lifted from an Android launcher and were full
      // of distribution in-jokes. None of them mean anything here, so this is
      // the guard that keeps them from creeping back in on a later edit.
      final banned = <RegExp>[
        RegExp(r'\brom\b', caseSensitive: false),
        RegExp(r'\broms\b', caseSensitive: false),
        RegExp('bliss', caseSensitive: false),
        RegExp('lineage', caseSensitive: false),
        RegExp('unicorn', caseSensitive: false),
        RegExp('xtended', caseSensitive: false),
        RegExp('paranoi', caseSensitive: false),
        RegExp('pixel experience', caseSensitive: false),
        RegExp('derp', caseSensitive: false),
        RegExp('buildbot', caseSensitive: false),
        RegExp('sushi', caseSensitive: false),
        RegExp('ice cold', caseSensitive: false),
        RegExp('evolution', caseSensitive: false),
        RegExp('quickspace', caseSensitive: false),
        RegExp('premium', caseSensitive: false),
        RegExp('easter egg', caseSensitive: false),
        RegExp('flash', caseSensitive: false),
        RegExp('launcher', caseSensitive: false),
        RegExp('rm -rf'),
        RegExp('HEAD'),
      ];
      for (final message in allTimelyMessages) {
        for (final pattern in banned) {
          expect(
            pattern.hasMatch(message),
            isFalse,
            reason: '"$message" matches ${pattern.pattern}',
          );
        }
      }
    });

    test('no doubled full stops left over from the source', () {
      for (final message in allTimelyMessages) {
        expect(message.endsWith('..'), isFalse, reason: message);
      }
    });

    test('no pool repeats a line within itself', () {
      for (final part in DayPart.values) {
        final pool = messagesFor(part);
        expect(
          pool.toSet().length,
          pool.length,
          reason: '$part repeats a line',
        );
      }
    });
  });

  group('timelyGreeting', () {
    final morning = DateTime(2026, 9, 4, 8, 30);

    test('the greeting always matches the hour', () {
      for (var hour = 0; hour < 24; hour++) {
        final when = DateTime(2026, 9, 4, hour);
        expect(
          timelyGreeting(when, random: Random(hour)).greeting,
          greetingFor(dayPartAt(when)),
        );
      }
    });

    test('the line comes from the hour pool or the wildcard pool', () {
      final everything = allTimelyMessages.toSet();
      for (var seed = 0; seed < 200; seed++) {
        expect(
          everything,
          contains(timelyGreeting(morning, random: Random(seed)).message),
        );
      }
    });

    test('a seed makes the pick reproducible', () {
      expect(
        timelyGreeting(morning, random: Random(42)),
        timelyGreeting(morning, random: Random(42)),
      );
    });

    test('avoid keeps the line on screen from coming back', () {
      final pool = messagesFor(DayPart.morning);
      final avoided = pool.first;
      final rng = Random(7);
      var seen = 0;
      for (var i = 0; i < 300; i++) {
        if (timelyGreeting(morning, random: rng, avoid: avoided).message ==
            avoided) {
          seen++;
        }
      }
      // Five independent misses on a pool this size is vanishingly unlikely,
      // and the wildcard pool does not contain this line either.
      expect(seen, 0);
    });

    test('the wildcard does fire, and is rare', () {
      final timed = messagesFor(DayPart.morning).toSet();
      var wildcards = 0;
      final rng = Random(11);
      for (var i = 0; i < 2000; i++) {
        if (!timed.contains(timelyGreeting(morning, random: rng).message)) {
          wildcards++;
        }
      }
      expect(wildcards, greaterThan(0));
      expect(wildcards, lessThan(400)); // well under a quarter of all picks
    });

    test('never prints a line that only restates the greeting', () {
      // "Good evening." over "It is evening right now." is the masthead saying
      // one thing twice, which is what the source arrays do when they are read
      // without a greeting above them.
      for (var hour = 0; hour < 24; hour++) {
        final when = DateTime(2026, 9, 4, hour);
        for (var seed = 0; seed < 120; seed++) {
          final picked = timelyGreeting(when, random: Random(seed));
          expect(
            restatesGreeting(picked.message, picked.greeting),
            isFalse,
            reason: '"${picked.greeting}" over "${picked.message}"',
          );
        }
      }
    });
  });

  group('restatesGreeting', () {
    test('catches the time of day named twice', () {
      expect(restatesGreeting('It is evening right now.', 'Good evening.'),
          isTrue);
      expect(restatesGreeting('It is afternoon right now.', 'Good afternoon.'),
          isTrue);
    });

    test('spares a line that adds something the greeting does not say', () {
      // "late" is not in either late-night greeting, so the pair still carries
      // two facts rather than one.
      expect(restatesGreeting('It is late right now.', 'Good night.'), isFalse);
      expect(
        restatesGreeting('It is late right now.', 'Solemn midnight.'),
        isFalse,
      );
    });

    test('leaves ordinary lines alone', () {
      expect(restatesGreeting('Enjoy the night.', 'Good evening.'), isFalse);
      expect(restatesGreeting('It is what it is.', 'Good evening.'), isFalse);
      expect(restatesGreeting('', 'Good evening.'), isFalse);
    });

    test('is not fooled by case', () {
      expect(restatesGreeting('IT IS EVENING RIGHT NOW.', 'good evening.'),
          isTrue);
    });
  });

  group('nextDayPartBoundary', () {
    test('returns the next boundary later the same day', () {
      expect(
        nextDayPartBoundary(DateTime(2026, 9, 4, 3, 20)),
        DateTime(2026, 9, 4, 5),
      );
      expect(
        nextDayPartBoundary(DateTime(2026, 9, 4, 12, 0)),
        DateTime(2026, 9, 4, 17),
      );
      expect(
        nextDayPartBoundary(DateTime(2026, 9, 4, 18, 59)),
        DateTime(2026, 9, 4, 19),
      );
    });

    test('rolls to tomorrow past the last boundary', () {
      expect(
        nextDayPartBoundary(DateTime(2026, 9, 4, 22, 30)),
        DateTime(2026, 9, 5),
      );
      // Month end, which is where a hand rolled +1 day would break.
      expect(
        nextDayPartBoundary(DateTime(2026, 9, 30, 23, 59)),
        DateTime(2026, 10, 1),
      );
    });

    test('is always strictly in the future', () {
      for (var hour = 0; hour < 24; hour++) {
        for (final minute in <int>[0, 30, 59]) {
          final now = DateTime(2026, 9, 4, hour, minute);
          expect(nextDayPartBoundary(now).isAfter(now), isTrue, reason: '$now');
        }
      }
    });
  });

  group('TimelyGreetingView', () {
    testWidgets('renders a greeting and a line, and a tap keeps it valid', (
      tester,
    ) async {
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: TimelyGreetingView(
              builder: (context, greeting) => Column(
                children: [Text(greeting.greeting), Text(greeting.message)],
              ),
            ),
          ),
        ),
      );

      final greetings = DayPart.values
          .map(greetingFor)
          .toSet()
          .toList(growable: false);
      Finder shown() => find.byWidgetPredicate(
        (w) => w is Text && greetings.contains(w.data),
      );

      expect(shown(), findsOneWidget);

      await tester.tap(find.byType(TimelyGreetingView));
      await tester.pump();
      expect(shown(), findsOneWidget);

      // Dispose the tree so the rollover timer is cancelled before teardown.
      await tester.pumpWidget(const SizedBox());
    });

    testWidgets('honours tapToChange: false by not swallowing taps', (
      tester,
    ) async {
      var taps = 0;
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: GestureDetector(
              onTap: () => taps++,
              child: TimelyGreetingView(
                tapToChange: false,
                builder: (context, greeting) => SizedBox(
                  width: 200,
                  height: 60,
                  child: Text(greeting.message),
                ),
              ),
            ),
          ),
        ),
      );

      await tester.tap(find.byType(TimelyGreetingView));
      await tester.pump();
      expect(taps, 1);

      await tester.pumpWidget(const SizedBox());
    });
  });
}
