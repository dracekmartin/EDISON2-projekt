# Test plán — specializované elementy zastávkového JŘ

> **Účel:** podrobný plán testování tří specializovaných elementů (`StopListElement`,
> `DepartureGridElement`, `NotesLegendElement`) — co znamenají, co umí zobrazit, a jak je cíleně
> otestovat s různými nastaveními na linkách, které mají konkrétní featury.
>
> **Datum:** 2026-06-23 · Sestaveno z analýzy zdrojů elementů + Caché agend + `cache-database-linky.md`.

---

## 0. Mentální model — jak elementy spolupracují

Zastávkový JŘ MHD = tři elementy vedle sebe / pod sebou, řádky se vodorovně zarovnávají
(jedna zastávka = jeden řádek):

```
┌─ StopListElement ──────┬─ DepartureGridElement ──────────────┐
│ čas │ zastávka      │  │  Prac. den │ Sobota │ Neděle        │  ← skupiny dnů = sloupce
│  0  │ ►Nádraží  ×   │  │ 5 │ 15 30  │  …     │  …            │  ← hodiny = řádky, minuty + anotace
│  3  │  Náměstí      │  │ 6 │ 00 22ᴬ │        │               │
│ ... │  ...          │  │...│        │        │               │
└─────────────────────┴──────────────────────────────────────┘
┌─ NotesLegendElement ───────────────────────────────────────┐
│ Poznámky: A neobsluhuje zast. X   ⊘ jede jen v prac. dny    │  ← legenda značek
└────────────────────────────────────────────────────────────┘
```

- **Data tečou z Caché** (`GetData`/`GetMockData`) jako `DataContext` s kolekcemi: `ZASTAVKY`,
  `SKUPINY_DNU`, `ODJEZDY`, `POZNAMKY`. Každý element čte svoji kolekci.
- **Multi-page:** `GetData` vrací N stránek = zastávka × směr. Live preview ukazuje stránky aktuální
  instance; PDF/PNG export projde všechny.
- **Jeden DataContext = jedna zastávka×směr** (typicky 1 A4).

### Která kolekce řídí který element

| Element | Čte kolekci | Nečte |
|---|---|---|
| StopList | `ZASTAVKY` (vč. vnořených `ZONY[]`, `PRUBEHY[]`, `ATRIBUTY[]`, `POZNAMKY_ZASTAVKY[]`) | SKUPINY_DNU, ODJEZDY, POZNAMKY |
| DepartureGrid | `SKUPINY_DNU` + `ODJEZDY` (vč. `ANOTACE[]`) | ZASTAVKY, POZNAMKY |
| NotesLegend | `POZNAMKY` (sloučené Caché: pozn. zastávky → stránkové → atributy) | ostatní |

---

## 1. Dostupnost dat — co lze testovat HNED

⚠️ **`^JRL` je per-namespace, NE per-IdFa.** TD vidí jen linky firmy z `templatedesigner.appsettings.json`
(aktuálně **`CB_GWBUS`**). Ostatní firmy = přepnout `Namespace`+`Idfa` + restart.

| Firma | Linky | Stav |
|---|---|---|
| **CB_GWBUS** (teď) | 0320113, 0320144, 0320993 | ✅ hned |
| KV_DP | 0410101, 0425017 | přepnout namespace |
| CB_CSADAD | 0340303, 0345061, 0365001, 0365004, 0365005 | přepnout namespace |
| VS_CSAD | 0945001, 0445102 | přepnout namespace |

> **Tip:** pro porovnání proti legacy gold oracle není přepnutí TD nutné — `cache_query
> ALVA.TISKMHD.Data/LoadData` s `VOLANO_Z~JRMAIN` a `dataNamespace=<firma>` funguje cross-namespace.
> Přepnutí TD je potřeba jen pro **živý náhled v designeru**.

---

## 2. Test matice: feature × linka

Legenda: ⭐ primární gold · ✓ taky pokrývá · — nemá

