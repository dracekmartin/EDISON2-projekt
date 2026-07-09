# Audit před produkcí — nálezy 2026-07-09

> **Účel:** komplexní audit stavu TD před produkčním nasazením (architektura, extenzibilita,
> UI/UX, agendy — důraz na zastávkový JŘ MHD i AD). Tři paralelní průzkumy kódu (.NET
> architektura, Caché agendy, UI/UX + pravidla R1–R5) + revize dokumentace.
> **Rozhodovací checklist = roadmap sekce „Audit před produkcí"** — tam se odškrtává;
> tento dokument drží detail (file:line, zdůvodnění, doporučený fix). Po odbavení položky
> checklist řádek přesunout do archivu, detail tady zůstává jako reference.

**Celkový verdikt:** architektura zdravá — vrstvy drží (PageLayoutEngine bez SkiaSharp,
renderer bez WPF), JSON wire konzistentní, versioning s forward-compat guardem, testy existují
(305 .NET + Caché TD v pre-push), dirty-flag kompletní, nová agenda = 0 změn v .NET.
Rozdíl MHD↔AD v C# nikde neprosakuje. **Hlavní riziko: dva kontraktní rozkoly ve skalárních
polích zastávkového JŘ** — kolekce prošly revizí (POZNAMKY/SKUPINY_DNU, commity
`f5151ad`/`eb2fe5a`), skaláry ne.

**Co je čisté (ověřeno grepem, žádná akce):** žádný MessageBox ve VM, žádný `_` v dot-notaci,
žádné `&` místo `&&`, žádná `@` indirection, API klíč mimo git a mimo logy, JsonDerivedType
kompletní vůči ElementTypes, dirty-flag všech 6 cest (nová/agenda/import/open/close/AI+PDF).

---

## A. Datový kontrakt zastávkového JŘ — skaláry (🔴 kritické)

### A1. `POPIS_SMER` nese v každé agendě něco jiného

- **Lidsky:** pole „Popis směru" vytiskne u AD šablony název konečné zastávky („Bystrc, zoo"),
  ale u MHD jen písmeno „T" nebo „Z". Kdo si zkopíruje šablonu mezi agendami (nebo použije
  distribuční šablonu), dostane u MHD nesmysl v hlavičce vývěsky.
- **Technicky:** MHD `ZastavkoveJrMhd.cls:951→971` setuje `smerPopis = $s(SmerID=0:"T",1:"Z")`;
  AD `ZastavkoveJrAd.cls:410` setuje název konečné (`posledniNazev`). Linkové agendy to mají
  čistě rozdělené: `SMER` (T/Z) + `POPIS_SMER` (konečná) — `LinkovyJrAd.cls:79-80`.
- **Doporučený fix:** zarovnat MHD na AD/Linkovy — doplnit skalár `SMER` (T/Z) do obou
  zastávkových agend, MHD `POPIS_SMER` naplnit názvem konečné (dohledatelné z trasy
  `^||TiskMHD("trasa",SmerID,…)` — poslední bod). Aditivní změna, gold re-check MHD.

### A2. `NEJEDOU_SPOJE` = dvoje různá data pod jedním klíčem

- **Lidsky:** stejné pole znamená u MHD „text prvního/posledního spoje", u AD „nástupiště".
  Cross-agenda šablona s tímto polem je nesmyslná; i v UI browseru polí má pole u každé
  agendy jiný label (matoucí pro autora šablon).
- **Technicky:** MHD `:977` + helper `CalculatePrvniAndPosledniSpojeText` (`:1620-1629`),
  label „Nejedou spoje" (`:1493`). AD `:296-299` — komentář sám přiznává „legacy misnomer —
  nese NÁSTUPIŠTĚ", label „Nástupiště" (`:1334`).
- **Doporučený fix:** rozdělit aditivně — nový klíč `NASTUPISTE` (AD) + `PRVNI_POSLEDNI_SPOJ`
  (MHD); `NEJEDOU_SPOJE` po přechodnou dobu emitovat duplicitně (existující šablony), ve
  schématu skrýt/označit deprecated. Evoluce kontraktu jen aditivně (pravidlo R3).

