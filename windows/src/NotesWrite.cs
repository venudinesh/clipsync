// Notes: filing what was written, and asking the model about it.
//
// Closing the editor files the note, so there is no save button to forget. What
// that costs is a rule about when a note counts as changed: comparing before
// writing, because otherwise opening a note and closing it untouched would bump
// its edited time and shuffle it to the top of the list for nothing.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class NotesView
    {
        private void Store()
        {
            Note n = _editing;
            if (n == null) return;
            string title = _name.Text.Trim();
            string body = _body.Text;
            List<string> tags = Parsed(_tagline.Text);
            if (_fresh && title.Length == 0 && body.Trim().Length == 0 && tags.Count == 0) return;
            if (!_fresh && title == (n.Title ?? "").Trim() && body == Lines(n.Content) &&
                Same(tags, n.Tags)) return;
            n.Title = title;
            n.Content = body;
            n.Tags = tags;
            n.UpdatedAt = DateTime.Now;
            if (_fresh) { Hub.Notes.Items.Add(n); _fresh = false; }
            Hub.Notes.Save();
            Hub.RaiseNotes();
        }

        /// Tags as typed: comma separated, blanks dropped, and the same tag twice
        /// counted once, since a note tagged "work, work" has one tag.
        private static List<string> Parsed(string line)
        {
            List<string> tags = new List<string>();
            string[] parts = (line ?? "").Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string t = parts[i].Trim();
                if (t.Length == 0) continue;
                bool seen = false;
                for (int j = 0; j < tags.Count; j++)
                {
                    if (string.Equals(tags[j], t, StringComparison.CurrentCultureIgnoreCase)) seen = true;
                }
                if (!seen) tags.Add(t);
            }
            return tags;
        }

        private static string Joined(List<string> tags)
        {
            if (tags == null) return "";
            string s = "";
            for (int i = 0; i < tags.Count; i++)
            {
                string t = (tags[i] ?? "").Trim();
                if (t.Length == 0) continue;
                if (s.Length > 0) s += ", ";
                s += t;
            }
            return s;
        }

        private static bool Same(List<string> a, List<string> b)
        {
            int bn = b == null ? 0 : b.Count;
            if (a.Count != bn) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (!string.Equals(a[i], b[i], StringComparison.CurrentCulture)) return false;
            }
            return true;
        }

        /// A text box wants carriage returns. Notes written on the phone have
        /// bare newlines, so they are converted coming in and left alone going
        /// out, which keeps a note the phone wrote comparable with itself.
        private static string Lines(string s)
        {
            if (s == null) return "";
            return s.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");
        }

        /// The model wrote the note. Puts the new text in front of whoever is
        /// looking at it, without counting that as something they typed.
        private void Wrote(string text)
        {
            _loading = true;
            _body.Text = Lines(text);
            _loading = false;
            if (_editing != null) _fresh = false;
            Invalidate(_metaRect);
        }

        private void Typed()
        {
            if (_loading || _editing == null) return;
            Invalidate(_metaRect);
        }

        /// One of the four actions over the writing surface. It works on what is
        /// in the box rather than what was last filed, so a sentence typed a
        /// second ago is included.
        private void Ask(string prompt)
        {
            if (_editing == null) return;
            if (_body.Text.Trim().Length == 0) { Hub.Oops("Write something first"); return; }
            if (!Hub.Brain.Ready) { Hub.Oops("Load a model in Settings first"); return; }
            if (_busy) { Hub.Oops("Already working on a note"); return; }
            Store();
            Work(_editing, prompt, _body.Text, "Note updated", false);
        }

        private void Working()
        {
            for (int i = 0; i < _acts.Length; i++) _acts[i].Busy = _busy;
            if (_editing != null) Invalidate(_metaRect);
        }

        /// The editor's own menu: the things a note can have done to it that are
        /// not worth a button on the page.
        private void OnMenu(object sender, EventArgs e)
        {
            if (Hub.Sheet == null || _editing == null) return;
            Note n = _editing;
            Verbs list = new Verbs();
            list.Add(Glyph.Copy, "Copy all", "The title and the body", false,
                delegate { Hub.Sheet.Close(); Store(); Copy(n); });
            if (Hub.Brain.Ready)
            {
                list.Add(Glyph.Sparkle, "Summarise", "Adds a summary under what you wrote", false,
                    delegate { Hub.Sheet.Close(); Store(); Summarise(n); });
            }
            list.Add(Glyph.Plus, "Duplicate note", "", false,
                delegate { Hub.Sheet.Close(); Store(); Duplicate(n); });
            list.Add(Glyph.Hash, "Word count", "", false,
                delegate { Hub.Sheet.Close(); Counted(); });
            list.Add(Glyph.Trash, "Delete note", "You can undo this", true,
                delegate { Hub.Sheet.Close(); Delete(n); });
            Hub.Sheet.Open("This note", Say.StampLong(n.UpdatedAt), list, list.Wants());
            list.Lay();
        }

        private void Counted()
        {
            string text = _body.Text;
            Hub.Say(Say.Join(Say.Plural(Say.Words(text), "word"),
                             Say.Plural(text.Length, "character")));
        }
    }
}
