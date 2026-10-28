# Architektura elementů - JŘ elementy, sdílené tabulkové jádro, symboly

> Sloučeno 2026-07-13 ze 3 dokumentů (schedule-elements-architecture, tabulkove-elementy-jadro,
> symboly-jr-katalog). Část 1 srovnaná s kódem 17. 8. (`B94`) - hierarchie měla o dvě vrstvy méně
> a výčty vlastností byly rozešlé s kódem.
>
> ⚠️ **Výčty vlastností a signatur sem nepatří** - zestárnou dřív než kód a nikdo si toho nevšimne
> (přesně to se stalo do 17. 8.). Piš sem jen to, co se z kódu nepozná: proč je hranice tam, kde je,
> co je vědomá výjimka, co se nemá sjednocovat.



<!-- ══════════════════════════════════════════════════════════════════════ -->
# ==== ČÁST: Specializované JŘ elementy (wire formát, hierarchie, zodpovědnosti) ====
<!-- ══════════════════════════════════════════════════════════════════════ -->

# Architektura specializovaných JŘ elementů

> **Stav:** Implementováno (2026-03-18). Tento dokument slouží jako referenční architektura.

---

## Třívrstvé oddělení

```
┌─────────────────────────────────────────────────────────┐
│ CACHÉ - Vrstva 1: Čistá data + Business logika          │
│                                                         │
│ GetData() vrací:                                        │
│ - Skalární hodnoty (číslo linky, platnost, dopravce)    │
│ - Kolekce ZASTAVKY (s atributy, zónami, poznámkami)     │
│ - Kolekce SKUPINY_DNU (SMO metadata + nadpisy)          │
│ - Kolekce ODJEZDY (flat: zastávka × skupina × čas)      │
│ - Kolekce POZNAMKY (legenda - seřazená)                 │
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
│ TEMPLATE - Vrstva 2: Layout + Vizuální nastavení        │
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
│ ENGINE + ELEMENTY - Vrstva 3: Výpočet + Vykreslení      │
│                                                         │
│ PageLayoutEngine: generický (sekce → stránky) - BEZE    │
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
├── TextElement, TableElement, DynamicTableElement, ImageElement, ... (generické)
└── ScheduleElementBase (abstract - GENERICKÁ tabulková vrstva: styly + kaskáda,
    │                    auto-scale fontu, typeface cache, render helpery)
    ├── LineTableElement         - tabulka celé linky (linkový / knižní JŘ)
    └── StopScheduleElementBase (abstract - ZASTÁVKOVÝ datový formát: DataSource klíče,
        │                        typovaná cache, Build* helpery nad vnořenými poli)
        ├── StopListElement      - tabulka zastávek (levý sloupec)
        ├── DepartureGridElement - mřížka minut odjezdů (pravý sloupec)
        └── NotesLegendElement   - legenda poznámek (pod mřížkou)
```

**Proč je uprostřed ještě jedna vrstva:** `LineTableElement` chce styly, kaskádu a auto-scale, ale
zastávkový datový tvar ne (má vlastní: `ZASTAVKY`/`SPOJE`/`SKUPINY_DNU`). Kdyby DataSource klíče
a `Build*` helpery zůstaly na `ScheduleElementBase`, tahal by je s sebou jako mrtvý balast. Hranice
tedy je: `ScheduleElementBase` = jak to vypadá, `StopScheduleElementBase` = jak vypadají zastávková
data. Tři úrovně jsou strop (viz `mvvm-patterns.md` §inheritance vs composition).

Každý element má vlastní `Render()` a `Clone()`.

### StopScheduleElementBase - zastávkový datový základ

```
DataSource klíče (editovatelné v šabloně):
  ZastavkyDataSource         = "ZASTAVKY"
  SkupinyDnuDataSource       = "SKUPINY_DNU"
  OdjezdyDataSource          = "ODJEZDY"
  PoznamkyDataSource         = "POZNAMKY"

Build* helpery (protected) - vnořená pole DataContextu → runtime POCO, s cache:
  BuildZastavky(data)          → List<ZastavkaData>
  BuildSkupinyDnu(data)        → List<SkupinaDnuData>
  BuildOdjezdyBySkupina(data)  → Dict<skupina, Dict<hodina, List<OdjezdData>>>
  BuildPoznamky(data)          → List<PoznamkaData>
  InvalidateCache()            → vymazání cache při změně DataSource
```

Ze `ScheduleElementBase` k tomu přibývá vizuální vrstva - `HeaderStyle` / `DataStyle` (`TextStyle`,
kaskáda), `Grid` (`GridStyle`), `DataRowHeight` a `ComputeFontScale()` (auto-scale při přetečení pevné
`Height`). ⚠️ **Ploché vlastnosti typu `FontFamily` / `FontSize` / `GridColor` na base už neexistují** -
nahradila je kaskáda stylů (viz §Jednotná kaskáda v `mvvm-patterns.md`). Kde je v elementu ještě
skalární property (`MinuteFontSize`, `NoteFontSize`), je to **vědomá výjimka** a smí jen doplňovat,
nikdy přebíjet kaskádu.

### StopListElement

Tabulka zastávek s konfigurovatelnými sloupci (TC, název, čas jízdy, zóna, průběhy, základní trasa). Každý sloupec má `Key`, `Header`, `Width`, `Visible`, `Alignment`. Sloupce editovatelné přes `StopColumnsEditorDialog`.

**Doménové odlišnosti** (proč je to vlastní element, ne tabulka z dat): zvýraznění aktuální zastávky
a ztišení projetých (obojí per-řádkový override nad kaskádou, viz „feature override" v
`mvvm-patterns.md`), odsazení a značky před názvem, filtr projížděných zastávek, variantní časové
sloupce. Aktuální seznam vlastností drží třída a formulář `StopListElementEdit.cls` - **výčet sem
nepatří**, zestárne dřív než kód.

### DepartureGridElement

Mřížka minut odjezdů - **pivot**, ne řádková tabulka: jedna buňka je hodina × skupina dnů a nese
N odjezdů. Doménové odlišnosti: časová osa (kompaktní / plná / rozsah), hodiny přes půlnoc, hodinový
sloupec zvlášť pro každou skupinu, invertované minuty u negativních značek, anotace u minuty.
Per-sloupcové nastavení nese `GroupColumns` (`DepartureGroupColumnStyle`) a edituje
`DepartureGroupEditorDialog`.

