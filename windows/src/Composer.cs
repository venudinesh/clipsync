// The composer.
//
// A recessed instrument rather than another underlined row: this is the one place
// on a page you type into, and it should not look like the setting above it. The
// primary verb sits at its bottom right, quiet alternatives at its bottom left,
// which is the order the phone build settled on and the order a mouse expects.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed class Composer : Card
    {
        public readonly Field Input = new Field();
        public readonly AppButton Go = new AppButton();

        private readonly List<AppButton> _quiet = new List<AppButton>();
        private int _lines = 3;

        public event EventHandler Submitted;

        public Composer()
        {
            Radius = Radii.Inner;
            Inset = Space.Md;
            Surface = Color.Empty;
            Input.Multiline = true;
            Input.Radius = Radii.Control;
            Input.Submitted += OnGo;
            Go.Label = "Keep";
            Go.ShowIcon = true;
            Go.Icon = Glyph.Check;
            Go.Look = ButtonLook.Filled;
            Go.Click += OnGo;
            Controls.Add(Input);
            Controls.Add(Go);
        }

        /// How many lines of text the field shows before it scrolls.
        public int Lines
        {
            get { return _lines; }
            set { _lines = value < 1 ? 1 : value; }
        }

        public string Hint
        {
            get { return Input.Placeholder; }
            set { Input.Placeholder = value; }
        }

        public override string Text
        {
            get { return Input.Text; }
            set { Input.Text = value; }
        }

        public bool Busy
        {
            get { return Go.Busy; }
            set { Go.Busy = value; }
        }

        /// A secondary way in: paste the clipboard, jump to dictation. Named,
        /// because a bare glyph beside a text field says nothing.
        public AppButton Quiet(Glyph icon, string label, EventHandler act)
        {
            AppButton b = new AppButton();
            b.Look = ButtonLook.Ghost;
            b.ShowIcon = true;
            b.Icon = icon;
            b.Label = label;
            b.Pad = Space.Sm;
            b.AccessibleName = label;
            if (act != null) b.Click += act;
            _quiet.Add(b);
            Controls.Add(b);
            return b;
        }

        private void OnGo(object sender, EventArgs e)
        {
            if (Submitted != null) Submitted(this, EventArgs.Empty);
        }

        public int Wants()
        {
            return Inset * 2 + FieldHeight + Space.Sm + Theme.Px(34);
        }

        private int FieldHeight
        {
            get { return Input.Wants(_lines); }
        }

        public void Lay()
        {
            Rectangle r = Inner;
            Input.SetBounds(r.X, r.Y, Math.Max(20, r.Width), FieldHeight);
            int y = Input.Bottom + Space.Sm;
            int h = Theme.Px(34);
            int gw = Math.Max(Theme.Px(84), Go.Wants());
            Go.SetBounds(r.Right - gw, y, gw, h);
            int x = r.X;
            for (int i = 0; i < _quiet.Count; i++)
            {
                int w = _quiet[i].Wants();
                if (x + w > Go.Left - Space.Sm) { _quiet[i].Visible = false; continue; }
                _quiet[i].Visible = true;
                _quiet[i].SetBounds(x, y, w, h);
                x += w + Space.Xs;
            }
        }

        public void ApplyTheme()
        {
            Input.ApplyTheme();
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Lay();
        }
    }
}
