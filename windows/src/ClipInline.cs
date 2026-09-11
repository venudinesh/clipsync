// The decorations inside a line: links, addresses and dates.
//
// Three rules that rewrite text in place, each of them writing markdown that it
// can also read. That is the whole difficulty. Run over its own output, the URL
// rule matched the target inside a link it had written and nested it in another,
// the address rule wrapped its own label, and a date collected one more calendar
// on every trip. One click was enough to see it: Copy result puts the tidied
// text back on the clipboard and the monitor catches whatever lands there. Text
// that arrived with links already in it, written by hand, was mangled the same
// way on the first pass.
//
// So each rule leaves alone what is already decorated. Nothing inside a markdown
// link is touched again, and a date keeps the one calendar it has.

using System;
using System.Text;
using System.Text.RegularExpressions;

namespace ClipSyncAI
{
    internal static partial class ClipRegex
    {
        private static readonly Regex Urls =
            new Regex(@"(https?://[^ <>""']+|www\.[^ <>""']+)", Ecma | RegexOptions.IgnoreCase);
        private static readonly Regex Emails =
            new Regex(@"[\w.+-]+@[\w-]+\.[\w.-]+", Ecma);
        private static readonly Regex IsoDate = new Regex(@"\b(\d{4}-\d{2}-\d{2})\b", Ecma);
        private static readonly Regex UsDate = new Regex(@"\b(\d{1,2}/\d{1,2}/\d{4})\b", Ecma);
        private static readonly Regex EuDate = new Regex(@"\b(\d{1,2}\.\d{1,2}\.\d{4})\b", Ecma);

        /// A markdown link that is already written: a label, then a target in
        /// brackets. The target ends at the first closing bracket, so a link
        /// whose target carries brackets of its own is recognised only as far as
        /// the first one. That is enough for what this is for, which is knowing
        /// which stretches of a line to keep hands off.
        private static readonly Regex Linked =
            new Regex(@"\[[^\]\n]*\]\([^)\n]*\)", Ecma);

        /// U+1F4C5. Built from its code point rather than pasted, so the source
        /// file's encoding cannot quietly change what ships.
        private static readonly string Calendar = char.ConvertFromUtf32(0x1F4C5);

        private static string LinkifyUrls(string text)
        {
            return Gaps(text, delegate(string part)
            {
                return Urls.Replace(part, delegate(Match m)
                {
                    string url = m.Value;
                    // A bare sentence often ends right after the address, and a
                    // period, comma, semicolon or colon is punctuation of the
                    // prose, not part of it. Strip the run of trailing marks but
                    // leave something that could still be a whole address,
                    // "https://x" at the shortest.
                    int drop = 0;
                    while (drop < url.Length &&
                           (url[url.Length - 1 - drop] == '.' ||
                            url[url.Length - 1 - drop] == ',' ||
                            url[url.Length - 1 - drop] == ';' ||
                            url[url.Length - 1 - drop] == ':'))
                    {
                        drop++;
                    }
                    if (drop > 0 && url.Length - drop >= 5)
                    {
                        url = url.Substring(0, url.Length - drop);
                    }
                    // Case sensitive on purpose, matching the Dart original: an
                    // upper case scheme is rare enough that changing the
                    // behaviour here would be a difference between the two
                    // builds for no gain.
                    string href = url.StartsWith("http", StringComparison.Ordinal)
                        ? url : "https://" + url;
                    return "[" + url + "](" + href + ")";
                });
            });
        }

        private static string LinkifyEmails(string text)
        {
            return Gaps(text, delegate(string part)
            {
                return Emails.Replace(part, delegate(Match m)
                {
                    string email = m.Value;
                    return "[" + email + "](mailto:" + email + ")";
                });
            });
        }

        private static string HighlightDates(string text)
        {
            return Gaps(text, delegate(string part)
            {
                part = Stamp(IsoDate, part);
                part = Stamp(UsDate, part);
                part = Stamp(EuDate, part);
                return part;
            });
        }

        /// Runs a rule over the stretches of a line that are not already a
        /// markdown link, and copies the links themselves out untouched. The
        /// address rule runs after the URL rule, so this is also what stops an
        /// address in a query string being linkified inside the link around it.
        private static string Gaps(string text, Func<string, string> rule)
        {
            if (text.IndexOf('[') < 0) return rule(text);
            StringBuilder sb = new StringBuilder(text.Length + 32);
            int at = 0;
            for (Match m = Linked.Match(text); m.Success; m = m.NextMatch())
            {
                sb.Append(rule(text.Substring(at, m.Index - at)));
                sb.Append(m.Value);
                at = m.Index + m.Length;
            }
            sb.Append(rule(text.Substring(at)));
            return sb.ToString();
        }

        /// Puts the calendar in front of every date one pattern finds, once.
        private static string Stamp(Regex when, string text)
        {
            return when.Replace(text, delegate(Match m)
            {
                if (Already(text, m.Index)) return m.Value;
                return Calendar + " " + m.Value;
            });
        }

        /// Whether a calendar and a space already sit in front of a date. The
        /// glyph is a surrogate pair, so the look back is two characters and the
        /// space, not one and the space.
        private static bool Already(string text, int at)
        {
            int n = Calendar.Length;
            if (at < n + 1 || text[at - 1] != ' ') return false;
            return string.CompareOrdinal(text, at - 1 - n, Calendar, 0, n) == 0;
        }
    }
}
