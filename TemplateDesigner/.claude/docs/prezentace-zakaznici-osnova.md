# Technická prezentace pro zákazníky — osnova (60 min)

> **Interní podklad pro prezentujícího.** Mix publika: vedoucí provozu + koncoví uživatelé.
> Tón: hands-on, technicky konkrétní, ne high-level marketing. Půlka = živá ukázka editoru,
> půlka = zastávkové elementy + business logika JŘ.
> Datum přípravy: 2026-06-22

---

## Cíl prezentace

1. Ukázat **nový TemplateDesigner** jako náhradu staršího nástroje postaveného v InterSystems Caché/Globals.
2. Demonstrovat **co umíme lépe** — vizuální WYSIWYG editace místo "naslepo do globálů".
3. Projít **zastávkový JŘ** — jak se z business logiky jízdních řádů konfigurují konkrétní elementy.

**Klíčové sdělení:** *"Stejná data, stejný výsledný tisk — ale návrh šablony je teď vizuální, okamžitý a v rukou uživatele."*

---

## Časový rozvrh

| Čas | Blok | Forma |
|---|---|---|
| 0:00–0:05 | Úvod a kontext | mluvené slovo + 1 slide |
| 0:05–0:32 | **Část 1** — Designer obecně, živá editace, srovnání se starým nástrojem | živá ukázka |
| 0:32–0:55 | **Část 2** — Zastávkové elementy a business logika JŘ | živá ukázka |
| 0:55–1:00 | Roadmapa + Q&A | mluvené slovo |

> Rezerva: bloky 1 a 2 mají vždy ~3 min polštář na dotazy během ukázky.
> **Pozn.:** obsahu je víc než času — body označené *(volitelné)* přeskoč podle publika a tempa.

---

## Jak prezentaci podat (interní)

> Toto řekni **na úvod** (rámec) a klíčovou žádost **zopakuj na konci**.

**Pravidlo pro publikum — jednoduché, ať se call nerozjede:**
- **Ptejte se hned** na *"co a jak umíme nastavit"* — funkční dotazy bereme interaktivně během ukázky.
- **Zapište a pošlete mailem** zásadní problém / něco co vám chybí / blokující připomínku — ať to nezdržuje a my to vezmeme strukturovaně a pořádně.

**Očekávání nastavit hned na začátku:**
- Tohle je **úvodní verze**. Zastávkový JŘ z AD dat je **datově ověřený 1:1** proti původnímu výstupu (na konkrétních linkách), ale technické detaily a méně časté konfigurace dolaďujeme — proto váš feedback vítáme a počítáme s ním.

**Žádost o jejich staré AD nastavení** (řekni na začátku → ať si během ukázky všímají rozdílů → zopakuj na konci):
> *"Budeme rádi, když nám sepíšete, co z nastavení ve starém AD reálně používáte a co je pro vás klíčové. Máme funkční úvodní verzi, technické věci pak doplníme podle vašich potřeb."*

Konkrétní otázky pro ně (ať nepíšou prózu):
1. Která **nastavení / přepínače** ve starém AD reálně používáte?
2. Je něco, **bez čeho se neobejdete** (must-have)?
3. Máte **vzorové JŘ výstupy**, na kterých ověříme shodu 1:1?
4. Liší se něco **mezi vašimi linkami / provozy**, co musíme podchytit?

---

## 0:00–0:05 — Úvod a kontext

**Mluvené body:**
- Co je TemplateDesigner: WYSIWYG editor tiskových šablon → výstup PDF.
- Dvě vrstvy systému: **1) Designer** (návrh šablony) → **2) Print Engine** (šablona + data = PDF).
- Co nahrazuje: starší nástroj pro návrh poznámek/sestav v Caché/Globals — návrh se dělal přes
  formuláře a číselné parametry, bez náhledu, výsledek se viděl až po tisku.
- Stejný tiskový engine pro: náhled v designeru (mock data), tisk z EDISONu (reálná data), i server/API.

