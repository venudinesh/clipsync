// Settings: appearance.
//
// The theme picker has three positions rather than a dark switch, because
// following Windows is the setting most people want and a switch cannot say it.
// AMOLED black is greyed out in a light theme instead of hidden, so the page does
// not change shape when the theme changes and the option can be found again.

using System;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class SettingsView
    {
        private Group Look()
        {
            _theme.SetOptions(new string[] { "System", "Light", "Dark" }, 0);
            _theme.Changed += OnTheme;
            _amoled.Changed += OnAmoled;
            _accent.Changed += OnAccent;
            _dense.Changed += OnDense;
            _motion.Changed += OnMotion;

            _amoledRow = Row("True black", "Turns the dark theme's off-black surfaces to pure " +
                "black, which some screens like and shadows do not", _amoled, 0);

            Group g = new Group("Appearance");
            g.Add(Row("Theme", "System follows the light and dark setting in Windows",
                _theme, Theme.Px(210)));
            g.Add(_amoledRow);
            g.Add(Row("Accent", "Used for the selected tab, links and anything the app is " +
                "asking you to press", _accent, 0));
            g.Add(Row("Compact spacing", "Fits more on screen by tightening every row and " +
                "shrinking the text a little", _dense, 0));
            g.Add(Row("Reduce motion", "Turns off the sliding and fading. Everything still " +
                "works, it just arrives instantly", _motion, 0));
            return g;
        }

        private void OnTheme(object sender, EventArgs e)
        {
            if (_loading) return;
            int i = _theme.Index;
            Hub.Settings.FollowSystemTheme = i == 0;
            Hub.Settings.Dark = i == 0 ? Theme.SystemPrefersDark() : i == 2;
            Skin();
        }

        private void OnAmoled(object sender, EventArgs e)
        {
            if (_loading) return;
            Hub.Settings.Amoled = _amoled.On;
            Skin();
        }

        private void OnAccent(object sender, EventArgs e)
        {
            if (_loading) return;
            Hub.Settings.Accent = _accent.Chosen;
            Skin();
        }

        private void OnDense(object sender, EventArgs e)
        {
            if (_loading) return;
            Hub.Settings.Dense = _dense.On;
            Skin();
        }

        private void OnMotion(object sender, EventArgs e)
        {
            if (_loading) return;
            Hub.Settings.ReduceMotion = _motion.On;
            Skin();
        }

        /// Writes the change and tells the shell to re-dress the whole window.
        /// Done through the hub rather than by reaching for the form, because a
        /// view that knows what is above it is a view that cannot be moved.
        private void Skin()
        {
            Hub.SaveSettings();
            Hub.RaiseTheme();
            Moots();
            Lay();
        }
    }
}
