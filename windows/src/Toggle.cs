// Switches and segmented pickers.
//
// The two settings shapes the app needs: a binary switch, and a small row of
// mutually exclusive choices. Both animate their marker rather than redrawing a
// different state, because the movement is what tells you which way it went.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed class Toggle : Widget
    {
        private readonly Anim _knob = new Anim(Motion.Base, Ease.Bounce);
        private bool _on;

        public event EventHandler Changed;

        public Toggle()
        {
            Width = Theme.Px(44);
            Height = Theme.Px(26);
            AccessibleRole = AccessibleRole.CheckButton;
        }

        public bool On
        {
            get { return _on; }
            set
            {
                if (_on == value) return;
                _on = value;
                _knob.To(value ? 1 : 0);
                AccessibleDescription = value ? "on" : "off";
                Invalidate();
            }
        }

        /// Sets the value without telling anybody, for loading settings into the
        /// interface without writing them straight back out.
        public void Preset(bool value)
        {
            _on = value;
            _knob.Set(value ? 1 : 0);
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            On = !_on;
            if (Changed != null) Changed(this, EventArgs.Empty);
            base.OnClick(e);
        }

        public override bool Tick()
        {
            bool busy = base.Tick();
            if (_knob.Advance()) { Invalidate(); return true; }
            return busy;
        }

        protected override void Render(Graphics g, Rectangle r)
        {
            double k = _knob.Value;
            int h = Math.Min(r.Height, Theme.Px(26));
            Rectangle track = new Rectangle(r.X, r.Y + (r.Height - h) / 2, r.Width, h);
            Color off = Palette.Mix(Theme.High, Theme.OnSurface, 0.10 + Hover.Value * 0.06);
            Color on = Palette.Mix(Theme.Accent, Color.White, Hover.Value * 0.08);
            Ui.Fill(g, track, track.Height / 2, Palette.Mix(off, on, k));
            if (k < 0.99) Ui.Stroke(g, track, track.Height / 2, Palette.Alpha(Theme.Outline, 1 - k), 1f);
            RingIfFocused(g, r, r.Height / 2);

            int pad = Theme.Px(3);
            int d = track.Height - pad * 2;
            int travel = track.Width - pad * 2 - d;
            int x = track.X + pad + (int)Math.Round(travel * k);
            Rectangle knob = new Rectangle(x, track.Y + pad, d, d);
            Ui.Shadow(g, knob, d / 2, Theme.Px(3), Color.Black, (int)(60 + 30 * k));
            Ui.Fill(g, knob, d / 2, k > 0.5 ? Theme.OnAccent : Theme.OnSurface);
        }
    }

    internal sealed class Segmented : Widget
    {
        private readonly Anim _slide = new Anim(Motion.Base);
        private string[] _options = new string[0];
        private int _index;

        public event EventHandler Changed;

        public Segmented()
        {
            Height = Theme.Px(34);
            AccessibleRole = AccessibleRole.ButtonDropDown;
        }

        public void SetOptions(string[] options, int selected)
        {
            _options = options ?? new string[0];
            _index = Clamp(selected);
            _slide.Set(_index);
            Invalidate();
        }

        public int Index
        {
            get { return _index; }
            set
            {
                int v = Clamp(value);
                if (v == _index) return;
                _index = v;
                _slide.To(v);
                AccessibleDescription = _options.Length > v ? _options[v] : "";
                Invalidate();
                if (Changed != null) Changed(this, EventArgs.Empty);
            }
        }

        public string Selected
        {
            get { return _options.Length > _index ? _options[_index] : ""; }
        }

        private int Clamp(int v)
        {
            if (_options.Length == 0) return 0;
            return v < 0 ? 0 : (v >= _options.Length ? _options.Length - 1 : v);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_options.Length == 0 || e.Button != MouseButtons.Left) return;
            int cell = Math.Max(1, Width / _options.Length);
            Index = e.X / cell;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Left || keyData == Keys.Right) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left) { Index = _index - 1; e.Handled = true; return; }
            if (e.KeyCode == Keys.Right) { Index = _index + 1; e.Handled = true; return; }
            base.OnKeyDown(e);
        }

        public override bool Tick()
        {
            bool busy = base.Tick();
            if (_slide.Advance()) { Invalidate(); return true; }
            return busy;
        }

        protected override void Render(Graphics g, Rectangle r)
        {
            Ui.Fill(g, r, Radii.Control, Theme.High);
            Ui.Stroke(g, r, Radii.Control, Theme.Hairline, 1f);
            RingIfFocused(g, r, Radii.Control);
            if (_options.Length == 0) return;

            int cell = r.Width / _options.Length;
            int pad = Theme.Px(3);
            Rectangle pill = new Rectangle(r.X + pad + (int)Math.Round(_slide.Value * cell),
                r.Y + pad, cell - pad * 2, r.Height - pad * 2);
            Ui.Fill(g, pill, Radii.Core(Radii.Control, pad), Palette.Alpha(Theme.Accent, 0.18));
            Ui.Stroke(g, pill, Radii.Core(Radii.Control, pad), Palette.Alpha(Theme.Accent, 0.55), 1f);

            for (int i = 0; i < _options.Length; i++)
            {
                Rectangle cellRect = new Rectangle(r.X + i * cell, r.Y, cell, r.Height);
                bool live = i == _index;
                Color ink = live ? Theme.AccentText : (Pointing ? Theme.OnSurface : Theme.Muted);
                Ui.Centre(g, _options[i], live ? Theme.BodyBold : Theme.Body, ink, cellRect);
            }
        }
    }
}
