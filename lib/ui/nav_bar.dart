import 'dart:math' as math;
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'design_tokens.dart';
import 'glass_panel.dart';

/// The fade a page's content gets as it slides into the band the navigation
/// capsule floats in.
///
/// Apple calls this a scroll edge effect, and the point is that it is *not* the
/// bar's own material. The capsule here ships perfectly clear — no tint, no
/// frost, by request — and a clear capsule has a problem the sliders cannot
/// solve: a long settings list prints its body copy straight through five tab
/// labels, two layers of type in the same pixels, and neither one wins. Raising
/// the labels' contrast helps them and does nothing for the sentence they are
/// sitting on top of. So the page yields instead, exactly where the bar lives.
///
/// The height is deliberately [navBarClearance] and not a number of its own:
/// that is the room every page already reserves at its bottom, so anything a
/// page has correctly kept clear of the capsule — a composer, a floating action,
/// the last row of a list — is above the gradient's zero stop and cannot be
/// dimmed by it. Only content that scrolls into the reserved band fades, which
/// is the only content that was ever a problem.
///
/// Decorative and never hit-testable.
class NavBarScrollEdge extends StatelessWidget {
  const NavBarScrollEdge({super.key});

  @override
  Widget build(BuildContext context) {
    final canvas = Theme.of(context).scaffoldBackgroundColor;
    return IgnorePointer(
      child: SizedBox(
        height: navBarClearance(context),
        child: DecoratedBox(
          decoration: BoxDecoration(
            gradient: LinearGradient(
              begin: Alignment.topCenter,
              end: Alignment.bottomCenter,
              // Slow to start, so the fade has no visible edge of its own, then
              // most of the way gone by the time it reaches the glyphs. It never
              // quite reaches the canvas: a hint of what is behind the glass is
              // the whole reason the bar is made of glass.
              colors: <Color>[
                canvas.withValues(alpha: 0),
                canvas.withValues(alpha: 0.10),
                canvas.withValues(alpha: 0.52),
                canvas.withValues(alpha: 0.88),
                canvas.withValues(alpha: 0.96),
              ],
              stops: const <double>[0, 0.10, 0.30, 0.60, 1],
            ),
          ),
        ),
      ),
    );
  }
}

/// The floating Liquid Glass tab bar.
///
/// Modelled on the iOS 26 tab bar: an inset capsule that hovers over the
/// content rather than a full-width strip welded to the bottom edge. Glass
/// belongs to the navigation layer only, so this widget and the sheets are the
/// only places in the app that sample the backdrop.
///
/// ## What "Liquid Glass" means here
///
/// Apple's material is a private GPU pipeline: it *bends* light through a
/// curved edge (lensing) instead of merely scattering it (blur). Flutter
/// exposes no displacement filter, so the look is reconstructed from parts that
/// do exist, and the result is an approximation rather than a port:
///
/// * **Body** — a real [BackdropFilter] blur, alpha-filled.
/// * **Lens rim** — a ring band that re-samples the backdrop at a *lower* blur
///   with boosted saturation and brightness. Sharper and brighter at the edge
///   against a softer centre is the visual signature of light concentrating
///   through a curve, and it is position-independent, unlike a matrix
///   displacement, which would drift with the widget's screen offset.
/// * **Chromatic split** — two hue-shifted arcs offset across the rim, the
///   colour fringe a real lens leaves behind.
/// * **Specular** — a catch of light along the top edge, and a shimmer that
///   sweeps once when the selection changes.
/// * **Illumination** — the touch point lights the glass from underneath.
///
/// Glass never samples other glass, so the active indicator is a fill, not a
/// second backdrop layer.
class LiquidGlassNavBar extends StatefulWidget {
  const LiquidGlassNavBar({
    required this.selectedIndex,
    required this.onDestinationSelected,
    required this.destinations,
    this.enabled = true,
    this.position = 16,
    this.opacity = 0,
    this.blurSigma = 0,
    this.barSize = 116,
    this.barWidth = 89,
    this.cornerRoundness = 100,
    this.refractionDepth = 100,
    this.refractionStrength = 100,
    this.chromaticSplit = 0,
    this.swipeSensitivity = 5.0,
    this.invertSwipe = true,
    this.holdToSwipe = true,
    this.useIOSGlassMode = true,
    super.key,
  });

  final int selectedIndex;
  final ValueChanged<int> onDestinationSelected;
  final List<LiquidNavDestination> destinations;

