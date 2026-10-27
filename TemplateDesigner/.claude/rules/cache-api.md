# Glob: CACHE/**

## 🚨 Nasazení `.cls`: editace nástrojem, ověření voláním serveru

`.cls` nasazuje na dev **PostToolUse hook** a ten reaguje **jen na Edit/Write**. Cokoli jiného
(skript z Bashe, `sed`, python) změní soubor v repu a **server nechá starý**.

- **Nikdy neupravuj `.cls` skriptem.** Ani „mechanicky" (mazání řádků, přidání dvou polí) - právě tam
  se to stalo dvakrát 4. 8. **Když se to stane, vrať soubor (`git checkout --`) a zapiš změnu znovu
  editačním nástrojem** - jen tak hook spustí harness a nasazení proběhne.
- ⚠️ **Ruční poslání cesty hooku NENASADÍ** (ověřeno 8. 9.). Volání
  `echo '{"tool_input":{"file_path":"<abs cesta>.cls"}}' | bash .claude/hooks/compile-objectscript.sh`
  skončí s kódem 0, bez chyby na stderr - a server dál vrací starou třídu. Do 8. 9. tady stálo jako
  doporučená záchrana; je to tedy **stejná past jako `cache_compile`** níž, jen tiší.
- ⚠️ **Skript může soubor rozbít tak, že se od té chvíle nenasadí NIC** (10. 9.). Python zápis
  s `encoding='utf-8-sig'` přidá souboru **BOM**, který v žádném `.cls` není; upload pak posílá
  soubor, jehož první řádek začíná neviditelným znakem, **překlad spadne a server drží poslední
  funkční verzi**. Navenek to vypadá jako „hook se nespustil": další editace nástrojem soubor
  poslušně nahrávají a taky se nepřeloží. Poznávací znak - `head -c 3 soubor.cls | xxd` vrátí
  `efbb bf` místo prvních písmen; ostatní `.cls` v repu BOM nemají.
- **`cache_compile` (MCP) to nespraví** a **lže uklidňujícím způsobem**: hledá zdroj pod
  `C:\DEV\EDISON`, takže u tohohle repa nahlásí `not-found-on-disk`, zkompiluje **starou serverovou
  kopii** a napíše „up-to-date".
- **Jak se to pozná:** testy jsou zelené a aplikace se chová podle starého kódu. Testy to nechytí -
  `%UnitTest` jede proti serveru, ale drift v parametrech/formulářích se v odtiscích neprojeví.
- **Ověřuj voláním, ne čtením.** „Zdroj v repu to má" ani „grep v nasazené metodě" nestačí:
  **zavolej agendu / dispatch a podívej se, co reálně vrátí** (`cache_query_cvar` na `GetParameters`,
  `Init`, …). To je jediná kontrola, která odpovídá na otázku „co uvidí klient".
  ⚠️ **Voláním ověř KAŽDOU třídu, kterou jsi změnil**, ne jednu za všechny - 5. 8. jsem ověřil
  legendu poznámek, ale ne seznam zastávek, a ten byl rozbitý (uživateli spadlo přidání prvku).

## 🚨 Uvozovka v textu se ZDVOJUJE

`"` uvnitř textového literálu ho **ukončí** - musí být `""`. Typicky to potká vysvětlivky:
`"… (výchozí „×")"` vypadá nevinně, ale ObjectScript to přečte jako konec textu, pak `)` mimo
literál a poslední `"` otevře nový → `<SYNTAX>` a **spadne to teprve uživateli při otevření
formuláře**. Správně: `"… (výchozí ""×"")"`.

Hlídá to `ObjectScriptSyntaxTests` (projede všechny `.cls` v repu). Nic jiného to nezachytí: rozbitá
třída se nasadí, .NET testy o Caché zdrojácích nevědí a `%UnitTest` kompiluje jen testovací package.
**Chyba se pak projeví jako Caché test padající bez hlášky** - na serveru zůstane stará verze třídy.

Kontrola má dvě části, protože chyba má dvě podoby:

1. **řádek končí uprostřed textu** (lichý počet uvozovek) - `s x = "neuzavřeno`
2. **uvozovek je sudě, ale text skončil uprostřed** - `d Neco(x, "„<" nevzniklo (" _ y _ ")")`.
   Tuhle půlku kontrola do 13. 8. neviděla. ⚠️ **Platí jen uvnitř závorek**: mimo ně je „text, mezera,
   slovo" legitimní podmíněný příkaz (`s:group'="" param.group = group`), takže tam se pravidlo
   neuplatní a chyba tohoto tvaru projde. V praxi vzniká v argumentu (hlášky testů, helpTexty).

