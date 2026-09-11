// Dictation, using the recogniser Windows already has.
//
// System.Speech ships with every Windows since 7, but it is not in the framework
// folder this app compiles against: it lives only in the GAC. Hard coding that path
// into the build would tie the build to one machine's layout, so the assembly is
// loaded at run time by name and driven through reflection. The cost is this file.
// The gain is a build with five ordinary references, and an app that says "dictation
// is not available on this PC" rather than failing to start on one without it.
//
// Callbacks arrive on a worker thread and are handed to the control passed in, so no
// view has to think about threads.

using System;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed class Dictation : IDisposable
    {
        private const string Name =
            "System.Speech, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35";

        private static Assembly _asm;
        private static bool _looked;

        private readonly Control _host;
        private object _engine;
        private bool _live;

        /// A phrase, once Windows has settled on it.
        public event Action<string> Heard;

        /// A sentence to show when listening stops by itself.
        public event Action<string> Failed;

        /// The microphone level, 0 to 100, for the meter.
        public event Action<int> Level;

        public bool Live { get { return _live; } }

        public Dictation(Control host)
        {
            _host = host;
        }

        /// True when this PC can dictate at all. Both halves are needed and neither
        /// is guaranteed: the assembly has to be present, and Windows has to have a
        /// recogniser installed.
        public static bool Available
        {
            get { return Loaded() != null && Voices.Installed().Count > 0; }
        }

        private static Assembly Loaded()
        {
            if (_looked) return _asm;
            _looked = true;
            try { _asm = Assembly.Load(Name); }
            catch (Exception ex) { Paths.Log("load speech", ex); _asm = null; }
            return _asm;
        }

        /// Starts listening. Returns false with a sentence in why, because every way
        /// this fails is something the reader can act on: no recogniser for their
        /// language, no microphone, or another app holding the device.
        public bool Start(string culture, out string why)
        {
            why = "";
            if (_live) return true;
            Assembly asm = Loaded();
            if (asm == null) { why = "Dictation is not available on this PC"; return false; }
            try
            {
                Type kind = asm.GetType("System.Speech.Recognition.SpeechRecognitionEngine", true);
                Type grammar = asm.GetType("System.Speech.Recognition.DictationGrammar", true);
                Type mode = asm.GetType("System.Speech.Recognition.RecognizeMode", true);
                object engine = Make(kind, culture);
                Call(engine, "LoadGrammar", Activator.CreateInstance(grammar));
                Call(engine, "SetInputToDefaultAudioDevice");
                Hook(engine, "SpeechRecognized", "OnHeard");
                Hook(engine, "AudioLevelUpdated", "OnLevel");
                Hook(engine, "RecognizeCompleted", "OnDone");
                Call(engine, "RecognizeAsync", Enum.Parse(mode, "Multiple"));
                _engine = engine;
                _live = true;
                return true;
            }
            catch (Exception ex)
            {
                Paths.Log("dictation start", ex);
                Shut();
                why = Excuse(ex);
                return false;
            }
        }

        /// Stops listening and lets the recogniser go. Keeping it would keep the
        /// microphone claimed and its light on for no reason, so a later dictation
        /// builds a new one.
        public void Stop()
        {
            if (!_live && _engine == null) return;
            _live = false;
            Shut();
        }

        public void Dispose()
        {
            _live = false;
            Shut();
        }

        private void Shut()
        {
            object e = _engine;
            _engine = null;
            if (e == null) return;
            try { Call(e, "RecognizeAsyncCancel"); }
            catch (Exception) { }
            IDisposable d = e as IDisposable;
            if (d != null) { try { d.Dispose(); } catch (Exception) { } }
        }

        /// The recogniser for a language, or the default one when none is stored. A
        /// stored language Windows no longer has falls back rather than refusing,
        /// because the words matter more than the accent.
        private static object Make(Type kind, string culture)
        {
            string want = (culture ?? "").Trim();
            if (want.Length > 0)
            {
                try { return Activator.CreateInstance(kind, new object[] { new CultureInfo(want) }); }
                catch (Exception ex) { Paths.Log("recogniser " + want, ex); }
            }
            return Activator.CreateInstance(kind);
        }

        /// Binds one of this object's methods to an event on the recogniser. The
        /// handlers take object rather than the real argument type, which the
        /// framework allows because passing a narrower type to a wider parameter is
        /// a reference conversion, and it keeps every speech type out of this file's
        /// signatures.
        private void Hook(object engine, string ev, string handler)
        {
            EventInfo e = engine.GetType().GetEvent(ev);
            if (e == null) return;
            MethodInfo m = GetType().GetMethod(handler,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (m == null) return;
            e.AddEventHandler(engine, Delegate.CreateDelegate(e.EventHandlerType, this, m));
        }

        private void OnHeard(object sender, object args)
        {
            object result = Get(args, "Result");
            object text = result == null ? null : Get(result, "Text");
            string said = text == null ? "" : text.ToString().Trim();
            if (said.Length == 0) return;
            Post(new Action(delegate { if (Heard != null) Heard(said); }));
        }

        private void OnLevel(object sender, object args)
        {
            object got = Get(args, "AudioLevel");
            if (!(got is int)) return;
            int level = (int)got;
            Post(new Action(delegate { if (Level != null) Level(level); }));
        }

        /// Listening ended. A cancel from Stop is not worth mentioning; an error is,
        /// because the microphone being taken away mid sentence looks like the app
        /// stopped working.
        private void OnDone(object sender, object args)
        {
            Exception bad = Get(args, "Error") as Exception;
            if (bad == null) return;
            Paths.Log("dictation run", bad);
            string why = Excuse(bad);
            Post(new Action(delegate
            {
                _live = false;
                if (Failed != null) Failed(why);
            }));
        }

        private void Post(Action what)
        {
            Control c = _host;
            if (c == null || c.IsDisposed || !c.IsHandleCreated) return;
            try { c.BeginInvoke(what); }
            catch (Exception) { }
        }

        /// Finds a method by name and argument count, checking each argument fits.
        /// Looking one up by exact parameter types is no use here, because a
        /// DictationGrammar is handed to a method that takes a Grammar.
        private static object Call(object on, string name, params object[] args)
        {
            MethodInfo[] all = on.GetType().GetMethods();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Name != name) continue;
                ParameterInfo[] p = all[i].GetParameters();
                if (p.Length != args.Length) continue;
                bool fits = true;
                for (int k = 0; k < p.Length; k++)
                {
                    if (args[k] != null && !p[k].ParameterType.IsInstanceOfType(args[k])) fits = false;
                }
                if (fits) return all[i].Invoke(on, args);
            }
            throw new MissingMethodException(on.GetType().Name, name);
        }

        private static object Get(object on, string name)
        {
            try
            {
                PropertyInfo p = on.GetType().GetProperty(name);
                return p == null ? null : p.GetValue(on, null);
            }
            catch (Exception) { return null; }
        }

        /// A sentence for the ways this fails in real life. Reflection wraps
        /// everything in a TargetInvocationException, so the real one is inside.
        private static string Excuse(Exception ex)
        {
            if (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
            if (ex is InvalidOperationException)
            {
                return "No microphone was found. Plug one in, and check microphone access " +
                    "is allowed in Windows privacy settings";
            }
            if (ex is ArgumentException)
            {
                return "Windows has no recogniser for that language. Choose another one in " +
                    "Settings";
            }
            return "Dictation could not be started on this PC";
        }
    }
}
