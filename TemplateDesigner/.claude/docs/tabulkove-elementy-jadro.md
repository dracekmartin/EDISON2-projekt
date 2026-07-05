# Tabulkové elementy — sdílené jádro vs element-specifické (architektura)

> **Kdy číst:** před přidáním nového tabulkového elementu, před úpravou render/layout logiky
> některé tabulky, nebo když se rozhoduje „mám tohle vytáhnout do jádra?". Evergreen referenční
> dokument (stav R1 hotový 2026-07-04). Návrh/rozhodovací proces: `r1-sdilene-tabulkove-jadro-navrh.md`.

## 0. Základní princip: dvě patra, konzistence žije na obou různě

Každý tabulkový element má **dvě patra**, která se sjednocují nezávisle:

| Patro | Co to je | Konzistence |
|---|---|---|
| **Data / nastavení** | model sloupců, kaskáda stylů, editory, 2-tab dialog | **sdílené VŠEMI** tabulkovými elementy (i pivotem DepartureGrid) |
| **Render / layout** | jak se to skládá na plátno (šířky, řádková smyčka) | sdílené jen mezi **řádkovými** tabulkami; pivot je mimo |

Klíč: „konzistentní" ≠ „stejný render". Element může sdílet vše, co uživatel edituje (a být tak
plně konzistentní z jeho pohledu), a přitom mít vlastní render, když je jeho tvar jiný.

---

## 1. Co je SDÍLENÉ — používej, neduplikuj

Vše v `dotnet/Rendering/`:

| Prvek | Co dělá | Používá |
|---|---|---|
| `CellRenderer.DrawCell` | jedna buňka: bg → padding → HAlign/VAlign → decorations. **Jediné místo pozicování textu buňky.** | všechny 4 (i DepartureGrid na hodinové buňky) |
| `ColumnRenderer.DrawRow` + `CellSpec` | jeden vodorovný řádek = sekvence buněk + volitelné svislé čáry | StopList, LineTable, DynamicTable (R1a) |
| `TableLayout.ResolveWidths` | šířky: pevné (mm/%) + auto-split zbytku rovným dílem | StopList, DynamicTable |
| `TableLayout.ResolveCascade` | kaskáda stylu buňky `column → element → baked` + legacy alignment fallback | StopList, DynamicTable, LineTable |
| `CellDefaults.Default` | poslední vrstva kaskády (Arial 9pt, padding 0.5mm, VAlign Middle…) | všechny |
| `ColumnHeader.Resolve` | sémantika „override přebíjí zdroj, prázdné → zdroj" u nadpisů | všechny |
| `Models/Expressions/RowConditions` | řádková podmínka: výraz nad daty řádku → viditelnost / barva zvýraznění pozadí (fail-open) | DynamicTable (vis+highlight), StopList + LineTable (highlight); DepartureGrid mimo = pivot |

**Editory (UI vrstva, `dotnet/Controls/` + `Windows/*EditorDialog/`):**

