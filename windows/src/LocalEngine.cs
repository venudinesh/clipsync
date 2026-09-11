// The embedded engine: llama.cpp running in this process.
//
// The phone build loads a GGUF file into llama.cpp and this is the desktop
// equivalent — the runtime DLLs are carried inside the executable and extracted
// on first use (see NativeLoader), so a model can be chosen and run directly
// from inside the app with nothing else installed. Like the phone build it is
// CPU-only and single sequence: requests are serialised on one context, which
// is all a clipboard reformat or a chat turn needs.
//
// Generation is a classic prompt/loop: tokenise, decode the prompt, then sample
// one token at a time until the model says it is finished or the budget runs
// out. The sampler chain is built per call so the temperature and top_p the
// caller chose are actually honoured; a temperature of zero falls back to a
// greedy pick.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ClipSyncAI
{
    internal sealed class LocalEngine : InferenceEngine
    {
        private const uint MaxContext = 4096;
        private const uint BatchSize = 512;
        private const uint UbatchSize = 256;
        private const int PieceBuf = 128;

        private readonly object _gate = new object();
        private bool _backend;
        private IntPtr _model;
        private IntPtr _ctx;
        private IntPtr _vocab;
        private string _loaded;
        private string _loadedName;

        public static string ModelsDir { get { return Path.Combine(Paths.Root, "models"); } }

        public override string Kind { get { return "On this PC"; } }
        public override string BaseUrl { get { return "embedded llama.cpp"; } }

        public bool HasModel { get { lock (_gate) return _ctx != IntPtr.Zero; } }
        public string LoadedName { get { lock (_gate) return _loadedName; } }

        private void EnsureBackend()
        {
            if (_backend) return;
            if (!NativeLoader.Available) throw new InvalidOperationException(NativeLoader.Error);
            Llama.BackendInit();
            // The CPU backend ships as a plugin (ggml-cpu-*.dll) that is only
            // discovered when the runtime is pointed at the folder holding it;
            // the no-argument variant scans the executable and working
            // directories, so every model load failed with "no backends".
            byte[] dirBytes = Encoding.UTF8.GetBytes(NativeLoader.Dir + "\0");
            GCHandle dh = GCHandle.Alloc(dirBytes, GCHandleType.Pinned);
            try { Llama.BackendLoadAllFromPath(dh.AddrOfPinnedObject()); }
            finally { dh.Free(); }
            _backend = true;
        }

        public override bool Ping(out string error)
        {
            error = null;
            if (IntPtr.Size != 8) { error = "Embedded models need the 64-bit build of ClipSyncAI."; return false; }
            if (!NativeLoader.Available) { error = NativeLoader.Error; return false; }
            // Selecting "On this PC" is the answer even before any model has been
            // downloaded or imported: the engine is what runs on this PC, and a
            // missing model is a status message, not a reason to fall back to
            // offline. The Models page offers the download and import rows.
            return true;
        }

        public override List<string> Models()
        {
            List<string> names = new List<string>();
            try
            {
                if (!Directory.Exists(ModelsDir)) return names;
                foreach (string f in Directory.GetFiles(ModelsDir, "*.gguf"))
                {
                    names.Add(Path.GetFileName(f));
                }
            }
            catch (Exception ex)
            {
                Paths.Log("local models", ex);
            }
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        public static long ModelSize(string fileName)
        {
            try
            {
                FileInfo fi = new FileInfo(Path.Combine(ModelsDir, fileName));
                return fi.Exists ? fi.Length : -1;
            }
            catch (Exception) { return -1; }
        }

        /// Resolves the picker value ("smollm2-135m.gguf" or a bare name) to a
        /// real file path, tolerating case differences and a missing extension.
        private static string Resolve(string model)
        {
            if (string.IsNullOrEmpty(model)) return null;
            string name = model.Trim();
            if (!name.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase)) name += ".gguf";
            try
            {
                string dir = ModelsDir;
                string full = Path.Combine(dir, name);
                if (File.Exists(full)) return full;
                if (!Directory.Exists(dir)) return null;
                foreach (string f in Directory.GetFiles(dir, "*.gguf"))
                {
                    if (string.Equals(Path.GetFileName(f), name, StringComparison.OrdinalIgnoreCase)) return f;
                }
            }
            catch (Exception) { }
            return null;
        }

        /// Loads the model named in the picker, freeing whatever was loaded
        /// before. Returns the loaded file name, or null with [error] set.
        public string Load(string model, out string error)
        {
            error = null;
            lock (_gate)
            {
                string path = Resolve(model);
                if (path == null)
                {
                    error = string.IsNullOrEmpty(model)
                        ? "No model chosen. Pick one below."
                        : "Model file not found: " + model;
                    return null;
                }
                FileInfo fi = new FileInfo(path);
                if (!fi.Exists || fi.Length < 1024 * 1024)
                {
                    error = "\"" + Path.GetFileName(path) + "\" does not look like a usable model.";
                    return null;
                }
                try
                {
                    EnsureBackend();
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return null;
                }
                if (_ctx != IntPtr.Zero && string.Equals(path, _loaded, StringComparison.OrdinalIgnoreCase))
                {
                    return _loadedName;
                }
                UnloadLocked();
                IntPtr modelH = IntPtr.Zero;
                IntPtr ctxH = IntPtr.Zero;
                try
                {
                    LlamaModelParams mp = Llama.ModelDefaultParams();
                    mp.NGpuLayers = 0;
                    byte[] pathBytes = Encoding.UTF8.GetBytes(path);
                    GCHandle ph = GCHandle.Alloc(pathBytes, GCHandleType.Pinned);
                    try { modelH = Llama.ModelLoadFromFile(ph.AddrOfPinnedObject(), mp); }
                    finally { ph.Free(); }
                    if (modelH == IntPtr.Zero)
                    {
                        error = "The model could not be loaded: " + Path.GetFileName(path);
                        return null;
                    }
                    LlamaContextParams cp = Llama.ContextDefaultParams();
                    cp.NCtx = MaxContext;
                    cp.NBatch = BatchSize;
                    cp.NUbatch = UbatchSize;
                    ctxH = Llama.InitFromModel(modelH, cp);
                    if (ctxH == IntPtr.Zero)
                    {
                        Llama.ModelFree(modelH);
                        error = "A context could not be created for: " + Path.GetFileName(path);
                        return null;
                    }
                    _model = modelH;
                    _ctx = ctxH;
                    _vocab = Llama.ModelGetVocab(modelH);
                    _loaded = path;
                    _loadedName = Path.GetFileName(path);
                    return _loadedName;
                }
                catch (Exception ex)
                {
                    if (ctxH != IntPtr.Zero) Llama.Free(ctxH);
                    if (modelH != IntPtr.Zero) Llama.ModelFree(modelH);
                    error = ex.Message;
                    return null;
                }
            }
        }

        public void Unload()
        {
            lock (_gate) UnloadLocked();
        }

        private void UnloadLocked()
        {
            if (_ctx != IntPtr.Zero) { Llama.Free(_ctx); _ctx = IntPtr.Zero; }
            if (_model != IntPtr.Zero) { Llama.ModelFree(_model); _model = IntPtr.Zero; }
            _vocab = IntPtr.Zero;
            _loaded = null;
            _loadedName = null;
        }

        /// Copies a GGUF file the user picked into the models folder.
        public string ImportModel(string source, out string error)
        {
            error = null;
            try
            {
                string dir = ModelsDir;
                Directory.CreateDirectory(dir);
                string name = Path.GetFileName(source);
                if (string.IsNullOrEmpty(name))
                {
                    error = "Nothing was chosen.";
                    return null;
                }
                string target = Path.Combine(dir, name);
                if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(target),
                    StringComparison.OrdinalIgnoreCase))
                {
                    error = "That file is already in the models folder.";
                    return null;
                }
                if (File.Exists(target))
                {
                    error = "\"" + name + "\" is already in the models folder.";
                    return null;
                }
                File.Copy(source, target);
                return name;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
        }

        /// Removes a model file. A loaded model is unloaded first so Windows
        /// lets go of the file handle.
        public bool DeleteModel(string fileName, out string error)
        {
            error = null;
            lock (_gate)
            {
                try
                {
                    string path = Resolve(fileName);
                    if (path == null)
                    {
                        error = "Model file not found: " + fileName;
                        return false;
                    }
                    if (_ctx != IntPtr.Zero && string.Equals(path, _loaded, StringComparison.OrdinalIgnoreCase))
                    {
                        UnloadLocked();
                    }
                    File.Delete(path);
                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }
            }
        }

        public override string Generate(string model, string system, string prompt,
            int maxTokens, double temperature, double topP, HttpCall call, Action<string> onToken)
        {
            List<ChatMessage> msgs = new List<ChatMessage>();
            if (!string.IsNullOrEmpty(system)) msgs.Add(new ChatMessage("system", system));
            msgs.Add(new ChatMessage("user", prompt ?? ""));
            return Chat(model, msgs, maxTokens, temperature, topP, call, onToken);
        }

        public override string Chat(string model, IList<ChatMessage> messages,
            int maxTokens, double temperature, double topP, HttpCall call, Action<string> onToken)
        {
            lock (_gate)
            {
                string error;
                string loaded = Load(model, out error);
                if (loaded == null) throw new InvalidOperationException(error ?? "No model loaded.");
                string promptText = ApplyTemplate(messages, true);
                int[] toks = Tokenize(promptText, true);
                if (toks.Length == 0) return "";
                return Run(toks, maxTokens, temperature, topP, call, onToken);
            }
        }

        private static string Utf8FromNative(IntPtr p)
        {
            if (p == IntPtr.Zero) return null;
            int len = 0;
            while (Marshal.ReadByte(p, len) != 0) len++;
            if (len == 0) return "";
            byte[] b = new byte[len];
            Marshal.Copy(p, b, 0, len);
            return Encoding.UTF8.GetString(b);
        }

        private int[] Tokenize(string text, bool addSpecial)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? "");
            GCHandle ph = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                int need = Llama.Tokenize(_vocab, ph.AddrOfPinnedObject(), bytes.Length,
                    IntPtr.Zero, 0, addSpecial, false);
                // A negative result is the required token count; this runtime
                // returns -needed when the output buffer is too small, so the
                // first sizing call comes back as -55, not 55.
                if (need < 0) need = -need;
                if (need == 0) return new int[0];
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    int[] toks = new int[need];
                    GCHandle th = GCHandle.Alloc(toks, GCHandleType.Pinned);
                    int got;
                    try
                    {
                        got = Llama.Tokenize(_vocab, ph.AddrOfPinnedObject(), bytes.Length,
                            th.AddrOfPinnedObject(), toks.Length, addSpecial, false);
                    }
                    finally { th.Free(); }
                    if (got >= 0)
                    {
                        if (got == 0) return new int[0];
                        if (got == toks.Length) return toks;
                        int[] result = new int[got];
                        Array.Copy(toks, result, got);
                        return result;
                    }
                    need = -got;
                }
                return new int[0];
            }
            finally { ph.Free(); }
        }

        /// Applies the model's chat template so roles are framed the way the
        /// model was trained to see them. Falls back to a flat "role: text"
        /// prompt for models without a template.
        private string ApplyTemplate(IList<ChatMessage> messages, bool addAssistantTurn)
        {
            string template = Utf8FromNative(Llama.ModelChatTemplate(_model, IntPtr.Zero));
            if (string.IsNullOrEmpty(template))
            {
                StringBuilder sb = new StringBuilder(256);
                for (int i = 0; i < messages.Count; i++)
                {
                    sb.Append(messages[i].Role).Append(": ")
                      .Append(messages[i].Content ?? "").Append("\n");
                }
                if (addAssistantTurn) sb.Append("assistant: ");
                return sb.ToString();
            }

            LlamaChatMessage[] arr = new LlamaChatMessage[messages.Count];
            List<GCHandle> pinned = new List<GCHandle>();
            try
            {
                for (int i = 0; i < messages.Count; i++)
                {
                    byte[] rb = Encoding.UTF8.GetBytes(messages[i].Role ?? "user");
                    byte[] cb = Encoding.UTF8.GetBytes(messages[i].Content ?? "");
                    GCHandle rp = GCHandle.Alloc(rb, GCHandleType.Pinned);
                    GCHandle cp = GCHandle.Alloc(cb, GCHandleType.Pinned);
                    pinned.Add(rp);
                    pinned.Add(cp);
                    arr[i].Role = rp.AddrOfPinnedObject();
                    arr[i].Content = cp.AddrOfPinnedObject();
                }
                GCHandle ah = GCHandle.Alloc(arr, GCHandleType.Pinned);
                pinned.Add(ah);
                IntPtr chat = ah.AddrOfPinnedObject();
                IntPtr count = (IntPtr)messages.Count;
                int need = Llama.ChatApplyTemplate(template, chat, count, addAssistantTurn, IntPtr.Zero, 0);
                if (need <= 0) return "";
                byte[] buf = new byte[need + 8];
                GCHandle bh = GCHandle.Alloc(buf, GCHandleType.Pinned);
                try
                {
                    int got = Llama.ChatApplyTemplate(template, chat, count, addAssistantTurn,
                        bh.AddrOfPinnedObject(), buf.Length);
                    if (got <= 0) return "";
                    return Encoding.UTF8.GetString(buf, 0, got);
                }
                finally { bh.Free(); }
            }
            finally
            {
                for (int i = 0; i < pinned.Count; i++) pinned[i].Free();
            }
        }

        private static readonly string[] EndMarkers =
        {
            "<|im_end|>", "|im_end|",
            "<|im_start|>", "|im_start|",
            "<|endoftext|>", "|endoftext|",
            "<|eot_id|>", "|eot_id|",
            "<|end_of_turn|>",
            "</s>",
        };

        /// The sampler can write a template marker like <|im_end|> one phonetic
        /// character at a time; each character token is a perfectly ordinary
        /// vocabulary entry, so no EOG flag or eos-id comparison sees it. The
        /// moment the reply spells a marker anywhere, the reply is over:
        /// generation is usually derailed past that point. Cut at the marker
        /// and return true so the caller stops instead of streaming it or
        /// whatever follows it.
        private static bool MarkerCut(StringBuilder all)
        {
            string s = all.ToString();
            int cut = int.MaxValue;
            for (int m = 0; m < EndMarkers.Length; m++)
            {
                int at = s.IndexOf(EndMarkers[m], StringComparison.Ordinal);
                if (at >= 0 && at < cut) cut = at;
            }
            if (cut < int.MaxValue)
            {
                all.Length = cut;
                return true;
            }
            return false;
        }

        /// A reply can end with a half-written marker: the sampler started "<|im_end|>"
        /// and hit a real end-of-generation token before finishing ("... 4. |<").
        /// Strip while the tail spells the reverse of a marker prefix. A bare ">"
        /// or "|" that could be a real character (the end of HTML, a pipe glyph)
        /// is left alone unless it actually starts a marker.
        private static void TrimMarkerFragment(StringBuilder all)
        {
            byte[] rev = new byte[24];
            int n = 0;
            while (all.Length > 0 && n < rev.Length)
            {
                rev[n] = (byte)all[all.Length - 1];
                n++;
                bool ok = false;
                for (int m = 0; m < EndMarkers.Length; m++)
                {
                    if (PrefixMatchesReversed(EndMarkers[m], rev, n)) { ok = true; break; }
                }
                if (!ok) break;
                all.Length--;
            }
            while (all.Length > 0 && char.IsWhiteSpace(all[all.Length - 1])) all.Length--;
        }

        private static bool PrefixMatchesReversed(string marker, byte[] rev, int n)
        {
            if (n > marker.Length) return false;
            for (int i = 0; i < n; i++)
            {
                if ((byte)marker[i] != rev[n - 1 - i]) return false;
            }
            return true;
        }

        private string Run(int[] prompt, int maxTokens, double temperature, double topP,
            HttpCall call, Action<string> onToken)
        {
            StringBuilder all = new StringBuilder(512);
            IntPtr ctx = _ctx;
            IntPtr vocab = _vocab;
            bool sample = temperature > 0.01;
            int eosId = -1;
            try { eosId = Llama.VocabEos(vocab); }
            catch (Exception) { eosId = -1; }

            // Every request re-decodes its full prompt (the app frames all of an
            // operation's history into the text), so nothing from the previous
            // request belongs in the cache. Without this the KV positions keep
            // climbing until the context is full and every later call silently
            // returns nothing, which also made model answers drift as calls wore
            // on within a session.
            Llama.MemoryClear(Llama.GetMemory(ctx), false);

            GCHandle ph = GCHandle.Alloc(prompt, GCHandleType.Pinned);
            try
            {
                // A prompt longer than one batch must be split: llama_decode
                // asserts on n_tokens > n_batch (llama-context.cpp) rather than
                // handling it, which aborted the process once chat history grew
                // past 512 tokens. Positions are tracked automatically, so each
                // slice simply continues in the KV cache.
                IntPtr basePtr = ph.AddrOfPinnedObject();
                int step = (int)BatchSize;
                for (int at = 0; at < prompt.Length; at += step)
                {
                    int n = prompt.Length - at;
                    if (n > step) n = step;
                    LlamaBatch pb = Llama.BatchGetOne(
                        new IntPtr(basePtr.ToInt64() + (long)at * sizeof(int)), n);
                    if (Llama.Decode(ctx, pb) != 0) return all.ToString();
                }
            }
            finally { ph.Free(); }

            IntPtr chain = Llama.SamplerChainInit(Llama.SamplerChainDefaultParams());
            if (sample) Llama.SamplerChainAdd(chain, Llama.SamplerInitTemp((float)temperature));
            if (topP > 0.01 && topP < 1.0) Llama.SamplerChainAdd(chain, Llama.SamplerInitTopP((float)topP, (IntPtr)1));
            IntPtr pick = sample
                ? Llama.SamplerInitDist((uint)(Environment.TickCount ^ Thread.CurrentThread.ManagedThreadId))
                : Llama.SamplerInitGreedy();
            Llama.SamplerChainAdd(chain, pick);

            byte[] piece = new byte[PieceBuf];
            int[] single = new int[1];
            GCHandle bh = GCHandle.Alloc(piece, GCHandleType.Pinned);
            GCHandle sh = GCHandle.Alloc(single, GCHandleType.Pinned);
            try
            {
                int emitted = 0;
                while (emitted < maxTokens)
                {
                    if (call != null && call.Cancelled) break;
                    int id = Llama.SamplerSample(chain, ctx, -1);
                    if (id < 0) break;
                    if (Llama.VocabIsEog(vocab, id) || (eosId >= 0 && id == eosId)) break;
                    int len = Llama.TokenToPiece(vocab, id, bh.AddrOfPinnedObject(), PieceBuf, 0, false);
                    string text = "";
                    if (len > 0) text = Encoding.UTF8.GetString(piece, 0, len);
                    if (text.Length > 0)
                    {
                        all.Append(text);
                        if (MarkerCut(all)) break;
                        if (onToken != null) onToken(text);
                    }
                    emitted++;
                    single[0] = id;
                    LlamaBatch nb = Llama.BatchGetOne(sh.AddrOfPinnedObject(), 1);
                    if (Llama.Decode(ctx, nb) != 0) break;
                }
            }
            finally
            {
                bh.Free();
                sh.Free();
                // The chain owns the samplers added to it (chain_add transfers
                // ownership), so freeing pick separately would double-free it.
                Llama.SamplerFree(chain);
            }
            TrimMarkerFragment(all);
            return all.ToString();
        }
    }
}
