// The readout: a count set large, with two supporting figures.
//
// The phone build learned that a number set at display size carries more than a
// row reading "Total clips   14", and that it belongs second in the reading order
// rather than tenth. The desktop keeps that, laid across instead of down since a
// window has width the phone did not.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed class Figure
    {
        public string Value;
        public string Label;

        public Figure(string value, string label)
        {
            Value = value ?? "";
            Label = label ?? "";
        }
    }

    internal sealed class Readout : Control
    {
        private string _value = "";
        private string _label = "";
        private Figure[] _facts = new Figure[0];

        public Readout()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            TabStop = false;
            Height = Theme.Px(58);
        }

        public void Set(string value, string label, params Figure[] facts)
        {
            _value = value ?? "";
            _label = label ?? "";
            _facts = facts ?? new Figure[0];
            AccessibleName = _value + " " + _label;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Canvas);
            Ui.Q(g);
            if (_value.Length == 0) return;
            Size vs = Ui.Measure(_value, Theme.Display);
            int y = (Height - Theme.Px(40)) / 2;
            Ui.Line(g, _value, Theme.Display, Theme.AccentText,
                new Rectangle(0, y, vs.Width + Theme.Px(4), Theme.Display.Height + Theme.Px(4)));
            int x = vs.Width + Space.Sm;
            Ui.Line(g, _label, Theme.Small, Theme.Muted,
                new Rectangle(x, y + Theme.Px(8), Math.Max(0, Width - x), Theme.Small.Height + Theme.Px(4)));
            x += Ui.Measure(_label, Theme.Small).Width + Space.Xl;
            for (int i = 0; i < _facts.Length; i++)
            {
                if (_facts[i] == null) continue;
                Size fs = Ui.Measure(_facts[i].Value, Theme.BodyBold);
                if (x + fs.Width > Width) return;
                using (Pen p = new Pen(Theme.Hairline))
                {
                    g.DrawLine(p, x - Space.Md, y + Theme.Px(6), x - Space.Md, y + Theme.Px(30));
                }
                Ui.Line(g, _facts[i].Value, Theme.BodyBold, Theme.OnSurface,
                    new Rectangle(x, y + Theme.Px(4), fs.Width + Theme.Px(4), Theme.BodyBold.Height + Theme.Px(2)));
                Ui.Line(g, _facts[i].Label, Theme.Tiny, Theme.Faint,
                    new Rectangle(x, y + Theme.Px(4) + Theme.BodyBold.Height,
                        Math.Max(0, Width - x), Theme.Tiny.Height + Theme.Px(2)));
                x += Math.Max(fs.Width, Ui.Measure(_facts[i].Label, Theme.Tiny).Width) + Space.Xl;
            }
        }
    }
}
