# CLAUDE.md — Kontext pro AI agenta

## Co je tento projekt

**TemplateDesigner** je WPF desktopová aplikace pro tvorbu šablon tiskových sestav.
Náhrada staršího nástroje postaveného v InterSystems Caché/Globals.
Při navrhování nových featur, bez v potaz, že v budoucnu by mohl být nástroj, alespoň částečně, ovládán pomocí AI.Dobre

Systém: **1. Designer** (tento projekt) — vizuální návrh šablon → **2. Print Engine** (integrovaný) — šablona + data = PDF.

---

## ⚡ Aktuální stav projektu

> **📝 PRO AI AGENTA:** Průběžně aktualizuj. Dokončené → přesuň do "✅ Co máme". Nové problémy → "🐛 Známé problémy".

**Datum poslední aktualizace:** 2026-06-12

### 🎯 Aktuální focus

**Zastávkový JŘ z AD dat (agenda `ZastavkoveJrAd`) ✅ datově 1:1 + výkonově pročištěná** — ověřeno proti legacy gold oracle (`ALVA.TISKMHD.Data` VolanoZ=JRMAIN), viz `.claude/docs/cache-database-linky.md §10`. Hotové: core, FTL 0-3, omezení, dopravce (vč. adresy), CIS názvy, multi-page, **rozdíly tras** (1:1 OmTrasa), **atributy zastávek** (jen „na znamení", jako legacy), **atributy spoje** (bezbariér H/F), **časy jízdy ve StopListu** (jeden sloupec = gold TrasaMin1), + **performance** (memoizace per-linka invariantů + W-trasa cache, behavior-preserving byte-identický).

**Zbývá k úplnému 1:1 (zdokumentováno v §10):** multi-varianta TrasaMin (víc časových sloupců — chce zásah do C# rendereru), číslování omezení TYPPOZ=1 (firm-config), šířka/layout legendy (vizuál). **Výkon:** zbývá 1 hot spot v **MHD** agendě (`VypocitatBitmapyObsluhy` O(bodů³)) — rizikový, samostatný krok.

Předtím: **Caché Migration Wave 1 + 1.5 + 2 + 3 ✅** (per-user state + agenda registry + template storage). Pak **Fáze A.2-D zákaznické tickety** (TRANSDEV/KORID/DPKV suspended tickety).

### ✅ Co máme

- Text, Image, Rectangle, Line, BarcodeElement (QR, Code128, EAN-13, EAN-8, PDF417, DataMatrix), TableElement (Fixed/Dynamic)
- Parametry sestavy (Part 1 + serializace do JSON), VisibilityExpression evaluator
- Výrazový jazyk kompletní: podmínky (`&&`, `||`, `!`), formátování (`{POLE:N2}`, `{POLE:UPPER}`, `{POLE:LEFT(4)}`), agregace (`SUM`, `COUNT`, `AVG`, `MIN`, `MAX`) v souhrnných řádcích tabulky
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
- **Caché ZastavkoveJrAd** — agenda zastávkového JŘ z dat **hlavního JŘ modulu** (AD/JRMAIN, čte `^JRL`/`^JRZ`/`^JRSET`); čistá reimplementace legacy `ALVA.TISKMHD.Data` větve VolanoZ=JRMAIN (nevolá legacy MAC routiny). FTL 0-3 sloupcové layouty (`ZarazeniSpoje`/`NacistSkupinyDnu`), datumová omezení jede/nejede (`SestavTextOmezeni`, 1:1 TODDO^JRapiNZUtl), POPIS_DOPRAVCE z `^JRSET("DOP")` (vč. adresy provozovny), názvy zastávek + CIS pole, terminus-skip, multi-page (zastávka × směr). **Rozdíly tras** (`RozdilyTras`, 1:1 `OmTrasa^JRapiLinPrtMhd`: „neobsluhuje zast. X", marker od „B"). **Atributy zastávek** filtrované jen na „na znamení" (legacy UVOD tiskne jen `x`). **Atributy spoje** (bezbariér `inv`/`cbe` → „H"/„F", `BezbarierKonfig`). **Časy jízdy ve StopListu** (jeden sloupec = gold `TrasaMin1`, `WMapa`/`CasJizdyZMapy`). **Performance:** per-linka/per-směr invarianty + W-trasa cachovány do `^||TiskAd` (behavior-preserving). 1:1 ověřeno proti gold oracle na 0410101 (FTL0), 0425017 (FTL1), 0320144 (FTL3), 0320993 (FTL2), 0365001 (rozdíly tras + bezbariér + časy). Zbývá: multi-varianta TrasaMin (renderer), TYPPOZ=1 číslování, vizuál legendy. Detail: `.claude/docs/cache-database-linky.md §10`
- **DataContext vnořená pole** — ParseMockData rozlišuje JsonArray/JsonObject vs skalární hodnoty; ScheduleElementBase parsuje vnořená pole (ZONY[], ATRIBUTY[], ANOTACE[], POZNAMKY_ZASTAVKY[])
- **Multi-data PDF export + PrintDialog** — `RenderMultiDataToPdf` zpracovává více DataContextů (1 zastávka/směr = 1+ PDF stránek); PrintDialog s parametry a exportem reálných dat z Caché (`GetData`); tisk i mock preview v jednom dialogu
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
- **Smart snap resize** — snap hran při resize (edge + gap 4mm); sdílená hranice (tah za hranu mění i sousedního); split element Ctrl+Shift+H/J
- **Designer font cap** — omezení fontu na 10pt v designeru pro čitelnost placeholderů
- **Rámeček elementů (border)** — `BorderColor` + `BorderWidth` na base `TemplateElement`; dvouprůchodové renderování (obsah → rámečky mimo clip sekce); pixel-snap na sdílených hranách → jeden rámeček; viditelné i na hranici sekce
- **Auto-scale font u JŘ elementů** — `ScheduleElementBase.ComputeFontScale(naturalH)`: pokud obsah přesahuje pevnou Height, fonty + výšky řádků se proporcionálně zmenší; schedule elementy nyní podporují pevnou Height (ne jen null/dynamickou)
- **Multi-element editing** — Enter při výběru více elementů otevře dialog společných vlastností (BorderColor, BorderWidth, ZIndex, IsLocked); změna se aplikuje na všechny najednou
- **Ikonový toolbar** — 33 Fluent UI System Icons (Microsoft, MIT); 1 band, 7 skupin (Soubor, Upravit, Přidat, Elementy, Vrstva, Zarovnat, Zobrazení); ToolbarStyles.xaml; maximalizované okno; preview 50:50 star sizing
- **Inline properties panel** — záložka "Vlastnosti" v levém panelu; automatická aktualizace při výběru elementu; modální dialog zachován jako fallback (Enter/dvojklik)
- **TextStyle unifikace** — `TextStyle` (nullable POCO, CSS cascade přes `Resolve()`) + `ResolvedTextStyle` (struct se SkiaSharp helpery: `CreateFont`, `CreateTextPaint`, `CreateBgPaint`); migrace všech elementů (TextElement, TableElement, ScheduleElementBase + StopList/DepartureGrid/NotesLegend); `BackgroundColor` na base `TemplateElement`; `CopyBaseTo()` eliminuje Clone() boilerplate; breaking JSON change (flat text props → composable `style`/`headerStyle`/`dataStyle`); `Strikethrough` property; `DrawTextDecorations()` pro Underline + Strikethrough (SkiaSharp 3 nevykresluje automaticky)
- **Proporcionální škálování šablony** — `ScaleTemplateCommand`: škáluje PageSize, margins, sekce (výšky), elementy (pozice, rozměry, mm/pt properties); `ApplyScale(float)` virtual metoda na všech elementech; dropdown v toolbaru (50%–200%); `LineSpacing` se neškáluje (bezrozměrný násobitel); undo přes inverzní škálu (1/scale)
- **Redistribuce výšek sekcí** — při smazání sekce se uvolněná výška proporcionálně rozdělí mezi zbývající; při přidání sekce se stávající proporcionálně zmenší; `AddSectionCommand` pro undo/redo
- **Výchozí nastavení nových elementů** — TextElement bez placeholder textu, zarovnání Center/Middle; všechny elementy: černý rámeček 0.1mm (jen nové — model defaults zůstávají pro zpětnou kompatibilitu JSON)
- **DepartureGrid: HourColumnPerGroup** — `HourColumnPerGroup=true` zobrazí sloupec hodin u každé skupiny dnů (nejen vlevo)
- **AI-enhanced PDF import** — dvoustupňový pipeline: heuristiky (geometrie) → volitelný AI enrichment (sémantika); `AiImportEnricher` s kontextovými prompty per `ScheduleImportType`; `EnrichedImportResult` (TextMappings s confidence, DayGroupValidations, DetectionValidation); sdílená `AnthropicApiClient.CallApiAsync()` (claude-haiku-4-5); auto-apply DB polí při confidence >= 0.7; graceful fallback při chybějícím API klíči nebo selhání AI; UI checkbox + agenda dropdown v `PdfImportDialog`
- **AI Vision import pipeline** — `AiVisionImportService` s composable `AgendaPromptContext` prompt systémem (extensibilní per agenda); `AiGuidedScheduleBuilder` (PDF → rasterizace → Claude Vision → rozpoznání JŘ struktury → přesné elementy z PdfPig geometrie); `AiVisionImportPipeline` orchestruje celý flow s fallback na heuristiku; konfigurovatelný model (`AppSettings.AiAssistant.VisionModel`); typ parametrů z `AgendaAPI.GetParameters()` s value-based fallback; font family z PdfPig
- **DepartureGrid: per-column font wrapping** — `ComputeLayout` respektuje per-column header font pro wrapping i min width měření; dynamická výška záhlaví z max per-column line height; `GroupColumnIndexResolver` — generický `BuildLookup<T>` s `IndexKeyPrefix` konstantou pro resolve `__INDEX_` klíčů
- **Skrytí záhlaví sekcí** — `DesignerState.ShowSectionHeaders` toggle; auto-detekce JŘ šablon (skryje záhlaví automaticky); toolbar menu položka
- **CellStyleVM font defaults** — dialog sloupců zobrazuje skutečné font hodnoty z elementu (ne prázdná pole); `headerDefaults`/`dataDefaults` parametry
- **AnthropicApiClient: shared HttpClient** — statický `HttpClient` s per-request headers místo per-call instancí
- **MmMath helpers** — `MmMath.Round(mm)` zaokrouhlí na 0.1mm (eliminuje IEEE-754 artefakty); `MmMath.Snap(mm, step)` přichytí na libovolný krok; nullable overload pro `Height`; aplikováno v EndResize, ScaleTemplate, DistributeH/V, PropertiesPanel
- **Normalizovat pozice** (`Ctrl+Shift+N`) — dialog s volbou kroku (0.1–5mm), live preview počtu změn, `CompositeCommand` undo; opraví float artefakty v existujících šablonách
- **Grid size v UI** — ComboBox 1/2/5/10mm v toolbaru; `DesignerState.GridSize` bindovatelná property (výchozí 5mm)
- **BarcodeElement rozšíření** — EAN-13, EAN-8, PDF417, DataMatrix; `static readonly` hints/writer/dashEffect; paleta konsolidována na 1 tlačítko "Kód"
- **TextStyle Strikethrough** — `Strikethrough` property + `DrawTextDecorations()`; oprava tichého nefungování Underline v SkiaSharp 3
- **Jednotná kaskáda stylů buněk** — 4-vrstvá CSS-like kaskáda `column.Style → element.HeaderStyle/DataStyle → table/schedule defaults → CellDefaults` jednotně ve StopList, DepartureGrid, TableElement; `Rendering/CellDefaults.cs` (autoritativní defaults), `Rendering/CellRenderer.cs` (jediné místo pozicování textu buňky: bg → padding → HAlign+VAlign → decorations), `Rendering/ColumnRenderer.cs` (orchestruje řádek sloupců), `Rendering/CellStyleEditOptions.cs` (per-kontextové skrývání UI); `TableColumn.DataStyle` + `TableElement.DataStyle` (per-column + element-level override datových buněk); `col.Alignment` legacy fallback zachován; TableElement nyní respektuje HAlign/VAlign/Padding/Underline/Strikethrough (dříve hardcoded baseline+4px offset)
- **Sdílený CellStyleEditor** — `Controls/CellStyleEditor.xaml` UserControl konzumovaný StopColumnsEditorDialog, DepartureGroupEditorDialog, TableEditorDialog; `CellStyleVM` rozšířen o `VAlignmentIndex`, `Underline`, `Strikethrough`, `LineSpacing`; per-dialog omezení přes `CellStyleEditOptions` (HeaderEditOptions/DataEditOptions na VM); `TableEditorDialog` má nový tab „Styl sloupců"
- **Tri-state editor stylů + placeholdery zděděných hodnot** — `CellStyleVM` všechny vlastnosti nullable (null = dědit); `Inherited: ResolvedTextStyle` z elementu (přes public `Resolve<Zone>Parent()`); XAML zobrazuje zděděné hodnoty jako šedivý italic placeholder v prázdných polích; `EffectiveBold/Italic/Underline/Strikethrough` derived pro 2-stavové checkboxy; „×" reset tlačítko viditelné jen při explicit override; `ToModel()` emituje nullable TextStyle (idempotentní open+OK beze změn); `CellStyleEditor.Converters.cs` (NullToVis, NotNullToVis, FirstNonEmptyHexToBrush, HAlign/VAlignToLabel)
- **Click-vs-drag threshold** — pouhý klik na element (selekce, otevření properties) nespustí soft-snap; element se začne hýbat až po překročení 4px myší. `_dragHasMoved` flag v `MainWindow.xaml.cs`; `EndDrag` necommituje, pokud myš nepřekročila threshold. Reason: před fixem klik při GridSize=5mm „strhnul" 0.1mm pozici elementu na 5mm grid
- **Per-template editor preferences** — `Models/TemplateEditorPreferences.cs` (nullable `GridSize`/`SnapToGrid`/`SnapToElements`/`ShowGrid`); `Template.EditorPreferences` property (vedle `Parameters`, `SampleData`, `CultureCode`); apply on Open / capture on Save → designové prostředí (jemný grid pro precizní šablonu) přežije reload. `RequestGridSizeUiSync` callback synchronizuje nebindovaný `CmbGridSize` ComboBox
- **Sjednocené Header/Data zóny v panelu** — princip „rychlý přístup × cascade": 4 base barvy (Header bg/text + Data bg/text) v properties panelu napříč všemi tabulkovými elementy (StopList/DepartureGrid/NotesLegend/TableElement); detailní cascade (font/align/padding/per-column overridy) zůstává v dialogu sloupců. Helper `ScheduleElementBase.GetCellZoneBaseColorDescriptors()`; TableElement parallel implementation (jiná hierarchie). Skupiny „Záhlaví"/„Datové buňky" konzistentně. Round-trip safe (panel↔dialog editují totéž pole)
- **Caché Template Storage** — třída `ALVA.TemplateDesigner.API.Sablony` s 5 metodami (`ListTemplates`, `LoadTemplate`, `SaveTemplate`, `DeleteTemplate`, `RenameTemplate`); per-IDFa storage v `^ALVA.TemplateDesigner.Templates(IdFa,"DTA",TemplateID,"Metadata"|"Data")` + index `ByAgenda`; JSON wire formát; metadata Name/AgendaCode/Author/CreatedAt/ModifiedAt/Version. .NET strana: `CacheTemplateService` (async wrapper přes Task.Run + `CacheTemplateException`), `CacheTemplatePickerDialog` (správce: list + filtr agendy + search + Open/Rename/Delete), `SaveToCacheDialog`. Menu „Soubor → Otevřít z Caché... / Uložit do Caché [jako...]" + „Nástroje → Šablony v Caché..." (dva entry pointy, sdílí dialog). `_loadedFromCache` flag + SaveCommand routing (Caché → save zpět; disk → file; nová → SaveAs)
- **Caché namespace struktura** — separace **podle role třídy**: `ALVA.TemplateDesigner.API.*` (obecná business logika TD, např. `Sablony` CRUD), `ALVA.TemplateDesigner.Agendy.*` (agenda implementations: `BaseAgenda` abstract + `ZastavkoveJrMhd`, `Faktury`), `ALVA.TemplateDesigner.EDList.*` (dispatch tridy pro EDList komponentu: `Sablony`, `Instance`). Folder/namespace 1:1 mapuje na roli, doménový název může opakovat napříč rolemi (`API.Sablony` vs `EDList.Sablony` jsou jasně rozlišené). Budoucí komponenty: `EDInputForm.*`, `EDInputGrid.*` (Wave 4+).
- **GENERAL agenda fallback** — `cacheClass = ""` → šablona bez DB vazby (statický obsah); CacheAgendaProvider short-circuit ve všech metodách; BuildAllPreviewDataPages vrací `[new DataContext()]` místo `[]` → PDF/PNG export funguje i bez agendy. Po Wave 2 W2-1 je GENERAL syntheticky injektován v `CacheAgendaRegistry` (auto-discovery ji nenajde — nemá Caché class).
- **Wave 2 W2-1 — Agenda registry v Caché** — auto-discovery z `BaseAgenda` subclasses. `BaseAgenda` deklaruje class parametry `AgendaCode` + `AgendaLabel`; konkrétní agendy (`ZastavkoveJrMhd`, `Faktury`) je override. Caché `ALVA.TemplateDesigner.API.AgendyRegistr.ListAgendy` přes `%Dictionary` SQL najde subclasses + jejich metadata → JSON pole. Eliminuje `appsettings.json agendas[]` (smazáno). Konvence agenda code: `<TYP_SESTAVY>_<DATOVY_ZDROJ>` (např. `ZASTAVKOVY_JR_MHD`, future `KNIZNI_JR_MHD`, `MEZIMESTSKY_JR_MHD`). .NET: `IAgendaRegistry` + `CacheAgendaRegistry` + `AgendaRegistryEntry` POCO; App.OnStartup sync load s 3s timeoutem, GENERAL agenda syntheticky prepended. Per-customer kontrola (AgendaEnabled, per-IDfa override) odložena — YAGNI.
- **Přepínač agendy** — `ChangeAgendaDialog` (list agend s „(aktuální)" badge); 3 entry pointy: menu „Nástroje → Změnit agendu...", **Ctrl+Shift+A**, klik na agenda chip ve status baru. Přepnutí vytváří **novou prázdnou šablonu** se zvolenou agendou (po Save/Discard/Cancel) — sdílený `ReplaceWithBlankTemplate(agendaCode)` helper s NewTemplate. Permanentní viditelnost agendy na 3 místech: status bar (zelený klikatelný chip), window title prefix `[MHD]`, sidebar „Pole agendy" header
- **Wave 1 + 1.5 — per-user preferences přes Caché** — `Services/TdPreferencesService.cs` (typed wrapper nad `IUserPrefsService`); ukládá per (IdFa × user) do `^ALVA.EDControls.UserData`. 5 blob konzumentů: `MainStatePref` (zoom, grid, snap, splittery, tab index, preview pane viditelnost — Typ 1), `RecentFiles` (cap 10, dedup — Typ 2), `RecentColors` (cap 8 — Typ 2), per-template `InstanceDefaults` (key = template Guid — Typ 3), per-agenda `PrintParams` (key = AgendaCode — Typ 3 per agenda, ne per template — params jsou agenda-level concerns). App.OnStartup async `LoadAsync` + 200ms wait + event fallback; MainWindow.Closing fire-and-forget save; Recent Files submenu dynamicky populated; instance defaults auto-load na Open template, save na InstancePicker OK; PrintDialog operuje nad sdíleným dict s preview lištou (live cross-notify); ColorPickerHelper integrace recent colors (DRY — odstraněn duplicate WinForms ColorDialog kód z PropertiesPanel). **Žádné lokální JSON soubory** — per-machine pattern v TD nepoužíváme (běží embedded z EDISONMAIN2). Registr scope ownerships + 3-typová taxonomie state viz `EDControls/DOCS/05-userdata-scopes.md`
- **Instance Picker (multi-výběr linek)** — `InstancePickerDialog` se search boxem (Enter), checkboxy multi-select, Vybrat vše / Odznačit vše. Caché `AgendaAPI.GetAvailableInstances` (abstract default empty) + impl v `ZastavkoveJrMhdAPI` iteruje `^ALVA.MHD.LSO(IdFa,1,"IDX",1,1,1,SkupinaLinekID,LinkaID)` — pouze linky aktivní skupiny linek (default první nezblokovaná z ENUMS, optional IN parametr `SKUPINA_LINEK_ID` pro Edison-driven kontext); TrasaID filter pro orphan linky; case-insensitive substring search server-side; limit 50. POCO `AgendaInstance(Value, Display, Subtitle?)`. `ActiveInstances` state v VM (mimo Template — session scope), `ActiveInstanceIndex`, `CurrentActiveInstance`. Status bar 2. modrý chip „Instance: X — Y (N/M)" s ◀▶ tlačítky; klik otevře picker. `RefreshDataForCurrentInstanceAsync` async GetData při změně instance, status feedback. Multi-instance batch v `BuildAllPreviewDataPages` (PDF/PNG) i `PrintDialog buildRealData` — 600 stránek za ~9s (5s fetch + 3.6s render, ~15ms/stránka, top decile vs commercial reporting). Reset instancí při změně agendy

### 🐛 Známé problémy

*(žádné aktivní)*

---

## Technologický stack

| Technologie | Použití |
|---|---|
| .NET 9, WPF | UI framework |
| SkiaSharp 3.119.2 | Renderování canvasu i PDF |
| SkiaSharp.Views.WPF 3.119.2 | SKElement WPF kontrola |
| System.Text.Json | Serializace šablon |
| EDControls (ProjectReference) | EDViewModel, RelayCommand |
| ZXing.Net 0.16.9 | BarcodeElement |
| PdfPig 0.1.9 | PDF import (extrakce textu, čar, obrázků) |

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

---

## Publish — distribuce build .exe

Single-file `.exe` se generuje skriptem `publish.ps1` v rootu projektu:

```powershell
cd C:\Users\miesb\source\EDISONMAIN2\TemplateDesigner
.\publish.ps1
```

Skript volá `dotnet publish` s profilem `SingleFileCEdison` → výstup do `C:\Edison\TemplateDesigner.exe` (~30 MB, self-extracting bundle s `templatedesigner.appsettings.json` jako embedded resource).

**Před spuštěním** — zavři běžící TemplateDesigner.exe (skript hned na začátku ověří lock a friendly errne, pokud je soubor uzamčený).

**Pozn.:** Publish přes Visual Studio GUI je občas rozbitý (silent skip GenerateSingleFileBundle, build prošel ale single-file se nevyrobil). CLI variantou tohoto skriptu se to obchází.

---

## Detailní dokumentace (`.claude/rules/`)

Soubory se načítají automaticky při práci na relevantních souborech.

| Soubor | Obsah | Načítá se pro |
|---|---|---|
| `architecture.md` | Struktura projektu, zodpovědnosti, MVVM hranice, souřadnice, DesignerState | `dotnet/**` |
| `data-model.md` | C# datové třídy, JSON formáty, renderovací pipeline, interakční model | `dotnet/Models/**` |
| `mvvm-patterns.md` | PropertyDescriptor, TableElement architektura, jak přidat element/okno | `dotnet/Windows/**` |
| `cache-api.md` | AgendaAPI architektura, wire formáty, Caché skeleton, nová agenda checklist | `CACHE/**` |
| `cache-database.md` | Mentální model DB: globály (LSO, ZST, CHR, ENUMS, SETTINGS), subscripty, business koncepty | `CACHE/**` |
| `implementation-patterns.md` | Barvy, RenderContext, ComboBox gotcha, SkiaSharp quirks | `dotnet/**/*.cs` |
| `ux-conventions.md` | Dialogová tlačítka (IsDefault/IsCancel), tooltipy, autoritativní tabulka keyboardových zkratek, context menu, confirmation patterns | `dotnet/Windows/**` |

Ruční reference:
- `.claude/docs/changelog.md` — kompletní changelog
- `.claude/docs/roadmap.md` — aktuální roadmapa (jen otevřené body); hotová práce → `.claude/docs/roadmap-archive.md`
- `.claude/docs/schedule-elements-architecture.md` — architektura specializovaných JŘ elementů (Caché wire formát, hierarchie elementů, matice zodpovědností)
- **`.claude/docs/feature-design-principles.md`** — **závazná reference před každým novým requestem**: kódová + UX rozhodovací mřížka, klasifikace typu variace (1/2/3), per-customer divergence, anti-patterns
- `.claude/docs/customer-requirements-zastavkovy-jr.md` — konkrétní požadavky zákazníků z helpdesku Podpory M-line (zastávkový JŘ — DPKV, TRANSDEV, KORID)
