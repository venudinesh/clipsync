// Settings: the window and the keyboard.
//
// Starting with Windows is written to the registry here rather than remembered as
// a preference and applied at startup, because the one place the setting has to be
// true is HKCU\...\Run and a preference that disagrees with it is a lie. The switch
// reads the key back, so removing the entry by hand shows up on this page.

using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ClipSyncAI
{
    internal sealed partial class SettingsView
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunName = "ClipSyncAI";

        private Group Shell()
        {
            _login.Changed += OnLogin;
            _small.Changed += OnSmall;
            _tray.Changed += OnTray;

            _hotShow.Look = ButtonLook.Outline;
            _hotShow.Click += OnHotShow;
            _hotDo.Look = ButtonLook.Outline;
            _hotDo.Click += OnHotDo;
            _hotHist.Look = ButtonLook.Outline;
            _hotHist.Click += OnHotHist;

            Group g = new Group("Window");
            g.Add(Row("Start with Windows", "Runs when you sign in, so the clipboard is being " +
                "kept from the moment you start work", _login, 0));
            g.Add(Row("Start minimised", "Starts in the tray with no window. Only useful with " +
                "the setting above", _small, 0));
            g.Add(Row("Closing keeps it running", "The X sends it to the tray instead of quitting. " +
                "Quit from the tray icon", _tray, 0));
            g.Add(Row("Show the window", "Brings ClipSyncAI to the front from any app", _hotShow,
                Theme.Px(190)));
            g.Add(Row("Tidy the clipboard", "Runs the formatter on whatever is copied right now, " +
                "without leaving the app you are in", _hotDo, Theme.Px(190)));
            g.Add(Row("Clip history", "A popup with your recent clips, to paste back " +
                "into whatever you are in", _hotHist, Theme.Px(190)));
            return g;
        }

        /// True when the Run entry exists and points at this executable. A copy
        /// moved to another folder is not the copy Windows would start, so the
        /// switch reads off rather than claiming a stale entry as its own.
        private static bool Logs()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (k == null) return false;
                    string got = k.GetValue(RunName) as string;
                    if (string.IsNullOrEmpty(got)) return false;
                    return got.IndexOf(Application.ExecutablePath,
                        StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch (Exception ex)
            {
                Paths.Log("autostart read", ex);
                return false;
            }
        }

        private void OnLogin(object sender, EventArgs e)
        {
            if (_loading) return;
            bool want = _login.On;
            string why = Enrol(want);
            if (why != null)
            {
                Hub.Oops(why);
                _loading = true;
                _login.Preset(Logs());
                _loading = false;
                return;
            }
            Hub.Settings.RunAtLogin = want;
            Hub.SaveSettings();
            Moots();
            Hub.Say(want ? "ClipSyncAI will start with Windows" : "It will not start by itself now");
        }

        /// Writes or clears the Run entry. Returns null on success, or a sentence
        /// to show, because a locked-down machine can refuse this and a switch
        /// that flips with nothing happening is worse than one that explains.
        private static string Enrol(bool want)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (k == null) return "Windows would not let this app write that setting";
                    if (want) k.SetValue(RunName, "\"" + Application.ExecutablePath + "\"");
                    else k.DeleteValue(RunName, false);
                }
                return null;
            }
            catch (Exception ex)
            {
                Paths.Log("autostart write", ex);
                return "That setting could not be saved. Your PC may not allow apps to " +
                       "start themselves";
            }
        }

        private void OnSmall(object sender, EventArgs e)
        {
            if (_loading) return;
            Hub.Settings.StartMinimised = _small.On;
            Hub.SaveSettings();
        }

        private void OnTray(object sender, EventArgs e)
        {
            if (_loading) return;
            Hub.Settings.CloseToTray = _tray.On;
            Hub.SaveSettings();
        }

        private void OnHotShow(object sender, EventArgs e)
        {
            Record("Show the window", Hub.Settings.HotkeyOverlay, Hub.Settings.HotkeyProcess,
                new Action<string>(TookShow));
        }

        private void OnHotDo(object sender, EventArgs e)
        {
            Record("Tidy the clipboard", Hub.Settings.HotkeyProcess, Hub.Settings.HotkeyOverlay,
                new Action<string>(TookDo));
        }

        private void OnHotHist(object sender, EventArgs e)
        {
            Record("Clip history", Hub.Settings.HotkeyHistory, Hub.Settings.HotkeyOverlay,
                new Action<string>(TookHist));
        }

        private void TookShow(string text)
        {
            Hub.Settings.HotkeyOverlay = text;
            Hub.SaveSettings();
            Fresh();
            Hub.RaiseHotkeys();
            Hub.Say(text.Length == 0 ? "That shortcut is off" : "Press " + text + " from anywhere");
        }

        private void TookDo(string text)
        {
            Hub.Settings.HotkeyProcess = text;
            Hub.SaveSettings();
            Fresh();
            Hub.RaiseHotkeys();
            Hub.Say(text.Length == 0 ? "That shortcut is off" : "Press " + text + " from anywhere");
        }

        private void TookHist(string text)
        {
            Hub.Settings.HotkeyHistory = text;
            Hub.SaveSettings();
            Fresh();
            Hub.RaiseHotkeys();
            Hub.Say(text.Length == 0 ? "That shortcut is off" : "Press " + text + " from anywhere");
        }
    }
}
