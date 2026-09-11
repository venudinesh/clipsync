// Capture: the page.
//
// Two ways to get words in without typing them: read them off the screen, or say
// them. On the phone this was a camera and a recorder. A desktop has no camera worth
// pointing at a page and no reason to record then transcribe, so the camera becomes
// a region of the screen and the recorder becomes live dictation through the
// recogniser Windows already ships.
//
// Each half keeps its own text. Switching to dictation does not show what was
// scanned, because those are two separate pieces of work and one of them may not be
// finished with.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class CaptureView : View
    {
        private readonly PageHead _head = new PageHead();
        private readonly Segmented _mode = new Segmented();
        private readonly string[] _got = new string[] { "", "" };

        /// The clipboard watcher, so "send to clips" files text the same way a copy
        /// is filed rather than reinventing the rules.
        private readonly Watcher _watch;

        private int _tab;
        private bool _loading;

        public CaptureView(Hub hub, Watcher watch) : base(hub)
        {
            _watch = watch;
            _head.Title = "Capture";
            _mode.SetOptions(new string[] { "Scan", "Dictate" }, 0);
            _mode.AccessibleName = "Scan or dictate";
            _mode.Changed += OnMode;
            Controls.Add(_head);
            Controls.Add(_mode);
            BuildScan();
            BuildVoice();
            BuildResult();
            Mode();
            Hub.StatusChanged += OnEngine;
        }

        public override string Label { get { return "Capture"; } }

        public override void Shown()
        {
            Lead();
            Lay();
        }

        /// Leaving the page stops the microphone. Dictation nobody is watching is a
        /// recording light on for no reason.
        public override void Hidden()
        {
            Listen(false);
        }

        public override void ApplyTheme()
        {
            base.ApplyTheme();
            _text.Face = Theme.Body;
            _text.ApplyTheme();
            _scan.Invalidate();
            _voice.Invalidate();
            _result.Invalidate();
            Lay();
        }

        public override bool Shortcut(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && _ears != null && _ears.Live) { Listen(false); return true; }
            if (e.Control && e.KeyCode == Keys.Z) return Hub.Undo();
            return false;
        }

        private void OnEngine()
        {
            Lead();
        }

        private void OnMode(object sender, EventArgs e)
        {
            int want = _mode.Index == 1 ? 1 : 0;
            if (want == _tab) return;
            if (_tab == 1) Listen(false);
            _tab = want;
            Mode();
            Lead();
            Lay();
        }

        /// Which half is on show, and the text that belongs to it.
        private void Mode()
        {
            bool scan = _tab == 0;
            _scan.Visible = scan;
            _voice.Visible = !scan;
            _loading = true;
            _text.Text = Lines(_got[_tab]);
            _loading = false;
            Told();
        }

        /// The line under the title. It names what will do the work, because that is
        /// the one thing on this page nobody can see, and it changes with what is in
        /// Settings.
        private void Lead()
        {
            _head.Subtitle = _tab == 0 ? ScanLead() : VoiceLead();
            _head.Invalidate();
            Told();
        }

        public override void Lay()
        {
            if (Width < 40 || Height < 40) return;
            int y = Space.Lg;
            int hw = _head.Wants();
            int mw = Theme.Px(180);
            _mode.SetBounds(Width - Edge - mw, y + Math.Max(0, (hw - Theme.Px(34)) / 2),
                mw, Theme.Px(34));
            _head.SetBounds(Edge, y, Math.Max(60, Content - mw - Space.Md), hw);
            y += hw + Space.Md;

            int top = _tab == 0
                ? Theme.Px(Theme.Dense ? 172 : 196)
                : Theme.Px(Theme.Dense ? 130 : 148);
            Card card = _tab == 0 ? (Card)_scan : _voice;
            card.SetBounds(Edge, y, Content, top);
            if (_tab == 0) LayScan(); else LayVoice();
            y += top + Space.Lg;

            int rest = Math.Max(Theme.Px(120), Height - y - Space.Lg);
            _result.SetBounds(Edge, y, Content, rest);
            LayResult();
        }

        /// Hands work back to the interface thread. Reading a picture and listening
        /// for speech both finish somewhere else.
        private void Post(Action done)
        {
            Control pump = Hub.Pump != null ? Hub.Pump : this;
            if (!pump.IsHandleCreated) { done(); return; }
            try { pump.BeginInvoke(done); }
            catch (Exception ex) { Paths.Log("capture post", ex); }
        }

        /// Windows line endings for the text box, which shows a lone newline as a
        /// box rather than a break.
        private static string Lines(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "\r\n");
        }

        private static string Flat(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("\r\n", "\n").Replace('\r', '\n');
        }
    }
}