**Co NEslibovat:** není to Word/Excel; sekce jsou povinná struktura; V1 neimportuje staré šablony z Globálů 1:1.

---

## 0:05–0:32 — ČÁST 1: Designer obecně (živá ukázka)

> **Příprava:** mít otevřenou rozpracovanou šablonu zastávkového JŘ + jednu prázdnou pro ukázku tvorby.
> Mít připojení na Caché (dev server / VPN) kvůli reálným datům a šablonám v Caché.

### 1a) Orientace v okně (2 min)
- Toolbar (ikonový, Fluent UI), levý panel (sekce / strom / pole agendy / vlastnosti), canvas, pravý náhled (preview).
- **Agenda chip** ve status baru — ukázat že šablona je vázaná na agendu (MHD / AD JŘ).
- Status bar: zoom, mřížka, počet stránek, varování o přetečení.

### 1b) Základní editace — "co dřív nešlo vidět" (6 min)
- **Přidat element** z palety (text, obrázek, čára, obdélník, čárový kód, tabulka).
- **Drag & drop** elementu z palety na canvas — ghost preview, zvýraznění cílové sekce.
- **Drag pole agendy** z levého panelu → vytvoří `{POLE}` placeholder.
- **Smart snap** — přichytávání hran/středů, mezera 4 mm, indikátor.
- **Chytré duplikování** — Ctrl+D + šipka umístí kopii vedle s mezerou.
- **Seskupování elementů** (Ctrl+G / Ctrl+Shift+G) — víc elementů jako jeden celek (badge, bounding box); klik vybere skupinu, Alt+klik jednotlivý prvek.
- **Undo/Redo** (Ctrl+Z/Y) — zdůraznit: každá akce je vratná (12+ typů příkazů).
  > *Srovnání: ve starém nástroji omyl v parametru = znovu projít formulář, žádné "zpět".*

### 1c) Vlastnosti a styly (5 min)
- **Inline panel "Vlastnosti"** — okamžitá editace vybraného elementu.
- **Styly textu** — font, velikost, tučné/kurzíva, podtržení, přeškrtnutí, barvy, zarovnání (H+V).
- **Kaskáda stylů** u tabulek — base barvy v panelu (rychlý přístup) × detailní per-sloupec v dialogu.
- **Rámeček + pozadí** na každém elementu; sdílené hrany se kreslí jako jeden rámeček.
- **Multi-element edit** — Enter na výběr více elementů → společné vlastnosti najednou.
- *(volitelné)* **Kopírovat/vložit styl** (Ctrl+Shift+C / Ctrl+Shift+V) — přenese font + rámeček + pozadí na jiný element.

### 1d) Výrazový jazyk a podmíněný obsah (3 min) — *"šablona reaguje na data"*
- **Placeholdery** `{POLE}` kdekoliv v textu.
- **Formátování v placeholderu** — `{POLE:N2}` (2 desetinná), `{POLE:UPPER}`, `{POLE:LEFT(4)}`, datum/čas dle `CultureCode`.
- **Podmíněná viditelnost** (`VisibilityExpression`) — element/sekce se zobrazí jen když platí podmínka (`{POCET} > 0`, `{PAGE} > 1`); podporuje `&&`, `||`, `!`.
- **Agregace** v souhrnných řádcích tabulky — `SUM`, `COUNT`, `AVG`, `MIN`, `MAX`.
  > *Srovnání: dřív napevno; teď jedna šablona pokryje víc variant podle dat.*

### 1e) Tabulky Fixed / Dynamic (2 min)
- **Fixed** — pevná struktura, buňky plní `{PLACEHOLDER}` ze skalárů (např. záhlaví linky, platnost, dopravce).
- **Dynamic** — sloupce nadefinuju, řádky se generují z dat (iterace kolekce).
- Editor sloupců, per-sloupec styl; tytéž principy stylování jako u JŘ elementů (Část 2).

