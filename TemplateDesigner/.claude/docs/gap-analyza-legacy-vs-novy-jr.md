# Gap analýza: legacy TiskMHD vs. nový zastávkový JŘ

> **Účel:** porovnání plochy funkcí starého návrháře (`C:\DEV\EDISON\TiskMHD\`, VB.NET + Caché
> `ALVA.TISKMHD`) proti současné implementaci v TemplateDesigneru. Cíl: zjistit, co reálně chybí
> před **uživatelským seminářem 24.6.2026**, a stanovit priority na dodělání během zítřka.
>
> **Datum:** 2026-06-22 · Analýza čtena z legacy formulářů + Caché Data/Designer/Sestavy + našich
> elementů `StopListElement`/`DepartureGridElement`/`NotesLegendElement`.
>
> **Rozsah pokrytí (2 kola):** ✅ **kompletní.** 1. kolo: 13 editorů sekcí + vlastnosti sestavy +
> všech 6 Caché tříd návrháře (`Data`/`Designer`/`Sestavy`/`Main`/`Linka`/`Images`). 2. kolo:
> hlavní okno návrháře (`formTISKMHDDesignerEdit`, 3472 ř.), datové struktury (`Struktury.vb` —
> autoritativní seznam properties), enumy, .NET renderer (`formTISKMHDReportDotNet` + `ProDrawString`
> + `Obecne`), správce sestav. **Vyřazeno jako irelevantní:** `frmPomocny_*` (generické dialogy),
> app shell (`MainRun`/`ApplicationEvents`/`frmMain`), přístupy, e-mail. **Mimo rozsah (pokryto
> dříve):** datový engine zastávkového tisku v JRMAIN/MHD (`JRapi WinTiskMhd`, `LinkyPrintFormatMHD`)
> — to je gold oracle, reverzně zdokumentován v `cache-database-linky.md §10`.

---

## Verdikt (TL;DR)

Datově je nový zastávkový JŘ **ověřený 1:1** proti legacy na gold linkách a výkonově pročištěný.
Plocha **uživatelských** nastavení je u 3 specializovaných prvků **bohatší nebo srovnatelná** s
legacy ve většině oblastí (legenda, časová osa, styly buněk).

**Skutečné funkční mezery vůči legacy jsou tři druhy:**
1. **Demo-riziko, ne mezera** — false-positive varování přetečení + DEV tweaky v repu. *Tohle je
   jediná věc, která může kazit živé demo. P0.*
2. **Velké odložené položky** — multi-varianta časů tras (15× TrasaMin). Vědomě deferred, čeká na
   reálný request. Na seminář se zmíní slovně.
3. **Niche / dekorativní** — trojúhelník na pozadí, ellipse/triangle tvary, uppercase jako
   font-flag, zaoblené rohy. Nízká hodnota pro JŘ demo.

**Pro seminář (uživatelský, ne dodávka) není nutné dodělat žádnou funkční featuru.** Stačí
zajistit, aby demo nevypadalo rozbitě (P0) a mít připravené poctivé odpovědi na mezery (P2).

---

## ☑️ Rozhodovací checklist na 24.6 (projít a rozhodnout udělat / neudělat / později)

> Jedno místo k proklikání ráno. Velikost: S = do hodiny, M = půl dne, L = víc dní.
> Do sloupce **Rozhodnutí** dopisuj: ✅ udělat dnes / ⏭️ později / ❌ ne.

| Pol. | Co | Velikost | Priorita | Rozhodnutí |
|---|---|---|---|---|
| P0-1 | Vrátit DEV tweaky (CB_GWBUS→firma, startup šablonu necháváme AD) | S | 🔴 blocker | ⏭️ firmu vyřešit později; startup AD ponechán |
| P0-2 | Opravit #8 false-positive přetečení | S–M | 🔴 blocker | ✅ hotovo 2026-06-23 (879489e) — minimální fix |
| P0-3 | Smoke test gold linek v PDF (obsah, ne pixely) | M | 🔴 blocker | ✅ 2026-06-23 — 0320144 (FTL3) base OK, jako smoke test postačuje |
| P1-2 | ~~Zkrácené označení linky u minut~~ — **NENÍ quick win** | L | 🟠 mezera | ❌ 2026-06-23: pole `ZKRACENE_OZNACENI_LINKY` obě agendy (AD i MHD) emitují **vždy prázdné** (`BuildOdjezd` voláno bez `zkrOznLinky`). Render bez dat nemá co ukázat → vyžaduje celý sdružený JŘ (L), překlasifikováno na P2 |
| P1-1 | Uppercase jako font-flag | S | ⚪ nízká | ⏭️ 2026-06-23 odloženo: UPPERCASE už jde přes `{POLE:UPPER}`; font-flag jen na TextElementu = nekonzistentní polotvar. Udělat později cross-component (vč. malých písmen) |
| P1-3 | NZ na nový řádek v legendě (AD-N4) | S–M | 🟡 quick win | |
| P2-1 | **Multi-varianta časů tras (TrasaMin)** — *jediná velká věc* | **L** | 🟠 mezera | |
| P2-2 | Barevně odlišit časy (6 pásem dle minut) na StopListu | M | 🟠 mezera | |
| P2-3 | NZ (3 ř.) + spec. znak (3×) v záhlaví mřížky | M | 🟠 mezera | |
| P2-7 | Režimy výšky řádku + „nadesignovaná výška = minimum" | S–M | 🟠 mezera | |
| P2-6 | Governance šablon (systémové/zamčené vs uživatelské) | M | 🟠 strategické | |
| P2-4 | Cizojazyčné poznámky | S–M | 🟠 mezera | |
| P2-5 | Potlačit NZ v některých sloupcích dnů (dormant, bez dat) | M–L | ⚪ čeká data | |
| P4 | Dekorativní: justify text, per-strana dash rámeček, zaoblené rohy, tvary (elipsa/trojúhelník), trojúhelník na pozadí, transparentní pixel obrázku | S každé | ⚪ nízká | |

**Doporučený default na ráno:** P0-1/2/3 = ✅, P1-2 = ✅ (bonus, data hotová), zbytek = ⏭️ později.
Nezahajovat L/M položku na poslední chvíli před demem.

---

## P0 — Nutné do zítřka (aby demo nevypadalo rozbitě)

| # | Co | Proč to hoří | Effort |
|---|---|---|---|
| **P0-1** | **Vrátit DEV tweaky** v repu na produkční default | firma **CB_GWBUS → KV_DP**, startup šablona **AD+1 sekce → MHD+Záhlaví/Obsah/Patička**. Jinak se demo spustí v cizí firmě / divné šabloně | S |
| **P0-2** | **Rozseknout #8 false-positive přetečení** (červený rámeček na nové šabloně) | příčina potvrzena: `PreviewCanvas.PageOverflows` ignoruje `ClipTop/ClipBottom` u `SplitAcrossPages` sekcí → každá legitimně rozdělená sekce falešně bliká červeně. Na živém demu vypadá jako bug | S–M |
| **P0-3** | **Smoke test gold linek** v PDF (0410101 / 0425017 / 0320144 / 0320993 / 0365001) | poslední vizuální kontrola 1:1 před publikem; zachytí regrese z dnešních commitů | M |

> Detail #8: oprava varianty A — pro `SplitAcrossPages` sekce přetečení nehlásit (obsah se nikdy
> neztratí, jen přeteče), hlásit jen u `KeepTogether` proti plné výšce sekce. ~30 řádků v
> `PreviewCanvas.cs` + regresní test v `PageLayoutEngine`.
>
> **✅ Vyřešeno 2026-06-23 (minimální fix):** `PreviewCanvas.PageOverflows` přeskakuje
> ne-`KeepTogether` sekce (jeden guard). Ověřeno: `SplitAcrossPages` 400 mm → 2 stránky bez
> červené. **Nález při testu:** engine si `KeepTogether` sekci sám roztáhne na obsah
> (`CalculateSectionHeight = max(DesignerHeight, maxBottom)`), takže element-vs-sekce kontrola
> v praxi nikdy nenaskočí → červené varování je teď fakticky dormantní. To je pro demo OK
> (žádné falešné poplachy), ale **genuine detekce chybí** → viz follow-up níže.
>
> **⏭️ Follow-up (po demu): genuine page-overflow detekce.** Současná kontrola je element-vs-sekce,
> ale sekce vždy doroste obsah → nikdy nechytí reálný případ „`KeepTogether` sekce je vyšší než
> tisková plocha stránky → obsah se ořízne". Správně: u `KeepTogether` hlásit
> `placed.Y + placed.Height > PrintableHeight + tol`. ~S, mění sémantiku → chce sadu testů
> (header/footer pozice, sekce přesně na hraně).

---

## P1 — Quick wins (volitelné, pokud zbyde čas)

Drobnosti s viditelnou hodnotou, malý zásah. **Žádná není blokující.**

| # | Co | Stav | Effort |
|---|---|---|---|
| ~~**P1-1**~~ | **Uppercase jako font-flag** → **odloženo** | ⏭️ *2026-06-23:* UPPERCASE už pokrývá výrazový jazyk (`{POLE:UPPER}`). Font-flag jen na `TextElement` (jedno místo, jen velká, ne malá) = nekonzistentní s kaskádou. Prototyp navrhnut (`TextStyle.Uppercase` + `ApplyCasing` v `CellRenderer`/`TextElement`) a **vrácen** — udělat až jako cross-component style-flag (vč. malých písmen), aby fungoval všude stejně | S→M |
| ~~**P1-2**~~ | **Zkrácené označení linky u minut** → **přesunuto na P2** | ⚠️ *Korekce 2026-06-23:* C# pole `OdjezdData.ZkraceneOznaceniLinky` se parsuje, ale **obě Caché agendy (AD i MHD) ho emitují vždy prázdné** — `BuildOdjezd` se volá bez `zkrOznLinky` (AD ř. 452, MHD ř. 1041). Render bez dat = nic. Vyžaduje celý sdružený JŘ (sloučení víc linek do mřížky + výpočet zkr. označení) = **L**, ne quick win | L |
| **P1-3** | **Negativní značky na nový řádek v legendě** (AD-N4) | legacy `PATCH,"NegZnNR"`; návrh existuje, chybí property + render v `NotesLegendElement` | S–M |

---

## P2 — Funkční mezery vůči legacy (zmínit slovně, NEslibovat termín)

Co legacy uměl a my (zatím) ne. Pro seminář připravit poctivou odpověď „je na roadmapě".

| # | Legacy funkce | Náš stav | Effort | Poznámka |
|---|---|---|---|---|
| **P2-1** | **15× sloupec „TrasaMin" + 4× „Pracovní"** ve seznamu zastávek (multi-varianta časů jízdy vedle sebe) | máme **1** sloupec časů (gold `TrasaMin1`) | **L** (3–5h) | Největší položka. Caché emituje flat seznam variant, renderer musí dynamicky vytvořit sloupce + strop přehlednosti. Gold: **0345061** (8 variant), 0365001 (2). Odloženo na reálný request |
| **P2-2** | **6 pravidel „barevně odlišit ceny"** podle počtu minut ve seznamu zastávek | `StopListElement` **nemá** ColorRules (DepartureGrid je má) | M | Podmíněné barvení časů jízdy dle prahu. Pattern existuje na DepartureGrid → přenést na StopList |
| **P2-3** | **Negativní značka (3 řádky) + spec. znak (3×) v záhlaví mřížky** odjezdů | DepartureGrid záhlaví = jen nadpis skupiny dnů | M | Blok NZ/spec-znak v hlavičce sloupce skupiny dnů. Ověřit, jestli to zákazníci reálně používají |
| **P2-4** | **Cizojazyčné poznámky** (AD-N11, `prtCzjPzn`) | ⬜ chybí | S–M | Meziměstské/pohraniční linky. Caché rozšíření + toggle v legendě |
| **P2-5** | **Potlačit NZ v některých sloupcích** skupin dnů (AD-N5) | ⬜ dormant | M–L | Data prázdná všude, čeká na reálný případ |

---

## P3 — Vědomé rozdíly (NE mezery — jiný/lepší přístup)

Nevyžadují akci; jen aby se to nepletlo s „chybí".

- **Časová osa odjezdů** — legacy měl 3 režimy (`CasovaOsaOdjezduBezMezer`). My máme **4** (`Compact` / `DataBounded` / `Range` vč. wrapu přes půlnoc / `Full`). **Bohatší.**
- **Legenda poznámek** — legacy uměl jen 1–2 sloupce + šířku + výšku řádku, řazení/zarovnání bylo zapečené v enginu. My máme **1–4 sloupce, řazení dle délky, směr wrapu, zarovnaný marker, filtr per typ**. **Bohatší.**
- **Styly buněk** — legacy 4 pevné font-profily; my máme **kaskádu** (sloupec → prvek → defaults) + per-sloupec override + zvýraznění aktuální/projeté zastávky. **Srovnatelné/bohatší.**
- **Pozicování sekcí** — legacy „přichytit zdola/zprava k sekci" (relativní flow). My máme absolutní pozice + flow layout v engine + drag&drop řazení. **Jiný model, funkčně pokryto.**
- **Čárové kódy** — legacy jen QR (JPG, bez ECC/barev). My QR + Code128 + EAN-13/8 + PDF417 + DataMatrix. **Bohatší.**
- **Vlastnosti stránky** — legacy A2–A6 + landscape + okraje + vodítka. My editor formátů + orientace + okraje + mřížka. **Pokryto.**
- **Wire formát** — legacy tilde-delimited; my JSON. **Lepší (verzovatelné, debugovatelné).**

---

## P4 — Dekorativní / nízká priorita (mimo JŘ jádro)

Legacy obecné prvky, které pro zastávkový JŘ nejsou klíčové:

- **Tvary** — legacy `Shape` měl 12 typů (ellipse, plná kružnice, trojúhelníky A–D plné/obrysové). My máme `RectangleElement` + `LineElement`, **žádné trojúhelníky/elipsy**.
- **Trojúhelník na pozadí** seznamu zastávek (směrový klín, 3 typy zúžení) — dekorativní, **chybí**.
- **Zaoblené rohy rámečku** (`BorderRadius`) — legacy jen u DBField; my pravé rohy nemáme.
- **Transparentní pixel obrázku přes souřadnici X/Y** (color-key) — niche, chybí.
- **„Přizpůsobit" písmo per zóna** — my máme jeden globální auto-scale (`ComputeFontScale`), ne per-zóna. Funkčně postačuje.

---

## Druhé kolo — doplňující nálezy (mimo editory sekcí)

Analýza hlavního okna návrháře, struktur a renderovacího enginu odhalila tyto věci, které editory
sekcí neukazovaly:

### ⚠️ Klíčová nuance: vizuální 1:1 NENÍ totéž co datové 1:1
Datové 1:1 je ověřené proti gold oracle (`ALVA.TISKMHD.Data`) — **správný obsah**. Ale legacy
**vizuální** render běží na **GDI+ (`System.Drawing`)**, náš na **SkiaSharp**. To jsou jiné
sázecí enginy → **pixelová shoda není garantovaná a ani se o ni neusiluje** (nový nástroj, vizuálně
podobný). Legacy navíc obsahuje empirické „fudge" konstanty (smršťování znaků `/15`,`/5`,`/40`,
`camfrnous=+1`, font v mm × 4 body/mm, auto-fit krok 0.3 mm, šířka sloupce minut hledaná po 0.1 mm).

**Důsledek pro P0-3 smoke test:** porovnávat **obsah a rozložení** (správné minuty, anotace,
poznámky, sloupce), ne pixely. Místa nejnáchylnější k odlišnosti, která stojí za pohled:
- pozice horních indexů u minut (nahoru/dolů/doprava + zmenšené písmo),
- inverzní negativní značka (bílé na černém — velikost černého obdélníku),
- šířka sloupce minut a auto-fit písma (zalomení / počet znaků na řádek),
- zalomení dlouhých poznámek v legendě.

### Nové funkční mezery (doplnit do priorit)
| # | Legacy funkce | Náš stav | Priorita |
|---|---|---|---|
| **P2-6** | **Governance šablon** — systémové (M-line) vs uživatelské sestavy, mimo M-line doménu read-only; M-line je distribuuje zákazníkům | máme per-IDfa storage v Caché, ale **žádné „systémová/zamčená šablona"** ani distribuční model | M — *strategické, ne demo* |
| **P2-7** | **Režimy výšky řádku** + „nadesignovaná výška = minimum, sekce roste" | máme `DataRowHeight` (null=auto) + auto-scale; explicitní „výška je minimum a roste" model částečně | S–M |
| **P4-x** | **Zarovnání textu do bloku (justify)** v textovém prvku | `TextElement` justify nemá | S, nízká |
| **P4-x** | **Per-strana styl rámečku (Solid/Dot/Dash) + zaoblené rohy** sekce/prvku | jednoduchý border (barva+šířka), bez per-strana dash a bez zaoblení | S–M, nízká |
| **P4-x** | **Sekce vyšší než stránka v designeru** — canvas má napevno výšku jedné A4 (`CalculatePageHeightPx => PageSize.Height`), property panel klamp výšky sekce obchází (na rozdíl od resize). Sekce > stránka přeteče na šedé pozadí mimo bílý list. Preview stránkuje korektně. ⏭️ odloženo 2026-06-23. Možnosti: (A) clamp v property panelu na tisknutelnou výšku (S, konzistentní s CLAUDE.md), (B) canvas roste přes víc stránek + vodítka A4 hranic (M) | jednostránkový canvas | S–M, nízká |

### Potvrzeno jako pokryté (druhé kolo)
- **Marker legendy 3 typy** (normální / bílé-na-černém / pictografický znak) → máme inverzní barvy NZ + spec. znaky. ✅
- **NZ invertovaně u minut v mřížce** (černý box + bílý znak) → ✅ 2026-06-23 (DepartureGrid, MHD). AD záměrně bez NZ u minut (CNZ jen klasifikuje sloupce dnů — ověřeno 1:1 gold).
- **Zebra liché/sudé řádky** (hodiny i minuty) → `AlternatingRowColor` + per-group. ✅
- **Pořadí sloupců** (`PoradiSloupcu`) → `Columns` order. ✅
- **Samostatná sekce „Hodina odjezdu" + NegZnacka/SpecZnak** → sloučeno do DepartureGrid/anotací (jiný, čistší model). ✅
- **Export/import šablony** (legacy `.GOF` raw global) → JSON export. ✅+

---

## Kompletní mapovací matice (prvek po prvku)

### Seznam zastávek (StopList)
| Legacy volba | Náš ekvivalent | Stav |
|---|---|---|
| Sloupce: Časy/Zóny/Průběhy/ZákladníTrasa/Pracovní2-5 | `Columns` (Key/Header/Width/Visible/Align/Style) | ✅ jádro, ⬜ Pracovní2-5 |
| TrasaMin 1–15 (multi-varianta) | 1 sloupec časů | 🔴 P2-1 |
| Per-sloupec šířka / zarovnání / pozadí / pravý okraj | `StopColumn.Style` + `GridColor` + separátory | ✅ (granularita per-border čar částečná) |
| 4 font-profily (hlavička/aktuální/projetá/neprojetá) | `HeaderStyle`/`DataStyle` + `CurrentStop*` + `PassedStopsForeColor` | ✅ |
| Zvýraznění aktuální zastávky (font + pozadí) | `CurrentStopBold/Underline/FontSize/BackgroundColor` | ✅ |
| Šipka vlevo (barva) | `ShowDirectionArrow` + `DirectionArrowPosition(Left/Right)` + barva | ✅ |
| Trojúhelník na pozadí (3 typy) | — | 🟡 P4 |
| Barevně odlišit ceny (6× práh minut) | — (StopList nemá ColorRules) | 🟡 P2-2 |
| Uppercase font-flag | jen `{POLE:UPPER}` | 🟡 P1-1 |
| Atributy/poznámky zastávky, „na znamení" | `ShowStopAttributes/Notes/SignalStopIndicator` + filtr | ✅ |
| Výška řádku pevná/dynamická | `DataRowHeight` (null=auto) + auto-scale | ✅ |

### Mřížka minut (DepartureGrid)
| Legacy volba | Náš ekvivalent | Stav |
|---|---|---|
| Skupiny dnů jako sloupce | `GroupColumns` + filtr `SkupinaDnuId` | ✅ |
| Sloupec hodin (vlastní nadpis, per-group) | `HourColumn*` + `HourColumnPerGroup` | ✅ |
| Časová osa (3 režimy) | `TimeAxisMode` (4 režimy + wrap půlnoc) | ✅+ |
| Zebra (lichý/sudý) | `AlternatingRowColor` + per-group override | ✅ |
| Anotace u minut (NZ, spec. znak, dny, rozdíly tras) | `ShowAnotace` + `AnotaceSymbolStyle/TextStyle` + engine | ✅ |
| Zmenšené písmo anotace (%) | `AnotaceFontSize` + oddělené styly | ✅ (jinak) |
| Vertikální zarovnání záhlaví | `HeaderVerticalAlignment` | ✅ |
| NZ (3 řádky) + spec. znak (3×) v záhlaví | — | 🟡 P2-3 |
| Zkrácené označení linky u minut | data parsována, render chybí | 🟡 P1-2 |
| Barevná pravidla (data-driven) | `ColorRules` | ✅ |

### Legenda poznámek (NotesLegend)
| Legacy volba | Náš ekvivalent | Stav |
|---|---|---|
| Počet sloupců (1–2) | `Columns` (1–4) | ✅+ |
| Šířka sloupce s kódy | `MarkerSirka` | ✅ |
| Výška řádku, font, barvy | `NoteLineHeight`/`NoteFontSize`/styly | ✅ |
| Řazení / zarovnání markeru / směr wrapu | `Razeni` + `MarkerSirka` + `WrapSmer` | ✅+ (nad legacy) |
| Filtr per typ (NZ / spec / text) | `ShowNegativniZnacky/SpecialniZnaky/TextovePoznamky` | ✅ |
| NZ na nový řádek | — | 🟡 P1-3 |
| Filtr per kategorie | jen per-typ | 🟡 (overlap s D5/D6, tech debt) |

---

## Doporučený plán na zítřek (24.6, ráno před seminářem)

1. **P0-1** vrátit DEV tweaky (5 min).
2. **P0-2** opravit #8 false-positive overflow + regresní test (~1h).
3. **P0-3** vyexportovat gold linky do PDF a vizuálně zkontrolovat (~30 min).
4. *(pokud zbyde čas)* **P1-2** dorenderovat zkrácené označení linky (data už máme) — pěkné „bonus" pro sdružené JŘ.
5. Připravit slovní odpovědi na **P2** (hlavně TrasaMin multi-varianta — „na roadmapě, přidáme dle vašich potřeb").

**Nezahajovat** žádnou P2/P4 položku na poslední chvíli — riziko regrese před demem převažuje hodnotu.
