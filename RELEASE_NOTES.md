# ClipSyncAI v1.0.0

**The complete interface rebuild.** A new design system, a Liquid Glass
navigation bar built in the iOS 26 idiom with every parameter exposed, soft
low-saturation theming, AMOLED pitch black, a Material / Liquid Glass switch
that opens on Material, a launcher mark and splash screen drawn from the same
palette as the rest of the app, and a full set of gestures so the app can be
driven with one thumb. Every existing feature is intact, every screen gained the
everyday management actions it was missing, and a cold start now reaches the
first frame in around 0.6 s.

Still 100% offline. Still no account, no server, no telemetry.

## Download

| File | Size | Min Android |
|---|---|---|
| `app-release.apk` | 127.5 MB | 7.0 (API 24) |

```
SHA-256  8beb7ebcfe1f17cca1faef6ba5f2a29c2b5c8ef7f4f17fd706542cc19dd5c1b0
```

This is a universal APK: it carries the `llama.cpp` native library for every
architecture, which is where nearly all of the size comes from. Verify the hash
before installing, and expect the "unknown developer" prompt since the build is
not Play-signed.

## Highlights

### Gestures

The app can now be driven with one thumb. Nothing that used to be a button
stopped being one; every gesture is a shortcut to something already on screen.

- **Tap a clip to read it.** A tap on a clip used to open a menu of six verbs
  about a clip you had not read yet, because the feed only ever shows its first
  few lines. It now opens the clip at full length on its own screen, and
  **holding** the row is what reaches the options.
- **Page sideways through your clips.** Inside a clip, drag left or right to
  reach the one before or after it, in the order the feed had them. The header
  prints your position so the gesture is discoverable without a tutorial.
- **Drag a reader down to put it away**, and long press its text to select it.
- **Swipe a row aside to act on it.** Right to pin or unpin, left to delete, on
  clips, notes and chat history alike. A pin springs the row back under your
  finger because the row is still there afterwards; a delete carries it off the
  edge because it is not, and still offers an Undo. The phone ticks once as you
  cross the threshold, so a thumb knows it has gone far enough without the eye
  having to check.
- **Pull the Clips feed down to catch up.** The clipboard monitor only sees a
  copy while it is running, so pulling the feed takes whatever is on the
  clipboard now. Pulling twice does not save the same text twice, and an empty
  clipboard says so instead of failing silently.
- **Swipe between tabs.** The five features are in a fixed order and a swipe
  walks it, however the navigation bar itself is arranged. Anything with its own
  sideways drag — a row being swiped, the chat openers, a settings slider —
  takes the gesture first, so nothing you could already do got harder.

### A header that greets you

The top of the Clips tab used to read "Clipboard intelligence" and never
changed. It now says the time of day, with a short line under it: a reminder to
drink some water, a stray fact about ketchup, or just a nudge to get on with it.

- **It tracks the clock.** Six slices of the day, each with its own greeting and
  its own pool of lines, so a two in the morning reading of the header is not
  the same as a nine in the morning one.
- **It refreshes when you would notice.** On the hour the day part changes, on
  every return to the app, and on a tap. A single timer sleeps until the next
  boundary rather than polling.
- **One in fourteen picks ignores the clock**, so a familiar hour can still
  hand you something you have not read before.
- All of it is a constant in the binary. No network, nothing stored, nothing
  about you inferred.

The layout was rebuilt around it: a larger app mark, the greeting set in the
accent above the app name, and the line below carried on a thin accent rule as a
pull quote. The old subtitle is gone, and no information with it, since the model
state is already reported twice further down the same page.

### A new mark, and the screen it lands on

The launcher icon is no longer a separate drawing that happened to sit next to
the app. It **is** the in-app mark: a rose quartz squircle with a soft light
across the top and a clipboard glyph in warm off-white, generated from the same
`content_paste_rounded` glyph the header renders. One generator writes every
size, so the two cannot drift apart.

- **A proper adaptive icon.** Separate background and foreground layers, so any
  launcher can mask it to its own shape, plus a `monochrome` layer for Android 13
  themed icons. The glyph is sized against the 72 dp region a launcher is
  guaranteed to show rather than the full 108 dp layer, so nothing clips.
