# Caché ↔ .NET wire formáty

> Standard pro komunikaci mezi .NET (EDControls/aplikace) a Caché (serverové API třídy) přes
> MLRest. Závazné pro veškerou novou interní komunikaci. Legacy endpointy a cloud-facing API
> mají vlastní pravidla.

## Pravidlo v jedné větě

**IN: CVar pojmenované klíče. OUT: podle typu obsahu** (CVar skaláry pro triviální, JSON
v jednom slotu pro entitu, JSON multi-slot pro seznam, JSON envelope pro composite).

CVar je .NET wrapper nad nativní strukturou `^||ML(...)`. Pro vstupní parametry (typicky
1-5 plochých hodnot) je to nejjednodušší vzor - jeden řádek na obou stranách. U výstupu
záleží na složitosti: plochá data se vejdou do CVaru, strukturovaná potřebují JSON.

## Čtyři výstupní styly - kdy co

```
Co vracíš?
├── Pár plochých skalárů (max ~5 hodnot)
│   └── Styl 1: CVar named scalars
├── Jedna strukturovaná entita (vnořená pole, kolekce uvnitř)
│   └── Styl 2: JSON v jednom slotu
├── Seznam stejnotypových entit
│   └── Styl 3: JSON multi-slot (jeden řádek per slot)
└── Composite struktura (víc kolekcí + metadata, hierarchie)
    └── Styl 4: JSON envelope
```

Skelet metody je ve všech stylech stejný (`Include GENERAL` v hlavičce třídy):

```objectscript
ClassMethod Neco(ByRef sys As %String) As %Status
{
    $$$Init
    try {
        $$$FillParams
        ; ...
    } catch (ex) { $$$CatchErrRest }
    ret ret
}
```

## Styl 1 - CVar named scalars (triviální response)

**Použít kdy:** response je 1-5 plochých skalárních hodnot (string, číslo, bool, datum).

```objectscript
ClassMethod GetCurrentUser(ByRef sys As %String) As %Status
{
    $$$Init
    try {
        $$$FillParams
        s ^||ML("OUT","userId")   = userID
        s ^||ML("OUT","fullName") = $g(sys("U","fullName"))
        s ^||ML("OUT","isAdmin")  = 1
    } catch (ex) { $$$CatchErrRest }
    ret ret
}
```

```csharp
var output = mlRest.QueryOrThrow("ALVA.X.UserAPI", "GetCurrentUser", new CVar());

var userId   = output["userId"]?.Value?.ToString() ?? "";
var fullName = output["fullName"]?.Value?.ToString() ?? "";
var isAdmin  = output["isAdmin"]?.Value?.ToString() == "1";
```

Žádný JSON parse, žádný record - čistá plochá dvojice klíč/hodnota.

## Styl 2 - JSON v jednom slotu (jedna strukturovaná entita)

**Použít kdy:** response je jedna entita s vlastnostmi, vnořenými poli nebo kolekcí uvnitř.

```objectscript
ClassMethod LoadLinka(ByRef sys As %String) As %Status
{
    $$$Init
    try {
        $$$FillParams
        s linkaID = $g(^||ML("IN","linkaID"))

        s obj = ##class(%DynamicObject).%New()
        d obj.%Set("cisloLinky", "42")
        d obj.%Set("nazevLinky", "Praha-Brno")
        d obj.%Set("platiOd",    "2026-01-01")
        d obj.%Set("platiDo",    "")
        s ^||ML("OUT","linka") = obj.%ToJSON()
    } catch (ex) { $$$CatchErrRest }
    ret ret
}
```

```csharp
public record LinkaDetail(string CisloLinky, string NazevLinky, DateTime? PlatiOd, DateTime? PlatiDo);

var input = new CVar();
input["linkaID"] = new CVar(linkaID);
var output = mlRest.QueryOrThrow("ALVA.X.LinkaAPI", "LoadLinka", input);

var json = output["linka"]?.Value?.ToString() ?? "{}";
var linka = JsonSerializer.Deserialize<LinkaDetail>(json, JsonOpts);
```

