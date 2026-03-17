# Architektura specializovaných JŘ elementů

> **Stav:** Architektonický koncept (schváleno 2026-03-17). Implementace dosud nezačala.
> **Priorita:** Primární architektonická změna — nutná pro podporu tisku jízdních řádů.

---

## Problém

Starý systém (Caché) nejen vrací data, ale i řídí celý layout tisku:
- Sestavuje **sekce** (`SEKCE_START/END`) — co, kde a v jakém pořadí
- Definuje **sloupce** (`SeznamZastavekDataSloupecTrasaMin1..15`) — hardcoded struktura
- Formátuje **buňky** (`value^ForeColor^BackColor^SpecialniZnaky`) — vizuál přímo v datech
- Počítá **layout parametry** (`NejvetsiPocetMinutNaJedneHodine`) — metrika pro rendering

Toto míchání dat + layoutu + formátování do jednoho wire formátu znemožňuje:
- Měnit vizuální podobu bez zásahu do Caché
- Přenášet šablony mezi agendami/dopravci
- Používat nový TemplateDesigner pro JŘ sestavy

---

## Nový princip: Třívrstvé oddělení

```
┌─────────────────────────────────────────────────────────┐
│ CACHÉ — Vrstva 1: Čistá data + Business logika          │
│                                                         │
│ GetData() vrací:                                        │
│ - Skalární hodnoty (číslo linky, platnost, dopravce)    │
│ - Kolekce ZASTAVKY (s atributy, zónami, poznámkami)     │
│ - Kolekce SPOJE (s typy dnů, platností, NZ)             │
│ - Kolekce ODJEZDY (flat: zastávka × spoj × čas)         │
│ - Kolekce POZNAMKY (poznámky/legenda)                   │
│                                                         │
│ Business logika ZŮSTÁVÁ v Caché:                        │
│ - Slučování zastávek                                    │
│ - Výpočet trasových bitmap                              │
│ - Automatické poznámkování rozdílů v trasách            │
│ - Generování NZ značek                                  │
│ ALE výstupem je strukturovaná data, NE layout příkazy   │
└─────────────────────┬───────────────────────────────────┘
                      │ JSON (čistá data)
┌─────────────────────▼───────────────────────────────────┐
│ TEMPLATE — Vrstva 2: Layout + Vizuální nastavení        │
│                                                         │
│ Šablona obsahuje specializované elementy:               │
│ - StopScheduleElement (zastávkový JŘ)                   │
│ - DepartureGridElement (mřížka minut odjezdů)           │
│ - BookScheduleElement (knižní JŘ)                       │
│ - VehicleScheduleElement (vozový JŘ)                    │
│ Každý element má bohaté properties pro vizuální         │
│ nastavení (šířky sloupců, barvy, co zobrazit)           │
└─────────────────────┬───────────────────────────────────┘
                      │
┌─────────────────────▼───────────────────────────────────┐
│ ENGINE + ELEMENTY — Vrstva 3: Výpočet + Vykreslení      │
│                                                         │
│ PageLayoutEngine: generický (sekce → stránky) — BEZE    │
│ ZMĚN, funguje automaticky pro nové elementy              │
│                                                         │
│ Specializované elementy encapsulují:                    │
│ - Pivotování dat (flat → matice)                        │
│ - Formátování buněk podle dat (barvy, symboly)          │
│ - Overflow do bloků (když se nevejde na šířku)          │
│ - Grouping po typech dne                                │
└─────────────────────────────────────────────────────────┘
```

**Klíčový princip:** Business logika (co se děje s daty) zůstává v Caché. Layout logika (jak to vypadá) přechází do C# elementů a šablony. Engine zůstává generický.

---

## Datový kontrakt Caché (nový)

### Co Caché DĚLÁ (business logika)
- Sloučí totožné zastávky za sebou
- Vypočítá bitové mapy tras (`TRASA_BITMAP` per spoj)
- Vygeneruje automatické poznámky k rozdílům v trasách (`TRASA_ROZDILY`)
- Přiřadí negativní značky (`NZ` + `NZ_TEXT` per spoj)
- Vyhodnotí platnosti spojů

### Co Caché NEDĚLÁ (oproti starému systému)
- **Nesestavuje sloupce** pro rendering (žádné `SeznamZastavekDataSloupecTrasaMin1..15`)
- **Nedefinuje sekce** (žádné `SEKCE_START/END`)
- **Neformátuje buňky** pro wire (žádné `value^ForeColor^BackColor`)
- **Nepočítá layout metriky** (žádné `NejvetsiPocetMinutNaJedneHodine`)

