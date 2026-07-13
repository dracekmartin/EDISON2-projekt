# Index dokumentace (.claude/docs)

> **Hygiena složky** (revize 2026-07-13): každý dokument patří do jedné kategorie níže.
> Jednorázová analýza se po vytěžení do roadmapy/kódu **maže** (git historie stačí) — nechává se
> jen datová mapa/rozhodnutí, na které se dál odkazuje. Hotový návrhový proces se maže s pointerem
> v evergreen dokumentu. Nová analýza = přidat sem řádek; mazání = smazat řádek.

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
| [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md) | R1–R7 pro škálování agend — číst před novou fází |
| [`feature-design-principles.md`](feature-design-principles.md) | Rozhodovací mřížka před každým requestem |
| [`tabulkove-elementy-jadro.md`](tabulkove-elementy-jadro.md) | Co je sdílené tabulkové jádro / co nesjednocovat |
| [`schedule-elements-architecture.md`](schedule-elements-architecture.md) | JŘ elementy — wire formát, hierarchie, zodpovědnosti |
| [`template-schema-versions.md`](template-schema-versions.md) | Verzování Template JSON + migrace |
| [`symboly-jr-katalog.md`](symboly-jr-katalog.md) | Mapa symbolů JŘ (font Timetable) |
| [`r2-partial-fit-kontrakt.md`](r2-partial-fit-kontrakt.md) | Rozhodnutý kontrakt element↔engine (implementace = C3) |
| [`governance-sablon-koncept.md`](governance-sablon-koncept.md) | Distribuce systémových šablon (v1 hotová, koncept pro kanál v2) |
| [`architecture-diagrams.md`](architecture-diagrams.md) | Konceptuální diagramy pro onboarding (autorita = rules + kód) |

## Datové mapy a vytěžené analýzy (referencované z roadmapy — nemazat, needitovat stavy)

| Dokument | Na co se odkazuje |
|---|---|
| [`cache-database-linky.md`](cache-database-linky.md) | AD data (`^JRL`…), §10 gold ověření ZastavkoveJrAd |
| [`ad-jr-nastaveni-linky-analyza.md`](ad-jr-nastaveni-linky-analyza.md) | Kde jsou AD nastavení linky (mapa, ne status) |
| [`ad-jr-typy-poznamek-layout-analyza.md`](ad-jr-typy-poznamek-layout-analyza.md) | Typy poznámek + layout legendy (podklad) |
| [`gap-analyza-legacy-vs-novy-jr.md`](gap-analyza-legacy-vs-novy-jr.md) | Legacy TiskMHD vs TD matice — analýza jednou, nedělat znovu |
| [`linkovy-knizni-jr-implementace-analyza.md`](linkovy-knizni-jr-implementace-analyza.md) | Knižní/linkový pilot; §11–12 odložené body (C2–C9) |
| [`analyza-stav-a-legacy-nahrada-2026-07.md`](analyza-stav-a-legacy-nahrada-2026-07.md) | Etapy náhrady legacy; §7 otevřené otázky E/F |
| [`legacy-schedule-generators.md`](legacy-schedule-generators.md) | Mapa legacy generátorů per typ JŘ (Fáze C/D/E) |
| [`prezentace-a-gap-2026.md`](prezentace-a-gap-2026.md) | Evidence prezentací 2026 (sdělení, FAQ, P1–P3) |

## Seedy zákaznické dokumentace (wiki — post-release úkol)

| Dokument | Cíl |
|---|---|
| [`uzivatelska-prirucka-obecna.md`](uzivatelska-prirucka-obecna.md) | Wiki 1a: obecné ovládání (draft 2026-07-08 — před publikací zaktualizovat: imposice, agenda×varianta, AI) |
| [`uzivatelska-prirucka-zastavkovy-jr.md`](uzivatelska-prirucka-zastavkovy-jr.md) | Wiki 1b: agenda zastávkový JŘ (draft 2026-07-08) |
| [`plan-a-zmeny.md`](plan-a-zmeny.md) | Wiki 2: „Novinky a plány" — zákaznický changelog+roadmapa (aktualizováno 2026-07-13) |

## Archivy (append-only)

[`roadmap-archive.md`](roadmap-archive.md) · [`changelog-archive-2026-05.md`](changelog-archive-2026-05.md)

---

**Smazáno po vytěžení** (git historie): `audit-pred-produkci-2026-07-09` (bloky A–D odbaveny),
`produkce-pripravenost-2026-07-07` (→ roadmapa + template-schema-versions),
`r1-sdilene-tabulkove-jadro-navrh` (→ tabulkove-elementy-jadro), `test-plan-specializovane-elementy`
(→ gold testy + checklist), `uzivatelska-prirucka` (nahrazena splitem obecná + zastávkový JŘ).
