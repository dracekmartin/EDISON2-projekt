# Roadmap

## 🔁 Pracovní postup (krok po kroku)

Každá položka projde tímto cyklem — nevynechat ani jeden krok:

1. **Projít** — připomenout co přesně má krok dělat, kontext a závislosti
2. **Validovat** — ověřit, že to dává smysl dělat teď (priority, závislosti, scope)
3. **Implementovat** — kód, Caché, XAML — dle pravidel v CLAUDE.md
4. **Otestovat** — ručně projít golden path + edge cases; u UX ověřit v běžícím buildu
5. **Aktualizovat docs** — changelog + roadmap (přesun do archivu) + CLAUDE.md pokud se mění architektura
6. **Další krok** — posunout `⬅️` na následující položku

---

## 🎯 Aktuální focus

1. Zastávkové JŘ end-to-end proof of concept: odladit elementy (StopList, DepartureGrid, NotesLegend), data z Caché. Cíl: funkční tisk MHD + meziměstských JŘ.
2. Ladění zastávkového JŘ na reálných datech — edge cases, vizuální shoda s referenčním PDF (krok 8 níže).
3. **Fáze B: UX Foundation** — po uzavření Fáze A; cíl: aplikace použitelná jako skutečný report designer bez každodenní friction.

---

## 📋 Co je potřeba dělat

### 🔴 Fáze A: Odladění MHD zastávkového JŘ s reálnými daty ⬅️ AKTUÁLNÍ PRIORITA

Cíl: end-to-end proof of concept — šablona v designeru → tisk s reálnými daty z Caché → PDF 1:1 odpovídající současnému MHD JŘ. Zpětná vazba od kolegů.

> **Detailní architektonický dokument:** [`schedule-elements-architecture.md`](schedule-elements-architecture.md)

| Krok | Co | Stav |
|---|---|---|
| 8 | Ladění zastávkového JŘ na reálných datech — edge cases (prázdné směry, speciální znaky, velké linky) | ⬅️ |
| 9 | Vizuální shoda s referenčním PDF — porovnání pixel-by-pixel, doladění mezer/fontů/zarovnání | |
| 10 | Fonty pro speciální znaky (①②③ anotace) — font fallback mechanismus v rendereru | |
| 11 | Proof of concept review — zpětná vazba od kolegů, seznam úprav | |

#### Poučení z implementace zastávkového JŘ — co vzít v potaz pro další JŘ typy

| Téma | Popis |
|---|---|
| **Jeden element → tři elementy** | Původní plán počítal s jedním `StopScheduleElement`. Ve skutečnosti se rozpadl na 3 nezávislé elementy — lépe odpovídá filozofii designeru (uživatel skládá ze stavebních bloků). Počítat s tím od začátku u dalších JŘ typů. |
| **Synchronní GetData** | `GetData` běží synchronně na UI thread — pro malé linky OK, pro velké může zamrznout UI. Vozový JŘ bude mít víc dat → async loading s progress barem bude nutný. |
| **SKTypeface/SKPaint alokace** | Schedule elementy alokují SKTypeface a SKPaint per render frame. Funguje, ale není optimální. Cachovat fonty a painty. |
| **Caché business logika je rozsáhlá** | ZastavkoveJrMhdAPI má ~1200 řádků. Počítat s tím v odhadu pro další JŘ typy. |
| **Definovat layout před implementací** | Aby se neopakovala situace, kde se plán změní v průběhu. |

---

### 🟠 Fáze B0: UX Sprint — implementační pořadí ⬅️ NÁSLEDUJÍCÍ PRIORITA

Konkrétní pořadí kroků pro Fázi B + B2 seřazené podle závislostí a viditelného dopadu. Každý krok projde pracovním postupem výše (validace → implementace → test → docs).

---

#### Blok 1 — Rychlé výhry + precision fix

Cíl: zastavit nové float precision leaky, dodat 3 rychlé features.

| # | Co | Proč teď | Velikost |
|---|---|---|---|
| B0-1 | ~~**Strikethrough** na `TextStyle` + descriptor; oprava tichého nefungování Underline~~ | ✅ | ✅ |
| B0-2 | ~~**Více barcode typů** — EAN-13, EAN-8, PDF417, DataMatrix; konsolidace palety (1 tlačítko Kód)~~ | ✅ | ✅ |
| B0-3 | **PNG export** — `PreviewCanvas.RenderToPng()`, File menu | Odloženo (nízká priorita) | S |
| B0-4 | ~~**Precision fix** — `MmMath.Round(0.1mm)`; resize end, ScaleTemplate, DistributeH/V, properties panel~~ | ✅ | ✅ |
| B0-5 | ~~**DistributeH/V: přesný gap** — zaokrouhlit, poslední element absorbuje zbytek~~ | ✅ | ✅ |
| B0-8 | ~~**Properties panel float formát** — `"0.0#"` pro mm pole, round on commit~~ | ✅ | ✅ |

---

#### Blok 2 — Normalizace + Layout assistant

Cíl: uživatel dokáže vzít "rozhozené" elementy a dostat je do čisté mřížky jedním dialogem.

**Kontext precision problému:**
Sub-mm floaty se dostávají do modelu 10 různými cestami (drag delta, smart-snap na float cíl, scale, distribute, properties panel). Snap pak nefunguje protože edge elementu A je na `X+39.49mm` a element B je na `40.0mm` — rozdíl 0.51mm může být mimo snap threshold závislý na zoomu.

