# Roadmap

> **Stav k 2026-07-31** (předchozí kolo dojeté - #7947, #7955, #7959, #13048 i #10660 zavřené;
> **30. 7. přišlo nové kolo zpětné vazby KOVED/IDZK** - 9 tiketů, viz Vlna 0).
> **Jen otevřená / akční práce.** Cokoli hotového → [`roadmap-archive.md`](roadmap-archive.md),
> průběžný changelog → [`changelog.md`](changelog.md). Hotové věci sem nevracet - patří do archivu.

> **▶ Další session:** **B40** z Vlny 1 (jednotné čáry a mřížka tabulek, M) - Vlna 0 je dojetá,
> jediný její zbytek **B37 je zablokovaný** odpovědí v #13093 (překryv s #13102 u Petra).
> `B28`-`B34`, `B38` a `B41` hotové 31. 7., `B35a`-`B35c`, `B36`, `B43`, `B45` a bod 2 z `B37` 3. 8.;
> **#13048 i #13088 zavřené 3. 8.** Linkový JŘ (**B10**, **B13**) jde až po zákaznické frontě.
> Follow-upy „na vyžádání zákazníka" žijí
> v [`customer-requirements §0.4`](customer-requirements-zastavkovy-jr.md).

---

## 🧭 Jak podle roadmapy pracovat

Práce je rozřezaná na **bloky velikosti „jedno sezení"** (`B10`, `B13`, …) seřazené do **vln**.
Velikosti: **S** = do hodiny · **M** = půl dne · **L** = víc dní.

**Nová session:** vezmi nejnižší nezablokovaný blok z nejnižší nedokončené vlny. Po dokončení:
changelog + přesun bloku **kompletně** do archivu (ne odškrtnutý řádek zde).

**Pravidla, aby roadmapa znovu nenarostla:**
0. **Číslo bloku je globální a nikdy se nerecykluje** - před založením grepni `B<č>` v roadmapě
   **i v archivu** (jinak vznikne druhý „B18“ a review se rozejde s tím, co kdo myslel).
