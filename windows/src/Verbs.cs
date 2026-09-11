// Verb lists.
//
// Six verbs behind a nineteen pixel glyph leaves no room to say what any of them
// does, which is why the phone build put a clip's actions in a sheet with words
// and a line of detail underneath. The desktop keeps that: a stacked list of
// named actions, each one a real focusable row rather than a menu item.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed class VerbRow : Widget
    {
        public Glyph Icon = Glyph.Check;
        public string Label = "";
        public string Detail = "";
        public bool Danger;

        public VerbRow()
        {
            Height = Theme.Px(44);
        }

        public int Wants()
        {
            return Theme.Px(Detail.Length > 0 ? 54 : 44);
        }

        protected override void Render(Graphics g, Rectangle r)
        {
            double h = Hover.Value;
            Color ink = Danger ? A11y.LegibleAccent(Palette.Danger, Theme.Base, 4.5) : Theme.OnSurface;
            if (h > 0.01)
            {
                Color wash = Danger ? Palette.Alpha(Palette.Danger, 0.10 * h)
                                    : Palette.Alpha(Theme.OnSurface, 0.06 * h);
                Ui.Fill(g, r, Radii.Control, wash);
            }
            RingIfFocused(g, r, Radii.Control);
            int d = Theme.Px(18);
            Rectangle ir = new Rectangle(r.X + Space.Md, r.Y + (r.Height - d) / 2, d, d);
            Icons.Draw(g, Icon, ir, Danger ? ink : Theme.AccentText, Math.Max(1.4f, Theme.Px(1.6)));
            int x = ir.Right + Space.Md;
            int w = Math.Max(10, r.Right - x - Space.Md);
            if (Detail.Length == 0)
            {
                Ui.Line(g, Label, Theme.Body, ink, new Rectangle(x, r.Y, w, r.Height));
                return;
            }
            int lh = Theme.Body.Height + Theme.Px(2);
            int y = r.Y + (r.Height - lh - Theme.Tiny.Height) / 2;
            Ui.Line(g, Label, Theme.Body, ink, new Rectangle(x, y, w, lh));
            Ui.Line(g, Detail, Theme.Tiny, Theme.Faint,
                new Rectangle(x, y + lh, w, Theme.Tiny.Height + Theme.Px(2)));
        }
    }

    /// A stack of verb rows, sized to its contents. Handed to a sheet as its
    /// body, so the sheet stays ignorant of what the verbs do.
    internal sealed class Verbs : Control
    {
        private readonly List<VerbRow> _rows = new List<VerbRow>();

        public Verbs()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            TabStop = false;
            BackColor = Theme.Base;
        }

        public VerbRow Add(Glyph icon, string label, string detail, bool danger, EventHandler act)
        {
            VerbRow row = new VerbRow();
            row.Icon = icon;
            row.Label = label ?? "";
            row.Detail = detail ?? "";
            row.Danger = danger;
            row.AccessibleName = row.Label;
            row.AccessibleDescription = row.Detail;
            if (act != null) row.Click += act;
            _rows.Add(row);
            Controls.Add(row);
            return row;
        }

        public VerbRow Add(Glyph icon, string label, EventHandler act)
        {
            return Add(icon, label, "", false, act);
        }

        public int Wants()
        {
            int h = Space.Xs;
            for (int i = 0; i < _rows.Count; i++) h += _rows[i].Wants();
            return h + Space.Xs;
        }

        public void Lay()
        {
            int y = Space.Xs;
            for (int i = 0; i < _rows.Count; i++)
            {
                int h = _rows[i].Wants();
                _rows[i].SetBounds(0, y, Math.Max(10, Width), h);
                y += h;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Lay();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
        }
    }
}
