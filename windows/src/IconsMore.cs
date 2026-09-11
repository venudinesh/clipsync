// The rest of the icon set.
//
// Split from Icons.cs only to keep each file readable; PaintMore is reached from
// the default arm of the first switch.

using System;
using System.Drawing;

namespace ClipSyncAI
{
    internal static partial class Icons
    {
        private static void PaintMore(Graphics g, Glyph glyph, Pen p, SolidBrush b)
        {
            switch (glyph)
            {
                case Glyph.Check:
                    L(g, p, 4.5f, 12.5f, 9.5f, 17.5f);
                    L(g, p, 9.5f, 17.5f, 19.5f, 6.5f);
                    break;

                case Glyph.Close:
                    L(g, p, 6f, 6f, 18f, 18f);
                    L(g, p, 18f, 6f, 6f, 18f);
                    break;

                case Glyph.Chevron:
                    L(g, p, 7f, 10f, 12f, 15f);
                    L(g, p, 12f, 15f, 17f, 10f);
                    break;

                case Glyph.Plus:
                    L(g, p, 12f, 5f, 12f, 19f);
                    L(g, p, 5f, 12f, 19f, 12f);
                    break;

                case Glyph.Sun:
                    g.DrawEllipse(p, 8f, 8f, 8f, 8f);
                    L(g, p, 12f, 2f, 12f, 4.5f);
                    L(g, p, 12f, 19.5f, 12f, 22f);
                    L(g, p, 2f, 12f, 4.5f, 12f);
                    L(g, p, 19.5f, 12f, 22f, 12f);
                    L(g, p, 5.2f, 5.2f, 6.9f, 6.9f);
                    L(g, p, 17.1f, 17.1f, 18.8f, 18.8f);
                    L(g, p, 5.2f, 18.8f, 6.9f, 17.1f);
                    L(g, p, 17.1f, 6.9f, 18.8f, 5.2f);
                    break;

                case Glyph.Moon:
                    g.DrawArc(p, 3.5f, 3.5f, 17f, 17f, 40, 290);
                    g.DrawArc(p, 8.5f, 1.5f, 17f, 17f, 130, 130);
                    break;

                case Glyph.Refresh:
                    g.DrawArc(p, 4f, 4f, 16f, 16f, 60, 250);
                    L(g, p, 19.5f, 5f, 19.5f, 10.5f);
                    L(g, p, 19.5f, 10.5f, 14f, 10.5f);
                    break;

                case Glyph.Link:
                    g.DrawArc(p, 2.5f, 8.5f, 11f, 7f, 100, 200);
                    g.DrawArc(p, 10.5f, 8.5f, 11f, 7f, 280, 200);
                    L(g, p, 8.5f, 12f, 15.5f, 12f);
                    break;

                case Glyph.Menu:
                    L(g, p, 4f, 7f, 20f, 7f);
                    L(g, p, 4f, 12f, 20f, 12f);
                    L(g, p, 4f, 17f, 20f, 17f);
                    break;

                case Glyph.Stop:
                    RR(g, p, 6.5f, 6.5f, 11f, 11f, 2.5f);
                    break;

                case Glyph.Tag:
                    L(g, p, 4f, 4f, 11f, 4f);
                    L(g, p, 11f, 4f, 20f, 13f);
                    L(g, p, 20f, 13f, 13f, 20f);
                    L(g, p, 13f, 20f, 4f, 11f);
                    L(g, p, 4f, 11f, 4f, 4f);
                    Dot(g, b, 8f, 8f, 1.3f);
                    break;

                case Glyph.More:
                    Dot(g, b, 6f, 12f, 1.6f);
                    Dot(g, b, 12f, 12f, 1.6f);
                    Dot(g, b, 18f, 12f, 1.6f);
                    break;

                case Glyph.External:
                    L(g, p, 10f, 5f, 5f, 5f);
                    L(g, p, 5f, 5f, 5f, 19f);
                    L(g, p, 5f, 19f, 19f, 19f);
                    L(g, p, 19f, 19f, 19f, 14f);
                    L(g, p, 11f, 13f, 20f, 4f);
                    L(g, p, 14f, 4f, 20f, 4f);
                    L(g, p, 20f, 4f, 20f, 10f);
                    break;

                case Glyph.Back:
                    L(g, p, 20f, 12f, 4f, 12f);
                    L(g, p, 4f, 12f, 10.5f, 5.5f);
                    L(g, p, 4f, 12f, 10.5f, 18.5f);
                    break;

                case Glyph.Edit:
                    L(g, p, 3.5f, 20.5f, 4.6f, 15.4f);
                    L(g, p, 4.6f, 15.4f, 15f, 5f);
                    L(g, p, 15f, 5f, 19f, 9f);
                    L(g, p, 19f, 9f, 8.6f, 19.4f);
                    L(g, p, 8.6f, 19.4f, 3.5f, 20.5f);
                    L(g, p, 12.5f, 7.5f, 16.5f, 11.5f);
                    break;

                case Glyph.Sort:
                    L(g, p, 7f, 20f, 7f, 4.5f);
                    L(g, p, 3.8f, 7.7f, 7f, 4.5f);
                    L(g, p, 7f, 4.5f, 10.2f, 7.7f);
                    L(g, p, 17f, 4f, 17f, 19.5f);
                    L(g, p, 13.8f, 16.3f, 17f, 19.5f);
                    L(g, p, 17f, 19.5f, 20.2f, 16.3f);
                    break;

                case Glyph.Tasks:
                    L(g, p, 3.5f, 7f, 5.8f, 9.3f);
                    L(g, p, 5.8f, 9.3f, 9.8f, 4.8f);
                    L(g, p, 12.5f, 7f, 20.5f, 7f);
                    L(g, p, 3.5f, 16.5f, 5.8f, 18.8f);
                    L(g, p, 5.8f, 18.8f, 9.8f, 14.3f);
                    L(g, p, 12.5f, 16.5f, 20.5f, 16.5f);
                    break;

                case Glyph.Spell:
                    L(g, p, 2.5f, 17.5f, 7.5f, 5.5f);
                    L(g, p, 7.5f, 5.5f, 12.5f, 17.5f);
                    L(g, p, 4.6f, 13f, 10.4f, 13f);
                    L(g, p, 14f, 13.6f, 16.6f, 16.2f);
                    L(g, p, 16.6f, 16.2f, 21.5f, 9.6f);
                    break;

                case Glyph.Hash:
                    L(g, p, 9.5f, 3f, 7.5f, 21f);
                    L(g, p, 17f, 3f, 15f, 21f);
                    L(g, p, 4.5f, 8.5f, 20.5f, 8.5f);
                    L(g, p, 3.5f, 15.5f, 19.5f, 15.5f);
                    break;
            }
        }

        /// The rail and the row buttons ask for an icon by its tab name, so the
        /// nav labels stay the single source of order.
        public static Glyph ForTab(string label)
        {
            if (string.Equals(label, "Clips", StringComparison.OrdinalIgnoreCase)) return Glyph.Clips;
            if (string.Equals(label, "Chat", StringComparison.OrdinalIgnoreCase)) return Glyph.Chat;
            if (string.Equals(label, "Notes", StringComparison.OrdinalIgnoreCase)) return Glyph.Notes;
            if (string.Equals(label, "Capture", StringComparison.OrdinalIgnoreCase)) return Glyph.Capture;
            return Glyph.Settings;
        }
    }
}
