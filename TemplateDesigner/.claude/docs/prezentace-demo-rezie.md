# Živá ukázka — podrobná režie (zastávkový JŘ)

> **Účel:** click-by-click runbook pro **živou demo** Návrháře šablon na semináři.
> Nahrazuje starou pptx (ta je odpis). Formát: **AKCE** (co udělat) · **ŘÍKÁŠ** (mluvený bod) ·
> **POZOR** (na co si dát pozor / čemu se vyhnout).
>
> **Stav featur:** bere v potaz, že **správné vykreslování negativních značek** a **FTL nastavení
> sloupců odjezdů** (sloupce jako ve starém nástroji) **jsou hotové** — v Části 2 je ukazujeme.
>
> **Rozhodni před zkouškou (defaulty níže):**
> - **Délka:** ~45–55 min živě + Q&A. *(default)*
> - **Zdroj dat:** primárně **AD JŘ** (1:1 ověřený) + krátké přepnutí na MHD. *(default)*
> - **Demo linka:** bohatá na featury — **doporučeno `0365001`** (rozdíly tras + bezbariér + časy)
>   nebo `0320144` (FTL3, víc sloupců dnů). *(uprav dle dostupné firmy)*

---

## 0. Příprava před spuštěním (10 min předem)

- [ ] **Vrátit DEV tweaky** na produkční default (firma, startup šablona) — viz gap analýza P0-1.
- [ ] **Opravit / ověřit #8** false-positive přetečení — jinak při nové šabloně bliká červený rámeček (vypadá jako bug). Pokud neopraveno, **vyhni se zakládání úplně nové prázdné šablony naživo**.
- [ ] Připojení na **Caché dev / VPN** živé (data, šablony, tisk).
- [ ] Otevřené **2 šablony**: (a) hotová zastávková JŘ šablona pro demo, (b) jedna jednoduchá na ukázku tvorby.
- [ ] Vybraná **demo linka** s: rozdíly tras (A/B), bezbariérovými spoji (H/F), omezeními platnosti, negativními značkami.
- [ ] **Zvětšené UI písmo** (EDTheme font scale) pro čitelnost na projektoru.
- [ ] **Záloha:** předem vyexportované PDF výstupy (kdyby vypadlo připojení).
- [ ] Zavřené rušivé aplikace, notifikace vypnuté.

**Rámec na úvod (řekni publiku):**
- *Ptejte se hned* na „co a jak umíme nastavit" — funkční dotazy bereme během ukázky.
- *Zapište a pošlete mailem* zásadní problém / co chybí — vezmeme strukturovaně.
- *Tohle je úvodní verze* — zastávkový JŘ je datově ověřený 1:1, technické detaily dolaďujeme.

---

## 1. Úvod a kontext (3–4 min, mluvené slovo)

> Mluvená verze — můžeš odříkat nebo parafrázovat. **Body 4 a 5 NEVYNECHAT** (viz callout níže).

**1) Přivítání + co dnes uvidí (15 s)**
> „Dobrý den, jmenuju se Jan Miesbauer z M-LINE. Dnes vám ukážu **Návrhář šablon** — nový nástroj, ve kterém se vizuálně navrhují tiskové sestavy, dnes konkrétně **zastávkové jízdní řády**. Bude to z velké části **živá ukázka přímo v aplikaci**, ne slidy."

**2) Hook — co to je a co nahrazuje (30 s)**
> „Návrhář nahrazuje starý nástroj, kde se sestava skládala přes formuláře a čísla — **bez náhledu**, výsledek jste viděli až po tisku. Tady navrhujete vizuálně a **okamžitě vidíte, co se vytiskne**. Stejný engine dělá náhled i reálný tisk — co vidíte na obrazovce, to dostanete na papíře."

**3) Formát a co očekávat (30 s)**
> „Hodina zhruba na dvě půlky: první **obecně editor** — jak se s ním pracuje — a druhá to nejdůležitější pro vás, **zastávkové jízdní řády** a jak z dat vznikne konkrétní tisk. Na konci krátká roadmapa a prostor na dotazy."

