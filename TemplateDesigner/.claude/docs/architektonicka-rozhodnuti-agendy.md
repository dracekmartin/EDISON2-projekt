# Architektonická rozhodnutí pro postupné přidávání agend

> **Datum:** 2026-07-02 · **Účel:** srozumitelně (bez zkratek a žargonu) sepsat rozhodnutí,
> která musíme udělat — nebo aspoň mít na paměti — aby model „jeden tiskový engine + postupně
> přidávané agendy" vydržel roky a nerozsypal se. Každé rozhodnutí má: co tím myslíme,
> jaký problém řeší, co se konkrétně rozhoduje, doporučení a termín „nejpozději kdy".
>
> Vzniklo ze strategického review 2026-07-02 (navazuje na
> [`legacy-nahrada.md`](legacy-nahrada.md)).

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


<!-- ══════════════════════════════════════════════════════════════════════ -->
# ==== ČÁST: R2 detail — Partial-fit kontrakt (element ↔ stránkovací engine) ====
<!-- ══════════════════════════════════════════════════════════════════════ -->

# R2 — Partial-fit kontrakt (element ↔ stránkovací engine): ROZHODNUTO

> **Datum:** 2026-07-05. Rozhoduje rozhraní mezi tabulkovým elementem a `PageLayoutEngine` pro
> stránkování dlouhého obsahu přes stránky. Konkrétní konzument: knižní/linkový JŘ (dlouhý seznam
> zastávek) + dlouhé faktury/reporty. **Tento dokument rozhoduje KONTRAKT, ne implementaci** —
> implementace patří k pagination/bin-packing tracku (viz §6). Gate: architektonické rozhodnutí R2.

## 0. Model (potvrzeno)

- **Element** rozhoduje jen lokálně o svém vnitřku (kolik řádků, jak vysoké, kde je nedělitelná
  jednotka) a **hlásí nahoru**: „umím vykreslit řádky `start`..`end`, tady je jejich výška".
- **Globální vrstva** (`PageLayoutEngine`) skládá sekce/stránky: ptá se elementu „kolik se vejde do
  zbývajícího místa", umístí okno, na další stránce pokračuje od dalšího řádku.

## 1. Jak se dělí DNES (a proč to nestačí)

`PageLayoutEngine` (ř. 287-336): u `SplitAcrossPages`/`AllowSplit` spočítá **celkovou výšku sekce**
a nakrájí ji na mm-pásy (`chunk`) dle zbytku místa; každý pás = `PlacedSection` s `ClipTop`/`ClipBottom`.
Renderer nakreslí celou sekci a **ořízne ji na ten mm-pás**. → Dělení je **pixelové** — uřízne řádek
i řádek textu vejpůl na hraně stránky, záhlaví se neopakuje. Pro spojitý text OK, pro tabulku špatně.

## 2. Dvě osy stránkování — R2 řeší JEN svislou

| Osa | Problém | Kde | Tento kontrakt |
|---|---|---|---|
| **Svislá (řádky)** | dlouhý seznam řádků/zastávek se nevejde na výšku | dlouhá faktura, knižní JŘ, StopList | ✅ **R2 (tady)** |
| **Vodorovná (sloupce)** | moc sloupců (159 spojů) se nevejde na šířku → krájet bloky sloupců + opakovat popisové | knižní/linkový JŘ (`Strih`) | ❌ samostatné, **C3 bin-packing** |

Column-axis (Strih) je jiný problém (opakuj fixní sloupce + porce datových) a řeší se vlastním
kontraktem v C3. R2 se ho nedotýká.

## 3. Kontrakt — rozhraní (rozhodnuto)

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

    /// Výška jedné řádkové jednotky v mm (element ji už umí — z fontu / pevná).
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
   do `spaceOnPage`. Alespoň jedna jednotka na stránku (i když přeteče — jinak nekonečná smyčka).
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
  „přetéct" s posledním blokem (neosiří na samostatné stránce bez dat — v1 stačí je počítat jako
  běžné jednotky na konci).
- **Element bez `IRowPaginatedElement`** = dnešní chování (pixel-ořez). Migrace elementů je postupná.

## 6. Implementace — kdy

