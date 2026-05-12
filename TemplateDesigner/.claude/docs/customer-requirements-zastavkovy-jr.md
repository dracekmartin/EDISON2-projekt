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

### A1. Pozice „osy směru" (svislé čáry) v sekci se zastávkami
- **Zákazník:** DPKV (Jan Slavík), #10660
- **Citace:** *„Červeně označenou čáru bychom měli radši na původním místě, tedy ne mezi minutami a názvem zastávky, ale až za názvem úplně vpravo v této sekci. Takto tato čára zbytečně zabírá moc místa na úkor názvu zastávky."*
- **Co máme dnes:** Šipka směru jízdy `┌─▼` ve StopList je vykreslená **mezi sloupcem minut a sloupcem názvu zastávky** (vlevo od názvu).
- **Co klient chce:** Tutéž svislou čáru přesunout **až za sloupec názvu zastávky** (vpravo, na pravém okraji sekce StopList). Cíl: ušetřit šířku pro název zastávky.
- **Gap:** Konfigurovatelná pozice šipky/osy směru ve StopList — `DirectionArrowPosition: BeforeStopName | AfterStopName` (default = aktuální BeforeStopName).

### A2. Atribut „na znamení" — prefix `x` před názvem zastávky
- **Zákazník:** DPKV (Jan Slavík), #10660
- **Citace:** *„označení zastávky na znamení (žluté označení) vypadá lépe, když je pouze malým písmenem x před názvem zastávky, tak jak to máme aktuálně"* (myšleno aktuálně v legacy verzi)
- **Co máme dnes:** Atributy zastávek se ve StopList tisknou jako samostatný sloupec nebo skupina značek (viz `StopColumnsEditorDialog`). Konkrétní prezentace pro `1003 = na znamení` je vizuálně odlišná než „malé `x` před názvem".
- **Co klient chce:** Atribut „na znamení" se má prezentovat **inline jako malé písmeno `x` bezprostředně před názvem zastávky** (ne v separátním sloupci, ne v jiném tvaru).
- **Gap:** Per-atribut volba prezentace — `AttributeRenderMode: SeparateColumn | InlinePrefix | InlineSuffix`. Pro `na znamení` default `InlinePrefix` se znakem `x`.

### A3. Highlight aktuální zastávky — podbarvení místo podtržení
- **Zákazník:** DPKV (Jan Slavík), #10660
- **Citace:** *„Aktuální zastávka (tedy na obrázku konkrétně Globus) je u nás podbarvená, což myslím vypadá lépe, než když je pouze podtržená, jelikož podtržená číslovka 0 nevypadá úplně dobře."*
- **Co máme dnes:** `StopListElement` zvýrazňuje aktuální zastávku přes `CurrentStopBold` / podtržení (`Underline`). Hodnota minut „0" má podtržení = nevypadá hezky.
- **Co klient chce:** Aktuální zastávka zvýrazněná **podbarvením pozadí** (bg color), ne podtržením textu.
- **Gap:** Doplnit `CurrentStopBgColor` na `StopListElement` (a v editoru per-row override) + vypnout default `Underline` pro aktuální zastávku ve prospěch bg colors.

### A4. Záhlaví „min." menší než záhlaví „Zastávky"
- **Zákazník:** DPKV (Jan Slavík), #10660
- **Citace:** *„nadpisu, kdy v naší verzi máme nápis min. menší, než nápis Zastávky, což také vypadá lépe a v návrháři jsem to nenašel"*
- **Co máme dnes:** Per-column header font v `StopColumnsEditorDialog` přes `CellStyleEditor` (CellStyle kaskáda). Teoreticky umožňuje různé velikosti, ale klient to v UI nenašel.
- **Co klient chce:** Sloupec „min." má **menší fontsize** v záhlaví než sloupec „Zastávky" — aby název zastávky vizuálně dominoval.
- **Gap:** Pravděpodobně už lze přes per-column `HeaderStyle.FontSize` — **úkol:** ověřit reálně v UI, případně dělat UX lepší (default `min.` má mít menší font?).

### A5. Odsazení/odrazení zastávky per směr, jen pro zastávkové JŘ
- **Zákazník:** TRANSDEV (Michal Podloučka), #7937
- **Citace:** *„potřebuji mít možnost odstadit/odrazit zastávku o určitý počet bodů, ale pouze pro zastávkové JŘ a pro jednotlivé směry (v jednom směru ano, v druhém ne)"*
- **Dodatek z #7937:** *„zda by šlo odražení nastavit tak, že se bude odrážet zastávka v nastavených Tč vývěsu, resp. nebude odrážet v určitých Tč vývěsu"* (tj. jen v určitých časových pásmech)
- **Co máme dnes:** Padding lze nastavit přes CellStyle, ale jen per-sloupec / per-element — ne per-řádek (per-zastávka) a ne per-směr.
- **Co klient chce:**
  - Posunout konkrétní zastávku (řádek) doprava o N bodů (visualní hierarchie).
  - Asymetricky: zastávka A je odsazena ve směru tam, ne ve směru zpět.
  - Volitelně podmíněno na rozsah časových pásem (Tč) vývěsu.
  - **Pouze pro zastávkové JŘ** — ne pro knižní.
- **Gap:** Per-row left padding ve StopListElement + parametr per-směr + Caché parametr „aktuální směr" / „Tč rozsah" pro vyhodnocení podmínky.

