// Chat: the page.
//
// One conversation at a time, with the history behind a button rather than in a
// column beside the words. A chat is a reading surface first, and the width is
// better spent on the reply than on a list of titles nobody is reading.
//
// The transcript hosts real controls instead of painting rows, which is the one
// place in the app where a drawn row is the wrong answer: a reply is markdown,
// and its text has to be selectable, copyable and readable by a screen reader.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class ChatView : View
    {
        /// The four tones, the phone's presets and the phone's words for them.
        private static readonly double[] Heats = { 0.2, 0.4, 0.7, 1.0 };
        private static readonly string[] HeatNames = { "Precise", "Measured", "Balanced", "Inventive" };
        private static readonly string[] HeatBlurbs =
        {
            "Replies stick closely to what you gave it",
            "Replies stay careful, with a little room to phrase things",
            "Replies balance accuracy against readability",
            "Replies take more liberties, and wander further"
        };

        /// A turn as the page holds it. Kept apart from the stored message
        /// because a reply still arriving has no stored form yet, and because
        /// what a question shows is not always what the model was sent.
        private sealed class Bubble
        {
            public string Role = "user";
            public string Text = "";
            public string Prompt = "";
            public string Note = "";
            public DateTime At = DateTime.Now;
            public bool Streaming;
        }

        /// Something read off the disk or the clipboard and waiting to go with
        /// the next question.
        private sealed class Attached
        {
            public string Name = "";
            public string Text = "";
        }

        private readonly PageHead _head = new PageHead();
        private readonly IconButton _past = new IconButton();
        private readonly IconButton _fresh = new IconButton();
        private readonly IconButton _menu = new IconButton();
        private readonly Scroller _feed = new Scroller();
        private readonly Composer _say = new Composer();
        private readonly AppButton _tone = new AppButton();
        private readonly List<AppButton> _openers = new List<AppButton>();
        private readonly List<Turn> _turns = new List<Turn>();
        private readonly List<Bubble> _msgs = new List<Bubble>();
        private readonly List<Attached> _files = new List<Attached>();
        private readonly List<ChatSession> _hist = new List<ChatSession>();
        private readonly object _gate = new object();

        private ChatSession _session = new ChatSession();
        private Past _pastBody;
        private HttpCall _call;
        private int[] _high = new int[0];
        private string _arriving = "";
        private Rectangle _strip;
        private Rectangle _room;
        private int _openTop;
        private int _drained;
        private int _at = -1;
        private int _heat = 2;
        private bool _busy;
        private bool _opened;

        public ChatView(Hub hub) : base(hub)
        {
            _past.Icon = Glyph.Menu;
            _past.Tip = "Chat history";
            _past.Click += OnPast;
            _fresh.Icon = Glyph.Plus;
            _fresh.Tip = "New chat";
            _fresh.Click += OnFresh;
            _menu.Icon = Glyph.More;
            _menu.Tip = "This chat";
            _menu.Click += OnMenu;

            _feed.Extent = Extent;
            _feed.Arrange = Place;
            _feed.AccessibleName = "Transcript";

            _say.Lines = 3;
            _say.Hint = "Ask anything";
            _say.Submitted += OnSend;
            _say.Quiet(Glyph.Plus, "Attach", OnAttach);

            _tone.Look = ButtonLook.Ghost;
            _tone.ShowIcon = true;
            _tone.Icon = Glyph.Tag;
            _tone.Pad = Space.Sm;
            _tone.Click += OnTone;

            Openers();
            Controls.Add(_head);
            Controls.Add(_past);
            Controls.Add(_fresh);
            Controls.Add(_menu);
            Controls.Add(_feed);
            Controls.Add(_tone);
            Controls.Add(_say);
            Dropping();
            Working();
            Toned();
            Titled();
            Hub.StatusChanged += OnEngine;
        }

        public override string Label { get { return "Chat"; } }

        /// The last conversation is reopened the first time the page is shown
        /// rather than in the constructor, because the store is loaded by the
        /// shell's boot and there is nothing to reopen before that has run.
        public override void Shown()
        {
            if (!_opened) { _opened = true; Latest(); }
            Titled();
            Lay();
        }

        /// Leaving the page does not stop a reply. It keeps arriving and is
        /// saved when it lands, which is what somebody who asked a question and
        /// went to look at their clips expects to come back to.
        public override void Hidden() { }

        public override void ApplyTheme()
        {
            base.ApplyTheme();
            _say.ApplyTheme();
            for (int i = 0; i < _turns.Count; i++) _turns[i].ApplyTheme();
            Lay();
        }

        public override bool Shortcut(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && _busy) { Stop(); return true; }
            if (e.Control && e.KeyCode == Keys.N) { OnFresh(this, EventArgs.Empty); return true; }
            if (e.Control && e.KeyCode == Keys.H) { OnPast(this, EventArgs.Empty); return true; }
            if (e.Control && e.KeyCode == Keys.L) { Wipe(); return true; }
            if (e.Control && e.KeyCode == Keys.Z) return Hub.Undo();
            return false;
        }

        private void OnEngine()
        {
            Titled();
            Invalidate();
        }
    }
}
