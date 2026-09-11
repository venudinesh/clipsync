// Settings: choosing a model.
//
// A machine with Ollama on it can hold thirty models with names like
// qwen2.5-coder:7b-instruct-q4_K_M, so this is a list that scrolls with a filter
// over it rather than a row of buttons. One click chooses: nothing here is
// destructive and a second press to confirm a name would only be in the way.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class SettingsView
    {
        /// The sheet's body: a filter over a ledger, wrapped because a sheet takes
        /// one control and these two belong together.
        private sealed class Picker : Control
        {
            public readonly Field Find = new Field();
            public readonly Ledger List = new Ledger();

            public Picker()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                TabStop = false;
                BackColor = Theme.Base;
                Find.ShowIcon = true;
                Find.Icon = Glyph.Search;
                Find.Placeholder = "Filter by name";
                List.AccessibleName = "Models";
                List.EmptyGlyph = Glyph.Search;
                List.EmptyTitle = "No match";
                List.EmptyBody = "Try part of the name, such as qwen or llama.";
                Controls.Add(Find);
                Controls.Add(List);
            }

            public void Lay()
            {
                int h = Theme.Px(34);
                int w = Math.Max(10, Width);
                Find.SetBounds(0, 0, w, h);
                List.SetBounds(0, h + Space.Sm, w, Math.Max(10, Height - h - Space.Sm));
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                Lay();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(BackColor);
            }
        }

        private Picker _pickBody;
        private readonly List<string> _pickAll = new List<string>();
        private readonly List<string> _pickShown = new List<string>();
        private string _pickNow = "";
        private Action<string> _pickTake;

        /// Puts the picker up. The caller says what is chosen now and what to do
        /// with a choice, so one list serves both the model and the vision model.
        private void Choose(string title, string subtitle, List<string> items, string current,
            Action<string> take)
        {
            if (Hub.Sheet == null) return;
            _pickAll.Clear();
            for (int i = 0; i < items.Count; i++) _pickAll.Add(items[i]);
            _pickNow = current ?? "";
            _pickTake = take;
            Picker body = new Picker();
            _pickBody = body;
            body.Find.Edited += delegate { PickFill(); };
            body.Find.Submitted += delegate { PickTake(0); };
            body.List.Count = delegate { return _pickShown.Count; };
            body.List.HeightOf = PickHigh;
            body.List.PaintRow = PickRow;
            body.List.Activated += PickTake;
            body.List.Clicked += PickClick;
            Hub.Sheet.Open(title, subtitle, body, Theme.Px(320));
            PickFill();
            body.Lay();
            body.Find.Box.Focus();
        }

        private void PickFill()
        {
            Picker body = _pickBody;
            if (body == null) return;
            string q = body.Find.Text.Trim();
            _pickShown.Clear();
            for (int i = 0; i < _pickAll.Count; i++)
            {
                string name = _pickAll[i];
                if (q.Length == 0 ||
                    name.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0)
                {
                    _pickShown.Add(name);
                }
            }
            body.List.Reload();
            int at = _pickShown.IndexOf(_pickNow);
            body.List.Selected = at < 0 ? 0 : at;
        }

        private int PickHigh(int i)
        {
            return Theme.Px(Theme.Dense ? 36 : 42);
        }

        private void PickRow(Graphics g, Rectangle r, int i, bool hot, bool live)
        {
            if (i < 0 || i >= _pickShown.Count) return;
            string name = _pickShown[i];
            bool now = name == _pickNow || (_pickNow.Length == 0 && name == "None");
            Rectangle plate = RowKit.Plate(r);
            RowKit.Wash(g, plate, hot, live || now);
            int d = Theme.Px(16);
            int cy = plate.Y + (plate.Height - d) / 2;
            Icons.Draw(g, name == "None" ? Glyph.Close : Glyph.Sparkle,
                new Rectangle(plate.X + Space.Md, cy, d, d),
                now ? Theme.AccentText : Theme.Muted, Math.Max(1.3f, Theme.Px(1.5)));
            int tx = plate.X + Space.Md + d + Space.Md;
            int tw = Math.Max(20, plate.Right - tx - Space.Xl - d);
            Ui.Line(g, name, now ? Theme.BodyBold : Theme.Body,
                now ? Theme.OnSurface : Theme.Muted, new Rectangle(tx, plate.Y, tw, plate.Height));
            if (now)
            {
                Icons.Draw(g, Glyph.Check, new Rectangle(plate.Right - d - Space.Md, cy, d, d),
                    Theme.AccentText, Math.Max(1.5f, Theme.Px(1.8)));
            }
        }

        private void PickClick(int i, Point p)
        {
            PickTake(i);
        }

        /// Takes the choice and closes. The body is dropped first, because a click
        /// arrives as both Clicked and Activated on a double press and the second
        /// one would otherwise reach a list the sheet has already disposed.
        private void PickTake(int i)
        {
            if (_pickBody == null || i < 0 || i >= _pickShown.Count) return;
            string name = _pickShown[i];
            Action<string> take = _pickTake;
            _pickBody = null;
            _pickTake = null;
            Hub.Sheet.Close();
            if (take != null) take(name);
        }
    }
}
