import 'dart:async';
import 'dart:math';

import 'package:flutter/material.dart';

/// Time of day greetings, and the short one liner that sits under them.
///
/// A salutation that tracks the clock plus a rotating aside: sometimes a
/// reminder, sometimes a small fact, sometimes just a nudge. It costs nothing,
/// touches no network, and gives the top of the page something to say other
/// than the app name.
///
/// Every message below is a compile time constant. Nothing is fetched and
/// nothing is stored.

/// Which slice of the day the clock is in. The slices are deliberately uneven,
/// because an evening behaves differently at six than it does at ten.
enum DayPart {
  /// 00:00 to 04:59.
  midnight,

  /// 05:00 to 11:59.
  morning,

  /// 12:00 to 16:59.
  afternoon,

  /// 17:00 to 18:59.
  earlyEvening,

  /// 19:00 to 21:59.
  evening,

  /// 22:00 to 23:59.
  night,
}

/// The hours a new [DayPart] begins on, ascending. Used both to classify a
/// timestamp and to work out when the next change is due.
const List<int> dayPartBoundaries = <int>[0, 5, 12, 17, 19, 22];

// ── Messages ────────────────────────────────────────────────────────────────

const List<String> _morningMessages = <String>[
  'Good morning!',
  'Good morning, time to rock!',
  'What a beautiful day!',
  'Have a nice day!',
  'What about a small 5 minutes nap?',
  "That's one way to wake up.",
  'You should change your alarm sounds.',
  "Looks like it's a beautiful day, isn't it?",
  "Let's get this bread.",
  'Check the weather just in case.',
  'Feeling a bit sleepy yet?',
  'Have you looked outside yet?',
  "Let's get this done.",
  'Good luck today!',
  "Don't forget about mental health.",
  'Life is good.',
  'Enjoy your breakfast.',
];

const List<String> _afternoonMessages = <String>[
  "Don't forget to stay hydrated!",
  'Remember to eat some snacks.',
  'UV rays are more harmful in the afternoon.',
  'It is afternoon right now.',
  "Always check your device's battery level!",
  'Remember to check your email and messages.',
  'Please check your to-do list if you have one.',
  "Don't forget to prepare your dinner.",
  'Enjoy your day.',
  'Hang on! The day is almost done!',
  'Have some fun.',
];

const List<String> _earlyEveningMessages = <String>[
  'Always stay hydrated!',
  '8 hours of sleep is a must.',
  'It is evening right now.',
  'Remember to charge up before you sleep.',
  'Remember to check your email and messages.',
  'Please check your to-do list if you have one.',
  'Remember to eat dinner.',
  'Always check the lights and sockets.',
  'Enjoy the night.',
  'Hey there! Give yourself some rest!',
  'Relax and get some rest.',
];

const List<String> _eveningMessages = <String>[
  "Don't drink too much water before sleeping.",
  'It is evening right now.',
  'Try to avoid overnight charging if possible.',
  'Remember to check your email and messages.',
  'Please check your to-do list if you have one.',
  'Remember to eat dinner.',
  '8 hours of sleep is a must.',
  'Enjoy the night.',
  'Lugaw is a nice recipe for dinner.',
  'Enjoy your sleep.',
];

/// Shared by [DayPart.night] and [DayPart.midnight]. The two differ in how they
/// greet you, not in what they have to say.
const List<String> _lateMessages = <String>[
  'Sleep deficiency is bad for your health.',
  "Don't forget to stay hydrated.",
  'Sleep deficiency can break melatonin production.',
  'It is late right now.',
  'Sleeping late is not healthy.',
  'You need to sleep now.',
  "Don't forget to sleep, or to prepare your breakfast.",
  'Enjoy the quiet.',
  'Sleep deficiency affects your body clock.',
  'Please take care of yourself.',
];

