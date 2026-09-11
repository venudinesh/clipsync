// Chat: asking.
//
// The model is asked on a worker thread and never on the interface thread, so a
// slow first token cannot freeze the window. What comes back is handed over
// through the shell's pump.
//
// A question with something attached is sent as one prompt with the attachment
// under it, while the turn on screen shows only the sentence that was typed and
// names the attachment on a line beneath. Printing five thousand characters of a
// document back at the person who just attached it is not a transcript.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class ChatView
    {
        private void OnSend(object sender, EventArgs e)
        {
            if (_busy) { Stop(); return; }
            string typed = _say.Text.Trim();
            if (typed.Length == 0)
            {
                if (_files.Count > 0) Hub.Oops("Type a question to go with it");
                _say.Input.Box.Focus();
                return;
            }
            if (!Hub.Brain.Ready) { Hub.Oops("Load a model in Settings first"); return; }

            Bubble mine = new Bubble();
            mine.Role = "user";
            mine.Text = typed;
            mine.Prompt = Folded(typed);
            mine.Note = _files.Count > 0 ? Named() : "";
            _files.Clear();
            _msgs.Add(mine);

            Bubble theirs = new Bubble();
            theirs.Role = "assistant";
            theirs.Streaming = true;
            _msgs.Add(theirs);
            _at = _msgs.Count - 1;

            _say.Text = "";
            Fill();
            Titled();
            _feed.End(true);
            Ask();
        }

        /// The prompt as the model sees it: the question, then each attachment
        /// under a named rule so it can tell them apart.
        private string Folded(string typed)
        {
            if (_files.Count == 0) return typed;
            StringBuilder sb = new StringBuilder(typed);
            for (int i = 0; i < _files.Count; i++)
            {
                sb.Append("\r\n\r\n--- ").Append(_files[i].Name).Append(" ---\r\n");
                sb.Append(_files[i].Text);
            }
            return sb.ToString();
        }

        /// The line under a question naming what went with it.
        private string Named()
        {
            if (_files.Count == 0) return "";
            if (_files.Count == 1) return "Attached " + _files[0].Name;
            string[] parts = new string[_files.Count];
            for (int i = 0; i < _files.Count; i++) parts[i] = _files[i].Name;
            return "Attached " + string.Join(", ", parts);
        }

        /// Everything said so far, in the order it was said, with the sent form
        /// of each question rather than the shown one. An attachment stays in
        /// context for the rest of the conversation, which is the whole point of
        /// having attached it.
        private void Ask()
        {
            List<ChatMessage> history = new List<ChatMessage>();
            for (int i = 0; i < _msgs.Count; i++)
            {
                Bubble b = _msgs[i];
                if (b.Streaming) continue;
                string body = Sent(b);
                if (body.Trim().Length == 0) continue;
                history.Add(new ChatMessage(b.Role, body));
            }
            HttpCall call = new HttpCall();
            _call = call;
            _busy = true;
            _drained = 0;
            lock (_gate) { _arriving = ""; }
            Working();
            double heat = Heats[_heat];
            int slot = _at;
            Thread t = new Thread(new ThreadStart(delegate { Work(history, heat, call, slot); }));
            t.IsBackground = true;
            t.Name = "chat-model";
            t.Start();
        }

        private static string Sent(Bubble b)
        {
            return b.Prompt.Length > 0 ? b.Prompt : b.Text;
        }

        private void Work(List<ChatMessage> history, double heat, HttpCall call, int slot)
        {
            string text = null;
            string oops = null;
            try
            {
                text = Hub.Brain.Reply(history, heat, call,
                    new Action<string>(delegate(string frag) { Token(frag, call); }));
            }
            catch (Exception ex)
            {
                if (!call.Cancelled)
                {
                    Paths.Log("chat reply", ex);
                    oops = "Something went wrong: " + First(ex.Message);
                }
            }
            string got = text;
            string bad = oops;
            Post(delegate { Landed(slot, got, bad, call); });
        }

        /// The first line of an exception message. The rest is a stack of frames,
        /// which is for the log file and not for a conversation.
        private static string First(string s)
        {
            if (string.IsNullOrEmpty(s)) return "the model did not answer";
            string[] lines = s.Split('\n');
            return lines[0].Trim();
        }

        private void Post(Action done)
        {
            Control pump = Hub.Pump != null ? Hub.Pump : this;
            if (!pump.IsHandleCreated) { done(); return; }
            try { pump.BeginInvoke(done); }
            catch (Exception ex) { Paths.Log("chat post", ex); }
        }
    }
}
