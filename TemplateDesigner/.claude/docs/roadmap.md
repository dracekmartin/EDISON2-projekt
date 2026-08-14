# Roadmap

> **Jen otevřená / akční práce.** Hotové → [`roadmap-archive.md`](roadmap-archive.md), průběžný
> changelog → [`changelog.md`](changelog.md), stav tiketů a věci „jen na poptávku" →
> [`customer-requirements §0`](customer-requirements-zastavkovy-jr.md).
>
> **Nejblíž na řadě:** `B81` (neděle × svátek ve vlastních sloupcích vývěsky) - **hlásí zákazník
> a tiše to vrací špatná data**, takže má přednost. Pak vybrat mezi **tabulkovými prvky**
> (`B76` + `B77`, dají se vzít vcelku) a **linkovými JŘ** (`B10` + `B13`) jako dedikovaným slotem.
>
> ⚠️ **Nevydané a jen zčásti proklikané je všechno od 12. 8. včetně** - poslední vydaná verze je
> z 11. 8. Seedy novinek čekají na vydání, dřív se nenahrávají.

---

## 🚌 Zastávkové JŘ

Stav tiketů, parkované položky i věci „jen na poptávku" drží
[`customer-requirements §0`](customer-requirements-zastavkovy-jr.md).

### Denní značky podle vyhlášky 122/2014 (revize `B39` kolegou, 13. 8.)

Kolega prošel vlastní sloupce vývěsky a narazil na to, že sloupec „Ne" nevrátí **žádné** spoje,
přestože na lince jsou - mají platnost `+`.
Ptal se současně, jak zadat `X` a jestli „Po-Pá" znamená kalendářní, nebo pracovní dny.
Obojí je tatáž věc: **vlastní sloupce zavedly zadávání dnů, které neodpovídá abecedě značek
z vyhlášky**, a na tom rozdílu se rozjíždí i vyhodnocení.

Co říká [vyhláška 122/2014](https://krajta.slv.cz/2014/122), příloha č. 2, značky časového rozsahu provozu:

- `X` = jede v pracovních dnech
- `+` = **jede v neděli a ve státem uznané svátky** (jedna značka pro obojí)
- `1`-`7` = jede v pondělí … v neděli
- značky lze kombinovat, některé kombinace jsou nepřípustné (výčet **nemáme** - příloha je
  obrázková, oficiální PDF na mv.gov.cz hodí 404; dočíst z tištěného znění, než se dělá validace)
- negativní značky = dvoumístné číslo 01-31 bíle v černém obdélníku, texty „jede jen…",
  „jede také…", „jede od…do…", „jede v sudých/lichých týdnech", „nejede…"

| # | Co | Vel. |
|---|---|---|
| B81 | **`+` musí znamenat i neděli** (bug, hlásí zákazník). `DnySpoje` nechá `+` jako `+` a `SedneDoSloupceDny` dělá průnik znaků (`ZastavkoveJrAd.cls:1584-1607`), takže sloupec `7` spoj s platností `+` nevezme. Legacy to nikdy neřešil - jeho nedělní sloupec je vždy `plk["7" || plk["+"` (`ZarazeniSpoje` FTL 0/3, legacy `JRapiLinPrtMhd1:112-113`), sloupec „jen neděle" starý modul neumí. **Druhá polovina téhož je osa Období**: `DenPatriDoSloupce` (`:1893`) pustí svátek jen do sloupce, který vyjmenoval `+`, takže velikonoční neděle nepatří do sloupce „Ne" a spoj se `7` vyjde jako „nejede celé období" (drží si negativní značku, kterou nemá mít). Presety `7+` / `67+` se nemění, dotčené jsou jen vlastní sloupce se samotným „Ne" nebo samotným „Svátkem" | S |
| B82 | **Zadávání dnů v abecedě vyhlášky + `X`.** Dnes jsou v editoru zaškrtávátka Po-Ne + Svátek a `X` nejde zadat vůbec. Navíc **týž string `Days` znamená na dvou místech dvě různé věci**: při zařazení spoje je `12345` kalendářní Po-Pá (a platnost `X` se rozepíše na 1-5, `DnySpoje`), zatímco na ose Období je `12345` „pracovní dny" (svátek do nich nikdy nepadne, `DenPatriDoSloupce`). Rozdíl je věcný, ne kosmetický: spoj `1` jede i o svátečním pondělí, spoj `X` ne | M |

**Rozhodnutí, která musí padnout před `B81`/`B82`** (byla rozepsaná, Jan je zatím neuzavřel):

1. **Co znamená `X` v editoru sloupce.** Buď zkratka, která zaškrtne 1-5 (význam zařazení se nemění,
   uložené šablony i vzory zůstanou platné, žádná migrace), nebo samostatná podmínka „jen spoje
   s platností `X`" (vyhláškově přesné, ale mění význam už uložených sloupců → migrace `Days`
   „12345" → „X12345" plus přestavba vzorů `PresetySloupcu`).
