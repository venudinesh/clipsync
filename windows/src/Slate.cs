// The text box a field puts its words in.
//
// Windows shows a placeholder in an empty edit control on request, and it will
// not do it for a multiline one: the message that asks is single line only, and
// the composer, the note body and the recognised text are all multiline. Without
// this they sit empty with nothing said about what belongs in them.
//
// So the box draws the hint itself, after Windows has drawn the box. An edit
// control clears its own client area, so anything painted before it is lost;
// this is the one order that survives.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed class Slate : TextBox
    {
        private const int WM_PAINT = 0x000F;

        /// The words to show while there are none. A single line box is still
        /// told to show its own, because Windows clears that on the first
        /// keystroke without anything having to repaint.
        public string Hint = "";

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != WM_PAINT || !Multiline) return;
            if (Hint.Length == 0 || TextLength > 0) return;
            Ghost();
        }

        private void Ghost()
        {
            try
            {
                using (Graphics g = Graphics.FromHwnd(Handle))
                {
                    Rectangle r = ClientRectangle;
                    Ui.Line(g, Hint, Font, Theme.Muted,
                        new Rectangle(r.X + Theme.Px(1), r.Y,
                            Math.Max(10, r.Width - Theme.Px(2)), Font.Height));
                }
            }
            catch (Exception) { }
        }
    }
}
