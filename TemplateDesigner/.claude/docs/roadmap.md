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

**v1.0 demo balíček HOTOV** (2026-05-15). EDList + EDFilter v1 uzavřen (2026-05-19). Caché namespace refactor (Agendy/EDList/API role separation) hotov (2026-05-19). **UI polish (theme, implicit styles, perf, layout) uzavřen** (2026-05-20). Detaily v [`changelog.md`](changelog.md) a [`roadmap-archive.md`](roadmap-archive.md).

**Stav UI/theme** (po 2 dnech práce):
- EDTheme design tokens (33 barev + typo + spacing) v EDControls — runtime swap přes DynamicResource
- EDStyles implicit styles pro Button/TextBox/ComboBox/CheckBox/TabItem/MenuItem/Menu/ContextMenu/Separator
- TD MainWindow + 10 dialogů + 5 EDControls komponent na tokens
- Default Window icon (Edison Molecule) všude přes class handler
- 3-button popup (IPopupDisplayer.ConfirmYesNoCancel) — náhrada Win32 MessageBox
- Toolbar restructure (Mřížka & snap / Zoom / Náhled podskupiny + CmbScale do Upravit menu)
- Y-zarovnání designer ↔ preview přes TopBufferPx 60px
- Canvas perf: ClipRect + viewport culling pro grid + cached shadow paint
- Zoom limit 500% (vyšší vyžaduje virtualized rendering — viz roadmap)

**Kde pokračovat dál — návrat ke core**:

1. **Fáze A.2-D — Caché business logic** ⬅️ nejvyšší zákaznický dopad
   TRANSDEV/KORID suspended tickety v `Agendy/ZastavkoveJrMhd`: text template „spoj jede až do XY", custom komentář přebíjející auto-poznámku, NZ bug, skrytí atributů hidden sloupců, vypnutí auto-bezbariérovost, KORID přípojové řádky. Tabulka A.2.D níže + [`customer-requirements-zastavkovy-jr.md`](customer-requirements-zastavkovy-jr.md).

2. **Wave 1 — UserPreferences plné využití** (foundation `IUserPrefsService` hotová)
   Per-user × IDFa state: zoom, grid, recent files, instance picker default, splittery, recent colors. Plný plán v § Wave 1 níže.

3. **H-18/H-19 — Template versioning + migrator** (prerekvizita produkčního Wave 3)
   Bez versioning breaking JSON change zničí existující šablony. Detail v § Fáze B → Template versioning.

**Mimo aktuální focus** (až bude potřeba):
- Plná sada Filter polí (EDFilter rozšíření)
- Tier 3 EDInputForm refactor (`$lb()` → JSON envelope + chybějící typy polí)
- Tier 4 TD editor migrace na EDInputForm
- Virtualized canvas rendering (zoom >500%)

**Reference docs:** [`EDControls/DOCS/`](../../../EDControls/DOCS/) — `00-overview`, `01-wire-formats`, `02-EDList`, `03-EDFilter`, `04-EDTheme`.

---

## 📖 Závazné reference před implementací

| Doc | Kdy číst |
|---|---|
| [`feature-design-principles.md`](feature-design-principles.md) | **Před každým novým requestem** — kódová + UX rozhodovací mřížka, klasifikace requestu (typ 1/2/3), per-customer divergence, anti-patterns |
| [`customer-requirements-zastavkovy-jr.md`](customer-requirements-zastavkovy-jr.md) | Při práci na zastávkovém JŘ — konkrétní požadavky zákazníků s citacemi a gap analýzou |
| `.claude/rules/mvvm-patterns.md` | Při dotyku stylingu / dialogů / per-column editorů |
| `.claude/rules/ux-conventions.md` | Při přidávání nového dialogu / okna / keyboardové zkratky |
| `.claude/rules/new-element-checklist.md` | Při přidání nového typu elementu |
| §5 Architektonických pravidel níže | R1–R5 (async, errors, JSON, MVVM, versioning) — závazné pro Caché migraci |

---

## 📐 5 Architektonických pravidel

Tato pravidla jsou závazná pro **veškerou novou práci** — zejména pro Caché migraci. Jsou záměrně obecnější než code style, protože mají chránit proti drahým rework cyklům pozdě v projektu.

