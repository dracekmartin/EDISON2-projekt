# Refactoring sekcí — březen 2026

## Motivace

Původní model měl 7 typů sekcí jako samostatné třídy:
- `SectionHeader`, `SectionFooter`, `SectionFixedHeight`, `SectionTable`, `SectionTableDesigned`, `SectionInfiniteText`, `SectionSeparator`

**Problém:** Tyto třídy byly **redundantní** — obsahově identické, lišily se jen výchozími hodnotami properties.

**Řešení:** Sloučit vše do jedné univerzální třídy `Section`, kde **chování určují properties**, ne typ třídy.

---

## Co se změnilo

### Před refactoringem:

```csharp
public abstract class Section {
    SectionPrintBehavior PrintBehavior
    SectionOverflowBehavior OverflowBehavior
    float? DesignerHeight
    List<TemplateElement> Elements
}

// 7 podtříd s pevným chováním
public class SectionHeader : Section {
    public SectionHeader() {
        PrintBehavior = OnEveryPage;
        DesignerHeight = 25f;
    }
}
// ... SectionFooter, SectionFixedHeight, atd.
```

**Problém:**
- Když jsem chtěl "záhlaví jen na první stránce", musel jsem použít `SectionHeader` a změnit `PrintBehavior=OnFirstPageOnly` → matoucí
- Nešel vytvořit nový typ sekce bez změny kódu
- 7 tříd, které se liší jen konstruktorem

### Po refactoringu:

```csharp
// Jedna univerzální třída (ne abstract!)
public class Section {
    string Name                          // editovatelné
    SectionPrintBehavior PrintBehavior   // editovatelné
    SectionOverflowBehavior OverflowBehavior // editovatelné
    float? DesignerHeight                // editovatelné
    string? VisibilityExpression         // NOVÉ — podmíněná viditelnost

    // Speciální properties (pro některé použití)
    string? DataSource                   // pro PrintBehavior=PerDataRow
    float? RowHeight                     // pro PrintBehavior=PerDataRow

    List<TemplateElement> Elements
}

enum SectionPrintBehavior {
    Once,
    OnEveryPage,
    OnFirstPageOnly,
    OnLastPageOnly,
    OnEveryPageExceptFirst,
    PerDataRow                           // NOVÉ — pro tabulky s vlastním designem řádků
}

// Podtřídy existují JEN pro zpětnou kompatibilitu JSON
[Obsolete("Použijte přímo Section s PrintBehavior=OnEveryPage")]
public class SectionHeader : Section {
    public SectionHeader() {
        Name = "Záhlaví";
        PrintBehavior = OnEveryPage;
        DesignerHeight = 25f;
    }
}
```

---

## Výhody

### 1. Flexibilita
Uživatel může vytvořit **libovolnou kombinaci** chování bez nutnosti "hack" existujících typů:
- Záhlaví jen na první stránce: `PrintBehavior=OnFirstPageOnly`
- Záhlaví kromě první: `PrintBehavior=OnEveryPageExceptFirst`
- Záhlaví jen na sudých stránkách: `PrintBehavior=OnEveryPage` + `VisibilityExpression="{PAGE} % 2 == 0"`

### 2. Jednoduchost
- **Kód:** 1 třída místo 7
- **JSON:** Nové šablony ukládají `"type": "Section"` místo `"type": "SectionHeader"`
- **UI:** Properties panel je stejný pro všechny sekce → konzistentní UX

### 3. Extensibility
Přidání nového chování = přidání enum hodnoty, ne nové třídy.

Příklad: Budoucí `SectionPrintBehavior.OnAlternatePages` (sudé/liché stránky) = **jedna řádka kódu**.

### 4. Zpětná kompatibilita zachována
Staré JSON šablony s `"type": "SectionHeader"` se načtou správně díky zachovanému polymorphismu.

---

## Migrace pro vývojáře

### Vytvoření nové sekce:

**Před:**
```csharp
var section = new SectionHeader();  // Musel jsem znát správný typ
```

**Po:**
```csharp
var section = new Section {
    Name = "Záhlaví stránky",
    PrintBehavior = SectionPrintBehavior.OnEveryPage,
    DesignerHeight = 25f
};
```

Nebo použít factory konstruktor pro rychlost (zpětná kompatibilita):
```csharp
var section = new SectionHeader();  // Stále funguje, jen je deprecated
```

### Type check:

**Před:**
```csharp
if (section is SectionTableDesigned tableSection) {
    // Přístup k DataSource, RowHeight
}
```

**Po:**
```csharp
if (section.PrintBehavior == SectionPrintBehavior.PerDataRow) {
    // DataSource a RowHeight jsou přímo v Section
    string dataSource = section.DataSource ?? "";
    float rowHeight = section.RowHeight ?? 8f;
}
```

### Properties panel:

**Před:** Různé properties pro různé typy sekcí.