### 1f) Layout a struktura (5 min)
- **Sekce** — hlavička / tělo / patička; chování tisku (každá stránka / jen první / jen poslední / kromě první).
- **Drag & drop řazení sekcí**, redistribuce výšek při přidání/smazání.
- **Vlastní šablony sekcí** — často používanou sekci uložím a vkládám ji opakovaně (úspora času).
- **Lokalizace** — `CultureCode` (cs-CZ, sk-SK, en-US, de-DE): formátování čísel/datumů nezávislé na Windows.
- **Škálování šablony** — dropdown 50–200 %, proporcionálně škáluje vše.
- **Normalizace pozic** (Ctrl+Shift+N) — srovná float artefakty na grid.

### 1g) Náhled, export, multi-page (4 min)
- **Live preview** — pravý panel, reálná mock data z agendy, filtruje `VisibilityExpression`.
- **Multi-page** — stránky pod sebou, stránkování řeší engine (hlavičky/patičky, dělení tabulek).
- **PDF / PNG export**, **PrintDialog** s parametry a reálnými daty z Caché.
- **Varování o přetečení** — červený rámeček, když se obsah nevejde.
- *(volitelné — dle času)* **Import z PDF** — z existujícího PDF se vytáhnou texty/čáry/obrázky jako elementy (volitelně s AI asistencí). Zatím vnímat jako pomůcku/výhled, ne produkční slib.

### 1h) Šablony v Caché (3 min)
- **Otevřít / uložit z Caché** — per-firma (IDfa) storage, ne lokální soubory.
- **Správce šablon** — filtr dle agendy, search, přejmenovat/smazat.
- **Per-user preference** — zoom, mřížka, recent files/colors, parametry tisku — uloženo per uživatel × firma.
  > *Srovnání: centrální, sdílené, verzované — ne roztroušené konfigurace po stanicích.*

**Shrnutí Části 1 (1 min):** vizuální návrh, okamžitý náhled, vše vratné, šablona reaguje na data, uloženo centrálně v Caché.

---

## 0:32–0:55 — ČÁST 2: Zastávkové elementy a business logika JŘ

> **Toto je jádro pro provozní uživatele.** Ukázat na reálné lince (mít připravenou linku s rozdíly tras,
> bezbariérovými spoji a omezeními platnosti, aby bylo co demonstrovat).

### 2a) Tři specializované elementy JŘ (4 min)
Vysvětlit, že zastávkový JŘ se skládá ze tří "chytrých" elementů, které rozumí datům JŘ:

| Element | Co zobrazuje |
|---|---|
| **StopListElement** | tabulka zastávek (názvy, časy jízdy, atributy, šipka směru) |
| **DepartureGridElement** | mřížka minut odjezdů po skupinách dnů (sloupce = pracovní den/so/ne…) |
| **NotesLegendElement** | legenda poznámek pod mřížkou |

- Každý má ~30+ konfigurovatelných vlastností; auto-scale fontu při pevné výšce.

### 2b) Odkud data jsou — dva zdroje (3 min)
- **Agenda MHD** (`ZastavkoveJrMhd`) — z MHD modulu (LSO/ZST/CHR/ENUMS).
- **Agenda AD JŘ** (`ZastavkoveJrAd`) — z hlavního JŘ modulu (čte `^JRL`/`^JRZ`/`^JRSET`),
  ověřeno **1:1 proti legacy gold oracle**.
- **Přepínač agendy** (Ctrl+Shift+A / chip ve status baru).
- **Instance picker** — multi-výběr linek, dávkový tisk (stovky stránek najednou).

### 2c) StopList — co umíme nastavit (4 min)
- **Sloupce** přes `StopColumnsEditorDialog` — pořadí, šířky, záhlaví, viditelnost, per-sloupec styl.
- **Atributy zastávek** vedle názvu (na znamení, jízdenkový automat, přestup) — z číselníku, jen relevantní.
- **Časy jízdy** ve sloupci (kumulativní z chronometráže / z mapy).
- **Šipka směru jízdy** (┌─▼), zvýraznění aktuální zastávky.

