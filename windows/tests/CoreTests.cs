// Everything below the UI: JSON, encryption, the stores, the design tokens,
// the greeting picker and the loopback guard.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

namespace ClipSyncAI.Tests
{
    internal static class CoreTests
    {
        public static void Run()
        {
            Json();
            Tokens();
            Crypt();
            Stores();
            Greetings();
            Loopback();
            ModelPick();
            CloudSettings();
            Catalog();
            Transport();
            LocalEngines();
        }

        private static void Json()
        {
            T.Group("JSON");
            JVal o = JVal.Object()
                .Set("s", "hello")
                .Set("n", 42L)
                .Set("f", 1.5)
                .Set("b", true)
                .Set("z", JVal.Null)
                .Set("a", JVal.Array().Add(JVal.Of(1L)).Add(JVal.Of("two")));
            string text = JsonWriter.Write(o);
            T.Eq("integers are written without a decimal point",
                "{\"s\":\"hello\",\"n\":42,\"f\":1.5,\"b\":true,\"z\":null,\"a\":[1,\"two\"]}", text);

            JVal back = JsonParser.Parse(text);
            T.Ok("round trip parses", back != null);
            T.Eq("string survives", "hello", back["s"].AsString());
            T.Eq("integer survives", 42, back["n"].AsInt(0));
            T.Near("double survives", 1.5, back["f"].AsNum(0), 1e-12);
            T.Eq("bool survives", true, back["b"].AsBool(false));
            T.Eq("null kind survives", true, back["z"].Kind == JKind.Null);
            T.Eq("array length survives", 2, back["a"].Count);
            T.Eq("array member survives", "two", back["a"].At(1).AsString());

            T.Eq("missing key is null, not a throw", true, back["nope"].Kind == JKind.Null);
            T.Eq("missing key falls back", "dflt", back["nope"].AsString("dflt"));
            T.Eq("index past the end falls back", true, back["a"].At(9).Kind == JKind.Null);

            string tricky = "line\nbreak\ttab \"quote\" back\\slash " + char.ConvertFromUtf32(0x1F4C5);
            JVal esc = JsonParser.Parse(JsonWriter.Write(JVal.Object().Set("t", tricky)));
            T.Eq("escapes round trip", tricky, esc["t"].AsString());

            T.Ok("invalid JSON returns null", JsonParser.Parse("{oops}") == null);
            T.Ok("truncated JSON returns null", JsonParser.Parse("{\"a\":") == null);
            T.Ok("trailing junk returns null", JsonParser.Parse("{} extra") == null);
            T.Eq("unicode escape is decoded", "A", JsonParser.Parse("\"\\u0041\"").AsString());
            T.Eq("nested objects", "deep",
                JsonParser.Parse("{\"a\":{\"b\":{\"c\":\"deep\"}}}")["a"]["b"]["c"].AsString());
            T.Eq("pretty output parses back", 1,
                JsonParser.Parse(JsonWriter.Pretty(JVal.Object().Set("k", 1L)))["k"].AsInt(0));
        }

