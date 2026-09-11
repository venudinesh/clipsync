// RTF assembly.
//
// A themed read only RichTextBox is what shows processed Markdown, because it is
// the one text surface in WinForms that can hold mixed weights, a monospace run
// and a coloured link without a browser control. That means the renderer's
// output language is RTF, so this file owns the escaping and the header and the
// Markdown side owns the structure.

using System;
using System.Drawing;
using System.Globalization;
using System.Text;

namespace ClipSyncAI
{
    internal sealed class Rtf
    {
        private readonly StringBuilder _sb = new StringBuilder(1024);
        private readonly StringBuilder _colors = new StringBuilder(128);
        private int _colorCount;

        /// Half points, which is the unit RTF measures type in. Pixels at the
        /// current display scale go in and RTF comes out, so the document
        /// matches the rest of the interface at 100 percent and at 200.
        public static int HalfPoints(double px)
        {
            int v = (int)Math.Round(px * 0.75 * 2.0);
            return v < 2 ? 2 : v;
        }

        /// Twips, for indents and spacing. One pixel is fifteen twips at 96 dpi.
        public static int Twips(double px)
        {
            int v = (int)Math.Round(px * 15.0);
            return v < 0 ? 0 : v;
        }

        /// Registers a colour and returns its one based table index.
        public int Color(Color c)
        {
            _colors.Append("\\red").Append(c.R)
                   .Append("\\green").Append(c.G)
                   .Append("\\blue").Append(c.B).Append(';');
            _colorCount++;
            return _colorCount;
        }

        public Rtf Raw(string control)
        {
            _sb.Append(control);
            return this;
        }

        /// Appends text with every RTF metacharacter and every non Latin-1
        /// character escaped. Emoji arrive here as surrogate pairs and each unit
        /// is written separately, which is what RTF expects.
        public Rtf Text(string s)
        {
            if (string.IsNullOrEmpty(s)) return this;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '\\': _sb.Append("\\\\"); break;
                    case '{': _sb.Append("\\{"); break;
                    case '}': _sb.Append("\\}"); break;
                    case '\t': _sb.Append("\\tab "); break;
                    case '\r': break;
                    case '\n': _sb.Append("\\line "); break;
                    default:
                        if (c >= 32 && c < 128)
                        {
                            // Printable ASCII passes through. Control characters
                            // (backspace, bell, 0x0B and 0x0C page breaks, the
                            // 0x0E..0x1F run, 0x7F) are raw bytes to RTF and would
                            // corrupt the document, so those and everything above
                            // 127 go through the surrogate escape instead.
                            _sb.Append(c);
                        }
                        else
                        {
                            // Signed 16 bit, because a value above 32767 has to
                            // be written negative for readers to accept it.
                            int v = c > 32767 ? c - 65536 : c;
                            _sb.Append("\\u").Append(v.ToString(CultureInfo.InvariantCulture)).Append('?');
                        }
                        break;
                }
            }
            return this;
        }

        /// Wraps the accumulated body in a document header. Called once, at the
        /// end, because the colour table has to list every colour the body used.
        public string Build()
        {
            StringBuilder doc = new StringBuilder(_sb.Length + 512);
            doc.Append("{\\rtf1\\ansi\\ansicpg1252\\uc1\\deff0");
            doc.Append("{\\fonttbl");
            doc.Append("{\\f0\\fswiss\\fcharset0 ").Append(Theme.Sans).Append(";}");
            doc.Append("{\\f1\\fmodern\\fcharset0 ").Append(Theme.Mono).Append(";}");
            doc.Append('}');
            doc.Append("{\\colortbl ;").Append(_colors).Append('}');
            doc.Append("\\viewkind4\\pard");
            doc.Append(_sb);
            doc.Append('}');
            return doc.ToString();
        }

        public bool IsEmpty { get { return _sb.Length == 0; } }
    }
}
