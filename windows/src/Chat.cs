// Chat records. A session is a title plus an ordered transcript; the model name
// is stored per session so reopening an old conversation does not silently
// switch which model answers it.

using System;
using System.Collections.Generic;

namespace ClipSyncAI
{
    internal sealed class ChatMessage
    {
        public string Role;   // "user", "assistant" or "system"
        public string Content;
        public DateTime At;

        /// What the reader sees, when that is not what the model was sent. A
        /// question with a document folded into it shows the sentence that was
        /// typed rather than five thousand characters of the document.
        public string Shown;

        /// A line under the words naming what came with the message.
        public string Note;

        public ChatMessage() { Role = "user"; Content = ""; At = DateTime.Now; Shown = ""; Note = ""; }

        public ChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
            At = DateTime.Now;
            Shown = "";
            Note = "";
        }

        public JVal ToJson()
        {
            JVal o = JVal.Object()
                .Set("role", Role)
                .Set("content", Content)
                .Set("at", Clock.ToMs(At));
            if (!string.IsNullOrEmpty(Shown)) o.Set("shown", Shown);
            if (!string.IsNullOrEmpty(Note)) o.Set("note", Note);
            return o;
        }

        public static ChatMessage FromJson(JVal j)
        {
            ChatMessage m = new ChatMessage();
            m.Role = j["role"].AsString("user");
            m.Content = j["content"].AsString("");
            m.Shown = j["shown"].AsString("");
            m.Note = j["note"].AsString("");
            long ms = j["at"].AsLong(0);
            m.At = ms > 0 ? Clock.FromMs(ms) : DateTime.Now;
            return m;
        }
    }

    internal sealed class ChatSession
    {
        public string Id;
        public string Title;
        public string Model;
        public List<ChatMessage> Messages;
        public DateTime CreatedAt;
        public DateTime UpdatedAt;

        /// Kept above the rest of the history list. A conversation you keep
        /// coming back to should not sink as newer ones are had.
        public bool IsPinned;

        public ChatSession()
        {
            Id = Ids.New();
            Title = "New chat";
            Model = "";
            Messages = new List<ChatMessage>();
            CreatedAt = DateTime.Now;
            UpdatedAt = CreatedAt;
        }

        /// The first thing a person types is what they will look for later, so
        /// it becomes the title. Trimmed at a word boundary rather than mid
        /// word, which is the difference between a label and a truncation.
        public void TitleFromFirstMessage()
        {
            for (int i = 0; i < Messages.Count; i++)
            {
                if (Messages[i].Role != "user") continue;
                string s = (Messages[i].Content ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
                while (s.Contains("  ")) s = s.Replace("  ", " ");
                if (s.Length == 0) return;
                if (s.Length > 42)
                {
                    int cut = s.LastIndexOf(' ', 41);
                    s = cut > 18 ? s.Substring(0, cut) : s.Substring(0, 41);
                    s = s.TrimEnd() + "...";
                }
                Title = s;
                return;
            }
        }

        public JVal ToJson()
        {
            JVal msgs = JVal.Array();
            for (int i = 0; i < Messages.Count; i++) msgs.Add(Messages[i].ToJson());
            return JVal.Object()
                .Set("id", Id)
                .Set("title", Title)
                .Set("model", Model)
                .Set("messages", msgs)
                .Set("createdAt", Clock.ToMs(CreatedAt))
                .Set("updatedAt", Clock.ToMs(UpdatedAt))
                .Set("isPinned", IsPinned);
        }

        public static ChatSession FromJson(JVal j)
        {
            ChatSession s = new ChatSession();
            s.Id = j["id"].AsString(s.Id);
            s.Title = j["title"].AsString("New chat");
            s.Model = j["model"].AsString("");
            s.Messages = new List<ChatMessage>();
            foreach (JVal m in j["messages"].Items()) s.Messages.Add(ChatMessage.FromJson(m));
            long c = j["createdAt"].AsLong(0);
            long u = j["updatedAt"].AsLong(0);
            s.CreatedAt = c > 0 ? Clock.FromMs(c) : DateTime.Now;
            s.UpdatedAt = u > 0 ? Clock.FromMs(u) : s.CreatedAt;
            s.IsPinned = j["isPinned"].AsBool(false);
            return s;
        }
    }
}
