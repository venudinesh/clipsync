import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'dart:ui' show PlatformDispatcher;

import 'package:file_picker/file_picker.dart';
import 'package:flutter/foundation.dart'
    show kDebugMode, defaultTargetPlatform, TargetPlatform;
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_markdown/flutter_markdown.dart';
import 'package:google_mlkit_text_recognition/google_mlkit_text_recognition.dart';
import 'package:hive_flutter/hive_flutter.dart';
import 'package:image_picker/image_picker.dart';
import 'package:intl/intl.dart';
import 'package:path_provider/path_provider.dart';
import 'package:permission_handler/permission_handler.dart';
import 'package:uuid/uuid.dart';
import 'package:whisper_flutter_new/whisper_flutter_new.dart';

import 'services/device_info_service.dart';
import 'services/ai_connection_service.dart';
import 'services/on_device_llm_service.dart';
import 'services/secure_storage_service.dart';
import 'services/voice_recorder_service.dart';

import 'ui/ui.dart';
import 'features/chat_view.dart';
import 'features/chat_history.dart';
import 'features/timely_messages.dart';
import 'onboarding/onboarding_gate.dart';

// ─────────────────────────────────────────────────────────────────────────────
// CONSTANTS & THEME
// ─────────────────────────────────────────────────────────────────────────────

class AppColors {
  AppColors._();

  /// Off-black, not `#000000`. Pure black cannot receive a shadow, so depth
  /// collapses on it. True black is still reachable, but only through the
  /// AMOLED toggle, where losing that depth is the whole point.
  static const Color canvas = DarkSurface.canvas;

  /// The one accent, live from settings.
  static Color get primaryAccent => appAccentColor.value;

  /// A near neighbour of the accent, used only for ambient gradients. Derived
  /// rather than fixed so the app never shows two competing hues.
  static Color get secondaryAccent => accentCompanion(appAccentColor.value);

  static const Color surfaceGlass = Color(0x0FFFFFFF); // 0.06 white
  static const Color surfaceGlassSubtle = Color(0x0AFFFFFF); // 0.04 white
  static const Color specEdge = Color(0x2EFFFFFF); // 0.18 white
  static const Color textPrimary = DarkSurface.onSurface;
  static const Color textSecondary = Color(0xFF9C9A97);
  static const Color textTertiary = Color(0xFF6A6864);

  // Semantic colours, desaturated to sit beside the soft accents. Danger still
  // reads as danger; it just stops shouting.
  static const Color danger = Semantic.danger;
  static const Color success = Semantic.success;
  static const Color warning = Semantic.warning;

  // Theme-aware text colors
  static Color themePrimary(BuildContext context) {
    final bright = Theme.of(context).brightness;
    return bright == Brightness.dark
        ? DarkSurface.onSurface
        : LightSurface.onSurface;
  }

  static Color themeSecondary(BuildContext context) {
    final bright = Theme.of(context).brightness;
    return bright == Brightness.dark
        ? const Color(0xFF9C9A97)
        : const Color(0xFF5F5C57);
  }

  static Color themeTertiary(BuildContext context) {
    final bright = Theme.of(context).brightness;
    return bright == Brightness.dark
        ? const Color(0xFF6A6864)
        : const Color(0xFF938F88);
  }

  static Color themeCanvas(BuildContext context) {
    final bright = Theme.of(context).brightness;
    if (bright != Brightness.dark) return LightSurface.canvas;
    return appAmoledMode.value ? const Color(0xFF000000) : DarkSurface.canvas;
  }

  static Color themeSurface(BuildContext context) {
    final bright = Theme.of(context).brightness;
    return bright == Brightness.dark
        ? Colors.white.withValues(alpha: 0.06)
        : Colors.black.withValues(alpha: 0.05);
  }

  /// Theme-aware hairline divider colour — visible on both light and dark
  /// themes (replaces hardcoded `Colors.white12` which is invisible on light).
  static Color divider(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final dark = scheme.brightness == Brightness.dark;
    return dark
        ? Colors.white.withValues(alpha: 0.10)
        : Colors.black.withValues(alpha: 0.10);
  }

  /// Low-level fill for inset plates, chips and toolbars. On dark it lifts,
  /// on light it recesses: a white veil over a white page is not a surface,
  /// so every one of these has to be asked for by intent, not by colour.
  static Color themeFill(BuildContext context, {double strength = 1}) {
    final dark = Theme.of(context).brightness == Brightness.dark;
    return dark
        ? Colors.white.withValues(alpha: (0.05 * strength).clamp(0.0, 1.0))
        : Colors.black.withValues(alpha: (0.04 * strength).clamp(0.0, 1.0));
  }

  /// The hairline that goes around [themeFill].
  static Color themeOutline(BuildContext context, {double strength = 1}) {
    final dark = Theme.of(context).brightness == Brightness.dark;
    return dark
        ? Colors.white.withValues(alpha: (0.11 * strength).clamp(0.0, 1.0))
        : Colors.black.withValues(alpha: (0.10 * strength).clamp(0.0, 1.0));
  }
}

/// Shared design-language helpers — `menuItem`, `ledgerMarkdown`, `SheetAction`
/// and `ledgerSheet` — live in `ui/editorial.dart` and arrive via `ui/ui.dart`.
class AppDefaults {
  AppDefaults._();

  static const String defaultModel = 'smollm2-135m';
  static const Duration clipboardDebounce = Duration(seconds: 2);
  static const Duration apiTimeout = Duration(seconds: 30);
  static const String hiveClipBox = 'clip_history';
  static const String hiveNoteBox = 'notes';
  static const String hiveSettingsBox = 'settings';
  static const String hiveChatBox = 'chat_sessions';
  static const String themeModeKey = 'themeMode'; // 'dark', 'light', 'system'
  static const String amoledKey = 'amoledMode';
  static const String accentColorKey = 'accentColor';
  static const String glassModeKey = 'glassMode'; // true = liquid-glass, false = Material (default)
  static const String lastModelPathKey = 'lastModelPath'; // auto-reloaded on launch
}

/// The app's canonical tab order. The `PageView` is always built in this
/// order; the navigation bar may render a permutation of it. Keeping the two
/// separate is what makes reordering safe: no feature can be lost by a bad
/// stored order.
const List<String> kNavTabLabels = ['Clips', 'Chat', 'Notes', 'OCR', 'Settings'];

const List<IconData> kNavTabIcons = [
  Icons.inbox_outlined,
  Icons.forum_outlined,
  Icons.edit_note_rounded,
  Icons.document_scanner_outlined,
  Icons.tune_rounded,
];

const List<IconData> kNavTabSelectedIcons = [
  Icons.inbox_rounded,
  Icons.forum_rounded,
  Icons.edit_note,
  Icons.document_scanner,
  Icons.tune,
];

/// Everything the floating Liquid Glass navigation bar can be tuned to.
///
/// Persisted to the `settings` box as a plain map, so adding a field here is
/// backward compatible: [NavBarConfig.fromMap] falls back to the default for
/// anything the stored map predates.
class NavBarConfig {
  final bool enabled;
  final double barSize; // 80–160, capsule height driver
  final double barWidth; // 50–100, percentage of available width
  final double position; // 0–40, gap above the bottom safe area
  final double cornerRoundness; // 0–100, 100 is a full capsule
  final double opacity; // 0–100, glass fill strength
  final double blurSigma; // 0–40, backdrop blur
  final double refractionDepth; // 0–100, thickness of the lens band
  final double refractionStrength; // 0–100, how hard the lens band bites
  final double chromaticSplit; // 0–100, colour fringe offset
  final bool useIOSGlassMode; // true = lensed (Android 13+), false = frosted
  final bool holdToSwipe; // swipe across the bar to change tabs
  final double swipeSensitivity; // 1–10
  final bool invertSwipe;

  /// Destination order, as indices into the app's canonical tab list. Empty
  /// means "untouched", which keeps the default order without having to store
  /// it. Reordering never adds or removes a destination.
  final List<int> order;

  /// The shipped tuning: a wide capsule of *clear* glass — no tint, no frost —
  /// carried entirely by the lens rim at full depth and strength, with no
  /// colour fringe. Sideways drags across the bar are inverted, which is the
  /// direct-manipulation mapping: the highlight travels with your finger, so
  /// dragging right selects the tab to the right.
  const NavBarConfig({
    this.enabled = true,
    this.barSize = 116,
    this.barWidth = 89,
    this.position = 16,
    this.cornerRoundness = 100,
    this.opacity = 0,
    this.blurSigma = 0,
    this.refractionDepth = 100,
    this.refractionStrength = 100,
    this.chromaticSplit = 0,
    this.useIOSGlassMode = true,
    this.holdToSwipe = true,
    this.swipeSensitivity = 5.0,
    this.invertSwipe = true,
    this.order = const [],
  });

  double get opacityFactor => (opacity / 100).clamp(0.0, 1.0);

  /// The order to actually render, repaired against [count]. A stored order
  /// that is stale, truncated or duplicated degrades to the default rather
  /// than dropping a tab, so a bad write can never hide a feature.
  List<int> resolvedOrder(int count) {
    final seen = <int>{};
    final out = <int>[];
    for (final i in order) {
      if (i >= 0 && i < count && seen.add(i)) out.add(i);
    }
    for (var i = 0; i < count; i++) {
      if (!seen.contains(i)) out.add(i);
    }
    return out;
  }

  NavBarConfig copyWith({
    bool? enabled,
    double? barSize,
    double? barWidth,
    double? position,
    double? cornerRoundness,
    double? opacity,
    double? blurSigma,
    double? refractionDepth,
    double? refractionStrength,
    double? chromaticSplit,
    bool? useIOSGlassMode,
    bool? holdToSwipe,
    double? swipeSensitivity,
    bool? invertSwipe,
    List<int>? order,
  }) => NavBarConfig(
    enabled: enabled ?? this.enabled,
    barSize: barSize ?? this.barSize,
    barWidth: barWidth ?? this.barWidth,
    position: position ?? this.position,
    cornerRoundness: cornerRoundness ?? this.cornerRoundness,
    opacity: opacity ?? this.opacity,
    blurSigma: blurSigma ?? this.blurSigma,
    refractionDepth: refractionDepth ?? this.refractionDepth,
    refractionStrength: refractionStrength ?? this.refractionStrength,
    chromaticSplit: chromaticSplit ?? this.chromaticSplit,
    useIOSGlassMode: useIOSGlassMode ?? this.useIOSGlassMode,
    holdToSwipe: holdToSwipe ?? this.holdToSwipe,
    swipeSensitivity: swipeSensitivity ?? this.swipeSensitivity,
    invertSwipe: invertSwipe ?? this.invertSwipe,
    order: order ?? this.order,
  );

  /// Resets the glass look without touching layout, gestures or tab order.
  NavBarConfig resetGlassEffects() => copyWith(
    opacity: const NavBarConfig().opacity,
    blurSigma: const NavBarConfig().blurSigma,
    refractionDepth: const NavBarConfig().refractionDepth,
    refractionStrength: const NavBarConfig().refractionStrength,
    chromaticSplit: const NavBarConfig().chromaticSplit,
  );

  Map<String, dynamic> toMap() => {
    'enabled': enabled,
    'barSize': barSize,
    'barWidth': barWidth,
    'position': position,
    'cornerRoundness': cornerRoundness,
    'opacity': opacity,
    'blurSigma': blurSigma,
    'refractionDepth': refractionDepth,
    'refractionStrength': refractionStrength,
    'chromaticSplit': chromaticSplit,
    'useIOSGlassMode': useIOSGlassMode,
    'holdToSwipe': holdToSwipe,
    'swipeSensitivity': swipeSensitivity,
    'invertSwipe': invertSwipe,
    'order': order,
  };

  factory NavBarConfig.fromMap(Map m) {
    const d = NavBarConfig();
    double num_(Object? v, double fallback) =>
        v is num ? v.toDouble() : fallback;
    return NavBarConfig(
      enabled: m['enabled'] as bool? ?? d.enabled,
      barSize: num_(m['barSize'], d.barSize),
      barWidth: num_(m['barWidth'], d.barWidth),
      position: num_(m['position'], d.position),
      cornerRoundness: num_(m['cornerRoundness'], d.cornerRoundness),
      opacity: num_(m['opacity'], d.opacity),
      blurSigma: num_(m['blurSigma'], d.blurSigma),
      refractionDepth: num_(m['refractionDepth'], d.refractionDepth),
      refractionStrength: num_(m['refractionStrength'], d.refractionStrength),
      chromaticSplit: num_(m['chromaticSplit'], d.chromaticSplit),
      useIOSGlassMode: m['useIOSGlassMode'] as bool? ?? d.useIOSGlassMode,
      holdToSwipe: m['holdToSwipe'] as bool? ?? d.holdToSwipe,
      swipeSensitivity: num_(m['swipeSensitivity'], d.swipeSensitivity),
      invertSwipe: m['invertSwipe'] as bool? ?? d.invertSwipe,
      order: (m['order'] as List?)?.whereType<num>().map((e) => e.toInt()).toList() ??
          const [],
    );
  }
}

const _uuid = Uuid();

// ─────────────────────────────────────────────────────────────────────────────
// MODELS
// ─────────────────────────────────────────────────────────────────────────────

class ClipEntry {
  final String id;
  final String rawText;
  final String processedMarkdown;
  final DateTime timestamp;
  final bool isPinned;
  final bool isChecklist;

  /// A short human title ("Q3 planning notes") and a few lowercase tags,
  /// written at capture time by the model when one is loaded, by a keyword
  /// heuristic otherwise. Empty on clips kept before titles existed.
  final String title;
  final List<String> tags;

  ClipEntry({
    String? id,
    required this.rawText,
    required this.processedMarkdown,
    DateTime? timestamp,
    this.isPinned = false,
    this.isChecklist = false,
    this.title = '',
    List<String>? tags,
  })  : id = id ?? _uuid.v4(),
        timestamp = timestamp ?? DateTime.now(),
        tags = tags ?? const [];

  Map<String, dynamic> toMap() => {
        'id': id,
        'rawText': rawText,
        'processedMarkdown': processedMarkdown,
        'timestamp': timestamp.toIso8601String(),
        'isPinned': isPinned,
        'isChecklist': isChecklist,
        if (title.isNotEmpty) 'title': title,
        if (tags.isNotEmpty) 'tags': tags,
      };

  factory ClipEntry.fromMap(Map<String, dynamic> m) => ClipEntry(
        id: m['id'],
        rawText: m['rawText'] ?? '',
        processedMarkdown: m['processedMarkdown'] ?? '',
        timestamp: DateTime.parse(m['timestamp']),
        isPinned: m['isPinned'] ?? false,
        isChecklist: m['isChecklist'] ?? false,
        title: (m['title'] as String?) ?? '',
        tags: (m['tags'] as List?)?.whereType<String>().toList() ?? const [],
      );

  ClipEntry copyWith({
    String? rawText,
    String? processedMarkdown,
    bool? isPinned,
    bool? isChecklist,
    String? title,
    List<String>? tags,
  }) =>
      ClipEntry(
        id: id,
        rawText: rawText ?? this.rawText,
        processedMarkdown: processedMarkdown ?? this.processedMarkdown,
        timestamp: timestamp,
        isPinned: isPinned ?? this.isPinned,
        isChecklist: isChecklist ?? this.isChecklist,
        title: title ?? this.title,
        tags: tags ?? this.tags,
      );
}

/// The rule drawn between clips when they are joined into one.
const String kClipJoinSeparator = '\n\n---\n\n';

/// What one redaction run caught: the masked text and how many secrets it held.
typedef RedactionResult = ({String text, int count});

/// One recognisable secret shape and the label its mask carries.
class _SecretPattern {
  const _SecretPattern(this.pattern, this.label);
  final RegExp pattern;
  final String label;
}

/// Masks API keys, tokens, passwords and private keys in [text], replacing
/// each with a labelled `[redacted:kind]` marker and reporting how many were
/// caught. Pure regex, no model needed, so it works the same offline on every
/// platform — and running it twice is a no-op, because a marker is never a
/// secret worth masking again.
RedactionResult redactSecrets(String text) {
  var out = text;
  var count = 0;

  String mark(String label) => '[redacted:$label]';

  final fixed = <_SecretPattern>[
    _SecretPattern(RegExp(r'sk-ant-[A-Za-z0-9_-]{10,}'), 'anthropic-key'),
    _SecretPattern(RegExp(r'sk-proj-[A-Za-z0-9_-]{20,}'), 'openai-key'),
    _SecretPattern(RegExp(r'sk-[A-Za-z0-9_-]{20,}'), 'openai-key'),
    _SecretPattern(
      RegExp(r'(?:gh[pousr]_[A-Za-z0-9]{36,}|github_pat_[A-Za-z0-9_]{20,})'),
      'github-token',
    ),
    _SecretPattern(RegExp(r'AKIA[0-9A-Z]{16}'), 'aws-access-key'),
    _SecretPattern(RegExp(r'AIza[0-9A-Za-z\-_]{35}'), 'google-api-key'),
    _SecretPattern(RegExp(r'xox[bpas]-[A-Za-z0-9-]{10,}'), 'slack-token'),
    _SecretPattern(
      RegExp(r'(?:sk_live|rk_live)_[A-Za-z0-9]{16,}'),
      'stripe-secret-key',
    ),
    _SecretPattern(
      RegExp(r'Bearer\s+[A-Za-z0-9\-._~+/]+=*'),
      'bearer-token',
    ),
  ];
  for (final p in fixed) {
    out = out.replaceAllMapped(p.pattern, (m) {
      count++;
      return mark(p.label);
    });
  }

  // JWTs: three base64url parts. Trailing prose punctuation (a sentence ending
  // in a token) is not part of the token, so it is hung back on afterwards.
  out = out.replaceAllMapped(
    RegExp(r'eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}'),
    (m) {
      var token = m.group(0)!;
      var tail = '';
      while (token.isNotEmpty && '.,;:!?)]}\'"'.contains(token[token.length - 1])) {
        tail = token[token.length - 1] + tail;
        token = token.substring(0, token.length - 1);
      }
      count++;
      return mark('jwt') + tail;
    },
  );

  // PEM private key blocks, whatever fits between the fences.
  out = out.replaceAllMapped(
    RegExp(
      r'-----BEGIN [A-Z0-9 ]*PRIVATE KEY-----[\s\S]*?-----END [A-Z0-9 ]*PRIVATE KEY-----',
    ),
    (m) {
      count++;
      return mark('private-key');
    },
  );

  // Card numbers: 13-19 digits that pass the Luhn checksum. Length plus
  // checksum is what separates a card from an order id.
  out = out.replaceAllMapped(
    RegExp(r'\b(?:\d[ \-]?){13,19}\b'),
    (m) {
      final digits = m.group(0)!.replaceAll(RegExp(r'[^0-9]'), '');
      if (digits.length < 13 || digits.length > 19 || !_luhnOk(digits)) {
        return m.group(0)!;
      }
      count++;
      return mark('card-number');
    },
  );

  // `password: hunter2`, `api_key="abc"`, `clientSecret: xyz`. The whole key
  // name is kept so the marker says what was caught; a value that is already
  // a marker is left alone, which is what makes a second run a no-op.
  out = out.replaceAllMapped(
    RegExp(
      r'''([A-Za-z0-9_.\-]*?(?:api[_-]?key|secret|passwd|password|pwd|auth[_-]?token|access[_-]?token|client[_-]?secret)[A-Za-z0-9_.\-]*)\s*[:=]\s*(["']?)([^\s"'`,;]+)\2''',
      caseSensitive: false,
    ),
    (m) {
      final key = m.group(1)!;
      final quote = m.group(2)!;
      final value = m.group(3)!;
      if (value.startsWith('[redacted:')) return m.group(0)!;
      count++;
      return '$key=$quote${mark(key.toLowerCase())}$quote';
    },
  );

  return (text: out, count: count);
}

/// The Luhn checksum, shared with card masking: true for digit strings whose
/// digits sum right, which real card numbers do and order ids usually don't.
bool _luhnOk(String digits) {
  if (digits.isEmpty) return false;
  var sum = 0;
  var twice = false;
  for (var i = digits.length - 1; i >= 0; i--) {
    var d = digits.codeUnitAt(i) - 0x30;
    if (d < 0 || d > 9) return false;
    if (twice) {
      d *= 2;
      if (d > 9) d -= 9;
    }
    sum += d;
    twice = !twice;
  }
  return sum % 10 == 0;
}

