// Chat: the layout.
//
// Three bands: the masthead with the conversation's name and its three actions,
// the transcript, and the composer pinned to the bottom with the tone control on
// a thin strip above it. The composer never moves as a conversation grows, which
// is the one thing a chat window has to get right.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class ChatView
    {
        public override void Lay()
        {
            if (Width < 40 || Height < 40) return;
            int y = Space.Lg;
            int d = Theme.Px(32);
            int hw = _head.Wants();
            int top = y + Math.Max(0, (hw - d) / 2);
            int x = Width - Edge - d;
            _menu.SetBounds(x, top, d, d);
            x -= d + Space.Xs;
            _fresh.SetBounds(x, top, d, d);
            x -= d + Space.Xs;
            _past.SetBounds(x, top, d, d);
            _head.SetBounds(Edge, y, Math.Max(60, x - Edge - Space.Md), hw);
            y += Math.Max(hw, d) + Space.Md;

            int ch = _say.Wants();
            _say.SetBounds(Edge, Height - Space.Lg - ch, Content, ch);

            int th = Theme.Px(26);
            int tw = Math.Max(Theme.Px(104), _tone.Wants());
            int sy = _say.Top - th - Space.Xs;
            _tone.SetBounds(Edge, sy, tw, th);
            _strip = new Rectangle(Edge + tw + Space.Sm, sy,
                Math.Max(10, Content - tw - Space.Sm), th);

            _room = new Rectangle(Edge, y, Content, Math.Max(Theme.Px(80), sy - Space.Sm - y));
            bool empty = _msgs.Count == 0;
            _feed.Visible = !empty;
            _feed.SetBounds(_room.X, _room.Y, _room.Width, _room.Height);
            Cards(empty);
        }

        /// The openers, laid in centred rows that stack up from just above the
        /// composer. They wrap rather than shrink, because a button narrower than
        /// its label is worse than a button on the next line.
        private void Cards(bool empty)
        {
            bool room = empty && _room.Height >= Theme.Px(190);
            for (int i = 0; i < _openers.Count; i++) _openers[i].Visible = room;
            _openTop = _room.Bottom;
            if (!room) return;
            int h = Theme.Px(32);
            int yy = _room.Bottom - h - Space.Md;
            int i0 = 0;
            while (i0 < _openers.Count)
            {
                int wide = 0;
                int i1 = i0;
                while (i1 < _openers.Count)
                {
                    int w = Wide(i1);
                    if (i1 > i0 && wide + Space.Sm + w > _room.Width) break;
                    wide += (i1 > i0 ? Space.Sm : 0) + w;
                    i1++;
                }
                int xx = _room.X + Math.Max(0, (_room.Width - wide) / 2);
                for (int i = i0; i < i1; i++)
                {
                    int w = Wide(i);
                    _openers[i].SetBounds(xx, yy, w, h);
                    xx += w + Space.Sm;
                }
                _openTop = yy;
                yy -= h + Space.Sm;
                i0 = i1;
            }
        }

        private int Wide(int i)
        {
            return Math.Min(_room.Width, Math.Max(Theme.Px(96), _openers[i].Wants()));
        }

        /// The four openers. Each one fills the composer and leaves the caret at
        /// the end rather than sending, because every one of them is a sentence
        /// with the text still missing.
        private void Openers()
        {
            Opener("Summarise", "Summarize the text below:\r\n\r\n");
            Opener("Explain", "Explain this like I have ten minutes:\r\n\r\n");
            Opener("Checklist", "Turn these notes into a checklist:\r\n\r\n");
            Opener("Proofread", "Find the mistakes in this text:\r\n\r\n");
        }

        private void Opener(string label, string prompt)
        {
            string p = prompt;
            AppButton b = new AppButton();
            b.Look = ButtonLook.Outline;
            b.Label = label;
            b.ShowIcon = true;
            b.Icon = Glyph.Sparkle;
            b.Pad = Space.Md;
            b.AccessibleName = label;
            b.AccessibleDescription = "Fills the box, then you paste your text";
            b.Click += delegate { Seed(p); };
            _openers.Add(b);
            Controls.Add(b);
        }

        private void Seed(string prompt)
        {
            _say.Text = prompt;
            TextBox box = _say.Input.Box;
            box.Focus();
            box.SelectionStart = box.TextLength;
        }

        /// The tone control cycles rather than opening a menu: four settings on
        /// one axis, and clicking through them is quicker than picking from a
        /// list. The list is still in the chat sheet, with what each one means.
        private void OnTone(object sender, EventArgs e)
        {
            Heated((_heat + 1) % Heats.Length);
        }

        private void Heated(int i)
        {
            _heat = i < 0 || i >= Heats.Length ? 2 : i;
            Toned();
            Hub.Say(HeatBlurbs[_heat]);
        }

        private void Toned()
        {
            _tone.Label = HeatNames[_heat];
            _tone.AccessibleName = "Tone: " + HeatNames[_heat];
            _tone.AccessibleDescription = "Change how freely the model answers";
            Lay();
        }
    }
}
