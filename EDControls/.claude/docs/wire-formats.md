# MLRest wire formáty — standard

> **Závazný standard pro nové Caché API metody v `EDControls` i `TemplateDesigner`.**
> Vznikl konsolidací po auditu z 2026-05-15.

## Pravidlo v jedné větě

**CVar pojmenované klíče na IN, JSON přes `%ToJSON()` na OUT.**

```csharp
// .NET
var input = new CVar();
input["scope"] = new CVar(scope);
input["key"]   = new CVar(key);
CVar output = mlRest.QueryOrThrow("ALVA.X.Y", "Method", input);
var json = output["data"]?.Value?.ToString();
var node = JsonNode.Parse(json);
```

```objectscript
// Caché
ClassMethod Method(ByRef sys As %String) As %Status
{
    try {
        $$$FillParams
        s scope = $g(^||ML("IN","scope"))
        s key   = $g(^||ML("IN","key"))

        s result = ##class(%DynamicObject).%New()
        s result.something = "value"
        s ^||ML("OUT","data") = result.%ToJSON()
    } catch (ex) { $$$CatchErrRest }
    q $$$OK
}
```

---

## Proč ne JSON i na IN

CVar **není pokus imitovat JSON**. Je to 1:1 zrcadlo nativní Caché datové struktury — `^||ML("IN","scope")` je přirozený sparse global subscript, který existuje ~30 let. CVar mapuje rovnou na to, co Caché už **je**.

Pro typické trivální parametry (`scope`, `templateId`, `agendaCode`) JSON přidává overhead bez výhody:

```objectscript
; dnes (CVar named IN) — 1 řádek per parametr:
s scope = $g(^||ML("IN","scope"))

; JSON-everywhere — 3 řádky + parse cost per volání:
s in = ^||ML("IN",1)
s obj = ##class(%DynamicObject).%FromJSON(in)
s scope = obj.scope
```

Co bychom navíc obětovali:
- **`CList` → `$lb()`** — kompaktní Caché-native list parsovatelný přes `$piece`/`$list`. JSON pole vyžaduje `%FromJSON` parse.
- **`MLRest.BinarySave(bytes)`** — `byte[]` jde side-channelem s GUID handle, neprochází serializací. JSON ekvivalent = base64 (+33 % payload).
- **Typová transparence v Caché** — `$g(^||ML(...))` vrátí string nebo číslo nativně; JSON deserializaci je třeba znát strukturu.

## Proč JSON na OUT

OUT je obvykle strukturovaný payload (seznam objektů, hierarchie). Tam JSON vyhrává:
- Jeden `%ToJSON()` call zaserializuje libovolnou hierarchii.
- `.NET` strana má typový parser (`JsonNode`, `JsonSerializer.Deserialize<T>`).
- Čitelnost na wire (debugger, log, network monitor).
- Versionable / forward-compatible (přidání pole nerozbije starého klienta).

Pro skalární návrat (single string, single number) je legitimní vrátit jako prostý string v `^||ML("OUT","klic")` bez JSON wrapperu.

---

## Audit stávajících endpointů (2026-05-15)

| Endpoint | IN | OUT | Status |
|---|---|---|---|
| `ALVA.TemplateDesigner.API.Templates` | CVar named | JSON `%ToJSON()` | ✅ standard |
| `ALVA.TemplateDesigner.Agendas.*` (AgendaAPI subclasses) | CVar named | JSON `%ToJSON()` | ✅ standard |
| `ALVA.EDControls.UserData` | CVar named | JSON string v `OUT["data"]` | ✅ standard |
| `ALVA.TemplateDesigner.API.TemplatesList` | **`object[]` + delimiter** | JSON `%ToJSON()` | ⚠️ IN out-of-line |
| `ALVA.EDControls.EDInputForm.Form` | CVar named | **`$lb()` legacy CVar wire** | ⚠️ OUT out-of-line |
| `ALVA.LIBRARY.Form.SaveParams` / `LoadParams` | `object[]` + delimiter | `object[]` + delimiter | legacy — sdíleno s VB moduly, ponecháno |

## Co dotáhnout (ne nutně teď)

1. **`TemplatesList` IN** — nedokončená migrace EDList prototypu. Fix = přepnout `EDList.LoadList(filter)` v `EDControls/dotnet/EDList/EDList.xaml.cs` na CVar named keys (cca 3 řádky). Prerekvizita pro `EDFilter` komponent.
2. **`EDInputForm.Form` OUT** — vrací `$lb(...)` per řádek, parsuje se přes pozičné `CList.GetString(n)`. Samostatný refaktor — vrátit JSON pole objektů, na .NET straně `JsonSerializer.Deserialize<List<...>>`.

## Co je legitimně out-of-scope

- `ALVA.LIBRARY.Form.SaveParams` / `LoadParams` — sdílíme s VB moduly přes EDISONMAIN2API. Změna by si vyžádala synchronní deploy do legacy ekosystému; výhody (konzistence) nevyváží riziko.
- Případy, kdy je payload jediná binárka — `MLRest.BinarySave/Load` zůstává správná cesta.

---

## Checklist pro nový Caché API endpoint

1. ☐ ClassMethod má `ByRef sys As %String`, volá `$$$FillParams`, končí `try/catch (ex) { $$$CatchErrRest } q $$$OK`
2. ☐ IN — pojmenované klíče: `$g(^||ML("IN","name"))`, nikdy `^||ML("IN",1)` jako strukturovaná data
3. ☐ OUT — pro strukturovaný payload `^||ML("OUT","klic") = obj.%ToJSON()`; pro skalár `^||ML("OUT","klic") = hodnota`
4. ☐ .NET strana — `mlRest.QueryOrThrow(class, method, cvar)` s CVar overloadem, nikoli `object[]`
5. ☐ .NET strana — pro JSON OUT používá `JsonNode.Parse(out["klic"]?.Value?.ToString())` nebo `JsonSerializer.Deserialize<T>(...)`
6. ☐ Žádné `$lb()` ve výstupu kromě legacy
7. ☐ Žádné `_` v dot-notation Caché kódu (`obj.KEY_NAME` nefunguje — `_` je konkatenace; použij `obj.%Set("KEY_NAME", v)`)

---

## Pojmy

- **CVar** — .NET wrapper (`ALWA.SYS.CVar`) nad hierarchickým `^||ML(...)` globálem. Mapuje subscripty 1:1.
- **CList** — `ALWA.SYS.CList`, mapuje na Caché `$lb()` (ordered list, binary encoding). Pro nový kód preferuj JSON pole; `CList` přijatelný v legacy.
- **`$$$FillParams`** — makro v `Include GENERAL`, naplní `IdFa`, `dataDB`, `del`, `userID`, `GUID` z `sys` parametru.
- **`$$$CatchErrRest`** — makro pro standardní error wrapper; nastaví `ret = "1~message"` pro `ThrowIfError()` v .NET.
- **R3** v `TemplateDesigner/.claude/docs/roadmap.md` — historicky řeklo „JSON wire format, ne CVar". Tato doc to zpřesňuje: JSON na OUT, CVar named na IN.