> ### ⚠️ NEZAPOMENOUT — body 4 a 5 (jediné, co od publika aktivně chceme)
>
> **4) Co mají dělat VY (řekni POMALU, 45 s):**
> > „Mám na vás dvě prosby, ať z toho vytěžíme maximum:
> > - **Ptejte se hned** během ukázky na *„co a jak umíme nastavit"* — funkční dotazy berem rovnou, je to interaktivní.
> > - Když narazíte na něco **zásadního** — co vám chybí, co je blokující — **zapište si to a pošlete mi mailem**. Ať to nezdržuje hovor a my to vezmeme strukturovaně a pořádně.
> > - A během ukázky **si všímejte rozdílů** oproti tomu, jak to děláte teď ve starém nástroji."
>
> **5) Žádost o jejich nastavení (30 s):**
> > „Budu rád, když mi po semináři **sepíšete, co z nastavení ve starém AD reálně používáte** a co je pro vás klíčové. Máme funkční úvodní verzi a technické detaily doplníme přesně podle vašich potřeb."
>
> **5b) ⭐ Prosba „nedávejte požadavky do zásoby" (30 s) — důležité naladění:**
> > „A jedna prosba k tomu feedbacku: **nedávejte nám prosím požadavky „do zásoby"** — tedy nastavení, které by se *teoreticky* někdy mohlo hodit. Záměrně to stavíme **odlehčeně** a přidáváme jen to, co reálně potřebujete. Starý nástroj měl tolik voleb, že se v něm nakonec sami těžko vyznáte. Chceme, aby nástroj **zůstal srozumitelný** — takže nám řekněte, co **opravdu používáte**, ne co by *šlo* mít. Začneme s tím podstatným a budeme přidávat podle skutečné potřeby."
>
> *(Body 4, 5 a 5b zopakuj i na konci v Q&A — viz sekce 4.)*

**6) Očekávání naladit poctivě (20 s)**
> „Důležité: tohle je **úvodní verze**. Zastávkový JŘ je **datově ověřený 1:1** proti původnímu výstupu na konkrétních linkách. Některé méně časté konfigurace ještě dolaďujeme — proto váš feedback vítáme a počítáme s ním."

**7) Přechod:** „Tak pojďme rovnou do aplikace."

