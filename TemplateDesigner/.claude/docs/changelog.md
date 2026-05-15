# Changelog

## 2026-05-15 — fix: cleanup aktivních instancí při Open template s jinou agendou

Když user otevřel šablonu (z disku nebo z Caché) s jinou `AgendaCode`, než měla předchozí, aktivní instance (linky MHD) se nevyčistily → ◀▶ na chipu zkusilo GetData s LinkaID z původní agendy → silent fail. Corner case z `2026-05-15 — v1.0 demo balíček` known limits.

- ✅ Nový helper `ClearActiveInstances()` v `MainWindowViewModel` — no-op pokud už prázdné, jinak reset + fire change events
- ✅ `OpenTemplateAsync` (disk) — snapshot starého `AgendaCode` před Load, po Load porovnání case-insensitive, clear pokud se změnilo
- ✅ `OpenFromCacheAsync` — totéž
- ✅ `ReplaceWithBlankTemplate` (volaná z `ChangeAgenda` / `NewTemplate`) — refaktor inline clearu na sdílený `ClearActiveInstances()` helper

Žádné breaking změny.

---

## 2026-05-15 — v1.0 demo balíček: Caché storage + přepínač agendy + Instance Picker

**Tři postupné commity (5ac9dd0 → 82b68be → fd25008) tvoří kompletní release readiness pro testovací nasazení.** Zákazníkovi lze ukázat end-to-end workflow pro zastávkový JŘ MHD.

### Caché Template Storage (`5ac9dd0`)

- ✅ **Nová třída** `ALVA.TemplateDesigner.API.Templates` s 5 metodami (`ListTemplates`, `LoadTemplate`, `SaveTemplate`, `DeleteTemplate`, `RenameTemplate`). Per-IDFa storage `^ALVA.TemplateDesigner.Templates(IdFa,"DTA",TemplateID,"Metadata"|"Data")` + sekundární index `("IDX","ByAgenda",AgendaCode,TemplateID)`. JSON wire formát (R3 architektonické pravidlo). Metadata: Name, AgendaCode, Author, CreatedAt, ModifiedAt, ModifiedBy, Version.
- ✅ **`CacheTemplateService`** (.NET) — async wrapper přes `Task.Run` nad sync MLRest (R1), vlastní `CacheTemplateException` (R2 přes `IPopupDisplayer`), POCO `CacheTemplateMetadata`.
- ✅ **`CacheTemplatePickerDialog`** — list šablon s filtrem agendy + search + Open/Rename/Delete; client-side ICollectionView filter (instant refresh). Druhý entry point „Nástroje → Šablony v Caché..." sdílí stejný dialog jako „Soubor → Otevřít z Caché..." (správce + open intent v jednom).
- ✅ **`SaveToCacheDialog`** — name + agenda dropdown při Save As.
- ✅ **Menu wiring** — „Soubor → Otevřít z Caché... / Uložit do Caché [jako...]"; `SaveCommand` routing: Caché origin → save zpět do Caché; disk → file; nová → SaveAs.
- ✅ **Caché namespace refactor** — `API/` (obecné nástroje, např. Templates) vs `Agendas/` (per-agenda data + AgendaAPI base) vs `Forms/` (Wave 4). ZastavkoveJrMhdAPI a TemplateDesignerFAKTURY přesunuty do Agendas/.
- ✅ **GENERAL agenda fallback** — agenda s `cacheClass = ""` v appsettings → šablona bez DB vazby. CacheAgendaProvider short-circuit ve všech metodách (GetSchema/Data/Mock/atd). Bugfix: `BuildAllPreviewDataPages` vrací `[new DataContext()]` místo `[]` → PDF/PNG export funguje i bez agendy.

### Přepínač agendy + permanentní status indikátor (`82b68be`)

- ✅ **`ChangeAgendaDialog`** — list dostupných agend s „(aktuální)" badge, výběr přepne celý kontext šablony. Důvod: agenda určuje schéma DB polí + typy elementů + reálná data, zachovat rozdělanou JŘ šablonu jako fakturu nedává smysl.
- ✅ **Tok přepnutí**: Klik na chip / menu / **Ctrl+Shift+A** → ChangeAgendaDialog → pokud nová agenda jiná → ConfirmUnsavedChanges (Save/Discard/Cancel) → `ReplaceWithBlankTemplate(agendaCode)` (sdílený helper s NewTemplate).
- ✅ **Permanentní viditelnost agendy na 3 místech**:
  - Status bar dole vlevo: zelený klikatelný chip „Agenda: …"
  - Window title prefix: `[MHD]` / `[FAKTURY]` / `[GENERAL]`
  - Sidebar „Pole agendy" header (klikatelný)

### Instance Picker pro MHD linky (`fd25008`)

