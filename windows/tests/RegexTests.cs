// The regex engine spec, ported from test/ollama_clip_processor_test.dart.
//
// Same assertions as the phone build's suite, so a divergence between the two
// implementations shows up here rather than in someone's clipboard.

using System;
using System.Collections.Generic;
using System.Text;

namespace ClipSyncAI.Tests
{
    internal static class RegexTests
    {
        private static readonly string Cal = char.ConvertFromUtf32(0x1F4C5);

        private static string P(string s) { return ClipRegex.Process(s); }

        public static void Run()
        {
            Fallback();
            Urls();
            Emails();
            Dates();
            Combined();
            Edges();
        }

        private static void Fallback()
        {
            T.Group("Regex fallback engine");
            T.Empty("empty input returns empty", P(""));
            T.Contains("plain text passthrough", P("Hello World"), "Hello World");

            string h = P("# Title\n## Subtitle");
            T.Contains("preserves h1", h, "# Title");
            T.Contains("preserves h2", h, "## Subtitle");

            string l = P("- item1\n* item2\n" + char.ConvertFromUtf32(0x2022) + " item3");
            T.Contains("preserves dash item", l, "- item1");
            T.Contains("preserves star item", l, "* item2");
            T.Contains("preserves bullet item", l, char.ConvertFromUtf32(0x2022) + " item3");

            string n = P("1. First\n2) Second");
            T.Contains("preserves 1.", n, "1. First");
            T.Contains("preserves 2)", n, "2) Second");

            string c = P("- [ ] unchecked\n- [x] checked");
            T.Contains("unchecked box", c, "- [ ] unchecked");
            T.Contains("checked box", c, "- [x] checked");
            T.Contains("upper case X normalises", P("- [X] done"), "- [x] done");

            string kv = P("Name: John");
            T.Contains("key-value colon key", kv, "**Name:**");
            T.Contains("key-value colon value", kv, "John");

            string kve = P("key = value");
            T.Contains("key-value equals key", kve, "**key:**");
            T.Contains("key-value equals value", kve, "value");

            T.Contains("code block wrapping", P("import package;\nconst x = 1;"), "```");

            string m = P("line1\nline2\n\nline3");
            T.Contains("multiline line1", m, "line1");
            T.Contains("multiline line2", m, "line2");
            T.Contains("multiline line3", m, "line3");
        }

        private static void Urls()
        {
            T.Group("URL linkification");
            T.Contains("https becomes a markdown link",
                P("Visit https://example.com today"),
                "[https://example.com](https://example.com)");
            T.Contains("http becomes a markdown link",
                P("Go to http://test.org"),
                "[http://test.org](http://test.org)");
            T.Contains("www gets an https prefix",
                P("Visit www.google.com"), "https://www.google.com");

            string two = P("https://a.com and https://b.com");
            T.Contains("first of two URLs", two, "https://a.com");
            T.Contains("second of two URLs", two, "https://b.com");

            T.Contains("path and fragment survive",
                P("https://example.com/path?q=1#section"), "example.com/path");
        }

        private static void Emails()
        {
            T.Group("Email linkification");
            T.Contains("basic email becomes mailto",
                P("Contact user@example.com"),
                "[user@example.com](mailto:user@example.com)");
            T.Contains("dots in the local part",
                P("Email first.last@domain.com"), "mailto:first.last@domain.com");
            T.Contains("plus addressing",
                P("Send to user+tag@host.com"), "user+tag@host.com");

            string two = P("a@b.com and c@d.com");
            T.Contains("first of two emails", two, "a@b.com");
            T.Contains("second of two emails", two, "c@d.com");
        }

        private static void Dates()
        {
            T.Group("Date highlighting");
            string iso = P("Due 2025-12-31");
            T.Contains("ISO date gets the calendar", iso, Cal);
            T.Contains("ISO date text survives", iso, "2025-12-31");
            T.Contains("US date gets the calendar", P("On 12/25/2025"), Cal);
            T.Contains("European date gets the calendar", P("By 31.12.2025"), Cal);
            T.Contains("two dates on one line", P("From 2025-01-01 to 2025-12-31"), Cal);
        }

        private static void Combined()
        {
            T.Group("Combined patterns");
            string input =
                "# Meeting Notes\n" +
                "- [ ] Prepare slides\n" +
                "- [x] Book room\n" +
                "\n" +
                "Email: team@company.com\n" +
                "Visit https://docs.example.com\n" +
                "Due: 2025-06-15";
            string r = P(input);
            T.Contains("header survives", r, "# Meeting Notes");
            T.Contains("unchecked task survives", r, "- [ ] Prepare slides");
            T.Contains("checked task survives", r, "- [x] Book room");
            T.Contains("email survives", r, "team@company.com");
            T.Contains("url survives", r, "https://docs.example.com");
            T.Contains("date marker present", r, Cal);

            string mixed = P("Hello\nimport dart:async;\nWorld");
            T.Contains("text before code", mixed, "Hello");
            T.Contains("code line", mixed, "import");
            T.Contains("text after code", mixed, "World");

            string kvUrl = P("Link: https://example.com");
            T.Contains("key-value with a URL keeps the key", kvUrl, "**Link:**");
            T.Contains("key-value with a URL keeps the host", kvUrl, "example.com");
        }

        private static void Edges()
        {
            T.Group("Regex engine edge cases");
            T.Empty("only newlines returns empty", P("\n\n\n"));
            T.Empty("only whitespace returns empty", P("   \n  "));

            string ws = P("line1\n   \nline2");
            T.Contains("whitespace line keeps line1", ws, "line1");
            T.Contains("whitespace line keeps line2", ws, "line2");
            T.Contains("whitespace line becomes blank", ws, "line1\n\nline2");

            T.NotEmpty("short key does not break", P("Hi: there"));

            string paren = P("(https://example.com)");
            T.Contains("parenthesised URL kept", paren, "https://example.com");
            T.Contains("closing paren is part of the match", paren, "https://example.com)");

            T.Contains("blank runs are preserved", P("a\n\n\nb"), "a\n\n\nb");
            T.Eq("trailing whitespace is trimmed per line", "hello\nworld", P("hello   \nworld"));

            T.NotEmpty("unicode survives", P("日本語テスト 🎉 émojis"));
            T.Contains("unicode is unchanged", P("日本語テスト"), "日本語テスト");

            // A line of Japanese must not be read as a key and a value. Dart's
            // \w is ASCII only and the port pins .NET to the same rule; without
            // it this line would come back bolded.
            T.NotContains("non ASCII text is not a key-value", P("日本語 テスト"), "**");

            StringBuilder big = new StringBuilder();
            for (int i = 0; i < 100; i++)
            {
                if (i > 0) big.Append('\n');
                big.Append("Line ").Append(i);
            }
            string longResult = P(big.ToString());
            T.Contains("long input keeps the first line", longResult, "Line 0");
            T.Contains("long input keeps the last line", longResult, "Line 99");

            T.Eq("checklist detection", true, ClipRegex.LooksLikeChecklist("- [ ] a\n- [x] b"));
            T.Eq("checklist detection is not fooled by a list", false, ClipRegex.LooksLikeChecklist("- a\n- b"));
        }
    }
}
