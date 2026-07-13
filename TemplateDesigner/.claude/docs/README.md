# Index dokumentace (.claude/docs)

> **Hygiena složky** (revize 2026-07-13, konsolidace 26 → 14 souborů): každý dokument patří do
> jedné kategorie níže. Jednorázová analýza se po vytěžení do roadmapy/kódu **maže** (git historie
> stačí) — nechává se jen datová mapa/rozhodnutí, na které se dál odkazuje. Nový dokument = přidat
> sem řádek a rozmyslet, zda nepatří do existujícího celku; mazání = smazat řádek.

## Živé pracovní (průběžně aktualizované)

| Dokument | Role |
|---|---|
| [`roadmap.md`](roadmap.md) | Jen otevřená práce; hotové → archiv |
| [`changelog.md`](changelog.md) | Průběžný changelog (technický) |
| [`customer-requirements-zastavkovy-jr.md`](customer-requirements-zastavkovy-jr.md) | Stav helpdesk tiketů + TR sada; §0 = dispatch |
| [`pre-version-checklist.md`](pre-version-checklist.md) | ⏳ TEMP — po vydání verze smazat (výsledek → changelog) |

## Závazné reference a rozhodnutí (evergreen)

| Dokument | Role |
|---|---|
| [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md) | R1–R7 pro škálování agend — číst před novou fází. Obsahuje i detail R2 (partial-fit kontrakt element↔engine, implementace = C3) a R6 (governance/distribuce šablon — v1 hotová, koncept kanálu v2) |
| [`feature-design-principles.md`](feature-design-principles.md) | Rozhodovací mřížka před každým requestem |
| [`elementy-architektura.md`](elementy-architektura.md) | Architektura elementů: JŘ elementy (wire formát, hierarchie) + sdílené tabulkové jádro (co ne/sjednocovat) + katalog symbolů (font Timetable) |
| [`template-schema-versions.md`](template-schema-versions.md) | Verzování Template JSON + migrace |
| [`architecture-diagrams.md`](architecture-diagrams.md) | Konceptuální diagramy pro onboarding (autorita = rules + kód) |

## Datové mapy a vytěžené analýzy (referencované z roadmapy — nemazat, needitovat stavy)

| Dokument | Obsah |
|---|---|
| [`ad-data-mapa.md`](ad-data-mapa.md) | AD zastávkový JŘ data: globály `^JRL`… + §10 gold ověření · nastavení linky a rozdíly tras · typy poznámek a layout legendy |
| [`legacy-nahrada.md`](legacy-nahrada.md) | Legacy vs TD: etapy náhrady (+§7 otázky E/F) · gap matice TiskMHD · mapa legacy generátorů · evidence prezentací 2026 |
| [`linkovy-knizni-jr-implementace-analyza.md`](linkovy-knizni-jr-implementace-analyza.md) | Knižní/linkový pilot; §11–12 odložené body (C2–C9) |

## Seedy zákaznické dokumentace (wiki — post-release úkol)

| Dokument | Cíl |
|---|---|
| [`uzivatelska-prirucka-obecna.md`](uzivatelska-prirucka-obecna.md) | Wiki 1a: obecné ovládání (draft 2026-07-08 — před publikací zaktualizovat: imposice, agenda×varianta, AI) |
| [`uzivatelska-prirucka-zastavkovy-jr.md`](uzivatelska-prirucka-zastavkovy-jr.md) | Wiki 1b: agenda zastávkový JŘ (draft 2026-07-08) |
| [`plan-a-zmeny.md`](plan-a-zmeny.md) | Wiki 2: „Novinky a plány" — zákaznický changelog+roadmapa (aktualizováno 2026-07-13) |

## Archivy (append-only)

[`roadmap-archive.md`](roadmap-archive.md) · [`changelog-archive-2026-05.md`](changelog-archive-2026-05.md)

---

**Historie úklidu 2026-07-13** (vše v git historii):
- Smazáno po vytěžení: `audit-pred-produkci-2026-07-09`, `produkce-pripravenost-2026-07-07`,
  `r1-sdilene-tabulkove-jadro-navrh`, `test-plan-specializovane-elementy`, `uzivatelska-prirucka`
  (nahrazena splitem obecná + zastávkový JŘ).
- Sloučeno: `r2-partial-fit-kontrakt` + `governance-sablon-koncept` → architektonicka-rozhodnuti ·
  `schedule-elements-architecture` + `tabulkove-elementy-jadro` + `symboly-jr-katalog` →
  elementy-architektura · `cache-database-linky` + `ad-jr-nastaveni-linky-analyza` +
  `ad-jr-typy-poznamek-layout-analyza` → ad-data-mapa · `analyza-stav-a-legacy-nahrada-2026-07` +
  `gap-analyza-legacy-vs-novy-jr` + `legacy-schedule-generators` + `prezentace-a-gap-2026` →
  legacy-nahrada.
