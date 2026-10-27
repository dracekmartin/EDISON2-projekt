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

Všechno hotové nebo rozhodnuté (27. 10.) - viz `roadmap-archive.md`, `B194` T1-T14 (T7 vědomý rozdíl).

## 3. Nabídka dat a parametry sestavy

Popisky a nápovědy výstup ani uložené volby nemění (návrhář je bere ze serveru). Popisky citují i nové
hlášky linky bez dat (`LinkovyJrAd.BuildStrankaLinky`, `LinkovyJrMhd.cls:420`) - při přejmenování opravit i je.

- **P2 Totéž rozdělení sloupců má ve vývěsce a v linkovém jiné volby** [M] **Jan** (*ověřeno:* server).
  „Pracovní / Sobota / Neděle+svátky" × „Pracovní dny / Soboty / Neděle+svátky", kryptické prázdninové volby
  vývěsky; vnitřní klíče jsou stejné (`PRAC_SO_NE`, `PRAC_VIKEND`). Volby jsou zamčené → přidat souběžný
  klíč `optionLabels`, `options` nechat jako text (`CacheAgendaProvider.ParseParameters` :305 by na objektu
  ztratil všechny parametry). Třetí kopie popisků v `DepartureColumnPresets.cs:20-37`.

## 4. Prvky a formuláře

Mění se jen popisky a struktura formulářů, klíče vlastností v šablonách zůstávají. Tady je nejvíc práce
a zároveň základ pro další prvky: **nejdřív doplnit kanon `ux-conventions §8` (Jan), pak sjednocovat.**

- **E3 Styl záhlaví a buněk má pět podob** [L] **Jan** (*ověřeno:* kód, server). Plný balíček (Seznam
  zastávek, Tabulka z dat), záhlaví za tlačítkem (Minuty), jen velikost a dvě barvy (Tabulka spojů), bez
  tučného (Poznámky). Skupina „Značky u odjezdů" má 37 polí, pořadí skupin nemá kostru (Minuty začínají
  „Skupiny dnů", Tabulka spojů „Řádky"). Návrh: kostra Obecné → specifické → Záhlaví → Buňky →
  Rámeček/Mřížka/Pozice/Podmínky, zóna stylu vždy stejným balíčkem `StyleFields`.
- **E13 Dialogy sloupců mají každý jiné ovládání** [M]. (a) popisky tlačítek hotové (changelog 2026-10-26
  (38), (39)); (b) uzavřeno bez změny - v Tabulce spojů se zadávají jen popisné sloupce, šířku spojů
  dopočítá tabulka, „Přizpůsobit" by spojům vzalo místo. Zbývá (c) sloučení do sdíleného `ColumnListEditor`
  (odloženo s E3).

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
- **Vědomě jinak (rozhodnutí Jan 26. 10., potvrzuje srpnové):** vysvětlivka „na znamení" je v Zastávkovém
  JŘ AD krátká a napevno („na znamení", `ZastavkoveJrAd.TextAtributu`), Linkový JŘ AD ji bere z číselníku
  stálých značek firmy („zastávka je jen na znamení nebo požádání"), agendy MHD z číselníku atributů MHD
  (`T4c`). Starý zastávkový tisk měl „zastávka pouze na znamení".
- **Vědomě jinak (rozhodnutí Jan 26. 10.):** zóny zastávky skládá každá agenda pravidlem svého starého
  modulu (`T8`): Zastávkový AD z trasy linky se záskokem na registr (`ZonyZastavky`), Linkový AD jen z trasy
  a „-" (`BuildZastavkaRadek`), Zastávkový MHD všechny zóny bez filtru systému, Linkový MHD jen systém
  linky spojené „/" (`ZonyZastavky`). Rozdíl je vidět jen u zastávek se zónou mimo trasu nebo ve dvou IDS.
- **Vědomě jinak (rozhodnutí Jan 26. 10.):** stanoviště u názvu zastávky (`T10b`). Linkový JŘ AD ho
  tiskne v řádku tabulky u zastávky s příznakem trasy „S" jako „, stan.16" (parita starého linkového tisku,
  `LinkovyJrAd` :1011), Zastávkový JŘ AD jen v nadpisu rozdělené vývěsky jako „, označník 3" / „, sloupek 1"
  (vzor KOVED #8828, `ZastavkoveJrAd` :718). Jiné místo i účel, zkratka „stan." neumí rozlišit označník
  a sloupek.

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
