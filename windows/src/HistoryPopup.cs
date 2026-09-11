// Clip history: the Win+V habit, without surrendering to it.
//
// A borderless popup near the pointer with the nine newest clips. Picking one
// puts it back on the clipboard and pastes it into whatever was in front, so a
// thing copied an hour ago is two keys away instead of a hunt through the feed.
// The hotkey toggles: pressed twice in a row it opens, then goes away.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed class HistoryPopup : Form
    {
        private readonly Hub _hub;
        private readonly ListBox _list = new ListBox();
        private readonly List<ClipEntry> _items = new List<ClipEntry>();

        public HistoryPopup(Hub hub)
        {
            _hub = hub;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.None;
            BackColor = Theme.Base;
            Width = Theme.Px(380);

            Label hint = new Label();
            hint.Text = "Clip history — Enter pastes · Esc closes";
            hint.ForeColor = Theme.Faint;
            hint.Font = Theme.Tiny;
            hint.AutoSize = false;
            hint.Height = Theme.Px(24);
            hint.Dock = DockStyle.Top;
            hint.TextAlign = ContentAlignment.MiddleLeft;
            hint.Padding = new Padding(Theme.Px(10), 0, 0, 0);
            Controls.Add(hint);

            _list.BorderStyle = BorderStyle.None;
            _list.BackColor = Theme.Base;
            _list.ForeColor = Theme.OnSurface;
            _list.Font = Theme.Body;
            _list.IntegralHeight = false;
            _list.Dock = DockStyle.Fill;
            _list.DoubleClick += delegate { Choose(); };
            _list.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { Choose(); e.Handled = true; }
                else if (e.KeyCode == Keys.Escape) { Close(); e.Handled = true; }
            };
            Controls.Add(_list);

            Fill();
            Place();
        }

        private void Fill()
        {
            List<ClipEntry> all = _hub.Clips.Items;
            for (int i = all.Count - 1; i >= 0 && _items.Count < 9; i--)
            {
                ClipEntry c = all[i];
                if (c == null) continue;
                _items.Add(c);
                string head = (c.Title ?? "").Trim();
                if (head.Length == 0) head = c.Preview(90);
                _list.Items.Add(head);
            }
            int rows = Math.Max(1, _items.Count);
            Height = Theme.Px(24) + rows * _list.ItemHeight + Theme.Px(8);
            if (_list.Items.Count > 0) _list.SelectedIndex = 0;
        }

        private void Place()
        {
            Point at = Cursor.Position;
            Rectangle room = Screen.GetWorkingArea(at);
            int x = Math.Min(at.X, room.Right - Width);
            int y = at.Y + Theme.Px(12);
            if (y + Height > room.Bottom) y = at.Y - Height - Theme.Px(12);
            Location = new Point(Math.Max(room.Left, x), Math.Max(room.Top, y));
        }

        private void Choose()
        {
            int i = _list.SelectedIndex;
            if (i < 0 || i >= _items.Count) return;
            ClipEntry c = _items[i];
            string body = (c.ProcessedMarkdown ?? "").Trim();
            if (body.Length == 0) body = c.RawText ?? "";
            if (body.Length == 0) return;
            Hide();
            if (!_hub.PutClipboard(body)) { Close(); return; }
            // Let the window that was in front take focus back before the keys
            // land, or they would paste into the popup's own corpse.
            Thread.Sleep(150);
            try
            {
                SendKeys.SendWait("^v");
            }
            catch (Exception ex)
            {
                Paths.Log("history paste", ex);
            }
            Close();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