  /// Governs the *glass* treatment only. The bar itself never disappears.
  final bool enabled;

  /// Gap between the capsule and the bottom safe-area edge, 0–40.
  final double position;

  /// Glass fill strength, 0–100.
  final double opacity;

  /// Backdrop blur sigma, 0–40. Zero is legal: it yields a clear capsule that
  /// tints without frosting, the `.clear` variant of the material.
  final double blurSigma;

  /// Capsule height driver, 80–160.
  final double barSize;

  /// Capsule width as a percentage of the available width, 50–100.
  final double barWidth;

  /// 0 keeps square corners, 100 gives a full capsule.
  final double cornerRoundness;

  /// Thickness of the lens band at the rim, 0–100.
  final double refractionDepth;

  /// How hard the lens band brightens and sharpens, 0–100.
  final double refractionStrength;

  /// Offset between the warm and cool rim samples, 0–100.
  final double chromaticSplit;

  final double swipeSensitivity; // 1–10
  final bool invertSwipe;
  final bool holdToSwipe;

  /// `true` runs the lensed engine (layered backdrop sampling, Android 13+).
  /// `false` runs the frosted engine: one blur pass and painted rim highlights,
  /// which is what older GPUs can hold at 60fps.
  final bool useIOSGlassMode;

  @override
  State<LiquidGlassNavBar> createState() => _LiquidGlassNavBarState();
}