**Kontrakt je rozhodnut; implementace NEteď.** Důvody:
- Pro knižní JŘ je palčivější **vodorovná** osa (C3 bin-packing), ne svislá — sám row-pagination mu
  moc nepomůže, dokud se neřeší sloupce.
- Nejsilnější konzument svislé osy je **dlouhá faktura/report** (Fáze B/F).
- Implementace = rozšíření `PageLayoutEngine` (nová větev split) + `LayoutResult` (řádková okna místo
  clip) + `RenderRowWindow` na 3 elementech + testy. Patří k paginačnímu/bin-packing tracku, ať se
  svislá i vodorovná osa navrhnou a otestují spolu na knižním JŘ.

Do té doby: dnešní pixel-ořez zůstává funkční fallback. Kontrakt výše je závazný pro tu implementaci.



<!-- ══════════════════════════════════════════════════════════════════════ -->
# ==== ČÁST: R6 detail — Governance a distribuce šablon (koncept) ====
<!-- ══════════════════════════════════════════════════════════════════════ -->

# Governance a distribuce šablon — koncept (2026-07-07)

> Model pro hybrid: **M-line distribuuje systémové šablony** + **zákazníci tvoří vlastní**. Koncept
> odsouhlasen 2026-07-07 (Jan). Odvozeno z legacy REPORTGENERATOR („MlineSestava"), modernizováno pro
> JSON/GUID svět TD. Toto je **návrh (krok 1 governance z roadmapy)** — implementace až se M-line
> distribuce reálně spustí. Legacy detail viz [`legacy-nahrada.md`](legacy-nahrada.md).

## Mentální model — dvě zásuvky v jednom globálu

Šablony žijí v `^ALVA.TemplateDesigner.Templates(IdFa, ...)`. Governance = dvě větve dle IdFa:

- **Zásuvka `MLINE`** (IdFa = `"MLINE"`) — **naše systémové šablony.** Jedna sdílená sada pro všechny
  firmy (ne kopírovaná do každé). Zákazník **read-only** — vidí, otevře, tiskne; needituje/nemaže.
- **Zásuvka firmy** (IdFa = reálná firma, např. `CB_DP`) — **zákaznické šablony.** Soukromé per firma,
  plně editovatelné.

Picker („Šablony v databázi") **spojí obě větve**: nahoře systémové (odznak „od M-line", zamčené),
pod tím vlastní (editovatelné).

## Životní cyklus systémové šablony

1. **Autorování (u nás).** M-line návrhář otevře TD na **dev/master** systému „jako MLINE" a šablonu
   navrhne + uloží → jde do MLINE zásuvky masteru. (Master = náš dev/release; **rozhodnutí 2026-07-07:
   zatím žádný separátní master systém, stačí dev/release.**)
2. **Zabalení do release.** Obsah MLINE zásuvky se vyexportuje jako **JSON** a přibalí do release
   (stejnou cestou jako Caché třídy — žádný zvláštní kanál, **ne** legacy GOF/SMB).
3. **Nasazení (u zákazníka).** Release naleje naše šablony do zákazníkovy **MLINE zásuvky**.
   **Klíčové pravidlo: aktualizace sáhne JEN na větev `MLINE`, nikdy na zákaznickou** (izolace při
   update — přebráno z legacy, které se osvědčilo).

## Zákaznický flow „kopie → úprava"

1. Zákazník otevře systémovou (MLINE) šablonu → **zamčená** (view/tisk, ne přepis).
2. Chce vlastní variantu → **„Uložit jako"** → nová šablona s novým GUID **pod jeho firmou**.
3. Od té chvíle je kopie jeho a nezávislá — příští aktualizace našeho originálu se jí nedotkne.

## Co už existuje vs co je nová práce

| Prvek | Stav |
|---|---|
| „Uložit jako" → nový GUID → zásuvka firmy (`SaveToCacheAsAsync`, `Guid.NewGuid()`) | ✅ existuje |
| Storage per-IdFa | ✅ existuje |
| Picker ukáže i MLINE větev (dnes `ListTemplates` čte jen session IdFa) | 🔧 sjednotit firmu + MLINE |
| Otevřít MLINE šablonu (dnes `LoadTemplate` bere jen session IdFa) | 🔧 zdrojová IdFa |
| **Zámek** MLINE — server odmítne zápis/mazání v MLINE mimo distribuční cestu; Ctrl+S na MLINE → vynutí „Uložit jako" | 🔧 nová kontrola server-side |
| **Distribuce** — export/import MLINE šablon jako JSON v release | 🔧 nová (malá) |

## Přehled / UI komponenta (EDList + EDFilter)

Picker („Šablony v databázi") **už je** `EDList` + `EDFilter` (data přes `ALVA.TemplateDesigner.EDList.Sablony`,
čte `^ALVA.TemplateDesigner.Templates(IdFa,...)` s filtrem agendy + hledáním) — ne monolit v TD. Díky tomu
je to **jedna komponenta, tři použití** dle scope/filtru:

| Použití | Scope | Kde běží |
|---|---|---|
| Zákaznický picker | firma + MLINE | dnes TD dialog; **vložitelné i do EDISON shellu** (bez spuštění celého návrháře) |
| M-line autorování systémových | session = `MLINE` | náš dev — stejný EDList, přihlášeni „jako MLINE" |
| M-line cross-firma admin přehled *(odloženo)* | celý globál + filtr firmy | admin nástroj — **tentýž EDList**, širší scope + sloupec/filtr firmy |

**Extrahovatelnost:** vytažení z TD dialogu do jiného hostu = obalit stejný `EDList`+`EDFilter` jiným
oknem, ne psát znovu (list + filtr + data jsou už oddělené od návrháře). Není to „headless" (pořád UI
seznam), ale reuse mimo celý TD. Řádek → klik otevře návrhář nad šablonou (stejná UX jako legacy list sestavy).