| # | Pravidlo | Proč |
|---|---|---|
| R1 | **Async Caché volání pro očekávaně pomalé operace** — pro Caché volání kde očekávaná doba >500ms (velké listy, složité queries, batch operace, nahrávání) vždy `async Task` + `await Task.Run(() => mlRest.Query(...))`. Pro typické fast operations (foundational komponenty s 50–300ms loady jako EDList, jednoduché lookupy) sync OK — async by přinesl spíš flicker než benefit. Při neznámé velikosti pesimisticky async. | Legacy BIL/BDG bylo 20 let synchronní a fungovalo — důkaz že je to akceptovatelné pro typické workflow. Naopak sync pro 1s+ operaci zamrzne UI a Windows zobrazí „Not Responding". Pravidlo chrání proti druhému; první je validní výjimka. |
| R2 | **Chyby přes `IPopupDisplayer` z EDControls** — žádný `MessageBox.Show` ani silent catch. Používat existující `EDControls.EDViewModel.IPopupDisplayer` (metody `Error(ex)`, `Confirm`, `Info`, `ConfirmClose`) přes DI nebo `EDViewModel` base. | Bez jednotné cesty se chyby v distribuci ztratí. `IPopupDisplayer` je firemní standard — už ho používá `EDViewModel.Command(...)` automaticky. |
| R3 | **CVar named IN + 4 OUT styly podle obsahu** — IN vždy CVar pojmenované klíče; OUT podle typu: CVar scalars (triviální), JSON v slotu (entita), JSON multi-slot (list), JSON envelope (composite, opt-in pro Tier 3). Plný standard, decision tree, příklady a checklist: [`EDControls/DOCS/01-wire-formats.md`](../../../EDControls/DOCS/01-wire-formats.md). | CVar je nativní zrcadlo Caché globálů (1 řádek per parametr), JSON pro strukturovaná data dává typovou bezpečnost přes `record` typy v .NET. Asymetrie není kompromis — odráží přirozenost obou stran. |
| R4 | **ViewModely bez WPF referencí** — žádný `using System.Windows` ani `MessageBox.Show` v `*ViewModel.cs` | MVVM princip, testovatelnost, možnost pozdějšího portu/unit testů. Dialogy přes `IPopupDisplayer` (R2). |
| R5 | **Template versioning je prerekvizita persistence** — dokud není `TemplateMigrator` + version field v JSON, neukládá se nic do Caché | Bez migrace se nové šablony nespustí na starším buildu → zničené ranní otevření souboru. Nelze to retro-fit po nasazení. |

**Předpoklad:** aplikace poběží nad Caché — není cíl fungovat offline, ne každá API potřebuje in-memory fallback. Fallback se řeší jen tam, kde to má konkrétní business smysl (např. poslední otevřené soubory v paměti během session).

**Kde jsou pravidla ověřitelná:**
- R1: review PR → `grep "MLRest\." *ViewModel*.cs` najde synchronní volání. U každého ověřit očekávanou dobu — fast operace OK, slow musí být async.
- R2/R4: `grep "MessageBox\." **/*ViewModel.cs` musí vracet 0 hitů
- R3: nové API metody: IN `$g(^||ML("IN","klic"))`, OUT podle obsahu (decision tree) — viz [`EDControls/DOCS/01-wire-formats.md`](../../../EDControls/DOCS/01-wire-formats.md)
- R5: Wave 3 nemůže začít bez hotového H-18/H-19 (versioning a migrator)

---

## 📋 Co je potřeba dělat

### 🔴 Fáze A.2-D: Caché business logic ⬅️ AKTUÁLNÍ PRIORITA

Suspended tickety TRANSDEV/KORID — Caché-side auto-poznámky a logika spojů v `ZastavkoveJrMhdAPI`. Plný kontext, citace a gap analýza viz **[`customer-requirements-zastavkovy-jr.md`](customer-requirements-zastavkovy-jr.md)**.

> **Pozn.:** Fáze A.2-A..C (StopList / DepartureGrid / NotesLegend UI úpravy) je hotová — viz Archiv níže. Zbývá A.2-D série (Caché logika) + A.2-A5/A6/B4 (odložené pro datový průzkum / diskuzi s kolegy).

#### A.2.D — Caché `ZastavkoveJrMhdAPI` úpravy

| Krok | Tiket | Zákazník | Co | Stav |
|---|---|---|---|---|
| A2-D1 | #7947 | TRANSDEV | Nový text template „spoj jede až do XY" pro spoje **prodlužující** základní trasu (symetrické k existujícímu „končí v zast. X") | |
| A2-D2 | #7955 | TRANSDEV | Vlastní komentář spoje musí přebít auto-poznámku rozdílů (suppress auto pokud má spoj custom text) | |
| A2-D3 | #7955 | TRANSDEV | **Bug** — „Spoje, nepoznámkovat" maže i inverzní znaménko (NZ); izolovat NZ od ostatních poznámek | |
| A2-D4 | #7161 | TRANSDEV | Skrýt „x — zastávka na znamení" pokud sloupec s `x` je hidden (kontrakt TD ↔ Caché — rozhodnutí kde se filtruje) | |
| A2-D5 | #7161 | TRANSDEV | Vypnutí auto-poznámky „všechny spoje s bezbariérovým vstupem" — parametr v `GetParameters` + checkbox v PrintDialog | |
| A2-D6 | #11300 | KORID | Skrýt prázdné přípojové řádky — vyžaduje **novou** doménovou strukturu `Pripoj` v `GetData` + nový element `ConnectionsListElement` | |

#### Odložené z A.2 (na rozhodnutí s kolegy)

| Krok | Tiket | Co | Důvod odložení |
|---|---|---|---|
| A2-A5 | #7937 | StopList per-row indent per směr / per Tč | Caché vrací stub `odsazeni=0`; chybí datový tok z `ALVA.MHD.ZST` (legacy „dta 10"?). Není přímo TD práce. |
| A2-A6 | #7959 | Filtr neobsluhovaných zastávek | Pravděpodobně už hotovo v Caché — vyžaduje verifikaci s reálnými TRANSDEV daty (linka 876xx se školní zastávkou) |
| A2-B4 | #8828 | Per-sloupek zastávky — samostatná vývěska | Vyžaduje (a) datový průzkum kde v `^ALVA.MHD` leží info o sloupcích, (b) business decision (identifikace, default chování). ~100 % práce v Caché. |

