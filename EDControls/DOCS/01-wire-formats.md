# Caché ↔ .NET wire formáty

> Standard pro komunikaci mezi .NET (EDControls/aplikace) a Caché (server API třídy) přes MLRest.
> Závazné pro veškerou novou interní komunikaci. Legacy endpointy a cloud-facing API mají vlastní pravidla.

---

## Pravidlo v jedné větě

**IN: CVar pojmenované klíče. OUT: podle typu obsahu (CVar scalars pro triviální / JSON v slotu pro entitu / JSON multi-slot pro list / JSON envelope pro composite).**

---

## Background — proč zrovna toto

CVar je .NET wrapper nad nativní Caché strukturou `^||ML(...)` globálu. Pro **vstupní parametry** (typicky 1–5 plochých hodnot jako `agendaCode`, `templateId`) je to nejjednodušší vzor — jeden řádek na obou stranách.

Pro **výstupní data** záleží na složitosti. Plochá data (scalar návrat) se vejde do CVaru; strukturovaná (entita s vnořenými poli) potřebuje JSON; seznam entit potřebuje opakovaný JSON.

---

## Čtyři výstupní styly — kdy co použít

### Decision tree

```
Co vracíš?
├── Pár plochých skalárů (max ~5 hodnot)
│   └── ✅ Styl 1: CVar named scalars
│
├── Jedna strukturovaná entita (vnořená pole, kolekce uvnitř)
│   └── ✅ Styl 2: JSON v jednom slotu
│
├── Seznam stejnotypových entit
│   └── ✅ Styl 3: JSON multi-slot (jeden řádek per slot)
│
└── Composite struktura (více kolekcí + metadata, hierarchie)
    └── ✅ Styl 4: JSON envelope (opt-in pro Tier 3+ kompozitní formuláře)
```

---

## Styl 1 — CVar named scalars (triviální response)

**Použít kdy:** response je 1–5 plochých skalárních hodnot (string, number, bool, datum).

### Příklad: vrátit informace o aktuálním uživateli

**Caché:**
```objectscript
ClassMethod GetCurrentUser(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s ^||ML("OUT","userId")   = userID
        s ^||ML("OUT","fullName") = $g(sys("U","fullName"))
        s ^||ML("OUT","isAdmin")  = 1
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}
```

**.NET:**
```csharp
var output = mlRest.QueryOrThrow("ALVA.X.UserAPI", "GetCurrentUser", new CVar());

var userId   = output["userId"]?.Value?.ToString() ?? "";
var fullName = output["fullName"]?.Value?.ToString() ?? "";
var isAdmin  = output["isAdmin"]?.Value?.ToString() == "1";
```

**Záhody:** žádný JSON parse, žádný record, čistá flat dvojice klíč/hodnota. Krátká implementace.

---

## Styl 2 — JSON v jednom slotu (jedna strukturovaná entita)

**Použít kdy:** response je jedna entita s vlastnostmi, vnořenými poli, nebo kolekcí uvnitř.

### Příklad: načíst detail linky

**Caché:**
```objectscript
ClassMethod LoadLinka(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s linkaID = $g(^||ML("IN","linkaID"))

        s obj = ##class(%DynamicObject).%New()
        d obj.%Set("cisloLinky", "42")
        d obj.%Set("nazevLinky", "Praha–Brno")
        d obj.%Set("platiOd",    "2026-01-01")
        d obj.%Set("platiDo",    "")
        s ^||ML("OUT","linka") = obj.%ToJSON()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}
```

**.NET:**
```csharp
// 1) Definuj record pro entitu (1× per typ):
public record LinkaDetail(string CisloLinky, string NazevLinky, DateTime? PlatiOd, DateTime? PlatiDo);

// 2) Volání + deserialize:
var input = new CVar();
input["linkaID"] = new CVar(linkaID);
var output = mlRest.QueryOrThrow("ALVA.X.LinkaAPI", "LoadLinka", input);

var json = output["linka"]?.Value?.ToString() ?? "{}";
var linka = JsonSerializer.Deserialize<LinkaDetail>(json, JsonOpts);
```

### O recordech v krátkosti

