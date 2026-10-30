# Roadmap

> **Jen otevřená / akční práce.** Hotové → [`roadmap-archive.md`](roadmap-archive.md), průběžný
> changelog → [`changelog.md`](changelog.md), stav tiketů a věci „jen na poptávku" →
> [`customer-requirements §0`](customer-requirements-zastavkovy-jr.md).

> **Nejblíž na řadě:** projít a protřídit backlog (Jan 30. 10.), pak relace AD (sekce 🚋 níž). `B56` sloučení kódu agend JŘ
> je uzavřené 30. 10. (archiv). Rozdílový linkový JŘ (`B192a-f`) je hotový 17. 10.; od 23. 10.
> ho jízdní řády (Nástroje → Porovnání linek) tisknou rovnou jako PDF v prohlížeči, šablona není veřejná; mezery proti vzoru v [`§19.6`](linkovy-knizni-jr-implementace-analyza.md).
> ⚠️ **Ze zastávkových tiketů práci neber bez ověření** - `#12843` byl 15. 9. zavřený (odpověď
> s výsledky hovoru z 11. 9.). Evidence u něj ještě 3. 9. vedla tři body jako zbývající, ačkoli
> hotové byly; než z ní něco vezmeš, přečti celý záznam, ne jen odstavec „Zbývá".

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
  a stav: [`qr-linkovy-jr-pro-cestujici.md`](qr-linkovy-jr-pro-cestujici.md). Patří k tomu **jedno pole čísla
  zastávky pro adresu v QR ve všech agendách** (dřív `B193h`): dnes ho každá agenda nese jinak, pole se vybere
  podle tvaru adresy.

- **`B185` Stránky dokladu jako pojem šablony** [L] (Jan 25. 9.). Pojmenované stránky (pevná /
  průběžná), pás stránek v návrháři, hlavička a patička s volbou stránek; nahradí „Začít na nové
  stránce" i „Jen první / poslední". Mění formát šablony (migrace). Až budou
  hotové jízdní řády a přijdou doklady typu faktura nebo stazka. Mock viz `B184`. `B184`, `B186` a `B187` hotové, dál podle poptávky.

- **`B179` AI import z PDF pro linkový JŘ** [L] (Jan 24. 9.; první verze 2. 10.). Funguje, ale
  polohy tabulky a poznámek jsou odhad modelu. **Od 9. 10. jen interní nástroj** - převody předloh
  dělá M-line; dál až se při nich ukáže, že šetří čas. Zbývají kroky **(2)** model pojmenuje bloky
  z PDF a **(3)** tabulka měřená z PDF celá. Stav a proč: [`architecture.md §Import z PDF`](architecture.md).

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

- **Externí datové zdroje** (na poptávku) - architektura je připravená, engine zná jen
  `Template + DataContext` a ten je zdrojově agnostický JSON. Nejpravděpodobnější nosič je
  **headless tisk** („pošli šablonu a JSON, vrať PDF"); samostatně by prvním krokem byl generický
  JSON zdroj ze souboru nebo URL. Dořešit přihlašovací údaje a odvození schématu z dat
  (analýza 12. 7., detail v historii roadmapy).

- **Sjednocené věci definovat jednou** [M] **Jan** (27. 10.) - nejdřív revize toho, co `B194` sjednotilo (popisky,
  nápovědy a rozsahy polí E8, údaje dopravce D4, nabídka podle dat E5, kanon `ux-conventions §8`), a pro každé
  rozhodnout, kde má extrakce na jedno místo smysl a kde stačí drift test. Kandidát: panel návrháře bere popisky
  a rozsahy ze schématu formuláře `*Edit.cls` místo opisu v `GetPropertyDescriptors` (dnes se píše dvakrát).
  Strana návrháře, navazuje na E3 / E13; kód agend sjednotil `B56`.

- **Nová pole v jízdních řádech, dohodnutá s kolegy 10. 8.** [S] - **čeká na kolegu**; v lokálním zdroji
  nových jízdních řádů zatím nejsou (29. 10.). **Cíl** u zastávky pro každý směr (TR5, ověřit, že po
  zastávce a směru), **odsazení zastávky** zvlášť pro oba směry (TRANSDEV #7937, pozastavený; trvá zákazník
  i na odsazení podle tarifních čísel?), **název směru** u linky (TRANSDEV #9088). Náš díl: pole přečíst
  a nabídnout v šabloně. Detail [`customer-requirements §TR`](customer-requirements-zastavkovy-jr.md).

- **P2 (z `B194c`)** Totéž rozdělení sloupců má ve vývěsce a v linkovém JŘ jiné popisky voleb (zamčené) [M]
  **Jan** - čeká, až kolegové uklidí nastavení tisku v jízdních řádech (domluveno 29. 10.; co agendy čtou
  a odkud: [`nastaveni-tisku-jr.md`](nastaveni-tisku-jr.md)). Definice pak patří do `Jr/Parametry.cls` (stálý kód
  vedle popisku). Detail [`legacy-nahrada.md §4`](legacy-nahrada.md)

- **Varianty tras i ve vývěsce MHD** [L] - sloupec „Varianty tras" v Seznamu zastávek umí jen AD
  (výpočet variant po trasách, písmena, pět parametrů). Jan je chce i v MHD (27. 10.): přenést výpočet
  na trasy a body MHD a ověřit proti datům, pak v `ZastavkoveJrMhd` přepnout `VariantyTras = 1` -
  nabídka sloupce se ukáže sama.

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
| F8 | **Pilot: Vozové listy (MHD)** - 5 polí, 2 tabulky, instance = oběh, volá MHD2Linky; vlastní třída zavolá starý `getSadu`/`getData` a převede výstup, registr se nemění. Narazí na zvýraznění řádku z dat (`F3`, R3). Před vydáním text „linka" napevno (`B194e` A5, pod tabulkou) | ⬜ po relacích |
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

- **Slovo „linka" napevno** (`B194e` A5) [M] - asi 25 textů (menu „Zkontrolovat vybrané linky…",
  hlášky „Žádná vybraná linka nemá data", průběh kontroly). Udělat **před vydáním první agendy bez
  linek** (pilot `F8`; ukázkové Faktury jsou od 30. 10. smazané) - skloňovat podle názvu instance agendy;
  okno výběru už ho bere (Jan 27. 10.).

---

## 🗄️ Hotová práce

Detail dokončených etap → [`roadmap-archive.md`](roadmap-archive.md). Průběžný changelog → [`changelog.md`](changelog.md).
