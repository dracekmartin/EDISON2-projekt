# EDList — komponenta pro Caché-driven seznamy

> WPF komponenta vykreslující seznam dat načtený z Caché. Multi-select, persist sloupců, client-side search, hyperlink buňky.
> Analogie legacy BIL (`BaseItemList`) s modernějším wire formátem.

---

## Kdy EDList použít

✅ Pro tyto případy:
- Pickery / dialog s výběrem (jedna nebo více položek)
- Read-only data tabulky načtené z Caché
- Seznamy entit pro CRUD parent dialog (Open / Edit / Delete tlačítka v hostujícím okně)
- Vše kde data jdou z Caché a UI je read-only mřížka

❌ NE pro tyto případy:
- Editovatelná tabulka s inline editací buněk (na to nemáme — viz legacy BDG, nemigrujeme)
- Free-form canvas / draw (na to nemá smysl)
- Data co je čistě v paměti aplikace (`ObservableCollection` v VM stačí)

---

## Quickstart — minimální použití

### 1. Caché backing class

```objectscript
Include GENERAL

Class ALVA.MyProject.API.MyList Extends %RegisteredObject
{

ClassMethod InitializeList(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s list = ##class(ALVA.EDControls.EDList.List).%New()
        d list.AddColumn("id",   "ID",    "TEXT",   80,  "", 1)
        d list.AddColumn("name", "Název", "TEXT",  250,  "", 1)
        d list.AddColumn("date", "Datum", "DATE",  120,  "", 1)
        d list.WriteColumnsToOut()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

ClassMethod LoadList(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s list = ##class(ALVA.EDControls.EDList.List).%New()
        // iterovat data, AddRow per řádek
        s row = list.AddRow("1")
        d row.%Set("id",   "1")
        d row.%Set("name", "Položka A")
        d row.%Set("date", "2026-01-15")
        d list.WriteRowsToOut()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}

}
```

### 2. WPF okno

**XAML — konfigurace v atributech** (konstanty per dialog patří sem):
```xml
<Window x:Class="MyProject.MyDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:el="clr-namespace:EDControls.EDList;assembly=EDControls"
        Width="600" Height="400">
    <Grid Margin="12">
        <el:EDList x:Name="TheList"
                   CacheClass="ALVA.MyProject.API.MyList"
                   HostId="MyProject.MyList"
                   Title="Můj list"/>
    </Grid>
</Window>
```

**Code-behind — jen WPF wiring** (eventy, Loaded handler):
```csharp
public partial class MyDialog : Window
{
    public MyDialog(MyDialogViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();

        // Wire-up event handlerů (events nelze přímo v XAML jako Command)
        TheList.SelectionChanged += (_, id) => /* ... */;
        TheList.RowDoubleClicked += (_, id) => /* ... */;

        Loaded += (_, _) =>
        {
            TheList.InitializeList();
            TheList.LoadList();
        };
    }
}
```

**Pravidlo:** konfigurační konstanty (CacheClass, HostId, Title, SelectionMode) patří **do XAML**. Code-behind drží jen **wiring** (DataContext, event handlery, Loaded entry point, VM callbacks). Dynamický stav (hodnoty z filteru, výběr řádku, mock data) žije ve **ViewModelu**.

Tím máš funkční list s automatickým client-side search, persist šířky sloupců, sort by header click.

---

## Properties

### Konfigurace (pre-load)

| Property | Typ | Popis |
|---|---|---|
| `CacheClass` | `string` | FQN Caché třídy implementující contract — povinné. Třída musí mít metody `InitializeList(ByRef sys)` a `LoadList(ByRef sys)` |
| `HostId` | `string?` | Identifikátor pro persist sloupců přes `IUserPrefsService`. Convention: `"<Project>.<DialogName>"`, např. `"TemplateDesigner.TemplatePicker"`. Null/empty = persist vypnutý |
| `Title` | `string?` | Title nad listem. Null = bez title; SearchVisible se default vypne |
| `SearchText` | `string` | Aktuální hodnota search boxu (R/W). Změna trigger client-side filter |
| `SearchVisible` | `bool` | Viditelnost search boxu. Default: `true` pokud `Title` set, jinak `false` |
| `SelectionMode` | `EListSelectionMode` | `Single` (klasický single-select) nebo `Check` (multi-select s checkbox sloupcem). Default `Single` |

