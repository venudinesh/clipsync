// Reading a Word document.
//
// A .docx is a zip with an XML document inside it, so it is opened by the small
// zip reader in this app rather than by ZipFile, which arrived in .NET 4.5 and is
// not there on the Windows 7 floor this build has to start on.
//
// The XML is walked once rather than run through a parser: what is wanted here is
// the words, in order, with paragraph and cell breaks kept. Loading a five
// megabyte document into a DOM to throw the structure away costs more than the
// scan and can throw on the malformed files that turn up in real folders.

using System;
using System.IO;
using System.Text;

namespace ClipSyncAI
{
    internal static partial class Extract
    {
        private static string Docx(string path)
        {
            string xml;
            try
            {
                using (Zip z = new Zip(path))
                {
                    xml = Body(z);
                }
            }
            catch (IOException) { throw; }
            catch (Exception ex)
            {
                Paths.Log("docx read", ex);
                throw new IOException("That .docx could not be opened. It may be damaged, " +
                    "or open in Word with a lock on it");
            }
            if (xml == null) throw new IOException("There is no document inside that .docx");
            string text = Words(xml);
            if (text.Trim().Length == 0) throw new IOException("That document has no text in it");
            return text;
        }

        /// The body of the document. Word puts it at a fixed place; the sweep
        /// afterwards is for the files written by everything else that claims to
        /// export .docx and puts it somewhere else.
        private static string Body(Zip z)
        {
            string got = z.Text("word/document.xml");
            if (got != null) return got;
            string other = z.Ending("/document.xml");
            return other == null ? null : z.Text(other);
        }

        /// WordprocessingML, flattened. Paragraphs and table rows end a line,
        /// cells are separated by a tab, and the two kinds of run that are not
        /// really text are skipped: field instructions, which hold the machinery
        /// behind a link rather than its words, and deleted text left behind by
        /// tracked changes.
        private static string Words(string xml)
        {
            StringBuilder sb = new StringBuilder(Math.Max(64, xml.Length / 3));
            bool skip = false;
            int i = 0;
            while (i < xml.Length)
            {
                char c = xml[i];
                if (c != '<')
                {
                    if (!skip) sb.Append(c);
                    i++;
                    continue;
                }
                int end = xml.IndexOf('>', i + 1);
                if (end < 0) break;
                string tag = xml.Substring(i + 1, end - i - 1);
                i = end + 1;
                bool closing = tag.Length > 0 && tag[0] == '/';
                bool empty = tag.Length > 0 && tag[tag.Length - 1] == '/';
                string name = Name(tag);
                if (name == "w:instrtext" || name == "w:deltext")
                {
                    if (!empty) skip = !closing;
                    continue;
                }
                if (skip) continue;
                if (name == "w:tab") sb.Append('\t');
                else if (name == "w:br" || name == "w:cr") sb.Append('\n');
                else if (closing && (name == "w:p" || name == "w:tr")) sb.Append('\n');
                else if (closing && name == "w:tc") sb.Append('\t');
            }
            return Unescape(sb.ToString());
        }

        private static string Name(string tag)
        {
            int a = tag.Length > 0 && tag[0] == '/' ? 1 : 0;
            int b = a;
            while (b < tag.Length)
            {
                char c = tag[b];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == '/') break;
                b++;
            }
            return b > a ? tag.Substring(a, b - a).ToLowerInvariant() : "";
        }

        /// The five named entities and both numeric forms. A smart quote written
        /// as a number would otherwise reach the model as its escape.
        private static string Unescape(string s)
        {
            if (s.IndexOf('&') < 0) return s;
            StringBuilder sb = new StringBuilder(s.Length);
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (c != '&') { sb.Append(c); i++; continue; }
                int end = s.IndexOf(';', i + 1);
                if (end < 0 || end - i > 10) { sb.Append(c); i++; continue; }
                string body = s.Substring(i + 1, end - i - 1);
                string got = Entity(body);
                if (got == null) { sb.Append(c); i++; continue; }
                sb.Append(got);
                i = end + 1;
            }
            return sb.ToString();
        }

        private static string Entity(string body)
        {
            if (body == "amp") return "&";
            if (body == "lt") return "<";
            if (body == "gt") return ">";
            if (body == "quot") return "\"";
            if (body == "apos") return "'";
            if (body.Length < 2 || body[0] != '#') return null;
            bool hex = body[1] == 'x' || body[1] == 'X';
            string digits = body.Substring(hex ? 2 : 1);
            if (digits.Length == 0) return null;
            int code;
            try
            {
                code = Convert.ToInt32(digits, hex ? 16 : 10);
            }
            catch (Exception) { return null; }
            if (code <= 0 || code > 0x10FFFF) return null;
            try { return char.ConvertFromUtf32(code); }
            catch (ArgumentException) { return null; }
        }
    }
}
