# Revize „sjednocené věci definovat jednou" (30. 10. 2026)

Jednorázová analýza: po vytěžení do roadmapy a dokumentace se smaže (pravidlo v `TemplateDesigner/CLAUDE.md`).
Podklad ze tří průzkumů (vlastnosti prvků, agendy a data, názvy a konstanty UI), ověřený jen čtením kódu.

## Rozhodnutí Jan 30. 10.

1. Formuláře prvků zůstávají v Caché (opravují se na serveru bez nového exe), panel návrháře se proti nim hlídá přísnějším testem podle klíče pole (B1), neslučuje se.
2. Pořadí: nejdřív opravit nalezené chyby (E), pak slučovat (A) a zpřísnit testy (B).
3. Výchozí mřížka plátna je 5 mm bez přichytávání (A6, E3).
4. Jméno dynamické a pevné tabulky: zjistit, jak se jim dnes říká, a podle toho sjednotit (E7).

## A. Sloučit na jedno místo (doporučeno)
- A1 ✅ (30. 10.) Katalog prvků `Models/ElementCatalog.cs`: typ, třída, jméno, popis, ikona, výchozí velikost, druhy agend, pozice v nabídce, založení instance. Odvozuje se z něj továrna, paleta, nabídka „Přidat prvek", `ElementNames`, `AgendaCapabilities`, seznam pro asistenta, seznam prvků v F1 a třída formuláře `{Typ}Edit`. Hlídá `ElementCatalogTests` (katalog = konstanty = atributy = třídy, formulář a ikona existují).
  Ručně zůstává: třída, `[JsonDerivedType]`, .cls, ikona, `SkiaHeightCalculator` (výška podle dat), `ZavislostNaDatech.JeStaticky` (bez prvku v seznamu se šablona bere jako závislá na datech, tedy bezpečně), poznámky pro asistenta (`CapabilityCatalog.ElementNote`). Jména mimo `ElementNames` (hlášky Minut, záhlaví Tabulky spojů) zůstávají.
- A2 ✅ (30. 10.) Části stylu prvku `TemplateElement.ResolvedStyleZones()` (z týchž metod jako kreslení; u Poznámek vytažené z Render) - z nich čte panel, lišta i formulář (`InheritedValuesFor`). 44 dosazených výchozích hodnot v deskriptorech pryč; zbylé znamenají „nenastaveno" (prázdný zdroj dat, 0 = automaticky). Symboly značek Minut vědomě mimo (velikost se dopočítá z minut). Výška řádku v `ScheduleElementBase` dál z `DataStyle.FontSize ?? 8f` (auto se rozpoznává zpětně, přepočet z kaskády by změnil, co Set považuje za auto).
- A3 ✅ (30. 10.) Tabulka zkratek `Designer/KeyboardShortcuts.cs` (akce, kategorie, klávesa; text se z klávesy skládá). Obsluha okna a buňky s ní porovnává (přesná shoda), menu a bubliny přes `{td:Gesture}`/`{td:Tip}`, místní nabídky, F1 a seznam pro asistenta z ní. Věty se zkratkou (asistent, hlášky, příručka celá, ux §3) hlídá `KlavesoveZkratkyTests` (ověřeno mutací). Navigace v buňce (Tab, Enter, šipky) a psaní textu zůstávají v obsluze kódem.
- A4 Šablonové vstupy GetData (DEFINICE_SLOUPCU, JAZYK, TURNUSY_KE_SPOJUM): 4 kopie v C# (ComposeGetDataParams Parameters.cs:479-508, ComposeWireSnapshot Columns.cs:154 podle pořadí, MockDataInput CacheAgendaProvider.cs:552, InfrastructureKeys v testu) → jeden typ.
- A5 Popisky vzorů sloupců: DepartureColumnPresets.cs:20-37 × ROZDELENI_/DELENI_SLOUPCU → PRESETY_SLOUPCU aditivně nese popisek, C# čte z dat.
- A6 Výchozí mřížka a přichytávání: DesignerState.cs:58-69 (1 mm, zapnuto) × MainStatePref (5 mm, vypnuto) × SnapEngine × XAML × příručka §6 → jedna konstanta. ROZHODNOUT default.
- A7 Úprava více prvků: vlastní 4 deskriptory (ElementPropertiesWindowViewModel.cs:363-398, skupiny „Rámeček"/„Pozice") → GetPositionDescriptors + konstanty skupin. Triviální.
- A8 Volby zarovnání: 4 kopie (PropertyDescriptor.cs:109-121, StyleFields.cls:47-49, TextElementEdit.cls:69-78, CellStyleEditor.Converters.cs:65-82) → C# jedna, proti .cls test.
- A9 Lišta formátu (FormatToolbar.xaml.cs:40-55) hledá deskriptory podle textu skupiny/popisku → konstanty nebo klíč.
- A10 Levý panel: menu Zobrazit → „Panel sekcí" (xaml.cs:489) × ☰/Ctrl+B (:468) dvě implementace → jeden příkaz, jedno jméno (asi i chyba: MinWidth 120).
- A11 Třída formuláře `ALVA.TemplateDesigner.EDInputForm.*Edit`: 23 literálů v 10 souborech → konvence + test existence .cls.
- A12 Příkazy vrstvy: 4 tvary textů → jedna sada (ROZHODNOUT kanon).

