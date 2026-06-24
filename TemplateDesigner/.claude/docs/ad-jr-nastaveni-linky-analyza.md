# Zastávkový JŘ (AD) — analýza nastavení linky a rozdílů tras

> **Jedna ucelená analýza** zastávkového jízdního řádu z **linkových/meziměstských dat** (agenda
> `ZastavkoveJrAd`, větev `VolanoZ=JRMAIN`, data `^JRL`/`^JRZ`/`^JRSET` per-namespace = per-firma).
> Pokrývá: jak legacy čte nastavení tisku na úrovni linky (taby *MHD* / *MHD – další* / dialog
> *Nastavení pro tisk linky*), co z toho umíme v novém, **rozdíly tras** (anotace vs variantní časy),
> a co zbývá. Sloučeno z dřívějších dvou analýz (nastavení linky + rozdíly tras).
>
> **Zdroje:** legacy MAC `C:\DEV\EDISON\JRMAIN\CACHE\JRapi*.mac` (čteno přímo) + formátovač
> `ALVA.TISKMHD.Data`; náš `ZastavkoveJrAd.cls`, `StopListElement.cs`, `DepartureGridElement.cs`,
> `NotesLegendElement.cs`. Ověřeno proti **gold oraclu** přes MCP (`cache_query ALVA.TISKMHD.Data/LoadData`).
>
> **Související:** helpdesk `customer-requirements-zastavkovy-jr.md`, principy `feature-design-principles.md §5`.
> **Datum:** 2026-06-21.

---

## 0. Klíčové architektonické zjištění

**`ALVA.TISKMHD.Data` není místo logiky — je to jen výstupní formátovač.** Přes `VolanoZ` vybírá
producenta dat (`$$WinTiskMhd^JRapi` pro JRMAIN). Veškerá logika čtení nastavení linky je v **MAC
řadě `JRapi*`** (na disku v `C:\DEV\EDISON`, ne v našem repu). `ZastavkoveJrAd` nahradil producenta
i formátovač jednou `GetData` → JSON, psanou proti gold oraclu.

**Tři vstupní body legacy** (jak se nastavení linky čtou):

| Reader (`$$label^routina`) | UI místo | Storage |
|---|---|---|
| `gPrpLin^JRapiLin` (ř.282) | vlastnosti linky **+ tab „MHD – další"** (FTL/NZ) | `^JRL(sku,lin,"PATCH",…)`, `("I")`, `("C",…)` |
| `gPrpLinMhd^JRapiLin1` (ř.82) | **tab „MHD"** (lokální označení, Informace, Komentáře, Spoje-nepoznámkovat) | `^JRL(sku,lin,"MhD",…)`, `("PATCH","MHDSPNPZN")` |
| `gSetLinPrt^JRapiLin1` (ř.183) | dialog **„Nastavení pro tisk linky"** (82 polí) | `^JRL(sku,lin,"PATCH",…)` |

> **Pozn. AD vs MHD modul:** ruční poznámky (Komentáře, nepoznámkovat, směrové) jsou **AD-only**
> (`^JRL` konstrukty). Self-contained MHD modul (`^ALVA.MHD.*`, agenda `ZastavkoveJrMhd`, jen DPCB/DPKV)
> je nemá. Většina dopravců (TRANSDEV ad.) jezdí AD + lokální MHD v AD datech → helpdesk „MHD" tikety
> jsou reálně o AD agendě.

---

## 1. Legenda stavů

**Máme:** ✅ hotovo a ověřeno · 🟡 částečně/dormant · ⬜ chybí · ➖ není věc agendy (šablona TD / jiný typ JŘ).
**Ve starém:** ✅ ověřeno (gold oracle) · 📄 doloženo · ❓ logika v MAC, neověřeno.

---

## 2. Hlavička a identifikace linky

