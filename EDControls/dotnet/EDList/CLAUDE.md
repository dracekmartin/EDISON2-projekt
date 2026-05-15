# EDList — poznámky pro vývojáře

## Stav: FUNKČNÍ ✅

**Co funguje:**

- ✅ Kompletní backend (Column.cls, List.cls, MockList.cls)
- ✅ Plně funkční EDList.xaml/.xaml.cs UserControl
- ✅ Parsování a formátování všech základních typů (TEXT, NUMBER, DATE, DATETIME, BOOL, HHMM, COLOR)
- ✅ Eventy: SelectionChanged, RowDoubleClicked, CellLinkClicked
- ✅ isLink sloupce — klikatelný podtržený text s `CellLinkClicked` eventem
- ✅ Testovací okno EDListTestWindow s mock daty
- ✅ JSON komunikace přes MLRest
- ✅ MLRest se čte automaticky z DataContext rodičovského okna (EDViewModel)

**Co chybí:**

- ⚠️ IMAGE typ (není implementován DataGridTemplateColumn s Image)
- ⚠️ CODEBOOK typ
- ⚠️ Filtrování v LoadList
- ⚠️ Search box v toolbaru (L3)
- ⚠️ CRUD tlačítka Add/Edit/Delete (L8 — nice to have)

**Co bylo dodáno (2026-05-15):**

- ✅ **L1** — Multi-select Check mode (`SelectionMode = Check`), tri-state header, `CheckAll`/`UncheckAll`, `EDListRow` s INotifyPropertyChanged
- ✅ **L2** — Column layout persist (šířky + pořadí + sort) přes `IUserPrefsService`
  - `HostId` property na EDList (povinné pro persist; null = persist off)
  - Storage: `^ALVA.EDControls.UserData(IdFa, userID, "EDControls.EDList", HostId)` = JSON blob
  - Load při `LoadList` (async, best-effort), Save debounced 500ms + force při Unload
  - Best-effort čtení — neznámé sloupce ignorovány, missing fields default
  - JSON schema versioning (`"version": 1`)
- ✅ MockList rozšířen o DATETIME + COLOR sloupce pro testing všech typů

## Účel

`EDList` je samostatný WPF `UserControl` zobrazující read-only seznam (tabulka se sloupci
a řádky) řízený z Caché backendu. Host nastaví `CacheClass` a zavolá `InitializeList()`
pro definici sloupců a `LoadList()` pro načtení dat. `MLRest` se čte automaticky
z `DataContext` rodičovského okna (`EDViewModel`). Výběr řádku a standardní interakce
(dvojklik, kontextové menu) jsou vystaveny jako eventy.

Více instancí v jednom okně je standardní scénář — každá instance je samostatný objekt
se svým `CacheClass` a voláními:

```csharp
ListVozidla.InitializeList();
ListVozidla.LoadList();

ListSpoje.InitializeList();
ListSpoje.LoadList();

ListVozidla.SelectionChanged += (_, id) => ListSpoje.LoadList();
```

---

## Mapa souborů

### C# / WPF  (`EDControls/EDList/`)

| Soubor | Stav | Role |
| --- | --- | --- |
| `EDList.xaml / .xaml.cs` | ✅ | UserControl. Host pro DataGrid, generování sloupců, napojení eventů. |
| `ListColumn.cs` | ✅ | Definice sloupce: `Key`, `Label`, `Type`, `Format`, `Width`, `Align`, `Sortable`, `Visible`, `IsLink`. |
| `EListColumnType.cs` | ✅ | Enum datových typů sloupce — viz sekce níže. |
| `EListColumnAlign.cs` | ✅ | Enum: `Auto`, `Left`, `Center`, `Right`. |
| `ColumnLayout.cs` | ❌ | Record pro ukládání uživatelského nastavení sloupců (pořadí + šířka). Zatím neimplementováno. |

### Caché ObjectScript  (`CACHE/ALVA/EDControls/EDList/`)

| Soubor | Stav | Role |
| --- | --- | --- |
| `Column.cls` | ✅ | `%RegisteredObject`. Model definice sloupce. |
| `List.cls` | ✅ | `%RegisteredObject`. Builder: `AddColumn`, `AddRow`, `WriteColumnsToOut`, `WriteRowsToOut`. |
| `MockList.cls` | ✅ | Testovací třída s hardcoded daty pro ověření EDList bez reálné databáze. |

### Testovací okno  (`EDISONMAIN2/Windows/EDListTestWindow/`)

| Soubor | Stav | Role |
| --- | --- | --- |
| `EDListTestWindow.xaml` | ✅ | Layout testovacího okna s toolbar, status bar a EDList komponentou. |
| `EDListTestWindow.xaml.cs` | ✅ | Code-behind s napojením na MockList, event handlery. |

---

## Datové typy sloupců

Typ sloupce určuje výchozí zarovnání, způsob formátování, řazení a renderování buňky.
Zarovnání lze vždy přebít vlastností `align`. Chování "link" (klikatelná buňka) je
samostatná vlastnost `isLink` — nezávislá na typu.

### Přehled typů

