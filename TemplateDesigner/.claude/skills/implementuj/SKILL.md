---
name: implementuj
description: Provede jednu položku roadmapy nebo tiket od zadání po commit - průzkum, testy, implementace, ověření branami, docs, report. Použij na konkrétní ohraničenou úlohu s ID bloku (B7) nebo číslem tiketu. Na pochopení požadavku před rozhodnutím je `analyza`, ne tohle.
argument-hint: <ID bloku roadmapy | číslo tiketu>
allowed-tools: Read, Edit, Write, Glob, Grep, ToolSearch, Bash(dotnet build *), Bash(dotnet test *), Bash(pwsh *), Bash(git status), Bash(git diff *), Bash(git log *), Bash(git add *), Bash(git commit *), Bash(grep *), Bash(ls *), mcp__edison-zurnal__zurnal_zapis, mcp__edison-zurnal__zurnal_rozhodnuti, mcp__edison-zurnal__zurnal_odpoved
---

# Implementuj jednu položku

Uživatel dodává **co a proč** (priorita, architektonická rozhodnutí, high-level kontext).
Ty vezeš **mechaniku** - průzkum, testy, kód, ověření, docs, commit - bez čekání na dílčí pokyny.

**Bez žurnálu nezačínej.** Úplně první věc, kterou uděláš, je `zurnal_zapis` typ `zamer`.
Když ten nástroj není dostupný (typicky neběží MCP server `edison-zurnal`), **zastav a řekni
to** - neber položku, needituj, a hlavně **nepřecházej na „vedu poznámky interně"**. Report
se sestavuje ze žurnálu (§Žurnál), takže bez něj nemáš čím splnit kontrakt skillu.
(Doloženo dvěma běhy 2026-08-03: oba degradovaly na interní poznámky, dojely do konce
a doručily výstup s nulovými událostmi `brana`.)

## 0. Vzít položku

| Vstup | Co udělat |
|---|---|
| ID bloku (`B7`, `L-2`) | najdi ho v [`roadmap.md`](../../docs/roadmap.md), přečti `hotovo když:` a velikost |
| Číslo tiketu | dotáhni z helpdesku (`ToolSearch("select:mcp__claude_ai_Podpora_M-line__get_ticket")`), včetně updatů |
| Nic | vezmi **nejnižší nezablokovaný blok z nejnižší nedokončené vlny**; zkontroluj zónu (paralelizace); **oznam, co bereš** |

**Zastav se a nech rozhodnutí na uživateli, když:**

- blok má velikost **L**, nebo je architektonický (dotýká se víc projektů, mění wire kontrakt)
- blok **nemá `hotovo když:`** a ty ho neumíš odvodit - doplň ho a nech potvrdit, než začneš
- narazíš **v průběhu** na volbu, kterou nejde odvodit z pravidel a ze zadání

To poslední je nejdůležitější. Předem se nepředpovídá každé rozhodnutí; když na jedno narazíš,
**nevybírej si sám** - zapiš ho přes `zurnal_rozhodnuti` s prázdným `vybral` a počkej.

## 1-2. Průzkum a čím to ověříš

Přečti relevantní kód **před** psaním. Pak řekni, čím se featura ověří, ideálně dřív než ji napíšeš:
xUnit · Caché `%UnitTest` · gold digest · ruční klikací cesta. Když `hotovo když:` z bloku
neodpovídá tomu, co jsi v kódu našel, řekni to - je to signál, že blok byl napsaný na jiný předpoklad.

## 3-4. Implementovat a iterovat

Podle `.claude/rules/` (R1-R5, kód EN / komentáře CZ, CVar, MVVM bez WPF). Auto-build hook hlásí
chyby po každé editaci, `drift-report` hook hlásí porušení R2/R4. Kroky 3-4 opakuj, než to vypadá dobře.

**Když tě `guard.sh` zablokuje** (gold baseline, legacy, testovací brána), neobcházej to.
Je to signál, že jsi na hranici, kde rozhoduje uživatel: vysvětli, co a proč chceš změnit, a zeptej se.

## 5. Brány - bez nich není hotovo

Spusť a **přečti výstup**. Netvrď „mělo by projít".

1. `dotnet build EDISON2.sln`
2. `hotovo když:` z bloku - to je ta hlavní brána
3. relevantní drift testy (`drift-report` hook ti u dotčených souborů řekl které)
4. u `.cls` změn: `pwsh -File tools/run-cache-tests.ps1 -Package <package>` (potřebuje VPN)