- ✅ **`AgendaAPI.GetAvailableInstances`** (abstract default vrací prázdné pole) — agenda bez konceptu instancí nemusí přepisovat.
- ✅ **`ZastavkoveJrMhdAPI` impl** — iteruje `^ALVA.MHD.LSO(IdFa,1,"IDX",1,1,1,SkupinaLinekID,LinkaID)`, pouze linky aktivní **skupiny linek** (default první nezblokovaná z `^ALVA.MHD.ENUMS(IdFa,"SkupinyLinek")` přes nový helper `GetDefaultSkupinaLinekID`; optional `SKUPINA_LINEK_ID` IN parametr pro Edison-driven kontext). TrasaID filter pro orphan linky (jinak by GetData padl na `<SUBSCRIPT>`). Case-insensitive substring search server-side, limit 50. Defensivní `if TrasaID = ""` guard v GetData s meaningful chybou.
- ✅ **`CacheAgendaProvider.GetAvailableInstances(agendaCode, search?, limit=50)`** + POCO `AgendaInstance(Value, Display, Subtitle?)`.
- ✅ **`InstancePickerDialog`** — search box (Enter → server query), checkboxy multi-select, Vybrat vše / Odznačit vše, count indicator.
- ✅ **`ActiveInstances` state** v VM (mimo Template — session scope; reset při změně agendy): `ActiveInstanceIndex`, `CurrentActiveInstance`, `ActiveInstanceText`, `HasMultipleActiveInstances`. Commands `OpenInstancePicker / Previous / Next`. `RefreshDataForCurrentInstanceAsync` — async fetch GetData při změně instance, status text feedback („Načítám / N stránek / Chyba").
- ✅ **Status bar 2. modrý chip** vedle agendy: „Instance: X — Y (N/M)" s ◀▶ tlačítky; klik otevře picker.
- ✅ **Multi-instance batch** — `BuildAllPreviewDataPages` (Soubor → Exportovat...) i `PrintDialog buildRealData` (Soubor → Tisk...) iterují všechny ActiveInstances, generují N×K stránek. **Performance: 600 stránek za ~9s (5.2s fetch + 3.6s render, ~15ms/stránka)** — top decile vs commercial reporting (Crystal Reports 30-60s, SSRS 20-45s, JasperReports 15-30s).
- ✅ **Per-agenda param mapping** — MHD → `LINKA_ID`, ostatní → `INSTANCE_ID` (hardcoded zatím; generalizace až přibude druhá agenda s instancemi).

### Architektonická pravidla (z roadmap §5 — všechna dodržena)

- **R1 Async-only Caché** — `CacheTemplateService` i instance fetch přes `Task.Run` (MLRest je sync, wrapper uvolní UI thread)
- **R2 Chyby přes `IPopupDisplayer`** — `PopupDisplayer.Error(ex)` / `Info(msg)` / `Confirm`; žádný `MessageBox.Show` v VM
- **R3 JSON wire format** — všechny nové API metody `obj.%ToJSON()` v OUT, .NET `JsonSerializer.Deserialize`
- **R4 ViewModely bez WPF** — `Application.Current.MainWindow` jen v handlerech otevírajících dialog (Owner), žádné `System.Windows` v core VM logice
- **R5 Template versioning** — `Template.Version` field uložen v metadatech v Caché; `TemplateMigrator` zatím no-op (žádné breaking změny)

### Známé limity (akceptované pro v1.0)

- LocalPrefs persistence aktivních instancí (po restartu se znovu vyberou)
- Pořadí linek v pickeru podle subscriptu ($o iterace), ne by Cislo/Nazev
- Tabbed UI pro Open dialog (zatím dvě menu cesty)
- Batch export blokuje UI thread (OK pro 1–30 linek; pro 100+ progress bar potřeba)

Žádné breaking JSON změny v Template formátu. Caché global `^ALVA.TemplateDesigner.Templates` je nový (žádná migrace dat).

---

## 2026-05-01 — ImageElement ScaleMode + tenké rámečky + descriptor sort

**Tři vyladění objevené při ladění šablon:**

- ✅ **ImageElement honoruje `ScaleMode`** — bug: renderer ignoroval property, vždy používal Stretch (deformoval obrázek). Fix: extrahovaný helper `DrawImageWithScaleMode` se 4 módy (Fit = vejde se celý / Fill = vyplní s oříznutím / Stretch = roztáhne / None = originální velikost). Fill a None se klipují na element rect. Defaultní Fit zachová poměr s letterboxem. Element `BackgroundColor` se zobrazí v mezerách u Fit. České labely v dropdownu.
- ✅ **„Přizpůsobit poměru obrázku" tlačítko** — `ButtonPropertyDescriptor.ActionMatchImageAspect`; `ImageElement.GetImageAspectRatio()` dekóduje base64 a vrátí W/H. Handler v `ElementPropertiesWindowViewModel` přepočítá `Height = Width / aspect`, undo přes `SetPropertyCommand`.
- ✅ **Sub-pixelové rámečky** — `RenderBorder` adaptive: `widthPx < 1f` → fractional stroke + IsAntialias=true, jinak pixel-snap (původní chování pro sdílené hrany). Dříve `MathF.Max(1f, ...)` clampoval cokoli pod ~0.265mm na 1px → tenké rámečky nešly. Teď 0.05mm vykreslí jako jemný AA pruh.
- ✅ **Stable sort by group v `PropertiesPanel`** — descriptory přicházející z různých helperů často nebyly seřazené podle skupiny; panel pak zobrazoval duplicitní hlavičky („Zastávky" a „Záhlaví" 2× v StopList). Teď se sortuje stabilně: skupiny v pořadí prvního výskytu, řádky uvnitř skupiny v deklaračním pořadí.
- ✅ **Border v „Obecné" group** — `BorderColor`/`BorderWidth` přesunuty z group „Rámeček" do „Obecné" v `GetPositionDescriptors`. Pro schedule elementy se Border merguje s `GridColor` (konzistentní rychlý přístup k základním vlastnostem). Pro non-schedule vytváří „Obecné" group (jediný název napříč elementy).

Žádné breaking JSON změny.

---

## 2026-04-30 — Doladění konzistence: DataRowHeight + dialog tlačítka + dokumentace patternů

Drobná polish vrstva nad sjednocením Header/Data zón.

- ✅ **Sjednocený `GetDataRowHeightDescriptor()` helper** na `ScheduleElementBase` — Get vrací reálnou výpočtenou výšku z fontu (uživatel vidí „4.5 mm" místo „0"), Set rozpozná „auto" zpětně přes epsilon comparison. StopList i DepartureGrid teď oba používají tento sjednocený pattern. Label jednotně „Výška řádku".
- ✅ **Konzistentní popisky tlačítek dialogů sloupců** — sjednoceno na „Sloupce" (label) + „Upravit sloupce..." (ButtonLabel) napříč StopList/DepartureGrid/TableElement.
- ✅ **`EnumPropertyDescriptor.HAlignOptions` + `VAlignOptions`** — sdílené statické tuple arrays na `EnumPropertyDescriptor`. DRY pro standardní zarovnání napříč elementy. TextElement `Vodorovně` rozšiřuje sdílené options o „Do bloku" (Justify) přes spread operátor — Justify nemá smysl pro cell-based elementy (CellRenderer ho ignoruje). Sjednoceny labely (Nahoře/Dole místo dřívějšího Nahoru/Dolů).
- ✅ **TableElement `Header()`/`Data()` privátní helpery** — zkracují 5× boilerplate `HeaderStyle ??= new TextStyle(); HeaderStyle.X = v;` na `Header().X = v`. Lazy init pro nullable styles.
- ✅ **`mvvm-patterns.md` doplněn o sekci „Styling patterny"** — Pattern 1 (single-zone, TextElement) vs Pattern 2 (dual-zone, tabulkové elementy). Šablony pro nové elementy + pravidla inheritance/composition + záměrné výjimky (NotesLegend `NoteFontSize`, DepartureGrid 5 zón, TableElement promoted shortcut).

Žádné breaking změny.

---

## 2026-04-30 — Sjednocení Header/Data zón v properties panelu

Konzistentní rychlý přístup k základním stylům buněk napříč všemi tabulkovými elementy.

**Problém:**
- StopList/DepartureGrid měly Header/Data styling jen v dialogu sloupců (špatná discoverability)
- NotesLegend nemá column dialog → HeaderStyle/DataStyle úplně nepřístupné (uživatel narazil na nemožnost změnit pozadí záhlaví)
- TableElement už měl ad-hoc 2 properties v panelu (legacy precedent), ale nekonzistentně se zbytkem

**Řešení — princip „rychlý přístup v panelu, kompletní cascade v dialogu":**

- ✅ **`ScheduleElementBase.GetCellZoneBaseColorDescriptors()`** (nový helper) — vrátí 4× `ColorPropertyDescriptor`:
  - `Pozadí záhlaví` → `HeaderStyle.BackgroundColor` (fallback `ScheduleHeaderDefaults.BackgroundColor`)
  - `Barva textu záhlaví` → `HeaderStyle.TextColor`
  - `Pozadí buněk` → `DataStyle.BackgroundColor`
  - `Barva textu buněk` → `DataStyle.TextColor`
- ✅ **NotesLegend** — refaktor na helper, zrušen ad-hoc fix; přejmenování skupin na konzistentní „Záhlaví"/„Datové buňky"/„Filtrování"/„Negativní značky"
- ✅ **StopListElement** + **DepartureGridElement** — přidaly `.. GetCellZoneBaseColorDescriptors()` do GetPropertyDescriptors. Nově dostupné 4 base barvy v panelu, dialog pro detail (font/align/padding) nezměněn
- ✅ **TableElement** (parallel implementation, jiná hierarchie) — reorganizace: existující „Pozadí záhlaví" + „Tučné záhlaví" přesunuty do skupiny „Záhlaví"; doplněno „Barva textu záhlaví", „Pozadí buněk", „Barva textu buněk". `Tučné záhlaví` zachováno (uživatel ho v panelu očekává — častý toggle u tabulek)

**Architektonické rozhodnutí — nesdílíme helper přes hranici `ScheduleElementBase ↔ TableElement`:**
- Jiné baked defaults (`ScheduleHeaderDefaults` vs `TableHeaderParent`)
- Jiná nullability `HeaderStyle` (non-null vs nullable)
- Sdílení přes interface = víc kódu než duplikace 4 deskriptorů. Refaktor až kdyby přibyl 5. cell-based element.

**Pattern „rychlý přístup × cascade":**
- Panel: 4 base barvy (bg+text pro Header a Data) — nejčastější rychlé úpravy
- Dialog sloupců: kompletní `CellStyleEditor` (font, align, padding, atd.) + per-column overridy

**Round-trip safe:** panel a dialog editují stejné pole; `CellStyleVM.FromModel`/`ToModel` preservuje explicit hodnoty → nastavení barvy v panelu přežije otevření a OK dialogu beze změny.

**Žádné breaking JSON změny** — jen UI cesta k existujícím polím.

---

## 2026-04-30 — Úklid duplicitního UI: LineSpacing + DataRowHeight

Tichý mrtvý kód v editoru stylu buněk + duplicitní property mezi schedule elementy.

- ✅ **`LineSpacing` smazán z `CellStyleEditor.xaml`** — pole „Řádkování × velikost fontu" nemělo žádný efekt: `CellRenderer.DrawCell` je single-line a `LineSpacing` ignoruje. Property fungovala jen v `TextElement` (multi-line word wrap), kde má vlastní property descriptor; pro tabulkové elementy (StopList/DepartureGrid/TableElement) byla mrtvá. Pro výšku řádku tabulkového elementu existuje `DataRowHeight` v properties panelu (skupina „Řádky")
- ✅ **`AllowLineSpacing` flag odstraněn z `CellStyleEditOptions`**
- ✅ **`CellStyleVM.LineSpacing` field smazán** — VM už ho neexponuje (TextStyle.LineSpacing zůstává v modelu pro TextElement). Existující JSON s `lineSpacing` na cell elementech round-tripne přes neserializované `null` → následující save ho odstraní
- ✅ **`DataRowHeight` hoistován do `ScheduleElementBase`** — odstraněna duplicitní property z `StopListElement` a `DepartureGridElement`. Stejná semantika („výška jednoho datového řádku v mm; null = auto z fontu"), stejný typ (`float?`). NotesLegendElement teď property dostal zdarma; `TableElement.RowHeight` zůstává separátně (jiná hierarchie, jiná semantika — non-nullable, fixní default 6mm)

Žádné breaking změny v JSON, žádné migrace třeba.

---

## 2026-04-30 — Click bez snapu + per-template grid/snap

Drobný UX fix kolem precizního pozicování:

- ✅ **Click-vs-drag threshold (4px)** — `MainWindow.xaml.cs` — pouhý klik na element (např. pro selekci nebo otevření properties) nespustí soft-snap. `_dragHasMoved` flag se nastaví až po překročení prahu; `EndDrag` necommituje, pokud myš nepřekročila threshold. Reason: před fixem klik při GridSize=5mm „strhnul" 0.1mm pozici elementu na 5mm grid
- ✅ **`TemplateEditorPreferences`** (nová třída v `Models/`) — nullable `GridSize`, `SnapToGrid`, `SnapToElements`, `ShowGrid`. Uloženo v JSON jako `editorPreferences` na `Template` (vedle `parameters`, `sampleData`, `cultureCode`)
- ✅ **Apply on Open / Capture on Save** — `MainWindowViewModel.ApplyEditorPreferencesFromTemplate()` + `CaptureEditorPreferencesToTemplate()`; volá se v `OpenTemplateAsync`, `SaveTemplate`, `SaveAsTemplate`, `SaveAndContinue`
- ✅ **`RequestGridSizeUiSync` callback** — VM → View; `MainWindow` napojuje `SyncGridSizeComboBox`, který najde `ComboBoxItem` podle Tag a aktivuje ho (CmbGridSize není bindovaný, jen event-driven)
- ✅ **VM proxy settery** v Apply — `GridSize = gs` (ne `State.GridSize = gs`), aby se firnul `PropertyChanged` a bound toggle buttony (`ShowGrid`/`SnapToGrid`/`SnapToElements`) se v toolbaru aktualizovaly
- 📋 **Backwards-compat** — staré šablony bez `editorPreferences` se chovají jako dřív (null → nepřebijí current state)

---

## 2026-04-22 — Tri-state editor stylů + placeholdery zděděných hodnot (`3cf59d1`)

Dokončení „známých omezení" z fáze 4 — editor stylů buněk nyní plně rozlišuje „dědit od rodiče" od „explicitně nastaveno".

- ✅ **`CellStyleVM` — nullable properties napříč** — všechny editovatelné vlastnosti jsou `T?` (`string?`, `float?`, `bool?`, `TextHorizontalAlignment?`, …). Null = dědit od rodiče v kaskádě. Konec workaround `defaultBold`/`defaultFontFamily`/`defaultFontSize` parametrů — FromModel bere povinně `ResolvedTextStyle inherited`
- ✅ **`EffectiveBold`/`EffectiveItalic`/`EffectiveUnderline`/`EffectiveStrikethrough` derived gettery** — `_bold ?? Inherited.Bold` — 2-stavové checkboxy zobrazují reálnou platnou hodnotu (nikoli „explicit override"). Setter zapisuje explicit override
- ✅ **`HasExplicit<X>` derived** — řídí viditelnost „×" reset tlačítek (vidět jen při explicit override)
- ✅ **Jeden `ResetFieldCommand(paramName)`** nahrazuje per-field reset commands; CommandParameter = název property
- ✅ **`CellStyleVM.Inherited: ResolvedTextStyle`** — read-only, drží vyřešený rodičovský styl; XAML ho bindne jako šedivý italic placeholder v prázdných polích (`FontFamily`, `FontSize`, HAlign/VAlign ComboBox overlay)
- ✅ **`CellStyleEditor.xaml`** přepracován — 2-stavové checkboxy (dřív matoucí IsThreeState s indeterminate čtverečkem); HAlign/VAlign ComboBox bez null-sentinelu, placeholder jako overlay TextBlock; barva přes `FirstNonEmptyHexToBrush` MultiBinding (explicit ?? inherited)
- ✅ **`Controls/CellStyleEditor.Converters.cs`** (nový) — `NullOrEmptyToVisibleConverter`, `NotNullToVisibleConverter`, `FirstNonEmptyHexToBrushConverter` (MultiBinding), `HAlignToLabelConverter`, `VAlignToLabelConverter`
- ✅ **Public API `Resolve<X>Parent()` na elementech** — `StopListElement.ResolveColumnHeaderParent/Data()`, `DepartureGridElement.ResolveHour{Header,Data}Parent()/ResolveGroup{Header,Minute}Parent()`, `TableElement.ResolveColumnHeaderParent/Data()`. Dialogy si volají parent kaskádu místo hardcoded defaultů
- ✅ **Legacy fallbacky v DepartureGrid přesunuty do parent getterů** — `HeaderVerticalAlignment`, `SkupinaDnuHeaderLineHeight`, `MinuteFontSize`, `MinuteBold` aplikovány v parent pro konzistenci UI a renderu
- 📋 **Bez breaking JSON změny** — model TextStyle byl již nullable, kaskáda renderu se nemění
- 📋 **Rozšiřitelnost hotová** — nový tabulkový element stačí implementovat `Resolve<X>Parent()` metody + host dialog volá `CellStyleVM.FromModel(style, element.Resolve<X>Parent())` + XAML `<CellStyleEditor>`

---

## 2026-04-22 — Jednotná kaskáda stylů buněk (fáze 0–4)

Koherentní refactor přes 4 commity: sjednocení stylování buněk napříč všemi tabulkovými elementy (StopList, DepartureGrid, TableElement) do čtyřvrstvé kaskády `column → element → defaults → CellDefaults`.

**Fáze 0+1 — infrastruktura + StopList** (`b19d5e9`):
- ✅ **`Rendering/CellDefaults.cs`** — autoritativní výchozí hodnoty pro buňku (`FontFamily=Arial`, `FontSize=9pt`, padding=0.5mm, HAlign=Left, VAlign=Middle, LineSpacing=1.2). Poslední vrstva kaskády
- ✅ **`Rendering/CellStyleEditOptions.cs`** — record s flagy `Allow<X>` pro per-kontextové skrývání UI editace (zatím všude `AllEnabled`)
- ✅ **`Rendering/CellRenderer.cs`** — jediné místo v aplikaci, kde se pozicuje a vykresluje text buňky: bg → padding shrink → HAlign+VAlign text → Underline/Strikethrough
- ✅ **`Rendering/ColumnRenderer.cs`** — orchestruje kreslení řádku sloupců přes `CellRenderer` + volitelné svislé mřížkové čáry
- ✅ **StopListElement** migrován na kaskádu: `Resolve*Style` helpery, `col.Alignment` legacy fallback zachován

**Fáze 2 — DepartureGrid** (`574e9b1`):
- ✅ **4 `Resolve*Style` helpery** — HourHeader, HourData, GroupHeader, GroupMinute
- ✅ **Pre-created per-group fonty/painty** — perf pro 100+ buněk/frame (6 dictionary: header/data × font/textPaint/bgPaint)
- ✅ **Anotace element-level** — nedědí z column.Data (symboly 80% z minute fontu, text z `AnotaceFontSize`)
- ✅ **Smazán dead code v ScheduleElementBase** — `BuildCellStyle`, `CellStyleResources`, `DisposeAll`, `RenderCellBackground`, `CreateCellBgPaint`, `CreateCellTextPaint`, `CreateCellFont`, `MakeFontStyle`. Zůstal jen typeface cache + `GetTypeface`

**Fáze 3 — TableElement** (`7080ea5`):
- ✅ **`TableColumn.DataStyle`** (new) + **`TableElement.DataStyle`** (new) — per-column a element-level override datových buněk
- ✅ **`TableHeaderParent`/`TableDataParent`/`TableSummaryParent`** kompaktně nad `CellDefaults.Default` přes `with { ... }` — nahradily 10-řádkové plné deklarace
- ✅ **Všechny 4 render paths** (Header, FixedRows, DynamicRows, SummaryRows) jdou přes `CellRenderer.DrawCell`
- ✅ **Legacy `col.Alignment`** zachován jako fallback v kaskádě (dříve nefunkční v UI, teď začíná reálně fungovat)
- ✅ **Vedlejší opravy** získané kaskádou: HAlign funguje všude, VAlign=Middle (místo hardcoded baseline), Padding 0.5mm default (místo hardcoded 4px), Underline/Strikethrough respektované u Dynamic řádků

**Fáze 4 — sdílený `CellStyleEditor`** (`f8b59d3`):
- ✅ **`Controls/CellStyleEditor.xaml` + `.xaml.cs`** — UserControl (jedno místo UI editace TextStyle zóny), DP `EditOptions`, 4× color picker commands, `HAlignmentOptions`/`VAlignmentOptions`/`FontSizeOptions` defaults
- ✅ **`CellStyleVM` rozšířen** — `VAlignmentIndex`, `Underline`, `Strikethrough`, `LineSpacing` (+ FromModel/ToModel/Reset)
- ✅ **3 dialogy migrovány** — `DepartureGroupEditorDialog` (-107 inline), `StopColumnsEditorDialog` (-107 inline), `TableEditorDialog` (nový tab „Styl sloupců" +65)
- ✅ **Bugfix** — `StopColumnVM.FromModel` nepředával `defaultBold: true` → po open+OK beze změn se ukládal explicit `Bold=false` overrriduje element-level `Bold=true` → záhlaví zastávek ztratilo tučnost. DepartureGroup/Table to měly správně
- 📋 ~~**Známá omezení** — `CellStyleVM.Bold/...` jsou `bool` + placeholder hodnoty chybí~~ → **vyřešeno v `3cf59d1`** (viz entry výše)

**Celkový efekt** (9 commitů modified, 2 new files):
- Jedna kaskáda pro všechny tři tabulkové elementy (ze 3 různých ad-hoc přístupů)
- Jeden render helper (`CellRenderer.DrawCell`) místo duplikované pozicovací logiky
- Jeden UI control (`CellStyleEditor`) místo inline XAML ve třech dialozích
- Nové vlastnosti (VAlign, Underline, Strikethrough, LineSpacing) dostupné napříč všemi elementy najednou
- Připraveno pro přidání dalších tabulkových elementů bez duplikace

---

## 2026-04-21 — Code Hygiene: Extract SaveSectionTemplateDialog (H-9)

- ✅ **Nový dialog triad `Windows/SaveSectionTemplateDialog/`** — Window + ViewModel + XAML dle závazného standardu (architecture.md); `SaveSectionTemplateDialogViewModel : EDViewModel` s `Name` property, `OnOk()` override (validace + close), používá `OkCommand` z base
- ✅ **`MainWindowViewModel.SaveSectionAsTemplate(Section, Window?)` → `SaveSectionTemplate(Section, string name)`** — odstraněno ~60 řádků imperativního WPF kódu (StackPanel, TextBox, Button, Click handlery); zůstala jen save logika (~10 řádků)
- ✅ **`MainWindow.xaml.cs.SaveSectionAsTemplate`** — nyní vytváří `SaveSectionTemplateDialog` + VM a na OK volá `_vm.SaveSectionTemplate(...)`; dialog konstrukce patří do code-behind (R4: VM bez WPF)
- 📋 **Zbytek `using System.Windows;` v MainWindowViewModel.cs** zachován pro `CommandManager.InvalidateRequerySuggested` (WPF command infra), YesNoCancel TODO (H-7), `Clipboard.SetText` a `ParameterTemplateSelector` class — žádné další inline dialog konstrukce

---

## 2026-04-21 — Code Hygiene: Extract TemplateTreePanel (H-8)

- ✅ **Nový UserControl `Controls/TemplateTreePanel/`** — XAML (DockPanel s hlavičkovým textem + TreeView) + code-behind (`Rebuild()`, 3 event handlery, `CreateElementNode`, `FindItemByTag`); ~160 řádků
- ✅ **API** — `SetContext(vm, openElementProperties, openSectionProperties, afterSelectionChanged)` + `Rebuild()`; pattern identický s `FormatToolbar`/`PropertiesPanel` (imperativní, bez samostatného VM — strom je čistě vizuální projekce `DesignerState`)
- ✅ **`MainWindow.xaml`** — inline TreeView + header Border nahrazeny `<controls:TemplateTreePanel x:Name="TreePanel"/>`
- ✅ **`MainWindow.xaml.cs`** — odstraněno ~150 řádků (`RebuildTemplateTree`, `CreateElementTreeNode`, `FindTreeItemByTag`, `GetTreeElementDetails`, `TemplateTree_SelectedItemChanged`, `TemplateTree_MouseDoubleClick`, `TemplateTree_PreviewKeyDown`, `_suppressTreeSelection`); `RefreshTreeIfVisible()` + `LeftTabControl_SelectionChanged` delegují na `TreePanel.Rebuild()`
- ✅ **Smazán orphan `Windows/TemplateTreeDialog/`** — nikdy se neinstanciovalo, dead code z doby, kdy strom byl modální okno
- 📋 **Proč UserControl, ne Window** — strom je tab content v levém panelu, ne dialog; kdyby někdy byla potřeba i samostatného okna, stačí UserControl hostovat v Window

---

## 2026-04-21 — Code Hygiene: MessageBox.Show → IPopupDisplayer (H-7)

- ✅ **`AiTemplateAssistantDialogViewModel`** — 5× `MessageBox.Show` → 4× `PopupDisplayer.Info` + 1× `PopupDisplayer.Error(ex)`; odstraněn `using System.Windows;`
- ✅ **`TableEditorDialogViewModel`** — 2× `MessageBox.Show` → 2× `PopupDisplayer.Info`
- ✅ **`ElementPropertiesWindowViewModel`** — 3× `MessageBox.Show` → 1× `Error(ex)` + 1× `Confirm(...)` + 1× `Info(...)`
- ✅ **`MainWindowViewModel`** — 7× `MessageBox.Show` → `PopupDisplayer.Confirm/Info/Error`; 1× kept s TODO komentářem (YesNoCancel v `ConfirmUnsavedChanges` nemá IPopupDisplayer ekvivalent — čeká na rozšíření EDControls)
- ✅ **`PdfImportDialogViewModel`** — 4× `Action<string,string>?` callback → `PopupDisplayer` přímo; odstraněny 3 property + 3 wire-up řádky v code-behind (`RequestShowError/Warning/Info`)
- 📋 **Design rozhodnutí** — `Warning` → `Info` (IPopupDisplayer nemá Warning — WPF rozdíl byl jen ikona); `YesNoCancel` → kept as MessageBox s TODO (jedna jediná výjimka v celé code base)
- 📋 **Proč teď** — rule R4: VM nesmí přímo znát WPF; rule R2: error handling přes `IPopupDisplayer`; toto byla poslední výjimka

---

## 2026-04-21 — Code Hygiene: checklist pro nové elementy (H-6)

- ✅ **Nový soubor `.claude/rules/new-element-checklist.md`** — autoritativní 10-kroková reference přidání nového elementu: třída + rendering, `ElementTypes` konstanta, `JsonDerivedType`, `MainWindowViewModel` 2 switches, toolbar menu, paleta, dynamická výška (`SkiaHeightCalculator`), `TemplateElement.GetSummary` pro strom, dokumentace; Glob `dotnet/Models/Elements/**` → automaticky načteno při práci na elementech
- ✅ **Tabulka dotčených souborů** — 10 bodů, jasně označeno co je povinné a co volitelné
- ✅ **Reference implementace** — `BarcodeElement` (minimální), `TextElement` (TextStyle + dynamická výška), `ScheduleElementBase`-derived (komplexní s cache)
- ✅ **`mvvm-patterns.md`** — 8-kroková sekce „Jak přidat nový typ elementu" zkrácena na pointer do checklistu (eliminace duplikátní dokumentace)
- 📋 **Proč teď** — během H-3 / H-4 se ukázalo, že checklist je rozesetý: `mvvm-patterns.md` má 8 kroků, ale nepokrývá `GetSummary`, `ApplyScale`, `SkiaHeightCalculator`, `CopyBaseTo`, paletu — vše bylo nutné dohledat v kódu

---

## 2026-04-21 — Code Hygiene: silent catch → TryParse (H-5)

- ✅ **`PropertiesPanel.xaml.cs` color picker seed** — prázdný `catch { /* ignore */ }` kolem `SKColor.Parse(cd.Get())` nahrazen za `SKColor.TryParse(...)`; stejné chování (dialog s defaultní barvou při nevalidním vstupu), ale bez try/catch jako control flow
- ℹ️ **TODO komentář** (ColorDialog minimalizuje Properties window) ponechán — patří do B2 (vlastní WPF color picker)

---

## 2026-04-21 — Code Hygiene: JSON wire format verify (H-4)

- ✅ **Průzkum** — `CacheAgendaProvider.cs` (C#) i `AgendaAPI.cls` + `ZastavkoveJrMhdAPI.cls` + `TemplateDesignerFAKTURY.cls` (Caché) používají **100% čistý JSON** wire formát: vše je `obj.%ToJSON()` → `JsonNode.Parse` / `JsonSerializer.Deserialize`. Žádné CVar tuple parsing, žádné `$lb()` na wire. `$lb()` výskyty jsou pouze v interních working globals (`^||TiskMHD(...)` 6-fázové intermediate data)
- ✅ **Kód se nemění** — pravidlo R3 je už dodržené
- ⚠️ **Oprava dokumentace** — `cache-api.md` + `AgendaAPI.cls` docstring u `GetData` popisovaly zastaralý formát (`OUT[1]` skaláry, `OUT[2..]` řádky s klíčem `collection`). Reálný stav: každý `OUT[n]` = jedna kompletní samostatná stránka ve formátu `GetMockData` (`{ values, rows }`)
- ✅ **Nová sekce "Wire formát pravidlo (R3)"** v `cache-api.md` — explicitní kontrakt pro příští agendy: čistý JSON, žádný hybrid, `$lb()` jen pro working globals

---

## 2026-04-21 — Code Hygiene: `ElementTypes` konstanty (H-3)

- ✅ **Nový soubor `Models/ElementTypes.cs`** — 9 `const string` konstant pro identifikátory typů elementů (`Text`, `Rectangle`, `Line`, `Image`, `Table`, `Barcode`, `StopList`, `DepartureGrid`, `NotesLegend`); jedna autoritativní definice pro JSON wire formát
- ✅ **`TemplateElement.cs`** — všech 9 `[JsonDerivedType(...)]` atributů odkazuje na `ElementTypes.X` (C# const expression je validní v atributu)
- ✅ **`MainWindowViewModel.GetDefaultElementSize` + `CreateElementByType`** — switch expressions + fallback `?? "TextElement"` → konstanty
- ✅ **`MainWindow.xaml.cs`** — 9 toolbar itemů + ghost ruler fallback → konstanty
- ⚠️ **XAML `Tag="TextElement"`** zůstává literál — `{x:Static}` binding v `Tag` by přidal verbose `xmlns` bez reálného benefitu; drift odchytne první switch výraz
- ⚠️ **`AnthropicApiClient.cs` JSON ukázky** zůstávají literály — jsou to prompt texty dokumentující wire formát pro AI model, ne code comparisons
- 📋 **Směrem dopředu** — nový element = přidat konstantu do `ElementTypes`, použít ji v JsonDerivedType atributu a ve factory; Wave 4 Caché migrace bude mapovat tyto konstanty na Caché form class names

---

## 2026-04-21 — Code Hygiene: `async void` → `async Task` + AsyncRelayCommand (H-2)

- ✅ **EDControls `AsyncRelayCommand`** — nová třída vedle `RelayCommand`, async ekvivalent; chytá výjimky přes `IPopupDisplayer.Error(ex)` (pravidlo R2), `CanExecute` vrací false během běhu → prevence double-click
- ✅ **EDControls `EDViewModel.CommandAsync(Func<Task>)`** — helper pro async handlery; samostatný název kvůli overload resolution (žádný dopad na existující `Command(Action)` volání)
- ✅ **`MainWindowViewModel`** — `OpenTemplateAsync` / `SaveTemplate` / `SaveAsTemplate` převedeny na `async Task`; commandy jdou přes `CommandAsync(...)`; property typ `AsyncRelayCommand`
- ✅ **Bonus fix `SaveTemplate` → `SaveAsTemplate`** — `await` místo fire-and-forget volání (předtím asymptomatická race condition: SaveTemplate se vrátil dřív než SaveAs dokončil)
- ✅ **`AiTemplateAssistantDialogViewModel.GenerateCommand`** — bonus: byl zamaskovaný `async void` přes lambdu `Command(async () => await GenerateAsync())`; teď `CommandAsync(GenerateAsync)` → výjimky už nepadají na UI thread
- ✅ **Pravidlo R1** — připraveno pro Wave 1–3 (Caché async volání masivně), pattern jednou zavedený místo retrofitu

---

## 2026-04-21 — Code Hygiene: smazat `ShowZkraceneOznaceniLinky` (H-1)

- ✅ **`DepartureGridElement`** — smazána ghost property `ShowZkraceneOznaceniLinky` (declaration, PropertyDescriptor, Clone); nikde se nečetla, klamala uživatele v panelu vlastností
- ✅ **Datová property `ZkraceneOznaceniLinky`** na `OdjezdData` zůstává (reálně se plní z Caché `ZKRACENE_OZNACENI_LINKY`)
- ✅ **Existující šablony** (`kv.json`, `kv_zaklad.json`, `test_cb_na_sirku.json`) — ghost klíč `showZkraceneOznaceniLinky` v JSON se deserializerem tiše ignoruje, migrace nepotřeba
- ✅ **Docs** — odstraněno ze seznamu properties v `schedule-elements-architecture.md`

---

## 2026-04-20 — Inline validace v PropertiesPanel (B0-11)

- ✅ Chybový stav pole: červený rámeček + tooltip s chybou místo TextBlocku pod polem
- ✅ `ApplyValidation(TextBox, string?)` sdílený helper — `ClearValue(BorderBrushProperty)` pro reset na výchozí styl
- ✅ `AddStringRow`: live validace přes `Validate` callback (aktuálně VisibilityExpression)
- ✅ `AddTextWithPickerRow`: neznámé `{PLACEHOLDER}` — stejný vizuální vzor; `UpdateFieldValidation` přepracována na `TextBox`

---

## 2026-04-20 — Rotace elementů (B0-14–17)

- ✅ **`Rotation` property** na `TemplateElement` (float, stupně CW) + JSON serializace + `CopyBaseTo()` + descriptor "Rotace" v panelu vlastností
- ✅ **Render** — `DesignerCanvas` + `PreviewCanvas`: `canvas.Save/RotateDegrees(cx,cy)/Restore` obaluje `el.Render()` i `RenderBorderPass()` pro rotované elementy
- ✅ **Hit-testing** — `HitTestElement` transformuje testovací bod inverzní rotací před AABB testem (kliknutí na rotovaný element funguje správně)
- ✅ **Resize handles** zakázány pro rotované elementy (`GetHandlesForElement` → `yield break` při `Rotation != 0`); `DrawRotationBadge` — modrý badge s úhlem nad vybraným rotovaným elementem
- ✅ **Selekční rámeček** nakreslen rotovaně (canvas.Save/RotateDegrees/DrawRect/Restore) — odpovídá skutečnému tvaru elementu

---

## 2026-04-20 — Format mini-toolbar + Copy/Paste Style (B0-13, B0-12)

- ✅ **`FormatToolbar` UserControl** — nad properties panelem; zobrazí se pouze pro TextElement (Group "Font" + "Zarovnání")
- ✅ **B I U S** — toggle tlačítka pro Tučné/Kurzíva/Podtržení/Přeškrtnutí; napojeno na `BoolPropertyDescriptor` přes `SetPropertyCommand`
- ✅ **L C R J** — vodorovné zarovnání (Left/Center/Right/Justify); mutually exclusive, stav synced přes `EnumPropertyDescriptor`
- ✅ **▲ ▬ ▼** — svislé zarovnání (Top/Middle/Bottom)
- ✅ **`StyleClipboard` record** v `DesignerState` — drží `TextStyle?`, `BorderColor`, `BorderWidth`, `BackgroundColor`
- ✅ **`CopyStyle()`** (`Ctrl+Shift+C`) — zachytí styl prvního vybraného elementu; `TextStyle.Clone()` pro TextElement
- ✅ **`PasteStyle()`** (`Ctrl+Shift+V`) — aplikuje na všechny vybrané elementy; `SetPropertyCommand` jen pro změněné property; obaleno `CompositeCommand` pro jeden Undo krok
- ✅ **Toolbar a properties panel** — oba refreshovány přes existující `RequestPropertiesPanelUpdate` pipeline; undo/redo udržuje sync automaticky

---

## 2026-04-20 — Normalizovat pozice (B0-6)

- ✅ **`MmMath.Snap(mm, step)`** — přichytí hodnotu na nejbližší násobek kroku (0.1/0.5/1/2/5mm)
- ✅ **`NormalizePositionsDialog`** — ComboBox s krokem, live preview "Bude upraveno: X elementů", OK/Storno
- ✅ **`NormalizePositions(elements, step)`** — `ResizeElementCommand` per změněný element, obaleno `CompositeCommand`; no-op guard (commands.Count == 0)
- ✅ **`Ctrl+Shift+N`** + menu Elementy → "Normalizovat pozice…"; bez výběru = všechny elementy šablony

---

## 2026-04-20 — Grid size v UI (B0-9)

- ✅ **`DesignerState.GridSize`** — property (dříve hardcoded 5mm); výchozí hodnota 5mm zachována
- ✅ **`MainWindowViewModel.GridSize`** — bindovatelná property; nastavení propaguje do `DesignerState` a invaliduje canvas
- ✅ **ComboBox v toolbaru** — možnosti 1/2/5/10mm vedle snap tlačítek; výchozí 5mm (index 2)
- ✅ **`CmbGridSize_SelectionChanged`** — čte `Tag` ComboBoxItem a předá `_vm.GridSize`

---

## 2026-04-20 — Precision fix, strikethrough, barcode typy, konsolidace palety

**Precision fix (B0-4/B0-5/B0-8):**
- ✅ **`MmMath.Round(float mm)`** — nový helper, zaokrouhlí na 0.1mm (`MidpointRounding.AwayFromZero`); nullable overload
- ✅ **`EndResize`** — zaokrouhlení X/Y/Width/Height těsně před commitem (drag zůstane plynulý)
- ✅ **`TemplateElement.ApplyScale()`** — zaokrouhlení po každém násobení scale
- ✅ **`ScaleTemplateCommand.Apply()`** — zaokrouhlení PageSize a sekcí po škálování
- ✅ **`DistributeH/V`** — gap zaokrouhlen na 0.1mm, poslední element drží původní pozici (absorbuje zbytek)
- ✅ **Properties panel** — mm pole zaokrouhlena na commit (`fd.Unit == "mm"`); X/Y/Šířka/Výška zobrazují `"0.0#"` formát

---

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
