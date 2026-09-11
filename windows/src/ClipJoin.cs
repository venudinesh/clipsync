// Clips: joining them.
//
// The desktop twin of the phone's joinClips: pick two or more clips and they
// become one new clip, oldest first so the result reads in the order the clips
// were captured, with a rule between each part. Each side prefers its own body
// and falls back to the other when that one is empty, so a clip that came
// through the formatter untouched still lands whole. The joined clip is a
// checklist only when every source was one.

using System;
using System.Collections.Generic;

namespace ClipSyncAI
{
    internal static class ClipJoin
    {
        public const string Separator = "\n\n---\n\n";

        public static ClipEntry Join(IList<ClipEntry> clips)
        {
            List<ClipEntry> ordered = new List<ClipEntry>(clips);
            ordered.Sort(delegate(ClipEntry a, ClipEntry b)
            {
                int t = a.Timestamp.CompareTo(b.Timestamp);
                return t != 0 ? t : string.CompareOrdinal(a.Id, b.Id);
            });
            List<string> raws = new List<string>();
            List<string> formatteds = new List<string>();
            bool allChecklist = ordered.Count > 0;
            for (int i = 0; i < ordered.Count; i++)
            {
                ClipEntry e = ordered[i];
                string raw = BodyOf(e.RawText, e.ProcessedMarkdown);
                string formatted = BodyOf(e.ProcessedMarkdown, e.RawText);
                if (raw.Length > 0) raws.Add(raw);
                if (formatted.Length > 0) formatteds.Add(formatted);
                if (!e.IsChecklist) allChecklist = false;
            }
            ClipEntry joined = new ClipEntry();
            joined.RawText = string.Join(Separator, raws.ToArray());
            joined.ProcessedMarkdown = string.Join(Separator, formatteds.ToArray());
            joined.IsChecklist = allChecklist;
            return joined;
        }

        private static string BodyOf(string primary, string fallback)
        {
            string p = (primary ?? "").Trim();
            return p.Length > 0 ? p : (fallback ?? "").Trim();
        }
    }
}
