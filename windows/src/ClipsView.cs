// Clips: the feed.
//
// The screen the app exists for. The greeting and the count at the top, the
// monitor and the composer under them, and then every clip that has been kept,
// newest first with pinned ones held above. Everything above the feed is a fixed
// header stack and the feed itself scrolls, which is the desktop version of the
// phone's one long scroll.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class ClipsView : View
    {
        private readonly PageHead _head = new PageHead();
        private readonly AppButton _grab = new AppButton();
        private readonly Readout _tally = new Readout();
        private readonly MonitorCard _monitor = new MonitorCard();
        private readonly Toggle _live = new Toggle();
        private readonly Composer _compose = new Composer();
        private readonly SectionHead _recent = new SectionHead();
        private readonly IconButton _resync = new IconButton();
        private readonly Field _find = new Field();
        private readonly Ledger _list = new Ledger();
        private readonly List<ClipRow> _rows = new List<ClipRow>();
        private readonly Watcher _watch;
        private TimelyGreeting _greet;

        public ClipsView(Hub hub, Watcher watch) : base(hub)
        {
            _watch = watch;

            _head.Title = "Clips";
            _grab.Label = "Read clipboard now";
            _grab.ShowIcon = true;
            _grab.Icon = Glyph.Clips;
            _grab.Look = ButtonLook.Soft;
            _grab.Click += delegate { _watch.Take(true); };

            _monitor.Title = "Clipboard monitor";
            _live.AccessibleName = "Clipboard monitor";
            _live.Changed += OnLive;
            _monitor.Controls.Add(_live);

            _compose.Hint = "Paste or type something worth keeping";
            _compose.Go.Label = "Keep";
            _compose.Submitted += OnKeep;
            _compose.Quiet(Glyph.Copy, "Clipboard", OnPasteIn);
            _compose.Quiet(Glyph.Mic, "Dictate", OnDictate);

            _recent.Label = "Recent";
            _recent.Reserve = Theme.Px(40);
            _resync.Icon = Glyph.Refresh;
            _resync.Tip = "Reprocess every clip with the current model";
            _resync.Click += OnResync;
            WireJoin();

            _find.ShowIcon = true;
            _find.Icon = Glyph.Search;
            _find.Placeholder = "Search your clips";
            _find.Edited += delegate { Fill(); };

            _list.Count = delegate { return _rows.Count; };
            _list.HeightOf = RowHeight;
            _list.PaintRow = DrawRow;
            _list.Activated += OnOpen;
            _list.Clicked += OnRowClick;
            _list.EmptyGlyph = Glyph.Clips;

            Controls.Add(_head);
            Controls.Add(_grab);
            Controls.Add(_tally);
            Controls.Add(_monitor);
            Controls.Add(_compose);
            Controls.Add(_find);
            Controls.Add(_recent);
            Controls.Add(_resync);
            Controls.Add(_list);

            Hub.ClipsChanged += OnStoreChanged;
        }

        public override string Label { get { return "Clips"; } }

        public override void Shown()
        {
            Greet();
            _live.Preset(Hub.Settings.CaptureEnabled);
            _monitor.Live = Hub.Settings.CaptureEnabled;
            Fill();
        }

        public override void ApplyTheme()
        {
            base.ApplyTheme();
            _compose.ApplyTheme();
            _find.ApplyTheme();
            _monitor.Live = Hub.Settings.CaptureEnabled;
            Lay();
        }

        public override bool Shortcut(KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.F) { _find.Box.Focus(); return true; }
            if (e.Control && e.KeyCode == Keys.R) { _watch.Take(true); return true; }
            return false;
        }

        private void OnStoreChanged()
        {
            Fill();
        }

        /// The greeting the phone build puts over the feed. It is picked once per
        /// visit rather than per paint, so the words do not change under the eye.
        private void Greet()
        {
            string avoid = _greet != null ? _greet.Message : "";
            _greet = Timely.Pick(avoid);
            _head.Eyebrow = _greet.Greeting;
            _head.Subtitle = _greet.Message;
        }

        public override void Lay()
        {
            if (Width < 40 || Height < 40) return;
            int y = Space.Lg;
            int hw = _head.Wants();
            int gw = Math.Max(Theme.Px(150), _grab.Wants());
            _grab.SetBounds(Width - Edge - gw, y + Math.Max(0, (hw - Theme.Px(34)) / 2), gw, Theme.Px(34));
            _head.SetBounds(Edge, y, Math.Max(60, Content - gw - Space.Md), hw);
            y += hw + Space.Md;

            bool roomy = Height > Theme.Px(620);
            bool tall = Height > Theme.Px(520);
            _tally.Visible = roomy;
            if (roomy)
            {
                _tally.SetBounds(Edge, y, Content, Theme.Px(52));
                y += Theme.Px(52) + Space.Sm;
            }
            _monitor.Visible = tall;
            if (tall)
            {
                _monitor.SetBounds(Edge, y, Content, Theme.Px(62));
                int tw = Theme.Px(44);
                _live.SetBounds(_monitor.Width - Space.Lg - tw, (_monitor.Height - Theme.Px(26)) / 2,
                    tw, Theme.Px(26));
                y += Theme.Px(62) + Space.Md;
            }
            _compose.Lines = roomy ? 3 : 2;
            int cw = _compose.Wants();
            _compose.SetBounds(Edge, y, Content, cw);
            y += cw + Space.Lg;

            int fw = Math.Min(Theme.Px(260), Math.Max(Theme.Px(140), Content / 3));
            _recent.SetBounds(Edge, y, Math.Max(40, Content - fw - Space.Md), Theme.Px(30));
            // Join mode parks a third button left of the toggle, so the header
            // keeps room for it only while it is on screen.
            _recent.Reserve = _joining ? Theme.Px(112) : Theme.Px(40);
            _resync.SetBounds(_recent.Right - Theme.Px(34), y - Theme.Px(1), Theme.Px(32), Theme.Px(32));
            _join.SetBounds(_recent.Right - Theme.Px(70), y - Theme.Px(1), Theme.Px(32), Theme.Px(32));
            _joinGo.SetBounds(_recent.Right - Theme.Px(106), y - Theme.Px(1), Theme.Px(32), Theme.Px(32));
            _find.SetBounds(Width - Edge - fw, y - Theme.Px(3), fw, Theme.Px(34));
            y += Theme.Px(30) + Space.Xs;

            _list.SetBounds(Edge, y, Content, Math.Max(Theme.Px(80), Height - y - Space.Md));
            _list.Reload();
        }
    }

    /// The monitor panel. It gets the accent edge while it is live, because on a
    /// page with one job the thing doing that job should look switched on.
    internal sealed class MonitorCard : Card
    {
        private bool _live;

        public string Title = "";

        public MonitorCard()
        {
            Radius = Radii.Inner;
            Inset = Space.Lg;
            Elevated = true;
        }

        public bool Live
        {
            get { return _live; }
            set { _live = value; Accented = value; Invalidate(); }
        }

        protected override void Paint2(Graphics g, Rectangle r)
        {
            int x = r.X + Space.Lg;
            int lh = Theme.Body.Height + Theme.Px(2);
            int y = r.Y + (r.Height - lh - Theme.Tiny.Height) / 2;
            int w = Math.Max(20, r.Width - Space.Lg * 2 - Theme.Px(56));
            Ui.Line(g, Title, Theme.BodyBold, Theme.OnSurface, new Rectangle(x, y, w, lh));
            string said = _live
                ? "Watching. Anything you copy lands in the feed."
                : "Paused. Nothing new is being kept.";
            Ui.Line(g, said, Theme.Tiny, _live ? Theme.Muted : Theme.Faint,
                new Rectangle(x, y + lh, w, Theme.Tiny.Height + Theme.Px(2)));
        }
    }
}
