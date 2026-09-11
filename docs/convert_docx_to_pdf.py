#!/usr/bin/env python3
"""Renders docs/ClipSync-AI-Documentation.pdf from the DOCX with reportlab.

Usage:  python docs/convert_docx_to_pdf.py
Input:  docs/ClipSync-AI-Documentation.docx
Output: docs/ClipSync-AI-Documentation.pdf
Requires: pip install reportlab python-docx

Used instead of Word automation because this machine's Word cannot export
PDFs headlessly. Faithful mapping: title, headings, bullets, tables, code.
"""

import os
import re
from xml.sax.saxutils import escape

from docx import Document
from docx.enum.text import WD_ALIGN_PARAGRAPH
from reportlab.lib.colors import HexColor
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import cm
from reportlab.platypus import (
    HRFlowable,
    ListFlowable,
    ListItem,
    PageBreak,
    Paragraph,
    Preformatted,
    SimpleDocTemplate,
    Spacer,
    Table,
    TableStyle,
)
from reportlab.platypus.tableofcontents import TableOfContents

INK = HexColor("#1a1a1c")
SAGE = HexColor("#3f6b52")
MUTED = HexColor("#5c5c60")
HAIRLINE = HexColor("#d8d8dc")
CODE_BG = HexColor("#f2f2f4")
PAGE_W, PAGE_H = A4

styles = getSampleStyleSheet()
s_title = ParagraphStyle("Title2", parent=styles["Title"], fontSize=30,
                         leading=36, textColor=INK, alignment=1, spaceAfter=6)
s_sub = ParagraphStyle("Sub2", parent=styles["Normal"], fontSize=12,
                       leading=16, textColor=MUTED, alignment=1, spaceAfter=20)
s_h1 = ParagraphStyle("H1", parent=styles["Heading1"], fontSize=18,
                      leading=22, textColor=INK, spaceBefore=16, spaceAfter=8,
                      keepWithNext=True)
# Same look, silent in the TOC: the generated Contents header must not list
# itself.
s_h1x = ParagraphStyle("H1x", parent=s_h1)
s_h2 = ParagraphStyle("H2", parent=styles["Heading2"], fontSize=14,
                      leading=18, textColor=SAGE, spaceBefore=10, spaceAfter=6,
                      keepWithNext=True)
s_body = ParagraphStyle("Body2", parent=styles["Normal"], fontSize=12,
                        leading=16, textColor=INK, spaceAfter=6)
s_bullet = ParagraphStyle("Bullet2", parent=s_body, leftIndent=18,
                          bulletIndent=8, spaceAfter=4)
s_cell = ParagraphStyle("Cell", parent=styles["Normal"], fontSize=12,
                        leading=15, textColor=INK)
s_cellh = ParagraphStyle("CellH", parent=s_cell, fontSize=14, leading=17)
s_code = ParagraphStyle("Code2", parent=styles["Code"], fontSize=12,
                        leading=15, textColor=INK, fontName="Courier")
s_cap = ParagraphStyle("Cap2", parent=styles["Normal"], fontSize=12,
                       leading=15, textColor=INK, alignment=1)
s_toc1 = ParagraphStyle("TOC1", parent=styles["Normal"], fontSize=12,
                        leading=16, textColor=INK)
s_toc2 = ParagraphStyle("TOC2", parent=styles["Normal"], fontSize=11,
                        leading=14, textColor=MUTED, leftIndent=16)


def runs_to_html(para):
    out = []
    for r in para.runs:
        t = escape(r.text)
        if r.bold:
            t = f"<b>{t}</b>"
        if r.italic:
            t = f"<i>{t}</i>"
        out.append(t)
    return "".join(out)


def is_code_para(para):
    fonts = {r.font.name for r in para.runs if r.text.strip()}
    return fonts == {"Consolas"}


def has_toc_field(para):
    # The TOC instruction itself, plus Word's cached entry paragraphs (they
    # carry PAGEREF fields). The reportlab TOC below supplies correct numbers.
    xml = para._element.xml
    return "TOC \\o" in xml or "PAGEREF" in xml


def footer(canvas, doc):
    canvas.saveState()
    canvas.setFont("Helvetica", 8)
    canvas.setFillColor(MUTED)
    canvas.drawString(2 * cm, PAGE_H - 1.3 * cm, "ClipSync AI — Documentation")
    canvas.drawRightString(PAGE_W - 2 * cm, 1.3 * cm, f"Page {doc.page}")
    canvas.setStrokeColor(HAIRLINE)
    canvas.setLineWidth(0.5)
    canvas.line(2 * cm, 1.7 * cm, PAGE_W - 2 * cm, 1.7 * cm)
    canvas.restoreState()


def styled_table(headers, rows):
    data = [[Paragraph(f"<b>{escape(h)}</b>", s_cellh) for h in headers]]
    for r in rows:
        data.append([Paragraph(escape(c), s_cell) for c in r])
    t = Table(data, colWidths=[4.5 * cm, 11.5 * cm], repeatRows=1)
    t.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, 0), SAGE),
        ("TEXTCOLOR", (0, 0), (-1, 0), HexColor("#ffffff")),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("GRID", (0, 0), (-1, -1), 0.6, HAIRLINE),
        ("ROWBACKGROUNDS", (0, 1), (-1, -1),
         [HexColor("#ffffff"), HexColor("#f7f9f7")]),
        ("TOPPADDING", (0, 0), (-1, -1), 5),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 5),
        ("LEFTPADDING", (0, 0), (-1, -1), 7),
        ("RIGHTPADDING", (0, 0), (-1, -1), 7),
    ]))
    return t


