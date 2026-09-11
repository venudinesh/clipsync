// Which languages this PC can take dictation in.
//
// Windows keeps its speech recognisers in the registry, which is where SAPI itself
// looks for them, so they can be listed without loading a speech assembly and
// without a reference this build cannot resolve on a Windows 7 machine. Two places
// hold them: the desktop engines every version has, and the newer OneCore engines
// on Windows 10 and 11. Both are read, and a language installed twice is listed
// once.
//
// The stored setting is a culture name such as en-GB rather than an engine name,
// because engine names differ between the two sets and a person choosing a
// language should not have to care which engine answers.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Win32;

namespace ClipSyncAI
{
    internal sealed class VoiceKind
    {
        public string Culture = "";
        public string Label = "";
    }

    internal static class Voices
    {
        private static readonly string[] Roots =
        {
            @"SOFTWARE\Microsoft\Speech\Recognizers\Tokens",
            @"SOFTWARE\Microsoft\Speech_OneCore\Recognizers\Tokens"
        };

        /// Every language with a dictation engine behind it, in alphabetical order.
        /// An empty list means dictation is not available on this PC, which the
        /// settings page says out loud rather than offering a list of nothing.
        public static List<VoiceKind> Installed()
        {
            Dictionary<string, VoiceKind> found =
                new Dictionary<string, VoiceKind>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Roots.Length; i++) Sweep(Roots[i], found);
            List<VoiceKind> all = new List<VoiceKind>(found.Values);
            all.Sort(ByLabel);
            return all;
        }

        private static int ByLabel(VoiceKind a, VoiceKind b)
        {
            return string.Compare(a.Label, b.Label, StringComparison.CurrentCulture);
        }

        private static void Sweep(string path, Dictionary<string, VoiceKind> into)
        {
            try
            {
                // The 64-bit view explicitly: a 32-bit build of this app would
                // otherwise be handed WOW6432Node, where the engines are not.
                using (RegistryKey b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,
                           Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default))
                using (RegistryKey k = b.OpenSubKey(path, false))
                {
                    if (k == null) return;
                    string[] names = k.GetSubKeyNames();
                    for (int i = 0; i < names.Length; i++) Token(k, names[i], into);
                }
            }
            catch (Exception ex)
            {
                Paths.Log("speech engines", ex);
            }
        }

        private static void Token(RegistryKey parent, string name, Dictionary<string, VoiceKind> into)
        {
            try
            {
                using (RegistryKey t = parent.OpenSubKey(name, false))
                {
                    if (t == null) return;
                    using (RegistryKey a = t.OpenSubKey("Attributes", false))
                    {
                        if (a == null) return;
                        if (a.GetValue("Dictation") == null) return;
                        string culture = Culture(a.GetValue("Language") as string);
                        if (culture.Length == 0 || into.ContainsKey(culture)) return;
                        VoiceKind v = new VoiceKind();
                        v.Culture = culture;
                        v.Label = Pretty(culture);
                        into[culture] = v;
                    }
                }
            }
            catch (Exception) { }
        }

        /// The token's language is a hex locale id, sometimes several separated by
        /// semicolons for an engine that covers a family. The first is the one the
        /// engine is actually built for.
        private static string Culture(string language)
        {
            if (string.IsNullOrEmpty(language)) return "";
            string first = language.Split(';')[0].Trim();
            int lcid;
            if (!int.TryParse(first, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out lcid))
            {
                return "";
            }
            try
            {
                return new CultureInfo(lcid).Name;
            }
            catch (Exception) { return ""; }
        }

        /// "English (United Kingdom)" rather than "en-GB". The code is kept in the
        /// file; the words are what the page shows.
        public static string Pretty(string culture)
        {
            if (string.IsNullOrEmpty(culture)) return "Windows default";
            try
            {
                return new CultureInfo(culture).DisplayName;
            }
            catch (Exception) { return culture; }
        }
    }
}
