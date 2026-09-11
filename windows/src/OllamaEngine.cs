// The Ollama adapter.
//
// Same REST contract as lib/services/ollama_service.dart, so a model that works
// on the phone works here with no change on the server side. keep_alive is 300
// seconds to match: a model that unloads between two clipboard copies makes the
// second one feel broken.

using System;
using System.Collections.Generic;
using System.Text;

namespace ClipSyncAI
{
    internal sealed class OllamaEngine : InferenceEngine
    {
        private const int KeepAliveSeconds = 300;
        private const int PingTimeoutMs = 900;
        private const int ListTimeoutMs = 4000;
        private const int FirstTokenTimeoutMs = 120000;
        private const int IdleTimeoutMs = 120000;

        private readonly string _base;

        public OllamaEngine(string baseUrl)
        {
            _base = string.IsNullOrEmpty(baseUrl) ? "http://127.0.0.1:11434" : baseUrl.TrimEnd('/');
        }

        public override string Kind { get { return "Ollama"; } }
        public override string BaseUrl { get { return _base; } }

        public override bool Ping(out string error)
        {
            error = null;
            try
            {
                string body = Http.Get(_base + "/api/tags", PingTimeoutMs);
                JVal j = JsonParser.Parse(body);
                if (j == null) { error = "Answered, but not with JSON."; return false; }
                return true;
            }
            catch (Exception ex)
            {
                error = EngineFactory.Explain(ex, _base);
                return false;
            }
        }

        public override List<string> Models()
        {
            List<string> names = new List<string>();
            try
            {
                JVal j = JsonParser.Parse(Http.Get(_base + "/api/tags", ListTimeoutMs));
                if (j == null) return names;
                foreach (JVal m in j["models"].Items())
                {
                    string n = m["name"].AsString("");
                    if (n.Length > 0) names.Add(n);
                }
            }
            catch (Exception ex)
            {
                Paths.Log("ollama tags", ex);
            }
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        private static JVal Options(int maxTokens, double temperature, double topP)
        {
            return JVal.Object()
                .Set("temperature", temperature)
                .Set("top_p", topP)
                .Set("num_predict", (long)maxTokens);
        }

        public override string Generate(string model, string system, string prompt,
            int maxTokens, double temperature, double topP, HttpCall call, Action<string> onToken)
        {
            JVal body = JVal.Object()
                .Set("model", model)
                .Set("prompt", prompt ?? "")
                .Set("stream", onToken != null)
                .Set("keep_alive", (long)KeepAliveSeconds)
                .Set("options", Options(maxTokens, temperature, topP));
            if (!string.IsNullOrEmpty(system)) body.Set("system", system);

            if (onToken == null)
            {
                JVal j = JsonParser.Parse(Http.PostJson(_base + "/api/generate", JsonWriter.Write(body), IdleTimeoutMs));
                return j == null ? "" : j["response"].AsString("");
            }

            StringBuilder all = new StringBuilder(512);
            Http.PostLines(_base + "/api/generate", JsonWriter.Write(body),
                FirstTokenTimeoutMs, IdleTimeoutMs, call, delegate(string line)
            {
                JVal j = JsonParser.Parse(line);
                if (j == null) return;
                string piece = j["response"].AsString("");
                if (piece.Length > 0)
                {
                    all.Append(piece);
                    onToken(piece);
                }
            });
            return all.ToString();
        }

        public override string Chat(string model, IList<ChatMessage> messages,
            int maxTokens, double temperature, double topP, HttpCall call, Action<string> onToken)
        {
            JVal msgs = JVal.Array();
            for (int i = 0; i < messages.Count; i++)
            {
                msgs.Add(JVal.Object()
                    .Set("role", messages[i].Role)
                    .Set("content", messages[i].Content ?? ""));
            }
            JVal body = JVal.Object()
                .Set("model", model)
                .Set("messages", msgs)
                .Set("stream", onToken != null)
                .Set("keep_alive", (long)KeepAliveSeconds)
                .Set("options", Options(maxTokens, temperature, topP));

            if (onToken == null)
            {
                JVal j = JsonParser.Parse(Http.PostJson(_base + "/api/chat", JsonWriter.Write(body), IdleTimeoutMs));
                return j == null ? "" : j["message"]["content"].AsString("");
            }

            StringBuilder all = new StringBuilder(512);
            Http.PostLines(_base + "/api/chat", JsonWriter.Write(body),
                FirstTokenTimeoutMs, IdleTimeoutMs, call, delegate(string line)
            {
                JVal j = JsonParser.Parse(line);
                if (j == null) return;
                string piece = j["message"]["content"].AsString("");
                if (piece.Length > 0)
                {
                    all.Append(piece);
                    onToken(piece);
                }
            });
            return all.ToString();
        }

        /// Vision goes through /api/generate with the image attached, which is
        /// how Ollama takes pictures for a multimodal model.
        public override string Vision(string model, string prompt, string base64Png,
            int maxTokens, HttpCall call)
        {
            JVal images = JVal.Array().Add(JVal.Of(base64Png));
            JVal body = JVal.Object()
                .Set("model", model)
                .Set("prompt", prompt ?? "")
                .Set("images", images)
                .Set("stream", false)
                .Set("keep_alive", (long)KeepAliveSeconds)
                .Set("options", Options(maxTokens, 0.2, 0.9));
            JVal j = JsonParser.Parse(Http.PostJson(_base + "/api/generate",
                JsonWriter.Write(body), IdleTimeoutMs));
            return j == null ? null : j["response"].AsString("");
        }
    }
}
