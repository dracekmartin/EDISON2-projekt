# Zastávkový JŘ — požadavky zákazníků z helpdesku

Snapshot z Podpory M-line k **2026-05-11**, **konsolidováno 2026-07-10** (sekce 0 = stav pro uzavírání
tiketů + dispatch). Slouží jako vstup pro plánování dalšího rozvoje TemplateDesigneru a Caché agend
(`ZastavkoveJrMhd` / `ZastavkoveJrAd`). Aktualizuj při dalším procházení tiketů.

> **Pozn. 2026-07-10:** od snapshotu přibyla **AD agenda** (`ZastavkoveJrAd`, TRANSDEV jede primárně AD)
> a většina helpdesk featur je vyřešená právě pro AD. Dnešní kontraktní práce (A-AD: skaláry `SMER` +
> `POPIS_SMER`=konečná + `NASTUPISTE`) a B2 (overflow konzistence) posouvají některé TR body — viz sekce 0.

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
| 9036  | ?                                    | suspended | **TRANSDEV** (Vladimír Šimko) | Nočné spoje — časová osa cez polnoc (22-5h) → **pokryto B3** |

Otevřené tikety #12434 (BBSK — „Export Tisk JŘ") a #12260 (BUSLINE — „úprava šablony") jsou obecné tracker / dotazy, ne konkrétní featury — neuvedeny.

Mimo scope zastávkového JŘ:
- **#7487** (KORID — NZ + týdenní platnost 4 na řádek) — týká se **knižních/linkových JŘ**, ne zastávkových. Vyřadit z této roadmap.

---

## 0. Konsolidovaný stav — pro uzavírání tiketů a dispatch (2026-07-10)

> Jedno místo: co je hotové (→ otestovat → zavřít na helpdesku), co malé/velké zbývá. Detaily v sekcích níže.
> **Legenda:** ✅ hotovo (AD) · 🧪 hotovo, ověřit u klienta · 🟢 malé zbývá · 🟠 velké zbývá (analýza/data) ·
> ⬜ MHD-port (AD hotové, MHD ne) · ➖ mimo TD / mimo scope.
> ⚠️ Featury jsou vyřešené primárně pro **AD** agendu (TRANSDEV = AD); MHD-port jen na reálnou potřebu (DPCB/DPKV).

### 0.1 Tikety (helpdesk)

| Ticket | Zákazník | Feature | Stav | Otestovat před zavřením | Detail |
|---|---|---|---|---|---|
| #10660 | DPKV | A1–A4 StopList (šipka/značky/podbarvení/per-col font) + B1/B2 záhlaví DepartureGrid + C1/C2 legenda | ✅ | Render DPKV šablony: pozice šipky/značek, podbarvení aktuální, per-column font, 2-řádkové záhlaví (underline), barvy značek | §G |
| #8772 | TRANSDEV | Podmíněný tisk sekcí | ✅ | `VisibilityExpression` na sekci/elementu (např. `{PAGE}>1`) | §E1 |
| #7947 | TRANSDEV | „jede až do XY" u poslední zastávky | ✅ AD · ⬜ MHD | Linka se spojem za základní trasu → legenda „jede až do zast. X" | §D1 |
| #7959 | TRANSDEV | Skrýt neobsluhovanou zastávku | ✅ AD | Školní linka: neobsluhovaná zast. se v seznamu netiskne | §A6 |
| #7955 | TRANSDEV | Vlastní komentář přebije auto (D2) + NZ bug (D3) | ✅ AD | `SCHOVAT_V_ROZSAHU`=Automatické → auto-rozdíl zmizí, ruční zůstane; NZ nemizí | §D2/D3 |
| #7955 | TRANSDEV | Výběr spoje celým číslem (D4) | ➖ mimo TD | UI bug ve Studiu JŘ (EDISON) — předat dál, nezavírat na TD | §D4 |
| #7161 | TRANSDEV | Skrýt na-znamení legendu (D5) + vypnout agregát bezbariér (D6) | ✅ AD · ⬜ MHD | D5: odškrtnout „Na znamení" → značky i řádek legendy zmizí (0320144); D6: `SCHOVAT_AGREGAT_BEZBARIER` (0365004) | §D5/D6 |
| #9036 | TRANSDEV | Časová osa přes půlnoc (22→5) | 🧪 ověřit u klienta | `TimeAxisMode=Range`, HODINY_OD=22/DO=5 → wrap přes půlnoc | §B3 |
| #7937 | TRANSDEV | Odsazení zastávky per směr / per Tč | 🟢 base AD · 🟠 rozšíření | Základ (odsazení z `^JRL` dta7) hotový; per-směr + per-Tč pásmo zbývá (změna zdroje) | §A5 |
| #8828 | TRANSDEV | Per-sloupek zastávky → samostatná vývěska | 🟠 | Datový průzkum (kde je sloupek v `ALVA.MHD.*`) + business rozhodnutí; ~0.5–1 den Caché | §B4 |
| #11300 | KORID | Skrýt prázdné přípojové řádky | ➖ linkové JŘ | Přípoje nejsou koncept zastávkového JŘ (řešit s linkovými) | §D7 |

