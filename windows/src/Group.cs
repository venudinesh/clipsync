// A group of settings on one card.
//
// The card is the grouping, so the page does not need a rule between every line
// and a heading floating over nothing. Rows are separated by a hairline that stops
// short of the padding, which is enough to say where one setting ends without
// drawing a table.
//
// The group measures itself from its rows, so adding a setting anywhere costs one
// line at the call site and no arithmetic.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed class Group : Card
    {
        private readonly List<Control> _rows = new List<Control>();
        private string _title = "";

        public Group(string title)
        {
            _title = title ?? "";
            Inset = Space.Lg;
            AccessibleRole = AccessibleRole.Grouping;
            AccessibleName = _title;
        }

        public Group Add(Control row)
        {
            if (row == null) return this;
            _rows.Add(row);
            Controls.Add(row);
            return this;
        }

        public Control[] Rows { get { return _rows.ToArray(); } }

        private int Head
        {
            get { return _title.Length == 0 ? 0 : Theme.Heading.Height + Space.Sm; }
        }

        private static int Asked(Control c, int width)
        {
            Pref p = c as Pref;
            return p != null ? p.Wants(width) : Math.Max(Theme.Px(24), c.Height);
        }

        public int Wants(int width)
        {
            int inner = Math.Max(Theme.Px(80), width - Inset * 2);
            int h = Inset * 2 + Head;
            for (int i = 0; i < _rows.Count; i++)
            {
                if (!_rows[i].Visible) continue;
                h += Asked(_rows[i], inner);
            }
            return h;
        }

        public void Lay()
        {
            Rectangle box = Inner;
            int y = box.Y + Head;
            for (int i = 0; i < _rows.Count; i++)
            {
                Control c = _rows[i];
                if (!c.Visible) continue;
                int h = Asked(c, box.Width);
                c.SetBounds(box.X, y, box.Width, h);
                Pref p = c as Pref;
                if (p != null) p.Lay();
                y += h;
            }
        }

        protected override void Paint2(Graphics g, Rectangle r)
        {
            Rectangle box = Inner;
            if (_title.Length > 0)
            {
                Ui.Line(g, _title, Theme.Heading, Theme.OnSurface,
                    new Rectangle(box.X, box.Y, box.Width, Theme.Heading.Height + Theme.Px(2)));
            }
            using (Pen pen = new Pen(Theme.Hairline))
            {
                bool first = true;
                for (int i = 0; i < _rows.Count; i++)
                {
                    Control c = _rows[i];
                    if (!c.Visible) continue;
                    if (!first) g.DrawLine(pen, box.X, c.Top, box.Right, c.Top);
                    first = false;
                }
            }
        }
    }
}