/// Joins [clips] into a single new clip, oldest first so the result reads in
/// the order the clips were captured. Each side prefers its own body and falls
/// back to the other when that one is empty, so a clip that came through the
/// formatter untouched (or a voice note with no separate original) still lands
/// whole. The joined clip is a checklist only when every source was one.
ClipEntry joinClips(Iterable<ClipEntry> clips) {
  final ordered = clips.toList()
    ..sort((a, b) {
      final byTime = a.timestamp.compareTo(b.timestamp);
      return byTime != 0 ? byTime : a.id.compareTo(b.id);
    });
  String bodyOf(String primary, String fallback) {
    final p = primary.trim();
    return p.isEmpty ? fallback.trim() : p;
  }

  final raws = <String>[];
  final formatteds = <String>[];
  for (final e in ordered) {
    final raw = bodyOf(e.rawText, e.processedMarkdown);
    final formatted = bodyOf(e.processedMarkdown, e.rawText);
    if (raw.isNotEmpty) raws.add(raw);
    if (formatted.isNotEmpty) formatteds.add(formatted);
  }
  return ClipEntry(
    rawText: raws.join(kClipJoinSeparator),
    processedMarkdown: formatteds.join(kClipJoinSeparator),
    isChecklist: ordered.isNotEmpty && ordered.every((e) => e.isChecklist),
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// SMART CAPTURE — similarity, ranking, titles, retention, auto-redact
// ─────────────────────────────────────────────────────────────────────────────

/// Words that carry no meaning for search or tags. The desktop build carries
/// the same list, so both ends agree on what a tag can be.
const List<String> kStopwords = [
  'a', 'an', 'the', 'and', 'or', 'but', 'of', 'to', 'in', 'on', 'for',
  'with', 'is', 'are', 'was', 'were', 'be', 'been', 'this', 'that',
  'these', 'those', 'it', 'its', 'as', 'at', 'by', 'from', 'into',
  'over', 'after', 'before', 'about', 'between', 'through', 'during',
  'will', 'would', 'can', 'could', 'should', 'has', 'have', 'had',
  'not', 'no', 'you', 'your', 'we', 'our', 'they', 'their', 'he',
  'she', 'him', 'her', 'his', 'them', 'then', 'than', 'too', 'very',
  'just', 'also', 'here', 'there', 'when', 'where', 'which', 'who',
  'what', 'how', 'all', 'any', 'both', 'each', 'few', 'more', 'most',
  'other', 'some', 'such', 'only', 'own', 'same', 'so', 'now',
];

/// Lowercase alphanumeric tokens of [text], stopwords dropped. The shared
/// vocabulary behind similarity, ranking and heuristic tags.
List<String> clipWords(String text, {int minLength = 2}) {
  return RegExp(r'[a-z0-9]+')
      .allMatches(text.toLowerCase())
      .map((m) => m.group(0)!)
      .where((w) => w.length >= minLength && !kStopwords.contains(w))
      .toList();
}

String _normalizeClip(String t) =>
    t.toLowerCase().replaceAll(RegExp(r'\s+'), ' ').trim();

/// Jaccard similarity of two texts' word sets: 1 for the same words, 0 for
/// none shared. Normalized-equal texts short-circuit to 1.
double clipSimilarity(String a, String b) {
  final na = _normalizeClip(a);
  final nb = _normalizeClip(b);
  if (na == nb) return 1;
  final sa = clipWords(na).toSet();
  final sb = clipWords(nb).toSet();
  if (sa.isEmpty || sb.isEmpty) return 0;
  final overlap = sa.intersection(sb).length;
  return overlap / sa.union(sb).length;
}

/// The existing clip most like [text], or null. Normalized equality always
/// wins; otherwise the best Jaccard match above [threshold] does, ignoring
/// stubs too short to judge.
ClipEntry? findSimilarClip(
  String text,
  Iterable<ClipEntry> clips, {
  double threshold = 0.85,
}) {
  final norm = _normalizeClip(text);
  if (norm.isEmpty) return null;
  ClipEntry? best;
  var bestScore = 0.0;
  for (final c in clips) {
    final cnorm = _normalizeClip(c.rawText);
    if (cnorm.isEmpty) continue;
    if (cnorm == norm) return c;
    if (norm.length < 20 || cnorm.length < 20) continue;
    final s = clipSimilarity(norm, cnorm);
    if (s >= threshold && s > bestScore) {
      bestScore = s;
      best = c;
    }
  }
  return best;
}

/// Rarity of each query term across the corpus: a word one clip in fifty uses
/// outranks one every other clip does.
Map<String, double> _clipIdf(List<ClipEntry> clips, List<String> terms) {
  final idf = <String, double>{};
  for (final t in terms.toSet()) {
    var df = 0;
    for (final c in clips) {
      final hay = '${c.title} ${c.tags.join(' ')} '
          '${c.processedMarkdown} ${c.rawText}'.toLowerCase();
      if (hay.contains(t)) df++;
    }
    idf[t] = 1 + (clips.length + 1) / (1 + df);
  }
  return idf;
}

double _rankClip(ClipEntry c, List<String> terms, Map<String, double> idf) {
  var score = 0.0;
  final title = c.title.toLowerCase();
  final tags = c.tags.join(' ').toLowerCase();
  final formatted = c.processedMarkdown.toLowerCase();
  final raw = c.rawText.toLowerCase();
  for (final t in terms) {
    final w = idf[t] ?? 1;
    if (title.contains(t)) score += 4 * w;
    if (tags.contains(t)) score += 3 * w;
    if (formatted.contains(t)) score += 2 * w;
    if (raw.contains(t)) score += 1 * w;
  }
  return score;
}

/// The clips matching [query], best first: title hits beat tag hits beat body
/// hits, rare words beat common ones, and recency breaks ties. Empty query
/// keeps the feed's own order.
List<ClipEntry> rankClips(String query, List<ClipEntry> clips) {
  final terms = clipWords(query).toSet().toList();
  if (terms.isEmpty) return clips.toList();
  final idf = _clipIdf(clips, terms);
  final scored = <ClipEntry, double>{};
  for (final c in clips) {
    final s = _rankClip(c, terms, idf);
    if (s > 0) scored[c] = s;
  }
  final out = scored.keys.toList()
    ..sort((a, b) {
      final cmp = scored[b]!.compareTo(scored[a]!);
      return cmp != 0 ? cmp : b.timestamp.compareTo(a.timestamp);
    });
  return out;
}

/// A title and tags for [text]: the model writes them when one is loaded,
/// otherwise the first line and the most frequent meaningful words do. Never
/// throws; the worst case is a plain first-line title with no tags.
Future<({String title, List<String> tags})> titleAndTags(
  String text,
  OllamaClipProcessor processor,
) async {
  if (processor.modelLoaded) {
    try {
      final answer = await processor.instruct(
        'Give this note a title of at most 6 words and up to 3 lowercase '
        'single-word tags, comma separated. Reply with exactly two lines:\n'
        'TITLE: <title>\nTAGS: <tag>, <tag>\n\n$text',
        maxTokens: 120,
      );
      if (answer != null) {
        final parsed = parseTitleTags(answer);
        if (parsed.title.isNotEmpty) return parsed;
      }
    } catch (_) {
      // Fall through to the heuristic.
    }
  }
  return _heuristicTitleTags(text);
}

/// Reads a `TITLE:` / `TAGS:` model answer into a title and tags. Public so
/// the contract is pinned by tests rather than by the model behind it.
({String title, List<String> tags}) parseTitleTags(String answer) {
  var title = '';
  var tags = <String>[];
  for (final line in answer.split('\n')) {
    final t = line.trim();
    if (t.toUpperCase().startsWith('TITLE:')) {
      title = t.substring(6).trim();
    } else if (t.toUpperCase().startsWith('TAGS:')) {
      tags = t
          .substring(5)
          .split(',')
          .map((s) => s.trim().toLowerCase().replaceAll(RegExp(r'[^a-z0-9]'), ''))
          .where((s) => s.isNotEmpty)
          .take(3)
          .toList();
    }
  }
  if (title.length > 60) title = '${title.substring(0, 57).trim()}…';
  return (title: title, tags: tags);
}

({String title, List<String> tags}) _heuristicTitleTags(String text) {
  final first = text
      .split('\n')
      .map((l) => l.trim().replaceAll(RegExp(r'^#+\s*'), ''))
      .firstWhere((l) => l.isNotEmpty, orElse: () => '');
  final title =
      first.length > 48 ? '${first.substring(0, 45).trim()}…' : first;
  final freq = <String, int>{};
  final order = <String>[];
  for (final w in clipWords(text, minLength: 4)) {
    freq[w] = (freq[w] ?? 0) + 1;
    if (!order.contains(w)) order.add(w);
  }
  final ranked = order.toList()
    ..sort((a, b) {
      final cmp = freq[b]!.compareTo(freq[a]!);
      return cmp != 0 ? cmp : order.indexOf(a).compareTo(order.indexOf(b));
    });
  return (title: title, tags: ranked.take(3).toList());
}

/// Deletes clips older than the retention setting and reports how many went.
/// Zero or missing `clipRetentionDays` keeps everything, forever.
Future<int> purgeExpiredClips() async {
  if (!Hive.isBoxOpen(AppDefaults.hiveSettingsBox) ||
      !Hive.isBoxOpen(AppDefaults.hiveClipBox)) {
    return 0;
  }
  final days =
      Hive.box(AppDefaults.hiveSettingsBox).get('clipRetentionDays') as int? ??
          0;
  if (days <= 0) return 0;
  final cutoff = DateTime.now().subtract(Duration(days: days));
  final box = Hive.box(AppDefaults.hiveClipBox);
  final dead = <dynamic>[];
  for (final key in box.keys) {
    if (key == 'lastRaw') continue;
    final val = box.get(key);
    if (val is! Map) continue;
    try {
      final entry =
          ClipEntry.fromMap(Map<String, dynamic>.from(val as Map));
      // Pinned clips are never the ones dropped: pinning is the user saying
      // keep this, and a lifetime setting must not overrule it.
      if (!entry.isPinned && entry.timestamp.isBefore(cutoff)) {
        dead.add(key);
      }
    } catch (_) {
      // An unreadable entry is not ours to burn.
    }
  }
  for (final key in dead) {
    await box.delete(key);
  }
  return dead.length;
}

/// Runs the secret masks over [text] when the auto-redact setting is on.
/// Safe to call anywhere, including tests without Hive: no box, no-op.
String applyAutoRedact(String text) {
  if (!Hive.isBoxOpen(AppDefaults.hiveSettingsBox)) return text;
  final on =
      Hive.box(AppDefaults.hiveSettingsBox).get('autoRedact') == true;
  return on ? redactSecrets(text).text : text;
}

/// Builds a clip the way every capture path should: secrets masked first so
/// they never reach the box, then formatted, then titled and tagged.
Future<ClipEntry> buildClipEntry(
  String rawText,
  OllamaClipProcessor processor,
) async {
  final clean = applyAutoRedact(rawText);
  final processed = await processor.process(clean);
  final meta = await titleAndTags(clean, processor);
  return ClipEntry(
    rawText: clean,
    processedMarkdown: processed,
    title: meta.title,
    tags: meta.tags,
  );
}

/// A capture pushed at the app from the outside (share sheet, quick tile),
/// for the dashboard to swallow on its next frame.
final ValueNotifier<bool> captureRequested = ValueNotifier<bool>(false);

/// Whether an app PIN is set. The PIN itself lives in the encrypted settings
/// box, so it rests under the same AES-256 key as the clips it guards.
bool appPinSet() {
  if (!Hive.isBoxOpen(AppDefaults.hiveSettingsBox)) return false;
  final pin = Hive.box(AppDefaults.hiveSettingsBox).get('appPin');
  return pin is String && pin.isNotEmpty;
}

class Note {
  final String id;
  String title;
  String content;
  List<String> tags;
  bool isPinned;
  DateTime createdAt;
  DateTime updatedAt;

  Note({
    String? id,
    this.title = '',
    this.content = '',
    List<String>? tags,
    this.isPinned = false,
    DateTime? createdAt,
    DateTime? updatedAt,
  })  : id = id ?? _uuid.v4(),
        tags = tags ?? [],
        createdAt = createdAt ?? DateTime.now(),
        updatedAt = updatedAt ?? DateTime.now();

  Map<String, dynamic> toMap() => {
        'id': id,
        'title': title,
        'content': content,
        'tags': tags,
        'isPinned': isPinned,
        'createdAt': createdAt.toIso8601String(),
        'updatedAt': updatedAt.toIso8601String(),
      };

  factory Note.fromMap(Map<String, dynamic> m) => Note(
        id: m['id'],
        title: m['title'] ?? '',
        content: m['content'] ?? '',
        tags: List<String>.from(m['tags'] ?? []),
        isPinned: m['isPinned'] ?? false,
        createdAt: DateTime.parse(m['createdAt']),
        updatedAt: DateTime.parse(m['updatedAt']),
      );
}

// ─────────────────────────────────────────────────────────────────────────────
// OLLAMA CLIP PROCESSOR  – REST + Regex Fallback
// ─────────────────────────────────────────────────────────────────────────────

class OllamaClipProcessor {
  final OnDeviceLlmService _llm;
  String _activeModel = AppDefaults.defaultModel;
  bool _modelLoaded = false;
  String? _loadedModelPath;

  OllamaClipProcessor({OnDeviceLlmService? llm})
      : _llm = llm ?? OnDeviceLlmService();

  // ── Public getters ────────────────────────────────────────────────────
  String get activeModel => _activeModel;
  set activeModel(String v) => _activeModel = v;
  bool get ollamaAvailable => _modelLoaded;
  bool get modelLoaded => _modelLoaded;
  String? get loadedModelPath => _loadedModelPath;
  OnDeviceLlmService get llm => _llm;

  // ── Model Lifecycle ─────────────────────────────────────────────────

  /// Loads a GGUF model file for on-device inference.
  Future<bool> loadModel(String modelPath, {String? modelId}) async {
    try {
      await _llm.loadModel(modelPath, modelId: modelId);
      _modelLoaded = true;
      _loadedModelPath = modelPath;
      if (modelId != null) _activeModel = modelId;
      return true;
    } catch (e) {
      _modelLoaded = false;
      return false;
    }
  }

  /// Unloads the current model and frees memory.
  Future<void> unloadModel() async {
    await _llm.disposeEngine();
    _modelLoaded = false;
    _loadedModelPath = null;
  }

  /// Checks health — returns true if a model is loaded and ready.
  Future<bool> checkHealth() async {
    _modelLoaded = _llm.isLoaded;
    return _modelLoaded;
  }

  /// Fetch list of downloaded model files.
  Future<List<String>> fetchModels() async {
    final files = await _llm.listDownloadedModels();
    return files.map((f) => f.path).toList();
  }

  /// Delete a downloaded model file.
  Future<void> deleteModel(String path) async {
    await _llm.deleteModel(path);
    if (_loadedModelPath == path) {
      _modelLoaded = false;
      _loadedModelPath = null;
    }
  }

  /// List downloaded models.
  Future<List<FileSystemEntity>> listDownloadedModels() async {
    return _llm.listDownloadedModels();
  }

  // ── Main Processing Pipeline ──────────────────────────────────────────

  /// Main processing entry — uses on-device llama.cpp, falls back to regex.
  Future<String> process(String rawText) async {
    if (rawText.trim().isEmpty) return '';

    if (_modelLoaded) {
      try {
        return await _processWithLlm(rawText);
      } catch (_) {
        // Fall through to regex
      }
    }
    return _processWithRegex(rawText);
  }

  /// Runs an arbitrary instruction through the loaded model, without the
  /// clipboard-to-markdown wrapper that [process] applies. Returns null when no
  /// model is loaded or the model returns nothing, so a caller can say so
  /// rather than presenting regex-reformatted text as if it were an answer.
  Future<String?> instruct(String prompt, {int maxTokens = 640}) async {
    if (!_modelLoaded || prompt.trim().isEmpty) return null;
    try {
      final result = await _llm.generate(
        prompt,
        maxTokens: maxTokens,
        temperature: 0.4,
        topP: 0.9,
      );
      final trimmed = result.trim();
      return trimmed.isEmpty ? null : trimmed;
    } catch (_) {
      return null;
    }
  }

  /// Chat-style processing with conversation history.
  Future<String> processViaChat(String rawText, {List<ChatMessage>? history}) async {
    if (rawText.trim().isEmpty) return '';

    if (_modelLoaded) {
      try {
        final messages = history ?? [];
        messages.add(ChatMessage(role: 'system', content: _systemPrompt));
        messages.add(ChatMessage(role: 'user', content: _buildPrompt(rawText)));

        final buffer = StringBuffer();
        await for (final token in _llm.chatStream(
          messages,
          maxTokens: 512,
          temperature: 0.3,
          topP: 0.9,
        )) {
          buffer.write(token);
        }
        final content = buffer.toString().trim();
        if (content.isNotEmpty) return content;
      } catch (_) {}
    }
    return _processWithRegex(rawText);
  }

  /// Streaming generate (returns token-by-token)
  Stream<String> processStream(String rawText) async* {
    if (rawText.trim().isEmpty) return;

    if (_modelLoaded) {
      try {
        final prompt = _buildPrompt(rawText);
        await for (final token in _llm.generateStream(
          prompt,
          maxTokens: 512,
          temperature: 0.3,
          topP: 0.9,
        )) {
          yield token;
        }
        return;
      } catch (_) {}
    }
    yield _processWithRegex(rawText);
  }

  // ── On-Device LLM Integration ────────────────────────────────────────

  Future<String> _processWithLlm(String rawText) async {
    final prompt = _buildPrompt(rawText);
    final result = await _llm.generate(
      prompt,
      maxTokens: 512,
      temperature: 0.3,
      topP: 0.9,
    );
    if (result.trim().isNotEmpty) {
      return result.trim();
    }
    return _processWithRegex(rawText);
  }

  static const _systemPrompt = 'You are a clipboard text processor. Convert raw copied text into clean, structured Markdown.';

  String _buildPrompt(String rawText) {
    return '''Convert the following raw copied text into clean, structured Markdown.
Rules:
- Use bullet points or numbered lists where appropriate
- Detect and preserve URLs as clickable markdown links
- Detect dates and format them clearly
- Detect email addresses and format as mailto links
- If the text looks like code, wrap it in code blocks
- If it contains checkboxes, convert to markdown checkboxes
- Keep the original meaning, don't add content
- Output ONLY the formatted markdown, no explanations

Raw text:
$rawText''';
  }

  /// Zero-latency regex/heuristic fallback engine
  String _processWithRegex(String rawText) {
    if (rawText.trim().isEmpty) return '';

    final buffer = StringBuffer();
    final lines = rawText.split('\n');

    for (var line in lines) {
      line = line.trimRight();
      if (line.isEmpty) {
        buffer.writeln();
        continue;
      }

      // Detect URLs and make them clickable
      line = _linkifyUrls(line);

      // Detect emails
      line = _linkifyEmails(line);

      // Detect dates (common formats)
      line = _highlightDates(line);

      // Detect checkbox patterns
      final checkboxMatch = RegExp(r'^[\s]*[-*]\s*\[([ xX])\]\s*(.*)$')
          .firstMatch(line);
      if (checkboxMatch != null) {
        final checked = checkboxMatch.group(1) != ' ';
        final text = checkboxMatch.group(2) ?? '';
        buffer.writeln('- [${checked ? 'x' : ' '}] $text');
        continue;
      }

      // Detect list items
      if (RegExp(r'^[\s]*[-*•]\s+').hasMatch(line)) {
        buffer.writeln(line);
        continue;
      }

      // Detect numbered lists
      if (RegExp(r'^[\s]*\d+[.)]\s+').hasMatch(line)) {
        buffer.writeln(line);
        continue;
      }

      // Detect headers
      if (RegExp(r'^#{1,6}\s').hasMatch(line)) {
        buffer.writeln(line);
        continue;
      }

      // Detect key-value patterns (key: value or key=value)
      final kvMatch =
          RegExp(r'^([\w\s]+?)\s*[:=]\s*(.+)$').firstMatch(line);
      if (kvMatch != null) {
        final key = kvMatch.group(1)?.trim() ?? '';
        final value = kvMatch.group(2)?.trim() ?? '';
        buffer.writeln('**$key:** $value');
        continue;
      }

      // Detect code-like patterns
      if (RegExp(r'^[\s]*(import |const |var |let |function |class |def |fn |pub )')
          .hasMatch(line)) {
        buffer.writeln('```');
        buffer.writeln(line);
        buffer.writeln('```');
        continue;
      }

      // Default: wrap as-is
      buffer.writeln(line);
    }

    return buffer.toString().trim();
  }

  String _linkifyUrls(String text) {
    // Matches http(s)://... or www... URLs, stopping at whitespace, angle brackets, or quotes
    final urlRegExp = RegExp(
      '(https?://[^ <>"' + "'" + ']+|www\.[^ <>"' + "'" + ']+)',
      caseSensitive: false,
    );
    return text.replaceAllMapped(urlRegExp, (match) {
      var url = match.group(0)!;
      // A bare sentence often ends right after the address, and a period,
      // comma, semicolon or colon is punctuation of the prose, not part of
      // it. Strip the run of trailing marks but leave something that could
      // still be a whole address, "https://x" at the shortest.
      var drop = 0;
      while (drop < url.length &&
          (url[url.length - 1 - drop] == '.' ||
              url[url.length - 1 - drop] == ',' ||
              url[url.length - 1 - drop] == ';' ||
              url[url.length - 1 - drop] == ':')) {
        drop++;
      }
      if (drop > 0 && url.length - drop >= 5) {
        url = url.substring(0, url.length - drop);
      }
      final href = url.startsWith('http') ? url : 'https://$url';
      return '[$url]($href)';
    });
  }

  String _linkifyEmails(String text) {
    final emailPattern = RegExp(r'[\w.+-]+@[\w-]+\.[\w.-]+');
    return text.replaceAllMapped(emailPattern, (match) {
      final email = match.group(0)!;
      return '[$email](mailto:$email)';
    });
  }

  String _highlightDates(String text) {
    // ISO dates
    text = text.replaceAllMapped(
        RegExp(r'\b(\d{4}-\d{2}-\d{2})\b'), (m) => '📅 ${m.group(0)}');
    // US dates MM/DD/YYYY
    text = text.replaceAllMapped(
        RegExp(r'\b(\d{1,2}/\d{1,2}/\d{4})\b'), (m) => '📅 ${m.group(0)}');
    // European dates DD.MM.YYYY
    text = text.replaceAllMapped(
        RegExp(r'\b(\d{1,2}\.\d{1,2}\.\d{4})\b'), (m) => '📅 ${m.group(0)}');
    return text;
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// MAIN APP
// ─────────────────────────────────────────────────────────────────────────────

void main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // Surface any first-frame / build errors to logcat so they aren't silently
  // swallowed. Kept in release: an error here is rare and worth seeing.
  FlutterError.onError = (details) {
    debugPrint('[ClipSync] ⚠️ FlutterError: ${details.exception}');
    debugPrint('[ClipSync] ⚠️ Stack: ${details.stack}');
  };
  PlatformDispatcher.instance.onError = (error, stack) {
    debugPrint('[ClipSync] ✅ PlatformDispatcher error handled: $error');
    return true;
  };

  // Draw behind the system bars. Not awaited, and hoisted above the storage
  // work so the window is being configured while Hive is still on disk. Icon
  // brightness is set per-theme by the AnnotatedRegion in ClipSyncApp, not
  // here, so light mode gets dark icons.
  SystemChrome.setEnabledSystemUIMode(
    SystemUiMode.edgeToEdge,
    overlays: [SystemUiOverlay.top, SystemUiOverlay.bottom],
  );

  // Initialize Hive with AES-256 encryption. This has to finish before the
  // tree is built, because the first screen reads its boxes synchronously.
  try {
    final appDir = await getApplicationDocumentsDirectory();
    await Hive.initFlutter(appDir.path);
    await SecureStorageService().openAllBoxes();
    _bootLog('Hive boxes opened');
  } catch (e, st) {
    debugPrint('[ClipSync] Hive init FAILED: $e');
    debugPrint('[ClipSync] Stack: $st');
    // Fallback: open plain boxes so the app still works
    try {
      const names = ['clip_history', 'notes', 'settings', 'chat_sessions'];
      await Future.wait<void>([
        for (final name in names)
          if (!Hive.isBoxOpen(name)) Hive.openBox(name),
      ]);
      _bootLog('Fallback plain boxes opened');
    } catch (e2) {
      debugPrint('[ClipSync] Fallback FAILED: $e2');
    }
  }

  runApp(const ClipSyncApp());
}

/// Startup chatter. Gated on [kDebugMode] rather than routed through
/// [debugPrint], which still writes to logcat in a release build; a compile
/// time false lets the whole call be tree shaken out of the launch path.
void _bootLog(String message) {
  if (kDebugMode) debugPrint('[ClipSync] $message');
}

/// Global notifier for theme changes — triggers MaterialApp rebuild.
final ValueNotifier<int> _themeRevision = ValueNotifier(0);

class ClipSyncApp extends StatelessWidget {
  const ClipSyncApp({super.key});

  @override
  Widget build(BuildContext context) {
    return ValueListenableBuilder<int>(
      valueListenable: _themeRevision,
      builder: (_, __, ___) {
        // Read theme settings from Hive
        final settings = Hive.box(AppDefaults.hiveSettingsBox);
        final modeStr = settings.get(AppDefaults.themeModeKey, defaultValue: 'dark') as String;
        final themeMode = modeStr == 'light' ? ThemeMode.light : modeStr == 'system' ? ThemeMode.system : ThemeMode.dark;
        final amoled = settings.get(AppDefaults.amoledKey, defaultValue: false) as bool;
        final accentInt = settings.get(AppDefaults.accentColorKey,
            defaultValue: kDefaultAccent.toARGB32()) as int;
        final accentColor = Color(accentInt);
        appAccentColor.value = accentColor;
        appAmoledMode.value = amoled;
        // Absent key = fresh install, which opens on Material. A stored value
        // always wins, so choosing Liquid Glass once is remembered.
        appGlassMode.value =
            settings.get(AppDefaults.glassModeKey, defaultValue: false) as bool;

    final seed = accentColor;
    // Only the AMOLED toggle reaches for true black. Everything else sits on
    // off-black so shadows and glass edges still read.
    final darkCanvas = amoled ? const Color(0xFF000000) : DarkSurface.canvas;
    final darkLow = amoled ? const Color(0xFF060607) : DarkSurface.low;

    final darkScheme = ColorScheme.fromSeed(
      seedColor: seed,
      brightness: Brightness.dark,
    ).copyWith(
      surface: darkCanvas,
      surfaceContainerLowest: darkLow,
      surfaceContainerLow: amoled ? const Color(0xFF0B0B0D) : DarkSurface.low,
      surfaceContainer: amoled ? const Color(0xFF101013) : DarkSurface.base,
      surfaceContainerHigh: amoled ? const Color(0xFF16161A) : DarkSurface.high,
      surfaceContainerHighest:
          amoled ? const Color(0xFF1D1D22) : DarkSurface.highest,
      onSurface: DarkSurface.onSurface,
      onSurfaceVariant: const Color(0xFF9C9A97),
      outline: DarkSurface.outline,
      outlineVariant: const Color(0xFF2A2A30),
      error: Semantic.danger,
      tertiary: accentCompanion(seed),
    );

    final lightScheme = ColorScheme.fromSeed(
      seedColor: seed,
      brightness: Brightness.light,
    ).copyWith(
      surface: LightSurface.canvas,
      surfaceContainerLowest: LightSurface.base,
      surfaceContainerLow: LightSurface.low,
      surfaceContainer: LightSurface.high,
      surfaceContainerHigh: LightSurface.highest,
      surfaceContainerHighest: const Color(0xFFDFDAD2),
      onSurface: LightSurface.onSurface,
      onSurfaceVariant: const Color(0xFF5F5C57),
      outline: LightSurface.outline,
      outlineVariant: const Color(0xFFE2DDD5),
      error: Semantic.danger,
      tertiary: accentCompanion(seed),
    );

    final darkTheme = ThemeData(
      useMaterial3: true,
      brightness: Brightness.dark,
      colorScheme: darkScheme,
      scaffoldBackgroundColor: Colors.transparent,
        splashFactory: InkSparkle.splashFactory,
        appBarTheme: AppBarTheme(
          backgroundColor: Colors.transparent,
          foregroundColor: darkScheme.onSurface,
          elevation: 0,
          scrolledUnderElevation: 0,
          surfaceTintColor: Colors.transparent,
          centerTitle: false,
          titleTextStyle: TextStyle(
            color: darkScheme.onSurface,
            fontSize: 20,
            fontWeight: FontWeight.w800,
            letterSpacing: -0.3,
          ),
        ),
        navigationBarTheme: const NavigationBarThemeData(
          backgroundColor: Colors.transparent,
          indicatorColor: Colors.transparent,
          elevation: 0,
          height: 0,
        ),
        cardTheme: const CardThemeData(
          color: Colors.transparent,
          elevation: 0,
          margin: EdgeInsets.zero,
        ),
        // Every context menu in the app inherits one radius, one surface and
        // one item height from here, so no two menus can look like they came
        // from different apps.
        popupMenuTheme: PopupMenuThemeData(
          color: DarkSurface.high,
          surfaceTintColor: Colors.transparent,
          elevation: 8,
          shadowColor: Colors.black.withValues(alpha: 0.44),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(Radii.inner),
            side: BorderSide(color: darkScheme.outline.withValues(alpha: 0.7)),
          ),
        ),
        // Every confirmation in the app inherits one surface, one radius and
        // one pair of type sizes from here, so a dialog can never arrive
        // looking like it was built for a different app.
        dialogTheme: DialogThemeData(
          backgroundColor: DarkSurface.high,
          surfaceTintColor: Colors.transparent,
          elevation: 10,
          shadowColor: Colors.black.withValues(alpha: 0.5),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(Radii.card),
            side: BorderSide(color: darkScheme.outline.withValues(alpha: 0.7)),
          ),
          titleTextStyle: TextStyle(
            color: darkScheme.onSurface,
            fontSize: 17.5,
            fontWeight: FontWeight.w700,
            letterSpacing: -0.35,
          ),
          contentTextStyle: TextStyle(
            color: darkScheme.onSurface.withValues(alpha: 0.70),
            fontSize: 13.5,
            height: 1.45,
            fontWeight: FontWeight.w500,
          ),
        ),
        // One toast for the whole app, and it lifts clear of the floating
        // capsule instead of arriving behind it. The words carry the meaning:
        // solid green, red and orange bars were three saturated hues turning
        // up one at a time on top of the content they were reporting on.
        snackBarTheme: SnackBarThemeData(
          backgroundColor: DarkSurface.highest,
          contentTextStyle: const TextStyle(
            color: DarkSurface.onSurface,
            fontSize: 13.5,
            fontWeight: FontWeight.w600,
            letterSpacing: -0.1,
          ),
          actionTextColor: legibleAccent(seed, DarkSurface.highest, minRatio: 3),
          behavior: SnackBarBehavior.floating,
          elevation: 6,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(Radii.inner),
            side: BorderSide(color: darkScheme.outline.withValues(alpha: 0.8)),
          ),
          insetPadding: const EdgeInsets.fromLTRB(
            Space.md,
            Space.sm,
            Space.md,
            Space.snackBar,
          ),
        ),
        inputDecorationTheme: InputDecorationTheme(
          filled: true,
          fillColor: Colors.white.withValues(alpha: 0.05),
          border: OutlineInputBorder(
            borderRadius: BorderRadius.circular(Radii.control),
            borderSide: BorderSide(color: Colors.white.withValues(alpha: 0.12)),
          ),
          enabledBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(Radii.control),
            borderSide: BorderSide(color: Colors.white.withValues(alpha: 0.12)),
          ),
          focusedBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(Radii.control),
            borderSide: BorderSide(color: darkScheme.primary, width: 1.4),
          ),
          contentPadding: const EdgeInsets.symmetric(
            horizontal: Space.lg,
            vertical: Space.lg - 2,
          ),
        ),
        dividerTheme: DividerThemeData(
          color: darkScheme.outlineVariant.withValues(alpha: 0.6),
          thickness: 0.6,
          space: Space.xl,
        ),
      );

    final lightTheme = ThemeData(
      useMaterial3: true,
      brightness: Brightness.light,
      colorScheme: lightScheme,
      scaffoldBackgroundColor: LightSurface.canvas,
      splashFactory: InkSparkle.splashFactory,
      appBarTheme: AppBarTheme(
        backgroundColor: Colors.transparent,
        foregroundColor: lightScheme.onSurface,
        elevation: 0,
        scrolledUnderElevation: 0,
        surfaceTintColor: Colors.transparent,
        centerTitle: false,
        titleTextStyle: TextStyle(
          color: lightScheme.onSurface,
          fontSize: 20,
          fontWeight: FontWeight.w800,
          letterSpacing: -0.3,
        ),
      ),
      navigationBarTheme: const NavigationBarThemeData(
        backgroundColor: Colors.transparent,
        indicatorColor: Colors.transparent,
        elevation: 0,
        height: 0,
      ),
      cardTheme: const CardThemeData(
        color: Colors.transparent,
        elevation: 0,
        margin: EdgeInsets.zero,
      ),
      popupMenuTheme: PopupMenuThemeData(
        color: LightSurface.base,
        surfaceTintColor: Colors.transparent,
        elevation: 8,
        shadowColor: Colors.black.withValues(alpha: 0.16),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(Radii.inner),
          side: const BorderSide(color: LightSurface.outline),
        ),
      ),
      dialogTheme: DialogThemeData(
        backgroundColor: LightSurface.base,
        surfaceTintColor: Colors.transparent,
        elevation: 10,
        shadowColor: Colors.black.withValues(alpha: 0.18),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(Radii.card),
          side: const BorderSide(color: LightSurface.outline),
        ),
        titleTextStyle: TextStyle(
          color: lightScheme.onSurface,
          fontSize: 17.5,
          fontWeight: FontWeight.w700,
          letterSpacing: -0.35,
        ),
        contentTextStyle: TextStyle(
          color: lightScheme.onSurface.withValues(alpha: 0.70),
          fontSize: 13.5,
          height: 1.45,
          fontWeight: FontWeight.w500,
        ),
      ),
      snackBarTheme: SnackBarThemeData(
        backgroundColor: const Color(0xFF23221F),
        contentTextStyle: const TextStyle(
          color: Color(0xFFF7F5F2),
          fontSize: 13.5,
          fontWeight: FontWeight.w600,
          letterSpacing: -0.1,
        ),
        actionTextColor:
            legibleAccent(seed, const Color(0xFF23221F), minRatio: 3),
        behavior: SnackBarBehavior.floating,
        elevation: 6,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(Radii.inner),
        ),
        insetPadding: const EdgeInsets.fromLTRB(
          Space.md,
          Space.sm,
          Space.md,
          Space.snackBar,
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: LightSurface.base,
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(Radii.control),
          borderSide: const BorderSide(color: LightSurface.outline),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(Radii.control),
          borderSide: const BorderSide(color: LightSurface.outline),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(Radii.control),
          borderSide: BorderSide(color: lightScheme.primary, width: 1.4),
        ),
        contentPadding: const EdgeInsets.symmetric(
          horizontal: Space.lg,
          vertical: Space.lg - 2,
        ),
      ),
      dividerTheme: const DividerThemeData(
        color: Color(0xFFE2DDD5),
        thickness: 0.6,
        space: Space.xl,
      ),
    );

    // First launch? Show onboarding (tutorial); otherwise app.
    final onboardingDone =
        settings.get('onboardingCompleted', defaultValue: false) as bool;

    return MaterialApp(
      title: 'ClipSyncAI',
      debugShowCheckedModeBanner: false,
      themeMode: themeMode,
      theme: lightTheme,
      darkTheme: darkTheme,
      initialRoute: onboardingDone ? 'app' : 'onboarding',
      // Status and navigation bar icons follow the resolved theme. Without
      // this the light theme inherits light-on-light icons and the clock
      // disappears.
      builder: (context, child) {
        final dark = Theme.of(context).brightness == Brightness.dark;
        return AnnotatedRegion<SystemUiOverlayStyle>(
          value: SystemUiOverlayStyle(
            statusBarColor: Colors.transparent,
            statusBarBrightness: dark ? Brightness.dark : Brightness.light,
            statusBarIconBrightness: dark ? Brightness.light : Brightness.dark,
            systemNavigationBarColor: Colors.transparent,
            systemNavigationBarIconBrightness:
                dark ? Brightness.light : Brightness.dark,
            systemNavigationBarContrastEnforced: false,
            systemStatusBarContrastEnforced: false,
          ),
          child: child ?? const SizedBox.shrink(),
        );
      },
      routes: {
        'onboarding': (_) => const OnboardingGate(),
        'app': (_) => const AppShell(),
      },
    );
      },
    );
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// APP SHELL – Bottom Navigation & Page View
// ─────────────────────────────────────────────────────────────────────────────

/// Set by a page that has opened a sub view *in place* rather than by pushing a
/// route, for as long as that view is open.
///
/// The note editor is the case this exists for. Because it is a state flag on
/// the Notes page and not a `Navigator` route, Android's Back had nothing of its
/// own to pop and closed the whole app instead — from a screen the user was
/// typing into. Registering the way out here lets the shell's one `PopScope`
/// spend the press on the sub view first.
final ValueNotifier<VoidCallback?> appBackInterceptor = ValueNotifier(null);

class AppShell extends StatefulWidget {
  const AppShell({super.key});

  @override
  State<AppShell> createState() => _AppShellState();
}

class _AppShellState extends State<AppShell> with WidgetsBindingObserver {
  int _currentIndex = 0;
  late final PageController _pageController;

  /// True while the pager is animating to a tab that was chosen rather than
  /// swiped to. A multi page animation reports every page it passes through, so
  /// without this a jump from the first tab to the last would tick the phone
  /// four times and rebuild the shell at each stop.
  bool _tabJumpInFlight = false;

  final _processor = OllamaClipProcessor();
  final _aiConnection = AiConnectionService();
  final _imagePicker = ImagePicker();
  NavBarConfig _navConfig = NavBarConfig();

  // Header scroll state
  final ValueNotifier<double> _headerScrollOffset = ValueNotifier(0.0);

  // Theme state
  ThemeMode _themeMode = ThemeMode.dark;
  bool _amoledMode = false;
  Color _accentColor = AppColors.primaryAccent;

  // Native service MethodChannel
  static const _serviceChannel = MethodChannel('com.sync.clipsync/service');
  static const _clipChannel = MethodChannel('com.sync.clipsync/clipboard');

  /// Intents handed over by native Android: a shared text, or a quick-tile tap.
  /// Missing everywhere but Android; every call is guarded, never assumed.
  static const _shareChannel = MethodChannel('com.sync.clipsync/share');
  bool _nativeServiceRunning = false;

