# Changelog

## 2026-07-28 - Interní nálezy A1: mock data náhledu bez freeze + úplné škálování šablony

Úklidová dávka po vydání verze (Fáze A, blok B16 + B17).

- **B16 - mock data náhledu nezamrzají UI.** Kreslicí cesta náhledu volala `GetMockData`
  **synchronně** (cold-start Caché 2-3 s = freeze při prvním vykreslení) a neúspěch se necachoval
  (→ nový HTTP pokus při každém překreslení). Nově: render čte **jen z cache**
  (`TryGetCachedMockData`), data se **prefetchují na pozadí** při změně agendy
  (`RefreshFieldsListCore` → `PrefetchMockDataAsync`) a po dosednutí překreslí náhled; do doby
  načtení se ukáží syntetická data ze schématu. Selhání/prázdno se **cachuje** (fail-soft),
  čistí ho „Obnovit data" (F5).
- **B17 - úplné škálování šablony (`ApplyScale`).** Nový `TextStyle.ApplyScale` (font v pt +
  paddingy v mm; `LineSpacing` bezrozměrný → NE) použit všude. Opraveno: schedule elementy
  škálovaly jen `FontSize`, ne paddingy; `DataRowHeight` se u LineTable/StopList/DepartureGrid
  neškáloval (nebo dvakrát - přesunuto do `ScheduleElementBase`, jednou); **per-sloupcové**
  (`ColumnVisualStyle`/`TableColumn`) a **per-skupinové** styly + `WidthOverride` skupin dnů +
  Fixed buňky `TableElement` + element-level styly Table/DynamicTable se dřív neškálovaly vůbec.
  Procentní šířky sloupců se (správně) neškálují.
- **Kodifikace UI/UX výjimek** do `architecture.md` (OK přes `Click=` v 6 EDInputForm dialozích
  kvůli `GeneralForm.Save()`, 5 tabů TableEditoru = uspaný element, ~59 `Click=` v MainWindow
  = postupná migrace při dotyku).
- Testy: `ApplyScaleTests` (4 - font+padding, per-sloupec/skupina, DataRowHeight jednou, % beze
  změny, LineSpacing beze změny). .NET 409/409.

## 2026-07-28 - Parita voleb legendy + nápovědy parametrů variant (do verze)

Dvě malé věci na přání Jana před publishem:

- **NotesLegend „Dle typu" doplněno do dialogu** - výchozí režim řazení poznámek v EDInputForm
  dialogu úplně chyběl (šel nastavit jen v panelu); volby sjednoceny 1:1 s panelem. Druhá půlka
  nálezu (TextElement „Do bloku" jen v dialogu) byla zastaralá - v panelu už je.
- **Nápovědy parametrů sestavy** - aditivní klíč `description` v GetParameters → helpText bublina
  ve formuláři (mapuje `AgendaParams.AddParamField`, `AddBoolParam` má nový volitelný parametr).
  Vyplněno u čtveřice variant/označník parametrů - hlavně vysvětlení, že písmena variant nahrazují
  automatické poznámky rozdílů tras. Starší agendy bez description beze změny.
- Testy .NET 405/405, Caché 45/45.

## 2026-07-28 - Konec dlouhých pomlček (em dash) v celém projektu

Rozhodnutí Jan: žádné em dashe - působí jako generovaný text. Hromadná náhrada za běžnou
pomlčku „-" napříč TemplateDesigner + testy (270 souborů, 5354 výskytů: kód, komentáře, UI texty,
dokumentace; rámečkové čáry ─ v komentářích nedotčené). Pravidlo zapsáno do TD i root CLAUDE.md,
UI labely nově hlídá ban v `EDInputFormSchemaDriftTests`. Ostatní projekty (EDControls,
EDISONMAIN2) záměrně nedotčené - mimo TD scope. Testy .NET 405/405, Caché 45/45 (gold beze
změny - em dash nebyl v žádném datovém výstupu).

## 2026-07-28 - Drobné nálezy revize: tichá selhání, guardy, kosmetika + 8 testů

Druhá dávka z nezávislé revize (po názvoslovném kanonu).

- **NotesLegend tučný symbol** - bezpodmínečný override `Bold = true` přebíjel uživatelovu volbu
  (komentář sliboval opak); teď default tučně JEN když `DataStyle.Bold` nenastaveno (anti-pattern
  scalar override z `mvvm-patterns.md`).
- **Tichý catch při načtení obrázku** (EDInputForm image field, App.xaml.cs) - selhání čtení/enkódování
  souboru nově hlásí přes `IPopupDisplayer.Error` (R2), dřív jen tiše nic.
- **Sjednocení skupiny „Podmínky"** - 7 formulářů mělo „Pokročilé" (s Uzamčeno uvnitř); nyní všude
  „Podmínky" a **Uzamčeno přesunuto do „Pozice a velikost"** (vzor novějších dialogů). Ban sken drift
  testu rozšířen i o labely skupin (`AddGroup`).
- **Kosmetika:** volby „Pozice šipky" v dialogu = plné znění jako v panelu („Vpravo (u pravého okraje
  elementu)"…), tooltip přetečení bez kódového „PageSize", „v návrháři" místo „v designeru", elipsy
  „…" v ButtonLabelech, komentáře vědomých výjimek (2× White v MainWindow, IsDefault+IsCancel na
  Zavřít v PrintDialogu).
- **Testy (+8):** `GroupColumnIndexResolverTests` (4 - poziční `__INDEX_` klíče, priorita reálného ID),
  `StopListCellTextTests` (4 - režimy sloupce Označník vč. fallbacku). `InternalsVisibleTo` pro testy
  (konec zveřejňování helperů kvůli testům). .NET 405/405.
- **Pozn.:** nález „GetMockData bez cache" z revize je zastaralý - cache existuje; skutečný zbytek R1
  (synchronní HTTP z kreslicí cesty při prvním načtení/selhání) řešen samostatně.

## 2026-07-28 - Názvoslovný kanon zamčený drift testy + hromadné sjednocení labelů

Z nezávislé revize (druhá session). Nejdřív pojistka, pak hromadné přejmenování - v tomto pořadí.

- **Drift testy rozšířeny** (`EDInputFormSchemaDriftTests`): (a) „stejný field key = stejný label
  napříč všemi EDInputForm .cls" (výjimky `Height`/`StrokeWidth` s komentářem), (b) ban list
  zakázaných tvarů v .cls labelech i volbách, (c) tentýž ban list nad panelovými PropertyDescriptors
  všech elementů. + oprava hledání kořene repa (fallback z cesty zdrojáku) - drift testy už neběží
  falešně červené z izolovaného výstupního adresáře.
- **Rozhodnutí Jan (kanon):** „Uzamčeno" (ne Zamčeno) · „Podmínka viditelnosti" (ne Viditelnost) ·
  „Tloušťka rámečku/okraje" (ne Šířka rámečku, Tl. okraje). Zapsáno do `ux-conventions §8`.
- **Hromadné sjednocení** (panel + dialogy + .cls): Tučné/Podtržení/Přeškrtnutí (ne Tučně/Podtržené/
  Přeškrtnuté - i CellStyleEditor a FormatToolbar), Vodorovně/Svisle + Nahoře/Dole, „Střídání řádků"
  (ne Zebra), „Velikost aktuální" (ne Font aktuální), „Řádek negativních značek" (ne Řádek NZ),
  „Přestupy (vlak/autobus)", „Barva pozadí" jednotně (ne Pozadí/Výplň), kontextové menu „Vrstva"
  (ne Z-pořadí), LineTable „Výška řádku (prázdné = auto)", + stale komentáře. Wire klíče beze změny.
- Testy: .NET 397/397 (drift 17/17), Caché 45/45.

## 2026-07-28 - Mechanické opravy z celkové revize (UI/UX, názvosloví, docs)

Quick wins z celkového auditu stavu (4 paralelní revize: UI/UX, názvosloví, testy, architektura).

- **Dark mode:** hlavička „Strom šablony" měla natvrdo `#555555` (nečitelné na tmavém panelu) →
  `EDTheme.Text.Secondary`. Jediný hardcoded hex brush v celém XAML.
- **Názvosloví:** „Povolit split" → **„Povolit rozdělení"** (přetečení sekce, panel i EDInputForm);
  „Velikost písma pozn." → **„Velikost poznámek"** (NotesLegend, panel i dialog - kanon §8);
  helpText „Legacy JŘ" → „Původní JŘ" (přestupy StopListu).
- **F1 nápověda:** smazán stale řádek „Sdílená hranice - tah za hranu mění i souseda" (funkce
  odstraněna už dřív, nápověda ji dál slibovala).
- **CLAUDE.md:** smazáno stale tvrzení o přepínači `InvertovatNegativniZnacky` (v kódu neexistuje,
  inverze je vždy zap); aktuální focus doplněn o stanovišťový balík #13048/#8828; datum aktualizace.

## 2026-07-28 - Doladění stanovišťového balíku: slovník „označník", titulek stránky, držení stránky, nálezy revize

Kolo zpětné vazby Jana nad 320680/330510 + nezávislá revize. Vše doladění #13048/#8828, gold beze změny.

- **Jednotný slovník „označník"** (rozhodnutí Jan): všechny UI texty (parametr, combobox elementu,
  sloupec, pole agend AD/MHD/linková, F1 nápověda) sjednoceny na „označník" - konec míchání
  nástupiště/stanoviště/sloupek; UI texty bez cizích výrazů („per"). Kanon v `ux-conventions §8`
  + `CLAUDE.md`; wire názvy (`NASTUPISTE`…) beze změny. Výjimka: legacy legenda „tisk stanoviště".
- **Titulek rozdělené stránky (#8828):** `POPIS_ZASTAVKA` += „, označník X" (jen při rozdělení) -
  propíše se do titulku i názvů exportních souborů; parametr přejmenován na **„Samostatná stránka
  pro každý označník"** (zrcadlí volbu elementu „Řádek pro každý označník"); **řádek vývěskové
  zastávky** na rozdělené stránce nese označník stránky (jinak stránky 11/22 vizuálně identické).
- **Stránkování náhledu:** re-fetch dat (změna server parametru, F5) **drží logickou stránku**
  (zastávka×směr, přednost shodě označníku - `FindMatchingPageIndex` + testy), ne pořadové číslo;
  deferovaný resync comboboxu stránek (WPF po výměně ItemsSource nechává prázdný zobrazovací box).
  Seznam server-side parametrů zobecněn (`ServerSideParams`).
- **Oprava bool parametrů na wire:** klient posílal `ToString()` „True", server srovnával „true" →
  bool parametry GetData nikdy nezabraly (i TR2). Fix obou stran (normalizace + tolerantní čtení).
- **Nálezy nezávislé revize (28. 7.):** (1) spoj úplně bez označníku při rozdělení spadne na stránku
  nejčetnějšího označníku - dřív zmizel ze všech (ztráta odjezdu); (2) „Řádek pro každý označník"
  se neaplikuje bez zapnutých variantních sloupců (duplikáty by nesly identický čas) + helpText
  navádí na sloupec Označník; (3) záměr „varianty se na rozdělené stránce nefiltrují" zdokumentován
  (stejná písmena napříč stránkami téže zastávky); (4) labely enum parametrů = zamčené (persistuje
  se text - `ux-conventions §8`); spodní mez zmenšení textu evidována k B2.
- **Testové pokrytí doplněno:** Caché `TestPismenaVariant` (end-to-end písmena variant, 0330510)
  + `TestSplitTitulekARadek` (titulek + vývěskový řádek, 0320680); xUnit `CellShrinkTests` (4),
  `PreviewPageMatchTests` (5), gate testu duplikace. Caché 45/45, dotčené .NET sady zelené.

## 2026-07-27 - Vývěska per označník (#8828 TRANSDEV) - stanovišťový balík dokončen

Zastávka obsluhovaná z více označníků (stanovišť), kde různé spoje jedou z různých sloupků, se dá
vytisknout jako **samostatná vývěska per označník** - každá jen s odjezdy z toho označníku (TRANSDEV
linky 876420/863383; referenční podoba = KOVED XLSX 802366).

- **Tiskový parametr** „Rozdělit vývěsku per označník" (skupina „Seznam zastávek", default vyp = dnešní
  jedna vývěska s odjezdy smíchanými). Server-side (věc generování výstupu, ne element).
- **Server (`ZastavkoveJrAd`)** - při zapnutí vývěska obsluhovaná z N označníků (dle `NastupisteSpoju`)
  → **N stránek**; `NacistOdjezdy` filtruje spoje na daný označník (per-spoj sloupek = W pozice 7,
  fallback J-věta); `NASTUPISTE` stránky = její označník. Jen označníky, ze kterých ≥1 spoj. Nezávislé
  na duplikaci řádku (bod 4).
- **Gold beze změny** (parametr default vyp → byte-identický výstup, žádný re-baseline). Nový
  `TestSplitPerOznacnik` (0320680: ČB aut.nádr. → označníky 11/22, odjezdy 5+1). 43/43 Caché.
- **Ověřeno:** 0320680 OFF=53 stránek, ON=55; ČB aut.nádr. (T) → 2 stránky (označník 11 = 5 odjezdů,
  22 = 1 odjezd).
- **Follow-up (odloženo):** členění exportu na samostatné soubory per označník (dnes lze rozlišit
  tokenem `{NASTUPISTE}` v názvu). Titulek/label výše než sloupec Nástupiště = levné doplnění.

## 2026-07-27 - Šev mezi plátnem a náhledem (blok B6, část a)

Na hranici návrháře a náhledu byl vidět „zdvojený pruh". Příčina nebyla jedna: **čtyři místa, která
mají mít shodnou barvu plochy, ji měla různou** - plocha kolem plátna brala špatný token
(`Border.Light`, skoro bílá), plocha kolem náhledu a pruh se stránkováním měly `#E4E4E4` natvrdo
v XAML, plátno stejnou barvu natvrdo v kódu. Správný token (`EDTheme.Bg.Canvas`, vč. tmavé varianty)
přitom existoval a nikdo ho nepoužíval. Druhá složka švu = tečkovaný zaostřovací rámeček děličů,
který zůstával po tažení.

- **Jeden token pro celou plochu** - oba `ScrollViewer`y, pruh stránkování i oba `GridSplitter`y na
  `EDTheme.Bg.Canvas`; děliče `FocusVisualStyle = null`, takže mezi plátnem a náhledem není žádná
  linka (oddělení dělá rámeček panelu náhledu).
- **Skia strana má jediný zdroj** - `Designer/CanvasTheme.Background` nahradilo dvě nezávislé
  konstanty v `DesignerCanvas`/`PreviewCanvas`; plní ho `MainWindow` z téhož tokenu, takže se WPF
  a Skia barva nemůže rozejít. Napojeno na `ThemeService.Applied` → plocha reaguje na přepnutí tématu
  (odregistrace v `OnWindowClosing` - statická událost by zavřené okno držela naživu).
- **Vědomě mimo:** ostatní chrome návrháře (papír, pravítka, záhlaví sekcí, výběr) zůstává pevný -
  patří do samostatné položky „dark mode", která potřebuje rozhodnutí, jak má v tmavém režimu vypadat
  *papír* (tiskne se na bílý). Export se nezměnil - PDF/PNG si plochu čistí bílou zvlášť.
- Vizuálně ověřeno Janem (šev i tečkovaný rámeček po tažení děliče zmizely). Zbývá část (b) -
  rozvržení editoru stylu buněk.

## 2026-07-27 - Drobné korektnostní nálezy (blok B3)

Dva nezávislé tiché nálezy z předchozí revize kódu, oba „nic nespadne, jen se ztratí nastavení".

- **Dialog pevné tabulky mazal per-buňkový formát a styl.** Řádkový mezi-model dialogu
  (`RowVM`/`CellVM`) nesl jen text buňky → otevřít + OK zahodilo `Format`, per-buňkový `Style`
  a barvu pozadí řádku. Nejvíc to bolelo u tabulek načtených ze šablony agendy - Caché posílá
  `format` (např. `dd.MM.yyyy`) a barvy per buňku, dialog je nikdy needitoval, ale smazal.
  Nově se přenášejí (styl se klonuje, ne sdílí). `TableElement.Clone()` byl v pořádku už dřív
  (drží ho reflexní `CloneCompletenessTests`) - problém byl jen v cestě přes dialog.
- **Ruční kreslicí / měřicí cesty JŘ elementů ignorovaly řez písma.** Legenda poznámek a měření
  záhlaví odjezdů si stavěly typeface natvrdo jako Normal/Bold, takže uživatelova váha písma
  (a kurzíva) se do nich nepropsala - a u legendy se navíc rozešlo měření s renderem (počet
  zalomených řádků → špatná výška boxu). Sjednoceno na kaskádu: nový helper
  `ScheduleElementBase.GetTypeface(ResolvedTextStyle)` (rodina + řez z `ToSKFontStyle`), použitý
  v legendě (text, záhlaví, zvýrazněný symbol, výpočet výšky), v měření a wrapu záhlaví skupin dnů
  u odjezdů a v designerových placeholderech odjezdů / linkové tabulky / seznamu zastávek + v měření
  šířky značek zastávky. Legenda tím zároveň začala respektovat **výchozí font dokumentu** (dřív
  padala na Arial).
- **Testy:** 8 nových xUnit (`TableEditorRowRoundTripTests` 4× round-trip řádku, `ScheduleFontCutTests`
  3× řez z kaskády + výška legendy reagující na tučný řez - ověřeno, že bez fixu padá). 384/384 xUnit.

## 2026-07-27 - Duplikace řádku per stanoviště (#13048 KOVED, bod 4)

