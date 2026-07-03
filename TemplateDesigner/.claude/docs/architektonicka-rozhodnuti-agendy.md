# Architektonická rozhodnutí pro postupné přidávání agend

> **Datum:** 2026-07-02 · **Účel:** srozumitelně (bez zkratek a žargonu) sepsat rozhodnutí,
> která musíme udělat — nebo aspoň mít na paměti — aby model „jeden tiskový engine + postupně
> přidávané agendy" vydržel roky a nerozsypal se. Každé rozhodnutí má: co tím myslíme,
> jaký problém řeší, co se konkrétně rozhoduje, doporučení a termín „nejpozději kdy".
>
> Vzniklo ze strategického review 2026-07-02 (navazuje na
> [`analyza-stav-a-legacy-nahrada-2026-07.md`](analyza-stav-a-legacy-nahrada-2026-07.md)).

---

## 0. Společný slovník: co je „engine" a co je „agenda"

Než k rozhodnutím — ať mluvíme stejným jazykem o základním modelu.

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

1. **Caché třída** (`Agendy/*.cls` dědící `BaseAgenda`) — ví, kde jsou data a jak z nich
   postavit JSON pro tisk (GetSchema/GetData/GetMockData/GetParameters).
2. **Element(y)** — pokud typ sestavy potřebuje nový druh „krabičky" na stránce
   (StopList, DepartureGrid…). Ne každá agenda ho potřebuje — Faktury vystačí
   s Text + Table.
3. **Šablony** — výchozí sady, které dostane zákazník.

**Cíl všech rozhodnutí níže:** aby cena bodů 1–3 pro desátou agendu byla stejná nebo nižší
než pro třetí. Legacy zemřelo přesně na tom, že každý nový typ sestavy znamenal nový
copy-paste render engine (TiskMHD 16k řádků, TiskJR 6,6k řádků, RG 10k řádků kreslení).

---

## R1. Sdílené tabulkové jádro (dřív jsem říkal „table kernel")

**Co tím myslím.** Skoro každý JŘ/RG element je nakonec tabulka: sloupce s hlavičkami,
řádky s buňkami, měření šířek a výšek, kreslení mřížky. Dnes má každý element vlastní
kompletní implementaci tohohle: `StopListElement` (~700 ř.), `DepartureGridElement`
(~1 500 ř.), `TableElement`, teď vzniká `DynamicTableElement`. Sdílíme **styly buněk**
(`CellRenderer` — jak vypadá jedna buňka) a **editory** (`ColumnListEditor`), ale
**ne layout** — tj. logiku „spočítej šířky sloupců, rozděl řádky, změř výšku, vykresli
řádek po řádku". Tu si každý element píše znovu.

„Sdílené jádro" = jedna třída/sada tříd, která umí: model sloupců (šířky, viditelnost,
pořadí), měření (kolik místa řádky zaberou), řádkový layout (kresli řádky odshora, tady
skonči, tolik se nevešlo). Konkrétní elementy by pak dodávaly jen to své: StopList
dekorace (šipka směru, zvýraznění aktuální zastávky), DepartureGrid pivot (hodiny ×
skupiny dnů — ten je jiný, pivot do jádra nepatří), LineTable sloupce spojů.

**Jaký problém to řeší.** Fáze C+D přinese `LineTableElement` (svislý seznam zastávek ×
sloupce spojů = strukturálně kříženec StopListu a TableElementu). Pak relace, vozové JŘ,
RG tabulky s per-řádkovým designem. Bez sdíleného jádra každý = dalších ~1 000+ řádků
layoutu, které se musí zvlášť ladit a zvlášť neúmyslně rozbíjet.

**Co se rozhoduje.** Až se začne navrhovat LineTableElement (Fáze C+D, krok 43):
postavit ho jako pátý samostatný element (rychlejší teď, dražší navždy), NEBO nejdřív
vytáhnout sdílené řádkové jádro z StopList + DynamicTable a LineTable postavit nad ním
(pomalejší teď o ~1–2 týdny, levnější každý další element).

