// Icons, drawn rather than fonted.
//
// Segoe MDL2 Assets only exists from Windows 10, and Segoe UI Symbol's glyphs
// differ across versions, so a font based icon set would look different on every
// machine the app supports. These are drawn on a 24 unit grid and scaled, so a
// 16 pixel icon and a 32 pixel icon are the same drawing at the same weight.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ClipSyncAI
{
    internal enum Glyph
    {
        Clips, Chat, Notes, Capture, Settings,
        Pin, Copy, Trash, Search, Send, Mic, Sparkle,
        Check, Close, Chevron, Plus, Sun, Moon, Refresh, Link, Menu, Stop, Tag, External,
        More, Back, Edit, Sort, Tasks, Spell, Hash
    }

    internal static partial class Icons
    {
        public static void Draw(Graphics g, Glyph glyph, Rectangle r, Color c, float weight)
        {
            if (r.Width <= 2 || r.Height <= 2 || c.A == 0) return;
            float s = Math.Min(r.Width, r.Height) / 24f;
            GraphicsState st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(r.X + (r.Width - 24f * s) / 2f, r.Y + (r.Height - 24f * s) / 2f);
            g.ScaleTransform(s, s);
            using (Pen p = new Pen(c, Math.Max(0.6f, weight / s)))
            using (SolidBrush b = new SolidBrush(c))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                p.LineJoin = LineJoin.Round;
                Paint(g, glyph, p, b);
            }
            g.Restore(st);
        }

        private static void RR(Graphics g, Pen p, float x, float y, float w, float h, float radius)
        {
            using (GraphicsPath path = new GraphicsPath())
            {
                float d = radius * 2f;
                if (d <= 0) { path.AddRectangle(new RectangleF(x, y, w, h)); }
                else
                {
                    path.AddArc(x, y, d, d, 180, 90);
                    path.AddArc(x + w - d, y, d, d, 270, 90);
                    path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
                    path.AddArc(x, y + h - d, d, d, 90, 90);
                    path.CloseFigure();
                }
                g.DrawPath(p, path);
            }
        }

        private static void L(Graphics g, Pen p, float x1, float y1, float x2, float y2)
        {
            g.DrawLine(p, x1, y1, x2, y2);
        }

        private static void Dot(Graphics g, Brush b, float cx, float cy, float rad)
        {
            g.FillEllipse(b, cx - rad, cy - rad, rad * 2f, rad * 2f);
        }

        private static void Paint(Graphics g, Glyph glyph, Pen p, SolidBrush b)
        {
            switch (glyph)
            {
                case Glyph.Clips:
                    RR(g, p, 5f, 4.5f, 14f, 16f, 3f);
                    RR(g, p, 9f, 2.5f, 6f, 4f, 1.6f);
                    L(g, p, 8.5f, 11f, 15.5f, 11f);
                    L(g, p, 8.5f, 15f, 13.5f, 15f);
                    break;

                case Glyph.Chat:
                    RR(g, p, 3f, 4f, 18f, 13f, 4f);
                    L(g, p, 8f, 17f, 8f, 21f);
                    L(g, p, 8f, 21f, 12.5f, 17f);
                    Dot(g, b, 8.5f, 10.5f, 1.1f);
                    Dot(g, b, 12f, 10.5f, 1.1f);
                    Dot(g, b, 15.5f, 10.5f, 1.1f);
                    break;

                case Glyph.Notes:
                    RR(g, p, 4.5f, 3f, 15f, 18f, 3f);
                    L(g, p, 8f, 8f, 16f, 8f);
                    L(g, p, 8f, 12f, 16f, 12f);
                    L(g, p, 8f, 16f, 12.5f, 16f);
                    break;

                case Glyph.Capture:
                    L(g, p, 3.5f, 8f, 3.5f, 5f);
                    L(g, p, 3.5f, 5f, 7f, 5f);
                    L(g, p, 17f, 5f, 20.5f, 5f);
                    L(g, p, 20.5f, 5f, 20.5f, 8f);
                    L(g, p, 20.5f, 16f, 20.5f, 19f);
                    L(g, p, 20.5f, 19f, 17f, 19f);
                    L(g, p, 7f, 19f, 3.5f, 19f);
                    L(g, p, 3.5f, 19f, 3.5f, 16f);
                    L(g, p, 7.5f, 12f, 16.5f, 12f);
                    break;

                case Glyph.Settings:
                    L(g, p, 4f, 7f, 20f, 7f);
                    L(g, p, 4f, 12f, 20f, 12f);
                    L(g, p, 4f, 17f, 20f, 17f);
                    g.FillEllipse(b, 7.2f, 4.7f, 4.6f, 4.6f);
                    g.FillEllipse(b, 13.2f, 9.7f, 4.6f, 4.6f);
                    g.FillEllipse(b, 8.2f, 14.7f, 4.6f, 4.6f);
                    break;

                case Glyph.Pin:
                    L(g, p, 12f, 14f, 12f, 21f);
                    RR(g, p, 7f, 3f, 10f, 11f, 4.5f);
                    Dot(g, b, 12f, 8.5f, 1.6f);
                    break;

                case Glyph.Copy:
                    RR(g, p, 8f, 8f, 12f, 13f, 2.5f);
                    L(g, p, 5.5f, 15.5f, 4.5f, 15.5f);
                    RR(g, p, 4f, 3f, 12f, 13f, 2.5f);
                    break;

                case Glyph.Trash:
                    L(g, p, 4f, 7f, 20f, 7f);
                    L(g, p, 9.5f, 7f, 9.5f, 4.5f);
                    L(g, p, 9.5f, 4.5f, 14.5f, 4.5f);
                    L(g, p, 14.5f, 4.5f, 14.5f, 7f);
                    RR(g, p, 6.5f, 7f, 11f, 14f, 2.5f);
                    L(g, p, 10f, 11f, 10f, 17f);
                    L(g, p, 14f, 11f, 14f, 17f);
                    break;

                case Glyph.Search:
                    g.DrawEllipse(p, 4f, 4f, 12f, 12f);
                    L(g, p, 15.5f, 15.5f, 20f, 20f);
                    break;

                case Glyph.Send:
                    L(g, p, 3.5f, 12f, 20f, 4f);
                    L(g, p, 20f, 4f, 13.5f, 20f);
                    L(g, p, 13.5f, 20f, 11f, 13f);
                    L(g, p, 11f, 13f, 3.5f, 12f);
                    break;

                case Glyph.Mic:
                    RR(g, p, 9f, 2.5f, 6f, 11f, 3f);
                    L(g, p, 5.5f, 11f, 5.5f, 12.5f);
                    g.DrawArc(p, 5.5f, 6.5f, 13f, 12f, 20, 140);
                    L(g, p, 18.5f, 11f, 18.5f, 12.5f);
                    L(g, p, 12f, 18.5f, 12f, 21.5f);
                    break;

                case Glyph.Sparkle:
                    L(g, p, 9f, 3f, 10.6f, 8.4f);
                    L(g, p, 10.6f, 8.4f, 16f, 10f);
                    L(g, p, 16f, 10f, 10.6f, 11.6f);
                    L(g, p, 10.6f, 11.6f, 9f, 17f);
                    L(g, p, 9f, 17f, 7.4f, 11.6f);
                    L(g, p, 7.4f, 11.6f, 2f, 10f);
                    L(g, p, 2f, 10f, 7.4f, 8.4f);
                    L(g, p, 7.4f, 8.4f, 9f, 3f);
                    L(g, p, 17.5f, 14f, 18.4f, 17.1f);
                    L(g, p, 18.4f, 17.1f, 21.5f, 18f);
                    L(g, p, 21.5f, 18f, 18.4f, 18.9f);
                    L(g, p, 18.4f, 18.9f, 17.5f, 22f);
                    L(g, p, 17.5f, 22f, 16.6f, 18.9f);
                    L(g, p, 16.6f, 18.9f, 13.5f, 18f);
                    L(g, p, 13.5f, 18f, 16.6f, 17.1f);
                    L(g, p, 16.6f, 17.1f, 17.5f, 14f);
                    break;

                default:
                    PaintMore(g, glyph, p, b);
                    break;
            }
        }
    }
}
