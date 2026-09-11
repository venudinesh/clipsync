import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:hive_flutter/hive_flutter.dart';
import 'package:intl/intl.dart';

import '../ui/ui.dart';

/// Hive box holding chat transcripts. It carries its own AES key rather than
/// riding on the notes key, because a conversation with the local model can
/// contain anything the user pasted into it.
const String kChatBoxName = 'chat_sessions';

/// A short, human readable age: "just now", "12m ago", "Mar 4".
String relativeTime(DateTime t) {
  final d = DateTime.now().difference(t);
  if (d.inSeconds < 60) return 'just now';
  if (d.inMinutes < 60) return '${d.inMinutes}m ago';
  if (d.inHours < 24) return '${d.inHours}h ago';
  if (d.inDays < 7) return '${d.inDays}d ago';
  return DateFormat('MMM d').format(t);
}

/// An attachment as it survives a restart: enough to redraw the chip on a
/// bubble, without the extracted text, which was already folded into the
/// prompt the model answered.
class StoredAttachment {
  const StoredAttachment({required this.name, required this.type});
  final String name;
  final String type;

  Map<String, dynamic> toMap() => {'name': name, 'type': type};

  factory StoredAttachment.fromMap(Map<dynamic, dynamic> m) => StoredAttachment(
    name: (m['name'] as String?) ?? 'attachment',
    type: (m['type'] as String?) ?? 'document',
  );
}

/// One turn in a persisted conversation.
class StoredMessage {
  const StoredMessage({
    required this.role,
    required this.content,
    this.prompt,
    this.attachments = const [],
  });

  final String role; // 'user' or 'assistant'
  final String content;

  /// What the model was handed, when it differs from [content] (attachment
  /// text, or a one-tap action's expanded instruction). Kept so a restored
  /// conversation can still answer follow-ups about an attached document.
  final String? prompt;
  final List<StoredAttachment> attachments;

  Map<String, dynamic> toMap() => {
    'role': role,
    'content': content,
    if (prompt != null) 'prompt': prompt,
    if (attachments.isNotEmpty)
      'attachments': attachments.map((a) => a.toMap()).toList(),
  };

  factory StoredMessage.fromMap(Map<dynamic, dynamic> m) {
    final raw = m['attachments'];
    return StoredMessage(
      role: (m['role'] as String?) == 'user' ? 'user' : 'assistant',
      content: (m['content'] as String?) ?? '',
      prompt: m['prompt'] as String?,
      attachments: raw is List
          ? raw.whereType<Map>().map(StoredAttachment.fromMap).toList()
          : const [],
    );
  }
}

/// A whole conversation. One Hive entry per session, keyed by [id], so saving
/// the open chat never rewrites every other transcript.
class ChatSession {
  ChatSession({
    required this.id,
    required this.title,
    required this.messages,
    required this.createdAt,
    required this.updatedAt,
    this.isPinned = false,
  });

  final String id;
  String title;
  List<StoredMessage> messages;
  bool isPinned;
  final DateTime createdAt;
  DateTime updatedAt;

  factory ChatSession.blank() {
    final now = DateTime.now();
    return ChatSession(
      id: 'chat_${now.microsecondsSinceEpoch}',
      title: 'New chat',
      messages: <StoredMessage>[],
      createdAt: now,
      updatedAt: now,
    );
  }

  bool get isEmpty => messages.isEmpty;

  /// First non-empty line, for the history row's second line.
  String get preview {
    for (final m in messages) {
      final t = m.content.trim();
      if (t.isNotEmpty) return t.replaceAll(RegExp(r'\s+'), ' ');
    }
    return 'Empty conversation';
  }

  /// The conversation as plain text, for "Copy transcript".
  String get transcript {
    final b = StringBuffer();
    for (final m in messages) {
      if (m.content.trim().isEmpty) continue;
      b.writeln(m.role == 'user' ? 'You:' : 'ClipSync AI:');
      b.writeln(m.content.trim());
      b.writeln();
    }
    return b.toString().trimRight();
  }

  Map<String, dynamic> toMap() => {
    'id': id,
    'title': title,
    'isPinned': isPinned,
    'createdAt': createdAt.toIso8601String(),
    'updatedAt': updatedAt.toIso8601String(),
    'messages': messages.map((m) => m.toMap()).toList(),
  };

