# Zastávkový JŘ - požadavky zákazníků z helpdesku

Snapshot z Podpory M-line k **2026-05-11**, **konsolidováno 2026-07-10, revize 2026-07-20**
(sekce 0 = stav pro uzavírání tiketů + dispatch; P3 imposice hotová, malí TR kandidáti odbaveni;
2026-07-20 ověřeno proti živému HD - #8772/#7161 resolved, skupina „ready-to-close" vytipována,
neevidované tiskové tikety protříděny = všechny linkové/mimo, viz „Mimo scope" níže).
Slouží jako vstup pro plánování dalšího rozvoje TemplateDesigneru a Caché agend
(`ZastavkoveJrMhd` / `ZastavkoveJrAd`). Aktualizuj při dalším procházení tiketů.

> **Pozn. 2026-07-10:** od snapshotu přibyla **AD agenda** (`ZastavkoveJrAd`, TRANSDEV jede primárně AD)
> a většina helpdesk featur je vyřešená právě pro AD. Dnešní kontraktní práce (A-AD: skaláry `SMER` +
> `POPIS_SMER`=konečná + `NASTUPISTE`) a B2 (overflow konzistence) posouvají některé TR body - viz sekce 0.

> **Postup pro AI agenta:**
> 1. Otevři odpovídající tiket přes `mcp__claude_ai_Podpora_M-line__get_ticket` s UUID z tabulky.
> 2. Pokud řešíš jednu z featur → ověř aktuální stav v UI a v `ZastavkoveJrMhdAPI`.
> 3. Po implementaci přesuň položku do sekce „Hotovo" a doplň datum + commit.
> 4. Při interpretaci nezjednodušuj - některé požadavky jsou jemné rozdíly proti tomu, co TD dnes dělá (např. „máme šipku směru, ale klient ji chce v opačné pozici").

---

## Aktivní tickety (otevřené + pozastavené, agenda Jízdní řády / MHD)

| # | UUID | Stav | Zákazník (kontakt) | Téma |
|---|---|---|---|---|
| 10660 | f3d3a7d8-0c3d-4076-93d1-c12bb25e851d | suspended | **DPKV** (Jan Slavík) | 4 oblasti grafických připomínek po importu z legacy |
| 11300 | 653b5c65-b84f-49e8-803f-329febe5629e | suspended | **KORID** (Jaroslav Smištík) | Skrýt prázdné přípojové řádky (PD vs. víkend) |
| 8828  | 02063e8a-e2c1-476d-a527-77e77e0f263e | suspended | **TRANSDEV** (Michal Podloučka, Jakub Holík) | Per-sloupek zastávky - samostatná vývěska |
| 8772  | b62718c6-e0aa-478a-be31-bdfb8c7b9917 | **resolved 15.7.** | **TRANSDEV** (Jakub Holík) | Návrhář sestav - podmíněný tisk sekcí |
| 7947  | 585158b0-d063-40d9-87de-eb076a236a87 | suspended | **TRANSDEV** (Michal Podloučka) | Logika auto-poznámky u poslední/předposlední zastávky |
| 7959  | 45fdc840-ce70-4436-a331-c5a61732d9d6 | **resolved 20.7.** | **TRANSDEV** (Michal Podloučka) | Skrýt zastávku v seznamu, pokud z tištěné zastávky není obsluhována |
| 7955  | dd0f7bd3-0efa-4e2d-947d-ed16559952ce | suspended | **TRANSDEV** (Michal Podloučka) | Vlastní komentář spoje má přebít auto-poznámku + 2 buga |
| 7937  | 64fd847c-7090-47d1-bc91-bc581b0aea42 | suspended | **TRANSDEV** (Michal Podloučka) | Odsazení/odrazení zastávky per směr, jen pro zastávkové JŘ |
| 7161  | 44e0a446-b05b-4c44-9497-3c96fbb945a2 | **resolved 15.7.** | **TRANSDEV** (Vladimír Šimko) | Vypínatelné automatické poznámky (na znamení, bezbariér.) |
| 9036  | 02f8fcda-25c5-4a2e-8784-79bc84860811 | suspended | **TRANSDEV** (Vladimír Šimko) | Nočné spoje - časová osa cez polnoc (22-5h) → **pokryto B3** |

Otevřené tikety #12434 (BBSK - „Export Tisk JŘ") a #12260 (BUSLINE - „úprava šablony") jsou obecné tracker / dotazy, ne konkrétní featury - neuvedeny.

Mimo scope zastávkového JŘ (protříděno proti živému HD 2026-07-20 - všechny tiskové „suspended" tikety
mimo tabulku výše jsou linkové/knižní JŘ = Fáze C/D, nebo úplně mimo TD; **žádný zastávkový nechybí**):

**Linkové / knižní JŘ (→ Fáze C/D, ne sem):**
- **#7487** (KORID) - NZ + týdenní platnost 4 na řádek.
- **#8360** (KU HK) - přípojové řádky: kurzíva / barva / velikost písma, aby čas přípoje nesplýval s časem spoje.
- **#8372** (KORID) - „vkládat prázdné sloupce" do nastavení tisku (roztažení řídkých linek na A4).
- **#8576** (KU HK) - tarifní zóny do sloupců + více zón u hraniční zastávky (layout linkového JŘ).
- **#9417** (KORID) - dopravci v záhlaví souhrnného JŘ jen pro spoje na dané stránce (velké A4 linky).
- **#11373** (KAD) - negativní značky a poznámky **vedle** tabulky s časy (layout linkového JŘ).
- **#9088** (TRANSDEV) - linkový cestovný poriadok · **#9283** (KOVED) - sdružený JŘ IDZK · **#11300** (KORID) - prázdné přípojové řádky.

**Úplně mimo TD (Studio JŘ / editace dat, ne tisk):** #12803 (KOVED - výběr změněných km úseků),
#7929/#8067/#8175/#8457/#9481/#9483/#10442/#12854 (editace spojů, MHD linek, stažky) - součinnost kolegů z JŘ.

**Obecné tracker / dotazy (ne konkrétní featura):** #12434 (BBSK), #12260 (BUSLINE).

---

## 0. Konsolidovaný stav - pro uzavírání tiketů a dispatch (2026-07-10)

> Jedno místo: co je hotové (→ otestovat → zavřít na helpdesku), co malé/velké zbývá. Detaily v sekcích níže.
> **Legenda:** ✅ hotovo (AD) · 🧪 hotovo, ověřit u klienta · 🟢 malé zbývá · 🟠 velké zbývá (analýza/data) ·
> ⬜ MHD-port (AD hotové, MHD ne) · ➖ mimo TD / mimo scope.
> ⚠️ Featury jsou vyřešené primárně pro **AD** agendu (TRANSDEV = AD); MHD-port jen na reálnou potřebu (DPCB/DPKV).

### 0.1 Tikety (helpdesk)

> **Pořadí projití (2026-07-28):** ▶ **1. ready-to-close** = ověřit v buildu + resolve odpovědí zavřít
(zbývají #7947, #7955; #9036 resolved 28. 7.). ▶ **2. testování u zákazníka**
> (#10660 DPKV - hotové, ale nejdřív zpřístupnit + proklikat jejich obrázky). ▶ **3. feature tracky, nezavírat**
> (#7937, #8828 - čeká práce v TD). Hotové zavřené: #8772, #7161 (15.7.), #7959 (20.7., čeká ověření klienta).

| Ticket | Zákazník | Feature | Stav | Otestovat před zavřením | Detail |
|---|---|---|---|---|---|
| #8772 | TRANSDEV | Podmíněný tisk sekcí | ✅ **resolved 15.7.** | - (zavřeno) | §E1 |
| #7161 | TRANSDEV | Skrýt na-znamení legendu (D5) + vypnout agregát bezbariér (D6) | ✅ **resolved 15.7.** | - (zavřeno) | §D5/D6 |
| #7959 | TRANSDEV | Skrýt neobsluhovanou zastávku | ✅ AD · **resolved 20.7.** (čeká ověření klienta) | Filtr `servedPoradi` = defaultně, bez přepínače (jiné než „projeté zastávky"); klient ověří na 876401/876402 | §A6 |
| #7947 | TRANSDEV | „jede až do XY" u poslední zastávky | ✅ **hotovo, připraveno k zavření** - znění dohodnuto 30. 7. (cíl na začátku věty + průjezd), ověřeno na `CB_GWBUS` 501/0000001/T proti starému modulu (tytéž 3 skupiny spojů, tytéž zastávky). | Zavřít **s výhradou**: ověřeno na naší lince, ne na datech zákazníka (876401/876402 nemáme) | §D1 |
| #7955 | TRANSDEV | Vlastní komentář přebije auto (D2) + NZ bug (D3) | ✅ AD · ▶ **ready-to-close** | `SCHOVAT_V_ROZSAHU`=Automatické → auto-rozdíl zmizí, ruční zůstane; NZ nemizí | §D2/D3 |
| #7955 | TRANSDEV | Výběr spoje celým číslem (D4) | ➖ mimo TD | UI bug ve Studiu JŘ (EDISON) - do resolve poznámka, řešit jinde | §D4 |
| #9036 | TRANSDEV | Časová osa přes půlnoc (22→5) | ✅ **RESOLVED 2026-07-28** - ověřeno Janem na nočním provozu MHD JH (345070/CB_CSADAD): celodenní osa „od 4 do 0" (půlnoc na konci) i noční výřez „22–5"; odpověď s oběma recepty odeslána | - | §B3 |
| #10660 | DPKV | A1–A4 StopList (šipka/značky/podbarvení/per-col font) + B1/B2 záhlaví DepartureGrid + C1/C2 legenda | ✅ · 🧪 **testování u zákazníka** | Render DPKV šablony: pozice šipky/značek, podbarvení aktuální, per-column font, 2-řádkové záhlaví (underline), barvy značek | §G |
| #7937 | TRANSDEV | Odsazení zastávky per směr / per Tč | 🟢 base AD · 🟠 rozšíření | Základ (odsazení z `^JRL` dta7) hotový; per-směr + per-Tč pásmo zbývá (změna zdroje) | §A5 |
| #8828 | TRANSDEV | Označníky → samostatná vývěska | ✅ **RESOLVED 2026-07-28** - parametr „Samostatná stránka pro každý označník" (+ titulek „…, označník X", spoj bez označníku na stránku nejčetnějšího); odpověď odeslána, klient ověří na 876420/863383 (znovuotevření = záchytka). MHD zdroj označníku až u MHD zákazníka. | changelog 27.–28. 7. |
| #11300 | KORID | Skrýt prázdné přípojové řádky | ➖ linkové JŘ | Přípoje nejsou koncept zastávkového JŘ (řešit s linkovými) | §D7 |
| #13048 | KOVED (IDZK/Lábsky) | Poznámky/sloupce zastávek (5 bodů) | ✅ **RESOLVED 2026-07-28** - všech 5 bodů v1, souhrnná odpověď odeslána (vč. info o stránce pro označník); klient ověří po dnešní verzi, znovuotevření = záchytka | Viz rozpad níže | §0.3 |

**Poznámka k uzavírání:** ready-to-close jsou u klientů `suspended` (M-line slíbil „nový tiskový modul"
polovina 2026 - viz §H). Zavírat po ověření dané featury v releasu TD + odkazem na konkrétní funkci
(vzor odpovědi = #8772/#7161: „vyřešeno v novém Návrháři sestav, viz wiki").

### 0.2 Nové požadavky bez samostatného tiketu (před uzavřením dohledat/založit ticket)

**TRANSDEV konsolidovaná sada (2026-07-03) - §TR:**

| # | Co | Vel. | Stav (2026-07-10) |
|---|---|---|---|
| TR1 | Časová osa přes půlnoc | - | ✅ = #9036 **resolved 2026-07-28** |
| TR2 | Na-znamení „všechny" souhrn (místo značek u každé) | S–M | 🟡 **IMPLEMENTOVÁNO 2026-07-10** (AD, opt-in `SOUHRN_NA_ZNAMENI`, gold-safe) · ⚠️ **čeká SK ověření**: SKNI nepoužívá „x" pro na-znamení → kolega prověřuje kódování |
| TR3 | Nástupiště jako sloupec vedle názvu zastávky | S | ✅ **HOTOVO 2026-07-10** (AD J-věta #3 + MHD Oznacnik; StopList sloupec „Nástupiště" opt-in). Ověřeno 0320680. |
| TR4 | Explicitní „fixed-then-shrink" výška řádku StopList | S–M | ✅ **HOTOVO 2026-07-10** - pevná `DataRowHeight` se zmenší při přetečení (default auto beze změny); sdílený helper `ScaledDataRowHeight` (StopList + DepartureGrid). |
| TR5 | Definice cíle referencovatelná v šabloně | M–L | 🟠 · ⚠️ **(min) „cíl per směr" HOTOVO** dnešním `POPIS_SMER`=konečná (A-AD); zbývá střední (per zast.×směr) + ideál (DB cílů) |
| TR6 | Barva linky (bg/text ve vlastnostech linky → volba v šabloně) | M | 🟠 nový datový model |
| TR7 | Výběr sloupců variant jízdních dob (stabilní číslování, per-písmeno) | S–M | 🟡 **zčásti vyřešeno 2026-07-27** (#13048 bod 5): varianty dle sledu zastávek (málo sloupců) + parametr „Řazení variant tras: Stabilní". Zbývá jen případný per-písmeno výběr - až TRANSDEV potvrdí nad novým chováním |
| TR8 | „<1" místo „0" u nulové jízdní doby (konfigurovatelné) | ? | ⏸️ **park - premisa nejasná** (viz níže §TR/8): naše časy jsou kumulativní → 0-segment se tiskne jako opakované číslo (1,1), ne „0"; per-segment sloupec TD nemá. Ověřit u TRANSDEV, který sloupec (legacy „Průběhy"?) |
| TR9 | Obrácená logika poznámkování (rozsah TČ v 1. tabulce) | M | 🟠 Caché-input (Studio JŘ) |
| TR10 | Skrýt spoje o So/Ne/svátcích | S–M | 🟠 Caché filtr |

**Z prezentací zákazníkům - §P:**

| # | Co | Vel. | Stav |
|---|---|---|---|
| P3 | Imposice 2×A5 → A4 | - | ✅ **HOTOVO 2026-07-12** (v1: N-up „Uspořádání na list" v tiskovém dialogu, mřížka AUTO, opakování pro řezání, ořezové značky; per-agenda persistence). Brožura/duplex řazení = vědomě v2 (knižní JŘ). Detail §P3. |

> **P1 a P2 vyřazeny (2026-07-10, dohoda):** P1 (poznámky do patičky) - nejasné přesné zadání, neodbavovat;
> P2 (sloučit více linek) - řeší se jinde (mimo tuto agendu). Detail v §P ponechán jako evidence.

### 0.3 #13048 KOVED - poznámky a sloupce zastávek (5 bodů, procházeno 2026-07-24→26)

Excel od IDZK (Lábsky) k zastávkovému JŘ z našeho návrháře. Body:

| # | Co | Vel. | Stav |
|---|---|---|---|
| 1a | Sloupec zón prázdný - ověřit, že něco vracíme | S | ✅ **HOTOVO 2026-07-24** - zóny 1:1 vs legacy (gold oracle `ZastavkoveJrAdLegacyTest`); prázdno u KOVED = jejich data/šablona, ne regrese |
| 1b | Přestupy (na znamení / vlak / bus) nejsou vidět | S–M | ✅ **HOTOVO 2026-07-26** - přepínač „Přestupy (vlak/bus)" na StopListu (vlak L / autobus A), default vyp = jako legacy; symbol u zastávky, ne v legendě. Na-znamení funguje beze změny |
| 2 | Per-buňka zmenšení fontu na šířku (shrink-to-fit) | S–M | 🟡 **IMPLEMENTOVÁNO 2026-07-27 jako vestavěné chování** (bez checkboxu, rozhodnutí Jan) - přetékající text buňky se v `CellRenderer` zmenší na šířku (trigger až skutečné přetečení; rezerva 0,6 mm, u názvů zastávek dle geometrie šipky 2,6 mm; značky za názvem se posouvají a škálují s textem). Platí pro buňky přes CellRenderer (StopList/DynamicTable/LineTable/záhlaví); NE legenda a minuty (vlastní render). Čeká finální vizuální ověření |
| 3 | Sloupec nástupiště | S | ✅ **OVĚŘENO 2026-07-27** - KOVED chce „čísla sloupků z trasy" = přesně TR3 (J-věta `#3` per směr, sloupec „Nástupiště" opt-in). Část „příp. z detailu spoje" = per-spoj nástupiště z **W-trasy** (zatím nečteme) → splývá s bodem 4 |
| 4 | Zastávka s více nástupišti (více řádků/hodnot) | M | ✅ **HOTOVO 2026-07-27 - všechny 3 režimy.** Přepínač „Více nástupišť" na StopListu: nejčetnější / výčet „2,3" / **duplikace řádku per stanoviště** (`RowPerPlatform`). Duplikace: klientská expanze `ExpandPlatformRows` nad aditivním serverovým polem `NASTUPISTE_VARIANT` (nástupiště per varianta, W pozice 7). Zastávka se ≥2 stanovišti → N řádků na místě v trase, časy jen variant daného stanoviště, vývěska/jednostanovištní = 1 řádek. Ověřeno daty 0330510 (`TestNastupisteVariantEmit` + 5 xUnit). Vývěsky per sloupek (samostatné stránky) = #8828 |
| 5 | Varianty tras (řazení dle četnosti, max čas, písmena, MHD parita) | M–L | ✅ **HOTOVO 2026-07-27, vizuálně ověřeno Janem na 0330510** - seskupování variant předěláno na **sled zastávek s max časy** (= legacy `prutmbmp` dedup; 0330510: 33 sloupců → 11; smoke test odhalil i letitou chybu `|`=„čas 0" a bool parametry na wire). Parametry (skupina Seznam zastávek): „Řazení variant tras" (dle četnosti default / stabilní - TR7), „Písmena variant u odjezdů" (default vyp; písmeno u minuty nahrazuje automatické rozdílové poznámky), „První varianta bez písmene" (default zap). **Nástupiště v identitě varianty vědomě NE** (duplicitní vizuálně identické sloupce) - „jiné stanoviště" řeší duplikace řádku (bod 4 `RowPerPlatform`) + #8828 vývěska per označník. MHD parita variant = mimo scope v1. Symbol „↓" u výchozí zastávky = odložený drobný vizuál |

Detaily hotových: changelog 2026-07-24 (zóny) + 2026-07-26 (přestupy) + 2026-07-27/28 (body 2, 4, 5, titulek, slovník „označník"). **Tiket resolved 28. 7.** - zbývá jen finální vizuál bodu 2 v rámci B2 (zavírací kolo); reklamace = znovuotevření tiketu.

**Dispatch:** malí kandidáti odbaveni (TR3 ✅, TR4 ✅, TR2 implementováno - ⚠️ čeká SK ověření
kódování na-znamení, TR8 ⏸️ park do potvrzení TRANSDEV). Zbývají jen 🟠 velké body (analýza/data).

### 0.4 Follow-upy uzavřených tiketů - JEN na vyžádání zákazníka (nezakládat dřív)

Vzniklé při stanovišťovém balíku a #9036 (27.-28. 7.); tikety jsou resolved, tohle jsou připravené
odpovědi na případné znovuotevření/dotaz:

- **Členění exportu na samostatné soubory per označník** - dnes rozdělené vývěsky rozliší titulek
  i `{POPIS_ZASTAVKA}` v názvu souboru (#8828)
- **Symbol „↓" u výchozí zastávky** ve variantních sloupcích (KOVED XLSX vizuál, bod 5)
- **Agregace označníku varianty přes všechny spoje** - dnes reprezentativní spoj; projeví se jen
  u dvou spojů se zcela shodnou trasou a různým označníkem (bod 4/5, přiznáno v odpovědi KOVED)
- **Duplikace řádku - fáze 2 dle zpětné vazby KOVED** (bod 4)
- **Per-písmeno výběr variantních sloupců** (TR7 zbytek - až TRANSDEV potvrdí nad novým chováním)
- **Vizuální oddělení přelomu půlnoci v mřížce** (#9036 v1 = jen přetočení osy; kandidát silnější
  čára mezi 23 a 0, automaticky jen když osa přetéká)
- **Zvýraznění ne-výchozích hodnot v liště parametrů** (per-agenda dědění hodnot mate - rozhodnutí
  Jan 28. 7.: zatím nechat)
- **Spodní mez zmenšení textu buňky** (extrém se zmenší bez limitu - zvážit při vizuálu bodu 2 v B2)

### 0.5 Parita a rozšíření - JEN na poptávku (přesunuto z roadmapy 29. 7.)

Není to nedodělek, ale práce, která se vyplatí až s konkrétním zákazníkem - v aktivní roadmapě
jen zabírala místo (pravidlo „roadmapa = akční práce"). Velikosti: S = do hodiny, M = půl dne.

- **Tiskový parametr „Rozdělení sloupců" pro MHD agendu** (M) - sjednocení s AD, kde už je
  (MHD SMO fáze 3)
- **Parita variant tras + písmen u odjezdů pro MHD agendu** (S–M) - AD hotové 27. 7. (#13048 bod 5),
  MHD až u zákazníka, který to bude chtít
- **MHD zdroj označníku pro samostatné stránky per označník** (S–M) - AD hotové, dtto
- **Exportní parametry per šablona** (S–M) - název souboru / členění / imposice jsou dnes per agenda;
  převod na šablonu má smysl, až někdo bude mít dvě šablony téže agendy s jiným exportem
- **Per-sloupcové zvýraznění řádků** (`HighlightRule.TargetColumn` - model to pole nese, render
  barví celý řádek) + **barva textu / kurzíva** pravidla zvýraznění

---

## A. StopListElement - seznam zastávek vlevo

### A1. ✅ HOTOVO (2026-05-12) - viz §G

### A2. ✅ HOTOVO (2026-05-12) - viz §G

### A3. ✅ HOTOVO (2026-05-12) - viz §G

### A4. ✅ HOTOVO (2026-05-12) - viz §G

### A5. 🟡 Základ hotový pro AD (2026-07-09) - per-směr / per-Tč otevřené

- **Zákazník:** TRANSDEV (Michal Podloučka), #7937 - TRANSDEV jede primárně **AD** (viz A6), takže cílová agenda = `ZastavkoveJrAd`.
- **Citace:** *„potřebuji mít možnost odstadit/odrazit zastávku o určitý počet bodů, ale pouze pro zastávkové JŘ a pro jednotlivé směry (v jednom směru ano, v druhém ne)"*
- **Dodatek:** *„zda by šlo odražení nastavit tak, že se bude odrážet zastávka v nastavených Tč vývěsu"* (per časové pásmo)
- **✅ Hotovo - základní odsazení (AD):** datový flow dotažen. `ZastavkoveJrAd` čte odsazení z
  `^JRL(sku,lin,"J",poradi)` **5. `#`-segment** (legacy `mkPLinWin` „odrazeni nazvu zastavky" = `gTrsLin dta 7`,
  ověřeno `JRapiLin.mac`/`JRapiLinPrtWin.mac`) → `BuildZastavka ODSAZENI` → element `z.Odsazeni` → N mezer.
  Živě ověřen render. Behavior-preserving (gold netknuté). Commit `62852f2`.
  - ✅ TD model `ZastavkaData.Odsazeni` + `ShowStopIndentation` toggle + render (prefix spaces).
- **⚠️ Otevřené (rozšíření nad legacy - potřebují datový model + UI rozhodnutí):**
  - **Per-směr** („v jednom směru ano, v druhém ne") - `^JRL` dta7 je **jedna hodnota per bod trasy**,
    sdílená pro tam i zpět (i legacy má single-per-point). Odsazení se teď projeví v **obou** směrech.
    Per-směr = nutná změna zdroje (per-směr pole) + editor.
  - **Per-Tč pásmo vývěsu** - neřešeno.
  - **MHD strana** - `ZastavkoveJrMhdAPI` má pořád hardcoded `s odsazeni = 0` („legacy: dta 10" v `ALVA.MHD.ZST`).
    Relevantní jen pro self-contained MHD (DPCB/DPKV), ne TRANSDEV.
- **Render caveat:** mezery jsou proporcionální (počet mezer = počet znaků); kdyby vizuál nesedel s legacy, přepočet na pevnou šířku.

### A6. ✅ Hotovo (2026-06-20) - ověřeno na úrovni kódu pro AD; klient potvrdí na svých datech

- **Zákazník:** TRANSDEV (Michal Podloučka), #7959
- **Upřesnění agendy:** TRANSDEV jezdí primárně **linkovou (AD)** + lokální MHD; **self-contained MHD
  modul používají jen DPCB/DPKV**. Tiket se proto týká **AD agendy** (`ZastavkoveJrAd`), ne MHD modulu.
- **Citace:** *„Mám linku, která má druhou zastávku pro školní spoj... Ovšem z první zastávky na tuto druhou zastávku žádný spoj nepojede. Potřeboval bych, aby zastávka nebyla v seznamu zastávek, pokud není aktuální."*
- **Stav v AD - obě chování implementovaná a aktivní:**
  - **(a) StopList filtr** - neobsluhovaná zastávka se nepřidá do seznamu: `ZastavkoveJrAd:296`
    `i '$d(servedPoradi(poradi)) continue` (`ObsluhovanePoradi`).
  - **(b) Stránka se negeneruje** - zastávka bez odjezdů: `:333` `odjezdy.%Size()=0 q ""` + `:278` terminus-skip.
- **⚠️ Známá nuance k případnému doladění** (jen pokud klient nahlásí): AD filtr (a) je
  **směrově-globální** (`servedPoradi` = staví tam aspoň 1 spoj směru), kdežto MHD je **from-current**
  (`ObsluhaBitmap` = obsluha z aktuální zastávky). Pro přesný školní scénář (zastávka dosažitelná jen
  spojem, který z tištěné zastávky nevyjíždí) by AD mohl zastávku ponechat. Pokud to vadí → přepnout AD
  na from-current sémantiku. Zatím necháno (těžko se hledá testovací linka; klient ověří v praxi).
- **Opt-out** (kdyby chtěl vidět vše bez filtru) → param `TISK_VSECH_ZASTAVEK` (paralelní k `TISK_PROJETYCH_ZAST`), zatím YAGNI.

---

## B. DepartureGridElement - mřížka minut odjezdu

### B1. ✅ HOTOVO (2026-05-12) - viz §G

### B2. ✅ HOTOVO (2026-05-12) - viz §G

### B3. ✅ HOTOVO (2026-05-12) - viz §G (auto-fit zvětšení odloženo)

### B4. Per-sloupek zastávky - samostatná vývěska ⏸️ Odloženo (2026-05-12)
- **Zákazník:** TRANSDEV (Jakub Holík přes Michala Podloučku), #8828
- **Konkrétní příklad:** Linka 876420, zastávka „Těrlicko,,obecní úřad" má 2 sloupky. Většina spojů obsluhuje sloupek 2 (oba směry), ale vybrané spoje obsluhují sloupek 1. Druhý příklad: linka 863383, zastávka „Šenov,,Škrbeň" má dva sloupky v každém směru - koordinátor (KORID) generuje vývěsky **per sloupek** odděleně.
- **Citace klienta po prvním M-line návrhu** (rozdělit zastávku na 2 v trase linky): *„Vámi navržené nás s kolegy samozřejmě napadlo, ale asi to vždy není vhodné. (...) V JŘ je pouze jedna zastávka Šenov,,Škrbeň (...) Proto dále prosím o zvážení jiného způsobu řešení, než popisujete."*
- **Co máme dnes:** Multi-data PDF export generuje 1 PDF stránku per `(zastávka × směr)`. Nezná pojem „sloupek" - atribut sloupku není ani v `ZastavkoveJrMhdAPI.GetData`.
- **Co klient chce:** Pro zastávku s více sloupky vygenerovat **samostatnou vývěsku per sloupek** - jen spoje obsluhující daný sloupek se vytisknou.
- **Gap:**
  - Caché: rozšířit `BuildOdjezd` (nebo `PromitnutSpoje` fáze 4) o filtr per sloupek - předtím rozšířit working globální klíč o `sloupekID` subscript: `^||TiskMHD("odjezdy",SmerID,BodID,TypProvozuID,sloupekID,hh,mm,SpojID)`.
  - `GetData` API: pro zastávku s více sloupky iterovat `(Smer × Bod × Sloupek)` → `OUT[N]` per sloupek. Skalární info per stránka rozšířit o `SLOUPEK_CISLO` / `SLOUPEK_POPIS`.
  - TD: **žádná změna kódu** - multi-data export už zvládá libovolný počet OUT stránek. Volitelně placeholder `{SLOUPEK_CISLO}` v šabloně.
  - Pro: zachovat single zastávku v trase linky (klient výslovně odmítá duplikaci v trase).
- **Důvod odložení:** Téměř 100 % práce je v Caché a vyžaduje:
  - **Datový průzkum** - zjistit, kde v `ALVA.MHD.*` globálech leží info o sloupcích (atribut spoje? per-bod v trase `("ModelTrasaBodDetail",BodID,"Val","SloupekTam")`? jinde?). Není dokumentováno v `cache-database.md`.
  - **Business decision s kolegy** - identifikace sloupku (číslo 1/2/3, písmeno A/B, nebo popis?), default chování (opt-in přes parametr `PER_SLOUPEK` vs vždy zapnuté), chování pro zastávky s jedním sloupkem.
  - Po vyjasnění obojího je vlastní implementace odhad **~půl dne až den** (rozšíření working globálu + iterace v `SestavitStranky` + nový parametr v `AddStandardJRParameters`).

---

## C. NotesLegendElement - legenda poznámek pod mřížkou

### C1. ✅ HOTOVO (2026-05-12) - viz §G (DB-driven barvy z PoznamkaSpoj)

### C2. ✅ HOTOVO (2026-05-12) - viz §G (verifikace, žádný kód)

### ~~C3. NZ + týdenní platnost - 4 na řádek (úspora místa)~~
**Mimo scope zastávkového JŘ** (přesunuto, 2026-05-12). Tiket #7487 (KORID) se týká **knižních/linkových** JŘ, ne zastávkových. Patří do roadmap knižních JŘ až bude existovat.

---

## D. Generování poznámek a obsluhových údajů (Caché `ZastavkoveJrMhdAPI`)

### D1. Text „spoj jede až do XY" pro spoje za základní trasu
- **Zákazník:** TRANSDEV (Michal Podloučka), #7947
- **Citace (zjednodušeno z dlouhého popisu):** Pro linky 876402, 876401, kde málo spojů jede až do koncové (Stonava, Nový svět / Horní Suchá, Pašůvka):
  - Pokud základní trasa končí v předposlední → spoje do poslední dostanou poznámku „jede přes zast. XY" (XY = předposlední) - **nesprávně formulováno, popisuje co bus míjí, ne kam dojede**.
  - Pokud základní trasa končí v poslední → všechny spoje se písmenkují (přebytečné info, když do koncové jezdí 2 spoje).
  - Pokud obě poslední mimo základní trasu → ještě horší.
- **Co klient chce:** *„Asi bych potřeboval, aby program uměl vypsat u poslední zastávky poznámku, že spoj jede až do."*
- **Co máme dnes:** `MakeOmezTrasaTxMH...` legacy helper + algoritmy 1001 (písmenková: a, b, c) a 1002 (písmena rozdílů tras: A, B, C) v `ZastavkoveJrMhdAPI`. Texty popisují, **kde spoj končí** (zkracuje trasu), ne **kam pokračuje** (prodlužuje).
- **✅ VYŘEŠENO PRO AD** (2026-06-21) - `ZastavkoveJrAd.RozdilyTras` větev prodloužení za vzor nově
  vypisuje **„jede až do zast. <konečná spoje>"** místo legacy matoucího „pokračuje přes <mezizastávky>"
  (znění A dle dohody - klient chce vědět KAM dojede). Záměrná odchylka od legacy (legacy „pokračuje"
  bylo špatné - to je důvod ticketu, proto bez gold oracle; kód analogický k ověřené větvi „jede jen do").
  **Pozn.:** prodloužení za základní trasu je v datech **vzácné** (J-věta #10 = obvykle nejdelší trasa);
  ověřeno proti CB_CSADAD 0345061/0365001 že běžné rozdíly (neobsluhuje/jede jen do) fungují 1:1 a změna
  je nedotkla. „jede až do" se projeví na lince, kde spoj reálně jede za vzor.
- **⬜ Zbývá pro MHD agendu** (`ZastavkoveJrMhd`) - stejná úprava neportována (až bude potřeba).

#### Změřený stav 30. 7. - měření z 29. 7. bylo neplatné (sonda `Tests/AdProdlouzeniProbe.cls`)

Ověřováno na `CB_CSADAD`, skupina 1 (240 linek, 9693 stránek).

**Měření z 29. 7. bylo kontaminované chybou cache v samotném generátoru.** `WMapa` /
`WNastupisteMapa` cachovaly W-mapu pod klíčem `^||TiskAd("wmap",cisloTrasy)` **bez linky** -
a `cisloTrasy` je číslo lokální v rámci linky. Sonda čte víc linek v jednom procesu, takže si linky
mapy navzájem přebíjely → cizí trasy vyráběly falešné rozdíly tras. **Opraveno** (linka je v klíči);
v produkci se to neprojevovalo, protože `GetData` jede jednu linku a cache na začátku maže.

Co z toho plyne pro tři „vady" evidované 29. 7.:

1. **Poznámky rozdílů tras se na stránku DOSTANOU** - původní vada 1 neexistuje. Doloženo:
   0365005 / Smrkovice / T má 6 textových poznámek (B, C, E, F, G, H), 0365001 / Vlakové nádraží / T
   jednu (B). Linka 0365003 z původní reprodukce žádné rozdíly tras nemá (po opravě cache
   `legendCelkem=0`) - „ztrácení" byl artefakt.
2. **Věta „jede až do zast. " bez cíle** byl týž artefakt (cizí bitmapa ukázala na pozici, kterou
   J-trasa nepojmenovává). Přesto **zpevněno**: nový `KonecnaNazev` vezme nejbližší pojmenovanou
   obsluhovanou pozici proti směru jízdy a bez známého cíle se věta nenapíše.
3. **„jede přes zast. X" + „jede až do" současně** - zkusili jsme „jede přes" u prodloužení vynechat,
   ale **měření to vyvrátilo a změna byla vrácena**: obě věty nesou různou informaci a po vynechání
   se texty různých skupin spojů staly identickými → dedup je sloučil do jedné značky. Doloženo na
   `CB_GWBUS` 501/0000001/T (viz níže): legacy tam má tři poznámky, po vynechání zbyla jedna.

#### ✅ Reprodukce nalezena - `CB_GWBUS`, skupina 501, linka 0000001, směr T

Hledání přes **všechny** skupiny a linky (ne jen skupinu 1): v `CB_CSADAD` 309 skupin / 31 216 linek /
62 432 kombinací linka×směr → **0 nálezů**, a to ze dvou nezávislých důvodů: 62 422 kombinací nemá
vzorovou trasu vůbec (fallback „union obsluhy", proti němuž nelze jet *za* vzor) a u zbylých 10, které
ji mají, je **vypnuté** auto-poznámkování. V téhle firmě tedy případ nastat nemůže.

V `CB_GWBUS` (2190 linek) ale ano - **12 výskytů** na okružní lince 0000001/T. Na stránce zastávky
Mikrotechna dorazí do legendy:

- `B` = „jede přes zast. Hlinecké sídl. , neobsluhuje zast. Dewetterova ul., **jede až do zast.
  Autobusové nádraží**"
- `C` = „jede přes zast. Hlinecké sídl., Poliklinika , neobsluhuje zast. Dewetterova ul., **jede až do
  zast. Autobusové nádraží**"

Věta má cíl, dorazí na stránku, má značku i řádek legendy → **funkčnost #7947 je ověřená**.

**✅ Skupiny spojů srovnány s legacy (oprava `|`).** Nejdřív jsme na té stránce měli **dvě** rozdílové
poznámky proti legacy **třem** a lišily se i seznamy zastávek. Příčina: `SpojBitmapa` počítala značku
`|` („zastávka je na trase, ale spoj v ní nestaví") jako obsluhovanou, zatímco `CasHodnota`
i `ObsluhovanePoradi` ji správně vylučují. Důsledky byly dva: falešné „jede přes zast. X" u zastávek,
kde spoj nestaví, a slévání skupin spojů, které se lišily právě jen `|`. Po opravě je grouping **1:1
s legacy** (B/C/E, tytéž seznamy, tatáž věta „neobsluhuje zast. Dewetterova ul.") - jediný rozdíl je
dohodnuté znění konce věty. Zlaté digesty se nezměnily (zlaté linky `|` nemají), CB_CSADAD beze změny,
v CB_GWBUS správně přibyly rozlišené skupiny (60 → 66 řádků legendy).

**✅ Znění dohodnuto a nasazeno (rozhodnutí Jan 30. 7.):** cíl jde **na začátek věty** a nese
**průjezd** - `jede až do zast. <konečná> přes <mezizastávky>`, pak teprve „jede přes" / „neobsluhuje".
Dva důvody: informace, kvůli které tiket vznikl, nesmí být na konci věty (legenda ořezává konec), a bez
průjezdu splývaly skupiny spojů lišící se jen cestou. Výsledek na ověřovací stránce:

- `B` „jede až do zast. Autobusové nádraží **přes Náměstí**, jede přes zast. Hlinecké sídl., Jiráskova
  ul., Milevská ul., Sokolovna, Poliklinika"
- `C` „jede až do zast. Autobusové nádraží **přes Náměstí, Hlinecké sídl.**, jede přes zast. Sokolovna,
  Poliklinika , neobsluhuje zast. Dewetterova ul."
- `E` „jede až do zast. Autobusové nádraží **přes Náměstí**, jede přes zast. Hlinecké sídl., Jiráskova
  ul., Sokolovna, Poliklinika"

Informačně tedy 1:1 s legacy (tytéž tři skupiny, tytéž zastávky), odchylka je pouze ve znění. **Bez
prodloužení se nemění nic** - pořadí i oddělovače (vč. legacy tvaru „ , " před „neobsluhuje") zůstávají
byte-identické, zlaté digesty beze změny.

**⬜ Chybí zdroj atributů zastávky (samostatné, blok `B16`):** na téže stránce má legacy řádek
„x = zastávka pouze na znamení", náš výstup ne. **Doměřeno:** není to filtr ani rozsah zastávek -
na té lince nemá „x" **žádná** zastávka v J-větě (jediné atributy jsou `TS` na nádraží). Legacy ho
bere odjinud; ve W-záznamu trasy jsou vidět atributy **per spoj** (např. `#7:6:1::V` = jen výstup),
které nečteme vůbec. Chybí nám tedy celý zdroj. Pozor i na znění: legacy „zastávka pouze na znamení"
× naše „na znamení".

**Nový nález z auditu - kolize značky (nezavírat s tiketem):** písmenná sada rozdílů tras (default
`BCEFGHIJOQRWeijqsvwxz`) obsahuje **H** a **F**, což jsou zároveň značky bezbariérových spojů →
na stránce se stejný symbol objeví ve dvou významech a legenda má dva různé řádky pod jedním znakem.
Změřeno na **9 stránkách** (0365005, 0365006), např. „H = spoj má bezbariérově přístupné vozidlo"
+ „H = neobsluhuje zast. Obchodní, jede jen do zast. Autobusové nádraží". MHD tuto třídu chyb nemá -
přiděluje písmena ze sdíleného poolu stránky a už použitý znak přeskočí. Oprava v AD je snadná
(vynechat obsazené znaky), ale **změní písmena na gold lince 0365005** → čeká na rozhodnutí, jestli
jít proti legacy paritě a přebaselinovat.

**Inventura zdrojů poznámek (240 linek / 9693 stránek, po opravě cache):**

| Zdroj (kategorie) | Značky u minut | Řádky legendy | Dorazí? |
|---|---|---|---|
| Datumová omezení (`omezeni`) | 24 062 | 14 924 | ✅ |
| Bezbariér spoje (`bezbarier`) | 17 490 | 4 546 | ✅ |
| Týdenní platnost (`tydenni-platnost`) | 12 299 | 7 536 | ✅ |
| Rozdíly tras (`rozdil-trasy`) | 946 | 190 | ✅ (jen 5 linek) |
| Atributy zastávek (`atribut-zastavky`) | 0 (záměr - symbol u zastávky) | 3 788 | ✅ |
| Bezbariér souhrnně (`bezbarier-agregat`) | 0 (záměr - bez značky) | 1 058 | ✅ |
| Ruční komentáře spoje (`rucni-komentar`) | 0 | 0 | ⬜ nelze ověřit - firma nemá data (0 linek s `SeT_SpeC`/`MhD_SpeC`) |
| Směrové poznámky linky (`smerove`) | 0 | 0 | ⬜ nelze ověřit - 0 linek s `SeT_MhD` `>`/`<`/`<>` |
| Písmena variant tras (`varianta-trasy`) | 0 | - | ⬜ opt-in parametr, default vypnutý |

**Křížová kontrola úplnosti prošla:** 0 případů „značka u minuty bez řádku legendy" napříč všemi
9693 stránkami. Rozsah „Spoje, nepoznámkovat" (`MHDSPNPZN`) nemá v této firmě data → parametr
„V rozsahu nepoznámkovat schovat" je zde no-op (proto signál o schovaných poznámkách počítá
skutečně skryté položky, ne zapnutý parametr).

### D2. Vlastní komentář spoje musí přebít auto-poznámku (ne paralelně tisknout)
- **Zákazník:** TRANSDEV (Michal Podloučka), #7955
- **Příklad:** Linka 402, spoj 20 jede jen v části trasy. Auto generuje „spoj jede pouze do zastávky XY". Klient chce vlastní text „ze zastávky XY pokračuje jako linka X do zastávky YX".
- **Pokus klienta:** zapnutí „Komentáře" → funguje, doplní písmeno + text. Současně ale nezmizí auto-poznámka.
- **Pokus o vypnutí přes „Spoje, nepoznámkovat":** chová se opačně - nechá auto-poznámku, schová vlastní komentář.
- **Citace:** *„Měl jsem za to, že když mám zapnuté automatické poznámkování, u vybraných spojů si v Komentářích zapnu vlastní poznámkování, a zadám do Spoje, nepoznámkovat stejné spoje a rozpětí Tč, budu mít u těchto spojů pouze svůj komentář. Toto mi ale teď nefunguje a je používána automatika a potlačen ruční komentář."*
- **Co máme dnes:** Auto-poznámka (rozdíly tras) a vlastní komentář se generují nezávisle ve fázi 6. Funkce „nepoznámkovat" má aktuálně sémantiku „vypnout vše kromě auto-rozdílů".
- **Co klient chce:** Pokud má spoj **vlastní komentář**, auto-poznámka rozdílů se pro tento spoj **NEgeneruje**. (Plus překlopit sémantiku „nepoznámkovat" - viz M-line interní odpověď: aktuální chování je dokumentované, ale neintuitivní.)
- **✅ VYŘEŠENO PRO AD agendu** (2026-06-20, commit `0b5838d`) - nový parametr sestavy
  **`SCHOVAT_V_ROZSAHU`** (Ruční komentáře [default] / Automatické (rozdíly tras) / Obojí). V rozsahu
  „Spoje, nepoznámkovat" volba „Automatické" schová auto-rozdíl trasy a nechá ruční komentář = přesně
  klientův požadavek. Řešeno **modelem nadmnožina + render-time TD filtr** (`NoteVisibilityFilter`),
  ne auto-suppress v Caché - legacy sémantika „nepoznámkovat" zachovaná (default), flip je opt-in volba.
  Viz `ad-data-mapa.md §5` + `feature-design-principles.md §5.2`.
- **Pozn. k MHD agendě:** tyto ruční poznámky jsou **AD-only** - `^JRL` konstrukty (`SeT_SpeC`/
  `MHDSPNPZN`/`SeT_MhD`). MHD agenda (`ZastavkoveJrMhd`) čte jiný zdroj (`^ALVA.MHD.*`) a tyto pole
  **nemá** (ověřeno 2026-06-20). Není co portovat, dokud se nepotvrdí, že MHD modul má vlastní
  ekvivalentní úložiště (dle kolegy pravděpodobně ne - nastavuje se jen v AD).

### D3. Bug - „Spoje, nepoznámkovat" maže i inverzní znaménko (NZ)
- **Zákazník:** TRANSDEV (Michal Podloučka), #7955
- **Citace:** *„obecně Spoje, nepoznámkovat ruší i označení inverzním znaménkem, což nevím, zda je záměr."*
- **Co máme dnes:** Funkce „nepoznámkovat" patrně omylem maže i `spojNZ` přidělení.
- **Gap:** Bug - NZ je v jiném datovém toku než ostatní poznámky, „nepoznámkovat" se ho dotýkat nemá. Opravit v 6. fázi `ZastavkoveJrMhdAPI`.
- **✅ V AD agendě bug NEvzniká** (2026-06-20) - `NepoznamkovatSpoj` + filtr se dotýká jen kategorie
  `rucni-komentar` / `rozdil-trasy` (dle volby); `omezeni`/`bezbarier` (kam patří NZ-style značky)
  filtr nikdy neschovává. Zůstává relevantní jen pro MHD agendu (legacy datový tok).

### D4. Bug - výběr spoje jen prvním číslem
- **Zákazník:** TRANSDEV (Michal Podloučka), #7955
- **Citace:** *„Výběr mezi spoji není možný zadáním celého čísla spoje, jde pouze první číslo."*
- **Co máme dnes:** UI vstup pro filtr spojů akceptuje jen jednociferné číslo.
- **Gap:** UI bug v EDISONu (Studio JŘ), ne přímo v TD ani v Caché API. Zaznamenáno - předat dál.

### D5. Skrýt poznámku „x - zastávka je na znamení", pokud sloupec se značkou je hidden
- **Zákazník:** TRANSDEV (Vladimír Šimko), #7161
- **Citace:** *„aby sa negenerovala poznámka »x - zastávka je na znamenie«, ak je v zostave zakázané zobrazenie stĺpca »Pracovní 2« (zaisťuje zobrazovanie značky »x« pred názvami príslušných zastávok)"*
- **Co máme dnes:** Atribut zastávek se vždy přidá do legendy přes `AddAtribut` ve fázi 6 - bez ohledu na to, zda je sloupec s atributem v šabloně viditelný.
- **Co klient chce:** Pokud sloupec, který zobrazuje značku `x` (v TD ekvivalent: `ShowSignalStopIndicator` na StopListu), je vypnutý → vynechat odpovídající popis z legendy.
- **✅ VYŘEŠENO PRO AD** (2026-06-21) - `NoteVisibilityFilter.ShouldHideNaZnameniLegend(template)`:
  když šablona má StopList, ale **žádný** nemá zapnuté `ShowSignalStopIndicator` („Na znamení"),
  filtr skryje řádky legendy kategorie `atribut-zastavky` („× na znamení"). Legenda automaticky
  následuje viditelnost značky - žádné nové ovládání (uživatel jen odškrtne „Na znamení" na StopListu).
  Žádná Caché změna (legenda už je tagovaná). **Test:** CB_GWBUS **0320144** (má „x - zastávka pouze
  na znamení") - odškrtnout „Na znamení" na StopListu → značky „×" i řádek legendy zmizí.
- **✅ MHD agenda** (2026-07-15) - značka fungovala přes sdílený element už dřív; skrytí legendy
  doplněno tagem kategorie v `ZastavkoveJrMhd.BuildPoznamka` (řádek „na znamení" = `atribut-zastavky`,
  rozpoznán dle popisu). Sdílený `NoteVisibilityFilter` ho schová stejně jako v AD. MHD gold
  re-baselinováno (jen md5, additivní tag). Rozpoznání „na znamení" je popisové → symbol se mezi
  agendami/firmami liší (AD „x", MHD „D"), popis „na znamení"/„na znamenie" je konzistentní.

### D6. Vypnutí auto-poznámky „všechny spoje s bezbariérovým vstupem"
- **Zákazník:** TRANSDEV (Vladimír Šimko), #7161
- **Citace:** *„Možnosť používateľsky nastaviť, aby sa automaticky negenerovala poznámka »všetky spoje s bezbariérovým vstupom do vozidla«."*
- **Co máme dnes:** `AddAtribut` v `ZastavkoveJrMhdAPI` zřejmě generuje shrnující text, když 100% spojů má daný atribut (např. bezbariérovost). Vypínání není.
- **Co klient chce:** User option v parametrech tisku - „negenerovat agregovanou poznámku" / per-atribut switch.
- **✅ VYŘEŠENO PRO AD** (2026-06-21) - parametr sestavy **`SCHOVAT_AGREGAT_BEZBARIER`** (bool). Agregát
  „všechny spoje mají bezbariérově přístupné vozidlo" (vzniká při `allBBP` = 100 % spojů bezbariér) je
  v legendě tagován kategorií `bezbarier-agregat`; TD `NoteVisibilityFilter` ho při zapnutém parametru
  skryje. Per-spoj „spoj má bezbariérově…" zůstává. Filtr-model jako #7955.
  **✅ Ověřeno na CB_CSADAD 0365004** (2026-06-21) - má agregát „všechny spoje mají bezbariérově přístupné
  vozidlo"; zapnutí parametru řádek korektně skryje, per-spoj bezbariér (0365001/0365005) zůstává.

### D7. Skrýt prázdné přípojové řádky ➖ přeřazeno k linkovým JŘ
> **Stav (2026-06-30):** přípoje/návaznosti **nejsou koncept zastávkového JŘ** - patří k linkovým
> JŘ (konzistentní s AD-N14). Řeší se až s agendou linkových JŘ, ne v zastávkovém. Ponecháno zde
> jen jako evidence tiketu #11300.
- **Zákazník:** KORID (Jaroslav Smištík), #11300
- **Citace:** *„zobrazujeme přípoje v příbojových řádcích, občas jsou uzly, kde je provoz celotýdenní, ale přípoje jsou jen v PD nebo naopak jen o víkendech, uvítali bychom, kdyby byla možnost prázdný přípojový řádek (v situacích kdy je PD a víkend na zvláštní tabulce/listu) netiskl a šetřil místo."*
- **Z M-line reply:** „Aktuálně není možné skrývat prázdné přípojové řádky. Pro tuto možnost bude nutné upravit tisk JŘ (...) jeho řešení pravděpodobně spojíme s novým tiskovým modulem pro JŘ" (2025-12-08, Jiří Bžoch).
- **Co máme dnes:** Přípojové řádky **nejsou** v TemplateDesigneru implementované - je to legacy fička.
- **Co klient chce:** Přípojové řádky obecně + filter na neprázdné. Pokud zastávka má v dané vývěsce (skupina dnů = PD nebo víkend) prázdný přípojový seznam, nevykreslit řádek.
- **Gap:**
  - Nová doménová struktura: `Pripoj` (data per uzel × skupina dnů) v `ZastavkoveJrMhdAPI.GetData`.
  - Nový element v TD (např. `ConnectionsListElement`) s `HideEmptyRows: true`.

---

## E. Návrhář sestav (TemplateDesigner UI)

*(Žádné aktivní položky - E1 přesunuto do §G Hotovo.)*

---

## P. Nové požadavky z prezentací zákazníkům

> Zaznamenáno z živých prezentací/seminářů (ne z helpdesk tiketů). Neanalyzováno - evidence, že se na to zákazníci ptali. Před implementací založit/dohledat ticket a ověřit přesné zadání.

### ~~P1. Poznámky ručně do patičky~~ ❌ Vyřazeno (2026-07-10)

**Neodbavovat** - dohoda: nejasné přesné zadání (volný `TextElement` v Footer už funguje; jestli chce klient
dedikované pole „Poznámky", není potvrzeno). Až přijde konkrétní požadavek/ticket, znovu založit.

### ~~P2. Sloučení více linek se stejným posledním trojčíslím~~ ❌ Vyřazeno (2026-07-10)

**Neodbavovat zde** - dohoda: řeší se jinde (mimo tuto agendu / úroveň, ne v rámci zastávkového JŘ dispatch).

### P3. Logická imposice - 2× A5 na jednu A4 ✅ HOTOVO v1 (2026-07-12)

- **Zdroj:** prezentace zákazníkům 2026-06-24; požadavek: N-up (2×A5 → A4), ideálně i duplex/brožura.
- **Řešení (zvolena varianta C - sheet layout vrstva):** `Models/Layout/SheetLayout.cs`
  (`SheetComposer` čistá logika), exportní pole v tiskovém dialogu „Uspořádání na list"
  (Vypnuto / A4 / A3 × orientace) + mřížka AUTO (největší 1:1 fit, bez zmenšování) + „Každá
  stránka na celý list" (táž vývěska N× pro řezání) + ořezové značky; persistence per agenda;
  platí pro tiskový dialog i export z hlavního okna (PNG bez imposice). Detail → changelog 2026-07-12.
- **Vědomě v2 (s knižním JŘ):** brožurové řazení stran + duplex - PDF duplex nekóduje,
  přední/zadní zůstává na tiskárně.

---

## F. Co je v TemplateDesigneru NAVÍC oproti legacy

Při komunikaci se zákazníky uvádět jako benefity nového řešení:

- **CellStyle kaskáda** - `column.Style → element.HeaderStyle/DataStyle → element defaults → CellDefaults`. Vše viditelně v dialozích sloupců s tri-state „dědit / přepsat" stylingem (placeholder zděděné hodnoty).
- **Per-column** styling napříč StopList / DepartureGrid / NotesLegend / TableElement.
- **WYSIWYG editor** s live preview a okamžitým náhledem mock-dat (per agenda).
- **Multi-data PDF export** - jeden tisk vygeneruje N PDF stránek (1 per zastávka × směr).
- **AI Vision import pipeline** - automatické rozpoznání struktury z existujícího PDF JŘ.
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
  - Pre-create `groupHeaderStyles2/Fonts2/TextPaints2` cache - vytváří se jen pokud `HeaderLine2Style != null` (žádná overhead při default).
  - UI: nový group **„Záhlaví - 2. řádek"** v properties panelu s 4 položkami (Velikost, Tučné, Kurzíva, Barva textu). Lazy-init helper `EnsureHeaderLine2Style()`.
- **Default chování zachované:** `HeaderLine2Style = null` → `hasLine2Override = false` → Line 2 použije stejný styl jako Line 1, žádná regrese pro existující šablony.
- **Pattern:** plně extensible přes TextStyle cascade - uživatel může v budoucnu doplnit per-column override Line 2, HAlign, padding atd. bez schema změny.

### A2. Konfigurovatelná pozice značek u zastávek (StopList)
- **Tiket:** #10660 (DPKV, Jan Slavík); zůstává **suspended** (řešíme i CBDP layout).
- **Datum:** 2026-05-12
- **Diagnóza:** Stávající TD renderoval značky (na znamení `×`, atributy, poznámky) vždy **za názvem** zastávky (CBDP layout). DPKV chce na znamení jako **prefix** před názvem (např. „× Globus") + chtějí, aby názvy zůstaly **zarovnané** i u zastávek bez značky.
- **Implementace:**
  - Nový enum `StopMarkersSide { AfterName, BeforeName }` + property `MarkersPosition` (default `AfterName` = CBDP layout).
  - Když `BeforeName`: render rozdělí sloupec zastávek na **2 sub-cells**:
    - **Marker sub-cell** (úzký, right-aligned) - značky
    - **Name sub-cell** (zbytek šířky, left-aligned) - pouze název
  - Šířka marker sub-cell je **auto-computed** podle nejširšího marker textu v datech přes `SKFont.MeasureText` + padding `MarkersSubCellPaddingMm = 1.0mm`. Pro běžné single-char markery (`×`, `D`) ~2.5mm.
  - Total width sloupce zastávek se nemění → žádný overflow.
  - Všechny 3 typy značek (na znamení + atributy + poznámky) se přepínají najednou jedním přepínačem. Per principles - minimum, per-marker per request later.
  - UI: `EnumPropertyDescriptor` „Pozice značek" v group „Zastávky" s labely „Za zastávkou" / „Před zastávkou".
- **Architektonický learning:** první návrh prepended markery do textu buňky (`„× Globus"`). To posouvalo názvy zastávek per řádek podle přítomnosti značky → nezarovnané. Druhá iterace s fixed 4mm sub-cell přidala vzdušnost ale byla zbytečně široká. Třetí (final) s auto-fit measure se přizpůsobí datům přesně. **Symetricky paralelní s A1 (DirectionArrowSide) - bounded enum, ne template variant.**

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
- **Architektonický learning:** první návrh přidával gutter MIMO sloupec → způsobilo overflow Width elementu (clip ořezal arrow). Druhá iterace dala arrow UVNITŘ column → na Vlevo to kolidovalo s left-aligned textem. Třetí (final) iterace asymetricky - Vpravo inside, Vlevo outside - funguje bez overflow i bez kolize s textem. **Per-customer divergence řešena přes property, ne template variant** (per `feature-design-principles.md` §5 - 2 bounded hodnoty, ne kombinace settings).

### A3. CurrentStopBackgroundColor + cleanup master toggle
- **Tiket:** #10660 (DPKV, Jan Slavík); zůstává **suspended**.
- **Datum:** 2026-05-12
- **Výsledek:** ✅ `CurrentStopBackgroundColor` property + render path + properties panel descriptor („Pozadí aktuální") v group „Zvýraznění" - vše už existovalo. Customer use case (podbarvení místo podtržení) je plně podporovaný - uživatel vypne `CurrentStopUnderline` a nastaví barvu `CurrentStopBackgroundColor`.
- **Bonus cleanup:** Odstraněn master toggle `HighlightCurrentStop` (matoucí UX - vypadal jako sourozenec sub-options, ale choval se jako rodič). Highlight se teď aktivuje implicitně podle toho, jestli je některá z highlight properties nastavená. Šipka směru jízdy teď navíc vždy ukazuje na aktuální zastávku (dřív závisela na master toggle).
- **Minor regression:** Staré šablony s explicitně vypnutým `HighlightCurrentStop=false` po načtení dostanou defaultní Bold+Underline (protože System.Text.Json ignoruje smazaný field). Per principles („no backward compat shims") neřeším migrací.

### A4. Per-column header font ve StopList - již plně podporováno
- **Tiket:** #10660 (DPKV, Jan Slavík); zůstává **suspended**.
- **Datum ověření:** 2026-05-12
- **Výsledek:** ✅ Funkčně pokryto. Klient nenašel UI cestu - funkce existuje, jen je v `StopColumnsEditorDialog` (tab „Sloupce") přes per-column `CellStyleEditor`, ne v properties panelu.
- **Code path:** `StopListElement.ResolveHeaderStyle(col)` aplikuje `col.Style.Header.Resolve(elementResolved)` → per-column přebíjí element-level v 4-vrstvé kaskádě. `RenderHeader` vytváří `ColumnRenderer.CellSpec` s **per-column ResolvedTextStyle**, takže každý sloupec má vlastní FontSize.
- **Tri-state placeholder** ✅ - sloupec bez override zobrazí šedý italic placeholder hodnoty zděděné z elementu; „×" reset jen při explicit override.
- **UI test ověřeno:** dialog sloupců → per-column FontSize → různé velikosti záhlaví v náhledu.

### B2. Underline a Strikethrough v záhlaví skupin dnů (DepartureGrid)
- **Tiket:** #10660 (DPKV, Jan Slavík); zůstává **suspended**.
- **Datum:** 2026-05-12
- **Diagnóza:** V `DepartureGridElement.cs:626` se text headeru skupiny dnů renderuje přes `Canvas.DrawText` napřímo, **bez** volání `DrawTextDecorations`. Důsledek: `HeaderStyle.Underline = true` ani `Strikethrough = true` v UI nic neměnily - decorations se nevykreslovaly vůbec. Bold/Italic fungovaly, protože jdou přes SKFont.
- **Customer complaint vs. reality:** DPKV v legacy systému viděl podtržení s nestejnou tloušťkou. V novém TD by viděli žádné podtržení. Fix pokrývá oba symptomy.
- **Fix:** 1 řádek za `DrawText` - volání `hs.DrawTextDecorations(ctx.Canvas, pt.X, pt.Y, textW * ctx.MmToPx, hFont, hTextPaint)`. Tloušťka linky je automaticky konstantní napříč řádky, protože `hFont` je sdílený a `metrics.UnderlineThickness` je per-font deterministická.
- **Otestováno:** Manuálně v běžícím TD - Underline + Strikethrough nyní fungují na single-line i multi-line headeru.
- **Pozn.:** Stejný pattern (`DrawText` bez `DrawTextDecorations`) je na 5 dalších místech v souboru (minuty 780, anotace 803, hour header 899/901, designer placeholder 886/888). **Záměrně neopraveno** - nedotčeno reálným klientovým use casem; opravit až přijde konkrétní request.

### B3. Rozsah hodin přes půlnoc + sjednocené parametrové UI (DepartureGrid)
- **Tikety:** #10660 (DPKV, Jan Slavík) + **#9036 (TRANSDEV, Vladimír Šimko - MHD Trenčín, nočné spoje 22-5h)**; oba zůstávají **suspended**.
- **Datum:** 2026-05-12
- **Tiket #9036 - explicitní pokrytí:** TRANSDEV potřeboval pro MHD Trenčín (provoz cca 22-5h) časovou osu „cez polnoc" (22, 23, 0, 1, 2, 3, 4, 5). Naše implementace **přesně toto** umí - `TimeAxisMode = Range`, `HODINY_OD = 22`, `HODINY_DO = 5` → wrap přes půlnoc s extended hours (data hodina 24-29 = pokračování). Spoje se přiřazují do sloupců podle `TypProvozuID` (skupiny dnů) beze změny - což TRANSDEV výslovně přijal („v noci z piatka na sobotu spoje pred polnocou budú obsiahnuté v stĺpci pracovný deň a spoje po polnoci v stĺpci sobota, nedeľa").
- **Diagnóza:** Stávající `RangeHoursWithWrap` clampovalo `to ≤ 23` a vůbec neumělo wrap přes půlnoc. Caché používá **extended hours konvenci** (data hour 24..47 = pokračování přes půlnoc následujícího provozního dne) - z fáze 4 `PromitnutSpoje` přicházejí kumulativní hodiny po chronometráži (např. spoj startuje 23:30, jede 90 min → poslední zastávka má `hh=25, mm=00`). Nová .NET pipeline ale očekávala 0..23 → noční odjezdy „mizely" v rozsahu 4→3.
- **Implementace:**
  - `TimeAxisMode` rozšířen o `DataBounded` (souvislá od první do poslední data hodiny - předtím chybělo). Plný den vědomě odebrán z UI (lze docílit přes Vlastní rozsah 0..23).
  - `RangeHoursExtended(from, to)` (přejmenováno z `RangeHoursWithWrap`): když `to < from`, vrací **extended hours** `[from..23, 24..to+24]`. Tj. 22→3 = `[22, 23, 24, 25, 26, 27]`.
  - `DrawHourDataCell` rendruje header label jako `H % 24` (data hour 27 → „3"). ByHour lookup zůstává raw - Caché dataset s extended hours se napáruje 1:1.
  - Non-Range filter (Compact/DataBounded/Full + HODINY_OD/DO): když to < from, používá `[from..to+24]` místo `OR` filteru → data hour 27 projde filtrem 22→3.
  - `ParseTimeAxisMode` - 3-vrstvý backward compat (popisné CZ + legacy CZ + EN názvy z předchozích iterací).
  - Caché `AddStandardJRParameters`: české popisné labely pro CASOVA_OSA („Jen hodiny s odjezdy" / „Souvislá od první do poslední" / „Vlastní rozsah"); HODINY_OD/DO mají nově `visibleWhen = "CASOVA_OSA=Vlastní rozsah"`.
- **Nová infrastruktura `VisibleWhen`:**
  - `PropertyDescriptor.VisibleWhen: Func<bool>?` (in-process, lazy) - `PropertiesPanel` filtruje skryté řádky, po commitu rebuilduje panel pokud existuje aspoň jeden VisibleWhen.
  - `TemplateParameter.VisibleWhen: string?` (JSON-friendly „NAME=VAL") - `ParameterControlVM.IsVisible` + `UpdateVisibility()`, parser v `CacheAgendaProvider`.
- **Sjednocení parametrového UI:**
  - Nové `Controls/ParameterTemplates.xaml` (shared ResourceDictionary se 4 DataTemplates) + `ParameterTemplateSelector` ve vlastním souboru.
  - `ParameterControlVM.Init(target, parameters, values, onChanged, commitFactory)` - sjednocuje wiring (clear → loop → auto-refresh visibility → initial check) mezi MainWindow preview lištou a PrintDialog.
- **Klientova „auto-fit zvětšení řádků" část:**
  - Nová property `AutoExpandToFillHeight: bool` + Caché parametr `ROZTAHNOUT_NA_VYSKU` (default `false`, opt-in).
  - Když `Height` fixní + obsah se vejde + flag = true → `dataGrow = (Height − headerH) / dataNaturalH` aplikuje se proporcionálně na: data řádky, minute fonty, anotace symbol/text fonty, padding minutových buněk, `scaledMinuteW`/`scaledOdjezdGap`, hour data buňka. Header (skupiny dnů + „Hod") zůstává na původním scale.
  - Render kód rozdělen na 2 scale factory: `scale` (header) a `dataScale = scale × dataGrow` (data) - symetrický k existujícímu shrink, ale selektivně.
  - Edge cases: `Height==null` → ignorováno; shrink + AutoExpand současně → shrink wins.
  - Override priority (jako u CASOVA_OSA): designer property → PrintDialog parametr override per print job.
- **Architektonický learning:** první iterace (Option A: `hodina # 24` v Caché) by ztratila informaci „toto je následující den" → konflikt pokud spoj jede ve 3:00 ráno **tento den i následující den**. Final fix (Option B) zachovává raw service hour v ByHour klíči, mod-24 jen na header label → distinct columns pro distinct service hours, žádné collisions.

### C1. Barevné odlišení značky vs. text poznámky (DB-driven)
- **Tiket:** #10660 (DPKV, Jan Slavík); zůstává **suspended**.
- **Datum:** 2026-05-12
- **Citace:** *„Kdyby bylo možné barevně odlišit značku (písmeno) poznámky, zpřehlednilo by to celý zastávkový jízdní řád."*
- **Architektonické rozhodnutí - DB-driven místo designer SymbolStyle:** Původní plán byl přidat designer-level `SymbolStyle` property (statická barva v šabloně). Při průzkumu DB se ale ukázalo, že DPKV **už má barvy uložené** per-poznámka v `^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",ID,"TiskJRMHDVozoveBarvaZnakuRGB")` (a `TiskJRMHDVozoveBarvaPozadiRGB`) - historicky pro vozové JŘ. Přeneseme stejnou barvu i do zastávkového JŘ - žádné zdvojené nastavování per-šablona.
- **Implementace:**
  - **Caché:** `IntToHex2` + `RgbToHex` helpery (převod „R;G;B" → „#FFRRGGBB" pro SkiaSharp). `BuildPoznamka` rozšířen o `foreColor`/`backColor` parametry → JSON `FORE_COLOR`/`BACK_COLOR`. `AddAnotace` rozšířen analogicky (pro konzistenci legenda ↔ anotace v odjezdech). Color lookup z `PoznamkaSpoj` ENUMS na 4 místech (poznámky trasy, speciální znaky spoje, textové poznámky spoje - vždy stejný pattern).
  - **.NET:** `PoznamkaData` + `AnotaceData` rozšířeny o `ForeColor`/`BackColor` properties (nullable). Parser v `BuildPoznamky` + `BuildOdjezdyBySkupina` čte `FORE_COLOR`/`BACK_COLOR` z JSON. Render v `NotesLegend.RenderSymbolWithText` aplikuje barvu na znak (tělo zůstává default). Render v `DepartureGrid` aplikuje barvu na anotaci nad minutou (konzistence s legendou).
  - **NZ vyloučeno** - `NegativniZnacka` má vlastní invertovaný styl (`NegativniZnackaBackColor`/`ForeColor` v elementu), color override z DB se neaplikuje.
  - **BackColor přenášeno přes wire, ale render zatím ignoruje** - klient explicitně chce zatím jen barvu znaku. Připraveno pro budoucí rozšíření bez schema break.
- **Stav v aktuálních DPKV datech (KV_DP):**
  - 1071 záznamů `PoznamkaSpoj`, z toho 69 má barvy (1003-1071) ale `TiskJRMHDZastavkove=0` (jen vozové). Záznamy 1001-1002 mají `TiskJRMHDZastavkove=1` ale žádnou barvu.
  - **Žádný současný záznam nemá současně zapnutý zastávkový tisk AND barvu** - infrastruktura připravená, klient si nastaví v DB.
  - Quick test pro ověření: dočasně `s ^ALVA.MHD.ENUMS("KV_DP","PoznamkaSpoj","DTA",1002,"TiskJRMHDVozoveBarvaZnakuRGB") = "255;0;0"`, najít linku se spojem používajícím 1002, render → znak „W" červený v legendě i nad minutou.
- **Co bylo doprovodně opraveno:** `<SUBSCRIPT>` pád v iteraci `^||TiskMHD("trasa",SmerID,...)` při linkách se sloučenými zastávkami (např. linka 1073). Příčina: `SloucitTotozneZastavky` zapisovala pomocný klíč `"slouceniInv"`, ale 7 iterací jen skipovalo `"slouceni"`. Fix: sjednoceno na `i 'BodID continue` (numeric coercion filtruje všechny ne-numerické subscripty, future-proof).
- **Auto-generované poznámky** (rozdíl tras 1001/1002 - „končí v zast. X", týdenní platnost) zůstávají default černé. Klient explicitně nepožadoval; pokud někdy přijde request, bude to drobné rozšíření (per-linka color v `LSO` nebo per-algorithm static).

### C2. Hlavička sekce poznámek (NotesLegend) - již plně podporováno
- **Tiket:** #10660 (DPKV, Jan Slavík); zůstává **suspended**.
- **Datum ověření:** 2026-05-12
- **Výsledek:** ✅ Funkčně pokryto. `NotesLegendElement` má `HeaderText` property (default „Poznámky") + volá `RenderSectionHeader` (background + text + linka pod) - stejný pattern jako StopList. UI exponuje text v properties panelu (group „Záhlaví"), barvy pozadí + textu přes `GetCellZoneBaseColorDescriptors` (skupina „Záhlaví"). Klient si může nastavit libovolný text + barvy v designeru.
- **Diagnóza klientovy zmatky:** DPKV šablona `kv.json` má `HeaderText = "na znamení"` (omylem nastaveno z dřívější iterace, kdy NotesLegend obsahoval jen tu poznámku). Po opravě na „Poznámky" header funguje jak DPKV chtěl.
- **Code path:** `NotesLegendElement.Render:66-67` → `RenderSectionHeader` v `ScheduleElementBase`. Property descriptor v `GetPropertyDescriptors` (Label = „Nadpis", Group = „Záhlaví").

### E1. Podmíněný tisk sekcí - VisibilityExpression
- **Tiket:** #8772 (TRANSDEV, Jakub Holík); zůstává **suspended** (interní rozhodnutí, ne uzavřen klientovi).
- **Datum ověření:** 2026-05-11
- **Výsledek verifikace:** ✅ Funkčně pokryto. `VisibilityExpression` na sekci i elementu se vyhodnocuje na 4 místech v `PageLayoutEngine` (elementy, sekce, page headers, page footers). `ExpressionEvaluator` podporuje `==`, `!=`, `>`, `<`, `>=`, `<=`, `&&`, `||`, `!`, numerické i string porovnání s auto-detekcí, závorky, literály (`"..."`, čísla), placeholdery `{POLE}` plus `{PAGE}`, `{TOTAL_PAGES}`. Live validace v UI přes `Validate()` v PropertyDescriptoru, AST cache, fail-open runtime.
- **Pokrytí klientova seznamu polí** ze schématu `ZastavkoveJrMhdAPI.GetSchema`:
  - ✅ `CISLO_LINKY`, `TYP_JR`
  - ⚠️ „typ linky" - částečně přes `LOKALNI_OZNACENI_LINKY`
  - ❌ `KOD_DOPRAVY` (CIS) chybí, ale v praxi nahraditelné `POPIS_DOPRAVCE`
- **Interní status note** (sepsáno 2026-05-11): kompaktní popis featury, syntaxe a možných rozšíření. Tiket zůstává suspended - na rozšíření čekáme až bude konkrétní potřeba od klienta.
- **Možná rozšíření** (až bude konkrétní request, ne preventivně): doplnění chybějících placeholderů, `IN` operátor, string funkce (`CONTAINS`, `STARTSWITH`), pattern matching, aritmetika, date typování v `Compare()`, `IS NULL` test.

---

## TR. TRANSDEV - konsolidovaná sada požadavků (2026-07-03, k analýze)

Souhrnný wishlist k zastávkovému CP (SK legislativa + praxe MHD Trenčín). **Neanalyzováno** - před
implementací dohledat/založit tikety + upřesnit. U každého poznámka o stavu/infra na TD straně:

1. **Časová os cez polnoc** (noční linky 22→5h) nebo aspoň manuální přeuspořádání hodin.
   → ✅ **Už hotové (B3, #9036)**: `TimeAxisMode=Range`, `HODINY_OD=22`/`HODINY_DO=5` → wrap přes půlnoc
   (extended hours). **Klient nejspíš netestoval na své verzi** - ověřit u něj, netřeba implementovat.
2. **Zastávky na znamení - „všechny"**: když jsou VŠECHNY zastávky na znamení, místo značek u každé
   vygenerovat souhrn „× všechny zastávky linky jsou na znamení" (analogie k bezbariér souhrnu). Konfigurovatelné
   (CZ legislativa může chtít značky). 🟡 **IMPLEMENTOVÁNO 2026-07-10 (AD, opt-in), čeká SK ověření:**
   - Opt-in param `SOUHRN_NA_ZNAMENI` (default off = per-zastávka × jako dnes). Když všechny obsluhované
     neprojeté zastávky mají „x" → potlačí per-zastávka × + agregátní řádek legendy. Mechanika 1:1 jako
     bezbariér `allBBP`. Čistě Caché (AD-only; MHD-port později, bezbariér agregát je taky jen AD). Gold-safe (33/33).
   - ✅ **Kódování potvrzeno „x"** (kolega 2026-07-10 + naše CB_CSADAD data) → detekce je **správná**,
     feature na reálných SK datech bude fungovat. `SKNI_MHDNITRA` má ale **0× „x" ve 27985 záznamech**
     (prohledána celá J-věta, ne jen dta2) = **stará/nekompletní data** (žádné na-znamení nastavené), NE jiné kódování.
   - ⏳ **Zbývá jen ukázka spuštění agregátu:** potřebuje linku, kde jsou VŠECHNY zastávky „x" (vzácné -
     ani CB_CSADAD nemá all-„x" linku). Stejná datová řídkost jako TR3; ověří se na reálné all-request-stop
     lince u zákazníka. Feature je jinak korektní a opt-in gold-safe.
3. **Zobrazení nástupiště**: nový sloupec vedle názvu zastávky (SK legislativa). ✅ **HOTOVO 2026-07-10:**
   per-zastávka `NASTUPISTE` v kolekci ZASTAVKY - **AD** z J-věty `#3` (`nastTam:nastZpet` dle směru),
   **MHD** z `Oznacnik` (`ALVA.MHD.ZST`). Nový `StopFields.Nastupiste` + StopList sloupec „Nástupiště"
   (default skrytý = opt-in). Bez mocku - kde označník není, prázdné (standardně jen terminály/uzly).
   Ověřeno reálně na CB_CSADAD **0320680** (nástupiště na 6 zastávkách). Gold re-baseline AD 2 + MHD 3.
4. **Výška řádku „zastávka" - auto (3. režim)**: pevná výška, při nedostatku místa se začne zmenšovat.
   ✅ **HOTOVO 2026-07-10:** StopList pevná `DataRowHeight` se nově zmenší při přetečení pevné Height
   (dřív zůstala → přeteklo/ořízlo). Default (auto, `DataRowHeight` prázdné) beze změny. Realizováno
   sdíleným helperem `ScheduleElementBase.ScaledDataRowHeight(fontPt, factor)` (fixed-then-shrink pravidlo
   v jednom místě; sdílí StopList + DepartureGrid - dřív se rozešly, B2 vs StopList). Rendering-only, .NET 316/316.
   Plná unifikace řádkového layoutu (row-plan) = krok C.
5. **Definice směru/cíle** - položka „cíl" odkazovatelná v šabloně. 3 varianty: (min) univerzální cíl per směr;
   (střední) cíl textem per zastávka × směr v trase (jako nástupiště/sloupík); (ideál) DB cílů + id per zastávka×směr.
   Dnes hackují přes `informace`/`titulek`. NEW, datový model, M–L. *(Odbourá i speciální cíle pro tabule.)*
6. **Barva linky**: ve vlastnostech linky bg + text barva; v šabloně u objektu volba „farba linky" místo pevné
   z palety → aplikuje se při tisku. Šetří hodiny u grafických změn (MHD Trenčín). NEW, M.
7. **Jízdní doby - výběr sloupců variant**: víc variant trasy → víc sloupců jízdních dob; číslování dnes odráží
   pořadí výskytu variantu v seznamu spojů (nestabilní - Sloupec 3 na začátku = Sloupec 2 uprostřed). Chce
   explicitní kontrolu (které spoje / úsek řídí generování). → **Relates L1 multi-varianta TrasaMin** (`VARIANTY_TRAS`
   + `ShowAllRouteVariants`); je to ta „jemnější výběr variant (per-písmeno)" z `ad-data-mapa.md §10`. M.
8. **Jízdní doby - „<1" místo „0"**: nulová jízdní doba mezi zastávkami → zobrazit „<1" (méně než minuta) místo „0".
   Konfigurovatelné. ⏸️ **PARK - premisa nejasná (analýza 2026-07-10):**
   - TD (i legacy „Časy"/„Trasa min.") zobrazuje **kumulativní** čas od nástupní zastávky. 0-minutový úsek
     (dvě sousední zastávky stejný kumulativ, ověřeno na `CB_CSADAD` lin **0380520** var. T3: 1→1) se tiskne
     jako **opakované číslo** (1,1), **ne jako „0"**. Jediná „0" = řádek nástupní zastávky (ta zůstává 0).
   - **Per-segment sloupec** (Δ z předchozí zastávky, kde by 0-úsek dal „0") **TD nemá**. V legacy je kandidát
     sloupec **„Průběhy"** (název napovídá per-segment) - neověřeno.
   - **Blokuje:** potvrzení od TRANSDEV - screenshot, ve kterém sloupci „0" vidí. Bez per-segment sloupce není
     TR8 co implementovat (není kde „0" tisknout). Pokud chtějí per-segment sloupec → nová funkce (sloupec + „<1").
9. **Poznámkování spojů - obrácená logika**: dnes horní tabulka (značka/text/spoje) + spodní tabulka (kde
   NEpoznámkovat). Chce: rozsah TČ přímo v první tabulce vedle značky/spojů. Legacy-input workflow (tab „MHD").
   Zjednodušení, méně chyb. M (Caché-input strana).
10. **Zobrazování spojů - skrýt v So/Ne/svátky**: dnes „spoje s NZ se nezobrazí" jen pro sloupce školní
    výuka/prázdniny; chtějí i skrýt spoje o So/Ne/svátcích. → rozšíření filtru (`MHD - další`). NEW, S–M (Caché).

## H. Status komunikace s klienty

M-line zákazníkům slíbil:

- **„Nový tiskový modul" / „Studio JŘ"** - release **polovina 2026**.
- Citace z `2024-10-04` na #7937, #7947, #7955, #7959 (TRANSDEV): *„S ohledem na množství podnětů týkajících se specifických možností nastavení a chování tisku jízdních řádů pracujeme na vývoji nového nástroje v našem systému (...) nový nástroj bude uvolněn přibližně v polovině příštího roku."*
- DPKV (#10660) z `2025-07-10` (Dita Chabičovská): *„momentálně se musíme soustředit především na obsahové aktualizace - jak zastávkových, tak i knižních jízdních řádů. Současně ještě nemáme uzavřenou část projektu týkající se plánování směn (...) vrátíme se k němu po dokončení klíčových etap implementace MHD ve Vašem dopravním podniku."*
- KORID (#11300) z `2025-12-08` (Jiří Bžoch): *„úpravu již »bohužel« nestihneme udělat v letošním roce. Problému se ale budeme věnovat příští rok, jeho řešení pravděpodobně spojíme s novým tiskovým modulem pro JŘ."*

**Z toho plyne:** roadmapa TemplateDesigneru je **viditelný závazek** vůči TRANSDEV, KORID a DPKV. Při uzavírání jednotlivých tiketů odkazovat na konkrétní featuru v TD + verzi releaseu.
