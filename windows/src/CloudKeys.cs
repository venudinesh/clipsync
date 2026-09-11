// The API key, sealed on disk away from the settings file.
//
// Settings export to plain JSON in a few places (backups, the diagnostics
// screen), so the key never travels through AppSettings. It lives in its own
// DPAPI-sealed store, loaded by the cloud engine at call time.

using System;
using System.IO;

namespace ClipSyncAI
{
    internal static class CloudKeys
    {
        private static string FilePath { get { return Paths.Store("apikey"); } }

        public static string Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return "";
                string s = Crypto.Open(File.ReadAllBytes(FilePath));
                return s ?? "";
            }
            catch (Exception ex)
            {
                Paths.Log("apikey read", ex);
                return "";
            }
        }

        public static void Save(string key)
        {
            try
            {
                if (string.IsNullOrEmpty(key))
                {
                    if (File.Exists(FilePath)) File.Delete(FilePath);
                    return;
                }
                Paths.EnsureRoot();
                File.WriteAllBytes(FilePath, Crypto.Seal(key));
            }
            catch (Exception ex)
            {
                Paths.Log("apikey write", ex);
            }
        }

        public static bool HasKey { get { return Load().Length > 0; } }
    }
}
