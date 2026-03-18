# Architektura specializovaných JŘ elementů

> **Stav:** Architektonický koncept (schváleno 2026-03-17). Doplněno o analýzu legacy kódu (2026-03-18). Implementace dosud nezačala.
> **Priorita:** Primární architektonická změna — nutná pro podporu tisku jízdních řádů.
> **Legacy zdroje:** `ALVA.TISKMHD.Data` (wire formát), `ALVA.MHD.LinkyPrintFormatMHD` (business logika + data assembly)

---

## Problém

Starý systém (Caché) nejen vrací data, ale i řídí celý layout tisku:
- Sestavuje **sekce** (`SEKCE_START/END`) — co, kde a v jakém pořadí
- Definuje **sloupce** (`SeznamZastavekDataSloupecTrasaMin1..15`) — hardcoded struktura
- Formátuje **buňky** (`value^ForeColor^BackColor^SpecialniZnaky`) — vizuál přímo v datech
- Počítá **layout metriky** (`NejvetsiPocetMinutNaJedneHodine`) — metrika pro rendering
- Concatenuje data do delimited stringů (`#` oddělovač řádků, `^` oddělovač polí, `&` oddělovač barev)

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
│ - Kolekce SKUPINY_DNU (SMO metadata + nadpisy)          │
│ - Kolekce ODJEZDY (flat: zastávka × skupina × čas)      │
│ - Kolekce POZNAMKY (legenda — seřazená)                 │
│                                                         │
│ Business logika ZŮSTÁVÁ v Caché:                        │
│ - Slučování zastávek za sebou                           │
│ - Výpočet trasových bitmap                              │
│ - Auto-poznámkování rozdílů v trasách (2 algoritmy)     │
│ - Generování NZ značek z platností                      │
│ - Týdenní platnost → speciální znaky                    │
│ - Řazení poznámek (spec.znaky → NZ → text)             │
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
│ - Výpočet layout metrik (max minut na hodinu)           │
└─────────────────────────────────────────────────────────┘
```

**Klíčový princip:** Business logika (co se děje s daty) zůstává v Caché. Layout logika (jak to vypadá) přechází do C# elementů a šablony. Engine zůstává generický.

---

## Legacy analýza — co starý systém dělá

> Zdrojové třídy: `ALVA.TISKMHD.Data.LoadData()`, `ALVA.MHD.LinkyPrintFormatMHD.MakeData()`

### Sekce starého systému (SEKCE_START/END)

| Sekce | Typ | Co obsahuje |
|-------|-----|-------------|
| `stDBField~CISLO_LINKY` | Skalár | Jedno pole = jedna sekce |
| `stDBField~POPIS_LINKY` | Skalár | ... opakuje se pro každý skalár |
| `stDBField~POPIS_DOPRAVCE` | Skalár | |
| `stDBField~POPIS_PLATNOSTI` | Skalár | |
| `stDBField~DATUM_PLATNOSTI_OD` | Skalár | |
| `stDBField~DATUM_PLATNOSTI_DO` | Skalár | |
| `stDBField~POPIS_SMER` | Skalár | |
| `stDBField~TYP_JR` | Skalár | |
| `stDBField~POZNAMKA_POD_DOPRAVCE` | Skalár | |
| `stDBField~INFORMACE` | Skalár | |
| `stDBField~NEJEDOU_SPOJE` | Skalár | Auto-generovaný text |
| `stQRKod` | QR kód | URL |
| **`stSekceSeznamZastavek`** | **Tabulka zastávek** | 25+ sloupců serializovaných do `#`-delimited stringů |
| `stHodinaOdjezdu` | Časová osa | `#`-delimited hodiny |
| **`stMinutyOdjezdu~SMO1`** | **Mřížka minut** | Minuty per hodina, SMO = typ dne |
| **`stMinutyOdjezdu~SMO2`** | **Mřížka minut** | |
| **`stMinutyOdjezdu~SMO3`** | **Mřížka minut** | |
| **`stMinutyOdjezdu~SMO4`** | **Mřížka minut** | |
| `stPoznamky` | Legenda | Poznámky + symboly |

**V novém systému:** Žádné sekce z Caché. Skaláry = `{POLE}` v TextElementech. Tabulka zastávek + mřížka minut + poznámky = `StopScheduleElement`. QR kód = `BarcodeElement`.

### Wire formát starého systému — buňky

Každá buňka v tabulce zastávek je zakódovaná:
```
SpecZnakyPred^hodnota^SpecZnakyZa^NegativniZnacky&ForeColor(r;g;b)&BackColor(r;g;b)
```
Příklad: `♿/🚌^Praha hl.n.^🚲/🛏️^22/33&255;255;255&49;140;231`

Minuty odjezdů:
```
minuta^ForeColor^BackColor^anotace^zkraceneOznaceniLinky
```
Kde anotace = `typ$znak&typ$znak&...` (typ: 1=text, 2=NZ, 3=spec.znak)

**V novém systému:** Strukturovaný JSON. Žádné delimited stringy.

### Vstupní parametry starého systému

