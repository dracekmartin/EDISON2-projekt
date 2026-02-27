# Architektura a UX — Template Designer

---

## Přehled systému

Systém se skládá ze dvou nezávislých částí:

**1. Designer** — WPF aplikace pro tvorbu šablon
- Uživatel vizuálně skládá šablonu ze sekcí a elementů
- Výstup: JSON soubor šablony

**2. Print Engine** (budoucí projekt)
- Vstup: JSON šablona + data
- Výstup: PDF soubor
- Renderování pomocí stejného SkiaSharp kódu jako designer (sdílená logika)

Oddělení těchto dvou částí je záměrné — designer neví nic o datech, print engine neví nic o UI.

---

## Konceptuální model šablony

### Stránka
Uživatel definuje rozměry stránky v mm (např. A4 = 210×297 mm) a okraje stránky (margins). Okraje jsou součástí definice stránky, ne sekcí.

```
PageSize: width, height, marginTop, marginBottom, marginLeft, marginRight  (vše v mm)
```

### Sekce
Stránka je vertikální sled sekcí. Sekce nelze umístit volně — přidávají se vždy dolů za poslední sekci, pořadí lze měnit drag & drop v panelu sekcí. Každá sekce má:
- Typ (záhlaví, tabulka, zápatí, oddělovač atd.)
- Výšku — pouze u statických sekcí (pevná výška v mm). Dynamické sekce (tabulka, nekonečný text) výšku nemají — ta se generuje za běhu podle dat
- Chování při tisku (viz níže)
- Seznam elementů uvnitř

### Elementy
Uvnitř sekce lze elementy umístit volně (absolutní pozicování v mm, relativně k sekci). Elementy lze přesunovat myší nebo klávesnicí, měnit jejich velikost a překrývat.

**Pozicování elementů:**
- Statické elementy (text, obdélník, obrázek, čára): `x, y, width, height` — vše v mm
- Dynamické elementy (TableElement): `x, y, width` — výška se neukládá, print engine ji vypočítá
- Print engine bere pozice z JSON jak jsou — žádný přepočet layoutu neprovádí (kromě dynamických výšek)

**Vlastnosti každého elementu:**
- `IsLocked` — uzamčený element nelze v designeru omylem posunout ani smazat
- `VisibilityExpression` — podmíněná viditelnost vyhodnocovaná v print enginu, např. `{POCET_RADU} > 0`; prázdné = vždy viditelný

---

## Chování při tisku (paginace)

Toto je klíčová vlastnost pro správný vícestránkový tisk.

### PrintBehavior — kdy se sekce tiskne
| Hodnota | Popis | Typické použití |
|---|---|---|
| `Once` | Jednou, na místě kde je | Běžná sekce s obsahem |
| `OnEveryPage` | Na každé stránce | Záhlaví, zápatí |
| `OnFirstPageOnly` | Pouze na první stránce | Úvodní blok |
| `OnLastPageOnly` | Pouze na poslední stránce | Závěrečný blok |
| `OnEveryPageExceptFirst` | Na každé stránce kromě první | Alternativní záhlaví |

### OverflowBehavior — co se stane když se sekce nevejde
| Hodnota | Popis | Typické použití |
|---|---|---|
| `KeepTogether` | Celá sekce přejde na další stránku | Záhlaví, zápatí, krátké bloky |
| `SplitAcrossPages` | Sekce se rozdělí — zbytek pokračuje na další stránce | Tabulky, jízdní řády |
| `AllowSplit` | Rozdělení povoleno ale ne preferováno | Textové bloky |

### Příklad — jízdní řád na více stránkách
```
Sekce záhlaví      → PrintBehavior: OnEveryPage,    OverflowBehavior: KeepTogether
Sekce jízdní řád   → PrintBehavior: Once,           OverflowBehavior: SplitAcrossPages
Sekce zápatí       → PrintBehavior: OnEveryPage,    OverflowBehavior: KeepTogether
```
Výsledek: záhlaví a zápatí se opakují na každé stránce, jízdní řád se rozdělí přes tolik stránek kolik potřebuje.

---

## Typy elementů

### TextElement
- Statický text nebo datově vázaný (placeholder jako `{JMENO_RIDICE}`)
- Nastavení: font, velikost, barva, zarovnání, tučné/kurzíva
- Víceřádkový text se zalomením

### ImageElement
- Statický obrázek (logo, razítko) nebo z datového zdroje
- Nastavení: způsob škálování (fill, fit, stretch, none)

