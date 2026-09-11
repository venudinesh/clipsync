// Chat: the sheets.
//
// A message's actions, the conversation's actions, and renaming. All three are
// verb stacks, which is the phone's answer as well: an action worth offering is
// worth naming, and a row of glyphs above every reply would be six unlabelled
// buttons on a surface meant for reading.

using System;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class ChatView
    {
        /// One message's actions. Summarise appears only when there is enough
        /// text to be worth summarising and a model to do it with, because an
        /// action that cannot work is worse than one that is not offered.
        private void Options(int k)
        {
            if (Hub.Sheet == null || k < 0 || k >= _msgs.Count) return;
            Bubble b = _msgs[k];
            if (b.Streaming) { Hub.Oops("Wait for this reply to finish"); return; }
            int at = k;
            bool reply = b.Role != "user";
            string body = b.Text.Trim();
            Verbs list = new Verbs();
            list.Add(Glyph.Copy, "Copy message", "", false,
                delegate { Hub.Sheet.Close(); Grab(at); });
            if (body.Length >= 40 && Hub.Brain.Ready)
            {
                list.Add(Glyph.Sparkle, "Summarise this", "Three bullets, numbers and names kept exact",
                    false, delegate { Hub.Sheet.Close(); Sum(at); });
            }
            list.Add(Glyph.Notes, "Save to Notes", "Filed under the chat tag", false,
                delegate { Hub.Sheet.Close(); Keep(at); });
            if (reply && Hub.Brain.Ready)
            {
                list.Add(Glyph.Refresh, "Ask again", "Throws this reply away and has another go",
                    false, delegate { Hub.Sheet.Close(); Redo(at); });
            }
            list.Add(Glyph.Trash, "Delete message", "You can undo this", true,
                delegate { Hub.Sheet.Close(); Drop(at); });
            Hub.Sheet.Open(reply ? "This reply" : "Your message",
                Say.Join(Say.Plural(Say.Words(body), "word"), Say.StampLong(b.At)),
                list, list.Wants());
            list.Lay();
        }

        /// The conversation's actions. Most of them need something to have been
        /// said first, so on an empty chat the sheet is a rename and a delete.
        private void OnMenu(object sender, EventArgs e)
        {
            if (Hub.Sheet == null) return;
            int n = Live();
            Verbs list = new Verbs();
            list.Add(Glyph.Edit, "Rename chat", "Names it in the history", false,
                delegate { Hub.Sheet.Close(); Rename(); });
            if (n > 0 && Hub.Brain.Ready)
            {
                list.Add(Glyph.Sparkle, "Summarise conversation",
                    "Adds the summary to this chat, written on this PC", false,
                    delegate { Hub.Sheet.Close(); SumAll(); });
            }
            if (n > 0)
            {
                list.Add(Glyph.Copy, "Copy transcript", "Every message, with who said it", false,
                    delegate { Hub.Sheet.Close(); CopyOut(Transcript(), "Transcript copied"); });
                list.Add(Glyph.Notes, "Save transcript to Notes", "", false,
                    delegate { Hub.Sheet.Close(); KeepAll(); });
                list.Add(Glyph.Refresh, "Clear messages", "Keeps the chat and its name", false,
                    delegate { Hub.Sheet.Close(); Wipe(); });
            }
            list.Add(Glyph.Trash, "Delete chat", "You can undo this", true,
                delegate { Hub.Sheet.Close(); Erase(); });
            Hub.Sheet.Open("This chat",
                n == 0 ? "Nothing asked yet"
                       : Say.Join(Say.Plural(n, "message"), Say.StampLong(_session.UpdatedAt)),
                list, list.Wants());
            list.Lay();
        }

        /// Renaming. The box starts on the current name unless that name is the
        /// placeholder, in which case it starts empty rather than making somebody
        /// clear the words "New chat" out of it first.
        private void Rename()
        {
            if (Hub.Sheet == null) return;
            Field box = new Field();
            box.Placeholder = "Chat title";
            box.Text = _session.Title == "New chat" ? "" : _session.Title;
            box.Submitted += delegate { Renamed(box.Text); };
            AppButton go = new AppButton();
            go.Label = "Rename";
            go.ShowIcon = true;
            go.Icon = Glyph.Check;
            go.Click += delegate { Renamed(box.Text); };
            Hub.Sheet.Open("Rename chat", "This is what the history lists it as",
                box, Theme.Px(44), go);
            box.Box.Focus();
            box.Box.SelectAll();
        }

        private void Renamed(string title)
        {
            string want = (title ?? "").Trim();
            Hub.Sheet.Close();
            if (want.Length == 0) { Hub.Oops("Give the chat a name"); return; }
            _session.Title = Say.FirstLine(want, 60);
            if (Hub.Chats.Items.Contains(_session))
            {
                _session.UpdatedAt = DateTime.Now;
                Hub.Chats.Save();
            }
            Titled();
            Hub.Say("Chat renamed");
        }

        /// Deletes the conversation and opens a fresh one. Undoable rather than
        /// confirmed: a second dialog in front of every delete costs more, over a
        /// week of use, than putting one back the once it was a mistake.
        private void Erase()
        {
            if (_busy) Stop();
            ChatSession gone = _session;
            int at = Hub.Chats.Items.IndexOf(gone);
            if (at >= 0)
            {
                Hub.Chats.Items.RemoveAt(at);
                Hub.Chats.Save();
            }
            _session = new ChatSession();
            _msgs.Clear();
            _files.Clear();
            _at = -1;
            Fill();
            Titled();
            if (at < 0) { Hub.Say("Chat deleted"); return; }
            Hub.Undoable("Chat deleted", new Action(delegate { Unerase(gone, at); }));
        }

        private void Unerase(ChatSession s, int at)
        {
            Hub.Chats.Items.Insert(Math.Max(0, Math.Min(Hub.Chats.Items.Count, at)), s);
            Hub.Chats.Save();
            Adopt(s);
            Hub.Say("Chat put back");
        }
    }
}