| Parametr | Hodnota | Popis |
|----------|---------|-------|
| `VOLANO_Z` | `"JRMAIN"` / `"MHD"` | Zdroj dat (meziměsto vs MHD) |
| `VALUE` | `SkupinaId#LinkaId#PageNumber#...` | Compound klíč, `#`-delimited |
| `TiskProjetychZast` | `0/1` | Zobrazit zastávky před aktuální |
| `CasovaOsaOdjezduBezMezer` | `1/2/3` | Režim časové osy |
| `CasovaOsaOdjezduBezMezerOd/Do` | `5-22` | Rozsah hodin (režim 3) |
| `BarevneOdlisitCeny1–6` | threshold → colors | Cenové barevné prahy (6 úrovní) |
| `NeAtributID` | `ID\|ID\|...` | Atributy k vyloučení z tisku |

---

## Datový kontrakt Caché (nový) — kompletní z legacy analýzy

### Co Caché DĚLÁ (business logika — zachovat)
- Sloučí totožné zastávky za sebou (`@mywrkglb@(1,SmerID,-100,...)`)
- Vypočítá bitové mapy tras per spoj (`TrasaBitmap`, `AllStopTrasaBitmap`)
- Auto-poznámkování rozdílů v trasách — **2 algoritmy:**
  - **Alg. 1001** (textový): `MakeOmezTrasaTx` → "nezajíždí do X", "končí v zast. Y", "jede přes Z"
  - **Alg. 1002** (písmenka): přiřadí písmena (a,b,c) trasovým variantám → u zastávky i u minuty
- Generování NZ z bitmap platností (`ProvozBitMapa` vs `TypProvozuID`)
- Týdenní platnost → speciální znaky dnů (1=Po, 2=Út, ..., 7=Ne)
- Atributy zastávek (z číselníku `ALVA.MHD.ENUMS("Atribut",...)` + z trasy linky)
- Poznámky zastávek (z trasy linky per bod per směr)
- Poznámky a atributy spojů (ruční + automatické)
- Řazení poznámek: speciální znaky → NZ → text se znakem → text bez znaku
- Konverze speciálních znaků (`ConvertSpecialniZnak(číslo)` → Unicode)

### Co Caché NEDĚLÁ (oproti starému systému — odstranit)
- **Nesestavuje sekce** (žádné `SEKCE_START/END`)
- **Neserializuje sloupce** do `#`-delimited stringů (žádné `SeznamZastavekDataSloupec...`)
- **Neformátuje buňky** inline (žádné `value^ForeColor^BackColor`)
- **Nepočítá layout metriky** (žádné `NejvetsiPocetMinutNaJedneHodine`)
- **Nevkládá skaláry do sekcí** (žádné `DBFieldText~...` + `SEKCE_END~stDBField~...`)
- **Neopakuje kód per SMO** (žádný 4× copy-paste blok pro SMO1–4)

### Wire formát — kompletní příklad

```json
{
  "values": {
    "CISLO_LINKY": "42",
    "LOKALNI_OZNACENI_LINKY": "A42",
    "TITULEK": "MHD v Brně",
    "POPIS_LINKY": "Líšeň–Bystrc",
    "POPIS_DOPRAVCE": "DPmB",
    "POPIS_PLATNOSTI": "01.01.2026 - 31.12.2026",
    "DATUM_PLATNOSTI_OD": "01.01.2026",
    "DATUM_PLATNOSTI_DO": "31.12.2026",
    "TYP_JR": "NÁVRH",
    "QR_KOD": "https://jr.example.com/42",
    "POPIS_ZASTAVKA": "Líšeň, Novolíšeňská",
    "CISLO_ZASTAVKA": "101-A",
    "POPIS_SMER": "Bystrc, zoo",
    "POZNAMKA_POD_DOPRAVCE": "",
    "INFORMACE": "",
    "NEJEDOU_SPOJE": "První spoj v 5:15, poslední v 23:30"
  },
  "rows": {
    "ZASTAVKY": [
      {
        "ID": "1",
        "POPIS": "Líšeň, Novolíšeňská",
        "TC": "101",
        "CAS_JIZDY": 0,
        "PORADI": 1,
        "JE_AKTUALNI": true,
        "ODSAZENI": 0,
        "ZONY": ["A1"],
        "PRUBEHY": ["a", "b"],
        "ZAKLADNI_TRASA": true,
        "NA_ZNAMENI": false,
        "ATRIBUTY": [
          { "ZNAK": "♿", "POPIS": "Bezbariérový přístup" }
        ],
        "POZNAMKY_ZASTAVKY": [
          { "ZNAK": "A", "TYP": "text", "TEXT": "Končí v zast. Rudolfov" }
        ]
      },
      {
        "ID": "2",
        "POPIS": "Líšeň, Mifkova",
        "TC": "102",
        "CAS_JIZDY": 2,
        "PORADI": 2,
        "JE_AKTUALNI": false,
        "ODSAZENI": 0,
        "ZONY": ["A1"],
        "PRUBEHY": [],
        "ZAKLADNI_TRASA": true,
        "NA_ZNAMENI": true,
        "ATRIBUTY": [],
        "POZNAMKY_ZASTAVKY": []
      }
    ],

    "SKUPINY_DNU": [
      {
        "ID": "SMO1",
        "NADPIS": "PRACOVNÍ DNY",
        "NADPIS2": "mimo prázdniny",
        "PORADI": 1
      },
      {
        "ID": "SMO2",
        "NADPIS": "SOBOTY",
        "NADPIS2": "",
        "PORADI": 2
      },
      {
        "ID": "SMO3",
        "NADPIS": "NEDĚLE A SVÁTKY",
        "NADPIS2": "",
        "PORADI": 3
      },
      {
        "ID": "SMO4",
        "NADPIS": "Pracovní dny o prázdninách",
        "NADPIS2": "Soboty - Neděle, svátky",
        "PORADI": 4
      }
    ],

    "ODJEZDY": [
      {
        "ZASTAVKA_ID": "1",
        "SKUPINA_DNU_ID": "SMO1",
        "HODINA": 5,
        "MINUTA": 15,
        "ZKRACENE_OZNACENI_LINKY": "",
        "FORE_COLOR": "",
        "BACK_COLOR": "",
        "ANOTACE": [
          { "TYP": "specialni", "ZNAK": "♿" }
        ]
      },
      {
        "ZASTAVKA_ID": "1",
        "SKUPINA_DNU_ID": "SMO1",
        "HODINA": 5,
        "MINUTA": 30,
        "ZKRACENE_OZNACENI_LINKY": "",
        "FORE_COLOR": "",
        "BACK_COLOR": "",
        "ANOTACE": [
          { "TYP": "nz", "ZNAK": "22" },
          { "TYP": "text", "ZNAK": "a" }
        ]
      },
      {
        "ZASTAVKA_ID": "1",
        "SKUPINA_DNU_ID": "SMO2",
        "HODINA": 7,
        "MINUTA": 0,
        "ZKRACENE_OZNACENI_LINKY": "ZOL1",
        "FORE_COLOR": "",
        "BACK_COLOR": "",
        "ANOTACE": []
      }
    ],

    "POZNAMKY": [
      { "TYP": "specialni", "ZNAK": "♿", "TEXT": "Spoj s bezbariérovým vozidlem" },
      { "TYP": "specialni", "ZNAK": "🚌", "TEXT": "Přestup na autobus" },
      { "TYP": "nz", "ZNAK": "22", "TEXT": "Nejede 24.12." },
      { "TYP": "nz", "ZNAK": "33", "TEXT": "Nejede 25.12." },
      { "TYP": "text", "ZNAK": "a", "TEXT": "nezajíždí do zast. Pardubice" },
      { "TYP": "text", "ZNAK": "A", "TEXT": "Končí v zast. Rudolfov" }
    ]
  }
}
```

