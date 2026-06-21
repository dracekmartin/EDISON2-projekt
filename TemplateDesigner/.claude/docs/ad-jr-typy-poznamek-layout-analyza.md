# Typy poznámek a layout legendy — analýza pro konfigurovatelnost

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

## 1b. Co uměl starý návrhář (srovnání) — částečně známé

⚠️ **Plný legacy stav nemáme zdokumentovaný.** Layout poznámkové sekce (sloupce, wrapping) byl v
**samostatném návrháři „Tisk MHD"** (WinForms appka, kterou TD nahrazuje — viz screenshoty „Sekce …
vlastnosti [SKNI_MHDNITRA]"). Její layout-definice **nejsou v JRMAIN kódu**, který máme → konkrétní
schopnosti (kolik sloupců poznámek, jaké wrapování) z kódu nevyčteme.

**Co doložené máme** (legacy nastavení linky ovlivňující legendu, viz `ad-jr-nastaveni-linky-analyza.md`):
| Legacy volba | Význam | Náš stav |
|---|---|---|
| `Trid_delK` (Uspořádat poznámky dle délky) | řazení poznámek dle délky textu | ⬜ → 3.2 |
| `NegZnNR` (Negativní značky na nový řádek) | NZ začínají na novém řádku | ⬜ → 3.2 |
| (sloupce / wrapping poznámek) | layout poznámkové sekce | ❓ legacy návrhář, nemáme zdroj → navrhnout nově (3.1) |

→ Pro sloupce/wrapping tedy **nejde o 1:1 port** (legacy zdroj nemáme), ale o **nový čistý návrh**
v `NotesLegendElement` (3.1). Cíl: dorovnat/překonat starý návrhář.

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

### 3.1 Počet sloupců + směr wrapování (1 / 2 / N)
- **Co:** dlouhá legenda zabírá moc výšky; rozdělit do 2+ sloupců = úspora místa.
- **Směr wrapování (důležité rozhodnutí):**
  - **Po sloupcích (column-major)** — naplní se sloupec 1 shora dolů, pak sloupec 2… (jako noviny).
    Čte se „dolů, pak vpravo". Pro legendu obvykle přirozenější.
  - **Po řádcích (row-major)** — naplní se řádek 1 přes všechny sloupce, pak řádek 2… Čte se
    „vpravo, pak dolů".
- **Auto rozšiřování:** počet řádků na sloupec = `ceil(počet poznámek / Columns)`; volitelně „nalít
  do N sloupců jen když se nevejde na výšku" (auto-wrap při přetečení, jinak 1 sloupec).
- **Jak přidat:** vlastnosti `Columns: int` (default 1) + `WrapSměr: enum { PoSloupcich, PoRadcich }`
  (default PoSloupcich). Render: rozdělit `filtered` do sloupců dle směru, kreslit per-sloupec s offsetem
  X = `Width/Columns × i`. Výška = `headerH + ceil(N/Columns) × řádek`. Střední velikost (render + výpočet výšky).

### 3.2 Řazení poznámek
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
| **Počet sloupců** | ⬜ přidat (3.1) |
| **Řazení poznámek** | ⬜ přidat (3.2) |
| **Viditelnost per KATEGORIE** | ⬜ přidat (3.3, řeší překryv s D5/D6) |
| Styl per typ | ⬜ odložit (3.4) |
