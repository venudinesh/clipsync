// Notes: what the buttons do.
//
// The verbs, in the phone's words. Two of them can lose work, so both put the
// note somewhere it can be got back from: deleting hands the note to the undo
// offer, and summarising in place hands over the text it replaced. The offer
// lives on the hub rather than in the toast, so Ctrl+Z reaches it too.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class NotesView
    {
        private Note At(int i)
        {
            return i >= 0 && i < _rows.Count ? _rows[i].Note : null;
        }

        private void OnRowClick(int i, Point p)
        {
            Note n = At(i);
            if (n == null) return;
            Rectangle plate = RowKit.PlateOf(_list.RowBounds(i));
            int k = RowKit.Hit(plate, p, RowActions.Length);
            if (k == 0) Copy(n);
            else if (k == 1) Pin(n);
            else if (k == 2) Options(n);
        }

        private void OnOpen(int i)
        {
            Note n = At(i);
            if (n != null) Edit(n, false);
        }

        /// A new note opens the editor on a note that is not filed yet. Nothing
        /// is written to the store until there is something in it to write.
        private void OnNew(object sender, EventArgs e)
        {
            Edit(new Note(), true);
        }

        private void Pin(Note n)
        {
            n.IsPinned = !n.IsPinned;
            n.UpdatedAt = DateTime.Now;
            Hub.Notes.Save();
            Hub.RaiseNotes();
            Hub.Say(n.IsPinned ? "Pinned to the top" : "Unpinned");
        }

        /// What a note copies out: the title over the body, or just the body
        /// when it has no title, since a blank line above the text is litter.
        private void Copy(Note n)
        {
            string title = (n.Title ?? "").Trim();
            string body = title.Length == 0 ? (n.Content ?? "") : title + "\r\n\r\n" + (n.Content ?? "");
            CopyOut(body, "Note copied");
        }

        private void Duplicate(Note n)
        {
            string title = (n.Title ?? "").Trim();
            Note copy = new Note();
            copy.Title = title.Length == 0 ? "Untitled (copy)" : title + " (copy)";
            copy.Content = n.Content ?? "";
            copy.Tags = new List<string>();
            if (n.Tags != null) copy.Tags.AddRange(n.Tags);
            Hub.Notes.Items.Add(copy);
            Hub.Notes.Save();
            Hub.RaiseNotes();
            Hub.Say("Note duplicated");
        }

        private void Delete(Note n)
        {
            int at = Hub.Notes.Items.IndexOf(n);
            if (at < 0) return;
            if (_editing == n) Shut();
            Hub.Notes.Items.RemoveAt(at);
            Hub.Notes.Save();
            Hub.RaiseNotes();
            Hub.Undoable("Note deleted", new Action(delegate { Restore(n, at); }));
        }

        private void Restore(Note n, int at)
        {
            int i = Math.Max(0, Math.Min(Hub.Notes.Items.Count, at));
            Hub.Notes.Items.Insert(i, n);
            Hub.Notes.Save();
            Hub.RaiseNotes();
            Hub.Say("Note restored");
        }

        /// Summarising in place rewrites the note, so what it replaced is handed
        /// to the undo offer before the model is asked for anything.
        private void Summarise(Note n)
        {
            if ((n.Content ?? "").Trim().Length == 0) { Hub.Oops("That note is empty"); return; }
            if (!Hub.Brain.Ready) { Hub.Oops("Load a model in Settings first"); return; }
            if (_busy) { Hub.Oops("Already working on a note"); return; }
            Work(n, Prompts.NoteSummaryPrefix, n.Content, "Summary added", true);
        }

        /// One path for every model action on a note: ask on a thread, then put
        /// the answer back on the interface thread with a way to undo it.
        private void Work(Note n, string prompt, string source, string said, bool append)
        {
            _busy = true;
            Working();
            string body = source ?? "";
            Thread t = new Thread(new ThreadStart(delegate { Asked(n, prompt, body, said, append); }));
            t.IsBackground = true;
            t.Name = "note-model";
            t.Start();
        }

        private void Asked(Note n, string prompt, string source, string said, bool append)
        {
            string result;
            try { result = Hub.Brain.Instruct(prompt + source, null, null); }
            catch (Exception ex) { Paths.Log("note model", ex); result = ""; }
            Post(delegate { Answered(n, source, result, said, append); });
        }

        private void Answered(Note n, string was, string result, string said, bool append)
        {
            _busy = false;
            Working();
            if (result == null || result.Trim().Length == 0)
            {
                Hub.Oops(append ? "The model did not return a summary" : "The model did not return anything");
                return;
            }
            n.Content = append ? was + "\r\n\r\n## Summary\r\n\r\n" + result.Trim() : result.Trim();
            n.UpdatedAt = DateTime.Now;
            if (!Hub.Notes.Items.Contains(n))
            {
                Hub.Notes.Items.Add(n);
                if (_editing == n) _fresh = false;
            }
            Hub.Notes.Save();
            if (_editing == n) Wrote(n.Content);
            Hub.RaiseNotes();
            Hub.Undoable(said, new Action(delegate { Put(n, was); }));
        }

        /// Puts the old text back, which is all undo means for a rewrite.
        private void Put(Note n, string was)
        {
            n.Content = was ?? "";
            n.UpdatedAt = DateTime.Now;
            Hub.Notes.Save();
            if (_editing == n) Wrote(n.Content);
            Hub.RaiseNotes();
            Hub.Say("Note put back");
        }

        private void Post(Action done)
        {
            Control pump = Hub.Pump != null ? Hub.Pump : this;
            if (!pump.IsHandleCreated) { done(); return; }
            try { pump.BeginInvoke(done); }
            catch (Exception ex) { Paths.Log("notes post", ex); }
        }
    }
}
