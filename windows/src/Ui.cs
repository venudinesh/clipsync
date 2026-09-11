// Painting primitives.
//
// Every surface in the app is drawn rather than themed, because the WinForms
// controls that come in the box cannot be made to look like the phone build and
// because owner drawing is the only way to hold one visual language across
// Windows 7 through 11. These are the pieces every control shares: rounded
// rectangles, soft shadows, and text that lands on whole pixels.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal static class Ui
    {
        /// The quality settings every paint starts from. ClearType stays on for
        /// text because this is a reading surface first.
        public static void Q(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        }

        public static GraphicsPath Round(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            if (r.Width <= 0 || r.Height <= 0) { p.AddRectangle(r); return p; }
            int max = Math.Min(r.Width, r.Height) / 2;
            int k = radius < 0 ? 0 : (radius > max ? max : radius);
            if (k == 0) { p.AddRectangle(r); return p; }
            int d = k * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Fill(Graphics g, Rectangle r, int radius, Color c)
        {
            if (c.A == 0 || r.Width <= 0 || r.Height <= 0) return;
            using (GraphicsPath p = Round(r, radius))
            using (SolidBrush b = new SolidBrush(c))
            {
                g.FillPath(b, p);
            }
        }

        public static void FillBrush(Graphics g, Rectangle r, int radius, Brush b)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            using (GraphicsPath p = Round(r, radius)) g.FillPath(b, p);
        }

        public static void Stroke(Graphics g, Rectangle r, int radius, Color c, float width)
        {
            if (c.A == 0 || r.Width <= 1 || r.Height <= 1) return;
            Rectangle inner = new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1);
            using (GraphicsPath p = Round(inner, radius))
            using (Pen pen = new Pen(c, width))
            {
                pen.Alignment = PenAlignment.Center;
                g.DrawPath(pen, p);
            }
        }

        /// A shadow built from concentric strokes. Cheaper than a blur and, at
        /// the alphas the design uses, indistinguishable from one.
        public static void Shadow(Graphics g, Rectangle r, int radius, int spread, Color tint, int alpha)
        {
            if (Theme.ReduceMotion && spread > 4) spread = 4;
            for (int i = spread; i >= 1; i--)
            {
                int a = (int)(alpha * (1.0 - (double)(i - 1) / spread) / spread * 2.0);
                if (a <= 0) continue;
                if (a > 255) a = 255;
                Rectangle rr = new Rectangle(r.X - i, r.Y - i + 1, r.Width + i * 2, r.Height + i * 2);
                Stroke(g, rr, radius + i, Color.FromArgb(a, tint), 1f);
            }
        }

        public static Brush Sheen(Rectangle r, Color top, Color bottom)
        {
            Rectangle safe = new Rectangle(r.X, r.Y, Math.Max(1, r.Width), Math.Max(1, r.Height));
            return new LinearGradientBrush(safe, top, bottom, LinearGradientMode.Vertical);
        }

        private const TextFormatFlags Base =
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;

        public static void Text(Graphics g, string s, Font f, Color c, Rectangle r, TextFormatFlags flags)
        {
            if (string.IsNullOrEmpty(s)) return;
            TextRenderer.DrawText(g, s, f, r, c, flags | Base);
        }

        public static void Line(Graphics g, string s, Font f, Color c, Rectangle r)
        {
            Text(g, s, f, c, r, TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                                TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }

        public static void Centre(Graphics g, string s, Font f, Color c, Rectangle r)
        {
            Text(g, s, f, c, r, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                                TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }

        public static void Wrap(Graphics g, string s, Font f, Color c, Rectangle r)
        {
            Text(g, s, f, c, r, TextFormatFlags.Left | TextFormatFlags.Top |
                                TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
        }

        public static Size Measure(string s, Font f)
        {
            if (string.IsNullOrEmpty(s)) return new Size(0, f.Height);
            return TextRenderer.MeasureText(s, f, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }

        public static Size MeasureWrapped(string s, Font f, int width)
        {
            if (string.IsNullOrEmpty(s)) return new Size(0, f.Height);
            return TextRenderer.MeasureText(s, f, new Size(Math.Max(1, width), int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.WordBreak);
        }

        /// Blends towards the accent by the given amount, which is how hover and
        /// press states are expressed everywhere.
        public static Color Lift(Color surface, double amount)
        {
            return Palette.Mix(surface, Theme.Accent, amount);
        }

        public static Rectangle Inset(Rectangle r, int by)
        {
            return new Rectangle(r.X + by, r.Y + by, Math.Max(0, r.Width - by * 2), Math.Max(0, r.Height - by * 2));
        }
    }
}
