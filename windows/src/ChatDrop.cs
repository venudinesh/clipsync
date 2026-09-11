// Chat: dropping a file on it.
//
// The thing a desktop has that a phone does not: a file already in front of you,
// in a window next to this one. Dragging it onto the conversation is the shortest
// path there is from a document to a question about it, so every surface of the
// page takes a drop, including the words of the transcript and the box itself.
//
// Text dropped rather than a file goes into the message. A sentence dragged out of
// a browser is usually the question; a long passage is the thing being asked
// about, so past a couple of thousand characters it becomes an attachment instead.

using System;
using System.IO;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class ChatView
    {
        /// Where a drop is taken. Each of these is a separate window as far as
        /// Windows is concerned, and a drop lands on whichever one the pointer
        /// happens to be over rather than on the page as a whole.
        private void Dropping()
        {
            Accept(this);
            Accept(_feed);
            Accept(_say.Input.Box);
        }

        private void Accept(Control c)
        {
            if (c == null) return;
            c.AllowDrop = true;
            c.DragEnter += OnDragIn;
            c.DragOver += OnDragIn;
            c.DragDrop += OnDropped;
        }

        private void OnDragIn(object sender, DragEventArgs e)
        {
            e.Effect = Wanted(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private static bool Wanted(IDataObject d)
        {
            if (d == null) return false;
            return d.GetDataPresent(DataFormats.FileDrop) ||
                   d.GetDataPresent(DataFormats.UnicodeText);
        }

        private void OnDropped(object sender, DragEventArgs e)
        {
            if (e.Data == null) return;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] names = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (names == null) return;
                for (int i = 0; i < names.Length; i++)
                {
                    if (Directory.Exists(names[i]))
                    {
                        Hub.Oops("A folder cannot be attached, only the files in it");
                        continue;
                    }
                    Absorb(names[i]);
                }
                return;
            }
            string text = e.Data.GetData(DataFormats.UnicodeText) as string;
            if (string.IsNullOrEmpty(text)) return;
            Landing(text);
        }

        private void Landing(string text)
        {
            string add = text.Replace("\0", "").Trim();
            if (add.Length == 0) return;
            if (add.Length > 2000)
            {
                Attached a = new Attached();
                a.Name = "Dropped text";
                a.Text = Extract.Fit(add, Prompts.AttachmentLimit);
                for (int i = _files.Count - 1; i >= 0; i--)
                {
                    if (_files[i].Name == a.Name) _files.RemoveAt(i);
                }
                Add(a, Say.Plural(Say.Words(a.Text), "word") + " attached");
                return;
            }
            string had = _say.Text;
            _say.Text = had.Trim().Length == 0 ? add : had.TrimEnd() + "\r\n\r\n" + add;
            TextBox box = _say.Input.Box;
            box.Focus();
            box.SelectionStart = box.TextLength;
            box.SelectionLength = 0;
            Lay();
        }
    }
}
