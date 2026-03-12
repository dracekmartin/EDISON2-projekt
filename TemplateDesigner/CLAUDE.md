# CLAUDE.md — Kontext pro AI agenta

## Co je tento projekt

**TemplateDesigner** je WPF desktopová aplikace pro tvorbu šablon tiskových sestav.
Náhrada staršího nástroje postaveného v InterSystems Caché/Globals.
Při navrhování nových featur, bez v potaz, že v budoucnu by mohl být nástroj, alespoň částečně, ovládán pomocí AI.Dobre

Systém: **1. Designer** (tento projekt) — vizuální návrh šablon → **2. Print Engine** (budoucí) — šablona + data = PDF.

---

## ⚡ Aktuální stav projektu

> **📝 PRO AI AGENTA:** Průběžně aktualizuj. Dokončené → přesuň do "✅ Co máme". Nové problémy → "🐛 Známé problémy".

**Datum poslední aktualizace:** 2026-03-13

### 🎯 Aktuální focus

*(žádný konkrétní — CultureInfo v šabloně dokončen, viz roadmap pro další možnosti)*

### ✅ Co máme

- Text, Image, Rectangle, Line, BarcodeElement (QR + Code128), TableElement (Fixed/Dynamic)
- Parametry sestavy (Part 1 + serializace do JSON), VisibilityExpression evaluator
- Výrazový jazyk kompletní: podmínky (`&&`, `||`, `!`), formátování (`{POLE:N2}`, `{POLE:UPPER}`, `{POLE:LEFT(4)}`), agregace (`SUM`, `COUNT`, `AVG`, `MIN`, `MAX`) v souhrnných řádcích tabulky
- Live preview, PDF export, DataSchema tree, section management, undo/redo (10 cmd typů)
- `TemplateDesignerViewModel : EDViewModel` — MVVM, duální startup (standalone + EDISON)
- Custom section templates (file-based), CompositeDataSchemaProvider (Caché + JSON fallback)
- Smart snap — přichytávání elementů k sobě (edge↔edge, center↔center, gap snap 4mm), zvýraznění cílových elementů, gap indikátor
- Chytré duplikování — Ctrl+D+šipka umístí kopii vedle originálu s 4mm mezerou; Ctrl+D bez šipky = plain duplikát (+5mm offset); neovlivňuje clipboard
- Drag & drop elementů z palety na canvas — ghost preview, zvýraznění cílové sekce, clamp + snap na mřížku
- Drag & drop databázových polí z panelu "Pole agendy" na canvas — vytvoří TextElement s `{POLE}`, nebo připojí placeholder k existujícímu TextElementu
- Drag & drop řazení sekcí v levém panelu — přetažením místo tlačítek ▲▼, vizuální indikátor pozice
- Locale / CultureInfo v šabloně — `Template.CultureCode` (cs-CZ, sk-SK, en-US, de-DE); formátování čísel a datumů nezávislé na systémovém nastavení Windows
- ElementGroup — seskupování elementů (`GroupId` property); klik vybere celou skupinu, Alt+klik jen jeden; Ctrl+G/Ctrl+Shift+G; vizuální badge + bounding box; hierarchický strom; PDF import auto-groups
- PDF import — `PdfImportDialog` s PdfPig extrakcí; 3 merge módy grafiky (None/LinesOnly/All); detekce okrajů; auto-grouping; texty seskupené do bloků; async konverze
- Strom šablony — záložka "Strom" v hlavním okně; hierarchický přehled celé šablony (sekce → skupiny → elementy)
- LineElement — custom dash/gap pro čárkované a tečkované čáry
- ImageElement — SKImage rendering s SKSamplingOptions (kvalitnější škálování)

### 🐛 Známé problémy

*(žádné aktivní)*

---

## Technologický stack

| Technologie | Použití |
|---|---|
| .NET 9, WPF | UI framework |
| SkiaSharp 3.119.2 | Renderování canvasu i PDF |
| SkiaSharp.Views.WPF 3.119.2 | SKElement WPF kontrola |
| System.Text.Json | Serializace šablon |
| EDControls (ProjectReference) | EDViewModel, RelayCommand |
| ZXing.Net 0.16.9 | BarcodeElement |
| PdfPig 0.1.9 | PDF import (extrakce textu, čar, obrázků) |

---

## Pravidla pro psaní kódu

- **Jazyk kódu: anglicky** (třídy, metody, proměnné); komentáře a UI texty: **česky**
- Každá akce uživatele = `IDesignerCommand` (undo/redo podporováno)
- Pozice elementů jsou **relativní k sekci**, jednotky = **milimetry**
- `Height = null` = dynamická výška (print engine počítá); `IsLocked = true` = nelze editovat
- `VisibilityExpression = null` = vždy viditelný; neprázdný = výraz vyhodnocuje ExpressionEvaluator
- `SKElement.PaintSurface` registrovat **jen v code-behind**, nikdy v XAML
- **Vždy `CVar`** pro MLRest volání; nikdy `object[]` v novém kódu
- Data binding syntaxe: `{NAZEV_POLE}` — konzistentní napříč celým systémem
- ObjectScript: **nikdy `_` v dot-notation** (`obj.KEY_NAME`) — `_` je operátor konkatenace; použij `obj.%Set("KEY_NAME", v)`
- Nová okna vždy v `dotnet/Windows/<Název>/` se vzorem `Window + ViewModel : EDViewModel`

---

## Co projekt není

- Není to WYSIWYG Word/Excel editor
- Sekce jsou povinné — ne free-form canvas bez struktury
- Print engine (krok 2) je **samostatný projekt** — designer pouze ukládá JSON šablony
- V1 nepodporuje import starých šablon z InterSystems Globals

---

## Detailní dokumentace (`.claude/rules/`)

Soubory se načítají automaticky při práci na relevantních souborech.

| Soubor | Obsah | Načítá se pro |
|---|---|---|
| `architecture.md` | Struktura projektu, zodpovědnosti, MVVM hranice, souřadnice, DesignerState | `dotnet/**` |
| `data-model.md` | C# datové třídy, JSON formáty, renderovací pipeline, interakční model | `dotnet/Models/**` |
| `mvvm-patterns.md` | PropertyDescriptor, TableElement architektura, jak přidat element/okno | `dotnet/Windows/**` |
| `cache-api.md` | AgendaAPI architektura, wire formáty, Caché skeleton, nová agenda checklist | `CACHE/**` |
| `implementation-patterns.md` | Barvy, RenderContext, ComboBox gotcha, SkiaSharp quirks | `dotnet/**/*.cs` |

Ruční reference:
- `.claude/docs/changelog.md` — kompletní changelog
- `.claude/docs/roadmap.md` — plná roadmapa s detaily