| Typ (enum) | Řetězec v JSON | Popis | Výchozí zarovnání |
|---|---|---|---|
| `Text` | `"TEXT"` | Prostý string | Vlevo |
| `Number` | `"NUMBER"` | Celé nebo desetinné číslo | Vpravo |
| `Date` | `"DATE"` | Datum | Vlevo |
| `DateTime` | `"DATETIME"` | Datum + čas | Vlevo |
| `Bool` | `"BOOL"` | Logická hodnota — zobrazí se jako „Ano" / „Ne" | Střed |
| `Image` | `"IMAGE"` | Název resource z WPF resource dictionary | Střed |
| `Color` | `"COLOR"` | Barva ve formátu R;G;B — buňka se vybarví | Střed |
| `HoursMinutes` | `"HHMM"` | Čas v minutách jako int (830 = 08:30) | Vpravo |
| `Codebook` | `"CODEBOOK"` | Číselník — kód + label *(do budoucna)* | Vlevo |

### Mapování z legacy typů

Staré typy používaly typ pro vyjádření zarovnání nebo chování odkazu.
V novém designu jsou toto samostatné vlastnosti (`align`, `isLink`):

| Legacy | Nový typ | align | isLink |
|---|---|---|---|
| `T` | `TEXT` | *(auto)* | false |
| `Tc` | `TEXT` | `CENTER` | false |
| `TL` | `TEXT` | *(auto)* | **true** |
| `N`, `N2` | `NUMBER` | *(auto)* | false |
| `Nc` | `NUMBER` | `CENTER` | false |
| `NL` | `NUMBER` | *(auto)* | **true** |
| `D` | `DATE` | *(auto)* | false |
| `DT` | `DATETIME` | *(auto)* | false |
| `B`, `ano/ne` | `BOOL` | *(auto)* | false |
| `I` | `IMAGE` | *(auto)* | false |
| `CLR` | `COLOR` | *(auto)* | false |
| `HHMM` | `HHMM` | *(auto)* | false |
| `CBO` | `CODEBOOK` | *(auto)* | false |

### Wire formát hodnot z Caché

Caché vždy posílá **interní formát**; typ + `format` na C# straně řídí zobrazení.

| Typ | Caché pošle | C# zobrazí |
|---|---|---|
| `TEXT` | `"Praha"` | `"Praha"` |
| `NUMBER` | `"1234.5"` (vždy tečka) | `"1 234,50"` dle `format` (`"N0"`, `"N2"`...) |
| `DATE` | `"2024-03-15"` (ISO 8601) | `"15.03.2024"` dle `format` |
| `DATETIME` | `"2024-03-15T08:30:00"` | `"15.03.2024 08:30"` dle `format` |
| `BOOL` | `"1"` / `"0"` | `"Ano"` / `"Ne"` |
| `IMAGE` | `"ic_bus"` (název resource) | ikona z WPF resource dictionary |
| `COLOR` | `"255;128;0"` (R;G;B) | podbarvení buňky danou barvou |
| `HHMM` | `"830"` (int, 830 = 08:30) | `"08:30"` |
| `CODEBOOK` | `"CB"` (raw kód) | `"České Budějovice"` (label z options) |

### Vlastnost `format`

Používá se pouze u typů, kde má smysl:

| Typ | Příklad `format` | Popis |
|---|---|---|
| `NUMBER` | `"N0"`, `"N2"` | C# numeric format string |
| `DATE` | `"dd.MM.yyyy"` | C# date format string |
| `DATETIME` | `"dd.MM.yyyy HH:mm"` | C# date format string |
| ostatní | *(prázdný)* | Ignorováno |

### Vlastnost `isLink`

Pokud `isLink = true`, buňka se zobrazí jako odkaz (podtržený text).
Kliknutí vyvolá event `CellLinkClicked` s `rowId` a `columnKey`.

```csharp
// Event na EDList:
event EventHandler<(string RowId, string ColumnKey)>? CellLinkClicked
```

---

## Komunikace

Stejný transport jako `EDInputForm`: process-private globály `^||ML("IN",...)` /
`^||ML("OUT",...)`, volání přes `MLRest.Query`.

EDList používá **JSON** místo delimitovaných stringů — data řádků jsou dynamická,
JSON objekty jsou samo-popisné a nevyžadují poziční parsování. `%DynamicObject`
v Caché a `System.Text.Json` v C# obstarají serializaci bez vlastních `Serialize()` metod.

### InitializeList — formát OUT

`OUT[1]` = JSON pole objektů sloupců:

```json
[
  {"key":"nazev","label":"Název","type":"TEXT","width":150,"align":"","sortable":true,"visible":true,"isLink":false},
  {"key":"pocet","label":"Počet","type":"NUMBER","format":"N0","width":80,"align":"","sortable":true,"visible":true,"isLink":false},
  {"key":"aktivni","label":"Aktivní","type":"BOOL","width":60,"align":"","sortable":false,"visible":true,"isLink":false},
  {"key":"barva","label":"Barva","type":"COLOR","width":60,"align":"","sortable":false,"visible":true,"isLink":false}
]
```

### LoadList — formát IN / OUT

**Vstup (IN):** Zatím se nic neposílá. Do budoucna bude `IN` obsahovat pojmenované
klíče (CVar formát nebo JSON) pro filtrování — žádné poziční číselné indexy.
Caché metoda má pro budoucí filtr připraven komentář:

```objectscript
// Budoucí filtr — pojmenované klíče, např.:
// s filterText = $g(^||ML("IN","filter"))
// s datumOd    = $g(^||ML("IN","od"))
```

