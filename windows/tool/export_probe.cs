using System;
using System.Runtime.InteropServices;
class Probe
{
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    static extern IntPtr GetProcAddress(IntPtr module, string name);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr LoadLibrary(string path);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool SetDllDirectory(string path);

    static void Main()
    {
        string dir = @"C:\Users\Toji\AppData\Roaming\ClipSyncAI\native\x64";
        SetDllDirectory(dir);
        IntPtr h = LoadLibrary(System.IO.Path.Combine(dir, "llama.dll"));
        Console.WriteLine("handle: " + h + "  lastError: " + Marshal.GetLastWin32Error());
        if (h != IntPtr.Zero)
        {
            IntPtr p = GetProcAddress(h, "llama_backend_init");
            Console.WriteLine("llama_backend_init: " + p + "  lastError: " + Marshal.GetLastWin32Error());
        }
        IntPtr ggml = LoadLibrary(System.IO.Path.Combine(dir, "ggml.dll"));
        Console.WriteLine("ggml handle: " + ggml + "  lastError: " + Marshal.GetLastWin32Error());
        if (ggml != IntPtr.Zero)
        {
            IntPtr p = GetProcAddress(ggml, "ggml_backend_load_all_from_path");
            Console.WriteLine("ggml_backend_load_all_from_path: " + p);
        }
    }
}