`record` v C# je třída s automaticky generovaným konstruktorem, vlastnostmi (immutable přes `init` setters), value-based `Equals`/`GetHashCode`, `ToString` a `Deconstruct`. Hodí se pro **data-bearing typy** (DTO, kontrakty). Jeden řádek deklarace nahradí ~30 řádků manuální třídy.

```csharp
// Tohle:
public record LinkaDetail(string CisloLinky, string NazevLinky, DateTime? PlatiOd);

// Je ekvivalent ručního:
public sealed class LinkaDetail : IEquatable<LinkaDetail>
{
    public string CisloLinky { get; init; }
    public string NazevLinky { get; init; }
    public DateTime? PlatiOd  { get; init; }
    // Plus konstruktor, Equals, GetHashCode, ToString, Deconstruct…
}
```

`JsonSerializer.Deserialize<T>` automaticky zmapuje JSON → instance recordu podle názvů vlastností. **Žádný manuální parsing per pole.**

---

## Styl 3 — JSON multi-slot (seznam entit)

**Použít kdy:** vracíš seznam entit stejného typu (řádky listu, položky kolekce).

### Příklad: list dostupných linek pro agendu

**Caché:**
```objectscript
ClassMethod LoadList(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s agendaCode = $g(^||ML("IN","agendaCode"))

        s i = 0
        for každá linka {
            s obj = ##class(%DynamicObject).%New()
            d obj.%Set("value",    LinkaID)
            d obj.%Set("display",  cisloLinky_" — "_nazevLinky)
            d obj.%Set("subtitle", "Platnost od "_platiOd)
            s i = $i(i)
            s ^||ML("OUT", i) = obj.%ToJSON()
        }
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}
```

**.NET:**
```csharp
public record AgendaInstance(string Value, string Display, string? Subtitle);

var input = new CVar();
input["agendaCode"] = new CVar(agendaCode);
var output = mlRest.QueryOrThrow("ALVA.X.InstancesList", "LoadList", input);

var rows = new List<AgendaInstance>();
foreach (var kvp in output)
{
    if (kvp.Key == "ERR") continue;
    var json = kvp.Value?.Value?.ToString();
    if (string.IsNullOrEmpty(json)) continue;
    var row = JsonSerializer.Deserialize<AgendaInstance>(json, JsonOpts);
    if (row is not null) rows.Add(row);
}
```

**Helper class `ALVA.EDControls.EDList.List`** v Caché abstrahuje multi-slot writing — viz [`02-EDList.md`](02-EDList.md) sekce Caché contract.

---

## Styl 4 — JSON envelope (composite, opt-in pro Tier 3+)

**Použít kdy:** response je composite struktura — vícero kolekcí + metadata + hierarchie. Typicky: definice formuláře (Tab → Group → Field s validacemi a viditelností), template s metadaty + body + parametry.

### Příklad: načíst definici formuláře

**Caché:**
```objectscript
ClassMethod GetForm(ByRef sys As %String) As %Status
{
    s err = ""
    try {
        $$$FillParams
        s req = ##class(%DynamicObject).%FromJSON(^||ML("IN","payload"))
        s elementType = req.%Get("elementType")

        s resp = ##class(%DynamicObject).%New()
        s resp.title = "Vlastnosti " _ elementType
        s resp.groups = ##class(%DynamicArray).%New()

        s g = ##class(%DynamicObject).%New()
        d g.%Set("name", "Font")
        s g.fields = ##class(%DynamicArray).%New()
        d g.fields.%Push({"key":"FontSize","label":"Velikost","type":"DECIMAL","min":4,"max":200})
        d g.fields.%Push({"key":"Bold","label":"Tučně","type":"BOOL"})
        d resp.groups.%Push(g)

        s ^||ML("OUT","payload") = resp.%ToJSON()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}
```

**.NET:**
```csharp
public record GetFormRequest(string ElementType);
public record FormField(string Key, string Label, string Type, float? Min, float? Max);
public record FormGroup(string Name, List<FormField> Fields);
public record FormDefinition(string Title, List<FormGroup> Groups);

var resp = await MLRestJson.CallAsync<GetFormRequest, FormDefinition>(
    mlRest,
    "ALVA.X.FormAPI",
    "GetForm",
    new GetFormRequest("TextElement"));

// resp.Title, resp.Groups[0].Fields[0].Label — vše typované
```

