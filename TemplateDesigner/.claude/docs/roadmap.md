# Roadmap

## 📋 Aktuálně rozpracováno / nejbližší kroky (stav k 2026-06-30)

> Obě zákaznické prezentace proběhly (statická ~19.6., živá 26.6.). Handoff blok 2026-06-21
> archivován do [`roadmap-archive.md`](roadmap-archive.md). Zastávkový JŘ (AD) je datově 1:1 +
> výkonově hotový. Nově (30.6.) je **databáze primární úložiště šablon** (Ctrl+S → DB, soubor =
> import/export).

**Aktivní rozpracované tracky:**
- **Wave 4 EDInputForm** — Fáze 1 + W4-6 ImageElement (Image field type v EDControls) ✅ hotové;
  **další = W4-9** komplexní vícetabové elementy (StopList/DepartureGrid/Table → tab „Obecné" do
  EDInputForm). Stylový pod-formulář zatím prototyp (1 zóna) — rozšířit na všechny zóny + StopList.
- **AD nastavení linky (PRIORITA 2b)** — zbývají přímočaré PATCH-read položky: **AD-N4** (NZ na nový
  řádek), **AD-N11** (cizojazyčné poznámky); AD-N5/N13/N15 odložené (dormant data / renderer).

**Úklid docs — stav 2026-06-30:**
- ✅ **Prezentační prep docs konsolidovány** — osnova + režie demo → jeden
  [`prezentace-a-gap-2026.md`](prezentace-a-gap-2026.md) (sdělení + FAQ + zpětná vazba); jednorázové
  artefakty smazány. **Gap analýza legacy vs nový ponechána** jako samostatná evergreen reference
  (`gap-analyza-legacy-vs-novy-jr.md`) — analýza udělaná jednou, plná mapovací matice se nemaže.
- ✅ **Hlavičkové freshness/status poznámky** — `architecture-diagrams.md` (konceptuální, kód autoritativní),
  `cache-database-linky.md §10` (snapshot 2026-06-02 → odkaz na changelog), obě AD analýzy (status → roadmap).
- 🟡 **Ořez `cache-database-linky.md §6`** — ponecháno: §6 stále obsahuje **otevřené** otázky (FTL/Plk/CNZ,
  skupina linek, vícezónový tarif). Přerámování na čistý „referenční datový model" zbývá až po jejich uzavření.
- ⬜ **changelog archivace** — changelog má ~1600 řádků; květnové Wave 1-3 → `changelog-archive-2026-05.md`
  (mechanické, čistě o velikosti — žádná staleness, low priorita).

**Nové z prezentací (24.6., neanalyzováno):** P1 ruční poznámky do patičky, P2 sloučení linek se
stejným trojčíslím / více dopravců, P3 logická imposice 2×A5→A4 — detail viz
[`customer-requirements-zastavkovy-jr.md §P`](customer-requirements-zastavkovy-jr.md) + backlog níže.

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

**🎯 Pořadí priorit po prezentacích (statická ~19.6., živá 26.6. — obě proběhly):**

