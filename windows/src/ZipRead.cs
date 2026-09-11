// Zip: pulling one entry out.
//
// The local header is read again rather than trusted from the directory, because
// its extra field is allowed to differ in length from the one in the directory and
// the data starts after it. The sizes and the method come from the directory,
// which always has them; a local header is allowed to leave them zero and write
// them after the data instead.

using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ClipSyncAI
{
    internal sealed partial class Zip
    {
        /// One entry's bytes, or null when the archive has no such entry.
        public byte[] Take(string name)
        {
            Slot s = Found(name);
            if (s == null) return null;
            if (s.Plain > Ceiling) throw new IOException(Fat);
            byte[] packed = Raw(s);
            if (s.How == 0) return packed;
            if (s.How != 8) throw new IOException("That document is packed in a way this build cannot read");
            return Blown(packed, s.Plain);
        }

        /// One entry as text, or null when there is no such entry. The parts
        /// inside a document are UTF-8 by the standard that describes them, and a
        /// mark at the front is allowed even though nothing writes one.
        public string Text(string name)
        {
            byte[] raw = Take(name);
            if (raw == null) return null;
            if (raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF)
            {
                return new UTF8Encoding(false).GetString(raw, 3, raw.Length - 3);
            }
            return new UTF8Encoding(false).GetString(raw);
        }

        private const string Fat = "There is too much inside that document to read";

        private byte[] Raw(Slot s)
        {
            long head = s.At + _shift;
            if (head < 0 || head + 30 > _f.Length) throw new IOException(Bad);
            byte[] local = new byte[30];
            _f.Position = head;
            Fill(local, 0, 30);
            if (local[0] != 0x50 || local[1] != 0x4B || local[2] != 0x03 || local[3] != 0x04)
            {
                throw new IOException(Bad);
            }
            long from = head + 30 + U16(local, 26) + U16(local, 28);
            if (s.Packed < 0 || from < 0 || from + s.Packed > _f.Length) throw new IOException(Bad);
            if (s.Packed > Ceiling) throw new IOException(Fat);
            byte[] packed = new byte[(int)s.Packed];
            _f.Position = from;
            Fill(packed, 0, packed.Length);
            return packed;
        }

        private const string Bad = "That document is damaged";

        /// Inflated in chunks with the ceiling checked as it goes, so a header
        /// claiming a small file that unpacks into a huge one is stopped at the
        /// limit rather than after the memory has already gone.
        private static byte[] Blown(byte[] packed, long plain)
        {
            int guess = plain > 0 && plain < Ceiling ? (int)plain : 64 * 1024;
            using (MemoryStream src = new MemoryStream(packed, false))
            using (DeflateStream flat = new DeflateStream(src, CompressionMode.Decompress))
            using (MemoryStream got = new MemoryStream(guess))
            {
                byte[] chunk = new byte[64 * 1024];
                long total = 0;
                while (true)
                {
                    int n = flat.Read(chunk, 0, chunk.Length);
                    if (n <= 0) break;
                    total += n;
                    if (total > Ceiling) throw new IOException(Fat);
                    got.Write(chunk, 0, n);
                }
                return got.ToArray();
            }
        }
    }
}
