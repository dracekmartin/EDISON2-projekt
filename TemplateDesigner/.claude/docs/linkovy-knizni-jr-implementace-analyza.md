# Linkový / knižní JŘ - analýza pro implementaci (podklad pro Fable)

> **Účel dokumentu.** Podklad k předání jinému AI agentovi (Claude Fable), aby zkusil **lokálně**
> naimplementovat novou agendu + element pro **linkový/knižní jízdní řád** v TemplateDesigneru,
> na základě legacy Caché kódu. Toto je **pouze analýza** - žádný kód zatím nevznikl. Než Fable
> začne, měl by si tento dokument znovu ověřit proti aktuálnímu stavu repa (roadmapa se mohla
> mezitím posunout) a přečíst navázané `.claude/rules/*.md` soubory (cache-api.md, mvvm-patterns.md,
> new-element-checklist.md, architektonicka-rozhodnuti-agendy.md).

**Datum analýzy:** 2026-07-04

---

## 1. Co to je a proč na tom záleží

Systém má dnes hotový **zastávkový JŘ** (agenda `ZastavkoveJrAd` + `ZastavkoveJrMhd`) - tiskne se
**1 zastávka × 1 směr** na stránku (vývěska na zastávce). Legacy ekvivalent: `ALVA.TISKMHD`.

**Linkový/knižní J�Ř** (legacy `ALVA.TISKJR`) je **jiný formát**: **celá linka v jednom tisku** -
svislý seznam zastávek × vodorovné sloupce jednotlivých **spojů** (ne skupin dnů!) s časy v buňkách.
Vylepuje se na zastávky v linkové (meziměstské) dopravě a tiskne se v knižních JŘ publikacích.
Legacy generuje oba výstupy (vývěsní i knižní) ze stejného enginu.

**Proč je to jiný, těžší problém než zastávkový JŘ:**
- Osa sloupců jsou **konkrétní spoje**, ne skupiny dnů → matice [zastávka × spoj], blíž
  `TableElement` než `DepartureGridElement`.
- **Celá linka na jednu sestavu** vyžaduje 2D bin-packing stránkování (sloupce i řádky se lámou
  přes stránky) - dnešní `PageLayoutEngine` to neřeší, je to nová layoutová schopnost.
- Vyžaduje **fixed-width layout** (legacy trik: vkládání prázdných sloupců, aby všechny stránky
  vyšly na stejnou šířku) a **auto-fit fontu na šířku** (dnešní `ComputeFontScale` je jen výškový).
- Vyžaduje **proprietární symbolový font** (`Timetable_nove`/SK, 66+ dopravních symbolů) -
  jiný a rozsáhlejší číselník než `ConvertSpecialniZnak` u MHD.
- Řádky **Vyčká/Navazuje** (návaznosti) jsou nový koncept, který zastávkový JŘ nepotřeboval.

Odhad v existující analýze (`legacy-nahrada.md`): srovnatelná velikost
práce jako celý zastávkový JŘ (~1 měsíc čistého času), ale metodika (čistá reimplementace proti
legacy gold oracle) je už vyzkoušená a funguje.

---

## 2. Stav v projektu - kde to je zapsané, co (ne)existuje

**V TemplateDesigneru dnes nic neimplementováno.** Žádný element, žádná agenda, žádný Caché kód.
Existuje jen dokumentace a roadmapa:

| Zdroj | Co říká |
|---|---|
| `CLAUDE.md` §Aktuální focus | Zastávkový JŘ je hotový 1:1; linkový/knižní je next big track, zatím nezahájený |
| `.claude/docs/legacy-nahrada.md` §2 | Business popis `ALVA.TISKJR` - zóny, km, Tč, obousměrnost, PostScript fixed-width trik, návaznosti |
| `.claude/docs/legacy-nahrada.md` §3.3 | Nejpodrobnější - rozsah (33 400 LOC VB + 3 290 Caché), navržené kroky, gold oracle `mkPLinWin` |
| `.claude/docs/roadmap.md` „Linkové a knižní JŘ" | Bloky `B10` (symbolový font), `B13` (rozřezání široké tabulky), `B14`, `B60` + odložené body `C5`-`C7` - **nezahájeno** |
| `.claude/docs/architektonicka-rozhodnuti-agendy.md` | ⚠️ **Gate před návrhem elementu** - R1 (sdílené tabulkové jádro StopList+DynamicTable+LineTable), R2 (layout zodpovědnosti), R5 (embedded fonty) - **přečíst před návrhem elementu** |
| `.claude/docs/ad-data-mapa.md` §„Dva formáty linkového JŘ" | Rozlišuje „klasický sloupcový" (zastávky×spoje, doložený reálnou linkou) vs „MHD minutová mřížka" - **toto je popis dat pro ZastavkoveJrAd, ne pro LineTableElement**; nepřeskakovat, ale nezaměňovat účel |

