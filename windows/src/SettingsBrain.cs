// Settings: the model.
//
// Five ways to answer: the rule based formatter on this PC, a local Ollama or
// OpenAI compatible server, a model run by the bundled llama.cpp inside this
// process, or — the one opt-in exception to the never-leaves-the-machine
// promise — a cloud provider behind an API key. The page says which one is
// answering rather than leaving it to be guessed, and the rows for the engines
// that do not apply hide rather than stare back empty.

using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal sealed partial class SettingsView
    {
        private Group Brain()
        {
            _kind.SetOptions(new string[] { "Built in", "Ollama", "OpenAI server", "On this PC", "Cloud AI" }, 0);
            _kind.Changed += OnKind;
            _find.Changed += OnFind;

            _url.Placeholder = "http://127.0.0.1:11434";
            _url.Submitted += OnUrl;
            _url.Box.Leave += OnUrl;
            _url.Height = Theme.Px(34);

            _cloud.SetOptions(new string[] { "OpenAI", "Anthropic", "Gemini", "Groq", "Mistral", "xAI", "OpenRouter", "Custom" }, 0);
            _cloud.Changed += OnCloud;

            _key.Placeholder = "sk-…";
            _key.Password = true;
            _key.Submitted += OnKey;
            _key.Box.Leave += OnKey;
            _key.Height = Theme.Px(34);

            _baseUrl.Placeholder = "https://api.example.com/v1";
            _baseUrl.Submitted += OnBaseUrl;
            _baseUrl.Box.Leave += OnBaseUrl;
            _baseUrl.Height = Theme.Px(34);

            _model.Look = ButtonLook.Outline;
            _model.ShowIcon = true;
            _model.Icon = Glyph.Sparkle;
            _model.Label = "Choose";
            _model.Click += OnModel;

            _vision.Look = ButtonLook.Outline;
            _vision.ShowIcon = true;
            _vision.Icon = Glyph.Capture;
            _vision.Label = "Choose";
            _vision.Click += OnVision;

            _test.Look = ButtonLook.Soft;
            _test.ShowIcon = true;
            _test.Icon = Glyph.Refresh;
            _test.Label = "Look again";
            _test.Click += OnTest;

            _download.Look = ButtonLook.Soft;
            _download.ShowIcon = true;
            _download.Icon = Glyph.Plus;
            _download.Label = "Download a model";
            _download.Click += OnDownload;

            _import.Look = ButtonLook.Soft;
            _import.ShowIcon = true;
            _import.Icon = Glyph.Link;
            _import.Label = "Import a GGUF file";
            _import.Click += OnImport;

            _load.Look = ButtonLook.Soft;
            _load.ShowIcon = true;
            _load.Icon = Glyph.Refresh;
            _load.Label = "Load now";
            _load.Click += OnLoad;

            _findRow = Row("Find it for me", "Looks for a server on the usual ports instead of " +
                "using the address below", _find, 0);
            _urlRow = Under("Server address", "Where the model server is listening. Change it if " +
                "you moved it off the usual port", _url);
            _modelRow = Row("Model", "Used for tidying clips, notes and everything you ask in " +
                "Chat", _model, Theme.Px(190));
            _visionRow = Row("Model for pictures", "Only needed to read text out of an image on " +
                "the Capture page", _vision, Theme.Px(190));
            _testRow = Row("Find a local server", "Checks the usual ports for Ollama, LM Studio, " +
                "llama.cpp and Jan", _test, Theme.Px(140));
            _cloudRow = Row("Provider", "Which AI service the key below belongs to", _cloud,
                Theme.Px(560));
            _keyRow = Under("API key", "Sent with every cloud request. Stored encrypted, and " +
                "never written into an export", _key);
            _baseUrlRow = Under("Endpoint", "A server that speaks the OpenAI chat contract, " +
                "such as a local gateway", _baseUrl);
            _downloadRow = Row("Add a model", "Fetches a small GGUF model from Hugging Face, " +
                "then it runs on this PC", _download, Theme.Px(190));
            _importRow = Row("Bring your own", "Copies a GGUF file you already have into the " +
                "models folder", _import, Theme.Px(190));
            _loadRow = Row("Loaded model", "The model is loaded on first use; this button does " +
                "it now or frees it again", _load, Theme.Px(190));

            Group g = new Group("Model");
            g.Add(Row("Where it runs", "Built in means no model at all: the formatter is a set " +
                "of rules on this PC, and it is quick", _kind, Theme.Px(340)));
            g.Add(_findRow);
            g.Add(_urlRow);
            g.Add(_testRow);
            g.Add(_modelRow);
            g.Add(_visionRow);
            g.Add(_cloudRow);
            g.Add(_keyRow);
            g.Add(_baseUrlRow);
            g.Add(_downloadRow);
            g.Add(_importRow);
            g.Add(_loadRow);
            return g;
        }

        private void OnKind(object sender, EventArgs e)
        {
            if (_loading) return;
            int i = _kind.Index;
            Hub.Settings.Engine = i == 1 ? EngineKind.Ollama
                : (i == 2 ? EngineKind.OpenAiCompatible
                : (i == 3 ? EngineKind.Local
                : (i == 4 ? EngineKind.Cloud : EngineKind.Offline)));
            Hub.SaveSettings();
            _loading = true;
            _url.Text = Address();
            _loading = false;
            Moots();
            Lay();
            if (Hub.Settings.Engine == EngineKind.Offline)
            {
                Hub.Brain.Engine = null;
                Hub.Status = new EngineStatus();
                Told();
                Fresh();
                return;
            }
            if (Hub.Settings.Engine == EngineKind.Cloud && !Hub.Settings.CloudApproved)
            {
                AskCloud();
            }
            else
            {
                Hunt();
            }
        }

        /// One-time notice before anything can leave this PC. The engine itself
        /// refuses to answer until this is granted, so skipping the sheet just
        /// leaves the cloud rows sitting there unapproved.
        private void AskCloud()
        {
            if (Hub.Sheet == null) return;
            string provider = CloudEngine.ProviderName(Hub.Settings.CloudProvider);
            Verbs body = new Verbs();
            body.Add(Glyph.Check, "Allow " + provider,
                "Prompts — including clipboard text you paste into Chat — will be sent to " +
                provider + ". Your API key is billed by the provider.", false,
                new EventHandler(CloudAllowed));
            body.Add(Glyph.Close, "Keep it local",
                "Stays on this PC; Cloud AI stays off", false,
                new EventHandler(CloudRefused));
            Hub.Sheet.Open("Send text to the cloud?",
                "ClipSyncAI's promise is that nothing leaves this PC.",
                body, body.Wants());
        }

        private void CloudAllowed(object sender, EventArgs e)
        {
            Hub.Sheet.Close();
            Hub.Settings.CloudApproved = true;
            Hub.SaveSettings();
            Hunt();
        }

        private void CloudRefused(object sender, EventArgs e)
        {
            Hub.Sheet.Close();
            _loading = true;
            _kind.Index = 0;
            _loading = false;
            Hub.Settings.Engine = EngineKind.Offline;
            Hub.SaveSettings();
            Moots();
            Lay();
            Hub.Brain.Engine = null;
            Hub.Status = new EngineStatus();
            Told();
            Fresh();
        }

        private void OnCloud(object sender, EventArgs e)
        {
            if (_loading) return;
            Hub.Settings.CloudProvider = CloudProviderKindAt(_cloud.Index);
            Hub.SaveSettings();
            Moots();
            Lay();
            if (Hub.Settings.Engine == EngineKind.Cloud) Hunt();
        }

        private static CloudProviderKind CloudProviderKindAt(int i)
        {
            switch (i)
            {
                case 1: return CloudProviderKind.Anthropic;
                case 2: return CloudProviderKind.Gemini;
                case 3: return CloudProviderKind.Groq;
                case 4: return CloudProviderKind.Mistral;
                case 5: return CloudProviderKind.Xai;
                case 6: return CloudProviderKind.OpenRouter;
                case 7: return CloudProviderKind.Custom;
                default: return CloudProviderKind.OpenAi;
            }
        }

        private static int CloudProviderIndex(CloudProviderKind p)
        {
            switch (p)
            {
                case CloudProviderKind.Anthropic: return 1;
                case CloudProviderKind.Gemini: return 2;
                case CloudProviderKind.Groq: return 3;
                case CloudProviderKind.Mistral: return 4;
                case CloudProviderKind.Xai: return 5;
                case CloudProviderKind.OpenRouter: return 6;
                case CloudProviderKind.Custom: return 7;
                default: return 0;
            }
        }

        private void OnKey(object sender, EventArgs e)
        {
            if (_loading) return;
            string want = _key.Text.Trim();
            if (want == CloudKeys.Load()) return;
            CloudKeys.Save(want);
            Hub.Say(want.Length == 0 ? "API key removed" : "API key saved");
            if (Hub.Settings.Engine == EngineKind.Cloud) Hunt();
        }

        private void OnBaseUrl(object sender, EventArgs e)
        {
            if (_loading) return;
            string want = _baseUrl.Text.Trim();
            if (want.Length == 0) return;
            if (want.IndexOf("://", StringComparison.Ordinal) < 0) want = "https://" + want;
            want = want.TrimEnd('/');
            if (want == Hub.Settings.CloudBaseUrl) return;
            Hub.Settings.CloudBaseUrl = want;
            Hub.SaveSettings();
            Hunt();
        }

        private void OnFind(object sender, EventArgs e)
        {
            if (_loading) return;
            Hub.Settings.AutoDiscoverEngine = _find.On;
            Hub.SaveSettings();
            Moots();
            if (Hub.Settings.Engine != EngineKind.Offline) Hunt();
        }

        /// The address, committed when the box is left or Enter is pressed rather
        /// than on every keystroke, because each commit goes looking for a server.
        private void OnUrl(object sender, EventArgs e)
        {
            if (_loading) return;
            string want = _url.Text.Trim();
            if (want.Length == 0) return;
            if (want.IndexOf("://", StringComparison.Ordinal) < 0) want = "http://" + want;
            want = want.TrimEnd('/');
            if (want == Address()) return;
            if (Hub.Settings.Engine == EngineKind.Ollama) Hub.Settings.OllamaUrl = want;
            else if (Hub.Settings.Engine == EngineKind.OpenAiCompatible) Hub.Settings.OpenAiUrl = want;
            else return;
            Hub.SaveSettings();
            _loading = true;
            _url.Text = want;
            _loading = false;
            Hunt();
        }

        private string Address()
        {
            if (Hub.Settings.Engine == EngineKind.OpenAiCompatible) return Hub.Settings.OpenAiUrl ?? "";
            if (Hub.Settings.Engine == EngineKind.Ollama) return Hub.Settings.OllamaUrl ?? "";
            return "";
        }

        private void OnTest(object sender, EventArgs e)
        {
            Hunt();
        }

        /// Goes looking on a worker thread. The button spins until the hub raises
        /// its status, which is the one signal that the search is over.
        private void Hunt()
        {
            _test.Busy = true;
            Hub.Say("Looking for a model to answer");
            Hub.Rediscover();
        }

        private void OnModel(object sender, EventArgs e)
        {
            List<string> got = Hub.Status.Models;
            if (got == null || got.Count == 0)
            {
                Hub.Oops("Nothing is answering, so there is nothing to choose from");
                return;
            }
            Choose("Model", Say.Plural(got.Count, "model") + " on " + Hub.Status.Kind,
                got, Hub.Brain.Model, new Action<string>(Adopted));
        }

        private void Adopted(string name)
        {
            Hub.Brain.Model = name;
            Hub.Settings.Model = name;
            Hub.SaveSettings();
            Fresh();
            Told();
            Hub.Say("Using " + name);
        }

        private void OnVision(object sender, EventArgs e)
        {
            List<string> got = Hub.Status.Models;
            if (got == null || got.Count == 0)
            {
                Hub.Oops("Nothing is answering, so there is nothing to choose from");
                return;
            }
            List<string> with = new List<string>();
            with.Add("None");
            for (int i = 0; i < got.Count; i++) with.Add(got[i]);
            Choose("Model for pictures", "A model that can see, such as llava or a vision build " +
                "of Qwen", with, Hub.Settings.VisionModel, new Action<string>(Seeing));
        }

        private void Seeing(string name)
        {
            Hub.Settings.VisionModel = name == "None" ? "" : name;
            Hub.SaveSettings();
            Fresh();
            Hub.Say(name == "None" ? "No model will be used on pictures" : "Pictures will go to " + name);
        }

        /// The embedded engine's model row picks from the GGUF files actually on
        /// disk, exactly like the server engines pick from a running server.
        private void OnLoad(object sender, EventArgs e)
        {
            LocalEngine local = Hub.Brain.Engine as LocalEngine;
            if (local != null && local.HasModel)
            {
                local.Unload();
                Hub.Say("Model unloaded; it will load again on next use");
                Fresh();
                return;
            }
            string err = null;
            string name = local != null
                ? local.Load(Hub.Brain.Model, out err)
                : null;
            if (name == null)
            {
                Hub.Oops(err ?? "No model loaded");
                return;
            }
            Hub.Brain.Model = name;
            Hub.Settings.Model = name;
            Hub.SaveSettings();
            Fresh();
            Told();
            Hub.Say("Loaded " + name);
        }

        private void OnDownload(object sender, EventArgs e)
        {
            List<ModelEntry> entries = ModelCatalog.All;
            _pickDownload = new Dictionary<string, ModelEntry>();
            List<string> names = new List<string>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                ModelEntry m = entries[i];
                string display = m.Name + "  (" + m.DisplaySize + ")";
                _pickDownload[display] = m;
                names.Add(display);
            }
            Choose("Download a model", "Runs fully on this PC after one download",
                names, "", new Action<string>(Downloaded));
        }

        private Dictionary<string, ModelEntry> _pickDownload;

        private void Downloaded(string display)
        {
            ModelEntry entry = null;
            if (_pickDownload != null && _pickDownload.TryGetValue(display, out entry)) { }
            if (entry == null) return;
            _pickDownload = null;
            _download.Busy = true;
            Hub.Say("Downloading " + entry.Name);
            System.Threading.Thread t = new System.Threading.Thread(
                new System.Threading.ThreadStart(delegate { DownloadWork(entry); }));
            t.IsBackground = true;
            t.Name = "model-download";
            t.Start();
        }

        private void DownloadWork(ModelEntry entry)
        {
            string done = null;
            string error = null;
            try
            {
                ModelDownloader.Download(entry,
                    delegate(long got, long total)
                    {
                        if (total <= 0) return;
                        double pct = Math.Min(100.0, got * 100.0 / total);
                        SayBusy("Downloading " + entry.Name + " " + pct.ToString("0") + "%");
                    }, null);
                done = entry.File;
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            LandDownload(done, error);
        }

        private void SayBusy(string text)
        {
            Control pump = Hub.Pump;
            if (pump == null || !pump.IsHandleCreated) return;
            try
            {
                pump.BeginInvoke(new Action(delegate
                {
                    _download.Busy = true;
                    if (Hub.Toast != null) Hub.Toast.Good(text);
                }));
            }
            catch (Exception) { }
        }

        private void LandDownload(string done, string error)
        {
            Control pump = Hub.Pump;
            if (pump == null || !pump.IsHandleCreated)
            {
                if (done != null) Fresh();
                return;
            }
            try
            {
                pump.BeginInvoke(new Action(delegate
                {
                    _download.Busy = false;
                    if (error != null)
                    {
                        Hub.Oops(error);
                        return;
                    }
                    bool local = Hub.Settings.Engine == EngineKind.Local;
                    if (local)
                    {
                        Hub.Brain.Model = done;
                        Hub.Settings.Model = done;
                    }
                    Hub.SaveSettings();
                    Fresh();
                    Told();
                    Hub.Say(local
                        ? "Downloaded " + done
                        : "Downloaded " + done + ". Switch 'Where it runs' to On this PC to use it");
                    if (local) Hunt();
                }));
            }
            catch (Exception) { }
        }

        private void OnImport(object sender, EventArgs e)
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Choose a GGUF model file";
                dlg.Filter = "GGUF models (*.gguf)|*.gguf|All files (*.*)|*.*";
                if (dlg.ShowDialog() != DialogResult.OK) return;
                LocalEngine localEngine = Hub.Brain.Engine as LocalEngine;
                // Importing just copies a file into the models folder, so it is
                // safe from any engine mode; the active engine does not matter.
                if (localEngine == null) localEngine = new LocalEngine();
                string err = null;
                string name = localEngine.ImportModel(dlg.FileName, out err);
                if (name == null)
                {
                    Hub.Oops(err ?? "Could not import that file");
                    return;
                }
                bool local = Hub.Settings.Engine == EngineKind.Local;
                if (local)
                {
                    Hub.Brain.Model = name;
                    Hub.Settings.Model = name;
                }
                Hub.SaveSettings();
                Fresh();
                Told();
                Hub.Say(local
                    ? "Imported " + name
                    : "Imported " + name + ". Switch 'Where it runs' to On this PC to use it");
                if (local) Hunt();
            }
        }
    }
}
