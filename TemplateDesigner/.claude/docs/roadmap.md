# Roadmap

## 🔁 Pracovní postup (krok po kroku)

Každá položka projde tímto cyklem — nevynechat ani jeden krok:

1. **Projít** — připomenout co přesně má krok dělat, kontext a závislosti
2. **Validovat** — ověřit, že to dává smysl dělat teď (priority, závislosti, scope)
3. **Implementovat** — kód, Caché, XAML — dle pravidel v CLAUDE.md
4. **Otestovat** — ručně projít golden path + edge cases; u UX ověřit v běžícím buildu
5. **Aktualizovat docs** — changelog + roadmap (přesun do archivu) + CLAUDE.md pokud se mění architektura
6. **Další krok** — posunout `⬅️` na následující položku

---

## 🎯 Aktuální focus

**v1.0 demo balíček HOTOV** (2026-05-15). EDList + EDFilter v1 uzavřen (2026-05-19). Caché namespace refactor hotov (2026-05-19). UI polish uzavřen (2026-05-20). **Wave 1 + 1.5 + 2 + 3 (per-user preferences + agenda registry + template storage s versioning) HOTOVY** (2026-05-20 — 2026-05-25). Detaily v [`changelog.md`](changelog.md) a [`roadmap-archive.md`](roadmap-archive.md).

**Stav UI/theme** (po 2 dnech práce):
- EDTheme design tokens (33 barev + typo + spacing) v EDControls — runtime swap přes DynamicResource
- EDStyles implicit styles pro Button/TextBox/ComboBox/CheckBox/TabItem/MenuItem/Menu/ContextMenu/Separator
- TD MainWindow + 10 dialogů + 5 EDControls komponent na tokens
- Default Window icon (Edison Molecule) všude přes class handler
- 3-button popup (IPopupDisplayer.ConfirmYesNoCancel) — náhrada Win32 MessageBox
- Toolbar restructure (Mřížka & snap / Zoom / Náhled podskupiny + CmbScale do Upravit menu)
- Y-zarovnání designer ↔ preview přes TopBufferPx 60px
- Canvas perf: ClipRect + viewport culling pro grid + cached shadow paint
- Zoom limit 500% (vyšší vyžaduje virtualized rendering — viz roadmap)

**🎯 Cíle s ohledem na prezentace (statická ~19. 6. 2026, živá pro zákazníka ~26. 6. 2026)** — pořadí priorit dohodnuto 2026-06-12:

