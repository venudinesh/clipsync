import 'package:flutter/material.dart';

/// Design tokens for ClipSyncAI.
///
/// Single source of truth for colour, shape, spacing and motion. Every visual
/// decision in the app resolves through here, which is what keeps the palette,
/// the shape language and the easing locked together: one accent, one radius
/// rule, one set of curves.

// ─────────────────────────────────────────────────────────────────────────────
// MOTION
// ─────────────────────────────────────────────────────────────────────────────

/// The house curve. Leaves heavy, glides to a stop. Anything that moves a
/// surface uses this: sheets, the nav indicator, panel reveals.
const Curve kGlassCurve = Cubic(0.32, 0.72, 0, 1);

/// Slight overshoot for elements that physically "land" — the nav indicator
/// settling into a slot, a button springing back after a press.
const Curve kBounceCurve = Cubic(0.34, 1.42, 0.64, 1);

/// Long, weighty entry for content arriving on screen.
const Curve kEntryCurve = Cubic(0.16, 1, 0.3, 1);

/// Colour and opacity only. Never used for movement.
const Curve kFadeCurve = Cubic(0.4, 0, 0.2, 1);

class Motion {
  const Motion._();
  static const Duration instant = Duration(milliseconds: 90);
  static const Duration fast = Duration(milliseconds: 180);
  static const Duration base = Duration(milliseconds: 280);
  static const Duration slow = Duration(milliseconds: 420);
  static const Duration reveal = Duration(milliseconds: 640);
  static const Duration shimmer = Duration(milliseconds: 900);
}

// ─────────────────────────────────────────────────────────────────────────────
// SHAPE — one rule, applied everywhere
// ─────────────────────────────────────────────────────────────────────────────

/// Radii step down as elements nest. When one surface sits inside another,
/// derive the inner value with [Radii.core] so the curves stay parallel; that
/// concentricity is what makes nested panels read as machined rather than
/// merely stacked.
class Radii {
  const Radii._();

  static const double shell = 28; // outermost container of a group
  static const double card = 22; // standard content surface
  static const double inner = 16; // a surface inside a card
  static const double control = 12; // buttons, inputs, chips
  static const double tight = 8; // badges, swatches, dots
  static const double pill = 999; // capsules

  /// Inner radius that stays concentric with an [outer] radius across a
  /// bezel of [inset] logical pixels.
  static double core(double outer, double inset) =>
      (outer - inset).clamp(0, double.infinity).toDouble();
}

/// Vertical and horizontal rhythm. Sections breathe at [Space.section]; a
/// dense data row is the only place [Space.xs] belongs.
class Space {
  const Space._();
  static const double xs = 4;
  static const double sm = 8;
  static const double md = 12;
  static const double lg = 16;
  static const double xl = 24;
  static const double xxl = 32;
  static const double section = 40;
  static const double gutter = 18; // page edge inset

  /// Room a scrollable has to leave at its bottom so the last item clears the
  /// floating navigation capsule rather than hiding behind it. Roughly the
  /// bar's own height plus a breath.
  static const double navBar = 110;

  /// Bottom inset for a floating snackbar, so a toast lands above the capsule
  /// instead of across it.
  ///
  /// A floating `SnackBar` is wrapped in `SafeArea(top: false, bottom: false)`
  /// by the framework, so — unlike [navBarClearance] — it is never handed the
  /// system inset and has to carry the gesture bar in this number itself.
  static const double snackBar = navBar + xl + xs;
}

/// Whether the soft keyboard is up, handed down from the app shell.
///
/// A page cannot work this out for itself: `Scaffold` strips
/// `viewInsets.bottom` out of the `MediaQuery` it gives its body — that is what
/// `resizeToAvoidBottomInset` does — so a read from inside a page is always
/// zero. The shell measures it above the `Scaffold` and passes it down here.
class KeyboardVisibility extends InheritedWidget {
  const KeyboardVisibility({
    required this.visible,
    required super.child,
    super.key,
  });

  final bool visible;

  static bool of(BuildContext context) =>
      context
          .dependOnInheritedWidgetOfExactType<KeyboardVisibility>()
          ?.visible ??
      false;

  @override
  bool updateShouldNotify(KeyboardVisibility oldWidget) =>
      oldWidget.visible != visible;
}

