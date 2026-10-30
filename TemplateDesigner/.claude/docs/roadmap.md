# Roadmap

> **Jen otevřená / akční práce.** Hotové → [`roadmap-archive.md`](roadmap-archive.md), průběžný
> changelog → [`changelog.md`](changelog.md), stav tiketů a věci „jen na poptávku" →
> [`customer-requirements §0`](customer-requirements-zastavkovy-jr.md).

> **Nejblíž na řadě:** `B56` (příprava `B194` hotová 30. 10.); přehled a pořadí níž. Rozdílový linkový JŘ (`B192a-f`) je hotový 17. 10.; od 23. 10.
> ho jízdní řády (Nástroje → Porovnání linek) tisknou rovnou jako PDF v prohlížeči, šablona není veřejná; mezery proti vzoru v [`§19.6`](linkovy-knizni-jr-implementace-analyza.md).
> ⚠️ **Ze zastávkových tiketů práci neber bez ověření** - `#12843` byl 15. 9. zavřený (odpověď
> s výsledky hovoru z 11. 9.). Evidence u něj ještě 3. 9. vedla tři body jako zbývající, ačkoli
> hotové byly; než z ní něco vezmeš, přečti celý záznam, ne jen odstavec „Zbývá".

---

## 🧱 `B56` Sloučení kódu agend JŘ [L] (přehled 30. 10.)

Cíl (Jan 29. 10.) není sjednotit všechno, ale ujasnit strukturu a dát agendám základ, aby se nerozjížděly tam, kde to uživatel pozná nebo kde se oprava dělala víckrát.
Sdílený kód jde do pomocných tříd mimo dědičnost agend ([`R8`](architektonicka-rozhodnuti-agendy.md)).
Postup, pojistky a snímek výstupu jsou v [`legacy-nahrada.md §4`](legacy-nahrada.md): jeden krok = jeden commit, snímek před i po.
Metoda s českým slovesem nebo se jménem, které jinde znamená něco jiného, se při přesunu přejmenuje (`cache-api.md` pravidlo 5).
Cílem je udržitelnost s dalšími agendami, ne méně kódu; model dvou os a pravidlo druhého použití jsou v R8, měření v `legacy-nahrada.md §4`.
**Fáze (Jan 30. 10.):** 1 struktura a pojistky → 2 popis agendy podle druhu výstupu → 3 výstup podle druhu → 4 data podle zdroje
(fáze 1-4 hotové 30. 10., kroky S1-S12 v archivu; pole pro QR čeká na vyhledávač) → 5 relace a vozový JŘ nanečisto (co by chybělo, nic se neimplementuje) →
6 po úklidu nastavení u kolegů → 7 funkce nad společnými třídami.

### Převzato z backlogu - souvisí se sjednocením agend

- **Úklid nastavení tisku v jízdních řádech - naše strana** [M] - **čeká na kolegu z jízdních řádů**.
  Domluveno 29. 10.: většinu tiskových nastavení z jízdních řádů vyhodí, zbytek dodělají. Až se k tomu
  dostane, sladit agendy: nastavení, které zmizí z jízdních řádů a návrhář ho dál čte, by zůstalo uložené
  a nešlo by změnit. Na tom visí kroky `B56`, které čtou nastavení linky. Co agendy čtou a odkud:
  [`nastaveni-tisku-jr.md`](nastaveni-tisku-jr.md).
- **P2 (z `B194c`)** Totéž rozdělení sloupců má ve vývěsce a v linkovém JŘ jiné popisky voleb (zamčené) [M]
  **Jan** - čeká na úklid nastavení v jízdních řádech (výš), ze kterého volby vycházejí; definice pak patří
  do `Jr/Parametry.cls` (stálý kód vedle popisku). Detail [`legacy-nahrada.md §4`](legacy-nahrada.md)
