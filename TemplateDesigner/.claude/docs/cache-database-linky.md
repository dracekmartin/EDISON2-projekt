# Linková JŘ data (JRMAIN / `^JRL`) — mentální model pro reimplementaci

Mapa legacy datového modelu **linkového / meziměstského** jízdního řádu, analogie k MHD
`cache-database.md`. Slouží jako podklad pro reimplementaci agendy `ZastavkoveJrLinky`
(PRIORITA 2 — zastávkový JŘ pro linkové JŘ data).

> **Stav 2026-06-02 — ČÁSTEČNĚ OVĚŘENO proti reálným datům.** Strukturu jsem odvodil z analýzy
> legacy generátoru `ALVA.TISKMHD.Data` + routin `JRapi*.mac` (projekt JRMAIN). Architektura
> (2 vrstvy) a výstupní sekce jsou ověřené přímo v kódu. Strukturu globálů jsem **ověřil přes
> `cache_zwglobal` na reálné lince `CB_GWBUS` / `^JRL(1,"0320113")`** (Linka IDS JK 113,
> ČB–Kolný–Lišov) — viz §8 Ověřené nálezy. **Data namespace = IdFa = `CB_GWBUS`.** FTL/Plk/CNZ
> grid logiku (§4) se mi zatím nepodařilo ověřit — tahle linka grid nepoužívá (`MHD_TYP_R`
> chybí); je potřeba najít linku s MHD-mřížkou. Zbývající otevřené otázky viz §6.

---

## 1. Architektura legacy — dvě vrstvy, ne jedna

`ALVA.TISKMHD.Data.LoadData` (`C:/DEV/EDISON/TiskMHD/CACHE/ALVA/TISKMHD/Data.cls`) je
**sdílený formátovač výstupu**, který je data-source agnostický. Na řádcích 147–152 se
přes `VolanoZ` vybírá **producent dat**, který plní jednotný lokální global:

```objectscript
if (VolanoZ = "JRMAIN") {                                          // linková / meziměstská data
  s err=$$WinTiskMhd^JRapi(IdFa, db, SkupinaId, LinkaId, glb, .arri, .out)
}                                                                  // → JRapiExp10.mac:77
if (VolanoZ = "MHD") {                                             // MHD data (už máme v TD)
  s err='##class(ALVA.MHD.LinkyPrintFormatMHD).MakeData(IdFa, db, LinkaId, glb, .arri, .out)
}
```

Oba producenti plní **stejný lokální global** (`glb`):

| Uzel | Obsah |
|---|---|
| `@glb@(PageNumber,"key",1,"dta",N)` | hlavičková skalární pole (N=1..23) |
| `@glb@(PageNumber,"key",2,iCounter,"dta",N)` | řádky zastávek (N=1..27) |
| `@glb@(PageNumber,"key",3,"arr",sloupec,...)` | minuty odjezdů per sloupec (typ provozu) |
| `@glb@(PageNumber,"key",4,"arr",...)` | poznámky (typ 0=text / 1=NZ / 2=spec. znak) |
| `@glb@(PageNumber,"key",5,"arr",...)` | osa hodin |

`LoadData` ho pak čte jednotně a emituje výstupní sekce (`SEKCE_START`/`SEKCE_END`):

| OUT sekce | Odpovídá TD elementu |
|---|---|
| `stDBField` (POPIS_LINKY, CISLO_ZASTAVKA, …) | TextElement placeholdery |
| `stSekceSeznamZastavek` (NazevZastavky, Casy, Zony, Prubehy, ZakladniTrasa, Pracovni2-5, TrasaMin1-15) | **StopListElement** |
| `stHodinaOdjezdu` | osa hodin v DepartureGrid |
| `stMinutyOdjezdu` SMO1-4 | **DepartureGridElement** (1 SMO = 1 sloupec / typ provozu) |
| `stPoznamky` | **NotesLegendElement** |

**Klíčový závěr:** výstupní kontrakt = přesně to, co už produkuje TD `ZastavkoveJrMhd`
(StopList + DepartureGrid + NotesLegend). Reimplementace linkové agendy = napsat `GetData`,
která čte `^JRL`/`^JRZ`/`^JRSET` a emituje stejný JSON DataContext.

**Pozn.:** náš MHD `ZastavkoveJrMhd` nahradil **oba** legacy díly (producent
`LinkyPrintFormatMHD.MakeData` + formátovač `LoadData`) jednou `GetData` → JSON. Linková
agenda jde stejnou cestou.

---

## 2. Datové zdroje — globály (JRMAIN model)

Přístup: `^[nmsp]GLOBAL(...)`. První dva subscripty linkových dat: `sku` (skupina linek)
+ `lin` (číslo linky, normalizované na 7 znaků zleva nulami).

