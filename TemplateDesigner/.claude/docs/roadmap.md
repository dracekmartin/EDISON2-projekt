# Roadmap

> **Jen otevřená / akční práce.** Hotové → [`roadmap-archive.md`](roadmap-archive.md), průběžný
> changelog → [`changelog.md`](changelog.md), stav tiketů a věci „jen na poptávku" →
> [`customer-requirements §0`](customer-requirements-zastavkovy-jr.md).

> **Nejblíž na řadě:** linkový JŘ MHD (`B191`) po krocích v Priority, shora dolů (Jan 12. 10.).
> Ostatní je v Backlogu - `B190` tisk z modulu JŘ čeká na kolegy, `B192` rozdílový JŘ na ukázku.
> ⚠️ **Ze zastávkových tiketů práci neber bez ověření** - `#12843` byl 15. 9. zavřený (odpověď
> s výsledky hovoru z 11. 9.). Evidence u něj ještě 3. 9. vedla tři body jako zbývající, ačkoli
> hotové byly; než z ní něco vezmeš, přečti celý záznam, ne jen odstavec „Zbývá".

---

## 🎯 Priority - linkový (knižní) JŘ MHD (`B191`, pořadí Jan 12. 10.)

Městská varianta linkového JŘ: agenda `LinkovyJrMhd` existuje, ale je skrytá - neprošla porovnáním
se starým modulem. „Dodělám AD a MHD spadne samo" neplatí: AD je nadmnožina v obsahu tabulky, ne
v pojmech (licenční × veřejné číslo linky, typ provozu, vlastní vrstva nastavení). Kroky shora dolů.

🧭 **Nastavení starého modulu:** *jak je výstup složený, patří do návrháře; z nastavení u linky se
bere jen to, co mění data.* Rozhodnutí po položkách, důvody a výskyt v datech: [`§13.4`-`§13.6`](linkovy-knizni-jr-implementace-analyza.md).
Závazná rozhodnutí agend (sdílené tabulkové jádro, layout, fonty): [`R1/R2/R5`](architektonicka-rozhodnuti-agendy.md).

Rozbor, oprava dat a porovnání se starým tiskem (`B191a-c`) hotové 12. 10.; před `B191d` vizuální zkouška (Jan); oracle, mezery a testovací linky v [`§18`](linkovy-knizni-jr-implementace-analyza.md).

1. **`B191d` Volby pro městské linky** [M-L]. Cíl (Jan 12. 10.): minimum = co šlo ve starém modulu
   (výběr typů provozu, kam legenda, rámečky kolem čísel spojů; směr hotový 12. 10.), k tomu
   Zvýraznit zastávku jako u AD; cílový stav = skupiny spojů se nastavují stejně ve všech čtyřech
   agendách JŘ. Návrh **čeká na potvrzení Jana**: [`§18.5`](linkovy-knizni-jr-implementace-analyza.md).
2. **`B191e` Číslo linky a výchozí šablona** [S]. `{CISLO_LINKY}` je u MHD licenční číslo, veřejné
   `{LOKALNI_OZNACENI_LINKY}` - šablona musí ukázat to veřejné. MHD nemá systémovou šablonu.
3. **`B191f` Vydání** [S]. `Released = 1` (jak se agenda skrývá: komentář u `Released`
   v `LinkovyJrAd.cls`), Novinky a příručka.

---

## 📥 Backlog

### Ostatní

