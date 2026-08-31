# Roadmap

> **Jen otevřená / akční práce.** Hotové → [`roadmap-archive.md`](roadmap-archive.md), průběžný
> changelog → [`changelog.md`](changelog.md), stav tiketů a věci „jen na poptávku" →
> [`customer-requirements §0`](customer-requirements-zastavkovy-jr.md).

> **Nejblíž na řadě:** **`L15`** - mřížka odjezdů (⚠️ nejdřív projít existující zastávkové šablony a rozhodnout otázku návrhu, viz řádek). Ze `L11` zbývá jen rodina atributů a ta čeká na překladovou tabulku glyfů.
> Vlna `L6` je hotová celá: šipka, obousměrný tisk i turnusy ke spojům.

---

## 🚆 Linkové a knižní JŘ (náhrada TiskJR)

> ⚠️ **Gate: R1 (sdílené tabulkové jádro), R2 (layout zodpovědnosti), R5 (embedded fonty)** -
> [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md).

Legacy `TiskJR` generuje **vývěsní i knižní JŘ z téhož enginu**, takže je to jeden track. Formát:
sloupcová tabulka celé linky (svislý seznam zastávek × sloupce spojů s časy; řádek platností se
symboly/NZ; Vyčká/Navazuje; skupinová záhlaví PD/So/Ne; 10 druhů poznámek).

**Na čem zbývající vlny stojí** (kontext, ne seznam úkolů): datové jádro
(`LinkovyJrAd`/`LinkovyJrMhd`), `LineTableElement` s plánem řádků, síť pro porovnání se starým
modulem (oracle `mkPLinWin`) a systémová výchozí šablona pro AD. ⚠️ **Pro MHD výchozí šablona
zatím není** - a nemá se dělat dřív, než městská agenda projde porovnáním se starým modulem
(dnes je skrytá). Byla dosud součástí `L6`, ale není to práce na repertoáru tabulky; patří
k městské agendě a **teď se neřeší** (rozhodnutí Jan 28. 8.).

⚠️ **Linková AD agenda je od 24. 8. zveřejněná** (`Released = 1`, žádost Jan) - bez toho na ni
nešlo v návrháři ani překliknout, takže nebylo jak zkoušet. **Městská zůstává skrytá**, neprošla
porovnáním se starým modulem. Pozor: zveřejněná agenda se ukáže i zákazníkům, jakmile se vydá.

⚠️ **Agendy jsou pořád úvodní draft**, takže se v nich smí měnit i to, co je jinde
zamčené - zastávkové se přitom nesmí dotknout. Slovník polí je srovnaný s vývěskou a hlídá ho
`AgendaSchemaDriftTests`; nové pole přidávej pod týmž názvem, jaký pro tutéž věc používá vývěska.
Do budoucí **MHD** šablony: `{CISLO_LINKY}` je **licenční** číslo, veřejné je
`{LOKALNI_OZNACENI_LINKY}`.

**Vlny se berou v pořadí a každá se poměřuje se starým modulem** (rozhodnutí Jan 19. 8.), **nejdřív
AD, MHD až po něm**: porovnání pro AD běží, AD má výchozí šablonu a oba zákaznické vzory jsou AD.
⚠️ **„Dodělám AD a MHD spadne samo" neplatí** - AD je nadmnožina v obsahu tabulky, ne v konceptech
(MHD má licenční × veřejné číslo linky, typ provozu jako datový pojem a vlastní vrstvu nastavení),
takže MHD si nese **vlastní inventář a vlastní oracle**.

⚠️ **Schéma `LinkovyJrMhd` je už teď citelně chudší než `LinkovyJrAd` u zastávkových řádků**
(zjištěno auditem 31. 8.) - `GetSchema` u MHD deklaruje jen `PORADI, TYP, NAZEV_ZASTAVKY, ZONY`
(`LinkovyJrMhd.cls:73-81`), AD má navíc `CZST, AKTUALNI_ZASTAVKA, TC, MHD, NASTUPISTE`
(`LinkovyJrAd.cls:100-116`) a `BuildZastavkaRadek` (`LinkovyJrMhd.cls:452-465`) chybějící pole
opravdu nikam neplní. Zvýraznění zastávky (`AKTUALNI_ZASTAVKA`) tak pro MHD dnes **nemá kam
zapsat příznak**, i kdyby přibyl parametr `ZVYRAZNIT_ZASTAVKU` (`GetParameters` u MHD je zatím
prázdné, `LinkovyJrMhd.cls:131-135`). Dokud je MHD skrytá, nikoho to netlačí - ale až se sada
voleb pro městské linky (výše) začne řešit, tohle schéma se musí dorovnat **první**, jinak
zvýraznění a další zastávková pole tiše nepůjdou udělat.

