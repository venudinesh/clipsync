// The shell's keys, messages and the wheel.
//
// The window is the app's message sink. Four things arrive here and nowhere else:
// clipboard notifications, the two global shortcuts, the "come forward" broadcast
// from a second launch, and word from Windows that the scale or the system theme
// changed. Each is handed on to whatever owns it rather than handled in place.

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ClipSyncAI
{
    /// Sends the wheel to whatever the pointer is over. Windows sends it to
    /// whatever has focus, which means a list will not scroll until it is clicked,
    /// and clicking a list to scroll it moves the caret out of the box being typed
    /// in. The message is passed on untouched, so a surface with no scrolling of
    /// its own still lets it bubble to the one above it.
    internal sealed class Wheel : IMessageFilter
    {
        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != Native.WM_MOUSEWHEEL) return false;
            Form form = Form.ActiveForm;
            if (form == null || !form.Visible) return false;
            Control at = Deepest(form, Control.MousePosition);
            if (at == null || at == form || at.Handle == m.HWnd) return false;
            Native.Relay(at.Handle, Native.WM_MOUSEWHEEL, m.WParam, m.LParam);
            return true;
        }

        /// The innermost control under a screen point that can actually be seen.
        ///
        /// Asking a control which child sits at a point, without saying which
        /// children ought to count, is answered with any of them: hidden ones
        /// included. Every page this app is not showing is a hidden child the
        /// size of the whole stage, and a page keeps its window once it has been
        /// shown, so after the second destination was visited the front of the z
        /// order at almost any point was a page nobody was looking at. The wheel
        /// was handed to that page and swallowed there, and no surface in the app
        /// scrolled by wheel again until it was restarted. Disabled children are
        /// skipped for the reason Windows skips them, which is that they take no
        /// input. The depth cap is there because a control that answered with
        /// itself would otherwise spin here forever.
        internal static Control Deepest(Control root, Point screen)
        {
            Control at = root;
            for (int guard = 0; guard < 24; guard++)
            {
                Control kid;
                try
                {
                    kid = at.GetChildAtPoint(at.PointToClient(screen),
                        GetChildAtPointSkip.Invisible | GetChildAtPointSkip.Disabled);
                }
                catch (Exception) { return at; }
                if (kid == null || kid == at) return at;
                at = kid;
            }
            return at;
        }
    }

    internal sealed partial class AppWindow
    {
        private const int KeyShow = 1;
        private const int KeyTidy = 2;

        /// 0 nothing was asked for, 1 taken, 2 refused by Windows.
        private int _show;
        private int _tidy;
        private bool _tidying;

        /// Takes the two global shortcuts. Refusal is normal: another app may
        /// already own the combination, and this app has to go on working without
        /// it rather than refusing to start.
        private void Bind()
        {
            Loose();
            if (!IsHandleCreated) return;
            _show = Take(KeyShow, _hub.Settings.HotkeyOverlay);
            _tidy = Take(KeyTidy, _hub.Settings.HotkeyProcess);
        }

        private int Take(int id, string text)
        {
            Hotkey k = Hotkey.Parse(text);
            if (k == null) return 0;
            if (Native.BindHotkey(Handle, id, k.Mods, k.Vk)) return 1;
            Paths.Log("hotkey refused by the system: " + text);
            return 2;
        }

        private void Loose()
        {
            if (!IsHandleCreated) return;
            if (_show == 1) Native.ReleaseHotkey(Handle, KeyShow);
            if (_tidy == 1) Native.ReleaseHotkey(Handle, KeyTidy);
            _show = 0;
            _tidy = 0;
        }

        /// Re-takes the shortcuts after they were rewritten, and says so when
        /// Windows refuses. A shortcut that silently failed would sit in Settings
        /// looking bound.
        private void Rebind()
        {
            Bind();
            if (_show == 2 && _tidy == 2) _hub.Oops("Another app already owns both shortcuts");
            else if (_show == 2) _hub.Oops("Another app already owns " + _hub.Settings.HotkeyOverlay);
            else if (_tidy == 2) _hub.Oops("Another app already owns " + _hub.Settings.HotkeyProcess);
            else _hub.Say("Shortcuts updated");
        }

        protected override void WndProc(ref Message m)
        {
            if (_watch != null && _watch.OnMessage(ref m)) { m.Result = IntPtr.Zero; return; }
            if (m.Msg == Native.WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (id == KeyShow) Front();
                else if (id == KeyTidy) Tidy();
                return;
            }
            uint ask = m.Msg >= 0xC000 && m.Msg <= 0xFFFF ? Native.ShowMessage() : 0;
            if (ask != 0 && (uint)m.Msg == ask) { Front(); return; }
            if (m.Msg == Native.WM_DPICHANGED) { Rescale(); Suggested(m.LParam); Dressed(); }
            else if (m.Msg == Native.WM_SETTINGCHANGE) Systemed();
            base.WndProc(ref m);
        }

        /// Re-reads the scale from the window's own monitor. Called when the handle
        /// appears and again whenever the window crosses onto a screen that is
        /// scaled differently.
        private void Rescale()
        {
            if (!IsHandleCreated) return;
            double s = Native.ScaleOf(Handle);
            if (s < 0.75 || s > 5.0) s = 1.0;
            if (Math.Abs(s - Theme.Scale) < 0.01) return;
            Theme.Scale = s;
            Theme.BuildFonts();
            Popup.Skin(_menu);
            MinimumSize = new Size(Theme.Px(720), Theme.Px(520));
        }

        /// The rectangle Windows suggests for the new screen. Taking it is what
        /// keeps the window the same physical size when it is dragged from a
        /// laptop panel to an external monitor.
        private void Suggested(IntPtr rect)
        {
            if (rect == IntPtr.Zero) return;
            try
            {
                int l = Marshal.ReadInt32(rect, 0);
                int t = Marshal.ReadInt32(rect, 4);
                int r = Marshal.ReadInt32(rect, 8);
                int b = Marshal.ReadInt32(rect, 12);
                if (r > l && b > t) SetBounds(l, t, r - l, b - t);
            }
            catch (Exception ex) { Paths.Log("dpi rectangle", ex); }
        }

        /// Windows changed something app-wide. The only one that matters here is
        /// the light or dark preference, and only while the app is following it.
        private void Systemed()
        {
            if (!_hub.Settings.FollowSystemTheme) return;
            bool dark = Theme.SystemPrefersDark();
            if (dark == _hub.Settings.Dark) return;
            _hub.Settings.Dark = dark;
            _hub.SaveSettings();
            _hub.RaiseTheme();
        }

        /// The second shortcut: tidy what is on the clipboard and put it back,
        /// without the window coming forward. The formatter is the one the watcher
        /// uses, so a tidied clip and a captured one read alike, and the text that
        /// was replaced is offered back.
        private void Tidy()
        {
            if (_tidying) { Tell("Still tidying the last one", true); return; }
            string text = _hub.TakeClipboard();
            if (text == null || text.Trim().Length == 0)
            {
                Tell("Nothing on the clipboard", true);
                return;
            }
            _tidying = true;
            Thread t = new Thread(new ParameterizedThreadStart(Tidying));
            t.IsBackground = true;
            t.Name = "hotkey-tidy";
            t.Start(text);
        }

        private void Tidying(object state)
        {
            string was = (string)state;
            string got;
            try
            {
                got = _hub.Settings.AutoProcess
                    ? _hub.Brain.Process(was)
                    : ClipRegex.Process(was);
            }
            catch (Exception ex)
            {
                Paths.Log("hotkey tidy", ex);
                got = ClipRegex.Process(was);
            }
            try { BeginInvoke(new Action(delegate { Tidied(was, got); })); }
            catch (Exception) { _tidying = false; }
        }

        private void Tidied(string was, string got)
        {
            _tidying = false;
            if (got == null || got.Trim().Length == 0)
            {
                Tell("The formatter returned nothing", true);
                return;
            }
            if (got.Trim() == was.Trim()) { Tell("Already tidy", false); return; }
            if (!_hub.PutClipboard(got)) { Tell("The clipboard could not be written", true); return; }
            if (Visible) _hub.Undoable("Clipboard tidied", new Action(delegate { _hub.PutClipboard(was); }));
            else Tell("Clipboard tidied", false);
        }

        /// Destinations by number, and the visible page's own shortcuts. The page
        /// is asked before the focused control sees the key, which is why every
        /// page only claims combinations with a modifier: plain typing has to
        /// reach the box being typed in.
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_sheet != null && _sheet.IsOpen)
            {
                if (keyData == Keys.Escape) { _sheet.Close(); return true; }
                return base.ProcessCmdKey(ref msg, keyData);
            }
            if (_views.Length > 0 && (keyData & Keys.Control) == Keys.Control)
            {
                Keys plain = keyData & Keys.KeyCode;
                int n = Digit(plain);
                if (n >= 0 && n < _views.Length) { Go(n); return true; }
                if (plain == Keys.Tab)
                {
                    int step = (keyData & Keys.Shift) == Keys.Shift ? -1 : 1;
                    Go(((_at + step) % _views.Length + _views.Length) % _views.Length);
                    return true;
                }
            }
            if (_at >= 0 && _at < _views.Length)
            {
                if (_views[_at].Shortcut(new KeyEventArgs(keyData))) return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private static int Digit(Keys k)
        {
            if (k >= Keys.D1 && k <= Keys.D9) return (int)k - (int)Keys.D1;
            if (k >= Keys.NumPad1 && k <= Keys.NumPad9) return (int)k - (int)Keys.NumPad1;
            return -1;
        }
    }
}
