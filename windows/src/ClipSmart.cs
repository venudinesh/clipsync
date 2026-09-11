// Clips: similarity, ranking, titles and retention.
//
// The desktop twin of the phone's smart-capture helpers: near-duplicate
// detection for the "already kept?" question, relevance-ranked search, titles
// and tags at capture time, and lifetime purges. Pure code, no model, except
// for titles, where a loaded model is asked first and a keyword heuristic
// answers when there is none.

using System;
using System.Collections.Generic;

namespace ClipSyncAI
{
    internal struct ClipTitleTags
    {
        public string Title;
        public List<string> Tags;
    }

    internal static class ClipSmart
    {
        /// Words that carry no meaning for search or tags. The phone carries
        /// the same list, so both ends agree on what a tag can be.
        public static readonly string[] Stopwords = new string[]
        {
            "a", "an", "the", "and", "or", "but", "of", "to", "in", "on",
            "for", "with", "is", "are", "was", "were", "be", "been", "this",
            "that", "these", "those", "it", "its", "as", "at", "by", "from",
            "into", "over", "after", "before", "about", "between", "through",
            "during", "will", "would", "can", "could", "should", "has",
            "have", "had", "not", "no", "you", "your", "we", "our", "they",
            "their", "he", "she", "him", "her", "his", "them", "then",
            "than", "too", "very", "just", "also", "here", "there", "when",
            "where", "which", "who", "what", "how", "all", "any", "both",
            "each", "few", "more", "most", "other", "some", "such", "only",
            "own", "same", "so", "now",
        };

        private static bool IsStop(string w)
        {
            return Array.IndexOf(Stopwords, w) >= 0;
        }

        /// Lowercase alphanumeric tokens, stopwords dropped.
        public static List<string> Words(string text, int minLength)
        {
            List<string> out_ = new List<string>();
            if (string.IsNullOrEmpty(text)) return out_;
            int i = 0;
            while (i < text.Length)
            {
                while (i < text.Length && !IsWord(text[i])) i++;
                int j = i;
                while (j < text.Length && IsWord(text[j])) j++;
                if (j > i)
                {
                    string w = text.Substring(i, j - i).ToLowerInvariant();
                    if (w.Length >= minLength && !IsStop(w)) out_.Add(w);
                }
                i = j;
            }
            return out_;
        }

        private static bool IsWord(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                (c >= '0' && c <= '9');
        }

        public static string Norm(string t)
        {
            if (string.IsNullOrEmpty(t)) return "";
            return System.Text.RegularExpressions.Regex.Replace(
                t.ToLowerInvariant(), @"\s+", " ").Trim();
        }

        /// Jaccard similarity of two texts' word sets, short-circuiting to 1
        /// for normalized-equal texts.
        public static double Similarity(string a, string b)
        {
            string na = Norm(a);
            string nb = Norm(b);
            if (na == nb) return 1;
            if (na.Length == 0 || nb.Length == 0) return 0;
            Dictionary<string, bool> sa = new Dictionary<string, bool>();
            foreach (string w in Words(na, 2)) sa[w] = true;
            int overlap = 0;
            Dictionary<string, bool> seen = new Dictionary<string, bool>();
            foreach (string w in Words(nb, 2))
            {
                if (seen.ContainsKey(w)) continue;
                seen[w] = true;
                if (sa.ContainsKey(w)) overlap++;
            }
            int union = sa.Count + seen.Count - overlap;
            return union == 0 ? 0 : (double)overlap / union;
        }

        /// The kept clip most like [text], or null. Normalized equality always
        /// wins; otherwise the best match above threshold does, ignoring stubs
        /// too short to judge.
        public static ClipEntry FindSimilar(
            string text, IList<ClipEntry> clips, double threshold)
        {
            string norm = Norm(text);
            if (norm.Length == 0) return null;
            ClipEntry best = null;
            double bestScore = 0;
            for (int i = 0; i < clips.Count; i++)
            {
                ClipEntry c = clips[i];
                if (c == null) continue;
                string cnorm = Norm(c.RawText);
                if (cnorm.Length == 0) continue;
                if (cnorm == norm) return c;
                if (norm.Length < 20 || cnorm.Length < 20) continue;
                double s = Similarity(norm, cnorm);
                if (s >= threshold && s > bestScore)
                {
                    bestScore = s;
                    best = c;
                }
            }
            return best;
        }

        /// Rarity of each query term across the corpus, computed once per
        /// search rather than per row.
        public static Dictionary<string, double> Idf(
            IList<ClipEntry> clips, List<string> terms)
        {
            Dictionary<string, double> idf = new Dictionary<string, double>();
            foreach (string t in terms)
            {
                if (idf.ContainsKey(t)) continue;
                int df = 0;
                for (int i = 0; i < clips.Count; i++)
                {
                    ClipEntry c = clips[i];
                    if (c == null) continue;
                    string hay = (c.Title ?? "") + " " +
                        string.Join(" ", (c.Tags ?? new List<string>()).ToArray()) +
                        " " + (c.ProcessedMarkdown ?? "") + " " + (c.RawText ?? "");
                    if (hay.ToLowerInvariant().IndexOf(t,
                        StringComparison.Ordinal) >= 0) df++;
                }
                idf[t] = 1 + (double)(clips.Count + 1) / (1 + df);
            }
            return idf;
        }