⚠️ **Element má pět zón** (hodina, záhlaví skupiny, minuta, symbol anotace, text anotace), ne dvě -
proto má víc skalárních vlastností v panelu než ostatní tabulky a **nemá dialog s taby po zónách**;
je to vědomá výjimka, ne nedodělek (`mvvm-patterns.md` §Známé záměrné výjimky).

### Jednotná kaskáda stylů buněk

Všechny tři tabulkové JŘ elementy (StopList, DepartureGrid, NotesLegend) používají spolu s `TableElement` sdílenou 4-vrstvou kaskádu `column.Style → element.HeaderStyle/DataStyle → element-specific defaults → CellDefaults`. Rendering přes `Rendering/CellRenderer.DrawCell` - jediné místo v aplikaci pozicování textu buňky (bg → padding → HAlign+VAlign → decorations). Okna se sloupci (`StopColumnsEditorDialog`, `DepartureGroupEditorDialog`, `LineTableColumnsEditorDialog`, `DynamicTableEditorDialog`) mají sdílenou záložku Sloupce `Controls/ColumnsTab` s editorem stylu `Controls/CellStyleEditor`.

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

## Dva režimy výšky - pevné plátno proti tekoucímu dokumentu

Není to dvojí cesta k témuž, jsou to **dva různé druhy sestavy** a míchání je past
(nález Jan 7. 9. při stavbě linkové šablony).

**Pevné plátno - zastávková vývěska.** Papír je daný, jedna zastávka = jedna stránka do vitríny.
Systémové šablony proto mají **jednu sekci 146,6 mm s „Držet pohromadě"** a prvkům dávají **pevnou
výšku** (seznam zastávek 120,4 · minuty odjezdů 120,4 · poznámky 103,1).
Pevná výška obsah **nezřezává, ale zmenšuje do fitu** (`ScheduleElementBase.ComputeFontScale`) -
a to je tady **záměr**: vývěska musí vyjít na jednu stránku, i když má zastávka o pět poznámek víc.

**Tekoucí dokument - linkový a knižní JŘ.** Obsahu je proměnlivě mnoho (30 nebo 300 zastávek)
a papír se přidá. Prvky mají **výšku prázdnou**, rozměr se řídí velikostí písma a výškou řádku,
sekce se smí dělit a co se nevejde, pokračuje na další stránce.

**Volí se to jako celek, ne per prvek:** buď pevná sekce + pevné výšky + „Držet pohromadě", nebo
prázdné výšky + „Rozdělit přes stránky".

⚠️ **Důsledky, které se z kódu nepoznají:**

- **Hlášení o zmenšení písma je vývěskový nástroj** (`ContentShrinkWarnings`, vzniklo z #13158:
  u vývěsky vyjde každá zastávka jinak podle počtu poznámek). V linkovém JŘ je pevná výška
  u rostoucího prvku skoro vždy omyl - legenda se pak vykreslí písmem na 46 % a nikdo nepozná proč.
- **Z palety proto přicházejí bez výšky prvky, které rostou z dat** (tabulka linky, legenda
  poznámek), a s výškou ty, které patří na pevné plátno. Seznam zastávek a minuty odjezdů
  s pevnou výškou zůstávají i technicky: seznam si render ořezává na výšku prvku a minuty mají
  volbu „Roztáhnout na výšku", která pevnou výšku potřebuje.
- **Výška sekce je v obou režimech minimum, ne strop** (`max(zadaná, obsah)`), takže v tekoucím
  dokumentu zadaná výška nad obsah zůstane jako mezera pod prvkem.

---

## Barvy a formátování buněk

### Dvě cesty

**1. Data-driven barvy (z Caché):**
Pokud Caché potřebuje říct "tato buňka má barvu" (logika příliš složitá pro expression), pošle barvu přímo v `ODJEZDY[].FORE_COLOR` / `BACK_COLOR` jako `#AARRGGBB`.

**2. Template-driven barvy (ColorRules ze šablony):**
`DepartureGridElement.ColorRules` - seznam pravidel vyhodnocovaných `ExpressionEvaluator`em:

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
| Slučování zastávek za sebou | Caché | **Caché** - vrací už sloučená data |
| Bitové mapy tras | Caché | **Caché** - vrací hotové výsledky v datech |
| Auto-poznámky k trasám (alg. 1001) | Caché (`MakeOmezTrasaTx`) | **Caché** - vrací text v `POZNAMKY[]` |
| Auto-poznámky k trasám (alg. 1002) | Caché (písmenka) | **Caché** - vrací písmenka v `ANOTACE[]` a `POZNAMKY_ZASTAVKY[]` |
| NZ značky z platností | Caché (bitmap porovnání) | **Caché** - vrací v `ANOTACE[]` a `POZNAMKY[]` |
| Týdenní platnost → spec. znaky | Caché (1–7 → dny) | **Caché** - vrací Unicode v `ANOTACE[]` |
| Atributy zastávek | Caché (číselník + trasa) | **Caché** - vrací v `ZASTAVKY[].ATRIBUTY[]` |
| Poznámky zastávek | Caché (z trasy per bod per směr) | **Caché** - vrací v `ZASTAVKY[].POZNAMKY_ZASTAVKY[]` |
| Konverze speciálních znaků | Caché (`ConvertSpecialniZnak`) | **Caché** - konvertuje před odesláním |
| Řazení poznámek (spec→NZ→text) | Caché | **Caché** - pošle seřazené |
| Grouping po typech dne | Caché (SMO1–4 jako sekce) | **Element** - seskupí podle `SKUPINA_DNU_ID` |
| Řazení odjezdů | Caché | **Element** - řadí podle `HODINA`, `MINUTA` |
| Formátování buněk (barvy) | Caché (inline `^ForeColor^BackColor`) | **Element** - z `FORE_COLOR`/`BACK_COLOR` nebo `ColorRules` |
| Výběr zobrazených sloupců | Hardcoded v starém designeru | **Template** - element properties |
| Počet minut na hodinu | Caché (`NejvetsiPocetMinut`) | **Element** - spočítá z dat |
| Pozicování elementů | Caché (`SEKCE_START/END`) | **Designer** - uživatel umístí element |
| Pivotování flat dat → matice | N/A (Caché posílal sloupce) | **Element** - z flat `ODJEZDY` |
| Sestavování sekcí | Caché (per-sekce output) | **Odstraněno** - elementy jsou nezávislé celky |
| Serializace do delimited stringů | Caché (`#`, `^`, `&` oddělovače) | **Odstraněno** - JSON |

---

## Související dokumentace