**Výstup (OUT):** jeden JSON objekt na řádek, vždy s klíčem `"id"`:

```
OUT[1] = {"id":"42","nazev":"Linka 1","pocet":"15","aktivni":"1","barva":"255;128;0"}
OUT[2] = {"id":"43","nazev":"Linka 2","pocet":"8","aktivni":"0","barva":"0;128;255"}
```

### Příklad skutečné komunikace z MockList

**InitializeList — OUT globály:**
```
^||ML("OUT") = 1
^||ML("OUT",1) = [{"key":"rz","label":"RZ","type":"TEXT","width":110,"align":"","sortable":false,"visible":true,"isLink":false},{"key":"typ","label":"Typ","type":"TEXT","width":130,"align":"","sortable":false,"visible":true,"isLink":false},{"key":"aktivni","label":"Aktivní","type":"BOOL","width":70,"align":"","sortable":false,"visible":true,"isLink":false},{"key":"odjezd","label":"Odjezd","type":"HHMM","width":75,"align":"RIGHT","sortable":false,"visible":true,"isLink":false},{"key":"pocet","label":"Počet km","type":"NUMBER","format":"N1","width":90,"align":"RIGHT","sortable":true,"visible":true,"isLink":false},{"key":"datum","label":"Datum","type":"DATE","width":100,"align":"","sortable":false,"visible":true,"isLink":false}]
```

**LoadList — OUT globály:**
```
^||ML("OUT") = 3
^||ML("OUT",1) = {"id":"1","rz":"1B2 3456","typ":"Kloubový","aktivni":"1","odjezd":"830","pocet":"142.5","datum":"2024-03-15"}
^||ML("OUT",2) = {"id":"2","rz":"2C3 7890","typ":"Standardní","aktivni":"0","odjezd":"1245","pocet":"98","datum":"2024-03-16"}
^||ML("OUT",3) = {"id":"3","rz":"3D4 1122","typ":"Kloubový","aktivni":"1","odjezd":"600","pocet":"210.75","datum":"2024-03-17"}
```

**C# parsování:**
- `InitializeList()` → parsuje `OUT[1]` jako JSON pole → vytvoří `List<ListColumn>`
- `LoadList()` → parsuje `OUT[1..n]` jako JSON objekty → vytvoří `ObservableCollection<Dictionary<string, string?>>`
- DataGrid má `ItemsSource = _rows`, sloupce bindují na `[{key}]`

---

## Caché ObjectScript — syntaxe a konstrukce

Tato sekce vysvětluje syntaxi Caché ObjectScript používanou v EDList třídách.

### Základní příkazy

| Příkaz | Význam | Příklad |
|--------|--------|---------|
| `s` | **Set** — přiřazení hodnoty do proměnné | `s list = ##class(List).%New()` |
| `d` | **Do** — volání metody bez použití návratové hodnoty | `d list.AddColumn("key", "Label")` |
| `q` | **Quit** — návrat z metody/funkce | `q 1` (vrátí status true) |
| `ret` | **Return** — alternativa k quit | `ret ret` (vrátí status) |

### Třídy a objekty

| Konstrukce | Význam | Příklad |
|------------|--------|---------|
| `##class(...)` | Reference na třídu (pro volání ClassMethod nebo %New) | `##class(ALVA.EDControls.EDList.List)` |
| `.%New()` | Vytvoření nové instance objektu (konstruktor) | `s list = ##class(List).%New()` |
| `.Insert(obj)` | Vložení objektu do `list Of ...` kolekce | `d ..columns.Insert(col)` |
| `.%Push(obj)` | Přidání prvku do `%DynamicArray` | `d ..rows.%Push(row)` |
| `..property` | Přístup k property aktuální instance (`this.property`) | `s ..rows = ##class(%DynamicArray).%New()` |

### Funkce a operátory

| Funkce | Význam | Příklad |
|--------|--------|---------|
| `$i(var)` | **Increment** — zvýší hodnotu proměnné o 1 a vrátí novou hodnotu | `s ^||ML("OUT", $i(^||ML("OUT"))) = json` |
| `$g(var)` | **Get** — vrátí hodnotu proměnné, pokud neexistuje vrátí "" | `s filter = $g(^||ML("IN","filter"))` |
| `$g(var, default)` | Get s výchozí hodnotou | `s limit = $g(^||ML("IN","limit"), 100)` |

### Globály (persistentní úložiště)

| Konstrukce | Význam | Příklad |
|------------|--------|---------|
| `^||ML(...)` | **Process-private global** — globální pole viditelné jen v aktuálním procesu | `s ^||ML("OUT", 1) = "data"` |
| `^ML(...)` | Běžný global — sdílený napříč procesy | `s ^ML("config", "version") = "1.0"` |

Globály jsou vícedimenzionální pole s klíči typu string:
```objectscript
s ^||ML("OUT")         = 0           // počítadlo výstupních řádků
s ^||ML("OUT", 1)      = "první řádek"
s ^||ML("OUT", 2)      = "druhý řádek"
s ^||ML("IN", "id")    = "42"
s ^||ML("IN", "filter") = "Praha"
```

### Smyčky

