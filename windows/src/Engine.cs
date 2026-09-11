// The inference engine abstraction and discovery.
//
// The phone build loads a GGUF file into llama.cpp inside the process; the
// desktop build now does the same (the runtime travels inside the executable,
// see LocalEngine) or talks to whatever local inference server is already on
// the machine, and one engine is opt-in cloud. Either way the deterministic
// regex engine stays a first class result rather than an excuse.

using System;
using System.Collections.Generic;
using System.Net;

namespace ClipSyncAI
{
    internal abstract class InferenceEngine
    {
        public abstract string Kind { get; }
        public abstract string BaseUrl { get; }

        /// A quick liveness check. Returns false with a human readable reason,
        /// which is what Settings shows next to the engine row.
        public abstract bool Ping(out string error);

        public abstract List<string> Models();

        /// Runs a single prompt. When onToken is set the reply is streamed and
        /// each fragment is handed over as it arrives; the full text is returned
        /// either way.
        public abstract string Generate(string model, string system, string prompt,
            int maxTokens, double temperature, double topP, HttpCall call, Action<string> onToken);

        public abstract string Chat(string model, IList<ChatMessage> messages,
            int maxTokens, double temperature, double topP, HttpCall call, Action<string> onToken);

        /// Sends an image with a prompt, for the Capture tab when a vision model
        /// is available. Returns null when the engine cannot do it.
        public virtual string Vision(string model, string prompt, string base64Png,
            int maxTokens, HttpCall call)
        {
            return null;
        }

        public override string ToString() { return Kind + " at " + BaseUrl; }
    }

    /// What discovery found, in a form the UI can show without knowing how any
    /// of it works.
    internal sealed class EngineStatus
    {
        public bool Available;
        public string Kind = "Offline";
        public string BaseUrl = "";
        public string Detail = "Formatting runs on this machine with no model.";
        public List<string> Models = new List<string>();
    }

    internal static class EngineFactory
    {
        /// Ports probed at startup, in the order a machine is most likely to
        /// have them. Ollama first because it is the engine the phone build
        /// already speaks to.
        private static readonly string[] OllamaCandidates =
        {
            "http://127.0.0.1:11434",
        };

        private static readonly string[] OpenAiCandidates =
        {
            "http://127.0.0.1:1234",  // LM Studio
            "http://127.0.0.1:8080",  // llama.cpp server
            "http://127.0.0.1:1337",  // Jan
            "http://127.0.0.1:5000",  // text generation webui
            "http://127.0.0.1:8000",  // generic OpenAI compatible
        };

        public static InferenceEngine Build(AppSettings s)
        {
            if (s == null) return null;
            if (s.Engine == EngineKind.Ollama) return new OllamaEngine(s.OllamaUrl);
            if (s.Engine == EngineKind.OpenAiCompatible) return new OpenAiEngine(s.OpenAiUrl);
            if (s.Engine == EngineKind.Local) return new LocalEngine();
            if (s.Engine == EngineKind.Cloud) return new CloudEngine(s);
            return null;
        }

        /// Probes for a local server. Called on a worker thread at startup and
        /// whenever the user asks Settings to look again. When nothing answers,
        /// fail carries the configured engine's reason so the UI can say why.
        public static InferenceEngine Discover(AppSettings s, out string fail)
        {
            fail = null;
            string err = null;
            if (s == null) return null;

            // The embedded and cloud engines have no ports to probe. Build the
            // configured engine and adopt it only when it answers.
            if (s.Engine == EngineKind.Local || s.Engine == EngineKind.Cloud)
            {
                InferenceEngine fixedEngine = Build(s);
                if (fixedEngine != null && fixedEngine.Ping(out err)) return fixedEngine;
                fail = err;
                return null;
            }

            if (!s.AutoDiscoverEngine)
            {
                InferenceEngine chosen = Build(s);
                if (chosen != null && chosen.Ping(out err)) return chosen;
                fail = err;
                return null;
            }

            // A URL the user typed is tried before the defaults, because they
            // set it for a reason.
            if (s != null && s.Engine == EngineKind.Ollama && !IsCandidate(s.OllamaUrl, OllamaCandidates))
            {
                OllamaEngine e = new OllamaEngine(s.OllamaUrl);
                if (e.Ping(out err)) return e;
            }
            if (s != null && s.Engine == EngineKind.OpenAiCompatible && !IsCandidate(s.OpenAiUrl, OpenAiCandidates))
            {
                OpenAiEngine e = new OpenAiEngine(s.OpenAiUrl);
                if (e.Ping(out err)) return e;
            }

            for (int i = 0; i < OllamaCandidates.Length; i++)
            {
                OllamaEngine e = new OllamaEngine(OllamaCandidates[i]);
                if (e.Ping(out err)) return e;
            }
            for (int i = 0; i < OpenAiCandidates.Length; i++)
            {
                OpenAiEngine e = new OpenAiEngine(OpenAiCandidates[i]);
                if (e.Ping(out err)) return e;
            }
            return null;
        }

        private static bool IsCandidate(string url, string[] list)
        {
            if (string.IsNullOrEmpty(url)) return true;
            string t = url.TrimEnd('/');
            for (int i = 0; i < list.Length; i++)
            {
                if (string.Equals(list[i], t, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        public static EngineStatus StatusFor(InferenceEngine e)
        {
            EngineStatus st = new EngineStatus();
            if (e == null) return st;
            string err;
            if (!e.Ping(out err))
            {
                st.Detail = string.IsNullOrEmpty(err) ? "Not responding." : err;
                st.Kind = e.Kind;
                st.BaseUrl = e.BaseUrl;
                return st;
            }
            st.Available = true;
            st.Kind = e.Kind;
            st.BaseUrl = e.BaseUrl;
            try { st.Models = e.Models(); }
            catch (Exception) { st.Models = new List<string>(); }
            if (e is LocalEngine && st.Models.Count == 0)
            {
                st.Detail = "No models on this PC yet. Download or import one.";
            }
            else if (st.Models.Count == 1)
            {
                st.Detail = "1 model available.";
            }
            else
            {
                st.Detail = st.Models.Count + " models available.";
            }
            return st;
        }

        /// Turns a transport failure into something worth reading. A refused
        /// connection means no server, which is a different problem from a
        /// server that answered with an error.
        public static string Explain(Exception ex, string baseUrl)
        {
            WebException we = ex as WebException;
            if (we != null)
            {
                if (we.Status == WebExceptionStatus.ConnectFailure) return "Nothing is listening at " + baseUrl + ".";
                if (we.Status == WebExceptionStatus.Timeout) return "Timed out waiting for " + baseUrl + ".";
                if (we.Status == WebExceptionStatus.RequestCanceled) return "Stopped.";
                string body = Http.BodyOf(we);
                if (!string.IsNullOrEmpty(body))
                {
                    JVal j = JsonParser.Parse(body);
                    string msg = j == null ? null : j["error"].AsString(null);
                    if (msg == null && j != null) msg = j["error"]["message"].AsString(null);
                    if (!string.IsNullOrEmpty(msg)) return msg;
                    return body.Length > 200 ? body.Substring(0, 200) : body;
                }
                return we.Message;
            }
            return ex == null ? "Unknown error." : ex.Message;
        }
    }
}
