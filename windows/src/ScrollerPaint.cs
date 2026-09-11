// The scroller's pointer and paint half.
//
// Nothing but the thumb is drawn here: the children paint themselves, and they
// are inset by the thumb's lane so the two never overlap.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class Scroller
    {
        private Rectangle Thumb()
        {
            if (!Scrolls) return Rectangle.Empty;
            int track = Height - Space.Sm * 2;
            int h = Math.Max(Theme.Px(28), (int)((double)track * Height / _total));
            double max = Math.Max(1, Max);
            int y = Space.Sm + (int)((track - h) * Math.Min(1.0, _top / max));
            return new Rectangle(Width - Gutter, y, Theme.Px(4), h);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            Wheel(e.Delta);
            base.OnMouseWheel(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Rectangle t = Thumb();
            if (t != Rectangle.Empty && e.X >= Width - Gutter - Theme.Px(4))
            {
                if (e.Y >= t.Y && e.Y < t.Bottom) { _dragging = true; _dragGrab = e.Y - t.Y; }
                else
                {
                    _want = (double)e.Y / Math.Max(1, Height) * _total - Height / 2.0;
                    Clamp();
                    Ticker.Poke();
                }
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging)
            {
                int track = Math.Max(1, Height - Space.Sm * 2 - Thumb().Height);
                _want = (double)(e.Y - Space.Sm - _dragGrab) / track * Max;
                _top = _want;
                Clamp();
                Place();
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _dragging = false;
            base.OnMouseUp(e);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Reload();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Canvas);
            Ui.Q(g);
            Rectangle t = Thumb();
            if (t == Rectangle.Empty) return;
            Ui.Fill(g, t, Theme.Px(2), Palette.Alpha(Theme.OnSurface, _dragging ? 0.34 : 0.18));
        }
    }
}
