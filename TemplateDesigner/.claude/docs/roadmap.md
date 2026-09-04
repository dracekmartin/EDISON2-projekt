# Roadmap

> **Jen otevřená / akční práce.** Hotové → [`roadmap-archive.md`](roadmap-archive.md), průběžný
> changelog → [`changelog.md`](changelog.md), stav tiketů a věci „jen na poptávku" →
> [`customer-requirements §0`](customer-requirements-zastavkovy-jr.md).

> **Nejblíž na řadě:** § Aktuální focus - všech pět položek čeká na odpověď zvenku nebo na
> zájem, ne na práci. U každé je první krok se zeptat, ne psát kód.
> ⚠️ **Ze zastávkových tiketů práci neber bez ověření** - #12843 je celý odpovězený, i když v evidenci
> ještě 3. 9. visely tři body jako zbývající (viz `customer-requirements §0.1`).

---

## 🎯 Aktuální focus

Vytaženo z Backlogu 31. 8. (rozhodnutí Jan): paritní vlny jsou hotové, a než se sáhne na velký
vnitřní úklid, jdou na řadu tyhle menší věci. ⚠️ **Čtyři z nich čekají na odpověď zvenčí, ne na
práci** - u nich je první krok se zeptat, ne psát kód:

- **`C8`** - zeptat se zákazníka, jestli to pořád chce.
- **`C9`** - potřebuje nové pole u linky od kolegů z jízdních řádů.
- **`B102`** - čeká na zákazníka s linkou, která má mnoho dopravců.
- **`B98`** - je to evidence toho, co nikdo nemá zapnuté; vytáhnout, až si o to někdo řekne.

**`B100`** jde udělat rovnou, ale je to zásah do serveru i klienta kvůli třem linkám z 93 tisíc, takže spíš až
se bude na přenos nastavení linky sahat z jiného důvodu. (`N1` odsud odešlo hotové 31. 8., `B103`, `B113`, `B114` a `B115` 3. 9.; `B101` zavřená
3. 9. jako neplatná - jeden sloupec odjezdů v praxi nenastává; `B116` zavřena 4. 9.
živými daty na devu, a `B117` odešla 4. 9. hotová - byla
dokončená už 3. 9., jen zapsaná jako by ne.)

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

- **`B100` Ruční zlom stránky na tarifním čísle** (`PATCH,ZLOM`) - u linky jde nastavit tarifní
  číslo, na kterém se má tisk rozdělit; starý modul to respektuje, my ne. **Odloženo kvůli
  frekvenci** (rozhodnutí Jan 25. 8.): nastaveno na **3 linkách z ≈ 93 tisíc**. Neblokuje nic -
  klientská strana pro zlomy existuje a na drátě by šlo o jedno pole navíc.

- **`B98` Nastavení starého tisku, která nikdo nemá zapnuté** - evidence, ne práce.
  Na 93 133 linkách šesti firem nejsou nikde nastavené: tvar tisku místenkování, vlastní texty
  sloupce MHD, tisk provozoven dopravce, schovávání u linky, vrstva pod linkou a řazení spojů
  ([měření 21. 8., `§13.6`](linkovy-knizni-jr-implementace-analyza.md)). Řada vznikla v legacy pro
  jednoho zákazníka. Vytáhnout, až si o to někdo řekne - **doklad z dat nebude**, půjde jen
  o implementaci proti čtení legacy zdroje.

---

## 🚆 Linkové a knižní JŘ (náhrada TiskJR)

> ⚠️ **Gate: R1 (sdílené tabulkové jádro), R2 (layout zodpovědnosti), R5 (embedded fonty)** -
> [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md).

