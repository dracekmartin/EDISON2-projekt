# R1 — Sdílené tabulkové jádro: návrh (podklad ke schválení před implementací)

> **Datum:** 2026-07-04. Vznikl z analýzy anatomie 4 tabulkových elementů (StopList, DynamicTable,
> LineTable, DepartureGrid) + sdílené `Rendering/` vrstvy. Gate: architektonické rozhodnutí R1.
> **Cíl:** aby další tabulková sestava stála co nejmíň — layout se píše jednou, elementy dodávají
> jen odlišnosti. **Tvrdá podmínka:** behavior-preserving; StopList je 1:1 ověřený proti legacy
> gold oracle, jeho výstup se nesmí změnit.

---

## 0. Klíčový nález analýzy — proč NE „jeden column class pro všechny"

Nabízela by se cesta „sloučit `StopColumn` + `TableColumn` do jedné třídy". **Zamítnuto** z těchto důvodů:

| Rozdíl modelů | StopColumn (StopList, LineTable) | TableColumn (DynamicTable) |
|---|---|---|
| Auto-šířka | `Width` nullable, `null` = auto | `Width` non-null, `<=0` = auto |
| Styl | jeden `ColumnVisualStyle?` (Header+Data spolu) | dva `TextStyle?` (HeaderStyle/DataStyle) |
| Binding | `Key` | `Field` + `Format` |
| Šířka | jen mm | `WidthMode` Mm/Percent |
| Výška řádku | z fontu (nullable `DataRowHeight`) | pevná mm (`RowHeight`) |

- Sloučení mění **JSON serializaci obou** → riziko pro gold-ověřené šablony (StopList) i pro
  DynamicTable (má testy). Vysoký churn, nutná migrace obou.
- Každý konzument by nesl pole, která nepoužívá (StopList by dostal `Format`/`WidthMode`, DynamicTable `Key`).
- **R1 nechce sjednotit datovou třídu — chce napsat layout ALGORITMUS jednou.** `ColumnRenderer.DrawRow`
  + `CellSpec` už jsou neutrální per-řádková abstrakce; chybí jen orchestrace NAD ní (šířky, kaskáda,
  výška, header+rows smyčka), která je dnes 3× zkopírovaná.

**Rozhodnutí:** extrahovat **sdílené algoritmy/helpery**, ne datovou třídu. Každý element si nechá
svůj column model i serializaci (**nulové riziko pro šablony**) a do jádra jen promítne neutrální tvar.

---

## 1. Co zůstává beze změny

