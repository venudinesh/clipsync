// A row of filters.
//
// The phone scrolls its tag rail sideways with a thumb. A desktop window is wide
// and has no thumb, so the same set of filters wraps instead: every tag is one
// click away rather than one swipe and a guess about what is off the edge.
//
// When there are more tags than three rows can hold, the last pill becomes the
// way into the rest. If the chosen tag is one of the hidden ones, that pill wears
// its name and its highlight, so the strip never shows a filter as off while it
// is on.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed class Tabs : Widget
    {
        private string[] _items = new string[0];
        private Rectangle[] _cells = new Rectangle[0];
        private int _index;
        private int _hot = -1;
        private int _shown;
        private bool _more;

        public int MaxRows = 3;
        public string MoreLabel = "More";

        public event EventHandler Changed;
        public event EventHandler Overflowed;

        public Tabs()
        {
            Height = Theme.Px(30);
        }

        public void SetItems(string[] items, int index)
        {
            _items = items != null ? items : new string[0];
            _index = _items.Length == 0 ? 0 : Math.Max(0, Math.Min(_items.Length - 1, index));
            Build(Width);
            Invalidate();
        }

        public int Index
        {
            get { return _index; }
            set
            {
                int v = _items.Length == 0 ? 0 : Math.Max(0, Math.Min(_items.Length - 1, value));
                if (v == _index) return;
                _index = v;
                Invalidate();
            }
        }

        public int Count { get { return _items.Length; } }

        /// The height this strip needs at the width it is about to be given.
        /// Layout calls this before setting bounds, and it doubles as the build
        /// step: the rectangles it works out are the ones painting and hit
        /// testing then use, so the three can never disagree.
        public int Wants(int width)
        {
            return Build(width);
        }

        private static int PillWidth(string label)
        {
            return Ui.Measure(label, Theme.Small).Width + Space.Md * 2;
        }

        private int Build(int width)
        {
            int h = Theme.Px(30);
            int gap = Space.Sm;
            int room = Math.Max(PillWidth("aaaa"), width);
            List<Rectangle> cells = new List<Rectangle>();
            int x = 0, y = 0, rows = 1;
            _shown = 0;
            _more = false;
            for (int i = 0; i < _items.Length; i++)
            {
                int w = PillWidth(_items[i]);
                if (x > 0 && x + w > room)
                {
                    if (rows >= MaxRows) { _more = true; break; }
                    rows++;
                    x = 0;
                    y += h + gap;
                }
                cells.Add(new Rectangle(x, y, w, h));
                x += w + gap;
                _shown++;
            }
            if (_more)
            {
                int w = PillWidth(MoreText);
                while (cells.Count > 1 && x + w > room)
                {
                    x = cells[cells.Count - 1].X;
                    cells.RemoveAt(cells.Count - 1);
                    _shown--;
                }
                cells.Add(new Rectangle(x, y, w, h));
            }
            _cells = cells.ToArray();
            return rows * h + (rows - 1) * gap;
        }

        /// What the overflow pill says: the chosen tag when it is one of the ones
        /// that did not fit, otherwise the plain invitation.
        private string MoreText
        {
            get { return _index >= _shown && _index < _items.Length ? _items[_index] : MoreLabel; }
        }

        private bool IsMore(int cell) { return _more && cell == _cells.Length - 1; }

        private int CellAt(Point p)
        {
            for (int i = 0; i < _cells.Length; i++)
            {
                if (_cells[i].Contains(p)) return i;
            }
            return -1;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Build(Width);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int was = _hot;
            _hot = CellAt(e.Location);
            if (was != _hot) Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hot == -1) return;
            _hot = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Choose(CellAt(e.Location));
        }

        private void Choose(int cell)
        {
            if (cell < 0) return;
            if (IsMore(cell))
            {
                if (Overflowed != null) Overflowed(this, EventArgs.Empty);
                return;
            }
            if (cell == _index) return;
            _index = cell;
            Build(Width);
            Invalidate();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Left || keyData == Keys.Right) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right)
            {
                int step = e.KeyCode == Keys.Left ? -1 : 1;
                int next = _index + step;
                if (next >= 0 && next < _items.Length)
                {
                    _index = next;
                    Build(Width);
                    Invalidate();
                    if (Changed != null) Changed(this, EventArgs.Empty);
                }
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }

        /// Space or Enter on the strip opens the overflow list when the chosen
        /// filter lives in it, since that is the only thing left to do here.
        protected override void Activate()
        {
            if (_more && _index >= _shown && Overflowed != null) Overflowed(this, EventArgs.Empty);
        }

        protected override void Render(Graphics g, Rectangle r)
        {
            for (int i = 0; i < _cells.Length; i++)
            {
                Rectangle c = _cells[i];
                bool more = IsMore(i);
                bool on = more ? _index >= _shown : i == _index;
                string label = more ? MoreText : _items[i];
                if (on)
                {
                    Ui.Fill(g, c, Radii.Pill, Palette.Alpha(Theme.Accent, 0.16));
                    Ui.Stroke(g, c, Radii.Pill, Palette.Alpha(Theme.Accent, 0.42), 1f);
                }
                else
                {
                    if (i == _hot) Ui.Fill(g, c, Radii.Pill, Palette.Alpha(Theme.OnSurface, 0.05));
                    Ui.Stroke(g, c, Radii.Pill, Theme.Hairline, 1f);
                }
                Ui.Centre(g, label, Theme.Small, on ? Theme.AccentText : Theme.Muted, c);
            }
            int lit = _more && _index >= _shown ? _cells.Length - 1 : _index;
            if (lit >= 0 && lit < _cells.Length) RingIfFocused(g, _cells[lit], Radii.Pill);
        }
    }
}