## Styl 3 - JSON multi-slot (seznam entit)

**Použít kdy:** vracíš seznam entit stejného typu (řádky listu, položky kolekce).

```objectscript
ClassMethod LoadList(ByRef sys As %String) As %Status
{
    $$$Init
    try {
        $$$FillParams
        s agendaCode = $g(^||ML("IN","agendaCode"))

        s i = 0
        for každá linka {
            s obj = ##class(%DynamicObject).%New()
            d obj.%Set("value",    LinkaID)
            d obj.%Set("display",  cisloLinky_" "_nazevLinky)
            d obj.%Set("subtitle", "Platnost od "_platiOd)
            s i = $i(i)
            s ^||ML("OUT", i) = obj.%ToJSON()
        }
    } catch (ex) { $$$CatchErrRest }
    ret ret
}
```

```csharp
public record AgendaInstance(string Value, string Display, string? Subtitle);

var input = new CVar();
input["agendaCode"] = new CVar(agendaCode);
var output = mlRest.QueryOrThrow("ALVA.X.InstancesList", "LoadList", input);

var rows = new List<AgendaInstance>();
foreach (var kvp in output)
{
    if (kvp.Key == "ERR") continue;          // MLRest metadata, ne data
    var json = kvp.Value?.Value?.ToString();
    if (string.IsNullOrEmpty(json)) continue;
    var row = JsonSerializer.Deserialize<AgendaInstance>(json, JsonOpts);
    if (row is not null) rows.Add(row);
}
```

Pro listy EDListu multi-slot zápis abstrahuje Caché helper `ALVA.EDControls.EDList.List` -
jeho kontrakt drží [`../dotnet/EDList/CLAUDE.md`](../dotnet/EDList/CLAUDE.md).

## Styl 4 - JSON envelope (composite)

**Použít kdy:** response je composite struktura - víc kolekcí, metadata, hierarchie.
Typicky definice formuláře (Tab → Group → Field s validacemi a viditelností).

```objectscript
ClassMethod GetForm(ByRef sys As %String) As %Status
{
    $$$Init
    try {
        $$$FillParams
        s elementType = $g(^||ML("IN","elementType"))

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
    ret ret
}
```

```csharp
public record FormField(string Key, string Label, string Type, float? Min, float? Max);
public record FormGroup(string Name, List<FormField> Fields);
public record FormDefinition(string Title, List<FormGroup> Groups);

var runner = new RestCallRunner(() => services.RequireMLRest);   // v testu fake IRestCallRunner
var input = new CVar();
input["elementType"] = new CVar("TextElement");
var output = await runner.RunAsync(rest => rest.QueryOrThrow("ALVA.X.FormAPI", "GetForm", input));

var json = output["payload"]?.Value?.ToString() ?? "{}";
var form = JsonSerializer.Deserialize<FormDefinition>(json, JsonOpts);
```

IN zůstává u pojmenovaných klíčů jako u ostatních stylů, celý composite response jde jedním
slotem `"payload"`. EDControls na to helper nemá: volání mimo UI thread jde přes `IRestCallRunner`
(klon), ne přes `Task.Run` nad sdílenou instancí. Použít **jen pro composite endpointy** - u běžných
listů a entit zůstaň u Stylu 2/3, na Caché straně je to míň boilerplate.

## Filter konvence - kde žijí filter hodnoty

U list endpointů (Styl 3) stojí vedle běžných IN parametrů (kontext, paging, sort) ještě
**filter hodnoty** - pole z UI (`EDFilter` a její `FilterX` children, nebo dict předaný do
`EDList.LoadList(filter)`).

**Konvence:** filter hodnoty vždy pod sub-subscriptem `^||ML("IN","filter",klic)`. Klasické
parametry zůstávají top-level.

