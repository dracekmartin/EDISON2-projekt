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

**Default:** přidávej pole s default hodnotou (chybějící v starých blobech → default).

**Breaking change** (např. rename pole, změna typu): přidej `int Version` do blobu a migrate kód.

```json
{ "v": 2, "zoom": 1.0, "showGrid": false, ... }
```

Klient kontroluje `v` a aplikuje migrační logiku. **Nezavádí dopředu** (YAGNI) — až bude breaking change, přidat.

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
