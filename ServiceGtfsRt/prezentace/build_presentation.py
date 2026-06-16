"""Sestavi prezentace.pptx (GTFS / GTFS Realtime) ze vzoru _vzor.pptx.

Strategie (stejna jako TemplateDesigner/prezentace): nacteme vzor, upravime
existujici slidy (1=intro, 2=template obsahoveho, 3=outro) a slide 2 naklonujeme
tolikrat, kolik potrebujeme obsahovych stran. Zachovavame puvodni shapes
(loga, grafiku, decorace) — menime jen texty.
"""

from copy import deepcopy
from pathlib import Path
from pptx import Presentation
from pptx.util import Cm
from lxml import etree

SRC = Path(__file__).parent / "_vzor.pptx"
DST = Path(__file__).parent / "prezentace.pptx"

# ------------------------------ obsah ------------------------------

DATUM = "16.6.2026"                                  # editovatelne v .pptx
AUTOR = "Jan Miesbauer"                              # 1. radek podnadpisu (intro/outro); vzor ma "David Zeman"
PROJEKT_PREFIX = "GTFS / GTFS REALTIME"              # nahrazuje "DPMCB" v zahlavi (intro/outro)
PROJEKT_PODNADPIS = "Jízdní řády a živá data na Google Maps"  # velky podnadpis na intro/outro
HEADER_BREADCRUMB = "GTFS / GTFS Realtime"           # breadcrumb v zahlavi obsahovych slidu

# Obsahove slidy (slide 2..10): (nadpis, odrazky)
SLIDES = [
    {
        "title": "Co je GTFS",
        "bullets": [
            "Cestující chtějí vidět jízdní řády i živá data v aplikacích, které běžně používají (Google Maps, Mapy.cz, Apple Maps)",
            "GTFS = General Transit Feed Specification — otevřený, celosvětově používaný standard",
            "Strojově čitelný popis dopravy: kdo jezdí, kudy, kdy a odkud kam",
            "Princip „jeden zdroj dat → mnoho aplikací“ — publikuji jednou, využijí všichni",
            "Dvě roviny: GTFS (statické jízdní řády) + GTFS Realtime (živá data)",
        ],
    },
    {
        "title": "Statické GTFS",
        "bullets": [
            "Feed = ZIP balíček textových souborů (CSV / .txt), každý soubor = jedna tabulka",
            {
                "t": "Povinné minimum (bez nich Google feed nepřijme):",
                "sub": [
                    "agency (dopravce), stops (zastávky + GPS), routes (linky)",
                    "trips (spoje), stop_times (časy), calendar (kdy jedou)",
                ],
            },
            {
                "t": "Volitelné (přidaná hodnota):",
                "sub": [
                    "shapes (přesný tvar trasy v mapě), transfers (přestupy), frequencies (intervaly)",
                    "fare_* (tarify), feed_info (informace o feedu)",
                ],
            },
            "Pravidlo: začít povinným minimem, volitelné dodávat postupně",
        ],
    },
    {
        "title": "GTFS Realtime",
        "bullets": [
            "Nadstavba nad statickým GTFS — co se děje právě teď",
            {
                "t": "Tři druhy zpráv (entit):",
                "sub": [
                    "VehiclePositions — poloha vozidel (živá tečka na mapě)",
                    "TripUpdates — zpoždění a předpovědi příjezdů/odjezdů",
                    "ServiceAlerts — mimořádnosti: výluky, zrušené spoje, objížďky",
                ],
            },
            "Formát Protocol Buffers (.pb); vždy se odkazuje na statický feed (shodné identifikátory)",
            "Doporučené pořadí zavádění: vozidla → zpoždění → výluky",
        ],
    },
    {
        "title": "Jak data dostat na Google a způsoby realizace",
        "bullets": [
            "Google si data buď stahuje sám (fetch z URL), nebo je přijímá (push) — vždy přes veřejnou HTTPS adresu",
            {
                "t": "Statické GTFS (ZIP):",
                "sub": [
                    "fetch z URL — Google stahuje dle rozvrhu (min. týdně, lze denně); doporučeno",
                    "ruční nahrání ZIP v konzoli — jednoduché, ale data snadno zastarají",
                ],
            },
            {
                "t": "GTFS Realtime (.pb):",
                "sub": [
                    "fetch endpoint — Google stahuje ~30 s; nebo push na Google přes OAuth 2.0",
                ],
            },
            "Možnosti realizace: ručně / vlastní generátor nad systémem JŘ / nákup hotové služby",
            "Vždy potřeba: kvalitní data, stabilní identifikátory (statika ↔ RT), veřejný HTTPS, aktuálnost",
        ],
    },
    {
        "title": "GTFS v Edisonu — jak to funguje",
        "bullets": [
            "Edison = primární systém jízdních řádů → zdroj dat pro statické GTFS i Realtime (drží shodné identifikátory)",
            {
                "t": "Co Edison umí:",
                "sub": [
                    "statické GTFS — naplánovaná úloha vygeneruje ZIP",
                    "Realtime — služba ServiceGtfsRt převádí živá data na protobuf (.pb)",
                ],
            },
            {
                "t": "Dnešní stav (DPCB):",
                "sub": [
                    "endpointy připravené; statiku zákazník nahrává ručně na Google portál",
                ],
            },
            {
                "t": "Automatizace (cíl):",
                "sub": [
                    "ZIP i .pb vystavené na veřejné HTTPS adrese → Google si oboje fetchuje sám = konec ručního nahrávání",
                ],
            },
        ],
    },
    {
        "title": "Architektura a co je potřeba ke zprovoznění",
        "bullets": [
            {
                "t": "Dva nezávislé směry:",
                "sub": [
                    "VSTUP (živá data): zdroj polohy vozidel → Edison/Caché → poskládá feed",
                    "VÝSTUP (Google): Google přes HTTPS fetchuje feed z veřejné adresy → ServiceGtfsRt (RT) / ZIP (statika)",
                ],
            },
            {
                "t": "Co zajistí IT zákazníka (ke zprovoznění):",
                "sub": [
                    "veřejná HTTPS adresa (reverse proxy) + certifikát + otevřený port 443",
                    "přístup k živému zdroji polohy vozidel z Edison serveru",
                    "v Google účtu nastavit fetch URL (statika i Realtime)",
                ],
            },
            "Dělba rolí: data = objednatel/dopravce · feed = Edison/M-line · publikace + Google účet = IT zákazníka",
        ],
    },
    {
        "title": "Chci zavést GTFS — co dál?",
        "bullets": [
            {
                "t": "Vy / vaše IT zajistíte:",
                "sub": [
                    "data v Edisonu — kvalitní JŘ, zastávky s GPS, čísla linek",
                    "veřejnou HTTPS adresu + certifikát (reverse proxy, port 443)",
                    "Google Transit účet + (u Realtime) přístup k živému zdroji polohy vozidel",
                ],
            },
            {
                "t": "My (M-line) zařídíme:",
                "sub": [
                    "export statického GTFS (ZIP) + automatickou úlohu",
                    "službu ServiceGtfsRt pro Realtime (.pb)",
                    "nastavení feedů + ověření ve validaci (Issues)",
                ],
            },
            "Spuštění: v Google účtu zadat fetch URL → Google stahuje a zobrazuje data",
        ],
    },
]

