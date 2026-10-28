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
  Stav 28. 10.: titulky oken vlastností hlídá `ElementNamesTests`, části slov se kontrolují jen v popiscích
  a volbách, nápovědy nic (odtud `H8`).
- **G5 Každé tlačítko formuláře má obsluhu v každém hostiteli** [S]. Odtud `C2`.
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
  Stav 28. 10.: zastávky už mají konstanty `StopFields`, Tabulka spojů ne.
- **R4 Schopnosti agend natvrdo v C#** [S-M] **Jan**. `JeRozdilovyJr`, `CreateSampleTemplate` (`"LINKOVY_JR_AD"`),
  `ScheduleImportTypes.ForAgenda` (podle prefixu kódu; enum `ZastavkovyJrMhd` platí i pro AD).
  `ShouldPrefillDepartureColumns` už jde přes příznak `CustomColumns` (28. 10.). Návrh:
  příznaky jako parametry třídy v `BaseAgenda`, přes `ListAgendy` do `AgendaRegistryEntry` (aditivně).
- **R5 Parametry vývěsek opsané dvakrát** [S-M]. `ZastavkoveJrAd.AddStandardJRParameters` ×
  `ZastavkoveJrMhd.AddStandardJRParameters`; dnes shodné (ověřeno voláním). První krok `B56` podle vzoru
  `LinkovyJrAd.Parametr*`, před doslovnými buildery.
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

- **Nutné před prvním krokem:** R1, G7 (část o chybě), G1 v malé podobě (všechny agendy přes `ListAgendy`:
  schéma, parametry a ukázka bez chyby a s platným JSON), G2 jako Caché test nad výstupem `GetParameters`
  (přežije přesun kódu a uvidí i parametry skládané za běhu), G8 a snímek výstupu všech agend.
  Ostatní G a R jsou užitečné, ale bez nich slučování tisk tiše nerozbije.
- **Snímek výstupu je sonda, ne test** (pravidlo o testech závislých na stroji): otisk `GetData` přes sady,
  které testy už mají (`ZastavkoveJrAdSadaTest.Sada`, `LinkovyJrSadaTest.Jadro`, `LinkovyJrMhdTest.LINKY`,
  MHD 1001/1003/1006), a celý JSON `GetParameters`, `GetSchema`, `GetMockData`.
  Po každém kroku musí vyjít bajtově stejně; snímek se v `B56` nepřepisuje.
  Dnešní pokrytí: zastávkový AD odtisk 3 linek a oracle jen zčásti, zastávkový MHD 3 linky a oracle,
  linkový AD 4 linky a oracle, linkový MHD jen oracle tabulky, rozdílový jen umělá data.
  Všechny odtisky jedou jen s výchozími parametry.
- **R1 - doporučení revize (b), pomocné třídy mimo hierarchii agend.**
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
  „Nastavení jízdních řádů, která čte návrhář".
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
  `POZNAMKY` shodné; datum dd.mm.rrrr a časy HH:MM jednotně; každé pole schématu agenda opravdu posílá.
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
H3 rozhodnutý (nechat, archiv), H6 hotový (changelog 2026-10-28 (22)).

- **H9 Poznámky při přepínání prvků působí jako chyba** [S] **Jan** (*ověřeno:* server).
  Výjimka `E3b` platí, ale: Záhlaví a Datové buňky jsou nahoře místo dole; pole stylu záhlaví jsou
  obráceně (Barva pozadí, Barva textu, Font, Řez, Velikost); chybí Tučné a Kurzíva, ačkoli záhlaví kreslí
  celý styl (`NotesLegendElement` kolem :191); Oddělovač mění obsah a stojí v Datových buňkách.
  Ve skupině Negativní značky „Barva pozadí značky", Minuty mají pro týž klíč „Negativní značka: Barva
  pozadí" (výjimka v `EDInputFormSchemaDriftTests`).
  Návrh revize: aspoň pořadí podle `StyleFields` a Oddělovač do Rozložení.
