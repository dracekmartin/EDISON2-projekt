# AD zastávkový JŘ - datová mapa (globály, nastavení linky, poznámky)

> Sloučeno 2026-07-13 ze 3 dokumentů (cache-database-linky, ad-jr-nastaveni-linky-analyza,
> ad-jr-typy-poznamek-layout-analyza). Mapa KDE data jsou, ne status implementace.



<!-- ══════════════════════════════════════════════════════════════════════ -->
# ==== ČÁST: Caché databáze linek (^JRL…, §10 gold ověření) ====
<!-- ══════════════════════════════════════════════════════════════════════ -->

# Linková JŘ data (JRMAIN / `^JRL`) - mentální model pro reimplementaci

Mapa legacy datového modelu **linkového / meziměstského** jízdního řádu, analogie k MHD
`cache-database.md`. Slouží jako podklad pro reimplementaci agendy `ZastavkoveJrLinky`
(PRIORITA 2 - zastávkový JŘ pro linkové JŘ data).

> **Stav 2026-06-02 - ČÁSTEČNĚ OVĚŘENO proti reálným datům.** Strukturu jsem odvodil z analýzy
> legacy generátoru `ALVA.TISKMHD.Data` + routin `JRapi*.mac` (projekt JRMAIN). Architektura
> (2 vrstvy) a výstupní sekce jsou ověřené přímo v kódu. Strukturu globálů jsem **ověřil přes
> `cache_zwglobal` na reálné lince `CB_GWBUS` / `^JRL(1,"0320113")`** (Linka IDS JK 113,
> ČB–Kolný–Lišov) - viz §8 Ověřené nálezy. **Data namespace = IdFa = `CB_GWBUS`.** FTL/Plk/CNZ
> grid logiku (§4) se mi zatím nepodařilo ověřit - tahle linka grid nepoužívá (`MHD_TYP_R`
> chybí); je potřeba najít linku s MHD-mřížkou. Zbývající otevřené otázky viz §6.

---

## 1. Architektura legacy - dvě vrstvy, ne jedna

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

## 2. Datové zdroje - globály (JRMAIN model)

Přístup: `^[nmsp]GLOBAL(...)`. První dva subscripty linkových dat: `sku` (skupina linek)
+ `lin` (číslo linky, normalizované na 7 znaků zleva nulami).

> **⚠️ POTVRZENO (Martin, 2026-06-02): `^JRL` NENÍ subscriptované IdFa.** Segregace dat linkových
> JŘ je **čistě per namespace = per firma** (`^["KV_DP"]JRL` = KV_DP data, `^["CB_GWBUS"]JRL` = GW).
> Pro čtení JRL stačí **`dataDB` (namespace)**, IdFa se nepoužívá. (Na rozdíl od MHD, kde
> `^[dataDB]ALVA.MHD.LSO(IdFa,...)` IdFa subscript MÁ.) IdFa == název namespace (KV_DP/CB_GWBUS).
> Jedna firma může mít **více skupin linek (`sku`)** - agenda zatím bere první skupinu (TODO: iterovat všechny).
> Pozn.: TD běží ve firmě dle `templatedesigner.appsettings.json` (aktuálně KV_DP) → GetData/GetMockData
> čtou KV_DP. Hardcoded CB_GWBUS linka v jiné firmě neexistuje → fallback na mock.

### `^JRL(sku,lin,...)` - linka, trasa, spoje

