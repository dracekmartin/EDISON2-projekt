# Linkový / knižní JŘ — analýza pro implementaci (podklad pro Fable)

> **Účel dokumentu.** Podklad k předání jinému AI agentovi (Claude Fable), aby zkusil **lokálně**
> naimplementovat novou agendu + element pro **linkový/knižní jízdní řád** v TemplateDesigneru,
> na základě legacy Caché kódu. Toto je **pouze analýza** — žádný kód zatím nevznikl. Než Fable
> začne, měl by si tento dokument znovu ověřit proti aktuálnímu stavu repa (roadmapa se mohla
> mezitím posunout) a přečíst navázané `.claude/rules/*.md` soubory (cache-api.md, mvvm-patterns.md,
> new-element-checklist.md, architektonicka-rozhodnuti-agendy.md).

**Datum analýzy:** 2026-07-04

---

## 1. Co to je a proč na tom záleží

Systém má dnes hotový **zastávkový JŘ** (agenda `ZastavkoveJrAd` + `ZastavkoveJrMhd`) — tiskne se
**1 zastávka × 1 směr** na stránku (vývěska na zastávce). Legacy ekvivalent: `ALVA.TISKMHD`.

**Linkový/knižní J�Ř** (legacy `ALVA.TISKJR`) je **jiný formát**: **celá linka v jednom tisku** —
svislý seznam zastávek × vodorovné sloupce jednotlivých **spojů** (ne skupin dnů!) s časy v buňkách.
Vylepuje se na zastávky v linkové (meziměstské) dopravě a tiskne se v knižních JŘ publikacích.
Legacy generuje oba výstupy (vývěsní i knižní) ze stejného enginu.

**Proč je to jiný, těžší problém než zastávkový JŘ:**
- Osa sloupců jsou **konkrétní spoje**, ne skupiny dnů → matice [zastávka × spoj], blíž
  `TableElement` než `DepartureGridElement`.
- **Celá linka na jednu sestavu** vyžaduje 2D bin-packing stránkování (sloupce i řádky se lámou
  přes stránky) — dnešní `PageLayoutEngine` to neřeší, je to nová layoutová schopnost.
- Vyžaduje **fixed-width layout** (legacy trik: vkládání prázdných sloupců, aby všechny stránky
  vyšly na stejnou šířku) a **auto-fit fontu na šířku** (dnešní `ComputeFontScale` je jen výškový).
- Vyžaduje **proprietární symbolový font** (`Timetable_nove`/SK, 66+ dopravních symbolů) —
  jiný a rozsáhlejší číselník než `ConvertSpecialniZnak` u MHD.
- Řádky **Vyčká/Navazuje** (návaznosti) jsou nový koncept, který zastávkový JŘ nepotřeboval.

Odhad v existující analýze (`analyza-stav-a-legacy-nahrada-2026-07.md`): srovnatelná velikost
práce jako celý zastávkový JŘ (~1 měsíc čistého času), ale metodika (čistá reimplementace proti
legacy gold oracle) je už vyzkoušená a funguje.

---

## 2. Stav v projektu — kde to je zapsané, co (ne)existuje

**V TemplateDesigneru dnes nic neimplementováno.** Žádný element, žádná agenda, žádný Caché kód.
Existuje jen dokumentace a roadmapa:

| Zdroj | Co říká |
|---|---|
| `CLAUDE.md` §Aktuální focus | Zastávkový JŘ je hotový 1:1; linkový/knižní je next big track, zatím nezahájený |
| `.claude/docs/legacy-schedule-generators.md` §2 | Business popis `ALVA.TISKJR` — zóny, km, Tč, obousměrnost, PostScript fixed-width trik, návaznosti |
| `.claude/docs/analyza-stav-a-legacy-nahrada-2026-07.md` §3.3 | Nejpodrobnější — rozsah (33 400 LOC VB + 3 290 Caché), navržené kroky, gold oracle `mkPLinWin` |
| `.claude/docs/roadmap.md` „Fáze C+D" (řádky 132–163) | Kroky 42–44 (analýza referenčních výstupů, symbolový font, `LineTableElement`, agenda `KnizniJrAd`) — **vše ⬜ nezahájeno** |
| `.claude/docs/architektonicka-rozhodnuti-agendy.md` | ⚠️ **Gate před krokem 43** — R1 (sdílené tabulkové jádro StopList+DynamicTable+LineTable), R2 (layout zodpovědnosti), R5 (embedded fonty) — **přečíst před návrhem elementu** |
| `.claude/docs/cache-database-linky.md` §„Dva formáty linkového JŘ" | Rozlišuje „klasický sloupcový" (zastávky×spoje, doložený reálnou linkou) vs „MHD minutová mřížka" — **toto je popis dat pro ZastavkoveJrAd, ne pro LineTableElement**; nepřeskakovat, ale nezaměňovat účel |