### Změny oproti původnímu konceptu

| Původní koncept | Po analýze legacy kódu | Důvod |
|-----------------|------------------------|-------|
| Kolekce `SPOJE` (per spoj) | **Zrušeno** — nahrazeno `SKUPINY_DNU` + flat `ODJEZDY` | MHD nemá koncept "spoje jako sloupce". Meziměsto ano, ale data jdou vyjádřit přes ODJEZDY se SKUPINA_DNU_ID |
| `SPOJ_ID` v ODJEZDY | **`SKUPINA_DNU_ID`** | Element groupuje po skupinách dnů, ne po spojích |
| Barvy v šabloně (ColorRules) | **Zachováno + data-driven barvy** | Legacy používá `ForeColor/BackColor` z Caché; nový systém: Caché může poslat barvu v `FORE_COLOR`/`BACK_COLOR` na odjezdu, ale element má i vlastní pravidla |
| Anotace jako `{ TYP, ZNAK }` | **Zachováno** — 3 typy | Přesně odpovídá legacy: typ 0=text, 1=NZ, 2=speciální znak |

### Mapování legacy dat → nový formát

#### Skaláry (key 1)

| Legacy (dta #) | Nový klíč | Zdroj v MakeData |
|----------------|-----------|------------------|
| dta 1 | `CISLO_LINKY` | `CisloLinkyDleLicence` |
| dta 2 | `LOKALNI_OZNACENI_LINKY` | `CisloVerejne` |
| dta 3 | `TITULEK` | fallback "Městská hromadná doprava..." |
| dta 4 | `POPIS_LINKY` | `NazevLinky` |
| dta 5 | `POPIS_DOPRAVCE` | (zatím prázdné v MHD) |
| dta 6 | `POPIS_PLATNOSTI` | `platiOd - platiDo` |
| dta 8 | `POPIS_ZASTAVKA` | název aktuální zastávky |
| dta 9 | `POPIS_SMER` | název poslední zastávky = směr |
| dta 13 | `QR_KOD` | `QRCode` z linky |
| dta 15 | `DATUM_PLATNOSTI_OD` | dd.mm.rrrr |
| dta 16 | `DATUM_PLATNOSTI_DO` | dd.mm.rrrr |
| dta 17 | `CISLO_ZASTAVKA` | číslo-označník |
| dta 18 | `TYP_JR` | NÁVRH / VÝLUKOVÝ / "" |
| dta 19 | `POZNAMKA_POD_DOPRAVCE` | volný text |
| dta 22 | `INFORMACE` | (zatím prázdné) |
| dta 23 | `NEJEDOU_SPOJE` | auto-generovaný text |

#### Zastávky (key 2)

| Legacy (dta #) | Nový klíč | Poznámka |
|----------------|-----------|----------|
| dta 1 | `JE_AKTUALNI` | 1/0 → bool |
| dta 2 | `TC` | tarifní číslo |
| dta 3 | `CAS_JIZDY` | minuty od aktuální; legacy má i ForeColor/BackColor |
| dta 4 | `POPIS` | název zastávky (bez formátování) |
| dta 4, SpecialniZnaky | `ATRIBUTY[]` | legacy: slash-delimited za názvem → nový: pole objektů |
| dta 5 | `ZONY[]` | legacy: subscriptované klíče → nový: pole stringů |
| dta 6 | `PRUBEHY[]` | legacy: subscriptované klíče → nový: pole stringů |
| dta 7 | `ZAKLADNI_TRASA` | 1/"" → bool |
| dta 8 | `NA_ZNAMENI` | z "pracovní sloupec 2" |
| dta 10 | `ODSAZENI` | počet znaků odsazení |
| — | `POZNAMKY_ZASTAVKY[]` | legacy: přilepené k názvu → nový: separátní pole |

**Pracovní sloupce 3–5 (dta 9, 11, 12):** V legacy kódu se plní pro meziměstský JŘ (přes `$WinTiskMhd^JRapi`). Pro MHD se plní jen sloupec 2 (na znamení). Ostatní sloupce jsou rozšiřitelné — v novém systému je pokrývají atributy a poznámky zastávky.

**TrasaMin1–15 (dta 13–27):** Sloupce spojů v meziměstském JŘ — matice zastávky × spoje. V MHD se nepoužívají. V novém systému je nahrazuje `DisplayMode = "FullTime"` s daty z ODJEZDY.

#### Odjezdy (key 3)

| Legacy | Nový klíč | Poznámka |
|--------|-----------|----------|
| `arr, sl (1-4)` | `SKUPINA_DNU_ID` | SMO1–4 → string klíč |
| `dta 1` (nadpis) | v `SKUPINY_DNU[].NADPIS` | přesunuto do metadata kolekce |
| `dta 2` (nadpis 2. řádek) | v `SKUPINY_DNU[].NADPIS2` | `<br>` oddělovač → separátní pole |
| `arr, hodin, cnt, dta 1` | `MINUTA` | 2-ciferná minuta |
| `arr, hodin, cnt, dta 2` | `ZKRACENE_OZNACENI_LINKY` | pro sdílené zastávky |
| `arr, hodin, cnt, dta 3` | `FORE_COLOR` | `r;g;b` → `#AARRGGBB` |
| `arr, cnt2, typ 1` | `ANOTACE[].TYP` | 0→"text", 1→"nz", 2→"specialni" |
| `arr, cnt2, dta 1` | `ANOTACE[].ZNAK` | Caché posílá už konvertovaný Unicode |

#### Poznámky (key 4)

| Legacy | Nový | Poznámka |
|--------|------|----------|
| `typ 1 = 2` | `TYP = "specialni"` | speciální znak (ikona) |
| `typ 1 = 1` | `TYP = "nz"` | negativní značka (bílé na černém) |
| `typ 1 = 0` | `TYP = "text"` | textová poznámka |
| `dta 1` | `ZNAK` | identifikátor (Unicode / písmeno / číslo NZ) |
| `dta 2` | `TEXT` | popis poznámky |

**Řazení:** Caché pošle seřazené (speciální → NZ → text se znakem → text bez znaku).

---

## Hierarchie specializovaných elementů

```
TemplateElement (abstract)
├── TextElement, TableElement, ImageElement, ... (existující generické)
└── ScheduleElementBase (abstract, nová base class pro JŘ)
    ├── StopScheduleElement      — zastávkový JŘ (MHD i meziměsto)
    ├── DepartureGridElement     — mřížka minut odjezdů (MHD specifické, pokud bude potřeba)
    ├── BookScheduleElement      — knižní JŘ (TBD)
    └── VehicleScheduleElement   — vozový JŘ (TBD)
```

Každý typ JŘ = vlastní element s vlastním `Render()`. Sdílí base class pro společnou logiku (indexování dat, grouping, přístup ke kolekcím).

### ScheduleElementBase — společný základ

```
Společné properties:
  // DataSource klíče (výchozí hodnoty, přepsatelné v šabloně)
  StopsDataSource           = "ZASTAVKY"
  DayGroupsDataSource       = "SKUPINY_DNU"
  DeparturesDataSource      = "ODJEZDY"
  NotesDataSource           = "POZNAMKY"

  // Společné vizuální
  FontFamily                = "Arial"
  FontSize                  = 8f              // pt
  HeaderFontSize            = 9f
  GridColor                 = "#FFD0D0D0"     // barva mřížky
  HeaderBackgroundColor     = "#FFE0E0E0"     // pozadí záhlaví
  HeaderForeColor           = "#FF000000"

Společné metody (protected):
  BuildStopIndex(data)          → List<StopData>
  BuildDayGroups(data)          → List<DayGroupData>
  BuildDeparturesByGroup(data)  → Dict<groupId, Dict<hour, List<DepartureData>>>
  BuildNotesLegend(data)        → List<NoteData>
```

---

## StopScheduleElement — zastávkový JŘ (nejkomplexnější)

Odpovídá starým sekcím `stSekceSeznamZastavek` + `stMinutyOdjezdu` + `stPoznamky`. Pokrývá **MHD i meziměsto** jedním elementem se dvěma display módy.

### Properties — kompletní seznam z legacy analýzy

```csharp
// ═══════════════════════════════════════════
// Display mód
// ═══════════════════════════════════════════
DisplayMode = "MinutesOnly"  // "FullTime" (meziměsto) | "MinutesOnly" (MHD)

// ═══════════════════════════════════════════
// Levé sloupce (tabulka zastávek)
// ═══════════════════════════════════════════
ShowStopNameColumn = true           // název zastávky (vždy zapnuto?)
StopNameColumnWidth = 50f           // mm
ShowStopIndentation = true          // odsazení vnořených zastávek
ShowTravelTimeColumn = false        // čas jízdy od aktuální zastávky
TravelTimeColumnWidth = 8f          // mm
ShowTcColumn = false                // tarifní číslo
TcColumnWidth = 8f
ShowZoneColumn = false              // zóna
ZoneColumnWidth = 12f
ShowRouteLettersColumn = false      // průběhová písmenka
RouteLettersColumnWidth = 8f
ShowBaseRouteColumn = false         // základní trasa (0/1)
BaseRouteColumnWidth = 6f
ShowSignalStopIndicator = true      // indikátor "na znamení" (z NA_ZNAMENI)

// ═══════════════════════════════════════════
// Zobrazení atributů a poznámek u zastávek
// ═══════════════════════════════════════════
ShowStopAttributes = true           // speciální znaky za názvem (♿, 🚌, ...)
ShowStopNotes = true                // poznámkové písmena za názvem (A, B, ...)

// ═══════════════════════════════════════════
// Zvýraznění aktuální zastávky
// ═══════════════════════════════════════════
HighlightCurrentStop = true         // tučné/podtržené JE_AKTUALNI=true
CurrentStopFontWeight = "Bold"
CurrentStopUnderline = true

// ═══════════════════════════════════════════
// Projeté zastávky
// ═══════════════════════════════════════════
ShowPassedStops = false             // zastávky před aktuální (legacy: TiskProjetychZast)
PassedStopsForeColor = "#FF999999"  // šedě

// ═══════════════════════════════════════════
// Mřížka minut odjezdů (DisplayMode = "MinutesOnly")
// ═══════════════════════════════════════════
ShowDayGroupHeaders = true          // nadpisy skupin dnů ("PRACOVNÍ DNY", ...)
DayGroupHeaderHeight = 8f           // mm
HourColumnWidth = 8f                // mm — sloupec "Hod"
MinuteWidth = 6f                    // mm — šířka jedné minuty v mřížce
MinuteFontSize = 7f                 // pt

// ═══════════════════════════════════════════
// Časová osa
// ═══════════════════════════════════════════
TimeAxisMode = "Compact"            // "Compact" (jen hodiny s odjezdy) | "Full" (min-max) | "Range"
TimeAxisRangeFrom = 0               // pro režim "Range" — od hodiny
TimeAxisRangeTo = 23                // pro režim "Range" — do hodiny

// ═══════════════════════════════════════════
// Matice zastávky × spoje (DisplayMode = "FullTime")
// ═══════════════════════════════════════════
ConnectionColumnWidth = 10f         // mm — šířka jednoho spoje
GroupByDayType = true               // seskupit sloupce po typech dne
GroupHeaderHeight = 10f             // mm — výška hlavičky skupiny
MaxConnectionsPerBlock = null       // null = auto-fit na šířku; číslo = overflow do bloků

// ═══════════════════════════════════════════
// Anotace u minut / spojů
// ═══════════════════════════════════════════
ShowAnnotationSymbols = true        // symboly u minut (♿, NZ, text)
AnnotationFontSize = 5f             // pt — menší než minuta
ShowAbbreviatedLineName = false     // zkrácené označení linky u minuty (pro sdílené zastávky)

// ═══════════════════════════════════════════
// Legenda poznámek (pod mřížkou)
// ═══════════════════════════════════════════
ShowNotesLegend = true              // automatická legenda
NotesLegendFontSize = 7f
NzBackgroundColor = "#FF000000"     // NZ: bílé na černém pozadí
NzForeColor = "#FFFFFFFF"

// ═══════════════════════════════════════════
// Barevné odlišení (nový ekvivalent BarevneOdlisitCeny)
// ═══════════════════════════════════════════
// Data-driven: Caché pošle FORE_COLOR/BACK_COLOR přímo v ODJEZDY
// Template-driven: ColorRules vyhodnocované ExpressionEvaluatorem
ColorRules = []                     // [{ Condition, ForeColor, BackColor }]

// ═══════════════════════════════════════════
// Vyloučené atributy
// ═══════════════════════════════════════════
ExcludedAttributeIds = []           // atributy k vyloučení z tisku (legacy: NeAtributID)
```

### Display mód "MinutesOnly" (MHD styl)

Odpovídá legacy sekcím `stSekceSeznamZastavek` (vlevo) + `stMinutyOdjezdu~SMO1–4` (vpravo/dole) + `stPoznamky` (legenda).

```
┌ StopScheduleElement ──────────────────────────────────────────┐
│                                                               │
│ ┌─ Tabulka zastávek ────────────────────────────────────────┐ │
│ │ TC │ Zastávka              │ Čas │ Zóna │ Trasa │         │ │
│ │ 101│ Líšeň, Novolíšeňská♿│  0  │  A1  │   1   │         │ │
│ │ 102│ Líšeň, Mifkova  ×    │  2  │  A1  │   1   │         │ │
│ │ 103│ ▸Šlapanice            │  5  │  A2  │   1   │         │ │
│ │ ...│ ...                   │ ... │ ...  │  ...  │         │ │
│ └───────────────────────────────────────────────────────────┘ │
│                                                               │
│ ┌─ PRACOVNÍ DNY mimo prázdniny ─────────────────────────────┐ │
│ │ Hod │ Minuty                                              │ │
│ │  5  │ 15  30ᴬ 45                                          │ │
│ │  6  │ 00  12  24  36  48                                  │ │
│ │  7  │ 03  15  27♿ 39  51²²                               │ │
│ │ ... │ ...                                                 │ │
│ └───────────────────────────────────────────────────────────┘ │
│                                                               │
│ ┌─ SOBOTY ──────────────────────────────────────────────────┐ │
│ │ Hod │ Minuty                                              │ │
│ │  7  │ 00  30                                              │ │
│ │ ... │ ...                                                 │ │
│ └───────────────────────────────────────────────────────────┘ │
│                                                               │
│ ┌─ Legenda ─────────────────────────────────────────────────┐ │
│ │ ♿ Spoj s bezbariérovým vozidlem                           │ │
│ │ ²² Nejede 24.12.                                          │ │
│ │ A  Končí v zast. Rudolfov                                 │ │
│ └───────────────────────────────────────────────────────────┘ │
└───────────────────────────────────────────────────────────────┘
```

### Display mód "FullTime" (meziměstský styl)

Matice: zastávky (řádky) × spoje (sloupce), seskupené po typech dne. Odpovídá legacy `TrasaMin1–15` sloupcům.

```
┌ StopScheduleElement ──────────────────────────────────────────────┐
│                                                                   │
│ ┌─ Matice zastávky × spoje ─────────────────────────────────────┐ │
│ │              │ PRACOVNÍ DNY         │ SOBOTY     │ NEDĚLE     │ │
│ │  Zastávka    │ 6:00  8:00 10:00 ... │ 7:00 11:00 │ 8:00 14:00│ │
│ ├──────────────┼──────────────────────┼────────────┼───────────│ │
│ │  Praha hl.n. │ 6:00  8:00 10:00    │ 7:00 11:00 │ 8:00 14:00│ │
│ │  Pardubice   │ 7:44  9:44 11:44    │ 8:44 12:44 │ 9:44 15:44│ │
│ │  Brno hl.n.  │ 9:30 11:30 13:30    │ 10:30 14:30│ 11:30 17:30││
│ └───────────────────────────────────────────────────────────────┘ │
│                                                                   │
│ ┌─ Legenda ─────────────────────────────────────────────────────┐ │
│ │ a  nezajíždí do zast. Pardubice                               │ │
│ │ ²² Nejede 24.12.                                              │ │
│ └───────────────────────────────────────────────────────────────┘ │
└───────────────────────────────────────────────────────────────────┘
```

**Poznámka:** Pro FullTime mód element pivotuje flat ODJEZDY do matice: pro každou zastávku najde odjezdy v dané skupině dnů a seřadí chronologicky. Každý "spoj" = jeden časový sloupec.

---

## Barvy a formátování buněk

### Starý systém
Caché posílá `ForeColor`/`BackColor` per buňka přímo v datech (inline v delimited stringu) → žádná kontrola v designeru.

Speciální případ: **BarevneOdlisitCeny** — 6 cenových prahů, každý mapuje `PocetMinut` → `ForeColor+BackColor`. Slouží k barevnému odlišení cenových pásem.

### Nový systém: Dvě cesty

**1. Data-driven barvy (z Caché):**
Pokud Caché potřebuje říct "tato buňka má barvu" (logika příliš složitá pro expression), pošle barvu přímo v `ODJEZDY[].FORE_COLOR` / `BACK_COLOR`.

```json
{ "ZASTAVKA_ID": "1", "SKUPINA_DNU_ID": "SMO1", "HODINA": 6, "MINUTA": 15,
  "FORE_COLOR": "#FFFF0000", "BACK_COLOR": "#FFFFFF00" }
```

**2. Template-driven barvy (ColorRules ze šablony):**
Element má property `ColorRules` — seznam pravidel vyhodnocovaných `ExpressionEvaluator`em:

```json
"colorRules": [
  { "condition": "{CAS_JIZDY} > 30", "foreColor": "#FFFF0000" },
  { "condition": "{TYP} == 'prujezd'", "foreColor": "#FF999999" }
]
```

**Priorita:** Data-driven barvy (z Caché) mají přednost před ColorRules (ze šablony).

**Nahrazení BarevneOdlisitCeny:** V novém systému cenové prahy mohou být buď:
- Přesunuty do Caché business logiky → Caché pošle `FORE_COLOR`/`BACK_COLOR` přímo
- Nebo implementovány jako `ColorRules` v šabloně (pokud je logika jednoduchá)

---

## Speciální znaky — rozhodnutí

**Rozhodnuto:** Caché posílá **hotové Unicode znaky** (konverze `ConvertSpecialniZnak` proběhne na Caché straně). Element jen renderuje Unicode text.

Příklady konverze (z legacy kódu):
- `108` → ♿ (nebo jiný symbol) — bezbariérovost
- `1`–`7` → symboly dnů v týdnu (pondělí–neděle)
- `"I"` → symbol invalidy

Element nepotřebuje mapovací tabulku — vše přijde jako Unicode.

---

## Matice zodpovědností

| Logika | Starý systém | Nový systém |
|---|---|---|
| Slučování zastávek za sebou | Caché (`@mywrkglb@(-100,...)`) | **Caché** — vrací už sloučená data |
| Bitové mapy tras | Caché (`TrasaBitmap`, `AllStopTrasaBitmap`) | **Caché** — vrací hotové výsledky v datech |
| Auto-poznámky k trasám (alg. 1001) | Caché (`MakeOmezTrasaTx`) | **Caché** — vrací text v `POZNAMKY[]` |
| Auto-poznámky k trasám (alg. 1002) | Caché (písmenka u zastávek + minut) | **Caché** — vrací písmenka v `ANOTACE[]` a `POZNAMKY_ZASTAVKY[]` |
| NZ značky z platností | Caché (bitmap porovnání) | **Caché** — vrací `NZ` + text v `ANOTACE[]` a `POZNAMKY[]` |
| Týdenní platnost → spec. znaky | Caché (1–7 → dny) | **Caché** — vrací Unicode znaky v `ANOTACE[]` |
| Atributy zastávek | Caché (číselník + trasa) | **Caché** — vrací v `ZASTAVKY[].ATRIBUTY[]` |
| Poznámky zastávek | Caché (z trasy per bod per směr) | **Caché** — vrací v `ZASTAVKY[].POZNAMKY_ZASTAVKY[]` |
| Konverze speciálních znaků | Caché (`ConvertSpecialniZnak`) | **Caché** — konvertuje před odesláním |
| Řazení poznámek (spec→NZ→text) | Caché (`polePoznamky` s indexy) | **Caché** — pošle seřazené |
| Grouping po typech dne | Caché (SMO1-4 jako sekce) | **Element** — seskupí podle `SKUPINA_DNU_ID` |
| Řazení odjezdů | Caché (přirozené řazení hodin/minut) | **Element** — řadí podle `HODINA`, `MINUTA` |
| Formátování buněk (barvy) | Caché (inline `^ForeColor^BackColor`) | **Element** — z `FORE_COLOR`/`BACK_COLOR` nebo `ColorRules` |
| Výběr zobrazených sloupců | Hardcoded v starém designeru | **Template** — element properties |
| Počet minut na hodinu | Caché (`NejvetsiPocetMinut`) | **Element** — spočítá z dat |
| Pozicování elementů | Caché (`SEKCE_START/END`) | **Designer** — uživatel umístí element |
| Pivotování flat dat → matice | N/A (Caché posílá sloupce) | **Element** — z flat `ODJEZDY` |
| Sestavování sekcí | Caché (per-sekce output) | **Odstraněno** — element je jeden celek |
| Serializace do delimited stringů | Caché (`#`, `^`, `&` oddělovače) | **Odstraněno** — JSON |

---

## Migrace Caché kódu — plán

### Co zachovat z `ALVA.MHD.LinkyPrintFormatMHD.MakeData()`

Business logika — přepsat do nové třídy `ALVA.JR.TiskAPI.GetData()`:

1. **Příprava trasy** — pole zastávek per směr, chronometráže
2. **Slučování totožných zastávek** — konverzní tabulka
3. **Poznamky a atributy zastávek** — z číselníku + z trasy
4. **Promítnutí spojů** — bitové mapy, časy odjezdů per zastávka per typ provozu
5. **Auto-poznámkování rozdílů** — oba algoritmy (1001 textový, 1002 písmenka)
6. **NZ generování** — z bitmap platností
7. **Týdenní platnost** — speciální znaky dnů
8. **Sestavení poznámek** — řazení (spec→NZ→text)

### Co odstranit

Layout logika — vše co produkuje wire formát pro starý designer:

1. ❌ `SEKCE_START` / `SEKCE_END` — žádné sekce
2. ❌ `SeznamZastavekDataSloupec*` — žádné serializované sloupce
3. ❌ `MinutyOdjezduDataHodiny~` — žádné `#`-delimited řádky minut
4. ❌ `PoznamkyDataPoznamky~` — žádné `#`-delimited poznámky
5. ❌ `DBFieldText~` — skaláry jdou přímo do JSON
6. ❌ `NejvetsiPocetMinutNaJedneHodine` — element si to spočítá
7. ❌ `HodinaOdjezduDataHodiny~` — element si hodiny vybere z dat
8. ❌ Všechny delimited string formáty (`^`, `#`, `&`)
9. ❌ `isZdendaPC` debug bloky

### Nová struktura výstupu

Místo `@outglb@(pgn,"key",...)` → JSON přes `%DynamicObject` / `%DynamicArray`:

```objectscript
ClassMethod GetData(ByRef sys As %String) As %Status
{
    $$$Init
    try {
        $$$FillParams
        // ... business logika (zachovaná z MakeData) ...

        // Výstup: JSON
        s json = ##class(%DynamicObject).%New()

        // Skaláry
        s values = ##class(%DynamicObject).%New()
        s values.CISLO_LINKY = cisloLinky
        s values.POPIS_LINKY = nazevLinky
        // ...
        s json.values = values

        // Kolekce
        s rows = ##class(%DynamicObject).%New()

        // ZASTAVKY
        s zastavky = ##class(%DynamicArray).%New()
        // ... naplnit z business logiky ...
        s rows.ZASTAVKY = zastavky

        // SKUPINY_DNU
        s skupiny = ##class(%DynamicArray).%New()
        // ... SMO metadata ...
        s rows.SKUPINY_DNU = skupiny

        // ODJEZDY (flat)
        s odjezdy = ##class(%DynamicArray).%New()
        // ... z @mywrkglb@(1,SmerID,1,cnt,1,TypProvozuID,hh,mm,SpojID) ...
        s rows.ODJEZDY = odjezdy

        // POZNAMKY (seřazené)
        s poznamky = ##class(%DynamicArray).%New()
        // ... z @mywrkglb@(1,SmerID,1,cnt,2,...) + (3,...) + (4,...) ...
        s rows.POZNAMKY = poznamky

        s json.rows = rows
        s ^||ML("OUT",$i(^||ML("OUT"))) = json.%ToJSON()
    }
    catch (ex) { $$$CatchErrRest }
    ret ret
}
```

---

## PageLayoutEngine — beze změn

Engine nepotřebuje žádné úpravy. Každý JŘ element:
- Implementuje `CalculateHeight()` → engine ho umístí na stránku
- Implementuje `Render(RenderContext)` → engine ho vykreslí
- Pokud se nevejde → `SplitAcrossPages` funguje automaticky
- Všechny stávající mechanismy (flow layout, hlavičky/patičky, PAGE/TOTAL_PAGES) fungují

---

## Postup implementace

| Krok | Co | Závislosti | Poznámka |
|---|---|---|---|
| 1 | `ScheduleElementBase` — abstraktní základ | žádné | DataSource properties, indexování, base rendering |
| 2 | `StopScheduleElement` — DisplayMode "MinutesOnly" | krok 1 | MHD varianta — pokrývá většinu use cases |
| 3 | Mock data pro preview (`GetMockData` na C# straně) | krok 2 | Testovací data bez Caché |
| 4 | `SkiaHeightCalculator` — měření výšky JŘ elementu | krok 2 | Pro PageLayoutEngine |
| 5 | `StopScheduleElement` — DisplayMode "FullTime" | krok 2 | Meziměstská varianta |
| 6 | PropertyDescriptors + properties panel | krok 2 | Všechny properties editovatelné v designeru |
| 7 | Caché `ALVA.JR.TiskAPI.GetData()` — nový wire formát | krok 2 | Refaktor `MakeData` → JSON výstup |
| 8 | Caché `ALVA.JR.TiskAPI.GetMockData()` — vzorová data | krok 7 | Pro preview v designeru |
| 9 | Napojení reálných dat na preview | krok 7+8 | |
| 10 | `DepartureGridElement` (pokud MHD potřebuje) | krok 1 | Samostatný element pro mřížku minut |
| 11 | `BookScheduleElement` | krok 1 + upřesnění | TBD |
| 12 | `VehicleScheduleElement` | krok 1 + upřesnění | TBD |

---

## Otevřené otázky

### Vyřešené (z legacy analýzy)

~~3. **Speciální znaky**~~ → Caché posílá hotové Unicode znaky. Element jen renderuje.

~~4. **TrasaMin sloupce**~~ → Sloupce spojů v meziměstském JŘ (matice zastávky × spoje). Pro MHD se nepoužívají. V novém systému: `DisplayMode = "FullTime"` s daty z ODJEZDY.

### Zbývající

1. **Knižní JŘ** — je to:
   a) StopScheduleElement s menšími bloky a automatickým stránkováním?
   b) Speciální layout s více matic vedle sebe na jedné stránce?
   c) Úplně jiná struktura?

