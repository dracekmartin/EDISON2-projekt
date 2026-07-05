# R2 — Partial-fit kontrakt (element ↔ stránkovací engine): ROZHODNUTO

> **Datum:** 2026-07-05. Rozhoduje rozhraní mezi tabulkovým elementem a `PageLayoutEngine` pro
> stránkování dlouhého obsahu přes stránky. Konkrétní konzument: knižní/linkový JŘ (dlouhý seznam
> zastávek) + dlouhé faktury/reporty. **Tento dokument rozhoduje KONTRAKT, ne implementaci** —
> implementace patří k pagination/bin-packing tracku (viz §6). Gate: architektonické rozhodnutí R2.

## 0. Model (potvrzeno)

- **Element** rozhoduje jen lokálně o svém vnitřku (kolik řádků, jak vysoké, kde je nedělitelná
  jednotka) a **hlásí nahoru**: „umím vykreslit řádky `start`..`end`, tady je jejich výška".
- **Globální vrstva** (`PageLayoutEngine`) skládá sekce/stránky: ptá se elementu „kolik se vejde do
  zbývajícího místa", umístí okno, na další stránce pokračuje od dalšího řádku.

## 1. Jak se dělí DNES (a proč to nestačí)

`PageLayoutEngine` (ř. 287-336): u `SplitAcrossPages`/`AllowSplit` spočítá **celkovou výšku sekce**
a nakrájí ji na mm-pásy (`chunk`) dle zbytku místa; každý pás = `PlacedSection` s `ClipTop`/`ClipBottom`.
Renderer nakreslí celou sekci a **ořízne ji na ten mm-pás**. → Dělení je **pixelové** — uřízne řádek
i řádek textu vejpůl na hraně stránky, záhlaví se neopakuje. Pro spojitý text OK, pro tabulku špatně.

## 2. Dvě osy stránkování — R2 řeší JEN svislou

| Osa | Problém | Kde | Tento kontrakt |
|---|---|---|---|
| **Svislá (řádky)** | dlouhý seznam řádků/zastávek se nevejde na výšku | dlouhá faktura, knižní JŘ, StopList | ✅ **R2 (tady)** |
| **Vodorovná (sloupce)** | moc sloupců (159 spojů) se nevejde na šířku → krájet bloky sloupců + opakovat popisové | knižní/linkový JŘ (`Strih`) | ❌ samostatné, **C3 bin-packing** |

Column-axis (Strih) je jiný problém (opakuj fixní sloupce + porce datových) a řeší se vlastním
kontraktem v C3. R2 se ho nedotýká.

## 3. Kontrakt — rozhraní (rozhodnuto)

Volitelná schopnost na řádkových tabulkových elementech (StopList/DynamicTable/LineTable):

```csharp
/// Element, který umí být stránkován po řádcích (svislá osa). Engine ho řídí místo pixel-ořezu.
public interface IRowPaginatedElement
{
    /// Počet nedělitelných řádkových jednotek. „Jednotka" definuje element:
    ///   StopList = 1 zastávka; DynamicTable = 1 datový řádek; LineTable = zastávka + její
    ///   vložené přípojné podřádky (drží pohromadě, nikdy se nerozdělí).
    int RowUnitCount(DataContext? data);

    /// Výška opakovaného záhlaví v mm (nadpisy sloupců + případný pás skupin dnů).
    /// 0 = element záhlaví neopakuje.
    float HeaderBandHeightMm(DataContext? data);

    /// Výška jedné řádkové jednotky v mm (element ji už umí — z fontu / pevná).
    float RowUnitHeightMm(DataContext? data, int unitIndex);

    /// Vykreslí okno jednotek [startUnit, endUnitExclusive). drawHeader = má-li se navrch
    /// zopakovat záhlaví (na každé pokračovací stránce true).
    void RenderRowWindow(RenderContext ctx, int startUnit, int endUnitExclusive, bool drawHeader);
}
```

Měřicí metody vracejí **mm** (žádný SkiaSharp) → engine zůstává čistá logika (jako dnes
`IElementHeightCalculator`).

## 4. Jak to engine řídí (rozhodnuto)

Pro sekci s `OverflowBehavior = SplitAcrossPages`, jejíž obsah je **právě jeden** `IRowPaginatedElement`
vyplňující sekci (typický dlouhý JŘ / faktura):

1. `header = HeaderBandHeightMm`; greedy plň jednotky od `startUnit`, dokud se `header + Σ výšek` vejde
   do `spaceOnPage`. Alespoň jedna jednotka na stránku (i když přeteče — jinak nekonečná smyčka).
2. Umísti `PlacedSection` s novými poli `StartRowUnit`/`EndRowUnit`/`RepeatHeader` (místo `ClipTop`/`ClipBottom`).
3. Zbývají-li jednotky → nová stránka, `startUnit = end`, `RepeatHeader = true`, opakuj.
4. Renderer u takové `PlacedSection` volá `RenderRowWindow(start, end, RepeatHeader)` místo pixel-ořezu.

**Fallback:** sekce, která NENÍ jeden řádkový element (víc elementů, spojitý text…) → **beze změny**,
dnešní pixel-ořez (`ClipTop`/`ClipBottom`). Kontrakt je aditivní, nic nerozbíjí.

## 5. Rozhodnuté detaily

- **Nedělitelná jednotka = volba elementu.** Knižní JŘ: zastávka + její přípojné podřádky = 1 jednotka
  (nikdy se nerozdělí přes stránku). Engine dovnitř jednotky nezasahuje.
- **Záhlaví se opakuje** na každé pokračovací stránce (`RepeatHeader`). Zap/vyp = property elementu
  (default zap pro tabulky). Pás skupin dnů (LineTable) je součást header-bandu.
- **Souhrnné řádky** (DynamicTable summary) = trailing jednotky; drží se u konce, engine je nechá
  „přetéct" s posledním blokem (neosiří na samostatné stránce bez dat — v1 stačí je počítat jako
  běžné jednotky na konci).
- **Element bez `IRowPaginatedElement`** = dnešní chování (pixel-ořez). Migrace elementů je postupná.

## 6. Implementace — kdy

**Kontrakt je rozhodnut; implementace NEteď.** Důvody:
- Pro knižní JŘ je palčivější **vodorovná** osa (C3 bin-packing), ne svislá — sám row-pagination mu
  moc nepomůže, dokud se neřeší sloupce.
- Nejsilnější konzument svislé osy je **dlouhá faktura/report** (Fáze B/F).
- Implementace = rozšíření `PageLayoutEngine` (nová větev split) + `LayoutResult` (řádková okna místo
  clip) + `RenderRowWindow` na 3 elementech + testy. Patří k paginačnímu/bin-packing tracku, ať se
  svislá i vodorovná osa navrhnou a otestují spolu na knižním JŘ.

Do té doby: dnešní pixel-ořez zůstává funkční fallback. Kontrakt výše je závazný pro tu implementaci.
