// The ledger's input and painting half.
//
// Split from the model so each file stays readable: this one is the pointer, the
// keyboard, the scrollbar and the paint loop. Only the rows that intersect the
// viewport are asked to draw, so list length costs nothing.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class Ledger
    {
        private int Gutter { get { return Theme.Px(8); } }
        private bool Scrolls { get { return _total > Height; } }

        private Rectangle Thumb()
        {
            if (!Scrolls) return Rectangle.Empty;
            int track = Height - Space.Sm * 2;
            int h = Math.Max(Theme.Px(28), (int)((double)track * Height / _total));
            double max = Math.Max(1, _total - Height + Space.Md);
            int y = Space.Sm + (int)((track - h) * Math.Min(1.0, _top / max));
            return new Rectangle(Width - Gutter, y, Theme.Px(4), h);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging)
            {
                int track = Math.Max(1, Height - Space.Sm * 2 - Thumb().Height);
                double max = Math.Max(0, _total - Height + Space.Md);
                _want = (double)(e.Y - Space.Sm - _dragGrab) / track * max;
                _top = _want;
                Clamp();
                Invalidate();
                return;
            }
            int was = _hot;
            _hot = e.X < Width - Gutter ? IndexAt(e.Y) : -1;
            if (_hot >= 0 && Skip(_hot)) _hot = -1;
            if (was != _hot) Invalidate();
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_hot != -1) { _hot = -1; Invalidate(); }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Select();
            Rectangle t = Thumb();
            if (t != Rectangle.Empty && e.X >= Width - Gutter - Theme.Px(4))
            {
                if (e.Y >= t.Y && e.Y < t.Bottom) { _dragging = true; _dragGrab = e.Y - t.Y; }
                else { _want = (double)e.Y / Math.Max(1, Height) * _total - Height / 2.0; Clamp(); Ticker.Poke(); }
                return;
            }
            int i = IndexAt(e.Y);
            if (i < 0 || Skip(i)) return;
            Selected = i;
            if (Clicked != null)
            {
                Rectangle b = RowBounds(i);
                Clicked(i, new Point(e.X - b.X, e.Y - b.Y));
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _dragging = false;
            base.OnMouseUp(e);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            int i = IndexAt(e.Y);
            if (i >= 0 && !Skip(i) && Activated != null) Activated(i);
            base.OnMouseDoubleClick(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            _want -= (double)e.Delta / 120.0 * Theme.Px(64);
            Clamp();
            if (Theme.ReduceMotion) _top = _want;
            Ticker.Poke();
            base.OnMouseWheel(e);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Reload();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Up: case Keys.Down: case Keys.PageUp: case Keys.PageDown:
                case Keys.Home: case Keys.End: case Keys.Enter:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            int n = Rows();
            if (n > 0)
            {
                if (e.KeyCode == Keys.Up) { Selected = Step(_live < 0 ? n : _live, -1); e.Handled = true; }
                else if (e.KeyCode == Keys.Down) { Selected = Step(_live < 0 ? -1 : _live, 1); e.Handled = true; }
                else if (e.KeyCode == Keys.Home) { Selected = Step(-1, 1); e.Handled = true; }
                else if (e.KeyCode == Keys.End) { Selected = Step(n, -1); e.Handled = true; }
                else if (e.KeyCode == Keys.PageUp) { Page(-1); e.Handled = true; }
                else if (e.KeyCode == Keys.PageDown) { Page(1); e.Handled = true; }
                else if (e.KeyCode == Keys.Enter && _live >= 0 && !Skip(_live) && Activated != null)
                {
                    Activated(_live);
                    e.Handled = true;
                }
            }
            base.OnKeyDown(e);
        }

        private void Page(int dir)
        {
            _want += dir * Math.Max(Theme.Px(64), Height - Space.Lg);
            Clamp();
            if (Theme.ReduceMotion) _top = _want;
            Ticker.Poke();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Canvas);
            Ui.Q(g);
            int n = Rows();
            if (n == 0) { Blank(g, ClientRectangle); return; }
            int off = (int)Math.Round(_top);
            int first = IndexAt(0);
            if (first < 0) first = _top <= 0 ? 0 : n - 1;
            int right = Width - Gutter;
            for (int i = first; i < n; i++)
            {
                int y = _tops[i] - off;
                if (y >= Height) break;
                Rectangle row = new Rectangle(0, y, right, _tops[i + 1] - _tops[i]);
                if (PaintRow != null) PaintRow(g, row, i, i == _hot, i == _live);
            }
            Rectangle t = Thumb();
            if (t != Rectangle.Empty)
            {
                Ui.Fill(g, t, Theme.Px(2), Palette.Alpha(Theme.OnSurface, _dragging ? 0.34 : 0.18));
            }
        }

        /// The empty state. A list with nothing in it is the first thing a new
        /// user sees, so it says what will appear here rather than sitting blank.
        private void Blank(Graphics g, Rectangle r)
        {
            if (EmptyTitle.Length == 0) return;
            int d = Theme.Px(56);
            int cx = r.X + (r.Width - d) / 2;
            int cy = r.Y + r.Height / 2 - Theme.Px(70);
            Rectangle disc = new Rectangle(cx, cy, d, d);
            Ui.Fill(g, disc, d / 2, Palette.Alpha(Theme.Accent, 0.12));
            int gi = Theme.Px(24);
            Icons.Draw(g, EmptyGlyph, new Rectangle(cx + (d - gi) / 2, cy + (d - gi) / 2, gi, gi),
                Theme.AccentText, Math.Max(1.4f, Theme.Px(1.6)));
            Rectangle tr = new Rectangle(r.X + Space.Xl, disc.Bottom + Space.Md,
                Math.Max(1, r.Width - Space.Xl * 2), Theme.Px(26));
            Ui.Centre(g, EmptyTitle, Theme.Title, Theme.OnSurface, tr);
            if (EmptyBody.Length == 0) return;
            Rectangle br = new Rectangle(tr.X, tr.Bottom + Theme.Px(2), tr.Width, Theme.Px(48));
            Ui.Text(g, EmptyBody, Theme.Small, Theme.Muted, br,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Ticker.Leave(this);
            base.Dispose(disposing);
        }
    }
}
