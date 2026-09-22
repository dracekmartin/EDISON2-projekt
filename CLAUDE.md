# EDISON2 - poznámky pro vývojáře

Dvouvrstvá WPF aplikace: .NET UI ↔ Caché (InterSystems) back-end přes HTTP (MLRest).

Aplikační projekty buildí lokálně (`bin/Debug/`, `bin/Release/`). Do `C:\Edison\` se
dostávají pouze přes `dotnet publish` s profilem „SingleFileCEdison"
(`Properties/PublishProfiles/`) - bundle bez volných NuGet DLL. Výjimky drží sdílený
`SingleFilePublish.targets` v rootu (importují ho EDISONMAIN2, EDUPDATE2, TemplateDesigner
i EDControls): **`EDControls.dll` je vedle exe volný soubor**, aby šla nasadit samostatně
(do produkce jde DLL + exe, který ji používá; druhý exe se nepřebuildí a novou DLL převezme).
Cena: **breaking change API znamená publikovat i konzumenty** (DLL nikdy samotnou) - viz
`EDControls/CLAUDE.md`.

Týmž profilem se vydává i **EDControls samostatně**: `PublishOwnAssemblyOnly` propustí jen
vlastní assembly (plný uzávěr závislostí - MLRest, EDLicence, EDCore, Newtonsoft - by
v `C:\Edison\` přepsal soubory legacy repa) a nezávislá kontrola počtu publish **shodí**,
než něco zapíše. `C:\Edison\` je staging: do produkce jde až explicitní klik v Compileru. Seznam binárek
k distribuci při vydání shellu (včetně těch z repa EDISON): `docs/deploy.md`.

## Struktura a pravidla pro CLAUDE.md

`EDISONMAIN2/`, `EDControls/`, `TemplateDesigner/`, `ServiceGtfsRt/` a `EDUPDATE2/`
(klientský updater nového shellu) mají **vlastní `CLAUDE.md`**, EDControls navíc per-komponenta. `EDTesting/` = Caché test framework
(`MLTestCase` + `TestRunner`), `docs/mlrest.md` = kompletní reference Caché architektury,
`.husky/` = git hooks (auto-install při `dotnet restore`).

**Krátké a výstižné**: root i project doc (`EDISONMAIN2/`, `EDControls/`,
`TemplateDesigner/`, `EDUPDATE2/`) ≤ ~150 řádků, komponentový ≤ ~60. Při překročení škrtej.

- **Patří sem**: neodvoditelné invarianty, wire kontrakty, pasti, workaroundy, netriviální
  WHY, pořadí volání, na kterém záleží.
- **Nepatří sem**: opisy API a signatur, kopie kódu, tutoriály, changelogy / roadmapy /
  ✅ stavy (patří do commitů), dlouhé usage příklady (odkaž na referenční implementaci).
- Když měníš chování popsané v CLAUDE.md, **aktualizuj doc v témže commitu**; stale
  tvrzení maž bez milosti. Nový vnořený CLAUDE.md zakládej jen pro komponentu s vlastními
  neodvoditelnými kontrakty; jinak patří poznámka do nadřazeného.
- **Nový samostatný doc nezakládej** - neodvoditelná poznámka patří do `CLAUDE.md`. Co už mimo
  něj žije, sedí v `docs/` (malými) kořene nebo projektu, jméno kebab-case česky bez číselných
  prefixů. `docs/analyzy/` a `.claude/reviews/` jsou datované výstupy skillů - **neudržují se**,
  ber je jako snímek ke dni vzniku.

## MLRest - použití v .NET

`MLRest` serializuje .NET hodnoty do Caché process-private globals (`^||ML("IN",...)`)
a odpověď (`^||ML("OUT",...)`) zpět. DLL odkazována přímo: `C:\Edison\MLRest.dll`.

Instance se nikdy nevytváří ve windows/ViewModelech - přístup přes `IMLRest` a `EDServices.Current`
(inicializuje `AppBootstrapper`): ViewModely mají `MLRest` z `EDViewModel`, controls (`EDInputForm`,
`EDList`) čtou `EDServices.Current` samy, testy si vytvářejí vlastní `EDServices` s mockem.

**Vždy používej `CVar`** (idiomatický strukturovaný typ). `object[]` jen v legacy kódu
s existujícími numerickými subscripty - důvod zdokumentuj; v novém kódu nikdy. Referenční
volání (CVar IN/OUT, foreach indexovaných řádků): `EDControls/dotnet/EDInputForm/EDInputFormController.cs`.

Pasti wire formátu: subscripty jsou **stringy** (`cVar["1"]` → `^||ML("IN","1")`, ne
numerické 1); `CList` ↔ `$lb()`, čte se `$li(^||ML("IN","i"), n)` (1-based); v named
foreach přeskoč klíč `"ERR"` (MLRest metadata, ne data).

`QueryOrThrow` = `Query` + `ThrowIfError()` - na `IMLRest` i jako extension na `MLRest` (legacy;
deleguje na `MLRestAdapter`, překlad chyby žije na jednom místě). Odděleně jen pro graceful degradation.
**Souběžné requesty jen na klonech** (`With()`) - `RestStatus` je per-instance a čte se až po
`Query`, takže paralelní volání na téže instanci si stavy prohodí (spolknutá chyba / cizí
výjimka). Klon má stejný GUID (pro server tatáž session) a každý request běží ve vlastním
Caché procesu, takže si nešlapou ani po `^||ML`. Vzor: `RestCallRunner` (EDControls,
`dotnet/RestCallRunner.cs`). Binární data: `BinarySave`/`BinaryLoad` (API MLRest.dll, viz `docs/mlrest.md`).

## Kostra Caché metody

Kompletní reference `docs/mlrest.md §2.2`. Minimum: `ClassMethod MyMethod(ByRef sys As %String)
As %Status`, tělo `$$$Init` → try `{ $$$FillParams ... }` catch `{ $$$CatchErrRest }` →
`ret ret`. Wire invariant: `$$$CatchErrRest` nastaví `ret="1~message"` → `ThrowIfError()`
vyhodí výjimku v .NET. Výstup buď named (`s ^||ML("OUT","klíč")=...`) nebo indexovaný
(`s ^||ML("OUT",$i(i))=$lb(...)` pro foreach).

**Výjimka `TemplateDesigner`:** `$$$Init` tam vědomě NEpoužíváme - metoda končí `ret $$$OK`
a `s err = ""` si musí nastavit sama; pravidla v `TemplateDesigner/.claude/rules/cache-api.md`.

## Přidání nového okna

Složka `EDISONMAIN2/dotnet/Windows/MyWindow/` s `MyWindow.xaml(.cs)` a `MyWindowViewModel`
(dědí `EDViewModel`, bezparametrický ctor čte `EDServices.Current`). Neodvoditelné:
`DataContext = _vm = new MyWindowViewModel();` musí být **před** `InitializeComponent()`;
registrace v `EdisonMainViewModel` jde přes `Command(...)` (= RelayCommand s error handlingem
přes `PopupDisplayer`, výjimky mimo příkazy bere globální handler); vstupní data konstruktorem.

Reference: `EDComponentAndViewModelTestWindow`, footer v `EDControls/dotnet/EDFooter/CLAUDE.md`,
EDInputForm příklad `EDControls/CACHE/ALVA/EDControls/EDInputForm/EDIFExample.cls`.

## Povinné nastavení nového aplikačního projektu

Platí pro aplikace postavené na EDControls. Bez těchto tří to padá za běhu: merge
`EDIcons.xaml` + `EDStrings.xaml` (jinak `{StaticResource}` hodí výjimku),
`DispatcherUnhandledException` handler s `e.Handled = true` (jinak crash)
a `EDAppearance.Initialize(app)` (fonty + theme). Referenční implementace
`EDISONMAIN2/dotnet/Startup/App.xaml(.cs)`. Výjimka `EDUPDATE2` - záměrně bez EDControls
(vyměňuje je za běhu), povinný zůstává jen exception handler; viz `EDUPDATE2/CLAUDE.md`.
Každý aplikační exe (i EDUPDATE2) má navíc `<ApplicationIcon>` na sdílené
`EDControls/dotnet/EDIcons/cube_molecule.ico`: připnutá aplikace na liště a okno bez `Icon`
berou ikonu z exe, ne z okna, a bez ní ukážou výchozí ikonu .NET.

**Vzhled vždy přes sdílené tokeny/resource, nikdy hardcoded literál:** barvy `EDTheme.*`
i `FontSize`/`IconSize` **přes `{DynamicResource}`** (`StaticResource` nepřežije swap dark
mode ani user škálování fontu přes `FontService`), UI stringy `{StaticResource EDStrings.*}`,
ikony `{StaticResource EDIcons.*}` (definice jen v `EDIcons.xaml`). Chybí token → doplň,
barvu do `EDTheme.xaml` **i** `EDThemeDark.xaml`; výjimka jen datové/dekorativní/externí
barvy (WebView2 CSS, WinForms dialog). Lokální `Style` na themovaném controlu **musí mít
`BasedOn`** - detaily `EDControls/CLAUDE.md`.

## Persistence stavu

- **Per Windows-uživatele (JSON)** - `LocalPrefs` (`%APPDATA%\m-line\EDISONMAIN2\local.json`) přes
  injektovaný `ILocalPrefs` (composition root `AppBootstrapper`). Nové preference = nullable
  property (chybějící klíč zůstane null - kompatibilní upgrade). `RememberedPassword` je DPAPI
  blob, předvyplní se jen když jméno v blobu == `LastLoginName`. `ShellWindowBounds` je **jedna
  geometrie shellu i přihlašovacího okna** - před loginem není session, do které by šlo ukládat.
- **Per-user × IDFa (Caché)** - stav agendových oken automaticky přes `EDViewModel` /
  `WindowStatePersistence` (úložiště je vyměnitelné, viz `IWindowStateStore`); detaily
  v `EDControls/dotnet/EDViewModel/CLAUDE.md`.
- **Cross-process (font, theme)** - `%APPDATA%\m-line\common\`; viz `EDControls/CLAUDE.md`.

Spouštění legacy modulů (EDISONMAIN2API, ACTIVITY zámek): `EDISONMAIN2/CLAUDE.md`.

## Testy

- **.NET**: `dotnet test EDISON2.sln` (xUnit + NSubstitute).
- **Caché**: test class dědí `ALVA.EDTesting.MLTestCase` (cleanup `^||ML` + `SetupSys(.sys)`
  pro `$$$FillParams` boilerplate), metody `TestXxx()`. Běh `pwsh -File
  tools/run-cache-tests.ps1 [-Package ...]`; nový package mimo default list skriptu
  (EDISONMAIN2/EDControls/TemplateDesigner Tests) tam přidej, jinak ho pre-push hook nepokryje.
- **Nasazení `.cls` na server dělá hook sám** (`.claude/hooks/compile-objectscript.sh`,
  PostToolUse na Edit/Write) - upload + compile, chyby na stderr. `cache_compile` (MCP) volej
  jen na to, co hook nepokryje; **ne na týž soubor po editaci** - dvojí nasazení si přepisuje
  výsledek. ⚠️ **Úprava `.cls` mimo Edit/Write hook NESPUSTÍ** (hromadná změna skriptem): na
  serveru zůstane stará třída, u EDInputForm formulářů se to projeví jako „pole je prázdné
  a uložení nic neudělá" a **drift testy to nechytí, čtou `.cls` z repa**. Deploy pak vynuť
  `echo '{"tool_input":{"file_path":"<abs cesta>.cls"}}' | bash
  .claude/hooks/compile-objectscript.sh` (MCP `cache_compile` hledá zdroj v `C:\DEV\EDISON`).
- **Pre-push hook** (`.husky/pre-push`): change-aware podle pushovaného rozsahu - sadu pustí
  jen když se změnily její zdroje (`.cs`→.NET, `.cls`→Caché); Caché zúží na dotčené package.
  Produkční `.cls` ve sdíleném `EDControls`/`EDTesting` spustí všechny; testovací `.cls`
  (EDControls `Tests/`) jen svůj balíček - TD sada je nejdražší (~45 s). Souhrn selhání
  `.husky/last-test-run.log`; bypass `git push --no-verify` jen nouzově.
- Caché testy potřebují dev server `192.168.16.1:57770` (VPN); user z `$env:USERNAME`
  (override `$env:CACHE_USER`).

## Typografie a komentáře

**Nepoužívat dlouhou pomlčku** (em dash, U+2014) - v kódu, UI textech, dokumentaci i odpovědích
vždy běžná „-". Pravidlo znak záměrně nezobrazuje, aby šlo hlídat grepem; v TemplateDesigneru
hlídají UI labely drift testy.

Komentáře v `EDISONMAIN2/` a `EDControls/` jsou **česky** (výjimka: stock VS šablony).
Defaultně nekomentovat; přidávej jen netriviální WHY (skrytá invarianta, workaround bugu)
a sémantiku legacy systému (Caché global, ACTIVITY zámek, licenční flag). Nekomentovat WHAT
ani odkazy na task/commit/plánovací položku - plánovací docy a „feature #N" odkazy jsou zakázané;
číslované Caché user flagy (feature #17/#23) jsou naopak doménová sémantika a zůstávají.
Délka 1 řádek, max 2-3; multi-paragraph `<remarks>` je špatně.
