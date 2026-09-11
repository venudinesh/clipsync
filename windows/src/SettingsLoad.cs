// Settings: reading the state back onto the page.
//
// Every control is preset from the settings file here, inside a flag that stops the
// change handlers from firing. Without it, loading the page would write everything
// back to disk and, worse, a Field raises its edited event when its text is set, so
// simply showing the server address would go looking for a server.
//
// The three refresh routines are separate on purpose. Load is the whole page from
// disk. Told is the masthead. Fresh is the handful of labels that depend on which
// model server answered, and that arrives on a worker thread long after the page
// was built.

using System;

namespace ClipSyncAI
{
    internal sealed partial class SettingsView
    {
        private void Load()
        {
            _loading = true;
            AppSettings s = Hub.Settings;

            _theme.Index = s.FollowSystemTheme ? 0 : (s.Dark ? 2 : 1);
            _amoled.Preset(s.Amoled);
            _accent.Preset(s.Accent);
            _dense.Preset(s.Dense);
            _motion.Preset(s.ReduceMotion);

            _watch.Preset(s.CaptureEnabled);
            _auto.Preset(s.AutoProcess);
            _safe.Preset(s.SkipSensitive);
            _keep.Index = Near(Keeps, s.MaxHistory);
            _floor.Index = Near(Floors, s.MinLength);
            _wait.Index = Near(Waits, s.DebounceMs);

            _kind.Index = s.Engine == EngineKind.Ollama ? 1
                : (s.Engine == EngineKind.OpenAiCompatible ? 2
                : (s.Engine == EngineKind.Local ? 3
                : (s.Engine == EngineKind.Cloud ? 4 : 0)));
            _find.Preset(s.AutoDiscoverEngine);
            _url.Text = Address();
            _cloud.Index = CloudProviderIndex(s.CloudProvider);
            _key.Text = CloudKeys.Load();
            _baseUrl.Text = s.CloudBaseUrl ?? "";

            _login.Preset(Logs());
            _small.Preset(s.StartMinimised);
            _tray.Preset(s.CloseToTray);

            _tess.Text = s.TesseractPath ?? "";

            _loading = false;
            Fresh();
        }

        /// The stored number, mapped to the nearest option offered. A settings file
        /// written by hand, or by a later build with a different set, still lands on
        /// something sensible instead of the first entry.
        private static int Near(int[] set, int value)
        {
            int best = 0;
            int gap = int.MaxValue;
            for (int i = 0; i < set.Length; i++)
            {
                int d = Math.Abs(set[i] - value);
                if (d >= gap) continue;
                gap = d;
                best = i;
            }
            return best;
        }

        /// Greys out what cannot apply. A row is dimmed rather than hidden so the
        /// page keeps its shape and the reason a thing is unavailable stays on
        /// screen next to the switch that would enable it. Rows for engines that
        /// are not selected hide entirely, because a row for "provider" means
        /// nothing while a local server is answering.
        private void Moots()
        {
            AppSettings s = Hub.Settings;
            bool local = s.Engine == EngineKind.Local;
            bool cloud = s.Engine == EngineKind.Cloud;
            bool server = s.Engine == EngineKind.Ollama || s.Engine == EngineKind.OpenAiCompatible;
            bool live = s.Engine != EngineKind.Offline;

            _amoledRow.Moot = !s.Dark;
            _autoRow.Moot = !s.CaptureEnabled;

            _findRow.Visible = server;
            _urlRow.Visible = server;
            _testRow.Visible = server || cloud;
            _modelRow.Visible = live;
            _visionRow.Visible = live && !local;
            _cloudRow.Visible = cloud;
            _keyRow.Visible = cloud;
            _baseUrlRow.Visible = cloud && s.CloudProvider == CloudProviderKind.Custom;
            _loadRow.Visible = local;
            // Getting a local GGUF onto this PC is always allowed, whatever engine
            // is answering. Importing or downloading only copies a file; the local
            // engine is the one that reads it, so nothing else is disturbed.
            _downloadRow.Visible = true;
            _importRow.Visible = true;

            _urlRow.Moot = !live || s.AutoDiscoverEngine;
            _testRow.Moot = !live;
            _modelRow.Moot = !live;
            _visionRow.Moot = !live || (s.TesseractPath ?? "").Trim().Length > 0;
            _loadRow.Moot = !live || (s.Model ?? "").Trim().Length == 0;
        }

