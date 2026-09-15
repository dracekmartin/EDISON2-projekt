# Roadmap

> **Jen otevřená / akční práce.** Hotové → [`roadmap-archive.md`](roadmap-archive.md), průběžný
> changelog → [`changelog.md`](changelog.md), stav tiketů a věci „jen na poptávku" →
> [`customer-requirements §0`](customer-requirements-zastavkovy-jr.md).

> **Nejblíž na řadě:** nic akčního - § Zastávkové JŘ je uzavřená a § Linkové a knižní JŘ čeká
> celá na odpověď zvenku. Vezmi položku z Backlogu.
> ⚠️ **Ze zastávkových tiketů práci neber bez ověření** - #12843 je celý odpovězený, i když v evidenci
> ještě 3. 9. visely tři body jako zbývající (viz `customer-requirements §0.1`). Z hovoru 11. 9.
> platí jen to, co stojí níž v § Zastávkové JŘ.

---

## 🚏 Zastávkové JŘ

Požadavky z hovoru s TRANSDEV 11. 9. jsou hotové (`B141`-`B143`, `B145`, `B147`-`B149`), viz
[`roadmap-archive.md`](roadmap-archive.md). `B144` odložený v Backlogu, `B146` zrušený jako
nezadání. **Nic otevřeného.**

---

## 🚆 Linkové a knižní JŘ (náhrada TiskJR)

> ⚠️ **Gate: R1 (sdílené tabulkové jádro), R2 (layout zodpovědnosti), R5 (embedded fonty)** -
> [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md).

**Stav: paritní vlny jsou hotové** a z průchodu testovacími linkami nevzešel datový rozdíl proti
starému tisku. Zbývá průchod § K ověření (doklady k tiketům) a věci, u kterých je první krok
dotaz ven. Sloučení dvou Caché tiskových enginů drží `B56` v Backlogu - je to úklid vnitřku
bez přírůstku pro uživatele (rozhodnutí Jan 31. 8.).

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

### Čeká na odpověď zvenku

Ani u jedné není první krok kód. Podklady k tiketům drží
[`§14`](linkovy-knizni-jr-implementace-analyza.md).

- **`#8785` Vejdi výstup na zadaný počet stran** [M] (POVED, dřív `C8`). Starý modul to odmítl jako
  nemožné, náš engine na to má lepší pozici. ⚠️ **Poptávka je doložená** (Jan 14. 9.): zákazníci
  chtějí vejít na jednu stranu, kdykoli to jde, a jeden si výslovně řekl o **agresivní zmenšování
  poznámek**, jen aby se to vešlo. Tím to přestalo být „podnět, ne závazek".
  Liší se to od `B151` rozsahem: `B151` zmenšuje **jen tabulku**, aby se nedělila, tohle sahá na
  **celou sestavu včetně poznámek** a potřebuje vlastní strop (90 % tam nestačí) a vlastní volbu -
  agresivní zmenšování nemůže být tiché.

- **`#9088` bod 3: ručně zadaný „název směru" u linky** [S] (TRANSDEV, dřív `C9`; chtěné i pro
  vývěsky). ⚠️ **Nejde udělat u nás** - vyžaduje **nové pole u linky v jízdních řádech**. Náš díl
  je malý (pole pustit schématem a nabídnout v šabloně), ale bez toho pole nemá kam sáhnout.

- **`#9417` Filtrování dopravců v záhlaví** [M] (KORID, dřív `B102`). Přetečení hlavičky pod
  tabulku je vyřešené (záhlaví má vlastní sekci), zbývá volba, kteří dopravci se tisknou.
  **Čeká na zákazníka s linkou, která má mnoho dopravců** - jeho případ byl souhrnný JŘ dvou linek
  a sdružování se řeší v jízdních řádech, ne u nás.

