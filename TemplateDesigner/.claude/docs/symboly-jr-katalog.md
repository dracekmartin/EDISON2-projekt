# Katalog symbolů JŘ (značky u zastávek / spojů / v legendě)

> **Účel:** převzatá „mapa" symbolů jízdního řádu z legacy, ať ji máme **v TD** a renderujeme podle
> ní, ne ad hoc. Sada a význam symbolů vychází z **konvence/standardu** (úprava JŘ veřejné dopravy +
> CIS; SK má vlastní obdobu) — my (M-line) to jen **implementujeme** fontem + mapou. Uživatel TD si
> symboly nevolí (je to systémové chování jako NZ box); volí jen zda značky tisknout.

## Zdroje (legacy, autoritativní)

| Co | Kde |
|---|---|
| **Katalog** (id · znak · význam) | `JRMAIN/CACHE/JRapiLinPrtWin.mac` → `GSId()` ř. 1550-1616 |
| **Render-dispatch** (jak se kreslí) | `TiskJR/formTISKJRReportDotNet_Module.vb` → `Render_SekceDataLinky_Poznamky_RenderPoznamku_Symbol` ř. 6311+ |
| **Výběr fontu dle locale** | tamtéž ř. 1197-1201: `"Timetable"` → `Timetable_nove_SK` (SK) / `Timetable_nove` (CZ) |
| **NZ box (ruční)** | tamtéž `Render_SekceNegZnacka_Data` ř. 4518 (`FillRectangle(Black)` + `DrawString(White)`) |

## ⭐ Autoritativní model (Jiří Bžoch, 2026-07-09)

Rozhodl kolega (doménový vlastník legacy JR tisku). Tři kategorie značek u spoje:

1. **Kroužkované jen `1`-`7` + `+` + `X`** — kroužek dělá **speciální font**, který kroužkové glyfy má
   POUZE pro tyto hodnoty (`1`-`7` dny, `+` „kladiva"=pracovní dny, `X` „kříž"=neděle/svátky). GSId id 8-16.
   Jednoznakové → kroužkový glyf fontu.
2. **NZ / datová omezení = číslo v boxu** — VŠECHNA datová omezení (i pozitivní „jede také", i „nejede"),
   typicky vícemístná čísla, se tisknou jako **číslo v černém boxu** (invertovaně). Font pro ně kroužek
   NEMÁ (jen 1-7) → box. GSId typ 3.
3. **Atributy = značky** — piktogram ve **speciálním fontu** (vozíček, na znamení…).

Legacy do tisku posílal **značku + příznak, JAK ji tisknout** (text / speciální font + kód) — to je
přesně `GSId` (převod) + render-dispatch níže. **Zdroj pravdy = `GSId` v `JRapiLinPrtWin`** (konvence
odvozená z úpravy JŘ).

