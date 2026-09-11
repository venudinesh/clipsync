// Capture: dictation.
//
// Windows has had a speech recogniser since Vista and it runs on the machine, which
// makes it the right one for an app whose whole claim is that nothing leaves the PC.
// The phone recorded first and transcribed after; there is no reason to here, so
// words appear as they are recognised.
//
// The meter shows the real microphone level, because the one question somebody asks
// when dictation is not working is whether the machine can hear them at all. It falls
// back on a timer, since the recogniser reports a level only while there is sound.

using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClipSyncAI
{
    /// The dictation card: a button, a line of state, and the level under it.
    internal sealed class LevelCard : Card
    {
        public int Level;
        public bool Live;
        public string Note = "";
        public int Reserve;

        protected override void Paint2(Graphics g, Rectangle r)
        {
            Rectangle box = Inner;
            int x = box.X + Reserve;
            Rectangle meter = new Rectangle(x, box.Y, Math.Max(0, box.Right - x), box.Height);
            if (meter.Width < Theme.Px(60)) return;
            int lh = Theme.Small.Height + Theme.Px(4);
            int y = meter.Y + Math.Max(0, (meter.Height - lh - Space.Sm - Theme.Px(30)) / 2);
            Ui.Line(g, Note, Theme.Small, Live ? Theme.OnSurface : Theme.Muted,
                new Rectangle(meter.X, y, meter.Width, lh));
            Bars(g, new Rectangle(meter.X, y + lh + Space.Sm, meter.Width, Theme.Px(30)));
        }

        /// The level, as bars drawn from the middle out, so silence is a thin line
        /// rather than an empty box. That is the difference between "not listening"
        /// and "listening to nothing", and it is worth a person being able to see it.
        private void Bars(Graphics g, Rectangle box)
        {
            if (box.Height < Theme.Px(8)) return;
            int w = Theme.Px(4);
            int step = w + Theme.Px(3);
            int n = Math.Max(1, box.Width / step);
            int mid = box.Y + box.Height / 2;
            double lit = Live ? Math.Max(0.05, Math.Min(1.0, Level / 65.0)) : 0.0;
            Color on = Theme.Accent;
            Color off = Palette.Alpha(Theme.OnSurface, Theme.Dark ? 0.18 : 0.13);
            int floor = Math.Max(2, Theme.Px(3));
            for (int i = 0; i < n; i++)
            {
                double t = n == 1 ? 1.0 : (double)i / (n - 1);
                double shape = 0.35 + 0.65 * Math.Sin(t * Math.PI);
                int h = Math.Max(floor, (int)(box.Height * shape * lit));
                Ui.Fill(g, new Rectangle(box.X + i * step, mid - h / 2, w, h),
                    w / 2, Live ? on : off);
            }
        }
    }

    internal sealed partial class CaptureView
    {
        private readonly LevelCard _voice = new LevelCard();
        private readonly AppButton _hear = new AppButton();
        private Dictation _ears;
        private Timer _clock;
        private DateTime _began;
        private int _phrases;
        private int _level;

        private void BuildVoice()
        {
            _voice.AccessibleName = "Dictation";
            _hear.Label = "Start dictating";
            _hear.ShowIcon = true;
            _hear.Icon = Glyph.Mic;
            _hear.Click += OnHear;
            _voice.Controls.Add(_hear);
            Controls.Add(_voice);
            _clock = new Timer();
            _clock.Interval = 250;
            _clock.Tick += OnClock;
        }

        private void LayVoice()
        {
            Rectangle box = _voice.Inner;
            int w = Math.Min(Math.Max(Theme.Px(150), box.Width / 3), Theme.Px(200));
            int h = Theme.Px(38);
            _hear.SetBounds(box.X, box.Y + Math.Max(0, (box.Height - h) / 2), w, h);
            _voice.Reserve = w + Space.Lg;
            _voice.Invalidate();
        }

        private string VoiceLead()
        {
            if (!Dictation.Available)
            {
                return "Dictation needs a Windows speech recogniser, and this PC has none";
            }
            return "Speech into text, recognised by Windows on this PC" + Say.Sep +
                Voices.Pretty(Hub.Settings.SpeechCulture);
        }

        private void OnHear(object sender, EventArgs e)
        {
            Listen(_ears == null || !_ears.Live);
        }

        /// Starts or stops. The recogniser is built when it is wanted and let go on
        /// stop, so the microphone is claimed only while this page is listening.
        private void Listen(bool want)
        {
            if (!want)
            {
                if (_ears != null) _ears.Stop();
                if (_clock != null) _clock.Enabled = false;
                _level = 0;
                Ears();
                return;
            }
            if (_ears != null && _ears.Live) return;
            if (!Dictation.Available)
            {
                Hub.Oops("Dictation is not available on this PC");
                return;
            }
            if (_ears == null)
            {
                _ears = new Dictation(this);
                _ears.Heard += OnPhrase;
                _ears.Level += OnLevel;
                _ears.Failed += OnDeaf;
            }
            string why;
            if (!_ears.Start(Hub.Settings.SpeechCulture, out why))
            {
                Hub.Oops(why.Length > 0 ? why : "Dictation could not be started");
                Ears();
                return;
            }
            _began = DateTime.Now;
            _phrases = 0;
            _level = 0;
            if (_clock != null) _clock.Enabled = true;
            Ears();
            Hub.Say("Listening. Speak normally");
        }

        /// The button and the card, told which state they are in.
        private void Ears()
        {
            bool live = _ears != null && _ears.Live;
            _hear.Label = live ? "Stop" : (_got[1].Length > 0 ? "Dictate more" : "Start dictating");
            _hear.Look = live ? ButtonLook.Danger : ButtonLook.Filled;
            _hear.Icon = live ? Glyph.Stop : Glyph.Mic;
            _hear.Invalidate();
            _voice.Live = live;
            _voice.Level = _level;
            _voice.Note = live ? Ticking() : Waiting();
            _voice.Invalidate();
            Told();
        }

        private string Ticking()
        {
            int secs = (int)Math.Max(0, (DateTime.Now - _began).TotalSeconds);
            string clock = (secs / 60).ToString("00") + ":" + (secs % 60).ToString("00");
            return "Listening" + Say.Sep + clock +
                (_phrases > 0 ? Say.Sep + Say.Plural(_phrases, "phrase") : "");
        }

        private string Waiting()
        {
            if (!Dictation.Available) return "No speech recogniser is installed";
            if (_got[1].Length > 0)
            {
                return "Stopped" + Say.Sep + Say.Plural(Say.Words(_got[1]), "word");
            }
            return "Press the button and talk. Say comma or full stop for punctuation";
        }

        /// Four times a second: the clock moves and the level falls back, because the
        /// recogniser reports a level only while it can hear something.
        private void OnClock(object sender, EventArgs e)
        {
            if (_ears == null || !_ears.Live) { _clock.Enabled = false; return; }
            _level = (int)(_level * 0.6);
            Ears();
        }

        /// A phrase, joined to what is there. The recogniser hands over sentences
        /// with no trailing space, so one goes in unless the text already ends in
        /// somewhere a space would be wrong.
        private void OnPhrase(string said)
        {
            string add = (said ?? "").Trim();
            if (add.Length == 0) return;
            _phrases++;
            string was = _got[1];
            _got[1] = was.Length == 0 ? add
                : (was.EndsWith("\n") || was.EndsWith(" ") ? was + add : was + " " + add);
            if (_tab == 1)
            {
                _loading = true;
                _text.Text = Lines(_got[1]);
                _loading = false;
                Tail();
            }
            Ears();
        }

        private void OnLevel(int level)
        {
            _level = Math.Max(0, Math.Min(100, level));
            _voice.Level = _level;
            _voice.Invalidate();
        }

        private void OnDeaf(string why)
        {
            if (_clock != null) _clock.Enabled = false;
            _level = 0;
            Ears();
            Hub.Oops(why != null && why.Length > 0 ? why : "Dictation stopped");
        }

        /// Puts the caret at the end so the box follows the dictation, instead of
        /// holding still while the words pile up out of sight.
        private void Tail()
        {
            TextBox b = _text.Box;
            if (b == null || b.IsDisposed) return;
            try
            {
                b.SelectionStart = b.TextLength;
                b.SelectionLength = 0;
                b.ScrollToCaret();
            }
            catch (Exception) { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_ears != null) { _ears.Dispose(); _ears = null; }
                if (_clock != null) { _clock.Dispose(); _clock = null; }
                if (_shot != null) { _shot.Dispose(); _shot = null; }
            }
            base.Dispose(disposing);
        }
    }
}