- **Nová pole v jízdních řádech, dohodnutá s kolegy 10. 8.** [S] - **čeká na kolegu**; v lokálním zdroji
  nových jízdních řádů zatím nejsou (29. 10.). **Cíl** u zastávky pro každý směr (TR5, ověřit, že po
  zastávce a směru), **odsazení zastávky** zvlášť pro oba směry (TRANSDEV #7937, pozastavený; trvá zákazník
  i na odsazení podle tarifních čísel?), **název směru** u linky (TRANSDEV #9088). Náš díl: pole přečíst
  a nabídnout v šabloně. Detail [`customer-requirements §TR`](customer-requirements-zastavkovy-jr.md).
- **Varianty tras i ve vývěsce MHD** [L] - sloupec „Varianty tras" v Seznamu zastávek umí jen AD
  (výpočet variant po trasách, písmena, pět parametrů). Jan je chce i v MHD (27. 10.): přenést výpočet
  na trasy a body MHD a ověřit proti datům, pak v `ZastavkoveJrMhd` přepnout `VariantyTras = 1` -
  nabídka sloupce se ukáže sama. Hodí se spojit s `B56` (sdílený kód agend).
- **Číslo zastávky pro adresu v QR ve všech agendách** [S] (dřív `B193h`, odloženo Janem 23. 10.) - jedno pole
  se stejným klíčem ve všech agendách JŘ; patří k Vyhledávači odjezdů v backlogu.
- **Sjednocené věci definovat jednou** [M] **Jan** (27. 10.) - nejdřív revize toho, co `B194` sjednotilo (popisky,
  nápovědy a rozsahy polí E8, údaje dopravce D4, nabídka podle dat E5, kanon `ux-conventions §8`), a pro každé
  rozhodnout, kde má extrakce na jedno místo smysl a kde stačí drift test. Kandidát: panel návrháře bere popisky
  a rozsahy ze schématu formuláře `*Edit.cls` místo opisu v `GetPropertyDescriptors` (dnes se píše dvakrát).
  Navazuje na E3 / E13; je to strana návrháře, kód agend řeší sekce výš.
- **Slovo „linka" napevno** (`B194e` A5) [M] - asi 25 textů (menu „Zkontrolovat vybrané linky…",
  hlášky „Žádná vybraná linka nemá data", průběh kontroly). Udělat, **až se bude vydávat agenda bez
  linek** (převod Report generátoru; ukázkové Faktury jsou od 30. 10. smazané) - skloňovat podle názvu
  instance agendy; okno výběru už ho bere (Jan 27. 10.).

### Vědomě nesjednocovat

Každá agenda tu drží tvar svého starého modulu: nadpis skupiny dnů, telefon dopravce, vysvětlivka „na znamení", zóny zastávky, stanoviště u názvu, popis dopravce, rozsahy čísel spojů, barva „0;0;0", značky u odjezdů.
Seznam s důvody je v [`legacy-nahrada.md §4`](legacy-nahrada.md); sloučení kódu je nesmí srovnat.

---

## 📥 Backlog

### Ostatní

- **Úvodní okno pro nové uživatele** [S-M] (nápad Jan 29. 10.). Dnes návrhář otevře prázdnou šablonu
  a nový uživatel neví, kde začít. Okno po otevření nabídne: otevřít ukázkovou šablonu agendy, otevřít
  šablony z databáze, nebo zavřít a pokračovat jako dnes (s odkazem na nápovědu). Otevřené: co je
  „ukázková šablona", jestli jen napoprvé / do „Příště nezobrazovat", a nesmí se ukázat při spuštění
  ze shellu s linkou nebo tisku do PDF. Dřív odložený „onboarding průvodce" (archiv) je širší.
  **Zkušební verze je ve větvi `td/uvodni-nabidka`** (29. 10.: formulář „Jak začít" přes prázdné plátno
  s testy) - odložená, než doladíme práci kolem `B56` (Jan).

- **Hledání ve formuláři po slovech a se synonymy** [M] (z `B194h` H12, Jan 28. 10.: zajímavé, ale ne
  teď). Hledání dnes bere celý text jako souvislou část popisku nebo skupiny: „písmo", „podbarvení"
  ani „čára" nenajdou nic, „výška záhlaví" po `H6` nenajde Nejmenší výšku. Návrh: každé slovo zvlášť
  přes popisek i skupinu, bez koncovky u slov od 5 písmen, a krátký seznam synonym (písmo → Font, Řez,
  Velikost; podbarvení → Barva pozadí). Sdílená komponenta EDControls - platí pro celý EDISON.
- **Změna agendy nesmí nechat v šabloně prvky jiné agendy** [S-M] - dnes jde šablonu s Minutami odjezdů
  přepnout na Obecnou (Ctrl+Shift+A) a prvek zůstane jen s upozorněním. Prvky JŘ mají jít otevřít jen
  ve své agendě, takže změnu agendy v takové šabloně zablokovat a říct proč (rozhodnutí Jan 27. 10.).
  Odtud i prázdná záložka „Vlastní definice sloupců" v obecné šabloně (review 27. 10., bod 4).

- **`B190` Tisk z modulu jízdních řádů bez návrháře** [L] - **čeká na kolegy** (spíš další verze).
  Uživatel v jízdních řádech vybere linky a dostane PDF bez otevření návrháře. Pro jednu linku PDF bez
  okna už je (rozdílový JŘ); zbývá víc linek a volba šablony z databáze. **Před začátkem rozhodnout**,
  odkud se bere výchozí šablona a jak ji uživatel volí. Stav a postup [`R4`](architektonicka-rozhodnuti-agendy.md).

- **`B189` Poslání šablony jinému uživateli EDISONu** [?] (podnět Jan 7. 10., **projít s Janem proti
  dnešnímu stavu**, nic se nedělá). Dnes: v rámci firmy šablonu vidí všichni (databáze je za firmu),
  mezi firmami jen *Exportovat do souboru* / *Importovat ze souboru*, a M-line rozesílá systémové
  šablony kanálem z helpdesku (`SystemTemplateChannelSync`). Otevřené: mezi firmami, nebo uvnitř
  firmy (sdílet / nabídnout konkrétnímu kolegovi)?

- **Vyhledávač odjezdů pro cestující** [M] (zadání Jan 13. 10.). QR u sloupku (zastávka a stanoviště)
  otevře aplikaci v ISYCloud: cestující zadá cíl a dozví se nejbližší přímé spoje odsud, data z CIS.
  Nahrazuje lokální demo z 1. 10. (`edwcore/apps/tisky-qr` nad EDISONem). Zadání, k ověření
  a stav: [`qr-linkovy-jr-pro-cestujici.md`](qr-linkovy-jr-pro-cestujici.md). Jedno pole čísla zastávky
  pro adresu v QR ve všech agendách je v přehledu `B56`.

- **`B185` Stránky dokladu jako pojem šablony** [L] (Jan 25. 9.). Pojmenované stránky (pevná /
  průběžná), pás stránek v návrháři, hlavička a patička s volbou stránek; nahradí „Začít na nové
  stránce" i „Jen první / poslední". Mění formát šablony (migrace). Až budou
  hotové jízdní řády a přijdou doklady typu faktura nebo stazka. Mock viz `B184`. `B184`, `B186` a `B187` hotové, dál podle poptávky.

- **`B179` AI import z PDF pro linkový JŘ** [L] (Jan 24. 9.; první verze 2. 10.). Funguje, ale
  polohy tabulky a poznámek jsou odhad modelu. **Od 9. 10. jen interní nástroj** - převody předloh
  dělá M-line; dál až se při nich ukáže, že šetří čas. Zbývají kroky **(2)** model pojmenuje bloky
  z PDF a **(3)** tabulka měřená z PDF celá. Stav a proč: [`architecture.md §Import z PDF`](architecture.md).

- **`B171` Úprava vysázené stránky před tiskem** [L] (nápad Jan 21. 9., zatím bez poptávky).
  Posunout nebo zvětšit prvek na jedné vysázené stránce před PDF, když se linkám s mnoha poznámkami
  zmenší písmo. ⚠️ **Data se znovu nevysází** a úprava platí jen pro jeden tisk. Vytáhnout, až nebudou
  stačit levnější cesty (prvek naroste do volného místa, vlastní šablona pro linku).
  Detail [`architecture.md`](architecture.md) §Úprava vysázené stránky.

- **`B155` Jemnější nastavení mřížky** [M] (podnět Jan 16. 9.). Mřížka má jednu barvu, tloušťku
  a styl pro všechny čáry; návrh jsou čtyři druhy čar s vlastním vzhledem zadávané kaskádou.
  ⚠️ Per-buňku ani per-hranu ne (Jan 16. 9.) a pohltí „Popisné sloupce bez vnitřních čar".
  Poptávka je zatím jedna (KOVED), vytáhnout, až se sejde víc požadavků.
  Návrh a rozhodnutí: [`elementy-architektura.md`](elementy-architektura.md) §Návrh jemnější mřížky.

- **`B144` Sloupec „Čas" i z ručních minut v trase** [S] **(zastávkové JŘ)** - volba zdroje hodnot:
  spočítané z jízdních dob (dnes, zůstává výchozí), ruční „MHD čas tam/zpět" z trasy, nebo
  „Automaticky (dle linky)". ⚠️ **Odloženo, protože si o to zákazník neřekl** (Jan 11. 9.) - bude
  radši používat varianty tras. Vytáhnout, až to někdo bude chtít.
  Detail [`ad-data-mapa.md`](ad-data-mapa.md) §Rozdílové trasy.

- **`B66` Vlastní bloky prvků („moje komponenta")** [M] (podnět Jan 12. 8.). Uživatel vybere pár
  prvků, uloží je pod jménem a vkládá jedním kliknutím do dalších šablon - mezistupeň mezi uloženou
  sekcí a seskupením prvků. **Čeká na pevnou tabulku**: jako jeden prvek je uložení skoro zadarmo,
  z šesti textů musí blok řešit relativní pozice. Otevřené: kde bloky žijí a odkud se vkládají.
  **Pohltí i připravené bloky agend** (dřív `B77`) - jsou to titíž pojmenované bloky, jen dodané
  námi (rozhodnutí Jan 17. 8.).

- **`B18` Deklarativní pravidla EDInputFormu** [M] (odloženo - Jan 5. 8.: **kapabilita sdílené komponenty
  pro celý EDISON**, ne featura návrháře). Formulář dnes reaguje na hodnoty jen oklikou přes server.
  Začít **viditelností** (`visibleWhen`), gramatiku sdílet s tiskovými parametry - nezavádět druhý
  dialekt; validace zůstává imperativní. Až s gramatikou, ne zvlášť: zašednutí polí bez účinku
  (parametry sestavy; u sekcí je drží návrhář, `B177`). Popis u parametru už je (`description`).

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

**Relace** jsou jen AD a nejsou slovenské: odjezdy nebo příjezdy z jedné zastávky seskupené podle cílové zastávky, v řádku buňky spojů s volně rozmístěnými údaji.
Živý zákazník je BBSK `#13070` (pozastavený): vypadá jako linkový JŘ, ale předloha je odjezdová relace.
**Vozový JŘ** je jen MHD - tisk DPKV v MHD2Tisk s vlastním kreslením, vydaný 27. 10.; vozové listy (MHD) a turnus pro řidiče (AD) jsou sady report generátoru a patří do sekce níž.
Rozbor 30. 10.: [`legacy-nahrada.md`](legacy-nahrada.md), mapa legacy generátorů §3-4.

| Co | Stav |
|---|---|
| **Relace AD** - agenda nad sdílenými třídami AD z `B56` a nový prvek (mřížka buněk spojů, každý údaj s vlastní pozicí a písmem) | ⬜ další po `B56` |
| **Vozový JŘ DPKV** do návrháře - jediný zákazník a čerstvý vlastní tisk | ⬜ na poptávku |

---

## 📊 Report generátor (náhrada REPORTGENERATOR)

> ⚠️ **Gate před `F2`: R3 (prezentační nápovědy v datovém kontraktu + aditivní evoluce)** -
> [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md).

27 agend starého report generátoru s asi 85 datovými sadami; agenda report generátoru je modul (AD, MHD, mzdy, pokladna…) a teprve jeho sada odpovídá naší agendě.
Agendy dodávají jen data a kreslí obecný kód - zvláštní prvky jako u jízdních řádů tam nejsou.
Kreslení je proto hlavně technický převod (tabulka z dat `F3`); koncepčně nové je spouštění tisku, výstupy a šablony zákazníků (`F9`, `F10`).
Dopravní agendy narazí jen na `F3`, mzdy, pokladna a faktury hned na `F9` a `F10`.
**Ne big-bang**, pilot podle R7. Rozbor 30. 10. (agendy, sady, mapování, pilot): [`legacy-nahrada.md`](legacy-nahrada.md) §3.1.

| # | Co | Stav |
|---|---|---|
| F7 | **Pojmenování agend** - sada = naše agenda podle výstupu (`VYPLATNI_PASKA`), modul se stejným výstupem = varianta (`MZDY2`); kód agendy se po vydání nemění, proto před první agendou | ⬜ rozhodnutí Jan |
| F8 | **Pilot: Vozové listy (MHD)** - 5 polí, 2 tabulky, instance = oběh, volá MHD2Linky; vlastní třída zavolá starý `getSadu`/`getData` a převede výstup, registr se nemění. Narazí na zvýraznění řádku z dat (`F3`, R3). Před vydáním text „linka" napevno (`B194e` A5 u `B56`) | ⬜ po relacích |
| F2 | **Obecný převodník vs čisté agendy** - rozhodnout po pilotu; převodník potřebuje registr, který vypíše dvojice agenda a sada, a varianty se stejným tvarem dat neumí | ⬜ rozhodnutí po `F8` |
| F3 | **Tabulka z dat na úroveň RG** (navazuje na dynamickou tabulku + `B76`) - řádky řízené daty, overflow, mezisoučty, seskupení řádků a vnořená data; ⚠️ tvar odvodit z reálných sestav RG. Rozpis [`legacy-nahrada.md`](legacy-nahrada.md) §5 etapa 4 | ⬜ L |
| F1 | **Pořadí agend** - statistika použití agend neexistuje (`Main.cls:161-188` jsou počty prvků) a tisky se nelogují; počet šablon podle agendy v databázích zákazníků (jen čtení), nebo počet volajících míst. Dopravní agendy napřed | ⬜ |
| F9 | **Hromadný tisk a tisk bez uživatele** - seznam dokladů, každý s vlastním PDF, heslem, e-mailem, podpisem nebo tiskárnou; spuštění přímo z Caché (mzdy). Návrhář umí jedno PDF v prohlížeči (`Vystup=PDF`). Navazuje R4 a `B190` | ⬜ před první agendou z mezd, pokladny, faktur |
| F10 | **Šablony zákazníků** - firmy mají vlastní šablony (nejméně stovky) a návrhář je neotevře: převodník, nebo nový návrh. Největší rozhodnutí převodu | ⬜ rozhodnutí Jan, před první agendou s šablonami zákazníků |
| F11 | **Data pro náhled** - vstup je složený (zaměstnanec + období) nebo jeden doklad z tisíců; u každé agendy ukázková data, nebo výběr dokladu s filtrem | ⬜ u každé agendy |
| F12 | **Skupiny v nabídce agend** podle oblasti práce (Jízdní řády, Plán a provoz, Mzdy…) a filtr podle licence; okno Otevřít má dnes plochý filtr agend | ⬜ od ~8-10 agend |
| F4 | **Další výstupy** - Excel (data podle typů sloupců), znakový tisk Epson, JPG | ⬜ dle poptávky |
| F5 | **Jazykové varianty šablon** - per-jazyk kopie provázaná na CS zdroj | ⬜ |
| F6 | **Vědomě neportovat bez poptávky** - grafy (12×), AZTEC (2×), calculated (2× - výrazy TD nadmnožina), checkbox (8×); ověřit + škrtnout | ⬜ validace |

---

## 🗄️ Hotová práce

Detail dokončených etap → [`roadmap-archive.md`](roadmap-archive.md). Průběžný changelog → [`changelog.md`](changelog.md).