**Doporučení.** Druhá cesta. Podmínky jsou příznivé: budou existovat právě 2–3 konzumenti
(pravidlo „extract až při 2+"), a refaktor existujících elementů kryjí letošní testy.
DepartureGrid do jádra necpat — je to pivot, ne řádková tabulka (viz W4-9 poznámka
v roadmapě: sdílení na úrovni nastavení/dat, ne renderu).

**Nejpozději kdy:** první design LineTableElementu. Do té doby drobnost s velkým efektem:
**vše, co teď vzniká v DynamicTableElement, psát s vědomím, že se to do jádra přesune**
(žádné privátní zkratky provázané s dialogem, čisté oddělení model sloupců ↔ render).

> **✅ Rozhodnuto 2026-07-03 (diskuze):** zárodkem jádra bude **DynamicTableElement** (nejčistší
> řádková tabulka — sloupce × řádky z dat, bez doménových dekorací); extrakce proběhne **až u
> LineTable** — tři konzumenti (DynamicTable + StopList + LineTable) ukážou, co je opravdu
> společné; dřívější extrakce nad dvěma by tvarovala abstrakci podle StopList domény a nesla
> riziko přepisu gold-ověřeného kódu bez okamžitého přínosu. **Akce hned (R1a):** hodinový
> kontrolní průchod DynamicTableElement — čisté rozvrstvení model sloupců / měření / render,
> aby budoucí extrakce byla mechanická. Ujasněno: jádro ≠ PageLayoutEngine (dvě patra —
> sekce/stránky × vnitřek elementu); jádro bydlí v `Rendering/` vedle `CellRenderer`.

---

## R2. Rozdělení layoutových zodpovědností (dřív „layout kompetence")

**Co tím myslím.** V systému jsou dvě místa, která rozhodují, „co bude kde na stránce":

- **PageLayoutEngine** — pracuje se **sekcemi**: která sekce na kterou stránku, rozdělení
  přes stránky, hlavičky/patičky.
- **Elementy** — pracují se svým **vnitřkem**: DepartureGrid si sám zmenší font, když se
  nevejde (`ComputeFontScale`), tabulka si rozvrhne sloupce.

Hranice mezi nimi je dnes jednoduchá: engine se elementu zeptá jen „jak jsi vysoký"
(`IElementHeightCalculator`) a jinak si nerozumí.

**Jaký problém to řeší.** Požadavky z legacy TiskJR a z prezentací padají přesně **mezi**
ty dvě úrovně: (a) skládání bloků vedle sebe i pod sebe podle zbývajícího místa (směr Tam
a Zpět na jedné stránce, když se vejdou), (b) „zbytek legendy dotiskni na další stránce",
(c) zmenšování fontu podle šířky s přepočtem sloupců, (d) imposice 2×A5 na A4 (požadavek
P3). Když se každý vyřeší ad hoc — něco v enginu, něco v elementu, jak zrovna hoří —
vzniknou dva poloviční layout systémy, které si lezou do zelí, a každá další featura bude
vyžadovat znát oba.

**Co se rozhoduje.** Sepsat (stačí 1–2 stránky do `.claude/rules/architecture.md`) tři věci:

1. **Co navždy patří enginu:** stránky, pořadí a dělení sekcí, hlavičky/patičky —
   a nově i imposice (P3), jako samostatná vrstva NAD enginem (bere hotové stránky
   a skládá je na archy), ne uvnitř něj.
2. **Co navždy patří elementu:** vnitřní mřížka, fonty, auto-zmenšování.
3. **Jak spolu mluví:** dnes jen „jak jsi vysoký". Pro „dotisk zbytku" bude potřeba
   bohatší dohoda — element umí říct „vykreslil jsem řádky 1–40, zbývá 12" a engine
   umí říct „pokračuj na další stránce od řádku 41". To je rozšíření rozhraní, které
   se musí navrhnout jednou a pořádně, ne třikrát různě.

**Doporučení + termín.** Napsat před začátkem Fáze C+D (půl dne). Do té doby: žádné nové
layoutové chování nepřidávat do elementů, pokud koncepčně patří enginu (a naopak).

> **✅ Model potvrzen 2026-07-03 (diskuze):** „engine skládá celek (elementy/sekce/stránky),
> element rozhoduje jen lokálně o svém vnitřku a výsledek hlásí nahoru" = přesně dnešní
> architektura, nic se nemění. Z R2 zbývá jediná otevřená věc: **návrh partial-fit rozhraní**
> (rozšíření hlášení z „jsem vysoký X mm" na „vykreslil jsem řádky 1–40, zbývá 12, pokračuj
> odtud") — jednou, jednotně pro všechny tabulkové elementy, před Fází C+D.

---

## R3. Datový kontrakt agend: stabilita + „prezentační nápovědy" (dřív „presentation hints")

**Co tím myslím.** „Kontrakt" = dohodnutý tvar JSONu, který Caché agenda vrací
(GetSchema/GetData/GetParameters — popsáno v `cache-api.md`). Je to jediné hrdlo, kterým
projde každá migrovaná agenda — designer, print engine i šablony vidí agendu jen skrz něj.

„Prezentační nápověda" = případ, kdy data nesou kus VZHLEDU, ne jen obsah. Normálně
platí: data říkají CO (odjezd 14:35), šablona říká JAK (font, barva). Ale někdy vzhled
z dat legitimně vyplývá: odjezd nízkopodlažního spoje má být zelený, mezisoučtový řádek
faktury tučný a opakovaný na další stránce. U nás už to v malém existuje — odjezd nese
`FORE_COLOR`/`BACK_COLOR` — ale jako výjimka, ne pravidlo.

**Jaký problém to řeší.** Celý REPORTGENERATOR je postavený OBRÁCENĚ než my: jeho server
posílá klientovi prezentační příkazy („řádek se zvýrazněním 3, buňka barvy X, přeteklý
text zalom módem 2"). Až budeme migrovat jeho agendy (Fáze F), každá bude potřebovat
nějak vyjádřit „tenhle řádek je zvýrazněný" — a když nebude dohodnutý způsob, každá si
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
rozhodnutím F2 (bridge vs. čisté agendy) — bez toho nejde F2 rozhodnout poučeně, protože
věrnost bridge stojí přesně na tom, kolik prezentace umíme přenést.

> **Upřesněno 2026-07-03 (diskuze):** základ kontraktu UŽ existuje (`cache-api.md` — tvary
> metod, čistý JSON, konvence klíčů; + `EDControls/DOCS/01-wire-formats.md`). Doplnit se mají
> právě dvě sekce: **(a) prezentační nápovědy v1** — princip „volitelný klíč + sémantický kde
> to jde": `ZVYRAZNENI` 0–5 (šablona určí vzhled úrovní), typ řádku (data / mezisoučet /
> nadpis skupiny), „opakuj řádek na další stránce", „text přes celou šířku"; přímé
> `FORE_COLOR`/`BACK_COLOR` legalizovat jako výjimku pro legacy paritu (JŘ je už používá).
> Seznam v1 držet krátký, rozšířit při F1 podle RG usage statistiky. **(b) pravidlo evoluce** —
> kontrakt se mění jen přidáváním klíčů; změna významu / odebrání = zakázáno bez migrace.

---

## R4. Kde poběží tisk bez designeru (headless) + oddělení „PrintEngine"

**Co tím myslím.** „Headless tisk" = vytištění sestavy BEZ otevřeného designeru — zákazník
v EDISONu klikne „vytiskni linku 320001" a dostane PDF. To je položka A3 v roadmapě.
Otázka, kterou roadmapa neřeší: **na kterém počítači ten render běží?**

- **Varianta A — na stanici zákazníka:** EDISON spustí `TemplateDesigner.exe` se speciálními
  parametry (tak to dělá legacy RG: `AUTOEXPORT=1`). Nejmenší práce; ale každá stanice musí
  mít správnou verzi .exe a nainstalované fonty.
- **Varianta B — na serveru:** malá služba (vzor máme — ServiceGtfsRt), která dostane
  „šablona + linka" a vrátí PDF. Jedna instalace, jedna verze, fonty jen na jednom místě;
  víc práce na začátku.

„PrintEngine fasáda" = technická příprava, která drží obě cesty otevřené: dnes umí PDF
vyrobit jen `PreviewCanvas`, což je třída designeru (drží stav editoru). Fasáda = vytáhnout
z ní čistou třídu „dej mi šablonu + data → vrátím PDF", bez vazby na editor. T2 (async
export) už udělalo půlku — render bere šablonu parametrem.

**Co se rozhoduje.** A vs. B pro pilot — a vědomí, že B je pravděpodobný cíl.

**Doporučení + termín.** Pro pilot varianta A (nejkratší cesta k zákazníkovi), ale
**fasádu udělat hned při implementaci A3** (~den práce navíc), takže přechod na B později
= zabalit fasádu do služby, ne přepis. Rozhodnout při návrhu bloku A.

---

## R5. Fonty jako součást aplikace, ne instalace Windows

**Co tím myslím.** Dnes render hledá fonty nainstalované ve Windows; když font chybí,
`FontResolver` potichu použije Arial. „Embedovat" = přibalit soubory fontů přímo do
aplikace (resource v .exe nebo blob v DB) a načítat je z ní — render pak NIKDY nezávisí
na tom, co je na stanici nainstalované.

**Jaký problém to řeší.** U textu je chybějící font kosmetika. U symbolového fontu
**Timetable** (legacy JŘ) je to věcná chyba: znak č. 72 je v tom fontu symbol vozíčkáře;
v Arialu je to písmeno „H". Jízdní řád pak potichu lže — žádná chyba, žádné varování,
jen špatný výstup u zákazníka, který si font nenainstaloval. Legacy to řešilo vynucenou
instalací fontu a kontrolou při startu — to nechceme kopírovat.

**Co se rozhoduje.** Málo co — spíš to prostě udělat: SkiaSharp umí font načíst ze streamu
(`SKTypeface.FromStream`), takže jde o rozšíření `FontResolver` + přibalení TTF souborů
(pozor na licence fontů — Timetable je interní/legacy, ověřit). Souvisí s R4: font musí
být tam, kde běží render.

**Doporučení + termín.** Nejpozději jako první krok Fáze C+D (krok 42b už to v roadmapě
naznačuje); zvážit dřív, pokud pilot ukáže problémy se spec. znaky u MHD.

---

## R6. Systémové šablony distribuovat s verzí aplikace, ne přes databázi

**Co tím myslím.** Blok B počítá se dvěma druhy šablon (podle legacy vzoru): **systémové**
(vyrábí M-line, zákazník je nemůže měnit) a **zákaznické** (vlastní tvorba). Otázka je,
KUDY se systémové dostanou k zákazníkovi.

- **Cesta 1 — přes DB** (legacy model `AktualizujAgendu`): M-line je nahraje do zákazníkovy
  databáze při aktualizaci.
- **Cesta 2 — s aplikací:** systémové šablony jsou přibalené k .exe (soubory vedle/uvnitř,
  read-only), DB slouží JEN zákaznickým.

**Jaký problém to řeší.** T3 správně zavedl: šablonu z novější verze formátu starší
aplikace odmítne. Při cestě 1 tím vzniká „verzní matice" — museli bychom hlídat, že
šablona v9 nejde k zákazníkovi s .exe v8, řešit částečné aktualizace atd. Při cestě 2
matice neexistuje z principu: šablony přišly se stejným buildem, se kterým se otvírají.
Zákazník dostane nové systémové šablony prostě s novou verzí aplikace.

**Doporučení + termín.** Cesta 2. Rozhodnout při návrhu bloku B (B1/B2) — zmenšuje ho
na zlomek (odpadá distribuční pipeline i zamykání v DB; „zamčené" = jsou read-only
soubory). Jediné, co zůstává v DB řešit, je „zákazník si systémovou šablonu zkopíruje
a upraví" — to je obyčejné „Uložit jako".

---

## R7. Pilot na jedné agendě před masovou migrací (zásada, ne kód)

**Co tím myslím.** Než se začnou přendávat další agendy, protáhnout JEDNU hotovou
(zastávkový JŘ) celou cestou až k reálnému zákazníkovi: spuštění z EDISONu (A1/A2),
tisk koncovým uživatelem (A3), výchozí šablony (B2), nasazení .exe i Caché tříd (D3/D4),
podpora. Kandidát: TRANSDEV (nejvíc otevřených tiketů = největší motivace spolupracovat).

**Jaký problém to řeší.** Trubka „EDISON → agenda → šablona → PDF → zákazník" ještě nikdy
neběžela naostro. Každá její vada objevená až při migraci páté agendy se opravuje 5×.
A navíc: zákazníkům byl modul slíben „na polovinu 2026" — ta je teď.

**Doporučení.** Pilot = další velký blok po dokončení rozdělané práce (DynamicTable).
Fáze C+D i F až po něm — poučené tím, co pilot ukáže.

---

## Souhrn — tahák

| # | Rozhodnutí jednou větou | Kdy nejpozději |
|---|---|---|
| R1 | Řádkový tabulkový layout se píše jednou (sdílené jádro), elementy dodávají jen odlišnosti | design LineTableElementu (Fáze C+D) |
| R2 | Sepsat, co v layoutu patří enginu, co elementu, a jak si předávají „nevešel jsem se" | před Fází C+D |
| R3 | Kontrakt agend: schválený seznam prezentačních nápověd + jen aditivní změny | před F2 (RG bridge rozhodnutí) |
| R4 | Headless tisk: pilot na stanici, ale render oddělit fasádou → později služba | návrh bloku A |
| R5 | Fonty přibalit do aplikace, nikdy nespoléhat na instalaci ve Windows | start Fáze C+D |
| R6 | Systémové šablony jdou se .exe, DB jen pro zákaznické | návrh bloku B |
| R7 | Jedna agenda k reálnému zákazníkovi dřív, než se migrují další | teď (další velký blok) |

**Nit, která to spojuje:** engine se má měnit vzácně a vědomě (R1/R2/R4/R5 ho k tomu
připravují), agendy se mají přidávat levně a bezpečně (R3/R7), a šablony se mají šířit
bez verzního chaosu (R6). Když tyhle hranice udržíme, „postupné přendávání" zůstane
lineární práce — přesně to, co legacy nedokázalo.
