# Glob: dotnet/Models/**

## Datový model

### Template
```csharp
class Template {
    Guid Id
    string Name
    string Description
    string? AgendaCode        // kód agendy pro databázová pole: "JR", "FAKTURY" — null = obecná šablona
    PageSize PageSize
    List<Section> Sections
}

class PageSize {
    float Width               // mm
    float Height              // mm
    float MarginTop           // mm
    float MarginBottom        // mm
    float MarginLeft          // mm
    float MarginRight         // mm
}
```

### Section (univerzální)
```csharp
class Section {
    Guid Id
    string Name

    // Chování tisku
    SectionPrintBehavior PrintBehavior      // určuje KDY a JAK ČASTO se sekce tiskne
    SectionOverflowBehavior OverflowBehavior // určuje chování při přetečení stránky

    // Rozměry
    float? DesignerHeight                   // výška v mm pro designer — null u dynamických sekcí

    // Pokročilé
    string? VisibilityExpression            // podmínka viditelnosti, např. "{PAGE} > 1"

    // Speciální properties (pro některé případy použití)
    string? DataSource                      // klíč dat pro PrintBehavior=PerDataRow
    float? RowHeight                        // výška řádku pro PrintBehavior=PerDataRow

    // Obsah
    List<TemplateElement> Elements
    Dictionary<string, string>? CustomProperties
}

enum SectionPrintBehavior {
    Once,                         // vytiskne se jednou (výchozí)
    OnEveryPage,                  // každá stránka (hlavička/patička)
    OnFirstPageOnly,              // jen první stránka
    OnLastPageOnly,               // jen poslední stránka
    OnEveryPageExceptFirst,       // všechny kromě první
    PerDataRow                    // LEGACY — logika opakování patří do elementů (TableElement), ne sekcí
}

enum SectionOverflowBehavior {
    KeepTogether,                 // nepřerušovat, přesunout celou na další stránku
    SplitAcrossPages,             // rozdělí se přes stránky (tabulky, dlouhý text)
    AllowSplit                    // rozdělení povoleno ale ne preferováno
}
```

**Poznámky:**
- Section je **univerzální třída** — chování určují properties (PrintBehavior, OverflowBehavior), ne typ třídy
- `DesignerHeight = null` → dynamická sekce — designer zobrazí placeholder výšku, print engine ji počítá z obsahu

### Běžné případy použití sekcí

| Požadavek | Nastavení |
|---|---|
| **Hlavička stránky** | PrintBehavior=OnEveryPage, OverflowBehavior=KeepTogether, DesignerHeight=25mm |
| **Patička stránky** | PrintBehavior=OnEveryPage, OverflowBehavior=KeepTogether, DesignerHeight=15mm |
| **Titulní stránka** | PrintBehavior=OnFirstPageOnly, DesignerHeight=30mm |
| **Závěrečná stránka** | PrintBehavior=OnLastPageOnly, DesignerHeight=20mm |
| **Hlavička kromě první** | PrintBehavior=OnEveryPageExceptFirst, DesignerHeight=25mm |
| **Pevný obsah** | PrintBehavior=Once, DesignerHeight=30mm |
| **Oddělovač** | PrintBehavior=Once, DesignerHeight=5mm |
| **Tabulka** | PrintBehavior=Once + TableElement s DataSource a Columns (logika opakování je v elementu!) |

### Custom šablony sekcí (uživatelské)

Uživatel si může uložit často používané sekce jako šablony a znovu je používat.

**Ukládání:**
- Pravý klik na sekci v levém panelu → "💾 Uložit jako šablonu..."
- Zadá název → uloží se do `section-templates/{název}.json`
- Formát: přesně stejný jako sekce v Template JSON (s elementy)

**Načítání:**
- Menu `[+ Přidat sekci ▾]` automaticky načte všechny `*.json` z `section-templates/`
- Deep clone s novými Guid při vložení
- Funguje offline (file-based)

