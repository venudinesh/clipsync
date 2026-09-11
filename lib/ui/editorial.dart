import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_markdown/flutter_markdown.dart';
import 'package:intl/intl.dart';

import 'design_tokens.dart';

// ─────────────────────────────────────────────────────────────────────────────
// THE LEDGER
//
// The app's second surface language. The first gave every idea its own rounded
// plate, which produced screens of equally sized cards with no hierarchy: the
// shape of generated output rather than of a designed page.
//
// This file replaces the plate with a rule. Groups are separated by a hairline
// and a space step, hierarchy is carried by type weight and colour, and an
// elevated surface is spent only where elevation means something (a sheet, an
// editor, a model reply). Numbers are tabular, so a value that changes does not
// shuffle the row it sits in.
// ─────────────────────────────────────────────────────────────────────────────

/// The type ramp. Six steps, each with one job. Nothing should invent a size
/// that is not on this ladder.
class AppType {
  AppType._();

  static const List<FontFeature> figures = [FontFeature.tabularFigures()];

  static Color _on(BuildContext context, double alpha) =>
      Theme.of(context).colorScheme.onSurface.withValues(alpha: alpha);

  /// Page owner. One per screen, at the top, never repeated inside it.
  static TextStyle display(BuildContext context) => TextStyle(
    color: _on(context, 1),
    fontSize: 31,
    height: 1.02,
    fontWeight: FontWeight.w800,
    letterSpacing: -1.1,
  );

  /// Sheet, editor and dialog owner.
  static TextStyle title(BuildContext context) => TextStyle(
    color: _on(context, 1),
    fontSize: 21,
    height: 1.12,
    fontWeight: FontWeight.w800,
    letterSpacing: -0.6,
  );

  /// A row's own name. The workhorse.
  static TextStyle heading(BuildContext context) => TextStyle(
    color: _on(context, 0.96),
    fontSize: 15,
    height: 1.26,
    fontWeight: FontWeight.w700,
    letterSpacing: -0.25,
  );

  /// Sentences. Anything longer than a label.
  static TextStyle body(BuildContext context) => TextStyle(
    color: _on(context, 0.72),
    fontSize: 13.5,
    height: 1.45,
    fontWeight: FontWeight.w500,
    letterSpacing: -0.05,
  );

  /// Timestamps, counts, engine names, byte figures. Always tabular.
  static TextStyle meta(BuildContext context) => TextStyle(
    color: _on(context, 0.50),
    fontSize: 11.5,
    height: 1.2,
    fontWeight: FontWeight.w600,
    letterSpacing: 0.05,
    fontFeatures: figures,
  );

  /// The group name that sits on a rule. Sentence case on purpose: an
  /// uppercase wide-tracked micro-label above every group is the single most
  /// recognisable generated-interface tell.
  static TextStyle label(BuildContext context) => TextStyle(
    color: _on(context, 0.60),
    fontSize: 12.5,
    height: 1,
    fontWeight: FontWeight.w700,
    letterSpacing: 0,
  );

  /// A figure set large enough to be read as a figure.
  static TextStyle figure(BuildContext context) => TextStyle(
    color: _on(context, 0.96),
    fontSize: 22,
    height: 1,
    fontWeight: FontWeight.w700,
    letterSpacing: -0.5,
    fontFeatures: figures,
  );
}

/// Ink at a given strength. Everything that is not on the type ramp asks for
/// its colour this way, so light mode never inherits a dark-mode veil.
Color ink(BuildContext context, double alpha) =>
    Theme.of(context).colorScheme.onSurface.withValues(alpha: alpha);

/// The hairline. One value for the whole app: strong enough to read on both
/// canvases, quiet enough to disappear when you are reading the content.
Color hairline(BuildContext context, {double strength = 1}) {
  final dark = Theme.of(context).brightness == Brightness.dark;
  return Theme.of(context).colorScheme.onSurface.withValues(
    alpha: ((dark ? 0.11 : 0.13) * strength).clamp(0.0, 1.0),
  );
}

/// The accent, lifted to pass contrast on the current background before it is
/// ever drawn.
Color accentOn(BuildContext context, {double minRatio = 4.5}) {
  final scheme = Theme.of(context).colorScheme;
  return legibleAccent(scheme.primary, scheme.surface, minRatio: minRatio);
}

/// The faintest usable fill: on dark it lifts, on light it recesses. Spent only
/// where a block genuinely needs to sit apart from the page — a code sample, a
/// turn you typed — never as a plate around a label.
Color fill(BuildContext context, {double strength = 1}) {
  final dark = Theme.of(context).brightness == Brightness.dark;
  return dark
      ? Colors.white.withValues(alpha: (0.05 * strength).clamp(0.0, 1.0))
      : Colors.black.withValues(alpha: (0.04 * strength).clamp(0.0, 1.0));
}

/// A 1px rule. [indent] pulls the line in to the text column so it reads as a
/// separator inside a list rather than a border around a box.
class Rule extends StatelessWidget {
  const Rule({this.indent = 0, this.endIndent = 0, this.strength = 1, super.key});
  final double indent;
  final double endIndent;
  final double strength;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.only(left: indent, right: endIndent),
      child: SizedBox(
        height: 1,
        child: ColoredBox(color: hairline(context, strength: strength)),
      ),
    );
  }
}

/// Press feedback for anything tappable that is not a Material button. The
/// scale is small enough to read as the surface taking the press rather than as
/// an animation, and it collapses to nothing under Reduce Motion.
class Pressable extends StatefulWidget {
  const Pressable({
    required this.child,
    this.onTap,
    this.onLongPress,
    this.scale = 0.975,
    this.haptic = true,
    this.behavior = HitTestBehavior.opaque,
    super.key,
  });
  final Widget child;
  final VoidCallback? onTap;
  final VoidCallback? onLongPress;
  final double scale;
  final bool haptic;
  final HitTestBehavior behavior;

  @override
  State<Pressable> createState() => _PressableState();
}

class _PressableState extends State<Pressable> {
  bool _down = false;

  @override
  Widget build(BuildContext context) {
    final calm = reduceMotion(context);
    final enabled = widget.onTap != null || widget.onLongPress != null;
    return GestureDetector(
      behavior: widget.behavior,
      onTapDown: enabled ? (_) => setState(() => _down = true) : null,
      onTapUp: enabled ? (_) => setState(() => _down = false) : null,
      onTapCancel: enabled ? () => setState(() => _down = false) : null,
      onTap: widget.onTap == null
          ? null
          : () {
              if (widget.haptic) HapticFeedback.selectionClick();
              widget.onTap!();
            },
      onLongPress: widget.onLongPress == null
          ? null
          : () {
              if (widget.haptic) HapticFeedback.mediumImpact();
              widget.onLongPress!();
            },
      child: AnimatedScale(
        scale: _down && !calm ? widget.scale : 1,
        duration: Motion.instant,
        curve: kGlassCurve,
        child: widget.child,
      ),
    );
  }
}