### Wire formát — příklad MHD zastávkový JŘ

```json
{
  "values": {
    "CISLO_LINKY": "42",
    "NAZEV_LINKY": "Praha–Brno",
    "DOPRAVCE": "DPmB",
    "PLATNOST_OD": "2026-01-01",
    "PLATNOST_DO": "2026-12-31",
    "TYP_JR": "NÁVRH",
    "QR_KOD": "https://...",
    "POPIS_SMER": "Líšeň → Bystrc"
  },
  "rows": {
    "ZASTAVKY": [
      {
        "ID": "1",
        "POPIS": "Líšeň, Novolíšeňská",
        "TC": "101",
        "KM": "0",
        "ZONA": "A1",
        "PORADI": "1",
        "JE_AKTUALNI": true,
        "ODSAZENI": 0,
        "CAS_JIZDY": "0",
        "ATRIBUTY": [
          { "ZNAK": "♿", "TYP": "specialni", "POPIS": "Bezbariérový přístup" }
        ],
        "POZNAMKY_ZASTAVKY": [
          { "ZNAK": "A", "TYP": "text", "TEXT": "Končí v zast. Rudolfov" }
        ]
      }
    ],

    "SPOJE": [
      {
        "ID": "S101",
        "CISLO": "101",
        "TYP_DNE_SKUPINA": "PRACOVNI",
        "TYP_DNE_NAZEV": "Pracovní dny mimo prázdniny",
        "TYP_DNE_NAZEV2": "",
        "PORADI": "1",
        "NZ": "22",
        "NZ_TEXT": "Nejede 24.12.",
        "OMEZENI_TYDNE": ["1","2","3","4","5"],
        "TRASA_BITMAP": "1110111",
        "TRASA_ROZDILY": [
          { "TYP": "nezajizdi", "ZASTAVKA_OD": "Pardubice", "ZASTAVKA_DO": "Brno" }
        ]
      }
    ],

    "ODJEZDY": [
      {
        "ZASTAVKA_ID": "1",
        "SPOJ_ID": "S101",
        "HODINA": 6,
        "MINUTA": 0,
        "TYP": "odjezd",
        "POZNAMKY": [
          { "TYP": "nz", "ZNAK": "22" },
          { "TYP": "specialni", "ZNAK": "♿" },
          { "TYP": "text", "ZNAK": "A" }
        ]
      }
    ],

    "POZNAMKY": [
      { "TYP": "specialni", "ZNAK": "♿", "TEXT": "Spoj s bezbariérovým vozidlem" },
      { "TYP": "nz", "ZNAK": "22", "TEXT": "Nejede 24.12." },
      { "TYP": "text", "ZNAK": "A", "TEXT": "Končí v zast. Rudolfov" }
    ]
  }
}
```

**Klíčové rozdíly oproti starému systému:**
- Žádné `value^ForeColor^BackColor` — barvy řeší element podle pravidel
- Žádné hardcoded sloupce (TrasaMin1..15) — element si data seskupí sám
- Business logika (`TRASA_BITMAP`, `TRASA_ROZDILY`) je výstup Caché ve strukturované formě
- Poznámky per odjezd jsou vnořené, ne zakódované v delimited stringu
- ODJEZDY je **flat kolekce** (zastávka × spoj × čas) — element pivotuje do matice při renderování

---

## Hierarchie specializovaných elementů

```
TemplateElement (abstract)
├── TextElement, TableElement, ImageElement, ... (existující generické)
└── ScheduleElementBase (abstract, nová base class pro JŘ)
    ├── StopScheduleElement      — zastávkový JŘ (MHD i meziměsto)
    ├── DepartureGridElement     — mřížka minut odjezdů (MHD specifické)
    ├── BookScheduleElement      — knižní JŘ
    └── VehicleScheduleElement   — vozový JŘ
```

Každý typ JŘ = vlastní element s vlastním `Render()`. Sdílí base class pro společnou logiku (indexování dat, grouping, přístup ke kolekcím).

### ScheduleElementBase — společný základ

```
Společné properties:
  StopsDataSource           = "ZASTAVKY"      // klíč kolekce zastávek
  ConnectionsDataSource     = "SPOJE"          // klíč kolekce spojů
  DeparturesDataSource      = "ODJEZDY"        // klíč kolekce odjezdů
  NotesDataSource           = "POZNAMKY"        // klíč kolekce poznámek

  FontSize, FontFamily, GridColor, HeaderBackgroundColor, ...

Společné metody:
  BuildStopIndex(data)      → Dict<id, stop>
  BuildConnectionIndex(data)→ Dict<id, connection>
  BuildDepartureMatrix(data)→ Dict<(stopId, connId), departure>
  GroupConnectionsByDayType(connections) → Dict<skupina, List<conn>>
```