## Caché namespace struktura

`ALVA.TemplateDesigner.*` se dělí **podle role třídy** do folder/namespace:

- **`ALVA.TemplateDesigner.API.*`** - obecná business logika TD projektu (cross-cutting). Příklady: `Sablony` (CRUD šablon), v budoucnu `UserPreferences`, `SectionTemplates`.
- **`ALVA.TemplateDesigner.Agendy.*`** - agenda implementations (data sources). Příklady: `BaseAgenda` (abstract base), `ZastavkoveJrMhd`, `Faktury`.
- **`ALVA.TemplateDesigner.EDList.*`** - dispatch třídy pro EDList komponentu (UI contract). Příklady: `Sablony` (backing pro CacheTemplatePickerDialog), `Instance` (backing pro InstancePickerDialog).
- **`ALVA.TemplateDesigner.EDInputForm.*`** - dispatch třídy pro EDInputForm komponentu (hlavní editační cesta elementů).
- **`ALVA.TemplateDesigner.EDInputGrid.*`** - dispatch třídy pro EDInputGrid (až bude potřeba).

**Klíčový princip:** folder/namespace 1:1 mapuje na **roli třídy**. Doménový název může opakovat napříč rolemi - `API.Sablony` (CRUD) vs `EDList.Sablony` (EDList dispatch nad šablonami) jsou jasně rozlišené namespace prefixem.

Folder = namespace prefix; class name musí odpovídat file path (InterSystems convention).

---

## Caché BaseAgenda architektura (implementováno)

Každá agenda (JR, FAKTURY, ...) má jednu Caché třídu dědící z abstraktního základu.
Třída slouží jako **kompletní API rozhraní** pro TemplateDesigner - design-time i print-time.

### Abstraktní základ - vynucuje kontrakt

```objectscript
Class ALVA.TemplateDesigner.Agendy.BaseAgenda Extends %RegisteredObject [ Abstract ]
{
    /// Vrátí DataSchema: skalární pole + skupiny tabulek (scope=table)
    ClassMethod GetSchema(ByRef sys As %String) As %Status [ Abstract ] {}

    /// Vrátí seznam dostupných Fixed šablon tabulek pro tuto agendu
    ClassMethod GetTableTemplates(ByRef sys As %String) As %Status [ Abstract ] {}

    /// Vrátí definici konkrétní šablony (sloupce + řádky)
    /// IN: key = klíč šablony (např. "JR_ZAHLAVI")
    ClassMethod GetTableTemplate(ByRef sys As %String) As %Status [ Abstract ] {}

    /// Vrátí data pro tisk: skaláry + kolekce řádků
    /// IN: parametry tisku (závisí na agendě)
    ClassMethod GetData(ByRef sys As %String) As %Status [ Abstract ] {}

    /// Vrátí vzorová data pro live preview v designeru
    /// OUT[1] = { "values": {...}, "rows": { "ZASTAVKY": [...] } }
    ClassMethod GetMockData(ByRef sys As %String) As %Status [ Abstract ] {}
}
```

Každá nová agenda **musí** implementovat všech pět metod - Caché kompilátor to ohlídá.

---

### Implementace per agenda