### A6. Skrýt zastávku, pokud z tištěné zastávky není obsluhována
- **Zákazník:** TRANSDEV (Michal Podloučka), #7959
- **Citace:** *„Mám linku, která má druhou zastávku pro školní spoj, a mohl bych pro ní vytisknout JŘ. Ovšem z první zastávky na tuto druhou zastávku žádný spoj nepojede. Potřeboval bych, aby zastávka nebyla v seznamu zastávek, pokud není aktuální."*
- **Co máme dnes:** `BuildZastavka` v `ZastavkoveJrMhdAPI.cls` generuje všechny zastávky trasy bez filtru na obsluhu ze zobrazované zastávky.
- **Co klient chce:** Při tisku JŘ ze zastávky `X` se v seznamu zobrazí pouze ty zastávky, kam **alespoň jeden spoj z `X` jede**. Zastávky bez obsluhy z `X` se vynechají.
- **Gap:** Filter v Caché `ZastavkoveJrMhdAPI` — fáze 6 `SestavitStranky` musí vyřadit zastávky, kde bitmapa obsluhy ze zobrazené zastávky je prázdná.

---

## B. DepartureGridElement — mřížka minut odjezdu

### B1. Per-line font v dvouřádkovém záhlaví skupiny dnů
- **Zákazník:** DPKV (Jan Slavík), #10660
- **Citace:** *„v sekci minuty odjezdu. Tam by bylo dobré, aby se dala nastavit různá velikost písma u nadpisu u obou řádků (modré označení), aktuálně máme větší první nadpis a menší ten druhý"*
- **Co máme dnes:** Per-column header font, sloučený řádek záhlaví, word-wrap záhlaví skupin. Není ale možnost dát **různý font pro 1. a 2. řádek** zalomeného textu v jednom buňce.
- **Co klient chce:** Záhlaví skupiny dnů zobrazené na dvou řádcích — např. **„Pracovní"** (větší font) a **„den"** (menší font).
- **Gap:** Header value parsing — pokud obsahuje `\n` nebo speciální token, různě stylovat řádky. Nebo: dva oddělené stringy (`HeaderLine1`, `HeaderLine2`) + dva `TextStyle`.

### B2. ✅ HOTOVO (2026-05-12) — viz §G

### B3. Rozsah hodin přes půlnoc (3 → 0)
- **Zákazník:** DPKV (Jan Slavík), #10660
- **Citace:** *„Když nastavím například 3-0 (což máme standartně), tak to nefunguje a když nastavím 3-24, tak se pouze zmenší tabulka o 3 řádky, což nevypadá dobře, ideální by bylo, aby šlo nastavit 3-0 a řádky by se automaticky zvětšily, aby to graficky vypadalo tak, jak má."*
- **Co máme dnes:** `DepartureGridElement` má rozsah hodin (HourFrom/HourTo). Při `3-24` zobrazí 21 řádků; rozsah `3-0` zřejmě neprochází (validace nebo prázdný výstup).
- **Co klient chce:**
  - Akceptovat rozsah přesahující půlnoc: **3:00–23:59 + 0:00–2:59 jako kontinuální** mřížku (typický noční provoz MHD).
  - + auto-fit: pokud má grid méně řádků než pevná výška elementu, **proporcionálně zvětšit výšku řádků** (ne nechat prázdné místo).
- **Gap:**
  - Logika wrap-around v `DepartureGridElement.ComputeLayout` — když `HourTo < HourFrom`, generovat `HourFrom..23` + `0..HourTo`.
  - Inverzní `ComputeFontScale` — dnes element zmenšuje fonty když nestačí výška; přidat opačné chování („expand-to-fit") když je naopak málo řádků.

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

### B2. Underline a Strikethrough v záhlaví skupin dnů (DepartureGrid)
- **Tiket:** #10660 (DPKV, Jan Slavík); zůstává **suspended**.
- **Datum:** 2026-05-12
- **Diagnóza:** V `DepartureGridElement.cs:626` se text headeru skupiny dnů renderuje přes `Canvas.DrawText` napřímo, **bez** volání `DrawTextDecorations`. Důsledek: `HeaderStyle.Underline = true` ani `Strikethrough = true` v UI nic neměnily — decorations se nevykreslovaly vůbec. Bold/Italic fungovaly, protože jdou přes SKFont.
- **Customer complaint vs. reality:** DPKV v legacy systému viděl podtržení s nestejnou tloušťkou. V novém TD by viděli žádné podtržení. Fix pokrývá oba symptomy.
- **Fix:** 1 řádek za `DrawText` — volání `hs.DrawTextDecorations(ctx.Canvas, pt.X, pt.Y, textW * ctx.MmToPx, hFont, hTextPaint)`. Tloušťka linky je automaticky konstantní napříč řádky, protože `hFont` je sdílený a `metrics.UnderlineThickness` je per-font deterministická.
- **Otestováno:** Manuálně v běžícím TD — Underline + Strikethrough nyní fungují na single-line i multi-line headeru.
- **Pozn.:** Stejný pattern (`DrawText` bez `DrawTextDecorations`) je na 5 dalších místech v souboru (minuty 780, anotace 803, hour header 899/901, designer placeholder 886/888). **Záměrně neopraveno** — nedotčeno reálným klientovým use casem; opravit až přijde konkrétní request.

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