---

## StopScheduleElement — zastávkový JŘ (nejkomplexnější)

Odpovídá starému `stSekceSeznamZastavek` + `stMinutyOdjezdu`. Pokrývá **MHD i meziměsto** jedním elementem se dvěma display módy.

### Properties (vizuální nastavení v šabloně)

```
// Levé sloupce
ShowZoneColumn              — zóna u zastávky
ShowKmColumn                — kilometry
ShowStopNameColumn          — název zastávky
ShowRouteColumn             — základní trasa (0/1 u každé zastávky)
ShowJourneyTimeColumn       — čas jízdy

// Sloupce spojů
ConnectionColumnWidth       — šířka jednoho spoje (mm)
GroupByDayType              — seskupit po typech dne
GroupHeaderHeight           — výška hlavičky skupiny

// Výpis minut vs. plný čas
DisplayMode = "FullTime" | "MinutesOnly"

// Poznámky
ShowAnnotationSymbols       — symboly u minut (♿, NZ, text)
ShowNotesLegend             — automatická legenda pod maticí

// Směr/šipky
ShowDirectionArrows
DirectionArrowField = "SIPKA"

// Barevné odlišení
ColorRules: [               — conditional formatting pravidla
  { Condition: "{CENA} > 30", ForeColor: "#FF0000", BackColor: "#FFFF00" }
]

// Overflow
MaxConnectionsPerBlock      — null = auto-fit na šířku
```

### Display mód "FullTime" (meziměstský styl)

Matice: zastávky (řádky) × spoje (sloupce), seskupené po typech dne.

```
┌─────────────────────────────────────────────────────────┐
│           │ PRACOVNÍ DNY        │ SOBOTY    │ NEDĚLE    │
│ Zastávka  │ 101  103  105  107  │ 201  203  │ 301  303  │
├───────────┼─────────────────────┼───────────┼───────────┤
│ Praha     │ 6:00 8:00 10:0 12:0│ 7:00 11:0 │ 8:00 14:0 │
│ Pardubice │ 7:44 9:44 11:4 13:4│ 8:44 12:4 │ 9:44 15:4 │
│ Brno      │ 9:30 11:3 13:3 15:3│ 10:3 14:3 │ 11:3 17:3 │
└───────────┴─────────────────────┴───────────┴───────────┘
```

### Display mód "MinutesOnly" (MHD styl)

Mřížka: hodiny (řádky) × minuty (buňky), seskupené po typech dne.

```
┌──────────────────────────────────────────────────────────┐
│  PRACOVNÍ DNY mimo prázdniny                             │
├──────┬────────────────────────────────────────────────────┤
│  Hod │ Minuty                                            │
├──────┼────────────────────────────────────────────────────┤
│   5  │ 15  30  45                                        │
│   6  │ 00  12  24  36  48                                │
│   7  │ 03  15  27♿ 39  51                                │
│  ... │ ...                                               │
└──────┴────────────────────────────────────────────────────┘
```

### DepartureGridElement — volitelně samostatný

Pokud se ukáže, že MHD styl minut je příliš odlišný od maticového stylu, může to být samostatný element. Pravděpodobně ale stačí `StopScheduleElement` s `DisplayMode = "MinutesOnly"`.

### BookScheduleElement a VehicleScheduleElement

Otevřená otázka — viz sekce "Otevřené otázky" níže. Budou řešeny až bude jasná vizuální podoba.

---

## Barvy a formátování buněk

### Starý systém
Caché přímo pošle `ForeColor`/`BackColor` per buňka v datech → žádná kontrola v designeru.

### Nový systém: Dvě cesty

**1. Conditional formatting rules (výchozí):**
Element má property `ColorRules` — seznam pravidel vyhodnocovaných `ExpressionEvaluator`em:

```json
"colorRules": [
  { "condition": "{TYP} == 'prujezd'", "foreColor": "#999999" },
  { "condition": "{CENA} > 50", "foreColor": "#FF0000", "backColor": "#FFFF00" }
]
```

**2. Data-driven barvy (fallback pro složitou logiku):**
Pokud Caché potřebuje říct "tato buňka je červená" (logika příliš složitá na expression), pošle barvu přímo v datech:

```json
{ "ZASTAVKA_ID": "1", "SPOJ_ID": "S101", "MINUTA": 15,
  "FORE_COLOR": "#FF0000", "BACK_COLOR": "#FFFF00" }
```