/// The fallback pool, and also the pool a small fraction of picks are diverted
/// to at any hour, so a familiar time of day can still surprise you.
const List<String> _anyTimeMessages = <String>[
  'Make peace, not war.',
  "Oh hey, what's up?",
  'Focus on your tasks.',
  'How many screenshots do you take?',
  'Spread love, not havoc.',
  "We didn't start the fire!",
  'Time for some good music.',
  'Starting from the ground up.',
  'Do something nice today.',
  'How is everything going?',
  'No illusions, welcome to reality!',
  'Thank you for your support.',
  "What a lovely experience, isn't it?",
  "What's on your mind?",
  'Expecto Patronum.',
  'Wubba Lubba Dub Dub.',
  'Try to avoid UV rays around noon.',
  'Stay hydrated!',
  "Pringles aren't actually potato chips.",
  'Laughing can increase blood flow.',
  'Coffee contains antioxidants.',
  'Ketchup was once sold as medicine.',
  'Cotton candy was invented by a dentist.',
  "Don't skip your meal!",
  "Remember to check your device's battery level!",
  'Check your email and messages.',
  'Check your to-do list if you have one.',
  'Go get them!',
  'Time to buy some rice!',
  'Time for lunch!',
  'All white rice starts brown.',
  'Want some soda?',
  'Always bring water to stay hydrated.',
  'Space smells like seared steak.',
  'Enjoy your day!',
];

// ── Selection ───────────────────────────────────────────────────────────────

/// A greeting and its one liner, picked together so the pair is always
/// consistent with the same instant.
@immutable
class TimelyGreeting {
  const TimelyGreeting({
    required this.part,
    required this.greeting,
    required this.message,
  });

  final DayPart part;

  /// "Good evening." and friends. Always ends in punctuation.
  final String greeting;

  /// The one liner underneath it.
  final String message;

  @override
  bool operator ==(Object other) =>
      other is TimelyGreeting &&
      other.part == part &&
      other.greeting == greeting &&
      other.message == message;

  @override
  int get hashCode => Object.hash(part, greeting, message);
}

/// Classifies an instant. Local time, because a greeting that disagrees with
/// the wall clock is worse than no greeting.
DayPart dayPartAt(DateTime now) {
  final hour = now.hour;
  if (hour < 5) return DayPart.midnight;
  if (hour < 12) return DayPart.morning;
  if (hour < 17) return DayPart.afternoon;
  if (hour < 19) return DayPart.earlyEvening;
  if (hour < 22) return DayPart.evening;
  return DayPart.night;
}

String greetingFor(DayPart part) {
  switch (part) {
    case DayPart.midnight:
      return 'Solemn midnight.';
    case DayPart.morning:
      return 'Good morning.';
    case DayPart.afternoon:
      return 'Good afternoon.';
    case DayPart.earlyEvening:
    case DayPart.evening:
      return 'Good evening.';
    case DayPart.night:
      return 'Good night.';
  }
}

List<String> messagesFor(DayPart part) {
  switch (part) {
    case DayPart.midnight:
    case DayPart.night:
      return _lateMessages;
    case DayPart.morning:
      return _morningMessages;
    case DayPart.afternoon:
      return _afternoonMessages;
    case DayPart.earlyEvening:
      return _earlyEveningMessages;
    case DayPart.evening:
      return _eveningMessages;
  }
}

/// Every message the app can show, in no particular order. Exposed so a test
/// can hold the whole set to the same house rules as any other copy.
List<String> get allTimelyMessages => <String>[
  ..._morningMessages,
  ..._afternoonMessages,
  ..._earlyEveningMessages,
  ..._eveningMessages,
  ..._lateMessages,
  ..._anyTimeMessages,
];

/// One pick in this many is diverted to [_anyTimeMessages] regardless of the
/// hour. Rare enough to feel like a find rather than a rotation.
const int _wildcardOdds = 14;

/// Whether [message] says nothing [greeting] has not already said.
///
/// The source arrays were written to stand alone, so a few lines name the time
/// of day outright. Here the greeting is already on the line above, which turns
/// "Good evening." over "It is evening right now." into the masthead stating one
/// fact twice — it reads like a template nobody finished filling in. Detected
/// rather than deleted: the pools are the extracted arrays and are held to the
/// house rules as a whole set, so the line stays and simply never gets chosen
/// under the greeting it echoes. "It is late right now." survives, because
/// "Good night." does not already say it.
bool restatesGreeting(String message, String greeting) {
  const opener = 'it is ';
  const closer = ' right now.';
  final m = message.toLowerCase();
  if (!m.startsWith(opener) || !m.endsWith(closer)) return false;
  final subject = m.substring(opener.length, m.length - closer.length);
  return subject.isNotEmpty && greeting.toLowerCase().contains(subject);
}

