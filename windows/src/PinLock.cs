// The privacy lock: a PIN over the whole app.
//
// The PIN never rests anywhere as text. What is stored is a salt and a
// SHA-256 over salt plus PIN, inside the settings file, which is itself
// sealed by the OS — so guessing at the lock screen is the only way in that
// does not go through the machine's own key store first.
//
// The lock screen is modal: unlocking closes it, and anything else quits the
// app outright. A lock that can be dismissed is decoration.

using System;
using System.Drawing;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal static class PinLock
    {
        public static bool Locked(AppSettings s)
        {
            return s != null && !string.IsNullOrEmpty(s.PinHash);
        }

        /// A fresh "salt:hash" for [pin], or null when it is too short.
        public static string Make(string pin)
        {
            if (string.IsNullOrEmpty(pin) || pin.Length < 4) return null;
            byte[] salt = new byte[16];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }
            return Hex(salt) + ":" + Hex(Hash(salt, pin));
        }

        public static bool Check(string stored, string pin)
        {
            if (string.IsNullOrEmpty(stored) || string.IsNullOrEmpty(pin))
                return false;
            string[] parts = stored.Split(':');
            if (parts.Length != 2) return false;
            try
            {
                byte[] salt = Unhex(parts[0]);
                return SlowEq(Hex(Hash(salt, pin)), parts[1]);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static byte[] Hash(byte[] salt, string pin)
        {
            byte[] body = Encoding.UTF8.GetBytes(pin ?? "");
            byte[] all = new byte[salt.Length + body.Length];
            Buffer.BlockCopy(salt, 0, all, 0, salt.Length);
            Buffer.BlockCopy(body, 0, all, salt.Length, body.Length);
            using (SHA256 sha = SHA256.Create()) return sha.ComputeHash(all);
        }

        private static string Hex(byte[] b)
        {
            char[] hex = "0123456789abcdef".ToCharArray();
            char[] out_ = new char[b.Length * 2];
            for (int i = 0; i < b.Length; i++)
            {
                out_[i * 2] = hex[(b[i] >> 4) & 15];
                out_[i * 2 + 1] = hex[b[i] & 15];
            }
            return new string(out_);
        }

        private static byte[] Unhex(string s)
        {
            byte[] out_ = new byte[s.Length / 2];
            for (int i = 0; i < out_.Length; i++)
            {
                out_[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
            }
            return out_;
        }

        private static bool SlowEq(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        /// The startup gate: true to run, false to quit. No PIN, no question.
        public static bool Startup()
        {
            AppSettings s;
            try
            {
                s = SettingsStore.Load();
            }
            catch (Exception)
            {
                return true;
            }
            if (!Locked(s)) return true;
            return Unlock(s);
        }

        /// The modal lock screen. Unlocking closes it; closing it quits.
        public static bool Unlock(AppSettings s)
        {
            using (PinForm f = new PinForm(s)) return f.ShowDialog() == DialogResult.OK;
        }

        /// A fresh salt:hash, or null when cancelled or mismatched.
        public static string Setup()
        {
            using (PinSetupForm f = new PinSetupForm())
            {
                return f.ShowDialog() == DialogResult.OK ? f.Hash : null;
            }
        }

        private sealed class PinForm : Form
        {
            private readonly AppSettings _settings;
            private readonly TextBox _pin = new TextBox();
            private readonly Label _error = new Label();

            public PinForm(AppSettings s)
            {
                _settings = s;
                Text = "ClipSync AI is locked";
                StartPosition = FormStartPosition.CenterScreen;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                ShowIcon = false;
                ClientSize = new Size(300, 168);

                Label title = new Label();
                title.Text = "ClipSync AI";
                title.Font = new Font(FontFamily.GenericSansSerif, 13, FontStyle.Bold);
                title.AutoSize = true;
                title.Location = new Point(20, 16);
                Controls.Add(title);

                Label hint = new Label();
                hint.Text = "Enter your PIN to open your clips.";
                hint.AutoSize = true;
                hint.Location = new Point(20, 44);
                Controls.Add(hint);

                _pin.UseSystemPasswordChar = true;
                _pin.Location = new Point(20, 70);
                _pin.Width = 260;
                _pin.KeyDown += delegate(object sender, KeyEventArgs e)
                {
                    if (e.KeyCode == Keys.Enter) TryUnlock();
                };
                Controls.Add(_pin);

                _error.ForeColor = Color.IndianRed;
                _error.AutoSize = true;
                _error.Location = new Point(20, 96);
                Controls.Add(_error);

                Button ok = new Button();
                ok.Text = "Unlock";
                ok.DialogResult = DialogResult.None;
                ok.Location = new Point(124, 122);
                ok.Width = 80;
                ok.Click += delegate { TryUnlock(); };
                Controls.Add(ok);
                AcceptButton = ok;

                Button quit = new Button();
                quit.Text = "Quit";
                quit.DialogResult = DialogResult.Cancel;
                quit.Location = new Point(210, 122);
                quit.Width = 70;
                Controls.Add(quit);
            }

            private void TryUnlock()
            {
                if (Check(_settings.PinHash, _pin.Text))
                {
                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                }
                _error.Text = "Wrong PIN, try again.";
                _pin.Text = "";
                _pin.Focus();
            }
        }

        private sealed class PinSetupForm : Form
        {
            private readonly TextBox _first = new TextBox();
            private readonly TextBox _second = new TextBox();
            private readonly Label _error = new Label();

            public string Hash { get; private set; }

            public PinSetupForm()
            {
                Text = "Lock with a PIN";
                StartPosition = FormStartPosition.CenterScreen;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                ShowIcon = false;
                ClientSize = new Size(300, 196);

                Label hint = new Label();
                hint.Text = "Minimum 4 digits. Your clips will ask for it on launch.";
                hint.AutoSize = true;
                hint.Location = new Point(20, 16);
                Controls.Add(hint);

                _first.UseSystemPasswordChar = true;
                _first.Location = new Point(20, 44);
                _first.Width = 260;
                Controls.Add(_first);

                _second.UseSystemPasswordChar = true;
                _second.Location = new Point(20, 72);
                _second.Width = 260;
                _second.KeyDown += delegate(object sender, KeyEventArgs e)
                {
                    if (e.KeyCode == Keys.Enter) TrySave();
                };
                Controls.Add(_second);

                _error.ForeColor = Color.IndianRed;
                _error.AutoSize = true;
                _error.Location = new Point(20, 98);
                Controls.Add(_error);

                Button ok = new Button();
                ok.Text = "Save";
                ok.Location = new Point(124, 148);
                ok.Width = 80;
                ok.Click += delegate { TrySave(); };
                Controls.Add(ok);
                AcceptButton = ok;

                Button cancel = new Button();
                cancel.Text = "Cancel";
                cancel.DialogResult = DialogResult.Cancel;
                cancel.Location = new Point(210, 148);
                cancel.Width = 70;
                Controls.Add(cancel);
            }

            private void TrySave()
            {
                if (_first.Text.Length < 4)
                {
                    _error.Text = "A PIN needs at least 4 digits.";
                    return;
                }
                if (_first.Text != _second.Text)
                {
                    _error.Text = "Those PINs did not match.";
                    _second.Text = "";
                    _second.Focus();
                    return;
                }
                Hash = Make(_first.Text);
                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }
}