#### A.2 — Status komunikace s klienty

Při uzavírání jednotlivých tiketů odkazovat na **konkrétní featuru v TD** + verzi releasu. Texty M-line v helpdesku (`viz customer-requirements-zastavkovy-jr.md §H`) zákazníkům slíbily „nový tiskový modul" / „Studio JŘ" na polovinu 2026.

---

### 🔵 Caché Migrace — 5 Vln

Postupný přesun state a logiky z .NET na Caché. Cíl: žádné „zjistili jsme že by se muselo přepsat všechno" překvapení.

**Stav vln:**
| Vlna | Název | Stav | Cíl |
|---|---|---|---|
| 1 | Per-user state | 🟠 Foundation hotová (F1), zbývá full integration | `IUserPrefsService` (✅) + TD specific binding (zoom, grid, recent files, splitters) |
| 2 | Shared catalogs | ⬜ Nezačato | Agenda registry, palette, element defaults, section templates |
| 3 | Template storage | 🟡 CRUD hotový, advanced features zbývají | Template Caché CRUD (✅), versioning/audit/thumbnails (zbývá) |
| 4 | Form-driven UI | ⬜ Připraveno (helper `MLRestJson`), nezačato | EDInputForm místo vlastního PropertiesPanel; wire formát = JSON envelope (Styl 4) |
| 5 | Business logic per agenda | 🟡 Pattern hotový pro MHD + FAKTURY, čeká na další agendy | Čisté rozdělení agenda-specific logiky |

**Graf závislostí:** Wave 1 → 2 → 3 → 4 (sekvenčně), Wave 5 běží paralelně nezávisle.

**Foundation hotová** (může se použít napříč všemi vlnami):
- ✅ `IUserPrefsService` + Caché `ALVA.EDControls.UserData` — generic per-user state (F1)
- ✅ Wire formát standard — CVar named IN + 4 OUT styly (DOCS/01-wire-formats.md)
- ✅ `MLRestJson.CallAsync<TReq, TResp>` helper — připraven pro Wave 4 composite endpointy
- ✅ EDList komponenta (Wave 3 list konzumenti používají)

---

#### 🟠 Wave 1 — Per-user state (UserPreferences)

**Foundation hotová** (F1 commit, 2026-05-15) — generický `IUserPrefsService` + Caché `ALVA.EDControls.UserData` poskytují per (IdFa × userID × scope × key) blob storage. Dnes využíváno přes scope `"EDControls.EDList"` pro persist sloupců EDListu.

**Zbývá:** napojit konkrétní TD state na foundation a vystavit UX (recent files picker, scope `"TemplateDesigner"`).

**Klíčový design:** Wave 1 **nebude mít vlastní Caché endpoint** — použije existující `ALVA.EDControls.UserData` se scope `"TemplateDesigner"` a structured JSON value. To zjednodušuje storage layer a unifikuje per-user persistenci napříč EDControls i TD.

**.NET strana:**
```csharp
public record TdPreferences(
    float Zoom,
    bool ShowGrid, bool SnapToGrid, bool SnapToElements,
    int GridSize,
    double? MainSplitterPosition,
    double? PreviewSplitterPosition,
    List<string> RecentFiles,
    List<string> RecentColors,
    Dictionary<string, bool> GroupExpanded,
    string? LastActiveTab
);

public class TdPreferencesService {
    private readonly IUserPrefsService _prefs;
    private const string Scope = "TemplateDesigner";
    private const string Key = "MainState";

    public async Task<TdPreferences> LoadAsync() {
        var json = await _prefs.LoadAsync(Scope, Key);
        return json?.Deserialize<TdPreferences>() ?? TdPreferences.Default;
    }
    public Task SaveAsync(TdPreferences prefs) =>
        _prefs.SaveAsync(Scope, Key, JsonSerializer.SerializeToNode(prefs)!);
}
```

**Před:** každé ráno: `Ctrl+O` → dialog → navigace → nastavení zoomu → rozbalení skupin.
**Po:** aplikace spadne do stavu z posledního zavření, recent files v File menu, Ctrl+R = picker.

**Změny v kódu:**
| Vrstva | Co |
|---|---|
| Caché | **Žádná** — foundation `ALVA.EDControls.UserData` (F1) už hotová |
| .NET Services | Nový `TdPreferencesService` jako wrapper nad `IUserPrefsService` se scope `"TemplateDesigner"` |
| .NET App | `App.xaml.cs` — load async před `MainWindow.Show`; save v `MainWindow.Closing` (fire-and-forget) |
| .NET VM | `MainWindowViewModel` — bindovat `Zoom`, `ShowGrid`, atd. skrze `TdPreferences` |
| appsettings.json | **Zůstává jen pro síťovou konfiguraci** (Caché host, port, agenda seznam) |

**Risks:**
- Performance: `LoadAsync` musí proběhnout do ~300ms nebo startup zamrzne. Mitigace: timeout + při selhání defaults v paměti pro danou session.