2. **Má sloupec se samotným `+` brát i spoje jedoucí v neděli?** Podle vyhlášky ano, `+` ty dva dny
   slévá; „jen svátky" značkami popsat nejde a patří do osy Období. Doporučení bylo ano, ale
   s tím, že rozdíl neděle × svátek **zůstává živý** v pokrytí období a v denních značkách - jinak
   přijdeme o „spoj nepokrývá celý sloupec" u svátečního provozu.

**Čím je to doložené** (ať se to nedeleguje znovu): definice značek z vyhlášky (příloha je
obrázková, texty přes krajta.slv.cz), legacy zdroj grepem rutin na dev serveru (`AdSloupekProbe.GrepRoutines`),
naše data sondou `AdSloupekProbe.ScanSvatky` - v `CB_CSADAD` je na 3 000 linkách **1 429 spojů, které
jedou v neděli a nejedou ve svátek**, takže data ty dvě kategorie reálně rozlišují a slít je natvrdo
nelze. **Neproklikané na kolegově lince** - chybí firma a číslo linky; na nich se dá `B81` doložit
přímo přes `GetData` (a zjistit, jestli mu nezařazené spoje hlásil oranžový badge, což je jediná
pojistka, která tu situaci má chytat).

### Rozvržení sloupců vývěsky se nastavuje ze dvou míst (`B88`)

Parametr sestavy „Rozdělení sloupců (typy provozu)" a vlastní sloupce v prvku Odjezdy odpovídají na
**tutéž otázku** (jaké sloupce vzniknou), a vlastní sloupce ten parametr **tiše přebíjejí**. Kdo je
jednou zadá, má z parametru navždycky mrtvé tlačítko a nedozví se proč. (Tab „Sloupce" u prvku je
v pořádku - ten řeší vzhled, ne existenci sloupců.)

**Dohodnuté řešení** (Jan 14. 8.): parametr přestane být druhé nastavení a stane se **ukazatelem
i ovladačem jednoho stavu**.

- Do nabídky přibude volba **„Vlastní rozložení"** (aditivní změna, stávající popisky se nemění).
- Jakmile má prvek zadané vlastní sloupce, parametr se na ni **automaticky překlopí**.
- Výběr pevného rozvržení vlastní sloupce zahodí → **potvrzení** (destruktivní) a vratné Ctrl+Z.

⚠️ **Dvě pasti, na kterých to stojí:**

1. **Schování sloupce ani přejmenování nadpisu v tabu „Sloupce" překlápět NESMÍ.** Nemění to, jaké
   sloupce server dodá, jen co je vidět. Kdyby překlápělo, muselo by výběrem pevného rozvržení
   schované sloupce zase odkrýt a nadpisy zahodit - tedy mazat odvedenou práci při nesouvisející akci.
2. **„Vlastní rozložení" se nesmí uložit jako předvolba agendy.** Je to fakt o jedné šabloně;
   v předvolbě napříč šablonami by lhalo u každé další. Když se hodnota přečte tam, kde žádná
   definice sloupců není, chovat se jako „Automaticky".

---

## 📥 Backlog

### Tabulkové prvky

Nezávislé na JŘ (faktury i obecné šablony). Cíl pevné tabulky **není faktura, ale náhrada
„poskládám to z textových polí"**. Model = **objekt na stránce s excelovskými klávesami**
(Word/PowerPoint), ne tabulkový procesor - žádný řádek vzorců ani odkazy typu A1.

