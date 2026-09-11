// The test harness.
//
// A console runner rather than a framework, for the same reason the app has no
// dependencies: it has to build with the compiler that is already on the
// machine. Failures print the expectation and the actual value and set a non
// zero exit code, which is all a build script needs.

using System;
using System.Collections.Generic;
using System.Text;

namespace ClipSyncAI.Tests
{
    internal static class T
    {
        private static int _pass;
        private static int _fail;
        private static string _group = "";
        private static readonly List<string> Failures = new List<string>();

        public static void Group(string name)
        {
            _group = name;
            Console.WriteLine();
            Console.WriteLine("  " + name);
        }

        public static void Ok(string what, bool condition)
        {
            if (condition)
            {
                _pass++;
                Console.WriteLine("    pass  " + what);
            }
            else
            {
                _fail++;
                Failures.Add(_group + " / " + what);
                Console.WriteLine("    FAIL  " + what);
            }
        }

        public static void Eq(string what, string expected, string actual)
        {
            bool same = string.Equals(expected, actual, StringComparison.Ordinal);
            Ok(what, same);
            if (!same)
            {
                Console.WriteLine("          expected: " + Show(expected));
                Console.WriteLine("          actual:   " + Show(actual));
            }
        }

        public static void Eq(string what, int expected, int actual)
        {
            bool same = expected == actual;
            Ok(what, same);
            if (!same) Console.WriteLine("          expected " + expected + ", got " + actual);
        }

        public static void Eq(string what, bool expected, bool actual)
        {
            bool same = expected == actual;
            Ok(what, same);
            if (!same) Console.WriteLine("          expected " + expected + ", got " + actual);
        }

        public static void Near(string what, double expected, double actual, double tolerance)
        {
            bool same = Math.Abs(expected - actual) <= tolerance;
            Ok(what, same);
            if (!same) Console.WriteLine("          expected " + expected + " +/- " + tolerance + ", got " + actual);
        }

        public static void Contains(string what, string haystack, string needle)
        {
            bool has = haystack != null && haystack.IndexOf(needle, StringComparison.Ordinal) >= 0;
            Ok(what, has);
            if (!has) Console.WriteLine("          in: " + Show(haystack));
        }

        public static void NotContains(string what, string haystack, string needle)
        {
            bool has = haystack != null && haystack.IndexOf(needle, StringComparison.Ordinal) >= 0;
            Ok(what, !has);
            if (has) Console.WriteLine("          in: " + Show(haystack));
        }

        public static void NotEmpty(string what, string value)
        {
            Ok(what, !string.IsNullOrEmpty(value));
        }

        public static void Empty(string what, string value)
        {
            bool empty = string.IsNullOrEmpty(value);
            Ok(what, empty);
            if (!empty) Console.WriteLine("          got: " + Show(value));
        }

        /// A colour as eight hex digits, alpha first. Painting is asserted by
        /// reading pixels back, and a failure that prints two Color structs says
        /// far less than one that prints two numbers.
        public static string Hex(System.Drawing.Color c)
        {
            return c.A.ToString("x2") + c.R.ToString("x2") +
                c.G.ToString("x2") + c.B.ToString("x2");
        }

        /// Control characters are shown as escapes, because a diff that hinges
        /// on a trailing newline is unreadable otherwise.
        private static string Show(string s)
        {
            if (s == null) return "null";
            StringBuilder sb = new StringBuilder(s.Length + 8);
            sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\n') sb.Append("\\n");
                else if (c == '\r') sb.Append("\\r");
                else if (c == '\t') sb.Append("\\t");
                else if (c < ' ') sb.Append("\\x").Append(((int)c).ToString("x2"));
                else sb.Append(c);
            }
            sb.Append('"');
            return sb.ToString();
        }

        public static int Report()
        {
            Console.WriteLine();
            Console.WriteLine("  " + _pass + " passed, " + _fail + " failed");
            if (_fail > 0)
            {
                Console.WriteLine();
                for (int i = 0; i < Failures.Count; i++) Console.WriteLine("  failed: " + Failures[i]);
            }
            return _fail == 0 ? 0 : 1;
        }
    }
}