Zastávka obsluhovaná z více stanovišť (sloupků) se v seznamu vypíše jako **N řádků, jeden per
stanoviště** (KOVED „duplikace řádku ... příklad Medlovice"). 4. volba comboboxu „Více nástupišť"
vedle „nejčetnější" / „výčet".

- **Server (aditivně, `ZastavkoveJrAd`)** - nové wire-pole `NASTUPISTE_VARIANT` per zastávka
  (nástupiště per varianta, zarovnané s `TRASA_MIN`; z reprezentativní trasy varianty, W pole 7,
  fallback J-věta). Identita variant se **nemění** → gold jen re-baseline (počty stejné, přibylo pole).
- **Klient (`StopListElement.ExpandPlatformRows`)** - režim `RowPerPlatform`: zastávku se ≥2 stanovišti
  rozvine na N řádků **na jejím místě v trase** (pořadí = první výskyt stanoviště napříč variantami,
  netřídí se). Každý řádek nese své stanoviště + **časy jen variant, které z něj jedou** (ostatní
  prázdné - řeší bod 5 „jiné stanoviště = jiná varianta" na úrovni zobrazení). Vývěska a jednostanovištní
  zastávka = 1 řádek; značky (atributy / na znamení / poznámky) jen na prvním řádku.
- **Testy:** 6 xUnit (`StopListPlatformDuplicationTests`, vč. zachování značky „<" projíždějící varianty) +
  Caché `TestNastupisteVariantEmit` (data lock na 0330510). Gold 0365001/0365005 re-baseline. 42/42 Caché + 6/6 xUnit.
- **Známé omezení:** nástupiště per varianta = z **reprezentativního spoje** varianty (jako TRASA_MIN).
  Dva spoje se **shodným sledem zastávek**, ale různým stanovištěm, se slijí do jedné varianty → zobrazí se
  jen stanoviště reprezentanta. Rozlišení i tohoto případu = stanoviště do **identity varianty** (vědomě NE -
  dávalo vizuálně identické sloupce, viz bod 5 / `c6d661d`); reálná data (0320680) mají různá stanoviště
  napříč různými variantami, kde to funguje. Plné rozlišení až na reálnou potřebu KOVED.

## 2026-07-27 - Varianty tras dle trasy + písmena u odjezdů (#13048 KOVED, bod 5)

Návrat k legacy sémantice variantních časových sloupců + KOVED rozšíření. Jen server
(`ZastavkoveJrAd`), .NET beze změny (anotace/hlavičky/pořadí jedou z dat).

- **Seskupování variant PŘEDĚLÁNO (bezpodmínečně):** varianta = **sled obsluhovaných zastávek**
  (1:1 legacy dedup dle `prutmbmp`, `JRapiLinPrtMhd1` ř.266-300 - „ponechá se delší čas"),
  NE časový profil jako dřív. Spoje stejné trasy s jinými časy se slévají; čas sloupce =
  **maximum přes spoje** („nejpozději za X min"). Efekt 0330510/T: 37 spojů, 33 sloupců →
  **11 variant tras** (hlavní 21 spojů; sonda `AdVariantyProbe.Scan`). **Nástupiště do identity
  NEJDE** (první verze ho měla - dalo vizuálně identické duplicitní časové sloupce, vidět na
  smoke testu; KOVED „jiné stanoviště = jiná varianta" pokryje vývěska per sloupek #8828,
  helper `WNastupisteMapa` připraven).
- **Oprava `|` ve W-trase = spoj nestaví** (nález ze smoke testu - „časy 0"): `|` znamená
  „zastávka na trase, ale spoj v ní nestaví" (legacy `JRapiExp10` ř.272-275 přeskakuje
  `<`/`|`/prázdné shodně); `CasHodnota` ho dřív četl přes `+"|"` jako čas 0 → falešné nuly
  v časových sloupcích, falešné „obsloužené" zastávky a slité varianty. Nyní: `CasHodnota("|")=""`
  (spoj s `|` na vývěskové zastávce nedostane odjezd), `ObsluhovanePoradi` `|`-only zastávky
  nezařadí, ve variantních sloupcích se `|` ukáže jako `<`. Gold beze změny (0365001/0365005
  `|` neobsahují), unit test rozšířen.
- **Řazení sloupců** - parametr „Řazení variant tras": *Dle četnosti ze zastávky* (default, KOVED -
  nejčastější první) / *Stabilní (pořadí spojů)* (TR7/TRANSDEV kontinuita). Parametry ve skupině
  „Seznam zastávek" (týkají se variantních sloupců StopListu). **.NET:** re-fetch trigger
  zobecněn z hardcoded `ROZDELENI_SLOUPCU` na seznam `ServerSideParams` (snapshot porovnání) -
  změna kteréhokoli variantního parametru přenačte data.
- **Oprava bool parametrů na wire (latentní, nález ze smoke testu):** klient posílal C# bool
  přes `ToString()` = **„True"**, server porovnává `= "true"` → bool parametry GetData nikdy
  nezabraly (týkalo se i TR2 `SOUHRN_NA_ZNAMENI` - proto „čekal ověření"). Fix obou stran:
  `CacheAgendaProvider.GetData` normalizuje bool → `"true"/"false"` (wire kontrakt),
  server čte tolerantně přes `$zcvt(...,"L")` (funguje i se starším klientem).
- **Písmena variant u odjezdů** - parametr „Písmena variant u odjezdů" (default **vyp** = beze změny
  pro stávající zákazníky): hlavičky sloupců = písmena A–Z (`PismenoVarianty` + unit test), u minuty
  odjezdu anotace s písmenem varianty (kategorie `varianta-trasy` - mimo filtry #7955), volba
  „První varianta bez písmene" (default zap - hlavní trasa čistá). Písmena **nahrazují automatické
  rozdílové poznámky** (`RozdilyTras` se nepočítá - jinak dvě abecedy na vývěsce); ruční poznámky
  zákazníka nedotčeny. Vyp = původní hlavičky (písmena rozdílů / „T1"…).
- **Ověřeno:** sonda `AdVariantyProbe.Stranka` (ostrá `BuildStrankaZastavky`) na 0330510: hlavičky
  `["", "A" … "J"]`, 16 anotací písmen = přesně počet spojů mimo hlavní variantu (37−21),
  `rozdil-trasy` v anotacích nahrazeno; default režim = kategorie beze změny. Gold: 0365001 **beze
  změny**, 0365005 re-baseline (jen pořadí 6 sloupců dle četnosti; počty 1:1). Caché 41/41,
  .NET 370/370.

Zákazník: spoje jedou z různých sloupků jedné zastávky → volba chování sloupce „Nástupiště".

- **Server (`ZastavkoveJrAd`):** nové pole řádku zastávky **`NASTUPISTE_SPOJU`** - nástupiště
  posbíraná ze spojů směru dle legacy pravidla „**nástupiště ze spoje, není-li, tak z linky**"
  (`JRapiExp10:1222-1226`): per-spoj hodnota z W-trasy, fallback J-věta `#3`. CSV seřazené dle
  četnosti (první = nejčetnější; shoda → menší hodnota, čistý helper `NastupisteCsv` + unit test).
  Agregace cachovaná per linka×směr (`^||TiskAd("nastCsv")`). ⚠️ **Past ověřená v legacy zdrojích:
  pole `trs(8)` = W-segment pozice 7** - legacy čas/čekání rozpadá W pozici 2 na dvě pole
  (`JRapiSp:234-241`), indexy jsou posunuté (první pokus s pozicí 8 četl zóny).
- **Element (StopList):** přepínač **„Více nástupišť"** (`MultiPlatformMode`): *Z trasy linky*
  (default = dnešní chování) / *Nejčetnější dle spojů* / *Výčet všech („2, 3")*. Panel + dialog
  (EDInputForm). Sloupec „Nástupiště" zůstává opt-in.
- **Data ověřena:** sonda `AdWTrasaProbe.ScanNastupiste` - per-spoj sloupky plní stovky linek
  (CB_GWBUS 541, CB_CSADAD 339) a multi-sloupkových zastávek jsou desítky (197/153; vč. gold
  0320993, 0320680, 0330510). End-to-end na 0330510: ČB aut.nádr. → „1,2,28,3" (fallback z trasy
  správně mezi četnostmi). Uzavírá i datový průzkum pro #8828 (vývěska per sloupek - zbývá stránkování).
- Gold: re-baseline 0365001/0365005 (jen md5 - nové pole v JSON; počty beze změny). Caché 40/40,
  .NET 370/370.

## 2026-07-27 - Přetékající text buněk se zmenšuje na šířku - vestavěné chování (#13048 KOVED, bod 2)

Zákazník chtěl u přetékajících názvů zastávek / seznamu zón zmenšení fontu jako v Excelu
(„přizpůsobit buňce"). Po UX diskusi **bez konfigurace - vestavěné chování všech buněk tabulek**:
dosavadní „chování" při přetečení bylo, že text podlezl pozadí vedlejší buňky (useknutá půlka
slova) - to není nikdy záměr. Žádný checkbox; opt-out doplníme až na reálnou poptávku.

- **`CellRenderer`**: text širší než buňka → proporcionální zmenšení fontu; **trigger až skutečné
  přetečení** (sloupce s auto šířkou na míru textu se nemění), cíl = šířka minus rezerva 0,6 mm
  (nekončit na hraně). Víceřádkový text dle nejširšího řádku (jednotná velikost). Hot path bez
  alokace navíc (`ComputeShrinkScale` overload s existujícím fontem).
- **StopList + značky za názvem**: zmenšení názvu počítá i s šířkou značek (`TrailingWidthMm`
  v `CellSpec` - obsah škálující se s textem); značky se posouvají i zmenšují se jménem (sdílený
  výpočet škály = pozice se nerozjedou). **Rezerva dle šipky směru**: u sloupce názvů se šipkou
  vpravo = odsazení svislice (0,6) + vodorovná část šipky (1,5) + buffer (0,5) = 2,6 mm, aby
  zmenšený text nepodlézal vodorovnou část šipky; ostatní buňky jen obecných 0,6 mm.
- **Rozsah:** buňky kreslené přes `CellRenderer` (StopList, DynamicTable/TableElement, LineTable,
  záhlaví DepartureGrid). Legenda poznámek (zalamuje) a minuty odjezdů (vlastní layout) beze změny.
- Bod 3 (#13048) ověřen: „čísla sloupků z trasy" = hotový sloupec „Nástupiště" (TR3); „z detailu
  spoje" (per-spoj W-trasa) splývá s bodem 4.

## 2026-07-27 - Šablony v DB: kompatibilita se staršími klienty (#13013 - „Šablona nebyla nalezena nebo je porušená")

Kolega s produkční verzí klienta (release 2026-07-14, bez podpory blobů po kusech) nemohl otevřít
systémovou šablonu z DB - nový build ji při seedu uložil po kusech a starý klient čte jen jednoklíčové
`data` → prázdno → chyba. Diagnóza vyloučila poškozená data (`SablonyDiag.IntegrityProbe`: žádná
utržená šablona, blob existoval - jen ve formátu, který starý klient neumí).

- **Fix (`Sablony.SaveTemplate`):** malý blob (vejde se do 1 kusu, tj. do ~20 000 znaků - drtivá
  většina šablon) se ukládá **jednoklíčově** = formát čitelný i staršími klienty. Po kusech jen
  vícekusé bloby (velké obrázkové šablony) - ty starší klient stejně nikdy neuložil (padaly ještě
  před chunking fixem). Kanonizace legacy→chunked z 2026-07-23 tím zrušena (bránila mixu verzí).
- **Testy:** `TestMalyBlobJednoklicovyProStarehoKlienta` + `TestVelkyBlobPoKusech` zamykají oba
  formáty; 39/39 zeleně.
- **Nový diag nástroj `Tests/SablonyDiag.IntegrityProbe`** (read-only): projde celé úložiště šablon
  a per šablona hlásí stav blobu (jednoklíčový / po kusech / CHYBÍ = utržená). Pro budoucí „nejde
  otevřít šablonu" případy.
- Systémová šablona v MLINE už je po re-seedu v kompatibilním formátu (ověřeno probe).
- **Pozn. k druhé výtce z tiketu** („okno chybového hlášení vypadá jinak"): jde o standardní
  EDControls `EDErrorPopup` (scrollovatelný dialog s plnou výjimkou, R2) - liší se od starého
  EDISON okna záměrně (nový stack). Případné sjednocení vzhledu / čistá hláška bez stacku pro
  doménové chyby = týmové rozhodnutí, ne bug.

## 2026-07-24 - Přestupy u zastávek (vlak/autobus) - nad rámec legacy (#13048 KOVED, bod 1)

Zákazník chtěl u zastávek zobrazit přestupy na vlak/autobus. Data je mají (znak „L"=vlak, „A"=autobus
v atributech zastávky, číselník 16), ale legacy StopList je záměrně netiskl (jen „x"=na znamení).

- **Server (`ZastavkoveJrAd`)** - `JePrestupovyAtribut` (V1: „A"/„L"); přestupy se **vždy posílají jako
  data zastávky** (ATRIBUTY), ale **do legendy nejdou** (jen symbol u zastávky). Ostatní znaky číselníku
  (formátovací P/T/N/V…) se dál netisknou.
- **Klient (`StopListElement`)** - nový přepínač **„Přestupy (vlak/bus)"** (`ShowTransferAttributes`,
  **default vyp = jako legacy**); render filtr rozlišuje na-znamení / přestup / ostatní atribut. Přepínač
  v panelu i EDInputform (`StopListElementEdit.cls`). Symbol se kreslí u názvu zastávky (piktogram).
- **Gold re-baseline** (0365001/0365005) - počty beze změny (`zast/odj/pozn` stejné, pozn potvrzuje že
  přestupy nejdou do legendy), změnil se jen md5 (přestupy přibyly do ATRIBUTY). Vědomé, ověřené.
- **Nový test `TestPrestupyEmit`** (0330510 ČB AN - autobus na terminálech) zamyká emisi. 37/37 zeleně.
- Přestupy sedí hlavně na **terminálech** (nádraží na koncích trasy) - tam je přestup logický.

## 2026-07-24 - Gold pojistka zón vůči legacy (AD zastávkový JŘ, #13048 KOVED)

Zákazník hlásil prázdný sloupec zón. Ověřeno, že náš `ZastavkoveJrAd.GetData` vrací zóny **1:1 shodně
se starým modulem** - a shoda zamčena testem, aby ji budoucí změny nerozbily.

- **Nová test třída `ALVA.TemplateDesigner.Tests.ZastavkoveJrAdLegacyTest`** - porovná řetězec zón per
  zastávka z našeho `GetData` proti **legacy producentovi** `ALVA.TISKMHD.Data.LoadData` (VolanoZ=JRMAIN,
  tentýž generátor, který krmil starý modul). 1:1 assert na CB_GWBUS 320113 (`101/104/109`) a KV_DP
  410101 (vč. vícezónové `11,12`). Namespace / legacy třída / prázdné zóny → skip (jiný server).
- Diagnostika mimo runner: `ZonyProbe` (IN DB/SKU/LIN/PAGE → OUT LEG/OUR/MATCH).
- **Pasti legacy volání zdokumentovány** (`ad-data-mapa.md §9`): `LoadData` čte IN jako řetězce
  „KLÍČ~HODNOTA" (ne pojmenované subscripty), OUT bez count node (iterovat `$order`), `$$$MakroGlobal`
  přepíná IN/OUT globál dle `sys("C","App")` (nutno `ALWA.SYS.MLRest`, jinak čte `^CacheTempMLSrv`).
- **Výsledek: zóny nejsou rozbité** - prázdný sloupec u KOVED = jejich data/šablona, ne regrese.
  Řezy 36/36 Caché testů zeleně.

## 2026-07-24 - Řezy písma (váhy) - výběr reálných řezů fontu (#13006 KOVED)

Zákazník chtěl používat víc řezů písma (TT Hoves Light/Medium/SemiBold/Heavy…), ne jen Tučně/Kurzíva.

- **Model:** `TextStyle.FontWeight` (100–900, nullable; null = auto dle Tučně, aditivní bez migrace);
  `ResolvedTextStyle.ToSKFontStyle` staví váhu (explicitní přebíjí Bold) → teče do renderu.
- **Sdílený `FontWeightCatalog`** - jediné místo „jaké řezy rodina reálně má" (z `SKFontManager.GetFontStyles`,
  jen vzpřímené) + názvosloví. **Nabízí jen skutečně nainstalované řezy** (ne fixní seznam) - chybějící font
  nemá, tak se nenabídne (dřívější fixní seznam mátl: nabízel váhy, co font nemá).
- **UI „Řez" dropdown** všude, kde se nastavuje font:
  - **EDInputForm** (Text, StyleFields zóny DepartureGrid/StopList/DynamicTable, NotesLegend) -
    klient plní z katalogu (`ElementFormBinder.PopulateFontWeightFields`) **při otevření**.
  - **Per-sloupec `CellStyleEditor`** (WPF) - tri-state `CellStyleVM.FontWeight` + **živě reaktivní**
    nabídka (změna rodiny → přepočet řezů). Layout řádku fontu změněn na WrapPanel (zalamování → nepřetéká).
- **Render:** řez respektují buňky tabulek (`CellRenderer`→`ToSKFontStyle`) + TextElement.
- **Odloženo do architektonického tracku** (viz roadmap): reaktivita EDInputForm dialogů (živý přepočet
  bez znovuotevření) + ruční render cesty schedule elementů (minuty DepartureGridu, text legendy) + čistý
  layout editoru sloupců.

## 2026-07-24 - Rohy elementu (zaoblení / zkosení) obecně na base (#12998 KOVED)

Zákazník chtěl seříznuté/zaoblené rohy barevných tvarů (odznaky linky). Uděláno **obecně na úrovni
každého elementu, jako rámeček** (architektura i UX).

- **Base `TemplateElement`:** `CornerStyle` {Ostré/Zaoblené/Zkosené} + `CornerSize` (mm). Serializace,
  `CopyBaseTo`, `ApplyScale`, descriptory („Rohy" + „Velikost rohu") jednotně.
- **Render centralizovaný:** rámeček (`RenderBorder`) i nově **pozadí** (`RenderBackgroundPass`,
  symetrické k border pass, volané canvasem před obsahem) kreslí tvar přes jediný helper
  `DrawElementShape` (Ostré=DrawRect, Zaoblené=DrawRoundRect, Zkosené=SKPath se seříznutými rohy).
  Z Barcode/Image/Rectangle odstraněna vlastní výplň pozadí → jedno místo.
- **UI:** „Rohy" + „Velikost rohu" ve skupině „Rámeček elementu" u všech boxových elementů
  (Rectangle, Text, Image, Barcode, StopList, DepartureGrid, NotesLegend, LineTable). Čára vynechána.
- **`RectangleElement`:** vlastní `CornerRadius` sjednoceno do base + **migrace 1.4.0 → 1.5.0**
  (`cornerRadius` → `cornerStyle=Rounded`/`cornerSize`); staré šablony 1:1. Rectangle si drží vlastní
  výplň/okraj (fill přes bg pass, okraj přes `DrawElementShape`).
- Testy: 370/370 (vč. 3× migrace). **Známý drobný nedostatek:** „Velikost rohu" je vidět i při Ostré
  (bez efektu) - EDInputForm nemá deklarativní `visibleWhen`; čisté skrytí je součástí tracku níže.
- **Fix (2026-07-24):** obsah elementu (výplně buněk DepartureGridu, pozadí Textu, obrázky…) přetékal
  přes zaoblený/zkosený roh. Řešeno **obecně na jednom místě**: render loop (náhled i plátno) volá
  `ApplyCornerClip` kolem obsahu každého elementu (no-op bez rohů) → ořez na tvar rohů plošně, obdoba
  rámečku. Base `GetContentClipRect` (element dodá skutečnou výšku; TextElement přepisuje).

## 2026-07-23 - Časová osa odjezdů jen v parametrech sestavy (#13009 KOVED)

Zákazník hlásil, že časová osa jde nastavit vždy jen na hodiny s odjezdy - změna nastavení nedělala nic.

- **Root cause:** časová osa šla nastavit na **dvou místech** - property elementu (dialog „Odjezdy")
  *i* parametr sestavy „Časová osa odjezdů" (`CASOVA_OSA`/`HODINY_OD`/`DO`). Preview ale do dat vždy
  vlévá **defaulty všech parametrů** (`LoadAgendaParameters`), takže `CASOVA_OSA="Jen hodiny s odjezdy"`
  je přítomen vždy a v renderu **přebíjí** property elementu → nastavení na elementu bylo mrtvé. Stejné
  v AD i MHD (ne AD-specific).
- **Fix:** časová osa je per-šablona parametr (sdílený, autoritativní) - odebrána z element dialogu
  (`DepartureGridElementEdit.cls`) i z descriptorů (Časová osa / Hodiny od / Hodiny do). Nastavuje se
  jen v parametrech sestavy (jako v MHD). Property `TimeAxisMode/RangeFrom/RangeTo` zůstávají jako render
  fallback (když param chybí), ale needitovatelné. Bez regrese - parametr element vždy přebíjel.

## 2026-07-23 - Legenda poznámek: nastavení se propisují (#13011 KOVED) + sjednocení termínu „Font"

Zákazník hlásil, že v legendě poznámek nejde nastavit barva textu, oddělovač ani font.

- **Barva textu buněk** - legenda kreslí text přímo přes `SchedulePaints.Text` (ne přes `CellRenderer`),
  který má barvu zapečenou natvrdo → `DataStyle.TextColor` se ignorovala. Nově se barva textu poznámek
  bere z „Barva textu buněk" (přebití paintu po `CreateStandardPaints`). Symboly bez vlastní barvy z dat
  ji přebírají taky; NZ boxy a barevné symboly z dat mají svoje barvy (nedotčené).
- **Font (nadpis i poznámky)** - přidán **editovatelný ComboBox** plněný z Caché standardní cestou
  (`$$$FieldTypeComboBox`, klíč `…FontFamily` → `ElementFormBinder.PopulateFontFields` = nainstalované
  fonty z `FontResolver`). Samostatně pro záhlaví (`HeaderStyle.FontFamily`) i data (`DataStyle.FontFamily`);
  render záhlaví nově bere font ze záhlaví (fallback na font poznámek → Arial). Doteď font pole úplně chybělo.
- **Oddělovač** - funguje dle návrhu jen pro **vlastní poznámky linky** (`Text` typ se značkou → „značka
  oddělovač popis"); u ikonových atributů (na znamení, bezbariér) se záměrně nepoužívá. Doplněn `helpText`
  a výchozí oddělovač změněn z em dash „-" na normální pomlčku „-" (default pro nové elementy).
- **Terminologie sjednocena na „Font"** napříč kódem (dřív „Rodina písma"/„Písmo") - TextElement,
  NotesLegend, jejich EDInputForm `.cls`. Konvence zapsána do `ux-conventions.md` §8 (glosář UI popisků +
  pravidlo „label je zdvojený .cs↔.cls, měň obě místa").

## 2026-07-23 - Šedé úsečky v PDF (#13004 KOVED): oddělovač sekcí jen v editačním plátně

Zákazník hlásil tenké šedé úsečky „v místě konce sekce" v návrhu, které se přenášely i do PDF/tisku,
i když neměl zapnutý žádný rámeček.

- **Příčina:** `PreviewCanvas.RenderPage` kreslila jemnou čáru (`#DDDDDD`) mezi sekcemi jako orientační
  pomůcku - jenže tuto metodu sdílí živý náhled **i** exporty (PDF, imposiční PDF, PNG). Pomůcka tak
  prosakovala do výstupu. Projevilo se to hlavně na hranici před **patičkou** (kotví se ke spodnímu
  okraji → čára „visela" uprostřed stránky nad bílou mezerou). U jediné sekce se nekreslila (jen mezi
  sekcemi), proto to nešlo reprodukovat na triviální šabloně.
- **Fix:** oddělovač z `PreviewCanvas` odstraněn úplně - **náhled teď zrcadlí výstup 1:1** (WYSIWYG).
  Hranice sekcí zůstávají jako vizuální pomůcka jen v **editačním plátně** (`DesignerCanvas` kreslí
  rámeček každé sekce sám), kde se edituje a dává smysl. PDF/PNG/tisk beze změny čisté.


Poslední dva body priority; celý track přesunut do roadmap-archive.

- **Rozsah platnosti ruční poznámky spoje** (`TiskJRMHDZastavkoveBodOd/Do`, helper
  `PoznamkaSpojeVRozsahu`): spec. znaky i textové poznámky se tisknou jen na stránkách zastávek
  v rozsahu - 1:1 legacy vč. jejich zvláštní „zpět" větve (brána přes BodDo, obrácené porovnání,
  `LinkyPrintFormatMHD:1047-1051`). Řeší „W - nejede do zastávky Globus" tištěné u nás všude;
  legenda 1073 = legacy 1:1 + jen auto-značky „jede jen…".
- **Parity test `TestParita1073KvDpSmoKolize`** (pre-push): zastávky 1:1 + poznámky pokrývají
  legacy (nový příznak `covered` v `MhdLegacyOracle.Summarize` = každý legacy klíč přítomen aspoň
  v legacy počtu) + odjezdy nadmnožina. Plná 1:1 na 1073 vědomě není cíl (SMO kolize, legacy
  ztrácí data). Ověřeno: test běží v package runu (výsledek 972) a prochází; „34/34" počítadlo
  runneru je multi-suite kuriozita, ne vynechaný test.
- Roadmap: priorita „MHD datová parita" KOMPLETNĚ → `roadmap-archive.md`; zbytky (varování
  v designeru, parametr „Rozdělení sloupců" pro MHD) → backlog.

## 2026-07-23 - MHD sloupce dle SMO konzistentně: prázdné sloupce + zrušen skrytý strop „Max skupin"

Nálezy z vizuálního smoke testu KV_DP 1073 (starý vs nový tisk):

- **SKUPINY_DNU = všechny SMO sloupce linky, i prázdné na dané stránce** (se záhlavím) - dřív se
  sloupec bez odjezdů na zastávce vynechal a zbytek se posunul (nekonzistentní layout napříč
  stránkami, neodpovídal SMO). Jako legacy. Gold 1001 re-baseline (jen md5, počty beze změny).
- **„Max skupin" default 3 → 0 (bez limitu)** - skrytý strop z éry před SMO seskupováním tiše
  ořezával 4. skupinu dnů v rozporu s checkboxy dialogu sloupců. Viditelnost řídí checkboxy;
  strop zůstává jen jako volitelná pojistka. **Migrace šablon 1.3.0 → 1.4.0**
  (`MigrationDepartureGridMaxGroups`: uložené `maxGroupCount: 3` = starý default → 0; vědomé
  hodnoty zůstávají) + testy, `template-schema-versions.md` doplněn o přehled verzí 1.1–1.4.
- **Dedup „stejné minuty" (fáze 4 SMO plánu) uzavřen jako bezpředmětný:** „00" + „00¹²" v 5:00
  jsou dva různé odjezdy (prázdninový vs. jen 31.12.) - sloučení by ztratilo informaci jako legacy;
  duplicity v rámci jedné skupiny tiskne i legacy. Čtení: bez značky = dle nadpisu sloupce,
  se značkou = dle legendy.
- Testy: .NET 367/367, Caché 34/34.
- **Nadpis sloučeného sloupce = dominantní typ + výčet ostatních na 2. řádku** („pracovní dny
  o prázdninách" / „24.12., 25.12. a 1.1., 31.12.") - čtenář vidí, co sloupec pokrývá, bez čtení
  legendy (návrh Jan); přepsatelné per šablona přes override nadpisu v dialogu sloupců. CB beze
  změny (sloupce bez sloučení nadpis nemění), gold 34/34 bez re-baseline.

## 2026-07-23 - MHD parita KV_DP dotažena: zastávky 1:1 + rozdílové poznámky A–K 1:1

Dokončení priority „MHD datová parita na složitých linkách". KV_DP 1073: **zast 693=693 ✓,
stránky 42=42 ✓, poznámky = legacy 1:1 (texty i písmena) + auto-značky (záměr)**; CB_DP 1001 vše 1:1.
Testy 34/34.

- **Příznak „netisknout na zastávkovém JŘ"** (`TisknoutZastavkovyTam/Zpet`, per bod trasy - KV_DP jím
  skrývá variantní větve): `PripravitTrasu` → `("trasa",SmerID,"netisknout",BodID)`; StopList i sběr
  atributů zastávek bod skryjí (kromě aktuální zastávky, 1:1 legacy `LinkyPrintFormatMHD:713-720`).
  Řeší zastávky 2× (1317→693) i duplicitní „na znamení" v legendě.
- **Alg. 1001 (texty rozdílů tras)** dostává `TisknutaTrasaBitmap` = obsluha bez skrytých bodů
  (1:1 legacy `:874`) → texty „zajíždí/nezajíždí/končí v…" 1:1.
- **Pořadí iterace odjezdů = legacy** (typ provozu vzestupně → hodina → minuta → spoj; dřív po
  sloupcích): na pořadí závisí přidělování písmen poznámek per stránka - teď A–K sedí s legacy
  vývěskou. Vedlejší efekt: jiné pořadí řádků `ODJEZDY` v JSON (element řadí sám) → re-baseline
  gold digestů 1001/1003/1006 (počty beze změny, jen md5).
- **Diagnostika:** `MhdLegacyOracle.StopProbe`/`NotesProbe` - výpis stop-listu/legendy jedné stránky
  legacy vs TD vedle sebe (trvalé nástroje pro budoucí parity práci).
- **SablonyTest opraven na chunk kontrakt** - od blob fixu (df22fe6) Save kanonizuje i jednoklíčová
  data na kusy a Load vrací `data1..N`; test četl staré `OUT("data")` → padal. Teď kusy skládá.
- Zbývá: gold pokrytí 1073 (roadmap bod 5) + drobná poznámka „W" (bod 6, rozsah BodOd/Do).

## 2026-07-23 - MHD: auto-značky sloučených skupin dnů (fáze 2 rozhodnutí „AD vzor")

Když v jednom SMO sloupci skončí víc typů provozu (KV_DP kolize), TD nově zajistí korektnost
sloupce sám - po vzoru AD varianty (auto-generovaná omezení), místo spoléhání na ruční datovou kázeň.

- **`PriraditZnackySmoSkupin`** (nová fáze 4b v `GetData`): dominantní typ (nejvíc spojů) dává nadpis
  sloupce (místo „poslední zapsaný vyhrává" → na 1073 už ne lživé „31.12."); každý ostatní typ dostane
  značku + legendu „jede jen <nadpis typu>". Značka: ruční `NegativniZnacka` typu z číselníku má
  přednost, jinak první volný znak z NZ řady (`RozsahNegativnichZnacekOd`, fallback 10 mimo denní 1–7).
  Značky jdou existujícím NZ kanálem (anotace „nz" + `nzPrirazen`) - žádný nový rendering.
- Podklad: `PromitnutSpoje` nově plní `spojTyp`/`smoTypy`; anotace se přidává v `SestavitJednuStranku`.
- **Ověřeno:** CB_DP 1001 dep/zast/pozn **1:1 match** (bez kolize = beze změny chování, gold zelený);
  KV_DP 1073: +115 řádků legendy „jede jen…" napříč 42 stránkami, nadpis sloupce 4 = dominantní typ.
- **⚠️ Vedlejší zjištění (experiment s čistým HEAD):** ranní „zastávky 693=693 vyřešeno" bylo měření
  proti serverové verzi třídy, která v gitu neexistuje - HEAD prokazatelně dává 1317 (2×) a poznámky
  A–K negeneruje. Bod znovu otevřen v roadmapě (ověřit u kolegy původ té verze, jinak fix napsat).
  Poučení zapsáno do paměti: před kompilací needitované `.cls` změřit baseline (server může být napřed).

## 2026-07-23 - MHD parita KV_DP 1073: diagnostika uzavřena - odjezdy +48 % = SMO kolize v datech

Analýza priority „MHD datová parita na složitých linkách" (bod 3 roadmapy). Nový oracle probe
`MhdLegacyOracle.SmoProbe` (stránky legacy vs TD + rozpad typů provozu linky na SMO sloupce).

- **Zjištění:** linka 1073 (KV_DP) má 7 typů provozu, z toho **4 mapované do SMO4** („pracovní dny
  o prázdninách" 77 spojů + „24.12." 37 + „25.12. a 1.1." 32 + „31.12." 64 = 210 spojů v jednom
  tiskovém sloupci). Stránky 42=42 → variantní stránky vyloučeny. Všech 12 vzorků dep-diffu má
  hlavičku „31.12.|" = kolizní sloupec.
- **Legacy kolizi neumí:** nadpis sloupce přepíše poslední typ (`LinkyPrintFormatMHD.cls:849`),
  minuty se per typ×hodina přepisují (`inxmm` nulován `:853`, zápis `:924`) → legacy **ztrácí
  odjezdy** (4295) a tiskne směs pod nadpisem „31.12.". TD sloučí bez ztráty (6352) → +48 %.
  **Bit-parity na 1073 není smysluplný cíl** (reprodukce ztrátového přepisu).
- **Rozdílové poznámky F–K (225 vs 213) mají jiný kořen:** legacy předává do alg 1001
  `TisknutaTrasaBitmap` respektující per-bod příznak `TisknoutZastavkovyTam/Zpet` („MH pro KV_DP",
  `:713-720`, `:874-875`); TD příznak nečte - zbývající kódový bod.
- **Výstup pro diskusi** (kolegové/DPKV): artifact „Skupiny dnů na vývěsce - analýza KV_DP 1073"
  s variantami řešení A (oprava dat u zákazníka, doporučeno) / B (warning v designeru) / C, D (zamítnuto).

Dokončení fixu z 2026-07-21 (chunking) - první pokus **neopravil pád**, protože měl špatně
identifikovaný limit. Ověřeno naostro na reálném obrázku (uložení + znovuotevření) + živý probe
proti dev serveru (chunkovaný save→load→delete round-trip).

- **Skutečný root cause (přepis mylné diagnózy „max string 3,64 MB"):** limit NENÍ délka Caché řetězce,
  ale délka **jednoho řádku při příjmu requestu**. MLRest server (`ALWA.SYS.Utility.StreamToGlobal`) čte
  vstup po záznamech přes `stream.ReadLine()` **bez délkového argumentu** → `%Stream` vrací nejvýš
  ~32 656 znaků na volání a řádek přes oddělovač **neslepuje** (ignoruje `eol`). Záznam delší než limit
  se rozpůlí, pokračování se rozparsuje jako „klíč" plný JSON/base64 znaků → neplatná indirekce
  `s @ref=val` → `<SYNTAX>` (proto ne `<MAXSTRING>`). Malé šablony (< ~32 KB) projdou v jednom čtení.
- **Vlastní fix:** velikost kusu `DefaultMaxChunkChars` **1 000 000 → 20 000 znaků** (bezpečně pod limitem
  řádku s rezervou). Kus o 1 MB byl 30× nad limitem → původní chunking nepomohl. Server `Sablony.cls`
  beze změny logiky (čte libovolný počet kusů) - jen zmenšení na klientovi.
- **Mechanika (z 07-21, platí dál):** blob jde **po kusech** (`dataChunkCount` + `data1..N`), server ukládá
  jako samostatné uzly `Data(n)`, **nikdy** neslepuje. Bez změny JSON schématu; legacy jednoklíčový `Data`
  se při loadu pozná (chybí `Data(1)`) a při dalším uložení kanonizuje. Zpětně kompatibilní obousměrně.
- **Testy:** `CacheBlobChunkingTests` (16×) - Split→Join round-trip vč. ~6 MB blobu, hranice/počty kusů,
  + regresní strážce velikosti kusu (`DefaultMaxChunkChars <= 30 000`, aby se limit znovu nevyšponoval).
- **Kapacita:** libovolný rozumný počet obrázků (reálně 2–5 na naše šablony); šablona roste lineárně, žádný
  zbylý single-string chokepoint na serveru → pro realistické velikosti nepadá (extrémní desítky MB by se
  jen zpomalily). Úložná strategie zůstává inline Base64 (self-contained JSON, ladí s Export/Import do souboru);
  přechod na binární úložiště přes `BinarySave`/GUID je vědomě odložený (dává smysl, až kdyby velké obrázky
  byly pravidlo).

## 2026-07-20 - MHD parita: sloupce odjezdů podle SMO (sekce minut odjezdu) - krok 1

MHD zastávkový JŘ na složitých linkách (mnoho typů provozu) datově nesedí legacy - gold pinuje jen
jednoduché CB_DP linky, takže to prošlo. Root cause #1: **dělení sloupců odjezdové mřížky.** Legacy
mapuje typ provozu → tiskový sloupec přes číselníkové pole `SMOID` (SMO1–4) a slučuje mnoho jemných
typů do ≤4 sloupců; TD dělal sloupec **per typ provozu** → na KV_DP desítky sloupců, rozsypané záhlaví.

- **Fix:** `SmoSloupec()` helper (typ → sloupec 1–4 dle `SMOID`, mimo SMO1–4 = netiskne se, jako legacy);
  `PromitnutSpoje` klíčuje odjezdy `smoCol` místo `TypProvozuID` + registruje `smoSloupce`/`smoNadpis`;
  `SestavitJednuStranku` staví `SKUPINY_DNU` i iteruje odjezdy po `smoCol`. Per-spoj logika (platnosti/NZ/
  rozdíly tras) beze změny (na `SpojID`). Uživateli se nic „SMO" neukazuje - jen čitelné nadpisy sloupců.
- **Ověřeno:** **CB_DP 1001 stále 1:1** (gold oracle: odjezdy 6597, zastávky 58, poznámky 1543 - vše match)
  → beze změny pinované gold, pre-push zelený. KV_DP sloupce vizuálně sedí s legacy.
- **Zbývá (viz roadmap „🔴 PRIORITA"):** #2 počty na složitých linkách (odjezdy 1,5×/zastávky 2× - které
  zastávky se v seznamu zobrazí, handling variantních tras) + #3 rozdílové poznámky A–K. Pravděpodobně
  společný kořen (variantní trasy). Krok 1 (sloupce) je hotový a bezpečný; #2/#3 jsou samostatné.

## 2026-07-20 - MHD tisk: odstraněno drahé nepoužívané pole „první/poslední spoj" (perf)

Přepínání instancí MHD trvalo ~5–7 s (velké linky), zatímco AD skoro okamžitě. Profilingem
(dočasné per-fáze `$zh` časovače) lokalizováno: **~91 % času `GetData` spolklo pole
`PRVNI_POSLEDNI_SPOJ`/`NEJEDOU_SPOJE`** - počítalo se **per stránka** (linka 1073 KV_DP: 6,8 s ze 7 s,
42 stránek) přes legacy MHD2 `CalculatePrvniAndPosledniSpojeText` (full sken spojů + `MakeTableSpoj`
za každý spoj + smyčka den-po-dni přes celé období platnosti).

- **Fix:** pole **odstraněno** (výpočet, oba scalary, pole ve schématu, mock, osiřelý helper `NejedouSpoje`).
  Nepoužívané (žádná šablona v repu ho nereferuje; per-linka flag `GenerovatNEJEDOU_SPOJE` ho jen zbytečně
  zapínal). MHD-only - AD `NEJEDOU_SPOJE` (= nástupiště, jiný levný význam) netknuto.
- **Výsledek:** `GetData` velké MHD linky z ~7 s na ~0,6 s (zbytek fází: trasa/spoje/bitmapy/stránky ~0,5 s).
- **Kdyby to někdo chtěl zpět:** reimplementovat „první/poslední spoj" v TD nad daty fáze 4 v `^||TiskMHD`
  (levné, bez legacy MHD2 volání per stránka) - poznámka v kódu na místě.
- ⚠️ **Gold:** odebrání scalaru mění MHD gold digesty (md5; počty pages/zast/odj/pozn beze změny) → re-baseline.

## 2026-07-20 - StopList: značky přes symbolový font (MHD „na znamení" konzistentní s legendou)

Značky u zastávek (na znamení, atributy, poznámky) se ve StopListu kreslily **normálním fontem**, takže
MHD „na znamení" (drátový kód SpecZnak 68) se ukázalo jako **surové písmeno „D"** místo piktogramu -
zatímco legenda a mřížka odjezdů (které symbolový Timetable font už používaly) ukázaly správný piktogram.
Nekonzistence hlášená z DPKV/MHD (U Jara „D", Panorama „D"; legenda „× na znamení").

- **Fix:** značky se kreslí **po úsecích s vlastním fontem** (`BuildStopMarkerRuns` + `DrawStopMarkers`):
  jednoznakový SpecZnak, který není textové „x/y/z", jde symbolovým Timetable fontem (piktogram);
  textové značky (AD „x") a poznámky normálním fontem. V jedné buňce nejde míchat font → značky
  oddělené od názvu a kreslené zvlášť, přes sdílené `CellLayout` helpery (zarovnání beze změny).
  Platí pro obě pozice značek (před/za názvem) i auto-šířku sub-buňky.
- **Agenda-agnostické:** žádná AD/MHD větev - rozdíl je jen v datech (SpecZnak číselníku firmy).
  AD „x" zůstává „x", MHD „D" → piktogram; StopList teď jede stejnou symbolovou cestou jako
  NotesLegend/DepartureGrid (uzavírá mezeru symbolového fontu C2 pro StopList). Relates #10660 (DPKV).

## 2026-07-15 - Tiskové parametry per šablona (obsahové)

Hodnoty **obsahových** tiskových parametrů (časová osa odjezdů, rozdělení sloupců, tisk šipek /
projetých zastávek, souhrny…) teď **patří šabloně** - sdílené mezi uživateli, priorita nad
per-agenda „naposledy použité", ukládají se **automaticky** se šablonou (změna → dirty → Ctrl+S).

- **Model:** aditivní `Template.PrintParameters` (`Dictionary<string,string>`, JSON `printParameters`).
  Staré šablony ho nemají → fallback na agendu (bez migrace).
- **Uložení:** `SyncParametersToTemplate` zapíše hodnoty obsahových parametrů (iteruje
  `_agendaParameters` → **export pole** název souboru/členění/imposice se záměrně nezachytí,
  zůstávají per agenda; převod exportu na per-šablonu odložen).
- **Načtení (priorita):** `Template.PrintParameters` > per-agenda > tvrdé defaulty.
- **Dirty:** `OnAgendaParamChanged` nastaví dirty jen při user změně (načtení jde přes
  `OnExternalParamsChanged`); `SetAgendaParamValue` je change-aware (žádný falešný dirty/re-fetch
  při reloadu EDInputform tabu).
- **Opravy z ladění:** cold-start časování - `RefreshFieldsList` nově vrací `Task`, otevírací flow
  ho awaituje (dřív async schéma přepsalo hodnoty defaulty); parametrový tab se po načtení hodnot
  **bezpodmínečně** přenačte z finálního stavu (`RefreshParamsUiAfterLoad`) - dřív se držel defaultů.

## 2026-07-15 - „Na znamení" řídí značku i legendu (#7161) + dev přepínač firem

- **StopList: „Na znamení" schová značku i legendu (#7161 D5).** Přepínač „Na znamení"
  (`ShowSignalStopIndicator`) teď řídí značku „na znamení" u zastávky **i** řádek legendy -
  vypnutím zmizí obojí (dřív mizela jen legenda, značka zůstávala). Příčina: ostrá AD i MHD data
  posílají „na znamení" jako **atribut** (ne přes pole `NaZnameni`), takže značku dřív řídil
  generický přepínač „Atributy". Nově se atribut „na znamení" rozpozná podle popisu
  (CZ „na znamení" / SK „na znamenie" - symbol se mezi agendami liší: AD „x", MHD „D") a řídí ho
  „Na znamení"; „Atributy" řídí jen ostatní atributy. **Značka** (TD render) funguje v AD i MHD.
  **Skrytí řádku legendy** vyžadovalo v MHD tag kategorie - `ZastavkoveJrMhd.BuildPoznamka` nově
  emituje `KATEGORIE` a řádek „na znamení" (dle popisu) je `atribut-zastavky`, takže sdílený
  `NoteVisibilityFilter` ho schová stejně jako v AD. Default beze změny (žádná regrese); MHD gold
  digesty re-baselinovány (jen md5, počty pages/zast/odj/pozn shodné → additivní tag, LegacyParity zelená).
- **Dev přepínač firem (jen pro vývojáře).** Nástroje → „Přepnout firmu (DEV)" - pevný seznam
  (CB_CSADAD, CB_GWBUS, KV_DP), přepne `Idfa` + datový namespace za běhu a znovu načte schéma/data
  (bez editace appsettings a restartu). Gate = lokální dev klíč (jako AI asistent), v produkci skryté;
  bez licenčního verify. Slouží k testování agend proti více firmám.

## 2026-07-15 - UX úklid (empty-state, hlášky DB, EDList výběr, Save dialog)

- **Empty-state panelu „Pole agendy"** - u agendy bez databázových polí (Obecná / bez připojení)
  se místo prázdna zobrazí hláška „Tato agenda nemá databázová pole…" (`ShowFieldsEmptyState`).
- **Přesnější hláška DB v dolní liště** - místo zavádějícího „○ Databáze: nedostupná" (evokovalo
  výpadek Caché) nově „○ Databáze: pro tuto agendu nejsou databázová pole" (týká se hlavně Obecné agendy).
- **EDList výběr (sdílené, EDControls)** - dvojklik na řádek = přepnout zaškrtnutí; Shift+klik =
  výběr rozsahu od kotvy. Platí pro všechny EDList seznamy (dřív bylo jen ad-hoc v jednom dialogu).
- **Uložení do databáze - nedostupné tlačítko místo popupu** - dialog „Uložit do databáze" má
  tlačítko „Uložit" nedostupné, dokud není vyplněné jméno (přes `EDInputForm.FieldChanged`); popup
  ze `Save()` zůstává jako záloha pro ostatní validace.
- **Sjednocení pořadových šipek** - `TableEditorDialog` (pořadí sloupců/řádků) měl tenké ↑↓,
  zbytek appky trojúhelníky ▲▼ → sjednoceno na ▲▼. (Šipky odsazení textu v CellStyleEditoru =
  směr, ne pořadí - ponechány ↑↓.)
- Roadmap: „jednotný resize grip" smazán (legacy, nejasné). **Rozhodnutí: toolbar písmenka
  („M"/„O"/„Fit"/„PDF"/„PNG"…) záměrně ponechána** - text je u PDF/PNG jasnější než ikona a drží
  styl; bod „toolbar text→ikony" z roadmapy odebrán.

## 2026-07-15 - Zákaznická wiki: 3 články (post-verze)

Dokumentace Návrháře na podpora.m-line.cz rozdělena na 3 prolinkované články (hub = příručka):

- **569** Uživatelská příručka (hub) - `90511fc5-10e5-476d-b7c1-3cd70672f26d`, zdroj `uzivatelska-prirucka.md`
- **570** Technické požadavky (IT) - `3a07cae1-1198-4ffa-8875-856e3fc36094`, zdroj `prirucka-it.md` (nový)
- **571** Novinky a plán - `6d48ca01-ce81-45d8-a396-76fcf0f982db`, zdroj `plan-a-zmeny.md`

Vše `internal` (zveřejnění + link do Edison widgetu „Novinka týdne" řeší uživatel). Zdroj pravdy =
`.md` v repu, wiki = zrcadlo (na wiki needitovat ručně). Název sjednocen **„Návrhář sestav"**
(dřív seed „Návrhář šablon" - kolidovalo s menu/e-mailem/názvy článků). Starý článek #6
(„uživatelský manuál", legacy nástroj) zatím ponechán.

**Release track uzavřen (2026-07-15):** verze v produkci (2026-07-14), dokumentace na wiki hotová.
Zákaznický e-mail se **dělat nebude** (rozhodnutí). `pre-version-checklist.md` **retirován/smazán**
(účel splněn - jeho instrukce v hlavičce to předpokládala); eval sada AI pomocníka z něj přesunuta
do `roadmap.md` (sekce AI). Nálezy byly průběžně přesouvány do roadmapy.

> ⚠️ **Learning - wiki markdown konvertor (TipTap) NEUMÍ tabulky**: GFM tabulka se slije do
> jednoho run-on odstavce. Do wiki článků psát tabulky jako **odrážky / definiční seznamy**,
> ne `| … |`. Nadpisy, seznamy (ordered/bullet), odkazy, tučné/kurzíva, blockquote, `---` fungují.

## 2026-07-15 - Snap na čtverec při resize (post-verze úklid)

- **Snap na čtverec při resize obdélníku** (poslední otevřený UX bod z pre-version §5) - u rohového
  úchytu tvarového elementu (Rectangle/Image/Barcode) se W≈H dorovná na přesný čtverec, když se
  strany přiblíží (práh 6 px přepočtený na mm dle zoomu). Protilehlý roh (kotva) zůstává, dorovnaná
  strana snapuje na grid, clamp na hranice sekce. Ctrl snap dočasně vypíná. Měřící badge při snapu
  vypíše „◻ čtverec". Uživatel nemusí rovné strany dopočítávat ručně. `MainWindow.xaml.cs` resize
  větev. Tím jsou nálezy pre-version checklistu §5 uzavřené (zbytek ověřen jako už hotový).

## 2026-07-13 - Vizuální audit UI + rychlé výhry (pre-produkce)

Audit modernosti vzhledu: základ zdravý (EDControlStyles flat theme, moderní paleta EDTheme -
Fluent/VS Code styl). Opraveny nálezy, kde se theme nedodržoval:

- **Barvy sjednoceny na EDTheme tokeny** - 44 hardcoded barev ve 14 souborech (v UI koexistovaly
  3 šedé, 2 modré, 2 červené): `#888`→`Text.Muted`, `#CCCCCC`→`Border.Medium`,
  `#C62828`/`OrangeRed`/`Firebrick`/`DarkRed`/`#D32F2F`→`Semantic.Danger`, `#2196F3`→`Accent.Blue`,
  `#F8F8F8`/`#DDDDDD`→`Bg.Footer`/`Border.Light`. Zároveň prerekvizita dark mode (EDThemeDark existuje).
- **TableEditorDialog: opraven skrytý bug** - lokální implicitní Button styl bez `BasedOn` přebíjel
  flat theme → tlačítka v dialogu měla výchozí šedý WPF 3D vzhled. Jednořádkový `BasedOn`.
- **Záložky levého panelu s ikonami** - 5 nesourodých Unicode dingbatů (▣⊞✎≣✦) nahrazeno
  EDIcons ikonami (Panels/Grid/Edit/Module/Help) + text.
- **Emoji pryč** - 💡 → „Tip:", ⚠ v červeném overflow bloku → jen text (blok sám je signál).
- **Status bar chipy zaobleny** - agenda/instance/overflow `CornerRadius="3"` + svislý margin
  (dřív ostré hrany na výšku celého baru).
- **Vědomě po verzi** (→ roadmapa): toolbar textová tlačítka („M"/„O"/„Fit"/„PDF") → ikony ·
  sjednocení ▲▼/↑↓ tlačítek · footer pattern + spacing tokeny v dialozích · dark mode zapojení ·
  dialog header pattern.

## 2026-07-13 - UX audit kritických flow + opravy (pre-produkce)

Dva fan-out audity (kritická flow prvouživatele + konzistence dialogů). Základ zdravý
(potvrzení destruktivních akcí, async vzory, jednotné Storno/Esc, 0× MessageBox). Opraveno:

- **Recent files** - jediná cesta otevírající šablonu bez potvrzení neuložených změn (tichá
  ztráta práce) → `ConfirmUnsavedChangesAsync` jako Ctrl+O/import.
- **Startovní agenda kontextově** - priorita: EDISON kontrakt (`Agenda=<kód>`) → **poslední
  použitá agenda uživatele** (`MainStatePref.LastAgendaCode`, capture při save stavu) → AD
  fallback. Odstraněn zafixovaný dev „NECOMMITOVAT" default.
- **PrintDialog „Tisk všech" už nemrazí** - fetch (HTTP per linka) + render na threadpoolu
  (`AsyncRelayCommand` s vestavěným guardem), snapshot parametrů (sdílený dict se dá měnit
  během fetche), render nad `DeepClone` šablony (nezávodí s preview renderem UI threadu),
  overflow check na UI (levný, sdílí element cache); dvoufázový progress ve StatusText.
- **AI klíč z menu s indikátorem** - wait-cursor + „Ověřuji dostupnost AI služby…" + guard
  proti opakovanému kliku (cold start mlweb resolve = sekundy, timeout až 60 s ticha).
- **„Uložit jako" kontroluje kolizi názvu** - stejné jméno v DB → potvrzení (dřív tichá
  nerozlišitelná duplicita); best-effort, výpadek listu neblokuje uložení.
- **PrintDialog**: `IsDefault` na Zavřít (Enter dřív nedělal nic), `ShowInTaskbar=False`.
- **Pomocník**: Enter ve vstupním poli odešle dotaz (`IsDefault` v UserControlu mimo dialog
  scope nefunguje → explicitní KeyDown).
- Dříve týž den (UI kosmetika): titulek „Návrhář sestav", vykání v Pomocníkovi, „z databáze"
  místo „z Caché", menu „soubor" místo „JSON soubor".
- **Vědomě odloženo** (→ roadmapa): VPN-down hláška „není nakonfigurována", empty-state Pole
  agendy, splash při startu, validace Save dialogů disabled tlačítkem, theme tokeny v editorech.

## 2026-07-13 - Nápověda F1: datově řízená + fulltext hledání (Ctrl+F)

- Obsah přesunut ze 500 řádků statického XAML do `HelpDialogViewModel.BuildContent()`
  (přidání zkratky = 1 řádek); XAML jen šablony (kategorie → řádky akce/zkratka → tip).
- **Hledání**: pole v hlavičce (Ctrl+F fokus, Esc smaže filtr), filtruje kategorie, akce,
  zkratky i tipy, **bez diakritiky** („razeni" najde „řazení"); „Nic nenalezeno" stav.
- Obsah srovnán s autoritativní tabulkou zkratek (ux-conventions) + doplněny nové sekce:
  Tisk a export (členění, imposice, ořezové značky, varování přetečení), Data/agenda/instance
  (Agenda×Varianta, štítky, F5), Výrazy a datová pole (formáty, aritmetika, podmínky, agregace,
  {PAGE}); opraveny šipky (1 mm / Shift = mřížka / Ctrl = 0,5 mm), doplněny Ctrl+1/Ctrl+B/
  copy-paste stylu; pryč uspaná Fixed tabulka. Texty bez anglicismů (chip→štítek,
  canvas→plocha, toolbar→lišta nástrojů) - zákaznický jazyk.

## 2026-07-13 - PDF import: Sonnet 5, robustnost proti halucinacím, standardní dialog

Průchod PDF importu před produkcí (živě testováno na KV_DP A5 vývěsce - vizuálně
i sémanticky nadprůměrný výsledek). Build 0 chyb, .NET 341/341.

**Model + prompty:**
- Vision import na `claude-sonnet-5` (adaptivní myšlení - percepční úloha; `max_tokens`
  16000, myšlení se počítá do limitu). `ModelSupportsTemperature` guard - Sonnet 5+
  temperature nedostane (jinak 400), starší modely dál 0.2.
- Sdílený `Services/AiPromptHelpers.AppendDataSchemaSection` - enricher i vision měly
  zkopírovaný identický blok formátování DataSchema (drift risk).
- Vision umí sloupec „Platform" (nástupiště, TR3) → `StopFields.Nastupiste`.

**Robustnost proti stochastice modelu (každý symptom z živého testu měl v kódu díru):**
- *Přetečení*: model vrátil rel. bottom 1.015 → element vyšší než stránka. Clamp AI
  souřadnic do 0.0–1.0 + pojistka v pipeline (pevné rozměry se clampnou do tiskové
  plochy; JŘ elementy obsah vejdou auto-scalem). NotesLegend nově s pevným boxem
  z AI bboxu (dynamická výška přetékala sekci s reálnými daty).
- *Falešný QR*: BarcodeElement vznikne jen s důkazem v PdfPig geometrii
  (`LooksLikeQrRegion` - shluk ≥15 malých čtverečků) + prompt „qrCode nevymýšlej".
- *Překryv obrázků*: dedup PdfPig obrázků (krok 5 vs 6 přidával týž obrázek 2×),
  placeholder se nepokládá přes existující element, krok 7 počítá obrázky do pokrytí.
- *Chybějící StopList*: retry - když v odpovědi chybí seznam zastávek/mřížka odjezdů,
  jeden opakovaný pokus; pokud ani pak, viditelné varování „doplňte ručně z palety"
  (dřív tichá díra ve výsledku).

**Standardní dialog (odstraněny volby, které uživateli nepatří):**
- Zůstává: PDF soubor + stránky. Vše ostatní odvozeno: **typ importu z otevřené agendy**
  (zastávková → AI JŘ import, jiná → obecný layout), AI vždy zapnuté, agenda = aktuální,
  sloučení grafiky pevně „pouze čáry". Bez dostupné AI je JŘ import zablokovaný
  (červená hláška s důvodem z mlweb - limit / chybí konfigurace).
- **JŘ import z vícestránkového PDF = právě jedna stránka** (výběr „Stránka: N z M");
  extrahuje/rasterizuje se jen ta jedna (dřív mlčky první + extrakce všech).
- Debug checkbox + debug soubory vedle PDF jen pro vývojáře (lokální dev klíč),
  bez popupu po importu. Mrtvá větev „AI obohacení textem" (haiku enricher) odstraněna
  z VM (vision ji nahradil; služba zůstává pro případný fallback).
- Generátor šablon: pole pro ruční API klíč jen pro vývojáře; hlášky nedostupnosti AI
  jednotně nesou důvod z mlweb (pomocník / generátor / import). Menu „AI Asistent
  šablon" před produkcí skryté (vidí jen vývojář); Import PDF zůstává pro všechny.

## 2026-07-13 - EDISONAI/mlweb napojení: spravovaný API klíč + hlášení spotřeby

Zákazníci nebudou mít vlastní Anthropic klíč - M-line jim ho spravuje přes mlweb
(podpora.m-line.cz), stejně jako u EDISONAI chatu. Build 0 chyb, .NET 341/341.

- **`Services/EdisonAiService.cs`** - replikace HTTP kontraktu EDISONAI (ne DLL reference -
  WinForms/.NET 4.8 vs WPF/.NET 10): (1) mlweb config z Caché `ALVA.EDISONAI.API.Config
  .GetMLWebConfig` (X-API-Key, baseUrl, isDev, devApiKey; jednou per session), (2) resolve
  `POST /api/mline/agent/resolve {idfa, loginName}` → allowed + apiKey, **fail-closed**
  (bez spojení/povolení/limitu AI nefunguje, hláška s reason), cache 60 s; jen provider
  `claude`, (3) spotřeba `POST /api/mline/usage {..., source: "templatedesigner"}` -
  best-effort na pozadí, odpověď `allowed` = gate pro další volání.
- **Pořadí klíče:** lokální `AiAssistant.AnthropicApiKey` = vývojářský bypass (mlweb se nevolá,
  spotřeba se nehlásí - dosavadní chování beze změny) → DEV server devApiKey → mlweb resolve.
- **Model zůstává v režii TD** (AssistantModel/VisionModel) - resolvovaný `model` z mlweb je
  konfigurace EDISONAI chatu; limity jsou tokenové, volba modelu je neobchází.
- **`AnthropicApiClient`** čte `usage` z každé odpovědi (text i vision) → `ReportUsage`.
- Call sites: pomocník fail-closed s hláškou; generátor + PDF import přes `TryGetAiApiKeyAsync`
  (AI tam je volitelný krok - bez klíče dál fungují bez AI).
- ⚠️ **Koordinace s autorem mlweb:** usage se hlásí se `source: "templatedesigner"` - ověřit,
  že endpoint neznámý source přijme (jinak se TD spotřeba nezapočítá do limitu); limit je
  sdílený s EDISONAI chatem per IdFa (záměr).

## 2026-07-13 - AI vrstva: Sonnet 5 + audit promptů (pomocník i generátor)

Před nasazením k zákazníkům (checklist „projít pomocníka"). Build 0 chyb, .NET 341/341.

- **Model:** pomocník (průvodce) i generátor šablon na `claude-sonnet-5`, konfigurovatelné přes
  `AiAssistant.AssistantModel` v appsettings. Vision import zůstává na `VisionModel`.
- **Sonnet 5 API specifika ošetřená v `AnthropicApiClient`:** temperature se neposílá (nedefaultní
  hodnoty S5 odmítá 400); pomocník volá s **explicitně vypnutým myšlením** (`thinking: disabled` -
  jinak je na S5 adaptivně zapnuté → latence), generátor s adaptivním (kvalita JSON); parsování
  odpovědi přes `ExtractText` = první `text` blok (thinking bloky můžou předcházet); generátor
  `max_tokens` 16000 (myšlení se počítá do limitu).
- **Generátor - opravy SystemPromptu proti reálnému modelu** (serializer tiše zahazuje neznámé
  properties → ploché fontSize/bold se dřív ZTRATILY): TextElement vnořený `style` objekt;
  Rectangle `backgroundColor` (ne `fillColor`); `TableElement` nahrazen `DynamicTableElement`
  (dataSource povinné, `headerStyle`/`dataStyle`, minRows/maxRows/expression/rowVisibility);
  plný výčet BarcodeType (+Ean13/Ean8/Pdf417/DataMatrix); přepsaný finální příklad.
- **Pomocník - znalostní vrstva v `CapabilityCatalog`:** trojstavové pravidlo nejistoty (v podkladech
  → přesná cesta / vím že chybí → „zatím neumíme" / nejistý → odkázat na F1/podporu) + grounding
  (každý krok cituje cestu v UI); `GeneralIntro` přepsán - **UI mapa Co→Kde** (tisk/imposice/členění,
  data/instance, vzhled, šablony v DB, navigace), Agenda×Varianta, výrazový jazyk, kompletní
  zkratky dle autoritativní tabulky (odstraněn neexistující Ctrl+Shift+S), sekce „co ZATÍM neumí";
  `ElementNote` pro DynamicTable (výrazy, zvýraznění, min/max, agregace) + LineTable, mrtvá Table
  větev pryč.
- **Eval sada** ~21 otázek (14 lookup + 6 chytáků na neexistující featury + 1 nejistota) v
  `pre-version-checklist.md` pro ruční otestování.

## 2026-07-13 - UX fixy z pre-version průchodu (blok A checklistu)

Nálezy z ručního testování před úterní verzí. Build 0 chyb, .NET 340/340.

- **Zoom ke kurzoru** - Ctrl+kolečko drží bod pod myší na místě (přepočet scroll offsetů po
  layout passu); dřív zoom „utíkal" doleva a uživatel se musel doscrollovat.
- **Snap rozměrů při vzniku elementu** - se zapnutým snapem se na krok mřížky zaokrouhlí
  i šířka/výška (výchozí velikosti 80×8, 190×25… nebyly násobky kroku → spodní hrana nikdy
  neseděla na mřížce). Element menší než krok (čára) se nezvětšuje.
- **Nové defaulty designeru:** SnapToGrid ZAP + krok mřížky **1 mm** (dřív VYP + 5 mm - hrubý krok
  byl hlavní zdroj pocitu „nepřesného" snapu). Per-user MainState / per-template EditorPreferences
  mají přednost - stávající uživatelé svůj stav neztratí.
- **Imposice viditelná ve statusu** - každý export s aktivní imposicí vypíše „… · imposice 2×1
  na list 210×297 mm" (per-agenda persistovaná volba jinak překvapila „proč mám půlku stránek").
- **Paleta/menu:** Kód (QR…) před Tabulkou. **Šipka směru:** labely voleb vysvětlují kreslení
  u okraje elementu (u širokého elementu je vpravo „daleko" - vypadalo jako nefunkční).
- Do backlogu (roadmap): snap/indikace čtverce při resize · per-šablona hodnoty tiskových parametrů.
- **`Released` flag agend (blok B průchodu):** výběr agendy nabízí jen zveřejněné - Obecná +
  Zastávkový JŘ (AD/MHD); Linkový JŘ (pilot v1) a Faktury (vzorová) skryté. Data-driven:
  `BaseAgenda.Released` parametr (default 0 - nová agenda „neuteče" omylem), registr wire aditivně
  (`released` bool, starší server → default true). Aktuální agenda šablony vidět vždy;
  existující šablony + EDISON launch nezveřejněných fungují dál.

## 2026-07-12 - UI doladění preview: sbalitelná lišta parametrů + skok na stránku dat + export volby dle agendy

Před-produkční UX úklid (analýza + implementace na žádost uživatele). Ověřeno: build 0 chyb,
.NET 344/344 (+4), Caché wire živě (GetParameters `group`, AgendaParams skupiny + split volby).

- **Sbalitelná lišta parametrů nad preview** - dřív WrapPanel (pole se zalamovala „náhodně" dle šířky
  panelu a tlačila preview dolů). Nově default sbaleno na 1 řádek „⚙ Parametry ▾" + **souhrn hodnot
  odchylných od defaultu** (uživatel vidí, že má něco přepnuté); klik kamkoli do hlavičky rozbalí
  svislý formulář. Stav persistován v `MainStatePref.ParamsBarExpanded`.
- **Seskupení parametrů `group`** (aditivní klíč kontraktu, R3) - `GetParameters` posílá skupinu;
  **konvence: skupina = UI název elementu, který parametr ovlivňuje** („Seznam zastávek" /
  „Mřížka odjezdů" / „Poznámky a legenda"), celo-šablonové parametry → „Obecné" (feedback
  uživatele - dřív tematické „Obsah"/„Vzhled"). `AddBoolParam`/`AddNumberParam` volitelný 5. arg,
  enumy `p.group`. Lišta groupuje přes CollectionViewSource (hlavičky skupin, prázdná skupina bez
  hlavičky), **PrintDialog** přes EDInputForm skupiny (`AgendaParams.AddParamGroups`, pořadí skupin =
  první výskyt v GetParameters - proto SOUHRN_NA_ZNAMENI přesunut k poznámkovým parametrům).
- **Skok na stránku dat v preview** - text „3 / 40" nahrazen ComboBoxem s popisky stránek
  „N - zastávka → směr" (`BuildPreviewPageLabel` z POPIS_ZASTAVKA/POPIS_SMER, fallback „Stránka N")
  + „/ celkem"; ◀▶ zůstávají. Konec proklikávání 37 stránek na konkrétní zastávku; combobox zároveň
  ukazuje, CO aktuální stránka zobrazuje.
- **Export volby dle capability agendy** (audit C-f) - `AgendaParams.AddExportFields` čte class
  parametry agendy: `PER_STOP` („Jeden PDF na zastávku/směr") jen pro `ElementKind=STOP`, `PER_LINE`
  label z `InstanceLabel` v akuzativu („Jeden PDF na linku" / „…na fakturu"; helper `Akuzativ`).
  Uložená hodnota, kterou agenda nenabízí → fallback SINGLE.
- **Oprava latentního bugu `TISK_SIPKY` default** - call-sites předávají string `"true"`, který COS
  v `$s(defaultVal:...)` vyhodnotí numericky jako 0 → wire `defaultValue:"false"` a šipky směru
  defaultně VYPNUTÉ proti záměru (element default `ShowDirectionArrow=true`, docs „true").
  `AddBoolParam` nyní bere `1` i `"true"`. ⚠️ Behavior change pro uživatele bez uložených PrintParams:
  šipky nově defaultně zapnuté (= původní záměr).
- **Instance chip** - šipky ◀▶ skryté při 0–1 aktivní instanci (dřív jen disabled, matoucí).
- **Stabilní pozice ◀▶ při proklikávání** (feedback z testu) - různě dlouhé názvy stránek/linek
  posouvaly tlačítka pod myší (navigace je centrovaná → šířka obsahu hýbe layoutem). Combobox
  stránek pevná šířka 230px (dropdown se obsahu přizpůsobí), text instance chipu pevná šířka
  260px + ellipsis + tooltip **jen při více instancích** (DataTrigger - u 0–1 instance chip
  zůstává kompaktní).
- **Fix prázdného comboboxu stránek po výměně dat** (feedback z testu „stránkování zmizelo") -
  výměna ItemsSource synchronně pushne SelectedIndex=-1 do VM a zpětná korekce se během
  reentrantního binding update zahodí → combobox zůstal prázdný. `NotifyPreviewPagesChanged`
  notifikuje v pořadí PreviewPageItems → RealPreviewIndex (index srovná po rebindingu).
  + Lišta stránkování nově viditelná i pro **jedinou** stránku dat (`HasPageNavigation` -
  combobox říká, CO preview zobrazuje; skrytá jen v mock módu).

## 2026-07-11 - Dvouosý model agend: Agenda × Varianta (názvosloví dle legacy analýzy)

Roadmap „typ šablony vs agenda" (podnět kolegy). Analýza legacy (RG strom `agenda→oblasti→sestavy`
z číselníku `ALVA.EDISON.Oblast`; TiskMHD/TiskJR = typ jako aplikace + skrytý `VolanoZ` JRMAIN×MHD,
šablony sdílené oběma zdroji) → rozhodnutí: **Agenda** = nejvyšší úroveň („Zastávkový JŘ", „Linkový JŘ",
„Faktury"; JŘ povýšené jako důležité, budoucí RG agendy přibudou vedle), **Varianta** = datový zdroj
(AD/MHD, ekvivalent legacy `VolanoZ`; předvolená launch kontextem). Duální role MHD (agenda provozních
tisků × varianta JŘ) odpovídá legacy. Ověřeno: build 0 chyb, .NET 340/340 (+4), Caché wire živě.

- **Caché:** `BaseAgenda` parametry - kombinovaný klíč `AgendaVariantCode` (dřív `AgendaCode`,
  hodnoty beze změny) + nové osy `Agenda`/`Variant` + `VariantLabel`; `AgendaLabel` nově jen label
  agendy („Zastávkový JŘ"). 5 agend + `AgendyRegistr` (wire aditivně: nové klíče `agenda`/`agendaLabel`/
  `variant`/`variantLabel`; staré `code`+`label` (kombinovaný formát „Zastávkový JŘ (AD)") zůstávají
  pro nasazené klienty).
- **.NET:** `AgendaRegistryEntry` + `Agenda`/`AgendaLabel`/`Variant`/`VariantLabel` (defaulty =
  fallback pro starší server), computed `DisplayName` („Zastávkový JŘ · AD") + `EffectiveAgendaLabel`.
- **UI:** ChangeAgendaDialog seskupený podle agendy (GroupStyle; řádky = varianty), staví z registru
  místo AgendaInfo; chip ve status baru + titulek okna `[Zastávkový JŘ · AD]` místo syrového kódu;
  filtr v přehledu šablon dvouosé popisky řazené po agendách.
- **Beze změny (vědomě):** hodnoty kódů `ZASTAVKOVY_JR_AD`… (uložené šablony, DB index ByAgenda,
  per-agenda preference, EDISON launch `Agenda=<kód>`), JSON pole `agendaCode`,
  `Template.AgendaCode` v C# (kombinovaný klíč - název sedí s wire; ambiguita byla jen v Caché
  třídě vedle parametru `Agenda`, tam vyřešeno). Slovo „agenda" v UI textech zůstává - nově znamená
  první osu, což je v souladu s firemním/legacy významem.
- **Doladění po uživatelském testu (týž den):** chip explicitně „Agenda: Zastávkový JŘ ·
  Varianta: AD" (učí názvosloví; bez varianty jen „Agenda: Faktury"); Ctrl+O sloupec „Agenda"
  (syrový kód) rozdělen na dva - „Agenda" + „Varianta" s labely resolvovanými server-side
  (`EDList.Sablony.ResolveAgendaVariant`, per-request cache, GENERAL → „Obecná").
- **Follow-up (roadmap):** strom v přehledu šablon (Ctrl+O) - EDList → seskupení per agenda.

## 2026-07-10 - Imposice: skládání stránek na fyzické listy (N-up, 2×A5 → A4)

Roadmap bod č. 1. Vlastnost VÝSTUPU, ne šablony: šablona vývěsky zůstává A5 (designer i náhled
beze změny), teprve PDF export skládá stránky 1:1 na listy (blok centrovaný, bez zmenšování).
Ověřeno: build 0 chyb, .NET 336/336 (+20: SheetComposer unit + PdfPig end-to-end smoke).

- **`Models/Layout/SheetLayout.cs`** - čistá vrstva bez SkiaSharp/WPF: `SheetLayoutOptions`
  (list, mřížka, opakování, značky + geometrie bloku), `SheetComposer` (`FromParamValues` parsování
  wire hodnot + validace, `ResolveGrid` AUTO = největší 1:1 fit, `Compose` stránky → listy),
  `SheetLayoutKeys` (wire kontrakt `__EXPORT_SHEET*`).
- **UI = exportní pole v PrintDialogu** (vzor `__EXPORT_SPLIT`): „Uspořádání na list"
  (Vypnuto/A4/A3 × výška/šířka), „Mřížka" (Auto/1×2/2×1/2×2/2×3/3×2/3×3), „Každá stránka na celý
  list (pro řezání)" - táž vývěska N× - a „Ořezové značky" (tenké šedé čáry na hranicích řezu).
  Detailní pole vidět jen při zapnutém listu (`ApplyVisibility`). Caché
  `AgendaParams.AddExportFields` + persistence per-agenda přes PrintParams (`ExportKeys` rozšířeno
  → restore při reopenu zdarma). Nevalidní mřížka (nevejde se 1:1) → česká hláška ve status baru.
- **Render:** `RenderMultiDataToPdf`/`RenderToPdf` volitelný `SheetLayoutOptions` - imposiční
  větev nalayoutuje logické stránky, `SheetComposer.Compose`, každý list = 1 PDF stránka,
  `RenderPage` s offsetem (už uměl). Aplikuje se v PrintDialogu (mock i ostrá data, split módy -
  imposice uvnitř každého souboru) i v exportu z hlavního okna (options zachyceny na UI threadu
  se snapshotem). PNG export záměrně bez imposice (rastr per stránka). Lichý počet → poslední
  pozice prázdné.
- Brožura/duplex řazení vědomě v2 (potřebuje až knižní JŘ) - model je na rozšíření připravený
  (options record + composer).

## 2026-07-10 - TR4: fixed-then-shrink výška řádku StopListu + sdílený helper

TRANSDEV požadavek (3. režim výšky řádku „zastávka"). Ověřeno: .NET 316/316, build 0 chyb. Rendering-only.

- **StopList:** pevná `DataRowHeight` se nově **zmenší při přetečení** pevné Height (dřív zůstala → obsah
  přetekl/ořízl). **Default (auto) beze změny** - mění se jen chování ručně nastavené pevné výšky při
  nedostatku místa (přeteče → zmenší). = přesně „fixed-then-shrink", který TRANSDEV chce.
- **Sdílený helper** `ScheduleElementBase.ScaledDataRowHeight(fontPt, factor)` - pravidlo fixed-then-shrink
  v **jednom místě**; StopList i DepartureGrid ho sdílí. (Dřív se rozešly: B2 opravil DepartureGrid, StopList
  lagoval - přesně ten drift.) DepartureGrid přepnut na helper (behavior-preserving). Malý extrakt; **plná
  unifikace řádkového layoutu = krok C** (R1/row-plan, gated).

## 2026-07-10 - TR2: souhrn „všechny zastávky na znamení" (AD, opt-in) - čeká na SK ověření

TRANSDEV požadavek - když jsou všechny zastávky na znamení, místo × u každé jeden souhrn.
Implementováno, ověřeno gold-safe; verifikace triggeru čeká na potvrzení SK kódování (kolega).

- **AD-only, opt-in** param `SOUHRN_NA_ZNAMENI` (default off = per-zastávka × jako dnes). Když všechny
  obsluhované neprojeté zastávky mají „x" (na znamení) → potlačí per-zastávka × + agregátní řádek legendy
  „× všechny zastávky linky jsou na znamení". Mechanika 1:1 analogická bezbariér agregátu (`allBBP`).
  **Čistě Caché** (žádná .NET změna - potlačení = absence atributu v datech, agregát přes POZNAMKY legendu).
- **Gold-safe:** Caché 33/33 (default off → gold beze změny). Scan potvrdil no-op na CB_CSADAD i SKNI
  (ani jedna linka není all-„x").
- ⚠️ **Čeká ověření (kolega):** `SKNI_MHDNITRA` (1308 linek) **nepoužívá „x"** pro na-znamení
  (dta2 znaky = A/E/L/P/T/o/p). Nutno potvrdit, jestli SK data používají „x" (pak feature funguje) nebo
  jiné kódování (pak rozšířit detekci `JeViditelnyAtribut` + scan). Do té doby na SK datech netriggeruje.

## 2026-07-10 - TR3: nástupiště jako sloupec zastávky (AD + MHD)

Zákaznický požadavek TRANSDEV (SK legislativa) - zobrazit nástupiště/označník u zastávek. Ověřeno:
Caché TD 33/33, TemplateDesigner.Tests 316/316, build 0 chyb.

- **Caché:** per-zastávka `NASTUPISTE` do kolekce ZASTAVKY - **AD** z J-věty `#3` (`nastTam:nastZpet` dle
  směru), **MHD** z `Oznacnik` (`ALVA.MHD.ZST`). Bez mocku/hardcoded dat - kde označník není zadaný,
  zůstává prázdné (standardně jen terminály/uzly). Gold re-baseline AD 2 + MHD 3 (counts identické, jen md5).
- **TD:** nový `StopFields.Nastupiste = "NASTUPISTE"` (sdílený kontrakt AD≡MHD zachován, zámek v
  `ColumnKeysTests`), parse do `ZastavkaData`, **nový StopList sloupec „Nástupiště"** (default skrytý = opt-in),
  `GetCellText`.
- **Reálná data ověřena:** CB_CSADAD linka **0320680** má nástupiště na 6 zastávkách (por 1/8/12/15/18/20,
  hodnoty 11, 1:4, 5:6, 1:1, 10:10, 9:9) - formule `#3` funguje. Většina zastávek prázdná (normální).

## 2026-07-10 - Audit A-AD + B2 (uzavření kontraktu skalárů + overflow konzistence)

Dokončení bloku A (AD strana, odblokováno po dojezdu paralelní font/značky práce) + B2.
Ověřeno: Caché TD 33/33, TemplateDesigner.Tests 316/316, build 0 chyb.

- **A-AD** - `SMER` (T/Z) + `NASTUPISTE` do `ZastavkoveJrAd` GetData + schéma (AD analog A1/A2; AD už měl
  `POPIS_SMER`=konečná). `NEJEDOU_SPOJE` ponecháno duplicitně (deprecated ve schématu). AD `GetMockData`
  jede přes `BuildStrankaZastavky` → nová pole dostane automaticky (žádný hardcoded mock). AD gold
  re-baseline 2 linky (0365001, 0365005 - counts identické, jen md5 = přidané skaláry, struktura zachovaná).
  Tím je **kontrakt skalárů sjednocen napříč MHD/AD/Linkový** (SMER=T/Z, POPIS_SMER=konečná všude).
- **B2** - DepartureGrid: `scale` i overflow/`CalculateHeightFromData` počítají ze **stejné skutečné výšky
  řádku** (vč. explicitní `DataRowHeight`). Odstraněna výjimka, co u nastavené `DataRowHeight` nezmenšovala
  fonty (obsah pak tiše přetékal). Nově se řádek škáluje konzistentně (shrink i grow) → **auto-shrink do fit
  jako ostatní JŘ elementy** (rozhodnutí: držet konzistentní shrink pattern). Bez gold dopadu (jen rendering).

## 2026-07-10 - Zastávkový JŘ: kroužkové denní značky týdenní platnosti (1-7) - dotažení 1:1 legacy

Dokončení font-bloku značek. Legacy (`mkPLinWin`/`GSId`) emituje týdenní platnost spoje jako kroužkové
číslice `1`-`7` (GSId id 8-16); náš kód je buď neemitoval (AD), nebo jako text „Po"/„Ne" (MHD).

- **AD** - z `plk` (`$p(hlava,":",7)`) se vytáhnou konkrétní dny (číslice 1-7) → anotace u minuty
  (`specialni` číslice → symbolový font ji kroužkuje) + legenda „① jede v pondělí" (`TextDneVTydnu`,
  plumbing přes `dnyLegenda` jako `bezbarLegenda`). Prázdné/`.`/`X`/`+` = skupinový vzor → bez značky
  (1:1 legacy: značka jen u konkrétních dnů). Ověřeno živě (spoj „jede v sobotu" → kroužková ⑥).
- **MHD** - emituje den jako **číslici** (`denZnak`) místo `ConvertSpecialniZnak` → „Po" (konzistentní
  s legendou, která už číslici používala). Gold-safe (CB_DP gold linky denní spoje nemají → no-op).
- Gold re-baseline AD (0365001 pozn 96→110, 0365005 168→183 = přidaná denní legenda; odj/zast beze změny).
  Caché 48/48, .NET 316/316.

Tím je **font-blok značek kompletní**: bezbariér piktogram, na-znamení „x" text, NZ box, datová omezení
box, týdenní platnost kroužkové 1-7. Vše locale-aware (CZ/SK).

## 2026-07-10 - Audit infra batch (B3, B4, B6, B7)

Robustnost/extenzibilita mimo kolizní zónu paralelní schedule-rendering práce. Ověřeno:
build 0 chyb, TemplateDesigner.Tests 316/316 (+11 z B4).

- **B7 - první `GetSchema` (R1) mimo UI thread.** `RefreshFieldsList` na cold cestě (uncached schéma)
  odbaví `GetSchema` přes `Task.Run` + doplní strom polí po návratu (`RefreshFieldsListCore`, marshalováno
  `TaskScheduler.FromCurrentSynchronizationContext`); warm (cachovaná) cesta zůstává synchronní. Nový
  `CacheAgendaProvider.IsSchemaCached`. Signatura `RefreshFieldsList()` beze změny → nulový caller ripple.
  Odstraňuje zamrznutí UI při otevření šablony cizí agendy na pomalé síti. + **`GetAvailableSkupiny` cache**
  (`_skupinyCache`, invalidace v `InvalidateCaches`) - sourozenci ji měli, tahle ne.
- **B4 - drift test registrace elementů.** `ElementRegistrationDriftTests`: reflexe `[JsonDerivedType]`
  na `TemplateElement` → pro každý (mimo allowlist uspaných `{Table}`) ověří, že `CreateElementByType`
  vrátí přesně ten typ (odhalí `_ => TextElement` fallback maskující zapomenutý case). Factory-only scope
  (paleta/Skia mimo). Allowlist se hlídá proti zplesnivění.
- **B3 - `CapabilityCatalog` data-driven.** AI průvodce už nebere JŘ elementy z string heuristiky
  (`Contains("JR")…`) + hardcoded seznamu, ale z `AgendaCapabilities.ScheduleElementTypes(ElementKind)` -
  jediný zdroj pravdy shodný s paletou/menu (zná LineTable). `ElementKind` protažen do `AiGuideContext`.
  `GenericTypes` opraveno `Table`→`DynamicTable` (AI katalog popisoval uspanou tabulku), `FriendlyName` +LineTable.
- **B6 - hardening resetu stránky.** String klíče property-path → sdílené konstanty `PagePathWidth/Height`;
  log když `FinalValues` klíč rozměru chybí (tichý výpadek detekce resetu se zviditelní); `OpenPropertiesForPage`
  `async void` → `async Task` s try/catch (chyby přes `IPopupDisplayer`, R2), callery `_ = …`.

## 2026-07-10 - Audit blok A: kontrakt skalárů zastávkového JŘ (MHD strana)

Sjednocení skalárního kontraktu podle vzoru linkové agendy (SMER = T/Z, POPIS_SMER = konečná).
Aditivní (R3). MHD strana + schéma + mock hotové; AD strana (přidat SMER, NASTUPISTE) odložená až
po commitu paralelní `PozitivniZnacka` práce (dotýká se AD gold testu → aby se neklobrovala).

- **A1 (MHD)** - `POPIS_SMER` už netiskne „T"/„Z", ale **název konečné** zastávky směru (jako AD/Linkový).
  T/Z je nově v samostatném poli **`SMER`**. Konečná = poslední zastávka ve směru (zachyceno ve smyčce
  ZASTAVKY, `konecnaNazev`). ⚠️ Breaking pro pilotní MHD šablony s `{POPIS_SMER}` pro T/Z → přejít na `{SMER}`.
- **A2 (MHD)** - `NEJEDOU_SPOJE` (legacy misnomer, u MHD = text prvního/posledního spoje) → nový klíč
  **`PRVNI_POSLEDNI_SPOJ`**; `NEJEDOU_SPOJE` ponecháno duplicitně pro kompatibilitu, ve schématu označeno „(zastaralé)".
- **A3 (MHD mock)** - mock doplněn o `SMER`=„T", `PRVNI_POSLEDNI_SPOJ`; `POPIS_SMER`=„Bystrc, zoo" je nyní
  pravdivý (dřív mock ukazoval konečnou, ostrý tisk „T").
- **A4 (schéma)** - AD deklaruje dříve nedeklarovaná pole: `POPIS_ZASTAVKA_CIS`, `POPIS_SMER_CIS`
  (AD-specifické, MHD je zatím neprodukuje); `VARIANTY_TRAS` + `TRASA_MIN` zdokumentovány jako wire-only
  (pole, ne skalár → plochý binding je nekonzumuje). AD `POPIS_SMER` label sjednocen na „Konečná směru".
- **MHD gold re-baseline** (3 linky) - `pages/zast/odj/pozn` **identické**, změnil se jen md5 (= jen skalární
  hodnoty, struktura zachovaná). Ověřeno determinismem (2. běh = shodný digest). MHD 32/33 (1 fail = AD 0365005
  je pre-existing z paralelní WIP, ne z této změny).

**Odloženo:** A1/A2 **AD strana** (přidat `SMER`, `NASTUPISTE`) - po commitu `PozitivniZnacka`; MHD `_CIS`
varianty (až bude potřeba, dohledat zdroj).

## 2026-07-10 - Audit před produkcí: bezpečný sweep (bloky B/C/D)

Odbaveny nízkorizikové nálezy auditu 2026-07-09 (behavior-preserving / aditivní / docs). Blok A
(kontrakt skalárů) a položky vyžadující UX rozhodnutí / gold data zůstávají otevřené v roadmapě.
Ověřeno: build 0 chyb, Caché 33/33 (gold+smoke+helpery), .NET 898/898 (TD 305 + EDControls 331 + EDISONMAIN2 262).

**Caché (behavior-preserving):**
- **C-a - symetrický úklid working global.** `k ^||TiskMHD` (MHD) přesunut z konce try za catch;
  `k ^||TiskAd` (AD) doplněn za catch. Working global se uklidí i po chybě (u poolovaných procesů
  nedrží data mezi requesty). Self-healing kill na začátku zůstává.
- **C-c - `AgendaParams.FetchParamsJson` zachovává `ERR`.** Wholesale `k ^||ML("OUT")` po nested
  `GetParameters` mazal i chybový uzel → selhání parametrů se tiše ztratilo (prázdný seznam místo
  hlášky). Nyní save/restore `ERR` okolo resetu counteru (counter se resetovat musí - `GetParameters`
  píše přes `$i(^||ML("OUT"))`, ne fixní `OUT("1")` jako `Instance.cls`).
- **C-d - `GetParameters` bez zbytečného `$$$FillParams`** (ZastavkoveJrAd + LinkovyJrAd). Parametry
  IdFa/dataDB nečtou → FillParams mimo try/catch byl jen riziko pádu mimo chybovou konvenci.
- **C-f (část) - `q $$$OK` → `ret $$$OK`** v ZastavkoveJrMhd + ZastavkoveJrAd (konvence TD).
  Export-split filtr dle `ElementKind` odložen (není mechanický - chce plumbing ElementKind do AgendaParams).

**.NET:**
- **B1 - DepartureGrid layout fingerprint doplněn** o `DataRowHeight`, `AnotaceGap`, `OdjezdGap`,
  `GroupSeparatorWidth`, `HourColumnPerGroup`, `ShowSkupinaDnuHeader`, `SkupinaDnuId`. Změna těchto
  vlastností v panelu se dřív v náhledu neprojevila (cache se neinvalidovala) - vypadalo to jako „mrtvé" pole.
- **B5 - F5 refresh inkrementuje `_previewDataGeneration`.** Guard proti dosednutí staršího in-flight
  fetche (souběh F5 × přepnutí instance).
- **D3 - drobné úklidy:** PrintDialog „Zavřít" → `{StaticResource EDStrings.Close}`; AI API klíč
  `TextBox` → `PasswordBox` (sync přes code-behind, Password není bindovatelný); AI debug JSON do
  `Path.GetTempPath()` místo instalačního adresáře; komentář k prázdnému `catch` v CellStyleEditor converteru.

**Docs (D1, D4):**
- **D1 - tabulka zkratek** (`ux-conventions.md`): sekce = `Ctrl+Shift+↑/↓` (ne holé ↑/↓); doplněn
  Shift=velký / Ctrl=jemný krok posunu elementu.
- **D4 - font 42b/C2** v roadmapě přeformulován (zastávkový JŘ hotový 9.7., zbývá linková část LineTable `ZNAK`).

**Odloženo (zůstává v roadmapě):** B2 (DepartureGrid overflow vs auto-scale - **nese UX rozhodnutí**,
ne mechanický fix: Render záměrně počítá scale z font-driven výšky, aby velká `DataRowHeight`
nezmenšovala fonty, kdežto overflow detekce bere `layout.RowH` - sjednocení musí rozhodnout, co má
u velké `DataRowHeight` vyhrát), celý blok A (kontrakt skalárů), B3/B4/B6/B7, C-b/C-e, D2, agenda-rename sjednocení.

## 2026-07-09 - Komplexní audit před produkcí (architektura + agendy + UI/UX)

Tři paralelní průzkumy kódu (.NET architektura/extenzibilita, Caché agendy MHD↔AD, UI/UX +
pravidla R1–R5) + revize dokumentace. Výstup:

- **[`audit-pred-produkci-2026-07-09.md`](audit-pred-produkci-2026-07-09.md)** - detail všech
  nálezů (file:line + lidské vysvětlení + doporučený fix).
- **Roadmap sekce „🔍 Audit před produkcí"** - rozhodovací checklist (bloky A–D) k projití.
- Nejzávažnější: **kontraktní rozkol skalárů zastávkového JŘ** (`POPIS_SMER` = „T/Z" v MHD vs
  název konečné v AD; `NEJEDOU_SPOJE` = první/poslední spoje v MHD vs nástupiště v AD; MHD mock
  drift). Dále DepartureGrid stale layout-cache, CapabilityCatalog drift (nezná LineTable),
  první `GetSchema` sync na UI threadu (R1), Caché hygiena (working global kill, ERR polykání).
- Pozitivní: vrstvy drží, wire čistý JSON, versioning s guardy, dirty-flag kompletní, žádný
  MessageBox ve VM, API klíč mimo git. Žádný kód neměněn - jen analýza + zápis.

Změna velikosti/orientace stránky se dřív snažila obsah „zachovat", ale jen napůl - přeškálovala jen
výšky body sekcí (`ScaleBodySections`), pozice a šířky elementů nechala být. Výsledek po A4→A5 / otočení:
elementy visely mimo stránku, překrývaly se, sekce squeezené. UX rozhodnutí (potvrzeno s uživatelem):
změna rozměrů = **čistý reset**, konzistentní se změnou agendy.

- **`ElementEditDialog.FinalValues`** - snapshot finálních hodnot polí po OK (klíč = property path).
  Volající přečte zamýšlené rozměry **bez** vykonání commandu (obecné, i pro jiné editory).
- **`OpenPropertiesForPage` detekuje změnu `PageSize.Width/Height`** (epsilon 0.1 mm). Změna →
  `ResetForPageChangeAsync`; jinak běžná undoable změna (okraje, kultura, font) přes `Execute`.
- **`TemplateDesignerViewModel.ConfirmPageResetAsync`** - vždy se ptá (i u uložené šablony): dirty →
  Save/Discard/Cancel, čistá → prosté potvrzení. Řeší tichý wipe (dřív by `ConfirmUnsavedChanges`
  u čisté šablony vrátil true bez ptaní).
- **`ResetForPageChangeAsync`** - čisté plátno s novou velikostí; zachová agendu (PageEdit ji needituje),
  převezme velikost/okraje/kulturu/font z dialogu přes `ElementPropertyAccessor` (reflexe, klíč = path).
  `ReplaceWithBlankTemplate` rozšířen o `configure` callback (PageSize nastaven před přidáním sekce).
- Odstraněna půlka řešení (`ScaleBodySections` v deskriptorové cestě už není use-site - descriptor
  editor stránky je legacy fallback). 305/305 .NET, build čistý. Živě ověřeno (reset / okraje bez wipe / Storno).

## 2026-07-09 - Zastávkový JŘ: symbolový font značek (Timetable, locale-aware) + katalog

Speciální symboly (bezbariér, na znamení…) se tiskly jako holé písmeno „H"/„x" místo piktogramu -
chyběl symbolový JŘ font (dlouho odložený bod „C2"). Odblokováno dodáním fontu.

- **Embedded `TimeTable2.ttf` (CZ) + `TimeTable2SK.ttf` (SK)** do `Fonts/` (M-line vlastní EDISON font;
  csproj glob je zabalí). Rodiny `Timetable_nove` / `TimeTable_nove_SK`.
- **`FontResolver.SymbolFontFamily(culture)`** - locale→font dle `Template.CultureCode` (`sk-*` → SK),
  1:1 s legacy `formTISKJRReportDotNet_Module` (ř. 1197).
- **Symbolový font pro jednoznakové `SpecialniZnak`** v legendě (NotesLegend) i u minut (DepartureGrid
  `anotGlyphFont`) - `H`/`F` → piktogram (vozíček). Uživatelský `AnotaceSymbolStyle` font má přednost.
- **Na znamení `x`/`y`/`z` = prostý text**, ne piktogram (helper `SymbolJeText`, dle konvence) - jinak
  font na pozici `x` kreslí vláček. Bezbariér zůstává piktogram.
- **NZ** (negativní) beze změny - ruční inverzní box (už bylo správně).
- **Katalog symbolů** (`.claude/docs/elementy-architektura.md`) - převzatá mapa z legacy `GSId` (66 symbolů:
  id·znak·význam·render-mód) + zdroje, jako reference pro další dotažení (text-multichar, kompozity).
- Čistě render vrstva → gold netknuté, 305/305 .NET. Živě ověřen render (vozíček, na-znamení x, SK přepnutí).

**Známý zbytek (samostatný Caché track):** pozitivní datová omezení („jede jen/také") se dnes slepují
s negativními do jedné NZ značky (box) - ve starém jsou pozitivní **kroužkovaná**. Fix = rozdělit
pozitivní/negativní značky + kroužek v renderu + gold re-baseline. Plus kroužkové dny v týdnu (emise dat).

## 2026-07-09 - Zastávkový JŘ: konzistentní řazení legendy poznámek dle typu (nový default)

Legenda poznámek se dřív vypisovala v pořadí, v jakém ji stavěla agenda („náhodně"; legacy stejně).
Helpdesk námět kolegy: seřadit konzistentně. Element už měl hook `LegendRazeni`, tak přidán mód:

- **`LegendRazeni.DleTypu`** - stabilní priorita `NegativniZnacka → SpecialniZnak → Text`
  (= značky → atributy → text). Uvnitř tieru zachová pořadí z dat.
- **Nový default** elementu (`Razeni = DleTypu`, dřív `Vychozi`). „Výchozí (dle dat)" zůstává jako opt-out.
- Dropdown „Řazení" rozšířen; sloupce / směr plnění beze změny - typ jen řídí lineární pořadí,
  které se pak zalomí do sloupců (seřaď → naplň, stávající architektura).
- Čistě .NET (render vrstva, „šablona říká JAK") → žádný Caché/gold dopad. Používá `PoznamkaData.Typ`
  (už v POCO). 305/305 .NET. Additivní enum + default → bez migrace (staré explicitní `Vychozi` se ctí).

## 2026-07-09 - Zastávkový JŘ (AD): dotažení odsazení názvu zastávky z ^JRL

Element odsazení názvu zastávky uměl vždy (`z.Odsazeni` → N mezer přes `ShowStopIndentation`), ale AD
posílalo natvrdo `ODSAZENI=0`. Dotaženo z legacy dat:

- **Zdroj:** `^JRL(sku,lin,"J",poradi)` - **5. `#`-segment** = počet znaků odsazení (legacy `mkPLinWin`
  „odrazeni nazvu zastavky" = `gTrsLin dta 7`, ověřeno v `JRapiLin.mac` parseru + `JRapiLinPrtWin.mac`).
- **Fix (1 řádek):** `ZastavkoveJrAd` stop-loop čte `s odsazeni = +$p(veta,"#",5)` a předá do `BuildZastavka`
  místo hardcoded `0`. C# strana beze změny (parse + render byly připravené).
- Behavior-preserving pro linky bez odsazení (0 = beze změny) → **gold netknuté** (33/33 TD zelené).
  Živě ověřen render odsazených podzastávek.

## 2026-07-09 - Zastávkový JŘ: revize kontraktu SKUPINY_DNU (schéma) - trojice+1 uzavřena

Čtvrtá (poslední) řádková kolekce zastávkového JŘ. Wire konzistentní (AD i MHD `ID, NADPIS, NADPIS2,
PORADI`), ale schéma se rozcházelo: AD `ID, NADPIS, PORADI` (bez NADPIS2), MHD `ID, NADPIS, NADPIS2`
(bez PORADI), obě navíc vystavovaly `ID` (join/pivot klíč).

- **Schéma SKUPINY_DNU sjednoceno na `NADPIS, NADPIS2, PORADI`** (AD i MHD). `ID` skryto (zůstává ve
  wire jako pivot klíč, konzistentní se skrytým ZASTAVKY.ID / ODJEZDY join klíči). **Jen GetSchema →
  gold netknuté** (33/33 TD).

Tím je zrevidovaný **kompletní datový kontrakt zastávkového JŘ**: skalární pole + všechny 4 řádkové
kolekce (ZASTAVKY, SKUPINY_DNU, ODJEZDY, POZNAMKY). Princip všude: honest flat kontrakt (jen
user-facing pole), join/pivot klíče + prezentační nápovědy zůstávají ve wire ale nejsou sloupce,
AD ≡ MHD, mrtvá pole pryč.

## 2026-07-09 - Zastávkový JŘ: revize kontraktu POZNAMKY (schéma) + zaflagovaná MHD KATEGORIE mezera

Poslední z trojice (ZASTAVKY/ODJEZDY/POZNAMKY). Názvy čitelné, žádné mrtvé pole ve wire (vše se
konzumuje: TYP/ZNAK/TEXT render, FORE_COLOR barví znak, KATEGORIE čte filtr viditelnosti, BACK_COLOR
rezervováno). Řešena jen schéma-nekonzistence:

- **Ploché schéma POZNAMKY sjednoceno na `TYP, ZNAK, TEXT`** - AD dřív vystavovalo 6 (vč. interní
  `KATEGORIE` + hintů `FORE/BACK_COLOR`), MHD 3. AD strimováno na 3 (MHD už mělo). `KATEGORIE`
  (filtr-tag, jako join klíč) + barevné hinty zůstávají ve wire, jen se nenabízejí jako sloupce.
  **Jen GetSchema → gold netknuté** (33/33 TD zelené beze změny digestů).

**Zaflagováno (samostatný ticket, ne úklid):** MHD poznámky nenesou `KATEGORIE` → kategorie-logika
`NoteVisibilityFilter` (#7161 D5 „× na znamení", D6 agregát bezbariér, rozsahové skrytí) je fakticky
**jen AD**. Fix není triviální tag: AD má atributovou legendu jen „na znamení", MHD emituje všechny
atributy (`ZastavkoveJrMhd` ř. 1219) → nutná doménová klasifikace + gold re-baseline + test filtru.

## 2026-07-09 - Zastávkový JŘ: revize kontraktu ODJEZDY (mrtvé pole + schéma)

Analogická revize jako u ZASTAVKY, teď pro ODJEZDY (zdroj DepartureGridu). ODJEZDY je čistě
**pivot-zdroj** (sloupce mřížky = skupiny dnů, ne pole) → žádné „sloupec = field" sjednocení jako
u StopListu; AD ≡ MHD navíc už bylo symetrické a názvy čitelné. Řešily se jen mrtvé/nekonzistentní pole:

- **`ZKRACENE_OZNACENI_LINKY` odstraněno** - v obou agendách vždy `""` (call sites posílaly jen 4 argy) a .NET ho nikde nerenderoval (doubly-dead, analog `TarifniCislo`). Pryč z wire (`BuildOdjezd` + param), schématu i POCO `OdjezdData.ZkraceneOznaceniLinky`.
- **Ploché schéma ODJEZDY sjednoceno na `HODINA, MINUTA`** (AD i MHD). Skryté ze schématu (zůstávají ve wire): `ZASTAVKA_ID`/`SKUPINA_DNU_ID` (join/pivot klíče - konzistentní se skrytým `ZASTAVKY.ID`) + `FORE_COLOR`/`BACK_COLOR` (prezentační nápovědy, čte je jen render, hex nedává smysl jako sloupec). Dřív AD vystavovalo 7 polí, MHD 4 → teď obě 2.
- **Ponecháno vědomě:** odjezdové `FORE_COLOR`/`BACK_COLOR` (R3 hint, renderer je čte - dormant, ne dead) a anotační `SPOJ` (AD plní reálné ID spoje z DB - rezerva pro budoucí per-spoj filtr, ne hardcoded test).
- ⚠️ Gold re-baseline (ZKRACENE odebrání změnilo `GetData` - jen `md5`, počty řádků beze změny). Ověřeno na serveru: **48/48** Caché, 305/305 .NET.

## 2026-07-09 - Zastávkový JŘ: gold re-baseline po sjednocení kontraktu

Přegenerování zmrazených digestů po záměrné změně `GetData` (fáze 1 + úklid):

- **Gold digesty přepsány** (AD 0365001/0365005, MHD 1001/1003/1006) - změnil se **jen `md5`**, počty řádků (`pages/zast/odj/pozn`) **beze změny** → potvrzení, že šlo čistě o přejmenování klíčů + zmizení `TC`/`ZAKLADNI_TRASA`/`PRUBEHY`, ne o datovou regresi.
- **`MhdLegacyOracle`** čte název zastávky z našeho výstupu nově přes `NAZEV_ZASTAVKY` (byl `POPIS`) → legacy parita zpět 1:1.
- Ověřeno na živém serveru: **48/48** Caché testů zelených (vč. 33 TD: gold + parita).

## 2026-07-09 - Zastávkový JŘ: úklid mrtvých polí zastávek (wire + .NET)

Dočištění po sjednocení kontraktu - odstranění write-only mrtvých polí, která se nikde nečetla:

- **`ZAKLADNI_TRASA`** (bool „na základní trase") - v `BuildZastavka` napevno `"true"` u každé zastávky → nulová informace; skutečná logika žije v `ZakladniCisloTrasy` (řídí variantní časové sloupce). Emit + POCO `z.ZakladniTrasa` pryč (AD+MHD).
- **`PRUBEHY`** (písmena rozdílů tras u zastávky) - v ostrém `GetData` **nikdy** neplněné (jen MHD mock); účel pokrývají anotace rozdílů tras v DepartureGridu + multi-variantní časové sloupce. Emit + `AddPrubeh` + POCO `z.Prubehy` pryč (AD+MHD).
- **`TarifniCislo`** POCO - osiřelé po odebrání TČ (fáze 1), pryč.
- ⚠️ Mění `GetData` výstup (2 klíče pryč) → součást téže gold re-baseline jako fáze 1/2. Testy 305/305, Caché kompiluje čistě.

## 2026-07-09 - Zastávkový JŘ: StopList sloupce field-based 1:1 s DynamicTable (sjednocení kontraktu, fáze 2/2)

Dokončení sjednocení - **identita sloupce StopListu = field ID kolekce ZASTAVKY**, takže je 1:1 s poli, která nabízí DynamicTable nad stejným zdrojem. Uživatel už nemusí luštit, proč jedna cesta zná `POPIS` a druhá `NAZEV_ZASTAVKY`.

- **`StopFields`** (`Models/StopFields.cs`) - nový jednotný field-ID kontrakt (`NAZEV_ZASTAVKY, CAS_JIZDY, PORADI, AKTUALNI_ZASTAVKA, ZONY` + interní `ID`, variant prefix `CAS_JIZDY#`). Sdílený mezi StopList render (`GetCellText`/anchory/varianty), `.NET` parse (`BuildZastavky`) a PDF import buildery.
- **StopList sloupce překlíčované** ze směrovacích kódů (`StopName`/`TravelTime`/…) na field ID. `GetDefaultColumns`, `GetCellText`, editor `FriendlyName` → `StopFields`. **Mrtvé sloupce `RouteLetters`/`BaseRoute` (průběhy/ZT) odstraněny** (uživatel je označil jako mrtvé).
- **`ColumnKeys` zúženo na linkový JŘ** - obsahuje už jen klíče `LineTableElement` (`StopName/Tc/Zone/Km/Mhd/Trip`). Zastávkový a linkový JŘ mají různý datový tvar → kontrakt nesdílí (vědomě).
- **Migrace `1.2.0 → 1.3.0`** (`MigrationUnifyStopColumns`): StopList `columns[].key` staré→field ID + drop `Tc`/`RouteLetters`/`BaseRoute`; DynamicTable/Table `columns[].field` `POPIS`→`NAZEV_ZASTAVKY`, `JE_AKTUALNI`→`AKTUALNI_ZASTAVKA`; placeholder tokeny ve všech textech/výrazech (`{POPIS}`→`{NAZEV_ZASTAVKY}`, `{JE_AKTUALNI}`→`{AKTUALNI_ZASTAVKA}`, `{TC}` pryč) - hranice tokenu chrání skalární `{POPIS_DOPRAVCE}`.
- Testy 305/305 (+6: `StopFields` lock, StopList default = field ID, 5× migrace vč. idempotence a ochrany hranice tokenu).
- **Specials zůstávají StopList-only** (šipka směru, inline značky/na-znamení, zvýraznění aktuální, rozvinutí variant tras) - nejsou součástí sdíleného sloupcového kontraktu, jak bylo zadáno.

## 2026-07-09 - Zastávkový JŘ: čitelná ID polí + TČ pryč všude (sjednocení kontraktu, fáze 1/2)

Sjednocení datového/sloupcového kontraktu ZASTAVKY mezi StopList elementem a DynamicTable - **čistá varianta A**: přejmenování drátových klíčů všude tak, aby ID odpovídalo labelu (`SLOVO1_SLOVO2`), ne kryptickému Caché klíči. Cíl: uživatel v podmíněném renderu / počítaných polích píše `{NAZEV_ZASTAVKY}`, ne `{POPIS}`.

- **Přejmenování klíčů (AD+MHD, wire + schéma + .NET parse):** `POPIS → NAZEV_ZASTAVKY`, `JE_AKTUALNI → AKTUALNI_ZASTAVKA`. `BuildZastavka` emituje nové klíče, `GetSchema` je vystavuje, `StopScheduleElementBase.BuildZastavky` je čte.
- **`TC` odebráno úplně (AD i MHD)** - v AD nesl jen J-klíč (= interní `ID`), v MHD `ZST.Cislo` (interní CIS číslo, pletlo by se uživateli). Poziční potřebu pokrývá `PORADI`. StopList sloupec „TČ" odebrán (`ColumnKeys.Tc` konstanta zůstává - sdílí LineTable).
- **Finální plochý kontrakt (AD ≡ MHD, identický):** `NAZEV_ZASTAVKY, CAS_JIZDY, PORADI, AKTUALNI_ZASTAVKA, ZONY`. `ID` zůstává v datech jako interní join-klíč (`ODJEZDY.ZASTAVKA_ID`), ne ve schématu.
- ⚠️ **Gold digesty se mění** (změněn `GetData` výstup - klíče přejmenované, TČ pryč) → nutná re-baseline na živém serveru (`ZastavkoveJrAdGoldTest`/`ZastavkoveJrMhdGoldTest`). .NET testy 299/299 zelené.
- **Zbývá (fáze 2):** StopList sloupce plně field-based (identita sloupce = field ID, jako DynamicTable) + oba editory čerpají dostupné sloupce ze schématu + migrace uložených šablon (`{POPIS}→{NAZEV_ZASTAVKY}` atd.).

## 2026-07-09 - Zastávkový JŘ: AD TČ odebráno + ZONY vystaveno (plné 1:1, gold-safe)

Dokončení 1:1 ZASTAVKY mezi StopList a DynamicTable.

- **AD `TC` odebráno ze schématu** - v AD `TC` nese jen J-klíč (= interní `ID`), ne reálné tarifní
  číslo (`PORADI` = zobrazovací čítač je to smysluplné). MHD `TC` (skutečné číslo zastávky) zůstává →
  schéma se AD↔MHD vědomě rozchází podle reálného významu dat.
- **`ZONY` vystaveno ve schématu** (AD+MHD, „Zóny") - bez změny `GetData` (**gold digesty netknuté**):
  `ZONY` zůstává v datech vnořené pole, ale `DataContext.GetString` skalární pole **spojí „/"** (např.
  „1/2"), takže ho zobrazí i plochá buňka DynamicTable. StopList beze změny (parsuje si pole sám).
  Objektová pole (ATRIBUTY {ZNAK,POPIS}) join přeskočí → do schématu se nevystavují.
- **Výsledek:** ploché pole ZASTAVKY jsou teď **1:1** - StopList i DynamicTable nabízí `POPIS, CAS_JIZDY,
  PORADI, JE_AKTUALNI, ZONY` (+ MHD `TC`). Jediný doménový rozdíl = StopListové značky/šipka/aktuální
  (inline render, ne sloupce) - inherentní specializovanému elementu.
- +4 testy `DataContextTests` (GetString: skalár / fallback / skalární array join / objektový array skip).

## 2026-07-09 - Zastávkový JŘ: pole ZASTAVKY co nejvíc 1:1 mezi StopList a DynamicTable

Pokračování „jeden zdroj = schéma ZASTAVKY". Cíl: uživatele nemate, že jedna cesta (StopList element)
nabízí jiná pole než druhá (DynamicTable nad zdrojem ZASTAVKY).

- **Schéma AD↔MHD sjednoceno** - obě agendy vystavují stejnou plochou sadu: `POPIS, TC, CAS_JIZDY,
  PORADI, JE_AKTUALNI`. MHD doplněno o `PORADI`+`JE_AKTUALNI` (data je emitují). `ID` odebráno z obou
  (interní join klíč `ODJEZDY.ZASTAVKA_ID`→`ZASTAVKY.ID`, uživatele mátl; data se emitovat nechají).
- **StopList doplněn o skryté sloupce `Pořadí` + `Aktuální zastávka`** (`ColumnKeys.Poradi`/`JeAktualni`,
  default `Visible=false`) - data v `ZastavkaData` už byla. StopList tak nabízí stejná plochá pole jako
  DynamicTable (+ navíc `Zóna`).
- **Výsledek:** ploché sloupce/pole jsou 1:1 (StopList i DynamicTable: POPIS/TC/CAS_JIZDY/PORADI/JE_AKTUALNI).
  Jediný zbylý rozdíl = **`Zóna` (vnořené `ZONY`)** má jen StopList - plochá DynamicTable vnořené pole neumí.
  Úplné sjednocení `ZONY` = zploštit na string (koordinovaná Caché+.NET změna + re-baseline gold digestů),
  vědomě odloženo (nízké ROI - DynamicTable nad ZASTAVKY je fallback, ostrý nástroj je StopList).

## 2026-07-09 - Zastávkový JŘ: jeden zdroj pravdy = schéma ZASTAVKY (úklid mrtvých polí)

Odstranění mrtvých / hardcoded polí, ať schéma ZASTAVKY = jediný zdroj pravdy sdílený DynamicTable
(nabídka sloupců) i StopList/výrazy. Dřív matoucí: DynamicTable nad zdrojem ZASTAVKY nabízel pole,
která vždy vracela konstantu (uživatel jimi nemohl nic rozumného udělat).

- **Schéma agendy (GetSchema, ZASTAVKY):** odebrána mrtvá pole - AD: `NA_ZNAMENI` (natvrdo 0),
  `ODSAZENI` (0), `ZAKLADNI_TRASA` (true); MHD: `NA_ZNAMENI`. Živá zůstala: `ID, POPIS, TC, CAS_JIZDY,
  PORADI, JE_AKTUALNI` (AD) / `ID, POPIS, TC, CAS_JIZDY` (MHD). DynamicTable je tak už nenabízí.
- **StopListElement:** z výchozích sloupců vyhozeny mrtvé sloty `Trasa` (`RouteLetters` → `PRUBEHY`,
  v živých datech prázdné) + `ZT` (`BaseRoute` → `ZAKLADNI_TRASA`, vždy „1"). Zbyly živé: Čas, TČ,
  Zastávka, Zóna. (Klíče v `ColumnKeys` ponechány kvůli zpětné kompat. uložených šablon.)
- **StopList helptext viditelnosti řádku:** vypisoval mrtvá pole a měl nefunkční příklad `{NA_ZNAMENI}==1`
  → nově živá pole (`POPIS, TC, CAS_JIZDY, PORADI, JE_AKTUALNI`) + příklad `{JE_AKTUALNI} == 1`.
- **Pozn.:** pro výrazy (highlight/viditelnost) je zdroj polí už sdílený (StopList i DynamicTable čtou
  celý řádek dat). Doplnění živých plochých polí (`PORADI`/`JE_AKTUALNI`) jako *sloupců* i do StopListu
  (default skryté) je možný navazující krok - dnes jsou dostupné ve výrazech, ne jako sloupce.

## 2026-07-09 - Zastávkový JŘ: poctivější labely skalárních polí agendy (per-agenda)

Validace „název pole ↔ co reálně vrací z Caché" (levý panel „Pole agendy"). Jen **labely** (zobrazení),
klíče beze změny → **nerozbíjí datový kontrakt** (R3). Labely smí divergovat AD↔MHD, ať každý vidí svou
doménovou terminologii.

- **AD `NEJEDOU_SPOJE`** „Nejedou spoje" → **„Nástupiště"** - v AD to pole reálně nese nástupiště vývěskové
  zastávky (zdokumentovaný legacy misnomer), ne „nejedou spoje". (MHD label ponechán - tam skutečně nese
  text „nejedou spoje".)
- **MHD `CISLO_LINKY`** „Číslo linky" → **„Číslo linky (dle licence)"** + **`LOKALNI_OZNACENI_LINKY`**
  „Označení linky" → **„Veřejné označení linky"** - v MHD je `CISLO_LINKY` = `CisloLinkyDleLicence`
  (licenční), veřejné číslo je v `LOKALNI_OZNACENI_LINKY` (`CisloVerejne`); labely to teď rozlišují.
  (AD ponecháno - `CISLO_LINKY` = `lin` z `^JRL` je tam prostě číslo linky, nemá licenční/veřejný rozštěp.)
- **Pozn.:** stejný `CISLO_LINKY` label mají i linkové agendy (`LinkovyJr*`) - sjednotit až při jejich
  review (jiná agenda + aktivně se na ní pracuje). Klíč `NEJEDOU_SPOJE` zůstává (rename = breaking) -
  label-key nesoulad v AD je vědomý; čistý fix (samostatný `NASTUPISTE` klíč, aditivní) je možný pozdější krok.

## 2026-07-08 - Víceřádkové záhlaví buňky přes `\n` (dvojjazyčné záhlaví sloupce)

Buňka/záhlaví tabulky může mít víc řádků - typicky dvouřádkové (CZ/EN) záhlaví sloupce.
Dřív šlo jen u DepartureGrid (datově z `Nadpis`/`Nadpis2`), autorské záhlaví sloupce ne.

- **`CellRenderer.DrawCell`** - multi-line větev: rozdělí text (`CellLayout.SplitLines`), blok N řádků,
  VAlign umístí blok, HAlign zarovná každý řádek zvlášť + decorations. Jednořádkový = rychlá cesta.
  **Jedno místo → všechny tabulkové elementy** (StopList, TableElement, LineTable, DepartureGrid) i header/data buňky.
- **Syntaxe:** literální token `\n` (autorská zkratka, `CellLayout` ho normalizuje na skutečný zlom)
  **nebo** Enter v poli (`AcceptsReturn` na poli „Záhlaví" v editoru sloupců zastávek + hint).
- **`StopListElement`** - výška záhlaví line-aware (`× MaxHeaderLines()`) v Render i CalculateHeightFromData (parita).
- TextElement `\n` už uměl (`TextWrapHelper`) - beze změny. TableElement pevná `HeaderHeight` (multi-line se vykreslí, výšku řídí uživatel); DepartureGrid/LineTable datově řízená záhlaví - bez změny výšky.
- Bez změny JSON schématu / migrace (`Header` je string s `\n`), zpětně kompatibilní. +9 testů `CellLayoutTests`, 295/295.
- Dokumentace: uživatelská příručka (obecná §8 + zastávkový JŘ §4).

## 2026-07-08 - Tabulkové jádro: levné švy před v1 (z analýzy architektury)

- **Krok 2 - extrakce středního švu (row-feature).** Po #1 už byla highlight logika sdílená přes
  `RowConditions` + `HighlightRules`; zbývala duplikovaná visibility trojice. Vytaženo: sdílený filtr
  `RowConditions.VisibleRows` (generický `<T>` přes `rowOf` selektor + `DataContext` overload, bez
  alokace při prázdném výrazu) - používá DynamicTable i LineTable; sdílený descriptor
  `RowConditionDescriptors.RowVisibility(get, set)` - nahradil 3× kopii 6řádkového `StringPropertyDescriptor`.
  **Resolve-wrappery (3a) vědomě NEextrahovány** - sdílené jádro je už `TableLayout.ResolveCascade`; per-element
  metody jsou jen tenké adaptéry na různé typy sloupců (TableColumn vs StopColumn) a baked parenty, genericky
  by to stálo víc než ušetří. **Pětice levných švů uzavřena** (#1,#3,#4,#5 + #2). +3 testy (VisibleRows).

- **Krok 1 (jádro) - řádkové zvýraznění: 1 pravidlo → seznam `HighlightRule[]`.** Model `HighlightRule`
  `{ Condition, BackgroundColor, TargetColumn? }`, sdílený DynamicTable/StopList/LineTable jako
  `List<HighlightRule>?` (nahradil skalární `RowHighlightExpression`/`RowHighlightColor`). Sémantika
  **první splněné pravidlo vyhrává**; `RowConditions.HighlightColor(rules, row)`. `TargetColumn`
  (per-sloupec) je v modelu rezervovaný, render zatím barví celý řádek (per-sloupec render = #2).
  **Breaking JSON → migrátor** (`MigrationHighlightRules`, 1.0.0 → 1.1.0): staré pole každého elementu
  se překlopí na `highlightRules:[{…}]` a odstraní; `TemplateMigrator.CurrentVersion` bump na `1.1.0`.
  `TemplateSerializer` nově razítkuje verzi při uložení (uložení = zápis aktuálního formátu). Odstraněn
  mrtvý rezervovaný `ColorRule` (nahrazen `HighlightRule`). Kryto testy (RowConditions, migrátor,
  round-trip, Clone completeness).
- **Krok 1 (editor) - `HighlightRulesEditorDialog` (varianta 3).** Custom dialog (DataGrid Add/Remove/▲▼,
  podmínka + swatch barvy, barva pozadí vybraného pravidla přes color picker) - konzistentní se vzorem
  editoru sloupců. Launch přes `FieldTypeButton "__highlightRules"` v `*Edit.cls` (EDInputForm), zapojený
  na **dvou plochách**: inline panel „Vlastnosti" (`InlinePropertiesForm.ButtonClicked` passthrough →
  `MainWindow.OpenHighlightRulesDialog`) i tab „Obecné" columns dialogů (StopColumns/DynamicTable/LineTable
  `GeneralForm.ButtonClicked` → `RequestApplyHighlight`). Apply přes `ApplyHighlightRulesEdit` (undoable
  `SetPropertyCommand`, prázdný seznam → null). Úzké rozhraní `IRowHighlightable` drží dialog + apply
  generické nad 3 elementy. `__`-prefix klíče drift test přeskakuje. **#1 hotové.**
- **Krok 1 (review fixy).** Z adversariálního review commitů #1: **(H1)** `TemplateSerializer.DeserializeSection`
  protažen migrátorem (obalí sekci do mini-šablony 1.0.0) - cesta šablon sekcí byla trvale migrátor-slepá,
  tiše by ztrácela data při každé budoucí breaking migraci; **(M2)** highlight z columns dialogů (StopColumns/
  DynamicTable/LineTable) se aplikuje až na OK dialogu (deferred `ResultHighlightRules`/`HighlightEdited`),
  ne okamžitě - Storno teď změnu zahodí, konzistentní se vzorem `style:` (inline panel zůstává okamžitý,
  tam je to správně); **(L5)** migrace nevytvoří bezbarevné pravidlo (staré zvýraznění vyžadovalo podmínku
  i barvu); **button** dostal `f.value = "Upravit pravidla…"` (jinak prázdný/úzký - `FieldTypeButton` renderuje
  `Value`, ne `Label`). Kryto 2 novými testy (DeserializeSection migrace, bezbarevné pravidlo).


Navazuje na analýzu připravenosti tabulkového jádra (4 agentní dimenze). Postupně se zavírají levné
seamy, které jinak platí daň při každém dalším tabulkovém elementu / featuře.

- **Krok 3 - reflexní test úplnosti `Clone()` (`CloneCompletenessTests`).** Enumeruje **všechny**
  konkrétní `TemplateElement` potomky přes `[JsonDerivedType]`, každou perzistentní property zmutuje na
  ne-default a porovná `serialize(original)` vs `serialize(clone)` (mimo přegenerované `Id`). Jeden test
  pokryje i budoucí elementy + meta-test hlídá, že enumerace = přesně konkrétní potomci.
  **Chytil reálný bug:** `CopyScheduleBaseProperties` nekopírovala `DataRowHeight` → při `Clone()`
  (Ctrl+D / undo) se ztrácela u `LineTableElement` a `NotesLegendElement` (StopList/DepartureGrid ji měly
  vypsanou explicitně, proto neselhaly). Fix: doplněna do base helperu, redundantní explicitní výpisy ve
  StopList/DepartureGrid odstraněny (base vlastní base property).
- **Krok 5 - `SkiaHeightCalculator` deleguje výšku `TableElement` na element.** Kalkulátor už
  nemá inline `CalculateTableElementHeight` (znal interní strukturu tabulky) - nově `TableElement`
  vystavuje `CalculateHeightFromData(data)` jako ostatní tabulkové elementy a kalkulátor jen deleguje.
  Behavior-preserving (logika 1:1, jen přesunutá; magic „3 ukázkové řádky" → existující `DesignerPreviewRows`).
  Nový tabulkový element už nepřidává větev do switche kalkulátoru.
- **Krok 4 - agregace nad výrazem (uživatelská featura).** Souhrnné řádky tabulky uměly jen
  `SUM({POLE})` (holé pole); nově `SUM({CENA}*{POCET})` - argument je **výraz** vyhodnocený per řádek
  přes `ExpressionEvaluator`, takže lze sečíst i počítaný sloupec (dřív šel počítaný sloupec vytvořit,
  ale ne agregovat). Zpětně kompatibilní (holé pole beze změny), aditivní. Omezení: bez vnořených
  závorek v argumentu. Zaevidováno v CLAUDE.md (výrazový jazyk).

## 2026-07-07 - Úklid z auditu v1 (P-C: R4 dluh + drobnosti)

Jasné (bezrozhodovací) hygienické body z auditu. Build 0 chyb, test projekt 0 chyb.

- **C-a - dokončení H3 (R4): 2 zapomenuté ViewModely bez WPF.** `ElementPropertiesWindowViewModel`
  už nekonstruuje `TableEditorDialog` (přes `Application.Current.MainWindow`) ani `OpenFileDialog` -
  nově callbacky `RequestOpenTableEditorDialog` + `RequestPickFile`, View je drátuje (MainWindow
  `OpenTableEditorDialog` + `_vm.ApplyTableEdit`, stejný vzor jako ostatní tabulkové editory). Odstraněn
  `using System.Windows` + nepoužitý `_tableTemplateProvider` (code-behind bere `_vm.TableTemplateProvider`).
  `CacheTemplatePickerDialogViewModel` už nekonstruuje `SaveSectionTemplateDialog` pro rename - callback
  `RequestRenameInput`.
- **C-c** - `CacheAgendaProvider` doplněn explicitní komentář-guard: metody jsou záměrně sync, pomalé
  operace (`GetData`, `GetAvailableInstances`) musí volající obalit `Task.Run` (R1).
- **C-d** - dangling odkaz na `tools/compare-gold.ps1` v smoke testu → odkaz na gold testy. Mrtvý stub
  `TemplateDesigner.Tests/Layout/RenderR2ToPngTest.cs` (untracked) k ručnímu smazání.
- **C-f** - `new-element-checklist.md` opraven odkaz `MainWindow/MainWindowViewModel.cs` → `TemplateDesignerViewModel.cs`.
- **Zbývá (vyžaduje rozhodnutí):** C-b (drift test PropertyDescriptor↔`*Edit.cls`), C-e (`agendaCode` „MHD" alias/migrace).

## 2026-07-07 - Gold regresní testy zastávkového JŘ (P-A z auditu v1)

Datová parita zastávkového JŘ nově krytá zmrazenými gold digest testy - pojistka „nerozbili jsme
vlastní výstup GetData". (Legacy-oracle parita = jiná věc, zůstává otevřená C1/C9.)

- **`ZastavkoveJrMhdGoldTest`** (nový) - MHD engine (dřív **0** gold testů) pokryt na CB_DP linkách
  1001/1003/1006. MHD data jsou multi-tenant (`^[dataDB]ALVA.MHD.LSO(IdFa,...)`) → gold přibitý na
  IdFa×NS CB_DP; pre-push runner jede právě pod CB_DP → gold reálně běží. Digesty vygenerovány z živých
  dat, determinismus ověřen (2× identický md5).
- **`GoldUtil.Digest()`** (nový, sdílený) - jedno místo výpočtu digestu (pages/zast/odj/pozn/md5) pro
  AD i MHD → nemohou divergovat (anti-drift, roadmap C8). AD gold přepojen z vlastní kopie `GoldDigest`.
- **AD gold zpřísněn** - dřív `digest=""` → `AssertTrue(1)` (tichá zelená i když gold linka zmizí).
  Nově: namespace chybí → legitimní skip; namespace **je** ale linka chybí → **FAIL** (rozbitý fixture).
- Úklid: dangling odkaz na neexistující `tools/compare-gold.ps1` v smoke testu → odkaz na gold testy;
  mrtvý stub `TemplateDesigner.Tests/Layout/RenderR2ToPngTest.cs` (untracked) k smazání.
- Ověřeno: `ALVA.TemplateDesigner.Tests` 30/30 green (vč. AD gold po refactoru - byte-identický).

**C1 - MHD 1:1 vůči legacy oracle ✅ HOTOVO (posun z „regrese" na „legacy parita", vč. pre-push gate).**
`ALVA.TemplateDesigner.Tests.MhdLegacyOracle` volá **skutečný legacy producent**
`ALVA.MHD.LinkyPrintFormatMHD.MakeData` (obchází padající formátovač `TISKMHD.Data.LoadData`) a **pozičně**
porovnává proti našemu `GetData`. `ZastavkoveJrMhdLegacyParityTest` = pre-push gate (skip jen když legacy
třída/namespace na serveru chybí).

- **CB_DP 1001/1003/1006: odjezdy + zastávky + poznámky 1:1** (poziční, 0 rozdílů). `ALVA.TemplateDesigner.Tests` 33/33.
- **Poziční klíče:** odjezd = zastávka × skupina-dnů (nadpis) × HH:MM; zastávka = název; poznámka = typ × znak × text.
  Ne line-global multiset - pins každý odjezd na správnou buňku.
- **Kontrakt `MakeData in`** minimální: `in("par",1)`=TiskProjetychZast, zbytek volitelný. „Neúplné `in()`"
  bylo o `LoadData`, ne producentovi.
- **Zjištění 1:** náš engine = legacy s **TiskProjetychZast=1** (tiskne projeté zastávky). `projete=0` legacy
  filtruje (832 vs 1543 na 1001) - náš engine ten toggle nemá (vazba na helpdesk A2-A6 #7959 „filtr neobsluhovaných").
- **Zjištění 2:** legacy ukládá speciální znaky jako surový ASCII kód (120/123), konvertuje až při renderu;
  náš engine v datech. Sjednoceno přes `ConvertSpecialniZnak` → shoda finálního glyfu zároveň ověřuje, že
  náš převod = legacy převod.
- **Zbývá (nice-to-have):** víc linek; AD analog (legacy oracle `$$WinTiskMhd^JRapi`, VolanoZ=JRMAIN).

## 2026-07-07 - Výběr skupiny linek v Instance Pickeru (AD + MHD)

InstancePicker dřív bral jen **default** skupinu (první nezablokovaná) → uživatel neviděl linky
z ostatních skupin (u MHD default často prázdná „pokusná"). Nově **searchable filtr skupin**.

- **Caché `GetAvailableSkupiny`** (nová metoda, vzor GetAvailableInstances): AD ze subscriptů `^JRL(sku)`,
  název = 1. `~`-piece hodnoty (např. „Vertikála k 1.1. - ŘÁDNÉ"); MHD z `ENUMS "SkupinyLinek"` (název
  `Nazev`, mimo `Blocked`); BaseAgenda default prázdné → agendy bez skupin (GENERAL/faktury) filtr skryjí.
- **`EDList.Instance`** (`LoadList`/`FetchInstances`) čte `filter","SKUPINA_LINEK_ID"` a předá do
  GetAvailableInstances - stejný rozšiřovací bod jako stávající `agendaCode` (žádný nový wire mechanismus).
- **.NET:** `AgendaSkupina(Value, Label)` POCO + `Display` = „číslo - název" (search najde podle ID i názvu,
  client-side); `CacheAgendaProvider.GetAvailableSkupiny`; Instance Picker dostal **searchable combobox**
  (filtr přes `ICollectionView`, ne Items.Clear → SelectedItem přežije) → výběr přenačte EDList s filtrem.
- **Propagace zvolené skupiny do GetData** (preview i export/tisk) - u AD je linka klíčovaná (skupina, linka),
  jinak by se fetchla z jiné skupiny. Status chip v hlavním okně „Skupina: číslo - název".
- **Vědomě odloženo (ne blocker):** skupiny pro knižní JŘ agendy (Fáze C+D); persistence zvolené skupiny
  per šablona (dnes session scope - `TryLoadInstanceDefaults` obnovuje z default skupiny).
- Build + 253/253 testů. Ověřeno na KV_DP (AD i MHD na jedné firmě).

## 2026-07-07 - Audit H3: ViewModel bez WPF (R4) - dialogy přes `IDesignerDialogService`

Poslední otevřený vysoko-prioritní audit bod. `TemplateDesignerViewModel` přímo instancoval WPF okna
(`new *Dialog(...) { Owner = Application.Current.MainWindow }` + `.ShowDialog()`), souborové dialogy
(`OpenFileDialog`/`SaveFileDialog`) a systémovou `Clipboard` → porušení R4, VM neunit-testovatelný.

- **Nový `Services/IDesignerDialogService`** - abstrakce View vrstvy: `ShowInstancePicker`/`ShowChangeAgenda`/
  `ShowCacheTemplatePicker`/`ShowSaveToCache` (nad dialogovým VM) + `PickOpenFile`/`PickSaveFile` + `SetClipboardText`.
- **`WpfDesignerDialogService`** (code-behind, drží owner okno) mapuje na konkrétní okna; napojeno v `MainWindow`.
- VM staví dialogové **ViewModely** (VM→VM, žádné WPF) a jen je nechá zobrazit; výsledek čte z dialogového VM.
- `using System.Windows` z VM pryč (zůstal jen `System.Windows.Input` kvůli `Key` enum v `MoveByKey`).
- VM je nyní testovatelný přes mock služby. Build + 253/253 testů. Detail → [`roadmap-archive.md`](roadmap-archive.md).

**Tím jsou všechny audit body H3–H6 + blokery hotové** - audit před produkcí uzavřen (H1/H2 přesunuty do Fáze C+D).

## 2026-07-07 - MHD zastávkový JŘ: B1 (dopravce z DB) + oprava crashe pickeru

**B1 (blocker) - pryč s KV hardcode v MHD tiskové cestě** (`ZastavkoveJrMhd.cls`):
- `POPIS_DOPRAVCE` - místo natvrdo „…Karlovy Vary, a.s.……." čte **reálného dopravce linky** z
  `ALVA.MHD.LSO` (helper `PopisDopravce`: `DruhDopravceID` → vlastní firma `$$gIdFa^MlJFapi` /
  cizí `$$gDopFa^JRapi`, 1:1 vzor MHD2 Export; defensivní try/catch → prázdno při selhání).
- `NEJEDOU_SPOJE` - napojen na generátor `TiskyFce.CalculatePrvniAndPosledniSpojeText` (gated flagem
  linky `GenerovatNEJEDOU_SPOJE`).
- `TITULEK` / `POZNAMKA_POD_DOPRAVCE` / `INFORMACE` - MHD pro ně nemá zdroj → **odstraněny ze
  schématu i GetData** (per-agenda poctivé schéma; AD si je nechává, tam data jsou). Schéma ověřeno živě.

**Oprava crashe MHD instance pickeru** (pre-existující, nesouvisí s B1):
- `<UNDEFINED>err` - `GetAvailableInstances` catch bez inicializovaného `err` (`$$$CatchErrRest` ho čte).
  Init `s err=""`. Širší latentní vzor napříč agendami → finding H6.
- Throw „Žádná aktivní skupina linek" i pro legitimní „firma bez MHD dat" → error popup. Teď **prázdný
  picker** místo chyby (přepnutí na MHD u AD-only firmy nevyhazuje popup). Ověřeno (`[]`, ok:true).

Součástí commitu i M1 (guard prázdného `TypProvozuID`/`SmerID` v `PromitnutSpoje`, z auditu).

## 2026-07-07 - AD zastávkový JŘ: oprava hlaviček variantních časových sloupců

Fix tří anomálií ve variantních časových sloupcích (`TRASA_MIN`) zastávkového JŘ z AD dat
(`ZastavkoveJrAd.cls`), nalezených při testování na lince 0365005:

- **Fallback hlavičky „ZT" → „T1"/„T2"…** - varianta bez písmene rozdílu trasy dřív spadla na „ZT"
  (základní trasa); víc takových variant → „ZT ZT ZT" (kolize). Teď inkrementální „T1"/„T2".
- **Dedup identických sloupců** - signatura varianty se počítá jen přes **zobrazené zastávky**
  (`servedPoradi`) místo celé W-mapy. Dvě varianty lišící se MIMO tuto vývěsku dřív daly dva
  identicky vypadající sloupce („C C", „E E"); teď se sloučí.
- **Unikátní hlavičky při kolizi písmene** - dvě různé varianty se stejným písmenem rozdílu trasy
  (liší se jen časy) dřív obě „B" s jinými hodnotami; teď „B1"/„B2". Unikátní písmeno zůstává „B".
- Gold `TestGold0365001`/`TestGold0365005` re-pinnuté (změnil se jen md5 - pages/zast/odj/pozn
  identické). Caché testy 27/27. Ověřeno v preview (0365005: `B B C C E E` → `B1 B2 C E`).

## 2026-07-07 - DynamicTable = varianta A (jako zastávky); R2 (řádkové stránkování) odstraněno

Po revizi konzistence (viz níže „⚠️ zjištění"): DynamicTable se má chovat **stejně jako zastávky**
(`ScheduleElementBase`) - **`Height` je strop**, obsah se do něj vejde auto-scalem, prvek se
NErozrůstá přes stránky. Vícestránkové řádkové stránkování s opakovaným záhlavím (R2) se ukázalo
jako nekonzistentní se zbytkem návrháře (statické prvky mají výšku = strop) a **vědomě odloženo** -
přidá se zpět jen na konkrétní poptávku, čistě, ne jako poloviční řešení.

- **Odstraněno kompletně (žádný mrtvý kód):** `IRowPaginatedElement`, `PageLayoutEngine.LayoutRowPaginated`
  + `RowPaginatedElement`, `PlacedSection.StartRowUnit/EndRowUnit/RepeatHeader`, R2 větev v `PreviewCanvas`,
  `DynamicTableElement.RenderRowWindow` + `RowUnitCount/HeaderBandHeightMm/RowUnitHeightMm`, testy R2.
- **Zůstává:** auto-scale do pevné Height (jako zastávky), dynamická výška (`Height=null`),
  pixel-ořez u „sekce vyšší než stránka" (beze změny).
- **Oprava rámečku:** vnější rámeček tabulky se vsazuje dovnitř o půl tahu (`InsetForStroke`), aby ho
  těsný clip sekce neukrojil na hraně (dřív poloviční/neviditelná horní/pravá/dolní hrana).
- Testy 214/214.

> Předchozí R2 záznamy (2026-07-06 níže) jsou tímto **superseded** - feature byla přidána a po
> validaci konzistence zase odstraněna, aby kód šel do produkce čistě.

## 2026-07-06 - Fix(R2): stránkování i pro odsazenou tabulku (Y > 0)

- 🐛 **R2 se nespouštělo, když byla tabulka odsazená od vrchu sekce** (uživatel ji posunul na
  Y=160.7 „aby přetékala"). Trigger vyžadoval `Y ≤ 20`. Ověřeno proti reálně uloženému JSONu.
- **Fix:** Y offset se pro řádkové stránkování **ignoruje** - paginující tabulka plní stránky od
  vrchu sekce (`RenderRowWindow` kreslí od 0, ne od `element.Y`). R2 tak funguje bez ohledu na pozici.
- Ověřeno render-do-PNG na přesné uživatelově konfiguraci (Y=160.7, výška prázdná, řádek 10, 36 zastávek):
  strana 2 = **zopakované záhlaví + řádky 29–36**. R2 potvrzeno end-to-end. Testy 218/218.

## 2026-07-06 - Fix: dynamická DynamicTable se v renderu ořezávala na ~40mm placeholder

- 🐛 **Dynamická (Height=null) DynamicTable ukazovala v preview/PDF jen ~5 řádků, zbytek uříznutý.**
  Kořen: `Render` používal pro clip `EffectiveDesignerHeight` (= placeholder ~40mm), místo skutečné
  výšky z dat. Ověřeno render-do-PNG: 36 řádků → viditelných jen 5.
- **Fix:** `tableHeight = Height ?? (ctx.Data is not null ? CalculateHeightFromData(ctx.Data) :
  EffectiveDesignerHeight)` - dynamická tabulka s daty se kreslí na plnou výšku obsahu. Po opravě
  render ukáže všech 36 řádků.
- Souvisí s R2: kvůli tomuto bugu nešlo R2 vyzkoušet (dynamická výška se ořízla, pevná výška ≤ stránka
  zase blokovala trigger - viz předchozí fix). Teď: dynamická tabulka s obsahem &gt; stránka → R2
  (row-break + opakované záhlaví, ověřeno render-do-PNG); s obsahem ≤ stránka → celá na jedné stránce.

## 2026-07-06 - R2: svislé stránkování řádků (DynamicTable) - láme po řádcích + opakuje záhlaví

- ✅ **Dlouhá tabulka se láme mezi celými řádky (nikdy vejpůl) a záhlaví se opakuje na každé stránce.**
  Nahrazuje dřívější pixel-ořez sekce (uřízl řádek + hlavička jen na 1. straně).
- **Rozhraní `IRowPaginatedElement`** (`Models/Layout/`): `RowUnitCount` / `HeaderBandHeightMm` /
  `RowUnitHeightMm` (mm, čistá logika) + `RenderRowWindow`. Implementuje **DynamicTable** (jednotka =
  datový řádek; souhrnné řádky = koncové jednotky; záhlaví = opakovaný band).
- **Engine** (`PageLayoutEngine.LayoutRowPaginated`): pro Split/AllowSplit sekci s jedním
  `IRowPaginatedElement` greedy plní jednotky do oken stránek (aspoň 1/stránku), `PlacedSection` nese
  `StartRowUnit`/`EndRowUnit`/`RepeatHeader` místo pixel-clipu. Fallback = dnešní pixel-ořez (nic nerozbito).
- **Renderer** (`PreviewCanvas.RenderPage`): u row-window PlacedSection volá `RenderRowWindow` (header +
  okno řádků + vlastní rámeček) místo clip-offsetu. Sdílený `RenderPage` → platí i pro PDF/PNG export.
- Trigger v reálné appce: **dynamická** (Height=null) DynamicTable naroste s obsahem → sekce přesáhne
  stránku (i výchozí SplitAcrossPages) → stránkuje po řádcích. Zebra/zvýraznění dle absolutního indexu
  řádku (spojité přes stránky). StopList/LineTable navážou (zatím jen DynamicTable).
- Testy 215/215 (`RowPaginationTest` - 50 řádků → souvislá okna, opakované záhlaví, chunky se vejdou).

## 2026-07-06 - Review fixes layout tracku (footer WYSIWYG + mrtvý kód)

- **Footer kotven ke spodku rostoucího papíru** (`GetSectionOrigins`) místo „za tělem" - u jednostránkové
  šablony tak patička v návrháři sedí s preview/tiskem (dole), u vysokého obsahu vyjde hned za tělem.
  Opravuje WYSIWYG regresi z Slice 1 (krátké tělo → patička dřív plavala uprostřed). Render i hit-test
  konzistentní (obojí přes GetSectionOrigins).
- Odstraněn mrtvý `DesignerCanvas.CalculatePageHeightPx(Template)` (nahradilo `TotalPaperHeightPx`).
- Nezávislý review tracku „sekce vyšší": žádný BLOCKER/MAJOR; ověřeno bez regrese jednostránkových
  šablon, hit-testing konzistentní, border-fix správný i pro PDF/PNG, bez rekurze v refreshi. Testy 214/214.

## 2026-07-06 - Fix: dvojité vykreslení rámečku u split sekce (preview/PDF/PNG)

- 🐛 **Sekce vyšší než stránka (SplitAcrossPages) kreslila rámeček elementu i na druhé straně.** Kořen:
  `PreviewCanvas.RenderPage` druhý průchod „rámečky mimo clip" běžel **zcela bez clipu**, takže u split
  chunku (kde má `ctx` origin posunutý o `ClipTop`) prosákl rámeček elementu z jiného chunku na tuto
  stránku. Nové elementy mají default rámeček → „dvojité vykreslení". Engine dělil správně (ověřeno).
- **Fix:** border-pass ořezán na **rámec stránky** (ne úplně bez clipu) - rámečky na hranici sekce
  zůstávají vidět, ale elementy z jiného chunku (posunuté mimo stránku) se ořežou. Jeden `RenderPage`
  = sdílený preview + PDF (`RenderMultiDataToPdf`) + PNG → opraveno všude. Pre-existing bug (Slice 1
  ho jen zpřístupnil). Testy 214/214 (nový `SplitSectionPaginationTest` na strukturu splitu).

## 2026-07-06 - Layout track / „sekce vyšší než stránka" - Slice 2: dělící čáry stránek

- ✅ **`DesignerCanvas.DrawPageBreakLines`** - přerušované modré čáry + popisek „Strana N" přes souvislé
  plátno, po výšce tisknutelné oblasti (`PageSize.Height − okraje`). Kreslí se jen když obsah přesahuje
  jednu stránku. Orientační vodítko „kudy půjde zlom" při editaci.
- **Varianta 1 (geometrické), vědomě NE pixel-přesné** vůči preview - nezná opakování hlaviček/patiček
  ani `KeepTogether`. Přesný stránkovaný výsledek je a zůstává v preview/PDF. (Varianta 2 „přesně dle
  enginu" zamítnuta - návrhář ukazuje autorské výšky, engine datové → stejně by neseděly.)
- Build 0 chyb (canvas = UI, ověřuje se vizuálně). Tím je track „sekce vyšší než stránka" hotový;
  navazuje R2 (svislé stránkování řádků - zpřesní i tyto zlomy).

## 2026-07-06 - Layout track / „sekce vyšší než stránka" - Slice 1: plátno roste

- ✅ **Návrhové plátno už není omezené na jednu A4.** `DesignerCanvas.GetSectionOrigins` skládá sekce
  souvisle shora dolů v jejich **pravé (neclampnuté) výšce**, bez zahazování za přehybem; hlavička+tělo
  v pořadí, patičky za tělem (autorský pohled - per-stránkové opakování řeší preview/engine).
- Nové `TotalPaperHeightPx` (horní okraj + Σ výšek sekcí + spodní okraj, min = 1 stránka); `Render`
  kreslí papír/rámeček/okraje/grid/pravítka na tuto rostoucí výšku. `UpdateCanvasSize` + `RequestCanvasRefresh`
  přepočítávají velikost plátna po každé změně (sekce/element).
- `GetClampedSectionHeightPx` → `SectionHeightPx` (pravá výška); hit-testing i render jedou přes tentýž
  zdroj → automaticky konzistentní. Odstraněn single-page strop `GetPageBottomY`.
- **Zatím bez dělících čar** (Slice 2) - plátno roste souvisle. **Preview/PDF beze změny** (už multi-page).
  Build 0 chyb, testy 213/213 (canvas = UI, ověřuje se vizuálně).

## 2026-07-05 - B1: embedding fontů do .exe (mechanismus, R5)

- ✅ **`FontResolver` embedding mechanismus** - `.ttf`/`.otf` hozené do `dotnet/Fonts/` se glob-em
  v .csproj zabalí jako `<EmbeddedResource>` do assembly (a single-file .exe), `FontResolver` je
  **auto-načte** z manifest resources podle názvu rodiny (bez úpravy kódu). Priorita
  `embedded → OS → Arial → Skia default`; embedded má přednost = deterministický výstup nezávislý
  na nainstalovaných fontech (důležité pro headless/server). Varianty (Bold/Italic) = samostatné
  soubory, vybírá se nejbližší dle stylu.
- Refaktor `FontResolver.Resolve` na **sdílené cachované typefacy** (app-lifetime, nedisposovat) -
  všechny cesty renderu (text/buňky/JŘ) přes něj už tečou, takže embedding platí všude. Bonus: mizí
  dřívější re-`FromFamilyName` per volání. `InstalledFamilies` = OS ∪ embedded (i v UI výběru fontu).
- `dotnet/Fonts/README.md` + `.csproj` komentář: **licence** - jen redistribuovatelné fonty
  (OFL/Apache - Arimo, Liberation Sans), NE komerční (Helvetica/Arial). Základní text je nepotřebuje
  (OS Arial). **Fonty samotné nepřidány** - mechanismus je připravený, uživatel dropne font do Fonts/.
- JŘ symbolový font (Timetable, C2) až po vyjasnění zdroje/licence. Testy 213/213 (4 nové - resolve/cache/fallback).

## 2026-07-05 - A3: min/max řádků DynamicTable + klasifikační pravidlo featur

- ✅ **`DynamicTableElement.MinRows` / `MaxRows`** - `MinRows` doplní prázdné řádky do minima (pevná
  výška boxu položek, např. faktura - součet/patička sedí na stejném místě bez ohledu na počet
  položek); `MaxRows` ořízne přebytečné datové řádky. 0 = vypnuto. Render rozlišuje 3 typy řádků
  (návrhový `{POLE}` placeholder / datový / prázdný padding); prázdné padding řádky drží mřížku,
  rámeček i zebru, bez zvýraznění. Výška↔render parita (`EffectiveRowCount` = Max ořez + Min padding),
  kombinuje se s filtrem viditelnosti. DynamicTable-only (kategorie 2, viz níže).
- 📐 **Klasifikační pravidlo featur** zapsáno do `elementy-architektura.md §0b` - rozhodnutí z
  diskuze 2026-07-05: (1) featury nad daty řádku → sdílený helper do všech tabulek; (2) struktura
  sloupce / počet řádků → jen generická DynamicTable (počítaná pole, min/max); (3) doménové
  renderování → element-specifické. Generické featury jsou pro daná data dostupné přes DynamicTable
  napojenou na tutéž kolekci (StopList = volba „doménový vzhled", ne „featura nejde"). NEslučovat
  StopColumn/TableColumn (R1).
- Testy 209/209 (4 nové - min padding, max ořez, kombinace s filtrem). Caché schema zkompilováno.

## 2026-07-05 - A2 review fixes

- **MAJOR:** `EvaluateToString` číslo teď přes `FormatNumber` - zaokrouhlení na 10 des. míst
  (odstraní IEEE-754 artefakty `0.1*3 → 0.30000000000000004`), oříznuté koncové nuly, invariantně.
  `NaN`/`±Infinity` → prázdno (nešíří „NaN" do buňky). Bez `Format` tak počítaný sloupec neukáže
  plovoucí smetí - důležité pro peníze/množství.
- Konkatenace v `+` (nečíselné) formátuje případný double invariantně (ne CurrentCulture).
- NIT: statický `TableEditorDialog` round-trip zachovává `Expression` (konzistence, dormant dialog).
- Nezávislý review: žádná regrese existující viditelnosti, priorita/asociativita/fail-soft ověřeny.
  Ponecháno vědomě: `ToDouble` = `NumberStyles.Any` (pre-existing, sdílené s Compare - tightening
  by riskoval regresi viditelnosti; data jsou invariantní). Testy 205/205 (2 nové - FP, NaN/Inf).

## 2026-07-05 - A2: počítaná pole (aritmetika ve výrazovém jádru + počítaný sloupec DynamicTable)

- ✅ **Aritmetika ve výrazovém jádru** - `ExpressionParser` rozšířen o `+ - * /`, unární minus,
  prioritu (`* /` > `+ -` > porovnání) a závorky (už byly). Nové gramatické úrovně additive/mult/unary.
  `ExpressionEvaluator`: `+` (čísla → součet, jinak konkatenace), `- * /` (nečíselný operand nebo
  dělení nulou → null = prázdná buňka), unární `-`. Funguje i v podmínkách (`{A}*2 > 10`).
- ✅ **`ExpressionEvaluator.EvaluateToString`** - hodnotové vyhodnocení výrazu (číslo invariantně,
  ať navazující `Format` přeparsuje dle kultury); prázdný/chybný výraz → prázdno (fail-soft).
- ✅ **`TableColumn.Expression`** - počítaný sloupec DynamicTable: výraz nad poli řádku
  (`{CENA} * {POCET}`), přebíjí `Field`, výsledek formátovatelný přes `Format` (např. „N2").
  Editace ve Sloupce tabu (nové pole „Výraz" + „Formát").
- 🐛 **Fix při této příležitosti:** editor sloupců DynamicTable dosud `Format` vůbec neukládal
  (ToModel/FromModel ho vynechávaly → při editaci sloupce se ztrácel). Teď round-trip včetně Format.
- Testy 203/203 (9 nových - aritmetika, priorita, dělení nulou, konkatenace, round-trip sloupce).
  Bez Caché změny (sloupce = WPF dialog, ne EDInputForm).

## 2026-07-05 - Review fixes podmíněných řádkových featur

- **DynamicTable:** placeholder `{POLE}` řádky se kreslí **jen v návrháři** (`ctx.Data is null`);
  s DataContextem (preview/tisk) - i když je vše odfiltrované nebo kolekce prázdná - se vykreslí
  reálně prázdná tabulka, ne fake řádky v tiskovém výstupu. Výška konzistentní (0 řádků). Opravuje
  pre-existing kvartét, který row-visibility filtr zpřístupnil.
- **LineTable:** sjednocen výpočet přípojných řádků ve výšce (prázdná i aktivní větev teď počítají
  jen pripoje matched na `Poradi` viditelné zastávky - přesně jako Render); odstraněna asymetrie
  a nepoužitá `PripojeRowCount`. Výška↔render parita ve všech případech.
- Review (nezávislý): žádný BLOCKER/MAJOR, invarianty (behavior-preserving, parita, index zarovnání,
  cache/SourceRow, precedence, fail-open) ověřeny. Zbývá NIT: `SKColor.Parse` na nevalidní barvě =
  latentní cross-cutting risk (sdílený s OddTcBackColor/AlternatingRowColor/…), řešit případně
  sdíleným TryParseColor napříč elementy. Testy 194/194.

## 2026-07-05 - Viditelnost řádku na StopList + LineTable + discoverability polí

- ✅ **Viditelnost řádku rozšířena na StopList + LineTable** (`RowVisibilityExpression`) přes sdílený
  `RowConditions`. StopList: skrytá zastávka se nevykreslí (skip po určení current/passed, ať se
  nerozbije sledování). LineTable: skip **in-place** (iterace přes plný index `si` → zarovnání časů
  spojů `Casy[si]` zůstává; skryje se řádek zastávky i její přípojné řádky).
- **Výška je guarded** - při prázdném výrazu původní kód beze změny (behavior-preserving pro gold-1:1
  StopList i pilot LineTable); při aktivním výrazu se počet řádků spočítá **přesně jako v Render**
  (parita výška↔render): StopList mirror logiky projetých zastávek, LineTable jen viditelné zastávky
  + jejich přípojné řádky.
- **Discoverability polí (dotaz uživatele):** helpText podmínkových polí teď vypisuje dostupné názvy
  polí řádku - StopList (POPIS, TC, PORADI, NA_ZNAMENI…), LineTable (POPIS, TC, ZONA, MHD, TYP…),
  DynamicTable („názvy = mapování Field ze sloupců"). Panel „Pole agendy" nadále ukazuje jen skalární
  pole (kolekční řádková pole tam vědomě nejsou - draggable by mátla).
- Testy 193/193 (beze změny - nová logika default-off). Caché StopList/LineTable/DynamicTable schema
  zkompilováno.

## 2026-07-05 - Podmíněné zvýraznění řádku: sdílený `RowConditions` + rozšíření na StopList + LineTable

- ✅ **`Models/Expressions/RowConditions.cs`** - sdílená řádková podmínková logika (výraz nad daty
  jednoho řádku → viditelnost / barva zvýraznění pozadí) nad `ExpressionEvaluator` (fail-open).
  R1 filozofie: sdílíme **algoritmus**, ne datovou třídu - každý element drží svá 2 pole
  (`RowHighlightExpression` + `RowHighlightColor`) i svůj řádkový POCO, do helperu posílá jen
  DataContext řádku.
- ✅ **Zvýraznění (podbarvení) rozšířeno na StopList + LineTable** - řádek/zastávka splňující podmínku
  dostane pozadí. StopList: priorita nad podbarvením aktuální zastávky. LineTable: celá šířka řádku
  zastávky. Per-řádek DataContext nese nové `[JsonIgnore] SourceRow` na `ZastavkaData`/`LineStopData`.
- **Zvýraznění je aditivní (jen pozadí) → žádná interakce s výškou** → default vypnuto = **nulová změna**
  → bezpečné pro gold-1:1 StopList i pilot LineTable (behavior-preserving při prázdném výrazu).
- DynamicTable přepnut na sdílený helper (dřív měl vlastní inline logiku). UI: pole ve skupině
  „Podmínky" (EDInputForm) u všech tří + zrcadlící PropertyDescriptory.
- **DepartureGrid vědomě mimo** - je pivot (hodina × skupina dnů), „řádek" = agregace odjezdů, ne
  jeden záznam; per-odjezd barvu řeší prezentační nápovědy `FORE_COLOR`/`BACK_COLOR` z dat.
- **Viditelnost řádku** zůstává zatím jen v DynamicTable (na StopListu se proplétá s logikou projetých
  zastávek a výškou - vědomý další krok, ne teď).
- Testy: 193/193 (7 nových - RowConditions helper). Caché StopList/LineTable schema zkompilováno.

## 2026-07-05 - DynamicTable: viditelnost řádku (`RowVisibilityExpression`) - PoC podmíněného renderingu

- ✅ **`DynamicTableElement.RowVisibilityExpression`** - výraz nad daty řádku (`{CENA} > 0`);
  nesplněné datové řádky se nevykreslí a **nezapočítají** (výška i agregace v souhrnných řádcích).
  Analogie element-level `VisibilityExpression`, jen per řádek. Prázdné = všechny řádky.
- Vyhodnocení přes sdílený `ExpressionEvaluator.IsVisible` proti DataContextu řádku (fail-open).
  Jednotný helper `VisibleDataRows` filtruje před render/výška/souhrn → skryté řádky se nesčítají.
- UI: pole v tabu „Obecné" (EDInputForm `DynamicTableElementEdit.cls`, skupina „Podmínky", pod
  viditelností elementu) + zrcadlící PropertyDescriptor. Binder = reflexe (žádná extra vazba).
- Testy: 183/183 (3 nové - filtr do výšky, prázdný výraz, vše skryté → náhledová fallback větev).
- **PoC pro navazující podmíněné formátování** (obarvení řádku dle podmínky) - to bude seznam
  pravidel (podmínka→barva) přes sdílený helper, ať ho vezme i StopList/LineTable.

## 2026-07-04 - R1: sdílené tabulkové jádro `TableLayout` (extrakce hotová)

- ✅ **`Rendering/TableLayout.cs`** - sdílené algoritmy řádkových tabulek nad `CellRenderer`/
  `ColumnRenderer`: `ResolveWidths` (pevné + auto-split zbytku + mm/%) a `ResolveCascade`
  (kaskáda column→element→baked + legacy alignment fallback). **Záměrně nesjednocuje datovou
  třídu sloupce** - StopColumn i TableColumn zůstávají, elementy jen promítnou neutrální tvar
  (`ColWidth`, per-column TextStyle) → nulové riziko pro uložené šablony.
- ✅ **Napojeno do DynamicTable, LineTable, StopList** - kaskádový vzorec 3 kopie → 1, výpočet
  šířek 2 kopie → 1 (StopList mm-only, DynamicTable mm/%; LineTable šířky ponechány - Km-expanze +
  trip-pool jsou element-specifické, migrovaná jen kaskáda). Pátá tabulková sestava dostane
  šířky+kaskádu zdarma.
- **DepartureGrid mimo jádro** (potvrzeno analýzou - je pivot hodina × skupina dnů, ne řádková
  tabulka; sdílí jen datovou/nastavovací vrstvu).
- Behavior-preserving: **180/180 testů** (vč. StopListCascadeTests/DepartureGridCascadeTests) +
  render diff LineTable AD (0320680) i MHD identický. Analýza + návrh:
  `.claude/docs/r1-sdilene-tabulkove-jadro-navrh.md`.
- Zbývá volitelně (R1a): render-loop orchestrátor (header+řádky+separátory jako 1 metoda) -
  DynamicTable dnes kreslí vlastní per-buňka smyčkou; nízká hodnota / vyšší riziko, odloženo.

## 2026-07-04 - LineTableElement: kolekce sloupců + 2-tab dialog (Obecné + Sloupce)

- ✅ **`LineTableElement.Columns : List<StopColumn>`** - element přešel z pevných `Show*Column`/
  `*ColumnWidth` properties na sdílený `StopColumn` model (klíče Zone/Km/Tč/StopName/Mhd + „Trip"
  pro časové sloupce spojů). Render iteruje viditelné sloupce v pořadí, resolvuje per-column
  header/data styl přes kaskádu `column.Style → element → CellDefaults` (`ResolveHeaderStyle`/
  `ResolveDataStyle`, vzor StopList), Km auto-expand na N sub-sloupců, MHD auto-remove když prázdné,
  auto šířky (null) sdílí zbytek se spoji. Render výstup beze změny (ověřeno na 0320680 i CB_DP 1001).
- ✅ **2-tab dialog `LineTableColumnsEditorDialog`** (Obecné = EDInputForm `LineTableElementEdit`,
  Sloupce = sdílený `ColumnListEditor` + `CellStyleEditor` + `LineTableColumnVM`) - dvojklik/Enter
  na LineTable teď otevře stejný dvoutabový editor jako StopList/DepartureGrid: reorder ▲▼,
  viditelnost, šířky, per-column styl záhlaví/dat, overflow warning. Konzistentní se sloupcovým API
  ostatních tabulkových elementů. EDInputForm schéma zbaveno sloupcových polí (přesunuta do tabu).
- Pozn.: přesně na architektuře R1 - sdílený column model + `ColumnListEditor`/`CellStyleEditor` je
  ta část, která je společná; extrahuje se až samotný layout algoritmus (později).

## 2026-07-04 - Knižní JŘ z MHD dat: agenda KnizniJrMhd

- ✅ **`ALVA.TemplateDesigner.Agendy.KnizniJrMhd`** (`KNIZNI_JR_MHD`) - MHD analogie `KnizniJrAd`
  (legacy větev `VolanoZ="MHD"` = `ALVA.MHD.LinkyPrintFormatJR.MakeData`): stejný knižní wire
  formát → konzumuje tentýž `LineTableElement` (kontextová paleta `KNIZNI_*` už platí). Data z
  `ALVA.MHD.LSO/ZST/ENUMS` + helpery `*Fce` (MakeTableSpoj, GetNazevHrl, GetPoznamkySpoj…):
  trasa s filtrem kategorie 1001 + slučování sousedních shodných zastávek, zdvojené řádky
  odjezd/příjezd (atribut trasy 1011), zóny přes IDSID match, skupiny = TypProvozu (nadpisy
  z ENUMS NadpisSMOPrvni/Druhy, řazení dle Poradi), markery `|` projíždí / `<` jede jinudy,
  NZ + auto-poznámkování rozdílů platností (GetJede/MakeOmezeniTXT), spec. znaky a atributy
  B→BEV/C→CBE v řádku platnosti. Bez Km/Tč/MHD sloupců a vyčká/navazuje (MHD zdroj nemá, 1:1).
  Ověřeno na CB_DP lince 1001 (29 zastávek × 159 spojů × 3 typy provozu, oba směry + render PNG).
- Pozn.: gold-oracle diff pro MHD odložen - legacy `LoadData` MHD větev na dev serveru padá
  (ztrap po MakeData); ověření zatím strukturální + vizuální. Bin-packing (159 sloupců → bloky)
  = krok 43 layout rozšíření.
- ✅ **EDInputForm pro LineTableElement** - `ALVA.TemplateDesigner.EDInputForm.LineTableElementEdit`
  (skupiny Sloupce/Řádky/Záhlaví/Datové buňky/Rámeček/Pozice/Pokročilé) + registrace v
  `EDInputFormSchema` → Enter/dvojklik otevře standardní form-driven dialog „Vlastnosti knižního JŘ"
  a inline panel jede na tomtéž schématu (konzistentní s ostatními elementy). Per-column tab
  „Sloupce" (ColumnListEditor) přijde až s R1 extrakcí jádra, až element dostane column kolekci
  (per-spoj overridy) - v prvním řezu jsou popisové sloupce pevné properties (vzor NotesLegend).

## 2026-07-04 - Knižní/linkový JŘ: agenda KnizniJrAd + LineTableElement (roadmap kroky 42/43/44 - první implementace)

- ✅ **Caché agenda `ALVA.TemplateDesigner.Agendy.KnizniJrAd`** (`KNIZNI_JR_AD`) - čistá reimplementace
  datového jádra legacy `mkPLinWin^JRapiLinPrtWin` nad `^JRL`/`^JRZ`/`^JRSET`: 1 stránka per linka×směr,
  sémantický JSON wire (ZASTAVKY svislá osa vč. zdvojených řádků příjezd/odjezd, SPOJE sloupce s CASY
  markery `|`/`<`, plný řádek platnosti X..+/místenka/u/p/atributy, PRIPOJE s gate typVýstupu/typZobrazení,
  SKUPINY_DNU z WinMHD DSC, POZNAMKY druhů 1-10 z NCOME/STALCOME, TiskDtm režimy datumů, mkPoradiSp
  řazení, zóny jen u IDS linek, MHD sloupec s auto-remove).
- ✅ **`LineTableElement`** - nový standalone element (ScheduleElementBase): matice zastávky × spoje,
  pás skupin dnů, řádky platnosti/NZ, přípojné řádky (RADEK=1), zdvojené podřádky, MHD/zóna/km/Tč
  sloupce s auto šířkou spojů, podbarvení lichých Tč.
- ✅ **Ověření 1:1 proti gold oracle** (`ALVA.TISKJR.Data/LoadData`, `VOLANO_Z~JRMAIN`): multi-linkový
  diff harness - **22/24 linek napříč CB_CSADAD/CB_GWBUS/KV_DP/VS_CSAD zcela beze změn**; zbylé
  odchylky kosmetické a zdokumentované (`linkovy-knizni-jr-implementace-analyza.md` §11-12).
- ✅ **Výchozí šablona** `templates/knizni-jr-ad-vychozi.json` (hlavička, JŘ tabulka, legenda poznámek).
- Vědomě odloženo: symbolový font Timetable (krok 42b), 2D bin-packing/Strih (layout řeší TD),
  turnusy, obousměrný tisk, konfigurovatelné pořadí poznámek.
- ✅ **Kontextová paleta JŘ elementů dle agendy** - `UpdatePaletteForAgenda()` (napojené na
  RequestTitleUpdate, který se volá při každé změně agendy/šablony): agendy `ZASTAVKOVY_*` ukazují
  StopList/DepartureGrid/NotesLegend, `KNIZNI_*` ukazuje LineTable (nové tlačítko palety
  `BtnPalLineTable`), agenda bez JŘ kontextu (FAKTURY/GENERAL) ukáže vše. Stejný filtr v menu
  „Přidat element". Instance picker pro `KNIZNI_JR_AD` posílá `LINKA_ID` (InstanceParamName).

## 2026-07-03 - Wave 4 W4-10: inline panel na EDInputForm (Wave 4 uzavřena)

- ✅ **Levý panel „Vlastnosti" hostuje EDInputForm** pro jeden vybraný element (všech 9 typů přes
  `*Edit.cls` schema + `ElementFormBinder`) místo PropertyDescriptorů. Sjednocuje „dvojí vizuální jazyk"
  - panel i modály jedou na stejném form-driven UI. Nový `Controls/InlinePropertiesForm` (embedded host).
- ✅ **Živý write-back:** nový `EDInputForm.FieldChanged` event / Enter → `ElementFormBinder.ExtractCommands`
  → `SetPropertyCommand` (undo) → canvas. Enter v poli commitne pole (ne otevře dialog); bare klávesy
  (Delete/šipky) patří poli (+ fix latentního bugu: Delete v poli mazal vybraný element).
- ✅ **EDControls (additivní):** `FieldChanged` event; `CacheClass` setter zahodí controller při změně →
  runtime přepnutí schématu naloadí správnou třídu (dřív držel první → StopList ukazoval textová pole).
- **Wave 4 tím funkčně uzavřena** (form-driven UI všude, kde uživatel přímo edituje). Záměrně nedokončeno:
  W4-11 (smazat PropertyDescriptory) neproveditelné - drží je FormatToolbar + multi-edit + fallback modál
  + AI CapabilityCatalog (paralelní systém); W4-12 (wire `$lb()`→JSON, collapsible groups) volitelné.
- Multi-select + FormatToolbar + AI catalog beze změny (na PropertyDescriptorech). Testy: TD 180/180, EDControls 331/331.

## 2026-07-03 - T4 malé kousky (R4 fix + rename) + StopList kaskáda testy

- ✅ **R4 fix - `TableEditorDialogViewModel` bez WPF.** Inline WPF picker šablon tabulky (Window + ListBox
  + Buttons stavěný ve ViewModelu) přesunut do code-behind `TableEditorDialog.xaml.cs` přes callback
  `RequestPickTableTemplate` (VM zná jen výsledný klíč). VM je teď 0× `System.Windows` (R4). Behavior-preserving.
- ✅ **Rename `MainWindowViewModel.cs` → `TemplateDesignerViewModel.cs`** - soubor teď odpovídá názvu třídy
  (`git mv`, obsah beze změny, SDK globbing).
- ✅ **`StopListCascadeTests` (9 testů)** - regresní pokrytí StopList-specifického cascade wiring: feature
  override aktuální/projeté zastávky (`ResolveRowStyle`), legacy `col.Alignment` fallback jen když kaskáda
  HAlign nenastaví, per-column `Style` priorita, element-level `HeaderStyle`. `ResolveHeaderStyle/DataStyle/
  RowStyle` zpřístupněny `public` (stejný vzor jako DepartureGrid Resolve*). Tím je uzavřen **malý T track**
  (T5a, T6, T4 R4+rename, StopList testy); velké T1b-robustní + plný T4 strangler zůstávají záměrně.

## 2026-07-03 - T5a: agenda kód→třída lookup bez hardcoded mirroru

- ✅ **`AgendyRegistr.GetClassForCode(agendaCode)`** - nový sdílený lookup (auto-discovery z `%Dictionary`,
  stejná cesta jako `ListAgendy`). `EDList/Instance.GetClassForAgenda` dřív hardcoduje mapování kód→FQN
  třídy (mirror Wave 2) → teď deleguje na `GetClassForCode`. Přidání nové agendy = jen nová `BaseAgenda`
  subclass, žádný druhý seznam k údržbě. Ověřeno: `ListAgendy` (stejný mechanismus) vrací správné mapování
  pro AD/MHD/FAKTURY.
- ⏭️ **T5b (`ZastavkoveJrAd.GetMockData` → statický mock) přeskočeno** - živý náhled z reálné linky
  ponechán záměrně (není zásadní; na rozdíl od MHD/Faktury zůstává AD design-time preview z živých dat).

## 2026-07-03 - T6 dokončeno (architektonický track drobnosti)

- ✅ **`architecture.md` doplněn o `/Services` (6 souborů) a `/PdfImport` (12 souborů)** - dřív v mapě
  složek chyběly (~7k LOC neviditelných v doc). Doplněn stručný popis každého souboru.
- ✅ **Per-frame `new SKPaint` v Render() běžných elementů - posouzeno a zavřeno** (nebude se dělat):
  painty jsou disposované (`using var`), per-element (ne per-buňka, jen 1–5/element/repaint), a závislé
  na měnitelných barvách elementu → cache by vyžadovala invalidaci za zanedbatelný zisk. Schedule elementy
  s 100+ buňkami painty pre-createují už dnes. Tím je **T6 celé hotové**.

## 2026-07-03 - StatusText segmentace: „stav výběru" × „akce/progres"

- ✅ **Status bar rozdělen na dvě zóny**, aby se nepřepisovaly. Dřív jeden `StatusText` míchal stav
  výběru (element/sekce), transientní feedback (uloženo/chyba) i progres (export N/N) - akční hláška se
  ztratila hned, jak uživatel klikl jinam. Nově: nová property **`SelectionStatusText`** (klidová zóna,
  píše ji jen `UpdateStatusForSelection`) + stávající **`StatusText`** repurposed jako transientní
  „akce/progres" zóna (~14 zápisů: export, uložení, chyby, načítání dat, migrace, schránka). XAML:
  levý `*` sloupec = výběr (šedý, ellipsis), pravý Auto sloupec = akce (tučný, primární barva),
  overflow blok posunut. Akční hláška zůstává viditelná i po změně výběru.

## 2026-07-03 - Escape ruší probíhající operaci (drag / resize / rubber band)

- ✅ **Escape během tažení / změny velikosti vrátí element na začátek** (`EndDrag`/`EndResize` s
  `commit: false` - už existující restore-z-init cesty). **Escape během rubber bandu** ho zahodí bez
  změny výběru. Bez operace = zruší výběr (beze změny), během přidávání z palety = zruší přidávání
  (beze změny). `OnWindowKeyDown` je na `PreviewKeyDown` okna → Escape dorazí i při mouse-capture dragu.

## 2026-07-03 - Discoverability: skryté featury do context menu

- ✅ **Context menu (pravý klik na element)** dostal položky, které dřív šly jen zkratkou:
  **Seskupit (Ctrl+G)** - jen při 2+ vybraných; **Rozeskupit (Ctrl+Shift+G)** - jen když je výběr
  ve skupině; **Rozdělit → Vodorovně (Ctrl+Shift+H) / Svisle (Ctrl+Shift+J)**; **Upravit text (F2)** -
  jen u TextElementu. Umístění: za „Z-pořadí", před „Vlastnosti elementu…", obklopeno separátory.
  Zviditelňuje featury bez nutnosti znát zkratku.

## 2026-07-03 - Zoom: tlačítko × zkratka konzistentní + přepis tabulky zkratek

- ✅ **Zoom - tlačítko a zkratka se shodují.** Dřív „1:1" tlačítko dělalo reset na 100 %, ale Ctrl+0
  dělal fit-to-page → nekonzistentní. Nově dvě konzistentní dvojice: **„Fit" tlačítko + Ctrl+0** =
  přizpůsobit stránce; **„1:1" tlačítko + Ctrl+1** = skutečná velikost 100 %. `ResetZoomTo100()` sdílený
  helper, `BtnFitPage_Click` → `FitToPage()`. (Dva nezávislé combo-zoomy designer/náhled ponechány -
  jsou to různé pohledy.)
- ✅ **`ux-conventions.md §3` přepsáno 1:1 dle KeyDown handleru** (byl drift). Smazán neexistující
  Ctrl+Shift+S (Uložit jako nemá zkratku); doplněno ~10 chybějících: F2 (inline text), Ctrl+P (tisk),
  Ctrl+Shift+P (vlastnosti stránky), Ctrl+Shift+A (změna agendy), Ctrl+B (levý panel),
  Ctrl+PageUp/PageDown (Z-order), šipky (posun elementu / sekce), Escape, Ctrl+0/Ctrl+1 (zoom).

## 2026-07-03 - Pre-pilot UX P0 dokončeno: U2 (context menu) + U6 (labely tlačítek)

- ✅ **U2 - context menu „Vlastnosti elementu…" otevře dialog.** Dřív jen vybralo element + přepnulo
  inline panel - u stejně pojmenované akce „Vlastnosti sekce…" (dialog) nekonzistentní, a „…" v názvu
  dialog implikuje. Nově volá `OpenPropertiesForElement(el)` (stejná cesta jako dvojklik).
- ✅ **U6 - sjednocení labelů tlačítek.** Cancel byl v TD rozjetý („Zrušit" 5× vs „Storno" 2×). Vše
  převedeno na `{StaticResource EDStrings.Cancel}` = „Storno" (firemní standard - EDStrings + EDFooter);
  „OK"/„Uložit" literály na `EDStrings.Ok`/`EDStrings.Save` (7 dialogů: CacheTemplatePicker, ChangeAgenda,
  InstancePicker, SaveSectionTemplate, SaveToCache, ElementEdit, NormalizePositions). Pravidlo (cancel =
  „Storno" přes EDStrings, primární = `EDStrings.Ok`/akční sloveso) zapsáno do `ux-conventions.md §1`.
- 🏁 **Tím je celé pre-pilot P0 (U1–U7) hotové.**

## 2026-07-03 - Pre-pilot UX P0: U3 (klik na paletu) + U4 (dvojklik do prázdna) + U7 ověřeno

- ✅ **U3 - klik na paletu vkládá element.** Dřív palette tlačítko jen zahájilo drag-to-canvas;
  prostý klik (bez tažení) nic nevložil (drag-only = neobjevitelné). Fallback v `EndAddingElement`:
  když se pustí bez cílové sekce (`_addingTargetSection == null`) a jde o element z palety, vloží se
  do `State.ActiveSection` na výchozí pozici 5×5 mm. Drag-to-canvas beze změny.
- ✅ **U4 - dvojklik do prázdna neotevírá vlastnosti stránky.** Fallback dvojkliku na prázdnou plochu
  (`OpenPropertiesForPage`) nahrazen zrušením výběru - dialog do prázdna byl překvapení. Vlastnosti
  stránky zůstávají na Ctrl+Shift+P / menu. Dvojklik na element / záhlaví sekce beze změny.
- ✅ **U7 ověřeno jako hotové** (audit byl stale): M/O snap toggly už tooltipy mají.

## 2026-07-03 - Pre-pilot UX P0: U1 (bug ukládání) + U5 + async confirm/close

- ✅ **U1 - BUG opraveno:** volba **„Ano = uložit"** v potvrzení neuložených změn (Nový / Otevřít /
  změna agendy / zavření okna) dřív ukládala do **JSON souboru** (`SaveAndContinue` → `SaveFileDialog` +
  `TemplateSerializer.Save`), ne do DB. Z pohledu uživatele ztráta dat - šablona nebyla v DB, kde ji čeká.
  Teď `SaveAndContinueAsync` volá **stejnou DB cestu jako Ctrl+S** (`SaveToCacheAsync`).
- ✅ **U5 - Ctrl+S bez DB** už netiše nepíše do status baru - `OfferJsonExportFallbackAsync` zobrazí
  popup („Databáze není dostupná - uložit jako JSON?") a na potvrzení spustí export.
- ✅ **Celý confirm/close flow async** (uložení do DB je async přes MLRest): `ConfirmUnsavedChanges()` →
  `ConfirmUnsavedChangesAsync()`, `SaveAndContinue()` → `SaveAndContinueAsync()`; `SaveToCacheAsync`/
  `SaveToCacheAsAsync`/`ExportToJsonAsync`/`OfferJsonExportFallbackAsync` vrací `Task<bool>` (true = reálně
  uloženo → volající pokračuje). `NewCommand`/`ChangeAgendaCommand` převedeny na `CommandAsync`;
  `MenuAiAssistant_Click`/`MenuImportPdf_Click` na `async void`.
- ✅ **Fix pádu při zavření okna** (`InvalidOperationException: nelze volat Close při zavírání`):
  nové async `Window_Closing` u čistého (nedirty) okna zavíralo synchronně uvnitř běžícího Closing eventu.
  Fix: nedirty okno → zavřít normálně (žádný cancel); dirty → `e.Cancel=true` + async potvrzení + `Close()`
  odložený přes `Dispatcher.BeginInvoke` (až po doběhnutí eventu).

## 2026-07-03 - Úklid T6: odstranění vestigiálního menu + MessageBox

- ✅ Odstraněno skryté menu **„Nástroje → Vlastnosti textu… (alternativa k dvojkliku)"** (+ handler
  `MenuTestTextEdit_Click`) - redundantní leftover z vývoje editoru; dvojklik i Enter dělají totéž
  (`OpenPropertiesForElement`). Tím zmizel i poslední `MessageBox` v `MainWindow.xaml.cs` (R2).
  `MessageBox` v `App.xaml.cs` (startup config warning + `OnUnhandledException`) ponechány - legitimní
  bootstrap/crash fallbacky (popup infra tam nemusí být dostupná). Dřív (t.d.) odstraněn i
  `ScheduleDetectorTest.cs` z produkční assembly.

## 2026-07-03 - Gold regresní test AD zastávkového JŘ (T1b základ)

- ✅ **`ALVA.TemplateDesigner.Tests.ZastavkoveJrAdGoldTest`** - základní 1:1 regresní pojistka:
  `GetData` na gold linkách **0365001 + 0365005**, digest = hrubé počty (ZASTAVKY/ODJEZDY/POZNAMKY)
  + MD5 celého spojeného výstupu, zmrazený jako baseline. Běží v pre-push Caché sadě (**27/27**).
  Smysl: zachytit, že se kódem nerozbil datový výstup.
- ⚠️ **Záměrně pevně vůči CB_CSADAD datům** (AD je per-firma namespace): drift JŘ dat = falešný pád
  (přegeneruj digest z hlášky „aktuální: …"), jiný server bez CB_CSADAD → test se přeskočí. Robustní
  verze (zmrazit vstupní `^JRL`/`^JRZ`/`^JRSET` do izolovaného namespace, data-drift-proof) odložena -
  viz roadmap T1b.

## 2026-07-03 - Architektonická rozhodnutí: R1 rozhodnuto, R2/R3 upřesněno, stav do roadmapy

- ✅ **Nová roadmap sekce „🧭 Architektonická rozhodnutí R1–R7 - stav a nejbližší akce"** -
  stavová tabulka všech rozhodnutí na jednom místě (plný text zůstává
  v `architektonicka-rozhodnuti-agendy.md`, kam doplněny poznámky z diskuze).
- ✅ **R1 rozhodnuto (diskuze):** zárodek sdíleného řádkového jádra = `DynamicTableElement`;
  extrakce až u LineTable (3 konzumenti ukážou, co je společné - dřívější extrakce nad dvěma
  by abstrakci tvarovala podle StopList domény). Nová akce **R1a**: hodinový kontrolní průchod
  DynamicTable (čisté rozvrstvení model sloupců / měření / render). Ujasněno: jádro ≠
  PageLayoutEngine (vnitřek elementu × sekce/stránky); domov `Rendering/` vedle CellRendereru.
- ✅ **R2 model potvrzen** (= dnešní architektura, nic se nemění); zbývá jen návrh partial-fit
  rozhraní („vykreslil jsem řádky 1–40, zbývá 12, pokračuj odtud") před Fází C+D.
- ✅ **R3 upřesněno:** základ kontraktu už existuje (`cache-api.md` + `01-wire-formats.md`);
  doplnit (a) prezentační nápovědy v1 (sémantické: `ZVYRAZNENI` 0–5, typ řádku,
  opakuj-na-další-straně, přes-celou-šířku; `FORE_COLOR`/`BACK_COLOR` legalizovat jen pro
  legacy paritu) + (b) pravidlo jen-aditivní evoluce kontraktu. Do `cache-api.md` před F2.

## 2026-07-02 - DynamicTableElement: samostatná dynamická tabulka + 2-tab editor

- ✅ **Rozdělení tabulek** - dynamická tabulka (řádky z `DataSource` kolekce) je samostatný
  element `DynamicTableElement`; statická `TableElement` **uspána** (`JsonDerivedType` zůstává
  pro načtení legacy šablon, z palety/menu odstraněna). Ztráta zpětné kompatibility akceptována.
- ✅ **`DynamicTableEditorDialog`** (2-tab, konzistentní se StopList/DepartureGrid): tab Obecné =
  EDInputForm (`ALVA.TemplateDesigner.EDInputForm.DynamicTableElementEdit`) + tab Sloupce
  (`ColumnListEditor` + `CellStyleEditor`, Add/Remove, mapování `Field`). Dvojklik na element
  otevře dialog rovnou (jako StopList).
- ✅ **Auto-scale do pevné Height** (jako JŘ elementy) + auto-distribuce šířky sloupců (`Width≤0`
  = rozdělí zbývající šířku); `BuildColumnsFromFields` helper; placeholder u prázdné tabulky.
- ✅ **Zdroj dat = EDInputForm List** plněný klientsky (kolekce agendy); seed sloupců
  **centralizovaný v dialogu** (undoable přes příkaz) + seed při otevření. Reapply výběru po
  re-Initu (EDInputForm auto-Init na `Loaded` = re-Init při návratu na tab). **Nemíchat
  EDInputForm + nativní WPF** - transient stav držet stranou (memory / temp global).
- ✅ **`ZastavkoveJrAd.GetSchema` synced 1:1 s reálnými `Build*` daty** - kolekce
  ZASTAVKY/SKUPINY_DNU/ODJEZDY/POZNAMKY doplněny o skutečná skalární pole (PORADI, JE_AKTUALNI,
  ODSAZENI, ZAKLADNI_TRASA, ZKRACENE_OZNACENI_LINKY, KATEGORIE, FORE/BACK_COLOR); phantom
  NADPIS2 odstraněn. (Vnořená pole ZONY/PRUBEHY/ATRIBUTY/… zůstávají doménou StopListu.)

## 2026-07-02 - Architektonická rozhodnutí R1–R7 pro škálování agend (nový doc)

- ✅ **`architektonicka-rozhodnuti-agendy.md`** - plain-text zápis 7 rozhodnutí, která drží
  model „stabilní tiskový engine + postupně přidávané agendy": R1 sdílené tabulkové jádro
  (gate u LineTableElementu), R2 rozdělení layout zodpovědností engine×element, R3 prezentační
  nápovědy v datovém kontraktu + aditivní evoluce, R4 kde běží headless tisk + PrintEngine
  fasáda, R5 fonty embedded v aplikaci (Timetable = sémantika), R6 systémové šablony s release
  (ne DB → žádná verzní matice), R7 pilot-first. Každé: co znamená / jaký problém řeší /
  co se rozhoduje / doporučení / nejpozději kdy. Úvodní slovník „co je engine, co je agenda".
- Roadmap: ⚠️ gate odkazy na R1/R2/R5 (Fáze C+D), R4/R7 (blok A), R6 (blok B), R3 (Fáze F);
  CLAUDE.md reference („číst před startem každé nové fáze").

## 2026-07-02 - T1b (část 1): Caché testy - SablonyTest + Mhd helpery (8 testů, pre-push)

- ✅ **Nový package `ALVA.TemplateDesigner.Tests`** (`CACHE/ALVA/TemplateDesigner/Tests/`,
  extends `ALVA.EDTesting.MLTestCase`) - přidán do default listu `tools/run-cache-tests.ps1`
  → pre-push hook kryje i TD Caché stranu. Celá Caché sada: 23/23.
- ✅ **`SablonyTest`** (5 testů) - CRUD primárního úložiště šablon, vzor „round-trip nad
  vlastními daty": izolovaná IdFa větev `EDTESTING` globálu (žádná závislost na živých
  firemních datech, cleanup v OnBeforeOneTest). Save→Load byte-identický blob + metadata,
  Delete + prázdný List + Load smazané, update zachová CreatedAt + přeindexuje ByAgenda,
  Rename, validace povinných polí (nevalidní Save nic nezapíše).
- ✅ **`ZastavkoveJrMhdHelperyTest`** (3 testy) - deterministický `ConvertSpecialniZnak`
  (dny 1–7, ASCII >31: 72→H/70→F/120→x/123→{, ≤31 beze změny). Mhd třída - mimo AD WIP.
- **Poznatek o kontraktu (zdokumentován v testu):** Sablony metody vrací `$$$OK` i po chybě -
  `$$$CatchErrRest` plní `ret`, který zvedá až MLRest HTTP vrstva; in-process se chyba
  detekuje přes observable stav (chybějící OUT klíče, nezměněný globál).
- **Kompilace:** root hook `compile-objectscript.sh` (Atelier PUT + compile) - v TD session
  volán ručně (hook je vázaný na root project dir). MCP `cache_compile` hledá zdroje pod
  `C:\DEV\EDISON` → pro TD soubory nepoužitelný (pozor: hlásí „up-to-date" i bez uploadu).
- **Část 2 (tentýž den - AD engine je commitnutý, WIP blocker pominul): +17 testů → 25/25
  (celá Caché sada 40/40):**
  - `ZastavkoveJrAdHelperyTest` (12) - čisté helpery se syntetickými vstupy: `ZarazeniSpoje`
    (FTL 0/1/2/3 vč. prázdninového dělení dle NZ konfigurace), `BitmapNaSkupiny`, `CasHodnota` +
    `CasJizdyZMapy` (W-mapa, čekání, projíždí), `PrefixOmezeni`/`FormatDatumyOmezeni`/`UpravDatum`
    (1:1 txtNZ/UprDat), `JeViditelnyAtribut`/`TextAtributu`, `GetZnk` (zdvojení po vyčerpání),
    `ZiskejMarker` (dedup), `NormalizLinku`.
  - `ZastavkoveJrAdSmokeTest` (3) - strukturální smoke `GetData` nad živými daty: linka hledaná
    dynamicky přes `PrvniLinka` (žádná hardcoded gold linka → přežije editace dat), asserty na
    strukturu wire formátu (values/rows, ZASTAVKY/SKUPINY_DNU neprázdné, HODINA/MINUTA rozsahy,
    vazby odjezdů); chybové cesty (bez LINKA_ID, neexistující linka) → žádné stránky.
  - **Nález zdokumentovaný testem:** `ZarazeniSpoje` s PRÁZDNOU NZ konfigurací řadí pracovní
    spoj jen do sloupce „mimo prázdniny" (prázdné cnz == prázdné nzftl1) - gold-ověřené chování
    (0320144, AD-N15: `MHD_NZ1_NZ2` v praxi prázdné), ne bug.
- **Zbývá z T1b:** už jen `tools/compare-gold.ps1` (1:1 vs legacy jako skript mimo hook).

## 2026-07-02 - T2: Async export PDF/PNG nad snapshotem šablony (UI zůstává plně použitelné)

- ✅ **Export PDF/PNG neblokuje UI (R1) a uživatel může dál editovat** - dřív sync fetch +
  render na UI threadu (~9 s „Not Responding" u 600 stránek). Nově orchestrace ve VM
  (`ExportPreviewPdfAsync`/`PngAsync` přes `CommandAsync`): fetch (`BuildAllPreviewDataPagesAsync`,
  `Task.Run` + `IProgress<string>`) i render na threadpoolu.
- ✅ **Snapshot šablony** - `TemplateSerializer.DeepClone` (JSON round-trip, bez razítkování
  ModifiedAt, bez migratoru) při kliknutí na export; `PreviewCanvas.RenderMultiDataToPdf/Png`
  dostaly `Template?` parametr → renderují snapshot, ne živý stav. **Editace během exportu
  výstup neovlivní** (exportuje se stav v okamžiku kliknutí). Žádný blokující overlay -
  jen `_exportRunning` guard (canExecute obou export commandů) proti souběžnému exportu.
  Pokryto testem (`DeepClone_JeNezavislaKopieANemeniModifiedAt`).
- ✅ **Progress ve status baru ve dvou fázích** - „Načítám data X/N: {instance}…" (per instance
  při fetchi) → „Generuji PDF: X/N datových stránek…" / „Generuji PNG: X/N stránek…"
  (`IProgress<(done,total)>` z render smyčky).
- ✅ **První řez god-object úklidu (T4)** - export logika z code-behind do VM; view wiring
  = 2 callbacky (`RenderPdfToFile`/`RenderPngToFile`, nahrazují `RequestExportPdf/Png`);
  `OpenExportedFile` (Process.Start) ve VM - bez WPF, R4 OK.
- Známý krajní případ: ambientní `DocumentDefaults.Style` se při renderu nastavuje ze snapshot
  šablony, ale je procesně-globální - změna *výchozího fontu šablony* přesně během běžícího
  exportu by mohla prosáknout do části stránek. Ostatní editace jsou plně izolované snapshotem.
  Follow-up mimo scope: první `GetSchema` hit v `RefreshFieldsList` zůstává sync (mitigováno cache).

## 2026-07-02 - Roadmap: merge akčních položek z analýzy legacy náhrady

- ✅ **Akční položky z `legacy-nahrada.md` zamergovány do `roadmap.md`**
  (jedno místo pravdy, analýza zůstává jako evergreen referenční detail se status poznámkou):
  - Nová sekce **„Architektonický track T1–T6"** (T1a testy ✅, T3 ✅, T2 async export ⬅️ další,
    T1b Caché testy, T4 strangler, T5 duplikace, T6 drobnosti) + 3. aktivní track nahoře.
  - **Blok A** doplněn o legacy vzory (RG headless CLI AUTOEXPORT/AUTOPRINT pro A3) + **A4**
    (merge PDF + e-mail - společný požadavek všech 3 legacy systémů).
  - **Blok B** doplněn o legacy „MlineSestava" vzor + **B4** (per-instance přiřazení šablony,
    linka → šablona A4/A5) + **B5** (audit log + ECL právo na designer).
  - **Fáze C + D sloučeny** (legacy TiskJR generuje vývěsní i knižní z téhož enginu; oracle
    `mkPLinWin`, prerekvizity: Timetable font → agenda → LineTableElement + layout rozšíření).
  - **Fáze E** rozšířena o Relace (TiskJRRELACE, čeká validace use case).
  - **Nová Fáze F: Náhrada REPORTGENERATOR** (F1 usage statistika, F2 bridge vs čisté agendy,
    F3 TableElement dorovnání, F4 výstupní kanály, F5 jazykové varianty, F6 neportovat bez poptávky).
  - **Otevřené otázky Q1–Q6** (Epson, bridge, Relace, grafy/AZTEC, jazykové varianty, V1 soubory).

## 2026-07-02 - T3: TemplateMigrator guardy (forward-compat + hlášení chyb migrace)

- ✅ **Forward-compat guard (R5)** - šablona s verzí novější než `CurrentVersion` se odmítne
  `TemplateDeserializationException` („aktualizujte TemplateDesigner") místo tichého přerazítkování
  dolů (= ztráta dat novější verze při následném uložení). Chyba doteče uživateli existujícími
  kanály (soubor: `PopupDisplayer.Error`, DB: `CacheTemplateException`) - žádná změna UI.
- ✅ **Selhání migrace = chyba, ne tiché pokračování** - dřívější `Debug.WriteLine + continue`
  („partial migration") nahrazeno výjimkou s popisem migrace; částečně zmigrovaná šablona se
  neotevře (a tedy ani neuloží poškozená).
- ✅ **Sémantické porovnání verzí** - early-out přes `VersionComparer` místo string equality;
  „1.0" == „1.0.0" už nehlásí falešnou migraci.
- ✅ **Testovatelnost pipeline** - `TemplateMigrator(IEnumerable<ITemplateMigration>?)` ctor;
  testy s fake migracemi (transformace, pořadí, skip novějších, propagace chyby). Skip test
  `NovejsiVerze_SeOdmitne` odskipnut → **164 testů, 0 skipů**.
- **H-19 (V1→V2 migrace) - ověřeno v DB:** šablony v `^ALVA.TemplateDesigner.Templates`
  (CB_CSADAD, CB_GWBUS) mají `version: 1.0.0`; DB úložiště (Wave 3, 2026-05-25) vzniklo až po
  TextStyle breaking change (2026-03-24) → V1 v DB neexistují. Zbytkové riziko: staré JSON
  soubory na disku z éry před 2026-03-24 (bez `version` pole = nerozlišitelné od aktuálních);
  pokud se objeví, dopsat migraci + heuristiku detekce flat-TextStyle.

## 2026-07-02 - AD: multi-varianta časů tras (dynamické TrasaMin) - L1

- ✅ **Dynamické variantní časové sloupce ve StopListu** (nahrazuje legacy fixních 15 slotů TrasaMin).
  Agenda `ZastavkoveJrAd` seskupí obsluhující spoje **podle časového profilu W-mapy** (prutmbmp) →
  emituje `TRASA_MIN[]` per zastávka + `VARIANTY_TRAS[]` (hlavičky = písmena rozdílů tras / „ZT").
  N = skutečný počet variant (ne fixních 15, bez capu). StopList rozvine kolonku „Čas" na N sub-sloupců.
- ✅ **1:1 ověřeno proti gold** (`ALVA.TISKMHD.Data`, 0365005): 6 variant B,C,E,F,G,H, časy + `<`
  (projíždí) / prázdno (konec varianty) přesně jako legacy. Grupování 12 `cisloTrasy` → 6 profilů.
- ✅ **Přepínač `ShowAllRouteVariants`** (default zap) - vyp = jeden sloupec = výchozí trasa
  (`ZakladniCisloTrasy`, nejvíc spojů). V panelu i v Caché schématu `StopListElementEdit`.
- ✅ **Fix `CacheAgendaProvider`** - page-level pole/objekty (VARIANTY_TRAS) se zachovají jako JsonNode
  (dřív stringifikovány → zahozeny). Řádkové pole (ZONY…) fungovaly už dřív.
- **Ověřeno 1:1 s legacy i koncepčně:** „2 báze" (nejvíc-spojů pro časy / dta12 `W_POZ` pro odchylky-
  poznámky) odpovídá starému `JRapiLinPrtMhd` (ř.108-127 vs `OmTrasa` VzorTr). Zbývá: šířka variantních
  sloupců je zatím pevná (`VariantColumnWidthMm`), jemnější výběr variant (per-písmeno) = budoucí v2b.

## 2026-07-02 - TemplateDesigner.Tests: první sada automatizovaných testů (krok 0–1 arch. tracku)

- ✅ **Nový projekt `TemplateDesigner.Tests/`** (xUnit, zrcadlo EDControls.Tests) přidán do
  `EDISON2.sln` → pre-push hook TD od teď kryje automaticky. **104 testů (103 ✅ / 1 skip)**:
  - `Layout/PageLayoutEngineTests` (15) - stránkování, KeepTogether/SplitAcrossPages + ClipTop/Bottom,
    PrintBehavior matice (OnEveryPage/OnFirstPageOnly/OnLastPageOnly dvouprůchod/ExceptFirst),
    patička od spodu, PAGE/TOTAL_PAGES injekce, PerDataRow expanze, VisibilityExpression,
    dynamická výška elementu, propagace CultureCode. `FixedHeightCalculator` test double (bez SkiaSharp).
  - `Expressions/*Tests` (~60) - ExpressionEvaluator (porovnání numericky vs. lexikograficky,
    logické operátory, truthiness, fail-open, Validate), PlaceholderFormatter (string funkce,
    N2 cs-CZ×en-US, datum, TimeSpan, fail-safe), AggregationResolver (SUM/COUNT/AVG/MIN/MAX, prázdné řádky).
  - `Serialization/TemplateSerializerRoundTripTests` - round-trip idempotence pro všech 9 typů
    elementů (Serialize→Deserialize→Serialize, `modifiedAt` neutralizován - Serialize razítkuje UtcNow),
    tolerance neznámé property, RoundedFloatConverter, legacy „MHD"→„ZASTAVKOVY_JR_MHD" remap.
  - `Serialization/TemplateMigratorTests` - charakterizace současného chování + 1 Skip test
    (žádoucí forward-compat guard, odskipovat s T3 opravou).
- **Nálezy z psaní testů:** (a) early-out v `TemplateMigrator.Migrate` porovnává verze string
  equality, ne VersionComparerem → „1.0" se hlásí jako migrovaná (neškodné, zvážit při T3);
  (b) `dotnet sln add` vygeneroval kolizní solution folder „TemplateDesigner" → ručně odstraněn ze sln.
- Kontext + celý plán: [`legacy-nahrada.md`](legacy-nahrada.md)
  (technický audit T1–T6 + legacy REPORTGENERATOR/TiskMHD/TiskJR + roadmapa náhrady).
- **Druhá vlna (tentýž den): TextStyle kaskáda + helpery → celkem 150 testů (149 ✅ / 1 skip):**
  - `Models/TextStyleTests` - CSS-like cascade (dědění, override, 3vrstvá kaskáda, trim fontu,
    idempotence, IsEmpty, Clone); `Models/DocumentDefaultsTests` - dokumentový default (overlay,
    prázdný font = no-override, propagace do Resolve), vždy s finally reset globálního stavu.
  - `Models/DepartureGridCascadeTests` - **regresní testy na anti-pattern „scalar přebíjí kaskádu"**
    (2× opravený bug, mvvm-patterns.md): `ResolveGroupMinuteParent` (MinuteFontSize/MinuteBold jen
    když MinuteStyle+DataStyle mlčí), `ResolveGroupHeaderParent` (SkupinaDnuHeaderLineHeight fallback,
    VAlign), padding 0 u minut, HourData dědění.
  - `Models/MmMathTests` (Round 0.1mm AwayFromZero, nullable, Snap), `Rendering/ColumnHeaderTests`
    (override vs. zdroj), `Elements/TextWrapHelperTests` (headless SkiaSharp - normalizace \r\n,
    extrémní šířky, prázdné řádky; bez assertů na pixelové metriky).
  - Testy stylů sdílejí xUnit kolekci `StyleCascade` - `DocumentDefaults.Style` je procesně-globální
    stav, kolekce vynucuje sekvenční běh (žádné vzájemné ovlivnění).
- **Třetí vlna (tentýž den): driftová pojistka EDInputForm schémat → celkem 161 testů (160 ✅ / 1 skip):**
  - `Serialization/EDInputFormSchemaDriftTests` - parsuje `CACHE/.../EDInputForm/*Edit.cls` přímo
    z repa (offline, bez serveru), vytáhne field keys (= property paths pro ElementFormBinder
    reflexi) a reflexí ověří existenci na cílovém C# typu. Mapa 10 schémat (8 elementů + Section
    + Page/Template). `StyleFields.Add` bloky expanduje podle suffixů čtených ze `StyleFields.cls`
    (self-updating). Virtuální klíče: `Format`/`Orientation` (PageEdit), `style:<prefix>` tlačítka
    pod-formuláře → validuje se path za prefixem. Uzavírá driftové riziko Wave 4 duplikace
    (memory `project_edinputform_schema_duplication.md`) - přejmenování property bez úpravy .cls
    od teď zablokuje push.

## 2026-07-02 - Startovní agenda z inputParam kontraktu

- ✅ **`inputParam` "Agenda=<kód>" → startovní agenda šablony.** `App.OnStartup` vytáhne kód
  z `EdisonLaunchArgs.InputParam`, `TemplateDesignerViewModel` ho ověří proti registru agend
  (case-insensitive) a nová šablona startuje rovnou s ní. Zdroj: atribut `input` uzlu stromu
  legacy shellu (JŘ uzel = `Agenda=ZASTAVKOVY_JR_AD`, MHD uzel = `Agenda=ZASTAVKOVY_JR_MHD`)
  a MHD2 tlačítko „Linky a oběhy". Neznámý/chybějící kód → dosavadní default.

## 2026-07-02 - Převzetí session shellu přes EDISONMAIN2 launch kontrakt

- ✅ **`App.OnStartup` parsuje EDISONMAIN2 argumentový kontrakt** (`EDControls.EdisonLaunchArgs`,
  zrcadlí `EDISONMAIN2API.FormToRunGetter`): sentinel `EDISONMAIN2` + server, restPort, namespace,
  idfa, login, fullName, mlp [, formName, shellGuid, inputParam]. Při spuštění ze shellu (starý
  EDISON přes wrapper formu `ALVA.TISKJR.formTISKJRTemplateDesigner`, MHD2 „Linky a oběhy") se
  MLRest staví ze session shellu (`CacheSchemaWiring.CreateMlRest`) místo z appsettings - TD běží
  pod správnou firmou a uživatelem.
- Bez argů beze změny (standalone z appsettings). Sentinel s nevalidními argy → varování + fallback
  standalone. `shellGuid` se záměrně nepřebírá - TD má vlastní session GUID, neváže se na ACTIVITY
  zámek shellu.

## 2026-06-30 - Ověření: pořadí poznámek v legendě 1:1 s legacy (živý gold oracle)

- ✅ **Default řazení legendy potvrzeno 1:1** proti gold oracle (`ALVA.TISKMHD.Data`, VOLANO_Z=JRMAIN)
  na CB_CSADAD lince **0365005** (page 1, Smrkovice → Dobešice). Pořadí **bezbariér → rozdíly tras
  (B,C,E,F,G,H) → omezení (29,68)** se shoduje včetně sub-pořadí a textů. `NotesLegendElement.Razeni
  = Vychozi` zachová Caché emit order → **žádný reorder není potřeba**.
- Vyvrací dřívější (retraktovanou) domněnku o nutném reorderu „typ1→2→3" - ta by pořadí naopak
  rozbila. **Neprověřeno na této lince:** pozice `atributy-zastavky` / `ruční-komentáře` / `směrové`
  vůči těmto třem (na stránce nebyly); emit order je dle kódu atributy → bezbariér → rozdíly →
  komentáře → omezení → směrové.

## 2026-06-30 - Font combobox plošně (sjednocení výběru fontu)

- ✅ **Jednotný font picker všude** - editovatelný ComboBox s nainstalovanými fonty (`SKFontManager`
  přes `FontResolver`) napříč všemi místy výběru fontu, ne jen v PageEdit:
  - `ElementFormBinder.PopulateFontFields` (sdílené, přesun z ElementEditDialog) - voláno ze **3 hostů**
    EDInputformu: `ElementEditDialog` + embedded form v `StopColumnsEditorDialog` / `DepartureGroupEditorDialog`.
  - `TextElementEdit.cls`: font List se 6 natvrdo hodnotami → **ComboBox** (všechny nainstalované,
    editovatelný, volitelný → prázdné dědí dokumentový default).
  - `StyleFields.cls`: font `AddText` → **ComboBox** (propíše se do všech tabulkových/JŘ style zón).
  - `CellStyleEditor` (per-column C#): font TextBox → **editovatelný ComboBox** (`FontFamilyOptions` DP,
    default `FontResolver.InstalledFamilies`); tri-state placeholder „dědit" zachován.
- Filtrování/omezení na podmnožinu fontů půjde později (změna jednoho seznamu). Varování „font není
  nainstalovaný" stále odložené (Arial fallback to mitiguje).

## 2026-06-30 - Výchozí font na úrovni šablony (dokumentový default + font combobox)

- ✅ **`Template.DefaultTextStyle: TextStyle?`** - dokumentový výchozí styl (UI zatím jen font, model
  rozšiřitelný o velikost/barvu). Kořen kaskády: elementy bez vlastního fontu ho zdědí.
- ✅ **Ambient `DocumentDefaults`** - `Apply()` overlayuje výchozí styl na zapečené dno kaskády
  (`ResolvedTextStyle.Default`, `CellDefaults.Default` + schedule/table rodiče převedeni na gettery;
  ze schedule rodičů odstraněn natvrdo `FontFamily="Arial"`; `CreateDefault` už font nenastavuje).
  Sync přes `PreviewCanvas.Layout()` wrapper (chokepoint před každým layoutem - pokrývá preview,
  PDF/PNG export, tisk i měření přetečení) + `DesignerCanvas.Render` + `RebuildSections`.
- ✅ **UI font combobox** - `PageEdit.cls` font pole = editovatelný ComboBox; klient ho po načtení
  schématu naplní nainstalovanými fonty z `SKFontManager` (`ElementEditDialog.OnFormInitialized`).
- ✅ **`FontResolver`** - jediný zdroj fontů (`SKFontManager`) + Arial fallback: neznámý/nenainstalovaný
  font → **Arial** (≈ Helvetica), ne náhodný systémový default. Vstup shovívavý (trim mezer, prázdné=default).
- 🐛 **Fix `ElementPropertyAccessor.Exists`** - traverzace podle typu (ne hodnoty) → zápis přes EDInputForm
  s null mezičlánkem (`DefaultTextStyle.FontFamily`) se už nezahazuje. (Týkalo se obecně vnořených cest.)
- 🐛 **Fix měření↔glyf asymetrie** - měření šířky v `DepartureGridElement`/`StopListElement` používá
  resolved font (vč. dokumentového defaultu), ne raw → sloupce se neořezávají při custom fontu.

## 2026-06-30 - Databáze jako primární úložiště šablon (JSON = import/export)

- ✅ **Sjednocení modelu ukládání** - Otevřít (Ctrl+O) / Uložit (Ctrl+S) / Uložit jako míří do
  **databáze**. Soubor degradován na **Importovat z JSON** / **Exportovat do JSON** (export nemění
  původ - kopie ven, obdoba PDF/PNG). Odstraněny duplicitní menu položky „Otevřít/Uložit do databáze".
- ✅ **Titulek ukazuje původ** `[DB]` / `[neuloženo]` (kam míří Uložit). Nová/importovaná šablona →
  Ctrl+S ji založí do DB (SaveAs formulář s jménem); existující → tichý update (bez confirm).
- **Pohlcuje S2** (asymetrie file/DB původu) - export už původ nemění, problém zaniká. Správce
  „Nástroje → Šablony v databázi" beze změny. Versioning prerekvizita (R5) splněna `TemplateMigrator`em.

## 2026-06-24 - Evidence nových zákaznických požadavků z prezentací (sekce P)

- 📝 **Zaznamenány 3 nové požadavky** z živých prezentací 2026-06-24 do
  `customer-requirements-zastavkovy-jr.md §P` (evidence, neanalyzováno; před implementací založit ticket):
  **P1** ruční poznámky do patičky, **P2** sloučení linek se stejným posledním trojčíslím (i různá Tč /
  více dopravců) do jednoho výstupu, **P3** logická imposice 2×A5 oboustranně na A4.

## 2026-06-23 - AD: datumová omezení jako negativní značky (cnz) + drobné opravy

- ✅ **Číslování omezení = číslo NZ (`cnz`) invertovaně** (1:1 legacy). Marker datumového omezení je
  nově `cnz` spoje (`$p(spoj,"#",7)`) renderovaný jako typ `nz` (invertovaný box), ne sekvenční „1".
  Text dál z per-spoj `SestavTextOmezeni`. Dedup per-cnz. `cnz` prázdné → fallback na staré sekvenční.
  Ověřeno gold oracle (`ALVA.TISKMHD.Data`) na **obou** firmách (CB_CSADAD „29", KV_DP 28/30/31/40).
  **Korekce dřívějších domněnek:** žádný firemní TYPPOZ gate (`MHDnewOM` prázdné u obou firem),
  KV_DP nebyl 1:1 ani předtím, text jde z per-spoj (ne ze zastaralého InPlatDef seedu). Detail §10.
- ✅ **Toggle `InvertovatNegativniZnacky` odstraněn** z `DepartureGridElement` - inverze NZ je teď
  napevno vždy zapnutá (AD i MHD, jako legacy); barvy `NegativniZnacka{Back,Fore}Color` ponechány.
- 🐛 **Drag DB pole nemělo výchozí rámeček** - `AddElementAtPosition` tvořil holý `TextElement` mimo
  `CreateElementByType`. Nově jde přes `CreateElementByType(Text)` → stejné defaulty (rámeček, rozměry)
  jako element z palety.

## 2026-06-23 - AD: konfigurovatelné rozdělení sloupců (typy provozu / FTL override)

- ✅ **Nový parametr „Rozdělení sloupců (typy provozu)"** v AD agendě - override FTL (default
  „Automaticky (dle linky)" = `MHD_TYP_R`) se 4 módy pokrývajícími FTL0-3 (= kombinace
  prázdninový split prac. dnů × sloučení víkendu). **Reuse existujícího `NacistSkupinyDnu` +
  `ZarazeniSpoje`** (engine beze změny, jen `$case` override `ftl`). Uživatel si tak může zvolit
  rozdělení/sloučení sloupců místo napevno dle dat linky.
- ✅ **Server-side param flow** - `RefreshDataForCurrentInstanceAsync` posílá print params do
  `GetData` (dřív jen instanci); změna server-paramu spustí **re-fetch** dat z Caché (client-side
  params jen překreslí, beze změny perf).
- **Datově:** víkend merge/split jede z Plk dat (funkční vždy); prázdninový split potřebuje NZ
  konfig (`MHD_NZ1_NZ2`, dormant ve všech firmách) → sloupec „o prázdninách" prázdný (očekávané).
- **Follow-up:** (1) generalizovat „server-side" flag parametru (zatím hardcoded
  `ROZDELENI_SLOUPCU` v C# re-fetch checku); (2) poznámka „jede v neděli" chybí u jednoho spoje
  0365005 (date-based omezení vs dateless denní note) - k dohledání; (3) jemné custom rozdělení
  (libovolné dny → sloupec) + multi-varianta tras ve StopListu = větší redesign.

## 2026-06-23 - DepartureGrid: adaptivní rozteč minut s anotacemi

- ✅ **Minuta s anotacemi se už nepřekrývá s další minutou** - slot odjezdu v `DrawMinuteCell` je
  nově adaptivní: nominální `scaledMinuteW` + naměřená šířka anotací (zachová nominální mezeru).
  Blok minut se zarovnává ze součtu adaptivních šířek (správný center/right align). Dřív se
  posouvalo o pevnou `MinuteWidth` → husté buňky s poznámkami se slévaly. Šířka sloupce
  (ComputeLayout) zatím beze změny - s redistribucí na šířku elementu bývá slack; měření šířky
  sloupce dle anotací = follow-up, pokud by dense buňka přetékala do sousedního sloupce.

## 2026-06-23 - NotesLegend: zalamování dlouhých poznámek

- ✅ **NotesLegend wrapuje text poznámek** na šířku sloupce (`DrawWrappedBody` přes `TextWrapHelper`)
  - dlouhá poznámka už nepřetéká doprava, ale zalomí se na další řádky (visutý odsazený blok pod
  tělem). Layout přešel z pevné mřížky řádků na **akumulaci Y per sloupec** (víceřádková poznámka
  posune další dolů). `CalculateHeightFromData` zrcadlí (výška = nejvyšší sloupec dle zalomených
  řádků → auto-scale u pevné výšky). Wrap je vždy zapnutý (overflow nikdy nechceme).

## 2026-06-23 - AD StopList: projeté zastávky + oprava dvojitého „na znamení" + názvy sloupců

- ✅ **AD: tisk projetých zastávek** (`ZastavkoveJrAd.BuildStrankaZastavky`) - agenda nyní emituje
  **celou trasu** ve směru (vč. projetých, analogicky MHD): `JE_AKTUALNI` na aktuální, projeté mají
  `casJizdy=0` a bez atributů. StopList je defaultně skryje (`ShowPassedStops=false` → výchozí výstup
  1:1 zachován); „tisk projetých zastávek" je zobrazí ztlumeně. Dřív AD emitovalo jen úsek od aktuální
  → toggle byl inertní (na rozdíl od MHD).
- ✅ **AD: oprava dvojitého „na znamení" (× x)** - agenda nastavovala `NA_ZNAMENI=true` (→ syntetický
  `SignalStopSymbol` „×") **i** přidávala atribut „x" → znak se tiskl dvakrát. Nyní `NaZnameni=0`,
  „na znamení" jde jen přes atribut „x" (1:1 legacy `UVOD^JRapiLinPrtMhd`).
- ✅ **Editor sloupců StopListu: čitelné názvy** - `StopColumnVM.DisplayName` odvozen z `Key`
  („Tarifní číslo (TČ)", „Základní trasa (ZT)", „Čas jízdy", „Průběhy trasy"…); tištěná zkratka
  v poli „Nadpis" zůstává. Uživatel už nemusí luštit „TČ"/„ZT".

## 2026-06-23 - Genuine page-overflow detekce (dokončení P0-2)

- ✅ **`PreviewCanvas.PageOverflows`: detekce sekce vyšší než stránka** - u `KeepTogether` sekcí
  (nelze rozdělit) hlásí přetečení, když `placed.Y + placed.Height > PrintableHeight` → obsah pod
  spodní hranou tiskové plochy se ořízne. Doplňuje dnešní #8 fix: ten odstranil false-positive u
  dělitelných sekcí, ale tím se ukázalo, že element-vs-sekce kontrola fakticky nikdy nenaskočí
  (sekce se sama roztáhne na obsah) → varování bylo dormantní. Teď chytá reálný případ.
- **Ověřeno live:** KeepTogether 400 mm → červená; přepnutí na SplitAcrossPages → bez červené
  (fix #8 drží); KeepTogether 100 mm + čistá šablona → bez false-positive.

## 2026-06-23 - Negativní značky invertovaně v mřížce odjezdů (legacy shoda)

- ✅ **DepartureGrid: NZ u minuty invertovaně** - negativní značka (`TypAnotace.NegativniZnacka`) se
  v mřížce kreslí jako **černý box + bílý znak**, jako legacy (`formTISKMHDReportDotNet_Module.vb`
  `FillRectangle(Color.Black)` + bílý text). Dřív se kreslila plain. Tři nové property:
  `InvertovatNegativniZnacky` (default zap), `NegativniZnackaBackColor` (černá), `NegativniZnackaForeColor`
  (bílá) - editovatelné v panelu (skupina „Anotace - symboly"). Backward-compat: staré šablony dostanou
  invert default = legacy shoda.
- **Ověřeno** na MHD agendě (mock NZ „22" v 6:45 → invertované). **AD záměrně bez NZ u minut** -
  v JRMAIN je CNZ jen klasifikátor sloupců dnů (omezení = textové anotace), ověřeno 1:1 proti gold
  oracle; přidání by AD od legacy odchýlilo.

## 2026-06-22 - Stylový pod-formulář přes CellStyleEditor (Fáze 2 - prototyp, 1 zóna)

- ✅ **`StyleEditDialog` + `StyleEditDialogViewModel`** - znovupoužitelný pod-formulář pro editaci
  JEDNOHO TextStyle (zóny) libovolného elementu přes sdílený **`CellStyleEditor`** (tri-state →
  zděděné hodnoty jako **šedý placeholder**, rozlišuje dědí/explicit, reset). VM čte/zapisuje styl
  reflexí dle prefixu; OnOk → `SetPropertyCommand` (undo). **Nikdy nezapíše null** (nenullovatelný
  HeaderStyle by spadl) - prázdná zóna = nový prázdný TextStyle / žádný command.
- ✅ **EDInputForm `ButtonClicked` event** (EDControls, aditivní) - klik na BUTTON pole dá hostu klíč
  → client-side akce (otevřít pod-formulář). Doplňuje server-side Validate.
- ✅ **Odjezdy „Styl záhlaví" → tlačítka** - HeaderStyle + 2. řádek balíčky (~30 polí) nahrazeny
  tlačítky „Upravit styl…" → pod-formulář. Hlavní „Obecné" se odlehčí. Commandy keyované prefixem
  (opakované otevření zóny nahradí) → aplikují se v parent OK jako jeden undo krok.
- **Prototyp:** jen zóna Styl záhlaví u Odjezdů; ostatní zóny + StopList zatím inline. `StyleEdit.cls`
  (Caché varianta A) ponechán jako nepoužitý server-side fallback. **Dál:** rozšířit na všechny zóny
  + StopList, pak uklidit. Detaily v roadmap (Fáze 2 - stylový pod-formulář).

## 2026-06-22 - Odjezdy „Obecné" → EDInputForm (Fáze 1, krok 4 - C# rewire) ✅ Fáze 1 hotová

- ✅ **Tab „Obecné" dialogu Odjezdů → `<eif:EDInputForm>`** (schéma `DepartureGridElementEdit`) místo
  ručních CellStyleEditorů + PropertiesPanel. Flow změněn z **live** (PropertiesPanel + executeCommand
  + undo na cancel) na **batch na OK** (jako StopList): code-behind `OkButton_Click` → `GeneralForm.Save()`
  → `ElementFormBinder.ExtractCommands` → `GeneralCommand`. Cancel = nic se neaplikuje.
- ✅ **VM odlehčen** - odebrány `ElementHeaderStyle`/`ElementDataStyle` + 9 element-level commandů +
  `PickElementColor` + `GeneralDescriptors` + ctor param; OnOk už nezapisuje HeaderStyle/DataStyle.
- ✅ **`ElementPropertyAccessor.Set` lazy-alokuje null mezičlánky** na cestě (generické) - pro nullable
  zóny jako `HeaderLine2Style`; jen při zápisu (Get/Exists nealokují → Cancel element nemutuje).
- ✅ **MainWindow** - undoDepth/cancel-undo zahozen; OK aplikuje GeneralCommand + GroupColumns +
  HourColumnStyle jako **jeden `CompositeCommand`** (single undo, jako StopList).
- Fix: `HourColumnWidth` jen v tabu Sloupce (řádek Hodiny) - odebráno z Obecné, ať nekoliduje (dvojí zdroj).
- **Fáze 1 (sjednocení tabulkových editorů) HOTOVÁ:** Zastávky i Odjezdy mají kostru Obecné(EDInputForm) /
  Styl záhlaví / Styl datových buněk / [element]-specifické. Známé low: hour header/width direct-write
  není undo; lazy-alloc po undo nechá prázdný `HeaderLine2Style`. **Navazuje:** extrahovat stylový balíček
  do pod-formuláře (StyleEdit + StyleEditDialog) - odlehčí oba editory (5 zón × 15 polí inline).

## 2026-06-22 - EDInputForm 4-skupinová kostra + sdílený StyleFields helper (Fáze 1, krok 4 - Caché část)

- ✅ **Sdílený `ALVA.TemplateDesigner.EDInputForm.StyleFields.Add`** - jediný zdroj „balíčku" polí
  stylu textu (Font/Velikost/Tučné/Kurzíva/Podtržení/Přeškrtnutí/Zarovnání H+V/Barvy/Padding/Řádkování)
  pro daný property-path prefix. Volitelně append do poslední skupiny + prefix labelů (víc zón
  v jedné skupině). Nové pole stylu stačí přidat zde → propíše se do všech zón i elementů.
- ✅ **`StopListElementEdit` přeskupeno** do 4-skupinové kostry: **Obecné · Styl záhlaví · Styl
  datových buněk · [Element]-specifické** (+ Pozice/Podmínky). Stylové skupiny přes helper.
  Stejné property-path klíče → chování zachované (jen seskupení); „Řádky" sloučeno do Obecné,
  „Zastávky/Směr/Zvýraznění" do „Zastávky - specifické".
- ✅ **`DepartureGridElementEdit`** - nové schéma ve stejné kostře (Obecné / Styl záhlaví +2.řádek /
  Styl datových buněk = minuty / Odjezdy - specifické = anotace symboly+text přes prefix labelů).
  `HeaderStyle.VAlign` primární (HeaderVerticalAlignment fallback), `TimeAxisMode` = enum názvy.
  **Zatím nenapojené** na dialog - čeká na C# rewire (lazy HeaderLine2 alloc, collapse legacy
  scalárů, SetHeaderFontSize cleanup v Init/OnOk).
- Standard: „Obecné = EDInputForm" pro jednoduché elementy; TableElement (DataGrid) je výjimka.

## 2026-06-22 - Auto + override šířka sloupců (Fáze 1, krok 2b)

- ✅ **StopList - nullable šířka (`StopColumn.Width` → `float?`)** - prázdné = **auto** (rovnoměrně
  rozdělí zbývající šířku elementu po pevných sloupcích), hodnota = pevná. Jediné místo:
  `StopListElement.ResolveColumnWidths` (Dictionary<StopColumn,float>); napojeno do layoutu
  (pozice šipky, BuildDataRowSpecs/BuildHeaderRowSpecs/RenderColumnHeaders), ApplyScale (škáluje
  jen pevné). Editor: `WidthText` (prázdné = auto), fit-to-width + overflow ošetřeno na null.
  **Default `Width = null`** (ne 10f) - jinak by se auto po uložení ztratilo (serializer WhenWritingNull).
- ✅ **DepartureGrid - per-group `WidthOverride`** (float?) - prázdné = auto (z obsahu + proporční
  dofill, jako dosud), hodnota = **pin** (vyloučen z přerozdělení zbytku). Sloupec Hodin edituje
  `HourColumnWidth`. Layout cache hashuje `WidthOverride`.
- ✅ **Sdílený `Controls.ColumnWidthText.Parse/Format`** - locale-robustní (přijímá tečku i čárku,
  InvariantCulture); použito oběma editory (odstraňuje duplikaci + tichý fail při desetinné čárce).
- Oba editory mají detail „Záhlaví + Šířka" se **stejným modelem** (prázdné = auto, hodnota = pevná).
  Existující šablony beze změny (mají šířky vyplněné = pevné). **Zbývá (krok 4):** Odjezdy
  „Obecné" → EDInputForm. **Později (krok 5):** content-aware auto šířka (dle délky dat).

## 2026-06-22 - Override nadpisu sloupce (AD-N6) + sdílený editor sloupců (Fáze 1, krok 1+2a)

- ✅ **Override nadpisu sloupce/skupiny** - `Rendering/ColumnHeader.Resolve(override, source)` = jediné
  místo sémantiky „override přebíjí zdroj, prázdné → zdroj". Napojeny **všechny tabulkové komponenty**:
  StopList + TableElement (zdroj = autorský `Header`) i DepartureGrid (`DepartureGroupColumnStyle.HeaderOverride`
  přebíjí nadpis skupiny dnů z dat). Koncept se neimplementuje per komponenta - liší se jen ČÍM je zdroj.
- ✅ **Sloupec Hodin přepisovatelný** - `DepartureGridElement.HourColumnHeader` (prázdné → „Hod"); render
  přes `ColumnHeader.Resolve`, Clone. Editor: override jde nastavit pro **všechny** sloupce vč. Hodin.
  Uzavírá legacy AD-N6 („Nadpisy sekcí pro rozvržení").
- ✅ **Sdílený seznam sloupců** `Controls/ColumnListEditor` + `Controls/IColumnRowVM`
  (`DisplayName`/`Visible`/`HasAnyOverride`/`ColumnWidth?`) - levý seznam jednotně pro Zastávky i Odjezdy
  (checkbox + název + override-tečka + „X mm" jen u pevné šířky + ▲▼). `StopColumnVM` i
  `DepartureGroupColumnVM` implementují rozhraní (v DepartureGroup přejmenováno `IsVisible`→`Visible`).
- ✅ **Pořadí skupin dle dialogu** - `ResolveSkupiny` (render) i ctor editoru řadí skupiny dle
  `GroupColumns` (dřív datové pořadí) → přeskládání ▲▼ se projeví ve výstupu a přežije reopen dialogu.
- ✅ **Zarovnání jednotně ve stylu** - odebráno duplicitní inline „Zarovnání" combo ze Zastávek
  (HAlign v `CellStyleEditor` mají oba). Detail „Struktura sloupce" sjednocen (Záhlaví; Šířka jen u Zastávek).
- Layout cache DepartureGridu hashuje override + pořadí (`GroupHeaderOverridesHash`, order-sensitive).
  JSON zpětně kompatibilní (chybějící = null = z dat). **Zbývá (krok 2b):** proporční auto šířka v obou
  (sjednotí i poslední rozdíl „X mm"); pak Obecné→EDInputForm u Odjezdů (krok 4).

## 2026-06-22 - Tisk s daty: členění výstupu + název souboru z dat

- ✅ **Členění výstupu** (`__EXPORT_SPLIT`) - při tisku s daty (více instancí zastávka × směr) volba:
  **Vše do jednoho PDF** (default, dnešní chování) / **Jeden PDF na linku** / **Jeden PDF na
  zastávku × směr** (legacy vývěska per zastávka). Single → `SaveFileDialog`; split →
  `OpenFolderDialog` + N souborů do složky přes existující `RenderMultiDataToPdf` (1 volání per
  skupina, žádná nová PreviewCanvas metoda). Seskupení `GroupBy("CISLO_LINKY")` (zachovává pořadí
  z Caché). Bez ZIP (volné soubory ve složce = přímo použitelné pro tisk vývěsek).
- ✅ **Název souboru z dat** (`__EXPORT_FILENAME`) - pattern s `{POLE}` tokeny vyhodnocený nad první
  instancí skupiny přes `DataContext.ResolvePlaceholders` (konzistentní s `{POLE}` všude jinde).
  Default `{CISLO_LINKY}_{POPIS_ZASTAVKA}_{POPIS_SMER}` (= legacy „NázevZastávky_ČísloLinky_…",
  funguje pro AD i MHD). Sanitizace neplatných znaků + kolize → přípona `_2`, `_3`. Pole viditelné
  jen ve split režimu (`PrintDialog.ApplyVisibility`).
- ✅ **Jako EDInputForm pole, ne bespoke WPF** - obě volby přidává **`AgendaParams.cls`** jako
  universální skupinu „Export" (společná všem agendám, NEjsou v `GetParameters` → neukáží se
  v preview liště). Hodnoty tečou přes sdílený `_paramValues` → **persistence per-agenda zadarmo**
  (přes existující `PrintParams`, líp než per-session). PrintDialog se nově staví i pro agendu bez
  `GetParameters` (`HasForm`, ne `HasParameters` - kvůli export skupině).
- **TODO** (roadmap): extrahovat na úroveň `Template`/agenda-config (firemní default jako legacy
  „Tisk → Linky-MHD"). Dorovnává legacy firemní „Název souboru pro export do pdf" + per-linka split.

## 2026-06-21 - Legenda poznámek: layout (sloupce / řazení / zarovnaný marker)

- ✅ **Víc-sloupcová legenda** - `NotesLegendElement.Columns` (1-4) + `WrapSmer` (po sloupcích / po
  řádcích). Render distribuuje poznámky do sloupců, výška = záhlaví + `ceil(N/Columns)` × řádek.
  Dorovnává legacy „Počet sloupců" (`stPOZ_PocetSloupcu`), bez 1/2 stropu.
- ✅ **Zarovnaný marker** - `MarkerSirka` (0 = inline jako dnes, >0 = pevná levá buňka se znakem →
  svisle zarovnané markery). Dorovnává legacy `stPOZ_SirkaSloupceTextVlevo`.
- ✅ **Řazení poznámek** - `Razeni` (výchozí / podle délky). Dorovnává legacy „Trid_delK".
- Vystaveno v EDInputformu (`NotesLegendElementEdit.cls`, skupina „Rozložení") i inline panelu.
  Default (1 sloupec, inline, výchozí řazení) = beze změny existujících šablon. Reference:
  legacy návrhář `formTISKMHDSekcePoznamkyEdit` (viz `ad-data-mapa.md §1b`).

## 2026-06-21 - AD #7161 D5 (skrýt legendu „× na znamení") + D6 ověřeno

- ✅ **#7161 D5** - `NoteVisibilityFilter.ShouldHideNaZnameniLegend(template)`: když šablona má StopList,
  ale žádný nemá zapnuté `ShowSignalStopIndicator` („Na znamení"), skryje se z legendy kategorie
  `atribut-zastavky` („× na znamení"). Legenda následuje viditelnost značky, žádné nové ovládání,
  žádná Caché změna. Test: CB_GWBUS 0320144. **#7161 uzavřen (D5+D6).**
- ✅ **#7161 D6 ověřeno** na CB_CSADAD 0365004 (agregát „všechny spoje bezbariérové" se parametrem skryje).

## 2026-06-21 - AD #7947 + #7161 D6 (rozdíly tras + bezbariér agregát)

- ✅ **#7947 „jede až do XY"** - `RozdilyTras` větev prodloužení vypisuje „jede až do zast. <konečná>"
  místo matoucího legacy „pokračuje přes". Záměrná odchylka (klient), kód analogický k „jede jen do".
- ✅ **#7161 D6 - skrýt agregát bezbariér** - parametr `SCHOVAT_AGREGAT_BEZBARIER` (bool). Caché taguje
  agregátní legendu (`allBBP` → kat. `bezbarier-agregat`), `NoteVisibilityFilter` ji skryje. Filtr-model
  jako #7955. Neověřeno na datech (žádná linka 100% bezbariér v KV_DP/CB_CSADAD - latentní).
- 🔧 **MCP `edison-cache` opraven** - deployovaný exe byl neúplný (chyběl runtime); rebuild +
  `.mcp.json` cesta na podsložku `C:\Edison\EdisonCacheMcp\`. Ověřeno proti gold oraclu (CB_CSADAD).
- 📄 **AD analýzy sloučeny** do `ad-data-mapa.md`; přípoje/km přesunuty do linkových JŘ.

## 2026-06-20 - AD #7955: co schovat v rozsahu „nepoznámkovat" (Fáze 2)

- ✅ **Parametr sestavy `SCHOVAT_V_ROZSAHU`** (enum: Ruční komentáře [default] / Automatické (rozdíly
  tras) / Obojí) - řeší #7955 (klient chce u spojů s vlastním komentářem schovat auto, ne komentář).
  Nad preview i v Ctrl+P (nativní param infra).
- **Model nadmnožina + TD filtr** (viz `feature-design-principles.md §5.2`): Caché už **nedropuje**,
  emituje všechny anotace/legendu otagované `KATEGORIE` + `SPOJ` + `V_ROZSAHU_NEPOZNAMKOVAT`. Schování
  je render-time `NoteVisibilityFilter` v TD → změna se projeví překreslením, bez Caché re-fetch.
- **Filtr** schová jen anotace v rozsahu „nepoznámkovat" (`V_ROZSAHU=1`) dané kategorie; legendu prořeže
  podle kategorie + přítomnosti znaku (sdílený znak mimo rozsah zůstane; „x na znamení" se neprořezává).
  Napojeno na všechny 3 datové cesty (preview, export, tisk reálných dat). Default = legacy (bez regrese).
- Caché legacy chování zachované (nepřeklopena sémantika „nepoznámkovat") - flip je čistě TD volba.

## 2026-06-20 - AD ruční poznámky linky z tabu MHD (Fáze 1)

- ✅ **AD-N7 Komentáře** - `ZastavkoveJrAd` čte ruční komentáře spoje (`MhD,"SeT_SpeC"` přiřazené přes
  per-spoj `MhD_SpeC`) → znak jako anotace u minuty + znak+text do legendy. 1:1 `JRapiLinPrtMhd1` ř.211-216.
- ✅ **AD-N8 Spoje, nepoznámkovat** - helper `NepoznamkovatSpoj` (1:1 `noPoz`): `MHDSPNPZN`=odTČ~doTČ,
  směrová logika dle parity spoje, fallback na konec trasy. Potlačí **ruční komentář** v rozsahu TČ
  (auto-poznámky běží dál - legacy sémantika; flip = Fáze 2 / #7955).
- ✅ **AD-N9 Směrové poznámky** - `MhD,"SeT_MhD"` tam/zpět/vše filtrované dle směru stránky
  (tam→`>`+`<>`, zpět→`<`+`<>`) → legenda. 1:1 ř.440-447.
- Caché-only (C# renderer beze změny - anotace + POZNAMKY existující cestou). Čitelné názvy proměnných
  (ne legacy zkratky). Zbývá: umístění směrových poznámek (legacy nahoru jako směrové záhlaví vs dole),
  Fáze 2 (#7955 opt-in přepínač).

## 2026-06-20 - AD nastavení linky 1:1 (track PRIORITA 2b)

- 📄 **Business analýza nastavení tisku na úrovni linky** - `ad-data-mapa.md`:
  podchyceno, jak legacy čte nastavení linky (taby MHD / MHD-další / dialog Nastavení tisku) přes 3
  readery (`gPrpLin`/`gPrpLinMhd`/`gSetLinPrt^JRapiLin*`), per-feature business význam + storage v
  `^JRL` + zda to máme/nemáme. Klíč: `ALVA.TISKMHD.Data` je jen formátovač, logika je v MAC `JRapi*`.
  Roadmap track **PRIORITA 2b (AD-N1..N15)** namapován 1:1 na §-čísla analýzy.
- ✅ **AD-N1 - Poznámka nad linku** - `ZastavkoveJrAd` nově čte `PATCH,"Head"` → placeholder
  `POZNAMKA_NAD_LINKU` (+ ve schématu). Legacy gPrpLin dta4.
- ✅ **AD-N2 - Typ JR** - `TYP_JR` (dosud prázdné) naplněno z `PATCH,"OBJ"` (1→„VÝLUKOVÝ", 2→„návrh
  jízdního řádu", znění 1:1 `JRapiLinPrtPS3`). Pozn.: zastávková větev OBJ nečte → volitelný
  placeholder, ne automatické razítko.

## 2026-06-17 - Preview: oprava falešného varování o přetečení

- 🐛 **Fix falešného červeného varování o přetečení** - `PreviewCanvas.PageOverflows` přestal hlásit
  přetečení pro JŘ element s pevnou výškou, který se do svého boxu vejde **auto-scalem** (zmenšením písma).
  Dřív: nový StopList (pevných 60 mm) na MHD mock datech (>~12 zastávek) → přirozená výška > box →
  `ContentOverflows=true` → červený rámeček celé stránky + „nevejde na stránku", i když element vizuálně
  sedl (auto-scale bez spodní meze ho vždy zmenší na box). Teď se hlásí **jen reálné geometrické
  přetečení** (element přesahuje spodní hranu sekce/stránky). `ContentOverflows` zůstává jako helper
  („box < obsah"), ale neřídí varování.
- ✅ **Agregovaná kontrola přetečení v tisku** - `PreviewCanvas.CountOverflowingPages(dataPages)` (layout-only,
  bez renderu) projede stránky všech instancí; `PrintDialog` při exportu reálných dat (data už fetchnutá
  z Caché → levné) vypíše „⚠ PDF uloženo, ale X z N stránek přetéká". Žádný fetch navíc, žádné nové UI.
  Ad-hoc kontrola bez generování PDF se případně doplní později na žádost zákazníků.

## 2026-06-17 - UI: font tokeny (škálování přes shell) + auto-width comboboxy + DEV cleanup

- ✅ **UI fonty na EDTheme tokeny** - natvrdo zadané `FontSize`/`FontFamily` v XAML (12 souborů) nahrazeny
  `{DynamicResource EDTheme.FontSize.*}` / `EDTheme.FontFamily`. TD nyní respektuje sdílenou user font
  preferenci z EDISONMAIN2 (FontService už TD inicializuje) a proporcionálně škáluje. Mapování
  9,10→Small · 11→Caption · 12→Body · 13,14→Header · 16,18→Large; `Consolas` (výrazy/glyfy) ponecháno.
- ✅ **Comboboxy v hlavním okně na `Width="Auto"`** (CmbGridSize/CmbSnapGap/CmbZoom/CmbPreviewZoom) +
  `MinWidth="64"` - text se neuřízne a roztáhne se i při zvětšeném fontu.
- ✅ **DEV cleanup** - odstraněn `#if DEBUG` auto-open `kv_ad_test.json` po startu (hardcoded osobní cesta;
  AD agenda hotová) v `MainWindow.xaml.cs` + `--test-detector` blok v `App.xaml.cs` (hardcoded `Downloads\*.PDF`).
  `PdfImport.ScheduleDetectorTest` zůstává jako ruční debug utilita (mimo app flow).

## 2026-06-15 - Wave 4: EDInputForm Image field type + migrace ImageElement (W4-6)

- ✅ **Nový `EFieldType.Image`** v EDControls EDInputForm - upload → base64 + náhled přes
  `Base64ToImageSourceConverter`. Image hodnota se **nikdy neposílá na server** (velký base64;
  `EDInputFormController` ji drží client-side a po Validate echu obnoví).
- ✅ **ImageElement migrován** do generického `ElementEditDialog` - Caché schema `ImageElementEdit.cls`;
  `EDInputFormSchema` switch v MainWindow mapuje `ImageElement → ImageElementEdit`. Uzavírá W4-6.
- Předtím (samostatné commity): EDInputForm Color field type + ComboBox field type (editable + autocomplete).

## 2026-06-12 - Wave 4: parametry tisku přes EDInputForm (Caché-driven)

- ✅ **Tiskový dialog → EDInputForm** - parametry sestavy se renderují přes EDInputForm, jehož schéma
  řídí agenda v Caché (`GetParameters`). Generická dispatch třída `ALVA.TemplateDesigner.EDInputForm.AgendaParams`
  (jedna pro všechny agendy) nested-callem získá param JSON a převede ho na `Form` (typy bool/number/enum/date/text);
  prefill aktuálními hodnotami přes `IN["values"]`. Žádná per-agenda EDInputForm třída - `GetParameters` zůstává
  jediný zdroj definic parametrů.
- ✅ **visibleWhen v C#** - `TemplateParameter.IsVisible(values)` (nový, jediný zdroj logiky) sdílí preview lišta
  i tiskový dialog; viditelnost (např. Hodiny od/do při „Vlastní rozsah") se přepíná živě na polích EDInputformu.
  `ParameterControlVM.UpdateVisibility` zredukováno na delegaci.
- ✅ **enum = required** v `AgendaParams` → List dropdown bez úvodního prázdného řádku (musí být vybráno).
- Odstraněn `ParameterControlVM` z tiskového dialogu + duplicitní nadpis; `CacheAgendaProvider.GetCacheClass` accessor.

## 2026-06-12 - Wave 4: „Vlastnosti sekce" přes EDInputForm + fix prázdného řádku v List

- ✅ **Vlastnosti sekce → EDInputForm** (W4-7 část) - nová Caché schema `ALVA.TemplateDesigner.EDInputForm.SectionEdit`
  (zrcadlí `BuildSectionDescriptors`: Název, Výška, Tisk/Umístění/Přetečení, rámeček, viditelnost).
  `ElementEditDialog` generalizován z `TemplateElement` na `object` (umí teď i sekci/stránku);
  `OpenPropertiesForSection` routuje na generický EDInputForm dialog místo `ElementPropertiesWindow`.
  Vědomé odchylky od starého panelu: vynechané „Datový zdroj"/„Výška řádku" (jen legacy PerDataRow,
  EDInputForm neumí conditional fields), bez clampu výšky, bez live validace výrazu viditelnosti.
- ✅ **EDInputForm List bez prázdného řádku u povinných polí** (EDControls `Field.cs`) - `ListItemsForDisplay`
  přidává implicitní prázdnou položku jen pro **nepovinná** pole (umožní vymazat na „nevybráno");
  u povinných (enumy) ji vynechá. Předtím se prázdný řádek zobrazoval ve všech strict List dropdownech.

## 2026-06-12 - Preview: varování o přetečení stránky + cap roztažení DepartureGrid

Dva nálezy z vizuálního testování na velkých linkách (0480202, 78 stránek):

- ✅ **Varování o přetečení v preview** - když se obsah stránky nevejde (přirozená výška obsahu >
  pevná Height boxu), přetékající stránka dostane **červený rámeček** (2,5px) a status bar zobrazí
  **červený blok** „⚠ Obsah se nevejde na stránku" (tooltip s radou zvětšit PageSize / zmenšit obsah).
  Detekce přes novou abstract `ScheduleElementBase.CalculateHeightFromData` (3 JŘ elementy = `override`)
  + `ContentOverflows` (`natural > Height + 0.5mm`, tj. **jakékoli** přetečení, ne práh). Běží v
  `PreviewCanvas.PageOverflows` **před** renderem (pre-detekce) → `OverflowChanged` callback →
  `HasPreviewOverflow` ve VM → status bar binding. Jen preview, ne v PDF/PNG exportu.
  (Iterace na vzhledu: zkusili jsme nádech přes obsah → podbarvení okolí → celé pozadí → finálně
  jen rámeček + blok v liště, což uživateli přišlo dostatečně výrazné a nejmíň rušivé.)
- ✅ **Cap roztažení DepartureGrid** - `AutoExpandToFillHeight` dostal strop `MaxAutoExpandFactor=1.5`.
  Řídká mřížka (např. linka s odjezdy jen v 1 hodinu) se jinak roztáhla na bizarně obří řádky;
  teď se vyplní max 1,5× přirozené výšky, zbytek místa zůstane prázdný.

## 2026-06-12 - ZastavkoveJrAd: dokončení 1:1 vůči legacy AD + performance

Agenda zastávkového JŘ z dat hlavního JŘ modulu (AD/JRMAIN) dotažena na **datové 1:1**
proti legacy gold oracle (`ALVA.TISKMHD.Data` VolanoZ=JRMAIN) + pročištěna výkonově.

**Funkční 1:1 (vše ověřeno proti gold oracle):**
- ✅ **Rozdíly tras** - `RozdilyTras`, 1:1 `OmTrasa^JRapiLinPrtMhd`: „neobsluhuje zast. X" /
  „jede přes…" / „jede jen do…" / „pokračuje…", marker z písmenné sady od „B", per zastávku.
  (Nejdřív zkusil MHD alg 1002 - ověřením proti gold se ukázal jako špatný, revertován; AD
  používá `OmTrasa`/NZ mechanismus.) Ověřeno 0365001.
- ✅ **POPIS_DOPRAVCE - adresa provozovny** - `NazevDopravce` čte i `$2` (ulice) z `^JRSET("DOP")`.
- ✅ **Filtr atributů zastávek** - `JeViditelnyAtribut` zúžen na jen „na znamení" (`x`); legacy
  `UVOD^JRapiLinPrtMhd` jiné atributy (A=autobus/L=vlak…) do StopListu netiskne. Odstraněna over-emise.
- ✅ **Atributy spoje (bezbariér)** - `BezbarierKonfig` (isinv/iscbe z `^JRSET("PS","LIMES")`, allBBP);
  `inv`=hlava:15→„H" (SpecZnak 72), `cbe`=hlava:22→„F" (70); anotace „specialni" + legenda.
- ✅ **Časy jízdy ve StopListu (TrasaMin)** - jeden sloupec = gold `TrasaMin1` (kumulativní min od
  vývěskové zastávky, jako MHD). Multi-varianta (`TrasaMin1-15`) odložena - chce zásah do C# rendereru.

**Performance (behavior-preserving, výstup byte-identický - 0365001: 680373 znaků, 39 stránek):**
- ✅ Per-linka/per-směr invarianty (`ObsluhovanePoradi`, `BezbarierKonfig`) memoizovány do `^||TiskAd`
  místo full-scanu spojů per stránka.
- ✅ W-trasa cachovaná mapou `poradi→cas` per `cisloTrasy` (`WMapa`) - nahradila lineární `$p` sken
  při každém lookupu; W-helpery (offset, časy, bitmapy, základní trasa) čtou z mapy.
- ✅ Bug fix: prázdná `cisloTrasy` (spoj bez trasy) padala na `<SUBSCRIPT>` - `^||` globály neberou
  prázdný subscript; přidán guard. Stejný `$d()` guard na `served` cache (merge z undef = `<UNDEFINED>`).

**Zbývá k úplnému 1:1 (zdokumentováno §10):** multi-varianta TrasaMin (renderer), číslování omezení
TYPPOZ=1 (firm-config), šířka/layout legendy.

---

> **Starší záznamy (2026-05 a dříve)** → [`changelog-archive-2026-05.md`](changelog-archive-2026-05.md).