# ------------------------------ helpers ------------------------------

def find_shape_by_text(slide, needle):
    for shape in slide.shapes:
        if shape.has_text_frame and needle in shape.text_frame.text:
            return shape
    return None


def replace_run_text(shape, mapping):
    """Nahradi runy s presnym textem z klicu — zachova formatovani (font, barvu, bold)."""
    for para in shape.text_frame.paragraphs:
        for run in para.runs:
            if run.text in mapping:
                run.text = mapping[run.text]


def set_single_text(shape, text):
    """Nastavi text v shape s jednim runem (ponecha formatovani prvniho runu)."""
    para = shape.text_frame.paragraphs[0]
    if para.runs:
        para.runs[0].text = text
        for run in para.runs[1:]:
            run.text = ""
    else:
        para.text = text


A_NS = "http://schemas.openxmlformats.org/drawingml/2006/main"
NS = {"a": A_NS}


def _make_para(template_xml, text, level):
    """Vyrobi odstavec z template_xml; level 0 = hlavni bod, level 1 = podbod (mensi font, odsazeni, pomlcka)."""
    new_p = deepcopy(template_xml)
    pPr = new_p.find("a:pPr", NS)
    if level == 1 and pPr is not None:
        pPr.set("marL", "742950")
        pPr.set("indent", "-285750")
        buChar = pPr.find("a:buChar", NS)
        if buChar is not None:
            buChar.set("char", "–")   # en-dash pro podbody
    runs = new_p.findall("a:r", NS)
    if runs:
        first_run = runs[0]
        for r in runs[1:]:
            new_p.remove(r)
        if level == 1:
            rPr = first_run.find("a:rPr", NS)
            if rPr is not None:
                rPr.set("sz", "1800")      # 18pt podbod (hlavni bod = 22pt ze vzoru)
        t = first_run.find("a:t", NS)
        if t is not None:
            t.text = text
    else:
        r = etree.SubElement(new_p, f"{{{A_NS}}}r")
        t = etree.SubElement(r, f"{{{A_NS}}}t")
        t.text = text
    return new_p


def replace_bullets(shape, bullets):
    """Nahradi obsah text_frame odrazkami. Polozka = string (hlavni bod)
    nebo dict {"t": "...", "sub": ["...", ...]} (hlavni bod + podbody)."""
    tf = shape.text_frame
    template_xml = deepcopy(tf.paragraphs[0]._p)

    tf.clear()
    tf.paragraphs[0]._p.getparent().remove(tf.paragraphs[0]._p)

    txBody = tf._txBody
    for bullet in bullets:
        if isinstance(bullet, dict):
            txBody.append(_make_para(template_xml, bullet["t"], 0))
            for sub in bullet.get("sub", []):
                txBody.append(_make_para(template_xml, sub, 1))
        else:
            txBody.append(_make_para(template_xml, bullet, 0))


