# Legacy EDISON komponenty — průzkum a porovnání s EDControls

> Reference dokument z průzkumu legacy `C:\DEV\EDISON\` repozitáře (2026-05-15). Slouží jako baseline pro plánování vývoje `EDControls` — co převzít, co dělat jinak, co nepotřebujeme.

## Mapa legacy ekosystému

```
C:\DEV\EDISON\
├── EDFORMS/                    ~24 000 LOC, .NET 4.8 WinForms, „EDFORMS.dll"
│   ├── ctrBaseInputGrid.cs      8 144 LOC ← BIG = formulářový vstup
│   ├── BaseDataGrid.cs          4 494 LOC ← BDG = editovatelná tabulka (C1FlexGrid)
│   ├── ctrBaseItemList.cs       2 113 LOC ← BIL = seznam s CRUD
│   ├── clsForms.cs              5 180 LOC ← BaseForm, IBaseForm, helpers
│   ├── clsDocument.cs           1 989 LOC ← BaseDocument framework
│   ├── frmBigPackageAdaptor.cs    574 LOC ← standalone obal BIG
│   ├── frmBdgPackageAdaptor.cs    504 LOC ← standalone obal BDG
│   ├── frmBilPackageAdaptor.cs    642 LOC ← standalone obal BIL
│   ├── frmCodebookList.cs               ← generický codebook picker
│   ├── DashPanel.cs               306 LOC ← CefSharp dashboard
│   ├── DateTimePickerYearMonth.cs       ← rok-měsíc výběr
│   ├── DialogCalendar.cs                ← kalendářový dialog
│   ├── InfoPanel.cs                     ← info panel
│   ├── formURLShow.cs                   ← PDF/URL viewer
│   └── CACHE/ALVA/EDFORMS/CONTROLS/     ← 21 Caché tříd (BIG.*, BIL.*, BDG.*)
│
├── EDComponents/                VB.NET, ~60 atomických UI kontrolek
│   ├── EDButton, EDComboBox, EDComboBoxEx, EDCheckedComboBox, EDCheckedListBox
│   ├── EDDatum, EDDatumEx, EDDatumOdDo, EDCalendarMonth, EDCalendarYear, EDCas
│   ├── EDDouble, EDDoubleOdDo
│   ├── EDFilter (FilterController pro BIL/BDG)
│   └── EDFlexGrid, EDFlexGrid2 (custom grid komponenta)
│
├── EDComponents2/               C#, mini-utility kontrolky
│   ├── EDAkceProbiha (loading indicator)
│   ├── EDStavovyRadek (status bar)
│   ├── EDInputBox (input popup)
│   ├── EDMessageBox + EDMessageBoxTimer
│   ├── EDScheduler
│   └── EDWait, EDWaitStatic, EDWaitWork
│
├── EDREPORTS/                   VB.NET, reporting + codebook API
│   ├── clsApi.vb                ← CodebookTypeEnum (~100+ číselníků)
│   ├── ctrInputItems.vb         ← input items komponenta
│   ├── clsExport.vb             ← export logika
│   ├── clsOpenXml.vb            ← Excel export
│   └── CACHE/ALVA/EDREPORTS/MLAPI.cls  ← LoadCodebook dispatcher
│
├── MajorAPI/                    C#, generic dialogy + helpery
│   ├── frmCodebookItem.cs       ← single codebook item edit
│   ├── frmCodebookList.cs       ← generický codebook picker dialog
│   ├── frmBaseList.cs           ← bázový list formulář
│   └── EDFlexGridAdaptor/       ← adaptor pro EDFlexGrid
│
└── BaseApi/                     C#, centrální helpery
    └── BaseApi.Style.cs         ← centrální Color/Style API
