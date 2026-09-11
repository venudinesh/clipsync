#!/usr/bin/env python3
"""Generates docs/ClipSync-AI-Documentation.docx with python-docx.

Usage:  python docs/generate_docs_docx.py
Output: docs/ClipSync-AI-Documentation.docx
Requires: pip install python-docx

Open in Word/LibreOffice and accept the table-of-contents update prompt.
"""

from docx import Document
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml.ns import qn
from docx.oxml import OxmlElement
from docx.shared import Pt, Cm, RGBColor

SAGE = RGBColor(0x3F, 0x6B, 0x52)
INK = RGBColor(0x1A, 0x1A, 0x1C)
MUTED = RGBColor(0x5C, 0x5C, 0x60)

doc = Document()

# ── Base styles ──────────────────────────────────────────────────────────
normal = doc.styles["Normal"]
normal.font.name = "Calibri"
normal.font.size = Pt(12)
normal.font.color.rgb = INK
normal.paragraph_format.space_after = Pt(6)
normal.paragraph_format.line_spacing = 1.2

for name, size, leading, color, before in (
    ("Heading 1", 18, 22, INK, 18),
    ("Heading 2", 14, 18, SAGE, 12),
):
    st = doc.styles[name]
    st.font.name = "Calibri"
    st.font.size = Pt(size)
    st.font.bold = True
    st.font.color.rgb = color
    st.paragraph_format.space_before = Pt(before)
    st.paragraph_format.space_after = Pt(6)
    st.paragraph_format.keep_with_next = True

title_style = doc.styles["Title"]
title_style.font.size = Pt(34)
title_style.font.color.rgb = INK
title_style.paragraph_format.alignment = WD_ALIGN_PARAGRAPH.CENTER


def h1(text):
    doc.add_heading(text, level=1)


def h2(text):
    doc.add_heading(text, level=2)


def p(text):
    doc.add_paragraph(text)


def bullets(items):
    for t in items:
        doc.add_paragraph(t, style="List Bullet")


def table(headers, rows):
    t = doc.add_table(rows=1 + len(rows), cols=len(headers))
    t.style = "Light Grid Accent 1"
    for i, htxt in enumerate(headers):
        cell = t.rows[0].cells[i]
        cell.text = ""
        run = cell.paragraphs[0].add_run(htxt)
        run.bold = True
        run.font.size = Pt(14)
    for r, row in enumerate(rows, start=1):
        for i, txt in enumerate(row):
            t.rows[r].cells[i].text = txt
    doc.add_paragraph()


def code_block(text):
    for line in text.split("\n"):
        para = doc.add_paragraph()
        para.paragraph_format.space_after = Pt(0)
        para.paragraph_format.space_before = Pt(0)
        run = para.add_run(line if line else " ")
        run.font.name = "Consolas"
        run.font.size = Pt(12)
        shading = OxmlElement("w:shd")
        shading.set(qn("w:fill"), "F2F2F4")
        para.paragraph_format.element.append(shading)
    doc.add_paragraph()


def add_toc():
    para = doc.add_paragraph()
    run = para.add_run()
    fld1 = OxmlElement("w:fldChar")
    fld1.set(qn("w:fldCharType"), "begin")
    instr = OxmlElement("w:instrText")
    instr.set(qn("xml:space"), "preserve")
    instr.text = 'TOC \\o "1-2" \\h \\z \\u'
    fld2 = OxmlElement("w:fldChar")
    fld2.set(qn("w:fldCharType"), "end")
    run._r.append(fld1)
    run2 = para.add_run()
    run2._r.append(instr)
    run3 = para.add_run()
    run3._r.append(fld2)
    # Run-level formatting on purpose: touching the shared Normal style here
    # would shrink and grey every paragraph after this note.
    note = doc.add_paragraph()
    nrun = note.add_run(
        "Right-click → Update Field in Word to fill the page numbers.")
    nrun.font.size = Pt(10)
    nrun.font.color.rgb = MUTED


# ── Cover ────────────────────────────────────────────────────────────────
doc.add_paragraph().add_run().add_break()
doc.add_heading("ClipSync AI", level=0)
sub = doc.add_paragraph(
    "Your clipboard, with a brain — and nothing ever leaves your machine.")
sub.alignment = WD_ALIGN_PARAGRAPH.CENTER

cap = doc.add_paragraph(
    "Product documentation: what the app does, how it keeps your data "
    "yours, and how to build and verify it yourself.")
cap.alignment = WD_ALIGN_PARAGRAPH.CENTER

doc.add_page_break()
h1("Contents")
add_toc()
doc.add_page_break()

# ── 1 ──
h1("1  Why this exists")
p("Every “smart clipboard” app takes the most sensitive thing on your "
  "device — your clipboard, full of passwords, one-time codes, half-written "
  "messages, medical notes — and ships it off to somebody else’s server. "
  "That is backwards. ClipSync AI does all of its thinking right there on "
  "your own hardware.")
p("No account. No server. No telemetry. No “we value your privacy” page "
  "that means nothing. Just an app that works with the Wi-Fi off.")
