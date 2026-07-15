# CLAUDE.md — Kontext pro AI agenta

## Co je tento projekt

**TemplateDesigner** je WPF desktopová aplikace pro tvorbu šablon tiskových sestav.
Náhrada staršího nástroje postaveného v InterSystems Caché/Globals.
Při navrhování nových featur, bez v potaz, že v budoucnu by mohl být nástroj, alespoň částečně, ovládán pomocí AI.Dobre

Systém: **1. Designer** (tento projekt) — vizuální návrh šablon → **2. Print Engine** (integrovaný) — šablona + data = PDF.

---

## ⚡ Aktuální stav projektu

> **📝 PRO AI AGENTA:** Průběžně aktualizuj. Dokončené → přesuň do "✅ Co máme". Nové problémy → "🐛 Známé problémy".

**Datum poslední aktualizace:** 2026-07-13

> **Prezentace proběhly** (statická ~19.6., živá 26.6.). Od 2026-06-30 je **databáze primární úložiště
> šablon** (Otevřít/Uložit/Uložit jako → DB; soubor = Import/Export JSON, obdoba PDF/PNG). Aktivní
> tracky a priority po prezentacích viz [`.claude/docs/roadmap.md`](.claude/docs/roadmap.md) (vršek).

### 🎯 Aktuální focus

**Knižní/linkový JŘ (Fáze C+D) — pilot uzavřen 2026-07-04 jako testovací v1.** Agenda
`KnizniJrAd` (`KNIZNI_JR_AD`, reimplementace jádra `mkPLinWin`) + `KnizniJrMhd` (`KNIZNI_JR_MHD`,
MHD analogie `LinkyPrintFormatJR.MakeData`) + standalone `LineTableElement` (kolekce sloupců
`StopColumn`, 2-tab dialog Obecné+Sloupce přes sdílený `ColumnListEditor`/`CellStyleEditor`,
EDInputForm, kontextová paleta KNIZNI_* ↔ ZASTAVKOVY_*). AD 22/24 linek 1:1 vs gold přes 4 firmy,
MHD strukturálně (CB_DP 1001). **Vědomě odložené body C1–C7** (MHD gold diff, font Timetable 42b,
2D bin-packing + R1 extrakce jádra, distribuce šablon, konfigurovatelné pořadí poznámek, turnusy)
viz `.claude/docs/roadmap.md` Fáze C+D „🧪 Pilot uzavřen" + `linkovy-knizni-jr-implementace-analyza.md` §11-12.
Dál se postupuje dle priorit (zastávkové tickety / roadmapa), ne nutně hned zde.

