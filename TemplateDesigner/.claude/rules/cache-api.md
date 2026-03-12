# Glob: CACHE/**

## Caché AgendaAPI architektura (implementováno)

Každá agenda (JR, FAKTURY, ...) má jednu Caché třídu dědící z abstraktního základu.
Třída slouží jako **kompletní API rozhraní** pro TemplateDesigner — design-time i print-time.

### Abstraktní základ — vynucuje kontrakt

```objectscript
Class ALVA.TemplateDesigner.AgendaAPI Extends %RegisteredObject [ Abstract ]
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

Každá nová agenda **musí** implementovat všech pět metod — Caché kompilátor to ohlídá.

---

### Implementace per agenda

```objectscript
Class ALVA.JR.TiskAPI Extends ALVA.TemplateDesigner.AgendaAPI
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

#### GetSchema — OUT[1]
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

#### GetTableTemplates — OUT[1]
```json
[
  { "key": "ZAHLAVI_LINKY",    "label": "Záhlaví linky" },
  { "key": "PLATNOST_DOPRAVCE","label": "Platnost a dopravce" }
]
```

#### GetTableTemplate — IN + OUT[1]
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

#### GetMockData — OUT[1] (design-time preview)
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

#### GetData — IN + OUT (print-time)
```
IN: parametry závislé na agendě (číslo linky, datum, ...)
OUT[1] = {"CISLO_LINKY":"42","NAZEV":"Linka 42"}          ← skalární hodnoty (JSON objekt)
OUT[2] = {"collection":"ZASTAVKY","POPIS":"Praha hl.n.","CAS_ODJ":"800"}
OUT[3] = {"collection":"ZASTAVKY","POPIS":"Smíchovské n.","CAS_ODJ":"812"}
```

Řádky kolekce jsou identifikovatelné přítomností klíče `"collection"`. Skalární objekt je vždy OUT[1].

---

### Názvosloví — přehled pojmů API agendy

| Anglický název v kódu | Český ekvivalent | Business význam | Příklad |
|---|---|---|---|
| `GetSchema` | GetSchema | Vrátí **definici dostupných dat** agendy — co existuje jako pole, ne konkrétní hodnoty | `{ fields: [{name:"CISLO_LINKY",...}], groups: [...] }` |
| `GetTableTemplates` | GetTableTemplates | Vrátí **seznam předdefinovaných layoutů** Fixed tabulek uložených v Caché | `[{key:"ZAHLAVI_LINKY", label:"Záhlaví linky"}]` |
| `GetTableTemplate` | GetTableTemplate | Vrátí **konkrétní předdefinovaný layout** — sloupce + předvyplněné řádky | `{ columns:[...], rows:[{cells:[{content:"{CISLO_LINKY}"}]}] }` |
| `GetData` | GetData | Vrátí **reálná data z DB** pro tisk; volaná print enginem za běhu | `{ CISLO_LINKY:"42", NAZEV:"Praha–Brno" }` + kolekce řádků |
| `GetMockData` | GetMockData | Vrátí **vzorová data** pro live preview v designeru; volaná design-time | stejná struktura jako GetData, ale s pevnými testovacími hodnotami |
| `fields` | skalární pole | Pole agendy kde každá hodnota existuje **právě jednou** na celou sestavu | `CISLO_LINKY`, `POPIS_DOPRAVCE`, `DATUM_PLATNOSTI_OD` |
| `fieldCollections` | tabulkové kolekce | Kolekce sad polí kde každá kolekce tvoří **seznam řádků** — zdroj dat pro Dynamic tabulku | kolekce `Zastávky` obsahuje pole `POPIS_ZASTAVKA`, `CAS_ODJEZDU`… |
| `collectionKey` | klíč kolekce | Identifikátor kolekce řádků v `fieldCollections`; designer ho zobrazuje jako volbu DataSource Dynamic tabulky | `"ZASTAVKY"`, `"SPOJE"` |
| `key` (v GetTableTemplates) | klíč šablony | Interní identifikátor Fixed šablony; předává se jako parametr do `GetTableTemplate` | `"ZAHLAVI_LINKY"`, `"PLATNOST_DOPRAVCE"` |

**Konvence klíčů šablon tabulek:** česky, velkými písmeny, podtržítko jako oddělovač, bez prefixu třídy/agendy. Příklady: `ZAHLAVI_LINKY`, `PLATNOST_DOPRAVCE`.

**Konvence collectionKey:** česky, velkými písmeny, množné číslo, podtržítko jako oddělovač. Příklady: `ZASTAVKY`, `SPOJE`, `POLOZKY_FAKTURY`.

---

### C# strana — konfigurace a provider

**appsettings.json:**
```json
{
  "agendas": [
    { "code": "JR",      "label": "Jízdní řád", "cacheClass": "ALVA.JR.TiskAPI" },
    { "code": "FAKTURY", "label": "Faktury",     "cacheClass": "ALVA.Faktury.TiskAPI" }
  ]
}
```

**CacheAgendaProvider** — jeden provider pokrývá obě rozhraní:
```csharp
// Implementuje IDataSchemaProvider + ITableTemplateProvider
class CacheAgendaProvider : IDataSchemaProvider, ITableTemplateProvider
{
    string _cacheClass;   // "ALVA.JR.TiskAPI"
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
- Přečte `appsettings.json` → pro každou agendu vytvoří `CacheAgendaProvider(cacheClass, mlRest)`
- Fallback: `CompositeDataSchemaProvider` (Caché primary + JSON fallback ze `schemas/*.schema.json`)

---

### Přidání nové agendy — checklist

1. Vytvořit Caché třídu `ALVA.<Agenda>.TiskAPI Extends ALVA.TemplateDesigner.AgendaAPI`
2. Implementovat `GetSchema`, `GetTableTemplates`, `GetTableTemplate`, `GetData`, `GetMockData`
3. Přidat záznam do `appsettings.json` → `agendas[]`
4. Volitelně: přidat `schemas/<Agenda>.schema.json` jako offline fallback
   - `GetMockData` vrací JSON: `{ "values": { "POLE": "hodnota", ... }, "rows": { "KLIC": [ {...}, ... ] } }`

---

## Vzdálená vize (mimo aktuální scope)

**JR specifika:**
- `JRTableElement` — specializovaný element pro jízdní řády (matice zastávky × spoje)
  - Aktuální `TableElement` Fixed/Dynamic nestačí — sloupce jsou dynamické i v ose X
  - Properties: levé sloupce (Zóna/Km/Zastávka/MHD), šířka sloupce spoje, záhlaví skupin dní, šipky
  - Data z Caché GetData; šablona přenositelná přes linky

**Komponenty:**
- **Fragmenty** — uložené skupiny elementů pro opakované použití (jako custom section templates, ale na úrovni elementů)
- **ChartElement** — grafy

**Pokročilé features:**
- **Conditional formatting** — barva/font buňky nebo elementu podle hodnoty dat
- **DB backend pro custom šablony** — `CacheSectionTemplateProvider` místo file-based V1
- **AI asistent** — generování šablon z přirozeného jazyka