h2("At a glance")
table(
    ["Platform", "What you get", "Needs"],
    [
        ["Android 7.0+", "Full app: clips, chat, notes, OCR, voice, share "
         "sheet, quick tile", "4 GB RAM, 1–5 GB for models"],
    ])

# ── 2 ──
h1("2  One app, on your phone")
p("ClipSync AI is an Android app that loads a GGUF model straight into "
  "llama.cpp inside its own process. Your phone becomes the AI server: "
  "Qwen 0.5B up to Llama 7B, depending on RAM.")
bullets([
    "No account, no server, no telemetry — it works with the Wi-Fi off.",
    "With no model at all, a built-in engine still cleans, classifies and "
    "tags every clip, and the app is honest about which buttons need a "
    "model and hides the ones that don't.",
    "It has never sent a single clip anywhere. That is not a setting — "
    "there is simply no code that does it.",
])

# ── 3 ──
h1("3  What it does, in plain words")
h2("Clips")
bullets([
    "Automatic capture — everything you copy is kept by a quiet background "
    "service.",
    "Search that understands — keyword search always works, ranked by "
    "relevance (titles beat tags, tags beat bodies, rare words beat common "
    "ones). Load the small indexing model and the feed ranks by meaning: "
    "“that AWS key from last week” finds the key.",
    "Titles and tags at capture — written by the model when one is loaded, "
    "by keywords otherwise.",
    "Join clips — pick two or more and merge them into one, oldest first, "
    "with a divider between parts.",
    "Redact secrets — one tap masks API keys, tokens, passwords, JWTs and "
    "card numbers; auto-redact masks them before a clip is ever saved.",
    "Duplicate sense — saving something twice asks: keep both, merge into "
    "the earlier clip, or discard.",
    "Clean up, summarise, retitle, pin, copy — with undo where it counts.",
])
h2("Chat, Notes, Capture")
bullets([
    "Chat — a real streamed conversation with the on-device model. Attach "
    "documents, OCR-read photos, or transcribed voice notes. Cloud "
    "providers (OpenAI, Anthropic, Gemini and others) are available only "
    "through an explicit, twice-confirmed opt-in.",
    "Notes — a markdown editor with tags, search, and an AI bar "
    "(summarise, expand, fix grammar, action items), every action undoable.",
    "Capture — camera OCR, dictation, and results you can edit before "
    "saving. Share text into Clips from any app’s share sheet, or file the "
    "clipboard from a Quick Settings tile without opening the app.",
])
h2("Privacy controls")
bullets([
    "App lock — a PIN over the whole app, asked on launch and on every "
    "return from the background. Fingerprint or face unlocks it where the "
    "hardware exists.",
    "Clip lifetime — keep clips forever, a day, a week, or a month. "
    "Expired, unpinned clips burn on launch. Pinned clips are never dropped.",
    "Export or wipe — everything out as markdown or JSON, or gone with "
    "confirmation.",
])

# ── 4 ──
h1("4  How your stuff stays yours")
bullets([
    "On-device inference. The model file lives in your app folder. Words go "
    "in, answers come out, nothing in between touches a network.",
    "Encrypted storage. AES-256 throughout — clips, notes, chats, settings, "
    "each with its own key, and the keys live in the system keystore.",
    "Secrets never land. Auto-redact, per-clip redaction, and lifetimes "
    "compose into real defence in depth.",
    "The ten-second audit. Turn on airplane mode. Everything keeps working "
    "except model downloads.",
])
h2("Honest limits")
bullets([
    "Models are hungry: small ones want 4 GB of RAM, bigger ones 6–8 GB+, "
    "plus room for the weight files.",
    "The background service shows a quiet notification — Android’s rule "
    "for anything that keeps working while the app is closed.",
])

# ── 5 ──
h1("5  Build it yourself")
h2("Android")
code_block("flutter pub get\n"
           "flutter build apk --release   # → build/app/outputs/flutter-apk/")
h2("Check the work")
code_block("flutter analyze && flutter test   # 249 Flutter tests")

# ── 6 ──
h1("6  Project map")
table(
    ["Path", "What lives there"],
    [
        ["lib/main.dart", "The app: clips feed, chat, notes, capture, "
         "settings, lock screen"],
        ["lib/services/", "On-device LLM, embeddings, biometrics, "
         "storage, catalogues"],
        ["lib/features/ + lib/ui/", "History, sheets, and the Liquid "
         "Glass design system"],
        ["android/", "Native shell: service, share sheet, quick tile"],
        ["test/", "The test suite"],
    ])

# ── 7 ──
h1("7  License")
p("MIT — do what you like, just keep the notice.")
p("This document describes the source as it stands. There are no binary "
  "releases attached to the repository; every build above reproduces from "
  "this tree.")

if __name__ == "__main__":
    import os
    here = os.path.dirname(os.path.abspath(__file__))
    out = os.path.join(here, "ClipSync-AI-Documentation.docx")
    doc.save(out)
    print(f"wrote {out} ({os.path.getsize(out)} bytes)")
