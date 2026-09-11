// The resolved theme. Views read colours and fonts from here and nowhere else,
// which is what keeps one accent and one surface family across every screen.

using System;
using System.Drawing;

namespace ClipSyncAI
{
    internal static class Theme
    {
        public static bool Dark = true;
        public static bool Amoled;
        public static Color Accent = Palette.DefaultAccent;

        /// Display scale, taken from the system DPI at startup. Every spacing
        /// and font number passes through Px so a 150% display gets a bigger
        /// app rather than a blurry one.
        public static double Scale = 1.0;

        /// Platform asks for less motion. Ambient loops stop and transitions
        /// collapse to a cross fade.
        public static bool ReduceMotion;

        /// Rows sit tighter. A desktop pointer can hit a smaller target than a
        /// thumb, and a clipboard feed is a list you scan rather than read.
        public static bool Dense;

        public static int Px(double v)
        {
            int r = (int)Math.Round(v * Scale);
            return r < 1 && v > 0 ? 1 : r;
        }

        public static Color Canvas
        {
            get { return Dark ? (Amoled ? Color.Black : Palette.DarkCanvas) : Palette.LightCanvas; }
        }

        public static Color Low
        {
            get { return Dark ? (Amoled ? Palette.DarkCanvas : Palette.DarkLow) : Palette.LightLow; }
        }

        public static Color Base
        {
            get { return Dark ? (Amoled ? Palette.DarkLow : Palette.DarkBase) : Palette.LightBase; }
        }

        public static Color High
        {
            get { return Dark ? (Amoled ? Palette.DarkBase : Palette.DarkHigh) : Palette.LightHigh; }
        }

        public static Color Highest
        {
            get { return Dark ? (Amoled ? Palette.DarkHigh : Palette.DarkHighest) : Palette.LightHighest; }
        }

        public static Color OnSurface
        {
            get { return Dark ? Palette.DarkOnSurface : Palette.LightOnSurface; }
        }

        /// Secondary text. A tint of the canvas rather than a grey of its own,
        /// so one palette stays in force.
        public static Color Muted
        {
            get { return Palette.Mix(OnSurface, Canvas, 0.42); }
        }

        /// Tertiary text: timestamps in the ledger margin, helper lines.
        public static Color Faint
        {
            get { return Palette.Mix(OnSurface, Canvas, 0.62); }
        }

        public static Color Outline
        {
            get { return Dark ? Palette.DarkOutline : Palette.LightOutline; }
        }

        /// The hairline a ledger row sits on. Lighter than an outline, because a
        /// rule between rows is a rhythm and not a border.
        public static Color Hairline
        {
            get { return Palette.Mix(Outline, Canvas, Dark ? 0.35 : 0.25); }
        }

        /// The accent, contrast lifted against the current background before it
        /// is drawn, so a chosen accent cannot ship illegible.
        public static Color AccentOn(Color background)
        {
            return A11y.LegibleAccent(Accent, background, 4.5);
        }

        public static Color AccentText
        {
            get { return A11y.LegibleAccent(Accent, Canvas, 4.5); }
        }

        /// Ink for a filled accent surface: whichever of the off-black or
        /// off-white pair is legible on the accent itself.
        public static Color OnAccent
        {
            get { return A11y.ReadableOn(Accent); }
        }

        // ── Type ────────────────────────────────────────────────────────────
        //
        // Segoe UI is the platform face on every Windows this app supports, and
        // matching the shell is the right call for an app that lives in the
        // tray. Consolas carries code and any raw clipboard payload.
        public const string Sans = "Segoe UI";
        public const string Mono = "Consolas";

        private static Font _display, _title, _heading, _body, _bodyBold, _small, _tiny, _mono, _monoSmall;

        public static void BuildFonts()
        {
            Dispose(ref _display); Dispose(ref _title); Dispose(ref _heading);
            Dispose(ref _body); Dispose(ref _bodyBold); Dispose(ref _small);
            Dispose(ref _tiny); Dispose(ref _mono); Dispose(ref _monoSmall);
            float k = (float)Scale;
            _display = new Font(Sans, 21f * k, FontStyle.Regular, GraphicsUnit.Pixel);
            _title = new Font(Sans, 16f * k, FontStyle.Regular, GraphicsUnit.Pixel);
            _heading = new Font(Sans, 13f * k, FontStyle.Bold, GraphicsUnit.Pixel);
            _body = new Font(Sans, 12.5f * k, FontStyle.Regular, GraphicsUnit.Pixel);
            _bodyBold = new Font(Sans, 12.5f * k, FontStyle.Bold, GraphicsUnit.Pixel);
            _small = new Font(Sans, 11f * k, FontStyle.Regular, GraphicsUnit.Pixel);
            _tiny = new Font(Sans, 10f * k, FontStyle.Regular, GraphicsUnit.Pixel);
            _mono = new Font(Mono, 12f * k, FontStyle.Regular, GraphicsUnit.Pixel);
            _monoSmall = new Font(Mono, 10.5f * k, FontStyle.Regular, GraphicsUnit.Pixel);
        }

        private static void Dispose(ref Font f)
        {
            if (f != null) { f.Dispose(); f = null; }
        }

        /// Hands a control one of these fonts. The plain assignment is not enough
        /// on its own: a control compares fonts by their metrics rather than by
        /// identity, and drops an assignment that looks like the value it already
        /// holds. Straight after a rebuild that value is a font which has just
        /// been disposed, and the control goes on measuring itself with something
        /// that is gone until the metrics happen to change too. Clearing it first
        /// makes the handover a real change.
        public static void Wear(System.Windows.Forms.Control c, Font f)
        {
            if (c == null || f == null) return;
            c.Font = null;
            c.Font = f;
        }

        public static Font Display { get { Ensure(); return _display; } }
        public static Font Title { get { Ensure(); return _title; } }
        public static Font Heading { get { Ensure(); return _heading; } }
        public static Font Body { get { Ensure(); return _body; } }
        public static Font BodyBold { get { Ensure(); return _bodyBold; } }
        public static Font Small { get { Ensure(); return _small; } }
        public static Font Tiny { get { Ensure(); return _tiny; } }
        public static Font MonoFont { get { Ensure(); return _mono; } }
        public static Font MonoSmall { get { Ensure(); return _monoSmall; } }

        private static void Ensure()
        {
            if (_body == null) BuildFonts();
        }

        /// Takes the stored settings on. Called at startup and whenever settings
        /// change, so nothing else in the app has to look at AppSettings to know
        /// what colour to paint.
        public static void Adopt(AppSettings s)
        {
            if (s == null) return;
            Dark = s.Dark;
            Amoled = s.Amoled;
            Dense = s.Dense;
            ReduceMotion = s.ReduceMotion;
            Accent = s.Accent;
        }

        /// The system's own light or dark preference, for follow the system mode.
        /// The registry value is absent on Windows 7 and 8, where the shell had
        /// no dark mode, so light is the honest answer there.
        public static bool SystemPrefersDark()
        {
            try
            {
                object v = Microsoft.Win32.Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "AppsUseLightTheme", null);
                if (v is int) return (int)v == 0;
            }
            catch (Exception) { }
            return false;
        }
    }
}
