// A turn's layout and painting.
//
// The plate behind a question is painted by the turn rather than parented into it,
// for the same reason the clear cross in a field is: a child control clears itself
// to its parent's colour and would print a flat square over a rounded fill.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class Turn
    {
        public void Lay()
        {
            int d = Theme.Px(26);
            More.SetBounds(Width - Gut + Theme.Px(4), _mine ? Space.Xs : Theme.Px(1), d, d);
            if (_mine)
            {
                int pw = Plate(Width);
                _plate = new Rectangle(Math.Max(0, Width - Gut - pw), 0, pw, Height);
                return;
            }
            _md.Visible = !_waiting;
            Take.Visible = _foot && !_waiting;
            Retry.Visible = _foot && !_waiting;
            if (_waiting) return;
            int tw = Math.Max(Theme.Px(80), Width - Gut);
            _md.SetBounds(0, Head, tw, Math.Max(Theme.Px(20), _md.Wants()));
            if (!_foot) return;
            int fy = _md.Bottom + Theme.Px(2);
            Take.SetBounds(0, fy, d, d);
            Retry.SetBounds(d + Space.Xs, fy, d, d);
        }

        public void ApplyTheme()
        {
            if (_md != null) _md.ApplyTheme();
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Lay();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Canvas);
            Ui.Q(g);
            if (_mine) { Asked(g); return; }
            Ui.Line(g, "ClipSyncAI", Theme.Tiny, Theme.Faint,
                new Rectangle(Theme.Px(2), 0, Math.Max(20, Width - Gut), Head));
            if (_waiting) Bars(g);
        }

        /// The question, in a soft plate against the trailing edge.
        private void Asked(Graphics g)
        {
            if (_plate.Width <= 0) return;
            Ui.Fill(g, _plate, Radii.Inner, Palette.Alpha(Theme.Accent, 0.13));
            Ui.Stroke(g, _plate, Radii.Inner, Palette.Alpha(Theme.Accent, 0.28), 1f);
            Rectangle inner = new Rectangle(_plate.X + Space.Md, _plate.Y + Space.Md,
                Math.Max(10, _plate.Width - Space.Md * 2), Math.Max(10, _plate.Height - Space.Md * 2));
            if (_note.Length > 0)
            {
                int nh = Theme.Tiny.Height + Theme.Px(3);
                Ui.Wrap(g, _body, Theme.Body, Theme.OnSurface,
                    new Rectangle(inner.X, inner.Y, inner.Width, Math.Max(10, inner.Height - nh)));
                Ui.Line(g, _note, Theme.Tiny, Theme.Faint,
                    new Rectangle(inner.X, inner.Bottom - nh, inner.Width, nh));
                return;
            }
            Ui.Wrap(g, _body, Theme.Body, Theme.OnSurface, inner);
        }

        /// Three bars where the words will be. Static rather than shimmering: the
        /// composer already says it is generating, and a pulse in the corner of
        /// the eye is not worth the frames on a machine that is busy inferring.
        private void Bars(Graphics g)
        {
            int w = Math.Max(Theme.Px(80), Width - Gut);
            double[] run = { 0.88, 0.72, 0.46 };
            int h = Theme.Px(9);
            int y = Head + Theme.Px(2);
            Color c = Palette.Alpha(Theme.OnSurface, 0.10);
            for (int i = 0; i < run.Length; i++)
            {
                Ui.Fill(g, new Rectangle(Theme.Px(2), y, (int)(w * run[i]), h), h / 2, c);
                y += h + Space.Sm;
            }
        }
    }
}