- **`Rendering/CellRenderer.DrawCell`** (bg → padding → HAlign/VAlign → decorations) — kanonická buňka, hotová.
- **`Rendering/ColumnRenderer.DrawRow` + `CellSpec`** — neutrální řádková orchestrace, hotová.
- **`CellDefaults.Default`, `ColumnHeader.Resolve`, `CellStyleEditOptions`** — hotová sdílená vrstva.
- **`StopColumn`, `TableColumn`** — modely + jejich JSON zůstávají. Žádná migrace šablon.
- **`DepartureGridElement`** — **plně mimo jádro** (je to pivot hodina × skupina dnů, ne řádková tabulka;
  jeho render se do „řádek = sekvence CellSpec" nevejde). Sdílí už dnes jen datovou/nastavovací vrstvu
  (column model `DepartureGroupColumnStyle`, kaskádu stylů, editory) — potvrzeno analýzou, v souladu s roadmapou.

---

## 2. Co se extrahuje — 3+1 konkrétní kusy (nové `Rendering/TableLayout.cs`)

### (A) Jednotný výpočet šířek — `TableLayout.ResolveWidths`
Nahrazuje `StopListElement.ResolveColumnWidths` + `DynamicTableElement.ComputeColumnWidths` (dnes 2 kopie
téhož vzorce). Vstup = neutrální popis sloupce (`Width?` + `WidthMode`) + šířka elementu, výstup = pole mm.

```csharp
// neutrální vstupní tvar (element promítne svůj sloupec)
public readonly record struct ColWidth(float? WidthMm, ColumnWidthMode Mode = ColumnWidthMode.Mm);

public static float[] ResolveWidths(IReadOnlyList<ColWidth> cols, float elementWidthMm);
// pevné (WidthMm != null) honorovány (Percent přepočítá); auto (null) sdílí rovným dílem zbytek.
```
Sjednocuje dvě sémantiky auto (`null` u StopColumn, `<=0` u TableColumn) na jednu (`null`) — na hranici
jádra, ne v modelech. DynamicTable si `<=0` převede na `null` v projekci.

### (B) Jednotná kaskáda stylů — `TableLayout.ResolveCascade`
Nahrazuje `ResolveHeaderStyle/ResolveDataStyle` (3× tentýž vzorec: column-override → element-parent →
legacy Alignment fallback).

```csharp
public static ResolvedTextStyle ResolveCascade(
    TextStyle? columnLayer,            // col.Style?.Header  /  col.HeaderStyle
    ResolvedTextStyle elementParent,   // element už resolved nad baked defaults
    TextHorizontalAlignment legacyAlignment,
    bool elementDefinesHAlign);        // aby fallback nepřebil element HAlign
```
Funguje pro oba modely — StopColumn dodá `Style?.Header`, TableColumn `HeaderStyle`.

### (C) R1a — DynamicTable na `ColumnRenderer.DrawRow`
DynamicTable dnes kreslí řádky vlastní per-buňka smyčkou (`RenderHeader`/`RenderDynamicRows`/
`RenderSummaryRows` volají `CellRenderer.DrawCell` přímo). Přejít na `ColumnRenderer.DrawRow` +
`CellSpec` (StopList/LineTable to už dělají) → jedna cesta kreslení řádku. Behavior-preserving
(DynamicTable je čerstvá + má testy).

### (D) Volitelně: `TableLayout.DrawHeaderAndBody` — orchestrátor pro „prosté" tabulky
Společný skelet „header bg + DrawRow(header) + čára pod záhlavím + [pro každý řádek: alt bg → DrawRow] +
separátory dle BorderStyle" pro **DynamicTable + LineTable** (jednoduché řádkové tabulky). Přijme
resolved šířky + header CellSpecs + provider řádkových CellSpecs + BorderStyle + scale.

**StopList orchestrátor NEpoužije** — má do smyčky vpletené příliš mnoho specifik (šipka směru,
markers sub-cell split, current/passed zvýraznění přes celou šířku, filtr projetých). StopList zůstane
u vlastní render smyčky, ale postavené na (A) `ResolveWidths` + (B) `ResolveCascade`. Tím se odmaže
duplikace beze změny jeho chování.

---

## 3. Co zůstává na elementech (jejich „odlišnosti")

- **StopList:** šipka směru jízdy, markers sub-cell (BeforeName), current/passed zvýraznění,
  filtr projetých zastávek, variantní časové sloupce (`ExpandTravelTimeColumns`).
- **DynamicTable:** summary rows + agregace, alternating row color, Fixed mód s `{PLACEHOLDER}` (dormant TableElement).
- **LineTable:** pás skupin dnů, řádek platnosti/NZ, přípojné řádky, Km auto-expand, MHD auto-remove.
- **Všechny:** vlastní `GetVisibleColumns`, mapování klíč→hodnota buňky, per-row overridy (patří do
  render-policy elementu, ne do sdíleného modelu).

---

## 4. Inkrementální plán (behavior-preserving, po každém kroku build + render diff)

1. **`Rendering/TableLayout.cs`** — `ResolveWidths` + `ResolveCascade` (+ `ColWidth`, přesun `ColumnWidthMode`).
2. **DynamicTable** → `ResolveWidths` + `ResolveCascade` + (C) `DrawRow` + (D) orchestrátor. Má testy → nejbezpečnější první. Ověření: build + render vzorové tabulky + `dotnet test`.
3. **LineTable** → `ResolveWidths` + `ResolveCascade` + (D) orchestrátor. Ověření: harness render AD (0320680) + MHD (CB_DP 1001) — musí být identické.
4. **StopList** → `ResolveWidths` + `ResolveCascade` (bez orchestrátoru, vlastní smyčka). **Nejcitlivější** —
   1:1 gold. Ověření: `StopListCascadeTests` + render zastávkové vývěsky před/po (vizuální diff),
   ideálně proti gold oracle na známé lince.
5. **DepartureGrid** — netknuto (jen ověřit, že build prošel).

Každý krok samostatný commit. Když render diff u kroku 4 najde odchylku → revert kroku, ne oprava naslepo.

---

## 5. Výsledek

- Vzorec výpočtu šířek: **2 kopie → 1**. Kaskáda stylů: **3 kopie → 1**. Řádkové kreslení: **1 cesta**
  (`DrawRow`) místo 3 ručních smyček. Prostý tabulkový skelet: **1 orchestrátor** pro nové elementy.
- Pátá tabulková sestava = column model + „co je v buňce" + její odlišnosti; layout dostane zdarma.
- Žádná změna JSON šablon, žádná změna chování ověřených elementů.
- Vyšší patro (partial-fit „vešlo se 40 řádků, zbývá 12" = R2, bin-packing) staví později NAD tímto jádrem.
