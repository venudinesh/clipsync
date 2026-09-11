// The hub: the one object every view is handed.
//
// Settings, the three stores, the inference wrapper and the two shared surfaces
// live here, so a view never reaches for a global and never owns state another
// view also needs. Anything that changes shared state raises an event rather than
// poking at other views directly.

using System;
using System.Threading;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed class Hub
    {
        public AppSettings Settings = new AppSettings();
        public readonly Store<ClipEntry> Clips;
        public readonly Store<Note> Notes;
        public readonly Store<ChatSession> Chats;
        public readonly Ai Brain = new Ai();
        public EngineStatus Status = new EngineStatus();

        public Toast Toast;
        public Sheet Sheet;

        /// The control whose thread owns the UI. Background work posts back
        /// through it, which is the only safe way to touch a WinForms control
        /// from a worker.
        public Control Pump;

        public event Action ClipsChanged;
        public event Action NotesChanged;
        public event Action StatusChanged;

        /// Raised when a setting changes the look of the app. The shell listens,
        /// re-adopts the theme and re-dresses every view, so a view changing a
        /// colour does not have to know what is above it.
        public event Action ThemeChanged;

        /// Raised when a shortcut has been rewritten. The shell owns the window
        /// handle the keys are bound to, so it is the only thing that can drop
        /// the old registration and take the new one.
        public event Action HotkeysChanged;

        /// Set while the app itself writes to the clipboard, so the watcher does
        /// not capture what the app just pasted out.
        public bool SelfCopy;

        public Hub()
        {
            Clips = new Store<ClipEntry>(AppDefaults.StoreClips,
                delegate(ClipEntry c) { return c.ToJson(); }, ClipEntry.FromJson);
            Notes = new Store<Note>(AppDefaults.StoreNotes,
                delegate(Note n) { return n.ToJson(); }, Note.FromJson);
            Chats = new Store<ChatSession>(AppDefaults.StoreChats,
                delegate(ChatSession s) { return s.ToJson(); }, ChatSession.FromJson);
        }

        public void Boot()
        {
            Paths.EnsureRoot();
            Settings = SettingsStore.Load();
            if (Settings.FollowSystemTheme) Settings.Dark = Theme.SystemPrefersDark();
            Theme.Adopt(Settings);
            Theme.BuildFonts();
            Clips.Load();
            Notes.Load();
            Chats.Load();
            Brain.Model = Settings.Model;
            int burned = ClipSmart.PurgeExpired(Clips, Settings.RetentionDays);
            if (burned > 0) Say("Cleared " +
                global::ClipSyncAI.Say.Plural(burned, "expired clip"));
        }

        public void SaveSettings()
        {
            Settings.Model = Brain.Model;
            SettingsStore.Save(Settings);
        }

        public void Say(string text)
        {
            if (Toast != null) Toast.Good(text);
        }

        public void Oops(string text)
        {
            if (Toast != null) Toast.Bad(text);
        }

        /// Says something happened and offers to put it back. The action is also
        /// parked here so Ctrl+Z can reach it: a word painted in a toast is no
        /// help to somebody working from the keyboard, and the toast leaves.
        public void Undoable(string text, Action undo)
        {
            _undo = undo;
            if (Toast == null) return;
            Toast.Offer(text, "Undo", new Action(delegate { Undo(); }));
        }

        /// Takes the offer, if one is open. Returns false when there was nothing
        /// to undo, so a shortcut handler knows whether it swallowed the key.
        public bool Undo()
        {
            Action undo = _undo;
            _undo = null;
            if (undo == null) return false;
            undo();
            return true;
        }

        private Action _undo;

        /// Writes to the clipboard without the watcher treating it as a new clip.
        /// The retrying overload is used because another process can hold the
        /// clipboard open for a moment and the first attempt then throws.
        public bool PutClipboard(string text)
        {
            if (text == null) text = "";
            try
            {
                SelfCopy = true;
                if (text.Length == 0) Clipboard.Clear();
                else Clipboard.SetDataObject(text, true, 4, 90);
                return true;
            }
            catch (Exception ex)
            {
                Paths.Log("clipboard write", ex);
                SelfCopy = false;
                return false;
            }
        }

        public string TakeClipboard()
        {
            try
            {
                if (Clipboard.ContainsText()) return Clipboard.GetText() ?? "";
            }
            catch (Exception ex)
            {
                Paths.Log("clipboard read", ex);
            }
            return "";
        }

        public void RaiseClips()
        {
            if (ClipsChanged != null) ClipsChanged();
        }

        public void RaiseNotes()
        {
            if (NotesChanged != null) NotesChanged();
        }

        public void RaiseTheme()
        {
            if (ThemeChanged != null) ThemeChanged();
        }

        public void RaiseHotkeys()
        {
            if (HotkeysChanged != null) HotkeysChanged();
        }

        /// Looks for a local model server on a worker thread, then adopts it on
        /// the UI thread. Called at startup and whenever engine settings change.
        public void Rediscover()
        {
            Thread t = new Thread(new ThreadStart(DiscoverWork));
            t.IsBackground = true;
            t.Name = "engine-discovery";
            t.Start();
        }

        private void DiscoverWork()
        {
            InferenceEngine found = null;
            string fail = null;
            try { found = EngineFactory.Discover(Settings, out fail); }
            catch (Exception ex) { Paths.Log("engine discovery", ex); fail = ex.Message; }
            EngineStatus status = EngineFactory.StatusFor(found);
            // When the configured engine is the embedded or cloud one, the
            // reason it did not answer (no model yet, no key, offline) is the
            // story the Settings masthead should tell.
            if (found == null && !string.IsNullOrEmpty(fail) &&
                (Settings.Engine == EngineKind.Local || Settings.Engine == EngineKind.Cloud))
            {
                status.Kind = Settings.Engine == EngineKind.Local ? "On this PC" : "Cloud AI";
                status.Detail = fail;
            }
            Land(found, status);
        }

        private void Land(InferenceEngine engine, EngineStatus status)
        {
            Control pump = Pump;
            if (pump == null || !pump.IsHandleCreated) { Adopt(engine, status); return; }
            try
            {
                pump.BeginInvoke(new Action(delegate { Adopt(engine, status); }));
            }
            catch (Exception) { }
        }

        private void Adopt(InferenceEngine engine, EngineStatus status)
        {
            Brain.Engine = engine;
            Status = status;
            Brain.Model = Ai.ChooseModel(status.Models, Settings.Model);
            if (StatusChanged != null) StatusChanged();
        }
    }
}