### 2d) DepartureGrid — minutová mřížka (5 min)
- **Skupiny dnů** = sloupce (pracovní den, sobota, neděle…) z `TypyProvozu`.
- **Per-sloupec nastavení** přes `DepartureGroupEditorDialog` — viditelnost, barvy, font, zarovnání, padding.
- **Sloupec hodin** — vlevo nebo u každé skupiny (`HourColumnPerGroup`).
- **Redistribuce šířek** na celou šířku, word-wrap záhlaví, sloučený řádek záhlaví.
- **Anotace u minut** (horní index) — ukázat na reálných spojích:
  - týdenní platnost (jede jen Po/So/Ne…),
  - speciální znaky spoje,
  - **negativní značky** (×, ⊘) — spoj nestaví všude,
  - bezbariérovost (H / F),
  - **rozdíly tras** (písmena A/B) — spoj jede odlišnou trasou.

### 2e) Business logika, kterou engine řeší automaticky (5 min)
Zdůraznit, že tohle uživatel **nemusí ručně psát** — vypočítá se z dat:
- **Omezení platnosti** "jede/nejede" — textová omezení dle data (1:1 s legacy).
- **Rozdíly tras** — algoritmus přiřadí písmena spojům s odlišnou trasou + text do legendy
  ("neobsluhuje zast. X", "končí v zast. Y").
- **Atributy spoje** — bezbariér (H/F) automaticky z konfigurace.
- **Dopravce** — popis + adresa provozovny z `^JRSET`.
- **Legenda poznámek** — tři zdroje (poznámky zastávky → stránkové → atributy), deduplikace,
  víc sloupců, řazení, zarovnaný marker.

### 2f) Legenda a poznámky (2 min)
- `NotesLegendElement` — víc sloupců, řazení dle délky, zarovnaný marker.
- Ukázat, jak se legenda sama naplní z toho, co je na stránce.

**Shrnutí Části 2 (1 min):** elementy rozumí datům JŘ — uživatel řeší *vzhled*, engine řeší *business logiku*.

---

## 0:55–1:00 — Roadmapa a Q&A

**Co je hotové (zmínit krátce):**
- Zastávkový JŘ z AD dat ověřený 1:1 proti legacy.
- Šablony i preference uložené v Caché, per-firma.
- Dávkový tisk linek.

**Co se chystá / kde jsme opatrní (upřímně):**
- Multi-varianta časů jízdy (víc časových sloupců) — vyžaduje zásah do rendereru.
- Číslování omezení dle firemní konfigurace.
- Doladění vizuálu legendy.
- (Pokud dojde řeč:) **AI-asistovaný import PDF** — stretch funkce, ne produkční slib.

**Otázky, na které být připravený:**
- *"Můžeme si šablonu upravit sami?"* → Ano, to je celý smysl — vizuální editor + uložení do Caché.
- *"Co se starými šablonami?"* → V1 neimportuje 1:1; nové se navrhnou znovu (rychle, díky WYSIWYG).
- *"Bude to vypadat stejně jako dosud?"* → U AD JŘ ověřeno 1:1 proti starému výstupu na konkrétních linkách.
- *"Jak se to dostane k nám?"* → Integrované v EDISONu; tisk přes stejný engine jako náhled.

---

## Příprava před prezentací (checklist)

- [ ] Připojení na Caché dev server / VPN (kvůli reálným datům, šablonám a tisku).
- [ ] Připravená rozpracovaná šablona zastávkového JŘ + prázdná na ukázku tvorby.
- [ ] Vybraná **demo linka** s: rozdíly tras (A/B), bezbariérovými spoji (H/F), omezeními platnosti, negativními značkami.
- [ ] Otevřený starý nástroj (nebo screenshot) pro srovnání "jak to bylo dřív".
- [ ] Vyzkoušený PDF/PNG export a PrintDialog s reálnými daty.
- [ ] Zvětšené UI písmo (EDTheme font scale) pro čitelnost na projektoru.
- [ ] Záloha: PDF výstupy předem vyexportované, kdyby vypadlo připojení.

