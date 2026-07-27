# Roadmap

> **Stav k 2026-07-27.** **Jen otevřená / akční práce.** Cokoli hotového → [`roadmap-archive.md`](roadmap-archive.md),
> průběžný changelog → [`changelog.md`](changelog.md). Hotové věci sem nevracet — patří do archivu.

---

## 🎯 „Stanovišťový balík" DOKONČEN (2026-07-27) — zbývá HD odpověď + zavřít tikety

Celý balík hotový v1 (viz changelog + archiv):
- **KOVED #13048** — všech 5 bodů (zóny, přestupy, shrink-to-fit, nástupiště vč. **duplikace řádku**, varianty tras).
- **TRANSDEV #8828** — **vývěska per označník** (tiskový parametr „Rozdělit vývěsku per označník", server
  dělá N stránek + filtruje odjezdy per označník; gold beze změny, default vyp).

**Zbývá (papírové, zóna 5):**
- **Souhrnná HD odpověď** KOVED #13048 (5 bodů) + TRANSDEV #8828 — po ověření v buildu.
- **Zavřít tikety** #13048, #8828 (resolved).
- **Follow-up na vyžádání:** členění exportu na soubory per označník (dnes token `{NASTUPISTE}`);
  titulek stránky s označníkem; symbol „↓" u výchozí zastávky ve variantních sloupcích (drobný vizuál).

---

## 🧭 Jak podle roadmapy pracovat

