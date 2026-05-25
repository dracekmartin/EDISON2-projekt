# 🗄️ Archiv — hotové kroky

> Vše pod touto čarou je už implementováno. Horní část roadmap obsahuje jen to, co je potřeba dělat.

## Wave 2 — Shared catalogs ✅ (2026-05-20)

**Scope** po diskusi zúžen ze 4 položek na 1 implementovanou + 3 skipnuté:

| # | Položka | Stav | Důvod |
|---|---|---|---|
| W2-1 | Agenda registry → Caché | ✅ Hotovo | Auto-discovery z `BaseAgenda` subclasses (commit `de0f872`) |
| W2-2 | Section templates → Caché | ⛔ Skipnuto | Funkčně překryto Wave 3 Template starters (Open + Save As pattern). Bez user requestu. Existující file-based zůstává jako side-feature. |
| W2-3 | Element defaults → Caché | ⛔ Skipnuto | Hardcoded v C# je rozumné; Wave 4 InputGrid persist do Template JSON pokrývá use case |
| W2-4 | Company palette → Caché | ⛔ Skipnuto | Wave 1.5 RecentColors pokrývá; brand palette není user request |

### W2-1 Agenda registry — auto-discovery design

**Princip:** „Třída JE registrace". Eliminuje drift mezi registrem a impl (vs JSON config list).

**Caché:**
- `BaseAgenda.cls` — 2 class parametry (`AgendaCode`, `AgendaLabel`); konvence `<TYP_SESTAVY>_<DATOVY_ZDROJ>`
- `ZastavkoveJrMhd.cls` — `AgendaCode = "ZASTAVKOVY_JR_MHD"` (přejmenováno z `"MHD"` — MHD je datový modul, ne typ sestavy)
- `Faktury.cls` — `AgendaCode = "FAKTURY"`
- NEW `API/AgendyRegistr.cls` — `ListAgendy` přes `%Dictionary` SQL nad subclasses BaseAgenda, vrátí JSON pole `[{code, label, cacheClass}, ...]`

**.NET:**
- NEW `Serialization/IAgendaRegistry.cs` — interface + `AgendaRegistryEntry` record
- NEW `Serialization/CacheAgendaRegistry.cs` — impl s Task.Run wrap (R1), synteticky prepend GENERAL agendy, fallback při chybě jen GENERAL
- `Config/CacheSchemaWiring.cs` — `TryCreate(MLRest, IReadOnlyList<AgendaRegistryEntry>)`
- `Config/AppSettings.cs` — odstraněna `Agendas` property + `SchemaAgendaSettings` třída
- `App.xaml.cs` — sync load agendy ze Caché s 3s timeoutem
- `MainWindow(MLRest?, prefs?, agendas?)` — 3. ctor param
- `templatedesigner.appsettings.json` — smazáno `agendas[]`

**Architektonická rozhodnutí:**
- Per-customer kontrola odložena (YAGNI — všichni zákazníci mají stejný kód)
- AgendaOrder parameter neřešen — alfabeticky podle code
- AgendaEnabled neřešeno — pre-release work řešen git branches
- Naming `<TYP_SESTAVY>_<DATOVY_ZDROJ>` — budoucí JŘ: `KNIZNI_JR_MHD`, `MEZIMESTSKY_JR_MHD`, `VOZOVY_JR_MHD`

**Commits:** `7d6704a` (docs scope reduction), `de0f872` (impl).

---

## Wave 1 + 1.5 — Per-user state přes Caché ✅ (2026-05-20)

První dvě dávky Caché Migration — persistence per-user × IDFa state v `^ALVA.EDControls.UserData` přes `IUserPrefsService`. Žádné lokální JSON soubory (TD běží embedded z EDISONMAIN2). Detail dokumentace, registr scope ownerships a 3-typová taxonomie state v [`EDControls/DOCS/05-userdata-scopes.md`](../../../EDControls/DOCS/05-userdata-scopes.md).

**Wave 1 (foundation + UX integrace):**

