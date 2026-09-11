import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'hello_path.dart';

/// Geometry lifted off the reference screen (1080x2412 device pixels): the ink
/// box spans 0.4407 of the screen width, is centred on the full screen, and is
/// drawn with a stroke 0.0139 of the width — 15 device pixels on the reference.
/// The hint's text box sits at 0.9071 of the height, which puts its ink centre
/// at 0.9204 where the reference had it. Its type is 0.0377 of the width, which
/// is the 40.7 device pixels that reproduce the reference's 319-pixel-wide line;
/// everything here is a fraction of the width rather than a dp figure because the
/// reference device runs a 454 dpi override, not the 480 its panel reports. The
/// 0.25 letter spacing is what Material's bodyMedium would have supplied anyway,
/// spelled out so the hint does not drift with the ambient text style.
const double _kInkWidthFraction = 0.4407;
const double _kStrokeFraction = 0.0139;
const double _kHintTopFraction = 0.9071;
const double _kHintFontFraction = 0.0377;
const double _kHintLetterSpacing = 0.25;

/// The reference recording draws for 75 frames at 30fps. The recovered timing
/// curve is stretched 1.0444x to hold the pen under 1.7x its average speed —
/// without it the first video frame, which already carries 9% of the stroke,
/// would flash into existence.
///
/// The greeting then loops: the finished word is held, faded off, and written
/// again, so the screen is never still while it waits. One controller runs the
/// whole cycle — writing, holding, fading — because a single repeating timeline
/// cannot drift out of step with itself the way three chained ones can. The fade
/// lands on empty ink exactly as the next pass starts from nothing, so the seam
/// is invisible.
const int _kWriteMs = 2611;
const int _kHoldMs = 1200;
const int _kFadeMs = 400;
const int _kCycleMs = _kWriteMs + _kHoldMs + _kFadeMs;
const Duration _kCycleDuration = Duration(milliseconds: _kCycleMs);

/// Where the pen finishes and where the fade begins, as fractions of the cycle.
const double _kWriteEnd = _kWriteMs / _kCycleMs;
const double _kFadeStart = (_kWriteMs + _kHoldMs) / _kCycleMs;

/// The greeting: "hello" written out in one unbroken cursive stroke, then a
/// swipe up to get into the app. Pitch black, nothing else on screen.
class HelloScreen extends StatefulWidget {
  const HelloScreen({super.key, required this.onDismiss});

  /// Called once the user has swiped the greeting away.
  final VoidCallback onDismiss;

  @override
  State<HelloScreen> createState() => _HelloScreenState();
}

