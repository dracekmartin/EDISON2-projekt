# Roadmap

> **Jen otevřená / akční práce.** Hotové → [`roadmap-archive.md`](roadmap-archive.md), průběžný
> changelog → [`changelog.md`](changelog.md), stav tiketů a věci „jen na poptávku" →
> [`customer-requirements §0`](customer-requirements-zastavkovy-jr.md).

> **Nejblíž na řadě:** linkový JŘ MHD jde do verze 27. 10. (`B191g`: finální zkouška, po vydání ověření
> u zákazníka), souběžně druhé kolo sjednocení agend JŘ (`B194`, bod po bodu), po něm `B56` (backlog). Rozdílový linkový JŘ (`B192a-f`) je hotový 17. 10.; od 23. 10.
> ho jízdní řády (Nástroje → Porovnání linek) tisknou rovnou jako PDF v prohlížeči, šablona není veřejná; mezery proti vzoru v [`§19.6`](linkovy-knizni-jr-implementace-analyza.md).
> ⚠️ **Ze zastávkových tiketů práci neber bez ověření** - `#12843` byl 15. 9. zavřený (odpověď
> s výsledky hovoru z 11. 9.). Evidence u něj ještě 3. 9. vedla tři body jako zbývající, ačkoli
> hotové byly; než z ní něco vezmeš, přečti celý záznam, ne jen odstavec „Zbývá".

---

## 🧩 Sjednocení agend JŘ - druhé kolo (`B194`, revize 23. 10.)

Nezávislá revize čtyř agend JŘ po `B193`, každý nález přeověřený proti kódu. **Jeden řádek = jeden nález**;
co uživatel vidí, doklady a návrh jsou pod stejným ID v [`konzistence-agend-jr.md`](konzistence-agend-jr.md).
Procházíme s Janem bod po bodu shora, hotový řádek jde do archivu. Značky: **tisk** = změní vytištěný výstup
vydané agendy · **Jan** = potřebuje rozhodnutí · **do 27. 10.** = týká se linkového MHD, nejlevnější před verzí.

### `B194b` Tisk - rozdíly mezi agendami (všechno **tisk** a **Jan**)

- **T7** „Jen nástup / jen výstup" chybí v Zastávkovém JŘ AD - **odloženo** (Jan 26. 10.), otevřené: podle
  kterých spojů šipku ukázat; rozpracováno ve stashi [M]

### `B194c` Nabídka dat a parametry (jen popisky a nápovědy, pokud není uvedeno jinak)

- **D2** „Pořadí" v linkovém AD je ve skutečnosti tarifní číslo [S] **Jan**
- **D3** „Konečná" × „cílová", CIS na začátku × na konci popisku, vývěsky bez výchozí zastávky [S]
- **D4** Údaje dopravce po jednotlivých polích má jen vývěska AD [M] **Jan**
- **D5** Pořadí polí v nabídce dat se mezi agendami liší [S]
- **D6** Nejasné popisky: „Popis linky", „Druh (1-10)", interní kódy typů poznámek [S]
- **D7** Náhled vývěsky MHD bez linky ukazuje vymyšlenou brněnskou linku [M]
- **D8** Drobnosti v datech a ukázkách - zbývá mylný komentář, typ času jízdy, typy provozu v ukázce MHD, sonda [S]
- **P1** Vývěsky: osm voleb bez nápovědy, dvě nesrozumitelné [S]
- **P2** Totéž rozdělení sloupců má ve vývěsce a v linkovém JŘ jiné popisky voleb (zamčené) [M] **Jan**
- **P3** Stránka pro stanoviště je ve vývěsce jinde a jinak pojmenovaná než v linkovém JŘ [S] **Jan**
- **P5** Linkový AD výlepy: bez pořadí stránek a bez PDF na zastávku [M] **Jan**

### `B194d` Prvky a formuláře (nejdřív kanon `ux-conventions §8`, **Jan**)