- **Full density ladder.** mdpi through xxxhdpi, each step rendered from a
  supersampled master rather than scaled up from a smaller file.
- **The splash uses it too.** Android 12 and up get the platform splash with the
  icon animated over a flat canvas; older versions get the same mark centred in
  the launch window. One mark, one colour, no second copy appearing when the
  system splash hands over.
- **The canvas follows your theme.** Warm off-white `#F7F5F2` in light, off-black
  `#0B0B0D` in dark, resolved by the resource system before a line of Dart runs.

### Launch

A cold start now reaches the first frame in **roughly 0.6 s**, and the launch
window matches the app it is about to become instead of flashing at you.

- **The model no longer races the first frame.** Loading a multi-gigabyte GGUF is
  the heaviest thing the app ever does and nothing on the first screen needs it,
  so it now starts after that screen is on the glass. It used to begin from
  `initState`, in a race with the launch frame that the launch frame lost.
- **Eight key reads became one.** Opening the four encrypted boxes asked the
  keystore twice per box, once to ask whether a key existed and once for the key
  itself. A single `readAll` answers both questions for all four, and only a
  genuine first run touches the store again, to write the keys it had to make.
- **Nothing logs on the launch path in release.** Startup chatter is compiled out
  rather than routed through `debugPrint`, which still writes to logcat in a
  release build.
- **llama.cpp is quiet in release.** Its log level was pinned to debug in every
  build. The FFI logging alone measurably slowed the model load, and a warning is
  all a shipped build has any use for.
- **Two redundant file stats gone.** The model pre-flight checked the file's
  existence and size, then checked both again to fill a log line.

### Chat history

Conversations are saved now, in their own AES-256 box with its own key rather
than riding on the notes key, because a chat can hold anything you pasted into
it.

- **History sheet.** Every saved conversation, pinned first then most recently
  updated, with a preview line and a relative timestamp. Rename, pin, delete or
  clear the lot from inside it.
- **New chat** starts a fresh session without discarding the last one.
- **A title you can change.** The first thing you type becomes the title; tap it
  in the header to rename.
- **A chat sheet** for Rename chat, Summarize conversation, Copy transcript,
  Save transcript to Notes, and Delete chat. It is the same sheet every other tab
  answers its overflow control with, and it can say what an action will do, which
  a dropdown row cannot.
- **Actions under the newest reply.** Copy and Ask again sit under the last
  answer, with the rest a tap further in. Older turns keep the clean measure and
  still open everything on a long press.
- **Openers that leave you the cursor.** The four suggestions on an empty chat
  used to send immediately, which asked the model to summarize nothing. They now
  write the instruction into the composer, put the caret after it and raise the
  keyboard, because the next thing you do on that screen is paste.
- **Per-message actions** on a long press: Copy, Summarize this, Save to Notes,
  Regenerate reply, Delete message. Regenerate throws away the reply and
  everything after it, then re-asks the same question.
- **A stop button.** While a reply streams, the send button becomes stop.
- A restored conversation keeps the prompt the model actually answered, not just
  the text you saw, so follow-up questions about an attached document still work
  after a restart.

### Every list got its management actions

- **Clips.** Hold a row for Copy result, Copy original, Summarize, Add to Notes,
  Pin, Delete, or reach the same sheet from the dot in the margin. Deleting
  offers an Undo.
- **Notes.** A sort menu (recently edited, newest first, by title) with the count
  in the subtitle, a per-card menu (open, pin, copy, summarize, duplicate,
  delete), a word count on the card footer, and an editor overflow menu. The AI
  actions bar gained **Fix Grammar** alongside Summarize, Extract Tasks and
  Expand, dims while a run is in flight, and every result is undoable.
- **OCR and Voice.** Recognised text is **editable before you save it**, because
  recognition is never perfect. The result panel gained a three-dot menu with
  Edit text, Summarize, Send to Clips, Scan again, and Clear result.

### Summarize, everywhere it makes sense

