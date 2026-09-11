// The test entry point.
//
// Runs against a scratch directory under the temp folder, so a test run cannot
// touch a real clipboard history. Exit code is the number a build script reads:
// zero for a clean run.

using System;
using System.IO;

namespace ClipSyncAI.Tests
{
    internal static class Runner
    {
        /// Single threaded apartment because part of the suite builds real
        /// controls to ask the shell's own questions of them, and a window is
        /// only ever created on this thread in the app.
        [STAThread]
        public static int Main(string[] args)
        {
            string scratch = Path.Combine(Path.GetTempPath(),
                "ClipSyncAI-tests-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Environment.TickCount);
            Paths.UseRoot(scratch);
            Crypto.Reset();

            Console.WriteLine("ClipSyncAI test suite");
            Console.WriteLine("  scratch: " + scratch);
            Console.WriteLine("  runtime: " + Environment.Version + "  " +
                (IntPtr.Size == 8 ? "64 bit" : "32 bit"));

            int code;
            try
            {
                RegexTests.Run();
                TidyTests.Run();
                JoinTests.Run();
                SecretsTests.Run();
                CoreTests.Run();
                MarkdownTests.Run();
                ShellTests.Run();
                WheelTests.Run();
                PopupTests.Run();
                LaneTests.Run();
                code = T.Report();
            }
            finally
            {
                try
                {
                    if (Directory.Exists(scratch)) Directory.Delete(scratch, true);
                }
                catch (Exception)
                {
                    Console.WriteLine("  note: scratch directory left behind at " + scratch);
                }
            }
            return code;
        }
    }
}