- **E3** Styl záhlaví a buněk má v pěti prvcích pět podob, formuláře nemají společnou kostru [L] **Jan** -
  **odloženo** (Jan 26. 10.) až za menší body E5-E16; patří sem i umístění voleb záhlaví (dnes „Skupiny
  dnů" / „Řádky", kanon chce „Obecné")
- **E5** Seznam zastávek nabízí v MHD sloupek, varianty tras a „Více stanovišť", které existují jen v AD [M] -
  **odloženo** (Jan 26. 10.); návrh: nabídka podle polí, která agenda posílá, zapnutý sloupec zůstane vidět
- **E8** Význam prázdné hodnoty zapsaný pěti způsoby, různé rozsahy [M] **Jan** - **odloženo k rozmyšlení**
  (Jan 26. 10.); návrh: vysvětlivka z popisku do nápovědy, rozsahy na nejširší (výška řádku 1-50 mm, písmo 3-200 pt)
- **E13** Dialogy sloupců - zbývá c (jedna společná komponenta oken se sloupci, přestavba) - **odloženo**
  spolu s E3 [M]

### `B194e` Aplikace a dokumentace

- **A1** Agenda se jmenuje sedmi způsoby, „AD (linková doprava)" [M] **Jan**
- **A2** Start z EDISONu vždy předvolí zastávkový JŘ, linkový JŘ nemá vstup [S-M] **Jan**
- **A3** Výběr linek: „Číslo" znamená v každé variantě něco jiného [S-M]
- **A4** Systémové šablony postavené různě (sekce, popis) [S]
- **A5** Drobnosti textů (tokeny, tooltip výběru linek, „Přejmenovat...") [S]
- **H1** Příručka nesedí s UI na 21 místech [S]
- **H2** Nápověda F1 zastaralá a s překlepem [S]

### `B194f` Pojistky před `B56`

- **G1** Kontraktový Caché test přes registr agend (schéma, ukázka, data, kódy, důvod prázdného výstupu) [M]
- **G2** Shoda celé definice stejnojmenného parametru ve všech agendách [S]
- **G3** Jeden popisek = jeden klíč napříč agendami JŘ [S]
- **G4** Kontrola zakázaných termínů i v nápovědách a titulcích oken [S]
- **G5** Každé tlačítko formuláře má obsluhu v každém hostiteli [S]
- **G7** Drobné díry drift testů [S]
- **G8** Zlatý odtisk linkového MHD včetně hlavičky (po 27. 10.) [S]

### `B194g` Architektura před `B56`

- **R1** Rozhodnout model sdílení kódu agend - registr najde jen přímé potomky `BaseAgenda` [S] **Jan**
- **R2** Duplicity pod jiným jménem (dopravce, platnost, barva, texty omezení) doplnit do `legacy-nahrada §4` [M]
- **R3** Zapsat kontrakt prvek ↔ agenda a hlídat ho testem [M]
- **R4** Schopnosti agend místo kódů agend natvrdo v C# [S-M] **Jan**
- **R5** Parametry vývěsek do jedné definice (první krok `B56`) [S-M]
- **R6** Dokumentace pro rozvoj (`cache-api.md` vzor a checklist, Faktury) [S]
- **R7** Názvosloví v kódu sjednotit jen u nových názvů [S]

---

## 🔎 Linkový JŘ MHD ve verzi 27. 10. (`B191g`)

Agenda, systémová šablona, Novinky i příručka jsou připravené 23. 10. (`B191f`, archiv).

1. **Finální zkouška (Jan) před úterkem** [S] - postup v [`§18.10`](linkovy-knizni-jr-implementace-analyza.md).
2. **Po vydání ověřit u zákazníka** [S], co dev data nemají: poznámky z trasy u zastávky, příjezd
   a odjezd na jedné zastávce, atributy spoje mimo B/C ([`§18.7`](linkovy-knizni-jr-implementace-analyza.md)).
   Postavené podle kódu starého tisku; nahrát Novinky na wiki až s verzí.

---

## 📥 Backlog

### Ostatní

- **Nastavení jízdních řádů, která čte návrhář** [M] **Jan** - Jan je nejdřív projde s kolegy (26. 10.).
  Dnes návrhář přebírá nastavení u linky a firmy z modulu JŘ tak, jak je ukládá starý modul (např.
  formát roku v omezeních, `B194a` C9). Po probrání rozhodnout, co z nich zůstane u linky, co se
  přesune do šablony a jak se má nastavovat.

- **`B56` Sloučení opsaného kódu agend** [L] - **po vydání** linkového JŘ MHD (čekalo na doimplementaci
  knižního JŘ, splněno 22. 10.). Bez přínosu pro uživatele, proto až po vydání. Začít tím, co se už
  rozešlo: výběr skupin linek a linek u AD (MHD sdílí od `B193c`), hlavička linky (rozcházela se
  v platnosti a razítku, `B193f`), společné parametry vývěsek, předpřipravené tabulky, které návrhář
  nenabízí (zrušit, nebo napojit). Podklad: [`legacy-nahrada.md §4`](legacy-nahrada.md). **Před startem**
  rozhodnout model sdílení (`B194g` R1: registr najde jen přímé potomky `BaseAgenda`) a mít pojistky `B194f`.

- **`B190` Tisk z modulu jízdních řádů bez návrháře** [L] - **čeká na kolegy** (spíš další verze).
  Uživatel v jízdních řádech (nové okno JŘ) vybere linky a dostane PDF bez otevření návrháře; šablona jednoduchou volbou výchozí šablony
  (nebo podobně, doladí se). R4 doporučuje začít spuštěním návrháře z EDISONu s parametry (launch
  kontrakt už je) a render oddělit fasádou. **Před začátkem rozhodnout**, odkud se bere výchozí
  šablona a jak ji uživatel volí. Podklad [`R4/R7`](architektonicka-rozhodnuti-agendy.md).
  PDF bez okna ze spuštění už je pro jednu linku (`Vystup=PDF`, rozdílový JŘ 23. 10., `Config/LaunchInput`),
  šablona zatím jen ze `LaunchTemplates/` v exe; zbývá víc linek a volba šablony z databáze.

- **`B189` Poslání šablony jinému uživateli EDISONu** [?] (podnět Jan 7. 10., **projít s Janem proti
  dnešnímu stavu**, nic se nedělá). Dnes: v rámci firmy šablonu vidí všichni (databáze je za firmu),
  mezi firmami jen *Exportovat do souboru* / *Importovat ze souboru*, a M-line rozesílá systémové
  šablony kanálem z helpdesku (`SystemTemplateChannelSync`). Otevřené: mezi firmami, nebo uvnitř
  firmy (sdílet / nabídnout konkrétnímu kolegovi)?

- **Vyhledávač odjezdů pro cestující** [M] (zadání Jan 13. 10.). QR u sloupku (zastávka a stanoviště)
  otevře aplikaci v ISYCloud: cestující zadá cíl a dozví se nejbližší přímé spoje odsud, data z CIS.
  Nahrazuje lokální demo z 1. 10. (`edwcore/apps/tisky-qr` nad EDISONem). Zadání, k ověření
  a stav: [`qr-linkovy-jr-pro-cestujici.md`](qr-linkovy-jr-pro-cestujici.md). Patří sem i jedno pole
  čísla zastávky pro adresu v QR ve všech agendách (dřív `B193h`, odloženo Janem 23. 10.).

- **`B185` Stránky dokladu jako pojem šablony** [L] (Jan 25. 9.). Pojmenované stránky (pevná /
  průběžná), pás stránek v návrháři, hlavička a patička s volbou stránek; nahradí „Začít na nové
  stránce" i „Jen první / poslední". Mění formát šablony (migrace). Až budou
  hotové jízdní řády a přijdou doklady typu faktura nebo stazka. Mock viz `B184`. `B184`, `B186` a `B187` hotové, dál podle poptávky.

- **`B179` AI import z PDF pro linkový JŘ** [L] (Jan 24. 9.; první verze 2. 10., viz changelog).
  **Od 9. 10. jen interní nástroj** (`dev.pdfImport` v lokálním configu) - převody předloh dělá
  M-line; dál až se při nich ukáže, že šetří čas. Funguje, ale není použitelný: polohy tabulky
  a poznámek jsou odhad modelu. Po krocích, každý
  změřit: ~~(1) měření kvality~~ hotové 2. 10. (`PdfImport/Kvalita/`, výchozí stav v changelogu),
  **(2)** model pojmenuje očíslované bloky z PDF místo odhadu souřadnic, **(3)** tabulka měřená
  z PDF celá. Proč a jak: [`architecture.md §Import z PDF`](architecture.md).

- **Ručně zadaný „název směru" u linky** [S] (TRANSDEV #9088 bod 3, dřív `C9`; linkové i zastávkové
  JŘ). Čeká na **nové pole u linky v jízdních řádech** - kolegové s ním v nové verzi počítají
  (zákazníkovi odpovězeno 23. 9.). Náš díl: pustit pole schématem obou agend a nabídnout v šabloně.

- **`B171` Úprava vysázené stránky před tiskem** [L] (nápad Jan 21. 9., zatím bez poptávky).
  Zákazník tiskne sto linek a jedna dvě mají tolik poznámek, že se jim písmo zmenší. Nápad: sázecí
  engine by uložil vysázené stránky jako dokument, kde jsou pořád prvky se souřadnicemi, a nad ním
  by šlo u jedné stránky prvek posunout nebo zvětšit, teprve pak PDF. Umí to FastReport
  a Stimulsoft, Acrobat totéž nad hotovým PDF.
  ⚠️ **Data se znovu nevysází** - roztažený prvek dá bílé místo, ne víc textu, a úprava platí jen
  pro ten jeden tisk. Levnější cesty k témuž: nechat prvek při přetečení narůst do volného místa
  pod sebou místo zmenšení písma, a přiřadit lince vlastní šablonu (index dnes jen podle agendy,
  chybí i dávkový tisk). Tohle vytáhnout, až se ukáže, že obojí nestačí.

- **`B155` Jemnější nastavení mřížky** [M] (podnět Jan 16. 9.). Dnes má `GridStyle` **jednu** barvu,
  tloušťku a styl pro vodorovné i svislé čáry - ty jdou jen zapnout nebo vypnout. Návrh: **čtyři
  druhy čar** (mezi řádky, mezi sloupci, pod záhlavím, na hranici skupiny sloupců), každý s vlastní
  barvou / tloušťkou / stylem a volbou „nekreslit", zadávané **kaskádou** jako styly buněk - nahoře
  jedna sada, kdo chce, přebije ji u druhu (dvanáct plných polí by formulář neunesl).
  ⚠️ **Per-buňku ani per-hranu ne** (rozhodnutí Jan 16. 9.) - u tiskové sestavy s desítkami řádků
  je to zbytečné. Vnější obrys zůstává rámečkem prvku, ne součástí mřížky.
  ⚠️ **Pohltí `PopisneSloupceBezVnitrnichCar`** („Popisné sloupce bez vnitřních čar", otázka Jan
  16. 9.) - je to per-prvek náplast, která vznikla právě proto, že obecná mřížka nestačila, a dnes
  je to jediný způsob, jak čáry odlišit uvnitř tabulky. V editoru je to **jeden z druhů čar**
  („mezi popisnými sloupci"), ne samostatná volba jinde ve formuláři. Zároveň se tím dá to, co
  dnes chybí: čára mezi zónou a km, ale ne mezi km podsloupci.
  Poptávka je zatím **jedna**: KOVED bod 5 ze `§15.2` chce jiný styl u spojů než u info sloupců
  a dnes se to obchází vypnutím svislých čar. Vytáhnout, až se sejde víc požadavků.

- **`B144` Sloupec „Čas" i z ručních minut v trase** [S] **(zastávkové JŘ)** - volba zdroje hodnot:
  spočítané z jízdních dob (dnes), ruční „MHD čas tam/zpět" z trasy linky, nebo „Automaticky
  (dle linky)" podle přepínače u linky. Výchozí musí zůstat spočítané, jinak se českým firmám
  sloupec vyprázdní (pole mají prázdné). Server-side parametr → do allowlistu.
  ⚠️ **Odloženo, protože si o to zákazník neřekl** (Jan 11. 9.): vyšlo to najevo při hovoru jako
  rozdíl proti starému výtisku na Nitře, ale on sám řekl, že bude radši používat varianty tras.
  Vytáhnout, až to někdo bude chtít.

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
  bez účinku (parametry sestavy; u sekcí je dnes drží návrhář, `B177`) a **popis u parametru sestavy** (dnes nejde vysvětlit krkolomný
  label) - všechno až s gramatikou, ne zvlášť.

- **Rozsekat `TemplateDesignerViewModel`** [M] (review 21. 9., odloženo). 4 217 řádků a 387 členů,
  poctivě rozdělených komentářovými pruhy na ~25 celků (soubor, databáze, schránka, zarovnání,
  parametry agendy, náhled dat, rozvržení sloupců). Každá featura nové agendy přistane sem a dva
  lidé si v něm překážejí. Návrh: `partial` soubory přesně podle těch pruhů, beze změny chování;
  precedens je `MainWindow.TableCells.cs` (týž problém má `MainWindow.xaml.cs`, 3 394 řádků).
  ⚠️ Je to diff přes celý soubor - brát **až nebude ve stromu běžet druhá session**, jinak konflikt.

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

⚠️ **Živý use case tu je: `#13070`** (BBSK, pozastavený). Vypadá jako linkový jízdní řád, ale
zákazníkova předloha je **odjezdová relace** - proto se v linkovém tracku neřeší a čeká tady.

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
