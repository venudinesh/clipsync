// Settings: the page.
//
// One scrolling column of cards. Every switch says in a sentence underneath what
// it does, because a settings page is the one screen where a person is looking for
// something they have never seen before and guessing wrong is expensive.
//
// Nothing here has a Save button. A change is written to disk the moment it is
// made and applied at once, which is what a preferences window on a desktop has
// done for twenty years.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class SettingsView : View
    {
        private readonly PageHead _head = new PageHead();
        private readonly Scroller _feed = new Scroller();
        private readonly List<Group> _cards = new List<Group>();

        private readonly Segmented _theme = new Segmented();
        private readonly Toggle _amoled = new Toggle();
        private readonly Swatches _accent = new Swatches();
        private readonly Toggle _dense = new Toggle();
        private readonly Toggle _motion = new Toggle();
        private Pref _amoledRow;

        private readonly Toggle _watch = new Toggle();
        private readonly Toggle _auto = new Toggle();
        private readonly Toggle _safe = new Toggle();
        private readonly Segmented _keep = new Segmented();
        private readonly Segmented _floor = new Segmented();
        private readonly Segmented _wait = new Segmented();
        private Pref _autoRow;

        private readonly Segmented _kind = new Segmented();
        private readonly Toggle _find = new Toggle();
        private readonly Field _url = new Field();
        private readonly AppButton _model = new AppButton();
        private readonly AppButton _vision = new AppButton();
        private readonly AppButton _test = new AppButton();
        private readonly Segmented _cloud = new Segmented();
        private readonly Field _key = new Field();
        private readonly Field _baseUrl = new Field();
        private readonly AppButton _download = new AppButton();
        private readonly AppButton _import = new AppButton();
        private readonly AppButton _load = new AppButton();
        private Pref _findRow;
        private Pref _urlRow;
        private Pref _modelRow;
        private Pref _visionRow;
        private Pref _testRow;
        private Pref _cloudRow;
        private Pref _keyRow;
        private Pref _baseUrlRow;
        private Pref _downloadRow;
        private Pref _importRow;
        private Pref _loadRow;

        private readonly Toggle _login = new Toggle();
        private readonly Toggle _small = new Toggle();
        private readonly Toggle _tray = new Toggle();
        private readonly AppButton _hotShow = new AppButton();
        private readonly AppButton _hotDo = new AppButton();

        private readonly Field _tess = new Field();
        private readonly AppButton _voice = new AppButton();
        private Pref _voiceRow;

        private readonly AppButton _folder = new AppButton();
        private readonly AppButton _export = new AppButton();
        private readonly AppButton _wipe = new AppButton();

        private int[] _high = new int[0];
        private bool _loading;

        public SettingsView(Hub hub) : base(hub)
        {
            _head.Title = "Settings";
            _feed.Extent = Extent;
            _feed.Arrange = Place;
            _feed.AccessibleName = "Settings";
            Controls.Add(_head);
            Controls.Add(_feed);
            Build();
            Load();
            Told();
            Hub.StatusChanged += OnEngine;
        }

        public override string Label { get { return "Settings"; } }

        public override void Shown()
        {
            Load();
            Told();
            Lay();
        }

        public override void ApplyTheme()
        {
            base.ApplyTheme();
            _url.ApplyTheme();
            _tess.ApplyTheme();
            for (int i = 0; i < _cards.Count; i++) _cards[i].Invalidate();
            Lay();
        }

        public override bool Shortcut(KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Z) return Hub.Undo();
            return false;
        }

        private void Build()
        {
            _cards.Add(Look());
            _cards.Add(Grab());
            _cards.Add(Brain());
            _cards.Add(Shell());
            _cards.Add(Tools());
            _cards.Add(Data());
            for (int i = 0; i < _cards.Count; i++) _feed.Controls.Add(_cards[i]);
        }

        /// A row, built in one line at the call site. The trailing control's width
        /// is set here because every kind of control in this page has a different
        /// idea of how much room it needs and the row does not care.
        private static Pref Row(string label, string detail, Control trail, int width)
        {
            Pref p = new Pref();
            p.Label = label;
            p.Detail = detail;
            if (trail != null && width > 0) trail.Width = width;
            p.Trail = trail;
            return p;
        }

        private static Pref Under(string label, string detail, Control trail)
        {
            Pref p = Row(label, detail, trail, 0);
            p.Below = true;
            return p;
        }

        private int Extent()
        {
            int w = _feed.Room - Edge * 2;
            if (_high.Length != _cards.Count) _high = new int[_cards.Count];
            int h = Space.Sm;
            for (int i = 0; i < _cards.Count; i++)
            {
                _high[i] = _cards[i].Wants(w);
                h += _high[i] + Space.Lg;
            }
            return h + Space.Xl;
        }

        private void Place(int off)
        {
            int w = _feed.Room - Edge * 2;
            int y = Space.Sm - off;
            for (int i = 0; i < _cards.Count; i++)
            {
                Group c = _cards[i];
                int h = i < _high.Length ? _high[i] : c.Height;
                Rectangle want = new Rectangle(Edge, y, w, h);
                if (c.Bounds != want) c.Bounds = want;
                c.Lay();
                y += h + Space.Lg;
            }
        }

        public override void Lay()
        {
            int hh = Math.Max(Theme.Px(52), _head.Wants());
            _head.SetBounds(Edge, Space.Lg, Content, hh);
            int y = Space.Lg + hh + Space.Md;
            _feed.SetBounds(0, y, Width, Math.Max(Theme.Px(80), Height - y));
            _feed.Reload();
        }

        private void OnEngine()
        {
            Told();
            Fresh();
        }
    }
}