```objectscript
Class ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd Extends ALVA.TemplateDesigner.Agendy.BaseAgenda
{
    ClassMethod GetSchema(ByRef sys As %String) As %Status
    {
        s def = ##class(%DynamicObject).%New()

        // Skalární pole
        s fields = ##class(%DynamicArray).%New()
        s f = ##class(%DynamicObject).%New()
        s f.name = "CISLO_LINKY"  s f.label = "Číslo linky"  s f.type = "number"
        d fields.%Push(f)
        // ... další pole ...
        s def.fields = fields

        // Skupiny tabulek (scope=table)
        s groups = ##class(%DynamicArray).%New()
        s g = ##class(%DynamicObject).%New()
        s g.name = "Zastávky"  s g.scope = "table"  s g.collectionKey = "ZASTAVKY"
        s gFields = ##class(%DynamicArray).%New()
        s gf = ##class(%DynamicObject).%New()
        s gf.name = "POPIS"  s gf.label = "Název zastávky"  s gf.type = "text"
        d gFields.%Push(gf)
        s g.fields = gFields
        d groups.%Push(g)
        s def.groups = groups

        s ^||ML("OUT", $i(^||ML("OUT"))) = def.%ToJSON()
        q 1
    }

    ClassMethod GetTableTemplates(ByRef sys As %String) As %Status
    {
        s arr = ##class(%DynamicArray).%New()
        s t = ##class(%DynamicObject).%New()
        s t.key = "JR_ZAHLAVI"  s t.label = "Záhlaví JR"
        d arr.%Push(t)
        s ^||ML("OUT", $i(^||ML("OUT"))) = arr.%ToJSON()
        q 1
    }

    ClassMethod GetTableTemplate(ByRef sys As %String) As %Status
    {
        s key = $g(^||ML("IN", "key"))
        if key = "JR_ZAHLAVI" {
            // Sestaví a vrátí { "columns": [...], "rows": [...] }
            // viz wire formát níže
        }
        q 1
    }

    ClassMethod GetData(ByRef sys As %String) As %Status
    {
        // Načte parametry z IN, vrátí skaláry + kolekce
        // viz wire formát níže
        q 1
    }
}
```

---

### Wire formáty

#### GetSchema - OUT[1]
```json
{
  "fields": [
    { "name": "CISLO_LINKY", "label": "Číslo linky",  "type": "number" },
    { "name": "NAZEV",       "label": "Název linky",   "type": "text"   }
  ],
  "fieldCollections": [
    {
      "name": "Zastávky", "collectionKey": "ZASTAVKY",
      "fields": [
        { "name": "POPIS",   "label": "Název zastávky", "type": "text" },
        { "name": "CAS_ODJ", "label": "Čas odjezdu",   "type": "time" }
      ]
    }
  ]
}
```

#### GetTableTemplates - OUT[1]
```json
[
  { "key": "ZAHLAVI_LINKY",    "label": "Záhlaví linky" },
  { "key": "PLATNOST_DOPRAVCE","label": "Platnost a dopravce" }
]
```

#### GetTableTemplate - IN + OUT[1]
```
IN: key = "ZAHLAVI_LINKY"
```
```json
{
  "columns": [
    { "header": "Položka", "width": 80, "widthMode": "Mm", "alignment": "Left" },
    { "header": "Hodnota", "width": 60, "widthMode": "Mm", "alignment": "Right" }
  ],
  "rows": [
    { "cells": [{ "content": "Linka" },       { "content": "{CISLO_LINKY}" }] },
    { "cells": [{ "content": "Platnost od" }, { "content": "{DATUM_OD}", "format": "dd.MM.yyyy" }] }
  ]
}
```

#### GetMockData - OUT[1] (design-time preview)
```json
{
  "values": {
    "CISLO_LINKY": "42",
    "NAZEV": "Linka Praha–Brno"
  },
  "rows": {
    "ZASTAVKY": [
      { "POPIS": "Praha hl.n.", "CAS_ODJ": "8:00" },
      { "POPIS": "Pardubice",   "CAS_ODJ": "9:15" }
    ],
    "SPOJE": [
      { "CISLO_SPOJE": "1001", "CAS": "8:00" }
    ]
  }
}
```

#### GetData - IN + OUT (print-time)

Na rozdíl od ostatních metod může `GetData` vrátit **více stránek** (např. zastávkový JŘ MHD = 1 stránka per zastávka × směr). Každá `OUT[n]` je **jedna kompletní samostatná stránka** ve stejném formátu jako `GetMockData`:

```
IN:      parametry závislé na agendě (číslo linky, datum, ...)
OUT[1] = { "values": {...}, "rows": { "ZASTAVKY": [...], "POZNAMKY": [...] } }   ← stránka 1
OUT[2] = { "values": {...}, "rows": {...} }                                       ← stránka 2
...
OUT[N] = { "values": {...}, "rows": {...} }                                       ← stránka N
```

Každý OUT node je vlastní `%DynamicObject.%ToJSON()` → na .NET straně `CacheAgendaProvider.GetData` iteruje všechny OUT klíče (kromě `"ERR"`) a parsuje každý jako samostatný `DataContext`. Výsledek je `List<DataContext>` předaný print enginu.

**Pro agendu s jediným výstupem** (např. jedna faktura) stačí zapsat pouze `OUT[1]` - C# strana přijímá 1..N stránek.

