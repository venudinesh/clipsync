// The cloud AI engine: an API key talks to a hosted provider.
//
// This is the one engine that sends your prompts somewhere else, which is why
// it is opt-in twice: an API key must be present and the one-time allowance in
// Settings must have been granted. Until both hold, every call fails with a
// readable reason and the clipboard pass falls back to the regex engine, so a
// half-configured cloud account can never quietly leak text.
//
// Two wire shapes cover every provider. OpenAI's /v1/chat/completions contract
// (OpenAI, Groq, Mistral, xAI, OpenRouter and any custom endpoint) is the same
// as the local OpenAI adapter, plus an Authorization header. Anthropic speaks
// /v1/messages with its own headers, and Gemini streams from
// streamGenerateContent with the key on the query string.

using System;
using System.Collections.Generic;
using System.Text;

namespace ClipSyncAI
{
    internal sealed class CloudEngine : InferenceEngine
    {
        private const int PingTimeoutMs = 8000;
        private const int ListTimeoutMs = 10000;
        private const int FirstTokenTimeoutMs = 120000;
        private const int IdleTimeoutMs = 120000;

        private readonly AppSettings _s;

        public CloudEngine(AppSettings s)
        {
            _s = s;
        }

        public override string Kind { get { return "Cloud AI"; } }

        /// The provider name rather than a URL: it is what the masthead and the
        /// engine row want to say, and the address is a detail the key row owns.
        public override string BaseUrl { get { return ProviderName(_s.CloudProvider); } }

        public static string ProviderName(CloudProviderKind p)
        {
            switch (p)
            {
                case CloudProviderKind.OpenAi: return "OpenAI";
                case CloudProviderKind.Anthropic: return "Anthropic";
                case CloudProviderKind.Gemini: return "Google Gemini";
                case CloudProviderKind.Groq: return "Groq";
                case CloudProviderKind.Mistral: return "Mistral";
                case CloudProviderKind.Xai: return "xAI";
                case CloudProviderKind.OpenRouter: return "OpenRouter";
                default: return "Custom endpoint";
            }
        }

        public static string ProviderBase(CloudProviderKind p)
        {
            switch (p)
            {
                case CloudProviderKind.OpenAi: return "https://api.openai.com/v1";
                case CloudProviderKind.Anthropic: return "https://api.anthropic.com/v1";
                case CloudProviderKind.Gemini: return "https://generativelanguage.googleapis.com";
                case CloudProviderKind.Groq: return "https://api.groq.com/openai/v1";
                case CloudProviderKind.Mistral: return "https://api.mistral.ai/v1";
                case CloudProviderKind.Xai: return "https://api.x.ai/v1";
                case CloudProviderKind.OpenRouter: return "https://openrouter.ai/api/v1";
                default: return "";
            }
        }

        private string Base
        {
            get
            {
                if (_s.CloudProvider == CloudProviderKind.Custom)
                {
                    string u = _s.CloudBaseUrl;
                    return string.IsNullOrEmpty(u) ? "" : u.TrimEnd('/');
                }
                return ProviderBase(_s.CloudProvider);
            }
        }

        private bool GeminiApi { get { return _s.CloudProvider == CloudProviderKind.Gemini; } }
        private bool AnthropicApi { get { return _s.CloudProvider == CloudProviderKind.Anthropic; } }

        private string RequireKey()
        {
            string key = CloudKeys.Load();
            if (key.Length == 0)
            {
                throw new InvalidOperationException(
                    "No API key set. Add one in Settings under Cloud API.");
            }
            return key;
        }

        private static Dictionary<string, string> Headers(string key)
        {
            Dictionary<string, string> h = new Dictionary<string, string>();
            h["Authorization"] = "Bearer " + key;
            return h;
        }