| Krok | Co | Commit |
|---|---|---|
| W1-0 | `IUserPrefsService` foundation + Caché `ALVA.EDControls.UserData` (generic blob storage per IdFa × userID × scope × key) | F1 (2026-05-15) |
| W1-1 | `TdPreferencesService` wrapper + `MainStatePref` typed record (Typ 1 — UI state, jeden blob) | 7cd39ab |
| W1-2 | Wire-up: `App.xaml.cs` async load + `TdPreferencesService.Loaded` event fallback pro cold-start | 7cd39ab |
| W1-3 | `MainWindowViewModel.ApplyLoadedPreferences` — Zoom/Grid/Snap/SnapGapMm/ShowGrid/ShowSectionHeaders/ShowRuler | 7cd39ab |
| W1-3b | UI-side prefs — splittery (LeftPanelWidthPx, PreviewPanelWidthPx), ActiveLeftTabIndex, ShowPreviewPane | 7cd39ab |
| W1-4 | Recent Files menu (Soubor → Otevřít poslední) + AddRecentFile při Open/SaveAs, missing-file graceful (Typ 2 — activity log) | 7cd39ab |
| W1-6 | Instance defaults per šablona (scope `"TemplateDesigner.InstanceDefaults"`, key = template Guid, Typ 3 — per entity) | 7cd39ab |

**Wave 1.5 (Recent Colors + PrintParams):**

| Krok | Co | Commit |
|---|---|---|
| W1.5-1 | Recent Colors — 8 last-used barev v color pickeru (`"TemplateDesigner"` / `"RecentColors"`, Typ 2). DRY refactor `ColorPickerHelper` jako single entry point — odstranění duplicitního WinForms ColorDialog kódu z `PropertiesPanel` | 06eeda2 |
| W1.5-2 | PrintDialog params per-agenda persist + live sync s preview lištou (`"TemplateDesigner.PrintParams"` / `<agendaCode>`, Typ 3 per agenda). Sdílený dict + cross-notification callback. Crash fix (odstranění sync `.Result` blocking UI thread) + re-entrance guard pro rychlé Ctrl+P. | 06eeda2 |
| W1.5-3 | PropertiesPanel group expanded state | ⛔ Odloženo do Wave 4 (EDInputForm migrace nahradí PropertiesPanel) |
| W1.5-4 | Font override (analogie EDISONMAIN2 LocalPrefs.FontFamily/FontSize) | ⛔ Odloženo (žádný user request) |

**Doc commity:**

| Commit | Obsah |
|---|---|
| ec654e4 | `EDControls/DOCS/05-userdata-scopes.md` — registr UserData scopes |
| ced96fa | 3 typy state taxonomy + Wave 1.5 priorities |

**Klíčová architektonická rozhodnutí:**
- **Žádné lokální JSON soubory** pro persistent state (TD běží embedded z EDISONMAIN2)
- **Async-first pattern** — žádné sync `.Result` block na UI threadu (poučení z crash fixu)
- **Per-agenda granularita pro PrintParams** — nová šablona stejné agendy zdědí poslední hodnoty
- **Sdílený dict + cross-notification** — single source of truth pro params mezi PreviewLišta a PrintDialog
- **3-typová taxonomie state** dokumentována jako závazná pro budoucí konzumenty (Typ 1 UI state, Typ 2 activity log, Typ 3 per entity)
- **Schema evolution backward-compat** — všechna pole records mají defaults v primary ctoru
- **DRY ColorPickerHelper** — jediný entry point pro color picker napříč TD

---

## v1.0 demo balíček ✅ (2026-05-15)

Tři commity tvoří kompletní release readiness pro testovací nasazení — zákazníkovi lze ukázat funkční end-to-end workflow pro zastávkový JŘ MHD. Detaily viz [`changelog.md`](changelog.md) 2026-05-15 entry.

