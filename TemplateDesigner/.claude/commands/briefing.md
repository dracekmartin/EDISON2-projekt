---
description: Briefing na začátek session — stav projektu, top 3 priority, otevřené problémy
allowed-tools: Read, Bash(git log *), Bash(git diff *)
---

Přečti níže uvedené soubory a připrav stručný briefing pro začátek session.

## Kontext

Aktuální stav projektu: !`cat CLAUDE.md`
Roadmapa: !`cat .claude/docs/roadmap.md`
Poslední commity: !`git log --oneline -5`
Neuložené změny: !`git diff --stat`

## Co vypsat

Odpověz v tomto formátu — stručně, žádné omáčky:

---

### 📍 Kde jsme
[1-2 věty: co bylo naposledy dokončeno podle CLAUDE.md a git logu]

### 🔴 Top 3 priority
1. **[název]** — [jedna věta proč je to první]
2. **[název]** — [jedna věta proč]
3. **[název]** — [jedna věta proč]

### ⚠️ Otevřené problémy
[seznam z "Známé problémy" — nebo "žádné" pokud je prázdné]

### 💡 Doporučení pro tuto session
[1 věta: na co se zaměřit dnes s ohledem na priority a aktuální stav]

---

Priority urči na základě roadmapy — sekce 🔴 Kritické má přednost před 🟡 Důležité,
ta před 🟢 Technický dluh. Pokud jsou neuložené změny, zmiň to v doporučení.