**Helper `MLRestJson.CallAsync<TReq, TResp>`** je dostupný v EDControls a zabaluje serialize/deserialize do jednoho řádku. Použít **jen pro composite endpointy** — pro běžné listy/entity zůstaň u Stylu 2/3 (méně boilerplate na Caché straně).

---

## Filter konvence — kde žijí filter hodnoty

Pro **list endpointy** (Styl 3) existuje vedle běžných IN parametrů (kontext, paging, sort) navíc kategorie **filter hodnot** — pole z UI filter komponenty (`EDFilter` + její `FilterX` children, nebo manuálně předaný `IReadOnlyDictionary` do `EDList.LoadList(filter)`).

**Konvence:** filter hodnoty vždy žijí pod sub-subscriptem `^||ML("IN","filter",klic)`. Klasické parametry zůstávají top-level.

### Příklad

```
^||ML("IN","templateId") = "abc-123"           ← parametr volání (top level)
^||ML("IN","limit") = "50"                     ← paging
^||ML("IN","sortBy") = "name"                  ← sort
^||ML("IN","filter","agendaCode") = "MHD"      ← filter hodnota
^||ML("IN","filter","name") = "Šablona A"      ← filter hodnota
^||ML("IN","filter","active") = "1"            ← filter hodnota
```

### Čtení v Caché

Per-klíč (typický případ):
```objectscript
s agendaCode = $g(^||ML("IN","filter","agendaCode"))
s nameFilter = $g(^||ML("IN","filter","name"))
```

Generický iterátor (pro dynamický filter handler):
```objectscript
s key = ""
for {
    s key = $o(^||ML("IN","filter",key), 1, val)
    q:key=""
    ; ... process per filter key (val)
}
```

### Sběr v .NET

`EDList.LoadList(filter)` automaticky balí filter dict pod `filter` subscript v CVar:
```csharp
var filter = new Dictionary<string, string> {
    ["agendaCode"] = "MHD",
    ["name"] = "Šablona A"
};
TheList.LoadList(filter);
// → ^||ML("IN","filter","agendaCode") = "MHD"
// → ^||ML("IN","filter","name") = "Šablona A"
```

`EDFilter` komponenta dělá totéž — sebere hodnoty ze svých `FilterX` children, sestaví dict, předá EDList.

### Kdy filter, kdy ne

| Hodnota | Kde |
|---|---|
| Kontext volání (`templateId` při Load, `agendaCode` při GetAvailableInstances) | **Top-level** — `^||ML("IN","klic")` |
| Filter (zužuje výsledek listu, hodnoty z EDFilter UI) | **Pod `filter`** — `^||ML("IN","filter","klic")` |
| Paging, sort, konfigurace (`limit`, `offset`, `sortBy`, `includeArchived`) | **Top-level** |

**Rozhodovací pravidlo:** „je to query parametr (zužuje výsledek) nebo parametr volání/konfigurace?". Query → filter subscript. Volání → top level.

### Mentální paralela s legacy

Legacy `$$$GetFiltr` makro naplnilo lokální Caché pole `filtr(klic) = hodnota` po parsing delimiter stringu z plochých numerických slotů. Naše pojmenovaná struktura `^||ML("IN","filter",klic) = hodnota` je **stejný mentální model**, jen bez parsing makra — pojmenované klíče jdou rovnou do globálu.

---

## Speciální případy mimo wire formát

### Binární data — `MLRest.BinarySave / BinaryLoad`

Side-channel s GUID handle. Caché-side automaticky uloženo do `$$$SYSBinary(GUID)`. Použít pro PDF, obrázky, velké blob payloady.

```csharp
string guid = mlRest.BinarySave(bytes);     // .NET → GUID
byte[] data = mlRest.BinaryLoad(guid);      // GUID → .NET
```

### Chybový stav

