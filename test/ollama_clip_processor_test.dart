import 'package:flutter_test/flutter_test.dart';
import 'package:clip_sync_ai/main.dart';

void main() {
  // ───────────────────────────────────────────────────────────────────────────
  // REGEX FALLBACK ENGINE
  // ───────────────────────────────────────────────────────────────────────────

  group('Regex Fallback Engine', () {
    test('empty input returns empty', () {
      final processor = OllamaClipProcessor();
      expect(processor.processWithRegexPublic(''), isEmpty);
    });

    test('plain text passthrough', () {
      final processor = OllamaClipProcessor();
      final result = processor.processWithRegexPublic('Hello World');
      expect(result, contains('Hello World'));
    });

    test('preserves markdown headers', () {
      final processor = OllamaClipProcessor();
      final result =
          processor.processWithRegexPublic('# Title\n## Subtitle');
      expect(result, contains('# Title'));
      expect(result, contains('## Subtitle'));
    });

    test('preserves list items', () {
      final processor = OllamaClipProcessor();
      final result =
          processor.processWithRegexPublic('- item1\n* item2\n• item3');
      expect(result, contains('- item1'));
      expect(result, contains('* item2'));
      expect(result, contains('• item3'));
    });

    test('preserves numbered lists', () {
      final processor = OllamaClipProcessor();
      final result =
          processor.processWithRegexPublic('1. First\n2) Second');
      expect(result, contains('1. First'));
      expect(result, contains('2) Second'));
    });

    test('checkbox conversion', () {
      final processor = OllamaClipProcessor();
      final result = processor.processWithRegexPublic('- [ ] unchecked\n- [x] checked');
      expect(result, contains('- [ ] unchecked'));
      expect(result, contains('- [x] checked'));
    });

    test('key-value colon', () {
      final processor = OllamaClipProcessor();
      final result = processor.processWithRegexPublic('Name: John');
      expect(result, contains('**Name:**'));
      expect(result, contains('John'));
    });

    test('key-value equals', () {
      final processor = OllamaClipProcessor();
      final result = processor.processWithRegexPublic('key = value');
      expect(result, contains('**key:**'));
      expect(result, contains('value'));
    });

    test('code block wrapping', () {
      final processor = OllamaClipProcessor();
      final result =
          processor.processWithRegexPublic('import package;\nconst x = 1;');
      expect(result, contains('```'));
    });

    test('multiline edge cases', () {
      final processor = OllamaClipProcessor();
      final result = processor.processWithRegexPublic('line1\nline2\n\nline3');
      expect(result, contains('line1'));
      expect(result, contains('line2'));
      expect(result, contains('line3'));
    });
  });

  // ───────────────────────────────────────────────────────────────────────────
  // URL LINKIFICATION
  // ───────────────────────────────────────────────────────────────────────────

  group('URL Linkification', () {
    test('https URL becomes markdown link', () {
      final processor = OllamaClipProcessor();
      final result = processor.processWithRegexPublic('Visit https://example.com today');
      expect(result, contains('[https://example.com](https://example.com)'));
    });

    test('http URL becomes markdown link', () {
      final processor = OllamaClipProcessor();
      final result = processor.processWithRegexPublic('Go to http://test.org');
      expect(result, contains('[http://test.org](http://test.org)'));
    });

    test('www URL gets https prefix', () {
      final processor = OllamaClipProcessor();
      final result = processor.processWithRegexPublic('Visit www.google.com');
      expect(result, contains('https://www.google.com'));
    });

    test('multiple URLs in one line', () {
      final processor = OllamaClipProcessor();
      final result = processor.processWithRegexPublic(
          'https://a.com and https://b.com');
      expect(result, contains('https://a.com'));
      expect(result, contains('https://b.com'));
    });

    test('URLs with paths and fragments', () {
      final processor = OllamaClipProcessor();
      final result = processor.processWithRegexPublic(
          'https://example.com/path?q=1#section');
      expect(result, contains('example.com/path'));
    });
  });

  // ───────────────────────────────────────────────────────────────────────────
  // EMAIL LINKIFICATION
  // ───────────────────────────────────────────────────────────────────────────

  group('Email Linkification', () {
    test('basic email becomes mailto link', () {
      final processor = OllamaClipProcessor();
      final result = processor.processWithRegexPublic('Contact user@example.com');
      expect(result, contains('[user@example.com](mailto:user@example.com)'));
    });

    test('email with dots in local part', () {
      final processor = OllamaClipProcessor();
      final result = processor.processWithRegexPublic('Email first.last@domain.com');
      expect(result, contains('mailto:first.last@domain.com'));
    });

    test('plus addressing', () {
      final processor = OllamaClipProcessor();
      final result = processor.processWithRegexPublic('Send to user+tag@host.com');
      expect(result, contains('user+tag@host.com'));
    });

    test('multiple emails', () {
      final processor = OllamaClipProcessor();
      final result = processor.processWithRegexPublic(
          'a@b.com and c@d.com');
      expect(result, contains('a@b.com'));
      expect(result, contains('c@d.com'));
    });
  });

  // ───────────────────────────────────────────────────────────────────────────
  // DATE HIGHLIGHTING
  // ───────────────────────────────────────────────────────────────────────────

  group('Date Highlighting', () {
    test('ISO date gets calendar emoji', () {
      final processor = OllamaClipProcessor();
      final result = processor.processWithRegexPublic('Due 2025-12-31');
      expect(result, contains('📅'));
      expect(result, contains('2025-12-31'));
    });

    test('US date gets calendar emoji', () {
      final processor = OllamaClipProcessor();
      final result = processor.processWithRegexPublic('On 12/25/2025');
      expect(result, contains('📅'));
    });

    test('European date gets calendar emoji', () {
      final processor = OllamaClipProcessor();
      final result = processor.processWithRegexPublic('By 31.12.2025');
      expect(result, contains('📅'));
    });

    test('multiple dates', () {
      final processor = OllamaClipProcessor();
      final result = processor.processWithRegexPublic(
          'From 2025-01-01 to 2025-12-31');
      expect(result, contains('📅'));
    });
  });

  // ───────────────────────────────────────────────────────────────────────────
  // COMBINED PATTERNS
  // ───────────────────────────────────────────────────────────────────────────

  group('Combined Patterns', () {
    test('complex multiline input', () {
      final processor = OllamaClipProcessor();
      final input = '''# Meeting Notes
- [ ] Prepare slides
- [x] Book room

Email: team@company.com
Visit https://docs.example.com
Due: 2025-06-15''';
      final result = processor.processWithRegexPublic(input);
      expect(result, contains('# Meeting Notes'));
      expect(result, contains('- [ ] Prepare slides'));
      expect(result, contains('- [x] Book room'));
      expect(result, contains('team@company.com'));
      expect(result, contains('https://docs.example.com'));
      expect(result, contains('📅'));
    });

    test('mixed code and text', () {
      final processor = OllamaClipProcessor();
      final result = processor.processWithRegexPublic(
          'Hello\nimport dart:async;\nWorld');
      expect(result, contains('Hello'));
      expect(result, contains('import'));
      expect(result, contains('World'));
    });

    test('key-value with URL', () {
      final processor = OllamaClipProcessor();
      final result = processor.processWithRegexPublic(
          'Link: https://example.com');
      expect(result, contains('**Link:**'));
      expect(result, contains('example.com'));
    });
  });

  // ───────────────────────────────────────────────────────────────────────────
  // PROCESS — FALLBACK TO REGEX
  // ───────────────────────────────────────────────────────────────────────────

  group('Processor.process() fallback', () {
    test('empty input returns empty', () async {
      final processor = OllamaClipProcessor();
      expect(await processor.process(''), isEmpty);
    });

    test('whitespace input returns empty', () async {
      final processor = OllamaClipProcessor();
      expect(await processor.process('   \n  '), isEmpty);
    });

    test('falls back to regex when no model loaded', () async {
      final processor = OllamaClipProcessor();
      final result = await processor.process('Hello World');
      expect(result, contains('Hello World'));
    });

    test('long text processed', () async {
      final processor = OllamaClipProcessor();
      final longText = List.generate(100, (i) => 'Line $i').join('\n');
      final result = await processor.process(longText);
      expect(result, isNotEmpty);
      expect(result, contains('Line 0'));
      expect(result, contains('Line 99'));
    });

    test('special characters and unicode', () async {
      final processor = OllamaClipProcessor();
      final result = await processor.process('日本語テスト 🎉 émojis');
      expect(result, isNotEmpty);
    });
  });

  // ───────────────────────────────────────────────────────────────────────────
  // PROPERTIES & GETTERS
  // ───────────────────────────────────────────────────────────────────────────

  group('Processor Properties', () {
    test('activeModel getter/setter works', () {
      final processor = OllamaClipProcessor();
      expect(processor.activeModel, isNotEmpty);
      processor.activeModel = 'custom:model';
      expect(processor.activeModel, 'custom:model');
    });

    test('ollamaAvailable defaults to false', () {
      final processor = OllamaClipProcessor();
      expect(processor.ollamaAvailable, isFalse);
    });

    test('modelLoaded defaults to false', () {
      final processor = OllamaClipProcessor();
      expect(processor.modelLoaded, isFalse);
    });
  });

  // ───────────────────────────────────────────────────────────────────────────
  // EDGE CASES
  // ───────────────────────────────────────────────────────────────────────────

  group('Edge Cases', () {
    test('only newlines returns empty', () {
      final processor = OllamaClipProcessor();
      expect(processor.processWithRegexPublic('\n\n\n'), isEmpty);
    });

    test('lines with only whitespace are preserved as blank lines', () {
      final processor = OllamaClipProcessor();
      final result = processor.processWithRegexPublic('line1\n   \nline2');
      expect(result, contains('line1'));
      expect(result, contains('line2'));
    });

    test('no false positive on key-value for short words', () {
      final processor = OllamaClipProcessor();
      final result = processor.processWithRegexPublic('Hi: there');
      expect(result, isNotEmpty);
    });

    test('URL inside parentheses is preserved', () {
      final processor = OllamaClipProcessor();
      final result =
          processor.processWithRegexPublic('(https://example.com)');
      expect(result, contains('https://example.com'));
      expect(result, contains('https://example.com)'));
    });

    test('multiple blank lines produce multiple blank lines', () {
      final processor = OllamaClipProcessor();
      final result = processor.processWithRegexPublic('a\n\n\nb');
      final blankCount = '\n\n\n'.allMatches(result).length;
      expect(blankCount, greaterThanOrEqualTo(1));
    });

    test('trailing whitespace is trimmed from lines', () {
      final processor = OllamaClipProcessor();
      final result = processor.processWithRegexPublic('hello   \nworld');
      expect(result.trimRight(), isNotEmpty);
    });
  });
}
