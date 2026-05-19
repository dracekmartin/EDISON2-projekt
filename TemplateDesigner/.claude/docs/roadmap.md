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

> **v1.0 demo balíček HOTOV (2026-05-15)** — Release readiness R1+R2+R4 splněno (3 commity: `5ac9dd0` Caché Template Storage, `82b68be` Přepínač agendy, `fd25008` Instance Picker). Zákazníkovi lze ukázat funkční end-to-end workflow pro zastávkový JŘ MHD. Detaily viz [`changelog.md`](changelog.md). Hotové sekce → **Archiv** dole.

### ✅ EDList migrační vlna uzavřena (stav 2026-05-19)

Foundation work pro EDControls knihovnu je za prvním milníkem. **Všechny TD dialogy s Caché-driven listy jsou na EDList**, wire formát standard je propsaný, public developer DOCS hotové. Detail viz commity `7982005` (DOCS), `22b9256` (L6), `9d3fbd1` (wire formát fix), `4b55396` (migration plan), `e54a382` (LoadList overload), `9f5b165` (L5).

**Hotové (commity v repu):**
- ✅ **F1 — `IUserPrefsService`** + `LocalPrefs` + Caché `ALVA.EDControls.UserData` (prerekvizita Wave 1)
- ✅ **L1–L3 EDList foundation** — multi-select, column layout persist (přes IUserPrefsService), Title + client-side search
- ✅ **L5 — CacheTemplatePickerDialog na EDList** (filter podle agendy, CRUD v parent dialogu)
- ✅ **L6 — InstancePickerDialog na EDList multi-select** (linky MHD, pre-selection, search)
- ✅ **EDList wire formát fix** — `LoadList(filter)` na CVar named keys (sjednoceno se standardem)
- ✅ **Wire formát standard** — experiment paralelní implementace na L6, výsledek: CVar named IN + 4 OUT styly podle obsahu (CVar scalars / JSON v slotu / JSON multi-slot / JSON envelope opt-in). Detail v [`EDControls/DOCS/01-wire-formats.md`](../../../EDControls/DOCS/01-wire-formats.md).
- ✅ **EDControls public DOCS** — `00-overview.md`, `01-wire-formats.md`, `02-EDList.md` (single source of truth pro programátory). Konvence: změna komponenty = update odpovídajícího `0X-název.md` ve stejném commitu.
- ✅ **`MLRestJson.CallAsync<TReq, TResp>` helper** — připraven v EDControls pro Tier 3 EDInputForm composite endpointy.

**Architektonická rozhodnutí (referenční dokumenty):**

| Téma | Kde |
|---|---|
| Wire formát standard (CVar named IN + 4 OUT styly) | [`EDControls/DOCS/01-wire-formats.md`](../../../EDControls/DOCS/01-wire-formats.md) |
| EDList API reference | [`EDControls/DOCS/02-EDList.md`](../../../EDControls/DOCS/02-EDList.md) |
| Migrace TD editorů na EDInputForm (architektura, drift handling) | [`td-editor-migration-plan.md`](../../../EDControls/.claude/docs/td-editor-migration-plan.md) |
| Legacy EDISON komponenty (BIG/BIL/BDG) — baseline analýza | [`legacy-component-research.md`](../../../EDControls/.claude/docs/legacy-component-research.md) |

---

### 🟢 Kde pokračovat dál

Tři realistické větve, nevylučují se (běží paralelně podle priority):

**1. Fáze A.2-D — Caché business logic** ⬅️ NEJVYŠŠÍ ZÁKAZNICKÝ DOPAD
Projít TRANSDEV/KORID suspended tickety v `ZastavkoveJrMhdAPI`: „spoj jede až do XY" template, custom komentář přebíjející auto-poznámku, NZ bug, skrytí atributů u hidden sloupců, vypnutí auto-poznámky bezbariérovosti, KORID přípojové řádky. Tabulka A.2-D níže + [`customer-requirements-zastavkovy-jr.md`](customer-requirements-zastavkovy-jr.md).