**Flat list vs strom (rozhodnutí):** legacy měl **strom** (USER/MLINE → oblasti → sestavy) — oblasti byly
uživatelské složky na organizaci **stovek** sestav. My máme **flat list** a horní osy stromu pokrýváme
filtry: firma/MLINE = scope+odznak, modul = AgendaCode. **Flat stačí pro v1** (pár šablon per agenda).
Funkci legacy „oblastí" nahrazuje **filtr + kategorie** (roadmap PC5 „Kategorie + popis" = pole v metadatech,
podle něj EDList grupuje/filtruje) — aditivní, nerozbije nic. **Full folder-tree = YAGNI**, do budoucna lze
z listu udělat složitější hierarchii, až to objem vyžádá.

## Rozhodnutí 2026-07-07

- **Master autorování** = náš dev/release, žádný separátní master systém (zatím).
- **Verzování** = **neřešit zatím.** Naše šablony se při aktualizaci přepíšou celé (jejich se nesáhne).
  „Přišla novější, aktualizovat?" + verze/otisk = až bude potřeba.
- **Read-only** = **vynutit server-side** (na rozdíl od legacy, kde `MlineReadOnly` flag backend nikdy
  nekontroloval — byla to jen klientská iluze).
- **Přehled = flat `EDList`** (ne legacy strom); firma/MLINE + agenda = filtry, legacy „oblasti" →
  kategorie (PC5). Složitější hierarchie později, až to objem vyžádá.

## Vědomě NEkopírovat z legacy

- **GOF export přes `%Library.Global.Export` na SMB share** + staging globály → nahrazeno JSON v release.
- **Fragmentace distribuce do desítek per-agenda tříd** (`ADREPORTGEN.Api` atd.) → jedna cesta.
- **Trik ID `<1000` / `≥1000`** jako kolizní ochrana → v JSON světě řeší GUID/stabilní klíče.
- **Detekce větve podle prefixu stringu** `SestavaId` (`"MLINE-..."`) → čistě přes IdFa subscript.
- **Verzování** — legacy ho nemělo; my ho zatím taky odkládáme (viz výše), ale je to vědomé, ne opomenutí.

## Navazuje (samostatné, ne součást distribuce)

- **Přiřazení šablony k tisku** (roadmap PC „navazující featury"): „linka X → výchozí šablona Y".
  O používání, ne o distribuci — řešit zvlášť.
- **Audit log** (kdo/kdy/co změnil šablonu) + **ECL právo „pouštět designer"** — legacy paralela
  (`^AlvaReportGeneratorLog`, ECL 360080002), mapovat 1:1 až s implementací governance.
