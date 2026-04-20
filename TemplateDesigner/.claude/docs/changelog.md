# Changelog

## 2026-04-20 — Strikethrough, více barcode typů, konsolidace palety

**TextStyle — Strikethrough + oprava Underline:**
- ✅ **`TextStyle` / `ResolvedTextStyle`** — přidána `Strikethrough` property (nullable bool / bool); `DrawTextDecorations()` metoda kreslí podtržení i přeškrtnutí pomocí `SKFont.Metrics` (UnderlinePosition, StrikeoutPosition) s fallback hodnotami
- ✅ **`TextElement.Render()`** — opraveno tiché nefungování Underline (dekorace se musí kreslit manuálně v SkiaSharp 3); `DrawTextDecorations()` voláno po každém `DrawText` v obou větvích (WordWrap i jednořádkový)
- ✅ **`TextElement.GetPropertyDescriptors()`** — přidán descriptor "Přeškrtnutí"

**BarcodeElement — rozšíření typů:**
- ✅ **`BarcodeElementType` enum** — rozšířen o EAN-13, EAN-8, PDF417, DataMatrix
- ✅ **`RenderBarcode()`** — switch expression přes 6 typů na `BarcodeFormat`; `_hints` dict, `_writer` a `_dashEffect` jsou `static readonly` (žádné per-frame alokace)
- ✅ **`GetPropertyDescriptors()`** — dropdown "Typ kódu" obsahuje všech 6 možností
- ✅ **Paleta elementů** — "QR kód" + "Čárový kód" sloučeny do jednoho tlačítka "Kód"; stejně v menu "Přidat element"

---

## 2026-04-03 — AI Vision import pipeline, per-column font wrapping, UX vylepšení

**AI Vision import:**
- ✅ **`AiVisionImportService`** — composable `AgendaPromptContext` prompt systém; per-agenda factory metody (`ZastavkovyJrMhdPromptContext`, `GenericPromptContext`); konfigurovatelný model z `AppSettings.AiAssistant.VisionModel`
- ✅ **`AiGuidedScheduleBuilder`** — PDF screenshot → Claude Vision → JŘ elementy s přesnými rozměry z PdfPig; refaktor `BuildFromVision` do 6 sub-metod; font family z PdfPig (`DetectDominantFontFamily`); `NormalizeHexColor` konsolidace; `PtToMm` konstanta z `ResolvedTextStyle`
- ✅ **`AiVisionImportPipeline`** — orchestrace: extrakce → rasterizace → AI vision → engine → Template; fallback na heuristiku; typ parametrů z `AgendaAPI.GetParameters()` s value-based fallback
- ✅ **`PdfPageRasterizer`** — rasterizace PDF stránky na base64 PNG pro AI Vision

**DepartureGrid per-column font wrapping:**
- ✅ **`ComputeLayout`** — per-column header font pro wrapping i min width měření; dynamická výška záhlaví z max per-column line height
- ✅ **`GroupColumnIndexResolver`** — generický `BuildLookup<T>` nahrazuje 3 duplicitní implementace; `IndexKeyPrefix` konstanta pro `__INDEX_` klíče
- ✅ **Layout cache fingerprint** — `ComputeLayoutFingerprint()` pro invalidaci cache při změně properties

**UX vylepšení:**
- ✅ **Skrytí záhlaví sekcí** — `DesignerState.ShowSectionHeaders` toggle; auto-detekce JŘ šablon; toolbar menu položka "Záhlaví sekcí"
- ✅ **CellStyleVM font defaults** — dialog sloupců zobrazuje skutečné font hodnoty z elementu (`headerDefaults`/`dataDefaults`)
- ✅ **AnthropicApiClient** — shared static `HttpClient` s per-request headers

---

## 2026-03-25 — AI-enhanced PDF import pro JŘ šablony

**AI enrichment pipeline:**
- ✅ **`EnrichedImportResult`** — datový model: `TextMapping` (OriginalText → DbField/PlaceholderText s Confidence), `DayGroupValidation`, `DetectionValidation`
- ✅ **`AiImportEnricher`** — AI služba s kontextovými prompty per `ScheduleImportType`; claude-haiku-4-5; strukturovaný user prompt z detekce + DataSchema; JSON response parsing; debug log
- ✅ **Refaktor `AnthropicApiClient`** — sdílená `CallApiAsync()` statická metoda; `GenerateTemplateAsync()` deleguje
- ✅ **`PdfImportDialogViewModel`** — AI checkbox, agenda dropdown, 3-fázový workflow (heuristiky → AI → apply); graceful fallback; `ApplyEnrichment` odstraní dekorativní texty + nahradí statické za placeholdery
- ✅ **`PdfImportDialog.xaml`** — sekce "AI obohacení" (viditelná jen při JŘ importu): checkbox s API key detekcí, ComboBox agend
- ✅ **MainWindow wiring** — předání `SchemaProvider` + `AnthropicApiKey` do dialogu

**Vylepšení heuristického importu:**
- ✅ **`ScheduleElementBuilder`** — detekce rámečků kolem textů (`FindEnclosingRectangle`); placeholder loga z prázdných obdélníků v hlavičce; Center/Middle zarovnání textů; NotesLegend s dynamickou výškou těsně pod DepartureGrid
- ✅ **`ScheduleDetector`** — detekce jízdních dob (`FindTravelTimeNumbers`); detekce poznámkových párů (`FindNoteLegendBlocks`); overlap test pro záhlaví skupin dnů; rozšíření notes regionu o legendové bloky
- ✅ **`PdfToTemplateConverter`** — margin detection s `ScheduleDetectionResult` (řeší dynamické elementy s `Height=null`); správná výška sekce (přesně = tisknutelná oblast)
- ✅ **`TextElement` bug fix** — jednořádkový režim (WordWrap=false) nyní respektuje HAlign/VAlign
- ✅ **API klíč** — přesunut z `appsettings.json` do `appsettings.local.json` (gitignored)

---

## 2026-03-24 — Proporcionální škálování, redistribuce sekcí, výchozí nastavení