**Instance bez jediné stránky** (nestandardní data) vrací místo prázdna `OUT("DUVOD_PRAZDNE")` = text
pro uživatele: co v datech chybí a co s tím. Všechny agendy JŘ přes `BaseAgenda`: `ZacitDuvodyPrazdne`
na začátku GetData, `q ..PrazdnaStranka("…")` místo prázdné stránky, `VydatDuvodPrazdne` na konci.
Návrhář z něj udělá hlášku-problém „nemá data, nevytiskne se" (náhled, kontrola, tisk víc linek).
⚠️ **Ne chyba přes `$$$ThrowStatus`**: výjimka by při tisku víc linek zastavila i ty s daty.

**Data, která linku vytisknout nedovolí** (linka v JŘ není, spoj MHD bez kalendáře, turnusy nejdou
načíst), hlásí agenda přes `..ChybaDat("co je špatně. Co s tím.")` z `BaseAgenda`: tisk se zastaví
(rozhodnutí Jan 26. 10.), návrhář text ukáže jako informaci s číslem linky, ne jako chybu programu.
Pozná ji podle obecné chyby 5001 (`CacheAgendaProvider.ChybaDat`), takže **technický text do
`$$$GeneralError` nepiš** - uživatel ho uvidí doslova.

---

### Wire formát pravidlo (R3)

**Veškerá komunikace mezi Caché API a .NET stranou je čistě JSON.** Caché vždy zapisuje `obj.%ToJSON()` do `^||ML("OUT", n)`, .NET čte `result[key]?.Value?.ToString()` a parsuje přes `JsonNode.Parse` / `JsonSerializer.Deserialize`.

**Co se NEpoužívá:**
- `$lb()` / `$listbuild` na wire formátu - pouze pro interní working globals (`^||TiskMHD("trasa",...)`), nikdy v `^||ML("OUT",...)`
- Numerické subscripty na `^||ML("OUT",...)` jako strukturovaná data (jen jako sekvenční indexy pro multi-page u `GetData`)
- Pipe-delimited nebo `del`-delimited stringy
- Smíšený CVar+JSON hybrid

**Proč:** JSON je autoreferenční (struktura popsaná schématem), verzovatelný, debugovatelný v síťovém monitoru, a mapuje se 1:1 na `%DynamicObject` v Caché a `JsonNode` v .NET. Nové API metody musí tento vzor následovat.

**⚠️ Past: nový server-side parametr sestavy** (hodnotu zpracovává `GetData`, ne klientský filtr)
musí mít v `GetParameters` příznak `serverSide` (`s pX.serverSide = 1`, u `AddBoolParam` poslední
argument) - jinak jeho změna v UI nespustí načtení dat a klik je no-op. Klient žádný seznam
takových parametrů nedrží (od 21. 9.), ptá se definice parametru. Bool parametry se na wire
posílají jako `"true"/"false"` a server je čte tolerantně přes `$zcvt(...,"L")` (starší klienti
posílali `"True"`). Labely enum parametrů jsou ZAMČENÉ - persistuje se zobrazovaný text
(viz `ux-conventions §8`).

**Volba, která patří k prvku, ale server ji musí znát** (dnes `DEFINICE_SLOUPCU`, `JAZYK`,
`TURNUSY_KE_SPOJUM`) **není parametr sestavy**: skládá se do IN vždy **čerstvě ze šablony**
v `ComposeGetDataParams` (nikdy z persistovaných hodnot - třída chyb `B24`), příznak `serverSide`
nedostane (patří do `InfrastructureKeys` v `ServerSideParamsDriftTests`) a **re-fetch při její změně drží
`CurrentWireSnapshot`** - do něj se nová volba musí přidat, jinak zaškrtnutí nic neudělá, dokud
uživatel nezmáčkne obnovení dat. Poznávací znak: „odpověď na tuhle otázku dává šablona, ne uživatel
před tiskem."

**Parametr, který má účinek jen s určitým sloupcem prvku**, nese `visibleWhenColumn` = klíč sloupce
(dnes parametry variant tras → `CAS_JIZDY_VARIANTY`) a návrhář ho schová, když sloupec žádný prvek
šablony neukazuje - uživatel má vidět jen nastavení pro to, s čím v šabloně pracuje (rozhodnutí Jan
5. 10.). ⚠️ **Samostatný klíč, ne nový tvar `visibleWhen`**: starší klient by neznámou podmínku
vyhodnotil jako nesplněnou a parametr schoval natrvalo, `visibleWhenColumn` jen ignoruje. Klíč sloupce
musí prvky znát (`ParametrySloupcemVariantTests`), jinak by se parametr schoval navždy.