| # | Co | Commit |
|---|---|---|
| R1 | **Agenda + Linka picker UX** — `ChangeAgendaDialog` se 3 entry pointy (menu / Ctrl+Shift+A / klik na status chip); `InstancePickerDialog` (multi-výběr linek, search, batch); Caché `GetAvailableInstances` iteruje skupinu linek; permanentní status indikátor (zelený agenda chip + modrý instance chip s ◀▶ + window title `[MHD]` prefix) | `82b68be` + `fd25008` |
| R2 | **Template storage v Caché** — třída `ALVA.TemplateDesigner.API.Templates` (5 metod, per-IDFa, JSON wire); `CacheTemplateService` async wrapper; `CacheTemplatePickerDialog` (správce: list + filtr + search + Open/Rename/Delete); `SaveToCacheDialog`; menu „Soubor → Otevřít z Caché... / Uložit do Caché [jako...]" + „Nástroje → Šablony v Caché..."; Caché namespace refactor `API/Agendas/Forms`; GENERAL agenda fallback | `5ac9dd0` |
| R4 | **Polish** — status bar chips, ConfirmUnsavedChanges integrace, error reporting přes IPopupDisplayer, BuildAllPreviewDataPages bugfix pro šablony bez agendy, debug logging do `^jm` (po debugu odstraněno) | průběžně |

**Performance:** 600 stránek MHD JŘ za ~9s (5.2s fetch + 3.6s render, ~15ms/stránka) — top decile vs commercial reporting (Crystal Reports 30-60s, SSRS 20-45s, JasperReports 15-30s).

**Architektonická pravidla R1–R5 (async/IPopupDisplayer/JSON/MVVM/versioning) dodržena ve všech 3 commitech.**

## Fáze A.2: Zákaznické úpravy zastávkového JŘ ✅ (2026-05-11 → 2026-05-12)

