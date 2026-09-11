// The layers between the stores and the screen: animation, the sensitive text
// guard, the small readouts a row prints, and the trimming an attachment gets
// before a model sees it. All of it is reachable without a window.

using System;
using System.Threading;

namespace ClipSyncAI.Tests
{
    internal static class ShellTests
    {
        public static void Run()
        {
            Animation();
            Secrets();
            Readouts();
            Peeking();
            Rows();
            Fitting();
            Languages();
        }

        private static void Animation()
        {
            T.Group("Animation");

            // The regression that put two current destinations in the rail at
            // once. A page being built for the first time can hold the message
            // loop for longer than a movement lasts, so the first frame the
            // timer delivers is already past the end. If that frame says nothing
            // changed, nobody repaints and whatever moved stays drawn where it
            // started, target reached but unseen.
            Anim late = new Anim(Motion.Instant);
            late.Set(0);
            late.To(1);
            Thread.Sleep(Motion.Instant + 60);
            T.Ok("a movement that finishes on its first frame asks to be painted", late.Advance());
            T.Near("and holds its target exactly", 1.0, late.Value, 0.0001);
            T.Ok("and asks for nothing after that", !late.Advance());
            T.Ok("and is no longer running", !late.Running);

            Anim flight = new Anim(Motion.Reveal);
            flight.Set(0);
            flight.To(1);
            T.Ok("a movement under way asks to be painted", flight.Advance());
            T.Ok("and stays inside its two ends", flight.Value >= 0 && flight.Value <= 1);
            T.Near("while remembering where it is headed", 1.0, flight.Target, 0.0001);

            Anim again = new Anim(Motion.Reveal);
            again.Set(0);
            again.To(1);
            Thread.Sleep(40);
            again.Advance();
            double reached = again.Value;
            again.To(1);
            again.Advance();
            T.Ok("asking again for the end it already has does not start it over",
                again.Value >= reached);

            Anim put = new Anim(Motion.Slow);
            put.Set(0);
            put.To(1);
            put.Set(0.5);
            T.Ok("putting a value down stops the movement", !put.Advance());
            T.Near("and it stays where it was put", 0.5, put.Value, 0.0001);

            bool was = Theme.ReduceMotion;
            try
            {
                Theme.ReduceMotion = true;
                Anim still = new Anim(Motion.Base);
                still.Set(0);
                still.To(1);
                T.Near("reduced motion arrives at once", 1.0, still.Value, 0.0001);
                T.Ok("with no frames to ask for", !still.Advance());
            }
            finally
            {
                Theme.ReduceMotion = was;
            }
        }

        private static void Secrets()
        {
            T.Group("Sensitive text");
            T.Ok("a private key block", Sensitive.Looks("-----BEGIN RSA PRIVATE KEY-----\nMIIB\n"));
            T.Ok("a named secret", Sensitive.Looks("api_key: sk-abcdef123456"));
            T.Ok("a password assignment", Sensitive.Looks("password=hunter2"));
            T.Ok("a bearer token", Sensitive.Looks(
                "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.dBjftJeZ4CVPmB92K27uhbUJU1p1r_wW1gFWFOEjXk"));
            T.Ok("a card number", Sensitive.Looks("card 4111 1111 1111 1111 expires soon"));
            T.Ok("a long order id is not a card", !Sensitive.Looks("order 4111111111111112"));
            T.Ok("ordinary prose", !Sensitive.Looks("Remember to buy milk on the way home."));
            T.Ok("nothing at all", !Sensitive.Looks(""));
            T.Ok("the Luhn check on a known good number", Sensitive.Luhn("79927398713"));
            T.Ok("and on the same number spoiled", !Sensitive.Luhn("79927398710"));
            T.Ok("and on something that is not digits", !Sensitive.Luhn("79927x98713"));
        }

