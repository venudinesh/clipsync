// The scrollbar for a native text box.
//
// Windows draws its own in the system colours, and they can't be changed on 7
// or 8. A light grey bar inside a dark card looks like a bug, so the edit
// control draws none and this draws the same four-pixel thumb as the feed and
// the ledger, in the same two shades.
//
// It keeps no record of the text: position and range come from the control
// through two messages it has answered since Windows 3. Wrapping, the caret,
// page keys and drag-selection all keep working.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed class Lane
    {
        private readonly Control _host;
        private readonly TextBoxBase _text;
        private bool _drag;
        private int _from;
        private int _top;

        public Lane(Control host, TextBoxBase text)
        {
            _host = host;
            _text = text;
        }

        /// The room kept clear on the right for the thumb. Reserved whether or
        /// not there is anything to scroll, so the words do not reflow the moment
        /// the text grows past the bottom of the box.
        public static int Gutter
        {
            get { return Theme.Px(8); }
        }

        /// Lines of text, counting a wrapped line as a line, and lines that fit
        /// in the box. Both in the control's own units, because the message that
        /// scrolls it counts in those.
        private int Rows
        {
            get
            {
                if (!_text.IsHandleCreated) return 1;
                return Math.Max(1, _text.GetLineFromCharIndex(Math.Max(0, _text.TextLength - 1)) + 1);
            }
        }

        private int Seen
        {
            get { return Math.Max(1, _text.ClientSize.Height / Math.Max(1, _text.Font.Height)); }
        }

        private int Reach
        {
            get { return Math.Max(0, Rows - Seen); }
        }

        public bool Shows
        {
            get { return _text.Multiline && _text.IsHandleCreated && Reach > 0; }
        }

        private int At
        {
            get { return Math.Min(Reach, Math.Max(0, Native.FirstVisibleLine(_text.Handle))); }
        }

        /// The thumb, in the host's coordinates. Empty when there is nothing to
        /// scroll, or too little height to say anything useful about it.
        public Rectangle Thumb()
        {
            if (!Shows) return Rectangle.Empty;
            int track = _host.Height - Space.Sm * 2;
            int least = Theme.Px(24);
            if (track < least) return Rectangle.Empty;
            int h = Math.Max(least, track * Seen / Math.Max(1, Rows));
            int y = Space.Sm + (track - h) * At / Math.Max(1, Reach);
            return new Rectangle(_host.Width - Gutter, y, Theme.Px(4), h);
        }

        public void Paint(Graphics g)
        {
            Rectangle t = Thumb();
            if (t == Rectangle.Empty) return;
            Ui.Fill(g, t, Theme.Px(2), Palette.Alpha(Theme.OnSurface, _drag ? 0.34 : 0.18));
        }

        /// True when the press belonged to the thumb, so the host leaves it be
        /// rather than putting the caret where it landed.
        public bool Down(MouseEventArgs e)
        {
            Rectangle t = Thumb();
            if (t == Rectangle.Empty || e.X < _host.Width - Gutter - Theme.Px(4)) return false;
            if (e.Y >= t.Y && e.Y < t.Bottom)
            {
                _drag = true;
                _from = e.Y;
                _top = At;
            }
            else Move(e.Y < t.Y ? -Seen : Seen);
            _host.Invalidate();
            return true;
        }

        public bool Drag(MouseEventArgs e)
        {
            if (!_drag) return false;
            int track = Math.Max(1, _host.Height - Space.Sm * 2 - Thumb().Height);
            Move(_top + (e.Y - _from) * Reach / track - At);
            _host.Invalidate();
            return true;
        }

        public void Up()
        {
            if (!_drag) return;
            _drag = false;
            _host.Invalidate();
        }

        /// A wheel notch, in the three lines Windows moves a list by.
        public void Wheel(int delta)
        {
            int notches = delta / 120;
            if (notches != 0 && Shows) Move(-notches * 3);
        }

        private void Move(int lines)
        {
            if (lines == 0) return;
            Native.ScrollLines(_text.Handle, lines);
            _host.Invalidate();
        }
    }
}