### RectangleElement
- Obdélník pro vizuální oddělení nebo pozadí
- Nastavení: barva výplně, barva okraje, tloušťka okraje, zaoblení rohů

### LineElement
- Horizontální nebo vertikální čára
- Nastavení: barva, tloušťka, styl (plná, čárkovaná, tečkovaná)

### TableElement
- Speciální komponenta s vlastní logikou
- Definuje sloupce (šířka, záhlaví, datový zdroj sloupce)
- Řádky se generují z dat za běhu v print enginu
- V designeru se zobrazuje jako náhled se vzorkovými daty nebo prázdná mřížka

---

## UI/UX — Designer

### Rozložení hlavního okna

```
┌──────────────────────────────────────────────────────────────────────┐
│  Menu bar: Soubor | Upravit | Zobrazit | Nástroje                    │
├──────────────────────────────────────────────────────────────────────┤
│  Toolbar: [+ Sekce ▾] [+ Element ▾] | [Zpět] [Vpřed] | [Zoom ▾]    │
│           [Grid ☐] [Snap ☐] | [Uložit] [Otevřít]                    │
├────────────────┬─────────────────────────────┬───────────────────────┤
│ Panel sekcí    │                             │                       │
│ (~200px)       │   Canvas (SKElement)        │  Live preview         │
│                │   — hlavní editační plocha  │  (~300px, skrytý)     │
│ Seznam sekcí   │                             │                       │
│ s ikonkou typu │                             │  Zobrazuje celou      │
│ D&D reorder    │                             │  stránku zmenšeně     │
│                │                             │  s mock daty          │
│ [+ Přidat]     │                             │                       │
├────────────────┴─────────────────────────────┴───────────────────────┤
│  Panel vlastností (~200px, kontextový podle výběru)                  │
└──────────────────────────────────────────────────────────────────────┘
```

Všechny tři postranní panely (sekce, preview, vlastnosti) jsou skryvatelné aby měl uživatel při editaci maximum prostoru pro canvas. Layout je implementován jako WPF `Grid` s `GridSplitter` — změna rozložení je triviální.

### Canvas — co musí fungovat

**Výběr:**
- Klik na element → vybere element (zobrazí handles)
- Shift+klik → přidá do výběru
- Ctrl+A → vybere vše v aktivní sekci
- Klik na prázdné místo → zruší výběr
- Tažení na prázdném místě → rubber band selection (obdélník výběru)

**Přesun:**
- Tažení myší → přesune vybraný element(y)
- Šipky na klávesnici → přesun o 1px (nebo o velikost gridu pokud snap zapnut)
- Shift+šipky → přesun o 10px

**Změna velikosti:**
- 8 resize handles kolem vybraného elementu (rohy + středy hran)
- Tažení handle → změna velikosti

**Snap to grid:**
- Volitelný, zapíná se v toolbaru
- Velikost gridu konfigurovatelná
- Grid se vizuálně zobrazuje na canvasu

**Zoom:**
- Ctrl+kolečko myši → zoom in/out
- Tlačítka v toolbaru (100%, fit page atd.)

**Undo/Redo:**
- Ctrl+Z → undo
- Ctrl+Y / Ctrl+Shift+Z → redo
- Každá akce (přesun, resize, přidání, smazání) je undoable

### Přidání sekce

Při přidání nové sekce má uživatel dvě možnosti:
- **Prázdná sekce** — přidá se bez elementů, uživatel plní ručně
- **Sekce s výchozí komponentou** — každý typ sekce může mít předpřipravený výchozí obsah (např. `SectionHeader` rovnou obsahuje `TextElement` s placeholderem názvu, `SectionTable` obsahuje prázdný `TableElement`)

Výchozí obsah sekcí je konfigurovatelný a slouží jako rychlý start — uživatel nemusí začínat od nuly u každého běžného případu. Elementy lze do sekce kdykoliv přidat, odebrat nebo přesunout i po vytvoření.

**Sekce — správa:**
- Drag & drop v panelu sekcí vlevo → změna pořadí sekcí
- Každá sekce má v panelu kontextové menu (přidat element, přejmenovat, smazat)

**Uzamčení elementu:**
- Element lze uzamknout přes panel vlastností nebo kontextové menu
- Uzamčený element nelze přesunout, změnit velikost ani smazat bez předchozího odemčení
- Vizuálně odlišen (jiná barva handles nebo ikona zámku)