        private static void Readouts()
        {
            T.Group("Readouts");
            T.Eq("a small count is itself", "0", Say.Compact(0));
            T.Eq("and so is the last one that fits", "999", Say.Compact(999));
            T.Eq("a thousand keeps a decimal", "1k", Say.Compact(1000));
            T.Eq("and shows it where it says something", "1.5k", Say.Compact(1500));
            T.Eq("past ten thousand it is dropped", "12k", Say.Compact(12345));
            T.Eq("a short line is left alone", "Hello there", Say.FirstLine("Hello there", 40));
            T.Eq("newlines and tabs become spaces", "one two three",
                Say.FirstLine("one\ttwo\r\nthree", 40));
            T.Contains("a long line is cut", Say.FirstLine(
                "the quick brown fox jumps over the lazy dog and keeps going", 20), "…");
            T.Ok("and is cut on a word", Say.FirstLine(
                "the quick brown fox jumps over the lazy dog", 20).IndexOf("bro…",
                StringComparison.Ordinal) < 0);
            T.Empty("nothing in, nothing out", Say.FirstLine("", 20));
            T.Eq("one of a thing", "1 clip", Say.Plural(1, "clip"));
            T.Eq("more than one", "2 clips", Say.Plural(2, "clip"));
            T.Eq("none of them", "0 clips", Say.Plural(0, "clip"));

            T.Ok("a line that is the title again is an echo",
                Say.Echoes("Shopping list", "Shopping list"));
            T.Ok("and so is one the title was cut out of",
                Say.Echoes("Shopping list for the…", "Shopping list for the weekend"));
            T.Ok("three dots count as the same cut",
                Say.Echoes("Shopping list for the...", "Shopping list for the weekend"));
            T.Ok("and a title that swallowed the line counts too",
                Say.Echoes("Shopping list for the weekend milk bread", "Shopping list"));
            T.Ok("a different line is not an echo",
                !Say.Echoes("Groceries", "milk bread eggs"));
            T.Ok("nothing is not an echo of anything", !Say.Echoes("", "milk"));
            T.Ok("and nothing echoes nothing", !Say.Echoes("Groceries", ""));
        }

        private static void Peeking()
        {
            T.Group("Markdown peek");
            T.Eq("a heading loses its hashes", "Shopping", Markdown.Peek("# Shopping\nmilk"));
            T.Eq("a bullet loses its dash", "milk", Markdown.Peek("- milk\n- bread"));
            T.Eq("emphasis is taken off", "really important",
                Markdown.Peek("**really** _important_"));
            T.Eq("a link keeps its words", "the release notes",
                Markdown.Peek("[the release notes](https://example.com/notes)"));
            // A clip that is nothing but code is a common clip, and the line
            // after the block is often no line at all, so the peek reaches into
            // the block rather than coming back empty. What it must never do is
            // show the fence.
            string fenced = Markdown.Peek("```\nvar x = 1;\n```\nafter the code");
            T.Eq("a code clip peeks at its first line of code", "var x = 1;", fenced);
            T.NotContains("and never at the fence", fenced, "`");
            T.Eq("blank lines are skipped", "first words", Markdown.Peek("\n\n   \nfirst words"));
            T.Empty("an empty document peeks at nothing", Markdown.Peek(""));
            T.Empty("and so does one that is only blank lines", Markdown.Peek("\n\n  \n"));

            T.Eq("the trail is everything after that line", "milk bread",
                Markdown.Trail("# Shopping\n- milk\n- bread", 100));
            T.Empty("a document of one line has no trail", Markdown.Trail("# Shopping", 100));
            T.Empty("and neither has an empty one", Markdown.Trail("", 100));
            T.Eq("blank lines between are closed up", "one two",
                Markdown.Trail("first\n\none\n\n\ntwo", 100));
            T.NotContains("a fence never reaches the trail",
                Markdown.Trail("intro\n```\nvar x = 1;\n```", 100), "`");
            T.Contains("a long trail is cut",
                Markdown.Trail("head\n" + new string('x', 40) + " " + new string('y', 40), 50), "…");
        }

