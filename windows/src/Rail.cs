// The navigation rail.
//
// The phone build floats a capsule over the content because a thumb reaches the
// bottom of a screen more easily than the top. A mouse has no such preference and
// a desktop window has width to spare, so the capsule becomes a rail down the
// left: every destination visible at once, labelled, with the keyboard order
// matching the reading order.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed class Rail : Widget
    {
        private readonly Anim _pill = new Anim(Motion.Base);
        private readonly Anim _fold = new Anim(Motion.Base);
        private string[] _items = new string[0];
        private int _index;
        private int _hot = -1;
        private string _status = "";
        private Color _statusInk;
        private bool _collapsed;

        public event EventHandler Changed;

        public Rail()
        {
            Cursor = Cursors.Default;
            _statusInk = Palette.Success;
            AccessibleRole = AccessibleRole.PageTabList;
        }

        public void SetItems(string[] items, int selected)
        {
            _items = items ?? new string[0];
            _index = selected;
            _pill.Set(selected);
            Invalidate();
        }

        public int Index
        {
            get { return _index; }
            set
            {
                if (_items.Length == 0) return;
                int v = value < 0 ? 0 : (value >= _items.Length ? _items.Length - 1 : value);
                if (v == _index) return;
                _index = v;
                _pill.To(v);
                AccessibleDescription = _items[v];
                Invalidate();
                if (Changed != null) Changed(this, EventArgs.Empty);
            }
        }

        public bool Collapsed
        {
            get { return _collapsed; }
            set
            {
                if (_collapsed == value) return;
                _collapsed = value;
                _fold.To(value ? 1 : 0);
                Invalidate();
            }
        }

        public void SetStatus(string text, Color ink)
        {
            _status = text ?? "";
            _statusInk = ink;
            Invalidate();
        }

        private int RowHeight { get { return Theme.Px(42); } }
        private int Origin { get { return Theme.Px(64); } }

        private int RowAt(int y)
        {
            int i = (y - Origin) / Math.Max(1, RowHeight);
            return i >= 0 && i < _items.Length ? i : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int was = _hot;
            _hot = RowAt(e.Y);
            Cursor = _hot >= 0 ? Cursors.Hand : Cursors.Default;
            if (was != _hot) Invalidate();
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hot = -1;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int row = RowAt(e.Y);
            if (row >= 0) Index = row;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Up || keyData == Keys.Down) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Up) { Index = _index - 1; e.Handled = true; return; }
            if (e.KeyCode == Keys.Down) { Index = _index + 1; e.Handled = true; return; }
            base.OnKeyDown(e);
        }

        public override bool Tick()
        {
            bool busy = base.Tick();
            if (_pill.Advance() | _fold.Advance()) { Invalidate(); return true; }
            return busy;
        }

        protected override void Render(Graphics g, Rectangle r)
        {
            Ui.Fill(g, r, 0, Theme.Low);
            using (Pen pen = new Pen(Theme.Hairline))
            {
                g.DrawLine(pen, r.Right - 1, r.Y, r.Right - 1, r.Bottom);
            }
            double folded = _fold.Value;
            bool wide = folded < 0.5;

            int pad = Theme.Px(14);
            int icon = Theme.Px(20);
            Rectangle mark = new Rectangle(r.X + pad, Theme.Px(20), icon, icon);
            Icons.Draw(g, Glyph.Sparkle, mark, Theme.AccentText, Math.Max(1.4f, Theme.Px(1.6)));
            if (wide)
            {
                Rectangle name = new Rectangle(mark.Right + Space.Sm, mark.Y - Theme.Px(3),
                    r.Width - mark.Right - Space.Sm, icon + Theme.Px(6));
                Ui.Line(g, "ClipSyncAI", Theme.Title, Palette.Alpha(Theme.OnSurface, 1 - folded * 2), name);
            }

            int cell = RowHeight;
            int inset = Theme.Px(8);
            if (_items.Length > 0)
            {
                Rectangle pill = new Rectangle(r.X + inset,
                    Origin + (int)Math.Round(_pill.Value * cell) + Theme.Px(3),
                    r.Width - inset * 2 - Theme.Px(1), cell - Theme.Px(6));
                Ui.Fill(g, pill, Radii.Control, Palette.Alpha(Theme.Accent, 0.16));
                Ui.Stroke(g, pill, Radii.Control, Palette.Alpha(Theme.Accent, 0.42), 1f);
            }

            for (int i = 0; i < _items.Length; i++)
            {
                Rectangle row = new Rectangle(r.X + inset, Origin + i * cell + Theme.Px(3),
                    r.Width - inset * 2, cell - Theme.Px(6));
                bool live = i == _index;
                if (!live && i == _hot)
                {
                    Ui.Fill(g, row, Radii.Control, Palette.Alpha(Theme.OnSurface, 0.05));
                }
                Color ink = live ? Theme.AccentText : (i == _hot ? Theme.OnSurface : Theme.Muted);
                Rectangle ir = new Rectangle(row.X + Theme.Px(10), row.Y + (row.Height - icon) / 2, icon, icon);
                Icons.Draw(g, Icons.ForTab(_items[i]), ir, ink, Math.Max(1.4f, Theme.Px(live ? 1.8 : 1.5)));
                if (wide)
                {
                    Rectangle tr = new Rectangle(ir.Right + Space.Sm, row.Y,
                        row.Width - ir.Right - Space.Sm, row.Height);
                    Ui.Line(g, _items[i], live ? Theme.BodyBold : Theme.Body,
                        Palette.Alpha(ink, 1 - folded * 2), tr);
                }
            }

            if (_status.Length > 0)
            {
                int dot = Theme.Px(8);
                Rectangle foot = new Rectangle(r.X + pad, r.Bottom - Theme.Px(34), r.Width - pad * 2, Theme.Px(20));
                using (SolidBrush b = new SolidBrush(_statusInk))
                {
                    g.FillEllipse(b, foot.X, foot.Y + (foot.Height - dot) / 2, dot, dot);
                }
                if (wide)
                {
                    Rectangle tr = new Rectangle(foot.X + dot + Space.Sm, foot.Y,
                        foot.Width - dot - Space.Sm, foot.Height);
                    Ui.Line(g, _status, Theme.Small, Palette.Alpha(Theme.Muted, 1 - folded * 2), tr);
                }
            }
        }
    }
}
