// Capture: reading a picture.
//
// Three ways to get one, in the order a desktop actually uses them. A region of the
// screen is the common case and the reason this page exists on a PC at all: the words
// worth capturing are usually already on screen, in a window that will not let you
// select them. The clipboard is second, because Print Screen is muscle memory. A file
// is third.
//
// The window hides itself before a region is drawn, since the thing being read is
// nearly always behind it.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace ClipSyncAI
{
    /// The scan card. It letterboxes the picture into the space under the buttons,
    /// because a preview stretched to fit says the wrong thing about what will be
    /// read.
    internal sealed class ShotCard : Card
    {
        public Bitmap Shot;
        public string Hint = "";
        public int Reserve;

        protected override void Paint2(Graphics g, Rectangle r)
        {
            Rectangle box = Inner;
            box = new Rectangle(box.X, box.Y + Reserve, box.Width,
                Math.Max(0, box.Height - Reserve));
            if (box.Height < Theme.Px(20)) return;
            Ui.Fill(g, box, Radii.Inner,
                Palette.Alpha(Theme.OnSurface, Theme.Dark ? 0.05 : 0.035));
            if (Shot == null)
            {
                Ui.Centre(g, Hint, Theme.Small, Theme.Muted, Ui.Inset(box, Space.Md));
                return;
            }
            Rectangle fit = Fit(Shot.Width, Shot.Height, Ui.Inset(box, Theme.Px(6)));
            InterpolationMode was = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(Shot, fit);
            g.InterpolationMode = was;
            Ui.Stroke(g, fit, Radii.Tight, Palette.Alpha(Theme.OnSurface, 0.14), 1f);
        }

        private static Rectangle Fit(int w, int h, Rectangle into)
        {
            if (w < 1 || h < 1 || into.Width < 1 || into.Height < 1) return into;
            double s = Math.Min((double)into.Width / w, (double)into.Height / h);
            if (s > 1) s = 1;
            int fw = Math.Max(1, (int)(w * s));
            int fh = Math.Max(1, (int)(h * s));
            return new Rectangle(into.X + (into.Width - fw) / 2,
                into.Y + (into.Height - fh) / 2, fw, fh);
        }
    }

    internal sealed partial class CaptureView
    {
        private readonly ShotCard _scan = new ShotCard();
        private readonly AppButton _region = new AppButton();
        private readonly AppButton _paste = new AppButton();
        private readonly AppButton _file = new AppButton();
        private Bitmap _shot;
        private bool _busy;
        private string _how = "";
        private string _from = "";

        private void BuildScan()
        {
            _scan.AccessibleName = "Picture to read";
            // What this frame is, not what the buttons above it do. The empty
            // state further down the page already gives the three ways in, and
            // the same sentence twice on one page reads as a mistake.
            _scan.Hint = "The picture appears here before it is read";
            _region.Label = "Choose a region";
            _region.ShowIcon = true;
            _region.Icon = Glyph.Capture;
            _region.Click += OnRegion;
            _paste.Label = "From the clipboard";
            _paste.Look = ButtonLook.Soft;
            _paste.ShowIcon = true;
            _paste.Icon = Glyph.Copy;
            _paste.Click += OnPaste;
            _file.Label = "Open a picture";
            _file.Look = ButtonLook.Soft;
            _file.ShowIcon = true;
            _file.Icon = Glyph.External;
            _file.Click += OnFile;
            _scan.Controls.Add(_region);
            _scan.Controls.Add(_paste);
            _scan.Controls.Add(_file);
            Controls.Add(_scan);
        }

        private void LayScan()
        {
            Rectangle box = _scan.Inner;
            int h = Theme.Px(34);
            int gap = Space.Sm;
            int w = Math.Max(Theme.Px(80), (box.Width - gap * 2) / 3);
            int third = box.X + (w + gap) * 2;
            _region.SetBounds(box.X, box.Y, w, h);
            _paste.SetBounds(box.X + w + gap, box.Y, w, h);
            _file.SetBounds(third, box.Y, Math.Max(Theme.Px(80), box.Right - third), h);
            _scan.Reserve = h + Space.Md;
            _scan.Invalidate();
        }

        private string ScanLead()
        {
            string tess = (Hub.Settings.TesseractPath ?? "").Trim();
            if (tess.Length > 0) return "Text out of a picture, read by Tesseract on this PC";
            string model = (Hub.Settings.VisionModel ?? "").Trim();
            if (model.Length == 0) return "Choose Tesseract or a model for pictures in Settings";
            if (!Hub.Brain.Ready) return "No model server is answering, so pictures cannot be read";
            return "Text out of a picture, read by " + model + " on this PC";
        }

        private bool Working()
        {
            if (!_busy) return false;
            Hub.Oops("Still reading the last picture");
            return true;
        }

        /// A region of the screen. The window goes away first and comes back in a
        /// finally, so a cancelled pick does not leave the app invisible.
        private void OnRegion(object sender, EventArgs e)
        {
            if (Working()) return;
            Form top = FindForm();
            bool was = top != null && top.Visible;
            Bitmap got = null;
            _busy = true;
            try
            {
                if (was)
                {
                    top.Visible = false;
                    Application.DoEvents();
                    Thread.Sleep(140);
                }
                got = Grab.Pick();
            }
            catch (Exception ex) { Paths.Log("pick region", ex); }
            finally
            {
                _busy = false;
                if (was && top != null && !top.IsDisposed) { top.Visible = true; top.Activate(); }
            }
            if (got == null) return;
            Took(got, "the screen");
        }

        private void OnPaste(object sender, EventArgs e)
        {
            if (Working()) return;
            Bitmap got = null;
            try
            {
                if (Clipboard.ContainsImage())
                {
                    Image img = Clipboard.GetImage();
                    if (img != null)
                    {
                        try { got = new Bitmap(img); }
                        finally { img.Dispose(); }
                    }
                }
            }
            catch (Exception ex) { Paths.Log("clipboard picture", ex); }
            if (got == null)
            {
                Hub.Oops("There is no picture on the clipboard. Press Print Screen first");
                return;
            }
            Took(got, "the clipboard");
        }

        private void OnFile(object sender, EventArgs e)
        {
            if (Working()) return;
            OpenFileDialog d = new OpenFileDialog();
            try
            {
                d.Title = "Open a picture";
                d.Filter = "Pictures|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|Every file|*.*";
                d.CheckFileExists = true;
                if (d.ShowDialog(FindForm()) != DialogResult.OK) return;
                Bitmap got = null;
                try
                {
                    using (Image img = Image.FromFile(d.FileName)) got = new Bitmap(img);
                }
                catch (Exception ex)
                {
                    Paths.Log("open picture", ex);
                    Hub.Oops("That file could not be opened as a picture");
                    return;
                }
                Took(got, Path.GetFileName(d.FileName));
            }
            finally { d.Dispose(); }
        }

        /// Keeps the picture and reads it. The one before is disposed here rather
        /// than when the next read finishes, because two screenfuls of a 4K display
        /// is 60 MB nobody asked for.
        private void Took(Bitmap got, string from)
        {
            if (_shot != null) _shot.Dispose();
            _shot = got;
            _scan.Shot = got;
            _scan.Invalidate();
            _from = from ?? "";
            _got[0] = "";
            if (_tab == 0)
            {
                _loading = true;
                _text.Text = "";
                _loading = false;
            }
            Run();
        }

        /// Reads on a worker thread. Tesseract is a process and a vision model is an
        /// HTTP call, so neither belongs on the thread drawing the window. The bitmap
        /// is copied first: the preview keeps painting while the read runs.
        private void Run()
        {
            if (_shot == null) { Hub.Oops("There is no picture to read"); return; }
            _busy = true;
            _how = "";
            _region.Busy = true;
            Told();
            Bitmap copy = new Bitmap(_shot);
            AppSettings s = Hub.Settings;
            Thread t = new Thread(new ThreadStart(delegate { Reads(copy, s); }));
            t.IsBackground = true;
            t.Name = "ocr";
            t.Start();
        }

        private void Reads(Bitmap copy, AppSettings s)
        {
            Reading r;
            try { r = Ocr.Read(copy, s, Hub.Brain, null); }
            catch (Exception ex)
            {
                Paths.Log("ocr", ex);
                r = new Reading();
                r.Why = "That picture could not be read";
            }
            finally { copy.Dispose(); }
            Post(delegate { Red(r); });
        }

        private void Red(Reading r)
        {
            _busy = false;
            _region.Busy = false;
            _how = r.How ?? "";
            if (!r.Ok)
            {
                Told();
                Hub.Oops(r.Why.Length > 0 ? r.Why : "There is no text in that picture");
                return;
            }
            _got[0] = r.Text;
            if (_tab == 0)
            {
                _loading = true;
                _text.Text = Lines(r.Text);
                _loading = false;
            }
            Told();
            Hub.Say(Say.Plural(Say.Words(r.Text), "word") + " read from " + _from);
        }
    }
}