---

## 🧪 Integrační smoke-test před prezentací (DOČASNÉ — po proklikání smazat)

> Cíl: projet **každou věc z osnovy** na reálných datech a podchytit bugy dřív, než je uvidí zákazník.
> Postupuj shora dolů; u každého kroku odškrtni ✅ funguje / ✍️ zapiš co je rozbité.
> Pořadí kopíruje strukturu prezentace, ať si zároveň natrénuješ flow.

### A) Start a připojení
- [ ] Spustit TemplateDesigner (standalone i z EDISONu, pokud budeš demovat oboje).
- [ ] Caché připojení živé — agenda chip ve status baru ukazuje správnou agendu.
- [ ] **Otevřít šablonu z Caché** — správce šablon se načte, filtr dle agendy funguje, search najde.
- [ ] Otevřít konkrétní AD JŘ šablonu → načte se bez chyby, preview se vykreslí.

### B) Základní editace (Část 1b)
- [ ] Přidat z palety: text, obrázek, čára, obdélník, čárový kód, tabulka — každý se vloží.
- [ ] Drag & drop elementu z palety na canvas — ghost preview + zvýraznění sekce funguje.
- [ ] Drag pole agendy z panelu → vznikne `{POLE}` placeholder.
- [ ] Smart snap — přichycení hran/středů, indikátor mezery.
- [ ] Ctrl+D + šipka (směrový duplikát) i Ctrl+D (plain) — kopie na správném místě.
- [ ] Ctrl+G / Ctrl+Shift+G — seskupit/rozeskupit; klik vybere skupinu, Alt+klik jeden prvek.
- [ ] Undo/Redo (Ctrl+Z/Y) opakovaně přes víc akcí — stav sedí, nic se „neztratí".

### C) Vlastnosti a styly (Část 1c)
- [ ] Inline panel Vlastnosti reaguje na výběr (i při přepnutí mezi elementy).
- [ ] Změna fontu / velikosti / tučné / kurzíva / podtržení / přeškrtnutí — projeví se v renderu.
- [ ] Barva textu + pozadí, zarovnání H+V.
- [ ] Rámeček + pozadí; sdílené hrany dvou elementů = jeden rámeček (ne dvojitý).
- [ ] Multi-select + Enter → společné vlastnosti se aplikují na všechny.
- [ ] *(volitelné)* Ctrl+Shift+C / Ctrl+Shift+V — kopírování stylu.

### D) Výrazy a podmíněný obsah (Část 1d)
- [ ] `{POLE}` se v preview resolvuje na reálnou hodnotu.
- [ ] Formátování `{POLE:N2}`, `{POLE:UPPER}`, `{POLE:LEFT(4)}` — výstup odpovídá.
- [ ] `VisibilityExpression` (`{POCET} > 0`, `{PAGE} > 1`) — element/sekce se schová/zobrazí dle dat.
- [ ] Validace výrazu v UI — chybný výraz hlásí červeně.
- [ ] Agregace v souhrnném řádku tabulky (SUM/COUNT/…) — spočítá správně.

### E) Tabulky (Část 1e)
- [ ] Fixed tabulka — buňky s `{PLACEHOLDER}` se naplní ze skalárů.
- [ ] Dynamic tabulka — řádky se vygenerují z kolekce, sloupce sedí.
- [ ] Editor sloupců + per-sloupec styl — změny se propíšou do renderu.