A clip, a note, a note in the editor, an OCR result, a transcription, a single
chat message, or a whole conversation. Summaries of recognised or authored text
are appended under a `## Summary` heading rather than replacing what you had.

Every Summarize entry is hidden unless a model is loaded, so the app never
offers an action that would silently do nothing.

### Export, and safer wipes

- **Export everything** writes clips, notes and chats into one markdown file and
  hands it to the system save dialog, so you choose the destination and nothing
  leaves the device unless you put it somewhere shared. Plain markdown, not a
  private format, so the backup opens in any editor.
- **Clear Clips / Clear Notes / Clear Chats** now confirm first and name how many
  entries will go. Clearing a whole box has no undo, and previously a single
  stray tap was enough.
- **What the phone is holding** now reads as one total with the clips, notes and
  chats split under it, instead of a five-row spec sheet you had to add up
  yourself.

### Liquid Glass navigation bar

A floating capsule rebuilt from scratch. Apple's Liquid Glass is a private GPU
material that bends light through a curved edge rather than merely scattering
it, and Flutter has no equivalent, so this reconstructs the look from a real
backdrop blur, a magnified backdrop sample in a thin band at the rim for the
lens, two hue-shifted rim samples for chromatic aberration, and a painted
specular highlight.

Two engines, and the app tells you which one is actually running:

- **iOS Liquid Glass (Android 13+)** for layered backdrop sampling with a lensed
  rim, refraction and chromatic split.
- **iOS Liquid Glass (Android 8+)** for a single blur pass with painted
  highlights. The two effects it cannot do are disabled and labelled, not faked.

Nine sliders and three toggles, all live against a working preview bar inside
the settings panel:

- **Shape and layout** Bar Width, Bar Height, Bottom Offset, Corner Roundness.
  These work even with glass switched off.
- **Glass effects** Glass Opacity, Blur Intensity, Refraction Depth, Refraction
  Strength, Chromatic Split. Blur at 0 gives clear glass: tint and lensing
  without frost.
- **Gestures** Hold to swipe across the bar to change tabs, Swipe Sensitivity,
  Invert swipe direction. Inverted is the default, because it is the direct
  manipulation reading: the highlight travels with your finger, so dragging
  right reaches the tab on the right.

It ships as clear glass. Bar width 89%, glass opacity 0, blur 0, refraction
depth and strength both 100, chromatic split 0 — no tint and no frost, with the
lensed rim carrying the whole shape. That is the most transparent thing the
sliders can describe, and it needed two supports the earlier build did not have:

- **The row lifts its own contrast.** With no surface of its own between the
  rims, a 56% grey label sits directly on whatever the page has scrolled
  underneath it. Every glyph now carries a soft halo in the canvas colour and
  the idle ink is heavier, so a label holds its own pixels. Over a tint or a
  frost both are dropped, including Material mode and the
  reduce-transparency path, where a halo would only read as a smudge.
- **The page fades into the band the bar floats in.** Apple calls this a scroll
  edge effect, and the point is that it is not the bar's own material —
  raising a label's contrast helps the label and does nothing for the sentence
  it is sitting on top of. Its height is the clearance every page already
  reserves at its bottom, so anything correctly kept clear of the capsule (the
  chat composer, a last row, a floating action) sits at the gradient's zero
  stop and cannot be dimmed. It never quite reaches the canvas either: a hint
  of what is behind the glass is the reason the bar is made of glass. It
  collapses with the keyboard, along with the bar it exists for.

Long press any icon to drag the tabs into whatever order you want. Three scoped
reset buttons: glass effects only, navigation order only, or the whole bar.

A reordered bar cannot lose you a feature. The page view is always built in the
canonical order and the bar renders a permutation of it, and a stored order that
is stale, truncated or duplicated is repaired against the real tab count instead
of trusted.

### Theming

- **Soft accent palette.** Eight swatches, every one deliberately under 80%
  saturation: Rose quartz, Sage, Mist blue, Sand, Clay, Lilac, Butter, Slate.
  Rose quartz is the new default. Accent colours are contrast-lifted before they
  are drawn, so a swatch that would fail WCAG AA on the current background is
  adjusted rather than shipped illegible.