  /// The privacy lock. On while a PIN is set and the session is not yet
  /// unlocked: cold start, and every return from the background.
  bool _locked = false;
  bool _wasPaused = false;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    _pageController = PageController();
    _loadNavConfig();
    _loadThemeSettings();
    _loadAiConnection();
    _initNativeService();
    _locked = appPinSet();
    // Loading a multi gigabyte GGUF is the heaviest thing the app ever does,
    // and nothing on the first screen depends on it, so it waits until that
    // screen is on the glass. Kicking it off from initState put llama.cpp's
    // model load in a race with the launch frame, and the launch frame lost.
    WidgetsBinding.instance.addPostFrameCallback((_) => _initLlmEngine());
    WidgetsBinding.instance
        .addPostFrameCallback((_) => _purgeExpiredClipsStart());
  }

  /// Burns clips older than the retention setting, once per launch, and says
  /// so only when something actually went.
  Future<void> _purgeExpiredClipsStart() async {
    final n = await purgeExpiredClips();
    if (n > 0 && mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Cleared ${plural(n, 'expired clip')}'),
          behavior: SnackBarBehavior.floating,
        ),
      );
    }
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.paused) {
      _wasPaused = true;
      return;
    }
    if (state == AppLifecycleState.resumed) {
      if (_wasPaused && appPinSet()) {
        setState(() => _locked = true);
      }
      _wasPaused = false;
      _pollExternalCapture();
    }
  }

  /// Picks up what native Android parked while the app was away: a shared text
  /// to file, or a quick-tile tap asking for a capture. No channel, no work.
  Future<void> _pollExternalCapture() async {
    try {
      final shared = await _shareChannel.invokeMethod<String>('getSharedText');
      if (shared != null && shared.trim().isNotEmpty) {
        await _shareChannel.invokeMethod('clearSharedText');
        final entry = await buildClipEntry(shared.trim(), _processor);
        await Hive.box(AppDefaults.hiveClipBox).put(entry.id, entry.toMap());
        if (!mounted) return;
        _goToTab(0);
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Saved from share'),
            behavior: SnackBarBehavior.floating,
          ),
        );
      }
      final tile = await _shareChannel.invokeMethod<String>('getTileAction');
      if (tile == 'capture') {
        await _shareChannel.invokeMethod('clearTileAction');
        if (!mounted) return;
        _goToTab(0);
        captureRequested.value = true;
      }
    } on MissingPluginException catch (_) {
      // Desktop builds and older installs have no share channel.
    } catch (_) {}
  }

  Future<void> _loadAiConnection() async {
    await _aiConnection.load();
    if (mounted) setState(() {});
  }

  void _loadNavConfig() {
    final settings = Hive.box(AppDefaults.hiveSettingsBox);
    final saved = settings.get('navBarConfig');
    if (saved != null && saved is Map) {
      _navConfig = NavBarConfig.fromMap(Map<String, dynamic>.from(saved));
    }
  }

  void _loadThemeSettings() {
    final settings = Hive.box(AppDefaults.hiveSettingsBox);
    final mode = settings.get(AppDefaults.themeModeKey, defaultValue: 'dark') as String;
    _themeMode = mode == 'light' ? ThemeMode.light : mode == 'system' ? ThemeMode.system : ThemeMode.dark;
    _amoledMode = settings.get(AppDefaults.amoledKey, defaultValue: false) as bool;
    appAmoledMode.value = _amoledMode;
    appGlassMode.value = settings.get(AppDefaults.glassModeKey, defaultValue: false) as bool;
    final accentInt = settings.get(AppDefaults.accentColorKey,
        defaultValue: kDefaultAccent.toARGB32()) as int;
    _accentColor = Color(accentInt);
    appAccentColor.value = _accentColor;
  }

  void _saveNavConfig() {
    final settings = Hive.box(AppDefaults.hiveSettingsBox);
    settings.put('navBarConfig', _navConfig.toMap());
  }

  Future<void> _initLlmEngine() async {
    final settings = Hive.box(AppDefaults.hiveSettingsBox);
    final savedModel = settings.get('activeModel', defaultValue: AppDefaults.defaultModel);
    _processor.activeModel = savedModel;

    // Auto-reload the last used model so the engine is ready on launch.
    final lastPath = settings.get(AppDefaults.lastModelPathKey);
    if (lastPath is String && lastPath.isNotEmpty) {
      try {
        if (await File(lastPath).exists()) {
          await _processor.loadModel(lastPath);
        }
      } catch (e) {
        debugPrint('[ClipSync] Auto-reload model failed: $e');
      }
    } else {
      await _processor.checkHealth();
    }
    if (mounted) setState(() {});
  }

  Future<void> _initNativeService() async {
    // Set up listener for clipboard events from native service
    _clipChannel.setMethodCallHandler((call) async {
      if (call.method == 'onClipboardChanged') {
        final text = call.arguments['text'] as String? ?? '';
        if (text.isNotEmpty) {
          await _processIncomingClipboard(text);
        }
      }
    });

    // Request notification permission (Android 13+)
    try {
      await _serviceChannel.invokeMethod('requestNotificationPermission');
    } catch (_) {}

    // Check if service is already running
    try {
      final running = await _serviceChannel.invokeMethod<bool>('isServiceRunning');
      _nativeServiceRunning = running ?? false;
    } catch (_) {}

    // Start the native foreground service
    final settings = Hive.box(AppDefaults.hiveSettingsBox);
    final autoStart = settings.get('bgServiceEnabled', defaultValue: true);
    if (autoStart) {
      await startNativeService();
    }

    if (mounted) setState(() {});
  }

  Future<void> startNativeService() async {
    try {
      await _serviceChannel.invokeMethod('startService');
      _nativeServiceRunning = true;
    } catch (e) {
      debugPrint('Failed to start native service: $e');
    }
    if (mounted) setState(() {});
  }

  Future<void> stopNativeService() async {
    try {
      await _serviceChannel.invokeMethod('stopService');
      _nativeServiceRunning = false;
    } catch (e) {
      debugPrint('Failed to stop native service: $e');
    }
    if (mounted) setState(() {});
  }

  Future<void> _processIncomingClipboard(String text) async {
    try {
      // Skip if identical to the most recent clip (prevents duplicates from
      // repeated onClipboardChanged callbacks and avoids redundant inference).
      final box = Hive.box(AppDefaults.hiveClipBox);
      String? lastRaw;
      DateTime? lastTs;
      for (final key in box.keys) {
        if (key == 'lastRaw') continue;
        final val = box.get(key);
        if (val is! Map) continue;
        final m = Map<String, dynamic>.from(val);
        final ts = DateTime.tryParse((m['timestamp'] ?? '') as String) ?? DateTime.fromMillisecondsSinceEpoch(0);
        if (lastTs == null || ts.isAfter(lastTs)) {
          lastTs = ts;
          lastRaw = (m['rawText'] ?? '') as String;
        }
      }
      if (lastRaw == text) {
        debugPrint('Native clipboard: duplicate ignored');
        return;
      }

      // The background path files silently: no duplicate sheet from a service
      // callback, just the same redaction, formatting and titling as a tap.
      final entry = await buildClipEntry(text, _processor);
      box.put(entry.id, entry.toMap());
      debugPrint('Native clipboard intercepted and saved: ${text.length} chars');
    } catch (e, st) {
      debugPrint('Clipboard processing failed: $e\n$st');
    }
  }

  /// Whether the keyboard was up on the previous metrics report, so
  /// [didChangeMetrics] can tell a close from an open.
  bool _keyboardWasUp = false;

  /// Drops text focus when the keyboard goes away.
  ///
  /// Android's Back key hides the keyboard without routing the press to the app,
  /// so Flutter is never told and the field keeps focus. The next
  /// `Navigator.pop` — closing a clip, dismissing a sheet — restores focus to
  /// that still-focused field, and the keyboard springs back up over a page the
  /// user had finished typing on. Letting focus fall with the inset is what
  /// keeps it down.
  @override
  void didChangeMetrics() {
    if (!mounted) return;
    final up = View.of(context).viewInsets.bottom > 0;
    if (_keyboardWasUp && !up) {
      final focus = FocusManager.instance.primaryFocus;
      // Only a text field's node, so nothing else in the tree loses focus here.
      if (focus?.context?.findAncestorWidgetOfExactType<EditableText>() !=
          null) {
        focus!.unfocus();
      }
    }
    _keyboardWasUp = up;
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    _clipChannel.setMethodCallHandler(null);
    _pageController.dispose();
    _aiConnection.dispose();
    super.dispose();
  }

  /// Moves to a tab that was chosen rather than swiped to: the nav bar, or a
  /// button on a page that sends you somewhere else. The pager is driven from
  /// here so both routes animate identically and neither is mistaken for a
  /// swipe while it travels.
  void _goToTab(int i) {
    if (i == _currentIndex) return;
    setState(() => _currentIndex = i);
    _headerScrollOffset.value = 0;
    _tabJumpInFlight = true;
    _pageController
        .animateToPage(i, duration: Motion.base, curve: kGlassCurve)
        .whenComplete(() => _tabJumpInFlight = false);
  }

  @override
  Widget build(BuildContext context) {
    // Canonical tab order. The PageView always keeps this order so a
    // reordered nav bar can never move, hide or lose a feature; only the
    // strip of buttons is permuted.
    final tabLabels = kNavTabLabels;
    final tabIcons = kNavTabIcons;
    final tabSelectedIcons = kNavTabSelectedIcons;
    final navOrder = _navConfig.resolvedOrder(tabLabels.length);
    final selectedSlot = navOrder.indexOf(_currentIndex).clamp(0, navOrder.length - 1);
    // Measured *here*, above the Scaffold, on purpose. Scaffold hands its body a
    // MediaQuery with viewInsets.bottom removed (that is what
    // `resizeToAvoidBottomInset` does), so a read from inside the body is always
    // zero and the capsule would keep floating above the keyboard, on top of
    // whatever composer or field the page has put there.
    final keyboardVisible = MediaQuery.viewInsetsOf(context).bottom > 100;
    // One Back handler for the whole app, so a press is only ever spent once.
    //
    // It goes to an in-place sub view first (the note editor registers itself in
    // [appBackInterceptor]), then to the first tab, and only leaves the app from
    // there. Before this, Back on any tab quit outright — including from the
    // note editor, which looked exactly like losing what you had just typed.
    return ValueListenableBuilder<VoidCallback?>(
      valueListenable: appBackInterceptor,
      builder: (context, intercept, _) => PopScope(
        canPop: intercept == null && _currentIndex == 0,
        onPopInvokedWithResult: (didPop, _) {
          if (didPop) return;
          if (intercept != null) {
            intercept();
            return;
          }
          if (_currentIndex != 0) _goToTab(0);
        },
        child: Scaffold(
      backgroundColor: Colors.transparent,
      extendBody: true,
      // Handed down so a page's `navBarClearance` can collapse the room it
      // reserves for the capsule once the capsule has slid away.
      body: KeyboardVisibility(
        visible: keyboardVisible,
        child: SizedBox.expand(
          child: Stack(
            children: [
            const GlassBackdrop(),
            // ─── Content (header is now inside each view, scrolling with content) ──
            // No SafeArea here on purpose. Every page is handed the whole
            // screen and pads itself out of the system bars, which is the only
            // way content can scroll *under* the status bar and the floating
            // glass capsule instead of stopping at a hard edge. A page that
            // wants an inset reads it from MediaQuery.
            Positioned.fill(
              child: PageView(
                controller: _pageController,
                // Swipe sideways to change tabs. The pages keep their canonical
                // order however the nav strip is arranged, so a swipe walks the
                // five features in a fixed order and the strip's highlight
                // follows along through [selectedSlot].
                //
                // Anything with its own horizontal drag — a feed row being
                // dragged aside, the chat opener strip, a settings slider — sits
                // deeper in the hit test than this and takes the gesture first,
                // so none of them had to change to allow this.
                onPageChanged: (i) {
                  if (_tabJumpInFlight || i == _currentIndex) return;
                  HapticFeedback.selectionClick();
                  setState(() => _currentIndex = i);
                  _headerScrollOffset.value = 0;
                },
                children: [
                  DashboardView(processor: _processor, onNavigateToTab: _goToTab, scrollNotifier: _headerScrollOffset, isServiceRunning: _nativeServiceRunning, onStartService: startNativeService, onStopService: stopNativeService),
                  ChatView(llm: _processor.llm, connection: _aiConnection),
                  NotesView(processor: _processor),
                  OcrVoiceView(processor: _processor, imagePicker: _imagePicker),
                  SettingsView(
                    processor: _processor,
                    connection: _aiConnection,
                    navConfig: _navConfig,
                    isServiceRunning: _nativeServiceRunning,
                    onStartService: startNativeService,
                    onStopService: stopNativeService,
                    onLockNow: () {
                      if (appPinSet()) setState(() => _locked = true);
                    },
                    onNavConfigChanged: (NavBarConfig newCfg) {
                      setState(() => _navConfig = newCfg);
                      _saveNavConfig();
                    },
                    onThemeChanged: () {
                      setState(() {});
                      _themeRevision.value++;
                    },
                  ),
                ],
              ),
            ),
            // ─── Bottom tab bar ────────────────────────────────
            // The content fade goes first so it sits under the capsule and over
            // the page. It is skipped with the keyboard, along with the bar it
            // exists for.
            if (!keyboardVisible)
              const Positioned(
                left: 0,
                right: 0,
                bottom: 0,
                child: NavBarScrollEdge(),
              ),
            // Hidden (slides down) while the keyboard is open so it never
            // floats above the keyboard while typing.
            Positioned(
              left: 0,
              right: 0,
              bottom: 0,
              child: AnimatedSlide(
                duration: const Duration(milliseconds: 220),
                curve: Curves.easeOutCubic,
                offset: keyboardVisible
                    ? const Offset(0, 1)
                    : const Offset(0, 0),
                child: AnimatedOpacity(
                  duration: const Duration(milliseconds: 220),
                  opacity: keyboardVisible ? 0.0 : 1.0,
                  child: IgnorePointer(
                    ignoring: keyboardVisible,
                    child: SafeArea(
                      top: false,
                      child: LiquidGlassNavBar(
                        selectedIndex: selectedSlot,
                        onDestinationSelected: (slot) => _goToTab(
                          navOrder[slot],
                        ),
                        enabled: _navConfig.enabled,
                        position: _navConfig.position,
                        opacity: _navConfig.opacity,
                        blurSigma: _navConfig.blurSigma,
                        barSize: _navConfig.barSize,
                        barWidth: _navConfig.barWidth,
                        cornerRoundness: _navConfig.cornerRoundness,
                        refractionDepth: _navConfig.refractionDepth,
                        refractionStrength: _navConfig.refractionStrength,
                        chromaticSplit: _navConfig.chromaticSplit,
                        useIOSGlassMode: _navConfig.useIOSGlassMode,
                        swipeSensitivity: _navConfig.swipeSensitivity,
                        invertSwipe: _navConfig.invertSwipe,
                        holdToSwipe: _navConfig.holdToSwipe,
                        destinations: [
                          for (final i in navOrder)
                            LiquidNavDestination(
                              icon: tabIcons[i],
                              selectedIcon: tabSelectedIcons[i],
                              label: tabLabels[i],
                            ),
                        ],
                      ),
                    ),
                  ),
                ),
              ),
            ),
            // The privacy lock sits above everything, tabs included: while it
            // is up there is no feed to read and no bar to leave by.
            if (_locked)
              Positioned.fill(
                child: AppLockScreen(
                  onUnlock: () => setState(() => _locked = false),
                ),
              ),
          ],
        ),
        ),
      ),
        ),
      ),
    );
  }
}

/// The privacy lock: a PIN pad over the whole app. The PIN rests in the
/// encrypted settings box, so guessing at this screen is the only way in that
/// does not go through the device keystore first.
class AppLockScreen extends StatefulWidget {
  const AppLockScreen({required this.onUnlock, super.key});
  final VoidCallback onUnlock;

  @override
  State<AppLockScreen> createState() => _AppLockScreenState();
}

class _AppLockScreenState extends State<AppLockScreen> {
  final _pin = TextEditingController();
  String? _error;

  @override
  void dispose() {
    _pin.dispose();
    super.dispose();
  }

  void _tryUnlock() {
    final saved = Hive.box(AppDefaults.hiveSettingsBox).get('appPin');
    if (saved is String && saved.isNotEmpty && _pin.text == saved) {
      HapticFeedback.lightImpact();
      widget.onUnlock();
    } else {
      setState(() => _error = 'Wrong PIN, try again');
      _pin.clear();
    }
  }

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: BoxDecoration(color: Theme.of(context).colorScheme.surface),
      child: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(Space.xl),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Center(child: AppMark(size: 56)),
              const SizedBox(height: Space.lg),
              Text(
                'ClipSync AI',
                textAlign: TextAlign.center,
                style: AppType.title(context),
              ),
              const SizedBox(height: Space.xs),
              Text(
                'Enter your PIN to open your clips',
                textAlign: TextAlign.center,
                style: AppType.meta(context),
              ),
              const SizedBox(height: Space.xl),
              TextField(
                controller: _pin,
                obscureText: true,
                autofocus: true,
                textAlign: TextAlign.center,
                keyboardType: TextInputType.number,
                onSubmitted: (_) => _tryUnlock(),
                decoration: InputDecoration(
                  hintText: 'PIN',
                  errorText: _error,
                ),
              ),
              const SizedBox(height: Space.md),
              PrimaryAction(
                label: 'Unlock',
                icon: Icons.lock_open_rounded,
                onPressed: _tryUnlock,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// DASHBOARD VIEW
// ─────────────────────────────────────────────────────────────────────────────

class DashboardView extends StatefulWidget {
  final OllamaClipProcessor processor;
  final void Function(int)? onNavigateToTab;
  final ValueNotifier<double>? scrollNotifier;
  final bool isServiceRunning;
  final VoidCallback? onStartService;
  final VoidCallback? onStopService;
  const DashboardView({
    super.key,
    required this.processor,
    this.onNavigateToTab,
    this.scrollNotifier,
    this.isServiceRunning = false,
    this.onStartService,
    this.onStopService,
  });

  @override
  State<DashboardView> createState() => _DashboardViewState();
}

class _DashboardViewState extends State<DashboardView>
    with AutomaticKeepAliveClientMixin {
  @override
  bool get wantKeepAlive => true;
  late Box _clipBox;
  final _manualController = TextEditingController();
  final _search = TextEditingController();
  bool _isProcessing = false;

  /// Join mode: rows toggle membership in [_selectedIds] instead of opening,
  /// and the join bar under the feed header commits the merge.
  bool _selecting = false;
  final Set<String> _selectedIds = <String>{};

  @override
  void initState() {
    super.initState();
    _clipBox = Hive.box(AppDefaults.hiveClipBox);
    captureRequested.addListener(_onCaptureRequested);
  }

  @override
  void dispose() {
    captureRequested.removeListener(_onCaptureRequested);
    _manualController.dispose();
    _search.dispose();
    super.dispose();
  }

  /// A capture ordered from outside the tab (quick tile, share sheet): the
  /// same pull-to-capture the feed answers, so there is one gesture and one
  /// code path for "take what is on the clipboard now".
  Future<void> _onCaptureRequested() async {
    if (!captureRequested.value) return;
    captureRequested.value = false;
    if (!mounted) return;
    await _captureFromClipboard();
  }

  void _toggleMonitor(bool value) {
    Hive.box(AppDefaults.hiveSettingsBox).put('bgServiceEnabled', value);
    if (value) {
      widget.onStartService?.call();
    } else {
      widget.onStopService?.call();
    }
  }

  List<ClipEntry> get _entries {
    final list = <ClipEntry>[];
    for (var key in _clipBox.keys) {
      if (key == 'lastRaw') continue;
      final val = _clipBox.get(key);
      if (val is Map) {
        list.add(ClipEntry.fromMap(Map<String, dynamic>.from(val)));
      }
    }
    list.sort((a, b) {
      if (a.isPinned != b.isPinned) return a.isPinned ? -1 : 1;
      return b.timestamp.compareTo(a.timestamp);
    });
    return list;
  }

  Future<void> _processManualInput() async {
    final text = _manualController.text.trim();
    if (text.isEmpty) return;

    // Hand focus back before the work starts. Leaving it on the field keeps the
    // keyboard standing over the feed the clip just landed in — and because
    // dismissing the keyboard with Back does not clear focus, it springs open
    // again the moment any pushed route (a clip, a note) is closed.
    FocusScope.of(context).unfocus();
    setState(() => _isProcessing = true);
    try {
      final entry = await buildClipEntry(text, widget.processor);
      final saved = await _saveWithDuplicateCheck(entry);
      if (saved) _manualController.clear();
    } catch (e) {
      debugPrint('Process error: $e');
      // Say so. A silent failure here looks like a dead send button, and the
      // text is still in the field, so the retry is one tap away.
      _toast('Could not save that clip: ${e.toString().split('\n').first}');
    }
    if (mounted) setState(() => _isProcessing = false);
  }

  Future<void> _pasteFromClipboard() async {
    final data = await Clipboard.getData(Clipboard.kTextPlain);
    final text = data?.text;
    if (text == null || text.isEmpty) {
      _toast('Clipboard is empty');
      return;
    }

    if (!mounted) return;
    setState(() => _isProcessing = true);
    try {
      final entry = await buildClipEntry(text, widget.processor);
      final saved = await _saveWithDuplicateCheck(entry);
      if (mounted) _toast(saved ? 'Clip pasted and formatted' : 'Discarded');
    } catch (e) {
      debugPrint('Paste error: $e');
      _toast('Could not read the clipboard: '
          '${e.toString().split('\n').first}');
    }
    // Guarded: leaving the tab while the model is still formatting disposes
    // this state before the await returns.
    if (mounted) setState(() => _isProcessing = false);
  }

  /// What pulling the feed down does, and what the empty feed's one button does.
  /// The gesture reads as "catch me up", so it takes whatever is on the
  /// clipboard now — which is the one thing the feed can be behind on, since the
  /// monitor only sees a copy while it is running.
  ///
  /// It refuses to save the same text twice: pulling twice in a row is a normal
  /// thing to do, and two identical clips is not a normal thing to get for it.
  Future<void> _captureFromClipboard() async {
    if (_isProcessing) return;
    final data = await Clipboard.getData(Clipboard.kTextPlain);
    final text = data?.text?.trim();
    if (text == null || text.isEmpty) {
      _toast('Nothing on the clipboard');
      return;
    }
    final newest = _entries.where((e) => e.rawText.trim() == text);
    if (newest.isNotEmpty) {
      _toast('Already kept, nothing new to catch up on');
      return;
    }
    setState(() => _isProcessing = true);
    try {
      final entry = await buildClipEntry(text, widget.processor);
      final saved = await _saveWithDuplicateCheck(entry);
      if (!mounted) return;
      if (saved) _toast('Clip captured');
    } catch (e) {
      debugPrint('Pull to capture failed: $e');
      if (mounted) _toast('Could not read the clipboard');
    } finally {
      if (mounted) setState(() => _isProcessing = false);
    }
  }

  /// Files [entry], asking first when it closely resembles a kept clip.
  /// Returns true when something was filed, false on an explicit discard.
  Future<bool> _saveWithDuplicateCheck(ClipEntry entry) async {
    final existing = findSimilarClip(entry.rawText, _entries);
    if (existing == null) {
      await _clipBox.put(entry.id, entry.toMap());
      return true;
    }
    final decision = await _resolveDuplicate(existing);
    if (decision == 'discard') return false;
    if (decision == 'merge') {
      await _mergeInto(existing, entry);
      return true;
    }
    await _clipBox.put(entry.id, entry.toMap());
    return true;
  }

  /// "This looks like one you kept" — keep both, merge into the earlier one
  /// with the join machinery, or drop the newcomer. Dismissing keeps both:
  /// the safe answer to an ambiguous question is the one that loses nothing.
  Future<String> _resolveDuplicate(ClipEntry existing) async {
    final preview = existing.title.isNotEmpty
        ? existing.title
        : existing.rawText.replaceAll(RegExp(r'\s+'), ' ').trim();
    final short =
        preview.length > 80 ? '${preview.substring(0, 77).trim()}…' : preview;
    final decision = await showDialog<String>(
      context: context,
      builder: (dctx) => AlertDialog(
        title: const Text('Very similar clip kept'),
        content: Text(
          'This looks like "$short" from ${relativeTime(existing.timestamp)}.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dctx, 'discard'),
            child: const Text('Discard'),
          ),
          TextButton(
            onPressed: () => Navigator.pop(dctx, 'merge'),
            child: const Text('Merge'),
          ),
          TextButton(
            onPressed: () => Navigator.pop(dctx, 'keep'),
            child: const Text('Keep both'),
          ),
        ],
      ),
    );
    return decision ?? 'keep';
  }

  /// Folds [candidate] into [existing] with the join machinery, keeping the
  /// earlier clip's identity and title and surfacing it as new.
  Future<void> _mergeInto(ClipEntry existing, ClipEntry candidate) async {
    final joined = joinClips([existing, candidate]);
    final updated = ClipEntry(
      id: existing.id,
      rawText: joined.rawText,
      processedMarkdown: joined.processedMarkdown,
      timestamp: DateTime.now(),
      isPinned: existing.isPinned,
      isChecklist: joined.isChecklist,
      title: existing.title.isNotEmpty ? existing.title : candidate.title,
      tags: {...existing.tags, ...candidate.tags}.toList(),
    );
    await _clipBox.put(updated.id, updated.toMap());
    if (!mounted) return;
    setState(() {});
    _toast('Merged into the earlier clip');
  }

  Future<void> _syncAllClips() async {
    setState(() => _isProcessing = true);
    int count = 0;
    try {
      for (var key in _clipBox.keys) {
        if (key == 'lastRaw') continue;
        final val = _clipBox.get(key);
        if (val is Map) {
          final entry = ClipEntry.fromMap(Map<String, dynamic>.from(val));
          final reprocessed = await widget.processor.process(entry.rawText);
          _clipBox.put(entry.id, entry.copyWith(processedMarkdown: reprocessed).toMap());
          count++;
        }
      }
      _toast('Synced $count clip${count == 1 ? '' : 's'} with the current model');
    } catch (e) {
      debugPrint('Sync error: $e');
      _toast('Sync stopped after $count clip${count == 1 ? '' : 's'}');
    }
    // A whole-box reformat is the longest job on this page; the tab it lives on
    // can easily be gone by the time it finishes.
    if (mounted) setState(() => _isProcessing = false);
  }

  // Chat navigation is handled by the bottom nav bar.

  void _deleteEntry(ClipEntry entry) {
    final snapshot = entry.toMap();
    _clipBox.delete(entry.id);
    setState(() {});
    _toast('Clip deleted', undo: () {
      _clipBox.put(entry.id, snapshot);
      setState(() {});
    });
  }

  /// One-line feedback, with an optional single-step undo.
  void _toast(String message, {VoidCallback? undo}) {
    if (!mounted) return;
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(
        SnackBar(
          content: Text(message),
          duration: Duration(seconds: undo == null ? 2 : 5),
          margin: snackBarMargin(context),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(Radii.control),
          ),
          action: undo == null
              ? null
              : SnackBarAction(label: 'Undo', onPressed: undo),
        ),
      );
  }

  /// Replaces a clip's formatted view with a summary, keeping the raw text
  /// untouched so the original is never lost.
  Future<void> _summarizeClip(ClipEntry entry) async {
    if (_isProcessing) return;
    final source = entry.rawText.trim().isEmpty
        ? entry.processedMarkdown
        : entry.rawText;
    if (source.trim().isEmpty) return;
    if (!widget.processor.modelLoaded) {
      _toast('Load a model in Settings first');
      return;
    }
    setState(() => _isProcessing = true);
    final result = await widget.processor.instruct(
      'Summarize the text below in at most three short bullet points. Keep '
      'names, numbers, links and dates exact. Output only the bullets.'
      '\n\n$source',
    );
    if (!mounted) return;
    setState(() => _isProcessing = false);
    if (result == null) {
      _toast('The model did not return a summary');
      return;
    }
    final previous = entry.processedMarkdown;
    _clipBox.put(entry.id, entry.copyWith(processedMarkdown: result).toMap());
    setState(() {});
    _toast('Clip summarized', undo: () {
      _clipBox.put(
          entry.id, entry.copyWith(processedMarkdown: previous).toMap());
      setState(() {});
    });
  }

  void _togglePin(ClipEntry entry) {
    _clipBox.put(entry.id, entry.copyWith(isPinned: !entry.isPinned).toMap());
    setState(() {});
  }

  /// Masks API keys, tokens and passwords inside the clip, in the original and
  /// the formatted text alike, so the secret stops being saved or shown
  /// anywhere in the app. Needs no model: the masks are plain regex, which is
  /// also why a second tap finds nothing left to hide.
  void _redactClip(ClipEntry entry) {
    final raw = redactSecrets(entry.rawText);
    final same = entry.processedMarkdown == entry.rawText;
    final formatted = same ? raw : redactSecrets(entry.processedMarkdown);
    final n = same ? raw.count : raw.count + formatted.count;
    if (n == 0) {
      _toast('No secrets found in this clip');
      return;
    }
    final prevRaw = entry.rawText;
    final prevFormatted = entry.processedMarkdown;
    _clipBox.put(
      entry.id,
      entry
          .copyWith(rawText: raw.text, processedMarkdown: formatted.text)
          .toMap(),
    );
    setState(() {});
    _toast('Redacted ${plural(n, 'secret')}', undo: () {
      _clipBox.put(
        entry.id,
        entry
            .copyWith(rawText: prevRaw, processedMarkdown: prevFormatted)
            .toMap(),
      );
      setState(() {});
    });
  }

  void _toggleSelectMode() {
    setState(() {
      _selecting = !_selecting;
      _selectedIds.clear();
    });
  }

  void _toggleSelect(String id) {
    setState(() {
      if (!_selectedIds.remove(id)) _selectedIds.add(id);
    });
  }

  /// Merges the picked clips into one new entry, oldest first, and leaves the
  /// sources alone: a join that ate its inputs would be un-undoable, and the
  /// undo here only has to drop the one row it made.
  Future<void> _joinSelected() async {
    final byId = {for (final e in _entries) e.id: e};
    final picked = [
      for (final id in _selectedIds)
        if (byId[id] != null) byId[id]!,
    ];
    if (picked.length < 2) {
      _toast('Pick at least two clips to join');
      return;
    }
    final joined = joinClips(picked);
    await _clipBox.put(joined.id, joined.toMap());
    if (!mounted) return;
    setState(() {
      _selecting = false;
      _selectedIds.clear();
    });
    _toast('Joined ${picked.length} clips', undo: () {
      _clipBox.delete(joined.id);
      setState(() {});
    });
  }

