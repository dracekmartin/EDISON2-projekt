# Konzistence agend jízdních řádů - druhé kolo (`B194`)

> Jednorázová analýza 23. 10. 2026: nezávislá revize zastávkového a linkového JŘ (AD i MHD) po uzavření
> `B193`, s důrazem na jednotné názvosloví a ovládání. Šest oblastí (parametry, nabídka dat, prvky,
> aplikace, tisk, architektura), každý nález **přeověřený proti kódu po commitu `3139b27d`**.
> **Smazat po dokončení `B194`** - trvalá pravidla do `cache-api.md` / `ux-conventions.md`,
> kandidáti sloučení kódu do [`legacy-nahrada.md §4`](legacy-nahrada.md).
> Rozdílový JŘ je v rozsahu jen tam, kde ho rozbila změna společného kódu (`C1`).

**Roadmapa** má každou položku jako jeden řádek pod týmž ID: §1-§7 tady = `B194a`-`B194g` tam. Hotovou
položku smaž tady i v roadmapě (do `roadmap-archive.md`).

**Jak číst položky.** `[S/M/L]` velikost · **tisk** = oprava změní vytištěný výstup vydané agendy ·
**Jan** = potřebuje rozhodnutí (jinak zjevná oprava) · *ověřeno:* kód / server (čtecí volání na dev
serveru) / neověřeno. Odkazy jsou jméno metody + řádek k 23. 10.; řádky se posouvají, metoda platí.

**Co je zamčené:** popisky voleb enum parametrů (ukládá se zobrazený text, `ux-conventions §8`), klíče
polí na wire a klíče vlastností v šablonách (jen přidávat), kódy agend. Popisky polí, parametrů,
skupin a formulářů se mění volně - bere je návrhář ze serveru.

**Linkový JŘ MHD jde ven ve verzi 27. 10.** Co se ho týká (`T5`, `T6`, `P4`, `P7`), je nejlevnější
opravit do té doby - zákazník pak změnu tisku vůbec neuvidí.

## 1. Chyby k opravě

Všechny hotové (C1-C12, 23.-26. 10.) - viz `roadmap-archive.md` a changelog.

## 2. Tisk - rozdíly mezi agendami

Všechno mění vytištěný výstup vydaných agend, proto **Jan** u každé. Kde je doložená parita se starým
modulem, stojí u položky; bez ní je rozdíl nahodilý.

- **T1 Vývěska AD neoznačuje spoje „na objednání" a „podmínečně"** [M] (*ověřeno:* kód; sonda
  `AtributySpojuVyskyt`: RBU 169 linek, RBP 21). Linkový AD kreslí telefon / vykřičník s vysvětlivkou
  (`LinkovyJrAd.BuildSpoj`, `AtributSablona`), vývěska nic (`ZastavkoveJrAd.NacistOdjezdy`, anotace
  :1032-1037). **Mezera proti starému modulu**, ten to tiskl (`JRapiLinPrtMhd1.mac:68-69, 231-241`,
  `JRapiExp10.mac:606-615`). Doporučení: doplnit.