| # | Co | Proč teď | Velikost |
|---|---|---|---|
| B0-6 | **"Normalizovat pozice"** (`Ctrl+Shift+N`) — zaokrouhlit X/Y/W/H vybraných elementů na zvolený krok (0.5mm default); dialog s náhledem; `CompositeCommand` pro undo | Opraví existující křivé šablony | M |
| B0-7 | **`LayoutGridDialog`** — rozmístit vybrané elementy do řádku/sloupce/mřížky se zadanou mezerou (mm); zakotvit od prvního elementu nebo od X=0; volba vyrovnat šířky/výšky; výstup = `CompositeCommand` | Standard UX pro report designer | M |
| B0-8 | **Properties panel float zobrazení** — formát `"0.0#"` mm, round na commit (0.5mm), bez trailing zeros | Zabraňuje novým leakům přes UI | S |
| B0-9 | ~~**Grid size v UI** — ComboBox 1/2/5/10mm v toolbaru, `DesignerState.GridSize` property~~ | ✅ | ✅ |

---

#### Blok 3 — Properties panel UX

Cíl: properties panel přehledný, styling přenosný.

| # | Co | Proč teď | Velikost |
|---|---|---|---|
| B0-10 | **Collapsible groups** v `PropertiesPanel` — accordion s `GroupDescriptor`, sbalené skupiny zobrazují summary řádek, stav sbalení per element type do `DesignerState` | Přehlednost u elementů s 20+ properties | M |
| B0-11 | **Inline validace** — červený rámeček + tooltip místo textu pod polem | Méně vizuálního šumu | S |
| B0-12 | **Copy Style / Paste Style** (`Ctrl+Shift+C/V`) — `StyleClipboard` v `DesignerState`, kopíruje `TextStyle` + `BorderColor/Width` + `BackgroundColor`; `PasteStyleCommand` přes `CompositeCommand` | Konzistence stylů bez editace každého elementu | M |

---

#### Blok 4 — Format toolbar

Cíl: nejčastější text operace = 1 kliknutí.

| # | Co | Proč teď | Velikost |
|---|---|---|---|
| B0-13 | **Kontextový format mini-toolbar** — zobrazí se nad properties panelem při výběru TextElement nebo TableElement; tlačítka: B/I/U/S̶ \| Align L/C/R/J \| VAlign ▲/▪/▼; napojené na existující `PropertyDescriptor` callbacks, žádná nová logika | Viditelný UX win, nízká implementační cena | M |

---

#### Blok 5 — Rotace

Cíl: vodoznaky, svislé popisky, záhlaví sloupců 90°.

| # | Co | Proč teď | Velikost |
|---|---|---|---|
| B0-14 | `Rotation` property na `TemplateElement` (float, stupně) + JSON serializace + `ApplyScale` rotaci neškáluje | Model základ | S |
| B0-15 | Render: `canvas.Save/RotateDegrees(cx,cy)/Restore` obalí `element.Render()` | Jádro featurky | S |
| B0-16 | Hit-testing: AABB z rotovaných rohů pro selection a handles | Potřebné pro použitelnost | M |
| B0-17 | Resize handles zakázat pro rotované elementy (jen Move + Enter) + visual indicator rotace | UX doplněk | S |

---

#### Blok 6 — Template versioning

Prerekvizita pro Caché persistence — bez migrace se nové šablony nespustí na starším buildu.

| # | Co | Proč teď | Velikost |
|---|---|---|---|
| B0-18 | `TemplateMigrator.Migrate(JsonDocument)` — detekce verze, pipeline migrací, spouští se před deserializací | Ochrana existujících šablon | S |
| B0-19 | Migrace V1→V2: `TextStyle` flat→nested (breaking change 2026-03-24) + varování v UI | Konkrétní existující breaking change | S |

---

> **Po dokončení Bloku 1–6:** Přejít na Fázi B (Caché persistence, WPF color picker) — viz sekce níže.

---

### 🟠 Fáze B: UX Foundation — základní použitelnost

Po schválení MHD proof of conceptu. Cíl: odbourat každodenní friction a dostat designer na úroveň skutečného report nástroje. Vychází z hloubkové UX analýzy (2026-04-20).

#### 🔴 B1: Caché persistence uživatelských preferencí — primární blocker

Aktuálně se mezi sezeními neukládá **vůbec nic** kromě samotného JSON souboru šablony. Každé ráno: otevření souboru přes dialog, ruční nastavení zoomu, gridu, rozbalení skupin polí.

**Nová Caché třída `ALVA.TemplateDesigner.API.UserPreferencesAPI`:**
```
GetPreferences(ByRef sys) → OUT[1] = JSON string s preferencemi uživatele
SavePreferences(ByRef sys) → IN["json"] = JSON string; uloží do ^ALVA.TDPrefs(IdFa, UserId)
```

Klíčovány per `IdFa` × `Mlp` (userId z MLRest) — každý uživatel každé firmy má vlastní nastavení.

**.NET `UserPreferencesService`:**
```csharp
class UserPreferences {
    float Zoom;                            // 1.0 = 100 %
    bool ShowGrid, SnapToGrid, SnapToElements;
    double MainSplitterPosition;           // levý panel / canvas
    double PreviewSplitterPosition;        // canvas / preview
    List<string> RecentFiles;             // max 10 cest
    List<string> RecentColors;            // max 16, #AARRGGBB — pro color picker
    Dictionary<string, bool> GroupExpanded; // stav rozbalení skupin v poli stromu
    string? LastActiveTab;                 // "Sekce" / "Pole" / "Vlastnosti" / "Strom"
}
```

- Načítá při startu (async, fallback na default hodnoty pokud Caché nedostupná)
- Ukládá při zavření okna (fire-and-forget, neblokuje)
- `appsettings.json` zůstává jen pro **síťovou konfiguraci** (Caché host, port, agendas)

