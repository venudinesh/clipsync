// The shell's tray half.
//
// A clipboard app spends most of its life not on screen, so the notification area
// is where it actually lives: the window closing puts it away rather than ending
// it, and the handle stays alive, which is what keeps the clipboard listener and
// the two global shortcuts working while nothing is visible.
//
// Anything worth saying while the window is away is said through the tray, because
// a toast painted on a hidden window is a message nobody receives.

using System;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class AppWindow
    {
        private NotifyIcon _tray;
        private ContextMenuStrip _menu;
        private bool _quitting;
        private bool _told;

        /// The tray icon carries the same three things the window does, in the
        /// order they are wanted: come forward, tidy what is on the clipboard,
        /// and stop.
        private void Tray()
        {
            _menu = Popup.Make();
            _menu.Items.Add(Popup.Item("Open ClipSyncAI", OnTrayOpen));
            _menu.Items.Add(Popup.Item("Tidy the clipboard", OnTrayTidy));
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(Popup.Item("Quit", OnTrayQuit));
            _tray = new NotifyIcon();
            _tray.Icon = _mark.Icon;
            _tray.Text = "ClipSyncAI";
            _tray.ContextMenuStrip = _menu;
            _tray.DoubleClick += OnTrayOpen;
            _tray.Visible = true;
        }

        private void Untray()
        {
            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
                _tray = null;
            }
            if (_menu != null) { _menu.Dispose(); _menu = null; }
        }

        /// Brings the window forward from wherever it is. Restoring comes before
        /// activating, because activating a minimised window leaves it minimised.
        /// ShowInTaskbar is never touched: changing it rebuilds the handle, and
        /// the handle is what the clipboard listener and the shortcuts are bound
        /// to.
        private void Front()
        {
            bool was = Visible;
            if (!was) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            if (was) return;
            if (_at >= 0 && _at < _views.Length) _views[_at].Shown();
            Lay();
        }

        /// Puts the window away. The page being left is told, so dictation stops
        /// rather than holding the microphone open behind nothing.
        private void Away()
        {
            if (_at >= 0 && _at < _views.Length) _views[_at].Hidden();
            Hide();
        }

        /// Says something through whichever surface can actually be seen.
        private void Tell(string text, bool bad)
        {
            if (Visible && WindowState != FormWindowState.Minimized)
            {
                if (bad) _hub.Oops(text); else _hub.Say(text);
                return;
            }
            if (_tray == null) return;
            try
            {
                _tray.BalloonTipTitle = "ClipSyncAI";
                _tray.BalloonTipText = text;
                _tray.BalloonTipIcon = bad ? ToolTipIcon.Warning : ToolTipIcon.Info;
                _tray.ShowBalloonTip(4000);
            }
            catch (Exception ex) { Paths.Log("tray balloon", ex); }
        }

        /// Closing the window puts the app away when Settings says so. Quit from
        /// the tray and Windows shutting down both close for real. The reassurance
        /// is shown once a session: every time would be nagging.
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            bool away = !_quitting
                && e.CloseReason == CloseReason.UserClosing
                && _hub.Settings.CloseToTray;
            if (away)
            {
                e.Cancel = true;
                Away();
                if (!_told)
                {
                    _told = true;
                    Tell("ClipSyncAI is still running in the notification area", false);
                }
                return;
            }
            if (_at >= 0 && _at < _views.Length) _views[_at].Hidden();
            base.OnFormClosing(e);
        }

        private void OnTrayOpen(object sender, EventArgs e)
        {
            Front();
        }

        private void OnTrayTidy(object sender, EventArgs e)
        {
            Tidy();
        }

        private void OnTrayQuit(object sender, EventArgs e)
        {
            _quitting = true;
            Close();
        }
    }
}
