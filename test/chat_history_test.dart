import 'package:flutter_test/flutter_test.dart';

import 'package:clip_sync_ai/features/chat_history.dart';

/// The chat-history model layer is what stands between a corrupt Hive row and a
/// crash on launch, so the round trip and the repair paths are tested directly
/// rather than through the sheet that renders them.
void main() {
  group('ChatSession.titleFrom', () {
    test('collapses whitespace and keeps a short line intact', () {
      expect(ChatSession.titleFrom('  Summarise   this\nnote '),
          'Summarise this note');
    });

    test('clips a long line and marks it as clipped', () {
      final title = ChatSession.titleFrom('a' * 80);
      expect(title.length, lessThanOrEqualTo(43));
      expect(title.endsWith('…'), isTrue);
    });

    test('falls back rather than returning an empty title', () {
      expect(ChatSession.titleFrom('   \n\t '), 'New chat');
    });
  });

  group('ChatSession', () {
    ChatSession sessionWith(List<StoredMessage> messages) => ChatSession(
          id: 'chat_1',
          title: 'Test chat',
          messages: messages,
          createdAt: DateTime(2026, 3, 4, 9),
          updatedAt: DateTime(2026, 3, 4, 10),
        );

    test('blank() starts empty and titled', () {
      final s = ChatSession.blank();
      expect(s.isEmpty, isTrue);
      expect(s.title, 'New chat');
      expect(s.messages, isEmpty);
    });

    test('preview uses the first non-empty line, whitespace collapsed', () {
      final s = sessionWith(const [
        StoredMessage(role: 'user', content: '   '),
        StoredMessage(role: 'assistant', content: 'first\n\nreal   line'),
      ]);
      expect(s.preview, 'first real line');
    });

    test('preview says so when there is nothing to show', () {
      expect(sessionWith(const []).preview, 'Empty conversation');
    });

    test('transcript labels both speakers and skips empty turns', () {
      final s = sessionWith(const [
        StoredMessage(role: 'user', content: 'Hello'),
        StoredMessage(role: 'assistant', content: ''),
        StoredMessage(role: 'assistant', content: 'Hi there'),
      ]);
      expect(s.transcript, 'You:\nHello\n\nClipSync AI:\nHi there');
    });

    test('survives a toMap / fromMap round trip, prompt included', () {
      final original = sessionWith(const [
        StoredMessage(
          role: 'user',
          content: 'Read this',
          prompt: 'Read this\n\n[document contents]',
          attachments: [StoredAttachment(name: 'spec.pdf', type: 'document')],
        ),
        StoredMessage(role: 'assistant', content: 'Done'),
      ])
        ..isPinned = true;

      final restored = ChatSession.fromMap(original.toMap());

      expect(restored.id, original.id);
      expect(restored.title, original.title);
      expect(restored.isPinned, isTrue);
      expect(restored.createdAt, original.createdAt);
      expect(restored.updatedAt, original.updatedAt);
      expect(restored.messages.length, 2);
      expect(restored.messages.first.prompt,
          'Read this\n\n[document contents]');
      expect(restored.messages.first.attachments.single.name, 'spec.pdf');
      expect(restored.messages.last.role, 'assistant');
    });
  });

  group('ChatSession.fromMap repair', () {
    test('a row with nothing in it still yields a usable session', () {
      final s = ChatSession.fromMap(<String, dynamic>{});
      expect(s.id, isNotEmpty);
      expect(s.title, 'Untitled chat');
      expect(s.messages, isEmpty);
      expect(s.isPinned, isFalse);
    });

    test('a blank title is replaced rather than shown', () {
      expect(ChatSession.fromMap({'title': '   '}).title, 'Untitled chat');
    });

    test('unparseable dates fall back instead of throwing', () {
      final s = ChatSession.fromMap({
        'createdAt': 'not a date',
        'updatedAt': 'also not a date',
      });
      expect(s.createdAt, isA<DateTime>());
      expect(s.updatedAt, isA<DateTime>());
    });

    test('messages that are not a list are dropped, not fatal', () {
      expect(ChatSession.fromMap({'messages': 'oops'}).messages, isEmpty);
    });

    test('non-map entries inside messages are skipped', () {
      final s = ChatSession.fromMap({
        'messages': [
          'garbage',
          {'role': 'user', 'content': 'kept'},
          42,
        ],
      });
      expect(s.messages.length, 1);
      expect(s.messages.single.content, 'kept');
    });

    test('an unknown role is normalised to assistant', () {
      final m = StoredMessage.fromMap({'role': 'system', 'content': 'x'});
      expect(m.role, 'assistant');
    });

    test('an attachment missing its fields gets defaults', () {
      final a = StoredAttachment.fromMap(<String, dynamic>{});
      expect(a.name, 'attachment');
      expect(a.type, 'document');
    });
  });

  group('relativeTime', () {
    final now = DateTime.now();

    test('reads as just now under a minute', () {
      expect(relativeTime(now.subtract(const Duration(seconds: 20))),
          'just now');
    });

    test('minutes, hours and days each get their own unit', () {
      expect(relativeTime(now.subtract(const Duration(minutes: 12))), '12m ago');
      expect(relativeTime(now.subtract(const Duration(hours: 5))), '5h ago');
      expect(relativeTime(now.subtract(const Duration(days: 3))), '3d ago');
    });

    test('past a week it switches to a date', () {
      final old = DateTime(2026, 3, 4);
      expect(relativeTime(old), 'Mar 4');
    });
  });
}
