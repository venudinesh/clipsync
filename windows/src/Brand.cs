// The app's mark.
//
// The title bar, the taskbar, Alt+Tab and the tray icon all want an icon, and they
// all get the same drawing at whatever size they asked for: the glyph the rail
// already shows, on a rounded accent tile. Nothing here is drawn by hand. A second
// hand made mark would drift from the one on screen, and at sixteen pixels a
// hand made clipboard reads as a battery.

using System;
using System.Drawing;

namespace ClipSyncAI
{
    internal sealed class Mark : IDisposable
    {
        private readonly IntPtr _handle;
        private readonly Icon _icon;

        public Mark(int size, Color accent)
        {
            int d = Math.Max(8, size);
            using (Bitmap b = new Bitmap(d, d))
            {
                using (Graphics g = Graphics.FromImage(b)) Draw(g, d, accent);
                _handle = b.GetHicon();
            }
            _icon = Icon.FromHandle(_handle);
        }

        public Icon Icon { get { return _icon; } }

        private static void Draw(Graphics g, int size, Color accent)
        {
            g.Clear(Color.Transparent);
            Ui.Q(g);
            Ui.Fill(g, new Rectangle(0, 0, size, size), Math.Max(2, size / 4), accent);
            int d = Math.Max(6, (int)(size * 0.58));
            Icons.Draw(g, Glyph.Sparkle, new Rectangle((size - d) / 2, (size - d) / 2, d, d),
                A11y.ReadableOn(accent), Math.Max(1.5f, size / 13f));
        }

        /// The handle goes back to Windows by hand. An Icon made from one does not
        /// own it, so disposing the Icon alone leaks a GDI object every time the
        /// accent changes.
        public void Dispose()
        {
            _icon.Dispose();
            Native.DropIcon(_handle);
        }
    }
}
