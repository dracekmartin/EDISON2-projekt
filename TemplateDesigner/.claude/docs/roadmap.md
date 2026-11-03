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

- **Výstup jízdního řádu do HTML** [M-L] (podnět Jan 30. 10.) - starý tisk měl kromě PostScriptu i HTML variantu.
  DPKV má na webu zastávkové JŘ MHD jako statické stránky (stránka na zastávku a směr, odkazy mezi zastávkami,
  [`dpkv.cz/_data/d-1/1130.htm`](https://www.dpkv.cz/_data/d-1/1130.htm)) a ptali se, jestli by šly generovat automaticky.
  Otevřené: HTML ze šablony návrháře (sazba je bez SkiaSharp, chyběl by jen výstup do HTML), nebo jednoduché stránky
  přímo z dat agendy; jak se soubory dostanou na web; jestli to nepokryje Vyhledávač odjezdů v ISYCloud.

- **`B190` Tisk z modulu jízdních řádů bez návrháře** [L] - **do příští verze (Jan 2. 11.)**, obě strany děláme my.
  Okno JŘ → Tisky → „Tisk linkového JŘ (nový)": formulář se šablonou, parametry a složkou, PDF bez návrháře.
  Hotové kroky 1-3 (linkový JŘ AD); položka je zákazníkům skrytá, na DEV ji zapne `^JRR("TiskNovymNavrharem")=1`.
  Krok 4 (2. 11.): výchozí šablonu nastavuje okno šablon (Ctrl+O); do vydání je v okně schované vše o tisku (tlačítko,
  řádek „Pro tisk" ve stromu, sloupec a tučná šablona v seznamu - Jan 3. 11., mátlo). Vrátit s popisem, k čemu slouží.
  Zbývá: odladit nastavení výchozí šablony (např. u varianty ve stromu), výchozí šablona M-line i pro ostatní agendy JŘ
  (dnes jen linkový AD), zastávkový JŘ. [`R4`](architektonicka-rozhodnuti-agendy.md)

- **Okno „Změnit agendu" a okno šablon (Ctrl+O) dělají podobnou věc a vypadají jinak** [M] (Jan 3. 11., **rozmyslet způsob**).
  Obě vybírají agendu stromem nebo seskupeným seznamem. Návrh: jedno okno šablon se stromem a tlačítkem „Nová šablona
  v této agendě", čip agendy a Ctrl+Shift+A by ho otevřely s vybranou aktuální agendou.

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

- **Externí datové zdroje** (na poptávku) - architektura je připravená, engine zná jen
  `Template + DataContext` a ten je zdrojově agnostický JSON. Nejpravděpodobnější nosič je
  **headless tisk** („pošli šablonu a JSON, vrať PDF"); samostatně by prvním krokem byl generický
  JSON zdroj ze souboru nebo URL. Dořešit přihlašovací údaje a odvození schématu z dat
  (analýza 12. 7., detail v historii roadmapy). Nabídku SQL a CSV z úvodní obrazovky Jan 3. 11. před vydáním
  odstranil - není v plánu; vrátí se, až se do něj dostane.

- **Nová pole v jízdních řádech, dohodnutá s kolegy 10. 8.** [S] - **čeká na kolegu**; v lokálním zdroji
  nových jízdních řádů zatím nejsou (29. 10.). **Cíl** u zastávky pro každý směr (TR5, ověřit, že po
  zastávce a směru), **odsazení zastávky** zvlášť pro oba směry (TRANSDEV #7937, pozastavený; trvá zákazník
  i na odsazení podle tarifních čísel?), **název směru** u linky (TRANSDEV #9088). Náš díl: pole přečíst
  a nabídnout v šabloně. Detail [`customer-requirements §TR`](customer-requirements-zastavkovy-jr.md).


---

## 🚋 Relace a vozové JŘ

**Relace** jsou jen AD a nejsou slovenské: odjezdy nebo příjezdy z jedné zastávky seskupené podle cílové zastávky, v řádku buňky spojů.
Starý modul je tiskne dvěma cestami: Windows šablonou se 448 vlastnostmi (tři čtvrtiny je rozmístění údajů v buňce) a postskriptem s 12 volbami a buňkou čas + platnosti + jeden doplněk.
Návrh bere kostru z postskriptu a z Windows jen to, co postskript neumí: prvek asi s 10-12 poli, asi 5 parametrů sestavy.
Používá je sedm zákazníků; otevřené tikety BBSK `#13070` (vzor, pozastavený), `#13693` (vývěska pro každý označník) a SADZV `#13654` (převod šablon).
**Vozový JŘ** je jen MHD - tisk DPKV v MHD2Tisk s vlastním kreslením, vydaný 27. 10.; vozové listy (MHD) a turnus pro řidiče (AD) jsou sady report generátoru a patří do sekce níž.
Rozbor 30. 10. (definice, obě cesty tisku, data a oracle, šablona, návrh, zkušební data VS_ZGBUS): [`legacy-nahrada.md`](legacy-nahrada.md), mapa legacy generátorů §3a-3g.

| Co | Stav |
|---|---|
| **Rozhodnutí před stavbou relací** - jak volná bude buňka (pevná kostra z postskriptu, nebo volné rozmístění údajů z Windows), „název pro tisk" výchozí zastávky, kniha; podklad je porovnání obou tisků na VS_ZGBUS a šablony BBSK a SADZV (§3f) | ⬜ rozhodnutí Jan |
| **Relace AD** - agenda (instance = relace ve skupině linek, data čte z `^JRi`, paritu ověří proti starému Windows tisku) a nový prvek (řádky cílových zastávek, buňka = čas + platnosti + doplňky, zvýraznění buněk podle druhu spoje a dopravce) | ⬜ další na řadě, po rozhodnutí |
| **Šablony relací zákazníků** (SADZV `#13654`) - převod, nebo nové šablony | ⬜ s `F10` |
| **Vozový JŘ DPKV** do návrháře - jediný zákazník a čerstvý vlastní tisk | ⬜ na poptávku |

---

## 📊 Report generátor (náhrada REPORTGENERATOR)

> ⚠️ **Gate před `F2`: R3 (prezentační nápovědy v datovém kontraktu + aditivní evoluce)** -
> [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md).

27 agend starého report generátoru s asi 85 datovými sadami; agenda report generátoru je modul (AD, MHD, mzdy, pokladna…) a teprve jeho sada odpovídá naší agendě.
Agendy dodávají jen data a kreslí obecný kód - zvláštní prvky jako u jízdních řádů tam nejsou.
Kreslení je proto hlavně technický převod (tabulka s řádky z dat `F3`); koncepčně nové je spouštění tisku, výstupy a šablony zákazníků (`F9`, `F10`).
Dopravní agendy narazí jen na `F3`, mzdy, pokladna a faktury hned na `F9` a `F10`.
**Ne big-bang**, pilot podle R7. Rozbor 30. 10. (agendy, sady, mapování, pilot): [`legacy-nahrada.md`](legacy-nahrada.md) §3.1.

| # | Co | Stav |
|---|---|---|
| F7 | **Pojmenování agend** - sada = naše agenda podle výstupu (`VYPLATNI_PASKA`), modul se stejným výstupem = varianta (`MZDY2`); kód agendy se po vydání nemění, proto před první agendou | ⬜ rozhodnutí Jan |
| F8 | **Pilot: Vozové listy (MHD)** - 5 polí, 2 tabulky, instance = oběh, volá MHD2Linky; vlastní třída zavolá starý `getSadu`/`getData` a převede výstup, registr se nemění. Narazí na zvýraznění řádku z dat (`F3`, R3). Agenda vyplní `InstanceTvary` (oběh) a `InstanceRod` | ⬜ po relacích |
| F2 | **Obecný převodník vs čisté agendy** - rozhodnout po pilotu; převodník potřebuje registr, který vypíše dvojice agenda a sada, a varianty se stejným tvarem dat neumí | ⬜ rozhodnutí po `F8` |
| F3 | **Tabulka s řádky z dat na úroveň RG** (navazuje na `B76`) - řádky řízené daty, overflow, mezisoučty, seskupení řádků a vnořená data; ⚠️ tvar odvodit z reálných sestav RG. Rozpis [`legacy-nahrada.md`](legacy-nahrada.md) §5 etapa 4 | ⬜ L |
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