**V1 implementace (nyní):**
- `SectionTemplateManager` — file-based provider
- Ukládá do složky vedle .exe: `bin/Debug/net9.0-windows/section-templates/`
- Portable (lze zkopírovat složku jinam)

**V2 plán (budoucnost):**
- DB backend přes `CacheSectionTemplateProvider`
- Sdílené šablony mezi uživateli
- Možnost hybrid (lokální + DB šablony)

### TemplateElement (abstraktní)
```csharp
abstract class TemplateElement {
    Guid Id
    float X, Y               // pozice v mm, relativně k sekci (levý horní roh)
    float Width              // šířka v mm
    float? Height            // výška v mm — null u dynamických elementů (TableElement atd.)
    int ZIndex
    bool IsLocked            // uzamčený element nelze přesunout ani změnit velikost
    string? VisibilityExpression  // podmíněná viditelnost, např. "{POCET} > 0", null = vždy viditelný
    abstract void Render(SKCanvas canvas, DataContext? data)
}
```

**Pozicování — klíčová pravidla:**
- Všechny souřadnice jsou v **milimetrech**, relativní k levému hornímu rohu sekce
- `Height` je `null` u dynamických elementů (TableElement, InfiniteText) — výšku vypočítá print engine za běhu podle dat
- Statické elementy (TextElement, RectangleElement, ImageElement, LineElement) mají vždy pevnou výšku
- Print engine bere pozice jak jsou uloženy v JSON — nepočítá žádný layout kromě dynamických výšek
- Konverze mm → px probíhá až při renderování (designer i print engine zvlášť podle svého DPI/zoomu)

### Typy elementů
| Typ | Popis |
|---|---|
| `TextElement` | Statický nebo datově vázaný text s placeholder podporou |
| `ImageElement` | Obrázek (statický nebo z dat) |
| `RectangleElement` | Obdélník / pozadí |
| `LineElement` | Čára / oddělovač |
| `BarcodeElement` | QR kód nebo Code128 čárový kód (ZXing.Net) |
| `TableElement` | Tabulka Fixed/Dynamic — sloupce + UI editor, DataSource z Caché |

Systém musí být **rozšiřitelný** — nové typy elementů i sekcí přidáváme podtřídami.

### DataSchema (schéma dostupných DB polí — pouze design time)
```csharp
class DataSchema {
    string AgendaCode                      // "JR", "FAKTURY" — odpovídá Template.AgendaCode
    string AgendaName                      // "Jízdní řád" — pro UI
    List<DataField> Fields                 // skalární pole — každá hodnota existuje jednou na sestavu
    List<DataFieldCollection> FieldCollections  // tabulkové kolekce — zdroje řádků pro Dynamic tabulky; volitelné V2+
}

class DataField {
    string FieldName              // "CISLO_LINKY" → uživatel píše {CISLO_LINKY}
    string Label                  // "Číslo linky" — zobrazuje se v UI browseru
    FieldDataType DataType        // Text | Number | Date | Time | DateTime | Boolean (metadata pro UI)
    string? Example               // "42" — ukázka hodnoty v browseru
    string? Description           // volitelný delší popis pro uživatele
}

class DataFieldCollection {      // volitelné — tabulkový datový zdroj pro V2+
    string Name                   // "Zastávky" — zobrazovací název
    string CollectionKey          // klíč kolekce (odpovídá collectionKey v JSON a DataSource na TableElement)
    List<DataField> Fields        // pole dostupná v každém řádku kolekce
}
```

**Klíčová pravidla:**
- DataSchema je **pouze metadata pro designer** — print engine ji nepotřebuje a nepoužívá
- `{FIELD_NAME}` placeholder lze použít v libovolném TextElementu kdekoliv v šabloně — schema neeviduje omezení
- Grouping (Scalar/Table scope) je **organizační hint pro UI**, nikoliv tvrdé omezení na použití placeholderu
- Schema je **oddělená od šablony** — Template ukládá jen `AgendaCode` (string reference), schema samotná je v `schemas/JR.schema.json`
- Flat `Fields` (V1) a hierarchické `Groups` (V2) mohou koexistovat v jednom souboru; UI zobrazí skupiny pokud jsou definované, jinak flat list

