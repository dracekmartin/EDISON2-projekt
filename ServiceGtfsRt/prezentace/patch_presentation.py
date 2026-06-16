"""Inkrementalni patch prezentace — pracuje NAD POSLEDNI VERZI, ne od nuly.

Na rozdil od build_presentation.py (ktery staví celou prezentaci ze vzoru a
prepsal by rucni upravy) tenhle skript nacte existujici .pptx a zmeni jen to,
co je v PATCH_SLIDES — vsechno ostatni (autor, nadpisy, rucni upravy) zustava.

Pouziti:
    python patch_presentation.py                       # prezentace.pptx -> prezentace_v2.pptx
    python patch_presentation.py vstup.pptx vystup.pptx

PATCH_SLIDES: {cislo_slidu (1-based): index do build_presentation.SLIDES}
Bod (odrazka) muze byt string (hlavni bod) nebo {"t": "...", "sub": [...]} (s podbody).
"""

import sys
from pathlib import Path
from pptx import Presentation

import build_presentation as b   # sdili SLIDES + replace_bullets (hierarchie odrazek)

# Slidy k patchnuti v teto vlne: hierarchicke odrazky na 3/4/5.
# Hodnota = index do b.SLIDES (SLIDES[0]=slide2, takze slide N -> SLIDES[N-2]).
PATCH_SLIDES = {
    3: 1,   # Statické GTFS
    4: 2,   # GTFS Realtime
    5: 3,   # Jak se data dostanou na Google
}


def find_bullets_shape(slide):
    """Odrazkovy ramecek = text frame s nejvic neprazdnymi odstavci
    (nadpis/zahlavi/paticka maji 1, odrazky 4-6)."""
    best, best_count = None, 1
    for sh in slide.shapes:
        if not sh.has_text_frame:
            continue
        count = sum(1 for p in sh.text_frame.paragraphs if p.text.strip())
        if count > best_count:
            best, best_count = sh, count
    return best


def main():
    src = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).parent / "prezentace.pptx"
    dst = Path(sys.argv[2]) if len(sys.argv) > 2 else Path(__file__).parent / "prezentace_v2.pptx"

    prs = Presentation(str(src))
    slides = list(prs.slides)

    for slide_no, slides_idx in PATCH_SLIDES.items():
        slide = slides[slide_no - 1]
        bullets = b.SLIDES[slides_idx]["bullets"]
        shape = find_bullets_shape(slide)
        if shape is None:
            print(f"  ! slide {slide_no}: odrazkovy ramecek nenalezen — preskakuji")
            continue
        b.replace_bullets(shape, bullets)
        print(f"  slide {slide_no}: odrazky aktualizovany ({len(bullets)} bodu)")

    prs.save(str(dst))
    print(f"OK: {dst}  (vstup: {src.name})")


if __name__ == "__main__":
    main()