| Subscript | Obsah |
|---|---|
| `("I")` / `("I",cnt)` | typ linky (#1\10) / název linky (více částí) |
| `("C",typ,cnt)` | poznámky (1=tarifní, 2=místenkovací, 3=všeobecné, 4=radiobus) |
| `("J",tc)` | zastávka na trase (tc = tarifní číslo); `("J",tc,tcd)` = km tarif:přesné:odbav |
| `("T",sp)` / `("Z",sp)` | spoj směr Tam / Zpět (sp = číslo spoje) |
| `("T",sp,omzId)` | omezení spoje (datumová, výluky, návaznosti) |
| `("W",wId)` | trasa spoje (časy/km/zóny per zastávka; wId = pole "CisloTrasy" ze spoje) |
| `("PATCH",...)` | metadata linky + nastavení tisku (viz §3) |

**Spoj `("T"/"Z",sp)`** - klíčová pole (`#`-oddělené sekce, `:`-oddělená pole):
- TcOd, TcDo (počáteční/koncová zastávka), CisloTrasy (→ `"W"`), Odjezd (min od půlnoci), Doba
- **Plk** = tarifní platnost (řetězec znaků `1`-`7`,`X`,`+`) - viz §4
- **CNZ** = negativní značka (dynamická, text nebo prázdno) - viz §4
- dopravci, barva spoje (RGB), atributy (bezbariérovost, kola, WiFi…), návaznosti

**Trasa spoje `("W")`** - per zastávka: čas od počátku, čekání, tarifní/přesné km, zóna,
nástupiště, atributy. (Analogie MHD `MakeTableSpoj`.)

**Atributy zastávky** (znaky číselníku 16 - `x` na znamení, `A`/`L` přestupy, `T`/`P`/`S`…) jsou na
**dvou místech a legacy čte obojí** (změřeno 30. 7.): v **trase linky** (J-věta `#1:2`, tedy per linka
a směr) **∪** v **registru zastávek** `^JRZ(czst)` `$`-pole **8** (per zastávka globálně). Firmy to mají
různě: `CB_CSADAD` má vše v trase a pole registru prázdné u všech 5728 zastávek, `CB_GWBUS` má „x"
naopak v registru. Čtení jen jednoho zdroje = chybějící vysvětlivka na vývěsce (bylo tak do 30. 7.,
viz `AtributyZastavky`). V registru se vyskytují jen znaky z číselníku, není to kategorie zastávky.

**Sloupek** (KOVED #13088, ověřeno 3. 8.) je **jiný údaj než označník** a v datech leží vedle něj:
J-věta segment **#12** (`sloupekTam:sloupekZpet`, legacy `dta14` - `JRapiLin` ř.700), per spoj
**W pozice 13** (legacy `trs(14)` - `JRapiSp` ř.311/349), spoj přebíjí trasu (`JRapiExp6` ř.1961-1962,
`JRapiExp10` ř.832-836). Hodnota je **odkaz do číselníku sloupků zastávky**
`^JRZ(czst,"Sloupek",id)` = `číslo ~ GPS lat ~ GPS lon ~ popis směru` („směr Brno přes UH"); číslo
1-255, legacy před exportem do CIS kontroluje číselnost (`+sloupek=sloupek`). ⚠️ **Vyplněnost záleží
na firmě**: ZL_KOVED ho má (linka 822995, Babice czst 204 = `1:2`, oba sloupky s GPS), kdežto
CB_CSADAD na lince 330510 nemá pole sloupku vyplněné u žádné zastávky a číselník sloupků byl prázdný
u všech kontrolovaných zastávek (ověřeno 1. 10.; do té doby tu stálo, že ho mají obě firmy u všech
zastávek testovaných linek - pro CB_CSADAD to neplatí). `id` záznamu se nemusí rovnat jeho číslu
(VS_CSAD czst 1 má záznamy 304 a 403, u 304 je číslo prázdné).
Záznam má **časové varianty** `^JRZ(czst,"CasoveVariantyUdaju",horolog,"Sloupek",id)` - webová
aplikace EDISONu čte jen je (`WebApp/CACHE/ALWA/EDISONWEB/Jr/Body.cls:851-861`), význam polí
(1 číslo, 2/3 GPS, 4 popis, 5 provozovatel, 6 rádius, … 39 polygony) drží
`ISYZastavky/CACHE/ALVA/ISY/ISYZastavky/API/ZastavkyOznacniky.cls:8-45`. Jinou polohu označníku
než tuhle `^JRZ` nemá.

**Označník (nástupiště)** = W pole **7** per spoj, fallback J-věta segment **#3** (`nastTam:nastZpet`)
per zastávka a směr. Ověřeno 30. 7. na `CB_CSADAD` 0320680: J-věta nese `11`, W-trasa spoje `22` -
tedy skutečně označníky té zastávky. ⚠️ **Obsah pole není zaručený**: na `CB_GWBUS` 501/0000001 je
v tomtéž poli text `JET`/`JETE` (v J-větě i ve W shodně), tedy něco, co označník není a co zatím
doménově nevíme vysvětlit. Featura „rozdělit vývěsku pro každý označník" (#8828) proto může nad
takovými daty vyrobit stránky „označník JET" - je opt-in a titulek stránky hodnotu zobrazuje, takže
je to poznat na první pohled; heuristika „vypadá to jako označník" se vědomě nedělá.

### `^JRZ(czst,...)` - zastávky

`czst` = číslo zastávky. Hlavní uzel je `$`-delimitovaný řetězec (~46+ polí): názvy
(obec/část/lokalita), názvy pro odbavovací zařízení, atributy, zóny IDS, GPS, příznaky.
**Časové varianty:** `^JRZ(czst,"CasoveVariantyUdaju",horolog)` - vybírá se poslední
varianta platná k datu (horolog = dny od 31.12.1840). Analogie MHD `ZastavkyFce.GetNazevHrl`.

#### Časové varianty zastávek v tisku (`B188`, od 7. 10. 2026)

AD agendy (zastávkový i linkový JŘ) čtou **každý údaj zastávky z registru** - název, název pro MHD,
okres, CIS název a číslo, atributy, zóny registru i popis sloupku - přes
`Jr.DataAd.ZaznamZastavkyKDatu` k **„platí od" linky** (`Jr.DataAd.HorologPlatnostiLinky`, `PATCH,"Plati"`;
rozhodnutí Jan s kolegou 7. 10.). Datum nastaví `BuildStrankaZastavky` / `BuildStrankaLinky`
(`^||TiskAd` / `^||TiskKnizni("zastavky","hrl")`); mimo sestavení stránky platí dnešek.

**Je to shoda se starým modulem, ne nová featura:** starý tisk AD čte každou zastávku přes
`gZst^JRapi` s `in("PAR","horlog")=linHrlOd` (`JRapiLinPrtMhd.mac:78`, `JRapiLinPrtPS1.mac:26`,
datum z `gLinHrlOd^JRapiLinUtl`). Pravidlo výběru je 1:1 `gZst^JRapiZst` (`JRapiZst.mac:123-147`)
a stejné drží registr (`ISYZastavky.API.Zastavky.nacti`): varianta s nejpozdějším začátkem ≤ datum,
jejíž konec (`$`-pole 42) je prázdný nebo ≥ datum, skončená se přeskočí; jinak hlavní uzel, a je-li
prázdný a datum před první variantou, první varianta. Hlídá `Tests.ZastavkyKDatuTest`.

⚠️ **Sloupky** mají vlastní podstrom u každé varianty; varianta bez sloupků dědí hlavní záznam.
Že to tak čte i starý tisk, **neověřeno** (`gZst` vrací jen `$`-větu). MHD agendy mají vlastní
registr a název k datu berou přes `ALVA.MHD.ZastavkyFce.GetNazevHrl` - beze změny.

### `^JRSET(...)` - číselníky a nastavení

| Uzel | Obsah |
|---|---|
| `("Definice","ZON",id)` | zóny IDS (popis, sousedi, oblast, městská zóna…) |
| `("Definice","WinMHD",rec,"DSC")` | **nadpisy sloupců** (Pracovní dny / Soboty / Neděle… per FTL) `rec`=CZ/*SK |
| `("Definice","WinMHD","prtset")` | nastavení tisku MHD (šipka, barevné schéma, oddělovač minut) |
| `("Cis","InPlatDef",sku,nz)` | negativní značky - popis, typ platnosti (DPL), text pro legendu, datumová omezení |
| `("Cis","InPlatODDO")` | rozsah znaků pro přidělování NZ |
| `("NCOME",rec,znk)` | texty platností pro znaky `1`-`7`,`+` (do legendy) |

---

## 3. PATCH metadata - řízení tisku linky

`^JRL(sku,lin,"PATCH",klíč)`. Klíčové pro odvození sloupců:

| PATCH klíč | Význam |
|---|---|
| `MHD_TYP_R` | **FTL mód** (0-3) - kolik sloupců a jaké (viz §4) |
| `MHD_NZ1_NZ2` | NZ pro prázdninové dělení: `NZ_oПrázdninách ~ NZ_mimoPrázdniny` |
| `MHD_NENZ1_NENZ2` | NZ pro **skrytí** spoje v daném sloupci (negativní filtr) |
| `MHD_NEZNZ123` | NZ k nevypisování (legenda) per sloupec |
| `linJEDNOSMERNA` | jednosměrná linka (jen jeden směr) |
| `ISY_LinkyTiskMHDNadpisy` | override nadpisů sloupců z `^JRSET("LinkyTiskMHDNadpisy",…)` |
| `nazMHD`, fonty, okraje, měřítka, barevný tisk | prezentační - pro TD řeší šablona, ne agenda |

---

## 4. ⭐ Odvození sloupců (typů provozu) - FTL + Plk + CNZ

**Toto je hlavní rozdíl oproti MHD.** MHD má `TypProvozuID` jako přímou entitu →
1 sloupec DepartureGridu. Linková verze sloupce **počítá** z kombinace FTL módu + Plk
(tarifní platnost spoje) + CNZ (negativní značka). Zdroj: `JRapiLinPrtMhd1.mac` (label
přiřazení ~ř. 18-300, nadpisy ~ř. 496-565).

### FTL módy (`MHD_TYP_R`)

| FTL | Sloupce |
|---|---|
| **0** | 3: Pracovní dny (1-5,X) / Soboty (6) / Neděle+Svátky (7,+) |
| **1** | 3: Prac. dny mimo prázdniny / Prac. dny o prázdninách / So+Ne+Svátky - dělení přes NZ |
| **2** | 2: Pracovní dny / So+Ne+Svátky |
| **3** | 4: Prac. dny mimo prázdniny / o prázdninách / Soboty / Neděle+Svátky |

### Plk - tarifní platnost spoje

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
(`prutmbmp`) - shodné bitmapy = stejná trasa → reuse. (Analogie MHD alg. 1002 rozdílové
trasy, ale tady přes časové průběhy, ne bitmapy obsluhy.)

⚠️ **Bitmapa se staví jen od tištěné zastávky do konce** (`f mytc=Tc:Krok:TcDo`,
`JRapiLinPrtMhd1.mac:274`) a uloží se pod klíč `(Tc, Smer)` - varianty jsou tedy vlastnictvím
konkrétní zastávky. Rozdíl trasy PŘED tou zastávkou variantu zakládat nesmí: časy jsou rozdíl proti
ní, takže zajezdka v první polovině linky by ve druhé polovině dala dva sloupce s identickými čísly.
My jsme to do 12. 8. srovnávali přes celý směr (na gold 0365001 to bylo 56 duplicitních sloupců
ze 116); dnes to drží `JeVUsekuVariant` + invariantní test `TestZadneDuplicitniVariantniSloupce`.

⚠️ **Sloupec „Časy" ve starém modulu NENÍ „výchozí spoj".** Přepínač u linky
`^JRL(sku,lin,"PATCH","mhd","par",1)` (legacy `arrprtlin("dta",73)`, popisek „mhd odjezdník -
automaticky generovat časové průběhy tras") rozhoduje mezi dvěma zdroji: **vypnuto** = ručně zadané
minuty u trasy (`^JRL(sku,lin,"J",poradi)`, 8. `#`-segment ve tvaru `Tam:Zpět`, přes `GetMin` na
rozdíly proti vývěsce; **jediný sloupec**), **zapnuto** = spočítané průběhy, a to **až čtyři sloupce**
(`JRapiExp10.mac:383-420` naplní `dta` 3/9/11/12 a ruční hodnotu zahodí).
Jak vzniká pořadí: pro **každý spoj** se od tištěné zastávky do konce směru poskládá průběh
kumulativních minut proti ní („<" = projíždí), shodné průběhy se sloučí a zbytek se seřadí **podle
délky textového zápisu** průběhu (`$l(prutm)`, `JRapiLinPrtMhd1.mac:322-337`) - první sloupec je tedy
ten, který **dosáhne nejdál**, ne „nejdelší čas" ani „nejvíc spojů". Kolik z těch čtyř se dostane na
papír, drží tisková předloha (neověřeno).
⚠️ **„Časy" a variantní „minutové sloupce" jsou v legacy dvě NEZÁVISLÉ rodiny**: Časy slučují shodné
**časy** (1-4 sloupce), variantní sloupce shodný **sled zastávek** (až 15, značka z nastavení linky)
a plní se **bez ohledu na přepínač**. Naše svázání „varianty nahradí sloupec Času" je vlastní
konstrukce, ne parita.
⚠️ **Přepínač je per LINKA, ne per firma, a Nitra ho má na obojí** (měřeno 13. 8., dřív tu stálo
paušální „Nitra má zapnuto"): linka `0000001` = zapnuto, linka `0000002` = vypnuto a ruční minuty
vyplněné (`J`-věta segment 8 = `0:34`) → **na téhle lince starý modul tiskne její ruční minuty, ne
spočítané**. České firmy naopak mají vypnuto s **prázdnými** minutami (vzorek `CB_CSADAD`, `ZL_KOVED`
0822995, `CB_GWBUS` 0320113 - segment 8 = `:`) → ve starém tiskne prázdno, náš spočítaný sloupec je
tam tedy featura, ne odchylka. **My přepínač ignorujeme a vždy počítáme**; trasu vybíráme podle
pokrytí seznamu (`VyberZaklTrasu`), což je legacy záměr měřený přesněji. Ručně zadané minuty
**nečteme vůbec** - u linek v ručním režimu s vyplněnými minutami (Nitra) proto tiskneme **jiná
čísla** než jejich starý výtisk. Vzorek jsou 4 linky, ne plošné měření.

✅ **Obsah sloupce už míchaný není** (`B67`, 13. 8.): variantní sloupec nese **průběh jedné vybrané
trasy** - v rámci varianty vyhrává delší jízda (`DelkaProfilu`), při shodě nižší číslo trasy. Totéž
pravidlo drží jediný sloupec „Časy" (`VyberZaklTrasu`: dosah, pak delší jízda), takže obě rodiny
sloupců mají jedno pravidlo a vždy jde o skutečný spoj. Hlídá to invariantní test
`TestVariantniSloupecJeProbehJedneTrasy` (ke každému sloupci musí existovat trasa, jejíž průběh mu
odpovídá; pod mutací +1 minuta padne na 60 z 60 sloupců). Legacy dělá totéž
(`necham prubeh s delsim casem`).

**Co od legacy zbývá jinak:** (a) kritérium pro jediný sloupec „Časy" měříme jako **dosah** (počet
obsloužených zastávek), legacy ho aproximuje **délkou textu průběhu** - koreluje to, totožné to není
a oraclem to ověřené nemáme; (b) legacy v automatickém režimu plní **až čtyři** sloupce Času, my jeden
(zbytek na vyžádání); (c) ruční minuty nečteme (viz výše)

**Návrh volby zdroje sloupce „Čas" (`B144`, odloženo Janem 11. 9.):** spočítané z jízdních dob (dnes), ruční „MHD čas tam/zpět" z trasy linky, nebo „Automaticky (dle linky)" podle přepínače u linky.
Výchozí musí zůstat spočítané, jinak se českým firmám sloupec vyprázdní (ruční minuty mají prázdné).
Parametr zpracuje server, takže v `GetParameters` potřebuje příznak `serverSide` (`cache-api.md`).
⚠️ Odloženo, protože si o to zákazník neřekl: vyšlo to najevo při hovoru jako rozdíl proti starému výtisku na Nitře, ale on sám řekl, že bude radši používat varianty tras.

⚠️ **Sloupec „Čas jízdy" nese tři stavy** (`B67`): číslo, „<" (základní trasa zastávkou projíždí bez
stání) nebo prázdno (mimo základní trasu). Dřív bylo prázdno v obou případech. Na wire je to od té
doby **text, ne číslo** (schéma pole zůstalo `number`, protože MHD posílá jen čísla); na gold lince
`0365001` stav „<" nevzniká, pravidlo proto drží jednotkový test `TestCasJizdySloupce`.

⚠️ **Variantní sloupce sloupec „Časy" nenahrazují** (`B67`, dřív ho vytlačily): jsou to dvě nezávislé
rodiny jako v legacy („Časy" vs „Trasa min. 1-15"), sloupec Času má vlastní viditelnost a varianty se
řadí za něj. V režimu „Řádek pro každé stanoviště" se základní čas vypíše jen na prvním z rozvinutých
řádků - na stanoviště vázaný není.

### Anotace u odjezdů + legenda

Speciální znaky a NZ se generují jako horní indexy u minut; texty se sbírají do legendy
(`stPoznamky`, typ 0/1/2). NZ se filtruje per sloupec (`noNZ2` - vyřadí NZ, která není
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

> **Úklid:** tato sekce se ořeže/přepíše až po vyřešení otázek - typicky během linkových a knižních JŘ
> (linkové/knižní JŘ), kde se tyto otázky (FTL/Plk/CNZ, skupina linek, tarif) reálně řeší.
> Do té doby zůstává jako referenční poznámky.

- ❓ **Číst `^JRL` přímo, nebo přes JRapi helpery?** MHD jsme reimplementovali přímo nad
  globály + jemné `Fce` třídy. JRMAIN helpery jsou složitější (working global `^CacheTempJB`).
  Pro čistou reimplementaci pravděpodobně číst přímo `^JRL`/`^JRZ` a logiku sloupců napsat
  nově - ale potvrdit na reálné lince, že rozumíme formátu vět.
- ❓ **FTL + Plk + CNZ algoritmus** - STÁLE OTEVŘENÉ: ověřit na reálné lince s MHD-mřížkou
  (`MHD_TYP_R` set, ideálně prázdninový JŘ FTL=1/3), zejména NENZ filtraci. CB_GWBUS/0320113
  grid nepoužívá. Potřebujeme najít zákazníka/linku s minutovou mřížkou.
- ✅ **`^JRL` struktura** - OVĚŘENO na CB_GWBUS/0320113 (viz §8). dta pozice spoje, trasa, "W",
  JRZ a PATCH potvrzeny.
- ❓ **Skupina linek (`sku`)** - jak se vybírá/předává (analogie MHD SkupinaLinekID v InstancePickeru)?
  Na CB_GWBUS je `sku=1`. Zjistit, jestli existuje víc skupin a jak se mapují.
- ❓ **Vícezónový tarif + km sloupec** - `KmsSmer,T/Z` ověřeno (km per směr, `-1`=nestaví);
  doladit přesné formátování zón (km informativní, jezdí se podle zón).
- ✅ **Návaznosti** - uloženy v `("T",sp,32)` (`m@…`); `NavaznostiZpusobVypisu` v PATCH řídí
  dole vs v tabulce. Potvrzeno na T,5,32 a T,11,32.

---

## 7. Mapování na TD JSON DataContext (cíl reimplementace)

`GetData` nové agendy emituje stejný JSON jako `ZastavkoveJrMhd` (viz `cache-api.md`
GetData wire formát) - 1 stránka = 1 zastávka × směr:

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
- `^JRL(sku)` - `sku` je **skupina linek** (číslo), uzel obsahuje hlavičku skupiny (`JŘ_2025~~~0~~1~0`).
- `lin` = **7-znakové číslo** (`"0320113"`).
- `("I")` = `0#CB`, `("I",1..N)` = názvové části (`České Budějovice`/`Kolný`/`Lišov`), `("I",0,2)` = mapování spojek názvu (`1:2:3`).

### Trasa linky `("J")`
- `("J")` = počet zastávek (`26`).
- `("J",poradi)` = `czst:atributy##…:…` - **první `:`-pole = czst** (číslo zastávky → `^JRZ`). `poradi` = 1..N.
- `("J",poradi,tcd)` / `…,"bd",idx` = km varianty + detailní body (fiktivní zóna `5~…~zona^km`).
- **Kompletní mapa `#`-segmentů** (1:1 `JRapiLin` ř.651-703, kde jsou jako `dta1..dta17`; ověřeno 3. 8.
  a doplněno, aby se nemusela znovu dohledávat v legacy). Segment = `dta` číslo minus 2 od `dta3` výš:

  | seg | dta | obsah |
  |---|---|---|
  | 1 | 1+2 | `czst:atributy` (atributy = znaky číselníku 16) |
  | 2 | 3 | (nepoužito) |
  | 3 | 4+5 | **označník** `nastupisteTam:nastupisteZpet` (volný text) |
  | 4 | 6 | MHD numero + systém (`I`/`P`/`Z`/`B` = ids/idpp/idpz/bip) |
  | 5 | 7 | **odsazení** zastávky (single per bod - proto `B12`/#7937 nejde per směr bez změny zdroje) |
  | 6 | 8 | tarifní pásmo MHD (1-4) |
  | 7 | 9 | předpis pro tisk trasy MHD (`tam:zpet`) |
  | 8 | 10 | předpis pro čas u trasy MHD |
  | 9 | 11 | **zóna IDS** (čárka = oddělovač) |
  | 10 | 12 | předpis pro tisk **základní** trasy MHD (= vzorová trasa) |
  | 11 | 13 | zóna IDS pro odbavovací zařízení |
  | 12 | 14 | **sloupek** `sloupekTam:sloupekZpet` (číselník, viz §výše) |
  | 13 | 15 | povel BUSE (číselník 26) |
  | 14 | 16 | jede přes - pro poznámku do odbavovacího zařízení |
  | 15 | 17 | DS zóny (`DSId|zonaId;…`) |

### Spoje `("T")` / `("Z")` ⭐ (oprava mapy)
- `("T")` = **počet spojů** ve směru (header), `("T",sp)` = věta spoje.
- **`sp` jsou libovolná ID, NE 1..N** - na téhle lince `3,5,7,9,11,13`. Vždy iterovat přes `$order`, nikdy nepředpokládat sekvenci. (`("T",1)` byl prázdný → původní probe selhal.)
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
  - `omzId=22` → **nejede** (`k@DD.MM.RRRR:DD.MM.RRRR#…` - rozsahy datumů)
  - `omzId=32` → **návaznost** (`m@…#linka:tc:popis:…`)

### Trasa spoje `("W",cisloTrasy)`
- `#`-segment per zastávka (pořadí 1..N), `:`-delimited: `poradi:čas:km:…`
  - poz 2 = **čas od počátku spoje** (min); poz 3 = km.
  - poz 5 = **atributy spoje v zastávce** (legacy `arrsp("trs",Tc,6)`, `JRapiSp.mac:239`);
    `V` = **jen výstup** - odjezd se netiskne (`JRapiLinPrtMhd1.mac:201`). Častý: VS_CSAD
    ~30 000 zastávek spojů, obvykle ta před konečnou (měřeno 25. 9.).
- **Konec spoje** = 2. pole hlavičky spoje `^JRL(sku,lin,smer,sp)` (`TcOd:TcDo:CisloTrasy:…`, legacy
  `arrsp("dta",2)`). Na konečné spoje se odjezd netiskne (`JRapiLinPrtMhd1.mac:194`).
  - **`<` = spoj zde NESTAVÍ** (projíždí / zastávka mimo trasu spoje).
- **Odjezd v zastávce = `Odjezd spoje + W[zastávka].čas`** (např. stop 26: 392+53 = 445 = 7:25).

### Zastávka `^JRZ(czst)`
- Hlavní uzel `$`-delimited: `NORM_NAZEV$Název,,lokalita$BLO$…$czst$0$zónaIDS~0~$GPS_N$GPS_E$…`
  (ověřeno: czst `4842`, GPS `48.97/14.49`).
- **`$`-pole 8 = atributy zastávky** (přestupy `A`/`L`, na znamení…) - čte se **spolu** s atributy
  z trasy linky, viz `B23`.
- **`$`-pole 9 = číslo zastávky dle celostátního registru (CIS)** (`JRapiZst` ř.150 `dta14`). ⚠️ Na
  KOVED datech je **shodné s czst** (41994 = 41994), takže se nepozná od pole `CISLO_ZASTAVKA`, které
  už tiskneme; šestimístný CIS kód uzel+sloupek (`419940`) je v `~`-struktuře `$`-pole 5. Viz `B35c`.
- **`^JRZ(czst,"Sloupek",id)` = číselník sloupků** zastávky (`číslo ~ GPS lat ~ GPS lon ~ popis směru`).
- **`$`-pole 11 = `zóna IDS ~ hranice IDS (0/1) ~ další zóny (";")`** (`JRapiZst` dta15/16/17).
  Ověřeno: zastávka `4842` má u sebe `100J`, kdežto trasa téže linky říká `101` - **zdroje se
  reálně liší**. Písmeno v kódu (`100J`, `O12`) nese **přepravní systém**, ne zónu
  (`JRapi4hand`: ODIS → „O", IDSOK → „I"); pro tisk se maže dle `ZonaIdsDelZnk`.
- **Zóna do tisku (1:1 legacy `JRapiExp10`):** trasa linky (J-věta 9. `#`-segment, čárka =
  oddělovač) → když prázdná, záskok na `$`-pole 11 (u hraniční zastávky i druhá zóna);
  hodnota `-` = netisknout.
- `^JRZ(czst,1,linka)` = index linek obsluhujících zastávku.

### PATCH (ověřeno)
- `Plati = od~do`, `Head` (poznámka nad linkou), `OBJ` (typ JŘ), `linJEDNOSMERNA`, `ZonaIdsDelZnk`,
  `KmsSmer,T/Z,varianta` (km per směr - `-1` = nestaví), `StrPoz,sp,tc` (poznámky pro strojky),
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

## 9. Zlatá reference (legacy oracle) - reprodukovatelná na vyžádání

Cílový výstup zastávkového JŘ z linkových dat jde vygenerovat z legacy generátoru kdykoliv přes
`cache_query` - slouží jako **test oracle** pro reimplementaci (Fáze 3 = porovnat náš výstup s tímto):

```
cache_query  ALVA.TISKMHD.Data / LoadData
  idfa=CB_GWBUS  dataNamespace=CB_GWBUS
  parameters=["VOLANO_Z~JRMAIN", "SESTAVA~1", "VALUE~<sku>#<lin>#<page>#3#0-23#0##"]
```
`VALUE` = `SkupinaId#LinkaId#PageNumber#CasovaOsa#OsaOdDo#TiskProjetych#FileName#NeAtribut`.
PageNumber = pořadí (zastávka × směr); iteruj 1,2,3… dokud OUT není prázdný.

**Ověřeno na 0320113 / page 1** (zastávka 4842 ČB AN → Lišov). Výstup = `SEKCE_START`/`SEKCE_END`
bloky: `stDBField` (skaláry), `stSekceSeznamZastavek` (StopList - 22 zastávek, zóny, TrasaMin1-N
varianty s `<`=nestaví), `stHodinaOdjezdu` (osa hodin), `stMinutyOdjezdu~SMO1..4` (DepartureGrid
sloupce = skupiny dnů: Pracovní dny / Soboty / Neděle a svátky), `stPoznamky` (legenda).

**Nález k odvození sloupců:** na 0320113 (FTL default, Plk=X) všech 6 spojů spadlo do SMO1
„Pracovní dny" s anotacemi „nejede" (†32/†39/†70 z `,22` omezení), SMO2/SMO3 prázdné. Tj. zařazení
do sloupce není čistě „Plk znak → sloupec" - promítají se i datová omezení. Tuto logiku engine replikuje.

### 9.1 Automatizovaný oracle (gold pojistka) - `ZastavkoveJrAdLegacyTest`

Ruční recept výše je zautomatizovaný v `ALVA.TemplateDesigner.Tests.ZastavkoveJrAdLegacyTest`:
zavolá legacy `LoadData` (VolanoZ=JRMAIN) i náš `GetData` a **1:1 assertne shodu zón** per zastávka
(CB_GWBUS 320113 = `101/104/109`, KV_DP 410101 vč. vícezónové `11,12`). Namespace / legacy třída /
prázdné zóny → skip. Diagnostika `ZonyProbe` (IN DB/SKU/LIN/PAGE → OUT LEG/OUR/MATCH). Rozšiřitelné
na TrasaMin varianty (bod 5). **Zóny ověřeny nerozbité vůči starému modulu.**

**Pasti volání legacy `LoadData` z ObjectScriptu** (jinak než přímé `cache_query`):
- IN parametry čte jako **řetězce „KLÍČ~HODNOTA"** iterací všech subscriptů (`$p(val,"~",2)`), ne
  pojmenované subscripty → nastav `^||ML("IN",1)="VOLANO_Z~JRMAIN"`, `(2)="SESTAVA~1"`, `(3)="VALUE~…"`.
- OUT **nemá count node** → iteruj `$order(^||ML("OUT",i))`, ne `for i=1:1:$g(^||ML("OUT"))`.
- `$$$MakroGlobal` přepíná IN/OUT globál dle `sys("C","App")` → nastav `"ALWA.SYS.MLRest"`, jinak čte
  `^CacheTempMLSrv`. IdFa/dataDB jde ze `sys("U","IdFa")`/`sys("U","DATA")` (AD data nejsou idfa-scoped).

---

## 10. Stav implementace agendy `ZastavkoveJrAd` (snapshot 2026-06-02)

> ⚠️ **Datum snapshotu zastaralý.** Tato sekce je historický záznam stavu k 2026-06-02; agenda je
> dnes datově 1:1 + výkonově hotová. **Aktuální stav** viz [`changelog.md`](changelog.md) +
> [`roadmap.md`](roadmap.md) (sekce „Co máme" / AD-N tabulka). Níže ponecháno jako referenční mapa
> implementace (co engine dělá a kde), ne jako status.

Třída `ALVA.TemplateDesigner.Agendy.ZastavkoveJrAd` (code `ZASTAVKOVY_JR_AD`, „Zastávkový JŘ (AD)").
Reimplementace legacy `ALVA.TISKMHD.Data` větve `VolanoZ=JRMAIN`. **Pozn.: TD běží ve firmě dle
`templatedesigner.appsettings.json` (testováno KV_DP, první linka 0410101 Cheb–Libá–Hazlov; ^JRL
je per-namespace, bez IdFa).**

### ✅ Hotovo a ověřeno (proti gold oracle `ALVA.TISKMHD.Data`)
- **GetSchema/GetMockData/GetParameters** - schema sdílené s MHD; default LINKA_ID
  = první linka firmy (dynamicky). GetMockData = živý náhled první linky firmy (DEV - viz dev tweaky).
- **GetData fázový engine** čte `^JRL`/`^JRZ`:
  - Hlavička (CISLO_LINKY, POPIS_LINKY z "I", platnost z PATCH, aktuální zastávka/směr).
  - Trasa → StopList se **zónami** + **na znamení** (atribut `x`); **filtr projížděných** (`<` -
    jen zastávky kde staví ≥1 spoj, přes `ObsluhovanePoradi`). Ověřeno: 0410101 → 17 zastávek (=legacy).
  - **Odjezdy** = Odjezd spoje + offset z trasy spoje "W"; **multi-group** (`SkupinyZPlk` - spoj může
    být ve více skupinách dnů, Plk "6+" → soboty i neděle). Ověřeno proti legacy SMO1/2/3.
  - **Poznámky** = legenda z datumových omezení (omzId 11-15,22; `Jr.DataAd.TextOmezeni`+`PredlozkaOmezeni`
    +`FormatDatumyOmezeni`, 1:1 s `txtNZ`/`TODDO^JRapiNZUtl`) + na-znamení.
  - **Multi-page**: stránka per zastávka × směr (Tam vzestupně / Zpět sestupně k pořadí 1);
    prázdné konečné se skipnou. Ověřeno 0410101: 17 Tam + 20 Zpět.
- **Instance picker** (`GetAvailableInstances`) - seznam linek z ^JRL (první skupina); proklikávání
  stránek v preview. C# `InstanceParamName` AD→LINKA_ID; GetData přijímá i `INSTANCE_ID` alias.

### ✅ 1:1 drobnosti dokončené (2026-06-04 - 06-11, ověřeno proti legacy zdroji + datům 0410101)
1. **Názvy zastávek** - `NazevZastavky` bere `^JRZ` `$7` (odbav/MHD název s obcí, "Cheb,aut.nádraží",
   "Fr. Lázně,Krapice,Lužná"), fallback `$2`. Zahozeno `FormatNazev`/`FormatNazevPlny` (drop-obec).
   `NazevLinky` join `–`→`-` (legacy pomlčka).
2. **Datumy v poznámkách** - `FormatDatumyOmezeni` + `FormatDatumOmezeni` (dřív `UpravDatum`) = 1:1 formát zastávkového JŘ
   (`JRapiLinPrtPS3`/`UprDat`): **2místný rok** (`25.12.22`, ne `25.12.2022`), strip nul ze dne/měsíce,
   rozsah `:`→`od D1 do D2`, položky spojené `, `, `k@`/`L@` prefix se stripuje. **POZOR:** NE `TODDO`
   (to dělá `D1-D2` + verbatim rok) - zastávkový JŘ používá PS3 formát; ověřeno gold 0340303/0365001.
3. **"jede" omezení** - `Jr.DataAd.TextOmezeni` (dřív `SestavTextOmezeni`) projde omzId 13,12,11,14,15,22, spojí do jedné poznámky;
   `PredlozkaOmezeni` = mapování (11 "jede jen", 12 "jede také", 13 "jede", 14/15 sudé/liché týdny, 22 "nejede").
4. **POPIS_DOPRAVCE** - `PopisDopravce` (distinct `fa:pr` z `#`-pole 2 spojů obou směrů, dedup názvů,
   "Zajišťuje: …") + `NazevDopravce` chain z `^JRSET("DOP")`: per-provoz `$6` → firma `$14` → `$7` → `$1`.
5. **LOKALNI_OZNACENI_LINKY + TITULEK + INFORMACE** - všechny z `^JRL(...,"MhD","SeT_MhD")` (delim `~`,
   1:1 `sTIT^JRapiLinPrtMhd1`): `$1` lokální označení (fallback `$E(Lin,5,7)`="101", `Jr.DataAd.LokalniOznaceniLinky`),
   `$2` titulek, `$5` informace (`<br>`→newline). Náhradní titulek starého tisku „Městská hromadná doprava..."
   od 30. 10. neposíláme (`B56` S7, rozhodnutí Jan): dostalo by ho 97 % linek včetně regionálních.
7. **Číslování poznámek** - pro KV_DP (TYPPOZ=0 = `^JRSET("Definice","MHDnewOM")` prázdné) legacy používá
   sekvenční čísla = moje 1/2/3 (schéma shodné, hodnoty 39/70/32 byly artefakt jiného seedu). Bez změny.
   TYPPOZ=1 (písmenka a/b/c) / NZ-symbol režim = firm-config feature, deferred. **KOREKCE 2026-06-23:
   CB_CSADAD tento režim MÁ** - 0365001 ukazuje u „nejede" **inverzní číslo „29"** (NZ číselník / `cnz`
   dta29 renderované inverzně), ne sekvenční. Máme tedy test firmu. Implementace: číst NZ číslo +
   render inverzně (jako NZ marker), místo `ZiskejMarker` sekvence. + bezbariér „H"/„F" je SpecZnak
   speciálního fontu (72=vozíček/70=částečně) - legacy GDI+ font; my ASCII glyf (vizuální 1:1 nuance).
6. **FTL 1-3 + CNZ prázdninové dělení sloupců** - `ZarazeniSpoje(ftl,plk,cnz,nzCfg)` staví `KamPrt` bitmapu
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
- **POPIS_DOPRAVCE 1:1** - `<název>, <sídlo provozovny>,,tel:<tel>` (když není zkrácený MHD název $6/$14);
  `NazevDopravce` rozšířeno. Ověřeno = legacy „Zajišťuje: DPKV, a.s., Sportovní…,,tel:353 504 518".
- **Počet stránek 35** - terminus check v `BuildStrankaZastavky`: board na konečné zastávce směru
  (za currentPoradi není obsluhovaná zastávka v servedPoradi) se netiskne (dřív 37 - 2 boardy navíc).
- **KRITICKÝ fix** - `GetData` padal `<SUBSCRIPT>` od commitu #1 (`NazevZastavkyCis(czst="")` → `^JRZ("")`);
  `GetMockData` to maskovalo (auto-pick vždy obsluhované poradí). Init `posledniCzst` + guard prázdného czst.
- **#2 anotace = falešný poplach** - markery na stejných časech jako legacy (subagent přečetl chybně).

### ✅ Atributy zastávek (2026-06-11)
`TextAtributu` = číselník `arrCis(16)^JRapiCis` (27 znaků); `JeViditelnyAtribut` filtruje významové
(`xyzNVALefgODCGabc` - na znamení, nástup/výstup, ikony dopravy autobus/vlak/metro/loď/letadlo, paragraf)
od formátovacích (T/p/P/J/o/S) a souhrnných (>/</E/H). StopList parsuje dta2 J věty → `AddAtribut` na
zastávku + sběr do legendy (`SestavPoznamky` s `atrSeen`). Ověřeno 0320144/CB_GWBUS (A=autobus, x=na znamení, L=vlak).

### Stav featur (2026-06-11, po scanu firem CB_GWBUS/KV_DP/CB_CSADAD/VS_CSAD)
- **Prázdninové dělení (FTL1/3 NZ split)** - kód hotový, **logika ověřena synteticky** (cnz=NZFTL1→jen
  „mimo prázdniny", cnz=NZFTL2→jen „o prázdninách"). `MHD_NZ1_NZ2` ale **prázdné ve všech firmách** → dormant.
- **Značky platnosti spojů (ZnkSp/NCOME)** - spoje používají jen `X`/`6+`/prázdné Plk (žádné denně-specifické)
  → v datech se nevyskytuje. NCOME registr = `^JRSET("NCOME",jazyk,N)` (jede v pondělí…). Neimplementováno (bez use-case).

### ✅ Rozdíly tras - HOTOVO (2026-06-11), ověřeno 1:1
1:1 reimplementace `OmTrasa^JRapiLinPrtMhd` (AD) - metody `RozdilyTras` + helpery `SpojBitmapa`,
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
- ✅ **POPIS_DOPRAVCE - adresa provozovny DOPLNĚNA** (2026-06-12). `NazevDopravce` nyní čte i `$2`
  (ulice,číslo) z `^JRSET("DOP",fa,pr)` (gDopFaPr: $1 sídlo, $2 ulice, $3 tel) → slot mezi sídlem a
  telefonem. Ověřeno 1:1 na 0365001: `prov.Písek,Nádražní 2294,tel:601 319 673` = gold.
- ✅ **Filtr atributů zastávek - OPRAVENO** (2026-06-12). `JeViditelnyAtribut` zúžen na **jen „x" (na
  znamení)** - legacy `UVOD^JRapiLinPrtMhd` (ř.189-190) promítá z dta2 do StopListu jen „x", ostatní
  znaky (A=autobus, L=vlak, metro, nástup/výstup…) v tomto výstupu netiskne. A/L over-emise pryč.
- ✅ **Atributy spoje (bezbariér) - HOTOVO** (2026-06-12). `BezbarierKonfig` (isinv/iscbe z
  `^JRSET("PS","LIMES")` „invalida"/„invalidaspomoc", allBBP = všechny spoje hlava:15) + per spoj
  v `NacistOdjezdy`: `inv`=hlava:15→marker „H" (SpecZnak 72), `cbe`=hlava:22→„F" (70), anotace
  „specialni" + legenda. Pořadí anotací/legendy 1:1 gold. Ověřeno 0365001: H = „spoj má
  bezbariérově přístupné vozidlo", na minutách `[B, H]`. (allBBP → text „všechny spoje…", bez
  markeru u minut.) Marker = `$char(SpecZnak)` jako MHD sourozenec (gold SEKCE ukazuje číslo „72",
  vizuálně týž glyf vozíčku).
- ✅ **Časy jízdy ve StopListu (TrasaMin) - JEDEN sloupec HOTOVO** (2026-06-12, 1:1 ověřeno na velké
  lince 0340303 + 0365001). `ZakladniCisloTrasy` = **hlavní (nejčastější) trasa** mezi spoji stavícími
  na vývěsce (1:1 legacy „trasa s největším počtem spojů" - vyhne se tomu, aby expres projíždějící uzly
  určoval časy) + `CasJizdyZMapy` (= `cas(poradi)-cas(currentPoradi)`, reference od vývěskové zastávky).
  `CAS_JIZDY` ve StopListu, renderer `StopListElement.cs:469` blankuje `<=0`.
  ⚠️ **Pobytové zastávky (uzly)** mají W-cas formát `příjezd/čekání` (např. `33/4`) → `CasHodnota`
  vrací **odjezd = příjezd+čekání** (1:1 legacy `Cas+Ceka`). Týž fix v `NacistOdjezdy` offsetu - jinak
  se pobytová vývěsková zastávka přeskočila → **chybějící odjezdy na stránkách uzlů** (byl to latentní
  bug i ve starém W-stringovém kódu). Ověřeno 0340303: uzly 37/72/127 = gold, stránky ČB/Třeboň/Písek
  mají odjezdy.
  ⬜ **Multi-varianta (`TrasaMin1-15`)** = víc časových sloupců per varianta trasy + `<` u projetých
  - odloženo (chce zásah do C# rendereru, který dnes umí jeden sloupec). Pro běžné linky (jedna
  trasa) je jednosloupcová verze 1:1; u variantních linek se ukáže jen jedna varianta.
- ✅ **Číslování omezení = číslo NZ (`cnz`) invertovaně** (HOTOVO 2026-06-23). Marker omezení je
  **`cnz` spoje** (`$p(spoj,"#",7)`, dta29) renderovaný jako typ `nz` (invertovaný box), text z per-spoj
  `^JRL(…,sp,omzId)` (`Jr.DataAd.TextOmezeni`). **Žádný firemní gate, žádné čtení InPlatDef** - ověřeno gold
  oracle (`ALVA.TISKMHD.Data`) na **obou** firmách: legenda `2#<cnz>#<text>`, anotace `2$<cnz>`.
  - **Korekce dřívějších domněnek:** (1) `MHDnewOM` (= údajný TYPPOZ gate) je prázdný u GWBUS i CSADAD →
    není to gate; chování je **univerzální**. (2) KV_DP spoje mají `cnz` taky (28/30/31/40) a legacy je
    renderuje jako čísla - naše dřívější sekvenční „1/2/3" tedy **nebylo 1:1 ani na KV_DP** (dřívější
    „1:1 ověřeno" tvrzení bylo chybné). (3) Text bere legacy z **per-spoj** datumů (aktuální 2022/2023),
    ne ze zastaralého InPlatDef seedu (2018/2019) → InPlatDef se nečte.
  - **Implementace:** `NacistOdjezdy` - `i cnz '= "" { znak=cnz, typOmez="nz", poznMap("typ",cnz)="nz" }`
    else fallback `ZiskejMarker` (typ „text", spoj bez NZ). `SestavPoznamky` čte `poznMap("typ",n)`.
    `cnz` prázdné → zachová staré sekvenční chování.
  - **Render:** přepínač `InvertovatNegativniZnacky` na `DepartureGridElement` odstraněn - inverze NZ je
    teď napevno vždy zapnutá (AD i MHD), barvy `NegativniZnacka{Back,Fore}Color` ponechány.
- ✅ Legenda POZNAMKY: víc sloupců (`NotesLegend.Columns` + `WrapSmer`) + šířka/layout - hotové.

### ⚡ Performance (GetData se volá per stránka = zastávka×směr, ~desítky/linka)
- ✅ **Per-linka/per-směr invarianty cachovány** (2026-06-12, behavior-preserving, výstup byte-identický):
  `ObsluhovanePoradi` (per směr) + `BezbarierKonfig` (per linka) se dřív full-scanovaly per stránka →
  teď cache `^||TiskAd("served"|"bezbar",…)`, čištěná na vstupu `GetData`/`GetMockData`. Eliminuje
  ~39× full-scan všech spojů na lince 0365001.
- ✅ **W-trasa cachovaná mapou `poradi→cas`** (2026-06-12, byte-identický): `WMapa` (cache
  `^||TiskAd("wmap",cisloTrasy)`) nahradila lineární `$p` sken W-stringu per lookup. `WCasNaZastavce`/
  `CasJizdyZTrasy`/`ZakladniWTrasa` → `WMapa`/`CasJizdyZMapy`/`ZakladniCisloTrasy`; `SpojBitmapa` +
  `NacistOdjezdy` čtou z mapy. Trasa se parsuje jednou per `cisloTrasy` místo per stránka×spoj.
  ⚠️ **Pozor - `^||` globály neberou prázdný subscript** (`<SUBSCRIPT>` na `^||TiskAd("wmap","")`);
  WMapa guarduje `cisloTrasy=""` (spoj bez trasy) + prázdné mapy necachuje (merge z undef = `<UNDEFINED>`).

### 🔧 DEV tweaky k odstranění před produkcí (označené TODO v kódu)
- `MainWindow.xaml.cs` `#if DEBUG` Loaded handler → auto-otevře `kv_ad_test.json` (hardcoded cesta).
- `GetMockData` čte živě z ^JRL (první linka firmy) - zvážit lehký statický vzorek.

### 🛠️ Debug nástroj
- MCP `cache_zwglobal` (read-only `zw` globálu) - Caché `ALVA.EDISONAI.McpDebug.DumpGlobal` (EDISON repo)
  + C# tool v `EdisonCacheMcp/Tools/CacheTools.cs`. Gold oracle: `cache_query ALVA.TISKMHD.Data/LoadData`
  s `VOLANO_Z~JRMAIN` (viz §9). Kompilace .cls jde přes PostToolUse hook, ne přes MCP.
- `ALVA.EDISONAI.McpDebug.ScanSubscript` (read-only) - najde záznamy 2-úrovňového globálu s daným pod-subscriptem.
  Volá se přes `cache_query`, params `[globalRef, tail("~"-sep), ns, max]`. Použito pro hledání linek s FTL:
  `cache_query ScanSubscript ["JRL","PATCH~MHD_TYP_R","KV_DP","2000"]` → seznam `sku~lin = hodnota`.



<!-- ══════════════════════════════════════════════════════════════════════ -->
# ==== ČÁST: Nastavení linky a rozdíly tras (analýza) ====
<!-- ══════════════════════════════════════════════════════════════════════ -->

# Zastávkový JŘ (AD) - analýza nastavení linky a rozdílů tras

> **Status (2026-06-30):** autoritativní podklad pro AD-N položky (legacy storage + readery). Většina
> AD-N je hotová - **aktuální stav** vždy v [`roadmap.md`](roadmap.md) tabulce „PRIORITA 2b". Tento
> dokument = mapa *kde* data jsou, ne status implementace.

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

**`ALVA.TISKMHD.Data` není místo logiky - je to jen výstupní formátovač.** Přes `VolanoZ` vybírá
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
| Návrh/výluka razítko (auto) | - | ⬜ | ❓ (jen PS větev) |

---

## 3. Sloupce mřížky (skupiny dnů) - tab „MHD – další"

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

**Dva nezávislé mechanismy** - pochopit odděleně je půlka řešení:

| | **A) Anotace odlišnosti** (písmena u minut) | **B) Variantní časové sloupce** (TrasaMin 1-15) |
|---|---|---|
| Vyjadřuje | „spoj jede jinudy" - textem | „varianta má tyto jízdní časy" - sloupcem |
| Kde | horní index u minuty + legenda | sloupec časů ve StopListu (`<` = nestaví) |
| Legacy | `OmTrasa^JRapiLinPrtMhd` | `prutmbmp` detekce variant, max 15 sloupců |
| **Máme** | ✅ **plně** (AD `RozdilyTras`, MHD alg.1002) | 🟡 **jen 1 sloupec** (hlavní trasa) |

### 4.1 Mechanismus A - anotace (✅ hotovo, ověřeno 1:1)
`RozdilyTras` porovná trasu spoje se vzorovou (základní) a sestaví text:
- „**neobsluhuje** zast. X" (přeskočí) · „**jede přes** zast. X" (navíc) · „**jede jen do** zast. X" (uťatý)
- „**jede až do** zast. X" (prodloužení - #7947, nahradilo matoucí legacy „pokračuje přes")

Marker (písmeno od „B", sada `BCEFGHIJ…` bez „D"), dedup, gated `PATCH,"mhd","not"` (AutoPozn).
**Ověřeno 1:1 na CB_CSADAD 0365005** (6 variant B/C/E/F/G/H = slovo od slova gold oracle).

### 4.2 Mechanismus B - variantní časy (🟡 mezera, vědomě odložená)
Legacy detekuje varianty přes bitmapu jízdních časů, každou dá do sloupce TrasaMin1-15 (`<` u projetých),
spoj → varianta přes písmeno. **My máme jeden sloupec** `CAS_JIZDY` (hlavní trasa); varianty 2-15 ⬜.

### 4.3 Obecnost - A vs B nejsou duplicitní
- **Topologie** (které zastávky) - umí oba (A textem, B přes `<`). Překryv.
- **Časy** (kvantitativní) - umí **jen B**. Případ „stejná trasa, jiné časy" je A slepý.
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

### 4.5 Legacy logika rozřazení (z kódu) + dynamický návrh (2026-06-30)

**Co dělá legacy (ověřeno ve zdroji):**
- **`TrasaMin1-15` = časové sloupce.** `JRapiLinPrtMhd1.mac` (ř. 266-300): per spoj se sestaví `prutm`
  (časy per zastávka) + `prutmbmp` (bitmapa obsluhy). **Dedup podle `prutmbmp`** - spoj se shodnou
  bitmapou = stejná varianta (ponechá se delší čas, ř. 292); nová bitmapa → nový sloupec + písmeno
  z `ZnkMinSlp`. Strop 15.
- **`Pracovni2-5` = mrtvé.** `Data.cls` (ř. 342-367) je čte jako markerové sloupce (`znaky^val^znaky^NZ`),
  ale **na žádné reálné AD lince se neplní** (0365001/0410101/0365005 → vždy prázdné `^^^&&`); linkový
  producer je needituje. Legacy strukturní pozůstatek → **ignorovat**.
- **Závěr:** existuje **jeden** reálný mechanismus variant = TrasaMin časy. „15 vs 5" byl falešný poplach.

**Dynamický návrh (bez 15 checkboxů):**
- **Caché:** AD agenda detekuje N *reálných* variant (dedup dle bitmapy obsluhy) a emituje **pole**
  proměnné délky (např. `TRASY[]` = `{písmeno, časy_per_zastávka}`); zpětně kompat. `CAS_JIZDY` = varianta 1.
- **StopList render:** uživatel má v sloupcích pořád **jeden** „Čas jízdy"; při renderu se slot
  **rozvine na N pod-sloupců** dle dat (1 varianta = jako dnes; N variant = N sloupců s písmeny).
- **Strop přehlednosti:** „max variantních sloupců" (default ~2-3); víc variant → fallback na 1 sloupec
  + anotace (mechanismus A, máme). **UI = 1 číslo + sdílený styl, 0 per-varianta checkboxů.**
- **Pracovni neportovat** (mrtvé + redundantní s anotací A).
- **Rozhodnutí (čeká):** implementovat capped TrasaMin NEBO zavřít (anotace stačí) - dle reálné potřeby
  přesných časů vedlejších variant.

---

## 5. Layout a viditelnost legendy/poznámek

**Princip:** v novém světě je layout/viditelnost legendy věc **`NotesLegendElement` (TD)**, ne port
legacy PATCH flagů. Tj. místo čtení `Trid_delK`/`NegZnNR` z Caché je vystavit jako vlastnosti elementu.

| Feature | Legacy | Cíl v novém | Stav |
|---|---|---|---|
| Uspořádat poznámky dle délky | `PATCH,"Trid_delK"` | `NotesLegend.Razeni` (DleDelky*) | ✅ AD-N3 |
| NZ na nový řádek | `PATCH,"NegZnNR"` | legenda = poznámka per řádek | ✅ AD-N4 |
| Layout legendy (1 vs 2 sloupce) | (vizuál) | vlastnost elementu | ⬜ |
| Skrýt agreg. „bezbariér" poznámku | - | parametr `SCHOVAT_AGREGAT_BEZBARIER` (filtr-model) | ✅ #7161 D6 (latentní) |
| Skrýt „x na znamení" když sloupec hidden | - | TD (viditelnost StopList) | ⬜ #7161 D5 |
| Číslování poznámek (TYPPOZ=1 pevná čísla) | firm-config | - | ⬜ deferred |

> **Ruční poznámky (tab MHD) - viditelnost řešena #7955:** parametr `SCHOVAT_V_ROZSAHU` (Ruční /
> Automatické / Obojí) + render-time `NoteVisibilityFilter` nad nadmnožinou z Caché. Viz §6.

---

## 6. Ruční / ad-hoc poznámky linky - tab „MHD" (✅ hotovo)

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

Tyto se v zastávkovém (vývěskovém) JŘ neřeší - patří do **linkových/knižních** JŘ (tarif dle km, přípoje):

- **Přípoje / návaznosti** (`("T",sp,32)`, `NavaznostiZpusobVypisu`) - #11300 KORID. Přesunout do roadmap linkových JŘ.
- **Km sloupce** (kilometrická vzdálenost / tarifní km, `linIDS`/`Kms`) - tarif dle km = linkové téma.
- Celý Win tisk A4/A5, kniha, HTML, odbavení/TELMAX/mikroelektronika - starý engine / mimo.

---

## 9. Co zbývá - business pohled (co to dělá / kde to uvidím)

Popsáno srozumitelně bez legacy zkratek (technický klíč v závorce jako reference). Seřazeno dle
doporučeného pořadí.

### ✅ #7161 D5 - Skrýt vysvětlivku „× na znamení", když značka není zobrazená - HOTOVO (2026-06-21)
- **Co to dělá:** Když uživatel ve vývěsce **odškrtne „Na znamení"** na StopListu, zmizí z **legendy**
  i řádek „× - zastávka na znamení". Legenda automaticky následuje viditelnost značky.
- **Kde to uvidí:** legenda poznámek pod jízdním řádem.
- **Jak:** `NoteVisibilityFilter.ShouldHideNaZnameniLegend(template)` - skryje kategorii `atribut-zastavky`,
  když žádný StopList nemá `ShowSignalStopIndicator`. Čistě TD, žádná Caché změna. Test: CB_GWBUS 0320144.

### 🟡 Layout a uspořádání legendy poznámek
Vše se týká **legendy pod jízdním řádem** - jak se poznámky uspořádají, aby se vešly a byly přehledné.
V novém světě to mají být **vlastnosti elementu legendy** (uživatel si nastaví v designeru), ne skryté
nastavení linky jako v legacy.

- **Legenda na 1 nebo 2 sloupce** - ✅ **HOTOVO** (`NotesLegend.Columns` 1–4 + `WrapSmer` po sloupcích/řádcích; ověřeno 2026-07-10).
- **Řazení poznámek podle délky** (AD-N3, legacy `Trid_delK`) - ✅ **HOTOVO** (`NotesLegend.Razeni` = `DleDelkyVzestupne/Sestupne`).
- **Omezení („nejede…") na nový řádek** (AD-N4, legacy `NegZnNR`) - ✅ **HOTOVO** (legenda renderuje každou poznámku na vlastní řádek).

### 🟡 Vlastní názvy sloupců skupin dnů
- **Co to dělá:** přepsat výchozí nadpisy sloupců odjezdů („Pracovní dny" / „Soboty" / „Neděle a svátky")
  vlastními texty (AD-N6, legacy `ISY_LinkyTiskMHDNadpisy`).
- **Kde to uvidí:** záhlaví mřížky odjezdů (`DepartureGridElement`).

### 🟡 Cizojazyčné poznámky
- **Co to dělá:** tisknout poznámky i v cizím jazyce (AD-N11, legacy `prtCzjPzn`) - pro pohraniční /
  mezinárodní linky. **Kde:** legenda pod JŘ.

### ✅ Více variant jízdních časů (AD-N13) - HOTOVO (L1, 2026-07-02)
- **Co to dělá:** když část spojů jede **jinou trasou / jinak rychle**, ukazujeme víc sloupců časů -
  každá varianta své časy (a „<" u zastávek, které varianta projíždí).
- **Kde:** seznam zastávek vlevo (`StopListElement` - `ShowAllRouteVariants`, dynamické sloupce z `VARIANTY_TRAS`/`TrasaMin`).
- **Stav:** ✅ **HOTOVO** - multi-varianta TrasaMin (L1): dynamický počet sloupců (N dle dat, ne 15 napevno).
  ⚠️ **„1:1 ověřeno gold 0365005" platilo pro písmena a jednosloupcový režim, NE pro počet variantních
  sloupců** - tam se to od legacy lišilo od začátku (opraveno 12. 8., viz níže). Gold digest je hash
  našeho výstupu, takže odchylku od starého modulu nemohl chytit. *(Zbývá „jemnější výběr variant" = `B67`.)*

### 🟢 Dormant (umíme, ale nemáme testovací data)
- **Potlačit negativní značku v některých sloupcích** (AD-N5, legacy `MHD_NEZNZ123`) - vázané na NZ, dnes
  prázdné ve všech firmách.
- **Prázdninové dělení sloupců** (AD-N15) - kód hotový, data prázdná.

---

## 10. Stav hotovo (tato + předchozí session)

**Hotovo a ověřeno 1:1:** hlavička + dopravce, FTL 0-3 sloupce, datumová omezení, **rozdíly tras
(anotace A - neobsluhuje/jede přes/jede jen do/jede až do)**, na znamení, bezbariér, časy jízdy (1
sloupec), zóny, ruční poznámky (Komentáře/nepoznámkovat/směrové), #7955 přepínač, multi-page,
poznámka nad linku, typ JR, skrytí agregátu bezbariér (#7161 D6), skrytí legendy na znamení (#7161 D5).
**Helpdesk zavřeno:** #7955, #7959, #7947, **#7161 (D5+D6)**.

---

## 11. Příloha - `gSetLinPrt` (dialog „Nastavení pro tisk linky", 82 polí)

Relevantní pro zastávkový JŘ (zbytek = ➖ Win tisk/kniha/HTML/odbavení):

Stav sloupce „Máme" opravený 29. 10. podle kódu; přehled za všechny agendy a části nastavení je
v [`nastaveni-tisku-jr.md`](nastaveni-tisku-jr.md).

| dta | Význam | PATCH | Máme |
|---|---|---|---|
| 18 | Tisk ručních názvů zastávek | `nazMHD` | ➖ vývěska nečte, název pro MHD bere vždy (Jan 23. 10., `B193i`); linkový AD ano |
| 20 | Tisk zóny IDS / km (0-3) | `linIDS` | ❌ vývěska nečte, zóny podle šablony; linkový AD ano |
| 26 | Uspořádat poznámky dle délky | `Trid_delK` | ✅ AD-N3 (`NotesLegend.Razeni`) |
| 27 | NZ na nový řádek | `NegZnNR` | ✅ AD-N4 (poznámka per řádek) |
| 28 | Cizojazyčné poznámky | `prtCzjPzn` | ⬜ AD-N11 |
| 30 | Gate auto-poznámek odchylek | `mhd,"not"` | ✅ |
| 32 | Sada písmen markerů | `mhd,"notchar"` | ✅ |
| 35 | Tisk bezbariér (0/1/2) | `TZPA,"BBP"` | ❌ vývěska nečte (bere firemní `PS,LIMES`); linkový AD ano |
| 36 | Rok v omezeních (0-4) | `TiskDtm` | ✅ |
| 73 | Auto generovat časové průběhy | `mhd,"par",1` | ➖ nečte se, varianty se počítají vždy (multi-varianta L1) |
| 74 | Volné značky pro minutové trasy | `mhd,"par",2` | ✅ AD-N13 (multi-varianta L1) |
| 78 | Způsob výpisu návazností | `NavaznostiZpusobVypisu` | ➖ linkové JŘ |

**Reference souborů:** `JRapiLin.mac` (`gPrpLin` ř.282), `JRapiLin1.mac` (`gPrpLinMhd` ř.82,
`gSetLinPrt` ř.183), `JRapiLinPrtMhd1.mac` (FTL/varianty ř.36-300), `JRapiLinPrtMhd.mac`
(`OmTrasa`/`gDopMHD`/`UVOD`), `JRapiNZUtl.mac` (omezení/NZ).



<!-- ══════════════════════════════════════════════════════════════════════ -->
# ==== ČÁST: Typy poznámek a layout legendy (analýza) ====
<!-- ══════════════════════════════════════════════════════════════════════ -->

# Typy poznámek a layout legendy - analýza pro konfigurovatelnost

> **Status (2026-06-30):** layout legendy (1–4 sloupce, řazení dle délky, směr wrapu, zarovnaný
> marker, filtr per typ) je **z velké části implementován** v `NotesLegendElement` - viz changelog
> 2026-06-21/23. Tento dokument = analytický podklad, ne status.

> **Účel.** Srozumitelně (bez legacy zkratek) popsat: jaké **typy poznámek** v zastávkovém JŘ máme,
> jak se dnes zobrazují a nastavují, a **jak umožnit uživateli nastavit layout legendy** (sloupce,
> řazení, viditelnost per kategorie). Podklad pro rozhodnutí, co a jak doplnit do `NotesLegendElement`.
>
> **Týká se TD elementu legendy** (`NotesLegendElement`), ne nastavení linky v Caché. Doplňuje
> `ad-data-mapa.md §5`. **Datum:** 2026-06-21.

---

## 1. Dvě klasifikace poznámky (klíč k pochopení)

Každá poznámka v legendě má **dvě nezávislé vlastnosti**:

### A) TYP - jak řádek VYPADÁ (řídí vykreslení)
| Typ | Jak vypadá | Příklad |
|---|---|---|
| **Negativní značka** | znak v **invertovaném** rámečku (bílé na černém) + text | omezující značka |
| **Speciální znak** | **symbol** (volitelně barevný) + text | „H" bezbariér, „×" na znamení |
| **Text** | prostý text; pokud má znak, znak + oddělovač + text | „B - neobsluhuje zast. X" |

→ V kódu enum `TypPoznamky { Text, NegativniZnacka, SpecialniZnak }`. Přichází z dat (pole „TYP").

### B) KATEGORIE - co poznámka ZNAMENÁ (business; řídí viditelnost)
| Kategorie | Co to je | Kde vzniká | TYP |
|---|---|---|---|
| `rozdil-trasy` | „neobsluhuje / jede přes / jede jen do / jede až do zast. X" | rozdíly tras spojů | Text |
| `omezeni` | „nejede 24.12.…", „jede také…" (datová platnost) | omezení spoje | Text |
| `bezbarier` | „spoj má bezbariérově přístupné vozidlo" (per spoj) | atribut spoje | Speciální znak |
| `bezbarier-agregat` | „**všechny** spoje mají bezbariérové vozidlo" (souhrn) | celá linka bezbariér | Speciální znak |
| `atribut-zastavky` | „× - zastávka na znamení" | atribut zastávky | Speciální znak |
| `rucni-komentar` | ruční poznámka linky (tab MHD Komentáře) | uživatel | Text |
| `smerove` | volná poznámka tam/zpět/vše | uživatel | Text |

→ V datech pole „KATEGORIE" (přidáno kvůli filtrům #7955/#7161). **Zatím se neparsuje do render
modelu `PoznamkaData`** - používá ji jen `NoteVisibilityFilter` (na úrovni dat, před vykreslením).

**Důležité:** TYP a KATEGORIE jsou kolmé. Např. `bezbarier` i `atribut-zastavky` jsou oba „Speciální
znak" (TYP), ale jiná kategorie. Dnešní filtrování v elementu jede podle **TYP** (hrubé) - viz §2.

Kategorie `tydenni-platnost` (kroužkované denní značky) a `varianta-trasy` (písmena variant, opt-in)
v tabulce chybí historicky - existují v datech taky.

### C) AD × MHD - čím se agendy v poznámkách liší (audit 30. 7.)

Obě agendy dělají „totéž" jinak; tohle je rozdílový seznam, ne návrh práce.

| Vlastnost | AD (`ZastavkoveJrAd`) | MHD (`ZastavkoveJrMhd`) |
|---|---|---|
| `KATEGORIE` u značek u minut | ✅ (+ `SPOJ`, `V_ROZSAHU_NEPOZNAMKOVAT`) | ❌ `AddAnotace` je nemá → **filtr viditelnosti poznámek na MHD nefunguje** (a je to záměr: `NoteVisibilityFilter` je agenda-agnostický, netagované značky nikdy neschová) |
| Přidělení písmene rozdílu trasy | ze písmenné sady, **bez ohledu na už obsazené znaky** → kolize s „H"/„F" bezbariéru (9 stránek, blok `B14`) | ze **sdíleného poolu stránky** - obsazený znak přeskočí, kolize nemůže vzniknout |
| Barvy poznámek (`FORE_COLOR`/`BACK_COLOR`) | vždy prázdné (dědí se styl šablony) | plní z číselníku (barevné spoje/poznámky) |
| Textové poznámky **zastávky** | ❌ jen atributy zastávky (znaky) | ✅ `poznamkyZast` = volný text u zastávky v trase |
| Kolize skupin dnů v jednom sloupci | ❌ (řeší se rozdělením sloupců přes FTL) | ✅ `PriraditZnackySmoSkupin` (+ známá díra u shodných ručních značek - viz `cache-business-logic.md`) |
| Ruční komentáře spoje | ✅ `SeT_SpeC`/`MhD_SpeC` | ✅ speciální znaky spoje z číselníku `PoznamkaSpoj` |
| Směrové poznámky linky (tam/zpět/vše) | ✅ `SeT_MhD` | ❌ |
| Souhrnná bezbariérová poznámka | ✅ `bezbarier-agregat` | ❌ |
| Negativní značka invertovaně u minut | ❌ záměrně (CNZ tam jen klasifikuje sloupce dnů) | ✅ |

---

## 1b. Co uměl starý návrhář (NALEZENO ve zdroji)

Legacy návrhář „Tisk MHD" (VB.NET WinForms, `C:\DEV\EDISON\TiskMHD`) měl pro sekci poznámek dialog
**`formTISKMHDSekcePoznamkyEdit`** (tab „Sekce Poznámky - vlastnosti"). Layout pole (struktura `SSekce`,
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
  Tj. ne striktně column-major/row-major - hybrid „pakuj krátké do sloupců, dlouhé full-width".
- Skutečně podporuje **jen 1 a 2 sloupce** (3+ neimplementováno).

→ **Závěr:** sloupce/wrapping **nejde 1:1 portovat** (legacy je VB renderer s vlastní logikou), ale teď
**víme přesně, co uměl** a můžeme to v `NotesLegendElement` navrhnout čistě a klidně lépe (viz 3.1).
Legacy „Trid_delK"/„NegZnNR" (řazení dle délky / NZ na nový řádek) jsou line-flagy mimo tento dialog
(viz `ad-data-mapa.md`).

---

## 2. Co dnes umíme nastavit (NotesLegendElement)

| Nastavení | Co dělá | Vlastnost |
|---|---|---|
| Nadpis | text záhlaví legendy („Poznámky") | `HeaderText` |
| Oddělovač | mezi znakem a textem (" - ") | `NoteSeparator` |
| Velikost písma / výška řádku | font a rozteč | `NoteFontSize`, `NoteLineHeight` |
| Barvy záhlaví/dat | 4 base barvy | (z báze) |
| Barvy negativní značky | pozadí/text invertovaného rámečku | `NegativniZnackaBackColor/ForeColor` |
| **Viditelnost dle TYP** | zap/vyp negativní značky / speciální znaky / textové poznámky | `ShowNegativniZnacky/ShowSpecialniZnaky/ShowTextovePoznamky` |
| Skrytí „× na znamení" | auto dle StopListu (#7161 D5) | filtr |
| Skrytí agregátu bezbariér | parametr (#7161 D6) | filtr |

**Layout dnes:** legenda je **jednosloupcová**, řádky v pořadí, jak je vrací Caché (atributy zastávek
→ bezbariér → rozdíly tras → ruční komentáře → omezení → směrové). Řazení ani počet sloupců se nedá nastavit.

---

## 3. Co by uživatel chtěl nastavit (layout) - a jak to přidat

Princip (dohodnuto): **layout legendy = vlastnosti `NotesLegendElement`** (uživatel v designeru),
ne skryté nastavení linky. Reálné požadavky:

### 3.1 Počet sloupců + směr wrapování (1 / 2 / N) - ✅ HOTOVO (2026-06-21)
**Implementováno:** `NotesLegendElement.Columns` (1-4) + `WrapSmer` (PoSloupcich/PoRadcich) +
`MarkerSirka` (0=inline, >0=zarovnaná buňka značky). Render distribuuje poznámky do sloupců,
výška = `headerH + ceil(N/Columns) × řádek`. V EDInputFormu i inline panelu (skupina „Rozložení").
Default 1 sloupec = beze změny. (Hybrid „dlouhé full-width" z legacy nepřevzat - jednodušší model.)
- **Co:** dlouhá legenda zabírá moc výšky; rozdělit do 2+ sloupců = úspora místa.
- **Směr wrapování (důležité rozhodnutí):**
  - **Po sloupcích (column-major)** - naplní se sloupec 1 shora dolů, pak sloupec 2… (jako noviny).
    Čte se „dolů, pak vpravo". Pro legendu obvykle přirozenější.
  - **Po řádcích (row-major)** - naplní se řádek 1 přes všechny sloupce, pak řádek 2… Čte se
    „vpravo, pak dolů".
- **Auto rozšiřování:** počet řádků na sloupec = `ceil(počet poznámek / Columns)`; volitelně „nalít
  do N sloupců jen když se nevejde na výšku" (auto-wrap při přetečení, jinak 1 sloupec).
- **Legacy chování (reference):** 2 sloupce s **hybridním wrapem** - krátké poznámky se skládají vedle
  sebe do úzkých sloupců, dlouhá poznámka se roztáhne přes celou šířku. Můžeme převzít, nebo nabídnout
  jednodušší `WrapSměr { PoSloupcich, PoRadcich }` + volitelně „dlouhé full-width".
- **Jak přidat:** vlastnosti `Columns: int` (default 1) + `WrapSměr: enum`. Render: rozdělit `filtered`
  do sloupců, kreslit per-sloupec s offsetem X = `Width/Columns × i`. Výška = `headerH + ceil(N/Columns) × řádek`.
- **Bonus z legacy - zarovnaný marker:** legacy měl **levou buňku se znakem o pevné šířce**
  (`SirkaSloupceTextVlevo`) → markery svisle zarovnané. Náš render dnes kreslí marker+text inline
  (nezarovnané). Vlastnost `SirkaMarkeru: float` (0 = inline jako dnes, >0 = pevná buňka) by to dorovnala.

### 3.2 Řazení poznámek - ✅ HOTOVO (2026-06-21)
**Implementováno:** `NotesLegendElement.Razeni` (Vychozi/DleDelky). Default Vychozi = beze změny.
(DleKategorie/DleZnaku zatím ne - YAGNI, snadné doplnit.)
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

1. **🟢 Počet sloupců (3.1)** + **řazení (3.2)** - největší praktický přínos (úspora místa, přehlednost),
   čistě vizuální, žádný dotyk dat. Dvě vlastnosti `NotesLegendElement`. Doporučuji jako první.
2. **🟡 Viditelnost per kategorie (3.3)** - užitečné, ale vyžaduje (a) parsovat KATEGORIE do `PoznamkaData`,
   (b) vyřešit překryv s D5/D6. Až po 1, s rozmyslem o interakci.
3. **🟢 Styl per typ (3.4)** - odložit (YAGNI).

**Pozn. k testování:** layout (sloupce/řazení) jde ověřit na jakékoli lince s bohatou legendou -
**CB_CSADAD 0365005** (6 řádků: B/C/E/F/G/H + omezení) je ideální.

---

## 5. Shrnutí - co máme vs co přidat

| | Stav |
|---|---|
| TYP render (NZ / speciální / text) | ✅ máme |
| Viditelnost per TYP | ✅ máme (hrubé) |
| Barvy (záhlaví, data, NZ, symbol z DB) | ✅ máme |
| Skrytí na znamení / agregát bezbariér | ✅ máme (#7161 D5/D6) |
| **Počet sloupců + směr wrapu** | ✅ hotovo (3.1) - `Columns`, `WrapSmer` |
| **Zarovnaný marker** (pevná buňka značky) | ✅ hotovo (3.1) - `MarkerSirka` |
| **Řazení poznámek** | ✅ hotovo (3.2) - `Razeni` |
| **Viditelnost per KATEGORIE** | ⬜ přidat (3.3, řeší překryv s D5/D6) |
| Styl per typ | ⬜ odložit (3.4) |
