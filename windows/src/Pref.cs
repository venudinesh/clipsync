// One line of settings.
//
// A name, a sentence under it saying what the switch actually does, and one
// control at the end. The sentence is the point: a toggle labelled "Skip
// sensitive" tells nobody anything, and a settings page that needs a manual
// beside it is a broken settings page.
//
// A row that ends in a switch keeps it on the right. A row that ends in a text
// box puts it underneath, because a URL needs the width and a label with forty
// pixels of box beside it is worse than two lines.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed class Pref : Control
    {
        private string _label = "";
        private string _detail = "";
        private Control _trail;

        public Pref()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            TabStop = false;
        }

        /// Puts the trailing control on its own line under the words.
        public bool Below;

        /// Greys the words and turns the control off, for a setting that has no
        /// meaning yet. The row stays where it is rather than vanishing, so the
        /// page does not reflow every time something else is switched.
        public bool Moot
        {
            get { return _moot; }
            set
            {
                if (_moot == value) return;
                _moot = value;
                if (_trail != null) _trail.Enabled = !value;
                Invalidate();
            }
        }

        private bool _moot;

        public string Label
        {
            get { return _label; }
            set { _label = value ?? ""; AccessibleName = _label; Invalidate(); }
        }

        public string Detail
        {
            get { return _detail; }
            set { _detail = value ?? ""; AccessibleDescription = _detail; Invalidate(); }
        }

        /// The control at the end. Held as a child so a page is a list of rows
        /// rather than a list of rows and the controls that belong to them.
        public Control Trail
        {
            get { return _trail; }
            set
            {
                if (_trail == value) return;
                if (_trail != null) Controls.Remove(_trail);
                _trail = value;
                if (_trail == null) return;
                _trail.Enabled = !_moot;
                Controls.Add(_trail);
            }
        }

        private int Words(int width)
        {
            int h = Theme.Body.Height + Theme.Px(2);
            if (_detail.Length > 0)
            {
                h += Math.Max(Theme.Small.Height,
                    Ui.MeasureWrapped(_detail, Theme.Small, Math.Max(Theme.Px(60), width)).Height) + Theme.Px(2);
            }
            return h;
        }

        public int Wants(int width)
        {
            int pad = Theme.Px(Theme.Dense ? 7 : 10);
            int tw = _trail == null ? 0 : _trail.Width;
            int text = Math.Max(Theme.Px(80), width - (_trail == null || Below ? 0 : tw + Space.Lg));
            int words = Words(text);
            if (_trail != null && Below) return words + Space.Sm + _trail.Height + pad * 2;
            return Math.Max(words, _trail == null ? 0 : _trail.Height) + pad * 2;
        }

        public void Lay()
        {
            if (_trail == null) return;
            int pad = Theme.Px(Theme.Dense ? 7 : 10);
            if (Below)
            {
                int words = Words(Math.Max(Theme.Px(80), Width));
                _trail.SetBounds(0, pad + words + Space.Sm, Width, _trail.Height);
                return;
            }
            _trail.SetBounds(Math.Max(0, Width - _trail.Width), Math.Max(pad, (Height - _trail.Height) / 2),
                _trail.Width, _trail.Height);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Lay();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Base);
            Ui.Q(g);
            int pad = Theme.Px(Theme.Dense ? 7 : 10);
            int tw = _trail == null || Below ? Width : Math.Max(Theme.Px(80), Width - _trail.Width - Space.Lg);
            int lh = Theme.Body.Height + Theme.Px(2);
            Color ink = _moot ? Theme.Faint : Theme.OnSurface;
            Ui.Line(g, _label, Theme.Body, ink, new Rectangle(0, pad, tw, lh));
            if (_detail.Length == 0) return;
            int dh = Math.Max(Theme.Small.Height,
                Ui.MeasureWrapped(_detail, Theme.Small, tw).Height);
            Ui.Wrap(g, _detail, Theme.Small, _moot ? Theme.Faint : Theme.Muted,
                new Rectangle(0, pad + lh, tw, dh + Theme.Px(2)));
        }
    }
}