/// Bottom padding for a scrollable on a page that runs edge to edge.
///
/// Every page owns the full screen and pads itself, which is what lets content
/// scroll *under* the status bar and the glass capsule instead of stopping at a
/// hard edge. The capsule floats inside the system inset, so that inset is
/// added to [Space.navBar] rather than replacing it.
double navBarClearance(BuildContext context, {double extra = 0}) {
  // With the keyboard up the capsule has already slid off screen, so reserving
  // its height would only strand a composer in a band of dead space above the
  // keys. Collapse to a breath and let the keyboard be the floor.
  if (KeyboardVisibility.of(context)) return Space.md + extra;
  return MediaQuery.of(context).padding.bottom + Space.navBar + extra;
}

/// Margin for a floating snackbar raised from a page inside the app shell.
///
/// Mirrors [navBarClearance] for toasts. The idle value clears the capsule; with
/// the keyboard up the capsule has gone and `Scaffold` already lifts the toast
/// above the keys, so holding that clearance back would only shove the toast
/// further across the page it is reporting on.
EdgeInsets snackBarMargin(BuildContext context) => EdgeInsets.fromLTRB(
      Space.md,
      Space.sm,
      Space.md,
      KeyboardVisibility.of(context) ? Space.md : Space.snackBar,
    );


// ─────────────────────────────────────────────────────────────────────────────
// PALETTE
// ─────────────────────────────────────────────────────────────────────────────

/// A selectable accent. Every entry is deliberately under 80% saturation, so
/// nothing in the palette can shout.
class AccentSwatch {
  const AccentSwatch(this.name, this.color, this.companion);

  /// Display name, sentence case.
  final String name;

  /// The single accent. One at a time, app-wide.
  final Color color;

  /// A near neighbour used only for slow ambient gradients, never for text or
  /// icons. Sharing a hue family with [color] keeps gradients monochromatic.
  final Color companion;
}

/// The soft accent set. Muted, low-saturation, closer to pigment than to LED.
/// Rose quartz is the default because it is the softest of the group and reads
/// well on both off-black and off-white.
const List<AccentSwatch> kAccents = [
  AccentSwatch('Rose quartz', Color(0xFFDFA5B4), Color(0xFFC98FA8)),
  AccentSwatch('Sage', Color(0xFFA9C1A8), Color(0xFF8FAF9B)),
  AccentSwatch('Mist blue', Color(0xFFA6BDD1), Color(0xFF8FA8C4)),
  AccentSwatch('Sand', Color(0xFFD9BE9B), Color(0xFFC7A886)),
  AccentSwatch('Clay', Color(0xFFD2A48F), Color(0xFFBC8E7C)),
  AccentSwatch('Lilac', Color(0xFFBCAFD6), Color(0xFFA79BC4)),
  AccentSwatch('Butter', Color(0xFFDFCB96), Color(0xFFCBB47F)),
  AccentSwatch('Slate', Color(0xFFB4BCC4), Color(0xFF9BA5AE)),
];

/// Default accent, used on a fresh install and by "reset".
const Color kDefaultAccent = Color(0xFFDFA5B4);

/// Derives the ambient companion for an arbitrary accent, so a custom colour
/// still produces a monochromatic gradient rather than a second hue.
Color accentCompanion(Color accent) {
  for (final s in kAccents) {
    if (s.color.toARGB32() == accent.toARGB32()) return s.companion;
  }
  final hsl = HSLColor.fromColor(accent);
  return hsl
      .withHue((hsl.hue - 14) % 360)
      .withSaturation((hsl.saturation * 0.88).clamp(0.0, 0.78))
      .withLightness((hsl.lightness * 0.90).clamp(0.0, 1.0))
      .toColor();
}

// ── Surfaces ────────────────────────────────────────────────────────────────

/// Dark surfaces. Off-black rather than `#000000`: pure black flattens depth,
/// because a shadow cast onto it is invisible. True black stays reachable, but
/// only through the AMOLED toggle, where losing that depth is the point.
class DarkSurface {
  const DarkSurface._();
  static const Color canvas = Color(0xFF0B0B0D);
  static const Color low = Color(0xFF101013);
  static const Color base = Color(0xFF16161A);
  static const Color high = Color(0xFF1D1D22);
  static const Color highest = Color(0xFF25252B);
  static const Color onSurface = Color(0xFFEDECEA); // off-white, faintly warm
  static const Color outline = Color(0xFF33333A);
}

/// Light surfaces. Warm off-white; pure `#ffffff` is reserved for specular
/// highlights, never for a page background.
class LightSurface {
  const LightSurface._();
  static const Color canvas = Color(0xFFF7F5F2);
  static const Color low = Color(0xFFFCFBF9);
  static const Color base = Color(0xFFFFFEFC);
  static const Color high = Color(0xFFF1EEE9);
  static const Color highest = Color(0xFFE8E4DE);
  static const Color onSurface = Color(0xFF1A1A1C); // off-black
  static const Color outline = Color(0xFFD8D3CB);
}

