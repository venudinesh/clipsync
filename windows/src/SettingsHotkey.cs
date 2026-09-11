// Settings: recording a shortcut.
//
// A shortcut is recorded by pressing it, not typed into a box. Typing "Ctrl+Shift+V"
// asks a person to know the spelling this app happens to use, and a box that accepts
// text accepts text that cannot be bound. Pressing the keys can only produce a
// combination Windows will take.
//
// The press itself is the confirmation, so there is no Save button: the moment a
// usable combination arrives, it is kept and the sheet goes away.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class SettingsView
    {
        /// The recorder. Claims every key as input so Tab, Enter and the arrows
        /// are recordable instead of being taken by the sheet for navigation.
        private sealed class Catcher : Control
        {
            public Action<Hotkey> Set;
            public Action Bail;
            private string _shown;
            private string _hint = "Hold Ctrl, Shift or Alt, then press a key";

            public Catcher(string current)
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.UserPaint | ControlStyles.ResizeRedraw |
                         ControlStyles.Selectable, true);
                TabStop = true;
                BackColor = Theme.Base;
                _shown = current ?? "";
                AccessibleName = "Press a shortcut";
            }

            protected override bool IsInputKey(Keys keyData)
            {
                return true;
            }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                if (e.KeyCode == Keys.Escape)
                {
                    if (Bail != null) Bail();
                    return;
                }
                Hotkey got = Hotkey.FromKeyData(e.KeyData);
                if (got == null)
                {
                    _hint = "That needs Ctrl, Shift or Alt held down as well";
                    Invalidate();
                    return;
                }
                _shown = got.Text;
                Invalidate();
                if (Set != null) Set(got);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.Clear(BackColor);
                Ui.Q(g);
                Rectangle r = new Rectangle(0, 0, Math.Max(10, Width - 1), Math.Max(10, Height - 1));
                Ui.Fill(g, r, Radii.Inner, Palette.Alpha(Theme.OnSurface, Theme.Dark ? 0.06 : 0.04));
                Ui.Stroke(g, r, Radii.Inner, Theme.Outline, 1f);
                int hh = Theme.Small.Height + Theme.Px(6);
                Rectangle top = new Rectangle(r.X + Space.Md, r.Y,
                    Math.Max(10, r.Width - Space.Md * 2), Math.Max(10, r.Height - hh - Space.Md));
                Ui.Centre(g, _shown.Length > 0 ? _shown : "Waiting for a key", Theme.Display,
                    _shown.Length > 0 ? Theme.OnSurface : Theme.Faint, top);
                Ui.Centre(g, _hint, Theme.Small, Theme.Muted,
                    new Rectangle(top.X, top.Bottom, top.Width, hh));
            }
        }

        private Catcher _hotBody;
        private Action<string> _hotTake;
        private string _hotOther = "";

        /// Puts the recorder up. The caller says what the shortcut is now, what the
        /// other shortcut is using, and what to do with a new one, so both
        /// shortcuts share this.
        private void Record(string what, string current, string other, Action<string> take)
        {
            if (Hub.Sheet == null) return;
            _hotTake = take;
            _hotOther = other ?? "";
            Catcher body = new Catcher(current);
            _hotBody = body;
            body.Set = new Action<Hotkey>(Caught);
            body.Bail = new Action(Bailed);
            AppButton off = new AppButton();
            off.Label = "Turn it off";
            off.Look = ButtonLook.Danger;
            off.ShowIcon = true;
            off.Icon = Glyph.Close;
            off.Click += delegate { Caught(null); };
            Hub.Sheet.Open(what, "Press the keys you want to use from any app", body,
                Theme.Px(120), off);
            body.Focus();
        }

        private void Bailed()
        {
            _hotBody = null;
            _hotTake = null;
            Hub.Sheet.Close();
        }

        /// Takes the combination. Refuses one already used by the other shortcut,
        /// because Windows would give the second registration nothing and the page
        /// would then show two shortcuts where only one worked.
        private void Caught(Hotkey got)
        {
            if (_hotBody == null) return;
            string text = got == null ? "" : got.Text;
            if (text.Length > 0 && text == _hotOther)
            {
                Hub.Oops(text + " is already used by the other shortcut");
                return;
            }
            Action<string> take = _hotTake;
            _hotBody = null;
            _hotTake = null;
            Hub.Sheet.Close();
            if (take != null) take(text);
        }
    }
}