- ✅ **Proporcionální škálování šablony** — `ScaleTemplateCommand` škáluje celou šablonu (PageSize, margins, sekce, elementy); `ApplyScale(float)` virtual metoda na všech elementech s override pro specifické mm/pt properties; dropdown v toolbaru (50%–200%); undo přes inverzní škálu
- ✅ **ApplyScale override na všech elementech** — TextElement (FontSize), LineElement (StrokeWidth, CustomDashMm, CustomGapMm), RectangleElement (StrokeWidth, CornerRadius), TableElement (RowHeight, HeaderHeight, column widths Mm), ScheduleElementBase (DataStyle/HeaderStyle FontSize), StopListElement (DataRowHeight, CurrentStopFontSize, column widths), DepartureGridElement (~12 mm/pt properties), NotesLegendElement (NoteFontSize, NoteLineHeight)
- ✅ **Redistribuce výšek sekcí** — smazání sekce proporcionálně rozdělí uvolněnou výšku; přidání sekce proporcionálně zmenší stávající; `AddSectionCommand` pro undo/redo
- ✅ **Výchozí nastavení nových elementů** — TextElement: prázdný text, Center/Middle zarovnání; všechny: černý rámeček 0.1mm (factory default, ne model default — zpětná kompatibilita JSON)
- ✅ **DepartureGrid: HourColumnPerGroup** — `HourColumnPerGroup` property; sloupec hodin u každé skupiny dnů
- ✅ **Fix: LineSpacing se neškáluje** — bezrozměrný násobitel (1.2 = 120%), ne absolutní rozměr

---

## 2026-03-24 — TextStyle unifikace (krok 8a)

- ✅ **`TextStyle` třída** — nullable POCO s CSS cascade vzorem; `TextStyle.Resolve(parent)` → `ResolvedTextStyle` (struct se SkiaSharp helpery: `CreateFont`, `CreateTextPaint`, `CreateBgPaint`, `ToSKFontStyle`)
- ✅ **TextElement migrace** — flat properties (`FontFamily`, `FontSize`, `FontStyle`, `FontWeight`, `HorizontalAlignment`, `VerticalAlignment`, `TextColor`) nahrazeny jedním `TextStyle Style`; odstraněny enumy `TextFontStyle` a `TextFontWeight` (→ `bool Bold`/`Italic`)
- ✅ **TableElement migrace** — `HeaderFontWeight`/`HeaderBackgroundColor`/`HeaderTextColor` → `TextStyle? HeaderStyle`; `TableColumn.HeaderStyle`; `TableCell.FontWeight`/`TextColor`/`BackgroundColor`/`Alignment` → `TextStyle? Style`
- ✅ **ScheduleElementBase migrace** — `FontFamily`/`FontSize`/`HeaderFontSize`/`HeaderBackgroundColor`/`HeaderForeColor` → `TextStyle DataStyle` + `TextStyle HeaderStyle`; `CopyScheduleBaseProperties` kopíruje `Clone()`
- ✅ **StopListElement / DepartureGridElement / NotesLegendElement** — všechny reference aktualizovány na `DataStyle`/`HeaderStyle`
- ✅ **PdfToTemplateConverter** — vytváří TextElement s `Style = new TextStyle { ... }`
- ✅ **CacheAgendaProvider** — `TableCell` konstrukce přes `TextStyle? Style`
- ✅ **`BackgroundColor` na base `TemplateElement`** — sdílená property pro všechny elementy
- ✅ **`CopyBaseTo()` pattern** — eliminuje Clone() boilerplate (base properties se kopírují jedním voláním)
- ⚠️ **Breaking JSON change** — flat text properties → composable `style`/`headerStyle`/`dataStyle` objekty; staré šablony se nenačtou

---

## 2026-03-23 — Ověření parametrů šablony, fix PrintDialog export, auto-open PDF

- ✅ **Fix: parametry nefungovaly v PrintDialog exportu** — `buildRealData` callback nevkládal parametry do DataContextu; C# rendering (HODINY_OD, CASOVA_OSA atd.) je nenašel → nyní merge přes `WithExtraValues()` po `GetData()`
- ✅ **Fix: HODINY_OD/DO filtr ve všech režimech** — dříve se rozsah hodin aplikoval jen v režimu Range; nyní filtruje i v Compact a Full režimu
- ✅ **TISK_SIPKY parametr** — nový runtime override pro `ShowDirectionArrow` v StopListElement
- ✅ **Auto-open PDF** — po exportu se PDF automaticky otevře ve výchozím prohlížeči (PrintDialog mock + real data + quick export)
- ✅ **Odebrán BAREVNE_ODLISENI** — nepoužívaný parametr z Caché `GetParameters()`
- ✅ **Odebrán TISK_TARIFU** — nepoužívaný parametr + C# Zone override logika

---

## 2026-03-23 — Per-column styling generalizace, StopList styling, architektonické vylepšení

- ✅ **ScheduleElementBase — sdílené font/paint helpery** — `CreateCellFont`, `CreateCellTextPaint`, `CreateCellBgPaint`, `RenderCellBackground`, `MakeFontStyle` extrahované ze schedule elementů do base class; eliminuje ~60 řádků duplikovaného kódu
- ✅ **StopListElement — per-column styling** — `ColumnVisualStyle` (Header/Data `CellStyle`) na `StopColumn`; font, barvy, zarovnání, bold/italic per sloupec; šipka směru jízdy (`ShowDirectionArrow`, `DirectionArrowColor`, `DirectionArrowBold`)
- ✅ **StopColumnsEditorDialog — dvou-tabový refaktor** — Tab 1 "Obecné" (PropertiesPanel pro element properties), Tab 2 "Sloupce" (ListBox + per-column Header/Data CellStyle editory)
- ✅ **CellStyleVM extrakce** — sdílený ViewModel pro CellStyle (header/data zóny) přesunut z DepartureGroupEditorDialogViewModel do `Controls/CellStyleVM.cs`; znovupoužitelný pro oba editorové dialogy
- ✅ **ColorPickerHelper** — sdílená statická utilita pro WinForms ColorDialog z WPF oken; eliminuje duplikaci v code-behind dialogů
- ✅ **ButtonPropertyDescriptor.ActionId** — stabilní strojový identifikátor pro routování button akcí; nahrazuje křehké porovnávání UI labelů (`btn.Label == "..."` → `btn.ActionId == ActionXxx`)
- ✅ **Unifikace disposal patternu** — `manualDisposables` seznam s jedním `foreach` dispose ve `finally`; konzistentní v DepartureGridElement i StopListElement

---

## 2026-03-20 — DepartureGrid per-column styling, redistribuce šířek, sloučené záhlaví