- **T2 Vývěska AD nebarví obarvené spoje** [M] (*ověřeno:* kód; 3 926 spojů s barvou). Linkový AD barví
  (`LinkovyJrAd.BuildSpoj` :1429, volba „Barvy spojů"), vývěska posílá `FORE_COLOR` vždy prázdné
  (`ZastavkoveJrAd.BuildOdjezd`). **Mezera proti starému modulu** (`JRapiLinPrtMhd1.mac:56`,
  `JRapiExp10.mac:602-605`). Doporučení: doplnit se stejnou volbou.
- **T3 Text omezení u značky je na vývěsce AD jiný** [M] (*ověřeno:* kód, server CB_CSADAD 0330510).
  „nejede 24.12.22, 31.12.22" × linkový „nejede 24.12.22,31.12.22"; vývěska tiskne rok vždy dvoumístně
  (linkový podle nastavení firmy: 0 VS_CSAD, KV_DP, SKNI_MHDNITRA; 1 CB_CSADAD, ZL_KOVED; 3 CB_GWBUS), má
  předložky natvrdo místo číselníku NCOME, jiné pořadí typů omezení a nevypíše „jen v sudých/lichých
  týdnech". `ZastavkoveJrAd.SestavTextOmezeni` (:2608) × `LinkovyJrAd.SestavTextOmezeni` (:2190).
  Parita vývěsky **nedoložena**: starý vývěskový tisk volá tutéž rutinu jako knižní
  (`JRapiLinPrtMhd1.mac:155` → `JRapiNZUtl2.mac:110-135`), linkový AD je proti ní ověřený oraclem.
  Doporučení: jedna metoda, předem porovnat `ZastavkoveJrAdLegacyTest.LegacyStrankaProbe`.
- **T4 Vysvětlivka téže značky zní v každé agendě jinak** [S] (*ověřeno:* kód, server). Bezbariér
  („spoj má bezbariérově přístupné vozidlo" jen vývěska AD), překlep **„částečne"** v obou AD (starý modul
  ho měl taky), „na znamení" (linkový AD bere ze STALCOME, vývěska vědomě Jan), přestup na autobus tři
  znění (vývěska AD vlastní text `TextPrestupu`). Doporučení: překlep opravit, přestup vývěsky ze STALCOME,
  zbytek rozhodnout.
- **T11 Vývěska AD má nadpisy skupin dnů natvrdo česky** [S-M] **tisk** (*ověřeno:* kód). `NacistSkupinyDnu`
  (:851); starý tisk bral číselník firmy (`JRapiLinPrtMhd1.mac:541-571`) - nejdřív změřit, u kolika firem
  se liší. (Vyčleněno z `T5`, které je uzavřené jako vědomý rozdíl - viz §8.)
- **T7 „Jen nástup / jen výstup" chybí jen na vývěsce AD** [S-M] (*ověřeno:* kód). Ostatní tři kreslí šipku
  a vysvětlivku; vývěska je řadí mezi tiskové pokyny (`ZastavkoveJrAd.JeViditelnyAtribut` :2539). Parita se
  starou vývěskou (tiskla jen „x"), počet zastávek nezměřen.
- **T8 Zóny se počítají čtyřmi pravidly** [S] (*ověřeno:* kód). Každé je parita se svým starým modulem
  (`ZastavkoveJrAd.ZonyZastavky`, `LinkovyJrAd.BuildZastavkaRadek`, `LinkovyJrMhd.ZonyZastavky`,
  vývěska MHD bez filtru IDS). Doporučení neměnit; opravit komentář `LinkovyJrMhd.cls:533-535`
  („jako u zastávkového JŘ MHD" neplatí).
- **T9 Oddělovač v legendě vývěsek je dlouhá pomlčka** [S] (*ověřeno:* kód). `zastavkovy-jr-ad.json:201`,
  `zastavkovy-jr-mhd.json:201` (znak U+2014), linkové šablony tři mezery, výchozí prvku „ - ". Pravidlo projektu
  dlouhou pomlčku zakazuje; parita se starým tiskem neověřena.
- **T10 Drobnosti** [S]. Pokračování spoje v linkovém AD je natvrdo česky i ve slovenské šabloně
  (`LinkovyJrAd.BuildSpoj` :1649, komentář tvrdí číselník) - zjevná oprava. Stanoviště „, stan.16" (linkový
  AD, parita) × „, označník 3" (vývěska AD, bez opory); rozdíly tras „neobsluhuje zast." × „nezajíždí do"
  (parita obou) - neměnit.

## 3. Nabídka dat a parametry sestavy

Popisky a nápovědy výstup ani uložené volby nemění (návrhář je bere ze serveru). Popisky citují i nové
hlášky linky bez dat (`LinkovyJrAd.BuildStrankaLinky`, `LinkovyJrMhd.cls:420`) - při přejmenování opravit i je.

- **D1 „Aktuální zastávka" je ve vývěsce název i příznak** [S] (*ověřeno:* kód, server). Skalár
  `POPIS_ZASTAVKA` i řádkový příznak `AKTUALNI_ZASTAVKA` se jmenují stejně a nabídka je ukáže vedle sebe;
  linkové agendy název aktuální zastávky nemají vůbec, do hlavičky výlepu se vloží příznak a vytiskne
  „true". Návrh: skalár „Název aktuální zastávky" (+ „(CIS)"), příznak nechat (používají ho šablony
  a dialog sloupců); přidat `POPIS_ZASTAVKA` do linkových (**Jan**, jen přidání).
- **D2 „Pořadí" v linkovém AD je tarifní číslo** [S] **Jan** (*ověřeno:* kód). Jinde 1..N, v linkovém AD
  tarifní číslo s mezerami a ve směru Zpět sestupně (`LinkovyJrAd.BuildZastavkaRadek` :1014). Prvek podle
  něj páruje řádky, význam měnit nejde. Návrh: z nabídky stáhnout a dál posílat (jako `POPIS_PLATNOSTI`).
- **D3 Konečná × cílová, CIS na začátku × na konci, vývěska bez výchozí zastávky** [S] (*ověřeno:* kód).
  „Konečná zastávka" vedle „CIS číslo cílové zastávky", „Aktuální zastávka (CIS)" vedle „CIS číslo zastávky"
  (`ZastavkoveJrAd.AddStandardJRFields`). Návrh: „Číslo … (CIS)" jednotně, přidat `VYCHOZI_ZASTAVKA` do vývěsek.
  Zákazník hledal „cílovou" (`MigrationKonecnaZastavka.cs:10`) - kanon „konečná" potvrdí **Jan**.
- **D4 Údaje dopravce po polích má jen vývěska AD** [M] **Jan** (*ověřeno:* kód). Jinde jen hotový blok,
  vývěska MHD od `C5` (24. 10.) navíc název.
  Linkový AD má tentýž zdroj (`ZastavkoveJrAd.BuildDopravci`), MHD název, ulici, město a telefon
  (`ZastavkoveJrMhd.PopisDopravce`). Jen přidání.
- **D5 Pořadí polí v nabídce se liší** [S] (*ověřeno:* kód). Nabídka nepřeřazuje. Linkový AD má poznámky
  mezi linkou a dopravcem, typ JŘ za platností, QR uprostřed (`LinkovyJrAd.GetSchema` :98-137). Návrh:
  bloky linka → dopravce → platnost → zastávka a směr → poznámky ve všech čtyřech.
- **D6 Nejasné popisky** [S] (*ověřeno:* kód). „Popis linky" je název linky → „Název linky" (všechny 4 naráz,
  drift test). „Druh (1-10)" → „Druh poznámky". „Typ poznámky" a „Typ řádku" vrací interní kódy, u poznámek
  podle rodiny jiné (`specialni` × `znak`; **Jan**). „QR kód URL" anglicismus, „MHD/IDS" zkratka.
- **D7 Náhled vývěsky MHD bez linky je vymyšlená brněnská linka** [M] (*ověřeno:* server KV_DP).
  `ZastavkoveJrMhd.GetMockData` (:1919) má pevná data „Líšeň-Bystrc", DPMB; PDF z ukázkových dat je
  vytiskne. Struktura se liší od dat (chybí označník, nese „na znamení", které data nikdy nemají). Ostatní
  tři agendy berou první reálnou linku (rozhodnutí C-e). Návrh: totéž pro MHD.
- **D8 Drobnosti v datech** [S]. Komentář o tarifním čísle ve vývěsce AD je mylný
  (`AddStandardJRCollections` :3438). `CAS_JIZDY` deklarovaný jako číslo nese „<" a prázdno. Ukázka
  linkového MHD nenese `TYPY_PROVOZU_LINKY` (panel bez linky ukáže všechny typy firmy). Záložní text ukázky
  „(žádná linka v ^JRL…)" je technický a objeví se i u existující linky (`LinkovyJrAd.GetMockData` :704,
  `LinkovyJrMhd` :302, `ZastavkoveJrAd` :3338). Sonda `AdSloupekProbe.cls:1397` čte neexistující klíče.
- **P1 Vývěsky: osm voleb bez nápovědy, dvě nesrozumitelné** [S] (*ověřeno:* kód, server). Šipky, projeté
  zastávky, časová osa, hodiny od/do, roztažení na výšku, souhrn „na znamení", „V rozsahu nepoznámkovat
  schovat" (`AddStandardJRParameters` v obou vývěskách - opsané, opravit obě). Linkové JŘ mají nápovědu
  všude. Návrh (popisky dvou voleb **Jan**): `SCHOVAT_V_ROZSAHU` „Schovat poznámky v rozsahu
  „nepoznámkovat"" - „U spojů a zastávek, které mají v jízdních řádech nastavené Spoje, nepoznámkovat, se
  v tisku schová vybraný druh poznámek: ruční komentáře, automatické poznámky o rozdílech tras, nebo obojí."
  (význam `NoteVisibilityFilter.cs:5-9`); `SOUHRN_NA_ZNAMENI` „Zastávky na znamení souhrnně" - „Když jsou
  všechny zbývající zastávky na znamení, nevytiskne se značka u každé z nich, ale jeden řádek v poznámkách."
  (rovné uvozovky v dnešním popisku pryč); `ROZTAHNOUT_NA_VYSKU` - „Když je hodin s odjezdy méně, než se do
  prvku vejde, řádky hodin i písmo se zvětší, aby prvek vyplnily. Záhlaví zůstává."
  (`DepartureGridElement.cs:672-680`); ostatním krátká věta.
- **P2 Totéž rozdělení sloupců má ve vývěsce a v linkovém jiné volby** [M] **Jan** (*ověřeno:* server).
  „Pracovní / Sobota / Neděle+svátky" × „Pracovní dny / Soboty / Neděle+svátky", kryptické prázdninové volby
  vývěsky; vnitřní klíče jsou stejné (`PRAC_SO_NE`, `PRAC_VIKEND`). Volby jsou zamčené → přidat souběžný
  klíč `optionLabels`, `options` nechat jako text (`CacheAgendaProvider.ParseParameters` :305 by na objektu
  ztratil všechny parametry). Třetí kopie popisků v `DepartureColumnPresets.cs:20-37`.
- **P3 Volba stránky pro stanoviště je jinde a jinak pojmenovaná** [S] **Jan** (*ověřeno:* server).
  Linkový „Stránka pro každou zastávku" v Obecné × vývěska „Samostatná stránka pro každé stanoviště"
  ve skupině Seznam zastávek (s ní „Číslo stanoviště podle"). Pravidlo linkového JŘ (Jan 31. 8.): co mění
  složení sestavy, patří do Obecné. Návrh: Obecné, „Stránka pro každé stanoviště".
- **P5 Linkový AD výlepy: bez pořadí stránek a PDF na zastávku** [M] **Jan** (*ověřeno:* kód). Stránky jdou
  vždy po směrech (`LinkovyJrAd.VypisVylepy`), vývěska nabízí „Po zastávkách"; export „Jeden PDF na
  zastávku" se nabízí jen agendám druhu STOP (`AgendaParams.AddExportFields`).
- **P6 „Obousměrný tisk" svítí i při jednom směru** [S] (*ověřeno:* server). Doplnit `visibleWhen`
  `SMER_TISKU=Oba` podle vzoru vývěsky, větu z nápovědy Směru pro tisk vypustit.

## 4. Prvky a formuláře

Mění se jen popisky a struktura formulářů, klíče vlastností v šablonách zůstávají. Tady je nejvíc práce
a zároveň základ pro další prvky: **nejdřív doplnit kanon `ux-conventions §8` (Jan), pak sjednocovat.**

- **E1 Strom šablony ukazuje anglické názvy tříd** [S] (*ověřeno:* kód). `[Z0] DepartureGrid: Odjezdy auto`,
  `Rectangle: Fill=#…` (`TemplateTreePanel.CreateElementNode`, `TemplateElement.GetSummary`). Správné názvy
  dává `ElementNames.SPopisem`.
- **E2 Okna nastavení se jmenují jinak než prvky** [S] (*ověřeno:* kód). „Zastávky - nastavení", „Odjezdy -
  nastavení", „Linkový JŘ - nastavení" (proti kanonu), „Tabulka - nastavení"; Tabulka z dat má čtyři názvy.
  Návrh: „Vlastnosti - {název prvku}" všude, test rozšířit na titulky.
- **E3 Styl záhlaví a buněk má pět podob** [L] **Jan** (*ověřeno:* kód, server). Plný balíček (Seznam
  zastávek, Tabulka z dat), záhlaví za tlačítkem (Minuty), jen velikost a dvě barvy (Tabulka spojů), bez
  tučného (Poznámky). Skupina „Značky u odjezdů" má 37 polí, pořadí skupin nemá kostru (Minuty začínají
  „Skupiny dnů", Tabulka spojů „Řádky"). Návrh: kostra Obecné → specifické → Záhlaví → Buňky →
  Rámeček/Mřížka/Pozice/Podmínky, zóna stylu vždy stejným balíčkem `StyleFields`.
- **E4 Záhlaví × nadpis** [S] (*ověřeno:* server). „Záhlaví skupin dnů" (Minuty) × „Nadpisy skupin dnů"
  (Tabulka spojů), „Nadpis popisných sloupců", „Styl 2. řádku nadpisu", v dialogu Minut obojí. Kanon říká
  „Záhlaví". Přesun do skupiny Obecné **Jan**.
- **E5 Seznam zastávek nabízí v MHD věci z AD** [M] (*ověřeno:* kód). Sloupec „Sloupek" (MHD ho nemá) a
  nápověda „Více stanovišť" s odkazem na parametr, který MHD nemá. Skrývání podle agendy už mají Poznámky
  a Tabulka spojů (`ViditelnostPole`).
- **E6 „Zarovnání" vedle „Svisle"** [S]. `StyleFields.cls:34` → „Vodorovně" (kanon); „Zarovnání záhlaví"
  v Minutách je jen svislé.
- **E7 „Element" × „prvek", „ZIndex" × „Vrstva", „engine"** [M] **Jan** (*ověřeno:* kód). Společná pole
  (`BaseElementFields.cls`: „Rámeček elementu", „Uzamčený element…", „(počítá engine)", „ZIndex"), lišta
  „Elementy", krok zpět „Přidat element", dialogy sloupců. Návrh: do kanonu „prvek" a „Vrstva";
  `BorderGroup` změnit v `.cls` i C# naráz.
- **E8 Význam prázdné hodnoty pěti zápisy, různé rozsahy** [M] **Jan**. „(prázdné = auto)", „(0 = ∞)",
  „(0 = ne)", „(0 = bez limitu)", „(0=vedle)"; výška řádku 1-50 / 1-20 / 2-15 / 2-50 mm, písmo 4-200 ×
  3-24 pt. Návrh: popisek = podstatné jméno, význam prázdna do nápovědy, rozsahy sjednotit.
- **E9 Minuty × Tabulka spojů pro tentýž pojem** [S] **Jan**. „Oddělovač skupin" × „Mezera mezi skupinami
  sloupců", „Mezera minuta ↔ značka" × „Mezera mezi značkou a časem", „Velikost značek/minut" jsou poměry
  vedle „Velikost" v bodech (kanon: Velikost = pt).
- **E10 Aktuální zastávka v Seznamu zastávek** [S]. „Tučně aktuální zast.", „Podtržení aktuální zast.",
  C# a Nápověda tomu říkají „Zvýraznění" (plete se se „Zvýraznění řádků"). Návrh: skupina „Aktuální
  zastávka", „Aktuální: Tučné / Podtržení / …".
- **E11 Popisky C# a formuláře se rozcházejí** [M] (*ověřeno:* kód). Uživatel je vidí v protokolu importu
  z PDF, v náhradním panelu a mluví jimi AI („Šířka sl. hodin", „Zalamování", „Výplň"). Návrh: `G6`.
  `mvvm-patterns.md:14` tvrdí, že úprava více prvků jde přes deskriptory - umí jen 4 pevná pole.
- **E12 Katalog sloupců Tabulky spojů jinak než data** [S]. „Zastávka", „Zóna", „MHD / IDS" × data
  „Název zastávky", „Zóny", „MHD/IDS"; „TČ" × „Tč"; „popisné" × „popisové" × „sloupce popisu".
- **E13 Dialogy sloupců mají každý jiné ovládání** [M]. „+ Přidat" × „Přidat", „▲" × „▲ Nahoru" × „Nahoru",
  „×" × „Vymazat", nadpisy sekcí; Tabulce spojů chybí „Přizpůsobit sloupce". Návrh: do sdíleného
  `ColumnListEditor`.
- **E14 Tabulka spojů ukazuje interní pole** [S] **Jan**. „Kolekce zastávek/spojů/skupin dnů" s klíči
  `ZASTAVKY`… ve skupině Podmínky (`LineTableElementEdit.cls:172-174`) - skrýt.
- **E15 Texty nápověd** [S]. Tykání vedle vykání (`ImageElementEdit`, `CellStyleEditor`, `StyleEditDialog`,
  dva dialogy sloupců), „engine", „padding", „dědit od rodiče", „Dashed=3mm", v F1 „pohledna definici"
  a cesty, které v UI nejsou. Dlouhá pomlčka v komentářích `App.xaml.cs:17, 210`.
- **E16 Drobnosti** [S]. Skupina „Rozměry a mřížka" bez mřížky; „Mřížka" znamená čáry tabulky i pomocnou
  mřížku lišty; barva negativní značky dvěma popisky; Tabulka spojů má ikonu Seznamu zastávek.

## 5. Aplikace a dokumentace

- **A1 Agenda se jmenuje sedmi způsoby** [M] **Jan** (*ověřeno:* kód, server). „Zastávkový JŘ · AD" (titulek,
  filtr), „Agenda: … · Varianta: AD" (stavový řádek), „Zastávkový JŘ (AD)" (Uložit jako), dvojice sloupců,
  „AD (linková doprava)" × „MHD" (výběr agendy; plete se s Linkovým JŘ), syrový kód při načítání
  (`RefreshFieldsList` :3384), „Obecná" × „Obecná (bez databáze)". Návrh: jeden tvar, symetrické popisy variant.
- **A2 Start z EDISONu vždy předvolí zastávkový JŘ** [S-M] **Jan** (*ověřeno:* kód). Linkový JŘ nemá ve stromu
  vstup (`EDISONMAIN2/.../API/Tree.cls:414, 493`, generuje se z legacy XML) a agenda ze shellu má přednost
  před naposledy použitou. Rozhodnout s autory stromu.
- **A3 Výběr linek: „Číslo" znamená v každé variantě něco jiného** [S-M] (*ověřeno:* kód). AD licenční a řadí
  podle něj, MHD veřejné a řadí podle interního ID; platnost ve sloupci „Detail"; „5 - Skupina 5" a „Linka
  <ID>" u nepojmenovaných. Měnit jen popisky (rozvržení sloupců se ukládá podle klíčů).
- **A4 Systémové šablony** [S]. Vývěsky jedna sekce „JŘ Zastávkový", linkové „Hlavička / Jízdní řád /
  Poznámky", nová šablona „Záhlaví / Obsah"; popis šablony AD používá staré názvy prvků. Oddělovač viz `T9`.
- **A5 Drobnosti textů** [S]. „Název souboru (tokeny {POLE})", „Klikněte pro výběr - linky (Ctrl+Shift+I)"
  (není tvar „Akce - klávesa"), titulek „Výběr - linky", „Přejmenovat..." třemi tečkami, dva texty pro
  šablonu bez parametrů, slovo „linka" natvrdo v tisku a menu (pro agendu bez linek nesedí).
- **H1 Příručka nesedí s UI na 21 místech** [S] (*ověřeno:* kód; seed, nahrává se až s verzí).
  `uzivatelska-prirucka.md`: „Instance:" (:91, :947) → „Linka:"; titulek (:109); dvě výchozí sekce (:117);
  štítek agendy (:128); sekce „Systémové M-line / Moje šablony" (:139, :1029, :1043, :1266, :1284) → sloupec
  Zdroj; popis v Uložit jako (:143); „Importovat z JSON souboru" (:152, :154); „/ N" (:523); výběr linek
  v tiskovém dialogu (:552); „Otevřít poslední JSON" (:598); „Tabulka zastávek" (:709, :780); „pro každý
  označník" (:769, :979, :791); zrušený parametr bezbariérového souhrnu (:774); „Definice sloupců" (:892);
  „Rozdělení sloupců (typy provozu)" (:917); „Písmena variant u odjezdů" (:978); filtr a „Vybrat vše" (:944);
  jen šablona AD (:1043); u vývěsky AD chybí čtyři parametry (tvar názvu, název pro MHD, výčet spojů
  u dopravce, číslo stanoviště podle); cesta ve stromu EDISONu (:12) není ani v `prirucka-it.md`.
- **H2 Nápověda F1** [S]. `HelpDialogViewModel.BuildContent`: štítek „Instance" (:96), import PDF v menu
  Nástroje, který běžný uživatel nemá (:116), „linkový a knižní JŘ" jako dvě sestavy (:244, :308,
  `CapabilityCatalog.cs:327`), „pohledna" (:301).

## 6. Pojistky - testy, které by tyto nálezy chytily

Dnes testy hlídají hlavně popisky. Bez nich se po sloučení kódu (`B56`) i po každé další agendě rozejde
totéž znovu. Pořadí podle přínosu:

- **G1 Kontraktový Caché test přes `AgendyRegistr.ListAgendy`** [M]. Každá agenda včetně rozdílového:
  `GetSchema`/`GetParameters`/`GetMockData` bez chyby; pole schématu jsou v ukázce i v datech (nebo na
  seznamu výjimek); každá kolekce dat je ve schématu; jedinečný `AgendaVariantCode`; `ElementKind` z {"",
  STOP, LINE}; jen známé klíče definice parametru (Faktury mají `default` místo `defaultValue`); výchozí
  hodnota enumu mezi volbami; „Obecné" první; `DUVOD_PRAZDNE` u linky bez dat. Pátou agendu pokryje sám.
  Dnešní Caché testy agendy vyjmenovávají ručně (`LinkovyJrSmokeTest`, `DuvodPrazdnehoTest`) - odtud `C1`.
- **G2 Celá definice stejnojmenného parametru shodná ve všech agendách** [S]. Popisek, typ, skupina,
  výchozí hodnota, volby, `serverSide` - s pojmenovanými výjimkami (`TVAR_NAZVU_ZASTAVKY`). Uzavře opsané
  parametry vývěsek (`AgendaParameterDriftTests` dnes porovná jen popisek; `ServerSideParamsDriftTests`
  spokojí příznak v kterékoli kopii).
- **G3 Jeden popisek = jeden klíč napříč agendami JŘ** [S]. Chytí `D1` a `D3` (dnes se hlídá jen stejný klíč).
- **G4 Ban list i na nápovědy, části slov a titulky oken** [S]. Odtud „nástupiště" (`P7`), „engine" (`E7`),
  „Tučně" uvnitř popisku (`E10`), „Linkový JŘ - nastavení" (`E2`).
- **G5 Každé tlačítko formuláře má obsluhu v každém hostiteli** [S]. Odtud `C2`.
- **G6 Deskriptor C# a formulář `.cls` u téhož klíče mají stejný popisek a skupinu** [S-M]. Odtud `E11`.
- **G7 Drobné díry drift testů** [S]. Regex schématu nevidí `AddField(kolekce.%Get("fields"), …)` (rozdílový
  `VERZE`, `TC_PUVODNI`, `ZONY_PUVODNI`) a chytá zakomentované řádky; parametry se jménem skládaným za
  běhu (`P4`) a pořadí „Obecné první" u vývěsek a linkového MHD (definice v pomocných metodách) test nevidí.
  Caché testy kontrolují chybu agendy v `^||ML("OUT","ERR")`, kam `$$$CatchErrRest` nepíše (chyba je
  v `^||ML("ERR")`), takže „bez chyby" nehlídají: `LinkovyJrSmokeTest` (:28, :134) a sonda
  `AdSloupekProbe` (nalezeno u `C1`, `DuvodPrazdnehoTest` už opravený).
- **G8 Zlatý odtisk linkového MHD včetně hlavičky** [S] (po 27. 10.). Bere z vývěsky MHD pět metod
  (dopravce, typ JŘ, seznam linek, skupiny); porovnání se starým modulem kryje jen tabulku a
  `TestDopravceJakoVyveska` porovnává s toutéž funkcí.

## 7. Architektura před sloučením kódu (`B56`)

- **R1 Rozhodnout model sdílení kódu agend** [S] **Jan** (*ověřeno:* kód). Registr hledá jen přímé potomky
  `BaseAgenda` (`AgendyRegistr.ListAgendy` :43, `GetClassForCode` :91), takže mezivrstva navržená
  v `legacy-nahrada.md §4` (`ZastavkovyJrBase`) by agendy schovala z nabídky. Dnes se sdílí třemi způsoby
  (`BaseAgenda` s AD helpery, MHD volá sesterskou vývěsku, vývěska AD a linkový AD volají navzájem
  `MaSpoje` a `BarvaLinky`). Volba: (a) registr přes dědičnost + test jedinečnosti kódu, nebo (b) pomocné
  třídy mimo hierarchii agend, `BaseAgenda` jen kontrakt. Podle toho opravit §4.
- **R2 Duplicity pod jiným jménem** [M] (*ověřeno:* kód; doplnit do §4, analýza je hledala podle názvu).
  Dopravce: tři implementace v Caché + skládání v C# (`PageLayoutEngine.cs:276`,
  `LineTableElement.PopisDopravcuStranky`), rozsahy čísel spojů dvakrát s jinými pravidly
  (`ZastavkoveJrAd.RozsahyCisel` × `LineTableElement.Rady`). Platnost linky `PATCH,"Plati"` na sedmi místech
  dvěma způsoby (příponu `:…` odstraňují jen dvě) a tři převody na `$h`. Barva `r;g;b`: `BaseAgenda.RgbNaHex`
  bere `0;0;0` jako nenastaveno, `RgbToHex` ve vývěskách jako černou (komentář `BaseAgenda.cls:209` „kopie
  téhož" neplatí; co znamená `0;0;0`, rozhodne **Jan**). Texty omezení viz `T3`.
- **R3 Kontrakt prvek ↔ agenda není zapsaný** [M]. Prvky čtou klíče řádků literálem (`LineTableElement.
  EnsureParsed` 31 klíčů, `StopScheduleElementBase.BuildZastavky`), chybějící klíč tiše vykreslí prázdno;
  `ATRIBUTY` má podle druhu agendy jiný tvar (shodilo náhled 8. 10.). Návrh: konstanty kontraktu podle
  druhu (STOP/LINE) + offline test, že je každá agenda daného druhu posílá (s výjimkami a důvodem).
- **R4 Schopnosti agend natvrdo v C#** [S-M] **Jan**. `ShouldPrefillDepartureColumns`, `JeRozdilovyJr`,
  `CreateSampleTemplate`, `ScheduleImportTypes.ForAgenda` (enum `ZastavkovyJrMhd` platí i pro AD). Návrh:
  příznaky jako parametry třídy v `BaseAgenda`, přes `ListAgendy` do `AgendaRegistryEntry` (aditivně).
- **R5 Parametry vývěsek opsané dvakrát** [S-M]. `ZastavkoveJrAd.AddStandardJRParameters` ×
  `ZastavkoveJrMhd.AddStandardJRParameters`; dnes shodné (ověřeno voláním). První krok `B56` podle vzoru
  `LinkovyJrAd.Parametr*`, před doslovnými buildery.
- **R6 Dokumentace pro rozvoj** [S]. `cache-api.md`: vzor agendy používá `groups`/`scope` a `q 1` bez
  ošetření chyb (porušuje vlastní pravidla 14-15), tabulka pojmů `groups`, „mock s pevnými hodnotami",
  seznam agend v appsettings (sám si odporuje), checklist nové agendy bez parametrů třídy, podmínky přímého
  potomka, `GetParameters`/`serverSide`, důvodu prázdného výstupu a kontraktu řádků. Totéž zastaralé
  v `CacheSchemaWiring.cs:8`, `CacheAgendaProvider.cs:15`, hlavičce `BaseAgenda.cls`; popis
  `LokalniOznaceniLinkyAd` se přilepil k `ZacitDuvodyPrazdne` (`BaseAgenda.cls:521-528`). `GetTableTemplate(s)`
  jsou povinné, ale nic je nečte - výchozí `[]` v `BaseAgenda`. `legacy-nahrada.md §4` odkazuje řádky mimo
  soubor - odkazovat metody. Faktury (ukázková agenda) učí `default`, `q 1` a prázdné `GetData`.
- **R7 Názvosloví v kódu** [S]. `^||TiskKnizni*`, infinitivy v `ZastavkoveJrMhd` (`PripravitTrasu`,
  `SestavitStranky`) proti `cache-api.md` pravidlu 5. Kanon jen pro nové názvy, interní sjednotit při `B56`,
  třídy nepřejmenovávat.

## 8. Co je v pořádku

- **Vědomě jinak (rozhodnutí Jan 26. 10.):** nadpis skupiny dnů je ve vývěsce MHD dvouřádkový, v linkovém
  MHD jedním řádkem (`T5`). Vývěska má úzké sloupce skupin, v Tabulce spojů se nadpis táhne přes všechny
  spoje skupiny. Požadavek DPKV #10660 se týkal vývěsky. Dva řádky v linkovém by se dělaly novým polem.
- **Vědomě jinak (rozhodnutí Jan 26. 10.):** telefon dopravce „tel:" ve vývěskách a „tel." v linkových JŘ
  (`T6`) - každý tvar odpovídá svému starému tisku a vývěsky jsou vydané.

- Parametry: „Obecné" první ve všech čtyřech agendách, ostatní skupiny nesou název prvku (od 26. 10. i typy provozu, `SkupinyParametruTest`);
  „Automaticky (dle linky)" jednotně jako první volba; společné definice (tvar názvu, směr, počet stran,
  zvýraznění zastávky) mají stejný klíč i popisek; `serverSide` sedí oběma směry.
- Data: obě čísla linky ve všech agendách pod stejným klíčem i popiskem; kolekce `ZASTAVKY`, `SKUPINY_DNU`,
  `POZNAMKY` shodné; datum dd.mm.rrrr a časy HH:MM jednotně; každé pole schématu agenda opravdu posílá.
- Tisk: texty dnů v týdnu a kroužkové číslice, piktogram bezbariéru, kola a místenka v AD, kategorie
  poznámek linky jsou všude stejné.
- Aplikace: paleta a nabídka prvků se řídí druhem agendy z registru; tiskový dialog je jeden obecný;
  Storno/OK ze sdílených textů; dlouhá pomlčka v UI textech není (kromě `T9`).
- Architektura: C# je až na `R4` agendově nezávislý; drift testy čtou `.cls` offline a novou agendu
  zahrnou samy; vzory k následování existují (`LinkovyJrAd.Parametr*`, „jen přidávat" s testem
  vyřazených polí, oracle se starým modulem u linkového AD, linkového MHD a vývěsek).
