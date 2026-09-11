// Notes: the sheets.
//
// Three of them: a note's actions, the sort order, and the tag list when the
// strip could not show every tag. The first two are verb stacks, which is what
// the phone puts a note's actions in. The third is a ledger rather than a stack,
// because a hundred tags is a list that has to scroll and a verb stack does not.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class NotesView
    {
        /// A note's actions, in the phone's order and with the phone's words.
        /// Summarise is offered only when there is something to summarise and a
        /// model to do it with, since an action that cannot work is worse than
        /// one that is not there.
        private void Options(Note n)
        {
            if (Hub.Sheet == null) return;
            Verbs list = new Verbs();
            list.Add(Glyph.Edit, "Open in editor", "", false,
                delegate { Hub.Sheet.Close(); Edit(n, false); });
            list.Add(Glyph.Pin, n.IsPinned ? "Unpin" : "Pin to top",
                n.IsPinned ? "" : "Keeps it above the rest of the list", false,
                delegate { Hub.Sheet.Close(); Pin(n); });
            list.Add(Glyph.Copy, "Copy note", "The title and the body", false,
                delegate { Hub.Sheet.Close(); Copy(n); });
            if ((n.Content ?? "").Trim().Length > 0 && Hub.Brain.Ready)
            {
                list.Add(Glyph.Sparkle, "Summarise", "Rewrites the note, on this PC", false,
                    delegate { Hub.Sheet.Close(); Summarise(n); });
            }
            list.Add(Glyph.Plus, "Duplicate", "Files a second copy", false,
                delegate { Hub.Sheet.Close(); Duplicate(n); });
            list.Add(Glyph.Trash, "Delete note", "You can undo this", true,
                delegate { Hub.Sheet.Close(); Delete(n); });
            string title = (n.Title ?? "").Trim();
            Hub.Sheet.Open(title.Length > 0 ? Say.FirstLine(title, 44) : "Untitled",
                Say.Join(Say.Plural(Say.Words(n.Content), "word"), Say.StampLong(n.UpdatedAt)),
                list, list.Wants());
            list.Lay();
        }

        private void OnSort(object sender, EventArgs e)
        {
            if (Hub.Sheet == null) return;
            Verbs list = new Verbs();
            Mode(list, "updated", "Last edited", "The note you touched most recently, first");
            Mode(list, "created", "Date created", "Newest note first, however long ago you edited it");
            Mode(list, "title", "Title A to Z", "A note with no title sorts as untitled");
            Hub.Sheet.Open("Sort notes", "Pinned notes stay on top either way", list, list.Wants());
            list.Lay();
        }

        /// One sort choice. The one in force wears a tick instead of the sort
        /// glyph, so the sheet answers "which is it now" without a second column.
        private void Mode(Verbs list, string key, string label, string detail)
        {
            string k = key;
            list.Add(_order == key ? Glyph.Check : Glyph.Sort, label, detail, false,
                delegate { Hub.Sheet.Close(); Sorted(k); });
        }

        private void Sorted(string key)
        {
            if (_order == key) return;
            _order = key;
            Fill();
            _list.Home();
        }

        /// Every tag, with how many notes carry it. Reached from the last pill in
        /// the strip when the tags outran three rows, and from the keyboard.
        private void OnAllTags(object sender, EventArgs e)
        {
            if (Hub.Sheet == null) return;
            string[] names = _tagNames;
            int[] counts = new int[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                for (int j = 0; j < Hub.Notes.Items.Count; j++)
                {
                    if (Tagged(Hub.Notes.Items[j], names[i])) counts[i]++;
                }
            }
            Ledger pick = new Ledger();
            pick.AccessibleName = "Tags";
            pick.Count = delegate { return names.Length + 1; };
            pick.HeightOf = delegate(int i) { return Theme.Px(38); };
            pick.PaintRow = delegate(Graphics g, Rectangle r, int i, bool hot, bool live)
            {
                PaintTag(g, r, i, hot, live, names, counts);
            };
            pick.Clicked += delegate(int i, Point p) { Take(i, names); };
            pick.Activated += delegate(int i) { Take(i, names); };
            int want = (names.Length + 1) * Theme.Px(38) + Space.Sm;
            Hub.Sheet.Open("Filter by tag", Say.Plural(names.Length, "tag"), pick,
                Math.Min(Theme.Px(320), want));
            pick.Selected = Row(names);
        }

        private int Row(string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], _filter, StringComparison.CurrentCulture)) return i + 1;
            }
            return 0;
        }

        private void Take(int i, string[] names)
        {
            if (i < 0 || i > names.Length) return;
            _filter = i == 0 ? "" : names[i - 1];
            Hub.Sheet.Close();
            Fill();
            _list.Home();
        }

        private void PaintTag(Graphics g, Rectangle r, int i, bool hot, bool live,
            string[] names, int[] counts)
        {
            if (i < 0 || i > names.Length) return;
            bool all = i == 0;
            string label = all ? "All notes" : "#" + names[i - 1];
            bool on = all ? _filter.Length == 0
                          : string.Equals(names[i - 1], _filter, StringComparison.CurrentCulture);
            string tally = (all ? Hub.Notes.Items.Count : counts[i - 1]).ToString();
            Rectangle plate = RowKit.Plate(r);
            RowKit.Wash(g, plate, hot, live);
            int d = Theme.Px(16);
            Icons.Draw(g, on ? Glyph.Check : Glyph.Hash,
                new Rectangle(plate.X + Space.Sm, plate.Y + (plate.Height - d) / 2, d, d),
                on ? Theme.AccentText : Theme.Faint, Math.Max(1.3f, Theme.Px(1.5)));
            int x = plate.X + Space.Sm + d + Space.Md;
            int cw = Ui.Measure(tally, Theme.Tiny).Width + Space.Sm;
            Ui.Line(g, label, Theme.Body, on ? Theme.AccentText : Theme.OnSurface,
                new Rectangle(x, plate.Y, Math.Max(20, plate.Right - x - cw - Space.Sm), plate.Height));
            Ui.Text(g, tally, Theme.Tiny, Theme.Faint,
                new Rectangle(plate.Right - cw, plate.Y, cw, plate.Height),
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }
}
