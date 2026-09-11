// Notes: the editor.
//
// Same page, different mode: the list hides and the writing surface appears. A
// sheet was the other option and it is the wrong one, because a sheet is capped
// at a readable column and a note wants the window. The rail stays put, so the
// rest of the app is still one click away while you write.
//
// Nothing is filed until there is something to file. A new note that is closed
// empty is dropped; an existing note emptied on purpose is kept, because that
// was a decision rather than an accident.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class NotesView
    {
        private Note _editing;
        private bool _fresh;
        private bool _busy;
        private bool _loading;
        private Rectangle _metaRect;

        private readonly IconButton _back = new IconButton();
        private readonly Field _name = new Field();
        private readonly IconButton _hold = new IconButton();
        private readonly IconButton _menu = new IconButton();
        private readonly Field _tagline = new Field();
        private readonly Field _body = new Field();
        private IconButton[] _acts = new IconButton[0];

        private void Build()
        {
            _back.Icon = Glyph.Back;
            _back.Tip = "Save and close";
            _back.Click += delegate { Close(); };

            _name.Bare = true;
            _name.Placeholder = "Untitled";

            _hold.Icon = Glyph.Pin;
            _hold.Tip = "Pin to top";
            _hold.Click += OnHold;

            _menu.Icon = Glyph.More;
            _menu.Tip = "More";
            _menu.Click += OnMenu;

            _tagline.Bare = true;
            _tagline.Placeholder = "Tags, comma separated";

            _body.Bare = true;
            _body.Multiline = true;
            _body.Placeholder = "Write. Markdown works here.";
            _body.Edited += delegate { Typed(); };

            _acts = new IconButton[4];
            _acts[0] = Deed(Glyph.Sparkle, "Summarise", Prompts.NoteTidyPrefix);
            _acts[1] = Deed(Glyph.Tasks, "Extract tasks", Prompts.NoteTasksPrefix);
            _acts[2] = Deed(Glyph.Spell, "Fix grammar", Prompts.NoteGrammarPrefix);
            _acts[3] = Deed(Glyph.Edit, "Expand", Prompts.NoteExpandPrefix);

            Controls.Add(_back);
            Controls.Add(_name);
            Controls.Add(_hold);
            Controls.Add(_menu);
            Controls.Add(_tagline);
            Controls.Add(_body);
            for (int i = 0; i < _acts.Length; i++) Controls.Add(_acts[i]);
            Faces();
            Mode(false);
        }

        /// One of the four model actions above the writing surface. They stay
        /// live with no model loaded, and say so when pressed, because a row of
        /// disabled buttons teaches nobody what the app can do.
        private IconButton Deed(Glyph icon, string label, string prompt)
        {
            string p = prompt;
            IconButton b = new IconButton();
            b.Icon = icon;
            b.Tip = label;
            b.Click += delegate { Ask(p); };
            return b;
        }

        /// The typing faces, reassigned on every theme change because the theme
        /// rebuilds its fonts and a field holds the one it was given.
        private void Faces()
        {
            _name.Face = Theme.Title;
            _tagline.Face = Theme.Small;
            _body.Face = Theme.Body;
            _name.ApplyTheme();
            _tagline.ApplyTheme();
            _body.ApplyTheme();
        }

        private void Edit(Note n, bool fresh)
        {
            _loading = true;
            _editing = n;
            _fresh = fresh;
            _name.Text = n.Title ?? "";
            _tagline.Text = Joined(n.Tags);
            _body.Text = Lines(n.Content);
            _loading = false;
            Held(n.IsPinned);
            Mode(true);
            if (fresh) _name.Box.Focus();
            else _body.Box.Focus();
        }

        private void Close()
        {
            Store();
            Shut();
        }

        /// Leaves the editor without filing anything, for when the note being
        /// edited has just been deleted from under it.
        private void Shut()
        {
            _editing = null;
            _fresh = false;
            Mode(false);
            Fill();
            _list.Select();
        }

        private void Mode(bool editing)
        {
            _head.Visible = !editing;
            _new.Visible = !editing;
            _tally.Visible = !editing;
            _find.Visible = !editing;
            _sort.Visible = !editing;
            _tags.Visible = !editing;
            _list.Visible = !editing;

            _back.Visible = editing;
            _name.Visible = editing;
            _hold.Visible = editing;
            _menu.Visible = editing;
            _tagline.Visible = editing;
            _body.Visible = editing;
            for (int i = 0; i < _acts.Length; i++) _acts[i].Visible = editing;
            Lay();
            Invalidate();
        }

        private void Held(bool pinned)
        {
            _hold.Look = pinned ? ButtonLook.Soft : ButtonLook.Ghost;
            _hold.Tip = pinned ? "Unpin" : "Pin to top";
            _hold.Invalidate();
        }

        private void OnHold(object sender, EventArgs e)
        {
            if (_editing == null) return;
            Store();
            Pin(_editing);
            Held(_editing.IsPinned);
        }
    }
}