/// The app's structural signature: a group name, the rest of the line taken up
/// by a hairline, and an optional figure or control at the right edge. This is
/// what replaced the card boundary. It costs one line of height, it says where
/// a group starts, and it carries a count without a chip around it.
class SectionHeader extends StatelessWidget {
  const SectionHeader(
    this.label, {
    this.trailing,
    this.count,
    this.top = Space.xl,
    this.bottom = Space.md,
    super.key,
  });
  final String label;

  /// A control at the right edge. Keep it to one.
  final Widget? trailing;

  /// A figure at the right edge, set tabular. Mutually exclusive with
  /// [trailing]; if both are given the control wins.
  final String? count;
  final double top;
  final double bottom;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.only(top: top, bottom: bottom),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: [
          Text(label, style: AppType.label(context)),
          const SizedBox(width: Space.md),
          const Expanded(child: Rule(strength: 0.9)),
          if (trailing != null)
            Padding(
              padding: const EdgeInsets.only(left: Space.md),
              child: trailing,
            )
          else if (count != null)
            Padding(
              padding: const EdgeInsets.only(left: Space.md),
              child: Text(count!, style: AppType.meta(context)),
            ),
        ],
      ),
    );
  }
}

/// A list row. Full bleed to the gutter, no container, no shadow, and a rule at
/// the bottom that stops short of the left edge so the column of text reads as
/// the spine of the list.
///
/// [flag] is the one place an accent may appear inside a row: a 2px edge, drawn
/// only when the row is genuinely marked (pinned), never as decoration.
class LedgerRow extends StatelessWidget {
  const LedgerRow({
    required this.child,
    this.onTap,
    this.onLongPress,
    this.flag = false,
    this.divided = true,
    this.padding = const EdgeInsets.symmetric(vertical: Space.md + 2),
    this.ruleIndent = 0,
    super.key,
  });
  final Widget child;
  final VoidCallback? onTap;
  final VoidCallback? onLongPress;
  final bool flag;
  final bool divided;
  final EdgeInsetsGeometry padding;
  final double ruleIndent;

  @override
  Widget build(BuildContext context) {
    final accent = accentOn(context, minRatio: 3);
    final body = Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Padding(
          padding: padding,
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              if (flag)
                Container(
                  width: 2,
                  margin: const EdgeInsets.only(right: Space.md, top: 2),
                  height: 17,
                  decoration: BoxDecoration(
                    color: accent,
                    borderRadius: BorderRadius.circular(Radii.pill),
                  ),
                ),
              Expanded(child: child),
            ],
          ),
        ),
        if (divided) Rule(indent: ruleIndent),
      ],
    );
    if (onTap == null && onLongPress == null) return body;
    return Pressable(onTap: onTap, onLongPress: onLongPress, child: body);
  }
}

