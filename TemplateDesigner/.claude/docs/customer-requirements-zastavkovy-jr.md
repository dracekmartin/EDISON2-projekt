# Zastávkový JŘ — požadavky zákazníků z helpdesku

Snapshot z Podpory M-line k **2026-05-11**. Slouží jako vstup pro plánování dalšího rozvoje TemplateDesigneru a Caché `ZastavkoveJrMhdAPI`. Aktualizuj při dalším procházení tiketů.

> **Postup pro AI agenta:**
> 1. Otevři odpovídající tiket přes `mcp__claude_ai_Podpora_M-line__get_ticket` s UUID z tabulky.
> 2. Pokud řešíš jednu z featur → ověř aktuální stav v UI a v `ZastavkoveJrMhdAPI`.
> 3. Po implementaci přesuň položku do sekce „Hotovo" a doplň datum + commit.
> 4. Při interpretaci nezjednodušuj — některé požadavky jsou jemné rozdíly proti tomu, co TD dnes dělá (např. „máme šipku směru, ale klient ji chce v opačné pozici").

---

## Aktivní tickety (otevřené + pozastavené, agenda Jízdní řády / MHD)

| # | UUID | Stav | Zákazník (kontakt) | Téma |
|---|---|---|---|---|
| 10660 | f3d3a7d8-0c3d-4076-93d1-c12bb25e851d | suspended | **DPKV** (Jan Slavík) | 4 oblasti grafických připomínek po importu z legacy |
| 11300 | 653b5c65-b84f-49e8-803f-329febe5629e | suspended | **KORID** (Jaroslav Smištík) | Skrýt prázdné přípojové řádky (PD vs. víkend) |
| 8828  | 02063e8a-e2c1-476d-a527-77e77e0f263e | suspended | **TRANSDEV** (Michal Podloučka, Jakub Holík) | Per-sloupek zastávky — samostatná vývěska |
| 8772  | b62718c6-e0aa-478a-be31-bdfb8c7b9917 | suspended | **TRANSDEV** (Jakub Holík) | Návrhář sestav — podmíněný tisk sekcí |
| 7947  | 585158b0-d063-40d9-87de-eb076a236a87 | suspended | **TRANSDEV** (Michal Podloučka) | Logika auto-poznámky u poslední/předposlední zastávky |
| 7959  | 45fdc840-ce70-4436-a331-c5a61732d9d6 | suspended | **TRANSDEV** (Michal Podloučka) | Skrýt zastávku v seznamu, pokud z tištěné zastávky není obsluhována |
| 7955  | dd0f7bd3-0efa-4e2d-947d-ed16559952ce | suspended | **TRANSDEV** (Michal Podloučka) | Vlastní komentář spoje má přebít auto-poznámku + 2 buga |
| 7937  | 64fd847c-7090-47d1-bc91-bc581b0aea42 | suspended | **TRANSDEV** (Michal Podloučka) | Odsazení/odrazení zastávky per směr, jen pro zastávkové JŘ |
| 7161  | 44e0a446-b05b-4c44-9497-3c96fbb945a2 | suspended | **TRANSDEV** (Vladimír Šimko) | Vypínatelné automatické poznámky (na znamení, bezbariér.) |
| 7487  | 20a4ff75-b7c1-4fb9-80cc-5d7be57c9dcf | suspended | **KORID** (Jaroslav Smištík) | NZ + týdenní platnost — 4 na řádek (úspora místa) |

Otevřené tikety #12434 (BBSK — „Export Tisk JŘ") a #12260 (BUSLINE — „úprava šablony") jsou obecné tracker / dotazy, ne konkrétní featury — neuvedeny.

---

## A. StopListElement — seznam zastávek vlevo

### A1. ✅ HOTOVO (2026-05-12) — viz §G

### A2. ✅ HOTOVO (2026-05-12) — viz §G

### A3. ✅ HOTOVO (2026-05-12) — viz §G

### A4. ✅ HOTOVO (2026-05-12) — viz §G

### A5. ⏸️ Odloženo (2026-05-12) — diskuze s kolegy potřeba

- **Zákazník:** TRANSDEV (Michal Podloučka), #7937
- **Citace:** *„potřebuji mít možnost odstadit/odrazit zastávku o určitý počet bodů, ale pouze pro zastávkové JŘ a pro jednotlivé směry (v jednom směru ano, v druhém ne)"*
- **Dodatek:** *„zda by šlo odražení nastavit tak, že se bude odrážet zastávka v nastavených Tč vývěsu"* (per časové pásmo)
- **Stav infrastruktury:**
  - ✅ TD model: `ZastavkaData.Odsazeni: int` + `ShowStopIndentation` toggle
  - ✅ TD render: prefix spaces (primitivní, potřebuje vylepšit na proper padding)
  - ✅ Caché schema: `ODSAZENI` field v `BuildZastavka` (default 0)
  - ❌ Caché data flow: `ZastavkoveJrMhdAPI.cls:881` má hardcoded `s odsazeni = 0` s komentářem „(legacy: dta 10)" — kdysi se to načítalo z legacy DB pole, v novém kódu nedotaženo
- **Důvod odložení:** potřeba domluvit s kolegy:
  - **Kde brát data** o odsazení per zastávka per směr (legacy field „dta 10" v `ALVA.MHD.ZST`? jiný?)
  - **Jak se v EDISONu konfiguruje** odsazení per zastávku (UI?)
  - **Jakou logiku** používat — per-směr, per-Tč pásmo, kombinace?
- **Není přímo TD práce** — TD má infrastrukturu, čeká na Caché data flow doplnění a UX rozhodnutí.

### A6. 🔍 Pravděpodobně hotovo (2026-05-12) — vyžaduje verifikaci s reálnými daty

- **Zákazník:** TRANSDEV (Michal Podloučka), #7959
- **Citace:** *„Mám linku, která má druhou zastávku pro školní spoj, a mohl bych pro ní vytisknout JŘ. Ovšem z první zastávky na tuto druhou zastávku žádný spoj nepojede. Potřeboval bych, aby zastávka nebyla v seznamu zastávek, pokud není aktuální."*
- **Zjištění:** **Filter už existuje v `ZastavkoveJrMhdAPI.cls:862-864`** (po refaktoru z legacy):
  ```objectscript
  // Za aktuální: jen obsluhovatelné zastávky
  i (SmerID=0)&&(bodIterator>aktualniZastBodID)&&('$e(ObsluhaBitmap,bodIterator)) continue
  i (SmerID=1)&&(bodIterator<aktualniZastBodID)&&('$e(ObsluhaBitmap,bodIterator)) continue
  ```
- **Logika:** Pro každý bod v trase za aktuální zastávkou se kontroluje `ObsluhaBitmap` (= union obsluhovaných zastávek ze všech spojů z aktuální zastávky, viz fáze 5 `VypocitatBitmapyObsluhy`). Pokud bit pro daný bod není nastavený → zastávka se do `ZASTAVKY` arrayové nepřidá.
- **Chování:** **Hardcoded, vždy aktivní, bez vypínače.** Komentář na řádku 855 *„ZASTAVKY — vždy všechny v daném směru"* je zavádějící — kód reálně filtruje.
- **Co je potřeba:**
  - Otestovat s konkrétními TRANSDEV daty (např. linka 876xx se školní zastávkou) — funguje to v praxi?
  - Pokud ano → uzavřít tiket s referenční šablonou (jako E1, A4).
  - Pokud bude některý klient chtít opt-out (vidět všechny zastávky bez filtru) → doplnit parametr `TISK_VSECH_ZASTAVEK` do `GetParameters` (paralelní s `TISK_PROJETYCH_ZAST`).
  - Tiket #7959 byl podaný 2024-02-09 — to bylo o **starém legacy systému** před přepisem; v novém kódu by to mělo být OK.

---

## B. DepartureGridElement — mřížka minut odjezdu

### B1. ✅ HOTOVO (2026-05-12) — viz §G

### B2. ✅ HOTOVO (2026-05-12) — viz §G

### B3. ✅ HOTOVO (2026-05-12) — viz §G (auto-fit zvětšení odloženo)

### B4. Per-sloupek zastávky — samostatná vývěska
- **Zákazník:** TRANSDEV (Jakub Holík přes Michala Podloučku), #8828
- **Konkrétní příklad:** Linka 876420, zastávka „Těrlicko,,obecní úřad" má 2 sloupky. Většina spojů obsluhuje sloupek 2 (oba směry), ale vybrané spoje obsluhují sloupek 1. Druhý příklad: linka 863383, zastávka „Šenov,,Škrbeň" má dva sloupky v každém směru — koordinátor (KORID) generuje vývěsky **per sloupek** odděleně.
- **Citace klienta po prvním M-line návrhu** (rozdělit zastávku na 2 v trase linky): *„Vámi navržené nás s kolegy samozřejmě napadlo, ale asi to vždy není vhodné. (...) V JŘ je pouze jedna zastávka Šenov,,Škrbeň (...) Proto dále prosím o zvážení jiného způsobu řešení, než popisujete."*
- **Co máme dnes:** Multi-data PDF export generuje 1 PDF stránku per `(zastávka × směr)`. Nezná pojem „sloupek" — atribut sloupku není ani v `ZastavkoveJrMhdAPI.GetData`.
- **Co klient chce:** Pro zastávku s více sloupky vygenerovat **samostatnou vývěsku per sloupek** — jen spoje obsluhující daný sloupek se vytisknou.
- **Gap:**
  - Caché: rozšířit `BuildOdjezd` o filtr per sloupek (musí to být v datech spoje — `SpojBodZastavkaSloupek` nebo podobné).
  - `GetData` API: pro zastávku s více sloupky vrátit `OUT[N]` per sloupek.
  - TD: žádná změna — multi-data export to umí, jen dostane víc stránek.
  - Pro: zachovat single zastávku v trase linky (klient výslovně odmítá duplikaci v trase).

---

## C. NotesLegendElement — legenda poznámek pod mřížkou

### C1. Barevné odlišení značky (písmena) od textu poznámky
- **Zákazník:** DPKV (Jan Slavík), #10660
- **Citace:** *„Kdyby bylo možné barevně odlišit značku (písmeno) poznámky, zpřehlednilo by to celý zastávkový jízdní řád."*
- **Co máme dnes:** `NotesLegendElement` renderuje řádky jako `<značka> <text>`. Celá buňka má jeden TextStyle (jedna barva).
- **Co klient chce:** Značka (jeden znak / krátký kód) má **odlišnou barvu / styl** od textu vysvětlení.
- **Gap:** V NotesLegendElement zavést dvě zóny stylu — `SymbolStyle` (left) + `TextStyle` (right), oddělené pevným paddingem nebo vlastním sloupcem.

### C2. Vlastní hlavička/nadpis sekce poznámek (symetrie s levou stranou)
- **Zákazník:** DPKV (Jan Slavík), #10660
- **Citace:** *„by vypadalo lépe, kdyby celá sekce měla nadpis (hlavičku), protože by to symetricky sedělo s druhou stranou a sekcí seznam zastávek"*
- **Co máme dnes:** `NotesLegendElement` ověřit — pokud má header row, je vypnutý / není designed.
- **Co klient chce:** Sekce legendy s vlastním záhlavím (např. „Poznámky") — vizuální symetrie s headerem StopList.
- **Gap:** Přidat volitelný `HeaderText` + `HeaderStyle` do NotesLegendElement (nebo přes wrapper TextElement nad legendou). Default off pro zpětnou kompatibilitu, ON pro nové šablony zastávkového JŘ.

### C3. NZ + týdenní platnost — 4 na řádek (úspora místa)
- **Zákazník:** KORID (Jaroslav Smištík), #7487
- **Citace:** *„Kvitujeme možnost tisku 1,2 nebo 3 na řádek, protože řada NZ je jen velmi krátkých, uvítali bychom možnost tisku i 4 NZ na řádek a uspořili tím místo."*
- **Z interních komentářů M-line:** Stará Caché třída `formTISKJRReportDotNet_Module.Render_SekceDataLinky_Poznamky_RenderJedenDruhPoznamky` má ~1000 řádků; David Zeman to odložil jako 2–3 dny práce.
- **Co máme dnes:** NotesLegendElement → ověřit aktuální max počet sloupců (zřejmě 1).
- **Co klient chce:**
  - Pro **negativní značky (NZ)**: až **4 NZ na řádek**.
  - Stejné rozšíření pro **tisk týdenní platnosti** (taky krátké texty).
  - Pokud položka přeleze do dalšího sloupce, další položka začne tisknout do volného sloupce.
- **Gap:** Konfigurovatelný `Columns: 1 | 2 | 3 | 4` na NotesLegendElement + flow layout přes sloupce s podporou overflow do dalšího sloupce.

---

## D. Generování poznámek a obsluhových údajů (Caché `ZastavkoveJrMhdAPI`)

### D1. Text „spoj jede až do XY" pro spoje za základní trasu
- **Zákazník:** TRANSDEV (Michal Podloučka), #7947
- **Citace (zjednodušeno z dlouhého popisu):** Pro linky 876402, 876401, kde málo spojů jede až do koncové (Stonava, Nový svět / Horní Suchá, Pašůvka):
  - Pokud základní trasa končí v předposlední → spoje do poslední dostanou poznámku „jede přes zast. XY" (XY = předposlední) — **nesprávně formulováno, popisuje co bus míjí, ne kam dojede**.
  - Pokud základní trasa končí v poslední → všechny spoje se písmenkují (přebytečné info, když do koncové jezdí 2 spoje).
  - Pokud obě poslední mimo základní trasu → ještě horší.
- **Co klient chce:** *„Asi bych potřeboval, aby program uměl vypsat u poslední zastávky poznámku, že spoj jede až do."*
- **Co máme dnes:** `MakeOmezTrasaTxMH...` legacy helper + algoritmy 1001 (písmenková: a, b, c) a 1002 (písmena rozdílů tras: A, B, C) v `ZastavkoveJrMhdAPI`. Texty popisují, **kde spoj končí** (zkracuje trasu), ne **kam pokračuje** (prodlužuje).
- **Gap:** Nový text template pro spoje **prodlužující** základní trasu — symetrický k aktuálnímu „končí v zast. X". Detekce: spoj má v bitmapě obsluhy zastávky, které **jsou ZA** koncovou základní trasy.

### D2. Vlastní komentář spoje musí přebít auto-poznámku (ne paralelně tisknout)
- **Zákazník:** TRANSDEV (Michal Podloučka), #7955
- **Příklad:** Linka 402, spoj 20 jede jen v části trasy. Auto generuje „spoj jede pouze do zastávky XY". Klient chce vlastní text „ze zastávky XY pokračuje jako linka X do zastávky YX".
- **Pokus klienta:** zapnutí „Komentáře" → funguje, doplní písmeno + text. Současně ale nezmizí auto-poznámka.
- **Pokus o vypnutí přes „Spoje, nepoznámkovat":** chová se opačně — nechá auto-poznámku, schová vlastní komentář.
- **Citace:** *„Měl jsem za to, že když mám zapnuté automatické poznámkování, u vybraných spojů si v Komentářích zapnu vlastní poznámkování, a zadám do Spoje, nepoznámkovat stejné spoje a rozpětí Tč, budu mít u těchto spojů pouze svůj komentář. Toto mi ale teď nefunguje a je používána automatika a potlačen ruční komentář."*
- **Co máme dnes:** Auto-poznámka (rozdíly tras) a vlastní komentář se generují nezávisle ve fázi 6. Funkce „nepoznámkovat" má aktuálně sémantiku „vypnout vše kromě auto-rozdílů".
- **Co klient chce:** Pokud má spoj **vlastní komentář**, auto-poznámka rozdílů se pro tento spoj **NEgeneruje**. (Plus překlopit sémantiku „nepoznámkovat" — viz M-line interní odpověď: aktuální chování je dokumentované, ale neintuitivní.)
- **Gap:** V `BuildOdjezd` v `ZastavkoveJrMhdAPI` přidat větvení: pokud `spojPoznamky` obsahuje custom text → suppress auto-rozdíl pro tento spoj.

### D3. Bug — „Spoje, nepoznámkovat" maže i inverzní znaménko (NZ)
- **Zákazník:** TRANSDEV (Michal Podloučka), #7955
- **Citace:** *„obecně Spoje, nepoznámkovat ruší i označení inverzním znaménkem, což nevím, zda je záměr."*
- **Co máme dnes:** Funkce „nepoznámkovat" patrně omylem maže i `spojNZ` přidělení.
- **Gap:** Bug — NZ je v jiném datovém toku než ostatní poznámky, „nepoznámkovat" se ho dotýkat nemá. Opravit v 6. fázi `ZastavkoveJrMhdAPI`.

### D4. Bug — výběr spoje jen prvním číslem
- **Zákazník:** TRANSDEV (Michal Podloučka), #7955
- **Citace:** *„Výběr mezi spoji není možný zadáním celého čísla spoje, jde pouze první číslo."*
- **Co máme dnes:** UI vstup pro filtr spojů akceptuje jen jednociferné číslo.
- **Gap:** UI bug v EDISONu (Studio JŘ), ne přímo v TD ani v Caché API. Zaznamenáno — předat dál.

### D5. Skrýt poznámku „x — zastávka je na znamení", pokud sloupec se značkou je hidden
- **Zákazník:** TRANSDEV (Vladimír Šimko), #7161
- **Citace:** *„aby sa negenerovala poznámka »x — zastávka je na znamenie«, ak je v zostave zakázané zobrazenie stĺpca »Pracovní 2« (zaisťuje zobrazovanie značky »x« pred názvami príslušných zastávok)"*
- **Co máme dnes:** Atribut zastávek se vždy přidá do legendy přes `AddAtribut` ve fázi 6 — bez ohledu na to, zda je sloupec s atributem v šabloně viditelný.
- **Co klient chce:** Pokud sloupec, který zobrazuje značku `x` (v TD ekvivalent: column visibility v `StopColumnsEditorDialog`), je vypnutý → vynechat odpovídající popis z legendy.
- **Gap:** Caché nezná stav viditelnosti v šabloně. **Cleaner řešení:** legenda atributů se nebude generovat v Caché vůbec a TD si sám sestaví legendu z viditelných sloupců StopList + DepartureGrid. Alternativně: TD posílá Caché při tisku seznam aktivních column klíčů.

### D6. Vypnutí auto-poznámky „všechny spoje s bezbariérovým vstupem"
- **Zákazník:** TRANSDEV (Vladimír Šimko), #7161
- **Citace:** *„Možnosť používateľsky nastaviť, aby sa automaticky negenerovala poznámka »všetky spoje s bezbariérovým vstupom do vozidla«."*
- **Co máme dnes:** `AddAtribut` v `ZastavkoveJrMhdAPI` zřejmě generuje shrnující text, když 100% spojů má daný atribut (např. bezbariérovost). Vypínání není.
- **Co klient chce:** User option v parametrech tisku — „negenerovat agregovanou poznámku" / per-atribut switch.
- **Gap:** Doplnit parametr do `GetParameters` (např. `SkipAggregatedAttributeNotes: true/false`) + checkbox v PrintDialog. V Caché v `AddAtribut` před zápisem do legendy ověřit.

### D7. Skrýt prázdné přípojové řádky
- **Zákazník:** KORID (Jaroslav Smištík), #11300
- **Citace:** *„zobrazujeme přípoje v příbojových řádcích, občas jsou uzly, kde je provoz celotýdenní, ale přípoje jsou jen v PD nebo naopak jen o víkendech, uvítali bychom, kdyby byla možnost prázdný přípojový řádek (v situacích kdy je PD a víkend na zvláštní tabulce/listu) netiskl a šetřil místo."*
- **Z M-line reply:** „Aktuálně není možné skrývat prázdné přípojové řádky. Pro tuto možnost bude nutné upravit tisk JŘ (...) jeho řešení pravděpodobně spojíme s novým tiskovým modulem pro JŘ" (2025-12-08, Jiří Bžoch).
- **Co máme dnes:** Přípojové řádky **nejsou** v TemplateDesigneru implementované — je to legacy fička.
- **Co klient chce:** Přípojové řádky obecně + filter na neprázdné. Pokud zastávka má v dané vývěsce (skupina dnů = PD nebo víkend) prázdný přípojový seznam, nevykreslit řádek.
- **Gap:**
  - Nová doménová struktura: `Pripoj` (data per uzel × skupina dnů) v `ZastavkoveJrMhdAPI.GetData`.
  - Nový element v TD (např. `ConnectionsListElement`) s `HideEmptyRows: true`.

---

## E. Návrhář sestav (TemplateDesigner UI)

*(Žádné aktivní položky — E1 přesunuto do §G Hotovo.)*

---

## F. Co je v TemplateDesigneru NAVÍC oproti legacy

Při komunikaci se zákazníky uvádět jako benefity nového řešení:

- **CellStyle kaskáda** — `column.Style → element.HeaderStyle/DataStyle → element defaults → CellDefaults`. Vše viditelně v dialozích sloupců s tri-state „dědit / přepsat" stylingem (placeholder zděděné hodnoty).
- **Per-column** styling napříč StopList / DepartureGrid / NotesLegend / TableElement.
- **WYSIWYG editor** s live preview a okamžitým náhledem mock-dat (per agenda).
- **Multi-data PDF export** — jeden tisk vygeneruje N PDF stránek (1 per zastávka × směr).
- **AI Vision import pipeline** — automatické rozpoznání struktury z existujícího PDF JŘ.
- **VisibilityExpression** na sekcích i elementech (pokrývá #8772).
- **Element groupy** + drag&drop z palety, snap, undo/redo, per-template editor preferences.

---

## G. Hotovo / vyřízeno

### B1. Per-line font v dvouřádkovém záhlaví skupin dnů (DepartureGrid)
- **Tiket:** #10660 (DPKV, Jan Slavík); zůstává **suspended**.
- **Datum:** 2026-05-12
- **Zjištění:** Data jsou **už rozdělená na 2 řádky** napříč všemi vrstvami (Caché `NadpisSMOPrvni`/`NadpisSMODruhy` → JSON `NADPIS`/`NADPIS2` → TD `SkupinaDnuData.Nadpis`/`Nadpis2`). TD je ale dříve před wrap fází konkatenovala do single string → ztracená hranice mezi nadpisy.
- **Implementace:**
  - Nový enum-record `HeaderLine(Text, IsSecondary)` zachová hranici Nadpis vs Nadpis2 až do render fáze.
  - Nová property `HeaderLine2Style: TextStyle?` na `DepartureGridElement` s cascade `HeaderLine2Style → HeaderStyle → ScheduleHeaderDefaults`.
  - Wrap logika: pokud `Nadpis2 != ""` → 2 distinct řádky bez word-wrap (každý si vejde sám); pokud prázdné → word-wrap pouze Nadpis (současné chování).
  - Height computation: per-line heights (Line 1 vs Line 2 font size), součet per group, max přes groupy.
  - Render: per-line style/font/paint selection podle `IsSecondary`; cumulative `runningY` pro různé výšky řádků; decorations (Underline/Strikethrough) per-line.
  - Pre-create `groupHeaderStyles2/Fonts2/TextPaints2` cache — vytváří se jen pokud `HeaderLine2Style != null` (žádná overhead při default).
  - UI: nový group **„Záhlaví — 2. řádek"** v properties panelu s 4 položkami (Velikost, Tučné, Kurzíva, Barva textu). Lazy-init helper `EnsureHeaderLine2Style()`.
- **Default chování zachované:** `HeaderLine2Style = null` → `hasLine2Override = false` → Line 2 použije stejný styl jako Line 1, žádná regrese pro existující šablony.
- **Pattern:** plně extensible přes TextStyle cascade — uživatel může v budoucnu doplnit per-column override Line 2, HAlign, padding atd. bez schema změny.

### A2. Konfigurovatelná pozice značek u zastávek (StopList)
- **Tiket:** #10660 (DPKV, Jan Slavík); zůstává **suspended** (řešíme i CBDP layout).
- **Datum:** 2026-05-12
- **Diagnóza:** Stávající TD renderoval značky (na znamení `×`, atributy, poznámky) vždy **za názvem** zastávky (CBDP layout). DPKV chce na znamení jako **prefix** před názvem (např. „× Globus") + chtějí, aby názvy zůstaly **zarovnané** i u zastávek bez značky.
- **Implementace:**
  - Nový enum `StopMarkersSide { AfterName, BeforeName }` + property `MarkersPosition` (default `AfterName` = CBDP layout).
  - Když `BeforeName`: render rozdělí sloupec zastávek na **2 sub-cells**:
    - **Marker sub-cell** (úzký, right-aligned) — značky
    - **Name sub-cell** (zbytek šířky, left-aligned) — pouze název
  - Šířka marker sub-cell je **auto-computed** podle nejširšího marker textu v datech přes `SKFont.MeasureText` + padding `MarkersSubCellPaddingMm = 1.0mm`. Pro běžné single-char markery (`×`, `D`) ~2.5mm.
  - Total width sloupce zastávek se nemění → žádný overflow.
  - Všechny 3 typy značek (na znamení + atributy + poznámky) se přepínají najednou jedním přepínačem. Per principles — minimum, per-marker per request later.
  - UI: `EnumPropertyDescriptor` „Pozice značek" v group „Zastávky" s labely „Za zastávkou" / „Před zastávkou".
- **Architektonický learning:** první návrh prepended markery do textu buňky (`„× Globus"`). To posouvalo názvy zastávek per řádek podle přítomnosti značky → nezarovnané. Druhá iterace s fixed 4mm sub-cell přidala vzdušnost ale byla zbytečně široká. Třetí (final) s auto-fit measure se přizpůsobí datům přesně. **Symetricky paralelní s A1 (DirectionArrowSide) — bounded enum, ne template variant.**

### A1. Konfigurovatelná pozice šipky směru jízdy (StopList)
- **Tiket:** #10660 (DPKV, Jan Slavík); zůstává **suspended** (řešíme i CBDP, kteří chtěli Vlevo).
- **Datum:** 2026-05-12
- **Diagnóza:** Stávající TD šipku kreslil VŽDY vpravo (DPKV preference). CBDP a podobní zákazníci, kteří dříve používali legacy se šipkou vlevo, neměli volbu.
- **Implementace:**
  - Nový enum `DirectionArrowSide { Right, Left }` + property `DirectionArrowPosition` (default `Right`, zachová zpětnou kompatibilitu).
  - Asymetrická pozice šipky (architektonicky čistá):
    - **Vpravo:** šipka UVNITŘ sloupce zastávek u pravé hrany. Funguje, protože text je left-aligned a k pravé hraně nezasahuje.
    - **Vlevo:** šipka MIMO sloupec na levé straně (1.5mm vlevo od levé hrany). Bracket vede zpět k hraně sloupce. Tento posun nezpůsobuje overflow šířky elementu.
  - `topY` posunut z `startY + 0.2 * rowH` (kousek pod horní hranou) na `startY + 0.5 * rowH` (uprostřed písmen aktuální zastávky).
  - Drobný padding `ArrowInsetFromEdgeMm = 0.6mm` mezi koncem bracketu a textem pro vizuální vzdušnost.
  - `EnumPropertyDescriptor` „Pozice šipky" v group „Směr jízdy".
- **Architektonický learning:** první návrh přidával gutter MIMO sloupec → způsobilo overflow Width elementu (clip ořezal arrow). Druhá iterace dala arrow UVNITŘ column → na Vlevo to kolidovalo s left-aligned textem. Třetí (final) iterace asymetricky — Vpravo inside, Vlevo outside — funguje bez overflow i bez kolize s textem. **Per-customer divergence řešena přes property, ne template variant** (per `feature-design-principles.md` §5 — 2 bounded hodnoty, ne kombinace settings).

### A3. CurrentStopBackgroundColor + cleanup master toggle
- **Tiket:** #10660 (DPKV, Jan Slavík); zůstává **suspended**.
- **Datum:** 2026-05-12
- **Výsledek:** ✅ `CurrentStopBackgroundColor` property + render path + properties panel descriptor („Pozadí aktuální") v group „Zvýraznění" — vše už existovalo. Customer use case (podbarvení místo podtržení) je plně podporovaný — uživatel vypne `CurrentStopUnderline` a nastaví barvu `CurrentStopBackgroundColor`.
- **Bonus cleanup:** Odstraněn master toggle `HighlightCurrentStop` (matoucí UX — vypadal jako sourozenec sub-options, ale choval se jako rodič). Highlight se teď aktivuje implicitně podle toho, jestli je některá z highlight properties nastavená. Šipka směru jízdy teď navíc vždy ukazuje na aktuální zastávku (dřív závisela na master toggle).
- **Minor regression:** Staré šablony s explicitně vypnutým `HighlightCurrentStop=false` po načtení dostanou defaultní Bold+Underline (protože System.Text.Json ignoruje smazaný field). Per principles („no backward compat shims") neřeším migrací.

### A4. Per-column header font ve StopList — již plně podporováno
- **Tiket:** #10660 (DPKV, Jan Slavík); zůstává **suspended**.
- **Datum ověření:** 2026-05-12
- **Výsledek:** ✅ Funkčně pokryto. Klient nenašel UI cestu — funkce existuje, jen je v `StopColumnsEditorDialog` (tab „Sloupce") přes per-column `CellStyleEditor`, ne v properties panelu.
- **Code path:** `StopListElement.ResolveHeaderStyle(col)` aplikuje `col.Style.Header.Resolve(elementResolved)` → per-column přebíjí element-level v 4-vrstvé kaskádě. `RenderHeader` vytváří `ColumnRenderer.CellSpec` s **per-column ResolvedTextStyle**, takže každý sloupec má vlastní FontSize.
- **Tri-state placeholder** ✅ — sloupec bez override zobrazí šedý italic placeholder hodnoty zděděné z elementu; „×" reset jen při explicit override.
- **UI test ověřeno:** dialog sloupců → per-column FontSize → různé velikosti záhlaví v náhledu.

### B2. Underline a Strikethrough v záhlaví skupin dnů (DepartureGrid)
- **Tiket:** #10660 (DPKV, Jan Slavík); zůstává **suspended**.
- **Datum:** 2026-05-12
- **Diagnóza:** V `DepartureGridElement.cs:626` se text headeru skupiny dnů renderuje přes `Canvas.DrawText` napřímo, **bez** volání `DrawTextDecorations`. Důsledek: `HeaderStyle.Underline = true` ani `Strikethrough = true` v UI nic neměnily — decorations se nevykreslovaly vůbec. Bold/Italic fungovaly, protože jdou přes SKFont.
- **Customer complaint vs. reality:** DPKV v legacy systému viděl podtržení s nestejnou tloušťkou. V novém TD by viděli žádné podtržení. Fix pokrývá oba symptomy.
- **Fix:** 1 řádek za `DrawText` — volání `hs.DrawTextDecorations(ctx.Canvas, pt.X, pt.Y, textW * ctx.MmToPx, hFont, hTextPaint)`. Tloušťka linky je automaticky konstantní napříč řádky, protože `hFont` je sdílený a `metrics.UnderlineThickness` je per-font deterministická.
- **Otestováno:** Manuálně v běžícím TD — Underline + Strikethrough nyní fungují na single-line i multi-line headeru.
- **Pozn.:** Stejný pattern (`DrawText` bez `DrawTextDecorations`) je na 5 dalších místech v souboru (minuty 780, anotace 803, hour header 899/901, designer placeholder 886/888). **Záměrně neopraveno** — nedotčeno reálným klientovým use casem; opravit až přijde konkrétní request.

### B3. Rozsah hodin přes půlnoc + sjednocené parametrové UI (DepartureGrid)
- **Tiket:** #10660 (DPKV, Jan Slavík); zůstává **suspended**.
- **Datum:** 2026-05-12
- **Diagnóza:** Stávající `RangeHoursWithWrap` clampovalo `to ≤ 23` a vůbec neumělo wrap přes půlnoc. Caché používá **extended hours konvenci** (data hour 24..47 = pokračování přes půlnoc následujícího provozního dne) — z fáze 4 `PromitnutSpoje` přicházejí kumulativní hodiny po chronometráži (např. spoj startuje 23:30, jede 90 min → poslední zastávka má `hh=25, mm=00`). Nová .NET pipeline ale očekávala 0..23 → noční odjezdy „mizely" v rozsahu 4→3.
- **Implementace:**
  - `TimeAxisMode` rozšířen o `DataBounded` (souvislá od první do poslední data hodiny — předtím chybělo). Plný den vědomě odebrán z UI (lze docílit přes Vlastní rozsah 0..23).
  - `RangeHoursExtended(from, to)` (přejmenováno z `RangeHoursWithWrap`): když `to < from`, vrací **extended hours** `[from..23, 24..to+24]`. Tj. 22→3 = `[22, 23, 24, 25, 26, 27]`.
  - `DrawHourDataCell` rendruje header label jako `H % 24` (data hour 27 → „3"). ByHour lookup zůstává raw — Caché dataset s extended hours se napáruje 1:1.
  - Non-Range filter (Compact/DataBounded/Full + HODINY_OD/DO): když to < from, používá `[from..to+24]` místo `OR` filteru → data hour 27 projde filtrem 22→3.
  - `ParseTimeAxisMode` — 3-vrstvý backward compat (popisné CZ + legacy CZ + EN názvy z předchozích iterací).
  - Caché `AddStandardJRParameters`: české popisné labely pro CASOVA_OSA („Jen hodiny s odjezdy" / „Souvislá od první do poslední" / „Vlastní rozsah"); HODINY_OD/DO mají nově `visibleWhen = "CASOVA_OSA=Vlastní rozsah"`.
- **Nová infrastruktura `VisibleWhen`:**
  - `PropertyDescriptor.VisibleWhen: Func<bool>?` (in-process, lazy) — `PropertiesPanel` filtruje skryté řádky, po commitu rebuilduje panel pokud existuje aspoň jeden VisibleWhen.
  - `TemplateParameter.VisibleWhen: string?` (JSON-friendly „NAME=VAL") — `ParameterControlVM.IsVisible` + `UpdateVisibility()`, parser v `CacheAgendaProvider`.
- **Sjednocení parametrového UI:**
  - Nové `Controls/ParameterTemplates.xaml` (shared ResourceDictionary se 4 DataTemplates) + `ParameterTemplateSelector` ve vlastním souboru.
  - `ParameterControlVM.Init(target, parameters, values, onChanged, commitFactory)` — sjednocuje wiring (clear → loop → auto-refresh visibility → initial check) mezi MainWindow preview lištou a PrintDialog.
- **Klientova „auto-fit zvětšení řádků" část:**
  - Nová property `AutoExpandToFillHeight: bool` + Caché parametr `ROZTAHNOUT_NA_VYSKU` (default `false`, opt-in).
  - Když `Height` fixní + obsah se vejde + flag = true → `dataGrow = (Height − headerH) / dataNaturalH` aplikuje se proporcionálně na: data řádky, minute fonty, anotace symbol/text fonty, padding minutových buněk, `scaledMinuteW`/`scaledOdjezdGap`, hour data buňka. Header (skupiny dnů + „Hod") zůstává na původním scale.
  - Render kód rozdělen na 2 scale factory: `scale` (header) a `dataScale = scale × dataGrow` (data) — symetrický k existujícímu shrink, ale selektivně.
  - Edge cases: `Height==null` → ignorováno; shrink + AutoExpand současně → shrink wins.
  - Override priority (jako u CASOVA_OSA): designer property → PrintDialog parametr override per print job.
- **Architektonický learning:** první iterace (Option A: `hodina # 24` v Caché) by ztratila informaci „toto je následující den" → konflikt pokud spoj jede ve 3:00 ráno **tento den i následující den**. Final fix (Option B) zachovává raw service hour v ByHour klíči, mod-24 jen na header label → distinct columns pro distinct service hours, žádné collisions.

### E1. Podmíněný tisk sekcí — VisibilityExpression
- **Tiket:** #8772 (TRANSDEV, Jakub Holík); zůstává **suspended** (interní rozhodnutí, ne uzavřen klientovi).
- **Datum ověření:** 2026-05-11
- **Výsledek verifikace:** ✅ Funkčně pokryto. `VisibilityExpression` na sekci i elementu se vyhodnocuje na 4 místech v `PageLayoutEngine` (elementy, sekce, page headers, page footers). `ExpressionEvaluator` podporuje `==`, `!=`, `>`, `<`, `>=`, `<=`, `&&`, `||`, `!`, numerické i string porovnání s auto-detekcí, závorky, literály (`"..."`, čísla), placeholdery `{POLE}` plus `{PAGE}`, `{TOTAL_PAGES}`. Live validace v UI přes `Validate()` v PropertyDescriptoru, AST cache, fail-open runtime.
- **Pokrytí klientova seznamu polí** ze schématu `ZastavkoveJrMhdAPI.GetSchema`:
  - ✅ `CISLO_LINKY`, `TYP_JR`
  - ⚠️ „typ linky" — částečně přes `LOKALNI_OZNACENI_LINKY`
  - ❌ `KOD_DOPRAVY` (CIS) chybí, ale v praxi nahraditelné `POPIS_DOPRAVCE`
- **Interní status note** (sepsáno 2026-05-11): kompaktní popis featury, syntaxe a možných rozšíření. Tiket zůstává suspended — na rozšíření čekáme až bude konkrétní potřeba od klienta.
- **Možná rozšíření** (až bude konkrétní request, ne preventivně): doplnění chybějících placeholderů, `IN` operátor, string funkce (`CONTAINS`, `STARTSWITH`), pattern matching, aritmetika, date typování v `Compare()`, `IS NULL` test.

---

## H. Status komunikace s klienty

M-line zákazníkům slíbil:

- **„Nový tiskový modul" / „Studio JŘ"** — release **polovina 2026**.
- Citace z `2024-10-04` na #7937, #7947, #7955, #7959 (TRANSDEV): *„S ohledem na množství podnětů týkajících se specifických možností nastavení a chování tisku jízdních řádů pracujeme na vývoji nového nástroje v našem systému (...) nový nástroj bude uvolněn přibližně v polovině příštího roku."*
- DPKV (#10660) z `2025-07-10` (Dita Chabičovská): *„momentálně se musíme soustředit především na obsahové aktualizace — jak zastávkových, tak i knižních jízdních řádů. Současně ještě nemáme uzavřenou část projektu týkající se plánování směn (...) vrátíme se k němu po dokončení klíčových etap implementace MHD ve Vašem dopravním podniku."*
- KORID (#11300) z `2025-12-08` (Jiří Bžoch): *„úpravu již »bohužel« nestihneme udělat v letošním roce. Problému se ale budeme věnovat příští rok, jeho řešení pravděpodobně spojíme s novým tiskovým modulem pro JŘ."*

**Z toho plyne:** roadmapa TemplateDesigneru je **viditelný závazek** vůči TRANSDEV, KORID a DPKV. Při uzavírání jednotlivých tiketů odkazovat na konkrétní featuru v TD + verzi releaseu.
