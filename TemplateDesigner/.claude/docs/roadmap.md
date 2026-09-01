# Roadmap

> **Jen otevřená / akční práce.** Hotové → [`roadmap-archive.md`](roadmap-archive.md), průběžný
> changelog → [`changelog.md`](changelog.md), stav tiketů a věci „jen na poptávku" →
> [`customer-requirements §0`](customer-requirements-zastavkovy-jr.md).

> **Nejblíž na řadě:** § Aktuální focus níže. Rovnou jde vzít **`B101`**; u čtyř dalších je první
> krok dotaz ven (zákazník, kolegové z jízdních řádů), ne psaní kódu.

---

## 🎯 Aktuální focus

Vytaženo z Backlogu 31. 8. (rozhodnutí Jan): paritní vlny jsou hotové, a než se sáhne na velký
vnitřní úklid, jdou na řadu tyhle menší věci. ⚠️ **Čtyři z nich čekají na odpověď zvenčí, ne na
práci** - u nich je první krok se zeptat, ne psát kód:

- **`C8`** - zeptat se zákazníka, jestli to pořád chce.
- **`C9`** - potřebuje nové pole u linky od kolegů z jízdních řádů.
- **`B102`** - čeká na zákazníka s linkou, která má mnoho dopravců.
- **`B98`** - je to evidence toho, co nikdo nemá zapnuté; vytáhnout, až si o to někdo řekne.

**Rovnou udělat jde `B101`** (drobné zlepšení srozumitelnosti). **`B100`** taky, ale je to zásah do
serveru i klienta kvůli třem linkám z 93 tisíc, takže spíš až se bude na přenos nastavení linky
sahat z jiného důvodu. (`N1` odsud odešlo hotové 31. 8.)