/// The one filled control on a screen. Accent fill, contrast-checked label, and
/// a press that scales rather than ripples.
class PrimaryAction extends StatelessWidget {
  const PrimaryAction({
    required this.label,
    required this.onPressed,
    this.icon,
    this.expand = true,
    this.busy = false,
    super.key,
  });
  final String label;
  final VoidCallback? onPressed;
  final IconData? icon;
  final bool expand;
  final bool busy;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final enabled = onPressed != null && !busy;
    final fill = enabled
        ? scheme.primary
        : Color.lerp(scheme.primary, scheme.surface, 0.62)!;
    final on = legibleAccent(scheme.onPrimary, fill, minRatio: 4.5);
    return Pressable(
      onTap: enabled ? onPressed : null,
      child: Container(
        height: 48,
        width: expand ? double.infinity : null,
        padding: EdgeInsets.symmetric(horizontal: expand ? Space.lg : Space.xl),
        decoration: BoxDecoration(
          color: fill,
          borderRadius: BorderRadius.circular(Radii.control),
          // A contact shadow tinted with the fill, so the control sits on the
          // page instead of glowing above it.
          boxShadow: enabled
              ? [
                  BoxShadow(
                    color: Color.lerp(Colors.black, fill, 0.35)!.withValues(
                      alpha: scheme.brightness == Brightness.dark ? 0.34 : 0.18,
                    ),
                    blurRadius: 12,
                    offset: const Offset(0, 4),
                  ),
                ]
              : null,
        ),
        child: Row(
          mainAxisSize: expand ? MainAxisSize.max : MainAxisSize.min,
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            if (busy)
              SizedBox(
                width: 16,
                height: 16,
                child: CircularProgressIndicator(strokeWidth: 2, color: on),
              )
            else if (icon != null)
              Icon(icon, size: 18, color: on),
            if (busy || icon != null) const SizedBox(width: Space.sm + 2),
            Text(
              label,
              style: TextStyle(
                color: on,
                fontSize: 14.5,
                fontWeight: FontWeight.w700,
                letterSpacing: -0.2,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// A secondary action: a verb, an icon at the type's optical size, and nothing
/// around it. Several of these in a row read as a toolbar without becoming a
/// grid of identical squares.
class QuietAction extends StatelessWidget {
  const QuietAction({
    required this.label,
    required this.onPressed,
    this.icon,
    this.danger = false,
    this.dense = false,
    super.key,
  });
  final String label;
  final VoidCallback? onPressed;
  final IconData? icon;
  final bool danger;
  final bool dense;

  @override
  Widget build(BuildContext context) {
    final enabled = onPressed != null;
    final Color tone;
    if (!enabled) {
      tone = ink(context, 0.30);
    } else if (danger) {
      tone = legibleAccent(
        Semantic.danger,
        Theme.of(context).colorScheme.surface,
      );
    } else {
      tone = ink(context, 0.82);
    }
    return Pressable(
      onTap: onPressed,
      child: Padding(
        padding: EdgeInsets.symmetric(
          horizontal: dense ? Space.sm : Space.md - 2,
          vertical: dense ? Space.sm : Space.md - 2,
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (icon != null) ...[
              Icon(icon, size: dense ? 16 : 17, color: tone),
              const SizedBox(width: 7),
            ],
            Text(
              label,
              style: TextStyle(
                color: tone,
                fontSize: dense ? 12.5 : 13.5,
                fontWeight: FontWeight.w700,
                letterSpacing: -0.15,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// An input that is a line of type on a rule rather than a box inside a box.
/// The label sits above the field, never inside it as a placeholder.
class InlineField extends StatelessWidget {
  const InlineField({
    required this.controller,
    required this.hint,
    this.label,
    this.onSubmitted,
    this.onChanged,
    this.trailing,
    this.maxLines = 1,
    this.autofocus = false,
    this.textInputAction,
    this.keyboardType,
    super.key,
  });
  final TextEditingController controller;
  final String hint;
  final String? label;
  final ValueChanged<String>? onSubmitted;
  final ValueChanged<String>? onChanged;
  final Widget? trailing;
  final int maxLines;
  final bool autofocus;
  final TextInputAction? textInputAction;
  final TextInputType? keyboardType;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        if (label != null) ...[
          Text(label!, style: AppType.label(context)),
          const SizedBox(height: Space.sm),
        ],
        Row(
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [
            Expanded(
              child: TextField(
                controller: controller,
                autofocus: autofocus,
                maxLines: maxLines,
                minLines: 1,
                textInputAction: textInputAction,
                keyboardType: keyboardType,
                cursorColor: accentOn(context, minRatio: 3),
                style: TextStyle(
                  color: ink(context, 0.96),
                  fontSize: 14.5,
                  fontWeight: FontWeight.w500,
                  height: 1.4,
                ),
                decoration: InputDecoration(
                  isDense: true,
                  // The rule below is this field's only edge. Left null, the
                  // theme's `filled: true` would paint a tinted box tight
                  // around the type — the box inside a box this widget exists
                  // to avoid.
                  filled: false,
                  hintText: hint,
                  hintStyle: TextStyle(
                    color: ink(context, 0.42),
                    fontSize: 14.5,
                    fontWeight: FontWeight.w500,
                  ),
                  border: InputBorder.none,
                  enabledBorder: InputBorder.none,
                  focusedBorder: InputBorder.none,
                  contentPadding: const EdgeInsets.only(bottom: Space.sm + 2),
                ),
                onSubmitted: onSubmitted,
                onChanged: onChanged,
              ),
            ),
            if (trailing != null) trailing!,
          ],
        ),
        const Rule(),
      ],
    );
  }
}

/// What a list says when it is empty. Left aligned, no circle, no gradient, no
/// centred stack: a quiet glyph, a sentence that reads like a person wrote it,
/// and the one action that fills the list.
class StateBlock extends StatelessWidget {
  const StateBlock({
    required this.icon,
    required this.title,
    required this.message,
    this.action,
    this.compact = false,
    super.key,
  });
  final IconData icon;
  final String title;
  final String message;
  final Widget? action;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.symmetric(vertical: compact ? Space.xl : Space.xxl),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(icon, size: 26, color: ink(context, 0.26)),
          const SizedBox(height: Space.lg),
          Text(
            title,
            style: AppType.title(context).copyWith(fontSize: compact ? 18 : 21),
          ),
          const SizedBox(height: Space.sm - 2),
          ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 320),
            child: Text(message, style: AppType.body(context)),
          ),
          if (action != null) ...[const SizedBox(height: Space.lg + 2), action!],
        ],
      ),
    );
  }
}

/// A row of labels with a moving underline. Used for the two mode switches in
/// the app (capture mode, settings section). Not filled pills: a filled pill per
/// option reads as three buttons competing, and only one of them is the state.
class TabRail extends StatelessWidget {
  const TabRail({
    required this.labels,
    required this.index,
    required this.onChanged,
    this.spread = false,
    super.key,
  });
  final List<String> labels;
  final int index;
  final ValueChanged<int> onChanged;

  /// Spread the labels across the full width instead of packing them left.
  final bool spread;

  @override
  Widget build(BuildContext context) {
    final accent = accentOn(context, minRatio: 3);
    final calm = reduceMotion(context);
    Widget tab(int i) {
      final active = i == index;
      return Pressable(
        onTap: active ? null : () => onChanged(i),
        haptic: true,
        child: Padding(
          padding: EdgeInsets.only(
            right: spread ? 0 : Space.xl,
            bottom: Space.sm + 2,
          ),
          child: Column(
            crossAxisAlignment: spread
                ? CrossAxisAlignment.center
                : CrossAxisAlignment.start,
            children: [
              AnimatedDefaultTextStyle(
                duration: calm ? Duration.zero : Motion.fast,
                curve: kGlassCurve,
                style: TextStyle(
                  color: active ? ink(context, 0.98) : ink(context, 0.44),
                  fontSize: 14.5,
                  fontWeight: active ? FontWeight.w800 : FontWeight.w600,
                  letterSpacing: -0.3,
                ),
                child: Text(labels[i]),
              ),
              const SizedBox(height: Space.sm),
              AnimatedContainer(
                duration: calm ? Duration.zero : Motion.base,
                curve: kEntryCurve,
                height: 2,
                width: active ? 22 : 0,
                decoration: BoxDecoration(
                  color: accent,
                  borderRadius: BorderRadius.circular(Radii.pill),
                ),
              ),
            ],
          ),
        ),
      );
    }

    final tabs = [for (var i = 0; i < labels.length; i++) tab(i)];
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (spread)
          Row(
            children: [for (final t in tabs) Expanded(child: t)],
          )
        else
          SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            physics: const ClampingScrollPhysics(),
            child: Row(children: tabs),
          ),
        const Rule(),
      ],
    );
  }
}

/// The head of a bottom sheet: a grab rule, the sheet's name, and at most one
/// action opposite it.
class SheetHeader extends StatelessWidget {
  const SheetHeader({required this.title, this.subtitle, this.action, super.key});
  final String title;
  final String? subtitle;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Center(
          child: Container(
            width: 34,
            height: 3,
            margin: const EdgeInsets.only(top: Space.md, bottom: Space.lg),
            decoration: BoxDecoration(
              color: ink(context, 0.20),
              borderRadius: BorderRadius.circular(Radii.pill),
            ),
          ),
        ),
        Padding(
          padding: const EdgeInsets.fromLTRB(Space.xl, 0, Space.md, Space.md),
          child: Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(title, style: AppType.title(context)),
                    if (subtitle != null) ...[
                      const SizedBox(height: 3),
                      Text(subtitle!, style: AppType.meta(context)),
                    ],
                  ],
                ),
              ),
              if (action != null) action!,
            ],
          ),
        ),
        const Rule(),
      ],
    );
  }
}

