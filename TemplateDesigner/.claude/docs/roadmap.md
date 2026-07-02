# Roadmap

## 📋 Aktuálně rozpracováno / nejbližší kroky (stav k 2026-06-30)

> Obě zákaznické prezentace proběhly (statická ~19.6., živá 26.6.). Handoff blok 2026-06-21
> archivován do [`roadmap-archive.md`](roadmap-archive.md). Zastávkový JŘ (AD) je datově 1:1 +
> výkonově hotový. Nově (30.6.) je **databáze primární úložiště šablon** (Ctrl+S → DB, soubor =
> import/export).

**Aktivní rozpracované tracky:**
- **Wave 4 EDInputForm** — Fáze 1 + W4-6 ImageElement ✅ hotové. **W4-9 komplexní elementy z větší
  části hotové** (StopList ✅ + DepartureGrid ✅ mají „Obecné" přes EDInputForm); **zbývá už jen
  TableElement** — plán „Dynamic-first, Fixed odložen" viz Wave 4 sekce níže. Stylový pod-formulář
  zatím prototyp (1 zóna). **Priorita:** spíš později, za aktuálními provozními věcmi.
- **AD nastavení linky (PRIORITA 2b)** — **zastávkový JŘ je 1:1 kompletní** (2026-07-02). AD-N4 (NZ na
  nový řádek) + AD-N11 (cizojazyčné) **přeřazeny do linkových JŘ** (legacy zastávkový je nepoužívá).
  Zbývá jen vizuál legendy + AD-N5/N15 (blokované dormant daty).
- **Architektonický track (z analýzy 2026-07)** — T1a C# testy ✅ (164 testů, pre-push) + T3 migrator
  guardy ✅ + T2 async export ✅ (vše 2026-07-02). Zbývá T1b Caché testy (po dokončení AD WIP),
  T4/T5 průběžně — viz sekce „Architektonický track" níže +
  [`analyza-stav-a-legacy-nahrada-2026-07.md`](analyza-stav-a-legacy-nahrada-2026-07.md).

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

1. **Zastávkový JŘ (AD)** — *hlavní deliverable*. Datově 1:1 + výkonově **hotovo**. **Multi-varianta
   TrasaMin (AD-N13) ✅ hotovo 2026-07-02 (L1).** **Zastávkový JŘ je tím 1:1 kompletní.** AD-N4/N11
   přeřazeny do linkových JŘ (legacy zastávkový je nepoužívá). Zbývá jen vizuál legendy. Číslování omezení (cnz) ✅ 2026-06-23.
2. **Wave 4 EDInputForm** — W4-6 ImageElement ✅; W4-9 StopList ✅ + DepartureGrid ✅; **zbývá TableElement** (Dynamic-first, Fixed odložen — viz Wave 4 sekce). Spíš později, za provozními věcmi.
3. **Helpdesk pro AD** — Fáze A.2-D hotová (#7955/#7947/#7161 zavřeno). #11300 KORID přípoje přeřazeno
   k linkovým JŘ (není zastávkový koncept). Zbývá jen odložené A2-A5/A6/B4 (datový průzkum / kolegové).
4. **Nové z prezentací (P1-P3)** — backlog, neanalyzováno; před implementací založit ticket.
5. **AI pomocník (stretch)** — funkční (generování šablony, PDF import, vision pipeline), další rozšíření low-prio.

> **Pozn.:** ⬅️ markery v sekcích níže pocházejí z dřívějšího plánu — autoritativní pořadí je tento
> seznam.

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
- ~~**Multi-varianta TrasaMin**~~ ✅ **hotovo 2026-07-02 (L1)** — dynamické variantní časové sloupce (N dle časového profilu W-mapy, ne fixních 15), `TRASA_MIN[]`/`VARIANTY_TRAS[]`, StopList rozvine „Čas" na N sub-sloupců, přepínač `ShowAllRouteVariants`. 1:1 ověřeno gold 0365005.
- ~~Číslování omezení TYPPOZ=1~~ ✅ **hotovo 2026-06-23** — marker = číslo NZ (`cnz`) invertovaně, žádný firm gate (changelog / §10).
- Šířka/layout legendy POZNAMKY (**vizuál** proti gold — počet sloupců, šířky, mezery) — na konec. Pozn.: **pořadí** poznámek už ✅ ověřeno 1:1 (2026-06-30), tady jde jen o vzhled.
- **DEV cleanup před produkcí** — ✅ odstraněn `#if DEBUG` auto-open `kv_ad_test.json` + `--test-detector` blok (2026-06-17). Zbývá: zvážit statický `GetMockData`.

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
| AD-N4 | — | ~~Negativní značky na nový řádek~~ → **linkové/PS JŘ** (legacy `NegZnNR` jen v `JRapiLinPrtPScm`, zastávkový `JRapiLinPrtMhd` ho NEpoužívá; v zastávkové legendě je navíc každé omezení už na vlastním řádku) | — | ➖ přeřazeno (2026-07-02) |
| AD-N5 | §3 | `MHD_NEZNZ123` — potlačit NZ v legendě per sloupec | `PATCH,"MHD_NEZNZ123"` | M (váže na NZ legend, dnes dormant) | ⬜ |
| AD-N6 | §3 | Override nadpisů sloupců (skupin dnů) → TD vlastnost `HeaderOverride` (ne port `^JRSET`) | (TD vlastnost elementu) | S | ✅ 2026-06-22 |
| AD-N7 | §5 | **Komentáře** (ruční poznámka linky + přiřazené spoje) | `MhD,"SeT_SpeC"` + per-spoj `MhD_SpeC` | M-L | ✅ 2026-06-20 (Fáze 1) |
| AD-N8 | §5 | **Spoje, nepoznámkovat** (gate ručního komentáře, 1:1 legacy) | `PATCH,"MHDSPNPZN"` (`odTC~doTC`) | M | ✅ 2026-06-20 (Fáze 1) |
| AD-N9 | §5 | Směrové poznámky tam/zpět/vše | `MhD,"SeT_MhD",id,cnt` | S-M | ✅ 2026-06-20 (Fáze 1) |
| AD-N8b | §5 | **Fáze 2** — co schovat v rozsahu „nepoznámkovat" (#7955) = parametr sestavy `SCHOVAT_V_ROZSAHU` (Ruční/Automatické/Obojí) + TD render-filtr nad nadmnožinou z Caché | param + `NoteVisibilityFilter` | M | ✅ 2026-06-20 (Fáze 2) |
| AD-N10 | §4 | Text „spoj jede až do XY" (prodloužení trasy) | rozšíření `RozdilyTras` | M | ✅ 2026-06-21 (#7947) |
| AD-N11 | — | ~~Cizojazyčné poznámky~~ → **linkové/PS JŘ** (legacy `prtCzjPzn`/`TxT_MezI` jen v `JRapiLinPrtPScm`, zastávkový `JRapiLinPrtMhd` je NEpoužívá) | — | ➖ přeřazeno (2026-07-02) |
| AD-N12 | — | ~~Km sloupce~~ → **patří do linkových JŘ** (tarif dle km), ne zastávkové | — | — | ➖ přesunuto |
| AD-N13 | §4 | Multi-varianta časů tras (víc časových sloupců) | `mhd,"par",2` + C# renderer | L | ✅ 2026-07-02 (L1) — dynamické variantní sloupce, 1:1 gold 0365005, přepínač `ShowAllRouteVariants` |
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
| 4 | Form-driven UI | 🟡 Rozpracováno | EDInputForm místo PropertiesPanel. Elementy + Image (W4-6) + Section + parametry tisku + W4-9 StopList/DepartureGrid ✅. Zbývá **TableElement** (Dynamic-first, Fixed odložen) + Page + inline panel cleanup |
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
| W4-9 | **Komplexní elementy** — tab „Obecné" → EDInputForm, sloupce/per-column část zůstává custom (`ColumnListEditor` + `CellStyleEditor`, EDInputForm neumí editovatelnou tabulku). **StopList ✅ + DepartureGrid ✅ hotové** (`StopListElementEdit.cls` / `DepartureGridElementEdit.cls`). **Zbývá TableElement** — viz plán níže. | 🟡 zbývá Table |
| W4-10 | Inline `PropertiesPanel` → EDInputForm (live mode) — plný dedup PropertyDescriptors. Velký refaktor, odložené | |
| W4-11 | Cleanup: PropertyDescriptor systém smazat až po 100% migraci | |
| W4-12 | (volitelné) EDInputForm wire formát `$lb()` OUT → JSON envelope; collapsible groups | |

**Co je / není kandidát na EDInputForm** (rozhodovací pravidlo, dohodnuto 2026-06-12):
- ✅ **Ano:** formuláře s **pevným, server-definovaným schématem** (vlastnosti elementu/sekce/stránky; parametry agendy z `GetParameters`). Cesta: `*Edit.cls` (reflexe přes property path) nebo generická `AgendaParams` (z param JSON).
- ❌ **Ne:** (1) triviální confirm s 1 polem (nulový dedup, jen Caché round-trip navíc); (2) dialogy s **živě počítaným náhledem** z client-side dat (Normalizovat pozice); (3) plně dynamické/data-driven formy bez server schématu. visibleWhen (podmíněná viditelnost) jde, ale evaluuje se **v C#** (pravidlo definuje Caché v JSON) — `Validate` roundtrip nenese InitParams.
- **Reusable pattern:** `AgendaParams` (jedna generická dispatch třída převádějící `GetParameters` JSON → EDInputForm Form) je vzor pro budoucí Caché-driven formy stavěné z existujícího JSON kontraktu (ne ručně psané `*Edit.cls`).

**Pořadí dalších kroků:** W4-6 (Image) ✅ → W4-9 StopList ✅ + DepartureGrid ✅ → **zbývá TableElement** (plán níže) → W4-7 Page → W4-10/11 (inline panel + cleanup, na konec).

##### W4-9 zbytek — TableElement (plán, spíš později za provozními věcmi)

**Rozhodnutí (2026-06-30, diskuze):** TableElement dotáhnout na stejný vzor jako StopList/DepartureGrid,
ale **po částech a Dynamic napřed** (autor preferuje rozdělit a případně zase sloučit, až bude v dobrém stavu).

**Architektura — sdílený základ + render per element** (NE dědičnost z TableElementu; pivot by nesměl dědit row-render):
```
Sdílený základ:  DataSource vazba + definice sloupců + per-sloupec styl (CellStyleEditor) + Obecné (EDInputForm)
   → Dynamic Table (render: řádky)  |  StopList (řádky + dekorace)  |  DepartureGrid (PIVOT) + extra
```
„Dynamická tabulka + extra" platí na úrovni **nastavení/dat** (sdílené už dnes přes `ColumnListEditor`/`CellStyleEditor`),
**ne na úrovni renderu** (DepartureGrid je pivot, ne row-tabulka).

**Kroky:**
1. **Dynamic tabulka jako standard (první)** — 2 taby: „Obecné" → EDInputForm (`TableElementEdit.cls`:
   `DataSource` picker z `collectionKey` agendy, header/default styl, border/bg, pozice) + „Sloupce" →
   sdílený `ColumnListEditor` + `CellStyleEditor`. Žádný cell grid (řádky z dat). **Unblockne faktury (POLOZKY).**
2. **Fixed odložit** — nechat běžet na **stávajícím editoru** (nemodernizovat, ale **nerozbít** — Fixed
   je nositel Caché table-templates `GetTableTemplate`: ZAHLAVI_LINKY / PLATNOST_DOPRAVCE). Dodělat eventuálně později
   (komplikovaná část = editovatelná 2D cell grid).
3. **Sjednocení gridů na `EDInputGrid`** (až bude víc konzumentů: Table + oba JŘ editory) — sdílený
   `ColumnListEditor` je předstupeň; swap naráz, contained refaktor.

**Otevřené rozhodnutí (až u implementace):** formální split Fixed/Dynamic na 2 elementy vs jeden
element s `Mode` + podmíněný editor. Lehčí cesta (bez JSON migrace) = nechat 1 `TableElement`, postavit
moderní editor na Dynamic módu, Fixed mód na starém editoru. Split jen pokud `Mode` v praxi překáží.

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
| H-18 | `TemplateMigrator.Migrate(JsonDocument)` — detekce verze v JSON, pipeline migrací, spouští se před deserializací | ✅ 2026-05-25 (W3-4 framework) + ✅ **2026-07-02 T3 guardy** (forward-compat R5: novější verze → odmítnout; selhání migrace → výjimka; sémantické porovnání verzí; pokryto `TemplateMigratorTests`) | S |
| H-19 | Migrace V1→V2: `TextStyle` flat→nested (breaking change 2026-03-24) + varování v UI | 🟡 framework připraven, konkrétní migrace nenapsaná. **V1 v DB neexistují** (ověřeno 2026-07-02, DB storage vznikl po breaking change); zbytkové riziko jen staré JSON soubory na disku (bez `version` pole) | S |

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


### 🟠 Fáze C + D: Výlepový/knižní JŘ linky (náhrada TiskJR) — pravděpodobně JEDEN track

> **Poznatek z hloubkového čtení legacy (analýza 2026-07 §3.3):** legacy `TiskJR` generuje
> **vývěsní i knižní JŘ z téhož enginu** — Fáze C (meziměstský/výlepový) a D (knižní) jsou tedy
> nejspíš jeden track, ne dva. Formát: sloupcová tabulka celé linky — svislý seznam zastávek
> (Zóna/Km/Tč/Název/Šipky) × sloupce spojů s časy; řádek platností se symboly/NZ pod čísly spojů;
> řádky Vyčká/Navazuje; skupinová záhlaví PD/So/Ne (3 režimy dělení); poznámky 10 druhů.
> **Gold oracle je `mkPLinWin` (ALVA.JRMAIN.MLAPI)** — ne `ALVA.TISKMHD.Data`; metodika = čistá
> reimplementace à la `ZastavkoveJrAd`. Velikost ≈ zastávkový JŘ (~1 měsíc).
>
> **Prerekvizity / nové schopnosti (pořadí):**
> 1. **Symbolový font Timetable_nove (+SK)** do SkiaSharp renderingu + převodní tabulka
>    (`ConvertSpecialniZnak` mapování 66+ symbolů známe z obou legacy systémů; TTF sada
>    v `C:\DEV\EDISON\TiskJR\_ruzne`). Prerekvizita jakéhokoli knižního/výlepového JŘ.
> 2. **Agenda `KnizniJrAd`** — čistá reimplementace, oracle `mkPLinWin`.
> 3. **`LineTableElement`** (zastávky × spoje) + layout rozšíření: flow skládání datových paketů
>    (N směrů vedle sebe/pod sebe dle místa), „dotisk poznámek z minulé stránky",
>    **auto-fit fontu na šířku** se zpětným přepočtem sloupců (ComputeFontScale je dnes jen výškový).
>    Legacy „měřicí řetězce" ping-pong odpadá — Skia měří lokálně.
> 4. Per-linka šablony (viz B4) — legacy `LoadNastaveniProTisk` (šablona per A4/A5).

| Krok | Co | Stav |
|---|---|---|
| 42 | Analýza referenčních výstupů (vývěsní + knižní PDF) + wire formát — nad rámec analýzy §3.3 | |
| 42b | Symbolový font Timetable do Skia + převodní tabulka | |
| 43 | `LineTableElement` + layout rozšíření (flow paketů, auto-fit šířka, dotisk legendy) | |
| 44 | Caché `KnizniJrAd` — GetData/GetMockData (Wave 5 pattern), oracle `mkPLinWin` | |

---

### 🟠 Fáze E: Vozový JŘ + Relace

Layout TBD — hlavička oběhu, tabulka spojů, poznámky. Pravděpodobně víc elementů (viz poučení z MHD).
Legacy generátory: `ALVA.MHD.ReportGenerator` (MHD) + `AD.ADREPORTGEN` (linky) — viz
[`legacy-schedule-generators.md`](legacy-schedule-generators.md).

**Relace (TiskJRRELACE)** — třetí typ výstupu (vývěsky nádraží: řádky = odjezdy, sloupce =
linka/čas/směr/nástupiště; slovenský zákazník, copy-paste fork TiskJR ~50k LOC). **Nejdřív ověřit,
zda je to živý use case** (otevřená otázka Q3 níže) — pak vlastní agenda + element.

| Krok | Co | Stav |
|---|---|---|
| 48 | Analýza referenčních vozových JŘ — definice layoutu a wire formátu | |
| 49 | `VehicleScheduleElement` — implementace (pravděpodobně víc nezávislých elementů) | |
| 50 | Caché `VozovyJrAPI` — GetData/GetMockData + async loading (R1, velký objem dat) | |

---

### 🟠 Fáze F: Náhrada REPORTGENERATOR (univerzální sestavy) — NOVÉ (analýza 2026-07)

> Největší kus náhrady legacy — ne kvůli featurám (TD je ve výrazech/undo/kódech už napřed), ale
> kvůli **ekosystému ~28 agend**, které implementují RG datový kontrakt (`GetSeznamSad`/`GetSadu`/
> `GetData`, `~`-tokenový stream; dispatch `Data.cls:627-654`). Reálné nasazení: 33k textových
> elementů, 774 tabulek napříč zákazníky. **Ne big-bang** (must-not-happen #4) — etapizovat.
> Detail: [`analyza-stav-a-legacy-nahrada-2026-07.md`](analyza-stav-a-legacy-nahrada-2026-07.md) §3.1 + §5 etapa 4.

| Krok | Co | Stav |
|---|---|---|
| F1 | **Prioritizace podle usage statistiky** — legacy `Main.cls:161-188` drží reálná čísla použití per zákazník; vytáhnout, seřadit agendy podle hodnoty | ⬜ první krok (levný) |
| F2 | **Rozhodnutí bridge vs čisté agendy** (otázka Q2) — bridge = jedna `LegacyRgAgenda` překládající `~`-tokenový protokol na TD JSON DataContext → všech 28 agend najednou s omezenou věrností; čisté reimplementace (vzor Faktury) tam, kde se vyplatí. Doporučení analýzy: **bridge nejdřív** | ⬜ rozhodnutí |
| F3 | **TableElement dorovnání na RG úroveň** (navazuje na W4-9 Dynamic-first + Fáze B): per-řádek zvýraznění 0-5 z dat, per-řádek/buňka custom barvy, overflow módy buňky (vč. zalamování → klonování řádků), opakování mezisoučtového řádku na další stránce, text přes celou šířku, datové typy sloupců (datum/minuty/záporné červeně/netisknout nuly), řádek-jako-designový-layout (koncepčně PerDataRow) | ⬜ L |
| F4 | **Výstupní kanály fáze 2** — Excel export tabulek, PDF heslo + el. podpis (PDFSIGNER), **Epson znakový tisk** (otázka Q1 — možná zůstane v legacy TISK) | ⬜ dle poptávky |
| F5 | **Jazykové varianty šablon** — per-jazyk kopie provázaná na CS zdroj (kaskádový delete/copy, auto-volba dle JazykId); souvisí i s AD-N11 (otázka Q5) | ⬜ |
| F6 | **Vědomě neportovat bez poptávky** — grafy (12 použití), AZTEC (2), calculated pole (2 — výrazový jazyk TD je nadmnožina), checkbox (8); ověřit se zákazníky a škrtnout (otázka Q4) | ⬜ validace |

---

### ❓ Otevřené otázky z analýzy 2026-07 (k rozhodnutí s týmem)

| # | Otázka | Dopad |
|---|---|---|
| Q1 | **Epson znakový tisk** — migruje do TD, nebo zůstane navždy v legacy TISK? | Určuje, zda TD potřebuje ne-grafický výstupní kanál (architektonicky cizí prvek) — F4 |
| Q2 | **RG bridge vs čisté agendy** — je akceptovatelná dočasná věrnost bridge překladu? | F2, tvar celé Fáze F |
| Q3 | **Relace (SK zákazník)** — živý use case? | Fáze E scope |
| Q4 | **Grafy/AZTEC/checkbox/calculated** — má někdo tvrdou závislost? (usage: 12/2/8/2) | F6 škrtnutí |
| Q5 | **Jazykové varianty šablon** — potřebné i pro JŘ (pohraniční vývěsky, AD-N11), nebo jen RG doména? | F5 priorita |
| Q6 | ~~V1 šablony v terénu~~ — **v DB neexistují** (ověřeno 2026-07-02); zbývá potvrdit staré JSON soubory na disku z éry před 2026-03-24 | H-19 uzavření |

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
- **Font follow-ups** (navazuje na „Výchozí font na úrovni šablony", 2026-06-30): (1) **live varování
  „nenainstalovaný font"** — dnes `FontResolver` tiše fallbackuje na Arial; chce **warning kanál
  v EDInputformu** (zobrazit uživateli, že font není k dispozici); (2) **rozšířit `Template.DefaultTextStyle`**
  z „jen font" i na size / barvu / rámeček — **„jen když budou chtít"** (YAGNI, model už pole má, chybí UI).
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

### 🔧 Architektonický track — technické dluhy T1–T6 (z analýzy 2026-07)

> Zdroj: [`analyza-stav-a-legacy-nahrada-2026-07.md`](analyza-stav-a-legacy-nahrada-2026-07.md) §2.3
> (technický audit). Optika: regresní pojistky + zdraví kódu před růstem plochy (knižní JŘ, RG).
> Běží paralelně s business trackem — položky vybírat tak, aby nekolidovaly s rozpracovanými soubory.

| ID | Co | Stav |
|---|---|---|
| T1a | **C# testy** — `TemplateDesigner.Tests` (PageLayoutEngine, výrazy, serializace round-trip, migrator, TextStyle kaskáda + DepartureGrid anti-pattern regrese, helpery, EDInputForm drift test) v pre-push | ✅ 2026-07-02 (164 testů) |
| T1b | **Caché testy** — `ALVA.TemplateDesigner.Tests` v pre-push (`run-cache-tests.ps1`), **25 testů ✅ 2026-07-02** (celá Caché sada 40/40): `SablonyTest` (CRUD úložiště šablon — round-trip nad vlastními daty v izolované IdFa větvi `EDTESTING`), `ZastavkoveJrMhdHelperyTest` + `ZastavkoveJrAdHelperyTest` (čisté helpery: ZarazeniSpoje FTL 0-3, W-mapa časy, datumová omezení, markery), `ZastavkoveJrAdSmokeTest` (strukturální smoke `GetData` — linka dynamicky přes `PrvniLinka`, asserty na strukturu ne hodnoty). **Zbývá:** `tools/compare-gold.ps1` (1:1 vs legacy jako skript mimo hook) + volitelně AgendyRegistr/EDInputForm Init smoke | 🟡 zbývá compare-gold |
| T2 | **Async multi-instance export** — fetch + render na threadpoolu (R1) nad **snapshotem šablony** (`TemplateSerializer.DeepClone` → editace během exportu výstup neovlivní, UI zůstává plně použitelné, bez blokujícího overlay), dvoufázový progress ve status baru (fetch per instance + render per stránka), orchestrace z code-behind → VM (= první řez T4). Cancel vynechán (až na reálnou potřebu); sync zůstává první `GetSchema` hit (mitigován `_schemaCache`) | ✅ 2026-07-02 |
| T3 | **Migrator guardy** — forward-compat (R5), selhání migrace → výjimka, sémantické porovnání verzí | ✅ 2026-07-02 |
| T4 | **God objects strangler** — `MainWindow.xaml.cs` (2,7k) + `TemplateDesignerViewModel` (2,5k): pravidlo „nové featury = nová služba/soubor" + postupné řezy (Export ✅ s T2, Instance, TemplatePersistence); R4 fix `TableEditorDialogViewModel:609` (WPF UI ve VM); rename `MainWindowViewModel.cs` → název třídy | ⬜ průběžně |
| T5 | **Zbylé duplikace** — `EDList/Instance.cls:113` hardcoded mirror agend registru → napojit na `AgendyRegistr`; `ZastavkoveJrAd.GetMockData` → statický mock (nezávislý na firemních datech). Drift test schémat ✅ (T1a) | ⬜ malé |
| T6 | Drobnosti — `architecture.md` nezná `PdfImport/`+`Services/` (7k LOC); `MessageBox` v `MainWindow.xaml.cs:1021`; per-frame `new SKPaint` v Render() běžných elementů; `ScheduleDetectorTest.cs` v produkční assembly | ⬜ nízká, při příležitosti |
| — | **StopList kaskáda testy** (analogie DepartureGridCascadeTests) | ⬜ po dokončení StopList WIP |

---

### 🔵 Produkční / integrační připravenost (co musíme udělat pro nasazení k zákazníkům)

> **Optika:** ne featury, ale „aby to šlo nasadit a integrovat pro zákazníky". **Klíčové zjištění
> (2026-06-30):** startup se připojuje k **jedné firmě z `templatedesigner.appsettings.json`** a
> **nebere žádný kontext z EDISONu** (`App.OnStartup` nečte cmdline args) → blok A je největší kus.

#### Kritická cesta (pořadí must → should)
1. **A1 — Caché session kontext z EDISONu** (blocker pro multi-zákazníka).
2. **A2/A3 — launch nad linkou + end-user tisk** (aby zákazník produkoval výstup z EDISONu).
3. **B — systémové šablony + default sady** (aby měl co tisknout, aniž by navrhoval).
4. **C — H-19 migrace** (než se uloží reálné šablony).
5. **D — produkční config + deploy proces**.

#### 🔴 A. EDISON integrace (dnes prakticky chybí — největší kus, NENÍ rozepsaný)

> **Legacy vzory (analýza 2026-07):** všechny 3 legacy systémy se volají z EDISONu přes EDForm
> s `PoleSestavaValue`; REPORTGENERATOR navíc **headless CLI** `AUTOEXPORT=1` / `AUTOPRINT=1`
> volané přímo z Caché agend (`MainRun.vb:17-184`) — vzor pro A3. Vrstvy 1–4 TD to architektonicky
> umí, chybí jen entrypoint.

| ID | Co | Stav |
|---|---|---|
| A1 | **Předání Caché session kontextu** (server / firma / uživatel / mlp) z EDISONu místo hardcoded appsettings. Vzor: legacy `EDISONMAIN2API.FormToRunGetter` (sentinel + ~10 args). Bez toho TD jede jen proti 1 nakonfigurované firmě. | ⬜ blocker |
| A2 | **Launch nad konkrétní linkou** — otevřít TD z modulu JŘ/MHD s předvybranou agendou + instancí. Startup dnes args nečte. | ⬜ |
| A3 | **End-user tiskový vstup** — zákazník hlavně *tiskne*, nenavrhuje. Headless tisk (vrstvy 1-4) z modulu nebo „vytiskni linku X". Dnes je print jen v designeru. Legacy vzor: RG CLI AUTOEXPORT/AUTOPRINT. | 🟡 |
| A4 | **Výstupní kanály fáze 1** — sloučení hromadného exportu do **1 PDF** (legacy `MergePDFFiles`) + **odeslání PDF e-mailem** — společný požadavek všech 3 legacy systémů (TiskMHD/TiskJR/RG je mají všechny) | ⬜ M |

#### 🟠 B. Šablony pro zákazníky (governance + distribuce)

> **Legacy vzor (analýza 2026-07, sdílený všemi 3 systémy):** dvouvrstvý model „MlineSestava"
> (vendor šablony ID ≥ 1000 read-only × zákaznické < 1000), release pipeline
> `ExportReportToGlb` → `ImportTransferGlobal` → `AktualizujAgendu` (rozkopíruje do všech IdFa
> při instalaci verze), audit log změn (`^Alva*Log`), právo „pouštět designer" (ECL).
> **Jedna sdílená infrastruktura k postavení jednou** — bez ní nejde žádnou náhradu legacy
> nasadit „jako produkt". Detail: analýza §4.

| ID | Co | Stav |
|---|---|---|
| B1 | **Systémové/zamčené šablony** (M-line tvoří + distribuuje, zákazník read-only) — dnes jen per-IDfa storage bez „zamčené" (gap P2-6). Legacy: `MlineSestava` flag + ID práh | ⬜ |
| B2 | **Výchozí sady šablon per agenda** (MHD / AD JŘ / faktury) k nasazení + distribuční pipeline v release (ekvivalent `AktualizujAgendu`, formát JSON místo GOF) | ⬜ |
| B3 | Kategorie + popis šablony v UI (metadata už drží Author/Created/Modified/Version) | ⬜ |
| B4 | **Per-instance přiřazení šablony** — linka → výchozí šablona (legacy: jiná pro A4 a jiná pro A5 + ~17 vlastností per formát, `LoadNastaveniProTisk`). `InstanceDefaults` (Wave 1) rozšířit o šablonu; potřebné pro JŘ produkci (A2/A3 launch „vytiskni linku X") | ⬜ |
| B5 | Audit log změn šablon + právo „pouštět designer" (ECL) — legacy parita, malé | ⬜ |

#### 🟠 C. Bezpečnost dat (než zákazníci ukládají šablony)
| ID | Co | Stav |
|---|---|---|
| C1 | **H-19 konkrétní migrace** (V1→V2) — `TemplateMigrator` framework ✅, migrace nenapsaná (no-op) | 🟡 |
| C2 | **Full migration pipeline** pro každou breaking change | ⬜ |

#### 🟡 D. Konfigurace / deployment
| ID | Co | Stav |
|---|---|---|
| D1 | **Dev firma `CB_CSADAD` → produkční** (nebo z EDISON kontextu, viz A1) | ⬜ |
| D2 | Anthropic API klíč prázdný/managed v produkci (AI graceful bez něj) | ✅ |
| D3 | **Distribuce .exe** — `publish.ps1` → `C:\Edison\TemplateDesigner.exe`; jak na zákaznické stanice (EDISON deploy?) | 🟡 build ✅, deploy proces nedefinován |
| D4 | **Nasazení Caché tříd** (agendy, `API.Sablony`, …) na zákaznický Caché server | ⬜ proces ověřit |

#### 🟢 E. Multi-tenant
| ID | Co | Stav |
|---|---|---|
| E1 | Per-IDfa template storage + per-user prefs | ✅ (Wave 1+3) |
| E2 | Per-zákazník enablement agend (vidí jen své) | ⬜ odloženo (Wave 2 YAGNI) |

#### 🟢 F. Provoz / podpora / kvalita
| ID | Co | Stav |
|---|---|---|
| F1 | Error handling přes `IPopupDisplayer` | ✅ |
| F2 | Testy (pre-push: xUnit + Caché) | ✅ |
| F3 | **Logging / telemetrie pro podporu** | ⬜ ověřit |
| F4 | CI/CD linting šablon (Caché job před deployem) | ⬜ odloženo |
| F5 | Výkon (batch 600/9s, overflow check, MHD perf analýza) | ✅ |

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

