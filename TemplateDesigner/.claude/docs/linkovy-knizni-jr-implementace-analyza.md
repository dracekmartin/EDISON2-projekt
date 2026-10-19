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

✅ **Tuhle volbu máme od 6. 9.** (`TableLayout.ZvolBloky`) - u **jednosměrného** tisku. Je to důvod,
proč ze starého modulu koukají listy jen s pracovními dny, přestože to nikdo nenastavil.

⚠️ **U obousměrné sestavy se vědomě lišíme.** Starý modul obousměrnou tabulku, která se má řezat,
**zahodí** a tiskne po směrech (`JRapiLinPrtWin.mac:628`: obousměrný tisk zůstane jen tehdy, když řez
vyšel na jediný blok - vodorovně i svisle); uživateli o tom neřekne nic, takže si dá „Ano" a dostane
dvě jednosměrné sady. My obousměrný tisk **držíme** a řežeme ho podle šířky (rozhodnutí Jan 6. 9.:
co je zaškrtnuté, to se tiskne, žádný tichý fallback). Řez podle skupin tam nejde použít ani
teoreticky - slot nese spoj z každého směru a jejich hranice skupin padnou jinam.

Vynucené oddělení nastavením u linky (`MHD2,rozdelitUmeleSekceNaVlastniStranky`) neděláme: na
93 133 linkách šesti firem ho nemá zapnuté nikdo, takže by nikomu nic nezměnilo.

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
| Výchozí šablona | `dotnet/SystemTemplates/linkovy-jr-ad-postskript-a5.json` (AD, tvar postskriptového tisku). Obecná A4 se 22. 9. zrušila - do verze jde jedna dotažená, ne dvě rozpracované. **MHD nemá.** |
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
| `ZonaIdsDelZnk` | odstranit písmena z označení zóny | ✅ doloženo 15. 9. (`ZL_KOVED 1 / 0823955`: `Z420,I201` → `420,201`) |
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
| `PATCH,ZonaIdsDelZnk` | **přebíjí** globální hodnotu téhož jména (`:1847`) | ✅ doloženo 15. 9. na `ZL_KOVED 1 / 0823955` (globální 1, u linky 2) |
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
| **#7487** | KORID | **Čtyři poznámky na řádek u jednoho druhu** (starý umí 1-3). ⚠️ **NEJDE o sloupce legendy** - viz oprava 3. 9. níže. Motivace je táž jako u #9417: šetřit místo, aby legenda nespadla pod tabulku | neumíme: náš počet sloupců je jednotný pro celou legendu, starý má počet **per druh poznámky** |
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

- **#11373** (KAD) - negativní značky a poznámky **vedle** tabulky, ne pod ní. Umístit vedle jde,
  prvky mají volnou pozici. ⚠️ **Výhrada „u linkových to není prvek Poznámky" padla `B117`** (3. 9.):
  od té doby je prvek Poznámky i u druhu agendy `LINE` a legendu skládá včetně negativních značek -
  ověřeno na výtisku `0330510` 15. 9. (značky `21`, `23`, `70` i dny `X`, `6`, `7`, `+`).
  Co zůstává: filtry na **kategorie** (bezbariér, na znamení, přestupy) jsou u linkových nečinné,
  protože linkový posílá kategorii prázdnou, a **počet poznámek na řádek per druh** prvek neumí
  vůbec (`#7487`).
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
| CB_CSADAD | 4 | 0330520 | **ruční pořadí spojů** u linky - a že se použije, ne dopočítá. Ručně `1,3,5,7,101,103,105`, podle času by vyšlo `1,101,3,103,5,7,105`; ověřeno voláním 15. 9., vracíme ruční. ⚠️ **Ne `1 / 0370618`**, jak tu stálo do 15. 9.: ta ruční pořadí sice má, ale shodné s časovým, takže na ní rozdíl vidět není. Linky, kde se obojí liší, najde `LinkovyJrNastaveniProbe.RozdilneRucniPoradi` (8 z 11 měřených) |
| CB_CSADAD | 591 | 0380751 | **barevné spoje** (barva je na spoji, linka ji umí potlačit). ⚠️ **Slabý doklad** (změřeno 26. 8.): v celé firmě má barvu **2 spoje z 483 246**, oba na téhle lince a oba světle růžové `255;128;255` - jeden z nich je **spoj 19**. Změna volby tedy přebarví dva sloupce z tisíců, takže na plné tabulce vypadá, že se neděje nic. Sledovat přímo spoj 19, ne celkový dojem. |
| CB_CSADAD | 801 | 0345062 | **bez zón i bez km** (čtyřstavový přepínač `linIDS` = 3) |
| ZL_KOVED | 1 | 0823955 | **písmena ze zóny se odstraňují** - v datech `Z420,I201`, agenda vrací `420,201`. ⚠️ **Ne `VS_CSAD 5 / 0771901`**, jak tu stálo do 15. 9.: Vsetín na to data nemá a zóny vůbec nevyplňuje. Linku se zónami najde `LinkovyJrSpojeProbe.Zony` - zóny má jen zlomek linek i tam, kde je tisk zón zapnutý |
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
| 3 | **odstranit sloupec MHD a sloupec se šipkou** | ✅ **umíme** - obojí má zaškrtávátko viditelnosti; MHD navíc zmizí sám, když je pro celou linku prázdný. ⚠️ **Sloupec se šipkou od 10. 9. existuje a je z výroby ZAPNUTÝ** (`B136` - nese `př`/`od` u rozdvojené zastávky, což je jediné, čím se ty dva řádky liší). Do 10. 9. tu stálo, že v prvku vůbec není; kdo chce vzor IDZK, musí ho teď vypnout |
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
- **Sloupec se šipkou** přibyl 10. 9. (`B136`) a je z výroby zapnutý; tady je doklad, že se hodí
  i **vypnout** - vzor IDZK ho nemá.
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

---

## 15. Přípojné řádky - legacy proti nám (projito 4. 9. 2026)

Vzniklo při dokládání `#11300`, když Jan poslal výtisk starého modulu téže linky.

**Co to je:** spoj má u zastávky zadané omezení návaznosti - buď tam **vyčká** příjezdu jiného
spoje (uzel `31`), nebo na něj jiný spoj **navazuje** (uzel `32`). Přípojný řádek to ukáže pod
řádkem zastávky: čas toho druhého spoje ve sloupci spoje, kterého se to týká.

**Jak to dělá starý modul** (`JRapiLinPrtWin01.mac:PripojneRadky`): řádek nepatří spoji, ale
**skupině**. Agregační klíč je `tarifní číslo | směr | druh dopravy | (linka)`, takže do jednoho
řádku se sejdou přípoje různých spojů, když vedou stejným směrem stejným druhem dopravy; časy se
rozdělí do sloupců podle spoje. Když už ten spoj ve řádku čas má, založí se řádek další.
Hrubost řídí volba **„Návaznosti v přípojných řádcích"** v jeho návrháři, per sekce šablony:
*Ne* (linka v klíči není, slučuje se nejvíc) / *Ano, dle licence* / *Ano, dle IDS*
(`in("A",2,"arr",9)`, `stDL_NavaznostiVPripojnychRadcich`). Popisek je souhrnný -
**„ze směru Trhové Sviny"** u vyčkání, **„směr České Budějovice"** u navázání, plus „př" / „od"
a ikona druhu dopravy.

