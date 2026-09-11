import 'dart:async';
import 'dart:io';

import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_markdown/flutter_markdown.dart';
import 'package:hive_flutter/hive_flutter.dart';
import 'package:google_mlkit_text_recognition/google_mlkit_text_recognition.dart';
import 'package:image_picker/image_picker.dart';
import 'package:whisper_flutter_new/whisper_flutter_new.dart';

import '../services/on_device_llm_service.dart';
import '../services/ai_connection_service.dart';
import '../services/voice_input_service.dart';
import '../ui/ui.dart';
import 'chat_history.dart';

/// The pulled-in corner of a chat bubble. Concentric with [Radii.inner] across
/// a [Space.md] bezel, so the tail curves parallel to the other three corners
/// instead of arriving at its own unrelated radius.
final double _bubbleTail = Radii.core(Radii.inner, Space.md);

/// Represents an attached file in a chat message.
class _Attachment {
  final String name;
  final String? filePath;
  final String? textContent; // extracted text from doc/image
  final AttachmentType type;

  const _Attachment({
    required this.name,
    this.filePath,
    this.textContent,
    required this.type,
  });
}

enum AttachmentType { image, document, audio, clipboard }

/// A chat message displayed in the conversation.
class _ChatBubble {
  final String role; // 'user' or 'assistant'
  final String content;

  /// What the model was actually handed, when that differs from what the
  /// bubble shows. Attachment text lives here, so the composer can send a
  /// document without pasting the whole thing into the visible turn, and a
  /// one-tap action can read "Summarize that message" while the model still
  /// receives the message itself.
  final String? promptText;
  final bool isStreaming;
  final List<_Attachment> attachments;

  const _ChatBubble({
    required this.role,
    required this.content,
    this.promptText,
    this.isStreaming = false,
    this.attachments = const [],
  });

  /// The text to put in the LLM's history for this turn.
  String get forModel => promptText ?? content;
}

/// Chat view for conversing with the locally loaded LLM.
class ChatView extends StatefulWidget {
  final OnDeviceLlmService llm;
  final AiConnectionService connection;
  const ChatView({super.key, required this.llm, required this.connection});

  @override
  State<ChatView> createState() => _ChatViewState();
}

class _ChatViewState extends State<ChatView> with AutomaticKeepAliveClientMixin {
  @override
  bool get wantKeepAlive => true;
  final TextEditingController _controller = TextEditingController();
  final ScrollController _scrollController = ScrollController();

  /// Held so an opener can drop its wording into the field and put the caret
  /// after it, rather than firing a half-finished question at the model.
  final FocusNode _composerFocus = FocusNode();
  final List<_ChatBubble> _messages = [];
  final VoiceInputService _voiceInput = VoiceInputService();
  final List<_Attachment> _pendingAttachments = [];
  bool _isGenerating = false;
  bool _isRecordingVoice = false;
  bool _isTranscribing = false;
  bool _isProcessingAttachment = false;
  StreamSubscription<String>? _streamSub;
  StreamSubscription<double>? _ampSub;
  double _amplitude = 0.0;
  double _temperature = 0.7;

  bool get _canChat => widget.llm.isLoaded || widget.connection.config.value.isConfigured;

  /// The conversation currently on screen. Null store means the encrypted box
  /// did not open, in which case chat still works but only for this run.
  ChatStore? _store;
  ChatSession _session = ChatSession.blank();

  @override
  void initState() {
    super.initState();
    _voiceInput.init();
    _restoreLastSession();
  }

  /// Reopens the conversation the user left, so Chat comes back where they left
  /// it instead of blank. Mutates fields directly: the first build has not run
  /// yet, so there is nothing to schedule.
  void _restoreLastSession() {
    final store = ChatStore.instance;
    _store = store;
    if (store == null) return;
    final all = store.all();
    if (all.isEmpty) return;
    // all() is pinned-first; "resume where I was" wants the most recent one
    // regardless of pinning.
    all.sort((a, b) => b.updatedAt.compareTo(a.updatedAt));
    _applySession(all.first);
  }

  void _applySession(ChatSession s) {
    _session = s;
    _messages
      ..clear()
      ..addAll(s.messages.map(_bubbleFrom));
    _pendingAttachments.clear();
    _isGenerating = false;
  }

  static _ChatBubble _bubbleFrom(StoredMessage m) => _ChatBubble(
        role: m.role,
        content: m.content,
        promptText: m.prompt,
        attachments: m.attachments
            .map((a) => _Attachment(name: a.name, type: _typeNamed(a.type)))
            .toList(),
      );

  static StoredMessage _storedFrom(_ChatBubble b) => StoredMessage(
        role: b.role,
        content: b.content,
        prompt: b.promptText,
        attachments: b.attachments
            .map((a) => StoredAttachment(name: a.name, type: a.type.name))
            .toList(),
      );

  static AttachmentType _typeNamed(String name) => AttachmentType.values
      .firstWhere((t) => t.name == name, orElse: () => AttachmentType.document);

  /// Writes the open conversation to the encrypted box. Called after each turn
  /// rather than on a timer, so being killed mid-conversation costs at most the
  /// tokens still streaming.
  Future<void> _persist() async {
    final store = _store;
    if (store == null) return;
    final keep = _messages
        .where((m) => !m.isStreaming || m.content.trim().isNotEmpty)
        .toList();
    // An untouched chat is not worth a row in the history list.
    if (keep.isEmpty) return;
    _session.messages = keep.map(_storedFrom).toList();
    _session.updatedAt = DateTime.now();
    if (_session.title == 'New chat') {
      final firstUser = keep.firstWhere(
        (m) => m.role == 'user' && m.content.trim().isNotEmpty,
        orElse: () => keep.first,
      );
      _session.title = ChatSession.titleFrom(firstUser.content);
    }
    try {
      await store.save(_session);
    } catch (e) {
      debugPrint('[Chat] Save failed: $e');
    }
  }

  @override
  void dispose() {
    _persist();
    _streamSub?.cancel();
    _ampSub?.cancel();
    _voiceInput.dispose();
    _controller.dispose();
    _scrollController.dispose();
    _composerFocus.dispose();
    super.dispose();
  }

