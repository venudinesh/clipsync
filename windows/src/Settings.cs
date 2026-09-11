// Persisted preferences.
//
// One flat record, read once at startup and written on every change. Defaults
// are chosen so a first run is immediately useful: capture on, processing on,
// and the offline engine selected until a local server is actually found.

using System;
using System.Drawing;
using System.Globalization;

namespace ClipSyncAI
{
    internal enum EngineKind { Offline, Ollama, OpenAiCompatible, Local, Cloud }

    internal enum CloudProviderKind { OpenAi, Anthropic, Gemini, Groq, Mistral, Xai, OpenRouter, Custom }

    internal sealed class AppSettings
    {
        // Appearance
        public bool Dark = true;
        public bool Amoled;
        public bool FollowSystemTheme = true;
        public bool Dense;
        public bool ReduceMotion;
        public int AccentArgb = Palette.DefaultAccent.ToArgb();

        // Capture
        public bool CaptureEnabled = true;
        public bool AutoProcess = true;
        public int DebounceMs = AppDefaults.ClipboardDebounceMs;
        public int MaxHistory = 500;
        public bool SkipSensitive = true;
        public bool AutoRedact;
        public int RetentionDays;
        public string PinHash = "";
        public int MinLength = 2;

        // Engine
        public EngineKind Engine = EngineKind.Offline;
        public bool AutoDiscoverEngine = true;
        public string OllamaUrl = "http://127.0.0.1:11434";
        public string OpenAiUrl = "http://127.0.0.1:1234";
        public string Model = AppDefaults.DefaultModel;
        public string VisionModel = "";

        // Embedded ("On this PC") and cloud engines. The API key itself never
        // lives here — it is sealed separately in its own store. Cloud models
        // are chosen through the ordinary Model row once discovery lists them.
        public CloudProviderKind CloudProvider = CloudProviderKind.OpenAi;
        public string CloudBaseUrl = "";
        public bool CloudApproved;

        // Shell
        public bool RunAtLogin;
        public bool StartMinimised;
        public bool CloseToTray = true;
        public string HotkeyOverlay = "Ctrl+Shift+V";
        public string HotkeyProcess = "Ctrl+Shift+C";
        public string HotkeyHistory = "Ctrl+Shift+H";

        // Capture tab
        public string TesseractPath = "";
        public string SpeechCulture = "";

        public Color Accent
        {
            get { return Color.FromArgb(255, Color.FromArgb(AccentArgb)); }
            set { AccentArgb = value.ToArgb(); }
        }

        public JVal ToJson()
        {
            return JVal.Object()
                .Set("dark", Dark)
                .Set("amoled", Amoled)
                .Set("followSystemTheme", FollowSystemTheme)
                .Set("dense", Dense)
                .Set("reduceMotion", ReduceMotion)
                .Set("accent", (long)AccentArgb)
                .Set("captureEnabled", CaptureEnabled)
                .Set("autoProcess", AutoProcess)
                .Set("debounceMs", (long)DebounceMs)
                .Set("maxHistory", (long)MaxHistory)
                .Set("skipSensitive", SkipSensitive)
                .Set("autoRedact", AutoRedact)
                .Set("retentionDays", (long)RetentionDays)
                .Set("pinHash", PinHash ?? "")
                .Set("minLength", (long)MinLength)
                .Set("engine", EngineName(Engine))
                .Set("autoDiscoverEngine", AutoDiscoverEngine)
                .Set("ollamaUrl", OllamaUrl)
                .Set("openAiUrl", OpenAiUrl)
                .Set("model", Model)
                .Set("visionModel", VisionModel)
                .Set("cloudProvider", CloudProviderName(CloudProvider))
                .Set("cloudBaseUrl", CloudBaseUrl)
                .Set("cloudApproved", CloudApproved)
                .Set("runAtLogin", RunAtLogin)
                .Set("startMinimised", StartMinimised)
                .Set("closeToTray", CloseToTray)
                .Set("hotkeyOverlay", HotkeyOverlay)
                .Set("hotkeyProcess", HotkeyProcess)
                .Set("hotkeyHistory", HotkeyHistory)
                .Set("tesseractPath", TesseractPath)
                .Set("speechCulture", SpeechCulture);
        }

