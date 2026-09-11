// The ledger: one scrolling list, used by clips, notes and chat.
//
// Rows are drawn by whoever owns the data, so the list itself knows nothing about
// clips or notes. It owns scrolling, hover, selection, the keyboard, and the
// scrollbar; the view owns what a row looks like. Row heights are asked for once
// per reload and cached as running totals, so hit testing and painting a list of
// a few thousand clips stay a binary search rather than a walk.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class Ledger : Control, IAnimated
    {
        public Func<int> Count;
        public Func<int, int> HeightOf;
        public Action<Graphics, Rectangle, int, bool, bool> PaintRow;

        /// Rows that are furniture rather than entries: a section heading printed
        /// inside the list. The pointer and the keyboard both step over them, so
        /// nothing can select a heading and then press Enter on it.
        public Func<int, bool> Skippable;

        public event Action<int> Activated;
        public event Action<int, Point> Clicked;

        public string EmptyTitle = "";
        public string EmptyBody = "";
        public Glyph EmptyGlyph = Glyph.Clips;

        private int[] _tops = new int[1];
        private int _total;
        private double _top;
        private double _want;
        private int _hot = -1;
        private int _live = -1;
        private bool _dragging;
        private int _dragGrab;

        public Ledger()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
            Ticker.Join(this);
        }

        public int Selected
        {
            get { return _live; }
            set
            {
                int n = Rows();
                int v = n == 0 ? -1 : (value < 0 ? 0 : (value >= n ? n - 1 : value));
                if (v == _live) return;
                _live = v;
                if (v >= 0) Reveal(v);
                Invalidate();
            }
        }

        private int Rows() { return Count != null ? Math.Max(0, Count()) : 0; }

        private bool Skip(int i)
        {
            return Skippable != null && Skippable(i);
        }

        /// The next row the keyboard should land on, walking past the furniture.
        /// Returns where it started when there is nothing further in that
        /// direction, so Up on the first entry stays put rather than wrapping.
        private int Step(int from, int dir)
        {
            int n = Rows();
            int i = from;
            for (int guard = 0; guard < n + 1; guard++)
            {
                i += dir;
                if (i < 0 || i >= n) return from < 0 || from >= n ? -1 : from;
                if (!Skip(i)) return i;
            }
            return from;
        }

        /// Recomputes row offsets. Called whenever the data or the width changes.
        public void Reload()
        {
            int n = Rows();
            _tops = new int[n + 1];
            int y = 0;
            for (int i = 0; i < n; i++)
            {
                _tops[i] = y;
                y += HeightOf != null ? Math.Max(1, HeightOf(i)) : Theme.Px(64);
            }
            _tops[n] = y;
            _total = y;
            if (_live >= n) _live = n - 1;
            Clamp();
            Invalidate();
        }

        private void Clamp()
        {
            double max = Math.Max(0, _total - Height + Space.Md);
            if (_want > max) _want = max;
            if (_want < 0) _want = 0;
            if (_top > max) _top = max;
            if (_top < 0) _top = 0;
        }

        public void Reveal(int index)
        {
            if (index < 0 || index >= Rows()) return;
            int rowTop = _tops[index];
            int rowBottom = _tops[index + 1];
            if (rowTop < _want) _want = rowTop - Space.Sm;
            else if (rowBottom > _want + Height) _want = rowBottom - Height + Space.Sm;
            Clamp();
            if (Theme.ReduceMotion) _top = _want;
            Ticker.Poke();
        }

        public void Home()
        {
            _want = 0;
            _top = 0;
            Invalidate();
        }

        private int IndexAt(int y)
        {
            int n = Rows();
            if (n == 0) return -1;
            int target = y + (int)Math.Round(_top);
            int lo = 0, hi = n - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (target < _tops[mid]) hi = mid - 1;
                else if (target >= _tops[mid + 1]) lo = mid + 1;
                else return mid;
            }
            return -1;
        }

        public Rectangle RowBounds(int index)
        {
            if (index < 0 || index >= Rows()) return Rectangle.Empty;
            int y = _tops[index] - (int)Math.Round(_top);
            return new Rectangle(0, y, Width - Theme.Px(8), _tops[index + 1] - _tops[index]);
        }

        public bool Tick()
        {
            double gap = _want - _top;
            if (Math.Abs(gap) < 0.5)
            {
                if (_top != _want) { _top = _want; Invalidate(); }
                return false;
            }
            _top += gap * (Theme.ReduceMotion ? 1.0 : 0.22);
            Invalidate();
            return true;
        }
    }
}