**Podmíněná viditelnost:**
- Každý element má volitelný výraz `VisibilityExpression`, např. `{POCET} > 0`
- V designeru se element zobrazuje vždy (výraz se vyhodnocuje až v print enginu)
- V panelu vlastností je jasně vidět že element má podmíněnou viditelnost

### Co designer záměrně nepodporuje (v1)

- Import šablon ze starého systému (InterSystems Globals)
- Více otevřených šablon najednou (záložky)
- Otočení elementů (rotate)
- Zarovnávací nástroje (align left, center atd.) — připravit ale neimplementovat
- Náhled s reálnými daty
- Tisk přímo z designeru

---

## Formát šablony (JSON)

Šablona se ukládá jako JSON soubor. Formát je verzovaný pro budoucí migrace.

```json
{
  "version": "1.0",
  "id": "...",
  "name": "Jízdní řád CB_DP",
  "pageSize": {
    "width": 210, "height": 297,
    "marginTop": 10, "marginBottom": 10, "marginLeft": 10, "marginRight": 10
  },
  "sections": [
    {
      "id": "...",
      "type": "SectionHeader",
      "name": "Záhlaví",
      "designerHeight": 25,
      "printBehavior": "OnEveryPage",
      "overflowBehavior": "KeepTogether",
      "elements": [
        {
          "type": "TextElement",
          "id": "...",
          "x": 10, "y": 5, "width": 190, "height": 12,
          "isLocked": false,
          "visibilityExpression": null,
          "text": "Jízdní řád {NAZEV}",
          "fontSize": 14,
          "fontWeight": "Bold"
        }
      ]
    },
    {
      "id": "...",
      "type": "SectionTable",
      "name": "Jízdní řád",
      "designerHeight": null,
      "printBehavior": "Once",
      "overflowBehavior": "SplitAcrossPages",
      "elements": [
        {
          "type": "TableElement",
          "id": "...",
          "x": 5, "y": 0, "width": 200,
          "height": null,
          "isLocked": false,
          "visibilityExpression": null,
          "dataSource": "JIZDNI_RAD",
          "rowHeight": 6.0,
          "columns": [
            { "header": "Čas", "width": 20, "field": "CAS" },
            { "header": "Linka", "width": 30, "field": "LINKA" }
          ]
        }
      ]
    }
  ]
}
```

---

## Rozšiřitelnost

Systém musí umožňovat přidávání nových typů bez zásahu do existujícího kódu:

- Nový typ elementu = nová třída dědící z `TemplateElement`
- Nový typ sekce = nová třída dědící z `Section`
- Každý element si renderuje sám sebe
- Print engine v kroku 2 používá stejné třídy elementů

---

## Live Preview (nice-to-have, není priorita v1)

Pravý panel designeru může zobrazovat živý náhled šablony s mock daty — uživatel vidí přibližný výsledek tisku při každé změně.

### Jak to funguje technicky
Renderovací kód elementů je sdílený — každý element implementuje `Render(SKCanvas, DataContext?)`. Preview panel pouze zavolá stejný render kód s mock `DataContext` místo reálných dat. Není potřeba žádná extra logika renderování.

```
Designer canvas     → Render(canvas, null)         → editační pohled
Preview panel       → Render(canvas, mockData)     → náhled výsledku
Print engine        → Render(canvas, realData)     → finální PDF
```

### Mock data
- Každá šablona může mít uložená vzorová data (součást JSON souboru)
- Nebo hardcoded fallback hodnoty per typ elementu (např. `TextElement` zobrazí název placeholderu)
- `TableElement` zobrazí pevný počet vzorových řádků (např. 3–5)

### UI
- Druhý `SKElement` v pravém panelu vedle canvasu
- Překresluje se při každé změně šablony (s drobným debounce aby nezdržoval editaci)
- Zobrazuje celou stránku ve zmenšeném měřítku (fit-to-panel zoom)
- Lze přepnout na celostránkový náhled

### Co preview nezobrazuje přesně
- Přesné zalomení stránek (to závisí na reálném objemu dat)
- Dynamické výšky sekcí s reálnými daty
- Víceřádkový text přesahující sekci

---

## Co tento systém řeší oproti starému nástroji

| Starý nástroj | Nový designer |
|---|---|
| Uzavřený v InterSystems Globals | Otevřený JSON formát |
| Nelze rozšiřovat o nové typy | Plně rozšiřitelná architektura |
| Žádné undo/redo | Plný undo/redo stack |
| Omezené možnosti layoutu | Svobodné umístění uvnitř sekcí |
| Tightly coupled s print logikou | Čisté oddělení designer / print engine |