| Feature | Legacy storage | Máme | Ve starém |
|---|---|---|---|
| Číslo + název linky | `("I")` | ✅ | ✅ |
| Platnost od/do | `PATCH,"Plati"` | ✅ | ✅ |
| Lokální označení / titulek / Informace | `MhD,"SeT_MhD"` $1/$2/$5 | ✅ | ✅ |
| Dopravce (název+adresa+tel) | `^JRSET("DOP",fa[,pr])` | ✅ vč. adresy | ✅ |
| **Poznámka nad linku** (`POZNAMKA_NAD_LINKU`) | `PATCH,"Head"` | ✅ AD-N1 | ✅ |
| **Typ JR** (`TYP_JR`: výlukový/návrh) | `PATCH,"OBJ"` | ✅ AD-N2 (volitelný placeholder) | ✅ |
| QR / poznámka pod dopravce | `PATCH,"QR_CODE"`/`"PoznPodDopr"` | 🟡 čteme, dle šablony | 📄 |
| Návrh/výluka razítko (auto) | — | ⬜ | ❓ (jen PS větev) |

---

## 3. Sloupce mřížky (skupiny dnů) — tab „MHD – další"

AD počítá sloupce z FTL + Plk + CNZ (MHD má `TypProvozuID` přímo). Čteno `gPrpLin` dta10-13, při tisku
`JRapiLinPrtMhd1.mac:36-45`.

| Feature | Legacy storage | Máme | Ve starém |
|---|---|---|---|
| FTL rozvržení (0-3 sloupce) | `PATCH,"MHD_TYP_R"` | ✅ FTL 0-3 | ✅ FTL0/1 ověřeno |
| NZ prázdninové dělení | `MHD_NZ1_NZ2`/`MHD_NENZ1_NENZ2` | 🟡 kód hotový, **dormant** (data prázdná) | 📄 |
| Nevypisovat NZ ve sloupci (legenda) | `MHD_NEZNZ123` | ⬜ AD-N5 | 📄 |
| Override nadpisů sloupců | `ISY_LinkyTiskMHDNadpisy` → `^JRSET` | ✅ AD-N6 (vlastnost elementu, `HeaderOverride`) | 📄 |
| Jednosměrná linka | `linJEDNOSMERNA` | 🟡 ❓ | 📄 |

---

## 4. Rozdíly tras ⭐ (jádro tématu)

**Dva nezávislé mechanismy** — pochopit odděleně je půlka řešení:

| | **A) Anotace odlišnosti** (písmena u minut) | **B) Variantní časové sloupce** (TrasaMin 1-15) |
|---|---|---|
| Vyjadřuje | „spoj jede jinudy" — textem | „varianta má tyto jízdní časy" — sloupcem |
| Kde | horní index u minuty + legenda | sloupec časů ve StopListu (`<` = nestaví) |
| Legacy | `OmTrasa^JRapiLinPrtMhd` | `prutmbmp` detekce variant, max 15 sloupců |
| **Máme** | ✅ **plně** (AD `RozdilyTras`, MHD alg.1002) | 🟡 **jen 1 sloupec** (hlavní trasa) |

### 4.1 Mechanismus A — anotace (✅ hotovo, ověřeno 1:1)
`RozdilyTras` porovná trasu spoje se vzorovou (základní) a sestaví text:
- „**neobsluhuje** zast. X" (přeskočí) · „**jede přes** zast. X" (navíc) · „**jede jen do** zast. X" (uťatý)
- „**jede až do** zast. X" (prodloužení — #7947, nahradilo matoucí legacy „pokračuje přes")

Marker (písmeno od „B", sada `BCEFGHIJ…` bez „D"), dedup, gated `PATCH,"mhd","not"` (AutoPozn).
**Ověřeno 1:1 na CB_CSADAD 0365005** (6 variant B/C/E/F/G/H = slovo od slova gold oracle).

### 4.2 Mechanismus B — variantní časy (🟡 mezera, vědomě odložená)
Legacy detekuje varianty přes bitmapu jízdních časů, každou dá do sloupce TrasaMin1-15 (`<` u projetých),
spoj → varianta přes písmeno. **My máme jeden sloupec** `CAS_JIZDY` (hlavní trasa); varianty 2-15 ⬜.