### A3. MHD mock lže o `POPIS_SMER`

- **Lidsky:** návrhář v preview ukazuje „Bystrc, zoo", ostrý tisk vytiskne „T". Autor si šablonu
  nastaví podle preview a v produkci vyjde jinak.
- **Technicky:** `ZastavkoveJrMhd.cls:1646` mock `POPIS_SMER = "Bystrc, zoo"` vs. reálný
  GetData `:971` „T"/„Z". Řeší se společně s A1 (po fixu A1 bude mock zase pravdivý).

### A4. AD GetData vrací pole nedeklarovaná ve schématu

- **Lidsky:** agenda posílá data, o kterých návrhář „neví" — pole nejsou v seznamu polí agendy,
  takže je autor šablony nemůže objevit; a jedno z nich je navíc pole-v-poli, se kterým generický
  binding nemusí počítat.
- **Technicky:** `POPIS_ZASTAVKA_CIS` (`ZastavkoveJrAd.cls:409`), `POPIS_SMER_CIS` (`:411`),
  `VARIANTY_TRAS` (`:448` — **array ve `values`**, jinak skalární sekci) nejsou
  v `AddStandardJRFields` (`:1316-1335`); `TRASA_MIN` v kolekci ZASTAVKY (`:401`) chybí ve
  wire-only výčtu komentáře (`:1341`) i v `AddStandardJRCollections`. MHD tato pole neprodukuje.
- **Doporučený fix:** deklarovat ve schématu (nebo doplnit do dokumentovaného wire-only výčtu),
  rozhodnout, zda `_CIS` varianty má emitovat i MHD.

---

## B. .NET — viditelné bugy editace a rizika (🟠 střední)

### B1. DepartureGrid: neúplný layout-cache fingerprint → „mrtvé" vlastnosti v náhledu

- **Lidsky:** uživatel v panelu změní např. „Výšku řádku" nebo „Oddělovač skupin" a náhled se
  nezmění (dokud nepřepne instanci / nedá F5). Sousední „Šířka minuty" přitom funguje živě —
  vypadá to jako rozbité.
- **Technicky:** `ComputeLayoutFingerprint()` (`DepartureGridElement.cs:179-183`) nezahrnuje
  `DataRowHeight`, `AnotaceGap`, `OdjezdGap`, `GroupSeparatorWidth`, `HourColumnPerGroup`,
  `ShowSkupinaDnuHeader`, `SkupinaDnuId` — všechny přitom vstupují do layoutu (`:287-297`,
  `:343`, `:363`). Preview re-renderuje se stejnou referencí `_realPreviewData` → cache se
  neinvaliduje. Systémové: fingerprint je ručně udržovaný seznam → drift při každé nové property.
- **Doporučený fix:** doplnit chybějící properties + zvážit robustnější invalidaci (verze modelu /
  dirty-bit v setterech místo ručního výčtu).

### B2. DepartureGrid: overflow varování vs. auto-scale počítají jinou „přirozenou výšku"

- **Lidsky:** červené varování „obsah se nevejde" a skutečné automatické zmenšení fontu se můžou
  rozejít — varování hlásí přetečení, ale scale zmenšuje podle jiného čísla (nebo naopak zmenší
  zbytečně).
- **Technicky:** `Render` počítá `scaleNaturalH` z font-based `ComputeRowH(DataStyle.FontSize)`
  (`DepartureGridElement.cs:516-518`), `CalculateHeightFromData` (zdroj `ContentOverflows`)
  z `DataRowHeight ?? ComputeRowH(...)` (`:1131`). Rozchod nastane při explicitní `DataRowHeight`.
- **Doporučený fix:** jeden zdroj přirozené výšky pro oba (sjednotit na `CalculateHeightFromData`).

### B3. `CapabilityCatalog` (AI průvodce) driftnul od palety — nezná LineTable

- **Lidsky:** u linkové agendy paleta správně nabízí tabulku linky (LineTable), ale AI asistent
  radí jen zastávkové elementy — UI a AI si protiřečí.
- **Technicky:** `Services/CapabilityCatalog.cs:204-209` string heuristika
  `Contains("JR")||Contains("MHD")…` + hardcoded `ScheduleTypes` (`:185-188`) bez LineTable.
  Paleta jde data-driven přes `AgendaCapabilities.ScheduleElementTypes(elementKind)`.