  factory ChatSession.fromMap(Map<dynamic, dynamic> m) {
    final raw = m['messages'];
    final created = DateTime.tryParse(m['createdAt'] as String? ?? '');
    final updated = DateTime.tryParse(m['updatedAt'] as String? ?? '');
    final id =
        (m['id'] as String?) ?? 'chat_${DateTime.now().microsecondsSinceEpoch}';
    return ChatSession(
      id: id,
      title: (m['title'] as String?)?.trim().isNotEmpty == true
          ? m['title'] as String
          : 'Untitled chat',
      messages: raw is List
          ? raw.whereType<Map>().map(StoredMessage.fromMap).toList()
          : <StoredMessage>[],
      isPinned: m['isPinned'] == true,
      createdAt: created ?? DateTime.now(),
      updatedAt: updated ?? created ?? DateTime.now(),
    );
  }

  /// Derives a session title from the first thing the user typed.
  static String titleFrom(String raw) {
    final cleaned = raw.trim().replaceAll(RegExp(r'\s+'), ' ');
    if (cleaned.isEmpty) return 'New chat';
    final clipped = cleaned.characters.take(42).toString().trim();
    return clipped.length < cleaned.length ? '$clipped…' : clipped;
  }
}

/// Thin wrapper over the Hive box so widgets never handle raw maps.
class ChatStore {
  ChatStore._(this._box);
  final Box _box;

  static ChatStore? _instance;

  /// The store, or null when the box could not be opened at startup. Chat then
  /// runs in memory for the session instead of failing outright.
  static ChatStore? get instance {
    final cached = _instance;
    if (cached != null) return cached;
    if (!Hive.isBoxOpen(kChatBoxName)) return null;
    return _instance = ChatStore._(Hive.box(kChatBoxName));
  }

  /// Every session, pinned first, then most recently updated.
  List<ChatSession> all() {
    final out = <ChatSession>[];
    for (final v in _box.values) {
      if (v is Map) {
        try {
          out.add(ChatSession.fromMap(v));
        } catch (_) {
          // A single unreadable entry must not take the whole list down.
        }
      }
    }
    out.sort((a, b) {
      if (a.isPinned != b.isPinned) return a.isPinned ? -1 : 1;
      return b.updatedAt.compareTo(a.updatedAt);
    });
    return out;
  }

  ChatSession? byId(String id) {
    final v = _box.get(id);
    if (v is Map) {
      try {
        return ChatSession.fromMap(v);
      } catch (_) {}
    }
    return null;
  }

  Future<void> save(ChatSession s) => _box.put(s.id, s.toMap());
  Future<void> remove(String id) => _box.delete(id);
  Future<void> removeAll() => _box.clear();
  int get count => _box.length;
}

