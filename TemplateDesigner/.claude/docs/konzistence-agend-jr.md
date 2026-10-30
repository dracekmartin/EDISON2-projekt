# Konzistence agend jízdních řádů - druhé kolo (`B194`)

> Jednorázová analýza 23. 10. 2026: nezávislá revize zastávkového a linkového JŘ (AD i MHD) po uzavření
> `B193`, s důrazem na jednotné názvosloví a ovládání. Šest oblastí (parametry, nabídka dat, prvky,
> aplikace, tisk, architektura), každý nález **přeověřený proti kódu po commitu `3139b27d`**.
> **Smazat po dokončení `B194`** - trvalá pravidla do `cache-api.md` / `ux-conventions.md`,
> kandidáti sloučení kódu do [`legacy-nahrada.md §4`](legacy-nahrada.md).
> Rozdílový JŘ je v rozsahu jen tam, kde ho rozbila změna společného kódu (`C1`).

**Roadmapa** má každou položku jako jeden řádek pod týmž ID: §1-§7 tady = `B194a`-`B194g` tam, §9 = `B194h`.
Hotovou položku smaž tady i v roadmapě (do `roadmap-archive.md`).

**Jak číst položky.** `[S/M/L]` velikost · **tisk** = oprava změní vytištěný výstup vydané agendy ·
**Jan** = potřebuje rozhodnutí (jinak zjevná oprava) · *ověřeno:* kód / server (čtecí volání na dev
serveru) / neověřeno. Odkazy jsou jméno metody + řádek k 23. 10.; řádky se posouvají, metoda platí.

**Co je zamčené:** popisky voleb enum parametrů (ukládá se zobrazený text, `ux-conventions §8`), klíče
polí na wire a klíče vlastností v šablonách (jen přidávat), kódy agend. Popisky polí, parametrů,
skupin a formulářů se mění volně - bere je návrhář ze serveru.

## 1. Chyby k opravě

Všechny hotové (C1-C12, 23.-26. 10.) - viz `roadmap-archive.md` a changelog.

## 2. Tisk - rozdíly mezi agendami

Všechno hotové nebo rozhodnuté (27. 10.) - viz `roadmap-archive.md`, `B194` T1-T14 (T7 vědomý rozdíl).

## 3. Nabídka dat a parametry sestavy

Zbývající `P2` (popisky voleb rozdělení sloupců) čeká v backlogu; detail přesunut do
[`legacy-nahrada.md §4`](legacy-nahrada.md) (28. 10.).

## 4. Prvky a formuláře

Všechno hotové (28. 10.) - viz `roadmap-archive.md`, `B194` E3, E3b, E3c a E13a-E13i. Trvalá pravidla jsou
v `ux-conventions §4` (kostra formuláře) a `§8` (popisky), záložka Sloupce v `mvvm-patterns.md`.
Co po tom našla revize, je v §9.

## 5. Aplikace a dokumentace

Vše hotové nebo rozhodnuté (27. 10.) - viz `roadmap-archive.md`, `B194` A1-A5.

## 6. Pojistky - testy, které by tyto nálezy chytily

Dnes testy hlídají hlavně popisky. Bez nich se po sloučení kódu (`B56`) i po každé další agendě rozejde
totéž znovu. Pořadí podle přínosu:

- **Zbytky dat agend** [S] **Jan** (nález `G1` 30. 10.). Vývěska AD posílá tři položky, které nabídka nemá
  a návrhář nečte: kolekci `DOPRAVCI` (provozovny, z nabídky skryté 1. 9., `B112` vyhozené 4. 9.)
  a `NZ_KONFIG` s `NEVYPISOVAT_ZNACKY_SLOUPCU` (editor sloupců je přestal číst 10. 8., nahradily je hotové
  vzory `PRESETY_SLOUPCU`). V `KontraktAgendTest.PolozkyMimoNabidku` mají druh `zbytek`. Návrh: přestat
  je posílat a smazat zastaralé komentáře u nich (`ZastavkoveJrAd` u skalárů dopravce, `BuildDopravci`,
  NZ konfigurace). Šablona je použít nemůže, v nabídce nejsou; snímek výstupu se tím vědomě změní.
- **G5 Každé tlačítko formuláře má obsluhu v každém hostiteli** [S]. Odtud `C2`.

## 7. Architektura před sloučením kódu (`B56`)

