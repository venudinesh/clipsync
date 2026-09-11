// Picking a region of the screen.
//
// The desktop's answer to the phone's camera. Everything on screen is copied once,
// up front, then the copy is shown full screen and dimmed so a region can be drawn
// on a picture that is not moving. Selecting on the live desktop would mean the
// window being read could repaint, scroll or close halfway through.
//
// It covers every monitor, including ones to the left of the main one, which is why
// the bounds come from the virtual screen rather than the primary screen.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed class Grab : Form
    {
        private readonly Bitmap _shot;
        private readonly Rectangle _all;
        private Point _from;
        private Point _to;
        private bool _drawing;

        /// The region that was chosen, in screen coordinates, or empty when the
        /// pick was abandoned.
        public Rectangle Picked = Rectangle.Empty;

        private Grab(Bitmap shot, Rectangle all)
        {
            _shot = shot;
            _all = all;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = all;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            Cursor = Cursors.Cross;
            KeyPreview = true;
            BackColor = Color.Black;
            Text = "Choose a region";
        }

        /// Takes a picture of a region the user draws. Returns null when nothing
        /// was chosen, which includes a click with no drag: a one pixel region is
        /// a slip, not an instruction.
        public static Bitmap Pick()
        {
            Rectangle all = SystemInformation.VirtualScreen;
            if (all.Width < 2 || all.Height < 2) return null;
            Bitmap shot = null;
            try
            {
                shot = new Bitmap(all.Width, all.Height);
                using (Graphics g = Graphics.FromImage(shot))
                {
                    g.CopyFromScreen(all.X, all.Y, 0, 0, all.Size, CopyPixelOperation.SourceCopy);
                }
                using (Grab pick = new Grab(shot, all))
                {
                    pick.ShowDialog();
                    Rectangle r = pick.Picked;
                    if (r.Width < 4 || r.Height < 4) return null;
                    Rectangle inside = new Rectangle(r.X - all.X, r.Y - all.Y, r.Width, r.Height);
                    inside.Intersect(new Rectangle(0, 0, shot.Width, shot.Height));
                    if (inside.Width < 4 || inside.Height < 4) return null;
                    return shot.Clone(inside, shot.PixelFormat);
                }
            }
            catch (Exception ex)
            {
                Paths.Log("screen region", ex);
                return null;
            }
            finally
            {
                if (shot != null) shot.Dispose();
            }
        }

        private Rectangle Band()
        {
            return Rectangle.FromLTRB(Math.Min(_from.X, _to.X), Math.Min(_from.Y, _to.Y),
                Math.Max(_from.X, _to.X), Math.Max(_from.Y, _to.Y));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) { Close(); return; }
            _from = e.Location;
            _to = e.Location;
            _drawing = true;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!_drawing) return;
            _to = e.Location;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (!_drawing) return;
            _drawing = false;
            _to = e.Location;
            Rectangle b = Band();
            Picked = new Rectangle(b.X + _all.X, b.Y + _all.Y, b.Width, b.Height);
            Close();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { Picked = Rectangle.Empty; Close(); }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.DrawImageUnscaled(_shot, 0, 0);
            Rectangle b = _drawing ? Band() : Rectangle.Empty;
            using (SolidBrush dim = new SolidBrush(Color.FromArgb(140, 8, 10, 14)))
            {
                if (b.Width < 2 || b.Height < 2)
                {
                    g.FillRectangle(dim, ClientRectangle);
                }
                else
                {
                    // The four bands around the region, so the region itself is
                    // shown at full brightness without compositing anything.
                    g.FillRectangle(dim, 0, 0, Width, b.Y);
                    g.FillRectangle(dim, 0, b.Bottom, Width, Math.Max(0, Height - b.Bottom));
                    g.FillRectangle(dim, 0, b.Y, b.X, b.Height);
                    g.FillRectangle(dim, b.Right, b.Y, Math.Max(0, Width - b.Right), b.Height);
                }
            }
            if (b.Width >= 2 && b.Height >= 2)
            {
                using (Pen p = new Pen(Color.FromArgb(235, 245, 250, 255), 1.5f))
                {
                    g.DrawRectangle(p, b.X, b.Y, b.Width - 1, b.Height - 1);
                }
                Told(g, b);
            }
            else Hint(g);
        }

        /// The size, next to the region. Below it when the region starts at the
        /// top of the screen, so the number is never off the edge.
        private void Told(Graphics g, Rectangle b)
        {
            string s = b.Width + " by " + b.Height;
            Size want = TextRenderer.MeasureText(s, Font);
            int y = b.Y - want.Height - 6 < 0 ? b.Bottom + 6 : b.Y - want.Height - 6;
            Rectangle box = new Rectangle(b.X, y, want.Width + 12, want.Height + 6);
            using (SolidBrush back = new SolidBrush(Color.FromArgb(220, 12, 14, 18)))
            using (GraphicsPath path = Round(box, 6))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillPath(back, path);
                g.SmoothingMode = SmoothingMode.None;
            }
            TextRenderer.DrawText(g, s, Font, new Point(box.X + 6, box.Y + 3),
                Color.FromArgb(240, 245, 250, 255));
        }

        private void Hint(Graphics g)
        {
            string s = "Drag to choose a region. Esc or right click to leave it.";
            Size want = TextRenderer.MeasureText(s, Font);
            Rectangle box = new Rectangle((Width - want.Width) / 2 - 14,
                Height / 2 - want.Height, want.Width + 28, want.Height + 14);
            using (SolidBrush back = new SolidBrush(Color.FromArgb(225, 12, 14, 18)))
            using (GraphicsPath path = Round(box, 10))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillPath(back, path);
                g.SmoothingMode = SmoothingMode.None;
            }
            TextRenderer.DrawText(g, s, Font,
                new Point(box.X + 14, box.Y + 7), Color.FromArgb(240, 245, 250, 255));
        }

        private static GraphicsPath Round(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            int d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