class _LiquidGlassNavBarState extends State<LiquidGlassNavBar>
    with TickerProviderStateMixin {
  // Swipe-to-switch state.
  double _dragOffset = 0;
  bool _isDragging = false;
  DateTime _lastHaptic = DateTime.fromMillisecondsSinceEpoch(0);

  // Interactive state: which slot is held down, and where the finger is.
  int? _pressedIndex;
  Offset? _touchPoint;

  /// Sweeps once whenever the selection changes. This is the shimmer the real
  /// material shows when it reacts to a touch.
  late final AnimationController _shimmer = AnimationController(
    vsync: this,
    duration: Motion.shimmer,
  );

  /// Drives the indicator's settle. Runs 0 -> 1 on every selection change so
  /// the capsule can overshoot and come back rather than easing flatly in.
  late final AnimationController _settle = AnimationController(
    vsync: this,
    duration: Motion.slow,
    value: 1,
  );

  /// Fades the touch-point illumination in and out.
  late final AnimationController _glow = AnimationController(
    vsync: this,
    duration: Motion.base,
  );

  /// Materialization: the bar builds itself out of the background on first
  /// frame instead of being pasted on top of it.
  late final AnimationController _materialize = AnimationController(
    vsync: this,
    duration: Motion.reveal,
  );

  @override
  void initState() {
    super.initState();
    _materialize.forward();
  }

  @override
  void didUpdateWidget(LiquidGlassNavBar old) {
    super.didUpdateWidget(old);
    if (old.selectedIndex != widget.selectedIndex) {
      _settle
        ..reset()
        ..forward();
      _shimmer
        ..reset()
        ..forward();
    }
  }

  @override
  void dispose() {
    _shimmer.dispose();
    _settle.dispose();
    _glow.dispose();
    _materialize.dispose();
    super.dispose();
  }

  void _triggerHaptic() {
    final now = DateTime.now();
    if (now.difference(_lastHaptic).inMilliseconds > 40) {
      _lastHaptic = now;
      HapticFeedback.selectionClick();
    }
  }

  // ── Geometry ──────────────────────────────────────────────────────────────

  double get _barHeight => 52.0 + (widget.barSize - 80) / 80 * 28.0;

  /// Corner radius. At roundness 100 the radius equals half the height, which
  /// is a true capsule; below that it relaxes toward a squircle-ish rectangle
  /// but never below the control radius, so the shape stays in the family.
  double _radius(double height) {
    final t = (widget.cornerRoundness / 100).clamp(0.0, 1.0);
    return Radii.control + (height / 2 - Radii.control) * t;
  }

  /// Lens band thickness in logical pixels.
  double get _rimBand => (widget.refractionDepth / 100).clamp(0.0, 1.0) * 16.0;

  double get _refract => (widget.refractionStrength / 100).clamp(0.0, 1.0);

  double get _aberration => (widget.chromaticSplit / 100).clamp(0.0, 1.0) * 3.2;

  /// True when the capsule's body draws nothing of its own — no tint, no frost —
  /// so the icons sit directly on whatever the page has scrolled underneath.
  ///
  /// This is the shipped default rather than an edge case: clear glass is what
  /// the app opens with. The lens rim still describes the shape, but between the
  /// rims there is only page, and a 56% grey label loses that fight outright the
  /// moment a line of body text passes behind it — two layers of type in the
  /// same pixels, neither readable. The row answers by carrying its own
  /// contrast, which is what keeps the capsule free of the tint it was asked not
  /// to have.
  bool get _clearBody => widget.opacity <= 0 && widget.blurSigma <= 0;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final dark = scheme.brightness == Brightness.dark;
    final amoled = dark && appAmoledMode.value;
    final calm = reduceMotion(context);
    final opaque = reduceTransparency(context);

    // `enabled` is the per-bar glass switch; appGlassMode is the app-wide
    // Material/Liquid Glass theme. Accessibility settings win over both.
    final glass = widget.enabled && appGlassMode.value && !opaque;

    final count = widget.destinations.length;
    if (count == 0) return const SizedBox.shrink();

    final height = _barHeight;
    final radius = _radius(height);
    // The gap below the capsule doubles as the runway for the entry slide, so
    // the bar always animates inside its own box. This is not cosmetic: a
    // widget that paints outside its bounds is a widget that cannot be
    // touched there, and the bar would spend its first frames untappable.
    final gap = Space.md + widget.position;
    final rise = math.min(20.0, gap);

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: Space.md),
      child: SizedBox(
        height: height + gap,
        child: LayoutBuilder(
          builder: (context, constraints) {
            final available = constraints.maxWidth;
            final barW = (available * (widget.barWidth / 100).clamp(0.5, 1.0))
                .clamp(math.min(220.0, available), available)
                .toDouble();
            final slotWidth = barW / count;

            final bar = SizedBox(
              width: barW,
              height: height,
              child: _buildCapsule(
                context: context,
                scheme: scheme,
                dark: dark,
                amoled: amoled,
                glass: glass,
                calm: calm,
                radius: radius,
                height: height,
                barW: barW,
                slotWidth: slotWidth,
                count: count,
              ),
            );

            // Top aligned, with the runway underneath: the capsule rests
            // exactly `gap` above the bottom of the box either way.
            return Align(
              alignment: Alignment.topCenter,
              child: calm
                  ? bar
                  : AnimatedBuilder(
                      animation: _materialize,
                      builder: (context, child) {
                        final t = Curves.easeOutCubic.transform(
                          _materialize.value,
                        );
                        return Opacity(
                          opacity: t,
                          child: Transform.translate(
                            offset: Offset(0, (1 - t) * rise),
                            child: Transform.scale(
                              scale: 0.94 + 0.06 * t,
                              child: child,
                            ),
                          ),
                        );
                      },
                      child: bar,
                    ),
            );
          },
        ),
      ),
    );
  }

  // ── The capsule ───────────────────────────────────────────────────────────

  Widget _buildCapsule({
    required BuildContext context,
    required ColorScheme scheme,
    required bool dark,
    required bool amoled,
    required bool glass,
    required bool calm,
    required double radius,
    required double height,
    required double barW,
    required double slotWidth,
    required int count,
  }) {
    final rr = BorderRadius.circular(radius);
    final indicatorLeft = (widget.selectedIndex * slotWidth - _dragOffset).clamp(
      0.0,
      (count - 1) * slotWidth,
    );

    return GestureDetector(
      behavior: HitTestBehavior.opaque,
      onHorizontalDragStart: widget.holdToSwipe
          ? (_) => setState(() => _isDragging = true)
          : null,
      onHorizontalDragUpdate: widget.holdToSwipe
          ? (d) {
              final dir = widget.invertSwipe ? -1.0 : 1.0;
              final next = _dragOffset + d.delta.dx * dir;
              setState(() {
                _dragOffset = next.clamp(
                  (widget.selectedIndex - count + 1) * slotWidth,
                  widget.selectedIndex * slotWidth,
                );
              });
            }
          : null,
      onHorizontalDragEnd: widget.holdToSwipe
          ? (d) => _settleDrag(d, slotWidth, count)
          : null,
      onHorizontalDragCancel: widget.holdToSwipe
          ? () => setState(() {
              _isDragging = false;
              _dragOffset = 0;
            })
          : null,
      child: DecoratedBox(
        // Contact shadow. Tinted toward the canvas rather than pure black, so
        // the capsule looks like it is resting on the page, not floating in a
        // void with a grey smear under it.
        decoration: BoxDecoration(
          borderRadius: rr,
          boxShadow: [
            BoxShadow(
              color: dark
                  ? Colors.black.withValues(alpha: amoled ? 0.66 : 0.52)
                  : scheme.shadow.withValues(alpha: 0.14),
              blurRadius: 26,
              spreadRadius: -4,
              offset: const Offset(0, 10),
            ),
          ],
        ),
        child: Stack(
          children: [
            Positioned.fill(
              child: IgnorePointer(
                child: ClipRRect(
                  borderRadius: rr,
                  child: glass
                      ? _buildGlassBody(scheme, dark, amoled, radius, height)
                      : _buildSolidBody(scheme, dark, amoled),
                ),
              ),
            ),
            _buildIndicator(
              scheme: scheme,
              dark: dark,
              glass: glass,
              calm: calm,
              left: indicatorLeft,
              slotWidth: slotWidth,
              height: height,
              radius: radius,
            ),
            Positioned.fill(
              child: _buildIconRow(scheme, dark, glass, calm, count, height),
            ),
            // Rim hairline last, so the icons cannot paint over the edge.
            Positioned.fill(
              child: IgnorePointer(
                child: _buildRim(scheme, dark, amoled, glass, rr),
              ),
            ),
          ],
        ),
      ),
    );
  }

  void _settleDrag(DragEndDetails d, double slotWidth, int count) {
    final dir = widget.invertSwipe ? -1.0 : 1.0;
    final velocity = (d.primaryVelocity ?? 0) * dir;

    // Sensitivity moves the commit threshold rather than scaling the drag, so
    // the indicator always tracks the finger 1:1 and only the decision to
    // commit gets easier or harder.
    final sens = widget.swipeSensitivity.clamp(1.0, 10.0);
    final threshold = slotWidth * (0.62 - 0.045 * (sens - 1));
    final flingCutoff = 320.0 - 18.0 * (sens - 1);

    var target = widget.selectedIndex;
    if (velocity.abs() > flingCutoff) {
      target += velocity < 0 ? 1 : -1;
    } else if (_dragOffset.abs() > threshold) {
      target += (-_dragOffset / slotWidth).round();
    }
    target = target.clamp(0, count - 1);

    setState(() {
      _isDragging = false;
      _dragOffset = 0;
    });
    if (target != widget.selectedIndex) {
      _triggerHaptic();
      widget.onDestinationSelected(target);
    }
  }

  // ── Glass body ────────────────────────────────────────────────────────────

  /// Boosts saturation and brightness by [k]. The lens band uses this on its
  /// backdrop sample: light passing through a curved edge arrives denser and
  /// more colourful than light passing through the flat centre.
  ui.ColorFilter _lensBoost(double k) {
    const lumR = 0.2126, lumG = 0.7152, lumB = 0.0722;
    final s = 1 + 0.85 * k;
    final b = 14.0 * k;
    final inv = 1 - s;
    return ui.ColorFilter.matrix(<double>[
      inv * lumR + s, inv * lumG, inv * lumB, 0, b,
      inv * lumR, inv * lumG + s, inv * lumB, 0, b,
      inv * lumR, inv * lumG, inv * lumB + s, 0, b,
      0, 0, 0, 1, 0,
    ]);
  }

  Widget _buildGlassBody(
    ColorScheme scheme,
    bool dark,
    bool amoled,
    double radius,
    double height,
  ) {
    final op = (widget.opacity / 100).clamp(0.0, 1.0);
    final sigma = widget.blurSigma.clamp(0.0, 40.0);
    final baseFill = dark
        ? (amoled ? Glass.fillDarkAmoled : Glass.fillDark)
        : Glass.fillLight;

    // The body tint. Dark themes lean on white at low alpha; light themes need
    // an actual surface colour underneath or text loses the backdrop fight.
    final tintTop = dark
        ? Colors.white.withValues(alpha: (baseFill + 0.06) * op)
        : Colors.white.withValues(alpha: (baseFill + 0.16).clamp(0.0, 1.0) * op);
    final tintBottom = dark
        ? (amoled ? const Color(0xFF0C0C0E) : scheme.surfaceContainer)
              .withValues(alpha: (baseFill + 0.30) * op)
        : scheme.surfaceContainerLowest.withValues(alpha: (baseFill + 0.24) * op);

    final body = DecoratedBox(
      decoration: BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: [tintTop, tintBottom],
        ),
      ),
    );

    return Stack(
      fit: StackFit.expand,
      children: [
        // 1. Body: one blur pass over the whole capsule. At sigma 0 the filter
        //    is skipped rather than run with a zero radius — a BackdropFilter
        //    costs a saveLayer of the whole capsule every frame whether or not
        //    it changes a pixel, and `.clear` glass is a shipped default.
        if (sigma > 0)
          BackdropFilter(
            filter: ui.ImageFilter.blur(
              sigmaX: sigma,
              sigmaY: sigma,
              tileMode: ui.TileMode.clamp,
            ),
            child: body,
          )
        else
          body,
        // 2. Lens rim and chromatic split, lensed engine only.
        if (widget.useIOSGlassMode && _refract > 0 && _rimBand > 0)
          ..._buildLensRim(dark, radius, sigma),
        // 3. Specular: the catch of light along the top edge.
        _buildSpecular(dark, height),
        // 4. Touch-point illumination.
        _buildTouchGlow(scheme, dark),
        // 5. Shimmer sweep on selection change.
        _buildShimmer(dark),
      ],
    );
  }

  /// The lens band: a ring at the rim that re-samples the backdrop at a much
  /// lower blur with boosted saturation, plus the colour fringe a real lens
  /// leaves at its edge.
  List<Widget> _buildLensRim(bool dark, double radius, double sigma) {
    final band = _rimBand;
    final rimSigma = sigma * 0.32;
    return [
      ClipPath(
        clipper: _RingClipper(radius: radius, band: band),
        child: BackdropFilter(
          filter: ui.ImageFilter.compose(
            outer: _lensBoost(_refract),
            inner: ui.ImageFilter.blur(
              sigmaX: rimSigma,
              sigmaY: rimSigma,
              tileMode: ui.TileMode.clamp,
            ),
          ),
          child: const SizedBox.expand(),
        ),
      ),
      if (_aberration > 0)
        Positioned.fill(
          child: CustomPaint(
            painter: _ChromaticRimPainter(
              radius: radius,
              split: _aberration,
              strength: _refract,
              dark: dark,
            ),
          ),
        ),
    ];
  }

  /// Top-edge specular catch plus a soft darkening at the bottom, which is what
  /// reads as thickness. A flat translucent panel has no thickness.
  Widget _buildSpecular(bool dark, double height) {
    final top = dark ? Glass.specularDark : Glass.specularLight;
    return Positioned.fill(
      child: DecoratedBox(
        decoration: BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topCenter,
            end: Alignment.bottomCenter,
            colors: [
              Colors.white.withValues(alpha: top),
              Colors.white.withValues(alpha: top * 0.12),
              Colors.transparent,
              Colors.black.withValues(alpha: dark ? 0.16 : 0.05),
            ],
            stops: const [0.0, 0.06, 0.55, 1.0],
          ),
        ),
      ),
    );
  }

  /// Light radiating from wherever the finger is. On the real material this is
  /// how a press announces itself before anything moves.
  Widget _buildTouchGlow(ColorScheme scheme, bool dark) {
    return Positioned.fill(
      child: AnimatedBuilder(
        animation: _glow,
        builder: (context, _) {
          final point = _touchPoint;
          if (point == null || _glow.value == 0) {
            return const SizedBox.shrink();
          }
          return LayoutBuilder(
            builder: (context, c) {
              final fx = (point.dx / c.maxWidth).clamp(0.0, 1.0);
              return DecoratedBox(
                decoration: BoxDecoration(
                  gradient: RadialGradient(
                    center: Alignment(fx * 2 - 1, 0.2),
                    radius: 0.55,
                    colors: [
                      scheme.primary.withValues(
                        alpha: (dark ? 0.26 : 0.20) * _glow.value,
                      ),
                      scheme.primary.withValues(alpha: 0),
                    ],
                  ),
                ),
              );
            },
          );
        },
      ),
    );
  }

  /// A single diagonal sweep across the capsule when the selection changes.
  Widget _buildShimmer(bool dark) {
    return Positioned.fill(
      child: AnimatedBuilder(
        animation: _shimmer,
        builder: (context, _) {
          if (_shimmer.value == 0 || _shimmer.value == 1) {
            return const SizedBox.shrink();
          }
          final t = Curves.easeInOutSine.transform(_shimmer.value);
          final alpha = math.sin(math.pi * _shimmer.value) * (dark ? 0.16 : 0.30);
          return DecoratedBox(
            decoration: BoxDecoration(
              gradient: LinearGradient(
                begin: Alignment(-2.4 + t * 4.4, -1),
                end: Alignment(-1.4 + t * 4.4, 1),
                colors: [
                  Colors.white.withValues(alpha: 0),
                  Colors.white.withValues(alpha: alpha),
                  Colors.white.withValues(alpha: 0),
                ],
                stops: const [0.0, 0.5, 1.0],
              ),
            ),
          );
        },
      ),
    );
  }

  /// Material mode, or any time the platform asks for reduced transparency: an
  /// opaque themed surface. Not a downgrade, just the other half of the switch.
  Widget _buildSolidBody(ColorScheme scheme, bool dark, bool amoled) {
    return ColoredBox(
      color: dark
          ? (amoled ? const Color(0xFF0A0A0C) : scheme.surfaceContainerHigh)
          : scheme.surfaceContainerLow,
    );
  }

  /// The hairline. Brighter along the top arc than the bottom, because a real
  /// edge only catches light on the side facing the light.
  Widget _buildRim(
    ColorScheme scheme,
    bool dark,
    bool amoled,
    bool glass,
    BorderRadius rr,
  ) {
    if (!glass) {
      return DecoratedBox(
        decoration: BoxDecoration(
          borderRadius: rr,
          border: Border.all(
            color: scheme.outlineVariant.withValues(alpha: dark ? 0.5 : 0.9),
            width: 1,
          ),
        ),
      );
    }
    final rim = dark ? Glass.rimDark : Glass.rimLight;
    return CustomPaint(
      painter: _RimPainter(
        radius: rr.topLeft.x,
        topAlpha: rim + (amoled ? 0.06 : 0.0),
        bottomAlpha: rim * 0.35,
        dark: dark,
      ),
    );
  }

  // ── Active indicator ──────────────────────────────────────────────────────

  /// The selected slot. A fill, never a second [BackdropFilter]: glass does not
  /// sample glass, and stacking blur passes is what makes the effect turn to
  /// mud. It squishes horizontally while travelling and springs back on arrival,
  /// which is the closest honest equivalent of the material's morph.
  Widget _buildIndicator({
    required ColorScheme scheme,
    required bool dark,
    required bool glass,
    required bool calm,
    required double left,
    required double slotWidth,
    required double height,
    required double radius,
  }) {
    final inset = height > 60 ? 7.0 : 5.0;
    final pill = Radii.core(radius, inset);
    return AnimatedPositioned(
      duration: _isDragging
          ? Duration.zero
          : (calm ? Motion.fast : Motion.base),
      curve: calm ? kFadeCurve : kBounceCurve,
      left: left + inset,
      top: inset,
      bottom: inset,
      width: slotWidth - inset * 2,
      child: IgnorePointer(
        child: AnimatedBuilder(
          animation: _settle,
          builder: (context, child) {
            if (calm || _isDragging) return child!;
            // Squish out on departure, back to square on arrival.
            final squish = math.sin(math.pi * _settle.value);
            return Transform.scale(
              scaleX: 1 + 0.10 * squish,
              scaleY: 1 - 0.06 * squish,
              child: child,
            );
          },
          child: DecoratedBox(
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(pill),
              gradient: LinearGradient(
                begin: Alignment.topCenter,
                end: Alignment.bottomCenter,
                colors: glass
                    ? [
                        Colors.white.withValues(alpha: dark ? 0.20 : 0.86),
                        Colors.white.withValues(alpha: dark ? 0.09 : 0.62),
                      ]
                    : [
                        scheme.primary.withValues(alpha: dark ? 0.26 : 0.20),
                        scheme.primary.withValues(alpha: dark ? 0.16 : 0.12),
                      ],
              ),
              border: Border.all(
                color: glass
                    ? Colors.white.withValues(alpha: dark ? 0.22 : 0.9)
                    : scheme.primary.withValues(alpha: 0.42),
                width: 0.8,
              ),
            ),
          ),
        ),
      ),
    );
  }

  // ── Icons ─────────────────────────────────────────────────────────────────

  Widget _buildIconRow(
    ColorScheme scheme,
    bool dark,
    bool glass,
    bool calm,
    int count,
    double height,
  ) {
    final activeColor = legibleAccent(
      scheme.primary,
      dark ? DarkSurface.high : LightSurface.base,
      minRatio: 3,
    );
    // A clear capsule leaves the row sitting on the page itself, so the row
    // lifts itself: heavier idle ink, plus a soft halo in the canvas colour that
    // holds a gap open around every glyph. On glass that reads as depth; over a
    // tint or a frost it would read as a smudge, so both are dropped the moment
    // the capsule has a surface of its own — including Material mode and the
    // reduce-transparency path, where `glass` is already false.
    final lift = glass && _clearBody;
    final idleColor = scheme.onSurface.withValues(
      alpha: lift ? (dark ? 0.78 : 0.74) : (dark ? 0.56 : 0.52),
    );
    final canvas = dark ? Colors.black : Colors.white;
    final halo = lift
        ? <Shadow>[
            // Tight and opaque to carve the gap, wide and faint to fade it out.
            // One shadow alone either rings the glyph or does nothing.
            Shadow(
              color: canvas.withValues(alpha: dark ? 0.72 : 0.86),
              blurRadius: 5,
            ),
            Shadow(
              color: canvas.withValues(alpha: dark ? 0.44 : 0.56),
              blurRadius: 12,
            ),
          ]
        : const <Shadow>[];
    final compact = height < 58;

    return Row(
      children: [
        for (var i = 0; i < count; i++)
          Expanded(
            child: _NavSlot(
              destination: widget.destinations[i],
              active: i == widget.selectedIndex,
              pressed: _pressedIndex == i,
              calm: calm,
              compact: compact,
              activeColor: activeColor,
              idleColor: idleColor,
              halo: halo,
              onPressStart: (localX) {
                setState(() {
                  _pressedIndex = i;
                  _touchPoint = Offset(localX, 0);
                });
                if (!calm) _glow.forward();
              },
              onPressEnd: () {
                setState(() => _pressedIndex = null);
                _glow.reverse();
              },
              onTap: () {
                if (i == widget.selectedIndex) return;
                _triggerHaptic();
                widget.onDestinationSelected(i);
              },
            ),
          ),
      ],
    );
  }
}

