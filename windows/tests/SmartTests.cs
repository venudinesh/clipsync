// Smart capture: similarity, ranking, titles and retention.
//
// The desktop twin of the phone's smart-clips tests.

using System;
using System.Collections.Generic;

namespace ClipSyncAI.Tests
{
    internal static class SmartTests
    {
        private static ClipEntry C(string raw, DateTime when)
        {
            ClipEntry c = new ClipEntry();
            c.RawText = raw;
            c.ProcessedMarkdown = raw;
            c.Timestamp = when;
            return c;
        }

        private static ClipEntry C(string raw)
        {
            return C(raw, new DateTime(2026, 1, 1));
        }

        public static void Run()
        {
            T.Group("Smart capture");

            T.Eq("identical texts score 1", true,
                ClipSmart.Similarity("hello world", "hello world") == 1);
            T.Eq("case and whitespace do not matter", true,
                ClipSmart.Similarity("  Hello   WORLD ", "hello world") == 1);
            T.Eq("disjoint texts score 0", true,
                ClipSmart.Similarity("cats purr softly", "quantum field theory") == 0);
            double partial = ClipSmart.Similarity(
                "the quarterly planning notes for june",
                "quarterly planning notes draft");
            T.Eq("partial overlap lands between", true, partial > 0.4 && partial < 1);

            List<ClipEntry> kept = new List<ClipEntry>();
            ClipEntry same = C("Deploy   Friday");
            kept.Add(same);
            kept.Add(C("something else entirely different here"));
            T.Eq("a normalized-equal clip is found",
                same.Id, ClipSmart.FindSimilar("deploy friday", kept, 0.85).Id);

            List<ClipEntry> notes = new List<ClipEntry>();
            ClipEntry plan = C("quarterly planning notes for june with action items and owners");
            notes.Add(plan);
            T.Eq("a near-duplicate above threshold is found", plan.Id,
                ClipSmart.FindSimilar(
                    "quarterly planning notes for june with action items, owners and dates",
                    notes, 0.85).Id);

            List<ClipEntry> other = new List<ClipEntry>();
            other.Add(C("the kubernetes deployment pipeline failed again this morning"));
            T.Eq("unrelated clips are ignored", true,
                ClipSmart.FindSimilar(
                    "buy milk and eggs on the way home tonight", other, 0.85) == null);

            List<ClipEntry> stubs = new List<ClipEntry>();
            stubs.Add(C("ok sure"));
            T.Eq("short texts only match exactly", true,
                ClipSmart.FindSimilar("ok", stubs, 0.85) == null);

            List<ClipEntry> feed = new List<ClipEntry>();
            ClipEntry titled = C("weekly sync notes and updates");
            titled.Title = "Q3 planning";
            ClipEntry bodied = C("some notes about q3 planning deadlines");
            feed.Add(bodied);
            feed.Add(titled);
            List<string> terms = ClipSmart.Words("planning", 2);
            Dictionary<string, double> idf = ClipSmart.Idf(feed, terms);
            T.Eq("a title hit outranks a body hit", true,
                ClipSmart.Score(titled, terms, idf) >
                ClipSmart.Score(bodied, terms, idf));

            ClipTitleTags heur = ClipSmart.Heuristic(
                "Q3 planning notes\nq3 goals, q3 budget, q3 hiring plan");
            T.Eq("the heuristic takes the first line",
                "Q3 planning notes", heur.Title);
            T.Eq("and the frequent words", true, heur.Tags.Contains("planning"));

            ClipTitleTags parsed = ClipSmart.ParseTitleTags(
                "TITLE: Weekend shopping\nTAGS: home, food, x");
            T.Eq("model answers parse to a title",
                "Weekend shopping", parsed.Title);
            T.Eq("and to three tags", 3, parsed.Tags.Count);
            T.Eq("lowercased", "home", parsed.Tags[0]);

            Store<ClipEntry> store = new Store<ClipEntry>("test_retention",
                delegate(ClipEntry c) { return c.ToJson(); }, ClipEntry.FromJson);
            DateTime now = DateTime.Now;
            ClipEntry old = C("old", now.AddDays(-30));
            ClipEntry fresh = C("fresh", now);
            ClipEntry pinned = C("pinned", now.AddDays(-30));
            pinned.IsPinned = true;
            store.Items.Add(old);
            store.Items.Add(fresh);
            store.Items.Add(pinned);
            store.Save();
            T.Eq("the purge burns old clips", 1, ClipSmart.PurgeExpired(store, 7));
            T.Eq("and keeps the fresh one", true, store.Items.Contains(fresh));
            T.Eq("and the pinned one", true, store.Items.Contains(pinned));

            Store<ClipEntry> keep = new Store<ClipEntry>("test_retention_keep",
                delegate(ClipEntry c) { return c.ToJson(); }, ClipEntry.FromJson);
            ClipEntry ancient = C("ancient", new DateTime(2020, 1, 1));
            keep.Items.Add(ancient);
            keep.Save();
            T.Eq("zero retention keeps everything", 0, ClipSmart.PurgeExpired(keep, 0));
            T.Eq("down to the ancient one", true, keep.Items.Contains(ancient));
        }
    }
}
