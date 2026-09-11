// One turn in a conversation.
//
// A reply takes the full measure under a quiet label, because it is the thing
// being read and markdown needs the width. What you asked sits in a soft plate on
// the right, narrower than the reply, so the two are told apart at a glance
// without a name printed over either of them.
//
// A reply is a real markdown surface rather than painted text: its words can be
// selected, Ctrl+C works on them, and a screen reader can read them. The lane down
// the right hand side is kept clear the whole way, which is where the one button
// every turn has lives, so those buttons line up instead of chasing the text.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class Turn : Control
    {
        public readonly IconButton More = new IconButton();
        public readonly IconButton Take = new IconButton();
        public readonly IconButton Retry = new IconButton();

        private readonly MarkdownBox _md;
        private readonly bool _mine;
        private Rectangle _plate;
        private string _body = "";
        private string _note = "";
        private bool _foot;
        private bool _waiting;

        public Turn(bool mine)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            TabStop = false;
            _mine = mine;
            More.Icon = Glyph.More;
            More.Tip = mine ? "Your message" : "This reply";
            Controls.Add(More);
            if (mine) return;
            _md = new MarkdownBox();
            _md.AutoHeight = true;
            _md.Pad = Space.Xs;
            Controls.Add(_md);
            Take.Icon = Glyph.Copy;
            Take.Tip = "Copy message";
            Retry.Icon = Glyph.Refresh;
            Retry.Tip = "Ask again";
            Controls.Add(Take);
            Controls.Add(Retry);
        }

        public bool Mine { get { return _mine; } }

        /// Takes a drop anywhere on the turn, the text surface included. A file
        /// dragged onto the conversation should attach wherever it lands, and the
        /// words of a reply cover most of the page once a chat is going.
        public void Accepts(DragEventHandler over, DragEventHandler drop)
        {
            Wire(this, over, drop);
            if (_md == null) return;
            Wire(_md, over, drop);
            Wire(_md.Box, over, drop);
        }

        private static void Wire(Control c, DragEventHandler over, DragEventHandler drop)
        {
            if (c == null) return;
            c.AllowDrop = true;
            c.DragEnter += over;
            c.DragOver += over;
            c.DragDrop += drop;
        }


        public string Body
        {
            get { return _body; }
            set
            {
                _body = value ?? "";
                AccessibleName = _body;
                if (_md != null) _md.Source = _body;
                Invalidate();
            }
        }

        /// A line under the words naming what was attached to the turn.
        public string Note
        {
            get { return _note; }
            set { _note = value ?? ""; Invalidate(); }
        }

        /// The footer of quick actions, given to the newest reply only, because
        /// three buttons under every reply in a long chat is a wall of buttons.
        public bool Foot
        {
            get { return _foot; }
            set { _foot = value; }
        }

        /// A reply with nothing in it yet. Three quiet bars say the model was
        /// asked and has not answered, which a blank gap does not.
        public bool Waiting
        {
            get { return _waiting; }
            set { _waiting = value; Invalidate(); }
        }

        private int Gut { get { return Theme.Px(34); } }
        private int Head { get { return _mine ? 0 : Theme.Tiny.Height + Theme.Px(3); } }

        public int Wants(int width)
        {
            int w = Math.Max(Theme.Px(120), width);
            if (_mine)
            {
                int pw = Plate(w);
                int h = Space.Md * 2 + Math.Max(Theme.Body.Height,
                    Ui.MeasureWrapped(_body, Theme.Body, pw - Space.Md * 2).Height);
                if (_note.Length > 0) h += Theme.Tiny.Height + Theme.Px(3);
                return h;
            }
            if (_waiting) return Head + Theme.Px(54);
            int tw = Math.Max(Theme.Px(80), w - Gut);
            if (_md.Width != tw) _md.Width = tw;
            return Head + _md.Wants() + (_foot ? Theme.Px(32) : 0);
        }

        /// How wide the plate behind a question is: the text's own width when it
        /// fits on one line, the whole column when it does not, so a three word
        /// question is not stretched across the window.
        private int Plate(int width)
        {
            int room = Math.Max(Theme.Px(120), width - Gut - Theme.Px(64));
            Size s = Ui.MeasureWrapped(_body, Theme.Body, room - Space.Md * 2);
            int one = s.Width + Space.Md * 2 + Theme.Px(2);
            if (s.Height <= Theme.Body.Height + Theme.Px(2) && one < room)
            {
                return Math.Max(Theme.Px(96), one);
            }
            return room;
        }
    }
}
