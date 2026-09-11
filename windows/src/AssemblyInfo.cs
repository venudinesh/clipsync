// Assembly identity.
//
// A binary with no name, no version and no copyright is exactly what malware
// looks like to a heuristic scanner, and this app does the things heuristics
// count: it listens to the clipboard, registers global hotkeys, captures the
// screen and can start with Windows. Every one of those is done through the
// documented API and only when the user asks, but the scanner cannot see that.
// What it can see is this block: a product name, a company, a version and a
// copyright, embedded by the compiler as the file's version resource. That is
// what shows in Explorer properties and Task Manager, and it is the cheapest
// legit signal a binary can carry. (A code-signing certificate would be the
// expensive one; this project has none.)
//
// Keep the version in step with pubspec.yaml by hand.

using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("ClipSyncAI")]
[assembly: AssemblyDescription("Offline clipboard intelligence for Windows. " +
    "Captures, tidies, summarises and files clipboard content on this PC only. " +
    "No account, no server, no telemetry.")]
[assembly: AssemblyCompany("ClipSyncAI")]
[assembly: AssemblyProduct("ClipSyncAI")]
[assembly: AssemblyCopyright("Copyright (c) 2026 ClipSyncAI contributors (MIT)")]
[assembly: AssemblyVersion("1.1.1.0")]
[assembly: AssemblyFileVersion("1.1.1.0")]

// Nothing in this program is exposed to COM.
[assembly: ComVisible(false)]