- **Doporučený fix:** `CapabilityCatalog` napojit na `AgendaEntry.ElementKind` +
  `AgendaCapabilities`, smazat lokální seznam i heuristiku.

### B4. Nový element = ~8 ručně synchronizovaných míst, bez strážce

- **Lidsky:** přidání nového typu elementu vyžaduje pamatovat na ~8 souborů; když se jedno
  zapomene, nic to nehlásí — projeví se to až dírou ve funkci (přesně to se stalo v B3).
- **Technicky:** ElementTypes, JsonDerivedType, `CreateElementByType` + `GetDefaultElementSize`
  (2 switche, `TemplateDesignerViewModel.cs:1590-1627`), `SkiaHeightCalculator.cs:20-25`,
  `CapabilityCatalog` (3 místa), menu/paleta (`MainWindow.xaml.cs:1177,1674` + XAML).
  Skill `new-element` sekvenci popisuje, ale nic ji nevynucuje.
- **Doporučený fix:** test „každý `JsonDerivedType` má záznam v každé tabulce" (levné);
  dlouhodobě centrální metadata tabulka `type → {factory, defaultSize, friendlyName, kind}`.

### B5. F5 refresh neinkrementuje generation counter

- **Lidsky:** když uživatel dá F5 ve chvíli, kdy zrovna běží načítání po přepnutí instance,
  můžou se na okamžik zobrazit starší data (vyhraje pomalejší úloha).
- **Technicky:** `RefreshData()` (`TemplateDesignerViewModel.cs:166-171`) volá fetch bez
  inkrementace `_previewDataGeneration`; dvě úlohy se stejnou generací → last-writer-wins.
  `RefreshDataForCurrentInstanceAsync` (`:173-222`) guard má.
- **Doporučený fix:** inkrementovat generaci i v `RefreshData` (1 řádek), příp. CTS per fetch.

### B6. Reset stránky (commit `37a80df`) — křehký string kontrakt + `async void`

- **Lidsky:** nová funkce „změna rozměru stránky = reset" detekuje změnu rozměru podle textového
  klíče názvu pole; kdyby se pole ve formuláři přejmenovalo, reset se tiše přestane spouštět
  (bez chyby). Plus drobnost: storno resetu zahodí i současně editované okraje.
- **Technicky:** `MainWindow.xaml.cs:1580-1583` `ParseMm(fv, "PageSize.Width"…)` — klíč
  z `ElementFormBinder.Snapshot`; nesedící klíč → fallback stará hodnota → `dimsChanged=false`.
  `OpenPropertiesForPage` je `async void` mimo event handler (`:1547`).
- **Doporučený fix:** sdílená konstanta property-path + log při chybějícím klíči; `async Task`
  s try/catch u volajícího.

### B7. R1: první `GetSchema` synchronně na UI threadu

