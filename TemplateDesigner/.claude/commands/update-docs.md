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

### 1. `CLAUDE.md` — sekce "Aktuální stav projektu"

- **Datum poslední aktualizace** → nastav na dnešní datum
- **"✅ Co máme"** → přidej nové dokončené věci z commitu (stručně, bullet)
- **"🎯 Aktuální focus"** → uprav na základě commitu; pokud commit dokončil focus položku,
  přesuň ji do "Co máme" a napiš nový focus (nebo ponech prázdné pokud není jasný další krok)
- **"🐛 Známé problémy"** → odeber problémy které commit řeší; přidej nové pokud commit
  něco zavádí (nebo ponech "(žádné aktivní)" pokud je vše čisté)

### 2. `.claude/docs/changelog.md` — přidej nový záznam na začátek

Formát záznamu:
```
**YYYY-MM-DD — <název commitu>:**
- ✅ Popis změny 1
- ✅ Popis změny 2
- ✅ Bug fix: popis pokud relevantní
```

Pravidla:
- Záznam přidej **na začátek souboru** (nejnovější nahoře)
- Zachovej stávající záznamy beze změny
- Popis změn piš česky, stručně ale výstižně
- Pokud commit opravuje bug z "Známé problémy", uveď to explicitně

### 3. `.claude/docs/roadmap.md` — pokud commit implementoval roadmap položku

- Najdi odpovídající položku v roadmapě
- Přidej k ní `✅ hotovo (YYYY-MM-DD)` nebo ji přesuň do sekce "Implementováno"
- Pokud commit částečně implementuje položku (Part 1 z 2), to označ jako `✅ Part 1 hotovo`
- Neměň položky které commit neovlivnil

### 4. ./README.md
- Pokud jde o uživatelskou změnu, přidej do obecného popisu pro koncového uživatele

## Důležité

- Nepiš žádný výstup uživateli — jen proveď změny v souborech
- Pokud diff není jednoznačný, raději buď konzervativní (méně změn než příliš)
- Zachovej stávající strukturu a formátování všech souborů
