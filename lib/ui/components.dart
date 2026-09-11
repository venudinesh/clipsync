import 'package:flutter/material.dart';

import 'design_tokens.dart';
import 'editorial.dart';

/// The app's mark: a rose squircle with a soft light across the top and the
/// clipboard glyph in the middle. The launcher icon is generated from the same
/// glyph, so the home screen and the header cannot drift apart.
class AppMark extends StatelessWidget {
  const AppMark({
    this.size = 36,
    this.icon = Icons.content_paste_rounded,
    super.key,
  });
  final double size;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final dark = scheme.brightness == Brightness.dark;
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(size * 0.30),
        // A contact shadow, not a glow. The mark is not a light source, so the
        // shadow stays under it and tinted rather than blooming outward.
        boxShadow: [
          BoxShadow(
            color: Color.lerp(Colors.black, scheme.primary, 0.30)!
                .withValues(alpha: dark ? 0.34 : 0.16),
            blurRadius: 10,
            offset: const Offset(0, 3),
          ),
        ],
      ),
      child: DecoratedBox(
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(size * 0.30),
          gradient: LinearGradient(
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
            colors: [scheme.primary, scheme.tertiary],
          ),
        ),
        child: Stack(
          children: [
            Positioned(
              top: 1.5,
              left: size * 0.18,
              right: size * 0.18,
              child: Container(
                height: size * 0.30,
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(size * 0.5),
                  gradient: LinearGradient(
                    begin: Alignment.topCenter,
                    end: Alignment.bottomCenter,
                    colors: [
                      Colors.white.withValues(alpha: 0.55),
                      Colors.white.withValues(alpha: 0.0),
                    ],
                  ),
                ),
              ),
            ),
            Center(
              child: Icon(icon, color: scheme.onPrimary, size: size * 0.50),
            ),
          ],
        ),
      ),
    );
  }
}

/// A page header: the page's name, an optional line under it, and at most a
/// couple of actions at the right edge, closed by a hairline. No plate, and no
/// window dots. Three coloured circles are a desktop-window quotation this app
/// never earns, and they spend three hues nothing else on the page uses.
class SolidWindowHeader extends StatelessWidget {
  const SolidWindowHeader({
    required this.title,
    this.subtitle,
    this.leading,
    this.trailing,
    this.compact = false,
    super.key,
  });
  final String title;
  final String? subtitle;
  final Widget? leading;
  final List<Widget>? trailing;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: EdgeInsets.fromLTRB(
          Space.lg, compact ? Space.sm : Space.md, Space.lg, 0),
      child: Column(
        children: [
          Row(
            children: [
              if (leading != null) ...[
                leading!,
                const SizedBox(width: Space.md),
              ],
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      title,
                      style: compact
                          ? AppType.heading(context)
                          : AppType.title(context),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                    if (subtitle != null) ...[
                      const SizedBox(height: 3),
                      Text(subtitle!,
                          style: AppType.meta(context),
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis),
                    ],
                  ],
                ),
              ),
              if (trailing != null) ...[
                const SizedBox(width: Space.sm),
                ...trailing!,
              ],
            ],
          ),
          const SizedBox(height: Space.md),
          const Rule(),
        ],
      ),
    );
  }
}

