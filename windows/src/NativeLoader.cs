// The bundled llama.cpp runtime: extraction and binding.
//
// The x64 executable carries llama.dll and its friends as embedded resources so
// the build stays a single file. On first use they are written out — beside the
// executable in portable mode, otherwise under %LOCALAPPDATA%\ClipSyncAI\native
// where the account can write without an administrator. Every entry point is
// resolved through GetProcAddress, so a missing or mismatched runtime fails as
// a readable line in Settings instead of a startup crash.
//
// The runtime is the CPU build of llama.cpp b10809 (the engine Ollama itself is
// built on). Recent llama.cpp releases ship x64 only, so the 32-bit executable
// reports that embedded models need the 64-bit build and keeps every other
// engine.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ClipSyncAI
{
    internal static class NativeLoader
    {
        /// The native DLLs that have to sit together for llama.dll to load.
        private static readonly string[] Payload =
        {
            "llama.dll",
            "ggml.dll",
            "ggml-base.dll",
            "ggml-rpc.dll",
            "libomp.dll",
            "ggml-cpu-alderlake.dll",
            "ggml-cpu-cannonlake.dll",
            "ggml-cpu-cascadelake.dll",
            "ggml-cpu-cooperlake.dll",
            "ggml-cpu-haswell.dll",
            "ggml-cpu-icelake.dll",
            "ggml-cpu-ivybridge.dll",
            "ggml-cpu-piledriver.dll",
            "ggml-cpu-sandybridge.dll",
            "ggml-cpu-sapphirerapids.dll",
            "ggml-cpu-skylakex.dll",
            "ggml-cpu-sse42.dll",
            "ggml-cpu-x64.dll",
            "ggml-cpu-zen4.dll",
        };

        private static readonly object Gate = new object();
        private static IntPtr _handle;
        private static IntPtr _ggmlHandle;
        private static string _dir;
        private static string _error = "";

        public static bool Available
        {
            get
            {
                lock (Gate)
                {
                    if (_handle != IntPtr.Zero) return true;
                    TryLoad();
                    return _handle != IntPtr.Zero;
                }
            }
        }

        public static string Error
        {
            get
            {
                lock (Gate)
                {
                    if (_handle != IntPtr.Zero) return "";
                    TryLoad();
                    return _error;
                }
            }
        }

        public static string Dir
        {
            get
            {
                lock (Gate)
                {
                    if (_dir == null) _dir = ResolveDir();
                    return _dir;
                }
            }
        }

        private static string ResolveDir()
        {
            if (Paths.Portable) return Path.Combine(Paths.ExeDir, "native");
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ClipSyncAI", "native", "x64");
        }

        private static void TryLoad()
        {
            if (_error.Length > 0) return;
            try
            {
                if (IntPtr.Size != 8)
                {
                    _error = "Embedded models need the 64-bit build of ClipSyncAI.";
                    return;
                }

                Assembly asm = typeof(NativeLoader).Assembly;
                string dir = Dir;
                Directory.CreateDirectory(dir);

                // Write anything that is missing or has grown (a partial write
                // from a killed first run must not be accepted).
                foreach (string name in Payload)
                {
                    string target = Path.Combine(dir, name);
                    if (!File.Exists(target) || new FileInfo(target).Length == 0)
                    {
                        using (Stream s = asm.GetManifestResourceStream(name))
                        {
                            if (s == null)
                            {
                                _error = "The runtime resource \"" + name +
                                    "\" is missing from this executable.";
                                return;
                            }
                            using (FileStream f = new FileStream(target, FileMode.Create,
                                FileAccess.Write, FileShare.None))
                            {
                                byte[] buf = new byte[65536];
                                int n;
                                while ((n = s.Read(buf, 0, buf.Length)) > 0) f.Write(buf, 0, n);
                            }
                        }
                    }
                }

                // Dependencies are found relative to llama.dll's own directory,
                // which the loader only searches once it has been told about it.
                SetDllDirectory(dir);
                IntPtr h = LoadLibrary(Path.Combine(dir, "llama.dll"));
                if (h == IntPtr.Zero)
                {
                    // Load the pieces in dependency order; the error is far more
                    // specific than a bare 0x7E.
                    foreach (string name in Payload)
                    {
                        if (name == "llama.dll") continue;
                        if (LoadLibrary(Path.Combine(dir, name)) == IntPtr.Zero)
                        {
                            _error = "The bundled runtime could not load \"" + name +
                                "\" (" + Marshal.GetLastWin32Error() + ").";
                            return;
                        }
                    }
                    h = LoadLibrary(Path.Combine(dir, "llama.dll"));
                }
                if (h == IntPtr.Zero)
                {
                    _error = "The bundled llama.cpp runtime could not be loaded (" +
                        Marshal.GetLastWin32Error() + ").";
                    return;
                }
                _handle = h;
            }
            catch (Exception ex)
            {
                _error = ex.Message;
            }
        }

        /// Resolves one export. Only called after [Available] is true.
        public static T Bind<T>(string name) where T : class
        {
            IntPtr p = GetProcAddress(_handle, name);
            if (p == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    "The bundled runtime is missing the export \"" + name + "\". " +
                    "It may be an older or newer build than expected.");
            }
            return Marshal.GetDelegateForFunctionPointer(p, typeof(T)) as T;
        }

        /// Resolves an export from one of the other bundled DLLs (the ggml
        /// backend lives in ggml.dll, not llama.dll). The module must already
        /// be loaded, which [Available] guarantees.
        public static T BindFrom<T>(string moduleName, string name) where T : class
        {
            lock (Gate)
            {
                if (moduleName == "ggml.dll" && _ggmlHandle == IntPtr.Zero)
                {
                    _ggmlHandle = LoadLibrary(Path.Combine(Dir, "ggml.dll"));
                }
                IntPtr module = moduleName == "ggml.dll" ? _ggmlHandle : _handle;
                IntPtr p = GetProcAddress(module, name);
                if (p == IntPtr.Zero)
                {
                    throw new InvalidOperationException(
                        "The bundled runtime is missing the export \"" + name +
                        "\" in " + moduleName + ".");
                }
                return Marshal.GetDelegateForFunctionPointer(p, typeof(T)) as T;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string path);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string name);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string path);
    }
}