**Poznámka k uzavírání:** #10660/#11300/#7xxx jsou u klientů `suspended` (M-line slíbil „nový tiskový modul"
polovina 2026 — viz §H). Zavírat po ověření dané featury v releasu TD + odkazem na konkrétní funkci.

### 0.2 Nové požadavky bez samostatného tiketu (před uzavřením dohledat/založit ticket)

**TRANSDEV konsolidovaná sada (2026-07-03) — §TR:**

| # | Co | Vel. | Stav (2026-07-10) |
|---|---|---|---|
| TR1 | Časová osa přes půlnoc | — | 🧪 = #9036 (jen ověřit u klienta) |
| TR2 | Na-znamení „všechny" souhrn (místo značek u každé) | S–M | 🟢 pattern = bezbariér `allBBP` agregát; relates #7161 |
| TR3 | Nástupiště jako sloupec vedle názvu zastávky | S | 🟢 data `Oznacnik` existují · ⚠️ **≠** dnešní skalár `NASTUPISTE` (ten = jen tištěná zast.) |
| TR4 | Explicitní „fixed-then-shrink" výška řádku StopList | S–M | 🟢 auto-scale částečně; relates B2 (2026-07-10, DepartureGrid) |
| TR5 | Definice cíle referencovatelná v šabloně | M–L | 🟠 · ⚠️ **(min) „cíl per směr" HOTOVO** dnešním `POPIS_SMER`=konečná (A-AD); zbývá střední (per zast.×směr) + ideál (DB cílů) |
| TR6 | Barva linky (bg/text ve vlastnostech linky → volba v šabloně) | M | 🟠 nový datový model |
| TR7 | Výběr sloupců variant jízdních dob (stabilní číslování, per-písmeno) | M | 🟠 relates L1 multi-varianta (`VARIANTY_TRAS`) |
| TR8 | „<1" místo „0" u nulové jízdní doby (konfigurovatelné) | S | 🟢 |
| TR9 | Obrácená logika poznámkování (rozsah TČ v 1. tabulce) | M | 🟠 Caché-input (Studio JŘ) |
| TR10 | Skrýt spoje o So/Ne/svátcích | S–M | 🟠 Caché filtr |

**Z prezentací zákazníkům — §P:**

| # | Co | Vel. | Stav |
|---|---|---|---|
| P1 | Poznámky ručně do patičky | S | 🟢 nejspíš už jde přes `TextElement` v Footer; příp. dedikované pole |
| P2 | Sloučit více linek stejného trojčíslí (i více dopravců) | L | 🟠 agenda + instance picker + agregace dopravců |
| P3 | Imposice 2×A5 → A4 | M–L | 🟠 = aktivní krok #1 roadmapy (imposiční vrstva) |

**Dispatch kandidáti (malé, TD/AD, bez velkého průzkumu):** TR8 · TR2 · TR3 · TR4 · P1.
Před dispatchem u každého rychle ověřit aktuální stav v kódu (nedělat už hotové).

---

## A. StopListElement — seznam zastávek vlevo

### A1. ✅ HOTOVO (2026-05-12) — viz §G

### A2. ✅ HOTOVO (2026-05-12) — viz §G

### A3. ✅ HOTOVO (2026-05-12) — viz §G

### A4. ✅ HOTOVO (2026-05-12) — viz §G

### A5. 🟡 Základ hotový pro AD (2026-07-09) — per-směr / per-Tč otevřené

- **Zákazník:** TRANSDEV (Michal Podloučka), #7937 — TRANSDEV jede primárně **AD** (viz A6), takže cílová agenda = `ZastavkoveJrAd`.
- **Citace:** *„potřebuji mít možnost odstadit/odrazit zastávku o určitý počet bodů, ale pouze pro zastávkové JŘ a pro jednotlivé směry (v jednom směru ano, v druhém ne)"*
- **Dodatek:** *„zda by šlo odražení nastavit tak, že se bude odrážet zastávka v nastavených Tč vývěsu"* (per časové pásmo)
- **✅ Hotovo — základní odsazení (AD):** datový flow dotažen. `ZastavkoveJrAd` čte odsazení z
  `^JRL(sku,lin,"J",poradi)` **5. `#`-segment** (legacy `mkPLinWin` „odrazeni nazvu zastavky" = `gTrsLin dta 7`,
  ověřeno `JRapiLin.mac`/`JRapiLinPrtWin.mac`) → `BuildZastavka ODSAZENI` → element `z.Odsazeni` → N mezer.
  Živě ověřen render. Behavior-preserving (gold netknuté). Commit `62852f2`.
  - ✅ TD model `ZastavkaData.Odsazeni` + `ShowStopIndentation` toggle + render (prefix spaces).