- **`C8` Vejdi výstup na zadaný počet stran** (M, podnět POVED #8785 bod 5 - **podnět, ne závazek**).
  Starý modul to odmítl jako principiálně nemožné, náš engine má lepší pozici (`Strih` už umí režim
  „nejmíň stránek"). Než se do toho pustí, má se zákazníka zeptat, jestli to pořád chce - jinak
  stavíme featuru, kterou nikdo nezadal.

- **`C9` Ručně zadaný „název směru" u linky jako datové pole** (#9088 bod 3, chtěné i pro vývěsky).
  ⚠️ **Nejde udělat u nás** - vyžaduje **nové pole u linky v jízdních řádech**, tedy součinnost
  s kolegy z JŘ. Náš díl je malý (pole projít schématem a nabídnout v šabloně), ale bez toho pole
  nemá kam sáhnout.

- **`B102` Filtrování dopravců v hlavičce** (z důsledku zrušené vlny `L7`, 25. 8.). Sdružování linek
  není naše práce, ale zákazníkův případ z #9417 (souhrnný JŘ dvou linek s dlouhým výčtem dopravců)
  se k nám může dostat jako **jedna linka s mnoha dopravci** - tedy bez čekání na cokoli dalšího.
  Až se to stane, filtrování dopravců přestane být odložené.

- **`B101` Není poznat, jestli je jedna skupina dnů nastavením, nebo daty linky** (z 25. 8.).
  Prvek pás skupin nekreslí, když vyjde jediná skupina - a uživatel pak nemá jak poznat, jestli
  si dělení nastavil špatně, nebo jestli tak prostě vypadá linka (na 0330511 jedou všechny spoje
  jen v pracovní dny). Otázku „mám to vůbec nastavené dobře?" dnes **nejde zodpovědět z aplikace**.
  Drobné zlepšení, ne chyba.

- **`B100` Ruční zlom stránky na tarifním čísle** (`PATCH,ZLOM`, z `L5` 25. 8.).
  U linky jde v jízdních řádech nastavit tarifní číslo, na kterém se má tisk rozdělit; starý modul
  to respektuje, my ne, takže se takové linky tisknou jinak. **Odloženo kvůli frekvenci**
  (rozhodnutí Jan 25. 8.): nastaveno na **3 linkách z ~93 tisíc** (měření 21. 8.), což je za cenu
  zásahu do serveru i klienta slabý důvod. ⚠️ **Neblokuje nic dalšího** - klientská strana pro
  zlomy existuje (`INedelitelneDily`, silné a slabé zlomy) a vynucený zlom by k ní jen přibyl;
  na drátě by šlo o jedno pole navíc, tedy aditivní změna. Vytáhnout, až si o to někdo řekne,
  nebo až se bude stejně sahat na přenos nastavení linky.

- **`B98` Nastavení starého tisku, která nikdo nemá zapnuté** (evidence, ne práce; z měření 21. 8.,
  [`§13.6`](linkovy-knizni-jr-implementace-analyza.md)). Na 93 133 linkách šesti firem nejsou nikde
  nastavené: **tvar tisku místenkování**, **vlastní texty sloupce MHD** místo „IDP-P"/„IDP-Z",
  **tisk provozů dopravce**, čtyři přepínače **schovávání u linky** (`MHD2,netisknout*`) a **celá
  vrstva pod linkou** (řádek se v knižním JŘ netiskne, i zvlášť per směr; umělá sekce u spoje)
  a **tři přepínače řazení spojů** (`PS_PrintPorSpTR`, `PS_PrintSouhrnPorSpPri`,
  `PS_PrintSouhrnPorSpTR` - řadit jen podle odjezdu, resp. jen podle příjezdu u souhrnných linek;
  souhrnných linek je 33, ale bez těch přepínačů se chovají stejně jako ostatní).
  Řada z nich vznikla v legacy **pro jednoho zákazníka** a nikdo jiný je nepoužívá. Vytáhnout, až
  si o to někdo řekne - a pak počítat s tím, že **doklad z dat nebude** a půjde jen o implementaci
  proti čtení legacy zdroje. ⚠️ U skrývání řádků platí, že **ubírá řádky** - zákazník to pozná na
  první pohled, takže se k tomu má vyrobit vlastní testovací případ, ne to udělat naslepo.

- **`B103` Hledání ve výběru linek prohledá jen prvních 200** (nález Jan 1. 9.). Dialog si
  načte 200 linek skupiny (`EDList.Instance` posílá `limit = 200`) a hledací pole pak filtruje
  **jen je** - napsané číslo linky, která je až za dvoustovkou, nenajde nic a vypadá to, že linka
  v datech není. Přitom tam je: `380761` v CB_CSADAD skupině 1 se přes výběr vybrat nedá, přes
  server se najde. ⚠️ Netýká se to jen zkoušení - u firem s tisíci linkami je většina linek
  hledáním nedosažitelná. Oprava = poslat hledaný text na server (`GetAvailableInstances` ho už
  přijímá jako `search`), ne filtrovat načtenou stránku.

---

## 🚆 Linkové a knižní JŘ (náhrada TiskJR)

> ⚠️ **Gate: R1 (sdílené tabulkové jádro), R2 (layout zodpovědnosti), R5 (embedded fonty)** -
> [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md).

**Stav: paritní vlny jsou hotové a žádná otevřená tu není.** Z průchodu testovacími linkami nevzešel
žádný datový rozdíl proti starému tisku; co zůstalo neprojité, drží
[§ K ověření](#k-ověření-na-reálné-sestavě) níže. Sloučení dvou Caché tiskových enginů (dřív `L8`)
je v Backlogu u `B56` - je to úklid vnitřku bez přírůstku pro uživatele a nad rozdělaným kódem by
jen zabetonoval rozpracovaný tvar (rozhodnutí Jan 31. 8.).

⚠️ **Linková AD agenda je zveřejněná** (`Released = 1`, 24. 8.) - **ukáže se i zákazníkům, jakmile
se vydá nová verze**. Městská zůstává skrytá, dokud neprojde porovnáním se starým modulem.

**Když přijde na řadu MHD** (dnes se neřeší, rozhodnutí Jan 28. 8.), platí čtyři věci, které se z kódu
nepoznají:

- **Vlastní inventář a vlastní porovnání se starým modulem.** „Dodělám AD a MHD spadne samo" neplatí -
  AD je nadmnožina v obsahu tabulky, ne v pojmech (licenční × veřejné číslo linky, typ provozu jako
  datový pojem, vlastní vrstva nastavení).
- **Sada voleb pro městské linky je nová featura, ne dorovnání** - starý modul jich u MHD čte tři
  z jedenácti, takže parita tam nemá čím se řídit.
- **V šabloně je `{CISLO_LINKY}` licenční číslo**; veřejné je `{LOKALNI_OZNACENI_LINKY}`.
- **Zastávkové řádky se musí dorovnat jako první** (audit 31. 8.): MHD dnes posílá jen pořadí, typ,
  název a zóny, kdežto AD navíc `CZST, AKTUALNI_ZASTAVKA, TC, MHD, NASTUPISTE`
  (`LinkovyJrMhd.cls:73-81` × `LinkovyJrAd.cls:100-116`). Zvýraznění zastávky tak u MHD **nemá kam
  zapsat příznak**, i kdyby přibyl parametr - a bez toho půjde část voleb udělat jen naoko.

📄 **Zkušební vzor pro celý track: linka 940 Kroměříž-Přerov (IDZK)** - reálné zadání zákazníka,
které starý modul nedokázal vyrobit. Až bude šablona stát, ověřuj proti němu.

🧭 **Jak se nakládá s nastavením starého modulu:** *to, jak je výstup složený, patří do návrháře;
z nastavení u linky se bere jen to, co mění data.* Sporné případy řeš směrem do šablony. Rozhodnutí
položku po položce, jejich důvody a změřený výskyt v datech drží
[`§13.4`-`§13.6`](linkovy-knizni-jr-implementace-analyza.md), pozastavené tikety helpdesku
[`§14`](linkovy-knizni-jr-implementace-analyza.md).

### K ověření na reálné sestavě

Ne implementace - **projít a zavřít**. Vzniká to tím, že návrhář umí věc, kterou starý modul neuměl,
takže nejde o práci, ale o doklad.

| Co | Proč to čeká |
|---|---|
| **Vícesloupcová legenda (#7487)** - zákazník chtěl čtyři sloupce | Prvek umí 1-4 sloupce včetně zalamování (zjištěno 26. 8.); výchozí je 1, proto si toho nikdo nevšiml |
| **Poznámky vedle tabulky, ne pod ní (#11373, KAD)** | Legenda je samostatný prvek, takže to umíme dnes; ověřit a tiket zavřít |
| **Pořadí sloupců spojů na `CB_CSADAD 1 / 0370618`** | Starý tisk dává oba směry do jedné tabulky, takže není co k čemu přiložit. ⚠️ **Odblokováno 28. 8.** - obousměrný tisk je hotový, takže tuhle linku už je čím porovnat. Vzít při nejbližším průchodu doklady |
| **Písmena ze zóny (`VS_CSAD 5 / 0771901`)** | V testovacích datech pro Vsetín na to není vzorek; firma navíc není v přepínači firem. Vzít, až ta data budou |
| **Živý náhled (`GetMockData`) ignoruje obousměrný tisk, turnusy i vlastní definici sloupců** (`LinkovyJrAd.cls:460-469` volá `BuildStrankaLinky` napevno s `deleni=2`, bez `defSloupcu`/`obousmerne`/`sTurnusy`) | Podle kontraktu `BaseAgenda` je mock jen ukázková data bez parametrů, takže to nejspíš je v pořádku - ověřit, jestli uživatel v návrháři čeká, že se mu obousměrný přepínač promítne do náhledu, a je zklamaný, že ne (zjištěno auditem 31. 8., nejistá závažnost) |

---

## 📥 Backlog

### Ostatní

- **`B66` Vlastní bloky prvků („moje komponenta")** (M, podnět Jan 12. 8.). Uživatel si vybere pár
  prvků (typicky svou hlavičku sestavy), uloží je pod jménem a pak je vkládá jedním kliknutím do
  dalších šablon. **Co už existuje:** uložení **celé sekce** jako vlastní šablony a seskupení prvků
  (Ctrl+G). Chybí mezistupeň mezi nimi - pojmenovaný blok menší než sekce. **Proč to čeká na pevnou
  tabulku:** dokud je hlavička poskládaná z 6 samostatných textů, musí blok řešit relativní pozice a
  vazby uvnitř skupiny; jako **jeden** prvek (pevná tabulka) je uložení skoro zadarmo. Otevřené:
  kde bloky žijí (per uživatel × firma jako šablony sekcí, nebo sdílené v DB) a jestli se vkládají
  z palety, nebo z nabídky „Přidat". **Pohltí i připravené bloky agend** (dřív `B77`): server pro
  každou agendu drží hotové sestavy tabulek („Záhlaví linky", „Shrnutí faktury"), ale od zrušení
  starého dialogu k nim z UI nevede cesta - je to titíž „pojmenovaný blok k vložení", jen dodaný
  námi místo uživatelem, takže samostatnou cestu do UI nedostane (rozhodnutí Jan 17. 8.).

- **`B18` Deklarativní pravidla EDInputFormu** (M, odloženo - rozhodnutí Jan 5. 8.: patří k velkým
  věcem do budoucna, ne do běžné fronty; je to **kapabilita sdílené komponenty pro celý EDISON**,
  ne featura návrháře). Dnes umí formulář reagovat na vyplněné hodnoty jen oklikou přes server
  (`Validate`, ručně per formulář). Zavést deklarativní vrstvu: začít **viditelností** (`visibleWhen`),
  gramatiku sdílet s tiskovými parametry (`POLE=hodnota` + nerovnost) - nezavádět druhý dialekt.
  Povinnost/povolení až s poptávkou; **skutečná validace zůstává imperativní** (potřebuje data
  a dopočty). **Follow-up:** migrace `CellStyleEditor` na Caché-driven formulář (S–M; háček je
  tri-state / reaktivní logika - bez `B18` nejde). **Follow-up 2:** rozšířit gramatiku o čtení
  **parametru sestavy**, aby šlo zašednout pole prvku mrtvá bez parametru (6 polí šipky vs.
  `TISK_SIPKY`, projeté zastávky vs. `TISK_PROJETYCH_ZAST`) - dnes to plní jen helpTexty.
  **Follow-up 3:** parametr sestavy nemá pole pro **popis**, takže krkolomné labely
  (`SCHOVAT_V_ROZSAHU`, `ROZTAHNOUT_NA_VYSKU`) nejde vysvětlit. Aditivní klíč na wire + tooltip -
  dělat spolu s gramatikou, ne zvlášť kvůli dvěma tooltipům.

- **`B56` Sjednocení Caché tiskových enginů a jejich opsaných pomocných funkcí** (odloženo -
  **rozhodnutí Jan 5. 8. a 31. 8.**: nedělat předčasnou deduplikaci, dokud se tvar výstupních dat
  neustálí; sjednocením by se zabetonoval stav, který se ještě hýbe).
  ⚠️ **Pohltilo i dřívější `L8`** (31. 8.) - byla to tatáž práce ve větším rozsahu (sdílené
  `^JRL` readery a `Build*` do `BaseAgenda`/`JrShared`, Caché obdoba extrakce jádra na .NET straně),
  jen visela v aktivní roadmapě jako velká položka **bez přírůstku pro uživatele**. Vzít **až bude
  knižní JŘ doimplementovaný** - do té doby není podle čeho sjednocovat.
  **Podklad z analýzy 5. 8., aby se nemusela dělat znovu:** 27 pomocných
  funkcí se stejným názvem ve 2+ agendách, ale jen **15 jsou doslovné kopie** (~97 řádků k odstranění,
  ne 400-500, jak tvrdil původní odhad). 4 mají stejnou signaturu a rozešlé tělo - a to **záměrně**
  (`AddStandardJRFields` se liší v uživatelských popiscích polí: AD „Číslo linky" × MHD „Číslo linky
  (dle licence)"; `AddStandardJRParameters` nabízí jiné parametry sestavy). 8 sdílí jen název a dělá
  jinou věc - `BuildPoznamka` má dvě rodiny s **jiným datovým kontraktem** (zastávková posílá
  `KATEGORIE`+barvy, linková `DRUH`), `PopisDopravce` existuje 3× se třemi signaturami. Sloučit jde
  bezpečně **jen skupinu doslovných kopií a jen u zastávkových agend**, které kryjí zlaté odtisky
  a 46 testů helperů; **linkové agendy nemají ani jeden test** (2 000 řádků), takže tam by zásah jel
  bez sítě. Vzor existuje - `BaseAgenda` už 4 sdílené helpery hostuje.

- **Headless tisk (A3)** - „vytiskni linku X" z modulu přes PrintEngine fasádu (šablona+data→PDF
  bez stavu editoru). Dnes tisk jen v návrháři. Gate R4/R7.

- **Externí datové zdroje** (analýza 2026-07-12, na poptávku) - architektura připravená (engine zná jen
  `Template + DataContext`; DataContext je zdrojově agnostický JSON). Tři švy: Caché gateway agenda
  (0 změn .NET) · .NET provider vedle Caché (vzor `JsonFileDataSchemaProvider` + Composite) · headless
  A3 („POST šablona+JSON → PDF" = externí zdroj bez adaptéru; **A3 je nejpravděpodobnější nosič**).
  První krok s nejlepším poměrem cena/hodnota: **generický JSON zdroj (soubor/URL)** - tržní minimum
  (CSV/Excel/JSON/REST), náš wire formát už JSON je; využilo by i AI + testování šablon. Dořešit:
  credentials, schéma z dat, změkčení předpokladu „běží nad Caché".


---


## 🚋 Relace a vozové JŘ

Layout TBD - hlavička oběhu, tabulka spojů, poznámky (pravděpodobně víc elementů). Legacy:
`ALVA.MHD.ReportGenerator` (MHD) + `AD.ADREPORTGEN` (linky), viz
[`legacy-nahrada.md`](legacy-nahrada.md). **Relace (TiskJRRELACE)** - vývěsky
nádraží (SK zákazník, fork TiskJR ~50k LOC); **nejdřív ověřit živý use case**.

| Co | Stav |
|---|---|
| Analýza referenčních vozových JŘ - layout + wire formát | ⬜ |
| `VehicleScheduleElement` (pravděpodobně víc elementů) | ⬜ |
| Caché `VozovyJrAPI` - GetData/GetMockData + async (velký objem dat) | ⬜ |

---

## 📊 Report generátor (náhrada REPORTGENERATOR)

> ⚠️ **Gate před `F2`: R3 (prezentační nápovědy v datovém kontraktu + aditivní evoluce)** -
> [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md).

Největší kus náhrady - ne kvůli featurám (TD je napřed), ale kvůli **ekosystému ~28 agend** s RG
kontraktem (`GetSeznamSad`/`GetSadu`/`GetData`, `~`-tokenový stream). Nasazení: 33k textových elementů,
774 tabulek. **Ne big-bang** - etapizovat. Detail:
[`legacy-nahrada.md`](legacy-nahrada.md) §3.1 + §5 etapa 4.

| # | Co | Stav |
|---|---|---|
| F1 | **Prioritizace podle usage** - legacy `Main.cls:161-188` drží reálná čísla použití; seřadit agendy | ⬜ první (levný) |
| F2 | **Bridge vs čisté agendy** - bridge `LegacyRgAgenda` (překlad `~`-protokolu na TD JSON, všech 28 najednou s omezenou věrností) vs čisté reimplementace; doporučení: **bridge nejdřív** | ⬜ rozhodnutí |
| F3 | **TableElement na RG úroveň** (navazuje na dynamickou tabulku + `B76`): per-řádek zvýraznění 0-5 z dat, custom barvy per řádek/buňka, overflow módy (zalamování → klonování řádků), opakování mezisoučtu na další stránce, datové typy sloupců (datum/minuty/záporné červeně/skrýt nuly), řádek-jako-layout, **seskupení řádků** (nadpis a mezisoučet za skupinu) a **vnořená data** (doklad → jeho položky). ⚠️ Poslední dvě visely samostatně v backlogu; patří sem, protože mají smysl jen nad daty (pevná tabulka je vyplňovaná ručně) a jejich tvar se má odvodit **z reálných sestav RG**, ne navrhnout dopředu naslepo. Poptávku nemá ani jedna | ⬜ L |
| F4 | **Výstupní kanály fáze 2** - Excel export, PDF heslo + el. podpis (PDFSIGNER), Epson znakový tisk | ⬜ dle poptávky |
| F5 | **Jazykové varianty šablon** - per-jazyk kopie provázaná na CS zdroj | ⬜ |
| F6 | **Vědomě neportovat bez poptávky** - grafy (12×), AZTEC (2×), calculated (2× - výrazy TD nadmnožina), checkbox (8×); ověřit + škrtnout | ⬜ validace |

---

## 🗄️ Hotová práce

Detail dokončených etap → [`roadmap-archive.md`](roadmap-archive.md). Průběžný changelog → [`changelog.md`](changelog.md).
