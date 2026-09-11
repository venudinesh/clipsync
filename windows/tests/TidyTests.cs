// Tidying the same text twice.
//
// Copy result puts the tidied version back on the clipboard, the monitor catches
// whatever lands there, and the formatter sees it again. So a second pass has to
// change nothing. It did: the URL rule nested the link it had written inside
// another, the address rule wrapped its own label, a date collected a calendar
// per trip, and a fenced block grew another pair of fences. Text with markdown
// already in it, written by hand, was mangled the same way on the first pass.
//
// Everything here is the same shape of assertion: tidy once, tidy the result,
// and the two have to read alike.

using System;

namespace ClipSyncAI.Tests
{
    internal static class TidyTests
    {
        private static readonly string Cal = char.ConvertFromUtf32(0x1F4C5);

        private static string P(string s) { return ClipRegex.Process(s); }

        public static void Run()
        {
            Round();
            Already();
        }

        /// Tidies once, then tidies that, and says so when they differ.
        private static void Twice(string what, string raw)
        {
            string once = P(raw);
            T.Eq(what + " reads the same tidied twice", once, P(once));
        }

        private static int Count(string text, string what)
        {
            int n = 0;
            for (int at = text.IndexOf(what, StringComparison.Ordinal); at >= 0;
                 at = text.IndexOf(what, at + what.Length, StringComparison.Ordinal)) n++;
            return n;
        }

        private static void Round()
        {
            T.Group("Tidying twice");

            // The clip that showed it: one line with a link, an address and a
            // date, copied out of a clip row and straight back in.
            string clip = "Standup notes for 2026-09-06\n" +
                "ship the windows build read https://example.com/notes " +
                "mail tyson@example.com about the rehash";
            string once = P(clip);
            T.Eq("a clip copied out and caught again reads the same", once, P(once));
            T.Eq("and again on the trip after that", once, P(P(P(clip))));
            T.Contains("its link is written once", once,
                "[https://example.com/notes](https://example.com/notes)");
            T.NotContains("and never nested in a second one", P(once),
                "notes](https://example.com/notes](");
            T.Contains("its address is written once", once,
                "[tyson@example.com](mailto:tyson@example.com)");
            T.Eq("one calendar, however many times it is tidied", 1, Count(P(once), Cal));

            Twice("a header", "# Title");
            Twice("a checklist", "- [ ] pack\n- [x] ship");
            Twice("a checklist with a link in it", "- [ ] read https://example.com/notes");
            Twice("a key and a value", "Name: John");
            Twice("a bare URL", "see https://example.com/a?q=1#top");
            Twice("a www URL", "see www.example.com");
            Twice("an address", "write to first.last+tag@example.co.uk");
            Twice("the three date shapes", "2025-12-31 and 12/25/2025 and 31.12.2025");
            Twice("a line of code", "const x = 1;");
            Twice("two lines of code", "import package;\nlet y = 2;");
            Twice("code among prose", "Hello\nimport dart:async;\nWorld");
            Twice("a fenced block", "```\nconst x = 1;\n```");
            Twice("a parenthesised URL", "(https://example.com)");
            Twice("a blank run", "a\n\n\nb");
            Twice("everything at once", clip);
        }

        /// Markdown the text arrived with. None of it is this engine's to
        /// rewrite, and a fresh URL beside it still gets its link.
        private static void Already()
        {
            T.Group("Markdown that is already there");

            string hand = "see [the docs](https://example.com/docs)";
            T.Eq("a link written by hand is left alone", hand, P(hand));

            string mail = "[me](mailto:me@example.com)";
            T.Eq("and one whose label is an address", mail, P(mail));

            string both = "[docs](https://a.com) and https://b.com";
            T.Contains("a URL outside a link is still linkified", P(both),
                "[https://b.com](https://b.com)");
            T.Contains("while the hand written one is untouched", P(both), "[docs](https://a.com)");

            string dated = "[log](https://example.com/2025-12-31)";
            T.Eq("a date inside a link keeps its shape", dated, P(dated));
            T.NotContains("no calendar is pushed into a link", P(dated), Cal);

            T.NotContains("an address in a query string is not linkified inside the link",
                P("https://example.com/?to=a@b.com"), "mailto:");

            T.Contains("a fenced block keeps its own text", P("```\nName: John\n```"), "Name: John");
            T.NotContains("and nothing in it is bolded", P("```\nName: John\n```"), "**");
            T.Eq("one pair of fences, not two", 2, Count(P("```\nconst x = 1;\n```"), "```"));
        }
    }
}