class _HelloScreenState extends State<HelloScreen>
    with TickerProviderStateMixin {
  late final AnimationController _cycle;
  late final AnimationController _hint;
  late final AnimationController _exit;

  /// Set once the user has touched the screen: the loop stops and the finished
  /// word stays put. A tap only settles the greeting — the swipe is still what
  /// opens the app, so the hint remains true after a tap.
  bool _frozen = false;
  bool _leaving = false;

  @override
  void initState() {
    super.initState();
    _hint = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 450),
    );
    _exit = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 320),
    );
    _cycle = AnimationController(vsync: this, duration: _kCycleDuration)
      ..addListener(_raiseHintOnce)
      ..repeat();
  }

  /// The hint appears the first time the pen finishes and then stays lit; it
  /// does not blink along with the rewriting.
  void _raiseHintOnce() {
    if (_cycle.value < _kWriteEnd) return;
    _cycle.removeListener(_raiseHintOnce);
    _hint.forward();
  }

  @override
  void dispose() {
    _cycle.dispose();
    _hint.dispose();
    _exit.dispose();
    super.dispose();
  }

  /// Stops the rewriting and shows the completed word. Any touch does this, so
  /// the greeting holds still the moment the user engages with it.
  void _freeze() {
    if (_frozen) return;
    _cycle.stop();
    _hint.forward();
    setState(() => _frozen = true);
  }

  /// How far up the greeting has been pushed, as a fraction of the throw
  /// distance. The exit controller is the only source of truth — a live drag
  /// writes straight into it, so releasing mid-swipe continues the same motion
  /// rather than snapping.
  double _throw(Size size) => size.height * 0.22;

  void _dragUpdate(DragUpdateDetails d, Size size) {
    if (_leaving) return;
    _exit.value = (_exit.value - d.primaryDelta! / _throw(size)).clamp(0.0, 1.0);
  }

  void _dragEnd(DragEndDetails d, Size size) {
    if (_leaving) return;
    final fling = d.primaryVelocity ?? 0;
    if (_exit.value > 0.32 || fling < -700) {
      _leave();
    } else {
      _exit.animateBack(0, curve: Curves.easeOutCubic);
    }
  }

  Future<void> _leave() async {
    if (_leaving) return;
    _leaving = true;
    HapticFeedback.lightImpact();
    await _exit.forward();
    if (mounted) widget.onDismiss();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFF000000),
      body: LayoutBuilder(
        builder: (context, constraints) {
          final size = constraints.biggest;
          return GestureDetector(
            behavior: HitTestBehavior.opaque,
            onTap: _freeze,
            onVerticalDragStart: (_) => _freeze(),
            onVerticalDragUpdate: (d) => _dragUpdate(d, size),
            onVerticalDragEnd: (d) => _dragEnd(d, size),
            child: AnimatedBuilder(
              animation: Listenable.merge(<Listenable>[_cycle, _hint, _exit]),
              builder: (context, _) {
                final lift = _exit.value * _throw(size);
                final t = _cycle.value;
                // Once frozen the word is simply finished: whichever phase the
                // loop was in, it snaps to full ink at full opacity.
                final double arc = _frozen
                    ? 1
                    : _arcAt((t / _kWriteEnd).clamp(0.0, 1.0));
                final double ink = _frozen || t < _kFadeStart
                    ? 1
                    : 1 - (t - _kFadeStart) / (1 - _kFadeStart);
                return Opacity(
                  opacity: (1 - _exit.value).clamp(0.0, 1.0),
                  child: Transform.translate(
                    offset: Offset(0, -lift),
                    child: Stack(
                      children: <Widget>[
                        Positioned.fill(
                          child: CustomPaint(
                            painter: _HelloPainter(arc: arc, alpha: ink),
                          ),
                        ),
                        Positioned(
                          top: size.height * _kHintTopFraction,
                          left: 0,
                          right: 0,
                          child: Opacity(
                            opacity: _hint.value,
                            child: Text(
                              'Swipe up to open',
                              textAlign: TextAlign.center,
                              style: TextStyle(
                                fontSize: size.width * _kHintFontFraction,
                                fontWeight: FontWeight.w400,
                                letterSpacing: _kHintLetterSpacing,
                                color: Colors.white.withValues(alpha: 0.9),
                              ),
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                );
              },
            ),
          );
        },
      ),
    );
  }
}

/// How much of the stroke has been laid down at time [t], read off the pen
/// rhythm recovered from the reference frames.
double _arcAt(double t) {
  if (t <= 0) return 0;
  if (t >= 1) return 1;
  final n = kHelloTiming.length ~/ 2;
  int lo = 0, hi = n - 1;
  while (lo + 1 < hi) {
    final mid = (lo + hi) >> 1;
    if (kHelloTiming[mid * 2] <= t) {
      lo = mid;
    } else {
      hi = mid;
    }
  }
  final t0 = kHelloTiming[lo * 2], t1 = kHelloTiming[hi * 2];
  final a0 = kHelloTiming[lo * 2 + 1], a1 = kHelloTiming[hi * 2 + 1];
  if (t1 <= t0) return a1;
  return a0 + (a1 - a0) * (t - t0) / (t1 - t0);
}

/// Draws the greeting into the box, revealing it up to [arc] of its length and
/// at [alpha] opacity — the fade at the end of each pass wipes the word off so
/// the next one can start from nothing.
/// The letterform is held as a unit-width path and scaled by the canvas, so the
/// stroke scales with it and the arc fractions stay resolution independent.
class _HelloPainter extends CustomPainter {
  const _HelloPainter({required this.arc, required this.alpha});

  final double arc;
  final double alpha;

  static final Path _unit = _buildUnitPath();
  static final double _unitLength = _unit
      .computeMetrics()
      .fold<double>(0, (sum, m) => sum + m.length);

  static Path _buildUnitPath() {
    final path = Path();
    final n = kHelloPoints.length ~/ 2;
    path.moveTo(kHelloPoints[0], kHelloPoints[1]);
    // Quadratics through the midpoints: the samples are ~1.25 reference pixels
    // apart, so this only takes the faceting off the curves.
    for (int i = 1; i < n - 1; i++) {
      final cx = kHelloPoints[i * 2], cy = kHelloPoints[i * 2 + 1];
      final mx = (cx + kHelloPoints[(i + 1) * 2]) / 2;
      final my = (cy + kHelloPoints[(i + 1) * 2 + 1]) / 2;
      path.quadraticBezierTo(cx, cy, mx, my);
    }
    path.lineTo(kHelloPoints[(n - 1) * 2], kHelloPoints[(n - 1) * 2 + 1]);
    return path;
  }

  @override
  bool shouldRepaint(_HelloPainter old) =>
      old.arc != arc || old.alpha != alpha;

  @override
  void paint(Canvas canvas, Size size) {
    if (arc <= 0 || alpha <= 0) return;
    final inkWidth = size.width * _kInkWidthFraction;
    final stroke = size.width * _kStrokeFraction;
    // The ink box is the centreline box grown by half a stroke all round, so
    // centring the centreline box centres the ink.
    final centreWidth = inkWidth - stroke;
    final centreHeight = centreWidth * kHelloHeight;

    canvas.save();
    canvas.translate(
      (size.width - centreWidth) / 2,
      (size.height - centreHeight) / 2,
    );
    canvas.scale(centreWidth);

    final paint = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = stroke / centreWidth
      ..strokeCap = StrokeCap.round
      ..strokeJoin = StrokeJoin.round
      ..color = const Color(0xFFFFFFFF).withValues(alpha: alpha.clamp(0.0, 1.0))
      ..isAntiAlias = true;

    if (arc >= 1) {
      canvas.drawPath(_unit, paint);
    } else {
      final metric = _unit.computeMetrics().first;
      canvas.drawPath(metric.extractPath(0, _unitLength * arc), paint);
    }
    canvas.restore();
  }
}