### 4.3 Obecnost — A vs B nejsou duplicitní
- **Topologie** (které zastávky) — umí oba (A textem, B přes `<`). Překryv.
- **Časy** (kvantitativní) — umí **jen B**. Případ „stejná trasa, jiné časy" je A slepý.
- **Reálné AD zjištění:** přeskočení zastávky **posune i následující časy** (0365001: TrasaMin1 `<`+16/18
  vs TrasaMin2 15/19). Takže B nese reálnou informaci, ne jen vizuál. My ukazujeme jen hlavní variantu →
  časy vedlejší varianty ±pár min nepřesné; anotace topologii ukazuje správně.

### 4.4 Kritika legacy + návrh lepšího systému
Legacy 15 hardcoded sloupců = široká nečitelná tabulka + **dvě paralelní sady písmen** (varianta vs
odlišnost) = matoucí. **Neportovat 1:1.** Návrh, až přijde reálná potřeba:
- Caché emituje pole variant (`CASY_VARIANT`, ne 15-strop); zpětně kompat. `CAS_JIZDY`.
- StopList: dynamický počet časových sloupců + **strop přehlednosti** (málo variant → sloupce, hodně →
  fallback na anotace A). Jedna sada písmen.
- **Gold oracle máme:** CB_CSADAD **0345061** (8 variant) + **0365001** (2 varianty).

---

## 5. Layout a viditelnost legendy/poznámek

**Princip:** v novém světě je layout/viditelnost legendy věc **`NotesLegendElement` (TD)**, ne port
legacy PATCH flagů. Tj. místo čtení `Trid_delK`/`NegZnNR` z Caché je vystavit jako vlastnosti elementu.

| Feature | Legacy | Cíl v novém | Stav |
|---|---|---|---|
| Uspořádat poznámky dle délky | `PATCH,"Trid_delK"` | vlastnost `NotesLegendElement` | ⬜ AD-N3 |
| NZ na nový řádek | `PATCH,"NegZnNR"` | vlastnost elementu | ⬜ AD-N4 |
| Layout legendy (1 vs 2 sloupce) | (vizuál) | vlastnost elementu | ⬜ |
| Skrýt agreg. „bezbariér" poznámku | — | parametr `SCHOVAT_AGREGAT_BEZBARIER` (filtr-model) | ✅ #7161 D6 (latentní) |
| Skrýt „x na znamení" když sloupec hidden | — | TD (viditelnost StopList) | ⬜ #7161 D5 |
| Číslování poznámek (TYPPOZ=1 pevná čísla) | firm-config | — | ⬜ deferred |

> **Ruční poznámky (tab MHD) — viditelnost řešena #7955:** parametr `SCHOVAT_V_ROZSAHU` (Ruční /
> Automatické / Obojí) + render-time `NoteVisibilityFilter` nad nadmnožinou z Caché. Viz §6.

---

## 6. Ruční / ad-hoc poznámky linky — tab „MHD" (✅ hotovo)

| Feature | Legacy storage | Stav |
|---|---|---|
| **Komentáře** (znak+text+spoje) | `MhD,"SeT_SpeC"` + per-spoj `MhD_SpeC` | ✅ AD-N7 |
| **Spoje, nepoznámkovat** (potlačit v rozsahu TČ) | `PATCH,"MHDSPNPZN"` (`odTC~doTC`) | ✅ AD-N8 |
| **Směrové poznámky** tam/zpět/vše | `MhD,"SeT_MhD",id,cnt` | ✅ AD-N9 |
| Co schovat v rozsahu (#7955) | parametr `SCHOVAT_V_ROZSAHU` + filtr | ✅ AD-N8b |

Model: Caché emituje **nadmnožinu** anotací/legendy otagovanou `KATEGORIE`/`SPOJ`/`V_ROZSAHU_NEPOZNAMKOVAT`;
TD render-filtr (`NoteVisibilityFilter`) schová dle volby. Caché už nedropuje, sémantika legacy zachována.

---

## 7. Atributy zastávek a spojů (✅ hotovo)

| Feature | Legacy storage | Stav |
|---|---|---|
| Na znamení (×) | data trasy „x" / `UVOD` | ✅ (filtr jen „x") |
| Bezbariér spoj (H/F) | `gSetLinPrt` dta35 `TZPA,"BBP"` + `^JRSET("PS","LIMES")` | ✅ |
| Agreg. „všechny spoje bezbariérové" | (allBBP) | ✅ (vypínač `SCHOVAT_AGREGAT_BEZBARIER` = #7161 D6) |
| Rok v omezeních (2místně) | `gSetLinPrt` dta36 `TiskDtm` | ✅ (PS3 formát) |
| Ruční názvy zastávek | `gSetLinPrt` dta18 `nazMHD` → `^JRZ` $7 | ✅ |
| Zóny | data | ✅ |
| Datumová omezení „jede/nejede" | data spoje (omzId 11-15,22) | ✅ |