  /// The join bar. Visible only in selection mode, under the feed header: how
  /// many clips are picked, the commit, and the way out.
  Widget _joinBar() {
    final n = _selectedIds.length;
    final ready = n >= 2 && !_isProcessing;
    return Panel(
      padding:
          const EdgeInsets.fromLTRB(Space.lg, Space.md, Space.lg, Space.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            n == 0
                ? 'Tap clips to pick the ones to join'
                : '${plural(n, 'clip')} picked, oldest first',
            style: AppType.meta(context),
          ),
          const SizedBox(height: Space.sm),
          Row(
            children: [
              QuietAction(
                label: 'Cancel',
                dense: true,
                onPressed: _toggleSelectMode,
              ),
              const SizedBox(width: Space.sm),
              Expanded(
                child: PrimaryAction(
                  label: n < 2 ? 'Join clips' : 'Join $n clips',
                  icon: Icons.merge_type_rounded,
                  onPressed: ready ? _joinSelected : null,
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Future<void> _addClipToNotes(ClipEntry entry) async {
    // Prefer the AI-formatted summary; fall back to the raw clip text.
    final content = entry.processedMarkdown.trim().isNotEmpty
        ? entry.processedMarkdown
        : entry.rawText;
    final note = Note(
      title: _noteTitleFrom(entry.rawText),
      content: content,
      tags: const ['clip'],
    );
    Hive.box(AppDefaults.hiveNoteBox).put(note.id, note.toMap());

    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: const Text('Added to Notes'),
        duration: const Duration(seconds: 4),
        action: SnackBarAction(
          label: 'Open',
          onPressed: () => widget.onNavigateToTab?.call(2), // Notes tab
        ),
      ),
    );
  }

  static String _noteTitleFrom(String raw) {
    final cleaned = raw.trim().replaceAll('\n', ' ');
    final firstLine = cleaned.characters.take(40).toString().trim();
    return firstLine.isEmpty ? 'Clip note' : firstLine;
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);
    // The box is read once. This build used to walk it three times: once for
    // the count in the header, once to test emptiness, once for the rows.
    final entries = _entries;
    final pinned = entries.where((e) => e.isPinned).length;
    final today = entries.where(_isToday).length;
    // A query ranks by relevance instead of merely filtering, because a
    // 500-clip feed is searched, not scrolled.
    final visible = _search.text.trim().isEmpty
        ? entries
        : rankClips(_search.text, entries);
    return NotificationListener<ScrollNotification>(
      onNotification: (notification) {
        if (notification is ScrollUpdateNotification && widget.scrollNotifier != null) {
          widget.scrollNotifier!.value = notification.metrics.pixels;
        }
        return false;
      },
      // Pull the feed down to take whatever is on the clipboard now. The only
      // page in the app with a refresh gesture, because it is the only one with
      // something outside itself that it can be behind on.
      child: RefreshIndicator(
        onRefresh: _captureFromClipboard,
        color: accentOn(context, minRatio: 3),
        backgroundColor: Theme.of(context).colorScheme.surface,
        strokeWidth: 2.2,
        displacement: MediaQuery.of(context).padding.top + 32,
        child: ListView(
      physics:
          const BouncingScrollPhysics(parent: AlwaysScrollableScrollPhysics()),
      padding: EdgeInsets.fromLTRB(
        16,
        MediaQuery.of(context).padding.top + 16,
        16,
        navBarClearance(context),
      ),
      children: [
        // ── The head of the page. The greeting in the accent, the app as the
        // owner of the screen, the timely line as a pull quote, and the mark
        // set opposite them. Split, not a centred stack.
        Entrance(
          child: TimelyGreetingView(
            builder: (context, greeting) => Masthead(
              eyebrow: greeting.greeting,
              title: 'ClipSync AI',
              lead: greeting.message,
              asset: const AppMark(size: 46),
            ),
          ),
        ),

        // ── What the page holds, as figures. A number set large carries more
        // than a row reading "Total clips   14", and it lands second in the
        // reading order instead of tenth. Nothing to count, nothing drawn.
        if (entries.isNotEmpty) ...[
          const SizedBox(height: Space.xl),
          Entrance(
            index: 1,
            child: Readout(
              value: '${entries.length}',
              label: entries.length == 1 ? 'clip kept' : 'clips kept',
              facts: [
                Figure('$today', 'today'),
                if (pinned > 0) Figure('$pinned', 'pinned'),
              ],
            ),
          ),
        ],
        const SizedBox(height: Space.xl),

        // ── Capture. The thing this screen exists for, so it gets the one
        // elevated surface on the page, and the accent edge while it is live.
        Entrance(index: 2, child: _capturePanel()),

        // ── Compose. A recessed instrument, not a third underlined row. This
        // is the one place on the page you type into, and it should not look
        // like the setting above it.
        const SizedBox(height: Space.lg + 2),
        Entrance(
          index: 3,
          child: Composer(
            controller: _manualController,
            hint: 'Paste or type something worth keeping',
            busy: _isProcessing,
            tooltip: 'Keep this text',
            onSubmit: _processManualInput,
            actions: [
              QuietAction(
                label: 'Clipboard',
                icon: Icons.content_paste_rounded,
                dense: true,
                onPressed: _isProcessing ? null : _pasteFromClipboard,
              ),
              QuietAction(
                label: 'Dictate',
                icon: Icons.mic_none_rounded,
                dense: true,
                onPressed: () => widget.onNavigateToTab?.call(3),
              ),
            ],
          ),
        ),
        // ── The feed. The page's one group name, with the bulk verb on its own
        // rule rather than as a third button under the field. The count lives
        // in the readout above, so it is not printed twice.
        Entrance(
          index: 4,
          child: SectionHeader(
            'Recent',
            trailing: entries.isEmpty
                ? null
                : Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      IconAction(
                        icon: _selecting
                            ? Icons.close_rounded
                            : Icons.checklist_rounded,
                        tooltip: _selecting
                            ? 'Cancel selection'
                            : 'Select clips to join',
                        size: 18,
                        onPressed: _toggleSelectMode,
                      ),
                      IconAction(
                        icon: Icons.sync_rounded,
                        tooltip: 'Reprocess every clip with the current model',
                        size: 18,
                        onPressed: _isProcessing ? null : _syncAllClips,
                      ),
                    ],
                  ),
          ),
        ),
        // Join mode gets its bar under the header it belongs to: the count of
        // what is picked, the commit, and the way out.
        if (_selecting) ...[
          const SizedBox(height: Space.md),
          Entrance(index: 5, child: _joinBar()),
        ],
        if (entries.isNotEmpty) ...[
          const SizedBox(height: Space.md),
          Entrance(index: 6, child: _clipSearchBox()),
        ],
        // Work in flight takes the shape of the entry it will become, rather
        // than a spinner that tells you nothing about what is arriving.
        if (_isProcessing) _pendingEntry(),
        if (visible.isEmpty)
          visible.length != entries.length
              ? StateBlock(
                  compact: true,
                  icon: Icons.search_off_rounded,
                  title: 'No match',
                  message:
                      'Nothing kept says that. Try a word from the reply.',
                )
              : EmptyState(
                  icon: Icons.content_paste_off_rounded,
                  title: 'Nothing captured yet',
                  message:
                      'Copy text anywhere on the phone and it lands here, tidied up.',
                  action: PrimaryAction(
                    label: 'Read the clipboard now',
                    icon: Icons.download_rounded,
                    expand: false,
                    onPressed: _captureFromClipboard,
                  ),
                )
        else
          ...visible.map(_buildClipRow),
      ],
    ),
      ),
    );
  }

  /// The capture instrument. Two facts on one raised surface: whether the
  /// service is watching, and what is tidying up what it catches. The page's
  /// elevation is spent here because this is the only thing on it with live
  /// state, and the accent edge appears only while that state is on.
  Widget _capturePanel() {
    final live = widget.isServiceRunning;
    final loaded = widget.processor.modelLoaded;
    return Panel(
      accentEdge: live,
      padding: const EdgeInsets.fromLTRB(
          Space.lg, Space.lg - 2, Space.lg, Space.lg - 2),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('Clipboard monitor', style: AppType.heading(context)),
                    const SizedBox(height: 3),
                    Text(
                      live
                          ? 'Watching, and saving what you copy'
                          : 'Paused, nothing is being saved',
                      style: AppType.meta(context),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: Space.md),
              LiquidToggle(value: live, onChanged: _toggleMonitor),
            ],
          ),
          const Padding(
            padding: EdgeInsets.symmetric(vertical: Space.md + 1),
            child: Rule(),
          ),
          Pressable(
            onTap: () => widget.onNavigateToTab?.call(4),
            child: Row(
              children: [
                Icon(
                  loaded ? Icons.memory_rounded : Icons.functions_rounded,
                  size: 17,
                  color: ink(context, 0.46),
                ),
                const SizedBox(width: Space.md - 1),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        loaded ? 'On-device model' : 'Regex formatting',
                        style:
                            AppType.heading(context).copyWith(fontSize: 14),
                      ),
                      const SizedBox(height: 2),
                      Text(
                        loaded
                            ? 'llama.cpp, running on this phone'
                            : 'Fast fallback while no model is loaded',
                        style: AppType.meta(context),
                      ),
                    ],
                  ),
                ),
                Icon(
                  Icons.chevron_right_rounded,
                  size: 18,
                  color: ink(context, 0.32),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  /// The feed's filter, in the same voice as the history sheet's: title hits
  /// above tag hits above body hits, rare words above common ones.
  Widget _clipSearchBox() {
    return InlineField(
      controller: _search,
      hint: 'Search clips',
      onChanged: (_) => setState(() {}),
      trailing: _search.text.isEmpty
          ? null
          : IconAction(
              icon: Icons.close_rounded,
              tooltip: 'Clear search',
              size: 16,
              onPressed: () {
                _search.clear();
                setState(() {});
              },
            ),
    );
  }

  /// The entry that is about to exist: the same margin, the same rule and two
  /// lines where the text will be, so the feed does not jump when the real one
  /// takes its place.
  Widget _pendingEntry() {
    return const MarginEntry(
      margin: 'now',
      child: Pulse(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            SkeletonLine(widthFactor: 0.94),
            SizedBox(height: Space.sm),
            SkeletonLine(widthFactor: 0.62),
          ],
        ),
      ),
    );
  }

  /// A clip as a margin entry: when it arrived in the margin, one vertical
  /// rule, then what it says. No plate, and no rule underneath either — twenty
  /// clips used to draw twenty full-width horizontals, which is the flattest a
  /// list can be.
  ///
  /// The original is printed only where the formatter actually changed
  /// something. It used to be printed always, so a clip that came through
  /// untouched showed the same sentence twice, once in full and once dimmed.
  ///
  /// Three gestures, the same three the notes feed answers: tap reads the clip,
  /// hold gives it its actions, and dragging it aside pins or deletes it without
  /// opening anything.
  Widget _buildClipRow(ClipEntry entry) {
    final raw = entry.rawText.replaceAll(RegExp(r'\s+'), ' ').trim();
    final result = entry.processedMarkdown.trim();
    final changed = raw.isNotEmpty &&
        raw != result.replaceAll(RegExp(r'\s+'), ' ').trim();
    // In selection mode the tick doubles as the picked mark, and the row
    // grows a checkbox so the gesture reads as picking, not opening.
    final selected = _selectedIds.contains(entry.id);
    final body = Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        MarkdownBody(
          data: result.isEmpty ? raw : result,
          styleSheet: ledgerMarkdown(context),
        ),
        if (changed) ...[
          const SizedBox(height: Space.sm - 1),
          Text(
            raw,
            style: AppType.meta(context).copyWith(color: ink(context, 0.36)),
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
          ),
        ],
      ],
    );
    return MarginEntry(
      margin: stamp(entry.timestamp),
      flag: entry.isPinned || selected,
      onTap: _selecting ? () => _toggleSelect(entry.id) : () => _openClip(entry),
      onLongPress: _selecting ? null : () => _clipSheet(entry),
      marginAction: IconAction(
        icon: Icons.more_horiz,
        tooltip: 'Clip options',
        size: 17,
        dense: true,
        alignment: Alignment.centerRight,
        tone: ink(context, 0.42),
        onPressed: () => _clipSheet(entry),
      ),
      swipeId: entry.id,
      swipeRight: SwipeAct(
        icon: entry.isPinned
            ? Icons.push_pin_rounded
            : Icons.push_pin_outlined,
        label: entry.isPinned ? 'Unpin' : 'Pin',
        onAct: () => _togglePin(entry),
      ),
      swipeLeft: SwipeAct(
        icon: Icons.delete_outline_rounded,
        label: 'Delete',
        tone: Semantic.danger,
        dismiss: true,
        onAct: () => _deleteEntry(entry),
      ),
      child: _selecting
          ? Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Checkbox(
                  value: selected,
                  visualDensity: VisualDensity.compact,
                  activeColor: Theme.of(context).colorScheme.primary,
                  onChanged: (_) => _toggleSelect(entry.id),
                ),
                Expanded(child: body),
              ],
            )
          : body,
    );
  }

  /// Opens the clip for reading, carrying the feed's order with it so the reader
  /// can be paged sideways through the rest of the clips from wherever it lands.
  Future<void> _openClip(ClipEntry entry) async {
    final ids = _entries.map((e) => e.id).toList();
    final start = ids.indexOf(entry.id);
    if (start < 0) return;
    await Navigator.of(context).push(
      PageRouteBuilder<void>(
        transitionDuration: Motion.base,
        reverseTransitionDuration: Motion.fast,
        pageBuilder: (_, __, ___) => ClipReader(
          ids: ids,
          startIndex: start,
          box: _clipBox,
          onOptions: _clipSheet,
          onCopy: (e) {
            final body = e.processedMarkdown.trim().isEmpty
                ? e.rawText
                : e.processedMarkdown;
            Clipboard.setData(ClipboardData(text: body));
            _toast('Copied');
          },
        ),
        transitionsBuilder: (context, animation, _, child) {
          final eased = CurvedAnimation(parent: animation, curve: kGlassCurve);
          if (reduceMotion(context)) {
            return FadeTransition(opacity: eased, child: child);
          }
          return FadeTransition(
            opacity: eased,
            child: SlideTransition(
              position: Tween<Offset>(
                begin: const Offset(0, 0.035),
                end: Offset.zero,
              ).animate(eased),
              child: child,
            ),
          );
        },
      ),
    );
    if (mounted) setState(() {});
  }

  /// The clip's actions. A sheet rather than a menu hanging off the dot in the
  /// margin: six verbs behind a 19px glyph leaves no room to say what any of
  /// them does, and this one can note that Summarize writes on this phone.
  /// Holding the row reaches it too, since the dot is a small target.
  Future<void> _clipSheet(ClipEntry entry) async {
    await ledgerSheet<void>(
      context,
      title: 'This clip',
      subtitle: stampLong(entry.timestamp),
      children: (ctx) => [
        SheetAction(
          icon: Icons.content_copy_rounded,
          label: 'Copy result',
          onTap: () {
            Navigator.pop(ctx);
            Clipboard.setData(ClipboardData(text: entry.processedMarkdown));
            _toast('Copied');
          },
        ),
        SheetAction(
          icon: Icons.short_text_rounded,
          label: 'Copy original',
          onTap: () {
            Navigator.pop(ctx);
            Clipboard.setData(ClipboardData(text: entry.rawText));
            _toast('Original copied');
          },
        ),
        if (widget.processor.modelLoaded)
          SheetAction(
            icon: Icons.auto_awesome_rounded,
            label: 'Summarize',
            detail: 'Three bullets, written on this phone',
            onTap: () {
              Navigator.pop(ctx);
              _summarizeClip(entry);
            },
          ),
        SheetAction(
          icon: Icons.notes_rounded,
          label: 'Add to notes',
          onTap: () {
            Navigator.pop(ctx);
            _addClipToNotes(entry);
          },
        ),
        SheetAction(
          icon: Icons.key_off_rounded,
          label: 'Redact secrets',
          detail: 'Masks keys, tokens and passwords, on this device',
          onTap: () {
            Navigator.pop(ctx);
            _redactClip(entry);
          },
        ),
        SheetAction(
          icon: Icons.checklist_rounded,
          label: 'Select for join',
          detail: 'Pick more clips, then join them into one',
          onTap: () {
            Navigator.pop(ctx);
            setState(() {
              _selecting = true;
              _selectedIds.add(entry.id);
            });
          },
        ),
        SheetAction(
          icon: entry.isPinned
              ? Icons.push_pin_rounded
              : Icons.push_pin_outlined,
          label: entry.isPinned ? 'Unpin' : 'Pin to top',
          onTap: () {
            Navigator.pop(ctx);
            _togglePin(entry);
          },
        ),
        SheetAction(
          icon: Icons.delete_outline_rounded,
          label: 'Delete clip',
          danger: true,
          divided: false,
          onTap: () {
            Navigator.pop(ctx);
            _deleteEntry(entry);
          },
        ),
      ],
    );
  }

  /// The margin stamp and its long form are shared with the other feeds and
  /// live in `ui/editorial.dart` as `stamp` / `stampLong`.
  static bool _isToday(ClipEntry e) {
    final now = DateTime.now();
    return e.timestamp.year == now.year &&
        e.timestamp.month == now.month &&
        e.timestamp.day == now.day;
  }

}

// ─────────────────────────────────────────────────────────────────────────────
// CLIP READER
// ─────────────────────────────────────────────────────────────────────────────

/// A clip at full length, on its own screen.
///
/// The feed can only ever show the first few lines of anything, so tapping a row
/// used to open a menu — six verbs about a clip you had not read yet. This reads
/// it instead: the formatted text at reading size and selectable, the original
/// underneath where the formatter changed something, and nothing else on the
/// page competing for the eye.
///
/// It holds ids rather than clips, and looks each one up in the box as it draws,
/// so a summary written from the sheet appears here without the reader knowing
/// anything about who wrote it. The order is frozen at the moment it opens:
/// pinning a clip you are reading should not slide the page under your thumb.
class ClipReader extends StatefulWidget {
  const ClipReader({
    required this.ids,
    required this.startIndex,
    required this.box,
    required this.onOptions,
    required this.onCopy,
    super.key,
  });

  /// The feed's order, at the moment the reader was opened.
  final List<String> ids;
  final int startIndex;
  final Box box;
  final Future<void> Function(ClipEntry) onOptions;
  final void Function(ClipEntry) onCopy;

  @override
  State<ClipReader> createState() => _ClipReaderState();
}

class _ClipReaderState extends State<ClipReader> {
  late final PageController _pages;
  late int _index;

  bool _closing = false;

  @override
  void initState() {
    super.initState();
    _index = widget.startIndex;
    _pages = PageController(initialPage: _index);
  }

  @override
  void dispose() {
    _pages.dispose();
    super.dispose();
  }

  /// The clip at [i], or null once it has been deleted from under the reader.
  ClipEntry? _clipAt(int i) {
    if (i < 0 || i >= widget.ids.length) return null;
    final raw = widget.box.get(widget.ids[i]);
    if (raw is! Map) return null;
    return ClipEntry.fromMap(Map<String, dynamic>.from(raw));
  }

  void _close() {
    if (_closing || !mounted) return;
    _closing = true;
    HapticFeedback.selectionClick();
    Navigator.of(context).maybePop();
  }

  /// Dragging the reader down past the top of the text puts it away, which is
  /// what the phone's own reading surfaces do, and it costs no second drag
  /// recogniser competing with the one that scrolls the text.
  ///
  /// The distance is read straight off the metrics. Bouncing physics never
  /// reports an [OverscrollNotification] at all — it lets the position travel
  /// out of range and calls that an ordinary scroll update — so counting
  /// overscroll here would mean the gesture never fires on a phone, which is
  /// exactly what it did until this was measured on one.
  ///
  /// Only a drag counts: the spring back after a fling crosses the top edge too,
  /// and that is not a request to leave. And only the vertical axis, because the
  /// reader's own sideways paging reports scroll through here as well.
  bool _onScroll(ScrollNotification n) {
    if (n.metrics.axis != Axis.vertical) return false;
    if (n is ScrollUpdateNotification && n.dragDetails != null) {
      if (n.metrics.minScrollExtent - n.metrics.pixels > 96) _close();
    }
    return false;
  }