        public override bool Ping(out string error)
        {
            error = null;
            if (_s.CloudProvider == CloudProviderKind.Custom && Base.Length == 0)
            {
                error = "Set the endpoint URL under Cloud API.";
                return false;
            }
            if (!CloudKeys.HasKey) { error = "No API key set."; return false; }
            try
            {
                if (GeminiApi) return GeminiPing(out error);
                if (AnthropicApi) return AnthropicPing(out error);
                string body = Http.GetRemote(Base + "/models",
                    Headers(CloudKeys.Load()), PingTimeoutMs);
                JVal j = JsonParser.Parse(body);
                if (j == null || j["data"].Kind != JKind.Arr)
                {
                    error = "Answered, but not with an OpenAI style model list.";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = RemoteExplain(ex, Base);
                return false;
            }
        }

        private bool GeminiPing(out string error)
        {
            error = null;
            string key = CloudKeys.Load();
            string body = Http.GetRemote(
                Base + "/v1beta/models?key=" + Uri.EscapeDataString(key), null, PingTimeoutMs);
            JVal j = JsonParser.Parse(body);
            if (j == null || j["models"].Kind != JKind.Arr)
            {
                error = "Answered, but not with a Gemini model list.";
                return false;
            }
            return true;
        }

        private bool AnthropicPing(out string error)
        {
            error = null;
            Dictionary<string, string> h = new Dictionary<string, string>();
            h["x-api-key"] = CloudKeys.Load();
            h["anthropic-version"] = "2023-06-01";
            string body = Http.GetRemote(Base + "/models", h, PingTimeoutMs);
            JVal j = JsonParser.Parse(body);
            if (j == null || j["data"].Kind != JKind.Arr)
            {
                error = "Answered, but not with an Anthropic model list.";
                return false;
            }
            return true;
        }

        private DateTime _cacheAt;
        private List<string> _cache;

        public override List<string> Models()
        {
            // The list is fetched at most once a minute; the pickers ask for it
            // often while settings is open.
            if (_cache != null && (DateTime.Now - _cacheAt).TotalSeconds < 60) return _cache;
            List<string> names = new List<string>();
            try
            {
                if (GeminiApi)
                {
                    JVal j = JsonParser.Parse(Http.GetRemote(Base + "/v1beta/models?key=" +
                        Uri.EscapeDataString(CloudKeys.Load()), null, ListTimeoutMs));
                    if (j != null)
                    {
                        foreach (JVal m in j["models"].Items())
                        {
                            string n = m["name"].AsString("");
                            if (n.StartsWith("models/", StringComparison.Ordinal)) n = n.Substring(7);
                            if (n.Length > 0) names.Add(n);
                        }
                    }
                }
                else if (AnthropicApi)
                {
                    Dictionary<string, string> h = new Dictionary<string, string>();
                    h["x-api-key"] = CloudKeys.Load();
                    h["anthropic-version"] = "2023-06-01";
                    JVal j = JsonParser.Parse(Http.GetRemote(Base + "/models", h, ListTimeoutMs));
                    if (j != null)
                    {
                        foreach (JVal m in j["data"].Items())
                        {
                            string n = m["id"].AsString("");
                            if (n.Length > 0) names.Add(n);
                        }
                    }
                }
                else
                {
                    JVal j = JsonParser.Parse(Http.GetRemote(Base + "/models",
                        Headers(CloudKeys.Load()), ListTimeoutMs));
                    if (j != null)
                    {
                        foreach (JVal m in j["data"].Items())
                        {
                            string n = m["id"].AsString("");
                            if (n.Length > 0) names.Add(n);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Paths.Log("cloud models", ex);
            }
            names.Sort(StringComparer.OrdinalIgnoreCase);
            _cache = names;
            _cacheAt = DateTime.Now;
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
            if (!_s.CloudApproved)
            {
                throw new InvalidOperationException(
                    "Cloud AI is not allowed yet. Open Settings, choose Cloud API and " +
                    "accept the notice there.");
            }
            if (string.IsNullOrEmpty(model))
            {
                throw new InvalidOperationException("No cloud model chosen in Settings.");
            }
            string key = RequireKey();
            if (GeminiApi) return GeminiChat(model, messages, maxTokens, temperature, call, onToken);
            if (AnthropicApi) return AnthropicChat(model, messages, maxTokens, temperature, call, onToken);
            return OpenAiChat(model, messages, maxTokens, temperature, topP, call, onToken);
        }

        private string OpenAiChat(string model, IList<ChatMessage> messages,
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

            string url = Base + "/chat/completions";
            Dictionary<string, string> h = Headers(CloudKeys.Load());
            if (onToken == null)
            {
                JVal j = JsonParser.Parse(Http.PostRemote(url, JsonWriter.Write(body), h, IdleTimeoutMs));
                if (j == null) return "";
                return j["choices"].At(0)["message"]["content"].AsString("");
            }

            StringBuilder all = new StringBuilder(512);
            Http.PostRemoteLines(url, JsonWriter.Write(body), h,
                FirstTokenTimeoutMs, IdleTimeoutMs, call, delegate(string line)
            {
                string payload = line;
                if (payload.StartsWith("data:", StringComparison.Ordinal))
                {
                    payload = payload.Substring(5).Trim();
                }
                if (payload.Length == 0 || payload == "[DONE]") return;
                JVal j = JsonParser.Parse(payload);
                if (j == null) return;
                string piece = j["choices"].At(0)["delta"]["content"].AsString("");
                if (piece.Length == 0) piece = j["choices"].At(0)["message"]["content"].AsString("");
                if (piece.Length > 0)
                {
                    all.Append(piece);
                    if (onToken != null) onToken(piece);
                }
            });
            return all.ToString();
        }

        private string AnthropicChat(string model, IList<ChatMessage> messages,
            int maxTokens, double temperature, HttpCall call, Action<string> onToken)
        {
            JVal msgs = JVal.Array();
            string system = "";
            for (int i = 0; i < messages.Count; i++)
            {
                if (messages[i].Role == "system")
                {
                    system = (system.Length == 0 ? "" : system + "\n\n") + (messages[i].Content ?? "");
                }
                else
                {
                    msgs.Add(JVal.Object()
                        .Set("role", messages[i].Role == "assistant" ? "assistant" : "user")
                        .Set("content", messages[i].Content ?? ""));
                }
            }
            JVal body = JVal.Object()
                .Set("model", model)
                .Set("max_tokens", (long)Math.Max(maxTokens, 64))
                .Set("temperature", temperature);
            if (system.Length > 0) body.Set("system", system);
            body.Set("messages", msgs);
            body.Set("stream", onToken != null);

            Dictionary<string, string> h = new Dictionary<string, string>();
            h["x-api-key"] = CloudKeys.Load();
            h["anthropic-version"] = "2023-06-01";
            h["content-type"] = "application/json";

            string url = Base + "/messages";
            if (onToken == null)
            {
                JVal j = JsonParser.Parse(Http.PostRemote(url, JsonWriter.Write(body), h, IdleTimeoutMs));
                if (j == null) return "";
                StringBuilder all = new StringBuilder();
                foreach (JVal c in j["content"].Items())
                {
                    all.Append(c["text"].AsString(""));
                }
                return all.ToString();
            }

            StringBuilder all2 = new StringBuilder(512);
            Http.PostRemoteLines(url, JsonWriter.Write(body), h,
                FirstTokenTimeoutMs, IdleTimeoutMs, call, delegate(string line)
            {
                string payload = line;
                if (payload.StartsWith("data:", StringComparison.Ordinal))
                {
                    payload = payload.Substring(5).Trim();
                }
                if (payload.Length == 0) return;
                JVal j = JsonParser.Parse(payload);
                if (j == null) return;
                string t = j["type"].AsString("");
                if (t != "content_block_delta") return;
                string piece = j["delta"]["text"].AsString("");
                if (piece.Length > 0)
                {
                    all2.Append(piece);
                    if (onToken != null) onToken(piece);
                }
            });
            return all2.ToString();
        }

        private string GeminiChat(string model, IList<ChatMessage> messages,
            int maxTokens, double temperature, HttpCall call, Action<string> onToken)
        {
            JVal contents = JVal.Array();
            string system = "";
            for (int i = 0; i < messages.Count; i++)
            {
                string role = messages[i].Role == "assistant" ? "model" : "user";
                if (messages[i].Role == "system")
                {
                    system = (system.Length == 0 ? "" : system + "\n\n") + (messages[i].Content ?? "");
                }
                else
                {
                    JVal part = JVal.Object()
                        .Set("text", messages[i].Content ?? "");
                    contents.Add(JVal.Object().Set("role", role).Set("parts", JVal.Array().Add(part)));
                }
            }
            JVal body = JVal.Object()
                .Set("contents", contents)
                .Set("generationConfig", JVal.Object()
                    .Set("maxOutputTokens", (long)maxTokens)
                    .Set("temperature", temperature));
            if (system.Length > 0)
            {
                body.Set("systemInstruction", JVal.Object()
                    .Set("parts", JVal.Array().Add(JVal.Object().Set("text", system))));
            }

            string key = Uri.EscapeDataString(CloudKeys.Load());
            string url = Base + "/v1beta/models/" + model + ":streamGenerateContent?key=" + key;
            if (onToken == null)
            {
                JVal j = JsonParser.Parse(Http.PostRemote(url, JsonWriter.Write(body), null, IdleTimeoutMs));
                if (j == null) return "";
                return j["candidates"].At(0)["content"]["parts"].At(0)["text"].AsString("");
            }

            StringBuilder all = new StringBuilder(512);
            Http.PostRemoteLines(url + "&alt=sse", JsonWriter.Write(body), null,
                FirstTokenTimeoutMs, IdleTimeoutMs, call, delegate(string line)
            {
                string payload = line;
                if (payload.StartsWith("data:", StringComparison.Ordinal))
                {
                    payload = payload.Substring(5).Trim();
                }
                if (payload.Length == 0) return;
                JVal j = JsonParser.Parse(payload);
                if (j == null) return;
                foreach (JVal p in j["candidates"].At(0)["content"]["parts"].Items())
                {
                    string piece = p["text"].AsString("");
                    if (piece.Length > 0)
                    {
                        all.Append(piece);
                        if (onToken != null) onToken(piece);
                    }
                }
            });
            return all.ToString();
        }

        /// Vision for the Capture page. Each provider takes an image a
        /// different way; a provider without a vision model answers with its
        /// own error, which the caller surfaces.
        public override string Vision(string model, string prompt, string base64Png,
            int maxTokens, HttpCall call)
        {
            if (!_s.CloudApproved)
            {
                throw new InvalidOperationException(
                    "Cloud AI is not allowed yet. Open Settings, choose Cloud API and " +
                    "accept the notice there.");
            }
            string key = RequireKey();
            if (GeminiApi)
            {
                JVal parts = JVal.Array()
                    .Add(JVal.Object().Set("inline_data", JVal.Object()
                        .Set("mime_type", "image/png")
                        .Set("data", base64Png)))
                    .Add(JVal.Object().Set("text", prompt ?? ""));
                JVal body = JVal.Object().Set("contents", JVal.Array()
                    .Add(JVal.Object().Set("role", "user").Set("parts", parts)));
                JVal j = JsonParser.Parse(Http.PostRemote(Base + "/v1beta/models/" + model +
                    ":generateContent?key=" + Uri.EscapeDataString(key),
                    JsonWriter.Write(body), null, IdleTimeoutMs));
                return j == null ? null : j["candidates"].At(0)["content"]["parts"].At(0)["text"].AsString("");
            }
            if (AnthropicApi)
            {
                JVal content = JVal.Array()
                    .Add(JVal.Object()
                        .Set("type", "image")
                        .Set("source", JVal.Object()
                            .Set("type", "base64")
                            .Set("media_type", "image/png")
                            .Set("data", base64Png)))
                    .Add(JVal.Object().Set("type", "text").Set("text", prompt ?? ""));
                JVal body = JVal.Object()
                    .Set("model", model)
                    .Set("max_tokens", (long)Math.Max(maxTokens, 64))
                    .Set("messages", JVal.Array()
                        .Add(JVal.Object().Set("role", "user").Set("content", content)));
                Dictionary<string, string> h = new Dictionary<string, string>();
                h["x-api-key"] = key;
                h["anthropic-version"] = "2023-06-01";
                h["content-type"] = "application/json";
                JVal j = JsonParser.Parse(Http.PostRemote(Base + "/messages",
                    JsonWriter.Write(body), h, IdleTimeoutMs));
                if (j == null) return null;
                StringBuilder all = new StringBuilder();
                foreach (JVal c in j["content"].Items()) all.Append(c["text"].AsString(""));
                return all.ToString();
            }
            JVal parts2 = JVal.Array()
                .Add(JVal.Object().Set("type", "text").Set("text", prompt ?? ""))
                .Add(JVal.Object()
                    .Set("type", "image_url")
                    .Set("image_url", JVal.Object()
                        .Set("url", "data:image/png;base64," + base64Png)));
            JVal body2 = JVal.Object()
                .Set("model", model)
                .Set("messages", JVal.Array()
                    .Add(JVal.Object().Set("role", "user").Set("content", parts2)))
                .Set("temperature", 0.2)
                .Set("max_tokens", (long)maxTokens)
                .Set("stream", false);
            JVal j2 = JsonParser.Parse(Http.PostRemote(Base + "/chat/completions",
                JsonWriter.Write(body2), Headers(key), IdleTimeoutMs));
            return j2 == null ? null : j2["choices"].At(0)["message"]["content"].AsString("");
        }

        /// Reads a transport failure and the provider's error body, which is
        /// where a bad key or a missing model explains itself.
        private static string RemoteExplain(Exception ex, string baseUrl)
        {
            System.Net.WebException we = ex as System.Net.WebException;
            if (we != null)
            {
                if (we.Status == System.Net.WebExceptionStatus.ConnectFailure)
                {
                    return "Could not reach " + baseUrl + ".";
                }
                if (we.Status == System.Net.WebExceptionStatus.Timeout) return "Timed out waiting for " + baseUrl + ".";
                if (we.Status == System.Net.WebExceptionStatus.RequestCanceled) return "Stopped.";
                string body = Http.BodyOf(we);
                if (!string.IsNullOrEmpty(body))
                {
                    JVal j = JsonParser.Parse(body);
                    if (j != null)
                    {
                        string msg = j["error"]["message"].AsString(null);
                        if (msg == null) msg = j["message"].AsString(null);
                        if (!string.IsNullOrEmpty(msg)) return msg;
                    }
                    return body.Length > 200 ? body.Substring(0, 200) : body;
                }
                return we.Message;
            }
            return ex == null ? "Unknown error." : ex.Message;
        }
    }
}