  void _scrollToBottom() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (_scrollController.hasClients) {
        _scrollController.animateTo(
          _scrollController.position.maxScrollExtent,
          duration: const Duration(milliseconds: 200),
          curve: Curves.easeOut,
        );
      }
    });
  }

  // ── Send Message ──────────────────────────────────────────────────────

  /// Sends a turn. With no arguments it consumes the composer and its pending
  /// attachments. [display] and [prompt] let a one-tap action show a short
  /// user turn while handing the model something longer, and [attachments] is
  /// only used when replaying an existing turn.
  Future<void> _sendMessage({
    String? display,
    String? prompt,
    List<_Attachment>? attachments,
  }) async {
    if (_isGenerating) return;
    final composed = display != null;
    final visible = (display ?? _controller.text).trim();
    if (visible.isEmpty && (composed || _pendingAttachments.isEmpty)) return;
    final remoteReady = widget.connection.config.value.isConfigured;
    if (!widget.llm.isLoaded && !remoteReady) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('No model is ready. Load a GGUF model or configure a connection in Settings → AI.'),
          behavior: SnackBarBehavior.floating,
        ),
      );
      return;
    }

    // Only a plain send consumes the composer; a replay must leave a draft alone.
    final List<_Attachment> sent;
    if (composed) {
      sent = attachments ?? const <_Attachment>[];
    } else {
      sent = List<_Attachment>.from(_pendingAttachments);
      _pendingAttachments.clear();
      _controller.clear();
    }

    // Compose what the model actually reads, folding in attachment text.
    String modelPrompt;
    if (prompt != null) {
      modelPrompt = prompt;
    } else {
      final buffer = StringBuffer();
      if (visible.isNotEmpty) buffer.writeln(visible);
      for (final att in sent) {
        if (att.textContent != null && att.textContent!.isNotEmpty) {
          buffer.writeln('\n---\n${att.name}:\n${att.textContent}');
        }
      }
      modelPrompt = buffer.toString().trim();
    }
    if (modelPrompt.isEmpty) return;

    final shown = visible.isNotEmpty
        ? visible
        : sent.length == 1
            ? 'Sent ${sent.single.name}'
            : 'Sent ${sent.length} attachments';

    setState(() {
      _messages.add(_ChatBubble(
        role: 'user',
        content: shown,
        promptText: modelPrompt == shown ? null : modelPrompt,
        attachments: sent,
      ));
      _messages.add(const _ChatBubble(role: 'assistant', content: '', isStreaming: true));
      _isGenerating = true;
    });
    _scrollToBottom();
    _persist();

    // Build conversation history for the LLM
    final history = <ChatMessage>[
      const ChatMessage(
        role: 'system',
        content: 'You are ClipSync AI, a helpful assistant. '
            'Be concise, helpful, and friendly. '
            'Format responses with markdown when appropriate. '
            'When the user shares documents or images, analyze the extracted text and provide useful insights.',
      ),
      ..._messages
          .where((m) => !m.isStreaming && m.content.isNotEmpty)
          .map((m) => ChatMessage(role: m.role, content: m.forModel)),
    ];

    final responseBuffer = StringBuffer();
    try {
      // An embedded GGUF remains the privacy-first default. A configured
      // connection takes precedence, letting a user explicitly route Chat to
      // Ollama or an OpenAI-compatible provider without changing their files.
      final stream = remoteReady
          ? widget.connection.chatStream(history, temperature: _temperature)
          : widget.llm.chatStream(
              history,
              maxTokens: 1024,
              temperature: _temperature,
              topP: 0.9,
            );

      _streamSub = stream.listen(
        (token) {
          responseBuffer.write(token);
          if (mounted) {
            setState(() {
              _messages.last = _ChatBubble(
                role: 'assistant',
                content: responseBuffer.toString(),
                isStreaming: true,
              );
            });
            _scrollToBottom();
          }
        },
        onDone: () {
          if (mounted) {
            setState(() {
              _messages.last = _ChatBubble(
                role: 'assistant',
                content: responseBuffer.toString(),
                isStreaming: false,
              );
              _isGenerating = false;
            });
            _scrollToBottom();
            _persist();
          }
        },
        onError: (e) {
          debugPrint('[Chat] Stream error: $e');
          if (mounted) {
            setState(() {
              _messages.last = _ChatBubble(
                role: 'assistant',
                content: 'Something went wrong: '
                    '${e.toString().split('\n').first}',
                isStreaming: false,
              );
              _isGenerating = false;
            });
            _persist();
          }
        },
      );
    } catch (e) {
      debugPrint('[Chat] Error: $e');
      if (mounted) {
        setState(() {
          _messages.last = _ChatBubble(
            role: 'assistant',
            content: 'Could not generate a reply: '
                '${e.toString().split('\n').first}',
            isStreaming: false,
          );
          _isGenerating = false;
        });
        _persist();
      }
    }
  }

  /// Cancels the stream and keeps whatever arrived, so a long generation on a
  /// slow device is interruptible instead of something to wait out.
  void _stopGenerating() {
    _streamSub?.cancel();
    if (!mounted) return;
    setState(() {
      if (_messages.isNotEmpty && _messages.last.role == 'assistant') {
        final partial = _messages.last.content;
        _messages.last = _ChatBubble(
          role: 'assistant',
          content: partial.trim().isEmpty ? '(stopped)' : partial,
          isStreaming: false,
        );
      }
      _isGenerating = false;
    });
    _persist();
  }

  // ── Session Actions ──────────────────────────────────────────────────

  /// The conversation as plain text, for copying or saving.
  String get _transcript {
    final b = StringBuffer();
    for (final m in _messages) {
      final t = m.content.trim();
      if (t.isEmpty) continue;
      b.writeln(m.role == 'user' ? 'You:' : 'ClipSync AI:');
      b.writeln(t);
      b.writeln();
    }
    return b.toString().trimRight();
  }

  Future<void> _newChat() async {
    if (_isGenerating) return;
    if (_messages.isEmpty) {
      _toast('Already a new chat');
      return;
    }
    await _persist();
    if (!mounted) return;
    setState(() => _applySession(ChatSession.blank()));
  }

  Future<void> _openHistory() async {
    final store = _store;
    if (store == null) return;
    await _persist();
    if (!mounted) return;
    final id = await showChatHistorySheet(
      context,
      store: store,
      currentId: _session.id,
    );
    if (!mounted) return;
    if (id != null && id != _session.id) {
      final picked = store.byId(id);
      if (picked != null) {
        setState(() => _applySession(picked));
        _scrollToBottom();
        return;
      }
    }
    // The open chat may have been deleted from inside the sheet.
    if (_messages.isNotEmpty && store.byId(_session.id) == null) {
      setState(() => _applySession(ChatSession.blank()));
    }
  }

  Future<void> _renameChat() async {
    final title = await promptForChatTitle(context, initial: _session.title);
    if (title == null || !mounted) return;
    setState(() => _session.title = title);
    _persist();
  }

  Future<void> _deleteChat() async {
    final store = _store;
    if (store == null) {
      // Nothing was ever written, so there is only the view to clear.
      setState(() => _applySession(ChatSession.blank()));
      _toast('Chat cleared');
      return;
    }
    final snapshot = _session.toMap();
    final hadRow = store.byId(_session.id) != null;
    try {
      await store.remove(_session.id);
    } catch (_) {}
    if (!mounted) return;
    setState(() => _applySession(ChatSession.blank()));
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: const Text('Chat deleted'),
        behavior: SnackBarBehavior.floating,
        duration: const Duration(seconds: 4),
        action: hadRow
            ? SnackBarAction(
                label: 'Undo',
                onPressed: () async {
                  final restored = ChatSession.fromMap(snapshot);
                  await store.save(restored);
                  if (mounted) {
                    setState(() => _applySession(restored));
                    _scrollToBottom();
                  }
                },
              )
            : null,
      ),
    );
  }

  void _toast(String message) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(message),
        behavior: SnackBarBehavior.floating,
        duration: const Duration(milliseconds: 1100),
      ),
    );
  }



  // ── Voice Recording ──────────────────────────────────────────────────

  Future<void> _toggleVoiceRecording() async {
    if (_isTranscribing) return;

    if (_isRecordingVoice) {
      setState(() {
        _isRecordingVoice = false;
        _isTranscribing = true;
      });
      _ampSub?.cancel();

      final text = await _voiceInput.stopAndTranscribe();
      if (mounted) {
        setState(() => _isTranscribing = false);
        if (text != null && text.isNotEmpty) {
          _controller.text = text;
          _sendMessage();
        } else {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              content: Text('No speech detected. Try again.'),
              behavior: SnackBarBehavior.floating,
              duration: Duration(milliseconds: 1500),
            ),
          );
        }
      }
    } else {
      await _voiceInput.startRecording();
      if (mounted) {
        setState(() => _isRecordingVoice = true);
        _ampSub = _voiceInput.amplitudeStream.listen((amp) {
          if (mounted) setState(() => _amplitude = amp);
        });
      }
    }
  }

  // ── Attachments ──────────────────────────────────────────────────────

  void _showAttachmentMenu() {
    ledgerSheet<void>(
      context,
      title: 'Attach something',
      subtitle: 'It is read on this phone and added to your next message',
      children: (ctx) => [
        SheetAction(
          icon: Icons.photo_library_outlined,
          label: 'Image',
          detail: 'From the gallery. The text in it is read out.',
          onTap: () {
            Navigator.pop(ctx);
            _pickImage();
          },
        ),
        SheetAction(
          icon: Icons.description_outlined,
          label: 'Document',
          detail: 'PDF, DOCX, TXT and other text files',
          onTap: () {
            Navigator.pop(ctx);
            _pickDocument();
          },
        ),
        SheetAction(
          icon: Icons.graphic_eq_rounded,
          label: 'Recording',
          detail: 'WAV, MP3 or M4A, transcribed by Whisper',
          onTap: () {
            Navigator.pop(ctx);
            _pickAudio();
          },
        ),
        SheetAction(
          icon: Icons.content_paste_rounded,
          label: 'Clipboard',
          detail: 'Whatever you copied last',
          divided: false,
          onTap: () {
            Navigator.pop(ctx);
            _insertClipboard();
          },
        ),
      ],
    );
  }

  Future<void> _pickImage() async {
    try {
      final picker = ImagePicker();
      final image = await picker.pickImage(source: ImageSource.gallery);
      if (image == null) return;

      setState(() => _isProcessingAttachment = true);

      // Extract text via OCR
      String? extractedText;
      try {
        final recognizer = TextRecognizer(script: TextRecognitionScript.latin);
        try {
          final result = await recognizer.processImage(
            InputImage.fromFilePath(image.path),
          );
          extractedText = result.text.trim();
        } finally {
          await recognizer.close();
        }
      } catch (_) {}

      if (mounted) {
        setState(() {
          _isProcessingAttachment = false;
          _pendingAttachments.add(_Attachment(
            name: image.name,
            filePath: image.path,
            textContent: extractedText?.isNotEmpty == true ? extractedText : null,
            type: AttachmentType.image,
          ));
        });
      }
    } catch (e) {
      if (mounted) setState(() => _isProcessingAttachment = false);
    }
  }

  Future<void> _pickDocument() async {
    try {
      final result = await FilePicker.platform.pickFiles(
        type: FileType.custom,
        allowedExtensions: ['txt', 'md', 'pdf', 'doc', 'docx', 'csv', 'json', 'xml', 'html', 'log'],
      );
      if (result == null || result.files.isEmpty) return;

      final file = result.files.first;
      setState(() => _isProcessingAttachment = true);

      // Extract text from file
      String? extractedText;
      try {
        if (file.path != null) {
          final content = await File(file.path!).readAsString();
          extractedText = content.length > 5000
              ? '${content.substring(0, 5000)}\n\n… (truncated, ${content.length} chars total)'
              : content;
        }
      } catch (_) {}

      if (mounted) {
        setState(() {
          _isProcessingAttachment = false;
          _pendingAttachments.add(_Attachment(
            name: file.name,
            filePath: file.path,
            textContent: extractedText,
            type: AttachmentType.document,
          ));
        });
      }
    } catch (e) {
      if (mounted) setState(() => _isProcessingAttachment = false);
    }
  }

  Future<void> _pickAudio() async {
    try {
      final result = await FilePicker.platform.pickFiles(
        type: FileType.custom,
        allowedExtensions: ['wav', 'mp3', 'm4a', 'ogg', 'flac'],
      );
      if (result == null || result.files.isEmpty) return;

      final file = result.files.first;
      setState(() => _isProcessingAttachment = true);

      // Transcribe the audio on-device via Whisper so the LLM can analyze it.
      String? extractedText;
      if (file.path != null) {
        try {
          const whisper = Whisper(
            model: WhisperModel.tiny,
            downloadHost:
                'https://hf-mirror.com/ggerganov/whisper.cpp/resolve/main',
          );
          final transcription = await whisper.transcribe(
            transcribeRequest: TranscribeRequest(
              audio: file.path!,
              isNoTimestamps: true,
              isTranslate: false,
              language: 'en',
            ),
          ).timeout(const Duration(seconds: 90));
          final text = transcription.text.trim();
          if (text.isNotEmpty) {
            extractedText = text.length > 5000
                ? '${text.substring(0, 5000)}\n\n… (truncated, ${text.length} chars total)'
                : text;
          }
        } catch (e) {
          debugPrint('[Chat] Audio transcription skipped: $e');
        }
      }

      if (mounted) {
        setState(() {
          _isProcessingAttachment = false;
          _pendingAttachments.add(_Attachment(
            name: file.name,
            filePath: file.path,
            textContent: extractedText,
            type: AttachmentType.audio,
          ));
        });
      }
    } catch (e) {
      if (mounted) setState(() => _isProcessingAttachment = false);
    }
  }

  Future<void> _insertClipboard() async {
    try {
      final data = await Clipboard.getData(Clipboard.kTextPlain);
      if (!mounted) return;
      final text = data?.text?.trim();
      if (text == null || text.isEmpty) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Clipboard is empty'),
            behavior: SnackBarBehavior.floating,
            duration: Duration(milliseconds: 1200),
          ),
        );
        return;
      }
      setState(() {
        _pendingAttachments.add(_Attachment(
          name: 'Clipboard',
          textContent: text,
          type: AttachmentType.clipboard,
        ));
      });
    } catch (_) {}
  }

  void _removeAttachment(int index) {
    setState(() => _pendingAttachments.removeAt(index));
  }

  void _showMessageActions(int index) {
    if (index < 0 || index >= _messages.length) return;
    final msg = _messages[index];
    final content = msg.content;
    final canAsk = _canChat && !_isGenerating;
    final canSummarize = canAsk && content.trim().length >= 40;
    final canRegenerate = msg.role == 'assistant' && canAsk;
    ledgerSheet<void>(
      context,
      title: msg.role == 'user' ? 'Your message' : 'This reply',
      children: (sheetCtx) => [
        SheetAction(
          icon: Icons.content_copy_rounded,
          label: 'Copy message',
          onTap: () {
            Navigator.pop(sheetCtx);
            Clipboard.setData(ClipboardData(text: content));
            _toast('Copied');
          },
        ),
        if (canSummarize)
          SheetAction(
            icon: Icons.auto_awesome_rounded,
            label: 'Summarize this',
            detail: 'Three bullets, numbers and names kept exact',
            onTap: () {
              Navigator.pop(sheetCtx);
              _sendMessage(
                display: 'Summarize that message',
                prompt: 'Summarize the following in three short bullet '
                    'points, keeping any numbers or names exact:\n\n$content',
              );
            },
          ),
        SheetAction(
          icon: Icons.notes_rounded,
          label: 'Save to Notes',
          onTap: () {
            Navigator.pop(sheetCtx);
            _saveAsNote(content);
          },
        ),
        if (canRegenerate)
          SheetAction(
            icon: Icons.refresh_rounded,
            label: 'Ask again',
            detail: 'Throws this reply away and has another go',
            onTap: () {
              Navigator.pop(sheetCtx);
              _regenerateFrom(index);
            },
          ),
        SheetAction(
          icon: Icons.delete_outline_rounded,
          label: 'Delete message',
          danger: true,
          divided: false,
          onTap: () {
            Navigator.pop(sheetCtx);
            _deleteMessage(index);
          },
        ),
      ],
    );
  }

  /// Throws away the reply at [index] and everything after it, then re-sends the
  /// user turn above it so the model gets another attempt at the same question.
  Future<void> _regenerateFrom(int index) async {
    if (_isGenerating) return;
    var i = index;
    while (i >= 0 && _messages[i].role != 'user') {
      i--;
    }
    if (i < 0) return;
    final user = _messages[i];
    setState(() => _messages.removeRange(i, _messages.length));
    await _sendMessage(
      display: user.content,
      prompt: user.forModel,
      attachments: user.attachments,
    );
  }

  void _deleteMessage(int index) {
    if (_isGenerating || index < 0 || index >= _messages.length) return;
    setState(() => _messages.removeAt(index));
    _persist();
  }

  void _saveAsNote(String content, {String? title}) {
    try {
      final now = DateTime.now().toIso8601String();
      final note = <String, dynamic>{
        'id': 'chat_${DateTime.now().microsecondsSinceEpoch}',
        'title': title ?? _noteTitleFrom(content),
        'content': content,
        'tags': <String>['chat'],
        'isPinned': false,
        'createdAt': now,
        'updatedAt': now,
      };
      Hive.box('notes').put(note['id'], note);
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Saved to Notes'),
          backgroundColor: Semantic.success,
          behavior: SnackBarBehavior.floating,
          duration: Duration(milliseconds: 1200),
        ),
      );
    } catch (_) {}
  }

  static String _noteTitleFrom(String raw) {
    final cleaned = raw.trim().replaceAll('\n', ' ');
    final firstLine = cleaned.characters.take(40).toString().trim();
    return firstLine.isEmpty ? 'Chat message' : firstLine;
  }

  // ── Build ────────────────────────────────────────────────────────────

  @override
  Widget build(BuildContext context) {
    super.build(context);
    final scheme = Theme.of(context).colorScheme;
    final bottomPad = navBarClearance(context);

    return Column(
      children: [
        SizedBox(height: MediaQuery.of(context).padding.top + Space.sm),
        _buildHeader(context),

        // ── The transcript ──
        Expanded(
          child: _messages.isEmpty
              ? _buildEmptyState(context)
              : ListView.builder(
                  controller: _scrollController,
                  physics: const BouncingScrollPhysics(),
                  padding: const EdgeInsets.fromLTRB(
                      Space.lg, Space.md, Space.lg, Space.lg),
                  itemCount: _messages.length,
                  itemBuilder: (context, i) => _buildBubble(context, i),
                ),
        ),

        // ── The composer ──
        // Glass, because it floats over the transcript on the same layer as the
        // navigation bar directly beneath it. Everything inside it is flat.
        Padding(
          padding: EdgeInsets.fromLTRB(Space.md, 0, Space.md, bottomPad),
          child: GlassPanel(
            borderRadius: Radii.card,
            padding: const EdgeInsets.fromLTRB(Space.sm, 6, Space.sm, 6),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                if (_pendingAttachments.isNotEmpty || _isProcessingAttachment)
                  _buildAttachmentPreview(scheme),
                Row(
                  children: [
                    IconAction(
                      icon: Icons.add_rounded,
                      tooltip: 'Attach something',
                      size: 22,
                      onPressed: _isGenerating ? null : _showAttachmentMenu,
                    ),
                    if (_isTranscribing)
                      SizedBox(
                        width: 44,
                        height: 44,
                        child: Center(
                          child: SizedBox(
                            width: 18,
                            height: 18,
                            child: CircularProgressIndicator(
                              strokeWidth: 1.8,
                              color: accentOn(context, minRatio: 3),
                            ),
                          ),
                        ),
                      )
                    else
                      IconAction(
                        icon: _isRecordingVoice
                            ? Icons.stop_rounded
                            : Icons.mic_none_rounded,
                        tooltip: _isRecordingVoice ? 'Stop' : 'Speak instead',
                        size: 21,
                        tone: _isRecordingVoice
                            ? legibleAccent(Semantic.danger, scheme.surface)
                            : null,
                        onPressed: _toggleVoiceRecording,
                      ),
                    Expanded(
                      child: Stack(
                        alignment: Alignment.center,
                        children: [
                          TextField(
                            controller: _controller,
                            focusNode: _composerFocus,
                            style: AppType.body(context).copyWith(
                              color: ink(context, 0.94),
                              fontSize: 14.5,
                            ),
                            decoration: InputDecoration(
                              isDense: true,
                              filled: false,
                              hintText: _isRecordingVoice
                                  ? 'Listening'
                                  : _isTranscribing
                                      ? 'Writing that down'
                                      : _canChat
                                          ? 'Ask anything'
                                          : 'Choose a model in Settings',
                              hintStyle: AppType.body(context).copyWith(
                                fontSize: 14.5,
                                color: _isRecordingVoice
                                    ? legibleAccent(
                                        Semantic.danger, scheme.surface)
                                    : ink(context, 0.40),
                              ),
                              border: InputBorder.none,
                              enabledBorder: InputBorder.none,
                              focusedBorder: InputBorder.none,
                              contentPadding: const EdgeInsets.symmetric(
                                  horizontal: Space.sm, vertical: Space.sm + 2),
                            ),
                            maxLines: 5,
                            minLines: 1,
                            textInputAction: TextInputAction.send,
                            onSubmitted: (_) => _sendMessage(),
                            enabled: !_isGenerating && !_isTranscribing,
                          ),
                          if (_isRecordingVoice)
                            Positioned.fill(
                              child: ColoredBox(
                                color: Colors.transparent,
                                child: Center(child: _buildAmplitudeWave(scheme)),
                              ),
                            ),
                        ],
                      ),
                    ),
                    if (!_isRecordingVoice && !_isTranscribing)
                      _buildSendButton(scheme),
                  ],
                ),
                // The one setting the composer carries, on its own line under
                // the field so it never crowds the send target.
                Padding(
                  padding: const EdgeInsets.fromLTRB(Space.xs, 0, Space.xs, 2),
                  child: Row(
                    children: [
                      _buildTemperatureBadge(scheme),
                      const Spacer(),
                      if (_isGenerating)
                        Text('Generating', style: AppType.meta(context)),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }

  /// Send, or stop while a reply is streaming. This is the accent's one job in
  /// the composer, so it is a filled target rather than a tinted outline.
  Widget _buildSendButton(ColorScheme scheme) {
    if (_isGenerating) {
      final danger = legibleAccent(Semantic.danger, scheme.surface);
      return Tooltip(
        message: 'Stop generating',
        child: Pressable(
          onTap: _stopGenerating,
          scale: 0.9,
          child: SizedBox(
            width: 44,
            height: 44,
            child: Center(
              child: Container(
                width: 34,
                height: 34,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  border: Border.all(color: danger, width: 1.4),
                ),
                child: Icon(Icons.stop_rounded, size: 17, color: danger),
              ),
            ),
          ),
        ),
      );
    }
    final ready = _canChat;
    final accent = accentOn(context, minRatio: 3);
    return Tooltip(
      message: 'Send',
      child: Semantics(
        button: true,
        label: 'Send',
        child: Pressable(
          onTap: _sendMessage,
          scale: 0.9,
          child: SizedBox(
            width: 44,
            height: 44,
            child: Center(
              child: Container(
                width: 34,
                height: 34,
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color: ready ? accent : ink(context, 0.12),
                ),
                child: Icon(
                  Icons.arrow_upward_rounded,
                  size: 18,
                  color: ready ? readableOn(accent) : ink(context, 0.42),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }

  // ── Header ───────────────────────────────────────────────────────────

  /// The conversation's name, what it is, and the session controls. The same
  /// header component every other tab uses, so Chat is not the one page with a
  /// bespoke title block.
  Widget _buildHeader(BuildContext context) {
    final saved = _store != null;
    final count = _messages.where((m) => !m.isStreaming).length;
    return SolidWindowHeader(
      compact: true,
      title: _session.title,
      subtitle: count == 0
          ? (saved ? 'Saved on this phone' : 'This run only')
          : '${plural(count, 'message')}${saved ? '' : ', not saved'}',
      trailing: [
        if (saved)
          IconAction(
            icon: Icons.history_rounded,
            tooltip: 'Chat history',
            onPressed: _isGenerating ? null : _openHistory,
          ),
        IconAction(
          icon: Icons.add_comment_outlined,
          tooltip: 'New chat',
          onPressed: _isGenerating ? null : _newChat,
        ),
        IconAction(
          icon: Icons.more_horiz,
          tooltip: 'Chat options',
          onPressed: () => _chatSheet(context),
        ),
      ],
    );
  }

  /// Everything you can do to the conversation as a whole. A sheet rather than a
  /// dropdown, because every other tab in the app answers its overflow control
  /// the same way, and because a menu cannot say what "Summarize" will cost.
  Future<void> _chatSheet(BuildContext context) async {
    final hasText = _messages.any((m) => m.content.trim().isNotEmpty);
    final canAsk = _canChat && !_isGenerating;
    final saved = _store != null;
    final count = _messages.where((m) => !m.isStreaming).length;
    await ledgerSheet<void>(
      context,
      title: _session.title,
      subtitle: count == 0
          ? (saved ? 'Saved on this phone' : 'This run only')
          : '${plural(count, 'message')} · '
              '${saved ? stampLong(_session.updatedAt) : 'not saved'}',
      children: (ctx) => [
        SheetAction(
          icon: Icons.edit_outlined,
          label: 'Rename chat',
          onTap: () {
            Navigator.pop(ctx);
            _renameChat();
          },
        ),
        if (hasText && canAsk)
          SheetAction(
            icon: Icons.auto_awesome_rounded,
            label: 'Summarize conversation',
            detail: 'Adds the summary to this chat, written on this phone',
            onTap: () {
              Navigator.pop(ctx);
              _summarizeConversation();
            },
          ),
        if (hasText) ...[
          SheetAction(
            icon: Icons.content_copy_rounded,
            label: 'Copy transcript',
            onTap: () {
              Navigator.pop(ctx);
              Clipboard.setData(ClipboardData(text: _transcript));
              _toast('Transcript copied');
            },
          ),
          SheetAction(
            icon: Icons.notes_rounded,
            label: 'Save transcript to Notes',
            onTap: () {
              Navigator.pop(ctx);
              _saveAsNote(_transcript, title: _session.title);
            },
          ),
        ],
        SheetAction(
          icon: Icons.delete_outline_rounded,
          label: 'Delete chat',
          danger: true,
          divided: false,
          onTap: () {
            Navigator.pop(ctx);
            _deleteChat();
          },
        ),
      ],
    );
  }

  /// Asks the loaded model to compress the whole conversation, in the
  /// conversation itself, so the summary is part of the saved transcript.
  void _summarizeConversation() {
    final body = _transcript;
    if (body.trim().isEmpty) return;
    _sendMessage(
      display: 'Summarize this conversation',
      prompt: 'Summarize the conversation below. Give a short paragraph of '
          'what was discussed, then bullet any decisions or answers that '
          'were reached. Keep names, numbers and code exact.\n\n$body',
    );
  }

  // ── Attachment Preview ───────────────────────────────────────────────

  // ── The composer's one setting ────────────────────────────────────────

  /// Sampling temperature, said in words. "T: 0.70" is a number only the
  /// person who wrote the sampler can read; the four presets have names, and
  /// the figure stays alongside for anyone who wants it.
  static const List<double> _tempPresets = [0.2, 0.4, 0.7, 1.0];
  static const List<String> _tempNames = [
    'Precise',
    'Measured',
    'Balanced',
    'Inventive',
  ];
  static const List<String> _tempBlurbs = [
    'Replies stick closely to what you gave it',
    'Replies stay careful, with a little room to phrase things',
    'Replies balance accuracy against readability',
    'Replies take more liberties, and wander further',
  ];

  int get _tempIndex {
    final i = _tempPresets.indexWhere((t) => t == _temperature);
    return i < 0 ? 2 : i;
  }

  Widget _buildTemperatureBadge(ColorScheme scheme) {
    final i = _tempIndex;
    return Tooltip(
      message: 'Change how freely the model answers',
      child: Semantics(
        button: true,
        label: 'Reply tone, ${_tempNames[i]}',
        child: Pressable(
          onTap: _cycleTemperature,
          scale: 0.96,
          child: Padding(
            padding: const EdgeInsets.symmetric(
                horizontal: Space.sm, vertical: Space.xs + 1),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text('Tone', style: AppType.meta(context)),
                const SizedBox(width: 6),
                Text(
                  _tempNames[i],
                  style: AppType.meta(context).copyWith(
                    color: ink(context, 0.82),
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(width: 5),
                Text(
                  _temperature.toStringAsFixed(1),
                  style: AppType.meta(context)
                      .copyWith(color: ink(context, 0.34)),
                ),
                const SizedBox(width: 4),
                Icon(Icons.unfold_more_rounded,
                    size: 13, color: ink(context, 0.36)),
              ],
            ),
          ),
        ),
      ),
    );
  }

  void _cycleTemperature() {
    final next = (_tempIndex + 1) % _tempPresets.length;
    HapticFeedback.selectionClick();
    setState(() => _temperature = _tempPresets[next]);
    _toast('${_tempNames[next]}. ${_tempBlurbs[next]}');
  }

  // ── What is attached to the next turn ─────────────────────────────────

  /// The glyph for a kind of attachment. One tone for all four: colour-coding
  /// four file types puts four hues in a strip the width of a thumb, and none
  /// of them mean anything the glyph has not already said.
  static IconData _attachmentGlyph(AttachmentType t) => switch (t) {
        AttachmentType.image => Icons.image_outlined,
        AttachmentType.document => Icons.description_outlined,
        AttachmentType.audio => Icons.graphic_eq_rounded,
        AttachmentType.clipboard => Icons.content_paste_rounded,
      };

  Widget _buildAttachmentPreview(ColorScheme scheme) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(Space.xs, Space.sm, Space.xs, Space.sm),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          SizedBox(
            height: 30,
            child: ListView(
              scrollDirection: Axis.horizontal,
              physics: const ClampingScrollPhysics(),
              children: [
                if (_isProcessingAttachment)
                  Padding(
                    padding: const EdgeInsets.only(right: Space.sm),
                    child: Row(
                      children: [
                        SizedBox(
                          width: 13,
                          height: 13,
                          child: CircularProgressIndicator(
                            strokeWidth: 1.6,
                            color: accentOn(context, minRatio: 3),
                          ),
                        ),
                        const SizedBox(width: 7),
                        Text('Reading it', style: AppType.meta(context)),
                      ],
                    ),
                  ),
                for (var i = 0; i < _pendingAttachments.length; i++)
                  _attachmentTag(_pendingAttachments[i], i),
              ],
            ),
          ),
          const SizedBox(height: Space.sm),
          const Rule(strength: 0.8),
        ],
      ),
    );
  }

  /// One attached thing: its glyph, its name, and the button that drops it.
  /// A hairline outline instead of a tinted plate, so a row of them reads as a
  /// list of things and not as a row of buttons.
  Widget _attachmentTag(_Attachment att, int index) {
    final name = att.name.length > 18 ? '${att.name.substring(0, 17)}…' : att.name;
    return Padding(
      padding: const EdgeInsets.only(right: Space.sm),
      child: Container(
        padding: const EdgeInsets.only(left: Space.sm + 1, right: 3),
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(Radii.tight),
          border: Border.all(color: hairline(context, strength: 2.2)),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(_attachmentGlyph(att.type),
                size: 13, color: ink(context, 0.62)),
            const SizedBox(width: 6),
            Text(
              name,
              style: AppType.meta(context).copyWith(color: ink(context, 0.80)),
            ),
            if (att.textContent != null) ...[
              const SizedBox(width: 5),
              Icon(Icons.check_rounded,
                  size: 12, color: accentOn(context, minRatio: 3)),
            ],
            Pressable(
              onTap: () => _removeAttachment(index),
              scale: 0.86,
              child: SizedBox(
                width: 24,
                height: 24,
                child: Center(
                  child: Icon(Icons.close_rounded,
                      size: 13, color: ink(context, 0.52)),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  // ── While the mic is open ─────────────────────────────────────────────

  /// Seven bars tracking input level. Reduce Motion holds them still and lets
  /// the hint text carry the state instead.
  Widget _buildAmplitudeWave(ColorScheme scheme) {
    final tone = legibleAccent(Semantic.danger, scheme.surface);
    if (reduceMotion(context)) {
      return Text(
        'Recording',
        style: AppType.meta(context).copyWith(color: tone),
      );
    }
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: List.generate(7, (i) {
        final falloff = (i - 3).abs() * 0.15;
        final h = (7 + _amplitude * 19 * (1 - falloff)).clamp(3.0, 26.0);
        return AnimatedContainer(
          duration: Motion.instant,
          curve: Curves.easeOut,
          margin: const EdgeInsets.symmetric(horizontal: 2),
          width: 2.5,
          height: h,
          decoration: BoxDecoration(
            color: tone.withValues(alpha: 0.78),
            borderRadius: BorderRadius.circular(Radii.pill),
          ),
        );
      }),
    );
  }

  // ── Nothing said yet ──────────────────────────────────────────────────

  /// The top of an empty transcript. No 80px gradient circle: a bloom behind an
  /// outline glyph is the most reproduced decoration in generated interfaces,
  /// and it made a blank page look like a warning. When a model is loaded, the
  /// openers sit under it as a strip that runs off the edge of the page.
  Widget _buildEmptyState(BuildContext context) {
    final ready = _canChat;
    // The page margin is applied per child rather than to the list, so the
    // opener strip can start on the same left margin as the text above it and
    // still run past the right edge of the screen.
    return ListView(
      physics: const BouncingScrollPhysics(),
      padding: const EdgeInsets.only(top: Space.xl, bottom: Space.lg),
      children: [
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: Space.lg),
          child: StateBlock(
            icon: ready
                ? Icons.chat_bubble_outline_rounded
                : Icons.psychology_outlined,
            title: ready ? 'Ask it something' : 'No model loaded',
            message: ready
                ? 'It answers on this phone, so nothing leaves the device. '
                    'Attach an image, a document or a recording and it will read '
                    'that first.'
                : 'Open Settings, then the AI tab, to download a model and load '
                    'it. Everything after that happens offline.',
          ),
        ),
        if (ready) ...[
          const Padding(
            padding: EdgeInsets.symmetric(horizontal: Space.lg),
            child: SectionHeader('Openers', top: Space.xl, bottom: Space.md),
          ),
          _openerStrip(),
        ],
      ],
    );
  }

  /// The openers, as a strip that runs off the right edge. Four full sentences
  /// stacked under hairlines is the same list shape the rest of the app uses for
  /// real content, and it made a blank page look busy; a short verb per card,
  /// with the wording it writes underneath, says more in less room.
  Widget _openerStrip() {
    const openers = <(String, String, IconData)>[
      ('Summarize', 'Summarize the text below:\n\n', Icons.compress_rounded),
      ('Explain', 'Explain this like I have ten minutes:\n\n',
          Icons.lightbulb_outline_rounded),
      ('Checklist', 'Turn these notes into a checklist:\n\n',
          Icons.checklist_rounded),
      ('Proofread', 'Find the mistakes in this text:\n\n',
          Icons.spellcheck_rounded),
    ];
    // Four cards at this width are wider than the screen, so the last of them is
    // cut off by the right edge of the page. That is the whole affordance:
    // nothing has to say "scroll".
    return SizedBox(
      height: 96,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        physics: const BouncingScrollPhysics(),
        padding: const EdgeInsets.symmetric(horizontal: Space.lg),
        itemCount: openers.length,
        separatorBuilder: (_, __) => const SizedBox(width: Space.sm),
        itemBuilder: (_, i) {
          final (label, prompt, icon) = openers[i];
          return _openerCard(label: label, prompt: prompt, icon: icon);
        },
      ),
    );
  }

  Widget _openerCard({
    required String label,
    required String prompt,
    required IconData icon,
  }) {
    // Tapping fills the field and leaves the caret after the colon rather than
    // sending: "Summarize the text below" with nothing below it is a question
    // the model cannot answer, and this is the one screen where the next thing
    // the user does is paste.
    return Pressable(
      onTap: () {
        HapticFeedback.selectionClick();
        _controller.text = prompt;
        _controller.selection =
            TextSelection.collapsed(offset: prompt.length);
        _composerFocus.requestFocus();
      },
      child: Container(
        width: 132,
        padding: const EdgeInsets.all(Space.md),
        decoration: BoxDecoration(
          color: fill(context),
          borderRadius: BorderRadius.circular(Radii.inner),
          border: Border.all(color: hairline(context)),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(icon, size: 19, color: ink(context, 0.50)),
            const Spacer(),
            Text(
              label,
              style: AppType.heading(context).copyWith(fontSize: 14),
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
            ),
            const SizedBox(height: 1),
            Text(
              'then paste',
              style: AppType.meta(context).copyWith(color: ink(context, 0.42)),
            ),
          ],
        ),
      ),
    );
  }

  // ── One turn ──────────────────────────────────────────────────────────

  /// A turn in the transcript. The model's reply gets the full measure of the
  /// page under a quiet speaker label; what you said sits in one soft block
  /// opposite it. There are no avatar circles: a gradient badge beside every
  /// single line spends the page's attention on saying "assistant" forty times.
  Widget _buildBubble(BuildContext context, int index) {
    final msg = _messages[index];
    final isUser = msg.role == 'user';
    return Padding(
      padding: EdgeInsets.only(bottom: isUser ? Space.lg : Space.xl - 2),
      child: isUser ? _userTurn(context, msg, index) : _replyTurn(context, msg, index),
    );
  }

  Widget _userTurn(BuildContext context, _ChatBubble msg, int index) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.end,
      children: [
        Flexible(
          child: Pressable(
            onLongPress: () => _showMessageActions(index),
            scale: 0.99,
            child: Container(
              padding: const EdgeInsets.symmetric(
                  horizontal: Space.md + 2, vertical: Space.md - 1),
              decoration: BoxDecoration(
                color: fill(context, strength: 1.5),
                borderRadius: BorderRadius.only(
                  topLeft: const Radius.circular(Radii.inner),
                  topRight: const Radius.circular(Radii.inner),
                  bottomLeft: const Radius.circular(Radii.inner),
                  bottomRight: Radius.circular(_bubbleTail),
                ),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  if (msg.attachments.isNotEmpty)
                    _messageAttachments(context, msg),
                  Text(
                    msg.content,
                    style: AppType.body(context).copyWith(
                      fontSize: 14.5,
                      color: ink(context, 0.94),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ],
    );
  }

  Widget _replyTurn(BuildContext context, _ChatBubble msg, int index) {
    final waiting = msg.content.isEmpty && msg.isStreaming;
    return Pressable(
      onLongPress: () => _showMessageActions(index),
      scale: 0.995,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('ClipSyncAI', style: AppType.label(context)),
          const SizedBox(height: Space.sm - 2),
          if (msg.attachments.isNotEmpty) _messageAttachments(context, msg),
          if (waiting)
            // The shape of the reply that is coming, breathing, rather than a
            // bar filling up against a track it never finishes: nothing here
            // knows how long the model will take, so nothing should imply it.
            const Padding(
              padding: EdgeInsets.only(top: 2),
              child: Pulse(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    SkeletonLine(widthFactor: 0.96),
                    SizedBox(height: Space.sm),
                    SkeletonLine(widthFactor: 0.88),
                    SizedBox(height: Space.sm),
                    SkeletonLine(widthFactor: 0.42),
                  ],
                ),
              ),
            )
          else
            MarkdownBody(
              data: msg.content,
              styleSheet: ledgerMarkdown(context, size: 14.5),
              onTapLink: (text, href, title) {},
            ),
          if (!msg.isStreaming &&
              index == _messages.length - 1 &&
              msg.content.trim().isNotEmpty)
            _replyFooter(index, msg.content),
        ],
      ),
    );
  }

  /// Under the newest reply only. Long-pressing a turn opens everything, but a
  /// long press is not an affordance anybody finds, and copying the answer is
  /// what you came here to do. Older replies keep the clean measure.
  Widget _replyFooter(int index, String content) {
    final canAsk = _canChat && !_isGenerating;
    return Padding(
      padding: const EdgeInsets.only(top: Space.md),
      // Pulled left so the first control's own padding lines its label up with
      // the reply above it instead of sitting a few pixels in from it.
      child: Transform.translate(
        offset: const Offset(-10, 0),
        child: Row(
          children: [
            QuietAction(
              label: 'Copy',
              icon: Icons.content_copy_rounded,
              dense: true,
              onPressed: () {
                Clipboard.setData(ClipboardData(text: content));
                _toast('Copied');
              },
            ),
            const SizedBox(width: Space.xs),
            if (canAsk)
              QuietAction(
                label: 'Ask again',
                icon: Icons.refresh_rounded,
                dense: true,
                onPressed: () => _regenerateFrom(index),
              ),
            const Spacer(),
            IconAction(
              icon: Icons.more_horiz,
              tooltip: 'Reply options',
              size: 18,
              dense: true,
              tone: ink(context, 0.44),
              onPressed: () => _showMessageActions(index),
            ),
          ],
        ),
      ),
    );
  }

  /// What came in with a turn. One tone, one shape, sized to sit inside a line
  /// of body text rather than to be looked at.
  Widget _messageAttachments(BuildContext context, _ChatBubble msg) {
    return Padding(
      padding: const EdgeInsets.only(bottom: Space.sm),
      child: Wrap(
        spacing: Space.sm,
        runSpacing: Space.xs + 1,
        children: [
          for (final att in msg.attachments)
            Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(_attachmentGlyph(att.type),
                    size: 12.5, color: ink(context, 0.48)),
                const SizedBox(width: 5),
                Text(
                  att.name.length > 22
                      ? '${att.name.substring(0, 21)}…'
                      : att.name,
                  style: AppType.meta(context),
                ),
              ],
            ),
        ],
      ),
    );
  }
}