- **Lidsky:** otevření šablony agendy, jejíž schéma ještě není v cache, zamrzne UI na dobu
  prvního HTTP dotazu do Caché (na VPN/pomalé síti klidně sekundy + „Neodpovídá").
- **Technicky:** `RefreshFieldsList` (sync void) → `SchemaProvider.GetSchema`
  (`TemplateDesignerViewModel.cs:1980/1992`), volané z Open/import/F5 cest. Provider sám
  v komentáři říká „první GetSchema musí volající obalit Task.Run — R1"
  (`CacheAgendaProvider.cs:19`). Jediné reálné R1 porušení v projektu; příbuzné:
  `LoadAgendaParameters` (`:2020`, cachované, jen první hit) a `GetAvailableSkupiny`
  (`:947`, bez cache vůbec — sourozenci cache mají).
- **Doporučený fix:** `RefreshFieldsList` → `async Task` + `await Task.Run(...)`, nebo
  pre-fetch schématu na pozadí; `GetAvailableSkupiny` doplnit cache.

---

## C. Caché — hygiena a performance (🟠 střední / 🟡 nízké)

### C-a. Working global úklid nesymetrický (MHD i AD)

- **Lidsky:** pomocná „pracovní paměť" tisku se po chybě (MHD) nebo vůbec (AD) neuklízí —
  u poolovaných serverových procesů drží data v paměti mezi požadavky. Neshodí to nic, ale je
  to skrytá zátěž a možný zdroj matoucích stavů při ladění.
- **Technicky:** MHD `k ^||TiskMHD` na konci je **uvnitř try** (`ZastavkoveJrMhd.cls:362`) —
  při chybě se přeskočí (self-healing jen díky killu na začátku `:278`). AD `k ^||TiskAd` jen
  na začátku (`ZastavkoveJrAd.cls:216`), na konci nikdy.
- **Doporučený fix:** kill symetricky po try/catch (před `ret`) v obou agendách.

### C-b. MHD performance: chybí memoizace, kterou AD má

- **Lidsky:** MHD tisk volá pro každou stránku znovu dotazy, které se nemění (název zastávky,
  dopravce) — u velkých dopravních podniků (DPCB) to bude znát na době exportu. AD si tohle
  cachuje, MHD ne.
- **Technicky:** `GetNazevHrl` bez cache v ~O(zastávky²×směry) smyčce
  (`ZastavkoveJrMhd.cls:1006`, + `:956`, `:1327`); `PopisDopravce` (legacy MAC
  `$$gIdFa^MlJFapi`/`$$gDopFa^JRapi`, cross-NS) per stránka (`:967`); ENUMS barvy per anotace
  (`:1109-1110`, `:1160-1161`). AD vzor: invarianty v `^||TiskAd` (`:601`, `:641`, `:668`).
- **Doporučený fix:** stejný memoizační vzor jako AD (`^||TiskMHD("nazev",…)`,
  `("config","PopisDopravce")`). Behavior-preserving, gold ověřitelné.

### C-c. `AgendaParams.FetchParamsJson` maže i `ERR` — nested chyba se tiše ztratí

- **Lidsky:** když selže načtení parametrů agendy, uživatel místo chybové hlášky dostane
  prázdný seznam parametrů — chyba zmizí beze stopy.
- **Technicky:** `AgendaParams.cls:99,104` `k ^||ML("OUT")` vcelku → smaže ERR z nested
  `GetParameters`. `EDList/Instance.cls:91,102` to dělá správně (ERR zachovává).
- **Doporučený fix:** čistit OUT po klíčích se zachováním ERR (kopie vzoru z Instance.cls).

### C-d. `GetParameters` — `$$$FillParams` mimo try/catch, bez `s err=""`

- **Technicky:** AD `ZastavkoveJrAd.cls:106` (+ Linkovy) volá `$$$FillParams` bez konvenčního
  obalu; MHD ho nevolá vůbec (nekonzistence — nepotřebuje ho ani AD, `AddStandardJRParameters`
  IdFa nečte). Pád FillParams → neošetřená chyba mimo ERR konvenci.
- **Doporučený fix:** buď obalit konvenčně, nebo FillParams z `GetParameters` vypustit.

### C-e. AD `GetMockData` čte živá firemní data (vlastní TODO)

- **Lidsky:** náhled v návrháři u AD agendy závisí na tom, jaká data má přihlášená firma —
  u zákazníka bez vhodné první linky se preview rozbije nebo bude divné; a náhled honí ostrý
  tiskový engine (pomalé).
- **Technicky:** `ZastavkoveJrAd.cls:1281-1311` — `GetMockData` volá živě
  `BuildStrankaZastavky` nad první linkou; TODO v kódu: „před produkčním nasazením vrátit
  statický vzorek". (= známé T5 riziko z auditu 2026-07-02.)
- **Doporučený fix:** zmrazit reprezentativní JSON výstup jako statický mock (gold linka).

### C-f. Nízké (Caché)

- `q $$$OK` vs `ret $$$OK` nekonzistence (konvence TD = `ret`) — MHD `:57,76,101,114,220`,
  AD `:56,75,100,116,198` vs `GetData` správně.
- Export split `PER_STOP`/`PER_LINE` (`AgendaParams.cls:68`) nefiltrován dle `ElementKind` —
  u FAKTURY nesmyslná volba v dialogu.
- `AgendyRegistr` bere jen přímé subclassy `BaseAgenda` (`AgendyRegistr.cls:39,77`, komentář
  to přiznává) — mezistupeň dědičnosti (např. budoucí `BaseJrAgenda`) by agendy z registru
  vyhodil. Vědět o tom při refaktoru C8.
- MHD stále volá legacy MAC (`MakeOmezTrasaTxMH29052025` per odjezd `:1259`) — defenzivně
  obalené; dluh patří do C8 (dedup Caché enginů), ne sem.

---

## D. UI/UX + dokumentace (🟡 nízké, rychlé)

### D1. Zkratky drift — dokumentace vs kód

- **Lidsky:** tabulka zkratek (autoritativní, `ux-conventions.md §3`) slibuje „holé ↑/↓ = přesun
  sekce", ve skutečnosti je to `Ctrl+Shift+↑/↓`; skutečná zkratka v tabulce chybí.
- **Technicky:** `MainWindow.xaml.cs:1843-1849` (`ctrl && shift && ActiveSection`);
  + nedokumentované modifikátory posunu elementu (Shift=velký krok, Ctrl=jemný, `:1864-1870`).
- **Fix:** aktualizovat tabulku (kód je v pořádku).

### D2. 6 dialogů řeší OK/Save přes code-behind `Click` místo VM Command

- **Technicky:** `ElementEditDialog.xaml:19`, `DepartureGroupEditorDialog.xaml:115`,
  `DynamicTableEditorDialog.xaml:161`, `StopColumnsEditorDialog.xaml:140`,
  `LineTableColumnsEditorDialog.xaml:121`, `SaveToCacheDialog.xaml:20` — proti závaznému vzoru
  (`architecture.md §Vzor pro každé okno`); druhá polovina dialogů jde správně přes `OkCommand`.
- **Fix:** sjednotit při nejbližším zásahu do každého dialogu (ne big-bang).

### D3. Drobné UI/AI

- `PrintDialog.xaml:37` literál „Zavřít" místo `{StaticResource EDStrings.Close}` (jediný dialog).
- API klíč v plaintext `TextBox` (`AiTemplateAssistantDialog.xaml:114`) — `PasswordBox`.
- `AnthropicApiClient.cs:618-630` zapisuje `ai_generated_template_debug.json` do instalačního
  adresáře při každé generaci (v `C:\Edison\` tiše selže / zaneřáďuje) — přesunout do temp
  nebo jen DEBUG build.
- `CellStyleEditor.Converters.cs:55` jediný zcela prázdný `catch {}` bez komentáře.
- Dvojí mechanismus renamu agendy: `"MHD"→"ZASTAVKOVY_JR_MHD"` post-deserializační remap
  (`TemplateSerializer.cs:120-147`) vs `KNIZNI→LINKOVY` verzová migrace — příští rename
  sjednotit do migračního pipeline.

### D4. Dokumentační drift

- Roadmap krok 42b/C2 vede symbolový font jako ⬜ — 9.7. hotový pro **zastávkový** JŘ
  (changelog); přeformulovat na zbývající linkovou část (multichar/kompozity, `ZNAK` sloupce
  LineTable).
- `analyza-stav-…md` T1 („nula testů") už neplatí — z T1 zbývá jen C9 (gold gate hardening);
  dokument je evergreen, ale odkazy z roadmapy na T1 zpřesnit.

---

## Mimo checklist — dlouhodobé (bez akce teď)

- **God objects trvají** (VM 2761 ř., MainWindow 2886 ř.) — známé T4; pravidlo „nové featury
  tam nepřidávat" platí dál.
- `StopScheduleElementBase.InvalidateCache()` mrtvý kód + DataSource-setery neinvalidují
  (3× duplikovaný cache pattern StopSchedule/DepartureGrid-layout/LineTable) — srovnat při
  R1 extrakci jádra (C3 roadmapy).
- `NotesLegendElement.CalculateHeightFromData` měří wrap na fixním DPI (`:248`) vs render na
  reálném — hraniční šířky, nízké.
- MHD typ `CISLO_LINKY` string vs AD number při `type=number` ve schématu — kosmetika.
