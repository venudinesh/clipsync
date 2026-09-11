import 'dart:ui' show PathMetric;

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../ui/design_tokens.dart' show reduceMotion;

/// The first-run tutorial: four pages, each one a hand-drawn mark, a headline
/// and a short explanation, on the same pitch black the greeting uses so the
/// swipe out of the greeting lands somewhere continuous.
///
/// The marks are drawn as ink — one white stroke, round caps, revealed along its
/// length the way the greeting writes itself. That is the only thing the pages
/// share with a stock onboarding card: no tinted glyph tile, no colour per step,
/// no dot row, and the accent is confined to the progress rule and the final
/// action, so the type carries the page. Pages advance on a swipe as well as a
/// tap, since the greeting has just taught the swipe.

/// Page geometry. The headline is the largest thing on screen and everything
/// else is a hairline — that ratio is what keeps the layout from reading as a
/// card floating in the middle of the display.
const double _kMargin = 28;
const double _kMarkWidth = 132;
const double _kMarkStroke = 2.6;
const double _kTitleSize = 34;
const double _kBodySize = 15.5;
const double _kActionSize = 56;

/// The body's leading, and the number of lines its box always reserves. Every
/// headline is a single line and every body runs to three, so reserving the
/// three keeps the mark, the headline and the first line of body at exactly the
/// same height on all four pages — nothing shifts under a sideways swipe, which
/// is the tell that the pages were laid out rather than merely filled.
const double _kBodyLead = 1.62;
const int _kBodyLines = 3;
const double _kBodyBox = _kBodySize * _kBodyLead * _kBodyLines;

/// Marks are authored in a 100 x 70 box and scaled to whatever the page gives
/// them, so the stroke stays even across the four of them.
const double _kMarkBox = 100;
const double _kMarkAspect = 0.7;

/// One page of the tutorial.
class _Step {
  const _Step({required this.mark, required this.title, required this.body});

  final _Mark mark;
  final String title;
  final String body;
}

/// Which mark heads a page.
enum _Mark { copy, tidy, capture, done }

const List<_Step> _kSteps = <_Step>[
  _Step(
    mark: _Mark.copy,
    title: 'Copy anything',
    body:
        'Whatever you copy lands here on its own, from a chat, a web page or '
        'a document. There is nothing to open first.',
  ),
  _Step(
    mark: _Mark.tidy,
    title: 'The AI tidies it',
    body:
        'A llama model running on this phone reformats raw copies into clean '
        'text. No cloud, no account, nothing leaves the device.',
  ),
  _Step(
    mark: _Mark.capture,
    title: 'Talk, or snap it',
    body:
        'Dictate a note or photograph a page and it comes back as text, '
        'transcribed here rather than on a server.',
  ),
  _Step(
    mark: _Mark.done,
    title: "You're set",
    body:
        'Start a chat, search everything you have ever copied, or switch the '
        'clipboard monitor off from the home screen.',
  ),
];

/// In-app tutorial shown on first install (or reset).
class OnboardingFlow extends StatefulWidget {
  final VoidCallback onComplete;
  const OnboardingFlow({super.key, required this.onComplete});

  @override
  State<OnboardingFlow> createState() => _OnboardingFlowState();
}

class _OnboardingFlowState extends State<OnboardingFlow> {
  final PageController _pages = PageController();
  int _page = 0;

  @override
  void dispose() {
    _pages.dispose();
    super.dispose();
  }

  void _advance() {
    HapticFeedback.selectionClick();
    if (_page < _kSteps.length - 1) {
      if (reduceMotion(context)) {
        _pages.jumpToPage(_page + 1);
      } else {
        _pages.nextPage(
          duration: const Duration(milliseconds: 420),
          curve: Curves.easeOutCubic,
        );
      }
    } else {
      widget.onComplete();
    }
  }

  void _skip() {
    HapticFeedback.lightImpact();
    widget.onComplete();
  }

  @override
  Widget build(BuildContext context) {
    final Color accent = Theme.of(context).colorScheme.primary;
    final bool finish = _page == _kSteps.length - 1;

    return Scaffold(
      backgroundColor: const Color(0xFF000000),
      body: SafeArea(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: <Widget>[
            _TopBar(page: _page, total: _kSteps.length, onSkip: _skip),
            Padding(
              padding: const EdgeInsets.fromLTRB(_kMargin, 20, _kMargin, 0),
              child: _ProgressRule(
                pages: _pages,
                count: _kSteps.length,
                settled: _page,
                accent: accent,
              ),
            ),
            Expanded(
              child: PageView.builder(
                controller: _pages,
                itemCount: _kSteps.length,
                onPageChanged: (int i) => setState(() => _page = i),
                itemBuilder: (BuildContext context, int i) =>
                    _StepPage(step: _kSteps[i], active: i == _page),
              ),
            ),
            _ActionRow(
              label: finish ? 'Get started' : 'Next',
              finish: finish,
              accent: accent,
              onPressed: _advance,
            ),
          ],
        ),
      ),
    );
  }
}