```

---

## 1. BIG — BaseInputGrid (formulářový vstup)

**Soubor:** `EDFORMS/ctrBaseInputGrid.cs` (8 144 LOC) | **Caché:** `ALVA.EDFORMS.CONTROLS.BIG.*` | **Typ:** WinForms UserControl

**Účel:** Hierarchický vstupní grid pro strukturované zadávání dat. Položky seskupené do skupin, každá má název, hodnotu, status, nápovědu.

### Aktivace (3 módy)

| Mód | Metoda | Popis |
|---|---|---|
| **Package** | `ActivateComponent(args)` s `PackageActionId` | Načte z Caché přes RPC |
| **Groups** (lokální) | `ActivateComponent(List<BaseInputGroup>)` | Předané skupiny |
| **Adaptor** (.NET) | `ActivateComponent(object)` | Reflexe nad .NET objektem |

### Datové typy (`$$$BigItemDataType*`)

| ID | Typ | Poznámka |
|---|---|---|
| 1 | Text | DenyWhiteSpaces option |
| 2 | Date | Kalendář picker, dd.MM.yyyy |
| 3 | Integer | Min/Max validace |
| 4 | Decimal | Format string, InvariantCulture |
| 5 | Boolean | Checkbox |
| 6 | List | ComboBox s AddListItem() |
| 7 | Codebook | CodebookType + EDReports picker |

**Plus .NET-only typy (rozšířené):** Password, Období/ObdobiR (rok-měsíc), DateInterval, Button, DialogDirectory/File/ServerDirectory/ServerFile/Color/Font/FileSave, GPSCords, Duration

### Klíčové vlastnosti `BIG.Item` (~20)

**Identifikace:** GroupId, GroupName, GroupExpanded, GroupBackColor/ForeColor, Id, Name, DataType

**Validace/chování:** Required, AllowEdit, ValidationRequired, **Hidden**, MultiSelect, DenyNullInput, DenyEmptySelection, DenyWhiteSpaces, DisplayAllValues, CodebookSelectionOnly, AllowCodebookAdd, ForceInput, ForceTemplate, Emphasized

**Formátování:** Format, MinValue, MaxValue, Information, HelpText, StartingEditMode, AltValidationKey

**Parametry:** AddParam, UpdateParam, GetParam (pro Codebook filtrování)

### Persist user state (KLÍČOVÉ!)

**Caché třída `ALVA.EDFORMS.CONTROLS.BIG.UserData`** — generic per-user persist:

```objectscript
ClassMethod Save(ByRef sys As %String) {
    // IN["formName"] = forma; IN["actionId"] = akce; IN["item",id,"hidden"] = 1/0
    k ^ALVA.EDFORMS.BIG.UserData(IdFa, userID, formName, actionId)
    m ^ALVA.EDFORMS.BIG.UserData(IdFa, userID, formName, actionId) = ^||ML("IN")
}

