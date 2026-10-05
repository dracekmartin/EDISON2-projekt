# EDISON2 - poznámky pro vývojáře

Dvouvrstvá WPF aplikace: .NET UI ↔ Caché (InterSystems) back-end přes HTTP (MLRest).

Aplikační projekty buildí lokálně (`bin/Debug/`, `bin/Release/`). Do `C:\Edison\` se
dostávají pouze přes `dotnet publish` s profilem „SingleFileCEdison"
(`Properties/PublishProfiles/`) - bundle bez volných NuGet DLL. Výjimky drží sdílený
`SingleFilePublish.targets` v rootu (importují ho EDISONMAIN2, EDUPDATE2, TemplateDesigner
i EDControls): **`EDControls.dll` je vedle exe volný soubor**, aby šla nasadit samostatně
(do produkce jde DLL + exe, který ji používá; druhý exe se nepřebuildí a novou DLL převezme).
Cena: **breaking change API znamená publikovat i konzumenty** (DLL nikdy samotnou) - viz
`EDControls/CLAUDE.md`. Tamtéž `AppHostDotNetSearch`: exe hledají .NET runtime nejdřív v `dotnet\`
vedle sebe (EDLOADER ho rozbalí z `EDISONRUNTIME.zip`), globální **jen když `dotnet\` neexistuje** -
neúplná složka exe shodí.

Týmž profilem se vydává i **EDControls samostatně**: `PublishOwnAssemblyOnly` propustí jen
vlastní assembly (plný uzávěr závislostí - MLRest, EDLicence, EDCore, Newtonsoft - by
v `C:\Edison\` přepsal soubory legacy repa) a nezávislá kontrola počtu publish **shodí**,
než něco zapíše. `C:\Edison\` je staging: do produkce jde až explicitní klik v Compileru. Seznam binárek
k distribuci při vydání shellu (včetně těch z repa EDISON): `docs/deploy.md`.

## Struktura a pravidla pro CLAUDE.md

`EDISONMAIN2/`, `EDControls/`, `TemplateDesigner/`, `ServiceGtfsRt/`, `EDUPDATE2/` (klientský
updater nového shellu), `EDISON2.IntegrationTests/` a `EDISON2.E2ETests/` mají **vlastní `CLAUDE.md`**,
EDControls a EDISONMAIN2 navíc per-komponenta (ACTIVITY zámek: `EDISONMAIN2/dotnet/Startup/`). `EDTesting/`
= Caché test framework (`MLTestCase` + `TestRunner`), `docs/mlrest.md` = kompletní reference
Caché architektury, `.husky/` = git hooks (auto-install při `dotnet restore`).

**Krátké a výstižné**: root i projektový doc ≤ ~150 řádků, komponentový ≤ ~60. Při překročení škrtej.

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

`QueryOrThrow` = `Query` + `ThrowIfError()` na `IMLRest` i jako extension na `MLRest`; odděleně
jen pro graceful degradation. **Souběžné requesty jen na klonech** (`With()`) - `RestStatus` je
per-instance a čte se až po `Query`, takže paralelní volání na téže instanci si stavy prohodí
(spolknutá chyba / cizí výjimka). Klon má stejný GUID (pro server tatáž session) a každý request
běží ve vlastním Caché procesu, takže si nešlapou ani po `^||ML`. Vzor: `RestCallRunner`
(`EDControls/dotnet/RestCallRunner.cs`). Binární data: `BinarySave`/`BinaryLoad` (`docs/mlrest.md`).

## Kostra Caché metody

Kompletní reference `docs/mlrest.md §2.2`. Minimum: `ClassMethod MyMethod(ByRef sys As %String)
As %Status`, tělo `$$$Init` → try `{ $$$FillParams ... }` catch `{ $$$CatchErrRest }` →
`ret ret`. Wire invariant: `$$$CatchErrRest` nastaví `ret="1~message"` a s `sys("C","App")` od MLRest
i `^||ML("ERR")`, ze kterého `ThrowIfError()` vyhodí výjimku v .NET (`ret` dispatcher ignoruje).
Výstup named (`s ^||ML("OUT","klíč")=...`) nebo indexovaný (`s ^||ML("OUT",$i(i))=$lb(...)`, foreach).

**Výjimka `TemplateDesigner`:** `$$$Init` tam vědomě NEpoužíváme - metoda končí `ret $$$OK`
a `s err = ""` si musí nastavit sama; pravidla v `TemplateDesigner/.claude/rules/cache-api.md`.

## Přidání nového okna

Složka `EDISONMAIN2/dotnet/Windows/MyWindow/` s `MyWindow.xaml(.cs)` a `MyWindowViewModel`
(dědí `EDViewModel`, bezparametrický ctor čte `EDServices.Current`). Neodvoditelné:
`DataContext = _vm = new MyWindowViewModel();` musí být **před** `InitializeComponent()`;
registrace v `EdisonMainViewModel` jde přes `Command(...)` (výjimky mimo příkazy bere globální
handler); vstupní data konstruktorem.

Reference: `EDComponentAndViewModelTestWindow`, footer v `EDControls/dotnet/EDFooter/CLAUDE.md`,
EDInputForm příklad `EDControls/CACHE/ALVA/EDControls/EDInputForm/EDIFExample.cls`.

## Povinné nastavení nového aplikačního projektu

Platí pro aplikace postavené na EDControls. Bez těchto tří to padá za běhu: merge
`EDIcons.xaml` + `EDStrings.xaml` (jinak `{StaticResource}` hodí výjimku),
`DispatcherUnhandledException` handler s `e.Handled = true` (jinak crash)
a `EDAppearance.Initialize(app)` (fonty + theme). Referenční implementace
`EDISONMAIN2/dotnet/App.xaml(.cs)` a `Startup/AppBootstrapper.cs`. Výjimka `EDUPDATE2` - záměrně bez EDControls
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

- **Per Windows-uživatele (JSON)** - `LocalPrefs` shellu (`%APPDATA%\m-line\EDISONMAIN2\local.json`)
  přes injektovaný `ILocalPrefs`. Nové preference = nullable property (chybějící klíč zůstane
  null - kompatibilní upgrade). Sdílí ho víc instancí shellu, `Save` slučuje jen vlastní změny (`LocalPrefs.Save`).
- **Per-user × IDFa (Caché)** - stav agendových oken automaticky přes `EDViewModel` /
  `WindowStatePersistence` (vyměnitelné přes `IWindowStateStore`), viz `EDControls/dotnet/EDViewModel/CLAUDE.md`.
- **Cross-process (font, theme)** - `%APPDATA%\m-line\common\`; viz `EDControls/CLAUDE.md`.

## Testy

- **Změna chování přichází s testem** na nejlevnější úrovni, která regresi chytí: logika → unit, `.cls` → Caché,
  hranice (odpověď serveru, formát, argumenty procesu) → navíc šev (mock dokazuje jen sám sebe), startup shellu → E2E.
- **.NET**: `dotnet test EDISON2.sln` (xUnit + NSubstitute). **Šev .NET ↔ Caché** `EDISON2.IntegrationTests` (skutečné MLRest)
  běží jen se serverem, **UI E2E** `EDISON2.E2ETests` (FlaUI, DEBUG shell s `e2e=1`) jen s `EDISON_E2E=1`, test se skutečným
  oknem (`[WindowFact]`, ne `[Fact]`) jen s `EDISON_WINDOW_TESTS=1` - nastavuje pre-push. Pravidla šva a E2E v jejich `CLAUDE.md`.
- **Caché**: test class dědí `ALVA.EDTesting.MLTestCase` (cleanup `^||ML` + `SetupSys(.sys)` pro `$$$FillParams`),
  metody `TestXxx()`. Běh `pwsh -File tools/run-cache-tests.ps1 [-Package ...]` proti dev serveru `192.168.16.1:57770`
  (VPN, user `$env:USERNAME`, override `$env:CACHE_USER`); nový package mimo default list skriptu
  (EDISONMAIN2/EDControls/TemplateDesigner Tests) tam přidej, jinak ho pre-push hook nepokryje.
- **Nasazení `.cls` dělá hook sám** (`.claude/hooks/compile-objectscript.sh`, PostToolUse na Edit/Write,
  chyby na stderr). MCP `cache_compile` jen na to, co hook nepokryje, **ne na týž soubor po editaci**
  (dvojí nasazení si přepisuje výsledek). ⚠️ **Úprava `.cls` mimo Edit/Write hook nespustí**: na serveru
  zůstane stará třída (EDInputForm: „pole je prázdné, uložení nic neudělá") a drift testy to nechytí, čtou
  repo. Deploy vynuť `echo '{"tool_input":{"file_path":"<abs cesta>.cls"}}' | bash .claude/hooks/compile-objectscript.sh`.
- **Pre-push hook** (`.husky/pre-push`): change-aware podle pushovaného rozsahu - `.cs`→.NET
  (bez `Category=Integration`/`E2E`), `.cls`→Caché zúžená na dotčené package, `.cs/.cls/.inc`→integrační
  sada (při `.cls/.inc` povinná). Produkční `.cls` ve sdíleném `EDControls`/`EDTesting` spustí
  všechny Caché balíčky, testovací `.cls` (EDControls `Tests/`) jen svůj - TD sada je nejdražší
  (~45 s). Souhrn selhání `.husky/last-test-run.log`; bypass `git push --no-verify` jen nouzově.

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
