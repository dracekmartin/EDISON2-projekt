"""Jednorazovy patch: slouci 3 Edison slidy do 2.

Slide 6 'GTFS v Edisonu'              -> 'GTFS v Edisonu — jak to funguje' (b.SLIDES[4])
Slide 7 'Architektura toku dat ...'   -> 'Architektura a co je potřeba ke zprovoznění' (b.SLIDES[5])
Slide 8 'Automatizace a dělba rolí'   -> SMAZAT (slouceno do 6 a 7)

Pracuje NAD POSLEDNI VERZI (default prezentace_v3.pptx -> prezentace_v4.pptx).
Pouziti: python merge_edison_slides.py [vstup.pptx] [vystup.pptx]
"""

import sys
from pathlib import Path
from pptx import Presentation

import build_presentation as b
from patch_presentation import find_bullets_shape
from merge_slides_5_6 import set_title, delete_slide, set_page_number

# Stare nadpisy ve v3 (k nalezeni shapu); kdyby se lisily, skript to nahlasi.
OLD_TITLE_EDISON = "GTFS v Edisonu"
OLD_TITLE_ARCH = "Architektura toku dat (Realtime)"

NEW_EDISON = b.SLIDES[4]   # GTFS v Edisonu — jak to funguje
NEW_ARCH = b.SLIDES[5]     # Architektura a co je potřeba ke zprovoznění


def rewrite(slide, old_title, entry, label):
    if set_title(slide, old_title, entry["title"]):
        print(f"  {label}: nadpis -> {entry['title']!r}")
    else:
        print(f"  ! {label}: puvodni nadpis {old_title!r} nenalezen (nadpis neprepsan)")
    shape = find_bullets_shape(slide)
    if shape is not None:
        b.replace_bullets(shape, entry["bullets"])
        print(f"  {label}: obsah ({len(entry['bullets'])} bodu)")


def main():
    src = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).parent / "prezentace_v3.pptx"
    dst = Path(sys.argv[2]) if len(sys.argv) > 2 else Path(__file__).parent / "prezentace_v4.pptx"

    prs = Presentation(str(src))
    slides = list(prs.slides)

    rewrite(slides[5], OLD_TITLE_EDISON, NEW_EDISON, "slide 6")   # index 5
    rewrite(slides[6], OLD_TITLE_ARCH, NEW_ARCH, "slide 7")       # index 6

    if delete_slide(prs, slides[7]):                              # index 7 = Automatizace
        print("  slide 8: smazan (Automatizace slouceno do 6 a 7)")

    total = len(prs.slides)
    n = 0
    for s in prs.slides:
        n += 1
        set_page_number(s, n, total)
    print(f"  precislovano: total = {total}")

    prs.save(str(dst))
    print(f"OK: {dst}  (vstup: {src.name})")


if __name__ == "__main__":
    main()