def clone_slide(prs, source_slide):
    """Naklonuje slide se vsemi shapes na konec; presune i image/media relationships."""
    layout = source_slide.slide_layout
    new_slide = prs.slides.add_slide(layout)

    for ph in list(new_slide.placeholders):
        sp = ph._element
        sp.getparent().remove(sp)

    rid_map = {}
    image_like = ("image", "chart", "diagram", "media", "oleObject")
    for rel in source_slide.part.rels.values():
        if any(k in rel.reltype for k in image_like):
            new_rid = new_slide.part.relate_to(rel.target_part, rel.reltype)
            rid_map[rel.rId] = new_rid

    rid_attrs = {
        "{http://schemas.openxmlformats.org/officeDocument/2006/relationships}embed",
        "{http://schemas.openxmlformats.org/officeDocument/2006/relationships}link",
    }
    for shape in source_slide.shapes:
        new_el = deepcopy(shape.element)
        for el in new_el.iter():
            for attr in rid_attrs:
                if attr in el.attrib and el.attrib[attr] in rid_map:
                    el.attrib[attr] = rid_map[el.attrib[attr]]
        new_slide.shapes._spTree.append(new_el)

    return new_slide


def set_intro_outro_header(slide):
    """Nahradi 'DPMCB 3.9.2025' -> PROJEKT_PREFIX + DATUM (runy ' ','3','.9.2025')."""
    dpmcb_shape = find_shape_by_text(slide, "DPMCB")
    if dpmcb_shape:
        day, monthyear = DATUM.split(".", 1)   # "16", "6.2026"
        replace_run_text(dpmcb_shape, {
            "DPMCB ": f"{PROJEKT_PREFIX} ",
            "3": day,
            ".9.2025": f".{monthyear}",
        })


def set_author(slide):
    """Nastavi 1. radek podnadpisu (autor) na AUTOR; vzor ma 'David Zeman'."""
    shape = find_shape_by_text(slide, "M-LINE SOFTWARE")
    if shape:
        para = shape.text_frame.paragraphs[0]   # 1. radek = jmeno autora
        if para.runs:
            para.runs[0].text = AUTOR
            for run in para.runs[1:]:
                run.text = ""


# ------------------------------ main ------------------------------

def main():
    prs = Presentation(str(SRC))
    slides = list(prs.slides)
    assert len(slides) == 3, f"Ocekavany 3 slidy ve vzoru, nalezeno {len(slides)}"

    intro, template, outro = slides[0], slides[1], slides[2]

    # --- SLIDE 1: intro ---
    set_intro_outro_header(intro)
    set_author(intro)
    podtitul_shape = find_shape_by_text(intro, "MHD Linky")
    if podtitul_shape:
        set_single_text(podtitul_shape, PROJEKT_PODNADPIS)

    # --- OBSAHOVE SLIDY ---
    content_slides = [template]
    for _ in range(len(SLIDES) - 1):
        content_slides.append(clone_slide(prs, template))

    total_slides = 1 + len(SLIDES) + 1  # intro + obsahove + outro
    for idx, (slide, data) in enumerate(zip(content_slides, SLIDES)):
        page_no = idx + 2

        header_shape = find_shape_by_text(slide, "MHD Linky")
        if header_shape:
            set_single_text(header_shape, HEADER_BREADCRUMB)

        title_shape = find_shape_by_text(slide, "Nadpis")
        if title_shape:
            set_single_text(title_shape, data["title"])
            title_shape.left = Cm(0.97)
            title_shape.width = Cm(23.46)
            title_shape.top = Cm(2.50)
            title_shape.height = Cm(1.80)

        bullets_shape = find_shape_by_text(slide, "Odr")
        if bullets_shape:
            replace_bullets(bullets_shape, data["bullets"])
            bullets_shape.top = Cm(4.70)
            bullets_shape.height = Cm(11.50)

        # strankovani "1 / 8" -> "N / total" (runy "1" a " / 8")
        for shape in slide.shapes:
            if not shape.has_text_frame:
                continue
            if "/ 8" in shape.text_frame.text and "www" not in shape.text_frame.text:
                replace_run_text(shape, {"1": str(page_no), " / 8": f" / {total_slides}"})
                break

    # --- presunout outro na konec + upravit ---
    xml_slides = prs.slides._sldIdLst
    for s in list(xml_slides):
        if prs.slides.get(int(s.get("id"))) == outro:
            xml_slides.remove(s)
            xml_slides.append(s)
            break

    set_intro_outro_header(outro)
    set_author(outro)
    outro_podtitul = find_shape_by_text(outro, "MHD Linky")
    if outro_podtitul:
        set_single_text(outro_podtitul, PROJEKT_PODNADPIS)

    prs.save(str(DST))
    print(f"OK: {DST}")
    print(f"Slidy ({len(prs.slides)}):")
    for i, s in enumerate(prs.slides):
        first_text = ""
        for sh in s.shapes:
            if sh.has_text_frame and sh.text_frame.text.strip():
                first_text = sh.text_frame.text.strip().split("\n")[0][:60]
                break
        print(f"  {i+1}. {first_text}")


if __name__ == "__main__":
    main()