Suspended tickety od DPKV (#10660) a TRANSDEV (#7937, #7959, #9036) z helpdesku. Detail viz [`customer-requirements-zastavkovy-jr.md §G`](customer-requirements-zastavkovy-jr.md).

| Krok | Tiket | Co | Datum |
|---|---|---|---|
| A2-A1 | #10660 | StopList: `DirectionArrowPosition` (Left/Right asymetricky) | 2026-05-12 |
| A2-A2 | #10660 | StopList: `MarkersPosition` (Before/After name) + auto-fit marker sub-cell | 2026-05-12 |
| A2-A3 | #10660 | StopList: `CurrentStopBgColor` + odstranění master toggle `HighlightCurrentStop` | 2026-05-12 |
| A2-A4 | #10660 | StopList: per-column header font (verifikace, již plně podporováno) | 2026-05-12 |
| A2-B1 | #10660 | DepartureGrid: per-line font v dvouřádkovém headeru (`HeaderLine2Style`) | 2026-05-12 |
| A2-B2 | #10660 | DepartureGrid: fix nestejného podtržení (`DrawTextDecorations` na header) | 2026-05-12 |
| A2-B3 | #10660 + #9036 | DepartureGrid: časová osa přes půlnoc + extended hours + „Roztáhnout na výšku" | 2026-05-12 |
| A2-C1 | #10660 | NotesLegend: DB-driven barvy poznámek (`PoznamkaSpoj.BarvaZnakuRGB/BarvaPozadiRGB`) | 2026-05-12 |
| A2-C2 | #10660 | NotesLegend: konfigurovatelná hlavička (verifikace, již podporováno) | 2026-05-12 |
| A2-E1 | #8772 | VisibilityExpression placeholdery — verifikace funkčnosti | 2026-05-11 |
| A2-A5 | #7937 | ⏸️ StopList per-row indent — odloženo (chybí Caché data flow, mimo TD scope) | — |
| A2-A6 | #7959 | 🔍 Filtr neobsluhovaných zastávek — již implementováno v Caché, vyžaduje verifikaci | — |
| A2-B4 | #8828 | ⏸️ Per-sloupek zastávky — odloženo (Caché datový průzkum + business decision) | — |

## Fáze B0.5 — Code Hygiene Sprint

| # | Co | Hotovo |
|---|---|---|
| H-1 | Smazat `ShowZkraceneOznaceniLinky` — ghost property v `DepartureGridElement` (nikde se nečetla) | 2026-04-21 |
| H-2 | `async void` → `async Task` + `AsyncRelayCommand` (EDControls) + `CommandAsync` helper; Open/Save/SaveAs + bonus GenerateCommand | 2026-04-21 |
| H-3 | `ElementTypes` statické konstanty v `Models/ElementTypes.cs` — nahrazeno ~30 magic strings v JsonDerivedType, `CreateElementByType`, `GetDefaultElementSize` a toolbar menu | 2026-04-21 |
| H-4 | JSON wire format verify — průzkum potvrdil 100% čistý JSON (Caché `obj.%ToJSON()` → .NET `JsonNode.Parse`); opraven zastaralý docstring `GetData` v `AgendaAPI.cls` + `cache-api.md` + přidána sekce "Wire formát pravidlo (R3)" | 2026-04-21 |
| H-5 | Silent catch v `PropertiesPanel.xaml.cs` color picker seed nahrazen za `SKColor.TryParse` — odstranil try/catch jako control flow | 2026-04-21 |
| H-6 | Nový `.claude/rules/new-element-checklist.md` — 10-kroková autoritativní reference (třída, ElementTypes, JsonDerivedType, 2 switches, toolbar menu, paleta, dynamická výška, GetSummary, docs); konsolidace z `mvvm-patterns.md` | 2026-04-21 |
| H-7 | `MessageBox.Show` → `IPopupDisplayer` — 5 VM (AiTemplateAssistant, TableEditor, ElementProperties, MainWindow, PdfImport); PdfImport odstranil 3 callback properties; YesNoCancel kept s TODO (IPopupDisplayer nemá ekvivalent); Warning → Info (WPF rozdíl byl jen ikona) | 2026-04-21 |
| H-8 | Extract `Controls/TemplateTreePanel/` — ~150 řádků ze `MainWindow.xaml.cs` přesunuto do samostatného UserControlu; `MainWindow` jen volá `TreePanel.Rebuild()`; smazán orphan `Windows/TemplateTreeDialog/` (nikdy se neinstanciovalo) | 2026-04-21 |
| H-9 | Extract `Windows/SaveSectionTemplateDialog/` — inline WPF konstrukce dialogu (~60 řádků StackPanel+TextBox+Button) z `MainWindowViewModel` přesunuta do Window+ViewModel+XAML dle závazného standardu; `SaveSectionTemplate(Section, string)` nyní čistá save logika | 2026-04-21 |
| H-10 | **N/A** — `TemplatePickerDialog` v kódu neexistoval. Template picker = `OpenFileDialog` (standardní Windows), section template picker = menu položky v "Add Section" dropdown. Žádný samostatný dialog k extrakci. Uzavřeno bez změn. | 2026-04-21 |
| H-11 | ⏭️ **Skip do potřeby** — `PropertyDescriptor.VisibleWhen` predikát. Aktuální ad-hoc skrývání v dialozích stačí, žádný blocker. | 2026-04-30 |
| H-12 | ⏭️ **Skip do potřeby** — `SharedPropertyDescriptors` helper. Duplikace deskriptorů akceptovatelná za stávající cenu. | 2026-04-30 |
| H-13 | ⏭️ **Skip do potřeby** — rozšíření error flow nad rámec `IPopupDisplayer`. Strukturované logování / telemetrie přidat až bude konkrétní požadavek. | 2026-04-30 |

## Sjednocení patternů + UX vyladění ✅ (2026-04-30 → 2026-05-01)

Kontinuální polish nad rámec původního B0.5 scope, motivovaný přípravou na 1:1 ladění zastávkového JŘ:

| Co | Stručný popis |
|---|---|
| **Sjednocené Header/Data zóny v panelu** | Princip „rychlý přístup × kompletní cascade" napříč StopList/DepartureGrid/NotesLegend/TableElement; helper `GetCellZoneBaseColorDescriptors()` |
| **Per-template editor preferences** | `Template.EditorPreferences` (grid + snap) — designové prostředí přežije reload |
| **Click-vs-drag threshold** | Klik na element nespustí soft-snap (4px threshold) |
| **ImageElement ScaleMode** | Renderer honoruje Fit/Fill/Stretch/None + tlačítko „Přizpůsobit poměru obrázku" |
| **Sub-pixelové rámečky** | Adaptive `RenderBorder` — pod 1px AA fractional, nad 1px pixel-snap |
| **Descriptor sort by group** | Stable sort v PropertiesPanel — bez duplicitních hlaviček |
| **Border v „Obecné" group** | Konzistentní rychlý přístup k základním vlastnostem |
| **DataRowHeight hoist** | Z duplicitních deklarací do `ScheduleElementBase` |
| **Úklid mrtvého UI** | Smazán `LineSpacing` z `CellStyleEditor` (CellRenderer ho ignoroval) |
| **Dokumentace stylingových patternů** | `mvvm-patterns.md` — Pattern 1 (single-zone) vs Pattern 2 (dual-zone) |

## Specializované JŘ elementy — společný základ ✅ (2026-03-18)

| Krok | Co |
|---|---|
| 1 | `ScheduleElementBase` — abstraktní základ (indexy, grouping, společné properties) |
| 2 | `StopListElement` + `DepartureGridElement` + `NotesLegendElement` — zastávkový JŘ (MHD) |
| 3 | Caché `ZastavkoveJrMhdAPI` — `GetData`/`GetMockData` + wire formát (ZASTAVKY, SKUPINY_DNU, ODJEZDY, POZNAMKY) |
| 4 | `SkiaHeightCalculator` — měření výšky JŘ elementů |
| 5 | `PrintDialog` — parametry + export reálných dat z Caché (`GetData`) + multi-data PDF |
| 6 | `CacheAgendaProvider` — ParseMockData vnořená pole, GetData, BuildRealData |

## Fáze A: Odladění MHD zastávkového JŘ ✅ (2026-05-11)

Cíl: hotová první kompletní verze šablony zastávkového JŘ a její review s kolegy.

> **Detailní architektonický dokument:** [`schedule-elements-architecture.md`](schedule-elements-architecture.md)

| Krok | Co | Stav |
|---|---|---|
| A | Hotová 1:1 šablona zastávkového JŘ + review s kolegy → seznam úprav v `customer-requirements-zastavkovy-jr.md` (= Fáze A.2) | ✅ (2026-05-11) |

### Poučení z implementace zastávkového JŘ — co vzít v potaz pro další JŘ typy

| Téma | Popis |
|---|---|
| **Jeden element → tři elementy** | Původní plán počítal s jedním `StopScheduleElement`. Ve skutečnosti se rozpadl na 3 nezávislé elementy — lépe odpovídá filozofii designeru (uživatel skládá ze stavebních bloků). Počítat s tím od začátku u dalších JŘ typů. |
| **Synchronní GetData** | `GetData` běží synchronně na UI thread — pro malé linky OK, pro velké může zamrznout UI. Vozový JŘ bude mít víc dat → async loading s progress barem bude nutný. Viz pravidlo R1. |
| **SKTypeface/SKPaint alokace** | ✅ Opraveno — `ScheduleElementBase` cachuje `SKTypeface` přes statický `ConcurrentDictionary<string, SKTypeface>`; `SKPaint` se vytváří a ukládá per instance cache. |
| **Caché business logika je rozsáhlá** | ZastavkoveJrMhdAPI má ~1200 řádků. Počítat s tím v odhadu pro další JŘ typy. |
| **Definovat layout před implementací** | Aby se neopakovala situace, kde se plán změní v průběhu. |

### Fáze A — hotové kroky

| Krok | Co | Stav |
|---|---|---|
| 7 | Per-column styling framework — CellStyle/ColumnVisualStyle, dva taby v DepartureGroupEditorDialog, InlineTextStyle pro minuty/anotace | ✅ (2026-03-21) |
| 7a | StopListElement — per-column styling analogicky k DepartureGrid (CellStyle pro záhlaví/data sloupců zastávek) + generalizace shared helpers do ScheduleElementBase + ColorPickerHelper + ActionId routing | ✅ (2026-03-23) |
| 7b | Ověření parametrů šablony — funguje přepsání element properties z template parameters (CASOVA_OSA, HODINY_OD, HODINY_DO, TISK_PROJETYCH_ZAST, TISK_SIPKY); fix merge parametrů do real data exportu; auto-open PDF; odebrání BAREVNE_ODLISENI + TISK_TARIFU | ✅ (2026-03-23) |
| 8a | Sjednocení textových nastavení — `TextStyle` (nullable POCO, CSS cascade) + `ResolvedTextStyle` (SkiaSharp helpery). Migrace: TextElement, TableElement, ScheduleElementBase + 3 JŘ elementy, PdfImport, CacheAgendaProvider. Breaking JSON change (flat props → `style`/`headerStyle`/`dataStyle`) | ✅ (2026-03-24) |
| 8b | Proporcionální škálování šablony + UX vylepšení — `ScaleTemplateCommand`, `ApplyScale()` na všech elementech, redistribuce výšek sekcí, výchozí nastavení nových elementů, `HourColumnPerGroup` | ✅ (2026-03-24) |
| 8c | AI-enhanced PDF import — dvoustupňový pipeline (heuristiky → AI enrichment); `AiImportEnricher` s kontextovými prompty; detekce rámečků, loga placeholder, margin fix, TextElement alignment fix | ✅ (2026-03-25) |
| 8d | AI Vision import pipeline — `AiVisionImportService` (composable prompty), `AiGuidedScheduleBuilder` (PdfPig geometrie), `AiVisionImportPipeline` (orchestrace); per-column font wrapping; section headers auto-hide; CellStyleVM font defaults; shared HttpClient | ✅ (2026-04-03) |

## Fáze B0 — UX Sprint ✅ (2026-04-20)

| # | Co |
|---|---|
| B0-1 | Strikethrough na TextStyle + descriptor; oprava tichého nefungování Underline |
| B0-2 | Více barcode typů — EAN-13, EAN-8, PDF417, DataMatrix; konsolidace palety |
| B0-4 | Precision fix — `MmMath.Round(0.1mm)`; resize end, ScaleTemplate, DistributeH/V, properties panel |
| B0-5 | DistributeH/V: přesný gap — zaokrouhlit, poslední element absorbuje zbytek |
| B0-6 | Normalizovat pozice (Ctrl+Shift+N) — dialog s krokem, live preview, CompositeCommand undo |
| B0-8 | Properties panel float formát — `"0.0#"` pro mm pole, round on commit |
| B0-9 | Grid size v UI — ComboBox 1/2/5/10mm v toolbaru, DesignerState.GridSize property |
| B0-11 | Inline validace — červený rámeček + tooltip místo textu pod polem |
| B0-12 | Copy Style / Paste Style (Ctrl+Shift+C/V) — StyleClipboard, kopíruje TextStyle + BorderColor/Width + BackgroundColor |
| B0-13 | Kontextový format mini-toolbar — B/I/U/S + L/C/R/J + ▲/▬/▼ nad properties panelem |
| B0-14 | Rotation property na TemplateElement — float, stupně, JSON serializace |
| B0-15 | Render rotace — canvas.Save/RotateDegrees(cx,cy)/Restore + IsRotated bool helper |
| B0-16 | Hit-testing rotovaných elementů — inverzní rotace test pointu kolem středu |
| B0-17 | Resize handles zakázány pro rotované elementy + rotation badge (angle label) |

---

## Další implementované oblasti

**UX audit — bezpečnost + frustrace + polish** ✅ (2026-03-18)
- Sanitizace VisibilityExpression (whitelist tokenizer + live validace v UI)
- Canvas fixed height + section clamping (sekce nepřesáhnou stránku)
- SectionRole (Header/Body/Footer) — patičky od spodu stránky
- Ctrl+X, Ctrl+0, context menu na canvasu, bound resize, potvrzení mazání sekce
- Nahrávání obrázků s auto-downscale (max 5 MB), SelectAll

**Zastávkový JŘ + Caché integrace** ✅ (2026-03-18)
- ScheduleElementBase + 3 elementy (StopListElement, DepartureGridElement, NotesLegendElement)
- ZastavkoveJrMhdAPI (~1200 řádků business logiky z legacy refaktoru, sloučeno z AgendaJRAPI + TiskMHD + TemplateDesignerMHD)
- CacheAgendaProvider: ParseMockData vnořená pole, GetData, BuildRealData
- PrintDialog: parametry + export reálných dat + multi-data PDF
- SkiaHeightCalculator: schedule element height calculation

**PageLayoutEngine + multi-page** ✅ (2026-03-17)
- Template + DataContext → `List<Page>` s PlacedSections; čistá logika (vrstva 3), žádný SkiaSharp/WPF
- Klasifikace sekcí (headers/footers/body), stránkování (KeepTogether/SplitAcrossPages), dvouprůchodový layout pro OnLastPageOnly
- Flow layout uvnitř sekcí, PerDataRow expanze, PAGE/TOTAL_PAGES injekce
- Multi-page preview (stránky pod sebou) + multi-page PDF export
- IElementHeightCalculator: SkiaHeightCalculator měří TextElement (word wrap) a TableElement

**Flow text v designeru (Word wrap + dynamická výška)** ✅ (2026-03-17)
- TextElement: `WordWrap=true` + `Height=null` → text se zalamuje, výška se dopočítá z obsahu
- `LineSpacing` property, vertikální zarovnání víceřádkového textu, fix `\r\n`
- InfiniteTextElement jako samostatný typ zamítnut — jedno chování řízené properties

**ElementGroup — seskupování elementů** ✅ (2026-03-12)
- `GroupId` property na `TemplateElement` — lehká logická vazba bez nested souřadnic
- Kliknutí na člen skupiny vybere celou skupinu; Alt+klik = jen ten jeden element
- Ctrl+G / Ctrl+Shift+G — seskupit / rozeskupit (plný undo/redo)
- Vizuální indikace: barevný badge v rohu, bounding box skupiny při výběru
- Strom šablony zobrazuje skupiny jako sbalitelné uzly
- Copy/Paste/Duplicate přemapují GroupId (klony nedědí skupinu originálu)
- PDF import automaticky seskupí grafiku podle typu a merge mode

**Locale / CultureInfo v šabloně** ✅ (2026-03-12)
- `Template.CultureCode` — kultura pro formátování čísel a datumů (cs-CZ, sk-SK, en-US, de-DE)
- Protéká celým pipeline: Template → DataContext.Culture → PlaceholderFormatter → TableElement
- Dropdown ve vlastnostech stránky; null = cs-CZ (výchozí); parsování vstupů zůstává InvariantCulture

**Drag & drop při přidávání elementů a sekcí** ✅ (2026-03-12)
- Drag element z toolbaru/palety na canvas — ghost preview, drop target highlight, clamp + snap
- Drag pole z panelu "Pole agendy" na canvas — vytvoří TextElement nebo append k existujícímu
- Drag & drop sekcí v levém panelu — přeřazení tažením, vizuální indikátor, `ReorderSectionCommand`
- Drag element mezi sekcemi — záměrně odloženo (nízká priorita, aktuální UX postačuje)

**Chytré duplikování elementů** ✅ (2026-03-12)
- Ctrl+D → šipka = směrové duplikování (kopie vedle originálu s 4mm mezerou)
- Ctrl+D bez šipky = plain duplikát (+5mm offset); neovlivňuje clipboard
- Multi-select, clamp na bounds sekce, plný undo/redo

**Přichytávání elementů k sobě (smart snap)** ✅ (2026-03-12)
- Edge↔Edge (4 kombinace), Center↔Center, Gap snap (4mm mezera) — bez kategorické priority, vyhraje nejbližší match
- Zvýraznění cílových elementů (šedý dashed rámeček), gap indikátor (černá čárka v mezeře)
- Element snap má přednost před grid snapem; osy X/Y nezávisle
- Toggle v toolbaru + menu; výchozí zapnuto

**Výrazový jazyk** ✅ (2026-03-12)
- Part 1 — podmínky a logické operátory: `{TISK_SIPKY}`, `{POCET} > 0`, `{TYP} == "PD"`, `&&`, `||`, `!`, závorky; VisibilityExpression na elementech a sekcích; ExpressionEvaluator + Parser + AST nodes
- Part 2a — agregace: `SUM({CENA})`, `COUNT()`, `AVG({POLE})`, `MIN({POLE})`, `MAX({POLE})` v TableElement; SummaryRows; AggregationResolver; záložka Souhrn v TableEditorDialog
- Part 2b — formátování placeholderů: `{POLE:N2}`, `{POLE:dd.MM.yyyy}` (.NET formáty); PlaceholderFormatter + DataContext rozšíření
- Part 2c — řetězcové funkce: `{POLE:LEFT(4)}`, `{POLE:UPPER}`, `{POLE:PADLEFT(3,'0')}`, `{POLE:TRIM}`

**UX polish — hotové položky**
- **f. Side panel vlastností** ✅ základ (2026-03-21) — inline záložka "Vlastnosti" v levém panelu; modální dialog zachován jako fallback
- **h. Ikonový toolbar** ✅ (2026-03-21) — 33 Fluent UI ikon, 7 skupin, ToolbarStyles.xaml

---

## 🖨️ PageLayoutEngine + stránkovaný preview (detail) ✅ (2026-03-17)

Preview v designeru = plný engine. "Tisk" = ten samý engine s reálnými daty → PDF.
Architektonické rozhodnutí (2026-03-17): engine je **integrovaný** v TemplateDesigneru, ne samostatný projekt. Oddělení do samostatných .csproj se udělá až bude druhý konzument.

### Vrstvy systému

Viz diagram v `CLAUDE.md` (sekce "Architektura — vrstvy systému").

### PageLayoutEngine — vstup/výstup

```csharp
class PageLayoutEngine {
    LayoutResult Layout(Template template, DataContext? data);
}
class LayoutResult { List<Page> Pages; int TotalPages; }
class Page { int PageNumber; List<PlacedSection> Sections; }
class PlacedSection {
    Section Source;        // odkaz na originální sekci
    float Y, Height;      // absolutní pozice na stránce (mm)
    float? ClipTop, ClipBottom;  // ořez pro SplitAcrossPages
    DataContext? Data;     // obohacená o PAGE/TOTAL_PAGES
}
```

### Implementační kroky ✅ vše hotovo

| Krok | Co | Stav |
|---|---|---|
| 1 | `PageLayoutEngine` — section ordering + repeating (OnEveryPage atd.) | ✅ |
| 2 | Stránkovaný preview — multi-page pod sebou se stíny a rámečky | ✅ |
| 3 | Flow layout — dynamická výška elementů posouvá elementy pod nimi | ✅ |
| 4 | Page breaking — sekce SplitAcrossPages se rozdělí přes stránky (ClipTop/ClipBottom) | ✅ |
| 5 | PerDataRow expanze — sekce se opakuje pro každý řádek dat | ✅ |
| 6 | PAGE/TOTAL_PAGES injekce + VisibilityExpression per stránku | ✅ |
| 7 | Multi-page PDF export — každá stránka = jedna PDF stránka | ✅ |

### Budoucí modularizace (až bude potřeba)

```
TemplateDesigner.Core.csproj      ← vrstvy 1+2+3 (žádný SkiaSharp, žádné WPF)
TemplateDesigner.Rendering.csproj ← vrstva 4 (SkiaSharp, žádné WPF)
TemplateDesigner.csproj           ← vrstva 5 WPF Designer
Edison.PrintService.csproj        ← vrstva 5 headless tisk z EDISONu
```

### Use cases díky oddělitelnosti

| Use case | Vrstvy | Popis |
|---|---|---|
| WPF Designer + preview | 1–5 | Aktuální aplikace |
| Tisk z EDISONu | 1–4 | Print dialog → GetData → engine → PDF |
| Batch tisk | 1–4 | Noční job: 200 faktur → 200 PDF |
| PDF jako služba | 1–4 | EDISON interně: dej PDF pro fakturu #123 |
| REST API | 1–4 | HTTP POST šablona + data → PDF response |
| Web preview | 1–4 | ASP.NET endpoint → PNG/PDF → iframe |
| Email příloha | 1–4 | Auto-generování PDF a připojení k emailu |
