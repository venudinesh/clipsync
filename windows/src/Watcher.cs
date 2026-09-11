// The clipboard watcher.
//
// Windows offers a real notification for clipboard changes, so nothing here
// polls. An update only means the clipboard changed, not that it settled: a drag
// select or a fast series of copies fires several times, so a short debounce
// waits for the last one before anything is stored or any model is called.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed class Watcher
    {
        private readonly Hub _hub;
        private readonly System.Windows.Forms.Timer _settle = new System.Windows.Forms.Timer();
        private IntPtr _hwnd = IntPtr.Zero;
        private bool _listening;
        private bool _working;

        public Watcher(Hub hub)
        {
            _hub = hub;
            _settle.Tick += OnSettled;
        }

        public bool Listening { get { return _listening; } }

        public void Start(IntPtr hwnd)
        {
            _hwnd = hwnd;
            if (_listening || hwnd == IntPtr.Zero) return;
            _listening = Native.ListenToClipboard(hwnd);
            if (!_listening) Paths.Log("clipboard listener refused by the system");
        }

        public void Stop()
        {
            _settle.Stop();
            if (!_listening) return;
            Native.StopListeningToClipboard(_hwnd);
            _listening = false;
        }

        /// Called from the shell's WndProc. Returns true when the message was
        /// ours, so the shell can stop looking at it.
        public bool OnMessage(ref Message m)
        {
            if (m.Msg != Native.WM_CLIPBOARDUPDATE) return false;
            if (_hub.SelfCopy) { _hub.SelfCopy = false; return true; }
            if (!_hub.Settings.CaptureEnabled) return true;
            _settle.Stop();
            _settle.Interval = Math.Max(200, _hub.Settings.DebounceMs);
            _settle.Start();
            return true;
        }

        private void OnSettled(object sender, EventArgs e)
        {
            _settle.Stop();
            Take(false);
        }

        /// Reads what is on the clipboard now and files it. Announce is true when
        /// a person asked for it, so the reason nothing happened is visible.
        public void Take(bool announce)
        {
            Absorb(_hub.TakeClipboard(), announce, true);
        }

        /// Files text a person typed or pasted into the composer. The same
        /// pipeline as a captured clip, so a kept line and a copied one are
        /// formatted alike. Returns false when nothing was filed, which is how
        /// the composer knows whether to clear itself.
        public bool Keep(string text)
        {
            return Absorb(text, true, false);
        }

        private bool Absorb(string text, bool announce, bool fromClipboard)
        {
            if (text == null) text = "";
            string trimmed = text.Trim();
            if (trimmed.Length < Math.Max(1, _hub.Settings.MinLength))
            {
                if (announce)
                {
                    _hub.Oops(fromClipboard ? "Nothing on the clipboard" : "Nothing to keep");
                }
                return false;
            }
            // Masked before anything else, so a secret copied in a hurry never
            // reaches the store. What follows compares and files the masked
            // text, which is the text the app now owns.
            if (_hub.Settings.AutoRedact) trimmed = Secrets.Redact(trimmed).Text;
            // The secret filter guards what arrives without anyone looking at it.
            // Text typed into the composer is already in front of the person who
            // typed it, so refusing to keep it would be the app overruling them.
            if (fromClipboard && _hub.Settings.SkipSensitive && Sensitive.Looks(trimmed))
            {
                if (announce) _hub.Oops("That looks like a secret, so it was not saved");
                return false;
            }
            if (Newest(trimmed))
            {
                if (announce) _hub.Say("Already the latest clip");
                return false;
            }
            ClipEntry similar = ClipSmart.FindSimilar(trimmed, _hub.Clips.Items, 0.85);
            if (similar != null && announce)
            {
                AskDuplicate(trimmed, similar, announce);
                return true;
            }
            if (_working)
            {
                if (announce) _hub.Oops("Still working on the last one");
                return false;
            }
            StartWork(trimmed, announce);
            return true;
        }

        private void StartWork(string trimmed, bool announce)
        {
            _working = true;
            Thread t = new Thread(new ParameterizedThreadStart(Work));
            t.IsBackground = true;
            t.Name = "clip-process";
            t.Start(new object[] { trimmed, announce });
        }

        /// "This looks like one you kept" — keep both, merge into the earlier
        /// one with the join machinery, or drop the newcomer. Asked on the
        /// interface thread, because a sheet is no place for a worker.
        private void AskDuplicate(string trimmed, ClipEntry similar, bool announce)
        {
            Control pump = _hub.Pump;
            Action ask = delegate { DuplicateSheet(trimmed, similar, announce); };
            if (pump == null || !pump.IsHandleCreated) { ask(); return; }
            try { pump.BeginInvoke(ask); }
            catch (Exception) { StartWork(trimmed, announce); }
        }

        private void DuplicateSheet(string trimmed, ClipEntry similar, bool announce)
        {
            if (_hub.Sheet == null) { StartWork(trimmed, announce); return; }
            string preview = similar.Title;
            if (string.IsNullOrEmpty(preview)) preview = similar.Preview(80);
            Verbs list = new Verbs();
            list.Add(Glyph.Copy, "Keep both", "File it as a new clip anyway", false,
                delegate { _hub.Sheet.Close(); StartWork(trimmed, announce); });
            list.Add(Glyph.Link, "Merge", "Fold it into the earlier clip", false,
                delegate { _hub.Sheet.Close(); MergeInto(similar, trimmed, announce); });
            list.Add(Glyph.Trash, "Discard", "Drop the newcomer", true,
                delegate { _hub.Sheet.Close(); _hub.Say("Discarded"); });
            _hub.Sheet.Open("Very similar clip kept",
                "This looks like \"" + preview + "\" from " +
                Say.StampLong(similar.Timestamp), list, list.Wants());
            list.Lay();
        }

        private void MergeInto(ClipEntry similar, string trimmed, bool announce)
        {
            // Titled here, heuristically: the merge itself runs on the
            // interface thread, which is no place to ask a model.
            ClipTitleTags meta = ClipSmart.Heuristic(trimmed);
            ClipEntry fresh = new ClipEntry();
            fresh.RawText = trimmed;
            fresh.Title = meta.Title;
            fresh.Tags = meta.Tags;
            ClipEntry joined = ClipJoin.Join(
                new List<ClipEntry>(new ClipEntry[] { similar, fresh }));
            similar.RawText = joined.RawText;
            similar.ProcessedMarkdown = joined.ProcessedMarkdown;
            similar.Timestamp = DateTime.Now;
            similar.IsChecklist = joined.IsChecklist;
            if (string.IsNullOrEmpty(similar.Title)) similar.Title = fresh.Title;
            if (similar.Tags != null && fresh.Tags != null)
            {
                foreach (string t in fresh.Tags)
                {
                    if (!similar.Tags.Contains(t)) similar.Tags.Add(t);
                }
            }
            _hub.Clips.Save();
            _hub.RaiseClips();
            if (announce) _hub.Say("Merged into the earlier clip");
        }

        private bool Newest(string trimmed)
        {
            ClipEntry newest = null;
            for (int i = 0; i < _hub.Clips.Items.Count; i++)
            {
                ClipEntry c = _hub.Clips.Items[i];
                if (newest == null || c.Timestamp > newest.Timestamp) newest = c;
            }
            return newest != null && newest.RawText != null && newest.RawText.Trim() == trimmed;
        }

        private void Work(object state)
        {
            object[] args = (object[])state;
            string text = (string)args[0];
            bool announce = (bool)args[1];
            string processed;
            try
            {
                processed = _hub.Settings.AutoProcess
                    ? _hub.Brain.Process(text)
                    : ClipRegex.Process(text);
            }
            catch (Exception ex)
            {
                Paths.Log("clip processing", ex);
                processed = ClipRegex.Process(text);
            }
            // Titled on the worker, never on the interface thread: asking a
            // model from the UI thread would freeze the feed it is typing on.
            ClipTitleTags meta = ClipSmart.Describe(text, delegate(string prompt)
            {
                try
                {
                    if (!_hub.Brain.Ready) return null;
                    return _hub.Brain.Instruct(prompt, null, null);
                }
                catch (Exception ex)
                {
                    Paths.Log("clip titling", ex);
                    return null;
                }
            });
            Post(text, processed, meta.Title, meta.Tags, announce);
        }

        private void Post(string raw, string processed, string title,
            List<string> tags, bool announce)
        {
            Control pump = _hub.Pump;
            Action done = delegate { File(raw, processed, title, tags, announce); };
            if (pump == null || !pump.IsHandleCreated) { done(); return; }
            try { pump.BeginInvoke(done); }
            catch (Exception) { _working = false; }
        }

        private void File(string raw, string processed, string title,
            List<string> tags, bool announce)
        {
            _working = false;
            ClipEntry entry = new ClipEntry();
            entry.RawText = raw;
            entry.ProcessedMarkdown = processed;
            entry.IsChecklist = ClipRegex.LooksLikeChecklist(raw);
            entry.Title = title ?? "";
            entry.Tags = tags ?? new List<string>();
            _hub.Clips.Items.Add(entry);
            Trim();
            _hub.Clips.Save();
            _hub.RaiseClips();
            if (announce) _hub.Say("Saved to your clips");
        }

        /// Keeps the history at the configured size. Pinned clips are never the
        /// ones dropped, because pinning is the user saying keep this.
        private void Trim()
        {
            int cap = Math.Max(20, _hub.Settings.MaxHistory);
            while (_hub.Clips.Items.Count > cap)
            {
                int worst = -1;
                for (int i = 0; i < _hub.Clips.Items.Count; i++)
                {
                    ClipEntry c = _hub.Clips.Items[i];
                    if (c.IsPinned) continue;
                    if (worst < 0 || c.Timestamp < _hub.Clips.Items[worst].Timestamp) worst = i;
                }
                if (worst < 0) return;
                _hub.Clips.Items.RemoveAt(worst);
            }
        }
    }
}