> **⚠️ POTVRZENO (Martin, 2026-06-02): `^JRL` NENÍ subscriptované IdFa.** Segregace dat linkových
> JŘ je **čistě per namespace = per firma** (`^["KV_DP"]JRL` = KV_DP data, `^["CB_GWBUS"]JRL` = GW).
> Pro čtení JRL stačí **`dataDB` (namespace)**, IdFa se nepoužívá. (Na rozdíl od MHD, kde
> `^[dataDB]ALVA.MHD.LSO(IdFa,...)` IdFa subscript MÁ.) IdFa == název namespace (KV_DP/CB_GWBUS).
> Jedna firma může mít **více skupin linek (`sku`)** — agenda zatím bere první skupinu (TODO: iterovat všechny).
> Pozn.: TD běží ve firmě dle `templatedesigner.appsettings.json` (aktuálně KV_DP) → GetData/GetMockData
> čtou KV_DP. Hardcoded CB_GWBUS linka v jiné firmě neexistuje → fallback na mock.

### `^JRL(sku,lin,...)` — linka, trasa, spoje

| Subscript | Obsah |
|---|---|
| `("I")` / `("I",cnt)` | typ linky (#1\10) / název linky (více částí) |
| `("C",typ,cnt)` | poznámky (1=tarifní, 2=místenkovací, 3=všeobecné, 4=radiobus) |
| `("J",tc)` | zastávka na trase (tc = tarifní číslo); `("J",tc,tcd)` = km tarif:přesné:odbav |
| `("T",sp)` / `("Z",sp)` | spoj směr Tam / Zpět (sp = číslo spoje) |
| `("T",sp,omzId)` | omezení spoje (datumová, výluky, návaznosti) |
| `("W",wId)` | trasa spoje (časy/km/zóny per zastávka; wId = pole "CisloTrasy" ze spoje) |
| `("PATCH",...)` | metadata linky + nastavení tisku (viz §3) |

**Spoj `("T"/"Z",sp)`** — klíčová pole (`#`-oddělené sekce, `:`-oddělená pole):
- TcOd, TcDo (počáteční/koncová zastávka), CisloTrasy (→ `"W"`), Odjezd (min od půlnoci), Doba
- **Plk** = tarifní platnost (řetězec znaků `1`-`7`,`X`,`+`) — viz §4
- **CNZ** = negativní značka (dynamická, text nebo prázdno) — viz §4
- dopravci, barva spoje (RGB), atributy (bezbariérovost, kola, WiFi…), návaznosti

**Trasa spoje `("W")`** — per zastávka: čas od počátku, čekání, tarifní/přesné km, zóna,
nástupiště, atributy. (Analogie MHD `MakeTableSpoj`.)

### `^JRZ(czst,...)` — zastávky

`czst` = číslo zastávky. Hlavní uzel je `$`-delimitovaný řetězec (~46+ polí): názvy
(obec/část/lokalita), názvy pro odbavovací zařízení, atributy, zóny IDS, GPS, příznaky.
**Časové varianty:** `^JRZ(czst,"CasoveVariantyUdaju",horolog)` — vybírá se poslední
varianta platná k datu (horolog = dny od 31.12.1840). Analogie MHD `ZastavkyFce.GetNazevHrl`.

### `^JRSET(...)` — číselníky a nastavení

| Uzel | Obsah |
|---|---|
| `("Definice","ZON",id)` | zóny IDS (popis, sousedi, oblast, městská zóna…) |
| `("Definice","WinMHD",rec,"DSC")` | **nadpisy sloupců** (Pracovní dny / Soboty / Neděle… per FTL) `rec`=CZ/*SK |
| `("Definice","WinMHD","prtset")` | nastavení tisku MHD (šipka, barevné schéma, oddělovač minut) |
| `("Cis","InPlatDef",sku,nz)` | negativní značky — popis, typ platnosti (DPL), text pro legendu, datumová omezení |
| `("Cis","InPlatODDO")` | rozsah znaků pro přidělování NZ |
| `("NCOME",rec,znk)` | texty platností pro znaky `1`-`7`,`+` (do legendy) |

---

## 3. PATCH metadata — řízení tisku linky

`^JRL(sku,lin,"PATCH",klíč)`. Klíčové pro odvození sloupců:

| PATCH klíč | Význam |
|---|---|
| `MHD_TYP_R` | **FTL mód** (0-3) — kolik sloupců a jaké (viz §4) |
| `MHD_NZ1_NZ2` | NZ pro prázdninové dělení: `NZ_oПrázdninách ~ NZ_mimoPrázdniny` |
| `MHD_NENZ1_NENZ2` | NZ pro **skrytí** spoje v daném sloupci (negativní filtr) |
| `MHD_NEZNZ123` | NZ k nevypisování (legenda) per sloupec |
| `linJEDNOSMERNA` | jednosměrná linka (jen jeden směr) |
| `ISY_LinkyTiskMHDNadpisy` | override nadpisů sloupců z `^JRSET("LinkyTiskMHDNadpisy",…)` |
| `nazMHD`, fonty, okraje, měřítka, barevný tisk | prezentační — pro TD řeší šablona, ne agenda |

---

## 4. ⭐ Odvození sloupců (typů provozu) — FTL + Plk + CNZ

**Toto je hlavní rozdíl oproti MHD.** MHD má `TypProvozuID` jako přímou entitu →
1 sloupec DepartureGridu. Linková verze sloupce **počítá** z kombinace FTL módu + Plk
(tarifní platnost spoje) + CNZ (negativní značka). Zdroj: `JRapiLinPrtMhd1.mac` (label
přiřazení ~ř. 18-300, nadpisy ~ř. 496-565).

### FTL módy (`MHD_TYP_R`)

| FTL | Sloupce |
|---|---|
| **0** | 3: Pracovní dny (1-5,X) / Soboty (6) / Neděle+Svátky (7,+) |
| **1** | 3: Prac. dny mimo prázdniny / Prac. dny o prázdninách / So+Ne+Svátky — dělení přes NZ |
| **2** | 2: Pracovní dny / So+Ne+Svátky |
| **3** | 4: Prac. dny mimo prázdniny / o prázdninách / Soboty / Neděle+Svátky |

### Plk — tarifní platnost spoje

Řetězec znaků: `1`-`5` = po–pá, `6` = sobota, `7` = neděle, `X` = denně, `+` = svátky,
`""`/`.` = bez specifikace (→ všechny sloupce). Test přes operátor `[` (obsahuje).

### Algoritmus (pseudokód, zjednodušený)

```
KamPrt = bitmapa sloupců ("1~0~1" = sloupec 1 a 3)

FTL=0:
  Plk prázdné/"."        → všechny sloupce
  Plk obsahuje 1-5 | X   → sloupec 1
  Plk obsahuje 6         → sloupec 2
  Plk obsahuje 7 | +     → sloupec 3

FTL=1 / FTL=3 (prázdninové dělení řízené NZ):
  NZFTL1   = MHD_NZ1_NZ2 ~[2]    (mimo prázdniny)
  NZFTL2   = MHD_NZ1_NZ2 ~[1]    (o prázdninách)
  NENZFTL1 = MHD_NENZ1_NENZ2 ~[2]  (skrýt ze sloupce 1)
  NENZFTL2 = MHD_NENZ1_NENZ2 ~[1]  (skrýt ze sloupce 2)

  prac. dny:
    CNZ = NZFTL1           → jen sloupec 1
    CNZ = NZFTL2           → jen sloupec 2
    jinak                  → sloupce 1+2, pak:
      CNZ ∈ NENZFTL1       → vyřadit ze sloupce 1
      CNZ ∈ NENZFTL2       → vyřadit ze sloupce 2
  víkend (6/7/+)           → sloupec 3 (FTL=1) / sloupce 3,4 (FTL=3)
```

**Pro reimplementaci nejdůležitější:** rozdíl NZFTL (pozitivní = patří do sloupce) vs
NENZFTL (negativní = skrýt ze sloupce). Tohle ověřit proti reálné lince s prázdninovým JŘ.

### Rozdílové trasy (Pracovni2-5, TrasaMin1-15)

Když spoje ve stejném sloupci mají **různý časový průběh** (jiné mezizastávkové časy),
generují se dodatečné minutové sloupce s označením A/B/C. Detekce přes bitmapu časů
(`prutmbmp`) — shodné bitmapy = stejná trasa → reuse. (Analogie MHD alg. 1002 rozdílové
trasy, ale tady přes časové průběhy, ne bitmapy obsluhy.)

### Anotace u odjezdů + legenda

Speciální znaky a NZ se generují jako horní indexy u minut; texty se sbírají do legendy
(`stPoznamky`, typ 0/1/2). NZ se filtruje per sloupec (`noNZ2` — vyřadí NZ, která není
v žádném tištěném sloupci).

---

## 5. Read-helpery (analogie MHD `*Fce` tříd)

Pro čistou reimplementaci je otázka, zda číst `^JRL`/`^JRZ` přímo, nebo přes tyto helpery
(viz otevřené otázky). Co poskytují:

| Helper (`$$label^routina`) | Soubor (~ř.) | Vrací |
|---|---|---|
| `gPrpLin^JRapiLin` | JRapiLin.mac:282 | vlastnosti linky (název, typ, QR, platnost, MHD metadata) |
| `gSetLinPrt^JRapiLin1` | JRapiLin1.mac:183 | nastavení tisku (`PATCH` dta 1-82) |
| `gTrsLin^JRapiLin` | JRapiLin.mac:650 | trasa linky (zastávky s TČ, km, atributy) |
| `lSpLin^JRapiSp` | JRapiSp.mac:6 | seznam spojů per směr |
| `gSpLin^JRapiSp` | JRapiSp.mac:25 | data spoje + trasa spoje + omezení |
| `gZst^JRapiZst` | JRapiZst.mac:45 | data zastávky (~59 polí, časová varianta) |
| `gComNZLiSp` / `gComNZPLiSp^JRapiNZUtl2` | JRapiNZUtl2.mac:110/592 | text negativní značky / písmenka |

---

## 6. Otevřené otázky (ověřit před implementací)

- ❓ **Číst `^JRL` přímo, nebo přes JRapi helpery?** MHD jsme reimplementovali přímo nad
  globály + jemné `Fce` třídy. JRMAIN helpery jsou složitější (working global `^CacheTempJB`).
  Pro čistou reimplementaci pravděpodobně číst přímo `^JRL`/`^JRZ` a logiku sloupců napsat
  nově — ale potvrdit na reálné lince, že rozumíme formátu vět.
- ❓ **FTL + Plk + CNZ algoritmus** — STÁLE OTEVŘENÉ: ověřit na reálné lince s MHD-mřížkou
  (`MHD_TYP_R` set, ideálně prázdninový JŘ FTL=1/3), zejména NENZ filtraci. CB_GWBUS/0320113
  grid nepoužívá. Potřebujeme najít zákazníka/linku s minutovou mřížkou.
- ✅ **`^JRL` struktura** — OVĚŘENO na CB_GWBUS/0320113 (viz §8). dta pozice spoje, trasa, "W",
  JRZ a PATCH potvrzeny.
- ❓ **Skupina linek (`sku`)** — jak se vybírá/předává (analogie MHD SkupinaLinekID v InstancePickeru)?
  Na CB_GWBUS je `sku=1`. Zjistit, jestli existuje víc skupin a jak se mapují.
- ❓ **Vícezónový tarif + km sloupec** — `KmsSmer,T/Z` ověřeno (km per směr, `-1`=nestaví);
  doladit přesné formátování zón (km informativní, jezdí se podle zón).
- ✅ **Návaznosti** — uloženy v `("T",sp,32)` (`m@…`); `NavaznostiZpusobVypisu` v PATCH řídí
  dole vs v tabulce. Potvrzeno na T,5,32 a T,11,32.

---

## 7. Mapování na TD JSON DataContext (cíl reimplementace)

`GetData` nové agendy emituje stejný JSON jako `ZastavkoveJrMhd` (viz `cache-api.md`
GetData wire formát) — 1 stránka = 1 zastávka × směr:

```
values: { CISLO_LINKY, NAZEV, POPIS_DOPRAVCE, DATUM_PLATNOSTI_OD/DO, TITULEK, ... }   ← stDBField
rows: {
  ZASTAVKY:  [ {POPIS, TC, zóny, atributy, je-aktuální}, ... ]      ← StopList
  ODJEZDY:   [ {sloupec/typ-provozu, hodina, minuta, anotace}, ... ] ← DepartureGrid
  POZNAMKY:  [ {typ, znak, text}, ... ]                             ← NotesLegend
}
```

---

## 8. Ověřené nálezy (CB_GWBUS / `^JRL(1,"0320113")`, 2026-06-02)

Ověřeno přes `cache_zwglobal` na reálné lince. Co se potvrdilo a co je potřeba upravit oproti
původní odvozené mapě:

### Skupina + linka
- `^JRL(sku)` — `sku` je **skupina linek** (číslo), uzel obsahuje hlavičku skupiny (`JŘ_2025~~~0~~1~0`).
- `lin` = **7-znakové číslo** (`"0320113"`).
- `("I")` = `0#CB`, `("I",1..N)` = názvové části (`České Budějovice`/`Kolný`/`Lišov`), `("I",0,2)` = mapování spojek názvu (`1:2:3`).

### Trasa linky `("J")`
- `("J")` = počet zastávek (`26`).
- `("J",poradi)` = `czst:atributy##…:…` — **první `:`-pole = czst** (číslo zastávky → `^JRZ`). `poradi` = 1..N.
- `("J",poradi,tcd)` / `…,"bd",idx` = km varianty + detailní body (fiktivní zóna `5~…~zona^km`).

### Spoje `("T")` / `("Z")` ⭐ (oprava mapy)
- `("T")` = **počet spojů** ve směru (header), `("T",sp)` = věta spoje.
- **`sp` jsou libovolná ID, NE 1..N** — na téhle lince `3,5,7,9,11,13`. Vždy iterovat přes `$order`, nikdy nepředpokládat sekvenci. (`("T",1)` byl prázdný → původní probe selhal.)
- **Formát věty** = `#`-segmenty, první segment `:`-delimited:
  | poz | význam | příklad (T,3) |
  |---|---|---|
  | 1 | TcOd (počáteční zastávka, pořadí v trase) | `1` |
  | 2 | TcDo (koncová) | `26` |
  | 3 | **CisloTrasy** → `("W",N)` | `3` |
  | 4 | **Odjezd** (minuty od půlnoci) | `392` = 6:32 |
  | 5 | Doba jízdy (min) | `53` |
  | 7 | **Plk** (tarifní platnost) | `X` = denně |
  - barva spoje RGB (`0;0;0`) v pozdějším `#`-segmentu.
- **Omezení/návaznosti** = subnody `("T",sp,omzId)`:
  - `omzId=22` → **nejede** (`k@DD.MM.RRRR:DD.MM.RRRR#…` — rozsahy datumů)
  - `omzId=32` → **návaznost** (`m@…#linka:tc:popis:…`)

### Trasa spoje `("W",cisloTrasy)`
- `#`-segment per zastávka (pořadí 1..N), `:`-delimited: `poradi:čas:km:…`
  - poz 2 = **čas od počátku spoje** (min); poz 3 = km.
  - **`<` = spoj zde NESTAVÍ** (projíždí / zastávka mimo trasu spoje).
- **Odjezd v zastávce = `Odjezd spoje + W[zastávka].čas`** (např. stop 26: 392+53 = 445 = 7:25).

### Zastávka `^JRZ(czst)`
- Hlavní uzel `$`-delimited: `NORM_NAZEV$Název,,lokalita$BLO$…$czst$0$zónaIDS~0~$GPS_N$GPS_E$…`
  (ověřeno: czst `4842`, zóna IDS `101`, GPS `48.97/14.49`).
- `^JRZ(czst,1,linka)` = index linek obsluhujících zastávku.

### PATCH (ověřeno)
- `Plati = od~do`, `Head` (poznámka nad linkou), `OBJ` (typ JŘ), `linJEDNOSMERNA`, `ZonaIdsDelZnk`,
  `KmsSmer,T/Z,varianta` (km per směr — `-1` = nestaví), `StrPoz,sp,tc` (poznámky pro strojky),
  `prt,win,1..N` (Win tisk config), `TZPA,BBP` (bezbariérový tisk), `cis,1,alt` (alternativní cíle).

### ⭐ Dva formáty linkového JŘ
- **Klasický sloupcový** (tahle linka): **`MHD_TYP_R` chybí**, `MHD_NZ1_NZ2`/`MHD_NENZ1_NENZ2` prázdné.
  Spoje běží denně (Plk=X), varianty dnů řeší **datová omezení** (`,22` nejede). Tabulka =
  zastávky × spoje (sloupce), časy v buňkách. ← StopList-like, ne minutová mřížka.
- **MHD minutová mřížka** (linky s `MHD_TYP_R` 0-3): teprve tady platí FTL/Plk/CNZ odvození
  sloupců (§4) → DepartureGrid. **Pro tento formát zatím nemáme ověřenou reálnou linku.**

→ Nová agenda musí umět **oba formáty** (nebo začít sloupcovým, který je doložený, a grid přidat
později až najdeme reálná data).

---

## 9. Zlatá reference (legacy oracle) — reprodukovatelná na vyžádání

Cílový výstup zastávkového JŘ z linkových dat jde vygenerovat z legacy generátoru kdykoliv přes
`cache_query` — slouží jako **test oracle** pro reimplementaci (Fáze 3 = porovnat náš výstup s tímto):

```
cache_query  ALVA.TISKMHD.Data / LoadData
  idfa=CB_GWBUS  dataNamespace=CB_GWBUS
  parameters=["VOLANO_Z~JRMAIN", "SESTAVA~1", "VALUE~<sku>#<lin>#<page>#3#0-23#0##"]
```
`VALUE` = `SkupinaId#LinkaId#PageNumber#CasovaOsa#OsaOdDo#TiskProjetych#FileName#NeAtribut`.
PageNumber = pořadí (zastávka × směr); iteruj 1,2,3… dokud OUT není prázdný.

**Ověřeno na 0320113 / page 1** (zastávka 4842 ČB AN → Lišov). Výstup = `SEKCE_START`/`SEKCE_END`
bloky: `stDBField` (skaláry), `stSekceSeznamZastavek` (StopList — 22 zastávek, zóny, TrasaMin1-N
varianty s `<`=nestaví), `stHodinaOdjezdu` (osa hodin), `stMinutyOdjezdu~SMO1..4` (DepartureGrid
sloupce = skupiny dnů: Pracovní dny / Soboty / Neděle a svátky), `stPoznamky` (legenda).

**Nález k odvození sloupců:** na 0320113 (FTL default, Plk=X) všech 6 spojů spadlo do SMO1
„Pracovní dny" s anotacemi „nejede" (†32/†39/†70 z `,22` omezení), SMO2/SMO3 prázdné. Tj. zařazení
do sloupce není čistě „Plk znak → sloupec" — promítají se i datová omezení. Tuto logiku engine replikuje.

---

## 10. Stav implementace agendy `ZastavkoveJrAd` (2026-06-02)

Třída `ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd` (code `ZASTAVKOVY_JR_AD`, „Zastávkový JŘ (AD)").
Reimplementace legacy `ALVA.TISKMHD.Data` větve `VolanoZ=JRMAIN`. **Pozn.: TD běží ve firmě dle
`templatedesigner.appsettings.json` (testováno KV_DP, první linka 0410101 Cheb–Libá–Hazlov; ^JRL
je per-namespace, bez IdFa).**

### ✅ Hotovo a ověřeno (proti gold oracle `ALVA.TISKMHD.Data`)
- **GetSchema/GetMockData/GetParameters/GetTableTemplates** — schema sdílené s MHD; default LINKA_ID
  = první linka firmy (dynamicky). GetMockData = živý náhled první linky firmy (DEV — viz dev tweaky).
- **GetData fázový engine** čte `^JRL`/`^JRZ`:
  - Hlavička (CISLO_LINKY, POPIS_LINKY z "I", platnost z PATCH, aktuální zastávka/směr).
  - Trasa → StopList se **zónami** + **na znamení** (atribut `x`); **filtr projížděných** (`<` —
    jen zastávky kde staví ≥1 spoj, přes `ObsluhovanePoradi`). Ověřeno: 0410101 → 17 zastávek (=legacy).
  - **Odjezdy** = Odjezd spoje + offset z trasy spoje "W"; **multi-group** (`SkupinyZPlk` — spoj může
    být ve více skupinách dnů, Plk "6+" → soboty i neděle). Ověřeno proti legacy SMO1/2/3.
  - **Poznámky** = legenda z datumových omezení (omzId 11-15,22; `SestavTextOmezeni`+`PrefixOmezeni`
    +`FormatDatumyOmezeni`, 1:1 s `txtNZ`/`TODDO^JRapiNZUtl`) + na-znamení.
  - **Multi-page**: stránka per zastávka × směr (Tam vzestupně / Zpět sestupně k pořadí 1);
    prázdné konečné se skipnou. Ověřeno 0410101: 17 Tam + 20 Zpět.
- **Instance picker** (`GetAvailableInstances`) — seznam linek z ^JRL (první skupina); proklikávání
  stránek v preview. C# `InstanceParamName` AD→LINKA_ID; GetData přijímá i `INSTANCE_ID` alias.

### ✅ 1:1 drobnosti dokončené (2026-06-04 — 06-11, ověřeno proti legacy zdroji + datům 0410101)
1. **Názvy zastávek** — `NazevZastavky` bere `^JRZ` `$7` (odbav/MHD název s obcí, "Cheb,aut.nádraží",
   "Fr. Lázně,Krapice,Lužná"), fallback `$2`. Zahozeno `FormatNazev`/`FormatNazevPlny` (drop-obec).
   `NazevLinky` join `–`→`-` (legacy pomlčka).
2. **Datumy v poznámkách s rokem** — `FormatDatumyOmezeni` = 1:1 `TODDO^JRapiNZUtl`: verbatim s rokem,
   `:`→rozsah `D1-D2`, položky čárkou bez mezery, `k@`/`L@` prefix se stripuje. (`DenMesic` smazán.)
3. **"jede" omezení** — `SestavTextOmezeni` projde omzId 11,12,13,14,15,22, spojí do jedné poznámky;
   `PrefixOmezeni` = mapování (11 "jede jen ", 12 "jede také ", 13 "jede ", 14/15 su/li, 22 "nejede ").
4. **POPIS_DOPRAVCE** — `PopisDopravce` (distinct `fa:pr` z `#`-pole 2 spojů obou směrů, dedup názvů,
   "Zajišťuje: …") + `NazevDopravce` chain z `^JRSET("DOP")`: per-provoz `$6` → firma `$14` → `$7` → `$1`.
5. **LOKALNI_OZNACENI_LINKY + TITULEK + INFORMACE** — všechny z `^JRL(...,"MhD","SeT_MhD")` (delim `~`,
   1:1 `sTIT^JRapiLinPrtMhd1`): `$1` lokální označení (fallback `$E(Lin,5,7)`="101"), `$2` titulek
   (fallback "Městská hromadná doprava..."), `$5` informace (`<br>`→newline). Inline v hlavičce, `LokalniOznaceni` helper zrušen.
7. **Číslování poznámek** — pro KV_DP (TYPPOZ=0 = `^JRSET("Definice","MHDnewOM")` prázdné) legacy používá
   sekvenční čísla = moje 1/2/3 (schéma shodné, hodnoty 39/70/32 byly artefakt jiného seedu). Bez změny.
   TYPPOZ=1 (písmenka a/b/c) / NZ-symbol režim = firm-config feature, deferred (žádná test firma to nemá).
6. **FTL 1-3 + CNZ prázdninové dělení sloupců** — `ZarazeniSpoje(ftl,plk,cnz,nzCfg)` staví `KamPrt` bitmapu
   sloupců 1:1 s `JRapiLinPrtMhd1` ř.30-152 pro všechny 4 módy; `BitmapNaSkupiny` → comma-list indexů;
   `NacistSkupinyDnu(ftl)` vrací sloupce per mód (FTL0 Prac/So/Ne+sv; FTL1 Prac-mimo/Prac-o-prázd/So+Ne+sv;
   FTL2 Prac/So+Ne+sv; FTL3 4 sloupce). CNZ = `$p(spoj,"#",7)` (=gSpLin dta29); NZ config z `MHD_NZ1_NZ2`
   (NZFTL1 nejede / NZFTL2 jede o prázd) + `MHD_NENZ1_NENZ2` filtr. Guard: KamPrt=0 → spoj se netiskne.
   **Ověřeno** na 0425017/KV_DP (FTL=1): 3 sloupce se správnými popisky, odjezdy distribuované (bez NZ
   config jdou prac. spoje do obou pracovních sloupců = 1:1 legacy). FTL=2/3 implementováno per-legacy,
   ale v KV_DP není linka k ověření (jen FTL=1). FTL=0 beze změny chování (0410101 = identický výstup).

### ✅ Comprehensive test vs legacy (2026-06-11, page-by-page diff 0410101 proti `ALVA.TISKMHD.Data`)
Potvrzeno **1:1**: počet stránek (**35** = Tam 16 + Zpět 19), všechny skaláry (POPIS_LINKY/ZASTAVKA/SMER/_CIS,
LOKALNI=101, TITULEK fallback, platnost), StopList (názvy, pořadí, zóny), 3 sloupce dnů, **odjezdy
+ anotace** (markery na stejných časech), legenda. Doděláno z testu:
- **POPIS_DOPRAVCE 1:1** — `<název>, <sídlo provozovny>,,tel:<tel>` (když není zkrácený MHD název $6/$14);
  `NazevDopravce` rozšířeno. Ověřeno = legacy „Zajišťuje: DPKV, a.s., Sportovní…,,tel:353 504 518".
- **Počet stránek 35** — terminus check v `BuildStrankaZastavky`: board na konečné zastávce směru
  (za currentPoradi není obsluhovaná zastávka v servedPoradi) se netiskne (dřív 37 — 2 boardy navíc).
- **KRITICKÝ fix** — `GetData` padal `<SUBSCRIPT>` od commitu #1 (`NazevZastavkyCis(czst="")` → `^JRZ("")`);
  `GetMockData` to maskovalo (auto-pick vždy obsluhované poradí). Init `posledniCzst` + guard prázdného czst.
- **#2 anotace = falešný poplach** — markery na stejných časech jako legacy (subagent přečetl chybně).

### ✅ Atributy zastávek (2026-06-11)
`TextAtributu` = číselník `arrCis(16)^JRapiCis` (27 znaků); `JeViditelnyAtribut` filtruje významové
(`xyzNVALefgODCGabc` — na znamení, nástup/výstup, ikony dopravy autobus/vlak/metro/loď/letadlo, paragraf)
od formátovacích (T/p/P/J/o/S) a souhrnných (>/</E/H). StopList parsuje dta2 J věty → `AddAtribut` na
zastávku + sběr do legendy (`SestavPoznamky` s `atrSeen`). Ověřeno 0320144/CB_GWBUS (A=autobus, x=na znamení, L=vlak).

### Stav featur (2026-06-11, po scanu firem CB_GWBUS/KV_DP/CB_CSADAD/VS_CSAD)
- **Prázdninové dělení (FTL1/3 NZ split)** — kód hotový, **logika ověřena synteticky** (cnz=NZFTL1→jen
  „mimo prázdniny", cnz=NZFTL2→jen „o prázdninách"). `MHD_NZ1_NZ2` ale **prázdné ve všech firmách** → dormant.
- **Značky platnosti spojů (ZnkSp/NCOME)** — spoje používají jen `X`/`6+`/prázdné Plk (žádné denně-specifické)
  → v datech se nevyskytuje. NCOME registr = `^JRSET("NCOME",jazyk,N)` (jede v pondělí…). Neimplementováno (bez use-case).

### ✅ Rozdíly tras — HOTOVO (2026-06-11), ověřeno 1:1
1:1 reimplementace `OmTrasa^JRapiLinPrtMhd` (AD) — metody `RozdilyTras` + helpery `SpojBitmapa`,
`TrasaBitmapaZPole`, `NazZPoradi`, `GetZnk` v `ZastavkoveJrAd.cls`. Gated `AutoPozn` (`PATCH,"mhd","not"`).
- Vzorová trasa = základní trasa (J-věta `#10` dta12), fallback tištěná (`#7`), fallback union obsluhy.
- Per spoj porovná jeho bitmapu trasy se vzorovou → text **„neobsluhuje zast. X" / „jede přes…" /
  „jede jen do…" / „pokračuje přes…"** (1:1 legacy).
- Marker z písmenné sady **od „B"** (default `"BCEFGHIJOQRWeijqsvwxz"` nebo `PATCH,"mhd","notchar"`),
  počítáno **per zastávku**, dedup textů, zdvojování když dojdou písmena.
- **Ověřeno 1:1** na 0365001 CB_CSADAD: str.1 marker `B` = „neobsluhuje zast. Lesní hřbitov" (= gold),
  multi-stop „Hradišťská, Lesní hřbitov", druhý marker `C`. Anotace u minut + legenda sedí.
**Test linky:** CB_CSADAD `0345061` (FTL2+AutoPozn), `0365001`; VS_CSAD `0945001`, `0445102`.

### ⬜ Zbývá pro úplné 1:1 (zjištěno field-by-field diffem str.1 vs gold, 2026-06-11)
- ✅ **POPIS_DOPRAVCE — adresa provozovny DOPLNĚNA** (2026-06-12). `NazevDopravce` nyní čte i `$2`
  (ulice,číslo) z `^JRSET("DOP",fa,pr)` (gDopFaPr: $1 sídlo, $2 ulice, $3 tel) → slot mezi sídlem a
  telefonem. Ověřeno 1:1 na 0365001: `prov.Písek,Nádražní 2294,tel:601 319 673` = gold.
- ✅ **Filtr atributů zastávek — OPRAVENO** (2026-06-12). `JeViditelnyAtribut` zúžen na **jen „x" (na
  znamení)** — legacy `UVOD^JRapiLinPrtMhd` (ř.189-190) promítá z dta2 do StopListu jen „x", ostatní
  znaky (A=autobus, L=vlak, metro, nástup/výstup…) v tomto výstupu netiskne. A/L over-emise pryč.
- ⬜ **Atributy spoje** (např. bezbariér „72") — legacy je má u minut + v legendě (`3#72#spoj má
  bezbariérově přístupné vozidlo`). Máme jen atributy zastávek, ne atributy **spoje**.
  Zdroj: `inv` = `$p(hlava,":",15)` gated `isinv` (`^JRSET("PS","LIMES")` obsahuje „invalida"),
  `cbe` = `$p(hlava,":",22)` gated `iscbe` („invalidaspomoc"); `AllSpIsBBP` (všechny spoje bezbar.)
  mění text. **POZOR:** reprezentace markeru „72" jde přes output-layer (`$$i`→„72") = nejasná,
  ověřit ať se neopakuje MHD-vs-AD chyba u rozdílů tras. **Funkční mezera, střední.**
- ⬜ **Časy jízdy ve StopListu (TrasaMin)** — `CAS_JIZDY=0` u všech; legacy má kumulativní minuty
  (`0,1,2,4,6,8,10,13,…`) + varianty trasy (`<`). Nepočítáme. **Větší kus.**
- ⬜ Číslování omezení (TYPPOZ=1 pevná čísla „29" vs naše sekvenční „1") — firm-config, dřív odložené.
- ⬜ Šířka/layout legendy POZNAMKY — vizuál, na konec.

### 🔧 DEV tweaky k odstranění před produkcí (označené TODO v kódu)
- `MainWindow.xaml.cs` `#if DEBUG` Loaded handler → auto-otevře `kv_ad_test.json` (hardcoded cesta).
- `GetMockData` čte živě z ^JRL (první linka firmy) — zvážit lehký statický vzorek.

### 🛠️ Debug nástroj
- MCP `cache_zwglobal` (read-only `zw` globálu) — Caché `ALVA.EDISONAI.McpDebug.DumpGlobal` (EDISON repo)
  + C# tool v `EdisonCacheMcp/Tools/CacheTools.cs`. Gold oracle: `cache_query ALVA.TISKMHD.Data/LoadData`
  s `VOLANO_Z~JRMAIN` (viz §9). Kompilace .cls jde přes PostToolUse hook, ne přes MCP.
- `ALVA.EDISONAI.McpDebug.ScanSubscript` (read-only) — najde záznamy 2-úrovňového globálu s daným pod-subscriptem.
  Volá se přes `cache_query`, params `[globalRef, tail("~"-sep), ns, max]`. Použito pro hledání linek s FTL:
  `cache_query ScanSubscript ["JRL","PATCH~MHD_TYP_R","KV_DP","2000"]` → seznam `sku~lin = hodnota`.