- ✅ **DepartureGridElement — per-column nastavení** — `DepartureGroupColumnStyle`: viditelnost, barvy záhlaví/dat, font size, bold/italic, horizontální + vertikální zarovnání; nullable override vzor (null = výchozí z elementu)
- ✅ **DepartureGroupEditorDialog** — nový dialog: seznam skupin s checkboxy, ComboBox pro zarovnání/font, CheckBox pro bold/italic, color picker; `DepartureGroupColumnVM` s `ToModel()`/`FromData()` konverzemi
- ✅ **DepartureGridElement — redistribuce šířek sloupců** — sloupce skupin dnů se proporcionálně roztáhnou na celou šířku elementu (místo prázdného místa napravo)
- ✅ **DepartureGridElement — word-wrap záhlaví** — text skupiny se nejprve zkusí na jeden řádek, zalamuje až když se nevejde; `WrapHeaderText` helper
- ✅ **DepartureGridElement — sloučené záhlaví** — „Hod" a názvy skupin dnů v jednom řádku místo dvou; výška = `max(groupHeaderH, headerH)`
- ✅ Odebrán `TotalWidth` z `GridLayout` recordu (redundantní po redistribuci)
- ✅ Render optimalizace: style dictionary lookup O(1) místo O(n) per-group, pre-alokované SKPaint pro pozadí sloupců

---

## 2026-03-20 — Smart snap resize, sdílená hranice, split elementu, designer font cap

- ✅ **SnapEngine.CalculateResize** — snap hran při změně velikosti elementu (edge + gap 4mm)
- ✅ **Sdílená hranice** — tah za hranu mění i sousední element (CompositeCommand undo)
- ✅ **Split element** — Ctrl+Shift+H (horizontální) / Ctrl+Shift+J (vertikální): rozdělení elementu na dvě poloviny (undoable)
- ✅ **CleanupSelection po undo/redo** — odstranění osiřelých elementů z výběru
- ✅ **Designer font cap** — omezení fontu na 10pt v designeru pro čitelnost placeholderů
- ✅ Aktualizace nápovědy (F1) o nové zkratky a funkce

## 2026-03-20 — Velikosti a orientace stránky, border, auto-scale, multi-element editing

- ✅ **PageSize editor** v ElementPropertiesWindow — editace rozměrů stránky (šířka, výška, okraje), orientace (na šířku/na výšku), předdefinované formáty
- ✅ **Clamp elementu na hranici sekce** — elementy se nepřesunou mimo bounds sekce
- ✅ **StopColumnsEditorDialog** — nový dialog pro editaci sloupců StopListElement (pořadí, šířky, záhlaví, viditelnost, zarovnání)
- ✅ **StopListElement vylepšení** — přehazování řádků u zastávek, nastavení elementů
- ✅ **Caché refaktor** — AgendaAPI/AgendaJRAPI/TiskMHD úpravy a vylepšení
- ✅ **Border systém v base class** (`TemplateElement`) — `BorderColor` + `BorderWidth`; dvouprůchodové renderování; pixel-snap na sdílených hranách
- ✅ **Auto-scale font** (`ScheduleElementBase`) — proporcionální zmenšení fontů při přesahu pevné Height
- ✅ **Pevná Height pro schedule elementy** — vertikální resize všemi 8 handles
- ✅ **Multi-element editing** — Enter při multi-selectu → dialog společných vlastností

---

## 2026-03-18 — MVVM refaktor + UX vylepšení

- ✅ **Code-behind → ViewModel refaktor** — přesun 7 metod z `MainWindow.xaml.cs` do `MainWindowViewModel`: `UpdateStatusForSelection`, `SelectFromTree`, `InsertOrCopyPlaceholder`, `AppendPlaceholderToElement`, `AddElementAtPosition`, `HandleElementHit`, `HandleEmptyAreaHit`
- ✅ **TextWrapHelper** — extrakce word wrap logiky z TextElement do sdíleného helperu (používá i SkiaHeightCalculator)
- ✅ **DesignerState ZIndex cache** — `GetElementsByZIndexAsc/Desc` per sekce místo opakovaného `OrderBy` při každém renderu a hit testu
- ✅ **Dirty flag + ConfirmUnsavedChanges** — `Window_Closing` handler chrání neuložené změny
- ✅ **Ctrl+X (Cut)** — Copy + Delete v jednom kroku
- ✅ **Ctrl+0 (Fit-to-Page)** — zoom na šířku stránky
- ✅ **Potvrzení mazání sekce** — MessageBox před smazáním sekce s elementy
- ✅ **Validace VisibilityExpression v UI** — live chybové hlášky v PropertiesPanel; `StringPropertyDescriptor.Validate` callback
- ✅ **SectionRole (Header/Body/Footer)** — nový enum oddělující "kdy tisknout" od "kam umístit"; patičky umístěny od spodu stránky v designeru i PageLayoutEngine
- ✅ **Canvas fixed height + section clamping** — stránka má pevnou výšku, sekce se clampují; footery kotveny ke spodnímu okraji
- ✅ **Context menu na canvasu** — pravý klik: element items + section items sloučeny
- ✅ **Bound resize** — element při resize nepřesáhne hranice sekce
- ✅ **Nahrávání obrázků** — max 5 MB, iterativní auto-downscale s potvrzením
- ✅ **SelectAll** — `Ctrl+A` vybere všechny elementy aktivní sekce

---
## 2026-03-18 — Zastávkové jízdní řády a integrace do Caché

- ✅ **ScheduleElementBase** — abstraktní základ JŘ elementů: DataSource klíče (ZASTAVKY, SKUPINY_DNU, ODJEZDY, POZNAMKY), Build helpery s cache (SetCacheRef atomicky čistí cache při změně DataContext reference), společné vizuální properties (FontFamily, FontSize, GridColor, HeaderBackgroundColor)
- ✅ **ScheduleModels** — runtime POCO modely: ZastavkaData, SkupinaDnuData, OdjezdData, PoznamkaData, AnotaceData, AtributData, PoznamkaZastavkyData + enumy TypPoznamky, TypAnotace
- ✅ **StopListElement** — tabulka zastávek s ~15 properties: sloupce (zastávka, čas jízdy, tarifní číslo, zóna, průběhy, základní trasa, na znamení), zvýraznění aktuální zastávky, šedé projeté zastávky, atributy a poznámky zastávky, odsazení
- ✅ **DepartureGridElement** — mřížka minut odjezdů per skupina dnů: nadpis skupiny, sloupec hodin + mřížka minut, anotace jako horní index, data-driven barvy (ForeColor/BackColor), negativní značky invertované; layout cache (`_cachedLayout`) zabraňuje dvojímu ComputeLayout
- ✅ **NotesLegendElement** — legenda poznámek pod odjezdy: 3 typy (Text, NegativníZnačka, SpeciálníZnak), invertované barvy u NZ, Unicode symboly
- ✅ **AgendaJRAPI** — abstraktní Caché třída (mezistupeň v hierarchii AgendaAPI → AgendaJRAPI → TiskMHD): helpery BuildZastavka, BuildOdjezd, BuildSkupinaDnu, BuildPoznamka + AddAnotace/AddAtribut/AddPoznamkaZastavky; ConvertSpecialniZnak; AddStandardJRFields/Collections/Parameters
- ✅ **TiskMHD** — kompletní Caché implementace pro MHD zastávkový JŘ (~1200 řádků): GetSchema, GetTableTemplates, GetTableTemplate, GetData (reálná data z DB: trasy, spoje, poznámky, NZ, atributy), GetMockData, GetParameters; refaktor legacy business logiky z `ALVA.MHD.LinkyPrintFormatMHD`
- ✅ **CacheAgendaProvider rozšíření** — `ParseMockData` rozlišuje JsonArray/JsonObject vs skalární hodnoty; `GetData` nová metoda pro načítání reálných dat s parametry; `BuildRealData` delegát pro PrintDialog
- ✅ **PrintDialog rozšíření** — parametry sestavy (CheckBox/ComboBox/TextBox), export mock PDF i reálných dat z Caché; `RenderMultiDataToPdf` pro multi-zastávkový export; `ShowSaveDialog` extrahováno
- ✅ **DesignerCanvas** — dynamické elementy (Height=null) zobrazují resize handle jen na bocích (ne na horních/dolních hranách); schedule elementy mají speciální placeholder text a šedé pozadí
- ✅ **SkiaHeightCalculator** — case pro ScheduleElementBase podtřídy (deleguje na element.CalculateHeight)
- ✅ **TemplateDesignerMHD** — přepnut na `Extends AgendaJRAPI` (místo AgendaAPI)

