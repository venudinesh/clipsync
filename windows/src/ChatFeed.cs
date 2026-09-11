// Chat: the transcript.
//
// The scroller is handed two closures and owns nothing else: one says how tall
// the turns are altogether, the other puts them where an offset says they go.
// Heights are measured once per reload and reused on every frame of a scroll,
// because measuring a markdown surface means asking Windows where a character
// landed and that is not a thing to do sixty times a second per turn.
//
// A turn outside the viewport is hidden rather than moved, so a conversation of a
// hundred messages costs the same to scroll as one of two.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class ChatView
    {
        private int Extent()
        {
            int w = _feed.Room;
            if (_high.Length != _turns.Count) _high = new int[_turns.Count];
            int h = Space.Sm;
            for (int i = 0; i < _turns.Count; i++)
            {
                _high[i] = _turns[i].Wants(w);
                h += _high[i] + Space.Lg;
            }
            return h + Space.Sm;
        }

        private void Place(int off)
        {
            int w = _feed.Room;
            int y = Space.Sm - off;
            int lo = -Theme.Px(60);
            int hi = _feed.Height + Theme.Px(60);
            for (int i = 0; i < _turns.Count; i++)
            {
                Turn t = _turns[i];
                int h = i < _high.Length ? _high[i] : t.Height;
                if (y + h > lo && y < hi)
                {
                    Rectangle want = new Rectangle(0, y, w, h);
                    if (t.Bounds != want) t.Bounds = want;
                    if (!t.Visible) t.Visible = true;
                }
                else if (t.Visible)
                {
                    t.Visible = false;
                }
                y += h + Space.Lg;
            }
        }

        /// Rebuilds the turns from the bubbles. Done on every send rather than
        /// patched, because a transcript is tens of items and one way of getting
        /// from the list to the screen is worth more than the saved controls.
        private void Fill()
        {
            for (int i = 0; i < _turns.Count; i++)
            {
                _feed.Controls.Remove(_turns[i]);
                _turns[i].Dispose();
            }
            _turns.Clear();
            for (int i = 0; i < _msgs.Count; i++) _turns.Add(Made(i));
            _high = new int[_turns.Count];
            Lay();
            _feed.Reload();
            Invalidate();
        }

        private Turn Made(int i)
        {
            Bubble b = _msgs[i];
            int k = i;
            Turn t = new Turn(b.Role == "user");
            t.Body = b.Text;
            t.Note = b.Note;
            t.Waiting = b.Streaming && b.Text.Length == 0;
            t.Foot = !t.Mine && !b.Streaming && i == _msgs.Count - 1;
            t.More.Click += delegate { Options(k); };
            t.Accepts(OnDragIn, OnDropped);
            if (!t.Mine)
            {
                t.Take.Click += delegate { Grab(k); };
                t.Retry.Click += delegate { Redo(k); };
            }
            _feed.Controls.Add(t);
            return t;
        }

        /// The one turn that changes without a rebuild. A reply arriving token by
        /// token would otherwise throw away and remake every control on the page
        /// a dozen times a second.
        private void Grew(int i)
        {
            if (i < 0 || i >= _turns.Count || i >= _msgs.Count) return;
            bool tail = _feed.AtEnd;
            Turn t = _turns[i];
            t.Waiting = _msgs[i].Text.Length == 0;
            t.Body = _msgs[i].Text;
            _feed.Reload();
            if (tail) _feed.End(true);
        }
    }
}
