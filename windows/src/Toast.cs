// The toast.
//
// One line of feedback for something that has already happened: copied, pinned,
// saved, deleted. It never asks a question and never blocks, so it takes no
// focus. It rises from the bottom of the content area, holds long enough to be
// read, and leaves.
//
// One exception earns a button: an action that threw something away offers to
// put it back. That toast holds longer, and because a painted word is no use to
// anyone who is not holding a mouse, the same undo is on Ctrl+Z.

using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal enum Tone { Plain, Good, Bad }

    internal sealed class Toast : Control, IAnimated
    {
        private readonly Anim _rise = new Anim(Motion.Base);
        private readonly Stopwatch _clock = new Stopwatch();
        private string _text = "";
        private string _verb = "";
        private Action _act;
        private bool _onVerb;
        private Glyph _icon = Glyph.Check;
        private Tone _tone = Tone.Plain;
        private int _holdMs = 2600;

        public Toast()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            TabStop = false;
            Visible = false;
            Height = Theme.Px(42);
            Ticker.Join(this);
        }

        /// Puts a message up. Calling again replaces what is showing, because two
        /// stacked toasts is two things competing to be read.
        public void Say(string text, Glyph icon, Tone tone)
        {
            Show(text, icon, tone, "", null);
        }

        /// A message with a way back. The verb is one word, and taking it also
        /// takes the toast away, since the offer is good once.
        public void Offer(string text, string verb, Action act)
        {
            Show(text, Glyph.Check, Tone.Good, verb ?? "", act);
        }

        private void Show(string text, Glyph icon, Tone tone, string verb, Action act)
        {
            _text = text ?? "";
            _verb = act != null ? verb : "";
            _act = act;
            _icon = icon;
            _tone = tone;
            _onVerb = false;
            Cursor = Cursors.Default;
            _holdMs = _act != null ? 6000 : (_text.Length > 60 ? 4200 : 2600);
            Height = Theme.Px(42);
            int want = Ui.Measure(_text, Theme.Body).Width + Theme.Px(72);
            if (_verb.Length > 0) want += Ui.Measure(_verb, Theme.BodyBold).Width + Space.Xl;
            Width = Math.Min(Parent != null ? Parent.Width - Space.Xl * 2 : 520, want);
            Lay();
            Visible = true;
            BringToFront();
            _clock.Reset();
            _clock.Start();
            _rise.To(1);
            Invalidate();
            Ticker.Poke();
        }

        public void Good(string text) { Say(text, Glyph.Check, Tone.Good); }
        public void Bad(string text) { Say(text, Glyph.Close, Tone.Bad); }

        /// Centres itself along the bottom of its parent. Called on show and
        /// whenever the shell resizes.
        public void Lay()
        {
            if (Parent == null) return;
            int y = Parent.ClientSize.Height - Height - Space.Xl;
            Left = (Parent.ClientSize.Width - Width) / 2;
            Top = y + (int)Math.Round((1 - _rise.Value) * Theme.Px(18));
        }

        public bool Tick()
        {
            bool busy = _rise.Advance();
            if (busy) { Lay(); Invalidate(); }
            if (_clock.IsRunning && _clock.ElapsedMilliseconds > _holdMs)
            {
                _clock.Reset();
                _rise.To(0);
                busy = true;
            }
            if (!busy && _rise.Value <= 0.01 && Visible) { _act = null; _verb = ""; Visible = false; }
            return busy || _clock.IsRunning;
        }

        private Rectangle VerbRect()
        {
            if (_verb.Length == 0) return Rectangle.Empty;
            int w = Ui.Measure(_verb, Theme.BodyBold).Width + Space.Md * 2;
            return new Rectangle(Math.Max(0, Width - Theme.Px(2) - Space.Sm - w), Theme.Px(3),
                w, Math.Max(4, Height - Theme.Px(6)));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool on = _act != null && VerbRect().Contains(e.Location);
            if (on == _onVerb) return;
            _onVerb = on;
            Cursor = on ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!_onVerb) return;
            _onVerb = false;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_act == null || !VerbRect().Contains(e.Location)) return;
            Action act = _act;
            _act = null;
            _verb = "";
            _clock.Reset();
            _rise.To(0);
            Ticker.Poke();
            act();
        }

        private Color Ink
        {
            get
            {
                if (_tone == Tone.Good) return Palette.Success;
                if (_tone == Tone.Bad) return Palette.Danger;
                return Theme.AccentText;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Canvas);
            Ui.Q(g);
            double v = _rise.Value;
            if (v <= 0.01) return;
            Rectangle r = Ui.Inset(ClientRectangle, Theme.Px(2));
            Ui.Shadow(g, r, Radii.Pill, Theme.Px(7), Color.Black, Theme.Dark ? 150 : 70);
            Ui.Fill(g, r, Radii.Pill, Palette.Alpha(Theme.Highest, v));
            Ui.Stroke(g, r, Radii.Pill, Palette.Alpha(Theme.Outline, v), 1f);
            int d = Theme.Px(16);
            Rectangle ir = new Rectangle(r.X + Space.Lg, r.Y + (r.Height - d) / 2, d, d);
            Icons.Draw(g, _icon, ir, Palette.Alpha(Ink, v), Math.Max(1.4f, Theme.Px(1.7)));
            Rectangle vr = VerbRect();
            int right = vr.IsEmpty ? r.Right - Space.Lg : vr.X - Space.Sm;
            Rectangle tr = new Rectangle(ir.Right + Space.Md, r.Y,
                Math.Max(10, right - ir.Right - Space.Md), r.Height);
            Ui.Line(g, _text, Theme.Body, Palette.Alpha(Theme.OnSurface, v), tr);
            if (vr.IsEmpty) return;
            using (Pen p = new Pen(Palette.Alpha(Theme.Hairline, v), 1f))
            {
                g.DrawLine(p, vr.X - Space.Sm, r.Y + Theme.Px(9), vr.X - Space.Sm, r.Bottom - Theme.Px(9));
            }
            if (_onVerb) Ui.Fill(g, vr, Radii.Pill, Palette.Alpha(Theme.AccentText, 0.12 * v));
            Ui.Centre(g, _verb, Theme.BodyBold, Palette.Alpha(Theme.AccentText, v), vr);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Ticker.Leave(this);
            base.Dispose(disposing);
        }
    }
}
