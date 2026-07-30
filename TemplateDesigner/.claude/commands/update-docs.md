---
description: Aktualizuj CLAUDE.md, changelog a roadmap podle posledního commitu
allowed-tools: Read, Edit, Bash(git log *), Bash(git diff *), Bash(git show *)
---

Přečti poslední commit a aktualizuj dokumentaci projektu.

## Kontext

Poslední commit: !`git log -1 --format="%H %s%n%b"`
Co se změnilo (soubory): !`git diff HEAD~1 HEAD --stat`
Diff: !`git diff HEAD~1 HEAD`

## Co aktualizovat

### 1. `CLAUDE.md` - **jen při změně invariantu**

**Stav se do `CLAUDE.md` nepíše.** Žádné datum, žádný výčet hotových featur, žádný focus -
to všechno drží roadmapa a changelog a v `CLAUDE.md` by to stálo kontext v každé session.

Sáhni tam **výhradně** tehdy, když commit změnil něco z těchhle kategorií:

- **neodvoditelný invariant** (persistovaný klíč, zpětná kompatibilita šablon, kontrakt se shellem)
- **pravidlo pro psaní kódu** nebo architektonická hranice vrstev
- tvrzení v `CLAUDE.md`, které commit učinil **nepravdivým** - to smaž nebo oprav

Jinak `CLAUDE.md` nech být. Když si nejsi jistý, jestli je to invariant nebo featura: featura.

### 2. `.claude/docs/changelog.md` - přidej nový záznam na začátek

Formát záznamu:
```
**YYYY-MM-DD - <název commitu>:**
- ✅ Popis změny 1
- ✅ Popis změny 2
- ✅ Bug fix: popis pokud relevantní
```

Pravidla:
- Záznam přidej **na začátek souboru** (nejnovější nahoře)
- Zachovej stávající záznamy beze změny
- Popis změn piš česky, stručně ale výstižně
- Pokud commit opravuje problém evidovaný v roadmapě nebo v tiketu, uveď to explicitně

### 3. `.claude/docs/roadmap.md` - pokud commit implementoval roadmap položku

- Najdi odpovídající položku v roadmapě
- **Hotovou položku přesuň KOMPLETNĚ do `roadmap-archive.md`** - nikdy v roadmapě
  nenechávej odškrtnutý „✅ hotovo" řádek. V aktivní roadmapě zůstává jen to, co se má
  dělat (+ krátký ukazatel do archivu, kde jsou detaily). Platí i pro rozhodovací tabulky.
- Částečně hotová položka (Part 1 z 2) v roadmapě **zůstává** - zúží se na to, co zbývá
- Neměň položky které commit neovlivnil

### 4. ./README.md
- Pokud jde o uživatelskou změnu, přidej do obecného popisu pro koncového uživatele

## Důležité

- Nepiš žádný výstup uživateli - jen proveď změny v souborech
- Pokud diff není jednoznačný, raději buď konzervativní (méně změn než příliš)
- Zachovej stávající strukturu a formátování všech souborů
