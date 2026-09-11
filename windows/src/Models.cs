// The records the app stores, and their JSON shape.
//
// Field names match the Dart models one for one, so an export from the phone
// build imports here without translation.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ClipSyncAI
{
    internal static class AppDefaults
    {
        public const string DefaultModel = "smollm2-135m";

        /// Two seconds after the clipboard settles before anything is captured.
        /// A password manager that pastes and immediately clears, or an editor
        /// that fires several change notifications for one copy, both land
        /// inside this window and produce a single entry or none.
        public const int ClipboardDebounceMs = 2000;

        public const string StoreClips = "clip_history";
        public const string StoreNotes = "notes";
        public const string StoreSettings = "settings";
        public const string StoreChats = "chat_sessions";

        public static readonly string[] NavLabels =
            new[] { "Clips", "Chat", "Notes", "Capture", "Settings" };
    }

    /// Identifiers are time ordered, so a list sorted by id is also sorted by
    /// age even if two records share a millisecond.
    internal static class Ids
    {
        private static long _last;
        private static readonly object Gate = new object();

        public static string New()
        {
            long ms = Clock.NowMs();
            lock (Gate)
            {
                if (ms <= _last) ms = _last + 1;
                _last = ms;
            }
            return ms.ToString("D13", CultureInfo.InvariantCulture) + "-" +
                   Guid.NewGuid().ToString("N").Substring(0, 8);
        }
    }

    internal static class Clock
    {
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static long NowMs()
        {
            return (long)(DateTime.UtcNow - Epoch).TotalMilliseconds;
        }

        public static long ToMs(DateTime t)
        {
            return (long)(t.ToUniversalTime() - Epoch).TotalMilliseconds;
        }

        public static DateTime FromMs(long ms)
        {
            return Epoch.AddMilliseconds(ms).ToLocalTime();
        }
    }

    internal sealed class ClipEntry
    {
        public string Id;
        public string RawText;
        public string ProcessedMarkdown;
        public DateTime Timestamp;
        public bool IsPinned;
        public bool IsChecklist;

        public ClipEntry()
        {
            Id = Ids.New();
            RawText = "";
            ProcessedMarkdown = "";
            Timestamp = DateTime.Now;
        }

        public string Preview(int max)
        {
            string s = (RawText ?? "").Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
            while (s.Contains("  ")) s = s.Replace("  ", " ");
            s = s.Trim();
            if (s.Length <= max) return s;
            return s.Substring(0, max).TrimEnd() + "...";
        }

        public JVal ToJson()
        {
            return JVal.Object()
                .Set("id", Id)
                .Set("rawText", RawText)
                .Set("processedMarkdown", ProcessedMarkdown)
                .Set("timestamp", Clock.ToMs(Timestamp))
                .Set("isPinned", IsPinned)
                .Set("isChecklist", IsChecklist);
        }

        public static ClipEntry FromJson(JVal j)
        {
            ClipEntry c = new ClipEntry();
            c.Id = j["id"].AsString(c.Id);
            c.RawText = j["rawText"].AsString("");
            c.ProcessedMarkdown = j["processedMarkdown"].AsString("");
            long ms = j["timestamp"].AsLong(0);
            c.Timestamp = ms > 0 ? Clock.FromMs(ms) : DateTime.Now;
            c.IsPinned = j["isPinned"].AsBool(false);
            c.IsChecklist = j["isChecklist"].AsBool(false);
            return c;
        }
    }

    internal sealed class Note
    {
        public string Id;
        public string Title;
        public string Content;
        public List<string> Tags;
        public bool IsPinned;
        public DateTime CreatedAt;
        public DateTime UpdatedAt;

        public Note()
        {
            Id = Ids.New();
            Title = "";
            Content = "";
            Tags = new List<string>();
            CreatedAt = DateTime.Now;
            UpdatedAt = CreatedAt;
        }

        public JVal ToJson()
        {
            JVal tags = JVal.Array();
            for (int i = 0; i < Tags.Count; i++) tags.Add(JVal.Of(Tags[i]));
            return JVal.Object()
                .Set("id", Id)
                .Set("title", Title)
                .Set("content", Content)
                .Set("tags", tags)
                .Set("isPinned", IsPinned)
                .Set("createdAt", Clock.ToMs(CreatedAt))
                .Set("updatedAt", Clock.ToMs(UpdatedAt));
        }

        public static Note FromJson(JVal j)
        {
            Note n = new Note();
            n.Id = j["id"].AsString(n.Id);
            n.Title = j["title"].AsString("");
            n.Content = j["content"].AsString("");
            n.Tags = new List<string>();
            foreach (JVal t in j["tags"].Items())
            {
                string s = t.AsString("").Trim();
                if (s.Length > 0) n.Tags.Add(s);
            }
            n.IsPinned = j["isPinned"].AsBool(false);
            long c = j["createdAt"].AsLong(0);
            long u = j["updatedAt"].AsLong(0);
            n.CreatedAt = c > 0 ? Clock.FromMs(c) : DateTime.Now;
            n.UpdatedAt = u > 0 ? Clock.FromMs(u) : n.CreatedAt;
            return n;
        }
    }
}