**2. Wave 1 — UserPreferences plné využití** (foundation hotová)
Per-user × IDFa nastavení nad hotovým `IUserPrefsService`: zoom, grid, recent files, instance picker default, splittery, recent colors. Klíčové API + storage popsaná v Caché Migrace § Wave 1 níže.

**3. EDFilter komponenta** (drobný rozsah, dotahuje L5/L6)
Nahradit ad-hoc agenda combo v `CacheTemplatePickerDialog` standardní deklarativní komponentou. Začít jen s `FilterCombo`, další typy (text, date, bool) podle reálné potřeby. Vzor:
```xml
<el:EDFilter Target="{Binding ElementName=TheList}">
    <el:FilterCombo Key="agendaCode" Label="Agenda"
                    ItemsSource="{Binding AgendaFilters}"
                    SelectedValuePath="Code" DisplayMemberPath="Label"/>
</el:EDFilter>
```
Po dokončení: refactor `CacheTemplatePickerDialog` na deklarativní XAML, odstranit `SelectedFilter` z VM.

**Co dělat až později (mimo aktuální focus):**
- Plná sada Filter polí (date, text, bool) — počkat na konkrétní dialog (YAGNI)
- Tier 3 EDInputForm refactor — `$lb()` OUT → JSON envelope, doplnění chybějících typů polí (Color, Font, Suggest, Text+Picker, ActionId Button). Detailní plán v `td-editor-migration-plan.md`.
- Tier 4 TD editor migrace na EDInputForm — Caché editor třídy per typ elementu, refactor 4 dialogů.

**Demo polish (kdykoli, malé položky):**
- Edison icon do okna, sample template „showcase" pro zastávkový JŘ
- Pořadí linek v pickeru, persistence aktivních instancí přes restart

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
| R1 | **Async-only Caché volání** — žádné synchronní `MLRest` v UI threadu, vždy `async Task` + `await` | Caché volání trvá 50–500ms; synchronní volání zamrzne UI. Musíme to chytit hned, ne retro-fit. |
| R2 | **Chyby přes `IPopupDisplayer` z EDControls** — žádný `MessageBox.Show` ani silent catch. Používat existující `EDControls.EDViewModel.IPopupDisplayer` (metody `Error(ex)`, `Confirm`, `Info`, `ConfirmClose`) přes DI nebo `EDViewModel` base. | Bez jednotné cesty se chyby v distribuci ztratí. `IPopupDisplayer` je firemní standard — už ho používá `EDViewModel.Command(...)` automaticky. |
| R3 | **CVar named IN + 4 OUT styly podle obsahu** — IN vždy CVar pojmenované klíče; OUT podle typu: CVar scalars (triviální), JSON v slotu (entita), JSON multi-slot (list), JSON envelope (composite, opt-in pro Tier 3). Plný standard, decision tree, příklady a checklist: [`EDControls/DOCS/01-wire-formats.md`](../../../EDControls/DOCS/01-wire-formats.md). | CVar je nativní zrcadlo Caché globálů (1 řádek per parametr), JSON pro strukturovaná data dává typovou bezpečnost přes `record` typy v .NET. Asymetrie není kompromis — odráží přirozenost obou stran. |
| R4 | **ViewModely bez WPF referencí** — žádný `using System.Windows` ani `MessageBox.Show` v `*ViewModel.cs` | MVVM princip, testovatelnost, možnost pozdějšího portu/unit testů. Dialogy přes `IPopupDisplayer` (R2). |
| R5 | **Template versioning je prerekvizita persistence** — dokud není `TemplateMigrator` + version field v JSON, neukládá se nic do Caché | Bez migrace se nové šablony nespustí na starším buildu → zničené ranní otevření souboru. Nelze to retro-fit po nasazení. |