- **⚠️ Otevřené (rozšíření nad legacy — potřebují datový model + UI rozhodnutí):**
  - **Per-směr** („v jednom směru ano, v druhém ne") — `^JRL` dta7 je **jedna hodnota per bod trasy**,
    sdílená pro tam i zpět (i legacy má single-per-point). Odsazení se teď projeví v **obou** směrech.
    Per-směr = nutná změna zdroje (per-směr pole) + editor.
  - **Per-Tč pásmo vývěsu** — neřešeno.
  - **MHD strana** — `ZastavkoveJrMhdAPI` má pořád hardcoded `s odsazeni = 0` („legacy: dta 10" v `ALVA.MHD.ZST`).
    Relevantní jen pro self-contained MHD (DPCB/DPKV), ne TRANSDEV.
- **Render caveat:** mezery jsou proporcionální (počet mezer = počet znaků); kdyby vizuál nesedel s legacy, přepočet na pevnou šířku.

### A6. ✅ Hotovo (2026-06-20) — ověřeno na úrovni kódu pro AD; klient potvrdí na svých datech

- **Zákazník:** TRANSDEV (Michal Podloučka), #7959
- **Upřesnění agendy:** TRANSDEV jezdí primárně **linkovou (AD)** + lokální MHD; **self-contained MHD
  modul používají jen DPCB/DPKV**. Tiket se proto týká **AD agendy** (`ZastavkoveJrAd`), ne MHD modulu.
- **Citace:** *„Mám linku, která má druhou zastávku pro školní spoj... Ovšem z první zastávky na tuto druhou zastávku žádný spoj nepojede. Potřeboval bych, aby zastávka nebyla v seznamu zastávek, pokud není aktuální."*
- **Stav v AD — obě chování implementovaná a aktivní:**
  - **(a) StopList filtr** — neobsluhovaná zastávka se nepřidá do seznamu: `ZastavkoveJrAd:296`
    `i '$d(servedPoradi(poradi)) continue` (`ObsluhovanePoradi`).
  - **(b) Stránka se negeneruje** — zastávka bez odjezdů: `:333` `odjezdy.%Size()=0 q ""` + `:278` terminus-skip.
- **⚠️ Známá nuance k případnému doladění** (jen pokud klient nahlásí): AD filtr (a) je
  **směrově-globální** (`servedPoradi` = staví tam aspoň 1 spoj směru), kdežto MHD je **from-current**
  (`ObsluhaBitmap` = obsluha z aktuální zastávky). Pro přesný školní scénář (zastávka dosažitelná jen
  spojem, který z tištěné zastávky nevyjíždí) by AD mohl zastávku ponechat. Pokud to vadí → přepnout AD
  na from-current sémantiku. Zatím necháno (těžko se hledá testovací linka; klient ověří v praxi).
- **Opt-out** (kdyby chtěl vidět vše bez filtru) → param `TISK_VSECH_ZASTAVEK` (paralelní k `TISK_PROJETYCH_ZAST`), zatím YAGNI.

---

## B. DepartureGridElement — mřížka minut odjezdu

### B1. ✅ HOTOVO (2026-05-12) — viz §G

### B2. ✅ HOTOVO (2026-05-12) — viz §G

### B3. ✅ HOTOVO (2026-05-12) — viz §G (auto-fit zvětšení odloženo)

### B4. Per-sloupek zastávky — samostatná vývěska ⏸️ Odloženo (2026-05-12)
- **Zákazník:** TRANSDEV (Jakub Holík přes Michala Podloučku), #8828
- **Konkrétní příklad:** Linka 876420, zastávka „Těrlicko,,obecní úřad" má 2 sloupky. Většina spojů obsluhuje sloupek 2 (oba směry), ale vybrané spoje obsluhují sloupek 1. Druhý příklad: linka 863383, zastávka „Šenov,,Škrbeň" má dva sloupky v každém směru — koordinátor (KORID) generuje vývěsky **per sloupek** odděleně.
- **Citace klienta po prvním M-line návrhu** (rozdělit zastávku na 2 v trase linky): *„Vámi navržené nás s kolegy samozřejmě napadlo, ale asi to vždy není vhodné. (...) V JŘ je pouze jedna zastávka Šenov,,Škrbeň (...) Proto dále prosím o zvážení jiného způsobu řešení, než popisujete."*
- **Co máme dnes:** Multi-data PDF export generuje 1 PDF stránku per `(zastávka × směr)`. Nezná pojem „sloupek" — atribut sloupku není ani v `ZastavkoveJrMhdAPI.GetData`.
- **Co klient chce:** Pro zastávku s více sloupky vygenerovat **samostatnou vývěsku per sloupek** — jen spoje obsluhující daný sloupek se vytisknou.
- **Gap:**
  - Caché: rozšířit `BuildOdjezd` (nebo `PromitnutSpoje` fáze 4) o filtr per sloupek — předtím rozšířit working globální klíč o `sloupekID` subscript: `^||TiskMHD("odjezdy",SmerID,BodID,TypProvozuID,sloupekID,hh,mm,SpojID)`.
  - `GetData` API: pro zastávku s více sloupky iterovat `(Smer × Bod × Sloupek)` → `OUT[N]` per sloupek. Skalární info per stránka rozšířit o `SLOUPEK_CISLO` / `SLOUPEK_POPIS`.
  - TD: **žádná změna kódu** — multi-data export už zvládá libovolný počet OUT stránek. Volitelně placeholder `{SLOUPEK_CISLO}` v šabloně.
  - Pro: zachovat single zastávku v trase linky (klient výslovně odmítá duplikaci v trase).
- **Důvod odložení:** Téměř 100 % práce je v Caché a vyžaduje:
  - **Datový průzkum** — zjistit, kde v `ALVA.MHD.*` globálech leží info o sloupcích (atribut spoje? per-bod v trase `("ModelTrasaBodDetail",BodID,"Val","SloupekTam")`? jinde?). Není dokumentováno v `cache-database.md`.
  - **Business decision s kolegy** — identifikace sloupku (číslo 1/2/3, písmeno A/B, nebo popis?), default chování (opt-in přes parametr `PER_SLOUPEK` vs vždy zapnuté), chování pro zastávky s jedním sloupkem.
  - Po vyjasnění obojího je vlastní implementace odhad **~půl dne až den** (rozšíření working globálu + iterace v `SestavitStranky` + nový parametr v `AddStandardJRParameters`).

---

## C. NotesLegendElement — legenda poznámek pod mřížkou

### C1. ✅ HOTOVO (2026-05-12) — viz §G (DB-driven barvy z PoznamkaSpoj)

### C2. ✅ HOTOVO (2026-05-12) — viz §G (verifikace, žádný kód)

### ~~C3. NZ + týdenní platnost — 4 na řádek (úspora místa)~~
**Mimo scope zastávkového JŘ** (přesunuto, 2026-05-12). Tiket #7487 (KORID) se týká **knižních/linkových** JŘ, ne zastávkových. Patří do roadmap knižních JŘ až bude existovat.

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
- **✅ VYŘEŠENO PRO AD** (2026-06-21) — `ZastavkoveJrAd.RozdilyTras` větev prodloužení za vzor nově
  vypisuje **„jede až do zast. <konečná spoje>"** místo legacy matoucího „pokračuje přes <mezizastávky>"
  (znění A dle dohody — klient chce vědět KAM dojede). Záměrná odchylka od legacy (legacy „pokračuje"
  bylo špatné — to je důvod ticketu, proto bez gold oracle; kód analogický k ověřené větvi „jede jen do").
  **Pozn.:** prodloužení za základní trasu je v datech **vzácné** (J-věta #10 = obvykle nejdelší trasa);
  ověřeno proti CB_CSADAD 0345061/0365001 že běžné rozdíly (neobsluhuje/jede jen do) fungují 1:1 a změna
  je nedotkla. „jede až do" se projeví na lince, kde spoj reálně jede za vzor.
- **⬜ Zbývá pro MHD agendu** (`ZastavkoveJrMhd`) — stejná úprava neportována (až bude potřeba).

### D2. Vlastní komentář spoje musí přebít auto-poznámku (ne paralelně tisknout)
- **Zákazník:** TRANSDEV (Michal Podloučka), #7955
- **Příklad:** Linka 402, spoj 20 jede jen v části trasy. Auto generuje „spoj jede pouze do zastávky XY". Klient chce vlastní text „ze zastávky XY pokračuje jako linka X do zastávky YX".
- **Pokus klienta:** zapnutí „Komentáře" → funguje, doplní písmeno + text. Současně ale nezmizí auto-poznámka.
- **Pokus o vypnutí přes „Spoje, nepoznámkovat":** chová se opačně — nechá auto-poznámku, schová vlastní komentář.
- **Citace:** *„Měl jsem za to, že když mám zapnuté automatické poznámkování, u vybraných spojů si v Komentářích zapnu vlastní poznámkování, a zadám do Spoje, nepoznámkovat stejné spoje a rozpětí Tč, budu mít u těchto spojů pouze svůj komentář. Toto mi ale teď nefunguje a je používána automatika a potlačen ruční komentář."*
- **Co máme dnes:** Auto-poznámka (rozdíly tras) a vlastní komentář se generují nezávisle ve fázi 6. Funkce „nepoznámkovat" má aktuálně sémantiku „vypnout vše kromě auto-rozdílů".
- **Co klient chce:** Pokud má spoj **vlastní komentář**, auto-poznámka rozdílů se pro tento spoj **NEgeneruje**. (Plus překlopit sémantiku „nepoznámkovat" — viz M-line interní odpověď: aktuální chování je dokumentované, ale neintuitivní.)
- **✅ VYŘEŠENO PRO AD agendu** (2026-06-20, commit `0b5838d`) — nový parametr sestavy
  **`SCHOVAT_V_ROZSAHU`** (Ruční komentáře [default] / Automatické (rozdíly tras) / Obojí). V rozsahu
  „Spoje, nepoznámkovat" volba „Automatické" schová auto-rozdíl trasy a nechá ruční komentář = přesně
  klientův požadavek. Řešeno **modelem nadmnožina + render-time TD filtr** (`NoteVisibilityFilter`),
  ne auto-suppress v Caché — legacy sémantika „nepoznámkovat" zachovaná (default), flip je opt-in volba.
  Viz `ad-jr-nastaveni-linky-analyza.md §5` + `feature-design-principles.md §5.2`.
- **Pozn. k MHD agendě:** tyto ruční poznámky jsou **AD-only** — `^JRL` konstrukty (`SeT_SpeC`/
  `MHDSPNPZN`/`SeT_MhD`). MHD agenda (`ZastavkoveJrMhd`) čte jiný zdroj (`^ALVA.MHD.*`) a tyto pole
  **nemá** (ověřeno 2026-06-20). Není co portovat, dokud se nepotvrdí, že MHD modul má vlastní
  ekvivalentní úložiště (dle kolegy pravděpodobně ne — nastavuje se jen v AD).

### D3. Bug — „Spoje, nepoznámkovat" maže i inverzní znaménko (NZ)
- **Zákazník:** TRANSDEV (Michal Podloučka), #7955
- **Citace:** *„obecně Spoje, nepoznámkovat ruší i označení inverzním znaménkem, což nevím, zda je záměr."*
- **Co máme dnes:** Funkce „nepoznámkovat" patrně omylem maže i `spojNZ` přidělení.
- **Gap:** Bug — NZ je v jiném datovém toku než ostatní poznámky, „nepoznámkovat" se ho dotýkat nemá. Opravit v 6. fázi `ZastavkoveJrMhdAPI`.
- **✅ V AD agendě bug NEvzniká** (2026-06-20) — `NepoznamkovatSpoj` + filtr se dotýká jen kategorie
  `rucni-komentar` / `rozdil-trasy` (dle volby); `omezeni`/`bezbarier` (kam patří NZ-style značky)
  filtr nikdy neschovává. Zůstává relevantní jen pro MHD agendu (legacy datový tok).

### D4. Bug — výběr spoje jen prvním číslem
- **Zákazník:** TRANSDEV (Michal Podloučka), #7955
- **Citace:** *„Výběr mezi spoji není možný zadáním celého čísla spoje, jde pouze první číslo."*
- **Co máme dnes:** UI vstup pro filtr spojů akceptuje jen jednociferné číslo.
- **Gap:** UI bug v EDISONu (Studio JŘ), ne přímo v TD ani v Caché API. Zaznamenáno — předat dál.

### D5. Skrýt poznámku „x — zastávka je na znamení", pokud sloupec se značkou je hidden
- **Zákazník:** TRANSDEV (Vladimír Šimko), #7161
- **Citace:** *„aby sa negenerovala poznámka »x — zastávka je na znamenie«, ak je v zostave zakázané zobrazenie stĺpca »Pracovní 2« (zaisťuje zobrazovanie značky »x« pred názvami príslušných zastávok)"*
- **Co máme dnes:** Atribut zastávek se vždy přidá do legendy přes `AddAtribut` ve fázi 6 — bez ohledu na to, zda je sloupec s atributem v šabloně viditelný.
- **Co klient chce:** Pokud sloupec, který zobrazuje značku `x` (v TD ekvivalent: `ShowSignalStopIndicator` na StopListu), je vypnutý → vynechat odpovídající popis z legendy.
- **✅ VYŘEŠENO PRO AD** (2026-06-21) — `NoteVisibilityFilter.ShouldHideNaZnameniLegend(template)`:
  když šablona má StopList, ale **žádný** nemá zapnuté `ShowSignalStopIndicator` („Na znamení"),
  filtr skryje řádky legendy kategorie `atribut-zastavky` („× na znamení"). Legenda automaticky
  následuje viditelnost značky — žádné nové ovládání (uživatel jen odškrtne „Na znamení" na StopListu).
  Žádná Caché změna (legenda už je tagovaná). **Test:** CB_GWBUS **0320144** (má „x — zastávka pouze
  na znamení") — odškrtnout „Na znamení" na StopListu → značky „×" i řádek legendy zmizí.
- **⬜ MHD agenda** neportováno (až bude potřeba).

### D6. Vypnutí auto-poznámky „všechny spoje s bezbariérovým vstupem"
- **Zákazník:** TRANSDEV (Vladimír Šimko), #7161
- **Citace:** *„Možnosť používateľsky nastaviť, aby sa automaticky negenerovala poznámka »všetky spoje s bezbariérovým vstupom do vozidla«."*
- **Co máme dnes:** `AddAtribut` v `ZastavkoveJrMhdAPI` zřejmě generuje shrnující text, když 100% spojů má daný atribut (např. bezbariérovost). Vypínání není.
- **Co klient chce:** User option v parametrech tisku — „negenerovat agregovanou poznámku" / per-atribut switch.
- **✅ VYŘEŠENO PRO AD** (2026-06-21) — parametr sestavy **`SCHOVAT_AGREGAT_BEZBARIER`** (bool). Agregát
  „všechny spoje mají bezbariérově přístupné vozidlo" (vzniká při `allBBP` = 100 % spojů bezbariér) je
  v legendě tagován kategorií `bezbarier-agregat`; TD `NoteVisibilityFilter` ho při zapnutém parametru
  skryje. Per-spoj „spoj má bezbariérově…" zůstává. Filtr-model jako #7955.
  **✅ Ověřeno na CB_CSADAD 0365004** (2026-06-21) — má agregát „všechny spoje mají bezbariérově přístupné
  vozidlo"; zapnutí parametru řádek korektně skryje, per-spoj bezbariér (0365001/0365005) zůstává.

### D7. Skrýt prázdné přípojové řádky ➖ přeřazeno k linkovým JŘ
> **Stav (2026-06-30):** přípoje/návaznosti **nejsou koncept zastávkového JŘ** — patří k linkovým
> JŘ (konzistentní s AD-N14). Řeší se až s agendou linkových JŘ, ne v zastávkovém. Ponecháno zde
> jen jako evidence tiketu #11300.
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

## P. Nové požadavky z prezentací zákazníkům

> Zaznamenáno z živých prezentací/seminářů (ne z helpdesk tiketů). Neanalyzováno — evidence, že se na to zákazníci ptali. Před implementací založit/dohledat ticket a ověřit přesné zadání.

### P1. Poznámky umět zadat ručně do patičky ⬜ Nové (2026-06-24)

- **Zdroj:** prezentace zákazníkům 2026-06-24.
- **Požadavek:** možnost ručně vepsat volné poznámky do patičky JŘ (ne jen auto-generovaná legenda).
- **Stav infrastruktury (k ověření):** TD už umí přidat `TextElement` s libovolným textem do libovolné sekce vč. patičky (`SectionRole=Footer`). Otázka, jestli zákazníkovi stačí volný TextElement, nebo chce **dedikované pole „Poznámky"** (např. vstup v tiskovém dialogu / parametr sestavy, který se promítne do patičky).
- **Odhad:** malé, pokud stačí volný text (už funguje); střední, pokud chce parametr-driven poznámku do patičky (nové pole v EDInputForm + propsání do DataContextu).

### P2. Sloučení více linek se stejným posledním trojčíslím (i různá Tč / více dopravců) ⬜ Nové (2026-06-24)

- **Zdroj:** prezentace zákazníkům 2026-06-24.
- **Požadavek:** *„Dokáže šablona sloučit více linek se stejným posledním trojčíslím — a to i v případě rozdílných tarifních čísel (více dopravců)?"* → jeden výstup zastávkového JŘ složený z více linek.
- **Stav:** **nové, datově náročné.** Dnes platí 1 linka = 1 sada stránek (zastávka × směr). Sloučení více linek do jednoho výstupu se dotýká **úrovně agendy / výběru instancí** (`GetData`, InstancePicker), ne jen vizuálu:
  - identifikace „stejné poslední trojčíslí" napříč linkami,
  - sloučení spojů z více linek do jedné mřížky odjezdů (různá Tč → mapování zastávek),
  - **více dopravců** v jednom výstupu (pole `POPIS_DOPRAVCE` dnes jedna hodnota — nutná agregace).
- **Odhad:** větší feature (agenda + instance picker + handling dopravců). Vyžaduje analýzu legacy chování (jak to řeší starý nástroj) + UX rozhodnutí o výběru linek.

### P3. Logická imposice — 2× A5 oboustranně na jednu A4 ⬜ Nové (2026-06-24)

- **Zdroj:** prezentace zákazníkům 2026-06-24.
- **Požadavek:** logicky vytisknout dvě A5 (oboustranně) na jednu A4 — tj. N-up imposice + duplex/brožura.
- **Stav dnes:** `RenderMultiDataToPdf` generuje PDF = sekvence stránek v rozměru `PageSize` šablony (1 logická stránka = 1 PDF stránka, kreslená od `(0,0)`). **Žádná imposice ani duplex** — dnes to řeší výhradně ovladač tiskárny / Windows dialog („2 stránky na list" + „oboustranně" + „brožura"). PDF duplex nekóduje (přední/zadní = nastavení tiskárny).
- **Háček pro implementaci:** `RenderPage(canvas, ps, ppm, xOffset, yOffset, page)` už bere offset → 2 logické A5 lze umístit na 1 fyzický A4 canvas.
- **Možnosti:** (A) nechat na ovladači (0 práce, nekonzistentní); (B) imposice v našem PDF; (C) „sheet layout" vrstva nad PageLayoutEngine (logické stránky → fyzické archy) — nejčistší, sedí na vrstvenou architekturu.
- **Odhad:** netriviální, hlavně pořadí stran pro brožuru (skládání). Samostatná imposiční vrstva, mimo per-element práci.

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
- **Tikety:** #10660 (DPKV, Jan Slavík) + **#9036 (TRANSDEV, Vladimír Šimko — MHD Trenčín, nočné spoje 22-5h)**; oba zůstávají **suspended**.
- **Datum:** 2026-05-12
- **Tiket #9036 — explicitní pokrytí:** TRANSDEV potřeboval pro MHD Trenčín (provoz cca 22-5h) časovou osu „cez polnoc" (22, 23, 0, 1, 2, 3, 4, 5). Naše implementace **přesně toto** umí — `TimeAxisMode = Range`, `HODINY_OD = 22`, `HODINY_DO = 5` → wrap přes půlnoc s extended hours (data hodina 24-29 = pokračování). Spoje se přiřazují do sloupců podle `TypProvozuID` (skupiny dnů) beze změny — což TRANSDEV výslovně přijal („v noci z piatka na sobotu spoje pred polnocou budú obsiahnuté v stĺpci pracovný deň a spoje po polnoci v stĺpci sobota, nedeľa").
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

### C1. Barevné odlišení značky vs. text poznámky (DB-driven)
- **Tiket:** #10660 (DPKV, Jan Slavík); zůstává **suspended**.
- **Datum:** 2026-05-12
- **Citace:** *„Kdyby bylo možné barevně odlišit značku (písmeno) poznámky, zpřehlednilo by to celý zastávkový jízdní řád."*
- **Architektonické rozhodnutí — DB-driven místo designer SymbolStyle:** Původní plán byl přidat designer-level `SymbolStyle` property (statická barva v šabloně). Při průzkumu DB se ale ukázalo, že DPKV **už má barvy uložené** per-poznámka v `^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",ID,"TiskJRMHDVozoveBarvaZnakuRGB")` (a `TiskJRMHDVozoveBarvaPozadiRGB`) — historicky pro vozové JŘ. Přeneseme stejnou barvu i do zastávkového JŘ — žádné zdvojené nastavování per-šablona.
- **Implementace:**
  - **Caché:** `IntToHex2` + `RgbToHex` helpery (převod „R;G;B" → „#FFRRGGBB" pro SkiaSharp). `BuildPoznamka` rozšířen o `foreColor`/`backColor` parametry → JSON `FORE_COLOR`/`BACK_COLOR`. `AddAnotace` rozšířen analogicky (pro konzistenci legenda ↔ anotace v odjezdech). Color lookup z `PoznamkaSpoj` ENUMS na 4 místech (poznámky trasy, speciální znaky spoje, textové poznámky spoje — vždy stejný pattern).
  - **.NET:** `PoznamkaData` + `AnotaceData` rozšířeny o `ForeColor`/`BackColor` properties (nullable). Parser v `BuildPoznamky` + `BuildOdjezdyBySkupina` čte `FORE_COLOR`/`BACK_COLOR` z JSON. Render v `NotesLegend.RenderSymbolWithText` aplikuje barvu na znak (tělo zůstává default). Render v `DepartureGrid` aplikuje barvu na anotaci nad minutou (konzistence s legendou).
  - **NZ vyloučeno** — `NegativniZnacka` má vlastní invertovaný styl (`NegativniZnackaBackColor`/`ForeColor` v elementu), color override z DB se neaplikuje.
  - **BackColor přenášeno přes wire, ale render zatím ignoruje** — klient explicitně chce zatím jen barvu znaku. Připraveno pro budoucí rozšíření bez schema break.
- **Stav v aktuálních DPKV datech (KV_DP):**
  - 1071 záznamů `PoznamkaSpoj`, z toho 69 má barvy (1003-1071) ale `TiskJRMHDZastavkove=0` (jen vozové). Záznamy 1001-1002 mají `TiskJRMHDZastavkove=1` ale žádnou barvu.
  - **Žádný současný záznam nemá současně zapnutý zastávkový tisk AND barvu** — infrastruktura připravená, klient si nastaví v DB.
  - Quick test pro ověření: dočasně `s ^ALVA.MHD.ENUMS("KV_DP","PoznamkaSpoj","DTA",1002,"TiskJRMHDVozoveBarvaZnakuRGB") = "255;0;0"`, najít linku se spojem používajícím 1002, render → znak „W" červený v legendě i nad minutou.
- **Co bylo doprovodně opraveno:** `<SUBSCRIPT>` pád v iteraci `^||TiskMHD("trasa",SmerID,...)` při linkách se sloučenými zastávkami (např. linka 1073). Příčina: `SloucitTotozneZastavky` zapisovala pomocný klíč `"slouceniInv"`, ale 7 iterací jen skipovalo `"slouceni"`. Fix: sjednoceno na `i 'BodID continue` (numeric coercion filtruje všechny ne-numerické subscripty, future-proof).
- **Auto-generované poznámky** (rozdíl tras 1001/1002 — „končí v zast. X", týdenní platnost) zůstávají default černé. Klient explicitně nepožadoval; pokud někdy přijde request, bude to drobné rozšíření (per-linka color v `LSO` nebo per-algorithm static).

### C2. Hlavička sekce poznámek (NotesLegend) — již plně podporováno
- **Tiket:** #10660 (DPKV, Jan Slavík); zůstává **suspended**.
- **Datum ověření:** 2026-05-12
- **Výsledek:** ✅ Funkčně pokryto. `NotesLegendElement` má `HeaderText` property (default „Poznámky") + volá `RenderSectionHeader` (background + text + linka pod) — stejný pattern jako StopList. UI exponuje text v properties panelu (group „Záhlaví"), barvy pozadí + textu přes `GetCellZoneBaseColorDescriptors` (skupina „Záhlaví"). Klient si může nastavit libovolný text + barvy v designeru.
- **Diagnóza klientovy zmatky:** DPKV šablona `kv.json` má `HeaderText = "na znamení"` (omylem nastaveno z dřívější iterace, kdy NotesLegend obsahoval jen tu poznámku). Po opravě na „Poznámky" header funguje jak DPKV chtěl.
- **Code path:** `NotesLegendElement.Render:66-67` → `RenderSectionHeader` v `ScheduleElementBase`. Property descriptor v `GetPropertyDescriptors` (Label = „Nadpis", Group = „Záhlaví").

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

## TR. TRANSDEV — konsolidovaná sada požadavků (2026-07-03, k analýze)

Souhrnný wishlist k zastávkovému CP (SK legislativa + praxe MHD Trenčín). **Neanalyzováno** — před
implementací dohledat/založit tikety + upřesnit. U každého poznámka o stavu/infra na TD straně:

1. **Časová os cez polnoc** (noční linky 22→5h) nebo aspoň manuální přeuspořádání hodin.
   → ✅ **Už hotové (B3, #9036)**: `TimeAxisMode=Range`, `HODINY_OD=22`/`HODINY_DO=5` → wrap přes půlnoc
   (extended hours). **Klient nejspíš netestoval na své verzi** — ověřit u něj, netřeba implementovat.
2. **Zastávky na znamení — „všechny"**: když jsou VŠECHNY zastávky na znamení, místo značek u každé
   vygenerovat souhrn „× všechny zastávky linky jsou na znamení" (analogie k bezbariér souhrnu). Konfigurovatelné
   (CZ legislativa může chtít značky). → **Pattern existuje** (AD bezbariér `allBBP` agregát); relates #7161. NEW, S–M.
3. **Zobrazení nástupiště**: nový sloupec vedle názvu zastávky (SK legislativa). → **Data existují** (`Oznacnik`
   v `ALVA.MHD.ZST`); přidat sloupec do StopListu + AD GetData pole. NEW, S.
4. **Výška řádku „zastávka" — auto (3. režim)**: pevná výška, při nedostatku místa se začne zmenšovat.
   → **Částečně**: StopList má auto-scale fontu při přetečení pevné Height (`ComputeFontScale`). Chce explicitní
   „fixed-then-shrink" režim výšky řádku. S–M.
5. **Definice směru/cíle** — položka „cíl" odkazovatelná v šabloně. 3 varianty: (min) univerzální cíl per směr;
   (střední) cíl textem per zastávka × směr v trase (jako nástupiště/sloupík); (ideál) DB cílů + id per zastávka×směr.
   Dnes hackují přes `informace`/`titulek`. NEW, datový model, M–L. *(Odbourá i speciální cíle pro tabule.)*
6. **Barva linky**: ve vlastnostech linky bg + text barva; v šabloně u objektu volba „farba linky" místo pevné
   z palety → aplikuje se při tisku. Šetří hodiny u grafických změn (MHD Trenčín). NEW, M.
7. **Jízdní doby — výběr sloupců variant**: víc variant trasy → víc sloupců jízdních dob; číslování dnes odráží
   pořadí výskytu variantu v seznamu spojů (nestabilní — Sloupec 3 na začátku = Sloupec 2 uprostřed). Chce
   explicitní kontrolu (které spoje / úsek řídí generování). → **Relates L1 multi-varianta TrasaMin** (`VARIANTY_TRAS`
   + `ShowAllRouteVariants`); je to ta „jemnější výběr variant (per-písmeno)" z `cache-database-linky §10`. M.
8. **Jízdní doby — „<1" místo „0"**: nulová jízdní doba mezi zastávkami → zobrazit „<1" (méně než minuta) místo „0".
   Konfigurovatelné. NEW, S.
9. **Poznámkování spojů — obrácená logika**: dnes horní tabulka (značka/text/spoje) + spodní tabulka (kde
   NEpoznámkovat). Chce: rozsah TČ přímo v první tabulce vedle značky/spojů. Legacy-input workflow (tab „MHD").
   Zjednodušení, méně chyb. M (Caché-input strana).
10. **Zobrazování spojů — skrýt v So/Ne/svátky**: dnes „spoje s NZ se nezobrazí" jen pro sloupce školní
    výuka/prázdniny; chtějí i skrýt spoje o So/Ne/svátcích. → rozšíření filtru (`MHD - další`). NEW, S–M (Caché).

## H. Status komunikace s klienty

M-line zákazníkům slíbil:

- **„Nový tiskový modul" / „Studio JŘ"** — release **polovina 2026**.
- Citace z `2024-10-04` na #7937, #7947, #7955, #7959 (TRANSDEV): *„S ohledem na množství podnětů týkajících se specifických možností nastavení a chování tisku jízdních řádů pracujeme na vývoji nového nástroje v našem systému (...) nový nástroj bude uvolněn přibližně v polovině příštího roku."*
- DPKV (#10660) z `2025-07-10` (Dita Chabičovská): *„momentálně se musíme soustředit především na obsahové aktualizace — jak zastávkových, tak i knižních jízdních řádů. Současně ještě nemáme uzavřenou část projektu týkající se plánování směn (...) vrátíme se k němu po dokončení klíčových etap implementace MHD ve Vašem dopravním podniku."*
- KORID (#11300) z `2025-12-08` (Jiří Bžoch): *„úpravu již »bohužel« nestihneme udělat v letošním roce. Problému se ale budeme věnovat příští rok, jeho řešení pravděpodobně spojíme s novým tiskovým modulem pro JŘ."*

**Z toho plyne:** roadmapa TemplateDesigneru je **viditelný závazek** vůči TRANSDEV, KORID a DPKV. Při uzavírání jednotlivých tiketů odkazovat na konkrétní featuru v TD + verzi releaseu.
