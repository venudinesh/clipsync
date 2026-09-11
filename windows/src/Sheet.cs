// The sheet: one modal surface for everything that needs an answer.
//
// Clip detail, delete confirmation, hotkey recording and the model picker all use
// it, so a decision only ever looks one way. It dims what is behind rather than
// opening a second window, because a tray app that scatters windows across the
// desktop is a tray app people close.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed class Sheet : Control, IAnimated
    {
        private readonly Anim _fade = new Anim(Motion.Base);
        private readonly IconButton _shut = new IconButton();
        private Control _body;
        private AppButton[] _actions = new AppButton[0];
        private Bitmap _snap;
        private Point _lift;
        private string _title = "";
        private string _subtitle = "";
        private int _bodyWant;
        private bool _open;
        private bool _keep;

        public event EventHandler Closed;

        public Sheet()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            TabStop = false;
            Visible = false;
            _shut.Icon = Glyph.Close;
            _shut.Tip = "Close";
            _shut.Click += delegate { Close(); };
            Controls.Add(_shut);
            Ticker.Join(this);
        }

        public bool IsOpen { get { return _open; } }

        /// Set right after Open when the body is a control the caller keeps and
        /// shows again later. By default a sheet disposes what it was handed
        /// once it has closed: most bodies are built for one showing, and a
        /// drawn control that is dropped without being disposed stays on the
        /// ticker being asked for frames for the rest of the session.
        public bool KeepBody { set { _keep = value; } }

        /// Puts a sheet up. The body is any control; the sheet positions it and
        /// hands it back on close so a caller can reuse or dispose it.
        public void Open(string title, string subtitle, Control body, int bodyHeight,
            params AppButton[] actions)
        {
            Shed();
            // Everything parented in here sits on the frame, and the frame is
            // painted in the card surface. Drawn controls clear to their
            // parent's colour to fake their rounded corners, so this is what
            // makes a body land on the right shade without being told.
            BackColor = Theme.Base;
            _title = title ?? "";
            _subtitle = subtitle ?? "";
            _body = body;
            _bodyWant = bodyHeight;
            _actions = actions ?? new AppButton[0];
            if (_body != null) Controls.Add(_body);
            for (int i = 0; i < _actions.Length; i++) Controls.Add(_actions[i]);
            _open = true;
            Snapshot();
            Visible = true;
            BringToFront();
            Lay();
            _fade.Set(0);
            _fade.To(1);
            Ticker.Poke();
            if (_actions.Length > 0) _actions[_actions.Length - 1].Select();
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            _fade.To(0);
            Ticker.Poke();
            if (Theme.ReduceMotion) Settle();
            if (Closed != null) Closed(this, EventArgs.Empty);
        }

        private void Shed()
        {
            Control body = _body;
            AppButton[] acts = _actions;
            if (body != null) { Controls.Remove(body); _body = null; }
            for (int i = 0; i < acts.Length; i++) Controls.Remove(acts[i]);
            _actions = new AppButton[0];
            if (_keep) { _keep = false; return; }
            Bin(body, acts);
        }

        /// Throws away a body the caller does not keep, on the next turn of the
        /// message pump rather than here: shedding can run from inside a verb
        /// row's own click, and disposing a control while its event is still
        /// unwinding is asking for trouble.
        private void Bin(Control body, AppButton[] acts)
        {
            if (body == null && acts.Length == 0) return;
            Action drop = delegate
            {
                if (body != null) body.Dispose();
                for (int i = 0; i < acts.Length; i++) acts[i].Dispose();
            };
            if (!IsHandleCreated) { drop(); return; }
            try { BeginInvoke(drop); }
            catch (Exception ex) { Paths.Log("sheet bin", ex); }
        }

        private void Settle()
        {
            Visible = false;
            Shed();
            Drop();
        }

        /// A still of the page underneath, taken the moment the sheet opens. A
        /// child control cannot really be transparent over its siblings, and a
        /// modal that hides the page instead of dimming it loses the reason the
        /// page was there. The page behind a sheet is not animating, so one
        /// snapshot is as good as live compositing and costs nothing per frame.
        private void Snapshot()
        {
            Drop();
            Control p = Parent;
            if (p == null || p.Width < 4 || p.Height < 4) return;
            try
            {
                Bitmap b = new Bitmap(p.Width, p.Height);
                p.DrawToBitmap(b, new Rectangle(0, 0, p.Width, p.Height));
                _snap = b;
                _lift = Lift(p);
            }
            catch (Exception ex)
            {
                Paths.Log("sheet backdrop", ex);
            }
        }

        /// Where the page starts inside that still. A window asked to draw itself
        /// draws its frame as well, so the picture opens with a title bar and a
        /// border and the page begins that far in. Drawn without the offset, the
        /// still sat a caption low and a border right of where it belongs, and the
        /// sheet looked like it had dimmed a second copy of the window with its own
        /// title bar showing.
        private static Point Lift(Control p)
        {
            Form host = p as Form;
            if (host == null) return Point.Empty;
            Point client = host.PointToScreen(Point.Empty);
            return new Point(client.X - host.Left, client.Y - host.Top);
        }

        private void Drop()
        {
            _lift = Point.Empty;
            if (_snap == null) return;
            _snap.Dispose();
            _snap = null;
        }

        private int HeadHeight { get { return Theme.Px(54); } }
        private int FootHeight { get { return _actions.Length > 0 ? Theme.Px(60) : Space.Lg; } }

        private Rectangle Frame()
        {
            int want = HeadHeight + _bodyWant + FootHeight + Space.Md;
            int w = Math.Min(Math.Max(Theme.Px(280), Width - Space.Xl * 2), Theme.Px(560));
            int h = Math.Min(Math.Max(Theme.Px(140), want), Math.Max(Theme.Px(140), Height - Space.Xl * 2));
            int lift = (int)Math.Round((1 - _fade.Value) * Theme.Px(14));
            return new Rectangle((Width - w) / 2, (Height - h) / 2 + lift, w, h);
        }

        public void Lay()
        {
            Rectangle f = Frame();
            int d = Theme.Px(32);
            _shut.SetBounds(f.Right - d - Space.Md, f.Y + Space.Md, d, d);
            if (_body != null)
            {
                _body.SetBounds(f.X + Space.Xl, f.Y + HeadHeight,
                    Math.Max(10, f.Width - Space.Xl * 2),
                    Math.Max(10, f.Height - HeadHeight - FootHeight));
            }
            int x = f.Right - Space.Xl;
            int y = f.Bottom - FootHeight + (FootHeight - Theme.Px(36)) / 2;
            for (int i = _actions.Length - 1; i >= 0; i--)
            {
                int w = Math.Max(Theme.Px(88), _actions[i].Wants());
                x -= w;
                _actions[i].SetBounds(x, y, w, Theme.Px(36));
                x -= Space.Sm;
            }
        }

        public bool Tick()
        {
            bool busy = _fade.Advance();
            if (busy) { Lay(); Invalidate(); }
            if (!busy && !_open && Visible && _fade.Value <= 0.01) Settle();
            return busy;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (!Frame().Contains(e.Location)) Close();
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Ui.Q(g);
            double v = _fade.Value;
            Rectangle f = Frame();
            if (_snap != null) g.DrawImageUnscaled(_snap, -Left - _lift.X, -Top - _lift.Y);
            else g.Clear(Theme.Canvas);
            using (SolidBrush scrim = new SolidBrush(Color.FromArgb((int)(150 * v), 0, 0, 0)))
            {
                g.FillRectangle(scrim, ClientRectangle);
            }
            Ui.Shadow(g, f, Radii.Shell, Theme.Px(10), Color.Black, Theme.Dark ? 170 : 90);
            Ui.Fill(g, f, Radii.Shell, Theme.Base);
            Ui.Stroke(g, f, Radii.Shell, Theme.Outline, 1f);
            Rectangle tr = new Rectangle(f.X + Space.Xl, f.Y + Space.Md,
                f.Width - Space.Xl - Theme.Px(52), Theme.Px(26));
            Ui.Line(g, _title, Theme.Title, Theme.OnSurface, tr);
            if (_subtitle.Length > 0)
            {
                Ui.Line(g, _subtitle, Theme.Small, Theme.Muted,
                    new Rectangle(tr.X, tr.Bottom, tr.Width, Theme.Px(18)));
            }
            if (_actions.Length > 0)
            {
                using (Pen p = new Pen(Theme.Hairline))
                {
                    int y = f.Bottom - FootHeight;
                    g.DrawLine(p, f.X + Space.Md, y, f.Right - Space.Md, y);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { Ticker.Leave(this); Drop(); }
            base.Dispose(disposing);
        }
    }
}
