using System;
using System.Collections.Generic;
using System.Text;
class Probe
{
    static void Main()
    {
        Console.WriteLine("available: " + ClipSyncAI.NativeLoader.Available);
        Console.WriteLine("error: " + (ClipSyncAI.NativeLoader.Available ? "-" : ClipSyncAI.NativeLoader.Error));

        ClipSyncAI.ModelEntry m = null;
        for (int i = 0; i < ClipSyncAI.ModelCatalog.All.Count; i++)
        {
            if (ClipSyncAI.ModelCatalog.All[i].Name.StartsWith("SmolLM2", StringComparison.Ordinal))
            {
                m = ClipSyncAI.ModelCatalog.All[i];
                break;
            }
        }
        Console.WriteLine("catalog entry: " + (m == null ? "missing" : m.Name + " " + m.DisplaySize));

        string target = System.IO.Path.Combine(ClipSyncAI.LocalEngine.ModelsDir, m.File);
        if (!System.IO.File.Exists(target))
        {
            Console.WriteLine("downloading...");
            ClipSyncAI.ModelDownloader.Download(m,
                delegate(long got, long total)
                {
                    Console.WriteLine("  " + (got * 100 / total) + "%");
                }, null);
            Console.WriteLine("download done: " + new System.IO.FileInfo(target).Length + " bytes");
        }
        else
        {
            Console.WriteLine("already present: " + new System.IO.FileInfo(target).Length + " bytes");
        }

        ClipSyncAI.LocalEngine local = new ClipSyncAI.LocalEngine();
        string err;
        string loaded = local.Load(m.File, out err);
        Console.WriteLine("load: " + (loaded ?? "FAIL " + err));

        var msgs = new List<ClipSyncAI.ChatMessage>();
        msgs.Add(new ClipSyncAI.ChatMessage("system", "You are a helpful assistant. Reply briefly."));
        msgs.Add(new ClipSyncAI.ChatMessage("user", "Say OK."));
        StringBuilder streamed = new StringBuilder();
        string reply = local.Chat(m.File, msgs, 64, 0.7, 0.9, null,
            delegate(string piece) { streamed.Append(piece); Console.Write(piece); });
        Console.WriteLine();
        Console.WriteLine("streamed == reply: " + (streamed.ToString() == reply));
        Console.WriteLine("reply length: " + (reply == null ? -1 : reply.Length));
        local.Unload();
        Console.WriteLine("done");
    }
}
