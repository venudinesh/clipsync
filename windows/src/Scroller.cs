// The scrolling surface that carries controls.
//
// The ledger paints its rows, which is right for a list of clips and wrong for a
// conversation: a reply is a rich text box, so its words can be selected and its
// markdown can carry weights, bullets and a code run. Those are real controls, so
// a transcript needs something that moves children rather than a viewport that
// draws rows.
//
// It draws the same four pixel thumb the ledger draws and eases the same way, so
// the two scroll alike even though only one of them owns what it shows.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class Scroller : Control, IAnimated
    {
        /// The height the children need in total. Asked for on every reload.
        public Func<int> Extent;

        /// Puts the children where the offset it is handed says they go.
        public Action<int> Arrange;

        private int _total;
        private double _top;
        private double _want;
        private bool _dragging;
        private int _dragGrab;

        public Scroller()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            TabStop = false;
            Ticker.Join(this);
        }

        public int Offset { get { return (int)Math.Round(_top); } }
        private int Gutter { get { return Theme.Px(8); } }
        private double Max { get { return Math.Max(0, _total - Height); } }
        private bool Scrolls { get { return _total > Height; } }

        /// The width a child may take, which is everything but the thumb's lane.
        public int Room { get { return Math.Max(10, Width - Gutter); } }

        /// True when the view is already at the end of the content. It is what
        /// tells a streaming reply whether to keep the newest line in sight or
        /// leave a reader who scrolled back where they put themselves.
        public bool AtEnd { get { return _want >= Max - Theme.Px(8); } }

        public void Reload()
        {
            _total = Extent != null ? Math.Max(0, Extent()) : 0;
            Clamp();
            Place();
            Invalidate();
        }

        private void Clamp()
        {
            double max = Max;
            if (_want > max) _want = max;
            if (_want < 0) _want = 0;
            if (_top > max) _top = max;
            if (_top < 0) _top = 0;
        }

        private void Place()
        {
            if (Arrange == null) return;
            SuspendLayout();
            Arrange(Offset);
            ResumeLayout(false);
        }

        public void End(bool now)
        {
            _want = Max;
            if (now || Theme.ReduceMotion) { _top = _want; Place(); Invalidate(); }
            else Ticker.Poke();
        }

        public void Home()
        {
            _want = 0;
            _top = 0;
            Place();
            Invalidate();
        }

        /// Taken from outside as well as from the pointer, because a rich text
        /// box eats the wheel message and has to hand it back to get here.
        public void Wheel(int delta)
        {
            if (!Scrolls) return;
            _want -= (double)delta / 120.0 * Theme.Px(72);
            Clamp();
            if (Theme.ReduceMotion) { _top = _want; Place(); Invalidate(); }
            else Ticker.Poke();
        }

        public void Page(int dir)
        {
            _want += dir * Math.Max(Theme.Px(72), Height - Space.Xl);
            Clamp();
            if (Theme.ReduceMotion) { _top = _want; Place(); Invalidate(); }
            else Ticker.Poke();
        }

        /// The scroller a control is sitting inside, or null when it is not in
        /// one. Lets a child hand back input it cannot use itself.
        public static Scroller Of(Control c)
        {
            Control p = c;
            while (p != null)
            {
                Scroller s = p as Scroller;
                if (s != null) return s;
                p = p.Parent;
            }
            return null;
        }

        public bool Tick()
        {
            double gap = _want - _top;
            if (Math.Abs(gap) < 0.5)
            {
                if (_top != _want) { _top = _want; Place(); Invalidate(); }
                return false;
            }
            _top += gap * (Theme.ReduceMotion ? 1.0 : 0.24);
            Place();
            Invalidate();
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Ticker.Leave(this);
            base.Dispose(disposing);
        }
    }
}
