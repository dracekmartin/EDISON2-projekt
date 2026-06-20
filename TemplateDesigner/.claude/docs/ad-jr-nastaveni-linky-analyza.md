# Business analýza: nastavení tisku na úrovni linky (zastávkový JŘ z AD/JRMAIN dat)

> **Účel.** Jeden ucelený přehled všech nastavení tisku, která se v legacy EDISONu konfigurují
> **na úrovni konkrétní linky** (taby *MHD*, *MHD – další*, dialog *Nastavení pro tisk linky*) a
> která ovlivňují zastávkový jízdní řád z **linkových/meziměstských dat** (agenda `ZastavkoveJrAd`,
> větev `VolanoZ=JRMAIN`). U každé položky: **business význam**, **jak/kde to legacy čte** (UI tab +
> reader v MAC + storage v `^JRL`), **zda to umíme v novém** a **zda/jak to bylo ve starém**.
>
> **Zdroj.** Legacy zdroják ověřen přímo na disku (`C:\DEV\EDISON\JRMAIN\CACHE\JRapi*.mac`), ne odhad.
> Stav nové agendy z `cache-database-linky.md §10`. Helpdesk vazby z `customer-requirements-zastavkovy-jr.md`.
>
> **Datum:** 2026-06-20.

---

## 0. Klíčové architektonické zjištění — „kde to ve starém je"

Důležité pro pochopení celé analýzy: **`ALVA.TISKMHD.Data` NENÍ místo, kde žije logika nastavení
linky.** Je to jen **sdílený výstupní formátovač** (data-source agnostic). Přes `VolanoZ` jen vybírá
producenta dat:

```objectscript
if (VolanoZ = "JRMAIN") { s err=$$WinTiskMhd^JRapi(IdFa, db, SkupinaId, LinkaId, glb, .arri, .out) }
```

Veškerá logika čtení nastavení linky (FTL sloupce, NZ dělení, automatické poznámky, rozdíly tras,
ruční komentáře…) je v **MAC routinách řady `JRapi*`** (na disku v `C:\DEV\EDISON`, **ne v našem
repu** — běží na serveru). Reimplementace `ZastavkoveJrAd` nahradila **producenta i formátovač**
dohromady jednou `GetData` → JSON, psanou proti **gold oraclu** (legacy LoadData výstup).

### Tři vstupní body pro čtení nastavení linky

Všechna nastavení z UI čtou **tři getter labely**. UI je volá, vykreslí do polí; uložení jde
zrcadlovým `s…` labelem (vždy nejdřív `k` podstrom, pak zápis neprázdných polí).

| Reader (`$$label^routina`) | UI místo | Co čte | Storage root |
|---|---|---|---|
| `gPrpLin^JRapiLin` (ř.282) | vlastnosti linky **+ tab „MHD – další"** | typ, platnost, poznámky, **FTL/NZ rozvržení**, názvy | `^JRL(sku,lin,"PATCH",…)`, `("I")`, `("C",…)` |
| `gPrpLinMhd^JRapiLin1` (ř.82) | **tab „MHD"** | lokální označení, info pro tisk, **Komentáře**, **Spoje-nepoznámkovat** | `^JRL(sku,lin,"MhD",…)`, `("PATCH","MHDSPNPZN")` |
| `gSetLinPrt^JRapiLin1` (ř.183) | **dialog „Nastavení pro tisk linky"** | 82 polí — vývěska, kniha, Win tisk, MHD odjezdník | `^JRL(sku,lin,"PATCH",…)` |

Při vlastním **tisku** producent čte tytéž PATCH klíče **přímo z globálu** (ne přes gettery) — hlavní
je `JRapiLinPrtMhd1.mac` (viz §3).

**Pozn.:** `^JRL` je per namespace = per firma (bez IdFa). `sku` = skupina linek, `lin` = 7-znakové
číslo linky (`$$cnvCsLin^JRapi`).

---

## 1. Legenda

**Máme v novém?** ✅ hotovo a ověřeno 1:1 · 🟡 částečně/dormant · ⬜ chybí · ➖ není věc agendy (řeší
šablona TD nebo starý Win engine).

**Ve starém?** ✅ ověřeno (gold oracle / zdroják) · 📄 doloženo daty/docs · ❓ logika v MAC, nutno potvrdit.