1. **Nic nevstoupí bez velikosti a čísla bloku** - volný text bez velikosti je přesně to, co se nahromadilo.
2. **Zákaznická položka má uzavírací podmínku** („co ověřit, aby se dala zavřít") - jinak zůstane „hotová, ale otevřená".
3. **Linkový JŘ má ukazatel `L‑1…L‑4`** (Fáze C+D) - velký kus rozřezaný na slotové kroky, aby v každé vlně postoupil.

---

## 🌊 Vlna 0 - zákaznická fronta KOVED/IDZK (kolo 30. 7. 2026)

KOVED (IDZK, Lábsky) začal vývěsky kreslit v návrháři naostro a poslal zpětnou vazbu po blocích.
Většina je doladění toho, co už tiskneme (vzhled, tvar textu, chybějící pole); dva body jsou velké
koncepty a leží ve Vlně 3. Řazeno **od nejrychleji zavíratelného** - cíl je zmenšit počet tiketů,
ne odbavit nejhezčí featuru.

| # | Blok | Vel. | Uzavírací podmínka |
|---|---|---|---|
| **B37** | **#13093 bod 3 - pokračující spoje** (bod 2 hotový 3. 8.). Návaznosti leží v subnode `32` (`m@33#linka:spoj:zastávka:…:dopravce`, u KOVED **10 578 spojů** v celé firmě) a nesou číslo linky i cíl, tedy vše, co klient chce („pokračuje do zast. X (l. 304)", poslední trojčíslí linky). Práce = seskupit podle (cíl, linka) místo podle čísla spoje + přepínač zobrazení. ⚠️ Neplest s dnešním textem „jede až do zast. X" - ten je o prodloužení po **vlastní** lince (#7947), kdežto tohle je pokračování **na jinou** linku. 🛑 **Než se to začne dělat, čeká odpověď v #13093:** stejný příklad (linka 304) řeší **#13102** u Petra - virtuální prodloužení spoje jen pro vývěsku. Kdyby se spoj v JŘ prodloužil, poznámka „pokračuje do…" je zbytečná a udělali bychom dvě řešení téhož. Vhodné jednou potvrdit, že `omzId 32` je to, co uživatel v JŘ vidí jako pokračování spoje. | M | klient si zapne a poznámka odpovídá jejich ruční praxi |

**Otázka na KOVED - odeslaná 3. 8. v odpovědi na #13048, nic neblokuje:** je u nich označník ještě
k něčemu? Kdyby ne, mohl by přepínač „Číslo stanoviště podle" zmizet a zůstat jen sloupek - ale je to
breaking pro zákazníka, který dělení stránek na označník používá (#8828), takže ne bez potvrzení obou.
**Nezeptáno (a nemusí se):** chtěli by **popis sloupku** („směr Brno přes UH")? Data ho nesou, sloupec
je jen schovaný z nabídky - viz rozhodnutí v changelogu 2026-08-03 (2).

**Mimo návrhář (vést jako práci JŘ, u nás jen navázat výstup):** **#13099** souhrnné JŘ (sdružení
linek se stejným koncovým trojčíslím pod vzorovou linku - dnes ručně skriptem v Excelu; pro KOVED
podmínka přesunu tvorby vývěsek do EDISONu; tisk hlavičky dopravců pro souhrnné JŘ je hotový `B33`) · **#13102** virtuální
prodloužení spoje jen pro vývěsku (zadávání v JŘ, přiřazeno Petrovi).
**#13098** je můj sběrný tiket - dnes v něm visí jen odsazení zastávky per směr = `B12` / #7937.

---

## 🌊 Vlna 1 - zákaznické velké + odemknout linkový

| # | Blok | Vel. |
|---|---|---|
| **B40** | **Čáry a mřížka tabulky jako jedno API napříč tabulkovými prvky** (rozhodnuto 31. 7. při review `B30`). Dnes jsou dva nesourodé světy: prvky JŘ mají barvu mřížky + přepínače „které čáry" per prvek, kdežto pevná a dynamická tabulka mají volbu **které** čáry (žádné / vnější / vše / řádky / sloupce), ale **barvu i tloušťku zadrátovanou v kódu** (`0xFFCCCCCC`, 0,5 px) - uživatel je nezmění vůbec. Sjednotit kompozicí (jeden objekt „mřížka": které čáry · barva · tloušťka mm · styl) použitý všemi pěti tabulkovými prvky, jedno místo pro paint. Vedlejší efekt: tabulky dostanou nastavitelnou barvu a tloušťku čar. Migrace stávajících vlastností (`gridColor`, `showRowSeparators`, `borderStyle`) → nový objekt. | M |
| **B10** | **L‑2: symbolový font - linková část** (`ZNAK` sloupce LineTable: multichar/kompozity, převodní tabulka `GSId` 66+ symbolů, TTF v `C:\DEV\EDISON\TiskJR\_ruzne`). Zastávkový JŘ hotový 9. 7. | M→S |
| **B12** | **#7937 rozšíření** - odsazení zastávky per-směr / per-Tč pásmo (`^JRL` dta7 je single-per-point → změna zdroje + editor; base AD hotový). Jen když TRANSDEV tlačí. | M |
| **B18** | **Deklarativní pravidla EDInputFormu** (kapabilita sdílené komponenty, celý EDISON) - dnes umí formulář reagovat na vyplněné hodnoty jen kolem přes server (`Validate`, ručně per formulář). Zavést deklarativní vrstvu: začít **viditelností** (`visibleWhen`), gramatiku sdílet s tiskovými parametry (`POLE=hodnota` + nerovnost) - nezavádět druhý dialekt. Povinnost/povolení až s poptávkou; **skutečná validace zůstává imperativní** (potřebuje data a dopočty). Rozhodnuto 29. 7.: samostatný blok, ne součást B4. **Follow-up:** migrace `CellStyleEditor` na Caché-driven formulář (S–M; layout sjednocen v B6, háček je tri-state / reaktivní logika - bez B18 nejde). | M |
| **B47** | **Drift test na `ServerSideParams` allowlist** (nález review 4. 8.) - už **3. výskyt** téže pasti (parametr čtený v `GetData` chyběl v allowlistu → klik no-op + tisk ≠ náhled; dřív `ROZDELIT_PER_OZNACNIK`, teď `SOUHRN_NA_ZNAMENI` + `SPOJE_U_DOPRAVCE`). Vyparsovat z `.cls` čtení `^\|\|ML("IN","X")`, odečíst infrastrukturní klíče (LINKA_ID, INSTANCE_ID, SKUPINA_LINEK_ID, key, search, limit) a assertovat podmnožinu v allowlistu - stejný vzor jako `EDInputFormSchemaDriftTests`. | S |
| **B48** | **Klasifikace značek podle kategorie i v seznamu zastávek** (nález review 4. 8.) - `StopListElement.IsTransferAttr` pozná přestup podle **písmene** („A"/„L"), stejná třída bugu, kterou `e10ad2b` opravil u anotací mřížky: MHD atribut s kódem 65/76 by se přeložil na cizí piktogram, nebo zmizel pod defaultně vypnutým `ShowTransferAttributes`, zatímco jeho řádek legendy zůstane. Fix = tagovat ATRIBUTY kategorií `prestup` na wire (AD ji u legendy už posílá) a překlad gatovat kategorií; přitom sjednotit i výjimku `SymbolJeText` (x/y/z = text je AD konvence, na MHD znaky bez kategorie nepatří). **Gate před vydáním, které dostane MHD firma:** ověřit v číselníku atributů MHD firem, jestli se kódy 65/76 reálně vyskytují - pak se to stává urgentní. | M |
| **B49** | **SK symbolový font - obrysová verifikace pozic glyfů** (nález review 4. 8.) - mapa `SymbolGlyph` (`z` autobus · `x` vlak · `L` kolo) je změřená jen proti `TimeTable2.ttf`; u `TimeTable2SK.ttf` víme, že `w`=autobus a `x`=moderní jednotka, ale komentář ve `FontResolver.cs` tvrdí „znaky jsou pro CZ i SK stejné" - **jedno z toho je špatně**. Před nasazením slovenskému zákazníkovi zopakovat kontaktní list (postup v `elementy-architektura.md`); pokud se pozice liší, `SymbolGlyph` dostane culture parametr. | S |

---

## 🌊 Vlna 2 - velký kus linkového (dedikovaně)

| # | Blok | Vel. |
|---|---|---|
| **B13** | **L‑4: 2D bin-packing / `Strih`** - široká tabulka (MHD linka = 100+ spojů) se dnes zmáčkne na šířku místo rozřezání do stránkových bloků; + flow paketů, „dotisk zbytku legendy na další stránce" | L |
| **B14** | **Dedup Caché tiskových enginů (C8) + testy tiskových enginů (C9)** - doprovod ostrého knižního JŘ, detail v tabulce Fáze C+D | L + M |

L‑4 stojí na hotovém `B11`: řádková vrstva existuje, takže rozřezání se píše **jednou** nad plánem
řádků, ne pětkrát v per-element smyčkách.

---

## 🌊 Vlna 3 - velké koncepty z kola KOVED

Body z fronty Vlny 0, které nejsou doladění - chtějí vlastní plánování. (`B38` odsud odpadla:
při analýze 31. 7. se ukázalo, že s `B13` nemá nic společného a je to **M** uvnitř prvku - hotová.)

| # | Blok | Vel. | Pozn. |
|---|---|---|---|
| **B39** | **#13045b vlastní skupiny dnů** - dnes pevné pracovní dny / soboty / neděle. Klient chce vlastní skupiny s obdobími („Vánoční provoz 27.-29. 12.", „Prázdniny" s několika rozsahy, „Dny školního vyučování"), spoje se do sloupce vyberou podle období. Mění datový model + chce editor. **Zbytek tiketu po `B29`** - ohlášená chybná značka je opravená 31. 7. (`B29`, archiv), takže část „netiskni značku, kterou už říká nadpis sloupce" je hotová a pro vlastní skupiny se jen musí dodržet i u nich. | L | koncept skupin + editor; pravidlo značek už existuje (`DenniZnacky`) |

---

### 📥 Backlog / budoucí velké tracky

- **Zastávkový JŘ business** (parkované helpdesk A2-A5/A6/B4 + TRANSDEV sada) → viz sekce „Zastávkový JŘ - business logika" níže
- **Fáze B - odložené:** **oživení fixní tabulky** (paleta + Fixed editor) + **merged cells** (gated na ni).
- **Tabulkové featury (bez customer demand):** grouping (`GroupHeader`/`GroupFooter`/`Summary`) ·
  vnořené datasource (`_rows` v `_rows`).
- **`B27` Řazení řádků v dynamické tabulce** (S–M; podnět kolegy, bez zákaznické poptávky) - dnes se
  řádky tisknou v pořadí, v jakém je vrátí agenda; uživatel je nemá jak přeřadit. Rozsah prvního
  kroku: **primární sloupec + volitelně sekundární**, vzestupně/sestupně, jako vlastnost prvku
  (aditivní JSON, bez migrace). Dvě věci rozhodnout dřív než se to začne psát: (1) řadit podle
  **hodnoty pole**, ne podle vykreslené buňky - jinak formát a placeholdery pořadí rozhodí, a typ brát
  ze schématu (číslo/datum/čas ≠ text); (2) **klientsky v prvku**, ne v agendě - serverové řazení by
  se muselo dělat per agenda. Platí jen pro **dynamickou tabulku**; JŘ prvky mají pořadí dané trasou
  a ta se řadit nesmí.
- **Fáze C+D** linkové/knižní JŘ · **Fáze E** vozový + Relace · **Fáze F** REPORTGENERATOR - velké
  tracky, každý gated rozhodnutími R1–R7 (viz [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md))
- **Externí datové zdroje** (analýza 2026-07-12, na poptávku) - architektura připravená (engine zná jen
  `Template + DataContext`; DataContext je zdrojově agnostický JSON). Tři švy: Caché gateway agenda
  (0 změn .NET) · .NET provider vedle Caché (vzor `JsonFileDataSchemaProvider` + Composite) · headless
  A3 („POST šablona+JSON → PDF" = externí zdroj bez adaptéru; **A3 je nejpravděpodobnější nosič**).
  První krok s nejlepším poměrem cena/hodnota: **generický JSON zdroj (soubor/URL)** - tržní minimum
  (CSV/Excel/JSON/REST), náš wire formát už JSON je; využilo by i AI + testování šablon. Dořešit:
  credentials, schéma z dat, změkčení předpokladu „běží nad Caché".
- **`B42` Co všechno umí import z předlohy nastavit** (M, nejdřív analýza; podnět Jan 31. 7. při `B41`).
  Import dnes umí **pozice a prvky** plus část vzhledu (fonty, výšky řádků, barvy záhlaví, sloupce).
  Otevřená otázka: umí z předlohy rozumně odvodit i **parametry sestavy** a **další nastavení prvků**
  (pozice značek, šipka, sloupce jako zóna/označník, varianty tras, skupiny dnů)? Je to velká
  a netriviální část importu, takže se to má projít vědomě, ne po jedné vlastnosti.
  ⚠️ **Klíčové omezení, na které to narazí:** hodnoty parametrů sestavy jsou uložené **per uživatel ×
  agenda**, ne per šablona - kdyby je import nastavil, přepíše nastavení pro všechny ostatní šablony
  té agendy. Buď se tedy musí zavést per-šablonové hodnoty parametrů (velké rozhodnutí, dotýká se
  `ApplyPersistedPrintParamsAsync`), nebo import smí jen **navrhnout** (viz krok níže).
  **Malý krok, který jde udělat hned (S):** detekci časové osy z předlohy vrátit a její výsledek
  **napsat do protokolu importu** („předloha má osu 5-23, nastavte ji v parametrech"), ať se informace
  neztratí a dostane se k tomu, kdo s ní může něco udělat. Původní detekce (`timeAxisRangeFrom/To`
  v `AiGuidedScheduleBuilder`) je v gitu před commitem `B41`.
- **`B44` Nabídka datových polí při psaní `{…}`** (M–L; podnět Jan 3. 8.). Dnes musí uživatel název
  pole znát a napsat ho bez překlepu; nabídka při psaní (jako doplňování příkazů v terminálu) by to
  zásadně zpříjemnila a odstranila tichý překlep v tokenu, který se projeví až v tisku. Týká se
  všech míst, kde se zadávají `{POLE}` (barvy, text prvku, obsah buněk), takže se to má navrhnout
  jednou a sdílet, ne dělat per pole. Zdroj nabídky = schéma agendy, které už klient má.
- **`B51` EDList plní řádky po jednom - škubnutí při psaní** (S; nález Jan 4. 8. na pomalé lince,
  odložené za verzi). `ApplyRows` přidává řádky jeden po druhém do `ObservableCollection` navázané
  na `DataGrid` s filtrem → 200 řádků = 200 oznámení o změně, každé s přepočtem filtru i layoutu,
  a nakonec ještě celý `RefreshFilter()`. Od chvíle, co je načítání async (změna 2026-08-04 (6)),
  to dosedá **zatímco uživatel píše** do filtru skupiny, takže je to znát jako škubání; dřív to bylo
  schované za zámrzem. Fix = odložit přepočet pohledu na dobu plnění (`ICollectionView.DeferRefresh`)
  → jeden průchod místo dvou set. ⚠️ Sdílená komponenta - projeví se u všech konzumentů EDListu
  (i výběr šablon z DB), takže po změně proklikat i EDISON, ne jen návrhář.
- AI (v pozadí) · font warning + ad-hoc overflow (YAGNI)

**Otevřené otázky k Fázi E/F** (Epson, RG bridge, Relace, grafy/AZTEC, jazykové varianty) → evidované
v [`legacy-nahrada.md §7`](legacy-nahrada.md); vyřeší se
při scope té fáze, ne teď.

---

## 🚀 Produkční / provozní - otevřené

1. **Headless tisk (A3)** - „vytiskni linku X" z modulu přes PrintEngine fasádu (šablona+data→PDF bez stavu editoru). Dnes tisk jen v designeru. Gate R4/R7.
2. **Samostatný distribuční kanál** systémových šablon mimo .exe - v1 doručuje embed→MLINE; „jiný kanál" (server push / admin) jen vymění zdroj, picker/UX beze změny.
3. **Další výchozí šablony** (MHD + linkový) = **krok 44b / C4** (linková varianta v L‑tracku Fáze C+D);
   mechanismus embed→MLINE hotový, zbývá kurátorovat + přesunout JSON do `SystemTemplates/`.
4. **Zákaznická dokumentace na wiki (post-release)** - seedy hotové (`uzivatelska-prirucka.md`,
   `plan-a-zmeny.md`); zbývá **obrázky + publikace**.

---

## 🅿️ Zastávkový JŘ - zákaznická fronta

> **Plný stav** (hotové featury + co otestovat pro uzavření helpdesk tiketů) →
> [`customer-requirements-zastavkovy-jr.md §0`](customer-requirements-zastavkovy-jr.md). Zde jen **otevřená
> akční práce**. Vel.: S = do hodiny, M = půl dne, L = víc dní.

### Čeká na někoho jiného - postrčit, není to práce

- **TR2** implementováno (opt-in `SOUHRN_NA_ZNAMENI`), ⚠️ čeká SK ověření kódování na-znamení - kolega
- **#13048 zavřený 3. 8.** (Jan) - vizuálně odsouhlaseny přestupy, sloupek i přepnutí logiky
  stanovišť. V odpovědi jsme se zeptali, **jestli označník ještě k něčemu využívají, nebo se to má
  řídit jen sloupkem** - podle odpovědi se dá překlopit výchozí hodnota „Číslo stanoviště podle"
  (dnes Označník kvůli zákazníkovi, který dělení stránek na označník používá, viz #8828)
- **#13091a `B30` + #12997 `B31` implementováno 31. 7.** - čeká, až to KOVED uvidí ve své vývěsce
  (vzhled seznamu zastávek, barva linky z dat u sloupců)
- **#13091 bod 4 (zóny) `B34` doportováno 31. 7.** - čekají tři odpovědi od KOVED, bez nich to nemá
  smysl dál posouvat: (1) má mít zastávka přednost i tam, kde trasa zónu má? (starý modul dává
  přednost trase - přehození by nás rozešlo s JDF a exporty) · (2) mají u linky zapnuté „Zóny - tisk
  a export do JDF bez písmen"? (bez toho písmeno zůstane) · (3) číslo linky a zastávky, kde to vidí
  špatně - ověří, že jejich kód zóny je „číslo + písmeno", ne jiný identifikátor
- **#12997 druhá barva linky dodělaná 3. 8.** - Petr téhož dne rozšířil JŘ na víc barev na linku,
  my přidali pole `BARVA_LINKY_2`. ⚠️ Čeká jediné: **nikde není linka se dvěma barvami**, takže to
  chce nastavit v JŘ (Hromadné úpravy vlastností linky) a projít očima; pak už je co odpovědět
  zákazníkovi (Petr o odpověď výslovně žádal, viz #12997)
- **#13093 bod 3 (pokračující spoje) = `B37`, čeká odpověď** - dotaz do #13093, jestli to nemá řešit
  **#13102** u Petra (virtuální prodloužení spoje v JŘ). Oba body ukazují na stejný příklad (linka 304);
  prodloužení v JŘ by poznámku „pokračuje do…" udělalo zbytečnou. Body 1, 2 a 4 tiketu jsou hotové,
  takže tiket drží už jen tohle rozhodnutí.
  ⚠️ **Zadarmo to z #13102 nedostaneme a je to dané jejich vlastním zadáním.** Kdyby prodloužení šlo do
  trasy spoje (`^JRL(...,"W",…)`), dopočítá se u nás všechno samo (obsluhované zastávky, varianty,
  odjezdy) - `ObsluhovanePoradi` to čte za běhu. Ale #13102 chce prodloužení **virtuální, jen pro
  vývěsku, bez úniku do jiných výstupů**, takže do trasy zapsat NESMÍ (čtou ji exporty JDF/CIS) →
  poloviny jsou dvě: oni zadávání + úložiště, my přečtení a přiložení k trase. **Naše polovina je malá,
  pokud se dopředu dohodne, kam to zapíšou** - a jejich struktura musí nést **čas u každé přidané
  zastávky** (bez času nejde odjezd nikam postavit). To je věta, která patří do #13102 **teď**, ne až
  po implementaci. Na vývěsce by pak stačil jeden parametr sestavy, prvky se nemění.
- **TR8 („<1" místo „0") park** (analýza 2026-07-10): naše časy jsou kumulativní, 0-segment se tiskne
  jako opakované číslo, ne „0". Per-segment sloupec TD nemá. Blokuje potvrzení od TRANSDEV
  (který sloupec - legacy „Průběhy"?). Detail → `customer-requirements §TR/8`.

### Reálná zbývající práce

`B12` #7937 per-směr / per-Tč odsazení (jen když TRANSDEV tlačí)

**Kolo KOVED z 30. 7. drží Vlna 0** (`B37` čeká na odpověď, `B45`; ostatní hotové) **+ Vlna 3**
(`B39`) - sem se nekopíruje.

### Bez poptávky nebrat - chce analýzu / datový model / business rozhodnutí

| # | Co | Vel. | Co potřebuje |
|---|---|---|---|
| TR10 | Skrýt spoje o So/Ne/svátcích | S–M | Caché filtr |
| TR7 | Per-písmeno výběr sloupců variant jízdních dob | S–M | jen pokud TRANSDEV potvrdí nad novým chováním (varianty dle trasy+nástupiště s max časy jsou od 27. 7. hotové) |
| TR6 | Barva linky (bg/text ve vlastnostech linky → volba v šabloně) | M | nový datový model |
| TR9 | Obrácená logika poznámkování (rozsah TČ v 1. tabulce) | M | Caché-input (Studio JŘ) |
| TR5 | Definice cíle - střední (per zast.×směr) + ideál (DB cílů) | M–L | datový model; *(min „cíl per směr" = hotovo přes `POPIS_SMER`=konečná)* |

*(Follow-upy uzavřených tiketů „na vyžádání zákazníka" → [`customer-requirements §0.4`](customer-requirements-zastavkovy-jr.md).)*

---

## 🟠 Fáze B: Element flexibility - jen odložené body

Z gap analýzy vůči komerčním generátorům. Nezávislé na JŘ (faktury i obecné šablony).

*(Nález z 30. 7. o chybějících barvách zón u zastávek a odjezdů byl při `B31` (31. 7.) **vyvrácen** -
pole tam jsou, jen pod jinými popisky; skutečná mezera byl textový zápis barvy a ta je opravená.
Detail v archivu roadmapy.)*

**Fixní (statická) tabulka - implementace odložena na poptávku faktur / F3.** Analýza 2026-07-12
rozhodla „koncept implementovat" (tabulka dává reflow podmíněných řádků, auto výšku řádku a
strukturální editaci, což skupina elementů principiálně neumí). Model i render Fixed módu jsou hotové
a na sdíleném jádru; pro JŘ to potřeba není.

- **Krok 1 - střízlivé oživení (S–M):** paleta „Tabulka (pevná)" + factory (`Mode=Fixed`, Mode v UI
  zamčený - dynamickou dělá `DynamicTableElement`); opravy nálezů: `Clone()` zapomíná `SummaryRows`,
  `RowVM.ToModel()` zahazuje per-cell `Format`+`Style`; render dorovnat na **plán řádků** (`RowSpec`; `B11` ji vědomě vynechal - kreslí svislou čáru za
  každým sloupcem včetně vnějšího a vrství dvě pozadí u pevných řádků, takže sjednocení jí výstup
  změní a jako uspaný element není v odtiscích)
  (sjednotí mřížku, auto-scale, **předpoklad pro merge**); drift-test allowlist zúžit.
- **Krok 2 - UX modernizace + merge (M, s poptávkou):** inline editace buněk na plátně (dvojklik,
  à la F2 na TextElementu - standardní pocit z FastReportu, řeší „5 tabů je moc"), redukce dialogu
  na 2-tab vzor, **ColSpan** merged cells (aditivní JSON, span-aware DrawRow). **RowSpan vědomě
  odložen** (vzácný, těžký - spanování výšek × zebra × VisibilityExpression) - až na poptávku / F3.

---

## 🟠 Fáze C + D: Výlepový/knižní JŘ linky (náhrada TiskJR) - pravděpodobně JEDEN track

> ⚠️ **Gate: R1 (sdílené tabulkové jádro), R2 (layout zodpovědnosti), R5 (embedded fonty)** -
> [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md). Krok 43 začíná
> designem sdíleného jádra (StopList + DynamicTable + LineTable), ne kódem elementu.

Legacy `TiskJR` generuje vývěsní i knižní JŘ z téhož enginu → C + D je nejspíš jeden track. Formát:
sloupcová tabulka celé linky (svislý seznam zastávek × sloupce spojů s časy; řádek platností se
symboly/NZ; Vyčká/Navazuje; skupinová záhlaví PD/So/Ne; 10 druhů poznámek). **Gold oracle `mkPLinWin`
(ALVA.JRMAIN.MLAPI)**, metodika = čistá reimplementace à la `ZastavkoveJrAd` (~1 měsíc).

**Zbývající prerekvizity:** (1) symbolový font - **linková část** (`GSId` 66+ symbolů na
multichar/kompozity, TTF v `C:\DEV\EDISON\TiskJR\_ruzne`) = `B10`; (2) layout rozšíření (flow paketů,
„dotisk poznámek z minulé stránky") = `B13`; (3) per-linka šablony = `C4`.
Datové jádro (`KnizniJrAd`/`KnizniJrMhd`), `LineTableElement`, plán řádků a auto-fit fontu jsou hotové.

### 🎯 Ukazatel postupu `L‑1…L‑4` - velký kus rozřezaný na slotové kroky

**Smysl:** aby linkový JŘ postoupil v **každé vlně**, i když běží helpdesk. Každý krok končí něčím
dokončeným; velký `L‑4` se otevírá až nad hotovým `L‑3`.

| Krok | Blok | Co | Vel. | Stav |
|---|---|---|---|---|
| **L‑2** | `B10` | Symbolový font - linková část (`ZNAK` sloupce, multichar/kompozity, `GSId` 66+) | M→S | ⬜ |
| **L‑4** | `B13` | 2D bin-packing / `Strih` - rozřezání široké tabulky do stránkových bloků | L | ⬜ **odemčeno** |
| - | `C4` | Linková výchozí šablona do distribuce (dá se přilepit ke kterémukoli slotu) | S | ⬜ |

Odložené body `C2`–`C9` níže jsou detailní evidence týchž kroků (`C2` = L‑2, `C3` = L‑4, `C8`+`C9` = `B14`).

### Odložené body knižního/linkového JŘ (z pilotu, seřazené dle hodnoty pro ostré nasazení)

| # | Co | Proč odloženo / dopad | Odhad |
|---|---|---|---|
| C2 | **Symbolový font - linková část** (gate R5): `ZNAK` sloupce LineTable, multichar/kompozitní symboly, převodní tabulka `GSId` (66+). Zastávkový JŘ hotový. | bez toho knižní/výlepový JŘ „tiše lže" u dopravních symbolů | M→S |
| C3 | **2D bin-packing / `Strih`** (gate R1/R2) - široká tabulka (MHD linka = 100+ spojů) se dnes zmáčkne na šířku místo rozřezání do stránkových bloků; + flow paketů, „dotisk zbytku legendy na další stránce". Staví na hotovém plánu řádků, takže se píše **jednou**. Zastávkový JŘ to nepotřebuje. | nutné pro reálný tisk MHD linek; největší kus | L |
| C4 | **Rozšíření výchozích šablon na další agendy** - mechanismus embed→MLINE hotový (governance v1, zastávková AD); přidat MHD + linkový JSON do `SystemTemplates/` | provozní; malý | S |
| C5 | **Konfigurovatelné pořadí poznámek** (`PoznamkyPoradiDruh`) - legacy AD parametr; TD má pevné pořadí druhů | per-zákaznická variace, řešit až s poptávkou | S |
| C6 | **Turnusy, obousměrný tisk, souhrnné JŘ (řádek zdrojové linky)** - legacy volby `in(arr,6/7)`, MHD `MakeData` je nemá | mimo scope pilotu; validovat, zda je zákazník chce, než portovat | - |
| C7 | **Rozdvojení příjezd/odjezd - jemný layout** + fixed-width padding sloupce (legacy `arr,13`) | kosmetika; TD řeší podřádky, ne padding | S |
| C8 | **Dedup Caché tiskových enginů** (*z auditu H1*) - `CasHodnota` bere `"|"` jinak v `KnizniJrAd` vs `ZastavkoveJrAd`, různé signatury `AddAnotace`/`BuildPoznamka`, ~15 helperů MHD↔AD + AD `^JRL` readery. Vyextrahovat sdílené `^JRL` readery + `Build*` do `BaseAgenda`/`JrShared`. **Caché analog R1 extrakce jádra** (C3 řeší .NET render-side, C8 Caché data-side). | korektnostní riziko; dělat s ostrým knižním JŘ, ne dopředu | L |
| C9 | **Testy Caché tiskových enginů** (*z auditu H2*) - `KnizniJr*` (~1975 ř.) 0 testů, `ZastavkoveJrMhd` 1 helper; AD gold gate tiše projde bez `CB_CSADAD`, křehký digest nad živými daty (pinuje 2/22 linek), `tools/compare-gold.ps1` neexistuje. Smoke + frozen-input gold testy; bezpodmínečný gold gate (zmrazit vstup do izolovaného ns). **= T1b.** | důvěra v datovou paritu; s ostrým knižním JŘ | M |

Detail datových odchylek a mezer viz [`linkovy-knizni-jr-implementace-analyza.md`](linkovy-knizni-jr-implementace-analyza.md) §11–12.

---

## 🟠 Fáze E: Vozový JŘ + Relace

Layout TBD - hlavička oběhu, tabulka spojů, poznámky (pravděpodobně víc elementů). Legacy:
`ALVA.MHD.ReportGenerator` (MHD) + `AD.ADREPORTGEN` (linky), viz
[`legacy-nahrada.md`](legacy-nahrada.md). **Relace (TiskJRRELACE)** - vývěsky
nádraží (SK zákazník, fork TiskJR ~50k LOC); **nejdřív ověřit živý use case (Q3)**.

| Krok | Co | Stav |
|---|---|---|
| 48 | Analýza referenčních vozových JŘ - layout + wire formát | ⬜ |
| 49 | `VehicleScheduleElement` (pravděpodobně víc elementů) | ⬜ |
| 50 | Caché `VozovyJrAPI` - GetData/GetMockData + async (velký objem dat) | ⬜ |

---

## 🟠 Fáze F: Náhrada REPORTGENERATOR (univerzální sestavy)

> ⚠️ **Gate před F2: R3 (prezentační nápovědy v datovém kontraktu + aditivní evoluce)** -
> [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md).

Největší kus náhrady - ne kvůli featurám (TD je napřed), ale kvůli **ekosystému ~28 agend** s RG
kontraktem (`GetSeznamSad`/`GetSadu`/`GetData`, `~`-tokenový stream). Nasazení: 33k textových elementů,
774 tabulek. **Ne big-bang** - etapizovat. Detail:
[`legacy-nahrada.md`](legacy-nahrada.md) §3.1 + §5 etapa 4.

| Krok | Co | Stav |
|---|---|---|
| F1 | **Prioritizace podle usage** - legacy `Main.cls:161-188` drží reálná čísla použití; seřadit agendy | ⬜ první (levný) |
| F2 | **Bridge vs čisté agendy (Q2)** - bridge `LegacyRgAgenda` (překlad `~`-protokolu na TD JSON, všech 28 najednou s omezenou věrností) vs čisté reimplementace; doporučení: **bridge nejdřív** | ⬜ rozhodnutí |
| F3 | **TableElement na RG úroveň** (navazuje na dynamickou tabulku + Fáze B): per-řádek zvýraznění 0-5 z dat, custom barvy per řádek/buňka, overflow módy (zalamování → klonování řádků), opakování mezisoučtu na další stránce, datové typy sloupců (datum/minuty/záporné červeně/skrýt nuly), řádek-jako-layout | ⬜ L |
| F4 | **Výstupní kanály fáze 2** - Excel export, PDF heslo + el. podpis (PDFSIGNER), Epson znakový tisk (Q1) | ⬜ dle poptávky |
| F5 | **Jazykové varianty šablon** - per-jazyk kopie provázaná na CS zdroj (Q5) | ⬜ |
| F6 | **Vědomě neportovat bez poptávky** - grafy (12×), AZTEC (2×), calculated (2× - výrazy TD nadmnožina), checkbox (8×); ověřit + škrtnout (Q4) | ⬜ validace |

---

## 🟡 Import PDF - doladění

| Krok | Co | Stav |
|---|---|---|
| 32 | **Obecný import: detekce tabulek z mřížky čar** - místo 50 čar + 20 textů jeden tabulkový element. ⚠️ **Gated na oživení fixní tabulky (Fáze B)** - cíl importu statické tabulky je Fixed mód, který je dnes uspaný; do té doby nedělat | ⬜ |
| 33 | JŘ import: **ladění na širší sadě JŘ** (jiné layouty než KV_DP vzor - jiná města, SK, netypické legendy) | 🟡 |

Křivky/Bézier v obecném importu - low-prio, bez poptávky.

---

## 🤖 AI integrace - v pozadí

> **Sekundární, ne core.** Aktuální funkce (pomocník, generátor šablon, AI Vision import PDF - vše Sonnet 5
> přes EDISONAI/mlweb spravovaný klíč) zůstávají funkční; další rozšíření až budou reální uživatelé
> s potřebou. JSON šablona + DataSchema jsou stabilní kontrakt pro AI - rozšíření kdykoli bez breaking
> changes. **Princip: copilot, ne autopilot.**

**Otevřené (post-release):** ostrá zkouška EDISONAI cesty bez lokálního klíče u první firmy ·
potvrzení `source: "templatedesigner"` u autora mlwebu · zveřejnění generátoru šablon
(menu dnes jen pro vývojáře) až po otestování na reálných zadáních.

**Eval sada AI pomocníka** (průběžné doškrtání; pomocník je už uživatelsky ověřen, tohle je regresní
sada) - u každé otázky hodnotit: správná cesta v UI / žádné vymyšlené featury / rychlost odpovědi.
- *Lookup:* „dvě vývěsky na A4" → Ctrl+P Uspořádání na list · „linka do vlastního PDF" → Ctrl+P Členění + Název souboru ·
  „stránka na A5" → Ctrl+Shift+P · „kopírovat styl" → Ctrl+Shift+C/V · „sloupec cena×počet" → výraz `{CENA}*{POCET}` ·
  „zvýraznit řádky >1000" → zvýraznění řádků · „víc linek k tisku" → chip Instance · „změnit agendu" → Ctrl+Shift+A ·
  „červený rámeček náhledu" → přetečení obsahu · „součet pod tabulku" → `SUM(...)` · „schovat sekci na 1. str." → VisibilityExpression ·
  „vrátit smazaný prvek" → Ctrl+Z · „písmo celé tabulky" → dialog sloupců · „šablona pro kolegu" → Ctrl+S do DB.
- *Chytáky* (očekává „zatím neumíme", ne vymyšlený návod): sloučení buněk · export do Excelu · PDF e-mailem ·
  brožura/duplex · vlastní font · import staré šablony z EDISONu.
- *Nejistota* (odkázat na F1/podporu, ne halucinovat): „špatný čas u 3. zastávky linky 12" (datový problém, ne UI).

**Budoucí:** MCP server (`get_template`/`apply_template`/`add_element`/… tenká vrstva nad ViewModelem, každý
tool → `_vm.Execute(command)`) · onboarding průvodce · smart suggestions (TableElement → sloupce z DataSchema) ·
migrace starých Caché sestav · template linting v CI/CD.

---

## 🗄️ Hotová práce

Detail dokončených etap → [`roadmap-archive.md`](roadmap-archive.md). Průběžný changelog → [`changelog.md`](changelog.md).
