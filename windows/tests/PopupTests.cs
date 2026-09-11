// The tray menu's skin.
//
// The one surface in the app that Windows draws, and the only one whose colours
// arrive through a renderer rather than from the paint code itself. Nothing but
// the tray builds one, so the menu the icon opens is the whole of Popup's use,
// and the way to look at it without a tray is to hand the renderer a bitmap and
// read the pixels back.
//
// The part that is not a colour: a menu built before the fonts were rebuilt
// would open measuring itself with a font that has been disposed, which is why
// the skin is re-read on the way open and not only at construction.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI.Tests
{
    internal static class PopupTests
    {
        public static void Run()
        {
            Built();
            Painted();
            Highlight();
            Rule();
            Lettered();
        }

        /// An item that can be asked to look picked. The framework marks one
        /// selected as the pointer crosses it in a menu that is open, and the
        /// method that does it belongs to the item, so a highlighted row with no
        /// pointer anywhere wants a derived one.
        private sealed class Picked : ToolStripMenuItem
        {
            public Picked(string text) : base(text) { }
            public void Pick() { Select(); }
        }

        private static void Built()
        {
            T.Group("Tray menu");
            bool reached = false;
            using (ContextMenuStrip m = Popup.Make())
            {
                T.Ok("the menu wears the app's own renderer", m.Renderer is PopupSkin);
                T.Ok("with no image margin indenting its labels", !m.ShowImageMargin);
                T.Eq("its surface is the theme's raised colour",
                    T.Hex(Theme.High), T.Hex(m.BackColor));
                T.Eq("and its ink the theme's", T.Hex(Theme.OnSurface), T.Hex(m.ForeColor));
                T.Ok("and it measures itself in the theme's body font",
                    ReferenceEquals(Theme.Body, m.Font));

                EventHandler act = delegate(object s, EventArgs e) { reached = true; };
                ToolStripMenuItem open = Popup.Item("Open ClipSyncAI", act);
                m.Items.Add(open);
                T.Eq("an item carries the words it was given", "Open ClipSyncAI", open.Text);
                open.PerformClick();
                T.Ok("and its click reaches the app", reached);

                // What the re-dressing is for. Rebuilding the fonts throws away
                // the one the menu is holding, and some of the ways Windows opens
                // a drop down measure it before its Opening event, so a menu left
                // to catch up there measures itself with a disposed font and the
                // right click that asked for it throws instead.
                Font stale = Theme.Body;
                Theme.BuildFonts();
                T.Ok("rebuilding the fonts leaves the menu holding the old one",
                    ReferenceEquals(stale, m.Font));
                Popup.Skin(m);
                T.Ok("re-dressing it hands over the font that is now current",
                    ReferenceEquals(Theme.Body, m.Font));
                string trouble = null;
                try { m.Show(new Point(-4000, -4000)); }
                catch (Exception e) { trouble = e.GetType().Name + ": " + e.Message; }
                finally { m.Close(); }
                T.Empty("so opening it measures itself with a font that is alive", trouble);
            }
        }

        /// The renderer, given a bitmap. Its overrides are reachable through the
        /// public Draw calls, which is the same road a real menu paint takes.
        private static void Painted()
        {
            T.Group("Tray menu paint");
            using (ContextMenuStrip m = Popup.Make())
            using (Bitmap b = new Bitmap(160, 30))
            using (Graphics g = Graphics.FromImage(b))
            {
                ToolStripRenderer skin = m.Renderer;
                Rectangle all = new Rectangle(0, 0, 160, 30);
                g.Clear(Color.Magenta);
                skin.DrawToolStripBackground(new ToolStripRenderEventArgs(g, m, all, Color.Empty));
                T.Eq("the surface is filled with the theme's raised colour",
                    T.Hex(Theme.High), T.Hex(b.GetPixel(80, 15)));
                skin.DrawToolStripBorder(new ToolStripRenderEventArgs(g, m, all, Color.Empty));
                T.Eq("and edged with the theme's outline",
                    T.Hex(Theme.Outline), T.Hex(b.GetPixel(0, 0)));
                T.Eq("on the far corner as well",
                    T.Hex(Theme.Outline), T.Hex(b.GetPixel(159, 29)));
                T.Eq("and the edge is an edge, not a second fill",
                    T.Hex(Theme.High), T.Hex(b.GetPixel(80, 15)));
            }
        }

        /// The one row that is not flat: the item under the pointer, tinted with
        /// the accent and inset so the surface still shows around it.
        private static void Highlight()
        {
            using (ContextMenuStrip m = Popup.Make())
            using (Bitmap b = new Bitmap(160, 26))
            using (Graphics g = Graphics.FromImage(b))
            {
                Picked hot = new Picked("Tidy the clipboard");
                m.Items.Add(hot);
                hot.AutoSize = false;
                hot.Size = new Size(160, 26);
                hot.Pick();
                T.Ok("a row can be given a size to paint into",
                    hot.Width == 160 && hot.Height == 26);
                T.Ok("and can be made to look picked", hot.Selected);

                g.Clear(Theme.High);
                m.Renderer.DrawMenuItemBackground(new ToolStripItemRenderEventArgs(g, hot));
                T.Ok("the picked row is tinted", T.Hex(b.GetPixel(80, 13)) != T.Hex(Theme.High));
                T.Ok("with the accent behind it rather than laid on it",
                    T.Hex(b.GetPixel(80, 13)) != T.Hex(Theme.Accent));
                T.Eq("and the tint is inset, so the surface shows around it",
                    T.Hex(Theme.High), T.Hex(b.GetPixel(0, 13)));

                ToolStripMenuItem cold = new ToolStripMenuItem("Quit");
                m.Items.Add(cold);
                cold.AutoSize = false;
                cold.Size = new Size(160, 26);
                g.Clear(Theme.High);
                m.Renderer.DrawMenuItemBackground(new ToolStripItemRenderEventArgs(g, cold));
                T.Eq("a row the pointer is not on is left flat",
                    T.Hex(Theme.High), T.Hex(b.GetPixel(80, 13)));

                hot.Enabled = false;
                g.Clear(Theme.High);
                m.Renderer.DrawMenuItemBackground(new ToolStripItemRenderEventArgs(g, hot));
                T.Eq("and so is one that cannot be chosen",
                    T.Hex(Theme.High), T.Hex(b.GetPixel(80, 13)));
            }
        }

        /// The line between the two halves of the menu. A separator keeps a width
        /// of its own whatever it is handed, so the probe is taken from the size
        /// it ends up with rather than the size it was asked for.
        private static void Rule()
        {
            using (ContextMenuStrip m = Popup.Make())
            using (Bitmap b = new Bitmap(160, 9))
            using (Graphics g = Graphics.FromImage(b))
            {
                ToolStripSeparator bar = new ToolStripSeparator();
                m.Items.Add(bar);
                bar.AutoSize = false;
                bar.Size = new Size(120, 9);
                T.Ok("the separator is wider than the inset on either side",
                    bar.Width > Theme.Px(10) * 2 && bar.Width <= 160);
                int mid = bar.Width / 2;
                g.Clear(Theme.High);
                m.Renderer.DrawSeparator(new ToolStripSeparatorRenderEventArgs(g, bar, false));
                T.Eq("the separator is a hairline",
                    T.Hex(Theme.Hairline), T.Hex(b.GetPixel(mid, bar.Height / 2)));
                T.Eq("inset from the edge", T.Hex(Theme.High), T.Hex(b.GetPixel(2, bar.Height / 2)));
                T.Eq("and drawn once, across the middle",
                    T.Hex(Theme.High), T.Hex(b.GetPixel(mid, 1)));
            }
        }

        /// The colours a label is given, which is the only part of a menu's text
        /// the app decides. Whatever font and colour the framework offers, the
        /// theme's are what get used.
        private static void Lettered()
        {
            T.Group("Tray menu text");
            using (ContextMenuStrip m = Popup.Make())
            using (Bitmap b = new Bitmap(160, 26))
            using (Graphics g = Graphics.FromImage(b))
            {
                g.Clear(Theme.High);
                ToolStripMenuItem on = new ToolStripMenuItem("Open ClipSyncAI");
                m.Items.Add(on);
                ToolStripItemTextRenderEventArgs live = Words(g, on);
                m.Renderer.DrawItemText(live);
                T.Eq("a label that can be chosen takes the theme's text colour",
                    T.Hex(Theme.OnSurface), T.Hex(live.TextColor));
                T.Ok("in the theme's body font, whatever font it was handed",
                    ReferenceEquals(Theme.Body, live.TextFont));

                on.Enabled = false;
                ToolStripItemTextRenderEventArgs dim = Words(g, on);
                m.Renderer.DrawItemText(dim);
                T.Eq("and one that cannot is faint", T.Hex(Theme.Faint), T.Hex(dim.TextColor));
            }
        }

        private static ToolStripItemTextRenderEventArgs Words(Graphics g, ToolStripItem i)
        {
            return new ToolStripItemTextRenderEventArgs(g, i, i.Text,
                new Rectangle(6, 2, 148, 22), Color.Magenta, Theme.Tiny,
                TextFormatFlags.Default);
        }
    }
}