**Dva typy JSON souborů v systému:**
- `*.json` — šablona (Template) — definuje layout stránky, sekce, elementy
- `*.schema.json` — schéma agendy (DataSchema) — definuje dostupná DB pole, uloženo ve složce `schemas/`

**IDataSchemaProvider** — rozhraní pro načítání schémat:
```csharp
interface IDataSchemaProvider {
    IReadOnlyList<AgendaInfo> GetAvailableAgendas()   // seznam dostupných agend
    DataSchema? GetSchema(string agendaCode)           // načte schéma pro danou agendu
}
```

Implementace:
- `JsonFileDataSchemaProvider` — načítá `schemas/*.schema.json` ze složky aplikace (offline fallback)
- `CacheAgendaProvider` — volá Caché `AgendaAPI.GetSchema()` přes MLRest; implementuje i `ITableTemplateProvider`
- `CompositeDataSchemaProvider` — Caché primary + JSON fallback; vrátí výsledek prvního úspěšného
- Wiring: `Config/CacheSchemaWiring.TryCreate(AppSettings)` → vrátí null pokud Caché není nakonfigurováno

---

## JSON formáty a příklady

### Formát schématu agendy (*.schema.json)

```json
{
  "agendaCode": "JR",
  "agendaName": "Jízdní řád",
  "fields": [
    { "name": "CISLO_LINKY",            "label": "Číslo linky",            "type": "number", "example": "42" },
    { "name": "LOKALNI_OZNACENI_LINKY", "label": "Lokální označení linky", "type": "text",   "example": "A42" },
    { "name": "TITULEK",                "label": "Titulek sestavy",         "type": "text",   "example": "Jízdní řád linky 42" },
    { "name": "CAS_ODJEZDU",            "label": "Čas odjezdu",             "type": "time",   "example": "08:42" }
  ]
}
```

Pole `fieldCollections` je volitelné rozšíření pro V2+:
```json
{
  "agendaCode": "JR",
  "agendaName": "Jízdní řád",
  "fields": [],
  "fieldCollections": [
    {
      "name": "Zastávky",
      "collectionKey": "ZASTAVKY",
      "fields": [
        { "name": "POPIS_ZASTAVKA", "label": "Název zastávky", "type": "text", "example": "Praha hl.n." }
      ]
    }
  ]
}
```

Platné hodnoty pro `type`: `text`, `number`, `date`, `time`, `dateTime`, `boolean`.

### Formát šablony (*.json)

```json
{
  "version": "1.0",
  "id": "...",
  "name": "Jízdní řád CB_DP",
  "agendaCode": "JR",
  "pageSize": {
    "width": 210, "height": 297,
    "marginTop": 10, "marginBottom": 10, "marginLeft": 10, "marginRight": 10
  },
  "sections": [
    {
      "name": "Záhlaví",
      "printBehavior": "OnEveryPage",
      "overflowBehavior": "KeepTogether",
      "designerHeight": 25,
      "elements": [
        {
          "type": "TextElement",
          "x": 10, "y": 5, "width": 190, "height": 12,
          "text": "Jízdní řád {NAZEV}",
          "fontSize": 14,
          "fontWeight": "Bold",
          "textColor": "#FF000000"
        }
      ]
    }
  ]
}
```

### JSON serializer nastavení

```csharp
WriteIndented = true                          // odsazený výstup, čitelný člověkem
PropertyNamingPolicy = CamelCase              // "printBehavior" místo "PrintBehavior"
DefaultIgnoreCondition = WhenWritingNull      // null hodnoty se do JSON nevypisují
JsonStringEnumConverter()                     // "OnEveryPage" místo 1
RoundedFloatConverter (4 des. místa)          // 0.5000001 → 0.5
```