**POZOR / tipy k podání:**
- **Nezačínej omluvou** („je to rozpracované…") — naladí špatně. Začni sebevědomě „ukážu vám", poctivost o úvodní verzi nech až do bodu 6.
- **Klíčová věta** (zopakuj i na konci): *„Stejná data, stejný výsledný tisk — ale návrh je teď vizuální, okamžitý a ve vašich rukou."*
- Nezabřednout do techniky. ~3–4 min, pak hned do živé aplikace.

**☑️ Checklist „co v úvodu zaznělo":** kdo jsem + živá ukázka · co nahrazuje + výhoda · formát (2 půlky) · **▶ ptejte se hned** · **▶ blokující věci mailem** · **▶ všímejte si rozdílů** · **▶ sepište co reálně používáte** · poctivě: úvodní verze + 1:1.

---

## 2. ČÁST 1 — Designer obecně (živá ukázka, ~22 min)

> Cíl: ukázat, že návrh je **vizuální, okamžitý, vratný**. Tempo svižné — tohle je rozjezd.

### 2a) Orientace v okně (2 min)
**AKCE:** ukázat toolbar → levý panel (záložky Sekce / Strom / Pole agendy / Vlastnosti) → canvas → pravý náhled → status bar.
**ŘÍKÁŠ:** „Vlevo struktura a data, uprostřed návrh, vpravo živý náhled toho, co se vytiskne."
**AKCE:** ukázat ve status baru **zelený štítek agendy** a **štítek instance (linka)**.

### 2b) Základní editace — „co dřív nešlo vidět" (6 min)
**AKCE (postupně):**
1. Přetáhni z palety **textový prvek** na canvas → ghost preview, zvýraznění sekce.
2. Z panelu **„Pole agendy"** přetáhni pole (např. `CISLO_LINKY`) → vznikne `{CISLO_LINKY}`.
3. Posuň prvek vedle jiného → ukaž **magnetické přichytávání** (vodítka, mezera 4 mm).
4. **Ctrl+D + šipka** → kopie vedle s mezerou.
5. Vyber dva prvky → **Ctrl+G** seskupit → klik vybere skupinu, **Alt+klik** jeden prvek.
6. **Ctrl+Z / Ctrl+Y** několikrát.
**ŘÍKÁŠ (u undo):** „Každá akce je vratná. Ve starém nástroji omyl v parametru znamenal projít celý formulář znovu — tady jen Ctrl+Z."
**POZOR:** nezakládej novou prázdnou šablonu (kvůli #8), edituj v připravené.

### 2c) Vlastnosti a styly (4 min)
**AKCE:** vyber prvek → záložka **Vlastnosti** → změň font, velikost, tučné, barvu, zarovnání → změna hned v náhledu.
**AKCE:** vyber víc prvků → **Enter** → společné vlastnosti najednou.
**AKCE (volitelné):** **Ctrl+Shift+C / Ctrl+Shift+V** — kopírování stylu.
**ŘÍKÁŠ:** „Styl se mění okamžitě, vidíte výsledek hned, ne až po tisku."

### 2d) Výrazy a podmíněný obsah (3 min) — *„šablona reaguje na data"*
**AKCE:** ukázat placeholder `{NAZEV}` → naformátování `{CENA:N2}` / `{NAZEV:UPPER}`.
**AKCE:** nastav **Podmíněnou viditelnost** prvku (`{PAGE} > 1`) → ukaž, že na 1. straně zmizí.
**AKCE:** napiš **chybný výraz** → ukaž **červené upozornění** (validace v UI).
**ŘÍKÁŠ:** „Jedna šablona pokryje víc variant podle dat — dřív bylo všechno napevno."

### 2e) Layout, sekce, multi-page (4 min)
**AKCE:** ukázat **sekce** v levém panelu, **drag&drop řazení**, chování tisku (hlavička každá stránka / patička).
**AKCE:** v náhledu ukaž **více stránek pod sebou**, proklikni se stránkou.
**ŘÍKÁŠ:** „Hlavičky, patičky a stránkování řeší engine sám."

### 2f) Náhled, export, šablony v Caché (3 min)
**AKCE:** **Export PDF** → otevři výsledek. Zmíň **PNG** a **PrintDialog** s reálnými daty.
**AKCE:** `Soubor → Otevřít z databáze` → ukaž **správce šablon** (filtr agendy, search).
**ŘÍKÁŠ:** „Šablony jsou uložené centrálně podle firmy — ne roztroušené po počítačích."

### 2g) Import z PDF + AI asistence (volitelné / experimentální, 4 min)
> Pozn.: prezentuj jako **pomůcku a výhled**, ne produkční slib.

**AKCE (PDF import):**
1. `Soubor → Import z PDF` → **PdfImportDialog**.
2. Ukázat **3 režimy grafiky** (žádná / jen čáry / vše), detekci okrajů, auto-seskupení textů do bloků.
3. Spustit import → z PDF vzniknou prvky (texty, čáry, obrázky) jako základ šablony.

**AKCE (AI asistence — pokud je API klíč):**
1. V dialogu zapnout **AI enrichment** + zvolit agendu → dvoustupňový pipeline: heuristika (geometrie) → AI (sémantika).
2. Ukázat **automatické přiřazení databázových polí** (při vysoké jistotě se aplikují samy).
3. Zmínit **AI Vision** — rozpoznání struktury JŘ z obrázku stránky (rasterizace → Claude Vision → přesné prvky z geometrie PDF).

**ŘÍKÁŠ:** „Z existujícího PDF umíme předpřipravit šablonu. S AI navíc pozná, co je co — texty, pole, strukturu JŘ. Bez API klíče funguje aspoň heuristika. Berte to jako akcelerátor návrhu, dolaďujeme to."
**POZOR:** AI naživo je riziko (síť, API klíč) — mít **zálohní screenshot/výsledek**; klidně přeskoč, pokud čas/připojení nejisté.

---

**Most do Části 2:** „To byl obecný editor. Teď to nejdůležitější — jak z dat jízdního řádu vznikne konkrétní zastávkový tisk."

---

## 3. ČÁST 2 — Zastávkové JŘ a business logika (živá ukázka, ~23 min)

> **Jádro pro provozní uživatele.** Sem patří obě nové věci: **vykreslení NZ** a **FTL sloupce odjezdů**.

### 3a) Tři specializované prvky (3 min)
**AKCE:** na demo šabloně ukázat 3 prvky: **tabulka zastávek**, **mřížka odjezdů**, **legenda poznámek**.
**ŘÍKÁŠ:** „Tyhle tři prvky rozumí datům JŘ — vy řešíte vzhled, logiku spočítá systém."

### 3b) Zdroj dat a výběr linky (3 min)
**AKCE:** **Ctrl+Shift+A** (nebo klik na štítek) → ukázat agendy → zůstaň na **AD JŘ**.
**AKCE:** otevřít **výběr linek (instance)** → vyhledat, ukázat **multi-výběr**.
**AKCE:** přepnout instanci (◀▶) → náhled se přerenderuje na jinou linku.
**ŘÍKÁŠ:** „Stejný designer, dva zdroje dat. Linku přepnu a hned vidím jiná data — bez znovuotevírání."

### 3c) Tabulka zastávek (4 min)
**AKCE:** otevřít **editor sloupců** → pořadí, šířky, viditelnost, **styl per sloupec**.
**AKCE:** ukázat **atributy u názvu** (na znamení, automat), **šipku směru jízdy**, zvýraznění aktuální zastávky.
**ŘÍKÁŠ:** „Sloupce si poskládáte, jak potřebujete pro danou linku."

### 3d) ⭐ Mřížka odjezdů — FTL sloupce + NZ (6 min) — *NOVÉ, klíčový moment*
**AKCE:**
1. Otevři **nastavení sloupců odjezdů** → ukaž **FTL nastavení** — sloupce skupin dnů (Prac / So / Ne+svátky / prázdniny…) **jako ve starém nástroji**.
2. Přenastav sloupce (pořadí / viditelnost / nadpis) → ukaž změnu v náhledu.
3. V mřížce ukaž **anotace u minut**: týdenní platnost, **negativní značky (×, ⊘) — správně vykreslené** (inverzní, bílé na černém), bezbariér (H/F), rozdíly tras (A/B).
**ŘÍKÁŠ:**
- u FTL: *„Tohle je přesně to, co šlo nastavit ve starém nástroji — rozvržení sloupců dnů — a teď to jde taky, ale vizuálně a s živým náhledem."*
- u NZ: *„Negativní značky se vykreslují přesně jako v originále."*
**POZOR:** tohle je highlight — nech tomu prostor, je to přímá odpověď na „uměl to starý nástroj?".

### 3e) Co engine řeší automaticky (5 min)
**ŘÍKÁŠ:** „Tohle nepíšete ručně — počítá se z dat:"
**AKCE (ukázat na náhledu):**
- **Omezení platnosti** „jede / nejede" (text dle data).
- **Rozdíly tras** — písmena A/B + text v legendě („neobsluhuje zast. X", „končí v zast. Y", „jede až do…").
- **Bezbariérovost** spojů.
- **Dopravce** — popis + adresa provozovny.
- **Legenda poznámek** — naplní se sama z toho, co je na stránce.
**ŘÍKÁŠ:** „Datově ověřeno 1:1 proti původnímu tisku na konkrétních linkách."

### 3f) Legenda poznámek (2 min)
**AKCE:** ukázat **víc sloupců**, **řazení dle délky**, zarovnaný marker.
**ŘÍKÁŠ:** „Legenda se sestaví sama a uspořádá tak, aby byla čitelná."

**Most na závěr:** „A celé tohle se dá vytisknout dávkově — jedna šablona přes všechny zastávky a směry."

### 3g) Dávkový tisk (2 min)
**AKCE:** **PrintDialog** s reálnými daty → ukaž **členění výstupu** (vše / per linka / per zastávka×směr) + název souboru z polí.
**ŘÍKÁŠ:** „Jedna šablona × stovky stránek — linka × zastávky × oba směry — do jednoho běhu."

---

## 4. Roadmapa + Q&A (5 min)

**Co je hotové (krátce):** zastávkový JŘ z AD dat 1:1, šablony v Caché per firma, dávkový tisk.

**Co se chystá (upřímně, NEslibovat termín):**
- **Multi-varianta časů tras** (víc časových sloupců vedle sebe) — *jediná velká chybějící věc, na roadmapě*.
- Barevné odlišení časů dle prahu, cizojazyčné poznámky, drobnosti.

**Připravené odpovědi (FAQ):**
- *„Můžeme si šablonu upravit sami?"* → Ano, to je celý smysl — vizuálně + uložení do databáze.
- *„Co se starými šablonami?"* → V1 neimportuje 1:1; nové se navrhnou znovu (rychle, WYSIWYG).
- *„Bude to vypadat stejně?"* → U AD JŘ datově ověřeno 1:1; vizuál je nový engine, obsah a rozložení sedí.
- *„Uměl to starý nástroj?"* (sloupce dnů) → Ano — a my to teď umíme taky, ukázal jsem to (FTL).
- *„Jak se to k nám dostane?"* → Integrované v EDISONu, tisk stejným enginem jako náhled.

---

## 5. Čeho se při živém demu vyvarovat

- **Nezakládat novou prázdnou šablonu** (#8 false-positive přetečení — červený rámeček vypadá jako bug), dokud není opraveno.
- **Neslibovat** multi-variantu časů tras jako hotovou — je to roadmapa.
- **Nezačínat** úpravy, které neznáš naživo — drž se nacvičené sekvence.
- Při výpadku připojení → přepnout na **zálohní PDF**.
- Nepouštět se do interních pojmů (FTL, Caché, gold oracle) směrem k publiku — říkat „rozvržení sloupců dnů", „databáze", „ověřeno proti původnímu tisku".

---

## 6. Rychlý nácvik (checklist na sucho)

- [ ] Projít celou sekvenci 1× na demo lince, stopnout čas.
- [ ] Ověřit, že **FTL sloupce odjezdů** i **NZ vykreslení** v demu fungují (pozn.: vyžaduje pushnout práci z druhého PC + pullnout sem, jinak běží stará verze).
- [ ] Vyzkoušet PDF export + PrintDialog s reálnými daty.
- [ ] Připravit 1 linku, kde je vidět rozdíl tras + bezbariér + omezení **najednou**.
- [ ] Nachystat zálohní PDF.

---

# Příloha A — Úplný katalog funkcí (vše, co umíme)

> Vyčerpávající seznam pro jistotu, že v demu nic nechybí. Označ ✅ ukázat / ⏭️ přeskočit.
> Scény výše jsou doporučený výběr; tady je **kompletní plocha**.

### A1. Typy prvků (elementy)
- [ ] **Text** — statický i s poli `{POLE}`, word-wrap, dynamická výška, řádkování (LineSpacing)
- [ ] **Obrázek** — nahrání s auto-downscale (max 5 MB), kvalitní škálování (SKImage)
- [ ] **Obdélník** — výplň, pozadí, rámeček
- [ ] **Čára** — vodorovná/svislá, vlastní čárkování/tečkování (dash/gap)
- [ ] **Kód** — QR, Code128, **EAN-13, EAN-8, PDF417, DataMatrix** (1 tlačítko „Kód")
- [ ] **Tabulka** — Fixed (pevné řádky) i Dynamic (řádky z dat)
- [ ] **Zastávkové prvky** — tabulka zastávek, mřížka odjezdů, legenda poznámek (viz A9)

### A2. Editace a manipulace
- [ ] Drag&drop z palety (ghost preview) · drag pole agendy → `{POLE}`
- [ ] Výběr: klik, **rubber-band** (tažení rámečku), Ctrl+klik, Ctrl+A (vše v sekci)
- [ ] **Magnetické přichytávání** — hrany, středy, mezera 4 mm, vodítka, indikátor; snap i při resize
- [ ] **Chytré duplikování** — Ctrl+D + šipka (vedle s mezerou) / Ctrl+D (offset)
- [ ] **Seskupování** — Ctrl+G / Ctrl+Shift+G, Alt+klik = jeden prvek
- [ ] **Rozdělení prvku** — Ctrl+Shift+H (vodorovně) / Ctrl+Shift+J (svisle)
- [ ] **Zarovnání + rozmístění** více prvků (align / distribute)
- [ ] **Z-order** (do popředí / pozadí)
- [ ] Cut/Copy/Paste (Ctrl+X/C/V) · **kopírování stylu** (Ctrl+Shift+C/V)
- [ ] **Click-vs-drag práh** (4 px) — pouhý klik nestrhne pozici na grid
- [ ] Undo/Redo (Ctrl+Z/Y) — 12+ typů příkazů
- [ ] **Context menu** (pravý klik) — prvek + sekce v jednom

### A3. Vlastnosti a styly
- [ ] **Inline panel Vlastnosti** + modální dialog (Enter / dvojklik)
- [ ] Font, velikost, **tučné / kurzíva / podtržení / přeškrtnutí**, barva textu + pozadí
- [ ] Zarovnání **vodorovné i svislé** (H+V), řádkování
- [ ] **Rámeček** (barva + tloušťka), sdílené hrany = jeden rámeček, pozadí prvku
- [ ] **Kaskáda stylů** (sloupec → prvek → defaults) — tri-state „dědit" s šedým placeholderem zděděné hodnoty + „×" reset
- [ ] **Hromadná editace** více prvků (Enter na multi-select)

### A4. Tabulky
- [ ] **Fixed** — sloupce i řádky ručně, buňky z polí `{POLE}`
- [ ] **Dynamic** — sloupce ručně, řádky z datové kolekce (DataSource)
- [ ] Editor sloupců — pořadí, šířky, záhlaví, viditelnost, **per-sloupec styl** (sdílený CellStyleEditor)
- [ ] **Agregace** v souhrnných řádcích — SUM / COUNT / AVG / MIN / MAX

### A5. Výrazový jazyk
- [ ] Placeholdery `{POLE}` kdekoli v textu
- [ ] Formátování — `{POLE:N2}`, `{POLE:UPPER}`, `{POLE:LEFT(4)}`, datum/čas dle jazyka
- [ ] Podmíněná viditelnost — `{POCET}>0`, `{PAGE}>1`; operátory `&&`, `||`, `!`
- [ ] **Validace výrazu v UI** — červené upozornění u chyby

### A6. Sekce a layout
- [ ] **Role** — Header / Body / Footer (kam se umístí)
- [ ] **Chování tisku** — Jednou / Každá stránka / Jen první / Jen poslední / Kromě první
- [ ] **Přetečení** — Nepřerušovat / Rozdělit přes stránky
- [ ] Drag&drop řazení sekcí, **redistribuce výšek** při přidání/smazání
- [ ] **Vlastní šablony sekcí** (uložit a vkládat opakovaně)
- [ ] **Skrytí záhlaví sekcí** (toggle; auto u JŘ šablon)
- [ ] Pevná výška canvasu, patičky kotvené ke spodku
- [ ] Potvrzení mazání sekce

### A7. Stránka a šablona
- [ ] **PageSize editor** — formáty (A2–A6…), orientace, okraje
- [ ] **Škálování šablony** 50–200 % (proporcionálně vše)
- [ ] **Normalizace pozic** (Ctrl+Shift+N) — srovná float artefakty na grid
- [ ] **Velikost mřížky** 1 / 2 / 5 / 10 mm; zapnutí/vypnutí gridu a snapu
- [ ] **Jazyk šablony** (cs-CZ / sk-SK / en-US / de-DE) — formát čísel/datumů nezávislý na Windows
- [ ] **Parametry sestavy** (definice + serializace)
- [ ] **Per-šablona předvolby editoru** (grid, snap — přežijí reload)

### A8. Náhled, export, tisk
- [ ] **Živý náhled** s reálnými mock daty (filtruje viditelnost)
- [ ] **Multi-page** náhled (stránky pod sebou)
- [ ] **Fit-to-Page** (Ctrl+0), zoom
- [ ] Export **PDF** i **PNG**
- [ ] **PrintDialog** — parametry + reálná data z databáze
- [ ] **Multi-data** — jedna šablona × více dat = jeden PDF (linka × zastávky × směry)
- [ ] **Členění výstupu** (vše / per linka / per zastávka×směr) + **název souboru z polí**
- [ ] **Varování o přetečení** (červený rámeček) — *pozn. P0: hlídat false-positive #8*

### A9. Zastávkové jízdní řády (jádro)
- [ ] **Tabulka zastávek** — sloupce (pořadí/šířky/viditelnost/styl), atributy u názvu, šipka směru, zvýraznění aktuální/projeté zastávky, časy jízdy
- [ ] **Mřížka odjezdů** — skupiny dnů jako sloupce, **FTL nastavení sloupců** (jako ve starém), sloupec hodin (vlevo / per skupina), redistribuce šířek, word-wrap záhlaví, zebra řádky
- [ ] **Anotace u minut** — týdenní platnost, **negativní značky (správné vykreslení, inverzní)**, bezbariér H/F, rozdíly tras A/B, barevná pravidla
- [ ] **Časová osa** — Compact / DataBounded / Range (i přes půlnoc) / Full
- [ ] **Legenda poznámek** — 1–4 sloupce, řazení dle délky, směr wrapu, zarovnaný marker, filtr per typ
- [ ] **Automatická logika** — omezení platnosti „jede/nejede", rozdíly tras + texty, bezbariér, dopravce (popis+adresa), auto-naplnění legendy
- [ ] **Auto-scale fontu** při pevné výšce

### A10. Data a agendy
- [ ] **Přepínač agendy** (Ctrl+Shift+A / štítek) — MHD ↔ AD JŘ ↔ obecná
- [ ] **Výběr linek (instance)** — vyhledávání, multi-výběr, ◀▶ přepínání
- [ ] **Panel „Pole agendy"** — databázová pole, drag na canvas
- [ ] **Strom šablony** — hierarchický přehled (sekce → skupiny → prvky)

### A11. Úložiště a stav
- [ ] **Šablony v databázi** — otevřít / uložit / přejmenovat / smazat, filtr agendy, search; per firma
- [ ] **Per-uživatel stav** — zoom, mřížka, naposledy otevřené soubory, naposledy použité barvy, parametry tisku, výchozí hodnoty instance
- [ ] Export šablony jako **soubor (JSON)** — poslat kolegovi/dopravci

### A12. AI a import (experimentální)
- [ ] **Import z PDF** — extrakce textů/čar/obrázků, 3 režimy grafiky, auto-seskupení
- [ ] **AI enrichment** — sémantické rozpoznání + auto-přiřazení DB polí (při jistotě), graceful fallback bez klíče
- [ ] **AI Vision** — rozpoznání struktury JŘ z obrázku stránky → přesné prvky

### A13. UX detaily
- [ ] **Ikonový toolbar** (Fluent UI, 7 skupin)
- [ ] **Klávesové zkratky** (kompletní tabulka — viz `ux-conventions.md`)
- [ ] **UI font škálování** (sdílená preference z EDISONu, bez restartu)
- [ ] Tooltipy u všech ikon

---

> **Tip pro nácvik:** projdi katalog, ✅ označ „must show", zbytek ⏭️. Pak nech v hlavních scénách (2/3)
> jen ✅ položky a drž se časování. Katalog slouží jako pojistka „na nic jsem nezapomněl" + zásobník
> pro dotazy publika („a umí to…?" → najdeš v příloze).

---

# Příloha B — Zastávkový JŘ z business pohledu (hloubkový rozbor)

> Tohle je jádro pro provozní uživatele: **co v jízdním řádu vidím, co si můžu nastavit, a co
> systém spočítá sám.** Psáno byznysově — pro vysvětlení publiku.

## B1. Tři prvky — co vidím / co nastavím / co je automaticky

### Tabulka zastávek (seznam zastávek shora dolů)
**Co vidím:** zastávky v pořadí trasy, u každé čas jízdy, případně atributy a šipku směru jízdy.

**Co si nastavím (business):**
- **Které sloupce** a v jakém pořadí — název zastávky, čas jízdy, tarifní číslo, zóna, trasa…; šířky, viditelnost, styl per sloupec.
- **Kde jsou značky** u názvu — **za názvem** (styl CB_DP) vs **před názvem** (styl DPKV). *Typická per-dopravce odlišnost.*
- **Šipka směru jízdy** — vlevo/vpravo, barva, tloušťka.
- **Zvýraznění aktuální zastávky** — tučně / podtržení / podbarvení (zastávka, ze které JŘ platí).
- **Zobrazit projeté zastávky** (před aktuální) a jejich barvu.

**Co je automaticky:** kumulativní **časy jízdy** (z chronometráže), **atributy zastávky** z číselníku, filtr „na znamení", umístění šipky od aktuální zastávky dolů.

### Mřížka odjezdů (minuty po hodinách a skupinách dnů)
**Co vidím:** sloupce = **skupiny dnů** (Pracovní / Sobota / Neděle+svátky…), řádky = hodiny, v buňkách minuty, u nich horní indexy (anotace).

**Co si nastavím (business):**
- **Rozvržení sloupců dnů (FTL)** — kolik a jaké skupiny dnů, jako ve starém nástroji *(klíčová featura — viz scéna 3d)*.
- **Pořadí / nadpisy / viditelnost** jednotlivých sloupců skupin.
- **Sloupec hodin** — jeden vlevo, nebo u každé skupiny dnů.
- **Časová osa** — jen hodiny s odjezdy / pevný rozsah (např. 4–23) / celých 24 h / od první do poslední.
- **Zebra** (střídavé podbarvení řádků), styl písma minut a anotací.

**Co je automaticky:** rozvržení sloupců dle FTL, **všechny anotace u minut** (viz B2), redistribuce šířek na celou šířku, výpočet výšky řádků.

### Legenda poznámek (vysvětlivky pod mřížkou)
**Co vidím:** seznam značek a jejich významů.

**Co si nastavím (business):** počet **sloupců (1–4)**, **řazení** (dle délky textu), směr plnění, zarovnaný marker, **filtr co zobrazit** (negativní značky / speciální znaky / textové poznámky).

**Co je automaticky:** legenda se **naplní sama** z toho, co je na konkrétní stránce; **deduplikace**; negativní značky se vykreslí **inverzně** (bílé na černém).

---

## B2. Typy poznámek a anotací — co znamenají

Anotace = malé značky/horní indexy u minut a u zastávek. Tady je celá typologie (co uživatel uvidí a co to znamená):

| # | Typ | Kde | Příklad | Co znamená |
|---|---|---|---|---|
| 1 | **Týdenní platnost** | u minuty | Po, So, Ne | spoj jede jen v určité dny |
| 2 | **Negativní značka** | u minuty + legenda | ×, ⊘ | spoj **nestaví všude** / má omezenou trasu (inverzně vykreslené) |
| 3 | **Rozdíl trasy** | u minuty (písmeno) + legenda | A, B, C | spoj jede **odlišnou trasou** — text v legendě: „neobsluhuje zast. X", „končí v zast. Y", „jede přes…", „jede až do…" |
| 4 | **Bezbariérovost** | u minuty + legenda | H, F | **H** = bezbariérově přístupné, **F** = částečně, nutná dopomoc |
| 5 | **Speciální znaky spoje** | u minuty | ikonové znaky | např. přípoj, zvláštní režim spoje |
| 6 | **Textové poznámky spoje** | u minuty + legenda | libovolný text | volná poznámka přiřazená spoji |
| 7 | **Ruční poznámky** (AD) | u minuty + legenda | komentář / směrové | uživatelem zadané — komentáře, směrové (tam/zpět/vše) |

**Atributy u zastávek** (vedle názvu v tabulce zastávek — ne u minut):

| Znak | Význam |
|---|---|
| × (x) | zastávka **na znamení** |
| { | jízdenkový automat |
| (ikona) | přestup na železniční dopravu |

> **Pro publikum:** „Značky u minut říkají něco o **spoji** (kdy jede, jakou trasou, bezbariér).
> Značky u názvu zastávky říkají něco o **zastávce** (na znamení, automat)."

---

## B3. Co systém počítá automaticky (uživatel nepíše ručně)

Tohle je silné sdělení — **byznys logika je v enginu**, uživatel řeší jen vzhled:

- **Časy jízdy** — kumulativně z dob jízdy mezi zastávkami.
- **Rozdíly tras** — porovná trasu každého spoje se vzorovou a sám přiřadí písmena (A/B…) + vygeneruje text do legendy („neobsluhuje / končí v / jede přes / jede až do").
- **Negativní značky** — odvodí z toho, kde spoj staví a kde ne.
- **Bezbariérovost** — pokud všechny spoje na lince jsou bezbariérové, zobrazí souhrn „všechny spoje bezbariérové"; jinak per spoj.
- **Omezení platnosti** — textová omezení dle data („nejede 24.–31. 12.", „jede také 28. 7.").
- **Dopravce** — popis + adresa provozovny z dat.
- **Skupiny dnů → sloupce** mřížky (rozvržení dle FTL).
- **Legenda** — sběr všech značek na stránce, deduplikace, sestavení.

> **Klíčová věta na seminář:** *„Vy řešíte vzhled — které sloupce, jaký font, kde značky. Co která
> značka znamená, jakou trasou spoj jede, kdy nejede — to spočítá systém z dat. Datově ověřeno 1:1
> proti původnímu tisku."*

---

## B4. Per-dopravce odlišnosti (pokud na ně dojde řeč)

Některá nastavení existují kvůli tomu, že každý dopravce tiskne JŘ trochu jinak:
- **Pozice značek** u zastávky — za názvem (CB_DP) vs před názvem (DPKV).
- **Šipka směru** — vlevo (mimo sloupec) vs vpravo (u hrany sloupce).
- **Co v legendě skrýt** — např. „× na znamení" se skryje, když se značka v tabulce netiskne; souhrn bezbariérovosti lze vypnout.

> Tohle ukazuje, že nástroj **není napevno** — per dopravce/linku se vzhled přizpůsobí přes nastavení,
> ne přes zásah do kódu.

---

# Příloha C — Vyřešené helpdesk tickety (projít na konci semináře)

> **Jak to podat:** tyhle požadavky z helpdesku **nebudu ukazovat naživo na datech** — vyžadují
> konkrétní linky/data daného zákazníka. Ale **máme je vyřešené** a budeme je **uzavírat na
> helpdesku**. Cíl: dát najevo, že podněty zákazníků reálně řešíme a posouváme.
>
> **Návrh formulace pro seminář:**
> > „Část vašich podnětů z helpdesku už máme hotovou. Nebudu je ukazovat naživo — vyžadují konkrétní
> > linky a data — ale vyřešené jsou a budeme je na helpdesku **uzavírat**. Pokud budete chtít, dolaďíme
> > je ještě na vašich reálných datech."

## Vyřešeno (převážně AD agenda, zákazník TRANSDEV)

| Ticket | Zákazník | Požadavek (krátce) | Jak je vyřešeno |
|---|---|---|---|
| **#7947** | TRANSDEV (Podloučka) | matoucí „pokračuje přes zast. X" + zbytečné písmenkování spojů do koncové | text **„jede až do zast. X"** + odstraněno zbytečné písmenkování |
| **#7955** | TRANSDEV (Podloučka) | u vybraných spojů chtěl **jen vlastní komentář** (ne automatiku zároveň) | přepínač **„co schovat v rozsahu"** (Ruční / Automatické / Obojí) + komentáře + směrové poznámky |
| **#7161 D5** | TRANSDEV (Šimko) | negenerovat „× — na znamení" do legendy, když je sloupec se značkou vypnutý | **auto-skrytí** řádku legendy podle viditelnosti značky |
| **#7161 D6** | TRANSDEV (Šimko) | vypínač pro souhrn „všechny spoje bezbariérové" | parametr **„skrýt agregát bezbariér"** |
| **#7959** | TRANSDEV (Podloučka) | zastávka, kam z aktuální nejede žádný spoj, nemá být v seznamu (školní spoj) | **filtr neobsluhovaných zastávek** + přeskočení stránky — *(formulovat jako „máme připravené, doladíme na vašich datech")* |
| **#10660** | DPKV (Slavík) | podbarvení aktuální zastávky místo podtržení | **už podporováno** (vypnout podtržení + nastavit barvu pozadí); ticket jinak zůstává suspended |

## ⚠️ Poctivé výhrady (NEpodávat jako 100% hotové)
- **#7955 — pod-položka „výběr spojů jde jen jednociferně"** je bug v **EDISON Studiu JŘ**, ne v Návrháři → neukazovat jako vyřešené, předat zvlášť.
- **#7959** funguje, ale vedeme jako **„ověřit na reálných TRANSDEV datech"** → formulovat jako „připraveno k ověření", ne „hotovo a uzavřeno".
- Vše vyřešeno v **AD agendě** (TRANSDEV jezdí linkově); MHD modul může mít drobné odlišnosti.

## ❌ Otevřené / mimo scope (NEzmiňovat jako vyřešené)
- **#7937** (TRANSDEV — odsazení zastávky per směr) — odloženo (chybí datový tok).
- **#7487** (KORID) — týká se knižních/linkových JŘ, ne zastávkových.
- **#11300, #8828, #8772, #9036** — nepotvrzeno / neřešeno.

> **Pozn. pro nácvik:** tyto věci jsou reálně hotové v kódu, ale **nepushnuté** (stranded na druhém PC).
> Pro skutečné uzavření na HD / doladění na datech bude potřeba je nejdřív pushnout.