        /// Relevance of one clip to the query terms: title hits beat tag hits
        /// beat body hits, rare words beat common ones.
        public static double Score(
            ClipEntry c, List<string> terms, Dictionary<string, double> idf)
        {
            string title = (c.Title ?? "").ToLowerInvariant();
            string tags = string.Join(" ",
                (c.Tags ?? new List<string>()).ToArray()).ToLowerInvariant();
            string formatted = (c.ProcessedMarkdown ?? "").ToLowerInvariant();
            string raw = (c.RawText ?? "").ToLowerInvariant();
            double score = 0;
            foreach (string t in terms)
            {
                double w = idf.ContainsKey(t) ? idf[t] : 1;
                if (title.IndexOf(t, StringComparison.Ordinal) >= 0)
                    score += 4 * w;
                if (tags.IndexOf(t, StringComparison.Ordinal) >= 0)
                    score += 3 * w;
                if (formatted.IndexOf(t, StringComparison.Ordinal) >= 0)
                    score += 2 * w;
                if (raw.IndexOf(t, StringComparison.Ordinal) >= 0)
                    score += w;
            }
            return score;
        }

        /// A title and tags for [text]: [ask] runs the model prompt when one is
        /// loaded (null or empty answer falls back), otherwise the first line
        /// and the most frequent meaningful words do. Never throws.
        public static ClipTitleTags Describe(string text, Func<string, string> ask)
        {
            if (ask != null)
            {
                try
                {
                    string answer = ask(
                        "Give this note a title of at most 6 words and up to 3 " +
                        "lowercase single-word tags, comma separated. Reply with " +
                        "exactly two lines:\nTITLE: <title>\nTAGS: <tag>, <tag>\n\n" +
                        text);
                    if (!string.IsNullOrEmpty(answer))
                    {
                        ClipTitleTags parsed = ParseTitleTags(answer);
                        if (parsed.Title.Length > 0) return parsed;
                    }
                }
                catch (Exception) { }
            }
            return Heuristic(text);
        }

        public static ClipTitleTags ParseTitleTags(string answer)
        {
            ClipTitleTags r = new ClipTitleTags();
            r.Title = "";
            r.Tags = new List<string>();
            string[] lines = (answer ?? "").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].Trim();
                if (t.Length > 6 &&
                    t.Substring(0, 6).ToUpperInvariant() == "TITLE:")
                {
                    r.Title = t.Substring(6).Trim();
                }
                else if (t.Length > 5 &&
                    t.Substring(0, 5).ToUpperInvariant() == "TAGS:")
                {
                    string[] parts = t.Substring(5).Split(',');
                    for (int k = 0; k < parts.Length && r.Tags.Count < 3; k++)
                    {
                        char[] buf = new char[parts[k].Length];
                        int n = 0;
                        foreach (char ch in parts[k].Trim().ToLowerInvariant())
                        {
                            if ((ch >= 'a' && ch <= 'z') ||
                                (ch >= '0' && ch <= '9')) buf[n++] = ch;
                        }
                        if (n > 0) r.Tags.Add(new string(buf, 0, n));
                    }
                }
            }
            if (r.Title.Length > 60) r.Title = r.Title.Substring(0, 57).Trim() + "…";
            return r;
        }

        public static ClipTitleTags Heuristic(string text)
        {
            ClipTitleTags r = new ClipTitleTags();
            r.Title = "";
            r.Tags = new List<string>();
            if (string.IsNullOrEmpty(text)) return r;
            string[] lines = text.Split('\n');
            foreach (string line in lines)
            {
                string t = line.Trim();
                while (t.StartsWith("#")) t = t.Substring(1).Trim();
                if (t.Length > 0)
                {
                    r.Title = t.Length > 48 ? t.Substring(0, 45).Trim() + "…" : t;
                    break;
                }
            }
            Dictionary<string, int> freq = new Dictionary<string, int>();
            List<string> order = new List<string>();
            foreach (string w in Words(text, 4))
            {
                if (!freq.ContainsKey(w))
                {
                    freq[w] = 0;
                    order.Add(w);
                }
                freq[w]++;
            }
            order.Sort(delegate(string a, string b)
            {
                int cmp = freq[b].CompareTo(freq[a]);
                return cmp != 0 ? cmp : order.IndexOf(a).CompareTo(order.IndexOf(b));
            });
            for (int i = 0; i < order.Count && r.Tags.Count < 3; i++)
            {
                r.Tags.Add(order[i]);
            }
            return r;
        }

        /// Drops unpinned clips older than [days] and reports how many went.
        /// Zero or negative keeps everything, forever.
        public static int PurgeExpired(Store<ClipEntry> clips, int days)
        {
            if (clips == null || days <= 0) return 0;
            DateTime cutoff = DateTime.Now.AddDays(-days);
            int n = 0;
            for (int i = clips.Items.Count - 1; i >= 0; i--)
            {
                ClipEntry c = clips.Items[i];
                if (c == null || c.IsPinned) continue;
                if (c.Timestamp < cutoff)
                {
                    clips.Items.RemoveAt(i);
                    n++;
                }
            }
            if (n > 0) clips.Save();
            return n;
        }
    }
}
