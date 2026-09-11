// Chat: answering.
//
// Fragments arrive on the worker thread, are appended under a lock, and are shown
// at most a dozen times a second. A local model can emit forty tokens a second
// and each one would otherwise re render a markdown surface and relayout the
// transcript, which costs more than reading the reply does.
//
// A result from a call that is no longer the current one is dropped: the reader
// stopped it, started another, or opened a different conversation, and the answer
// to a question that is no longer on screen has nowhere to land.

using System;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class ChatView
    {
        /// Template markers the tiny local models sometimes write out character
        /// by character. The engine cuts a finished reply at the first one, but a
        /// reply that ended before it was finished can still arrive as the
        /// streaming partial, so the fallback path strips them too.
        private static string WithoutMarkers(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            string[] bad =
            {
                "<|im_end|>", "|im_end|", "<|im_start|>", "|im_start|",
                "<|endoftext|>", "|endoftext|", "<|eot_id|>", "|eot_id|",
                "<|end_of_turn|>",
            };
            string t = s;
            for (int i = 0; i < bad.Length; i++) t = t.Replace(bad[i], "");
            // A half-written marker ("... |<") is the same accident; a trailing
            // > or | that is not the start of a marker keeps its meaning.
            string[] fragMarkers =
            {
                "<|im_end|>", "<|im_start|>", "<|endoftext|>",
                "<|eot_id|>", "<|end_of_turn|>", "</s>",
                "|im_end|", "|im_start|", "|endoftext|", "|eot_id|",
            };
            int trimmed = 0;
            while (trimmed < t.Length && trimmed < 24)
            {
                char c = t[t.Length - 1 - trimmed];
                bool ok = false;
                for (int i = 0; i < fragMarkers.Length; i++)
                {
                    string m = fragMarkers[i];
                    bool prefix = trimmed + 1 <= m.Length;
                    for (int j = 0; prefix && j <= trimmed; j++)
                    {
                        if (m[j] != t[t.Length - 1 - trimmed + j]) { prefix = false; break; }
                    }
                    if (prefix) { ok = true; break; }
                }
                if (!ok) break;
                trimmed++;
            }
            if (trimmed > 0) t = t.Substring(0, t.Length - trimmed);
            return t.TrimEnd();
        }

        private void Token(string frag, HttpCall call)
        {
            if (frag == null || frag.Length == 0) return;
            bool poke;
            lock (_gate)
            {
                _arriving += frag;
                int now = Environment.TickCount;
                poke = _drained == 0 || now - _drained >= 80;
                if (poke) _drained = now;
            }
            if (poke) Post(delegate { Drain(call); });
        }

        private void Drain(HttpCall call)
        {
            if (call != _call) return;
            if (_at < 0 || _at >= _msgs.Count) return;
            string got;
            lock (_gate) { got = _arriving; }
            _msgs[_at].Text = got;
            Grew(_at);
        }

        /// The end of a reply, however it ended. What arrived is kept even when
        /// the call failed halfway: half an answer is usually still worth reading,
        /// and throwing it away to print an error would lose it.
        private void Landed(int slot, string text, string oops, HttpCall call)
        {
            if (call != _call) return;
            _call = null;
            _busy = false;
            Working();
            if (slot < 0 || slot >= _msgs.Count) return;
            string partial;
            lock (_gate) { partial = _arriving; }
            Bubble b = _msgs[slot];
            b.Streaming = false;
            if (text != null && text.Trim().Length > 0)
            {
                b.Text = text.Trim();
            }
            else if (partial.Trim().Length > 0)
            {
                // Half an answer and a failure: the answer goes in the
                // transcript and the failure goes in a toast, because an error
                // printed over the words would throw them away.
                b.Text = WithoutMarkers(partial.Trim());
                if (oops != null) Hub.Oops(oops);
            }
            else if (oops != null) b.Text = oops;
            else b.Text = "Could not generate a reply: the model sent nothing back";
            bool tail = _feed.AtEnd;
            Fill();
            Titled();
            if (tail) _feed.End(true);
            Save();
        }

        /// Stop keeps what arrived. The phone build keeps it too, and a reply cut
        /// off after two paragraphs is usually the two paragraphs you wanted.
        private void Stop()
        {
            HttpCall call = _call;
            if (call == null) return;
            _call = null;
            _busy = false;
            call.Cancel();
            Working();
            string partial;
            lock (_gate) { partial = _arriving; }
            if (_at >= 0 && _at < _msgs.Count)
            {
                Bubble b = _msgs[_at];
                b.Streaming = false;
                b.Text = partial.Trim().Length == 0 ? "(stopped)" : WithoutMarkers(partial.Trim());
            }
            bool tail = _feed.AtEnd;
            Fill();
            Titled();
            if (tail) _feed.End(true);
            Save();
        }

        /// The send control doubles as the stop control while a reply is
        /// arriving. A second button that is dead most of the time is worse than
        /// one that says what it does now.
        private void Working()
        {
            _say.Go.Label = _busy ? "Stop" : "Send";
            _say.Go.Icon = _busy ? Glyph.Stop : Glyph.Send;
            _say.Go.Look = _busy ? ButtonLook.Danger : ButtonLook.Filled;
            _say.Go.AccessibleName = _busy ? "Stop generating" : "Send message";
            _say.Go.AccessibleDescription = _busy ? "Keeps what has arrived" : "Enter sends; Shift+Enter for a line";
            _say.Lay();
            for (int i = 0; i < _openers.Count; i++) _openers[i].Enabled = !_busy;
            Invalidate();
        }
    }
}
