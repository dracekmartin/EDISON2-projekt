# Glob: dotnet/Windows/**

## UX konvence — co už dodržujeme

Tento soubor formalizuje patterny, které se v TD už používají. Cíl: zachovat **konzistenci napříč okny** při přidávání nových dialogů a featur.

> **Související:** vzor okna a MVVM hranice viz `architecture.md` §Vzor pro každé okno.

---

## 1. Dialogová tlačítka a struktura okna

**Vzor okna / dialogu (struktura souborů, ViewModel, code-behind, XAML binding, IsDefault/IsCancel, Owner, RequestClose):** viz `architecture.md §Vzor pro každé okno`. Je závazný standard, nepřepisuj sem.

**Co doplňuje tento doc** (UX-specifické konvence, ne kódový template):

- **Labely standardních tlačítek přes `{StaticResource EDStrings.*}`** — jediný zdroj pravdy (EDStrings.xaml je mergnutý v App.xaml). Nepoužívej literály:
  - `EDStrings.Cancel` = **„Storno"** (firemní standard — EDFooter i EDStrings; NE „Zrušit")
  - `EDStrings.Ok` = „OK", `EDStrings.Save` = „Uložit", `EDStrings.Close` = „Zavřít"
  - IsCancel tlačítko = vždy `EDStrings.Cancel`. IsDefault (primární) = buď `EDStrings.Ok`, nebo **akční sloveso** k obsahu dialogu („Otevřít", „Použít", „Přidat") — akční sloveso je vhodnější, když je jasnější než generické OK.
- Tlačítka **akčně pojmenovaná**, ne „Ano/Ne" — např. „Smazat" / „Storno", „Uložit" / „Zahodit", ne „Yes" / „No".
- Destruktivní akce a confirmation → vždy přes `IPopupDisplayer.Confirm` z EDControls (R2), nikdy `MessageBox.Show`.
- Multi-tab dialog: **max 3 taby**. Pokud potřebuješ víc, rozděl do samostatných dialogů nebo restrukturuj.

---

## 2. Tooltipy

- **Každý ikonový button** v toolbaru / paletě **musí mít** `ToolTip` — bez textu je tlačítko slepé.
- Tooltip format: `"Akce — klávesa"` (např. `"Duplikovat — Ctrl+D"`).
- Pokud akce nemá keyboard shortcut, jen `"Akce"`.

---

## 3. Keyboardové zkratky — autoritativní tabulka

### Edit operace

| Zkratka | Akce |
|---|---|
| Ctrl+Z / Ctrl+Y | Undo / Redo |
| Ctrl+X / Ctrl+C / Ctrl+V | Cut / Copy / Paste |
| Ctrl+D | Duplikovat (plain duplikát +5mm offset) |
| Ctrl+D + šipka | Směrové duplikování (kopie vedle s 4mm mezerou) |
| Ctrl+Shift+C / Ctrl+Shift+V | Copy / Paste **stylu** (TextStyle + Border + Background) |
| Delete | Smazat výběr |
| Ctrl+A | Vybrat vše (v aktivní sekci) |
| Enter | Otevřít dialog vlastností výběru |
| Dvojklik | Otevřít dialog vlastností elementu |

### Grouping / layout

| Zkratka | Akce |
|---|---|
| Ctrl+G / Ctrl+Shift+G | Seskupit / Rozeskupit elementy |
| Alt+klik | Vybrat jen jeden element ze skupiny |
| Ctrl+Shift+H / Ctrl+Shift+J | Split element horizontálně / vertikálně |
| Ctrl+Shift+N | Normalizovat pozice (snap na grid) |

### Soubory a view

| Zkratka | Akce |
|---|---|
| Ctrl+S / Ctrl+Shift+S | Uložit / Uložit jako |
| Ctrl+O | Otevřít šablonu |
| Ctrl+N | Nová šablona |
| Ctrl+0 | Fit-to-Page (zoom) |
| F1 | Nápověda |

### Pravidla pro nové zkratky

- **Nepřebíjet** standardní Windows zkratky (Ctrl+S, Ctrl+Z, Ctrl+C, Alt+F4, F1, Esc).
- **Ctrl+Shift+X** prostor je pro „advanced" varianty (Save As, Copy Style).
- **Alt+X** prostor je pro alternativní výběr (Alt+klik).
- Při přidání nové zkratky — **doplnit ji do této tabulky**.

---

## 4. Properties panel a dialog

> Detail viz `feature-design-principles.md` §2 UX rozhodovací mřížka.

- **Panel** = nejčastěji měněné properties (rychlý přístup).
- **Dialog** = kompletní cascade, per-column granularita.
- **Šablona / preset** = per-customer kombinace, ne element properties.
- Tri-state placeholder: `null = dědit` → XAML zobrazí *italic šedý* placeholder zděděné hodnoty; „×" reset jen při explicit override.
- Multi-element edit: Enter na multi-select → dialog společných vlastností.

---

## 5. Context menu

- Pravý klik na canvasu = sloučené menu (element items + section items).
- Pořadí položek pro elementy: **Cut / Copy / Paste / Delete / --- / Z-order / --- / Vlastnosti**.
- Separátory oddělují skupiny akcí (editační / pořadí / detail).

---

## 6. Toolbar / paleta

- Ikony z **Fluent UI System Icons** (Microsoft, MIT licence) — viz `EDIcons.xaml`.
- Skupiny tlačítek oddělené `Separator` (Soubor / Upravit / Přidat / Elementy / Vrstva / Zarovnat / Zobrazení).
- Drag-from-palette: `PreviewMouseLeftButtonDown` → ghost preview na canvasu → drop = `AddElementCommand`.

---

## 7. Confirmation a chybové stavy

- **Mazání sekce / šablony** → confirmation dialog povinný (přes `IPopupDisplayer.Confirm`).
- **Mazání elementu** → bez confirmation (Ctrl+Z to vrátí).
- **Neuložené změny při zavření** → `IPopupDisplayer.ConfirmClose` se 3 možnostmi (Save / Discard / Cancel).
- **Validace v UI** — červený rámeček + tooltip s důvodem (B0-11 pattern).

---

## 8. Co `ux-conventions.md` **neřeší**

- Konkrétní rozměry oken (default sizes) — záměrně, je to per-dialog.
- Konkrétní wording („Opravdu chcete..." vs „Smazat?") — záměrně, vyžaduje review s týmem.
- Barvy / theming — záměrně, doménová decision.
- Loading state UX — řešeno per-case (`EDActionInProgress` overlay pro long ops).

Pokud některá z těchto otázek vyplyne v konkrétním requestu, řeší se ad hoc + případně se sem doplní policy.
