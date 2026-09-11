// Where the wheel is aimed.
//
// Between a real mouse wheel and a surface that scrolls there is one decision:
// which control the pointer is over. The shell's shape is what makes that
// decision hard, so it is tested against that shape rather than against a bare
// window. Every page the app is not showing is a hidden child the size of the
// whole stage, a page keeps its window once it has been shown, and the sheet
// sits over all of them whether or not it is up.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI.Tests
{
    internal static class WheelTests
    {
        public static void Run()
        {
            T.Group("Wheel aim");
            using (Form f = new Form())
            {
                f.FormBorderStyle = FormBorderStyle.None;
                f.StartPosition = FormStartPosition.Manual;
                f.ShowInTaskbar = false;
                // Far off the desktop, and really shown. The test Windows applies
                // to a child walks the whole chain above it, so a page inside a
                // window that was never shown counts as hidden as well and every
                // answer here would come back empty. Off screen there is nothing
                // to see and nothing to click.
                f.Bounds = new Rectangle(-4000, -4000, 300, 300);
                f.Name = "window";
                f.Show();
                T.Ok("the test window has a window of its own", f.Handle != IntPtr.Zero);

                Panel shown = Page("shown");
                Panel left = Page("left");
                f.Controls.Add(shown);
                f.Controls.Add(left);
                // What switching destination leaves behind: the page being left
                // is hidden, keeps the window it was given, and is not moved out
                // of the way in the z order.
                left.Visible = false;
                left.BringToFront();
                T.Ok("a page that has been left behind keeps its window", left.Handle != IntPtr.Zero);

                Point at = f.PointToScreen(new Point(150, 150));

                // The platform behaviour that the search has to work around, kept
                // here so the reason for the extra argument cannot be lost: asked
                // without being told which children count, Windows answers with a
                // page that cannot be seen.
                T.Ok("asking without saying which children count answers with the hidden page",
                    ReferenceEquals(left, f.GetChildAtPoint(f.PointToClient(at))));

                T.Eq("the wheel goes to the page on screen, not a hidden one in front of it",
                    "shown", Named(Wheel.Deepest(f, at)));

                Panel inner = Page("inner");
                inner.Bounds = new Rectangle(100, 100, 100, 100);
                shown.Controls.Add(inner);
                T.Ok("a surface inside the page has a window too", inner.Handle != IntPtr.Zero);
                T.Eq("and it reaches the innermost surface under the pointer",
                    "inner", Named(Wheel.Deepest(f, at)));

                // A sheet is one of those hidden children until it opens, and
                // from then until it closes it is what the wheel belongs to.
                left.Visible = true;
                T.Eq("a surface that really is over the page takes the wheel",
                    "left", Named(Wheel.Deepest(f, at)));

                left.Visible = false;
                Panel off = Page("off");
                off.Enabled = false;
                f.Controls.Add(off);
                off.BringToFront();
                T.Eq("a surface that takes no input does not take the wheel either",
                    "inner", Named(Wheel.Deepest(f, at)));

                T.Eq("and a point past every page stays with the window itself",
                    "window", Named(Wheel.Deepest(f, new Point(at.X + 4000, at.Y))));
            }
        }

        private static Panel Page(string name)
        {
            Panel p = new Panel();
            p.Name = name;
            p.Bounds = new Rectangle(0, 0, 300, 300);
            return p;
        }

        private static string Named(Control c)
        {
            return c == null ? "nothing" : c.Name;
        }
    }
}