- **R1 rozhodnuto 29. 10.: varianta (b)**, zapsané jako [`architektonicka-rozhodnuti-agendy.md` R8](architektonicka-rozhodnuti-agendy.md).
  Registr hledá jen přímé potomky `BaseAgenda` (`AgendyRegistr.ListAgendy` :43, `GetClassForCode` :91),
  takže mezivrstva `ZastavkovyJrBase` navržená v `legacy-nahrada.md §4` nepřipadá v úvahu - opravit §4 při R2.
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
  Stav 28. 10.: zastávky už mají konstanty `StopFields`, Tabulka spojů ne.
- **R4 Schopnosti agend natvrdo v C#** [S-M] **Jan**. `JeRozdilovyJr`, `CreateSampleTemplate` (`"LINKOVY_JR_AD"`),
  `ScheduleImportTypes.ForAgenda` (podle prefixu kódu; enum `ZastavkovyJrMhd` platí i pro AD).
  `ShouldPrefillDepartureColumns` už jde přes příznak `CustomColumns` (28. 10.). Návrh:
  příznaky jako parametry třídy v `BaseAgenda`, přes `ListAgendy` do `AgendaRegistryEntry` (aditivně).
- **R5 Parametry vývěsek opsané dvakrát** [S-M]. `ZastavkoveJrAd.AddStandardJRParameters` ×
  `ZastavkoveJrMhd.AddStandardJRParameters`; dnes shodné (ověřeno voláním). První krok `B56` podle vzoru
  `LinkovyJrAd.Parametr*`, před doslovnými buildery. Shodu hlídá `KontraktAgendTest.TestSdileneParametryShodne`
  (`G2`); přehled sdílených parametrů vypíše `KontraktAgendTest.Prehled` - 29. 10.: 20 sdílených, 16 shodných,
  4 vědomé rozdíly (tvar názvu zastávky, počet stran, zastávky bez zastavení, skupina barev spojů).
- **R6 Dokumentace pro rozvoj** [S]. `cache-api.md`: vzor agendy používá `groups`/`scope` a `q 1` bez
  ošetření chyb (porušuje vlastní pravidla 14-15), tabulka pojmů `groups`, „mock s pevnými hodnotami",
  seznam agend v appsettings (sám si odporuje), checklist nové agendy bez parametrů třídy, podmínky přímého
  potomka, `GetParameters`/`serverSide`, důvodu prázdného výstupu a kontraktu řádků. Totéž zastaralé
  v `CacheSchemaWiring.cs:8`, `CacheAgendaProvider.cs:15`, hlavičce `BaseAgenda.cls`. Popisy přilepené
  k cizí metodě (stav 28. 10.): popis `AddBoolParam` nad `RgbNaHex` (`BaseAgenda.cls`), popis
  `GetAvailableInstances` nad `GetAvailableSkupiny` v obou vývěskách. `GetTableTemplate(s)`
  jsou povinné, ale nic je nečte - výchozí `[]` v `BaseAgenda`. `legacy-nahrada.md §4` odkazuje řádky mimo
  soubor - odkazovat metody. Faktury (ukázková agenda) učí `default`, `q 1` a prázdné `GetData`.
- **R7 Názvosloví v kódu** [S]. `^||TiskKnizni*`, infinitivy v `ZastavkoveJrMhd` (`PripravitTrasu`,
  `SestavitStranky`) proti `cache-api.md` pravidlu 5. Kanon jen pro nové názvy, interní sjednotit při `B56`,
  třídy nepřejmenovávat.

**Připravenost na `B56`** (revize 28. 10., čtení kódu, na serveru nic nespuštěno):

- **Nutné před prvním krokem: splněno 29. 10.** (G1 malá podoba, G2, G7, G8 a snímek výstupu), 30. 10.
  doplněno hlídání linek z testů (`TestovaciLinkyTest`), výměna linky, která z dat zmizela, a celé G1
  (data agendy odpovídají nabídce, známé klíče parametrů).
  Ostatní G a R jsou užitečné, ale bez nich slučování tisk tiše nerozbije.
