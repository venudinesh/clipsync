# ClipSync AI v1.1.0

**Your clipboard, with a brain — and nothing ever leaves your machine.**

A privacy-first clipboard app that runs every model on your own hardware. No account, no
telemetry, no cloud jamboree. Everything below is **100% offline-capable**; the optional AI
provider connections are strictly opt-in, twice.

## What's new in v1.1.0

**Windows**
- Replies are now cut cleanly at template markers — no more model chatter bleeding past the end of an answer.
- Enter now sends the chat message (Shift+Enter stays a newline).
- Voice recordings are transcribed **on device** through the embedded llama.cpp runtime instead of echoing file metadata.

**Android (Flutter app)**
- New **Model connection** dialog with one-tap presets for OpenAI, Gemini, Claude and other OpenAI-compatible providers, plus a custom gateway option — an explicit opt-in before any chat data leaves the phone.
- Connection dialog widened (provider list no longer feels like a slot window).
- Voice recording writes proper WAV audio so transcription actually hears you.
- New Linux and macOS desktop runners, with a full GitHub Actions pipeline that renders `.deb`, `.rpm`, `.tar.xz`, and `.dmg`/`.zip` for both Apple Silicon and Intel — see the [README](README.md#build-for-linux--macos-github-actions).

## Downloads

| Platform | File | Size | SHA-256 |
|:--|:--|--:|:--|
| Windows | `ClipSyncAI-x64.exe` | ~23 MB | see checksums below |
| Windows | `ClipSyncAI-x86.exe` | ~23 MB | see checksums below |
| Linux .deb | `clip-sync-ai_1.0.0_amd64.deb` | via [Actions](https://github.com/venudinesh/clipsync/actions) | — |
| Linux .rpm | `clip-sync-ai_1.0.0.x86_64.rpm` | via [Actions](https://github.com/venudinesh/clipsync/actions) | — |
| macOS (Apple Silicon) | `clip_sync_ai-macos-arm64.dmg/.zip` | via [Actions](https://github.com/venudinesh/clipsync/actions) | — |
| macOS (Intel) | `clip_sync_ai-macos-x64.dmg/.zip` | via [Actions](https://github.com/venudinesh/clipsync/actions) | — |

Linux and macOS desktop binaries are produced by the
[`desktop.yml`](.github/workflows/desktop.yml) workflow — push to `main` or use
**Actions → Desktop builds → Run workflow**, then grab the artifacts from the run page.

## How your stuff stays yours

- **On-device inference.** Every model runs inside the app. Turn off the Wi-Fi mid-demo if you like; the only thing that stops is model downloads.
- **Encrypted storage.** AES-256, per-domain keys, sealed to your device.
- **No exfiltration code.** There is no code path that exports your clips. Not a setting — just the absence of a feature.

## Platform notes

- **Windows** — no installer, no admin rights, .NET Framework 4.0+ only. 64-bit recommended for embedded models. Tray icon, global hotkeys (`Ctrl+Shift+V` summon, `Ctrl+Shift+C` tidy).
- **macOS** — unsigned builds are ad-hoc signed so they launch; on first run use right-click → **Open**. Voice transcription works (Whisper ships for macOS); Linux currently hides the voice button.
- **Linux** — `.deb` depends on `libgtk-3-0`, `libsecret-1-0`, `zenity`; the `.rpm` mirrors those as Requires.

## Honest note

Fresh unsigned builds sometimes trip antivirus (Malwarebytes in particular) — a clipboard-watching program *sounds* like spyware until you read the code. Everything uses official documented APIs, asks for no admin rights, and ships full version identity. Allowlist it, or [report the false positive](https://www.malwarebytes.com/support/false-positive).

## Checksums

Computed on the release machine for the attached Windows executables:

```
ClipSyncAI-x64.exe  0BB56E68685B73765C662B17EF87E4BFDA9C9E8E6F8B9A50DACF63942EEBE374
ClipSyncAI-x86.exe  C2623AC5BAD5CD72A14A630FAB67AC4778E52EA9B45EC83B8985CF2C93ED2D80
```