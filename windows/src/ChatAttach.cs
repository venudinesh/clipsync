// Chat: attaching something to a question.
//
// A document is read here and now, on this PC, and folded into the next message.
// Nothing is uploaded and nothing is kept: the words go into the prompt, the
// prompt goes to a model on this machine, and the file is closed again.
//
// The limit is a few thousand characters. Past that a model spends its context on
// the tail of a document instead of the question asked about it, so the text is
// cut at a line break and the cut is said out loud in the prompt.

using System;
using System.IO;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class ChatView
    {
        private void OnAttach(object sender, EventArgs e)
        {
            if (Hub.Sheet == null) return;
            Verbs list = new Verbs();
            list.Add(Glyph.Notes, "Document", "Text, markdown, code or a Word file", false,
                delegate { Hub.Sheet.Close(); Pick(); });
            list.Add(Glyph.Copy, "Clipboard", "Whatever is on the clipboard right now", false,
                delegate { Hub.Sheet.Close(); Pasted(); });
            if (_files.Count > 0)
            {
                list.Add(Glyph.Close, "Remove attachments", Named(), true,
                    delegate { Hub.Sheet.Close(); Unattach(); });
            }
            Hub.Sheet.Open("Attach something",
                "It is read on this PC and added to your next message", list, list.Wants());
            list.Lay();
        }

        private void Pick()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Attach a document";
                dlg.Filter = Extract.Filter;
                dlg.Multiselect = true;
                dlg.CheckFileExists = true;
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                string[] names = dlg.FileNames;
                for (int i = 0; i < names.Length; i++) Absorb(names[i]);
            }
        }

        /// Reads one file in. A failure names the file and says why in a toast
        /// rather than a dialog: several files can be chosen at once, and three
        /// dialogs to dismiss over three unreadable files is a punishment.
        private void Absorb(string path)
        {
            string text;
            try
            {
                text = Extract.Read(path);
            }
            catch (Exception ex)
            {
                Paths.Log("attach " + path, ex);
                Hub.Oops(First(ex.Message));
                return;
            }
            string body = Extract.Fit(text, Prompts.AttachmentLimit);
            if (body.Trim().Length == 0) { Hub.Oops("There is no text in that file"); return; }
            Attached a = new Attached();
            a.Name = Path.GetFileName(path);
            a.Text = body;
            Add(a, Say.Join(a.Name, Say.Plural(Say.Words(body), "word") + " attached"));
        }

        private void Pasted()
        {
            string text = Hub.TakeClipboard();
            if (text.Trim().Length == 0) { Hub.Oops("There is no text on the clipboard"); return; }
            for (int i = _files.Count - 1; i >= 0; i--)
            {
                if (_files[i].Name == "Clipboard") _files.RemoveAt(i);
            }
            Attached a = new Attached();
            a.Name = "Clipboard";
            a.Text = Extract.Fit(text, Prompts.AttachmentLimit);
            Add(a, Say.Plural(Say.Words(a.Text), "word") + " from the clipboard");
        }

        /// Four is the ceiling. Not a technical one: five documents and a
        /// sentence is a research project, and the model will answer it as one.
        private void Add(Attached a, string said)
        {
            if (_files.Count >= 4)
            {
                Hub.Oops("Four attachments is as much as one question can carry");
                return;
            }
            for (int i = 0; i < _files.Count; i++)
            {
                if (_files[i].Name == a.Name)
                {
                    Hub.Oops(a.Name + " is already attached");
                    return;
                }
            }
            _files.Add(a);
            Lay();
            Invalidate();
            Hub.Say(said);
            _say.Input.Box.Focus();
        }

        private void Unattach()
        {
            if (_files.Count == 0) { Hub.Oops("Nothing is attached"); return; }
            _files.Clear();
            Lay();
            Invalidate();
            Hub.Say("Attachments removed");
        }
    }
}