/// A switch: a track, a thumb, and the accent as the on state. The shimmer
/// sweep that used to run across it was an idle loop on every settings row,
/// which is the animation Reduce Motion exists to stop and the one a list of
/// settings has least use for.
class LiquidToggle extends StatelessWidget {
  const LiquidToggle({
    required this.value,
    required this.onChanged,
    this.activeColor,
    super.key,
  });
  final bool value;
  final ValueChanged<bool> onChanged;
  final Color? activeColor;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final accent = activeColor ?? scheme.primary;
    final calm = reduceMotion(context);
    return Semantics(
      toggled: value,
      child: Pressable(
        onTap: () => onChanged(!value),
        scale: 0.94,
        child: AnimatedContainer(
          duration: calm ? Duration.zero : Motion.fast,
          curve: kGlassCurve,
          width: 46,
          height: 28,
          padding: const EdgeInsets.all(3),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(Radii.pill),
            color: value
                ? legibleAccent(accent, scheme.surface, minRatio: 2.2)
                : ink(context, 0.13),
          ),
          child: AnimatedAlign(
            duration: calm ? Duration.zero : Motion.fast,
            curve: kGlassCurve,
            alignment: value ? Alignment.centerRight : Alignment.centerLeft,
            child: Container(
              width: 22,
              height: 22,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                color: value ? scheme.surface : ink(context, 0.55),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

const Set<String> _acronyms = {
  'ai', 'llm', 'ocr', 'gguf', 'ram', 'cpu', 'apk', 'os', 'ui', 'id',
};

/// Un-shouts a label. Call sites may still pass an all-caps string; the
/// interface stops shouting it back, because an uppercase wide-tracked label
/// above every group is the loudest generated-interface tell there is. A value
/// that arrives already lowercase, as an enum name does, is capitalised rather
/// than passed through, so no line in the interface opens in lower case.
String sentenceCase(String raw) {
  final trimmed = raw.trim();
  if (trimmed.isEmpty) return trimmed;
  final words = (trimmed.toUpperCase() == trimmed ? trimmed.toLowerCase() : trimmed)
      .split(' ');
  for (var i = 0; i < words.length; i++) {
    final w = words[i];
    if (w.isEmpty) continue;
    if (_acronyms.contains(w.toLowerCase())) {
      words[i] = w.toUpperCase();
    } else if (i == 0) {
      words[i] = w[0].toUpperCase() + w.substring(1);
    }
  }
  return words.join(' ');
}

/// A group name on a rule, with an optional figure at the right edge. Kept for
/// call sites that already ask for a label; it renders as [SectionHeader].
class SectionLabel extends StatelessWidget {
  const SectionLabel(this.text, {this.count, this.top = 0, super.key});
  final String text;
  final String? count;
  final double top;

  @override
  Widget build(BuildContext context) =>
      SectionHeader(sentenceCase(text), count: count, top: top);
}

/// What a list says when it is empty. The 92px gradient circle it used to draw
/// is gone: a bloom around an outline glyph made every empty screen read as an
/// alert, and it is the single most reproduced decoration in generated UI.
class EmptyState extends StatelessWidget {
  const EmptyState({
    required this.icon,
    required this.title,
    required this.message,
    this.action,
    super.key,
  });
  final IconData icon;
  final String title;
  final String message;
  final Widget? action;

  @override
  Widget build(BuildContext context) => StateBlock(
    icon: icon,
    title: title,
    message: message,
    action: action,
  );
}

/// Work in flight: a label and a 2px indeterminate rule, on the page rather
/// than on a plate, because a plate makes a passing state look like furniture.
class ProcessingBar extends StatelessWidget {
  const ProcessingBar({this.label = 'Working', super.key});
  final String label;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: Space.sm + 2),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(label, style: AppType.label(context)),
          const SizedBox(height: Space.sm + 1),
          _Indeterminate(color: accentOn(context, minRatio: 3)),
        ],
      ),
    );
  }
}

/// The progress line. Under Reduce Motion it stops travelling and simply states
/// that something is running, instead of animating for as long as it takes.
class _Indeterminate extends StatelessWidget {
  const _Indeterminate({required this.color});
  final Color color;

  @override
  Widget build(BuildContext context) {
    if (reduceMotion(context)) {
      return SizedBox(
        height: 2,
        child: ColoredBox(color: color.withValues(alpha: 0.55)),
      );
    }
    return SizedBox(
      height: 2,
      child: LinearProgressIndicator(
        minHeight: 2,
        color: color,
        backgroundColor: hairline(context, strength: 1.6),
      ),
    );
  }
}

/// An icon-only header control. Same API as before, with the 40x40 tinted plate
/// removed: see [IconAction].
class HeaderIconButton extends StatelessWidget {
  const HeaderIconButton({
    required this.icon,
    required this.tooltip,
    required this.onPressed,
    super.key,
  });
  final IconData icon;
  final String tooltip;
  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) =>
      IconAction(icon: icon, tooltip: tooltip, onPressed: onPressed);
}

/// One fact on a line: the glyph at the type's own size, then the value. The
/// tinted rounded square that used to sit behind the glyph turned three plain
/// facts into three chips competing with the content they described.
class StatBadge extends StatelessWidget {
  const StatBadge({
    required this.icon,
    required this.iconColor,
    required this.label,
    super.key,
  });
  final IconData icon;
  final Color iconColor;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, color: iconColor.withValues(alpha: 0.92), size: 14),
        const SizedBox(width: 6),
        Text(
          label,
          style: AppType.meta(context).copyWith(color: ink(context, 0.68)),
        ),
      ],
    );
  }
}