/// The way out, and nothing else. Where you are is already stated by the
/// progress rule immediately below this, so a zero-padded `01 / 04` set in wide
/// tracking beside it would be the same fact twice, in the loudest type on the
/// page. On the last page Skip has nothing left to skip, so it fades off rather
/// than sitting there doing the same job as the action.
class _TopBar extends StatelessWidget {
  const _TopBar({
    required this.page,
    required this.total,
    required this.onSkip,
  });

  final int page;
  final int total;
  final VoidCallback onSkip;

  @override
  Widget build(BuildContext context) {
    final bool skippable = page < total - 1;
    return Padding(
      padding: const EdgeInsets.fromLTRB(_kMargin, 12, _kMargin - 12, 0),
      child: Row(
        children: <Widget>[
          const Spacer(),
          AnimatedOpacity(
            opacity: skippable ? 1 : 0,
            duration: const Duration(milliseconds: 240),
            curve: Curves.easeOut,
            child: IgnorePointer(
              ignoring: !skippable,
              child: TextButton(
                onPressed: onSkip,
                style: TextButton.styleFrom(
                  minimumSize: const Size(48, 48),
                  padding: const EdgeInsets.symmetric(horizontal: 12),
                  foregroundColor: Colors.white,
                ),
                child: Text(
                  'Skip',
                  style: TextStyle(
                    fontSize: 13.5,
                    fontWeight: FontWeight.w600,
                    letterSpacing: 0.2,
                    color: Colors.white.withValues(alpha: 0.6),
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// Four hairline segments instead of a dot row. It reads off the scroll
/// position rather than the settled page, so the next segment fills under the
/// finger as the page is dragged and the rule is never out of step with what
/// the eye is doing.
class _ProgressRule extends StatelessWidget {
  const _ProgressRule({
    required this.pages,
    required this.count,
    required this.settled,
    required this.accent,
  });

  final PageController pages;
  final int count;
  final int settled;
  final Color accent;

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: pages,
      builder: (BuildContext context, Widget? _) {
        final double at = pages.hasClients
            ? (pages.page ?? settled.toDouble())
            : settled.toDouble();
        return Row(
          children: List<Widget>.generate(count, (int i) {
            final double fill = (at - i + 1).clamp(0.0, 1.0);
            return Expanded(
              child: Padding(
                padding: EdgeInsets.only(right: i == count - 1 ? 0 : 6),
                child: SizedBox(
                  height: 2,
                  child: Stack(
                    children: <Widget>[
                      Positioned.fill(
                        child: DecoratedBox(
                          decoration: BoxDecoration(
                            color: Colors.white.withValues(alpha: 0.14),
                            borderRadius: BorderRadius.circular(1),
                          ),
                        ),
                      ),
                      Positioned.fill(
                        child: FractionallySizedBox(
                          alignment: Alignment.centerLeft,
                          widthFactor: fill,
                          child: DecoratedBox(
                            decoration: BoxDecoration(
                              color: accent,
                              borderRadius: BorderRadius.circular(1),
                            ),
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            );
          }),
        );
      },
    );
  }
}

/// A page: the mark inks itself on, then the headline and the body rise into
/// place behind it. Everything is left aligned off one margin, which is what
/// gives the page an axis to read down instead of a centred stack.
class _StepPage extends StatefulWidget {
  const _StepPage({required this.step, required this.active});

  final _Step step;
  final bool active;

  @override
  State<_StepPage> createState() => _StepPageState();
}

class _StepPageState extends State<_StepPage>
    with SingleTickerProviderStateMixin {
  late final AnimationController _entry = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 820),
  );

  static const Interval _kMarkIn = Interval(0, 0.88, curve: Curves.easeOutCubic);
  static const Interval _kTitleIn = Interval(
    0.12,
    0.62,
    curve: Curves.easeOutCubic,
  );
  static const Interval _kBodyIn = Interval(
    0.26,
    0.82,
    curve: Curves.easeOutCubic,
  );

  /// The entrance starts as soon as the page is mounted, but while the greeting
  /// is still on top the tutorial sits under a muted `TickerMode`, so a page
  /// mounted behind it holds at nothing and only inks itself on once the
  /// greeting has been swiped off.
  ///
  /// Under Reduce Motion the mark is already drawn when the page arrives. The
  /// ink pass is the page's one flourish, and a flourish is exactly what that
  /// setting is asking not to see.
  bool _calm = false;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    _calm = reduceMotion(context);
    if (_calm) {
      _entry.value = 1;
    } else if (widget.active && _entry.value == 0) {
      _entry.forward();
    }
  }

  @override
  void didUpdateWidget(_StepPage old) {
    super.didUpdateWidget(old);
    if (_calm) {
      _entry.value = 1;
      return;
    }
    // Coming back to a page draws it again rather than showing a stale one.
    if (widget.active && !old.active) _entry.forward(from: 0);
  }

  @override
  void dispose() {
    _entry.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: _entry,
      builder: (BuildContext context, Widget? _) {
        final double t = _entry.value;
        return Padding(
          padding: const EdgeInsets.symmetric(horizontal: _kMargin),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              SizedBox(
                width: _kMarkWidth,
                height: _kMarkWidth * _kMarkAspect,
                child: CustomPaint(
                  painter: _MarkPainter(
                    mark: widget.step.mark,
                    progress: _kMarkIn.transform(t),
                  ),
                ),
              ),
              const SizedBox(height: 46),
              _Rise(
                t: _kTitleIn.transform(t),
                dy: 16,
                child: Text(
                  widget.step.title,
                  style: const TextStyle(
                    fontSize: _kTitleSize,
                    fontWeight: FontWeight.w700,
                    height: 1.08,
                    letterSpacing: -1.1,
                    color: Colors.white,
                  ),
                ),
              ),
              const SizedBox(height: 18),
              _Rise(
                t: _kBodyIn.transform(t),
                dy: 12,
                child: ConstrainedBox(
                  constraints: const BoxConstraints(
                    maxWidth: 330,
                    minHeight: _kBodyBox,
                  ),
                  child: Text(
                    widget.step.body,
                    style: TextStyle(
                      fontSize: _kBodySize,
                      height: _kBodyLead,
                      letterSpacing: 0.1,
                      color: Colors.white.withValues(alpha: 0.55),
                    ),
                  ),
                ),
              ),
            ],
          ),
        );
      },
    );
  }
}

/// Fades a line in while lifting it the last few pixels into place.
class _Rise extends StatelessWidget {
  const _Rise({required this.t, required this.dy, required this.child});

  final double t;
  final double dy;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Opacity(
      opacity: t.clamp(0.0, 1.0),
      child: Transform.translate(offset: Offset(0, dy * (1 - t)), child: child),
    );
  }
}

/// Inks a mark on: one continuous pass across its strokes, in the order they
/// were authored, so it looks drawn rather than faded up.
class _MarkPainter extends CustomPainter {
  const _MarkPainter({required this.mark, required this.progress});

  final _Mark mark;
  final double progress;

  static final Map<_Mark, Path> _cache = <_Mark, Path>{
    for (final _Mark m in _Mark.values) m: _build(m),
  };

  @override
  bool shouldRepaint(_MarkPainter old) =>
      old.progress != progress || old.mark != mark;

  @override
  void paint(Canvas canvas, Size size) {
    if (progress <= 0) return;
    final Path path = _cache[mark]!;
    final double scale = size.width / _kMarkBox;
    canvas.save();
    canvas.scale(scale);
    final Paint paint = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = _kMarkStroke / scale
      ..strokeCap = StrokeCap.round
      ..strokeJoin = StrokeJoin.round
      ..color = const Color(0xFFFFFFFF)
      ..isAntiAlias = true;
    canvas.drawPath(progress >= 1 ? path : _upTo(path, progress), paint);
    canvas.restore();
  }

  /// The first [progress] of the mark's total stroke length, strokes consumed
  /// in order.
  static Path _upTo(Path path, double progress) {
    final List<PathMetric> metrics = path.computeMetrics().toList();
    final double total = metrics.fold<double>(0, (double s, m) => s + m.length);
    double budget = total * progress;
    final Path out = Path();
    for (final PathMetric m in metrics) {
      if (budget <= 0) break;
      out.addPath(
        m.extractPath(0, budget < m.length ? budget : m.length),
        Offset.zero,
      );
      budget -= m.length;
    }
    return out;
  }

  static Path _build(_Mark mark) => switch (mark) {
    _Mark.copy => _copy(),
    _Mark.tidy => _tidy(),
    _Mark.capture => _capture(),
    _Mark.done => _done(),
  };

  /// A card in front of a card, the back one cut off where the front covers it.
  static Path _copy() {
    final Path p = Path();
    p.moveTo(24, 44);
    p.lineTo(24, 13);
    p.quadraticBezierTo(24, 7, 30, 7);
    p.lineTo(78, 7);
    _card(p, 32, 19, 92, 63, 9);
    p.moveTo(44, 32);
    p.lineTo(80, 32);
    p.moveTo(44, 42);
    p.lineTo(80, 42);
    p.moveTo(44, 52);
    p.lineTo(68, 52);
    return p;
  }

  /// Three scribbles, an arrow, three ruled lines.
  static Path _tidy() {
    final Path p = Path();
    for (final double y in const <double>[15, 35, 55]) {
      _scribble(p, 6, 40, y);
    }
    p.moveTo(46, 35);
    p.lineTo(59, 35);
    p.moveTo(54.5, 30.5);
    p.lineTo(59, 35);
    p.lineTo(54.5, 39.5);
    p.moveTo(66, 15);
    p.lineTo(94, 15);
    p.moveTo(66, 35);
    p.lineTo(94, 35);
    p.moveTo(66, 55);
    p.lineTo(84, 55);
    return p;
  }

  /// A voice trace, then a viewfinder drawn corner by corner.
  static Path _capture() {
    final Path p = Path();
    const List<double> reach = <double>[7, 15, 23, 11, 19, 9];
    for (int i = 0; i < reach.length; i++) {
      final double x = 7 + i * 8.4;
      p.moveTo(x, 35 - reach[i]);
      p.lineTo(x, 35 + reach[i]);
    }
    p.moveTo(63, 27);
    p.lineTo(63, 17);
    p.lineTo(73, 17);
    p.moveTo(86, 17);
    p.lineTo(96, 17);
    p.lineTo(96, 27);
    p.moveTo(96, 43);
    p.lineTo(96, 53);
    p.lineTo(86, 53);
    p.moveTo(73, 53);
    p.lineTo(63, 53);
    p.lineTo(63, 43);
    p.addOval(Rect.fromCircle(center: const Offset(79.5, 35), radius: 6));
    return p;
  }

  /// One sweep, the way the greeting is one sweep.
  static Path _done() {
    final Path p = Path();
    p.moveTo(15, 34);
    p.quadraticBezierTo(25, 43, 35, 55);
    p.quadraticBezierTo(57, 28, 88, 9);
    return p;
  }

  /// A rounded rectangle built from corner quadratics rather than an arc, so it
  /// inks on at the same rate as everything drawn beside it.
  static void _card(
    Path p,
    double l,
    double t,
    double r,
    double b,
    double c,
  ) {
    p.moveTo(l + c, t);
    p.lineTo(r - c, t);
    p.quadraticBezierTo(r, t, r, t + c);
    p.lineTo(r, b - c);
    p.quadraticBezierTo(r, b, r - c, b);
    p.lineTo(l + c, b);
    p.quadraticBezierTo(l, b, l, b - c);
    p.lineTo(l, t + c);
    p.quadraticBezierTo(l, t, l + c, t);
    p.close();
  }

  /// The wave that stands in for a raw, unformatted copy.
  static void _scribble(Path p, double x0, double x1, double y) {
    const int humps = 6;
    final double w = (x1 - x0) / humps;
    p.moveTo(x0, y);
    for (int i = 0; i < humps; i++) {
      p.quadraticBezierTo(
        x0 + w * i + w / 2,
        y + (i.isEven ? -5 : 5),
        x0 + w * (i + 1),
        y,
      );
    }
  }
}

/// The advance: a label and a ring, right aligned, one tap target. A ring
/// rather than a filled bar — the bar is the thing every tutorial has, and at
/// this size the ring still clears the 48dp target.
class _ActionRow extends StatelessWidget {
  const _ActionRow({
    required this.label,
    required this.finish,
    required this.accent,
    required this.onPressed,
  });

  final String label;
  final bool finish;
  final Color accent;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    final ColorScheme scheme = Theme.of(context).colorScheme;
    return Padding(
      padding: const EdgeInsets.fromLTRB(_kMargin, 22, _kMargin, 34),
      child: Row(
        children: <Widget>[
          const Spacer(),
          InkWell(
            onTap: onPressed,
            borderRadius: BorderRadius.circular(_kActionSize / 2),
            child: Padding(
              padding: const EdgeInsets.only(left: 18),
              child: Row(
                children: <Widget>[
                  Text(
                    label,
                    style: TextStyle(
                      fontSize: 15.5,
                      fontWeight: FontWeight.w600,
                      letterSpacing: 0.2,
                      color: Colors.white.withValues(alpha: 0.9),
                    ),
                  ),
                  const SizedBox(width: 18),
                  Container(
                    width: _kActionSize,
                    height: _kActionSize,
                    decoration: BoxDecoration(
                      shape: BoxShape.circle,
                      color: finish ? accent : Colors.transparent,
                      border: Border.all(
                        color: finish
                            ? accent
                            : Colors.white.withValues(alpha: 0.22),
                      ),
                    ),
                    child: Icon(
                      finish ? Icons.check_rounded : Icons.arrow_forward_rounded,
                      size: 20,
                      color: finish
                          ? scheme.onPrimary
                          : Colors.white.withValues(alpha: 0.9),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}