### F) Layout a struktura (Část 1f)
- [ ] Sekce hlavička/tělo/patička; chování tisku (každá stránka / první / poslední / kromě první) — ověřit v multi-page preview.
- [ ] Drag & drop řazení sekcí + redistribuce výšek při přidání/smazání.
- [ ] Uložit sekci jako vlastní šablonu → znovu vložit do jiné šablony.
- [ ] Přepnout `CultureCode` (cs-CZ → sk-SK / de-DE) — formát čísel/datumů se změní.
- [ ] Škálování šablony 50–200 % — vše proporcionálně; pak zpět na 100 %.
- [ ] Ctrl+Shift+N (normalizace pozic) — preview počtu změn + aplikace.

### G) Náhled, export, multi-page (Část 1g)
- [ ] Live preview odpovídá canvasu; více stránek pod sebou.
- [ ] Multi-page stránkování (hlavičky/patičky se opakují, tabulka se dělí přes stránky).
- [ ] **PDF export** — otevřít výsledek, zkontrolovat 1. + poslední stránku.
- [ ] **PNG export** — vznikne, čitelný.
- [ ] **PrintDialog** s reálnými daty z Caché — parametry, členění výstupu (Vše / per linka / per zastávka×směr), název souboru z `{POLE}` tokenů.
- [ ] Varování o přetečení — schválně přeplnit sekci → červený rámeček + status hláška.
- [ ] *(volitelné)* Import z PDF — vytáhne texty/čáry/obrázky; (s AI klíčem i bez něj — graceful fallback).

### H) Šablony a stav v Caché (Část 1h)
- [ ] Uložit do Caché (přepis i „jako…") → znovu otevřít → obsah sedí.
- [ ] Přejmenovat / smazat šablonu ve správci.
- [ ] Zavřít a znovu otevřít app → per-user stav (zoom, mřížka, recent files) se obnoví.

### I) Zastávkové elementy a JŘ logika (Část 2) — *jádro, testovat důkladně*
- [ ] **Přepínač agendy** (Ctrl+Shift+A / chip) — přepnout MHD ↔ AD JŘ.
- [ ] **Instance picker** — vyhledat linku, multi-výběr, Vybrat vše / Odznačit vše.
- [ ] Po výběru instance se **GetData** načtou (status feedback), preview se přerenderuje.
- [ ] **StopList** — názvy zastávek, časy jízdy (sloupec), atributy u názvu, šipka směru; editor sloupců (pořadí/šířky/viditelnost/styl).
- [ ] **DepartureGrid** — minuty po skupinách dnů; sloupec hodin (vlevo i `HourColumnPerGroup`); redistribuce šířek; word-wrap záhlaví; per-skupina styl přes dialog.
- [ ] **Anotace u minut** na reálné lince: týdenní platnost, speciální znaky, negativní značky (×/⊘), bezbariér (H/F), rozdíly tras (A/B).
- [ ] **Omezení „jede/nejede"** — text dle data odpovídá očekávání (porovnat s legacy výstupem).
- [ ] **Rozdíly tras** — písmena přiřazena + text v legendě („neobsluhuje zast. X" / „končí v zast. Y").
- [ ] **Dopravce** — popis + adresa provozovny.
- [ ] **NotesLegend** — legenda se naplní z toho, co je na stránce; víc sloupců, řazení, zarovnaný marker.
- [ ] **Multi-page per zastávka × směr** — projet linku s víc zastávkami, zkontrolovat víc stránek.
- [ ] **Dávkový tisk víc linek** — vybrat několik linek, export → počet souborů/stránek sedí, názvy bez kolizí (`_2`/`_3`).

### J) 1:1 kontrola proti legacy (must-have před prezentací)
- [ ] Vyexportovat AD JŘ pro **ověřené gold linky** (0410101 / 0425017 / 0320144 / 0320993 / 0365001) a vizuálně porovnat se starým výstupem.
- [ ] Zkontrolovat alespoň jednu linku s **bezbariérem + rozdíly tras + omezeními** najednou.

### Nálezy (sem zapisuj bugy k opravě před prezentací)