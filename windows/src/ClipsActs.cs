// Clips: what the buttons do.
//
// Everything on this page that changes a clip or opens a sheet, kept apart from
// the layout and the row painting so each file stays one job. The verb list is
// the phone's, in the phone's order, with the same words: the two builds should
// not disagree about what an action is called.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class ClipsView
    {
        private bool _resyncing;

        private void OnLive(object sender, EventArgs e)
        {
            Hub.Settings.CaptureEnabled = _live.On;
            Hub.SaveSettings();
            _monitor.Live = _live.On;
            Hub.Say(_live.On ? "Watching the clipboard" : "Clipboard monitor paused");
        }

        private void OnKeep(object sender, EventArgs e)
        {
            if (_watch.Keep(_compose.Text)) _compose.Text = "";
        }

        private void OnPasteIn(object sender, EventArgs e)
        {
            string text = Hub.TakeClipboard();
            if (text.Trim().Length == 0) { Hub.Oops("Nothing on the clipboard"); return; }
            _compose.Text = text;
            _compose.Input.Box.Focus();
            _compose.Input.Box.SelectionStart = text.Length;
        }

        private void OnDictate(object sender, EventArgs e)
        {
            Hub.Say("Dictation lives on the Capture tab");
        }

        private ClipEntry At(int i)
        {
            return i >= 0 && i < _rows.Count ? _rows[i].Clip : null;
        }

        /// A click in a row. The action zones are hit tested against the same
        /// rectangles the row painted, which is why the ledger hands back where in
        /// the row the click landed.
        private void OnRowClick(int i, Point p)
        {
            ClipEntry c = At(i);
            if (c == null) return;
            // In join mode every zone of the row is a pick, not an action.
            if (_joining) { TogglePick(c); return; }
            Rectangle body = RowKit.PlateOf(_list.RowBounds(i));
            int k = RowKit.Hit(body, p, RowActions.Length);
            if (k == 0) CopyOut(Body(c), "Copied");
            else if (k == 1) Pin(c);
            else if (k == 2) Options(c);
        }

        private void OnOpen(int i)
        {
            ClipEntry c = At(i);
            if (c == null) return;
            if (_joining) { TogglePick(c); return; }
            Read(c);
        }

        /// What a clip copies out: the formatted result where there is one, the
        /// original otherwise. The same rule the phone's reader uses.
        private static string Body(ClipEntry c)
        {
            string result = (c.ProcessedMarkdown ?? "").Trim();
            return result.Length > 0 ? result : (c.RawText ?? "");
        }

        private void Pin(ClipEntry c)
        {
            c.IsPinned = !c.IsPinned;
            Hub.Clips.Save();
            Hub.RaiseClips();
            Hub.Say(c.IsPinned ? "Pinned to the top" : "Unpinned");
        }

        private void Delete(ClipEntry c)
        {
            Hub.Clips.Items.Remove(c);
            Hub.Clips.Save();
            Hub.RaiseClips();
            Hub.Say("Clip deleted");
        }

        private void ToNotes(ClipEntry c)
        {
            Note n = new Note();
            n.Title = Say.FirstLine(Markdown.Peek(Body(c)), 60);
            n.Content = Body(c);
            n.Tags = new List<string>();
            n.Tags.Add("clip");
            Hub.Notes.Items.Add(n);
            Hub.Notes.Save();
            Hub.RaiseNotes();
            Hub.Say("Added to your notes");
        }

        /// The clip's actions, in the phone's order and with the phone's words.
        /// Summarise appears only when a model is actually loaded, because an
        /// action that cannot work is worse than one that is not offered.
        private void Options(ClipEntry c)
        {
            if (Hub.Sheet == null) return;
            Verbs list = new Verbs();
            list.Add(Glyph.Copy, "Copy result", "The tidied version", false,
                delegate { Hub.Sheet.Close(); CopyOut(Body(c), "Copied"); });
            list.Add(Glyph.Copy, "Copy original", "Exactly what was on the clipboard", false,
                delegate { Hub.Sheet.Close(); CopyOut(c.RawText, "Original copied"); });
            if (Hub.Brain.Ready)
            {
                list.Add(Glyph.Sparkle, "Summarise", "Three bullets, written on this PC", false,
                    delegate { Hub.Sheet.Close(); Summarise(c); });
            }
            list.Add(Glyph.Notes, "Add to notes", "", false,
                delegate { Hub.Sheet.Close(); ToNotes(c); });
            list.Add(Glyph.Tasks, "Select for join", "Pick more clips, then join them into one", false,
                delegate { Hub.Sheet.Close(); StartJoin(c); });
            list.Add(Glyph.Pin, c.IsPinned ? "Unpin" : "Pin to top",
                c.IsPinned ? "" : "Keeps it above the rest of the feed", false,
                delegate { Hub.Sheet.Close(); Pin(c); });
            list.Add(Glyph.Trash, "Delete clip", "This cannot be undone", true,
                delegate { Hub.Sheet.Close(); Delete(c); });
            Hub.Sheet.Open("Clip options", Say.StampLong(c.Timestamp), list, list.Wants());
            list.Lay();
        }

        /// The clip, read. A markdown surface in a sheet, with copy beside close,
        /// which is what the phone's reader offers once its paging is taken away.
        /// The text sizes to itself inside the app's own scroller rather than
        /// scrolling on its own, so the sheet moves under the same four pixel
        /// thumb as the feed. A rich text box's scrollbar is drawn by Windows and
        /// cannot be coloured on 7 or 8, and one grey bar in a dark sheet reads as
        /// a fault.
        private void Read(ClipEntry c)
        {
            if (Hub.Sheet == null) return;
            Scroller pane = new Scroller();
            MarkdownBox box = new MarkdownBox();
            box.AutoHeight = true;
            box.Pad = 0;
            box.Surface(Theme.Base);
            pane.Extent = delegate
            {
                box.Width = pane.Room;
                box.Height = Math.Max(10, box.Wants());
                return box.Height;
            };
            pane.Arrange = delegate(int off) { box.Location = new Point(0, -off); };
            pane.Controls.Add(box);
            box.Source = Body(c);
            AppButton more = new AppButton();
            more.Label = "Options";
            more.Look = ButtonLook.Outline;
            more.ShowIcon = true;
            more.Icon = Glyph.More;
            more.Click += delegate { Hub.Sheet.Close(); Options(c); };
            AppButton copy = new AppButton();
            copy.Label = "Copy";
            copy.ShowIcon = true;
            copy.Icon = Glyph.Copy;
            copy.Click += delegate { CopyOut(Body(c), "Copied"); };
            string meta = Say.Join(Say.StampLong(c.Timestamp),
                Say.Plural(Say.Words(c.RawText), "word"),
                c.IsPinned ? "pinned" : "");
            Hub.Sheet.Open("Clip", meta, pane, Theme.Px(320), more, copy);
        }

        private void Summarise(ClipEntry c)
        {
            if (!Hub.Brain.Ready) { Hub.Oops("No model is loaded"); return; }
            Hub.Say("Summarising on this PC");
            string source = Body(c);
            Thread t = new Thread(new ThreadStart(delegate { SummariseWork(c, source); }));
            t.IsBackground = true;
            t.Name = "clip-summarise";
            t.Start();
        }

        private void SummariseWork(ClipEntry c, string source)
        {
            string result;
            try
            {
                result = Hub.Brain.Instruct(Prompts.SummarisePrefix + source, null, null);
            }
            catch (Exception ex)
            {
                Paths.Log("summarise", ex);
                result = "";
            }
            Post(delegate { Summarised(c, result); });
        }

        private void Summarised(ClipEntry c, string result)
        {
            if (result == null || result.Trim().Length == 0)
            {
                Hub.Oops("The model had nothing to say");
                return;
            }
            Note n = new Note();
            n.Title = "Summary of " + Say.FirstLine(Markdown.Peek(Body(c)), 40);
            n.Content = result.Trim();
            n.Tags = new List<string>();
            n.Tags.Add("summary");
            Hub.Notes.Items.Add(n);
            Hub.Notes.Save();
            Hub.RaiseNotes();
            Hub.Say("Summary saved to your notes");
        }

        /// Runs every clip through the formatter again. Worth having on a desktop
        /// in a way it is not on a phone: this is where a model gets installed
        /// after a few hundred clips were already filed without one.
        private void OnResync(object sender, EventArgs e)
        {
            if (_resyncing) { Hub.Oops("Already reprocessing"); return; }
            int n = Hub.Clips.Items.Count;
            if (n == 0) { Hub.Oops("There is nothing to reprocess"); return; }
            _resyncing = true;
            _resync.Busy = true;
            Hub.Say("Reprocessing " + Say.Plural(n, "clip"));
            ClipEntry[] snapshot = Hub.Clips.Items.ToArray();
            Thread t = new Thread(new ThreadStart(delegate { ResyncWork(snapshot); }));
            t.IsBackground = true;
            t.Name = "clip-resync";
            t.Start();
        }

        private void ResyncWork(ClipEntry[] clips)
        {
            int done = 0;
            for (int i = 0; i < clips.Length; i++)
            {
                string raw = clips[i].RawText ?? "";
                if (raw.Trim().Length == 0) continue;
                string result;
                try { result = Hub.Brain.Process(raw); }
                catch (Exception ex) { Paths.Log("resync clip", ex); result = ClipRegex.Process(raw); }
                clips[i].ProcessedMarkdown = result;
                done++;
            }
            int total = done;
            Post(delegate { Resynced(total); });
        }

        private void Resynced(int done)
        {
            _resyncing = false;
            _resync.Busy = false;
            Hub.Clips.Save();
            Hub.RaiseClips();
            Hub.Say(Say.Plural(done, "clip") + " reprocessed");
        }

        /// Hands work back to the interface thread. Every background call on this
        /// page ends here, so none of them has to know how that is done.
        private void Post(Action done)
        {
            Control pump = Hub.Pump != null ? Hub.Pump : this;
            if (!pump.IsHandleCreated) { done(); return; }
            try { pump.BeginInvoke(done); }
            catch (Exception ex) { Paths.Log("clips post", ex); }
        }
    }
}