**Parametr, který se týká jen některých instancí**, nese `visibleWhenListedIn` = název hodnoty v datech,
kterou `GetData` posílá jako pole názvů parametrů týkajících se načtené instance; panel ukáže jen ty
(dnes typy provozu linkového JŘ MHD → `TYPY_PROVOZU_LINKY`: firma jich má desítky, linka pár).
⚠️ Seznam **nesmí záviset na hodnotách parametrů** - vypnutý typ by z panelu zmizel a nešel zapnout.
Bez dat, bez té hodnoty a v tiskovém dialogu (tiskne víc instancí) se nic neschovává.

---

### Prezentační nápovědy (R3) - kolik vzhledu smějí nést data

**Princip:** data říkají CO (odjezd 14:35), šablona říká JAK (font, barva). **Prezentační nápověda** je
úzká výjimka: klíč, kterým data legitimně nesou kus VZHLEDU, protože z nich vyplývá (nízkopodlažní spoj
zelený, mezisoučet tučný). Pravidla:

- **Vždy volitelná, nikdy povinná.** Element ji *smí* respektovat; element, který ji nezná, ji ignoruje
  (nesmí kvůli ní spadnout ani vyžadovat její přítomnost).
- **Vizuální význam sémantické úrovně vlastní ŠABLONA, ne data.** Data řeknou „zvýraznění úrovně 3",
  šablona určí, jak úroveň 3 vypadá. (Výjimka: přímé barvy níže - legacy parita.)

**Povolený seznam v1** (schválené, produkované/konzumované dnes):

| Klíč | Kde | Hodnota | Sémantika |
|---|---|---|---|
| `FORE_COLOR` | řádek kolekce / položka (odjezd, anotace, poznámka) | `#AARRGGBB`, prázdné = dědit | barva textu buňky/značky; přímá barva (legacy parita - JŘ ji už používá pro barevné spoje/poznámky) |
| `BACK_COLOR` | dtto | `#AARRGGBB`, prázdné = dědit | barva pozadí |

Prázdná hodnota = „element/šablona rozhodne" (nepřebíjí kaskádu stylů). Dnes je čte `ScheduleElementBase`
(→ DepartureGrid minuty/anotace, NotesLegend značky).

**Vědomě odložené na F1** (rozšíří se dle reálného RG usage - REPORTGENERATOR je hlavní konzument,
zatím nemigrovaný; navrhovat teď naslepo = špatný tvar): sémantická úroveň zvýraznění (`ZVYRAZNENI` 0–5,
šablona určí vzhled úrovní), typ řádku (data / mezisoučet / nadpis skupiny), „opakuj tento řádek na další
straně" (mezisoučty), „text přes celou šířku". Rezervujeme **princip + konvenci** (viz níže), ne konkrétní klíče.

