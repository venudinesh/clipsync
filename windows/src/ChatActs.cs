// Chat: the conversation itself.
//
// Reopening, titling, saving, clearing and the two things a single message can
// have done to it. Every one of them writes the store: a tray app is closed from
// the tray, and a conversation that was only in memory would be a lost one.

using System;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class ChatView
    {
        /// The masthead. The phone says whether a chat is saved at all; here it
        /// always is, so that room goes to when it was last touched instead.
        private void Titled()
        {
            _head.Title = Say.FirstLine(_session.Title, 46);
            int n = Live();
            _head.Subtitle = n == 0
                ? (Hub.Brain.Ready ? "Nothing asked yet" : "No model loaded")
                : Say.Join(Say.Plural(n, "message"), Say.StampLong(_session.UpdatedAt));
            AccessibleName = "Chat: " + _head.Title;
            Lay();
        }

        private int Live()
        {
            int n = 0;
            for (int i = 0; i < _msgs.Count; i++)
            {
                if (_msgs[i].Text.Trim().Length > 0) n++;
            }
            return n;
        }

        /// Reopens the conversation last written to. Starting on a blank page
        /// every launch throws away whatever you were in the middle of, and the
        /// history is one button away either way.
        private void Latest()
        {
            ChatSession best = null;
            for (int i = 0; i < Hub.Chats.Items.Count; i++)
            {
                ChatSession s = Hub.Chats.Items[i];
                if (s.Messages.Count == 0) continue;
                if (best == null || s.UpdatedAt > best.UpdatedAt) best = s;
            }
            if (best != null) Adopt(best);
        }

        private void Adopt(ChatSession s)
        {
            if (_busy) Stop();
            _session = s;
            _files.Clear();
            _msgs.Clear();
            _at = -1;
            for (int i = 0; i < s.Messages.Count; i++)
            {
                ChatMessage m = s.Messages[i];
                if (m.Role == "system") continue;
                Bubble b = new Bubble();
                b.Role = m.Role;
                b.Prompt = m.Content ?? "";
                b.Text = string.IsNullOrEmpty(m.Shown) ? b.Prompt : m.Shown;
                b.Note = m.Note ?? "";
                b.At = m.At;
                _msgs.Add(b);
            }
            Fill();
            Titled();
            _feed.End(true);
        }

        /// Writes the conversation out. An empty chat that was never filed stays
        /// unfiled, so the history does not collect blank entries; one that has
        /// been emptied is written through, so the deletion sticks.
        private void Save()
        {
            _session.Messages.Clear();
            for (int i = 0; i < _msgs.Count; i++)
            {
                Bubble b = _msgs[i];
                if (b.Streaming || b.Text.Trim().Length == 0) continue;
                ChatMessage m = new ChatMessage(b.Role, Sent(b));
                if (b.Prompt.Length > 0 && b.Prompt != b.Text) m.Shown = b.Text;
                m.Note = b.Note;
                m.At = b.At;
                _session.Messages.Add(m);
            }
            bool filed = Hub.Chats.Items.Contains(_session);
            if (_session.Messages.Count == 0 && !filed) return;
            if (_session.Title == "New chat") _session.TitleFromFirstMessage();
            _session.Model = Hub.Brain.Model;
            _session.UpdatedAt = DateTime.Now;
            if (!filed) Hub.Chats.Items.Add(_session);
            Hub.Chats.Save();
        }

        private void OnFresh(object sender, EventArgs e)
        {
            if (_msgs.Count == 0) { Hub.Oops("Already a new chat"); return; }
            if (_busy) Stop();
            _session = new ChatSession();
            _msgs.Clear();
            _files.Clear();
            _at = -1;
            Fill();
            Titled();
            _say.Input.Box.Focus();
        }

        /// Clears the messages and keeps the conversation, which is the phone's
        /// Clear chat: the name and its place in the history survive.
        private void Wipe()
        {
            if (_msgs.Count == 0) { Hub.Oops("Nothing to clear"); return; }
            if (_busy) Stop();
            _msgs.Clear();
            _at = -1;
            Fill();
            Save();
            Titled();
            Hub.Say("Chat cleared");
        }

        private void Grab(int k)
        {
            if (k < 0 || k >= _msgs.Count) return;
            CopyOut(_msgs[k].Text, "Copied");
        }

        /// Ask again throws the reply away and puts the question back to the
        /// model. Anything after it goes too: a conversation that carried on past
        /// a reply cannot have that reply swapped out underneath it.
        private void Redo(int k)
        {
            if (_busy) { Hub.Oops("Wait for this reply to finish"); return; }
            if (k <= 0 || k >= _msgs.Count || _msgs[k].Role != "assistant") return;
            if (!Hub.Brain.Ready) { Hub.Oops("Load a model in Settings first"); return; }
            while (_msgs.Count > k) _msgs.RemoveAt(_msgs.Count - 1);
            Bubble theirs = new Bubble();
            theirs.Role = "assistant";
            theirs.Streaming = true;
            _msgs.Add(theirs);
            _at = _msgs.Count - 1;
            Fill();
            _feed.End(true);
            Ask();
        }

        private void Drop(int k)
        {
            if (_busy) { Hub.Oops("Wait for this reply to finish"); return; }
            if (k < 0 || k >= _msgs.Count) return;
            Bubble b = _msgs[k];
            int at = k;
            _msgs.RemoveAt(k);
            Fill();
            Save();
            Titled();
            Hub.Undoable("Message deleted", new Action(delegate { Undrop(b, at); }));
        }

        private void Undrop(Bubble b, int at)
        {
            _msgs.Insert(Math.Max(0, Math.Min(_msgs.Count, at)), b);
            Fill();
            Save();
            Titled();
            Hub.Say("Message put back");
        }
    }
}
