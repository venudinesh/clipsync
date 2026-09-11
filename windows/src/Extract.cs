// Reading a file into words.
//
// What can be attached to a question on a desktop and what it turns into. Text
// files are read as they are, with the encoding worked out from the bytes rather
// than assumed, because a log written by a Windows tool is very often ANSI and a
// file written by anything modern is UTF-8 with no mark at the front of it.
//
// Word documents are unpacked by this app's own zip reader. The obvious class for
// the job, ZipFile, arrived in .NET 4.5 and this build runs on Windows 7 with 4.0,
// where DeflateStream and a walk of the central directory are what there is.

using System;
using System.IO;
using System.Text;

namespace ClipSyncAI
{
    internal static partial class Extract
    {
        /// What the file dialog offers. Word first because it is the one people
        /// look for; the long list of text kinds is one entry so the dropdown
        /// does not become a menu of its own.
        public const string Filter =
            "Text and documents|*.txt;*.md;*.markdown;*.log;*.csv;*.tsv;*.json;*.xml;" +
            "*.yml;*.yaml;*.ini;*.cfg;*.conf;*.htm;*.html;*.css;*.js;*.ts;*.cs;*.py;" +
            "*.java;*.c;*.h;*.cpp;*.rs;*.go;*.rb;*.php;*.sql;*.sh;*.ps1;*.bat;*.srt;" +
            "*.vtt;*.rtf;*.docx|Word document (*.docx)|*.docx|Plain text (*.txt)|*.txt|" +
            "All files (*.*)|*.*";

        /// A file bigger than this is not read at all. Only the first few
        /// thousand characters are ever sent to the model, and pulling a
        /// gigabyte log into memory to throw all but the top of it away is a
        /// good way to take the window down with it.
        public const int Ceiling = 8 * 1024 * 1024;

        /// Reads a file as text. Throws with a sentence worth showing when it
        /// cannot, because every caller of this shows the message it gets.
        public static string Read(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new IOException("No file was chosen");
            FileInfo f = new FileInfo(path);
            if (!f.Exists) throw new FileNotFoundException("That file is not there any more");
            if (f.Length > Ceiling)
            {
                throw new IOException("That file is over " + (Ceiling / (1024 * 1024)) +
                    " MB, which is more than a question needs");
            }
            string ext = (Path.GetExtension(path) ?? "").ToLowerInvariant();
            if (ext == ".docx") return Docx(path);
            if (ext == ".rtf") return FromRtf(File.ReadAllText(path, Encoding.ASCII));
            if (ext == ".pdf")
            {
                throw new IOException("PDF text is not read yet on this build, so copy the " +
                    "part you want and attach the clipboard instead");
            }
            string text = Plain(path);
            if (text.IndexOf('\0') >= 0)
            {
                throw new IOException("That looks like a program or an image rather than text");
            }
            return text;
        }

        /// A text file, with the encoding taken from the bytes. A mark at the
        /// front settles it; without one, UTF-8 is tried strictly and the
        /// machine's own code page is the fallback, which is the right guess in
        /// that order for a file on a Windows PC.
        public static string Plain(string path)
        {
            byte[] raw = File.ReadAllBytes(path);
            if (raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF)
            {
                return new UTF8Encoding(false).GetString(raw, 3, raw.Length - 3);
            }
            if (raw.Length >= 2 && raw[0] == 0xFF && raw[1] == 0xFE)
            {
                return Encoding.Unicode.GetString(raw, 2, raw.Length - 2);
            }
            if (raw.Length >= 2 && raw[0] == 0xFE && raw[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode.GetString(raw, 2, raw.Length - 2);
            }
            try
            {
                return new UTF8Encoding(false, true).GetString(raw);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.Default.GetString(raw);
            }
        }

        /// Rich text, unpacked by the control that already knows how. Cheaper and
        /// far more correct than a reader written here, and the attach path runs
        /// on the interface thread where a control is allowed to exist.
        private static string FromRtf(string rtf)
        {
            using (System.Windows.Forms.RichTextBox box = new System.Windows.Forms.RichTextBox())
            {
                try { box.Rtf = rtf; }
                catch (ArgumentException) { throw new IOException("That rich text file is damaged"); }
                return box.Text ?? "";
            }
        }

        /// Cuts an attachment down to what is worth sending and says that it did.
        /// A model given the tail of a long document spends its context on the
        /// document instead of the question about it.
        public static string Fit(string text, int limit)
        {
            string s = (text ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Trim();
            if (s.Length <= limit) return s;
            int cut = s.LastIndexOf('\n', Math.Min(limit - 1, s.Length - 1));
            if (cut < limit / 2) cut = limit;
            return s.Substring(0, cut).TrimEnd() +
                "\n\n... (truncated, " + s.Length + " chars total)";
        }
    }
}
