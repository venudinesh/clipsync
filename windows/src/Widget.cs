// The base every drawn control sits on.
//
// Handles the parts that would otherwise be repeated a dozen times: double
// buffered painting, hover and press values that animate themselves, keyboard
// activation and a visible focus ring, and the accessible name that screen
// readers announce. Subclasses implement Render and nothing else.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal abstract class Widget : Control, IAnimated
    {
        protected readonly Anim Hover = new Anim(Motion.Fast);
        protected readonly Anim Press = new Anim(Motion.Instant);
        protected readonly Anim Ring = new Anim(Motion.Fast);

        private bool _down;

        protected Widget()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            Ticker.Join(this);
        }

        /// True while the pointer is over the control, kept as a field because
        /// Control.MouseHover fires once and MouseEnter can be missed when a
        /// window appears under a stationary pointer.
        protected bool Pointing { get; private set; }

        protected bool Held { get { return _down; } }

        /// The colour behind this control, so a rounded shape can leave its
        /// corners the right colour without real transparency.
        protected Color Behind
        {
            get { return Parent != null ? Parent.BackColor : Theme.Canvas; }
        }

        public virtual bool Tick()
        {
            bool busy = Hover.Advance() | Press.Advance() | Ring.Advance();
            if (busy) Invalidate();
            return busy;
        }

        protected abstract void Render(Graphics g, Rectangle r);

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Behind);
            Ui.Q(e.Graphics);
            Render(e.Graphics, ClientRectangle);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            Pointing = true;
            Hover.To(1);
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            Pointing = false;
            _down = false;
            Hover.To(0);
            Press.To(0);
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _down = true;
                Press.To(1);
                if (TabStop) Select();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _down = false;
            Press.To(0);
            base.OnMouseUp(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            Ring.To(1);
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            Ring.To(0);
            base.OnLostFocus(e);
        }

        /// Space and Enter activate, which is what keyboard users expect of
        /// anything that looks like a button.
        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Space || keyData == Keys.Enter) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                Press.To(1);
                Activate();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            Press.To(0);
            base.OnKeyUp(e);
        }

        /// What a keyboard activation does. Buttons raise Click; controls that
        /// mean something else override this.
        protected virtual void Activate()
        {
            OnClick(EventArgs.Empty);
        }

        protected void RingIfFocused(Graphics g, Rectangle r, int radius)
        {
            if (Ring.Value <= 0.01) return;
            int a = (int)(190 * Ring.Value);
            Ui.Stroke(g, Ui.Inset(r, 1), radius, Color.FromArgb(a, Theme.AccentText), 2f);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Ticker.Leave(this);
            base.Dispose(disposing);
        }
    }
}