| Feature ↓ \ Linka → | 0320113 | 0320144 | 0320993 | 0410101 | 0425017 | 0340303 | 0345061 | 0365001 | 0365004 | 0365005 |
|---|:--:|:--:|:--:|:--:|:--:|:--:|:--:|:--:|:--:|:--:|
| FTL 0 (3 sl.) | ✓ | — | — | ⭐ | — | ✓ | — | ✓ | ✓ | ✓ |
| FTL 1 (prázdn. popisky) | — | — | — | — | ⭐ | — | — | — | — | — |
| FTL 2 (2 sl.) | — | — | ⭐ | — | — | — | ✓ | — | — | — |
| FTL 3 (4 sl.) | — | ⭐ | — | — | — | — | — | — | — | — |
| Klasický sloupcový (no grid) | ⭐ | — | — | — | — | — | — | — | — | — |
| Rozdíly tras (A/B písmena) | — | — | — | — | — | — | ✓ | ✓ | — | ⭐ 6 variant |
| Bezbariér H/F (per spoj) | — | — | — | — | — | — | — | ⭐ | ✓ | ✓ |
| Bezbariér agregát + filtr | — | — | — | — | — | — | — | — | ⭐ | — |
| Datumová omezení (jede/nejede) | ✓ | — | — | ⭐ | — | — | — | ✓ | — | — |
| Atributy zastávek „na znamení" (x) | — | ⭐ | — | ✓ | — | — | — | — | — | — |
| Časy jízdy StopList (1 sl.) | — | — | — | ✓ | — | ⭐ uzly | ✓ | ✓ | — | — |
| Multi-varianta TrasaMin ⬜ (nehotové) | — | — | — | — | — | — | ⭐ 8 | ✓ 2 | — | ✓ |
| Dopravce s adresou | — | — | — | ✓ | — | — | — | ⭐ | — | — |
| Multi-page (zastávka×směr) | ✓ | ✓ | ✓ | ⭐ 35 str. | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

> **Multi-varianta TrasaMin** (víc časových sloupců) je jediná nehotová featura (chce zásah do
> rendereru) — gold 0345061 (8 variant). Dnešní 1sloupcová verze je u variantních linek nepřesná.
> **NZ prázdninové dělení** je dormant ve všech firmách — nelze testovat na reálných datech.

---

## 3. StopListElement — seznam zastávek

**Význam:** levý blok JŘ — zastávky shora dolů (čas jízdy, TČ, název, zóny, průběhy, ZT, značky).

### Co umí zobrazit / klíčové properties k testu

| Oblast | Property | Co testovat |
|---|---|---|
| **Sloupce** | `Columns` (Key/Header/Width/Visible/Alignment/Style) | Viditelnost, pořadí, pevné vs auto šířky. Klíče: `TravelTime`, `Tc`, `StopName`, `Zone`, `RouteLetters`, `BaseRoute`. **Edge:** Σ pevných > Width → auto sloupce dostanou 0, ořez clipem |
| **Aktuální zastávka** | `CurrentStopBold/Underline/FontSize`, `CurrentStopBackgroundColor` | Řídí `JE_AKTUALNI` v datech. Highlight + start šipky. **Edge:** žádná aktuální → v default režimu se nevykreslí NIC |
| **Projeté zastávky** | `ShowPassedStops`, `PassedStopsForeColor` | False = skryje zastávky před aktuální; true = šedě. Override `TISK_PROJETYCH_ZAST` |
| **Šipka směru** | `ShowDirectionArrow`, `DirectionArrowPosition` (Left/Right), barva, bold | Left = mimo sloupec (CBDP); Right = uvnitř (DPKV). Override `TISK_SIPKY` |
| **Značky zastávky** | `ShowSignalStopIndicator`+`SignalStopSymbol`, `ShowStopAttributes`, `ShowStopNotes`, `MarkersPosition` (AfterName/BeforeName), `ExcludedAttributeIds` | „na znamení" (×), atributy, poznámky. BeforeName = samostatná sub-cell před názvem |
| **Styl** | `HeaderStyle`/`DataStyle` (4 base barvy v panelu + dialog sloupců), `GridColor`, `ShowRowSeparators`, `ShowHeader` | Kaskáda sloupec→element→defaults |
| **Výška** | `Height` null vs pevná, `DataRowHeight` | null = dynamická; pevná → auto-scale fontu |

### Jak testovat
- **FTL/struktura:** 0320144 (FTL3), 0320993 (FTL2) — hned. Ověř názvy, pořadí, terminus-skip.
- **Atributy „na znamení":** **0320144** — zastávky s × značkou, ověř `ShowStopAttributes` + filtr.
- **Časy jízdy:** 0340303 (uzly, formát příjezd/čekání) — KV/CSADAD.
- **Aktuální + projetí + šipka:** kterákoli; přepínej `ShowPassedStops`, `DirectionArrowPosition`,
  zvýraznění; sleduj live preview.
- **Sloupce:** zapni/vypni `Tc`/`Zone`/`RouteLetters`/`BaseRoute`, měň šířky; ověř clip a auto-fit.

