import 'dart:math' as math;
import 'package:flutter/material.dart';

import 'design_tokens.dart';
import 'glass_panel.dart';

/// The ambient layer every screen sits on: a soft wash, three slow-drifting
/// orbs in the accent's hue family, a vignette, and a fixed grain field.
///
/// The orbs read as light sources rather than decoration, which is what lets
/// the glass surfaces above them have something to refract. Grain is what
/// stops the whole thing from looking like flat vector fill.
class GlassBackdrop extends StatefulWidget {
  const GlassBackdrop({super.key});
  @override
  State<GlassBackdrop> createState() => _GlassBackdropState();
}

class _GlassBackdropState extends State<GlassBackdrop>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;
  bool _calm = false;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: const Duration(seconds: 34),
    );
    appGlassMode.addListener(_syncAnimation);
    appAccentColor.addListener(_onAccent);
    _syncAnimation();
  }

  void _onAccent() {
    if (mounted) setState(() {});
  }

  void _syncAnimation() {
    if (appGlassMode.value && !_calm) {
      if (!_controller.isAnimating) _controller.repeat();
    } else {
      _controller.stop();
    }
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final calm = reduceMotion(context);
    if (calm != _calm) {
      _calm = calm;
      _syncAnimation();
    }
  }

  @override
  void dispose() {
    appGlassMode.removeListener(_syncAnimation);
    appAccentColor.removeListener(_onAccent);
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final dark = theme.brightness == Brightness.dark;
    final amoled = dark && appAmoledMode.value;
    final scheme = theme.colorScheme;
    final glass = appGlassMode.value && !reduceTransparency(context);

    if (!glass) {
      // Material mode, or an accessibility request for less transparency: a
      // clean static surface. This is the documented behaviour, not a
      // degraded one.
      return IgnorePointer(
        child: RepaintBoundary(
          child: ColoredBox(
            color: amoled
                ? const Color(0xFF000000)
                : dark
                    ? DarkSurface.canvas
                    : LightSurface.canvas,
          ),
        ),
      );
    }

    final accent = appAccentColor.value;
    final companion = accentCompanion(accent);

    return IgnorePointer(
      child: RepaintBoundary(
        child: Stack(
          children: [
            // Base wash. One neutral family per theme: cool-neutral on dark,
            // warm off-white on light. Never both.
            Positioned.fill(
              child: DecoratedBox(
                decoration: BoxDecoration(
                  gradient: amoled
                      ? null
                      : LinearGradient(
                          begin: Alignment.topLeft,
                          end: Alignment.bottomRight,
                          colors: dark
                              ? const [
                                  Color(0xFF0D0D10),
                                  DarkSurface.canvas,
                                  Color(0xFF08080A),
                                ]
                              : const [
                                  Color(0xFFFAF8F5),
                                  LightSurface.canvas,
                                  Color(0xFFF2EFEA),
                                ],
                        ),
                  color: amoled ? const Color(0xFF000000) : null,
                ),
              ),
            ),
            // Drifting orbs. Accent and its companion only, so the ambience
            // stays monochromatic however the accent is set.
            Positioned.fill(
              child: RepaintBoundary(
                child: AnimatedBuilder(
                  animation: _controller,
                  builder: (context, _) {
                    final t = _controller.value;
                    return Stack(
                      children: [
                        _Orb(
                          t: t,
                          alignment: const Alignment(-1.1, -0.9),
                          color: accent.withValues(
                            alpha: amoled ? 0.11 : (dark ? 0.26 : 0.17),
                          ),
                          radius: 300,
                        ),
                        _Orb(
                          t: t,
                          phase: 0.5,
                          alignment: const Alignment(0.95, -0.35),
                          color: companion.withValues(
                            alpha: amoled ? 0.09 : (dark ? 0.20 : 0.13),
                          ),
                          radius: 250,
                        ),
                        _Orb(
                          t: t,
                          phase: 0.8,
                          alignment: const Alignment(-0.3, 1.0),
                          color: scheme.secondary.withValues(
                            alpha: amoled ? 0.08 : (dark ? 0.16 : 0.11),
                          ),
                          radius: 270,
                        ),
                      ],
                    );
                  },
                ),
              ),
            ),
            // Vignette, pulling focus to the middle of the page.
            Positioned.fill(
              child: DecoratedBox(
                decoration: BoxDecoration(
                  gradient: RadialGradient(
                    center: Alignment.center,
                    radius: 1.2,
                    colors: [
                      Colors.transparent,
                      Colors.black.withValues(alpha: dark ? 0.26 : 0.045),
                    ],
                    stops: const [0.58, 1.0],
                  ),
                ),
              ),
            ),
            // Grain. Seeded, so it is identical every frame and never crawls.
            Positioned.fill(
              child: RepaintBoundary(
                child: CustomPaint(
                  painter: _GrainPainter(
                    opacity: dark ? 0.030 : 0.022,
                    dark: dark,
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _Orb extends StatelessWidget {
  const _Orb({
    required this.t,
    required this.alignment,
    required this.color,
    required this.radius,
    this.phase = 0,
  });
  final double t;
  final double phase;
  final Alignment alignment;
  final Color color;
  final double radius;

  @override
  Widget build(BuildContext context) {
    const twoPi = 6.283185307179586;
    final drift = twoPi * t;
    final dx = alignment.x +
        0.15 *
            (0.6 + 0.4 * math.sin(drift + phase)) *
            (0.5 + 0.5 * math.cos(drift * 1.3 + phase));
    final dy = alignment.y +
        0.12 *
            (0.6 + 0.4 * math.cos(drift + phase * 1.7)) *
            (0.5 + 0.5 * math.sin(drift * 1.1 + phase));
    return Align(
      alignment: Alignment(dx, dy),
      child: IgnorePointer(
        child: Container(
          width: radius,
          height: radius,
          decoration: BoxDecoration(
            shape: BoxShape.circle,
            gradient: RadialGradient(
              colors: [
                color,
                color.withValues(alpha: color.a * 0.6),
                color.withValues(alpha: 0),
              ],
              stops: const [0.0, 0.45, 1.0],
            ),
          ),
        ),
      ),
    );
  }
}

/// A fixed field of single-pixel specks. Cheap because the seed is constant,
/// the layer has its own [RepaintBoundary], and nothing above it invalidates.
class _GrainPainter extends CustomPainter {
  const _GrainPainter({required this.opacity, required this.dark});

  final double opacity;
  final bool dark;

  @override
  void paint(Canvas canvas, Size size) {
    if (size.isEmpty) return;
    final rng = math.Random(0x5EED);
    final paint = Paint()
      ..color = (dark ? Colors.white : Colors.black).withValues(alpha: opacity);
    // Density tuned by area so a tablet is not visibly cleaner than a phone.
    final count = (size.width * size.height / 620).clamp(200, 3200).toInt();
    for (var i = 0; i < count; i++) {
      final x = rng.nextDouble() * size.width;
      final y = rng.nextDouble() * size.height;
      canvas.drawRect(Rect.fromLTWH(x, y, 1, 1), paint);
    }
  }

  @override
  bool shouldRepaint(_GrainPainter old) =>
      old.opacity != opacity || old.dark != dark;
}
