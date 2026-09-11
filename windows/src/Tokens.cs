// Design tokens for ClipSyncAI on Windows.
//
// A direct port of lib/ui/design_tokens.dart. Single source of truth for
// colour, shape, spacing and motion, so the desktop build and the phone build
// resolve every visual decision through the same numbers: one accent, one
// radius rule, one set of curves.

using System;
using System.Drawing;

namespace ClipSyncAI
{
    // ── MOTION ──────────────────────────────────────────────────────────────
    //
    // Durations in milliseconds. The house curve leaves heavy and glides to a
    // stop; on GDI+ it is sampled by Ease.Glass rather than handed to a
    // compositor.
    internal static class Motion
    {
        public const int Instant = 90;
        public const int Fast = 180;
        public const int Base = 280;
        public const int Slow = 420;
        public const int Reveal = 640;
        public const int Shimmer = 900;
    }

    internal static class Ease
    {
        // Cubic bezier with x1/y1/x2/y2, solved for y at time t. Newton on the
        // x polynomial: three passes land inside a pixel at these speeds.
        public static double Bezier(double t, double x1, double y1, double x2, double y2)
        {
            if (t <= 0) return 0;
            if (t >= 1) return 1;
            double u = t;
            for (int i = 0; i < 5; i++)
            {
                double x = CurveAt(u, x1, x2) - t;
                double d = SlopeAt(u, x1, x2);
                if (Math.Abs(d) < 1e-6) break;
                u -= x / d;
                if (u < 0) u = 0;
                else if (u > 1) u = 1;
            }
            return CurveAt(u, y1, y2);
        }

        private static double CurveAt(double t, double a1, double a2)
        {
            double it = 1 - t;
            return 3 * it * it * t * a1 + 3 * it * t * t * a2 + t * t * t;
        }

        private static double SlopeAt(double t, double a1, double a2)
        {
            double it = 1 - t;
            return 3 * it * it * a1 + 6 * it * t * (a2 - a1) + 3 * t * t * (1 - a2);
        }

        /// The house curve. Anything that moves a surface uses this.
        public static double Glass(double t) { return Bezier(t, 0.32, 0.72, 0, 1); }

        /// Slight overshoot for elements that physically land.
        public static double Bounce(double t) { return Bezier(t, 0.34, 1.42, 0.64, 1); }

        /// Long, weighty entry for content arriving on screen.
        public static double Entry(double t) { return Bezier(t, 0.16, 1, 0.3, 1); }

        /// Colour and opacity only. Never used for movement.
        public static double Fade(double t) { return Bezier(t, 0.4, 0, 0.2, 1); }
    }

    // ── SHAPE ───────────────────────────────────────────────────────────────
    //
    // Radii step down as elements nest. Derive an inner value with Radii.Core
    // so the curves stay parallel; that concentricity is what makes nested
    // panels read as machined rather than merely stacked.
    internal static class Radii
    {
        public const int Shell = 28;   // outermost container of a group
        public const int Card = 22;    // standard content surface
        public const int Inner = 16;   // a surface inside a card
        public const int Control = 12; // buttons, inputs, chips
        public const int Tight = 8;    // badges, swatches, dots
        public const int Pill = 999;   // capsules

        /// Inner radius that stays concentric with an outer radius across a
        /// bezel of inset pixels.
        public static int Core(int outer, int inset)
        {
            int v = outer - inset;
            return v < 0 ? 0 : v;
        }
    }

    /// Vertical and horizontal rhythm. Sections breathe at Space.Section; a
    /// dense data row is the only place Space.Xs belongs.
    internal static class Space
    {
        public const int Xs = 4;
        public const int Sm = 8;
        public const int Md = 12;
        public const int Lg = 16;
        public const int Xl = 24;
        public const int Xxl = 32;
        public const int Section = 40;
        public const int Gutter = 18; // page edge inset

        /// Width of the navigation rail. The phone build floats a capsule over
        /// the content; a desktop window has room to give navigation its own
        /// column, so the rail is a real gutter rather than an overlay and
        /// nothing has to reserve clearance underneath it.
        public const int Rail = 208;
        public const int RailCollapsed = 64;
    }
}