---

## 8. Co NEpatří do zastávkového JŘ (→ linkové/knižní JŘ)

Tyto se v zastávkovém (vývěskovém) JŘ neřeší — patří do **linkových/knižních** JŘ (tarif dle km, přípoje):

- **Přípoje / návaznosti** (`("T",sp,32)`, `NavaznostiZpusobVypisu`) — #11300 KORID. Přesunout do roadmap linkových JŘ.
- **Km sloupce** (kilometrická vzdálenost / tarifní km, `linIDS`/`Kms`) — tarif dle km = linkové téma.
- Celý Win tisk A4/A5, kniha, HTML, odbavení/TELMAX/mikroelektronika — starý engine / mimo.

---

## 9. Co zbývá — business pohled (co to dělá / kde to uvidím)

Popsáno srozumitelně bez legacy zkratek (technický klíč v závorce jako reference). Seřazeno dle
doporučeného pořadí.

### ✅ #7161 D5 — Skrýt vysvětlivku „× na znamení", když značka není zobrazená — HOTOVO (2026-06-21)
- **Co to dělá:** Když uživatel ve vývěsce **odškrtne „Na znamení"** na StopListu, zmizí z **legendy**
  i řádek „× — zastávka na znamení". Legenda automaticky následuje viditelnost značky.
- **Kde to uvidí:** legenda poznámek pod jízdním řádem.
- **Jak:** `NoteVisibilityFilter.ShouldHideNaZnameniLegend(template)` — skryje kategorii `atribut-zastavky`,
  když žádný StopList nemá `ShowSignalStopIndicator`. Čistě TD, žádná Caché změna. Test: CB_GWBUS 0320144.

### 🟡 Layout a uspořádání legendy poznámek
Vše se týká **legendy pod jízdním řádem** — jak se poznámky uspořádají, aby se vešly a byly přehledné.
V novém světě to mají být **vlastnosti elementu legendy** (uživatel si nastaví v designeru), ne skryté
nastavení linky jako v legacy.