/// An icon-only control. No plate behind it: a 40x40 tinted rounded square
/// around every glyph is the pattern that made the old interface read as a grid
/// of chips. The touch target stays 44x44 without drawing anything.
class IconAction extends StatelessWidget {
  const IconAction({
    required this.icon,
    required this.tooltip,
    required this.onPressed,
    this.size = 20,
    this.tone,
    this.dense = false,
    this.alignment = Alignment.center,
    super.key,
  });
  final IconData icon;
  final String tooltip;
  final VoidCallback? onPressed;
  final double size;
  final Color? tone;

  /// A smaller hit box, for the one place a control sits inside a list margin
  /// rather than in a header. Still the primary route to nothing: whatever it
  /// opens is also reachable from the row itself.
  final bool dense;

  /// Where the glyph sits inside the hit box, which is always larger than it.
  ///
  /// Centred is right in a row of controls, where each one's slack spaces it
  /// from the next. It is wrong at the end of a right-aligned column: the box
  /// lines up but the glyph stops half a box short, floating in from the edge
  /// everything above it is set to. [Alignment.centerRight] hangs the glyph on
  /// that edge instead, and the target keeps its full size either way.
  final AlignmentGeometry alignment;

  @override
  Widget build(BuildContext context) {
    final color = onPressed == null ? ink(context, 0.28) : (tone ?? ink(context, 0.78));
    return Tooltip(
      message: tooltip,
      child: Semantics(
        button: true,
        label: tooltip,
        child: Pressable(
          onTap: onPressed,
          scale: 0.9,
          child: SizedBox(
            width: dense ? 40 : 44,
            height: dense ? 36 : 44,
            child: Align(
              alignment: alignment,
              child: Icon(icon, size: size, color: color),
            ),
          ),
        ),
      ),
    );
  }
}

/// Entry motion for the first screenful: a short rise and fade, staggered down
/// the page so arrival establishes reading order. It runs once, it animates only
/// transform and opacity, and Reduce Motion turns it off entirely.
class Entrance extends StatefulWidget {
  const Entrance({required this.child, this.index = 0, super.key});
  final Widget child;
  final int index;

  @override
  State<Entrance> createState() => _EntranceState();
}

class _EntranceState extends State<Entrance>
    with SingleTickerProviderStateMixin {
  late final AnimationController _c = AnimationController(
    vsync: this,
    duration: Motion.slow,
  );
  Timer? _start;

  @override
  void initState() {
    super.initState();
    _start = Timer(Duration(milliseconds: 40 * widget.index), () {
      if (mounted) _c.forward();
    });
  }

  @override
  void dispose() {
    _start?.cancel();
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    if (reduceMotion(context)) return widget.child;
    final t = CurvedAnimation(parent: _c, curve: kEntryCurve);
    return AnimatedBuilder(
      animation: t,
      builder: (context, child) => Opacity(
        opacity: t.value.clamp(0.0, 1.0),
        child: Transform.translate(
          offset: Offset(0, 10 * (1 - t.value)),
          child: child,
        ),
      ),
      child: widget.child,
    );
  }
}

/// A fact on a ledger line: what it is at the left, what it says at the right.
/// Figures are tabular, so a column of them lines up on the decimal instead of
/// wobbling as the numbers change.
class LedgerFact extends StatelessWidget {
  const LedgerFact(
    this.label,
    this.value, {
    this.tone,
    this.divided = true,
    this.onTap,
    super.key,
  });
  final String label;
  final String value;
  final Color? tone;
  final bool divided;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return LedgerRow(
      divided: divided,
      onTap: onTap,
      padding: const EdgeInsets.symmetric(vertical: Space.md - 1),
      child: Row(
        children: [
          Expanded(
            child: Text(
              label,
              style: AppType.body(context).copyWith(color: ink(context, 0.80)),
            ),
          ),
          const SizedBox(width: Space.md),
          Text(
            value,
            style: AppType.meta(context).copyWith(
              color: tone ?? ink(context, 0.92),
              fontSize: 12.5,
              fontWeight: FontWeight.w700,
            ),
          ),
        ],
      ),
    );
  }
}

