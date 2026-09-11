// Where the app keeps its files.
//
// Roaming AppData by default. If a file named portable.txt sits next to the
// executable, everything moves beside the executable instead, so the app can
// live on a stick without leaving anything behind on the host. DPAPI still
// seals the key to the Windows account either way, which is the honest limit of
// portable mode and is stated in Settings rather than hidden.

using System;
using System.IO;
using System.Reflection;

namespace ClipSyncAI
{
    internal static class Paths
    {
        private static string _root;

        public static string ExeDir
        {
            get
            {
                string p = Assembly.GetExecutingAssembly().Location;
                string d = Path.GetDirectoryName(p);
                return string.IsNullOrEmpty(d) ? Environment.CurrentDirectory : d;
            }
        }

        public static bool Portable
        {
            get
            {
                try { return File.Exists(Path.Combine(ExeDir, "portable.txt")); }
                catch (Exception) { return false; }
            }
        }

        public static string Root
        {
            get
            {
                if (_root != null) return _root;
                if (Portable)
                {
                    _root = Path.Combine(ExeDir, "data");
                }
                else
                {
                    string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    _root = Path.Combine(appData, "ClipSyncAI");
                }
                return _root;
            }
        }

        public static void EnsureRoot()
        {
            if (!Directory.Exists(Root)) Directory.CreateDirectory(Root);
        }

        /// Points the whole store somewhere else. Exists so the test suite can
        /// run against a scratch directory instead of the real profile; a test
        /// that clobbers a person's clipboard history is not a test.
        public static void UseRoot(string path)
        {
            _root = path;
        }

        public static string KeyFile { get { return Path.Combine(Root, "key.bin"); } }

        public static string Store(string name)
        {
            return Path.Combine(Root, name + ".dat");
        }

        public static string LogFile { get { return Path.Combine(Root, "diagnostics.log"); } }

        /// Appends a line to the diagnostics log, capped so it cannot grow
        /// without bound on a machine that is left running for months.
        public static void Log(string message)
        {
            try
            {
                EnsureRoot();
                string path = LogFile;
                if (File.Exists(path))
                {
                    FileInfo fi = new FileInfo(path);
                    if (fi.Length > 256 * 1024)
                    {
                        string old = path + ".1";
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(path, old);
                    }
                }
                using (StreamWriter w = new StreamWriter(path, true))
                {
                    w.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message);
                }
            }
            catch (Exception)
            {
                // Logging must never be the reason the app fails.
            }
        }

        public static void Log(string context, Exception ex)
        {
            Log(context + ": " + (ex == null ? "null" : ex.GetType().Name + " " + ex.Message));
        }
    }
}
