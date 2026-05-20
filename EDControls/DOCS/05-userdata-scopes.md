# UserData scopes — registr

Centrální evidence všech **scope × key** kombinací uložených v `^ALVA.EDControls.UserData` napříč EDISON ekosystémem. Slouží jako single source of truth pro:

- Co se kde ukládá (žádné kolize napříč aplikacemi)
- Kdo vlastní jaký scope (přidání nové aplikace = nový scope, ne klíč v existujícím)
- Schéma jednotlivých blobů (klienti znají strukturu, server je „dumb")

> **Když přidáváš nový konzument, ZAVOLEJ:** doplň řádek do tabulky [§ Registr](#registr) níže ve stejném commitu jako kód.

---

## Princip pattern

`^ALVA.EDControls.UserData` je **generic document storage** — opaque JSON blob per `(IdFa × userID × scope × key)`. Server (Caché) **neinterpretuje** hodnoty — to je úloha klienta.

```
^ALVA.EDControls.UserData(IdFa, userID, scope, key) = JSON blob
                                ↑       ↑
                            namespace klíč v rámci scope
                            komponenty
```

**Inspirace:** localStorage / Windows Registry HKCU / iOS NSUserDefaults / Android SharedPreferences. Standard pattern pro per-user state.

**Pros:** Server-side schema-less, klienti se evolují nezávisle, žádné DB migrace při přidání nové komponenty.
**Cons → mitigace:** schema drift → backward-compatible JSON, corrupt blob → defensive parsing fallback na defaults, kolize → tento registr + scope naming konvence.

---

## Dva patterny: komponenta vs aplikace

Konzumenti `IUserPrefsService` se dělí na dvě kategorie. Liší se v tom, **jak generují klíče v rámci scope** — pochopení rozdílu pomáhá při návrhu scope/key schématu.

| Aspekt | **Standard komponenta** (EDList, EDInputGrid, EDFilter) | **Ad-hoc aplikace / form** (TemplateDesigner, Grafikon) |
|---|---|---|
| Sdílení | Mnoho instancí napříč mnoha okny | Jediná aplikace |
| Identita instance | **HostId** (per použití) | **App name** + případně sub-features |
| Schema | **Fixní** — každá instance má stejný tvar (např. `ColumnLayout`) | **Specifické** pro tu aplikaci (custom records) |
| Scope naming | `"EDControls.<ComponentName>"` | `"<AppName>"` + případně `"<AppName>.<Feature>"` |
| Keys v scope | **N** (jeden per HostId/instance) | **Málo** (`"MainState"`, `"RecentFiles"`, …) |
| Schema evolution | Vlastník = autor komponenty (řízeně, breaking change zasahuje všechny konzumenty) | Aplikace evoluje samostatně |
| Příklad | 20 EDList instancí v projektu = 20 keys v jednom scope | TD má 3 scopes (`"TemplateDesigner"`, `"TemplateDesigner.InstanceDefaults"`, později další) |

### Jak global vypadá s ekosystémem několika aplikací + komponent

```
^ALVA.EDControls.UserData(IdFa, user, "EDControls.EDList",                  "TD.TemplatePicker") = {...}
                                                                            "MHD.LinkyList")     = {...}
                                                                            "MHD.SpojeList")     = {...}
^ALVA.EDControls.UserData(IdFa, user, "EDControls.EDInputGrid",             "TD.StopColumns")    = {...}
^ALVA.EDControls.UserData(IdFa, user, "EDControls.EDFilter",                "MHD.LinkyFilter")   = {...}
^ALVA.EDControls.UserData(IdFa, user, "TemplateDesigner",                   "MainState")         = {...}
                                                                            "RecentFiles")       = [...]
^ALVA.EDControls.UserData(IdFa, user, "TemplateDesigner.InstanceDefaults",  "<guid-1>")          = [...]
^ALVA.EDControls.UserData(IdFa, user, "Grafikon",                           "MainState")         = {...}
                                                                            "Tabs")              = [...]
^ALVA.EDControls.UserData(IdFa, user, "Grafikon.LineLayouts",               "<lineId-42>")       = {...}
```

V Caché Studio si pak vidíš celý strom najednou. `zw ^ALVA.EDControls.UserData(IdFa, user, "TemplateDesigner")` vrátí jen TD věci.

### Když je hraniční případ

Někdy aplikace **používá komponentu vnitřně** a chce její state persistovat per-aplikační kontext (např. TemplatePicker dialog v TD chce vlastní column layout). V tom případě:

- **EDList si vlastní svůj scope** (`"EDControls.EDList"`)
- **TD definuje konkrétní HostId** (např. `"TemplateDesigner.TemplatePicker"`)
- Tím se HostId stává **bridge mezi 2 patterny** — komponenta vlastní schema, aplikace vlastní namespace v HostId

Funguje to čistě, dokud HostIdy mají FQN prefix té aplikace.

---

## Konvence pro klienty

### 1. Scope naming

Vždy **fully qualified** — prefix komponenty/aplikace + sub-namespace přes tečku.

| ✅ Dobře | ❌ Špatně | Důvod |
|---|---|---|
| `"EDControls.EDList"` | `"EDList"` | Bez prefixu kolize s budoucí aplikací |
| `"TemplateDesigner.InstanceDefaults"` | `"InstanceDefaults"` | Patří TD, ne genericky |
| `"TemplateDesigner"` (main scope) | `"TD"`, `"td"` | Vyhnout se zkratkám |

Sub-namespacing **přes tečku v scope**, ne přes prefix v keys:

| ✅ Dobře | ❌ Špatně |
|---|---|
| scope `"TemplateDesigner.InstanceDefaults"` + key `"<templateID>"` | scope `"TemplateDesigner"` + key `"InstanceDefaults.<templateID>"` |

### 2. Typed records v C# (no dynamic JSON)

Každý blob má typed record matchující schéma. Žádné `JsonNode.GetProperty(...)` runtime lookups.

```csharp
public record MainStatePref(
    float Zoom = 1.0f,
    bool ShowGrid = false,
    int GridSize = 5,
    // ... defaults pro všechny pole — backward-compat
);
```

### 3. Defensive parsing — fallback na defaults

```csharp
try
{
    var json = await _prefs.LoadAsync(scope, key);
    return json?.Deserialize<MainStatePref>() ?? MainStatePref.Default;
}
catch (Exception ex)
{
    // log, ale aplikace nesmí padnout — fallback default
    Debug.WriteLine($"[Prefs] Load failed ({scope}/{key}): {ex.Message}");
    return MainStatePref.Default;
}
```

### 4. Backward-compatible schema changes

Klient se musí umět vyrovnat s tím, že starý JSON v globalu nemá nová pole nebo má pole, která už nezná.

**Tabulka jak `System.Text.Json` reaguje na mismatch (s typed recordem + defaults):**

| Změna ve schématu | JSON v globalu | Výsledek |
|---|---|---|
| Přidáš pole s default (`bool ShowMinimap = false`) | chybí klíč „ShowMinimap" | ✅ default `false` se použije, ostatní načteno OK |
| Smažeš pole | JSON obsahuje navíc starý klíč | ✅ extra klíč se tiše ignoruje |
| Přidáš pole **bez** default | chybí klíč | ⚠️ Deserialize vrátí null → defensive catch → fallback na `T.Default` (vše ztratíš) |
| Přejmenuješ pole | starý název v JSON | ⚠️ stará hodnota propadne, dostaneš default pro nové jméno |
| Změníš typ pole | nekompatibilní hodnota | ⚠️ JsonException → catch → fallback na defaults |
| Změníš sémantiku (jednotky, význam) | starou hodnotu načteš s novým významem | 💀 Tichý bug — nepoznáme, doporučujeme **bumpnout Version** |

**Pravidlo zlatého standardu pro evoluci schématu:**

> Vždy nová property má **default hodnotu v primary ctoru** — `bool Foo = false`, ne `bool Foo`.

To samo o sobě zajišťuje seamless upgrade v 95% případů. Konkrétně:

```csharp
public record MainStatePref(
    float Zoom = 1.0f,
    bool ShowGrid = false,
    bool ShowMinimap = false,   // ← přidáno v nové verzi, starý JSON to nezná
    ...);
```

Starý JSON bez „ShowMinimap" se načte → `ShowMinimap = false` (z ctor default), ostatní fields normálně. Po prvním save se „ShowMinimap" zapíše a od té chvíle je v globalu permanentně.

**Breaking change** (rename, type change, sémantika): explicitní version + migration v `LoadBlobAsync`:

```csharp
public record MainStatePref(
    int Version = 2,              // ← bumpni z 1 na 2
    float CellSize = 5f,          // ← rename z GridSize
    ...);

// v LoadBlobAsync, mezi LoadAsync a Deserialize:
if (node["Version"] is null && node["GridSize"] is { } oldGridSize)
    node["CellSize"] = oldGridSize.GetValue<float>();
```

**Nezavádíme dopředu** (YAGNI) — `Version` přidáme až tehdy, kdy bude první skutečně breaking change. Do té doby všechny nové fields mají default.

### 5. Save policy

- **Atomická save** celého blobu (ne per-field) — JSON je nedělitelný
- **Debounce frekventní změny** (např. drag splitter → 500ms debounce → 1 save)
- **Fire-and-forget** v většině případů (don't block UI on save)
- **Error handling**: log + pokračovat, nikdy crash

---

## Registr

> **Format:** `scope` | `keys` | typed schema (C# record nebo struktura) | owner / kde implementováno

### `EDControls.EDList`

| Key | Schema | Owner |
|---|---|---|
| `<HostId>` (např. `"TemplateDesigner.TemplatePicker"`) | `ColumnLayout` record — šířky / pořadí / sort sloupců | `EDControls.EDList.ColumnLayout` |

**Stav:** ✅ Implementováno (L1, F1 commit, 2026-05-15).

### `TemplateDesigner` (Wave 1)

| Key | Schema | Owner |
|---|---|---|
| `"MainState"` | `MainStatePref` record — zoom, grid, snap, showGrid, showSectionHeaders, showRuler, gridSize, snapGapMm, showPreviewPane, leftPanelWidthPx, previewPanelWidthPx, activeLeftTabIndex | `TemplateDesigner.Services.TdPreferencesService` (plánováno) |
| `"RecentFiles"` | `List<string>` — last N opened file paths (capped, např. 10) | `TemplateDesigner.Services.TdPreferencesService` (plánováno) |

**Stav:** ⬜ Wave 1 — plánováno.

### `TemplateDesigner.InstanceDefaults` (Wave 1)

| Key | Schema | Owner |
|---|---|---|
| `<templateID>` (Guid string) | `List<string>` — instance IDs vybrané při posledním použití šablony | `TemplateDesigner.Services.TdPreferencesService` (plánováno) |

**Stav:** ⬜ Wave 1 — plánováno.

### `TemplateDesigner.PropertiesPanel` (budoucí)

| Key | Schema | Owner |
|---|---|---|
| `"GroupExpanded.<elementType>"` (např. `"GroupExpanded.TextElement"`) | `Dictionary<string, bool>` — group name → expanded/collapsed | TD PropertiesPanel (zatím skip — Wave 4 EDInputForm migrace stejně to nahradí) |

**Stav:** ⬜ Skip — odloženo do Wave 4.

### `TemplateDesigner.RecentColors` (budoucí)

| Key | Schema | Owner |
|---|---|---|
| `"Colors"` | `List<string>` — last N použitých barev v color pickeru (#AARRGGBB) | TD ColorPickerHelper |

**Stav:** ⬜ Skip pro V1 — možná Wave 1.5.

---

## Storage layery — co kde

Ujasnění proti dalším storage mechanismům v EDISON ekosystému:

| Layer | Storage | Per | Co | Použij když |
|---|---|---|---|---|
| **A. UserData (tento registr)** | `^ALVA.EDControls.UserData` přes `IUserPrefsService` | (IdFa × userID × scope × key) | Per-user preference, layout, recent files | State přežívá restart, sdílí mezi stroji téhož uživatele |
| **B. WindowStatePersistence** | `^ALVA.LIBRARY.Form.Params` (legacy) | (MLP × IDfa × FormID) | Window pozice, velikost, Maximized | Specificky WPF Window state — separátní legacy mechanism, ne duplikovat zde |
| **C. Template storage** | `^ALVA.TemplateDesigner.Templates` | (IdFa × TemplateID) | Šablony (metadata + JSON data) | Doménová data šablon |
| **D. Embedded v Template JSON** | Property uvnitř šablony | Per šablona | Per-template state cestující se šablonou (např. `Template.EditorPreferences`) | Když settings patří k šabloně, ne k uživateli |

### Rozhodovací mřížka

> „Mám nový state X — kam s ním?"

1. Cestuje to **se šablonou**? → **D** (Template JSON property)
2. Je to **WPF Window** pozice/velikost? → **B** (WindowStatePersistence)
3. Je to **per-user preference** (zoom, grid, layout)? → **A** (tento registr)
4. Je to **doménová data** (šablony, faktury, linky)? → **C** nebo per-doménový global

**Pravidlo:** lokální JSON soubory (`%APPDATA%`) se **nepoužívají** pro persistentní state. Pokud něco potřebuje přežít restart, jde do Caché.

---

## Jak přidat nový scope

1. **Rozhodni:** je to per-user × IDfa state? Pokud ano, pokračuj. Pokud ne, použij jiný layer.
2. **Naming:** vyber fully qualified scope (`MyComponent.SubFeature`).
3. **Schema:** vytvoř C# record s defaults pro všechny pole.
4. **Service wrapper:** napiš service třídu kolem `IUserPrefsService` s typed API (LoadAsync / SaveAsync s konkrétním record).
5. **Defensive parsing:** Load → catch exception → fallback na defaults.
6. **Doplň řádek do § Registr výše** ve stejném commitu jako kód.

---

## Anti-patterns — neopakovat

- ❌ **Generic JsonNode manipulation** v aplikaci — vždy typed records
- ❌ **Sdílený scope mezi nesouvisejícími features** („misc") — separátní scopes vždy
- ❌ **Encoding metadata do key** místo do struktury (`"GroupExpanded.TextElement.Font"` → použij vnořený JSON v hodnotě)
- ❌ **Save on every keystroke** bez debouncing — flooding Caché
- ❌ **Throw on parsing failure** — vždy graceful degradation na defaults
- ❌ **Persistovat per-session state** (selected elements, undo stack, transient UI state) — to není „preference"
