# UserData scopes — registr

Centrální evidence všech **scope × key** kombinací uložených v `^ALVA.EDControls.UserData` napříč EDISON ekosystémem. Slouží jako single source of truth pro:

- Co se kde ukládá (žádné kolize napříč aplikacemi)
- Kdo vlastní jaký scope (přidání nové aplikace = nový scope, ne klíč v existujícím)
- Schéma jednotlivých blobů (klienti znají strukturu, server je „dumb")

> **🛑 Než přidáš nový state, přečti [§ 3 typy state — rozhodovací mřížka](#3-typy-state--rozhodovací-mřížka).** Bez ní vznikne ad-hoc nepořádek.
>
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

## 3 typy state — rozhodovací mřížka

> **Závazné pravidlo pro každý nový state:** zařaď ho do jednoho ze 3 typů níže. Tabulka pokrývá ~99% per-user persistence v EDISON ekosystému. Pokud věříš že tvůj případ tam **nepatří**, otevři diskusi a doplň zde — neulož to ad-hoc.

| Typ | Charakteristika | Trigger savu | Storage pattern |
|---|---|---|---|
| **1. UI state** | Omezená sada preferencí; všechny se mění během session, persistují společně | **On close** (jeden event) | **Jeden blob** ve scope `"<App>"`, key `"MainState"` |
| **2. Activity log** | Rostoucí list (FIFO, cap), aktualizuje se s každou user akcí | **On event** (per akce) | **Per-list klíč** ve scope `"<App>"`, key = název listu |
| **3. Per-entity** | Stav scaluje s N entitami (šablony, okna, sekce, ...) | **On event** (per entita) | **Vlastní scope** `"<App>.<Feature>"`, key = entity ID |

### Rozhodovací mřížka

```
1. Scaluje to s N entitami (templates, okna, sekce, ...)?
   ├── YES → Typ 3: separátní scope "<App>.<Feature>", key = entity ID
   └── NO → krok 2

2. Roste to v čase (list, history, FIFO)?
   ├── YES → Typ 2: vlastní key v app scope, save on event
   └── NO → Typ 1: do MainState blobu, save on close
```

### Příklady aplikace (TemplateDesigner)

| Item | Typ | Storage |
|---|---|---|
| Zoom, grid, snap, splittery, tab index, preview pane | 1 | `"TemplateDesigner"` / `"MainState"` |
| RecentFiles | 2 | `"TemplateDesigner"` / `"RecentFiles"` |
| RecentColors | 2 | `"TemplateDesigner"` / `"RecentColors"` |
| InstanceDefaults per šablona | 3 | `"TemplateDesigner.InstanceDefaults"` / `<template-guid>` |
| PrintParams per šablona | 3 | `"TemplateDesigner.PrintParams"` / `<template-guid>` |

### Proč 3 typy a ne víc/míň

**Proč nebundlovat všechno** do jednoho MainState blobu napříč všemi typy:

| Problém s bundling | Důvod splitnout |
|---|---|
| Crash ztratí celou history (Type 2) | Activity log saveuje **immediately on event** |
| Race condition (save on close + during-session save) | Per-list key má vlastní lifecycle |
| Write amplification (open jednoho file přepíše celý MainState) | Per-list key = malý zápis |
| 200 templates v MainState by ho zaplnily | Type 3 má vlastní scope = vizuální izolace |

**Proč neseparovat všechno** na samostatné klíče (i pro UI state):

| Problém s plnou separací | Důvod bundle Type 1 |
|---|---|
| N round-tripů při savu | Jeden save = atomic |
| Atomicita — Zoom uložen, GridSize ne | Bundle = guaranteed konsistentní snapshot |
| Overhead (Caché ~50ms per call) | 12 fields = 1 save místo 12 |

### Edge cases (zatím v EDISON neřešíme)

Pokud někdy přijde podobný požadavek, **otevři diskusi** — pravděpodobně to nepatří do `^ALVA.EDControls.UserData` ale do separátního storage mechanismu:

| Scenario | Proč nepatří sem | Co by mohlo být řešení |
|---|---|---|
| Auth tokens, credentials | Bezpečnostní concern, separátní treatment | Secure storage (DPAPI, key vault) |
| Audit log (kdo co kdy) | Cross-user/server log, ne per-user prefs | Vlastní auditní global |
| Real-time collaborative state | Multi-user shared editing | WebSocket session storage |
| Velká binární data (>100KB) | Storage určen na JSON blobs, ne BLOBs | `BinarySave` / file storage |
| Per-machine settings (path k Caché serveru) | Není per-user × IdFa, je per-machine | Lokální config (appsettings.json) |

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

### `TemplateDesigner` — Typ 1 (UI state) + Typ 2 (activity logs)

| Key | Typ | Schema | Owner |
|---|---|---|---|
| `"MainState"` | 1 | `MainStatePref` record — zoom, grid, snap, showGrid, showSectionHeaders, showRuler, gridSize, snapGapMm, showPreviewPane, leftPanelWidthPx, previewPanelWidthPx, activeLeftTabIndex | `TemplateDesigner.Services.TdPreferencesService` |
| `"RecentFiles"` | 2 | `List<string>` — last 10 opened file paths (capped, dedup case-insensitive) | `TdPreferencesService.AddRecentFileAsync` |
| `"RecentColors"` | 2 | `List<string>` — last 8 použitých barev v color pickeru (`"#AARRGGBB"`) | `TdPreferencesService.AddRecentColorAsync` (Wave 1.5) |

**Stav:** ✅ Wave 1 (2026-05-20, MainState + RecentFiles). ✅ Wave 1.5 (2026-05-20, RecentColors).

### `TemplateDesigner.InstanceDefaults` — Typ 3 (per template)

| Key | Schema | Owner |
|---|---|---|
| `<templateID>` (Guid string) | `List<string>` — instance IDs vybrané při posledním použití šablony | `TdPreferencesService.SaveInstanceDefaultsAsync` |

**Stav:** ✅ Wave 1 (2026-05-20).

### `TemplateDesigner.PrintParams` — Typ 3 (per agenda)

| Key | Schema | Owner |
|---|---|---|
| `<agendaCode>` (např. `"MHD"`, `"FAKTURY"`) | `Dictionary<string, JsonNode?>` — values for `TemplateParameter` keys naposledy použité v PrintDialog / preview liště pro tuto agendu | `TdPreferencesService.SavePrintParamsAsync` |

**Per-agenda granularita** (ne per-template): parametry (datum, varianta JŘ, časová osa) jsou agenda-level concerns. Nová šablona v rámci stejné agendy zdědí naposledy použité hodnoty — uživatel pokračuje kde skončil.

**Stav:** ✅ Wave 1.5 (2026-05-20).

### `EDISONMAIN2` — Typ 2 (oblíbené + používanost stromu modulů)

| Key | Typ | Schema | Owner |
|---|---|---|---|
| `"Favorites"` | 2 | `List<string>` — ElementId oblíbených modulů (ruční ★) v pořadí připnutí | `EDISONMAIN2.Windows.EdisonMainWindow.Modules.ModulePreferencesService` |
| `"ModuleUsage"` | 2 | `Dictionary<string, UsageEntry>` — ElementId → `{ Count, LastUsedUtc }` (automaticky při spuštění modulu) | `ModulePreferencesService` |
| `"TreeExpansion"` | 1 | `List<string>` — klíče rozbalených uzlů `"<tree>:<index-cesta>"` (např. `"main:5.2"`, `"favorites:0"`); `null`/chybí = serverové výchozí | `ModulePreferencesService` |
| `"SelectedTab"` | 1 | `int` — index naposledy otevřené záložky stromu; `null`/chybí = výchozí dle oblíbených | `ModulePreferencesService` |

Záložky „Oblíbené" (ořezaný strom dle Favorites) a „Používané" (žebříček dle ModuleUsage)
v hlavním shellu. ElementId = numerické ID z EDISFormEnum (string). TreeExpansion drží
rozbalení stromu modulů mezi relacemi (save na každou změnu rozbalení).

**Stav:** ✅ Implementováno (2026-06-22).

### Záměrně skipnuté

| Scope/key | Důvod skipu |
|---|---|
| `"TemplateDesigner.PropertiesPanel"` (group expanded state) | Wave 4 EDInputForm migrace panel kompletně nahradí (W4-10 řeší stejnou potřebu generic) |
| `"TemplateDesigner.FontOverride"` (per-user UI font) | Žádný user request pro TD; EDISONMAIN2 to má kvůli legacy accessibility, TD má EDTheme tokens |

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

## Jak přidat nový state — checklist

1. **Layer rozhodnutí:** je to per-user × IDfa state? Pokud ano, pokračuj. Jinak viz [§ Storage layery](#storage-layery--co-kde) (window pozice → B, doménová data → C, per-machine → out-of-band).
2. **Typ rozhodnutí:** projdi [§ 3 typy state — rozhodovací mřížka](#3-typy-state--rozhodovací-mřížka). Zařaď do Typu 1 / 2 / 3.
3. **Naming podle typu:**
   - Typ 1 → key v hlavním app scope (např. `"MainState"`)
   - Typ 2 → vlastní key v hlavním app scope (např. `"RecentFiles"`)
   - Typ 3 → vlastní scope `"<App>.<Feature>"`, key = entity ID
4. **Schema:** typed C# record (Typ 1) nebo `List<>` / `Dictionary<>` (Typ 2/3) s defaults pro všechny pole.
5. **Service wrapper:** rozšiř existující service třídu (např. `TdPreferencesService`) — typed API pro Load/Save dané položky.
6. **Defensive parsing:** Load → catch exception → fallback na defaults / empty.
7. **Save policy:** Typ 1 = on close (atomic). Typ 2/3 = on event (immediate).
8. **Doplň řádek do § Registr** ve stejném commitu jako kód — uveď i číslo typu.

---

## Anti-patterns — neopakovat

- ❌ **Ad-hoc rozhodnutí kam to dát** — vždy zařadit do [§ 3 typů](#3-typy-state--rozhodovací-mřížka)
- ❌ **Generic JsonNode manipulation** v aplikaci — vždy typed records
- ❌ **Sdílený scope mezi nesouvisejícími features** („misc") — separátní scopes vždy
- ❌ **Encoding metadata do key** místo do struktury (`"GroupExpanded.TextElement.Font"` → použij vnořený JSON v hodnotě)
- ❌ **Save on every keystroke** bez debouncing — flooding Caché
- ❌ **Throw on parsing failure** — vždy graceful degradation na defaults
- ❌ **Persistovat per-session state** (selected elements, undo stack, transient UI state) — to není „preference"
- ❌ **Bundlovat Typ 2/3 do MainState blobu** — crash ztratí history; race condition mezi save on close vs save on event
- ❌ **Separovat Typ 1 properties na samostatné klíče** — N round-tripů, ztráta atomicity
