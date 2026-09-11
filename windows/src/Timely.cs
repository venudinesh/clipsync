// Greeting selection. A port of the selection half of
// lib/features/timely_messages.dart.

using System;
using System.Collections.Generic;

namespace ClipSyncAI
{
    internal sealed class TimelyGreeting
    {
        public DayPart Part;
        public string Greeting;
        public string Message;

        public TimelyGreeting(DayPart part, string greeting, string message)
        {
            Part = part;
            Greeting = greeting;
            Message = message;
        }
    }

    internal static class Timely
    {
        /// One pick in this many is diverted to the any time pool regardless of
        /// the hour. Rare enough to feel like a find rather than a rotation.
        private const int WildcardOdds = 14;

        private static readonly Random Rng = new Random();

        /// Local time, because a greeting that disagrees with the wall clock is
        /// worse than no greeting.
        public static DayPart PartAt(DateTime now)
        {
            int hour = now.Hour;
            if (hour < 5) return DayPart.Midnight;
            if (hour < 12) return DayPart.Morning;
            if (hour < 17) return DayPart.Afternoon;
            if (hour < 19) return DayPart.EarlyEvening;
            if (hour < 22) return DayPart.Evening;
            return DayPart.Night;
        }

        public static string GreetingFor(DayPart part)
        {
            switch (part)
            {
                case DayPart.Midnight: return "Solemn midnight.";
                case DayPart.Morning: return "Good morning.";
                case DayPart.Afternoon: return "Good afternoon.";
                case DayPart.EarlyEvening: return "Good evening.";
                case DayPart.Evening: return "Good evening.";
                default: return "Good night.";
            }
        }

        public static string[] MessagesFor(DayPart part)
        {
            switch (part)
            {
                case DayPart.Morning: return TimelyPools.Morning;
                case DayPart.Afternoon: return TimelyPools.Afternoon;
                case DayPart.EarlyEvening: return TimelyPools.EarlyEvening;
                case DayPart.Evening: return TimelyPools.Evening;
                default: return TimelyPools.Late;
            }
        }

        /// Whether a message says nothing the greeting has not already said.
        /// "Good evening." over "It is evening right now." states one fact twice
        /// and reads like a template nobody finished filling in. The line stays
        /// in the pool and simply never gets chosen under the greeting it
        /// echoes; "It is late right now." survives, because "Good night." does
        /// not already say it.
        public static bool RestatesGreeting(string message, string greeting)
        {
            const string opener = "it is ";
            const string closer = " right now.";
            if (message == null || greeting == null) return false;
            string m = message.ToLowerInvariant();
            if (!m.StartsWith(opener, StringComparison.Ordinal)) return false;
            if (!m.EndsWith(closer, StringComparison.Ordinal)) return false;
            string subject = m.Substring(opener.Length, m.Length - closer.Length - opener.Length);
            return subject.Length > 0 && greeting.ToLowerInvariant().Contains(subject);
        }

        /// Picks a greeting and a line. Pass avoid to discourage repeating a
        /// line that is already on screen, because a click that changes nothing
        /// reads as a dead click.
        public static TimelyGreeting Pick(DateTime now, string avoid, Random random)
        {
            Random rng = random ?? Rng;
            DayPart part = PartAt(now);
            string greeting = GreetingFor(part);
            string[] source = rng.Next(WildcardOdds) == 0 ? TimelyPools.AnyTime : MessagesFor(part);

            List<string> usable = new List<string>(source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                if (!RestatesGreeting(source[i], greeting)) usable.Add(source[i]);
            }
            // No pool is one line of echo away from empty, but choosing from
            // nothing would throw, and a duplicated line beats a crashed
            // masthead.
            IList<string> pool = usable.Count == 0 ? (IList<string>)source : usable;

            string message = pool[rng.Next(pool.Count)];
            if (avoid != null && pool.Count > 1)
            {
                // A handful of re-rolls, not a loop that could spin.
                for (int i = 0; i < 4 && message == avoid; i++)
                {
                    message = pool[rng.Next(pool.Count)];
                }
            }
            return new TimelyGreeting(part, greeting, message);
        }

        public static TimelyGreeting Pick(string avoid)
        {
            return Pick(DateTime.Now, avoid, null);
        }

        /// The next instant the greeting should change, so a view can wait for
        /// the exact moment instead of polling the clock.
        public static DateTime NextBoundary(DateTime now)
        {
            for (int i = 0; i < TimelyPools.Boundaries.Length; i++)
            {
                int hour = TimelyPools.Boundaries[i];
                if (hour > now.Hour) return new DateTime(now.Year, now.Month, now.Day, hour, 0, 0);
            }
            // Past the last boundary of the day, so the next one is tomorrow's
            // midnight. Built by adding a day to the date rather than 24 hours
            // to the instant, which would land an hour off when clocks move.
            return new DateTime(now.Year, now.Month, now.Day, 0, 0, 0).AddDays(1);
        }
    }
}