| Prvek | Co dělá | Používá |
|---|---|---|
| `ColumnListEditor` + `IColumnRowVM` | levý seznam sloupců (checkbox viditelnost, override tečka, ▲▼, šířka) | StopList, DepartureGrid, LineTable |
| `CellStyleEditor` + `CellStyleVM` | tri-state editor stylu buňky (font/barvy/align/padding, „×" reset) | všechny dialogy sloupců |
| **2-tab dialog** (Obecné + Sloupce) | Tab 1 = EDInputForm (`*ElementEdit.cls`, element-wide props), Tab 2 = `ColumnListEditor` + `CellStyleEditor` | StopColumnsEditorDialog, DepartureGroupEditorDialog, DynamicTableEditorDialog, LineTableColumnsEditorDialog |

**2-tab dialog = závazný vzor** pro každý tabulkový element s editovatelnou kolekcí sloupců. Nový
element ho postaví jako klon některého existujícího (nejblíž `LineTableColumnsEditorDialog`), NIKDY
neduplikuje inline XAML pro editaci stylu buňky.

---

## 2. Co je ZÁMĚRNĚ element-specifické — a proč

Tohle NEpatří do jádra; každý element si drží sám, protože je to jeho doménová odlišnost:

**StopList** (zastávková vývěska):
- Šipka směru jízdy (gutter, bracket, hlavice), markers sub-cell (znaky před názvem), current/passed
  zvýraznění (per-řádkové overridy), filtr projížděných zastávek, variantní časové sloupce (Km-expand).

**DynamicTable** (obecná tabulka):
- Souhrnné řádky s agregacemi (SUM/COUNT/AVG…), alternating row color, Fixed mód s `{PLACEHOLDER}`
  ze skalárů (dormant `TableElement`).

**LineTable** (knižní JŘ):
- **Výpočet šířek** (Km-expanze na N sub-sloupců dle dat + časové sloupce spojů sdílejí auto pool —
  není to obecný per-sloupec vzorec, proto NEpoužívá `ResolveWidths`), pás skupin dnů, řádek platnosti/NZ,
  přípojné řádky (vyčká/navazuje), MHD auto-remove.

**DepartureGrid** (minutová mřížka) — **celý render mimo jádro**:
- Je to **pivot** (2D matice hodina × skupina dnů, jedna buňka = N odjezdů), ne řádková tabulka.
  `ColumnRenderer.DrawRow` („řádek = sekvence sloupců") jeho tvar neumí vyjádřit. Vlastní: pivot layout,
  redistribuce šířek, word-wrap víceřádkových záhlaví, HourColumnPerGroup, NZ invertované minuty,
  extended hours přes půlnoc. **Sdílí ale plně datovou vrstvu** (column model `DepartureGroupColumnStyle`,
  kaskádu, `CellStyleEditor`/`ColumnListEditor`, `CellRenderer.DrawCell` na hodinové buňky) → z pohledu
  uživatele je konzistentní.

---

## 3. Nový tabulkový element — co dostaneš zdarma, co musíš dodat

**Zdarma z jádra:** kreslení buňky, kreslení řádku, výpočet šířek (pokud tvůj model je „per-sloupec"),
kaskáda stylů, editor sloupců + styl editor, 2-tab dialog (klon).

**Musíš dodat (tvé odlišnosti):** column model (nebo reuse `StopColumn`/`TableColumn`), mapování
„co je v buňce" (klíč → hodnota z dat), `Render()` orchestraci (header + řádky přes `DrawRow`),
`CalculateHeightFromData`, per-row overridy pokud máš (zvýraznění apod.), a EDInputForm schéma
(`*ElementEdit.cls`) pro tab Obecné.

**Rozhodni:** je to řádková tabulka (iteruješ záznamy → řádky) → do řádkového jádra. Nebo pivot /
jiný tvar → vlastní render, ale sdílej datovou vrstvu (jako DepartureGrid).

---

## 4. Co vědomě NEsjednocovat (a proč) — nevracet se k tomu bez nového důvodu

| Nesjednocovat | Proč |
|---|---|
| **`StopColumn` + `TableColumn` do jedné třídy** | Různá serializace (riziko pro gold-ověřené šablony StopListu), různé potřeby (StopColumn = `Key` + `ColumnVisualStyle` + mm; TableColumn = `Field`/`Format` + dva `TextStyle` + mm/%). Každý konzument by nesl pole, co nepoužívá. R1 sjednotil ALGORITMY, ne datovou třídu. |
| **DepartureGrid render** | Pivot ≠ řádková tabulka (viz §2). Vtlačení by jádro zbytnělo podmínkami „když pivot" nebo by vyžadovalo rizikový přepis bez zisku. |
| **LineTable výpočet šířek** | Km-expanze + trip-pool = legitimní odlišnost, ne obecný vzorec. Kaskádu sdílí, šířky ne. |

---

## 5. Kandidáti na budoucí extrakci (triggery — udělat, až nastane důvod)

| Kandidát | Trigger | Poznámka |
|---|---|---|
| **R1a — render-loop orchestrátor** (header + řádky + separátory jako 1 metoda) | šestá tabulka, nebo ladění separátorové logiky na jednom místě | DynamicTable dnes kreslí vlastní per-buňka smyčkou místo `ColumnRenderer.DrawRow`; ~20 řádků skeletu, nízká hodnota / churn na render. |
| **Výška-z-fontu vs pevná mm** | další element s font-driven výškou | dnes StopList/LineTable počítají výšku z fontu, DynamicTable má pevné `RowHeight` mm. Sjednotit jako `RowHeight?` (null = z fontu) pokrylo by oba. |
| **Column model sjednocení** | jen kdyby vznikl konkrétní tlak + bezpečná migrace serializace | viz §4 — dnes vědomě NE. |

Pravidlo: extrahovat **až při 2+ konzumentech** stejné potřeby (ne dopředu). R1 se extrahoval právě
proto, že vznikl 3. konzument (LineTable) — do té doby by přepis gold-ověřeného StopListu nesl jen riziko.