- **Wire formát (JSON příklad, GetData/GetMockData kontrakt):** viz `.claude/rules/cache-api.md`
- **Business logika Caché (fáze zpracování, working globals):** viz `.claude/docs/cache-business-logic.md`
- **Changelog:** viz `.claude/docs/changelog.md`



<!-- ══════════════════════════════════════════════════════════════════════ -->
# ==== ČÁST: Sdílené tabulkové jádro (co sdílet / co nesjednocovat) ====
<!-- ══════════════════════════════════════════════════════════════════════ -->

# Tabulkové elementy - sdílené jádro vs element-specifické (architektura)

> **Kdy číst:** před přidáním nového tabulkového elementu, před úpravou render/layout logiky
> některé tabulky, nebo když se rozhoduje „mám tohle vytáhnout do jádra?". Evergreen referenční
> dokument (stav R1 hotový 2026-07-04). Návrh/rozhodovací proces smazán - viz git historie
> (`r1-sdilene-tabulkove-jadro-navrh.md`).

## 0. Základní princip: dvě patra, konzistence žije na obou různě

Každý tabulkový element má **dvě patra**, která se sjednocují nezávisle:

| Patro | Co to je | Konzistence |
|---|---|---|
| **Data / nastavení** | model sloupců, kaskáda stylů, editory, 2-tab dialog | **sdílené VŠEMI** tabulkovými elementy (i pivotem DepartureGrid) |
| **Render / layout** | jak se to skládá na plátno (šířky, řádková smyčka) | sdílené jen mezi **řádkovými** tabulkami; pivot je mimo |

Klíč: „konzistentní" ≠ „stejný render". Element může sdílet vše, co uživatel edituje (a být tak
plně konzistentní z jeho pohledu), a přitom mít vlastní render, když je jeho tvar jiný.

---

## 0b. Klasifikace featur - kam nová tabulková featura patří (rozhodovací pravidlo)

> **Vzniklo z diskuze 2026-07-05.** Chrání proti dvěma extrémům: (a) duplikaci téže featury ručně
> v každém elementu, (b) cpaní generických featur do specializovaných elementů, kam nepatří.

Když přidáváš tabulkovou featuru, zařaď ji do jedné ze tří kategorií - tím je rozhodnuto, kam patří:

| Kategorie | Poznávací znak | Kam patří | Příklady |
|---|---|---|---|
| **1. Nad daty řádku** | čte jen hodnoty jednoho řádku (`DataContext`), nezávisí na způsobu renderu | **sdílený helper** (à la `RowConditions`), zapoj do **všech** řádkových tabulek | viditelnost řádku, zvýraznění řádku, (budoucí) styl řádku dle podmínky |
| **2. Struktura sloupce / počet řádků** | definuje, CO je sloupec nebo KOLIK řádků; předpokládá uživatelsky definované datové sloupce | **jen generická `DynamicTable`** (`TableColumn`) | počítaná pole (`Expression`), mapování `Field`, `Format`, min/max řádků |
| **3. Doménové renderování** | opinionated vzhled konkrétní sestavy | **element-specifické, nikdy nesdílet** | šipka směru, podbarvení aktuální zastávky, pivot časů spojů, terminus-skip |

**Proč kategorie 2 nepatří do StopList/LineTable:** jejich sloupce jsou *doménové sloty*
(`StopColumn.Key` = Název/TČ/Zóna/Km…), ne uživatelsky mapovaná data. „Počítaný sloupec" ani
„doplň prázdné řádky" na seznamu konkrétních zastávek nedává smysl.

**Únikový východ pro data, ne pro element:** generické featury (kat. 2) jsou pro daná data
dostupné vždy přes **`DynamicTable` napojenou na tutéž kolekci** (např. `ZASTAVKY`). Specializovaný
element je volba „chci doménový vzhled", ne „tady featura nejde". Dva nástroje na stejná data,
různý účel - jako Table vs Matrix v reportovacích nástrojích. Featury se **needuplikují**, žijí tam,
kam podle účelu patří.

**NEslučovat** `StopColumn` a `TableColumn` do jedné třídy ani pod společné `IColumn` rozhraní
(zamítnuto v R1: migrace JSON gold-ověřených šablon, děravá abstrakce). Sdílíme **chování**
(`TableLayout`, `CellRenderer`, `RowConditions`), ne datovou třídu.

---

## 1. Co je SDÍLENÉ - používej, neduplikuj

Vše v `dotnet/Rendering/`:

| Prvek | Co dělá | Používá |
|---|---|---|
| `RowPlanRenderer.DrawRows` + `RowSpec`/`RowKind` | **svislá smyčka**: element popíše řádky (druh, výška, buňky, pozadí, čáry) a jádro je nakreslí pod sebe. `RowKind` nese *význam* řádku (`Header`/`GroupHeader`/`Data`/`SubRow`/`Summary`) pro budoucí řádkové featury (řezání stránek, opakování mezisoučtu). | StopList, LineTable, DynamicTable, TableElement. **DepartureGrid mimo** = pivot |
| `ColumnRenderer.DrawRow` + `CellSpec` | **vodorovná** vrstva pod tím: jeden řádek = sekvence buněk + volitelné svislé čáry | volá ji `RowPlanRenderer`; přímo jen StopList (řádek mimo plán). Elementy staví `CellSpec` a zbytek přenechají plánu |
| `CellRenderer.DrawCell` | jedna buňka: bg → padding → HAlign/VAlign → decorations. **Jediné místo pozicování textu buňky.** | přes `ColumnRenderer` všechny řádkové tabulky; přímo DepartureGrid (hodinové buňky) |
| `TableLayout.ResolveWidths` | šířky: pevné (mm/%) + auto-split zbytku rovným dílem; **nevejde-li se, zmenší šířky na šířku elementu** (místo dřívějšího oříznutí) | StopList, DynamicTable, TableElement |
| `TableLayout.ComputeWidthScale` | vodorovný protějšek `ComputeFontScale` - škála pro **fonty a výšky řádků**, aby se tabulka vešla do šířky. Element ji kombinuje s výškovou přes `MathF.Min` a předává do `CellRenderer` jako `fontScale`. Bez ní na šířku reagovalo jen zmenšování textu v jednotlivé buňce → vedle sebe různé velikosti písma; a přebytek se tiše oříznul (svislé varování hlídá jen výšku) | StopList, DynamicTable, LineTable (vlastní výpočet šířek → skalární varianta), TableElement. **DepartureGrid mimo** = pivot, málo sloupců |
| `TableLayout.ResolveCascade` | kaskáda stylu buňky `column → element → baked` + legacy alignment fallback | StopList, DynamicTable, LineTable, TableElement |
| `TableStyleDefaults` | zapečený vzhled buňky tabulky (tučné záhlaví, barvy textu, souhrnný řádek) + podbarvení záhlaví nové tabulky | pevná tabulka + tabulka z dat. JŘ prvky mimo - mají vlastní, jinak vypadající výchozí hodnoty (`ScheduleElementBase`) |
| `CellDefaults.Default` | poslední vrstva kaskády (Arial 9pt, padding 0.5mm, VAlign Middle…) | všechny |
| `ColumnHeader.Resolve` | sémantika „override přebíjí zdroj, prázdné → zdroj" u nadpisů | všechny |
| `Models/Expressions/RowConditions` | řádková podmínka: výraz nad daty řádku → viditelnost / barva zvýraznění pozadí (fail-open) | DynamicTable + StopList + LineTable (vis+highlight); DepartureGrid mimo = pivot. Element potřebuje per-řádek DataContext (`SourceRow`); default-off = behavior-preserving |

