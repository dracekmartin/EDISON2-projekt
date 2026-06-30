# Typy poznámek a layout legendy — analýza pro konfigurovatelnost

> **Status (2026-06-30):** layout legendy (1–4 sloupce, řazení dle délky, směr wrapu, zarovnaný
> marker, filtr per typ) je **z velké části implementován** v `NotesLegendElement` — viz changelog
> 2026-06-21/23. Tento dokument = analytický podklad, ne status.

> **Účel.** Srozumitelně (bez legacy zkratek) popsat: jaké **typy poznámek** v zastávkovém JŘ máme,
> jak se dnes zobrazují a nastavují, a **jak umožnit uživateli nastavit layout legendy** (sloupce,
> řazení, viditelnost per kategorie). Podklad pro rozhodnutí, co a jak doplnit do `NotesLegendElement`.
>
> **Týká se TD elementu legendy** (`NotesLegendElement`), ne nastavení linky v Caché. Doplňuje
> `ad-jr-nastaveni-linky-analyza.md §5`. **Datum:** 2026-06-21.

---

## 1. Dvě klasifikace poznámky (klíč k pochopení)

Každá poznámka v legendě má **dvě nezávislé vlastnosti**:

### A) TYP — jak řádek VYPADÁ (řídí vykreslení)
| Typ | Jak vypadá | Příklad |
|---|---|---|
| **Negativní značka** | znak v **invertovaném** rámečku (bílé na černém) + text | omezující značka |
| **Speciální znak** | **symbol** (volitelně barevný) + text | „H" bezbariér, „×" na znamení |
| **Text** | prostý text; pokud má znak, znak + oddělovač + text | „B — neobsluhuje zast. X" |