## B. Dvě místa + zpřísnit test (doporučeno, neslučovat)
- B1 Popisky/skupiny/meze/jednotky/volby: C# deskriptory (~205) × *Edit.cls. Přidat klíč pole (cesta k property) do deskriptoru, test páruje podle klíče a hlídá vše. Neslučovat: formulář se opravuje na serveru bez nového exe, offline konzumenti (import PDF, AI katalog, záloha) by závisely na serveru, parser .cls v testech je křehký.
  Dnes nehlídané: skupiny (~180, 24 rozdílných: Poznámky „Filtrování" ×3 skupiny formuláře, Tabulka z dat „Tabulka", Pevná tabulka „Tabulka"), meze (~54, jen písmo + editor stylu), volby výběrů (22, neznámý klíč tiše bere první hodnotu ElementPropertyAccessor.cs:154).
  Rozdíly: šířka čáry min 1 C# × 0 .cls (TemplateElement.cs:331 × LineElementEdit.cls:53); Tabulka z dat barva záhlaví `?? #FFE8E8E8` × TableStyleDefaults prázdná = nic.
- B2 ✅ (30. 10., s A2) Formulář u nenastavené vlastnosti stylu ukazuje hodnotu z kaskády u všech prvků s textem; testy `VychoziHodnotyZKaskadyTests`, `FormularTextuZKaskadyTests`.
- B3 Zamčené popisky enumů, které C# porovnává (~15: DepartureGridElement.cs:1679-1693, NoteVisibilityFilter.cs:53-58, Columns.cs:204-207, PageLayoutEngine.cs:107-113) a názvy parametrů čtené klientem (~11: CASOVA_OSA, HODINY_OD/DO, PISMENA_VARIANT, ...) → .NET test proti options/parametrům v .cls; konstanty C# do jedné třídy.
- B4 KontraktDat × PolozkyMimoNabidku (36 „prvek"): test porovná přesně (kolekce + klíč), ne hledání řetězce; KontraktDatTests i čtení přes konstanty; POPIS_DOPRAVCE do kontraktu.
- B5 Registr agend: BaseAgenda 13 parametrů → ručně ListAgendy (AgendyRegistr.cls:51-77) × AgendaRegistryEntry. Test: každý Parameter z BaseAgenda má vlastnost; případně ListAgendy generickým průchodem %Dictionary.
- B6 JSON definice sloupců (8 klíčů, VlastniSloupce.cls:23-40 × IColumnLayoutOwner.cs:45-51), ZNAMEKLICEPARAMETRU (12, KontraktAgendTest.cls:15 × CacheAgendaProvider.cs:194-216), typy sekcí (SectionEdit.cls:37-40 × TemplateTreeNames) → testy.
- B7 Názvosloví: test kanon ux §8 (~~…~~) ↔ ban list (~12 tvarů chybí: canvas, Výplň, Pořadí vrstvy, popisové sloupce, Zobrazit nadpis, Max., dle - pozor „Automaticky (dle linky)" zamčené).
- B8 Zástupná pole `{POLE}` v SystemTemplates/LaunchTemplates proti nabídce dat agendy: test.
- B9 Výjimky KontraktDatAgendTests odvodit z příznaků registru místo kódu agendy.

## C. Nechat být
Nápověda (jen v .cls), FromImage, nabídka dat (NabidkaDat.cls, C# čte ze serveru), serverSide, skupiny parametrů, visibleWhenColumn, typy a kategorie poznámek, formáty data, ElementKind (jen literály → konstanty), editor stylu sloupce (test existuje), šedé pozadí záhlaví E0 × E8 (ověřit záměr).

## D. Smazat
Offline nabídka dat: JsonFileDataSchemaProvider, CompositeDataSchemaProvider, csproj:45 `schemas\*.schema.json` (složka neexistuje od 899e05ba), wiring TemplateDesignerViewModel.cs:378, zmínky v cache-api.md.

## E. Nalezené chyby (stav 30. 10.)
- E1 ✅ Formulář textu ukazuje u nenastavené vlastnosti stylu hodnotu z kaskády (`ElementFormBinder.InheritedValuesFor`); panel už nepřenáší zděděné hodnoty buňky pevné tabulky k dalšímu prvku. Ověřeno testem, ne v aplikaci.
- E2 ✅ F1 a ux §3: mezera u Ctrl+D + šipka = mezera pro přichytávání (výchozí 2 mm); škálování šablony je v menu Upravit, ne na liště.
- E3 ✅ Výchozí mřížka 5 mm bez přichytávání v jedné konstantě (`DesignerState.Default*`), předvolby ji přebírají; příručka opravená.
- E4 ✅ Menu Zobrazit → Levý panel ovládá totéž co ☰ a Ctrl+B; název „Levý panel". Ověřeno jen čtením a buildem, ne v aplikaci.
- E5 ✅ Panel a lišta ukazují výchozí písmo šablony místo „Arial" (s A2).
- E6 ✅ Asistent: Poznámky v obou JŘ (odvozeno z AgendaCapabilities), změna formátu stránky se ptá, 6 záložek, pevná tabulka je živý prvek.
- E7 ✅ Jména tabulek: „Pevná tabulka" a „Tabulka s řádky z dat" všude (rozhodnutí Jan 30. 10.), staré tvary zakázané v testu názvosloví. Jména mimo `ElementNames` (hlášky Minut, záhlaví Tabulky spojů) zůstávají ke katalogu prvků (A1).
- E8 ✅ Výška prvku podle obsahu: „(podle obsahu)" místo „(dynamická)".
- E9 ✅ Zastaralé komentáře a skill `novy-element` opravené; `CapabilityCatalog.cs:251` (skupiny „jak je uživatel vidí") zůstává do B1.
- E10 ✅ „…" v místní nabídce, ux §3 doplněné o Ctrl+klik, Ctrl+kolečko a Esc. „Del/PgDn" × „Delete/PageDown" sjednocené s A3 na „Delete", „PageDown".
