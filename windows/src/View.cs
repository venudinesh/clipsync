// The base every destination sits on.
//
// A view is handed the hub and owns nothing else: no static state, no reaching
// into a sibling. The shell creates all five at startup and shows one at a time,
// so switching a destination costs a Visible flip rather than a rebuild, and a
// half typed message or a scroll position survives a trip to Settings and back.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal abstract class View : Control
    {
        protected readonly Hub Hub;

        protected View(Hub hub)
        {
            Hub = hub;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            TabStop = false;
            BackColor = Theme.Canvas;
        }

        /// The rail label this view answers to, which is also its accessible name.
        public abstract string Label { get; }

        /// Called when the view becomes the visible one. Views refresh here
        /// rather than on every store change they might have missed.
        public virtual void Shown() { }

        public virtual void Hidden() { }

        /// Re-reads the theme. Called at startup and whenever settings change.
        public virtual void ApplyTheme()
        {
            BackColor = Theme.Canvas;
            Invalidate();
        }

        /// Lays out children. Called on resize and after anything that changes
        /// how much room a section needs.
        public virtual void Lay() { }

        /// A shortcut the shell offers the visible view first. Return true when
        /// it was used, so the shell stops looking.
        public virtual bool Shortcut(KeyEventArgs e) { return false; }

        /// The page margin. One number, so every view's content starts on the
        /// same vertical line.
        protected int Edge { get { return Space.Xl; } }

        protected int Content { get { return Math.Max(10, Width - Edge * 2); } }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Lay();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
        }

        /// Copies text out and says so, which is the same two lines in six
        /// places otherwise.
        protected void CopyOut(string text, string said)
        {
            if (text == null || text.Trim().Length == 0)
            {
                Hub.Oops("Nothing to copy");
                return;
            }
            if (Hub.PutClipboard(text)) Hub.Say(said);
            else Hub.Oops("Windows would not let go of the clipboard");
        }
    }
}