/// Semantic colours, desaturated to sit beside the soft accents without
/// hijacking attention. Danger still reads as danger; it just stops screaming.
class Semantic {
  const Semantic._();
  static const Color success = Color(0xFF83B294);
  static const Color warning = Color(0xFFD6A96A);
  static const Color danger = Color(0xFFCE8181);
  static const Color info = Color(0xFF9DB4C8);
}

// ─────────────────────────────────────────────────────────────────────────────
// GLASS
// ─────────────────────────────────────────────────────────────────────────────

/// Tuning constants for the glass approximation.
///
/// Apple's Liquid Glass is a private, GPU-side material: it bends light through
/// a curved edge (lensing) rather than merely scattering it (blur). There is no
/// public equivalent on Flutter, so this app reconstructs the look from parts
/// that do exist — a real backdrop blur for the body, a magnified backdrop
/// sample in a thin band at the rim for the lens, two hue-shifted rim samples
/// for chromatic aberration, and a painted specular highlight. It is an
/// approximation, and it is tuned to read correctly rather than to be literal.
class Glass {
  const Glass._();

  /// Fill alpha over the blurred backdrop. Dark surfaces need less because the
  /// blur already darkens; light surfaces need more to stay legible.
  static const double fillDark = 0.10;
  static const double fillDarkAmoled = 0.07;
  static const double fillLight = 0.52;

  /// The hairline at the edge of a glass surface. Without it the surface has no
  /// boundary and stops reading as a physical plate.
  static const double rimDark = 0.18;
  static const double rimLight = 0.72;

  /// Inner top highlight, the 1px catch of light along the upper edge.
  static const double specularDark = 0.22;
  static const double specularLight = 0.85;

  /// Chromatic aberration tints, applied to the two rim samples. Barely
  /// perceptible on their own; together they make the edge feel refractive.
  static const Color aberrationWarm = Color(0xFFFFB9A8);
  static const Color aberrationCool = Color(0xFFA8C6FF);
}

// ─────────────────────────────────────────────────────────────────────────────
// ACCESSIBILITY
// ─────────────────────────────────────────────────────────────────────────────

/// True when the platform asks for less motion. Above this, entry animations
/// and ambient loops are skipped and transitions collapse to a cross-fade.
bool reduceMotion(BuildContext context) =>
    MediaQuery.maybeDisableAnimationsOf(context) ?? false;

/// True when the platform asks for less transparency or more contrast. Glass
/// falls back to an opaque themed surface, which is the documented behaviour
/// rather than a degradation.
bool reduceTransparency(BuildContext context) {
  final mq = MediaQuery.maybeOf(context);
  if (mq == null) return false;
  return mq.highContrast || mq.accessibleNavigation;
}

/// Relative luminance per WCAG 2.1.
double _luminance(Color c) => c.computeLuminance();

/// Contrast ratio between two colours, 1.0 (identical) to 21.0 (black on
/// white). Body text needs 4.5, large text and icons need 3.0.
double contrastRatio(Color a, Color b) {
  final la = _luminance(a);
  final lb = _luminance(b);
  final hi = la > lb ? la : lb;
  final lo = la > lb ? lb : la;
  return (hi + 0.05) / (lo + 0.05);
}

/// Picks whichever of black or white text is legible on [background], returned
/// as the off-black / off-white pair rather than pure values.
Color readableOn(Color background) =>
    contrastRatio(DarkSurface.onSurface, background) >=
            contrastRatio(LightSurface.onSurface, background)
        ? DarkSurface.onSurface
        : LightSurface.onSurface;

/// Lifts [accent] until it clears [minRatio] against [background]. The soft
/// palette is light enough to pass on dark surfaces untouched, but a custom
/// accent on a light background can fall short, so it gets darkened instead.
Color legibleAccent(
  Color accent,
  Color background, {
  double minRatio = 4.5,
}) {
  if (contrastRatio(accent, background) >= minRatio) return accent;
  final backgroundIsDark = _luminance(background) < 0.5;
  var hsl = HSLColor.fromColor(accent);
  for (var i = 0; i < 24; i++) {
    final next = (hsl.lightness + (backgroundIsDark ? 0.03 : -0.03)).clamp(
      0.0,
      1.0,
    );
    hsl = hsl.withLightness(next);
    final candidate = hsl.toColor();
    if (contrastRatio(candidate, background) >= minRatio) return candidate;
    if (next == 0.0 || next == 1.0) break;
  }
  return hsl.toColor();
}