| Krok | Co | Stav |
|---|---|---|
| 12 | Caché `UserPreferencesAPI` — `GetPreferences` + `SavePreferences`, storage v `^ALVA.TDPrefs` | |
| 13 | .NET `UserPreferencesService` — Load/Save, fallback, napojení na MainWindow lifecycle | |
| 14 | Napojit na stav: Zoom, Grid/Snap toggles, SplitterPositions, LastActiveTab | |
| 15 | Recent Files — File menu + keyboard Ctrl+R (filterable dialog), uložení do Caché prefs | |
| 16 | Template thumbnails — při uložení šablony generovat PNG náhled (100×140px) vedle `.json` | |

---

#### 🔴 B2: WPF Color Picker — fix blokující bug

`WinForms ColorDialog` minimalizuje Properties panel při kliknutí OK (known TODO v `PropertiesPanel.xaml.cs:555`). Uživatel musí panel znovu otevřít po každém výběru barvy.

**Řešení: vlastní WPF color picker popup:**
- 16 swatches z `UserPreferences.RecentColors` (aktualizuje se po výběru)
- Hex input field (#AARRGGBB) + alpha slider
- Tlačítko "Více barev..." → rozšířená paleta nebo HSV picker
- Popup nad fields kde je aktuálně ColorDialog — `PlacementTarget` = color button

| Krok | Co | Stav |
|---|---|---|
| 17 | WPF color picker control (`ColorPickerPopup.xaml`) — swatches, hex, alpha | |
| 18 | Nahradit všechna volání `ColorPickerHelper.PickColor()` novým controllem | |
| 19 | Synchronizovat `RecentColors` s `UserPreferencesService` | |

---

#### 🟠 B3: Properties panel — přehlednost

Aktuálně lineární seznam 15–30 properties bez hierarchie. Každá změna fontu = scroll přes pozici/barvy/zarovnání.

**Collapsible groups (accordion):**
```
▼ Pozice         [X: 10] [Y: 5] [W: 80] [H: 20]
▶ Font           Arial 10pt Tučné                  ← sbaleno, zobrazuje summary
▼ Barvy          Text: ■ #000  Pozadí: □
▶ Ohraničení     0.1mm Plné                        ← sbaleno
▶ Pokročilé      VisibilityExpr, Lock, ZIndex       ← výchozí sbaleno
```

- Stav sbalení skupin uložen do `UserPreferences.GroupExpanded` (per element type)
- Sbalená skupina zobrazí 1-řádkový summary (hodnoty klíčových properties)

**Format mini-toolbar nad properties panelem (pro TextElement + Table):**
```
[B] [I] [U] [S̶] | [≡] [⟺] [≡] [⟷] | [▲─] [▪] [─▼]
```
- Zobrazí se kontextově jen pro elementy s textem
- Snižuje počet kliknutí pro nejčastější operace z ~6 na 2

| Krok | Co | Stav |
|---|---|---|
| 20 | Accordion wrapper v `PropertiesPanel` — `GroupDescriptor`, collapsible sekce, stav do prefs | |
| 21 | Format mini-toolbar — kontextový, napojený na existující `PropertyDescriptor` callbacks | |
| 22 | Inline validace — červený rámeček + tooltip místo textu pod polem | |

---

#### 🟠 B4: Copy Style / Paste Style

Uživatel nadesignuje TextElement (font, barvy, padding) — chce aplikovat styl na dalších 5 elementů. Aktuálně edituje každý zvlášť.

- `Ctrl+Shift+C` = Copy Style (zkopíruje `TextStyle` / `BorderColor,BorderWidth` / `BackgroundColor`)
- `Ctrl+Shift+V` = Paste Style (aplikuje styl na všechny vybrané elementy, zachová pozici/obsah)
- Undo/redo přes `CompositeCommand` + `SetPropertyCommand`
- Styl clipboardu = samostatná proměnná vedle elementového clipboardu

| Krok | Co | Stav |
|---|---|---|
| 23 | `StyleClipboard` v `DesignerState` + `CopyStyle`/`PasteStyleCommand` | |
| 24 | Keyboard shortcuts Ctrl+Shift+C/V + menu položky | |

---

#### 🟠 B5: Rotace elementů

Bez rotace nelze dělat: svislé popisky sloupců, vodoznaky ("NÁVRH", "VÝLUKA"), záhlaví otočená 90° pro úzké sloupce.

- `float Rotation` property na `TemplateElement` (stupně; 0/90/180/270 nebo libovolné)
- `canvas.RotateDegrees(rotation, centerX, centerY)` obalí `Render()` call
- Hit-testing přepočítá bounds pro rotovaný element (AABB z rotovaných rohů)
- `ApplyScale()` rotaci nepřekládá (bezrozměrná)
- Vodoznak = RectangleElement (alpha) + TextElement (45°, 30% alpha) seskupené přes GroupId

| Krok | Co | Stav |
|---|---|---|
| 25 | `Rotation` property na base + JSON serializace | |
| 26 | Render: `canvas.Save/RotateDegrees/Restore` kolem `element.Render()` | |
| 27 | Hit-testing: rotovaný AABB pro handle a selection overlay | |
| 28 | Resize handles zakázány pro rotované elementy (jen move + Enter pro properties) | |

---

#### 🟡 B6: Template versioning

Aktuálně žádná migrace při změně JSON formátu. Existující šablony přestávají fungovat při každém breaking change.

- `"version": "2"` field v JSON (aktuálně `"1.0"` ale nepoužíváno)
- `TemplateMigrator.Migrate(JsonDocument)` — spouští se před deserializací
- Konkrétní migrace: V1→V2 = přejmenování properties, přidání defaults pro nová pole
- Varování v UI při načtení starší verze

| Krok | Co | Stav |
|---|---|---|
| 29 | `TemplateMigrator` class — detekce verze + pipeline migrací | |
| 30 | Migrace V1→V2: `TextStyle` flat → nested (breaking change z 2026-03-24) | |

---

#### 🟡 B7: Ruler na canvasu

Pro přesné layouty fakticky nezbytné. Bez pravítka uživatel odhaduje polohu vizuálně.

- Horizontální + vertikální pravítko v mm podél hran canvasu
- Zvýrazňuje rozsah vybraného elementu (šrafovaný pruh na pravítku)
- Respektuje zoom level

| Krok | Co | Stav |
|---|---|---|
| 31 | Ruler rendering v `DesignerCanvas` — mm ticks, labels, zvýraznění selection | |

---

#### 🟡 B8: Vylepšení importu PDF

| Krok | Co | Stav |
|---|---|---|
| 32 | Import: detekce tabulek z mřížky čar (3a) — místo 50 čar + 20 textů → jeden `TableElement` | |
| 33 | Import: JŘ-aware mód (3d) — dvoustupňový AI+engine import | ⬅️ rozpracováno (2026-04-03) — AI Vision pipeline + AiGuidedScheduleBuilder fungují, ladění na širší sadě JŘ |

**Import PDF — aktuální stav:**
- ✅ Režim 1 (deterministický 1:1 import) hotovo (2026-03-13)
- 3a: Detekce tabulek z mřížky čar (viz popis níže)
- 3b: Křivky/Bézier (nízká priorita — zaoblené rohy rasterizovat jako bitmapa)
- 3c: AI post-processing — nahrazení statických textů za `{PLACEHOLDER}`, detekce záhlaví/patičky
- **3d: JŘ-aware import — dvoustupňový AI + engine přístup:**
  - **Stupeň 1 — AI (sémantika):** Claude dostane screenshot PDF + DataSchema agendy. Vrátí sémantickou mapu oblastí: "levý sloupec = StopList, střed = DepartureGrid se 3 skupinami dnů, spodek = NotesLegend". AI řeší **co** je kde, neřeší přesné rozměry.
  - **Stupeň 2 — Engine (geometrie):** Deterministický kód vezme extrahované čáry/texty z PDF + sémantickou mapu od AI. Z průsečíků čar spočítá přesné pozice (X,Y,W,H), z rozestupů textů odvodí HourColumnWidth/MinuteWidth, z textů v záhlaví identifikuje skupiny dnů, z font size odvodí DataStyle/HeaderStyle. Výstup = přesný Template JSON.
  - **Princip:** AI = rozpoznání struktury (co), engine = přesné rozměry (kde). AI nemusí hádat pixely, engine nemusí rozumět sémantice.
  - **Prerekvizity:** Fungující elementy (krok 8/9), zdokumentované JSON schema elementů, Claude API přístup.
  - **Mezikrok (jednodušší alternativa):** Import PDF jako background obrázek (merge mode All) → uživatel ručně skládá elementy přes referenci → smaže background. Funguje okamžitě, bez AI.

---

### 🟠 Fáze B2: Element flexibility — rozšíření možností elementů

Vychází z gap analýzy oproti komerčním report generátorům (Crystal Reports, Stimulsoft, DevExpress). Nezávislé na JŘ — platí pro fakturační i obecné šablony.

#### Strikethrough + TextStyle doladění

| Krok | Co | Stav |
|---|---|---|
| 34 | `Strikethrough` property na `TextStyle` — `SKPaint.StrikeThruText = true`; přidat do descriptors | |

#### Merged cells v TableElement

Pro faktury standardní. Záhlaví přes 2 sloupce ("Dodavatel / Odběratel"), sloučené řádky titulu.

| Krok | Co | Stav |
|---|---|---|
| 35 | `ColSpan` + `RowSpan` na `TableCell` — JSON serializace, rendering skip pro sloučené | |
| 36 | Merge editor v `TableEditorDialog` — UI pro nastavení span | |

#### Conditional cell formatting v tabulce

Analogie `ColorRules` z `DepartureGrid` — generalizovat na `TableColumn`.

| Krok | Co | Stav |
|---|---|---|
| 37 | `ColorRules` na `TableColumn` — `{ expression, foreColor, backColor }` array | |
| 38 | Vyhodnocení v print engine při renderování buňky | |

#### Více formátů barkódů

ZXing.Net podporuje vše, jen chybí UI enum.

| Krok | Co | Stav |
|---|---|---|
| 39 | Přidat do `BarcodeType`: EAN-13, EAN-8, PDF417, DataMatrix, Aztec | |

#### PNG export

SkiaSharp `SKBitmap.Encode()` — triviální přidání.

| Krok | Co | Stav |
|---|---|---|
| 40 | PNG export (per-page nebo všechny stránky) — File menu + `PreviewCanvas.RenderToPng()` | |

#### Dynamická viditelnost sekce v designeru

VisibilityExpression se vyhodnocuje jen při tisku. Uživatel nevidí jak bude šablona vypadat.

| Krok | Co | Stav |
|---|---|---|
| 41 | Toggle "Simulovat viditelnost" v sekci — vyhodnotí expression s mock daty, zšedí/skryje sekci v designeru | |

---

### 🟠 Fáze C: Meziměstský JŘ

Konceptuálně podobné MHD — zastávky + odjezdy spojů + poznámky. Rozdíl: sloupce odjezdů nejsou typy provozu (jako MHD), ale mají jiný koncept specifický pro meziměsto. Layout je pivot tabulka (zastávky × spoje). Pravděpodobně bude potřeba samostatný element (ne rozšíření DepartureGridElement).

| Krok | Co | Stav |
|---|---|---|
| 42 | Definovat meziměstský layout — analýza referenčních PDF, wire formát dat | |
| 43 | Implementace meziměstského JŘ elementu | |
| 44 | Caché `TiskMeziměsto` — GetData/GetMockData pro meziměstský JŘ | |

---

### 🟠 Fáze D: Knižní JŘ

Layout TBD — pravděpodobně kompaktní formát pro knižní publikaci. Definovat layout před implementací.

| Krok | Co | Stav |
|---|---|---|
| 45 | Analýza referenčních knižních JŘ — definice layoutu a wire formátu | |
| 46 | `BookScheduleElement` — implementace (pravděpodobně víc elementů, viz poučení z MHD) | |
| 47 | Caché `TiskKnizni` — GetData/GetMockData | |

---

### 🟠 Fáze E: Vozový JŘ

Layout TBD — hlavička oběhu, tabulka spojů, poznámky. Pravděpodobně víc elementů (viz poučení z MHD).

| Krok | Co | Stav |
|---|---|---|
| 48 | Analýza referenčních vozových JŘ — definice layoutu a wire formátu | |
| 49 | `VehicleScheduleElement` — implementace (pravděpodobně víc nezávislých elementů) | |
| 50 | Caché `TiskVozovy` — GetData/GetMockData + async loading (větší objem dat) | |

---

### 🟣 Plánované architektonické změny (nutné, ne volitelné)

**Caché-driven formuláře a komponenty (větší refaktor — neobejdeme se bez něj)**

Současný `PropertiesPanel` + `PropertyDescriptor` systém je dočasné řešení. Do budoucna bude veškerá business logika (definice vlastností elementů, formuláře, tabulky) řízena z Caché přes standardní EDControls komponenty. **Není to urgentní teď**, ale protože jde o větší refaktor, eviduje se jako vyšší priorita než běžné feature items — až přijde čas, musí být jasné že se bude řešit.

**Cílová architektura:**
- `ElementPropertiesWindow` — místo vlastního `PropertiesPanel` použít `EDInputForm` (z EDControls)
- Formulář pro každý element bude definovaný v Caché třídě `ALVA.TemplateDesigner.Elements.<Typ>.Init/Validate/Save`
- Editor sloupců `TableElement` — použít `EDDataGrid` (z EDControls, až bude hotový)
- Presety elementů a tabulek (např. JR tabulka se sloupci) — načítat z Caché místo hardkódování v C#

**Co se NEZMĚNÍ:**
- C# třídy elementů (renderování, Clone, model, JSON serializace)
- Drag & drop, resize, canvas logika

**Překladová vrstva .NET ↔ Caché (nutná pro undo/redo):**
- `Init`: .NET serializuje aktuální hodnoty elementu → Caché postaví formulář s hodnotami
- `Save`: Caché vrátí změněné hodnoty → .NET updatuje element přes `SetPropertyCommand` (zachová undo/redo)

**Kdy řešit:** Až bude `EDDataGrid` hotový a/nebo bude potřeba první Caché-driven formulář.
Do té doby `PropertyDescriptor` systém funguje a neblokuje — ale počítat s refaktorem při plánování dalších vlastností elementů (aby se nepřekopávalo dvakrát).

**Reference:** Viz `EDControls/EDInputForm/` — existující implementace + Caché třídy `ALVA.EDControls.EDInputForm.*`

---

### 🟡 Důležité — omezení flexibility bez toho

**3b. Obecný layout-reservation pattern u elementů**
Aktuálně když element potřebuje rezervovat místo pro volitelný vizuální prvek (šipka směru u `StopListElement`, případně jiné badge/decorace), řeší se to ad-hoc — vypočítá se vlastní gutter a posune se obsah. Pro každou takovou vlastnost to znamená hardkódovat výjimku v `Render` metodě elementu. Do budoucna zvážit obecný mechanismus: např. deklarativní mapu "pokud property X=true → rezervuj Y mm vlevo/vpravo/nahoře/dole", nebo vrstvu „před-layout" kde element hlásí své padding požadavky a base třída spočítá contentRect. Vyřeší to konzistentně napříč všemi elementy a zjednoduší přidávání podobných featur.

**4. Min/max počet řádků dynamic tabulky**
Nové properties na `TableElement`: `MinRows`, `MaxRows`. Print engine ořízne přebývající řádky nebo doplní prázdné řádky do minima. Užitečné pro tabulky s proměnlivým počtem dat (faktury, JŘ).

**5. Hranice mezi buňkami tabulky (cell borders)**
`TableElement` property pro vnitřní cell borders (horizontální/vertikální čáry mezi buňkami, šířka, barva). Aktuálně má tabulka jen vnější rámeček — vnitřní oddělení buněk chybí.

**6. Zarovnávání napříč sekcemi**
Aktuálně alignment funguje jen v rámci jedné sekce. Umožnit zarovnat elementy z různých sekcí — referenční bod = stránka, ne sekce. Vyžaduje přepočet souřadnic (element.X/Y jsou relativní k sekci).

**7. Vnořené datasource (hierarchická data)**
Flat `_values + _rows` nestačí pro hierarchická data.
- `_rows` uvnitř `_rows` — kolekce uvnitř kolekce (faktura → položky → daně)
- TableElement může mít vnořený DataSource odkazující na child kolekci
- DataContext API rozšířit o `GetChildRows(collectionKey, parentIndex)`

---

### 🟢 Menší priorita

**UX polish:**
| # | Položka | Náročnost | Popis |
|---|---------|-----------|-------|
| g | Inline text editing na canvasu | Velká | Dvojklik na text → inline editace přímo na canvasu (bez otevření dialogu) |
| h | Cross-section rubber band selection | Střední | Aktuálně funguje jen v aktivní sekci; umožnit výběr přes sekce s přepočtem souřadnic |
| i | Align to page center | Malá | Zarovnat element na střed stránky (horizontálně nebo vertikálně); aktuálně jen k prvnímu/poslednímu prvku |
| j | Grid size v UI | Malá | Aktuálně hardcoded 5mm v `DesignerState.cs:26`; přidat do nastavení designeru |
| k | Page Break element | Malá | Explicitní element "zalomit stránku zde" — alternativa k oddělení přes sekce |
| l | XLSX export | Velká | Tabulky → Excel; vyžaduje ClosedXML nebo NPOI |

**8. Grouping v TableElement**
Seskupení řádků dynamické tabulky podle hodnoty pole.
- `TableRowType` enum: `GroupHeader`, `Data`, `GroupFooter`, `Summary`
- `GroupByField` property na TableElement
- Print engine sleduje změnu hodnoty groupovacího pole a vkládá Header/Footer řádky
- GroupFooter může obsahovat agregace ze skupiny (SUM/COUNT)

**9. Computed fields**
Pole vypočítaná na C# straně ze surových dat před předáním šabloně.
- Definice v agendě (nebo přímo v šabloně)
- Např. `CELKOVA_CENA = CENA * MNOZSTVI` — hotovo před renderováním
- Alternativa k výrazovému jazyku pro komplexnější logiku

---

### 🔵 Před produkcí (odloženo — řešit až půjde do produkce)

**7. Migrace verzí šablon**
Základní versioning je naplánovaný v B6 (krok 29–30). Rozšíření pro produkci:
- Full migration pipeline pro každou breaking change (ne jen 2 verze)
- Verzování šablon v Caché (kdo kdy co změnil)
- CI/CD linting — ověřit šablony před deployem

**8. Metadata a správa šablon**
Důležité jakmile šablon bude víc než 10. Zatím jich je málo — řešit až při nasazení.
- Autor, datum změny, kategorie, popis
- Thumbnail preview (PNG generovaný při uložení)
- Verzování šablon (kdo kdy co změnil)

---

## 🤖 AI integrace — vize a plán

Architektonické rozhodnutí (2026-03-17): **žádné zásadní rozhodnutí teď není potřeba.** JSON šablona + DataSchema jsou dostatečné rozhraní pro AI. Rozšíření se dá přidat kdykoliv později bez breaking changes.

**Princip:** AI = copilot, ne autopilot. Vždy navrhne → uživatel potvrdí/upraví.

### Aktuální stav
- ✅ AI Assistant — generování celé šablony z textového popisu (Claude API, prompt-based)
- ✅ Schema injection — AI dostane seznam polí/kolekcí/parametrů agendy

### Fáze 1 — prompt-based (stačí teď)

Pro většinu use cases stačí dát AI kontext přes systémový prompt (JSON šablona + schema):

| Use case | Přístup |
|---|---|
| Generování šablony z popisu | ✅ hotovo |
| Vysvětlení šablony ("co dělá tato sekce?") | prompt + aktuální JSON |
| Nápověda k výrazům ("jak skrýt řádek?") | prompt + docs |
| Template linting / validace | prompt + JSON + schema |
| PDF import režim 2 ("zchytři to") | prompt + importovaný JSON + schema |

### Fáze 2 — MCP server (až budou reální uživatelé s potřebou AI asistence)

Pro operace měnící stav aplikace (úpravy šablony hlasem/textem, smart suggestions) by TemplateDesigner vystavil MCP server:

```
MCP Tools:
├── get_template()                  → aktuální JSON šablona
├── get_schema(agendaCode)          → DataSchema dané agendy
├── get_available_agendas()         → seznam agend
├── get_mock_data(agendaCode)       → vzorová data
├── apply_template(json)            → nahradí šablonu (s undo)
├── add_element(sectionId, element) → přidá element (s undo)
├── modify_element(elementId, props)→ změní properties (s undo)
├── add_section(preset, index)      → přidá sekci
├── validate_template()             → vrátí seznam problémů
└── get_help(topic)                 → dokumentace k funkci
```

MCP je tenká vrstva nad ViewModelem — každý tool volá existující metody (`_vm.Execute(command)`).
Standardní protokol (stdio, lokální) — funguje s Claude i jinými AI, žádný vlastní server.

### Fáze 3 — pokročilé use cases (dlouhodobě)

| Use case | Kategorie | Popis |
|---|---|---|
| Onboarding průvodce | Noví uživatelé | AI popíše co nástroj umí, navede uživatele krok za krokem |
| "Přidej patičku s číslem stránky" | Zkušení uživatelé | AI modifikuje existující šablonu (ne generuje od nuly) |
| Smart suggestions | Zkušení uživatelé | Přidáš TableElement → AI navrhne sloupce z DataSchema |
| Migrace starých Caché sestav | Automatizace | AI analyzuje starý kód → navrhne TemplateDesigner šablonu |
| Template linting v CI/CD | Automatizace | AI zkontroluje šablony před deployem |
| Batch generování šablon | Automatizace | Nová agenda → AI vygeneruje základní šablony |

### Proč to neblokuje

JSON šablona je stabilní kontrakt mezi designerem a AI. Dokud AI umí číst a psát JSON v definovaném formátu, integrace funguje bez ohledu na to, jestli je to prompt, MCP, nebo REST API. Rozšíření se dá přidat kdykoliv bez refactoringu.

---

## ✅ Záměrně jinak — neřešit

- **Caché jako source of truth pro tabulkové šablony** — dává smysl pro doménovou logiku
- **Univerzální Section třída** — jednodušší než pevná hierarchie (Crystal Reports)
- **Absolutní pozicování jako default** — správné pro tiskové sestavy s pevným layoutem

---

# 🗄️ Archiv — hotové kroky

> Vše pod touto čarou je už implementováno. Horní část roadmap obsahuje jen to, co je potřeba dělat.

## Specializované JŘ elementy — společný základ ✅ (2026-03-18)

| Krok | Co |
|---|---|
| 1 | `ScheduleElementBase` — abstraktní základ (indexy, grouping, společné properties) |
| 2 | `StopListElement` + `DepartureGridElement` + `NotesLegendElement` — zastávkový JŘ (MHD) |
| 3 | Caché `ZastavkoveJrMhdAPI` — `GetData`/`GetMockData` + wire formát (ZASTAVKY, SKUPINY_DNU, ODJEZDY, POZNAMKY) |
| 4 | `SkiaHeightCalculator` — měření výšky JŘ elementů |
| 5 | `PrintDialog` — parametry + export reálných dat z Caché (`GetData`) + multi-data PDF |
| 6 | `CacheAgendaProvider` — ParseMockData vnořená pole, GetData, BuildRealData |

## Fáze A: Odladění MHD zastávkového JŘ — hotové kroky

| Krok | Co | Stav |
|---|---|---|
| 7 | Per-column styling framework — CellStyle/ColumnVisualStyle, dva taby v DepartureGroupEditorDialog, InlineTextStyle pro minuty/anotace | ✅ (2026-03-21) |
| 7a | StopListElement — per-column styling analogicky k DepartureGrid (CellStyle pro záhlaví/data sloupců zastávek) + generalizace shared helpers do ScheduleElementBase + ColorPickerHelper + ActionId routing | ✅ (2026-03-23) |
| 7b | Ověření parametrů šablony — funguje přepsání element properties z template parameters (CASOVA_OSA, HODINY_OD, HODINY_DO, TISK_PROJETYCH_ZAST, TISK_SIPKY); fix merge parametrů do real data exportu; auto-open PDF; odebrání BAREVNE_ODLISENI + TISK_TARIFU | ✅ (2026-03-23) |
| 8a | Sjednocení textových nastavení — `TextStyle` (nullable POCO, CSS cascade) + `ResolvedTextStyle` (SkiaSharp helpery). Migrace: TextElement, TableElement, ScheduleElementBase + 3 JŘ elementy, PdfImport, CacheAgendaProvider. Breaking JSON change (flat props → `style`/`headerStyle`/`dataStyle`) | ✅ (2026-03-24) |
| 8b | Proporcionální škálování šablony + UX vylepšení — `ScaleTemplateCommand`, `ApplyScale()` na všech elementech, redistribuce výšek sekcí, výchozí nastavení nových elementů, `HourColumnPerGroup` | ✅ (2026-03-24) |
| 8c | AI-enhanced PDF import — dvoustupňový pipeline (heuristiky → AI enrichment); `AiImportEnricher` s kontextovými prompty; detekce rámečků, loga placeholder, margin fix, TextElement alignment fix | ✅ (2026-03-25) |
| 8d | AI Vision import pipeline — `AiVisionImportService` (composable prompty), `AiGuidedScheduleBuilder` (PdfPig geometrie), `AiVisionImportPipeline` (orchestrace); per-column font wrapping; section headers auto-hide; CellStyleVM font defaults; shared HttpClient | ✅ (2026-04-03) |

## Další implementované oblasti

**UX audit — bezpečnost + frustrace + polish** ✅ (2026-03-18)
- Sanitizace VisibilityExpression (whitelist tokenizer + live validace v UI)
- Canvas fixed height + section clamping (sekce nepřesáhnou stránku)
- SectionRole (Header/Body/Footer) — patičky od spodu stránky
- Ctrl+X, Ctrl+0, context menu na canvasu, bound resize, potvrzení mazání sekce
- Nahrávání obrázků s auto-downscale (max 5 MB), SelectAll

**Zastávkový JŘ + Caché integrace** ✅ (2026-03-18)
- ScheduleElementBase + 3 elementy (StopListElement, DepartureGridElement, NotesLegendElement)
- ZastavkoveJrMhdAPI (~1200 řádků business logiky z legacy refaktoru, sloučeno z AgendaJRAPI + TiskMHD + TemplateDesignerMHD)
- CacheAgendaProvider: ParseMockData vnořená pole, GetData, BuildRealData
- PrintDialog: parametry + export reálných dat + multi-data PDF
- SkiaHeightCalculator: schedule element height calculation

**PageLayoutEngine + multi-page** ✅ (2026-03-17)
- Template + DataContext → `List<Page>` s PlacedSections; čistá logika (vrstva 3), žádný SkiaSharp/WPF
- Klasifikace sekcí (headers/footers/body), stránkování (KeepTogether/SplitAcrossPages), dvouprůchodový layout pro OnLastPageOnly
- Flow layout uvnitř sekcí, PerDataRow expanze, PAGE/TOTAL_PAGES injekce
- Multi-page preview (stránky pod sebou) + multi-page PDF export
- IElementHeightCalculator: SkiaHeightCalculator měří TextElement (word wrap) a TableElement

**Flow text v designeru (Word wrap + dynamická výška)** ✅ (2026-03-17)
- TextElement: `WordWrap=true` + `Height=null` → text se zalamuje, výška se dopočítá z obsahu
- `LineSpacing` property, vertikální zarovnání víceřádkového textu, fix `\r\n`
- InfiniteTextElement jako samostatný typ zamítnut — jedno chování řízené properties

**ElementGroup — seskupování elementů** ✅ (2026-03-12)
- `GroupId` property na `TemplateElement` — lehká logická vazba bez nested souřadnic
- Kliknutí na člen skupiny vybere celou skupinu; Alt+klik = jen ten jeden element
- Ctrl+G / Ctrl+Shift+G — seskupit / rozeskupit (plný undo/redo)
- Vizuální indikace: barevný badge v rohu, bounding box skupiny při výběru
- Strom šablony zobrazuje skupiny jako sbalitelné uzly
- Copy/Paste/Duplicate přemapují GroupId (klony nedědí skupinu originálu)
- PDF import automaticky seskupí grafiku podle typu a merge mode

**Locale / CultureInfo v šabloně** ✅ (2026-03-12)
- `Template.CultureCode` — kultura pro formátování čísel a datumů (cs-CZ, sk-SK, en-US, de-DE)
- Protéká celým pipeline: Template → DataContext.Culture → PlaceholderFormatter → TableElement
- Dropdown ve vlastnostech stránky; null = cs-CZ (výchozí); parsování vstupů zůstává InvariantCulture

**Drag & drop při přidávání elementů a sekcí** ✅ (2026-03-12)
- Drag element z toolbaru/palety na canvas — ghost preview, drop target highlight, clamp + snap
- Drag pole z panelu "Pole agendy" na canvas — vytvoří TextElement nebo append k existujícímu
- Drag & drop sekcí v levém panelu — přeřazení tažením, vizuální indikátor, `ReorderSectionCommand`
- Drag element mezi sekcemi — záměrně odloženo (nízká priorita, aktuální UX postačuje)

**Chytré duplikování elementů** ✅ (2026-03-12)
- Ctrl+D → šipka = směrové duplikování (kopie vedle originálu s 4mm mezerou)
- Ctrl+D bez šipky = plain duplikát (+5mm offset); neovlivňuje clipboard
- Multi-select, clamp na bounds sekce, plný undo/redo

**Přichytávání elementů k sobě (smart snap)** ✅ (2026-03-12)
- Edge↔Edge (4 kombinace), Center↔Center, Gap snap (4mm mezera) — bez kategorické priority, vyhraje nejbližší match
- Zvýraznění cílových elementů (šedý dashed rámeček), gap indikátor (černá čárka v mezeře)
- Element snap má přednost před grid snapem; osy X/Y nezávisle
- Toggle v toolbaru + menu; výchozí zapnuto

**Výrazový jazyk** ✅ (2026-03-12)
- Part 1 — podmínky a logické operátory: `{TISK_SIPKY}`, `{POCET} > 0`, `{TYP} == "PD"`, `&&`, `||`, `!`, závorky; VisibilityExpression na elementech a sekcích; ExpressionEvaluator + Parser + AST nodes
- Part 2a — agregace: `SUM({CENA})`, `COUNT()`, `AVG({POLE})`, `MIN({POLE})`, `MAX({POLE})` v TableElement; SummaryRows; AggregationResolver; záložka Souhrn v TableEditorDialog
- Part 2b — formátování placeholderů: `{POLE:N2}`, `{POLE:dd.MM.yyyy}` (.NET formáty); PlaceholderFormatter + DataContext rozšíření
- Part 2c — řetězcové funkce: `{POLE:LEFT(4)}`, `{POLE:UPPER}`, `{POLE:PADLEFT(3,'0')}`, `{POLE:TRIM}`

**UX polish — hotové položky**
- **f. Side panel vlastností** ✅ základ (2026-03-21) — inline záložka "Vlastnosti" v levém panelu; modální dialog zachován jako fallback
- **h. Ikonový toolbar** ✅ (2026-03-21) — 33 Fluent UI ikon, 7 skupin, ToolbarStyles.xaml

---

## 🖨️ PageLayoutEngine + stránkovaný preview (detail) ✅ (2026-03-17)

Preview v designeru = plný engine. "Tisk" = ten samý engine s reálnými daty → PDF.
Architektonické rozhodnutí (2026-03-17): engine je **integrovaný** v TemplateDesigneru, ne samostatný projekt. Oddělení do samostatných .csproj se udělá až bude druhý konzument.

### Vrstvy systému

Viz diagram v `CLAUDE.md` (sekce "Architektura — vrstvy systému").

### PageLayoutEngine — vstup/výstup

```csharp
class PageLayoutEngine {
    LayoutResult Layout(Template template, DataContext? data);
}
class LayoutResult { List<Page> Pages; int TotalPages; }
class Page { int PageNumber; List<PlacedSection> Sections; }
class PlacedSection {
    Section Source;        // odkaz na originální sekci
    float Y, Height;      // absolutní pozice na stránce (mm)
    float? ClipTop, ClipBottom;  // ořez pro SplitAcrossPages
    DataContext? Data;     // obohacená o PAGE/TOTAL_PAGES
}
```

### Implementační kroky ✅ vše hotovo

| Krok | Co | Stav |
|---|---|---|
| 1 | `PageLayoutEngine` — section ordering + repeating (OnEveryPage atd.) | ✅ |
| 2 | Stránkovaný preview — multi-page pod sebou se stíny a rámečky | ✅ |
| 3 | Flow layout — dynamická výška elementů posouvá elementy pod nimi | ✅ |
| 4 | Page breaking — sekce SplitAcrossPages se rozdělí přes stránky (ClipTop/ClipBottom) | ✅ |
| 5 | PerDataRow expanze — sekce se opakuje pro každý řádek dat | ✅ |
| 6 | PAGE/TOTAL_PAGES injekce + VisibilityExpression per stránku | ✅ |
| 7 | Multi-page PDF export — každá stránka = jedna PDF stránka | ✅ |

### Budoucí modularizace (až bude potřeba)

```
TemplateDesigner.Core.csproj      ← vrstvy 1+2+3 (žádný SkiaSharp, žádné WPF)
TemplateDesigner.Rendering.csproj ← vrstva 4 (SkiaSharp, žádné WPF)
TemplateDesigner.csproj           ← vrstva 5 WPF Designer
Edison.PrintService.csproj        ← vrstva 5 headless tisk z EDISONu
```

### Use cases díky oddělitelnosti

| Use case | Vrstvy | Popis |
|---|---|---|
| WPF Designer + preview | 1–5 | Aktuální aplikace |
| Tisk z EDISONu | 1–4 | Print dialog → GetData → engine → PDF |
| Batch tisk | 1–4 | Noční job: 200 faktur → 200 PDF |
| PDF jako služba | 1–4 | EDISON interně: dej PDF pro fakturu #123 |
| REST API | 1–4 | HTTP POST šablona + data → PDF response |
| Web preview | 1–4 | ASP.NET endpoint → PNG/PDF → iframe |
| Email příloha | 1–4 | Auto-generování PDF a připojení k emailu |
