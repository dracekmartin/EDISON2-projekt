# Architektura specializovaných JŘ elementů

> **Stav:** Implementováno (2026-03-18). Tento dokument slouží jako referenční architektura.

---

## Třívrstvé oddělení

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
│ - StopListElement (tabulka zastávek)                    │
│ - DepartureGridElement (mřížka minut odjezdů)           │
│ - NotesLegendElement (legenda poznámek)                 │
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
│ - Grouping po typech dne                                │
│ - Výpočet layout metrik (max minut na hodinu)           │
└─────────────────────────────────────────────────────────┘
```

**Klíčový princip:** Business logika (co se děje s daty) zůstává v Caché. Layout logika (jak to vypadá) je v C# elementech a šabloně. Engine zůstává generický.

---

## Hierarchie elementů

```
TemplateElement (abstract)
├── TextElement, TableElement, ImageElement, ... (generické)
└── ScheduleElementBase (abstract — společný základ JŘ elementů)
    ├── StopListElement          — tabulka zastávek (levý sloupec)
    ├── DepartureGridElement     — mřížka minut odjezdů (pravý sloupec)
    └── NotesLegendElement       — legenda poznámek (pod mřížkou)
```

Každý element má vlastní `Render()` a `Clone()`. Sdílí base class pro společnou logiku (DataSource klíče, Build helpery, cache, auto-scale fontu).

### ScheduleElementBase — společný základ

```
DataSource klíče (editovatelné v šabloně):
  ZastavkyDataSource       = "ZASTAVKY"
  SkupinyDnuDataSource     = "SKUPINY_DNU"
  OdjezdyDataSource        = "ODJEZDY"
  PoznamkyDataSource       = "POZNAMKY"

Společné vizuální properties:
  FontFamily               = "Arial"
  FontSize                 = 8f              // pt
  HeaderFontSize           = 9f
  GridColor                = "#FFD0D0D0"
  HeaderBackgroundColor    = "#FFE0E0E0"
  HeaderForeColor          = "#FF000000"

Společné metody (protected):
  BuildZastavky(data)       → List<ZastavkaData>
  BuildSkupinyDnu(data)     → List<SkupinaDnuData>
  BuildOdjezdyByGroup(data) → Dict<groupId, Dict<hour, List<OdjezdData>>>
  BuildPoznamky(data)       → List<PoznamkaData>
  ComputeFontScale(naturalH) → float (auto-scale při přetečení pevné Height)
  InvalidateCache()          → vymazání cache při změně DataSource
```

### StopListElement

Tabulka zastávek s konfigurovatelnými sloupci (TC, název, čas jízdy, zóna, průběhy, základní trasa). Každý sloupec má `Key`, `Header`, `Width`, `Visible`, `Alignment`. Sloupce editovatelné přes `StopColumnsEditorDialog`.

Další properties: `ShowStopIndentation`, `ShowSignalStopIndicator`, `SignalStopSymbol`, `ShowStopAttributes`, `ShowStopNotes`, `HighlightCurrentStop`, `CurrentStopUnderline`, `ShowPassedStops`, `PassedStopsForeColor`, `ExcludedAttributeIds`.

### DepartureGridElement

Mřížka minut odjezdů. Klíčové properties:
- `SkupinaDnuId` — filtr skupin ("" = všechny, "SMO1,SMO2" = výběr)
- `HourColumnWidth`, `MinuteWidth`, `MinuteFontSize`, `MinuteBold`
- `ShowSkupinaDnuHeader`, `SkupinaDnuHeaderLineHeight`, `HeaderVerticalAlignment`
- `TimeAxisMode` — `Compact` / `Full` / `Range` + `TimeAxisRangeFrom/To`
- `GroupSeparatorWidth`, `AlternatingRowColor`, `DataRowHeight`
- `ShowAnotace`, `AnotaceFontSize`
- `GroupColumns: List<DepartureGroupColumnStyle>` — per-column nastavení (viditelnost, barvy, font, zarovnání, bold/italic) editovatelné přes `DepartureGroupEditorDialog`
- `ColorRules: List<ColorRule>` — template-driven barvy

### Jednotná kaskáda stylů buněk

Všechny tři tabulkové JŘ elementy (StopList, DepartureGrid, NotesLegend) používají spolu s `TableElement` sdílenou 4-vrstvou kaskádu `column.Style → element.HeaderStyle/DataStyle → element-specific defaults → CellDefaults`. Rendering přes `Rendering/CellRenderer.DrawCell` — jediné místo v aplikaci pozicování textu buňky (bg → padding → HAlign+VAlign → decorations). Dialogy sloupců (`StopColumnsEditorDialog`, `DepartureGroupEditorDialog`, `TableEditorDialog`) používají sdílený `Controls/CellStyleEditor` UserControl.

Detaily patternu + element-side pattern (`Resolve*Style` helpery, pre-created per-column fonty/painty pro perf) viz `.claude/rules/mvvm-patterns.md §Jednotná kaskáda stylů buněk`.

### NotesLegendElement

Legenda poznámek: `HeaderText`, `NoteSeparator`, `NoteFontSize`, `NoteLineHeight`, `NegativniZnackaBackColor/ForeColor`, `ShowNegativniZnacky`, `ShowSpecialniZnaky`, `ShowTextovePoznamky`.

---

## Display mód "MinutesOnly" (MHD styl)

```
┌ StopListElement ─────────┐ ┌ DepartureGridElement(s) ──────────────────┐
│ TC │ Zastávka          │Čas│ │ ┌─ PRACOVNÍ DNY mimo prázdniny ─────────┐ │
│ 101│ Líšeň, Novolíšeň.♿│ 0 │ │ │ Hod │ Minuty                          │ │
│ 102│ Líšeň, Mifkova  × │ 2 │ │ │  5  │ 15  30ᴬ 45                      │ │
│ 103│ ▸Šlapanice        │ 5 │ │ │  6  │ 00  12  24  36  48              │ │
│ ...│ ...               │...│ │ │  7  │ 03  15  27♿ 39  51²²            │ │
└──────────────────────────┘ │ │ ... │ ...                              │ │
                             │ └──────────────────────────────────────────┘ │
                             │ ┌─ SOBOTY ────────────────────────────────┐ │
                             │ │ Hod │ Minuty                            │ │
                             │ │  7  │ 00  30                            │ │
                             │ │ ... │ ...                               │ │
                             │ └──────────────────────────────────────────┘ │
                             └─────────────────────────────────────────────┘