  @override
  Widget build(BuildContext context) {
    return ValueListenableBuilder<Box>(
      valueListenable: widget.box.listenable(),
      builder: (context, box, _) {
        final entry = _clipAt(_index);
        // Deleted while it was open. Leave rather than draw an empty reader.
        if (entry == null) {
          WidgetsBinding.instance.addPostFrameCallback((_) => _close());
          return const SizedBox.shrink();
        }
        return Scaffold(
          backgroundColor: Colors.transparent,
          body: Stack(
            children: [
              const GlassBackdrop(),
              Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  _header(entry),
                  const Rule(),
                  Expanded(
                    // Selection wraps the pager rather than each page, and the
                    // nesting is the whole point. A selection region installs a
                    // horizontal drag recogniser of its own on touch, and the
                    // deeper of two tied recognisers wins the arena — so a
                    // region inside each page takes the sideways drag and the
                    // pages stop turning. From out here the pager is the deeper
                    // one, so it keeps its own gesture and selection is left
                    // with the long press it actually wants.
                    child: SelectionArea(
                      child: NotificationListener<ScrollNotification>(
                        onNotification: _onScroll,
                        child: PageView.builder(
                          controller: _pages,
                          physics: const BouncingScrollPhysics(),
                          itemCount: widget.ids.length,
                          onPageChanged: (i) {
                            HapticFeedback.selectionClick();
                            setState(() => _index = i);
                          },
                          itemBuilder: (context, i) {
                            final clip = _clipAt(i);
                            if (clip == null) return const SizedBox.shrink();
                            return _page(clip);
                          },
                        ),
                      ),
                    ),
                  ),
                ],
              ),
            ],
          ),
        );
      },
    );
  }

  /// When it was captured, what it is, and the two things you do with it. The
  /// position is printed because it is also the only honest hint that the page
  /// can be dragged sideways to the clip before or after this one.
  Widget _header(ClipEntry entry) {
    final source =
        entry.rawText.trim().isEmpty ? entry.processedMarkdown : entry.rawText;
    final words = wordCount(source);
    final line = <String>[
      if (widget.ids.length > 1) '${_index + 1} of ${widget.ids.length}',
      if (words > 0) plural(words, 'word'),
    ].join('  ·  ');
    return SafeArea(
      bottom: false,
      child: Padding(
        padding: const EdgeInsets.fromLTRB(
            Space.sm, Space.sm, Space.sm, Space.md - 2),
        child: Row(
          children: [
            IconAction(
              icon: Icons.arrow_back_rounded,
              tooltip: 'Back to clips',
              onPressed: _close,
            ),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      if (entry.isPinned) ...[
                        Icon(
                          Icons.push_pin_rounded,
                          size: 13,
                          color: accentOn(context, minRatio: 3),
                        ),
                        const SizedBox(width: 5),
                      ],
                      Expanded(
                        child: Text(
                          stampLong(entry.timestamp),
                          style: AppType.heading(context),
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                    ],
                  ),
                  if (line.isNotEmpty) ...[
                    const SizedBox(height: 2),
                    Text(line, style: AppType.meta(context)),
                  ],
                ],
              ),
            ),
            IconAction(
              icon: Icons.content_copy_rounded,
              tooltip: 'Copy this clip',
              size: 18,
              onPressed: () => widget.onCopy(entry),
            ),
            IconAction(
              icon: Icons.more_horiz,
              tooltip: 'Clip options',
              onPressed: () => widget.onOptions(entry),
            ),
          ],
        ),
      ),
    );
  }

  /// One clip, at reading size. Selection is handled once around the pager
  /// rather than per page — see [build] for why it has to be up there.
  Widget _page(ClipEntry clip) {
    final result = clip.processedMarkdown.trim();
    final raw = clip.rawText.trim();
    final tidy = RegExp(r'\s+');
    final changed = raw.isNotEmpty &&
        raw.replaceAll(tidy, ' ') != result.replaceAll(tidy, ' ');
    final body = result.isEmpty ? raw : result;
    return ListView(
      physics:
          const BouncingScrollPhysics(parent: AlwaysScrollableScrollPhysics()),
      padding: EdgeInsets.fromLTRB(
        Space.xl,
        Space.lg,
        Space.xl,
        MediaQuery.of(context).padding.bottom + Space.section,
      ),
      children: [
        if (body.isEmpty)
          const StateBlock(
            icon: Icons.text_snippet_outlined,
            title: 'This clip is empty',
            message: 'Nothing was captured with it. You can delete it from '
                'the options above.',
          )
        else
          MarkdownBody(
            data: body,
            styleSheet: ledgerMarkdown(context, size: 15),
          ),
        if (changed) ...[
          const SectionHeader(
            'As it was copied',
            top: Space.section,
            bottom: Space.md,
          ),
          DecoratedBox(
            decoration: BoxDecoration(
              color: fill(context),
              borderRadius: BorderRadius.circular(Radii.inner),
            ),
            child: Padding(
              padding: const EdgeInsets.all(Space.lg - 2),
              child: Text(
                raw,
                style: AppType.body(context).copyWith(
                  color: ink(context, 0.72),
                  height: 1.5,
                ),
              ),
            ),
          ),
        ],
      ],
    );
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// NOTES SUITE VIEW
// ─────────────────────────────────────────────────────────────────────────────

class NotesView extends StatefulWidget {
  final OllamaClipProcessor processor;
  const NotesView({super.key, required this.processor});

  @override
  State<NotesView> createState() => _NotesViewState();
}

class _NotesViewState extends State<NotesView>
    with AutomaticKeepAliveClientMixin {
  @override
  bool get wantKeepAlive => true;
  late Box _noteBox;
  final _searchController = TextEditingController();
  final _titleController = TextEditingController();
  final _contentController = TextEditingController();
  final _tagsController = TextEditingController();
  String _filterTag = '';
  /// 'updated' | 'created' | 'title'. Pinned notes stay on top regardless.
  String _sortMode = 'updated';
  bool _isEditing = false;
  Note? _editingNote;
  /// True while an AI action is in flight, so the bar can dim and refuse
  /// a second run rather than racing itself.
  bool _aiBusy = false;

  @override
  void initState() {
    super.initState();
    _noteBox = Hive.box(AppDefaults.hiveNoteBox);
  }

  @override
  void dispose() {
    // Never leave a callback pointing into a dead State behind.
    if (appBackInterceptor.value == _saveNote) appBackInterceptor.value = null;
    _searchController.dispose();
    _titleController.dispose();
    _contentController.dispose();
    _tagsController.dispose();
    super.dispose();
  }

  List<Note> get _notes {
    final list = <Note>[];
    for (var key in _noteBox.keys) {
      final val = _noteBox.get(key);
      if (val is Map) {
        list.add(Note.fromMap(Map<String, dynamic>.from(val)));
      }
    }
    // Filter
    final query = _searchController.text.toLowerCase();
    final filtered = list.where((n) {
      if (_filterTag.isNotEmpty && !n.tags.contains(_filterTag)) return false;
      if (query.isNotEmpty) {
        return n.title.toLowerCase().contains(query) ||
            n.content.toLowerCase().contains(query);
      }
      return true;
    }).toList();
    filtered.sort((a, b) {
      if (a.isPinned != b.isPinned) return a.isPinned ? -1 : 1;
      switch (_sortMode) {
        case 'created':
          return b.createdAt.compareTo(a.createdAt);
        case 'title':
          final at = a.title.trim().isEmpty ? 'untitled' : a.title.toLowerCase();
          final bt = b.title.trim().isEmpty ? 'untitled' : b.title.toLowerCase();
          return at.compareTo(bt);
        default:
          return b.updatedAt.compareTo(a.updatedAt);
      }
    });
    return filtered;
  }

  /// Every note in the box, ignoring the search and tag filters, walked once
  /// for the three figures at the top of the page.
  ({int total, int pinned, int words}) get _noteStats {
    var total = 0, pinned = 0, words = 0;
    for (final key in _noteBox.keys) {
      final val = _noteBox.get(key);
      if (val is! Map) continue;
      total++;
      final note = Note.fromMap(Map<String, dynamic>.from(val));
      if (note.isPinned) pinned++;
      words += wordCount(note.content);
    }
    return (total: total, pinned: pinned, words: words);
  }

  List<String> get _allTags {
    final tags = <String>{};
    for (var key in _noteBox.keys) {
      final val = _noteBox.get(key);
      if (val is Map) {
        final note = Note.fromMap(Map<String, dynamic>.from(val));
        tags.addAll(note.tags);
      }
    }
    return tags.toList()..sort();
  }

  void _createNote() {
    setState(() {
      _isEditing = true;
      _editingNote = Note();
      _titleController.text = '';
      _contentController.text = '';
      _tagsController.text = '';
    });
    // Back now closes the editor instead of the app.
    appBackInterceptor.value = _saveNote;
  }

  void _editNote(Note note) {
    setState(() {
      _isEditing = true;
      _editingNote = note;
      _titleController.text = note.title;
      _contentController.text = note.content;
      _tagsController.text = note.tags.join(', ');
    });
    appBackInterceptor.value = _saveNote;
  }

  void _saveNote() {
    final note = _editingNote;
    if (note == null) return;
    appBackInterceptor.value = null;
    // A note opened and closed without a keystroke is not a note. Writing it
    // anyway left an "Untitled · Empty" row in the list every time someone
    // tapped the compose button and changed their mind. Only a *new* note is
    // dropped this way: emptying one that already exists is an edit, and
    // deleting it out from under the user would be a surprise.
    final blank = note.title.trim().isEmpty &&
        note.content.trim().isEmpty &&
        note.tags.isEmpty;
    if (blank && !_noteBox.containsKey(note.id)) {
      setState(() {
        _isEditing = false;
        _editingNote = null;
      });
      return;
    }
    note.updatedAt = DateTime.now();
    _noteBox.put(note.id, note.toMap());
    setState(() {
      _isEditing = false;
      _editingNote = null;
    });
  }

  /// Delete from the list, with one chance to take it back.
  void _deleteNoteWithUndo(Note note) {
    final snapshot = note.toMap();
    _noteBox.delete(note.id);
    setState(() {});
    _toast(
      'Note deleted',
      undo: () {
        _noteBox.put(snapshot['id'], snapshot);
        setState(() {});
      },
    );
  }

  void _duplicateNote(Note note) {
    final copy = Note(
      title: note.title.trim().isEmpty ? 'Untitled (copy)' : '${note.title} (copy)',
      content: note.content,
      tags: List<String>.from(note.tags),
    );
    _noteBox.put(copy.id, copy.toMap());
    setState(() {});
    _toast('Note duplicated');
  }

  void _copyNote(Note note) {
    final body = note.title.trim().isEmpty
        ? note.content
        : '${note.title}\n\n${note.content}';
    Clipboard.setData(ClipboardData(text: body));
    _toast('Note copied');
  }

  void _togglePinNote(Note note) {
    note.isPinned = !note.isPinned;
    note.updatedAt = DateTime.now();
    _noteBox.put(note.id, note.toMap());
    setState(() {});
  }

  /// Summarises a note from the list and appends the result under a heading,
  /// so nothing the user wrote is overwritten.
  Future<void> _summarizeNoteInPlace(Note note) async {
    if (_aiBusy) return;
    if (note.content.trim().isEmpty) {
      _toast('That note is empty');
      return;
    }
    if (!widget.processor.modelLoaded) {
      _toast('Load a model in Settings first');
      return;
    }
    setState(() => _aiBusy = true);
    _toast('Summarizing…');
    final result = await widget.processor.instruct(
      'Summarize the note below in three short bullet points. Keep names, '
      'numbers and dates exact. Output only the bullets.\n\n${note.content}',
    );
    if (!mounted) return;
    setState(() => _aiBusy = false);
    if (result == null) {
      _toast('The model did not return a summary');
      return;
    }
    final previous = note.content;
    note.content = '$previous\n\n## Summary\n\n$result';
    note.updatedAt = DateTime.now();
    _noteBox.put(note.id, note.toMap());
    setState(() {});
    _toast(
      'Summary added',
      undo: () {
        note.content = previous;
        _noteBox.put(note.id, note.toMap());
        setState(() {});
      },
    );
  }

  /// One-line feedback, with an optional single-step undo.
  void _toast(String message, {VoidCallback? undo}) {
    if (!mounted) return;
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(
        SnackBar(
          content: Text(message),
          duration: Duration(seconds: undo == null ? 2 : 5),
          margin: snackBarMargin(context),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(Radii.control),
          ),
          action: undo == null
              ? null
              : SnackBarAction(label: 'Undo', onPressed: undo),
        ),
      );
  }

  Future<void> _aiAction(String action) async {
    final note = _editingNote;
    if (note == null || _aiBusy) return;
    // The live editor text, not the last saved copy, so an action always runs
    // on what the user can actually see.
    final content = _contentController.text;
    if (content.trim().isEmpty) {
      _toast('Write something first');
      return;
    }
    if (!widget.processor.modelLoaded) {
      _toast('Load a model in Settings first');
      return;
    }

    final prompt = switch (action) {
      'summarize' => 'Summarize the note below concisely, as markdown. Keep '
          'names, numbers and dates exact. Output only the summary.\n\n$content',
      'actions' => 'Extract every action item from the note below as a '
          'markdown checklist. Output only the checklist.\n\n$content',
      'expand' => 'Expand the note below with more detail and clearer '
          'structure, as markdown. Keep the original meaning and every fact '
          'that is already there. Output only the note.\n\n$content',
      'grammar' => 'Correct the spelling, grammar and punctuation of the text '
          'below. Keep the wording, the meaning, the language and the markdown '
          'formatting. Output only the corrected text.\n\n$content',
      _ => content,
    };

    setState(() => _aiBusy = true);
    final result = await widget.processor.instruct(prompt);
    if (!mounted) return;
    setState(() => _aiBusy = false);
    // The user may have left the editor while the model was thinking.
    if (_editingNote != note) return;
    if (result == null) {
      _toast('The model did not return anything');
      return;
    }

    setState(() {
      note.content = result;
      note.updatedAt = DateTime.now();
      // Without this the editor keeps showing the old text and the action
      // looks like it did nothing.
      _contentController.text = result;
    });
    _noteBox.put(note.id, note.toMap());
    _toast(
      'Note updated',
      undo: () {
        setState(() {
          note.content = content;
          _contentController.text = content;
        });
        _noteBox.put(note.id, note.toMap());
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);
    if (_isEditing && _editingNote != null) {
      return _buildEditor();
    }
    return _buildList();
  }

  Widget _buildList() {
    final notes = _notes;
    final tags = _allTags;
    final stats = _noteStats;
    final narrowed =
        _searchController.text.trim().isNotEmpty || _filterTag.isNotEmpty;
    // Pinned notes already sort first; naming the break turns one long column
    // into two short ones, which is the whole difference between a list and a
    // page that has been laid out.
    final pinned = notes.where((n) => n.isPinned).toList();
    final rest = notes.where((n) => !n.isPinned).toList();
    return ListView(
      physics: const BouncingScrollPhysics(),
      padding: EdgeInsets.fromLTRB(
        Space.lg,
        MediaQuery.of(context).padding.top + Space.lg,
        Space.lg,
        navBarClearance(context),
      ),
      children: [
        Entrance(child: _notesMasthead(stats, narrowed, notes.length)),
        if (stats.total > 0) ...[
          const SizedBox(height: Space.xl),
          Entrance(index: 1, child: _notesReadout(stats, tags.length)),
        ],
        const SizedBox(height: Space.xl),
        Entrance(index: 2, child: _notesFilters(tags)),
        if (pinned.isNotEmpty) ...[
          Entrance(
            index: 3,
            child: SectionHeader('Pinned', trailing: _buildNotesSortMenu()),
          ),
          ...pinned.map(_noteEntry),
        ],
        if (notes.isEmpty)
          _notesEmpty(narrowed)
        else if (rest.isNotEmpty) ...[
          Entrance(
            index: pinned.isEmpty ? 3 : 4,
            child: SectionHeader(
              pinned.isEmpty ? 'All notes' : 'Other notes',
              trailing: pinned.isEmpty ? _buildNotesSortMenu() : null,
              count: pinned.isEmpty ? null : plural(rest.length, 'note'),
            ),
          ),
          ...rest.map(_noteEntry),
        ],
      ],
    );
  }

  /// The head of the page. No standing eyebrow: it appears only when a search
  /// or a tag has narrowed what is below, and then it says which.
  Widget _notesMasthead(
    ({int total, int pinned, int words}) stats,
    bool narrowed,
    int shown,
  ) {
    return Masthead(
      title: 'Notes',
      eyebrow: !narrowed
          ? null
          : (_filterTag.isEmpty ? 'Search' : '#$_filterTag'),
      lead: !narrowed
          ? null
          : shown == 0
              ? 'Nothing here matches that.'
              : '$shown of ${plural(stats.total, 'note')} match.',
      asset: _newNoteTile(),
    );
  }

  /// New note as an object in the corner rather than a glyph in a bar. It is
  /// the only thing on this page that creates something, so it is the only
  /// filled control and the only lifted one.
  Widget _newNoteTile() {
    final scheme = Theme.of(context).colorScheme;
    final dark = Theme.of(context).brightness == Brightness.dark;
    return Tooltip(
      message: 'New note',
      child: Semantics(
        button: true,
        label: 'New note',
        child: Pressable(
          onTap: _createNote,
          scale: 0.92,
          child: Container(
            width: 46,
            height: 46,
            decoration: BoxDecoration(
              color: scheme.primary,
              borderRadius: BorderRadius.circular(Radii.inner),
              boxShadow: [
                BoxShadow(
                  color: Color.lerp(Colors.black, scheme.primary, 0.3)!
                      .withValues(alpha: dark ? 0.34 : 0.16),
                  blurRadius: 14,
                  offset: const Offset(0, 5),
                ),
              ],
            ),
            child: Icon(
              Icons.add_rounded,
              size: 24,
              color: legibleAccent(scheme.onPrimary, scheme.primary),
            ),
          ),
        ),
      ),
    );
  }

  /// What the notebook holds, as figures. Two facts at most, because the third
  /// one is always the one nobody reads.
  Widget _notesReadout(({int total, int pinned, int words}) stats, int tagCount) {
    return Readout(
      value: '${stats.total}',
      label: stats.total == 1 ? 'note written' : 'notes written',
      facts: [
        if (stats.pinned > 0) Figure('${stats.pinned}', 'pinned'),
        if (stats.words > 0) Figure(_compact(stats.words), 'words'),
        if (stats.pinned == 0 && stats.words == 0 && tagCount > 0)
          Figure('$tagCount', tagCount == 1 ? 'tag' : 'tags'),
      ],
    );
  }

  /// Word counts run into five figures on a real notebook, and a five figure
  /// number in a strip of facts reads as an error.
  static String _compact(int n) => n < 1000
      ? '$n'
      : '${(n / 1000).toStringAsFixed(n < 10000 ? 1 : 0)}k';

  /// The two ways in: the words you remember, and the tag you filed it under.
  Widget _notesFilters(List<String> tags) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        InlineField(
          controller: _searchController,
          hint: 'Search titles and text',
          textInputAction: TextInputAction.search,
          onChanged: (_) => setState(() {}),
          trailing: _searchController.text.isEmpty
              ? null
              : IconAction(
                  icon: Icons.close_rounded,
                  tooltip: 'Clear search',
                  size: 17,
                  onPressed: () {
                    _searchController.clear();
                    setState(() {});
                  },
                ),
        ),
        // The tags are the same switch as the capture modes, so they use the
        // same rail rather than a second chip vocabulary.
        if (tags.isNotEmpty) ...[
          const SizedBox(height: Space.md + 2),
          TabRail(
            labels: ['All', ...tags.map((t) => '#$t')],
            index: _filterTag.isEmpty ? 0 : tags.indexOf(_filterTag) + 1,
            onChanged: (i) => setState(
              () => _filterTag = i == 0 ? '' : tags[i - 1],
            ),
          ),
        ],
      ],
    );
  }

  Widget _notesEmpty(bool narrowed) {
    return StateBlock(
      icon: narrowed ? Icons.search_off_rounded : Icons.edit_note_rounded,
      title: narrowed ? 'Nothing matches' : 'No notes yet',
      message: narrowed
          ? 'Try fewer words, or clear the tag you are filtering by.'
          : 'Notes are yours to write and edit. Markdown, tags, and '
              'the model on hand to summarise or tidy what you wrote.',
      action: narrowed
          ? null
          : PrimaryAction(
              label: 'Write a note',
              icon: Icons.add_rounded,
              expand: false,
              onPressed: _createNote,
            ),
    );
  }


  Widget _buildNotesSortMenu() {
    return PopupMenuButton<String>(
      tooltip: 'Sort notes',
      position: PopupMenuPosition.under,
      padding: EdgeInsets.zero,
      constraints: const BoxConstraints(minWidth: 200),
      icon: Icon(Icons.swap_vert_rounded, size: 20, color: ink(context, 0.62)),
      onSelected: (v) => setState(() => _sortMode = v),
      itemBuilder: (_) => [
        menuItem('updated', Icons.history_rounded, 'Last edited',
            checked: _sortMode == 'updated'),
        menuItem('created', Icons.schedule_rounded, 'Date created',
            checked: _sortMode == 'created'),
        menuItem('title', Icons.sort_by_alpha_rounded, 'Title A to Z',
            checked: _sortMode == 'title'),
      ],
    );
  }

  /// A note in the feed. The edit time hangs in the margin, the note hangs off
  /// the rail, and pinning shows as the rail turning accent rather than as a
  /// glyph competing with the title.
  ///
  /// The same three gestures as every other feed in the app: tap opens it, hold
  /// gives it its actions, and dragging it aside pins or deletes it.
  Widget _noteEntry(Note note) {
    final title = note.title.trim();
    final preview = note.content.replaceAll(RegExp(r'\s+'), ' ').trim();
    final words = wordCount(note.content);
    final filing = <String>[
      if (words > 0) plural(words, 'word'),
      if (note.tags.isNotEmpty) note.tags.take(2).map((t) => '#$t').join(' '),
    ];
    return MarginEntry(
      margin: stamp(note.updatedAt),
      flag: note.isPinned,
      onTap: () => _editNote(note),
      onLongPress: () => _noteSheet(note),
      marginAction: IconAction(
        icon: Icons.more_horiz,
        tooltip: 'Note options',
        size: 17,
        dense: true,
        alignment: Alignment.centerRight,
        tone: ink(context, 0.42),
        onPressed: () => _noteSheet(note),
      ),
      swipeId: note.id,
      swipeRight: SwipeAct(
        icon: note.isPinned ? Icons.push_pin_rounded : Icons.push_pin_outlined,
        label: note.isPinned ? 'Unpin' : 'Pin',
        onAct: () => _togglePinNote(note),
      ),
      swipeLeft: SwipeAct(
        icon: Icons.delete_outline_rounded,
        label: 'Delete',
        tone: Semantic.danger,
        dismiss: true,
        onAct: () => _deleteNoteWithUndo(note),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            title.isEmpty ? 'Untitled' : title,
            style: AppType.heading(context).copyWith(
              color: title.isEmpty ? ink(context, 0.52) : null,
            ),
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
          ),
          if (preview.isNotEmpty) ...[
            const SizedBox(height: 3),
            Text(
              preview,
              style: AppType.body(context),
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
            ),
          ],
          if (filing.isNotEmpty) ...[
            const SizedBox(height: Space.sm),
            Text(
              filing.join('  ·  '),
              style: AppType.meta(context),
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
            ),
          ],
        ],
      ),
    );
  }


  /// The note's actions. A sheet rather than a menu under a dot, because six
  /// verbs behind a 19px glyph, once per row, is furniture, and the sheet has
  /// room to say what "Summarize" will actually do.
  Future<void> _noteSheet(Note note) async {
    HapticFeedback.selectionClick();
    final words = wordCount(note.content);
    await ledgerSheet<void>(
      context,
      title: note.title.trim().isEmpty ? 'Untitled note' : note.title.trim(),
      subtitle: words == 0
          ? 'Edited ${stampLong(note.updatedAt)}'
          : '${plural(words, 'word')} · ${stampLong(note.updatedAt)}',
      children: (ctx) => [
        SheetAction(
          icon: Icons.edit_outlined,
          label: 'Open in editor',
          onTap: () {
            Navigator.pop(ctx);
            _editNote(note);
          },
        ),
        SheetAction(
          icon: note.isPinned ? Icons.push_pin : Icons.push_pin_outlined,
          label: note.isPinned ? 'Unpin' : 'Pin to top',
          onTap: () {
            Navigator.pop(ctx);
            _togglePinNote(note);
          },
        ),
        SheetAction(
          icon: Icons.content_copy_rounded,
          label: 'Copy note',
          onTap: () {
            Navigator.pop(ctx);
            _copyNote(note);
          },
        ),
        if (note.content.trim().isNotEmpty && widget.processor.modelLoaded)
          SheetAction(
            icon: Icons.auto_awesome_rounded,
            label: 'Summarize',
            detail: 'Rewrites the note, on this phone',
            onTap: () {
              Navigator.pop(ctx);
              _summarizeNoteInPlace(note);
            },
          ),
        SheetAction(
          icon: Icons.copy_all_rounded,
          label: 'Duplicate',
          onTap: () {
            Navigator.pop(ctx);
            _duplicateNote(note);
          },
        ),
        SheetAction(
          icon: Icons.delete_outline_rounded,
          label: 'Delete note',
          danger: true,
          divided: false,
          onTap: () {
            Navigator.pop(ctx);
            _deleteNoteWithUndo(note);
          },
        ),
      ],
    );
  }

  Widget _buildEditor() {
    final note = _editingNote;
    final pinned = note?.isPinned == true;
    return Column(
      children: [
        SizedBox(height: MediaQuery.of(context).padding.top + Space.sm),
        // The title is the page's own name, so it is the field you type into
        // rather than a label above one.
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: Space.sm - 2),
          child: Row(
            children: [
              IconAction(
                icon: Icons.arrow_back_rounded,
                tooltip: 'Save and close',
                onPressed: _saveNote,
              ),
              Expanded(
                child: TextField(
                  controller: _titleController,
                  style: AppType.title(context),
                  cursorColor: accentOn(context, minRatio: 3),
                  decoration: InputDecoration(
                    isDense: true,
                    // A page title, not a boxed field: the theme's fill would
                    // put a tinted plate behind the note's own name.
                    filled: false,
                    hintText: 'Untitled',
                    hintStyle: AppType.title(context)
                        .copyWith(color: ink(context, 0.28)),
                    border: InputBorder.none,
                    contentPadding: const EdgeInsets.symmetric(vertical: 4),
                  ),
                  onChanged: (v) => _editingNote?.title = v,
                ),
              ),
              IconAction(
                icon: pinned ? Icons.push_pin_rounded : Icons.push_pin_outlined,
                tooltip: pinned ? 'Unpin' : 'Pin to top',
                tone: pinned ? accentOn(context, minRatio: 3) : null,
                onPressed: () {
                  if (note != null) _togglePinNote(note);
                },
              ),
              _buildEditorMenu(),
            ],
          ),
        ),

        // What the header knows about the note: how long it is now, and when it
        // was last put down. Live, because a word count that only updates when
        // you leave the screen is a word count nobody trusts.
        _editorMeta(note),

        const SizedBox(height: Space.md),
        const Rule(),

        // Four verbs in one tone. Four differently coloured buttons made the
        // row read as four unrelated features. One rule above them and none
        // below: a strip fenced on both sides reads as a toolbar bolted to the
        // page rather than as part of it.
        //
        // Dimmed with no model loaded, because none of the four can do anything
        // then. They stay tappable rather than being hidden: each one explains
        // itself with a toast pointing at Settings, which teaches more than an
        // action that silently is not there.
        Opacity(
          opacity: widget.processor.modelLoaded ? 1 : 0.45,
          child: SizedBox(
            height: 48,
            child: SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              physics: const BouncingScrollPhysics(),
              padding: const EdgeInsets.symmetric(horizontal: Space.sm - 2),
              child: Row(
                children: [
                  QuietAction(
                    label: 'Summarize',
                    icon: Icons.auto_awesome_rounded,
                    onPressed: _aiBusy ? null : () => _aiAction('summarize'),
                  ),
                  QuietAction(
                    label: 'Extract tasks',
                    icon: Icons.checklist_rounded,
                    onPressed: _aiBusy ? null : () => _aiAction('actions'),
                  ),
                  QuietAction(
                    label: 'Fix grammar',
                    icon: Icons.spellcheck_rounded,
                    onPressed: _aiBusy ? null : () => _aiAction('grammar'),
                  ),
                  QuietAction(
                    label: 'Expand',
                    icon: Icons.notes_rounded,
                    onPressed: _aiBusy ? null : () => _aiAction('expand'),
                  ),
                ],
              ),
            ),
          ),
        ),
        if (_aiBusy)
          const Padding(
            padding: EdgeInsets.symmetric(horizontal: Space.lg),
            child: ProcessingBar(label: 'Working on this note'),
          ),

        // The note itself, on the page. A box around a full page of text only
        // repeats the edge the screen already has. The bottom clears the glass
        // bar, so the last line you type is not written underneath it.
        Expanded(
          child: Padding(
            padding: EdgeInsets.fromLTRB(
                Space.lg, Space.md, Space.lg, navBarClearance(context)),
            child: TextField(
              controller: _contentController,
              maxLines: null,
              expands: true,
              textAlignVertical: TextAlignVertical.top,
              cursorColor: accentOn(context, minRatio: 3),
              style: TextStyle(
                color: ink(context, 0.94),
                fontSize: 15,
                height: 1.6,
                fontWeight: FontWeight.w500,
                letterSpacing: -0.1,
              ),
              decoration: InputDecoration(
                isDense: true,
                // The page is the paper. A filled box around the body would
                // draw an edge around every line the note grows by.
                filled: false,
                hintText: 'Write. Markdown works here.',
                hintStyle: TextStyle(
                  color: ink(context, 0.34),
                  fontSize: 15,
                  height: 1.6,
                  fontWeight: FontWeight.w500,
                ),
                border: InputBorder.none,
                contentPadding: EdgeInsets.zero,
              ),
              onChanged: (v) => _editingNote?.content = v,
            ),
          ),
        ),
      ],
    );
  }

  /// The header's second line: a live word count, when the note was last put
  /// down, and the tags, all on the title's own left edge.
  Widget _editorMeta(Note? note) {
    const inset = EdgeInsets.only(left: 50, right: Space.lg);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Padding(
          padding: inset,
          child: ValueListenableBuilder<TextEditingValue>(
            valueListenable: _contentController,
            builder: (_, value, __) {
              final words = wordCount(value.text);
              final edited = note == null ? null : stampLong(note.updatedAt);
              final line = <String>[
                words == 0 ? 'Empty' : plural(words, 'word'),
                if (edited != null) edited,
              ].join('  ·  ');
              return Text(line, style: AppType.meta(context));
            },
          ),
        ),
        const SizedBox(height: Space.sm),
        // Tags belong with the title, not with the verbs: they file the note,
        // they do not act on it.
        Padding(
          padding: inset,
          child: Row(
            children: [
              Icon(Icons.sell_outlined, size: 15, color: ink(context, 0.42)),
              const SizedBox(width: Space.sm),
              Expanded(
                child: TextField(
                  controller: _tagsController,
                  cursorColor: accentOn(context, minRatio: 3),
                  style: AppType.meta(context).copyWith(
                    color: ink(context, 0.86),
                    fontSize: 12.5,
                  ),
                  decoration: InputDecoration(
                    isDense: true,
                    // Sits on the meta line beside its own glyph. A fill here
                    // boxed the tags away from the icon that labels them.
                    filled: false,
                    hintText: 'Tags, comma separated',
                    hintStyle: AppType.meta(context).copyWith(fontSize: 12.5),
                    border: InputBorder.none,
                    contentPadding: EdgeInsets.zero,
                  ),
                  onChanged: (v) {
                    _editingNote?.tags = v
                        .split(',')
                        .map((t) => t.trim())
                        .where((t) => t.isNotEmpty)
                        .toList();
                  },
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }

  Widget _buildEditorMenu() {
    final note = _editingNote;
    if (note == null) return const SizedBox.shrink();
    return PopupMenuButton<String>(
      tooltip: 'More',
      iconSize: 20,
      padding: EdgeInsets.zero,
      position: PopupMenuPosition.under,
      constraints: const BoxConstraints(minWidth: 208),
      icon: Icon(Icons.more_horiz, size: 20, color: ink(context, 0.62)),
      onSelected: (v) {
        // The controllers hold the live text; mirror it back before acting.
        note.title = _titleController.text;
        note.content = _contentController.text;
        switch (v) {
          case 'copy':
            _copyNote(note);
          case 'duplicate':
            _duplicateNote(note);
          case 'count':
            final text = _contentController.text;
            _toast('${wordCount(text)} words · ${text.length} characters');
          case 'summarize':
            _aiAction('summarize');
          case 'delete':
            _closeEditorAndDelete(note);
        }
      },
      itemBuilder: (_) => [
        menuItem('copy', Icons.content_copy_rounded, 'Copy all'),
        if (widget.processor.modelLoaded)
          menuItem('summarize', Icons.auto_awesome_rounded, 'Summarize'),
        menuItem('duplicate', Icons.copy_all_rounded, 'Duplicate note'),
        menuItem('count', Icons.numbers_rounded, 'Word count'),
        menuItem('delete', Icons.delete_outline_rounded, 'Delete note',
            danger: true),
      ],
    );
  }

  /// Leaves the editor and removes the note, with one chance to take it back.
  void _closeEditorAndDelete(Note note) {
    final snapshot = note.toMap();
    appBackInterceptor.value = null;
    _noteBox.delete(note.id);
    setState(() {
      _isEditing = false;
      _editingNote = null;
    });
    _toast(
      'Note deleted',
      undo: () {
        _noteBox.put(snapshot['id'], snapshot);
        setState(() {});
      },
    );
  }

}

// ─────────────────────────────────────────────────────────────────────────────
// OCR / VOICE VIEW
// ─────────────────────────────────────────────────────────────────────────────

class OcrVoiceView extends StatefulWidget {
  final OllamaClipProcessor processor;
  final ImagePicker imagePicker;
  const OcrVoiceView({
    super.key,
    required this.processor,
    required this.imagePicker,
  });

  @override
  State<OcrVoiceView> createState() => _OcrVoiceViewState();
}

class _OcrVoiceViewState extends State<OcrVoiceView>
    with SingleTickerProviderStateMixin, AutomaticKeepAliveClientMixin {
  @override
  bool get wantKeepAlive => true;
  // ── Mode toggle ──────────────────────────────────────────────────────
  int _mode = 0; // 0 = OCR, 1 = Voice
  late final TabController _tabController;

  // ── OCR state ─────────────────────────────────────────────────────────
  String _extractedText = '';
  bool _isProcessing = false;
  File? _selectedImage;

  // ── Voice state ───────────────────────────────────────────────────────
  final _voiceRecorder = VoiceRecorderService();
  bool _isRecording = false;
  double _currentAmplitude = 0.0;
  StreamSubscription<double>? _ampSubscription;
  String _voiceTranscription = '';
  bool _isTranscribing = false;
  Timer? _recordingTimer;
  int _recordingSeconds = 0;

  // ── Result editing / AI state ────────────────────────────────────────
  /// Which result panel is in edit mode: 'ocr', 'voice', or null for neither.
  /// Recognition is never perfect, so the text is correctable before it is
  /// saved anywhere.
  String? _editingField;
  final _resultEditController = TextEditingController();
  bool _aiBusy = false;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 2, vsync: this);
    _tabController.addListener(() {
      if (!_tabController.indexIsChanging) {
        setState(() => _mode = _tabController.index);
      }
    });
    _voiceRecorder.init();
  }

  @override
  void dispose() {
    _tabController.dispose();
    _ampSubscription?.cancel();
    _recordingTimer?.cancel();
    _resultEditController.dispose();
    _voiceRecorder.dispose();
    super.dispose();
  }

  // ── OCR Methods ───────────────────────────────────────────────────────

  Future<void> _pickImage(ImageSource source) async {
    try {
      final picked = await widget.imagePicker.pickImage(
        source: source,
        maxWidth: 2048,
        maxHeight: 2048,
        imageQuality: 85,
      );
      if (picked == null) return;

      setState(() {
        _selectedImage = File(picked.path);
        _isProcessing = true;
        _extractedText = '';
      });
      await _performOCR(File(picked.path));
    } catch (e) {
      setState(() => _isProcessing = false);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Could not open that image: '
                '${e.toString().split('\n').first}'),
          ),
        );
      }
    }
  }

  Future<void> _performOCR(File imageFile) async {
    // Google ML Kit ships Android/iOS implementations only, so desktop
    // builds (macOS and Linux) cannot recognise text yet.
    if (defaultTargetPlatform != TargetPlatform.android &&
        defaultTargetPlatform != TargetPlatform.iOS) {
      setState(() => _isProcessing = false);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Text recognition is not available on this platform'),
          ),
        );
      }
      return;
    }

    try {
      // Real on-device OCR via Google ML Kit — runs fully offline.
      final inputImage = InputImage.fromFile(imageFile);
      final recognizer = TextRecognizer(script: TextRecognitionScript.latin);
      final recognized = await recognizer.processImage(inputImage);
      await recognizer.close();

      final rawText = recognized.text.trim();
      if (rawText.isEmpty) {
        setState(() => _isProcessing = false);
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              content: Text('No text detected in the image'),
            ),
          );
        }
        return;
      }

      final processed = await widget.processor.process(rawText);
      setState(() {
        _extractedText = processed;
        _isProcessing = false;
      });
    } catch (e) {
      setState(() => _isProcessing = false);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Could not read text from that image: '
                '${e.toString().split('\n').first}'),
          ),
        );
      }
    }
  }

  // ── Voice Recording Methods ───────────────────────────────────────────

  Future<void> _toggleRecording() async {
    if (_isRecording) {
      await _stopRecording();
    } else {
      await _startRecording();
    }
  }

  Future<void> _startRecording() async {
    // The Whisper engine behind transcription has no Linux implementation, so
    // the voice tab is disabled on this platform until the plugin catches up.
    if (defaultTargetPlatform == TargetPlatform.linux) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Voice transcription is not available on Linux yet'),
          ),
        );
      }
      return;
    }

    try {
      // Request microphone permission on mobile. On macOS the record engine
      // triggers its own system prompt (permission_handler_apple has no macOS
      // implementation), and other desktop platforms never reach this point.
      if (defaultTargetPlatform == TargetPlatform.android ||
          defaultTargetPlatform == TargetPlatform.iOS) {
        final hasPermission = await Permission.microphone.isGranted;
        if (!hasPermission) {
          final result = await Permission.microphone.request();
          if (!result.isGranted) {
            if (mounted) {
              ScaffoldMessenger.of(context).showSnackBar(
                const SnackBar(
                  content: Text('Microphone permission is required for voice notes'),
                ),
              );
            }
            return;
          }
        }
      }

      await _voiceRecorder.startRecording();

      // Stream amplitude for waveform
      _ampSubscription = _voiceRecorder.amplitudeStream.listen((amp) {
        setState(() => _currentAmplitude = amp);
      });

      // Timer for recording duration display
      _recordingSeconds = 0;
      _recordingTimer = Timer.periodic(const Duration(seconds: 1), (_) {
        setState(() => _recordingSeconds++);
      });

      setState(() {
        _isRecording = true;
        _voiceTranscription = '';
      });
    } catch (e) {
      setState(() => _isRecording = false);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Could not record: '
                '${e.toString().split('\n').first}'),
          ),
        );
      }
    }
  }

  Future<void> _stopRecording() async {
    final path = await _voiceRecorder.stopRecording();

    _ampSubscription?.cancel();
    _ampSubscription = null;
    _recordingTimer?.cancel();
    _recordingTimer = null;

    setState(() {
      _isRecording = false;
      _isTranscribing = true;
      _currentAmplitude = 0.0;
    });

    if (path == null || !File(path).existsSync()) {
      setState(() => _isTranscribing = false);
      return;
    }

    // Defensive: Whisper has no Linux engine, so recordings on that platform
    // can never be transcribed (the start path already blocks this earlier).
    if (defaultTargetPlatform == TargetPlatform.linux) {
      setState(() => _isTranscribing = false);
      return;
    }

    // Transcribe the recording on-device via Whisper, the same engine the
    // chat attachments and the chat voice input use. The transcription is the
    // product here, so it goes straight into the result: no model round-trip
    // that could rewrite what was actually said.
    try {
      const whisper = Whisper(
        model: WhisperModel.tiny,
        downloadHost:
            'https://hf-mirror.com/ggerganov/whisper.cpp/resolve/main',
      );
      final result = await whisper.transcribe(
        transcribeRequest: TranscribeRequest(
          audio: path,
          isNoTimestamps: true,
          isTranslate: false,
          language: 'en',
        ),
      ).timeout(const Duration(seconds: 90));

      if (!mounted) return;
      final text = result.text.trim();
      if (text.isEmpty) {
        setState(() => _isTranscribing = false);
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Whisper heard nothing clear enough to transcribe. '
                'Speak a little longer or closer to the mic.'),
          ),
        );
        return;
      }
      setState(() {
        _voiceTranscription = text;
        _isTranscribing = false;
      });
    } catch (e) {
      setState(() => _isTranscribing = false);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Could not transcribe that: '
                '${e.toString().split('\n').first}'),
          ),
        );
      }
    }
  }

  Future<void> _cancelRecording() async {
    await _voiceRecorder.cancelRecording();
    _ampSubscription?.cancel();
    _ampSubscription = null;
    _recordingTimer?.cancel();
    _recordingTimer = null;
    setState(() {
      _isRecording = false;
      _currentAmplitude = 0.0;
    });
  }

  String _formatDuration(int seconds) {
    final m = (seconds ~/ 60).toString().padLeft(2, '0');
    final s = (seconds % 60).toString().padLeft(2, '0');
    return '$m:$s';
  }

  void _copyResult(String text) {
    Clipboard.setData(ClipboardData(text: text));
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('Text copied to clipboard')),
    );
  }

  void _saveAsNote(String text, String title, List<String> tags) {
    final note = Note(
      title: title,
      content: text,
      tags: tags,
    );
    Hive.box(AppDefaults.hiveNoteBox).put(note.id, note.toMap());
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('Saved to Notes')),
    );
  }

  // ── Result actions ────────────────────────────────────────────────────

  /// One-line feedback, with an optional single-step undo.
  void _toast(String message, {VoidCallback? undo}) {
    if (!mounted) return;
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(
        SnackBar(
          content: Text(message),
          duration: Duration(seconds: undo == null ? 2 : 5),
          margin: snackBarMargin(context),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(Radii.control),
          ),
          action: undo == null
              ? null
              : SnackBarAction(label: 'Undo', onPressed: undo),
        ),
      );
  }

  String _resultText(String field) =>
      field == 'ocr' ? _extractedText : _voiceTranscription;

  void _setResultText(String field, String value) {
    if (field == 'ocr') {
      _extractedText = value;
    } else {
      _voiceTranscription = value;
    }
  }

  void _startEditingResult(String field) {
    _resultEditController.text = _resultText(field);
    setState(() => _editingField = field);
  }

  void _commitResultEdit(String field) {
    final edited = _resultEditController.text;
    setState(() {
      _setResultText(field, edited);
      _editingField = null;
    });
  }

  void _cancelResultEdit() => setState(() => _editingField = null);

  /// Appends a model-written summary under a heading, so the recognised text
  /// itself is never replaced by it.
  Future<void> _summarizeResult(String field) async {
    if (_aiBusy) return;
    final text = _resultText(field);
    if (text.trim().isEmpty) return;
    if (!widget.processor.modelLoaded) {
      _toast('Load a model in Settings first');
      return;
    }
    setState(() => _aiBusy = true);
    final result = await widget.processor.instruct(
      'Summarize the text below in three short bullet points. Keep names, '
      'numbers and dates exact. Output only the bullets.\n\n$text',
    );
    if (!mounted) return;
    setState(() => _aiBusy = false);
    if (result == null) {
      _toast('The model did not return a summary');
      return;
    }
    setState(() => _setResultText(field, '$text\n\n## Summary\n\n$result'));
    _toast('Summary added', undo: () {
      setState(() => _setResultText(field, text));
    });
  }

  Future<void> _sendResultToClips(String field) async {
    final text = _resultText(field);
    if (text.trim().isEmpty) return;
    // A dictated note is a deliberate act, so it files without the duplicate
    // sheet — but through the same redaction, formatting and titling.
    final entry = await buildClipEntry(text, widget.processor);
    Hive.box(AppDefaults.hiveClipBox).put(entry.id, entry.toMap());
    _toast('Added to Clips');
  }

  void _clearResult(String field) {
    final text = _resultText(field);
    final image = _selectedImage;
    setState(() {
      _setResultText(field, '');
      _editingField = null;
      if (field == 'ocr') _selectedImage = null;
    });
    _toast('Cleared', undo: () {
      setState(() {
        _setResultText(field, text);
        if (field == 'ocr') _selectedImage = image;
      });
    });
  }

  Future<void> _rerunOcr() async {
    final image = _selectedImage;
    if (image == null || _isProcessing) return;
    setState(() {
      _isProcessing = true;
      _extractedText = '';
      _editingField = null;
    });
    await _performOCR(image);
  }

  // ── Build ─────────────────────────────────────────────────────────────

  @override
  Widget build(BuildContext context) {
    super.build(context);
    // The whole page scrolls, head and all, so the two modes are read as one
    // screen with a switch rather than as a fixed chrome bar over a pane.
    return SingleChildScrollView(
      physics: const BouncingScrollPhysics(),
      padding: EdgeInsets.fromLTRB(
        Space.lg,
        MediaQuery.of(context).padding.top + Space.lg,
        Space.lg,
        navBarClearance(context),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Entrance(
            child: Masthead(
              title: 'OCR & Voice',
              lead: _mode == 0
                  ? 'Text out of a picture, recognised on this phone.'
                  : 'A recording, transcribed on this phone.',
            ),
          ),
          const SizedBox(height: Space.xl),
          Entrance(
            index: 1,
            child: TabRail(
              labels: const ['Scan', 'Voice'],
              index: _mode,
              onChanged: (i) => _tabController.animateTo(i),
            ),
          ),
          const SizedBox(height: Space.lg + 2),
          Entrance(
            index: 2,
            child: _mode == 0 ? _buildOcrTab() : _buildVoiceTab(),
          ),
        ],
      ),
    );
  }

  // ── OCR Tab ───────────────────────────────────────────────────────────

  Widget _buildOcrTab() {
    final blank =
        _extractedText.isEmpty && !_isProcessing && _selectedImage == null;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _scanPanel(),
        if (_isProcessing) _resultSkeleton('Reading the picture'),
        // The text itself, full length, scrolling with the page.
        if (_extractedText.isNotEmpty && !_isProcessing) ...[
          _buildResultHeader(
            label: 'Extracted text',
            field: 'ocr',
            canRerun: _selectedImage != null,
            onCopy: () => _copyResult(_extractedText),
            onSave: () => _saveAsNote(
              _extractedText,
              'OCR ${DateFormat('MMM d HH:mm').format(DateTime.now())}',
              ['OCR'],
            ),
          ),
          _buildResultBody('ocr'),
        ],
        if (blank)
          const StateBlock(
            icon: Icons.document_scanner_outlined,
            title: 'Nothing scanned yet',
            message: 'Point the camera at a page, a receipt, a whiteboard. '
                'Recognition runs on this phone and the picture never leaves it.',
          ),
      ],
    );
  }

  /// The camera as an instrument: the frame the picture will land in, and the
  /// two ways to fill it, on the screen's one raised surface. The edge turns
  /// accent once there is a picture in it.
  Widget _scanPanel() {
    final picked = _selectedImage;
    return Panel(
      accentEdge: picked != null,
      padding: const EdgeInsets.all(Space.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          ClipRRect(
            borderRadius: BorderRadius.circular(Radii.control),
            child: picked == null
                ? _scanFrame()
                : Image.file(
                    picked,
                    height: 168,
                    width: double.infinity,
                    fit: BoxFit.cover,
                  ),
          ),
          const SizedBox(height: Space.md),
          // One primary way in and one alternative, rather than two identical
          // cards that give the camera and the gallery equal billing.
          Row(
            children: [
              Expanded(
                child: PrimaryAction(
                  label: picked == null ? 'Take a picture' : 'Take another',
                  icon: Icons.photo_camera_rounded,
                  onPressed: _isProcessing
                      ? null
                      : () => _pickImage(ImageSource.camera),
                ),
              ),
              const SizedBox(width: Space.xs),
              QuietAction(
                label: 'Gallery',
                icon: Icons.photo_library_outlined,
                dense: true,
                onPressed: _isProcessing
                    ? null
                    : () => _pickImage(ImageSource.gallery),
              ),
            ],
          ),
        ],
      ),
    );
  }

  /// The empty frame. It states the size of the thing that is missing, which is
  /// more use than a blank gap where a picture will appear later.
  Widget _scanFrame() {
    return Container(
      height: 132,
      color: fill(context, strength: 1.5),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(
            Icons.crop_free_rounded,
            size: 24,
            color: ink(context, 0.30),
          ),
          const SizedBox(height: Space.sm),
          Text('No picture yet', style: AppType.meta(context)),
        ],
      ),
    );
  }

  /// Work in flight in the shape of the result it will become. A spinner says
  /// something is happening; this says what is arriving.
  Widget _resultSkeleton(String label) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SectionHeader(label, bottom: Space.md),
        const Pulse(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              SkeletonLine(widthFactor: 0.97),
              SizedBox(height: Space.sm),
              SkeletonLine(widthFactor: 0.93),
              SizedBox(height: Space.sm),
              SkeletonLine(widthFactor: 0.99),
              SizedBox(height: Space.sm),
              SkeletonLine(widthFactor: 0.54),
            ],
          ),
        ),
      ],
    );
  }

  // ── Voice Tab ─────────────────────────────────────────────────────────

  Widget _buildVoiceTab() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _recorderPanel(),
        if (_isTranscribing) _resultSkeleton('Transcribing'),
        // The transcript, full length, scrolling with the page.
        if (_voiceTranscription.isNotEmpty && !_isTranscribing) ...[
          _buildResultHeader(
            label: 'Transcript',
            field: 'voice',
            onCopy: () => _copyResult(_voiceTranscription),
            onSave: () => _saveAsNote(
              _voiceTranscription,
              'Voice note ${DateFormat('MMM d HH:mm').format(DateTime.now())}',
              ['Voice'],
            ),
          ),
          _buildResultBody('voice'),
        ],
        if (!_isRecording && !_isTranscribing && _voiceTranscription.isEmpty)
          _buildVoiceEmptyState(),
      ],
    );
  }

  /// The recorder, as one object: the level, the clock and the control that
  /// starts and stops them, on the screen's one raised surface. Everything
  /// centred inside it, because this is the one place on the page where the
  /// thing you reach for should be under your thumb rather than at a margin.
  Widget _recorderPanel() {
    final rec = _isRecording;
    return Panel(
      accentEdge: rec,
      padding: const EdgeInsets.fromLTRB(
          Space.lg, Space.lg, Space.lg, Space.lg + 2),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (rec) ...[
            _buildWaveform(),
            const SizedBox(height: Space.md),
            Center(
              child: Text(
                _formatDuration(_recordingSeconds),
                style: AppType.figure(context)
                    .copyWith(fontSize: 34, letterSpacing: -1.4),
              ),
            ),
            const SizedBox(height: 3),
            Center(child: Text('Recording', style: AppType.meta(context))),
            const SizedBox(height: Space.lg),
          ] else ...[
            Center(
              child: Text(
                _isTranscribing
                    ? 'Working through the recording'
                    : 'Hold the phone close and talk',
                style: AppType.body(context),
                textAlign: TextAlign.center,
              ),
            ),
            const SizedBox(height: Space.lg),
          ],
          // The one big round control in the app, and the only place a shape
          // that large is earned: it is the thing you reach for without looking.
          Center(
            child: rec
                ? Column(
                    children: [
                      _circleControl(
                        icon: Icons.stop_rounded,
                        tooltip: 'Stop and transcribe',
                        onTap: _stopRecording,
                        size: 68,
                      ),
                      const SizedBox(height: Space.sm),
                      QuietAction(
                        label: 'Discard',
                        icon: Icons.close_rounded,
                        danger: true,
                        onPressed: _cancelRecording,
                      ),
                    ],
                  )
                : _circleControl(
                    icon: Icons.mic_rounded,
                    tooltip: 'Start recording',
                    onTap: _isTranscribing ? null : _toggleRecording,
                    size: 76,
                  ),
          ),
        ],
      ),
    );
  }

  /// A filled accent circle. Contact shadow, no bloom, and it scales on press
  /// rather than rippling.
  Widget _circleControl({
    required IconData icon,
    required String tooltip,
    required VoidCallback? onTap,
    required double size,
  }) {
    final scheme = Theme.of(context).colorScheme;
    final fill = onTap == null
        ? Color.lerp(scheme.primary, scheme.surface, 0.62)!
        : scheme.primary;
    return Semantics(
      button: true,
      label: tooltip,
      child: Tooltip(
        message: tooltip,
        child: Pressable(
          onTap: onTap,
          scale: 0.94,
          child: Container(
            width: size,
            height: size,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: fill,
              boxShadow: onTap == null
                  ? null
                  : [
                      BoxShadow(
                        color: Color.lerp(Colors.black, fill, 0.35)!.withValues(
                          alpha: scheme.brightness == Brightness.dark
                              ? 0.36
                              : 0.20,
                        ),
                        blurRadius: 14,
                        offset: const Offset(0, 5),
                      ),
                    ],
            ),
            child: Icon(
              icon,
              size: size * 0.42,
              color: legibleAccent(scheme.onPrimary, fill),
            ),
          ),
        ),
      ),
    );
  }

  // ── Shared UI Components ──────────────────────────────────────────────

  Widget _buildWaveform() {
    final accent = accentOn(context, minRatio: 3);
    return SizedBox(
      height: 76,
      width: double.infinity,
      child: Row(
        mainAxisAlignment: MainAxisAlignment.center,
        crossAxisAlignment: CrossAxisAlignment.center,
        children: List.generate(40, (i) {
          const baseHeight = 10.0;
          const maxHeight = 56.0;
          final distance = (i - 20).abs() / 20.0;
          final amplitudeFactor = _currentAmplitude.clamp(0.05, 1.0);
          final height = baseHeight +
              (maxHeight - baseHeight) * amplitudeFactor * (1.0 - distance * 0.6);
          return Container(
            width: 3,
            margin: const EdgeInsets.symmetric(horizontal: 1.5),
            height: height,
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(Radii.pill),
              color: accent.withValues(
                  alpha: 0.26 + amplitudeFactor * 0.7 * (1.0 - distance * 0.5)),
            ),
          );
        }),
      ),
    );
  }

  Widget _buildVoiceEmptyState() {
    return const StateBlock(
      icon: Icons.graphic_eq_rounded,
      title: 'Nothing recorded yet',
      message: 'Say it out loud and it comes back as text you can edit. '
          'Whisper fetches its model the first time, then works offline.',
    );
  }

  Widget _buildResultHeader({
    required String label,
    required VoidCallback onCopy,
    required VoidCallback onSave,
    required String field,
    bool canRerun = false,
  }) {
    final editing = _editingField == field;
    final canAsk = widget.processor.modelLoaded && !_aiBusy;
    final text = _resultText(field);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SectionHeader(
          label,
          top: Space.xl,
          bottom: Space.xs,
          trailing: IconAction(
            icon: Icons.more_horiz,
            tooltip: 'Result options',
            size: 19,
            onPressed: () => _resultSheet(
              field: field,
              label: label,
              canRerun: canRerun,
            ),
          ),
        ),
        // How much came back. A recognised page and a recognised receipt look
        // the same until something says how long they are.
        Text(
          '${plural(wordCount(text), 'word')}  ·  '
          '${plural(text.length, 'character')}',
          style: AppType.meta(context),
        ),
        if (_aiBusy) const ProcessingBar(label: 'Asking the model'),
        const SizedBox(height: Space.xs),
        // The actions sit on the page, all in one tone. Which one matters is
        // said by the order they are read in, not by three different colours.
        // What is missing from this row is in the sheet, and nothing is in both.
        Transform.translate(
          offset: const Offset(-10, 0),
          child: Wrap(
            crossAxisAlignment: WrapCrossAlignment.center,
            children: editing
                ? [
                    QuietAction(
                      label: 'Save changes',
                      icon: Icons.check_rounded,
                      onPressed: () => _commitResultEdit(field),
                    ),
                    QuietAction(
                      label: 'Cancel',
                      icon: Icons.close_rounded,
                      onPressed: _cancelResultEdit,
                    ),
                  ]
                : [
                    QuietAction(
                      label: 'Copy',
                      icon: Icons.content_copy_rounded,
                      onPressed: onCopy,
                    ),
                    QuietAction(
                      label: 'Save as note',
                      icon: Icons.note_add_outlined,
                      onPressed: onSave,
                    ),
                    if (canAsk)
                      QuietAction(
                        label: 'Summarize',
                        icon: Icons.auto_awesome_rounded,
                        onPressed: () => _summarizeResult(field),
                      ),
                  ],
          ),
        ),
      ],
    );
  }

  /// Everything the inline row left out. A sheet, not a menu under a dot: the
  /// verbs here change or throw away the text, and they deserve room to say so.
  Future<void> _resultSheet({
    required String field,
    required String label,
    required bool canRerun,
  }) async {
    HapticFeedback.selectionClick();
    await ledgerSheet<void>(
      context,
      title: label,
      subtitle: 'Recognised on this phone',
      children: (ctx) => [
        if (_editingField != field)
          SheetAction(
            icon: Icons.edit_outlined,
            label: 'Edit text',
            detail: 'Fix what recognition got wrong',
            onTap: () {
              Navigator.pop(ctx);
              _startEditingResult(field);
            },
          ),
        SheetAction(
          icon: Icons.content_paste_rounded,
          label: 'Send to Clips',
          onTap: () {
            Navigator.pop(ctx);
            _sendResultToClips(field);
          },
        ),
        if (canRerun)
          SheetAction(
            icon: Icons.refresh_rounded,
            label: 'Scan again',
            detail: 'Run recognition over the same picture',
            onTap: () {
              Navigator.pop(ctx);
              _rerunOcr();
            },
          ),
        SheetAction(
          icon: Icons.delete_outline_rounded,
          label: 'Clear result',
          danger: true,
          divided: false,
          onTap: () {
            Navigator.pop(ctx);
            _clearResult(field);
          },
        ),
      ],
    );
  }

  /// The result body: rendered markdown, or a plain editor while the user is
  /// correcting what recognition got wrong. Recognised text is the content of
  /// this screen, so it sits on the page rather than inside a plate.
  Widget _buildResultBody(String field) {
    if (_editingField == field) {
      return Padding(
        padding: const EdgeInsets.only(top: Space.sm),
        child: InlineField(
          controller: _resultEditController,
          hint: 'Correct the text',
          maxLines: 14,
        ),
      );
    }
    return Padding(
      padding: const EdgeInsets.only(top: Space.xs),
      child: MarkdownBody(
        data: _resultText(field),
        styleSheet: ledgerMarkdown(context, size: 14),
      ),
    );
  }
}