/// One line in a popup menu: a glyph at the label's optical size, then the
/// label. Every menu in the app builds its rows through this, so no two menus
/// size or space themselves differently.
PopupMenuItem<String> menuItem(
  String value,
  IconData icon,
  String label, {
  bool danger = false,
  bool enabled = true,
  bool checked = false,
}) {
  return PopupMenuItem<String>(
    value: value,
    height: 44,
    enabled: enabled,
    child: Builder(
      builder: (context) {
        final Color tone;
        if (!enabled) {
          tone = ink(context, 0.32);
        } else if (danger) {
          tone = legibleAccent(
            Semantic.danger,
            Theme.of(context).colorScheme.surface,
          );
        } else {
          tone = ink(context, 0.88);
        }
        return Row(
          children: [
            Icon(icon, size: 17, color: tone),
            const SizedBox(width: Space.md - 2),
            Expanded(
              child: Text(
                label,
                style: TextStyle(
                  color: tone,
                  fontSize: 13.5,
                  fontWeight: FontWeight.w600,
                  letterSpacing: -0.15,
                ),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
            ),
            // The current choice is marked with the glyph the platform uses for
            // it, not with a coloured dot.
            if (checked)
              Padding(
                padding: const EdgeInsets.only(left: Space.sm),
                child: Icon(
                  Icons.check_rounded,
                  size: 15,
                  color: accentOn(context, minRatio: 3),
                ),
              ),
          ],
        );
      },
    ),
  );
}

/// The markdown that a clip, a note and a model reply are all rendered with.
/// One stylesheet, so the same document does not change size, colour or rhythm
/// depending on which screen you opened it from.
MarkdownStyleSheet ledgerMarkdown(BuildContext context, {double size = 14}) {
  final accent = accentOn(context, minRatio: 3);
  final base = TextStyle(
    color: ink(context, 0.92),
    fontSize: size,
    height: 1.45,
    fontWeight: FontWeight.w500,
    letterSpacing: -0.1,
  );
  return MarkdownStyleSheet(
    p: base,
    strong: base.copyWith(color: ink(context, 1), fontWeight: FontWeight.w800),
    em: base.copyWith(fontStyle: FontStyle.italic),
    a: base.copyWith(color: accent, decoration: TextDecoration.underline),
    listBullet: base.copyWith(color: accent, fontWeight: FontWeight.w700),
    h1: AppType.title(context).copyWith(fontSize: size + 4),
    h2: AppType.heading(context).copyWith(fontSize: size + 2),
    h3: AppType.heading(context).copyWith(fontSize: size),
    code: base.copyWith(
      fontFamily: 'monospace',
      fontSize: size - 1.5,
      backgroundColor: Colors.transparent,
      color: ink(context, 0.82),
    ),
    codeblockPadding: const EdgeInsets.all(Space.md),
    codeblockDecoration: BoxDecoration(
      color: fill(context, strength: 1.4),
      borderRadius: BorderRadius.circular(Radii.tight),
    ),
    blockquotePadding: const EdgeInsets.only(left: Space.md),
    blockquoteDecoration: BoxDecoration(
      border: Border(
        left: BorderSide(color: accent.withValues(alpha: 0.45), width: 2),
      ),
    ),
    horizontalRuleDecoration: BoxDecoration(
      border: Border(top: BorderSide(color: hairline(context))),
    ),
    blockSpacing: Space.sm + 2,
  );
}

/// One choice in a bottom sheet: a glyph, what it does, and optionally a line
/// saying what that means. No tinted 40x40 plate behind the glyph and no
/// per-option hue: a list of four choices is a list, not four coloured cards.
class SheetAction extends StatelessWidget {
  const SheetAction({
    required this.icon,
    required this.label,
    required this.onTap,
    this.detail,
    this.danger = false,
    this.divided = true,
    super.key,
  });
  final IconData icon;
  final String label;
  final String? detail;
  final VoidCallback onTap;
  final bool danger;
  final bool divided;

  @override
  Widget build(BuildContext context) {
    final tone = danger
        ? legibleAccent(Semantic.danger, Theme.of(context).colorScheme.surface)
        : ink(context, 0.88);
    return LedgerRow(
      onTap: onTap,
      divided: divided,
      padding: const EdgeInsets.symmetric(vertical: Space.md + 1),
      child: Row(
        children: [
          Icon(icon, size: 18, color: tone),
          const SizedBox(width: Space.md + 2),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  label,
                  style: AppType.body(context)
                      .copyWith(color: tone, fontWeight: FontWeight.w600),
                ),
                if (detail != null) ...[
                  const SizedBox(height: 2),
                  Text(detail!, style: AppType.meta(context)),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}

/// Shows a sheet on the app's one sheet surface: raised off the canvas, square
/// at the bottom edge because it is anchored there, and inset from the sides by
/// the page gutter so the rows below line up with the rows on the page.
Future<T?> ledgerSheet<T>(
  BuildContext context, {
  required String title,
  required List<Widget> Function(BuildContext sheetContext) children,
  String? subtitle,
  bool scrollable = false,
}) {
  return showModalBottomSheet<T>(
    context: context,
    backgroundColor: Colors.transparent,
    barrierColor: Colors.black.withValues(alpha: 0.46),
    isScrollControlled: scrollable,
    builder: (sheetContext) {
      final scheme = Theme.of(sheetContext).colorScheme;
      final body = Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          SheetHeader(title: title, subtitle: subtitle),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: Space.xl),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: children(sheetContext),
            ),
          ),
          const SizedBox(height: Space.md),
        ],
      );
      return SafeArea(
        top: false,
        child: DecoratedBox(
          decoration: BoxDecoration(
            color: scheme.surface,
            borderRadius: const BorderRadius.vertical(
              top: Radius.circular(Radii.card),
            ),
            border: Border(top: BorderSide(color: hairline(sheetContext))),
          ),
          child: scrollable
              ? ConstrainedBox(
                  constraints: BoxConstraints(
                    maxHeight: MediaQuery.of(sheetContext).size.height * 0.82,
                  ),
                  child: SingleChildScrollView(
                    physics: const ClampingScrollPhysics(),
                    child: body,
                  ),
                )
              : body,
        ),
      );
    },
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// COMPOSITION
//
// The ledger fixed the plate problem and created a new one: every group became
// a stack of full-width rows with a hairline under each, under a label. A page
// built entirely from one family reads as undesigned however clean each part
// is, and a rule under every row of a long list is the flattest thing a list
// can be.
//
// What follows is the set of other families, so a screen can be composed from
// four or five of them instead of repeating one: a split masthead, a strip of
// figures set large, an elevated instrument, an inset field, and a list that
// hangs off a margin rail instead of sitting between two rules.
// ─────────────────────────────────────────────────────────────────────────────

/// An elevated surface. Spent once per screen, on the thing the screen is for:
/// the capture switch, the model, the editor. Everything else stays on the page.
///
/// The border radius is the card step and the shadow is tinted with the accent
/// rather than neutral black, so the surface reads as lifted off this page
/// rather than pasted onto any page.
class Panel extends StatelessWidget {
  const Panel({
    required this.child,
    this.padding = const EdgeInsets.all(Space.lg),
    this.raised = true,
    this.accentEdge = false,
    this.onTap,
    super.key,
  });
  final Widget child;
  final EdgeInsetsGeometry padding;

  /// A contact shadow. Off for a panel that sits inside another surface.
  final bool raised;

  /// A 2px accent bar down the leading edge, for a panel whose state is live.
  final bool accentEdge;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final dark = scheme.brightness == Brightness.dark;
    final accent = accentOn(context, minRatio: 3);
    Widget surface = DecoratedBox(
      decoration: BoxDecoration(
        color: dark ? DarkSurface.base : LightSurface.base,
        borderRadius: BorderRadius.circular(Radii.card),
        border: Border.all(color: hairline(context, strength: 1.1)),
        boxShadow: raised
            ? [
                BoxShadow(
                  color: Color.lerp(Colors.black, accent, 0.22)!
                      .withValues(alpha: dark ? 0.30 : 0.10),
                  blurRadius: 18,
                  offset: const Offset(0, 6),
                ),
              ]
            : null,
      ),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(Radii.card),
        // A Stack rather than a Row, so the edge takes the content's height
        // without an intrinsic pass. A non-uniform Border cannot carry a
        // radius, which rules out drawing it as a border side.
        child: Stack(
          children: [
            Padding(
              padding: padding.add(
                EdgeInsets.only(left: accentEdge ? 2 : 0),
              ),
              child: child,
            ),
            if (accentEdge)
              Positioned(
                left: 0,
                top: 0,
                bottom: 0,
                width: 2,
                child: ColoredBox(color: accent),
              ),
          ],
        ),
      ),
    );
    if (onTap != null) surface = Pressable(onTap: onTap, child: surface);
    return surface;
  }
}