---

## 2026-03-17 — PageLayoutEngine — multi-page stránkování, hlavičky/patičky, PDF export

- ✅ **PageLayoutEngine** — nový čistý logický engine (vrstva 3): `Template + DataContext → List<Page>` s `PlacedSection` strukturami; žádný SkiaSharp/WPF
- ✅ **IElementHeightCalculator + SkiaHeightCalculator** — interface pro měření výšky dynamických elementů (TextElement word wrap, TableElement řádky) oddělený od SkiaSharp
- ✅ **Klasifikace sekcí** podle `PrintBehavior`: headers (OnEveryPage, OnFirstPageOnly, OnEveryPageExceptFirst), footers (OnLastPageOnly), body (Once, PerDataRow)
- ✅ **Stránkování**: `KeepTogether` → nová stránka, `SplitAcrossPages` → ClipTop/ClipBottom ořez přes stránky, `AllowSplit`
- ✅ **Hlavičky a patičky** na správných stránkách: OnEveryPage, OnFirstPageOnly, OnLastPageOnly, OnEveryPageExceptFirst; dvouprůchodový layout pro OnLastPageOnly (první průchod zjistí totalPages)
- ✅ **PerDataRow expanze** — sekce se opakuje pro každý řádek z DataContext.GetRows
- ✅ **Flow layout uvnitř sekcí** — dynamické elementy (Height=null) posouvají elementy pod sebou
- ✅ **PAGE/TOTAL_PAGES injekce** — DataContext obohacen per stránku; `{PAGE}` a `{TOTAL_PAGES}` fungují v placeholderech
- ✅ **VisibilityExpression per stránku** — opakující se sekce (OnEveryPage) vyhodnocují výraz na každé stránce (podpora `{PAGE} > 1`)
- ✅ **CultureCode propagace** — `Template.CultureCode` → DataContext.WithCulture při startu layoutu
- ✅ **Multi-page preview** — PreviewCanvas refaktorován: volá engine, renderuje `LayoutResult`; stránky zobrazeny pod sebou s mezerami a stíny
- ✅ **Multi-page PDF export** — `RenderToPdf` iteruje `LayoutResult.Pages`, každá stránka = jedna stránka PDF dokumentu
- ✅ `MainWindow.UpdatePreviewSize()` předává mock data pro správný výpočet výšky při více stránkách

---

## 2026-03-17 — Word wrap + dynamická výška TextElementu

- ✅ **Word wrap v rendereru** — TextElement s `WordWrap=true` nyní skutečně zalamuje text do řádků podle šířky elementu (dříve jen ořezával na jednu řádku)
- ✅ **Dynamická výška** — `Height=null` + `WordWrap=true` → výška se dopočítá z textu; designer zobrazí aproximaci, print engine spočítá z reálných dat
- ✅ **LineSpacing** — nová property (výchozí 1.2×), editovatelná v panelu vlastností
- ✅ **Vertikální zarovnání** pro víceřádkový text (Top/Middle/Bottom)
- ✅ **Fix `\r\n`** — normalizace řádkových zlomů z WPF TextBoxu (dříve zobrazoval čtverečky)
- ✅ Rozhodnutí: InfiniteTextElement jako samostatný typ zamítnut — flow chování řízeno properties na existujícím TextElementu (`WordWrap` + `Height=null`)

---

## 2026-03-13 — Automatická šablona z importovaného PDF

- ✅ **PDF import pipeline** — `PdfPageExtractor` (PdfPig) extrahuje text, čáry, obdélníky a obrázky s pozicemi, fonty a barvami; `PdfTextGrouper` seskupuje slova do textových bloků podle vzdálenosti a fontu; `PdfToTemplateConverter` generuje kompletní Template
- ✅ **PdfImportDialog** — WPF dialog s výběrem PDF souboru, stránek (rozsah/všechny), 3 merge módy grafiky (None/LinesOnly/All), debug logem; async konverze přes `Task.Run` pro UI responsivitu
- ✅ **3 merge módy grafiky** — None (čáry a obdélníky jako elementy), LinesOnly (čáry do background image, ostatní jako elementy), All (vše do jednoho background image)
- ✅ **Detekce okrajů** — automatická detekce a ořez okrajů PDF stránky; přepočet souřadnic na tisknutelnou oblast
- ✅ **Auto-grouping** — importované elementy automaticky seskupeny podle typu a merge mode (grafika do skupiny, texty bez skupiny)
- ✅ **Strom šablony** — nová záložka "Strom" v hlavním okně; hierarchický TreeView celé šablony (sekce → skupiny → elementy); sdílená utilita `TemplateElement.GetSummary()` a `TruncateText()`
- ✅ **LineElement rozšíření** — custom `DashLength` a `GapLength` pro čárkované/tečkované čáry; `GetDefaultDashPattern(LineStyle)` jako veřejná statická metoda
- ✅ **ImageElement vylepšení** — rendering přes `SKImage` + `SKSamplingOptions` místo `SKBitmap.Resize()` (kvalitnější škálování)
- ✅ **MVVM compliance** — PdfImportDialogViewModel bez `System.Windows` závislosti; MessageBox nahrazen callback pattern (`RequestShowError`/`Warning`/`Info`); `RemapGroupId` helper sdílený mezi paste a duplicate
- ✅ **PdfPig NuGet** — `UglyToad.PdfPig 0.1.9` přidán do `TemplateDesigner.csproj`

