// The processing facade.
//
// The port of OllamaClipProcessor: try the local engine, fall back to the regex
// engine, and never return nothing. Callers in the UI hold one of these and do
// not care which path produced the text.

using System;
using System.Collections.Generic;

namespace ClipSyncAI
{
    internal sealed class Ai
    {
        private readonly object _gate = new object();
        private InferenceEngine _engine;
        private string _model = AppDefaults.DefaultModel;

        public InferenceEngine Engine
        {
            get { lock (_gate) return _engine; }
            set { lock (_gate) _engine = value; }
        }

        public string Model
        {
            get { lock (_gate) return _model; }
            set { lock (_gate) _model = string.IsNullOrEmpty(value) ? AppDefaults.DefaultModel : value; }
        }

        public bool Ready { get { return Engine != null; } }

        /// The clipboard pass. Falls through to the regex engine on any failure,
        /// which is what makes a missing or crashed server a non event.
        public string Process(string rawText)
        {
            if (rawText == null || rawText.Trim().Length == 0) return "";
            InferenceEngine e = Engine;
            if (e != null)
            {
                try
                {
                    string result = e.Generate(Model, Prompts.System, Prompts.Build(rawText),
                        Prompts.ProcessMaxTokens, Prompts.ProcessTemperature, Prompts.ProcessTopP,
                        null, null);
                    if (result != null && result.Trim().Length > 0) return result.Trim();
                }
                catch (Exception ex)
                {
                    Paths.Log("process via engine", ex);
                }
            }
            return ClipRegex.Process(rawText);
        }

        /// Streams the clipboard pass. onToken receives fragments as they
        /// arrive; the return value is the finished text. When there is no
        /// engine the regex result is handed over in one piece, so a caller can
        /// use the same code path either way.
        public string ProcessStream(string rawText, HttpCall call, Action<string> onToken)
        {
            if (rawText == null || rawText.Trim().Length == 0) return "";
            InferenceEngine e = Engine;
            if (e != null)
            {
                try
                {
                    string result = e.Generate(Model, Prompts.System, Prompts.Build(rawText),
                        Prompts.ProcessMaxTokens, Prompts.ProcessTemperature, Prompts.ProcessTopP,
                        call, onToken);
                    if (result != null && result.Trim().Length > 0) return result.Trim();
                }
                catch (Exception ex)
                {
                    if (call != null && call.Cancelled) return null;
                    Paths.Log("process stream", ex);
                }
            }
            string fallback = ClipRegex.Process(rawText);
            if (onToken != null && fallback.Length > 0) onToken(fallback);
            return fallback;
        }

        /// An arbitrary instruction, without the clipboard wrapper. Returns null
        /// when there is no engine, so a caller can say so plainly rather than
        /// presenting reformatted text as if it were an answer.
        public string Instruct(string prompt, HttpCall call, Action<string> onToken)
        {
            if (prompt == null || prompt.Trim().Length == 0) return null;
            InferenceEngine e = Engine;
            if (e == null) return null;
            try
            {
                string result = e.Generate(Model, Prompts.System, prompt,
                    Prompts.InstructMaxTokens, Prompts.InstructTemperature, Prompts.InstructTopP,
                    call, onToken);
                if (result == null) return null;
                string t = result.Trim();
                return t.Length == 0 ? null : t;
            }
            catch (Exception ex)
            {
                if (call != null && call.Cancelled) return null;
                Paths.Log("instruct", ex);
                throw;
            }
        }

        /// A chat turn. The system prompt is prepended once, ahead of the stored
        /// transcript, so an old conversation does not accumulate copies of it.
        /// The temperature is the caller's because the chat page lets the reader
        /// choose how freely the model answers.
        public string Reply(IList<ChatMessage> transcript, double temperature,
            HttpCall call, Action<string> onToken)
        {
            InferenceEngine e = Engine;
            if (e == null) return null;
            List<ChatMessage> msgs = new List<ChatMessage>(transcript.Count + 1);
            msgs.Add(new ChatMessage("system", Prompts.Chat));
            for (int i = 0; i < transcript.Count; i++)
            {
                if (transcript[i].Role == "system") continue;
                msgs.Add(transcript[i]);
            }
            string result = e.Chat(Model, msgs, Prompts.ChatMaxTokens,
                temperature, Prompts.ChatTopP, call, onToken);
            if (result == null) return null;
            string t = result.Trim();
            return t.Length == 0 ? null : t;
        }

        /// A picture and a question about it. Returns null when there is no engine
        /// or no vision model chosen, so the Capture page can say which of the two
        /// is missing rather than reporting an empty read.
        public string See(string model, string prompt, string base64Png, HttpCall call)
        {
            InferenceEngine e = Engine;
            if (e == null || string.IsNullOrEmpty(model) || string.IsNullOrEmpty(base64Png))
            {
                return null;
            }
            try
            {
                string result = e.Vision(model, prompt, base64Png, Prompts.InstructMaxTokens, call);
                if (result == null) return null;
                string t = result.Trim();
                return t.Length == 0 ? null : t;
            }
            catch (Exception ex)
            {
                if (call != null && call.Cancelled) return null;
                Paths.Log("vision", ex);
                throw;
            }
        }

        /// Picks a sensible model out of what the engine reports. Preference
        /// goes to the configured name, then to anything small, because the
        /// clipboard pass is a reformat and does not need a large model.
        public static string ChooseModel(List<string> available, string preferred)
        {
            if (available == null || available.Count == 0) return preferred;
            if (!string.IsNullOrEmpty(preferred))
            {
                for (int i = 0; i < available.Count; i++)
                {
                    if (string.Equals(available[i], preferred, StringComparison.OrdinalIgnoreCase)) return available[i];
                }
                for (int i = 0; i < available.Count; i++)
                {
                    if (available[i].StartsWith(preferred, StringComparison.OrdinalIgnoreCase)) return available[i];
                }
            }
            string[] small = { "smollm", "qwen2.5:0.5b", "qwen2:0.5b", "tinyllama", "gemma:2b", "phi3:mini", "llama3.2:1b" };
            for (int s = 0; s < small.Length; s++)
            {
                for (int i = 0; i < available.Count; i++)
                {
                    if (available[i].IndexOf(small[s], StringComparison.OrdinalIgnoreCase) >= 0) return available[i];
                }
            }
            return available[0];
        }
    }
}
