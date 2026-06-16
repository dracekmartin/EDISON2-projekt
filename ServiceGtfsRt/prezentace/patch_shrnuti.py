"""Jednorazovy patch: predela slide 'Shrnutí a další kroky' na
'Chci zavést GTFS — co dál?' (high-level postup zavedeni).

Najde slide podle PUVODNIHO nadpisu (robustni vuci precislovani),
prepise nadpis + odrazky obsahem z build_presentation.SLIDES.
Pracuje NAD POSLEDNI VERZI (default prezentace_v4.pptx -> prezentace_v5.pptx).

Pouziti: python patch_shrnuti.py [vstup.pptx] [vystup.pptx]
"""

import sys
from pathlib import Path
from pptx import Presentation

import build_presentation as b
from patch_presentation import find_bullets_shape
from merge_slides_5_6 import set_title

OLD_TITLE = "Shrnutí a další kroky"
NEW = b.SLIDES[6]   # Chci zavést GTFS — co dál?


def find_slide_by_title(prs, title):
    for s in prs.slides:
        for sh in s.shapes:
            if sh.has_text_frame and sh.text_frame.text.strip() == title:
                return s
    return None


def main():
    src = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).parent / "prezentace_v4.pptx"
    dst = Path(sys.argv[2]) if len(sys.argv) > 2 else Path(__file__).parent / "prezentace_v5.pptx"

    prs = Presentation(str(src))
    slide = find_slide_by_title(prs, OLD_TITLE)
    if slide is None:
        print(f"  ! slide s nadpisem {OLD_TITLE!r} nenalezen — nic nezmeneno")
        return

    set_title(slide, OLD_TITLE, NEW["title"])
    print(f"  nadpis -> {NEW['title']!r}")
    shape = find_bullets_shape(slide)
    if shape is not None:
        b.replace_bullets(shape, NEW["bullets"])
        print(f"  obsah ({len(NEW['bullets'])} bodu)")

    prs.save(str(dst))
    print(f"OK: {dst}  (vstup: {src.name})")


if __name__ == "__main__":
    main()