ClassMethod Load(ByRef sys As %String) {
    m ^||ML("OUT") = ^ALVA.EDFORMS.BIG.UserData(IdFa, userID, formName, actionId)
}
```

**Pattern:** mass-merge celého IN stromu do globu pod klíčem `(IdFa × userID × formName × actionId)`. **Generický blob storage** — Caché nepotřebuje znát strukturu, jen ukládá/načítá strom. Aktuálně reálně ukládá jen `item.hidden`, ale pattern by zvládl víc.

### Validace flow

1. User mění hodnotu → `OnValidateInput` event
2. BIG serializuje stav → Caché `ValidateItems` (Init/Change/BeforeSave trigger)
3. Caché vrátí updatované items se stavem (Ok/Warning/Error/Information)
4. BIG aktualizuje grid — ikony, zprávy, editovatelnost

---

## 2. BIL — BaseItemList (seznam s CRUD)

**Soubor:** `EDFORMS/ctrBaseItemList.cs` (2 113 LOC) | **Caché:** `ALVA.EDFORMS.CONTROLS.BIL.*` | **Typ:** WinForms UserControl

**Účel:** Multi-sloupcový seznam s filtrováním, řazením, **multi-select** a CRUD operacemi. Read-only buňky, edit přes BIG dialog.

### Aktivace (2 módy)

| Mód | Popis |
|---|---|
| Package (Caché) | `ActivateComponent(string package, args)` — načte z Caché |
| Local data | `ActivateComponent(ComponentData data, args)` — předané |

### ActivateComponentArgs (klíčové)

| Property | Default | Popis |
|---|---|---|
| `FormMode` | List | List / View / Browse / Selection / SelectionAdd / SelectionAddEditDel |
| `ListMode` | mSingleSelect | **mSingleSelect / mCheckSelect** (multi-select s checkboxy!) |
| `AllowSorting` | true | Řazení klikem |
| `AllowLoadUserSort` | true | **Persist user sort** |
| `AllowEmptySelection` | true | Prázdný výběr |
| `AllowCellMerging` | false | Sloučení buněk |
| `AllowWordWrapping` | false | Zalamování |
| `FrozenCols` | 0 | Připnuté sloupce vlevo |
| `RecordsVisible/Count` | false / -1 | Limit záznamů |
| `DisabledButtons` | None | Add / Edit / Delete (flags) |
| `SelectedRowsId` | empty | Předvybrané řádky |
| `ContextMenu` | null | Custom context menu |
| `ReportId/Name` | empty | Report kontext |

### Datové typy

| ID | Typ | Default zarovnání |
|---|---|---|
| 1 | Text | Left |
| 2 | Date | Right |
| 3 | Integer | Right |
| 4 | Decimal | Right |
| 5 | Boolean | Center |
| 6 | List | Left |
| 7 | Codebook | Left |
| 8 | HHMM | Right |
| 105 | Color (R;G;B) | — |
| 201 | HyperLinkString | Left (clickable) |
| 202 | HyperLinkInteger | Right (clickable) |

### Wire formát (CList delimited!)

**Sloupce:**
```
col~<id>~<name>~<dataType>~<codebookType>~<editable>~<visible>~<wordWrap>~<format>~<textAlign>
colCombo~<key>~<value>    // pro List sloupce
```

**Řádky:**
```
row~<id>~<name>~<state>~<message>~<foreColor>~<stateFlags>~[key]
cell~<id>~<name>~<colId>~<colSpan>~<toolTipText>~[<valueData>~<valueName>~<valueKey>]
value~<data>~<name>~<key>    // další hodnoty multi-select
title~<text>                  // titulek gridu
```

**Pozn.:** Wire delimited `~` — křehčí než JSON (špatně escapuje znaky, brittle parser).

### CRUD tlačítka (`ButtonTypeEnum` [Flags])

- **Add** = 1 — otevře BIG dialog s prázdnou položkou
- **Edit** = 2 — otevře BIG dialog s vybranou položkou
- **Delete** = 4 — volá `DeleteItems` v Caché

Viditelnost závisí na FormMode (List = všechna 3, Selection = žádná).

### Filtry (FilterController)

```
filter~sys~RecordsCount~<count>           // limit záznamů
filter~form~<filterId>~<itemId>           // formulářový filtr
```

Předává se do Caché `LoadItems`, projektová implementace filtr aplikuje v `$order` cyklu.

---

## 3. BDG — BaseDataGrid (editovatelná tabulka)

**Soubor:** `EDFORMS/BaseDataGrid.cs` (3 333 LOC) + `BaseDataGridClasses.cs` (1 161 LOC) | **Caché:** libovolná třída s Load/Validate/Save/Delete | **Typ:** WinForms UserControl na C1FlexGrid (komerční)

**Účel:** Editovatelná tabulka s 15 datovými typy, inline editace, validace, Excel export, AI asistent.

### Aktivace — `BaseDataGridActivationInput`

```csharp
new BaseDataGridActivationInput {
    MLRest = mlRest,                    // POVINNÉ
    CacheClass = "ALVA.Projekt.formMuj",
    ItemId = "123",                     // kontext
    ActionId = "mojeAkce",              // pro persist preferenci
    Params = new CVar(),
    Title = "Tabulka",
    HideStateColumn = false,
    HideIndexColumn = false,
    FrozenColumns = 0,
    AllowAddNew = false,                // Tab na konci → new row
    CheckSelect = false,                // checkbox sloupec
    AllowFiltering = false,
    AllowSorting = false,
    AllowColumnsOptions = false,        // persist menu
    AllowSearch = false,                // search box
    AutoValidateRequiredColumns = true,
    AutoFitColumns = false,
    AIEnabled = false                   // AI asistent v toolbaru
};
```

### Datové typy (15 typů — nejširší v legacy)

| ID | Typ | Zkratky |
|---|---|---|
| 1 | Text | — |
| 2 | Date (dd.MM.yyyy) | "d" → dnes |
| 3 | Integer | — |
| 4 | Decimal (#,##0.00) | — |
| 5 | Boolean (Checkbox) | — |
| 6 | List (ComboBox) | — |
| 7 | Codebook | EDReports picker |
| 8 | Time (HH:mm) | "d"/"t" → teď |
| 9 | DateRange (dd.MM.yyyy-dd.MM.yyyy) | "d" → dnes-dnes |
| 10 | **Template** (BIL/BIG/BDG dialog v buňce) | — |
| 11 | DirectoryDialog | — |
| 12 | Color (R;G;B) | — |
| 13 | Duration (H:mm) | "125" → 2:05 |
| 14 | FileDialog | — |
| 15 | Button (read-only) | — |

### CList definice sloupce (16 pozic)

```
$lb(colId, caption, dataType, required, editable, validated,
    codebookType, multiselect, hidden, alignment,
    displayAllValues, valueSeparator, valueDisplayMode,
    inputValidationDataType, format, sortMode)
