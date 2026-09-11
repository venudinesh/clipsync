// Chat: the things done to a conversation other than talking to it.
//
// Summarising a message or the whole conversation, filing either into Notes, and
// the plain text form used for both the clipboard and the note. A summary is
// written into the transcript rather than shown in a box and thrown away: it is
// part of what was said, and the model should see it in the turns that follow.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace ClipSyncAI
{
    internal sealed partial class ChatView
    {
        /// A one shot instruction that answers into the transcript. It borrows the
        /// streaming path whole, so the reply arrives token by token, Stop works
        /// on it, and it is saved when it lands.
        private void Chore(string prompt, string note)
        {
            if (_busy) { Hub.Oops("Wait for this reply to finish"); return; }
            if (!Hub.Brain.Ready) { Hub.Oops("Load a model in Settings first"); return; }
            Bubble b = new Bubble();
            b.Role = "assistant";
            b.Note = note;
            b.Streaming = true;
            _msgs.Add(b);
            _at = _msgs.Count - 1;
            HttpCall call = new HttpCall();
            _call = call;
            _busy = true;
            _drained = 0;
            lock (_gate) { _arriving = ""; }
            Working();
            Fill();
            Titled();
            _feed.End(true);
            int slot = _at;
            Thread t = new Thread(new ThreadStart(delegate { Chored(prompt, call, slot); }));
            t.IsBackground = true;
            t.Name = "chat-chore";
            t.Start();
        }

        private void Chored(string prompt, HttpCall call, int slot)
        {
            string text = null;
            string oops = null;
            try
            {
                text = Hub.Brain.Instruct(prompt, call,
                    new Action<string>(delegate(string frag) { Token(frag, call); }));
            }
            catch (Exception ex)
            {
                if (!call.Cancelled)
                {
                    Paths.Log("chat summarise", ex);
                    oops = "Something went wrong: " + First(ex.Message);
                }
            }
            string got = text;
            string bad = oops;
            Post(delegate { Landed(slot, got, bad, call); });
        }

        private void Sum(int k)
        {
            if (k < 0 || k >= _msgs.Count) return;
            string body = _msgs[k].Text.Trim();
            if (body.Length < 40) { Hub.Oops("Too short to be worth summarising"); return; }
            Chore(Prompts.ChatMessagePrefix + body, "Summary of a message above");
        }

        private void SumAll()
        {
            string body = Transcript();
            if (body.Length < 40) { Hub.Oops("Not enough here to summarise yet"); return; }
            Chore(Prompts.ChatSummaryPrefix + body, "Summary of this conversation");
        }

        /// One message, filed. Tagged so the notes page can gather everything that
        /// came out of a conversation under one tag.
        private void Keep(int k)
        {
            if (k < 0 || k >= _msgs.Count) return;
            string body = _msgs[k].Text.Trim();
            if (body.Length == 0) { Hub.Oops("Nothing to save"); return; }
            Filed(Say.FirstLine(Markdown.Peek(body), 40), body, "Saved to Notes");
        }

        private void KeepAll()
        {
            string body = Transcript();
            if (body.Length == 0) { Hub.Oops("Nothing to save"); return; }
            string title = _session.Title == "New chat"
                ? Say.FirstLine(body, 60) : Say.FirstLine(_session.Title, 60);
            Filed(title, body, "Saved to Notes");
        }

        private void Filed(string title, string body, string toast)
        {
            Note n = new Note();
            n.Title = title.Trim().Length == 0 ? "Chat message" : title;
            n.Content = body;
            n.Tags = new List<string>();
            n.Tags.Add("chat");
            Hub.Notes.Items.Add(n);
            Hub.Notes.Save();
            Hub.RaiseNotes();
            Hub.Say(toast);
        }

        /// The conversation as plain text, with the phone's headers. Whoever
        /// pastes this into a mail or a ticket needs to be able to tell which
        /// half of it they wrote.
        private string Transcript()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < _msgs.Count; i++)
            {
                Bubble b = _msgs[i];
                string body = b.Text.Trim();
                if (body.Length == 0) continue;
                if (sb.Length > 0) sb.Append("\r\n\r\n");
                sb.Append(b.Role == "user" ? "You:" : "ClipSyncAI:");
                sb.Append("\r\n").Append(body);
            }
            return sb.ToString();
        }
    }
}