- **`#7487` Počet poznámek na řádek podle druhu** [M] (KORID) - ⏸️ **Jan probere s kolegou**, jak
  je důležitý. Starý modul má volbu u **každého druhu** (Jedna / Dvě / Tři / Žádná) plus pořadí
  druhů, zákazník chce čtyři; náš „Počet sloupců" dělí celou legendu jednou mřížkou.

- **`B98` Nastavení starého tisku, která nikdo nemá zapnuté** - evidence, ne práce. Na 93 133
  linkách šesti firem nejsou nikde nastavené: místenkování, vlastní texty sloupce MHD, tisk
  provozoven dopravce, schovávání u linky, vrstva pod linkou, řazení spojů
  ([měření 21. 8., `§13.6`](linkovy-knizni-jr-implementace-analyza.md)). Vytáhnout, až si o to
  někdo řekne - **doklad z dat nebude**.

### K ověření na reálné sestavě

Ne implementace - **projít a zavřít**. Vzniká to tím, že návrhář umí věc, kterou starý modul neuměl,
takže nejde o práci, ale o doklad.

| Co | Proč to čeká |
|---|---|
| **Prázdný přípojný řádek nemá zabírat místo (#11300, KORID)** | **Zbývá ukázka a odpověď, ne kód** - postup a čím je to podložené: [`§16.2`](linkovy-knizni-jr-implementace-analyza.md). Tiket je pozastavený od 12/2025 s příslibem „příští rok s novým tiskovým modulem"; od 6. 9. jde jeho rozvržení předvést. |
| **Poznámky vedle tabulky, ne pod ní (#11373, KAD)** | Legenda je samostatný prvek, takže to umíme dnes; ověřit a tiket zavřít |
| **Pořadí sloupců spojů na `CB_CSADAD 1 / 0370618`** | Starý tisk dává oba směry do jedné tabulky, takže není co k čemu přiložit. ⚠️ **Odblokováno 28. 8.** - obousměrný tisk je hotový, takže tuhle linku už je čím porovnat. Vzít při nejbližším průchodu doklady |
| **Písmena ze zóny (`VS_CSAD 5 / 0771901`)** | V testovacích datech pro Vsetín na to není vzorek; firma navíc není v přepínači firem. Vzít, až ta data budou |
| **Živý náhled (`GetMockData`) ignoruje obousměrný tisk, turnusy i vlastní definici sloupců** (`LinkovyJrAd.cls:460-469` volá `BuildStrankaLinky` napevno s `deleni=2`, bez `defSloupcu`/`obousmerne`/`sTurnusy`) | Podle kontraktu `BaseAgenda` je mock jen ukázková data bez parametrů, takže to nejspíš je v pořádku - ověřit, jestli uživatel v návrháři čeká, že se mu obousměrný přepínač promítne do náhledu, a je zklamaný, že ne (zjištěno auditem 31. 8., nejistá závažnost) |

---

## 📥 Backlog

### Ostatní

- **`B144` Sloupec „Čas" i z ručních minut v trase** [S] **(zastávkové JŘ)** - volba zdroje hodnot:
  spočítané z jízdních dob (dnes), ruční „MHD čas tam/zpět" z trasy linky, nebo „Automaticky
  (dle linky)" podle přepínače u linky. Výchozí musí zůstat spočítané, jinak se českým firmám
  sloupec vyprázdní (pole mají prázdné). Server-side parametr → do allowlistu.
  ⚠️ **Odloženo, protože si o to zákazník neřekl** (Jan 11. 9.): vyšlo to najevo při hovoru jako
  rozdíl proti starému výtisku na Nitře, ale on sám řekl, že bude radši používat varianty tras.
  Vytáhnout, až to někdo bude chtít.

- **`B138` Odložené do pozadí** [-] (Jan 9. 9., evidence). Tisk celé sestavy jen černě (dnes jde
  vypnout jen barvy spojů) a volba „bez roku" u textů omezení v šabloně (dnes to řídí nastavení
  EDISONu a to stačí). Vytáhnout, až si o to někdo řekne.

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
