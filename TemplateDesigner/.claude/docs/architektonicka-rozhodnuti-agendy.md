# Architektonická rozhodnutí pro postupné přidávání agend

> **Datum:** 2026-07-02 · **Účel:** srozumitelně (bez zkratek a žargonu) sepsat rozhodnutí,
> která musíme udělat - nebo aspoň mít na paměti - aby model „jeden tiskový engine + postupně
> přidávané agendy" vydržel roky a nerozsypal se. Každé rozhodnutí má: co tím myslíme,
> jaký problém řeší, co se konkrétně rozhoduje, doporučení a termín „nejpozději kdy".
>
> Vzniklo ze strategického review 2026-07-02 (navazuje na
> [`legacy-nahrada.md`](legacy-nahrada.md)).

---

## 0. Společný slovník: co je „engine" a co je „agenda"

Než k rozhodnutím - ať mluvíme stejným jazykem o základním modelu.

**Tiskový engine** = stabilní jádro, které se při přidání agendy NEMĚNÍ (nebo jen výjimečně
a vědomě). Konkrétně v našem kódu:

| Část | Kde | Co dělá |
|---|---|---|
| Datový model šablony | `Models/` (Template, Section, TemplateElement) | co je šablona |
| Výrazový jazyk | `Models/Expressions/` | `{POLE:N2}`, podmínky, agregace |
| Stránkování | `Models/Layout/PageLayoutEngine` | rozložení sekcí na stránky |
| Kreslení | `Rendering/` + `Render()` elementů + `PreviewCanvas` | pixely/PDF |
| Ukládání | `Serialization/` + Caché `API.Sablony` | JSON šablony v DB |
| Designer UI | okna, canvas, undo/redo, EDInputForm dialogy | editace |

**Agenda** = zásuvný modul, který se přidává. Skládá se ze tří artefaktů:

1. **Caché třída** (`Agendy/*.cls` dědící `BaseAgenda`) - ví, kde jsou data a jak z nich
   postavit JSON pro tisk (GetSchema/GetData/GetMockData/GetParameters).
2. **Element(y)** - pokud typ sestavy potřebuje nový druh „krabičky" na stránce
   (StopList, DepartureGrid…). Ne každá agenda ho potřebuje - Faktury vystačí
   s Text + Table.
3. **Šablony** - výchozí sady, které dostane zákazník.

**Cíl všech rozhodnutí níže:** aby cena bodů 1–3 pro desátou agendu byla stejná nebo nižší
než pro třetí. Legacy zemřelo přesně na tom, že každý nový typ sestavy znamenal nový
copy-paste render engine (TiskMHD 16k řádků, TiskJR 6,6k řádků, RG 10k řádků kreslení).

---

## R1. Sdílené tabulkové jádro (dřív jsem říkal „table kernel")

**Co tím myslím.** Skoro každý JŘ/RG element je nakonec tabulka: sloupce s hlavičkami,
řádky s buňkami, měření šířek a výšek, kreslení mřížky. Dnes má každý element vlastní
kompletní implementaci tohohle: `StopListElement` (~700 ř.), `DepartureGridElement`
(~1 500 ř.), `TableElement`, teď vzniká `DynamicTableElement`. Sdílíme **styly buněk**
(`CellRenderer` - jak vypadá jedna buňka) a **editory** (`ColumnListEditor`), ale
**ne layout** - tj. logiku „spočítej šířky sloupců, rozděl řádky, změř výšku, vykresli
řádek po řádku". Tu si každý element píše znovu.

„Sdílené jádro" = jedna třída/sada tříd, která umí: model sloupců (šířky, viditelnost,
pořadí), měření (kolik místa řádky zaberou), řádkový layout (kresli řádky odshora, tady
skonči, tolik se nevešlo). Konkrétní elementy by pak dodávaly jen to své: StopList
dekorace (šipka směru, zvýraznění aktuální zastávky), DepartureGrid pivot (hodiny ×
skupiny dnů - ten je jiný, pivot do jádra nepatří), LineTable sloupce spojů.

**Jaký problém to řeší.** Linkové a knižní JŘ přinesou `LineTableElement` (svislý seznam zastávek ×
sloupce spojů = strukturálně kříženec StopListu a TableElementu). Pak relace, vozové JŘ,
RG tabulky s per-řádkovým designem. Bez sdíleného jádra každý = dalších ~1 000+ řádků
layoutu, které se musí zvlášť ladit a zvlášť neúmyslně rozbíjet.

**Co se rozhoduje.** Až se začne navrhovat LineTableElement (linkové a knižní JŘ):
postavit ho jako pátý samostatný element (rychlejší teď, dražší navždy), NEBO nejdřív
vytáhnout sdílené řádkové jádro z StopList + DynamicTable a LineTable postavit nad ním
(pomalejší teď o ~1–2 týdny, levnější každý další element).

