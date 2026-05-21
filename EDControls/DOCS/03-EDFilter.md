# EDFilter — komponenta pro filtr listu

> WPF komponenta pro deklarativní filtr nad EDListem (a v budoucnu dalšími list-style komponentami).
> Auto-trigger reload při změně hodnoty kteréhokoliv pole; filter hodnoty se posílají do Caché
> jako pojmenované sub-subscripty `^||ML("IN","filter",klic)` — viz [`01-wire-formats.md`](01-wire-formats.md#filter-konvence---kde-zijí-filter-hodnoty).

---

## Kdy EDFilter použít

✅ Pro tyto případy:
- Dialog s `EDListem`, kde uživatel filtruje seznam přes 1+ UI ovládací prvek (combo box pro agenda/typ/stav, checkbox pro „pouze aktivní", v budoucnu text search field, date range, atd.)
- Filter hodnoty se mají poslat do Caché jako součást `LoadList(filter)` volání
- Auto-update listu při změně filter pole je žádoucí

❌ NE pro tyto případy:
- Inline search v EDListu (na to má EDList vlastní client-side search box přes `Title` property)
- Filtrace čistě client-side bez round-tripu do Caché (na to stačí `CollectionViewSource.Filter`)
- Filter nad jiným typem dat než `EDList` (zatím; v budoucnu lze rozšířit, viz extensibility)

---

## Quickstart — minimální použití

### XAML

```xml
<Window xmlns:el="clr-namespace:EDControls.EDList;assembly=EDControls"
        xmlns:edf="clr-namespace:EDControls.EDFilter;assembly=EDControls">
    <DockPanel>
        <el:EDFilter DockPanel.Dock="Top" Target="{Binding ElementName=TheList}">
            <edf:FilterCombo Key="agendaCode" Label="Agenda"
                             ItemsSource="{Binding AgendaFilters}"
                             SelectedValuePath="Code"
                             DisplayMemberPath="Label"/>
        </el:EDFilter>

        <el:EDList x:Name="TheList"/>
    </DockPanel>
</Window>
```

### Code-behind

```csharp
public partial class MyDialog : Window
{
    public MyDialog(MyDialogViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();

        TheList.CacheClass = "ALVA.MyProject.API.MyList";
        TheList.HostId = "MyProject.MyList";

        Loaded += (_, _) =>
        {
            TheList.InitializeList();
            // EDFilter sám zavolá TheList.LoadList(filter) když má FilterCombo
            // s defaultní/initial hodnotou. Pokud filter pole je prázdné,
            // zavolá LoadList() bez filtru.
        };
    }
}
```

Tím máš funkční list s filtrem. Změna v `FilterCombo` automaticky vyvolá reload se správným filter dict.

---

## Architektura — jak to funguje

### Kontrolky a životní cyklus

`EDFilter` je `UserControl` s `[ContentProperty(nameof(Fields))]` který:
- Vykresluje **horizontální panel** s child `FilterX` poli (vedle sebe)
- Při `Loaded` sebere initial hodnoty ze všech `FilterX` children a zavolá `Target.LoadList(filter)` (initial load)
- Subscribuje na `ValueChanged` event každé `FilterX` field
- Při změně hodnoty kteréhokoliv pole **rebuilduje filter dict** a znovu volá `Target.LoadList(filter)` (auto-trigger)

`FilterX` field je abstraktní bázová třída (`FilterFieldBase`). Konkrétní typy:
- `FilterCombo` — combobox z dat (v1)
- (v2+) `FilterText`, `FilterCheck`, `FilterDate`, `FilterDateRange`, `FilterCheckedCombo` (multi-select)

Každé pole zná svůj **filter klíč** (`Key` property) a aktuální **hodnotu** (typed property, např. `SelectedValue`).

### Tok dat

```
1. User vybere v FilterCombo → SelectedValue="MHD"
2. FilterCombo fires ValueChanged event nahoru do EDFilter
3. EDFilter sebere hodnoty ze všech FilterX children:
   filter = { "agendaCode" = "MHD" }
   (prázdné hodnoty se vynechávají)
4. EDFilter volá Target.LoadList(filter)   ← Target je referenced EDList
5. EDList přes IMLRest pošle CVar:
   input["filter"]["agendaCode"] = "MHD"
6. Caché global po HTTP:
   ^||ML("IN","filter","agendaCode") = "MHD"
7. Caché endpoint čte:
   s agendaCode = $g(^||ML("IN","filter","agendaCode"))
   if agendaCode '= "" { filter rows by agenda }
8. Vrátí řádky, EDList vykreslí
```

---

## API — EDFilter

### Properties

| Property | Typ | Popis |
|---|---|---|
| `Target` | `EDList` (DP) | EDList komponenta, kterou tento filter řídí. **Povinné.** |
| `AutoTrigger` | `bool` (default `true`) | Pokud `true`, změna hodnoty pole okamžitě volá `Target.LoadList(filter)`. Pokud `false`, vyžaduje stisk Refresh tlačítka (zobrazí se v toolbaru). V1 podporuje pouze `true`; `false` je placeholder pro v2. |

### Content

`EDFilter` přijímá 0+ `FilterFieldBase` children jako XAML content (přes `[ContentProperty(nameof(Fields))]`). Layout: `WrapPanel` horizontálně (přetečení do dalšího řádku při užším okně).

### Veřejné metody

| Metoda | Popis |
|---|---|
| `BuildFilterDict()` | Vrátí `Dictionary<string, string>` s aktuálními neprázdnými hodnotami z FilterX children. Volá se interně, ale je veřejná pro custom workflow |
| `Refresh()` | Manuální trigger reload (pro `AutoTrigger=false` v budoucnu) |

### Eventy

| Event | Popis |
|---|---|
| `FilterChanged` | Vyvoláno po každém rebuilds filter dict (před voláním `Target.LoadList`). Host může observovat pro vlastní logiku. |

---

## API — FilterFieldBase a konkrétní pole

### `FilterFieldBase` (abstract)

Společný kontrakt všech filter polí. Hostovatelné jako child v `EDFilter`.

| Property | Typ | Popis |
|---|---|---|
| `Key` | `string` (DP) | Klíč, pod kterým hodnota poletí do Caché — `^||ML("IN","filter",Key)`. **Povinné.** |
| `Label` | `string?` (DP) | Vizuální popisek nad/vedle pole. Pokud null, label se nevykresluje |

| Metoda / Event | Popis |
|---|---|
| `GetValue(): string` | Vrátí aktuální hodnotu jako string (prázdná pokud žádná). Implementuje konkrétní subclass |
| `ValueChanged` | Event vyvolaný při změně hodnoty. EDFilter ho subscribuje |

> **Šířku pole** lze nastavit standardně přes WPF `Width` / `MinWidth` / `MaxWidth` atributy (zděděno z `UserControl`).

### `FilterCombo` — combo box (v1)

```xml
<edf:FilterCombo Key="agendaCode" Label="Agenda"
                 ItemsSource="{Binding AgendaFilters}"
                 SelectedValuePath="Code"
                 DisplayMemberPath="Label"
                 MinWidth="200"/>
```

| Property | Typ | Popis |
|---|---|---|
| `Key`, `Label` | (zděděno z `FilterFieldBase`) | |
| `ItemsSource` | `IEnumerable` (DP) | Zdroj dat pro combo. Aplikace si binding zařídí přes VM |
| `SelectedValuePath` | `string` (DP) | Cesta k value property položky (typicky `Code` / `Id`) |
| `DisplayMemberPath` | `string` (DP) | Cesta k display property položky (typicky `Label` / `Name`) |
| `SelectedValue` | `object?` (DP, TwoWay) | Aktuální výběr. Null/empty → filter klíč se neposílá (= žádný filter) |

> **Prázdná volba** („Všechny agendy") se dnes řeší tak, že VM přidá speciální item s `Code=null` do `ItemsSource`. Vyhrazený `Placeholder` DP plánován pro v2.

`GetValue()` vrátí `SelectedValue?.ToString() ?? ""`.

`ValueChanged` se vyvolá při změně `SelectedValue`.

---

## Wire formát — co teče do Caché

### Co posílá EDFilter

EDFilter sestaví `Dictionary<string, string>` z **neprázdných** hodnot child polí a předá `Target.LoadList(filter)`. EDList interně vytvoří CVar s vnořením pod `filter`:

```csharp
var input = new CVar();
input["filter"]["agendaCode"] = new CVar("MHD");
input["filter"]["name"] = new CVar("Šablona A");
// plus event. top-level parametry: kontext, paging, sort
```

### Caché global po přenosu

```
^||ML("IN","filter","agendaCode") = "MHD"
^||ML("IN","filter","name") = "Šablona A"
```

### Caché endpoint — čtení

```objectscript
ClassMethod LoadList(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s agendaCode = $g(^||ML("IN","filter","agendaCode"))
        s name       = $g(^||ML("IN","filter","name"))

        s list = ##class(ALVA.EDControls.EDList.List).%New()
        s id = ""
        for {
            s id = $o(^MyData(id))
            q:id=""
            ; Apply filter:
            if agendaCode '= "", $g(^MyData(id,"agenda")) '= agendaCode continue
            if name '= "", '($f($zcvt($g(^MyData(id,"name")),"L"), $zcvt(name,"L"))) continue

            s row = list.AddRow(id)
            d row.%Set("id", id)
            ; ...
        }
        d list.WriteRowsToOut()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}
```

**Pravidlo: prázdná filter hodnota = filter klíč se neposílá.** Caché `$g()` vrací `""` pro neexistující klíč, takže `if klic '= ""` patřičně skip.

---

## Auto-trigger vs Manual mode

**V1 podporuje pouze auto-trigger** (default `AutoTrigger=true`). Změna kteréhokoliv pole okamžitě vyvolá reload.

**Pro v2 plánováno** `AutoTrigger=false`:
- EDFilter zobrazí Refresh tlačítko v pravém horním rohu
- Hodnoty se sbírají, ale `LoadList(filter)` se zavolá až po stisku Refresh
- Použít pro complex formuláře (5+ polí), kde každá změna by zaplavila server

V1 řešení pro complex případy: nepoužívat EDFilter, řešit ad-hoc v hostujícím dialogu jako dosud. Pro 1-3 fields stačí auto-trigger.

---

## Extensibility — jak přidat nový typ filter pole

Implementace nového `FilterX` typu v `EDControls/dotnet/EDFilter/`:

1. **Dědit `FilterFieldBase`** (abstract WPF UserControl)
2. **Definovat DPs** pro typ-specifický config (např. `MinValue/MaxValue` pro `FilterNumber`)
3. **Implementovat `GetValue(): string`** — jak se hodnota převede na string pro filter dict
4. **Fire `ValueChanged`** event při změně hodnoty (typicky v PropertyChanged callback DP)
5. **XAML template** — vykreslení v EDFilter panelu (UserControl nebo Style)

Plánované budoucí typy (v2+, dle reálné potřeby):

| Typ | Použití | GetValue → string |
|---|---|---|
| `FilterText` | Substring search | `Text` (případně lowercase) |
| `FilterCheck` | Boolean filter | `"1"` / `""` (false → neposílat) |
| `FilterDate` | Single datum | `"2026-01-15"` ve ISO |
| `FilterDateRange` | Datum od-do | dvě filter klíče: `Key+"From"`, `Key+"To"` (split internally) |
| `FilterCheckedCombo` | Multi-select | Comma-separated values nebo nested filter values |
| `FilterCodebook` | Číselník picker | Vybraný kód |

Žádný z nich není v v1 — implementujeme **až bude reálný consumer**.

---

## Co je v v1 záměrně mimo scope (YAGNI)

| Feature | Stav | Kdy zvážit |
|---|---|---|
| `AutoTrigger=false` (manual Refresh button) | v2 | Až bude první complex consumer s 5+ poli |
| Variants (uložené pojmenované filtry) | v3 | Až bude power-user request na opakované filtry |
| `Default` button (reset filter) | v2 | Drobnost, kdykoli |
| Settings (SaveOnClose, AutoRefresh timer) | v4 | Niche, neoptičujeme |
| Container pattern (libovolné WPF kontrolky v ContentPanel) | v2 nebo paralelní `EDFilterClassic` | Až bude consumer co potřebuje custom UserControl uvnitř filteru |

Vše plánované je **aditivně přidatelné bez breaking change** v1 API.

---

## Vztah k EDList

EDFilter je **opt-in nadstavba** nad EDListem. Bez EDFilter funguje EDList normálně (manuálně se volá `LoadList()` nebo `LoadList(filter)` z code-behind dialogu — jak je dnes v `CacheTemplatePickerDialog` před L7 refactorem). S EDFilter se to dělá deklarativně.

**Co se nemění v EDList API:** `LoadList(IReadOnlyDictionary<string,string>? filter)` zůstává stejné. EDFilter ho jen volá automaticky.

**Změna v EDList wire encoding:** filter dict se nově balí pod `input["filter"]` sub-subscript místo flat `input[key]`. To je interní změna, host dialog se nemění. Caché endpoints volané z EDList **musí** číst přes `$g(^||ML("IN","filter","klic"))` místo dřívějšího `$g(^||ML("IN","klic"))` — viz section v `01-wire-formats.md` „Filter konvence".

---

## Referenční implementace (po L7 refactoru)

| Dialog | Caché backing | EDFilter použití |
|---|---|---|
| `CacheTemplatePickerDialog` | `ALVA.TemplateDesigner.API.TemplatesList` | 1× `FilterCombo` pro agenda code (Code/Label z `AgendaFilters` v VM) |

---

## Versioning a kompatibilita

EDFilter v1 API:
- `EDFilter` UserControl s `Target`, `AutoTrigger` DPs
- `FilterFieldBase` abstract base + `FilterCombo` concrete

Plánované v2+ aditivní rozšíření (bez breaking changes):
- `AutoTrigger=false` mode + Refresh button
- `FilterText`, `FilterCheck`, `FilterDate`, `FilterDateRange`, `FilterCheckedCombo`, `FilterCodebook`
- `Default` button + `OnReset` event

Breaking change vyžaduje verzování + migrate guide v této doc.
