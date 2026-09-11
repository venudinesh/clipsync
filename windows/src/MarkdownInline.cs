// Markdown inline spans.
//
// Bold, italic, strikethrough, code and links, each emitted as an RTF group so
// the reader restores the surrounding character formatting at the closing brace
// and nesting needs no bookkeeping. An unclosed delimiter is left on the page as
// typed rather than swallowing the rest of the line.

using System;
using System.Text;

namespace ClipSyncAI
{
    internal sealed partial class MdRenderer
    {
        private void Inline(string s, int fs, bool bold, bool italic)
        {
            if (string.IsNullOrEmpty(s)) return;
            StringBuilder lit = new StringBuilder(s.Length);
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];

                if (c == '\\' && i + 1 < s.Length && IsMark(s[i + 1]))
                {
                    lit.Append(s[i + 1]);
                    i += 2;
                    continue;
                }

                if (c == '`')
                {
                    int end = s.IndexOf('`', i + 1);
                    if (end > i)
                    {
                        Flush(lit);
                        CodeSpan(s.Substring(i + 1, end - i - 1));
                        i = end + 1;
                        continue;
                    }
                }
                else if (c == '[')
                {
                    string text, url;
                    int next;
                    if (ParseLink(s, i, out text, out url, out next))
                    {
                        Flush(lit);
                        Anchor(text, url, fs, bold, italic);
                        i = next;
                        continue;
                    }
                }
                else if ((c == '*' || c == '_') && i + 1 < s.Length && s[i + 1] == c)
                {
                    // Runs first: ***x*** is bold and italic together. Feeding it
                    // to the double branch leaves the third mark as literal
                    // text, which is how the page ended up reading as *x* x.
                    if (i + 2 < s.Length && s[i + 2] == c)
                    {
                        int trip = s.IndexOf(new string(c, 3), i + 3, StringComparison.Ordinal);
                        if (trip > i + 2)
                        {
                            Flush(lit);
                            Wrap("\\b\\i ", s.Substring(i + 3, trip - i - 3), fs, true, true);
                            i = trip + 3;
                            continue;
                        }
                    }
                    int end = s.IndexOf(new string(c, 2), i + 2, StringComparison.Ordinal);
                    if (end > i + 1)
                    {
                        Flush(lit);
                        Wrap("\\b ", s.Substring(i + 2, end - i - 2), fs, true, italic);
                        i = end + 2;
                        continue;
                    }
                }
                else if (c == '*' || (c == '_' && WordEdge(s, i - 1)))
                {
                    int end = s.IndexOf(c, i + 1);
                    if (end > i + 1 && (c == '*' || WordEdge(s, end + 1)))
                    {
                        Flush(lit);
                        Wrap("\\i ", s.Substring(i + 1, end - i - 1), fs, bold, true);
                        i = end + 1;
                        continue;
                    }
                }
                else if (c == '~' && i + 1 < s.Length && s[i + 1] == '~')
                {
                    int end = s.IndexOf("~~", i + 2, StringComparison.Ordinal);
                    if (end > i + 1)
                    {
                        Flush(lit);
                        Wrap("\\strike ", s.Substring(i + 2, end - i - 2), fs, bold, italic);
                        i = end + 2;
                        continue;
                    }
                }

                lit.Append(c);
                i++;
            }
            Flush(lit);
        }

        private void Flush(StringBuilder lit)
        {
            if (lit.Length == 0) return;
            Out(lit.ToString());
            lit.Length = 0;
        }

        private void Wrap(string control, string inner, int fs, bool bold, bool italic)
        {
            _r.Raw("{" + control);
            Inline(inner, fs, bold, italic);
            _r.Raw("}");
        }

        private void CodeSpan(string code)
        {
            _r.Raw("{\\f1\\fs" + _fsCode + "\\highlight" + _cCodeBg + " ");
            Out(code);
            _r.Raw("}");
        }

        /// Records where the link landed in the plain text so the view can hit
        /// test a click without re-parsing the Markdown.
        private void Anchor(string text, string url, int fs, bool bold, bool italic)
        {
            int start = _plain.Length;
            _r.Raw("{\\cf" + _cLink + "\\ul ");
            Inline(text.Length == 0 ? url : text, fs, bold, italic);
            _r.Raw("}");
            LinkSpan span = new LinkSpan();
            span.Start = start;
            span.Length = _plain.Length - start;
            span.Url = url;
            _links.Add(span);
        }

        /// The target runs to the last closing paren before whitespace, not the
        /// first. The processor's URL rule deliberately swallows a trailing
        /// paren, so [https://x.com)](https://x.com)) has to resolve as one link
        /// rather than a link plus a stray bracket.
        private static bool ParseLink(string s, int i, out string text, out string url, out int next)
        {
            text = "";
            url = "";
            next = i;
            int close = s.IndexOf(']', i + 1);
            if (close < 0 || close + 1 >= s.Length || s[close + 1] != '(') return false;
            int p = close + 2;
            int last = -1;
            while (p < s.Length && s[p] != ' ' && s[p] != '\t')
            {
                if (s[p] == ')') last = p;
                p++;
            }
            if (last < 0) return false;
            text = s.Substring(i + 1, close - i - 1);
            url = s.Substring(close + 2, last - close - 2);
            next = last + 1;
            return url.Length > 0;
        }

        private static bool WordEdge(string s, int i)
        {
            if (i < 0 || i >= s.Length) return true;
            char c = s[i];
            return !char.IsLetterOrDigit(c);
        }

        private static bool IsMark(char c)
        {
            return c == '*' || c == '_' || c == '`' || c == '[' || c == ']' ||
                   c == '(' || c == ')' || c == '#' || c == '\\' || c == '~' ||
                   c == '-' || c == '>' || c == '|' || c == '.';
        }
    }
}