**Po:** Properties se zobrazují/skrývají podle `PrintBehavior`:
```csharp
if (section.PrintBehavior == SectionPrintBehavior.PerDataRow) {
    // Zobraz "Datový zdroj" a "Výška řádku"
}
```

---

## UI změny (budoucí krok 2)

### Současný stav:
```
[+ Přidat sekci ▾]
  → Sekce hlavička
  → Sekce s pevnou výškou
  → Sekce s tabulkou
  → Sekce s tabulkou s designem řádku
  → Sekce s tabulkou s pružnou výškou
  → Sekce s kombinovanou tabulkou
  → Sekce s nekonečným textem
  → Sekce oddělovač
  → Sekce patička
```

### Po dokončení refactoringu UI:
```
[+ Přidat sekci ▾]

  Běžné:
  ├─ Hlavička stránky          (PrintBehavior=OnEveryPage)
  ├─ Zápatí stránky            (PrintBehavior=OnEveryPage)
  ├─ Detail (per řádek)        (PrintBehavior=PerDataRow)
  ├─ Oddělovač                 (PrintBehavior=Once, Height=5mm)

  Pokročilé:
  ├─ Hlavička sestavy          (PrintBehavior=OnFirstPageOnly)
  ├─ Zápatí sestavy            (PrintBehavior=OnLastPageOnly)
  ├─ Hlavička kromě první str. (PrintBehavior=OnEveryPageExceptFirst)

  Ad-hoc:
  └─ ⚙ Prázdná sekce (obecná)  (uživatel nastaví vše ručně)
```

→ **Jednoduší menu**, **jasnější pojmenování**, **méně redundance**.

---

## Zpětná kompatibilita JSON

Starý formát (před refactoringem):
```json
{
  "type": "SectionHeader",
  "name": "Záhlaví",
  "designerHeight": 25,
  "elements": [...]
}
```

Nový formát (po refactoringu):
```json
{
  "type": "Section",
  "name": "Záhlaví stránky",
  "printBehavior": "OnEveryPage",
  "designerHeight": 25,
  "elements": [...]
}
```

**Obě formy se načtou správně** díky zachovanému `JsonPolymorphic` + `JsonDerivedType`.

---

## Další kroky

1. ✅ **Model sloučen** (1 třída Section)
2. ✅ **Zpětná kompatibilita** (podtřídy jako deprecated factory konstruktory)
3. ✅ **Build úspěšný**
4. ⏳ **UI presety** — zjednodušit menu "Přidat sekci"
5. ⏳ **Custom šablony** — uživatel si ukládá často používané sekce
6. ⏳ **Dokumentace uživatelská** — návod jak používat nový model

---

## Poznámky pro budoucí rozšíření

### Grouping (Group Header/Footer):
Nový enum:
```csharp
enum SectionPrintBehavior {
    // ... stávající
    GroupHeader,   // Per skupina dat (např. per linka v jízdním řádu)
    GroupFooter
}
```

Nová property:
```csharp
class Section {
    string? GroupByField;  // např. "CISLO_LINKY" — pole, podle kterého se grupuje
}
```

### Podmíněná viditelnost:
Už implementováno! Property `VisibilityExpression` podporuje:
- `"{PAGE} > 1"` — kromě první stránky
- `"{PAGE} % 2 == 0"` — sudé stránky
- `"{POCET} > 0"` — pokud jsou data

Print engine vyhodnotí, designer zobrazuje vždy.

---

## Cleanup — odstranění deprecated kódu (2026-03-02)

**Rozhodnutí:** Odstranit všechny deprecated podtřídy sekcí. Projekt je ve vývoji, zpětná kompatibilita JSON není priorita.

**Provedeno:**
1. Smazána celá složka `Models/Sections/` (7 souborů)
2. Odstraněny `JsonPolymorphic` a `JsonDerivedType` atributy z `Section.cs`
3. Odstraněn `using TemplateDesigner.Models.Sections;` ze všech souborů
4. Nahrazeny všechny instance `new SectionHeader()` → `new Section { PrintBehavior = OnEveryPage, ... }`
5. Type check `section is SectionTableDesigned` → `section.PrintBehavior == PerDataRow`

**Výsledek:**
- ✅ Kód je čistší a udržitelnější
- ✅ Žádné deprecated třídy
- ✅ Build úspěšný (0 chyb, 31 warnings)
- ⚠️ **Staré JSON šablony se NENAČTOU** (to je OK — projekt je ve vývoji)

**JSON formát po cleanup:**

Před (polymorphic):
```json
{
  "type": "SectionHeader",
  "name": "Záhlaví",
  "designerHeight": 25,
  "elements": [...]
}
```

Po (přímá serializace):
```json
{
  "name": "Záhlaví",
  "printBehavior": "OnEveryPage",
  "designerHeight": 25,
  "elements": [...]
}
```

→ **Jednodušší a čitelnější JSON** bez type discriminatoru.

