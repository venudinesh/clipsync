// Chat: what the page paints itself.
//
// Two things only. The empty state, which is the first thing a new user sees and
// has to answer whether there is a model behind the window at all. And the thin
// strip over the composer, which says what is attached and who is answering.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class ChatView
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            Ui.Q(g);
            if (_msgs.Count == 0) Blank(g);
            Strip(g);
        }

        /// Nothing asked yet. It names the model that will answer, because the
        /// whole promise of the app is where the answer comes from, and a chat
        /// window that will not answer should say so before you type a paragraph.
        private void Blank(Graphics g)
        {
            bool ready = Hub.Brain.Ready;
            Rectangle r = new Rectangle(_room.X, _room.Y, _room.Width,
                Math.Max(Theme.Px(120), _openTop - _room.Y));
            int d = Theme.Px(56);
            int cx = r.X + (r.Width - d) / 2;
            int cy = r.Y + Math.Max(Space.Md, r.Height / 2 - Theme.Px(76));
            Rectangle disc = new Rectangle(cx, cy, d, d);
            Ui.Fill(g, disc, d / 2, Palette.Alpha(Theme.Accent, 0.12));
            int gi = Theme.Px(24);
            Icons.Draw(g, ready ? Glyph.Chat : Glyph.Sparkle,
                new Rectangle(cx + (d - gi) / 2, cy + (d - gi) / 2, gi, gi),
                Theme.AccentText, Math.Max(1.4f, Theme.Px(1.6)));
            Rectangle tr = new Rectangle(r.X + Space.Xl, disc.Bottom + Space.Md,
                Math.Max(1, r.Width - Space.Xl * 2), Theme.Px(26));
            Ui.Centre(g, ready ? "Ask it something" : "No model loaded",
                Theme.Title, Theme.OnSurface, tr);
            string body = ready
                ? "Answers are written on this PC by " + Hub.Brain.Model + ", and nothing you type leaves it"
                : "Open Settings, then AI, to point ClipSyncAI at a model on this PC";
            Ui.Text(g, body, Theme.Small, Theme.Muted,
                new Rectangle(tr.X, tr.Bottom + Theme.Px(2), tr.Width, Theme.Px(48)),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak);
            if (_openTop >= _room.Bottom) return;
            Ui.Text(g, "Start with one of these, then paste your text", Theme.Tiny, Theme.Faint,
                new Rectangle(r.X, _openTop - Theme.Tiny.Height - Theme.Px(7),
                    r.Width, Theme.Tiny.Height + Theme.Px(4)),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.SingleLine);
        }

        /// The strip over the composer. Painted rather than assembled from
        /// labels: it is two facts on one line and neither is clickable.
        private void Strip(Graphics g)
        {
            if (_strip.Width < Theme.Px(40)) return;
            string right = _busy ? "Generating"
                : (Hub.Brain.Ready ? Hub.Brain.Model : "No model loaded");
            int rw = Math.Min(_strip.Width, Ui.Measure(right, Theme.Small).Width + Theme.Px(4));
            Ui.Text(g, right, Theme.Small, _busy ? Theme.AccentText : Theme.Faint,
                new Rectangle(_strip.Right - rw, _strip.Y, rw, _strip.Height),
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            if (_files.Count == 0) return;
            int lw = _strip.Width - rw - Space.Md;
            if (lw < Theme.Px(60)) return;
            Ui.Line(g, Say.FirstLine(Named(), 64), Theme.Small, Theme.AccentText,
                new Rectangle(_strip.X, _strip.Y, lw, _strip.Height));
        }
    }
}