---

## 2026-03-12 — ElementGroup — seskupování elementů

- ✅ **`GroupId` property na `TemplateElement`** — lehká logická vazba; serializuje se do JSON (null = nezobrazí se); `StringPropertyDescriptor` v panelu vlastností
- ✅ **`ElementGroupHelper`** — utility: `NewGroupId()` (8-char GUID), `GetGroupMembers()`, `GetGroupColor()` (deterministická barva z hash)
- ✅ **`GroupCommand` + `UngroupCommand`** — plný undo/redo; GroupCommand přiřadí společný GroupId, UngroupCommand nastaví null (s obnovou předchozí hodnoty při undo)
- ✅ **Výběr skupiny kliknutím** — klik na člen skupiny vybere celou skupinu v rámci sekce; Alt+klik = jen ten jeden element; Ctrl+klik toggle celou skupinu v multi-selectu
- ✅ **Klávesové zkratky** — Ctrl+G seskupí vybrané elementy, Ctrl+Shift+G rozeskupí
- ✅ **Vizuální indikace** — barevný badge (8×8px) v pravém dolním rohu groupovaných elementů; tečkovaný barevný bounding box kolem celé skupiny při výběru
- ✅ **Strom šablony** — skupiny jako sbalitelné `[G]` uzly s nested elementy; klik na uzel skupiny vybere všechny členy
- ✅ **Copy/Paste/Duplicate** — klony dostanou nový společný GroupId (elementy z jedné skupiny sdílejí nový ID; nelinkují se s originálem)
- ✅ **PDF import** — automatické skupiny: LinesOnly mode → grafika (image + rectangles) do jedné skupiny; None mode → čáry do jedné, obdélníky do druhé skupiny; texty vždy bez skupiny
- ✅ `GroupId = GroupId` v `Clone()` všech 6 typů elementů

---

## 2026-03-12 — Locale / CultureInfo v šabloně + fix drag & drop polí

- ✅ **CultureInfo v šabloně** — nová property `Template.CultureCode` (cs-CZ, sk-SK, en-US, de-DE); formátování čísel a datumů v preview i tisku je nezávislé na systémovém nastavení Windows
- ✅ Kultura protéká celým pipeline: `Template` → `DataContext.Culture` → `PlaceholderFormatter.Format()` → `TableElement.FormatValue()`; parsování vstupů zůstává `InvariantCulture`
- ✅ Dropdown "Formát čísel a datumů" ve vlastnostech stránky; null = cs-CZ (výchozí, neukládá se do JSON)
- ✅ `DataContext.WithCulture()` — rekurzivní propagace kultury do řádkových DataContextů (pro Caché mock data)
- ✅ **Bug fix: drag & drop databázových polí nefungoval** — `ContainerFromElement` u vnořeného TreeView vracel rodičovskou skupinu (`SchemaGroupVM`) místo listové položky (`SchemaFieldVM`); fix: `FindVisualParent<TreeViewItem>` prochází visual tree a najde nejbližší kontejner
- ✅ **Bug fix: TreeView přebíjel CaptureMouse** — field drag nyní používá Window-level `PreviewMouseMove`/`PreviewMouseLeftButtonUp` místo `skCanvas.CaptureMouse()`, které TreeView interně resetoval

---

## 2026-03-12 — Drag & drop elementů, databázových polí a sekcí

- ✅ **Element palette** — nový toolbar řádek (Band 1) s ikonami 7 typů elementů (Text, Obdélník, Čára, Obrázek, Tabulka, QR, Code128); klik + tah na canvas = element se vytvoří na pozici puštění myši
- ✅ **Ghost preview** — při tažení z palety poloprůhledný čárkovaný obrys elementu na pozici kurzoru + jemný modrý overlay přes cílovou sekci
- ✅ **Drag databázových polí** z panelu "Pole agendy" na canvas — vytvoří TextElement s `{POLE}` na pozici kurzoru; pokud se pole pustí na existující TextElement, připojí `{POLE}` k jeho textu (append, ne přepis)
- ✅ **Drag & drop řazení sekcí** — přetažení sekce v levém panelu na jinou pozici; vizuální indikátor pozice dropu (zvětšený margin); 5px threshold pro rozlišení klik vs. drag
- ✅ `ReorderSectionCommand` — nový undo/redo příkaz pro přeřazení sekce (10. typ příkazu)
- ✅ `GetDefaultElementSize()` + `CreateElementByType()` — factory metody ve ViewModelu, sdílené paletou i toolbar menu (eliminace duplikace velikostí)
- ✅ Clamp na bounds sekce + snap na mřížku při dropu elementu
- ✅ Escape během tažení → zrušení, návrat do Idle

---

## 2026-03-12 — Chytré duplikování Ctrl+D+šipka

- ✅ Směrové duplikování: Ctrl+D → šipka umístí kopii přesně vedle originálu s 4mm mezerou (↓ pod, ↑ nad, → vpravo, ← vlevo)
- ✅ Dvoustupňový chord: Ctrl+D nastaví pending flag; následující šipka = směrové duplikování; jiná klávesa nebo klik = plain duplikát (+5mm offset)
- ✅ `SnapEngine.DefaultGapMm` — extrahovaná konstanta (4mm) sdílená mezi gap snapem a duplikováním
- ✅ `DuplicateDirection` enum + `DuplicateSelected(DuplicateDirection?)` — nový overload klonuje přímo z výběru bez ovlivnění clipboardu
- ✅ Clamp na bounds sekce (X ∈ [0, PrintableWidth - Width], Y ∈ [0, SectionHeight - Height])
- ✅ Multi-select: každý element se zduplikuje s vlastní výškou; undo přes CompositeCommand

---

## 2026-03-12 — Automatické snapování elementů

- ✅ `SnapEngine` — nový engine pro přichytávání elementů k sobě při dragu (edge↔edge 4 kombinace, center↔center, gap snap s konfigurovatelnou mezerou 4mm)
- ✅ Bez kategorické priority — vyhraje nejbližší match napříč všemi typy snapu
- ✅ Zvýraznění cílových elementů — jemný šedý dashed rámeček kolem elementu ke kterému se snapuje
- ✅ Gap indikátor — černá čárka v mezeře mezi dvěma elementy při gap snapu, pozice stabilní (odvozena z cílového elementu, ne z kurzoru myši)
- ✅ Element snap má přednost před grid snapem; na každé ose (X, Y) se rozhoduje nezávisle
- ✅ Toolbar toggle "Snap elem." + menu položka "Přichytit k elementům" (výchozí zapnuto)
- ✅ `DesignerState` rozšířen o `SnapToElements`, `ActiveGapIndicators`, `SnapTargetElements`