// ─────────────────────────────────────────────────────────────────────────────
// SETTINGS VIEW
// ─────────────────────────────────────────────────────────────────────────────

class SettingsView extends StatefulWidget {
  final OllamaClipProcessor processor;
  final AiConnectionService connection;
  final NavBarConfig navConfig;
  final ValueChanged<NavBarConfig>? onNavConfigChanged;
  final VoidCallback? onThemeChanged;

  /// The background capture service, wired the same way the Clips tab has it.
  /// Both switches write the same setting, so whichever one you find first is
  /// the one that works.
  final bool isServiceRunning;
  final VoidCallback? onStartService;
  final VoidCallback? onStopService;

  /// Locks the app immediately. Offered on the Data tab only while a PIN is
  /// set; the shell owns the lock, settings only asks for it.
  final VoidCallback? onLockNow;
  const SettingsView({
    super.key,
    required this.processor,
    required this.connection,
    NavBarConfig? navConfig,
    this.onNavConfigChanged,
    this.onThemeChanged,
    this.isServiceRunning = false,
    this.onStartService,
    this.onStopService,
    this.onLockNow,
  }) : navConfig = navConfig ?? const NavBarConfig();

  @override
  State<SettingsView> createState() => _SettingsViewState();
}

class _SettingsViewState extends State<SettingsView>
    with AutomaticKeepAliveClientMixin {
  @override
  bool get wantKeepAlive => true;
  late Box _settings;
  bool _isChecking = false;
  String _connectionStatus = 'Checking';
  List<FileSystemEntity> _downloadedModels = [];
  bool _isLoadingModels = false;
  late NavBarConfig _navCfg;

  /// Which slot the *preview* bar has selected. The preview is a live bar, not
  /// a picture of one, so it needs its own selection to move: driving the real
  /// tab index from here would change the page under the settings you are
  /// tuning. Clamped against the destination list on every read, so a reorder
  /// can never leave it pointing past the end.
  int _previewSlot = 0;

  /// True while the export markdown is being built and handed to the system
  /// save dialog, so a second tap cannot start a parallel export.
  bool _isExporting = false;

  // Tab navigation for settings categories
  int _settingsTab = 0;
  static const _tabLabels = ['AI', 'Themes & UI', 'System', 'Data'];

  @override
  void initState() {
    super.initState();
    _navCfg = widget.navConfig;
    _settings = Hive.box(AppDefaults.hiveSettingsBox);
    _connectionStatus = widget.processor.modelLoaded
        ? 'Model loaded'
        : 'No model loaded';
    _loadDownloadedModels();
  }

  Future<void> _loadDownloadedModels() async {
    setState(() => _isLoadingModels = true);
    try {
      _downloadedModels = await widget.processor.listDownloadedModels();
    } catch (_) {}
    setState(() => _isLoadingModels = false);
  }

  Widget _buildBrowseLocalButton() {
    return QuietAction(
      label: 'Browse this device for a GGUF file',
      icon: Icons.folder_open_rounded,
      onPressed: _browseLocalModel,
    );
  }

  void _confirmDeleteModel(String path, String name, bool isLoaded) {
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Delete this model?'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              name,
              overflow: TextOverflow.ellipsis,
              style: AppType.body(ctx).copyWith(fontWeight: FontWeight.w600),
            ),
            const SizedBox(height: Space.sm),
            Text(
              isLoaded
                  ? 'It is loaded right now, so it will be unloaded first. The '
                      'file is removed from this phone and cannot be recovered.'
                  : 'The file is removed from this phone and cannot be '
                      'recovered. You can download it again later.',
              style: DefaultTextStyle.of(ctx).style,
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('Keep it'),
          ),
          TextButton(
            style: TextButton.styleFrom(
              foregroundColor: legibleAccent(
                Semantic.danger,
                Theme.of(ctx).colorScheme.surface,
              ),
            ),
            onPressed: () async {
              Navigator.pop(ctx);
              try {
                await widget.processor.deleteModel(path);
                await _loadDownloadedModels();
                if (mounted) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    SnackBar(content: Text('Deleted $name')),
                  );
                }
              } catch (e) {
                if (mounted) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    SnackBar(
                      content: Text('Could not delete it: '
                          '${e.toString().split('\n').first}'),
                    ),
                  );
                }
              }
            },
            child: const Text('Delete', style: TextStyle(fontWeight: FontWeight.w700)),
          ),
        ],
      ),
    );
  }

  Future<void> _browseLocalModel() async {
    try {
      FilePickerResult? result;
      try {
        result = await FilePicker.platform.pickFiles(
          type: FileType.custom,
          allowedExtensions: ['gguf'],
          dialogTitle: 'Select a GGUF model file',
        );
      } on PlatformException catch (_) {
        // Android doesn't recognize '.gguf' in its MIME/extension mapping and
        // throws "Unsupported filter". Fall back to any-file so the user can
        // still pick a GGUF from storage.
        result = await FilePicker.platform.pickFiles(
          type: FileType.any,
          dialogTitle: 'Select a GGUF model file',
        );
      }
      if (result == null || result.files.isEmpty) return;

      final picked = result.files.first;
      if (picked.path == null) return;

      setState(() {
        _isChecking = true;
        _connectionStatus = 'Importing the model';
      });

      // Copy file to app models directory
      final modelsDir = await widget.processor.llm.getModelsDirectory();
      final destPath = '${modelsDir.path}${Platform.pathSeparator}${picked.name}';

      // Check if already exists
      final destFile = File(destPath);
      if (!await destFile.exists()) {
        await File(picked.path!).copy(destPath);
      }

      await _loadDownloadedModels();
      await _loadModelFile(destPath);

      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Imported ${picked.name}'),
          ),
        );
      }
    } catch (e) {
      debugPrint('Browse local model error: $e');
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Could not import that file: '
                '${e.toString().split('\n').first}'),
          ),
        );
      }
    }
    setState(() {
      _isChecking = false;
      _connectionStatus = widget.processor.modelLoaded ? 'Model loaded' : 'No model loaded';
    });
  }

  Future<void> _loadModelFile(String path) async {
    setState(() {
      _isChecking = true;
      _connectionStatus = 'Loading the model';
    });
    try {
      final ok = await widget.processor.loadModel(path);
      setState(() {
        _isChecking = false;
        _connectionStatus = ok ? 'Model loaded' : 'Could not load it';
      });
      if (ok) {
        Hive.box(AppDefaults.hiveSettingsBox)
            .put(AppDefaults.lastModelPathKey, path);
      }
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(ok ? 'Model loaded on this phone' : 'Could not load the model'),
          ),
        );
      }
    } catch (e, stackTrace) {
      debugPrint('[Settings] Model load error: $e');
      debugPrint('[Settings] Stack: $stackTrace');
      setState(() {
        _isChecking = false;
        _connectionStatus = 'Could not load it';
      });
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Could not load the model: '
                '${e.toString().split('\n').first}'),
            duration: const Duration(seconds: 5),
          ),
        );
      }
    }
  }

  Future<void> _unloadModel() async {
    await widget.processor.unloadModel();
    setState(() => _connectionStatus = 'No model loaded');
    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Model unloaded')),
      );
    }
  }

  Future<void> _downloadAndLoadModel(ModelCatalogEntry catalog) async {
    // if already downloading, do nothing (progress shown from global notifier)
    if (widget.processor.llm.isDownloading(catalog.id)) return;
    try {
      final path = await widget.processor.llm.startDownload(catalog);
      await _loadDownloadedModels();
      await _loadModelFile(path);
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Could not finish the download: '
                '${e.toString().split('\n').first}'),
          ),
        );
      }
    }
  }



  /// Clears the onboarding flag and re-presents the Hello + tutorial flow.
  void _replayTutorial() {
    _settings.put('onboardingCompleted', false);
    Navigator.of(context).pushReplacementNamed('onboarding');
  }

  /// Asks before a wipe. Clearing a whole box has no undo, so unlike the
  /// single-item deletes elsewhere in the app these tiles confirm first rather
  /// than acting on one stray tap.
  ///
  /// Every visual decision comes from `dialogTheme`, so this reads identically
  /// to the model delete on the AI tab. It used to set its own surface, its own
  /// radius and its own three type sizes, and carried a red glyph beside the
  /// question, which made the same confirmation arrive in two different styles
  /// depending on which tab you asked from.
  Future<bool> _confirmWipe({
    required String title,
    required String message,
    required String confirmLabel,
  }) async {
    final result = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(title),
        content: Text(message),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Cancel'),
          ),
          TextButton(
            style: TextButton.styleFrom(
              foregroundColor: legibleAccent(
                  Semantic.danger, Theme.of(ctx).colorScheme.surface),
            ),
            onPressed: () => Navigator.pop(ctx, true),
            child: Text(confirmLabel,
                style: const TextStyle(fontWeight: FontWeight.w700)),
          ),
        ],
      ),
    );
    return result ?? false;
  }

  Future<void> _clearClipHistory() async {
    final box = Hive.box(AppDefaults.hiveClipBox);
    final count = box.length;
    if (count == 0) {
      _dataToast('No clips to clear');
      return;
    }
    final ok = await _confirmWipe(
      title: 'Clear clip history?',
      message: '$count ${count == 1 ? 'clip' : 'clips'} will be deleted, '
          'including pinned ones. This cannot be undone.',
      confirmLabel: 'Clear clips',
    );
    if (!ok) return;
    await box.clear();
    _refreshStorageFigures();
    _dataToast('Clip history cleared');
  }

  Future<void> _clearNotes() async {
    final box = Hive.box(AppDefaults.hiveNoteBox);
    final count = box.length;
    if (count == 0) {
      _dataToast('No notes to clear');
      return;
    }
    final ok = await _confirmWipe(
      title: 'Clear all notes?',
      message: '$count ${count == 1 ? 'note' : 'notes'} will be deleted, '
          'including pinned ones. This cannot be undone.',
      confirmLabel: 'Clear notes',
    );
    if (!ok) return;
    await box.clear();
    _refreshStorageFigures();
    _dataToast('Notes cleared');
  }

  /// Wipes saved chat transcripts. Kept separate from clips and notes because a
  /// conversation can hold anything the user pasted into it, so somebody
  /// clearing their history usually wants exactly this and nothing else.
  Future<void> _clearChatHistory() async {
    if (!Hive.isBoxOpen(AppDefaults.hiveChatBox)) {
      _dataToast('Chat storage is unavailable');
      return;
    }
    final box = Hive.box(AppDefaults.hiveChatBox);
    final count = box.length;
    if (count == 0) {
      _dataToast('No saved chats to clear');
      return;
    }
    final ok = await _confirmWipe(
      title: 'Clear chat history?',
      message: '$count ${count == 1 ? 'conversation' : 'conversations'} will '
          'be deleted. The chat open right now stays on screen until you leave '
          'it. This cannot be undone.',
      confirmLabel: 'Clear chats',
    );
    if (!ok) return;
    await box.clear();
    _refreshStorageFigures();
    _dataToast('Chat history cleared');
  }

  /// The storage panel is a [FutureBuilder], so it only re-reads the box counts
  /// when this State rebuilds.
  void _refreshStorageFigures() {
    if (mounted) setState(() {});
  }

  // ── Export ────────────────────────────────────────────────────────────

  /// Builds one markdown document holding everything stored locally: clips,
  /// notes and saved chats. Plain markdown rather than a private format, so a
  /// backup stays readable in any editor and this app is not the only thing
  /// that can open it.
  String _buildExportMarkdown() {
    final stamp = DateFormat('yyyy-MM-dd HH:mm').format(DateTime.now());
    final out = StringBuffer()
      ..writeln('# ClipSyncAI export')
      ..writeln()
      ..writeln('Exported $stamp. Everything below came off this device only.')
      ..writeln();

    // ── Clips ──
    final clips = <ClipEntry>[];
    for (final v in Hive.box(AppDefaults.hiveClipBox).values) {
      if (v is Map) {
        try {
          clips.add(ClipEntry.fromMap(Map<String, dynamic>.from(v)));
        } catch (_) {
          // Skip an unreadable row rather than abandoning the export.
        }
      }
    }
    clips.sort((a, b) => b.timestamp.compareTo(a.timestamp));
    out.writeln('## Clips (${clips.length})');
    out.writeln();
    if (clips.isEmpty) out.writeln('_None._\n');
    for (final c in clips) {
      final when = DateFormat('yyyy-MM-dd HH:mm').format(c.timestamp);
      out.writeln('### $when${c.isPinned ? ' · pinned' : ''}');
      out.writeln();
      out.writeln('**Original**');
      out.writeln();
      out.writeln(c.rawText.trim().isEmpty ? '_empty_' : c.rawText.trim());
      out.writeln();
      if (c.processedMarkdown.trim().isNotEmpty &&
          c.processedMarkdown.trim() != c.rawText.trim()) {
        out.writeln('**Processed**');
        out.writeln();
        out.writeln(c.processedMarkdown.trim());
        out.writeln();
      }
    }

    // ── Notes ──
    final notes = <Note>[];
    for (final v in Hive.box(AppDefaults.hiveNoteBox).values) {
      if (v is Map) {
        try {
          notes.add(Note.fromMap(Map<String, dynamic>.from(v)));
        } catch (_) {}
      }
    }
    notes.sort((a, b) => b.updatedAt.compareTo(a.updatedAt));
    out.writeln('## Notes (${notes.length})');
    out.writeln();
    if (notes.isEmpty) out.writeln('_None._\n');
    for (final n in notes) {
      final title = n.title.trim().isEmpty ? 'Untitled' : n.title.trim();
      out.writeln('### $title${n.isPinned ? ' · pinned' : ''}');
      out.writeln();
      if (n.tags.isNotEmpty) {
        out.writeln('Tags: ${n.tags.map((t) => '`$t`').join(', ')}');
        out.writeln();
      }
      out.writeln(
          'Edited ${DateFormat('yyyy-MM-dd HH:mm').format(n.updatedAt)}');
      out.writeln();
      out.writeln(n.content.trim().isEmpty ? '_empty_' : n.content.trim());
      out.writeln();
    }

    // ── Chats ──
    final sessions = ChatStore.instance?.all() ?? const <ChatSession>[];
    out.writeln('## Chats (${sessions.length})');
    out.writeln();
    if (sessions.isEmpty) out.writeln('_None._\n');
    for (final s in sessions) {
      out.writeln('### ${s.title}${s.isPinned ? ' · pinned' : ''}');
      out.writeln();
      out.writeln(
          'Updated ${DateFormat('yyyy-MM-dd HH:mm').format(s.updatedAt)} · '
          '${s.messages.length} messages');
      out.writeln();
      out.writeln(s.transcript.trim());
      out.writeln();
    }

    return out.toString();
  }

  /// Hands the export to the system save dialog. The user picks the
  /// destination, so nothing leaves the device unless they choose to put it
  /// somewhere shared.
  Future<void> _exportData() async {
    if (_isExporting) return;
    setState(() => _isExporting = true);
    try {
      final markdown = _buildExportMarkdown();
      final bytes = Uint8List.fromList(utf8.encode(markdown));
      final name =
          'clipsyncai-export-${DateFormat('yyyyMMdd-HHmm').format(DateTime.now())}.md';
      final saved = await FilePicker.platform.saveFile(
        dialogTitle: 'Save ClipSyncAI export',
        fileName: name,
        bytes: bytes,
      );
      if (!mounted) return;
      _dataToast(saved == null
          ? 'Export cancelled'
          : 'Exported ${_readableSize(bytes.length)}');
    } catch (e) {
      if (mounted) {
        _dataToast('Could not export: '
            '${e.toString().split('\n').first}');
      }
    } finally {
      if (mounted) setState(() => _isExporting = false);
    }
  }

  static String _readableSize(int bytes) {
    if (bytes < 1024) return '$bytes B';
    if (bytes < 1024 * 1024) return '${(bytes / 1024).toStringAsFixed(1)} KB';
    if (bytes < 1024 * 1024 * 1024) {
      return '${(bytes / (1024 * 1024)).toStringAsFixed(1)} MB';
    }
    // Model weights run to several gigabytes, and '2458.3 MB' is a number nobody
    // reads as a size.
    return '${(bytes / (1024 * 1024 * 1024)).toStringAsFixed(2)} GB';
  }

  void _dataToast(String message) {
    if (!mounted) return;
    final messenger = ScaffoldMessenger.of(context);
    messenger.hideCurrentSnackBar();
    messenger.showSnackBar(
      SnackBar(
        content: Text(message),
        duration: const Duration(seconds: 2),
        shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(Radii.control)),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SizedBox(height: MediaQuery.of(context).padding.top + Space.lg),
        // The same head every other tab carries, with the tab's own sentence as
        // the lead. A settings page is the last place that should look like it
        // came from a different app.
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: Space.lg),
          child: Masthead(
            title: 'Settings',
            lead: _settingsSubtitles[
                _settingsTab.clamp(0, _settingsSubtitles.length - 1)],
          ),
        ),
        Padding(
          padding: const EdgeInsets.fromLTRB(Space.lg, Space.xl, Space.lg, 0),
          child: TabRail(
            labels: _tabLabels,
            index: _settingsTab,
            onChanged: (i) {
              if (i == _settingsTab) return;
              HapticFeedback.selectionClick();
              setState(() => _settingsTab = i);
            },
          ),
        ),
        Expanded(
          child: ListView(
            physics: const BouncingScrollPhysics(),
            padding: EdgeInsets.fromLTRB(
                Space.lg, 0, Space.lg, navBarClearance(context)),
            children: _buildTabContent(),
          ),
        ),
      ],
    );
  }

  /// What each tab is for, said once at the top instead of repeated as a
  /// caption under every group inside it.
  static const _settingsSubtitles = [
    'The local model, and what it is allowed to do',
    'Colour, contrast, and the shape of the shell',
    'Permissions, background work, and this device',
    'Everything stored on this phone, and how to get it out',
  ];

  List<Widget> _buildTabContent() {
    switch (_settingsTab) {
      case 0: return _buildAITab();
      case 1: return _buildThemeTab();
      case 2: return _buildSystemTab();
      case 3: return _buildDataTab();
      default: return _buildAITab();
    }
  }

  // ════════════════════════════════════════════════════════════════════════════
  // TAB 1: AI MODELS
  // ════════════════════════════════════════════════════════════════════════════

  List<Widget> _buildAITab() {
    return [
      const SizedBox(height: Space.lg),
      _connectionPanel(),
      _enginePanel(),

      SectionHeader(
        'On this phone',
        count: _downloadedModels.isEmpty ? null : '${_downloadedModels.length}',
      ),
      if (_downloadedModels.isEmpty && !_isLoadingModels)
        const StateBlock(
          compact: true,
          icon: Icons.folder_open_rounded,
          title: 'No models yet',
          message: 'Download one below, or point the app at a GGUF file you '
              'already have on this device.',
        ),
      ..._downloadedModels.map(_modelFileRow),
      Padding(
        padding: const EdgeInsets.only(top: Space.sm),
        child: Transform.translate(
          offset: const Offset(-10, 0),
          child: _buildBrowseLocalButton(),
        ),
      ),

      const SectionHeader('Available to download'),
      // Downloads are tracked app-level (they survive screen changes), so this
      // listens to the service's notifier to reflect live progress.
      ValueListenableBuilder<Map<String, DownloadState>>(
        valueListenable: widget.processor.llm.downloadStates,
        builder: (context, states, _) {
          return Column(
            children: [
              for (final catalog in kModelCatalog)
                FutureBuilder<bool>(
                  future: widget.processor.llm.isModelDownloaded(catalog),
                  builder: (context, snap) => _catalogRow(
                    catalog,
                    downloaded: snap.data ?? false,
                    state: states[catalog.id],
                  ),
                ),
            ],
          );
        },
      ),
      const SizedBox(height: Space.lg),
    ];
  }

  /// External inference is opt-in. The embedded model remains available below,
  /// while this compact panel makes the active destination explicit before chat
  /// sends any clipboard or attachment text to it.
  Widget _connectionPanel() {
    return ValueListenableBuilder<AiConnectionConfig>(
      valueListenable: widget.connection.config,
      builder: (context, connection, _) => Panel(
        padding: const EdgeInsets.all(Space.lg),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(
              connection.provider == AiProvider.ollama
                  ? Icons.memory_rounded
                  : Icons.key_rounded,
              color: accentOn(context, minRatio: 3),
              size: 20,
            ),
            const SizedBox(width: Space.md),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    connection.isConfigured
                        ? '${connection.displayName} · ${connection.model}'
                        : 'Connect a model server',
                    style: AppType.heading(context),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                  ),
                  const SizedBox(height: 3),
                  Text(
                    connection.isConfigured
                        ? connection.provider == AiProvider.ollama
                            ? 'Chat uses your Ollama server when configured.'
                            : 'Chat uses ${connection.provider.label} through an '
                                'OpenAI-compatible endpoint.'
                        : 'Use local Ollama or connect a cloud model or custom '
                            'gateway.',
                    style: AppType.body(context),
                  ),
                  const SizedBox(height: Space.sm),
                  Transform.translate(
                    offset: const Offset(-10, 0),
                    child: QuietAction(
                      label: connection.isConfigured ? 'Edit connection' : 'Set up connection',
                      icon: Icons.tune_rounded,
                      dense: true,
                      onPressed: _editConnection,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _editConnection() async {
    final current = widget.connection.config.value;
    final endpoint = TextEditingController(text: current.endpoint);
    final model = TextEditingController(text: current.model);
    final name = TextEditingController(text: current.displayName);
    final key = TextEditingController();
    var provider = current.provider;
    var busy = false;
    String? feedback;
    // A little wider than before: room for a provider list, a server, a model
    // and a key without the dialog feeling like a slot window.
    final dialogWidth = (MediaQuery.sizeOf(context).width - 48)
        .clamp(320.0, 560.0)
        .toDouble();
    await showDialog<void>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setDialogState) => AlertDialog(
          title: const Text('Model connection'),
          // AlertDialog caps at 280 wide unless given an explicit constraint,
          // and its default 40px side insets starve it before that. Give it
          // the room we computed above and shrink the side margin so the
          // wider window actually reaches the screen.
          constraints: BoxConstraints(maxWidth: dialogWidth),
          insetPadding: const EdgeInsets.symmetric(horizontal: 20, vertical: 28),
          content: SingleChildScrollView(
            child: SizedBox(
              width: dialogWidth,
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  DropdownButtonFormField<AiProvider>(
                    value: provider,
                    decoration: const InputDecoration(labelText: 'Provider'),
                    items: [
                      for (final option in AiProvider.values)
                        DropdownMenuItem(
                          value: option,
                          child: Text(option.label),
                        ),
                    ],
                    onChanged: busy
                        ? null
                        : (value) => setDialogState(() {
                              final previous = provider;
                              provider = value!;
                              // Fill the preset server until the user has typed
                              // their own address.
                              if (endpoint.text.isEmpty ||
                                  (previous != provider &&
                                      endpoint.text == previous.defaultUrl)) {
                                endpoint.text = provider.defaultUrl;
                              }
                            }),
                  ),
                  const SizedBox(height: Space.sm),
                  TextField(
                      controller: name,
                      decoration: const InputDecoration(labelText: 'Connection name')),
                  const SizedBox(height: Space.sm),
                  TextField(
                    controller: endpoint,
                    keyboardType: TextInputType.url,
                    decoration: InputDecoration(
                      labelText: 'Server URL',
                      hintText: provider == AiProvider.customGateway
                          ? 'http://your-gateway:3000/v1'
                          : (provider.hint ?? provider.defaultUrl),
                    ),
                  ),
                  const SizedBox(height: Space.sm),
                  TextField(
                      controller: model,
                      decoration: const InputDecoration(
                          labelText: 'Model name',
                          hintText: 'Choose or type a model ID')),
                  if (provider != AiProvider.ollama) ...[
                    const SizedBox(height: Space.sm),
                    TextField(
                      controller: key,
                      obscureText: true,
                      decoration: InputDecoration(
                        labelText: 'API key',
                        hintText: provider == AiProvider.customGateway
                            ? 'Key your gateway expects, or leave blank'
                            : 'Leave blank to keep the saved key',
                      ),
                    ),
                  ],
                  if (feedback != null) Padding(
                    padding: const EdgeInsets.only(top: Space.sm),
                    child: Text(feedback!, style: AppType.meta(context)),
                  ),
                ],
              ),
            ),
          ),
          actions: [
            TextButton(onPressed: busy ? null : () => Navigator.pop(context), child: const Text('Cancel')),
            TextButton(
              onPressed: busy ? null : () async {
                setDialogState(() { busy = true; feedback = 'Checking server…'; });
                final draft = AiConnectionConfig(
                  provider: provider,
                  endpoint: endpoint.text,
                  model: model.text,
                  displayName: name.text.trim().isEmpty ? provider.label : name.text.trim(),
                );
                try {
                  final found = await widget.connection.discoverModels(draft, apiKey: key.text);
                  setDialogState(() { busy = false; feedback = found.isEmpty ? 'Connected, but no models were returned.' : 'Connected. Found ${found.length} model${found.length == 1 ? '' : 's'}: ${found.take(4).join(', ')}'; });
                } catch (error) {
                  setDialogState(() { busy = false; feedback = 'Could not connect: $error'; });
                }
              },
              child: const Text('Test'),
            ),
            FilledButton(
              onPressed: busy ? null : () async {
                if (endpoint.text.trim().isEmpty || model.text.trim().isEmpty) {
                  setDialogState(() => feedback = 'Server URL and model name are required.');
                  return;
                }
                await widget.connection.save(
                  AiConnectionConfig(
                    provider: provider,
                    endpoint: endpoint.text,
                    model: model.text,
                    displayName: name.text.trim().isEmpty ? provider.label : name.text.trim(),
                  ),
                  apiKey: key.text,
                );
                if (context.mounted) Navigator.pop(context);
              },
              child: const Text('Save'),
            ),
          ],
        ),
      ),
    );
    endpoint.dispose(); model.dispose(); name.dispose(); key.dispose();
  }

  /// The state of the engine, as the one raised thing on the tab. This used to be
  /// three ledger lines reading Status / Runtime / Where it runs, which spent a
  /// third of the page restating two constants; what changes is whether a model
  /// is loaded, and that is now the sentence you land on.
  Widget _enginePanel() {
    final scheme = Theme.of(context).colorScheme;
    final loaded = widget.processor.modelLoaded;
    final failed = _connectionStatus == 'Could not load it';
    final path = widget.processor.loadedModelPath;
    final name = (path == null || path.isEmpty)
        ? null
        : path.split(Platform.pathSeparator).last.replaceAll('.gguf', '');
    // Not having loaded a model yet is the starting state, not a fault, so it is
    // stated in the ordinary ink. Red is kept for the one status that reports
    // something actually went wrong.
    final tone = failed ? legibleAccent(Semantic.danger, scheme.surface) : null;
    return Panel(
      accentEdge: loaded,
      padding: const EdgeInsets.all(Space.lg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Text(
                  loaded ? (name ?? 'Model loaded') : _connectionStatus,
                  style: AppType.title(context).copyWith(
                    fontSize: 19,
                    letterSpacing: -0.4,
                    color: tone,
                  ),
                  maxLines: 2,
                ),
              ),
              if (_isChecking) ...[
                const SizedBox(width: Space.md),
                Padding(
                  padding: const EdgeInsets.only(top: 5),
                  child: SizedBox(
                    width: 14,
                    height: 14,
                    child: CircularProgressIndicator(
                      strokeWidth: 1.6,
                      color: accentOn(context, minRatio: 3),
                    ),
                  ),
                ),
              ],
            ],
          ),
          const SizedBox(height: Space.xs),
          Text(
            loaded
                ? 'Answering on this phone through llama.cpp. '
                    'Nothing you ask it leaves the device.'
                : failed
                    ? 'The file opened but the runtime refused it. Try another '
                        'quantisation, or a smaller model.'
                    : 'Pick one from below and it loads here. Everything after '
                        'that happens offline.',
            style: AppType.body(context),
          ),
          if (loaded) ...[
            const SizedBox(height: Space.lg),
            // Size and runtime, one dot between them. Two facts is the whole
            // budget: a strip of six is a spec sheet with the hairlines removed.
            Text(_engineFacts().join('  ·  '), style: AppType.meta(context)),
          ],
          if (loaded) ...[
            const SizedBox(height: Space.md),
            Transform.translate(
              offset: const Offset(-10, 0),
              child: QuietAction(
                label: 'Unload model',
                icon: Icons.eject_rounded,
                danger: true,
                dense: true,
                onPressed: _unloadModel,
              ),
            ),
          ],
        ],
      ),
    );
  }

  /// What is worth printing about the file that is loaded: its size on disk, and
  /// the runtime. Both are read cheaply; anything that needs the model itself is
  /// left to the model rows below.
  List<String> _engineFacts() {
    final out = <String>[];
    final path = widget.processor.loadedModelPath;
    if (path != null && path.isNotEmpty) {
      try {
        final len = File(path).lengthSync();
        if (len > 0) out.add(_readableSize(len));
      } catch (_) {
        // A model can be loaded from a path the shell can no longer stat. Not
        // worth a line of its own.
      }
    }
    out.add('llama.cpp');
    return out;
  }

  /// One GGUF file already on the phone. Tapping it loads it; the loaded one
  /// carries the accent edge instead of a coloured 'ACTIVE' tag.
  Widget _modelFileRow(FileSystemEntity f) {
    final name = f.path.split(Platform.pathSeparator).last;
    final isLoaded = widget.processor.loadedModelPath == f.path;
    return LedgerRow(
      flag: isLoaded,
      onTap: isLoaded ? null : () => _loadModelFile(f.path),
      padding: const EdgeInsets.symmetric(vertical: Space.sm - 1),
      child: Row(
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  name,
                  style: AppType.body(context).copyWith(
                    color: ink(context, isLoaded ? 0.94 : 0.80),
                    fontWeight: isLoaded ? FontWeight.w700 : FontWeight.w500,
                  ),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
                const SizedBox(height: 2),
                Text(
                  isLoaded ? 'Loaded' : 'Tap to load',
                  style: AppType.meta(context),
                ),
              ],
            ),
          ),
          IconAction(
            icon: Icons.delete_outline_rounded,
            tooltip: 'Delete model',
            size: 18,
            onPressed: () => _confirmDeleteModel(f.path, name, isLoaded),
          ),
        ],
      ),
    );
  }

  /// One model in the catalogue: what it is, what it costs to keep, and the one
  /// action available for it right now.
  Widget _catalogRow(
    ModelCatalogEntry catalog, {
    required bool downloaded,
    DownloadState? state,
  }) {
    final downloading = state?.status == 'downloading';
    final progress = state?.progress ?? 0.0;
    final accent = accentOn(context, minRatio: 3);
    return LedgerRow(
      flag: downloaded,
      padding: const EdgeInsets.symmetric(vertical: Space.sm + 2),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  catalog.displayName,
                  style: AppType.heading(context),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
              if (downloading)
                Text(
                  '${(progress * 100).toStringAsFixed(0)}%',
                  style: AppType.meta(context)
                      .copyWith(color: accent, fontWeight: FontWeight.w700),
                )
              else if (downloaded)
                Text('On this phone', style: AppType.meta(context))
              else
                IconAction(
                  icon: Icons.download_rounded,
                  tooltip: 'Download ${catalog.displayName}',
                  size: 19,
                  onPressed: () => _downloadAndLoadModel(catalog),
                ),
            ],
          ),
          const SizedBox(height: 2),
          Text(
            '${catalog.paramLabel} · ${catalog.sizeLabel}, about '
            '${catalog.estimatedTokPerSec.toStringAsFixed(0)} tokens a second',
            style: AppType.meta(context),
          ),
          const SizedBox(height: 5),
          Text(catalog.description, style: AppType.body(context)),
          if (downloading) ...[
            const SizedBox(height: Space.sm),
            // The meter draws the part that has arrived and leaves the rest as
            // paper, rather than filling a grey track that reads as a second
            // finished bar sitting behind the first.
            Meter(
              value: progress,
              detail: (state?.totalBytes ?? 0) > 0
                  ? '${_readableSize(state!.receivedBytes)} of '
                      '${_readableSize(state.totalBytes)}'
                  : 'Starting',
            ),
          ],
        ],
      ),
    );
  }

  // ════════════════════════════════════════════════════════════════════════════
  // TAB 2: THEME
  // ════════════════════════════════════════════════════════════════════════════

  List<Widget> _buildThemeTab() {
    final settings = Hive.box(AppDefaults.hiveSettingsBox);
    final currentMode = settings.get(AppDefaults.themeModeKey, defaultValue: 'dark') as String;
    final isAmoled = settings.get(AppDefaults.amoledKey, defaultValue: false) as bool;
    final currentAccentInt = settings.get(AppDefaults.accentColorKey,
        defaultValue: kDefaultAccent.toARGB32()) as int;
    final currentAccent = Color(currentAccentInt);
    final isGlass = settings.get(AppDefaults.glassModeKey, defaultValue: false) as bool;

    return [
      // The sample goes above the controls, not three groups below them: the
      // point of a theme page is to see what the next tap will do to the type
      // you actually read.
      const SizedBox(height: Space.lg),
      _themeSpecimen(),

      const SectionHeader('Brightness'),
      _buildThemeModeOption('Dark', 'dark', currentMode, settings),
      _buildThemeModeOption('Light', 'light', currentMode, settings),
      _buildThemeModeOption('Follow system', 'system', currentMode, settings),
      _buildAmoledRow(isAmoled, settings),

      // ── Surface style: Material or Liquid Glass ──
      const _SectionLabel('SURFACE STYLE'),
      const SizedBox(height: Space.sm),
      GlassPanel(
        padding: const EdgeInsets.all(Space.md),
        borderRadius: Radii.card,
        child: Column(
          children: [
            Row(
              children: [
                Expanded(
                  child: _themeChoiceCard(
                    label: 'Material',
                    icon: Icons.layers_rounded,
                    description: 'Opaque, tactile surfaces',
                    selected: !isGlass,
                    onTap: () => _setGlassMode(false),
                  ),
                ),
                const SizedBox(width: Space.sm + 2),
                Expanded(
                  child: _themeChoiceCard(
                    label: 'Liquid Glass',
                    icon: Icons.water_drop_rounded,
                    description: 'Translucent, refractive',
                    selected: isGlass,
                    onTap: () => _setGlassMode(true),
                  ),
                ),
              ],
            ),
            Divider(color: AppColors.divider(context), height: Space.xl),
            _buildToggleRow(
              title: isGlass ? 'Liquid Glass active' : 'Material active',
              subtitle: isGlass
                  ? 'Blur, rim light and refraction are on'
                  : 'Flat fills, no blur passes',
              value: isGlass,
              onChanged: _setGlassMode,
            ),
          ],
        ),
      ),
      const SizedBox(height: Space.xl),

      // ── Accent ──
      const SectionHeader('Accent'),
      _buildAccentGrid(currentAccent, settings),

      // ── Navigation order ──
      const _SectionLabel('NAVIGATION LAYOUT'),
      const SizedBox(height: Space.sm),
      _buildNavOrderCard(),

      // ── The glass card from the spec ──
      const _SectionLabel('LIQUID GLASS'),
      const SizedBox(height: Space.sm),
      _buildNavBarSettingsPanel(),
      const SizedBox(height: Space.xxl),
    ];
  }

  Widget _buildThemeModeOption(
      String label, String value, String current, Box settings) {
    final selected = current == value;
    return LedgerRow(
      flag: selected,
      onTap: selected
          ? null
          : () {
              HapticFeedback.selectionClick();
              settings.put(AppDefaults.themeModeKey, value);
              setState(() {});
              widget.onThemeChanged?.call();
            },
      padding: const EdgeInsets.symmetric(vertical: Space.md),
      child: Semantics(
        button: true,
        selected: selected,
        label: label,
        child: Row(
          children: [
            Icon(
              value == 'dark'
                  ? Icons.dark_mode_rounded
                  : value == 'light'
                      ? Icons.light_mode_rounded
                      : Icons.brightness_auto_rounded,
              size: 17,
              color: ink(context, selected ? 0.86 : 0.42),
            ),
            const SizedBox(width: Space.md - 1),
            Expanded(
              child: Text(
                label,
                style: AppType.body(context).copyWith(
                  color: ink(context, selected ? 0.94 : 0.72),
                  fontWeight: selected ? FontWeight.w700 : FontWeight.w500,
                ),
              ),
            ),
            if (selected)
              Icon(Icons.check_rounded,
                  size: 16, color: accentOn(context, minRatio: 3)),
          ],
        ),
      ),
    );
  }

  // ── Shared rows for the Themes & UI tab ─────────────────────────────

  /// AMOLED is the only route to a true `#000000` canvas. Everywhere else the
  /// dark surface is off-black so that shadows and rim light still register.
  Widget _buildAmoledRow(bool isAmoled, Box settings) {
    return _switchRow(
      title: 'AMOLED pitch black',
      subtitle: isAmoled
          ? 'Pure black canvas, no surface gradient'
          : 'Off black canvas, keeps depth and shadow',
      value: isAmoled,
      divided: false,
      onChanged: (v) {
        settings.put(AppDefaults.amoledKey, v);
        appAmoledMode.value = v;
        setState(() {});
        widget.onThemeChanged?.call();
      },
    );
  }

  /// A switch on a ledger line, for the settings groups this page owns. The nav
  /// bar's own panel keeps its plated rows, so it is left on [_buildToggleRow].
  Widget _switchRow({
    required String title,
    required bool value,
    required ValueChanged<bool> onChanged,
    String? subtitle,
    bool divided = true,
  }) {
    return LedgerRow(
      divided: divided,
      padding: const EdgeInsets.symmetric(vertical: Space.md - 1),
      child: Semantics(
        toggled: value,
        label: title,
        child: Row(
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    title,
                    style: AppType.body(context).copyWith(
                      color: ink(context, 0.88),
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                  if (subtitle != null) ...[
                    const SizedBox(height: 2),
                    Text(subtitle, style: AppType.meta(context)),
                  ],
                ],
              ),
            ),
            const SizedBox(width: Space.md),
            LiquidToggle(
              value: value,
              onChanged: (v) {
                HapticFeedback.selectionClick();
                onChanged(v);
              },
            ),
          ],
        ),
      ),
    );
  }

  /// One switch row used by every toggle on this tab, so the control reads the
  /// same everywhere instead of drifting per call site.
  Widget _buildToggleRow({
    required String title,
    String? subtitle,
    required bool value,
    required ValueChanged<bool> onChanged,
    IconData? icon,
  }) {
    final scheme = Theme.of(context).colorScheme;
    final dark = scheme.brightness == Brightness.dark;
    final accent = legibleAccent(AppColors.primaryAccent, scheme.surface,
        minRatio: 3);
    return Semantics(
      toggled: value,
      label: title,
      child: Row(
        children: [
          if (icon != null) ...[
            Icon(icon,
                size: 18,
                color: value ? accent : AppColors.themeTertiary(context)),
            const SizedBox(width: Space.md - 2),
          ],
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  title,
                  style: TextStyle(
                    color: AppColors.themePrimary(context),
                    fontSize: 13,
                    fontWeight: FontWeight.w500,
                    letterSpacing: -0.1,
                  ),
                ),
                if (subtitle != null) ...[
                  const SizedBox(height: 2),
                  Text(
                    subtitle,
                    style: TextStyle(
                      color: AppColors.themeTertiary(context),
                      fontSize: 11,
                      height: 1.3,
                    ),
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(width: Space.sm),
          Switch(
            value: value,
            onChanged: (v) {
              HapticFeedback.selectionClick();
              onChanged(v);
            },
            thumbColor: WidgetStateProperty.resolveWith((states) =>
                states.contains(WidgetState.selected)
                    ? Colors.white
                    : (dark ? const Color(0xFF6A6864) : const Color(0xFFFCFBF9))),
            trackColor: WidgetStateProperty.resolveWith((states) =>
                states.contains(WidgetState.selected)
                    ? AppColors.primaryAccent.withValues(alpha: dark ? 0.85 : 0.9)
                    : scheme.onSurface.withValues(alpha: dark ? 0.10 : 0.08)),
            trackOutlineColor: WidgetStateProperty.resolveWith((states) =>
                states.contains(WidgetState.selected)
                    ? Colors.transparent
                    : scheme.outlineVariant.withValues(alpha: dark ? 0.5 : 0.9)),
          ),
        ],
      ),
    );
  }

  /// The eight soft accents from the token file. One accent drives the whole
  /// app, so this grid is the only place a hue is chosen.
  Widget _buildAccentGrid(Color currentAccent, Box settings) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SizedBox(height: Space.sm + 2),
        LayoutBuilder(
          builder: (context, constraints) {
            const columns = 4;
            const gap = Space.md;
            final cell =
                (constraints.maxWidth - gap * (columns - 1)) / columns;
            return Wrap(
              spacing: gap,
              runSpacing: Space.md + 2,
              children: [
                for (final swatch in kAccents)
                  SizedBox(
                    width: cell,
                    child: _buildAccentSwatch(
                      swatch: swatch,
                      selected:
                          swatch.color.toARGB32() == currentAccent.toARGB32(),
                      settings: settings,
                    ),
                  ),
              ],
            );
          },
        ),
        const SizedBox(height: Space.md + 2),
        Text(
          'One hue runs through every surface, chart and highlight. The shades '
          'are desaturated on purpose, so a long reading session stays '
          'comfortable.',
          style: AppType.meta(context).copyWith(height: 1.4),
        ),
      ],
    );
  }

  Widget _buildAccentSwatch({
    required AccentSwatch swatch,
    required bool selected,
    required Box settings,
  }) {
    return Semantics(
      button: true,
      selected: selected,
      label: '${swatch.name} accent',
      child: Pressable(
        scale: 0.93,
        onTap: selected
            ? null
            : () {
                HapticFeedback.selectionClick();
                settings.put(
                    AppDefaults.accentColorKey, swatch.color.toARGB32());
                appAccentColor.value = swatch.color;
                setState(() {});
                widget.onThemeChanged?.call();
              },
        child: Column(
          children: [
            // The ring is drawn outside the sample so the colour is never
            // reduced by a border sitting on top of it.
            AnimatedContainer(
              duration: reduceMotion(context) ? Duration.zero : Motion.fast,
              curve: kFadeCurve,
              padding: const EdgeInsets.all(3),
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                border: Border.all(
                  color: selected
                      ? swatch.color.withValues(alpha: 0.85)
                      : Colors.transparent,
                  width: 1.5,
                ),
              ),
              child: AspectRatio(
                aspectRatio: 1,
                child: DecoratedBox(
                  decoration: BoxDecoration(
                    shape: BoxShape.circle,
                    gradient: LinearGradient(
                      begin: Alignment.topLeft,
                      end: Alignment.bottomRight,
                      colors: [swatch.color, swatch.companion],
                    ),
                  ),
                  child: selected
                      ? Center(
                          child: Icon(Icons.check_rounded,
                              size: 15, color: readableOn(swatch.color)),
                        )
                      : null,
                ),
              ),
            ),
            const SizedBox(height: Space.sm - 1),
            Text(
              swatch.name,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              textAlign: TextAlign.center,
              style: AppType.meta(context).copyWith(
                fontSize: 10.5,
                color: ink(context, selected ? 0.88 : 0.46),
                fontWeight: selected ? FontWeight.w700 : FontWeight.w600,
              ),
            ),
          ],
        ),
      ),
    );
  }

  // ── Preview and navigation layout ───────────────────────────────────

  /// Shows the text ranks and the semantic palette on the real surface, so a
  /// theme choice can be judged without leaving the page.
  /// The type ramp and the palette, on a real surface, so the brightness and
  /// accent controls below it have something to change in front of you.
  Widget _themeSpecimen() {
    return Panel(
      padding: const EdgeInsets.all(Space.lg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Aa  Quick brown fox', style: AppType.title(context)),
          const SizedBox(height: Space.sm - 2),
          Text(
            'Body copy sits at this weight and colour. It is the rank you read '
            'the most, so it carries the highest contrast.',
            style: AppType.body(context),
          ),
          const SizedBox(height: Space.xs),
          Text(
            'Captions and figures drop to this rank.',
            style: AppType.meta(context),
          ),
          const SizedBox(height: Space.lg),
          Row(
            children: [
              _previewChip('Accent', AppColors.primaryAccent),
              _previewChip('Success', AppColors.success),
              _previewChip('Warning', AppColors.warning),
              _previewChip('Danger', AppColors.danger),
            ],
          ),
        ],
      ),
    );
  }

  /// One entry in the palette legend: the colour itself, then its name. A
  /// sample is the content here, which is the one case a coloured mark earns.
  Widget _previewChip(String label, Color color) {
    return Padding(
      padding: const EdgeInsets.only(right: Space.lg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            width: 34,
            height: 6,
            decoration: BoxDecoration(
              color: color,
              borderRadius: BorderRadius.circular(Radii.pill),
            ),
          ),
          const SizedBox(height: Space.sm - 2),
          Text(
            label,
            style: AppType.meta(context).copyWith(fontSize: 10.5),
          ),
        ],
      ),
    );
  }

  /// Long-press drag to reorder the destinations. The pages themselves stay in
  /// their canonical order; only the button strip is permuted, so no tab can
  /// ever be reordered out of existence.
  Widget _buildNavOrderCard() {
    final scheme = Theme.of(context).colorScheme;
    final dark = scheme.brightness == Brightness.dark;
    final order = _navCfg.resolvedOrder(kNavTabLabels.length);
    final isDefault = _navCfg.order.isEmpty;
    return GlassPanel(
      padding: const EdgeInsets.all(Space.lg - 2),
      borderRadius: Radii.card,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(Icons.drag_indicator_rounded,
                  size: 18, color: AppColors.themeTertiary(context)),
              const SizedBox(width: Space.sm),
              Expanded(
                child: Text(
                  'Tab order',
                  style: TextStyle(
                    color: AppColors.themePrimary(context),
                    fontSize: 13.5,
                    fontWeight: FontWeight.w600,
                    letterSpacing: -0.1,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: Space.md + 2),
          LayoutBuilder(
            builder: (context, box) {
              // Every tab has to be on screen at once. A fixed 62 wide tile
              // needed 350 and the card gives about 317, so the last one was
              // sliced down the middle by the panel edge — a reorder control
              // showing four and a half of five, which is not an order anyone
              // can judge. The tile takes whatever fits instead, and stops at
              // the width of its own chip; below that (a very small screen, or
              // a large font scale) the row goes back to scrolling rather than
              // overflowing.
              const gap = Space.sm + 2;
              final fitted =
                  (box.maxWidth - gap * (order.length - 1)) / order.length;
              final tile = fitted.clamp(_navTileChip, 62.0);
              return SizedBox(
                height: 76,
                child: ReorderableListView.builder(
                  scrollDirection: Axis.horizontal,
                  buildDefaultDragHandles: false,
                  padding: EdgeInsets.zero,
                  itemCount: order.length,
                  proxyDecorator: (child, index, animation) => AnimatedBuilder(
                    animation: animation,
                    builder: (context, _) {
                      final lift = Curves.easeOut.transform(animation.value);
                      return Transform.scale(
                        scale: 1 + 0.08 * lift,
                        child: Opacity(opacity: 1 - 0.15 * lift, child: child),
                      );
                    },
                  ),
                  // `onReorderItem` hands back a destination index that already
                  // accounts for the dragged tile being lifted out of the list,
                  // so there is no off-by-one to patch up here.
                  onReorderItem: (from, to) {
                    HapticFeedback.mediumImpact();
                    final next = [...order];
                    next.insert(to, next.removeAt(from));
                    _updateNavConfig((c) => c.copyWith(order: next));
                  },
                  itemBuilder: (context, slot) {
                    final i = order[slot];
                    return ReorderableDelayedDragStartListener(
                      key: ValueKey('nav-slot-$i'),
                      index: slot,
                      child: Padding(
                        // No gap after the last tile, or the row is one gap
                        // wider than it measured itself to be and scrolls by
                        // exactly that much.
                        padding: EdgeInsets.only(
                          right: slot == order.length - 1 ? 0 : gap,
                        ),
                        child: _navOrderTile(i, slot, dark, scheme, tile),
                      ),
                    );
                  },
                ),
              );
            },
          ),
          const SizedBox(height: Space.md),
          Text(
            'Long press and drag to reorder',
            style: TextStyle(
              color: AppColors.themeTertiary(context),
              fontSize: 11,
              letterSpacing: 0.1,
            ),
          ),
          const SizedBox(height: Space.md + 2),
          _buildOutlineButton(
            label: 'Reset navigation order',
            icon: Icons.restart_alt_rounded,
            enabled: !isDefault,
            onTap: () => _updateNavConfig((c) => c.copyWith(order: const [])),
          ),
        ],
      ),
    );
  }

  /// The chip inside a tab-order tile, and so the narrowest a tile can get
  /// before the row has to scroll instead of shrink.
  static const double _navTileChip = 50;

  Widget _navOrderTile(
    int i,
    int slot,
    bool dark,
    ColorScheme scheme,
    double width,
  ) {
    // The leftmost slot is accented so the direction of the row is obvious
    // without a legend.
    final active = slot == 0;
    return SizedBox(
      width: width,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: _navTileChip,
            height: 46,
            decoration: BoxDecoration(
              color: active
                  ? AppColors.primaryAccent.withValues(alpha: dark ? 0.18 : 0.13)
                  : scheme.onSurface.withValues(alpha: dark ? 0.05 : 0.035),
              borderRadius: BorderRadius.circular(Radii.inner),
              border: Border.all(
                color: active
                    ? AppColors.primaryAccent.withValues(alpha: 0.45)
                    : scheme.outlineVariant
                        .withValues(alpha: dark ? 0.3 : 0.55),
              ),
            ),
            child: Icon(
              kNavTabSelectedIcons[i],
              size: 20,
              color: active
                  ? legibleAccent(AppColors.primaryAccent, scheme.surface,
                      minRatio: 3)
                  : AppColors.themeSecondary(context),
            ),
          ),
          const SizedBox(height: Space.sm - 2),
          Text(
            kNavTabLabels[i],
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              color: AppColors.themeTertiary(context),
              fontSize: 10,
              fontWeight: FontWeight.w500,
            ),
          ),
        ],
      ),
    );
  }

  /// The quiet secondary action used by both reset buttons.
  Widget _buildOutlineButton({
    required String label,
    required IconData icon,
    required VoidCallback onTap,
    bool enabled = true,
  }) {
    final scheme = Theme.of(context).colorScheme;
    final dark = scheme.brightness == Brightness.dark;
    final tint = AppColors.themeSecondary(context);
    return Opacity(
      opacity: enabled ? 1 : 0.45,
      child: Semantics(
        button: true,
        enabled: enabled,
        label: label,
        child: GestureDetector(
          onTap: enabled
              ? () {
                  HapticFeedback.lightImpact();
                  onTap();
                }
              : null,
          child: Container(
            width: double.infinity,
            padding: const EdgeInsets.symmetric(vertical: Space.md),
            decoration: BoxDecoration(
              color: scheme.onSurface.withValues(alpha: dark ? 0.05 : 0.035),
              borderRadius: BorderRadius.circular(Radii.control),
              border: Border.all(
                color: scheme.outlineVariant
                    .withValues(alpha: dark ? 0.35 : 0.7),
              ),
            ),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Icon(icon, size: 15, color: tint),
                const SizedBox(width: Space.sm),
                Text(
                  label,
                  style: TextStyle(
                      color: tint,
                      fontSize: 12.5,
                      fontWeight: FontWeight.w600,
                      letterSpacing: -0.1),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  // ════════════════════════════════════════════════════════════════════════════
  // TAB 3: SYSTEM
  // ════════════════════════════════════════════════════════════════════════════

  List<Widget> _buildSystemTab() {
    return [
      const SizedBox(height: Space.lg),
      // The one thing on this tab you can change goes first. Everything under it
      // is a reading, and readings do not belong above controls.
      _captureSwitch(),
      _autoRedactSwitch(),
      _storageReadout(),
      const SectionHeader('How it is kept'),
      const LedgerFact('Encryption', 'AES-256, key in the Android Keystore'),
      // The absolute path was ellipsised into nothing at this width and could
      // not be acted on anyway. What matters is that the boxes sit inside the
      // app's own private storage, so that is what it says.
      const LedgerFact('Location', 'This app\'s private storage',
          divided: false),
      const SectionHeader('This device'),
      _buildDeviceInfoPanel(),
      const SizedBox(height: Space.lg),
    ];
  }

  /// Background capture, on the tab whose name promises it. The same switch is on
  /// the Clips tab, and both write the one setting the service reads.
  Widget _captureSwitch() {
    final on = widget.isServiceRunning;
    return _switchRow(
      title: 'Capture in the background',
      subtitle: on
          ? 'Everything you copy is saved to Clips, with a quiet notification '
              'while it runs.'
          : 'Clips only fill up while the app is open in front of you.',
      value: on,
      divided: false,
      onChanged: (v) {
        HapticFeedback.selectionClick();
        Hive.box(AppDefaults.hiveSettingsBox).put('bgServiceEnabled', v);
        if (v) {
          widget.onStartService?.call();
        } else {
          widget.onStopService?.call();
        }
        setState(() {});
      },
    );
  }

  /// Masks keys, tokens and passwords before a clip is ever saved, so a
  /// secret copied in a hurry never reaches the box in the first place.
  Widget _autoRedactSwitch() {
    return _switchRow(
      title: 'Redact secrets on capture',
      subtitle: 'API keys, tokens and passwords are masked as clips land.',
      value: _settings.get('autoRedact') == true,
      onChanged: (v) {
        HapticFeedback.selectionClick();
        _settings.put('autoRedact', v);
        setState(() {});
      },
    );
  }

  /// What the phone is actually holding, as three figures instead of a five row
  /// spec sheet. The total is the number you came for; the split is the detail.
  Widget _storageReadout() {
    final clips = Hive.box(AppDefaults.hiveClipBox).length;
    final notes = Hive.box(AppDefaults.hiveNoteBox).length;
    final chats = Hive.isBoxOpen(AppDefaults.hiveChatBox)
        ? Hive.box(AppDefaults.hiveChatBox).length
        : 0;
    final total = clips + notes + chats;
    return Padding(
      padding: const EdgeInsets.only(top: Space.xl),
      child: Readout(
        value: '$total',
        label: total == 1 ? 'item on this phone' : 'items on this phone',
        facts: [
          Figure('$clips', clips == 1 ? 'clip' : 'clips'),
          Figure('$notes', notes == 1 ? 'note' : 'notes'),
          Figure('$chats', chats == 1 ? 'chat' : 'chats'),
        ],
      ),
    );
  }

  // ════════════════════════════════════════════════════════════════════════════
  // TAB 4: DATA
  // ════════════════════════════════════════════════════════════════════════════

  /// The privacy rows: a PIN over the whole app, a lifetime for clips, and
  /// the way back out. The PIN rests in the encrypted settings box, under the
  /// same key as the clips it guards.
  static const _retentionLabels = {
    0: 'Forever',
    1: '24 hours',
    7: '7 days',
    30: '30 days',
  };

  String get _retentionLabel {
    final days = _settings.get('clipRetentionDays') as int? ?? 0;
    return _retentionLabels[days] ?? 'Forever';
  }

  bool get _pinSet {
    final pin = _settings.get('appPin');
    return pin is String && pin.isNotEmpty;
  }

  List<Widget> _privacyRows() {
    return [
      _privacyRow(
        icon: Icons.lock_outline_rounded,
        label: 'Clip lifetime',
        detail: 'Clips older than this are cleared on launch · $_retentionLabel',
        onTap: _pickRetention,
      ),
      _privacyRow(
        icon: _pinSet ? Icons.lock_rounded : Icons.lock_open_rounded,
        label: _pinSet ? 'Change PIN' : 'Lock with a PIN',
        detail: _pinSet
            ? 'The app asks for it on launch and on return'
            : 'Your clips ask for it before they open',
        onTap: _setPinFlow,
      ),
      if (_pinSet) ...[
        _privacyRow(
          icon: Icons.phonelink_lock_rounded,
          label: 'Lock now',
          detail: 'Back behind the PIN immediately',
          onTap: () => widget.onLockNow?.call(),
        ),
        _privacyRow(
          icon: Icons.no_encryption_outlined,
          label: 'Remove PIN',
          detail: 'Your clips open freely again',
          onTap: _removePinFlow,
          danger: true,
        ),
      ],
    ];
  }

  Widget _privacyRow({
    required IconData icon,
    required String label,
    required String detail,
    required VoidCallback onTap,
    bool danger = false,
  }) {
    final tone = danger
        ? legibleAccent(Semantic.danger, Theme.of(context).colorScheme.surface)
        : ink(context, 0.88);
    return LedgerRow(
      onTap: onTap,
      padding: const EdgeInsets.symmetric(vertical: Space.md - 1),
      child: Row(
        children: [
          Icon(icon, size: 17, color: tone.withValues(alpha: 0.9)),
          const SizedBox(width: Space.md - 1),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  label,
                  style: AppType.body(context).copyWith(
                    color: tone,
                    fontWeight: FontWeight.w600,
                  ),
                ),
                const SizedBox(height: 2),
                Text(detail, style: AppType.meta(context)),
              ],
            ),
          ),
          Icon(Icons.chevron_right_rounded, size: 18, color: ink(context, 0.30)),
        ],
      ),
    );
  }

  void _settingsToast(String message) {
    if (!mounted) return;
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(
        SnackBar(
          content: Text(message),
          behavior: SnackBarBehavior.floating,
          duration: const Duration(seconds: 2),
        ),
      );
  }

  Future<void> _pickRetention() async {
    final picked = await showDialog<int>(
      context: context,
      builder: (dctx) => AlertDialog(
        title: const Text('Keep clips for'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            for (final days in _retentionLabels.keys)
              ListTile(
                dense: true,
                contentPadding: EdgeInsets.zero,
                title: Text(_retentionLabels[days]!),
                trailing: (_settings.get('clipRetentionDays') as int? ?? 0) ==
                        days
                    ? Icon(Icons.check_rounded,
                        color: Theme.of(dctx).colorScheme.primary)
                    : null,
                onTap: () => Navigator.pop(dctx, days),
              ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dctx),
            child: const Text('Cancel'),
          ),
        ],
      ),
    );
    if (picked == null) return;
    await _settings.put('clipRetentionDays', picked);
    final burned = await purgeExpiredClips();
    if (mounted) setState(() {});
    _settingsToast(burned == 0
        ? 'Clips now keep for ${_retentionLabels[picked]}'
        : 'Cleared ${plural(burned, 'expired clip')}');
  }

  Future<String?> _askPin(String title, String hint) {
    final ctrl = TextEditingController();
    return showDialog<String>(
      context: context,
      builder: (dctx) => AlertDialog(
        title: Text(title),
        content: TextField(
          controller: ctrl,
          obscureText: true,
          autofocus: true,
          keyboardType: TextInputType.number,
          decoration: InputDecoration(hintText: hint),
          onSubmitted: (v) => Navigator.pop(dctx, v),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dctx),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () => Navigator.pop(dctx, ctrl.text),
            child: const Text('Save'),
          ),
        ],
      ),
    ).whenComplete(ctrl.dispose);
  }

  Future<void> _setPinFlow() async {
    final first = await _askPin('Set a PIN', 'Minimum 4 digits');
    if (first == null) return;
    if (first.length < 4) {
      _settingsToast('A PIN needs at least 4 digits');
      return;
    }
    final second = await _askPin('Confirm the PIN', 'Once more');
    if (second == null) return;
    if (second != first) {
      _settingsToast('Those PINs did not match');
      return;
    }
    await _settings.put('appPin', first);
    if (mounted) setState(() {});
    _settingsToast('PIN set. Your clips lock on launch and on return.');
  }

  Future<void> _removePinFlow() async {
    final ok = await showDialog<bool>(
      context: context,
      builder: (dctx) => AlertDialog(
        title: const Text('Remove the PIN?'),
        content: const Text('Your clips will open without asking again.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dctx, false),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () => Navigator.pop(dctx, true),
            child: const Text('Remove'),
          ),
        ],
      ),
    );
    if (ok != true) return;
    await _settings.put('appPin', '');
    if (mounted) setState(() {});
    _settingsToast('PIN removed');
  }

  /// One of the three wipe rows. Destructive work is stated in words and only
  /// the verb carries the danger tone, so the group does not read as an alarm.
  Widget _buildWipeTile({
    required IconData icon,
    required String label,
    required String detail,
    required VoidCallback onTap,
  }) {
    final danger =
        legibleAccent(Semantic.danger, Theme.of(context).colorScheme.surface);
    return LedgerRow(
      onTap: onTap,
      padding: const EdgeInsets.symmetric(vertical: Space.md - 1),
      child: Row(
        children: [
          Icon(icon, size: 17, color: danger.withValues(alpha: 0.9)),
          const SizedBox(width: Space.md - 1),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  label,
                  style: AppType.body(context).copyWith(
                    color: danger,
                    fontWeight: FontWeight.w600,
                  ),
                ),
                const SizedBox(height: 2),
                Text(detail, style: AppType.meta(context)),
              ],
            ),
          ),
          Icon(Icons.chevron_right_rounded, size: 18, color: ink(context, 0.30)),
        ],
      ),
    );
  }

  List<Widget> _buildDataTab() {
    return [
      const SizedBox(height: Space.lg),
      _exportPanel(),
      const SectionHeader('Privacy lock'),
      ..._privacyRows(),
      const SectionHeader('Remove things'),
      _buildWipeTile(
        icon: Icons.delete_sweep_rounded,
        label: 'Clear clips',
        detail: 'Removes every captured clip from this phone',
        onTap: _clearClipHistory,
      ),
      _buildWipeTile(
        icon: Icons.note_alt_outlined,
        label: 'Clear notes',
        detail: 'Removes every note, pinned ones included',
        onTap: _clearNotes,
      ),
      _buildWipeTile(
        icon: Icons.forum_outlined,
        label: 'Clear chats',
        detail: 'Removes every saved conversation',
        onTap: _clearChatHistory,
      ),
      const SectionHeader('Getting started'),
      LedgerRow(
        onTap: _replayTutorial,
        divided: false,
        padding: const EdgeInsets.symmetric(vertical: Space.md),
        child: Row(
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Replay the tutorial',
                    style: AppType.body(context).copyWith(
                      color: ink(context, 0.90),
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    'The greeting and the walkthrough, from the top',
                    style: AppType.meta(context),
                  ),
                ],
              ),
            ),
            Icon(Icons.play_arrow_rounded, size: 18, color: ink(context, 0.55)),
          ],
        ),
      ),
      const SizedBox(height: Space.section),
      Text(
        'ClipSyncAI 1.0.0',
        style: AppType.meta(context).copyWith(color: ink(context, 0.40)),
      ),
      const SizedBox(height: 3),
      Text(
        'Everything runs on this phone. Nothing is sent anywhere.',
        style: AppType.meta(context).copyWith(color: ink(context, 0.30)),
      ),
      const SizedBox(height: Space.xl),
    ];
  }

  /// Getting your things off the phone, as the one lifted surface on the tab.
  /// It was a ledger row with a share glyph on the end, which gave the only
  /// constructive act on a page of destructive ones the smaller target.
  Widget _exportPanel() {
    final clips = Hive.box(AppDefaults.hiveClipBox).length;
    final notes = Hive.box(AppDefaults.hiveNoteBox).length;
    final chats = Hive.isBoxOpen(AppDefaults.hiveChatBox)
        ? Hive.box(AppDefaults.hiveChatBox).length
        : 0;
    final total = clips + notes + chats;
    return Panel(
      padding: const EdgeInsets.all(Space.lg),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text('Export everything',
              style: AppType.title(context)
                  .copyWith(fontSize: 19, letterSpacing: -0.4)),
          const SizedBox(height: Space.xs),
          Text(
            total == 0
                ? 'Nothing saved yet. Once there is, this writes it all out as '
                    'one markdown file you can read anywhere.'
                : 'Clips, notes and chats, written out as one markdown file. '
                    'You choose where it lands.',
            style: AppType.body(context),
          ),
          if (total > 0) ...[
            const SizedBox(height: Space.md),
            Text(
              [
                if (clips > 0) plural(clips, 'clip'),
                if (notes > 0) plural(notes, 'note'),
                if (chats > 0) plural(chats, 'chat'),
              ].join('  ·  '),
              style: AppType.meta(context),
            ),
          ],
          const SizedBox(height: Space.lg),
          PrimaryAction(
            label: _isExporting ? 'Writing the file' : 'Export to a file',
            icon: Icons.ios_share_rounded,
            busy: _isExporting,
            onPressed: (_isExporting || total == 0) ? null : _exportData,
          ),
        ],
      ),
    );
  }

  // ── Liquid Glass Nav Bar Settings ──────────────────────────────────

  void _updateNavConfig(NavBarConfig Function(NavBarConfig) updater) {
    setState(() => _navCfg = updater(_navCfg));
    widget.onNavConfigChanged?.call(_navCfg);
  }

  /// Sets the global UI theme — `glass == true` → Liquid Glass, `false` → Material.
  void _setGlassMode(bool glass) {
    Hive.box(AppDefaults.hiveSettingsBox).put(AppDefaults.glassModeKey, glass);
    appGlassMode.value = glass;
    setState(() {});
    widget.onThemeChanged?.call();
  }

  Widget _buildNavBarSettingsPanel() {
    final cfg = _navCfg;
    // The glass groups only bite when *both* switches are on: the per-bar one
    // here and the app-wide Material/Liquid Glass choice above. A slider that
    // cannot change a pixel is dimmed rather than left looking live.
    final glassLive = cfg.enabled && appGlassMode.value;
    return GlassPanel(
      padding: const EdgeInsets.all(Space.lg),
      borderRadius: Radii.card,
      elevation: 2,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _navFxHeader(),
          const SizedBox(height: Space.lg),
          _buildToggleRow(
            title: 'Enable Liquid Glass',
            subtitle: 'Blur, lens rim and chromatic split on the tab bar',
            value: cfg.enabled,
            onChanged: (v) => _updateNavConfig((c) => c.copyWith(enabled: v)),
          ),
          const SizedBox(height: Space.md),
          _navFxStatusChip(cfg),
          _navFxDivider(),

          // Engine and effects only bite while glass is on, so they dim out
          // rather than sitting there looking live.
          _navFxGroup(
            enabled: glassLive,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                _navFxLabel('GLASS ENGINE'),
                const SizedBox(height: Space.md),
                _buildGlassModeOption(
                  'iOS Liquid Glass (Android 13+)',
                  true,
                  'Layered backdrop sampling, lensed rim',
                ),
                const SizedBox(height: Space.md - 2),
                _buildGlassModeOption(
                  'iOS Liquid Glass (Android 8+)',
                  false,
                  'One blur pass, painted highlights',
                ),
              ],
            ),
          ),
          _navFxDivider(),

          _navFxLabel('SHAPE & LAYOUT'),
          const SizedBox(height: Space.md),
          _buildSliderRow('Bar Width', cfg.barWidth, 50, 100,
              (v) => _updateNavConfig((c) => c.copyWith(barWidth: v)),
              divisions: 50, suffix: '%'),
          _buildSliderRow('Bar Height', cfg.barSize, 80, 160,
              (v) => _updateNavConfig((c) => c.copyWith(barSize: v)),
              divisions: 80),
          _buildSliderRow('Bottom Offset', cfg.position, 0, 40,
              (v) => _updateNavConfig((c) => c.copyWith(position: v)),
              divisions: 40),
          _buildSliderRow('Corner Roundness', cfg.cornerRoundness, 0, 100,
              (v) => _updateNavConfig((c) => c.copyWith(cornerRoundness: v)),
              divisions: 100),
          _navFxDivider(),

          _navFxGroup(
            enabled: glassLive,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                _navFxLabel('GLASS EFFECTS'),
                const SizedBox(height: Space.md),
                _buildSliderRow('Glass Opacity', cfg.opacity, 0, 100,
                    (v) => _updateNavConfig((c) => c.copyWith(opacity: v)),
                    divisions: 100, suffix: '%'),
                _buildSliderRow('Blur Intensity', cfg.blurSigma, 0, 40,
                    (v) => _updateNavConfig((c) => c.copyWith(blurSigma: v)),
                    divisions: 40,
                    subtitle: cfg.blurSigma < 1
                        ? 'Clear glass, tint without frost'
                        : null),
                _buildSliderRow('Refraction Depth', cfg.refractionDepth, 0, 100,
                    (v) => _updateNavConfig((c) => c.copyWith(refractionDepth: v)),
                    divisions: 100),
                _buildSliderRow(
                    'Refraction Strength', cfg.refractionStrength, 0, 100,
                    (v) => _updateNavConfig(
                        (c) => c.copyWith(refractionStrength: v)),
                    divisions: 100),
                _buildSliderRow('Chromatic Split', cfg.chromaticSplit, 0, 100,
                    (v) => _updateNavConfig((c) => c.copyWith(chromaticSplit: v)),
                    divisions: 100),
                if (!cfg.useIOSGlassMode)
                  Padding(
                    padding: const EdgeInsets.only(top: Space.xs),
                    child: Text(
                      'Refraction and chromatic split need the Android 13+ engine.',
                      style: TextStyle(
                          color: AppColors.themeTertiary(context), fontSize: 11),
                    ),
                  ),
              ],
            ),
          ),
          _navFxDivider(),

          _navFxLabel('GESTURES'),
          const SizedBox(height: Space.md),
          _buildToggleRow(
            title: 'Hold to swipe',
            subtitle: cfg.holdToSwipe
                ? 'Drag across the bar to change tabs'
                : 'Swiping off, tap a tab to change',
            value: cfg.holdToSwipe,
            onChanged: (v) => _updateNavConfig((c) => c.copyWith(holdToSwipe: v)),
          ),
          const SizedBox(height: Space.lg),
          _buildSliderRow('Swipe Sensitivity', cfg.swipeSensitivity, 1, 10,
              (v) => _updateNavConfig((c) => c.copyWith(swipeSensitivity: v)),
              divisions: 9,
              subtitle: 'Higher commits sooner, lower needs a longer drag'),
          _buildToggleRow(
            title: 'Invert swipe direction',
            subtitle: cfg.invertSwipe
                ? 'Drag right for the next tab, the highlight follows your finger'
                : 'Drag right for the previous tab, the strip moves under it',
            value: cfg.invertSwipe,
            onChanged: (v) => _updateNavConfig((c) => c.copyWith(invertSwipe: v)),
          ),
          _navFxDivider(),

          _navFxLabel('PREVIEW'),
          const SizedBox(height: Space.xs),
          Text(
            cfg.holdToSwipe
                ? 'Live, not a picture. Tap a tab or drag across the bar to try '
                    'the gestures above.'
                : 'Live, not a picture. Tap a tab to try it — swiping is off.',
            style: TextStyle(
              color: AppColors.themeTertiary(context),
              fontSize: 11,
              height: 1.35,
            ),
          ),
          const SizedBox(height: Space.md),
          _buildNavBarPreview(cfg),
          const SizedBox(height: Space.lg),

          _buildOutlineButton(
            label: 'Reset all glass settings',
            icon: Icons.water_drop_outlined,
            onTap: () => _updateNavConfig((c) => c.resetGlassEffects()),
          ),
          const SizedBox(height: Space.sm),
          _buildOutlineButton(
            label: 'Restore navigation bar defaults',
            icon: Icons.settings_backup_restore_rounded,
            onTap: () => _updateNavConfig((_) => const NavBarConfig()),
          ),
        ],
      ),
    );
  }

  Widget _navFxHeader() {
    final scheme = Theme.of(context).colorScheme;
    final dark = scheme.brightness == Brightness.dark;
    return Row(
      children: [
        Container(
          width: 38,
          height: 38,
          decoration: BoxDecoration(
            color: AppColors.primaryAccent
                .withValues(alpha: dark ? 0.18 : 0.14),
            borderRadius: BorderRadius.circular(Radii.core(Radii.card, Space.lg)),
            border: Border.all(
                color: AppColors.primaryAccent.withValues(alpha: 0.35)),
          ),
          child: Icon(Icons.water_drop_rounded,
              size: 19,
              color: legibleAccent(AppColors.primaryAccent, scheme.surface,
                  minRatio: 3)),
        ),
        const SizedBox(width: Space.md),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'Navigation Bar Effects',
                style: TextStyle(
                  color: AppColors.themePrimary(context),
                  fontSize: 14.5,
                  fontWeight: FontWeight.w700,
                  letterSpacing: -0.2,
                ),
              ),
              const SizedBox(height: 1),
              Text(
                'The tab bar is the only glass layer in the app',
                style: TextStyle(
                    color: AppColors.themeTertiary(context), fontSize: 11),
              ),
            ],
          ),
        ),
      ],
    );
  }

  /// Reads back what is actually rendering right now, which is not always what
  /// the switches say: the app-wide Material theme and the platform's reduced
  /// transparency setting both outrank them.
  Widget _navFxStatusChip(NavBarConfig cfg) {
    final scheme = Theme.of(context).colorScheme;
    final dark = scheme.brightness == Brightness.dark;
    final opaque = reduceTransparency(context);
    final glassOn = cfg.enabled && appGlassMode.value && !opaque;
    final String detail;
    if (opaque) {
      detail = 'Solid, system asked for reduced transparency';
    } else if (!appGlassMode.value) {
      detail = 'Solid, Material surface style is selected';
    } else if (!cfg.enabled) {
      detail = 'Solid, Liquid Glass is switched off';
    } else {
      detail = cfg.useIOSGlassMode
          ? 'iOS Liquid Glass (Android 13+)'
          : 'iOS Liquid Glass (Android 8+)';
    }
    final tint = glassOn
        ? legibleAccent(AppColors.primaryAccent, scheme.surface, minRatio: 4)
        : AppColors.themeSecondary(context);
    return Container(
      padding: const EdgeInsets.symmetric(
          horizontal: Space.md, vertical: Space.sm),
      decoration: BoxDecoration(
        color: glassOn
            ? AppColors.primaryAccent.withValues(alpha: dark ? 0.13 : 0.10)
            : scheme.onSurface.withValues(alpha: dark ? 0.05 : 0.04),
        borderRadius: BorderRadius.circular(Radii.pill),
        border: Border.all(
          color: glassOn
              ? AppColors.primaryAccent.withValues(alpha: 0.32)
              : scheme.outlineVariant.withValues(alpha: dark ? 0.35 : 0.7),
        ),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: 6,
            height: 6,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: glassOn ? AppColors.success : AppColors.themeTertiary(context),
            ),
          ),
          const SizedBox(width: Space.sm),
          Flexible(
            child: Text(
              'Running: $detail',
              maxLines: 2,
              style: TextStyle(
                color: tint,
                fontSize: 11.5,
                fontWeight: FontWeight.w600,
                letterSpacing: -0.05,
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _navFxLabel(String text) => Text(
        text,
        style: TextStyle(
          color: AppColors.themeTertiary(context),
          fontSize: 10.5,
          fontWeight: FontWeight.w700,
          letterSpacing: 1.1,
        ),
      );

  Widget _navFxDivider() => Divider(
        color: AppColors.divider(context),
        height: Space.xxl,
      );

  Widget _navFxGroup({required bool enabled, required Widget child}) {
    return AnimatedOpacity(
      duration: Motion.fast,
      curve: kFadeCurve,
      opacity: enabled ? 1 : 0.42,
      child: IgnorePointer(ignoring: !enabled, child: child),
    );
  }

  // Selectable card for the Material / Liquid Glass UI theme choice.
  Widget _themeChoiceCard({
    required String label,
    required IconData icon,
    required String description,
    required bool selected,
    required VoidCallback onTap,
  }) {
    final scheme = Theme.of(context).colorScheme;
    final dark = scheme.brightness == Brightness.dark;
    return GestureDetector(
      onTap: onTap,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 200),
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 14),
        decoration: BoxDecoration(
          color: selected
              ? AppColors.primaryAccent.withValues(alpha: dark ? 0.22 : 0.12)
              : Colors.transparent,
          borderRadius: BorderRadius.circular(Radii.control),
          border: Border.all(
            color: selected
                ? AppColors.primaryAccent.withValues(alpha: dark ? 0.6 : 0.5)
                : scheme.outlineVariant.withValues(alpha: dark ? 0.25 : 0.4),
            width: selected ? 1.4 : 1,
          ),
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon,
                size: 20,
                color: selected ? AppColors.primaryAccent : AppColors.themeTertiary(context)),
            const SizedBox(height: 8),
            Text(
              label,
              style: TextStyle(
                color: selected ? AppColors.themePrimary(context) : AppColors.themeSecondary(context),
                fontSize: 13,
                fontWeight: selected ? FontWeight.w700 : FontWeight.w500,
              ),
            ),
            const SizedBox(height: 2),
            Text(
              description,
              textAlign: TextAlign.center,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(
                color: AppColors.themeTertiary(context),
                fontSize: 10,
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildGlassModeOption(String label, bool iosMode, [String? detail]) {
    final selected = _navCfg.useIOSGlassMode == iosMode;
    final scheme = Theme.of(context).colorScheme;
    final accent =
        legibleAccent(AppColors.primaryAccent, scheme.surface, minRatio: 3);
    return Semantics(
      inMutuallyExclusiveGroup: true,
      selected: selected,
      label: label,
      child: GestureDetector(
        onTap: () {
          if (selected) return;
          HapticFeedback.selectionClick();
          _updateNavConfig((c) => c.copyWith(useIOSGlassMode: iosMode));
        },
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Padding(
              padding: const EdgeInsets.only(top: 1),
              child: Icon(
                selected
                    ? Icons.radio_button_checked
                    : Icons.radio_button_unchecked,
                color: selected ? accent : AppColors.themeTertiary(context),
                size: 18,
              ),
            ),
            const SizedBox(width: Space.md - 2),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    label,
                    style: TextStyle(
                      color: selected
                          ? AppColors.themePrimary(context)
                          : AppColors.themeSecondary(context),
                      fontSize: 13,
                      fontWeight: selected ? FontWeight.w600 : FontWeight.w400,
                      letterSpacing: -0.1,
                    ),
                  ),
                  if (detail != null) ...[
                    const SizedBox(height: 2),
                    Text(
                      detail,
                      style: TextStyle(
                          color: AppColors.themeTertiary(context), fontSize: 11),
                    ),
                  ],
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildSliderRow(
    String label,
    double value,
    double min,
    double max,
    ValueChanged<double> onChanged, {
    String? subtitle,
    int? divisions,
    String suffix = '',
  }) {
    final scheme = Theme.of(context).colorScheme;
    final dark = scheme.brightness == Brightness.dark;
    final accent =
        legibleAccent(AppColors.primaryAccent, scheme.surface, minRatio: 4);
    return Padding(
      padding: const EdgeInsets.only(bottom: Space.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(
                child: Text(
                  label,
                  style: TextStyle(
                    color: AppColors.themePrimary(context),
                    fontSize: 12.5,
                    fontWeight: FontWeight.w500,
                    letterSpacing: -0.1,
                  ),
                ),
              ),
              // Tabular value readout, so the number does not jump sideways as
              // the thumb moves.
              Container(
                constraints: const BoxConstraints(minWidth: 44),
                padding: const EdgeInsets.symmetric(
                    horizontal: Space.sm, vertical: 2),
                decoration: BoxDecoration(
                  color: scheme.onSurface
                      .withValues(alpha: dark ? 0.055 : 0.04),
                  borderRadius: BorderRadius.circular(Radii.tight),
                ),
                child: Text(
                  '${value.round()}$suffix',
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    color: accent,
                    fontSize: 11.5,
                    fontWeight: FontWeight.w700,
                    fontFeatures: const [FontFeature.tabularFigures()],
                  ),
                ),
              ),
            ],
          ),
          if (subtitle != null)
            Padding(
              padding: const EdgeInsets.only(top: 2),
              child: Text(
                subtitle,
                style: TextStyle(
                    color: AppColors.themeTertiary(context), fontSize: 10.5),
              ),
            ),
          SliderTheme(
            data: SliderThemeData(
              activeTrackColor: AppColors.primaryAccent,
              inactiveTrackColor:
                  scheme.onSurface.withValues(alpha: dark ? 0.12 : 0.10),
              thumbColor: AppColors.primaryAccent,
              thumbShape: const RoundSliderThumbShape(enabledThumbRadius: 7.5),
              overlayColor: AppColors.primaryAccent.withValues(alpha: 0.14),
              overlayShape: const RoundSliderOverlayShape(overlayRadius: 16),
              activeTickMarkColor: Colors.transparent,
              inactiveTickMarkColor: Colors.transparent,
              trackHeight: 3,
              padding: EdgeInsets.zero,
            ),
            child: Slider(
              value: value.clamp(min, max),
              min: min,
              max: max,
              divisions: divisions,
              label: '${value.round()}$suffix',
              onChanged: onChanged,
              onChangeEnd: (_) => HapticFeedback.selectionClick(),
            ),
          ),
        ],
      ),
    );
  }

  /// The real navigation bar, live, over a busy backdrop.
  ///
  /// It is the actual widget with the actual settings, and it is *interactive*:
  /// tap a destination to watch the indicator travel, shimmer and light the
  /// glass, or drag across it to try the gesture group exactly as configured —
  /// hold-to-swipe, sensitivity and inversion all come from [cfg]. Selection is
  /// kept in [_previewSlot] so none of that moves the page behind the settings.
  /// Anything less would be a drawing of the settings rather than a preview of
  /// them.
  Widget _buildNavBarPreview(NavBarConfig cfg) {
    final scheme = Theme.of(context).colorScheme;
    final dark = scheme.brightness == Brightness.dark;
    final accent = AppColors.primaryAccent;
    final order = cfg.resolvedOrder(kNavTabLabels.length);
    final slot = _previewSlot.clamp(0, order.length - 1);
    return ClipRRect(
      borderRadius:
          BorderRadius.circular(Radii.core(Radii.card, Space.lg)),
      child: SizedBox(
        height: 156,
        width: double.infinity,
        child: Stack(
          fit: StackFit.expand,
          children: [
            DecoratedBox(
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  begin: Alignment.topLeft,
                  end: Alignment.bottomRight,
                  colors: dark
                      ? [
                          DarkSurface.base,
                          accent.withValues(alpha: 0.28),
                          DarkSurface.canvas,
                        ]
                      : [
                          LightSurface.high,
                          accent.withValues(alpha: 0.35),
                          LightSurface.base,
                        ],
                ),
              ),
            ),
            // High-frequency edges. A flat gradient hides every artefact the
            // refraction and blur sliders exist to control.
            Positioned.fill(
              child: CustomPaint(
                painter: _PreviewBackdropPainter(
                  warm: accent,
                  cool: accentCompanion(accent),
                  dark: dark,
                ),
              ),
            ),
            // The label of the destination the preview is on, so a drag that
            // lands is legible without counting icons.
            Positioned(
              left: Space.md,
              top: Space.sm,
              child: _previewSlotBadge(kNavTabLabels[order[slot]]),
            ),
            // Bottom pinned, exactly as the real bar sits in the app shell.
            Positioned(
              left: 0,
              right: 0,
              bottom: 0,
              child: LiquidGlassNavBar(
                selectedIndex: slot,
                onDestinationSelected: (i) =>
                    setState(() => _previewSlot = i),
                enabled: cfg.enabled,
                position: cfg.position,
                opacity: cfg.opacity,
                blurSigma: cfg.blurSigma,
                barSize: cfg.barSize,
                barWidth: cfg.barWidth,
                cornerRoundness: cfg.cornerRoundness,
                refractionDepth: cfg.refractionDepth,
                refractionStrength: cfg.refractionStrength,
                chromaticSplit: cfg.chromaticSplit,
                useIOSGlassMode: cfg.useIOSGlassMode,
                holdToSwipe: cfg.holdToSwipe,
                swipeSensitivity: cfg.swipeSensitivity,
                invertSwipe: cfg.invertSwipe,
                destinations: [
                  for (final i in order)
                    LiquidNavDestination(
                      icon: kNavTabIcons[i],
                      selectedIcon: kNavTabSelectedIcons[i],
                      label: kNavTabLabels[i],
                    ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  /// Small caption over the preview backdrop naming the selected destination.
  Widget _previewSlotBadge(String label) {
    return Container(
      padding: const EdgeInsets.symmetric(
          horizontal: Space.sm, vertical: Space.xs - 1),
      decoration: BoxDecoration(
        color: Theme.of(context)
            .colorScheme
            .surface
            .withValues(alpha: 0.55),
        borderRadius: BorderRadius.circular(Radii.pill),
      ),
      child: Text(
        label.toUpperCase(),
        style: TextStyle(
          color: AppColors.themeSecondary(context),
          fontSize: 10,
          fontWeight: FontWeight.w700,
          letterSpacing: 1.1,
        ),
      ),
    );
  }

  // ── Device Info Panel ───────────────────────────────────────────────

  Widget _buildDeviceInfoPanel() {
    final svc = DeviceInfoService();
    return FutureBuilder<DeviceProfile?>(
      future: () async {
        if (svc.info == null) await svc.init();
        return svc.info;
      }(),
      builder: (context, snapshot) {
        final info = snapshot.data;
        if (info == null) {
          return const ProcessingBar(label: 'Reading this device');
        }
        final keys = info.summary.keys.toList();
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Padding(
              padding: const EdgeInsets.only(top: Space.sm, bottom: Space.md),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(info.deviceModel, style: AppType.title(context)),
                        const SizedBox(height: 2),
                        Text('Android ${info.osVersion}',
                            style: AppType.meta(context)),
                      ],
                    ),
                  ),
                  const SizedBox(width: Space.md),
                  // The tier is a judgement about this phone, so it is stated
                  // as a word at the type's own size rather than as a badge.
                  Text(
                    '${sentenceCase(info.tier.name)} tier',
                    style: AppType.meta(context).copyWith(
                      color: legibleAccent(_tierColor(info.tier),
                          Theme.of(context).colorScheme.surface),
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ],
              ),
            ),
            for (var i = 0; i < keys.length; i++)
              LedgerFact(
                keys[i],
                info.summary[keys[i]]!,
                divided: i != keys.length - 1,
              ),
          ],
        );
      },
    );
  }

  Color _tierColor(DeviceTier tier) {
    switch (tier) {
      case DeviceTier.high: return Semantic.success;
      case DeviceTier.medium: return Semantic.warning;
      case DeviceTier.low: return Semantic.danger;
    }
  }
}

/// A group name in Settings. It reads as a heading on a hairline now instead of
/// a wide-tracked uppercase eyebrow; the call sites still pass their original
/// shouted strings, and [sentenceCase] un-shouts them.
class _SectionLabel extends StatelessWidget {
  final String text;
  const _SectionLabel(this.text);

  @override
  Widget build(BuildContext context) => SectionHeader(
        sentenceCase(text),
        top: Space.xl,
        bottom: Space.xs,
      );
}

/// Backdrop for the nav bar preview: diagonal bands plus a couple of discs, in
/// the accent's own hue family. Its only job is to give the glass something with
/// hard edges to bend, since blur and refraction are invisible over flat fill.
class _PreviewBackdropPainter extends CustomPainter {
  const _PreviewBackdropPainter({
    required this.warm,
    required this.cool,
    required this.dark,
  });

  final Color warm;
  final Color cool;
  final bool dark;

  @override
  void paint(Canvas canvas, Size size) {
    if (size.isEmpty) return;
    canvas.save();
    canvas.clipRect(Offset.zero & size);

    final band = Paint()..style = PaintingStyle.fill;
    final step = size.width / 7;
    for (var i = -2; i < 9; i++) {
      band.color = (i.isEven ? warm : cool)
          .withValues(alpha: dark ? 0.16 : 0.20);
      final x = i * step;
      canvas.drawPath(
        Path()
          ..moveTo(x, 0)
          ..lineTo(x + step * 0.42, 0)
          ..lineTo(x + step * 0.42 - size.height * 0.5, size.height)
          ..lineTo(x - size.height * 0.5, size.height)
          ..close(),
        band,
      );
    }

    canvas.drawCircle(
      Offset(size.width * 0.22, size.height * 0.3),
      size.height * 0.26,
      Paint()..color = warm.withValues(alpha: dark ? 0.30 : 0.34),
    );
    canvas.drawCircle(
      Offset(size.width * 0.78, size.height * 0.24),
      size.height * 0.18,
      Paint()..color = cool.withValues(alpha: dark ? 0.34 : 0.38),
    );
    // A hairline grid: the finest detail present, so it is the first thing the
    // blur slider visibly eats.
    final grid = Paint()
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1
      ..color = (dark ? Colors.white : Colors.black)
          .withValues(alpha: dark ? 0.10 : 0.08);
    for (var y = size.height * 0.12; y < size.height; y += 9) {
      canvas.drawLine(Offset(0, y), Offset(size.width, y), grid);
    }
    canvas.restore();
  }

  @override
  bool shouldRepaint(_PreviewBackdropPainter old) =>
      old.warm != warm || old.cool != cool || old.dark != dark;
}

// ─────────────────────────────────────────────────────────────────────────────
// EXTENSION: Regex fallback exposed publicly for Dashboard toggleChecklist
// ─────────────────────────────────────────────────────────────────────────────

extension OllamaProcessorExt on OllamaClipProcessor {
  String processWithRegexPublic(String text) {
    return _processWithRegex(text);
  }
}
