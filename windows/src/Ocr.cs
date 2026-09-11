// Reading text out of a picture.
//
// Two ways, both on this PC. Tesseract if it is installed, because it is a real
// recogniser and it is fast; a vision model on the local server if not. Neither is
// bundled: this build is one executable with no dependency beyond the .NET Framework
// Windows already has, and a 30 MB language pack inside it would be a lie about what
// it is.
//
// Which one answered is returned alongside the text, because a page that says
// "recognised on this PC" should be able to say by what.

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace ClipSyncAI
{
    internal sealed class Reading
    {
        public string Text = "";
        public string How = "";
        public string Why = "";
        public bool Ok { get { return Text.Length > 0; } }
    }

    internal static class Ocr
    {
        private const int TesseractTimeoutMs = 60000;

        /// The prompt for a vision model. It is told to transcribe rather than
        /// describe, because a model asked about a picture of a receipt will
        /// happily explain what a receipt is.
        private const string Ask = "Transcribe every piece of text in this image exactly as it " +
            "appears, in reading order. Keep line breaks. Do not describe the image, do not " +
            "explain, do not add anything. If there is no text, reply with nothing at all.";

        /// Reads the picture. Runs on a worker thread: Tesseract is a process and
        /// a vision model is an HTTP call, and both take seconds.
        public static Reading Read(Bitmap picture, AppSettings settings, Ai brain, HttpCall call)
        {
            Reading r = new Reading();
            if (picture == null) { r.Why = "There is no picture to read"; return r; }
            string tess = (settings.TesseractPath ?? "").Trim();
            if (tess.Length > 0)
            {
                if (!File.Exists(tess))
                {
                    r.Why = "Tesseract is not at the path in Settings any more";
                    return r;
                }
                return ByTesseract(picture, tess);
            }
            string model = (settings.VisionModel ?? "").Trim();
            if (model.Length == 0)
            {
                r.Why = "Choose a model for pictures in Settings, or point this app at " +
                    "Tesseract on your PC";
                return r;
            }
            if (!brain.Ready)
            {
                r.Why = "No model server is answering, so there is nothing to read the picture";
                return r;
            }
            return ByModel(picture, model, brain, call);
        }

        private static Reading ByTesseract(Bitmap picture, string exe)
        {
            Reading r = new Reading();
            r.How = "Tesseract";
            string stem = Path.Combine(Path.GetTempPath(), "clipsyncai-ocr-" + Guid.NewGuid().ToString("N"));
            string png = stem + ".png";
            try
            {
                picture.Save(png, ImageFormat.Png);
                ProcessStartInfo psi = new ProcessStartInfo(exe);
                // stdout rather than a second temporary file, and a quoted input
                // path because Program Files is not the only folder with a space
                // in it.
                psi.Arguments = "\"" + png + "\" stdout";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.WorkingDirectory = Path.GetDirectoryName(exe) ?? Path.GetTempPath();
                using (Process p = Process.Start(psi))
                {
                    if (p == null) { r.Why = "Tesseract would not start"; return r; }

                    // Both pipes are read by their own task. A synchronous
                    // stdout-then-stderr sequence can deadlock on enough stderr
                    // output, and then the timeout below never gets a chance to
                    // fire because WaitForExit is only reached afterwards.
                    string outText = null;
                    string errText = null;
                    System.Threading.Tasks.Task stdout = System.Threading.Tasks.Task.Factory.StartNew(
                        delegate { outText = p.StandardOutput.ReadToEnd(); });
                    System.Threading.Tasks.Task stderr = System.Threading.Tasks.Task.Factory.StartNew(
                        delegate { errText = p.StandardError.ReadToEnd(); });
                    bool finished = System.Threading.Tasks.Task.WaitAll(
                        new System.Threading.Tasks.Task[] { stdout, stderr }, TesseractTimeoutMs);
                    if (!finished)
                    {
                        try { p.Kill(); } catch (Exception) { }
                        try { System.Threading.Tasks.Task.WaitAll(new System.Threading.Tasks.Task[] { stdout, stderr }, 2000); }
                        catch (Exception) { }
                        r.Why = "Tesseract took too long and was stopped";
                        return r;
                    }

                    r.Text = Tidy(outText);
                    if (r.Text.Length == 0)
                    {
                        r.Why = p.ExitCode == 0
                            ? "There is no text in that picture"
                            : "Tesseract could not read it. " + Say.FirstLine(errText, 160);
                    }
                }
            }
            catch (Exception ex)
            {
                Paths.Log("tesseract", ex);
                r.Why = "Tesseract could not be run. Check the path in Settings";
            }
            finally
            {
                try { if (File.Exists(png)) File.Delete(png); }
                catch (Exception) { }
            }
            return r;
        }

        private static Reading ByModel(Bitmap picture, string model, Ai brain, HttpCall call)
        {
            Reading r = new Reading();
            r.How = model;
            try
            {
                string b64;
                using (MemoryStream ms = new MemoryStream())
                {
                    picture.Save(ms, ImageFormat.Png);
                    b64 = Convert.ToBase64String(ms.ToArray());
                }
                string got = brain.See(model, Ask, b64, call);
                r.Text = Tidy(got);
                if (r.Text.Length == 0)
                {
                    r.Why = got == null
                        ? "That server cannot read pictures with the model that is chosen"
                        : "There is no text in that picture";
                }
            }
            catch (Exception ex)
            {
                Paths.Log("vision ocr", ex);
                r.Why = EngineFactory.Explain(ex, "");
            }
            return r;
        }

        /// Normalises line endings and drops the trailing blank lines both
        /// Tesseract and a chatty model leave behind.
        private static string Tidy(string got)
        {
            if (string.IsNullOrEmpty(got)) return "";
            return got.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        }
    }
}
