// Downloads GGUF models from Hugging Face, the hosted library for llama.cpp.
//
// This is the second of the two opt-in remote features (the other is the cloud
// engine): a user picks a model from the catalog and the download starts with a
// progress bar, resuming from where a previous attempt stopped. Nothing here
// is ever called automatically.

using System;
using System.IO;
using System.Net;

namespace ClipSyncAI
{
    internal static class ModelDownloader
    {
        /// Fetches a catalog model into the models folder. progress receives
        /// (bytes so far, total bytes); when the caller cancels, the partial
        /// file stays behind so the next attempt resumes from there.
        public static void Download(ModelEntry m, Action<long, long> progress, HttpCall call)
        {
            string dir = LocalEngine.ModelsDir;
            Directory.CreateDirectory(dir);
            string target = Path.Combine(dir, m.File);
            string part = target + ".part";

            long existing = File.Exists(part) ? new FileInfo(part).Length : 0;
            // A body that already reached the catalog size but was not renamed
            // (a crash between the last write and the move) is ready to use; a
            // freshly opened resume would ask a server for a range past the end
            // and end in a 416 that nothing recovers from.
            if (existing >= m.Bytes)
            {
                if (File.Exists(target)) File.Delete(target);
                File.Move(part, target);
                return;
            }

            HttpWebRequest r = Http.OpenRemote(ModelCatalog.DownloadUrl(m), "GET", 30000);
            r.ReadWriteTimeout = 60000;
            if (existing > 0) r.AddRange((long)existing);
            if (call != null) call.Request = r;

            long total = m.Bytes;
            long done = existing;
            bool restart = false;

            using (WebResponse resp = r.GetResponse())
            {
                HttpStatusCode status = ((HttpWebResponse)resp).StatusCode;
                long len = resp.ContentLength;

                // A server that ignored the Range header sends a full 200;
                // appending would corrupt the file, so start over. The total is
                // then just this body: adding the old partial on top of it would
                // both lie to the progress bar and send a "got exactly total but
                // the file has extra bytes" result, which the finish check below
                // then misreads as a stop early hop.
                if (status == HttpStatusCode.OK && existing > 0)
                {
                    done = 0;
                    restart = true;
                    total = len > 0 ? len : m.Bytes;
                    if (progress != null) progress(0, total);
                }
                else if (len > 0)
                {
                    total = existing + len;
                }

                using (Stream src = resp.GetResponseStream())
                using (FileStream fs = new FileStream(part,
                    restart ? FileMode.Create : FileMode.Append,
                    FileAccess.Write, FileShare.None))
                {
                    byte[] buf = new byte[65536];
                    while (true)
                    {
                        if (call != null && call.Cancelled) return;
                        int n = src.Read(buf, 0, buf.Length);
                        if (n <= 0) break;
                        fs.Write(buf, 0, n);
                        done += n;
                        if (progress != null) progress(done, total);
                    }
                }
            }

            long size = new FileInfo(part).Length;
            if (size < total * 0.95)
            {
                throw new InvalidOperationException(
                    "Download stopped early: got " + size + " of about " + total +
                    " bytes. Try again; it will resume from where it stopped.");
            }
            if (File.Exists(target)) File.Delete(target);
            File.Move(part, target);
        }
    }
}