┌ NotesLegendElement ──────────────────────────────────────────────────────┐
│ ♿ Spoj s bezbariérovým vozidlem                                         │
│ ²² Nejede 24.12.                                                        │
│ A  Končí v zast. Rudolfov                                               │
└──────────────────────────────────────────────────────────────────────────┘
```

**Poznámka:** StopListElement, DepartureGridElement a NotesLegendElement jsou 3 nezávislé elementy. Uživatel je umístí na canvas vedle sebe / pod sebe. Mohou být v jedné sekci nebo ve více sekcích.

---

## Barvy a formátování buněk

### Dvě cesty

**1. Data-driven barvy (z Caché):**
Pokud Caché potřebuje říct "tato buňka má barvu" (logika příliš složitá pro expression), pošle barvu přímo v `ODJEZDY[].FORE_COLOR` / `BACK_COLOR` jako `#AARRGGBB`.

**2. Template-driven barvy (ColorRules ze šablony):**
`DepartureGridElement.ColorRules` — seznam pravidel vyhodnocovaných `ExpressionEvaluator`em:

```json
"colorRules": [
  { "condition": "{CAS_JIZDY} > 30", "foreColor": "#FFFF0000" },
  { "condition": "{TYP} == 'prujezd'", "foreColor": "#FF999999" }
]
```

**Priorita:** Data-driven barvy (z Caché) mají přednost před ColorRules (ze šablony).

**Legacy ekvivalent BarevneOdlisitCeny** (6 cenových prahů) může být implementován buď jako data-driven barvy z Caché, nebo jako ColorRules v šabloně.

---

## Speciální znaky

Caché posílá **hotové Unicode znaky** (konverze `ConvertSpecialniZnak` proběhne na Caché straně). Elementy jen renderují Unicode text. Žádná mapovací tabulka na .NET straně.

---

## Matice zodpovědností

| Logika | Starý systém | Nový systém |
|---|---|---|
| Slučování zastávek za sebou | Caché | **Caché** — vrací už sloučená data |
| Bitové mapy tras | Caché | **Caché** — vrací hotové výsledky v datech |
| Auto-poznámky k trasám (alg. 1001) | Caché (`MakeOmezTrasaTx`) | **Caché** — vrací text v `POZNAMKY[]` |
| Auto-poznámky k trasám (alg. 1002) | Caché (písmenka) | **Caché** — vrací písmenka v `ANOTACE[]` a `POZNAMKY_ZASTAVKY[]` |
| NZ značky z platností | Caché (bitmap porovnání) | **Caché** — vrací v `ANOTACE[]` a `POZNAMKY[]` |
| Týdenní platnost → spec. znaky | Caché (1–7 → dny) | **Caché** — vrací Unicode v `ANOTACE[]` |
| Atributy zastávek | Caché (číselník + trasa) | **Caché** — vrací v `ZASTAVKY[].ATRIBUTY[]` |
| Poznámky zastávek | Caché (z trasy per bod per směr) | **Caché** — vrací v `ZASTAVKY[].POZNAMKY_ZASTAVKY[]` |
| Konverze speciálních znaků | Caché (`ConvertSpecialniZnak`) | **Caché** — konvertuje před odesláním |
| Řazení poznámek (spec→NZ→text) | Caché | **Caché** — pošle seřazené |
| Grouping po typech dne | Caché (SMO1–4 jako sekce) | **Element** — seskupí podle `SKUPINA_DNU_ID` |
| Řazení odjezdů | Caché | **Element** — řadí podle `HODINA`, `MINUTA` |
| Formátování buněk (barvy) | Caché (inline `^ForeColor^BackColor`) | **Element** — z `FORE_COLOR`/`BACK_COLOR` nebo `ColorRules` |
| Výběr zobrazených sloupců | Hardcoded v starém designeru | **Template** — element properties |
| Počet minut na hodinu | Caché (`NejvetsiPocetMinut`) | **Element** — spočítá z dat |
| Pozicování elementů | Caché (`SEKCE_START/END`) | **Designer** — uživatel umístí element |
| Pivotování flat dat → matice | N/A (Caché posílal sloupce) | **Element** — z flat `ODJEZDY` |
| Sestavování sekcí | Caché (per-sekce output) | **Odstraněno** — elementy jsou nezávislé celky |
| Serializace do delimited stringů | Caché (`#`, `^`, `&` oddělovače) | **Odstraněno** — JSON |

---

## Související dokumentace

- **Wire formát (JSON příklad, GetData/GetMockData kontrakt):** viz `.claude/rules/cache-api.md`
- **Business logika Caché (fáze zpracování, working globals):** viz `.claude/rules/cache-business-logic.md`
- **Changelog:** viz `.claude/docs/changelog.md`