- **AMOLED pitch black.** A toggle for a true `#000000` canvas. Everywhere else
  the app now uses off-black `#0B0B0D`, because pure black makes shadows
  invisible and flattens depth.
- **Material or Liquid Glass.** A switch between two complete looks. **The app
  opens on Material 3**, because a first launch should show the platform look
  before it shows an opinion, and because Material is opaque and flat with no
  blur passes — the faster choice on a budget device. Glass is one toggle away
  and stays on once chosen. Neither is a degraded version of the other, and the
  change applies across every screen at once.
- **Brightness.** Dark, Light, or Follow system, with light mode fixed
  throughout: the white veils and white hairlines that a dark-first theme leaves
  behind on a near-white canvas are gone.

### Redesigned settings

Four tabs instead of one long scroll: **AI**, **Themes & UI**, **System**,
**Data**. Model management, the device tier readout, downloads and the local
GGUF browser all live under AI; everything visual lives under Themes & UI.

Each tab now lands on one raised thing of its own, rather than four pages of the
same hairline rows in a different order.

- **AI opens on the engine.** Whether a model is loaded, the file it came from,
  its size on disk and the runtime, in a panel that carries the accent edge only
  while something is actually loaded. Three rows reading Status / Runtime / Where
  it runs went with it: two of them restated constants.
- **Themes shows you the type first.** The specimen moved above the brightness
  and accent controls instead of sitting three groups below them, so a tap has
  something to change in front of you.
- **System leads with the switch.** Background capture is on the tab whose name
  promises it — the same setting the Clips tab writes, so whichever one you find
  first is the one that works. Readings sit under the control, not above it.
- **Data promotes the export.** Writing your things to a file was the smallest
  target on a page of destructive ones; it is now the page's one panel and its
  one filled button, with the counts it is about to write printed above it.
- **Downloads draw only what has arrived.** The model progress meter marks the
  part that has landed and leaves the rest as paper, rather than filling a grey
  track that reads as a second finished bar behind the first. Sizes past a
  gigabyte print as `2.41 GB` instead of `2458.3 MB`.

### A single design system

Every colour, radius, spacing step and easing curve now resolves through one
token file.

- **Concentric radii.** When a surface sits inside another, its radius is
  derived rather than picked, so nested panels read as machined instead of
  stacked. The shape family is fixed and nothing in the app falls outside it.
- **No outer glows anywhere.** Every accent bloom has been replaced with a
  tinted contact shadow offset downward. State is carried by fill and rim.
- **Custom motion only.** Four house curves, and animation restricted to
  transform and opacity.
- **No pure black or pure white** outside the AMOLED toggle and specular
  highlights.

### Every surface on the same system

The design system above reached the permanent screens first, and it showed
everywhere else. The parts of the interface that are only up for a moment were
the loudest remaining tell that the app had been generated rather than designed,
so they were taken back to the theme.

- **One dialog treatment.** Confirmations each set their own surface colour, their
  own radius and three hand-picked type sizes, and some carried a red glyph
  beside the question while others did not, so the same confirmation arrived
  looking different depending on which tab you asked from. Every dialog now
  resolves from a single `dialogTheme`: the question at title size, the
  consequence in body ink, the destructive action in the danger tone, and nothing
  else competing with either.
- **Toasts clear the navigation bar.** Twenty-one of them set a full-bleed
  background of their own, in four different colours, and came up edge to edge
  underneath the glass capsule. They now arrive on one themed surface with a
  hairline rim, inset from the page and lifted above the bar, so a message never
  covers the control you would reach for next.
- **No decoration standing in for content.** The gradient circle behind every
  empty-state glyph, the tinted plates behind header icons and single facts, the
  shimmer sweeping across every settings switch, and the three coloured dots
  quoting a desktop title bar are all gone. An empty list no longer reads as an
  alert, and three plain facts no longer read as three competing chips.
- **Reduce Motion reaches the last loops.** The progress rule stops travelling
  and simply states that something is running, and switches move without easing
  rather than animating for as long as a job takes.

### The copy stopped shouting

A pass over every user-visible string, on the principle that an interface which
talks like a spec sheet reads as machine-written however well it is drawn.