**Roadmapa čeká přesně na tuto práci** - bloky linkových a knižních JŘ jsou navržené, ale nikdo je
nezačal. Tento dokument je fakticky rozpracování prvního z nich („Analýza referenčních výstupů + wire
formát").

---

## 3. Legacy zdroje - kde je skutečný kód (lokálně dostupný)

Legacy VB.NET + Caché kód **existuje lokálně** na této stanici (jiný git repo, ne EDISONMAIN2):

```
C:\DEV\EDISON\TiskJR\                              - VB.NET klient (~33 400 LOC)
  CACHE\ALVA\TISKJR\Data.cls                        - formátovací/překladová vrstva (1644 řádků)
  CACHE\ALVA\TISKJR\Main.cls                        - CRUD sestav/šablon (232 řádků) - precedens, ne datový zdroj
  CACHE\ALVA\TISKJR\Sestavy.cls                     - správa sestav (399 řádků) - precedens, ne datový zdroj
  CACHE\ALVA\TISKJR\Designer.cls, Images.cls
  _ruzne\                                            - TTF fonty (Timetable_nove aj.)

C:\DEV\EDISON\TiskJRRELACE\                         - slovenský fork (~49 700 LOC) - SAMOSTATNÝ use case, viz §7

C:\DEV\EDISON\JRMAIN\CACHE\JRapiLinPrtWin.mac        - ⭐ SKUTEČNÝ ORACLE, 4 224 řádků
                                                        obsahuje mkPLinWin, WinTiskMhd^JRapi, mkPRelWin
C:\DEV\EDISON\JRMAIN\CACHE\ALVA\JRMAIN\MLAPI.cls     - wrapper třída volající mkPLinWin/mkPRelWin
C:\DEV\EDISON\JRMAIN\DOCS\06-sestavy-tisk.md         - vysokoúrovňový přehled tiskových kanálů JRMAIN
C:\DEV\EDISON\JRMAIN\DOCS\07-cache-api.md            - API detaily
```

**Klíčový poznatek (potvrzeno čtením kódu):** `ALVA.TISKJR.Data.LoadData` je **jen tenká
překladová vrstva** - skutečná byznys logika (výběr spojů, platnosti, bin-packing, zóny, poznámky)
žije v `mkPLinWin` (soubor `JRapiLinPrtWin.mac` v JRMAIN, ne v TiskJR). **Reimplementace musí
vycházet primárně z `JRapiLinPrtWin.mac`**, `TISKJR/Data.cls` slouží jen k pochopení, jak se
legacy struktura mapuje na VB.NET klienta (užitečné pro pojmenování polí, ne pro algoritmy).

Datový zdroj (globály `^JRL`/`^JRZ`/`^JRSET`) je **stejný** jako u `ZastavkoveJrAd` - žádná nová
databázová vrstva navíc, jen jiná byznys logika nad stejnými daty. Mentální model globálů je
zdokumentovaný v `.claude/docs/ad-data-mapa.md` §1–8 (netřeba znovu objevovat).

---

## 4. `mkPLinWin` - signatura a I/O kontrakt

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
in("A",2,"MS",typ,podtyp) = šířka sloupce daného typu - VIZ NÍŽE, klient-driven layout
```

## 16. Plán vlny `L5` (rozřezání tabulky) - po blocích

**Stav, ze kterého se vychází** (ověřeno čtením kódu 25. 8.):

- **Svisle už se řeže**: `PageLayoutEngine` rozseká sekci přes stránky (`ClipTop`/`ClipBottom`)
  a `PreviewCanvas` posune počátek nahoru. Prvek o tom **neví** - kreslí se celý a přebytek se
  ořízne. ⛔ Důsledek: na pokračovací stránce **chybí řádek s čísly spojů**, protože se odroluje.
- **Vodorovně se neřeže vůbec**: `LineTableElement` místo toho spočítá `widthScale` a **zmenší
  sloupce i písmo**, aby se vešly. Odtud „u širokých dat není nic vidět".
- Řez podle šířky je **čistě geometrický** (viz měření níže), takže ho musí spočítat **klient** -
  jen on zná fonty a šířky. Serverová cesta se tím nemění.

**Bloky v pořadí, každý samostatně ověřitelný:**

| # | Co | Hotovo když |
|---|---|---|
| ~~`L5-1`~~ | ✅ 24. 8. Rozdělení sloupců spojů do bloků podle skutečných šířek a písma. | - |
| ~~`L5-2`~~ | ✅ 25. 8. Bloky se sázejí pod sebe, popisné sloupce se v každém zopakují. | - |
| ~~`L5-3`~~ | ✅ 25. 8. **Odpadl** - bloky pod sebou proteče **stávající svislé** stránkování (`ClipTop`/`ClipBottom` v sekci `SplitAcrossPages`), takže druhá (vodorovná) osa v enginu nevznikla. ⚠️ **Doloženo testem, ne úvahou** (`SirokaLinkaStrankovaniTests`): 200 spojů × 25 zastávek = 9 bloků → 3 stránky, chunky na sebe navazují bez díry i překryvu. | - |
| `L5-4` | **Dopravci v záhlaví jen za spoje, které jsou vidět** (#9417). ⚠️ **Zpřesněno 25. 8. čtením tiketu:** zákazník žádá **per stránka** („jen ty spoje které na listu jsou"), ne per blok, jak si to přepsala roadmapa. Potřebuje ze serveru **vazbu dopravce↔spoj** (dnes `PopisDopravce` projde všechny spoje obou směrů a vrátí jeden text). ⚠️ Druhá polovina tiketu, **přetečení hlavičky pod tabulku, je vyřešená jinak** - viz níže. | v záhlaví jsou jen dopravci vidět spojů |
| ~~`L5-5`~~ | ✅ 25. 8. Pás skupin dnů (i řádek platnosti a negativních značek) je nad **každým** blokem. O tom, které řádky tabulka má, rozhodují všechny spoje linky, ne spoje bloku - dřív blok padlý celý do jedné skupiny nadpis neměl. | - |
| ~~`L5-6`~~ | ✅ 25. 8. Řez se drží hranic bloků; uvnitř bloku (linka s tolika zastávkami, že blok přeroste stránku) padne mezi řádky zastávek a pokračovací stránka **zopakuje hlavu bloku** - pás skupin, čísla spojů, platnost, negativní značky. Místo na ni vyhradí stránkování, aby ji prvek nepřetiskl přes obsah. | - |
| ~~`L5-7`~~ | **Vědomě odloženo 25. 8. do `B100`** - nastaveno na 3 linkách z ~93 tisíc, což na zásah do serveru i klienta nestačí (rozhodnutí Jan). Neblokuje: vynucený zlom by k dnešním silným a slabým zlomům jen přibyl. | - |
| `L5-8` | **Dorovnání šířky prázdnými sloupci** jako parametr (#8372). ✅ **Předpoklad hotový 25. 8.**: sloupce spojů jsou ve všech blocích stejně široké a neúplný blok nechá místo vpravo (dřív se čtyři spoje roztáhly na sloupce 58,8 mm proti 6,4 mm v prvním bloku - přesně to „roztažení do absurdna"). Zbývá **volba**, jestli se to místo vyplní prázdnými sloupci. | volba funguje a nedorovnává donekonečna |
| `L5-9` | **Slučování přípojných řádků** (přesunuto z `L2` 24. 8.) - mění počet řádků, tedy kam řez padne. | podle rozhodnutí; dnes nechat po jednom |

⚠️ **Zlomy mají dvě síly.** Hranice bloku je **silná** (přirozený konec stránky), hranice řádku
zastávky **slabá** - náhrada pro blok, který se na stránku nevejde celý. Silná má přednost, i když je
výš; brát prostě „poslední, co se vejde" by řezalo doprostřed bloku pokaždé, když v něm zbyl řádek.

⚠️ **Řez stránky se musí držet hranic bloků** (zjištěno měřením 25. 8.): geometrický řez o blocích
nevěděl, takže na A4 na šířku vyšly dva bloky po 105,7 mm na plochu 210 mm jako „blok a půl + **1,4 mm
proužek**". Prvek proto nabízí místa, kudy se řezat smí (`INedelitelneDily`), a engine na ně řez
přichytí; díl vyšší než celá stránka se rozřízne i tak. Hlídá `SirokaLinkaStrankovaniTests`.

⚠️ **Dno šířky sloupce řídí, kam řez padne** (zjištěno 25. 8.): dokud bylo pevné (3 mm), nasypalo
dělení do bloku tolik spojů, kolik se jich při nečitelné šířce vešlo - bloky vycházely, ale text v nich
byl slitý. Dno se proto **měří z písma** (šířka času „00:00" i s odsazením), takže zvětšení fontu ubere
spojů na blok místo aby tabulku zhustilo. Hlídá `LineTableBlokyTests`.

⚠️ **Testovací případ z #9088**: prázdná legenda nesmí zamíchat stránkováním.
⚠️ **Zkušební vzor**: linka 940 Kroměříž-Přerov (IDZK), §15.

📐 **Jak starý modul řeže tabulku - změřeno 25. 8.** (podklad pro `L5`, sonda `BlokyDleSirky`):

Rozřezává **sloupce spojů podle šířky plochy**, ne podle skupin dnů, a v každém bloku zopakuje
sloupec zastávek. Na `CB_CSADAD` 0330510 (směr T má 41 spojů: 30 pracovní + 5 soboty + 6 neděle):

| Šířka plochy | Bloků | Dělení směru T |
|---|---|---|
| 120 | 4 | 21 + 20 |
| 200 | 4 | 39 + 2 |
| 400+ | 2 | 41 (vejde se) |

⛔ **Řez nepadá na hranice skupin** (ty by byly 30 a 35) - blok tedy běžně obsahuje **konec jedné
skupiny a začátek další**. Domněnka „každá stránka má svůj typ provozu" **neplatí** a `L5` se podle
ní nesmí navrhnout: pás s nadpisy skupin se musí umět **rozseknout a zopakovat nad dalším blokem**.
S tím souvisí zákaznický požadavek #9417 (dopravci v záhlaví jen za spoje, které jsou vidět).

**⚠️ Architektonická past, kterou nechceme replikovat:** legacy je **klient-driven, dvoufázový
layout** - VB.NET klient nejdřív *měří text ve skutečném tiskovém fontu* a posílá zpět serveru
„počet měřicích znaků" (`in("A",2,"MS",...)`), server podle toho určuje zlomy stránek. To je
ping-pong přes síť. **V TD tohle odpadá** - `SkiaHeightCalculator` měří lokálně na klientu (C#),
takže není potřeba tenhle roundtrip; jen je potřeba **replikovat výsledek** (jaké sloupce/řádky
kam patří), ne mechanismus měření.

### Výstup `out("dta",idx1)` - skalární metadata linky

Typ JŘ (obyčejný/objížďkový/návrh), číslo, název, platnost od/do, poznámky nad linku / pod
dopravce, QR kód, řádky textu dopravce, ID dopravce, existence loga. Analogie k `stDBField`
sekci u zastávkového JŘ (`GetSchema`/skalární pole v `ZastavkoveJrAd`).

---

## 5. Výstupní struktura `glbout` - objektový model

Na rozdíl od `ZastavkoveJrAd` (čistě JSON-per-page, viz `ad-data-mapa.md`), legacy tady
používá **objektově orientovanou strukturu**: `idobj` = číslo objektu (datový paket), každý
objekt je buď **datový blok** (typ 2 = blok odjezdů) nebo **poznámkový blok** (typ 3), navzájem
provázané (`arr,5,idobj2`). Tohle je přímý zdroj pro „flow skládání datových paketů" zmíněné
v roadmapě (`B13`) - N bloků vedle sebe i pod sebe podle místa.

```
@glbout@(idobj,"row",radek,"arr",1) = typ řádku:  1=platnost, 2=zastávka, 3=vyčká, 4=navazuje, 5=text
@glbout@(idobj,"row",radek,"arr",2) = podbarvení: 0=žádné, 1=25% šedá (liché TČ), 2=přípojný, 4=inverzní
@glbout@(idobj,"row",radek,"arr",3) = podtržení:  0/dole/nahoře/obojí

@glbout@(idobj,"column",sloupec,"arr",1) = typ sloupce: 1=Zóna, 2=Km, 3=Tč, 4=Zastávky, 6=MHD, 7=Spoj
@glbout@(idobj,"column",sloupec,"arr",3) = číslo spoje (jen typ 7)
@glbout@(idobj,"column",sloupec,"arr",4) = skupina odjezdů (1/2/3 dle in("A",2,"arr",3))

@glbout@(idobj,"dta",radek,sloupec,idx1,"arr",1) = typ buňky: 1=text, 2=symbol, 3=NZ, 4=čas, 5=zmenšený text
```

**Doporučení pro nový wire formát (R3 - čistě JSON, žádný `$lb`/objektový model):** tohle je
přesně ten typ struktury, kterou je potřeba **přenavrhnout od nuly jako JSON**, ne mapovat 1:1.
Návrh: pole `columns[]` (typ + metadata jako u `TableColumn`), pole `rows[]` (typ řádku + buňky),
podobně jako dnešní `TableElement`/`DynamicTableElement` JSON kontrakt - matice zastávka × spoj
mapuje se přirozeně na sloupcovou tabulku s speciálními typy řádků (platnost/vyčká/navazuje/text).

---

## 6. Byznys logika - detaily nutné pro 1:1 shodu

### 6.1 Zóny, km, tarifní číslo, MHD sloupec

- **Zóna** (sloupec typ 1): text z `z(tc,"gTrsLin","dta",11)`; šířka řízena max délkou textu (3
  podtypy).
- **Km** (sloupec typ 2): pro směr Tam přímo; pro směr Zpět se **odečítá od max km na trase**
  (`mymaxkm - km`). Hodnota `-1` → zobrazí se jako `<` („jede jinudy" - spoj se odchyluje od
  hlavní trasy).
- **Tarifní číslo** (sloupec typ 3): lze vypnout globálně i per-linka; **liché TČ** → podbarvený
  řádek (viz `row,"arr",2`=1).
- **MHD** (sloupec typ 6): mapuje interní kódy na text „MHD"/„IDS"/vlastní IDP-P/IDP-Z; sloupec
  se **automaticky odstraní**, pokud je pro celou linku prázdný - důležité pravidlo, replikovat.
- **Zastávky** (sloupec typ 4): název ze standardního pole nebo upravený MHD název; atribut trasy
  `"S"` (stanice) připojuje „, stan.N"; atributy `"p","o","J","P","T"` mohou **rozdvojit řádek**
  na samostatný příjezd+odjezd (frakční index `row - 0.01`) - edge case, který StopList dnes neřeší.

### 6.2 Skupinová záhlaví PD/So/Ne - „3 režimy dělení"

Řízeno `in("A",2,"arr",3)`:

| Hodnota | Rozdělení |
|---|---|
| `0` | bez rozlišení - jedna skupina |
| `1` | dva bloky: (X/1-5 pracovní) / (6/7/+ víkend+svátky) |
| `2` | tři bloky: (X/1-5) / (6 sobota) / (7,+ neděle+svátek) |

Platnost spoje: `mkSttKalLin` + `mkStaLinSp`; **bez explicitní platnosti fallback = tiskne se
všude** (fix HD 10183, 8.4.2025 - behaviorální detail hodný gold-oracle testu). Manuální override
`MHD2/umeleDatDoSekci`.

### 6.3 Auto-fit a 2D bin-packing - `Strih()`

Dvoufázový algoritmus:
- `typstrih=0` - úsporný, ignoruje skupiny dnů (nejmíň stránek).
- `typstrih=1` - skupiny jako samostatné bloky, ale **jen pokud to nezhorší celkový počet
  stránek** (heuristika, ne tvrdé pravidlo).

Řezy (`cutx`/`cuty`) = greedy bin-packing sloupců/řádků do šířky/výšky tiskové plochy, s podporou
ručního zlomu na konkrétním TČ (`pset("SET",3,"arr",1,8)`). **Toto je nová layoutová schopnost,
kterou `PageLayoutEngine` dnes nemá** - dnešní engine dělá vertikální flow sekcí, ne 2D chunking
sloupců tabulky napříč stránkami. Prázdné padding sloupce (`in("A",2,"arr",13)`) sjednocují počet
sloupců napříč stránkami - legacy PostScript trik zmíněný v `legacy-nahrada.md`.

### 6.4 Deset druhů poznámek - pořadí je KONFIGUROVATELNÉ (rozdíl oproti AD!)

| # | Druh | Zdrojové klíče |
|---|---|---|
| 1 | Týdenní platnost | `X,1..7,+` |
| 2 | NZ (negativní značka) | číselné klíče `>9` |
| 3 | Vyčká/navazuje/pokračuje | `u`,`m`,`^` |
| 4 | Poznámka pro spoj/TČ | `p` (+ prefix `MHD2~`) |
| 5 | Atributy | `BEV,IDS,NKA,OBC,PCZ,PJI,POS,RBU,SAO,CBE` |
| 6 | Místenkování | `&,AMS,R,Ü,þ` |
| 7 | Stálé značky linky | `<,MHD,\|,auto,vlak,x` |
| 8 | Poznámky linky - tarifní | `C1` |
| 9 | Poznámky linky - všeobecné | `C3` |
| 10 | Poznámky linky - radiobus | `C4` |

**Důležitý rozdíl oproti `ZastavkoveJrAd`:** u AD je pořadí legendy pevná konstanta (ověřeno a
zdokumentováno v CLAUDE.md - „Pořadí legendy" 1:1). U linkového JŘ je pořadí **uživatelsky
konfigurovatelné** přes parametr `PoznamkyPoradiDruh` (`TISKJR/Data.cls` ř. 113, 438). Nová agenda
buď replikuje jako server-side parametr (konzistentní s existujícím vzorem „Rozdělení sloupců"
u `ZastavkoveJrAd`), nebo se vědomě zdokumentuje jako gap k doplnění později.

### 6.5 Symbolový font `Timetable_nove` (+ SK varianta)

66+ dopravních symbolů, mapovací tabulka `Main.cls` (legacy VB) řádky cca 35–107; záporné kódy =
composite symboly kreslené kódem (ne jeden glyph). TTF soubory v `C:\DEV\EDISON\TiskJR\_ruzne\`.
**Toto je tvrdá prerekvizita** - bez převodu fontu do SkiaSharp (embed TTF nebo vlastní SVG glyph
sada + převodní tabulka analogická `ConvertSpecialniZnak`) nejde žádný obsah s dopravními symboly
vykreslit korektně. Roadmapa to řadí jako krok 42b, **před** `LineTableElement` (krok 43).

### 6.6 Řazení spojů a časy

`mkPoradiSp^JRapi` + filtrace dle platnosti. Čas: `$$$NT2` (sekundy → HH:MM). Markery: `"<"` =
jede jinudy, `"|"` = projíždí bez zastavení. Řádky Vyčká/Navazuje se vkládají jako **frakční
indexy** mezi celočíselné indexy zastávek (`tc2row(tc) ± 0.1`) - technika řešení „vlož řádek
mezi existující" bez renumbering.

---

## 7. TiskJRRELACE - vědomě mimo scope tohoto kroku

Slovenský fork (~49 700 LOC), třetí odlišný výstup (řádky = odjezdy, sloupce = linka/čas/směr/
nástupiště) - blíž zastávkovému JŘ, ale ne identický. **Open question v `legacy-nahrada.md`:
je to živý use case?** Roadmapa („Relace a vozové JŘ") říká „nejdřív ověřit živý use case".
**Doporučení: nezahrnovat do prvního implementačního pokusu.** Zaměřit se čistě na `mkPLinWin`
(klasický linkový/knižní), Relace je samostatné rozhodnutí až po ověření u zákazníka.

---

## 8. Klíčové architektonické rozdíly oproti `ZastavkoveJrAd` (shrnutí)

| Aspekt | ZastavkoveJrAd (hotovo) | Linkový/knižní JŘ (chybí) |
|---|---|---|
| Rozsah 1 tisku | 1 zastávka × 1 směr | Celá linka (všechny zastávky × všechny spoje) |
| Osa sloupců | Skupiny dnů (DepartureGrid) | Konkrétní spoje (matice, blíž TableElement) |
| Stránkování | Vertikální flow (PageLayoutEngine) | 2D bin-packing (sloupce i řádky se lámou) - **nová schopnost** |
| Layout šířky | Pevný layout, žádný auto-fit na šířku | Auto-fit fontu na šířku + fixed-width padding trik |
| Font | Arial/systémový + `ConvertSpecialniZnak` (dny + pár ASCII) | Proprietární `Timetable_nove` - 66+ symbolů, **prerekvizita** |
| Nové řádkové typy | - | Vyčká / Navazuje (frakční indexy mezi zastávkami) |
| Pořadí poznámek | Pevná konstanta (1:1 ověřeno) | Konfigurovatelný parametr (`PoznamkyPoradiDruh`) |
| Datový zdroj | `^JRL`/`^JRZ`/`^JRSET` (JRMAIN) | **Stejný** zdroj, jiná byznys logika (`mkPLinWin`, ne přes `ALVA.TISKMHD.Data`) |
| Wire formát oracle | JSON-per-page (jednoduché) | Objektový model s `idobj`/`row`/`column`/`dta` - **přenavrhnout jako JSON od nuly** |

---

## 9. Doporučený postup implementace (pro Fable)

Návrh sekvence, respektující gate v `architektonicka-rozhodnuti-agendy.md` (R1/R2/R5) a
existující roadmapové kroky 42–44:

1. **Nejdřív přečíst `architektonicka-rozhodnuti-agendy.md`** - než navrhovat `LineTableElement`,
   ověřit rozhodnutí o sdíleném tabulkovém jádru (má `LineTableElement` sdílet infrastrukturu
   s `TableElement`/`DynamicTableElement`, nebo je to svébytný element jako `ScheduleElementBase`
   větev?).
2. **Krok 42b - symbolový font.** Bez převodu `Timetable_nove` do SkiaSharp nejde nic vizuálně
   ověřit. Navrhnout: embed TTF + mapovací tabulka (`GSId()` v `JRapiLinPrtWin.mac`, cca ř.
   1550–1636, je centrální znakový číselník - začít tam).
3. **Wire formát návrh (R3, čistě JSON)** - nová Caché agenda `KnizniJrAd` (jméno z roadmapy),
   `GetData`/`GetMockData` vrací JSON strukturu inspirovanou `columns[]`/`rows[]` (viz §5 návrh),
   ne 1:1 kopii `idobj`/`arr` legacy modelu.
4. **`LineTableElement`** - nový element (viz `skill `novy-element`` pro
   kompletní checklist kroků: model, JsonDerivedType, factory switche, toolbar, dynamická výška,
   GetSummary). Klíčová odlišnost od `TableElement`: podpora řádkových typů (platnost/vyčká/
   navazuje) a per-sloupec = per-spoj metadata (číslo spoje, TČ zvýraznění).
5. **Layout rozšíření** - 2D bin-packing (flow paketů + auto-fit šířky) je nad rámec jednoho
   elementu, zasahuje do `PageLayoutEngine`. Pro první lokální pokus **doporučeno zjednodušit**:
   nejdřív ověřit datovou/vizuální správnost na jedné lince/stránce (bez multi-page bin-packing),
   plnou stránkovací logiku řešit až v navazujícím kroku.
6. **Gold oracle test** - `mkPLinWin` lze volat přímo přes `cache_query` (stejný vzor jako
   u `ZastavkoveJrAd`, viz `ad-data-mapa.md` §9 pro analogický příklad s `LoadData`).
   Ověřit na reálné lince s `^JRL` daty (kandidát z `ad-data-mapa.md`: `CB_GWBUS` /
   `0320113`, pokud má vhodný formát - jinak najít jinou realnou linku).

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

## 10. Otevřené otázky (přenést do `legacy-nahrada.md`, pokud se zodpoví)

- `ALVA.TISKJR` - umí i MHD, nebo je čistě linkový? (stále nezodpovězeno, viz legacy doc)
- Je vhodné `LineTableElement` stavět jako samostatnou hierarchii, nebo rozšířit `TableElement`
  o řádkové typy? → rozhodnout dle gate R1 v `architektonicka-rozhodnuti-agendy.md`.
- Prioritní zákazník/linka pro gold-oracle validaci - potřeba reálná data s dostatečně bohatým
  obsahem (zóny, Tč, MHD sloupec, aspoň jeden Vyčká/Navazuje řádek).

---

## 11. Stav implementace (2026-07-04, Fable - lokální vertikální řez, NECOMMITNUTO)

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
(39/-/70/-/70/32), časy vč. markerů, zóny, km obou sloupců vč. přepočtu Zpět (max−km), návaznosti
(spoj 5→16, 11→12 vč. času a textu), NZ texty datumově znak-po-znaku („nejede 24.12.,31.12.",
„od 21.12. do 5.1.,31.1., od 3.2. do 7.2.…" - knižní formát bez roku, jiný než zastávkový),
tarifní poznámka C1. **Klíčový nález:** W-věta rozlišuje TŘI stavy času - číslo / `|` (projíždí) /
`<` (jede jinudy) - `ZastavkoveJrAd.CasHodnota` zná jen `<`; při extrakci sdílených helperů pozor.

**Vědomé mezery prvního řezu** (soupis i v docstringu třídy): Strih 2D bin-packing + padding
sloupce (layout řeší TD), turnusy, obousměrný tisk, MHD sloupec (J-věta dta6 mapování neověřeno),
rozdvojení řádku příjezd/odjezd, poznámky druhů 4/5/6, konfigurovatelné pořadí poznámek
(`PoznamkyPoradiDruh`), texty druhu 1 hardcoded (legacy `^JRSET("NCOME")`), řazení spojů jen dle
odjezdu (ne plné `mkPoradiSp`), skupinové nadpisy hardcoded (legacy WinMHD DSC), symbolový font.

**Další krok k ostré verzi:** krok 42b (font Timetable), R1 extrakce sdíleného řádkového jádra
(DynamicTable+StopList+LineTable), plné 1:1 porovnání proti mkPLinWin na více linkách (KV_DP,
CB_CSADAD), rozhodnutí o commitnutí tohoto řezu.

---

## 12. Druhá vlna - „1:1" push (2026-07-04, autonomní pokračování)

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
  - computus; den = číslice i „+" současně) - aproximace mkStaLinSp kalendáře
- poznámky druhů 1-10: NCOME/STALCOME texty (rec=CZ/*SK), TiskDtm režimy datumů (0/1/2/3),
  pořadí omezení 13,12,11,SU,LI,22; sudé/liché týdny (hlava:8); návaznosti agregované per (spoj×tc)
  s AVL slovníkem (A=autobus, V=vlak, L=loď) a gate typVýstupu/typZobrazení (omz31 :4/:7, omz32 :4/:9);
  pokračuje (omz34); atributy generická věta; místenkování; stálé značky z J-atributů + ^JRZ $8
  + W piece5 (trasové značky V/N/x/G/a/b/c); poznámky linky s `#`-odstavci (prázdný → mezera)
- zóna jen u IDS linek (PATCH linIDS), prázdná → „-"; MHD sloupec z J-věty `#`-pole 4
  (gTrsLinDta2ToText mapování) s auto-remove

**Element:** MHD sloupec (auto-hide), příjezdové podřádky, přípojné řádky jen s RADEK=1.

**Výchozí šablona:** `TemplateDesigner/templates/knizni-jr-ad-vychozi.json` (hlavička s placeholdery,
LineTableElement, legenda přes DynamicTable POZNAMKY) - generovaná harness režimem `template`,
vizuálně ověřená na 0320680 (2 strany, zdvojené řádky, skupiny dnů, legenda). Zapojení do
distribuce šablon = blok B2/R6 (zatím ruční Import JSON).

**Zbývající známé odchylky (dokumentované, nekritické):**
- pořadí časově identických „dvojčat" spojů (0945001) - obsah 1:1, jen pořadí sloupců
- názvy zastávek v návaznostech u městských linek (0945001: legacy zkrácený místní tvar bez obce)
- počet přípojných řádků 0330510 Z (legacy 5 vs my 8 - legacy zřejmě slučuje conn do jednoho řádku)
- druh 7: symbol 108 „jen pro vystupování" na 0320113/118 (zdroj per-spoj V značky nedohledán)
- gating atributů nastavením PAtrSp/TZPA (legacy umí BEV ap. potlačit konfigurací)

---

---

## 13. Revize stavu 2026-08-19 - co reálně máme a inventář nastavení starého modulu

> Účel revize: před dalším kolem implementace zjistit, **kolik z chování starého modulu už
> pokrýváme** a hlavně **která nastavení EDISONu do výstupu mluví**. U zastávkového JŘ se ta
> nastavení objevovala jedno po druhém jako zákaznické tikety; tady je chceme mít vyjmenovaná
> dopředu, i ta, která vědomě neděláme.

### 13.1 Co je dnes v repu

| Vrstva | Stav |
|---|---|
| Caché agenda `LinkovyJrAd` (1 343 ř.) | data celé linky, 1 stránka per (linka × směr), řazení spojů, zdvojené řádky příjezd/odjezd, řádek platnosti, NZ, návaznosti, poznámky druhů 1/2/3/7/8/9/10 |
| Caché agenda `LinkovyJrMhd` (692 ř.) | týž wire formát nad MHD daty; bez Km/Tč/MHD sloupců a bez návazností (zdroj je nemá) |
| `LineTableElement` (596 ř.) | matice zastávka × spoj, plán řádků, kaskáda stylů, škálování na šířku i výšku, podmíněná viditelnost a zvýraznění řádku |
| Výchozí šablona | `dotnet/SystemTemplates/linkovy-jr-ad.json` (AD). **MHD nemá.** |
| Testy | Síť z `L1` v repu: porovnání se starým modulem (`LinkovyJrAdLegacyTest` nad oraclem `LinkovyJrLegacyOracle`), zlatý odtisk, typová sada linek, smoke. Běží v `run-cache-tests.ps1`. **Nekryje nastavení** - viz §13.3. |

Sloupcový repertoár prvku: Zóna, Km, Tč, Zastávka, MHD, Spoj. Legacy jich má devět
(`gMSArrPLinWin`, `JRapiLinPrtWin.mac`): navíc **Šipky za zastávkou** (typ 5), **Šipky před
zastávkou** (typ 8) a **Spoj před zastávkou** (typ 9) - poslední dva jsou zrcadlo pro obousměrný
tisk (sloupce nalevo od názvů).

⚠️ **Nalevo jde ten směr, jehož pořadí sleduje seznam zastávek** - tedy ten, jehož časy jdou odshora
dolů; opačný je vpravo. **Doloženo tiskem** obousměrné linky ze starého modulu (Jan 28. 8.): vlevo
spoj 71 s časy 07:33 → 07:38 a značkou „od" u první zastávky, vpravo spoj 72 s časy 07:53 → 07:39
a značkou „př". Ze zdroje to **odvodit nešlo** a jedno takové odvození bylo chybné: index sloupce
v `MT2A` (`:3429`, obousměrný blok začíná spoje na indexu 5) **není vizuální pořadí**, o straně
rozhoduje **typ** sloupce (7 vs 9).

### 13.2 Nastavení, která starý modul čte (kompletní inventář)

> Přepsáno 2026-08-20 čtením zdroje starého modulu, protože předchozí verze inventáře měla tři
> mezery a jednu vrstvu neznala vůbec. Cíl: mít vstupní kontrakt starého modulu vyjmenovaný celý,
> ať se u `L2` nezjišťuje po jednom z tiketů.

Cesta nastavení do tisku má dva úseky.
Klient (VB) posbírá volby uživatele a pošle je do `ALVA.TISKJR.Data.LoadData`, ta z nich složí
pole `in("A",2,...)` a zavolá engine (`TISKJR/CACHE/ALVA/TISKJR/Data.cls:145-160`).
Zbytek si engine načte sám z globálů, dvěma vstupními body do pole `SET`:
`gPGSetWin` (globální nastavení) a `gPLSetWin` (nastavení u linky), oba
v `JRMAIN/CACHE/JRapiLinPrtWin.mac:1809-1897`.

**Enginy jsou dva a kontrakt mají různý.**
AD: `mkPLinWin^JRapiLinPrtWin` (4 224 řádků).
MHD: `ALVA.MHD.LinkyPrintFormatJR.MakeData` (1 400 řádků).
MHD engine má v hlavičce opsaný celý seznam parametrů od AD, ale reálně čte jen tři z nich.
To je důležité pro plánování: „linkový MHD nemá žádné parametry" není jen naše mezera, starý modul
je tam taky ignoroval.

#### A. Volby uživatele u tisku (`in("A",2,...)`)

| Legacy klíč | Co dělá | AD engine | MHD engine | U nás |
|---|---|---|---|---|
| `arr,3` RozdelovatSpojePodlePlatnosti | dělení sloupců na skupiny dnů (0 / PD+víkend / PD+So+Ne) | ✅ `:423,441,504` | ❌ nečte | ✅ parametr „Dělení spojů dle platnosti" |
| `arr,4` PoznamkyZpusobTisku | kam jde legenda (per blok / per směr / souhrnně / + odstránkovat) | ✅ `:713-761` | ✅ `:301` | ❌ legenda je vždy jednou na konci |
| `arr,5` ZvyraznenaZastavka | zvýraznit řádek zvolené zastávky | ✅ `:392,1212,1240` | ❌ nečte | ✅ parametr „Zvýraznit zastávku“ (data řádek jen označí, vzhled řeší šablona) |
| `arr,6` TiskTurnusuKeSpojum | tisk turnusů ke spojům | ✅ `:354` → `SET,2,arr,1,3` | ❌ nečte | ❌ (vědomě, `L6`) |
| `arr,7` TiskObousmernychJR | oba směry na jednu sestavu (zrcadlené sloupce). ⚠️ **Není to „chci zrcadlo“, ale „zrcadlo, pokud se vejde“** - engine ho vypne, když v jednom směru nejsou spoje (`:601`), a znovu po střihu: když `Strih` zrcadlený blok musí řezat, tiše spadne na směr zvlášť (`:627`) | ✅ `:598` | ❌ nečte | ❌ (vědomě, `L6`) |
| `arr,9` NavaznostiVPripojnychRadcich | tisknout číslo linky/vlaku v přípojném řádku | ✅ `:2771` | ❌ nečte | ❌ tiskneme vždy |
| `arr,10` TC | zvýraznit konkrétní tarifní číslo (inverzní řádek) | ✅ `:1214,1242` | ❌ nečte | ✅ parametr „Zvýraznit tarifní číslo“ |
| `arr,11` FilterDTA | výběr **typů provozu a směrů** k tisku (`TypProvozuID#id~0/1`, `SmerID#id~0/1`) | ❌ nečte | ✅ `:305-321` | ❌ tiskneme vše |
| `arr,12` NekreslitRameckyKolemCiselSpoju | bez rámečků kolem čísel spojů; v MHD navíc **vypne i čísla spojů** | ❌ nečte | ✅ `:324` | ❌ (u nás řeší styl prvku, vedlejší efekt MHD chybí) |
| `arr,13` VkladatPrazdneSloupce | dorovnání šířky prázdnými sloupci | ✅ `:1038` | ❌ nečte | ✅ volba prvku „Dorovnat prázdnými sloupci“. ⚠️ **Parita neověřená** - oracle layout vědomě neměří (dává legacy obrovskou plochu, aby vyrobil jeden blok), takže dorovnáním síť nikdy neprošla; shoda se starým modulem je zatím jen ze čtení `:1038` |
| `dta,1` / `dta,2` / `MS` | šířka tiskové plochy, počet řádků, měřicí řetězce | ✅ | ✅ | ⛔ nepřenášíme vědomě (layout měříme sami) |

**Mrtvé položky kontraktu** (aby se nehledaly znovu):
`arr,1` (zóny) a `arr,2` (km sloupce) jsou v hlavičce označené „zrušeno, bere se z linky" a engine je
nečte, nahradilo je nastavení u linky `linIDS`.
`arr,8` ZalamovatOznaceniTurnusuZaTeckou klient posílá, ale **žádný engine ho nečte** a legacy si
u něj sám píše „todo" (`:178`).
`PoznamkyPoradiDruh` klient načte, ale do `in` ho nikdy nevloží, takže se do tisku nedostane.

**Volby, které zůstávají u klienta** a do enginu se nedostanou vůbec:
`CelkovaSirkaSekce`, `SirkaSloupceSeznamZastavek`, `SirkaSloupceSipky`, `PocetMoznychRadku`
a `MericiRetezec` se přepočítají na `dta,1`/`dta,2`/`MS`; `Preteceni` klient přečte a dál nepošle.
To je ta hranice, kterou u nás drží prvek a šablona.

⚠️ **Kontrakt umí víc linek v jedné sestavě už dnes.**
`ValueId` nese `SeznamSkupinALinek` ve tvaru `4|820701#4|820741` a klient nad ním cykluje
(`Data.cls:125-133`), takže engine se volá per linka a výstupy se skládají za sebe.
Pro `L7` (sdružený JŘ) to znamená, že se nevymýšlí nový kontrakt, jen se opakuje volání.

#### B. Globální nastavení programu (`^JRSET("Definice",...)`)

Výčet je **uzavřený a ověřený**: knižní JŘ si z celého číselníku bere právě těchto deset hodnot
(`JRapiLinPrtWin.mac:1817-1826`, mapování klíčů `JRapiSet.mac:288-342`).

| Klíč | Co ovlivňuje | U nás |
|---|---|---|
| `TiskDtm` | tvar datumů v omezeních (0-3) | ✅ včetně per-linka override |
| `PAtrSp` | které atributy nad spojem se vůbec tisknou (bezbariérově přístupné, kola, samoobslužný, nízkokapacitní) | ✅ hotovo 21. 8., u bezbariérového přebíjí linka (`TZPA,BBP`) |
| `MistOR` / `MistNOD` | tvar tisku místenkování (odřádkování, dvojtečky) | ❌ |
| `MHDPZ("P")` / `MHDPZ("Z")` | vlastní text místo „IDP-P" / „IDP-Z" ve sloupci MHD + jeho řádek legendy | ❌ máme napevno IDP-P/IDP-Z |
| `ZonaIdsDelZnk` | odstranit písmena z označení zóny | ❌ (zastávkový JŘ to umí) |
| `Print_Dopr_First` / `SDOP` / `PS_PrintSouhrnDoprLinYes` | pořadí a tisk dopravců a provozů, souhrn linek k dopravci | ❌ `PopisDopravce` má vlastní logiku, proti nastavení neověřenou |

Z jazykové verze (`gMode`) engine odvozuje dvě věci, ne jednu:
číselník textů CZ vs. SK a **počet druhů místenkování** (dva `R`, `R1` versus čtyři `R`..`R3`,
`SET,2,arr,1,2`).

**Texty značek nejsou v kódu, jsou v číselníku** a jsou per jazyk:
`^JRSET("NCOME",rec,id)` a `^JRSET("STALCOME",rec,id)` (`JRapiLinPrtWin.mac:2350-2361`).
Tam se má brát i těch třináct textů atributů spoje z nálezu `L1`, ne opisovat je do našeho kódu.

#### C. Nastavení u konkrétní linky (`^JRL(sku,lin,...)`)

Tohle je nejnebezpečnější vrstva: mění výstup jen u některých linek, takže se na testovací lince
nemusí nikdy projevit.
Kompletní seznam toho, co tisková cesta reálně čte (`gPLSetWin` plus přímá čtení v enginu):

| Klíč | Co dělá | U nás |
|---|---|---|
| `PATCH,KmsSmer,T/Z` a `PATCH,KmsSmer,sm,sl` | počet km sloupců per směr a obsah jednotlivého sloupce | ✅ počet; obsah sloupce neověřen |
| `PATCH,linIDS` | **čtyřstavový přepínač** zóny × km (0 = jen km, 1 = jen zóna, 2 = obojí, 3 = nic) | ⚠️ **částečně a chybně** - čteme jen „je neprázdné → tiskni zónu"; u hodnot `0` a `3` zónu tiskneme, legacy ne, a km sloupce neřídíme vůbec (`LinkovyJrAd.cls:348,453`) |
| `PATCH,nazMHD` | u MHD linky použít ručně upravený název zastávky | ❌ týž problém jako tvar názvu v `L3` |
| `PATCH,netisknoutSloupecTC` | schovat sloupec tarifních čísel | ❌ |
| `PATCH,tisknoutJenTcKdeSpojeZastavuji` | **vyhodit řádky**, kde žádný spoj nestaví (legacy to používá na 5 místech) | ❌ |
| `PATCH,podtrzeniPaJ` | podtržení u zdvojených řádků příjezd/odjezd (0 obě zastávky nad i pod / 1 první zastávka pod) | ❌ |
| `PATCH,barevnyTiskSpoju` | vypnout barvy spojů (barva je na spoji) | ❌ barvy spojů neposíláme vůbec, agenda nemá jediné `FORE_COLOR` (zastávková jich má 9) |
| `PATCH,cisIDS` | jak se skládá tištěné číslo linky (3místné, vlastní číslo, tablo) | ❌ |
| `PATCH,ZLOM` | ruční zlom stránky na zadaném tarifním čísle | ❌ **vědomě odloženo** do `B100` (rozhodnutí Jan 25. 8.) - nastaveno na 3 linkách z ~93 tisíc. Ty se u nás tisknou jinak než ve starém modulu; **není to nedodělek, který by někdo hledal**, je to zapsané rozhodnutí |
| `PATCH,TZPA("BBP")` | kam s bezbariérovým vozidlem (nad spoj / do poznámek) | ❌ |
| `PATCH,ZonaIdsDelZnk` | **přebíjí** globální hodnotu téhož jména (`:1847`) | ❌ |
| `MHD2,netisknoutTC` / `netisknoutCislaSpoju` / `netisknoutSipky` / `netisknoutKladivkaAPodobne` | schová sloupec Tč, čísla spojů, šipky, značky | ❌ |
| `MHD2,umeleDatDoSekci` / `rozdelitUmeleSekceNaVlastniStranky` | ruční přiřazení spojů do skupin a jejich stránkování | ❌ |

**Dvě mrtvé položky, které vypadají živě.**
`SET,3,arr,1,3` a `1,4` mají v hlavičce dokumentovaný význam „Vyčká / Navazuje jako řádek v tabulce
odjezdů místo v poznámkách", ale `gPLSetWin` je natvrdo nastaví na `0` s poznámkou „není zatím nikde
uloženo" a engine je nikdy nečte (`:1856-1859`).
Legacy tedy tuhle volbu **neumí**, ačkoli ji má popsanou.
My přípojné řádky tiskneme, takže se v tomhle od starého modulu lišíme z vlastního rozhodnutí,
ne kvůli nastavení.

⚠️ **My čteme u linky i to, co v tomhle inventáři nebylo.**
Naše AD agenda sahá navíc na `PATCH,Head`, `PATCH,OBJ`, `PATCH,PoznPodDopr` a `PATCH,Plati`
(`LinkovyJrAd.cls:208,323,324,336`), tedy záhlaví, objížďkový příznak, poznámka pod dopravce
a platnost.
Jsou to klíče sdílené s vývěskovou agendou; do matice patří stejně jako ostatní, protože i ony
mění výstup jen u některých linek.

#### D. Nastavení **pod** úrovní linky (chybělo úplně)

Vrstvy nejsou tři, ale čtyři.
Starý modul čte nastavení i na jednotlivém řádku a jednotlivém spoji, mimo pole `SET`:

| Klíč | Granularita | Co dělá |
|---|---|---|
| `^JRL(sku,lin,"J",tc,"MHD2","KnizniNetisknout")` | tarifní číslo | řádek se v knižním JŘ netiskne (`:3262`) |
| `^JRL(sku,lin,"J",tc,"MHD2","KnizniNetisknoutTam"/"Zpet")` | tarifní číslo × směr | při zapnutí obou se tarifní číslo přeskočí (`:1965`) |
| `^JRL(sku,lin,sm,sp,"MHD2","umeleDatDoSekce")` | spoj | do které umělé sekce spoj patří (`:582`) |

Tohle je pro testování nejtvrdší část matice.
Nastavení není u linky, ale u konkrétního řádku nebo spoje, takže ho neodhalí ani porovnání celé
linky, pokud si ho na ní nikdo nenastavil.
Zároveň je to nastavení, které **ubírá řádky** - přesně ta třída odchylky, kterou zákazník pozná
na první pohled.

#### E. MHD zdroj má vlastní vrstvu

MHD engine kromě `arr,4`, `arr,11` a `arr,12` čte nastavení ze svých vlastních globálů:

| Klíč | Úroveň | U nás |
|---|---|---|
| `AutomatickyPoznamkovatOdlisnostiVPlatnostechSpoju` | linka | ✅ čteme (`LinkovyJrMhd.cls:574`) |
| `QRCode`, `CisloVerejne`, `IDSID` | linka | ✅ čteme |
| `TypLinkyID` | linka | ❌ ověřeno grepem, nula výskytů |
| `RozsahNegativnichZnacekOd` / `Do` (`^ALVA.MHD.SETTINGS(idfa,"Obecne","DTA",1001,...)`) | firma | ❌ ověřeno grepem, nula výskytů |

### 13.3 Co z toho plyne pro testy

Zlaté odtisky hlídají **náš výstup proti včerejšku**, ne proti starému modulu - to je
pojmenované v `R6`, takže pro linkový JŘ potřebujeme obojí.
Odtisk i porovnání se starým modulem **jsou od `L1` hotové** (`LinkovyJrAdLegacyTest`,
`LinkovyJrGoldTest`, oracle volá `mkPLinWin` přímo, ne překladovou vrstvu `LoadData`).
Co ta síť **nekryje**, je právě matice nastavení, a to ze dvou důvodů:

- **Odtisk ani porovnání na výchozím nastavení nechytí nic z vrstev C a D.** Test musí každé
  nastavení **zapnout a vypnout** a ověřit, že se výstup změnil právě tak, jak ho změní legacy.
  Jinak platí totéž, co u zastávkového JŘ: „funguje to", dokud nepřijde zákazník, který si to
  nastavení zapnul.
- **Přepnout jde dnes jen vrstva A.** Oracle podstrčí starému modulu pole `in("A",2,...)`, takže
  volby uživatele u tisku umíme nastavit z testu. Vrstvy B, C a D jsou ale **data na serveru** -
  globální číselník, nastavení u linky, příznaky u tarifního čísla a u spoje. Bez rozhodnutí, jak
  je v testu přepnout, se z „matice nastavení" stane „změřili jsme, co bylo v datech" (varianty
  a jejich cena v §13.4 bod 1).


---

### 13.4 Rozhodnutí před `L2` (rozhodnuto Janem 2026-08-21)

Sedm věcí, u kterých se z inventáře nedalo odvodit, co je správně. **Šest je rozhodnuto**
(Jan, 21. 8.), **bod 4 se vědomě odkládá do MHD kola** - `L2` je tím pro AD vzatelná celá.
Varianty a ceny zůstávají zapsané, aby bylo vidět, co se zvažovalo a proč vyhrálo to, co vyhrálo.

⚠️ **U tří bodů se rozhodnutí od návrhu agenta liší** (3, 5, 7) a všechny tři míří stejným směrem:
**to, jak je výstup složený, patří do návrháře; z nastavení u linky se bere jen to, co mění data.**
Umístění legendy ani pořadí druhů poznámek tedy nejsou parametry sestavy, ale vlastnosti šablony.

#### 1. Jak v testu přepnout nastavení, které je uložené u linky

Volby, které si uživatel zaškrtne před tiskem, umíme starému modulu podstrčit z testu (vrstva A).
Nastavení u linky a globální číselník (vrstvy B, C, D) jsou **uložená data** - test je nemá jak
na chvíli změnit, aniž by sáhl do databáze.
Bez rozhodnutí ověříme jen ty přepínače, které někdo v testovacích datech náhodou zapnuté má.

| Varianta | Cena |
|---|---|
| Test si nastavení u linky přepne a po sobě vrátí | plné pokrytí, ale zapisuje do zákaznických dev dat a dva běhy naráz si šlápnou |
| **Testovací kopie linky** pod vlastním číslem, po testu smazaná | nic cizího se nemění a odpovídá to pravidlu „test si vytvoří svoje pod unikátním jménem"; otevřené je, jak hluboko kopie musí jít (trasa, zastávky, spoje) |
| Porovnávat jen linky, které to nastavené **už mají** | nejrychlejší a bezpečné, ale pokrytí je dané tím, co je v datech |
| Podstrčit nastavení oběma stranám mimo databázi | u nás snadné, u starého modulu zásah do jeho kódu (čte si je sám a hluboko) |

**Návrh:** třetí varianta jako první krok (změří, kolik přepínačů v datech vůbec je), druhá jako
cílový stav. První nedělat.

**Rozhodnuto:** nastavení se bere **tak, jak je u linky nastavené**. Pokrytí obou stavů zajistí
sama různost zákazníků - někdo to nastavené má, někdo ne, a porovnává se obojí. Testovací linku
si stavět nezačínáme.

⚠️ **Povinný doplněk, aby to nebylo tvrzení bez důkazu:** změřit, které přepínače se v datech
reálně vyskytují, a u těch, které nikde zapnuté nejsou, napsat **neověřeno** - ne mlčet a nechat to
vypadat jako pokryté. To je totéž pravidlo jako R6.

#### 2. Nastavení pod úrovní linky (vrstva D)

U konkrétní zastávky v lince jde zaškrtnout „tuhle v knižním JŘ netiskni", zvlášť pro každý směr.
U jednotlivého spoje jde ručně určit, do které skupiny sloupců patří.
Když to neuděláme, zákazník uvidí schovaný řádek zpátky - tabulka bude delší než dřív a bude v ní
zastávka, kterou tam nechtěl.

**Návrh:** skrývání řádků do `L2`, ruční přiřazení spojů do skupin až do vlny se stránkováním
(`L5`), protože to ovlivňuje rozřezání na stránky.

**Rozhodnuto:** skrývání řádků pod úrovní linky **do `L2`**. Ruční přiřazení spojů do skupin
zůstává v `L5`, protože ovlivňuje rozřezání na stránky.

#### 3. Tři položky, které neumí ani starý modul

Vedli jsme je jako svůj dluh; čtení zdroje ukázalo, že v legacy nejsou funkční.

- **Pořadí druhů poznámek** - starý klient volbu nabídl a do tisku ji nikdy neposlal. Zákazník si
  o to říká (#9088), takže proti škrtnutí stojí tiket.
- **Zalamování označení turnusu** - nedodělek, který si takto sám starý modul označuje. Turnusy
  jako celek jsou až v `L6`.
- **„Vyčká / Navazuje" jako řádek tabulky místo poznámky** - v kontraktu popsané včetně obou stavů,
  natvrdo vypnuté, nikdy nečtené.

**Návrh:** pořadí poznámek ano (je za tím tiket), ostatní dvě vyřadit ze seznamu dluhů.

**Rozhodnuto** (po upřesnění, co která položka je - jsou to tři různé případy, ne jeden):

- **Pořadí druhů poznámek** → **nastavení v návrháři, ne u linky** (rozhodnutí Jan 21. 8.).
  ⚠️ **A tak to bylo i ve starém modulu** - hodnota se ukládá `^AlvaTiskJR(KEY, SekceId,
  "stDL_PoznamkyPoradiDruh")`, tedy **u sekce šablony** (`TISKJR/Designer.cls:156,785`), ne u linky.
  Nepřesouváme to tedy nikam: patřilo to do návrháře vždycky a chyba byla, že to **tisk nikdy
  nepoužil** (klient hodnotu načte a do `in` ji nevloží). U nás je to **vlastnost prvku legendy**
  (pořadí druhů), ne parametr sestavy a ne čtení nastavení linky - stejnou logikou jako umístění
  legendy v bodu 5.
- **Zalamování označení turnusu** - klient volbu posílá, ale žádný engine ji nečte (legacy si u ní
  sám píše „todo"). Nastavit jde, takže brát ji budeme - ale **až s turnusy** v `L6`, samostatně
  nemá co zalamovat.
- **„Vyčká / Navazuje jako řádek"** - tohle **nastavit nejde**: legacy si hodnotu natvrdo nuluje
  s poznámkou „není zatím nikde uloženo", takže v EDISONu pro to není místo. Není co respektovat.
  My přípojné řádky tiskneme, takže jsme v tomhle **napřed z vlastního rozhodnutí** - přestává to
  být náš dluh.

#### 4. Kolik voleb dát uživateli u městských linek

Starý modul nabízel u příměstských a dálkových linek asi deset voleb, u městských reálně **tři**
(kam legenda, rámečky kolem čísel spojů, které typy provozu a směry tisknout).
Zbytek si do okna přečetl a engine ho ignoroval.
Parita tedy pro MHD nedává skoro nic a sada voleb je tam **nová featura**, ne dorovnání.
Proti tomu stojí, že uživatel po přepnutí agendy čeká stejný panel.

**Návrh:** dát MHD tutéž sadu tam, kde má význam; rozdíly nechat jen s doménovým důvodem
(typy provozu MHD má, vlastní sloupce tam smysl nemají - viz `L3`).

**Nerozhoduje se teď** (Jan 21. 8.): **řeší se v MHD kole, ne v AD.** Sada voleb pro městské linky
je rozhodnutí o MHD variantě a to celé jde po AD - jinak bychom teď rozhodovali o modulu, na kterém
nepracujeme.

Co si z průzkumu nese MHD kolo jako vstup: **starý modul u městských linek většinu voleb ignoroval**
(jeho MHD engine čte tři z jedenácti, které má v okně vypsané), takže parita tam nemá čím se řídit
a jakákoli sada voleb je u MHD **nová featura**, ne dorovnání.

#### 5. Kam se tiskne legenda poznámek

Starý modul umí legendu pod každý blok, jednou za směr, nebo celou na konec, volitelně na vlastní
stránku.
My ji dáváme vždy jednou na konec.
Je to volba uživatele, ale varianty „pod každý blok" a „na vlastní stránku" mají smysl teprve nad
rozřezáním tabulky na stránkové bloky.

**Návrh:** volbu zavést mezi parametry (`L3`), varianty závislé na blocích zapnout až s `L5`.
Do `L2` to netahat.

**Rozhodnuto - jinak, než agent navrhoval:** **není to parametr sestavy, ale nastavení v šabloně**
u hlavního prvku (rozhodnutí Jan 21. 8.).

Důvod je silnější než původní návrh: v návrháři je legenda **samostatný prvek**, který si uživatel
položí, kam chce. Starý modul tu volbu měl právě proto, že tuhle svobodu neměl. Přidávat k tomu
ještě parametr by byly dvě pravdy o jedné věci.

Varianta „pod každý blok" se stane vlastností **opakování** až nad rozřezáním na bloky (`L5`) -
i pak je to vlastnost sekce, ne parametr.

#### 6. Texty značek: číselník, nebo opis do našeho kódu

Popisky značek a texty atributů spoje jsou ve starém modulu v číselníku, zvlášť pro češtinu
a slovenštinu (`^JRSET("NCOME",rec,id)`, `^JRSET("STALCOME",rec,id)`).
Část máme opsanou u sebe.
Z číselníku: zákazníkova úprava textu se v tisku projeví a slovenština funguje bez další práce.
Opis: máme kontrolu, ale zákazníkova úprava se neprojeví a slovenštinu řešíme zvlášť.

**Návrh:** brát z číselníku.
Je to zároveň oprava nálezu z `L1`, kde nám u textu atributu přebývalo sloveso.

⛔ **PŮVODNĚ rozhodnuto „brát z číselníku", ZRUŠENO 21. 8. - návrh stál na omylu.**
Texty atributů spoje **v číselníku nejsou**: v `NCOME` a `STALCOME` jsou předložky omezení a stálé
značky, kdežto věty atributů má legacy **zadrátované ve zdroji** (`gAtrNZLi^JRapiNZUtl3:161-192`),
zvlášť česky a zvlášť slovensky. Zákazník je tedy nemůže upravit ani ve starém modulu.
Řešením je **doslovný opis obou sad** - hotovo 21. 8., čímž je vyřešená i stížnost #9088 na
kostrbatou slovenštinu.

#### 7. Kde končí „respektujeme nastavení" a začíná „to řeší šablona"

Pravidlo z 19. 8. říká, že nastavení se neignoruje jen proto, že je to vzhled.
Tři případy stojí na hraně, protože u nich vzhled řídí naše šablona:

| Nastavení | Co říká | Povaha |
|---|---|---|
| Podtržení u zastávek s rozděleným příjezdem a odjezdem | podtrhnout obě zastávky, nebo jen první | čistý vzhled, ale je to u linky a šablona to nemá odkud vědět |
| Tvar tisku místenkování | odřádkovat po částech, vkládat dvojtečky | mění tištěný text, tedy data |
| Skládání tištěného čísla linky | třímístné číslo / vlastní číslo / varianta pro tablo | mění hodnotu, tedy data |

**Návrh:** místenkování a číslo linky jako data.
Podtržení řešit **prezentační nápovědou** v datovém kontraktu (R3) - data řeknou „tady se podtrhává
takto", šablona to smí respektovat.
Byl by to druhý případ téhož mechanismu po barvách spojů.

**Rozhodnuto - směr od Jana 21. 8.:** **vizuální věci patří do šablony, business logika do dat.**

- **tvar tisku místenkování** a **skládání tištěného čísla linky** → **data** (mění tištěný text)
- **podtržení u zastávek s rozděleným příjezdem a odjezdem** → **šablona** (je to čistý vzhled)

⚠️ **Zbývá napětí, které se vědomě neřeší teď:** u podtržení jde o vzhled, ale zároveň je to
nastavené **u linky** - a platí, že co je nastavené, máme brát v potaz. Čistá cesta je táž jako
u schovávání sloupců: vlastnost v šabloně, jejíž výchozí hodnota je **„Automaticky (dle linky)"**.
Zavádět ten mechanismus kvůli podtržení ale nemá smysl, dokud o něj nikdo nepožádal - **na tuhle
jednu položku není poptávka ani tiket**. Řeší se, až se ozve. (Jan 21. 8.: „teď neumím říci".)

#### Co jde dělat i bez těchto rozhodnutí

Rutinní část `L2`, na kterou nemá ani jedno rozhodnutí vliv:

- oprava čtyřstavového přepínače zóny × km (dnes čteme zjednodušeně a u dvou hodnot tiskneme zónu,
  kterou legacy netiskne, km neřídíme vůbec)
- barvy spojů do datového kontraktu (že je posílat máme, je rozhodnuté; otevřené je jen **kde**)
- vyhazování řádků u tarifních čísel, kde spoje nestaví
- vlastní texty sloupce MHD místo dnešních napevno zadaných
- pořadí sloupců spojů proti starému modulu (nález z `L1`, dvě linky)

---

### 13.5 Zadání `L2` - kam patří která položka (projito 2026-08-21)

Průchod celým inventářem §13.2 po rozhodnutích §13.4. Cíl: aby se u každé položky nemuselo znovu
odvozovat, jestli je to data, šablona, parametr, nebo jiná vlna.

#### Rozhodovací pravidlo pro sporné případy

> **Když to jde rozumně mít v šabloně, patří to do šablony** - na úroveň linky jen to, co tam být
> opravdu musí (rozhodnutí Jan 21. 8.).
>
> Důvod je situační a stojí za to ho říct nahlas: **starý modul se přepisuje a část nastavení se
> může přesunout jinam.** Čím méně věcí visí na konkrétní lince, tím méně se toho bude přetahovat.
>
> ⚠️ Neruší to pravidlo z 19. 8. „co je nastavené, bereme v potaz". Obojí drží **třístav**: vlastnost
> žije v šabloně (nebo jako parametr), ale její výchozí hodnota je **„Automaticky (dle linky)"**,
> takže nastavení u linky se respektuje, dokud ho uživatel nepřebije.

#### Kategorie použité v tabulce

| Značka | Znamená |
|---|---|
| **data** | respektuje se při načítání dat - mění hodnoty, řádky nebo texty |
| **třístav** | vlastnost šablony nebo parametr s výchozí hodnotou „Automaticky (dle linky)" |
| **šablona** | vlastnost prvku nebo šablony; z nastavení linky se nebere |
| **parametr** | volba uživatele u tisku (patří do `L3`) |
| **jiná vlna** | `L5` layout · `L6` turnusy a obousměrný tisk · `L7` sdružený JŘ · MHD kolo |
| ⛔ | vědomě nikdy |

#### A. Volby uživatele u tisku

| Položka | Kam | Poznámka k práci |
|---|---|---|
| dělení sloupců na skupiny dnů | ✅ hotovo (parametr) | `L3` to rozšíří na vlastní sloupce |
| kam jde legenda | **šablona** | legenda je samostatný prvek; „pod každý blok" je vlastnost opakování až s `L5` |
| zvýraznit zvolenou zastávku | **parametr** + **data** | parametr říká kterou, data řádek označí příznakem, vzhled řeší šablona (týž vzor jako vývěsková „aktuální zastávka") |
| zvýraznit konkrétní tarifní číslo | **parametr** + **data** | dtto |
| číslo linky/vlaku v přípojném řádku | **šablona** | wire už nese `LINKA` a `SPOJ` zvlášť, takže je to skládání textu; ⚠️ samotnou větu vzít z číselníku (§13.4 bod 6) |
| bez rámečků kolem čísel spojů | **šablona** | u nás mřížka prvku; vedlejší efekt „vypne i čísla spojů" je MHD, do MHD kola |
| turnusy · obousměrný tisk | **`L6`** | nejsou to přepínače, ale chybějící kusy tabulky |
| výběr typů provozu a směrů | **MHD kolo** | AD engine to nečte vůbec |
| dorovnání prázdnými sloupci | **`L5`** | jako parametr - KORID (#8372) chce volbu u tisku, ne v šabloně |
| šířka plochy, počet řádků, měřicí řetězce | ⛔ | layout měříme sami |

#### B. Globální nastavení programu

| Položka | Kam | Poznámka k práci |
|---|---|---|
| tvar datumů v omezeních | ✅ hotovo | včetně per-linka override |
| které atributy se tisknou nad spojem | **data** | dnes `TODO` v kódu |
| tvar tisku místenkování | **data** | mění tištěný text |
| vlastní text sloupce MHD místo „IDP-P"/„IDP-Z" | **data** | + jeho řádek legendy |
| odstranit písmena z označení zóny | **data** | pozor, **linka to přebíjí** |
| pořadí a tisk dopravců, souhrn linek k dopravci | **data** | dorovnat naši vlastní logiku `PopisDopravce` |
| texty atributů spoje | **data** | ✅ hotovo 21. 8. **doslovným opisem legacy šablon**, ne z číselníku - ten je nemá (viz bod 6) |
| počet druhů místenkování dle jazyka (2 × 4) | **data** | odvozuje se z jazyka, ne ze samostatného přepínače |

#### C. Nastavení u konkrétní linky

| Položka | Kam | Poznámka k práci |
|---|---|---|
| počet km sloupců per směr | ✅ počet hotový | **obsah** jednotlivého sloupce neověřen → do matice |
| čtyřstavový přepínač zóny × km | **data** | ⚠️ oprava: u hodnot `0` a `3` tiskneme zónu, legacy ne; km neřídíme vůbec |
| ručně upravený název zastávky u MHD linky | **parametr** | jde s tvarem názvu do `L3` |
| schovat sloupec tarifních čísel | **třístav** | |
| schovat Tč / čísla spojů / šipky / značky (`MHD2` klíče) | **třístav** | ⚠️ **jsou to AD nastavení**, jen mají v globálu podklíč `MHD2` - čte je AD engine |
| vyhodit řádky, kde žádný spoj nestaví | **třístav**, vyhodnocuje server | ubírá řádky, takže výchozí „dle linky" musí fungovat i bez zásahu uživatele |
| vypnout barvy spojů | **data** + **třístav** | barvy jako prezentační nápověda, přepínač je může potlačit |
| skládání tištěného čísla linky | **data** | mění hodnotu |
| kam s bezbariérovým vozidlem (nad spoj / do poznámek) | **data** | rozhoduje, která kolekce to nese |
| podtržení u zdvojených řádků | **šablona** | není poptávka ani tiket → řeší se, až se ozve |
| ruční zlom stránky na tarifním čísle | **`L5`** | |
| ruční přiřazení spojů do sekcí + jejich stránkování | **`L5`** | |
| záhlaví, objížďkový příznak, poznámka pod dopravce, platnost | **data** | ⚠️ **čteme je, ale v inventáři starého modulu nebyly** - do matice patří stejně, protože i ony mění výstup jen u některých linek |

#### D. Nastavení pod úrovní linky

| Položka | Kam | Poznámka k práci |
|---|---|---|
| řádek se v knižním JŘ netiskne (u tarifního čísla) | **data** | |
| totéž zvlášť pro každý směr | **data** | při zapnutí obou se tarifní číslo přeskočí |
| do které umělé sekce spoj patří | **`L5`** | ovlivňuje rozřezání |

⚠️ **Tuhle vrstvu porovnání celé linky nenajde.** Nastavení není u linky, ale u konkrétního řádku,
takže na lince, kde si ho nikdo nenastavil, se neprojeví vůbec. Chce to **cílený test**, ne odtisk -
a je to zároveň nastavení, které **ubírá řádky**, tedy odchylka, kterou zákazník pozná na první pohled.

#### E. MHD zdroj

Celé do **MHD kola**. Zapsané mezery pro to kolo: typ linky a rozsah negativních značek nečteme
(ověřeno grepem, nula výskytů).

#### Jak se to testuje

Tři různé způsoby, ne jeden:

1. **Přepínače, které jde podstrčit z testu** (volby u tisku) - test je nastaví oběma stranám
   a porovná obojí. Plné pokrytí.
2. **Nastavení uložená v datech** (globální, u linky) - porovnává se **tak, jak je nastavené**,
   pokrytí obou stavů zajistí různost zákazníků. ⚠️ **Povinně změřit, které přepínače jsou v datech
   reálně zapnuté**, a u zbytku napsat **neověřeno** (§13.4 bod 1).
3. **Vrstva pod linkou** - cílený test nad linkou, kde to nastavené je; když taková není, vyrobit
   si vlastní případ, ne tvrdit pokrytí.

#### Doporučené pořadí práce

1. **Změřit výskyt** všech přepínačů vrstev B, C, D v dostupných firmách. Bez toho čísla se neví,
   co jde ověřit hned - a je to podklad pro rozhodnutí, jestli je nakonec potřeba testovací linka.
2. **Opravit tři změřené tiché odchylky** - zóny × km, barvy spojů, řádky bez zastavení. Jsou
   rozhodnuté, mají doklad z `L1` a nezávisí na ničem dalším.
3. **Texty z číselníku** (značky, atributy, sloupec MHD, dopravci) - jeden celek, opravuje nález `L1`
   a rozbíhá slovenštinu.
4. **Třístavy** - schovávání sloupců a řádků; první uplatnění mechanismu „Automaticky (dle linky)"
   v linkovém JŘ, takže sem patří i pravidlo „prázdný sloupec se netiskne" v prvku.
5. **Vrstva pod linkou** - skrývání řádků u tarifního čísla, s cíleným testem.
6. **Zbytek dat** - atributy nad spojem, místenkování, číslo linky, bezbariérové vozidlo, obsah
   km sloupce, naše čtyři nezaevidované klíče.

### 13.6 Změřený výskyt nastavení v datech (2026-08-21, krok 1 vlny `L2`)

Změřeno sondou `LinkovyJrNastaveniProbe` nad **93 133 linkami v šesti firmách** (CB_CSADAD,
CB_GWBUS, KV_DP, VS_CSAD, ZL_KOVED, SKNI_MHDNITRA). Sonda jen čte globály, nevolá `GetData`.

Účel: `L2` má u každého nastavení ověřit **oba stavy**, ale nastavení uložená v datech z testu
přepnout nejde. Tohle měření říká, co jde ověřit hned a u čeho se musí napsat **neověřeno**.

⚠️ **Co číslo neříká:** počítají se všechny linky včetně dávno neplatných. „Vyskytuje se" tedy
neznamená „vyskytuje se na aktuálně platné lince".

#### Ověřitelné hned - oba stavy jsou v datech

| Nastavení | Výskyt |
|---|---|
| které atributy se tisknou nad spojem (`PAtrSp`) | 6 firem ze 6, tři mají `0~0~0~0`, tři `1~1~0~0` |
| tvar datumů v omezeních (`TiskDtm`) | 6 firem, hodnoty `0`×3, `1`×2, `3`×1 |
| **schovat sloupec tarifních čísel** | 1 736 linek nastaveno, z toho **377 zapnuto** |
| **vyhodit řádky, kde žádný spoj nestaví** | 1 145 nastaveno, z toho **362 zapnuto** |
| vypnout barvy spojů | 1 662 nastaveno, z toho **24 zapnuto** |
| odstranit písmena ze zóny - **u linky** | 28 747 nastaveno, hodnoty `0`×25 010, `1`×370, `2`×3 367 |
| čtyřstavový přepínač zóny × km (`linIDS`) | 56 525 nastaveno, hodnoty `1`×1 420, `2`×53 979, **`3`×1 126** |

⚠️ **U `linIDS` se hodnota `0` v datech nevyskytuje ani jednou** - a to je polovina naší chyby.
Druhá polovina ale ověřitelná je: **1 126 linek má `3` („netisknout ani zónu, ani km")** a my u nich
zónu tiskneme. Oprava je tedy doložitelná na reálných datech.

#### Nastaveno tak vzácně, že to samo o sobě nic nedoloží

| Nastavení | Výskyt |
|---|---|
| podtržení u zdvojených řádků | 1 736 nastaveno, ale zapnuto na **jediné lince** |
| ruční zlom stránky na tarifním čísle | **3 linky** |

#### ⛔ Nikde nenastaveno - ověřit proti legacy nejde

Tohle je hlavní výsledek měření. U všech níže platí, že po implementaci půjde napsat jen
**„neověřeno proti starému modulu"**, ne „ověřeno":

| Nastavení | Vrstva |
|---|---|
| tvar tisku místenkování (`MistOR`, `MistNOD`) | globální - **0 firem** |
| vlastní text sloupce MHD (`MHDPZ` P/Z) | globální - **0 firem** |
| tisk provozů dopravce (`SDOP`) | globální - **0 firem** |
| schovat Tč / čísla spojů / šipky / značky (`MHD2` klíče u linky) | u linky - **0 linek** |
| ruční přiřazení spojů do sekcí (`MHD2,umeleDatDoSekci`) | u linky - **0 linek** |
| **celá vrstva D** - řádek se v knižním JŘ netiskne (i per směr), umělá sekce u spoje | pod linkou - **0 výskytů** |

⚠️ **Vrstva D je prázdná úplně.** Byla označená jako „nejtvrdší část matice" a to platí dál, ale
z jiného důvodu, než se čekalo: ne že se těžko hledá, ale že **v dostupných datech neexistuje**.
Nedá se tedy porovnat se starým modulem na reálné lince - buď se pro ni vyrobí vlastní případ,
nebo se implementuje proti čtení legacy zdroje a označí jako neověřená.

#### Co z toho plyne pro rozsah `L2`

**Rozhodnutí Jan 21. 8.: co není nastavené nikde, se teď nedělá** a jde do backlogu.

Důvod je doménový, ne technický: **řada těchto nastavení vznikla v legacy pro jednoho zákazníka**
a nikdo jiný je nepoužívá, takže tam jen visí. Dělat je naslepo a bez možnosti ověření je práce
s nulovou hodnotou; stačí o nich vědět.

`L2` tedy dělá **jen ta nastavení, která jsou v datech reálně zapnutá**:

1. ✅ **hotovo 21. 8.** zóny × km - 1 126 linek s hodnotou `3` doložilo naši chybu
2. ✅ **hotovo 21. 8.** odstranění písmen ze zóny - 28 747 nastavení ve třech hodnotách; ⚠️ u linky
   se ukládá posunuté o jedničku (1 = neodstraňovat, 2 = odstraňovat) a přebíjí globální
3. ✅ **hotovo 21. 8.** řádky bez zastavení - 362 linek zapnuto; třístav a filtr per směr
4. ✅ **hotovo 21. 8.** schování sloupce tarifních čísel - 377 linek zapnuto; první třístav
   „Automaticky (dle linky)" v linkovém JŘ a s ním pravidlo „prázdný sloupec se netiskne" v prvku
5. ✅ **hotovo 21. 8.** barvy spojů - 24 linek vypnuto, 3 926 spojů barvu má; matice se k tomu
   naučila předpoklad nad zdrojovými daty
6. ✅ **hotovo 21. 8.** atributy nad spojem - včetně nastavení „značka atributu nad spoj"
   a čtyř tvarů věty; ⚠️ texty **nejsou v číselníku** (viz bod 6 výše, rozhodnutí se tím ruší)
7. tvar datumů v omezeních - už umíme, jen doplnit do matice

**Do backlogu** (`B98`): tvar místenkování, vlastní texty sloupce MHD, tisk provozů dopravce,
`MHD2` klíče schovávání u linky, **celá vrstva D** (skrývání řádků u tarifního čísla i per směr,
umělá sekce u spoje). Tím se **ruší rozhodnutí §13.4 bodu 2** - „skrývání řádků do `L2`" padlo
za předpokladu, že to jde ověřit; neplatí.

**Nechat být i dál:** podtržení u zdvojených řádků (zapnuto na jediné lince ze 93 tisíc) a ruční
zlom stránky (3 linky) - u obojího navíc platí, že to má jinou vlnu nebo žádnou poptávku.

#### ⚠️ Past, kterou matice sama odhalila (21. 8.)

U nastavení, které něco **potlačuje**, je nulové měření splněné i tehdy, když tu vlastnost **vůbec
neumíme**. „Žádné barvy spojů" vyjde samo, protože barvy neposíláme; „v zóně nejsou písmena" vyjde
na lince, jejíž zóny žádná písmena nemají. Matice by tak tvrdila něco, co jsme neudělali - a test
mrtvých dluhů by k tomu ještě vyzval, ať se z toho udělá hlídané tvrzení.

**Řešení:** takový řádek zůstává na „jen změřit" (`?`), dokud k němu nejde doplnit **předpoklad nad
zdrojovými daty** - „linka má u spojů nastavenou barvu", „linka má v zóně písmeno". Předpoklady
matice zatím neumí a doplní se s tím krokem, který tu vlastnost implementuje.

Dotčené dnes: **barvy spojů** a **odstranění písmen ze zóny**. U schování sloupce tarifních čísel
past nehrozí - tarifní čísla posíláme vždy, takže nula je tam skutečný doklad potlačení.

#### Barvy spojů: v datech jsou, i když se to nezdálo (změřeno 21. 8.)

Předpoklad byl, že barvu u spoje **nejde zadat** (barva se přidávala u linky, ne u spoje), a tím
by celá věc padla. Měření (`LinkovyJrNastaveniProbe.BarvySpoju`) říká něco jiného:

- **1 100 482 spojů** prohledáno, **3 926 z nich má barvu nastavenou** (12. `#`-pole záznamu spoje
  ve tvaru `r;g;b`; `0;0;0` a prázdno = nenastaveno)
- v **deseti různých barvách**, takže to nikdo neklikl omylem jednou
- konkrétní případ k testu: `CB_CSADAD 0380751` spoj 19 = `255;128;255`

Je to necelé procento spojů, tedy niche - ale na lince, kde zákazník spoje obarvil, o tu informaci
v tisku přijde celou. **A hlavně: tím padá důvod, proč to nešlo ověřit.** Existuje linka s barvami
v datech, takže se dá tvrdit obojí - že barvy posíláme, i že je přepínač u linky potlačí.

**Co presne se barví** (čteno v legacy 26. 8., `JRapiLinPrtWin.mac:1422,1428,1434`): barva je
vlastnost **položky uvnitř buňky** (pole `dta,6`) a dostane ji jen **čas**, značka **projíždí** (`|`)
a značka **jede jinudy** (`<`). Obyčejný text barvu nedostává a **pozadí se nebarví vůbec** -
kontrakt na ně pole nemá. Naše `CASY` nesou přesně jen tyhle tři hodnoty plus prázdno, takže se
množina obarvených buněk s legacy kryje beze zbytku; číslo spoje v záhlaví ani řádek platnosti
obarvené nejsou. Přepínač u linky legacy řeší tím, že barvu **vymaže** (`:2102`), tedy `1` = černě -
shodné s naší volbou „Netisknout“.

#### Tvar datumů: dvě různé věci, které se pletou

- **Formát** (tečky vs. lomítka, pořadí složek) je otázka lokalizace, ne nastavení. Opraveno v `B96`
  (datum platnosti s tečkami). Číselníky textů už jazyk rozlišují (`gMode` → CZ / `*SK`), takže
  případné další lokalizace se má věšet tam, ne na nový přepínač.
- **`TiskDtm`** je jiná osa: **jestli a jak se tiskne rok** v textech omezení (0 = jak zadáno,
  1 = rok dvoumístně, 2 = v rozsazích se u „od" rok vynechá, 3 = bez roku). Není to formátování
  na český tvar a **nastavené to je ve všech šesti firmách** ve třech ze čtyř hodnot, takže je to
  živé nastavení a už ho čteme. Neplést s tím prvním.

## 14. Pozastavené tikety helpdesku k linkovému JŘ (projito 2026-08-19)

Projito **všech 28 pozastavených** tiketů v agendách Jízdní řády + MHD. Roztřídění samo nebylo nové
(seznam „Mimo scope zastávkového JŘ" v `customer-requirements-zastavkovy-jr.md` ho drží od 20. 7.);
nové je, **co v nich zákazník konkrétně chce**. Detail tady, ne tam, protože zastávkový dokument
u nich vědomě říká „řeší se se svým trackem".

Všech osm dole má od nás v tiketu slíbeno „vyřešíme v novém návrháři". Nejsou to tedy volitelné
podněty - je to náš vydaný závazek a při plánování vln s nimi tak zacházej.

### 14.1 Požadavky, které mají být pokryté

| Tiket | Zákazník | Co chce | Kam patří |
|---|---|---|---|
| **#9088** | TRANSDEV (SK) | Nejbohatší podnět, pět věcí naráz (viz §14.2) | vlny 2, 3, 5 |
| **#8360** | KU HK | **Přípojné řádky potřebují vlastní vzhled** - kurzívu, tučnost, velikost a barvu textu nezávisle na časech spojů. Dnes čas přípoje splývá s časem spoje a je to matoucí. Zákazník výslovně nechce řešit barvou (tiskne černobíle), chce **řez a velikost** | vlna 2 (u nás levné - kaskáda stylů existuje, přípojný řádek jen nemá vlastní vrstvu) |
| **#11300** | KORID | **Prázdný přípojný řádek nemá zabírat místo.** Uzel má celotýdenní provoz, ale přípoje jen v pracovní dny; při rozdělení na bloky PD × víkend zůstane v druhém bloku prázdný řádek | vlna 2, ale **až po dělení na bloky** - „prázdný" má smysl teprve vůči bloku |
| **#8576** | KU HK | **Zóny do více sloupců**, když má zastávka víc zón (hraniční zastávka mezi zónami jednoho IDS - PID, IDZK). Dnes je zóna jedna hodnota v jednom sloupci | vlna 2 (datová část) + vlna 6 (sloupce jako u km) |
| **#8372** | KORID | **„Vkládat prázdné sloupce" má být volba u tisku, ne v šabloně** - jinak si na to musí dělat druhou šablonu. Navíc u linek s málo spoji to dnes roztáhne rozestupy „do absurdna" | vlna 5 (`Strih`), a rozhoduje o tom, že to bude **parametr sestavy** |
| **#9417** | KORID | **Dopravci v záhlaví jen za spoje na dané stránce.** U velkých linek na A4 (Praha-Rumburk) se vypisují všichni a hlavička přeteče pod tabulku | ⚠️ **Rozpadá se na dvě věci** (rozbor 25. 8.): **přetečení pod tabulku** ✅ vyřešeno - nová šablona má záhlaví ve vlastní sekci, takže tabulku odsune. **Filtrování dopravců** zbývá; tiket žádá **per stránka**, ne per blok. ⚠️ Zákazníkův případ je **souhrnný JŘ dvou linek** (100 400 + 104 400); sdružování se řeší **v jízdních řádech, ne u nás** (upřesnil Jan 25. 8.), takže k návrháři může dorazit jako **jedna linka s mnoha dopravci** - pak odklad padá. Náš výčet navíc zatím nevypisuje čísla spojů u dopravce, takže je kratší než ten, kvůli kterému tiket vznikl |
| **#7487** | KORID | **Legenda do 4 sloupců** (dnes 1-3) s přetékáním delší poznámky do dalšího sloupce. Motivace je táž jako u #9417: šetřit místo, aby legenda nespadla pod tabulku | vlna 6 (vícesloupcová legenda; dnes je legenda tabulka z dat, sloupcový tok nemá) |
| **#9283** | KOVED | **Sdružený JŘ více linek** včetně výlukové varianty, v grafické podobě IDZK. Zákazník si sám řekl o kalkulaci, čeká placenou práci | ⚠️ **mění zařazení `C6`** - „souhrnné JŘ" v roadmapě viselo jako „nejdřív ověřit, jestli to zákazník chce". Chce ⚠️ **Není naše práce** (upřesnil Jan 25. 8.): sdružování linek se řeší v jízdních řádech, návrhář dostane výslednou linku a tiskne ji jako každou jinou. |

### 14.2 #9088 rozepsaný (TRANSDEV, slovenská instalace)

Pět samostatných věcí v jednom tiketu:

1. **Chyba: stránky se slévají do jedné**, když nejsou povolené žádné z poznámek (týdenní / negativní /
   stálé). Zákazník to sám dvakrát zpřesňoval. Je to legacy layoutová chyba, u nás ji dědit nemáme -
   ale je to **testovací případ pro vlnu 5**: prázdná legenda nesmí zamíchat stránkováním.
2. **Směrová zastávka nerespektuje směr** - ukazuje poslední tarifní číslo v obou směrech, čekal
   poslední pro Tam a první pro Zpět. Týká se hlavičkového pole, tedy naší datové části.
3. **Přání: ručně zadaný „název směru" u linky**, vkládaný do šablony jako datové pole - a to
   **i pro zastávkové JŘ**. Vyžaduje nové pole u linky v jízdních řádech, ne jen v návrháři.
4. **Směrová zastávka v podobě názvu MHD** (bez obce) - je to týž problém jako `B60`, tvar názvu
   zastávky se v linkovém JŘ nedá ovlivnit.
5. **Volby, které se dají nastavit, ale nefungují:** „nevypisovat čísla spojů", „tisk turnusů ke spojům",
   „návaznosti v přípojných řádcích". Přesně tři z položek inventáře v §13.2 - potvrzuje, že ta vrstva
   není teoretická, zákazník si ji zapnul a nic se nestalo. (K bodu 5 jsme mu v roce 2024 napsali, že
   turnusy fungují a návaznosti řídí nastavení „Tisk na JŘ jako" = „Přípoj. řádek".)
6. **Texty generovaných poznámek** (vyčká / navazuje) jsou v slovenštině „kostrbaté" a nejdou upravit.
   U nás je ta věta **napevno česky v kódu prvku** (`LineTableElement.PripojText`), takže na SK
   instalaci vyjde česky - horší než legacy. Patří k jazykové variantě číselníků, kterou agenda
   už řeší (`Jazyk()` → `CZ` / `*SK`).

### 14.3 Co se ukázalo jako mimo tento track

- **#11373** (KAD) - negativní značky a poznámky **vedle** tabulky, ne pod ní. V TD to **umíme už dnes**:
  legenda je samostatný prvek, který si uživatel položí, kam chce. Ověřit na reálné šabloně a tiket
  zavřít, ne plánovat.
- **#13070** (BBSK) - vypadá jako linkový, ale zákazníkova předloha je **odjezdová relace**;
  patří tracku Relace, kde ještě nic není.
- **#8175** (TRANSDEV) + bod 12 z **#8785** (POVED) - řazení spojů volitelně per linka a ruční pořadí.
  Je to **editor jízdních řádů** (Studio JŘ), ne tisk. Pro nás z toho plyne jen tolik, že pořadí spojů
  máme napevno podle času odjezdu (`SeradSpoje` = legacy `mkPoradiSp`) a je otevřená otázka,
  jestli existuje nastavení, které to v legacy přebíjí.
- **#12803**, **#7929**, **#8067**, **#8457**, **#9481**, **#9483**, **#10442**, **#12854**, **#11796**,
  **#6386**, **#13025** - editace dat a jiné moduly, netýká se návrháře.
- **#8785** bod 5 (POVED) - „vejdi výstup na jednu nebo dvě strany podle mé volby". Ve starém modulu to
  odmítnuto jako principiálně nemožné, ale **náš engine na to má lepší pozici** (legacy `Strih` už umí
  režim „nejmíň stránek"). Zapsat jako podnět k vlně 5, ne slibovat.


---

## 15b. Sada testovacích linek (změřeno 25. 8.)

Konkrétní linky, na kterých je vidět **jedno určité nastavení**. Vyhledala je sonda
`LinkovyJrNastaveniProbe.KdeToZkouset` (běh 25. 8.) plus linky, na kterých se ladilo stránkování.
Smysl: procházet business logiku v aplikaci proti něčemu, co to nastavení opravdu má - ne hledat
naslepo.

| Firma | Skupina | Linka | Co je na ní vidět |
|---|---|---|---|
| CB_CSADAD | 1 | 0370618 | **ruční pořadí spojů** u linky (a že se použije, ne dopočítá) |
| CB_CSADAD | 591 | 0380751 | **barevné spoje** (barva je na spoji, linka ji umí potlačit). ⚠️ **Slabý doklad** (změřeno 26. 8.): v celé firmě má barvu **2 spoje z 483 246**, oba na téhle lince a oba světle růžové `255;128;255` - jeden z nich je **spoj 19**. Změna volby tedy přebarví dva sloupce z tisíců, takže na plné tabulce vypadá, že se neděje nic. Sledovat přímo spoj 19, ne celkový dojem. |
| CB_CSADAD | 801 | 0345062 | **bez zón i bez km** (čtyřstavový přepínač `linIDS` = 3) |
| VS_CSAD | 5 | 0771901 | **písmena ze zóny se odstraňují** |
| CB_GWBUS | 106 | 0320144 | **schovaný sloupec tarifních čísel** |
| CB_GWBUS | 104 | 0320157 | **bez zastávek, kde žádný spoj nestaví** (ubírá řádky, nejvíc vidět) |
| CB_CSADAD | 1 | 0330510 | **rozřezání na bloky**: 41 spojů, přípojné řádky, tři skupiny dnů (37/1, 38/2, 41/3 podle volby dělení) |
| CB_CSADAD | 1 | 0330511 | **jediná skupina dnů** - všechny spoje jen v pracovní dny, pás se nemá podle čeho rozdělit (viz `B101`) |
| ZL_KOVED | 260 | 0000621 | **barevné spoje, kde je to vidět** - 48 obarvených spojů (změřeno 26. 8.). Náhrada za 0380751, kde jsou dva světle růžové a přepnutí volby není v tabulce poznat. |
| CB_GWBUS | 21 | 0322993 | **barevné spoje** - 9 obarvených spojů; drží se, když už jsi v téhle firmě kvůli sloupci Tč a zastávkám |

⚠️ **Tři linky jsou v jiné firmě než CB_CSADAD** (VS_CSAD, CB_GWBUS). Sonda prohledává víc firem,
ale návrhář pracuje s tou, do které je uživatel přihlášený - na ty tři je potřeba se přihlásit jinam,
nebo si k témuž nastavení najít linku ve vlastní firmě.

⚠️ **Sada nepokrývá nastavení, která nikdo nemá zapnutá** (analýza §13.4) - u nich není proti čemu
porovnávat a žádná linka je neukáže.

⚠️ **„Linka to nastavení má“ ještě neznamená „je to na ní vidět“** (naučeno 26. 8. na barvách spojů). Sonda
hledá **výskyt** hodnoty, ne její dopad na výstup - u nastavení, které se týká jednotlivých spojů,
může být zasažený jeden sloupec z tisíce. U každé linky v sadě proto patří **i to, kde se to na ní
projeví**; bez toho se hlásí „nemění se nic“ tam, kde se mění dva spoje.

**Šablona k procházení:** `templates/prochazeni-linkovy-jr.json` (Soubor → Importovat).
Vychází ze systémové šablony pro AD, ale je **na šířku** a má **všechny popisné sloupce zapnuté** -
jinak není poznat, že je linka schovává. Dorovnání prázdnými sloupci je schválně vypnuté, ať jde
porovnat obojí. Ověřeno načtením: 3 sekce, 6 viditelných sloupců, šířka tabulky 277 mm.

**Jak porovnávat:** ke každé lince pustit starý tisk a dát vedle sebe s naším náhledem (Jan má
starý modul k dispozici). Rozdíly zapisovat do tří škatulek, které projekt používá jinde:
**odchylka** (chceme se lišit, s důvodem), **dluh** (nechceme, ale zatím ano - patří k vlně),
**rozdíl** (chyba, opravit). Bez toho rozlišení se za měsíc nepozná, co byl záměr a co nedodělek.

## 15. Co ve starém modulu nešlo nastavit (podklad Zlín, 2026-08-19)

Kolega zkoušel ve **starém** modulu vyrobit linkový JŘ podle vzoru IDZK a narazil na řadu věcí,
které se nedaly nastavit. Zapsáno od Jana 19. 8. včetně dvou PDF: cílový vzor (linka 940
Kroměříž-Přerov) a to, co ze starého modulu reálně vyšlo (linka 439 Konice-Olomouc).

⚠️ **Nejsou to požadavky na paritu.** Skoro všechno níže je **vzhled**, a ten porovnání se starým
modulem (`L1`) záměrně neměří - v novém návrháři je to práce šablony, ne dat. Otázka tedy nezní
„chováme se jako starý modul", ale „umí to návrhář nastavit". U většiny bodů je odpověď ano, a to
je samo o sobě argument pro náhradu.

### 15.1 Rozdíl obou PDF na první pohled

| Vzor (940) | Co vyrobil starý modul (439) |
|---|---|
| info sloupce **km, zastávka, zóny** | **zóny, km, zastávka, MHD** + úzký sloupec se šipkou |
| jeden nadpis **„↓ Směr jízdy ↓"** přes všechny info sloupce | každý info sloupec má vlastní popisek, společný nadpis nejde |
| **mezery mezi bloky** info × pracovní dny × víkendy | souvislá mřížka bez mezer |
| km bez vnitřního ohraničení | každý km podsloupec orámovaný zvlášť |
| jemné vodorovné linky, střídavé podbarvení | plná mřížka všude |
| legenda ve dvou sloupcích pod tabulkou | (na první straně není) |

### 15.2 Body ze zadání a stav v návrháři

Ověřeno čtením kódu 19. 8., ne odhadem.

| # | Co ve starém modulu nešlo | Stav v návrháři |
|---|---|---|
| 1 | odstranit **šedivý pruh nad nadpisy** | ✅ **umíme** - pás skupin dnů i řádek záhlaví berou barvu z `HeaderStyle` prvku (pozadí i text), nastavuje se v panelu |
| 2 | **změnit pořadí info sloupců** (zóny, km, zastávky…) | ✅ **umíme** - sloupce jsou seznam a dialog je umí přesouvat (`ColumnListEditor`, `MoveUpCommand`) |
| 3 | **odstranit sloupec MHD a sloupec se šipkou** | ✅ **umíme** - MHD má zaškrtávátko viditelnosti a navíc zmizí sám, když je pro celou linku prázdný; **sloupec se šipkou v prvku vůbec není** (přidat ho má naopak `L6`) |
| 4 | **odstranit ohraničení jednotlivých km sloupců** | ✅ **umíme** (26. 8., `L9`) - „Popisné sloupce bez vnitřních čar" slije km, zastávku a zóny do jednoho bloku a čáry mezi sloupci spojů nechá. ⚠️ Původní tvrzení „kreslí se buď všude, nebo nikde" **neplatilo**: `Grid.ShowColumnLines` prvek vůbec nečte a mřížku kreslí vždy (ověřeno vykreslením 27. 8., vedeno jako `L10`) |
| 5 | **jiný styl čar mezi spoji** pro celou sekci | 🟡 **částečně** - `GridStyle` má zvlášť vodorovné a svislé čáry, ale **jednu barvu, tloušťku a styl pro obojí**. Vzor má jemné vodorovné a žádné svislé, což už jde; různá barva pro každý směr ne |
| 6 | **jeden nadpis přes info sloupce** (jako „Pracovní dny" nad spoji) | ✅ **umíme** (26. 8., `L9`) - „Nadpis popisných sloupců". Pás se přitom musel odvázat od skupin dnů: kreslil se jen při víc než jedné skupině, takže na lince s jedinou by nadpis nebyl |
| 7 | **mezera mezi bloky** (info × pracovní dny × víkendy) | ✅ **umíme** (27. 8., `L9`) - „Mezera mezi skupinami sloupců" v mm, výchozí 0. Bere se ze šířky prvku, takže se do řádku vejde méně spojů |
| 8 | **popisky info sloupců podle vzoru** | ✅ **umíme** - hlavička sloupce je volný text |
| 9 | **záhlaví napevno; pruh dopravce se nevejde, když je dopravců víc** | ⚠️ **Ověřeno 25. 8. vykreslením - tvrzení „umíme lépe" NEPLATILO.** Textový prvek sice roste, ale **prvky uvnitř sekce se neposouvají**, takže záhlaví s 26 dopravci leželo přes tabulku stejně jako v legacy. Řeší to **rozdělení na dvě sekce** (tok mezi sekcemi funguje), a tak to teď zakládá nová šablona. |
| 10 | **levé ohraničení, které leze, i když ho nikde nemám** | ✅ **odpadá** - vada starého modulu; v návrháři je vnější obrys rámeček prvku a ten se dá vypnout |

### 15.3 Co z toho plyne

- **Šest z deseti bodů návrhář už dnes umí** a jsou to přesně ty, kvůli kterým se starý modul
  nahrazuje. Do roadmapy nepatří jako práce, ale **do ověření na reálné šabloně** (Zlín 940 je
  dobrý zkušební vzor - je to reálné zadání zákazníka, ne vymyšlený případ).
- **Tři mezery jsou drobné a patří k sobě** (ohraničení per sloupec, nadpis nad info sloupci,
  mezera mezi bloky) - všechny jsou o tom, aby tabulka nevypadala jako jedna souvislá mřížka.
  ✅ **Všechny tři hotové jako `L9`** (26.-27. 8.), a potvrdilo se, že je to jeden pojem:
  **skupina sloupců** s nadpisem, bez vnitřních čar a s mezerou za sebou.
- **Sloupec se šipkou** má `L6` v plánu přidat; tady je doklad, že se hodí i **vypnout**.
- ⚠️ **Neověřeno:** v PDF ze starého modulu jsou v buňkách písmena (`K`, `A`, `J`, `D`) tam, kde
  vzor má piktogramy (`|`, `⟨`). Může to být nevložený symbolový font v tom konkrétním PDF, nebo
  jiný obsah - **rozhodne se to až s `L4`**, netvrdit nic dopředu.


## Zdrojové soubory citované v této analýze

```
C:\DEV\EDISON\JRMAIN\CACHE\JRapiLinPrtWin.mac                - hlavní oracle (mkPLinWin, mkPRelWin)
C:\DEV\EDISON\JRMAIN\CACHE\ALVA\JRMAIN\MLAPI.cls              - wrapper volající mkPLinWin
C:\DEV\EDISON\JRMAIN\DOCS\06-sestavy-tisk.md                  - přehled tiskových kanálů JRMAIN
C:\DEV\EDISON\JRMAIN\DOCS\07-cache-api.md                     - API detaily
C:\DEV\EDISON\TiskJR\CACHE\ALVA\TISKJR\Data.cls               - formátovací vrstva → VB.NET klient
C:\DEV\EDISON\TiskJR\CACHE\ALVA\TISKJR\Main.cls               - CRUD sestav (precedens, ne data)
C:\DEV\EDISON\TiskJR\CACHE\ALVA\TISKJR\Sestavy.cls            - správa sestav (precedens, ne data)
C:\DEV\EDISON\TiskJR\_ruzne\                                  - TTF fonty (Timetable_nove aj.)

TemplateDesigner\.claude\docs\legacy-nahrada.md
TemplateDesigner\.claude\docs\legacy-nahrada.md
TemplateDesigner\.claude\docs\roadmap.md  (Linkové a knižní JŘ)
TemplateDesigner\.claude\docs\ad-data-mapa.md
TemplateDesigner\.claude\docs\architektonicka-rozhodnuti-agendy.md
TemplateDesigner\.claude\rules\new-element-checklist.md
TemplateDesigner\.claude\rules\cache-api.md
```