/// Picks a greeting and a line for [now].
///
/// Pass [avoid] to discourage repeating a line that is already on screen: a tap
/// that changes nothing reads as a dead tap. Pass [random] to make a test
/// deterministic.
TimelyGreeting timelyGreeting(DateTime now, {Random? random, String? avoid}) {
  final rng = random ?? Random();
  final part = dayPartAt(now);
  final greeting = greetingFor(part);
  final source = rng.nextInt(_wildcardOdds) == 0
      ? _anyTimeMessages
      : messagesFor(part);
  final usable = source
      .where((m) => !restatesGreeting(m, greeting))
      .toList(growable: false);
  // No pool is one line of echo away from empty, but choosing from nothing would
  // throw, and a duplicated line beats a crashed masthead.
  final pool = usable.isEmpty ? source : usable;

  var message = pool[rng.nextInt(pool.length)];
  if (avoid != null && pool.length > 1) {
    // A handful of re-rolls, not a loop that could spin. Landing on the same
    // line twice in a row is a small annoyance, not a correctness problem, so
    // giving up after a few tries is the right trade.
    for (var i = 0; i < 4 && message == avoid; i++) {
      message = pool[rng.nextInt(pool.length)];
    }
  }

  return TimelyGreeting(
    part: part,
    greeting: greeting,
    message: message,
  );
}

/// The next instant the greeting should change. Lets a widget wait for the
/// exact moment instead of polling the clock.
DateTime nextDayPartBoundary(DateTime now) {
  for (final hour in dayPartBoundaries) {
    if (hour > now.hour) {
      return DateTime(now.year, now.month, now.day, hour);
    }
  }
  // Past the last boundary of the day, so the next one is tomorrow's midnight.
  // Built by day overflow rather than by adding 24 hours, which would land an
  // hour off on the days the clocks move.
  return DateTime(now.year, now.month, now.day + 1);
}

// ── Widget ──────────────────────────────────────────────────────────────────

/// Keeps a [TimelyGreeting] current and hands it to [builder].
///
/// Colour and type are left to the caller, because the app's palette helpers
/// live with the screens that use them and this file has no business knowing
/// about them.
///
/// The line is re-picked when the day part rolls over, when the app comes back
/// to the foreground, and on a tap.
class TimelyGreetingView extends StatefulWidget {
  const TimelyGreetingView({
    required this.builder,
    this.tapToChange = true,
    super.key,
  });

  final Widget Function(BuildContext context, TimelyGreeting greeting) builder;

  /// Whether tapping asks for a different line. On by default; it is the only
  /// way to see more than one of these in a sitting.
  final bool tapToChange;

  @override
  State<TimelyGreetingView> createState() => _TimelyGreetingViewState();
}

class _TimelyGreetingViewState extends State<TimelyGreetingView>
    with WidgetsBindingObserver {
  final Random _random = Random();
  late TimelyGreeting _current;
  Timer? _rollover;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _current = timelyGreeting(DateTime.now(), random: _random);
    _scheduleRollover();
  }

  @override
  void dispose() {
    _rollover?.cancel();
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    // A fresh line on every return to the app. Coming back is the moment the
    // header is actually being read, and the clock may have moved on a lot.
    if (state == AppLifecycleState.resumed) _repick();
  }

  /// Sleeps until the greeting is due to change rather than ticking, so nothing
  /// runs in between and an app left open overnight is not still wishing you a
  /// good evening at breakfast.
  void _scheduleRollover() {
    _rollover?.cancel();
    final now = DateTime.now();
    var wait = nextDayPartBoundary(now).difference(now);
    if (wait.isNegative) wait = const Duration(minutes: 1);
    _rollover = Timer(wait + const Duration(seconds: 1), _repick);
  }

  void _repick() {
    if (!mounted) return;
    setState(() {
      _current = timelyGreeting(
        DateTime.now(),
        random: _random,
        avoid: _current.message,
      );
    });
    _scheduleRollover();
  }

  @override
  Widget build(BuildContext context) {
    final view = widget.builder(context, _current);
    if (!widget.tapToChange) return view;
    return GestureDetector(
      behavior: HitTestBehavior.opaque,
      onTap: _repick,
      child: view,
    );
  }
}