Práce je rozřezaná na **bloky velikosti „jedno sezení"** (`B2`, `B4`, …) seřazené do **vln**. Každý blok
má **zónu** = oblast, kde si dvě sezení šlapou po prstech (viz „Paralelizace" níže).
Velikosti: **S** = do hodiny · **M** = půl dne · **L** = víc dní.

**Nová session:** vezmi nejnižší nezablokovaný blok z nejnižší nedokončené vlny; když běží jiná session,
zkontroluj zónu. Po dokončení: changelog + přesun bloku **kompletně** do archivu (ne odškrtnutý řádek zde).

**Tři pravidla, aby roadmapa znovu nenarostla:**
1. **Nic nevstoupí bez velikosti a čísla bloku** — volný text bez velikosti je přesně to, co se nahromadilo.
2. **Zákaznická položka má uzavírací podmínku** („co ověřit, aby se dala zavřít") — jinak zůstane „hotová, ale otevřená".
3. **Linkový JŘ má ukazatel `L‑1…L‑4`** (Fáze C+D) — velký kus rozřezaný na slotové kroky, aby v každé vlně postoupil.

---

## 🌊 Vlna 1 — vyčistit frontu

| # | Blok | Vel. | Zóna | Závisí na |
|---|---|---|---|---|
| **B2** | **Zavírací kolo helpdesku** — ověřit v buildu a zavřít #7947, #7955, #9036 + finální vizuál #13048 bodu 2; postrčit čekající (viz „Zákaznická fronta" níže) | M | 5 | aktuální publish |

Po vlně 1 je zákaznická fronta reálně **4 položky práce** místo dnešních dvanácti evidovaných.

---

## 🌊 Vlna 2 — zlevnit si další práci, rozjet linkový

| # | Blok | Vel. | Zóna | Závisí na |
|---|---|---|---|---|
| **B4** | **Sdílená base pole formulářů + deklarativní `visibleWhen`** — detail a dohodnutý směr v §B4 níže. První výhra: „Velikost rohu" se skryje při ostrém rohu čistě, bez server round-tripu. | M | 2 + 3 | — |
| **B5** | **L‑1: auto-fit fontu na šířku** u linkové tabulky (dnes je `ComputeFontScale` jen výškový) | S–M | 3 | — |
| **B6** | **Rozvržení `CellStyleEditor`** — dnes WrapPanel, po přidání „Řezu" se řádek zalomí uprostřed skupiny (potřebuje ~700 px, v 680px dialogu má ~450) → dvousloupcová mřížka ve stylu EDInputFormu (popisky ve sdíleném sloupci, prvky pod sebou); tri-state logika i skrývání voleb beze změny. Projít **všech 6 hostujících dialogů**. | S–M | 4 | — |
| **B7** | **Výchozí šablony MHD + linková do distribuce** — mechanismus embed→MLINE hotový, zbývá kurátorovat + přesunout JSON do `SystemTemplates/` (= krok 44b / C4) | S | 2 | B5 |

**Proč B4 tak brzy:** společná vlastnost elementu (rámeček / pozice / rohy) je dnes opsaná v každém `.cls`
(rohy = 8 souborů) → každá další drobnost na elementech je osmkrát dražší. Čím dřív, tím víc se vrátí.

---

## 🌊 Vlna 3 — zákaznické velké + odemknout linkový

| # | Blok | Vel. | Zóna | Závisí na |
|---|---|---|---|---|
| **B8** | **Varianty tras — dokončení.** Serverová část HOTOVÁ 2026-07-27 (#13048 bod 5 + TR7: varianta = sled zastávek dle legacy dedup — nástupiště vědomě NE, řeší #8828; čas = maximum, řazení dle četnosti / stabilní, písmena variant u odjezdů; oprava `|`=„čas 0"; detail changelog). Zbývá: **vizuální ověření v buildu + MHD parita**; per-písmeno výběr sloupců až na potvrzení TRANSDEV nad novým chováním. | S–M | 1 + 3 | ne spolu s B9 |
| **B9** | **#8828 — vývěska per sloupek zastávky:** = **krok 2 „stanovišťového balíku"** (viz 🎯 vršek roadmapy — tam je aktuální handoff). Datový průzkum HOTOV 2026-07-27 (per-spoj sloupek = W-trasa pozice 7, fallback J-věta; `NastupisteSpoju` už čte). MHD zdroj sloupku dořešit až u MHD zákazníka. | M | 1 | ne spolu s B8 |
| **B10** | **L‑2: symbolový font — linková část** (`ZNAK` sloupce LineTable: multichar/kompozity, převodní tabulka `GSId` 66+ symbolů, TTF v `C:\DEV\EDISON\TiskJR\_ruzne`). Zastávkový JŘ hotový 9. 7. | M→S | 3 (izolovaně) | — |
| **B11** | **L‑3: row-plan vrstva** — `List<RowSpec>` (druh řádku data/header/groupHeader/groupFooter/subrow + buňky) → **jedna sdílená kreslicí smyčka** nad `ColumnRenderer.DrawRow` místo per-element `Render` smyček. Zde proběhne **R1 extrakce řádkového jádra**; LineTable = prototyp. | M | 3 **výhradně** | B5 |
| **B12** | **#7937 rozšíření** — odsazení zastávky per-směr / per-Tč pásmo (`^JRL` dta7 je single-per-point → změna zdroje + editor; base AD hotový). Jen když TRANSDEV tlačí. | M | 1 | — |

---

## 🌊 Vlna 4 — velký kus linkového (dedikovaně)

| # | Blok | Vel. | Zóna | Závisí na |
|---|---|---|---|---|
| **B13** | **L‑4: 2D bin-packing / `Strih`** — široká tabulka (MHD linka = 100+ spojů) se dnes zmáčkne na šířku místo rozřezání do stránkových bloků; + flow paketů, „dotisk zbytku legendy na další stránce" | L | 3 výhradně | B11 |
| **B14** | **Dedup Caché tiskových enginů (C8) + testy tiskových enginů (C9)** — doprovod ostrého knižního JŘ, detail v tabulce Fáze C+D | L + M | 1 | — |

L‑4 nezačínat před B11 — dnes by to znamenalo napsat totéž pětkrát (per-element smyčky).

---

## 🔀 Paralelizace — zóny a pravidla

**Zóna = oblast, kde si dvě sezení šlapou po prstech. Pravidlo: v jedné zóně jedno sezení.**

| Zóna | Co obsahuje | Kolik sezení naráz |
|---|---|---|
| **1** | Caché data agend (`ZastavkoveJrAd`/`Mhd`, `KnizniJr*`) | **1** |
| **2** | Caché infrastruktura (`API.Sablony`, seed, registr agend, EDInputForm `.cls`) | **1** |
| **3** | Kreslicí jádro + elementy (`Rendering/*`, `ScheduleElementBase`, elementy) | **1**; během B11/B13 **výhradně** — nikdo jiný na elementy |
| **4** | Okna, dialogy, XAML, vzhled | 1–2, pokud jsou to jiné dialogy |
| **5** | Dokumentace, helpdesk, ověřování v buildu (bez kódu) | libovolně |

**Dva mechanické limity, které tohle nastavují:**

1. **Vývojový Caché server je jeden** — kompilace tříd je globální akce; dvě kompilující sezení si přepisují
   stav a zlaté testy dostanou falešná selhání. **Jen jedno sezení smí kompilovat Caché**, i kdyby pracovala
   v oddělené kopii repozitáře → zóny 1 a 2 se **nedají paralelizovat, jen střídat**.
2. **.NET paralelizovat lze, ale pracovní kopie je společná** — pro víc než jedno .NET sezení je potřeba
   **oddělený worktree pro každé**, jinak si mění soubory pod rukama (platí i pro paralelní session kolegy).
   Bez worktree: jedno .NET sezení + libovolně „papírových" (zóna 5).

**Bezpečné kombinace:**
- ✅ **B8 nebo B9 + B6 + zavírání tiketů** — vlna 3
- ✅ **B10 + B9** — fontová cesta je izolovaná
- ❌ **B8 + B9** — obě sahají do téže agendy
- ❌ **cokoli na elementech během B11/B13**
- ❌ **B4 + jakýkoli element dialog** — B4 přepisuje pole ve všech `.cls`
- ⚠️ **publish je výhradní vždy**

**Dokumentace:** každé sezení dopisuje záznam na začátek changelogu → při paralelním běhu tam vznikne drobná
kolize. Je triviální; případně ať docs uzavírá poslední dobíhající sezení.

---

## 🏗️ §B4 detail — EDInputForm: sdílená base-pole + deklarativní `visibleWhen` (2026-07-24)

> Vzešlo z tiketů KOVED (rohy elementu #12998). **Dva provázané problémy** + dohodnutý směr.
> **Naplánováno jako blok B4 (vlna 2)** — ne samostatný velký track; kroky 1+2 jsou B4, krok 3 (rozhodnutí
> o C# mirroru) může zůstat otevřený i po něm.

**Problém 1 — imperativní validace na vše.** EDInputForm má jen `Validate` (Caché, server round-trip)
pro dynamické chování. Chybí deklarativní podmíněná viditelnost → „Velikost rohu" je vidět i při Ostré.
**Problém 2 — duplikace schématu.** Každý element staví pole ručně v `.cls` (+ zrcadlící C#
PropertyDescriptors, které **stále žijí** — čte je `PropertiesPanel`); base pole (rámeček/pozice/rohy)
opsaná v každém `.cls` → společná vlastnost = editace N souborů (rohy = 8).

**Dohodnutý směr (standardní vzor „deklarativní 80 % / imperativní escape hatch 20 %", jako ServiceNow
UI Policies vs Client Scripts; TD print-params `visibleWhen` už mají):**
1. **`visibleWhen` do jádra EDInputFormu** (EDControls) — `Field.visibleWhen`, klient přepočítá lokálně
   (bez round-tripu, řeší init-state past). Začít **jen `visibleWhen`** (YAGNI). Sdílet gramatiku podmínek
   s TD print-params — nezavádět druhý dialekt. `Validate` zůstává pro cross-field/počítané.
2. **Sdílený Caché helper `BaseElementFields`** (vzor = `StyleFields.cls`) — base skupiny polí
   (Rámeček+Rohy, Pozice, Pokročilé) volané z každého `.cls`. **První výhra:** rohy deklarují `visibleWhen`
   jednou → „Velikost rohu" se skryje při Ostré čistě.
3. **Prošetřit + rozhodnout** o C# descriptor mirroru (rozsah `PropertiesPanel` vs `.cls`-driven
   `InlinePropertiesForm`) → retire nebo ponechat. Ne slepě mazat (dnes živý).

**Navázané body z řezů písma (#13006, 2026-07-24 — implementováno až na tyto), rozřazené do bloků:**
- **→ B4** **Reaktivita EDInputForm** — „Řez" v element dialozích se plní jen **při otevření**; živý přepočet
  při změně rodiny (bez znovuotevření) = ten `visibleWhen`/reaktivní mechanismus výše. (CellStyleEditor to už
  má — WPF je reaktivní.)
- **→ B6** **Layout editoru sloupců (`CellStyleEditor`)** — dnes horizontální řádek zalomený WrapPanelem
  (dočasné, po přidání řezu se to natáhlo). Přepracovat na přehlednější grid, ideálně sjednotit s EDInputForm stylem.

(B4 samo M, dotkne se všech elementů + EDControls. Detail úvahy v changelog/commit historii session 2026-07-24.)

---

## 🧹 Úklid a drobnosti — zásoba pro další vlny

> Malé věci **na jednom místě** (dřív rozseté po šesti sekcích), aby se daly brát **tematickými dávkami**
> — jedno sezení = jedno téma, ne posbírané drobnosti napříč. Co už je naplánované, nese číslo bloku.

**Provozní** — `B7` výchozí šablony MHD/linková ·
varování v designeru „N skupin dnů sdílí sloupec bez rozlišovacích značek" (S, MHD SMO fáze 1)

**Vzhled / téma** — `B6` rozvržení `CellStyleEditor` · **dark mode** runtime swap + ověření (M–L;
EDThemeDark + tokenizace hotové 2026-07-13, plocha plátna už na tokenu — zbývá chrome návrháře
(papír / pravítka / záhlaví sekcí) + rozhodnutí, jak v tmavém režimu vypadá papír) · **postupná
unifikace dialogů** (EDInputForm + sdílený „DialogChrome"; průběžně při dotyku dialogu, ne velký
refaktor — L celkem)

**Na poptávku** — tiskový parametr „Rozdělení sloupců" pro MHD (M, sjednocení s AD; MHD SMO fáze 3) ·
exportní parametry per šablona (název souboru / členění / imposice dnes per agenda — S–M) ·
per-sloupcové zvýraznění řádků (`HighlightRule.TargetColumn` — model pole nese, render barví celý řádek)
+ barva textu / kurzíva pravidla

**Odložené UX nálezy (2026-07-13)** — VPN-down rozlišit „nedostupná" od „není nakonfigurována" + varování
při startu (M; sem patří i „okno naskočí až po ~3 s při nedostupném připojení")

---

### 📥 Backlog / budoucí velké tracky

> Audit stavu v1 (scan 2026-07-07) — P-A/P-B/P-C hotové, P-D rehomed do produkční sekce →
> **detail v [`roadmap-archive.md`](roadmap-archive.md)** (Session 2026-07-08). Konsolidace jádra
> hotová, plocha může růst.

- **Zastávkový JŘ business** (parkované helpdesk A2-A5/A6/B4 + TRANSDEV sada) → viz sekce „Zastávkový JŘ — business logika" níže
- **Fáze B — odložené:** **oživení fixní tabulky** (paleta + Fixed editor) + **merged cells** (gated na ni).
  (Conditional formatting seznam pravidel = HOTOVO #1, 2026-07-08.)
- **Tabulkové featury (bez customer demand):** grouping (`GroupHeader`/`GroupFooter`/`Summary`) ·
  vnořené datasource (`_rows` v `_rows`).
- **Fáze C+D** linkové/knižní JŘ · **Fáze E** vozový + Relace · **Fáze F** REPORTGENERATOR — velké
  tracky, každý gated rozhodnutími R1–R7 (viz [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md))
- **Výkon MHD tisku** — hlavní bottleneck **vyřešen 2026-07-20** (odstraněno drahé nepoužívané pole
  „první/poslední spoj" počítané per stránka přes legacy MHD2 → ~7 s na ~0,6 s, viz changelog).
  Původní hypotéza (memoizace `GetNazevHrl`/`PopisDopravce`) se **neukázala** — názvy byly zanedbatelné
  (profiling: build zastávek 0,02 s). Zbytek fází ~0,5 s = zatím OK; další memoizace až kdyby reálně bolelo.
- **Konverze legacy PostScript JŘ → TD šablona (`.json`)** (na poptávku, nejdřív průzkum) — cíl: zákazníci
  s jízdními řády v legacy PostScriptu je nemusí přepisovat od nuly. ⚠️ **Klíčové rozlišení, od kterého se
  musí začít:** PostScript je **vyrenderovaný výstup** (layout + zapečená konkrétní data), kdežto naše
  `.json` je **šablona** (jen vzhled; data zvlášť z DB). Takže to **není převod dat**, ale **rekonstrukce
  layoutu** — přesně tentýž problém jako Import PDF. Nejpravděpodobnější cesta **bez psaní vlastního PS
  parseru**: `PS → PDF` (Ghostscript) → **stávající AI Vision import PDF** (rozpozná StopList / mřížku
  odjezdů / legendu a namapuje pole agendy → živá editovatelná šablona, ne obrázek). Dořešit: kolik
  zákazníků to reálně má a co přesně (samostatné `.ps` soubory? embedded v tisku?), věrnost převodu,
  a že výstupem musí být editovatelná šablona napojená na data. Relates: sekce „Import PDF" níže + „Externí
  datové zdroje".
- **Externí datové zdroje** (analýza 2026-07-12, na poptávku) — architektura připravená (engine zná jen
  `Template + DataContext`; DataContext je zdrojově agnostický JSON). Tři švy: Caché gateway agenda
  (0 změn .NET) · .NET provider vedle Caché (vzor `JsonFileDataSchemaProvider` + Composite) · headless
  A3 („POST šablona+JSON → PDF" = externí zdroj bez adaptéru; **A3 je nejpravděpodobnější nosič**).
  První krok s nejlepším poměrem cena/hodnota: **generický JSON zdroj (soubor/URL)** — tržní minimum
  (CSV/Excel/JSON/REST), náš wire formát už JSON je; využilo by i AI + testování šablon. Dořešit:
  credentials, schéma z dat, změkčení předpokladu „běží nad Caché".
- AI (v pozadí) · font warning + ad-hoc overflow (YAGNI)

*(Drobnosti a odložené UX/vizuální nálezy → sekce „Úklid a drobnosti" výše.)*

**Otevřené otázky k Fázi E/F** (Epson, RG bridge, Relace, grafy/AZTEC, jazykové varianty) → evidované
v [`legacy-nahrada.md §7`](legacy-nahrada.md); vyřeší se
při scope té fáze, ne teď.

---

## 🚀 Produkční / provozní — otevřené

1. **Headless tisk (A3)** — „vytiskni linku X" z modulu přes PrintEngine fasádu (šablona+data→PDF bez stavu editoru). Dnes tisk jen v designeru. Gate R4/R7.
2. **Samostatný distribuční kanál** systémových šablon mimo .exe — v1 doručuje embed→MLINE; „jiný kanál" (server push / admin) jen vymění zdroj, picker/UX beze změny.
3. **Další výchozí šablony** (MHD + linkový) → **blok `B7`** (vlna 2); táž práce = krok 44b / C4.
4. **Zákaznická dokumentace na wiki (post-release)** — 2 dokumenty, seedy HOTOVÉ a aktuální
   (2026-07-13): `uzivatelska-prirucka.md` (část I obecné ovládání + část II zastávkový JŘ;
   nové agendy = nové části) + `plan-a-zmeny.md` („Novinky a plány"). Zbývá: obrázky + publikace.

---

## 🅿️ Zastávkový JŘ — zákaznická fronta

> **Plný stav** (hotové featury + co otestovat pro uzavření helpdesk tiketů) →
> [`customer-requirements-zastavkovy-jr.md §0`](customer-requirements-zastavkovy-jr.md). Zde jen **otevřená
> akční práce**. Vel.: S = do hodiny, M = půl dne, L = víc dní.

### Čeká jen na doběhnutí — blok B2

| Tiket | Uzavírací podmínka (co ověřit, aby se dal zavřít) |
|---|---|
| #7947 | linka se spojem za základní trasu → legenda „jede až do zast. X" |
| #7955 | `SCHOVAT_V_ROZSAHU`=Automatické → auto-rozdíl zmizí, ruční zůstane; NZ nemizí |
| #9036 | `TimeAxisMode=Range`, HODINY_OD=22 / DO=5 → wrap přes půlnoc |
| #13048 bod 2 | finální vizuál zmenšování textu (názvy zastávek, zóny, rezerva u šipky směru) |
| #13048 bod 5 | vizuál variant tras + písmen u odjezdů (server hotový, viz B8) |

### Čeká na někoho jiného — postrčit, není to práce

- **#10660 DPKV** — hotové, zpřístupnit build + proklikat jejich obrázky
- **#7959** — ověření klientem na 876401/876402
- **TR2** implementováno (opt-in `SOUHRN_NA_ZNAMENI`), ⚠️ čeká SK ověření kódování na-znamení — kolega
- **#13048 bod 4 fáze 2** (duplikace řádku u víc nástupišť) — čeká potvrzení KOVED
- **TR8 („<1" místo „0") park** (analýza 2026-07-10): naše časy jsou kumulativní, 0-segment se tiskne
  jako opakované číslo, ne „0". Per-segment sloupec TD nemá. Blokuje potvrzení od TRANSDEV
  (který sloupec — legacy „Průběhy"?). Detail → `customer-requirements §TR/8`.

### Reálná zbývající práce

`B8` dokončení variant tras · `B9` #8828 vývěska per sloupek · `B12` #7937 per-směr / per-Tč odsazení

### Bez poptávky nebrat — chce analýzu / datový model / business rozhodnutí

| # | Co | Vel. | Co potřebuje |
|---|---|---|---|
| TR10 | Skrýt spoje o So/Ne/svátcích | S–M | Caché filtr |
| TR7 | Výběr sloupců variant jízdních dob (per-písmeno výběr) | S–M | **zčásti vyřešeno 2026-07-27** (#13048 bod 5): varianty nově dle trasy+nástupiště s max časy (málo sloupců) + parametr „Řazení variant tras: Stabilní" = stabilní pořadí. Zbývá jen případný per-písmeno výběr, až ho TRANSDEV potvrdí nad novým chováním |
| TR6 | Barva linky (bg/text ve vlastnostech linky → volba v šabloně) | M | nový datový model |
| TR9 | Obrácená logika poznámkování (rozsah TČ v 1. tabulce) | M | Caché-input (Studio JŘ) |
| TR5 | Definice cíle — střední (per zast.×směr) + ideál (DB cílů) | M–L | datový model; *(min „cíl per směr" = hotovo přes `POPIS_SMER`=konečná)* |

*(#7937 = blok `B12`, #8828 = blok `B9` — detail v tabulkách vln výše.)*

---

## 🟠 Fáze B: Element flexibility — jen odložené body

Z gap analýzy vůči komerčním generátorům. Nezávislé na JŘ (faktury i obecné šablony). Hotová část
(conditional formatting, počítaná pole, min/max, versioning framework) → archiv.

**Fixní (statická) tabulka — analýza + plán HOTOVY (2026-07-12), implementace odložena na poptávku faktur/F3:**

> Rozhodnutí z analýzy: **koncept implementovat** (FastReport/Stimulsoft svět našich uživatelů Table
> objekt čeká; tabulka dává logiku, kterou skupina elementů principiálně neumí — **reflow podmíněných
> řádků** (skrytý řádek zkolabuje, díra nezůstane), **auto výška řádku** (obsah odtlačí řádky pod sebou),
> strukturální editace (insert řádku, sloupcový resize), sémantika pro Caché distribuci + AI).
> Pro JŘ není potřeba (bloky záhlaví = 2×3 bez podmínek) → **nedělat hned, ale s poptávkou faktur / F3**.
> Průzkum stavu: model+render Fixed módu KOMPLETNÍ a na sdíleném jádru (kaskáda, CellRenderer,
> ResolveWidths), 5-tab `TableEditorDialog` funkční vč. „Ze šablony…" na hotový `GetTableTemplate`
> kontrakt (agendy už nabízejí ZAHLAVI_LINKY/PLATNOST_DOPRAVCE/FAKTURA_*).

- **Krok 1 — střízlivé oživení (S–M):** paleta „Tabulka (pevná)" + factory (`Mode=Fixed`, Mode v UI
  zamčený — dynamickou dělá `DynamicTableElement`); opravy nálezů: `Clone()` zapomíná `SummaryRows`,
  `RowVM.ToModel()` zahazuje per-cell `Format`+`Style`; render dorovnat na `ColumnRenderer.DrawRow`
  (sjednotí mřížku, auto-scale, **předpoklad pro merge**); drift-test allowlist zúžit.
- **Krok 2 — UX modernizace + merge (M, s poptávkou):** inline editace buněk na plátně (dvojklik,
  à la F2 na TextElementu — standardní pocit z FastReportu, řeší „5 tabů je moc"), redukce dialogu
  na 2-tab vzor, **ColSpan** merged cells (aditivní JSON, span-aware DrawRow). **RowSpan vědomě
  odložen** (vzácný, těžký — spanování výšek × zebra × VisibilityExpression) — až na poptávku / F3.

---

## 🟠 Fáze C + D: Výlepový/knižní JŘ linky (náhrada TiskJR) — pravděpodobně JEDEN track

> ⚠️ **Gate: R1 (sdílené tabulkové jádro), R2 (layout zodpovědnosti), R5 (embedded fonty)** —
> [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md). Krok 43 začíná
> designem sdíleného jádra (StopList + DynamicTable + LineTable), ne kódem elementu.

Legacy `TiskJR` generuje vývěsní i knižní JŘ z téhož enginu → C + D je nejspíš jeden track. Formát:
sloupcová tabulka celé linky (svislý seznam zastávek × sloupce spojů s časy; řádek platností se
symboly/NZ; Vyčká/Navazuje; skupinová záhlaví PD/So/Ne; 10 druhů poznámek). **Gold oracle `mkPLinWin`
(ALVA.JRMAIN.MLAPI)**, metodika = čistá reimplementace à la `ZastavkoveJrAd` (~1 měsíc).

**Prerekvizity (pořadí):** (1) symbolový font Timetable_nove(+SK) do Skia — **základ hotový 9.7.** (embed
TTF + locale-aware `FontResolver.SymbolFontFamily`, single-char symboly zastávkového JŘ); pro linkovou část
zbývá převodní tabulka `GSId` (66+ symbolů, TTF v `C:\DEV\EDISON\TiskJR\_ruzne`) na multichar/kompozity; (2) agenda
`KnizniJrAd`; (3) `LineTableElement` + layout rozšíření (flow paketů, „dotisk poznámek z minulé stránky",
**auto-fit fontu na šířku** — dnes je ComputeFontScale jen výškový); (4) per-linka šablony (viz B4).

> Pilotní část hotová (`KnizniJrAd`/`KnizniJrMhd` datové jádro, element, editor, paleta) → archiv
> 2026-07-04 + detail `linkovy-knizni-jr-implementace-analyza.md` §11-12.

### 🎯 Ukazatel postupu `L‑1…L‑4` — velký kus rozřezaný na slotové kroky

**Smysl:** aby linkový JŘ postoupil v **každé vlně**, i když běží helpdesk. Každý krok končí něčím
dokončeným; velký `L‑4` se otevírá až nad hotovým `L‑3`.

| Krok | Blok | Co | Vel. | Stav |
|---|---|---|---|---|
| **L‑1** | `B5` | Auto-fit fontu na šířku u linkové tabulky (dnes jen výškový `ComputeFontScale`) | S–M | ⬜ |
| **L‑2** | `B10` | Symbolový font — linková část (`ZNAK` sloupce, multichar/kompozity, `GSId` 66+) | M→S | ⬜ |
| **L‑3** | `B11` | Row-plan vrstva = jedna sdílená kreslicí smyčka (**R1 extrakce řádkového jádra**) | M | ⬜ |
| **L‑4** | `B13` | 2D bin-packing / `Strih` — rozřezání široké tabulky do stránkových bloků | L | ⬜ gate L‑3 |
| — | `B7` | Linková výchozí šablona do distribuce (dá se přilepit ke kterémukoli slotu) | S | ⬜ |

**Mapování na staré číslování kroků:** 42b = L‑2 · 43 = L‑1 + L‑3 + L‑4 · 44b = `B7`.
Odložené body C2–C9 níže zůstávají jako detailní evidence (C2 = L‑2, C3 = L‑3 + L‑4, C4 = `B7`,
C8 + C9 = `B14`).

### Odložené body knižního/linkového JŘ (z pilotu, seřazené dle hodnoty pro ostré nasazení)

> Pilot uzavřen 2026-07-04 jako „testovací v1" (detail → archiv). Dál dle priorit, ne nutně hned zde.

| # | Co | Proč odloženo / dopad | Odhad |
|---|---|---|---|
| C2 | **Symbolový font Timetable_nove(+SK)** do Skia (krok 42b, gate R5). **Zastávkový JŘ hotový 9.7.** — embed `TimeTable2.ttf`/`TimeTable2SK.ttf`, locale-aware `FontResolver.SymbolFontFamily`, single-char `SpecialniZnak` → piktogram (NotesLegend + DepartureGrid); na-znamení `x/y/z` = text. **Zbývá linková část**: `ZNAK` sloupce LineTable (multichar/kompozitní symboly, převodní tabulka `GSId` 66+ symbolů) | vizuální parita; bez linkové části knižní/výlepový JŘ „tiše lže" u dopravních symbolů (R5) | M→S |
| C3 | **2D bin-packing / `Strih`** (krok 43 + gate R1/R2, *přesunuto z layout tracku 2026-07-07*) — široká tabulka (MHD linka = 100+ spojů) se dnes zmáčkne na šířku místo rozřezání do stránkových bloků. Součást: **auto-fit fontu na šířku** (dnes jen výškový `ComputeFontScale`), flow paketů, „dotisk zbytku legendy na další stránce". **Zde proběhne R1 extrakce sdíleného řádkového jádra** = zavedení **row-plan vrstvy** (`List<RowSpec>`: druh řádku data/header/groupHeader/groupFooter/subrow + buňky → **jedna sdílená kreslicí smyčka** nad `ColumnRenderer.DrawRow`), ať „řádkový layout" (grouping, page-spanning, per-typ-řádku, merged) přestane být per-element `Render` smyčka (dnes N míst → pak 1 datové tvarování). **LineTable = prototyp** — už heterogenní řádky skládá inline. Pozn.: sdílené low-level (buňka/sloupec/kaskáda/šířky) i řádkové podmínky (viditelnost/zvýraznění) jsou **hotové** (pětice levných švů 2026-07-08); row-plan je poslední zbývající řádková vrstva. Zastávkový JŘ to nepotřebuje (DepartureGrid = málo sloupců). | nutné pro reálný tisk MHD linek; největší kus, dělá se jednou pořádně | L |
| C4 | **Rozšíření výchozích šablon na další agendy** — mechanismus embed→MLINE hotový (governance v1, zastávková AD); přidat MHD + linkový JSON do `SystemTemplates/` | provozní; malý | S |
| C5 | **Konfigurovatelné pořadí poznámek** (`PoznamkyPoradiDruh`) — legacy AD parametr; TD má pevné pořadí druhů | per-zákaznická variace, řešit až s poptávkou | S |
| C6 | **Turnusy, obousměrný tisk, souhrnné JŘ (řádek zdrojové linky)** — legacy volby `in(arr,6/7)`, MHD `MakeData` je nemá | mimo scope pilotu; validovat, zda je zákazník chce, než portovat | — |
| C7 | **Rozdvojení příjezd/odjezd — jemný layout** + fixed-width padding sloupce (legacy `arr,13`) | kosmetika; TD řeší podřádky, ne padding | S |
| C8 | **Dedup Caché tiskových enginů** (*z auditu H1*) — `CasHodnota` bere `"|"` jinak v `KnizniJrAd` vs `ZastavkoveJrAd`, různé signatury `AddAnotace`/`BuildPoznamka`, ~15 helperů MHD↔AD + AD `^JRL` readery. Vyextrahovat sdílené `^JRL` readery + `Build*` do `BaseAgenda`/`JrShared`. **Caché analog R1 extrakce jádra** (C3 řeší .NET render-side, C8 Caché data-side). | korektnostní riziko; dělat s ostrým knižním JŘ, ne dopředu | L |
| C9 | **Testy Caché tiskových enginů** (*z auditu H2*) — `KnizniJr*` (~1975 ř.) 0 testů, `ZastavkoveJrMhd` 1 helper; AD gold gate tiše projde bez `CB_CSADAD`, křehký digest nad živými daty (pinuje 2/22 linek), `tools/compare-gold.ps1` neexistuje. Smoke + frozen-input gold testy; bezpodmínečný gold gate (zmrazit vstup do izolovaného ns). **= T1b.** | důvěra v datovou paritu; s ostrým knižním JŘ | M |

Detail datových odchylek a mezer viz [`linkovy-knizni-jr-implementace-analyza.md`](linkovy-knizni-jr-implementace-analyza.md) §11–12.

---

## 🟠 Fáze E: Vozový JŘ + Relace

Layout TBD — hlavička oběhu, tabulka spojů, poznámky (pravděpodobně víc elementů). Legacy:
`ALVA.MHD.ReportGenerator` (MHD) + `AD.ADREPORTGEN` (linky), viz
[`legacy-nahrada.md`](legacy-nahrada.md). **Relace (TiskJRRELACE)** — vývěsky
nádraží (SK zákazník, fork TiskJR ~50k LOC); **nejdřív ověřit živý use case (Q3)**.

| Krok | Co | Stav |
|---|---|---|
| 48 | Analýza referenčních vozových JŘ — layout + wire formát | ⬜ |
| 49 | `VehicleScheduleElement` (pravděpodobně víc elementů) | ⬜ |
| 50 | Caché `VozovyJrAPI` — GetData/GetMockData + async (velký objem dat) | ⬜ |

---

## 🟠 Fáze F: Náhrada REPORTGENERATOR (univerzální sestavy)

> ⚠️ **Gate před F2: R3 (prezentační nápovědy v datovém kontraktu + aditivní evoluce)** —
> [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md).

Největší kus náhrady — ne kvůli featurám (TD je napřed), ale kvůli **ekosystému ~28 agend** s RG
kontraktem (`GetSeznamSad`/`GetSadu`/`GetData`, `~`-tokenový stream). Nasazení: 33k textových elementů,
774 tabulek. **Ne big-bang** — etapizovat. Detail:
[`legacy-nahrada.md`](legacy-nahrada.md) §3.1 + §5 etapa 4.

| Krok | Co | Stav |
|---|---|---|
| F1 | **Prioritizace podle usage** — legacy `Main.cls:161-188` drží reálná čísla použití; seřadit agendy | ⬜ první (levný) |
| F2 | **Bridge vs čisté agendy (Q2)** — bridge `LegacyRgAgenda` (překlad `~`-protokolu na TD JSON, všech 28 najednou s omezenou věrností) vs čisté reimplementace; doporučení: **bridge nejdřív** | ⬜ rozhodnutí |
| F3 | **TableElement na RG úroveň** (navazuje na dynamickou tabulku + Fáze B): per-řádek zvýraznění 0-5 z dat, custom barvy per řádek/buňka, overflow módy (zalamování → klonování řádků), opakování mezisoučtu na další stránce, datové typy sloupců (datum/minuty/záporné červeně/skrýt nuly), řádek-jako-layout | ⬜ L |
| F4 | **Výstupní kanály fáze 2** — Excel export, PDF heslo + el. podpis (PDFSIGNER), Epson znakový tisk (Q1) | ⬜ dle poptávky |
| F5 | **Jazykové varianty šablon** — per-jazyk kopie provázaná na CS zdroj (Q5) | ⬜ |
| F6 | **Vědomě neportovat bez poptávky** — grafy (12×), AZTEC (2×), calculated (2× — výrazy TD nadmnožina), checkbox (8×); ověřit + škrtnout (Q4) | ⬜ validace |

---

## 🟡 Import PDF — doladění

> Pre-produkční průchod HOTOVÝ 2026-07-13 (Sonnet 5, anti-halucinace, retry, standardní dialog,
> jednostránkový JŘ import) → changelog. Zde jen dlouhodobé ladění:

| Krok | Co | Stav |
|---|---|---|
| 32 | **Obecný import: detekce tabulek z mřížky čar** — místo 50 čar + 20 textů jeden tabulkový element. ⚠️ **Gated na oživení fixní tabulky (Fáze B)** — cíl importu statické tabulky je Fixed mód, který je dnes uspaný; do té doby nedělat | ⬜ |
| 33 | JŘ import: **ladění na širší sadě JŘ** (jiné layouty než KV_DP vzor — jiná města, SK, netypické legendy) | 🟡 |

Křivky/Bézier v obecném importu — low-prio, bez poptávky.

---

## 🤖 AI integrace — v pozadí

> **Sekundární, ne core.** Aktuální funkce (pomocník, generátor šablon, AI Vision import PDF — vše Sonnet 5
> přes EDISONAI/mlweb spravovaný klíč) zůstávají funkční; další rozšíření až budou reální uživatelé
> s potřebou. JSON šablona + DataSchema jsou stabilní kontrakt pro AI — rozšíření kdykoli bez breaking
> changes. **Princip: copilot, ne autopilot.**

**Otevřené (post-release):** ostrá zkouška EDISONAI cesty bez lokálního klíče u první firmy ·
potvrzení `source: "templatedesigner"` u autora mlwebu · zveřejnění generátoru šablon
(menu dnes jen pro vývojáře) až po otestování na reálných zadáních.

**Eval sada AI pomocníka** (průběžné doškrtání; pomocník je už uživatelsky ověřen, tohle je regresní
sada) — u každé otázky hodnotit: správná cesta v UI / žádné vymyšlené featury / rychlost odpovědi.
- *Lookup:* „dvě vývěsky na A4" → Ctrl+P Uspořádání na list · „linka do vlastního PDF" → Ctrl+P Členění + Název souboru ·
  „stránka na A5" → Ctrl+Shift+P · „kopírovat styl" → Ctrl+Shift+C/V · „sloupec cena×počet" → výraz `{CENA}*{POCET}` ·
  „zvýraznit řádky >1000" → zvýraznění řádků · „víc linek k tisku" → chip Instance · „změnit agendu" → Ctrl+Shift+A ·
  „červený rámeček náhledu" → přetečení obsahu · „součet pod tabulku" → `SUM(...)` · „schovat sekci na 1. str." → VisibilityExpression ·
  „vrátit smazaný prvek" → Ctrl+Z · „písmo celé tabulky" → dialog sloupců · „šablona pro kolegu" → Ctrl+S do DB.
- *Chytáky* (očekává „zatím neumíme", ne vymyšlený návod): sloučení buněk · export do Excelu · PDF e-mailem ·
  brožura/duplex · vlastní font · import staré šablony z EDISONu.
- *Nejistota* (odkázat na F1/podporu, ne halucinovat): „špatný čas u 3. zastávky linky 12" (datový problém, ne UI).

**Budoucí:** MCP server (`get_template`/`apply_template`/`add_element`/… tenká vrstva nad ViewModelem, každý
tool → `_vm.Execute(command)`) · onboarding průvodce · smart suggestions (TableElement → sloupce z DataSchema) ·
migrace starých Caché sestav · template linting v CI/CD.

---

## 🗄️ Hotová práce

Detail dokončených etap → [`roadmap-archive.md`](roadmap-archive.md). Průběžný changelog → [`changelog.md`](changelog.md).