---

## 2026-03-12 — Agregační funkce v tabulkách

- ✅ `AggregationResolver` — nová třída vyhodnocující `SUM({POLE})`, `COUNT()`, `AVG({POLE})`, `MIN({POLE})`, `MAX({POLE})` nad kolekcí řádků
- ✅ `TableElement.SummaryRows` — souhrnné řádky (zápatí tabulky) s 3-krokovou pipeline: agregace → placeholder resolve → formát
- ✅ `TableEditorDialog` — nová záložka "Souhrn" pro editaci souhrnných řádků a jejich buněk
- ✅ `RenderDynamicRows` / `RenderFixedRows` refaktorovány — vrací `currentY` pro navázání souhrnných řádků
- ✅ `MockDefault()` helper — výchozí mock hodnoty podle typu pole (Number → "100", Date → "01.01.2024" atd.) když schema nemá `Example`
- ✅ Bug fix: `ElementPropertiesWindow` — sdílený ViewModel hromadil `RequestClose` handlery; opraveno odregistrací v `Closed` eventu

---

## 2026-03-12 — Formátovací rozšíření placeholderů {POLE:format}

- ✅ `PlaceholderFormatter` — nová třída aplikující formátovací instrukce na hodnoty placeholderů
- ✅ .NET formáty: `{CENA:N2}` → "12 000,00", `{DATUM:dd.MM.yyyy}` → "01.01.2024"
- ✅ Řetězcové funkce: `UPPER`, `LOWER`, `LEFT(n)`, `RIGHT(n)`, `PADLEFT(n,'c')`, `PADRIGHT(n,'c')`, `TRIM` — ověřeno end-to-end v live preview
- ✅ `DataContext.ResolvePlaceholders` rozšířen o podporu formátu: regex `\{(\w+)(?::([^}]+))?\}`
- ✅ `DataContext.FormatValue` — statická metoda pro použití z TableElement renderingu
- ✅ `NormalizeWhitespace` — nahrazení non-breaking spaces (U+00A0, U+202F) za běžné mezery pro kompatibilitu se SkiaSharp

---

## 2026-03-12 — Parametry sestavy: serializace do JSON šablony

- ✅ `SyncParametersToTemplate()` — nová metoda volaná před každým uložením; zapíše definice parametrů (`name`, `label`, `type`, `defaultValue`, `options`) do `Template.Parameters`
- ✅ Parametry jsou nyní součástí JSON šablony → print engine je může číst přímo ze souboru bez Caché
- ✅ Fallback v `LoadAgendaParameters()` — pokud Caché není dostupné, načte parametry z JSON šablony

---

## 2026-03-12 — HelpDialog (nápověda klávesových zkratek)

- ✅ `HelpDialog.xaml/.cs` — statický scrollovatelný dialog se 6 kategoriemi: Základní ovládání, Soubory a šablony, Editace, Sekce, Zobrazení, Elementy + Tipy a triky
- ✅ Klávesová zkratka F1 pro otevření, tlačítko v menu Nápověda
- ✅ Pokrývá všechny klávesové zkratky aplikace (Ctrl+Z/Y/C/V/D/A/N/O/S, Del, šipky, Ctrl+scroll, Ctrl+Shift+↑↓, PgUp/PgDn, ...)

---

## 2026-03-06 — MVVM refaktoring: TemplateDesignerViewModel : EDViewModel

- ✅ `TemplateDesigner.csproj` — přidán `<ProjectReference>` na `EDControls.csproj`
- ✅ `Config/CacheSchemaWiring.cs` — přidána přetížení `TryCreateMlRest(AppSettings)` a `TryCreate(MLRest, AppSettings)`; refaktorováno původní `TryCreate(AppSettings)` na volání obou nových metod
- ✅ `MainWindowViewModel.cs` (nový, ~500 ř.) — `TemplateDesignerViewModel : EDViewModel`
  - Bindovatelné properties: `CanUndo`, `CanRedo`, `StatusText`, `AgendaNameText`, `HasParameters`, `ShowGrid`, `SnapToGrid`
  - Commands: `UndoCommand`, `RedoCommand`, `SaveCommand`, `SaveAsCommand`, `NewCommand`, `OpenCommand`, `ExportPdfCommand`, `DeleteCommand`, `CopyCommand`, `PasteCommand`
  - `ObservableCollection<SectionVM> Sections` + `RebuildSections()` + `event SectionsRebuilt`
  - `ObservableCollection<SchemaGroupVM> SchemaGroups` + `RebuildSchemaTree()`
  - `ObservableCollection<ParameterControlVM> PreviewParameters` + `RebuildParametersPanel()`
  - Callback properties pro UI: `RequestCanvasRefresh`, `RequestTitleUpdate`, `RequestCanvasSizeUpdate`, `RequestPropertiesPanelUpdate`, `RequestPreviewRefresh`, `RequestExportPdf`
  - Helper VMs ve stejném souboru: `SectionVM`, `SchemaGroupVM`, `SchemaFieldVM`, `ParameterControlVM`, `ParameterTemplateSelector`
  - `public enum AlignMode` přesunut na namespace level
  - Všechny business metody přesunuty z `MainWindow.xaml.cs`
- ✅ `App.xaml` — odebráno `StartupUri="MainWindow.xaml"`
- ✅ `App.xaml.cs` — přidán `OnStartup` (MLRest z AppSettings → předán do `MainWindow`)
- ✅ `MainWindow.xaml` — kompletní přepis s data-bindingem
  - XAML bindingy: sekce ListBox `ItemsSource="{Binding Sections}"`, schema TreeView s `HierarchicalDataTemplate`, parametry `ItemsControl` s `DataTemplateSelector`
  - Grid/Snap: `IsChecked="{Binding ShowGrid/SnapToGrid, Mode=TwoWay}"` (bez code-behind handlerů)
  - Undo/Redo: `Command="{Binding UndoCommand/RedoCommand}"`
- ✅ `MainWindow.xaml.cs` — přepis (~500 ř., bylo 2186): zachováno jen mouse state machine, canvas rendering, klávesnice; veškerá business logika deleguje na `_vm`
- ✅ Build: **0 chyb, 0 varování**

---

## 2026-03-05 — Podmíněné řádky Fixed tabulky + oprava WPF DataGrid crashe

