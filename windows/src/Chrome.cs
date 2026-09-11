// Window chrome that only newer Windows understands.
//
// Every call here is a request, never a requirement. On Windows 11 the title bar
// takes the app's own colours and the corners round off; on 10 the dark title bar
// applies and the rest is ignored; on 7 and 8 none of it exists and the window is
// simply a normal window. Nothing in the app's behaviour depends on any of it.

using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace ClipSyncAI
{
    internal static class Chrome
    {
        private const int DarkModeOld = 19;      // pre 1903 builds
        private const int DarkMode = 20;         // DWMWA_USE_IMMERSIVE_DARK_MODE
        private const int BorderColour = 34;     // DWMWA_BORDER_COLOR
        private const int CaptionColour = 35;    // DWMWA_CAPTION_COLOR
        private const int TextColour = 36;       // DWMWA_TEXT_COLOR
        private const int CornerStyle = 33;      // DWMWA_WINDOW_CORNER_PREFERENCE
        private const int BackdropType = 38;     // DWMWA_SYSTEMBACKDROP_TYPE

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        private static bool Set(IntPtr hwnd, int attr, int value)
        {
            if (hwnd == IntPtr.Zero) return false;
            try
            {
                int v = value;
                return DwmSetWindowAttribute(hwnd, attr, ref v, 4) == 0;
            }
            catch (Exception) { return false; }
        }

        /// COLORREF is 0x00BBGGRR, the reverse of the order everything else in
        /// the app uses.
        private static int Ref(Color c)
        {
            return c.R | (c.G << 8) | (c.B << 16);
        }

        public static void UseDarkTitleBar(IntPtr hwnd, bool dark)
        {
            int on = dark ? 1 : 0;
            if (!Set(hwnd, DarkMode, on)) Set(hwnd, DarkModeOld, on);
        }

        public static void PaintTitleBar(IntPtr hwnd, Color caption, Color text, Color border)
        {
            Set(hwnd, CaptionColour, Ref(caption));
            Set(hwnd, TextColour, Ref(text));
            Set(hwnd, BorderColour, Ref(border));
        }

        /// 0 default, 1 square, 2 round, 3 slightly round.
        public static void Corners(IntPtr hwnd, int style)
        {
            Set(hwnd, CornerStyle, style);
        }

        /// 1 auto, 2 mica, 3 acrylic, 4 tabbed. Mica is the one that belongs on
        /// a window this size; it only lands on Windows 11 build 22621 and up.
        public static bool Backdrop(IntPtr hwnd, int kind)
        {
            return Set(hwnd, BackdropType, kind);
        }

        /// Applies the whole set for the current theme. Called on creation and
        /// again whenever the theme changes, because DWM keeps no memory of it
        /// across a colour change.
        public static void Apply(IntPtr hwnd)
        {
            UseDarkTitleBar(hwnd, Theme.Dark);
            PaintTitleBar(hwnd, Theme.Canvas, Theme.OnSurface, Theme.Hairline);
            Corners(hwnd, 2);
        }
    }
}