/// One tappable slot. Split out so the press scale rebuilds a single slot
/// instead of the whole bar.
class _NavSlot extends StatelessWidget {
  const _NavSlot({
    required this.destination,
    required this.active,
    required this.pressed,
    required this.calm,
    required this.compact,
    required this.activeColor,
    required this.idleColor,
    required this.halo,
    required this.onPressStart,
    required this.onPressEnd,
    required this.onTap,
  });

  final LiquidNavDestination destination;
  final bool active;
  final bool pressed;
  final bool calm;
  final bool compact;
  final Color activeColor;
  final Color idleColor;

  /// Drawn behind the glyph and the label when the capsule itself draws nothing.
  /// Empty otherwise, which costs nothing.
  final List<Shadow> halo;
  final ValueChanged<double> onPressStart;
  final VoidCallback onPressEnd;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final color = active ? activeColor : idleColor;
    return Semantics(
      button: true,
      selected: active,
      label: destination.label,
      child: GestureDetector(
        behavior: HitTestBehavior.opaque,
        onTapDown: (d) => onPressStart(d.localPosition.dx),
        onTapUp: (_) => onPressEnd(),
        onTapCancel: onPressEnd,
        onTap: onTap,
        child: AnimatedScale(
          // Presses register in the surface itself, not just in a colour change.
          scale: pressed ? 0.90 : 1.0,
          duration: calm ? Duration.zero : Motion.fast,
          curve: kBounceCurve,
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              AnimatedScale(
                scale: active ? 1.10 : 1.0,
                duration: calm ? Duration.zero : Motion.base,
                curve: kBounceCurve,
                child: Icon(
                  active ? destination.selectedIcon : destination.icon,
                  size: compact ? 21 : 23,
                  color: color,
                  shadows: halo,
                ),
              ),
              SizedBox(height: compact ? 2 : 3),
              Text(
                destination.label,
                maxLines: 1,
                overflow: TextOverflow.clip,
                style: TextStyle(
                  color: color,
                  fontSize: compact ? 9.5 : 10,
                  height: 1.05,
                  letterSpacing: 0.1,
                  fontWeight: active ? FontWeight.w600 : FontWeight.w500,
                  shadows: halo,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Clips to the band between the outer edge and an inset rounded rect, which
/// is where a curved glass edge would concentrate light.
class _RingClipper extends CustomClipper<Path> {
  const _RingClipper({required this.radius, required this.band});

  final double radius;
  final double band;

  @override
  Path getClip(Size size) {
    final outer = Path()..addRRect(
      RRect.fromRectAndRadius(
        Offset.zero & size,
        Radius.circular(radius),
      ),
    );
    final innerRect = Rect.fromLTWH(
      band,
      band,
      math.max(0, size.width - band * 2),
      math.max(0, size.height - band * 2),
    );
    final inner = Path()..addRRect(
      RRect.fromRectAndRadius(
        innerRect,
        Radius.circular(Radii.core(radius, band)),
      ),
    );
    return Path.combine(PathOperation.difference, outer, inner);
  }

  @override
  bool shouldReclip(_RingClipper old) =>
      old.radius != radius || old.band != band;
}

/// The colour fringe. Two hue-shifted strokes offset across the rim: warm
/// pushed one way, cool the other, exactly as a lens splits white light.
class _ChromaticRimPainter extends CustomPainter {
  const _ChromaticRimPainter({
    required this.radius,
    required this.split,
    required this.strength,
    required this.dark,
  });

  final double radius;
  final double split;
  final double strength;
  final bool dark;

  @override
  void paint(Canvas canvas, Size size) {
    final rrect = RRect.fromRectAndRadius(
      Offset.zero & size,
      Radius.circular(radius),
    );
    final alpha = (dark ? 0.34 : 0.26) * strength;
    // Screen blending keeps the fringe additive, so it tints the edge instead
    // of painting a coloured line on top of it.
    final warm = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1.2
      ..blendMode = BlendMode.screen
      ..color = Glass.aberrationWarm.withValues(alpha: alpha);
    final cool = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1.2
      ..blendMode = BlendMode.screen
      ..color = Glass.aberrationCool.withValues(alpha: alpha);

    canvas.save();
    canvas.translate(-split * 0.5, -split * 0.35);
    canvas.drawRRect(rrect.deflate(0.6), cool);
    canvas.restore();

    canvas.save();
    canvas.translate(split * 0.5, split * 0.35);
    canvas.drawRRect(rrect.deflate(0.6), warm);
    canvas.restore();
  }

  @override
  bool shouldRepaint(_ChromaticRimPainter old) =>
      old.radius != radius ||
      old.split != split ||
      old.strength != strength ||
      old.dark != dark;
}

/// The hairline, drawn with a vertical alpha ramp so the top arc catches more
/// light than the bottom. A single flat 1px border reads as a drawn outline;
/// this reads as an edge.
class _RimPainter extends CustomPainter {
  const _RimPainter({
    required this.radius,
    required this.topAlpha,
    required this.bottomAlpha,
    required this.dark,
  });

  final double radius;
  final double topAlpha;
  final double bottomAlpha;
  final bool dark;

  @override
  void paint(Canvas canvas, Size size) {
    final rect = Offset.zero & size;
    final rrect = RRect.fromRectAndRadius(
      rect.deflate(0.5),
      Radius.circular(radius),
    );
    final paint = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1
      ..shader = ui.Gradient.linear(
        Offset(size.width / 2, 0),
        Offset(size.width / 2, size.height),
        [
          Colors.white.withValues(alpha: topAlpha),
          Colors.white.withValues(alpha: topAlpha * 0.45),
          Colors.white.withValues(alpha: bottomAlpha),
        ],
        const [0.0, 0.45, 1.0],
      );
    canvas.drawRRect(rrect, paint);

    if (dark) {
      // A whisper of shade just inside the bottom edge gives the plate a
      // measurable thickness instead of leaving it infinitely thin.
      canvas.drawRRect(
        rrect.deflate(1),
        Paint()
          ..style = PaintingStyle.stroke
          ..strokeWidth = 1
          ..color = Colors.black.withValues(alpha: 0.18),
      );
    }
  }

  @override
  bool shouldRepaint(_RimPainter old) =>
      old.radius != radius ||
      old.topAlpha != topAlpha ||
      old.bottomAlpha != bottomAlpha ||
      old.dark != dark;
}

/// One destination in the bar.
class LiquidNavDestination {
  const LiquidNavDestination({
    required this.icon,
    required this.selectedIcon,
    required this.label,
  });

  final IconData icon;
  final IconData selectedIcon;
  final String label;
}