→ V kódu enum `TypPoznamky { Text, NegativniZnacka, SpecialniZnak }`. Přichází z dat (pole „TYP").

### B) KATEGORIE — co poznámka ZNAMENÁ (business; řídí viditelnost)
| Kategorie | Co to je | Kde vzniká | TYP |
|---|---|---|---|
| `rozdil-trasy` | „neobsluhuje / jede přes / jede jen do / jede až do zast. X" | rozdíly tras spojů | Text |
| `omezeni` | „nejede 24.12.…", „jede také…" (datová platnost) | omezení spoje | Text |
| `bezbarier` | „spoj má bezbariérově přístupné vozidlo" (per spoj) | atribut spoje | Speciální znak |
| `bezbarier-agregat` | „**všechny** spoje mají bezbariérové vozidlo" (souhrn) | celá linka bezbariér | Speciální znak |
| `atribut-zastavky` | „× — zastávka na znamení" | atribut zastávky | Speciální znak |
| `rucni-komentar` | ruční poznámka linky (tab MHD Komentáře) | uživatel | Text |
| `smerove` | volná poznámka tam/zpět/vše | uživatel | Text |

→ V datech pole „KATEGORIE" (přidáno kvůli filtrům #7955/#7161). **Zatím se neparsuje do render
modelu `PoznamkaData`** — používá ji jen `NoteVisibilityFilter` (na úrovni dat, před vykreslením).

**Důležité:** TYP a KATEGORIE jsou kolmé. Např. `bezbarier` i `atribut-zastavky` jsou oba „Speciální
znak" (TYP), ale jiná kategorie. Dnešní filtrování v elementu jede podle **TYP** (hrubé) — viz §2.

---

## 1b. Co uměl starý návrhář (NALEZENO ve zdroji)

Legacy návrhář „Tisk MHD" (VB.NET WinForms, `C:\DEV\EDISON\TiskMHD`) měl pro sekci poznámek dialog
**`formTISKMHDSekcePoznamkyEdit`** (tab „Sekce Poznámky — vlastnosti"). Layout pole (struktura `SSekce`,
prefix `stPOZ_`):

| Legacy pole | Co dělá | Náš stav |
|---|---|---|
| **`stPOZ_PocetSloupcu`** | **počet sloupců** legendy (render umí 1 a 2; „else" neimplementováno) | ⬜ → 3.1 |
| **`stPOZ_SirkaSloupceTextVlevo`** | šířka **levé buňky se znakem/markerem** (pevná); text vyplní zbytek | ⬜ (my renderujeme marker+text inline, ne pevná buňka) |
| **`stPOZ_VyskaRadku`** | výška řádku | ✅ `NoteLineHeight` |
| **`stPOZ_NadesignovanaVyskaSekceJeVyskouMinimalni`** | nadesignovaná výška = **minimální** (sekce se roztáhne dolů dle obsahu) | 🟡 máme dynamickou výšku / auto-scale, jiný model |
| font: `stPOZ_FontTyp/Bold/Podtrzene/Italic/Uppercase/Size/Color` | písmo vč. **VERZÁLEK** | ✅ kromě uppercase ⬜ |
| rámeček (per strana), pozadí | border + background | ✅ |
| `PrichytitZdola/ZpravaKSekci` | přichytit sekci k jiné (zdola/zprava) | ➖ (v TD jiný model umístění) |

**Layout chování (z rendereru `Render_SekcePoznamky_Data`):**
- **Řádek = [levá buňka se znakem (pevná šířka `SirkaSloupceTextVlevo`)] + [text vpravo].** Markery
  jsou tak svisle zarovnané (náš inline render je nezarovnává).
- **2 sloupce = chytrý wrap:** krátké poznámky (text ≤ šířka úzkého sloupce) se skládají **vedle sebe**
  do sloupců; **dlouhá** poznámka se roztáhne **přes celou šířku** (`SirkaSloupceValuePrezVsechnySloupce`).
  Tj. ne striktně column-major/row-major — hybrid „pakuj krátké do sloupců, dlouhé full-width".
- Skutečně podporuje **jen 1 a 2 sloupce** (3+ neimplementováno).

→ **Závěr:** sloupce/wrapping **nejde 1:1 portovat** (legacy je VB renderer s vlastní logikou), ale teď
**víme přesně, co uměl** a můžeme to v `NotesLegendElement` navrhnout čistě a klidně lépe (viz 3.1).
Legacy „Trid_delK"/„NegZnNR" (řazení dle délky / NZ na nový řádek) jsou line-flagy mimo tento dialog
(viz `ad-jr-nastaveni-linky-analyza.md`).

---

## 2. Co dnes umíme nastavit (NotesLegendElement)

| Nastavení | Co dělá | Vlastnost |
|---|---|---|
| Nadpis | text záhlaví legendy („Poznámky") | `HeaderText` |
| Oddělovač | mezi znakem a textem (" — ") | `NoteSeparator` |
| Velikost písma / výška řádku | font a rozteč | `NoteFontSize`, `NoteLineHeight` |
| Barvy záhlaví/dat | 4 base barvy | (z báze) |
| Barvy negativní značky | pozadí/text invertovaného rámečku | `NegativniZnackaBackColor/ForeColor` |
| **Viditelnost dle TYP** | zap/vyp negativní značky / speciální znaky / textové poznámky | `ShowNegativniZnacky/ShowSpecialniZnaky/ShowTextovePoznamky` |
| Skrytí „× na znamení" | auto dle StopListu (#7161 D5) | filtr |
| Skrytí agregátu bezbariér | parametr (#7161 D6) | filtr |

**Layout dnes:** legenda je **jednosloupcová**, řádky v pořadí, jak je vrací Caché (atributy zastávek
→ bezbariér → rozdíly tras → ruční komentáře → omezení → směrové). Řazení ani počet sloupců se nedá nastavit.

---

## 3. Co by uživatel chtěl nastavit (layout) — a jak to přidat

Princip (dohodnuto): **layout legendy = vlastnosti `NotesLegendElement`** (uživatel v designeru),
ne skryté nastavení linky. Reálné požadavky:

### 3.1 Počet sloupců + směr wrapování (1 / 2 / N) — ✅ HOTOVO (2026-06-21)
**Implementováno:** `NotesLegendElement.Columns` (1-4) + `WrapSmer` (PoSloupcich/PoRadcich) +
`MarkerSirka` (0=inline, >0=zarovnaná buňka značky). Render distribuuje poznámky do sloupců,
výška = `headerH + ceil(N/Columns) × řádek`. V EDInputFormu i inline panelu (skupina „Rozložení").
Default 1 sloupec = beze změny. (Hybrid „dlouhé full-width" z legacy nepřevzat — jednodušší model.)
- **Co:** dlouhá legenda zabírá moc výšky; rozdělit do 2+ sloupců = úspora místa.
- **Směr wrapování (důležité rozhodnutí):**
  - **Po sloupcích (column-major)** — naplní se sloupec 1 shora dolů, pak sloupec 2… (jako noviny).
    Čte se „dolů, pak vpravo". Pro legendu obvykle přirozenější.
  - **Po řádcích (row-major)** — naplní se řádek 1 přes všechny sloupce, pak řádek 2… Čte se
    „vpravo, pak dolů".
- **Auto rozšiřování:** počet řádků na sloupec = `ceil(počet poznámek / Columns)`; volitelně „nalít
  do N sloupců jen když se nevejde na výšku" (auto-wrap při přetečení, jinak 1 sloupec).
- **Legacy chování (reference):** 2 sloupce s **hybridním wrapem** — krátké poznámky se skládají vedle
  sebe do úzkých sloupců, dlouhá poznámka se roztáhne přes celou šířku. Můžeme převzít, nebo nabídnout
  jednodušší `WrapSměr { PoSloupcich, PoRadcich }` + volitelně „dlouhé full-width".
- **Jak přidat:** vlastnosti `Columns: int` (default 1) + `WrapSměr: enum`. Render: rozdělit `filtered`
  do sloupců, kreslit per-sloupec s offsetem X = `Width/Columns × i`. Výška = `headerH + ceil(N/Columns) × řádek`.
- **Bonus z legacy — zarovnaný marker:** legacy měl **levou buňku se znakem o pevné šířce**
  (`SirkaSloupceTextVlevo`) → markery svisle zarovnané. Náš render dnes kreslí marker+text inline
  (nezarovnané). Vlastnost `SirkaMarkeru: float` (0 = inline jako dnes, >0 = pevná buňka) by to dorovnala.

### 3.2 Řazení poznámek — ✅ HOTOVO (2026-06-21)
**Implementováno:** `NotesLegendElement.Razeni` (Vychozi/DleDelky). Default Vychozi = beze změny.
(DleKategorie/DleZnaku zatím ne — YAGNI, snadné doplnit.)
- **Co:** dnes pořadí dle Caché. Možnosti: dle délky textu (krátké/dlouhé k sobě), dle kategorie,
  abecedně dle znaku, nebo zachovat.
- **Jak přidat:** vlastnost `Razeni: enum { Vychozi, DleDelky, DleKategorie, DleZnaku }`. Seřadit
  `filtered` před renderem. Malé.

### 3.3 Viditelnost per KATEGORIE (jemnější než dnešní per-TYP)
- **Co:** dnes lze vypnout jen hrubě „speciální znaky" (skryje bezbariér **i** na znamení najednou).
  Uživatel by mohl chtít vypnout konkrétně „omezení" nebo „rozdíly tras" v legendě.
- **Jak přidat:** parsovat **KATEGORIE do `PoznamkaData`** (dnes chybí) + per-kategorie toggly.
  ⚠️ **Pozor na překryv:** na znamení (D5) a agregát bezbariér (D6) už mají vlastní automatiku/parametr.
  Per-kategorie toggle by se s nimi nesměl prát → buď je sjednotit, nebo nové toggly jen pro kategorie
  bez vlastní logiky (rozdíly tras, omezení, směrové, ruční komentář). **Rozhodnout při návrhu.**

### 3.4 Styl per TYP / kategorie (volitelné, nižší priorita)
- **Co:** jiný font/barva pro různé typy (např. omezení menším písmem).
- **Jak:** rozšířit o per-typ styl. Vyšší složitost, zatím YAGNI.

---

## 4. Doporučení / pořadí

1. **🟢 Počet sloupců (3.1)** + **řazení (3.2)** — největší praktický přínos (úspora místa, přehlednost),
   čistě vizuální, žádný dotyk dat. Dvě vlastnosti `NotesLegendElement`. Doporučuji jako první.
2. **🟡 Viditelnost per kategorie (3.3)** — užitečné, ale vyžaduje (a) parsovat KATEGORIE do `PoznamkaData`,
   (b) vyřešit překryv s D5/D6. Až po 1, s rozmyslem o interakci.
3. **🟢 Styl per typ (3.4)** — odložit (YAGNI).

**Pozn. k testování:** layout (sloupce/řazení) jde ověřit na jakékoli lince s bohatou legendou —
**CB_CSADAD 0365005** (6 řádků: B/C/E/F/G/H + omezení) je ideální.

---

## 5. Shrnutí — co máme vs co přidat

| | Stav |
|---|---|
| TYP render (NZ / speciální / text) | ✅ máme |
| Viditelnost per TYP | ✅ máme (hrubé) |
| Barvy (záhlaví, data, NZ, symbol z DB) | ✅ máme |
| Skrytí na znamení / agregát bezbariér | ✅ máme (#7161 D5/D6) |
| **Počet sloupců + směr wrapu** | ✅ hotovo (3.1) — `Columns`, `WrapSmer` |
| **Zarovnaný marker** (pevná buňka značky) | ✅ hotovo (3.1) — `MarkerSirka` |
| **Řazení poznámek** | ✅ hotovo (3.2) — `Razeni` |
| **Viditelnost per KATEGORIE** | ⬜ přidat (3.3, řeší překryv s D5/D6) |
| Styl per typ | ⬜ odložit (3.4) |
