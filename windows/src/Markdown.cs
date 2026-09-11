// Markdown to RTF, block level.
//
// The subset the app actually produces and stores: headers, bullet and numbered
// lists, task boxes, fenced and indented code, block quotes, rules, and the
// inline spans handled in MarkdownInline.cs. Anything unrecognised is shown as
// written, which is the right failure for a clipboard tool: never hide text.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ClipSyncAI
{
    internal sealed class LinkSpan
    {
        public int Start;
        public int Length;
        public string Url;
    }

    internal sealed class MdDoc
    {
        public string Rtf = "";
        public string Plain = "";
        public List<LinkSpan> Links = new List<LinkSpan>();
    }

    internal sealed partial class MdRenderer
    {
        private readonly Rtf _r = new Rtf();
        private readonly StringBuilder _plain = new StringBuilder(512);
        private readonly List<LinkSpan> _links = new List<LinkSpan>();

        private int _cText, _cMuted, _cLink, _cCodeBg, _cRule, _cAccent;
        private int _fsBody, _fsCode, _fsH1, _fsH2, _fsH3;

        public static MdDoc Render(string markdown)
        {
            MdRenderer m = new MdRenderer();
            return m.Run(markdown ?? "");
        }

        private MdDoc Run(string markdown)
        {
            _cText = _r.Color(Theme.OnSurface);
            _cMuted = _r.Color(Theme.Muted);
            _cLink = _r.Color(Theme.AccentText);
            _cCodeBg = _r.Color(Theme.High);
            _cRule = _r.Color(Theme.Hairline);
            _cAccent = _r.Color(Theme.AccentText);
            double k = Theme.Scale;
            _fsBody = Rtf.HalfPoints(12.5 * k);
            _fsCode = Rtf.HalfPoints(11.5 * k);
            _fsH1 = Rtf.HalfPoints(18.0 * k);
            _fsH2 = Rtf.HalfPoints(15.0 * k);
            _fsH3 = Rtf.HalfPoints(13.0 * k);

            string[] lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            bool inFence = false;
            List<string> fence = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.TrimStart();

                if (trimmed.StartsWith("```", StringComparison.Ordinal) ||
                    trimmed.StartsWith("~~~", StringComparison.Ordinal))
                {
                    if (inFence) { CodeBlock(fence); fence.Clear(); inFence = false; }
                    else inFence = true;
                    continue;
                }
                if (inFence) { fence.Add(line); continue; }

                if (trimmed.Length == 0) { Blank(); continue; }

                if (IsRule(trimmed)) { Rule(); continue; }

                int hashes = HeaderLevel(trimmed);
                if (hashes > 0) { Header(hashes, trimmed.Substring(hashes).TrimStart()); continue; }

                if (trimmed.StartsWith("> ", StringComparison.Ordinal) || trimmed == ">")
                {
                    Quote(trimmed.Length > 1 ? trimmed.Substring(2) : "");
                    continue;
                }

                string task;
                bool done;
                if (TaskItem(trimmed, out done, out task)) { Task(done, task); continue; }

                string bullet;
                if (BulletItem(trimmed, out bullet)) { Bullet(bullet); continue; }

                string number, rest;
                if (NumberItem(trimmed, out number, out rest)) { Numbered(number, rest); continue; }

                Paragraph(trimmed);
            }
            if (inFence && fence.Count > 0) CodeBlock(fence);

            MdDoc doc = new MdDoc();
            doc.Rtf = _r.Build();
            doc.Plain = _plain.ToString();
            doc.Links = _links;
            return doc;
        }

        private static bool IsRule(string s)
        {
            if (s.Length < 3) return false;
            char c = s[0];
            if (c != '-' && c != '*' && c != '_') return false;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] != c && s[i] != ' ') return false;
            }
            return true;
        }

        private static int HeaderLevel(string s)
        {
            int n = 0;
            while (n < s.Length && s[n] == '#') n++;
            if (n == 0 || n > 6) return 0;
            return n < s.Length && (s[n] == ' ' || s[n] == '\t') ? n : 0;
        }

        private static bool TaskItem(string s, out bool done, out string text)
        {
            done = false;
            text = "";
            if (s.Length < 5) return false;
            if (s[0] != '-' && s[0] != '*') return false;
            int i = 1;
            while (i < s.Length && s[i] == ' ') i++;
            if (i + 2 >= s.Length || s[i] != '[' || s[i + 2] != ']') return false;
            char mark = s[i + 1];
            if (mark != ' ' && mark != 'x' && mark != 'X') return false;
            done = mark != ' ';
            text = s.Substring(i + 3).TrimStart();
            return true;
        }

        private static bool BulletItem(string s, out string text)
        {
            text = "";
            if (s.Length < 2) return false;
            char c = s[0];
            if (c != '-' && c != '*' && c != '\u2022') return false;
            if (s[1] != ' ' && s[1] != '\t') return false;
            text = s.Substring(2).TrimStart();
            return true;
        }

        private static bool NumberItem(string s, out string number, out string text)
        {
            number = "";
            text = "";
            int i = 0;
            while (i < s.Length && s[i] >= '0' && s[i] <= '9') i++;
            if (i == 0 || i > 3 || i + 1 >= s.Length) return false;
            if (s[i] != '.' && s[i] != ')') return false;
            if (s[i + 1] != ' ') return false;
            number = s.Substring(0, i) + s[i];
            text = s.Substring(i + 2).TrimStart();
            return true;
        }
    }

    internal static class Markdown
    {
        public static MdDoc Render(string markdown) { return MdRenderer.Render(markdown); }

        /// The text without its markup, for previews and for copying a clip as
        /// plain text. The document's own trailing paragraph breaks go: they are
        /// rendering, not content. MdDoc.Plain keeps them, because that is what
        /// link offsets are measured against.
        public static string Plain(string markdown)
        {
            return MdRenderer.Render(markdown).Plain.TrimEnd('\n');
        }

        private static readonly Regex Fence = new Regex(@"^\s*(```|~~~)", RegexOptions.Compiled);
        private static readonly Regex Lead = new Regex(
            @"^\s*(?:>\s*)*(?:#{1,6}\s+|[-*+]\s+\[[ xX]\]\s+|[-*+]\s+|\d+[.)]\s+)?",
            RegexOptions.Compiled);
        private static readonly Regex Emphasis = new Regex(@"(\*\*|__|\*|_|`)", RegexOptions.Compiled);
        private static readonly Regex LinkText = new Regex(@"!?\[([^\]]*)\]\([^)]*\)", RegexOptions.Compiled);
        private static readonly Regex Blank = new Regex(@"\s+", RegexOptions.Compiled);

        /// The first line of a document with its markup taken off, for a list row
        /// that has room for one line. This does not go through the renderer:
        /// building RTF for every row of a five hundred clip feed to read one line
        /// off the front of it is work nobody sees.
        public static string Peek(string markdown)
        {
            if (string.IsNullOrEmpty(markdown)) return "";
            string[] lines = Rows(markdown);
            for (int i = 0; i < lines.Length; i++)
            {
                string s = Bare(lines[i]);
                if (s.Length > 0) return s;
            }
            return "";
        }

        /// Everything after the line Peek returns, markup off and run together on
        /// one line of its own.
        ///
        /// A row prints Peek above Trail, so the pair reads as the start of the
        /// document rather than the same words twice: the second line is always
        /// what comes next, never what the first line already said. A document of
        /// one line has no trail, which is what keeps a one line clip one row tall.
        public static string Trail(string markdown, int max)
        {
            if (string.IsNullOrEmpty(markdown)) return "";
            string[] lines = Rows(markdown);
            StringBuilder sb = new StringBuilder();
            bool first = true;
            for (int i = 0; i < lines.Length && sb.Length < max; i++)
            {
                string s = Bare(lines[i]);
                if (s.Length == 0) continue;
                if (first) { first = false; continue; }
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(s);
            }
            return Say.FirstLine(sb.ToString(), max);
        }

        private static string[] Rows(string markdown)
        {
            return markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }

        /// One line with its markup taken off, or nothing at all when the line
        /// was blank or a fence. Both readers above skip on nothing, which is how
        /// a clip that is only a code block still peeks at its code.
        private static string Bare(string line)
        {
            if (Fence.IsMatch(line)) return "";
            string s = Lead.Replace(line, "", 1);
            s = LinkText.Replace(s, "$1");
            s = Emphasis.Replace(s, "");
            return Blank.Replace(s, " ").Trim();
        }
    }
}
