// Containers.
//
// A card is the surface everything else sits on: one fill, one hairline, one
// radius, and a padding rule its children are laid out against. A page head is
// the title block at the top of every view. Neither reacts to the pointer, which
// is why they are plain controls rather than widgets.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal class Card : Control
    {
        public int Radius = Radii.Card;
        public int Inset = Space.Lg;
        public bool Bordered = true;
        public bool Elevated;
        public bool Accented;

        /// Overrides the surface colour. Left transparent to mean "use the
        /// theme's card surface", so a card follows the theme by default.
        public Color Surface = Color.Empty;

        public Card()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            TabStop = false;
        }

        public Color Skin
        {
            get { return Surface == Color.Empty ? Theme.Base : Surface; }
        }

        /// The rectangle a child may occupy.
        public Rectangle Inner
        {
            get
            {
                return new Rectangle(Inset, Inset,
                    Math.Max(0, Width - Inset * 2), Math.Max(0, Height - Inset * 2));
            }
        }

        protected Color Behind
        {
            get { return Parent != null ? Parent.BackColor : Theme.Canvas; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Behind);
            Ui.Q(g);
            Rectangle r = ClientRectangle;
            if (Elevated) Ui.Shadow(g, Ui.Inset(r, 1), Radius, Theme.Px(6), Color.Black, Theme.Dark ? 120 : 60);
            Ui.Fill(g, r, Radius, Skin);
            if (Accented)
            {
                Ui.Stroke(g, r, Radius, Palette.Alpha(Theme.Accent, 0.45), 1.4f);
            }
            else if (Bordered)
            {
                Ui.Stroke(g, r, Radius, Theme.Outline, 1f);
            }
            Paint2(g, r);
        }

        /// Extra painting for a subclass, after the surface is down and before
        /// children are drawn over it.
        protected virtual void Paint2(Graphics g, Rectangle r) { }
    }

    /// A group name with a rule running off it, and room for one icon action at
    /// the end. The count that used to sit here lives in the readout, so it is
    /// not printed twice.
    internal sealed class SectionHead : Control
    {
        private string _label = "";

        public SectionHead()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            TabStop = false;
            Height = Theme.Px(30);
        }

        public string Label
        {
            get { return _label; }
            set { _label = value ?? ""; AccessibleName = _label; Invalidate(); }
        }

        /// Pixels at the end of the rule left clear for a trailing control.
        public int Reserve;

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Canvas);
            Ui.Q(g);
            Size s = Ui.Measure(_label, Theme.Heading);
            Ui.Line(g, _label, Theme.Heading, Theme.OnSurface,
                new Rectangle(0, 0, s.Width + Theme.Px(4), Height));
            int x = s.Width + Space.Md;
            int right = Math.Max(x, Width - Reserve);
            if (right <= x) return;
            using (Pen p = new Pen(Theme.Hairline))
            {
                int y = Height / 2;
                g.DrawLine(p, x, y, right, y);
            }
        }
    }

    /// The title block at the top of a view: a greeting in the accent above the
    /// destination's name, with the timely line under it. Trailing controls are
    /// positioned by the view itself; this draws the words and reserves height.
    internal sealed class PageHead : Control
    {
        private string _eyebrow = "";
        private string _title = "";
        private string _subtitle = "";

        public PageHead()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            TabStop = false;
            Height = Theme.Px(62);
        }

        public string Eyebrow
        {
            get { return _eyebrow; }
            set { _eyebrow = value ?? ""; Invalidate(); }
        }

        public string Title
        {
            get { return _title; }
            set { _title = value ?? ""; AccessibleName = _title; Invalidate(); }
        }

        public string Subtitle
        {
            get { return _subtitle; }
            set { _subtitle = value ?? ""; Invalidate(); }
        }

        /// Width the words take, so a view can keep its buttons clear of them.
        public int TitleWidth
        {
            get { return Math.Max(Ui.Measure(_title, Theme.Display).Width,
                                  Ui.Measure(_subtitle, Theme.Small).Width); }
        }

        /// The three rows the block is made of. Both the height it asks for and
        /// the height it paints are added up from these, so the two cannot drift
        /// apart and clip the title. A row with no words takes none, which is what
        /// keeps the same block right on a page that has only a name.
        private int Brow { get { return _eyebrow.Length > 0 ? Theme.Small.Height + Theme.Px(3) : 0; } }

        private int Big { get { return Theme.Display.Height + Theme.Px(4); } }

        private int Under { get { return _subtitle.Length > 0 ? Theme.Small.Height + Theme.Px(3) : 0; } }

        public int Wants()
        {
            return Brow + Big + Under + Theme.Px(2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Canvas);
            Ui.Q(g);
            int y = Math.Max(0, (Height - Wants()) / 2);
            if (Brow > 0)
            {
                Ui.Line(g, _eyebrow, Theme.Small, Theme.AccentText, new Rectangle(0, y, Width, Brow));
                y += Brow;
            }
            Ui.Line(g, _title, Theme.Display, Theme.OnSurface, new Rectangle(0, y, Width, Big));
            y += Big;
            if (Under == 0) return;
            Ui.Line(g, _subtitle, Theme.Small, Theme.Muted, new Rectangle(0, y, Width, Under));
        }
    }
}