| Krok | Co | Stav |
|---|---|---|
| W1-0 | `IUserPrefsService` foundation + Caché `ALVA.EDControls.UserData` | ✅ F1 (2026-05-15) |
| W1-1 | `TdPreferencesService` wrapper nad `IUserPrefsService` | |
| W1-2 | Wire-up: `App.xaml.cs` load at startup (async + timeout) | |
| W1-3 | `MainWindowViewModel` — bind Zoom/Grid/Snap/Splitters/Tab přes preferences | |
| W1-4 | Recent Files (Ctrl+R picker, File menu) — ukládá do prefs | |
| W1-5 | Recent Colors — last-used barvy v color pickeru (swatches v `ColorPickerHelper` nebo v EDInputForm Color field po W4-8) | |
| W1-6 | Instance picker default — per-template (`AgendaCode + TemplateID`) seznam vybraných instancí; obnoví se při otevření šablony. Vlastní key (`InstanceDefaults/{guid}`), ne MainState blob | |

---

#### 🟠 Wave 2 — Shared catalogs

Sdílený konfigurační obsah, který dnes žije v .NET souborech nebo hardcoded v kódu. Přesun do Caché = jedno místo pravdy, možnost aktualizace bez redeploymentu.

**4 nové Caché třídy:**

| Třída | Co | Dnes |
|---|---|---|
| `AgendaRegistryAPI` | Seznam dostupných agend (kód, label, Caché class pointer) | `appsettings.json` `agendas[]` |
| `CompanyPaletteAPI` | Firemní paleta barev (16 swatches, logo, default fonts) | Hardcoded v `DesignerState` |
| `ElementDefaultsAPI` | Výchozí property values per element type (font size, border width, ...) | Hardcoded v C# constructors |
| `SectionTemplateAPI` | Custom section templates (vlastní šablony sekcí) | Soubory v `section-templates/*.json` |

**Před:**
```
appsettings.json  →  seznam agend (každý uživatel má vlastní)
C# constructors   →  TextElement.FontSize = 10, BorderWidth = 0.1, ...
section-templates/*.json  →  per-machine soubory, nesdílí se
```

**Po:**
```
Caché ^ALVA.TDConfig(IdFa)   →  jedno místo pravdy pro firmu
IConfigCatalog (DI)         →  všechny klienti čtou odsud
Lokální cache v paměti       →  rychlost v rámci session (refresh na vyžádání)
```

**Změny v kódu:**
| Vrstva | Co |
|---|---|
| Caché | 4 nové třídy + storage `^ALVA.TDConfig(IdFa,<catalog>)` |
| .NET Services | `IAgendaRegistry`, `ICompanyPalette`, `IElementDefaults`, `ISectionTemplateCatalog` — každý má Caché implementaci |
| .NET DI | Centrální `ServiceLocator` / `EDServices` rozšířit o tyto 4 služby |
| .NET Elements | Constructor čte z `IElementDefaults.GetDefaults("TextElement")` místo hardcoded hodnot |
| .NET UI | ColorPicker čte swatches z `ICompanyPalette` (zatím `ColorPickerHelper`, později EDInputForm Color field po W4-8) |
| .NET `SectionTemplateManager` | Nahrazen `ISectionTemplateCatalog` |
| appsettings.json | **Smazat `agendas[]`** — přesouvá se do Caché |

