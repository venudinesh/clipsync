// The shape of a list row, in one place.
//
// Clips and notes print the same kind of entry: when it happened down the left, a
// vertical rule, then what it says, with the actions appearing in whichever row
// the pointer is over. That is one design, so it is one piece of code rather than
// a drawing routine per page slowly drifting out of agreement with its twin.
//
// Every measurement here is also used for hit testing, which is the point: a row
// tests a click against the same rectangles it painted, so the two can never
// disagree about where a button was.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal static class RowKit
    {
        /// The left column, wide enough for "Mar 14" at body scale.
        public static int MarginWidth { get { return Theme.Px(52); } }

        /// One action's worth of room at the right end of a row.
        public static int ZoneWidth { get { return Theme.Px(30); } }

        /// The plate a row paints on: the row inset a little, so a hover wash has
        /// an edge of its own instead of meeting the rows above and below.
        public static Rectangle Plate(Rectangle row)
        {
            return new Rectangle(row.X + Space.Xs, row.Y + Theme.Px(1),
                Math.Max(10, row.Width - Space.Xs * 2), Math.Max(4, row.Height - Theme.Px(2)));
        }

        /// The plate for a click the ledger reported in row coordinates.
        public static Rectangle PlateOf(Rectangle rowBounds)
        {
            return Plate(new Rectangle(0, 0, rowBounds.Width, rowBounds.Height));
        }

        public static void Wash(Graphics g, Rectangle plate, bool hot, bool live)
        {
            if (live) Ui.Fill(g, plate, Radii.Control, Palette.Alpha(Theme.Accent, 0.10));
            else if (hot) Ui.Fill(g, plate, Radii.Control, Palette.Alpha(Theme.OnSurface, 0.05));
        }

        /// The margin: the stamp, a pin under it when the entry is held, and the
        /// rule that separates the margin from the text. The rule thickens and
        /// takes the accent when pinned, which is how a held row reads as held
        /// from across the window without shouting.
        public static void Margin(Graphics g, Rectangle plate, string when, bool pinned)
        {
            int mx = plate.X + Space.Sm;
            int mw = MarginWidth - Space.Sm - Theme.Px(4);
            Ui.Text(g, when, Theme.Tiny, pinned ? Theme.AccentText : Theme.Faint,
                new Rectangle(mx, plate.Y + Theme.Px(Theme.Dense ? 10 : 13), mw,
                    Theme.Tiny.Height + Theme.Px(2)),
                TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            if (pinned)
            {
                int d = Theme.Px(11);
                Icons.Draw(g, Glyph.Pin, new Rectangle(mx, plate.Bottom - d - Theme.Px(9), d, d),
                    Theme.AccentText, 1.2f);
            }
            int rule = plate.X + MarginWidth;
            Color edge = pinned ? Palette.Alpha(Theme.Accent, 0.55) : Theme.Hairline;
            using (Pen p = new Pen(edge, pinned ? Math.Max(1.4f, Theme.Px(1.6)) : 1f))
            {
                g.DrawLine(p, rule, plate.Y + Theme.Px(6), rule, plate.Bottom - Theme.Px(6));
            }
        }

        public static int TextLeft(Rectangle plate)
        {
            return plate.X + MarginWidth + Space.Md;
        }

        /// How much room the text has. It gives way to the actions only while the
        /// pointer is in the row, so a long line is not permanently cropped for
        /// buttons that are not on screen.
        public static int TextWidth(Rectangle plate, bool hot, int zones)
        {
            int keep = hot ? ZoneWidth * zones + Space.Sm : Space.Sm;
            return Math.Max(20, plate.Right - TextLeft(plate) - keep);
        }

        public static Rectangle ZoneAt(Rectangle plate, int k, int zones)
        {
            int x = plate.Right - Space.Xs - ZoneWidth * (zones - k);
            return new Rectangle(x, plate.Y, ZoneWidth, plate.Height);
        }

        /// Which action a click landed on, or -1 for none.
        public static int Hit(Rectangle plate, Point p, int zones)
        {
            for (int k = 0; k < zones; k++)
            {
                if (ZoneAt(plate, k, zones).Contains(p)) return k;
            }
            return -1;
        }

        /// The actions themselves. They are painted, not child controls: a feed of
        /// five hundred entries would otherwise carry fifteen hundred buttons that
        /// are invisible for all but one row.
        public static void Zones(Graphics g, Rectangle plate, Glyph[] icons, int lit)
        {
            if (icons == null) return;
            int d = Theme.Px(16);
            for (int k = 0; k < icons.Length; k++)
            {
                Rectangle z = ZoneAt(plate, k, icons.Length);
                Icons.Draw(g, icons[k],
                    new Rectangle(z.X + (z.Width - d) / 2, z.Y + (z.Height - d) / 2, d, d),
                    k == lit ? Theme.AccentText : Theme.Muted, Math.Max(1.3f, Theme.Px(1.5)));
            }
        }
    }
}