- **Snímek výstupu je sonda, ne test** (pravidlo o testech závislých na stroji), sejmutý znovu 30. 10.: schéma,
  parametry a ukázka všech agend z registru a `GetData` linek ze sad testů (`ZastavkoveJrAdSadaTest.Sada`,
  `LinkovyJrSadaTest.Sada`, porovnání verzí CB_CSADAD 0380760, zastávkový MHD 1001/1003/1006,
  `LinkovyJrMhdTest.LINKY`) - 45 souborů, 15 MB, dva běhy po sobě shodné. Seznam drží `SnimekAgendProbe`,
  ukládá a porovnává `SnimekAgendNaostro`: `TD_SNIMEK_AGEND=<složka> dotnet test TemplateDesigner.Tests
  --filter SnimekAgendNaostro` (u Jana `C:\Users\miesb\source\EDISON2-snimek-agend`, mimo repo).
  Po každém kroku musí vyjít bajtově stejně; kódem `B56` se snímek nepřepisuje.
  Výstup závisí na datech dev serveru a ta mění i kolegové (30. 10. zmizely dvě linky z testovacích sad).
  Proto se snímek porovná **i před krokem**, na nezměněném kódu: když se liší, změnila se data, snímek se
  sejme znovu a teprve pak přijde krok.
  Dnešní pokrytí: zastávkový AD odtisk 3 linek a oracle jen zčásti, zastávkový MHD 3 linky a oracle,
  linkový AD 4 linky a oracle, linkový MHD oracle tabulky a od 29. 10. odtisk 7 linek včetně hlavičky
  (`LinkovyJrMhdGoldTest`), rozdílový jen umělá data.
  Všechny odtisky jedou jen s výchozími parametry.
- **Proč R1 (b), pomocné třídy mimo hierarchii agend.**
  Kopie vedou dvěma směry: vývěsky AD a MHD sdílejí stavbu výstupu (`BuildOdjezd`, `AddAtribut`), vývěska AD
  a linkový AD čtení dat AD (`NazevLinky`, `PrvniLinka`, `WMapa`); jedna dědičnost pokryje jen jeden.
  Dnešní sdílení už má tvar (b): `LinkovyJrMhd` volá `LinkovyJrAd.Parametr*` a `ZastavkoveJrMhd.SeznamLinek`.
  Metody se stejným jménem a jiným významem (`Jazyk`, `ZonaBezPismen`, `PopisDopravce`, `BuildStrankaLinky`)
  by v mezivrstvě přes `..X()` tiše volaly verzi podtřídy (na serveru neověřeno).
- **Pořadí kroků:** nejdřív R5.
  Sedm společných parametrů (`PORADI_STRANEK`, `TISK_SIPKY`, `TISK_PROJETYCH_ZAST`, `CASOVA_OSA`, `HODINY_OD`,
  `HODINY_DO`, `ROZTAHNOUT_NA_VYSKU`) je znak po znaku shodných.
  AD je má proložené asi 15 vlastními a musí je přidávat ve stávajícím pořadí, jinak se panel parametrů přeskupí.
  Drift testy upravit v témže commitu.
  Pak doslovné kopie (po odstranění komentářů bez rozdílu), jedna skupina = jeden commit + snímek:
  vývěsky `AddAtribut`, `AddPoznamkaZastavky`, `AddZona`, `BuildOdjezd`, `BuildSkupinaDnu`,
  `ConvertSpecialniZnak`, `IntToHex2`, `RgbToHex`, `GetTableTemplate(s)`; linkové `BuildPoznamka`,
  `BuildSkupina`, `FormatCas`; AD `NazevLinky`, `NormalizLinku`, `PrvniLinka`, `CasHodnota`,
  `GetAvailableSkupiny`/`GetAvailableInstances` (liší se jen `q` × `ret`).
  Kopie s rozdílem až s rozhodnutím Jana nebo s oraclem.
  Metody, které čtou nastavení linky (`NactiNastaveniNazvu`, `ZonaBezPismen`, `TiskBarevSpoju`), až po backlogu
  „Úklid nastavení tisku v jízdních řádech" (kolegové většinu nastavení vyhodí, domluveno 29. 10.).
