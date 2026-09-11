// The Markdown to RTF renderer.
//
// Two things matter here beyond "it does not throw": the RTF has to be
// structurally sound, because a RichTextBox given a malformed document shows
// nothing at all, and the plain text has to stay in step with it, because that
// is what link offsets are measured against.

using System;
using System.Collections.Generic;

namespace ClipSyncAI.Tests
{
    internal static class MarkdownTests
    {
        private static readonly string Dot = char.ConvertFromUtf32(0x2022);
        private static readonly string BoxOn = char.ConvertFromUtf32(0x2611);
        private static readonly string BoxOff = char.ConvertFromUtf32(0x2610);

        public static void Run()
        {
            Structure();
            PlainText();
            Links();
            Escaping();
        }

        /// Counts braces the way an RTF reader does: a backslash escapes the
        /// character after it, so \{ is text and not a group.
        private static int BraceBalance(string rtf)
        {
            int depth = 0;
            int worst = 0;
            for (int i = 0; i < rtf.Length; i++)
            {
                char c = rtf[i];
                if (c == '\\') { i++; continue; }
                if (c == '{') depth++;
                else if (c == '}') { depth--; if (depth < worst) worst = depth; }
            }
            return depth != 0 ? depth : worst;
        }

        private static void Structure()
        {
            T.Group("RTF structure");
            string[] samples = {
                "# Heading\n\nBody **bold** and *italic* and `code`.",
                "- one\n- two\n1. first\n2. second",
                "- [ ] open\n- [x] done",
                "> quoted line",
                "---",
                "```\nvar x = 1;\n```",
                "a { b } c \\ d",
                "",
                "no trailing newline"
            };
            for (int i = 0; i < samples.Length; i++)
            {
                MdDoc d = Markdown.Render(samples[i]);
                T.Ok("sample " + i + " opens with an RTF header",
                    d.Rtf.StartsWith("{\\rtf1", StringComparison.Ordinal));
                T.Eq("sample " + i + " has balanced groups", 0, BraceBalance(d.Rtf));
                T.Ok("sample " + i + " declares both fonts",
                    d.Rtf.IndexOf("\\f0\\fswiss", StringComparison.Ordinal) >= 0 &&
                    d.Rtf.IndexOf("\\f1\\fmodern", StringComparison.Ordinal) >= 0);
            }

            T.Contains("bold becomes a bold run", Markdown.Render("**x**").Rtf, "{\\b ");
            T.Contains("italic becomes an italic run", Markdown.Render("*x*").Rtf, "{\\i ");
            T.Contains("strikethrough becomes a struck run", Markdown.Render("~~x~~").Rtf, "{\\strike ");
            T.Contains("a code span switches to the mono font", Markdown.Render("`x`").Rtf, "{\\f1");
            T.Contains("a rule draws a bottom border", Markdown.Render("---").Rtf, "\\brdrb");
            T.Contains("a quote draws a left border", Markdown.Render("> x").Rtf, "\\brdrl");
            T.Contains("a bullet hangs its marker", Markdown.Render("- x").Rtf, "\\fi-");
            T.Ok("an empty document still renders", Markdown.Render("").Rtf.Length > 40);
            T.Eq("null renders as empty text", "", Markdown.Plain(null));
        }

        private static void PlainText()
        {
            T.Group("Markdown plain text");
            T.Eq("bold markers are dropped", "bold", Markdown.Plain("**bold**"));
            T.Eq("italic markers are dropped", "italic", Markdown.Plain("*italic*"));
            T.Eq("underscore italic is dropped", "italic", Markdown.Plain("_italic_"));
            T.Eq("code ticks are dropped", "code", Markdown.Plain("`code`"));
            T.Eq("a header keeps only its text", "Heading", Markdown.Plain("# Heading"));
            T.Eq("a deep header keeps only its text", "Six", Markdown.Plain("###### Six"));
            T.Eq("seven hashes is not a header", "####### Seven", Markdown.Plain("####### Seven"));
            T.Eq("a hash without a space is not a header", "#tag", Markdown.Plain("#tag"));

            T.Contains("a bullet gets a real bullet glyph", Markdown.Plain("- one"), Dot);
            T.Contains("a bullet keeps its text", Markdown.Plain("- one"), "one");
            T.Contains("a star bullet works too", Markdown.Plain("* one"), Dot);
            T.Contains("a numbered item keeps its number", Markdown.Plain("1. first"), "1.");
            T.Contains("a done task gets a ticked box", Markdown.Plain("- [x] done"), BoxOn);
            T.Contains("an upper case mark also ticks", Markdown.Plain("- [X] done"), BoxOn);
            T.Contains("an open task gets an empty box", Markdown.Plain("- [ ] open"), BoxOff);
            T.NotContains("a task drops its brackets", Markdown.Plain("- [x] done"), "[x]");

            T.Eq("a rule leaves no characters", "", Markdown.Plain("---"));
            T.Contains("a quote keeps its text", Markdown.Plain("> quoted"), "quoted");
            T.NotContains("a quote drops its marker", Markdown.Plain("> quoted"), ">");

            string code = Markdown.Plain("```\nvar x = **1**;\n```");
            T.Contains("fenced code is verbatim", code, "var x = **1**;");

            T.Contains("an unclosed marker is shown as typed", Markdown.Plain("**bold"), "**bold");
            T.Contains("snake case is not italic", Markdown.Plain("foo_bar_baz"), "foo_bar_baz");
            T.Eq("two paragraphs keep their gap", "one\n\ntwo", Markdown.Plain("one\n\ntwo\n"));
        }