### Read-only state (po load)

| Property | Typ | Popis |
|---|---|---|
| `Columns` | `IReadOnlyList<ListColumn>?` | Načtené sloupce z Caché (po `InitializeList()`) |
| `Rows` | `IReadOnlyList<EDListRow>` | Načtené řádky (po `LoadList()`) |
| `SelectedRow` | `EDListRow?` | Aktuálně označený řádek (Single mode) nebo poslední klikatý (Check mode) |
| `SelectedRowId` | `string?` | ID označeného řádku (alias na `SelectedRow.Id`) |
| `SelectedRowIds` | `IReadOnlyList<string>` | V `Check` mode: zaškrtnuté řádky. V `Single` mode: `[SelectedRowId]` nebo prázdné |

---

## Metody

### `InitializeList()`
Volá Caché `InitializeList` na `CacheClass` → načte definici sloupců → vykreslí header.
**Volej v `Loaded` event dialogu.**

### `LoadList()` / `LoadList(IReadOnlyDictionary<string, string>? filter)`
Volá Caché `LoadList` → načte řádky. Volitelný `filter` se posílá pod sub-subscript `^||ML("IN", "filter", "klic") = "hodnota"` (filter konvence — viz [`01-wire-formats.md`](01-wire-formats.md#filter-konvence---kde-zijí-filter-hodnoty)). Server může (ale nemusí) filter aplikovat.

```csharp
TheList.LoadList();   // bez filtru
TheList.LoadList(new Dictionary<string, string> { ["agendaCode"] = "MHD" });
```

> **Poznámka k async:** `InitializeList()` i `LoadList()` jsou dnes **synchronní** — adekvátní pro typické TD listy (50–300ms load). Pro slow operace (1s+, velké agenda listy) přidáme `LoadListAsync()` jako paralelní async metodu, až bude reálný konzument. Viz R1 v TD roadmapě — sync je validní výjimka pro fast foundational komponenty.

### `SetChecked(string rowId, bool isChecked)`
V `Check` mode programaticky zaškrtne / odškrtne konkrétní řádek.

### `CheckAll()` / `UncheckAll()`
V `Check` mode zaškrtne / odškrtne všechny aktuálně viditelné řádky.

---

## Eventy

| Event | Signature | Kdy se vyvolá |
|---|---|---|
| `SelectionChanged` | `EventHandler<string?>` (předané `SelectedRowId`) | Po změně označeného řádku |
| `RowDoubleClicked` | `EventHandler<string>` (předané ID řádku) | Po dvojkliku na řádek |
| `CheckedRowsChanged` | `EventHandler<IReadOnlyList<string>>` (předané `SelectedRowIds`) | V `Check` mode po každé změně zaškrtnutí |
| `CellLinkClicked` | `EventHandler<(string RowId, string ColKey)>` | Po kliknutí na hyperlink buňku (sloupec s `isLink=true`) |

Příklad subscription v code-behind:

```csharp
TheList.SelectionChanged += (_, rowId) => _vm.OnRowSelected(rowId);
TheList.RowDoubleClicked += (_, rowId) => _vm.OpenCommand.Execute(rowId);
TheList.CheckedRowsChanged += (_, ids) => _vm.OnCheckedChanged(ids);
TheList.CellLinkClicked += (_, e) => _vm.OnLinkClicked(e.RowId, e.ColKey);
```

---

## Caché backing class contract

Třída ve FQN určená v `CacheClass` musí mít:

### `InitializeList(ByRef sys As %String) As %Status`

Definuje sloupce. Použije helper `ALVA.EDControls.EDList.List`:

```objectscript
ClassMethod InitializeList(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s list = ##class(ALVA.EDControls.EDList.List).%New()

        // AddColumn(key, label, type, width, align, sortable, format)
        d list.AddColumn("id",     "ID",        "TEXT",      80, "",  1)
        d list.AddColumn("name",   "Název",     "TEXT",     250, "",  1)
        d list.AddColumn("price",  "Cena",      "NUMBER",   100, "",  1, "N2")
        d list.AddColumn("date",   "Datum",     "DATE",     120, "",  1)
        d list.AddColumn("active", "Aktivní",   "BOOL",      80, "",  0)
        d list.AddColumn("link",   "Detail",    "TEXT",     100, "",  0).isLink = 1

        d list.WriteColumnsToOut()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}
```

**Datové typy sloupců:**

| Type | Použití | Renderování |
|---|---|---|
| `TEXT` | Text | `TextBlock` |
| `NUMBER` | Číslo (int/decimal) | `TextBlock` s `format` (`"N0"`, `"N2"`, …) |
| `DATE` | Datum | `TextBlock` ve formátu `dd.MM.yyyy` |
| `DATETIME` | Datum a čas | `TextBlock` ve formátu `dd.MM.yyyy HH:mm` |
| `BOOL` | Ano/Ne | Hodnota `"1"` → „Ano", jinak „Ne" |
| `HHMM` | Čas v minutách | `830` → `08:30` |
| `COLOR` | Barva `R;G;B` | Obarvený obdélníček |
| `IMAGE` | Obrázek | (zatím neimpl) |

Plus `isLink = 1` udělá z buňky klikatelný hyperlink → fires `CellLinkClicked` event.

### `LoadList(ByRef sys As %String) As %Status`

Načte řádky. Vstup: filter klíče pod sub-subscriptem `^||ML("IN","filter",klic)` (viz [filter konvence](01-wire-formats.md#filter-konvence---kde-zijí-filter-hodnoty)). Výstup: multi-slot JSON (jeden slot per řádek).

```objectscript
ClassMethod LoadList(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s filter = $g(^||ML("IN","filter","agendaCode"))

        s list = ##class(ALVA.EDControls.EDList.List).%New()

        // Iterovat data ze své doménové struktury
        s id = ""
        for {
            s id = $o(^MyData(id))
            q:id=""
            // (volitelně) aplikovat filter

            s row = list.AddRow(id)
            d row.%Set("id",     id)
            d row.%Set("name",   $g(^MyData(id,"name")))
            d row.%Set("price",  $g(^MyData(id,"price")))
            d row.%Set("date",   $g(^MyData(id,"date")))
            d row.%Set("active", $g(^MyData(id,"active")))
        }

        d list.WriteRowsToOut()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}
```

**`AddRow(id)` přijímá unikátní identifikátor řádku** — string. Použije se v eventech (`SelectedRowId`, `CheckedRowsChanged` IDs).

---

## Vzory použití

### Vzor 1 — Single-select picker

```xml
<el:EDList x:Name="TheList"/>
<StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
    <Button Content="Otevřít" IsDefault="True" Command="{Binding OpenCommand}"/>
    <Button Content="Zrušit" IsCancel="True"/>
</StackPanel>
```

```csharp
TheList.SelectionChanged += (_, _) => _vm.Selected = TheList.SelectedRow;
TheList.RowDoubleClicked += (_, _) => { if (_vm.OpenCommand.CanExecute(null)) _vm.OpenCommand.Execute(null); };
```

VM má `Selected: EDListRow?` property; `OpenCommand` checkne `Selected is not null`, vrátí `Selected.Id` přes `CloseDialog(true)`.

### Vzor 2 — Multi-select s předoznačením

```csharp
TheList.SelectionMode = EListSelectionMode.Check;

Loaded += (_, _) =>
{
    TheList.InitializeList();
    TheList.LoadList(new Dictionary<string, string> { ["agendaCode"] = _vm.AgendaCode });

    // Předoznačit aktuální výběr (např. z předchozího otevření)
    foreach (var id in _vm.InitiallySelectedIds)
        TheList.SetChecked(id, true);
};

TheList.CheckedRowsChanged += (_, ids) => _vm.SelectedCount = ids.Count;
```

Po OK z VM:
```csharp
protected override void OnOk()
{
    // Mapuje EDListRow → vlastní typ entity
    var selected = new List<MyEntity>();
    foreach (var id in TheList.SelectedRowIds)
    {
        var row = TheList.Rows.FirstOrDefault(r => r.Id == id);
        if (row is null) continue;
        var name = row["name"] ?? "";
        selected.Add(new MyEntity(id, name));
    }
    SelectedEntities = selected;
    CloseDialog(true);
}
```

### Vzor 3 — Filter změna při změně combo box (ad-hoc, do EDFilter komponenty)

```csharp
// VM:
public string? SelectedAgenda { get => _sel; set => Set(ref _sel, value); }

// Code-behind:
_vm.PropertyChanged += (_, e) =>
{
    if (e.PropertyName == nameof(MyVM.SelectedAgenda))
        ReloadList();
};

private void ReloadList()
{
    var agenda = _vm.SelectedAgenda;
    if (string.IsNullOrEmpty(agenda))
        TheList.LoadList();
    else
        TheList.LoadList(new Dictionary<string, string> { ["agendaCode"] = agenda });
}
```

Toto je legacy ad-hoc řešení. **Preferovaná cesta je deklarativní `EDFilter`** (EDList je jeho
`IEDFilterTarget`), viz [`dotnet/EDFilter/CLAUDE.md`](../dotnet/EDFilter/CLAUDE.md):

```xml
<edf:EDFilter Target="{Binding ElementName=TheList}" HostId="MujProjekt.MujPicker">
    <edf:EDFilterCombo Key="agendaCode" Label="Agenda"
                       ItemsSource="{Binding AgendaFilters}"
                       SelectedValuePath="Code" DisplayMemberPath="Label"/>
</edf:EDFilter>
<el:EDList x:Name="TheList"/>
```

**Důležité:** filter hodnoty se v CVar balí pod sub-subscript `^||ML("IN","filter","klic")` (nikoliv flat `^||ML("IN","klic")`). Caché endpoint čte přes `$g(^||ML("IN","filter","klic"))`. Detail v [`01-wire-formats.md`](01-wire-formats.md#filter-konvence---kde-zijí-filter-hodnoty).

---

## Persistence sloupců

Pokud nastavíš `HostId`, EDList automaticky:
- **Načte uložené rozložení sloupců** po `LoadList()` (šířky, pořadí, sort)
- **Uloží změny** s 500ms debounce kdykoli uživatel:
  - Roztáhne sloupec
  - Změní pořadí sloupců drag&drop
  - Klikne na header pro sort

Storage: `IUserPrefsService` scope `"EDControls.EDList"`, key = `HostId`. Persist je per (IdFa × userID × HostId).

```csharp
TheList.HostId = "MyProject.MyList";   // tahle volba aktivuje persist
```

Pro vypnutí: prostě `HostId` nenastavuj.

---

## Search

EDList má built-in client-side search box (zobrazuje se nad listem pokud `Title` nebo `SearchVisible=true`):

- Aktualizuje filter na všech viditelných data sloupcích (skipuje checkbox sloupec)
- Case-insensitive substring matching
- Smaž text → vrátí kompletní list

```csharp
TheList.Title = "Můj seznam";        // automaticky zobrazí title + search box
TheList.SearchVisible = false;       // nebo explicit vypnout search box
TheList.SearchText = "hledaný výraz"; // programaticky nastavit search
```

Pro server-side search použij `LoadList(filter)` s filter parametrem; client-side a server-side můžou koexistovat.

---

## Co EDList nemá (záměrně)

- **CRUD tlačítka** (Add/Edit/Delete) — hostující dialog si je dělá sám (mimo EDList)
- **Filter UI** — komponenta `EDFilter` plánovaná na pozdější (po L6 + Tier 3)
- **Context menu** — host si nastavuje sám pokud potřeba
- **Inline editace buněk** — EDList je read-only, na editaci je `EDInputForm` nebo modal dialog
- **Pagination** — listy nad ~500 řádků nejsou zamýšlené use case (na to bude budoucí stream-loading patternu pokud potřeba)

---

## Referenční implementace

Reálné dialogy v TemplateDesigneru používající EDList:

| Dialog | Caché backing | Použité features |
|---|---|---|
| `CacheTemplatePickerDialog` | `ALVA.TemplateDesigner.API.TemplatesList` | Single-select, agenda filter, Open/Rename/Delete CRUD v parent dialog |
| `InstancePickerDialog` | `ALVA.TemplateDesigner.API.InstancesList` | Multi-select (Check mode), pre-selection, search |

Studuj jejich code-behind pokud potřebuješ vzor pro nový dialog.

---

## Versioning a kompatibilita

EDList API je stabilní. Plánované budoucí přidatky (aditivní, ne breaking):

- `Filter` DP — pro EDFilter komponentu (nahradí ad-hoc combo + PropertyChanged subscription)
- `CRUDButtons` sekce — možná
- Pagination — pouze pokud reálný use case

Breaking change vyžaduje verzování + migrate guide v této doc.