1. **Zastávkový JŘ (AD)** — *hlavní deliverable*. Datově 1:1 + výkonově **hotovo**. Zbývají odložené
   nice-to-have (multi-varianta TrasaMin přes C# renderer = AD-N13; vizuál legendy) + přímočaré
   PATCH-read položky nastavení linky (AD-N4, AD-N11). Číslování omezení (cnz) ✅ hotovo 2026-06-23.
2. **Wave 4 EDInputForm** — W4-6 ImageElement ✅ hotovo (commit 72b724d); **další = W4-9** komplexní vícetabové elementy.
3. **Helpdesk pro AD** — Fáze A.2-D hotová (#7955/#7947/#7161 zavřeno). #11300 KORID přípoje přeřazeno
   k linkovým JŘ (není zastávkový koncept). Zbývá jen odložené A2-A5/A6/B4 (datový průzkum / kolegové).
4. **Nové z prezentací (P1-P3)** — backlog, neanalyzováno; před implementací založit ticket.
5. **AI pomocník (stretch)** — funkční (generování šablony, PDF import, vision pipeline), další rozšíření low-prio.

> **Pozn.:** ⬅️ markery v sekcích níže pocházejí z dřívějšího plánu — autoritativní pořadí je tento
> seznam. **Výkonový rest** (MHD `VypocitatBitmapyObsluhy`) zůstává rizikový samostatný krok.

### ⬅️ PRIORITA 1 — Wave 4: dokončit EDInputForm migraci (🟡 rozpracováno)

EDInputForm je živý a generický základ funguje. Cíl: **co má být EDInputForm, ať JE EDInputForm** — logika na jednom místě, ne půl na půl mezi PropertyDescriptors a Caché schema.

**Hotovo (2026-05-25 — 27):** EDInputForm visual upgrade + EDComponentToolbar (sdílený s EDList) + generický `ElementEditDialog` (reflexe přes property-path keys) + 5 elementů migrovaných (Text/Line/Rectangle/Barcode/NotesLegend) + SaveToCacheDialog. Detail Wave 4 sekce níže.

**Zbývá projít VŠECHNO a předělat kde to dává smysl:**
- ~~**ImageElement**~~ — ✅ hotovo (Image field type v EDControls + `ImageElementEdit.cls`, commit 72b724d)
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
- **DEV cleanup před produkcí** — ✅ odstraněn `#if DEBUG` auto-open `kv_ad_test.json` + `--test-detector` blok (2026-06-17). Zbývá: zvážit statický `GetMockData`.

**Performance rest (samostatný krok):** MHD agenda `VypocitatBitmapyObsluhy` má 3 průchody `obsluhaKonec` + vnořené smyčky → potenc. O(bodů³). **Rizikový zásah do ověřeného MHD enginu** — udělat zvlášť s re-verifikací proti MHD gold oracle. (AD agenda už per-stránka neefektivnosti nemá.)

**Reference:** [`legacy-schedule-generators.md`](legacy-schedule-generators.md) — mapování legacy Caché generátorů na typy JŘ.

### 🟢 PRIORITA 2b — AD nastavení linky 1:1 (mapováno na analýzu) ⬅️ NOVÝ TRACK

Cíl: postupně dotáhnout, aby `ZastavkoveJrAd` bral v potaz **nastavení tisku na úrovni linky** (taby
*MHD* / *MHD – další* / dialog *Nastavení pro tisk linky*), které legacy čte z `^JRL` PATCH / `MhD`.
**Autoritativní podklad:** [`ad-jr-nastaveni-linky-analyza.md`](ad-jr-nastaveni-linky-analyza.md) — každá
položka níže odkazuje na § analýzy a nese přesný legacy storage + reader.

**Postup per položka** (dle pracovního cyklu — viz `CLAUDE.md §Pracovní postup`): *projít → validovat (máme/nemáme, dává smysl teď?)
→ implementovat NEBO odložit s důvodem → otestovat proti gold oraclu → docs*. Pořadí = **přímočaré v
legacy nejdřív** (čistý PATCH read → placeholder/flag), složité/renderer/nová struktura na konec.

| ID | Analýza § | Co | Legacy storage | Složitost | Stav |
|---|---|---|---|---|---|
| AD-N1 | §2 | Poznámka nad linku → placeholder `POZNAMKA_NAD_LINKU` | `PATCH,"Head"` (gPrpLin dta4) | XS | ✅ 2026-06-20 |
| AD-N2 | §2 | Typ JR → `TYP_JR` placeholder (volitelný; zastávkový legacy razítko z OBJ nestaví) | `PATCH,"OBJ"` (gPrpLin dta3) | S | ✅ 2026-06-20 |
| AD-N3 | §7 | Uspořádat poznámky v legendě podle délky | `PATCH,"Trid_delK"` (gSetLinPrt dta26) | S | ✅ 2026-06-21 (řazení vzestupně i sestupně) |
| AD-N4 | §7 | Negativní značky na nový řádek (layout legendy) | `PATCH,"NegZnNR"` (dta27) | S | ⬜ ⬅️ DALŠÍ |
| AD-N5 | §3 | `MHD_NEZNZ123` — potlačit NZ v legendě per sloupec | `PATCH,"MHD_NEZNZ123"` | M (váže na NZ legend, dnes dormant) | ⬜ |
| AD-N6 | §3 | Override nadpisů sloupců (skupin dnů) → TD vlastnost `HeaderOverride` (ne port `^JRSET`) | (TD vlastnost elementu) | S | ✅ 2026-06-22 |
| AD-N7 | §5 | **Komentáře** (ruční poznámka linky + přiřazené spoje) | `MhD,"SeT_SpeC"` + per-spoj `MhD_SpeC` | M-L | ✅ 2026-06-20 (Fáze 1) |
| AD-N8 | §5 | **Spoje, nepoznámkovat** (gate ručního komentáře, 1:1 legacy) | `PATCH,"MHDSPNPZN"` (`odTC~doTC`) | M | ✅ 2026-06-20 (Fáze 1) |
| AD-N9 | §5 | Směrové poznámky tam/zpět/vše | `MhD,"SeT_MhD",id,cnt` | S-M | ✅ 2026-06-20 (Fáze 1) |
| AD-N8b | §5 | **Fáze 2** — co schovat v rozsahu „nepoznámkovat" (#7955) = parametr sestavy `SCHOVAT_V_ROZSAHU` (Ruční/Automatické/Obojí) + TD render-filtr nad nadmnožinou z Caché | param + `NoteVisibilityFilter` | M | ✅ 2026-06-20 (Fáze 2) |
| AD-N10 | §4 | Text „spoj jede až do XY" (prodloužení trasy) | rozšíření `RozdilyTras` | M | ✅ 2026-06-21 (#7947) |
| AD-N11 | §7 | Cizojazyčné poznámky | `PATCH,"prtCzjPzn"` + `("C")` arr6 | M | ⬜ |
| AD-N12 | — | ~~Km sloupce~~ → **patří do linkových JŘ** (tarif dle km), ne zastávkové | — | — | ➖ přesunuto |
| AD-N13 | §4 | Multi-varianta časů tras (víc časových sloupců) | `mhd,"par",2` + C# renderer | L | ⬜ odložit (gold oracle: 0345061) |
| AD-N14 | — | ~~Návaznosti / přípoje~~ → **patří do linkových/knižních JŘ**, ne zastávkové | — | — | ➖ přesunuto |
| AD-N15 | §3 | NZ prázdninové dělení — ověřit na reálné lince | `MHD_NZ1_NZ2`/`MHD_NENZ1_NENZ2` | — | 🟡 kód hotový, čeká data |

> **Pozn. k odloženým:** AD-N12/13/14 jsou „ne přímočaré" (zásah do C# rendereru / nová doménová
> struktura) — necháváme až po jednoduchých PATCH-read položkách. AD-N15 nelze ověřit bez firmy s
> prázdninovým FTL JŘ (`MHD_NZ1_NZ2` prázdné všude). AD-N7/N8/N9 jsou doménově nejcennější (tab MHD
> ruční poznámky) — úložiště zmapováno, takže nejsou blokované.

### Helpdesk tickety (Fáze A.2-D) — NIŽŠÍ priorita

TRANSDEV/KORID/DPKV suspended tickety v `Agendy/ZastavkoveJrMhd` (auto-poznámky bugs, NZ bug, …) — tabulka A.2.D níže. **Až po prioritě 1 + 2.** (KORID přípojové řádky #11300 přeřazeno k linkovým JŘ.)

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

## 📋 Co je potřeba dělat

### 🆕 Nové požadavky z prezentací (P1-P3) — backlog, neanalyzováno

Zaznamenáno z živých prezentací 2026-06-24 (ne helpdesk). Před implementací založit/dohledat ticket a
ověřit přesné zadání. Detail + odhady viz [`customer-requirements-zastavkovy-jr.md §P`](customer-requirements-zastavkovy-jr.md).

| # | Co | Odhad | Pozn. |
|---|---|---|---|
| P1 | Ruční poznámky do patičky JŘ (volný text nebo dedikované pole „Poznámky") | XS–M | `TextElement` v patičce už funguje; M jen pokud param-driven |
| P2 | Sloučení linek se stejným posledním trojčíslím (i různá Tč / více dopravců) → 1 výstup | L | Dotýká se agendy + InstancePicker + agregace `POPIS_DOPRAVCE`; chce analýzu legacy |
| P3 | Logická imposice 2×A5 oboustranně na A4 (N-up + duplex/brožura) | M–L | Samostatná „sheet layout" vrstva nad PageLayoutEngine; `RenderPage` už bere offset |

---

### 🟢 Fáze A.2-D: Caché business logic (A2-D1..D5 ✅ hotovo)

Suspended tickety TRANSDEV/KORID/DPKV — Caché-side auto-poznámky a logika spojů v `ZastavkoveJrMhdAPI`. Plný kontext, citace a gap analýza viz **[`customer-requirements-zastavkovy-jr.md`](customer-requirements-zastavkovy-jr.md)**.

> **Pozn.:** Fáze A.2-A..C (StopList / DepartureGrid / NotesLegend UI úpravy) je hotová — viz Archiv níže. Zbývá A.2-D série (Caché logika) + A.2-A5/A6/B4 (odložené pro datový průzkum / diskuzi s kolegy).
>
> **Priorita:** Wave 2 → H-18/H-19 → Wave 3 finishing → Fáze A.2-D. Caché Migration foundation se uzavře dřív, než se vrátíme k zákaznickým ticketům — eliminuje riziko rework po pozdním přechodu.

#### A.2.D — Caché `ZastavkoveJrMhdAPI` úpravy

> **Stav (2026-06-30):** A2-D1..D5 ✅ vyřešeno **pro AD agendu** (`ZastavkoveJrAd`); helpdesk #7947/
> #7955/#7161 zavřeno. Port na MHD agendu (D1, D4) neproveden — až bude potřeba. **Série A.2-D je
> hotová** (A2-D6 přeřazeno mimo zastávkový JŘ — viz níže).

| Krok | Tiket | Zákazník | Co | Stav |
|---|---|---|---|---|
| A2-D1 | #7947 | TRANSDEV | Nový text template „spoj jede až do XY" pro spoje **prodlužující** základní trasu (symetrické k existujícímu „končí v zast. X") | ✅ AD 2026-06-21 |
| A2-D2 | #7955 | TRANSDEV | Vlastní komentář spoje musí přebít auto-poznámku rozdílů (suppress auto pokud má spoj custom text) | ✅ AD 2026-06-20 |
| A2-D3 | #7955 | TRANSDEV | **Bug** — „Spoje, nepoznámkovat" maže i inverzní znaménko (NZ); izolovat NZ od ostatních poznámek | ✅ AD 2026-06-20 (bug nevzniká) |
| A2-D4 | #7161 | TRANSDEV | Skrýt „x — zastávka na znamení" pokud sloupec s `x` je hidden (kontrakt TD ↔ Caché — rozhodnutí kde se filtruje) | ✅ AD 2026-06-21 |
| A2-D5 | #7161 | TRANSDEV | Vypnutí auto-poznámky „všechny spoje s bezbariérovým vstupem" — parametr v `GetParameters` + checkbox v PrintDialog | ✅ AD 2026-06-21 |

> ➖ **#11300 (KORID) — přípojové řádky přeřazeno k linkovým JŘ.** Přípoje/návaznosti nejsou
> koncept zastávkového JŘ (konzistentní s AD-N14). Řeší se až s agendou linkových JŘ (`Pripoj`
> struktura + `ConnectionsListElement`), ne zde.

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
| 4 | Form-driven UI | 🟡 Rozpracováno | EDInputForm místo PropertiesPanel. Elementy + Image (W4-6) + Section + parametry tisku ✅. Zbývá komplexní elementy (W4-9) + Page + inline panel cleanup |
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
| W4-6 | **ImageElement** — nový `Image` field type v EDControls (upload→base64+náhled přes `Base64ToImageSourceConverter`) + `ImageElementEdit.cls`; Image hodnota se neposílá na server (jen client-side). | ✅ 2026 (commit 72b724d) |
| W4-7 | **Section / Page** — generalizace `ElementEditDialog` na `object` + Caché schemas | 🟡 Section ✅ (2026-06-12, `SectionEdit.cls`); Page ⬅️ zbývá (formát/orientace + ScaleBodySections nelze přes čistou reflexi — chce hook nebo zjednodušení) |
| W4-8 | **Simple dialogy** — SaveSectionTemplate, NormalizePositions | ⛔ **Záměrně neimigrováno** (rozhodnuto 2026-06-12): „Uložit jako šablonu sekce" = 1 pole, nulový dedup → plain dialog stačí; „Normalizovat pozice" = živě počítaný náhled „Bude upraveno: N" (client-side geometrie), EDInputForm to nepřepočítá → ponecháno custom. Tiskový dialog (původně zvažovaný zde) hotov jako W4-5b. |
| W4-9 | **Komplexní elementy** — StopList/DepartureGrid/TableElement: tab „Obecné" → EDInputForm, tabulková/per-column část (editor sloupců/skupin) zůstane custom `CellStyleEditor` (EDInputForm neumí editovatelnou tabulku) → vždy částečná migrace + sešití dvou částí do okna. | ⬅️ DALŠÍ |
| W4-10 | Inline `PropertiesPanel` → EDInputForm (live mode) — plný dedup PropertyDescriptors. Velký refaktor, odložené | |
| W4-11 | Cleanup: PropertyDescriptor systém smazat až po 100% migraci | |
| W4-12 | (volitelné) EDInputForm wire formát `$lb()` OUT → JSON envelope; collapsible groups | |

**Co je / není kandidát na EDInputForm** (rozhodovací pravidlo, dohodnuto 2026-06-12):
- ✅ **Ano:** formuláře s **pevným, server-definovaným schématem** (vlastnosti elementu/sekce/stránky; parametry agendy z `GetParameters`). Cesta: `*Edit.cls` (reflexe přes property path) nebo generická `AgendaParams` (z param JSON).
- ❌ **Ne:** (1) triviální confirm s 1 polem (nulový dedup, jen Caché round-trip navíc); (2) dialogy s **živě počítaným náhledem** z client-side dat (Normalizovat pozice); (3) plně dynamické/data-driven formy bez server schématu. visibleWhen (podmíněná viditelnost) jde, ale evaluuje se **v C#** (pravidlo definuje Caché v JSON) — `Validate` roundtrip nenese InitParams.
- **Reusable pattern:** `AgendaParams` (jedna generická dispatch třída převádějící `GetParameters` JSON → EDInputForm Form) je vzor pro budoucí Caché-driven formy stavěné z existujícího JSON kontraktu (ne ručně psané `*Edit.cls`).

**Pořadí dalších kroků:** W4-6 (Image) ✅ → **W4-9 (komplexní vícetabové elementy)** ⬅️ → W4-7 Page (formát/orientace hook) → W4-10/11 (inline panel + cleanup, velký refaktor, na konec).

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

### 🟠 Fáze B: Element flexibility — rozšíření možností elementů

Vychází z gap analýzy oproti komerčním report generátorům (Crystal Reports, Stimulsoft, DevExpress). Nezávislé na JŘ — platí pro fakturační i obecné šablony. Může běžet paralelně s Caché migrací (neblokuje a není blokované).

#### Template versioning (prerekvizita produkčního Wave 3) — ✅ FRAMEWORK HOTOVÝ

H-18 framework hotový (Wave 3 W3-4, `TemplateMigrator` + `version` field, archiv). Od 2026-06-30 je
databáze **primární** úložiště šablon (Ctrl+S → DB) — versioning prerekvizita (R5 / „must-not-happen #3")
je tím splněna. H-19 (konkrétní migrace) zůstává no-op: nové i DB šablony jsou V2+, takže V1→V2 migrace
není akutní; doplnit při prvním reálném breaking change nebo pokud se objeví staré V1 soubory.

| # | Co | Stav | Velikost |
|---|---|---|---|
| H-18 | `TemplateMigrator.Migrate(JsonDocument)` — detekce verze v JSON, pipeline migrací, spouští se před deserializací | ✅ 2026-05-25 (W3-4 framework) | S |
| H-19 | Migrace V1→V2: `TextStyle` flat→nested (breaking change 2026-03-24) + varování v UI | 🟡 framework připraven, konkrétní migrace nenapsaná (YAGNI — žádné V1 šablony) | S |

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

- **Fáze 2 — stylový pod-formulář (rozšířit + uklidit)** — 🟡 prototyp 2026-06-22 (1 zóna). Hotovo:
  `StyleEditDialog` + `StyleEditDialogViewModel` (generický pod-formulář pro 1 TextStyle přes sdílený
  `CellStyleEditor` — tri-state zděděné placeholdery), EDInputform `ButtonClicked` event (EDControls),
  Odjezdy „Styl záhlaví" → tlačítka „Upravit styl…". **Zbývá:** (1) rozšířit na všechny zóny Odjezdů
  (DataStyle/MinuteStyle/Anotace symboly+text) + **StopList** (HeaderStyle/DataStyle) → hlavní „Obecné"
  všude = scaláry + tlačítka „Styl…"; (2) uklidit — `StyleEdit.cls` (nepoužitá Caché varianta A),
  zvážit zda `StyleFields` helper zůstane (inline zóny) nebo se styl celý přesune na CellStyleEditor;
  (3) drobnost: pod-formulář aplikuje na parent OK (akumulace keyovaná prefixem) — OK.
- **Export: extrahovat členění + název souboru výš** — `__EXPORT_SPLIT` + `__EXPORT_FILENAME` jsou
  zatím export pole v tiskovém EDInputformu (`AgendaParams`, persistence per-agenda přes `PrintParams`,
  ✅ 2026-06-22). Do budoucna zvážit přesun na úroveň **`Template`** (perzistuje se šablonou jako
  `CultureCode`) nebo **agenda-config** (firemní default, jak to měl legacy v „Tisk → Linky-MHD").
  Pattern používá `{POLE}` tokeny — případně do UI doplnit picker dostupných polí agendy.
- **Ad-hoc kontrola přetečení v tisku** (na žádost zákazníků) — dnes se přetečení přes všechny instance
  počítá až při exportu reálných dat (`PreviewCanvas.CountOverflowingPages` → hláška „⚠ X z N stránek
  přetéká"). Kdyby zákazníci chtěli, přidat samostatné tlačítko „Zkontrolovat přetečení" v `PrintDialog`,
  které fetchne data a vypíše počet bez generování PDF. Zatím YAGNI.
- **Sjednotit API editoru sloupců** — ✅ **HOTOVO (Fáze 1, 2026-06-22)** pro StopList + DepartureGrid:
  `IColumnRowVM` (`DisplayName`/`Visible`/`HasAnyOverride`/`ColumnWidth?`) + sdílený `ColumnListEditor`.
  **Zbývá:** TableElement (má vlastní DataGrid editor sloupců — záměrná výjimka, sjednotit jen pokud
  bude potřeba).

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
| **Dev firma v appsettings** | `templatedesigner.appsettings.json` má dev `CB_CSADAD` (demo gold linka 0365001) — před produkcí ověřit cílovou firmu (`KV_DP` apod.). Startup šablona už revertována na produkční MHD+3 sekce (2026-06-21). |
| Full migration pipeline | Po H-19 rozšířit na každou breaking change (ne jen 2 verze) |
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

## 🗄️ Hotová práce

Detail dokončených etap (v1.0 demo balíček, Fáze A.2, B0/B0.5, JŘ elementy, PageLayoutEngine, …) → [`roadmap-archive.md`](roadmap-archive.md). Průběžný changelog → [`changelog.md`](changelog.md).