**Priorita:** Data-driven barvy (z Caché) mají přednost před ColorRules (ze šablony).

---

## Matice zodpovědností

| Logika | Starý systém | Nový systém |
|---|---|---|
| Slučování zastávek | Caché (bitmap) | **Caché** — vrací už sloučená data |
| Bitové mapy tras | Caché | **Caché** — vrací `TRASA_BITMAP` per spoj |
| Auto-poznámky k trasám | Caché (MakeOmezTrasaTx) | **Caché** — vrací `TRASA_ROZDILY` per spoj |
| NZ značky platností | Caché | **Caché** — vrací `NZ` + `NZ_TEXT` per spoj |
| Grouping po typech dne | Caché (SMO1-4) | **Element** — seskupí podle `TYP_DNE_SKUPINA` |
| Řazení spojů | Caché | **Element** — řadí podle `PORADI` |
| Formátování buněk (barvy) | Caché (`^ForeColor^BackColor`) | **Element** — podle `ColorRules` nebo data-driven |
| Výběr zobrazených sloupců | Starý designer (omezené) | **Template** — element properties |
| Počet minut na hodinu | Caché (`NejvetsiPocetMinut`) | **Element** — spočítá z dat |
| Pozicování elementů | Caché (SEKCE_START/END) | **Designer** — uživatel umístí element |
| Pivotování flat dat → matice | N/A (Caché posílá sloupce) | **Element** — z flat ODJEZDY |

---

## PageLayoutEngine — beze změn

Engine nepotřebuje žádné úpravy. Každý JŘ element:
- Implementuje `CalculateHeight()` → engine ho umístí na stránku
- Implementuje `Render(RenderContext)` → engine ho vykreslí
- Pokud se nevejde → `SplitAcrossPages` funguje automaticky
- Všechny stávající mechanismy (flow layout, hlavičky/patičky, PAGE/TOTAL_PAGES) fungují

---

## Postup implementace

| Krok | Co | Závislosti |
|---|---|---|
| 1 | `ScheduleElementBase` — abstraktní základ s indexy a groupingem | žádné |
| 2 | `StopScheduleElement` s `DisplayMode = "FullTime"` (meziměsto) | krok 1 |
| 3 | Úprava Caché `GetData`/`GetMockData` — vrací ZASTAVKY, SPOJE, ODJEZDY, POZNAMKY | krok 2 |
| 4 | `SkiaHeightCalculator` — měření výšky JŘ elementu | krok 2 |
| 5 | `DisplayMode = "MinutesOnly"` (MHD) | krok 2 |
| 6 | `DepartureGridElement` (pokud MHD potřebuje samostatný element) | krok 1 |
| 7 | `BookScheduleElement` | krok 1 + upřesnění layoutu |
| 8 | `VehicleScheduleElement` | krok 1 + upřesnění layoutu |

---

## Otevřené otázky (pro budoucí diskuzi)

1. **Knižní JŘ** — je to:
   a) StopScheduleElement s menšími bloky a automatickým stránkováním?
   b) Speciální layout s více matic vedle sebe na jedné stránce?
   c) Úplně jiná struktura?

2. **Vozový JŘ** — je to:
   a) Stejná matice jako zastávkový, jen osa X = oběhy místo spojů?
   b) Diagram oběhu vozidla (lineární timeline)?
   c) Něco jiného?

3. **Speciální znaky (font symboly)** — starý systém konvertuje čísla na Unicode znaky (`ConvertSpecialniZnak`). Nový systém:
   a) Caché pošle už konvertované Unicode znaky?
   b) Element má mapovací tabulku?

4. **"TrasaMin" sloupce** — starý systém má až 15 sloupců s minutovými trasami. Co přesně zobrazují? Jsou to varianty trasy nebo minutové odjezdy per zastávka?

---

## Shrnutí

| Aspekt | Odpověď |
|---|---|
| **Je to možné?** | Ano — architektura je navržena |
| **Kde je složitost?** | V specializovaných elementech (ne v engine, ne v Caché layoutu) |
| **Co dělá Caché?** | Business logika + čistý JSON výstup (žádné SEKCE_START, žádné ForeColor v datech) |
| **Co dělá template?** | Definuje vizuální nastavení přes element properties |
| **Co dělá engine?** | Generické stránkování (beze změn) |
| **Kolik elementů pro JŘ?** | Min. 1 (StopScheduleElement s módy), max. 4 (per typ JŘ) |
| **Postupný vývoj?** | Začít StopScheduleElement s FullTime → přidat MinutesOnly → další typy |