---

## 4. DepartureGridElement — mřížka minut odjezdů

**Význam:** řádky = hodiny, sloupce = skupiny dnů (typy provozu), buňka = minuty + anotace.

### Co umí zobrazit / klíčové properties k testu

| Oblast | Property | Co testovat |
|---|---|---|
| **Skupiny dnů (sloupce)** | `GroupColumns` (IsVisible/pořadí/Style/HeaderOverride/WidthOverride/AlternatingRowColor), `SkupinaDnuId`, `MaxGroupCount` | FTL módy mapují počet sloupců. Per-group zebra, override nadpisu, pin šířky |
| **Záhlaví skupin** | `ShowSkupinaDnuHeader`, `SkupinaDnuHeaderLineHeight`, `HeaderVerticalAlignment`, `HeaderLine2Style`, „Velikost záhlaví" | 2řádkový nadpis (Nadpis/Nadpis2 = „Prac./den"). Typografie záhlaví je element-uniformní |
| **Sloupec hodin** | `HourColumnWidth`, `HourColumnPerGroup`, `HourColumnStyle`, `HourColumnHeader` | PerGroup **rozdvojuje** render (vysoké riziko) |
| **Minuty** | `MinuteWidth`, `MinuteStyle`/`MinuteFontSize`/`MinuteBold` (scalar fallbacky), `OdjezdGap`, `GroupSeparatorWidth` | Pozor anti-pattern: scalar fallback jen když MinuteStyle i DataStyle null |
| **Anotace** | `ShowAnotace`, `AnotaceSymbolStyle`/`AnotaceTextStyle`/`AnotaceFontSize`, `AnotaceGap`/`AnotaceInterGap`, **`InvertovatNegativniZnacky`** + `NegativniZnacka{Back,Fore}Color` | Typy: text (a/A, dny), specialni (F/H/×), **NZ invertovaně** (černý box, default zap — MHD má, AD ne). NZ otestuj na **MHD agendě** (mock „22") |
| **Časová osa** | `TimeAxisMode` (Compact/DataBounded/Range/Full), `TimeAxisRangeFrom/To`, override `CASOVA_OSA`/`HODINY_OD`/`HODINY_DO` | Range wrap přes půlnoc (to<from → extended 24..47). |
| **Výška/roztažení** | `Height` null vs pevná, `AutoExpandToFillHeight` (strop 1.5×), override `ROZTAHNOUT_NA_VYSKU`, `DataRowHeight`, `AlternatingRowColor` | Shrink auto-scale vs grow (dataScale) |
| **Barvy** | per-odjezd `FORE_COLOR` (z dat), zebra | ⚠️ `ColorRules` je **mrtvý kód** — barvení jede přes data, ne pravidla |

### Jak testovat
- **FTL sloupce:** 0320144 (4 sl. FTL3), 0320993 (2 sl. FTL2) — hned; 0410101 (3 sl. FTL0),
  0425017 (FTL1 prázdninové popisky) po přepnutí na KV_DP.
- **NZ invertovaně:** **přepni agendu na MHD** (Ctrl+Shift+A) → mock má NZ „22" v 6:45 → černý box.
  V AD se NZ u minut nezobrazuje (CNZ jen klasifikuje sloupce) — to je správně.
- **Anotace rozdílů tras (A/B):** 0365005 (6 variant), 0365001 — CB_CSADAD.
- **Bezbariér H/F u spoje:** 0365001 — anotace „H"/„F".
- **Časová osa:** přepínej `TimeAxisMode`, zkus Range s wrapem (22→5); sleduj sadu hodin.
- **HourColumnPerGroup, roztažení, zebra:** přepínej a sleduj live preview.

---

## 5. NotesLegendElement — legenda poznámek

**Význam:** vysvětlivky značek pod mřížkou — NZ (invertované), speciální znaky, textové poznámky.

### Co umí zobrazit / klíčové properties k testu

| Oblast | Property | Co testovat |
|---|---|---|
| **Filtrování typů** | `ShowNegativniZnacky`, `ShowSpecialniZnaky`, `ShowTextovePoznamky` | Vypni/zapni; sleduj, co z legendy zmizí |
| **Rozložení** | `Columns` (1–4), `WrapSmer` (PoSloupcich/PoRadcich), `MarkerSirka` (0=inline / >0=zarovnaný sloupec), `Razeni` (Vychozi/DleDelky↑↓) | 16 kombinací Columns×Wrap×Marker |
| **NZ marker** | `NegativniZnackaBackColor`/`ForeColor` | Invertovaný obdélník + znak (default černá/bílá) |
| **Text** | `HeaderText`, `NoteSeparator`, `NoteFontSize`, `NoteLineHeight` | Nadpis, oddělovač znak↔text |
| **Styl** | `HeaderStyle` (font/bg/text), `DataStyle.FontFamily`, `GridColor` | Záhlaví vždy Bold |
| **Výška** | `Height` null vs pevná | Pevná → auto-scale |

### Jak testovat
- **Tři typy obsahu:** 0320144 (atributy „na znamení" → legenda), 0365001/0365005 (rozdíly tras
  A/B → textové poznámky + bezbariér H/F speciální znaky).
- **Filtr:** přepínej `Show*` flagy, ověř že odpovídající řádky zmizí.
- **Vícesloupcové:** nastav `Columns=2..4`, přepínej `WrapSmer`, `MarkerSirka`, `Razeni`.
- **NZ marker:** na MHD agendě (legenda NZ je invertovaná i v AD? — NZ legenda jede z `POZNAMKY`;
  ověř, jaké NZ poznámky daná linka vrací).

---

## 6. Integrační nastavení (tiskové parametry → override properties)

Tyto parametry z tiskového dialogu (DataContext skaláry) **přebíjejí** properties elementu — testovat
že override funguje:

| Parametr | Přebíjí | Element |
|---|---|---|
| `TISK_PROJETYCH_ZAST` | `ShowPassedStops` | StopList |
| `TISK_SIPKY` | `ShowDirectionArrow` | StopList |
| `CASOVA_OSA` | `TimeAxisMode` | DepartureGrid |
| `HODINY_OD` / `HODINY_DO` | rozsah hodin | DepartureGrid |
| `ROZTAHNOUT_NA_VYSKU` | `AutoExpandToFillHeight` | DepartureGrid |
| `SCHOVAT_AGREGAT_BEZBARIER` | filtr agregátu | (0365004) |

---

## 7. Doporučená testovací sekvence

**Fáze A — CB_GWBUS (hned, bez přepnutí):**
1. `0320144` (FTL3) — StopList atributy „na znamení", DepartureGrid 4 sloupce, NotesLegend filtr.
2. `0320993` (FTL2) — DepartureGrid 2 sloupce.
3. `0320113` — klasický sloupcový baseline.
4. **Agenda MHD** — NZ invertovaně v mřížce (mock „22").

**Fáze B — přepnout na KV_DP:**
5. `0410101` (FTL0, 35 stránek) — multi-page, dopravce s adresou, datumová omezení.
6. `0425017` (FTL1) — prázdninové popisky skupin.

**Fáze C — přepnout na CB_CSADAD (nejbohatší):**
7. `0365005` — rozdíly tras 6 variant (A/B/C…).
8. `0365001` — rozdíly tras + bezbariér H/F + dopravce s adresou.
9. `0365004` — agregát bezbariér + filtr.
10. `0340303` — časy jízdy přes pobytové uzly.
11. `0345061` — 8 variant TrasaMin (ověřit, jak se chová nehotová multi-varianta — očekávaná nepřesnost).

---

## 8. Edge cases a pasti (na co dát pozor)

- **Žádná aktuální zastávka** (StopList): v default režimu se nevykreslí nic + šipka chybí.
- **Σ pevných šířek sloupců > Width** (StopList): auto sloupce = 0, obsah ořezán clipem.
- **Range časová osa wrap** (DepartureGrid): `to < from` generuje extended hodiny 24..47, header `%24`.
- **Auto-scale vs auto-expand** (oba elementy s pevnou Height): shrink (scale<1) vs grow (cap 1.5×).
- **Výška vs glyf asymetrie:** výška řádku se počítá z max resolved per-column fontu; `Render()` a
  `CalculateHeightFromData()` musí dát identickou výšku (jinak špatný scale/overflow).
- **ColorRules = mrtvý kód** — neočekávej, že barevná pravidla fungují; barvení jde z dat (`FORE_COLOR`).
- **Cache layout DepartureGrid:** fingerprint neobsahuje všechny layout-affecting properties
  (`HourColumnPerGroup`, `GroupSeparatorWidth`, `ShowSkupinaDnuHeader`) → potenciální stale-cache;
  testovat změnu těchto + reopen.
- **Designer vs print:** bez dat designer ukáže placeholdery; s daty ale 0 položek → prázdno.
- **Multi-varianta TrasaMin** (0345061): záměrně nepřesná (1sloupcová), ne bug — odloženo.