```
^||ML("IN","templateId") = "abc-123"           <- parametr volání (top level)
^||ML("IN","limit") = "50"                     <- paging
^||ML("IN","sortBy") = "name"                  <- sort
^||ML("IN","filter","agendaCode") = "MHD"      <- filter hodnota
^||ML("IN","filter","name") = "Šablona A"      <- filter hodnota
```

Čtení v Caché - per klíč, nebo generickým iterátorem:

```objectscript
s agendaCode = $g(^||ML("IN","filter","agendaCode"))

s key = ""
for {
    s key = $o(^||ML("IN","filter",key), 1, val)
    q:key=""
    ; ... process per filter key (val)
}
```

`EDList.LoadList(filter)` i `EDFilter` balí dict pod `filter` subscript samy, host nic
nepřevádí.

**Rozhodovací pravidlo:** je to query parametr (zužuje výsledek), nebo parametr volání /
konfigurace? Query → `filter` subscript. Volání, paging, sort, `includeArchived` → top level.

Mentální paralela s legacy: makro `$$$GetFiltr` plnilo lokální pole `filtr(klic) = hodnota`
parsováním delimiter stringu z plochých numerických slotů. Pojmenovaná struktura je stejný
model, jen bez parsovacího makra.

## Speciální případy mimo wire formát

**Binární data - `MLRest.BinarySave` / `BinaryLoad`.** Side-channel s GUID handle, Caché-side
uloženo do `$$$SYSBinary(GUID)`. Pro PDF, obrázky, velké blob payloady.

```csharp
string guid = mlRest.BinarySave(bytes);     // .NET -> GUID
byte[] data = mlRest.BinaryLoad(guid);      // GUID -> .NET
```

**Chybový stav.** `$$$Init` inicializuje `ret` i `err`, `$$$CatchErrRest` v catch bloku
nastaví `ret = "1~zpráva"`. .NET strana přes `QueryOrThrow(...)` vyhodí výjimku, kterou chytá
`EDViewModel.Command(...)` a ukáže přes `IPopupDisplayer`. Bez `$$$Init` se catch handler sám
shodí na `<UNDEFINED>` nad `err` - proto si ho TemplateDesigner, který `$$$Init` vědomě
nepoužívá, inicializuje sám (`TemplateDesigner/.claude/rules/cache-api.md`).

## Sdílený `JsonSerializerOptions` v .NET

```csharp
public static readonly JsonSerializerOptions JsonOpts = new()
{
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
};
```

Konvence: **camelCase** v Caché, **PascalCase** v C# recordu - `PropertyNameCaseInsensitive`
to spojí bez `[JsonPropertyName]` na každé property. Underscore se takhle nemapuje
(`agenda_code` → `AgendaCode` nefunguje), tam je dekorátor nutný.

## Checklist pro nový Caché endpoint

1. ☐ `ClassMethod` má `ByRef sys As %String`, tělo otevírá `$$$Init` a volá `$$$FillParams`
2. ☐ `try` / `catch (ex) { $$$CatchErrRest }` / `ret ret`
3. ☐ IN přes `$g(^||ML("IN","nazev"))` - nikdy `^||ML("IN",1)` jako strukturovaná data
4. ☐ OUT styl podle decision tree, konzistentně v rámci jedné metody
5. ☐ Pro Styl 2/3/4 `%DynamicObject.%ToJSON()` / `%DynamicArray.%ToJSON()`, nikdy `$lb()`
6. ☐ Pro list endpointy zvážit helper `ALVA.EDControls.EDList.List`
7. ☐ Žádné `_` v dot-notation - `obj.KEY_NAME` nefunguje, použít `obj.%Set("KEY_NAME", v)`
8. ☐ .NET strana má `record` per typ, parsuje `JsonSerializer.Deserialize<T>(json, JsonOpts)`
9. ☐ Pomalé volání ne synchronně v UI threadu - mimo něj přes `IRestCallRunner` (klon), nikdy holý
   `Task.Run` nad sdílenou instancí (souběžná volání si prohodí `RestStatus`)