def code_table(lines):
    t = Table([[Preformatted("\n".join(lines), s_code)]],
              colWidths=[16 * cm])
    t.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, -1), CODE_BG),
        ("BOX", (0, 0), (-1, -1), 0.6, HAIRLINE),
        ("TOPPADDING", (0, 0), (-1, -1), 8),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 8),
        ("LEFTPADDING", (0, 0), (-1, -1), 10),
    ]))
    return t


def build(src, dst):
    srcdoc = Document(src)

    # Walk the body XML in document order so tables land where they belong.
    final = []
    body = srcdoc.element.body
    from docx.table import Table as DxTable
    from docx.text.paragraph import Paragraph as DxPara
    blocks = []
    for child in body.iterchildren():
        if child.tag.endswith("}p"):
            blocks.append(("p", DxPara(child, srcdoc)))
        elif child.tag.endswith("}tbl"):
            blocks.append(("t", DxTable(child, srcdoc)))

    story2 = []
    code_buf = []
    # Word's cached TOC entries sit between the TOC field and the note; the
    # reportlab TOC supplies correct numbers, so the whole region is skipped.
    past_toc = False
    for kind, el in blocks:
        if kind == "t":
            if code_buf:
                story2.append(code_table(code_buf))
                story2.append(Spacer(1, 0.3 * cm))
                code_buf = []
            rows = [[c.text.strip() for c in row.cells]
                    for row in el.rows]
            story2.append(styled_table(rows[0], rows[1:]))
            story2.append(Spacer(1, 0.4 * cm))
            continue
        para, text, style = el, el.text.strip(), el.style.name
        if has_toc_field(para):
            continue
        if style.lower().startswith("toc "):
            # Word's cached TOC entries (written back by an editor session).
            # The reportlab TOC supplies correct numbers.
            continue
        if text.startswith("Right-click") and "Update Field" in text:
            past_toc = True
            continue
        if not past_toc and re.match(r".+[\t ]\d+\s*$", text):
            continue
        if not text:
            if code_buf:
                story2.append(code_table(code_buf))
                story2.append(Spacer(1, 0.3 * cm))
                code_buf = []
            continue
        if is_code_para(para):
            code_buf.append(para.text.rstrip())
            continue
        if code_buf:
            story2.append(code_table(code_buf))
            story2.append(Spacer(1, 0.3 * cm))
            code_buf = []
        if style == "Title":
            story2.append(Spacer(1, 3 * cm))
            story2.append(Paragraph(escape(text), s_title))
        elif para.alignment == WD_ALIGN_PARAGRAPH.CENTER:
            story2.append(Paragraph(runs_to_html(para), s_cap))
        elif style == "Heading 1":
            if text == "Contents":
                # The source's own Contents heading; replaced by the
                # generated one with correct page numbers.
                continue
            story2.append(Paragraph(escape(text), s_h1))
        elif style == "Heading 2":
            story2.append(Paragraph(escape(text), s_h2))
        elif style == "List Bullet":
            story2.append(ListFlowable(
                [ListItem(Paragraph(runs_to_html(para), s_bullet),
                          bulletColor=SAGE, value="•")],
                bulletType="bullet", start="•", leftIndent=18))
        else:
            story2.append(Paragraph(runs_to_html(para), s_body))
    if code_buf:
        story2.append(code_table(code_buf))

    # Real contents page with page numbers.
    toc = TableOfContents()
    toc.levelStyles = [s_toc1, s_toc2]

    out = []
    cover_end = 0
    for i, el in enumerate(story2):
        out.append(el)
        if (isinstance(el, Paragraph) and el.style.name == "Cap2"
                and "reproduces from" not in el.text
                and "Product documentation" in el.text):
            cover_end = len(out)
            break
    head = out[:cover_end]
    rest = story2[cover_end:]
    final_story = head + [PageBreak(), Paragraph("Contents", s_h1x), toc,
                          PageBreak()] + rest

    class DocTemplate(SimpleDocTemplate):
        def afterFlowable(self, flowable):
            if isinstance(flowable, Paragraph):
                if flowable.style.name == "H1":
                    self.notify("TOCEntry",
                                (0, flowable.getPlainText(), self.page))
                elif flowable.style.name == "H2":
                    self.notify("TOCEntry",
                                (1, flowable.getPlainText(), self.page))

    tmpl = DocTemplate(dst, pagesize=A4, leftMargin=2 * cm,
                       rightMargin=2 * cm, topMargin=2 * cm,
                       bottomMargin=2 * cm,
                       title="ClipSync AI — Documentation", author="venudinesh")
    tmpl.multiBuild(final_story, onFirstPage=footer, onLaterPages=footer)


if __name__ == "__main__":
    import os
    here = os.path.dirname(os.path.abspath(__file__))
    src = os.path.join(here, "ClipSync-AI-Documentation.docx")
    dst = os.path.join(here, "ClipSync-AI-Documentation.pdf")
    build(src, dst)
    print(f"wrote {dst} ({os.path.getsize(dst)} bytes)")