- **Sentence case throughout.** Engine statuses read "Model loaded", "Loading the
  model" and "Could not load it" instead of Title Case with trailing ellipses. A
  value that arrives already lowercase, as an enum name does, is capitalised
  rather than passed through, so no line in the app opens in lower case.
- **No emoji or glyphs inside prose.** The warning triangles on chat error
  bubbles and the page glyph folded into the model prompt are gone; the sentences
  carry it.
- **Real plurals.** "Sent 3 attachments" rather than "attachment(s)", and clips,
  notes and conversations are counted the same way on every screen.
- **Nothing said twice on one screen.** The device panel listed the phone model,
  the Android version and the performance tier as rows underneath a header that
  already stated all three, one of them shouted in capitals.
- **Failures name what failed, not which routine did.** "Could not read text from
  that image" rather than "OCR failed", and a thrown exception now contributes
  only its first line instead of pasting a stack-shaped string into a toast that
  is 3 seconds wide.
- **No abbreviations the column did not need.** "Estimated speed", not
  "Est. Speed".

### Accessibility

- Reduce Transparency and Increase Contrast collapse glass to an opaque themed
  surface.
- Reduce Motion skips entry animations and stops ambient loops.
- Contrast is checked against WCAG AA in code rather than by eye.
- Switches expose toggled state, reordering is exposed to the accessibility
  tree, and numeric readouts use tabular figures so values do not shift rows as
  they change.

## Unchanged

Nothing was removed to make room for any of this. Clipboard history and the
background service, AI clip processing, notes with tags and markdown, on-device
OCR, voice recording and Whisper transcription, streaming chat with attachments,
the model catalogue and downloads, device tiering and AES-256 encrypted storage
all behave as before.

## Fixes

- **Settings opened with an alarm about nothing.** "No model loaded" was tinted
  the danger colour, but not having picked a model yet is the starting state on a
  fresh install rather than a fault. Red is now kept for a load that genuinely
  failed.
- **One storage row could not be read or acted on.** It printed the absolute
  database directory into a right-aligned meta column, where it ellipsised down
  to nothing and could not be copied anyway. It now states the fact that actually
  matters for this app: the boxes sit inside its own private storage.
- **The device tier rendered as "high tier"**, opening a line in lower case,
  because the label helper returned any input that was not already all-caps
  unchanged.
- The onboarding sequence printed `01 / 04` above a progress rule that already
  showed you where you were.
- **Notes AI actions did nothing visible.** The result was written to the note
  object but never back into the open editor, so Summarize, Expand and Extract
  Tasks all looked like no-ops until you closed and reopened the note.
- **Notes AI actions were also asking the wrong question.** Every instruction was
  routed through the clipboard-to-markdown processor, which wraps its argument in
  "convert the following raw text into clean Markdown". The instruction was
  therefore handed to the model as *data* inside a formatting task rather than as
  the request. AI actions now call the model directly, and return nothing rather
  than falling back to the regex reformatter, so a summary is either a real
  summary or an honest failure.
- Every tab except Clips was cropped at the top. Those pages sat inside a
  `SafeArea` *and* separately added the status bar inset from `MediaQuery`, and
  because a build method's own context sits above the `SafeArea` it inserts, the
  inset was never removed and got paid twice. All five tabs now run edge to edge
  and pad themselves the way Clips already did, so content scrolls under the
  status bar and under the glass capsule instead of stopping at a hard edge.
  Scrollables reserve room for the capsule through one shared token rather than
  a per-page guess.
- The dark launch theme was missing the transparent system bars and
  `fitsSystemWindows="false"` that the light one had, so a cold start in dark
  mode began fitted and jumped down by a status bar once Dart went edge to edge.
- **A cold start in light mode flashed pure black.** The light launch theme drew
  `@android:color/black` while only the dark one drew the branded launch window.
  Both configurations now draw the same layer list, whose canvas colour resolves
  per theme.
- **The splash bar icons were unreadable in light mode.** `windowLightStatusBar`
  and `windowLightNavigationBar` were `false` in both configurations, so white
  icons sat on a near-white splash. They are now set per configuration.