- ✅ `RowVM.VisibilityExpression` — nová property s `OnChanged()` + `⚑` příznak v `RowSummary`
- ✅ `TableEditorDialog` záložka Řádky — přidáno pole "Viditelnost řádku:" (TextBox s Consolas); přímé property management (`_suppressVisibilityChange` flag, bez DataContext bindingu)
- ✅ `RowVM.ToModel()` / `FromModel()` — serializuje `VisibilityExpression` (null pokud prázdné)
- ✅ `TableElement.RenderFixedRows()` — filtruje řádky přes `ExpressionEvaluator.IsVisible(row.VisibilityExpression, ctx.Data)`; `visibleIdx` counter pro správné střídání barev přes skryté řádky
- ✅ Bug fix: `SelectionUnit="Cell"` odebráno z `ColumnsGrid` i `CellsGrid` — opravuje `InvalidOperationException` při přidávání řádků (WPF interní logika výběru řádku selhávala)
- ✅ Simplify: `NotifySummary()` tělo deleguje na `OnChanged(nameof(RowSummary))`; suppress flag přesunut mimo if/else větve v `RowsList_SelectionChanged`

---

## 2026-03-05 — VisibilityExpression evaluator + Schema TreeView

- ✅ `ExpressionNode.cs` — AST uzel; `LiteralNode(object?)`, `VariableNode(string)`, `UnaryNode(string, node)`, `BinaryNode(string, node, node)` jako C# records
- ✅ `ExpressionParser.cs` — recursive descent parser; tokenizer zvládá `{IDENT}`, string literály, čísla, `true/false`, `==!=><>=<=`, `&&||!`, závorky; interní `sealed` třída
- ✅ `ExpressionEvaluator.cs` — `IsVisible(string?, DataContext?) → bool` (fail-open na chybu), `Validate(string?) → string?` (vrátí chybovou zprávu); AST cache (`ConcurrentDictionary<string, ExpressionNode?>`)
  - Typová koerce: `""` → false, `"false"/"0"` → false, neprázdný string → true, číslo != 0 → true
  - Short-circuit `&&` a `||`
- ✅ `PreviewCanvas.DrawContent()` — sekce se přeskočí celé pokud `IsVisible` vrátí false; elementy filtrovány přes `IsVisible(e.VisibilityExpression, data)`
- ✅ `TemplateElement.GetPositionDescriptors()` — nahrazena `BoolPropertyDescriptor` "Tisknout" za `StringPropertyDescriptor` "Viditelnost"; zpětně kompatibilní (starý `"false"` v JSON stále funguje)
- ✅ `ElementPropertiesWindow.BuildSectionDescriptors()` — přidán `StringPropertyDescriptor` "Viditelnost" pro sekce
- ✅ `MainWindow` — levý panel "Pole agendy": `ListBox` nahrazen `TreeView x:Name="SchemaTree"` se třemi expandovatelnými skupinami:
  - **📋 Skalární pole (N)** — skalární `DataField` ze schématu
  - **📊 Kolekce (N)** — `DataFieldCollection` se svými poli jako potomci
  - **⚙ Parametry (N)** — `_agendaParameters` s příznakem "(parametr)"
  - Dvojklik na listový uzel → vloží `{FIELDNAME}` (Tag); skupinové uzly jsou ignorovány (tag je null)
- ✅ `BuildSchemaTree()` — volá se až po `LoadAgendaParameters()` (parametry musí být načteny); sdílené `static readonly` frozen brushy + `FontFamily` (jednou za běh, ne per položka)

---

## 2026-03-05 — Parametry sestavy (Part 1)

- ✅ `TemplateParameter` model — Name, Label, Type (`"bool"/"enum"/"string"/"number"/"date"`), DefaultValue, Options, `GetDefaultTyped()`
- ✅ `Template.Parameters` — nullable list; přítomno v JSON šabloně; připraveno pro budoucí offline fallback (bez Caché)
- ✅ `DataContext.WithExtraValues(IReadOnlyDictionary)` — immutabilní merge; vrací nový DataContext
- ✅ `CacheAgendaProvider.GetParameters(agendaCode)` — volá Caché `AgendaAPI.GetParameters`, parsuje JSON pole, cachuje per agendaCode
- ✅ `AgendaAPI.cls` — přidána neabstraktní `GetParameters` s výchozí implementací `"[]"` (zachovává zpětnou kompatibilitu s existujícími Caché třídami)
- ✅ `TemplateDesignerMHD.cls` — `GetParameters` pro JR: TISK_SIPKY (bool/true), TISK_TARIFU (bool/false), FORMAT_CASU (enum/H:mm); helper `AddBoolParam`
- ✅ `MainWindow` — `LoadAgendaParameters()`, `RefreshParametersPanel()`, `_previewParams`, `_cachedParamValues`
  - Preview panel: `PreviewParamsBorder/Panel` (WrapPanel) — CheckBox pro bool, ComboBox pro enum, TextBox pro ostatní
  - Panel se zobrazí jen pokud agenda má parametry; schovaný pokud `_agendaParameters.Count == 0`
  - Změna kontrolky → invalidace `_cachedParamValues` + `InvalidatePreview()`
  - `_lastParamsAgendaCode` guard s normalizací null/empty → parametry se nenačítají opakovaně při přepínání tabů
- ✅ `getAvailableFields` lambda — merguje schema fields + parametry jako `DataField` s příznakem "(parametr)"; `{TISK_SIPKY}` není označen červeně v Properties panelu
- ✅ `BuildPreviewMockData()` — injektuje `_cachedParamValues` do DataContextu; parametry přepíší případné kolize s mock daty
- ✅ Simplify: `_cachedParamValues` kešuje `ToDictionary` — alokuje se jen při změně, ne každý frame
- ✅ Simplify: `MakeLabel()` lokální funkce — extrahuje duplikovaný `TextBlock` label v `RefreshParametersPanel`

---

## 2026-03-05 — PDF export live preview

- ✅ `PreviewCanvas.RenderToPdf(filePath, data)` — SkiaSharp `SKDocument.CreatePdf`, 72pt/mm, bílé pozadí, respektuje okraje šablony
- ✅ Menu: Soubor → Exportovat náhled jako PDF... (SaveFileDialog, default název z názvu šablony/souboru)
- ✅ Tlačítko PDF v záhlaví live preview panelu (rychlý přístup bez nutnosti otevřít menu)
- ✅ `BuildPreviewMockData()` extrahována ze `OnPreviewPaintSurface` — sdílena s PDF exportem
- ✅ Simplify fix: `schema.FieldCollections ?? []` cachováno do local var (bylo vyhodnocováno dvakrát)
- ✅ Simplify fix: `doc.Close()` odstraněno — `using var doc` zavolá Dispose, který Close volá interně
- ✅ Simplify fix: fallback název souboru „sablona.pdf" místo „.pdf" pro šablony bez názvu

---

## 2026-03-05 — "Ze šablony..." picker + DataFieldCollection refaktoring + bug fixy

