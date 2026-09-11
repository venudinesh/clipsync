// The shell.
//
// One window: a rail down the left and one of five views showing at a time. All
// five are built at startup and switched by flipping Visible, so a half typed
// message, a scroll position or a picture waiting to be read survives a trip to
// Settings and back.
//
// The window is also the app's only message sink. Clipboard notifications, the two
// global hotkeys and the "come forward" broadcast from a second launch all need a
// window handle, and this is the handle that lives for the whole session.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class AppWindow : Form
    {
        private readonly Hub _hub;
        private readonly Watcher _watch;
        private readonly Rail _rail;
        private readonly Toast _toast;
        private readonly Sheet _sheet;
        private View[] _views = new View[0];
        private Mark _mark;
        private int _at = -1;

        public AppWindow()
        {
            _hub = new Hub();
            _hub.Boot();
            _watch = new Watcher(_hub);
            _rail = new Rail();
            _toast = new Toast();
            _sheet = new Sheet();
            Dress();
            _hub.Pump = this;
            _hub.Toast = _toast;
            _hub.Sheet = _sheet;
            Build();
            _hub.ThemeChanged += Dressed;
            _hub.HotkeysChanged += Rebind;
            _hub.StatusChanged += Standing;
            Application.AddMessageFilter(new Wheel());
        }

        /// The window itself. A minimum size that still holds the rail and a
        /// readable column beside it on a 1024 wide screen, which is the smallest
        /// desktop this app claims to support.
        private void Dress()
        {
            Text = "ClipSyncAI";
            Font = Theme.Body;
            BackColor = Theme.Canvas;
            ForeColor = Theme.OnSurface;
            DoubleBuffered = true;
            KeyPreview = true;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(Theme.Px(720), Theme.Px(520));
            ClientSize = Fits();
            _mark = new Mark(32, Theme.Accent);
            Icon = _mark.Icon;
        }

        /// A window that fits the screen it opens on. At 150% scale the wanted
        /// size is larger than a small laptop's desktop, and a window taller than
        /// the screen hides its own bottom edge.
        private static Size Fits()
        {
            Rectangle work = Screen.PrimaryScreen.WorkingArea;
            return new Size(
                Math.Min(Theme.Px(1060), Math.Max(660, work.Width - 96)),
                Math.Min(Theme.Px(700), Math.Max(470, work.Height - 96)));
        }

        private void Build()
        {
            _views = new View[]
            {
                new ClipsView(_hub, _watch),
                new ChatView(_hub),
                new NotesView(_hub),
                new CaptureView(_hub, _watch),
                new SettingsView(_hub)
            };
            string[] labels = new string[_views.Length];
            for (int i = 0; i < _views.Length; i++)
            {
                labels[i] = _views[i].Label;
                _views[i].AccessibleName = _views[i].Label;
                _views[i].Visible = false;
                Controls.Add(_views[i]);
            }
            _rail.SetItems(labels, 0);
            _rail.Changed += OnRail;
            _rail.AccessibleName = "Destinations";
            Controls.Add(_rail);
            Controls.Add(_toast);
            Controls.Add(_sheet);
            Tray();
            Standing();
            Go(0);
        }

        private void OnRail(object sender, EventArgs e)
        {
            Go(_rail.Index);
        }

        /// Switches destination. The view being left is told, so dictation stops
        /// and anything mid flight is put down rather than left running behind a
        /// page nobody is looking at.
        private void Go(int index)
        {
            if (_views.Length == 0) return;
            int want = index < 0 ? 0 : (index >= _views.Length ? _views.Length - 1 : index);
            if (want == _at) return;
            if (_at >= 0 && _at < _views.Length)
            {
                _views[_at].Visible = false;
                _views[_at].Hidden();
            }
            _at = want;
            View now = _views[_at];
            now.Bounds = Stage();
            now.Visible = true;
            now.Shown();
            // Laid out after it is filled, not before. Shown() is where a view
            // reads the store, and what comes back changes how much room the page
            // needs: a masthead with a greeting under it is taller than one with
            // only a name, and a section that has nothing to list takes no height
            // at all. Assigning Bounds above only re-lays the view when the size
            // actually changed, so this is the pass that measures the real words.
            now.Lay();
            _toast.BringToFront();
            _sheet.BringToFront();
            _rail.Index = _at;
            Text = "ClipSyncAI" + Say.Sep + now.Label;
        }

        private bool Narrow { get { return ClientSize.Width < Theme.Px(880); } }

        private int RailWidth
        {
            get { return Theme.Px(Narrow ? Space.RailCollapsed : Space.Rail); }
        }

        private Rectangle Stage()
        {
            int rail = RailWidth;
            return new Rectangle(rail, 0, Math.Max(10, ClientSize.Width - rail),
                Math.Max(10, ClientSize.Height));
        }

        public void Lay()
        {
            if (_views.Length == 0) return;
            _rail.Collapsed = Narrow;
            _rail.SetBounds(0, 0, RailWidth, ClientSize.Height);
            if (_at >= 0 && _at < _views.Length)
            {
                _views[_at].Bounds = Stage();
                _views[_at].Lay();
            }
            _sheet.SetBounds(0, 0, ClientSize.Width, ClientSize.Height);
            _sheet.Lay();
            _toast.Lay();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Lay();
        }

        /// Re-reads the theme everywhere. A settings page changes one colour and
        /// raises one event; this is the only thing that knows what is on screen
        /// and has to be told about it.
        private void Dressed()
        {
            Theme.Adopt(_hub.Settings);
            Theme.BuildFonts();
            Theme.Wear(this, Theme.Body);
            BackColor = Theme.Canvas;
            ForeColor = Theme.OnSurface;
            // The tray menu is not on screen, so nothing else here would reach it,
            // and the font it is holding has just been disposed. Some of the ways
            // Windows opens a drop down measure it before its Opening event, so it
            // is re-dressed now rather than left to catch up later.
            Popup.Skin(_menu);
            Remark();
            if (IsHandleCreated) Chrome.Apply(Handle);
            for (int i = 0; i < _views.Length; i++) _views[i].ApplyTheme();
            _rail.Invalidate();
            Standing();
            Lay();
            Invalidate(true);
        }

        /// The icon, redrawn in the new accent. The old one is disposed after the
        /// new one is in place, because the tray keeps painting the handle it was
        /// given until it is handed another.
        private void Remark()
        {
            Mark was = _mark;
            _mark = new Mark(32, Theme.Accent);
            Icon = _mark.Icon;
            if (_tray != null) _tray.Icon = _mark.Icon;
            if (was != null) was.Dispose();
        }

        /// The foot of the rail: whether a model is answering. Offline is a
        /// supported way to run this app, not a fault, so it gets a quiet dot
        /// rather than a warning colour.
        private void Standing()
        {
            EngineStatus s = _hub.Status;
            if (s == null || !s.Available)
            {
                _rail.SetStatus("Offline", Theme.Faint);
                return;
            }
            string model = _hub.Brain.Model;
            _rail.SetStatus(model != null && model.Length > 0 ? model : s.Kind, Palette.Success);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Chrome.Apply(Handle);
            Rescale();
            _watch.Start(Handle);
            Bind();
            _hub.Rediscover();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Lay();
            if (_hub.Settings.StartMinimised) Away();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Loose();
                _watch.Stop();
                Untray();
                if (_mark != null) { _mark.Dispose(); _mark = null; }
            }
            base.Dispose(disposing);
        }
    }
}
