---
name: analyza
description: Analýza požadavku proti skutečnému stavu kódu - z tiketu z helpdesku, z business otázky, nebo z položky roadmapy. Nic nemění, produkuje analýzu a běhový žurnál. Použij, když je potřeba pochopit, co se po nás chce a co kód dnes umí, před tím, než se rozhodne o implementaci.
argument-hint: <číslo tiketu | ID položky roadmapy | otázka>
allowed-tools: Read, Glob, Grep, ToolSearch, Bash(git log *), Bash(git show *), Bash(git diff *), Bash(git blame *), Bash(ls *), mcp__edison-zurnal__zurnal_zapis, mcp__edison-zurnal__zurnal_rozhodnuti, mcp__edison-zurnal__zurnal_odpoved
---

# Analýza: požadavek vs. stav kódu

Tvým výstupem je **analýza**, ne změna v kódu a ne plán implementace do posledního kroku.

## Tvrdé omezení

**Nesaháš na kód.** Žádný `Write`, `Edit`, žádný deploy `.cls`, žádný commit. Když
narazíš na něco, co se dá opravit jedním řádkem, **neopravuj to** - zapiš to jako
zjištění a jdi dál. Tenhle běh je bezpečný právě tím, že nic nemění.

## Vstup

Podle toho, co dostaneš v argumentu:

| Tvar | Co udělat |
|---|---|
| Číslo tiketu (`13102`, `#13102`) | Načti ho z helpdesku - nástroje si dotáhni přes `ToolSearch("select:mcp__claude_ai_Podpora_M-line__get_ticket,mcp__claude_ai_Podpora_M-line__list_ticket_updates")`. Přečti i updaty, často je v nich to podstatné |
| ID položky roadmapy (`B7`, `C3`) | Najdi ji v `TemplateDesigner/.claude/docs/roadmap.md` |
| Otázka nebo oblast | Ber jak je, jen si ujasni rozsah |

Když je vstup prázdný nebo nejednoznačný, **zeptej se** - nehádej, co se má analyzovat.

## Postup

1. **Co se po nás chce.** Přeformuluj požadavek vlastními slovy, jazykem uživatele.
   U tiketu odděl, co zákazník napsal, od toho, co si myslíš, že tím myslel - a to
   druhé označ jako předpoklad.
2. **Co kód dnes umí.** Projdi relevantní kód. **Každé tvrzení o kódu podlož
   `file:line`.** Bez odkazu je to dohad, ne zjištění.
3. **Mezera.** Co chybí, co existuje jinak, co existuje a jen se to nenapojilo.
   Nejcennější nález je „tohle už tam je" - hledej ho aktivně.
4. **Varianty řešení.** Dvě až tři schůdné cesty s doporučením a důvodem. Ne katalog
   všeho možného.
5. **Dopad.** Které soubory a vrstvy, obě strany (`dotnet/` i `CACHE/`), breaking
   change wire kontraktu nebo JSON šablony ano/ne, dotčené testy.
6. **Nejistota.** Co jsi neověřil a čím by se to ověřilo. Vyslovit, ne zamlčet.

## Žurnál

Zapisuj průběžně, jak pracuješ, ne až na konci.

**Filtr, co do žurnálu patří:** *změní to, co uživatel uvidí, na co klikne, nebo co
dostane vytisknuté?* Když ne, do uživatelského kanálu to nepatří.

| Nástroj | Kdy |
|---|---|
| `zurnal_zapis` typ `zamer` | 1× na začátku - co analyzuju a proč |
| `zurnal_zapis` typ `zjisteni` | nález v kódu, který mění pohled na požadavek. Vždy s `file:line` |
| `zurnal_zapis` typ `predpoklad` | vzal jsi něco za dané bez důkazu (typicky výklad tiketu) |
| `zurnal_zapis` typ `dusledek` | co by se změnilo v používání aplikace |
| `zurnal_rozhodnuti` | volba, kterou uživatel pocítí. **Když si vybrat neumíš, nech `vybral` prázdné** - beh počká |
| `zurnal_zapis` typ `hotovo` | 1× na konci |

Do žurnálu **nepatří**: čtení souborů, grepy, tvůj vnitřní postup, volba mezi
interními implementačními variantami, které uživatel nepozná.

Když `zurnal_rozhodnuti` vrátí `cekej id=<id>`, volej `zurnal_odpoved(<id>)`, dokud
nedostaneš odpověď. Nespamuj - stačí občas. Na `poznamka:` zapracuj obsah a pokračuj,
na `zrus` zastav a nahlas, kam jsi došel.

## Výstup

```
## Co se po nás chce
[2-4 věty, jazykem uživatele]

## Co kód dnes umí
[s odkazy file:line]

## Mezera
[co chybí / co už tam je a jen se nenapojilo]

## Varianty
1. **[název]** - [co to znamená] · doporučuji, protože [důvod]
2. **[název]** - [co to znamená] · [proč ne]

## Dopad
- Soubory / vrstvy:
- Breaking change (wire, JSON šablony):
- Testy, které se dotknou:

## Nejistota
[co jsem neověřil a čím by se to ověřilo]
```

Analýzu vypiš do konverzace. Žurnál si píše MCP server sám, ty ho neukládáš.

**Na konci nabídni** uložení analýzy do `docs/analyzy/<id>.md` - a ulož ji jen tehdy,
když si o to uživatel řekne. Bez jeho slova nezakládej soubor.

## Co nedělat

- Nerozhodovat architekturu. Varianty předlož, výběr nechej na uživateli.
- Nepsat implementační plán krok za krokem - to je práce `implementuj`, ne tvoje.
- Nepřepisovat obsah tiketu. Zajímavá je **mezera vůči kódu**, ne převyprávění.
- Netvrdit „ověřeno", když jsi jen přečetl kód. Přečtený kód není spuštěný kód.