**Roadmapa čeká přesně na tuto práci** — kroky 42–44 ve Fázi C+D jsou navržené, ale nikdo je
nezačal. Tento dokument je fakticky rozpracování kroku 42 („Analýza referenčních výstupů + wire
formát").

---

## 3. Legacy zdroje — kde je skutečný kód (lokálně dostupný)

Legacy VB.NET + Caché kód **existuje lokálně** na této stanici (jiný git repo, ne EDISONMAIN2):

```
C:\DEV\EDISON\TiskJR\                              — VB.NET klient (~33 400 LOC)
  CACHE\ALVA\TISKJR\Data.cls                        — formátovací/překladová vrstva (1644 řádků)
  CACHE\ALVA\TISKJR\Main.cls                        — CRUD sestav/šablon (232 řádků) — precedens, ne datový zdroj
  CACHE\ALVA\TISKJR\Sestavy.cls                     — správa sestav (399 řádků) — precedens, ne datový zdroj
  CACHE\ALVA\TISKJR\Designer.cls, Images.cls
  _ruzne\                                            — TTF fonty (Timetable_nove aj.)

C:\DEV\EDISON\TiskJRRELACE\                         — slovenský fork (~49 700 LOC) — SAMOSTATNÝ use case, viz §7

C:\DEV\EDISON\JRMAIN\CACHE\JRapiLinPrtWin.mac        — ⭐ SKUTEČNÝ ORACLE, 4 224 řádků
                                                        obsahuje mkPLinWin, WinTiskMhd^JRapi, mkPRelWin
C:\DEV\EDISON\JRMAIN\CACHE\ALVA\JRMAIN\MLAPI.cls     — wrapper třída volající mkPLinWin/mkPRelWin
C:\DEV\EDISON\JRMAIN\DOCS\06-sestavy-tisk.md         — vysokoúrovňový přehled tiskových kanálů JRMAIN
C:\DEV\EDISON\JRMAIN\DOCS\07-cache-api.md            — API detaily
```

**Klíčový poznatek (potvrzeno čtením kódu):** `ALVA.TISKJR.Data.LoadData` je **jen tenká
překladová vrstva** — skutečná byznys logika (výběr spojů, platnosti, bin-packing, zóny, poznámky)
žije v `mkPLinWin` (soubor `JRapiLinPrtWin.mac` v JRMAIN, ne v TiskJR). **Reimplementace musí
vycházet primárně z `JRapiLinPrtWin.mac`**, `TISKJR/Data.cls` slouží jen k pochopení, jak se
legacy struktura mapuje na VB.NET klienta (užitečné pro pojmenování polí, ne pro algoritmy).

Datový zdroj (globály `^JRL`/`^JRZ`/`^JRSET`) je **stejný** jako u `ZastavkoveJrAd` — žádná nová
databázová vrstva navíc, jen jiná byznys logika nad stejnými daty. Mentální model globálů je
zdokumentovaný v `.claude/docs/cache-database-linky.md` §1–8 (netřeba znovu objevovat).

---

## 4. `mkPLinWin` — signatura a I/O kontrakt

```objectscript
mkPLinWin(idfa, nmsp, skuv, lin, glbout, in, out) public
```

- `skuv` = skupina linek, `lin` = číslo linky, `glbout` = **jméno** cílového globálu (zapisuje se
  přes `@glbout@(...)` indirection), `in`/`out` jsou `ByRef` pole.

### Klíčové vstupní přepínače (`in("A",2,...)`)

```
in("A",2,"arr",3)  = 0/1/2   členění odjezdových sloupců/skupin dnů (viz §5)
in("A",2,"arr",4)  = 0..4    umístění poznámek (per blok / per směr / souhrnně / +odstránkovat)
in("A",2,"arr",5)  = czst    zvýrazněná zastávka
in("A",2,"arr",6)  = 0/1     tisk turnusů ke spojům
in("A",2,"arr",7)  = 0/1     oboustranný tisk (T+Z na jednu sestavu)
in("A",2,"arr",9)  = 0/1     tisk čísla linky/vlaku v přípojných řádcích (Vyčká/Navazuje)
in("A",2,"arr",10) = tc/""   zvýraznění konkrétního tarifního čísla
in("A",2,"arr",13) = 0/1     vkládat prázdné sloupce (fixed-width padding)
in("A",2,"dta",1)  = šířka tiskové plochy (v „měřicích znacích")
in("A",2,"dta",2)  = počet řádků pro tisk JŘ
in("A",2,"MS",typ,podtyp) = šířka sloupce daného typu — VIZ NÍŽE, klient-driven layout
```

**⚠️ Architektonická past, kterou nechceme replikovat:** legacy je **klient-driven, dvoufázový
layout** — VB.NET klient nejdřív *měří text ve skutečném tiskovém fontu* a posílá zpět serveru
„počet měřicích znaků" (`in("A",2,"MS",...)`), server podle toho určuje zlomy stránek. To je
ping-pong přes síť. **V TD tohle odpadá** — `SkiaHeightCalculator` měří lokálně na klientu (C#),
takže není potřeba tenhle roundtrip; jen je potřeba **replikovat výsledek** (jaké sloupce/řádky
kam patří), ne mechanismus měření.

### Výstup `out("dta",idx1)` — skalární metadata linky

Typ JŘ (obyčejný/objížďkový/návrh), číslo, název, platnost od/do, poznámky nad linku / pod
dopravce, QR kód, řádky textu dopravce, ID dopravce, existence loga. Analogie k `stDBField`
sekci u zastávkového JŘ (`GetSchema`/skalární pole v `ZastavkoveJrAd`).

---

## 5. Výstupní struktura `glbout` — objektový model

Na rozdíl od `ZastavkoveJrAd` (čistě JSON-per-page, viz `cache-database-linky.md`), legacy tady
používá **objektově orientovanou strukturu**: `idobj` = číslo objektu (datový paket), každý
objekt je buď **datový blok** (typ 2 = blok odjezdů) nebo **poznámkový blok** (typ 3), navzájem
provázané (`arr,5,idobj2`). Tohle je přímý zdroj pro „flow skládání datových paketů" zmíněné
v roadmapě (Fáze C+D, bod 2 prerekvizit) — N bloků vedle sebe i pod sebe podle místa.

```
@glbout@(idobj,"row",radek,"arr",1) = typ řádku:  1=platnost, 2=zastávka, 3=vyčká, 4=navazuje, 5=text
@glbout@(idobj,"row",radek,"arr",2) = podbarvení: 0=žádné, 1=25% šedá (liché TČ), 2=přípojný, 4=inverzní
@glbout@(idobj,"row",radek,"arr",3) = podtržení:  0/dole/nahoře/obojí

@glbout@(idobj,"column",sloupec,"arr",1) = typ sloupce: 1=Zóna, 2=Km, 3=Tč, 4=Zastávky, 6=MHD, 7=Spoj
@glbout@(idobj,"column",sloupec,"arr",3) = číslo spoje (jen typ 7)
@glbout@(idobj,"column",sloupec,"arr",4) = skupina odjezdů (1/2/3 dle in("A",2,"arr",3))

@glbout@(idobj,"dta",radek,sloupec,idx1,"arr",1) = typ buňky: 1=text, 2=symbol, 3=NZ, 4=čas, 5=zmenšený text
```

**Doporučení pro nový wire formát (R3 — čistě JSON, žádný `$lb`/objektový model):** tohle je
přesně ten typ struktury, kterou je potřeba **přenavrhnout od nuly jako JSON**, ne mapovat 1:1.
Návrh: pole `columns[]` (typ + metadata jako u `TableColumn`), pole `rows[]` (typ řádku + buňky),
podobně jako dnešní `TableElement`/`DynamicTableElement` JSON kontrakt — matice zastávka × spoj
mapuje se přirozeně na sloupcovou tabulku s speciálními typy řádků (platnost/vyčká/navazuje/text).

---

## 6. Byznys logika — detaily nutné pro 1:1 shodu

### 6.1 Zóny, km, tarifní číslo, MHD sloupec

- **Zóna** (sloupec typ 1): text z `z(tc,"gTrsLin","dta",11)`; šířka řízena max délkou textu (3
  podtypy).
- **Km** (sloupec typ 2): pro směr Tam přímo; pro směr Zpět se **odečítá od max km na trase**
  (`mymaxkm - km`). Hodnota `-1` → zobrazí se jako `<` („jede jinudy" — spoj se odchyluje od
  hlavní trasy).
- **Tarifní číslo** (sloupec typ 3): lze vypnout globálně i per-linka; **liché TČ** → podbarvený
  řádek (viz `row,"arr",2`=1).
- **MHD** (sloupec typ 6): mapuje interní kódy na text „MHD"/„IDS"/vlastní IDP-P/IDP-Z; sloupec
  se **automaticky odstraní**, pokud je pro celou linku prázdný — důležité pravidlo, replikovat.
- **Zastávky** (sloupec typ 4): název ze standardního pole nebo upravený MHD název; atribut trasy
  `"S"` (stanice) připojuje „, stan.N"; atributy `"p","o","J","P","T"` mohou **rozdvojit řádek**
  na samostatný příjezd+odjezd (frakční index `row - 0.01`) — edge case, který StopList dnes neřeší.

### 6.2 Skupinová záhlaví PD/So/Ne — „3 režimy dělení"

Řízeno `in("A",2,"arr",3)`:

| Hodnota | Rozdělení |
|---|---|
| `0` | bez rozlišení — jedna skupina |
| `1` | dva bloky: (X/1-5 pracovní) / (6/7/+ víkend+svátky) |
| `2` | tři bloky: (X/1-5) / (6 sobota) / (7,+ neděle+svátek) |

Platnost spoje: `mkSttKalLin` + `mkStaLinSp`; **bez explicitní platnosti fallback = tiskne se
všude** (fix HD 10183, 8.4.2025 — behaviorální detail hodný gold-oracle testu). Manuální override
`MHD2/umeleDatDoSekci`.

### 6.3 Auto-fit a 2D bin-packing — `Strih()`

Dvoufázový algoritmus:
- `typstrih=0` — úsporný, ignoruje skupiny dnů (nejmíň stránek).
- `typstrih=1` — skupiny jako samostatné bloky, ale **jen pokud to nezhorší celkový počet
  stránek** (heuristika, ne tvrdé pravidlo).

Řezy (`cutx`/`cuty`) = greedy bin-packing sloupců/řádků do šířky/výšky tiskové plochy, s podporou
ručního zlomu na konkrétním TČ (`pset("SET",3,"arr",1,8)`). **Toto je nová layoutová schopnost,
kterou `PageLayoutEngine` dnes nemá** — dnešní engine dělá vertikální flow sekcí, ne 2D chunking
sloupců tabulky napříč stránkami. Prázdné padding sloupce (`in("A",2,"arr",13)`) sjednocují počet
sloupců napříč stránkami — legacy PostScript trik zmíněný v `legacy-schedule-generators.md`.

### 6.4 Deset druhů poznámek — pořadí je KONFIGUROVATELNÉ (rozdíl oproti AD!)

| # | Druh | Zdrojové klíče |
|---|---|---|
| 1 | Týdenní platnost | `X,1..7,+` |
| 2 | NZ (negativní značka) | číselné klíče `>9` |
| 3 | Vyčká/navazuje/pokračuje | `u`,`m`,`^` |
| 4 | Poznámka pro spoj/TČ | `p` (+ prefix `MHD2~`) |
| 5 | Atributy | `BEV,IDS,NKA,OBC,PCZ,PJI,POS,RBU,SAO,CBE` |
| 6 | Místenkování | `&,AMS,R,Ü,þ` |
| 7 | Stálé značky linky | `<,MHD,\|,auto,vlak,x` |
| 8 | Poznámky linky — tarifní | `C1` |
| 9 | Poznámky linky — všeobecné | `C3` |
| 10 | Poznámky linky — radiobus | `C4` |

**Důležitý rozdíl oproti `ZastavkoveJrAd`:** u AD je pořadí legendy pevná konstanta (ověřeno a
zdokumentováno v CLAUDE.md — „Pořadí legendy" 1:1). U linkového JŘ je pořadí **uživatelsky
konfigurovatelné** přes parametr `PoznamkyPoradiDruh` (`TISKJR/Data.cls` ř. 113, 438). Nová agenda
buď replikuje jako server-side parametr (konzistentní s existujícím vzorem „Rozdělení sloupců"
u `ZastavkoveJrAd`), nebo se vědomě zdokumentuje jako gap k doplnění později.

### 6.5 Symbolový font `Timetable_nove` (+ SK varianta)

66+ dopravních symbolů, mapovací tabulka `Main.cls` (legacy VB) řádky cca 35–107; záporné kódy =
composite symboly kreslené kódem (ne jeden glyph). TTF soubory v `C:\DEV\EDISON\TiskJR\_ruzne\`.
**Toto je tvrdá prerekvizita** — bez převodu fontu do SkiaSharp (embed TTF nebo vlastní SVG glyph
sada + převodní tabulka analogická `ConvertSpecialniZnak`) nejde žádný obsah s dopravními symboly
vykreslit korektně. Roadmapa to řadí jako krok 42b, **před** `LineTableElement` (krok 43).

### 6.6 Řazení spojů a časy

`mkPoradiSp^JRapi` + filtrace dle platnosti. Čas: `$$$NT2` (sekundy → HH:MM). Markery: `"<"` =
jede jinudy, `"|"` = projíždí bez zastavení. Řádky Vyčká/Navazuje se vkládají jako **frakční
indexy** mezi celočíselné indexy zastávek (`tc2row(tc) ± 0.1`) — technika řešení „vlož řádek
mezi existující" bez renumbering.

---

## 7. TiskJRRELACE — vědomě mimo scope tohoto kroku

Slovenský fork (~49 700 LOC), třetí odlišný výstup (řádky = odjezdy, sloupce = linka/čas/směr/
nástupiště) — blíž zastávkovému JŘ, ale ne identický. **Open question v `legacy-schedule-generators.md`:
je to živý use case?** Roadmapa (`roadmap.md` Fáze E) říká „nejdřív ověřit živý use case (Q3)".
**Doporučení: nezahrnovat do prvního implementačního pokusu.** Zaměřit se čistě na `mkPLinWin`
(klasický linkový/knižní), Relace je samostatné rozhodnutí až po ověření u zákazníka.

---

## 8. Klíčové architektonické rozdíly oproti `ZastavkoveJrAd` (shrnutí)

| Aspekt | ZastavkoveJrAd (hotovo) | Linkový/knižní JŘ (chybí) |
|---|---|---|
| Rozsah 1 tisku | 1 zastávka × 1 směr | Celá linka (všechny zastávky × všechny spoje) |
| Osa sloupců | Skupiny dnů (DepartureGrid) | Konkrétní spoje (matice, blíž TableElement) |
| Stránkování | Vertikální flow (PageLayoutEngine) | 2D bin-packing (sloupce i řádky se lámou) — **nová schopnost** |
| Layout šířky | Pevný layout, žádný auto-fit na šířku | Auto-fit fontu na šířku + fixed-width padding trik |
| Font | Arial/systémový + `ConvertSpecialniZnak` (dny + pár ASCII) | Proprietární `Timetable_nove` — 66+ symbolů, **prerekvizita** |
| Nové řádkové typy | — | Vyčká / Navazuje (frakční indexy mezi zastávkami) |
| Pořadí poznámek | Pevná konstanta (1:1 ověřeno) | Konfigurovatelný parametr (`PoznamkyPoradiDruh`) |
| Datový zdroj | `^JRL`/`^JRZ`/`^JRSET` (JRMAIN) | **Stejný** zdroj, jiná byznys logika (`mkPLinWin`, ne přes `ALVA.TISKMHD.Data`) |
| Wire formát oracle | JSON-per-page (jednoduché) | Objektový model s `idobj`/`row`/`column`/`dta` — **přenavrhnout jako JSON od nuly** |

---

## 9. Doporučený postup implementace (pro Fable)

Návrh sekvence, respektující gate v `architektonicka-rozhodnuti-agendy.md` (R1/R2/R5) a
existující roadmapové kroky 42–44:

1. **Nejdřív přečíst `architektonicka-rozhodnuti-agendy.md`** — než navrhovat `LineTableElement`,
   ověřit rozhodnutí o sdíleném tabulkovém jádru (má `LineTableElement` sdílet infrastrukturu
   s `TableElement`/`DynamicTableElement`, nebo je to svébytný element jako `ScheduleElementBase`
   větev?).
2. **Krok 42b — symbolový font.** Bez převodu `Timetable_nove` do SkiaSharp nejde nic vizuálně
   ověřit. Navrhnout: embed TTF + mapovací tabulka (`GSId()` v `JRapiLinPrtWin.mac`, cca ř.
   1550–1636, je centrální znakový číselník — začít tam).
3. **Wire formát návrh (R3, čistě JSON)** — nová Caché agenda `KnizniJrAd` (jméno z roadmapy),
   `GetData`/`GetMockData` vrací JSON strukturu inspirovanou `columns[]`/`rows[]` (viz §5 návrh),
   ne 1:1 kopii `idobj`/`arr` legacy modelu.
4. **`LineTableElement`** — nový element (viz `.claude/rules/new-element-checklist.md` pro
   kompletní checklist kroků: model, JsonDerivedType, factory switche, toolbar, dynamická výška,
   GetSummary). Klíčová odlišnost od `TableElement`: podpora řádkových typů (platnost/vyčká/
   navazuje) a per-sloupec = per-spoj metadata (číslo spoje, TČ zvýraznění).
5. **Layout rozšíření** — 2D bin-packing (flow paketů + auto-fit šířky) je nad rámec jednoho
   elementu, zasahuje do `PageLayoutEngine`. Pro první lokální pokus **doporučeno zjednodušit**:
   nejdřív ověřit datovou/vizuální správnost na jedné lince/stránce (bez multi-page bin-packing),
   plnou stránkovací logiku řešit až v navazujícím kroku.
6. **Gold oracle test** — `mkPLinWin` lze volat přímo přes `cache_query` (stejný vzor jako
   u `ZastavkoveJrAd`, viz `cache-database-linky.md` §9 pro analogický příklad s `LoadData`).
   Ověřit na reálné lince s `^JRL` daty (kandidát z `cache-database-linky.md`: `CB_GWBUS` /
   `0320113`, pokud má vhodný formát — jinak najít jinou realnou linku).

**Rozsah pro první lokální implementaci (dle zadání „pouze lokálně... abychom to portli"):**
doporučuji **necílit na plnou produkční paritu** hned napoprvé. Minimální smysluplný řez:
- jedna linka, jeden směr, bez bin-packing multi-page (stránkuje se jen když se fyzicky nevejde,
  bez legacy `Strih()` heuristiky),
- symbolový font aspoň částečně (nejpoužívanější symboly, ne všech 66+),
- pořadí poznámek pevné (ne konfigurovatelné) jako první iterace,
- Vyčká/Navazuje řádky ano (jsou strukturálně jednoduché, jen frakční indexování).

Tohle dá funkční vertikální řez k ověření s kolegy/zákazníkem, než se investuje do plné 2D
bin-packing a fontové infrastruktury.

---

## 10. Otevřené otázky (přenést do `legacy-schedule-generators.md`, pokud se zodpoví)

- `ALVA.TISKJR` — umí i MHD, nebo je čistě linkový? (stále nezodpovězeno, viz legacy doc)
- Je vhodné `LineTableElement` stavět jako samostatnou hierarchii, nebo rozšířit `TableElement`
  o řádkové typy? → rozhodnout dle gate R1 v `architektonicka-rozhodnuti-agendy.md`.
- Prioritní zákazník/linka pro gold-oracle validaci — potřeba reálná data s dostatečně bohatým
  obsahem (zóny, Tč, MHD sloupec, aspoň jeden Vyčká/Navazuje řádek).

---

## 11. Stav implementace (2026-07-04, Fable — lokální vertikální řez, NECOMMITNUTO)

První lokální implementace hotová a ověřená end-to-end. Rozsah dle dohody: vertikální řez
(1 linka / 1 směr na stránku), standalone element (R1 extrakce jádra odložena na ostrou
implementaci), symboly jako textový fallback (font = krok 42b), kompilace na dev server.

**Co vzniklo:**

| Artefakt | Kde | Stav |
|---|---|---|
| Caché agenda `ALVA.TemplateDesigner.Agendy.KnizniJrAd` | `CACHE/ALVA/TemplateDesigner/Agendy/KnizniJrAd.cls` (nový soubor) | ✅ zkompilováno na dev, auto-discovered v AgendyRegistr (`KNIZNI_JR_AD`) |
| `LineTableElement` | `dotnet/Models/Elements/LineTableElement.cs` (nový) + registrace (ElementTypes, JsonDerivedType, GetSummary, factory switche, toolbar „Knižní JŘ (linka)", SkiaHeightCalculator) | ✅ build OK, render ověřen harness PNG |
| Wire formát | dokumentován v class docstringu KnizniJrAd.cls | ✅ sémantický JSON (ne 1:1 legacy idobj model) |
| Dev harness | scratchpad `LineTableHarness` (mimo repo) | živá data → PNG |

**Wire formát (finální tvar):** 1 stránka per (linka × směr); `values` = hlavička linky
(CISLO_LINKY, POPIS_LINKY, POZNAMKA_NAD_LINKU, POPIS_DOPRAVCE, PLATNOST_*, TYP_JR, SMER,
POPIS_SMER…); `rows.ZASTAVKY` = svislá osa (PORADI/CZST/POPIS/TC/ZONA/KM[]/ATRIBUTY[]/NASTUPISTE);
`rows.SPOJE` = sloupce (CISLO_SPOJE/SKUPINA/NZ/PLATNOST[]/CASY[] zarovnané indexem na ZASTAVKY
s markery `|` projíždí a `<` jede jinudy/PRIPOJE[]); `rows.SKUPINY_DNU`; `rows.POZNAMKY`
(DRUH 1-10, TYP text/znak/nz). Spoj ve víc skupinách dnů = emitován vícekrát (jako legacy).

**Ověřeno proti gold oracle** (`ALVA.TISKJR.Data/LoadData` s `VOLANO_Z~JRMAIN`, CB_GWBUS 1/0320113;
dump ve scratchpadu `oracle_0320113.txt`): řazení spojů (3,5,7,11,9,13 = dle odjezdu), NZ per spoj
(39/—/70/—/70/32), časy vč. markerů, zóny, km obou sloupců vč. přepočtu Zpět (max−km), návaznosti
(spoj 5→16, 11→12 vč. času a textu), NZ texty datumově znak-po-znaku („nejede 24.12.,31.12.",
„od 21.12. do 5.1.,31.1., od 3.2. do 7.2.…" — knižní formát bez roku, jiný než zastávkový),
tarifní poznámka C1. **Klíčový nález:** W-věta rozlišuje TŘI stavy času — číslo / `|` (projíždí) /
`<` (jede jinudy) — `ZastavkoveJrAd.CasHodnota` zná jen `<`; při extrakci sdílených helperů pozor.

**Vědomé mezery prvního řezu** (soupis i v docstringu třídy): Strih 2D bin-packing + padding
sloupce (layout řeší TD), turnusy, obousměrný tisk, MHD sloupec (J-věta dta6 mapování neověřeno),
rozdvojení řádku příjezd/odjezd, poznámky druhů 4/5/6, konfigurovatelné pořadí poznámek
(`PoznamkyPoradiDruh`), texty druhu 1 hardcoded (legacy `^JRSET("NCOME")`), řazení spojů jen dle
odjezdu (ne plné `mkPoradiSp`), skupinové nadpisy hardcoded (legacy WinMHD DSC), symbolový font.

**Další krok k ostré verzi:** krok 42b (font Timetable), R1 extrakce sdíleného řádkového jádra
(DynamicTable+StopList+LineTable), plné 1:1 porovnání proti mkPLinWin na více linkách (KV_DP,
CB_CSADAD), rozhodnutí o commitnutí tohoto řezu.

---

## 12. Druhá vlna — „1:1" push (2026-07-04, autonomní pokračování)

Commit prvního řezu = `47e5be4`. Následně **multi-linkový diff harness** (scratchpad
`LineTableHarness`, režim `diff`): stáhne legacy `ALVA.TISKJR.Data/LoadData` stream i náš JSON pro
6 linek × 4 firmy a porovná spoje/pořadí/NZ/časy/km/zóny/MHD/poznámky. Výsledek po iteracích:
**22/24 linek zcela beze změn** (CB_CSADAD, CB_GWBUS, KV_DP, VS_CSAD).

**Doplněno do agendy (dle verbatim spec agentů z JRapiLinPrtWin.mac + JRapiNZUtl2/3 + JRapiSp):**
- prázdná buňka = mimo trasu; W-věta markery `<`/`|` 1:1 (symbolová mapa diffu: 74=`|`, 75=`<`)
- řazení spojů 1:1 mkPoradiSp (odjezd z výchozí + bublinové doseřazení přes TcOd/TcDo tč)
- zdvojené řádky příjezd/odjezd (J-atributy P/J; wire `ZASTAVKY.TYP="prijezd"`; ořez krajů per spoj)
- plný řádek platnosti: X..+ → místenka R/þ/&/Ü (hlava:6+11) → u/p (omz34/41/42) → atributy spoje
  (hlava:12-26 dle gSpLin mapy: POS/IDS/BEV/SAO/PCZ/OBC/PJI/NKA/RBU/CBE/RBP/wifi/zasu), pevné pořadí Spoj2Tb
- klasifikace do skupin dnů: Plk; bez Plk odvození z datumů omezení (víkend/svátek vč. Velikonoc
  — computus; den = číslice i „+" současně) — aproximace mkStaLinSp kalendáře
- poznámky druhů 1-10: NCOME/STALCOME texty (rec=CZ/*SK), TiskDtm režimy datumů (0/1/2/3),
  pořadí omezení 13,12,11,SU,LI,22; sudé/liché týdny (hlava:8); návaznosti agregované per (spoj×tc)
  s AVL slovníkem (A=autobus, V=vlak, L=loď) a gate typVýstupu/typZobrazení (omz31 :4/:7, omz32 :4/:9);
  pokračuje (omz34); atributy generická věta; místenkování; stálé značky z J-atributů + ^JRZ $8
  + W piece5 (trasové značky V/N/x/G/a/b/c); poznámky linky s `#`-odstavci (prázdný → mezera)
- zóna jen u IDS linek (PATCH linIDS), prázdná → „-"; MHD sloupec z J-věty `#`-pole 4
  (gTrsLinDta2ToText mapování) s auto-remove

**Element:** MHD sloupec (auto-hide), příjezdové podřádky, přípojné řádky jen s RADEK=1.

**Výchozí šablona:** `TemplateDesigner/templates/knizni-jr-ad-vychozi.json` (hlavička s placeholdery,
LineTableElement, legenda přes DynamicTable POZNAMKY) — generovaná harness režimem `template`,
vizuálně ověřená na 0320680 (2 strany, zdvojené řádky, skupiny dnů, legenda). Zapojení do
distribuce šablon = blok B2/R6 (zatím ruční Import JSON).

**Zbývající známé odchylky (dokumentované, nekritické):**
- pořadí časově identických „dvojčat" spojů (0945001) — obsah 1:1, jen pořadí sloupců
- názvy zastávek v návaznostech u městských linek (0945001: legacy zkrácený místní tvar bez obce)
- počet přípojných řádků 0330510 Z (legacy 5 vs my 8 — legacy zřejmě slučuje conn do jednoho řádku)
- druh 7: symbol 108 „jen pro vystupování" na 0320113/118 (zdroj per-spoj V značky nedohledán)
- gating atributů nastavením PAtrSp/TZPA (legacy umí BEV ap. potlačit konfigurací)

---

## Zdrojové soubory citované v této analýze

```
C:\DEV\EDISON\JRMAIN\CACHE\JRapiLinPrtWin.mac                — hlavní oracle (mkPLinWin, mkPRelWin)
C:\DEV\EDISON\JRMAIN\CACHE\ALVA\JRMAIN\MLAPI.cls              — wrapper volající mkPLinWin
C:\DEV\EDISON\JRMAIN\DOCS\06-sestavy-tisk.md                  — přehled tiskových kanálů JRMAIN
C:\DEV\EDISON\JRMAIN\DOCS\07-cache-api.md                     — API detaily
C:\DEV\EDISON\TiskJR\CACHE\ALVA\TISKJR\Data.cls               — formátovací vrstva → VB.NET klient
C:\DEV\EDISON\TiskJR\CACHE\ALVA\TISKJR\Main.cls               — CRUD sestav (precedens, ne data)
C:\DEV\EDISON\TiskJR\CACHE\ALVA\TISKJR\Sestavy.cls            — správa sestav (precedens, ne data)
C:\DEV\EDISON\TiskJR\_ruzne\                                  — TTF fonty (Timetable_nove aj.)

TemplateDesigner\.claude\docs\legacy-schedule-generators.md
TemplateDesigner\.claude\docs\analyza-stav-a-legacy-nahrada-2026-07.md
TemplateDesigner\.claude\docs\roadmap.md  (Fáze C+D)
TemplateDesigner\.claude\docs\cache-database-linky.md
TemplateDesigner\.claude\docs\architektonicka-rozhodnuti-agendy.md
TemplateDesigner\.claude\rules\new-element-checklist.md
TemplateDesigner\.claude\rules\cache-api.md
```
