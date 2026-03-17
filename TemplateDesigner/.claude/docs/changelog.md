# Changelog

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