```objectscript
// FOR smyčka: for var=start:increment:end
f i=1:1:..columns.Count() {
    s col = ..columns.GetAt(i)
    // ...
}

// FOR přes %DynamicArray (index začína 0)
f i=0:1:..rows.%Size()-1 {
    s row = ..rows.%Get(i)
    // ...
}

// Nekonečná smyčka s podmínkou uvnitř
f {
    // ... kód ...
    q:podmínka  // quit if podmínka
}
```

### Error handling

```objectscript
try {
    // kód, který může vyhodit chybu
    s result = ..SomeMethod()
}
catch (ex) {
    // ex je %Exception.AbstractException
    s error = ex.DisplayString()
    // logování nebo propagace
}
```

### Property definice

```objectscript
// Jednoduchá property
Property key As %String;

// Property s výchozí hodnotou
Property type As %String [ InitialExpression = "TEXT" ];
Property width As %Integer [ InitialExpression = 0 ];

// Property s collection typem
Property columns As list Of ALVA.EDControls.EDList.Column;

// Property s dynamic objektem a inicializací
Property rows As %DynamicArray [ InitialExpression = {##class(%DynamicArray).%New()} ];
```

Modifikátory v `[ ]`:
- `InitialExpression` — výchozí hodnota (vyhodnotí se při `%New()`)
- `Private`, `Internal` — viditelnost
- `Calculated` — vypočítaná property (má Get/Set metody)

### JSON serializace

```objectscript
// %DynamicObject — mutable JSON objekt
s row = ##class(%DynamicObject).%New()
s row.id = "42"
s row.nazev = "Praha"
s json = row.%ToJSON()  // vrátí: {"id":"42","nazev":"Praha"}

// %DynamicArray — mutable JSON pole
s arr = ##class(%DynamicArray).%New()
d arr.%Push("prvni")
d arr.%Push("druhy")
s json = arr.%ToJSON()  // vrátí: ["prvni","druhy"]

// Přístup k prvkům
s value = obj.%Get("key")     // nebo: s value = obj.key
s item  = arr.%Get(0)         // index od 0
s size  = arr.%Size()
```

### Praktický příklad z MockList.cls

```objectscript
ClassMethod LoadList(ByRef sys As %String) As %Status
{
    // Vytvoř builder objekt
    s list = ##class(ALVA.EDControls.EDList.List).%New()

    // Přidej první řádek — AddRow vrátí %DynamicObject
    s row = list.AddRow("1")
    s row.rz      = "1B2 3456"      // nastaví property přímo
    s row.typ     = "Kloubový"
    s row.aktivni = "1"
    s row.odjezd  = "830"           // HHMM jako int
    s row.pocet   = "142.5"         // NUMBER jako string s tečkou
    s row.datum   = "2024-03-15"    // DATE jako ISO 8601

    // Přidej další řádky...
    s row = list.AddRow("2")
    // ...

    // Zapiš všechny řádky jako JSON do ^||ML("OUT", 1..n)
    d list.WriteRowsToOut()

    // Vrať success status
    q 1
}
```

### Konvence pojmenování