**Stav: paritní vlny jsou hotové a žádná otevřená tu není.** Z průchodu testovacími linkami nevzešel
žádný datový rozdíl proti starému tisku; co zůstalo neprojité, drží
[§ K ověření](#k-ověření-na-reálné-sestavě) níže. Sloučení dvou Caché tiskových enginů (dřív `L8`)
je v Backlogu u `B56` - je to úklid vnitřku bez přírůstku pro uživatele a nad rozdělaným kódem by
jen zabetonoval rozpracovaný tvar (rozhodnutí Jan 31. 8.).

⚠️ **Linková AD agenda je zveřejněná a je výchozí pro novou šablonu** (`Released = 1`; 24. 8.
zveřejněná, 1. 9. skrytá kvůli vydání, **3. 9. vrácená** kvůli dovývoji) - **ukáže se i zákazníkům,
jakmile se vydá nová verze**, takže před vydáním se musí znovu rozhodnout. Skrývá se na **dvou
místech**: `Parameter Released` v `LinkovyJrAd.cls` a konstanta ve `CreateSampleTemplate` (jinak
skrytí obejde samotný start aplikace); vazbu hlídá `SkrytaAgendaTests`.
⚠️ **`Released` je serverová hodnota, ne v exe** - nasazení `.cls` na dev změní nabídku agend i těm,
kdo mají starší exe. Městská zůstává skrytá, dokud neprojde porovnáním se starým modulem.

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
| **Poznámky vedle tabulky, ne pod ní (#11373, KAD)** | Legenda je samostatný prvek, takže to umíme dnes; ověřit a tiket zavřít |
| **Pořadí sloupců spojů na `CB_CSADAD 1 / 0370618`** | Starý tisk dává oba směry do jedné tabulky, takže není co k čemu přiložit. ⚠️ **Odblokováno 28. 8.** - obousměrný tisk je hotový, takže tuhle linku už je čím porovnat. Vzít při nejbližším průchodu doklady |
| **Písmena ze zóny (`VS_CSAD 5 / 0771901`)** | V testovacích datech pro Vsetín na to není vzorek; firma navíc není v přepínači firem. Vzít, až ta data budou |
| **Živý náhled (`GetMockData`) ignoruje obousměrný tisk, turnusy i vlastní definici sloupců** (`LinkovyJrAd.cls:460-469` volá `BuildStrankaLinky` napevno s `deleni=2`, bez `defSloupcu`/`obousmerne`/`sTurnusy`) | Podle kontraktu `BaseAgenda` je mock jen ukázková data bez parametrů, takže to nejspíš je v pořádku - ověřit, jestli uživatel v návrháři čeká, že se mu obousměrný přepínač promítne do náhledu, a je zklamaný, že ne (zjištěno auditem 31. 8., nejistá závažnost) |

---

## 📥 Backlog

### Ostatní

- **`#7487` Počet poznámek na řádek podle druhu** [M] - ⏸️ **Jan probere s kolegou**, jak je
  důležitý. Starý modul má volbu u **každého druhu** (Jedna / Dvě / Tři / Žádná) plus pořadí
  druhů, zákazník chce čtyři; náš „Počet sloupců" dělí celou legendu jednou mřížkou. Není to
  nedodělek prvku Poznámky - ten je hotový a nastavení má kam jít (`DRUH` je na drátě).

- **`B66` Vlastní bloky prvků („moje komponenta")** [M] (podnět Jan 12. 8.). Uživatel vybere pár
  prvků, uloží je pod jménem a vkládá jedním kliknutím do dalších šablon - mezistupeň mezi uloženou
  sekcí a seskupením prvků. **Čeká na pevnou tabulku**: jako jeden prvek je uložení skoro zadarmo,
  z šesti textů musí blok řešit relativní pozice. Otevřené: kde bloky žijí a odkud se vkládají.
  **Pohltí i připravené bloky agend** (dřív `B77`) - jsou to titíž pojmenované bloky, jen dodané
  námi (rozhodnutí Jan 17. 8.).

- **`B18` Deklarativní pravidla EDInputFormu** [M] (odloženo - rozhodnutí Jan 5. 8.: je to
  **kapabilita sdílené komponenty pro celý EDISON**, ne featura návrháře). Formulář dnes reaguje na
  vyplněné hodnoty jen oklikou přes server. Začít **viditelností** (`visibleWhen`), gramatiku sdílet
  s tiskovými parametry - nezavádět druhý dialekt; skutečná validace zůstává imperativní.
  Nese s sebou tři návazné věci: migraci `CellStyleEditor` na formulář z Caché, zašednutí polí
  mrtvých bez parametru sestavy a **popis u parametru sestavy** (dnes nejde vysvětlit krkolomný
  label) - všechno až s gramatikou, ne zvlášť.

- **`B56` Sjednocení Caché tiskových enginů a jejich opsaných pomocných funkcí** (odloženo -
  rozhodnutí Jan 5. 8. a 31. 8.: nedělat předčasnou deduplikaci, dokud se tvar výstupních dat
  neustálí). ⚠️ **Pohltilo i dřívější `L8`** - tatáž práce ve větším rozsahu, jen visela v aktivní
  roadmapě bez přírůstku pro uživatele. Vzít **až bude knižní JŘ doimplementovaný**.
  Podklad (co je doslovná kopie, co se liší záměrně, kde chybí testy):
  [`legacy-nahrada.md §4`](legacy-nahrada.md).

- **Headless tisk (A3)** - „vytiskni linku X" z modulu přes PrintEngine fasádu (šablona+data→PDF
  bez stavu editoru). Dnes tisk jen v návrháři. Gate R4/R7.

- **Externí datové zdroje** (na poptávku) - architektura je připravená, engine zná jen
  `Template + DataContext` a ten je zdrojově agnostický JSON. Nejpravděpodobnější nosič je
  **headless tisk** („pošli šablonu a JSON, vrať PDF"); samostatně by prvním krokem byl generický
  JSON zdroj ze souboru nebo URL. Dořešit přihlašovací údaje a odvození schématu z dat
  (analýza 12. 7., detail v historii roadmapy).

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
