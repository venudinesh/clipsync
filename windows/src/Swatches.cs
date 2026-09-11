// The accent picker.
//
// Eight circles. A colour is the one setting where a word is worse than the
// thing itself, so the swatches are the control rather than a list of names with
// a preview beside it. The chosen one wears a ring in the ink of the page, not in
// its own colour, so it stays visible when the colour is pale.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed class Swatches : Widget
    {
        private readonly Anim _pick = new Anim(Motion.Base, Ease.Bounce);
        private int _index;
        private int _hot = -1;

        public event EventHandler Changed;

        public Swatches()
        {
            Height = Theme.Px(30);
            Width = Span();
            AccessibleRole = AccessibleRole.ButtonDropDownGrid;
            AccessibleName = "Accent colour";
        }

        private static int Dot { get { return Theme.Px(22); } }
        private static int Step { get { return Dot + Space.Sm; } }

        private static int Span()
        {
            return Step * Palette.Accents.Length - Space.Sm;
        }

        public int Index
        {
            get { return _index; }
            set
            {
                int v = value < 0 ? 0 : (value >= Palette.Accents.Length ? Palette.Accents.Length - 1 : value);
                if (v == _index) return;
                _index = v;
                _pick.To(v);
                AccessibleDescription = Palette.Accents[v].Name;
                Invalidate();
                if (Changed != null) Changed(this, EventArgs.Empty);
            }
        }

        /// Puts the ring on the swatch matching a colour, without telling anybody.
        /// A colour that is not in the palette leaves no swatch chosen, which is
        /// the honest answer for one that was set by hand.
        public void Preset(Color accent)
        {
            int at = -1;
            for (int i = 0; i < Palette.Accents.Length; i++)
            {
                if (Palette.Accents[i].Color.ToArgb() == accent.ToArgb()) at = i;
            }
            _index = at < 0 ? -1 : at;
            _pick.Set(Math.Max(0, _index));
            AccessibleDescription = _index < 0 ? "custom" : Palette.Accents[_index].Name;
            Invalidate();
        }

        public Color Chosen
        {
            get
            {
                return _index >= 0 && _index < Palette.Accents.Length
                    ? Palette.Accents[_index].Color : Theme.Accent;
            }
        }

        private int At(int x)
        {
            int i = x / Math.Max(1, Step);
            return i >= 0 && i < Palette.Accents.Length && x - i * Step <= Dot ? i : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int was = _hot;
            _hot = At(e.X);
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
            int at = At(e.X);
            if (at >= 0) Index = at;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Left || keyData == Keys.Right) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left) { Index = Math.Max(0, _index) - 1; e.Handled = true; return; }
            if (e.KeyCode == Keys.Right) { Index = Math.Max(0, _index) + 1; e.Handled = true; return; }
            base.OnKeyDown(e);
        }

        public override bool Tick()
        {
            bool busy = base.Tick();
            if (_pick.Advance()) { Invalidate(); return true; }
            return busy;
        }

        protected override void Render(Graphics g, Rectangle r)
        {
            int y = r.Y + (r.Height - Dot) / 2;
            for (int i = 0; i < Palette.Accents.Length; i++)
            {
                Rectangle d = new Rectangle(r.X + i * Step, y, Dot, Dot);
                if (i == _hot && i != _index) d = Ui.Inset(d, -Theme.Px(1));
                using (SolidBrush b = new SolidBrush(Palette.Accents[i].Color))
                {
                    g.FillEllipse(b, d);
                }
                using (Pen p = new Pen(Palette.Alpha(Theme.OnSurface, 0.16)))
                {
                    g.DrawEllipse(p, d);
                }
            }
            if (_index < 0) { RingIfFocused(g, r, Radii.Pill); return; }

            Rectangle live = new Rectangle(r.X + (int)Math.Round(_pick.Value * Step), y, Dot, Dot);
            Rectangle ring = Ui.Inset(live, -Theme.Px(3));
            using (Pen p = new Pen(Theme.OnSurface, Math.Max(1.6f, Theme.Px(1.8))))
            {
                g.DrawEllipse(p, ring);
            }
            int tick = Theme.Px(11);
            Icons.Draw(g, Glyph.Check,
                new Rectangle(live.X + (Dot - tick) / 2, live.Y + (Dot - tick) / 2, tick, tick),
                A11y.ReadableOn(Palette.Accents[Math.Max(0, _index)].Color), Math.Max(1.5f, Theme.Px(1.8)));
            RingIfFocused(g, r, Radii.Pill);
        }
    }
}