```

### Persist column preferences

**Caché třída `ALVA.EDFORMS.CONTROLS.BDG.ColumnsOptions`:**
```
^[dataDB]ALVA.EDFORMS.BDG(IdFa, "ColumnsOptions", userID, form, actionId) = blob
```

Aktivní pouze pokud `AllowColumnsOptions = true`.

### Speciální funkce

- **Excel export** — DocumentFormat.OpenXml, viditelné sloupce + data, auto-sizing
- **AI asistent** — `IEDAIChatHost`, `IEDAIToolProvider`, `IEDAIContextProvider`; 15+ AI nástrojů (read_cell, update_cell, add_row, remove_row, validate)
- **Search** — case-insensitive substring napříč sloupci
- **Sort** — type-aware komparátory (Czech date, time, duration)

### Validation reasons (`BaseDataGridValidationReason`)

| Hodnota | Důvod | Kdy |
|---|---|---|
| 0 | None | Bez specifické |
| 1 | UserEdit | User změnil buňku |
| 2 | AfterLoad | Po načtení gridu |
| 3 | BeforeSave | Před uložením |
| 4 | AddedRow | Přidán řádek |
| 5 | ButtonClick | Klik na Button buňku |
| 6 | DuplicatedRow | Duplikovaný řádek |

---

## 4. Codebook (Číselníky) — centrální katalog

### Concept

Legacy má **~100+ číselníků** s unique int ID (1001-99999), seskupené po doménách:

| Range | Doména | Příklady |
|---|---|---|
| 1001-1099 | Obecné | Účty, Strediska, Zakazky, Vozidla, Banky, Meny |
| 1101-1199 | ES (účetnictví) | Účetní případy, Agendy, Činnosti, Daňové kódy |
| 2001-2099 | HIM (majetek) | Druhy/Třídy majetku, Karty, Odpisy |
| 3001-3099 | Sklady | Sklady, Materiály, MJ |
| 4001-... | Fakturace | Řady, Doklady, Zálohy, Měny |
| 5001-... | Mzdy | Střediska, Profese, Pojišťovny |
| 8001-... | ISY | Linky, Zóny, Dopravci |
| 9001-... | MHD | Cíle, Pracoviště, Skupiny linek, **Zastávky** |
| 99001-... | MTZ | Materiály, MJ |

### Architektura

```
EDREPORTS.API.CodebookTypeEnum (.NET)
└── Definuje 100+ codebook typů

ALVA.EDREPORTS.MLAPI.LoadCodebook(IdFa, codebookId) (Caché)
└── Generic dispatcher s velkým `elseif` switchem
    └── Pro každé ID deleguje na konkrétní agendu API
        (např. CodebookTypeEnum.UcetniPripady → ALVA.ESLT.MLAPI.LoadSezUcPr)
└── Output: ^||ML(...) = { id → displayName } mapping

MajorAPI.frmCodebookList (.NET WinForms)
└── Generický picker UserControl/Form
    - Input: CodebookTypeEnum + optional filter
    - Features: search, filter, single/multi-select, "Add new" (volitelně)
    - Output: Dictionary<key, displayName> vybraných

Integrace s formulářovými controly:
- BIG.Item / BIL.ListColumn / BDG.Column má property CodebookType
- UI vykreslí lookup tlačítko v buňce/řádku
- Klik → frmCodebookList(CodebookType) → výsledek zpět do hodnoty
- Konfigurace: MultiSelect, CodebookSelectionOnly, AllowCodebookAdd
```

### Pattern stručně

**„Field má typ + CodebookType, framework si zajistí zbytek"** — UI, picker dialog, načtení dat, parsování výsledku.

---

## 5. Styling — kde a jak

### Legacy přístup: centralizovaná static class

**Soubor:** `BaseApi/BaseApi/BaseApi.Style.cs`

```csharp
namespace ALVA.BASE.API {
    public static partial class BaseApi {
        public static class Style {
            public static class Colors {
                public static readonly Color FrozenColor      = Color.FromArgb(240, 240, 255);
                public static readonly Color ReadOnlyBackColor = Color.FromArgb(234, 234, 234);
                public static readonly Color ReadOnlyForeColor = Color.FromArgb(120, 120, 120);
                public static readonly Color ErrorLightColor   = ControlPaint.Light(Color.Red, 1.5f);
                public static readonly Color ErrorDarkColor    = Color.DarkRed;
                public static readonly Color WarningColor      = ControlPaint.Light(Color.DarkOrange, 1.5f);
            }