> **Upřesnění (Jiří Bžoch, 2026-07-10):** vícemístné datové značky („36 jede také… nejede…") jsou
> ve starém tisku TAKÉ **box**, ne kroužek. Kroužek dělá jen font pro hodnoty `1`-`7`. Dřívější domněnka,
> že pozitivní „jede" datové omezení má být kroužkované, byla **mylná** — proto se pokus o „pozitivní
> značku = ručně kreslený kroužek" **revertoval** (datová omezení zůstávají box, což je 1:1 s legacy).
>
> **Zbývá (deferred):** kroužkové `1`-`7`/`+`/`X` (týdenní platnost) přes font — AD je dnes neemituje
> jako jednoznakové anotace (dny řeší sloupci skupin dnů), takže je to samostatný feature, ne bug.

## Render-módy (jak se symbol kreslí)

1. **Font-glyf** — znak vykreslený v **Timetable fontu** (piktogram). Legacy: `ToChar(ID)` v `iFont1TimeTable`.
   Locale: CZ `Timetable_nove`, SK `Timetable_nove_SK` (embedded `Fonts/TimeTable2*.ttf`).
2. **Prostý text** — znak/zkratka v **normálním fontu** (ne piktogram). Legacy: `GetFont(...,"1"=Times,...)`.
   Např. `MHD`, `u`, `p`, `AMS`, `IDS`, `n`, `CLO`, `WC`, a **`x` = na znamení**.
3. **NZ box (ruční)** — negativní značka „nejede…": černý obdélník + bílé číslo. Kreslí se **ručně**,
   ne z fontu. **TD to už dělá** (`NotesLegendElement.RenderNegativniZnacka` + DepartureGrid inverzní box).
4. **Kompozit (ruční)** — font-glyf + horní/dolní index nebo + text. Legacy záporná ID (-1…-19):
   paragraf `G` + horní index 1/2/3 · metro (znak 35) + dolní index A/B/C · invalida (znak 72) + „WC".

## Katalog (GSId, id 1-66)

> Sloupec **Render** = doporučený mód v TD (odvozeno z legacy dispatch; ⚠️ = ověřit vizuálně proti fontu).
> „znak" = mnemonický znak z číselníku (co nese datový atribut / anotace).

| id | znak | význam | render |
|---|---|---|---|
| 1 | `\|` | projíždí | font-glyf |
| 2 | `<` | jede jinudy | font-glyf |
| 3 | MHD | MHD | **text** |
| 4 | `u` | pokračuje | **text** |
| 5 | `m` | vyčká | font-glyf |
| 6 | `^` | navazuje | font-glyf |
| 7 | `p` | poznámka pro spoj a tč spoje | **text** |
| 8-14 | `1`-`7` | týdenní platnost 1-7 (dny) | font-glyf (kroužková číslice) |
| 15 | `+` | týdenní platnost + | font-glyf |
| 16 | `X` | týdenní platnost X (denně) | font-glyf |
| 17 | AMS | AMS | **text** |
| 18 | `&` | místenkování možno AMS | font-glyf |
| 19 | `R` | místenkování možno R | font-glyf |
| 20 | `Ü` | místenkování nutno AMS | font-glyf |
| 21 | `þ` | místenkování nutno R | font-glyf |
| 22 | BEV | bezbariérový (vozíček ♿) | font-glyf |
| 23 | IDS | ids | **text** |
| 24 | NKA | nízkokapacitní | font-glyf |
| 25 | OBC | občerstvení | font-glyf |
| 26 | PCZ | zavazadla | font-glyf |
| 27 | PJI | kola | font-glyf |
| 28 | POS | zásilky | font-glyf |
| 29 | RBU;r;OZO | radiobus — spoj jede na objednání | font-glyf |
| 30 | SAO | samoobslužný | font-glyf |
| 31 | `N` | jen nástup | font-glyf |
| 32 | `V` | jen výstup | font-glyf |
| **33** | **`x`** | **zastávka pouze na znamení** | **text** ⚠️ (v legendě/u zastávky prosté „x", ne piktogram) |
| 34 | `A` | autobus | font-glyf |
| 35 | `L` | vlak | font-glyf (vláček) |
| 36 | `C` | clo | font-glyf |
| 37 | `G` | paragraf | font-glyf |
| 38-40 | `a`/`b`/`c` | paragraf 1/2/3 | font-glyf |
| 41-43 | šb/šc/še | šipka dolů počátek/pokračuje/konec | font-glyf |
| 44-45 | od/př | od-do / do-do šipky | font-glyf |
| 46 | zbez | zastávka bezbariérově přístupná | font-glyf |
| 47 | WC | veřejné WC v objektu zastávky | **text** |
| 48 | `y` | na znamení od 20:00 | ⚠️ (viz #33) |
| 49 | `z` | na znamení v prac. dny od 22:00, víkend celodenně | ⚠️ (viz #33) |
| 50-52 | `e`/`f`/`g` | metro A/B/C | font-glyf (kompozit + index) |
| 53 | `O` | letadlo | font-glyf |
| 54 | RZ | občerstvení/restaurace v objektu zastávky | font-glyf |
| 55 | BWC | veřejné bezbariérové WC v objektu zastávky | font-glyf |
| 56 | CBE | částečně bezbariérový vůz | font-glyf |
| 57 | RBP;i;OZP | spoj jede pominečně | font-glyf |
| 58 | TP | terminál s pomocí osobám se ZP | font-glyf |
| 59 | ZP | zastávka upravená pro osoby s těžkým ZP | font-glyf |
| 60 | `M` | metro | font-glyf |
| 61 | lod;D | loď | font-glyf |
| 62 | PR | parkoviště P+R | font-glyf |
| 63-64 | šbu/šeu | šipka nahoru počátek/konec | font-glyf |
| 65 | wifi | wifi | font-glyf |
| 66 | zásu | zásuvka | font-glyf |

## Co reálně používá zastávkový JŘ (AD/MHD agendy)

Dnes agendy emitují jako **atributy zastávek** (StopList) + **anotace spojů** (DepartureGrid) +
**legendu** (NotesLegend) hlavně: `x` (na znamení #33), `H`/`72`+`F`/`CBE` (bezbariér #22/#56),
dny (#8-14), a **NZ** (negativní, ruční box). Ostatní katalog je rezerva pro budoucí pokrytí.

## ⚠️ Otevřené k ověření při implementaci

- **`x` na znamení = prostý text, ne font-glyf.** Legacy ho v legendě tiskne jako „x" (konvence), ne
  jako piktogram. Naivní „surový znak → Timetable font" ho vykreslí jako **vláček** (glyf na pozici `x`
  je jiný symbol) — proto musí jít přes render-mód **text**, ne font-glyf. Totéž `y`/`z`.
- **id → znak fontu:** legacy `Case Else` kreslí `ToChar(ID)`; je třeba ověřit, jakou hodnotu nese
  datový atribut (ASCII mnemoniky `H`=72 / `x`=120 vs. GSId 1-66) a že sedí na glyf ve fontu.
- **Font `x` = vláček** ověřeno v TimeTable2 (SkiaSharp render u uživatele) → potvrzuje, že na-znamení
  NEsmí jít fontem.

## Návrh implementace v TD (per konvence)

Zavést **jeden zdroj pravdy** — mapu `atribut/anotace → (render-mód, hodnota)`:
- `font-glyf` → Timetable font (locale přes `FontResolver.SymbolFontFamily`),
- `text` → normální font (např. `x`, `MHD`, `u`, `p`, WC-zkratky),
- `nz-box` → stávající ruční inverzní box,
- `kompozit` → glyf + index (později, málo časté).

Konzumují ji `StopListElement` (atributy u zastávky), `DepartureGridElement` (anotace u minut),
`NotesLegendElement` (legenda) — místo dnešního „single-char → symbol font". Katalog výše = obsah mapy.