- **Android 12 and up would have shown the mark twice**, once as the platform
  splash icon and again as the launch window's own centred copy, for the frame
  between the system splash dismissing and Flutter's first frame. The API 31
  launch window now draws a flat colour and lets the platform own the icon.
- **A failed keystore read could have opened a plaintext box.** The launch path's
  new batched key read had to distinguish "there is no key yet", which is what
  permits opening a legacy unencrypted box, from "the read failed", which does
  not. Conflating them would have written clipboard and note data to disk in the
  clear after a transient keystore error, so a failed batch read now falls back
  to reading each key individually instead of reporting them absent.
- The navigation bar's first frames were untappable. Its entry animation slid the
  capsule outside its own render box, and Flutter bounds-checks a hit test at
  every level, so the bar painted where it could not be touched. The bar now
  animates inside its own bounds, and resting geometry is unchanged.
- Reordering used the deprecated reorder callback with a manual index fixup,
  which double-corrected the destination on some drags. It now uses
  `onReorderItem`, whose index already accounts for the lifted tile.
- Light mode inherited dark-mode white veils and white hairlines, which are
  invisible on a near-white page. Every one of those surfaces now asks for a
  fill by intent instead of by colour.
- **The capsule floated over the keyboard.** The bar measured the bottom inset
  from inside the `Scaffold` body, and `resizeToAvoidBottomInset` is precisely
  the thing that removes that inset before the body sees it, so the read was
  always zero. The capsule stayed put and sat on top of whatever composer had
  just raised the keys. The measurement moved above the `Scaffold`, the bar
  slides away while the keyboard is up, and every page's bottom clearance
  collapses with it so a composer is not stranded in a band of dead space.
  Floating toasts follow the same rule from the same token.
- **The keyboard sprang back on its own.** Android's Back key hides the keyboard
  without routing the press to the app, so Flutter is never told and the field
  keeps focus. The next `Navigator.pop` — closing a clip, dismissing a sheet —
  handed focus back to that still-focused field and the keyboard rose over a
  page the user had finished typing on. Focus now falls with the inset, and only
  a text field's own node is dropped.
- **Back quit the app from the note editor.** The editor is a state flag on the
  Notes page rather than a route, so Android's Back had nothing of its own to pop
  and closed the app outright — from a screen being typed into. One handler for
  the whole shell now spends the press on an in-place sub view first, then on
  returning to the first tab, and only leaves the app from there.
- **Opening the composer and changing your mind left a note behind.** An
  "Untitled · Empty" row was written every time. A new note with no title, no
  body and no tags is now discarded on close; emptying a note that already
  exists is still an edit, because deleting it out from under you would be a
  surprise.
- **The notes AI strip could be run twice at once.** Tapping Summarize again
  while the first run was still in flight raced two writes at the same note. The
  strip dims and refuses while a run is in flight, and a run that returns
  nothing says so rather than looking like a no-op.
- **A control in the margin read as dropped rather than filed.** A glyph centred
  in a hit box wider than itself stops short of the margin's edge, so a feed
  row's dot did not line up with the timestamp above it. It hangs on the same
  edge now, and the 40x36 target is unchanged — the box is what you tap, not the
  17px glyph.
- **A tinted plate hugged the type inside the clips composer.** The app's input
  theme describes a boxed field because one dialog needs one, and
  `InputDecoration` inherits `filled` from the theme unless it says otherwise.
  Fields that are meant to be a line of type on a rule now say otherwise.
- **The masthead stated one fact twice.** A few of the extracted one-liners name
  the time of day outright, which under a greeting that already said it read like
  a template nobody finished filling in. The line is detected rather than
  deleted: it stays in the pool and simply never gets chosen under the greeting
  it echoes, so "It is late right now." still appears under "Good night."
- **The tab-order strip was sliced by the panel edge.** Five fixed 62px tiles
  need 350 and the card gives about 317, so the fifth chip was cut down the
  middle — a reorder control showing four and a half of five, which is not an
  order anyone can judge. The tile takes whatever fits instead, down to the width
  of its own chip; below that the row scrolls rather than overflows.