/// The head of a screen. Text on the left, an asset on the right, nothing
/// centred: a centred stack of eyebrow, huge title and one-line subtitle is the
/// most reproduced page opening there is, and it wastes the width a phone has
/// least of.
///
/// [lead] is set as a pull quote against a 2px accent edge rather than as a
/// third grey paragraph, so the screen has one sentence that reads like it was
/// written and not generated.
class Masthead extends StatelessWidget {
  const Masthead({
    required this.title,
    this.eyebrow,
    this.lead,
    this.asset,
    this.actions,
    super.key,
  });
  final String title;

  /// One short line above the title, in the accent. Never a section number.
  final String? eyebrow;
  final String? lead;

  /// The right-hand element: the app mark, a figure, a state.
  final Widget? asset;
  final List<Widget>? actions;

  @override
  Widget build(BuildContext context) {
    final accent = accentOn(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  if (eyebrow != null) ...[
                    Text(
                      eyebrow!,
                      style: AppType.label(context).copyWith(color: accent),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                    const SizedBox(height: 5),
                  ],
                  Text(title, style: AppType.display(context)),
                ],
              ),
            ),
            if (asset != null)
              Padding(
                padding: const EdgeInsets.only(left: Space.md, top: 2),
                child: asset,
              ),
            if (actions != null) ...actions!,
          ],
        ),
        if (lead != null) ...[
          const SizedBox(height: Space.md + 1),
          Container(
            decoration: BoxDecoration(
              border: Border(
                left: BorderSide(
                  color: accent.withValues(alpha: 0.38),
                  width: 2,
                ),
              ),
            ),
            padding: const EdgeInsets.only(left: 11),
            child: Text(
              lead!,
              style: AppType.body(context),
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
            ),
          ),
        ],
      ],
    );
  }
}

/// A block standing in for content that has not arrived. Its opacity breathes
/// rather than a highlight sweeping across it, and under Reduce Motion it simply
/// sits there.
class Pulse extends StatefulWidget {
  const Pulse({required this.child, super.key});
  final Widget child;

  @override
  State<Pulse> createState() => _PulseState();
}

class _PulseState extends State<Pulse> with SingleTickerProviderStateMixin {
  late final AnimationController _c = AnimationController(
    vsync: this,
    duration: Motion.shimmer,
  )..repeat(reverse: true);

  @override
  void dispose() {
    _c.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    if (reduceMotion(context)) return Opacity(opacity: 0.7, child: widget.child);
    return FadeTransition(
      opacity: Tween<double>(begin: 0.45, end: 0.95).animate(
        CurvedAnimation(parent: _c, curve: kFadeCurve),
      ),
      child: widget.child,
    );
  }
}

/// One bar of placeholder text.
class SkeletonLine extends StatelessWidget {
  const SkeletonLine({this.widthFactor = 1, this.height = 11, super.key});
  final double widthFactor;
  final double height;

  @override
  Widget build(BuildContext context) {
    return Align(
      alignment: Alignment.centerLeft,
      child: FractionallySizedBox(
        widthFactor: widthFactor.clamp(0.0, 1.0),
        child: Container(
          height: height,
          decoration: BoxDecoration(
            color: fill(context, strength: 2.4),
            borderRadius: BorderRadius.circular(Radii.tight - 3),
          ),
        ),
      ),
    );
  }
}

/// Determinate progress with no track behind it. A grey capsule that is always
/// full and a coloured capsule that grows inside it draws the same thing twice;
/// the rule the page already uses is enough to show what is left.
class Meter extends StatelessWidget {
  const Meter({required this.value, this.label, this.detail, super.key});

  /// 0 to 1. Clamped, so a caller that divides by a stale total cannot overrun.
  final double value;
  final String? label;
  final String? detail;