**Předpoklad:** aplikace poběží nad Caché — není cíl fungovat offline, ne každá API potřebuje in-memory fallback. Fallback se řeší jen tam, kde to má konkrétní business smysl (např. poslední otevřené soubory v paměti během session).

**Kde jsou pravidla ověřitelná:**
- R1: review PR → `grep "MLRest\." *ViewModel*.cs` ukáže synchronní volání
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
| A2-D4 | #7955 | TRANSDEV | **Bug** — výběr spoje v UI jen prvním číslem (Studio JŘ, ne přímo TD) | |
| A2-D5 | #7161 | TRANSDEV | Skrýt „x — zastávka na znamení" pokud sloupec s `x` je hidden (kontrakt TD ↔ Caché — rozhodnutí kde se filtruje) | |
| A2-D6 | #7161 | TRANSDEV | Vypnutí auto-poznámky „všechny spoje s bezbariérovým vstupem" — parametr v `GetParameters` + checkbox v PrintDialog | |
| A2-D7 | #11300 | KORID | Skrýt prázdné přípojové řádky — vyžaduje **novou** doménovou strukturu `Pripoj` v `GetData` + nový element `ConnectionsListElement` | |

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
| W1-5 | Recent Colors — integrace s color pickerem (po B2) | |
| W1-6 | Template thumbnails — 100×140px PNG při uložení šablony | |

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
| .NET UI | ColorPicker (B2) čte swatches z `ICompanyPalette` |
| .NET `SectionTemplateManager` | Nahrazen `ISectionTemplateCatalog` |
| appsettings.json | **Smazat `agendas[]`** — přesouvá se do Caché |

