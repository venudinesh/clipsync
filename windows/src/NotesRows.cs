// Notes: building the list.
//
// The store is filtered, sorted, then cut into the two sections the phone
// prints: what you pinned, and everything else. The headings are rows in the
// list rather than controls above it, so they scroll with the entries they
// belong to instead of hanging over a list that has moved on. The ledger is
// told to step over them, so nothing can select a heading and press Enter.
//
// Every string a row prints is worked out here, once, because a resize drag
// asks each row how tall it is on every frame.

using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ClipSyncAI
{
    /// A line in the notes list: either a note prepared for painting, or a
    /// heading with an optional count at its right end.
    internal sealed class NoteRow
    {
        public readonly Note Note;
        public readonly string Label;
        public readonly string Tally;
        public readonly string Title;
        public readonly bool Named;
        public readonly string Preview;
        public readonly string Filing;

        public NoteRow(string label, string tally)
        {
            Label = label;
            Tally = tally ?? "";
            Title = "";
            Preview = "";
            Filing = "";
        }

        public NoteRow(Note n)
        {
            Note = n;
            Label = "";
            Tally = "";
            string t = (n.Title ?? "").Trim();
            Named = t.Length > 0;
            Title = Named ? t : "Untitled";
            string body = n.Content ?? "";
            string first = Markdown.Peek(body);
            string rest = Markdown.Trail(body, 300);
            // What the note says, minus the line the title is already showing.
            // Every note the app writes for you is titled from its own first
            // line, and printing that line again underneath in a dimmer size
            // reads as a mistake rather than as a preview.
            Preview = Named && Say.Echoes(Title, first)
                ? rest
                : Say.FirstLine((first + " " + rest).Trim(), 300);
            int words = Say.Words(n.Content);
            Filing = Say.Join(words > 0 ? Say.Plural(words, "word") : "", Hashes(n.Tags));
        }

        /// The first two tags, written the way tags are written everywhere.
        private static string Hashes(List<string> tags)
        {
            if (tags == null) return "";
            string s = "";
            for (int i = 0; i < tags.Count && i < 2; i++)
            {
                string t = (tags[i] ?? "").Trim();
                if (t.Length == 0) continue;
                if (s.Length > 0) s += " ";
                s += "#" + t;
            }
            return s;
        }

        public bool Head { get { return Note == null; } }
    }

    internal sealed partial class NotesView
    {
        private void Fill()
        {
            Tags();
            string q = _find.Text.Trim();
            List<Note> kept = new List<Note>();
            List<Note> all = Hub.Notes.Items;
            for (int i = 0; i < all.Count; i++)
            {
                if (Matches(all[i], q)) kept.Add(all[i]);
            }
            kept.Sort(Rank);

            _rows.Clear();
            int pinned = 0;
            for (int i = 0; i < kept.Count; i++)
            {
                if (kept[i].IsPinned) pinned++;
            }
            if (pinned > 0) _rows.Add(new NoteRow("Pinned", ""));
            for (int i = 0; i < pinned; i++) _rows.Add(new NoteRow(kept[i]));
            int rest = kept.Count - pinned;
            if (rest > 0)
            {
                _rows.Add(pinned > 0
                    ? new NoteRow("Other notes", Say.Plural(rest, "note"))
                    : new NoteRow("All notes", ""));
                for (int i = pinned; i < kept.Count; i++) _rows.Add(new NoteRow(kept[i]));
            }

            bool narrowed = q.Length > 0 || _filter.Length > 0;
            Masthead(narrowed, kept.Count, all.Count);
            Count();
            Empty(narrowed);
            Lay();
        }

        /// The tag strip, rebuilt from the store. A filter whose last note was
        /// just deleted quietly becomes no filter, rather than a strip showing a
        /// tag that no longer exists over a list that can never match it.
        private void Tags()
        {
            List<string> names = new List<string>();
            List<Note> all = Hub.Notes.Items;
            for (int i = 0; i < all.Count; i++)
            {
                List<string> tags = all[i].Tags;
                if (tags == null) continue;
                for (int j = 0; j < tags.Count; j++)
                {
                    string t = (tags[j] ?? "").Trim();
                    if (t.Length > 0 && !names.Contains(t)) names.Add(t);
                }
            }
            names.Sort(StringComparer.CurrentCultureIgnoreCase);
            _tagNames = names.ToArray();

            int at = 0;
            for (int i = 0; i < _tagNames.Length; i++)
            {
                if (string.Equals(_tagNames[i], _filter, StringComparison.CurrentCulture)) at = i + 1;
            }
            if (at == 0) _filter = "";

            string[] items = new string[_tagNames.Length + 1];
            items[0] = "All";
            for (int i = 0; i < _tagNames.Length; i++) items[i + 1] = "#" + _tagNames[i];
            _tags.SetItems(items, at);
        }

        private bool Matches(Note n, string q)
        {
            if (_filter.Length > 0 && !Tagged(n, _filter)) return false;
            if (q.Length == 0) return true;
            return Has(n.Title, q) || Has(n.Content, q);
        }

        private static bool Tagged(Note n, string tag)
        {
            if (n.Tags == null) return false;
            for (int i = 0; i < n.Tags.Count; i++)
            {
                if (string.Equals((n.Tags[i] ?? "").Trim(), tag, StringComparison.CurrentCulture)) return true;
            }
            return false;
        }

        private static bool Has(string haystack, string needle)
        {
            return haystack != null &&
                   haystack.IndexOf(needle, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }
    }
}
