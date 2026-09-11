// The sensitive content filter.
//
// A desktop clipboard carries far more traffic than a phone's, including things
// nobody wants written to disk. Skipping a clip is invisible data loss, though,
// so this only refuses what is unambiguous: a private key block, a JWT, a card
// number that passes Luhn, or a line that names a secret outright. Anything that
// merely looks complicated is kept.

using System;
using System.Text.RegularExpressions;

namespace ClipSyncAI
{
    internal static class Sensitive
    {
        private static readonly Regex Jwt = new Regex(
            @"^[A-Za-z0-9_\-]{8,}\.[A-Za-z0-9_\-]{8,}\.[A-Za-z0-9_\-]{8,}$",
            RegexOptions.ECMAScript | RegexOptions.Compiled);

        private static readonly Regex Named = new Regex(
            @"(?:api[_\-]?key|secret[_\-]?key|client[_\-]?secret|access[_\-]?token|password|passwd|pwd)\s*[:=]\s*\S",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex Digits = new Regex(
            @"\b(?:\d[ \-]?){13,19}\b", RegexOptions.ECMAScript | RegexOptions.Compiled);

        public static bool Looks(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            string t = text.Trim();
            if (t.IndexOf("-----BEGIN", StringComparison.Ordinal) >= 0) return true;
            if (t.IndexOf("PRIVATE KEY", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (t.Length < 400 && Jwt.IsMatch(t)) return true;
            if (Named.IsMatch(t)) return true;
            return HasCard(t);
        }

        /// A run of 13 to 19 digits that passes the Luhn check. The length and
        /// checksum together are what separate a card number from an order id.
        private static bool HasCard(string t)
        {
            foreach (Match m in Digits.Matches(t))
            {
                string only = Strip(m.Value);
                if (only.Length < 13 || only.Length > 19) continue;
                if (Luhn(only)) return true;
            }
            return false;
        }

        private static string Strip(string s)
        {
            char[] buf = new char[s.Length];
            int n = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] >= '0' && s[i] <= '9') buf[n++] = s[i];
            }
            return new string(buf, 0, n);
        }

        public static bool Luhn(string digits)
        {
            if (string.IsNullOrEmpty(digits)) return false;
            int sum = 0;
            bool twice = false;
            for (int i = digits.Length - 1; i >= 0; i--)
            {
                int d = digits[i] - '0';
                if (d < 0 || d > 9) return false;
                if (twice)
                {
                    d *= 2;
                    if (d > 9) d -= 9;
                }
                sum += d;
                twice = !twice;
            }
            return sum % 10 == 0;
        }
    }
}