        private static void Tokens()
        {
            T.Group("Design tokens");
            T.Near("black on white contrast is 21", 21.0,
                A11y.ContrastRatio(Color.Black, Color.White), 0.05);
            T.Near("a colour against itself is 1", 1.0,
                A11y.ContrastRatio(Palette.DarkCanvas, Palette.DarkCanvas), 0.001);

            for (int i = 0; i < Palette.Accents.Length; i++)
            {
                AccentSwatch a = Palette.Accents[i];
                double onDark = A11y.ContrastRatio(a.Color, Palette.DarkCanvas);
                T.Ok(a.Name + " clears 4.5 on the dark canvas", onDark >= 4.5);
                Color lifted = A11y.LegibleAccent(a.Color, Palette.LightCanvas, 4.5);
                T.Ok(a.Name + " is lifted to 4.5 on the light canvas",
                    A11y.ContrastRatio(lifted, Palette.LightCanvas) >= 4.4);
                double h, s, l;
                Hsl.FromColor(a.Color, out h, out s, out l);
                T.Ok(a.Name + " stays under 80 percent saturation", s < 0.80);
            }

            T.Ok("ink on an accent is legible",
                A11y.ContrastRatio(A11y.ReadableOn(Palette.DefaultAccent), Palette.DefaultAccent) >= 4.5);

            double hh, ss, ll;
            Hsl.FromColor(Color.FromArgb(255, 0xDF, 0xA5, 0xB4), out hh, out ss, out ll);
            Color rt = Hsl.ToColor(hh, ss, ll);
            T.Eq("HSL round trip red", 0xDF, rt.R);
            T.Eq("HSL round trip green", 0xA5, rt.G);
            T.Eq("HSL round trip blue", 0xB4, rt.B);

            T.Eq("concentric inner radius", 16, Radii.Core(Radii.Card, 6));
            T.Eq("inner radius clamps at zero", 0, Radii.Core(4, 10));
            T.Near("the house curve starts at zero", 0.0, Ease.Glass(0), 1e-9);
            T.Near("the house curve ends at one", 1.0, Ease.Glass(1), 1e-9);
            T.Ok("the house curve leads ahead of linear at the midpoint", Ease.Glass(0.5) > 0.5);
            T.Ok("the bounce curve overshoots", Ease.Bounce(0.62) > 1.0);

            Theme.Scale = 1.5;
            T.Eq("scaled spacing rounds", 24, Theme.Px(16));
            T.Eq("a hairline never rounds away", 1, Theme.Px(0.5));
            Theme.Scale = 1.0;
            T.Eq("unscaled spacing passes through", 16, Theme.Px(16));

            T.Eq("the rail replaces the phone's floating bar", 208, Space.Rail);
        }

        private static void Crypt()
        {
            T.Group("Encryption");
            string text = "secret clipboard text " + char.ConvertFromUtf32(0x1F4C5) + " 日本語";
            byte[] sealedBlob = Crypto.Seal(text);
            T.Ok("sealed output is not the plain text",
                Encoding.UTF8.GetString(sealedBlob).IndexOf("secret", StringComparison.Ordinal) < 0);
            T.Eq("unseal returns the original", text, Crypto.Open(sealedBlob));

            byte[] tampered = (byte[])sealedBlob.Clone();
            tampered[tampered.Length - 20] ^= 0x01;
            T.Ok("a tampered tag is refused", Crypto.Open(tampered) == null);

            byte[] flipped = (byte[])sealedBlob.Clone();
            flipped[30] ^= 0x40;
            T.Ok("a tampered body is refused", Crypto.Open(flipped) == null);

            byte[] truncated = new byte[sealedBlob.Length - 8];
            Buffer.BlockCopy(sealedBlob, 0, truncated, 0, truncated.Length);
            T.Ok("a truncated blob is refused", Crypto.Open(truncated) == null);

            T.Ok("a foreign blob is refused", Crypto.Open(Encoding.UTF8.GetBytes("not ours at all")) == null);
            T.Eq("an empty string round trips", "", Crypto.Open(Crypto.Seal("")));

            byte[] a = Crypto.Seal(text);
            byte[] b = Crypto.Seal(text);
            T.Ok("the same text seals to different bytes each time",
                Convert.ToBase64String(a) != Convert.ToBase64String(b));
        }

