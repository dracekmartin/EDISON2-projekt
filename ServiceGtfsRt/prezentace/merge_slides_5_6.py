"""Jednorazovy patch: slouci slide 5 (Jak se data dostanou na Google) a
slide 6 (Zpusoby realizace) do jednoho souhrnneho slidu, smaze druhy a
precisluje stranky. Pracuje NAD POSLEDNI VERZI (default prezentace_v2.pptx).

Pouziti:
    python merge_slides_5_6.py                       # v2 -> v3
    python merge_slides_5_6.py vstup.pptx vystup.pptx
"""

import re
import sys
from pathlib import Path
from pptx import Presentation

import build_presentation as b   # merged obsah je b.SLIDES[3]
from patch_presentation import find_bullets_shape

R_ID = "{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id"
PAGE_RE = re.compile(r"^\s*\d+\s*/\s*\d+\s*$")

MERGED_TITLE = b.SLIDES[3]["title"]
MERGED_BULLETS = b.SLIDES[3]["bullets"]
OLD_TITLE = "Jak se data dostanou na Google"


def set_title(slide, old_text, new_text):
    for sh in slide.shapes:
        if sh.has_text_frame and sh.text_frame.text.strip() == old_text:
            para = sh.text_frame.paragraphs[0]
            if para.runs:
                para.runs[0].text = new_text
                for r in para.runs[1:]:
                    r.text = ""
            return True
    return False


def delete_slide(prs, slide):
    """Smaze slide z prezentace (sldIdLst + relationship)."""
    id_lst = prs.slides._sldIdLst
    for sldId in list(id_lst):
        if prs.part.related_part(sldId.get(R_ID)) is slide.part:
            prs.part.drop_rel(sldId.get(R_ID))
            id_lst.remove(sldId)
            return True
    return False


def set_page_number(slide, page, total):
    """Najde ramecek '<n> / <m>' a nastavi nove cislo + total."""
    for sh in slide.shapes:
        if not sh.has_text_frame:
            continue
        if PAGE_RE.match(sh.text_frame.text):
            para = sh.text_frame.paragraphs[0]
            runs = para.runs
            if len(runs) >= 2:
                runs[0].text = str(page)
                runs[1].text = f" / {total}"
                for r in runs[2:]:
                    r.text = ""
            elif runs:
                runs[0].text = f"{page} / {total}"
            return True
    return False


def main():
    src = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).parent / "prezentace_v2.pptx"
    dst = Path(sys.argv[2]) if len(sys.argv) > 2 else Path(__file__).parent / "prezentace_v3.pptx"

    prs = Presentation(str(src))
    slides = list(prs.slides)

    # 1) slide 5 (index 4): novy nadpis + slouceny obsah
    slide5 = slides[4]
    if set_title(slide5, OLD_TITLE, MERGED_TITLE):
        print(f"  slide 5: nadpis -> {MERGED_TITLE!r}")
    shape = find_bullets_shape(slide5)
    if shape is not None:
        b.replace_bullets(shape, MERGED_BULLETS)
        print(f"  slide 5: slouceny obsah ({len(MERGED_BULLETS)} bodu)")

    # 2) smazat slide 6 (index 5): Zpusoby realizace
    if delete_slide(prs, slides[5]):
        print("  slide 6: smazan (Zpusoby realizace slouceny do slide 5)")

    # 3) precislovat stranky
    total = len(prs.slides)
    n = 0
    for s in prs.slides:
        n += 1
        if set_page_number(s, n, total):
            pass
    print(f"  precislovano: total = {total}")

    prs.save(str(dst))
    print(f"OK: {dst}  (vstup: {src.name})")


if __name__ == "__main__":
    main()