**Jak to děláme my:** řádek per (spoj × přípoj), žádná agregace, popisek konkrétní („vyčká na
spoj 21 linky 320163 do Trhové Sviny"). Zapíná se jedním přepínačem **Přípojné řádky** na prvku
tabulky, bez volby hrubosti. Druh dopravy se u přípoje na drátě **vůbec neposlá** (používá se jen
v textu poznámky) a směr je slitý do textu popisku.

### 15.1 Co z toho zbývá

- ✅ **Neúplný přípoj řádek nedostane** (hotovo 4. 9.). Starý modul zahodí přípoj bez minuty
  příjezdu nebo bez směru; my jsme obojí posílali dál a v tabulce vznikl řádek s časem „00:00"
  a prázdným popiskem. **Naměřeno: 965 z 71 889** přípojů, které řádek dělají (CB_CSADAD 504,
  CB_GWBUS 461, VS_CSAD a KV_DP nula). Drží `LinkovyJrPripojeTest`.
- ✅ **Slučování řádků podle směru** (hotovo 4. 9., rozhodnutí Jan: šetření místa váží víc než
  číslo linky u každého řádku). Volba **Slučovat přípoje** na prvku: *Ne* / *Podle směru* /
  *Podle směru a linky*, výchozí **podle směru** - dnešní tvar byla odchylka, ne volba, takže se
  výchozí uplatní i na šabloně uložené dřív. Sloučený řádek má popisek „ze směru X" / „směr X".
  Klíč kopíruje legacy (tarifní číslo + směr + druh dopravy, volitelně linka); na drát proto přibylo
  pole `AVL` (aditivní změna, zlaté odtisky přebaselinovány v témž commitu).
  ⚠️ **Dva přípoje téhož spoje zakládají řádek další** - jeden řádek nese od jednoho spoje jen
  jeden čas, jinak by se tiše přebil. Drží `PripojeSlucovaniTests` (čtyři mutace shozené).
- ✅ **Vlastní „linka pro tisk" přebíjí číslo linky** (hotovo 4. 9.). Starý modul to dělá **vždy**,
  když je pole vyplněné (`JRapiLinPrtWin01.mac:76,135`) - u vyčkání 11. položka věty, u navázání
  12. **Naměřeno: 3 515 z 71 889** přípojů ho má a u **3 512** se číslo liší - CB_GWBUS 29 %,
  VS_CSAD 10 %, CB_CSADAD 0,2 %, KV_DP nula. U vlaku stojí v surovém poli číslo vlaku („Os 8009"),
  ale tisknout se má označení linky („S1"); jinde je to zkrácené číslo (`330518` → `518`) nebo
  bez vodicích nul (`000485` → `485`).
  ⚠️ **Linky jádra zlatých odtisků to pole nemají**, takže opravu nekryjí - drží ji
  `LinkovyJrPripojeTest` na `CB_GWBUS 2 / 0320144` (mutace „přepis zrušen" ji shodí).

- ⛔ **Volba „Návaznosti v přípojných řádcích" (Ne / dle licence / dle IDS) se nedělá** - v přípojných
  řádcích nic nemění. Řídí jen to, jestli linka jde do agregačního klíče, a hodnota *dle IDS*
  padá v `$case(typslucovani,1:liv,:"")` do výchozí větve, tedy se chová stejně jako *Ne*.
  ⚠️ **Není to o tom, které číslo linky se tiskne** - to řeší „linka pro tisk" výše a děje se
  nepodmíněně. Naše volba **Slučovat přípoje** (*Ne* / *Podle směru* / *Podle směru a linky*)
  legacy trojici pokrývá a není matoucí (do 4. 9. tu bylo naopak napsané, že jde o volbu čísla -
  vyvráceno měřením).
---

## 16. UZAVŘENO: spoje s trojčíselným číslem se nekreslily (4. 9. 2026)

**Projev:** na `CB_GWBUS 2 / 0320144` chyběly v tabulce spoje **501-509**.

**Vyřešeno.** Po opravě „šablona bez vlastních sloupců už nedostane cizí rozvržení"
(changelog 2026-09-04 (12)) se na téže lince tiskne první blok jako `61 1 31 33 5 73 37 63 501`
vlevo a `2 62 4 32 6 34 64 38 502` vpravo - tedy přesně prvních devět spojů každého směru
v serverovém pořadí. Řez do bloků funguje. Ověřeno v aplikaci 4. 9. odpoledne.

⚠️ **Nelze doložit, která změna to spravila** - původní běh se nezreprodukuje (měl i jiné období
platnosti, 2025 místo 2026). Proto neplatí ani pozorování zapsané tu ráno („první blok obsahuje
49, 71 a 51"): bylo z rozbitého stavu.

### 16.1 Proč se nedělá varování o nezařazených spojích

Zvažovalo se přidat u linkových JŘ varování „spoj nesedl do žádného sloupce" (jako mají vývěsky
`NEZARAZENE_SPOJE`). **Zamítnuto 4. 9. (rozhodnutí Jan):** varování, po kterém sloupec pořád chybí,
je obklad - a hlavně **ta situace se z aplikace vyvolat nedá**:

- Spoj by vypadl jen definicí sloupců se **značkovými filtry** (`znackyJen`/`znackyBez`), ale ten
  editor (`DepartureGroupEditorDialog`) zapisuje **jen do prvku vývěsky**. Linková tabulka dostane
  `CustomColumns` výhradně jako hotový vzor ze serveru (den-based, bez filtrů), nebo null - editor
  pro ručně skládané sloupce u ní není.
- Druhá cesta je spoj s **nerozpoznanou týdenní platností** (`DnySpoje` → „-"). To ale není vada,
  ale **vědomá parita**: starý modul takový spoj netiskne také (`KamPrt=0`).
- Vývěska, kde se díra v definici vyrobit dá, varování už má.

⚠️ **Kdyby k linkové tabulce editor vlastních sloupců se značkami přibyl, tahle mezera se otevře** -
pak se rozhoduje ne o varování, ale o tom, kam takový spoj vytisknout (legacy dává spoj bez
dopočítané platnosti do **všech** sekcí, `JRapiLinPrtWin:476-481`).

### 16.2 `#11300` (KORID) - co zbývá udělat

**Zákazník** Jaroslav Smištík, KORID; tiket **pozastavený** od 8. 12. 2025 s odpovědí Jiřího Bžocha
*„úpravu letos nestihneme, příští rok to spojíme s novým tiskovým modulem"*. Od té doby mu nikdo
nic neukázal, takže zbývá **ukázka a odpověď, ne kód**.

**Co přesně chce** (jeho slova): *„uvítali bychom, kdyby byla možnost prázdný přípojový řádek
(v situacích kdy je PD a víkend na zvláštní tabulce/listu) netiskl a šetřil místo."* Na jeho druhém
obrázku je list **jen s pracovními dny** a v něm řádek `ze směru Nové Zákupy` úplně prázdný - ty
přípoje jsou jen o víkendu. Je to **linkový tabelární JŘ** (linka 260, IDOL), přes název tiketu
„Dotaz k vývěsným JŘ".

**Čím je to hotové:**

- **Prázdný přípojný řádek u nás nemůže vzniknout konstrukcí** - řádek se negeneruje pro zastávku,
  ale pro každý přípoj **spojů toho bloku** (`PrazdnyPripojnyRadekTests` + dvě mutace).
- **Přípoje téhož směru jdou na jeden řádek** (volba „Slučovat přípoje", 4. 9.) - týž tvar, jaký má
  jeho výtisk ze starého modulu; do té doby jsme dělali řádek pro každý spoj.
- ⚠️ **Jeho rozvržení jde vyrobit teprve od 6. 9.** Žádá o šetření místa na listu jen s pracovními
  dny, a oddělování skupin dnů jsme do té doby neměli (§6.3). **Musí se tisknout jednosměrně** -
  u obousměrné sestavy se skupiny neoddělují a prázdné buňky v přípojném řádku vzniknout mohou.

**✅ Doloženo okem v návrháři 23. 9. - `CB_CSADAD 1 / 0330510`, směr Zpět, jednosměrný tisk,
dělení *Pracovní dny / víkend+svátky*.** (Jeho vlastní firma ČSAD Liberec / IDOL na dev serveru
není, proto náhrada.) Linka se nevejde na šířku, takže se skupiny oddělí samy:

- **Tabulka pracovních dnů** má u Českého Krumlova pět přípojných řádků (Přídolí, Velešín,
  Rožmberk, Větřní, Horní Planá), každý s časem.
- **Tabulka sobot, nedělí a svátků** má jen „směr Horní Planá" - ostatní čtyři přípoje jsou jen
  v pracovní dny, starý tisk by je tu nechal prázdné.

⚠️ **Zápis z 15. 9. tvrdil opak** („list jen s pracovními dny nemá jediný přípojný řádek") a 23. 9.
podle něj agent Janovi popsal, co má uvidět - nesedělo to. Než se tvrzení odsud použije jako
očekávaný výsledek, ověřit ho na obrazovce.

⚠️ **Rozdělení nejde vynutit** - skupiny se oddělí jen tehdy, když se linka nevejde na šířku
a oddělení nestojí blok navíc. Na lince, která se vejde (`0330512`), stojí skupiny v jedné tabulce
a přípojný řádek pracovních dnů jde i přes nedělní sloupce (prázdné buňky, ne prázdný řádek).

⚠️ **Dvě dřívější doporučení linky byla mimo** (obojí ověřeno voláním serveru 15. 9.):

- `0330511` má všech devět spojů v **jedné skupině dnů**, takže se tabulka nemá podle čeho
  rozdělit a zákazníkova situace („PD a víkend na zvláštní tabulce") na ní vůbec nenastane.
- `0370618` dvě skupiny dnů má, ale jsou to dva spoje - oddělení by stálo blok navíc, takže
  se nedělá. Její jediný přípoj navíc nese `RADEK=0`, tedy se v tabulce nekreslí tak jako tak.

**Jak najít jinou linku:** `LinkovyJrSpojeProbe.NajdiProUkazku` hledá linku, kde spoje spadají do
víc skupin dnů a zároveň aspoň jedna skupina nese přípoj tištěný jako řádek. Situace je běžná -
19 z 23 měřených stránek. `LinkovyJrSpojeProbe.Pripoje` vypíše přípoje per spoj včetně `RADEK`.

### 16.3 Měření 4. 9. - sonda

Sonda `ALVA.TemplateDesigner.Tests.LinkovyJrSpojeProbe` je read-only a zůstává použitelná pro
jakoukoli linku: `Spoje` vypíše z odpovědi `GetData` číslo, směr, skupinu dnů, počet neprázdných
časů, NZ a platnost per spoj; `Parametry` vypíše persistované parametry tisku uložené uživatelům;
`Sablony` řekne, které šablony agendy nesou vlastní definici sloupců.

Naměřeno na `0320144`: **43 spojů na směr**, obousměrný tisk je slévá do jedné stránky s 86 spoji.
Spoje 5xx jsou nejúplnější sloupce (501-507 mají čas na všech 36 zastávkách). ⚠️ **Premisa
„obousměrný tisk vypnutý" byla špatná** - uložené parametry mají `OBOUSMERNY_TISK: true`.

### 16.4 Vada nalezená po cestě - `B122`

`Rozvrzeni` (helper, na kterém stojí testy „blok se vejde do šířky prvku") volá `BuildColumns`
s **počtem slotů**, kdežto kreslení (`LineTableElement:1215`) i `Vejde` s **počtem sloupců**
(`naBlok * SloupcuNaSlot`). U obousměrné sestavy tedy testy měří polovinu sloupců a ten invariant
je pro dvousměrné linky nehlídaný. `TableLayout.RozdelSpojeDoBloku` navíc nikdo nevolá (mrtvý kód).


---

## 17. Postskriptový tisk - co z něj umíme nastavit (projito 7. 9. 2026)

Podklad pro zákazníka, který dnes tiskne přes **postskriptový výstup**, ne přes Windows tisk.
Vzniklo při přípravě školení; čísla nejsou odhad, jsou **odečtená z programu zákazníka**
`^JRSET("PS","NVYVESY")` na `CB_CSADAD` (vývěska linky) a z inventáře nastavení u linky
`gSetLinPrt^JRapiLin1:183`.

> ⚠️ **Postskript ≠ Windows tisk.** Jsou to dvě různé cesty s různým kontraktem. Inventář v `§13.2`
> popisuje **Windows** engine (`mkPLinWin`); tady jde o `mkLinPS^JRapiLinPrtPS`. Některé věci má jen
> jedna z nich (tečková výplň jen postskript, `SablonaProTiskA4` jen Windows).

### 17.0 Kilometrické sloupce - kde se filtrují a co bereme špatně (změřeno 7. 9.)

**Km sloupec není spoj, ale profil vzdáleností** podél celé linky (řada km po tarifních číslech).
Profily se **předpočítají a uloží u linky**; starý tisk ani my nic nepočítáme ze spojů.

**Kde se filtruje:** v `mkKms` (`JRapiLinUtlKms1.mac:36-200`), která ty seznamy **generuje** při
uložení linky. Čtyři kroky: posbírat profily ze spojů → zahodit profil **obsažený v jiném** →
volitelně (`Kms,MaxFiltr`) zahodit i ten, kterému v překryvu nevadí projetí `<` → poskládat profily
k sobě → při překročení prahu `Kms,Filtr` (výchozí 10) mazat sloupce od toho startovního bodu, kde
jich je nejvíc, a brát nejkratší s nejvíc projetími. Prahy jsou u linky (`PATCH,Kms,Filtr`) i
globálně (`JRSET("Definice","Kms","Filtr")`).

✅ **Filtr tedy máme zadarmo** - dostáváme už profiltrované seznamy. Není co implementovat.

⚠️ **Co bereme špatně:** legacy čte pro **obousměrný** tisk **společný** seznam
`^JRL(sku,lin,"K")`, pro jednosměrný ty směrové - `JRapiLinPrtWin.mac:2523`, větev `sm="A"`.
Společný je odduplikovaná sjednocenina obou směrů. My čteme směrové vždy a v obousměrné sestavě je
slepíme za sebe, čímž vyrobíme duplicitní sloupce.

Změřeno na `CB_CSADAD 1 / 0330511` (sonda `LinkovyJrSpojeProbe.KmSloupce`):

- společný `^JRL(...,"K")` = **8**, `KmsSmer,T` = **5**, `KmsSmer,Z` = **6** → my sázíme **11**
- tři profily z těch jedenácti jsou obsažené v jiných: `T~01` v `Z~01`, `Z~04` v `T~03`,
  a `T~05` je s `Z~06` totožný → 11 − 3 = 8
- přepínač `PrintKmsSmerNo` u linky ani `PS_PrintKmsSmerNo` globálně nastavené nejsou, takže
  jednosměrný tisk u téhle linky opravdu jede podle směrových seznamů
- prahy: u linky `Kms.Filtr` = 10 a `MaxFiltr` = 0, globálně `Filtr` = 14

Vedeno jako `B124`. **Se variantními sloupci jízdních dob v zastávkovém JŘ to nesouvisí** - ty
vznikají z variant trasy u jedné zastávky a počítají se za běhu na klientovi.

### 17.1 Jak je ten tisk vůbec nastavitelný

Celý layout nese **PostScriptový program uložený v databázi** (`^JRSET("PS",...)`, sedm programů
podle typu sestavy). Server ho předsadí před data a přepíše v něm **devět čísel a logo**
(`LinkyPSTisk.cls:129-138`) - nic víc:

| Přepisuje se | Význam | U nás |
|---|---|---|
| `/LM /RM /TM /BM` | okraje stránky (mm) | ✅ vlastnosti stránky |
| `/Spojscale` | velikost písma hodin | ✅ styl datových buněk |
| `/Spojscale1` | velikost písma minut | ✅ volba prvku „Velikost minut" (podíl) |
| `/zastwidth` | šířka sloupce zastávek | ✅ šířka sloupce v editoru |
| `/LgX /LgY` + program loga | logo a jeho pozice | ✅ obrázek |

Všechno ostatní v programu je **mimo dosah uživatele** - kdo chce jiný vzhled, objedná si u nás
úpravu `.ps` programu.

### 17.2 Ostatní hodnoty v jejich programu

Reálné hodnoty vývěsky `CB_CSADAD`: stránka **208 × 145 mm**, okraje 15/15/12/13, `zastwidth 45`,
`Linkascale 11.0` (název linky), `Prepscale 7` (dopravce), `Spojscale 6.0` / `Spojscale1 4.5`
(**poměr přesně 0,75**), `tl1 0.1` / `tl2 0.6` (tloušťky čar).

| Program | Význam | U nás |
|---|---|---|
| `PLW`, `PLH` | rozměr stránky | ✅ vlastnosti stránky |
| `Linkascale`, `Prepscale` | velikost názvu linky a řádku dopravce | ✅ styl textového prvku |
| `tl1`, `tl2` | tenká a tlustá čára | ✅ tloušťka mřížky a rámečku prvku |
| `Plascale` | velikost pásu značek platnosti | ✅ volba prvku „Velikost značek" (podíl, výchozí 1,5 = 9/6) |
| `Vyscale`, `NSL`, `prvnip`, `more`, `h3`, `LgK`, `LgV`, `LgPl` | ⛔ **význam nedekódován** | - |

⚠️ Poslední řádek je poctivě otevřený: ty proměnné jsem v programu **nedohledal k použití**,
takže o nich netvrdím nic. Kdyby se ukázalo, že jde o něco podstatného, patří to sem doplnit.

### 17.3 Obsahové volby u linky

Postskript čte tytéž `^JRL(...,"PATCH",...)` klíče jako Windows tisk (inventář `§13.2 C`), plus
vlastní: `PSVMargin`, `PSKMargin`, `PSVFont`, `PSVSIZA`, `PSKSIZA`, `SCALE`. Ty jsou právě těch
devět čísel výše a **u nás se všechny přesunuly do šablony**.

⚠️ **Font u linky jde přebít jen u vývěsky, ne u knihy** (`LinkyPSTisk.cls:88` - `s:vk="V"`).

### 17.4 Co postskript umí a my ne

Seřazeno podle toho, jak moc je to vidět:

| Co | Kde v legacy | Stav |
|---|---|---|
| Negativní značky **visí pod číslem spoje** a využívají prázdné řádky nad prvním odjezdem, takže záhlaví neroste | `JRapiLinPrtPS1.mac:128-160` | ❌ máme pevný pás nahoře - stojí to výšku |
| Odjezdová a příjezdová tabule zastávky (relace) | `RelacePSTisk.cls` | ❌ celá agenda chybí |
| Seznam zastávek a označení zastávek jako samostatný tisk | `^JRSET("PS","SEZNAM"/"ZaOzn")` | ❌ |
| Značky `od` / `pf` u výchozí a konečné zastávky | `JRapiLinPrtPS.mac` | ❌ |
| „Jede po jiné trase" jako svislá vlnovka | `LomiK = "±"` | ✅ 10. 9. (`B126`) - glyf 75, „projíždí" glyf 74 |
| Skládání tištěného čísla linky (3místné / vlastní / tablo) | `PATCH,cisIDS` | ❌ |
| Ruční zlom stránky na tarifním čísle | `PATCH,ZLOM` | ❌ vědomě (`B127`, 3 linky z ~93 tis.) |
| Podtržení u zdvojených řádků příjezd/odjezd | `PATCH,podtrzeniPaJ` | ❌ (viz `B123`) |
| Volby dělené linky (na jednu stranu, hlavička pro směr) | `gSetLinPrt` dta 21-23 | ❌ |
| **Spoje se na části dělí rovnoměrně**, ne do plna | `JRapiLinPrtPS.mac:492` (`pokrac`) | ✅ 14. 9. (`B152`) - rovným dílem, uvnitř skupiny dnů; windowsový tisk přitom plní do plna |
| **Řez po zastávkách se nesmí trefit na řádek s daty** | `JRapiLinPrtPS8.mac:79` (`rez`, `free`) | ⚠️ my řežeme na kterékoli hranici řádku |
| Počet řádků tabulky na stránku zadávaný u linky | `PATCH,VERTIM` | ❌ u nás vychází z výšky sekce a papíru |

#### Jak postskript stránkuje (dohledáno 7. 9.)

**Řeže obojím směrem** a rozhodnutí o vejití je přitom **celé o šířce**
(`JRapiLinPrtPS2.mac:194-219`): počítá se požadovaná šířka spojů obou směrů plus km sloupců proti
kapacitě a zvlášť počet spojů v každém směru. Výška v té podmínce není - řeší se až při sazbě.

- **Po spojích** (`pokrac`): šířka spojů směru / kapacita stránky = počet částí, a spoje se pak
  rozdělí **rovnoměrně** (`slpage = dim/pages`).
- **Po zastávkách** (`rez`): na část nejvýš `VertiM` řádků, a řez se **posouvá zpátky**, dokud
  netrefí řádek, kde nejsou data (`free()` povolí řez jen tam, kde jsou tečky, `>`, `|` nebo čas).
- **Sloučená obousměrná tabulka se zkouší jen u linky, která není jednosměrná** - u jednosměrné
  podmínka navíc žádá spoje v obou směrech, takže se sloučení nepovolí nikdy.
  Když sloučení neprojde: tisk po směrech, a když se ani jeden směr nevejde do šířky, tisk po
  směrech s řezem (`TyptI` 3 → 4 → 5). To je ta tichá degradace, kterou vědomě nemáme
  (rozhodnutí Jan 6. 9.).
- Když má linka zakázané prázdné sloupce, postskript je při těsném vejití **odebere**
  (`uprFreeTZ`) - jejich volba „vkládat prázdné sloupce" tam slouží jako záchrana.

### 17.4b Dělená linka - co to je, jak často a na čem to vyzkoušet (změřeno 10. 9. 2026)

Podklad k `B127`. **Dělení doprava:** když se sloupce spojů nevejdou do šířky, starý tisk rozdělí
linku na části a **každá část dostane celý seznam zastávek**. Svislé dělení podle zastávek je jiná
věc (`B135`). Rozhodnutí o vejití je u postskriptu celé o šířce, viz 17.4.

Tři volby u linky (`gSetLinPrt` dta 21-23, uložené v `^JRL(sku,lin,"PATCH",…)`), změřené přes
šest firem - **93 135 linek**:

| Volba | Klíč | Hodnota | Linek | Nejvíc spojů (na téhle se to projeví) |
|---|---|---|---|---|
| Tisk na jednu stranu | `ONEPAG` | zapnuto | **24 191** | `VS_CSAD / 945005` (155 spojů) |
| Hlavička a poznámky | `Hlava&pozN` | 1 = název a poznámky **pro směr** | **27 600** | `VS_CSAD / 470390` (183) |
| | | 2 = název a poznámky **pro část** | **6 125** | `VS_CSAD / 862783` (138) |
| | | 3 = jen poznámky pro směr | 17 | `CB_GWBUS / 320145` (87) |
| | | 4 = jen poznámky pro část | 2 | `ZL_KOVED / 947621` (14) |
| Směr na stranu | `Smer_1_stranA` | zapnuto | **17 951** | `VS_CSAD / 470390` (183) |
| Ruční zlom na tč | `ZLOM` | zadané tč | **3** | `CB_CSADAD / 390210` (tč 43) |

Pro porovnání: linka **bez** kterékoli volby s nejvíc spoji je `SKNI_MHDNITRA / 0000024`
(270 spojů). Měření se opakuje `LinkovyJrNastaveniProbe.DelenaLinkaVyskyt` a `…Priklady`.

**Co z čísel plyne:**

- ⚠️ **Není to okrajové nastavení jako v `B98`** - třetina linek. Předpoklad, že to dopadne jako
  ruční zlom (3 linky), neplatil.
- **Hodnoty 3 a 4 u `Hlava&pozN` prakticky neexistují** (17 a 2 linky). Reálné jsou jen „pro směr"
  a „pro část".
- ⚠️ **`VS_CSAD` není v přepínači firem** návrháře, takže na těch nejlepších příkladech jde
  porovnat jen starý tisk. Z firem, které máme, je nejlepší `CB_GWBUS / 320145`.

**Co ověřit na výtisku, než se něco napíše** - u každé volby jde o to, jestli u nás totéž
nedělá už něco jiného:

| Volba | Podezření, čím to u nás je | Co na výtisku porovnat |
|---|---|---|
| Směr na stranu | **vypnutý obousměrný tisk** (parametr sestavy) | vyjde stará sestava se `Smer_1_stranA` stejně jako naše s vypnutým obousměrným tiskem? |
| Hlavička a poznámky pro směr / pro část | **sekce** - „Každá stránka" vs „Jednou", plus `B134` | opakuje stará sestava záhlaví a legendu u každé části, nebo jen jednou za směr? |
| Tisk na jednu stranu | **nemáme nic** - my dělíme podle šířky prvku vždycky | co stará sestava udělá, když se to na jednu stranu nevejde - zmenší, ořízne, nebo dělí i tak? |

Teprve podle toho se pozná, jestli je `B127` jedna chybějící volba, nebo tři.

**Rozhodnutý tvar voleb: enum s „Automaticky (dle linky)"** (Jan 14. 9.). Co z těchhle voleb
zůstane, se udělá jako parametr sestavy ve tvaru **Automaticky (dle linky) / Ano / Ne**, stejně
jako `ROZDELENI_SLOUPCU` - tedy se **čte nastavení u linky** a šablona ho smí přebít.

Námitka, že sazba patří do návrháře a ne k lince (pravidlo výš v této kapitole), tím **neplatí
absolutně**: dokud to má nastavené třetina linek, přestat to číst znamená změnit výstup 33 744
linkám naráz. A hlavně to není slepá ulička - **až se dohodneme, že se to z nastavení u linky
vyhodí, stačí z nabídky odebrat položku „Automaticky" a zůstane čistá volba v šabloně.** Druhá
cesta k témuž vadí jen tehdy, když se jí nedá zbavit; tahle má východ.

⚠️ Důsledek na wire: „Automaticky" znamená, že agenda musí hodnotu od linky **posílat v datech**,
jinak klient neví, na co se automatika vyhodnotí. Aditivní přírůstek kontraktu (R3), ne změna.
⚠️ Popisky voleb budou po vydání **zamčené** (persistuje se zobrazovaný text) - pojmenovat napoprvé.

#### `Hlava&pozN` - projito na výtiscích 14. 9. (oracle)

Tři výtisky ze starého **postskriptového knižního** tisku, `CB_GWBUS`:

| Hodnota | Linka | Co je na papíře |
|---|---|---|
| 1 = název a poznámky pro směr | `990 / 0320125` | 6 stran: plná hlavička na začátku **každého směru** (1, 4), legenda na **konci každého směru** (3, 6), mezistrany jen „pokračování" |
| 2 = název a poznámky pro část | `251 / 0340100` | 4 strany: **každá strana samostatná sestava** - hlavička, tabulka, legenda; žádné „pokračování" |
| 3 = jen poznámky pro směr | `2 / 0320145` | 4 strany: hlavička **jen jednou** na první straně, legenda za každý směr |

⚠️ **Hlavní zjištění: legenda se mezi částmi liší OBSAHEM, ne jen umístěním.** Na `0340100` má
strana 1 v legendě jen „jede v pracovních dnech", strana 2 navíc „jede v sobotu" a „jede v neděli
a ve státem uznané svátky" - přitom nesou tytéž zastávky a liší se jen sloupci spojů. Totéž
u `0320145`: legenda směru Tam nese návaznosti spojů 9, 67, 71, legenda směru Zpět ne.
**Legenda vypisuje jen poznámky spojů, které jsou na dané části.**

**Rozdělení zodpovědnosti (rozhodnutí Jan 14. 9., varianta „umístění = sekce"):**

- **Umístění hlavičky i legendy zůstává nastavením sekce** („Jednou" / „Každá stránka" / „Pod
  obsahem" z `B134`). Parametr sestavy do toho sahat nebude. Důvod: každá otázka typu „kde se to
  objeví a opakuje se to" je dnes sekce; kdyby ji parametr přebíjel, panel ukazuje jedno a tiskne
  se druhé - třída chyb `B24`. (Hodnoty parametrů přitom **jsou** součástí šablony,
  `Template.PrintParameters`; problém není kde bydlí, ale že by jednu věc řídily dva ovladače.)
- **Enum bude řídit OBSAH legendy** - návrh voleb „Automaticky (dle linky) / Za celou sestavu /
  Za směr / Za část". To je věc typu `ROZDELENI_SLOUPCU` (rozhoduje, co v prvku vznikne), ne
  umístění.
- ⚠️ **Vzdáváme se tím per-linkového umístění hlavičky.** Zákazník s pomíchanými hodnotami 1 a 2
  dostane pro všechny linky stejné umístění. Odhad (neověřený): nebude to vidět, protože „hlavička
  na každé straně" vypadá dobře v obou případech a rozdíl je hlavně v legendě.

**Co z toho plyne pro odhad:** u téhle volby **nejde o nastavení, ale o schopnost prvku poznámek**.
Dnes bere poznámky celé sestavy a o dělení tabulky na části neví. Enum je až přepínač nad tím.
Záhlaví tabulky (řádek čísel spojů) se řešit nemusí - to si tabulka na pokračovacích stranách
opakuje sama (`OpakovanaHlavickaMm`).

#### `Smer_1_stranA` a `ONEPAG` - projito 14. 9. (oracle)

| Linka | Nastavení | Výsledek |
|---|---|---|
| `CB_GWBUS 101 / 0340313` | `Smer_1_stranA` + `Hlava&pozN=1` | **2 strany** - každý směr právě jedna, všechny jeho části naskládané pod sebe; hlavička i legenda na obou |
| `CB_GWBUS 990 / 0320125` | jen `Hlava&pozN=1` (srovnávací) | 6 stran - týž obsah rozložený po částech |
| `CB_GWBUS 101 / 0320180` | `ONEPAG` | **1 strana** - oba směry pod sebou, hlavička i legenda jednou |

**Mechanismus je u obou týž a je dohledaný v samotném postskriptovém programu:** procedura
`newpage` obsahuje `more 0 eq {TISK} if`, kde `TISK` posouvá plochu (a vysouvá list). Značky
`HOWGH4`/`HOWGH5`, které obě volby vyvolávají, nastaví `more = 2` → `TISK` se přeskočí →
**plocha se neposune a obsah pokračuje pod tím**. Liší se jen tím, kde se to uplatní:
`Smer_1_stranA` uvnitř směru, `ONEPAG` i mezi směry.

⚠️ **`ONEPAG` se uplatní jen tehdy, když se každý směr vejde do šířky** (`TyptI=4`,
`JRapiLinPrtPS.mac:328`). Na `501 / 0000002` (79 spojů) ho postskript ignoroval - značky vyšly
`HOWGH1/HOWGH2`, tedy `more = 0`. Příklad se hledá `DelenaLinkaSeznam` od nejmenšího počtu spojů.

**Celá `B127` se tím scvrkla na dvě věci, ne tři volby:**

1. **Co je v legendě** - za sestavu / za směr / za část. To prvek poznámek neumí.
2. **Kde se smí zalomit stránka** - starý tisk umí říct „tady neřež" a obsah se naskládá pod sebe.
   My řežeme vždy podle výšky.

💡 **Hypotéza k ověření na naší straně:** druhá věc může být z velké části
`SectionOverflowBehavior.KeepTogether`, který už máme, plus pojem „nová strana na začátku směru".
Ověřit na `0340313` s vypnutým obousměrným tiskem - jestli vyjdou dvě strany, je ta volba hotová.

**Rozluštění (ověřeno 14. 9.): obě volby jsou o tom, KOLIK MÍSTA má jedna „strana", a to je u nás
velikost stránky v šabloně. Není co implementovat.**

Starý tisk má dvě úrovně - logickou stránku 208 × 150 mm a fyzický list, na který se vejdou dvě nad
sebou (`TISK` střídá horní a dolní obrázek). Naše postskriptová šablona má 210 × 148 mm, tedy přesně
tu **logickou** stránku. Obě volby říkají „neposouvej plochu", čímž obsah přeteče z logické stránky
na celý list.

| Případ | Starý tisk | My |
|---|---|---|
| `0340313` bez volby (srovnávací `0320125`) | 3 strany na směr | **3 strany na směr** (shoda) |
| `0340313` se `Smer_1_stranA` | 1 strana na směr, obsah 226-229 mm | 1 strana na směr **po zvýšení stránky na 279 mm** (ověřil Jan) |
| `0320180` s `ONEPAG` | 1 strana, oba směry | 2 strany (směr na stránku); týž fyzický výstup dá **uspořádání na list** |

⚠️ **Domněnka „naše tabulka je vyšší než jejich" se NEPOTVRDILA a je stažená** (14. 9.). Vznikla
z toho, že starý tisk měl na `0340313` obsah ve 226-229 mm, kdežto u nás se do 279 mm tabulka
vejde, ale legenda už ne. **Ta čísla ale nejsou srovnatelná s našimi milimetry:**

- **Převod `.ps` → PDF mění měřítko.** Program deklaruje využitelnou šířku 173 mm (`PLW 208`,
  `LM 15`, `RM 20`), v PDF měřím obsah široký 195,7 mm začínající na 13,5 mm. `SCALE,LINKY` je
  přitom `1:1`, takže to dělá Ghostscript při sazbě na Letter, ne nastavení linky.
- **„Stránka" v PDF je fyzický list, ne logická stránka.** Na jeden list se vejdou dvě (`TISK`
  střídá horní a dolní obrázek), takže ani počet řádků na stránku PDF neporovnává totéž - na
  `0320125` vyšlo 29, 51 a 12 řádků na stranu.

Kdo se k tomu vrátí, musí nejdřív určit měřítko převodu a mapování logických stránek na listy -
teprve pak má smysl cokoli měřit. **Závěry o `B127` výš na milimetrech nestojí** (počty listů
a posloupnosti značek měřítko neovlivní), takže platí dál.

- ✅ *Neřezat uvnitř směru* u téhle linky **umíme** - směr se na stránku vejde a nic se nedělí.
- ❌ *Oba směry na jeden list* neumíme: u nás jsou to dvě stránky sazby.
- 💡 Nemusí to být práce v enginu: **uspořádání na list** (2 stránky na jeden papír), které
  postskript nemá vůbec (`§17.5`), tímhle dokáže vyrobit týž fyzický výstup. Ověřit, jestli to
  zákazníkovi stačí, než se bude do enginu zavádět „nedělej zlom mezi směry".

#### Jak se to zjišťovalo - postskript jde číst bez tisknutí

`ALVA.JRMAIN.LinkyPSTisk.GetPSFiles` vrací **samotný program po řádcích** (`FILE_LINE~…`), ne cestu
k souboru. IN: `ID~<sku>|l|<linka>`, `TYP~1` vývěska / `TYP~2` kniha, `STANOVISTE~0`. Z výstupu jde
vytáhnout posloupnost značek `HOWGH*` i definice procedur, takže se **struktura zjistí voláním**
a výtisk je potřeba jen na otázky o vzhledu (nápad Jan 14. 9.).

⚠️ **Oprava dřívějšího tvrzení:** knižní tisk sází **dvě logické stránky na jeden list** (`TISK`
střídá „horní obrázek" a „dolní obrázek"), takže `PLH 150 mm` je výška poloviny, ne listu. Dřív tu
stálo, že se sestava nevejde do stránky, kterou si deklaruje - to byl omyl z toho, že se měřil
obsah celého listu proti `PLH`.

### 17.5 Co umíme my a postskript ne

Není to jednosměrný seznam - stojí za to to na školení říct:

- **Uspořádání na list** (2×A5 na A4, ořezové značky, táž vývěska N× k rozstříhání) - postskript
  to nemá vůbec, dnes to za zákazníka řeší tiskař.
- **Členění výstupu na soubory** s názvem z datových polí.
- **Hlášení o přetečení** ještě před tiskem.
- **Řez tabulky podle skutečné šířky prvku**, ne do pevné stránky - na `330511` se nám celá linka
  vejde do jednoho bloku tam, kde postskript potřebuje dva.

### 17.6 Font

Program sází **Helvetica** (+ Bold, + Bold-Italic), přeencodovanou na české diakritice
(`^JRSET("PS","NVYVESY")` uzly 92-97). Symboly (kladivo, vlak, autobus, trojúhelníky) **nejsou
font** - jsou to vektorové procedury Type 3 psané přímo v programu (`/hammer`, `/loco`, `/bus`,
`/whitetri`, `/blacktri`).

⚠️ **Pravou Helveticu nemá ani Ghostscript**, který jejich `.ps` převádí na PDF - nahrazuje ji
**Nimbus Sans**. Výtisk, který zákazník považuje za postskriptový vzhled, je tedy s velkou
pravděpodobností Nimbus Sans, ne Helvetica. Ověřitelné ve vlastnostech jejich PDF (záložka Písma).
Arial má s Helveticou shodné šířky (naměřeno na desetinu), ale jinou kresbu.

### Šířka sloupce spoje - co kolik stojí (změřeno 7. 9. na `330511`, A5 na šířku, oba směry)

Prvek 200 mm, 9 spojových slotů (obousměrně = 18 sloupců), popisné sloupce 95,6 mm z toho
**39,6 mm samotné kilometry** (11 sloupců po 3,6 mm).

- Základ sloupce spoje **5,03 mm**: text „23 59" při 4,8 pt měří 4,03 mm, zbytek je vnitřní
  odsazení buňky (2× 0,5 mm). Odsazení je **absolutní**, takže při drobném písmu dělá čtvrtinu
  šířky sloupce - odtud dojem „kolem minut je spousta místa".
- Volba **„Stejně široké sloupce spojů" přidá 1,93 mm každému sloupci**, i tomu bez značky
  návaznosti: jednotná šířka se počítá za celou linku, a stačí jediná značka kdekoli. Na téhle
  lince to je 18 × 1,93 = **34,7 mm** a stojí to přesně jeden slot: zapnuté = 8 slotů ve dvou
  blocích, vypnuté = **všech 9 v jednom** (194,8 mm). Vypnuté je zároveň chování starého tisku -
  ten má pro sloupec čtyři šířky podle počtu značek (`JRapiLinPrtWin.mac:1793`).
- Užší kilometry (3,6 → 2,6 mm) dají jeden blok taky, samostatně: **193,2 mm**. Legacy jich navíc
  tiskne jen osm (`B124`).

⚠️ **Rozpadlý tisk, který Jan viděl, nezpůsobila šířka.** Dva bloky pod sebou zdvojnásobí výšku
tabulky a sekce byla nastavená „držet pohromadě", takže se druhý blok neměl kam odsunout
(přesah 43,7 mm). Nápravu dnes hláška o přetečení říká sama.


### 17.7 Průchod nastavením postskriptu s kolegou (9. 9. 2026)

Seznam 21 podnětů „co ve starém nastavení je a u nás chybí", projitý proti kódu. Účel je stejný jako
u `§13.2`: mít to jednou roztříděné, ať se u dalšího průchodu neodvozuje znovu.

**Šest podnětů bylo mylných - ta věc v aplikaci je.** Stojí to za samostatné vypsání, protože se
vedly jako mezera:

| Podnět | Kde to je |
|---|---|
| Nezobrazovat rok u „jede - nejede" | `LinkovyJrAd.UpravDatum`, režimy `2`/`3` = bez roku, s přebitím u linky |
| Místenkování jako poznámka | značky `R`,`R1`,`R2`,`R3` u spoje (`:1172`) + čtyři věty v legendě (`:1737`) |
| Radiobus | glyf `RBU` u spoje i vysvětlivka; prvek poznámek to má jako volbu |
| Jede na objednání / podmínečně | glyfy od 31. 8. (`L11`); v datech 22 spojů podmínečně |
| Zvýrazněná zastávka a tarifní číslo | parametry `ZVYRAZNIT_ZASTAVKU` a `ZVYRAZNIT_TC` |
| Nastavení názvu souboru | dialog tisku, vzor z datových polí |

⚠️ **Nastavené nikde není jen tvar tisku místenkování** (`MistOR`/`MistNOD`, 0 firem ze šesti) -
to je `B98`, ne mezera proti požadavku. Požadavek zněl „má to být jen nějaká poznámka", což děláme.

**Rozhodnutí z tohoto průchodu:**

- **Počet spojů ani počet řádků na stránku se jako nastavení nezavádí** (Jan 9. 9.) - dělení má být
  adaptivní. Ruší to i případné dorovnání legacy `PATCH,VERTIM`; celá váha jde na pravidla řezu
  (`B135`). ⚠️ Adaptivní dělení má smysl teprve nad `B130` - dnes o něm rozhoduje výška sekce
  a uživatel nevidí, kolik místa na stránce zbývá.
- **Prefix IDS u zóny je nová položka** (`B137`), ne dorovnání: legacy umí jen odebrání písmen
  (`ZonaIdsDelZnk`, 28 747 linek), tedy pravý opak. Písmeno v označení zóny je zároveň jediné, čím
  se dnes IDS pozná (KOVED `Z400`).
- **Do pozadí** (`B138`): černý tisk celé sestavy, volba „bez roku" v šabloně.
- **Souhrnný JŘ zůstává `L7`** a teď se nebere. Kontrakt víc linek v jedné sestavě umí už dnes
  (`§13.2 A`), engine se jen volá per linka.

**Podněty, které míří na už vedené položky:** poznámky u dělených linek → `B127` · měřítka → `B132` ·
km sloupce → `B124` · okraje a výška bloku poznámek → `B130` + `B133` · pořadí druhů poznámek →
rozhodnuto v `§13.4` bodu 3 (vlastnost prvku legendy), dnes prvek umí jen způsob řazení, ne vlastní
pořadí druhů, souvisí s `#7487` · bezbariérovost agregovat do poznámky → `PATCH,TZPA(BBP)`, vedeno
v `§13.5` jako data · málo zastávek a hodně spojů na jednu stránku → bloky pod sebou na jedné stránce
jdou už dnes, pokud sekce nedrží obsah pohromadě (měření 7. 9. na `330511`); ověřit na jejich lince.

**Zbývá doptat:**

1. **Návaznosti seskupit pod tarifní číslo, nebo samostatně** - dnes se přípojný řádek tiskne pod
   zastávkou, ke které patří. Není jasné, jestli jde o všechny návaznosti zastávky na jednom řádku,
   nebo o blok mimo tabulku.
2. **Km sloupce „jen z aktuální tabulky"** - jestli se po řezu na stránky mají tisknout jen sloupce
   použité na dané části. To je nad `B124` (duplicitní sloupce v obousměrné sestavě) něco navíc.

### 17.8 Pravidla svislého řezu - zadání `B135` (dohodnuto 14. 9. 2026)

Vzniklo průchodem staré sestavy `CB_CSADAD 1 / 0380760` proti našemu výstupu téže linky.

**Jak to dělá staré.** `VertiM` je pevný strop řádků na část (výchozí 55, `JRapiLinPrtPS.mac:53`,
u linky `PATCH,VERTIM`). Řízne se po něm **bez ohledu na zbylé místo** - na `0380760` vyšlo 55 + 9,
přestože stránka místo měla. Pak řez couvá dvěma pravidly (`JRapiLinPrtPS8.mac:83-85`):

- `rez1` couvá **po dvou**, dokud nenarazí na řádek s kladným tarifním číslem. Pole `PiloT`
  (`JRapiLinPrtPS5.mac:19-27`) drží rozdvojenou zastávku jako **trojici** řádků: prázdný, zastávka
  se **záporným** tč, prázdný. Staré pravidlo tedy nezní „nerozpůl rozdvojenou zastávku", ale
  **„řež jedině hned za obyčejnou jednořádkovou zastávkou"**.
- `rez2` couvá po jednom, dokud `free()` nepovolí - a ta povolí jen řádek, kde **žádný spoj nemá
  čas** (jen tečky, „projíždí", „jede jinudy").

**Co z toho bereme.** Jen to první. `free()` vědomě ne (rozhodnutí Jan 14. 9.): jejich couvání má
dno ve `VertiM`, naše by nemělo, a na husté lince takový řádek nemusí existovat. Vrátit se k tomu
jde, až bude porovnání proti staré sestavě říkat, že to vadí.

**Dohodnutá pravidla:**

1. Řeže se, **jen když se to nevejde**. Žádný počet řádků ani spojů jako nastavení (Jan 9. 9.).
2. Řez smí padnout **jen na hranici mezi zastávkami**. Rozdvojená zastávka (příjezd + odjezd) je
   z hlediska řezu jeden celek a odchází na další stránku celá.
3. **Zastávka a její přípojné řádky drží pohromadě** - na `0380760` visí pod Vodňany,aut.nádr.
   řádek s návazným spojem a dnes se může odtrhnout stejně snadno jako půlka rozdvojené zastávky.
4. **Tři stupně priority místa řezu:** hranice mezi bloky sloupců (dnešní `Zlom.Silny`) → hranice
   za obyčejnou zastávkou → nouzově kterákoli hranice řádku. Nouzový stupeň musí zůstat, aby
   engine neuvázl (dvojice vyšší než stránka).
5. Platí **jen pro linkovou tabulku**. Zastávkový JŘ se takhle nestránkuje a rozdvojené zastávky nemá.

**Proč to spěchá proti `B140`.** V dnešním (windowsovém) tvaru se název zastávky opakuje na obou
řádcích, takže rozpůlená zastávka je ošklivá, ale čitelná - proto si nikdo nestěžoval.
V postskriptovém tvaru jsou km, tarifní číslo i název **sloučené přes oba řádky a napsané jednou**,
takže by druhá stránka dostala řádek bez názvu i bez tč. `B135` musí být hotové dřív než `B140`,
nebo aspoň s ním.

**Jak se to doloží.** Oracle proti staré sestavě na `0380760` a aspoň jedné další lince: porovnává
se **místo řezu**, ne počet stran (ten se lišit bude a je to v pořádku, viz pravidlo 1).

**Co to znamená v kódu.** `LineTableElement.VyskyRadkuBloku` (`:1423-1456`) dnes z řádků vyrobí holý
seznam výšek a význam řádku se ztratí - musí ho začít nést. `Zlom` (`INedelitelneDily.cs:19`)
potřebuje třetí stupeň místo `bool Silny` a `PageLayoutEngine.PosledniZlomDo` (`:164-174`) musí umět
počítat zastávky, ne jen milimetry.

---

## 18. Linkový JŘ MHD proti AD a proti starému modulu - rozbor `B191a` (12. 10. 2026)

Cesty bez prefixu jsou `CACHE/ALVA/TemplateDesigner/Agendy/LinkovyJrMhd.cls`, „legacy" je
`C:\DEV\EDISON\MHD\CACHE\ALVA\MHD\LinkyPrintFormatJR.cls`. Měřeno na dev serveru 12. 10.

### 18.1 Čím porovnávat

- **Starý tisk je `ALVA.MHD.LinkyPrintFormatJR.MakeData(idfa,nmsp,LinkaID,outglb,.in,.out)`**
  (legacy `:6`), volá ho `ALVA.TISKJR.Data` ve větvi `VolanoZ="MHD"` (`TISKJR/.../Data.cls:216`).
  Na dev serveru je a shoduje se s diskem. Vrací 1 = v pořádku.
- `WinTiskMhd^JRapi`, který do 12. 10. uváděla roadmapa, je jiná cesta: tisk městských linek
  vedených v modulu JŘ (AD data). Pro `LinkovyJrMhd` se nehodí.
- **Pasti pro oracle:**
  - `in("A",2,"arr",11)` musí vyjmenovat všechny typy provozu a oba směry jako zapnuté
    (`TypProvozuID#id~1`, `SmerID#0~1`, `SmerID#1~1`). Bez toho starý modul nevytiskne nic (`:534`, `:538`).
  - Tabulku dělí na **bloky po typu provozu**, stejně jako AD po skupinách dnů. Každý blok nese
    celý seznam zastávek trasy ve směru (`:723-834`, přidání zastávky leží uvnitř smyčky přes
    všechny body trasy), seznam je tedy ve všech blocích týž. Typ provozu je u MHD to, co je u AD
    skupina dnů. Porovnávat se musí po dvojicích směr × typ provozu.
  - Spoje v bloku řadí bublinkově podle časů na jednotlivých zastávkách (`:951-990`), my podle
    výchozího času. Na lince, kde spoje začínají na různých zastávkách, se pořadí může lišit.
  - Plocha a měřicí řetězce jako u AD oracle (`LinkovyJrLegacyOracle.SpustLegacy`).
- Hlavička starého modulu nese jako číslo linky **veřejné číslo** (`CisloVerejne`, legacy `:1084`),
  což potvrzuje zadání `B191e`.

### 18.2 Co agenda posílá a co chybí

**Tři chyby v datech** (oracle by na nich spadl hned, patří do `B191b`):

1. **Číslo spoje je interní ID.** `CISLO_SPOJE` = `SpojID` (`:487`), starý modul tiskne pole
   `CisloSpoje` (legacy `:536`, `:1167`). CB_DP 11350: posíláme 2036117.
2. **Poznámky a atributy spoje se nepřečtou nikdy.** Pomocník `GetPoznamkySpoj` vrací
   `out("arr",1,PoznamkaSpojID,pole)`, agenda ale prochází o úroveň hlouběji a za ID poznámky bere
   jméno pole (`:500-522`, atributy `:527-543`). Zastávkový JŘ MHD to čte správně
   (`ZastavkoveJrMhd.cls:774-775`). Následek: řádek platnosti je prázdný, v legendě chybí druh 4 a 5.
   CB_DP 11350 má 10 spojů s poznámkou pro knižní tisk, výstup žádnou.
3. **Negativní značky jen z pole spoje.** Agenda čte `NegativniZnacka` (`:600`). Starý modul
   bez auto-poznámkování bere NZ z poznámky spoje typu 2 s `TiskJRMHDKnizni` (legacy `:625-645`);
   s ním spojům s `RucniZadani` NZ **přidělí** z rozsahu `RozsahNegativnichZnacekOd/Do` a poznámky
   typu 1 a 2 zahodí (legacy `:540-624`). CB_DP 11350: 10 ručně zadaných spojů, NZ máme u jednoho.

**Mezery proti starému modulu:**

- Starý modul tiskne jen poznámky s příznakem `TiskJRMHDKnizni` a u `TiskJRMHDKnizniPouzeZnacku`
  bez textu v legendě (legacy `:427-432`, `:636`). Agenda filtr nemá: po opravě bodu 2 by tiskla
  i poznámky určené jen pro zastávkový JŘ (např. KV_DP poznámka 1002 „nejede do zastávky Globus").
- Značky zastávky (atributy N/V/x/A a poznámky z trasy `PoznamkyTam/Zpet`) starý modul připíše
  za název zastávky (legacy `:403-440`, `:812-821`). My atributy dáváme jen do legendy (`:668-687`)
  a poznámky z trasy nečteme.
- Zóna: starý modul bere poslední zónu v IDS linky, my první (`:331-340` × legacy `:353-358`).
  Liší se jen u zastávky s víc zónami téhož IDS.

**Proti AD** (rozsah `B191b`):

- Zastávky: MHD posílá `PORADI, TYP, CZST, NAZEV_ZASTAVKY, ZONY` a prázdné `ATRIBUTY, KM`.
  AD navíc `AKTUALNI_ZASTAVKA, TC, MHD, NASTUPISTE`. `CZST` nese interní `ZastavkaID`, ne číslo
  zastávky (`:457`); číslo agenda má (`body` pozice 6) a nepošle. Schéma `CZST` nevyhlašuje (`:76-79`).
- Spoje: AD navíc `SMER, ZNACKY, DOPRAVCI, FORE_COLOR`. Barvu spoje ani turnusy starý MHD modul
  netiskne.
- Hodnoty: MHD nemá `POZNAMKA_POD_DOPRAVCE`, `AKTUALNI_ZASTAVKA_CZST/TC`, `VYLEP_ZASTAVKY`.
  Typ linky (legacy `out("dta",3)`) neposílá žádná agenda.
- Parametry: MHD žádné (`:131-135`), AD jich má 12. Starý MHD modul čte `arr` 4, 11 a 12 (§13.2 A).

### 18.3 Testovací linky (sonda `Tests/LinkovyJrMhdProbe`)

Data modulu MHD na dev serveru mají **CB_DP** a **KV_DP**. CK_SCHWAN, TA_COMET a VS_CSAD mají po
1-2 linkách s jedním či dvěma spoji. Linky CB_DP 11xxx jsou kopie plánu s platností do 2026/2027,
starší 1001-1024 platily v roce 2018.

| Linka | Co na ní je |
|---|---|
| CB_DP 11350 (linka 18, 122 spojů) | NZ z poznámky i ruční zadání, IDS, auto-poznámkování - hlavní případ NZ |
| CB_DP 11341 (linka 1, 318 spojů) | 51 ručně zadaných spojů - přidělování NZ z rozsahu |
| CB_DP 11354 (linka 23, 411 spojů) | 205 spojů s poznámkou pro knižní tisk |
| CB_DP 11018 (linka 13, 228 spojů) | 9 typů provozu - bloky po typu provozu |
| CB_DP 11338 (linka 33, 24 spojů) | malá linka s poznámkami, rychlý test |
| KV_DP 1073 (205 spojů) | 7 typů provozu, „jede jinudy"; bez NZ, poznámek a IDS |

**Na dev datech chybí** příjezd a odjezd (atribut trasy 1011), atribut spoje B/C a poznámka z trasy
pro knižní tisk. Tyto cesty oracle neověří a v dokladech parity se píšou jako neověřené (R6).

### 18.4 Co z toho plyne

- `B191b` je větší, než stálo v roadmapě: kromě polí zastávek tři chyby z §18.2.
- `B191c`: oracle po dvojicích směr × typ provozu, `arr,11` vše zapnuto.
- Bloky po typu provozu nejsou nová věc: prvek je kreslí jako skupiny dnů u AD.

### 18.5 Skupiny spojů jednotně ve všech agendách - návrh pro `B191d` (12. 10. 2026)

**Cíl (Jan 12. 10.):** minimum je pokrýt, co uživatelé mohli dělat ve starém modulu. Cílový stav je,
aby se se skupinami spojů (časové platnosti u AD, typy provozu u MHD) pracovalo stejně v zastávkovém
i linkovém JŘ a v obou datových zdrojích.

**Dnešní stav:**

| Agenda | Z čeho jsou skupiny | Co volí uživatel |
|---|---|---|
| Zastávkový AD | dny v týdnu z kalendáře spoje | „Rozdělení sloupců (typy provozu)": Automaticky (dle linky), 4 vzory, Vlastní rozložení |
| Linkový AD | dny v týdnu z kalendáře spoje | „Dělení spojů dle platnosti": 3 vzory, Vlastní rozložení (bez „Automaticky") |
| Zastávkový MHD | typy provozu, sloučené do nejvýš 4 sloupců podle pole SMO v číselníku (`ZastavkoveJrMhd.cls:612-617`) | nic |
| Linkový MHD | typ provozu = blok, pořadí z číselníku (`LinkovyJrMhd.cls:380-428`) | nic |

Vlastní rozložení u AD je jeden mechanismus pro obě agendy: prvek drží definici sloupců
(`DepartureColumnDefinition`: dny, značky, období, nadpis), klient ji posílá jako `DEFINICE_SLOUPCU`
(`TemplateDesignerViewModel.ComposeGetDataParams`) a parametr dělení ji jen zrcadlí. Spoj, který
nesedne do žádného sloupce, se netiskne - tím jde zároveň skupinu vynechat. MHD agendy
`DEFINICE_SLOUPCU` nečtou.

Starý linkový MHD před tiskem nabízel zaškrtávátka typů provozu a směrů
(`LinkyPrintBIGFilterED.cls:111-123`, všechny typy firmy, ne jen linky).

**Návrh:** jeden model skupin, jiné kritérium podle datového zdroje.

1. `DepartureColumnDefinition` dostane další kritérium **typy provozu** (seznam ID). U AD zůstává
   prázdné, u MHD nahradí dny. Aditivní změna šablony i wire.
2. Obě MHD agendy začnou číst `DEFINICE_SLOUPCU` a posílat vzory a seznam typů provozu linky.
   Výchozí rozložení odpovídá starému tisku: linkový blok na typ provozu, zastávkový sloučení podle SMO.
3. Editor sloupců a parametr dělení jsou tytéž jako u AD; u MHD editor místo dnů ukáže typy provozu.
   Vynechání typu provozu (minimum ze starého modulu) = odebrat ho z rozložení.
4. Směr tisku je samostatná volba linkového JŘ („Oba / Tam / Zpět"), pro AD i MHD stejná.

**Pořadí:** linkový MHD v `B191d` (body 1-4), zastávkový MHD jako samostatný krok po něm. Sjednocení
názvu parametru dělení mezi zastávkovým a linkovým AD jde udělat zvlášť - popisky voleb jsou
zamčené (persistuje se text), název parametru ne.

**Neověřeno:** jak editor sloupců dnes skládá dialog podle agendy, a tedy kolik stojí bod 3.

### 18.6 Stav po `B191b` (12. 10. 2026)

Tři chyby z §18.2 jsou opravené a značky spojů porovnává se starým tiskem `LinkovyJrMhdTest`
(oracle `Tests/LinkovyJrMhdLegacyOracle`). Při opravě vyšly najevo další dvě věci:

- **Text omezení u negativní značky byl vždy prázdný** - agenda volala funkce kalendáře s jinými
  argumenty než starý modul (a bez kroku `mkOmzFromOnlyTwoBitmaps`) a chybu polykal catch.
  Zastávkový JŘ MHD má týž výpočet bez toho kroku (`ZastavkoveJrMhd.cls:556-599`); porovnání
  `MhdLegacyOracle` na CB_DP 11350 a 11018 ukazovalo chybějící značky v poznámkách. Opraveno týž den.
- **Značky zastávky bere starý tisk i z atributů bodu trasy** (`AtributyTam/Zpet`, legacy :442-483),
  ne jen z číselníku zastávek. Na KV_DP 1073 je tak „na znamení".

Atributy spoje kromě B/C starý tisk posílá jako id znaku, které klient převádí - u nás se netisknou
(v datech dev serveru nejsou, neověřeno).

### 18.7 Stav po `B191c` (12. 10. 2026)

Oracle porovnává celou tabulku a na 27 linkách CB_DP a 38 KV_DP je shoda. Co zbývá neověřené,
protože to dev data nemají: příjezd/odjezd na jedné zastávce (atribut trasy 1011, sloupec „od/př"
starého tisku nahrazený podřádky) a poznámky z trasy (`PoznamkyTam/Zpet` pro knižní tisk), které
starý tisk připisuje k názvu zastávky - ty agenda zatím nečte vůbec. Linky KV_DP 1105 a 1106 starý
tisk nevytiskne (chyba starého modulu); naše agenda pro ně vrátí prázdný výstup bez hlášky.

## 19. Rozdílový linkový JŘ podle vzoru z CIS JŘ - rozbor `B192` (16. 10. 2026)

**Rozhodnutí Jan 16. 10.:** rozdílový JŘ je pevný výstup, ne agenda pro návrháře. Uživatel v hlavním
okně vybere dvě verze linky a dostane PDF v tomhle tvaru. Šablona i logika porovnání jsou naše,
agenda zůstává skrytá (`Released = 0`) a návrhář slouží jen k testování. Porovnání se přidává po
krocích (roadmapa `B192a-f`) a každý krok se ověří na skutečných linkách (§19.4).

### 19.1 Vzor

Dva výstupy CIS JŘ (CHAPS) od zákazníka z 16. 10., v repu nejsou: 950904 Jeseník-Rýmařov-Olomouc-Přerov
(ARRIVA, oba směry, 4 strany) a 780400 Prostějov-Lutín-Olomouc (FTL, 3 strany). Obě linky mají
změnu trasy, takže je agenda ve stavu z 9. 10. neporovná a vytiskne jen novou verzi s hláškou.

### 19.2 Pravidla vzoru

Červená = jen v původní verzi, zelená = jen v nové, přeškrtnutí = text, který zmizel. Odvozeno ze
sedmi stran, ne z dokumentace CHAPS.

- **Hlavička:** původní platnost přeškrtnutá nad novou. Dopravci po řádcích s čísly spojů, zmizelý
  přeškrtnutý, nový zeleně.
- **Zastávky:** vlevo dva páry sloupců, původní a nové pásmo a původní a nové Tč. Odlišné Tč je
  červené a zelené; po vložené zastávce se posunou všechna další (ARRIVA).
- **Přidaná zastávka** má celý řádek zelený (ARRIVA Ludvíkov,pošta), **zrušená** celý červený
  (FTL Olomouc,Nedvězí). Zastávka nahrazená jinou jsou dva řádky (FTL Nedvězí → Nedvězí,křiž.).
- **Spoj se změněným časem** jsou dva sloupce vedle sebe; číslo spoje i odlišné buňky mají barvu
  své verze. Podbarvené jsou i buňky „…" (spoj v úseku nejede), buňky ⟆ a | ne.
- **Spoj, který se liší jen trasou**, zůstává jednou: čas u zrušené zastávky stojí v červeném řádku,
  u nové v zeleném (FTL spoj 17).
- **Zrušený spoj** má celý sloupec červený včetně km (ARRIVA 527 a 528).
- **Změna jen ve značce** dvojici nedělá. Značka jen v nové verzi je v zeleném rámečku, jen
  v původní v tmavě červeném (FTL 23, 33, 35 přibyla 31; ARRIVA 1 ubyla 31; ARRIVA 8 změna 38 → 37).
- **Km spoje:** původní hodnota přeškrtnutá vedle nové (FTL spoj 17 „24 23").
- **Legenda:** číslované poznámky původní přeškrtnuté v tmavě červeném rámečku, nové zeleně; stejné
  číslo s jiným textem je tam dvakrát (ARRIVA 17, 18, 72). Stejně vysvětlivky piktogramů a textové
  poznámky.
- **Nejasné:** ARRIVA spoj 23 je ve dvojici, ačkoli se jeho časy podle všeho nemění; FTL spoj 23
  s podobnou změnou značky ve dvojici není.

### 19.3 Proti agendě ze 9. 10.

Spoj se změněným časem je dvakrát a odlišné buňky jsou podbarvené (`LinkovyJrAdRozdilovy.cls:263-283`,
`LineTableElement.cs:3186`). Páruje se podle směru, čísla a skupiny dnů (`:322`) a porovnávají se
jen časy (`:329`). Nový spoj je celý zelený, zrušené jsou jen text v poznámkách (`:286-317`). Když
se liší zastávky, porovnání se nedělá (`:255`). Z původní verze se bere jen platnost (`:302`).
Číslo spoje ani značky barvu verze nedostanou, verzi čtou jen buňky časů (`LineTableElement.cs:3187`).
Tabulka má jeden sloupec pásem a jeden Tč (`ColumnKeys.cs:20-21`).

⚠️ **Díra už ve stavu z 9. 10.:** legenda se skládá jen z nové verze (`:308-317`, text poznámky
`LinkovyJrAd.cls:1851`), ale původní spoj ve dvojici nese své značky. Značka, kterou nová verze
nemá, tak zůstane bez vysvětlení, a stejné číslo s jiným textem (ARRIVA 17) se vysvětlí textem
nové verze. Řeší `B192b`. Na skutečných datech je to vidět od `B192a`: zrušené spoje 380742/15
a 380715/37, 39, 46 (skupina 207 proti 2) nesou značku 93, kterou legenda nové verze nemá.

Řádek s km spoje pod tabulkou linkový JŘ nemá vůbec. To je samostatná věc, ne porovnání.

### 19.4 Testovací linky

Dvojice hledá sonda `Tests/RozdilovyJrProbe.NajdiDvojice` (vstup `NOVE` a `PUVODNI` = skupiny JŘ).
Vrací i dvojice se změnou trasy (od `B192d`, počet `zastavky`) a dopravce (`dopravci`). Pořadí spojů,
zastávky jen v jedné verzi a dopravce obou verzí jedné dvojice vypíše `PrehledDvojice` (`NOVA`,
`PUVODNI`, `LINKA`). Změny trasy jsou hlavně v objížďkových skupinách (306-321). Výtisk se skutečnými daty dělá
`ZkouskaTiskuNaostro` nad kopií šablony, která má v parametrech `SKUPINA_LINEK_ID` a `POROVNAT_SKUPINU`
(`TD_IDFA=CB_CSADAD`, `TD_NAMESPACE=CB_CSADAD`). Skupina 2 je verze z let 2022-2023, 207 od 14. 12. 2025.

| Dvojice (CB_CSADAD) | Co na ní je | Pro krok |
|---|---|---|
| 380715, skupina 207 proti 2 | 2 změněné, 3 zrušené spoje | `a` (výtisk 16. 10.) |
| 380738, skupina 207 proti 2 | 3 změněné, 1 nový, 1 zrušený | `a` (výtisk 16. 10.) |
| 380742, skupina 207 proti 2 | 2 nové, 1 zrušený, 5 jen se změnou značek | `a`, `b`, `c` (výtisk 16. 10.) |
| 380756, skupina 207 proti 2 | 16 jen se změnou značek | `c` (výtisk 16. 10.) |
| 380717, skupina 207 proti 2 | 8 změněných, 7 jen se změnou značek | `c` (výtisk 16. 10.) |
| 380742, skupina 207 bez porovnání | jen nová platnost | `f` platnost (výtisk 16. 10.) |
| 0380724, skupina 306 proti 2 | přidaná zastávka Kejnice,rozc. | `d`, `e` (výtisk 16. 10.) |
| 0380730, skupina 308 proti 2 | nahrazené Hlupín → Hlupín,rozc. a Blatná,Boženy Němcové → Blatná,Palackého | `d` (výtisk 16. 10.) |
| 0370617, skupina 309 proti 2 | nahrazený Písek,aut.nádr. → Písek,U autobusového nádraží | `d` (výtisk 16. 10.) |
| 0320167, skupina 141 proti 127 | dopravce GW BUS jen v původní verzi (v nové spoje dopravce nemají), 72 spojů | `f` dopravci |
| 0132400, skupina 141 proti 127 | dopravce Jan Kukla jen v nové verzi | `f` dopravci |
| 380760, skupina 307 proti 207 | změněný spoj 21 Tam | základ z 9. 10. |
| 380735, skupina 307 proti 207 | změněné spoje 14 a 18 Zpět | základ z 9. 10. |

### 19.5 Zjištěno na výtiscích po `B192a` (16. 10.)

- **Mezi dvojici se vkládal prázdný sloupec dorovnání** (380715 Zpět, spoj 6). Opraveno týž den:
  prázdný sloupec jde za dvojici (rozhodnutí Jan: porovnávané sloupce stojí u sebe, i za cenu výjimky
  z rozložení starého tisku).
- **Po `B192b` vysvětlovala legenda i značky, které v tabulce nebyly** (380742: 11 a 23 u spojů
  se stejnými časy). Vyřešeno `B192c`: původní značka je teď přímo ve sloupci.
- **Neřešeno: dvojici může rozdělit hranice bloku.** Když se spoje nevejdou na šířku a tabulka se
  láme do bloků pod sebou, může původní podoba skončit na konci bloku a nová začít další blok.
  Na výtiscích 16. 10. se to nestalo; ověřit na lince s víc změněnými spoji.

### 19.6 Po `B192f` (17. 10.) - co proti vzoru zbývá

Kroky `B192a-f` jsou hotové. Vzor (PDF od zákazníka, v repu nejsou) má navíc, nebo jinak:

- **Vědomě jinak (rozhodnutí Jan 19. 10.):** vzor vyznačuje původní platnost, dopravce
  a poznámky přeškrtnutím a nové barevným písmem, my všude podbarvením jako buňky tabulky.
  Jednotný rozdíl na celém výtisku má přednost před shodou se vzorem. Černobílý tisk tím rozdíl
  v hlavičce a legendě ztratí.

- **Dávka v patičce:** „Dávka: ~~ARRIVA_MORAVA/5454~~ ARRIVA_MORAVA/5982". Naše šablona dávku
  netiskne vůbec.
- **Km spoje pod tabulkou** s původní hodnotou přeškrtnutou (FTL spoj 17 „24 23"). Řádek km spojů
  linkový JŘ nemá vůbec (§19.3).
- **Pořadí dopravců:** vzor drží pořadí původní verze a nové přidává na konec, my tiskneme
  společné, pak původní, pak nové (rozhodnutí Jan 17. 10.). Když žádný dopravce není v obou
  verzích, stojí „Přepravu zajišťuje" na řádku samo a první dopravce až pod ním; vzor ho má
  na řádku s popiskem.
- **Dopravci za směr, ne za stránku:** sekce s nimi mají výšku podle obsahu a ta se počítá před
  stránkováním, takže text za stránku by mohl přetéct (u ostatních šablon `POPIS_DOPRAVCE` za
  stránku, #9417). U směru přes víc stránek se vypíšou spoje celého směru.
- **Světlé barvy podbarvení** (`#FFF08080`, `#FF92D36E`) jsem se vzorem srovnal jen pohledem,
  hodnoty ze vzoru nejsou změřené.
- **Data:** na dev serveru se dopravce mezi verzemi liší jen tím, že v jedné chybí úplně
  (0320167, 0132400). Dopravce se změněnými spoji (vzor ARRIVA) kryje jen test.
