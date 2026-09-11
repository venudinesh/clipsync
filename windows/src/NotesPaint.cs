// Notes: ordering, the masthead, and what a row looks like.
//
// A note prints as the same margin entry a clip does, through the same RowKit,
// so the two lists are one design rather than two that resemble each other. What
// differs is what goes in the text column: a title, what the note says after
// that line, and a filing line saying how long it is and what it is tagged.
//
// A heading is a row too. It prints its name at the text column, so it lines up
// with the titles it introduces, a count at the far end when the count is worth
// knowing, and a hairline under itself to say the section starts here.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class NotesView
    {
        private static readonly Glyph[] RowActions =
            new Glyph[] { Glyph.Copy, Glyph.Pin, Glyph.More };

        /// Pinned notes first, then whichever order the sort control is set to.
        private int Rank(Note a, Note b)
        {
            if (a.IsPinned != b.IsPinned) return a.IsPinned ? -1 : 1;
            if (_order == "created") return b.CreatedAt.CompareTo(a.CreatedAt);
            if (_order == "title")
            {
                string at = (a.Title ?? "").Trim();
                string bt = (b.Title ?? "").Trim();
                if (at.Length == 0) at = "untitled";
                if (bt.Length == 0) bt = "untitled";
                return string.Compare(at, bt, StringComparison.CurrentCultureIgnoreCase);
            }
            return b.UpdatedAt.CompareTo(a.UpdatedAt);
        }

        /// The head says nothing while the whole list is on show. Once a search
        /// or a tag has cut it down, it says what cut it and how much is left,
        /// because that is the one moment the number is not on screen already.
        private void Masthead(bool narrowed, int shown, int total)
        {
            if (!narrowed)
            {
                _head.Eyebrow = "";
                _head.Subtitle = "";
                return;
            }
            _head.Eyebrow = _filter.Length == 0 ? "Search" : "#" + _filter;
            _head.Subtitle = shown == 0
                ? "Nothing here matches that."
                : shown + " of " + Say.Plural(total, "note") + " match.";
        }

        /// The readout over the list, counted from the whole store rather than
        /// from what is on screen: it is a standing figure, not a result.
        private void Count()
        {
            int total = Hub.Notes.Items.Count;
            int pinned = 0, words = 0;
            for (int i = 0; i < total; i++)
            {
                Note n = Hub.Notes.Items[i];
                if (n.IsPinned) pinned++;
                words += Say.Words(n.Content);
            }
            Figure[] facts;
            if (pinned > 0 && words > 0)
            {
                facts = new Figure[] { new Figure(pinned.ToString(), "pinned"),
                                       new Figure(Say.Compact(words), words == 1 ? "word" : "words") };
            }
            else if (pinned > 0) facts = new Figure[] { new Figure(pinned.ToString(), "pinned") };
            else if (words > 0)
            {
                facts = new Figure[] { new Figure(Say.Compact(words), words == 1 ? "word" : "words") };
            }
            else if (_tagNames.Length > 0)
            {
                facts = new Figure[] { new Figure(_tagNames.Length.ToString(),
                                                 _tagNames.Length == 1 ? "tag" : "tags") };
            }
            else facts = new Figure[0];
            _tally.Set(total.ToString(), total == 1 ? "note written" : "notes written", facts);
        }

        /// Two empty states. Nothing written yet is an invitation; nothing found
        /// is a dead end, and saying "no notes yet" to somebody with two hundred
        /// of them would be a lie.
        private void Empty(bool narrowed)
        {
            if (narrowed && Hub.Notes.Items.Count > 0)
            {
                _list.EmptyGlyph = Glyph.Search;
                _list.EmptyTitle = "Nothing matches";
                _list.EmptyBody = "Try fewer words, or clear the tag you are filtering by.";
                return;
            }
            _list.EmptyGlyph = Glyph.Notes;
            _list.EmptyTitle = "No notes yet";
            _list.EmptyBody = "Notes are yours to write and edit. Markdown, tags, and the model " +
                              "on hand to summarise or tidy what you wrote.";
        }

        private int RowHeight(int i)
        {
            if (i < 0 || i >= _rows.Count) return Theme.Px(54);
            NoteRow row = _rows[i];
            if (row.Head) return Theme.Px(Theme.Dense ? 30 : 38);
            int h = Theme.Px(Theme.Dense ? 16 : 22) + Theme.Body.Height;
            if (row.Preview.Length > 0) h += Theme.Small.Height + Theme.Px(1);
            if (row.Filing.Length > 0) h += Theme.Tiny.Height + Theme.Px(2);
            return h;
        }

        private void DrawRow(Graphics g, Rectangle r, int i, bool hot, bool live)
        {
            if (i < 0 || i >= _rows.Count) return;
            NoteRow row = _rows[i];
            Rectangle plate = RowKit.Plate(r);
            if (row.Head) { DrawHead(g, plate, row); return; }

            Note n = row.Note;
            RowKit.Wash(g, plate, hot, live);
            RowKit.Margin(g, plate, Say.Stamp(n.UpdatedAt), n.IsPinned);

            int tx = RowKit.TextLeft(plate);
            int tw = RowKit.TextWidth(plate, hot, RowActions.Length);
            int lh = Theme.Body.Height + Theme.Px(2);
            int ph = row.Preview.Length > 0 ? Theme.Small.Height + Theme.Px(1) : 0;
            int fh = row.Filing.Length > 0 ? Theme.Tiny.Height + Theme.Px(2) : 0;
            int ty = plate.Y + Math.Max(0, (plate.Height - lh - ph - fh) / 2);

            Ui.Line(g, row.Title, Theme.Body, row.Named ? Theme.OnSurface : Theme.Faint,
                new Rectangle(tx, ty, tw, lh));
            if (ph > 0)
            {
                Ui.Line(g, row.Preview, Theme.Small, Theme.Muted, new Rectangle(tx, ty + lh, tw, ph));
            }
            if (fh > 0)
            {
                Ui.Line(g, row.Filing, Theme.Tiny, Theme.Faint,
                    new Rectangle(tx, ty + lh + ph, tw, fh));
            }
            if (hot) RowKit.Zones(g, plate, RowActions, n.IsPinned ? 1 : -1);
        }

        private static void DrawHead(Graphics g, Rectangle plate, NoteRow row)
        {
            int x = RowKit.TextLeft(plate);
            int h = Theme.Small.Height + Theme.Px(2);
            int y = plate.Bottom - h - Theme.Px(7);
            int cw = row.Tally.Length > 0 ? Ui.Measure(row.Tally, Theme.Tiny).Width : 0;
            Ui.Line(g, row.Label, Theme.Small, Theme.Muted,
                new Rectangle(x, y, Math.Max(20, plate.Right - x - cw - Space.Md), h));
            if (cw > 0)
            {
                Ui.Text(g, row.Tally, Theme.Tiny, Theme.Faint,
                    new Rectangle(plate.Right - cw - Space.Xs, y + Theme.Px(1), cw, h),
                    TextFormatFlags.Right | TextFormatFlags.SingleLine);
            }
            using (Pen p = new Pen(Theme.Hairline, 1f))
            {
                g.DrawLine(p, x, plate.Bottom - Theme.Px(2), plate.Right - Space.Xs,
                    plate.Bottom - Theme.Px(2));
            }
        }
    }
}