---

## Krok 2 — UI přepracování menu (2026-03-02)

**Cíl:** Zjednodušit menu "Přidat sekci" na logické presety.

**Změny:**

### Před refactoringem:
```
[+ Přidat sekci ▾]
  → Záhlaví
  → Pevná výška
  → Tabulka
  → Tabulka (vlastní design)
  → Nekonečný text
  → Oddělovač
  → Zápatí
```

### Po refactoringu (finální):
```
[+ Přidat sekci ▾]

  Běžné:
  → Hlavička stránky
  → Patička stránky
  → Oddělovač
  ─────────────────────
  Pokročilé:
  → Hlavička sestavy (jen 1. str.)
  → Patička sestavy (jen posl. str.)
  → Hlavička kromě 1. str.
  ─────────────────────
  Ad-hoc:
  → ⚙ Prázdná sekce
```

**Proč tyto změny:**

| Staré | Nové | Důvod |
|---|---|---|
| "Záhlaví" / "Zápatí" | "Hlavička stránky" / "Patička stránky" | Běžnější české výrazy, jasnější význam |
| "Tabulka (vlastní design)" | ❌ Odstraněno | **Chybný koncept** — logika opakování patří do TableElement, ne sekce! |
| "Pevná výška" | "⚙ Prázdná sekce" | Méně matoucí — uživatel nastaví vše ručně |
| "Tabulka" | ❌ Odstraněno | Tabulka je element, ne sekce! |
| "Nekonečný text" | ❌ Odstraněno | Příliš specifické, lze vytvořit přes "Prázdná sekce" |

**Nové položky:**
- **Hlavička sestavy (jen 1. str.)** — `PrintBehavior=OnFirstPageOnly` — pro titulní stránku
- **Patička sestavy (jen posl. str.)** — `PrintBehavior=OnLastPageOnly` — pro závěrečné info
- **Hlavička kromě 1. str.** — `PrintBehavior=OnEveryPageExceptFirst` — alternativní záhlaví
- **⚙ Prázdná sekce** — obecná sekce s výchozími hodnotami, uživatel nastaví vše v properties

**Důležité architektonické rozhodnutí:**
- ❌ **`PrintBehavior.PerDataRow` je LEGACY** — ponechán v enum pro edge cases, ale NENÍ v UI menu
- ✅ **Tabulky jsou elementy (TableElement)**, ne sekce — logika opakování řádků patří do elementu
- ✅ Sekce jsou jen **kontejnery** s metadata "kdy tisknout", **ne datová logika**

**Výsledek:**
- ✅ Jednodušší výběr pro běžné případy (3 položky)
- ✅ Pokročilé možnosti jsou dostupné (3 položky)
- ✅ Flexibilita přes "Prázdná sekce"
- ✅ Čistší separace concerns (sekce = layout, elementy = data)

---

## Krok 3 — Custom šablony sekcí (2026-03-02)

**Cíl:** Umožnit uživateli ukládat často používané sekce jako šablony.

**Implementace:**

### Backend:
- `SectionTemplateManager` — správa custom šablon
- File-based V1: ukládá do `section-templates/*.json` vedle .exe
- Připraveno na DB backend V2 (swap FileSectionTemplateProvider → CacheSectionTemplateProvider)
- `TemplateSerializer` rozšířen o `SerializeSection()` / `DeserializeSection()`
- `Section.Clone()` — deep copy včetně elementů s novými Guid

### UI — Uložení:
- Pravý klik na sekci v levém panelu → **"💾 Uložit jako šablonu..."**
- Dialog pro zadání názvu
- Uložení do `section-templates/{sanitized-název}.json`

### UI — Načtení:
- Menu `[+ Přidat sekci ▾]` automaticky načte všechny `*.json` šablony
- Zobrazí se s ikonou 💾 před názvem
- Deep clone s novými Guid při vložení

### Formát JSON:
```json
{
  "name": "Jízdní řád",
  "printBehavior": "Once",
  "overflowBehavior": "SplitAcrossPages",
  "elements": [
    {
      "type": "TableElement",
      "dataSource": "JIZDNI_RAD",
      "columns": [ ... ]
    }
  ]
}
```

→ **Přesně stejný formát jako sekce v Template JSON**

### Výhody:
- ✅ Okamžitě použitelné (file-based)
- ✅ Portable (zkopíruješ složku = máš šablony)
- ✅ Připraveno na DB upgrade (stejné rozhraní)
- ✅ Sdílení mezi kolegy (přes sdílený disk)

### Budoucnost:
- V2: DB backend přes Caché (`CacheSectionTemplateProvider`)
- V3: Hybrid (lokální WIP šablony + firemní sdílené z DB)

---

**Datum:** 2026-03-02
**Autor:** Claude Sonnet 4.5 + User
**Status:** ✅ Kroky 1, 2 a 3 dokončeny (model + cleanup + UI + custom šablony)