- **Errors named the routine, or nothing at all.** A thrown exception now
  contributes only its first line instead of pasting a stack-shaped string into a
  toast that is three seconds wide, and every async path checks it is still
  mounted before it touches state, so leaving a screen mid-run cannot throw.
- **The settings preview bar was a picture of a bar.** It is live: the same
  widget the shell builds, over a striped backdrop so the glass has something to
  refract, and every slider and gesture toggle can be tried against it in place.
  It is also the one bar in the app that deliberately gets no content fade, since
  showing content behind the glass is the entire point of a preview.
- Deprecated Flutter colour and switch APIs replaced throughout.

## Install

1. Download `app-release.apk`.
2. Check the SHA-256 above.
3. Allow installation from unknown sources when prompted.
4. On first launch, open **Settings → AI** and download or pick a model. Nothing
   AI-related works until a model is loaded, and the app will say so rather than
   failing quietly.
5. Start the clipboard service from **Settings → System** if you want history
   captured in the background.

## Requirements

- Android 7.0 (API 24) or newer. The Android 13+ glass engine needs API 33.
- 4 GB RAM for a 1B to 2B model, 6 GB for 3B, 8 GB and up for 7B.
- 1 to 5 GB free for weights.

## Known limits

- The universal APK is large. Build with `--split-per-abi` from source if size
  matters.
- **The splash follows the system, not your in-app theme.** A launch window is
  drawn by Android before any Dart runs, so it cannot read the brightness setting
  stored inside the app. If you set the app to Dark while the system is Light,
  the splash is light and the app arrives dark. AMOLED pitch black is in the same
  position: the launch canvas is off-black `#0B0B0D`, not `#000000`.
- Glass is an approximation of a private GPU material, not a port of it.
- Refraction and chromatic split need API 33.
- Whisper fetches its model on first use.
- Inference speed is your CPU's. The device tier readout is there to set
  expectations before you download.

## Verification

`flutter analyze` reports zero errors. 192 widget and unit tests pass, covering
the navigation bar's gestures and reorder repair, its shipped defaults and the
agreement between the bar's own fallbacks and the stored config, the label lift
and the content fade behind the capsule, the feed row's tap, hold and swipe
contract, the clip reader's paging and its drag to close, glass rendering,
input sanitising, the margin column's alignment and hit target, the fields that
must not inherit a box from the theme, the model layer, the chat-history round
trip and its repair of corrupt rows, the greeting clock and the house rules its
copy has to pass, and the onboarding sequence.

The release build was then verified on a physical device from a wiped install:
the launch window, the launcher icon, the first-run greeting and tutorial, all
five tabs, and a clip taken from raw text through the regex path to a saved,
rendered card. Cold start measured 579 to 622 ms across three runs. The keystore
warning that would indicate an unencrypted fallback did not appear, so the four
boxes opened encrypted on a genuine first run.

The launch window was sampled in both configurations by capturing frames during a
cold start and matching them against the compiled colours: the surround is
`#0B0B0D` with the system in dark and `#F7F5F2` with it in light, with the mark
centred on both and the status bar icons the right polarity for each.

The interface pass was verified the same way rather than from the code, because
the offscreen test renderer and the renderer on the phone do not always agree.
Every tab, all four settings sub-tabs, the chat-history sheet, a toast and a
confirmation dialog were captured from the installed release build. Three
defects were only visible there and are among the fixes above: the red status on
a fresh install, the unreadable path row, and the lowercase tier. A toast was
confirmed to land fully clear of the glass capsule, and the confirmation dialogs
were checked to share one title treatment across the tabs that raise them.

The Chat and Settings surfaces were rebuilt after that capture — the opener
strip, the reply footer, the chat sheet, and the four settings panels described
above — and the gestures came later still. Those ship with `flutter analyze`
clean and the tests green.