        private static void Stores()
        {
            T.Group("Stores");
            Store<ClipEntry> clips = new Store<ClipEntry>(
                "test_clips", delegate(ClipEntry c) { return c.ToJson(); }, ClipEntry.FromJson);
            clips.Load();
            clips.Items.Clear();

            ClipEntry one = new ClipEntry();
            one.RawText = "hello  world\nsecond line";
            one.ProcessedMarkdown = "**hello:** world";
            one.IsPinned = true;
            clips.Items.Add(one);
            clips.Save();

            Store<ClipEntry> reread = new Store<ClipEntry>(
                "test_clips", delegate(ClipEntry c) { return c.ToJson(); }, ClipEntry.FromJson);
            reread.Load();
            T.Eq("one clip persisted", 1, reread.Items.Count);
            T.Eq("raw text persisted", one.RawText, reread.Items[0].RawText);
            T.Eq("pin persisted", true, reread.Items[0].IsPinned);
            T.Eq("id persisted", one.Id, reread.Items[0].Id);
            T.Eq("timestamp survives to the second",
                one.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                reread.Items[0].Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
            T.Eq("preview collapses whitespace", "hello world second line", reread.Items[0].Preview(80));
            T.Contains("preview truncates", one.Preview(8), "...");

            T.Ok("the store file is not readable as text",
                File.ReadAllText(Paths.Store("test_clips")).IndexOf("hello", StringComparison.Ordinal) < 0);

            Note n = new Note();
            n.Title = "Title";
            n.Content = "Body";
            n.Tags.Add("work");
            n.Tags.Add("later");
            Note n2 = Note.FromJson(JsonParser.Parse(JsonWriter.Write(n.ToJson())));
            T.Eq("note title round trips", "Title", n2.Title);
            T.Eq("note tag count round trips", 2, n2.Tags.Count);
            T.Eq("note tag round trips", "later", n2.Tags[1]);

            ChatSession cs = new ChatSession();
            cs.Messages.Add(new ChatMessage("user", "Explain the difference between a mutex and a semaphore please"));
            cs.Messages.Add(new ChatMessage("assistant", "Short answer."));
            cs.TitleFromFirstMessage();
            T.Ok("chat title comes from the first message", cs.Title.StartsWith("Explain the difference", StringComparison.Ordinal));
            T.Ok("chat title is trimmed", cs.Title.Length <= 45);
            ChatSession cs2 = ChatSession.FromJson(JsonParser.Parse(JsonWriter.Write(cs.ToJson())));
            T.Eq("chat messages round trip", 2, cs2.Messages.Count);
            T.Eq("chat roles round trip", "assistant", cs2.Messages[1].Role);

            AppSettings s = new AppSettings();
            s.Dark = false;
            s.Amoled = true;
            s.MaxHistory = 999;
            s.Engine = EngineKind.Ollama;
            s.Accent = Palette.Accents[2].Color;
            SettingsStore.Save(s);
            AppSettings s2 = SettingsStore.Load();
            T.Eq("theme flag persisted", false, s2.Dark);
            T.Eq("amoled persisted", true, s2.Amoled);
            T.Eq("history cap persisted", 999, s2.MaxHistory);
            T.Eq("engine kind persisted", true, s2.Engine == EngineKind.Ollama);
            T.Eq("accent persisted", Palette.Accents[2].Color.ToArgb(), s2.Accent.ToArgb());

            AppSettings clamped = AppSettings.FromJson(
                JsonParser.Parse("{\"maxHistory\":5,\"debounceMs\":99999}"));
            T.Eq("a silly history cap is clamped up", 20, clamped.MaxHistory);
            T.Eq("a silly debounce is clamped down", 15000, clamped.DebounceMs);
            T.Eq("an absent key keeps the default", true, clamped.CaptureEnabled);

            clips.Clear();
            T.Eq("clear empties the store", 0, clips.Items.Count);
        }

        private static void Greetings()
        {
            T.Group("Greetings");
            T.Eq("midnight", true, Timely.PartAt(new DateTime(2026, 1, 1, 0, 30, 0)) == DayPart.Midnight);
            T.Eq("morning", true, Timely.PartAt(new DateTime(2026, 1, 1, 5, 0, 0)) == DayPart.Morning);
            T.Eq("afternoon", true, Timely.PartAt(new DateTime(2026, 1, 1, 12, 0, 0)) == DayPart.Afternoon);
            T.Eq("early evening", true, Timely.PartAt(new DateTime(2026, 1, 1, 17, 0, 0)) == DayPart.EarlyEvening);
            T.Eq("evening", true, Timely.PartAt(new DateTime(2026, 1, 1, 19, 0, 0)) == DayPart.Evening);
            T.Eq("night", true, Timely.PartAt(new DateTime(2026, 1, 1, 22, 0, 0)) == DayPart.Night);

            T.Eq("midnight greeting", "Solemn midnight.", Timely.GreetingFor(DayPart.Midnight));
            T.Eq("early evening shares the evening greeting",
                Timely.GreetingFor(DayPart.Evening), Timely.GreetingFor(DayPart.EarlyEvening));

            T.Eq("an echo is detected", true,
                Timely.RestatesGreeting("It is evening right now.", "Good evening."));
            T.Eq("a non echo is not", false,
                Timely.RestatesGreeting("It is late right now.", "Good night."));
            T.Eq("an unrelated line is not an echo", false,
                Timely.RestatesGreeting("Enjoy the night.", "Good evening."));

            // The picker must never hand back the line the greeting already
            // states, at any hour, on any seed.
            for (int seed = 0; seed < 200; seed++)
            {
                DateTime when = new DateTime(2026, 1, 1, seed % 24, 0, 0);
                TimelyGreeting g = Timely.Pick(when, null, new Random(seed));
                if (Timely.RestatesGreeting(g.Message, g.Greeting))
                {
                    T.Ok("picker avoided the echo at hour " + when.Hour, false);
                    return;
                }
                if (g.Message == null || g.Message.Length == 0)
                {
                    T.Ok("picker returned a line at hour " + when.Hour, false);
                    return;
                }
            }
            T.Ok("the picker never echoes the greeting across 200 seeds", true);

            T.Eq("the next boundary from 06:00 is noon", 12,
                Timely.NextBoundary(new DateTime(2026, 1, 1, 6, 0, 0)).Hour);
            DateTime rollover = Timely.NextBoundary(new DateTime(2026, 1, 1, 23, 30, 0));
            T.Eq("past the last boundary it rolls to the next day", 2, rollover.Day);
            T.Eq("and lands on midnight", 0, rollover.Hour);

            // Every line in every pool is held to the house copy rules.
            string[][] pools = {
                TimelyPools.Morning, TimelyPools.Afternoon, TimelyPools.EarlyEvening,
                TimelyPools.Evening, TimelyPools.Late, TimelyPools.AnyTime
            };
            int bad = 0;
            for (int p = 0; p < pools.Length; p++)
            {
                for (int i = 0; i < pools[p].Length; i++)
                {
                    string m = pools[p][i];
                    if (m.IndexOf('\u2014') >= 0 || m.IndexOf('\u2013') >= 0) bad++;
                    if (m.Trim() != m) bad++;
                    if (m.Length == 0) bad++;
                }
            }
            T.Eq("no pool line uses a dash the house rules ban, or stray space", 0, bad);
        }

        private static void Loopback()
        {
            T.Group("Loopback guard");
            T.Eq("127.0.0.1 is allowed", true, Http.IsLoopback("http://127.0.0.1:11434/api/tags"));
            T.Eq("localhost is allowed", true, Http.IsLoopback("http://localhost:1234/v1/models"));
            T.Eq("the IPv6 loopback is allowed", true, Http.IsLoopback("http://[::1]:8080/v1/models"));
            T.Eq("a public host is refused", false, Http.IsLoopback("http://api.example.com/v1/chat"));
            T.Eq("a LAN address is refused", false, Http.IsLoopback("http://192.168.1.10:11434"));
            T.Eq("a name that could resolve anywhere is refused", false, Http.IsLoopback("http://ollama.local:11434"));
            T.Eq("a non HTTP scheme is refused", false, Http.IsLoopback("file:///C:/secrets.txt"));
            T.Eq("nonsense is refused", false, Http.IsLoopback("not a url"));
            T.Eq("empty is refused", false, Http.IsLoopback(""));
        }

        private static void ModelPick()
        {
            T.Group("Model selection");
            List<string> have = new List<string> { "llama3.1:70b", "smollm2:135m", "qwen2.5:7b" };
            T.Eq("an exact match wins", "smollm2:135m", Ai.ChooseModel(have, "smollm2:135m"));
            T.Eq("a prefix match wins next", "smollm2:135m", Ai.ChooseModel(have, "smollm2"));
            T.Eq("otherwise the smallest known family wins", "smollm2:135m", Ai.ChooseModel(have, "not-installed"));
            T.Eq("with nothing known, take the first", "llama3.1:70b",
                Ai.ChooseModel(new List<string> { "llama3.1:70b" }, "nope"));
            T.Eq("an empty list keeps the preference", "wanted", Ai.ChooseModel(new List<string>(), "wanted"));
            T.Eq("a GGUF file name is picked by family", "smollm2-135m.gguf",
                Ai.ChooseModel(new List<string> { "llama-3.2-3b.gguf", "smollm2-135m.gguf" }, "smollm2"));
        }

        private static void CloudSettings()
        {
            T.Group("Cloud and embedded settings");
            AppSettings s = new AppSettings();
            s.Engine = EngineKind.Local;
            s.CloudProvider = CloudProviderKind.Anthropic;
            s.CloudBaseUrl = "https://proxy.example.com/v1";
            s.CloudApproved = true;
            SettingsStore.Save(s);
            AppSettings back = SettingsStore.Load();
            T.Eq("local engine kind persists", true, back.Engine == EngineKind.Local);
            T.Eq("provider persists", true, back.CloudProvider == CloudProviderKind.Anthropic);
            T.Eq("custom endpoint persists", "https://proxy.example.com/v1", back.CloudBaseUrl);
            T.Eq("cloud approval persists", true, back.CloudApproved);

            s.Engine = EngineKind.Cloud;
            SettingsStore.Save(s);
            back = SettingsStore.Load();
            T.Eq("cloud engine kind persists", true, back.Engine == EngineKind.Cloud);

            T.Ok("engine names round trip", AppSettings.ParseEngine(AppSettings.EngineName(EngineKind.Cloud)) == EngineKind.Cloud);
            T.Ok("local engine names round trip", AppSettings.ParseEngine(AppSettings.EngineName(EngineKind.Local)) == EngineKind.Local);
            T.Eq("unknown engine names fall back", EngineKind.Offline.ToString(), AppSettings.ParseEngine("something-new").ToString());
            T.Ok("provider names round trip", AppSettings.ParseCloudProvider(AppSettings.CloudProviderName(CloudProviderKind.Gemini)) == CloudProviderKind.Gemini);
            T.Eq("a grok alias maps to xAI", CloudProviderKind.Xai.ToString(), AppSettings.ParseCloudProvider("grok").ToString());

            T.Eq("openai base", "https://api.openai.com/v1", CloudEngine.ProviderBase(CloudProviderKind.OpenAi));
            T.Eq("groq base", "https://api.groq.com/openai/v1", CloudEngine.ProviderBase(CloudProviderKind.Groq));
            T.Eq("gemini base", "https://generativelanguage.googleapis.com", CloudEngine.ProviderBase(CloudProviderKind.Gemini));
            T.Eq("provider names are human", "OpenRouter", CloudEngine.ProviderName(CloudProviderKind.OpenRouter));

            // The engine factory maps the new kinds without touching a port.
            s.Engine = EngineKind.Local;
            T.Ok("build makes a local engine", EngineFactory.Build(s) is LocalEngine);
            s.Engine = EngineKind.Cloud;
            T.Ok("build makes a cloud engine", EngineFactory.Build(s) is CloudEngine);
            AppSettings off = new AppSettings();
            T.Eq("offline builds nothing", true, EngineFactory.Build(off) == null);

            // The key lives in its own sealed store, never in the settings JSON.
            CloudKeys.Save("sk-test-secret-123");
            T.Eq("key round trips through its sealed store", "sk-test-secret-123", CloudKeys.Load());
            string json = JsonWriter.Write(s.ToJson());
            T.Ok("the key never appears in the settings export",
                json.IndexOf("sk-test-secret-123", StringComparison.Ordinal) < 0);
            CloudKeys.Save("");
            T.Eq("an empty key clears the store", false, CloudKeys.HasKey);
        }

        private static void Catalog()
        {
            T.Group("Model catalog");
            T.Ok("the catalog is not empty", ModelCatalog.All.Count >= 5);
            for (int i = 0; i < ModelCatalog.All.Count; i++)
            {
                ModelEntry m = ModelCatalog.All[i];
                T.Ok(m.Name + " has a repo and file", m.Repo.Length > 0 && m.File.Length > 0);
                T.Ok(m.Name + " has a sane size", m.Bytes > 10L * 1024 * 1024);
                string u = ModelCatalog.DownloadUrl(m);
                T.Ok(m.Name + " downloads from huggingface", u.StartsWith("https://huggingface.co/", StringComparison.Ordinal));
                T.Ok(m.Name + " url names its file", u.EndsWith("/" + m.File, StringComparison.Ordinal));
                T.Ok(m.Name + " size reads humanly", m.DisplaySize.Length > 0);
            }
        }

        private static void Transport()
        {
            T.Group("Transport gating");
            // The loopback-only helpers must refuse anything that is not local.
            try
            {
                Http.Get("http://api.example.com/v1/models", 500);
                T.Ok("loopback helper refuses a remote GET", false);
            }
            catch (InvalidOperationException) { T.Ok("loopback helper refuses a remote GET", true); }
            catch (Exception) { T.Ok("loopback helper refuses a remote GET", false); }
            // The remote helpers build a request to a public host without
            // checking the loopback rule (the network call itself is not made).
            T.Ok("remote helper builds a request to a public host",
                Http.OpenRemote("https://api.example.com/v1/models", "GET", 500) != null);
        }

        private static void LocalEngines()
        {
            T.Group("Embedded engine state");
            LocalEngine local = new LocalEngine();
            T.Eq("kind reads well", "On this PC", local.Kind);

            string modelsDir = Path.Combine(Paths.Root, "models");
            Directory.CreateDirectory(modelsDir);
            T.Eq("no models yet", 0, local.Models().Count);
            string err;
            T.Eq("loading with nothing present explains itself", true,
                local.Load("anything.gguf", out err) == null && !string.IsNullOrEmpty(err));

            // A fake GGUF that is large enough to pass the sanity check: with
            // the runtime absent (tests carry no DLLs) the load must still fail
            // cleanly with the runtime's own message, not a crash.
            byte[] blob = new byte[2 * 1024 * 1024];
            File.WriteAllBytes(Path.Combine(modelsDir, "dummy.gguf"), blob);
            T.Eq("a plausible file appears in the list", 1, local.Models().Count);
            T.Eq("the name is the file name", "dummy.gguf", local.Models()[0]);
            string loadErr;
            string got = local.Load("dummy.gguf", out loadErr);
            T.Ok("loading without the runtime reports why",
                got == null && (loadErr ?? "").Length > 0);
            local.Unload();

            // A file too small to be a model is refused before the runtime is
            // even considered.
            File.WriteAllBytes(Path.Combine(modelsDir, "tiny.gguf"), new byte[4]);
            string tinyErr;
            T.Ok("a stub file is refused as unusable",
                local.Load("tiny.gguf", out tinyErr) == null &&
                (tinyErr ?? "").IndexOf("usable", StringComparison.OrdinalIgnoreCase) >= 0);

            // Importing a foreign file copies it into the models folder.
            string src = Path.Combine(Paths.Root, "elsewhere.bin");
            File.WriteAllBytes(src, new byte[8]);
            string impErr;
            string imported = local.ImportModel(src, out impErr);
            T.Eq("import copies into the models folder", "elsewhere.bin", imported);
            T.Eq("imported file exists", true, File.Exists(Path.Combine(modelsDir, "elsewhere.bin")));
            T.Eq("importing an existing name refuses", true,
                local.ImportModel(src, out impErr) == null && impErr.IndexOf("already", StringComparison.OrdinalIgnoreCase) >= 0);

            T.Eq("delete removes the model file", true, local.DeleteModel("dummy.gguf", out err));
            T.Eq("delete of a missing model reports it", false, local.DeleteModel("dummy.gguf", out err));

            // Full smoke test only when the real runtime and a real model are
            // present (an end user's machine after a download, or a dev box).
            if (NativeLoader.Available)
            {
                List<string> real = local.Models();
                string chosen = null;
                for (int i = 0; i < real.Count; i++)
                {
                    if (real[i].EndsWith(".gguf", StringComparison.OrdinalIgnoreCase) &&
                        new FileInfo(Path.Combine(modelsDir, real[i])).Length > 10L * 1024 * 1024)
                    {
                        chosen = real[i];
                        break;
                    }
                }
                if (chosen != null)
                {
                    T.Ok("a real model loads", local.Load(chosen, out err) != null);
                    List<ChatMessage> msgs = new List<ChatMessage>();
                    msgs.Add(new ChatMessage("user", "Say OK."));
                    StringBuilder gotText = new StringBuilder();
                    string reply = local.Chat(chosen, msgs, 32, 0.0, 1.0, null,
                        delegate(string piece) { gotText.Append(piece); });
                    T.Ok("the model answers", (reply ?? "").Length > 0);
                    T.Ok("streaming delivered the same text", gotText.ToString() == reply);
                    local.Unload();
                }
            }
        }
    }
}
