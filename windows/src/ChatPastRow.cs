// Chat history: a row, and what a click on one does.
//
// The same margin entry the clips and notes lists print, through the same RowKit:
// when it was last spoken to down the left, a rule, then the title with the last
// thing said under it. Two actions appear in the row the pointer is in, and a
// click anywhere else opens the conversation, because that is what a list of
// conversations is for.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class ChatView
    {
        private static readonly Glyph[] PastActions = new Glyph[] { Glyph.Pin, Glyph.Trash };

        private int PastHigh(int i)
        {
            return Theme.Px(Theme.Dense ? 16 : 22) + Theme.Body.Height +
                   Theme.Small.Height + Theme.Px(1) + Theme.Tiny.Height + Theme.Px(2);
        }

        private void PastRow(Graphics g, Rectangle r, int i, bool hot, bool live)
        {
            if (i < 0 || i >= _hist.Count) return;
            ChatSession s = _hist[i];
            Rectangle plate = RowKit.Plate(r);
            RowKit.Wash(g, plate, hot, live);
            RowKit.Margin(g, plate, Say.Stamp(s.UpdatedAt), s.IsPinned);

            string title = (s.Title ?? "").Trim();
            bool named = title.Length > 0 && title != "New chat";
            string last = PastLast(s);
            int n = 0;
            for (int k = 0; k < s.Messages.Count; k++)
            {
                if (s.Messages[k].Role != "system") n++;
            }
            string filing = Say.Join(n == 0 ? "Empty conversation" : Say.Plural(n, "message"),
                s.Model ?? "", s == _session ? "open now" : "");

            int tx = RowKit.TextLeft(plate);
            int tw = RowKit.TextWidth(plate, hot, PastActions.Length);
            int lh = Theme.Body.Height + Theme.Px(2);
            int ph = Theme.Small.Height + Theme.Px(1);
            int fh = Theme.Tiny.Height + Theme.Px(2);
            int ty = plate.Y + Math.Max(0, (plate.Height - lh - ph - fh) / 2);
            Ui.Line(g, named ? title : "Untitled chat", Theme.Body,
                named ? Theme.OnSurface : Theme.Faint, new Rectangle(tx, ty, tw, lh));
            Ui.Line(g, last.Length > 0 ? last : "Nothing was said", Theme.Small,
                last.Length > 0 ? Theme.Muted : Theme.Faint, new Rectangle(tx, ty + lh, tw, ph));
            Ui.Line(g, filing, Theme.Tiny, Theme.Faint, new Rectangle(tx, ty + lh + ph, tw, fh));
            if (hot) RowKit.Zones(g, plate, PastActions, s.IsPinned ? 0 : -1);
        }

        /// The last thing said, whoever said it. A question is marked as one, so a
        /// conversation that ended on a question does not read as an answer.
        private static string PastLast(ChatSession s)
        {
            for (int i = s.Messages.Count - 1; i >= 0; i--)
            {
                ChatMessage m = s.Messages[i];
                if (m.Role == "system") continue;
                string body = string.IsNullOrEmpty(m.Shown) ? m.Content : m.Shown;
                if (body == null || body.Trim().Length == 0) continue;
                return (m.Role == "user" ? "You: " : "") + Say.FirstLine(body, 180);
            }
            return "";
        }

        private void PastClick(int i, Point p)
        {
            if (_pastBody == null || i < 0 || i >= _hist.Count) return;
            Rectangle plate = RowKit.PlateOf(_pastBody.List.RowBounds(i));
            int k = RowKit.Hit(plate, p, PastActions.Length);
            if (k == 0) { PastPin(i); return; }
            if (k == 1) { PastKill(i); return; }
            PastOpen(i);
        }

        private void PastOpen(int i)
        {
            if (_pastBody == null || i < 0 || i >= _hist.Count) return;
            ChatSession want = _hist[i];
            _pastBody = null;
            Hub.Sheet.Close();
            if (want == _session) return;
            Adopt(want);
            _say.Input.Box.Focus();
        }

        private void PastPin(int i)
        {
            if (i < 0 || i >= _hist.Count) return;
            ChatSession s = _hist[i];
            s.IsPinned = !s.IsPinned;
            Hub.Chats.Save();
            PastFill();
            Hub.Say(s.IsPinned ? "Pinned to the top" : "Unpinned");
        }

        /// Deleting from the history. The conversation on screen can be the one
        /// deleted, in which case the page moves to a new chat rather than sitting
        /// on words that are no longer filed anywhere.
        private void PastKill(int i)
        {
            if (i < 0 || i >= _hist.Count) return;
            ChatSession s = _hist[i];
            int at = Hub.Chats.Items.IndexOf(s);
            if (at < 0) return;
            Hub.Chats.Items.RemoveAt(at);
            Hub.Chats.Save();
            if (s == _session)
            {
                if (_busy) Stop();
                _session = new ChatSession();
                _msgs.Clear();
                _files.Clear();
                _at = -1;
                Fill();
                Titled();
            }
            PastFill();
            Hub.Undoable("Chat deleted", new Action(delegate { PastUnkill(s, at); }));
        }

        private void PastUnkill(ChatSession s, int at)
        {
            Hub.Chats.Items.Insert(Math.Max(0, Math.Min(Hub.Chats.Items.Count, at)), s);
            Hub.Chats.Save();
            if (_msgs.Count == 0) Adopt(s);
            PastFill();
            Hub.Say("Chat put back");
        }
    }
}