- **Barva linky v linkovém JŘ** [S] (slíbeno TRANSDEV v `#13606` 12. 10., „v některé z příštích verzí").
  Zastávkový JŘ AD posílá `BARVA_LINKY` / `BARVA_LINKY_2` (`ZastavkoveJrAd.cls:352-355`, přes
  `ALVA.ISY.ISYJR2.API.Linky.GetBarvaLinky`), linkové agendy ne. AD: stejné pole do `LinkovyJrAd`.
  MHD: nejdřív zjistit, jestli modul MHD barvu linky vůbec má.

- **Jedno záhlaví „km" nad všemi km sloupci** [S] (dotaz Jan 13. 10. při přípravě školení).
  Tabulka spojů píše „km" nad každý km sloupec. Rozdělené zóny mají společné záhlaví a km ho záměrně
  nemají (`LineTableElement.cs:1677`, změnilo by to hotové sestavy). Návrh: volba u popisných
  sloupců, výchozí vypnutá.

- **Vývěsky MHD: značka zastávky u názvu** [S] (nález 12. 10., `MhdLegacyOracle`). Starý tisk
  připisuje značku zastávky k názvu („Areál VŠTE, A" na CB_DP 11350), my ne - linkový JŘ MHD už
  značky bere i z bodu trasy (`LinkovyJrMhd.ZnackyBodu`), vývěsky ne. Ověřit proti starému tisku.

- **Sjednotit nastavení skupin spojů mezi agendami JŘ** [S] (Jan 12. 10.: stejný pojem = stejný
  název). Zastávkový AD má „Rozdělení sloupců (typy provozu)" s volbou „Automaticky (dle linky)",
  linkový AD „Dělení spojů dle platnosti" bez ní. Název parametru jde změnit volně, popisky voleb
  jsou zamčené (ukládá se text) - jen se stabilním kódem vedle. Podklad [`§18.5`](linkovy-knizni-jr-implementace-analyza.md).

- **`B190` Tisk z modulu jízdních řádů bez návrháře** [L] - **čeká na kolegy** (spíš další verze).
  Uživatel v jízdních řádech (nové okno JŘ) vybere linky a dostane PDF bez otevření návrháře; šablona jednoduchou volbou výchozí šablony
  (nebo podobně, doladí se). R4 doporučuje začít spuštěním návrháře z EDISONu s parametry (launch
  kontrakt už je) a render oddělit fasádou. **Před začátkem rozhodnout**, odkud se bere výchozí
  šablona a jak ji uživatel volí. Podklad [`R4/R7`](architektonicka-rozhodnuti-agendy.md).

- **`B189` Poslání šablony jinému uživateli EDISONu** [?] (podnět Jan 7. 10., **projít s Janem proti
  dnešnímu stavu**, nic se nedělá). Dnes: v rámci firmy šablonu vidí všichni (databáze je za firmu),
  mezi firmami jen *Exportovat do souboru* / *Importovat ze souboru*, a M-line rozesílá systémové
  šablony kanálem z helpdesku (`SystemTemplateChannelSync`). Otevřené: mezi firmami, nebo uvnitř
  firmy (sdílet / nabídnout konkrétnímu kolegovi)?

- **Vyhledávač odjezdů pro cestující** [M] (zadání Jan 13. 10.). QR u sloupku (zastávka a stanoviště)
  otevře aplikaci v ISYCloud: cestující zadá cíl a dozví se nejbližší přímé spoje odsud, data z CIS.
  Nahrazuje lokální demo z 1. 10. (`edwcore/apps/tisky-qr` nad EDISONem). Zadání, k ověření
  a stav: [`qr-linkovy-jr-pro-cestujici.md`](qr-linkovy-jr-pro-cestujici.md).

- **Provozně (ne vývoj, Jan 7. 10.):** projít se zákazníky, kteří tisknou ze **starých šablon**
  (starý tisk JŘ a jeho návrhář sestav), jak totéž udělat po novu v Návrháři šablon - převést
  jejich vzhled do nové šablony a ukázat postup. Nejdřív sepsat, kdo staré šablony reálně používá.

- **`B192` Rozdílový linkový JŘ** [M] (základ 9. 10., **čeká na ukázku zákazníkům**, podle ní se
  rozhodne, co dál). Nezveřejněná agenda „Linkový JŘ / AD - rozdílový" + šablona
  `templates/rozdilovy-linkovy-jr-a4.json`. Otevřené: tlačítko z okna linek (`B190`), zrušené spoje
  jako sloupce, rozdíly v platnosti a značkách, jiná trasa. Dvojice na ukázku: CB_CSADAD 340351
  (842 proti 203), 380760 (307 proti 207); další najde `RozdilovyJrProbe.NajdiDvojice`.

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
  bez účinku (parametry sestavy; u sekcí je dnes drží návrhář, `B177`) a **popis u parametru sestavy** (dnes nejde vysvětlit krkolomný
  label) - všechno až s gramatikou, ne zvlášť.

- **`B56` Sjednocení Caché tiskových enginů a jejich opsaných pomocných funkcí** (odloženo -
  rozhodnutí Jan 5. 8., 31. 8. a 21. 9.: nedělat předčasnou deduplikaci, dokud se tvar výstupních
  dat neustálí). ⚠️ **Pohltilo i dřívější `L8`** - tatáž práce ve větším rozsahu, jen visela
  v aktivní roadmapě bez přírůstku pro uživatele. Vzít **až bude knižní JŘ doimplementovaný**.
  Podklad (co je doslovná kopie, co se liší záměrně, kde chybí testy):
  [`legacy-nahrada.md §4`](legacy-nahrada.md).
  **Změřeno 21. 9.:** zastávkové agendy mají 10 metod znak po znaku shodných (~100 řádků: barvy,
  atributy zastávky, stavba odjezdu a skupiny dnů, obě metody předdefinovaných tabulek) a ze schématu
  je 25 z 27 popisů polí MHD totožných s AD (u linkových 22 z 24). Liší se dva popisky po ose
  AD × MHD (`CISLO_LINKY`, `LOKALNI_OZNACENI_LINKY`) - ve společné metodě by byly parametrem.
  Tvar až se to bude brát: mezivrstva `ZastavkovyJrBase` / `LinkovyJrBase`, varianta si doplní svoje.
  Popisky mezitím hlídá `AgendaSchemaDriftTests`, takže tiše se rozejít nemůžou.

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
