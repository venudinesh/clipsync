// The one Windows-drawn surface in the app, wearing the app's colours.
//
// Everything else here is painted by hand, but the tray menu is worth handing to
// Windows: it already knows how to position itself against any edge of the
// taskbar, dismiss on a click elsewhere and work from the keyboard. What it does
// not know is the theme, so the renderer supplies that and nothing else.

using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal static class Popup
    {
        public static ContextMenuStrip Make()
        {
            ContextMenuStrip m = new ContextMenuStrip();
            m.Renderer = new PopupSkin();
            m.ShowImageMargin = false;
            m.Opening += OnOpening;
            Skin(m);
            return m;
        }

        public static ToolStripMenuItem Item(string text, EventHandler act)
        {
            ToolStripMenuItem i = new ToolStripMenuItem(text);
            i.Click += act;
            return i;
        }

        /// Re-read on the way open, as the last guard. The fonts are rebuilt
        /// whenever the theme or the scale changes, and a menu holding the old one
        /// would be measuring itself with a disposed font. This is not enough on
        /// its own: some of the ways Windows opens a drop down measure it before
        /// the event is raised, so whoever rebuilds the fonts calls Skin as well.
        private static void OnOpening(object sender, CancelEventArgs e)
        {
            Skin(sender as ContextMenuStrip);
        }

        public static void Skin(ContextMenuStrip m)
        {
            if (m == null) return;
            Theme.Wear(m, Theme.Body);
            m.BackColor = Theme.High;
            m.ForeColor = Theme.OnSurface;
        }
    }

    internal sealed class PopupSkin : ToolStripRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.Graphics.Clear(Theme.High);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            Rectangle r = e.AffectedBounds;
            using (Pen p = new Pen(Theme.Outline))
            {
                e.Graphics.DrawRectangle(p, 0, 0, Math.Max(1, r.Width - 1), Math.Max(1, r.Height - 1));
            }
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            Ui.Q(e.Graphics);
            Ui.Fill(e.Graphics,
                new Rectangle(Theme.Px(3), Theme.Px(1),
                    Math.Max(1, e.Item.Width - Theme.Px(6)), Math.Max(1, e.Item.Height - Theme.Px(2))),
                Radii.Inner, Palette.Alpha(Theme.Accent, 0.18));
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Theme.OnSurface : Theme.Faint;
            e.TextFont = Theme.Body;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using (Pen p = new Pen(Theme.Hairline))
            {
                int y = e.Item.Height / 2;
                e.Graphics.DrawLine(p, Theme.Px(10), y, Math.Max(1, e.Item.Width - Theme.Px(10)), y);
            }
        }
    }
}