  @override
  Widget build(BuildContext context) {
    final t = value.clamp(0.0, 1.0);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (label != null || detail != null) ...[
          Row(
            children: [
              if (label != null)
                Expanded(child: Text(label!, style: AppType.label(context))),
              if (detail != null) Text(detail!, style: AppType.meta(context)),
            ],
          ),
          const SizedBox(height: Space.sm + 1),
        ],
        SizedBox(
          height: 2,
          child: Stack(
            children: [
              Positioned.fill(
                child: ColoredBox(color: hairline(context, strength: 1.1)),
              ),
              FractionallySizedBox(
                widthFactor: t,
                child: DecoratedBox(
                  decoration: BoxDecoration(
                    color: accentOn(context, minRatio: 3),
                    borderRadius: BorderRadius.circular(Radii.pill),
                  ),
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

/// The input as an instrument: a recessed surface holding the field, its
/// secondary verbs, and the one control that commits it.
///
/// An underlined line of type is right for a setting inside a list. It is wrong
/// for the thing a screen exists to do, which needs to look like somewhere you
/// put text rather than like the row above it.
class Composer extends StatelessWidget {
  const Composer({
    required this.controller,
    required this.hint,
    required this.onSubmit,
    this.actions = const [],
    this.busy = false,
    this.maxLines = 5,
    this.tooltip = 'Send',
    super.key,
  });
  final TextEditingController controller;
  final String hint;
  final VoidCallback onSubmit;

  /// Verbs that belong to the field, on the line under it.
  final List<Widget> actions;
  final bool busy;
  final int maxLines;
  final String tooltip;

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: BoxDecoration(
        color: fill(context, strength: 0.85),
        borderRadius: BorderRadius.circular(Radii.card),
        border: Border.all(color: hairline(context, strength: 1.1)),
      ),
      padding: const EdgeInsets.fromLTRB(Space.lg, Space.md, Space.md - 2, Space.sm),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TextField(
            controller: controller,
            maxLines: maxLines,
            minLines: 1,
            textInputAction: TextInputAction.newline,
            keyboardType: TextInputType.multiline,
            cursorColor: accentOn(context, minRatio: 3),
            style: TextStyle(
              color: ink(context, 0.96),
              fontSize: 14.5,
              fontWeight: FontWeight.w500,
              height: 1.42,
            ),
            decoration: InputDecoration(
              isDense: true,
              // The panel around this field is the surface. The theme fills a
              // boxed field by default, which drew a second tinted plate hugging
              // the line of type — it read as a highlighted row, not an input.
              filled: false,
              hintText: hint,
              hintStyle: TextStyle(
                color: ink(context, 0.40),
                fontSize: 14.5,
                fontWeight: FontWeight.w500,
              ),
              border: InputBorder.none,
              enabledBorder: InputBorder.none,
              focusedBorder: InputBorder.none,
              contentPadding: EdgeInsets.zero,
            ),
          ),
          const SizedBox(height: Space.sm + 2),
          Row(
            children: [
              ...actions,
              const Spacer(),
              _Commit(
                controller: controller,
                busy: busy,
                onSubmit: onSubmit,
                tooltip: tooltip,
              ),
            ],
          ),
        ],
      ),
    );
  }
}

/// The commit control for a [Composer]. It watches the field it belongs to, so
/// an empty composer cannot be sent and does not need the caller to rebuild.
class _Commit extends StatelessWidget {
  const _Commit({
    required this.controller,
    required this.busy,
    required this.onSubmit,
    required this.tooltip,
  });
  final TextEditingController controller;
  final bool busy;
  final VoidCallback onSubmit;
  final String tooltip;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return ValueListenableBuilder<TextEditingValue>(
      valueListenable: controller,
      builder: (context, value, _) {
        final ready = value.text.trim().isNotEmpty && !busy;
        final surface = ready ? scheme.primary : fill(context, strength: 2.2);
        final glyph = ready
            ? legibleAccent(scheme.onPrimary, scheme.primary)
            : ink(context, 0.34);
        return Tooltip(
          message: tooltip,
          child: Semantics(
            button: true,
            enabled: ready,
            label: tooltip,
            child: Pressable(
              onTap: ready ? onSubmit : null,
              scale: 0.9,
              child: AnimatedContainer(
                duration: reduceMotion(context) ? Duration.zero : Motion.fast,
                curve: kFadeCurve,
                width: 38,
                height: 34,
                decoration: BoxDecoration(
                  color: surface,
                  borderRadius: BorderRadius.circular(Radii.control - 2),
                ),
                child: Center(
                  child: busy
                      ? SizedBox(
                          width: 15,
                          height: 15,
                          child: CircularProgressIndicator(
                            strokeWidth: 2,
                            color: ink(context, 0.44),
                          ),
                        )
                      : Icon(Icons.arrow_upward_rounded, size: 18, color: glyph),
                ),
              ),
            ),
          ),
        );
      },
    );
  }
}

/// One thing a feed row does when you drag it aside.
///
/// [dismiss] is what separates "act on this row" from "remove this row": a pin
/// springs the row back under your finger because the row is still there
/// afterwards, a delete carries it off the edge because it is not.
class SwipeAct {
  const SwipeAct({
    required this.icon,
    required this.label,
    required this.onAct,
    this.tone,
    this.dismiss = false,
  });
  final IconData icon;

  /// One word. It is read at a glance, from behind a moving row.
  final String label;
  final VoidCallback onAct;

  /// Defaults to the accent, which is what an ordinary action gets. Only a
  /// destructive one asks for [Semantic.danger].
  final Color? tone;
  final bool dismiss;
}

/// A list entry that hangs off a margin instead of sitting between two rules.
///
/// The time and the entry's own control live in the left margin, and a single
/// vertical rule separates them from the content, so a feed of twenty clips
/// draws twenty short verticals rather than twenty full-width horizontals. When
/// the entry is marked the rule itself becomes the accent, which means the flag
/// costs nothing extra on the page.
///
/// Tap opens, long press gives the row's actions, and dragging the row aside
/// runs one action without opening anything. All three are wired here rather
/// than at each call site so the clips feed, the notes feed and the chat history
/// cannot drift apart in how they answer the same gesture.
class MarginEntry extends StatelessWidget {
  const MarginEntry({
    required this.margin,
    required this.child,
    this.marginAction,
    this.flag = false,
    this.onTap,
    this.onLongPress,
    this.marginWidth = 48,
    this.swipeId,
    this.swipeRight,
    this.swipeLeft,
    super.key,
  }) : assert(
          (swipeRight == null && swipeLeft == null) || swipeId != null,
          'A swipeable entry needs a stable swipeId of its own.',
        );

  /// The tabular line in the margin: a time, a count, an index.
  final String margin;
  final Widget child;

  /// The entry's menu, under the margin line.
  ///
  /// The margin is one right-aligned column, so a control here has to hang its
  /// glyph on the right edge of its own hit box — `IconAction` takes
  /// `alignment: Alignment.centerRight` for exactly this. Aligning only the box
  /// leaves the glyph short of the edge the time above it is set to, which is
  /// what made the dots read as dropped rather than filed.
  final Widget? marginAction;
  final bool flag;
  final VoidCallback? onTap;
  final VoidCallback? onLongPress;
  final double marginWidth;

  /// Identity for the drag, unique within the feed. The row's own id.
  final String? swipeId;

  /// Revealed by dragging the row to the right, and to the left.
  final SwipeAct? swipeRight;
  final SwipeAct? swipeLeft;

  @override
  Widget build(BuildContext context) {
    final accent = accentOn(context, minRatio: 3);
    final body = Padding(
      padding: const EdgeInsets.symmetric(vertical: Space.md - 2),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: marginWidth,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.end,
              mainAxisSize: MainAxisSize.min,
              children: [
                Padding(
                  padding: const EdgeInsets.only(top: 1),
                  child: Text(
                    margin,
                    style: AppType.meta(context),
                    textAlign: TextAlign.right,
                  ),
                ),
                if (marginAction != null) marginAction!,
              ],
            ),
          ),
          Expanded(
            child: Container(
              decoration: BoxDecoration(
                border: Border(
                  left: BorderSide(
                    color: flag ? accent : hairline(context, strength: 1.2),
                    width: flag ? 2 : 1,
                  ),
                ),
              ),
              padding: EdgeInsets.only(left: flag ? Space.lg - 1 : Space.lg),
              child: child,
            ),
          ),
        ],
      ),
    );
    if (onTap == null && onLongPress == null) return _swipeable(body);
    return _swipeable(
      Pressable(
        onTap: onTap,
        onLongPress: onLongPress,
        scale: 0.99,
        child: body,
      ),
    );
  }

  Widget _swipeable(Widget row) {
    if (swipeRight == null && swipeLeft == null) return row;
    return _SwipeLayer(
      id: swipeId!,
      right: swipeRight,
      left: swipeLeft,
      child: row,
    );
  }
}