---

## 2. Hlavička a identifikace linky

| Feature | Business význam | Legacy: UI / reader / storage | Máme? | Ve starém? |
|---|---|---|---|---|
| Číslo + název linky | Záhlaví JŘ | `gPrpLin` / `^JRL(...,"I")` | ✅ | ✅ |
| Typ linky | VNIT/DALK/MEST… | `gPrpLin` dta1 / `("I")` #1\10 | ✅ nepřímo | ✅ |
| Platnost od/do | Datum platnosti JŘ | `gPrpLin` dta2 / `PATCH,"Plati"` = `od~do` | ✅ | ✅ |
| Typ JR (návrh/výluka) | Razítko „NÁVRH"/„VÝLUKOVÝ" | `gPrpLin` dta3 / `PATCH,"OBJ"` | ⬜ | 📄 |
| Poznámka nad linku | Text v hlavičce | `gPrpLin` dta4 / `PATCH,"Head"` | ⬜ | 📄 |
| Poznámka pod dopravce | Text pod blokem dopravce | `gPrpLin` dta5 / `PATCH,"PoznPodDopr"` | 🟡 čteme do `POZNAMKA_POD_DOPRAVCE` | 📄 |
| QR kód | QR pro JŘ | `gPrpLin` dta9 / `PATCH,"QR_CODE"` | 🟡 čteme do `QR_KOD` | 📄 |
| Lokální označení linky | „101" pro cestující | `gPrpLinMhd` dta1 / `MhD,"SeT_MhD"` $1 | ✅ | ✅ |
| Titulek („MHD v…") | Nadpis sestavy | `gPrpLinMhd` dta2 / `SeT_MhD` $2 | ✅ | ✅ |
| Informace pro tisk | Volný text (`<br>`→newline) | `gPrpLinMhd` dta5 / `SeT_MhD` $5 | ✅ | ✅ |
| Dopravce (název+adresa+tel) | „Zajišťuje: DPKV, a.s., Sportovní…, tel:…" | `^JRSET("DOP",fa[,pr])` | ✅ vč. adresy provozovny | ✅ |

> Naše agenda čte `SeT_MhD` $1/$2/$5 přes `sTIT^JRapiLinPrtMhd1` — **shoduje se** s tímto readerem.

---

## 3. Odvození sloupců mřížky (skupiny dnů) — tab „MHD – další"

**Jádro odlišnosti AD od MHD.** MHD má `TypProvozuID` jako přímou entitu = 1 sloupec. AD sloupce
**počítá** z kombinace FTL módu + Plk (tarifní platnost spoje) + CNZ (negativní značka). Čteno v UI
přes `gPrpLin` (dta10-13), při tisku přímo v `JRapiLinPrtMhd1.mac:36-45`.

| Feature | Business význam | Legacy: UI / reader / storage | Máme? | Ve starém? |
|---|---|---|---|---|
| Tisk v rozvržení (FTL 0-3) | Kolik sloupců (PD/So/Ne; prázdninové dělení; 4 sloupce) | tab MHD-další radio / `gPrpLin` dta10 (`+1`) / `PATCH,"MHD_TYP_R"` | ✅ FTL 0-3 (`ZarazeniSpoje`, `NacistSkupinyDnu`) | ✅ FTL0/1 ověřeno; FTL2/3 per-legacy bez test-linky |
| Zařazení spoje dle Plk | denně/PD/So/Ne dle tarifní platnosti | data spoje (Plk) `#`-pole 7 | ✅ | ✅ |
| NZ „jede mimo / o prázdninách" | NZ řídící prázdninové dělení (FTL 1/3) | tab MHD-další / `gPrpLin` dta11 / `PATCH,"MHD_NZ1_NZ2"` (~: $2 nejede, $1 jede o prázd.) | 🟡 kód hotový, **dormant** (prázdné ve všech firmách) | 📄 |
| Nevypisovat spoje s NZ (per sloupec) | Negativní filtr — spoj se ve sloupci skryje | tab MHD-další / `gPrpLin` dta12 / `PATCH,"MHD_NENZ1_NENZ2"` | 🟡 kód hotový, dormant | 📄 |
| Nevypisovat NZ ve sloupci 1-4 (legenda) | Potlačit symbol NZ v legendě per sloupec | tab MHD-další / `gPrpLin` dta13 / `PATCH,"MHD_NEZNZ123"` (~: NZNSH1-4) | ⬜ **nečteme** | 📄 (čteno v `JRapiLinPrtMhd1:42-45`) |
| Override nadpisů sloupců | Vlastní texty „Pracovní dny / Soboty…" | tab MHD-další dropdown / `gPrpLin` dta31 / `PATCH,"ISY_LinkyTiskMHDNadpisy"` → `^JRSET` | ⬜ fixní per-FTL nadpisy | 📄 (čteno v `JRapiLinPrtMhd1:499`) |
| Jednosměrná linka | Tisknout jen jeden směr | `gPrpLin` dta19 / `PATCH,"linJEDNOSMERNA"` | 🟡 ❓ | 📄 |

> **Dormant (NZ dělení):** logika prázdninového dělení je naprogramovaná a synteticky ověřená
> (`cnz=NZFTL1→jen „mimo prázdniny"`, `cnz=NZFTL2→jen „o prázdninách"`), ale `MHD_NZ1_NZ2` je
> **prázdné ve všech naskenovaných firmách** (CB_GWBUS/KV_DP/CB_CSADAD/VS_CSAD). Umíme to, ale nemáme
> reálná data k demu.
>
> **Print-engine potvrzení (`JRapiLinPrtMhd1.mac`):**
> ```objectscript
> :36  s FTL=+$g(^[db]JRL(Sku,Lin,"PATCH","MHD_TYP_R"))
> :37-40 NZFTL1/2, NENZFTL1/2 = $p(MHD_NZ1_NZ2 / MHD_NENZ1_NENZ2,"~",…)
> :42-45 NZNSH1..4 = $p(MHD_NEZNZ123,"~",1..4)        ← nevypisovat NZ ve sloupci 1-4 (NEČTEME)
> :499   ISYLinkyTiskMHDNadpisy = ...,"ISY_LinkyTiskMHDNadpisy" (NEČTEME)
> ```
> Náš `ZarazeniSpoje`/`NacistSkupinyDnu` = 1:1 reimplementace téhle FTL+NZ logiky. Jediné, co tahle
> větev čte a my **ne**: `MHD_NEZNZ123` a `ISY_LinkyTiskMHDNadpisy`.

---

## 4. Automatické poznámky ⭐

Legacy logika: `OmTrasa^JRapiLinPrtMhd` (rozdíly tras), `JRapiNZUtl` (texty omezení/NZ). Gate +
marker sada se čtou přes `gSetLinPrt` (dta30/32), při tisku přímo z PATCH.

| Feature | Business význam | Legacy: UI / reader / storage | Máme? | Ve starém? |
|---|---|---|---|---|
| Gate auto-poznámkování | Hlavní vypínač auto-poznámek rozdílů tras | dialog / `gSetLinPrt` dta30 / `PATCH,"mhd","not"` | ✅ (`AutoPozn`) | ✅ |
| Sada písmen pro markery | Které znaky pro A/B/C (default „BCEFGH…") | dialog / `gSetLinPrt` dta32 / `PATCH,"mhd","notchar"` | ✅ | ✅ |
| Rozdíly tras „neobsluhuje zast. X" | Spoj jede jinou trasou než základní | auto (gate) / `OmTrasa^JRapiLinPrtMhd` | ✅ 1:1 (`RozdilyTras`, marker od „B") | ✅ ověřeno 0365001 |
| Datumová omezení „jede/nejede" | „jede jen…", „nejede…", „jede také…" | data spoje (omzId 11-15,22) / `JRapiNZUtl` | ✅ 1:1 (`SestavTextOmezeni`+`PrefixOmezeni`) | ✅ |
| Formát datumů v poznámkách | 2místný rok, strip nul, „od D1 do D2" | dialog / `gSetLinPrt` dta36 / `PATCH,"TiskDtm"` (PS3 formát) | ✅ (`FormatDatumyOmezeni`/`UpravDatum`) | ✅ |
| Číslování poznámek (sekvenční) | 1/2/3 indexy | TYPPOZ=0 (`^JRSET("Definice","MHDnewOM")` prázdné) | ✅ pro TYPPOZ=0 | ✅ |
| Číslování poznámek (a/b/c / NZ-symbol) | TYPPOZ=1 režim | firm-config | ⬜ deferred (žádná test firma) | 📄 |
| Časové průběhy tras (auto) | Auto generovat časové sloupce | dialog / `gSetLinPrt` dta73 / `PATCH,"mhd","par",1` | 🟡 (1 sloupec) | 📄 |
| Text „spoj jede AŽ DO XY" | Symetrický k „končí v zast. X" — prodloužení trasy | auto | ⬜ **chybí** → **#7947** | ❓ |
| Týdenní platnosti (1-7,+) | „jede v pondělí…" z NCOME | `^JRSET("NCOME",jazyk,N)` | ⬜ (v datech se nevyskytuje) | 📄 |

---

## 5. Ruční / ad-hoc poznámky linky — tab „MHD" ⭐⭐ (NEJVĚTŠÍ GAP)

**Nejdůležitější mezera.** V tabu *MHD* jsou gridy, kterými dispečer ručně dolaďuje poznámky linky.
Čteny přes `gPrpLinMhd^JRapiLin1:82-131`. **Nová agenda je vůbec nečte.**

### Komentáře (grid Znak | Komentář | Spoje)

Ruční poznámka linky: uživatel zadá **znak** (id) + **text** + vybere **spoje**.
```
^JRL(sku,lin,"MhD","SeT_SpeC",id)   = text komentáře (id = znak/písmeno)
^JRL(sku,lin,sm,sp,"MhD_SpeC")      = id   ← přiřazení: spoj sp (směr sm=T/Z) → znak id
```
Reader pro každý `id` vytáhne text a zpětně projde všechny spoje obou směrů, posbírá ty s `MhD_SpeC [ id`.

### Spoje, nepoznámkovat (grid Spoj | od TČ | do TČ)
```
^JRL(sku,lin,"PATCH","MHDSPNPZN",sp) = odTC~doTC
```
Potlačení automatické poznámky pro spoj `sp` v rozsahu tarifních čísel od–do.

### Poznámky k MHD (tam / zpět / vše)
```
^JRL(sku,lin,"MhD","SeT_MhD",id,cnt) = text   (id ">"=tam, "<"=zpět, "<>"=vše)
```

| Feature | Business význam | Legacy: storage | Máme? | Ve starém? |
|---|---|---|---|---|
| Komentáře (znak+text+spoje) | Ruční vlastní poznámka přiřazená spojům | `MhD,"SeT_SpeC"` + per-spoj `MhD_SpeC` | ✅ Fáze 1 | ✅ (`gPrpLinMhd`) |
| Spoje, nepoznámkovat | Potlačit poznámku pro spoje v rozsahu TČ | `PATCH,"MHDSPNPZN"` = `odTC~doTC` | ✅ Fáze 1 (`NepoznamkovatSpoj`) | ✅ (`gPrpLinMhd`) |
| Poznámky tam/zpět/vše | Volné textové poznámky per směr | `MhD,"SeT_MhD",id,cnt` | ✅ Fáze 1 | ✅ |
| Co schovat v rozsahu nepoznámkovat (#7955) | Ruční / Automatické (rozdíly tras) / Obojí | param `SCHOVAT_V_ROZSAHU` + TD filtr | ✅ Fáze 2 (`0b5838d`) | ❓ (legacy uměl jen „ruční") |
| Bug: „nepoznámkovat" maže i NZ | Inverzní znaménko se nemá mazat | (chování) | ✅ v AD bug nevzniká (filtr nesahá na `omezeni`/`bezbarier`) | ❓ (jen MHD) |

> **Vazba na helpdesk:** #7955 **vyřešeno pro AD** (Fáze 1 + 2) — viz `customer-requirements §D2/D3`.
> Klientův scénář („zapnu Komentáře + nepoznámkovat → jen můj komentář") pokryt přes parametr
> `SCHOVAT_V_ROZSAHU=Automatické`. Detail mechanismu §0 + `feature-design-principles.md §5.2`.

---

## 6. Atributy zastávek a spojů

| Feature | Business význam | Legacy: storage / reader | Máme? | Ve starém? |
|---|---|---|---|---|
| Na znamení (×) | Zastávka jen na znamení | data trasy „x" / `UVOD^JRapiLinPrtMhd` | ✅ (filtr jen „x") | ✅ ověřeno 0365001 |
| Bezbariér spoj (H/F) | „spoj má bezbariérové vozidlo" / „částečně" | `gSetLinPrt` dta35 / `PATCH,"TZPA","BBP"` (0 nenast./1 nad spoj/2 v pozn.) + `^JRSET("PS","LIMES")` | ✅ (`BezbarierKonfig`, H/F) | ✅ ověřeno 0365001 |
| Agreg. poznámka „všechny spoje bezbariérové" | Souhrn místo per-spoj | (allBBP) | ✅ | ✅ |
| Vypnout agreg. poznámku bezbariér | User option | — | ⬜ → **#7161 D6** | ❓ |
| Skrýt „x na znamení" když je sloupec hidden | Legenda jen pro viditelné sloupce | (TD column visibility) | ⬜ → **#7161 D5** (řešit v TD) | ➖ |
| Ostatní atributy (autobus/vlak/zóny…) | Ikonové atributy | data trasy | ➖ legacy je v tomto výstupu netiskne (jen „x") | ✅ (záměrně potlačeno) |

---

## 7. Časy jízdy, návaznosti, zóny / km sloupce

| Feature | Business význam | Legacy: storage / reader | Máme? | Ve starém? |
|---|---|---|---|---|
| Časy jízdy ve StopListu (1 sloupec) | Minuty jízdy od vývěskové zastávky | trasa spoje „W" | ✅ 1:1 (`ZakladniCisloTrasy`+`CasJizdyZMapy`) | ✅ ověřeno 0340303 |
| Časy jízdy — multi-varianta (TrasaMin1-15) | Víc časových sloupců per varianta + `<` u projetých | `gSetLinPrt` dta74 / `PATCH,"mhd","par",2` | ⬜ (1 sloupec; chce zásah do C# rendereru) | 📄 |
| Tisk ručních názvů zastávek | Použít ruční název | `gSetLinPrt` dta18 / `PATCH,"nazMHD"` | ✅ (`^JRZ` $7) | ✅ |
| Zóny vs km sloupce | „Netisknout zónu, tisknout km" apod. | `gSetLinPrt` dta20 / `PATCH,"linIDS"` (0-3) | 🟡 zóny ✅, km sloupce ⬜ | 📄 (zóny ✅) |
| Km sloupce — max / filtr | Redukce počtu km sloupců | `gSetLinPrt` dta33-34 / `PATCH,"Kms",…` | ⬜ | 📄 |
| Zóny IDS — bez písmen (JDF) | Formát zón | `gSetLinPrt` dta77 / `PATCH,"ZonaIdsDelZnk"` | 🟡 ❓ | 📄 |
| Návaznosti / přípoje | Přípojové spoje v uzlech | spoj `("T",sp,32)` | ⬜ **nečteme** | ✅ struktura ověřena |
| Způsob výpisu návazností | Dole vs. v tabulce; seskupit pro TČ | `gSetLinPrt` dta78 / `PATCH,"NavaznostiZpusobVypisu"` | ⬜ | 📄 |
| Skrýt prázdné přípojové řádky | Šetřit místo když přípoj jen PD/víkend | — | ⬜ → **#11300** | ❓ (legacy fička) |
| Uspořádat poznámky dle délky | Layout legendy | `gSetLinPrt` dta26 / `PATCH,"Trid_delK"` | ⬜ | 📄 |
| Negativní značky na nový řádek | Layout legendy | `gSetLinPrt` dta27 / `PATCH,"NegZnNR"` | ⬜ | 📄 |
| Cizojazyčné poznámky | Vícejazyčné pozn. | `gSetLinPrt` dta28 / `PATCH,"prtCzjPzn"` | ⬜ | 📄 |
| Rozdělení linky v zadaném TČ | Dlouhá linka na víc tabulek | `gSetLinPrt` dta1 / `PATCH,"ZLOM"` | ⬜ | 📄 |
| Odsazení/odražení zastávky per směr | Vizuální odsazení jen v jednom směru | (legacy „dta 10"?) | ⬜ infra v TD, data flow ne → **#7937** | ❓ |

---

## 8. Vzhled / Win tisk (dialog „Nastavení pro tisk linky") — většinou mimo TD

`gSetLinPrt` má 82 polí; velkou část přebírá **šablona v TemplateDesigneru** nebo se týkají **starého
Win/kniha enginu**, který nový TD nahrazuje. Kompletní výčet všech 82 polí je v **Příloze A** (§11).
Shrnutí kategorií:

- **Vizuál vývěsky** (měřítko, okraje, fonty, šíře sloupců — dta 11,13,15,16) → ➖ řeší šablona TD.
- **Kniha** (dta 7-10, 12, 14, 17) → ➖ jiný typ JŘ, ne zastávkový.
- **Win tisk A4/A5** (dta 37-72, 75-76, 79-82) → ➖ starý tiskový engine.
- **HTML / odbavení / TELMAX / mikroelektronika** → ➖ mimo tisk zastávkového JŘ.

---

## 9. Souhrn — co bereme a co ne

### ✅ Čteme a odpovídá legacy
Lokální označení / titulek / informace (`SeT_MhD`), platnost (`Plati`), FTL rozvržení (`MHD_TYP_R`),
gate auto-poznámek (`mhd,"not"`), marker sada (`mhd,"notchar"`), bezbariér (`TZPA,"BBP"`), rok v
omezeních (`TiskDtm`), ruční názvy zastávek (`nazMHD` → `^JRZ` $7), dopravce (`^JRSET("DOP")`), časy
jízdy (1 sloupec), zóny, datumová omezení, rozdíly tras, na znamení, multi-page.

### 🟡 Čteme s omezením / dormant
NZ prázdninové dělení (`MHD_NZ1_NZ2`/`MHD_NENZ1_NENZ2` — kód hotový, data prázdná), QR / poznámka pod
dopravce (závisí na šabloně), zóny vs km (`linIDS` — zóny ano, km ne), časové průběhy (`mhd,"par"` —
1 sloupec).

### ✅ Tab MHD — ruční poznámky (HOTOVO Fáze 1+2, 2026-06-20)
- Komentáře (`SeT_SpeC`+`MhD_SpeC`), Spoje-nepoznámkovat (`MHDSPNPZN`), směrové poznámky (`SeT_MhD`),
  #7955 přepínač co schovat v rozsahu (`SCHOVAT_V_ROZSAHU`).

### ⬜ Vůbec nečteme (zbývající doménové mezery)
- `MHD_NEZNZ123` (potlačení NZ v legendě per sloupec), `ISY_LinkyTiskMHDNadpisy` (override nadpisů)
- Text „spoj jede až do XY" (#7947), vypínače agreg. poznámek bezbariér (#7161 D6)
- Skrýt „x na znamení" když je sloupec hidden (#7161 D5 — řešit v TD)
- Návaznosti/přípoje + `NavaznostiZpusobVypisu` (#11300)
- Ruční poznámkové bloky `("C",1-4)` (tarifní/místenk./všeob./radiobus), uspořádání legendy
  (`Trid_delK`, `NegZnNR`), cizojazyčné (`prtCzjPzn`), rozdělení linky (`ZLOM`)
- Multi-varianta časů jízdy (`TrasaMin1-15`), km sloupce (`linIDS`/`Kms`), odsazení zastávky (#7937)
- Vizuál legendy (2 sloupce), umístění směrových poznámek (legacy nahoru), číslování omezení (TYPPOZ=1)

### ➖ Mimo agendu (řeší šablona TD nebo starý engine)
Měřítka, okraje, fonty, šíře, celý Win tisk A4/A5, kniha, HTML, odbavení/TELMAX/mikroelektronika.

---

## 10. Prioritizace mezer

### 🔴 Vysoká (přímý dopad, focus „dolní část / poznámky")
1. **Ruční komentáře linky + „nepoznámkovat" (§5)** — největší gap (#7955/#7161). Úložiště zmapováno,
   už není blokované. Číst grid → přiřadit poznámku ke spojům → potlačit auto-rozdíl u spojů s vlastním komentářem.
2. **Text „spoj jede až do XY" (§4)** — #7947, symetrické rozšíření `RozdilyTras`. Malé.
3. **Vypnutí agreg. poznámky bezbariér (§6)** + **skrytí „x" dle viditelnosti sloupce** — #7161
   (8.5 patří do TD, 8.4 parametr tisku).

### 🟡 Střední (pro úplné 1:1, bez akutního tiketu)
4. `MHD_NEZNZ123` (§3) — potlačení NZ v legendě per sloupec.
5. Override nadpisů sloupců `ISY_LinkyTiskMHDNadpisy` (§3).
6. Návaznosti/přípoje (§7) — #11300; větší práce (nová struktura + element).
7. Multi-varianta časů jízdy TrasaMin1-15 (§7) — zásah do rendereru.

### 🟢 Nízká / dormant
8. Prázdninové dělení NZ (§3) — kód hotový, dormant (chybí data).
9. TYPPOZ=1 číslování, týdenní platnosti NCOME (§4) — bez test firmy.
10. Km sloupce, odsazení zastávky, IDS číslo, uspořádání legendy (§7).

---

## 11. Příloha A — kompletní `gSetLinPrt` (82 polí dialogu „Nastavení pro tisk linky")

Plný výčet z `JRapiLin1.mac:183-372`. Sloupec „Zast. JŘ?" = relevance pro vývěsní/zastávkový výstup
(ano = ovlivňuje náš PDF; ne/vizuál = mimo). „Bereme?" dle §1 legendy.

| dta | Význam | PATCH klíč | Zast. JŘ? | Bereme? |
|---|---|---|---|---|
| 1 | Rozdělení linky v zadaném TČ | `ZLOM` | ano | ⬜ |
| 2 | Nestandardní výška JŘ | `VERTIM` | vizuál | ➖ |
| 3-4 | Vývěska — počet spojů tam/zpět | `No_sp_jR,"TaM"/"ZpeT"` | ano | ⬜ |
| 5-6 | Vývěska — vkládat prázdné sloupce tam/zpět | `No_sp_jR,"prSlp…"` | ano | ⬜ |
| 7-10 | Kniha — počet spojů / prázdné sloupce | `No_sp_jR_K,…` | ne (kniha) | ➖ |
| 11 | Vývěska — měřítko x~y | `SCALE,"VYVESY"` | vizuál | ➖ |
| 12 | Kniha — měřítko | `SCALE,"LINKY"` | ne | ➖ |
| 13 | Vývěska — okraje l~p~h~d | `PSVMargin` | vizuál | ➖ |
| 14 | Kniha — okraje | `PSKMargin` | ne | ➖ |
| 15 | Vývěska — fonty hh~mm | `PSVFont` | vizuál | ➖ |
| 16 | Vývěska — šíře zastávek | `PSVSIZA` | vizuál | ➖ |
| 17 | Kniha — šíře zastávek | `PSKSIZA` | ne | ➖ |
| 18 | Linka MHD — tisk ručních názvů zastávek (0/1) | `nazMHD` | ano | ✅ |
| 19 | Tisk 3místný~vlastníČíslo~tablo | `cisIDS` | ano | ⬜ |
| 20 | Tisk zóny IDS a KMS (0 jen km / 1 jen zóny / 2 obojí / 3 nic) | `linIDS` | ano | 🟡 |
| 21 | Dělená linka, tisk na jednu stranu | `ONEPAG` | ano | ⬜ |
| 22 | Dělená linka, hlava a pozn. pro směry | `Hlava&pozN` | ano | ⬜ |
| 23 | Dělená linka, tisk směru na stranu | `Smer_1_stranA` | ano | ⬜ |
| 24 | Posun podtržení pro opačný směr | `Podtr_Z_uP` | vizuál | ➖ |
| 25 | Tisk km pro jedn. směry | `PrintKmsSmerNo` | ano | ⬜ |
| 26 | Uspořádat poznámky podle délky | `Trid_delK` | ano (legenda) | ⬜ |
| 27 | Negativní značky na nový řádek | `NegZnNR` | ano (legenda) | ⬜ |
| 28 | Tisk cizojazyčných pozn. na ne-mezinár. linku | `prtCzjPzn` | ano | ⬜ |
| 29 | HTML — tisk km sloupců | `HtmlKmsNe` | ne (html) | ➖ |
| 30 | MHD — poznámkovat odchylky v trase (gate) | `mhd,"not"` | ano | ✅ |
| 31 | MHD lokální název u zastávky | `mhd,"ln"` | ano | ⬜ |
| 32 | MHD — volná písmena pro odchylky | `mhd,"notchar"` | ano | ✅ |
| 33-34 | Km sloupce — max počet / filtr | `Kms,"Filtr"/"MaxFiltr"` | ano | ⬜ |
| 35 | Tisk bezbariér (0 nenast./1 nad spoj/2 v pozn.) | `TZPA,"BBP"` | ano | ✅ |
| 36 | Tisk roku v omezeních (0-4) | `TiskDtm` | ano | ✅ |
| 37-72 | Win tisk A4/A5 (šablony, fonty, šíře, druhy poznámek) | `prt,"win",1..36` | ne (win) | ➖ |
| 73 | MHD — auto generovat časové průběhy tras | `mhd,"par",1` | ano | 🟡 |
| 74 | MHD — volné značky pro minutové trasy spojů | `mhd,"par",2` | ano | ⬜ |
| 75-76 | Win tisk MHD šablona A4/A5 | `prt,"winmhd",1/2` | ne | ➖ |
| 77 | Odstranění písmen ze zóny IDS (JDF) | `ZonaIdsDelZnk` | ano | 🟡 |
| 78 | Způsob výpisu návazností (0 seskupit / 1 samostatně) | `NavaznostiZpusobVypisu` | ano | ⬜ |
| 79 | Win — netisknout sloupec TČ | `netisknoutSloupecTC` | ne (win) | ➖ |
| 80 | Win — způsob podtržení (3 čáry / 1 čára) | `podtrzeniPaJ` | ne (win) | ➖ |
| 81 | Win — barevný tisk spojů | `barevnyTiskSpoju` | ne (win) | ➖ |
| 82 | Win — jen TČ kde spoje zastavují | `tisknoutJenTcKdeSpojeZastavuji` | ne (win) | ➖ |

---

## 12. Příloha B — reference souborů a labelů

| Soubor (`C:\DEV\EDISON\JRMAIN\CACHE\`) | Label | Řádky | Role |
|---|---|---|---|
| `JRapiLin.mac` | `gPrpLin` / `sPrpLin` | 282 / ~440 | vlastnosti linky + tab MHD-další (FTL/NZ) |
| `JRapiLin1.mac` | `gPrpLinMhd` / `sPrpLinMhd` | 82 / 133 | tab MHD (komentáře, nepoznámkovat, směrové pozn.) |
| `JRapiLin1.mac` | `gSetLinPrt` / `sSetLinPrt` | 183 / 374 | dialog Nastavení tisku (82 polí) |
| `JRapiLinPrtMhd1.mac` | (print engine) | 36-45, 497-565 | čtení FTL/NZ/nadpisů při tisku |
| `JRapiLinPrtMhd.mac` | `OmTrasa`, `gDopMHD`, `UVOD` | — | rozdíly tras, dopravce, atributy |
| `JRapiNZUtl.mac` | `TODDO`, texty NZ | — | omezení / negativní značky |
| `ALVA.TISKMHD.Data` (TiskMHD repo) | `LoadData` | — | jen výstupní formátovač — **žádné nastavení linky nečte** |

---

## 13. Shrnutí pro školení

- **Umíme dnes 1:1** (ověřeno proti legacy gold oraclu): hlavička + dopravce (vč. adresy), FTL 0-3
  sloupce, datumová omezení jede/nejede, rozdíly tras (A/B/C), na znamení, bezbariér (H/F), časy
  jízdy (1 sloupec), zóny, multi-page, číslování poznámek.
- **Navíc oproti legacy:** WYSIWYG editor, per-column styling, CellStyle kaskáda, multi-data PDF
  export, VisibilityExpression, AI Vision import.
- **Vědomě ještě neumíme:** ruční komentáře linky + „nepoznámkovat" (§5), „jede až do" (§4),
  vypínače agregovaných poznámek (§6), návaznosti/přípoje (§7), multi-varianta časů (§7),
  `MHD_NEZNZ123` + override nadpisů (§3).
- **Upřímná pozice:** linkové nastavení tisku, které není čistě vizuální, čteme z `^JRL` PATCH;
  ruční ad-hoc poznámky (tab MHD Komentáře/nepoznámkovat) jsou hlavní zbývající doménová mezera —
  a od teď víme přesně, kde je legacy čte.