**Doporučení.** Druhá cesta. Podmínky jsou příznivé: budou existovat právě 2–3 konzumenti
(pravidlo „extract až při 2+"), a refaktor existujících elementů kryjí letošní testy.
DepartureGrid do jádra necpat - je to pivot, ne řádková tabulka: sdílí se na úrovni
nastavení a dat, ne renderu.

**Nejpozději kdy:** první design LineTableElementu. Do té doby drobnost s velkým efektem:
**vše, co teď vzniká v DynamicTableElement, psát s vědomím, že se to do jádra přesune**
(žádné privátní zkratky provázané s dialogem, čisté oddělení model sloupců ↔ render).

> **✅ Rozhodnuto 2026-07-03 (diskuze):** zárodkem jádra bude **DynamicTableElement** (nejčistší
> řádková tabulka - sloupce × řádky z dat, bez doménových dekorací); extrakce proběhne **až u
> LineTable** - tři konzumenti (DynamicTable + StopList + LineTable) ukážou, co je opravdu
> společné; dřívější extrakce nad dvěma by tvarovala abstrakci podle StopList domény a nesla
> riziko přepisu gold-ověřeného kódu bez okamžitého přínosu. **Akce hned (R1a):** hodinový
> kontrolní průchod DynamicTableElement - čisté rozvrstvení model sloupců / měření / render,
> aby budoucí extrakce byla mechanická. Ujasněno: jádro ≠ PageLayoutEngine (dvě patra -
> sekce/stránky × vnitřek elementu); jádro bydlí v `Rendering/` vedle `CellRenderer`.

---

## R2. Rozdělení layoutových zodpovědností (dřív „layout kompetence")

**Co tím myslím.** V systému jsou dvě místa, která rozhodují, „co bude kde na stránce":

- **PageLayoutEngine** - pracuje se **sekcemi**: která sekce na kterou stránku, rozdělení
  přes stránky, hlavičky/patičky.
- **Elementy** - pracují se svým **vnitřkem**: DepartureGrid si sám zmenší font, když se
  nevejde (`ComputeFontScale`), tabulka si rozvrhne sloupce.

Hranice mezi nimi je dnes jednoduchá: engine se elementu zeptá jen „jak jsi vysoký"
(`IElementHeightCalculator`) a jinak si nerozumí.

**Jaký problém to řeší.** Požadavky z legacy TiskJR a z prezentací padají přesně **mezi**
ty dvě úrovně: (a) skládání bloků vedle sebe i pod sebe podle zbývajícího místa (směr Tam
a Zpět na jedné stránce, když se vejdou), (b) „zbytek legendy dotiskni na další stránce",
(c) zmenšování fontu podle šířky s přepočtem sloupců, (d) imposice 2×A5 na A4 (požadavek
P3). Když se každý vyřeší ad hoc - něco v enginu, něco v elementu, jak zrovna hoří -
vzniknou dva poloviční layout systémy, které si lezou do zelí, a každá další featura bude
vyžadovat znát oba.

**Co se rozhoduje.** Sepsat (stačí 1–2 stránky do `.claude/docs/architecture.md`) tři věci:

1. **Co navždy patří enginu:** stránky, pořadí a dělení sekcí, hlavičky/patičky -
   a nově i imposice (P3), jako samostatná vrstva NAD enginem (bere hotové stránky
   a skládá je na archy), ne uvnitř něj.
2. **Co navždy patří elementu:** vnitřní mřížka, fonty, auto-zmenšování.
3. **Jak spolu mluví:** dnes jen „jak jsi vysoký". Pro „dotisk zbytku" bude potřeba
   bohatší dohoda - element umí říct „vykreslil jsem řádky 1–40, zbývá 12" a engine
   umí říct „pokračuj na další stránce od řádku 41". To je rozšíření rozhraní, které
   se musí navrhnout jednou a pořádně, ne třikrát různě.

**Doporučení + termín.** Napsat před začátkem linkových a knižních JŘ (půl dne). Do té doby: žádné nové
layoutové chování nepřidávat do elementů, pokud koncepčně patří enginu (a naopak).

> **✅ Model potvrzen 2026-07-03 (diskuze):** „engine skládá celek (elementy/sekce/stránky),
> element rozhoduje jen lokálně o svém vnitřku a výsledek hlásí nahoru" = přesně dnešní
> architektura, nic se nemění. Z R2 zbývá jediná otevřená věc: **návrh partial-fit rozhraní**
> (rozšíření hlášení z „jsem vysoký X mm" na „vykreslil jsem řádky 1–40, zbývá 12, pokračuj
> odtud") - jednou, jednotně pro všechny tabulkové elementy, před linkovými a knižními JŘ.

---

## R3. Datový kontrakt agend: stabilita + „prezentační nápovědy" (dřív „presentation hints")

**Co tím myslím.** „Kontrakt" = dohodnutý tvar JSONu, který Caché agenda vrací
(GetSchema/GetData/GetParameters - popsáno v `cache-api.md`). Je to jediné hrdlo, kterým
projde každá migrovaná agenda - designer, print engine i šablony vidí agendu jen skrz něj.

„Prezentační nápověda" = případ, kdy data nesou kus VZHLEDU, ne jen obsah. Normálně
platí: data říkají CO (odjezd 14:35), šablona říká JAK (font, barva). Ale někdy vzhled
z dat legitimně vyplývá: odjezd nízkopodlažního spoje má být zelený, mezisoučtový řádek
faktury tučný a opakovaný na další stránce. U nás už to v malém existuje - odjezd nese
`FORE_COLOR`/`BACK_COLOR` - ale jako výjimka, ne pravidlo.

**Jaký problém to řeší.** Celý REPORTGENERATOR je postavený OBRÁCENĚ než my: jeho server
posílá klientovi prezentační příkazy („řádek se zvýrazněním 3, buňka barvy X, přeteklý
text zalom módem 2"). Až budeme migrovat jeho agendy (report generátor), každá bude potřebovat
nějak vyjádřit „tenhle řádek je zvýrazněný" - a když nebude dohodnutý způsob, každá si
vymyslí vlastní klíče a za rok budeme mít pět dialektů téhož.

**Co se rozhoduje.**

1. **Schválit úzký, pojmenovaný seznam prezentačních nápověd**, které smí data nést
   a elementy respektovat. Kandidáti (z reálného použití RG + JŘ): barva textu/pozadí
   buňky či řádku, úroveň zvýraznění řádku (např. 0–5, šablona určí, jak úrovně vypadají),
   „opakuj tento řádek na další stránce" (mezisoučty), „text přes celou šířku".
   Všechno ostatní zůstává věcí šablony.
2. **Pravidlo evoluce kontraktu: jen aditivně.** Nový klíč v JSONu = OK (starý klient ho
   ignoruje). Změna významu nebo odebrání existujícího klíče = zakázáno bez migrace.
   (Stejná filozofie jako u JSON šablon a TemplateMigratoru.)

**Doporučení + termín.** Zapsat do `cache-api.md` jako závaznou sekci. Nejpozději před
rozhodnutím F2 (bridge vs. čisté agendy) - bez toho nejde F2 rozhodnout poučeně, protože
věrnost bridge stojí přesně na tom, kolik prezentace umíme přenést.

> **Upřesněno 2026-07-03 (diskuze):** základ kontraktu UŽ existuje (`cache-api.md` - tvary
> metod, čistý JSON, konvence klíčů; + `EDControls/docs/wire-formaty.md`). Doplnit se mají
> právě dvě sekce: **(a) prezentační nápovědy v1** - princip „volitelný klíč + sémantický kde
> to jde": `ZVYRAZNENI` 0–5 (šablona určí vzhled úrovní), typ řádku (data / mezisoučet /
> nadpis skupiny), „opakuj řádek na další stránce", „text přes celou šířku"; přímé
> `FORE_COLOR`/`BACK_COLOR` legalizovat jako výjimku pro legacy paritu (JŘ je už používá).
> Seznam v1 držet krátký, rozšířit při F1 podle RG usage statistiky. **(b) pravidlo evoluce** -
> kontrakt se mění jen přidáváním klíčů; změna významu / odebrání = zakázáno bez migrace.

---

## R4. Kde poběží tisk bez designeru (headless) + oddělení „PrintEngine"

**Co tím myslím.** „Headless tisk" = vytištění sestavy BEZ otevřeného designeru - zákazník
v EDISONu klikne „vytiskni linku 320001" a dostane PDF. To je položka A3 v roadmapě.
Otázka, kterou roadmapa neřeší: **na kterém počítači ten render běží?**

- **Varianta A - na stanici zákazníka:** EDISON spustí `TemplateDesigner.exe` se speciálními
  parametry (tak to dělá legacy RG: `AUTOEXPORT=1`). Nejmenší práce; ale každá stanice musí
  mít správnou verzi .exe a nainstalované fonty.
- **Varianta B - na serveru:** malá služba (vzor máme - ServiceGtfsRt), která dostane
  „šablona + linka" a vrátí PDF. Jedna instalace, jedna verze, fonty jen na jednom místě;
  víc práce na začátku.

„PrintEngine fasáda" = technická příprava, která drží obě cesty otevřené: dnes umí PDF
vyrobit jen `PreviewCanvas`, což je třída designeru (drží stav editoru). Fasáda = vytáhnout
z ní čistou třídu „dej mi šablonu + data → vrátím PDF", bez vazby na editor. T2 (async
export) už udělalo půlku - render bere šablonu parametrem.

**Co se rozhoduje.** A vs. B pro pilot - a vědomí, že B je pravděpodobný cíl.

**Doporučení + termín.** Pro pilot varianta A (nejkratší cesta k zákazníkovi), ale
**fasádu udělat hned při implementaci A3** (~den práce navíc), takže přechod na B později
= zabalit fasádu do služby, ne přepis. Rozhodnout při návrhu bloku A.

**Navazuje `B190` - tisk z modulu jízdních řádů bez návrháře** (roadmapa, do příští verze, obě strany děláme my).
Uživatel v jízdních řádech (nové okno JŘ) vybere linky a dostane PDF bez otevření návrháře; šablonu určí jednoduchá volba výchozí šablony (nebo podobně, doladí se).
Podle doporučení výše začít spuštěním návrháře z EDISONu s parametry (launch kontrakt už je) a render oddělit fasádou; postupovat pilotem podle R7.
PDF bez okna ze spuštění už je pro jednu linku (`Vystup=PDF`, rozdílový JŘ 23. 10., `Config/LaunchInput`), šablona zatím jen ze `LaunchTemplates/` v exe.
Zbývá víc linek a volba šablony z databáze.

**Výchozí šablona (návrh 1. 11., rozhodnuto 2. 11., čeká na domluvu s kolegou z JŘ).**
Starý systém výchozí šablonu neznal: report generátor, TiskJR i TiskMHD tiskly šablonu, kterou jim poslal volající modul.
Moduly ji určovaly napevno v kódu, nastavením firmy (zpřesněným na provozovnu, pokladnu, dílnu, linku nebo zaměstnance), poslední volbou uživatele v dialogu, nebo u faktur pravidly podle druhu dokladu.
JŘ měly u linky šablonu pro A4 a A5 (`^JRL(...,"PATCH","prt","win",1|7)`, MHD `"prt","winmhd",1|2`) a ta přebila volbu v dialogu.

- **Agenda** (kód agendy a varianty) je zdroj dat a druh výstupu; výchozí šablona se váže na ni, stejně jako dnes šablony (RG: sada = agenda, modul = varianta, `F7`).
- **Instance** je jednotka tisku (linka, zaměstnanec, doklad); tisk víc instancí je seznam, `F9` k němu později přidá PDF, heslo a e-mail ke každé.
- **Volající modul**, který potřebuje pro jednu agendu víc šablon (provozovna, pokladna), si volbu drží ve svém nastavení a pošle číslo šablony; obecný mechanismus pro to nestavíme.

Pravidlo výběru šablony běží na serveru, jedno pro návrhář, okno JŘ i tisk bez uživatele; platí první krok, který šablonu dá:

1. šablona, kterou poslal volající (uživatel ji vybral v okně modulu, nebo ji modul určuje svým nastavením - dnes porovnání linek s rozdílovým JŘ);
2. šablona přiřazená instanci (linka, zaměstnanec) - jen připravit, nedělat;
3. výchozí šablona firmy pro agendu;
4. výchozí šablona M-line pro agendu;
5. jinak hláška „agenda nemá šablonu pro tisk".

Nahradí dnešní skrytá pravidla: spuštění bere nejnověji upravenou šablonu firmy, úvodní obrazovka první systémovou v pořadí souborů.

Rozsah `B190`:

- ✅ (2. 11., krok 1) výchozí šablona firmy mimo JSON šablony `^ALVA.TemplateDesigner.Templates(IdFa,"DEFAULT",AgendaCode)` (příznak v JSON by zkopírovalo „Uložit jako"); výchozí šablonu M-line určuje parametr třídy agendy `VychoziSablonaMline` (linkový JŘ AD: A4 s poznámkami pod tabulkou); pravidlo a seznam pro formulář v `API/SablonyProTisk.cls`;
- ✅ (2. 11., krok 4) nastavení v okně šablon (Ctrl+O) se stromem šablon (níž): tlačítko „Nastavit jako výchozí pro tisk" / „Zrušit výchozí pro tisk" u vybrané šablony; šablona, kterou tisk vezme, je v seznamu tučně a ve sloupci Pro tisk, aby ji uživatel poznal na první pohled (Jan 2. 11.);
- ✅ (2. 11., krok 2) spuštění bez okna rozšířené o víc linek (`LINKA_ID` čárkou, jedno PDF), volitelné číslo šablony (`Sablona=`) a PDF ze šablony z databáze; rozdílový JŘ dál z `LaunchTemplates/`.

Rozhodnuto (Jan 2. 11.):

- výchozí šablona je za firmu - tisk na jeden klik dopadne stejně, ať tiskne kdokoli; výchozí za uživatele jde přidat jako další krok pravidla;
- šablona u linky zatím ne: krok 2 zůstává v pravidle zapsaný, ale nedělá se a šablona se bere výš (firma, M-line); čtení od konkrétní linky se doladí, až se ukáže potřeba. Staré nastavení u linky ukazuje na staré šablony, převzít nejde. Kolik firem tiskne linky různými šablonami, změřené není;
- víc linek dá jedno PDF, linky v pořadí, v jakém je pošle volající. PDF po souborech, dynamické názvy souborů a řazení se dodělají, až na ně přijde řada - starý systém na to měl nástroje, z nich se vyjde; tiskové okno návrháře už umí členění a název s datovými poli (`__EXPORT_SPLIT`, `__EXPORT_FILENAME`);
- kdo jen tiskne, nechodí přes návrhář: zůstává v okně JŘ jako dnes a nový tisk tam nahradí cesty přes report generátor a postskript. Návrhář jen vyrobí PDF bez okna. Okno JŘ AD tiskne zastávkový a linkový JŘ AD, později relace; okno MHD totéž vedle.

**Okno JŘ AD (domluveno s kolegou 2. 11., obě strany děláme my):** tlačítko Tisky dostane hned pod „Návrhář sestav" položky „Tisk linkového JŘ (nový)" a „Tisk zastávkového JŘ (nový)", souběžně se starými tisky, kterým končí podpora 31. 1. 2027 (`ISYJR2/CACHE/ALVA/ISY/ISYJR2/TEMPLATES/Tisk.cls`).
Položka otevře BIG formulář jako starý Tisk JŘ (`Tisky/TiskJR.cls`): nahoře skupina „Nastavení tisku" se šablonou (šablony agendy z databáze návrháře, předvybraná podle pravidla) a složkou pro PDF, pod ní **všechny parametry sestavy agendy** ve skupinách agendy.
Po OK okno spustí návrhář bez okna s označenými linkami (jsou vždy z jedné skupiny, posílá se skupina a seznam čísel; příkazová řádka unese zhruba 3 500 linek) a s hodnotami parametrů, návrhář ukáže průběh a otevře PDF.
Nejdřív linkový JŘ (pilot), po odladění zastávkový.
Do příští verze (Jan 2. 11.): položka je zákazníkům skrytá, ukáže ji `^JRR("TiskNovymNavrharem")=1` v datovém namespace firmy.

**Strom šablon (rozhodnuto Jan 2. 11.):** jedno okno pro přehled, otevření, výchozí šablonu pro tisk a později poslání šablony (`B189`), ne druhé okno vedle Ctrl+O - dva seznamy téhož by se rozešly.
Plochý seznam agend by se s agendami report generátoru nevešel, proto pevný strom: oblast práce → agenda → varianta, šablony vybrané větve vpravo.
Strom skládá server z registru agend (`API.StromSablon`) a nic v něm uloženého není; oblast práce ohlásí agenda parametrem `OblastPrace`, seznam oblastí je v `AgendyRegistr.OblastiPrace` (z rozboru převodu report generátoru, `F7`).
Variantu strom vynechá, když ji agenda má jen jednu; oblast bez agendy neukáže; nahoře je Všechny šablony (i agend mimo strom) a hledání v seznamu pak prohledá všechny.
Složky, které si zakládá uživatel, vědomě nejsou (Jan 2. 11.): starý report generátor měl pevnou větev a modul a pod modulem složky libovolně hluboko, ale na CB_DP je zakládala skoro jen M-line a odpovídaly naší agendě (Mzdy 2: Výplatní páska, Mzdový list…) nebo oblasti práce (Osobní doprava: Plánování, Zpracování); zákazník tam měl dvě.
Šablony, které firma vidí (vlastní a M-line), čtou strom, seznam okna i formulář před tiskem jednou cestou (`API.Sablony.VetveFirmy` a `IdSablon`); další větev (šablony od jiné firmy, `B189`) přibude jen tam.
Později: licence (varianta ponese modul a strom firmě skryje agendy bez licence; starý návrhář ukazoval všechny), jazykové verze šablon (ve starém stromu pod českou předlohou; vlastnost šablony, ne úroveň stromu, s fakturami), strom i v shellu (táž odpověď serveru, pak komponenta v EDControls).

**Parametr sestavy × nastavení prvku (Jan 2. 11.):** parametr je volba, která se smí lišit tisk od tisku. Šablona nese jeho výchozí hodnotu (firma ji nastaví v návrháři a uloží se šablonou), formulář před tiskem ji ukáže a uživatel ji smí změnit jen pro tento tisk, šablonu nepřepíše.
Nastavení prvku je vzhled a obsah výtisku a ve formuláři před tiskem není. Formulář proto ukazuje všechny parametry agendy jako tiskové okno návrháře.
Paměť poslední volby uživatele formulář zatím nemá - jednorázová změna by tiše platila i příště a dva lidé by ze stejné šablony tiskli jinak; přidat jde kdykoli.
BIG i EDInputForm (`AgendaParams.cls`) jsou tenké převody téže definice parametrů z agendy (`SablonyProTisk.ParametryProTisk`), nový parametr se objeví v obou.
Parametr jen pro určitý sloupec (`visibleWhenColumn`) a jen pro některé linky (`visibleWhenListedIn`) BIG ukazuje vždy, jako tiskové okno návrháře při tisku víc linek.

Dnešní formulář má navíc „Režim zvýraznění". Režim „po linkách" (linkový JŘ jednou za každou zastávku se zvýrazněnou zastávkou) pokrývá parametr „Stránka pro každou zastávku"; režim „po zastávkách" (řazení podle zastávek napříč linkami) pokrytý není. Volba „Upřednostnit šablonu a vlastnosti z linky" odpadá se šablonou u linky.

---

## R5. Fonty jako součást aplikace, ne instalace Windows

**Co tím myslím.** Dnes render hledá fonty nainstalované ve Windows; když font chybí,
`FontResolver` potichu použije Arial. „Embedovat" = přibalit soubory fontů přímo do
aplikace (resource v .exe nebo blob v DB) a načítat je z ní - render pak NIKDY nezávisí
na tom, co je na stanici nainstalované.

**Jaký problém to řeší.** U textu je chybějící font kosmetika. U symbolového fontu
**Timetable** (legacy JŘ) je to věcná chyba: znak č. 72 je v tom fontu symbol vozíčkáře;
v Arialu je to písmeno „H". Jízdní řád pak potichu lže - žádná chyba, žádné varování,
jen špatný výstup u zákazníka, který si font nenainstaloval. Legacy to řešilo vynucenou
instalací fontu a kontrolou při startu - to nechceme kopírovat.

**Co se rozhoduje.** Málo co - spíš to prostě udělat: SkiaSharp umí font načíst ze streamu
(`SKTypeface.FromStream`), takže jde o rozšíření `FontResolver` + přibalení TTF souborů
(pozor na licence fontů - Timetable je interní/legacy, ověřit). Souvisí s R4: font musí
být tam, kde běží render.

**Doporučení + termín.** Nejpozději jako první krok linkových a knižních JŘ (roadmapa už to
naznačuje); zvážit dřív, pokud pilot ukáže problémy se spec. znaky u MHD.

---

## R6. Systémové šablony distribuovat s verzí aplikace, ne přes databázi

**Co tím myslím.** Blok B počítá se dvěma druhy šablon (podle legacy vzoru): **systémové**
(vyrábí M-line, zákazník je nemůže měnit) a **zákaznické** (vlastní tvorba). Otázka je,
KUDY se systémové dostanou k zákazníkovi.

- **Cesta 1 - přes DB** (legacy model `AktualizujAgendu`): M-line je nahraje do zákazníkovy
  databáze při aktualizaci.
- **Cesta 2 - s aplikací:** systémové šablony jsou přibalené k .exe (soubory vedle/uvnitř,
  read-only), DB slouží JEN zákaznickým.

**Jaký problém to řeší.** T3 správně zavedl: šablonu z novější verze formátu starší
aplikace odmítne. Při cestě 1 tím vzniká „verzní matice" - museli bychom hlídat, že
šablona v9 nejde k zákazníkovi s .exe v8, řešit částečné aktualizace atd. Při cestě 2
matice neexistuje z principu: šablony přišly se stejným buildem, se kterým se otvírají.
Zákazník dostane nové systémové šablony prostě s novou verzí aplikace.

**Doporučení + termín.** Cesta 2. Rozhodnout při návrhu bloku B (B1/B2) - zmenšuje ho
na zlomek (odpadá distribuční pipeline i zamykání v DB; „zamčené" = jsou read-only
soubory). Jediné, co zůstává v DB řešit, je „zákazník si systémovou šablonu zkopíruje
a upraví" - to je obyčejné „Uložit jako".

---

## R7. Pilot na jedné agendě před masovou migrací (zásada, ne kód)

**Co tím myslím.** Než se začnou přendávat další agendy, protáhnout JEDNU hotovou
(zastávkový JŘ) celou cestou až k reálnému zákazníkovi: spuštění z EDISONu (A1/A2),
tisk koncovým uživatelem (A3), výchozí šablony (B2), nasazení .exe i Caché tříd (D3/D4),
podpora. Kandidát: TRANSDEV (nejvíc otevřených tiketů = největší motivace spolupracovat).

**Jaký problém to řeší.** Trubka „EDISON → agenda → šablona → PDF → zákazník" ještě nikdy
neběžela naostro. Každá její vada objevená až při migraci páté agendy se opravuje 5×.
A navíc: zákazníkům byl modul slíben „na polovinu 2026" - ta je teď.

**Doporučení.** Pilot = další velký blok po dokončení rozdělané práce (DynamicTable).
Linkové JŘ i report generátor až po něm - poučené tím, co pilot ukáže.

---

## R8. Sdílení kódu mezi agendami a cíl sloučení (`B56`) - ROZHODNUTO 29. 10. 2026

**Co tím myslím.** Po čtyřech agendách JŘ (zastávkový a linkový, AD a MHD) je vidět, co mají agendy
společné. Kód se dnes sdílí třemi způsoby naráz: společný základ `BaseAgenda`, přímá volání mezi
agendami a opsané kopie. Kopie se už začaly rozcházet (dopravce ve třech verzích, platnost linky
na sedmi místech, barva `0;0;0` jednou černá, jednou nenastavená).

**Jaký problém to řeší.** Pátá agenda by opsala nejbližšího sourozence a rozjezd by se násobil:
tatáž oprava na víc místech a nikdo neví, na kterých.

**Rozhodnuto (Jan 29. 10.):**

- Agendy zůstávají **přímými potomky `BaseAgenda`** - registr hledá jen ty a kódy agend se nemění.
  `BaseAgenda` je kontrakt a obecné věci.
- Sdílený kód jde do **pomocných tříd mimo hierarchii agend**, rozdělených podle toho, co dělají (čtení
  dat AD, stavba výstupu zastávkového JŘ, stavba výstupu linkového JŘ, společné parametry). Ne dědičnost:
  kód se sdílí ve dvou směrech (AD × MHD a zastávkový × linkový) a jedna dědičnost pokryje jen jeden.

**Upřesněno 30. 10. (Jan) - dvě osy a pravidlo druhého použití:**

- **Podle zdroje dat** (AD, MHD): načtení linky, tras, spojů, platnosti, dopravce, poznámek a nastavení linky.
  Sdílí je zastávkový i linkový JŘ téže varianty, později relace a vozový JŘ.
  Data nebo jméno pracovního globálu dostane parametrem (`cache-api.md` pravidlo 6), jinak by relace nemohla číst víc linek najednou.
- **Podle druhu výstupu** (zastávkový, linkový, později relace a vozový): stránky a řádky ve tvaru, který čtou prvky
  (`KontraktDat.cs`), parametry a nabídka dat druhu. Sdílí je AD i MHD.
- **Obecný základ** (`BaseAgenda`): kontrakt agendy, hlášky bez dat a chyby dat, formát data, pomocníci nabídky dat a parametrů.
  Slouží i agendám report generátoru, proto do něj nepatří čtení dat AD ani věci jen pro JŘ (30. 10. jich tam bylo 14 z 28 metod,
  přesunuly se v `B56` S13; že základ data nečte, hlídá `DataPodleZdrojeTests.ZakladAgendNectaData`).
- **Věc společná oběma druhům výstupu** (vlastní sloupce spojů ze šablony) má vlastní pomocnou třídu podle role (`Jr.VlastniSloupce`).
- **Agenda nevolá jinou agendu** - co potřebují dvě, je v pomocné třídě (výjimka: rozdílový JŘ postavený nad linkovým AD);
  hlídá `DataPodleZdrojeTests.AgendaNevolaJinouAgendu`.
- **Agenda** vybere zdroj a druh, drží pořadí kroků a vědomé rozdíly svého starého modulu.
  Pomocné třídy JŘ jsou v balíku `ALVA.TemplateDesigner.Agendy.Jr` (složka `Agendy/Jr/`), jméno podle role (`DataAd`, ne `Helpers`).
- **Žádný společný tvar dat pro AD i MHD** (jeden výpočet nad daty z obou zdrojů): výpočty pocházejí ze dvou starých
  modulů a výstup musí zůstat obsahově stejný - byl by to přepis, ne sloučení. Společným tvarem je výstup agendy.
- **Pravidlo druhého použití:** kód jde do pomocné třídy, až když ho potřebuje druhá agenda; do té doby zůstává v agendě.
  Žádná předčasná extrakce „pro jistotu" - proto se s `B56` čekalo, až budou hotové čtyři agendy JŘ (Jan 30. 10.).
- **Proč se to vyplatí** (měření 30. 10., `legacy-nahrada.md §4`): opsaného kódu je jen asi 8 %, ale za pět měsíců
  se 42× táž změna dělala ve dvou agendách a 56× se změnila jen jedna kopie. Cílem je udržitelnost s dalšími agendami,
  ne méně řádků.
- **Cíl `B56` není sjednotit všechno, co jde.** Cíl je ujasnit a zapsat strukturu, projít agendy a dostat
  je na základ, na kterém se další agenda staví bez opisování. Sjednocuje se tam, kde hrozí rozjezd, který
  uživatel uvidí, nebo kde se tatáž oprava dělala víckrát. Vědomé rozdíly mezi agendami
  ([`legacy-nahrada.md §4`](legacy-nahrada.md)) zůstávají.
- Postup a pojistky: [`legacy-nahrada.md §4`](legacy-nahrada.md) („Postup `B56`") - výstup všech agend musí
  po každém kroku vyjít obsahově stejně (snímek porovnává stránky jako JSON, pořadí položek jen hlásí).
  Vědomá změna výstupu jen s rozhodnutím Jana a v samostatném zápisu (titulek a typ čísla linky v S7).

**Kdy nejpozději.** Před další agendou (relace, vozový JŘ, převod Report generátoru).

---

## R9. Sdílení šablon mezi firmami (`B189`) - rozhodnuto 3. 11. 2026, zapnuto všem 4. 11.

**Cesta.** Obecné sdílení dat mezi firmami EDISONu v ISYCloudu (edwcore `apps/isycloud/docs/sdileni/api-edison.md`):
zásilka od firmy pro 1 až 100 firem se soubory, stav u každého příjemce, e-mail o přijetí, vlákno v portálu.
Vlastní kanál návrhář nestaví. Sdílení firmě povoluje administrátor ISYCloudu; bez povolení firma nic neodešle ani nepřijme.

**Rozhodnuto (Jan 3. 11.):**
- Posílají se jen **vlastní šablony firmy** (šablony M-line mají všichni), víc najednou jako jedna zásilka:
  výběr v okně šablon, každá šablona jeden soubor JSON ve tvaru exportu. API dovolí 20 souborů v zásilce.
- **Hodnoty platné jen ve firmě** (vybrané typy provozu, skupiny linek…) se při odeslání vymažou a u příjemce
  platí výchozí, stejně jako u šablon M-line (`B191f`).
- **Přijaté šablony jsou v okně šablon** (uzel Přijaté šablony, řádek = zásilka). Když firmě něco přijde, hlavní okno
  ukáže pruh s tlačítkem Zobrazit (okno šablon v Přijatých) a křížkem. Pruh je výrazný (modrý, tučný text, plné
  tlačítko), protože zavřít ho jde vždy jedním klikem (Jan 3. 11.). Křížek i Zobrazit ho schovají a uživatel si pro
  firmu uloží čas nejnovější viděné zásilky (`TdPreferencesService.ReceivedSeen`): pruh se mu ukáže znovu jen kvůli
  novější zásilce nebo nové verzi, i po restartu; kolegovi, který ho nezavřel, ne (Jan 4. 11.).
- **Zásilka se převezme nebo odmítne celá** (Jan 3. 11.; API odmítne jen celou): akce Převzít a Odmítnout u řádku
  zásilky. Šablona, kterou převzít nejde, zastaví převzetí celé zásilky dřív, než se cokoli uloží.
- **Víc zásilek naráz přes zaškrtávání** (Jan 3. 11., po vyzkoušení po jedné): Převzít a Odmítnout platí pro
  zaškrtnuté, bez zaškrtnutí pro vybranou. Zásilky se převezmou po sobě a nakonec jedna zpráva řekne, co se převzalo
  a které nešly; nepřevzatelná nezastaví ostatní, Storno u otázky na přepsání zastaví zbylé. Po sobě proto, že obsah
  další zásilky se čte až po převzetí předchozí, takže dvě verze téže šablony nezaloží dvě nové šablony.
- **Převzatá šablona je vlastní šablonou firmy** (nové Id) a pamatuje si původ: firmu odesílatele a Id šablony u ní.
  Přijde-li šablona téhož původu znovu, okno nabídne aktualizaci; u převzaté, kterou firma mezitím upravila,
  se zeptá, zda přepsat, nebo uložit jako novou. Vazba nejde přes `externiId` - to platí pro celou zásilku.
  Upravená = od převzetí uložená nebo přejmenovaná (značka `PuvodBezUprav`, `API.Sablony.WriteMetadata` ji maže).
  Uložení jako nové přenese původ na novou kopii a dřívější převzatá je dál obyčejnou vlastní šablonou, aktualizace
  pak míří vždy na jednu šablonu (volba při implementaci 3. 11., Jan může vetovat).
- **Převzít jde jen šablonu agendy, kterou příjemce má, a formátu, který jeho návrhář umí**; jinak hláška.
- **Firma bez povoleného sdílení:** akce zůstane vidět, po kliknutí řekne, že sdílení povoluje podpora M-line.
- **Návrhář jen odesílá a přijímá.** Kdo komu co poslal a kdo převzal, ukazuje portál ISYCloudu.

**Typ dat `SABLONA`** si přidala agenda sama migrací v edwcore (`TypSablona`, 3. 11., každá agenda si typ dělá sama):
bez kontroly obsahu, atribut `sablony` = názvy pro portál, `verzeStruktury` 1 (verze formátu je uvnitř JSON).
Typ `SOUBOR` ne: typ zásilky je filtr příjmu, se `SOUBOR` by se šablony míchaly s PDF.
**Klient API běží v Caché** (`Sdileni.Klient`), ne v návrháři: API chce klíč firmy na serveru. Klíč leží
v `^["MLINE"]ALVA.SdileniDat`, protože ISYCloud vydá firmě jediný klíč pro všechny agendy (převezme ho ISYJR).
Vývojový server bez nastavené adresy ostrý ISYCloud nevolá - registroval by tam firmy z DEV dat.
**Testuje se proti testovacímu ISYCloudu na Railway** (`https://mline-isycloud-test.up.railway.app`, od 3. 11.): DEV
server nenaváže spojení na počítač připojený přes VPN. Adresu má DEV server nastavenou (`Sdileni.Klient.SetAdresa`),
firmy CB_CSADAD a CB_DP jsou v testu založené přes správní API (test MLWebu zápisy firem blokuje) a mají povolené sdílení.
Příjem si o šablonách zásilky drží název, agendu, verzi a Id u odesílatele (`PRIJATE`), ať se soubory nestahují při
každém zobrazení seznamu.

Sdílení uvnitř firmy (práva k šablonám, osobní šablony) se zatím neřeší (Jan 4. 11.).

---

## Souhrn - tahák

| # | Rozhodnutí jednou větou | Kdy nejpozději |
|---|---|---|
| R1 | Řádkový tabulkový layout se píše jednou (sdílené jádro), elementy dodávají jen odlišnosti | design LineTableElementu (linkové a knižní JŘ) |
| R2 | Sepsat, co v layoutu patří enginu, co elementu, a jak si předávají „nevešel jsem se" | před linkovými a knižními JŘ |
| R3 | Kontrakt agend: schválený seznam prezentačních nápověd + jen aditivní změny | před F2 (RG bridge rozhodnutí) |
| R4 | Headless tisk: pilot na stanici, ale render oddělit fasádou → později služba | návrh bloku A |
| R5 | Fonty přibalit do aplikace, nikdy nespoléhat na instalaci ve Windows | start linkových a knižních JŘ |
| R6 | Systémové šablony jdou se .exe, DB jen pro zákaznické | návrh bloku B |
| R7 | Jedna agenda k reálnému zákazníkovi dřív, než se migrují další | teď (další velký blok) |
| R8 | Agendy dědí jen kontrakt, sdílený kód v pomocných třídách podle účelu; sjednotit jen tam, kde hrozí rozjezd | před další agendou |

**Nit, která to spojuje:** engine se má měnit vzácně a vědomě (R1/R2/R4/R5 ho k tomu
připravují), agendy se mají přidávat levně a bezpečně (R3/R7), a šablony se mají šířit
bez verzního chaosu (R6). Když tyhle hranice udržíme, „postupné přendávání" zůstane
lineární práce - přesně to, co legacy nedokázalo.


<!-- ══════════════════════════════════════════════════════════════════════ -->
# ==== ČÁST: R2 detail - Partial-fit kontrakt (element ↔ stránkovací engine) ====
<!-- ══════════════════════════════════════════════════════════════════════ -->

# R2 - Partial-fit kontrakt (element ↔ stránkovací engine): ROZHODNUTO

> **Datum:** 2026-07-05. Rozhoduje rozhraní mezi tabulkovým elementem a `PageLayoutEngine` pro
> stránkování dlouhého obsahu přes stránky. Konkrétní konzument: knižní/linkový JŘ (dlouhý seznam
> zastávek) + dlouhé faktury/reporty. **Tento dokument rozhoduje KONTRAKT, ne implementaci** -
> implementace patří k pagination/bin-packing tracku (viz §6). Gate: architektonické rozhodnutí R2.

## 0. Model (potvrzeno)

- **Element** rozhoduje jen lokálně o svém vnitřku (kolik řádků, jak vysoké, kde je nedělitelná
  jednotka) a **hlásí nahoru**: „umím vykreslit řádky `start`..`end`, tady je jejich výška".
- **Globální vrstva** (`PageLayoutEngine`) skládá sekce/stránky: ptá se elementu „kolik se vejde do
  zbývajícího místa", umístí okno, na další stránce pokračuje od dalšího řádku.

## 1. Jak se dělí DNES (a proč to nestačí)

`PageLayoutEngine` (ř. 287-336): u `SplitAcrossPages`/`AllowSplit` spočítá **celkovou výšku sekce**
a nakrájí ji na mm-pásy (`chunk`) dle zbytku místa; každý pás = `PlacedSection` s `ClipTop`/`ClipBottom`.
Renderer nakreslí celou sekci a **ořízne ji na ten mm-pás**. → Dělení je **pixelové** - uřízne řádek
i řádek textu vejpůl na hraně stránky, záhlaví se neopakuje. Pro spojitý text OK, pro tabulku špatně.

## 2. Dvě osy stránkování - R2 řeší JEN svislou

| Osa | Problém | Kde | Tento kontrakt |
|---|---|---|---|
| **Svislá (řádky)** | dlouhý seznam řádků/zastávek se nevejde na výšku | dlouhá faktura, knižní JŘ, StopList | ✅ **R2 (tady)** |
| **Vodorovná (sloupce)** | moc sloupců (159 spojů) se nevejde na šířku → krájet bloky sloupců + opakovat popisové | knižní/linkový JŘ (`Strih`) | ❌ samostatné, **C3 bin-packing** |

Column-axis (Strih) je jiný problém (opakuj fixní sloupce + porce datových) a řeší se vlastním
kontraktem v C3. R2 se ho nedotýká.

## 3. Kontrakt - rozhraní (rozhodnuto)

Volitelná schopnost na řádkových tabulkových elementech (StopList/DynamicTable/LineTable):

```csharp
/// Element, který umí být stránkován po řádcích (svislá osa). Engine ho řídí místo pixel-ořezu.
public interface IRowPaginatedElement
{
    /// Počet nedělitelných řádkových jednotek. „Jednotka" definuje element:
    ///   StopList = 1 zastávka; DynamicTable = 1 datový řádek; LineTable = zastávka + její
    ///   vložené přípojné podřádky (drží pohromadě, nikdy se nerozdělí).
    int RowUnitCount(DataContext? data);

    /// Výška opakovaného záhlaví v mm (nadpisy sloupců + případný pás skupin dnů).
    /// 0 = element záhlaví neopakuje.
    float HeaderBandHeightMm(DataContext? data);

    /// Výška jedné řádkové jednotky v mm (element ji už umí - z fontu / pevná).
    float RowUnitHeightMm(DataContext? data, int unitIndex);

    /// Vykreslí okno jednotek [startUnit, endUnitExclusive). drawHeader = má-li se navrch
    /// zopakovat záhlaví (na každé pokračovací stránce true).
    void RenderRowWindow(RenderContext ctx, int startUnit, int endUnitExclusive, bool drawHeader);
}
```

Měřicí metody vracejí **mm** (žádný SkiaSharp) → engine zůstává čistá logika (jako dnes
`IElementHeightCalculator`).

## 4. Jak to engine řídí (rozhodnuto)

Pro sekci s `OverflowBehavior = SplitAcrossPages`, jejíž obsah je **právě jeden** `IRowPaginatedElement`
vyplňující sekci (typický dlouhý JŘ / faktura):

1. `header = HeaderBandHeightMm`; greedy plň jednotky od `startUnit`, dokud se `header + Σ výšek` vejde
   do `spaceOnPage`. Alespoň jedna jednotka na stránku (i když přeteče - jinak nekonečná smyčka).
2. Umísti `PlacedSection` s novými poli `StartRowUnit`/`EndRowUnit`/`RepeatHeader` (místo `ClipTop`/`ClipBottom`).
3. Zbývají-li jednotky → nová stránka, `startUnit = end`, `RepeatHeader = true`, opakuj.
4. Renderer u takové `PlacedSection` volá `RenderRowWindow(start, end, RepeatHeader)` místo pixel-ořezu.

**Fallback:** sekce, která NENÍ jeden řádkový element (víc elementů, spojitý text…) → **beze změny**,
dnešní pixel-ořez (`ClipTop`/`ClipBottom`). Kontrakt je aditivní, nic nerozbíjí.

## 5. Rozhodnuté detaily

- **Nedělitelná jednotka = volba elementu.** Knižní JŘ: zastávka + její přípojné podřádky = 1 jednotka
  (nikdy se nerozdělí přes stránku). Engine dovnitř jednotky nezasahuje.
- **Záhlaví se opakuje** na každé pokračovací stránce (`RepeatHeader`). Zap/vyp = property elementu
  (default zap pro tabulky). Pás skupin dnů (LineTable) je součást header-bandu.
- **Souhrnné řádky** (DynamicTable summary) = trailing jednotky; drží se u konce, engine je nechá
  „přetéct" s posledním blokem (neosiří na samostatné stránce bez dat - v1 stačí je počítat jako
  běžné jednotky na konci).
- **Element bez `IRowPaginatedElement`** = dnešní chování (pixel-ořez). Migrace elementů je postupná.

## 6. Implementace - kdy

**Kontrakt je rozhodnut; implementace NEteď.** Důvody:
- Pro knižní JŘ je palčivější **vodorovná** osa (C3 bin-packing), ne svislá - sám row-pagination mu
  moc nepomůže, dokud se neřeší sloupce.
- Nejsilnější konzument svislé osy je **dlouhá faktura/report** (tabulkové prvky, report generátor).
- Implementace = rozšíření `PageLayoutEngine` (nová větev split) + `LayoutResult` (řádková okna místo
  clip) + `RenderRowWindow` na 3 elementech + testy. Patří k paginačnímu/bin-packing tracku, ať se
  svislá i vodorovná osa navrhnou a otestují spolu na knižním JŘ.

Do té doby: dnešní pixel-ořez zůstává funkční fallback. Kontrakt výše je závazný pro tu implementaci.



<!-- ══════════════════════════════════════════════════════════════════════ -->
# ==== ČÁST: R6 detail - Governance a distribuce šablon (koncept) ====
<!-- ══════════════════════════════════════════════════════════════════════ -->

# Governance a distribuce šablon - koncept (2026-07-07)

> Model pro hybrid: **M-line distribuuje systémové šablony** + **zákazníci tvoří vlastní**. Koncept
> odsouhlasen 2026-07-07 (Jan). Odvozeno z legacy REPORTGENERATOR („MlineSestava"), modernizováno pro
> JSON/GUID svět TD. Toto je **návrh (krok 1 governance z roadmapy)** - implementace až se M-line
> distribuce reálně spustí. Legacy detail viz [`legacy-nahrada.md`](legacy-nahrada.md).

## Mentální model - dvě zásuvky v jednom globálu

Šablony žijí v `^ALVA.TemplateDesigner.Templates(IdFa, ...)`. Governance = dvě větve dle IdFa:

- **Zásuvka `MLINE`** (IdFa = `"MLINE"`) - **naše systémové šablony.** Jedna sdílená sada pro všechny
  firmy (ne kopírovaná do každé). Zákazník **read-only** - vidí, otevře, tiskne; needituje/nemaže.
- **Zásuvka firmy** (IdFa = reálná firma, např. `CB_DP`) - **zákaznické šablony.** Soukromé per firma,
  plně editovatelné.

Picker („Šablony v databázi") **spojí obě větve**: nahoře systémové (odznak „od M-line", zamčené),
pod tím vlastní (editovatelné).

## Životní cyklus systémové šablony

1. **Autorování (u nás).** M-line návrhář otevře TD na **dev/master** systému „jako MLINE" a šablonu
   navrhne + uloží → jde do MLINE zásuvky masteru. (Master = náš dev/release; **rozhodnutí 2026-07-07:
   zatím žádný separátní master systém, stačí dev/release.**)
2. **Zabalení do release.** Obsah MLINE zásuvky se vyexportuje jako **JSON** a přibalí do release
   (stejnou cestou jako Caché třídy - žádný zvláštní kanál, **ne** legacy GOF/SMB).
3. **Nasazení (u zákazníka).** Release naleje naše šablony do zákazníkovy **MLINE zásuvky**.
   **Klíčové pravidlo: aktualizace sáhne JEN na větev `MLINE`, nikdy na zákaznickou** (izolace při
   update - přebráno z legacy, které se osvědčilo).
4. **Oprava a vyřazení (3. 11. 2026).** Změněný soubor šablony seed u zákazníka přepíše. Vyřazená šablona je
   v `SystemTemplateSeeder.Retired`: návrhář ji při startu smaže z MLINE a server ji už nezapíše
   (`API.Sablony.RetireTemplate`); kopie firem zůstanou.

## Zákaznický flow „kopie → úprava"

1. Zákazník otevře systémovou (MLINE) šablonu → **zamčená** (view/tisk, ne přepis).
2. Chce vlastní variantu → **„Uložit jako"** → nová šablona s novým GUID **pod jeho firmou**.
3. Od té chvíle je kopie jeho a nezávislá - příští aktualizace našeho originálu se jí nedotkne.

## Co už existuje vs co je nová práce

| Prvek | Stav |
|---|---|
| „Uložit jako" → nový GUID → zásuvka firmy (`SaveToCacheAsAsync`, `Guid.NewGuid()`) | ✅ existuje |
| Storage per-IdFa | ✅ existuje |
| Picker ukáže i MLINE větev (dnes `ListTemplates` čte jen session IdFa) | 🔧 sjednotit firmu + MLINE |
| Otevřít MLINE šablonu (dnes `LoadTemplate` bere jen session IdFa) | 🔧 zdrojová IdFa |
| **Zámek** MLINE - server odmítne zápis/mazání v MLINE mimo distribuční cestu; Ctrl+S na MLINE → vynutí „Uložit jako" | 🔧 nová kontrola server-side |
| **Distribuce** - export/import MLINE šablon jako JSON v release | 🔧 nová (malá) |

## Přehled / UI komponenta (EDList + EDFilter)

Picker („Šablony v databázi") **už je** `EDList` + `EDFilter` (data přes `ALVA.TemplateDesigner.EDList.Sablony`,
čte `^ALVA.TemplateDesigner.Templates(IdFa,...)` s filtrem agendy + hledáním) - ne monolit v TD. Díky tomu
je to **jedna komponenta, tři použití** dle scope/filtru:

| Použití | Scope | Kde běží |
|---|---|---|
| Zákaznický picker | firma + MLINE | dnes TD dialog; **vložitelné i do EDISON shellu** (bez spuštění celého návrháře) |
| M-line autorování systémových | session = `MLINE` | náš dev - stejný EDList, přihlášeni „jako MLINE" |
| M-line cross-firma admin přehled *(odloženo)* | celý globál + filtr firmy | admin nástroj - **tentýž EDList**, širší scope + sloupec/filtr firmy |

**Extrahovatelnost:** vytažení z TD dialogu do jiného hostu = obalit stejný `EDList`+`EDFilter` jiným
oknem, ne psát znovu (list + filtr + data jsou už oddělené od návrháře). Není to „headless" (pořád UI
seznam), ale reuse mimo celý TD. Řádek → klik otevře návrhář nad šablonou (stejná UX jako legacy list sestavy).

**Flat list vs strom (rozhodnutí):** legacy měl **strom** (USER/MLINE → oblasti → sestavy) - oblasti byly
uživatelské složky na organizaci **stovek** sestav. My máme **flat list** a horní osy stromu pokrýváme
filtry: firma/MLINE = scope+odznak, modul = AgendaCode. **Flat stačí pro v1** (pár šablon per agenda).
Funkci legacy „oblastí" nahrazuje **filtr + kategorie** (roadmap PC5 „Kategorie + popis" = pole v metadatech,
podle něj EDList grupuje/filtruje) - aditivní, nerozbije nic. **Full folder-tree = YAGNI**, do budoucna lze
z listu udělat složitější hierarchii, až to objem vyžádá.

## Rozhodnutí 2026-07-07

- **Master autorování** = náš dev/release, žádný separátní master systém (zatím).
- **Verzování** = **neřešit zatím.** Naše šablony se při aktualizaci přepíšou celé (jejich se nesáhne).
  „Přišla novější, aktualizovat?" + verze/otisk = až bude potřeba.
- **Read-only** = **vynutit server-side** (na rozdíl od legacy, kde `MlineReadOnly` flag backend nikdy
  nekontroloval - byla to jen klientská iluze).
- **Přehled = flat `EDList`** (ne legacy strom); firma/MLINE + agenda = filtry, legacy „oblasti" →
  kategorie (PC5). Složitější hierarchie později, až to objem vyžádá.

## Vědomě NEkopírovat z legacy

- **GOF export přes `%Library.Global.Export` na SMB share** + staging globály → nahrazeno JSON v release.
- **Fragmentace distribuce do desítek per-agenda tříd** (`ADREPORTGEN.Api` atd.) → jedna cesta.
- **Trik ID `<1000` / `≥1000`** jako kolizní ochrana → v JSON světě řeší GUID/stabilní klíče.
- **Detekce větve podle prefixu stringu** `SestavaId` (`"MLINE-..."`) → čistě přes IdFa subscript.
- **Verzování** - legacy ho nemělo; my ho zatím taky odkládáme (viz výše), ale je to vědomé, ne opomenutí.

## Navazuje (samostatné, ne součást distribuce)

- **Přiřazení šablony k tisku** (roadmap PC „navazující featury"): „linka X → výchozí šablona Y".
  O používání, ne o distribuci - řešit zvlášť. Návrh pravidla výběru šablony je u `B190` v R4.
- **Audit log** (kdo/kdy/co změnil šablonu) + **ECL právo „pouštět designer"** - legacy paralela
  (`^AlvaReportGeneratorLog`, ECL 360080002), mapovat 1:1 až s implementací governance.
