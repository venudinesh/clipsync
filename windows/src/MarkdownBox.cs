// The markdown surface.
//
// Assistant replies, note bodies and clip detail all arrive as markdown, and all
// three need mixed weights, a monospace run, a quote rule and a clickable link in
// the same block of text. A read only RichTextBox fed the RTF the renderer builds
// is the one WinForms surface that holds all of that without a browser control,
// and it keeps selection, Ctrl+C and screen reader text for free.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed class MarkdownBox : Control
    {
        private readonly RichTextBox _box = new RichTextBox();
        private MdDoc _doc;
        private string _source = "";
        private bool _overLink;

        public event Action<string> LinkClicked;

        /// Grows to fit its content instead of scrolling. Chat bubbles and note
        /// cards size to the text; the clip detail pane scrolls instead.
        public bool AutoHeight;

        public int Pad = Space.Md;

        public MarkdownBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            _box.BorderStyle = BorderStyle.None;
            _box.ReadOnly = true;
            _box.DetectUrls = false;
            _box.WordWrap = true;
            _box.ScrollBars = RichTextBoxScrollBars.None;
            _box.Cursor = Cursors.IBeam;
            _box.MouseMove += OnBoxMove;
            _box.MouseUp += OnBoxUp;
            _box.GotFocus += OnBoxFocus;
            _box.MouseWheel += OnBoxWheel;
            _box.KeyUp += delegate { Native.NoCaret(_box.Handle); };
            Controls.Add(_box);
            ApplyTheme();
        }

        public RichTextBox Box { get { return _box; } }

        public override string Text
        {
            get { return _source; }
            set { Source = value; }
        }

        /// The markdown to show. Rendering happens once here, not on every paint.
        public string Source
        {
            get { return _source; }
            set
            {
                _source = value ?? "";
                _doc = Markdown.Render(_source);
                if (_doc.Rtf.Length == 0) _box.Clear();
                else
                {
                    try { _box.Rtf = _doc.Rtf; }
                    catch (ArgumentException) { _box.Text = _doc.Plain; }
                }
                AccessibleName = _doc.Plain;
                Place();
                if (AutoHeight) Height = Wants();
            }
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Canvas;
            _box.BackColor = Theme.Canvas;
            _box.ForeColor = Theme.OnSurface;
            Theme.Wear(_box, Theme.Body);
            if (_source.Length > 0) Source = _source;
            Place();
        }

        /// Sits the text on a surface other than the canvas, for a sheet or a
        /// card. A RichTextBox cannot be transparent, so it has to be told.
        public void Surface(Color c)
        {
            BackColor = c;
            _box.BackColor = c;
            Invalidate();
        }

        /// The height this box needs for its current text at its current width.
        public int Wants()
        {
            if (_doc == null || _box.TextLength == 0) return Pad * 2;
            Point end = _box.GetPositionFromCharIndex(Math.Max(0, _box.TextLength - 1));
            return end.Y + Theme.Px(20) + Pad * 2;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Place();
        }

        private void Place()
        {
            _box.SetBounds(Pad, Pad, Math.Max(10, Width - Pad * 2), Math.Max(10, Height - Pad * 2));
        }

        private int LinkAt(Point p)
        {
            if (_doc == null || _doc.Links.Count == 0) return -1;
            int i = _box.GetCharIndexFromPosition(p);
            for (int k = 0; k < _doc.Links.Count; k++)
            {
                LinkSpan s = _doc.Links[k];
                if (i >= s.Start && i < s.Start + s.Length) return k;
            }
            return -1;
        }

        private void OnBoxMove(object sender, MouseEventArgs e)
        {
            bool over = LinkAt(e.Location) >= 0;
            if (over == _overLink) return;
            _overLink = over;
            _box.Cursor = over ? Cursors.Hand : Cursors.IBeam;
        }

        private void OnBoxUp(object sender, MouseEventArgs e)
        {
            Native.NoCaret(_box.Handle);
            if (e.Button != MouseButtons.Left) return;
            if (_box.SelectionLength > 0) return;
            int k = LinkAt(e.Location);
            if (k < 0) return;
            if (LinkClicked != null) LinkClicked(_doc.Links[k].Url);
        }

        private void OnBoxFocus(object sender, EventArgs e)
        {
            Native.NoCaret(_box.Handle);
        }

        /// A rich text box keeps the wheel to itself, and one sized to its own
        /// text has nothing to do with it. Handing it to the scroller behind is
        /// what stops a transcript freezing under the pointer.
        private void OnBoxWheel(object sender, MouseEventArgs e)
        {
            if (!AutoHeight) return;
            Scroller s = Scroller.Of(this);
            if (s != null) s.Wheel(e.Delta);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
        }
    }
}
