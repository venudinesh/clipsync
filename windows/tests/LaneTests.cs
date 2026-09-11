// The painted scrollbar, driven.
//
// Windows draws its own inside an edit control, in the system's colours, and will
// not recolour it on 7 or 8. So the control is told to have none and a thumb is
// painted instead, keeping no record of the text: where the box is scrolled to
// and how far it can go are asked of the box itself.
//
// Which makes the thumb testable the way a person tests it, by pressing it and
// pulling it down. That is the one part of the walk a photograph cannot show,
// because a picture of a thumb halfway down proves nothing about how it got
// there.

using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace ClipSyncAI.Tests
{
    internal static class LaneTests
    {
        private const int Wide = 300;
        private const int Tall = 200;

        public static void Run()
        {
            T.Group("Painted scrollbar");
            using (Form f = Stage())
            {
                Panel host = new Panel();
                host.Bounds = new Rectangle(0, 0, Wide, Tall);
                f.Controls.Add(host);

                TextBox box = new TextBox();
                box.Multiline = true;
                box.WordWrap = false;
                box.Bounds = new Rectangle(0, 0, Wide - Lane.Gutter, Tall);
                host.Controls.Add(box);
                Theme.Wear(box, Theme.Body);
                T.Ok("the box has a window, so it can be asked where it is",
                    box.IsHandleCreated);

                Lane lane = new Lane(host, box);
                T.Eq("the gutter is the width the theme asks for", Theme.Px(8), Lane.Gutter);

                box.Text = "one short line";
                T.Ok("nothing to scroll shows no thumb", !lane.Shows);
                T.Ok("and there is no thumb to draw", lane.Thumb() == Rectangle.Empty);

                box.Text = Lines(200);
                T.Ok("text past the bottom shows a thumb", lane.Shows);
                Shape(lane);
                Reaches(lane, box);
                Pressed(lane, box);
                Dragged(lane, box);
                Notched(lane, box);
                Drawn(lane, host);
            }
        }

        /// A window off the desktop that is really shown. A control only answers
        /// where it is scrolled to once it has a window, and the pages this lane
        /// belongs to are built inside a window like this one.
        private static Form Stage()
        {
            Form f = new Form();
            f.FormBorderStyle = FormBorderStyle.None;
            f.StartPosition = FormStartPosition.Manual;
            f.ShowInTaskbar = false;
            f.Bounds = new Rectangle(-4000, -4000, Wide + 20, Tall + 20);
            f.Show();
            return f;
        }

        private static string Lines(int n)
        {
            StringBuilder sb = new StringBuilder(n * 12);
            for (int i = 0; i < n; i++) sb.Append("line ").Append(i).Append("\r\n");
            return sb.ToString();
        }

        private static MouseEventArgs At(int x, int y)
        {
            return new MouseEventArgs(MouseButtons.Left, 1, x, y, 0);
        }

        /// Where the thumb sits when the box is at the top, and how big it is.
        private static void Shape(Lane lane)
        {
            Rectangle t = lane.Thumb();
            T.Eq("the thumb sits in the gutter", Wide - Lane.Gutter, t.X);
            T.Eq("four pixels wide, the same as the feed's", Theme.Px(4), t.Width);
            T.Ok("no shorter than the least it may be", t.Height >= Theme.Px(24));
            T.Eq("and starts at the top of its track", Space.Sm, t.Y);
            T.Ok("with room left below it to travel", t.Bottom < Tall - Space.Sm);
        }

        /// The two ends. Scrolling the box is what moves the thumb, never the
        /// other way about, so the box is scrolled by the message the control
        /// answers and the thumb is asked where it ended up.
        private static void Reaches(Lane lane, TextBox box)
        {
            Native.ScrollLines(box.Handle, 5000);
            Rectangle low = lane.Thumb();
            T.Ok("scrolled to the bottom the thumb has moved down", low.Y > Space.Sm);
            T.Ok("and stops inside its track", low.Bottom <= Tall - Space.Sm);
            Native.ScrollLines(box.Handle, -5000);
            T.Eq("scrolled back it returns to the top", Space.Sm, lane.Thumb().Y);
            T.Eq("and the box is back on its first line", 0,
                Native.FirstVisibleLine(box.Handle));
        }

        /// A press. The host asks the lane about every one before it lets the box
        /// put a caret where it landed, so what the lane claims matters as much as
        /// what it does with it: the gutter is the lane's, the words are not, and
        /// a press in the empty track pages rather than jumping to the pointer.
        private static void Pressed(Lane lane, TextBox box)
        {
            Rectangle t = lane.Thumb();
            T.Ok("a press in the words is not the lane's",
                !lane.Down(At(t.X - Theme.Px(20), t.Y + 2)));
            T.Ok("a press on the thumb is", lane.Down(At(t.X + 1, t.Y + 2)));
            T.Eq("and taking hold of it alone scrolls nothing", 0,
                Native.FirstVisibleLine(box.Handle));
            lane.Up();

            T.Ok("a press in the empty track below is the lane's too",
                lane.Down(At(t.X + 1, t.Bottom + Theme.Px(4))));
            lane.Up();
            int down = Native.FirstVisibleLine(box.Handle);
            T.Ok("and it moves down rather than jumping to the pointer", down > 0);
            T.Ok("by a boxful, not by a line", down > 3);

            Rectangle low = lane.Thumb();
            T.Ok("the thumb has left the top of its track", low.Y > Space.Sm);
            T.Ok("a press in the track above it is the lane's as well",
                lane.Down(At(low.X + 1, Space.Sm)));
            lane.Up();
            T.Eq("and it pages back to the first line", 0,
                Native.FirstVisibleLine(box.Handle));
        }

        /// The drag. This is the part a photograph of a thumb halfway down cannot
        /// prove, because a picture says nothing about how it got there: the press
        /// takes hold, the move carries it, and the box has to follow by as much as
        /// the pointer travelled.
        private static void Dragged(Lane lane, TextBox box)
        {
            Rectangle t = lane.Thumb();
            T.Ok("a move with nothing held is not the lane's",
                !lane.Drag(At(t.X + 1, Tall / 2)));
            T.Eq("and it moved nothing", 0, Native.FirstVisibleLine(box.Handle));

            int pull = Theme.Px(60);
            lane.Down(At(t.X + 1, t.Y + 2));
            T.Ok("with the thumb held, a move is the lane's",
                lane.Drag(At(t.X + 1, t.Y + 2 + pull)));
            int mid = Native.FirstVisibleLine(box.Handle);
            T.Ok("and the box has followed it down", mid > 0);
            T.Near("the thumb has come along under the pointer",
                t.Y + pull, lane.Thumb().Y, 3);

            lane.Drag(At(t.X + 1, Tall * 4));
            T.Ok("pulled past the bottom the box goes further still",
                Native.FirstVisibleLine(box.Handle) > mid);
            Rectangle end = lane.Thumb();
            T.Ok("and the thumb stops inside its track", end.Bottom <= Tall - Space.Sm);

            lane.Drag(At(t.X + 1, -Tall));
            T.Eq("pulled back above the top it returns to the first line", 0,
                Native.FirstVisibleLine(box.Handle));
            lane.Up();
            T.Ok("and once it is let go a move is no longer the lane's",
                !lane.Drag(At(t.X + 1, Tall / 2)));
        }

        /// The wheel, which arrives at the lane in the same detents Windows sends
        /// everywhere else. Three lines a notch, and a part of a notch is kept
        /// rather than rounded up, so a fine wheel does not stutter.
        private static void Notched(Lane lane, TextBox box)
        {
            lane.Wheel(-120);
            T.Eq("a notch down moves three lines", 3, Native.FirstVisibleLine(box.Handle));
            lane.Wheel(-240);
            T.Eq("two notches at once move six more", 9,
                Native.FirstVisibleLine(box.Handle));
            lane.Wheel(60);
            T.Eq("half a notch moves nothing", 9, Native.FirstVisibleLine(box.Handle));
            lane.Wheel(120 * 3);
            T.Eq("and three notches up come back to the first line", 0,
                Native.FirstVisibleLine(box.Handle));
        }

        /// The paint. Two shades of the one colour, the stronger while it is being
        /// held, and nothing anywhere but the thumb itself.
        private static void Drawn(Lane lane, Control host)
        {
            Rectangle t = lane.Thumb();
            int x = t.X + t.Width / 2;
            int y = t.Y + t.Height / 2;
            using (Bitmap b = new Bitmap(host.Width, host.Height))
            using (Graphics g = Graphics.FromImage(b))
            {
                g.Clear(Theme.Canvas);
                lane.Paint(g);
                string rest = T.Hex(b.GetPixel(x, y));
                T.Ok("the thumb is painted", rest != T.Hex(Theme.Canvas));
                T.Eq("and the words beside it are left alone", T.Hex(Theme.Canvas),
                    T.Hex(b.GetPixel(t.X - Theme.Px(20), y)));
                T.Eq("as is the track it has not reached", T.Hex(Theme.Canvas),
                    T.Hex(b.GetPixel(x, host.Height - Space.Sm - 1)));

                g.Clear(Theme.Canvas);
                lane.Down(At(t.X + 1, t.Y + 2));
                lane.Paint(g);
                string held = T.Hex(b.GetPixel(x, y));
                lane.Up();
                T.Ok("held, it is drawn in the stronger shade", held != rest);

                g.Clear(Theme.Canvas);
                lane.Paint(g);
                T.Eq("and let go it is back to the quiet one", rest,
                    T.Hex(b.GetPixel(x, y)));
            }
        }
    }
}