Když brána spadne dvakrát na tomtéž, **přestaň to zkoušet do třetice** - zapiš důvod, zachovej
poslední čistý stav a nahlas to. Zaseknutá položka je lepší výsledek než tichý workaround.

## 6. Docs

- **changelog** vždy - záznam na začátek, formát podle existujících
- **roadmapa** - hotový blok **KOMPLETNĚ do [`roadmap-archive.md`](../../docs/roadmap-archive.md)**,
  ne odškrtnutý „✅ hotovo" řádek. Částečně hotový blok zůstane, zúžený na to, co zbývá
- **`CLAUDE.md`** jen když se změnil **invariant nebo pravidlo**. Featura tam nepatří
- **wiki** jen když featura mění user-facing chování **a** je pokrytá `seed`em z `.claude/wiki-map.json`;
  pravidla (TipTap bez tabulek, seed = pravda) viz `CLAUDE.md` §Wiki

## 7. Commit

`TD - typ(scope): proč` - popisuj **proč**, co je vidět v diffu. Granulárně.

**Commit je oddělený od schválení** (rozhodnutí Jan 2026-07-28):

| Velikost | Commit |
|---|---|
| `[S]`, `[M]` se zelenými branami | **commituj sám** - granulární historie se dá vrátit `git revert` |
| `[L]` nebo zásadní rozhodnutí | **necommituj** - ukaž výsledek a počkej |

Commit **neznamená** „schváleno". Co stroj neověřil, patří do reportu jako „potřebuje tvoje oči".
**Nikdy nepushuj.**

## 8. Report

Sekce oddělené podle **kdo to ověřil** - to je celá hodnota reportu:

```
✅ OVĚŘENO STROJOVĚ - nemusíš kontrolovat
   B7  <co> · <která brána a výsledek>  → commit <sha>

👁 POTŘEBUJE TVOJE OČI - stroj to nevidí
   <co> · proč to test nevidí · přesná klikací cesta

🛑 ZASTAVIL JSEM SE
   <co> · co potřebuju rozhodnout · varianty

⚠️ PŘEDPOKLADY (můžeš vetovat)
   <co jsem vzal za dané bez důkazu>
```

Na konci navrhni **další logický krok** podle roadmapy - ať nemusí zadávat znovu.

## Žurnál

Zapisuj průběžně, ne na konci. **Filtr:** změní to, co uživatel uvidí, na co klikne, nebo co
dostane vytisknuté? Když ne, do uživatelského kanálu to nepatří.

| Nástroj | Kdy |
|---|---|
| `zurnal_zapis` `zamer` | 1× na začátku - co beru a proč |
| `zurnal_zapis` `zjisteni` | nález v kódu, který mění plán; vždy `file:line` |
| `zurnal_rozhodnuti` | volba, kterou uživatel pocítí (default, chování, umístění v UI, pojmenování, hraniční případ, zpětná kompatibilita). **Prázdné `vybral` = neumím rozhodnout a čekám** |
| `zurnal_zapis` `dusledek` | co se mění v používání aplikace |
| `zurnal_zapis` `brana` | co se ověřovalo a jak to skončilo |
| `zurnal_zapis` `pozor` | změnu stroj neuvidí - klikací cesta |
| `zurnal_zapis` `hotovo` | 1× na konci, čím je to podložené |

Report se sestavuje **ze žurnálu**. Co jsi nezapsal, v reportu chybí - a je to vidět.

Vnitřní volby, které uživatel nepozná (algoritmus, pořadí kroků, pomocný skript), do žurnálu
nepatří. Na `cekej id=<id>` volej `zurnal_odpoved(<id>)`; po vypršení dostaneš „nikdo neodpověděl"
a máš pokračovat svou volbou a uvést v reportu, že rozhodnutí zůstalo otevřené.

## Co nedělat

- Nerozhodovat za uživatele architekturu ani priority.
- Nepokračovat, když chybí žurnál, a nevést poznámky interně místo něj.
- Neobcházet `guard.sh` a neupravovat brány, aby prošly.
- Netvrdit „ověřeno", když jsi jen přečetl kód nebo spustil build.
- Nenechávat v roadmapě odškrtnuté hotové řádky.
