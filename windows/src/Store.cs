// The encrypted record stores.
//
// Each store is one file: a JSON array of records inside a sealed envelope.
// Small enough at this scale that rewriting the whole file on change is both
// simpler and safer than an append log, and a full rewrite is what makes
// deletion actually delete.

using System;
using System.Collections.Generic;
using System.IO;

namespace ClipSyncAI
{
    internal sealed class Store<T> where T : class
    {
        private readonly string _name;
        private readonly Func<T, JVal> _toJson;
        private readonly Func<JVal, T> _fromJson;
        private readonly object _gate = new object();

        public readonly List<T> Items = new List<T>();

        public Store(string name, Func<T, JVal> toJson, Func<JVal, T> fromJson)
        {
            _name = name;
            _toJson = toJson;
            _fromJson = fromJson;
        }

        public string Name { get { return _name; } }

        public void Load()
        {
            lock (_gate)
            {
                Items.Clear();
                string path = Paths.Store(_name);
                if (!File.Exists(path)) return;
                string text = null;
                try
                {
                    text = Crypto.Open(File.ReadAllBytes(path));
                }
                catch (IOException)
                {
                    // A sharing violation or a key file that could not be read
                    // is not a corrupt store. Leave the file where it is and let
                    // the caller report it.
                    throw;
                }
                catch (UnauthorizedAccessException)
                {
                    // The same rule for permissions: never rename a store that
                    // simply could not be opened.
                    throw;
                }
                catch (Exception ex)
                {
                    Paths.Log("store read " + _name, ex);
                }
                if (text == null)
                {
                    // Unreadable, so it is set aside under a dated name instead
                    // of being overwritten by the first save. Losing access to
                    // data is bad; destroying it is worse.
                    try { File.Move(path, path + ".unreadable-" + Clock.NowMs()); }
                    catch (Exception) { }
                    return;
                }
                JVal root = JsonParser.Parse(text);
                if (root == null) return;
                JVal list = root.Kind == JKind.Arr ? root : root["items"];
                foreach (JVal j in list.Items())
                {
                    try
                    {
                        T item = _fromJson(j);
                        if (item != null) Items.Add(item);
                    }
                    catch (Exception)
                    {
                        // One malformed record costs that record, not the store.
                    }
                }
            }
        }

        public bool Save()
        {
            lock (_gate)
            {
                JVal arr = JVal.Array();
                for (int i = 0; i < Items.Count; i++)
                {
                    try { arr.Add(_toJson(Items[i])); }
                    catch (Exception) { }
                }
                JVal root = JVal.Object()
                    .Set("v", 1L)
                    .Set("savedAt", Clock.NowMs())
                    .Set("items", arr);
                byte[] blob;
                try { blob = Crypto.Seal(JsonWriter.Write(root)); }
                catch (Exception ex)
                {
                    Paths.Log("store seal " + _name, ex);
                    return false;
                }
                Paths.EnsureRoot();
                string path = Paths.Store(_name);
                string tmp = path + ".tmp";
                try
                {
                    File.WriteAllBytes(tmp, blob);
                    Swap(tmp, path);
                    return true;
                }
                catch (Exception ex)
                {
                    Paths.Log("store write " + _name, ex);
                    return false;
                }
            }
        }

        /// Replace in one step where the platform allows it, so a crash midway
        /// leaves the previous file intact rather than a half written one.
        private static void Swap(string tmp, string path)
        {
            if (!File.Exists(path))
            {
                File.Move(tmp, path);
                return;
            }
            try
            {
                File.Replace(tmp, path, null);
            }
            catch (Exception)
            {
                // Keep the old file until the new one is in place; a crash in
                // between must not leave neither.
                string bak = path + ".bak";
                try
                {
                    if (File.Exists(bak)) File.Delete(bak);
                    File.Move(path, bak);
                    File.Move(tmp, path);
                    try { File.Delete(bak); }
                    catch (Exception) { }
                }
                catch (Exception)
                {
                    if (!File.Exists(path) && File.Exists(bak))
                    {
                        try { File.Move(bak, path); }
                        catch (Exception) { }
                    }
                    throw;
                }
            }
        }

        public bool Clear()
        {
            lock (_gate) { Items.Clear(); }
            return Save();
        }
    }

    /// Settings live in their own single object file. Same envelope, no list.
    internal static class SettingsStore
    {
        public static AppSettings Load()
        {
            string path = Paths.Store(AppDefaults.StoreSettings);
            if (!File.Exists(path)) return new AppSettings();
            // No catch-all here: a locked key or settings file must not turn
            // into default settings, because that would drop the PIN and let
            // Boot trim the real history under the default cap.
            string text = Crypto.Open(File.ReadAllBytes(path));
            if (text == null)
            {
                throw new InvalidDataException(
                    AppDefaults.StoreSettings + " is unreadable.");
            }
            JVal root = JsonParser.Parse(text);
            if (root == null)
            {
                throw new InvalidDataException(
                    AppDefaults.StoreSettings + " is not valid JSON.");
            }
            return AppSettings.FromJson(root);
        }

        public static void Save(AppSettings s)
        {
            if (s == null) return;
            try
            {
                Paths.EnsureRoot();
                string path = Paths.Store(AppDefaults.StoreSettings);
                string tmp = path + ".tmp";
                File.WriteAllBytes(tmp, Crypto.Seal(JsonWriter.Write(s.ToJson())));
                if (File.Exists(path))
                {
                    try { File.Replace(tmp, path, null); }
                    catch (Exception)
                    {
                        string bak = path + ".bak";
                        try
                        {
                            if (File.Exists(bak)) File.Delete(bak);
                            File.Move(path, bak);
                            File.Move(tmp, path);
                            try { File.Delete(bak); }
                            catch (Exception) { }
                        }
                        catch (Exception)
                        {
                            if (!File.Exists(path) && File.Exists(bak))
                            {
                                try { File.Move(bak, path); }
                                catch (Exception) { }
                            }
                            throw;
                        }
                    }
                }
                else File.Move(tmp, path);
            }
            catch (Exception ex)
            {
                Paths.Log("settings write", ex);
            }
        }
    }
}