            public static Color GetColor(string color, Color defaultIfNull = default)
            public static string ColorToString(Color color)  // "R;G;B" format
            public static string ColorToHex(Color color)
            public static Color GetPrimaryColor() => Color.DarkSlateBlue;
            public static Color GetDarkerColor(Color color, int diff = ...)
        }
    }
}
```

### Charakteristiky

- **Centrální static class** ve sdíleném BaseApi projektu (NE per-komponenta)
- **Pevně dané barvy** (ne theme system, ne dynamicky vyměnitelné)
- **Konsensuální paleta:** DarkSlateBlue primary, šedá read-only, červená error, oranžová warning, jemně modrá frozen
- **`"R;G;B"` jako wire format** — Caché posílá barvu jako `"255;128;0"` string, `Style.GetColor()` parsuje
- **Žádné resource dictionary** — WinForms má vlastní vzor (statické konstanty + `Control.BackColor = Style.Colors.X`)

### Implikace pro EDControls

- **WPF má lepší pattern:** Resource Dictionary v `EDIcons.xaml` styl
- **EDControls aktuálně:** žádný centrální color resource dictionary (color hardcoded v jednotlivých XAML / SkiaSharp renderech)
- **Potenciální budoucí cesta:** vytvořit `EDControls/Themes/Base.xaml` s `EDColors.Primary`, `EDColors.ReadOnlyBack`, atd. — analogie BaseApi.Style, jen WPF Resource style
- **Aktuálně NETŘEBA řešit** pro v1.0 demo — vystačíme s hardcoded barvami; jen je dobré vědět, že legacy má jeden centrální zdroj a my můžeme udělat lépe (theme switch v budoucnu)

---

## 6. Architektonické rozdíly: Legacy ↔ EDControls

| Aspekt | Legacy | EDControls |
|---|---|---|
| **UI framework** | WinForms + C1FlexGrid (komerční) | WPF + nativní controly |
| **MVVM** | ❌ code-behind heavy | ✅ EDViewModel base + bindings |
| **Async** | ❌ Synchronní + overlay panel | ✅ async/await + AsyncRelayCommand (R1) |
| **Error reporting** | Status sloupec + ikony per item | IPopupDisplayer + ValidationError (R2) |
| **Wire format** | CList (`$lb` poziční + `~` delimited) | JSON (samo-popisný, versionable) (R3) |
| **Testovatelnost** | ❌ tightly coupled to WinForms | ✅ Controller pattern bez WPF |
| **DI** | ❌ tightly coupled na BaseForm | ✅ EDServices.Current, IMLRest |
| **Persist user state** | ✅ generic UserData blob storage | ❌ chybí (musíme dotáhnout) |
| **Multi-select v listu** | ✅ EFlexGrid2.mCheckSelect | ❌ chybí |
| **CRUD v listu** | ✅ Add/Edit/Delete tlačítka | ❌ read-only |
| **Filter UI v listu** | ✅ FilterController | ❌ chybí |
| **Codebook** | ✅ ~100+ typů, generic picker | ❌ deklarováno v EDList, neimpl |
| **Lokální groups mode BIG** | ✅ ano | ❌ jen Package mode |
| **LOC** | ~24 000 (EDFORMS) | ~2 100 (EDInputForm + EDList + base) |

---

## 7. Současný stav EDControls

### EDInputForm (analogie BIG, ~1 500 LOC)

| Feature | Stav |
|---|---|
| Caché Package mode | ✅ |
| Lokální Groups mode | ❌ |
| Adaptor (reflexe) mode | ❌ |
| Datové typy | Text, Number, Date, Decimal, Bool, Button, List, File |
| Codebook field type | ❌ |
| Validation (Init/Change/BeforeSave triggers) | ✅ |
| Per-item ValidateOnChange | ✅ |
| Visibility (4 stavy: Visible/Collapsed/UserHidden/ProgrammerHidden) | ✅ jemnější než legacy |
| HelpText (Tooltip / Column) | ✅ |
| Klávesy (Enter/Tab next, Ctrl+Enter, Ctrl+číslo) | ✅ |
| Persist user state (UserData) | ❌ |
| Round-trip pattern (server keeps state) | ✅ |
| Testovatelný Controller | ✅ EDInputFormController bez WPF |

### EDList (analogie BIL, ~600 LOC)

| Feature | Stav |
|---|---|
| Caché-driven sloupce (InitializeList) | ✅ |
| Caché-driven data (LoadList) | ✅ |
| Datové typy | TEXT, NUMBER, DATE, DATETIME, BOOL, IMAGE (chybí), COLOR, HHMM, CODEBOOK (deklar., neimpl.) |
| Wire formát | **JSON** (lepší než legacy CList) |
| Single-select (SelectedRowId) | ✅ |
| Multi-select (checkbox sloupec) | ❌ |
| Sort (z WPF DataGridu) | ⚠️ základní |
| Persist sort | ❌ |
| Filter UI | ❌ |
| ColumnLayout persist (šířky + pořadí + visible) | ❌ deklarováno, neimpl. |
| CRUD tlačítka | ❌ |
| Search v DB (F3) | ❌ |
| Context menu | ❌ |
| isLink hyperlink cells | ✅ (lepší design než HyperLinkString/Integer typy) |
| Eventy (Selection, RowDouble, CellLink) | ✅ |

### Co dalšího EDControls má

- `EDViewModel` base s `Command(...)`, `CommandAsync(...)`, `RelayCommand`, `AsyncRelayCommand`
- `EDFooter`, `EDToolbar` — high-level UI komponenty
- `EDActionInProgress` — loading indicator (analogie legacy EDAkceProbiha)
- `EDIcons` — resource dictionary 33 Fluent UI System Icons
- `EDStrings` — sdílené texty tlačítek
- `WindowStatePersistence` — pozice/velikost/maximized state per (MLP × IDFa) přes legacy `ALVA.LIBRARY.Form.SaveParams`
- `IMLRest` + `MLRestAdapter` — abstrakce nad MLRest, sdílená přes EDServices

### Co EDControls **nemá** oproti legacy

- BDG (editovatelná tabulka) — záměrně, je to 4 494 LOC + C1FlexGrid (komerční licence)
- Atomické kontrolky (EDButton, EDCheckedComboBox, EDDatum...) — nahrazujeme nativními WPF
- EDFilter (komplexní filter controller)
- EDInputBox, EDMessageBox, EDScheduler, EDWait* — pokryto WPF `IPopupDisplayer`
- BaseDocument framework — nepotřebujeme pro TD scope
- PackageAdaptors (frmBig/Bdg/BilPackageAdaptor) — naše dialogy v WPF jsou už standalone
- Codebook centrální katalog — TD má jen 2-3 codebooky (agendy, linky, šablony), nevyplatí se overhead
- Centrální color/style class — místo toho mohou být WPF resource dictionaries (budoucnost)

---

## 8. Roadmap věci k převzetí (priorita)

### Tier 1 — Foundation (`UserData` persist API)

**Co převzít:** generic Save/Load pattern z `BIG.UserData.cls`

**Caché design:**
```objectscript
Class ALVA.EDControls.UserData [ Abstract ] {
    /// Save(scope, key) — uloží IN strom pod (IdFa × userID × scope × key)
    ClassMethod Save(ByRef sys As %String) As %Status
    /// Load(scope, key) — vrátí strom do OUT
    ClassMethod Load(ByRef sys As %String) As %Status
    /// Delete(scope, key) — smaže klíč
    ClassMethod Delete(ByRef sys As %String) As %Status
}
// Globál: ^ALVA.EDControls.UserData(IdFa, userID, scope, key, ...) = blob
```

**.NET design:**
```csharp
public interface IUserPrefsService {
    Task<JsonNode?> LoadAsync(string scope, string key);
    Task SaveAsync(string scope, string key, JsonNode data);
    Task DeleteAsync(string scope, string key);
}
```

**Konvence pro `scope`:**
- `"EDList"` — pro EDList persistované state (ColumnLayout, sort, ...)
- `"EDInputForm"` — pro EDInputForm persistované state (hidden items, ...)
- `"TD"` — pro TemplateDesigner-level prefs (LastAgenda, LastInstances, ...)

**Konvence pro `key`:** unikátní string identifikátor instance (např. `"TemplatePickerColumnLayout"`, `"InstancePickerSort"`)

### Tier 2 — EDList feature parity s BIL

| Krok | Co | Hodnota |
|---|---|---|
| L1 | Multi-select checkbox sloupec (`SelectionMode = Single \| Check` + `SelectedRowIds`) | ⭐⭐⭐⭐ |
| L2 | ColumnLayout persist (šířky + pořadí + visible) přes IUserPrefsService | ⭐⭐⭐⭐ |
| L3 | Sort by column + persist | ⭐⭐⭐ |
| L4 | Filter / search box | ⭐⭐⭐ |
| L5 | Migrate `CacheTemplatePickerDialog` na EDList (real-world test) | ⭐⭐⭐⭐ |
| L6 | Migrate `InstancePickerDialog` na EDList multi-select | ⭐⭐⭐⭐ |
| L7 | Image sloupec typ | ⭐ |
| L8 | CRUD tlačítka (volitelně otevírá EDInputForm) | ⭐⭐ |

### Tier 3 — EDInputForm feature parity s BIG (volitelné)

| Krok | Co | Hodnota |
|---|---|---|
| I1 | Lokální Groups mode (mimo Caché) | ⭐⭐⭐⭐ |
| I2 | UserData persist (hidden + last values) | ⭐⭐⭐ |
| I3 | Migrate `SaveToCacheDialog` + `ChangeAgendaDialog` na EDInputForm lokál | ⭐⭐⭐ |
| I4 | Codebook field type | ⭐⭐ |
| I5 | DateInterval, Color, Font, Password | ⭐ |

### Tier 4 — NE dělat (rozhodnutí)

| Co | Důvod |
|---|---|
| **BDG / EditableDataGrid** | 4 494 LOC, AI integrace, Excel export — overkill. TD má StopColumnsEditorDialog s vlastní DataGrid logikou, funguje. |
| **PackageAdaptors** | Legacy WinForms pattern — naše dialogy v WPF jsou už standalone. |
| **EDFilter v plné šíři** | Komplexní filter controller — pro TD stačí jednoduchý search box. |
| **BaseDocument framework** | Nemáme zákazníky pro „komplexní strukturované formuláře". |
| **EDInputBox/MessageBox/Scheduler** | WPF + `IPopupDisplayer` to pokrývá; Scheduler je vlastní doménová věc. |
| **Atomické kontrolky (EDButton, EDCheckedComboBox, ...)** | Nativní WPF kontrolky jsou plně dostatečné. |
| **Centrální color/style class** | Mohu udělat WPF Resource Dictionary later (budoucí), ne teď. |
| **Codebook centrální katalog** | TD má 2-3 codebooky — overhead se nevyplatí. |

---

## 9. Užitečné lokace pro budoucí průzkum

| Co najdeš | Cesta |
|---|---|
| BIG (BaseInputGrid) | `C:\DEV\EDISON\EDFORMS\ctrBaseInputGrid.cs` |
| BIL (BaseItemList) | `C:\DEV\EDISON\EDFORMS\ctrBaseItemList.cs` |
| BDG (BaseDataGrid) | `C:\DEV\EDISON\EDFORMS\BaseDataGrid.cs` |
| BIG UserData (persist vzor) | `C:\DEV\EDISON\EDFORMS\CACHE\ALVA\EDFORMS\CONTROLS\BIG\UserData.cls` |
| BDG ColumnsOptions (persist) | `C:\DEV\EDISON\EDFORMS\CACHE\ALVA\EDFORMS\CONTROLS\BDG\ColumnsOptions.cls` |
| CodebookTypeEnum (~100 codebooků) | `C:\DEV\EDISON\EDREPORTS\clsApi.vb:98-264` |
| LoadCodebook dispatcher | `C:\DEV\EDISON\EDREPORTS\CACHE\ALVA\EDREPORTS\MLAPI.cls` |
| frmCodebookList (generic picker) | `C:\DEV\EDISON\MajorAPI\frmCodebookList.cs` |
| Centrální style API | `C:\DEV\EDISON\BaseApi\BaseApi\BaseApi.Style.cs` |
| EDFORMS dokumentace | `C:\DEV\EDISON\EDFORMS\DOCS\00-..05-*.md` |
| EDFORMS CLAUDE.md | `C:\DEV\EDISON\EDFORMS\CLAUDE.md` |

---

## 10. Změny od průzkumu

- 2026-05-15 — vytvořeno z průzkumu `C:\DEV\EDISON\` pro plánování EDControls v1.0+