/// The drag half of a feed row.
///
/// What shows behind the moving row is an icon and one word at the edge the row
/// came from, over the faintest wash of the action's own colour — not the
/// saturated slab a stock dismissible paints, which turns a quiet feed into a
/// traffic light the moment a finger touches it. The wash exists only so the
/// glyph has something to sit on.
class _SwipeLayer extends StatefulWidget {
  const _SwipeLayer({
    required this.id,
    required this.child,
    this.right,
    this.left,
  });
  final String id;
  final Widget child;
  final SwipeAct? right;
  final SwipeAct? left;

  @override
  State<_SwipeLayer> createState() => _SwipeLayerState();
}

class _SwipeLayerState extends State<_SwipeLayer> {
  /// Whether the drag is currently far enough to fire. Tracked only so the
  /// phone can tick once at the crossing, which is what tells a thumb it has
  /// gone far enough without the eye having to check.
  bool _armed = false;

  DismissDirection get _direction {
    if (widget.right != null && widget.left != null) {
      return DismissDirection.horizontal;
    }
    return widget.right != null
        ? DismissDirection.startToEnd
        : DismissDirection.endToStart;
  }

  SwipeAct? _actFor(DismissDirection direction) =>
      direction == DismissDirection.startToEnd ? widget.right : widget.left;

  @override
  Widget build(BuildContext context) {
    return Dismissible(
      key: ValueKey('swipe-${widget.id}'),
      direction: _direction,
      // A third of the row. Short enough to reach with one thumb, long enough
      // that a sloppy vertical flick never deletes anything.
      dismissThresholds: const {
        DismissDirection.startToEnd: 0.32,
        DismissDirection.endToStart: 0.32,
      },
      movementDuration: Motion.base,
      resizeDuration: Motion.fast,
      // A dismissible refuses a secondary background without a primary one, so
      // a row that only drags one way still gets an empty layer behind the
      // direction it does not travel. Nothing is ever drawn there: [_direction]
      // has already ruled that side out.
      background: widget.right == null
          ? const SizedBox.shrink()
          : _reveal(widget.right!, Alignment.centerLeft),
      secondaryBackground: widget.left == null
          ? null
          : _reveal(widget.left!, Alignment.centerRight),
      onUpdate: (details) {
        if (details.reached == _armed) return;
        _armed = details.reached;
        if (_armed) HapticFeedback.selectionClick();
      },
      confirmDismiss: (direction) async {
        final act = _actFor(direction);
        if (act == null) return false;
        HapticFeedback.mediumImpact();
        // A dismissing action runs once the row has actually left, so the two
        // things happen in the order the eye expects.
        if (act.dismiss) return true;
        act.onAct();
        return false;
      },
      onDismissed: (direction) => _actFor(direction)?.onAct(),
      child: widget.child,
    );
  }

  Widget _reveal(SwipeAct act, Alignment side) {
    final tone = legibleAccent(
      act.tone ?? Theme.of(context).colorScheme.primary,
      Theme.of(context).colorScheme.surface,
      minRatio: 3,
    );
    final fromLeft = side == Alignment.centerLeft;
    return ColoredBox(
      color: tone.withValues(alpha: 0.07),
      child: Align(
        alignment: side,
        child: Padding(
          padding: EdgeInsets.only(
            left: fromLeft ? Space.lg : 0,
            right: fromLeft ? 0 : Space.lg,
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(act.icon, size: 17, color: tone),
              const SizedBox(width: Space.sm),
              Text(
                act.label,
                style: AppType.label(context).copyWith(color: tone),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// One figure and what it counts.
class Figure {
  const Figure(this.value, this.label);
  final String value;
  final String label;
}

/// The screen's numbers, read at a glance: one figure set large enough to be
/// the second thing you see, and up to two more hung off the right edge.
///
/// Deliberately not three equal cells. Three equal anything is the shape of a
/// generated feature grid, and it says none of the three matters more, which is
/// never true.
class Readout extends StatelessWidget {
  const Readout({
    required this.value,
    required this.label,
    this.facts = const [],
    super.key,
  });
  final String value;
  final String label;
  final List<Figure> facts;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Rule(strength: 0.9),
        Padding(
          padding: const EdgeInsets.only(top: Space.md + 2),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    value,
                    style: AppType.figure(context).copyWith(
                      fontSize: 34,
                      fontWeight: FontWeight.w800,
                      letterSpacing: -1.6,
                    ),
                  ),
                  const SizedBox(height: 3),
                  Text(label, style: AppType.meta(context)),
                ],
              ),
              const Spacer(),
              if (facts.isNotEmpty)
                Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    for (var i = 0; i < facts.length; i++)
                      Padding(
                        padding: EdgeInsets.only(top: i == 0 ? 0 : 5),
                        child: Text.rich(
                          TextSpan(
                            children: [
                              TextSpan(
                                text: facts[i].value,
                                style: AppType.meta(context).copyWith(
                                  color: ink(context, 0.92),
                                  fontSize: 12.5,
                                  fontWeight: FontWeight.w800,
                                ),
                              ),
                              TextSpan(
                                text: ' ${facts[i].label}',
                                style: AppType.meta(context),
                              ),
                            ],
                          ),
                        ),
                      ),
                  ],
                ),
            ],
          ),
        ),
      ],
    );
  }
}

/// The margin stamp. Compact on purpose: it sits in a 48px column, where
/// "just now" wraps onto two lines and "now" does not.
String stamp(DateTime dt) {
  final diff = DateTime.now().difference(dt);
  if (diff.inMinutes < 1) return 'now';
  if (diff.inMinutes < 60) return '${diff.inMinutes}m';
  if (diff.inHours < 24) return '${diff.inHours}h';
  if (diff.inDays < 7) return '${diff.inDays}d';
  return DateFormat('MMM d').format(dt);
}

/// The same moment written out, for a sheet or a header that has room for it.
String stampLong(DateTime dt) {
  final now = DateTime.now();
  final sameDay =
      dt.year == now.year && dt.month == now.month && dt.day == now.day;
  return sameDay
      ? 'Today at ${DateFormat.jm().format(dt)}'
      : DateFormat('MMM d, h:mm a').format(dt);
}

/// A count and its noun, pluralised. Written once because five screens print
/// "1 note" / "4 notes" and each one getting its own ternary is how the copy
/// drifts apart.
String plural(int n, String one, [String? many]) =>
    '$n ${n == 1 ? one : (many ?? '${one}s')}';

/// Words in a blob of text. One definition, because "240 words" printed by two
/// screens that count differently is worse than either count alone.
int wordCount(String text) =>
    text.trim().isEmpty ? 0 : text.trim().split(RegExp(r'\s+')).length;
