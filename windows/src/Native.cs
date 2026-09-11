// The Win32 surface the app needs.
//
// Four jobs: hear about clipboard changes without polling, own a global hotkey,
// paste into whatever window had focus, and tell Windows we handle our own
// scaling. Everything version dependent is attempted and then forgotten about
// if it is missing, so the same binary runs on 7 and on 11.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal static class Native
    {
        public const int WM_CLIPBOARDUPDATE = 0x031D;
        public const int WM_HOTKEY = 0x0312;
        public const int WM_DPICHANGED = 0x02E0;
        public const int WM_SETTINGCHANGE = 0x001A;

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const uint MOD_NOREPEAT = 0x4000;

        private const int HWND_BROADCAST = 0xFFFF;
        private const byte VK_CONTROL = 0x11;
        private const byte VK_V = 0x56;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct DevMode
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public ushort dmSpecVersion;
            public ushort dmDriverVersion;
            public ushort dmSize;
            public ushort dmDriverExtra;
            public uint dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public uint dmDisplayOrientation;
            public uint dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public ushort dmLogPixels;
            public uint dmBitsPerPel;
            public uint dmPelsWidth;
            public uint dmPelsHeight;
            public uint dmDisplayFlags;
            public uint dmDisplayFrequency;
            public uint dmICMMethod;
            public uint dmICMIntent;
            public uint dmMediaType;
            public uint dmDitherType;
            public uint dmReserved1;
            public uint dmReserved2;
            public uint dmPanningWidth;
            public uint dmPanningHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Ansi, EntryPoint = "EnumDisplaySettingsA", SetLastError = true)]
        private static extern bool EnumDisplaySettingsA(string deviceName, int modeNum, ref DevMode devMode);

        /// The display's current refresh rate in Hertz, or 0 when it cannot be
        /// read. ENUM_CURRENT_SETTINGS is -1.
        public static int DisplayFrequency()
        {
            try
            {
                DevMode dm = new DevMode();
                dm.dmSize = (ushort)Marshal.SizeOf(typeof(DevMode));
                if (EnumDisplaySettingsA(null, -1, ref dm)) return (int)dm.dmDisplayFrequency;
            }
            catch (Exception) { }
            return 0;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint mods, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern uint RegisterWindowMessage(string name);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);

        [DllImport("user32.dll")]
        private static extern bool HideCaret(IntPtr hwnd);

        /// Hides the caret in a read only text surface. A blinking caret in what
        /// reads as a paragraph of prose says the text is editable when it is
        /// not; selection still works, so nothing is lost by hiding it.
        public static void NoCaret(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            try { HideCaret(hwnd); }
            catch (Exception) { }
        }

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr icon);

        /// Hands an icon handle back to Windows. A Bitmap can produce one but does
        /// not own it, and an Icon built from one does not own it either, so this
        /// is the only thing that frees it.
        public static void DropIcon(IntPtr icon)
        {
            if (icon == IntPtr.Zero) return;
            try { DestroyIcon(icon); }
            catch (Exception) { }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessageW(IntPtr hwnd, int msg, IntPtr w, string l);

        /// The placeholder an edit control shows while it is empty. Native since
        /// XP with visual styles on, and silently ignored otherwise, which is why
        /// no field depends on it for meaning.
        public static void SetCueBanner(IntPtr edit, string text)
        {
            const int EM_SETCUEBANNER = 0x1501;
            if (edit == IntPtr.Zero) return;
            try { SendMessageW(edit, EM_SETCUEBANNER, new IntPtr(1), text ?? ""); }
            catch (Exception) { }
        }

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("shcore.dll")]
        private static extern int SetProcessDpiAwareness(int value);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        /// Told to Windows before the first window exists, newest call first.
        /// Per monitor v2 is what keeps a window crisp when it is dragged to a
        /// second screen; the older calls leave it merely correct at start up.
        public static void DeclareDpiAwareness()
        {
            try
            {
                // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
                if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return;
            }
            catch (Exception) { }
            try
            {
                if (SetProcessDpiAwareness(2) == 0) return;   // PROCESS_PER_MONITOR_DPI_AWARE
            }
            catch (Exception) { }
            try { SetProcessDPIAware(); }
            catch (Exception) { }
        }

        /// The scale factor for a window, as a multiplier on 96 dpi. Falls back
        /// to the device context, which is all Windows 7 offers.
        public static double ScaleOf(IntPtr hwnd)
        {
            try
            {
                uint dpi = GetDpiForWindow(hwnd);
                if (dpi >= 72 && dpi <= 480) return dpi / 96.0;
            }
            catch (Exception) { }
            try
            {
                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromHwnd(hwnd))
                {
                    return g.DpiX / 96.0;
                }
            }
            catch (Exception) { return 1.0; }
        }

        public static bool ListenToClipboard(IntPtr hwnd)
        {
            try { return AddClipboardFormatListener(hwnd); }
            catch (Exception) { return false; }
        }

        public static void StopListeningToClipboard(IntPtr hwnd)
        {
            try { RemoveClipboardFormatListener(hwnd); }
            catch (Exception) { }
        }

        public static bool BindHotkey(IntPtr hwnd, int id, uint mods, uint vk)
        {
            try { return RegisterHotKey(hwnd, id, mods | MOD_NOREPEAT, vk); }
            catch (Exception) { return false; }
        }

        public static void ReleaseHotkey(IntPtr hwnd, int id)
        {
            try { UnregisterHotKey(hwnd, id); }
            catch (Exception) { }
        }

        public static IntPtr Foreground()
        {
            try { return GetForegroundWindow(); }
            catch (Exception) { return IntPtr.Zero; }
        }

        /// Hands focus back to the window that had it and sends Ctrl+V. Used by
        /// the overlay: pick a clip, it lands in the app you were typing in.
        public static void PasteInto(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            try
            {
                SetForegroundWindow(hwnd);
                keybd_event(VK_CONTROL, 0, 0, IntPtr.Zero);
                keybd_event(VK_V, 0, 0, IntPtr.Zero);
                keybd_event(VK_V, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
            }
            catch (Exception) { }
        }

        /// The process behind the foreground window, for attributing a clip to
        /// where it came from. Empty when it cannot be determined, which is
        /// normal for elevated windows.
        public static string ForegroundApp()
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return "";
                uint pid;
                GetWindowThreadProcessId(hwnd, out pid);
                if (pid == 0) return "";
                if (pid == (uint)Process.GetCurrentProcess().Id) return "";
                using (Process p = Process.GetProcessById((int)pid)) return p.ProcessName;
            }
            catch (Exception) { return ""; }
        }

        private static uint _showMessage;

        /// A message every ClipSyncAI window answers, so a second launch can ask
        /// the running one to come forward instead of opening a rival window.
        public static uint ShowMessage()
        {
            if (_showMessage == 0)
            {
                try { _showMessage = RegisterWindowMessage("ClipSyncAI.Show.v1"); }
                catch (Exception) { _showMessage = 0; }
            }
            return _showMessage;
        }

        public static void BroadcastShow()
        {
            uint msg = ShowMessage();
            if (msg == 0) return;
            try { PostMessage(new IntPtr(HWND_BROADCAST), msg, IntPtr.Zero, IntPtr.Zero); }
            catch (Exception) { }
        }

        public const int WM_MOUSEWHEEL = 0x020A;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr w, IntPtr l);

        /// The first line an edit control is showing, counting a wrapped line as a
        /// line. There is no property for it: the control knows where it has
        /// scrolled to and this is the only way to ask.
        public static int FirstVisibleLine(IntPtr edit)
        {
            const int EM_GETFIRSTVISIBLELINE = 0x00CE;
            if (edit == IntPtr.Zero) return 0;
            try { return SendMessage(edit, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero).ToInt32(); }
            catch (Exception) { return 0; }
        }

        /// Scrolls an edit control by a number of lines, the way its own scrollbar
        /// would. Negative goes up. The control clamps at both ends itself, so
        /// there is nothing to check before asking.
        public static void ScrollLines(IntPtr edit, int lines)
        {
            const int EM_LINESCROLL = 0x00B6;
            if (edit == IntPtr.Zero || lines == 0) return;
            try { SendMessage(edit, EM_LINESCROLL, IntPtr.Zero, new IntPtr(lines)); }
            catch (Exception) { }
        }

        /// Hands a message to a different window of this app, unchanged. Used for
        /// the wheel: Windows sends it to whatever has focus, and this app wants
        /// it to go to whatever the pointer is over.
        public static void Relay(IntPtr hwnd, int msg, IntPtr w, IntPtr l)
        {
            if (hwnd == IntPtr.Zero) return;
            try { PostMessage(hwnd, (uint)msg, w, l); }
            catch (Exception) { }
        }
    }
}
