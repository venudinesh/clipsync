// The OpenAI compatible adapter.
//
// One shape covers LM Studio, the llama.cpp server, Jan and anything else that
// exposes /v1/chat/completions, which is most of what a Windows machine is
// likely to already have running. Streaming arrives as server sent events, so
// the reader strips the "data:" prefix and stops at the sentinel.

using System;
using System.Collections.Generic;
using System.Text;

namespace ClipSyncAI
{
    internal sealed class OpenAiEngine : InferenceEngine
    {
        private const int PingTimeoutMs = 900;
        private const int ListTimeoutMs = 4000;
        private const int FirstTokenTimeoutMs = 120000;
        private const int IdleTimeoutMs = 120000;

        private readonly string _base;

        public OpenAiEngine(string baseUrl)
        {
            _base = string.IsNullOrEmpty(baseUrl) ? "http://127.0.0.1:1234" : baseUrl.TrimEnd('/');
        }

        public override string Kind { get { return "OpenAI compatible"; } }
        public override string BaseUrl { get { return _base; } }

        public override bool Ping(out string error)
        {
            error = null;
            try
            {
                JVal j = JsonParser.Parse(Http.Get(_base + "/v1/models", PingTimeoutMs));
                if (j == null) { error = "Answered, but not with JSON."; return false; }
                // A server that answers /v1/models with something that has no
                // data array is probably not the API we think it is.
                if (j["data"].Kind != JKind.Arr) { error = "Not an OpenAI style model list."; return false; }
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
                JVal j = JsonParser.Parse(Http.Get(_base + "/v1/models", ListTimeoutMs));
                if (j == null) return names;
                foreach (JVal m in j["data"].Items())
                {
                    string n = m["id"].AsString("");
                    if (n.Length > 0) names.Add(n);
                }
            }
            catch (Exception ex)
            {
                Paths.Log("openai models", ex);
            }
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
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
                .Set("temperature", temperature)
                .Set("top_p", topP)
                .Set("max_tokens", (long)maxTokens)
                .Set("stream", onToken != null);

            string url = _base + "/v1/chat/completions";
            if (onToken == null)
            {
                JVal j = JsonParser.Parse(Http.PostJson(url, JsonWriter.Write(body), IdleTimeoutMs));
                if (j == null) return "";
                return j["choices"].At(0)["message"]["content"].AsString("");
            }

            StringBuilder all = new StringBuilder(512);
            Http.PostLines(url, JsonWriter.Write(body),
                FirstTokenTimeoutMs, IdleTimeoutMs, call, delegate(string line)
            {
                string payload = line;
                if (payload.StartsWith("data:", StringComparison.Ordinal))
                {
                    payload = payload.Substring(5).Trim();
                }
                if (payload.Length == 0) return;
                if (payload == "[DONE]") return;
                JVal j = JsonParser.Parse(payload);
                if (j == null) return;
                JVal choice = j["choices"].At(0);
                string piece = choice["delta"]["content"].AsString("");
                // A few servers send the whole message rather than a delta on
                // the final event. Taking both keeps the last words of a reply.
                if (piece.Length == 0) piece = choice["message"]["content"].AsString("");
                if (piece.Length > 0)
                {
                    all.Append(piece);
                    onToken(piece);
                }
            });
            return all.ToString();
        }

        /// Vision uses the content parts form, with the image inline as a data
        /// URL. Servers that do not support it answer with an error, which the
        /// caller surfaces rather than swallowing.
        public override string Vision(string model, string prompt, string base64Png,
            int maxTokens, HttpCall call)
        {
            JVal parts = JVal.Array()
                .Add(JVal.Object().Set("type", "text").Set("text", prompt ?? ""))
                .Add(JVal.Object()
                    .Set("type", "image_url")
                    .Set("image_url", JVal.Object()
                        .Set("url", "data:image/png;base64," + base64Png)));
            JVal msgs = JVal.Array().Add(JVal.Object().Set("role", "user").Set("content", parts));
            JVal body = JVal.Object()
                .Set("model", model)
                .Set("messages", msgs)
                .Set("temperature", 0.2)
                .Set("max_tokens", (long)maxTokens)
                .Set("stream", false);
            JVal j = JsonParser.Parse(Http.PostJson(_base + "/v1/chat/completions",
                JsonWriter.Write(body), IdleTimeoutMs));
            if (j == null) return null;
            return j["choices"].At(0)["message"]["content"].AsString("");
        }
    }
}
