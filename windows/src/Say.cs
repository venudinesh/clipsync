// Words shared by every view.
//
// The phone build keeps these in one place because five screens print "1 note"
// and "4 notes", and each screen getting its own ternary is how the copy drifts
// apart. The desktop build keeps the same wording, so the two read as one
// product rather than two ports.

using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ClipSyncAI
{
    internal static class Say
    {
        private static readonly CultureInfo Local = CultureInfo.CurrentCulture;

        /// The margin stamp: short enough to sit in a list row.
        public static string Stamp(DateTime t)
        {
            TimeSpan d = DateTime.Now - t;
            if (d.TotalMinutes < 1) return "now";
            if (d.TotalMinutes < 60) return ((int)d.TotalMinutes) + "m";
            if (d.TotalHours < 24) return ((int)d.TotalHours) + "h";
            if (d.TotalDays < 7) return ((int)d.TotalDays) + "d";
            return t.ToString("MMM d", Local);
        }

        /// The same moment written out, for a sheet or a header with room for it.
        public static string StampLong(DateTime t)
        {
            DateTime now = DateTime.Now;
            bool sameDay = t.Year == now.Year && t.Month == now.Month && t.Day == now.Day;
            if (sameDay) return "Today at " + t.ToString("h:mm tt", Local);
            return t.ToString("MMM d, h:mm tt", Local);
        }

        public static string Plural(int n, string one)
        {
            return Plural(n, one, one + "s");
        }

        public static string Plural(int n, string one, string many)
        {
            return n + " " + (n == 1 ? one : many);
        }

        /// What goes between two facts on one line. A middle dot, spaced, which
        /// is the separator the phone build uses; it is built from its code point
        /// so this file stays plain ASCII on disk.
        public static readonly string Sep = " " + char.ConvertFromUtf32(0x00B7) + " ";

        /// Facts joined by that separator, skipping the empty ones so a row does
        /// not print a dangling dot when a clip has nothing to say in a slot.
        public static string Join(params string[] parts)
        {
            if (parts == null) return "";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i])) continue;
                if (sb.Length > 0) sb.Append(Sep);
                sb.Append(parts[i]);
            }
            return sb.ToString();
        }

        private static readonly Regex Gap = new Regex(@"\s+", RegexOptions.Compiled);

        /// Words in a blob of text. One definition, because "240 words" printed
        /// by two screens that count differently is worse than either alone.
        public static int Words(string text)
        {
            if (text == null) return 0;
            string t = text.Trim();
            if (t.Length == 0) return 0;
            return Gap.Split(t).Length;
        }

        public static bool SameDay(DateTime a, DateTime b)
        {
            return a.Year == b.Year && a.Month == b.Month && a.Day == b.Day;
        }

        /// A big number shortened for a readout, where the width is fixed and the
        /// last two digits of a word count were never the point.
        public static string Compact(int n)
        {
            if (n < 1000) return n.ToString(Local);
            double k = n / 1000.0;
            return k.ToString(n < 10000 ? "0.#" : "0", Local) + "k";
        }

        /// One line of a longer text, for a row that has room for a line.
        public static string FirstLine(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string t = text.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
            t = Gap.Replace(t, " ").Trim();
            if (t.Length <= max) return t;
            int cut = t.LastIndexOf(' ', Math.Min(max, t.Length - 1));
            if (cut < max / 2) cut = max;
            // Cutting on the low half of a surrogate pair leaves a lone mark; a
            // cut word around an emoji is still a valid string.
            if (cut > 0 && char.IsHighSurrogate(t[cut - 1]) && cut < t.Length)
            {
                cut++;
            }
            else if (cut > 1 && char.IsLowSurrogate(t[cut - 1]))
            {
                cut--;
            }
            return t.Substring(0, cut).TrimEnd() + char.ConvertFromUtf32(0x2026);
        }

        /// True when a line would only repeat a title the row already prints.
        ///
        /// A title taken from a body is that body's first line cut short, so this
        /// is a prefix test rather than an equality: to a reader, "Standup notes
        /// for the…" and "Standup notes for the sixth" are the same line. Either
        /// direction counts, because notes written before titles were cut on a
        /// line boundary are still on disk with the whole body flattened into the
        /// title, and those rows should stop repeating themselves too.
        public static bool Echoes(string title, string line)
        {
            string a = Loose(title);
            string b = Loose(line);
            if (a.Length == 0 || b.Length == 0) return false;
            return b.StartsWith(a, StringComparison.CurrentCultureIgnoreCase) ||
                   a.StartsWith(b, StringComparison.CurrentCultureIgnoreCase);
        }

        /// A line without the marks that exist only for the layout: the ellipsis
        /// a cut leaves behind, whether it was written as one character or as
        /// three dots, and every run of space.
        private static string Loose(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            string t = s.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
            t = Gap.Replace(t, " ").Trim();
            while (t.EndsWith("...", StringComparison.Ordinal)) t = t.Substring(0, t.Length - 3);
            return t.TrimEnd((char)0x2026, ' ');
        }
    }
}
