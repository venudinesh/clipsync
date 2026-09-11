// Text fields and switches.
//
// A field wraps a real TextBox rather than drawing its own caret: editing, IME
// composition, selection, undo and the clipboard shortcuts all have to behave
// exactly as Windows users expect, and none of that is worth reimplementing for
// a border. The frame around it is drawn, so it matches everything else.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed class Field : Control, IAnimated
    {
        private readonly Slate _box = new Slate();
        private readonly Anim _focus = new Anim(Motion.Fast);
        private readonly Anim _hover = new Anim(Motion.Fast);
        private readonly Lane _lane;
        private string _placeholder = "";
        private bool _onTrail;

        public bool ShowIcon;
        public Glyph Icon = Glyph.Search;
        public int Radius = Radii.Control;

        /// An action drawn at the end of the field, used for the clear cross on a
        /// search box. It is painted by the field rather than parented into it: a
        /// child control clears itself to its parent's colour first, so a button
        /// sitting on the fill would print a flat square over it.
        public bool ShowTrail;
        public Glyph Trail = Glyph.Close;
        public event EventHandler Trailed;

        /// A field with no frame, for the places where the text is the page
        /// rather than an entry in a form: a note's title, its tags, its body.
        /// Focus is shown as a rule under the line instead of a border.
        public bool Bare;

        /// The face the text is typed in, when body size is wrong for the job.
        /// It has to be reassigned on a theme change, because the theme rebuilds
        /// its fonts, so owners set this in their own ApplyTheme.
        public Font Face;

        public event EventHandler Submitted;
        public event EventHandler Edited;

        public Field()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            _lane = new Lane(this, _box);
            _box.BorderStyle = BorderStyle.None;
            _box.AutoSize = false;
            _box.GotFocus += delegate { _focus.To(1); Invalidate(); };
            _box.LostFocus += delegate { _focus.To(0); Invalidate(); };
            _box.MouseEnter += delegate { _hover.To(1); };
            _box.MouseLeave += delegate { _hover.To(0); };
            _box.TextChanged += OnBoxText;
            _box.KeyDown += OnBoxKey;
            _box.KeyUp += delegate { Bumped(); };
            _box.MouseUp += delegate { Bumped(); };
            _box.MouseWheel += OnBoxWheel;
            Controls.Add(_box);
            Height = Theme.Px(36);
            Ticker.Join(this);
            ApplyTheme();
        }

        /// Repaints the thumb after the box has moved rather than before. An edit
        /// control scrolls itself in its own message handling, which runs after
        /// the events above, so the paint has to be posted behind it.
        private void Bumped()
        {
            if (!_box.Multiline || !IsHandleCreated) return;
            try { BeginInvoke(new MethodInvoker(Invalidate)); }
            catch (Exception) { }
        }

        /// The wheel over the text. The field scrolls the box itself and says the
        /// message is dealt with, so the movement is the same number of lines the
        /// thumb is drawn from and behaves the same on every Windows.
        private void OnBoxWheel(object sender, MouseEventArgs e)
        {
            HandledMouseEventArgs h = e as HandledMouseEventArgs;
            if (h != null) h.Handled = true;
            _lane.Wheel(e.Delta);
        }

        public TextBox Box { get { return _box; } }

        /// The height this field needs to show a number of lines of its own face,
        /// frame and padding included. An owner that wants three lines of text
        /// asks for three rather than adding up the padding itself and being one
        /// line out.
        public int Wants(int lines)
        {
            Font f = Face != null ? Face : Theme.Body;
            int pad = Bare ? Theme.Px(1) : Space.Sm + Theme.Px(2);
            return f.Height * (lines < 1 ? 1 : lines) + pad * 2;
        }

        public override string Text
        {
            get { return _box.Text; }
            set { _box.Text = value ?? ""; }
        }

        public string Placeholder
        {
            get { return _placeholder; }
            set
            {
                _placeholder = value ?? "";
                AccessibleName = _placeholder;
                _box.Hint = _placeholder;
                if (_box.IsHandleCreated) Native.SetCueBanner(_box.Handle, _placeholder);
                _box.Invalidate();
            }
        }

        /// A multiline field has no Windows scrollbar. It is drawn by the system
        /// in the system's colours, and it cannot be recoloured on 7 or 8, so the
        /// field draws its own in the theme instead.
        public bool Multiline
        {
            get { return _box.Multiline; }
            set
            {
                _box.Multiline = value;
                _box.ScrollBars = ScrollBars.None;
                _box.AcceptsReturn = value;
                _box.Invalidate();
                Place();
            }
        }

        public bool Password
        {
            get { return _box.UseSystemPasswordChar; }
            set { _box.UseSystemPasswordChar = value; }
        }

        /// Enter submits a field; in a multiline field Shift+Enter inserts a
        /// line instead, which is what chat inputs everywhere do.
        /// Escape empties a field that offers a clear action, so the mouse is not
        /// the only way to undo a search. The trail action stays a mouse action:
        /// a field whose trail is "browse for a file" must not throw its file
        /// dialog open at whoever presses Escape.
        private void OnBoxKey(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && ShowTrail && _box.Text.Length > 0)
            {
                e.SuppressKeyPress = true;
                _box.Text = "";
                _box.Focus();
                return;
            }
            bool send = _box.Multiline
                ? e.KeyCode == Keys.Enter && !e.Shift
                : e.KeyCode == Keys.Enter;
            if (!send) return;
            e.SuppressKeyPress = true;
            if (Submitted != null) Submitted(this, EventArgs.Empty);
        }

        private void OnBoxText(object sender, EventArgs e)
        {
            if (ShowTrail) Invalidate();
            Bumped();
            if (Edited != null) Edited(this, EventArgs.Empty);
        }

        private Rectangle TrailRect()
        {
            int w = Theme.Px(16) + Space.Sm * 2;
            return new Rectangle(Math.Max(0, Width - w - Theme.Px(2)), 0, w, Height);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_lane.Drag(e)) return;
            bool on = ShowTrail && TrailRect().Contains(e.Location);
            if (on == _onTrail) return;
            _onTrail = on;
            Cursor = on ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover.To(0);
            if (!_onTrail) return;
            _onTrail = false;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hover.To(1);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_lane.Down(e)) return;
            if (ShowTrail && TrailRect().Contains(e.Location))
            {
                _box.Text = "";
                if (Trailed != null) Trailed(this, EventArgs.Empty);
                return;
            }
            _box.Focus();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _lane.Up();
        }

        /// The wheel over the frame, the padding or the thumb's lane, none of
        /// which the box itself covers.
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            _lane.Wheel(e.Delta);
        }

        public void ApplyTheme()
        {
            BackColor = Theme.Canvas;
            _box.BackColor = Bare ? Behind : Theme.High;
            _box.ForeColor = Theme.OnSurface;
            Theme.Wear(_box, Face != null ? Face : Theme.Body);
            Place();
            Invalidate();
        }

        private Color Behind
        {
            get { return Parent != null ? Parent.BackColor : Theme.Canvas; }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.SetCueBanner(_box.Handle, _placeholder);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Place();
        }

        private void Place()
        {
            int pad = Bare ? Theme.Px(1) : Space.Sm + Theme.Px(2);
            int left = pad + (ShowIcon ? Theme.Px(16) + Space.Sm : 0);
            int right = pad + (ShowTrail ? Theme.Px(16) + Space.Sm : 0) +
                        (_box.Multiline ? Lane.Gutter : 0);
            int h = _box.Multiline ? Height - pad * 2 : _box.PreferredHeight;
            int y = _box.Multiline ? pad : Math.Max(pad - Theme.Px(2), (Height - h) / 2);
            _box.SetBounds(left, y, Math.Max(10, Width - left - right), Math.Max(10, h));
        }

        public bool Tick()
        {
            bool busy = _focus.Advance() | _hover.Advance();
            if (busy) Invalidate();
            return busy;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Behind);
            Ui.Q(g);
            Rectangle r = ClientRectangle;
            if (Bare) { Naked(g, r); return; }
            Color fill = Palette.Mix(Theme.High, Theme.Accent, _focus.Value * 0.06);
            _box.BackColor = fill;
            Ui.Fill(g, r, Radius, fill);
            Color edge = Palette.Mix(Theme.Outline, Theme.Accent, Math.Max(_hover.Value * 0.5, _focus.Value));
            Ui.Stroke(g, r, Radius, edge, _focus.Value > 0.5 ? 1.6f : 1f);
            if (ShowIcon)
            {
                int d = Theme.Px(16);
                Rectangle ir = new Rectangle(r.X + Space.Sm + Theme.Px(2), r.Y + (r.Height - d) / 2, d, d);
                Icons.Draw(g, Icon, ir, _focus.Value > 0.5 ? Theme.AccentText : Theme.Muted,
                    Math.Max(1.3f, Theme.Px(1.5)));
            }
            if (ShowTrail && _box.Text.Length > 0) Cross(g);
            _lane.Paint(g);
        }

        /// The frameless variant. A rule under the text carries the focus, and it
        /// only reaches full accent while the field is being typed in, so a page
        /// of these does not look like a page of underlines.
        private void Naked(Graphics g, Rectangle r)
        {
            _box.BackColor = Behind;
            double lit = Math.Max(_hover.Value * 0.35, _focus.Value);
            int y = r.Bottom - 1;
            using (Pen p = new Pen(Palette.Mix(Theme.Hairline, Theme.Accent, lit),
                _focus.Value > 0.5 ? Math.Max(1.4f, Theme.Px(1.6)) : 1f))
            {
                g.DrawLine(p, r.X, y, r.Right - Theme.Px(2), y);
            }
            if (ShowTrail && _box.Text.Length > 0) Cross(g);
            _lane.Paint(g);
        }

        private void Cross(Graphics g)
        {
            Rectangle t = TrailRect();
            int d = Theme.Px(16);
            if (_onTrail)
            {
                int s = d + Space.Sm;
                Ui.Fill(g, new Rectangle(t.X + (t.Width - s) / 2, t.Y + (t.Height - s) / 2, s, s),
                    Radii.Pill, Palette.Alpha(Theme.OnSurface, 0.08));
            }
            Icons.Draw(g, Trail,
                new Rectangle(t.X + (t.Width - d) / 2, t.Y + (t.Height - d) / 2, d, d),
                _onTrail ? Theme.OnSurface : Theme.Muted, Math.Max(1.3f, Theme.Px(1.5)));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Ticker.Leave(this);
            base.Dispose(disposing);
        }
    }
}