1. **Odladit zastávkový JŘ (AD)** — *hlavní věc k ukázání*. Datově 1:1 + výkonově **hotovo** (PRIORITA 2 níže). Zbývá průběžné ladění vzhledu/UX podle testování na velkých linkách (poslední kolo: varování o přetečení + cap roztažení DepartureGrid — viz changelog 2026-06-12) + odložené nice-to-have (multi-varianta TrasaMin přes C# renderer, číslování omezení TYPPOZ=1, vizuál legendy).
2. **Navazující věci ohledně komponent** — EDInputForm (Wave 4) a další UI komponenty, jak vyplyne z ladění JŘ.
3. **Helpdesk požadavky primárně pro AD, řešitelné bez kolegů** — Fáze A.2-D série: vlastní komentář spoje přebíjí auto-poznámku (#7955), skrýt „x — zastávka na znamení" když je sloupec hidden (#7161), vypnutí auto-poznámky bezbariér (#7161). Viz Fáze A.2-D níže + [`customer-requirements-zastavkovy-jr.md`](customer-requirements-zastavkovy-jr.md).
4. **AI pomocník (stretch, když zbude čas)** — validace PDF importu + AI asistent (pasivní → aktivní). Detail v sekci „🤖 AI integrace" níže.

> **Pozn.:** PRIORITA 1/2 a ⬅️ markery v sekcích níže pocházejí z dřívějšího plánu (před prezentacemi) — autoritativní pořadí je tento číslovaný seznam. **Výkonový rest** (MHD `VypocitatBitmapyObsluhy`) zůstává rizikový samostatný krok mimo prezentační scope.

### ⬅️ PRIORITA 1 — Wave 4: dokončit EDInputForm migraci (🟡 rozpracováno)

EDInputForm je živý a generický základ funguje. Cíl: **co má být EDInputForm, ať JE EDInputForm** — logika na jednom místě, ne půl na půl mezi PropertyDescriptors a Caché schema.

**Hotovo (2026-05-25 — 27):** EDInputForm visual upgrade + EDComponentToolbar (sdílený s EDList) + generický `ElementEditDialog` (reflexe přes property-path keys) + 5 elementů migrovaných (Text/Line/Rectangle/Barcode/NotesLegend) + SaveToCacheDialog. Detail Wave 4 sekce níže.

**Zbývá projít VŠECHNO a předělat kde to dává smysl:**
- **ImageElement** — potřebuje nový `Image` field type v EDInputForm (upload→base64+náhled)
- **Section / Page properties** — generalizovat `ElementEditDialog` z `TemplateElement` na `object` (`ElementPropertyAccessor` už je generický)
- **Simple dialogy** — SaveSectionTemplate, NormalizePositions
- **Komplexní elementy (StopList / DepartureGrid / TableElement)** — rozdělit: **tab „Obecné" → EDInputForm**, tabulková / per-column část buď nechat custom (CellStyleEditor / grid) nebo vyřešit extra. Žádné půl-na-půl — obecné properties patří do EDInputForm.

### PRIORITA 2 — Zastávkový JŘ pro data z linkových JŘ (ne jen MHD) — ✅ DATOVĚ 1:1 + VÝKONOVĚ HOTOVO (2026-06-12)

Nová agenda `ZastavkoveJrAd` (`ZASTAVKOVY_JR_AD`, „Zastávkový JŘ (AD)") — reimplementace legacy `ALVA.TISKMHD.Data` větve `VolanoZ=JRMAIN` (linková/meziměstská data z `^JRL`/`^JRZ`/`^JRSET`, per-namespace bez IdFa). Čistý refaktor, ne wrapper (analogicky k MHD migraci).

**Hotovo (2026-06-02 — 06-12), vše 1:1 ověřeno proti gold oracle:** Fáze 0-2 (data model, skeleton, GetData fázový engine) + 1:1 drobnosti (názvy zastávek, datumy s rokem, „jede/nejede" omezení, **POPIS_DOPRAVCE vč. adresy provozovny**, LOKALNI_OZNACENI/TITULEK/INFORMACE, FTL 0-3 sloupcový model, prázdninové dělení) + **rozdíly tras** (`RozdilyTras`, 1:1 `OmTrasa^JRapiLinPrtMhd`: „neobsluhuje zast. X", marker od „B" — NE MHD alg 1002, ten byl revertován) + **atributy zastávek** (filtr jen „na znamení", jako legacy UVOD) + **atributy spoje** (bezbariér H/F, `BezbarierKonfig`) + **časy jízdy StopList** (jeden sloupec = gold `TrasaMin1`). Ověřeno na 0410101/0425017/0320144/0320993/**0365001** (rozdíly tras + bezbariér + časy). **Performance:** per-linka invarianty + W-trasa cachovány do `^||TiskAd` (behavior-preserving, byte-identický výstup). Detail §10 + changelog 2026-06-12.

**Zbývá k úplnému 1:1 (zdokumentováno §10, nice-to-have):**
- **Multi-varianta TrasaMin** (`TrasaMin1-15` = víc časových sloupců per varianta trasy) — vyžaduje zásah do **C# StopListElement rendereru** (dnes jeden sloupec). Pro běžné linky s jednou trasou je jednosloupcová verze 1:1.
- **Číslování omezení TYPPOZ=1** (legacy pevná čísla „29" vs naše sekvenční „1") — firm-config.
- Šířka/layout legendy (vizuál) — na konec.
- **DEV cleanup před produkcí** — `#if DEBUG` auto-open `kv_ad_test.json`, zvážit statický `GetMockData`.

**Performance rest (samostatný krok):** MHD agenda `VypocitatBitmapyObsluhy` má 3 průchody `obsluhaKonec` + vnořené smyčky → potenc. O(bodů³). **Rizikový zásah do ověřeného MHD enginu** — udělat zvlášť s re-verifikací proti MHD gold oracle. (AD agenda už per-stránka neefektivnosti nemá.)

**Reference:** [`legacy-schedule-generators.md`](legacy-schedule-generators.md) — mapování legacy Caché generátorů na typy JŘ.

### Helpdesk tickety (Fáze A.2-D) — NIŽŠÍ priorita

TRANSDEV/KORID/DPKV suspended tickety v `Agendy/ZastavkoveJrMhd` (auto-poznámky bugs, NZ bug, KORID přípojové řádky, …) — tabulka A.2.D níže. **Až po prioritě 1 + 2.**

**Případné defer items pro budoucnost** (revisit on real user request):
- Wave 3 auto-snapshots history / audit log / thumbnails
- Wave 5 další agendy (knižní/meziměstský JŘ) — až bude business request

**Mimo aktuální focus** (až bude potřeba):
- Plná sada Filter polí (EDFilter rozšíření)
- Tier 3 EDInputForm refactor (`$lb()` → JSON envelope + chybějící typy polí)
- Tier 4 TD editor migrace na EDInputForm
- Virtualized canvas rendering (zoom >500%)

**Reference docs:** [`EDControls/DOCS/`](../../../EDControls/DOCS/) — `00-overview`, `01-wire-formats`, `02-EDList`, `03-EDFilter`, `04-EDTheme`.

---

## 📖 Závazné reference před implementací

| Doc | Kdy číst |
|---|---|
| [`feature-design-principles.md`](feature-design-principles.md) | **Před každým novým requestem** — kódová + UX rozhodovací mřížka, klasifikace requestu (typ 1/2/3), per-customer divergence, anti-patterns |
| [`customer-requirements-zastavkovy-jr.md`](customer-requirements-zastavkovy-jr.md) | Při práci na zastávkovém JŘ — konkrétní požadavky zákazníků s citacemi a gap analýzou |
| `.claude/rules/mvvm-patterns.md` | Při dotyku stylingu / dialogů / per-column editorů |
| `.claude/rules/ux-conventions.md` | Při přidávání nového dialogu / okna / keyboardové zkratky |
| `.claude/rules/new-element-checklist.md` | Při přidání nového typu elementu |
| §5 Architektonických pravidel níže | R1–R5 (async, errors, JSON, MVVM, versioning) — závazné pro Caché migraci |

---

## 📐 5 Architektonických pravidel

Tato pravidla jsou závazná pro **veškerou novou práci** — zejména pro Caché migraci. Jsou záměrně obecnější než code style, protože mají chránit proti drahým rework cyklům pozdě v projektu.

| # | Pravidlo | Proč |
|---|---|---|
| R1 | **Async Caché volání pro očekávaně pomalé operace** — pro Caché volání kde očekávaná doba >500ms (velké listy, složité queries, batch operace, nahrávání) vždy `async Task` + `await Task.Run(() => mlRest.Query(...))`. Pro typické fast operations (foundational komponenty s 50–300ms loady jako EDList, jednoduché lookupy) sync OK — async by přinesl spíš flicker než benefit. Při neznámé velikosti pesimisticky async. | Legacy BIL/BDG bylo 20 let synchronní a fungovalo — důkaz že je to akceptovatelné pro typické workflow. Naopak sync pro 1s+ operaci zamrzne UI a Windows zobrazí „Not Responding". Pravidlo chrání proti druhému; první je validní výjimka. |
| R2 | **Chyby přes `IPopupDisplayer` z EDControls** — žádný `MessageBox.Show` ani silent catch. Používat existující `EDControls.EDViewModel.IPopupDisplayer` (metody `Error(ex)`, `Confirm`, `Info`, `ConfirmClose`) přes DI nebo `EDViewModel` base. | Bez jednotné cesty se chyby v distribuci ztratí. `IPopupDisplayer` je firemní standard — už ho používá `EDViewModel.Command(...)` automaticky. |
| R3 | **CVar named IN + 4 OUT styly podle obsahu** — IN vždy CVar pojmenované klíče; OUT podle typu: CVar scalars (triviální), JSON v slotu (entita), JSON multi-slot (list), JSON envelope (composite, opt-in pro Tier 3). Plný standard, decision tree, příklady a checklist: [`EDControls/DOCS/01-wire-formats.md`](../../../EDControls/DOCS/01-wire-formats.md). | CVar je nativní zrcadlo Caché globálů (1 řádek per parametr), JSON pro strukturovaná data dává typovou bezpečnost přes `record` typy v .NET. Asymetrie není kompromis — odráží přirozenost obou stran. |
| R4 | **ViewModely bez WPF referencí** — žádný `using System.Windows` ani `MessageBox.Show` v `*ViewModel.cs` | MVVM princip, testovatelnost, možnost pozdějšího portu/unit testů. Dialogy přes `IPopupDisplayer` (R2). |
| R5 | **Template versioning je prerekvizita persistence** — dokud není `TemplateMigrator` + version field v JSON, neukládá se nic do Caché | Bez migrace se nové šablony nespustí na starším buildu → zničené ranní otevření souboru. Nelze to retro-fit po nasazení. |

**Předpoklad:** aplikace poběží nad Caché — není cíl fungovat offline, ne každá API potřebuje in-memory fallback. Fallback se řeší jen tam, kde to má konkrétní business smysl (např. poslední otevřené soubory v paměti během session).

**Kde jsou pravidla ověřitelná:**
- R1: review PR → `grep "MLRest\." *ViewModel*.cs` najde synchronní volání. U každého ověřit očekávanou dobu — fast operace OK, slow musí být async.
- R2/R4: `grep "MessageBox\." **/*ViewModel.cs` musí vracet 0 hitů
- R3: nové API metody: IN `$g(^||ML("IN","klic"))`, OUT podle obsahu (decision tree) — viz [`EDControls/DOCS/01-wire-formats.md`](../../../EDControls/DOCS/01-wire-formats.md)
- R5: Wave 3 nemůže začít bez hotového H-18/H-19 (versioning a migrator)

---

## 📋 Co je potřeba dělat

### 🔴 Fáze A.2-D: Caché business logic ⬅️ AKTUÁLNÍ PRIORITA

Suspended tickety TRANSDEV/KORID/DPKV — Caché-side auto-poznámky a logika spojů v `ZastavkoveJrMhdAPI`. Plný kontext, citace a gap analýza viz **[`customer-requirements-zastavkovy-jr.md`](customer-requirements-zastavkovy-jr.md)**.

> **Pozn.:** Fáze A.2-A..C (StopList / DepartureGrid / NotesLegend UI úpravy) je hotová — viz Archiv níže. Zbývá A.2-D série (Caché logika) + A.2-A5/A6/B4 (odložené pro datový průzkum / diskuzi s kolegy).
>
> **Priorita:** Wave 2 → H-18/H-19 → Wave 3 finishing → Fáze A.2-D. Caché Migration foundation se uzavře dřív, než se vrátíme k zákaznickým ticketům — eliminuje riziko rework po pozdním přechodu.

#### A.2.D — Caché `ZastavkoveJrMhdAPI` úpravy

| Krok | Tiket | Zákazník | Co | Stav |
|---|---|---|---|---|
| A2-D1 | #7947 | TRANSDEV | Nový text template „spoj jede až do XY" pro spoje **prodlužující** základní trasu (symetrické k existujícímu „končí v zast. X") | |
| A2-D2 | #7955 | TRANSDEV | Vlastní komentář spoje musí přebít auto-poznámku rozdílů (suppress auto pokud má spoj custom text) | |
| A2-D3 | #7955 | TRANSDEV | **Bug** — „Spoje, nepoznámkovat" maže i inverzní znaménko (NZ); izolovat NZ od ostatních poznámek | |
| A2-D4 | #7161 | TRANSDEV | Skrýt „x — zastávka na znamení" pokud sloupec s `x` je hidden (kontrakt TD ↔ Caché — rozhodnutí kde se filtruje) | |
| A2-D5 | #7161 | TRANSDEV | Vypnutí auto-poznámky „všechny spoje s bezbariérovým vstupem" — parametr v `GetParameters` + checkbox v PrintDialog | |
| A2-D6 | #11300 | KORID | Skrýt prázdné přípojové řádky — vyžaduje **novou** doménovou strukturu `Pripoj` v `GetData` + nový element `ConnectionsListElement` | |

#### Odložené z A.2 (na rozhodnutí s kolegy)

| Krok | Tiket | Co | Důvod odložení |
|---|---|---|---|
| A2-A5 | #7937 | StopList per-row indent per směr / per Tč | Caché vrací stub `odsazeni=0`; chybí datový tok z `ALVA.MHD.ZST` (legacy „dta 10"?). Není přímo TD práce. |
| A2-A6 | #7959 | Filtr neobsluhovaných zastávek | Pravděpodobně už hotovo v Caché — vyžaduje verifikaci s reálnými TRANSDEV daty (linka 876xx se školní zastávkou) |
| A2-B4 | #8828 | Per-sloupek zastávky — samostatná vývěska | Vyžaduje (a) datový průzkum kde v `^ALVA.MHD` leží info o sloupcích, (b) business decision (identifikace, default chování). ~100 % práce v Caché. |

#### A.2 — Status komunikace s klienty

Při uzavírání jednotlivých tiketů odkazovat na **konkrétní featuru v TD** + verzi releasu. Texty M-line v helpdesku (`viz customer-requirements-zastavkovy-jr.md §H`) zákazníkům slíbily „nový tiskový modul" / „Studio JŘ" na polovinu 2026.

---

### 🔵 Caché Migrace — 5 Vln

Postupný přesun state a logiky z .NET na Caché. Cíl: žádné „zjistili jsme že by se muselo přepsat všechno" překvapení.

**Stav vln:**
| Vlna | Název | Stav | Cíl |
|---|---|---|---|
| 1 | Per-user state | ✅ Hotová (2026-05-20) | MainState (zoom/grid/snap/splittery/tab/preview), RecentFiles, InstanceDefaults per šablona |
| 2 | Shared catalogs | ✅ Hotová (2026-05-20) | W2-1 ✅ Agenda registry (auto-discovery). W2-2 Section templates skipnuto — funkčně překryto Wave 3 Template starters. Element defaults + company palette skipnuté |
| 3 | Template storage | ✅ Hotová (2026-05-25) | CRUD (✅), schema versioning + migrator framework (✅), explicit user versions via Save As (✅), undo within-session (✅). Auto-snapshots / audit / thumbnails skipnuté (revisit on real request) |
| 4 | Form-driven UI | 🟡 Rozpracováno (2026-05-27) | EDInputForm místo PropertiesPanel. 5 elementů hotovo přes generický `ElementEditDialog` (reflexe). Zbývá Image/Section/Page/komplexní elementy |
| 5 | Business logic per agenda | 🟡 Pattern hotový pro MHD + FAKTURY, čeká na další agendy | Čisté rozdělení agenda-specific logiky |

**Graf závislostí:** Wave 1 → 2 → 3 → 4 (sekvenčně), Wave 5 běží paralelně nezávisle.

**Foundation hotová** (může se použít napříč všemi vlnami):
- ✅ `IUserPrefsService` + Caché `ALVA.EDControls.UserData` — generic per-user state (F1, 2026-05-15)
- ✅ Wave 1 typed konzument — `TdPreferencesService` se 3 scopes pro TD (2026-05-20)
- ✅ Registr scope ownerships — [`EDControls/DOCS/05-userdata-scopes.md`](../../../EDControls/DOCS/05-userdata-scopes.md)
- ✅ Wire formát standard — CVar named IN + 4 OUT styly (DOCS/01-wire-formats.md)
- ✅ `MLRestJson.CallAsync<TReq, TResp>` helper — připraven pro Wave 4 composite endpointy
- ✅ EDList komponenta (Wave 3 list konzumenti používají)

> **Wave 1 + 1.5 hotové ✅ 2026-05-20** — detail v [`roadmap-archive.md`](roadmap-archive.md).

---

> **Wave 2 hotová ✅ 2026-05-20** — W2-1 Agenda registry → Caché auto-discovery. W2-2 Section templates skipnuto (funkčně překryto Wave 3 Template starters; bez user requestu). Detail v [`roadmap-archive.md`](roadmap-archive.md).

---

> **Wave 3 hotová ✅ 2026-05-25** — CRUD (`API.Sablony`) + schema versioning framework (`TemplateMigrator` Fáze 1). Auto-snapshots history, audit log, thumbnails, import wizard skipnuté (revisit on real request). Detail v [`roadmap-archive.md`](roadmap-archive.md).

---

#### 🟠 Wave 4 — Form-driven UI (EDInputForm) — 🟡 ROZPRACOVÁNO

Nahrazení `PropertiesPanel` + `PropertyDescriptor` systému standardním `EDInputForm` z EDControls.

**⚠️ Skutečný přístup (2026-05-27) se LIŠÍ od původního plánu** (ElementFormBridge + JSON envelope + feature flags). Implementovaná architektura je jednodušší:

**Generický `ElementEditDialog` přes reflexi:**
- **Jeden dialog** pro všechny elementy (`Windows/ElementEditDialog/`). Žádný per-element C# kód.
- **Caché schema class per element** (`ALVA.TemplateDesigner.EDInputForm.<Typ>Edit`) definuje form layout. **Field key = property path** na C# elementu (dot notation, např. `Style.FontSize`, `HeaderStyle.BackgroundColor`).
- **`ElementPropertyAccessor`** (reflexe) — Get/Set Field.Value ↔ typovaná property; traversal nested paths; type conversion string ↔ float/int/bool/enum/nullable.
- Pre-fill po `EDInputForm.Initialized` eventu přes reflexi (ne IN params). Change detection proti pre-filled hodnotám → no-edit = žádný command.
- Save → `CompositeCommand` ze `SetPropertyCommand`ů (reflexní apply) → `_vm.Execute` → undo/redo zachován.
- **MainWindow `EDInputFormSchema(element)`** switch mapuje element type → (cacheClass, title). Fallback na legacy `ElementPropertiesWindow` pro nemigrované typy.

**Přidání dalšího elementu = 1 Caché schema class + 1 řádek ve switchi.** Žádný dialog kód, žádný ElementFormBridge, žádný feature flag (paralelní běh řeší switch fallback).

**⚠️ Schema duplikace dluh:** Caché schema je manual mirror PropertyDescriptors v C# (data model). Akceptováno kvůli Caché patchovatelnosti (server-side změna labelů/validace bez .exe redeploy). Detail: memory `project_edinputform_schema_duplication.md`. Long-term: schema-as-data v Caché global → auto-gen.

**Co zůstává v .NET:** všechny `TemplateElement` třídy (model, Render, Clone, JSON), PageLayoutEngine, PageRenderer, canvas, undo/redo. Inline `PropertiesPanel` (levý panel) zatím PropertyDescriptors — live edit.

**Risks:**
- Reflexe fragile — property path jako string, typo v Caché = runtime error (ne compile-time). Mitigace: optional CI test (path existuje na elementu).
- Inline panel vs modal: dokud inline zůstává PropertyDescriptors, validace (Min/Max) je duplikovaná. Plný dedup = migrace inline panelu na EDInputForm (live mode — odloženo).

| Krok | Co | Stav |
|---|---|---|
| W4-0 | EDComponentToolbar (sdílený toolbar EDInputForm + EDList) + search filter + diacritics | ✅ 2026-05-26 |
| W4-1 | EDInputForm visual upgrade (3-col compact layout, group sectioned headers, font harmonizace) | ✅ 2026-05-25 |
| W4-2 | Form helpery (AddText/AddBool/AddNumber/AddDecimal) — hybrid syntax | ✅ 2026-05-26 |
| W4-3 | SaveToCacheDialog — první migrace (PoC, vlastní dispatch) | ✅ 2026-05-26 |
| W4-4 | Generický `ElementEditDialog` + `ElementPropertyAccessor` (reflexe, property-path keys) | ✅ 2026-05-27 |
| W4-5 | Elementy: Text, Line, Rectangle, Barcode, NotesLegend (+border parity audit) | ✅ 2026-05-27 |
| W4-5b | **Tiskový dialog — parametry sestavy přes EDInputForm** (`AgendaParams` generic dispatch z `GetParameters`; visibleWhen v C# přes `TemplateParameter.IsVisible`) | ✅ 2026-06-12 |
| W4-6 | **ImageElement** — nový `Image` field type v EDInputForm (upload→base64+náhled). ⬅️ DALŠÍ. Vyžaduje rozšíření sdílené komponenty EDControls (nový field type + WPF renderer náhledu), nebo obejít přes `BUTTON` + vlastní handler (vzor `SaveTemplate`). | ⬅️ |
| W4-7 | **Section / Page** — generalizace `ElementEditDialog` na `object` + Caché schemas | 🟡 Section ✅ (2026-06-12, `SectionEdit.cls`); Page ⬅️ zbývá (formát/orientace + ScaleBodySections nelze přes čistou reflexi — chce hook nebo zjednodušení) |
| W4-8 | **Simple dialogy** — SaveSectionTemplate, NormalizePositions | ⛔ **Záměrně neimigrováno** (rozhodnuto 2026-06-12): „Uložit jako šablonu sekce" = 1 pole, nulový dedup → plain dialog stačí; „Normalizovat pozice" = živě počítaný náhled „Bude upraveno: N" (client-side geometrie), EDInputForm to nepřepočítá → ponecháno custom. Tiskový dialog (původně zvažovaný zde) hotov jako W4-5b. |
| W4-9 | **Komplexní elementy** — StopList/DepartureGrid/TableElement: tab „Obecné" → EDInputForm, tabulková/per-column část (editor sloupců/skupin) zůstane custom `CellStyleEditor` (EDInputForm neumí editovatelnou tabulku) → vždy částečná migrace + sešití dvou částí do okna. Po W4-6. | |
| W4-10 | Inline `PropertiesPanel` → EDInputForm (live mode) — plný dedup PropertyDescriptors. Velký refaktor, odložené | |
| W4-11 | Cleanup: PropertyDescriptor systém smazat až po 100% migraci | |
| W4-12 | (volitelné) EDInputForm wire formát `$lb()` OUT → JSON envelope; collapsible groups | |

**Co je / není kandidát na EDInputForm** (rozhodovací pravidlo, dohodnuto 2026-06-12):
- ✅ **Ano:** formuláře s **pevným, server-definovaným schématem** (vlastnosti elementu/sekce/stránky; parametry agendy z `GetParameters`). Cesta: `*Edit.cls` (reflexe přes property path) nebo generická `AgendaParams` (z param JSON).
- ❌ **Ne:** (1) triviální confirm s 1 polem (nulový dedup, jen Caché round-trip navíc); (2) dialogy s **živě počítaným náhledem** z client-side dat (Normalizovat pozice); (3) plně dynamické/data-driven formy bez server schématu. visibleWhen (podmíněná viditelnost) jde, ale evaluuje se **v C#** (pravidlo definuje Caché v JSON) — `Validate` roundtrip nenese InitParams.
- **Reusable pattern:** `AgendaParams` (jedna generická dispatch třída převádějící `GetParameters` JSON → EDInputForm Form) je vzor pro budoucí Caché-driven formy stavěné z existujícího JSON kontraktu (ne ručně psané `*Edit.cls`).

**Pořadí dalších kroků:** W4-6 (Image — chce nový field type v EDControls) → W4-9 (komplexní vícetabové elementy) → W4-7 Page (formát/orientace hook) → W4-10/11 (inline panel + cleanup, velký refaktor, na konec).

---

#### 🔵 Wave 5 — Business logic per agenda (paralelně)

Čisté rozdělení agenda-specific business logiky do samostatných Caché tříd. Běží paralelně nezávisle na Wave 1–4.

**Cíl:** `ZastavkoveJrMhdAPI` (~1200 řádků) je OK, ale pro každou novou agendu (meziměstský, knižní, vozový) bude podobný. Zavést pattern před implementací Fáze C/D/E.

**Pattern:**
```
ALVA.TemplateDesigner.AgendaAPI          ← abstraktní
├── ZastavkoveJrMhdAPI                   ← MHD (hotovo)
├── MeziMestskyJrAPI                     ← Fáze C
├── KnizniJrAPI                          ← Fáze D
├── VozovyJrAPI                          ← Fáze E
└── TemplateDesignerFAKTURY              ← faktury (hotovo)

Každá implementuje:
  GetSchema, GetData, GetMockData, GetTableTemplates, GetTableTemplate
```

**Nezávislost na Wave 1–4:** business logika nedeleguje na uživatele ani storage — čistá data pipeline. Pokud Wave 4 ještě není hotový, agenda klidně funguje.

**Risks:**
- Shared helpery: dnes v `ZastavkoveJrMhdAPI` mnoho helperů (BuildZastavka, BuildOdjezd, AddAnotace). Extrahovat do base class až budou 2+ konzumenti — ne dopředu.
- Caché velikost: ~1000+ řádků per agenda. Mitigace: dělit do partial classes/routines když přesáhne 1500 řádků.

| Krok | Co | Stav |
|---|---|---|
| W5-0 | `AgendaAPI` abstrakt base class + 2 implementace (`ZastavkoveJrMhdAPI`, `TemplateDesignerFAKTURY`) | ✅ v archivu |
| W5-1 | Dokumentace `AgendaAPI` contract — co se musí implementovat, co je volitelné | ✅ částečně v [`cache-api.md`](../rules/cache-api.md) |
| W5-2 | Fáze C (Meziměstský) — využije pattern | |
| W5-3 | Fáze D (Knižní) — využije pattern | |
| W5-4 | Fáze E (Vozový) — využije pattern | |
| W5-5 | Refaktor: extract `AgendaAPIBase` helpery až budou 2+ konzumenti | |

---

#### 🚫 "Must not happen" scénáře

Konkrétní situace, které Caché migrace musí předejít. Kontrolní seznam při code review.

1. **Synchronní Caché volání v UI thread** → zamrznutí aplikace. Kontrola: `grep "MLRest\." *ViewModel*.cs` hledá synchronní volání.
2. **Silent fail při Caché chybě** → UserPreferences se nenačetly, uživatel neví proč. Kontrola: chyba jde přes `IPopupDisplayer.Error(ex)`.
3. **Breaking JSON change bez migrátoru** → staré šablony nejdou otevřít. Kontrola: před produkčním nasazením s reálnými šablonami musí být H-18/H-19 hotové (versioning + migrator). Wave 3 CRUD už běží, ale versioning chybí.
4. **Všechno naráz** (big bang migration) → nelze vrátit. Kontrola: feature flags per element (Wave 4), paralelní běh starého a nového systému.

---

### 🟠 Fáze B: Element flexibility — rozšíření možností elementů

Vychází z gap analýzy oproti komerčním report generátorům (Crystal Reports, Stimulsoft, DevExpress). Nezávislé na JŘ — platí pro fakturační i obecné šablony. Může běžet paralelně s Caché migrací (neblokuje a není blokované).

#### Template versioning (prerekvizita produkčního Wave 3)

Wave 3 CRUD už běží bez versioning, ale **před produkčním nasazením s reálnými zákaznickými šablonami** musí být H-18/H-19 hotové — jinak breaking JSON change zničí existující data.

| # | Co | Proč | Velikost |
|---|---|---|---|
| H-18 | `TemplateMigrator.Migrate(JsonDocument)` — detekce verze v JSON, pipeline migrací, spouští se před deserializací | Ochrana existujících šablon před breaking changes | S |
| H-19 | Migrace V1→V2: `TextStyle` flat→nested (breaking change 2026-03-24) + varování v UI | Konkrétní existující breaking change | S |

#### Merged cells v TableElement

Pro faktury standardní. Záhlaví přes 2 sloupce ("Dodavatel / Odběratel"), sloučené řádky titulu.

| Krok | Co | Stav |
|---|---|---|
| 35 | `ColSpan` + `RowSpan` na `TableCell` — JSON serializace, rendering skip pro sloučené | |
| 36 | Merge editor v `TableEditorDialog` — UI pro nastavení span | |

#### Conditional cell formatting v tabulce

Analogie `ColorRules` z `DepartureGrid` — generalizovat na `TableColumn`.

| Krok | Co | Stav |
|---|---|---|
| 37 | `ColorRules` na `TableColumn` — `{ expression, foreColor, backColor }` array | |
| 38 | Vyhodnocení v print engine při renderování buňky | |

#### Obecný layout-reservation pattern u elementů

Aktuálně když element potřebuje rezervovat místo pro volitelný vizuální prvek (šipka směru u `StopListElement`), řeší se to ad-hoc. Do budoucna zvážit deklarativní mapu "pokud property X=true → rezervuj Y mm vlevo/vpravo". Vyřeší konzistentně napříč všemi elementy.


### 🟠 Fáze C: Meziměstský JŘ

Konceptuálně podobné MHD — zastávky + odjezdy spojů + poznámky. Rozdíl: sloupce odjezdů nejsou typy provozu (jako MHD), ale mají jiný koncept specifický pro meziměsto. Layout je pivot tabulka (zastávky × spoje). Pravděpodobně bude potřeba samostatný element (ne rozšíření DepartureGridElement).

| Krok | Co | Stav |
|---|---|---|
| 42 | Definovat meziměstský layout — analýza referenčních PDF, wire formát dat | |
| 43 | Implementace meziměstského JŘ elementu | |
| 44 | Caché `MeziMestskyJrAPI` — GetData/GetMockData (Wave 5 pattern) | |

---

### 🟠 Fáze D: Knižní JŘ

Layout TBD — pravděpodobně kompaktní formát pro knižní publikaci. Definovat layout před implementací.

| Krok | Co | Stav |
|---|---|---|
| 45 | Analýza referenčních knižních JŘ — definice layoutu a wire formátu | |
| 46 | `BookScheduleElement` — implementace (pravděpodobně víc elementů, viz poučení z MHD) | |
| 47 | Caché `KnizniJrAPI` — GetData/GetMockData (Wave 5 pattern) | |

---

### 🟠 Fáze E: Vozový JŘ

Layout TBD — hlavička oběhu, tabulka spojů, poznámky. Pravděpodobně víc elementů (viz poučení z MHD).

| Krok | Co | Stav |
|---|---|---|
| 48 | Analýza referenčních vozových JŘ — definice layoutu a wire formátu | |
| 49 | `VehicleScheduleElement` — implementace (pravděpodobně víc nezávislých elementů) | |
| 50 | Caché `VozovyJrAPI` — GetData/GetMockData + async loading (R1, velký objem dat) | |

---

### 🟡 Import PDF — doladění

| Krok | Co | Stav |
|---|---|---|
| 32 | Import: detekce tabulek z mřížky čar (3a) — místo 50 čar + 20 textů → jeden `TableElement` | |
| 33 | Import: JŘ-aware mód (3d) — dvoustupňový AI+engine import | ⬅️ rozpracováno (2026-04-03) — AI Vision pipeline + AiGuidedScheduleBuilder fungují, ladění na širší sadě JŘ |

**Aktuální stav:**
- ✅ Režim 1 (deterministický 1:1 import) hotovo (2026-03-13)
- 3a: Detekce tabulek z mřížky čar
- 3b: Křivky/Bézier (nízká priorita — zaoblené rohy rasterizovat jako bitmapa)
- 3c: AI post-processing — nahrazení statických textů za `{PLACEHOLDER}`
- **3d: JŘ-aware import** — dvoustupňový AI + engine, viz `architecture.md`

---

### 🟢 Menší priorita — UX polish

*(prázdné — všechny zachycené QoF body jsou hotové, viz changelog)*

---

### Tabulkové featury (na backlogu, zatím bez customer demand)

- **Grouping v TableElement** — seskupení řádků dynamické tabulky podle hodnoty pole (`GroupHeader`, `GroupFooter`, `Summary` řádky)
- **Computed fields** — pole vypočítaná na C# straně ze surových dat před předáním šabloně (`CELKOVA_CENA = CENA * MNOZSTVI`)
- **Vnořené datasource** — `_rows` uvnitř `_rows` (faktura → položky → daně); TableElement s child kolekcí
- **Min/max počet řádků dynamic tabulky** — `MinRows`, `MaxRows` properties; engine ořízne/doplní (zmínil kolega, není urgent)

---

### 🔵 Před produkcí (odloženo)

| Co | Popis |
|---|---|
| Full migration pipeline | Po H-18/H-19 rozšířit na každou breaking change (ne jen 2 verze) |
| CI/CD linting šablon | Ověřit šablony před deployem (Caché job) |
| Kategorie + popis šablony v UI | Wave 3 dnes drží Author/CreatedAt/ModifiedAt/Version v metadatech; chybí kategorie + popis v UI |

> Versioning, audit, thumbnails, history → viz Wave 3 (W3-4 až W3-9).

---

### 🟡 Virtualized canvas rendering (zoom >500%)

**Problém:** `SkiaSharp.Views.WPF.SKElement` při každém repaint alokuje a uploaduje byte buffer velikosti celé plochy (`Width × Height × 4`). Při zoom 1000% (A4) je SKElement ~8000×11000 px = **365 MB upload per frame**. Toto je hardware-bound bottleneck SkiaSharp Views.WPF integrace.

**Aktuální workaround (2026-05-20):** Zoom max omezen na 500% (SKElement ~93 MB upload). Funguje, ale teoretické maximum zoomu omezené.

**Proper řešení (až bude potřeba):** Virtualizace — SKElement udržovat ve velikosti viewport (~1600×900 px), implementovat custom scroll handling. Skia canvas translatuje by scroll offset, uživatel vidí jen viewport, ScrollViewer/scrollbary řízeny manuálně.

**Velikost:** ~4-6h. Refactor `<ScrollViewer><SKElement/></ScrollViewer>` na `<Grid><SKElement/> + custom ScrollBars/></Grid>`. Update hit testing (mouse coords → page coords s scroll offset), drag&drop, rubber band selection.

**Risk:** middle — mouse event coordinate transformation se musí důsledně updatovat napříč všemi interakcemi.

**Kdy:** jakmile bude potřeba zoom >500% pro reálnou práci. Aktuálně 500% pokrývá většinu inspekčních use case.

---

## 🤖 AI integrace — v pozadí

> **Status:** Sekundární, ne core feature. Aktuální AI funkce (generování šablony, PDF import enrichment, vision pipeline) zůstávají funkční, další rozšíření neplánovaná dokud není core (zastávkový 1:1, Caché migrace) vyřešený.

Architektonické rozhodnutí (2026-03-17): **žádné zásadní rozhodnutí teď není potřeba.** JSON šablona + DataSchema jsou dostatečné rozhraní pro AI. Rozšíření se dá přidat kdykoliv později bez breaking changes.

**Princip:** AI = copilot, ne autopilot. Vždy navrhne → uživatel potvrdí/upraví.

### Aktuální stav
- ✅ AI Assistant — generování celé šablony z textového popisu (Claude API)
- ✅ Schema injection — AI dostane seznam polí/kolekcí/parametrů agendy
- ✅ AI Vision pipeline — rozpoznání JŘ struktury z PDF obrázku

### Budoucí možnosti (až budou reální uživatelé s potřebou AI asistence)

**MCP server** — TemplateDesigner vystaví `get_template`, `apply_template`, `add_element`, `modify_element`, `validate_template` atd. Tenká vrstva nad ViewModelem; každý tool volá existující `_vm.Execute(command)`. Standardní protokol (stdio) — funguje s Claude i jinými AI.

**Pokročilé use cases (dlouhodobě):** onboarding průvodce, "přidej patičku s číslem stránky" (modifikace existující šablony), smart suggestions (TableElement → AI navrhne sloupce z DataSchema), migrace starých Caché sestav, template linting v CI/CD, batch generování šablon pro novou agendu.

**Proč to neblokuje:** JSON šablona je stabilní kontrakt mezi designerem a AI. Dokud AI umí číst a psát JSON v definovaném formátu, integrace funguje bez ohledu na to, jestli je to prompt, MCP, nebo REST API.

---

## ✅ Záměrně jinak — neřešit

- **Caché jako source of truth pro konfiguraci a šablony** — dává smysl pro doménovou logiku (Wave 1–3 to provádí)
- **Univerzální Section třída** — jednodušší než pevná hierarchie (Crystal Reports)
- **Absolutní pozicování jako default** — správné pro tiskové sestavy s pevným layoutem
- **PropertyDescriptor a EDInputForm paralelně** — ne cut-over, per element migrace (Wave 4)
- **Server-side migrace šablon** — autoritativní, ne klientská (Wave 3)

---

## 🗄️ Hotová práce

Detail dokončených etap (v1.0 demo balíček, Fáze A.2, B0/B0.5, JŘ elementy, PageLayoutEngine, …) → [`roadmap-archive.md`](roadmap-archive.md). Průběžný changelog → [`changelog.md`](changelog.md).

