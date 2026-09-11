// Markdown to RTF, block emitters and inline spans.
//
// Every visible character goes through Out, which writes the escaped RTF and
// the parallel plain string in one step. That is what keeps link offsets valid:
// the plain buffer ends up character for character identical to what the
// RichTextBox reports, so a recorded span still points at the right words.

using System;
using System.Text;

namespace ClipSyncAI
{
    internal sealed partial class MdRenderer
    {
        private static readonly string Dot = char.ConvertFromUtf32(0x2022);
        private static readonly string BoxOn = char.ConvertFromUtf32(0x2611);
        private static readonly string BoxOff = char.ConvertFromUtf32(0x2610);

        private void Out(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            _r.Text(s);
            _plain.Append(s);
        }

        private void Break()
        {
            _r.Raw("\\par ");
            _plain.Append('\n');
        }

        /// Opens a body paragraph. hangPx pulls the first line back out of the
        /// indent, which is how a list marker sits in the margin and the wrapped
        /// text lines up under itself.
        private void Pard(double leftPx, double hangPx)
        {
            _r.Raw("\\pard\\sa0\\sb0");
            int li = Rtf.Twips(leftPx);
            if (li > 0) _r.Raw("\\li" + li);
            int fi = Rtf.Twips(hangPx);
            if (fi > 0) _r.Raw("\\fi-" + fi);
            _r.Raw("\\f0\\fs" + _fsBody + "\\cf" + _cText + " ");
        }

        private void Blank()
        {
            _r.Raw("\\pard\\sa0\\sb0\\fs" + Rtf.HalfPoints(6.0 * Theme.Scale) + " ");
            Break();
        }

        private void Rule()
        {
            _r.Raw("\\pard\\sa0\\sb0\\brdrb\\brdrs\\brdrw15\\brdrcf" + _cRule +
                   "\\fs" + Rtf.HalfPoints(4.0 * Theme.Scale) + " ");
            Break();
        }

        private void Header(int level, string text)
        {
            int fs = level <= 1 ? _fsH1 : (level == 2 ? _fsH2 : _fsH3);
            _r.Raw("\\pard\\sa0\\sb0\\f0\\b\\fs" + fs + "\\cf" + _cText + " ");
            Inline(text, fs, true, false);
            _r.Raw("\\b0 ");
            Break();
        }

        private void Quote(string text)
        {
            _r.Raw("\\pard\\sa0\\sb0\\brdrl\\brdrs\\brdrw30\\brdrcf" + _cAccent + "\\brsp" +
                   Rtf.Twips(Space.Sm) + "\\li" + Rtf.Twips(Space.Md) +
                   "\\f0\\i\\fs" + _fsBody + "\\cf" + _cMuted + " ");
            Inline(text, _fsBody, false, true);
            _r.Raw("\\i0 ");
            Break();
        }

        private void Task(bool done, string text)
        {
            Pard(Space.Md, Space.Md);
            _r.Raw("{\\cf" + (done ? _cAccent : _cMuted) + " ");
            Out(done ? BoxOn : BoxOff);
            _r.Raw("}");
            Out("  ");
            Inline(text, _fsBody, false, false);
            Break();
        }

        private void Bullet(string text)
        {
            Pard(Space.Md, Space.Md);
            _r.Raw("{\\cf" + _cAccent + " ");
            Out(Dot);
            _r.Raw("}");
            Out("  ");
            Inline(text, _fsBody, false, false);
            Break();
        }

        private void Numbered(string number, string text)
        {
            Pard(Space.Lg, Space.Lg);
            _r.Raw("{\\cf" + _cAccent + " ");
            Out(number);
            _r.Raw("}");
            Out("  ");
            Inline(text, _fsBody, false, false);
            Break();
        }

        private void Paragraph(string text)
        {
            Pard(0, 0);
            Inline(text, _fsBody, false, false);
            Break();
        }

        private void CodeBlock(System.Collections.Generic.List<string> lines)
        {
            int trim = CommonIndent(lines);
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                if (trim > 0 && line.Length >= trim) line = line.Substring(trim);
                _r.Raw("\\pard\\sa0\\sb0\\li" + Rtf.Twips(Space.Md) +
                       "\\f1\\fs" + _fsCode + "\\cf" + _cText + "\\highlight" + _cCodeBg + " ");
                Out(" " + line + " ");
                _r.Raw("\\highlight0 ");
                Break();
            }
        }

        /// Fenced blocks arrive with whatever indent the surrounding list gave
        /// them. Dropping the shallowest indent keeps relative structure without
        /// pushing every line right.
        private static int CommonIndent(System.Collections.Generic.List<string> lines)
        {
            int min = int.MaxValue;
            for (int i = 0; i < lines.Count; i++)
            {
                string s = lines[i];
                if (s.Trim().Length == 0) continue;
                int n = 0;
                while (n < s.Length && s[n] == ' ') n++;
                if (n < min) min = n;
            }
            return min == int.MaxValue ? 0 : min;
        }
    }
}
