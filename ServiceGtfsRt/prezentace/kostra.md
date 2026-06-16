# Kostra prezentace — GTFS / GTFS Realtime

> **Účel:** IT seminář pro zástupce **objednatelů** (organizátorů dopravy) i **dopravců**.
> **Cíl:** Vysvětlit, co je GTFS / GTFS-RT, co je povinné a co volitelné, jak se data
> dostanou na Google Maps, a jak to lze realizovat obecně i v rámci Edisonu.
> **Tón:** obecný úvod pro všechny (méně technický) → na konci pár techničtějších slidů pro IT.
> **Rozsah:** 9 slidů na cca 15 minut. **Datum:** _(editovat v .pptx)_
>
> Legenda: **🔲 [DOPLNÍ JAN]** = placeholder na konkrétní podklad, který dodáš.
> Vzor vzhledu = `_vzor.pptx` (intro slide → obsahové slidy → outro), stejný postup jako
> u `TemplateDesigner/prezentace`. Generuje `build_presentation.py` → `prezentace.pptx`.

---

## Slide 1 — Titulní (intro)

- Nadpis: **GTFS / GTFS Realtime**
- Podnadpis: **Jízdní řády a živá data na Google Maps**
- Logo M-line, datum

---

## ČÁST A — Obecný úvod (pro všechny)

### Slide 2 — Proč GTFS a co to je

- Cestující chtějí vidět **jízdní řády i živá data** v aplikacích, které běžně používají (Google Maps, Mapy.cz, Apple Maps)
- **GTFS = General Transit Feed Specification** — otevřený, celosvětově používaný standard
- Strojově čitelný popis dopravy: **kdo jezdí, kudy, kdy a odkud kam**
- Princip **jeden zdroj dat → mnoho aplikací** (publikuji jednou, využijí všichni)
- Dvě roviny: **GTFS** (statické JŘ) + **GTFS Realtime** (živá data)

### Slide 3 — Z čeho se GTFS skládá

- Feed = **ZIP balíček textových souborů** (CSV / `.txt`), každý soubor = jedna tabulka
- **Povinné minimum** (bez nich Google feed nepřijme):
  - `agency` (dopravce), `stops` (zastávky + GPS), `routes` (linky)
  - `trips` (spoje), `stop_times` (časy), `calendar` (kdy jedou)
- **Volitelné** (přidaná hodnota):
  - `shapes` (přesný tvar trasy v mapě), `transfers` (přestupy), `frequencies` (intervaly)
  - `fare_*` (tarify), `feed_info` (informace o feedu)
- Pravidlo: **začít povinným minimem, volitelné dodávat postupně**

### Slide 4 — GTFS Realtime

- Nadstavba nad statickým GTFS — **co se děje právě teď**
- **Tři druhy zpráv (entit):**
  - **VehiclePositions** — poloha vozidel (živá tečka na mapě)
  - **TripUpdates** — zpoždění a předpovědi příjezdů/odjezdů
  - **ServiceAlerts** — mimořádnosti: výluky, zrušené spoje, objížďky
- Formát **Protocol Buffers (`.pb`)**; vždy se odkazuje na statický feed (shodné identifikátory)
- Doporučené pořadí zavádění: **vozidla → zpoždění → výluky**

### Slide 5 — Jak data dostat na Google a způsoby realizace

> Sloučený slide (původně „Jak se data dostanou na Google" + „Způsoby realizace").
> Obecné možnosti; co používáme konkrétně, je na následujícím slidu (Edison).

- Google si data buď **stahuje sám (fetch z URL)**, nebo je **přijímá (push)** — vždy přes veřejnou HTTPS adresu
- **Statické GTFS (ZIP):**
  - **fetch z URL** — Google stahuje dle rozvrhu (min. týdně, lze denně); doporučeno
  - **ruční nahrání ZIP** v konzoli — jednoduché, ale data snadno zastarají
- **GTFS Realtime (`.pb`):**
  - **fetch endpoint** — Google stahuje ~30 s; nebo **push** na Google přes OAuth 2.0
- **Možnosti realizace:** ručně / vlastní generátor nad systémem JŘ / nákup hotové služby
- **Vždy potřeba:** kvalitní data, stabilní identifikátory (statika ↔ RT), veřejný HTTPS, aktuálnost

---

## ČÁST B — Realizace v rámci Edisonu

> Rámcově vyplněno z dnešního stavu (`STAV-NASAZENI-DPCB.md`, `ServiceGtfsRt/CLAUDE.md`).
> Konkrétní „co umíme / co máme hotové" doplní Jan (edituje přímo v .pptx).

### Slide 6 — GTFS v Edisonu — jak to funguje

- Edison = **primární systém jízdních řádů** → zdroj dat pro statické GTFS i Realtime (drží shodné identifikátory)
- **Co Edison umí:**
  - statické GTFS — naplánovaná úloha vygeneruje **ZIP**
  - Realtime — služba **`ServiceGtfsRt`** převádí živá data na protobuf (`.pb`)
- **Dnešní stav (DPCB):**
  - endpointy připravené; **statiku zákazník nahrává ručně** na Google portál
- **Automatizace (cíl):**
  - ZIP i `.pb` vystavené na **veřejné HTTPS adrese** → Google si oboje **fetchuje sám** = konec ručního nahrávání

> Stav ověřen v ticketu 11701 (DPCB): statika běží ručně pod účtem DPCB; Edison umí
> automatický export ZIP (úloha) — chybí jen veřejná adresa od IT DPCB pro fetch.

### Slide 7 — Architektura a co je potřeba ke zprovoznění

- **Dva nezávislé směry:**
  - **VSTUP (živá data):** zdroj polohy vozidel → Edison/Caché → poskládá feed
  - **VÝSTUP (Google):** Google přes HTTPS **fetchuje** feed z veřejné adresy → `ServiceGtfsRt` (RT) / ZIP (statika)
- **Co zajistí IT zákazníka (ke zprovoznění):**
  - veřejná HTTPS adresa (reverse proxy) + certifikát + otevřený port 443
  - přístup k živému zdroji polohy vozidel z Edison serveru
  - v Google účtu nastavit **fetch URL** (statika i Realtime)
- **Dělba rolí:** data = objednatel/dopravce · feed = Edison/M-line · publikace + Google účet = IT zákazníka

### Slide 8 — Chci zavést GTFS — co dál?

> High-level postup zavedení (bez opakování, co je GTFS). Co zajistí zákazník × co M-line.

- **Vy / vaše IT zajistíte:**
  - data v Edisonu — kvalitní JŘ, zastávky s GPS, čísla linek
  - veřejnou HTTPS adresu + certifikát (reverse proxy, port 443)
  - Google Transit účet + (u Realtime) přístup k živému zdroji polohy vozidel
- **My (M-line) zařídíme:**
  - export statického GTFS (ZIP) + automatickou úlohu
  - službu `ServiceGtfsRt` pro Realtime (`.pb`)
  - nastavení feedů + ověření ve validaci (Issues)
- **Spuštění:** v Google účtu zadat **fetch URL** → Google stahuje a zobrazuje data

---

## Slide 9 — Závěr / kontakt (outro)

- Poděkování, prostor na dotazy
- Kontakt M-line, logo, datum
