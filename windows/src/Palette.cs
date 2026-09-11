// Palette and accessibility helpers. Ported from lib/ui/design_tokens.dart.

using System;
using System.Collections.Generic;
using System.Drawing;

namespace ClipSyncAI
{
    /// A selectable accent. Every entry is deliberately under 80% saturation,
    /// so nothing in the palette can shout.
    internal sealed class AccentSwatch
    {
        public readonly string Name;
        public readonly Color Color;
        public readonly Color Companion;

        public AccentSwatch(string name, Color color, Color companion)
        {
            Name = name;
            Color = color;
            Companion = companion;
        }
    }

    internal static class Palette
    {
        private static Color Hex(int rgb)
        {
            return Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }

        /// The soft accent set. Muted, low saturation, closer to pigment than
        /// to LED. Rose quartz is the default because it is the softest of the
        /// group and reads well on both off-black and off-white.
        public static readonly AccentSwatch[] Accents = new[]
        {
            new AccentSwatch("Rose quartz", Hex(0xDFA5B4), Hex(0xC98FA8)),
            new AccentSwatch("Sage",        Hex(0xA9C1A8), Hex(0x8FAF9B)),
            new AccentSwatch("Mist blue",   Hex(0xA6BDD1), Hex(0x8FA8C4)),
            new AccentSwatch("Sand",        Hex(0xD9BE9B), Hex(0xC7A886)),
            new AccentSwatch("Clay",        Hex(0xD2A48F), Hex(0xBC8E7C)),
            new AccentSwatch("Lilac",       Hex(0xBCAFD6), Hex(0xA79BC4)),
            new AccentSwatch("Butter",      Hex(0xDFCB96), Hex(0xCBB47F)),
            new AccentSwatch("Slate",       Hex(0xB4BCC4), Hex(0x9BA5AE)),
        };

        public static readonly Color DefaultAccent = Hex(0xDFA5B4);

        /// Derives the ambient companion for an arbitrary accent, so a custom
        /// colour still produces a monochromatic gradient rather than a second
        /// hue.
        public static Color AccentCompanion(Color accent)
        {
            for (int i = 0; i < Accents.Length; i++)
            {
                if (Accents[i].Color.ToArgb() == accent.ToArgb()) return Accents[i].Companion;
            }
            double h, s, l;
            Hsl.FromColor(accent, out h, out s, out l);
            h = (h - 14) % 360;
            if (h < 0) h += 360;
            s = Math.Min(s * 0.88, 0.78);
            l = l * 0.90;
            return Hsl.ToColor(h, s, l);
        }

        // ── Surfaces ────────────────────────────────────────────────────────
        //
        // Off-black rather than #000000: pure black flattens depth, because a
        // shadow cast onto it is invisible. True black stays reachable, but
        // only through the AMOLED toggle, where losing that depth is the point.
        public static readonly Color DarkCanvas = Hex(0x0B0B0D);
        public static readonly Color DarkLow = Hex(0x101013);
        public static readonly Color DarkBase = Hex(0x16161A);
        public static readonly Color DarkHigh = Hex(0x1D1D22);
        public static readonly Color DarkHighest = Hex(0x25252B);
        public static readonly Color DarkOnSurface = Hex(0xEDECEA); // faintly warm off-white
        public static readonly Color DarkOutline = Hex(0x33333A);

        // Warm off-white; pure #ffffff is reserved for specular highlights,
        // never for a page background.
        public static readonly Color LightCanvas = Hex(0xF7F5F2);
        public static readonly Color LightLow = Hex(0xFCFBF9);
        public static readonly Color LightBase = Hex(0xFFFEFC);
        public static readonly Color LightHigh = Hex(0xF1EEE9);
        public static readonly Color LightHighest = Hex(0xE8E4DE);
        public static readonly Color LightOnSurface = Hex(0x1A1A1C); // off-black
        public static readonly Color LightOutline = Hex(0xD8D3CB);

        /// Semantic colours, desaturated to sit beside the soft accents without
        /// hijacking attention. Danger still reads as danger; it just stops
        /// screaming.
        public static readonly Color Success = Hex(0x83B294);
        public static readonly Color Warning = Hex(0xD6A96A);
        public static readonly Color Danger = Hex(0xCE8181);
        public static readonly Color Info = Hex(0x9DB4C8);

        public static Color Mix(Color a, Color b, double t)
        {
            if (t <= 0) return a;
            if (t >= 1) return b;
            return Color.FromArgb(
                (int)Math.Round(a.A + (b.A - a.A) * t),
                (int)Math.Round(a.R + (b.R - a.R) * t),
                (int)Math.Round(a.G + (b.G - a.G) * t),
                (int)Math.Round(a.B + (b.B - a.B) * t));
        }

        public static Color Alpha(Color c, double a)
        {
            int v = (int)Math.Round(255 * (a < 0 ? 0 : (a > 1 ? 1 : a)));
            return Color.FromArgb(v, c.R, c.G, c.B);
        }
    }
}