**Editory (UI vrstva, `dotnet/Controls/` + `Windows/*EditorDialog/`):**

| Prvek | Co dělá | Používá |
|---|---|---|
| `ColumnsTab` + `ColumnsTabViewModel` + `ColumnRowVM` | celá záložka Sloupce: seznam, detail sloupce, součet šířek, Přizpůsobit, přidání/odebrání, reset | všechny čtyři okna se sloupci |
| `ColumnListEditor` + `IColumnRowVM` | levý seznam uvnitř záložky (zaškrtávátko viditelnosti, tečka vlastního stylu, ▲▼, šířka) | `ColumnsTab` |
| `CellStyleEditor` + `CellStyleVM` | tri-state editor stylu buňky (font/barvy/align/padding, „×" reset) | `ColumnsTab` |
| **2-tab dialog** (Obecné + Sloupce) | Tab 1 = EDInputForm (`*ElementEdit.cls`, element-wide props), Tab 2 = `ColumnsTab` | StopColumnsEditorDialog, DepartureGroupEditorDialog, DynamicTableEditorDialog, LineTableColumnsEditorDialog |

**2-tab dialog = závazný vzor** pro každý tabulkový element s editovatelnou kolekcí sloupců. Záložka
Sloupce se **nekopíruje** (do 28. 10. ji měla každá tabulka vlastní a rozešly se, `B194` E13c): nový
element dodá podtřídu `ColumnRowVM` (převod sloupce z/do modelu a schopnosti řádku) a vytvoří
`ColumnsTabViewModel`; pole navíc dá do `ExtraColumnTemplate` / `ExtraDataTemplate`. Detail v
`mvvm-patterns.md` §Sdílená záložka Sloupce.

---

## 2. Co je ZÁMĚRNĚ element-specifické - a proč

Tohle NEpatří do jádra; každý element si drží sám, protože je to jeho doménová odlišnost:

**StopList** (zastávková vývěska):
- Šipka směru jízdy (gutter, bracket, hlavice), markers sub-cell (znaky před názvem), current/passed
  zvýraznění (per-řádkové overridy), filtr projížděných zastávek, variantní časové sloupce (Km-expand).

**DynamicTable** (obecná tabulka):
- Souhrnné řádky s agregacemi (SUM/COUNT/AVG…), střídání řádků, řazení, zvýraznění řádků, min/max počet.

**TableElement** (pevná, ručně vyplňovaná tabulka):
- Řádky a texty buněk drží šablona (`Rows`/`Cells`), ne data - buňka nese text nebo `{PLACEHOLDER}`
  ze skalárů. Mód `Dynamic` na téže třídě zůstal **jen pro staré šablony**; z UI se nenastavuje.

**LineTable** (knižní JŘ):
- **Výpočet šířek** (Km-expanze na N sub-sloupců dle dat + časové sloupce spojů sdílejí auto pool -
  není to obecný per-sloupec vzorec, proto NEpoužívá `ResolveWidths`), pás skupin dnů, řádek platnosti/NZ,
  přípojné řádky (vyčká/navazuje), MHD auto-remove.

**DepartureGrid** (minutová mřížka) - **celý render mimo jádro**:
- Je to **pivot** (2D matice hodina × skupina dnů, jedna buňka = N odjezdů), ne řádková tabulka.
  `ColumnRenderer.DrawRow` („řádek = sekvence sloupců") jeho tvar neumí vyjádřit. Vlastní: pivot layout,
  redistribuce šířek, word-wrap víceřádkových záhlaví, HourColumnPerGroup, NZ invertované minuty,
  extended hours přes půlnoc. **Sdílí ale plně datovou vrstvu** (column model `DepartureGroupColumnStyle`,
  kaskádu, záložku Sloupce `ColumnsTab`, `CellRenderer.DrawCell` na hodinové buňky) → z pohledu
  uživatele je konzistentní.

---

## 3. Nový tabulkový element - co dostaneš zdarma, co musíš dodat

**Zdarma z jádra:** kreslení buňky, kreslení řádku, **svislá smyčka** (`RowPlanRenderer`), výpočet
šířek (pokud tvůj model je „per-sloupec"), kaskáda stylů, záložka Sloupce (`ColumnsTab` - dodáš jen
řádek sloupce), 2-tab dialog.

**Musíš dodat (tvé odlišnosti):** column model (nebo reuse `StopColumn`/`TableColumn`), mapování
„co je v buňce" (klíč → hodnota z dat), **plán řádků** (`RowSpec` per řádek s druhem, výškou
a buňkami - ne vlastní svislou smyčku), `CalculateHeightFromData`, per-row overridy pokud máš
(zvýraznění apod.), a EDInputForm schéma (`*ElementEdit.cls`) pro tab Obecné.

**Rozhodni:** je to řádková tabulka (iteruješ záznamy → řádky) → do řádkového jádra. Nebo pivot /
jiný tvar → vlastní render, ale sdílej datovou vrstvu (jako DepartureGrid).

---

## 4. Co vědomě NEsjednocovat (a proč) - nevracet se k tomu bez nového důvodu

| Nesjednocovat | Proč |
|---|---|
| **`StopColumn` + `TableColumn` do jedné třídy** | Různá serializace (riziko pro gold-ověřené šablony StopListu), různé potřeby (StopColumn = `Key` + `ColumnVisualStyle` + mm; TableColumn = `Field`/`Format` + dva `TextStyle` + mm/%). Každý konzument by nesl pole, co nepoužívá. R1 sjednotil ALGORITMY, ne datovou třídu. |
| **DepartureGrid render** | Pivot ≠ řádková tabulka (viz §2). Vtlačení by jádro zbytnělo podmínkami „když pivot" nebo by vyžadovalo rizikový přepis bez zisku. |
| **LineTable výpočet šířek** | Km-expanze + trip-pool = legitimní odlišnost, ne obecný vzorec. Kaskádu sdílí, šířky ne. |

---

## 5. Kandidáti na budoucí extrakci (triggery - udělat, až nastane důvod)

| Kandidát | Trigger | Poznámka |
|---|---|---|
| **Výška-z-fontu vs pevná mm** | další element s font-driven výškou | dnes StopList/LineTable počítají výšku z fontu, DynamicTable i TableElement mají pevné `RowHeight` mm. Sjednotit jako `RowHeight?` (null = z fontu) pokrylo by oba. |
| **Column model sjednocení** | jen kdyby vznikl konkrétní tlak + bezpečná migrace serializace | viz §4 - dnes vědomě NE. |

Pravidlo: extrahovat **až při 2+ konzumentech** stejné potřeby (ne dopředu). R1 se extrahoval právě
proto, že vznikl 3. konzument (LineTable) - do té doby by přepis gold-ověřeného StopListu nesl jen riziko.

**Hotové, ať se to nenavrhuje znovu:** *R1a - render-loop orchestrátor* je `RowPlanRenderer` (§1).
Svislou smyčku měl každý element vlastní; dnes ji mají všechny čtyři řádkové tabulky společnou a řádek
navíc nese `RowKind`, tedy svůj význam - na tom stojí řezání širokých tabulek do stránkových bloků
(`B13` v roadmapě), aby se psalo jednou nad plánem a ne pětkrát v per-element smyčkách.



<!-- ══════════════════════════════════════════════════════════════════════ -->
# ==== ČÁST: Katalog symbolů JŘ (font Timetable) ====
<!-- ══════════════════════════════════════════════════════════════════════ -->

# Katalog symbolů JŘ (značky u zastávek / spojů / v legendě)

> **Účel:** převzatá „mapa" symbolů jízdního řádu z legacy, ať ji máme **v TD** a renderujeme podle
> ní, ne ad hoc. Sada a význam symbolů vychází z **konvence/standardu** (úprava JŘ veřejné dopravy +
> CIS; SK má vlastní obdobu) - my (M-line) to jen **implementujeme** fontem + mapou. Uživatel TD si
> symboly nevolí (je to systémové chování jako NZ box); volí jen zda značky tisknout.

## Zdroje (legacy, autoritativní)

| Co | Kde |
|---|---|
| **Katalog** (id · znak · význam) | `JRMAIN/CACHE/JRapiLinPrtWin.mac` → `GSId()` ř. 1550-1616 |
| **Render-dispatch** (jak se kreslí) | `TiskJR/formTISKJRReportDotNet_Module.vb` → `Render_SekceDataLinky_Poznamky_RenderPoznamku_Symbol` ř. 6311+ |
| **Výběr fontu dle locale** | tamtéž ř. 1197-1201: `"Timetable"` → `Timetable_nove_SK` (SK) / `Timetable_nove` (CZ) |
| **NZ box (ruční)** | tamtéž `Render_SekceNegZnacka_Data` ř. 4518 (`FillRectangle(Black)` + `DrawString(White)`) |

## ⭐ Autoritativní model (Jiří Bžoch, 2026-07-09)

Rozhodl kolega (doménový vlastník legacy JR tisku). Tři kategorie značek u spoje:

1. **Kroužkované jen `1`-`7` + `+` + `X`** - kroužek dělá **speciální font**, který kroužkové glyfy má
   POUZE pro tyto hodnoty (`1`-`7` dny, `+` „kladiva"=pracovní dny, `X` „kříž"=neděle/svátky). GSId id 8-16.
   Jednoznakové → kroužkový glyf fontu.
2. **NZ / datová omezení = číslo v boxu** - VŠECHNA datová omezení (i pozitivní „jede také", i „nejede"),
   typicky vícemístná čísla, se tisknou jako **číslo v černém boxu** (invertovaně). Font pro ně kroužek
   NEMÁ (jen 1-7) → box. GSId typ 3.
3. **Atributy = značky** - piktogram ve **speciálním fontu** (vozíček, na znamení…).

Legacy do tisku posílal **značku + příznak, JAK ji tisknout** (text / speciální font + kód) - to je
přesně `GSId` (převod) + render-dispatch níže. **Zdroj pravdy = `GSId` v `JRapiLinPrtWin`** (konvence
odvozená z úpravy JŘ).

> **Upřesnění (Jiří Bžoch, 2026-07-10):** vícemístné datové značky („36 jede také… nejede…") jsou
> ve starém tisku TAKÉ **box**, ne kroužek. Kroužek dělá jen font pro hodnoty `1`-`7`. Dřívější domněnka,
> že pozitivní „jede" datové omezení má být kroužkované, byla **mylná** - proto se pokus o „pozitivní
> značku = ručně kreslený kroužek" **revertoval** (datová omezení zůstávají box, což je 1:1 s legacy).
>
> **Zbývá (deferred):** kroužkové `1`-`7`/`+`/`X` (týdenní platnost) přes font - AD je dnes neemituje
> jako jednoznakové anotace (dny řeší sloupci skupin dnů), takže je to samostatný feature, ne bug.

## Render-módy (jak se symbol kreslí)

1. **Font-glyf** - znak vykreslený v **Timetable fontu** (piktogram). Legacy: `ToChar(ID)` v `iFont1TimeTable`.
   Locale: CZ `Timetable_nove`, SK `Timetable_nove_SK` (embedded `Fonts/TimeTable2*.ttf`).
2. **Prostý text** - znak/zkratka v **normálním fontu** (ne piktogram). Legacy: `GetFont(...,"1"=Times,...)`.
   Např. `MHD`, `u`, `p`, `AMS`, `IDS`, `n`, `CLO`, `WC`, a **`x` = na znamení**.
3. **NZ box (ruční)** - negativní značka „nejede…": černý obdélník + bílé číslo. Kreslí se **ručně**,
   ne z fontu. **TD to už dělá** (`NotesLegendElement.RenderNegativniZnacka` + DepartureGrid inverzní box).
4. **Kompozit (ruční)** - font-glyf + horní/dolní index nebo + text. Legacy záporná ID (-1…-19):
   paragraf `G` + horní index 1/2/3 · metro (znak 35) + dolní index A/B/C · invalida (znak 72) + „WC".

## Katalog (GSId, id 1-66)

> Sloupec **Render** = doporučený mód v TD (odvozeno z legacy dispatch; ⚠️ = ověřit vizuálně proti fontu).
>
> **⚠️ Kde se bere kód glyfu (nalezeno 31. 8. 2026):** `ALVA.TISKJR.Main.ConvertSpecialniZnak`
> v `C:\DEV\EDISON\TiskJR\CACHE\ALVA\TISKJR\Main.cls` překládá **id z číselníku** `$$$SALL`
> (`JRapiLinPrtWin.mac:14`) na **kód znaku ve fontu**. Záporná hodnota = text nebo složená značka,
> kterou si windowsí klient kreslí vlastní větví. Do 31. 8. se myslelo, že tenhle překlad v repu
> není, a pozice se odvozovaly z významu zkratky - **je, jen v klientovi, ne v serverové části.**
> Opsané do `SymbolMap.PiktogramyLinky`: OBC 25→77, PCZ 26→112, PJI 27→76, POS 28→168, RBU 29→90,
> SAO 30→115, BEV 22→72, CBE 56→70, RBP 57→240, loď 61→180, wifi 65→186, zásuvka 66→179.
> **Textem i podle legacy:** IDS 23→„-11", NKA 24→„-12" (a klient NKA vypisuje jako „n", ne „NKA").
>
> **⚠️ Autobus: legacy 119, my 122 - a je to tak správně** (dořešeno 31. 8.). Není to odchylka, ale
> **jiný font**: starý klient sází fontem **„Timetable"** (`formTISKJRReportDotNet_Module.vb:2611`),
> my TimeTable2. Právě na 119 se sady rozcházejí - v TimeTable2 CZ je tam vagon, v SK autobus (`B49`),
> zatímco na 122 je autobus v obou. Přepsat to „kvůli paritě" by českému zákazníkovi vrátilo vagon
> u přestupu na autobus, což je vada opravená 3. 8. (#13093, #13048). Hlídá to test
> `Autobus_NemiriNaPoziciZLegacy`. **Důsledek pro budoucí opisování z legacy:** čísla v té tabulce
> jsou pozice ve **starém** fontu; u nové značky se musí ověřit v TimeTable2, ne jen opsat.
>
> **Text místo glyfu:** záporná hodnota v tabulce znamená „vypiš text", a ten text nemusí být
> zkratkou z dat - nízkokapacitní vůz (24 → „-12") se tiskne jako **„n"**, IDS (23 → „-11") jako
> „IDS" (`formTISKJRReportDotNet_Module.vb:3348-3365`). Drží to `SymbolMap.ZkratkyLinkyJinymTextem`.
>
> **Kde ty atributy v datech vůbec jsou** (změřeno 31. 8. sondou
> `LinkovyJrNastaveniProbe.AtributySpojuVyskyt` přes šest firem): BEV 48 389 linek · PJI 4 620 ·
> RBU 168 · IDS 60 · wifi 59 · RBP 22 · OBC 17 · PCZ 17 · NKA 9 · POS 1. ⚠️ **SAO, CBE a zásuvka
> nejsou nikde** - jejich pozice jsou opsané z legacy, ale na reálném tisku je nikdo neviděl.
>
> **Vývěsky jedou jinou tabulku:** `ALVA.TISKMHD.Main.ConvertSpecialniZnak` zná jen dny (1-7→49-55),
> vozíček (`I`→72), částečnou bezbariérovost (`8`→70) a svátky (`+`→43). Loď, wifi ani zásuvku
> **vůbec nemá**, takže jejich piktogram ve vývěsce by byl nová featura, ne parita.
> „znak" = mnemonický znak z číselníku (co nese datový atribut / anotace).

| id | znak | význam | render |
|---|---|---|---|
| 1 | `\|` | projíždí | font-glyf |
| 2 | `<` | jede jinudy | font-glyf |
| 3 | MHD | MHD | **text** |
| 4 | `u` | pokračuje | **text** |
| 5 | `m` | vyčká | font-glyf |
| 6 | `^` | navazuje | font-glyf |
| 7 | `p` | poznámka pro spoj a tč spoje | **text** |
| 8-14 | `1`-`7` | týdenní platnost 1-7 (dny) | font-glyf (kroužková číslice) |
| 15 | `+` | týdenní platnost + | font-glyf |
| 16 | `X` | týdenní platnost X (denně) | font-glyf |
| 17 | AMS | AMS | **text** |
| 18 | `&` | místenkování možno AMS | font-glyf |
| 19 | `R` | místenkování možno R | font-glyf |
| 20 | `Ü` | místenkování nutno AMS | font-glyf |
| 21 | `þ` | místenkování nutno R | font-glyf |
| 22 | BEV | bezbariérový (vozíček ♿) | font-glyf |
| 23 | IDS | ids | **text** |
| 24 | NKA | nízkokapacitní | font-glyf |
| 25 | OBC | občerstvení | font-glyf |
| 26 | PCZ | zavazadla | font-glyf |
| 27 | PJI | kola | font-glyf |
| 28 | POS | zásilky | font-glyf |
| 29 | RBU;r;OZO | radiobus - spoj jede na objednání | font-glyf |
| 30 | SAO | samoobslužný | font-glyf |
| 31 | `N` | jen nástup | font-glyf |
| 32 | `V` | jen výstup | font-glyf |
| **33** | **`x`** | **zastávka pouze na znamení** | **text** ⚠️ (v legendě/u zastávky prosté „x", ne piktogram) |
| 34 | `A` | autobus | font-glyf |
| 35 | `L` | vlak | font-glyf (vláček) |
| 36 | `C` | clo | font-glyf |
| 37 | `G` | paragraf | font-glyf |
| 38-40 | `a`/`b`/`c` | paragraf 1/2/3 | font-glyf |
| 41-43 | šb/šc/še | šipka dolů počátek/pokračuje/konec | font-glyf |
| 44-45 | od/př | od-do / do-do šipky | font-glyf |
| 46 | zbez | zastávka bezbariérově přístupná | font-glyf |
| 47 | WC | veřejné WC v objektu zastávky | **text** |
| 48 | `y` | na znamení od 20:00 | ⚠️ (viz #33) |
| 49 | `z` | na znamení v prac. dny od 22:00, víkend celodenně | ⚠️ (viz #33) |
| 50-52 | `e`/`f`/`g` | metro A/B/C | font-glyf (kompozit + index) |
| 53 | `O` | letadlo | font-glyf |
| 54 | RZ | občerstvení/restaurace v objektu zastávky | font-glyf |
| 55 | BWC | veřejné bezbariérové WC v objektu zastávky | font-glyf |
| 56 | CBE | částečně bezbariérový vůz | font-glyf |
| 57 | RBP;i;OZP | spoj jede pominečně | font-glyf |
| 58 | TP | terminál s pomocí osobám se ZP | font-glyf |
| 59 | ZP | zastávka upravená pro osoby s těžkým ZP | font-glyf |
| 60 | `M` | metro | font-glyf |
| 61 | lod;D | loď | font-glyf |
| 62 | PR | parkoviště P+R | font-glyf |
| 63-64 | šbu/šeu | šipka nahoru počátek/konec | font-glyf |
| 65 | wifi | wifi | font-glyf |
| 66 | zásu | zásuvka | font-glyf |

## Co reálně používá zastávkový JŘ (AD/MHD agendy)

Dnes agendy emitují jako **atributy zastávek** (StopList) + **anotace spojů** (DepartureGrid) +
**legendu** (NotesLegend) hlavně: `x` (na znamení #33), `H`/`72`+`F`/`CBE` (bezbariér #22/#56),
dny (#8-14), a **NZ** (negativní, ruční box). Ostatní katalog je rezerva pro budoucí pokrytí.

## ⚠️ Otevřené k ověření při implementaci

- **`x` na znamení = prostý text, ne font-glyf.** Legacy ho v legendě tiskne jako „x" (konvence), ne
  jako piktogram. Naivní „surový znak → Timetable font" ho vykreslí jako **vláček** (glyf na pozici `x`
  je jiný symbol) - proto musí jít přes render-mód **text**, ne font-glyf. Totéž `y`/`z`.
- ✅ **Repertoár fontu změřen (4. 8. 2026, `B10` gate)** - obrysy všech 94 tisknutelných pozic
  vytažené z obou fontů. Mapa níže; **pozice `znak` z katalogu se s pozicí ve fontu často NESHODUJE**
  (letadlo je na `n`, ne na `O`; paragraf na `P`, ne na `G`; metro na `#`, ne na `M`) - je to táž past
  jako autobus `A`→`z`, jen v širším měřítku. Implementace `B10` musí jít přes mapu, ne přes písmeno.

  | Pozice | Piktogram | | Pozice | Piktogram |
  |---|---|---|---|---|
  | `+` 0x2B | kříž | | `X` 0x58 | zkřížená kladiva |
  | `.` 0x2E | šipka doprava dolů | | `Y` 0x59 · `Z` 0x5A | telefon |
  | `/` 0x2F | hodiny | | `[` 0x5B | autovlak (auto na voze) |
  | `0`-`9` 0x30-39 | kroužkové číslice | | `a`-`j` 0x61-6A | číslice 0-9 v rámečku |
  | `:` 0x3A · `;` 0x3B | šipka nahoru / dolů | | `k` 0x6B | „K" v osmiúhelníku |
  | `=` 0x3D | zvedací plošina pro vozík | | `n` 0x6E | **letadlo** |
  | `?` 0x3F · `@` 0x40 | šipka vlevo / vpravo | | `o` 0x6F | budova |
  | `A` 0x41 · `B` 0x42 | trojúhelník | | `p` 0x70 | **zavazadla** (kufr) |
  | `D` 0x44 | křížek (CZ) / **dlaň (SK)** | | `q` 0x71 | přeškrtnutý kufr |
  | `F` 0x46 | **částečně bezbariér** | | `r` 0x72 | „R" v rámečku |
  | `G` 0x47 | nevidomý s holí | | `w` 0x77 | vagon (CZ) / **autobus (SK)** |
  | `H` 0x48 | **bezbariér** (vozíček) | | `x` 0x78 | lokomotiva (CZ) / **jednotka (SK)** |
  | `L` 0x4C | **jízdní kolo** | | `y` 0x79 | restaurace (příbor v rámečku) |
  | `M` 0x4D | příbor (restaurace) | | `z` 0x7A | **autobus** |
  | `P` 0x50 | **paragraf** § | | `}` 0x7D | informace |
  | `Q` 0x51 | sklenka | | `~` 0x7E | **P+R** |
  | `V` 0x56 | postel | | `!`-`)` 0x21-29 | loga dopravců |

  ⚠️ **Ve fontu NENÍ** loď (katalog 61), wifi (65) ani zásuvka (66) - ty tři se z tohohle fontu
  nakreslit nedají a při implementaci potřebují jiný zdroj (nebo text).

  ✅ **CZ × SK se liší přesně ve 3 z 94 pozic:** `D`, `w`, `x` (viz tabulka). Zbytek repertoáru je
  shodný, takže sdílená mapa `B10` culture parametr potřebuje **jen pro tyhle tři**.
  **Částečně ověřeno 2026-08-03** (#13048 bod 1, #13093 bod 2): pozice glyfů se **neshodují s písmeny
  číselníku**, takže datový znak se musí přeložit - **autobus `A` → `z` (0x7A)**, **vlak `L` → `x`
  (0x78, lokomotiva)**, **kola → `L` (0x4C)**; `w` (0x77) je železniční vagon, `y` (0x79) restaurace.
  Kola nemají písmeno v číselníku (je to atribut spoje), proto je agenda posílá vlastním markerem
  **`K`** - poslat rovnou `L` nelze, to je vlak. Překlad drží `StopScheduleElementBase.SymbolGlyph`
  (zamčeno `SymbolGlyphTests`) a používají ho **obě** kreslicí cesty - značka u zastávky i znak
  v legendě, jinak by se rozešly. Aplikuje se **jen na piktogramovou cestu** (`JePiktogram`):
  značka kreslená normálním fontem je písmeno zadané zákazníkem a překlad by z „A" udělal „z".
  Zároveň to vysvětluje, proč „na znamení" **musí** jít textem: na `x` je lokomotiva.

  ⚠️ **Pozice musí platit v CZ i SK fontu** (`TimeTable2.ttf` / `TimeTable2SK.ttf`, přepíná
  `FontResolver.SymbolFontFamily`) - fonty **nejsou** kopie. Porovnáno 2026-08-03: `z` (0x7A) je
  autobus a `L` (0x4C) kolo v obou, `x` (0x78) je vlak v obou (CZ parní lokomotiva, SK moderní
  jednotka), ale **`w` (0x77) je v CZ železniční vagon a v SK autobus**. Právě proto jde autobus
  na `z`, ne na `w`: `w` by tiskl v Česku vagon a na Slovensku autobus. Nová položka mapy se musí
  zkontrolovat v **obou** fontech, jinak se vzhled rozejde podle jazyka.

  ⚠️ **Překlad se váže na KATEGORII, ne na písmeno** (od 4. 8.). Agenda MHD posílá značku jako
  `$c(kód)`, tedy **přímo pozici ve fontu** (`ConvertSpecialniZnak`) - kód 65 dá „A", 75 „K", takže
  plošný překlad podle písmene by jí nakreslil autobus nebo kolo místo jejího symbolu. Sémantické
  kategorie (dnes `prestup`, `odjezd-kola`, `odjezd-mistenka`) drží `Models/NoteCategories.Semanticke`;
  jména kategorií mají jediný zdroj, protože na nich visí i vypínání obsahu v šabloně.
  Výjimka od 23. 10.: **přestup** posílá MHD písmenem (`A`/`L`) s kategorií `prestup`, jako AD - kód
  fontu 120 dal „x", které se tiskne textem jako „na znamení" (`B194a` C3).

  🔧 **Jak pozici ověřit** (Mapa znaků na to nestačí - dvakrát se z ní přečetlo špatně, protože
  ikony jsou drobné a sousedí spolu vagon s autobusem): vytáhnout **obrysy glyfů přímo z fontu** a
  nakreslit kontaktní list s popisky kódů. `fontTools` přečte `cmap` (font je mapovaný v symbolové
  oblasti `0xF020`+, takže znak = kód − `0xF000`; jména glyfů jsou obecná typu `exclam`, sémantiku
  nenesou), obrysy pak stačí vykreslit `PIL`em jako polylinie. Jednorázový skript, do repa nepatří.
  📐 **Doměřeno 24. 8. 2026** (`L4`, znovu vytažením obrysů; **CZ i SK font jsou v těchto pozicích
  identické**). Kontrola metody: na `p` vyšel kufr a na `M` příbor, tedy shodně s dřívějším měřením.

  | Pozice | Co tam doopravdy je | Poznámka |
  |---|---|---|
  | `\|` 0x7C | čtverec (rámeček) | **není svislá čára** - „projíždí" fontem nakreslit nejde |
  | `<` 0x3C | osmiúhelník s vodorovnou čárou | **není „menší než"** |
  | `m` 0x6D | půlkruhový tvar („D") | |
  | `u` 0x75 | prázdný osmiúhelník | |
  | `^` 0x5E | kosočtverec | |
  | `C` 0x43 | písmeno „C" | ne piktogram cla |
  | `e`/`f`/`g` 0x65-67 | číslice **4/5/6 v rámečku** | ⛔ **ne metro** - dřívější domněnka byla mylná |
  | `O` 0x4F | obdélník s šikmou čarou | ⛔ **ne letadlo** (to je na `n`) |

  ⛔ **Tím padají dvě dosavadní domněnky:** „paragrafy `a`/`b`/`c`" a „metro `e`/`f`/`g`". Řada
  `a`-`j` je prostě **číslice 0-9 v rámečku**, jak tvrdí tabulka výš - `e`=4, `f`=5, `g`=6 to potvrzuje.

  ✅ **Návaznosti rozhodnuty tištěným vzorem** (stará sestava linky 330511, 24. 8.): „vyčká" má
  **dutý** trojúhelník △, „navazuje" **plný** ▲, a tisknou se **vedle času**, ne místo něj.
  Který glyf je který, se pozná počtem obrysů: `A` má dva (dutý), `B` jeden (plný) - v CZ i SK
  shodně. Mapa proto vede `m` → `A` a `^` → `B`.
  ⚠️ Cíl se v mapě linky bere **z tabulky**, ne přes vývěskový překlad - ten by „vyčká" (pozice `A`)
  poslal dál na autobus. „Pokračuje" `u` zůstává neověřené, v jádru sady se nevyskytuje.

  ⚠️ **Měření samo říká jen, co v pozici je - ne že ji starý tisk používá.** U „projíždí"/„jede
  jinudy" je odpověď jasná z fontu (znaky tam nejsou), u návazností ji dal až tištěný vzor.

  ⚠️ **Neověřené zůstávají** už jen `G` a `a`/`b`/`c`. `H`/`F` (bezbariér) a kroužkové dny sedí na písmenu.

  📐 **Měření CZ × SK (4. 8. 2026, `B49`)** - obrysy vytažené z obou fontů, ať se to nemusí opakovat:

  | Kód | `TimeTable2.ttf` (CZ) | `TimeTable2SK.ttf` (SK) | Význam |
  |---|---|---|---|
  | `z` 0x7A | autobus | autobus (**identický obrys**) | ✅ shodný |
  | `L` 0x4C | jízdní kolo | jízdní kolo (**identický obrys**) | ✅ shodný |
  | `x` 0x78 | parní lokomotiva | moderní jednotka | ✅ oba **vlak**, jen jiná kresba |
  | `w` 0x77 | železniční vagon | **autobus** | ❌ **jiný význam** |
  | `D` 0x44 | křížek | **dlaň** | ❌ **jiný význam** |

  Závěr: **co dnes překládáme, je pro SK správně** (`A`→`z`, `L`→`x`, `K`→`L`), takže `SymbolGlyph`
  culture parametr nepotřebuje. Ale SK font **není** jen jiné ztvárnění týchž kódů - u `w` a `D`
  se liší význam, takže ověření nové položky mapy musí proběhnout **v obou fontech**.
- **Font `x` = vláček** ověřeno v TimeTable2 (SkiaSharp render u uživatele) → potvrzuje, že na-znamení
  NEsmí jít fontem.

## Návrh implementace v TD (per konvence)

Zavést **jeden zdroj pravdy** - mapu `atribut/anotace → (render-mód, hodnota)`:
- `font-glyf` → Timetable font (locale přes `FontResolver.SymbolFontFamily`),
- `text` → normální font (např. `x`, `MHD`, `u`, `p`, WC-zkratky),
- `nz-box` → stávající ruční inverzní box,
- `kompozit` → glyf + index (později, málo časté).

Konzumují ji `StopListElement` (atributy u zastávky), `DepartureGridElement` (anotace u minut),
`NotesLegendElement` (legenda) - místo dnešního „single-char → symbol font". Katalog výše = obsah mapy.
