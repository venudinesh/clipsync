// HSL conversion and the WCAG contrast helpers. Ported from
// lib/ui/design_tokens.dart so an accent cannot ship illegible.

using System;
using System.Drawing;

namespace ClipSyncAI
{
    internal static class Hsl
    {
        public static void FromColor(Color c, out double h, out double s, out double l)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double d = max - min;
            l = (max + min) / 2.0;
            if (d < 1e-9)
            {
                h = 0;
                s = 0;
                return;
            }
            s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
            if (max == r) h = ((g - b) / d + (g < b ? 6 : 0));
            else if (max == g) h = (b - r) / d + 2;
            else h = (r - g) / d + 4;
            h *= 60;
        }

        public static Color ToColor(double h, double s, double l)
        {
            h = h % 360;
            if (h < 0) h += 360;
            if (s < 0) s = 0; else if (s > 1) s = 1;
            if (l < 0) l = 0; else if (l > 1) l = 1;
            double c = (1 - Math.Abs(2 * l - 1)) * s;
            double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
            double m = l - c / 2;
            double r, g, b;
            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }
            return Color.FromArgb(
                255,
                (int)Math.Round((r + m) * 255),
                (int)Math.Round((g + m) * 255),
                (int)Math.Round((b + m) * 255));
        }
    }

    internal static class A11y
    {
        /// Relative luminance per WCAG 2.1.
        public static double Luminance(Color c)
        {
            return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
        }

        private static double Channel(int v)
        {
            double x = v / 255.0;
            return x <= 0.03928 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4);
        }

        /// Contrast ratio between two colours, 1.0 (identical) to 21.0 (black
        /// on white). Body text needs 4.5, large text and icons need 3.0.
        public static double ContrastRatio(Color a, Color b)
        {
            double la = Luminance(a), lb = Luminance(b);
            double hi = la > lb ? la : lb;
            double lo = la > lb ? lb : la;
            return (hi + 0.05) / (lo + 0.05);
        }

        /// Picks whichever of black or white text is legible on background,
        /// returned as the off-black / off-white pair rather than pure values.
        public static Color ReadableOn(Color background)
        {
            return ContrastRatio(Palette.DarkOnSurface, background) >=
                   ContrastRatio(Palette.LightOnSurface, background)
                ? Palette.DarkOnSurface
                : Palette.LightOnSurface;
        }

        /// Lifts an accent until it clears minRatio against background. The soft
        /// palette is light enough to pass on dark surfaces untouched, but a
        /// custom accent on a light background can fall short, so it gets
        /// darkened instead.
        public static Color LegibleAccent(Color accent, Color background, double minRatio)
        {
            if (ContrastRatio(accent, background) >= minRatio) return accent;
            bool backgroundIsDark = Luminance(background) < 0.5;
            double h, s, l;
            Hsl.FromColor(accent, out h, out s, out l);
            for (int i = 0; i < 24; i++)
            {
                double next = l + (backgroundIsDark ? 0.03 : -0.03);
                if (next < 0) next = 0; else if (next > 1) next = 1;
                l = next;
                Color candidate = Hsl.ToColor(h, s, l);
                if (ContrastRatio(candidate, background) >= minRatio) return candidate;
                if (next == 0.0 || next == 1.0) break;
            }
            return Hsl.ToColor(h, s, l);
        }

        public static Color LegibleAccent(Color accent, Color background)
        {
            return LegibleAccent(accent, background, 4.5);
        }
    }
}