- **H10 Tabulka spojů: pořadí a názvy skupin** [S] **Jan** (*ověřeno:* server).
  Přípojné řádky (obsah) stojí za Šířkou sloupců (rozložení), proti pořadí v `ux §4`.
  Skupina „Řádky" nic neříká a hledání „řádk" ji plete s Přípojnými řádky a Výškou řádku; návrh „Řádky značek".
  Řádek platnosti, Řádek negativních značek a Řádky turnusů nemají nápovědu.
- **H11 Minuty: předpona „Text:"** [S] **Jan** (*ověřeno:* server).
  Skupina Značky u odjezdů má 21 polí a z „Text: Barva textu" není poznat, o jaký text jde.
  Návrh: „Textová značka: …" souběžně s „Negativní značka: …".
- **H12 Hledání ve formuláři** [M] **Jan** (*ověřeno:* server, kód hledání).
  Formuláře tabulek mají 86-109 polí bez sbalování, hledání je hlavní cesta k poli.
  Porovnává podřetězec popisku nebo názvu skupiny bez synonym a tvarů slov (`EDInputForm.xaml.cs`
  `ComputeSearchMatches`, `SearchNormalization.cs` v EDControls).
  „písmo", „barva písma" a „podbarvení" nenajdou nic (Font, Barva textu, Barva pozadí); „čára" nic (Čáry mezi
  řádky, Styl čáry); „rámeček" jen přes název skupiny; „velikost" a „pozice" vytáhnou celou skupinu Pozice
  a velikost (v Seznamu zastávek 7 z 12 výsledků mimo).
  Návrh: seznam synonym, slova od 5 znaků porovnat bez koncovky, a když se shoduje popisek pole, nerozbalovat
  celou skupinu jen kvůli shodě v jejím názvu.
  Popisky neměnit. Změna je v EDControls, platí pro formuláře celého EDISONu.
- **H13 Novinky se starými názvy polí** [S] **Jan**.
  `plan-a-zmeny.md` (článek je veřejný) má „Rozestup odjezdů" a „Více stanovišť"; záznamy jsou datované.
  Jestli je opravit a jestli přejmenování polí z 28. 10. patří do Novinek (pravidlo: jen změny, které uživatel
  pozná), rozhodne Jan. Příručka a dokumenty pro agenty jsou opravené (changelog 2026-10-28 (21)).
- **H16 `review-proklik-checklist.md`** [S] **Jan**.
  Jednorázový dokument ze 4. 8. pro ruční proklik; podle hlavičky ho po projití smazat, ale část bodů pořád
  čeká na Jana (vizuální dojem, hlášky naživo). Projít, nebo smazat.

**Před smazáním tohoto souboru** přesunout §7 (připravenost na `B56`) do `legacy-nahrada §4` spolu s R2
a odstavec níž k položce backlogu, které se týká. P2 a vědomé rozdíly už jsou v `legacy-nahrada.md §4` (H14).

**K backlogu „Popisky jednoduchých prvků"** (táž revize, ověřeno na serveru):
- Obecné názvy skupin, kvůli kterým se u tabulek rušilo „Obecné": „Vzhled" (Obdélník, Kód, Sekce), „Barvy" (Text).
  Text má styl ve třech skupinách (Font, Barvy, Zarovnání), tabulky v jedné skupině části.
- „Okraj" znamená tři věci: obrys Obdélníku (a pod ním ještě skupina Rámeček prvku, tedy dva rámečky),
  „Vnitřní okraj" u tabulek, skupina Okraje u Stránky v pořadí Nahoře, Dole, Vlevo, Vpravo (tabulky nahoře,
  vpravo, dole, vlevo).
- Obrázek má Barvu pozadí ve skupině Rámeček prvku a volbu „Vyplnit (oříznout)" se závorkou.
- Stránka: „Kultura (formátování)" a volba „(výchozí dle systému)"; skupina „Výchozí styl" má jediné pole.
- Čára má skupinu Rámeček prvku (jestli ho kreslí, neověřeno); Text nemá u Barvy textu nápovědu
  `{BARVA_LINKY}`, kterou tabulky mají; u Sekce „rámeček" najde jen přepínač, ne barvu a tloušťku.
