// Animation.
//
// One timer drives every moving thing in the app, and it only runs while
// something is actually moving, so an idle window costs nothing. Each control
// owns however many Anim values it needs and reports whether it still wants
// frames; when nobody does, the timer stops until the next hover.
//
// Reduce motion is honoured at the source: an Anim asked to move jumps straight
// to its target instead, so no control needs to check the setting.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal interface IAnimated
    {
        /// Advances this control's animations. True while it still needs frames.
        bool Tick();
    }

    internal sealed class Anim
    {
        private static readonly Stopwatch Watch = Stopwatch.StartNew();

        private readonly int _ms;
        private readonly Func<double, double> _ease;
        private double _from, _to, _cur;
        private double _start;
        private bool _running;

        public Anim(int ms) : this(ms, null) { }

        public Anim(int ms, Func<double, double> ease)
        {
            _ms = ms < 1 ? 1 : ms;
            _ease = ease;
        }

        public double Value { get { return _cur; } }
        public double Target { get { return _to; } }
        public bool Running { get { return _running; } }

        public void Set(double v)
        {
            _cur = v;
            _to = v;
            _from = v;
            _running = false;
        }

        public void To(double target)
        {
            if (_to == target && (_running || _cur == target)) return;
            if (Theme.ReduceMotion) { Set(target); return; }
            _from = _cur;
            _to = target;
            _start = Watch.Elapsed.TotalMilliseconds;
            _running = true;
            Ticker.Poke();
        }

        /// Moves the value on and says whether it changed, which is the question
        /// every caller is really asking: they repaint when it did.
        ///
        /// The frame that finishes an animation counts as a change. It has to: a
        /// caller only paints when told to, and if the finishing frame said
        /// nothing the last thing drawn would be wherever the value had reached
        /// the frame before. Usually that is a pixel short and invisible, but a
        /// page being built for the first time can hold the message loop past
        /// the whole duration, and then the first frame is also the last, the
        /// value snaps to its target unseen, and whatever moved is left drawn
        /// where it started. That is how the rail came to show two current
        /// destinations at once.
        public bool Advance()
        {
            if (!_running) return false;
            double t = (Watch.Elapsed.TotalMilliseconds - _start) / _ms;
            if (t >= 1.0)
            {
                _cur = _to;
                _running = false;
                return true;
            }
            if (t < 0) t = 0;
            double k = _ease != null ? _ease(t) : Ease.Glass(t);
            _cur = _from + (_to - _from) * k;
            return true;
        }
    }

    internal static class Ticker
    {
        private static readonly List<IAnimated> Items = new List<IAnimated>();
        private static Timer _timer;
        private static int _idleFrames;

        /// The timer interval that matches the display's refresh rate, so no
        /// painted frame is ever thrown away or skipped by the monitor. A rate
        /// that the API cannot see resolves to 60 Hz. Capped between ~30 and 144
        /// Hz so a software animation never spends itself on a wall of timers.
        public static int FrameInterval()
        {
            try
            {
                int rate = Native.DisplayFrequency();
                if (rate >= 45 && rate <= 360)
                {
                    int ms = (int)Math.Round(1000.0 / rate);
                    if (ms < 6) return 6;
                    if (ms > 33) return 33;
                    return ms;
                }
            }
            catch (Exception) { }
            return 16;
        }

        public static void Join(IAnimated a)
        {
            if (a == null || Items.Contains(a)) return;
            Items.Add(a);
        }

        public static void Leave(IAnimated a)
        {
            Items.Remove(a);
        }

        /// Called whenever an animation starts. Starting the timer here rather
        /// than on a schedule is what keeps an idle app at zero wake ups.
        public static void Poke()
        {
            _idleFrames = 0;
            if (_timer != null) { if (!_timer.Enabled) _timer.Start(); return; }
            _timer = new Timer();
            _timer.Interval = FrameInterval();
            _timer.Tick += OnTick;
            _timer.Start();
        }

        private static void OnTick(object sender, EventArgs e)
        {
            bool busy = false;
            for (int i = Items.Count - 1; i >= 0; i--)
            {
                IAnimated a = Items[i];
                try
                {
                    if (a.Tick()) busy = true;
                }
                catch (Exception ex)
                {
                    Paths.Log("animation tick failed", ex);
                    Items.RemoveAt(i);
                }
            }
            if (busy) { _idleFrames = 0; return; }
            // A short grace period, so a hover that ends and restarts does not
            // stop and start the timer twice a second.
            _idleFrames++;
            if (_idleFrames > 30 && _timer != null) _timer.Stop();
        }
    }
}
