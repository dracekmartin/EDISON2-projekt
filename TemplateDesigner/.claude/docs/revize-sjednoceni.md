# Revize „sjednocené věci definovat jednou" (30. 10. 2026)

Jednorázová analýza: po vytěžení do roadmapy a dokumentace se smaže (pravidlo v `TemplateDesigner/CLAUDE.md`).
Podklad ze tří průzkumů (vlastnosti prvků, agendy a data, názvy a konstanty UI), ověřený jen čtením kódu.

## Rozhodnutí Jan 30. 10.

1. Formuláře prvků zůstávají v Caché (opravují se na serveru bez nového exe), panel návrháře se proti nim hlídá přísnějším testem podle klíče pole (B1), neslučuje se.
2. Pořadí: nejdřív opravit nalezené chyby (E), pak slučovat (A) a zpřísnit testy (B).
3. Výchozí mřížka plátna je 5 mm bez přichytávání (A6, E3).
4. Jméno dynamické a pevné tabulky: zjistit, jak se jim dnes říká, a podle toho sjednotit (E7).

## A. Sloučit na jedno místo (doporučeno)
- A1 Katalog prvků `ElementCatalog` (Models): typ, jméno, ikona, výchozí rozměr, factory, druh (obecný/STOP/LINE), statický, poznámka pro AI; třída formuláře konvencí `{Type.Name}Edit`.
  Nový prvek dnes 12 míst (obecný) / ~20 (JŘ s oknem); katalog odvodí 13-14. Ručně zůstane třída, `[JsonDerivedType]`, .cls, ikona, docs. Test katalog = atributy = třídy.
  Místa: ElementTypes.cs, TemplateElement.cs:11-21, Editing.cs:263-300, Menus.cs:353-375, MainWindow.xaml:339-386, Properties.cs:553-557 a :104-135, AgendaCapabilities.cs, CapabilityCatalog.cs:304/325, SkiaHeightCalculator.cs:17, ZavislostNaDatech.cs:41, ElementNames.
- A2 Výchozí hodnoty písma/velikosti v deskriptorech: 78× `?? 9f`, `?? "Arial"` v Elements/*.cs → číst z kaskády (`Resolve*Parent`), „Arial" jedna konstanta. CellDefaults (Arial 9), ResolvedTextStyle.Default (10 Left/Top), TextStyle.CreateDefault (10 Center), ScheduleHeader/DataDefaults (9/8).
- A3 Klávesové zkratky: C# tabulka (gesto, akce, kategorie) → F1 HelpDialogViewModel, CapabilityCatalog:53-144, bubliny, InputGestureText. 7 kopií. Obsluha MainWindow.Keyboard.cs zůstává kódem. Příručka §12 + ux §3: test podmnožiny.
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
- B2 Test výchozích hodnot rozšířit: vlastnost null → formulář musí ukázat hodnotu z kaskády.
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

## E. Nalezené chyby (opravit zvlášť)
- E1 Text z importu PDF: VAlign null → formulář „Na střed" (TextElementEdit.cls:78 default), tiskne se „Nahoře" (ResolvedTextStyle.Default). Ověřeno čtením, ne v aplikaci.
- E2 Ctrl+D + šipka: kód State.SnapGapMm (2 mm, Editing.cs:159), ux §3 + HelpDialogViewModel.cs:183,355 tvrdí 4 mm.
- E3 Výchozí mřížka: ApplyLoadedPreferences (TemplateDesignerViewModel.cs:413-425) přepíše DesignerState 1 mm/zapnuto na 5 mm/vypnuto (odhad).
- E4 Panel sekcí v menu nejspíš panel neschová (MinWidth 120) a nesynchronizuje se s ☰ (odhad).
- E5 Panel ukazuje „Arial" místo výchozího fontu šablony (TextElement.cs:153, NotesLegendElement.cs:800,805) (odhad).
- E6 AI texty zastaralé: Poznámky „(zastávkový JŘ)" CapabilityCatalog.cs:325; „TableElement NEPOUŽÍVEJ" AnthropicApiClient.cs:165; „změna rozměru = nová šablona" CapabilityCatalog.cs:119; 5 záložek místo 6 :125; F1 „lišta 50–200 %" HelpDialogViewModel.cs:353.
- E7 Jména prvků mimo ElementNames: bubliny palety (xaml:359,363,379), Menus.cs:373, DepartureColumnWarnings.cs:45,55, LineTableElement.cs:3704; „Tabulka (z dat)" × „Dynamická tabulka" (příručka). ROZHODNOUT kanon.
- E8 Výška „(dynamická)" TemplateElement.cs:334 × kanon „výška podle obsahu".
- E9 Zastaralé: skill novy-element (handler, brána drift testu, chybějící místa), ElementNames.cs:11 (BuildAddElementMenu), TemplateElement.cs:324-326 (rozsahy hlídá test), DescriptorLabelsMatchFormTests.cs:9, CapabilityCatalog.cs:251, DynamicTableElement.cs:18 „dormant".
- E10 Drobnosti: „..." × „…" (Mouse.cs:624,658), „Del/PgDn" × „Delete/PageDown", ux §3 chybí Ctrl+klik, Ctrl+kolečko, Esc na úvodní obrazovce.
