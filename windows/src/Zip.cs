// Reading one file out of a zip.
//
// A .docx is a zip with an XML file inside it, and the class that would open one
// in a line, ZipFile, arrived in .NET 4.5. This build starts on Windows 7 with
// 4.0, so the central directory is walked here and the entry inflated through
// DeflateStream, which has been in the framework since 2.0. The alternative was
// the packaging reader in WindowsBase, which means loading a megabyte of WPF into
// a WinForms process and naming an absolute framework path in the build.
//
// What is not handled is deliberate: no writing, no encryption, no Zip64, no
// spanned archives. A document needing any of those is not one this app reads.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ClipSyncAI
{
    internal sealed partial class Zip : IDisposable
    {
        /// One entry as the central directory describes it.
        private sealed class Slot
        {
            public string Name = "";
            public int How;
            public long Packed;
            public long Plain;
            public long At;
        }

        /// An entry that inflates past this is not read. Text packs about ten to
        /// one and what all of this becomes is a few thousand characters of
        /// prompt, so the ceiling only ever catches a book.
        public const int Ceiling = 64 * 1024 * 1024;

        private readonly FileStream _f;
        private readonly List<Slot> _slots = new List<Slot>();
        private long _shift;

        public Zip(string path)
        {
            _f = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            try { Walk(); }
            catch (Exception) { _f.Dispose(); throw; }
        }

        public void Dispose() { _f.Dispose(); }

        /// Every entry's name, in the order the archive lists them.
        public string[] Names()
        {
            string[] got = new string[_slots.Count];
            for (int i = 0; i < _slots.Count; i++) got[i] = _slots[i].Name;
            return got;
        }

        /// The first entry whose name ends this way, or null. For the exporters
        /// that put a document somewhere other than where Word puts it.
        public string Ending(string tail)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Name.EndsWith(tail, StringComparison.OrdinalIgnoreCase))
                {
                    return _slots[i].Name;
                }
            }
            return null;
        }

        /// The end of central directory record, then the directory itself. The
        /// shift is what makes an archive with something glued to the front of it
        /// readable: every offset inside is written relative to a start that is
        /// no longer byte zero of the file.
        private void Walk()
        {
            long len = _f.Length;
            if (len < 22) throw new IOException("That file is not a document");
            int tail = (int)Math.Min(len, 66000);
            byte[] buf = new byte[tail];
            _f.Position = len - tail;
            Fill(buf, 0, tail);
            int end = -1;
            for (int i = tail - 22; i >= 0; i--)
            {
                if (buf[i] == 0x50 && buf[i + 1] == 0x4B && buf[i + 2] == 0x05 && buf[i + 3] == 0x06)
                {
                    end = i;
                    break;
                }
            }
            if (end < 0) throw new IOException("That file is not a document");
            int count = U16(buf, end + 10);
            long size = U32(buf, end + 12);
            long at = U32(buf, end + 16);
            if (count == 0xFFFF || at == 0xFFFFFFFF || size == 0xFFFFFFFF)
            {
                throw new IOException("That document is packed in a way this build cannot read");
            }
            if (size < 0 || size > 32 * 1024 * 1024) throw new IOException("That document is damaged");
            _shift = Math.Max(0, (len - tail + end) - size - at);
            long from = at + _shift;
            if (from < 0 || from + size > len) throw new IOException("That document is damaged");
            byte[] cd = new byte[(int)size];
            _f.Position = from;
            Fill(cd, 0, cd.Length);
            Sort(cd, count);
        }

        private void Sort(byte[] cd, int count)
        {
            int p = 0;
            for (int n = 0; n < count && p + 46 <= cd.Length; n++)
            {
                if (cd[p] != 0x50 || cd[p + 1] != 0x4B || cd[p + 2] != 0x01 || cd[p + 3] != 0x02) break;
                Slot s = new Slot();
                s.How = U16(cd, p + 10);
                s.Packed = U32(cd, p + 20);
                s.Plain = U32(cd, p + 24);
                int nl = U16(cd, p + 28);
                int el = U16(cd, p + 30);
                int cl = U16(cd, p + 32);
                s.At = U32(cd, p + 42);
                if (p + 46 + nl > cd.Length) break;
                s.Name = Encoding.UTF8.GetString(cd, p + 46, nl).Replace('\\', '/').TrimStart('/');
                if (s.Name.Length > 0 && !s.Name.EndsWith("/")) _slots.Add(s);
                p += 46 + nl + el + cl;
            }
        }

        private Slot Found(string name)
        {
            string want = (name ?? "").Replace('\\', '/').TrimStart('/');
            if (want.Length == 0) return null;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (string.Equals(_slots[i].Name, want, StringComparison.OrdinalIgnoreCase))
                {
                    return _slots[i];
                }
            }
            return null;
        }

        private void Fill(byte[] into, int at, int count)
        {
            int got = 0;
            while (got < count)
            {
                int n = _f.Read(into, at + got, count - got);
                if (n <= 0) throw new IOException("That document ends sooner than it says it does");
                got += n;
            }
        }

        private static int U16(byte[] b, int at)
        {
            return b[at] | (b[at + 1] << 8);
        }

        private static long U32(byte[] b, int at)
        {
            return (long)b[at] | ((long)b[at + 1] << 8) | ((long)b[at + 2] << 16) | ((long)b[at + 3] << 24);
        }
    }
}
