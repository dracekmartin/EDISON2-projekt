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
- A4 ✅ (30. 10.) Údaje ze šablony pro server (sloupce, turnusy, jazyk) v jednom typu `Models/TemplateDataInputs`: vstup `GetData` i ukázkových dat, klíč jejich cache, nové načtení dat po změně (rovnost záznamu) a výjimky testu parametrů. Ukázková data nově dostávají i turnusy; tisk víc linek bere jazyk ze snímku šablony, ne z živé šablony na pozadí. Pátá kopie byla v živém testu tisku (`ZkouskaTiskuNaostro`, jazyk napevno „cs").
- A5 ✅ (30. 10.) Popisky vzorů sloupců posílá server u voleb parametru „Rozdělení sloupců" v aditivním poli `optionKeys` (rozhodnutí Jan: k volbám, ne do dat - data tisku a zlaté otisky se nemění). Agendy staví volby, klíče i převod volby na rozložení z jednoho seznamu (`VzorySloupcu`, `VzorySloupcuLinky`); klientské kopie `DepartureColumnPresets.All`/`Linkove` jsou pryč. Shodu voleb a `PRESETY_SLOUPCU` hlídá Caché `VzorySloupcuTest` (ověřeno mutací), čtení klíčů `VzorySloupcuZParametruTests`.
- A6 ✅ (30. 10., s E3) Výchozí mřížka 5 mm bez přichytávání v `DesignerState.Default*`, předvolby ji přebírají.
- A7 ✅ (30. 10.) Okno společných vlastností bere deskriptory z `TemplateElement.MultiEditDescriptors()` (tytéž jako panel prvku, skupiny „Rámeček prvku" a „Pozice a velikost"). Při tom nalezená chyba: Ctrl+Z vracel všem prvkům hodnotu prvního, teď každému jeho (reprodukováno testem `SpolecneVlastnostiTests`).
- A8 ✅ (30. 10.) Popisky zarovnání v `Models/Elements/AlignmentLabels.cs`; čte je panel, editor stylu sloupce (nabídka i šedá nápověda) a bubliny lišty formátu. Formuláře v Caché (`StyleFields.cls`, `TextElementEdit.cls`) hlídá `VolbyZarovnaniTests` (kód i popisek, ověřeno mutací).
- A9 ✅ (30. 10.) Lišta formátu hledá vlastnosti podle `PropertyDescriptor.ToolbarSlot`, ne podle popisku a skupiny; nastavují ho text a buňka pevné tabulky. Hlídá `ListaFormatuTests` (ověřeno mutací).
- A10 ✅ (30. 10., s E4) Menu Zobrazit → Levý panel ovládá totéž co ☰ a Ctrl+B.
- A11 ✅ (30. 10., s A1) Formulář prvku podle pravidla `{Typ}Edit` z katalogu prvků, existenci .cls hlídá `ElementCatalogTests`. Literály zůstaly jen u formulářů mimo prvky (sekce, stránka, řádek a buňka tabulky, vložení tabulky), každý na jednom místě.
- A12 ✅ (30. 10.) Příkazy vrstvy „Přenést dopředu / Odeslat dozadu / Přenést zcela dopředu / Odeslat zcela dozadu" (rozhodnutí Jan) všude: hlavní menu (`{td:Action}`), místní nabídka, bubliny i historie kroků berou text z `KeyboardShortcuts`. „Úplně dopředu/dozadu" je v testu názvosloví zakázané.

## B. Dvě místa + zpřísnit test (doporučeno, neslučovat)
- B1 ✅ (30. 10.) `PanelProtiFormulariTests` spáruje deskriptor s polem formuláře podle popisku (u stejného popisku ve více skupinách i podle skupiny) a porovná skupinu, meze, jednotku, druh a volby (kódy i popisky); formuláře čte offline `FormularePrvku` i se sdílenými částmi. Klíč do ~216 deskriptorů se nepřidával: popisek je s formulářem shodný povinně (`DescriptorLabelsMatchFormTests`), takže páruje stejně spolehlivě. Nalezeno a srovnáno: 23 skupin v kódu podle formulářů (Poznámky, obě tabulky), čára smí mít v panelu šířku i výšku 0 (`NejmensiRozmerMm`), pole výšky ve formulářích nejméně 1 (dřív 0, „podle obsahu" je prázdné pole; `BaseElementFields.AddPosition`, nasazeno). Výjimky: „0 = prázdné" u Nejvýš km sloupců a Symbol: Velikost. Ověřeno mutací.
- B2 ✅ (30. 10., s A2) Formulář u nenastavené vlastnosti stylu ukazuje hodnotu z kaskády u všech prvků s textem; testy `VychoziHodnotyZKaskadyTests`, `FormularTextuZKaskadyTests`.
- B3 ✅ (30. 10.) `Models/ParametrySestavy`: 12 jmen parametrů a 8 zamčených popisků voleb, které klient čte nebo porovnává (Minuty, Seznam zastávek, Poznámky, rozdělení sloupců, počet stran). `ParametrySestavyTests`: každou hodnotu posílá některá agenda a kód ji jinde nepíše napevno (výjimky: kontrakt dat linkovaný do integračních testů, zadání pro AI, záhlaví „Sloupek"). Ověřeno mutací.
- B4 ✅ (30. 10.) `PolozkyMimoNabidkuTests`: položka „prvek" musí být v `KontraktDat` se stejnou kolekcí i klíčem (dřív hledání klíče v uvozovkách kdekoli v kódu). `KontraktDatTests` dosazuje všechny řetězcové konstanty (dřív jen `StopFields`): našel `POPIS_DOPRAVCE` (sazba stránek, doplněn do kontraktu linkových JŘ, integrační test potvrdil) a tři čtení mimo data agendy (výjimky s důvodem). Ověřeno mutací.
- B5 ✅ (30. 10.) `RegistrAgendKontraktTests`: každý parametr `BaseAgenda.cls` registr čte (`ListAgendy`) a klíče registru = parametry `AgendaRegistryEntry`. Generický průchod %Dictionary zamítnut - jména klíčů se od parametrů liší (`AgendaVariantCode` → `code`). Ověřeno mutací.
- B6 ✅ (30. 10.) `KliceKontraktuTests`: klíče definice sloupců (klient píše = server čte, vzory serveru ⊆ definice, klient čte vzory týmiž klíči), `ZNAMEKLICEPARAMETRU` = klíče, které čte klient a formulář parametrů `AgendaParams.cls` (popis čte jen ten), typy sekce v `SectionEdit.cls` = `TypSekce` + názvy stromu. Ověřeno mutací.
- B7 ✅ (30. 10.) `KanonNazvosloviTests`: každý přeškrtnutý tvar §8 je v seznamu zákazů, nebo má výjimku s důvodem (7: slova správná v jiném významu). Ze 31 nehlídaných tvarů 24 doplněno do seznamů (canvas, Výplň, Pořadí vrstvy, popisové sloupce, Zobrazit nadpis, dynamická výška…), navíc „Max. "; žádný text je neporušoval. „dle" zakázat nejde (zamčené volby parametrů). Ověřeno mutací.
- B8 ✅ (30. 10.) `ZastupnaPoleSablonTests`: pole `{…}` dodávaných šablon (6 šablon) jsou v nabídce dat jejich agendy (parser `AgendaSchemaDriftTests`, porovnání verzí s převzatou nabídkou linkového JŘ), pole stránky nebo dopravci ze sazby. Dnes vše sedí; ověřeno mutací.
- B9 ✅ (30. 10.) Položka kontraktu nese příznak registru (`KontraktDat.Polozka.Priznak`: varianty tras ×4, vzory sloupců); `KontraktDatAgendTests` ho čte z `ListAgendy` a 5 výjimek podle kódu agendy zmizelo. Zbylé výjimky (sloupky, tarifní čísla, sloupec MHD, směr a dopravci v linkovém MHD) příznak nemají. Ověřeno mutací.

## C. Nechat být
Nápověda (jen v .cls), FromImage, nabídka dat (NabidkaDat.cls, C# čte ze serveru), serverSide, skupiny parametrů, visibleWhenColumn, typy a kategorie poznámek, formáty data, ElementKind (jen literály → konstanty), editor stylu sloupce (test existuje), šedé pozadí záhlaví E0 × E8 (ověřit záměr).

## D. Smazat
✅ (30. 10., souhlas Jan) Smazané `JsonFileDataSchemaProvider`, `CompositeDataSchemaProvider` a nepoužitý `CacheDataSchemaProvider` (stará cesta přes `LoadDBFieldIds`), odkaz `schemas\*.schema.json` v projektu a zmínky v dokumentaci. Bez serveru `EmptyDataSchemaProvider` - chování stejné, složka `schemas` neexistovala.

## E. Nalezené chyby (stav 30. 10.)
- E1 ✅ Formulář textu ukazuje u nenastavené vlastnosti stylu hodnotu z kaskády (`ElementFormBinder.InheritedValuesFor`); panel už nepřenáší zděděné hodnoty buňky pevné tabulky k dalšímu prvku. Ověřeno testem, ne v aplikaci.
- E2 ✅ F1 a ux §3: mezera u Ctrl+D + šipka = mezera pro přichytávání (výchozí 2 mm); škálování šablony je v menu Upravit, ne na liště.
- E3 ✅ Výchozí mřížka 5 mm bez přichytávání v jedné konstantě (`DesignerState.Default*`), předvolby ji přebírají; příručka opravená.
- E4 ✅ Menu Zobrazit → Levý panel ovládá totéž co ☰ a Ctrl+B; název „Levý panel". Ověřeno jen čtením a buildem, ne v aplikaci.
- E5 ✅ Panel a lišta ukazují výchozí písmo šablony místo „Arial" (s A2).
- E6 ✅ Asistent: Poznámky v obou JŘ (odvozeno z AgendaCapabilities), změna formátu stránky se ptá, 6 záložek, pevná tabulka je živý prvek.
- E7 ✅ Jména tabulek: „Pevná tabulka" a „Tabulka s řádky z dat" všude (rozhodnutí Jan 30. 10.), staré tvary zakázané v testu názvosloví. Jména mimo `ElementNames` (hlášky Minut, záhlaví Tabulky spojů) zůstávají ke katalogu prvků (A1).
- E8 ✅ Výška prvku podle obsahu: „(podle obsahu)" místo „(dynamická)".
- E9 ✅ Zastaralé komentáře a skill `novy-element` opravené; skupiny pro asistenta (`CapabilityCatalog`) odpovídají formulářům od B1.
- E10 ✅ „…" v místní nabídce, ux §3 doplněné o Ctrl+klik, Ctrl+kolečko a Esc. „Del/PgDn" × „Delete/PageDown" sjednocené s A3 na „Delete", „PageDown".
