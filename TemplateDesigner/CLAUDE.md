# CLAUDE.md — Kontext pro AI agenta

## Co je tento projekt

**TemplateDesigner** je WPF desktopová aplikace pro tvorbu šablon tiskových sestav.
Náhrada staršího nástroje postaveného v InterSystems Caché/Globals.
Při navrhování nových featur, bez v potaz, že v budoucnu by mohl být nástroj, alespoň částečně, ovládán pomocí AI.Dobre

Systém: **1. Designer** (tento projekt) — vizuální návrh šablon → **2. Print Engine** (integrovaný) — šablona + data = PDF.

---

## ⚡ Aktuální stav projektu

> **📝 PRO AI AGENTA:** Průběžně aktualizuj. Dokončené → přesuň do "✅ Co máme". Nové problémy → "🐛 Známé problémy".

**Datum poslední aktualizace:** 2026-04-23

### 🎯 Aktuální focus

1. **Fáze A: Odladění MHD zastávkového JŘ s reálnými daty** (Fáze B0.5 Code Hygiene dokončena; jednotná kaskáda stylů buněk napříč StopList/DepartureGrid/TableElement dokončena).
2. Krok 8 roadmapy — edge cases (prázdné směry, speciální znaky, velké linky), vizuální shoda s referenčním PDF (pixel-by-pixel), fonty pro speciální znaky.

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
- **Caché ZastavkoveJrMhdAPI** — jedna třída `ALVA.TemplateDesigner.API.ZastavkoveJrMhdAPI` dědící přímo z `AgendaAPI`; kompletní 6-fázový tiskový engine MHD + všechny helpery (BuildZastavka, BuildOdjezd, BuildSkupinaDnu, BuildPoznamka, AddAnotace, AddAtribut, AddPoznamkaZastavky, ConvertSpecialniZnak) + GetSchema/GetData/GetMockData
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

Ruční reference:
- `.claude/docs/changelog.md` — kompletní changelog
- `.claude/docs/roadmap.md` — plná roadmapa s detaily
- `.claude/docs/schedule-elements-architecture.md` — architektura specializovaných JŘ elementů (Caché wire formát, hierarchie elementů, matice zodpovědností)
