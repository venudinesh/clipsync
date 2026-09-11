// The clipboard text processor.
//
// A line by line port of OllamaClipProcessor from lib/main.dart. The regex
// engine is the one part of the pipeline that always runs: it needs no model,
// no server and no network, so every AI action in the app still produces
// something useful on a machine with nothing installed. When a local engine is
// present it takes the first attempt and this remains the fallback.
//
// Character class semantics are pinned to ECMAScript, which is what Dart's
// RegExp uses. Without that, .NET would widen \w and \d to every script in
// Unicode and a line of Japanese would start matching the key and value rule
// that the phone build leaves alone.
//
// Tidying has to survive being done twice. Copy result puts the tidied text
// back on the clipboard, the monitor catches whatever lands there, and it
// arrives here a second time, so text that is already tidy has to come out
// unchanged. The inline rules in ClipInline.cs leave what they have already
// written alone; the fence test below is the same promise for whole lines.

using System;
using System.Text;
using System.Text.RegularExpressions;

namespace ClipSyncAI
{
    internal static partial class ClipRegex
    {
        private const RegexOptions Ecma = RegexOptions.ECMAScript;

        private static readonly Regex Checkbox =
            new Regex(@"^[\s]*[-*]\s*\[([ xX])\]\s*(.*)$", Ecma);
        private static readonly Regex Bullet =
            new Regex(@"^[\s]*[-*\u2022]\s+", Ecma);
        private static readonly Regex Numbered =
            new Regex(@"^[\s]*\d+[.)]\s+", Ecma);
        private static readonly Regex Header =
            new Regex(@"^#{1,6}\s", Ecma);
        private static readonly Regex KeyValue =
            new Regex(@"^([\w\s]+?)\s*[:=]\s*(.+)$", Ecma);
        private static readonly Regex CodeLike =
            new Regex(@"^[\s]*(import |const |var |let |function |class |def |fn |pub )", Ecma);

        public static string Process(string rawText)
        {
            if (rawText == null || rawText.Trim().Length == 0) return "";

            StringBuilder buffer = new StringBuilder(rawText.Length + 64);
            string[] lines = rawText.Split('\n');
            bool fenced = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string raw = lines[i].TrimEnd();
                string line = raw;

                // A fence, and everything between one and the next, is copied
                // out as it came in. Code is not prose: a key and a value
                // inside a block is a line of source and not something to
                // bold, and a block this engine wrote itself must not collect
                // a second pair of fences the next time the text comes past.
                // The fence check reads a trimmed line, but inside the block
                // the original line goes back out: trailing whitespace before
                // a newline can carry meaning in source, and trimming it would
                // quietly edit the quoted code.
                if (Fence(line))
                {
                    fenced = !fenced;
                    buffer.Append(line).Append('\n');
                    continue;
                }
                if (fenced)
                {
                    buffer.Append(raw).Append('\n');
                    continue;
                }

                if (line.Length == 0)
                {
                    buffer.Append('\n');
                    continue;
                }

                line = LinkifyUrls(line);
                line = LinkifyEmails(line);
                line = HighlightDates(line);

                Match cb = Checkbox.Match(line);
                if (cb.Success)
                {
                    bool checked_ = cb.Groups[1].Value != " ";
                    string text = cb.Groups[2].Value;
                    buffer.Append("- [").Append(checked_ ? "x" : " ").Append("] ").Append(text).Append('\n');
                    continue;
                }

                if (Bullet.IsMatch(line) || Numbered.IsMatch(line) || Header.IsMatch(line))
                {
                    buffer.Append(line).Append('\n');
                    continue;
                }

                Match kv = KeyValue.Match(line);
                if (kv.Success)
                {
                    string key = kv.Groups[1].Value.Trim();
                    string value = kv.Groups[2].Value.Trim();
                    buffer.Append("**").Append(key).Append(":** ").Append(value).Append('\n');
                    continue;
                }

                if (CodeLike.IsMatch(line))
                {
                    buffer.Append("```\n").Append(line).Append("\n```\n");
                    continue;
                }

                buffer.Append(line).Append('\n');
            }

            return buffer.ToString().Trim();
        }

        /// The line that opens or closes a code block, in either of the two
        /// spellings markdown allows for one.
        private static bool Fence(string line)
        {
            string t = line.TrimStart();
            return t.StartsWith("```", StringComparison.Ordinal) ||
                   t.StartsWith("~~~", StringComparison.Ordinal);
        }

        /// True when a clip already reads as a checklist, which is what decides
        /// whether the Clips row offers a checklist affordance.
        public static bool LooksLikeChecklist(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            string[] lines = text.Split('\n');
            int hits = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (Checkbox.IsMatch(lines[i].TrimEnd())) hits++;
            }
            return hits > 0;
        }
    }
}