        /// The masthead. It says which engine is answering and where, because that
        /// is the one fact on this page a person cannot check by looking at a
        /// switch, and the answer changes without anything being pressed.
        private void Told()
        {
            EngineStatus st = Hub.Status;
            AppSettings s = Hub.Settings;
            if (s.Engine == EngineKind.Offline)
            {
                _head.Subtitle = "Formatting runs on this PC with no model";
            }
            else if (s.Engine == EngineKind.Local)
            {
                LocalEngine local = Hub.Brain.Engine as LocalEngine;
                if (st.Available)
                {
                    string model = Hub.Brain.Model;
                    if (local != null && local.HasModel)
                    {
                        _head.Subtitle = "On this PC, " + local.LoadedName + " loaded";
                    }
                    else if (string.IsNullOrEmpty(model) ||
                             (local != null && local.Models().Count == 0))
                    {
                        _head.Subtitle = "On this PC, no model yet — download or import one below";
                    }
                    else
                    {
                        _head.Subtitle = "On this PC, using " + model;
                    }
                }
                else
                {
                    _head.Subtitle = "No embedded model yet. " + (st.Detail ?? "");
                }
            }
            else if (s.Engine == EngineKind.Cloud)
            {
                if (st.Available)
                {
                    string model = Hub.Brain.Model;
                    _head.Subtitle = "Cloud AI · " + st.Kind +
                        (string.IsNullOrEmpty(model) ? "" : ", using " + model);
                }
                else
                {
                    _head.Subtitle = "Cloud AI is not answering. " + (st.Detail ?? "");
                }
            }
            else if (st.Available)
            {
                string model = Hub.Brain.Model;
                _head.Subtitle = st.Kind + " at " + st.BaseUrl +
                    (string.IsNullOrEmpty(model) ? "" : ", using " + model);
            }
            else
            {
                _head.Subtitle = "No model server is answering. " + (st.Detail ?? "");
            }
            _head.Invalidate();
        }

        /// The labels that follow discovery. The search runs on a worker thread and
        /// lands whenever it lands, so nothing here may assume the page was just
        /// built or that the buttons still say what they said.
        private void Fresh()
        {
            _test.Busy = false;
            AppSettings s = Hub.Settings;
            bool cloud = s.Engine == EngineKind.Cloud;
            bool local = s.Engine == EngineKind.Local;
            _test.Label = cloud ? "Test connection" : "Look again";
            _testRow.Detail = cloud
                ? "Checks the key against " + CloudEngine.ProviderName(s.CloudProvider)
                : "Checks the usual ports for Ollama, LM Studio, llama.cpp and Jan";
            _model.Label = Short(Hub.Brain.Model, "Choose");
            _vision.Label = Short(Hub.Settings.VisionModel, "None");
            LocalEngine engine = Hub.Brain.Engine as LocalEngine;
            _load.Label = local && engine != null && engine.HasModel ? "Unload" : "Load now";
            _voice.Label = Voices.Pretty(Hub.Settings.SpeechCulture);
            _hotShow.Label = Shortcut(Hub.Settings.HotkeyOverlay);
            _hotDo.Label = Shortcut(Hub.Settings.HotkeyProcess);
            if (_whereRow != null) _whereRow.Detail = Where();
            Moots();
        }

        /// A model name with the registry prefix dropped. A button is not wide
        /// enough for registry.ollama.ai/library/qwen2.5:7b and the part that
        /// identifies it is at the end.
        private static string Short(string name, string fallback)
        {
            string s = (name ?? "").Trim();
            if (s.Length == 0) return fallback;
            int cut = s.LastIndexOf('/');
            if (cut >= 0 && cut < s.Length - 1) s = s.Substring(cut + 1);
            return s.Length > 26 ? s.Substring(0, 25) + char.ConvertFromUtf32(0x2026) : s;
        }

        private static string Shortcut(string text)
        {
            string s = (text ?? "").Trim();
            return s.Length == 0 ? "Off" : s;
        }

        private static string Where()
        {
            return Paths.Root + Say.Sep + (Paths.Portable
                ? "portable, so it travels with the app"
                : "encrypted, and readable only by your Windows account");
        }
    }
}
