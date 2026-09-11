// Settings: the clipboard.
//
// What the app takes and what it refuses. The numbers are pickers rather than
// boxes because there is no useful answer between 500 and 2000 clips, and a
// spinner on a settings page is an invitation to type 999999 and then wonder why
// the list is slow.

using System;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class SettingsView
    {
        private static readonly int[] Keeps = { 200, 500, 2000, 10000 };
        private static readonly int[] Floors = { 1, 2, 5, 20 };
        private static readonly int[] Waits = { 400, 800, 1500, 2000, 3000 };

        private Group Grab()
        {
            _watch.Changed += OnWatch;
            _auto.Changed += OnAuto;
            _safe.Changed += OnSafe;
            _keep.SetOptions(new string[] { "200", "500", "2000", "10000" }, 1);
            _keep.Changed += OnKeep;
            _floor.SetOptions(new string[] { "1", "2", "5", "20" }, 1);
            _floor.Changed += OnFloor;
            _wait.SetOptions(new string[] { "0.4s", "0.8s", "1.5s", "2s", "3s" }, 1);
            _wait.Changed += OnWait;

            _autoRow = Row("Tidy new clips by itself", "Runs the formatter on anything copied, " +
                "so a clip is already cleaned up when you come looking for it", _auto, 0);

            Group g = new Group("Clipboard");
            g.Add(Row("Watch the clipboard", "Everything you copy is kept here on this PC. " +
                "Nothing is uploaded, ever", _watch, 0));
            g.Add(_autoRow);
            g.Add(Row("Skip passwords and keys", "Anything that looks like a password, a card " +
                "number or an API key is not saved at all", _safe, 0));
            g.Add(Row("Keep at most", "The oldest go first when the list is full. Pinned clips " +
                "are never dropped", _keep, Theme.Px(250)));
            g.Add(Row("Ignore anything shorter than", "In characters. Stops a stray letter from " +
                "filling the list", _floor, Theme.Px(210)));
            g.Add(Row("Wait before taking a copy", "Long enough that holding Ctrl+C down, or an " +
                "app writing the clipboard twice, counts as one clip", _wait, Theme.Px(250)));
            return g;
        }

        private void OnWatch(object sender, EventArgs e)
        {
            if (_loading) return;
            Hub.Settings.CaptureEnabled = _watch.On;
            Hub.SaveSettings();
            Moots();
            Hub.Say(_watch.On ? "Watching the clipboard" : "Not watching any more");
        }

        private void OnAuto(object sender, EventArgs e)
        {
            if (_loading) return;
            Hub.Settings.AutoProcess = _auto.On;
            Hub.SaveSettings();
        }

        private void OnSafe(object sender, EventArgs e)
        {
            if (_loading) return;
            Hub.Settings.SkipSensitive = _safe.On;
            Hub.SaveSettings();
            if (!_safe.On) Hub.Say("Passwords will be saved like anything else now");
        }

        private void OnKeep(object sender, EventArgs e)
        {
            if (_loading) return;
            Hub.Settings.MaxHistory = Keeps[Math.Min(_keep.Index, Keeps.Length - 1)];
            Hub.SaveSettings();
        }

        private void OnFloor(object sender, EventArgs e)
        {
            if (_loading) return;
            Hub.Settings.MinLength = Floors[Math.Min(_floor.Index, Floors.Length - 1)];
            Hub.SaveSettings();
        }

        private void OnWait(object sender, EventArgs e)
        {
            if (_loading) return;
            Hub.Settings.DebounceMs = Waits[Math.Min(_wait.Index, Waits.Length - 1)];
            Hub.SaveSettings();
        }
    }
}