        private static void Links()
        {
            T.Group("Markdown links");
            MdDoc d = Markdown.Render("see [docs](https://x.com) now");
            T.Eq("one link is recorded", 1, d.Links.Count);
            T.Eq("the target is kept", "https://x.com", d.Links[0].Url);
            T.Eq("the span points at the link text", "docs",
                d.Plain.Substring(d.Links[0].Start, d.Links[0].Length));
            T.Contains("the visible text is the label, not the target", d.Plain, "see docs now");
            T.NotContains("the target is not shown", d.Plain, "https://x.com");
            T.Contains("a link is coloured and underlined", d.Rtf, "\\ul ");

            // What the processor actually produces for a bare URL: label and
            // target are the same string.
            MdDoc bare = Markdown.Render(ClipRegex.Process("go to https://example.com today"));
            T.Eq("a linkified URL yields one link", 1, bare.Links.Count);
            T.Eq("its target is the URL", "https://example.com", bare.Links[0].Url);
            T.Contains("its label is the URL", bare.Plain, "https://example.com");

            MdDoc paren = Markdown.Render("[https://example.com)](https://example.com))");
            T.Eq("the swallowed paren stays one link", 1, paren.Links.Count);
            T.Eq("and the paren is part of the target", "https://example.com)", paren.Links[0].Url);
            T.NotContains("no stray bracket is left behind", paren.Plain, "))");

            MdDoc mail = Markdown.Render(ClipRegex.Process("write to a@b.com"));
            T.Eq("an email becomes a mailto link", "mailto:a@b.com", mail.Links[0].Url);

            MdDoc two = Markdown.Render("[a](u1) and [b](u2)");
            T.Eq("two links are recorded", 2, two.Links.Count);
            T.Eq("the second span is right", "b",
                two.Plain.Substring(two.Links[1].Start, two.Links[1].Length));

            MdDoc inList = Markdown.Render("- see [docs](https://x.com)");
            T.Eq("a link inside a bullet is recorded", 1, inList.Links.Count);
            T.Eq("its offset accounts for the marker", "docs",
                inList.Plain.Substring(inList.Links[0].Start, inList.Links[0].Length));

            T.Eq("a bracket with no target is not a link", 0,
                Markdown.Render("[not a link] plain").Links.Count);
            T.Eq("an empty target is not a link", 0, Markdown.Render("[x]()").Links.Count);
        }

        private static void Escaping()
        {
            T.Group("RTF escaping");
            MdDoc d = Markdown.Render("a { b } c \\ d");
            T.Contains("an open brace is escaped", d.Rtf, "\\{");
            T.Contains("a close brace is escaped", d.Rtf, "\\}");
            T.Contains("a backslash is escaped", d.Rtf, "\\\\");
            T.Eq("and the plain text is unchanged", "a { b } c \\ d\n", d.Plain);

            MdDoc jp = Markdown.Render("日本語");
            T.Contains("a non Latin character becomes a unicode escape", jp.Rtf, "\\u26085?");
            T.Eq("and survives in the plain text", "日本語\n", jp.Plain);

            // U+1F4C5 is above the basic plane, so it arrives as a surrogate
            // pair and each half has to be written as a signed 16 bit value.
            MdDoc emoji = Markdown.Render(char.ConvertFromUtf32(0x1F4C5) + " 2026-09-05");
            T.Contains("the high surrogate is written negative", emoji.Rtf, "\\u-10179?");
            T.Contains("the low surrogate is written negative", emoji.Rtf, "\\u-9019?");
            T.Contains("the date beside it is intact", emoji.Plain, "2026-09-05");

            T.Contains("a tab becomes a tab control word", Markdown.Render("a\tb").Rtf, "\\tab ");
            T.Eq("trailing paragraph breaks are trimmed", "x", Markdown.Plain("x"));
            T.Eq("but the render keeps them for offsets", "x\n", Markdown.Render("x").Plain);

            List<string> long_ = new List<string>();
            for (int i = 0; i < 400; i++) long_.Add("line " + i);
            string big = string.Join("\n", long_.ToArray());
            MdDoc many = Markdown.Render(big);
            T.Eq("a long document stays balanced", 0, BraceBalance(many.Rtf));
            T.Contains("and keeps its last line", many.Plain, "line 399");
        }
    }
}
