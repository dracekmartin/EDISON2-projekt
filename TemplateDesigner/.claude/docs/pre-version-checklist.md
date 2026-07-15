# Pre-version checklist — úterní verze (cíl 2026-07-14)

> **Účel:** jedno místo pro validaci před vydáním verze — testovací průchod, doladění dokumentace,
> release kroky a příprava komunikace. Odškrtávej `[x]`, nálezy zapisuj do tabulky dole.
> Po vydání: hotový checklist → smazat / přesunout výsledek do changelogu, nálezy do roadmapy.
>
> Automatika už kryje logiku (.NET 340/340, Caché 33 gold + smoke v pre-push). Ruční průchod
> míří na vizuál, UX a integrační cesty, kam testy nevidí.

---

## 1. Testovací průchod

### A. Kritická cesta — musí projít (~30 min)

- [ ] **Zlatý workflow AD:** distribuční šablona zastávkového JŘ (Ctrl+O, M-line sekce) → 2–3 linky
  vč. gold (0365001, 0320144) → živý náhled → Ctrl+P → tisk s reálnými daty do jednoho PDF.
  Zkontrolovat: minuty, poznámky, legenda, **kroužkové denní číslice ⑥**, bezbariér **piktogram**
  (ne „H"), na-znamení „x" jako text, NZ boxy.
- [ ] **Totéž MHD** (CB_DP 1001) — hlavně **`{POPIS_SMER}` teď tiskne konečnou, ne „T"**
  (kontraktní změna 10.7.!). ⚠️ Projít uložené MHD šablony: pokud některá používala
  `{POPIS_SMER}` jako T/Z, přepnout na `{SMER}`.
- [ ] **Ukládání:** změna → Ctrl+S (tichý update) → zavřít → otevřít → obsah sedí.
  Nová šablona → Ctrl+S založí do DB (Uložit jako).
- [ ] **Spuštění z EDISON shellu / MHD2** — startovní agenda se předvolí, MLRest session funguje
  (nejcitlivější integrace; launch kontrakt se neměnil, ale ověřit).

### B. Nové featury tohoto týdne (~45 min)

- [ ] **Imposice:** Ctrl+P → „A4 na šířku" + Auto → 2×A5 na list sekvenčně; „Každá stránka na celý
  list" → 2× táž vývěska; ořezové značky; **nevalidní mřížka (2×2 na A4) → hláška, žádný soubor**;
  Vypnuto = výstup identický s dřívějškem; export z hlavního okna bere stejné nastavení;
  persistence přes restart aplikace.
- [ ] **Dvouosý model agend:** Ctrl+Shift+A — seskupený dialog (nadpisy agend, varianty pod nimi,
  badge „(aktuální)", dvojklik); chip „Agenda: Zastávkový JŘ · Varianta: AD"; titulek
  `[Zastávkový JŘ · AD]`; Ctrl+O — sloupce **Agenda | Varianta** hned za názvem + filtr funguje;
  obecná šablona → „Agenda: Obecná (bez databáze)".
- [ ] **TR3 nástupiště:** v dialogu sloupců StopListu zapnout sloupec „Nástupiště" →
  CB_CSADAD 0320680 ukáže hodnoty (por. 1/8/12/15/18/20), jinde prázdné.
- [ ] **TR4 fixed-then-shrink:** StopList s ručně nastavenou pevnou výškou řádku + malá Height
  elementu → řádky se zmenší (nepřetečou); default auto beze změny.
- [ ] **Řazení legendy dle typu (nový default!):** legenda = značky → atributy → text;
  stará šablona s explicitním „Výchozí (dle dat)" pořadí NEzmění.
- [ ] **Reset stránky:** Ctrl+Shift+P → změna A4→A5 → potvrzovací dialog → čistý reset;
  jen změna okrajů → žádný reset (undoable změna); Storno → šablona netknutá.
- [ ] **Odsazení zastávek (AD):** linka s odsazenými podzastávkami → odsazení v StopListu
  (`ShowStopIndentation`).
- [ ] **TR2 souhrn na znamení:** opt-in param default VYP → beze změny chování; zapnout na lince,
  kde jsou všechny zastávky „x" → jeden agregátní řádek legendy místo × u každé.
  (SK data zatím netriggerují — čeká na potvrzení kódování, netestovat na SKNI.)

### C. Regresní rychlovky po auditních opravách (~20 min)

- [ ] **B1 fingerprint:** u DepartureGridu měnit v panelu „Výška řádku" / „Oddělovač skupin" /
  „Mezera anotace" → náhled se mění **okamžitě** (dřív mrtvé).
- [ ] **B7 async schéma:** otevřít šablonu agendy nepoužité v session → UI nezamrzne,
  chip „načítám schéma…" → doplní se.
- [ ] **B2 auto-scale:** DepartureGrid s pevnou Height menší než obsah → fonty se zmenší,
  varování nesvítí zbytečně.
- [ ] **B5:** F5 během přepínání instancí → žádná stará data v náhledu.
- [ ] **D3:** AI dialog — API klíč tečkovaný (PasswordBox); PrintDialog tlačítko „Zavřít" funguje.
- [ ] **Chybové cesty:** bez VPN/Caché → TD startuje s GENERAL + srozumitelný stav (ne pád);
  chyba parametrů agendy → hláška, ne tichý prázdný seznam (C-c).

### D. Edge / dle času (~15 min)

- [ ] Import JSON staré šablony (starý kód „MHD" / `KNIZNI_JR_*`) → migrace + remap projde.
- [ ] Export split (per linka / per zastávka×směr) + imposice + název souboru z tokenů dohromady.
- [ ] Multi-varianta časů (0365005, `ShowAllRouteVariants`) — dynamické sloupce OK.
- [ ] PNG export (bez imposice — záměrně) + fit-to-page (Ctrl+0), 1:1 (Ctrl+1), Ctrl+B panel.

### E. Publish build — NEVYNECHAT (~15 min)

- [ ] Zavřít běžící TemplateDesigner.exe → `.\publish.ps1` → `C:\Edison\TemplateDesigner.exe`.
- [ ] Ve **publikovaném** .exe: symbolový font Timetable (piktogramy v legendě/mřížce) —
  embedded fonty jsou glob v csproj, publish je jediné reálné ověření single-file bundle.
- [ ] V publikovaném .exe: rychlý smoke — otevřít šablonu z DB, náhled, 1 PDF export s imposicí.
- [ ] Spuštění publikovaného .exe z EDISON shellu (ne standalone) — session kontrakt.

---

## 2. Dokumentace před verzí

- [x] **Changelog** — záznamy od minulé verze kompletní (AI vrstva, EDISONAI, PDF import, nápověda —
  2026-07-13).
- [x] **Roadmapa čistá** — plná revize 2026-07-13: import průchod odškrtnut, dispatch sekce sloučená,
  wiki dokumentace přidána, import krok 32 gated na fixní tabulku, priorita 1 = vydání.
- [x] **CLAUDE.md „Co máme"** — nové featury zapsané vč. AI/import/nápověda (2026-07-13); „Známé
  problémy" prázdné — sedí.
- [x] **customer-requirements-zastavkovy-jr.md** — revize 2026-07-13: P3 imposice ✅ hotová (v1),
  dispatch kandidáti odbaveni (TR3/TR4 ✅, TR2 čeká SK, TR8 park); podklad pro helpdesk připraven.
- [x] **ux-conventions tabulka zkratek** — bez driftu; nápověda F1 přepsána podle ní (2026-07-13).

---

## 3. Release kroky

- [ ] Vše commitnuto + **pushnuto** (pre-push testy prošly).
- [ ] Publish build (sekce 1E) proběhl z finálního commitu.
- [ ] Označit verzi: git tag / poznámka commit hashe do release záznamu (dle firemní konvence).
- [ ] Distribuční šablony: embed→MLINE seed proběhl (zastávková AD výchozí je v MLINE větvi,
  klienti ji uvidí read-only).
- [ ] Krátký záznam „co je ve verzi" (interní) — vzít z changelogu, období od minulé verze.

---

## 4. Komunikace

### Interní poznámka kolegům (⚠️ důležité)

- [ ] **Breaking pro pilotní MHD šablony:** `{POPIS_SMER}` nově = konečná zastávka (jednotné
  AD≡MHD≡Linkový); T/Z je v novém poli `{SMER}`. Kdo má rozpracovanou MHD šablonu s POPIS_SMER
  jako T/Z, musí přepnout pole. AD šablon se netýká.
- [ ] Nové názvosloví: **Agenda** (Zastávkový JŘ / Linkový JŘ / Faktury) × **Varianta** (AD / MHD)
  — viz kolegův podnět; kombinované kódy se nemění.
- [ ] TR2 (souhrn na znamení) čeká na potvrzení SK kódování na-znamení — kolega.
- [ ] **EDISONAI/mlweb — domluva s autorem mlweb:** TD hlásí spotřebu na `/api/mline/usage`
  se `source: "templatedesigner"` — ověřit, že endpoint neznámý source přijme a započítá
  (jinak TD spotřeba nejde do limitu); limit sdílený s EDISONAI chatem per IdFa = záměr, potvrdit.

### Draft e-mailu zákazníkům (doplnit adresáty + tón dle zvyklostí)

- [ ] Zkontrolovat/doladit draft níže a odeslat po vydání.

> **Předmět:** Nový Návrhář sestav v EDISONu
>
> Dobrý den,
>
> rádi bychom vám dali vědět, že jsme dnes v EDISONu spustili nový **Návrhář sestav** —
> nástroj, ve kterém si sami navrhnete podobu tiskových jízdních řádů. Vytvoříte v něm
> vzhled vývěsky a systém už do ní sám doplní aktuální data a připraví PDF k tisku.
>
> Najdete ho v modulu **Jízdní řády a MHD**, v části **Nastavení a číselníky → Návrhář sestav**.
>
> Abyste nezačínali od prázdné stránky, připravili jsme pro vás **úvodní šablonu**, na
> které můžete rovnou stavět — najdete ji v dialogu Otevřít pod „Systémové M‑line" a
> zároveň ji dáváme k dispozici jako soubor JSON (na wiki), který si můžete kdykoli
> naimportovat. Stačí si ji upravit podle sebe. Během návrhu přitom průběžně vidíte živý
> náhled s reálnými daty — stačí vybrat linku a Návrhář dopočítá zbytek. Hotovou šablonu
> uložíte do databáze, odkud ji máte k dispozici celá firma, a kdykoli z ní vytisknete
> PDF s aktuálním jízdním řádem. Zatím je Návrhář připravený pro zastávkové jízdní řády,
> další typy budeme postupně přidávat.
>
> Podrobnější návod a přehled novinek dáváme na naši wiki — **[odkaz doplníme]** —,
> kterou budeme dál průběžně rozšiřovat.
>
> Návrhář navíc umí dvě věci s pomocí umělé inteligence: **Pomocníka**, který poradí, kde
> v aplikaci co hledat, a **Import z PDF**, který z existující vývěsky rovnou vytvoří
> editovatelnou šablonu. Obojí je volitelné — pokud byste to chtěli používat, ozvěte se
> nám; zřídíme vám přístup, nastavíme rozumné limity a rozpočet a hned to budete mít
> v Návrháři k dispozici.
>
> **Poznámka pro vaše IT (jednorázově):** Návrhář ke svému běhu potřebuje **.NET Desktop
> Runtime 10.0 (64bit)** od Microsoftu. Na počítačích, kde ho ještě nemáte, ho stačí jednou
> nainstalovat — je zdarma ke stažení na stránkách Microsoftu
> (dotnet.microsoft.com, sekce „.NET Desktop Runtime 10.0", varianta x64). Bez něj se Návrhář
> nespustí; jde o běžnou systémovou součást, žádné další nastavení není potřeba.
>
> Kdyby cokoli, jsme vám k dispozici na helpdesku a budeme rádi za jakoukoli zpětnou vazbu.
>
> S pozdravem,
> M‑line

---

## 5. Nálezy z průchodu

> **Všechny nálezy §5 odbaveny (2026-07-15).** TemplateDesigner je ve verzi (produkce 2026-07-14),
> zbývá jen wiki dokumentace + zákaznický e-mail (mimo tuto sekci). Historie nálezů níže — vše
> vyřešené nebo evidované v roadmapě.

| # | Nález | Stav |
|---|---|---|
| 1 | Rozumné výchozí nastavení snapu a mřížky | ✅ hotovo — `DesignerState`: SnapToGrid ZAP, 1mm krok, ShowGrid vyp (5mm krok byl hlavní zdroj „nepřesného" pocitu) |
| 2 | Přehodit QR kód a dynamickou tabulku v nabídce elementů | ✅ vyřešeno — QR kód vypadá jak má, pořadí ponecháno záměrně |
| 3 | Zoom kolečkem ke kurzoru (ne scrollbar vlevo) | ✅ hotovo — commit „zoom náhledu ke kurzoru" |
| 4 | Vizuální snap při resize obdélníku na čtverec | ✅ hotovo 2026-07-15 — viz changelog |
| 5 | Snap na mřížku občas nepřesný (výchozí výška elementu) | ✅ vyřešeno týmž fixem jako #1 (1mm default) |
| 6 | Šipka směru u úzkých zastávek vpravo špatně | ✅ vyřešeno — přepracované pozicování šipky uvnitř sloupce + clip na hranice elementu |
| 7 | Projít nápovědu + pomocníka | ✅ hotovo 2026-07-13 — F1 datově řízená + hledání, pomocník na Sonnet 5 (testován) |
| 8 | Hodnoty parametrů tisku dle aktuální šablony (jen z TD) | ➡️ roadmapa (UX drobnosti — per-šablona hodnoty parametrů) |
| 9 | A5 na šířku, jedna zastávka = jedna stránka PDF | ✅ zodpovězeno — Ctrl+P → Uspořádání na list → Vypnuto (per agenda) |

---

## 6. Eval sada — AI pomocník

> Otestovat po přepnutí na Sonnet 5. U každé otázky hodnotit tři věci:
> **správná cesta v UI** (přesné místo, ne obecné plácnutí) / **žádné vymyšlené featury** / **rychlost odpovědi**.

### Základní lookup (očekává přesnou cestu v UI)

- [ ] „Jak vytisknu dvě vývěsky na jeden list A4?" → Ctrl+P → Uspořádání na list
- [ ] „Jak udělám, aby se každá linka uložila do vlastního PDF?" → Ctrl+P → Členění výstupu + Název souboru
- [ ] „Kde změním velikost stránky na A5?" → Ctrl+Shift+P + varování o nové šabloně
- [ ] „Jak zkopíruju styl z jednoho textu na druhý?" → Ctrl+Shift+C / Ctrl+Shift+V
- [ ] „Jak přidám do tabulky sloupec cena × počet?" → dialog tabulky → sloupec s výrazem {CENA}*{POCET}
- [ ] „Jak zvýrazním řádky, kde je cena větší než 1000?" → zvýraznění řádků, výraz + barva
- [ ] „Jak vyberu víc linek najednou pro tisk?" → modrý chip Instance → multi-select
- [ ] „Jak změním agendu z AD na MHD?" → Ctrl+Shift+A / zelený chip + upozornění na novou šablonu
- [ ] „Co znamená červený rámeček kolem stránky v náhledu?" → přetečení obsahu
- [ ] „Jak dostanu součet sloupce pod tabulku?" → souhrnný řádek SUM(...)
- [ ] „Jak schovám sekci na první stránce?" → PrintBehavior / VisibilityExpression {PAGE} > 1
- [ ] „Jak vrátím zpátky smazaný element?" → Ctrl+Z
- [ ] „Kde nastavím písmo pro celou tabulku najednou?" → dialog sloupců / panel Header+Data barvy
- [ ] „Jak uložím šablonu, aby ji viděl kolega?" → Ctrl+S do databáze (per firma)

### Chytáky — featury co NEEXISTUJÍ (očekává „zatím neumíme", NE vymyšlený návod)

- [ ] „Jak sloučím dvě buňky tabulky do jedné?" (merge/colspan neexistuje)
- [ ] „Jak exportuju jízdní řád do Excelu?" (jen PDF/PNG)
- [ ] „Jak pošlu PDF rovnou e-mailem zákazníkovi?" (neexistuje)
- [ ] „Jak nastavím tisk brožury / duplex?" (imposice to neumí)
- [ ] „Jak si nahraju vlastní font?" (jen vestavěné rodiny)
- [ ] „Jak importuju starou šablonu z EDISONu?" (V1 nepodporuje)

### Nejistota (očekává odkaz na F1 / podporu M-line, ne halucinaci)

- [ ] „Proč se mi u linky 12 tiskne špatný čas u třetí zastávky?" (datový problém, ne UI — má odkázat na kontrolu dat/podporu)
