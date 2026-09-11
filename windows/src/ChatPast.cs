// Chat: the history.
//
// Every conversation on this PC, behind one button. A sheet rather than a column
// down the side of the page: the list is the thing you use for two seconds to get
// back to something, and the width it would cost permanently belongs to the words.
//
// The body is a search box over a ledger, which is the same list the clips and
// notes pages use, so a conversation is found the same way a clip is.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class ChatView
    {
        /// The sheet's body. Two controls that belong together, and a sheet takes
        /// one, so they are wrapped in the smallest container that will hold them.
        private sealed class Past : Control
        {
            public readonly Field Find = new Field();
            public readonly Ledger List = new Ledger();

            public Past()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
                TabStop = false;
                BackColor = Theme.Base;
                Find.ShowIcon = true;
                Find.Icon = Glyph.Search;
                Find.ShowTrail = true;
                Find.Trail = Glyph.Close;
                Find.Placeholder = "Search chats";
                Find.Trailed += delegate { Find.Box.Focus(); };
                List.AccessibleName = "Chat history";
                Controls.Add(Find);
                Controls.Add(List);
            }

            public void Lay()
            {
                int h = Theme.Px(34);
                int w = Math.Max(10, Width);
                Find.SetBounds(0, 0, w, h);
                List.SetBounds(0, h + Space.Sm, w, Math.Max(10, Height - h - Space.Sm));
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                Lay();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(BackColor);
            }
        }

        private void OnPast(object sender, EventArgs e)
        {
            if (Hub.Sheet == null) return;
            Past body = new Past();
            _pastBody = body;
            body.Find.Edited += delegate { PastFill(); };
            body.Find.Submitted += delegate { PastOpen(0); };
            body.List.Count = delegate { return _hist.Count; };
            body.List.HeightOf = PastHigh;
            body.List.PaintRow = PastRow;
            body.List.Activated += PastOpen;
            body.List.Clicked += PastClick;
            AppButton all = new AppButton();
            all.Label = "Delete all";
            all.Look = ButtonLook.Danger;
            all.ShowIcon = true;
            all.Icon = Glyph.Trash;
            all.Click += delegate { PastAll(); };
            int total = Hub.Chats.Items.Count;
            Hub.Sheet.Open("Chat history",
                total == 0 ? "Nothing here yet" : Say.Plural(total, "conversation") + " on this PC",
                body, Theme.Px(360), all);
            PastFill();
            body.Lay();
            body.Find.Box.Focus();
        }

        /// The list, rebuilt from the store. Pinned first, then whichever was
        /// spoken to last, which is the order the page's own reopening follows.
        private void PastFill()
        {
            Past body = _pastBody;
            if (body == null) return;
            string q = body.Find.Text.Trim();
            _hist.Clear();
            List<ChatSession> all = Hub.Chats.Items;
            for (int i = 0; i < all.Count; i++)
            {
                if (PastMatch(all[i], q)) _hist.Add(all[i]);
            }
            _hist.Sort(PastRank);
            bool narrowed = q.Length > 0 && all.Count > 0;
            body.List.EmptyGlyph = narrowed ? Glyph.Search : Glyph.Chat;
            body.List.EmptyTitle = narrowed ? "No match" : "No conversations yet";
            body.List.EmptyBody = narrowed
                ? "Try fewer words. The search reads titles and everything that was said."
                : "Ask something and it is kept here, on this PC, until you delete it.";
            body.List.Reload();
        }

        private static int PastRank(ChatSession a, ChatSession b)
        {
            if (a.IsPinned != b.IsPinned) return a.IsPinned ? -1 : 1;
            return b.UpdatedAt.CompareTo(a.UpdatedAt);
        }

        private static bool PastMatch(ChatSession s, string q)
        {
            if (q.Length == 0) return true;
            if (Held(s.Title, q)) return true;
            for (int i = 0; i < s.Messages.Count; i++)
            {
                ChatMessage m = s.Messages[i];
                if (m.Role == "system") continue;
                if (Held(m.Shown, q) || Held(m.Content, q)) return true;
            }
            return false;
        }

        private static bool Held(string haystack, string needle)
        {
            return haystack != null &&
                   haystack.IndexOf(needle, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        /// Wipes the history. The conversation on screen goes with it, because
        /// leaving it open over an empty history would put it straight back the
        /// next time anything was said.
        private void PastAll()
        {
            if (Hub.Chats.Items.Count == 0) { Hub.Oops("There is nothing to delete"); return; }
            List<ChatSession> gone = new List<ChatSession>(Hub.Chats.Items);
            Hub.Sheet.Close();
            if (_busy) Stop();
            Hub.Chats.Items.Clear();
            Hub.Chats.Save();
            _session = new ChatSession();
            _msgs.Clear();
            _files.Clear();
            _at = -1;
            Fill();
            Titled();
            Hub.Undoable(Say.Plural(gone.Count, "conversation") + " deleted",
                new Action(delegate { PastBack(gone); }));
        }

        private void PastBack(List<ChatSession> gone)
        {
            for (int i = 0; i < gone.Count; i++)
            {
                if (!Hub.Chats.Items.Contains(gone[i])) Hub.Chats.Items.Add(gone[i]);
            }
            Hub.Chats.Save();
            Latest();
            Hub.Say("History put back");
        }
    }
}