**Risks:**
- Backward compat: existující šablony musí pokračovat s hardcoded defaults pokud Caché nemá `ElementDefaults` naplněné. Mitigace: `IElementDefaults.GetDefaults` vrací merge (Caché přepíše C# defaults).
- Migration: per-machine `section-templates/*.json` se musí jednorázově importovat do Caché. Mitigace: startup detekce → upload dialog "Nahrát šablony z disku?".

**Výstup:** žádné agenda/styl info v kódu → při přidání nové agendy se restart už neřeší.

| Krok | Co | Stav |
|---|---|---|
| W2-1 | Caché `AgendaRegistryAPI` + `.NET IAgendaRegistry` — migrace `appsettings.json` agendas | |
| W2-2 | Caché `CompanyPaletteAPI` + integrace s ColorPicker (B2) | |
| W2-3 | Caché `ElementDefaultsAPI` + `IElementDefaults` konzumované v elementech | |
| W2-4 | Caché `SectionTemplateAPI` + `ISectionTemplateCatalog` + jednorázová migrace lokálních souborů | |

---

#### 🟡 Wave 3 — Template storage

**Základní CRUD hotový** (commit `5ac9dd0`, 2026-05-15). Šablony se ukládají do Caché přes `ALVA.TemplateDesigner.API.Templates` s 5 metodami (`ListTemplates`, `LoadTemplate`, `SaveTemplate`, `DeleteTemplate`, `RenameTemplate`). UI v `CacheTemplatePickerDialog` (po L5 migraci přes EDList) + `SaveToCacheDialog`. JSON wire formát (R3), per-IDFa storage.

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
| W3-1 | Caché `ALVA.TemplateDesigner.API.Templates` (CRUD: List, Load, Save, Delete, Rename) | ✅ 5ac9dd0 |
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

#### Obecný layout-reservation pattern u elementů

Aktuálně když element potřebuje rezervovat místo pro volitelný vizuální prvek (šipka směru u `StopListElement`), řeší se to ad-hoc. Do budoucna zvážit deklarativní mapu "pokud property X=true → rezervuj Y mm vlevo/vpravo". Vyřeší konzistentně napříč všemi elementy.

#### Collapsible groups v properties panelu

| Krok | Co | Stav |
|---|---|---|
| B0-10 | **Collapsible groups** v `PropertiesPanel` — accordion s `GroupDescriptor`, sbalené skupiny zobrazují summary řádek, stav sbalení per element type do `UserPreferences.GroupExpanded` | |

#### WPF Color Picker — fix blokující bug

`WinForms ColorDialog` minimalizuje Properties panel při kliknutí OK (TODO v `PropertiesPanel.xaml.cs:555`). Vlastní WPF color picker s palette z `UserPreferences.RecentColors`.

| Krok | Co | Stav |
|---|---|---|
| B2-1 | WPF color picker control (`ColorPickerPopup.xaml`) — swatches, hex, alpha | |
| B2-2 | Nahradit všechna volání `ColorPickerHelper.PickColor()` novým controllem | |
| B2-3 | Synchronizovat `RecentColors` s `UserPreferencesService` (Wave 1) | |

---

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

| # | Položka | Náročnost | Popis |
|---|---------|-----------|-------|
| g | Inline text editing na canvasu | Velká | Dvojklik na text → inline editace přímo na canvasu (bez otevření dialogu) |
| h | Cross-section rubber band selection | Střední | Aktuálně funguje jen v aktivní sekci; umožnit výběr přes sekce s přepočtem souřadnic |
| i | Align to page center | Malá | Zarovnat element na střed stránky (horizontálně nebo vertikálně) |
| k | Page Break element | Malá | Explicitní element "zalomit stránku zde" — alternativa k oddělení přes sekce |
| l | XLSX export | Velká | Tabulky → Excel; vyžaduje ClosedXML nebo NPOI |
| m | Ruler na canvasu | Střední | Horizontální + vertikální pravítko v mm podél hran canvasu; zvýrazňuje rozsah vybraného elementu |
| n | `LayoutGridDialog` (B0-7) | Střední | Rozmístit vybrané elementy do řádku/sloupce/mřížky se zadanou mezerou; výstup = `CompositeCommand` |

**Grouping v TableElement** — seskupení řádků dynamické tabulky podle hodnoty pole (`GroupHeader`, `GroupFooter`, `Summary` řádky).

**Computed fields** — pole vypočítaná na C# straně ze surových dat před předáním šabloně (`CELKOVA_CENA = CENA * MNOZSTVI`).

**Min/max počet řádků dynamic tabulky** — `MinRows`, `MaxRows` properties; engine ořízne/doplní.

**Hranice mezi buňkami tabulky** — vnitřní cell borders (h/v čáry mezi buňkami).

**Zarovnávání napříč sekcemi** — alignment s referenčním bodem = stránka, ne sekce.

**Vnořené datasource** — `_rows` uvnitř `_rows` (faktura → položky → daně); TableElement s child kolekcí.

---

### 🔵 Před produkcí (odloženo)

| Co | Popis |
|---|---|
| Full migration pipeline | Po H-18/H-19 rozšířit na každou breaking change (ne jen 2 verze) |
| CI/CD linting šablon | Ověřit šablony před deployem (Caché job) |
| Versioning šablon v Caché | Wave 3 CRUD už běží; produkce potřebuje: kdo kdy co změnil, rollback (W3-5/W3-6/W3-8) |
| Autor, datum, kategorie, popis | Wave 3 dnes drží Author/CreatedAt/ModifiedAt/Version v metadatech; chybí kategorie + popis v UI |

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

# 🗄️ Archiv — hotové kroky

> Vše pod touto čarou je už implementováno. Horní část roadmap obsahuje jen to, co je potřeba dělat.

## v1.0 demo balíček ✅ (2026-05-15)

Tři commity tvoří kompletní release readiness pro testovací nasazení — zákazníkovi lze ukázat funkční end-to-end workflow pro zastávkový JŘ MHD. Detaily viz [`changelog.md`](changelog.md) 2026-05-15 entry.

| # | Co | Commit |
|---|---|---|
| R1 | **Agenda + Linka picker UX** — `ChangeAgendaDialog` se 3 entry pointy (menu / Ctrl+Shift+A / klik na status chip); `InstancePickerDialog` (multi-výběr linek, search, batch); Caché `GetAvailableInstances` iteruje skupinu linek; permanentní status indikátor (zelený agenda chip + modrý instance chip s ◀▶ + window title `[MHD]` prefix) | `82b68be` + `fd25008` |
| R2 | **Template storage v Caché** — třída `ALVA.TemplateDesigner.API.Templates` (5 metod, per-IDFa, JSON wire); `CacheTemplateService` async wrapper; `CacheTemplatePickerDialog` (správce: list + filtr + search + Open/Rename/Delete); `SaveToCacheDialog`; menu „Soubor → Otevřít z Caché... / Uložit do Caché [jako...]" + „Nástroje → Šablony v Caché..."; Caché namespace refactor `API/Agendas/Forms`; GENERAL agenda fallback | `5ac9dd0` |
| R4 | **Polish** — status bar chips, ConfirmUnsavedChanges integrace, error reporting přes IPopupDisplayer, BuildAllPreviewDataPages bugfix pro šablony bez agendy, debug logging do `^jm` (po debugu odstraněno) | průběžně |

**Performance:** 600 stránek MHD JŘ za ~9s (5.2s fetch + 3.6s render, ~15ms/stránka) — top decile vs commercial reporting (Crystal Reports 30-60s, SSRS 20-45s, JasperReports 15-30s).

**Architektonická pravidla R1–R5 (async/IPopupDisplayer/JSON/MVVM/versioning) dodržena ve všech 3 commitech.**

## Fáze A.2: Zákaznické úpravy zastávkového JŘ ✅ (2026-05-11 → 2026-05-12)

Suspended tickety od DPKV (#10660) a TRANSDEV (#7937, #7959, #9036) z helpdesku. Detail viz [`customer-requirements-zastavkovy-jr.md §G`](customer-requirements-zastavkovy-jr.md).

| Krok | Tiket | Co | Datum |
|---|---|---|---|
| A2-A1 | #10660 | StopList: `DirectionArrowPosition` (Left/Right asymetricky) | 2026-05-12 |
| A2-A2 | #10660 | StopList: `MarkersPosition` (Before/After name) + auto-fit marker sub-cell | 2026-05-12 |
| A2-A3 | #10660 | StopList: `CurrentStopBgColor` + odstranění master toggle `HighlightCurrentStop` | 2026-05-12 |
| A2-A4 | #10660 | StopList: per-column header font (verifikace, již plně podporováno) | 2026-05-12 |
| A2-B1 | #10660 | DepartureGrid: per-line font v dvouřádkovém headeru (`HeaderLine2Style`) | 2026-05-12 |
| A2-B2 | #10660 | DepartureGrid: fix nestejného podtržení (`DrawTextDecorations` na header) | 2026-05-12 |
| A2-B3 | #10660 + #9036 | DepartureGrid: časová osa přes půlnoc + extended hours + „Roztáhnout na výšku" | 2026-05-12 |
| A2-C1 | #10660 | NotesLegend: DB-driven barvy poznámek (`PoznamkaSpoj.BarvaZnakuRGB/BarvaPozadiRGB`) | 2026-05-12 |
| A2-C2 | #10660 | NotesLegend: konfigurovatelná hlavička (verifikace, již podporováno) | 2026-05-12 |
| A2-E1 | #8772 | VisibilityExpression placeholdery — verifikace funkčnosti | 2026-05-11 |
| A2-A5 | #7937 | ⏸️ StopList per-row indent — odloženo (chybí Caché data flow, mimo TD scope) | — |
| A2-A6 | #7959 | 🔍 Filtr neobsluhovaných zastávek — již implementováno v Caché, vyžaduje verifikaci | — |
| A2-B4 | #8828 | ⏸️ Per-sloupek zastávky — odloženo (Caché datový průzkum + business decision) | — |

## Fáze B0.5 — Code Hygiene Sprint

| # | Co | Hotovo |
|---|---|---|
| H-1 | Smazat `ShowZkraceneOznaceniLinky` — ghost property v `DepartureGridElement` (nikde se nečetla) | 2026-04-21 |
| H-2 | `async void` → `async Task` + `AsyncRelayCommand` (EDControls) + `CommandAsync` helper; Open/Save/SaveAs + bonus GenerateCommand | 2026-04-21 |
| H-3 | `ElementTypes` statické konstanty v `Models/ElementTypes.cs` — nahrazeno ~30 magic strings v JsonDerivedType, `CreateElementByType`, `GetDefaultElementSize` a toolbar menu | 2026-04-21 |
| H-4 | JSON wire format verify — průzkum potvrdil 100% čistý JSON (Caché `obj.%ToJSON()` → .NET `JsonNode.Parse`); opraven zastaralý docstring `GetData` v `AgendaAPI.cls` + `cache-api.md` + přidána sekce "Wire formát pravidlo (R3)" | 2026-04-21 |
| H-5 | Silent catch v `PropertiesPanel.xaml.cs` color picker seed nahrazen za `SKColor.TryParse` — odstranil try/catch jako control flow | 2026-04-21 |
| H-6 | Nový `.claude/rules/new-element-checklist.md` — 10-kroková autoritativní reference (třída, ElementTypes, JsonDerivedType, 2 switches, toolbar menu, paleta, dynamická výška, GetSummary, docs); konsolidace z `mvvm-patterns.md` | 2026-04-21 |
| H-7 | `MessageBox.Show` → `IPopupDisplayer` — 5 VM (AiTemplateAssistant, TableEditor, ElementProperties, MainWindow, PdfImport); PdfImport odstranil 3 callback properties; YesNoCancel kept s TODO (IPopupDisplayer nemá ekvivalent); Warning → Info (WPF rozdíl byl jen ikona) | 2026-04-21 |
| H-8 | Extract `Controls/TemplateTreePanel/` — ~150 řádků ze `MainWindow.xaml.cs` přesunuto do samostatného UserControlu; `MainWindow` jen volá `TreePanel.Rebuild()`; smazán orphan `Windows/TemplateTreeDialog/` (nikdy se neinstanciovalo) | 2026-04-21 |
| H-9 | Extract `Windows/SaveSectionTemplateDialog/` — inline WPF konstrukce dialogu (~60 řádků StackPanel+TextBox+Button) z `MainWindowViewModel` přesunuta do Window+ViewModel+XAML dle závazného standardu; `SaveSectionTemplate(Section, string)` nyní čistá save logika | 2026-04-21 |
| H-10 | **N/A** — `TemplatePickerDialog` v kódu neexistoval. Template picker = `OpenFileDialog` (standardní Windows), section template picker = menu položky v "Add Section" dropdown. Žádný samostatný dialog k extrakci. Uzavřeno bez změn. | 2026-04-21 |
| H-11 | ⏭️ **Skip do potřeby** — `PropertyDescriptor.VisibleWhen` predikát. Aktuální ad-hoc skrývání v dialozích stačí, žádný blocker. | 2026-04-30 |
| H-12 | ⏭️ **Skip do potřeby** — `SharedPropertyDescriptors` helper. Duplikace deskriptorů akceptovatelná za stávající cenu. | 2026-04-30 |
| H-13 | ⏭️ **Skip do potřeby** — rozšíření error flow nad rámec `IPopupDisplayer`. Strukturované logování / telemetrie přidat až bude konkrétní požadavek. | 2026-04-30 |

## Sjednocení patternů + UX vyladění ✅ (2026-04-30 → 2026-05-01)

Kontinuální polish nad rámec původního B0.5 scope, motivovaný přípravou na 1:1 ladění zastávkového JŘ:

| Co | Stručný popis |
|---|---|
| **Sjednocené Header/Data zóny v panelu** | Princip „rychlý přístup × kompletní cascade" napříč StopList/DepartureGrid/NotesLegend/TableElement; helper `GetCellZoneBaseColorDescriptors()` |
| **Per-template editor preferences** | `Template.EditorPreferences` (grid + snap) — designové prostředí přežije reload |
| **Click-vs-drag threshold** | Klik na element nespustí soft-snap (4px threshold) |
| **ImageElement ScaleMode** | Renderer honoruje Fit/Fill/Stretch/None + tlačítko „Přizpůsobit poměru obrázku" |
| **Sub-pixelové rámečky** | Adaptive `RenderBorder` — pod 1px AA fractional, nad 1px pixel-snap |
| **Descriptor sort by group** | Stable sort v PropertiesPanel — bez duplicitních hlaviček |
| **Border v „Obecné" group** | Konzistentní rychlý přístup k základním vlastnostem |
| **DataRowHeight hoist** | Z duplicitních deklarací do `ScheduleElementBase` |
| **Úklid mrtvého UI** | Smazán `LineSpacing` z `CellStyleEditor` (CellRenderer ho ignoroval) |
| **Dokumentace stylingových patternů** | `mvvm-patterns.md` — Pattern 1 (single-zone) vs Pattern 2 (dual-zone) |

## Specializované JŘ elementy — společný základ ✅ (2026-03-18)

| Krok | Co |
|---|---|
| 1 | `ScheduleElementBase` — abstraktní základ (indexy, grouping, společné properties) |
| 2 | `StopListElement` + `DepartureGridElement` + `NotesLegendElement` — zastávkový JŘ (MHD) |
| 3 | Caché `ZastavkoveJrMhdAPI` — `GetData`/`GetMockData` + wire formát (ZASTAVKY, SKUPINY_DNU, ODJEZDY, POZNAMKY) |
| 4 | `SkiaHeightCalculator` — měření výšky JŘ elementů |
| 5 | `PrintDialog` — parametry + export reálných dat z Caché (`GetData`) + multi-data PDF |
| 6 | `CacheAgendaProvider` — ParseMockData vnořená pole, GetData, BuildRealData |

## Fáze A: Odladění MHD zastávkového JŘ ✅ (2026-05-11)

Cíl: hotová první kompletní verze šablony zastávkového JŘ a její review s kolegy.

> **Detailní architektonický dokument:** [`schedule-elements-architecture.md`](schedule-elements-architecture.md)

| Krok | Co | Stav |
|---|---|---|
| A | Hotová 1:1 šablona zastávkového JŘ + review s kolegy → seznam úprav v `customer-requirements-zastavkovy-jr.md` (= Fáze A.2) | ✅ (2026-05-11) |

### Poučení z implementace zastávkového JŘ — co vzít v potaz pro další JŘ typy

| Téma | Popis |
|---|---|
| **Jeden element → tři elementy** | Původní plán počítal s jedním `StopScheduleElement`. Ve skutečnosti se rozpadl na 3 nezávislé elementy — lépe odpovídá filozofii designeru (uživatel skládá ze stavebních bloků). Počítat s tím od začátku u dalších JŘ typů. |
| **Synchronní GetData** | `GetData` běží synchronně na UI thread — pro malé linky OK, pro velké může zamrznout UI. Vozový JŘ bude mít víc dat → async loading s progress barem bude nutný. Viz pravidlo R1. |
| **SKTypeface/SKPaint alokace** | ✅ Opraveno — `ScheduleElementBase` cachuje `SKTypeface` přes statický `ConcurrentDictionary<string, SKTypeface>`; `SKPaint` se vytváří a ukládá per instance cache. |
| **Caché business logika je rozsáhlá** | ZastavkoveJrMhdAPI má ~1200 řádků. Počítat s tím v odhadu pro další JŘ typy. |
| **Definovat layout před implementací** | Aby se neopakovala situace, kde se plán změní v průběhu. |

### Fáze A — hotové kroky

| Krok | Co | Stav |
|---|---|---|
| 7 | Per-column styling framework — CellStyle/ColumnVisualStyle, dva taby v DepartureGroupEditorDialog, InlineTextStyle pro minuty/anotace | ✅ (2026-03-21) |
| 7a | StopListElement — per-column styling analogicky k DepartureGrid (CellStyle pro záhlaví/data sloupců zastávek) + generalizace shared helpers do ScheduleElementBase + ColorPickerHelper + ActionId routing | ✅ (2026-03-23) |
| 7b | Ověření parametrů šablony — funguje přepsání element properties z template parameters (CASOVA_OSA, HODINY_OD, HODINY_DO, TISK_PROJETYCH_ZAST, TISK_SIPKY); fix merge parametrů do real data exportu; auto-open PDF; odebrání BAREVNE_ODLISENI + TISK_TARIFU | ✅ (2026-03-23) |
| 8a | Sjednocení textových nastavení — `TextStyle` (nullable POCO, CSS cascade) + `ResolvedTextStyle` (SkiaSharp helpery). Migrace: TextElement, TableElement, ScheduleElementBase + 3 JŘ elementy, PdfImport, CacheAgendaProvider. Breaking JSON change (flat props → `style`/`headerStyle`/`dataStyle`) | ✅ (2026-03-24) |
| 8b | Proporcionální škálování šablony + UX vylepšení — `ScaleTemplateCommand`, `ApplyScale()` na všech elementech, redistribuce výšek sekcí, výchozí nastavení nových elementů, `HourColumnPerGroup` | ✅ (2026-03-24) |
| 8c | AI-enhanced PDF import — dvoustupňový pipeline (heuristiky → AI enrichment); `AiImportEnricher` s kontextovými prompty; detekce rámečků, loga placeholder, margin fix, TextElement alignment fix | ✅ (2026-03-25) |
| 8d | AI Vision import pipeline — `AiVisionImportService` (composable prompty), `AiGuidedScheduleBuilder` (PdfPig geometrie), `AiVisionImportPipeline` (orchestrace); per-column font wrapping; section headers auto-hide; CellStyleVM font defaults; shared HttpClient | ✅ (2026-04-03) |

## Fáze B0 — UX Sprint ✅ (2026-04-20)

| # | Co |
|---|---|
| B0-1 | Strikethrough na TextStyle + descriptor; oprava tichého nefungování Underline |
| B0-2 | Více barcode typů — EAN-13, EAN-8, PDF417, DataMatrix; konsolidace palety |
| B0-4 | Precision fix — `MmMath.Round(0.1mm)`; resize end, ScaleTemplate, DistributeH/V, properties panel |
| B0-5 | DistributeH/V: přesný gap — zaokrouhlit, poslední element absorbuje zbytek |
| B0-6 | Normalizovat pozice (Ctrl+Shift+N) — dialog s krokem, live preview, CompositeCommand undo |
| B0-8 | Properties panel float formát — `"0.0#"` pro mm pole, round on commit |
| B0-9 | Grid size v UI — ComboBox 1/2/5/10mm v toolbaru, DesignerState.GridSize property |
| B0-11 | Inline validace — červený rámeček + tooltip místo textu pod polem |
| B0-12 | Copy Style / Paste Style (Ctrl+Shift+C/V) — StyleClipboard, kopíruje TextStyle + BorderColor/Width + BackgroundColor |
| B0-13 | Kontextový format mini-toolbar — B/I/U/S + L/C/R/J + ▲/▬/▼ nad properties panelem |
| B0-14 | Rotation property na TemplateElement — float, stupně, JSON serializace |
| B0-15 | Render rotace — canvas.Save/RotateDegrees(cx,cy)/Restore + IsRotated bool helper |
| B0-16 | Hit-testing rotovaných elementů — inverzní rotace test pointu kolem středu |
| B0-17 | Resize handles zakázány pro rotované elementy + rotation badge (angle label) |

---

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