        /// The two lines a feed row prints. Both row types are plain classes over
        /// a stored item, so the strings a list would draw can be read without a
        /// window anywhere near them.
        private static void Rows()
        {
            T.Group("Feed rows");

            // The row used to print the clip's first line and then the whole clip
            // flattened underneath, so the second line opened with the words that
            // were already on the first. Now it prints the first line and then
            // what follows it.
            ClipEntry c = new ClipEntry();
            c.RawText = "Standup notes for 2026-09-06\n- ship the windows build\n- read the notes";
            c.ProcessedMarkdown = ClipRegex.Process(c.RawText);
            ClipRow row = new ClipRow(c);
            T.Contains("a clip row leads with the clip's first line", row.Head, "Standup notes for");
            T.Ok("and does not repeat it underneath",
                row.Under.IndexOf("Standup notes", StringComparison.Ordinal) < 0);
            T.Eq("the line underneath is what comes next",
                "ship the windows build read the notes", row.Under);
            T.Ok("so the row asks for two lines", row.Two);

            ClipEntry one = new ClipEntry();
            one.RawText = "just the one line";
            one.ProcessedMarkdown = ClipRegex.Process(one.RawText);
            ClipRow single = new ClipRow(one);
            T.Eq("a one line clip prints that line", "just the one line", single.Head);
            T.Empty("with nothing underneath", single.Under);
            T.Ok("and stays one line tall", !single.Two);

            ClipEntry bare = new ClipEntry();
            bare.RawText = "nothing formatted this";
            ClipRow raw = new ClipRow(bare);
            T.Eq("a clip that was never formatted falls back to what was copied",
                "nothing formatted this", raw.Head);

            // The same defect in the notes feed: every note the app writes for you
            // is titled from its own first line, and the row printed that line
            // again underneath in a dimmer size.
            Note filed = new Note();
            filed.Content = "Standup notes for 2026-09-06\n- ship the windows build";
            filed.Title = Say.FirstLine(Markdown.Peek(filed.Content), 60);
            NoteRow written = new NoteRow(filed);
            T.Eq("a note titled from its first line keeps that title",
                "Standup notes for 2026-09-06", written.Title);
            T.Eq("and previews what the title does not already say",
                "ship the windows build", written.Preview);

            Note named = new Note();
            named.Title = "Groceries";
            named.Content = "milk\nbread\neggs";
            NoteRow own = new NoteRow(named);
            T.Eq("a title of its own leaves the preview whole",
                "milk bread eggs", own.Preview);

            Note untitled = new Note();
            untitled.Content = "no title on this one\nbut two lines in it";
            NoteRow blank = new NoteRow(untitled);
            T.Eq("an untitled note says so", "Untitled", blank.Title);
            T.Ok("and is not counted as named", !blank.Named);
            T.Eq("and previews the whole thing",
                "no title on this one but two lines in it", blank.Preview);
        }

        private static void Fitting()
        {
            T.Group("Attachment fitting");
            T.Eq("a short document is sent whole", "a note", Extract.Fit("a note", 100));
            T.Eq("carriage returns are normalised", "one\ntwo", Extract.Fit("one\r\ntwo", 100));
            string big = new string('x', 40) + "\n" + new string('y', 40);
            string cut = Extract.Fit(big, 50);
            T.Contains("a long one says it was cut", cut, "truncated");
            T.Contains("and says how long it really was", cut, "81 chars");
            T.Ok("and is cut on a line where it can be",
                cut.StartsWith(new string('x', 40), StringComparison.Ordinal));
            T.Ok("and the kept part is no longer than asked",
                cut.IndexOf("\n\n...", StringComparison.Ordinal) <= 50);
            T.Empty("nothing in, nothing out", Extract.Fit(null, 50));
        }

        private static void Languages()
        {
            T.Group("Speech languages");
            T.Eq("nothing named is the system's own", "Windows default", Voices.Pretty(""));
            T.Contains("a culture is spelled out", Voices.Pretty("en-GB"), "English");
            // Windows accepts any well formed tag as a locale it has never heard
            // of, so a spoiled setting comes back named after itself rather than
            // throwing. Either way the code has to survive in the words, because
            // it is what the file keeps.
            T.Contains("an unknown code survives in the words",
                Voices.Pretty("zz-ZZ"), "zz-ZZ");
            T.NotEmpty("and something is always shown", Voices.Pretty("not a culture at all"));
        }
    }
}
