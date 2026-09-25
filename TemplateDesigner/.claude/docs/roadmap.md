# Roadmap

> **Jen otevřená / akční práce.** Hotové → [`roadmap-archive.md`](roadmap-archive.md), průběžný
> changelog → [`changelog.md`](changelog.md), stav tiketů a věci „jen na poptávku" →
> [`customer-requirements §0`](customer-requirements-zastavkovy-jr.md).

> **Nejblíž na řadě:** `B180` a `B181` (zastávkové JŘ, TRANSDEV `#12843`), pak `#7487` (u něj nejdřív upřesnit tvar nastavení s kolegou). Zpětná vazba kolegy z 18. 9.
> i TRANSDEV z 16. 9. jsou vyřízené; co z nich zbylo, je obecná věc formuláře v Backlogu.
> ⚠️ **Ze zastávkových tiketů práci neber bez ověření** - `#12843` byl 15. 9. zavřený (odpověď
> s výsledky hovoru z 11. 9.). Evidence u něj ještě 3. 9. vedla tři body jako zbývající, ačkoli
> hotové byly; než z ní něco vezmeš, přečti celý záznam, ne jen odstavec „Zbývá".

---

## 🚆 Linkové a knižní JŘ (náhrada TiskJR)

> ⚠️ **Gate: R1 (sdílené tabulkové jádro), R2 (layout zodpovědnosti), R5 (embedded fonty)** -
> [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md).

**Stav: linkový JŘ z AD dat je od 22. 9. vydaný zákazníkům** a je výchozí agendou pro novou
šablonu. Zbývá jeden výtisk k ověření a věci, u kterých je první krok dotaz ven. Sloučení
dvou Caché tiskových enginů drží `B56` v Backlogu - je to úklid vnitřku bez přírůstku pro uživatele
(rozhodnutí Jan 31. 8.). Městská varianta zůstává skrytá, dokud neprojde porovnáním se starým
modulem; jak se agenda skrývá, popisuje komentář u `Released` v `LinkovyJrAd.cls`.

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
které starý modul nedokázal vyrobit. Novou práci na linkovém JŘ ověřuj proti němu.

🧭 **Jak se nakládá s nastavením starého modulu:** *to, jak je výstup složený, patří do návrháře;
z nastavení u linky se bere jen to, co mění data.* Sporné případy řeš směrem do šablony. Rozhodnutí
položku po položce, jejich důvody a změřený výskyt v datech drží
[`§13.4`-`§13.6`](linkovy-knizni-jr-implementace-analyza.md), pozastavené tikety helpdesku
[`§14`](linkovy-knizni-jr-implementace-analyza.md).

### Připravené k práci

- **`#7487` Rozložení poznámek podle druhu** [M] (KORID; potvrzeno hovorem s kolegou 25. 9.).
  Druhy jako ve starém win tisku (dny platnosti, negativní značky, návaznosti, atributy, textové
  poznámky…): u každého **pořadí** a **počet sloupců** zvlášť, např. „všechno do 3 sloupců, negativní
  značky do 1". Dnes jde jen jeden „Počet sloupců" pro celou legendu. Přesný tvar nastavení se
  ještě upřesní s kolegou - nejdřív návrh formuláře, pak kód.

---

## 📥 Backlog

### Ostatní

- **`B180` Minuty odjezdů u zastávky, kde spoj končí** [S] (TRANSDEV `#12843`, 23. 9., chyba).
  Vývěska vypisuje odjezd i v konečné zastávce spoje - dřív ne, takže regrese. Příklad v příloze
  tiketu „Linka 32.pdf", str. 16, zastávka Kmeťova (spoje s poznámkou „končí v zastávce Kmeťova");
  podle zákazníka se to děje na všech linkách. Nejdřív najít, kdy se to rozbilo.

- **`B181` Nejvýš N sloupců variant tras** [S] (TRANSDEV `#12843`, 23. 9.). Volba s počtem (např. 2):
  varianty se vyberou a seřadí jako dnes (slučování, řazení podle délky), vytisknou se jen první N.
  Jinak vývěsku kazí moc sloupců. Otázka: co se stane se spoji zbylých variant (dnes by zůstaly bez
  sloupce) - ověřit na jeho linkách, než se to zapne.

- **`B179` AI import z PDF pro linkový JŘ** [M] (Jan 24. 9., zkoušel to zákazník). Import dnes zná jen
  prvky zastávkového JŘ (seznam zastávek, minuty odjezdů, poznámky); `Tabulka spojů` v něm není
  (`PdfImport/` se jí nedotýká), takže linkový JŘ by skončil jako obecné tabulky a texty. Nejdřív
  zkusit na zákazníkově PDF, jak se chová, a podle toho rozhodnout, jestli naučit import
  rozpoznat tabulku spojů a nastavit její vzhled.

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

- **Jízdní řád ze zastávky na mobilu (QR na vývěsce)** (nápad Jan 22. 9., zatím bez poptávky).
  Kód na vývěsce nese **linku, zastávku a stanoviště**, takže stránka ví, kde člověk fyzicky stojí -
  to je jediná věc, kterou vyhledávač spojení neumí. Odpoví větou („nejbližší odjezd za 25 minut",
  „dnes už odsud nic nejede, první zítra v 6:10", „z druhé strany jede za 6 minut"), pod tím výběr
  cíle s dobou jízdy a texty poznámek místo značek. Papír navíc zastarává, odkaz ne.
  ⚠️ **Nedělat plánovač s přestupy ani mapy** - tam vyhrává IDOS. Hranice je „co odsud jede přímo".
  ⚠️ **Vidlička k rozhodnutí:** vývěska je jedna linka, cestující chce jednu cestu - silnější verze
  je „odjezdy ze stanoviště za všechny linky", což je blíž relacím než vývěsce.
  ⚠️ **Slabé místo je „jede to dnes"**: svátky a omezení dnes tiskneme jako text, ne jako datum,
  které jde spočítat. Prototyp může jet na dni v týdnu a značky vypsat slovy.
  Zpoždění v reálném čase by hodnotu zvedlo nejvíc, ale data živá nejsou (GTFS RT jen u DPCB).

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
