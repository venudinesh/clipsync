// Buttons.
//
// Five looks cover the whole app: a filled accent button for the one action a
// screen is about, soft and outline for the ones beside it, ghost for toolbar
// icons, and danger for anything that deletes. A button can also spin, which is
// how a call to a local model reports that it is thinking.

using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal enum ButtonLook { Filled, Soft, Outline, Ghost, Danger }

    internal class AppButton : Widget
    {
        private static readonly Stopwatch Spin = Stopwatch.StartNew();

        public string Label = "";
        public Glyph Icon = Glyph.Check;
        public bool ShowIcon;
        public ButtonLook Look = ButtonLook.Filled;
        public int Radius = Radii.Control;
        public int Pad = Space.Md;

        private bool _busy;

        public AppButton()
        {
            Height = Theme.Px(34);
        }

        public bool Busy
        {
            get { return _busy; }
            set
            {
                if (_busy == value) return;
                _busy = value;
                Enabled = !value;
                Cursor = value ? Cursors.WaitCursor : Cursors.Hand;
                if (value) Ticker.Poke();
                Invalidate();
            }
        }

        public override bool Tick()
        {
            bool busy = base.Tick();
            if (_busy) { Invalidate(); return true; }
            return busy;
        }

        /// The width this button wants, so a row of them can be laid out without
        /// hard coded numbers.
        public int Wants()
        {
            int w = Pad * 2;
            if (ShowIcon) w += Theme.Px(16) + (Label.Length > 0 ? Space.Sm : 0);
            if (Label.Length > 0) w += Ui.Measure(Label, Theme.BodyBold).Width;
            return Math.Max(w, Theme.Px(32));
        }

        protected override void Render(Graphics g, Rectangle r)
        {
            double h = Enabled ? Hover.Value : 0;
            double p = Press.Value;
            Color fill, ink, edge;
            Bench(h, p, out fill, out ink, out edge);

            if (Look == ButtonLook.Filled || Look == ButtonLook.Danger)
            {
                if (h > 0.01 && !Theme.ReduceMotion)
                {
                    Ui.Shadow(g, Ui.Inset(r, 2), Radius, Theme.Px(4), fill, (int)(70 * h));
                }
            }
            Ui.Fill(g, r, Radius, fill);
            if (edge.A > 0) Ui.Stroke(g, r, Radius, edge, 1f);
            RingIfFocused(g, r, Radius);

            if (!Enabled) ink = Palette.Alpha(ink, 0.45);

            Rectangle content = Ui.Inset(r, Pad);
            content.Y += (int)(p * Theme.Px(1));
            if (_busy)
            {
                int d = Theme.Px(16);
                Rectangle ring = new Rectangle(r.X + (r.Width - d) / 2, r.Y + (r.Height - d) / 2, d, d);
                float angle = (float)(Spin.Elapsed.TotalMilliseconds / Motion.Shimmer * 360.0 % 360.0);
                using (Pen pen = new Pen(ink, Math.Max(1.5f, Theme.Px(2))))
                {
                    g.DrawArc(pen, ring, angle, 260f);
                }
                return;
            }

            int textWidth = Label.Length > 0 ? Ui.Measure(Label, Theme.BodyBold).Width : 0;
            int iconSize = ShowIcon ? Theme.Px(16) : 0;
            int gap = ShowIcon && Label.Length > 0 ? Space.Sm : 0;
            int total = iconSize + gap + textWidth;
            int x = content.X + Math.Max(0, (content.Width - total) / 2);

            if (ShowIcon)
            {
                Rectangle ir = new Rectangle(x, content.Y + (content.Height - iconSize) / 2, iconSize, iconSize);
                Icons.Draw(g, Icon, ir, ink, Math.Max(1.4f, Theme.Px(1.6)));
                x += iconSize + gap;
            }
            if (Label.Length > 0)
            {
                Rectangle tr = new Rectangle(x, content.Y, textWidth + Theme.Px(2), content.Height);
                Ui.Line(g, Label, Theme.BodyBold, ink, tr);
            }
        }

        private void Bench(double h, double p, out Color fill, out Color ink, out Color edge)
        {
            edge = Color.Transparent;
            switch (Look)
            {
                case ButtonLook.Filled:
                    fill = Palette.Mix(Theme.Accent, Theme.OnSurface, p * 0.14);
                    fill = Palette.Mix(fill, Color.White, h * 0.10);
                    ink = Theme.OnAccent;
                    break;
                case ButtonLook.Danger:
                    fill = Palette.Mix(Palette.Danger, Theme.OnSurface, p * 0.14);
                    fill = Palette.Mix(fill, Color.White, h * 0.10);
                    ink = A11y.ReadableOn(Palette.Danger);
                    break;
                case ButtonLook.Soft:
                    fill = Palette.Alpha(Theme.Accent, 0.12 + h * 0.08 + p * 0.05);
                    ink = Theme.AccentText;
                    break;
                case ButtonLook.Outline:
                    fill = Palette.Alpha(Theme.OnSurface, h * 0.05);
                    edge = Palette.Mix(Theme.Outline, Theme.Accent, h);
                    ink = Theme.OnSurface;
                    break;
                default:
                    fill = Palette.Alpha(Theme.OnSurface, h * 0.07 + p * 0.04);
                    ink = Pointing ? Theme.OnSurface : Theme.Muted;
                    break;
            }
        }
    }

    /// A square ghost button holding one glyph. The whole toolbar vocabulary.
    internal sealed class IconButton : AppButton
    {
        /// One tooltip host for every icon button in the app. A glyph with no
        /// label beside it has to be able to say what it does on hover, and a
        /// WinForms tooltip belongs to a provider rather than to the control,
        /// so there is one provider instead of one per button.
        private static readonly ToolTip Tips = MakeTips();

        private static ToolTip MakeTips()
        {
            ToolTip t = new ToolTip();
            t.InitialDelay = 420;
            t.ReshowDelay = 120;
            t.AutoPopDelay = 6000;
            t.ShowAlways = true;
            return t;
        }

        public IconButton()
        {
            Look = ButtonLook.Ghost;
            ShowIcon = true;
            Radius = Radii.Tight;
            Pad = Space.Xs;
            Width = Theme.Px(32);
            Height = Theme.Px(32);
        }

        public string Tip
        {
            set
            {
                string v = value ?? "";
                AccessibleName = v;
                AccessibleDescription = v;
                try { Tips.SetToolTip(this, v); }
                catch (Exception) { }
            }
        }
    }
}
