// Notes: the editor's layout and the line that reports on it.
//
// One column. The back, pin and menu buttons sit in a toolbar above it, so the
// title, the tags and the body all start on the same vertical line as the page
// margin everywhere else in the app, and the body is not indented by a button
// that happens to be beside the title.
//
// The four model actions and the status line share a row directly over the
// writing surface: verbs on the left, what the note is on the right. It is the
// one place both belong, and it saves a row of chrome over stacking them.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class NotesView
    {
        private Rectangle _tagRect;

        private void LayEdit()
        {
            int y = Space.Lg;
            int d = Theme.Px(32);
            _back.SetBounds(Edge, y, d, d);
            _menu.SetBounds(Width - Edge - d, y, d, d);
            _hold.SetBounds(Width - Edge - d * 2 - Space.Xs, y, d, d);
            y += d + Space.Sm;

            int th = Math.Max(Theme.Px(30), Theme.Title.Height + Space.Sm);
            _name.SetBounds(Edge, y, Content, th);
            y += th + Space.Sm;

            int gh = Math.Max(Theme.Px(26), Theme.Small.Height + Space.Sm);
            int tg = Theme.Px(15);
            _tagRect = new Rectangle(Edge, y + (gh - tg) / 2, tg, tg);
            _tagline.SetBounds(Edge + tg + Space.Sm, y, Math.Max(40, Content - tg - Space.Sm), gh);
            y += gh + Space.Md;

            int ah = Theme.Px(32);
            int after = LayActs(y, ah);
            _metaRect = new Rectangle(after + Space.Sm, y,
                Math.Max(0, Width - Edge - after - Space.Sm), ah);
            y += ah + Space.Sm;

            _body.SetBounds(Edge, y, Content, Math.Max(Theme.Px(80), Height - y - Space.Md));
        }

        /// The action strip. It takes labels when there is room for all four and
        /// the status line beside them, and drops to glyphs alone when there is
        /// not. The word is kept on the accessible name either way, which is
        /// where the tooltip put it and where a screen reader looks for it.
        private int LayActs(int y, int height)
        {
            int wide = 0;
            for (int i = 0; i < _acts.Length; i++)
            {
                _acts[i].Label = _acts[i].AccessibleName ?? "";
                _acts[i].Pad = Space.Sm;
                _acts[i].Radius = Radii.Pill;
                wide += _acts[i].Wants() + Space.Xs;
            }
            bool labelled = wide + Theme.Px(150) <= Content;
            int x = Edge;
            for (int i = 0; i < _acts.Length; i++)
            {
                if (!labelled)
                {
                    _acts[i].Label = "";
                    _acts[i].Pad = Space.Xs;
                    _acts[i].Radius = Radii.Tight;
                }
                int w = Math.Max(Theme.Px(32), _acts[i].Wants());
                _acts[i].SetBounds(x, y, w, height);
                x += w + Space.Xs;
            }
            return x - Space.Xs;
        }

        /// What the line beside the actions says. A multiline text box cannot
        /// show a cue banner, so an empty note borrows this line for the hint
        /// that would have been in the box, and the model borrows it to say it
        /// is working, which is the one fact worth interrupting the count for.
        private string Meta()
        {
            if (_busy) return "Working on this note";
            Note n = _editing;
            if (n == null) return "";
            int words = Say.Words(_body.Text);
            if (words == 0) return "Write. Markdown works here.";
            return Say.Join(Say.Plural(words, "word"), Say.StampLong(n.UpdatedAt));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_editing == null) return;
            Graphics g = e.Graphics;
            Ui.Q(g);
            if (_tagRect.Width > 0)
            {
                Icons.Draw(g, Glyph.Hash, _tagRect, Theme.Faint, Math.Max(1.3f, Theme.Px(1.5)));
            }
            string meta = Meta();
            if (meta.Length == 0 || _metaRect.Width < Theme.Px(40)) return;
            Ui.Text(g, meta, Theme.Small, _busy ? Theme.AccentText : Theme.Faint, _metaRect,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }
}
