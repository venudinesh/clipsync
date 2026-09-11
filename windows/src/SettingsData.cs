// Settings: the files.
//
// Where everything is kept, how to get a copy out, and how to delete the lot. The
// folder is named on the page rather than described, because "your data is stored
// locally" is a claim and a path is a fact that can be opened and looked at.
//
// The export is plain JSON, not the encrypted store, and the page says so. A backup
// nothing else can read is not a backup.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class SettingsView
    {
        private Pref _whereRow;

        private Group Data()
        {
            _folder.Look = ButtonLook.Soft;
            _folder.ShowIcon = true;
            _folder.Icon = Glyph.External;
            _folder.Label = "Open folder";
            _folder.Click += OnFolder;

            _export.Look = ButtonLook.Soft;
            _export.ShowIcon = true;
            _export.Icon = Glyph.Copy;
            _export.Label = "Save a copy";
            _export.Click += OnExport;

            _wipe.Look = ButtonLook.Danger;
            _wipe.ShowIcon = true;
            _wipe.Icon = Glyph.Trash;
            _wipe.Label = "Delete everything";
            _wipe.Click += OnWipe;

            _pinSet.Look = ButtonLook.Soft;
            _pinSet.Click += OnPinSet;
            _pinDrop.Look = ButtonLook.Outline;
            _pinDrop.Label = "Remove";
            _pinDrop.Click += OnPinDrop;
            _lockNow.Look = ButtonLook.Outline;
            _lockNow.Label = "Lock now";
            _lockNow.Click += OnLockNow;

            _whereRow = Row("Where it is kept", Paths.Root, _folder, Theme.Px(160));

            Group g = new Group("Your files");
            g.Add(_whereRow);
            g.Add(Row("Export everything", "One JSON file with every clip, note and conversation " +
                "in it. Readable by anything, so keep it somewhere you trust", _export, Theme.Px(160)));
            g.Add(Row("Lock with a PIN", "Your clips ask for it before they open, on launch " +
                "and whenever you lock", _pinSet, Theme.Px(160)));
            g.Add(Row("Lock now", "Back behind the PIN immediately", _lockNow, Theme.Px(160)));
            g.Add(Row("Remove the PIN", "Your clips open freely again", _pinDrop, Theme.Px(160)));
            g.Add(Row("Delete everything", "Clears every clip, note and conversation on this PC. " +
                "This cannot be undone", _wipe, Theme.Px(200)));
            return g;
        }

        private void OnPinSet(object sender, EventArgs e)
        {
            string hash = PinLock.Setup();
            if (hash == null) return;
            Hub.Settings.PinHash = hash;
            Hub.SaveSettings();
            Fresh();
            Hub.Say("PIN set. Your clips lock on launch.");
        }

        private void OnPinDrop(object sender, EventArgs e)
        {
            Hub.Settings.PinHash = "";
            Hub.SaveSettings();
            Fresh();
            Hub.Say("PIN removed");
        }

        private void OnLockNow(object sender, EventArgs e)
        {
            if (!PinLock.Locked(Hub.Settings)) return;
            if (!PinLock.Unlock(Hub.Settings)) Application.Exit();
        }

        private void OnFolder(object sender, EventArgs e)
        {
            try
            {
                Paths.EnsureRoot();
                Process.Start("explorer.exe", "\"" + Paths.Root + "\"");
            }
            catch (Exception ex)
            {
                Paths.Log("open folder", ex);
                Hub.Oops("That folder could not be opened");
            }
        }

        private void OnExport(object sender, EventArgs e)
        {
            SaveFileDialog d = new SaveFileDialog();
            try
            {
                d.Title = "Save a copy of everything";
                d.Filter = "JSON|*.json|Every file|*.*";
                d.FileName = "clipsyncai-" + DateTime.Now.ToString("yyyy-MM-dd") + ".json";
                d.OverwritePrompt = true;
                if (d.ShowDialog(FindForm()) != DialogResult.OK) return;
                File.WriteAllText(d.FileName, JsonWriter.Pretty(Bundle()), new UTF8Encoding(false));
                Hub.Say("Saved to " + Path.GetFileName(d.FileName));
            }
            catch (Exception ex)
            {
                Paths.Log("export", ex);
                Hub.Oops("That copy could not be saved. Try a folder you own, such as Documents");
            }
            finally
            {
                d.Dispose();
            }
        }

        /// Everything, in one object. Settings go in too: a person restoring this
        /// on another PC wants their accent and their server address back, and they
        /// are the smallest part of the file.
        private JVal Bundle()
        {
            JVal root = JVal.Object();
            root.Set("app", "ClipSyncAI");
            root.Set("exported", DateTime.UtcNow.ToString("o"));
            root.Set("settings", Hub.Settings.ToJson());
            root.Set("clips", Rolled<ClipEntry>(Hub.Clips.Items, ClipRow));
            root.Set("notes", Rolled<Note>(Hub.Notes.Items, NoteRow));
            root.Set("chats", Rolled<ChatSession>(Hub.Chats.Items, ChatRow));
            return root;
        }

        private static JVal Rolled<T>(List<T> items, Func<T, JVal> one)
        {
            JVal arr = JVal.Array();
            for (int i = 0; i < items.Count; i++) arr.Add(one(items[i]));
            return arr;
        }

        private static JVal ClipRow(ClipEntry c) { return c.ToJson(); }
        private static JVal NoteRow(Note n) { return n.ToJson(); }
        private static JVal ChatRow(ChatSession s) { return s.ToJson(); }

        private void OnWipe(object sender, EventArgs e)
        {
            int total = Hub.Clips.Items.Count + Hub.Notes.Items.Count + Hub.Chats.Items.Count;
            if (total == 0) { Hub.Oops("There is nothing to delete"); return; }
            if (Hub.Sheet == null) return;
            Verbs body = new Verbs();
            body.Add(Glyph.Trash, "Delete all of it", Say.Join(
                Say.Plural(Hub.Clips.Items.Count, "clip"),
                Say.Plural(Hub.Notes.Items.Count, "note"),
                Say.Plural(Hub.Chats.Items.Count, "conversation")), true,
                new EventHandler(Wiped));
            body.Add(Glyph.Close, "Keep it", "Nothing is deleted", false,
                new EventHandler(Kept));
            Hub.Sheet.Open("Delete everything?", "There is no undo for this one",
                body, body.Wants());
        }

        private void Kept(object sender, EventArgs e)
        {
            Hub.Sheet.Close();
        }

        private void Wiped(object sender, EventArgs e)
        {
            Hub.Sheet.Close();
            Hub.Clips.Clear();
            Hub.Notes.Clear();
            Hub.Chats.Clear();
            Hub.RaiseClips();
            Hub.RaiseNotes();
            Hub.Say("Everything on this PC has been deleted");
        }
    }
}
