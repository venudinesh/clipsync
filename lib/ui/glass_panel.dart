import 'package:flutter/material.dart';

import 'design_tokens.dart';

/// Global AMOLED mode flag — set from settings.
final ValueNotifier<bool> appAmoledMode = ValueNotifier(false);

/// Global accent color — set from settings whenever the user picks a new accent,
/// and read live by `AppColors.primaryAccent` so the whole UI follows it.
final ValueNotifier<Color> appAccentColor = ValueNotifier(kDefaultAccent);

/// Global glass mode flag — `true` = liquid-glass surfaces, `false` =
/// basic/solid Material surfaces (the default a fresh install opens on).
/// Toggled from Settings › Themes & UI, and persisted, so a user who picks
/// Liquid Glass keeps it.
final ValueNotifier<bool> appGlassMode = ValueNotifier(false);

/// A pane of glass with a rim hairline and a top-edge specular catch.
///
/// Uses a semi-transparent fill rather than a `BackdropFilter`. That is a
/// deliberate cost decision: content surfaces appear dozens at a time in
/// scrolling lists, and per-surface blur passes are what turn a glass UI into
/// a slideshow. The real backdrop blur is spent where it reads best, on the
/// navigation layer.
class GlassPanel extends StatelessWidget {
  const GlassPanel({
    required this.child,
    this.margin = EdgeInsets.zero,
    this.padding,
    this.borderRadius = Radii.card,
    this.tint,
    this.elevation = 1,
    this.borderColor,
    this.gradient,
    this.opacity,
    this.blurSigma,
    super.key,
  });
  final Widget child;
  final EdgeInsetsGeometry margin;
  final EdgeInsetsGeometry? padding;
  final double borderRadius;
  final Color? tint;
  final int elevation;
  final Color? borderColor;
  final Gradient? gradient;
  final double? opacity;
  final double? blurSigma;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final dark = theme.brightness == Brightness.dark;
    final amoled = dark && appAmoledMode.value;
    final scheme = theme.colorScheme;
    // Reduced transparency wins over the glass switch: an accessibility
    // request is not a preference to be overridden.
    final glass = appGlassMode.value && !reduceTransparency(context);
    final r = BorderRadius.circular(borderRadius);
    // In Material mode we rely on tonal fill + elevation (no hairline outline),
    // unless the caller explicitly requested a border (e.g. the nav bar).
    final showBorder = glass || borderColor != null;
    final border = borderColor ??
        (glass
            ? Colors.white.withValues(
                alpha: dark ? Glass.rimDark * 0.8 : Glass.rimLight * 0.85)
            : scheme.outlineVariant.withValues(alpha: dark ? 0.7 : 1.0));
    final Color fill;
    if (tint != null) {
      fill = tint!;
    } else if (glass) {
      fill = Colors.white.withValues(
        alpha: opacity ??
            (amoled
                ? Glass.fillDarkAmoled
                : dark
                    ? Glass.fillDark * 0.8
                    : Glass.fillLight),
      );
    } else {
      fill = dark
          ? (amoled ? const Color(0xFF0A0A0C) : scheme.surfaceContainer)
          : scheme.surfaceContainerLowest;
    }
    // Shadows carry the canvas hue rather than neutral black, so a surface
    // reads as sitting on this page instead of floating over a generic one.
    final shadowTint = dark
        ? Color.lerp(Colors.black, scheme.surfaceContainerHighest, 0.18)!
        : Color.lerp(Colors.black, scheme.primary, 0.22)!;
    final shadows = <BoxShadow>[
      BoxShadow(
        color: shadowTint.withValues(alpha: dark ? 0.34 : 0.07),
        blurRadius: 10 + elevation * 7.0,
        offset: Offset(0, 4 + elevation * 2.0),
      ),
      if (elevation >= 2)
        BoxShadow(
          color: shadowTint.withValues(alpha: dark ? 0.18 : 0.04),
          blurRadius: 26,
          spreadRadius: -6,
          offset: const Offset(0, 12),
        ),
    ];
    return Container(
      margin: margin,
      decoration: BoxDecoration(
        borderRadius: r,
        border: showBorder ? Border.all(color: border, width: 0.7) : null,
        boxShadow: shadows,
        gradient: gradient,
      ),
      child: ClipRRect(
        borderRadius: r,
        child: DecoratedBox(
          decoration: BoxDecoration(color: fill),
          child: Stack(
            children: [
              if (padding != null)
                Padding(padding: padding!, child: child)
              else
                child,
              // The 1px catch of light along the upper edge. Without it the
              // fill has no thickness and stops reading as a plate.
              if (glass)
                Positioned(
                  top: 0,
                  left: 0,
                  right: 0,
                  child: IgnorePointer(
                    child: Container(
                      height: 1,
                      decoration: BoxDecoration(
                        gradient: LinearGradient(
                          colors: [
                            Colors.white.withValues(
                              alpha: dark
                                  ? Glass.specularDark * 0.6
                                  : Glass.specularLight * 0.65,
                            ),
                            Colors.transparent,
                          ],
                        ),
                      ),
                    ),
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}
