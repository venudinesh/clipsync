// The message pools behind the Clips masthead greeting.
//
// A direct extraction of the arrays in lib/features/timely_messages.dart. Every
// line is a compile time constant: nothing is fetched and nothing is stored, so
// the greeting costs no network and no disk.

using System;

namespace ClipSyncAI
{
    internal enum DayPart { Midnight, Morning, Afternoon, EarlyEvening, Evening, Night }

    internal static class TimelyPools
    {
        /// The hours a new day part begins on, ascending. Used both to classify
        /// a timestamp and to work out when the next change is due.
        public static readonly int[] Boundaries = new[] { 0, 5, 12, 17, 19, 22 };

        public static readonly string[] Morning = new[]
        {
            "Good morning!",
            "Good morning, time to rock!",
            "What a beautiful day!",
            "Have a nice day!",
            "What about a small 5 minutes nap?",
            "That's one way to wake up.",
            "You should change your alarm sounds.",
            "Looks like it's a beautiful day, isn't it?",
            "Let's get this bread.",
            "Check the weather just in case.",
            "Feeling a bit sleepy yet?",
            "Have you looked outside yet?",
            "Let's get this done.",
            "Good luck today!",
            "Don't forget about mental health.",
            "Life is good.",
            "Enjoy your breakfast.",
        };

        public static readonly string[] Afternoon = new[]
        {
            "Don't forget to stay hydrated!",
            "Remember to eat some snacks.",
            "UV rays are more harmful in the afternoon.",
            "It is afternoon right now.",
            "Always check your device's battery level!",
            "Remember to check your email and messages.",
            "Please check your to-do list if you have one.",
            "Don't forget to prepare your dinner.",
            "Enjoy your day.",
            "Hang on! The day is almost done!",
            "Have some fun.",
        };

        public static readonly string[] EarlyEvening = new[]
        {
            "Always stay hydrated!",
            "8 hours of sleep is a must.",
            "It is evening right now.",
            "Remember to charge up before you sleep.",
            "Remember to check your email and messages.",
            "Please check your to-do list if you have one.",
            "Remember to eat dinner.",
            "Always check the lights and sockets.",
            "Enjoy the night.",
            "Hey there! Give yourself some rest!",
            "Relax and get some rest.",
        };

        public static readonly string[] Evening = new[]
        {
            "Don't drink too much water before sleeping.",
            "It is evening right now.",
            "Try to avoid overnight charging if possible.",
            "Remember to check your email and messages.",
            "Please check your to-do list if you have one.",
            "Remember to eat dinner.",
            "8 hours of sleep is a must.",
            "Enjoy the night.",
            "Lugaw is a nice recipe for dinner.",
            "Enjoy your sleep.",
        };

        /// Shared by Night and Midnight. The two differ in how they greet you,
        /// not in what they have to say.
        public static readonly string[] Late = new[]
        {
            "Sleep deficiency is bad for your health.",
            "Don't forget to stay hydrated.",
            "Sleep deficiency can break melatonin production.",
            "It is late right now.",
            "Sleeping late is not healthy.",
            "You need to sleep now.",
            "Don't forget to sleep, or to prepare your breakfast.",
            "Enjoy the quiet.",
            "Sleep deficiency affects your body clock.",
            "Please take care of yourself.",
        };

        /// The fallback pool, and also the pool a small fraction of picks are
        /// diverted to at any hour, so a familiar time of day can still
        /// surprise you.
        public static readonly string[] AnyTime = new[]
        {
            "Make peace, not war.",
            "Oh hey, what's up?",
            "Focus on your tasks.",
            "How many screenshots do you take?",
            "Spread love, not havoc.",
            "We didn't start the fire!",
            "Time for some good music.",
            "Starting from the ground up.",
            "Do something nice today.",
            "How is everything going?",
            "No illusions, welcome to reality!",
            "Thank you for your support.",
            "What a lovely experience, isn't it?",
            "What's on your mind?",
            "Expecto Patronum.",
            "Wubba Lubba Dub Dub.",
            "Try to avoid UV rays around noon.",
            "Stay hydrated!",
            "Pringles aren't actually potato chips.",
            "Laughing can increase blood flow.",
            "Coffee contains antioxidants.",
            "Ketchup was once sold as medicine.",
            "Cotton candy was invented by a dentist.",
            "Don't skip your meal!",
            "Remember to check your device's battery level!",
            "Check your email and messages.",
            "Check your to-do list if you have one.",
            "Go get them!",
            "Time to buy some rice!",
            "Time for lunch!",
            "All white rice starts brown.",
            "Want some soda?",
            "Always bring water to stay hydrated.",
            "Space smells like seared steak.",
            "Enjoy your day!",
        };
    }
}