2. **Vozový JŘ** — je to:
   a) Stejná matice jako zastávkový, jen osa X = oběhy místo spojů?
   b) Diagram oběhu vozidla (lineární timeline)?
   c) Něco jiného?

3. **Pracovní sloupce 3–5** (legacy dta 9, 11, 12) — pro meziměstský JŘ se plní přes `$WinTiskMhd^JRapi`. Potřebujeme analyzovat JR variantu `MakeData` stejně jako MHD? Jaký je obsah těchto sloupců pro meziměsto?

4. **BarevneOdlisitCeny** — 6 cenových prahů (PocetMinut → barvy). Přesunout do Caché (data-driven `FORE_COLOR`), nebo do šablony (`ColorRules`)? Nebo obojí?

5. **Stránkování per zastávka (PageNumber)** — legacy systém generuje jednu "stránku" per zastávka (per směr). Nový systém: jeden element = jeden směr jedné zastávky? Nebo element dostane všechny zastávky a řeší paging sám?

---

## Shrnutí

| Aspekt | Odpověď |
|---|---|
| **Je to možné?** | Ano — architektura je navržena + legacy kód analyzován |
| **Kde je složitost?** | V `StopScheduleElement` (rendering, pivotování) a v refaktoru Caché `MakeData` |
| **Co dělá Caché?** | Business logika + čistý JSON výstup (slučování, bitmapy, NZ, poznámky, konverze znaků) |
| **Co dělá template?** | Definuje vizuální nastavení přes ~30 element properties |
| **Co dělá engine?** | Generické stránkování (beze změn) |
| **Legacy wire formát?** | Kompletně nahrazen JSON. Žádné `SEKCE_START`, `#`-delimited stringy, inline barvy |
| **Kolik elementů pro JŘ?** | Min. 1 (`StopScheduleElement` s 2 módy), max. 4 (per typ JŘ) |
| **Postupný vývoj?** | Začít StopScheduleElement MinutesOnly → FullTime → mock data → Caché API → napojení |