**Konvence pro budoucí nápovědy:** klíč VELKÝMI písmeny (jako pole); **sémantické před syrovým vzhledem**
(radši „typ řádku = mezisoučet" než napevno barva); přímé barvy (`*_COLOR`) jen tam, kde to legacy vyžaduje.

### Evoluce kontraktu (R3) - jen aditivně

Datový kontrakt agend (schema, řádkové kolekce, prezentační nápovědy) se smí měnit **výhradně přidáváním**:

- **Přidat nový klíč / pole / nápovědu = OK.** Starší klient/element ho ignoruje (dopředná kompatibilita).
- **Změnit význam existujícího klíče nebo ho odebrat = ZAKÁZÁNO** bez migrace. (Stejná filozofie jako
  JSON šablony + `TemplateMigrator`.)

Důsledek: agenda může kdykoli obohatit výstup, aniž rozbije existující šablony a elementy. Migrovaný RG
(F1/F2) přidává prezentační nápovědy podle tohoto pravidla, ne ad hoc vlastními dialekty.

---

### Názvosloví - přehled pojmů API agendy

| Anglický název v kódu | Český ekvivalent | Business význam | Příklad |
|---|---|---|---|
| `GetSchema` | GetSchema | Vrátí **definici dostupných dat** agendy - co existuje jako pole, ne konkrétní hodnoty | `{ fields: [{name:"CISLO_LINKY",...}], groups: [...] }` |
| `GetTableTemplates` | GetTableTemplates | Vrátí **seznam předdefinovaných layoutů** Fixed tabulek uložených v Caché | `[{key:"ZAHLAVI_LINKY", label:"Záhlaví linky"}]` |
| `GetTableTemplate` | GetTableTemplate | Vrátí **konkrétní předdefinovaný layout** - sloupce + předvyplněné řádky | `{ columns:[...], rows:[{cells:[{content:"{CISLO_LINKY}"}]}] }` |
| `GetData` | GetData | Vrátí **reálná data z DB** pro tisk; volaná print enginem za běhu | `{ CISLO_LINKY:"42", NAZEV:"Praha–Brno" }` + kolekce řádků |
| `GetMockData` | GetMockData | Vrátí **vzorová data** pro live preview v designeru; volaná design-time | stejná struktura jako GetData, ale s pevnými testovacími hodnotami |
| `fields` | skalární pole | Pole agendy kde každá hodnota existuje **právě jednou** na celou sestavu | `CISLO_LINKY`, `POPIS_DOPRAVCE`, `DATUM_PLATNOSTI_OD` |
| `fieldCollections` | tabulkové kolekce | Kolekce sad polí kde každá kolekce tvoří **seznam řádků** - zdroj dat pro Dynamic tabulku | kolekce `Zastávky` obsahuje pole `POPIS_ZASTAVKA`, `CAS_ODJEZDU`… |
| `collectionKey` | klíč kolekce | Identifikátor kolekce řádků v `fieldCollections`; designer ho zobrazuje jako volbu DataSource Dynamic tabulky | `"ZASTAVKY"`, `"SPOJE"` |
| `key` (v GetTableTemplates) | klíč šablony | Interní identifikátor Fixed šablony; předává se jako parametr do `GetTableTemplate` | `"ZAHLAVI_LINKY"`, `"PLATNOST_DOPRAVCE"` |

**Konvence klíčů šablon tabulek:** česky, velkými písmeny, podtržítko jako oddělovač, bez prefixu třídy/agendy. Příklady: `ZAHLAVI_LINKY`, `PLATNOST_DOPRAVCE`.

**Konvence collectionKey:** česky, velkými písmeny, množné číslo, podtržítko jako oddělovač. Příklady: `ZASTAVKY`, `SPOJE`, `POLOZKY_FAKTURY`.

---

### C# strana - konfigurace a provider

**templatedesigner.appsettings.json** (vlastní název kvůli sdílené složce `C:\Edison\`):
```json
{
  "agendas": [
    { "code": "MHD",     "label": "Zastávkový JŘ MHD", "cacheClass": "ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd" },
    { "code": "FAKTURY", "label": "Faktury",            "cacheClass": "ALVA.TemplateDesigner.Agendy.Faktury" }
  ]
}
```

**CacheAgendaProvider** - jeden provider pokrývá obě rozhraní:
```csharp
// Implementuje IDataSchemaProvider + ITableTemplateProvider
class CacheAgendaProvider : IDataSchemaProvider, ITableTemplateProvider
{
    string _cacheClass;   // "ALVA.TemplateDesigner.Agendy.ZastavkoveJrMhd"
    MLRest _mlRest;

    // IDataSchemaProvider
    DataSchema? GetSchema(string agendaCode)     // volá GetSchema
    IReadOnlyList<AgendaInfo> GetAvailableAgendas()

    // ITableTemplateProvider
    IReadOnlyList<TableTemplateInfo> GetAvailableTemplates()    // volá GetTableTemplates
    TableTemplateDefinition GetTemplate(string key)             // volá GetTableTemplate
}
```

**Wiring** (`Config/CacheSchemaWiring.cs`):
- Agendy zjistí **auto-discovery** ze serveru (`AgendyRegistr.ListAgendy` → `CacheAgendaRegistry`); pro každou vytvoří `CacheAgendaProvider(cacheClass, mlRest)`. `templatedesigner.appsettings.json` agendy nedefinuje.
- Fallback: `CompositeDataSchemaProvider` (Caché primary + JSON fallback ze `schemas/*.schema.json`)

---

### Přidání nové agendy - checklist

1. Vytvořit Caché třídu `ALVA.TemplateDesigner.Agendy.<Agenda> Extends ALVA.TemplateDesigner.Agendy.BaseAgenda`
2. Implementovat `GetSchema`, `GetTableTemplates`, `GetTableTemplate`, `GetData`, `GetMockData`
3. Nic neregistrovat ručně - agendu najde auto-discovery (`AgendyRegistr.ListAgendy`); v UI se nabízí až s `Released = 1`
4. Volitelně: přidat `schemas/<Agenda>.schema.json` jako offline fallback
   - `GetMockData` vrací JSON: `{ "values": { "POLE": "hodnota", ... }, "rows": { "KLIC": [ {...}, ... ] } }`

---

## Kódové konvence pro ObjectScript

### Operátory
1. **Vždy `&&` pro AND** - nikdy `&` (legacy syntaxe)
2. **Vždy `||` pro OR** - nikdy `!` (legacy OR syntaxe); negace v ObjectScript = `'` (apostrof), ne `!`

### Pojmenování
3. **Čitelné názvy - čím globálnější, tím důležitější.** Parametry metod, properties tříd, klíče globálů: vždy celá slova (`AutoPoznamkyRozdilTrasyAlgoritmus` místo `AutoPoznRozdTrsSpAlg`). Lokální temp proměnná použitá na jednom místě může být rozumná zkratka.
4. **Žádné `_` v názvech proměnných** - `_` je v ObjectScript operátor spojení stringů. Použít camelCase nebo celá slova.
5. **Jazyk: "czechglish"** - business logika česky (`pocetZastavek`, `aktualniSmer`, `casOdjezdu`), obecné programátorské vzory anglicky (`GetData`, `SetTrasa`, `BuildOdjezdy`). Ne `ZiskatData` ani `NastavitTrasu`.
6. **Žádné tajemné číselné indexy** v globálech - pojmenované klíče (`^||TiskMHD("trasa",SmerID,BodID)` místo `^||wg(1,2,3)`)

### Struktura kódu
7. **Žádná `@` indirection** - přímé reference (`^||TiskMHD(...)` místo `@wg@(...)`)
8. **Klíčová slova ve zkratkách** - `s`, `d`, `i`, `q`, `f` jsou OK (firemní standard)
9. **Konzistentní error handling** - `$$$CatchErrRest`; debug přes `^jm` jen při ladění, v produkci čisté
10. **`_` v dot-notation** - zakázáno (`obj.KEY_NAME` nefunguje); používat `obj.%Set("KEY_NAME", v)`
11. **Preferovat třídy + ClassMethody** - žádné routiny, makra ani jiné legacy konstrukty
12. **Komentáře jen uvnitř třídy** - žádné komentáře mimo blok `Class ... { }` (Caché API pro vyhledávání tříd je ignoruje/ořízne)

### Parametry a makra
13. **`$$$FillParams` místo předávání IdFa/dataDB** - pokud třída Include GENERAL a metoda má `ByRef sys`, makro `$$$FillParams` zpřístupní `IdFa`, `dataDB`, `del`, `userID`, `GUID` atd. jako lokální proměnné. Nepředávat je jako parametry mezi metodami - každá metoda si zavolá `$$$FillParams` sama.

### Návratové hodnoty a error handling
14. **Nepoužívat `$$$Init`** - TD konvence. ⚠️ **Pozor na odchylku:** EDISON-wide skeleton (`docs/mlrest.md §2.2.4` + root `CLAUDE.md`) `$$$Init` **standardně má** (inicializuje `ret`+`err`, konec `ret ret`); TD ho vědomě NEpoužívá a končí `ret $$$OK`. Pokud kopíruješ skeleton z `docs/mlrest.md`, `$$$Init` vynech. **Důsledek:** protože `$$$Init` odpadá, `err` (které čte `$$$CatchErrRest`) si musí inicializovat metoda sama - viz rule 15.
15. **Návratový vzor pro API metody** (s `ByRef sys`): `s err = ""` na prvním řádku, pak `try { $$$FillParams ... } catch (ex) { $$$CatchErrRest } ret $$$OK`. **`s err = ""` je POVINNÉ** - `$$$FillParams` `err` nenastavuje, `$$$CatchErrRest` ho čte; bez initu spadne catch při reálné chybě na sekundární `<UNDEFINED>err` místo čisté hlášky. (Nahrazuje to, co dřív dělal `$$$Init`.)
16. **Helper metody bez `ByRef sys`** - nemusí mít try/catch ani vracet %Status, pokud jsou volány jen z API metod které catch mají.
