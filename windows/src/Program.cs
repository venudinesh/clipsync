// The entry point.
//
// Three things have to happen in this order and nowhere else: tell Windows the app
// scales itself, before any window exists; work out the scale, because every font
// and every measurement is built from it; and make sure this is the only copy
// running, because two clipboard listeners would file every copy twice.

using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace ClipSyncAI
{
    internal static class Program
    {
        /// Per user rather than global, so two people signed in at the same time
        /// each get a copy instead of one of them being told it is already
        /// running.
        private const string Only = "ClipSyncAI.single.v1";

        [STAThread]
        private static void Main()
        {
            Native.DeclareDpiAwareness();
            bool first;
            Mutex one = new Mutex(true, Only, out first);
            try
            {
                if (!first)
                {
                    // Already running: ask that copy to come forward and stop.
                    Native.BroadcastShow();
                    return;
                }
                Run();
            }
            finally
            {
                if (first) { try { one.ReleaseMutex(); } catch (Exception) { } }
                one.Close();
            }
        }

        private static void Run()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnUiFault;
            AppDomain.CurrentDomain.UnhandledException += OnFault;
            Theme.Scale = Desktop();
            try
            {
                Application.Run(new AppWindow());
            }
            catch (Exception ex)
            {
                Paths.Log("fatal", ex);
                throw;
            }
        }

        /// The desktop's scale, read before any window exists. Windows 7 has no
        /// per window call, and the first layout has to be built from something.
        /// The window re-reads it from its own monitor once it has a handle.
        private static double Desktop()
        {
            try
            {
                using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    double s = g.DpiX / 96.0;
                    return s >= 0.75 && s <= 5.0 ? s : 1.0;
                }
            }
            catch (Exception) { return 1.0; }
        }

        /// A fault on the interface thread. This is a tray utility holding work
        /// nobody has finished with, so it says what happened and carries on
        /// rather than vanishing and taking a page of dictation with it.
        private static void OnUiFault(object sender, ThreadExceptionEventArgs e)
        {
            Paths.Log("unhandled on the interface thread", e.Exception);
            Grumble();
        }

        private static void OnFault(object sender, UnhandledExceptionEventArgs e)
        {
            Paths.Log("unhandled", e.ExceptionObject as Exception);
        }

        private static void Grumble()
        {
            try
            {
                MessageBox.Show(
                    "Something went wrong. What happened was written to:" +
                    Environment.NewLine + Environment.NewLine + Paths.LogFile,
                    "ClipSyncAI", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception) { }
        }
    }
}
