# EDISON2 - poznámky pro vývojáře

Dvouvrstvá WPF aplikace: .NET UI ↔ Caché (InterSystems) back-end přes HTTP (MLRest).

Aplikační projekty buildí lokálně (`bin/Debug/`, `bin/Release/`). Do `C:\Edison\` se
dostávají pouze přes `dotnet publish` s profilem „SingleFileCEdison"
(`Properties/PublishProfiles/`) - bundle bez volných NuGet DLL. Výjimky drží sdílený
`SingleFilePublish.targets` v rootu (importují ho oba aplikační csproj): **`EDControls.dll`
je vedle exe volný soubor** sdílený oběma aplikacemi, aby šla nasadit samostatně (nové
pole v EDInputFormu → do produkce jde DLL + exe, který ho používá; druhý exe se
nepřebuildí a novou DLL převezme). Cena: veřejné API EDControls a klíče theme resource
měň **aditivně** - viz `EDControls/CLAUDE.md`.

Týmž profilem se vydává i **EDControls samostatně** (bez buildu aplikací). U knihovny
publish jinak vyplivne celý uzávěr závislostí (MLRest, EDLicence, EDCore, Newtonsoft), který
by v `C:\Edison\` přepsal soubory legacy repa - proto `PublishOwnAssemblyOnly` propustí jen
vlastní assembly a nezávislá kontrola počtu publish **shodí**, než něco zapíše.
`C:\Edison\` je staging: do produkce jde až explicitní klik na položku v Compileru
(`EDISONMAIN2/docs/UPDATE_FLOW.md`).

Nové features pokrývej xUnit testy a Caché `%UnitTest` testy - viz sekce **Testy**.

## Struktura repozitáře

| Složka | Role |
| --- | --- |
| `EDISONMAIN2/` | Hlavní WPF shell. Vlastní `CLAUDE.md`. |
| `EDControls/` | Sdílená WPF knihovna + Caché strana. Vlastní `CLAUDE.md` (+ per-komponenta). |
| `TemplateDesigner/` | WYSIWYG návrhář tiskových šablon. Vlastní `CLAUDE.md`. |
| `ServiceGtfsRt/` | ASP.NET Core Windows Service - GTFS Realtime feed. Vlastní `CLAUDE.md`. |
| `EDControls.Tests/`, `EDISONMAIN2.Tests/` | xUnit testy (NSubstitute). |
| `EDTesting/` | Caché test framework - `MLTestCase` + `TestRunner`. |
| `tools/` | PowerShell skripty (`run-cache-tests.ps1`). |
| `.husky/` | Husky.NET git hooks (auto-install při `dotnet restore`). |
| `MLRest.md` | Kompletní reference Caché architektury (namespace, makra, protokol). |

## Pravidla pro CLAUDE.md

**Krátké a výstižné**: root ≤ ~150 řádků, komponentový ≤ ~60. Při překročení škrtej.

- **Patří sem**: neodvoditelné invarianty, wire kontrakty, pasti, workaroundy, netriviální
  WHY, pořadí volání, na kterém záleží.
- **Nepatří sem**: opisy API a signatur (jsou ve zdrojáku), kopie kódu, tutoriály jazyků,
  changelogy / roadmapy / ✅ stavy (patří do commitů), dlouhé usage příklady
  (odkaž na referenční implementaci).
- Když měníš chování popsané v CLAUDE.md, **aktualizuj doc v témže commitu**; stale
  tvrzení maž bez milosti.
- Nový vnořený CLAUDE.md zakládej jen pro komponentu s vlastními neodvoditelnými
  kontrakty; jinak patří poznámka do nadřazeného.

## MLRest - použití v .NET

`MLRest` serializuje .NET hodnoty do Caché process-private globals (`^||ML("IN",...)`)
a odpověď (`^||ML("OUT",...)`) zpět. DLL odkazována přímo: `C:\Edison\MLRest.dll`.

Instance se nikdy nevytváří ve windows/ViewModelech - přístup přes `IMLRest`
a `EDServices.Current` (inicializuje `AppBootstrapper`). ViewModely mají `MLRest`
property z `EDViewModel`; ovládací prvky (`EDInputForm`, `EDList`) čtou
`EDServices.Current` samy; testy si vytvářejí vlastní `EDServices` s mockem.

**Vždy používej `CVar`** (idiomatický strukturovaný typ). `object[]` jen v legacy kódu
s existujícími numerickými subscripty - důvod zdokumentuj; v novém kódu nikdy.

Referenční volání (CVar IN/OUT, foreach indexovaných řádků): `EDControls/dotnet/EDInputForm/EDInputFormController.cs`.

Pasti wire formátu: subscripty jsou **stringy** (`cVar["1"]` → `^||ML("IN","1")`, ne
numerické 1); `CList` ↔ `$lb()`, čte se `$li(^||ML("IN","i"), n)` (1-based); v named
foreach přeskoč klíč `"ERR"` (MLRest metadata, ne data).

`QueryOrThrow` = `Query` + `ThrowIfError()` atomicky. Existuje dvakrát: na `IMLRest`
(ViewModely) a jako extension na konkrétním `MLRest` (legacy). Odděleně volej jen pro
graceful degradation. Binární data: `BinarySave(bytes)` → GUID, `BinaryLoad(guid)`
(server: `$$$SYSBinary(GUID)`, automaticky se uvolní).

## Kostra Caché metody

Kompletní reference `MLRest.md §2.2`. Minimum: `ClassMethod MyMethod(ByRef sys As
%String) As %Status`, tělo `$$$Init` → try `{ $$$FillParams ... }` catch
`{ $$$CatchErrRest }` → `ret ret`. Wire invariant: `$$$CatchErrRest` nastaví
`ret="1~message"` → `ThrowIfError()` vyhodí výjimku v .NET. Výstup buď named
(`s ^||ML("OUT","klíč")=...`) nebo indexovaný (`s ^||ML("OUT",$i(i))=$lb(...)` pro foreach).

**Výjimka `TemplateDesigner`:** `$$$Init` tam vědomě NEpoužíváme - metoda končí `ret $$$OK`
a `s err = ""` si musí nastavit sama. Tuhle kostru tam nekopíruj, pravidla jsou
v `TemplateDesigner/.claude/rules/cache-api.md`.

## Přidání nového okna

1. Složka `EDISONMAIN2/dotnet/Windows/MyWindow/` - `MyWindow.xaml(.cs)`,
   `MyWindowViewModel.cs` (dědí `EDViewModel`; bezparametrický ctor čte
   `EDServices.Current`).
2. V code-behind `DataContext = _vm = new MyWindowViewModel();` **před**
   `InitializeComponent()`.
3. Registrace v `EdisonMainViewModel`:
   `OpenMyWindowCommand = Command(() => WindowNavigator.Show(new MyWindow()));`
   (`Command(...)` = RelayCommand s error handlingem přes `PopupDisplayer`; výjimky mimo
   příkazy chytá globální handler). Vstupní data oken jdou konstruktorem.

Reference: šablona okna s footerem v `EDControls/dotnet/EDFooter/CLAUDE.md`; funkční
implementace `EDComponentAndViewModelTestWindow`; EDInputForm příklad
`EDControls/CACHE/ALVA/EDControls/EDInputForm/EDIFExample.cls`.

## Povinné nastavení nového aplikačního projektu

1. Merge resource dictionaries (`EDIcons.xaml`, `EDStrings.xaml`) - jinak
   `{StaticResource}` hodí výjimku za běhu.
2. `DispatcherUnhandledException` handler s `e.Handled = true` - jinak crash.
3. `EDAppearance.Initialize(app)` - fonty + theme (viz `EDControls/CLAUDE.md`).

Referenční implementace: `EDISONMAIN2/dotnet/Startup/App.xaml(.cs)`.

**Vzhled vždy přes sdílené tokeny/resource, nikdy hardcoded literál:**

- **Barvy** → `EDTheme.*` přes `{DynamicResource}` (jinak nereagují na dark mode); chybí
  token → doplň do `EDTheme.xaml` + `EDThemeDark.xaml`. Výjimka jen datové/dekorativní/
  externí barvy (WebView2 CSS, WinForms dialog).
- **FontSize** → `EDTheme.FontSize.*` přes `{DynamicResource}` (jinak nerespektují user
  škálování fontu přes `FontService`); nová velikost = nový token.
- **Běžné UI stringy** (OK/Storno/Ano/…) → `{StaticResource EDStrings.*}`.
- **Ikony** → `{StaticResource EDIcons.*}`, definice jen v `EDIcons.xaml`.
- Lokální `Style` na themovaném controlu **musí mít `BasedOn`** (viz `EDControls/CLAUDE.md`).

## Persistence stavu

- **Per-machine JSON** - `LocalPrefs` (`%APPDATA%\m-line\EDISONMAIN2\local.json`),
  přístup přes injektovaný `ILocalPrefs` (composition root `AppBootstrapper`). Ukládá
  `LastIdfa`, `LastLoginName`, `RememberedPassword` (DPAPI blob - předvyplní se jen
  když jméno v blobu == `LastLoginName`), `TryWindowsAutoLogin`, `MiniAppLayouts`,
  `ShowWindowsAsDialogs` (zatím no-op). Nové preference = nullable property; chybějící
  klíče zůstanou null (kompatibilní upgrade).
- **Per-user × IDFa (Caché)** - stav oken automaticky přes `EDViewModel` /
  `WindowStatePersistence`; detaily v `EDControls/dotnet/EDViewModel/CLAUDE.md`.
- **Cross-process (font, theme)** - `%APPDATA%\m-line\common\`; viz `EDControls/CLAUDE.md`.

Spouštění legacy modulů (EDISONMAIN2API, ACTIVITY zámek): `EDISONMAIN2/CLAUDE.md`.

## Testy

- **.NET**: `dotnet test EDISON2.sln` (xUnit + NSubstitute).
- **Caché**: test class dědí `ALVA.EDTesting.MLTestCase` (cleanup `^||ML` +
  `SetupSys(.sys)` pro `$$$FillParams` boilerplate), metody `TestXxx()`. Běh
  `pwsh -File tools/run-cache-tests.ps1 [-Package ...]`.
  Nový package mimo default list skriptu (EDISONMAIN2/EDControls/TemplateDesigner
  Tests) tam přidej, jinak ho pre-push hook nepokryje.
- **Nasazení `.cls` na server dělá hook sám** (`.claude/hooks/compile-objectscript.sh`,
  PostToolUse na Edit/Write) - upload + compile, chyby na stderr. `cache_compile` (MCP)
  volej jen na to, co hook nepokryje (dávka, změna mimo agenta); **ne na týž soubor po
  editaci** - dvojí nasazení si navzájem přepisuje výsledek.
- **Pre-push hook** (`.husky/pre-push`): change-aware podle pushovaného rozsahu - sadu
  pustí jen když se změnily její zdroje (`.cs`→.NET, `.cls`→Caché); Caché zúží na dotčené
  package, ale změna v sdíleném `EDControls`/`EDTesting` spustí všechny. Souhrn selhání
  `.husky/last-test-run.log`. Bypass `git push --no-verify` jen nouzově. Onboarding:
  `dotnet restore` v rootu aktivuje hooks automaticky.
- Caché testy potřebují dev server `192.168.16.1:57770` (VPN); user z `$env:USERNAME`
  (override `$env:CACHE_USER`).

## Typografie

**Nepoužívat dlouhou pomlčku** (em dash, U+2014) - v kódu, komentářích, UI textech,
dokumentaci i odpovědích vždy běžná pomlčka „-". Pravidlo znak záměrně nezobrazuje, aby
šlo hlídat grepem. (Rozhodnutí Jan 2026-07-28; v TemplateDesigneru hlídají UI labely
drift testy. Root `CLAUDE.md` pročištěn 2026-07-29; `EDControls`/`EDISONMAIN2` doc soubory
ještě ne.)

## Komentáře

Komentáře v `EDISONMAIN2/` a `EDControls/` jsou **česky** (výjimka: stock VS šablony).
Defaultně nekomentovat; přidávej jen netriviální WHY (skrytá invarianta, workaround
bugu) a sémantiku legacy systému (Caché global, ACTIVITY zámek, licenční flag).
Nekomentovat WHAT ani reference na task/commit/roadmapu - `PLAN.md` a „feature #N"
odkazy na plánovací položky jsou zakázané (`PLAN.md` je stale; číslované Caché user
flagy jako feature #17/#23 jsou naopak doménová sémantika a zůstávají). Délka 1 řádek,
max 2–3; multi-paragraph `<remarks>` je špatně.