- ✅ `TableEditorDialog` — tlačítko "Ze šablony..." v záložce Sloupce (jen Fixed mód)
  - Načte seznam šablon přes `ITableTemplateProvider.GetAvailableTemplates()`
  - Picker dialog zobrazí šablony z Caché; výběrem nahradí sloupce i řádky
  - Tlačítko skryté pokud není dostupný provider nebo mód je Dynamic
  - Auto-prefill sloupců při otevření Dynamic tabulky bez sloupců (rovnoměrné šířky z `element.Width / count`)
- ✅ `DataFieldGroup` → `DataFieldCollection`, odstraněn `FieldScope` enum
  - Všechny kolekce jsou implicitně tabulkové (Scalar skupiny se nikdy nepoužívaly)
  - `CollectionKey` je nyní `string` (non-nullable); kolekce bez klíče se přeskočí
  - Caché wire formát: `groups` → `fieldCollections`; C# parser má fallback na `groups` pro zpětnou kompatibilitu se staršími Caché třídami (loguje DEBUG warning)
- ✅ Bug fix: Resize "zrychlení" u nových elementů s `Height=null`
  - Příčina: `ComputeResize` četl `EffectiveDesignerHeight` z elementu každý frame — měnila se
  - Fix: nový field `_resizeInitHComputed` zachytí výšku jednou při MouseDown; `_resizeInitH` zůstává null pro undo
- ✅ Bug fix: Minimalizace MainWindow po zavření dialogů (obecný ToolWindow problém)
  - `TableEditorDialog.Owner = Application.Current.MainWindow` (ne ToolWindow)
  - `ElementPropertiesWindow.Hide()` explicitně volá `Owner.Activate()` (s guard `IsLoaded`)
- ✅ `TableElement.GetPropertyDescriptors()` — "Zdroj dat" descriptor se zobrazuje jen v Dynamic módu
- ✅ `EnhanceTableEditor` — změna Režimu obnovuje panel (Zdroj dat se zobrazí/skryje)

---

## 2026-03-04 (pokračování) — Caché mock data + DataSource picker

- ✅ `GetMockData` — nová metoda v `AgendaAPI.cls` + `TemplateDesignerMHD.cls`
  - Vrací JSON `{ "values": {...}, "rows": { "ZASTAVKY": [...], "SPOJE": [...] } }`
  - `CacheAgendaProvider.GetMockData()` + `ParseMockData()` — volá Caché a mapuje na `DataContext`
  - Live preview nyní prioritně používá Caché mock data; fallback = schema examples
- ✅ `SuggestStringPropertyDescriptor` — nový typ property descriptoru
  - Filtrovatelný editovatelný ComboBox; `GetSuggestions()` vrací `(Value, Label)` páry
  - Použit pro `DataSource` property v `TableElement` (nabízí groups ze schématu scope=table)
- ✅ DataSource picker — výběr datového zdroje Dynamic tabulky přímo ze schématu agendy
- ✅ Field picker v `TableEditorDialog` — kolonce "Datové pole" nabízí pouze pole z vybrané skupiny (DataSource)
- ✅ Bug fix: null AgendaCode (`GetSchema(null)`) house `ArgumentNullException` — opraveno v `CacheAgendaProvider` i `CompositeDataSchemaProvider`
- ✅ ObjectScript: NIKDY nepoužívat `_` v dot-notation (`obj.KEY_NAME = v`); `_` je v COS operátor konkatenace řetězců! Pro property s podtržítkem použij `obj.%Set("KEY_NAME", v)`

---

## 2026-03-04 — TableElement + CacheAgendaProvider

- ✅ TableElement — kompletní implementace Fixed/Dynamic módu
  - Fixed mód: uživatel definuje sloupce a řádky v UI editoru; buňky podporují statický text i `{PLACEHOLDER}`
  - Dynamic mód: sloupce z designeru, řádky za běhu z `DataContext._rows[dataSource]`; v designeru zobrazuje 3 placeholder řádky
  - `TableMode`, `TableBorderStyle`, `ColumnWidthMode`, `TableRow`, `TableCell` — plný model
  - `AlternatingRowColor`, `HeaderBackgroundColor`, `HeaderTextColor`, `BorderStyle`
  - Šířky sloupců v mm nebo procentech, viditelnost sloupce (`Visible`)
- ✅ `TableEditorDialog` — WPF dialog pro editaci sloupců (záložka Sloupce) i statických řádků (záložka Řádky, jen Fixed mód)
  - Přidat/odebrat/přesunout sloupec nahoru/dolů
  - Přidat/odebrat/přesunout řádek; editace buněk přímo v DataGridu
  - Validace Field názvů proti DataSchema (červené pozadí pro neplatné pole)
- ✅ `CacheAgendaProvider` — nová třída nahrazující `CacheDataSchemaProvider`
  - Implementuje `IDataSchemaProvider` + `ITableTemplateProvider`
  - Cache na GetSchema, GetTableTemplates, GetTableTemplate (Dictionary per agendaCode)
  - Bug fix: `_templatesCache` byl `List?` → nyní `Dictionary<string, List>` keyed by agendaCode
- ✅ `ITableTemplateProvider` — nové rozhraní pro načítání Fixed šablon tabulek
- ✅ `AgendaAPI.cls` (Caché) — abstraktní základ s metodami GetSchema, GetTableTemplates, GetTableTemplate, GetData
- ✅ Referenční implementace `TemplateDesignerMHD.cls` pro JR agendu
- ✅ Bug fixes:
  - `TimeSpan.TryParse` místo `TryParseExact` — opravuje mock hodnoty pro jednociferné hodiny (8:15)
  - Resize undo: ukládá `null` height pro dynamické elementy (ne materializovanou hodnotu)
  - `IsFieldValid` odmítá `"{}"` jako prázdné (ne platné)
  - `_validFieldNames` HashSet s `StringComparer.OrdinalIgnoreCase`
  - Šířky sloupců tabulky se předpočítávají jednou v `Render()`, ne per buňka
  - `AlternatingRowColor` se parsuje jednou před smyčkou, ne per řádek

---

## 2026-03-03 — BarcodeElement + restrukturalizace

- ✅ BarcodeElement (QR kód + Code128, ZXing.Net 0.16.9)
- ✅ Restrukturalizace složek — kód do `dotnet/`, Caché do `CACHE/`, `.vscode/tasks.json`

---

## 2026-03-02 — Section refactoring + UI vylepšení

- ✅ Section refactoring — 7 podtříd → univerzální `Section` s PrintBehavior/OverflowBehavior
- ✅ BackgroundColor na TextElement, custom section templates (`SectionTemplateManager`)