        public static AppSettings FromJson(JVal j)
        {
            AppSettings s = new AppSettings();
            if (j == null || j.Kind != JKind.Obj) return s;
            s.Dark = j["dark"].AsBool(s.Dark);
            s.Amoled = j["amoled"].AsBool(s.Amoled);
            s.FollowSystemTheme = j["followSystemTheme"].AsBool(s.FollowSystemTheme);
            s.Dense = j["dense"].AsBool(s.Dense);
            s.ReduceMotion = j["reduceMotion"].AsBool(s.ReduceMotion);
            s.AccentArgb = unchecked((int)j["accent"].AsLong(s.AccentArgb));
            s.CaptureEnabled = j["captureEnabled"].AsBool(s.CaptureEnabled);
            s.AutoProcess = j["autoProcess"].AsBool(s.AutoProcess);
            s.DebounceMs = Clamp(j["debounceMs"].AsInt(s.DebounceMs), 200, 15000);
            s.MaxHistory = Clamp(j["maxHistory"].AsInt(s.MaxHistory), 20, 20000);
            s.SkipSensitive = j["skipSensitive"].AsBool(s.SkipSensitive);
            s.AutoRedact = j["autoRedact"].AsBool(false);
            s.RetentionDays = Clamp(j["retentionDays"].AsInt(0), 0, 3650);
            s.PinHash = j["pinHash"].AsString("");
            s.MinLength = Clamp(j["minLength"].AsInt(s.MinLength), 1, 200);
            s.Engine = ParseEngine(j["engine"].AsString("offline"));
            s.AutoDiscoverEngine = j["autoDiscoverEngine"].AsBool(s.AutoDiscoverEngine);
            s.OllamaUrl = NonEmpty(j["ollamaUrl"].AsString(""), s.OllamaUrl);
            s.OpenAiUrl = NonEmpty(j["openAiUrl"].AsString(""), s.OpenAiUrl);
            s.Model = NonEmpty(j["model"].AsString(""), s.Model);
            s.VisionModel = j["visionModel"].AsString("");
            s.CloudProvider = ParseCloudProvider(j["cloudProvider"].AsString("openai"));
            s.CloudBaseUrl = j["cloudBaseUrl"].AsString("");
            s.CloudApproved = j["cloudApproved"].AsBool(false);
            s.RunAtLogin = j["runAtLogin"].AsBool(s.RunAtLogin);
            s.StartMinimised = j["startMinimised"].AsBool(s.StartMinimised);
            s.CloseToTray = j["closeToTray"].AsBool(s.CloseToTray);
            s.HotkeyOverlay = NonEmpty(j["hotkeyOverlay"].AsString(""), s.HotkeyOverlay);
            s.HotkeyProcess = NonEmpty(j["hotkeyProcess"].AsString(""), s.HotkeyProcess);
            s.HotkeyHistory = NonEmpty(j["hotkeyHistory"].AsString(""), s.HotkeyHistory);
            s.TesseractPath = j["tesseractPath"].AsString("");
            s.SpeechCulture = j["speechCulture"].AsString("");
            return s;
        }

        private static string NonEmpty(string v, string fallback)
        {
            return string.IsNullOrEmpty(v) ? fallback : v;
        }

        private static int Clamp(int v, int lo, int hi)
        {
            return v < lo ? lo : (v > hi ? hi : v);
        }

        public static string EngineName(EngineKind k)
        {
            if (k == EngineKind.Ollama) return "ollama";
            if (k == EngineKind.OpenAiCompatible) return "openai";
            if (k == EngineKind.Local) return "local";
            if (k == EngineKind.Cloud) return "cloud";
            return "offline";
        }

        public static EngineKind ParseEngine(string s)
        {
            if (s == null) return EngineKind.Offline;
            string t = s.Trim().ToLowerInvariant();
            if (t == "ollama") return EngineKind.Ollama;
            if (t == "openai" || t == "openaicompatible" || t == "lmstudio") return EngineKind.OpenAiCompatible;
            if (t == "local" || t == "onndevice" || t == "embedded") return EngineKind.Local;
            if (t == "cloud" || t == "cloudapi") return EngineKind.Cloud;
            return EngineKind.Offline;
        }

        public static string CloudProviderName(CloudProviderKind p)
        {
            switch (p)
            {
                case CloudProviderKind.OpenAi: return "openai";
                case CloudProviderKind.Anthropic: return "anthropic";
                case CloudProviderKind.Gemini: return "gemini";
                case CloudProviderKind.Groq: return "groq";
                case CloudProviderKind.Mistral: return "mistral";
                case CloudProviderKind.Xai: return "xai";
                case CloudProviderKind.OpenRouter: return "openrouter";
                default: return "custom";
            }
        }

        public static CloudProviderKind ParseCloudProvider(string s)
        {
            if (s == null) return CloudProviderKind.OpenAi;
            string t = s.Trim().ToLowerInvariant();
            if (t == "anthropic" || t == "claude") return CloudProviderKind.Anthropic;
            if (t == "gemini" || t == "google") return CloudProviderKind.Gemini;
            if (t == "groq") return CloudProviderKind.Groq;
            if (t == "mistral") return CloudProviderKind.Mistral;
            if (t == "xai" || t == "grok") return CloudProviderKind.Xai;
            if (t == "openrouter") return CloudProviderKind.OpenRouter;
            if (t == "custom") return CloudProviderKind.Custom;
            return CloudProviderKind.OpenAi;
        }

        public AppSettings Clone()
        {
            return FromJson(JsonParser.Parse(JsonWriter.Write(ToJson())));
        }
    }
}