| # | Co | Vel. |
|---|---|---|
| B76 | **Výběr více buněk tažením** a na něm hromadné formátování + **ColSpan** slučování buněk (aditivní JSON, span-aware DrawRow). **RowSpan vědomě odložen** - spanování výšek × střídání řádků × podmíněná viditelnost. ⚠️ Zjemní i vkládání ze schránky: dnes se vkládá od vybrané buňky a tabulka se dorovná (Word/Docs chování, jediné neztrátové bez výběru oblasti); s výběrem má platit **vybraná oblast = cíl vložení** (Excel), jedna buňka = dnešní chování | M |
| B77 | **Připravené bloky agend při vkládání tabulky** (`GetTableTemplates`: „Záhlaví linky", „Shrnutí faktury"). Na serveru existují pro každou agendu, ale od zrušení starého dialogu k nim **nevede z UI cesta**. Patří k vkládání prvku („prázdná / z připraveného bloku"), ne do dialogu | S |

### Import PDF - doladění

| Co | Stav |
|---|---|
| **Obecný import: detekce tabulek z mřížky čar** - místo 50 čar + 20 textů jeden tabulkový element. Cílem je **pevná tabulka**, která od 12. 8. žije (prvek v paletě, obsah se vyplňuje na plátně), takže gate padl a záleží jen na prioritě | ⬜ |
| **JŘ import: ladění na širší sadě JŘ** (jiné layouty než KV_DP vzor - jiná města, SK, netypické legendy) | 🟡 |

Křivky/Bézier v obecném importu - low-prio, bez poptávky.

### AI integrace - v pozadí

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
- *Nejistota* (odkázat na podporu, ne halucinovat): „špatný čas u 3. zastávky linky 12" (datový problém, ne UI).

**Budoucí:** MCP server (`get_template`/`apply_template`/`add_element`/… tenká vrstva nad ViewModelem, každý
tool → `_vm.Execute(command)`) · onboarding průvodce · smart suggestions (TableElement → sloupce z DataSchema) ·
migrace starých Caché sestav · template linting v CI/CD.

### Ostatní

- **`B65` Kalendářový výběr období u sloupce vývěsky** (S, rozhodnutí Jan 10. 8.: do první verze
  stačí text). Dnes se rozsahy dat píšou ručně („14.2.-18.2., 1.7.-31.8.") a tvar hlídá validace
  při OK. Až se featura usadí, nahradit vybíráním v kalendáři - u „dnů školního vyučování" je ruční
  psaní desítek rozsahů otravné. Tvar zápisu na wire se tím nemění, jde čistě o zadávání.

- **`B66` Vlastní bloky prvků („moje komponenta")** (M, podnět Jan 12. 8.). Uživatel si vybere pár
  prvků (typicky svou hlavičku sestavy), uloží je pod jménem a pak je vkládá jedním kliknutím do
  dalších šablon. **Co už existuje:** uložení **celé sekce** jako vlastní šablony a seskupení prvků
  (Ctrl+G). Chybí mezistupeň mezi nimi - pojmenovaný blok menší než sekce. **Proč to čeká na pevnou
  tabulku:** dokud je hlavička poskládaná z 6 samostatných textů, musí blok řešit relativní pozice a
  vazby uvnitř skupiny; jako **jeden** prvek (pevná tabulka) je uložení skoro zadarmo. Otevřené:
  kde bloky žijí (per uživatel × firma jako šablony sekcí, nebo sdílené v DB) a jestli se vkládají
  z palety, nebo z nabídky „Přidat".

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

- **`B42` Co všechno umí import z předlohy nastavit** (M, nejdřív analýza; podnět Jan 31. 7. při `B41`).
  Import dnes umí **pozice a prvky** plus část vzhledu (fonty, výšky řádků, barvy záhlaví, sloupce).
  Otevřená otázka: umí z předlohy rozumně odvodit i **parametry sestavy** a **další nastavení prvků**
  (pozice značek, šipka, sloupce jako zóna/označník, varianty tras, skupiny dnů)? Je to velká
  a netriviální část importu, takže se to má projít vědomě, ne po jedné vlastnosti.
  ⚠️ **Omezení už neplatí tak, jak se dřív psalo:** hodnoty parametrů sestavy se od 15. 7. ukládají
  **i do šablony** a mají přednost před per-agenda nastavením (`SyncParametersToTemplate`), takže
  import by je nastavit mohl, aniž rozhodí ostatní šablony té agendy. Zůstává druhá past: uloží se
  i hodnoty, kterých se nikdo nedotkl - detail u #12843 v `customer-requirements §0.1`.
  **Malý krok, který jde udělat hned (S):** detekci časové osy z předlohy vrátit a její výsledek
  **napsat do protokolu importu** („předloha má osu 5-23, nastavte ji v parametrech"), ať se informace
  neztratí a dostane se k tomu, kdo s ní může něco udělat. Původní detekce (`timeAxisRangeFrom/To`
  v `AiGuidedScheduleBuilder`) je v gitu před commitem `B41`.

- **Headless tisk (A3)** - „vytiskni linku X" z modulu přes PrintEngine fasádu (šablona+data→PDF
  bez stavu editoru). Dnes tisk jen v návrháři. Gate R4/R7.

- **Samostatný distribuční kanál** systémových šablon mimo `.exe` - v1 doručuje embed→MLINE;
  „jiný kanál" (server push / admin) jen vymění zdroj, picker/UX beze změny.

- **Externí datové zdroje** (analýza 2026-07-12, na poptávku) - architektura připravená (engine zná jen
  `Template + DataContext`; DataContext je zdrojově agnostický JSON). Tři švy: Caché gateway agenda
  (0 změn .NET) · .NET provider vedle Caché (vzor `JsonFileDataSchemaProvider` + Composite) · headless
  A3 („POST šablona+JSON → PDF" = externí zdroj bez adaptéru; **A3 je nejpravděpodobnější nosič**).
  První krok s nejlepším poměrem cena/hodnota: **generický JSON zdroj (soubor/URL)** - tržní minimum
  (CSV/Excel/JSON/REST), náš wire formát už JSON je; využilo by i AI + testování šablon. Dořešit:
  credentials, schéma z dat, změkčení předpokladu „běží nad Caché".

- **Tabulkové featury (bez customer demand):** grouping (`GroupHeader`/`GroupFooter`/`Summary`) ·
  vnořené datasource (`_rows` v `_rows`).

- Font warning + ad-hoc overflow (YAGNI).

---

## 🚆 Linkové a knižní JŘ (náhrada TiskJR)

> ⚠️ **Gate: R1 (sdílené tabulkové jádro), R2 (layout zodpovědnosti), R5 (embedded fonty)** -
> [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md). Začíná se **designem
> sdíleného jádra** (StopList + DynamicTable + LineTable), ne kódem elementu.
>
> Bere se **vcelku**, ne po kusech (rozhodnutí Jan 4. 8.): `B10` sám o sobě nic nedoručí, protože
> bez `B13` se široká tabulka stejně nevytiskne.

Legacy `TiskJR` generuje **vývěsní i knižní JŘ z téhož enginu**, takže je to jeden track. Formát:
sloupcová tabulka celé linky (svislý seznam zastávek × sloupce spojů s časy; řádek platností se
symboly/NZ; Vyčká/Navazuje; skupinová záhlaví PD/So/Ne; 10 druhů poznámek). **Gold oracle `mkPLinWin`
(ALVA.JRMAIN.MLAPI)**, metodika = čistá reimplementace à la `ZastavkoveJrAd` (~1 měsíc).

Datové jádro (`LinkovyJrAd`/`LinkovyJrMhd`), `LineTableElement`, plán řádků a auto-fit fontu jsou
hotové. Kromě bloků níže chybí ještě **výchozí linková šablona** (nový soubor v `SystemTemplates/`,
zabalí i nalije se sám - vel. S).

| # | Blok | Vel. |
|---|---|---|
| **B10** | **Symbolový font - linková část** (gate R5). Linková tabulka dnes symboly **nekreslí vůbec** - agenda posílá legacy klíče (`|`, `<`, `m`, `u`…) a chybí převod na pozice ve fontu. ✅ **Podklady hotové:** katalog 66 symbolů, render-módy i **změřený repertoár fontu** (94 pozic z obou fontů) jsou v `elementy-architektura.md`, takže neznámá „který kód nese který piktogram" je pryč. ⚠️ Pozice `znak` z katalogu se s pozicí ve fontu často **neshoduje** (letadlo `n`, paragraf `P`, metro `#`) - musí jít přes mapu, ne přes písmeno. **Rozsah (rozhodnutí Jan 4. 8.):** sdílená mapa `značka → (render-mód, hodnota)` pro všechny prvky, nahradí i dnešní `SymbolGlyph` u zastávkových vývěsek → dotkne se běžícího zastávkového JŘ, chce zlaté odtisky. Culture rozlišení jen pro **3 pozice** (`D`, `w`, `x`). Ve fontu chybí loď, wifi a zásuvka. | M→S |
| **B13** | **2D bin-packing / `Strih`** (gate R1/R2) - široká tabulka (MHD linka = 100+ spojů) se dnes zmáčkne na šířku místo rozřezání do stránkových bloků; + flow paketů, „dotisk zbytku legendy na další stránce". Nutné pro reálný tisk MHD linek; zastávkový JŘ to nepotřebuje. Stojí na hotovém `B11`: řádková vrstva existuje, takže rozřezání se píše **jednou** nad plánem řádků, ne pětkrát v per-element smyčkách. | L |
| **B14** | **Dedup Caché tiskových enginů + jejich testy** - doprovod ostrého knižního JŘ, ne dopředu. (a) **Dedup** (*audit H1*): různé signatury `AddAnotace`/`BuildPoznamka`, ~15 helperů MHD↔AD + AD `^JRL` readery → vyextrahovat sdílené readery + `Build*` do `BaseAgenda`/`JrShared`; je to **Caché analog R1 extrakce jádra** (`B13` řeší .NET render-side, tohle Caché data-side). Korektnostní riziko. Menší výsek Build\* helperů = `B56` (v Backlogu); při realizaci jednoho zredukovat druhý. ⚠️ **Korekce z analýzy 5. 8.:** dřívější tvrzení „`CasHodnota` bere `"|"` jinak" **neplatí** - obě verze jsou funkčně identické, lišil se jen komentář. A objem duplikace je menší, než audit odhadoval: z 27 stejně pojmenovaných helperů je jen 15 doslovných kopií (~97 řádků). (b) **Testy** (*audit H2*): `LinkovyJr*` (~2 000 ř.) 0 testů, `ZastavkoveJrMhd` 1 helper; AD gold gate tiše projde bez `CB_CSADAD`, křehký digest nad živými daty (pinuje 2/22 linek), `tools/compare-gold.ps1` neexistuje → smoke + frozen-input gold testy, bezpodmínečný gate (zmrazit vstup do izolovaného ns). | L + M |
| **B60** | **Název zastávky se v linkovém JŘ tvoří jinak než na vývěsce** (nález analýzy `B56`, 5. 8.) - vývěska respektuje volbu tvaru názvu (#13089, 4 volby) i přepínač „použít upravený název pro MHD"; linkový JŘ bere název natvrdo, takže **táž zastávka může mít na dvou výstupech jiné jméno**. ⚠️ **Změřeno 5. 8. - není to malá věc:** tvarování visí na **dvou parametrech sestavy** (`TVAR_NAZVU_ZASTAVKY`, `NAZEV_PRO_MHD`) načítaných do `^||TiskAd`, takže srovnání = přidat linkovému JŘ dva uživatelsky viditelné parametry (popisky enumu jsou po vydání zamčené) + sdílet 5 helperů (`FormatNazvuZastavky`, `MezeryPoInterpunkci`, `TvarNazvuZastavky`, `PouzitNazevProMhd`, `NactiNastaveniNazvu`) - a to **bez jakéhokoli testu** (linkové agendy 0 testů, síť staví `B14b`). **Nejdřív rozhodnout, jestli ty parametry v linkovém JŘ chceme.** | M |

### Odložené body z pilotu (seřazené dle hodnoty pro ostré nasazení)

| # | Co | Proč odloženo / dopad | Odhad |
|---|---|---|---|
| C5 | **Konfigurovatelné pořadí poznámek** (`PoznamkyPoradiDruh`) - legacy AD parametr; TD má pevné pořadí druhů | per-zákaznická variace, řešit až s poptávkou | S |
| C6 | **Turnusy, obousměrný tisk, souhrnné JŘ (řádek zdrojové linky)** - legacy volby `in(arr,6/7)`, MHD `MakeData` je nemá | mimo scope pilotu; validovat, zda je zákazník chce, než portovat | - |
| C7 | **Rozdvojení příjezd/odjezd - jemný layout** + fixed-width padding sloupce (legacy `arr,13`) | kosmetika; TD řeší podřádky, ne padding | S |

Detail datových odchylek a mezer viz [`linkovy-knizni-jr-implementace-analyza.md`](linkovy-knizni-jr-implementace-analyza.md) §11–12.

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
| F3 | **TableElement na RG úroveň** (navazuje na dynamickou tabulku + `B76`): per-řádek zvýraznění 0-5 z dat, custom barvy per řádek/buňka, overflow módy (zalamování → klonování řádků), opakování mezisoučtu na další stránce, datové typy sloupců (datum/minuty/záporné červeně/skrýt nuly), řádek-jako-layout | ⬜ L |
| F4 | **Výstupní kanály fáze 2** - Excel export, PDF heslo + el. podpis (PDFSIGNER), Epson znakový tisk | ⬜ dle poptávky |
| F5 | **Jazykové varianty šablon** - per-jazyk kopie provázaná na CS zdroj | ⬜ |
| F6 | **Vědomě neportovat bez poptávky** - grafy (12×), AZTEC (2×), calculated (2× - výrazy TD nadmnožina), checkbox (8×); ověřit + škrtnout | ⬜ validace |

---

## 🗄️ Hotová práce

Detail dokončených etap → [`roadmap-archive.md`](roadmap-archive.md). Průběžný changelog → [`changelog.md`](changelog.md).
