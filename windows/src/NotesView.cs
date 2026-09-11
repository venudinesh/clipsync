// Notes: the page.
//
// The phone's notes screen, given a window. The masthead says nothing until a
// search or a tag narrows the list, because a standing subtitle over a list you
// can already see is furniture. Under it: how much you have written, a search
// box with the sort control beside it, and the tags as a wrapping strip.
//
// The editor is not a dialog. It is this same page with the list hidden and the
// writing surface shown, because a sheet is capped at a readable column width
// and a note being written wants the whole window. The rail stays visible, which
// is one thing the desktop does better than the phone, where the editor covers
// the navigation.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class NotesView : View
    {
        private readonly PageHead _head = new PageHead();
        private readonly AppButton _new = new AppButton();
        private readonly Readout _tally = new Readout();
        private readonly Field _find = new Field();
        private readonly IconButton _sort = new IconButton();
        private readonly Tabs _tags = new Tabs();
        private readonly Ledger _list = new Ledger();
        private readonly List<NoteRow> _rows = new List<NoteRow>();
        private string[] _tagNames = new string[0];
        private string _filter = "";
        private string _order = "updated";

        public NotesView(Hub hub) : base(hub)
        {
            _head.Title = "Notes";
            _new.Label = "New note";
            _new.ShowIcon = true;
            _new.Icon = Glyph.Plus;
            _new.AccessibleName = "New note";
            _new.Click += OnNew;

            _find.ShowIcon = true;
            _find.Icon = Glyph.Search;
            _find.Placeholder = "Search titles and text";
            _find.ShowTrail = true;
            _find.Trail = Glyph.Close;
            _find.Edited += delegate { Fill(); };
            _find.Trailed += delegate { _find.Box.Focus(); };

            _sort.Icon = Glyph.Sort;
            _sort.Tip = "Sort notes";
            _sort.Click += OnSort;

            _tags.MoreLabel = "More tags";
            _tags.AccessibleName = "Filter by tag";
            _tags.Changed += OnTag;
            _tags.Overflowed += OnAllTags;

            _list.Count = delegate { return _rows.Count; };
            _list.HeightOf = RowHeight;
            _list.PaintRow = DrawRow;
            _list.Skippable = delegate(int i) { return i >= 0 && i < _rows.Count && _rows[i].Head; };
            _list.Activated += OnOpen;
            _list.Clicked += OnRowClick;
            _list.EmptyGlyph = Glyph.Notes;

            Controls.Add(_head);
            Controls.Add(_new);
            Controls.Add(_tally);
            Controls.Add(_find);
            Controls.Add(_sort);
            Controls.Add(_tags);
            Controls.Add(_list);
            Build();

            Hub.NotesChanged += OnStoreChanged;
        }

        public override string Label { get { return "Notes"; } }

        public override void Shown()
        {
            Fill();
        }

        /// Leaving the page files what is in the editor. A note is not lost
        /// because somebody clicked Settings while thinking about a sentence.
        public override void Hidden()
        {
            if (_editing != null) Store();
        }

        public override void ApplyTheme()
        {
            base.ApplyTheme();
            _find.ApplyTheme();
            Faces();
            Lay();
        }

        public override bool Shortcut(KeyEventArgs e)
        {
            if (_editing != null)
            {
                if (e.KeyCode == Keys.Escape) { Close(); return true; }
                if (e.Control && e.KeyCode == Keys.S) { Store(); Hub.Say("Note saved"); return true; }
                return false;
            }
            if (e.Control && e.KeyCode == Keys.F) { _find.Box.Focus(); return true; }
            if (e.Control && e.KeyCode == Keys.N) { OnNew(this, EventArgs.Empty); return true; }
            if (e.Control && e.KeyCode == Keys.Z) return Hub.Undo();
            return false;
        }

        private void OnStoreChanged()
        {
            Fill();
        }

        private void OnTag(object sender, EventArgs e)
        {
            int i = _tags.Index;
            _filter = i <= 0 || i > _tagNames.Length ? "" : _tagNames[i - 1];
            Fill();
        }

        public override void Lay()
        {
            if (Width < 40 || Height < 40) return;
            if (_editing != null) { LayEdit(); return; }

            int y = Space.Lg;
            int hw = _head.Wants();
            int gw = Math.Max(Theme.Px(130), _new.Wants());
            _new.SetBounds(Width - Edge - gw, y + Math.Max(0, (hw - Theme.Px(34)) / 2), gw, Theme.Px(34));
            _head.SetBounds(Edge, y, Math.Max(60, Content - gw - Space.Md), hw);
            y += hw + Space.Md;

            bool roomy = Height > Theme.Px(560) && Hub.Notes.Items.Count > 0;
            _tally.Visible = roomy;
            if (roomy)
            {
                _tally.SetBounds(Edge, y, Content, Theme.Px(52));
                y += Theme.Px(52) + Space.Sm;
            }

            int sw = Theme.Px(32);
            _find.SetBounds(Edge, y, Math.Max(80, Content - sw - Space.Sm), Theme.Px(34));
            _sort.SetBounds(Width - Edge - sw, y + Theme.Px(1), sw, sw);
            y += Theme.Px(34) + Space.Sm;

            _tags.Visible = _tagNames.Length > 0;
            if (_tags.Visible)
            {
                int th = _tags.Wants(Content);
                _tags.SetBounds(Edge, y, Content, th);
                y += th + Space.Sm;
            }

            _list.SetBounds(Edge, y, Content, Math.Max(Theme.Px(80), Height - y - Space.Md));
            _list.Reload();
        }
    }
}