- **Kde by sloučení změnilo tisk** (doplnit do `legacy-nahrada §4` v rámci R2):
  - Texty omezení: `LinkovyJrAd.SestavTextOmezeni` tiskne omezení 14/15 i bez dat, zastávkový je přeskočí.
    Liší se i předložka (`PredlozkaOmezeni` × `PrefixOmezeni`) a zdroj jazyka.
  - Stejné jméno, jiný význam: `Jazyk` (`*SK`/`CZ` z `JRSET` × `cs`/`sk` z `^||TiskAd`), `ZonaBezPismen`
    (úprava textu × ano/ne z nastavení), `NactiNastaveniNazvu` (výchozí „Plný CIS" × „Zkrácený"),
    `PopisDopravce` (oddělovač `", "` × dva konce řádku).
  - Pracovní globály: `WMapa` (`^||TiskKnizni("wmap",trasa)` × `^||TiskAd("wmap",db,sku,lin,trasa)`),
    `ZaznamZastavky` bere datum z jiného globálu.
  - Barva `0;0;0`: MHD posílá přes `RgbToHex` černou jako `BACK_COLOR` poznámek.
    Sloučení na `RgbNaHex` by černá pozadí z tisku odstranilo (**Jan**, viz R2).
  - Nadmnožiny: `AddAnotace` (AD navíc `KATEGORIE`, `SPOJ`, `V_ROZSAHU_NEPOZNAMKOVAT`) a
    `AddStandardJRCollections` (AD o 5 polí víc).
    MHD by dostal klíče, které neplní, a filtry Poznámek podle kategorie by začaly působit i na MHD.
- **Pojistky můžou při přesunu kódu ztichnout:** drift testy čtou jen `Agendy/*.cls` nerekurzivně, test schématu
  vynechává `BaseAgenda.cls`, pořadí polí se kontroluje po souborech, `ServerSideParamsDriftTests` hledá
  doslova `AddBoolParam(arr,`.
  Proto kontroly na serveru (G1, G2), nebo úprava testů v témže commitu.

## 8. Co je v pořádku

- **Vědomě jinak (rozhodnutí Jan 26. 10.):** `T5`, `T6`, `T4c`, `T8`, `T10b` - přesunuto do
  [`legacy-nahrada.md §4`](legacy-nahrada.md) (28. 10.), odstavec „Vědomé rozdíly mezi agendami JŘ".

- Parametry: „Obecné" první ve všech čtyřech agendách, ostatní skupiny nesou název prvku (od 26. 10. i typy provozu, `SkupinyParametruTest`);
  „Automaticky (dle linky)" jednotně jako první volba; společné definice (tvar názvu, směr, počet stran,
  zvýraznění zastávky) mají stejný klíč i popisek; `serverSide` sedí oběma směry.
- Data: obě čísla linky ve všech agendách pod stejným klíčem i popiskem; kolekce `ZASTAVKY`, `SKUPINY_DNU`,
  `POZNAMKY` shodné; datum dd.mm.rrrr a časy HH:MM jednotně; každé pole schématu agenda opravdu posílá
  (od 30. 10. hlídá `KontraktAgendTest.TestDataOdpovidajiNabidce`).
- Tisk: texty dnů v týdnu a kroužkové číslice, piktogram bezbariéru, kola a místenka v AD, kategorie
  poznámek linky jsou všude stejné.
- Aplikace: paleta a nabídka prvků se řídí druhem agendy z registru; tiskový dialog je jeden obecný;
  Storno/OK ze sdílených textů; dlouhá pomlčka v UI textech není (kromě `T9`).
- Architektura: C# je až na `R4` agendově nezávislý; drift testy čtou `.cls` offline a novou agendu
  zahrnou samy; vzory k následování existují (`LinkovyJrAd.Parametr*`, „jen přidávat" s testem
  vyřazených polí, oracle se starým modulem u linkového AD, linkového MHD a vývěsek).

## 9. Dodělávky po sjednocení formulářů (revize 28. 10.)

Nezávislá revize `B194d`: formuláře šesti tabulek a Textu volané na serveru (server = repo do posledního pole),
kód a testy (build bez chyb, 2492 testů zelených v čisté kopii `7dfb5b1a`) a dokumentace.
Směr potvrzený: tabulky mají jednu kostru, stejné sady stylu a stejné popisky; „barva" najde v každém
formuláři všechna barevná pole.
H1, H2, H4, H5, H7, H8, H14, H15 a dokumentační část H13 jsou hotové (changelog 2026-10-28 (18)-(21)),
H3 rozhodnutý (nechat, archiv), H6, H9, H10 a H11 hotové (changelog 2026-10-28 (22)-(26)), H12 v backlogu (roadmapa),
H13 rozhodnutý (Novinky beze změny), H16 smazaný, H17 hotový (changelog 2026-10-28 (27)). `B194h` je uzavřený.

Popisky jednoduchých prvků z téže revize jsou hotové (changelog 2026-10-28 (28)).

**Před smazáním tohoto souboru** přesunout §7 (připravenost na `B56`) do `legacy-nahrada §4` spolu s R2.
P2 a vědomé rozdíly už jsou v `legacy-nahrada.md §4` (H14).