📄 **Zkušební vzor pro celý track: linka 940 Kroměříž-Přerov (IDZK)** - reálné zadání zákazníka,
které starý modul nedokázal vyrobit (analýza §15). Až bude šablona stát, ověřuj proti němu.

⚖️ **Jak se nakládá s nastavením starého modulu** (platí dál pro `L3`+): **co uměl starý modul, musí
umět i nový**, a nastavení se **neignoruje proto, že je to vzhled** - když si ho zákazník u linky
nastavil, tisk ho musí respektovat. Podle toho, co dělá: **mění data** → načtení dat · **volba
uživatele** → parametr sestavy · **něco schovává** → třístav „Automaticky (dle linky)"
([`ux-conventions §8`](../rules/ux-conventions.md), i s důvodem, proč je výchozí hodnota konstanta)
· **stránkování / chybějící featura** → `L5`/`L6` · **měřicí řetězce a šířka plochy** → vědomě nikdy.
V prvku k tomu patří pravidlo **prázdný sloupec se netiskne**.

🧭 **Vůdčí linie:** *to, jak je výstup složený, patří do návrháře; z nastavení u linky se bere jen
to, co mění data.* **Sporné případy řeš směrem do šablony** - starý modul se přepisuje a část
nastavení se může přesunout. Rozhodnutí položku po položce, jejich důvody a změřený výskyt
nastavení v datech: [`§13.4`-`§13.6`](linkovy-knizni-jr-implementace-analyza.md); pozastavené
tikety helpdesku (8 požadavků se slíbeným „vyřešíme v novém návrháři") [`§14`](linkovy-knizni-jr-implementace-analyza.md).

⚠️ **Sada voleb pro městské linky se rozhoduje až po AD** - starý modul u nich většinu voleb
ignoroval (jeho MHD engine čte tři z jedenácti), takže parita tam nemá čím se řídit a jakákoli
sada voleb je **nová featura**, ne dorovnání.

🎯 **Pořadí prací (rozhodnutí Jan 24. 8.): nejdřív dotáhnout paritu se starým tiskem, teprve pak
uživatelské volby a vzhled.** Důvod: dokud se data liší, není co procházet z business hlediska -
každý nález by mohl být „ještě to neumíme" místo „takhle to má být". Pořadí vln bylo proto
**testovací linky → `L9` → `L6`**; první dvě jsou za námi, ze `L6` je hotová `L6a`.

✅ **Průchod testovacími linkami je hotový** (26. 8., detail v archivu). **Datový rozdíl proti
starému tisku nenašel žádný** - jediný nález je chybějící **obousměrný tisk** (patří do `L6`).
Co z něj zbylo neprojité, je v [§ K ověření](#k-ověření-na-reálné-sestavě) níže.

| # | Vlna | Vel. |
|---|---|---|
| **L8** | **Dedup Caché tiskových enginů** (dřív `B14a`) - doprovod ostrého knižního JŘ, ne dopředu. Různé signatury `AddAnotace`/`BuildPoznamka`, ~15 helperů MHD↔AD + AD `^JRL` readery → sdílené readery a `Build*` do `BaseAgenda`/`JrShared`; Caché analog R1 extrakce jádra (`L5` řeší .NET render-side). Korektnostní riziko, proto **až nad hotovou sítí z `L1`**. Menší výsek = `B56` v Backlogu; při realizaci jednoho zredukovat druhý. ⚠️ Korekce z analýzy 5. 8.: z 27 stejně pojmenovaných helperů je jen 15 doslovných kopií (~97 řádků), a tvrzení o odlišném chování `CasHodnota` **neplatí**. | L |
| **L14** | **Dvě sebeoznačené R6 nejistoty v `LinkovyJrAd.cls` visí jen v komentářích, mimo dohled** (audit 31. 8.). (1) `VetaAtributu` (`:1601-1603`): legacy počítá spoje s atributem přes **celou linku**, náš kód přes **stránku** (jeden směr) - u jednosměrné linky to vyjde stejně, u obousměrné (hotová od `L6`, 31. 8.) se může rozejít věta „všechny spoje mají BEV" vs. výčet; neprošlo oraclem. (2) `GenerickaVeta` (`:1562-1564`): oprava textu pro atributy POS a IDS je opsaná ze zdroje legacy, ale **neověřená** - na kontrolních linkách se ty dva atributy nevyskytly. Autor obojí správně napsal jako neověřené (R6), jenže bez zápisu do roadmapy to nikdo nezvedne, dokud si nestěžuje zákazník s obousměrnou linkou nebo POS/IDS atributem. Vzít při hledání dat s obousměrným tiskem, nebo až se najde firma používající POS/IDS. | S |
| **L11** | **Zbytek rodiny atributů spoje se tiskne zkratkou, ne piktogramem** (IDS, NKA, OBC, POS, RBU, SAO, RBP). Zalomení značek po dvou i inverzní negativní značka jsou hotové 31. 8.; tohle je zbytek. ⚠️ **Chybí překladová tabulka „zkratka → glyf"** - starý modul ji má ve windowsím tiskovém klientovi, který v repu není, takže z tabulky glyfů se dá odvodit jen to, co má jednoznačný tvar (kolo, kufr, zásuvka - ty už doplněné jsou). U občerstvení má font dva kandidáty (káva i příbor) a není čím rozhodnout. **Navíc není na čem to ověřit**: v datech měřených firem není z celé rodiny nastavené nic než BEV, takže oracle vůči starému tisku nevznikne. Vzít, až se najde buď ten překlad, nebo firma, která atributy používá. | S |
| **L9-R3** | **Oddělená barva a tloušťka vodorovných × svislých čar** (odloženo, dřív viselo u `L10`). `GridStyle` má dnes jednu barvu i tloušťku pro obojí a je **sdílený se všemi tabulkovými prvky**, takže se to má naplánovat pro všechny naráz, ne rozšiřovat jen u linkové tabulky. Zapnout/vypnout jednotlivé druhy čar už jde (`L10`), tohle je krok navíc. | S |

### Nálezy k opravě mimo TD

| # | Co | Vel. |
|---|---|---|
| **T1** | **Sdílený Caché runner sčítá i cizí běhy** (změřeno 28. 8.). `ALVA.EDTesting.TestRunner.RunPackage` iteruje **všechny** výsledky, které v `^UnitTest.Result` vzniknou po jeho startu, a dedupe dělá jen na (třída, metoda) - takže když na témže dev serveru běží současně jiná sada (kolegův pre-push hook), přičte si její testy. Doklad: dva běhy hlásily **186/186** místo 128, rozdíl 58 = `EDISONMAIN2` (23) + `EDControls` (35). ⚠️ **Nejde jen o nafouknuté číslo** - stejnou cestou si běh může přivlastnit i **cizí selhání**, takže se hledá chyba, která není naše. Oprava je malá: filtrovat výsledky podle seznamu tříd, které runner sám spustil. Je to v `EDTesting/`, tedy **sdílená infrastruktura mimo TD** - nechávám na rozhodnutí, kdo to vezme. | S |

### Mimo paritu

Věci, které starý modul **neumí** - nejsou to dluhy, ale featury. Vědomě mimo paritní vlnu
(rozhodnutí Jan 28. 8.), ať se neplete „dorovnáváme starý tisk" s „přidáváme něco nového".

| # | Co | Vel. |
|---|---|---|
| **N1** | **Zóny do více sloupců u hraniční zastávky** (#8576, KU HK). ⚠️ **Starý modul zóny do sloupců nedělí** - má na ně jeden sloupec, který jen zesílí podle délky obsahu (vzor 940 je má v jednom sloupci oddělené čárkou). Zákazník chce u hraniční zastávky mezi zónami jednoho IDS (PID, IDZK) samostatné sloupce, jako to máme u km. Na naší straně levné (km už rozpad na podsloupce dělá), ale agenda musí poslat zóny jako **seznam**, ne jeden text. | S |

### K ověření na reálné sestavě

Ne implementace - **projít a zavřít**. Vzniká to tím, že návrhář umí věc, kterou starý modul neuměl,
takže nejde o práci, ale o doklad.

| Co | Proč to čeká |
|---|---|
| **Vícesloupcová legenda (#7487)** - zákazník chtěl čtyři sloupce | Prvek umí 1-4 sloupce včetně zalamování (zjištěno 26. 8.); výchozí je 1, proto si toho nikdo nevšiml |
| **Poznámky vedle tabulky, ne pod ní (#11373, KAD)** | Legenda je samostatný prvek, takže to umíme dnes; ověřit a tiket zavřít |
| **Pořadí sloupců spojů na `CB_CSADAD 1 / 0370618`** | Starý tisk dává oba směry do jedné tabulky, takže není co k čemu přiložit. ⚠️ **Odblokováno 28. 8.** - obousměrný tisk je hotový, takže tuhle linku už je čím porovnat. Vzít při nejbližším průchodu doklady |
| **Písmena ze zóny (`VS_CSAD 5 / 0771901`)** | V testovacích datech pro Vsetín na to není vzorek; firma navíc není v přepínači firem. Vzít, až ta data budou |
| **Živý náhled (`GetMockData`) ignoruje obousměrný tisk, turnusy i vlastní definici sloupců** (`LinkovyJrAd.cls:460-469` volá `BuildStrankaLinky` napevno s `deleni=2`, bez `defSloupcu`/`obousmerne`/`sTurnusy`) | Podle kontraktu `BaseAgenda` je mock jen ukázková data bez parametrů, takže to nejspíš je v pořádku - ověřit, jestli uživatel v návrháři čeká, že se mu obousměrný přepínač promítne do náhledu, a je zklamaný, že ne (zjištěno auditem 31. 8., nejistá závažnost) |

---

## 📥 Backlog

### Ostatní

- **`B105` Prvek odjezdů nejde vykreslit v testu** (z `L15`, 31. 8.). Ručně složená data
  (skupiny dnů + odjezdy) se načtou správně - `GetAvailableSkupinyDnu` vrátí 2 skupiny, `ODJEZDY`
  má 4 řádky - ale `Render` nevykreslí **ani pixel**, takže obrazové testy měří prázdné plátno
  a projdou naprázdno. ⚠️ **Není to kosmetika:** je to jediný tabulkový prvek, který takhle nejde
  testovat, a proto jsou 4 testy z `L15` v repu **vypnuté** (`Skip` s důvodem) - chování mřížky
  odjezdů dnes nehlídá nic než oči. Příčina nenalezena v rozumném počtu pokusů; žádný jiný test
  tenhle prvek nekreslí, takže není podle čeho se řídit. Vzít, až na prvek odjezdů někdo sáhne. | S |

- **`C8` Vejdi výstup na zadaný počet stran** (M, podnět POVED #8785 bod 5 - **podnět, ne závazek**).
  Starý modul to odmítl jako principiálně nemožné, náš engine má lepší pozici (`Strih` už umí režim
  „nejmíň stránek"). Než se do toho pustí, má se zákazníka zeptat, jestli to pořád chce - jinak
  stavíme featuru, kterou nikdo nezadal.

- **`C9` Ručně zadaný „název směru" u linky jako datové pole** (#9088 bod 3, chtěné i pro vývěsky).
  ⚠️ **Nejde udělat u nás** - vyžaduje **nové pole u linky v jízdních řádech**, tedy součinnost
  s kolegy z JŘ. Náš díl je malý (pole projít schématem a nabídnout v šabloně), ale bez toho pole
  nemá kam sáhnout.

- **`B102` Filtrování dopravců v hlavičce** (z důsledku zrušené vlny `L7`, 25. 8.). Sdružování linek
  není naše práce, ale zákazníkův případ z #9417 (souhrnný JŘ dvou linek s dlouhým výčtem dopravců)
  se k nám může dostat jako **jedna linka s mnoha dopravci** - tedy bez čekání na cokoli dalšího.
  Až se to stane, filtrování dopravců přestane být odložené.

- **`B101` Není poznat, jestli je jedna skupina dnů nastavením, nebo daty linky** (z 25. 8.).
  Prvek pás skupin nekreslí, když vyjde jediná skupina - a uživatel pak nemá jak poznat, jestli
  si dělení nastavil špatně, nebo jestli tak prostě vypadá linka (na 0330511 jedou všechny spoje
  jen v pracovní dny). Otázku „mám to vůbec nastavené dobře?" dnes **nejde zodpovědět z aplikace**.
  Drobné zlepšení, ne chyba.

- **`B100` Ruční zlom stránky na tarifním čísle** (`PATCH,ZLOM`, z `L5` 25. 8.).
  U linky jde v jízdních řádech nastavit tarifní číslo, na kterém se má tisk rozdělit; starý modul
  to respektuje, my ne, takže se takové linky tisknou jinak. **Odloženo kvůli frekvenci**
  (rozhodnutí Jan 25. 8.): nastaveno na **3 linkách z ~93 tisíc** (měření 21. 8.), což je za cenu
  zásahu do serveru i klienta slabý důvod. ⚠️ **Neblokuje nic dalšího** - klientská strana pro
  zlomy existuje (`INedelitelneDily`, silné a slabé zlomy) a vynucený zlom by k ní jen přibyl;
  na drátě by šlo o jedno pole navíc, tedy aditivní změna. Vytáhnout, až si o to někdo řekne,
  nebo až se bude stejně sahat na přenos nastavení linky.

- **`B99` Loď, wifi i zásuvka jsou ve fontu - ve vývěskách se pořád nepoužívají**
  (z `L4` 24. 8., **v linkovém JŘ vyřešeno 31. 8.**). ⚠️ Dvakrát tu stálo něco nepravdivého:
  nejdřív že ve fontu nejsou všechny tři, pak že chybí aspoň zásuvka. **Jsou tam všechny** -
  tabulka glyfů obou fontů je našla shodně na 180 (loď), 186 (wifi) a 179 (zásuvka), v souvislém
  bloku ikon vybavení vozidla. Linkový JŘ je od 31. 8. kreslí piktogramem. **Ve vývěskách zůstávají
  textem**, protože přepnutí by změnilo hotový tisk - je to rozhodnutí o vzhledu, ne oprava.
  V datech měřených firem tyhle značky nemá nikdo, takže to nikoho netlačí.

- **`B98` Nastavení starého tisku, která nikdo nemá zapnuté** (evidence, ne práce; z měření 21. 8.,
  [`§13.6`](linkovy-knizni-jr-implementace-analyza.md)). Na 93 133 linkách šesti firem nejsou nikde
  nastavené: **tvar tisku místenkování**, **vlastní texty sloupce MHD** místo „IDP-P"/„IDP-Z",
  **tisk provozů dopravce**, čtyři přepínače **schovávání u linky** (`MHD2,netisknout*`) a **celá
  vrstva pod linkou** (řádek se v knižním JŘ netiskne, i zvlášť per směr; umělá sekce u spoje)
  a **tři přepínače řazení spojů** (`PS_PrintPorSpTR`, `PS_PrintSouhrnPorSpPri`,
  `PS_PrintSouhrnPorSpTR` - řadit jen podle odjezdu, resp. jen podle příjezdu u souhrnných linek;
  souhrnných linek je 33, ale bez těch přepínačů se chovají stejně jako ostatní).
  Řada z nich vznikla v legacy **pro jednoho zákazníka** a nikdo jiný je nepoužívá. Vytáhnout, až
  si o to někdo řekne - a pak počítat s tím, že **doklad z dat nebude** a půjde jen o implementaci
  proti čtení legacy zdroje. ⚠️ U skrývání řádků platí, že **ubírá řádky** - zákazník to pozná na
  první pohled, takže se k tomu má vyrobit vlastní testovací případ, ne to udělat naslepo.

- **`B66` Vlastní bloky prvků („moje komponenta")** (M, podnět Jan 12. 8.). Uživatel si vybere pár
  prvků (typicky svou hlavičku sestavy), uloží je pod jménem a pak je vkládá jedním kliknutím do
  dalších šablon. **Co už existuje:** uložení **celé sekce** jako vlastní šablony a seskupení prvků
  (Ctrl+G). Chybí mezistupeň mezi nimi - pojmenovaný blok menší než sekce. **Proč to čeká na pevnou
  tabulku:** dokud je hlavička poskládaná z 6 samostatných textů, musí blok řešit relativní pozice a
  vazby uvnitř skupiny; jako **jeden** prvek (pevná tabulka) je uložení skoro zadarmo. Otevřené:
  kde bloky žijí (per uživatel × firma jako šablony sekcí, nebo sdílené v DB) a jestli se vkládají
  z palety, nebo z nabídky „Přidat". **Pohltí i připravené bloky agend** (dřív `B77`): server pro
  každou agendu drží hotové sestavy tabulek („Záhlaví linky", „Shrnutí faktury"), ale od zrušení
  starého dialogu k nim z UI nevede cesta - je to titíž „pojmenovaný blok k vložení", jen dodaný
  námi místo uživatelem, takže samostatnou cestu do UI nedostane (rozhodnutí Jan 17. 8.).

- **`B18` Deklarativní pravidla EDInputFormu** (M, odloženo - rozhodnutí Jan 5. 8.: patří k velkým
  věcem do budoucna, ne do běžné fronty; je to **kapabilita sdílené komponenty pro celý EDISON**,
  ne featura návrháře). Dnes umí formulář reagovat na vyplněné hodnoty jen oklikou přes server
  (`Validate`, ručně per formulář). Zavést deklarativní vrstvu: začít **viditelností** (`visibleWhen`),
  gramatiku sdílet s tiskovými parametry (`POLE=hodnota` + nerovnost) - nezavádět druhý dialekt.
  Povinnost/povolení až s poptávkou; **skutečná validace zůstává imperativní** (potřebuje data
  a dopočty). **Follow-up:** migrace `CellStyleEditor` na Caché-driven formulář (S–M; háček je
  tri-state / reaktivní logika - bez `B18` nejde). **Follow-up 2:** rozšířit gramatiku o čtení
  **parametru sestavy**, aby šlo zašednout pole prvku mrtvá bez parametru (6 polí šipky vs.
  `TISK_SIPKY`, projeté zastávky vs. `TISK_PROJETYCH_ZAST`) - dnes to plní jen helpTexty.
  **Follow-up 3:** parametr sestavy nemá pole pro **popis**, takže krkolomné labely
  (`SCHOVAT_V_ROZSAHU`, `ROZTAHNOUT_NA_VYSKU`) nejde vysvětlit. Aditivní klíč na wire + tooltip -
  dělat spolu s gramatikou, ne zvlášť kvůli dvěma tooltipům.

- **`B56` Sjednocení opsaných pomocných funkcí agend** (odloženo - **rozhodnutí Jan 5. 8.**:
  nedělat předčasnou deduplikaci, dokud se tvar výstupních dat neustálí; sjednocením by se zabetonoval
  stav, který se ještě hýbe). **Podklad z analýzy 5. 8., aby se nemusela dělat znovu:** 27 pomocných
  funkcí se stejným názvem ve 2+ agendách, ale jen **15 jsou doslovné kopie** (~97 řádků k odstranění,
  ne 400-500, jak tvrdil původní odhad). 4 mají stejnou signaturu a rozešlé tělo - a to **záměrně**
  (`AddStandardJRFields` se liší v uživatelských popiscích polí: AD „Číslo linky" × MHD „Číslo linky
  (dle licence)"; `AddStandardJRParameters` nabízí jiné parametry sestavy). 8 sdílí jen název a dělá
  jinou věc - `BuildPoznamka` má dvě rodiny s **jiným datovým kontraktem** (zastávková posílá
  `KATEGORIE`+barvy, linková `DRUH`), `PopisDopravce` existuje 3× se třemi signaturami. Sloučit jde
  bezpečně **jen skupinu doslovných kopií a jen u zastávkových agend**, které kryjí zlaté odtisky
  a 46 testů helperů; **linkové agendy nemají ani jeden test** (2 000 řádků), takže tam by zásah jel
  bez sítě. Vzor existuje - `BaseAgenda` už 4 sdílené helpery hostuje.

- **Headless tisk (A3)** - „vytiskni linku X" z modulu přes PrintEngine fasádu (šablona+data→PDF
  bez stavu editoru). Dnes tisk jen v návrháři. Gate R4/R7.

- **Externí datové zdroje** (analýza 2026-07-12, na poptávku) - architektura připravená (engine zná jen
  `Template + DataContext`; DataContext je zdrojově agnostický JSON). Tři švy: Caché gateway agenda
  (0 změn .NET) · .NET provider vedle Caché (vzor `JsonFileDataSchemaProvider` + Composite) · headless
  A3 („POST šablona+JSON → PDF" = externí zdroj bez adaptéru; **A3 je nejpravděpodobnější nosič**).
  První krok s nejlepším poměrem cena/hodnota: **generický JSON zdroj (soubor/URL)** - tržní minimum
  (CSV/Excel/JSON/REST), náš wire formát už JSON je; využilo by i AI + testování šablon. Dořešit:
  credentials, schéma z dat, změkčení předpokladu „běží nad Caché".


---


## 🚋 Relace a vozové JŘ

Layout TBD - hlavička oběhu, tabulka spojů, poznámky (pravděpodobně víc elementů). Legacy:
`ALVA.MHD.ReportGenerator` (MHD) + `AD.ADREPORTGEN` (linky), viz
[`legacy-nahrada.md`](legacy-nahrada.md). **Relace (TiskJRRELACE)** - vývěsky
nádraží (SK zákazník, fork TiskJR ~50k LOC); **nejdřív ověřit živý use case**.

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