- **Legenda na 1 nebo 2 sloupce** — dlouhá legenda zabírá moc výšky; možnost dvou sloupců šetří místo.
- **Řazení poznámek podle délky** (AD-N3, legacy `Trid_delK`) — krátké poznámky k sobě, dlouhé k sobě → úhlednější.
- **Omezení („nejede…") na nový řádek** (AD-N4, legacy `NegZnNR`) — každé datové omezení začne na novém řádku.

### 🟡 Vlastní názvy sloupců skupin dnů
- **Co to dělá:** přepsat výchozí nadpisy sloupců odjezdů („Pracovní dny" / „Soboty" / „Neděle a svátky")
  vlastními texty (AD-N6, legacy `ISY_LinkyTiskMHDNadpisy`).
- **Kde to uvidí:** záhlaví mřížky odjezdů (`DepartureGridElement`).

### 🟡 Cizojazyčné poznámky
- **Co to dělá:** tisknout poznámky i v cizím jazyce (AD-N11, legacy `prtCzjPzn`) — pro pohraniční /
  mezinárodní linky. **Kde:** legenda pod JŘ.

### 🔴 Více variant jízdních časů (jediná velká věc)
- **Co to dělá:** když část spojů jede **jinou trasou / jinak rychle**, dnes ukazujeme v seznamu
  zastávek **jeden** sloupec časů (hlavní trasa). Legacy uměl víc sloupců — každá varianta své časy
  (a „×" u zastávek, které varianta projíždí). U variantních linek tak naše časy nesedí přesně pro
  vedlejší spoje (±pár minut); rozdíl v trase ale popisuje textová anotace (AD-N13, §4).
- **Kde to uvidí:** seznam zastávek vlevo (`StopListElement`).
- **Stav:** velký zásah (data + renderer). Navrhnout moderně (dynamický počet sloupců + strop přehlednosti,
  ne 15 napevno). Čeká na reálnou potřebu; testovací linka připravená (CB_CSADAD 0345061 = 8 variant).

### 🟢 Dormant (umíme, ale nemáme testovací data)
- **Potlačit negativní značku v některých sloupcích** (AD-N5, legacy `MHD_NEZNZ123`) — vázané na NZ, dnes
  prázdné ve všech firmách.
- **Prázdninové dělení sloupců** (AD-N15) — kód hotový, data prázdná.

---

## 10. Stav hotovo (tato + předchozí session)

**Hotovo a ověřeno 1:1:** hlavička + dopravce, FTL 0-3 sloupce, datumová omezení, **rozdíly tras
(anotace A — neobsluhuje/jede přes/jede jen do/jede až do)**, na znamení, bezbariér, časy jízdy (1
sloupec), zóny, ruční poznámky (Komentáře/nepoznámkovat/směrové), #7955 přepínač, multi-page,
poznámka nad linku, typ JR, skrytí agregátu bezbariér (#7161 D6), skrytí legendy na znamení (#7161 D5).
**Helpdesk zavřeno:** #7955, #7959, #7947, **#7161 (D5+D6)**.

---

## 11. Příloha — `gSetLinPrt` (dialog „Nastavení pro tisk linky", 82 polí)

Relevantní pro zastávkový JŘ (zbytek = ➖ Win tisk/kniha/HTML/odbavení):

| dta | Význam | PATCH | Máme |
|---|---|---|---|
| 18 | Tisk ručních názvů zastávek | `nazMHD` | ✅ |
| 20 | Tisk zóny IDS / km (0-3) | `linIDS` | 🟡 zóny ✅, km ➖ (linkové) |
| 26 | Uspořádat poznámky dle délky | `Trid_delK` | ⬜ AD-N3 |
| 27 | NZ na nový řádek | `NegZnNR` | ⬜ AD-N4 |
| 28 | Cizojazyčné poznámky | `prtCzjPzn` | ⬜ AD-N11 |
| 30 | Gate auto-poznámek odchylek | `mhd,"not"` | ✅ |
| 32 | Sada písmen markerů | `mhd,"notchar"` | ✅ |
| 35 | Tisk bezbariér (0/1/2) | `TZPA,"BBP"` | ✅ |
| 36 | Rok v omezeních (0-4) | `TiskDtm` | ✅ |
| 73 | Auto generovat časové průběhy | `mhd,"par",1` | 🟡 1 sloupec |
| 74 | Volné značky pro minutové trasy | `mhd,"par",2` | ⬜ AD-N13 |
| 78 | Způsob výpisu návazností | `NavaznostiZpusobVypisu` | ➖ linkové JŘ |

**Reference souborů:** `JRapiLin.mac` (`gPrpLin` ř.282), `JRapiLin1.mac` (`gPrpLinMhd` ř.82,
`gSetLinPrt` ř.183), `JRapiLinPrtMhd1.mac` (FTL/varianty ř.36-300), `JRapiLinPrtMhd.mac`
(`OmTrasa`/`gDopMHD`/`UVOD`), `JRapiNZUtl.mac` (omezení/NZ).