Caché straně použít makro `$$$CatchErrRest` v catch bloku. **Důležitě nastavit `s err = ""` na začátku metody** — makro samo proměnnou `err` neinicializuje a bez init by se sám catch handler shodil s `<UNDEFINED>`. Wire formát: chybový stav v `^||ML("OUT","ERR")` (automaticky přes makro). .NET strana přes `mlRest.QueryOrThrow(...)` vyhodí výjimku → chytá `EDViewModel.Command(...)` přes `IPopupDisplayer`.

---

## Sdílený `JsonSerializerOptions` v .NET

Pro konzistenci napříč EDControls:

```csharp
public static readonly JsonSerializerOptions JsonOpts = new()
{
    PropertyNameCaseInsensitive = true,   // Caché posílá camelCase, C# property je PascalCase
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
};
```

`PropertyNameCaseInsensitive = true` znamená že JSON klíč `agendaCode` mapuje na C# property `AgendaCode` (records typicky používají PascalCase, Caché camelCase). Není třeba dekorovat každou property `[JsonPropertyName(...)]`.

---

## Audit endpointů (stav k 2026-05-19)

| Endpoint | IN | OUT styl | R3 compliance |
|---|---|---|---|
| `ALVA.TemplateDesigner.API.Templates` (5 metod) | CVar named | Styl 2 (JSON v slotu) | ✅ |
| `ALVA.TemplateDesigner.API.TemplatesList` | CVar named | Styl 3 (JSON multi-slot) | ✅ |
| `ALVA.TemplateDesigner.API.InstancesList` | CVar named | Styl 3 (JSON multi-slot) | ✅ |
| `ALVA.TemplateDesigner.Agendas.*` (AgendaAPI subclasses) | CVar named | Styl 2/3 | ✅ |
| `ALVA.EDControls.UserData` | CVar named | Styl 2 (JSON string v `OUT["data"]`) | ✅ |
| `ALVA.EDControls.EDInputForm.Form` | CVar named | **Legacy `$lb()` poziční** | ⚠️ Refactor v Tier 3 |
| `ALVA.LIBRARY.Form.SaveParams / LoadParams` | `object[]` + delimiter | `object[]` + delimiter | ❌ Legacy, sdíleno s VB moduly — nedotýkat |

---

## Checklist pro nový Caché endpoint

1. ☐ ClassMethod má `ByRef sys As %String`, volá `$$$FillParams`
2. ☐ **`s err = ""`** na začátku těla metody (před `try`)
3. ☐ `try` / `catch (ex) { $$$CatchErrRest }` / `q $$$OK`
4. ☐ IN přes `$g(^||ML("IN","nazev"))` — nikdy `^||ML("IN",1)` jako strukturovaná data
5. ☐ OUT styl podle decision tree (Styl 1/2/3/4) — konzistentně v rámci jedné metody
6. ☐ Pro Styl 2/3/4 použít `%DynamicObject.%ToJSON()` / `%DynamicArray.%ToJSON()`, nikdy `$lb()` ve výstupu
7. ☐ Pro list endpointy zvážit `ALVA.EDControls.EDList.List` helper (writes columns + rows do slotů)
8. ☐ Žádné `_` v dot-notation Caché kódu — `obj.KEY_NAME` nefunguje, použít `obj.%Set("KEY_NAME", v)`
9. ☐ .NET strana má `record` pro každý typ (pro Styl 2/3/4), parsuje přes `JsonSerializer.Deserialize<T>(json, JsonOpts)`
10. ☐ Žádný synchronní `mlRest.Query(...)` v UI threadu — vždy `await Task.Run(...)` nebo `IMLRest.QueryOrThrowAsync` pokud existuje

---

## Co je `JsonOpts` v praxi (drobná převodní tabulka)

| JSON klíč v Caché | C# record property | Funguje? |
|---|---|---|
| `agendaCode` | `string AgendaCode` | ✅ (case insensitive) |
| `agendaCode` | `string agendaCode` | ✅ |
| `agenda_code` | `string AgendaCode` | ❌ — underscore nemapuje na PascalCase |
| `agenda_code` | `[JsonPropertyName("agenda_code")] string AgendaCode` | ✅ s explicit dekorátorem |

Doporučená konvence: **camelCase** v Caché, **PascalCase** v C# recordu — `PropertyNameCaseInsensitive` to spojí.
