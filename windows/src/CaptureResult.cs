// Capture: the result.
//
// One card for both halves of the page, because what you do with recognised text and
// with dictated text is the same list: copy it, keep it, tidy it, or throw it away.
// The text is editable in place. Recognition is never perfect and a person who can
// see the mistake should be able to fix it without a round trip through Notes.
//
// Clearing and summarising both hand back what they replaced. A page of recognised
// text is minutes of somebody's work and one wrong button should not end it.

using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace ClipSyncAI
{
    /// The result card: a header with room for the actions, a rule, and the body
    /// below it. With nothing in it, the empty state instead.
    internal sealed class ResultCard : Card
    {
        public string Label = "";
        public string Detail = "";
        public bool Empty;
        public Glyph EmptyGlyph = Glyph.Capture;
        public string EmptyTitle = "";
        public string EmptyBody = "";

        /// Pixels at the end of the header row kept clear for the action buttons.
        public int Reserve;

        protected override void Paint2(Graphics g, Rectangle r)
        {
            Rectangle box = Inner;
            if (Empty) { Blank(g, box); return; }
            int lh = Theme.Px(34);
            Size ls = Ui.Measure(Label, Theme.Heading);
            Ui.Line(g, Label, Theme.Heading, Theme.OnSurface,
                new Rectangle(box.X, box.Y + (lh - Theme.Heading.Height) / 2,
                    Math.Min(box.Width, ls.Width + Theme.Px(4)), Theme.Heading.Height + Theme.Px(2)));
            int x = box.X + ls.Width + Space.Md;
            int room = Math.Max(0, box.Right - Reserve - x);
            if (Detail.Length > 0 && room > Theme.Px(40))
            {
                Ui.Line(g, Detail, Theme.Small, Theme.Muted,
                    new Rectangle(x, box.Y + (lh - Theme.Small.Height) / 2 + Theme.Px(1),
                        room, Theme.Small.Height + Theme.Px(2)));
            }
            using (Pen p = new Pen(Theme.Hairline))
            {
                g.DrawLine(p, box.X, box.Y + lh + Theme.Px(2), box.Right, box.Y + lh + Theme.Px(2));
            }
        }

        private void Blank(Graphics g, Rectangle r)
        {
            if (EmptyTitle.Length == 0) return;
            int d = Theme.Px(56);
            int all = d + Space.Md + Theme.Px(26) + Theme.Px(50);
            int cx = r.X + (r.Width - d) / 2;
            int cy = r.Y + Math.Max(0, (r.Height - all) / 2);
            Rectangle disc = new Rectangle(cx, cy, d, d);
            Ui.Fill(g, disc, d / 2, Palette.Alpha(Theme.Accent, 0.12));
            int gi = Theme.Px(24);
            Icons.Draw(g, EmptyGlyph, new Rectangle(cx + (d - gi) / 2, cy + (d - gi) / 2, gi, gi),
                Theme.AccentText, Math.Max(1.4f, Theme.Px(1.6)));
            Rectangle tr = new Rectangle(r.X + Space.Xl, disc.Bottom + Space.Md,
                Math.Max(1, r.Width - Space.Xl * 2), Theme.Px(26));
            Ui.Centre(g, EmptyTitle, Theme.Title, Theme.OnSurface, tr);
            if (EmptyBody.Length == 0) return;
            Rectangle br = new Rectangle(tr.X, tr.Bottom + Theme.Px(2), tr.Width, Theme.Px(48));
            Ui.Text(g, EmptyBody, Theme.Small, Theme.Muted, br,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak);
        }
    }

    internal sealed partial class CaptureView
    {
        private readonly ResultCard _result = new ResultCard();
        private readonly Field _text = new Field();
        private readonly IconButton _copy = new IconButton();
        private readonly IconButton _note = new IconButton();
        private readonly IconButton _sum = new IconButton();
        private readonly IconButton _clip = new IconButton();
        private readonly IconButton _clear = new IconButton();
        private readonly IconButton _rerun = new IconButton();
        private IconButton[] _acts = new IconButton[0];
        private bool _asking;

        private void BuildResult()
        {
            _result.AccessibleName = "Result";
            _text.Bare = true;
            _text.Multiline = true;
            _text.Face = Theme.Body;
            _text.Placeholder = "The text will appear here";
            _text.AccessibleName = "Recognised text";
            _text.Edited += OnEdit;
            Deed(_copy, Glyph.Copy, "Copy", OnCopy);
            Deed(_note, Glyph.Notes, "Save as a note", OnNote);
            Deed(_sum, Glyph.Sparkle, "Summarise", OnSum);
            Deed(_clip, Glyph.Clips, "Send to clips", OnClip);
            Deed(_clear, Glyph.Trash, "Clear", OnClear);
            Deed(_rerun, Glyph.Refresh, "Read it again", OnRerun);
            _acts = new IconButton[] { _copy, _note, _sum, _clip, _clear, _rerun };
            _result.Controls.Add(_text);
            for (int i = 0; i < _acts.Length; i++) _result.Controls.Add(_acts[i]);
            Controls.Add(_result);
        }

        private static void Deed(IconButton b, Glyph icon, string tip, EventHandler act)
        {
            b.Icon = icon;
            b.Tip = tip;
            b.Look = ButtonLook.Ghost;
            b.Click += act;
        }

        /// The result card, told what it is showing. One place decides the header,
        /// the buttons and whether the empty state is up, because five things change
        /// it and they should not each hold an opinion about the other four.
        private void Told()
        {
            bool scan = _tab == 0;
            string text = _got[_tab];
            bool has = text.Length > 0;
            bool reading = _busy && scan;
            _result.Empty = !has && !reading;
            _result.Label = reading ? "Reading the picture"
                : (scan ? "Extracted text" : "Dictated text");
            _result.Detail = reading ? "This can take a moment" : Detail(text);
            _result.EmptyGlyph = scan ? Glyph.Capture : Glyph.Mic;
            _result.EmptyTitle = scan ? "Nothing scanned yet" : "Nothing dictated yet";
            _result.EmptyBody = scan
                ? "Choose a region of the screen, paste a screenshot, or open a picture. " +
                  "The reading happens on this PC and the picture never leaves it."
                : "Press the button and talk. Windows does the listening, on this PC, and " +
                  "nothing is sent anywhere.";
            _text.Visible = has;
            bool acts = has && !reading;
            _copy.Visible = acts;
            _note.Visible = acts;
            _sum.Visible = acts;
            _clip.Visible = acts;
            _clear.Visible = acts;
            _rerun.Visible = scan && _shot != null && !reading;
            _result.Reserve = Bar() + Space.Md;
            _result.Invalidate();
            LayResult();
        }

        /// The line beside the header. It says how much there is and, for a scan,
        /// what read it, because a page claiming the work happened on this PC should
        /// be able to say by what.
        private string Detail(string text)
        {
            if (text.Length == 0) return "";
            string much = Say.Plural(Say.Words(text), "word");
            if (_tab != 0 || _how.Length == 0) return much;
            return much + Say.Sep + _how;
        }

        private int Bar()
        {
            int n = 0;
            for (int i = 0; i < _acts.Length; i++) if (_acts[i].Visible) n++;
            return n == 0 ? 0 : n * Theme.Px(32) + (n - 1) * Space.Xs;
        }

        private void LayResult()
        {
            if (_result.Width < Theme.Px(80) || _result.Height < Theme.Px(50)) return;
            Rectangle box = _result.Inner;
            int lh = Theme.Px(34);
            int w = Theme.Px(32);
            int x = box.Right - w;
            for (int i = _acts.Length - 1; i >= 0; i--)
            {
                if (!_acts[i].Visible) continue;
                _acts[i].SetBounds(x, box.Y + (lh - w) / 2, w, w);
                x -= w + Space.Xs;
            }
            int ty = box.Y + lh + Space.Md;
            _text.SetBounds(box.X, ty, box.Width, Math.Max(Theme.Px(30), box.Bottom - ty));
        }

        /// An edit writes straight through. The word count follows it, but the empty
        /// state does not come back mid sentence: a box that snaps away while it is
        /// being emptied on purpose is hostile.
        private void OnEdit(object sender, EventArgs e)
        {
            if (_loading) return;
            _got[_tab] = Flat(_text.Text);
            _result.Detail = Detail(_got[_tab]);
            _result.Invalidate();
        }

        private string Words()
        {
            return Flat(_text.Text).Trim();
        }

        private void OnCopy(object sender, EventArgs e)
        {
            CopyOut(Words(), "Text copied");
        }

        /// The title carries the date, because a page of notes all called Scan is a
        /// page of notes with no titles.
        private void OnNote(object sender, EventArgs e)
        {
            string text = Words();
            if (text.Length == 0) { Hub.Oops("Nothing to save"); return; }
            Note n = new Note();
            n.Title = (_tab == 0 ? "Scan " : "Dictation ") + Say.Stamp(DateTime.Now);
            n.Content = text;
            n.Tags.Add(_tab == 0 ? "OCR" : "Dictation");
            Hub.Notes.Items.Add(n);
            Hub.Notes.Save();
            Hub.RaiseNotes();
            Hub.Say("Saved to your notes");
        }

        /// Sending to clips goes through the watcher, so it is filed by the same code
        /// that files a copy: the length floor, the duplicate check, the formatting
        /// pass and the history cap all apply.
        private void OnClip(object sender, EventArgs e)
        {
            string text = Words();
            if (text.Length == 0) { Hub.Oops("Nothing to send"); return; }
            if (_watch == null) { Hub.Oops("Clips are not available"); return; }
            _watch.Keep(text);
        }

        private void OnClear(object sender, EventArgs e)
        {
            string was = _got[_tab];
            if (was.Length == 0) return;
            int tab = _tab;
            Put(tab, "");
            Hub.Undoable(tab == 0 ? "Scan cleared" : "Dictation cleared",
                new Action(delegate { Put(tab, was); }));
        }

        private void Put(int tab, string text)
        {
            _got[tab] = text == null ? "" : text;
            if (_tab == tab)
            {
                _loading = true;
                _text.Text = Lines(_got[tab]);
                _loading = false;
            }
            Told();
        }

        private void OnRerun(object sender, EventArgs e)
        {
            if (_tab != 0 || Working()) return;
            if (_shot == null) { Hub.Oops("There is no picture to read again"); return; }
            Run();
        }

        /// Summarising appends rather than replaces, because a summary of a receipt
        /// is not a substitute for the receipt.
        private void OnSum(object sender, EventArgs e)
        {
            string text = Words();
            if (text.Length == 0) { Hub.Oops("Nothing to summarise"); return; }
            if (!Hub.Brain.Ready)
            {
                Hub.Oops("No model server is answering, so there is nothing to summarise with");
                return;
            }
            if (_asking) { Hub.Oops("Already working on this"); return; }
            _asking = true;
            _sum.Busy = true;
            int tab = _tab;
            Thread t = new Thread(new ThreadStart(delegate { Asked(tab, text); }));
            t.IsBackground = true;
            t.Name = "capture-model";
            t.Start();
        }

        private void Asked(int tab, string body)
        {
            string got;
            try { got = Hub.Brain.Instruct(Prompts.NoteSummaryPrefix + body, null, null); }
            catch (Exception ex) { Paths.Log("capture summary", ex); got = ""; }
            Post(delegate { Summed(tab, body, got); });
        }

        private void Summed(int tab, string was, string got)
        {
            _asking = false;
            _sum.Busy = false;
            if (got == null || got.Trim().Length == 0)
            {
                Hub.Oops("The model did not return a summary");
                Told();
                return;
            }
            Put(tab, was + "\n\n## Summary\n\n" + got.Trim());
            Hub.Undoable("Summary added", new Action(delegate { Put(tab, was); }));
        }
    }
}