**Risks:**
- Backward compat: existující šablony musí pokračovat s hardcoded defaults pokud Caché nemá `ElementDefaults` naplněné. Mitigace: `IElementDefaults.GetDefaults` vrací merge (Caché přepíše C# defaults).
- Migration: per-machine `section-templates/*.json` se musí jednorázově importovat do Caché. Mitigace: startup detekce → upload dialog "Nahrát šablony z disku?".

**Výstup:** žádné agenda/styl info v kódu → při přidání nové agendy se restart už neřeší.

| Krok | Co | Stav |
|---|---|---|
| W2-1 | Caché `AgendaRegistryAPI` + `.NET IAgendaRegistry` — migrace `appsettings.json` agendas | |
| W2-2 | Caché `CompanyPaletteAPI` + integrace s ColorPickerem (firemní paleta swatchů) | |
| W2-3 | Caché `ElementDefaultsAPI` + `IElementDefaults` konzumované v elementech | |
| W2-4 | Caché `SectionTemplateAPI` + `ISectionTemplateCatalog` + jednorázová migrace lokálních souborů | |

---

#### 🟡 Wave 3 — Template storage

**Základní CRUD hotový** (commit `5ac9dd0`, 2026-05-15). Šablony se ukládají do Caché přes `ALVA.TemplateDesigner.API.Sablony` s 5 metodami (`ListTemplates`, `LoadTemplate`, `SaveTemplate`, `DeleteTemplate`, `RenameTemplate`). UI v `CacheTemplatePickerDialog` (po L5 migraci přes EDList) + `SaveToCacheDialog`. JSON wire formát (R3), per-IDFa storage.

**Reálný storage layout** (liší se od původního plánu — atomická Metadata/Data + sekundární index):
```
^ALVA.TemplateDesigner.Templates(IdFa, "DTA", TemplateID, "Metadata") = $lb(Name, AgendaCode, Author, CreatedAt, ModifiedAt, ModifiedBy, Version)
^ALVA.TemplateDesigner.Templates(IdFa, "DTA", TemplateID, "Data")     = JSON šablony
^ALVA.TemplateDesigner.Templates(IdFa, "IDX", "ByAgenda", AgendaCode, TemplateID) = ""
```

**Zbývá pro plnou Wave 3:**
- Versioning (R5 prerekvizita) — H-18 + H-19 (TemplateMigrator + version field v JSON)
- Server-side migrace (`TemplateMigrationAPI` — migrace při Load)
- Audit log (`TemplateAuditAPI`)
- Thumbnails (100×140px PNG při Save → file picker s náhledem)
- History (seznam verzí + restore)
- Soft-delete místo hard delete
- Import wizard (jednorázová migrace `*.json` z disku do Caché)

**Pozn.:** dnešní stav postačuje pro **demo a běžné použití**. Versioning a audit budou potřeba před produkčním nasazením s reálnými zákaznickými šablonami — bez migrátoru breaking change zničí data.

**Změny v kódu pro doplnění:**
| Vrstva | Co |
|---|---|
| Caché | Rozšířit `Templates.cls` o versioning logic; nové třídy `TemplateMigrationAPI`, `TemplateAuditAPI` |
| .NET migrace | `TemplateMigrator` (R5) jako .NET-side diagnostic helper; autoritativní je Caché |
| .NET UI | `TemplateBrowserDialog` rozšířit o thumbnails, history, filter UI |

**Risks:**
- Conflict handling: uživatel A a B editují stejnou šablonu. MVP: last-write-wins + varování. V2: lock nebo merge.
- Performance: load 50 šablon s thumbnails = 5 MB JSON. Mitigace: `ListTemplates` vrací jen metadata, thumbnails fetch-on-demand.

| Krok | Co | Stav |
|---|---|---|
| W3-1 | Caché `ALVA.TemplateDesigner.API.Sablony` (CRUD: List, Load, Save, Delete, Rename) | ✅ 5ac9dd0 |
| W3-2 | .NET `CacheTemplateService` async wrapper + `CacheTemplateException` | ✅ 5ac9dd0 |
| W3-3 | `CacheTemplatePickerDialog` + `SaveToCacheDialog` UI | ✅ 5ac9dd0 (L5 migration na EDList: 9f5b165) |
| W3-4 | H-18 + H-19 (versioning + migrator) — prerekvizita R5 | |
| W3-5 | Caché `TemplateMigrationAPI` — server-side migrace jako source of truth | |
| W3-6 | Caché `TemplateAuditAPI` — audit log | |
| W3-7 | Thumbnails (100×140px PNG při uložení) | |
| W3-8 | History (verze + restore UI) | |
| W3-9 | Import wizard — jednorázová migrace existujících `*.json` souborů do Caché | |

---

#### 🟠 Wave 4 — Form-driven UI (EDInputForm)

Nahrazení `PropertiesPanel` + `PropertyDescriptor` systému standardním `EDInputForm` z EDControls. Každý element má Caché třídu `ALVA.TemplateDesigner.Elements.<Typ>` s metodami `Init/Validate/Save`.

**⚠️ Paralelní migrace:** `PropertyDescriptor` a `EDInputForm` **běží vedle sebe měsíce**. Nikdy ne cut-over all-at-once — per element gradual migration přes feature flag.

**Klíčové koncepty:**

```objectscript
/// Init: pro daný element vrátí definici formuláře (EDInputForm format)
ALVA.TemplateDesigner.Elements.TextElement.Init(ByRef sys)
  IN["elementJson"] = aktuální JSON elementu
  OUT["form"] = EDInputForm definice (pole, typy, validace, layout)

/// Save: uživatel submitne form → Caché vrátí updated values
ALVA.TemplateDesigner.Elements.TextElement.Save(ByRef sys)
  IN["elementJson"] = starý element
  IN["formValues"] = změněné hodnoty
  OUT["elementJson"] = nový element (validován)
```

**Překladová vrstva (`ElementFormBridge`):**
- .NET: element → JSON → Caché Init → EDInputForm definice → render ve WPF
- User edituje → submit → Caché Save → nový JSON → .NET vytvoří `SetPropertyCommand(oldJson, newJson)` → Execute → undo/redo zachován

**Feature flag per element type:**
```csharp
class ElementEditingConfig {
    HashSet<string> UseEDInputFormFor;   // ["TextElement"] — zbytek PropertyDescriptor
}
```
Uživatel může postupně přepnout element po elementu; při problémech rollback = flag odebrat.

**Změny v kódu:**
| Vrstva | Co |
|---|---|
| Caché | Nová třída per element type (`TextElement`, `ImageElement`, ...) — Init/Validate/Save |
| .NET | `ElementFormBridge` — překlad JSON ↔ EDInputForm values |
| .NET UI | `ElementPropertiesWindow` — pokud feature flag, použít EDInputForm; jinak PropertiesPanel |
| .NET testy | Integrační test: editace elementu přes form → správný `SetPropertyCommand` → undo/redo |

**Co zůstává v .NET:**
- Všechny `TemplateElement` třídy (model, Render, Clone, JSON serializace)
- `PageLayoutEngine`, `PageRenderer`, canvas interakce, drag & drop
- Undo/redo systém

**Risks:**
- Undo/redo parity: EDInputForm musí generovat `SetPropertyCommand` pro každou změnu, ne batch. Mitigace: per-field commit místo form-level commit.
- Latency: každé otevření properties = Caché round-trip. Mitigace: cache form definice per element type (Init = statická definice, Values = dynamická).
- EDDataGrid pro TableEditorDialog — odložit do chvíle, kdy EDDataGrid bude produkčně zralý.

**Kdy začít:** Wave 3 CRUD je stabilní, foundation pro Wave 4 (`MLRestJson.CallAsync`, wire formát standard, JSON envelope dokumentován jako Styl 4) je hotová. Není blocker teď — `PropertyDescriptor` funguje. Ale každá nová property v PropertyDescriptor = práce k přepsání později → zvažovat nové properties s vědomím, že budou dvakrát.

**Wire formát Wave 4:** JSON envelope (Styl 4) — composite struktura formuláře (Tab → Group → Field s validacemi) přirozeně pasuje. Helper `MLRestJson.CallAsync<TReq, TResp>` v EDControls je připraven. Detail wire formátu v [`EDControls/DOCS/01-wire-formats.md`](../../../EDControls/DOCS/01-wire-formats.md), architektonický plán v [`td-editor-migration-plan.md`](../../../EDControls/.claude/docs/td-editor-migration-plan.md).

| Krok | Co | Stav |
|---|---|---|
| W4-0 | `MLRestJson.CallAsync<TReq, TResp>` helper + JSON envelope wire formát | ✅ 22b9256 |
| W4-1 | Caché `ALVA.TemplateDesigner.Forms.TextElement` — první element migrace | |
| W4-2 | .NET `ElementFormBridge` — JSON ↔ element instance přes reflexi | |
| W4-3 | Feature flag + fallback na PropertyDescriptor | |
| W4-4 | Postupně: ImageElement, RectangleElement, LineElement, BarcodeElement | |
| W4-5 | TableElement — composite case (tabs Obecné/Sloupce, nested CellStyleEditor) | |
| W4-6 | Schedule elements (StopList, DepartureGrid, NotesLegend) | |
| W4-7 | Cleanup: PropertyDescriptor systém smazat až po 100% migraci | |
| W4-8 | EDInputForm rozšíření: chybějící typy polí (Color, Font, Suggest, Text+Picker, ActionId Button) | |
| W4-9 | EDInputForm wire formát refactor `$lb()` OUT → JSON | |
| W4-10 | **Collapsible groups** v EDInputForm — generický koncept (accordion, summary řádek u sbalené, stav per element type/scope do `IUserPrefsService`). Nahrazuje dříve plánovaný B0-10 v `PropertiesPanel` (zbytečné dělat v dočasném panelu). | |

---

#### 🔵 Wave 5 — Business logic per agenda (paralelně)

Čisté rozdělení agenda-specific business logiky do samostatných Caché tříd. Běží paralelně nezávisle na Wave 1–4.

**Cíl:** `ZastavkoveJrMhdAPI` (~1200 řádků) je OK, ale pro každou novou agendu (meziměstský, knižní, vozový) bude podobný. Zavést pattern před implementací Fáze C/D/E.

**Pattern:**
```
ALVA.TemplateDesigner.AgendaAPI          ← abstraktní
├── ZastavkoveJrMhdAPI                   ← MHD (hotovo)
├── MeziMestskyJrAPI                     ← Fáze C
├── KnizniJrAPI                          ← Fáze D
├── VozovyJrAPI                          ← Fáze E
└── TemplateDesignerFAKTURY              ← faktury (hotovo)

Každá implementuje:
  GetSchema, GetData, GetMockData, GetTableTemplates, GetTableTemplate
```

**Nezávislost na Wave 1–4:** business logika nedeleguje na uživatele ani storage — čistá data pipeline. Pokud Wave 4 ještě není hotový, agenda klidně funguje.

**Risks:**
- Shared helpery: dnes v `ZastavkoveJrMhdAPI` mnoho helperů (BuildZastavka, BuildOdjezd, AddAnotace). Extrahovat do base class až budou 2+ konzumenti — ne dopředu.
- Caché velikost: ~1000+ řádků per agenda. Mitigace: dělit do partial classes/routines když přesáhne 1500 řádků.

| Krok | Co | Stav |
|---|---|---|
| W5-0 | `AgendaAPI` abstrakt base class + 2 implementace (`ZastavkoveJrMhdAPI`, `TemplateDesignerFAKTURY`) | ✅ v archivu |
| W5-1 | Dokumentace `AgendaAPI` contract — co se musí implementovat, co je volitelné | ✅ částečně v [`cache-api.md`](../rules/cache-api.md) |
| W5-2 | Fáze C (Meziměstský) — využije pattern | |
| W5-3 | Fáze D (Knižní) — využije pattern | |
| W5-4 | Fáze E (Vozový) — využije pattern | |
| W5-5 | Refaktor: extract `AgendaAPIBase` helpery až budou 2+ konzumenti | |

---

#### 🚫 "Must not happen" scénáře

Konkrétní situace, které Caché migrace musí předejít. Kontrolní seznam při code review.

1. **Synchronní Caché volání v UI thread** → zamrznutí aplikace. Kontrola: `grep "MLRest\." *ViewModel*.cs` hledá synchronní volání.
2. **Silent fail při Caché chybě** → UserPreferences se nenačetly, uživatel neví proč. Kontrola: chyba jde přes `IPopupDisplayer.Error(ex)`.
3. **Breaking JSON change bez migrátoru** → staré šablony nejdou otevřít. Kontrola: před produkčním nasazením s reálnými šablonami musí být H-18/H-19 hotové (versioning + migrator). Wave 3 CRUD už běží, ale versioning chybí.
4. **Všechno naráz** (big bang migration) → nelze vrátit. Kontrola: feature flags per element (Wave 4), paralelní běh starého a nového systému.

---

### 🟠 Fáze B: Element flexibility — rozšíření možností elementů

Vychází z gap analýzy oproti komerčním report generátorům (Crystal Reports, Stimulsoft, DevExpress). Nezávislé na JŘ — platí pro fakturační i obecné šablony. Může běžet paralelně s Caché migrací (neblokuje a není blokované).

#### Template versioning (prerekvizita produkčního Wave 3)

Wave 3 CRUD už běží bez versioning, ale **před produkčním nasazením s reálnými zákaznickými šablonami** musí být H-18/H-19 hotové — jinak breaking JSON change zničí existující data.

| # | Co | Proč | Velikost |
|---|---|---|---|
| H-18 | `TemplateMigrator.Migrate(JsonDocument)` — detekce verze v JSON, pipeline migrací, spouští se před deserializací | Ochrana existujících šablon před breaking changes | S |
| H-19 | Migrace V1→V2: `TextStyle` flat→nested (breaking change 2026-03-24) + varování v UI | Konkrétní existující breaking change | S |

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

#### Obecný layout-reservation pattern u elementů

Aktuálně když element potřebuje rezervovat místo pro volitelný vizuální prvek (šipka směru u `StopListElement`), řeší se to ad-hoc. Do budoucna zvážit deklarativní mapu "pokud property X=true → rezervuj Y mm vlevo/vpravo". Vyřeší konzistentně napříč všemi elementy.


### 🟠 Fáze C: Meziměstský JŘ

Konceptuálně podobné MHD — zastávky + odjezdy spojů + poznámky. Rozdíl: sloupce odjezdů nejsou typy provozu (jako MHD), ale mají jiný koncept specifický pro meziměsto. Layout je pivot tabulka (zastávky × spoje). Pravděpodobně bude potřeba samostatný element (ne rozšíření DepartureGridElement).

| Krok | Co | Stav |
|---|---|---|
| 42 | Definovat meziměstský layout — analýza referenčních PDF, wire formát dat | |
| 43 | Implementace meziměstského JŘ elementu | |
| 44 | Caché `MeziMestskyJrAPI` — GetData/GetMockData (Wave 5 pattern) | |

---

### 🟠 Fáze D: Knižní JŘ

Layout TBD — pravděpodobně kompaktní formát pro knižní publikaci. Definovat layout před implementací.

| Krok | Co | Stav |
|---|---|---|
| 45 | Analýza referenčních knižních JŘ — definice layoutu a wire formátu | |
| 46 | `BookScheduleElement` — implementace (pravděpodobně víc elementů, viz poučení z MHD) | |
| 47 | Caché `KnizniJrAPI` — GetData/GetMockData (Wave 5 pattern) | |

---

### 🟠 Fáze E: Vozový JŘ

Layout TBD — hlavička oběhu, tabulka spojů, poznámky. Pravděpodobně víc elementů (viz poučení z MHD).

| Krok | Co | Stav |
|---|---|---|
| 48 | Analýza referenčních vozových JŘ — definice layoutu a wire formátu | |
| 49 | `VehicleScheduleElement` — implementace (pravděpodobně víc nezávislých elementů) | |
| 50 | Caché `VozovyJrAPI` — GetData/GetMockData + async loading (R1, velký objem dat) | |

---

### 🟡 Import PDF — doladění

| Krok | Co | Stav |
|---|---|---|
| 32 | Import: detekce tabulek z mřížky čar (3a) — místo 50 čar + 20 textů → jeden `TableElement` | |
| 33 | Import: JŘ-aware mód (3d) — dvoustupňový AI+engine import | ⬅️ rozpracováno (2026-04-03) — AI Vision pipeline + AiGuidedScheduleBuilder fungují, ladění na širší sadě JŘ |

**Aktuální stav:**
- ✅ Režim 1 (deterministický 1:1 import) hotovo (2026-03-13)
- 3a: Detekce tabulek z mřížky čar
- 3b: Křivky/Bézier (nízká priorita — zaoblené rohy rasterizovat jako bitmapa)
- 3c: AI post-processing — nahrazení statických textů za `{PLACEHOLDER}`
- **3d: JŘ-aware import** — dvoustupňový AI + engine, viz `architecture.md`

---

### 🟢 Menší priorita — UX polish

*(prázdné — všechny zachycené QoF body jsou hotové, viz changelog)*

---

### Tabulkové featury (na backlogu, zatím bez customer demand)

- **Grouping v TableElement** — seskupení řádků dynamické tabulky podle hodnoty pole (`GroupHeader`, `GroupFooter`, `Summary` řádky)
- **Computed fields** — pole vypočítaná na C# straně ze surových dat před předáním šabloně (`CELKOVA_CENA = CENA * MNOZSTVI`)
- **Vnořené datasource** — `_rows` uvnitř `_rows` (faktura → položky → daně); TableElement s child kolekcí
- **Min/max počet řádků dynamic tabulky** — `MinRows`, `MaxRows` properties; engine ořízne/doplní (zmínil kolega, není urgent)

---

### 🔵 Před produkcí (odloženo)

| Co | Popis |
|---|---|
| Full migration pipeline | Po H-18/H-19 rozšířit na každou breaking change (ne jen 2 verze) |
| CI/CD linting šablon | Ověřit šablony před deployem (Caché job) |
| Kategorie + popis šablony v UI | Wave 3 dnes drží Author/CreatedAt/ModifiedAt/Version v metadatech; chybí kategorie + popis v UI |

> Versioning, audit, thumbnails, history → viz Wave 3 (W3-4 až W3-9).

---

### 🟡 Virtualized canvas rendering (zoom >500%)

**Problém:** `SkiaSharp.Views.WPF.SKElement` při každém repaint alokuje a uploaduje byte buffer velikosti celé plochy (`Width × Height × 4`). Při zoom 1000% (A4) je SKElement ~8000×11000 px = **365 MB upload per frame**. Toto je hardware-bound bottleneck SkiaSharp Views.WPF integrace.

**Aktuální workaround (2026-05-20):** Zoom max omezen na 500% (SKElement ~93 MB upload). Funguje, ale teoretické maximum zoomu omezené.

**Proper řešení (až bude potřeba):** Virtualizace — SKElement udržovat ve velikosti viewport (~1600×900 px), implementovat custom scroll handling. Skia canvas translatuje by scroll offset, uživatel vidí jen viewport, ScrollViewer/scrollbary řízeny manuálně.

**Velikost:** ~4-6h. Refactor `<ScrollViewer><SKElement/></ScrollViewer>` na `<Grid><SKElement/> + custom ScrollBars/></Grid>`. Update hit testing (mouse coords → page coords s scroll offset), drag&drop, rubber band selection.

**Risk:** middle — mouse event coordinate transformation se musí důsledně updatovat napříč všemi interakcemi.

**Kdy:** jakmile bude potřeba zoom >500% pro reálnou práci. Aktuálně 500% pokrývá většinu inspekčních use case.

---

## 🤖 AI integrace — v pozadí

> **Status:** Sekundární, ne core feature. Aktuální AI funkce (generování šablony, PDF import enrichment, vision pipeline) zůstávají funkční, další rozšíření neplánovaná dokud není core (zastávkový 1:1, Caché migrace) vyřešený.

Architektonické rozhodnutí (2026-03-17): **žádné zásadní rozhodnutí teď není potřeba.** JSON šablona + DataSchema jsou dostatečné rozhraní pro AI. Rozšíření se dá přidat kdykoliv později bez breaking changes.

**Princip:** AI = copilot, ne autopilot. Vždy navrhne → uživatel potvrdí/upraví.

### Aktuální stav
- ✅ AI Assistant — generování celé šablony z textového popisu (Claude API)
- ✅ Schema injection — AI dostane seznam polí/kolekcí/parametrů agendy
- ✅ AI Vision pipeline — rozpoznání JŘ struktury z PDF obrázku

### Budoucí možnosti (až budou reální uživatelé s potřebou AI asistence)

**MCP server** — TemplateDesigner vystaví `get_template`, `apply_template`, `add_element`, `modify_element`, `validate_template` atd. Tenká vrstva nad ViewModelem; každý tool volá existující `_vm.Execute(command)`. Standardní protokol (stdio) — funguje s Claude i jinými AI.

**Pokročilé use cases (dlouhodobě):** onboarding průvodce, "přidej patičku s číslem stránky" (modifikace existující šablony), smart suggestions (TableElement → AI navrhne sloupce z DataSchema), migrace starých Caché sestav, template linting v CI/CD, batch generování šablon pro novou agendu.

**Proč to neblokuje:** JSON šablona je stabilní kontrakt mezi designerem a AI. Dokud AI umí číst a psát JSON v definovaném formátu, integrace funguje bez ohledu na to, jestli je to prompt, MCP, nebo REST API.

---

## ✅ Záměrně jinak — neřešit

- **Caché jako source of truth pro konfiguraci a šablony** — dává smysl pro doménovou logiku (Wave 1–3 to provádí)
- **Univerzální Section třída** — jednodušší než pevná hierarchie (Crystal Reports)
- **Absolutní pozicování jako default** — správné pro tiskové sestavy s pevným layoutem
- **PropertyDescriptor a EDInputForm paralelně** — ne cut-over, per element migrace (Wave 4)
- **Server-side migrace šablon** — autoritativní, ne klientská (Wave 3)

---

## 🗄️ Hotová práce

Detail dokončených etap (v1.0 demo balíček, Fáze A.2, B0/B0.5, JŘ elementy, PageLayoutEngine, …) → [`roadmap-archive.md`](roadmap-archive.md). Průběžný changelog → [`changelog.md`](changelog.md).