Every gesture was then driven by hand on the installed build of this APK, on a
phone, because a drag is the one thing an offscreen renderer cannot be trusted
about. Tapping a clip row opened it at full length; a sideways drag turned the
page and the printed position followed; a drag down put the reader away. On the
feed, dragging a row right revealed the pin and fired it — the header gained a
pinned count, the row rose to the top under its accent rule, and the row sprang
back, since a pin does not remove it. Dragging left revealed the delete in the
danger tone, carried the row off the edge, dropped the kept count by one and
offered an Undo that put it back. Holding a row raised its options sheet, which
read Unpin for the row that was pinned. Pulling the feed down saved what was on
the clipboard, and pulling a second time added nothing. A sideways drag on a
page body changed tabs, and the capsule's marker followed. Short drags were
tried against the row and the feed and moved nothing — not the row, not the
feed, and not the tab either, so a row that takes a sideways drag does not leak
it through to the pager underneath.

Two defects came out of that, one from each direction, which is the argument for
verifying both ways:

- **Only the phone could catch the first.** The drag that puts the reader away
  counted an `OverscrollNotification`, and bouncing physics never sends one — it
  lets the scroll position travel out of range and reports that as an ordinary
  scroll update. The gesture passed offscreen and did nothing at all in the
  hand. It now reads the distance off the metrics, and only while a finger is
  down, so the spring back after a fling is not read as a request to leave.
- **Only the offscreen test could catch the second.** A selection region installs
  a horizontal drag recogniser of its own on touch, so with one inside each
  reader page the deeper recogniser won the arena and the pages stopped turning.
  On the phone it worked anyway, by coincidence: Android reports a touch slop of
  about 8 dp and a scrollable passes it down to its own recognisers, while a
  selection region keeps Flutter's 18 dp default, so the pager tripped first and
  won. Offscreen there is no platform value, both wait 18, and a tie goes to the
  child. Selection now wraps the pager rather than each page, so paging no longer
  rests on which slop the platform happens to report, and a long press still
  selects a word.

Chat and Settings were then captured again, after their rebuild. Chat's header
carries its history, new chat and overflow controls over the no-model state and
a composer with its tone stepper. Each settings sub-tab lands on the one raised
thing it was given: the engine panel on AI, unaccented and in plain ink while
nothing is loaded; the type specimen above the controls on Themes & UI; the
capture switch first with its readings underneath on System, which also prints
`High tier` and `This app's private storage` where the two defective rows used to
be; and the export panel with the counts above its one filled button on Data,
with the destructive rows below under a heading of their own.

One thing is still uncaptured on a phone: the chat opener strip and the reply
footer, which appear only once a model is loaded and so want a multi-gigabyte
download this device has not been given.

The one defect the tests did find is worth naming, because it is the kind that
only a real drag reaches: a row wired to swipe one way only tripped an assertion
inside `Dismissible`, which refuses a second background without a first. Every
feed in the app swipes both ways, so it would not have fired in the shipped
build, but the shared row supports either direction alone and now provides an
empty layer for the side it never travels.

The clear-glass defaults were then read back off the phone rather than off the
source, because a slider's stored value and the pixels it produces are two
different claims. All six show as asked in Themes & UI — bar width 89%, glass
opacity 0, blur 0, refraction depth 100, refraction strength 100, chromatic
split 0 — with invert swipe on and its copy reading "the highlight follows your
finger", and the five tab chips now sit fully inside the tab-order card instead
of the fifth being sliced by the panel edge.

Two rounds were needed on the legibility of a clear capsule, and the first one
is worth recording because it was wrong. The label halo went in first and looked
plausible in a full screenshot; magnified crops of the same band, before and
after, showed it had barely moved anything. A halo in the canvas colour is
nearly invisible over a near-black AMOLED page, and a shadow behind a glyph
cannot remove the sentence beside it. Escalating it would only have turned the
row into a smudge, so the real mechanism — page content passing through a
capsule with no material of its own — got its own fix, and the halo stayed for
what it does do, which is win inside its own pixels. The magnified band after
both shows five labels and five glyphs reading cleanly while the settings row
behind them fades out, and the two composers that pad themselves with the same
clearance token, in Chat and in Clips, were checked on the phone and are
untouched by the fade.

The device was left on Material 3 to match a fresh install, and the app
confirms it: "Running: Solid, Liquid Glass is switched off", with the shape and
layout sliders still holding their values for whenever glass is switched back
on.