- **ClassMethod** — statická metoda (jako C# `static method`)
- **Method** — instanční metoda (jako C# běžná `method`)
- **Property** — property třídy (jako C# `property`)
- `%` prefix — systémové třídy (např. `%String`, `%Integer`, `%DynamicObject`)
- `..` prefix — aktuální instance (jako C# `this.`)

---

## Caché model

### Column.cls

Jednoduchý data model bez logiky — jen properties.

```objectscript
Class ALVA.EDControls.EDList.Column Extends %RegisteredObject
{
    /// Identifikátor sloupce — používá se jako klíč v datech řádku.
    Property key As %String;

    /// Zobrazovaný nadpis sloupce.
    Property label As %String;

    /// Datový typ: TEXT | NUMBER | DATE | DATETIME | BOOL | IMAGE | COLOR | HHMM | CODEBOOK
    Property type As %String [ InitialExpression = "TEXT" ];

    /// Formátovací řetězec.
    /// NUMBER: "N0", "N2" apod.  DATE/DATETIME: "dd.MM.yyyy" apod.  Ostatní: prázdný.
    Property format As %String;

    /// Šířka sloupce v pixelech. 0 = automaticky podle obsahu.
    Property width As %Integer [ InitialExpression = 0 ];

    /// Zarovnání buněk: prázdný = auto (dle typu), LEFT, CENTER, RIGHT.
    Property align As %String;

    Property sortable As %Boolean [ InitialExpression = 0 ];

    Property visible As %Boolean [ InitialExpression = 1 ];

    /// Pokud true, buňka se zobrazí jako odkaz a vyvolá event CellLinkClicked.
    Property isLink As %Boolean [ InitialExpression = 0 ];
}
```

**Poznámka:** Třída nepoužívá `%JSON.Adaptor` — serializaci do JSON obstarává `List.cls::WriteColumnsToOut()` ručně přes `%DynamicObject`.

### List.cls

Builder třída pro sestavení sloupců a řádků. **Klíčová třída** pro práci s EDList.

```objectscript
Class ALVA.EDControls.EDList.List Extends %RegisteredObject
{
    Property columns As list Of ALVA.EDControls.EDList.Column;
    Property rows As %DynamicArray;

    // ─── Inicializace ─────────────────────────────────────────────────────
    Method %OnNew() As %Status
    {
        s ..rows = ##class(%DynamicArray).%New()
        q 1
    }

    // ─── Builder — sloupce ────────────────────────────────────────────────
    /// Přidá sloupec a vrátí ho pro případné další nastavení (isLink apod.).
    /// format: NUMBER → "N0"/"N2", DATE/DATETIME → "dd.MM.yyyy", ostatní prázdný.
    Method AddColumn(key As %String, label As %String, type As %String = "TEXT",
                     width As %Integer = 0, align As %String = "",
                     sortable As %Boolean = 0, format As %String = "") As ALVA.EDControls.EDList.Column
    {
        s col = ##class(ALVA.EDControls.EDList.Column).%New()
        s col.key      = key
        s col.label    = label
        s col.type     = type
        s col.width    = width
        s col.align    = align
        s col.sortable = sortable
        s col.format   = format
        d ..columns.Insert(col)
        q col
    }

    // ─── Builder — řádky ──────────────────────────────────────────────────
    /// Vytvoří nový řádek s daným id, přidá ho do interní %DynamicArray a vrátí ho.
    /// Volající nastaví pole přímo:  s row.nazev = "Praha"
    Method AddRow(id As %String) As %DynamicObject
    {
        s row    = ##class(%DynamicObject).%New()
        s row.id = id
        d ..rows.%Push(row)
        q row
    }

    // ─── Výstup ───────────────────────────────────────────────────────────
    /// Zapíše OUT[n] = JSON pole definic sloupců (jedno volání = jeden OUT záznam).
    Method WriteColumnsToOut()
    {
        s arr = ##class(%DynamicArray).%New()
        f i=1:1:..columns.Count() {
            s col = ..columns.GetAt(i)
            s obj = ##class(%DynamicObject).%New()
            s obj.key      = col.key
            s obj.label    = col.label
            s obj.type     = col.type
            s obj.format   = col.format
            s obj.width    = col.width
            s obj.align    = col.align
            s obj.sortable = col.sortable
            s obj.visible  = col.visible
            s obj.isLink   = col.isLink
            d arr.%Push(obj)
        }
        s ^||ML("OUT", $i(^||ML("OUT"))) = arr.%ToJSON()
    }

    /// Zapíše OUT[n], OUT[n+1], ... = jeden JSON objekt na řádek.
    Method WriteRowsToOut()
    {
        f i=0:1:..rows.%Size()-1 {
            s ^||ML("OUT", $i(^||ML("OUT"))) = ..rows.%Get(i).%ToJSON()
        }
    }
}
```

**Klíčové body:**
- `AddColumn()` vrací `Column` objekt → umožňuje fluent API: `s col = list.AddColumn(...) s col.format = "N2"`
- `AddRow()` vrací `%DynamicObject` → volající nastavuje properties přímo: `s row.nazev = "Praha"`
- `columns` je `list Of Column` (indexováno od 1, `.GetAt(i)`, `.Insert(obj)`)
- `rows` je `%DynamicArray` (indexováno od 0, `.%Get(i)`, `.%Push(obj)`)
- `WriteColumnsToOut()` vytvoří jedno JSON pole s definicemi všech sloupců
- `WriteRowsToOut()` zapíše každý řádek jako samostatný JSON objekt

### Vzor BL třídy — MockList.cls (testovací implementace)

Kompletní funkční příklad z `CACHE/ALVA/EDControls/EDList/MockList.cls`:

```objectscript
/// Testovací třída s hardcoded daty pro ověření EDList bez reálné databáze.
Class ALVA.EDControls.EDList.MockList Extends %RegisteredObject
{
    ClassMethod InitializeList(ByRef sys As %String) As %Status
    {
        s list = ##class(ALVA.EDControls.EDList.List).%New()
        d list.AddColumn("rz",      "RZ",          "TEXT",   110)
        d list.AddColumn("typ",     "Typ",         "TEXT",   130)
        d list.AddColumn("aktivni", "Aktivní",     "BOOL",    70)
        d list.AddColumn("odjezd",  "Odjezd",      "HHMM",    75, "RIGHT")

        d list.AddColumn("pocet", "Počet km", "NUMBER",  90, "RIGHT", 1, "N1")

        d list.AddColumn("datum",   "Datum",       "DATE",   100)
        d list.WriteColumnsToOut()
        q 1
    }

    ClassMethod LoadList(ByRef sys As %String) As %Status
    {
        s list = ##class(ALVA.EDControls.EDList.List).%New()

        // Řádek 1
        s row = list.AddRow("1")
        s row.rz      = "1B2 3456"
        s row.typ     = "Kloubový"
        s row.aktivni = "1"             // BOOL: "1" = Ano, "0" = Ne
        s row.odjezd  = "830"           // HHMM: 830 = 08:30
        s row.pocet   = "142.5"         // NUMBER: vždy s tečkou, ne čárkou
        s row.datum   = "2024-03-15"    // DATE: ISO 8601 formát

        // Řádek 2
        s row = list.AddRow("2")
        s row.rz      = "2C3 7890"
        s row.typ     = "Standardní"
        s row.aktivni = "0"
        s row.odjezd  = "1245"          // 12:45
        s row.pocet   = "98"
        s row.datum   = "2024-03-16"

        // Řádek 3
        s row = list.AddRow("3")
        s row.rz      = "3D4 1122"
        s row.typ     = "Kloubový"
        s row.aktivni = "1"
        s row.odjezd  = "600"           // 06:00
        s row.pocet   = "210.75"
        s row.datum   = "2024-03-17"

        d list.WriteRowsToOut()
        q 1
    }
}
```

### Vzor BL třídy — reálná aplikace (s databází)

```objectscript
Class APP.VozidlaList Extends %RegisteredObject
{
    ClassMethod InitializeList(ByRef sys As %String) As %Status
    {
        $$$Init
        try {
            $$$FillParams
            s list = ##class(ALVA.EDControls.EDList.List).%New()
            d list.AddColumn("rz",        "RZ",           "TEXT",   100)
            d list.AddColumn("typ",       "Typ",          "TEXT",   150)
            d list.AddColumn("aktivni",   "Aktivní",      "BOOL",    60)
            d list.AddColumn("odjezd",    "Odjezd",       "HHMM",    70)
            d list.AddColumn("kilometraz","Kilometráž",   "NUMBER",  90, "RIGHT")
            d list.WriteColumnsToOut()
        }
        catch (ex) { $$$CatchErrRest }
        ret ret
    }

    ClassMethod LoadList(ByRef sys As %String) As %Status
    {
        $$$Init
        try {
            $$$FillParams
            // Budoucí filtr:
            // s filterText = $g(^||ML("IN","filter"))
            // s datumOd    = $g(^||ML("IN","od"))

            s list = ##class(ALVA.EDControls.EDList.List).%New()

            // SQL dotaz nebo procházení globálů
            &sql(DECLARE C1 CURSOR FOR
                 SELECT ID, RZ, Typ, Aktivni, Odjezd, Kilometraz
                 FROM Vozidla
                 WHERE Aktivni = 1
                 ORDER BY RZ)
            &sql(OPEN C1)
            f {
                &sql(FETCH C1 INTO :id, :rz, :typ, :aktivni, :odjezd, :km)
                q:SQLCODE'=0

                s row = list.AddRow(id)
                s row.rz        = rz
                s row.typ       = typ
                s row.aktivni   = aktivni         // "1" nebo "0"
                s row.odjezd    = odjezd          // int, např. "830"
                s row.kilometraz = km             // decimal jako string s tečkou
            }
            &sql(CLOSE C1)

            d list.WriteRowsToOut()
        }
        catch (ex) { $$$CatchErrRest }
        ret ret
    }
}
```

---

## C# veřejné API

Pro hostitelské okno je toto **public contract** EDList. Co tady **není**, je interní implementační detail — neopírej se o to.

### Konfigurace (set před InitializeList)

```csharp
public string CacheClass { get; set; } = "";
// Caché třída implementující InitializeList + LoadList (např. "ALVA.MHD.LinkyList").
// MLRest se čte automaticky z DataContext rodičovského okna (EDViewModel).

public EListSelectionMode SelectionMode { get; set; } = EListSelectionMode.Single;
// Single: klasický single-select přes DataGrid.SelectedItem
// Check : multi-select — vlevo se prepend checkbox sloupec (header = toggle-all tri-state)
// Změna za běhu vyvolá RebuildColumns (přidá/odebere checkbox sloupec).
```

### Volání (host inicializuje + reaguje na refresh)

```csharp
public void InitializeList();
// Načte definici sloupců z Caché → generuje DataGrid sloupce. Volat 1× po setup.

public void LoadList();
// Načte data z Caché → naplní řádky. Volat opakovaně (refresh, after filter).
// Při LoadList se vyčistí checked state.
```

### Stav (read-only)

```csharp
public IReadOnlyList<ListColumn>? Columns { get; }
// Definice sloupců, dostupné po InitializeList.

public string? SelectedRowId { get; }
// Single mode: ID aktuálně vybraného řádku (DataGrid SelectedItem).
// Check mode: stejné jako Single (řádek může být "selected" navíc k "checked").

public IReadOnlyList<string> SelectedRowIds { get; }
// Single mode: [SelectedRowId] nebo prázdné.
// Check mode: ID všech checknutých řádků (z EDListRow.IsChecked).
```

### Multi-select API (Check mode)

```csharp
public void SetChecked(string rowId, bool isChecked);
// Programaticky toggle checked stav konkrétního řádku.
// No-op pokud rowId neexistuje. Vyvolá CheckedRowsChanged.

public void CheckAll();
// Zaškrtne všechny řádky. Vyvolá CheckedRowsChanged 1×.

public void UncheckAll();
// Odznačí všechny. Vyvolá CheckedRowsChanged 1×.
```

### Eventy (host subscribe v constructoru)

```csharp
public event EventHandler<string?>? SelectionChanged;
// DataGrid native — fire při změně SelectedRowId (klik na řádek).

public event EventHandler<string>? RowDoubleClicked;
// Dvojklik na řádek → rowId.

public event EventHandler<IReadOnlyList<string>>? CheckedRowsChanged;
// Check mode — fire kdykoliv se checked sada změní (manual click / SetChecked /
// CheckAll / UncheckAll / LoadList reset). Argument = aktuální SelectedRowIds.

public event EventHandler<(string RowId, string ColKey)>? CellLinkClicked;
// Klik na buňku se sloupcem IsLink=true (modrý podtržený text).
```

### Typický vzor použití

```xaml
<el:EDList x:Name="LinkyList"
           CacheClass="ALVA.MHD.LinkyList"
           SelectionMode="Check"/>
```

```csharp
public LinkyPickerDialog() {
    DataContext = new EDViewModel();           // poskytuje MLRest
    InitializeComponent();

    LinkyList.InitializeList();                // sloupce z Caché
    LinkyList.LoadList();                      // data z Caché

    LinkyList.CheckedRowsChanged += (_, ids) =>
        OkButton.IsEnabled = ids.Count > 0;
}

private void OkClick(...) {
    var ids = LinkyList.SelectedRowIds;        // z Check mode
    // nebo: var id = LinkyList.SelectedRowId; // z Single mode
}
```

### Implementační detail — EDListRow

```csharp
public sealed class EDListRow : INotifyPropertyChanged
{
    public string Id { get; }
    public IReadOnlyDictionary<string, string?> Values { get; }
    public string? this[string key] { get; }      // = Values[key] nebo null
    public bool IsChecked { get; set; }            // notifikuje
}
```

Host ji **nepoužívá přímo** — EDList ji vystavuje jen jako `dataGrid.SelectedItem` typ pro WPF binding. Pokud host potřebuje data, použije `SelectedRowId` / `SelectedRowIds` a vlastní cache toho, co posílá do Caché.

### L2 — Column layout persist (implementováno)

```csharp
public string? HostId { get; set; }
// Klíč pro persist column layout přes IUserPrefsService.
// null nebo prázdné = persist off (žádný Load/Save).
// Konvence: "<Project>.<ConsumerName>", např. "TemplateDesigner.TemplatePicker".
// Storage: ^ALVA.EDControls.UserData(IdFa, userID, "EDControls.EDList", HostId)
//
// Layout obsahuje:
// - widths (column.ActualWidth)
// - order (column.DisplayIndex)
// - sort (column.SortDirection + ICollectionView.SortDescriptions)
//
// Save: debounced 500ms po změně column property + force při Unload okna.
// Load: async po LoadList, aplikuje na current sloupce best-effort.
```

### Budoucí rozšíření (v dalších commitech)

```csharp
// L3 — Search + Title
public string Title { get; set; }              // header text nad gridem
public string SearchText { get; set; }         // client-side filter

// L7 (později) — Image sloupec, Codebook field type
// L8 — CRUD tlačítka (Add/Edit/Delete) → otevírá EDInputForm dialog
```

---

## Nastavení WPF DataGridu

```csharp
dataGrid.CanUserReorderColumns = true;
dataGrid.CanUserResizeColumns  = true;
dataGrid.CanUserSortColumns    = true;
dataGrid.SelectionMode         = DataGridSelectionMode.Single;
dataGrid.IsReadOnly            = true;
```

Generování sloupců po `InitializeList`:
- Základní typy (`TEXT`, `NUMBER`, `DATE`, `DATETIME`, `BOOL`, `HHMM`) → `DataGridTextColumn`
- `COLOR` → `DataGridTemplateColumn` s barevným pozadím buňky
- `IMAGE` → `DataGridTemplateColumn` s `Image` elementem, resource lookup přes `Application.Current.Resources`
- `isLink = true` → `DataGridHyperlinkColumn` nebo `DataGridTemplateColumn` s `TextBlock` jako odkaz

Šířka: `width == 0` → `DataGridLengthUnitType.Auto`, `width > 0` → fixní pixely.

---

## Praktické použití

### V XAML okně

```xaml
<Window xmlns:el="clr-namespace:EDControls.EDList;assembly=EDControls">
    <Grid>
        <el:EDList x:Name="VozidlaList" />
    </Grid>
</Window>
```

### V code-behind

```csharp
public partial class VozidlaWindow : Window
{
    public VozidlaWindow()
    {
        DataContext = new VozidlaViewModel();
        InitializeComponent();

        // Konfigurace — MLRest se čte z DataContext (EDViewModel) automaticky
        VozidlaList.CacheClass = "APP.VozidlaList";

        // Načtení
        VozidlaList.InitializeList();
        VozidlaList.LoadList();

        // Eventy
        VozidlaList.SelectionChanged += OnVozidloSelected;
        VozidlaList.RowDoubleClicked += OnVozidloDoubleClick;
    }

    private void OnVozidloSelected(object sender, string? rowId)
    {
        if (rowId is null)
        {
            // Žádný výběr
            return;
        }

        // Načti detail vozidla...
        DetailPanel.LoadDetail(rowId);
    }

    private void OnVozidloDoubleClick(object sender, string rowId)
    {
        // Otevři editaci...
        var editWindow = new VozidloEditWindow(rowId);
        editWindow.ShowDialog();
    }
}
```

### Master-detail scénář

```csharp
// Dvě instance EDList v jednom okně
public partial class DispecinkWindow : Window
{
    public DispecinkWindow()
    {
        DataContext = new DispecinkViewModel();
        InitializeComponent();

        // Master list — vozidla (MLRest z DataContext automaticky)
        ListVozidla.CacheClass = "APP.VozidlaList";
        ListVozidla.InitializeList();
        ListVozidla.LoadList();

        // Detail list — spoje vybraného vozidla
        ListSpoje.CacheClass = "APP.SpojeList";
        ListSpoje.InitializeList();

        // Při změně výběru vozidla načti jeho spoje
        ListVozidla.SelectionChanged += (_, vozidloId) =>
        {
            if (vozidloId is not null)
            {
                // Budoucí: předání filtru do LoadList
                // ListSpoje.LoadList(new { vozidloId });
                ListSpoje.LoadList();
            }
        };
    }
}
```

---

## Testovací okno — EDListTestWindow

Testovací okno je v projektu EDISONMAIN2 ve složce `Windows/EDListTestWindow/`.

### Soubory

- **EDListTestWindow.xaml** — layout s toolbar (InitializeList, LoadList), status bar a EDList komponentou
- **EDListTestWindow.xaml.cs** — code-behind s napojením na MockList

### Použití

```csharp
// Otevřeni přes WindowNavigator (MLRest z EDServices automaticky):
WindowNavigator.Show("EDListTestWindow");
```

### Funkce

- **Toolbar:**
  - `InitializeList` — načte definici sloupců z `ALVA.EDControls.EDList.MockList::InitializeList`
  - `LoadList` — načte testovací data z `ALVA.EDControls.EDList.MockList::LoadList`

- **Status bar:**
  - Zobrazuje aktuální výběr řádku
  - Zobrazuje ID řádku při dvojkliku

- **Mock data:**
  - 6 sloupců: RZ, Typ, Aktivní (BOOL), Odjezd (HHMM), Počet km (NUMBER), Datum (DATE)
  - 3 testovací řádky vozidel

---

## Roadmapa

### ✅ MVP — ověření s mock daty (HOTOVO)

**Caché:**
- ✅ `Column.cls` — model sloupce
- ✅ `List.cls` — builder (AddColumn, AddRow, WriteColumnsToOut, WriteRowsToOut)
- ✅ `MockList.cls` — testovací třída s hardcoded daty (InitializeList + LoadList)

**C#:**
- ✅ `ListColumn.cs`, `EListColumnType.cs`, `EListColumnAlign.cs` — datové modely
- ✅ `EDList.xaml / .xaml.cs` — UserControl
  - ✅ `InitializeList()` — volá Caché, parsuje JSON sloupce, generuje `DataGrid.Columns`
  - ✅ `LoadList()` — volá Caché, parsuje JSON řádky, naplní `ItemsSource`
  - ✅ `SelectedRowId` property
  - ✅ `RowDoubleClicked` a `SelectionChanged` eventy
  - ✅ Formátování hodnot (BOOL → "Ano"/"Ne", HHMM → "08:30", DATE, NUMBER)
  - ✅ COLOR typ s barevným pozadím (RgbStringToBrushConverter)
  - ✅ Zarovnání sloupců (auto dle typu nebo explicitní)
- ✅ Testovací okno v EDISONMAIN2 — zapojí EDList s `MockList` třídou

### 🚧 Dokončení základních funkcí

- [ ] **IMAGE typ** — DataGridTemplateColumn s Image elementem + resource lookup
- [x] **isLink sloupce** — DataGridTemplateColumn s podtrženým modrým textem + event CellLinkClicked ✅
- [ ] **CODEBOOK typ** — mapování kódů na labely z inline options

### 📋 Pokročilé funkce

- [ ] **Filtrování** — `LoadList` přijme `CVar` nebo JSON s pojmenovanými klíči
- [ ] **ReloadData()** — refresh se zapamatovaným posledním filtrem
- [ ] **ColumnLayout** — ukládání pořadí a šířky sloupců per uživatel
- [ ] **Design-time mock data** v C# (analogie `DesignTimeData` z EDInputForm)
- [ ] **Kontextové menu** — event pro zobrazení custom menu na řádku
- [ ] **Sdílené styly** — přidat `Themes/Base.xaml` do EDControls (barvy, fonty, styl DataGridu); EDISONMAIN2 merguje přes `App.xaml`. Aktuálně isLink používá `SystemColors.HotTrackBrush` — po zavedení témat nahradit za `{StaticResource LinkBrush}`

---

## Návrhová rozhodnutí

- Zarovnání a chování odkazu jsou vlastnosti sloupce (`align`, `isLink`), ne součást typu — legacy typy jako `Tc`, `NL` se mapují na `TEXT`/`NUMBER` + příslušné vlastnosti.
- `BOOL` vždy zobrazuje „Ano" / „Ne"; vstupní hodnota z Caché je `"1"` / `"0"`.
- `HHMM` přijímá celé číslo v HHMM formátu (830 = 08:30), C# zobrazuje `HH:mm`.
- `COLOR` přijímá `"R;G;B"` (255;128;0), C# vykreslí jako podbarvení buňky.
- `IMAGE` přijímá název resource; lookup jde přes `Application.Current.Resources` — nezávislé na konkrétním projektu.
- `rows` v `List.cls` je `%DynamicArray` (ne `list Of %DynamicObject`) — spolehlivé a JSON-nativní.
- Žádné vlastní `Serialize()` metody — `%JSON.Adaptor` a `%DynamicObject` obstarají vše.
- `%RegisteredObject` (ne `%Persistent`) — žádné ukládání do databáze.
- Filtr bude předáván pojmenovanými klíči, nikoli číselnými indexy.
- Kontextové menu je odpovědností hostu — EDList žádné nedefinuje.