**Zastávkový JŘ z AD dat (agenda `ZastavkoveJrAd`) ✅ datově 1:1 + výkonově pročištěná** — ověřeno proti legacy gold oracle (`ALVA.TISKMHD.Data` VolanoZ=JRMAIN), viz `.claude/docs/ad-data-mapa.md §10`. Hotové: core, FTL 0-3, omezení, dopravce (vč. adresy), CIS názvy, multi-page, **rozdíly tras** (1:1 OmTrasa), **atributy zastávek** (jen „na znamení", jako legacy), **atributy spoje** (bezbariér H/F), **časy jízdy ve StopListu** (jeden sloupec = gold TrasaMin1), + **performance** (memoizace per-linka invariantů + W-trasa cache, behavior-preserving byte-identický).

**Zbývá k úplnému 1:1 (zdokumentováno v §10):** šířka/layout legendy (vizuál). **Hotovo 2026-07-02:** multi-varianta TrasaMin (L1) — dynamické variantní časové sloupce (N dle dat, ne fixních 15), 1:1 ověřeno gold 0365005 + přepínač `ShowAllRouteVariants`. **Hotovo 2026-06-23:** číslování omezení = číslo NZ (`cnz`) invertovaně (1:1, žádný firm gate). **Pořadí legendy** ✅ živě ověřeno 2026-06-30 proti gold oracle (`ALVA.TISKMHD.Data`) na 0365005: bezbariér → rozdíly tras (B,C,E,F,G,H) → omezení (29,68) 1:1 vč. sub-pořadí; default `Razeni=Vychozi` správné, reorder není potřeba (dřívější domněnka „typ1→2→3" byla chybná).

Předtím: **Caché Migration Wave 1 + 1.5 + 2 + 3 ✅** (per-user state + agenda registry + template storage). Pak **Fáze A.2-D zákaznické tickety** (TRANSDEV/KORID/DPKV suspended tickety).

### ✅ Co máme

- Text, Image, Rectangle, Line, BarcodeElement (QR, Code128, EAN-13, EAN-8, PDF417, DataMatrix)
- **DynamicTableElement** — samostatná dynamická tabulka (řádky z `DataSource` kolekce agendy); 2-tab editor `DynamicTableEditorDialog` (Obecné = EDInputForm `DynamicTableElementEdit.cls` + Sloupce přes `ColumnListEditor`/`CellStyleEditor` s Add/Remove a mapováním `Field`); auto-scale do pevné Height + auto-distribuce šířky (`Width≤0`), seed sloupců z polí kolekce centralizovaný v dialogu (undoable), dvojklik otevře dialog rovnou; **podmíněné řádkové featury** přes sdílený `Models/Expressions/RowConditions` (výraz nad daty řádku): **viditelnost řádku** (`RowVisibilityExpression` — nesplněné řádky se nevykreslí ani nezapočítají do výšky; DynamicTable + StopList + LineTable) + **zvýraznění řádku** (`RowHighlightExpression`+`RowHighlightColor` → pozadí; DynamicTable + StopList + LineTable). DepartureGrid mimo = pivot. Default-off = behavior-preserving pro gold; helpText podmínkových polí vypisuje dostupné názvy polí řádku. ; **počítaná pole** (`TableColumn.Expression` — aritmetický výraz nad poli řádku `{CENA}*{POCET}`, přebíjí `Field`, výsledek formátovatelný přes `Format` — editace ve Sloupce tabu vč. dřív chybějícího `Format`); **min/max řádků** (`MinRows` doplní prázdné řádky do minima = pevná výška boxu např. faktura, `MaxRows` ořízne — DynamicTable-only). Statická `TableElement` uspána (`JsonDerivedType` jen pro legacy načtení, ne v UI)
- Parametry sestavy (Part 1 + serializace do JSON), VisibilityExpression evaluator
- Výrazový jazyk kompletní: podmínky (`&&`, `||`, `!`), porovnání, **aritmetika (`+ - * /`, unární minus, závorky, priorita)** pro počítaná pole, formátování (`{POLE:N2}`, `{POLE:UPPER}`, `{POLE:LEFT(4)}`), agregace (`SUM`, `COUNT`, `AVG`, `MIN`, `MAX`) v souhrnných řádcích tabulky **nad výrazem, ne jen holým polem** — `SUM({CENA}*{POCET})` sečte počítanou řádkovou hodnotu (argument vyhodnocuje `ExpressionEvaluator` per řádek; omezení: bez vnořených závorek v argumentu). `ExpressionEvaluator.EvaluateToString` = hodnotové vyhodnocení (fail-soft); dělení nulou / nečíselný operand → prázdno
- Live preview, PDF export, DataSchema tree, section management, undo/redo (12 cmd typů)
- `TemplateDesignerViewModel : EDViewModel` — MVVM, duální startup (standalone + EDISON)
- Custom section templates (file-based), CompositeDataSchemaProvider (Caché + JSON fallback)
- Smart snap — přichytávání elementů k sobě (edge↔edge, center↔center, gap snap 4mm), zvýraznění cílových elementů, gap indikátor
- Chytré duplikování — Ctrl+D+šipka umístí kopii vedle originálu s 4mm mezerou; Ctrl+D bez šipky = plain duplikát (+5mm offset); neovlivňuje clipboard
- Drag & drop elementů z palety na canvas — ghost preview, zvýraznění cílové sekce, clamp + snap na mřížku
- Drag & drop databázových polí z panelu "Pole agendy" na canvas — vytvoří TextElement s `{POLE}`, nebo připojí placeholder k existujícímu TextElementu
- Drag & drop řazení sekcí v levém panelu — přetažením místo tlačítek ▲▼, vizuální indikátor pozice
- Locale / CultureInfo v šabloně — `Template.CultureCode` (cs-CZ, sk-SK, en-US, de-DE); formátování čísel a datumů nezávislé na systémovém nastavení Windows
- ElementGroup — seskupování elementů (`GroupId` property); klik vybere celou skupinu, Alt+klik jen jeden; Ctrl+G/Ctrl+Shift+G; vizuální badge + bounding box; hierarchický strom; PDF import auto-groups
- PDF import — `PdfImportDialog` s PdfPig extrakcí; 3 merge módy grafiky (None/LinesOnly/All); detekce okrajů; auto-grouping; texty seskupené do bloků; async konverze
- Strom šablony — záložka "Strom" v hlavním okně; hierarchický přehled celé šablony (sekce → skupiny → elementy)
- LineElement — custom dash/gap pro čárkované a tečkované čáry
- ImageElement — SKImage rendering s SKSamplingOptions (kvalitnější škálování)
- **Zastávkový JŘ** — 3 specializované elementy: `StopListElement` (tabulka zastávek), `DepartureGridElement` (mřížka minut per skupina dnů), `NotesLegendElement` (legenda poznámek); `ScheduleElementBase` abstraktní základ s Build helpery, cache a sdílenými font/paint helpery (`CreateCellFont`, `CreateCellTextPaint`, `CreateCellBgPaint`, `MakeFontStyle`); ~30+ konfigurovatelných properties; SkiaHeightCalculator integrace; DepartureGrid: redistribuce šířek sloupců na celou šířku, word-wrap záhlaví skupin, sloučený řádek záhlaví (Hod + názvy skupin); per-column nastavení (`DepartureGroupColumnStyle` + `ColumnVisualStyle`/`CellStyle`: viditelnost, barvy, font, zarovnání H+V, bold/italic, padding) přes `DepartureGroupEditorDialog`; StopList: per-column styling analogicky k DepartureGrid, šipka směru jízdy (┌─▼)
- **Caché ZastavkoveJrMhd** — jedna třída `ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd` dědící přímo z `BaseAgenda`; kompletní 6-fázový tiskový engine MHD + všechny helpery (BuildZastavka, BuildOdjezd, BuildSkupinaDnu, BuildPoznamka, AddAnotace, AddAtribut, AddPoznamkaZastavky, ConvertSpecialniZnak) + GetSchema/GetData/GetMockData
- **Caché ZastavkoveJrAd** — agenda zastávkového JŘ z dat **hlavního JŘ modulu** (AD/JRMAIN, čte `^JRL`/`^JRZ`/`^JRSET`); čistá reimplementace legacy `ALVA.TISKMHD.Data` větve VolanoZ=JRMAIN (nevolá legacy MAC routiny). FTL 0-3 sloupcové layouty (`ZarazeniSpoje`/`NacistSkupinyDnu`), datumová omezení jede/nejede (`SestavTextOmezeni`, 1:1 TODDO^JRapiNZUtl), POPIS_DOPRAVCE z `^JRSET("DOP")` (vč. adresy provozovny), názvy zastávek + CIS pole, terminus-skip, multi-page (zastávka × směr). **Rozdíly tras** (`RozdilyTras`, 1:1 `OmTrasa^JRapiLinPrtMhd`: „neobsluhuje zast. X", marker od „B"). **Atributy zastávek** filtrované jen na „na znamení" (legacy UVOD tiskne jen `x`). **Atributy spoje** (bezbariér `inv`/`cbe` → „H"/„F", `BezbarierKonfig`). **Časy jízdy ve StopListu** — **multi-varianta** (L1, 2026-07-02): dynamické variantní sloupce dle časového profilu W-mapy (`TRASA_MIN[]` + `VARIANTY_TRAS[]`, N dle dat, 1:1 gold 0365005), přepínač `ShowAllRouteVariants` (vyp = jeden sloupec = `ZakladniCisloTrasy`). **Performance:** per-linka/per-směr invarianty + W-trasa cachovány do `^||TiskAd` (behavior-preserving). 1:1 ověřeno proti gold oracle na 0410101 (FTL0), 0425017 (FTL1), 0320144 (FTL3), 0320993 (FTL2), 0365001 (rozdíly tras + bezbariér + časy). **Uživatelsky volitelné rozdělení sloupců** (param „Rozdělení sloupců" = override FTL: auto/dle linky + 4 módy pokrývající FTL0-3; server-side param → re-fetch). Zbývá: vizuál legendy, jemné custom rozdělení (libovolné dny), jemnější výběr variant (per-písmeno). Detail: `.claude/docs/ad-data-mapa.md §10`
- **DataContext vnořená pole** — ParseMockData rozlišuje JsonArray/JsonObject vs skalární hodnoty; ScheduleElementBase parsuje vnořená pole (ZONY[], ATRIBUTY[], ANOTACE[], POZNAMKY_ZASTAVKY[])
- **Multi-data PDF export + PrintDialog** — `RenderMultiDataToPdf` zpracovává více DataContextů (1 zastávka/směr = 1+ PDF stránek); PrintDialog s parametry a exportem reálných dat z Caché (`GetData`); tisk i mock preview v jednom dialogu. **Členění výstupu** (Vše do jednoho / Per linka / Per zastávka×směr) + **název souboru z dat** (`{POLE}` tokeny, default `{CISLO_LINKY}_{POPIS_ZASTAVKA}_{POPIS_SMER}`); split → výběr složky + N souborů, kolize → `_2`/`_3`. Implementováno jako universální export pole v EDInputformu (`AgendaParams.cls`: `__EXPORT_SPLIT`/`__EXPORT_FILENAME`, mimo `GetParameters` → ne v preview liště), persistence per-agenda přes `PrintParams`. TODO extrakce na Template/agenda-config (roadmap). **Export z hlavního okna async nad snapshotem šablony** (T2, 2026-07-02): fetch + render na threadpoolu, `TemplateSerializer.DeepClone` → editace během exportu výstup neovlivní (exportuje se stav při kliknutí), dvoufázový progress ve status baru, UI stav (instance/parametry/kultura) zachycen na UI threadu před Task.Run
- **Imposice (N-up) při PDF exportu** — skládání logických stránek na fyzické listy (2×A5 → A4 naležato apod.); vlastnost výstupu, ne šablony (designer/náhled beze změny). `Models/Layout/SheetLayout.cs` (`SheetComposer` — čistá logika, `SheetLayoutKeys` wire kontrakt), exportní pole `__EXPORT_SHEET`/`_GRID`/`_REPEAT`/`_MARKS` v PrintDialogu (Caché `AgendaParams.AddExportFields`, persistence per-agenda), imposiční větev `RenderMultiDataToPdf`. Mřížka AUTO = největší 1:1 fit (bez zmenšování), opakování stránky (táž vývěska N× pro řezání), ořezové značky. Platí pro PrintDialog (mock + ostrá data, uvnitř split souborů) i export z hlavního okna; PNG bez imposice. Brožura/duplex = v2 (knižní JŘ)
- Word wrap + dynamická výška TextElementu — `WordWrap=true` + `Height=null` → text se zalamuje a výška se dopočítá z obsahu; `LineSpacing` property
- **PageLayoutEngine** — čistý logický engine (vrstva 3): Template + DataContext → `List<Page>` s PlacedSections; multi-page stránkování, hlavičky/patičky (OnEveryPage, OnFirstPageOnly, OnLastPageOnly, OnEveryPageExceptFirst), SplitAcrossPages, PerDataRow expanze, flow layout uvnitř sekcí, PAGE/TOTAL_PAGES injekce, dvouprůchodový layout pro OnLastPageOnly
- Multi-page preview — PreviewCanvas zobrazuje všechny stránky pod sebou se stíny a rámečky
- Multi-page PDF export — každá stránka LayoutResult = jedna stránka PDF
- **MVVM refaktor** — code-behind → ViewModel: `UpdateStatusForSelection`, `SelectFromTree`, `InsertOrCopyPlaceholder`, `AddElementAtPosition`, `HandleElementHit`, `HandleEmptyAreaHit`, `CutSelected`, `FitToPage`; `TextWrapHelper` extrakce; `DesignerState` ZIndex cache; dirty flag + `ConfirmUnsavedChanges`
- **UX vylepšení** — Ctrl+X (Cut), Ctrl+0 (Fit-to-Page), potvrzení mazání sekce, validace VisibilityExpression v UI (live chybové hlášky), nahrávání obrázků s auto-downscale (max 5 MB)
- **SectionRole (Header/Body/Footer)** — oddělení "kdy tisknout" (PrintBehavior) od "kam umístit" (Role); patičky se umisťují od spodu stránky v designeru i v PageLayoutEngine
- **Canvas fixed height** — stránka má pevnou výšku, sekce se clampují aby nepřesáhly stránku; footer sekce kotveny ke spodnímu okraji
- **Context menu na canvasu** — pravý klik: element items (Cut/Copy/Paste/Delete/Z-order/Vlastnosti) + section items (Vlastnosti sekce/Přesunout/Smazat) sloučeny do jednoho menu
- **Bound resize + clamp** — element při resize/přesunu nepřesáhne hranice sekce (clamp na PrintableWidth × EffectiveDesignerHeight)
- **StopColumnsEditorDialog** — dvou-tabový dialog: Tab 1 "Obecné" (PropertiesPanel), Tab 2 "Sloupce" (pořadí, šířky, záhlaví, viditelnost, per-column styling přes CellStyleVM)
- **PageSize editor** — editace rozměrů stránky, orientace, předdefinované formáty
- **Smart snap resize** — snap hran při resize (edge + gap 4mm); split element Ctrl+Shift+H/J. (Sdílená hranice „tah za hranu mění i sousedního" odstraněna — mátla, resize teď mění jen vybraný prvek.)
- **Designer font cap** — omezení fontu na 10pt v designeru pro čitelnost placeholderů
- **Rámeček elementů (border)** — `BorderColor` + `BorderWidth` na base `TemplateElement`; dvouprůchodové renderování (obsah → rámečky mimo clip sekce); pixel-snap na sdílených hranách → jeden rámeček; viditelné i na hranici sekce
- **Auto-scale font u JŘ elementů** — `ScheduleElementBase.ComputeFontScale(naturalH)`: pokud obsah přesahuje pevnou Height, fonty + výšky řádků se proporcionálně zmenší; schedule elementy nyní podporují pevnou Height (ne jen null/dynamickou)
- **Varování o přetečení v preview** — když se obsah stránky nevejde (přirozená výška > pevná Height), přetékající stránka dostane červený rámeček + status bar červený blok „⚠ Obsah se nevejde na stránku". Detekce přes abstract `ScheduleElementBase.CalculateHeightFromData` + `ContentOverflows` (jakékoli přetečení, ne práh); `PreviewCanvas.PageOverflows` běží před renderem, `OverflowChanged` callback → `HasPreviewOverflow` ve VM. Jen preview, ne v PDF/PNG
- **Cap roztažení DepartureGrid** — `AutoExpandToFillHeight` má strop `MaxAutoExpandFactor=1.5` (řídká mřížka se neroztáhne na bizarně obří řádky)
- **Multi-element editing** — Enter při výběru více elementů otevře dialog společných vlastností (BorderColor, BorderWidth, ZIndex, IsLocked); změna se aplikuje na všechny najednou
- **Ikonový toolbar** — 33 Fluent UI System Icons (Microsoft, MIT); 1 band, 7 skupin (Soubor, Upravit, Přidat, Elementy, Vrstva, Zarovnat, Zobrazení); ToolbarStyles.xaml; maximalizované okno; preview 50:50 star sizing
- **Inline properties panel** — záložka "Vlastnosti" v levém panelu; automatická aktualizace při výběru elementu; modální dialog zachován jako fallback (Enter/dvojklik)
- **TextStyle unifikace** — `TextStyle` (nullable POCO, CSS cascade přes `Resolve()`) + `ResolvedTextStyle` (struct se SkiaSharp helpery: `CreateFont`, `CreateTextPaint`, `CreateBgPaint`); migrace všech elementů (TextElement, TableElement, ScheduleElementBase + StopList/DepartureGrid/NotesLegend); `BackgroundColor` na base `TemplateElement`; `CopyBaseTo()` eliminuje Clone() boilerplate; breaking JSON change (flat text props → composable `style`/`headerStyle`/`dataStyle`); `Strikethrough` property; `DrawTextDecorations()` pro Underline + Strikethrough (SkiaSharp 3 nevykresluje automaticky)
- **Proporcionální škálování šablony** — `ScaleTemplateCommand`: škáluje PageSize, margins, sekce (výšky), elementy (pozice, rozměry, mm/pt properties); `ApplyScale(float)` virtual metoda na všech elementech; dropdown v toolbaru (50%–200%); `LineSpacing` se neškáluje (bezrozměrný násobitel); undo přes inverzní škálu (1/scale)
- **Redistribuce výšek sekcí** — při smazání sekce se uvolněná výška proporcionálně rozdělí mezi zbývající; při přidání sekce se stávající proporcionálně zmenší; `AddSectionCommand` pro undo/redo
- **Výchozí nastavení nových elementů** — TextElement bez placeholder textu, zarovnání Center/Middle; všechny elementy: černý rámeček 0.1mm (jen nové — model defaults zůstávají pro zpětnou kompatibilitu JSON)
- **DepartureGrid: HourColumnPerGroup** — `HourColumnPerGroup=true` zobrazí sloupec hodin u každé skupiny dnů (nejen vlevo)
- **DepartureGrid: NZ invertovaně u minut** — negativní značka (`TypAnotace.NegativniZnacka`) se kreslí jako černý box + bílý znak (legacy shoda); `InvertovatNegativniZnacky` (default zap) + `NegativniZnackaBackColor`/`NegativniZnackaForeColor`. MHD má, AD záměrně ne (CNZ tam jen klasifikuje sloupce dnů)
- **AI Vision import PDF (JŘ)** — `AiVisionImportService` (composable `AgendaPromptContext` prompt per agenda) + `AiGuidedScheduleBuilder` (rasterizace → Claude Vision → rozpoznání struktury → přesné elementy z PdfPig geometrie) + `AiVisionImportPipeline` (orchestrace, fallback na heuristiku); model `AiAssistant.VisionModel` (default claude-sonnet-5, adaptivní myšlení, max_tokens 16000). **Robustnost proti stochastice modelu**: clamp rel. souřadnic 0–1 + pipeline clamp do tiskové plochy (NotesLegend s pevným boxem z bboxu), QR jen s důkazem v geometrii (`LooksLikeQrRegion`), dedup obrázků + placeholder ne přes existující element, **retry při chybějícím StopList/DepartureGrid** + viditelné varování. Sloupec Platform (nástupiště). Sdílený `AiPromptHelpers.AppendDataSchemaSection` (DataSchema sekce promptu, společná s enricherem). Heuristický enricher (`AiImportEnricher`, haiku) bez UI vstupu — vision ho nahradil, služba zůstává jako fallback kandidát
- **PdfImportDialog standardizován** — uživatel volí jen PDF (+ stránku u vícestránkových); typ importu odvozen z otevřené agendy (zastávková → AI JŘ import, jiná → obecný layout LinesOnly), AI vždy zapnuté, agenda = aktuální; **JŘ import = právě jedna stránka** (extrahuje se jen ta); bez dostupné AI je JŘ import zablokován s důvodem z mlweb; debug checkbox + soubory jen pro vývojáře (lokální dev klíč), bez popupu
- **DepartureGrid: per-column font wrapping** — `ComputeLayout` respektuje per-column header font pro wrapping i min width měření; dynamická výška záhlaví z max per-column line height; `GroupColumnIndexResolver` — generický `BuildLookup<T>` s `IndexKeyPrefix` konstantou pro resolve `__INDEX_` klíčů
- **Skrytí záhlaví sekcí** — `DesignerState.ShowSectionHeaders` toggle; auto-detekce JŘ šablon (skryje záhlaví automaticky); toolbar menu položka
- **CellStyleVM font defaults** — dialog sloupců zobrazuje skutečné font hodnoty z elementu (ne prázdná pole); `headerDefaults`/`dataDefaults` parametry
- **AnthropicApiClient: shared HttpClient** — statický `HttpClient` s per-request headers místo per-call instancí
- **EDISONAI/mlweb napojení (`Services/EdisonAiService.cs`)** — spravovaný API klíč pro zákazníky: mlweb config z Caché `ALVA.EDISONAI.API.Config.GetMLWebConfig`, resolve `POST /api/mline/agent/resolve` (fail-closed, 60s cache, jen provider claude), spotřeba `POST /api/mline/usage` se `source: "templatedesigner"` (best-effort, `AnthropicApiClient` čte `usage` z každé odpovědi). Lokální `AiAssistant.AnthropicApiKey` = dev bypass (mlweb se nevolá, spotřeba se nehlásí). Model v režii TD (`AssistantModel`/`VisionModel`), ne z mlweb. Hlášky nedostupnosti jednotně nesou důvod z mlweb (pomocník popup / generátor / import červený text); tlačítko „Požádat o přístup" TD nemá (jen EDISON chat) — zákazník jde přes helpdesk
- **AI pomocník + generátor šablon na Sonnet 5** — `AiAssistant.AssistantModel` (default claude-sonnet-5): pomocník volá s **vypnutým myšlením** (`thinking: disabled`, rychlé lookup odpovědi), generátor s adaptivním; temperature se na S5+ neposílá (`ModelSupportsTemperature`, jinak 400); parsování přes `ExtractText` = první text blok (thinking-safe). Pomocník: znalosti z `CapabilityCatalog` (SystemRole s trojstavovou nejistotou + groundingem, GeneralIntro s UI mapou Co→Kde, auto-gen katalog elementů dle ElementKind); eval sada ~21 otázek v `roadmap.md` (sekce AI integrace). Generátor: SystemPrompt srovnaný s reálným modelem (vnořený `style`, DynamicTableElement, plný BarcodeType); menu „AI Asistent šablon" před produkcí skryté (vidí jen vývojář s lokálním klíčem), ruční zadání klíče v dialogu jen dev
- **Nápověda F1 datově řízená + hledání** — obsah v `HelpDialogViewModel.BuildContent()` (kategorie → řádky akce/zkratka → tip; při nové zkratce aktualizovat TAM + ux-conventions tabulku); fulltext filtr bez diakritiky (Ctrl+F fokus, Esc čistí), zákaznický jazyk bez anglicismů
- **MmMath helpers** — `MmMath.Round(mm)` zaokrouhlí na 0.1mm (eliminuje IEEE-754 artefakty); `MmMath.Snap(mm, step)` přichytí na libovolný krok; nullable overload pro `Height`; aplikováno v EndResize, ScaleTemplate, DistributeH/V, PropertiesPanel
- **Normalizovat pozice** (`Ctrl+Shift+N`) — dialog s volbou kroku (0.1–5mm), live preview počtu změn, `CompositeCommand` undo; opraví float artefakty v existujících šablonách
- **Grid size v UI** — ComboBox 1/2/5/10mm v toolbaru; `DesignerState.GridSize` bindovatelná property (výchozí 5mm)
- **BarcodeElement rozšíření** — EAN-13, EAN-8, PDF417, DataMatrix; `static readonly` hints/writer/dashEffect; paleta konsolidována na 1 tlačítko "Kód"
- **TextStyle Strikethrough** — `Strikethrough` property + `DrawTextDecorations()`; oprava tichého nefungování Underline v SkiaSharp 3
- **Jednotná kaskáda stylů buněk** — 4-vrstvá CSS-like kaskáda `column.Style → element.HeaderStyle/DataStyle → table/schedule defaults → CellDefaults` jednotně ve StopList, DepartureGrid, TableElement; `Rendering/CellDefaults.cs` (autoritativní defaults), `Rendering/CellRenderer.cs` (jediné místo pozicování textu buňky: bg → padding → HAlign+VAlign → decorations), `Rendering/ColumnRenderer.cs` (orchestruje řádek sloupců), `Rendering/CellStyleEditOptions.cs` (per-kontextové skrývání UI); `TableColumn.DataStyle` + `TableElement.DataStyle` (per-column + element-level override datových buněk); `col.Alignment` legacy fallback zachován; TableElement nyní respektuje HAlign/VAlign/Padding/Underline/Strikethrough (dříve hardcoded baseline+4px offset)
- **Override nadpisu sloupce/skupiny** — `Rendering/ColumnHeader.Resolve(override, source)` = jediné místo sémantiky „override přebíjí zdroj, prázdné → zdroj"; sdílené **všemi** tabulkovými komponentami (StopList/TableElement zdroj = autorský `Header`; DepartureGrid `DepartureGroupColumnStyle.HeaderOverride` přebíjí nadpis skupiny dnů z dat, vč. sloupce Hodin přes `HourColumnHeader`). Edituje se v DepartureGroupEditorDialog; uzavírá AD-N6. Layout cache DepartureGridu hashuje override + pořadí
- **Víceřádkové záhlaví/buňka přes `\n`** — `CellRenderer.DrawCell` má multi-line větev (blok N řádků, VAlign umístí blok, HAlign per řádek); `CellLayout.SplitLines`/`LineCount` normalizují literální token `\n` i skutečný zlom (Enter, `AcceptsReturn` na poli Záhlaví). Sdílené → všechny tabulkové elementy. `StopListElement` výška záhlaví line-aware (`× MaxHeaderLines()` v Render i CalculateHeightFromData). Použití: dvojjazyčné (CZ/EN) záhlaví sloupce. TableElement pevná `HeaderHeight`, DepartureGrid/LineTable datově řízená záhlaví → bez auto-výšky. Bez migrace (`Header` = string s `\n`)
- **Sdílený editor sloupců (Fáze 1, krok 2a)** — `Controls/ColumnListEditor` UserControl + `Controls/IColumnRowVM` (`DisplayName`/`Visible`/`HasAnyOverride`/`ColumnWidth?`): jednotný levý seznam sloupců pro Zastávky i Odjezdy (checkbox + název + override-tečka + „X mm" jen u pevné šířky + ▲▼). Pořadí skupin/sloupců dle `GroupColumns` (`ResolveSkupiny` + ctor editoru — reorder přežije reopen). Zarovnání jednotně přes HAlign v `CellStyleEditor` (odebráno duplicitní inline combo). **Zbývá:** krok 2b proporční auto šířka v obou, krok 4 Obecné→EDInputForm u Odjezdů
- **Sdílený CellStyleEditor** — `Controls/CellStyleEditor.xaml` UserControl konzumovaný StopColumnsEditorDialog, DepartureGroupEditorDialog, TableEditorDialog; `CellStyleVM` rozšířen o `VAlignmentIndex`, `Underline`, `Strikethrough`, `LineSpacing`; per-dialog omezení přes `CellStyleEditOptions` (HeaderEditOptions/DataEditOptions na VM); `TableEditorDialog` má nový tab „Styl sloupců"
- **Tri-state editor stylů + placeholdery zděděných hodnot** — `CellStyleVM` všechny vlastnosti nullable (null = dědit); `Inherited: ResolvedTextStyle` z elementu (přes public `Resolve<Zone>Parent()`); XAML zobrazuje zděděné hodnoty jako šedivý italic placeholder v prázdných polích; `EffectiveBold/Italic/Underline/Strikethrough` derived pro 2-stavové checkboxy; „×" reset tlačítko viditelné jen při explicit override; `ToModel()` emituje nullable TextStyle (idempotentní open+OK beze změn); `CellStyleEditor.Converters.cs` (NullToVis, NotNullToVis, FirstNonEmptyHexToBrush, HAlign/VAlignToLabel)
- **Click-vs-drag threshold** — pouhý klik na element (selekce, otevření properties) nespustí soft-snap; element se začne hýbat až po překročení 4px myší. `_dragHasMoved` flag v `MainWindow.xaml.cs`; `EndDrag` necommituje, pokud myš nepřekročila threshold. Reason: před fixem klik při GridSize=5mm „strhnul" 0.1mm pozici elementu na 5mm grid
- **Per-template editor preferences** — `Models/TemplateEditorPreferences.cs` (nullable `GridSize`/`SnapToGrid`/`SnapToElements`/`ShowGrid`); `Template.EditorPreferences` property (vedle `Parameters`, `SampleData`, `CultureCode`); apply on Open / capture on Save → designové prostředí (jemný grid pro precizní šablonu) přežije reload. `RequestGridSizeUiSync` callback synchronizuje nebindovaný `CmbGridSize` ComboBox
- **Sjednocené Header/Data zóny v panelu** — princip „rychlý přístup × cascade": 4 base barvy (Header bg/text + Data bg/text) v properties panelu napříč všemi tabulkovými elementy (StopList/DepartureGrid/NotesLegend/TableElement); detailní cascade (font/align/padding/per-column overridy) zůstává v dialogu sloupců. Helper `ScheduleElementBase.GetCellZoneBaseColorDescriptors()`; TableElement parallel implementation (jiná hierarchie). Skupiny „Záhlaví"/„Datové buňky" konzistentně. Round-trip safe (panel↔dialog editují totéž pole)
- **Caché Template Storage** — třída `ALVA.TemplateDesigner.API.Sablony` s 5 metodami (`ListTemplates`, `LoadTemplate`, `SaveTemplate`, `DeleteTemplate`, `RenameTemplate`); per-IDFa storage v `^ALVA.TemplateDesigner.Templates(IdFa,"DTA",TemplateID,"Metadata"|"Data")` + index `ByAgenda`; JSON wire formát; metadata Name/AgendaCode/Author/CreatedAt/ModifiedAt/Version. .NET strana: `CacheTemplateService` (async wrapper přes Task.Run + `CacheTemplateException`), `CacheTemplatePickerDialog` (správce: list + filtr agendy + search + Open/Rename/Delete), `SaveToCacheDialog`. **Databáze = primární úložiště** (2026-06-30): Otevřít (Ctrl+O) / Uložit (Ctrl+S) / Uložit jako → databáze; **soubor degradován na Importovat z JSON / Exportovat do JSON** (export nemění původ). Titulek ukazuje `[DB]` / `[neuloženo]`. Nová/importovaná → Ctrl+S založí do DB (SaveAs s jménem); existující → tichý update. „Nástroje → Šablony v databázi" = správce. `_loadedFromCache` flag + SaveCommand routing
- **Caché namespace struktura** — separace **podle role třídy**: `ALVA.TemplateDesigner.API.*` (obecná business logika TD, např. `Sablony` CRUD), `ALVA.TemplateDesigner.Agendy.*` (agenda implementations: `BaseAgenda` abstract + `ZastavkoveJrMhd`, `Faktury`), `ALVA.TemplateDesigner.EDList.*` (dispatch tridy pro EDList komponentu: `Sablony`, `Instance`). Folder/namespace 1:1 mapuje na roli, doménový název může opakovat napříč rolemi (`API.Sablony` vs `EDList.Sablony` jsou jasně rozlišené). Budoucí komponenty: `EDInputForm.*`, `EDInputGrid.*` (Wave 4+).
- **GENERAL agenda fallback** — `cacheClass = ""` → šablona bez DB vazby (statický obsah); CacheAgendaProvider short-circuit ve všech metodách; BuildAllPreviewDataPages vrací `[new DataContext()]` místo `[]` → PDF/PNG export funguje i bez agendy. Po Wave 2 W2-1 je GENERAL syntheticky injektován v `CacheAgendaRegistry` (auto-discovery ji nenajde — nemá Caché class).
- **Wave 2 W2-1 — Agenda registry v Caché** — auto-discovery z `BaseAgenda` subclasses. Caché `ALVA.TemplateDesigner.API.AgendyRegistr.ListAgendy` přes `%Dictionary` SQL najde subclasses + jejich metadata → JSON pole. Eliminuje `appsettings.json agendas[]` (smazáno). .NET: `IAgendaRegistry` + `CacheAgendaRegistry` + `AgendaRegistryEntry` POCO; App.OnStartup sync load s 3s timeoutem, GENERAL agenda syntheticky prepended. Per-customer kontrola (AgendaEnabled, per-IDfa override) odložena — YAGNI.
- **Dvouosý model agend: Agenda × Varianta** (2026-07-11, z legacy analýzy) — **Agenda** = nejvyšší úroveň členění („Zastávkový JŘ", „Linkový JŘ", „Faktury", „Obecná"; budoucí RG agendy přibudou vedle), **Varianta** = datový zdroj („AD"/„MHD" — ekvivalent legacy `VolanoZ`; předvolená launch kontextem). `BaseAgenda` parametry: **`AgendaVariantCode`** = kombinovaný stabilní persistovaný klíč (`ZASTAVKOVY_JR_AD`… — hodnoty se NIKDY nemění: šablony JSON `agendaCode`, DB index ByAgenda, preference, EDISON launch) + `Agenda`/`AgendaLabel`/`Variant`/`VariantLabel`. Wire `ListAgendy` aditivní (staré klíče `code`+`label` v kombinovaném formátu zůstávají pro nasazené klienty). .NET `AgendaRegistryEntry.DisplayName` („Zastávkový JŘ · AD") → status bar chip, titulek okna, seskupený ChangeAgendaDialog (GroupStyle per agenda), filtr pickeru šablon. `Template.AgendaCode` v C# = kombinovaný klíč (název drží s wire; nechat). Duální role MHD/AD (agenda provozních tisků × varianta JŘ) je záměr — zrcadlí legacy (RG agenda MHD × TiskMHD `VolanoZ=MHD`).
- **Přepínač agendy** — `ChangeAgendaDialog` (list agend s „(aktuální)" badge); 3 entry pointy: menu „Nástroje → Změnit agendu...", **Ctrl+Shift+A**, klik na agenda chip ve status baru. Přepnutí vytváří **novou prázdnou šablonu** se zvolenou agendou (po Save/Discard/Cancel) — sdílený `ReplaceWithBlankTemplate(agendaCode)` helper s NewTemplate. Permanentní viditelnost agendy na 3 místech: status bar (zelený klikatelný chip), window title prefix `[MHD]`, sidebar „Pole agendy" header
- **Wave 1 + 1.5 — per-user preferences přes Caché** — `Services/TdPreferencesService.cs` (typed wrapper nad `IUserPrefsService`); ukládá per (IdFa × user) do `^ALVA.EDControls.UserData`. 5 blob konzumentů: `MainStatePref` (zoom, grid, snap, splittery, tab index, preview pane viditelnost — Typ 1), `RecentFiles` (cap 10, dedup — Typ 2), `RecentColors` (cap 8 — Typ 2), per-template `InstanceDefaults` (key = template Guid — Typ 3), per-agenda `PrintParams` (key = AgendaCode — Typ 3 per agenda, ne per template — params jsou agenda-level concerns). App.OnStartup async `LoadAsync` + 200ms wait + event fallback; MainWindow.Closing fire-and-forget save; Recent Files submenu dynamicky populated; instance defaults auto-load na Open template, save na InstancePicker OK; PrintDialog operuje nad sdíleným dict s preview lištou (live cross-notify); ColorPickerHelper integrace recent colors (DRY — odstraněn duplicate WinForms ColorDialog kód z PropertiesPanel). **Žádné lokální JSON soubory** — per-machine pattern v TD nepoužíváme (běží embedded z EDISONMAIN2). Registr scope ownerships + 3-typová taxonomie state viz `EDControls/DOCS/05-userdata-scopes.md`
- **Instance Picker (multi-výběr linek)** — `InstancePickerDialog` se search boxem (Enter), checkboxy multi-select, Vybrat vše / Odznačit vše. Caché `AgendaAPI.GetAvailableInstances` (abstract default empty) + impl v `ZastavkoveJrMhdAPI` iteruje `^ALVA.MHD.LSO(IdFa,1,"IDX",1,1,1,SkupinaLinekID,LinkaID)` — pouze linky aktivní skupiny linek (default první nezblokovaná z ENUMS, optional IN parametr `SKUPINA_LINEK_ID` pro Edison-driven kontext); TrasaID filter pro orphan linky; case-insensitive substring search server-side; limit 50. POCO `AgendaInstance(Value, Display, Subtitle?)`. `ActiveInstances` state v VM (mimo Template — session scope), `ActiveInstanceIndex`, `CurrentActiveInstance`. Status bar 2. modrý chip „Instance: X — Y (N/M)" s ◀▶ tlačítky; klik otevře picker. `RefreshDataForCurrentInstanceAsync` async GetData při změně instance, status feedback. Multi-instance batch v `BuildAllPreviewDataPages` (PDF/PNG) i `PrintDialog buildRealData` — 600 stránek za ~9s (5s fetch + 3.6s render, ~15ms/stránka, top decile vs commercial reporting). Reset instancí při změně agendy
- **Embedding fontů do .exe (`FontResolver`)** — `.ttf`/`.otf` v `dotnet/Fonts/` (glob `<EmbeddedResource>`) se zabalí do single-file .exe a `FontResolver` je auto-načte podle rodiny (bez úpravy kódu); priorita `embedded → OS → Arial`, embedded = deterministický výstup nezávislý na stroji. Sdílené cachované typefacy (nedisposovat). Vše rendrování fontů teče přes `FontResolver.Resolve`. ⚠️ Jen redistribuovatelné licence (OFL/Apache — Arimo/Liberation), NE Helvetica/Arial. JŘ symbolový font (C2) čeká na zdroj/licenci
- **UI font škálování přes EDTheme/FontService** — UI fonty TD na `{DynamicResource EDTheme.FontSize.*}` / `EDTheme.FontFamily` (Small/Caption/Body/Header/Large); TD respektuje sdílenou user font preferenci z EDISONMAIN2 (`FontService` už TD inicializuje v `App.OnStartup`) a proporcionálně škáluje bez restartu. `Consolas` (výrazy/glyfy) záměrně ponecháno. Comboboxy v hlavním okně na `Width="Auto"` + `MinWidth=64` (neuřízne text, roste s fontem)
- **Detekce přetečení = jen reálné geometrické přetečení** — `PreviewCanvas.PageOverflows` hlásí jen u **`KeepTogether`** sekcí (dělitelné `SplitAcrossPages`/`AllowSplit` engine chunkuje přes stránky → obsah se neztratí, nevaruje): (a) element přesahující spodní hranu sekce, (b) **sekce vyšší než tisková plocha** (`placed.Y + placed.Height > PrintableHeight` → nejde rozdělit, obsah se ořízne). JŘ element s pevnou výškou, který se do boxu vejde auto-scalem, nevaruje (`ScheduleElementBase.ContentOverflows` zůstává jako helper „box < obsah", neřídí varování). Tisk: `PreviewCanvas.CountOverflowingPages` (layout-only) projede všechny instance a `PrintDialog` při exportu reálných dat vypíše „⚠ X z N stránek přetéká"

- **EDISONMAIN2 launch kontrakt** — `App.OnStartup` parsuje argumenty kontraktu
  (`EDControls.EdisonLaunchArgs`, zrcadlí `EDISONMAIN2API.FormToRunGetter`); při spuštění ze shellu
  se MLRest staví ze session shellu (server, rest port, namespace, IdFa, login, fullName, mlp) místo
  z appsettings. Bez argů standalone beze změny; nevalidní argy → varování + fallback. `shellGuid`
  se nepřebírá (vlastní session GUID, bez vazby na ACTIVITY zámek shellu). Spouštěcí místa: strom
  starého shellu (wrapper `ALVA.TISKJR.formTISKJRTemplateDesigner`), MHD2 „Linky a oběhy".
  `inputParam` kontraktu ve tvaru `Agenda=<kód>` předvolí startovní agendu šablony (JŘ uzel stromu
  → `ZASTAVKOVY_JR_AD`, MHD uzel + MHD2 tlačítko → `ZASTAVKOVY_JR_MHD`); neznámý kód → default.

### 🐛 Známé problémy

*(žádné aktivní)*

---

## Technologický stack

| Technologie | Použití |
|---|---|
| .NET 10, WPF | UI framework |
| SkiaSharp 3.119.2 | Renderování canvasu i PDF |
| SkiaSharp.Views.WPF 3.119.2 | SKElement WPF kontrola |
| System.Text.Json | Serializace šablon |
| EDControls (ProjectReference) | EDViewModel, RelayCommand |
| ZXing.Net 0.16.9 | BarcodeElement |
| PdfPig 0.1.13 | PDF import (extrakce textu, čar, obrázků) |

---

## Pravidla pro psaní kódu

- **Jazyk kódu: anglicky** (třídy, metody, proměnné); komentáře a UI texty: **česky**
- Každá akce uživatele = `IDesignerCommand` (undo/redo podporováno)
- Pozice elementů jsou **relativní k sekci**, jednotky = **milimetry**
- `Height = null` = dynamická výška (print engine počítá); `IsLocked = true` = nelze editovat
- `VisibilityExpression = null` = vždy viditelný; neprázdný = výraz vyhodnocuje ExpressionEvaluator
- `SKElement.PaintSurface` registrovat **jen v code-behind**, nikdy v XAML
- **Vždy `CVar`** pro MLRest volání; nikdy `object[]` v novém kódu
- Data binding syntaxe: `{NAZEV_POLE}` — konzistentní napříč celým systémem
- ObjectScript: **nikdy `_` v dot-notation** (`obj.KEY_NAME`) — `_` je operátor konkatenace; použij `obj.%Set("KEY_NAME", v)`
- Nová okna vždy v `dotnet/Windows/<Název>/` se vzorem `Window + ViewModel : EDViewModel`

---

## Vývojová pipeline (agent ji jede sám z jednoho zadání)

Tohle je **pasivní pravidlo, ne skill k ručnímu vyvolání** — platí pro každou úlohu automaticky.
Uživatel dodá **featuru + volitelný kontext** a dělá **prioritizační a architektonická rozhodnutí**;
mechaniku (kód → testy → docs → wiki → commit) veze agent sám, bez čekání na dílčí pokyny.

0. **Zadání & kontext** — ujasnit konkrétní featuru, vstřebat extra kontext od uživatele, zařadit do scope/roadmapy.
1. **Projít kód** — přečíst relevantní existující kód a pochopit současný stav (ne psát naslepo).
2. **Navrhnout testy** — čím se featura ověří (xUnit / Caché `%UnitTest` / ruční golden path), ideálně před implementací.
3. **Implementovat** — dle pravidel (`.claude/rules/`, R1–R5, kód EN / komentáře CZ, CVar, MVVM). Auto-build hook hlásí chyby.
4. **Otestovat & iterovat** — spustit testy, u UX ověřit v běžícím buildu; body 3–4 opakovat, než to vypadá dobře.
5. **Lokální docs** — changelog vždy; roadmap (hotové → archiv, posun šipky); CLAUDE.md „Co máme" jen při arch změně.
6. **Wiki** — jen pokud featura mění user-facing chování **a projekt má linknutý článek**: aktualizovat seed + sync (TipTap-safe, mapa seed↔UUID). Jinak přeskočit.
7. **Commit** — Janův scope, granular, `TD -` prefix. Gating viz níže.
8. **Další krok** — po commitu navrhnout **další logický krok v roadmapě** nebo jiný smysluplný postup.

**Gating commitu (chytré):**
- **Jednoduchá / mechanická změna, kterou nedává smysl ručně proklikat** (a testy projdou) → **commit ihned**, nečekat.
- **Vizuální / nová / větší featura, kterou má uživatel proklikat a vyzkoušet** → **necommitovat**, ukázat
  výsledek a **počkat na potvrzení**; teprve po potvrzení commit + krok 8.

**Co zůstává na uživateli:** co se dělá (priority), architektonická rozhodnutí, high-level kontext aplikace.
Ne mechanika kroků 1–8.

> ⚠️ **`roadmap.md` drž ČISTOU — jen aktivní úkoly.** Když je něco hotové (i rozhodnutí), **přesuň
> to KOMPLETNĚ do [`roadmap-archive.md`](.claude/docs/roadmap-archive.md)** — nenechávej to v roadmapě
> jako odškrtnuté „✅ hotovo" řádky. V aktivní roadmapě nesmí viset starý nepořádek; zůstává jen to,
> co se má dělat (+ krátký pointer do archivu, kde detaily jsou). Platí i pro rozhodovací tabulky.

### Wiki — mapování a sync (krok 6 pipeline)

Linkované wiki články drží **`.claude/wiki-map.json`** (allow-list). Pravidla:

- **Synchni JEN články z mapy.** Co v mapě není, se nikdy netkne (folder-level: platí nejbližší `wiki-map.json` nad měněným souborem; dnes jedna na úrovni TD).
- Krok 6: pokud featura mění **user-facing chování** pokryté některým `seed`em z mapy → aktualizuj ten seed a synchni jeho článek (MCP `update_wiki_article`, `id` z mapy). Jinak wiki přeskoč.
- **TipTap-safe markdown:** konvertor **NEUMÍ tabulky** (slije je do jednoho odstavce) → piš odrážky / definiční seznamy. OK: nadpisy, seznamy, odkazy, tučné/kurzíva, blockquote, `---`.
- **Seed = source of truth, wiki = zrcadlo** — na wiki needitovat ručně.
- Ověření po syncu drž **lehké** (nečti celý obsah zpět — velký článek přesáhne limit odpovědi; ověř `number`/`updatedAt`).
- Budoucí tvrdá pojistka (až se v praxi ukáže, že se na sync zapomíná): pre-push guard čtoucí `wiki-map.json`, případně CI přes REST.

---

## Architektura — vrstvy systému

```
5. Aplikace         WPF Designer, EDISON tisk, Server API, CLI batch
4. Renderer         SkiaSharp → WPF canvas / PDF soubor
3. PageLayoutEngine Section repeating, flow layout, page breaking (žádné UI/SkiaSharp)
2. Expressions      ExpressionEvaluator, PlaceholderFormatter, AggregationResolver
1. Models           Template, Section, TemplateElement, DataContext, DataSchema
```

**Klíčové hranice:** `PageLayoutEngine` (vrstva 3) nezná SkiaSharp — vstup: `Template + DataContext`, výstup: `List<Page>`.
`PageRenderer` (vrstva 4) nezná WPF — kreslí na `SKCanvas`. Díky tomu jde systém rozdělit:

- **TemplateDesigner** = vrstvy 1–5 (vše pohromadě, jeden .csproj)
- **EDISON tisk** = vrstvy 1–4 (bez WPF designeru, headless PDF)
- **Server/API** = vrstvy 1–4 (HTTP endpoint: šablona + data → PDF)

Preview v designeru = plný engine s mock daty. "Tisk" = tentýž engine s reálnými daty → PDF.

---

## Co projekt není

- Není to WYSIWYG Word/Excel editor
- Sekce jsou povinné — ne free-form canvas bez struktury
- V1 nepodporuje import starých šablon z InterSystems Globals

### Záměrně jinak — neřešit

Vědomá rozhodnutí, která se znovu neotevírají (ne mezery):

- **Databáze jako source of truth pro konfiguraci a šablony** — dává smysl pro doménovou logiku (Wave 1–3)
- **Univerzální Section třída** — jednodušší než pevná hierarchie (Crystal Reports)
- **Absolutní pozicování jako default** — správné pro tiskové sestavy s pevným layoutem
- **PropertyDescriptor a EDInputForm paralelně** — ne cut-over, per element migrace (Wave 4)
- **Server-side migrace šablon** — autoritativní, ne klientská (Wave 3)

---

## Publish — distribuce build .exe

Single-file `.exe` se generuje skriptem `publish.ps1` v rootu projektu:

```powershell
cd C:\Users\miesb\source\EDISONMAIN2\TemplateDesigner
.\publish.ps1
```

Skript volá `dotnet publish` s profilem `SingleFileCEdison` → výstup do `C:\Edison\TemplateDesigner.exe` (~20 MB, self-extracting bundle s `templatedesigner.appsettings.json` jako embedded resource).

**Před spuštěním** — zavři běžící TemplateDesigner.exe (skript hned na začátku ověří lock a friendly errne, pokud je soubor uzamčený).

**Pozn.:** Publish přes Visual Studio GUI je občas rozbitý (silent skip GenerateSingleFileBundle, build prošel ale single-file se nevyrobil). CLI variantou tohoto skriptu se to obchází.

---

## Detailní dokumentace (`.claude/rules/`)

Soubory se načítají automaticky při práci na relevantních souborech.

| Soubor | Obsah | Načítá se pro |
|---|---|---|
| `architecture.md` | Struktura projektu, zodpovědnosti, MVVM hranice, souřadnice, DesignerState | `dotnet/**` |
| `architecture-rules.md` | **Závazná pravidla R1-R5** (async Caché, IPopupDisplayer, CVar wire, MVVM bez WPF, versioning) + „must-not-happen" scénáře | `dotnet/**` |
| `data-model.md` | C# datové třídy, JSON formáty, renderovací pipeline, interakční model | `dotnet/Models/**` |
| `mvvm-patterns.md` | PropertyDescriptor, TableElement architektura, jak přidat element/okno | `dotnet/Windows/**` |
| `cache-api.md` | AgendaAPI architektura, wire formáty, Caché skeleton, nová agenda checklist | `CACHE/**` |
| `cache-database.md` | Mentální model DB: globály (LSO, ZST, CHR, ENUMS, SETTINGS), subscripty, business koncepty | `CACHE/**` |
| `implementation-patterns.md` | Barvy, RenderContext, ComboBox gotcha, SkiaSharp quirks | `dotnet/**/*.cs` |
| `ux-conventions.md` | Dialogová tlačítka (IsDefault/IsCancel), tooltipy, autoritativní tabulka keyboardových zkratek, context menu, confirmation patterns | `dotnet/Windows/**` |

## Dokumentace v `.claude/docs/` — kam co evidovat

> ⚠️ **NEZAKLÁDAT nový soubor na každou novou věc.** Složka je konsolidovaná do logických celků
> (2026-07-13, 26 → 18 souborů) — nový záznam patří téměř vždy do jednoho z nich. Index
> s kategoriemi: [`.claude/docs/README.md`](.claude/docs/README.md).

**Směrovací tabulka — nová informace → kam patří:**

| Co zapisuji | Kam |
|---|---|
| Dokončená práce | `changelog.md` (+ hotový bod z roadmapy KOMPLETNĚ do `roadmap-archive.md`) |
| Otevřená/plánovaná práce, backlog | `roadmap.md` (sekce dle fáze/tracku) |
| Zákaznický požadavek, stav tiketu | `customer-requirements-zastavkovy-jr.md` §0 |
| Architektonické rozhodnutí pro agendy (R1–R7 a jejich detaily) | `architektonicka-rozhodnuti-agendy.md` — **závazné, číst před novou fází** |
| Jak fungují elementy / tabulkové jádro / symboly JŘ | `elementy-architektura.md` |
| Kde jsou AD data (globály, nastavení linky, poznámky) | `ad-data-mapa.md` |
| Zjištění o legacy systémech (TiskMHD/TiskJR/RG), gap vs TD | `legacy-nahrada.md` |
| Breaking změna JSON šablony / migrace | `template-schema-versions.md` |
| Text pro zákazníky (novinky, návody) | wiki seedy: `plan-a-zmeny.md`, `uzivatelska-prirucka-*.md` |
| Pravidlo pro psaní kódu (per-glob) | `.claude/rules/*.md` |

**Nový soubor je oprávněný jen pro:** (a) jednorázovou analýzu, která se po vytěžení do celků výše
**smaže** (git historie stačí), (b) nový velký trvalý celek (≈ nová fáze/doména) — pak přidat řádek
do README indexu. Před založením se vždy zeptat: „nepatří to do existujícího celku?"

Nejčastěji potřebné reference: **`feature-design-principles.md`** (závazná mřížka před každým novým
requestem) · **`architektonicka-rozhodnuti-agendy.md`** (R1–R7) · `linkovy-knizni-jr-implementace-analyza.md`
(aktivní track C+D, §11–12 odložené body).
