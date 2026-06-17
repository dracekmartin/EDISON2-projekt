"""Precisluje stranky v prezentaci ('<n> / <m>') podle skutecneho poctu slidu.

Hodi se po rucnim smazani/pridani slidu v PowerPointu, kdy se nase vlastni
ramecky s cislem stranky neaktualizuji samy. Meni JEN cisla stranek, nic jineho.

Pouziti:
    python renumber.py                       # prezentace_v5.pptx (v miste)
    python renumber.py soubor.pptx           # jiny soubor (v miste)
    python renumber.py vstup.pptx vystup.pptx
"""

import sys
from pathlib import Path
from pptx import Presentation

from merge_slides_5_6 import set_page_number


def main():
    src = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).parent / "prezentace_v5.pptx"
    dst = Path(sys.argv[2]) if len(sys.argv) > 2 else src   # default: v miste

    prs = Presentation(str(src))
    total = len(prs.slides)
    changed = 0
    n = 0
    for s in prs.slides:
        n += 1
        if set_page_number(s, n, total):
            changed += 1
    prs.save(str(dst))
    print(f"OK: {dst.name} — {total} slidu, precislovano {changed} ramecku se strankou")


if __name__ == "__main__":
    main()