/// Asks for a new title. Returns the trimmed value, or null if the user
/// cancelled or cleared it. Shared by the history sheet and the chat header so
/// renaming behaves identically from either side.
Future<String?> promptForChatTitle(
  BuildContext context, {
  required String initial,
}) async {
  final ctrl = TextEditingController(text: initial);
  final entered = await showDialog<String>(
    context: context,
    builder: (dctx) => AlertDialog(
      title: const Text('Rename chat'),
      content: TextField(
        controller: ctrl,
        autofocus: true,
        maxLength: 60,
        textCapitalization: TextCapitalization.sentences,
        decoration: const InputDecoration(hintText: 'Chat title'),
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
  );
  ctrl.dispose();
  final title = entered?.trim();
  return (title == null || title.isEmpty) ? null : title;
}

/// Opens the history sheet and resolves with the id of the session the user
/// tapped, or null if they dismissed it. Deletions are committed to the store
/// as they happen, so a caller whose own chat may have been deleted has to
/// re-check the store rather than trust this value: the sheet can also be
/// dismissed by dragging, which returns nothing at all.
Future<String?> showChatHistorySheet(
  BuildContext context, {
  required ChatStore store,
  String? currentId,
}) {
  return showModalBottomSheet<String>(
    context: context,
    isScrollControlled: true,
    backgroundColor: Colors.transparent,
    barrierColor: Colors.black.withValues(alpha: 0.46),
    builder: (_) => _ChatHistorySheet(store: store, currentId: currentId),
  );
}

class _ChatHistorySheet extends StatefulWidget {
  const _ChatHistorySheet({required this.store, this.currentId});
  final ChatStore store;
  final String? currentId;

  @override
  State<_ChatHistorySheet> createState() => _ChatHistorySheetState();
}

class _ChatHistorySheetState extends State<_ChatHistorySheet> {
  final TextEditingController _search = TextEditingController();
  late List<ChatSession> _sessions;

  @override
  void initState() {
    super.initState();
    _sessions = widget.store.all();
  }

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  List<ChatSession> get _visible {
    final q = _search.text.trim().toLowerCase();
    if (q.isEmpty) return _sessions;
    return _sessions.where((s) {
      if (s.title.toLowerCase().contains(q)) return true;
      return s.messages.any((m) => m.content.toLowerCase().contains(q));
    }).toList();
  }

  void _reload() => setState(() => _sessions = widget.store.all());

  Future<void> _togglePin(ChatSession s) async {
    HapticFeedback.selectionClick();
    s.isPinned = !s.isPinned;
    await widget.store.save(s);
    _reload();
  }

  Future<void> _delete(ChatSession s) async {
    final snapshot = s.toMap();
    // Dropped from the visible list in the same frame the row leaves, before
    // the box is asked to write. A row swiped away has to be gone from the list
    // immediately; waiting on the write would leave it on screen for a frame.
    setState(() => _sessions = _sessions.where((e) => e.id != s.id).toList());
    await widget.store.remove(s.id);
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text('Deleted "${s.title}"'),
        behavior: SnackBarBehavior.floating,
        duration: const Duration(seconds: 4),
        action: SnackBarAction(
          label: 'Undo',
          onPressed: () async {
            await widget.store.save(ChatSession.fromMap(snapshot));
            if (mounted) _reload();
          },
        ),
      ),
    );
  }

  Future<void> _rename(ChatSession s) async {
    final title = await promptForChatTitle(context, initial: s.title);
    if (title == null) return;
    s.title = title;
    await widget.store.save(s);
    if (mounted) _reload();
  }

  Future<void> _deleteAll() async {
    final n = _sessions.length;
    final ok = await showDialog<bool>(
      context: context,
      builder: (dctx) => AlertDialog(
        title: const Text('Delete all chats?'),
        content: Text(
          n == 1
              ? 'One conversation will be removed from this device.'
              : '$n conversations will be removed from this device.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dctx, false),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () => Navigator.pop(dctx, true),
            style: TextButton.styleFrom(
              foregroundColor: legibleAccent(
                Semantic.danger,
                Theme.of(dctx).colorScheme.surface,
              ),
            ),
            child: const Text('Delete all',
                style: TextStyle(fontWeight: FontWeight.w700)),
          ),
        ],
      ),
    );
    if (ok != true) return;
    await widget.store.removeAll();
    if (mounted) _reload();
  }

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final visible = _visible;
    final media = MediaQuery.of(context);
    final n = _sessions.length;
    return Padding(
      padding: EdgeInsets.only(bottom: media.viewInsets.bottom),
      child: ConstrainedBox(
        constraints: BoxConstraints(maxHeight: media.size.height * 0.82),
        child: DecoratedBox(
          decoration: BoxDecoration(
            color: scheme.surface,
            borderRadius: const BorderRadius.vertical(
              top: Radius.circular(Radii.card),
            ),
            border: Border(top: BorderSide(color: hairline(context))),
          ),
          child: SafeArea(
            top: false,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                SheetHeader(
                  title: 'Chat history',
                  subtitle: n == 0
                      ? 'Nothing saved yet'
                      : '$n saved ${n == 1 ? 'conversation' : 'conversations'}',
                  action: n == 0
                      ? null
                      : IconAction(
                          icon: Icons.delete_sweep_outlined,
                          tooltip: 'Delete all chats',
                          size: 20,
                          tone: legibleAccent(Semantic.danger, scheme.surface),
                          onPressed: _deleteAll,
                        ),
                ),
                if (_sessions.isNotEmpty) _searchBox(),
                if (visible.isEmpty)
                  _emptyNote()
                else
                  Flexible(
                    child: ListView.builder(
                      padding: const EdgeInsets.fromLTRB(
                          Space.xl, 0, Space.xl, Space.md),
                      itemCount: visible.length,
                      itemBuilder: (_, i) => _row(visible[i]),
                    ),
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _searchBox() {
    return Padding(
      padding: const EdgeInsets.fromLTRB(Space.xl, Space.md, Space.xl, Space.xs),
      child: InlineField(
        controller: _search,
        hint: 'Search chats',
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
      ),
    );
  }

  Widget _emptyNote() {
    return Padding(
      padding: const EdgeInsets.fromLTRB(Space.xl, Space.lg, Space.xl, Space.xl),
      child: StateBlock(
        compact: true,
        icon: _sessions.isEmpty ? Icons.forum_outlined : Icons.search_off_rounded,
        title: _sessions.isEmpty ? 'No conversations yet' : 'No match',
        message: _sessions.isEmpty
            ? 'Chats are saved here as you have them, encrypted on this phone.'
            : 'Nothing here matches that search. Try a word from the reply.',
      ),
    );
  }

  Widget _row(ChatSession s) {
    final isCurrent = s.id == widget.currentId;
    final n = s.messages.length;
    // The open chat is marked by the rule's own accent tick, not by a tinted
    // card and an uppercase badge. When it was last touched hangs in the margin
    // with the other feeds in the app, so the row itself is title and preview.
    return MarginEntry(
      margin: stamp(s.updatedAt),
      flag: isCurrent,
      onTap: () => Navigator.pop(context, s.id),
      onLongPress: () => _sessionSheet(s),
      marginAction: IconAction(
        icon: Icons.more_horiz,
        tooltip: 'Chat options',
        size: 17,
        dense: true,
        alignment: Alignment.centerRight,
        tone: ink(context, 0.42),
        onPressed: () => _sessionSheet(s),
      ),
      swipeId: s.id,
      swipeRight: SwipeAct(
        icon: s.isPinned ? Icons.push_pin_rounded : Icons.push_pin_outlined,
        label: s.isPinned ? 'Unpin' : 'Pin',
        onAct: () => _togglePin(s),
      ),
      swipeLeft: SwipeAct(
        icon: Icons.delete_outline_rounded,
        label: 'Delete',
        tone: Semantic.danger,
        dismiss: true,
        onAct: () => _delete(s),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Row(
            children: [
              if (s.isPinned) ...[
                Icon(Icons.push_pin_rounded, size: 12, color: ink(context, 0.52)),
                const SizedBox(width: 5),
              ],
              Expanded(
                child: Text(
                  s.title,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: AppType.heading(context).copyWith(fontSize: 14),
                ),
              ),
            ],
          ),
          const SizedBox(height: 3),
          Text(
            s.preview,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: AppType.body(context),
          ),
          const SizedBox(height: Space.sm - 2),
          Text(plural(n, 'message'), style: AppType.meta(context)),
        ],
      ),
    );
  }

  /// What you can do to one saved conversation. The same sheet the rest of the
  /// app opens from a row's margin, so nothing in this list needs a dropdown of
  /// its own.
  Future<void> _sessionSheet(ChatSession s) async {
    HapticFeedback.selectionClick();
    final n = s.messages.length;
    await ledgerSheet<void>(
      context,
      title: s.title,
      subtitle: '${plural(n, 'message')} · ${stampLong(s.updatedAt)}',
      children: (ctx) => [
        SheetAction(
          icon: Icons.open_in_new_rounded,
          label: 'Open',
          onTap: () {
            Navigator.pop(ctx);
            Navigator.pop(context, s.id);
          },
        ),
        SheetAction(
          icon: Icons.drive_file_rename_outline_rounded,
          label: 'Rename',
          onTap: () {
            Navigator.pop(ctx);
            _rename(s);
          },
        ),
        SheetAction(
          icon: Icons.push_pin_rounded,
          label: s.isPinned ? 'Unpin' : 'Pin to top',
          detail: s.isPinned ? null : 'Keeps it above the rest of the list',
          onTap: () {
            Navigator.pop(ctx);
            _togglePin(s);
          },
        ),
        SheetAction(
          icon: Icons.content_copy_rounded,
          label: 'Copy transcript',
          onTap: () {
            Navigator.pop(ctx);
            Clipboard.setData(ClipboardData(text: s.transcript));
            ScaffoldMessenger.of(context).showSnackBar(
              const SnackBar(
                content: Text('Transcript copied'),
                behavior: SnackBarBehavior.floating,
                duration: Duration(milliseconds: 900),
              ),
            );
          },
        ),
        SheetAction(
          icon: Icons.delete_outline_rounded,
          label: 'Delete chat',
          danger: true,
          divided: false,
          onTap: () {
            Navigator.pop(ctx);
            _delete(s);
          },
        ),
      ],
    );
  }
}
