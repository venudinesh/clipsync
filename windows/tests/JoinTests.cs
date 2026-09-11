// Joining clips.
//
// The desktop twin of the phone's joinClips tests: two or more clips become
// one new clip, oldest first, with a rule between each part, preferring each
// side's own body and falling back to the other.

using System;
using System.Collections.Generic;

namespace ClipSyncAI.Tests
{
    internal static class JoinTests
    {
        private static ClipEntry C(string raw, string formatted, DateTime when, bool checklist)
        {
            ClipEntry c = new ClipEntry();
            c.RawText = raw;
            c.ProcessedMarkdown = formatted;
            c.Timestamp = when;
            c.IsChecklist = checklist;
            return c;
        }

        private static ClipEntry C(string raw, string formatted)
        {
            return C(raw, formatted, new DateTime(2026, 1, 1), false);
        }

        public static void Run()
        {
            T.Group("Joining clips");

            List<ClipEntry> outOfOrder = new List<ClipEntry>();
            outOfOrder.Add(C("second", "Second", new DateTime(2026, 3, 2), false));
            outOfOrder.Add(C("first", "First", new DateTime(2026, 3, 1), false));
            ClipEntry ordered = ClipJoin.Join(outOfOrder);
            T.Eq("oldest clip comes first whatever the pick order",
                "first" + ClipJoin.Separator + "second", ordered.RawText);
            T.Eq("and the formatted side follows it",
                "First" + ClipJoin.Separator + "Second", ordered.ProcessedMarkdown);

            List<ClipEntry> gappy = new List<ClipEntry>();
            gappy.Add(C("", "Only formatted", new DateTime(2026, 2, 1), false));
            gappy.Add(C("Only raw", "", new DateTime(2026, 2, 2), false));
            ClipEntry filled = ClipJoin.Join(gappy);
            T.Eq("an empty original falls back to the formatted body",
                "Only formatted" + ClipJoin.Separator + "Only raw", filled.RawText);
            T.Eq("and an empty formatted side falls back to the original",
                "Only formatted" + ClipJoin.Separator + "Only raw", filled.ProcessedMarkdown);

            List<ClipEntry> checks = new List<ClipEntry>();
            checks.Add(C("a", "A", new DateTime(2026, 1, 1), true));
            checks.Add(C("b", "B", new DateTime(2026, 1, 2), true));
            T.Eq("all checklists stay a checklist", true, ClipJoin.Join(checks).IsChecklist);

            List<ClipEntry> mixed = new List<ClipEntry>();
            mixed.Add(C("a", "A", new DateTime(2026, 1, 1), true));
            mixed.Add(C("c", "C", new DateTime(2026, 1, 2), false));
            T.Eq("one plain clip unchecks the join", false, ClipJoin.Join(mixed).IsChecklist);

            List<ClipEntry> two = new List<ClipEntry>();
            two.Add(C("a", "A"));
            two.Add(C("b", "B"));
            ClipEntry fresh = ClipJoin.Join(two);
            T.Eq("the joined clip gets its own id", true, fresh.Id.Length > 0);
            T.Eq("with a fresh timestamp", true,
                fresh.Timestamp > DateTime.Now.AddSeconds(-5));

            List<ClipEntry> three = new List<ClipEntry>();
            three.Add(C("one", "One", new DateTime(2026, 1, 3), false));
            three.Add(C("two", "Two", new DateTime(2026, 1, 1), false));
            three.Add(C("three", "Three", new DateTime(2026, 1, 2), false));
            T.Eq("three clips join oldest first with a rule between each",
                "Two" + ClipJoin.Separator + "Three" + ClipJoin.Separator + "One",
                ClipJoin.Join(three).ProcessedMarkdown);
        }
    }
}
