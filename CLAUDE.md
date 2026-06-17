# EDISON2 — poznámky pro vývojáře

Dvouvrstvá WPF aplikace: .NET UI ↔ Caché (InterSystems) back-end přes HTTP (MLRest).

Aplikační projekty buildí lokálně (`bin/Debug/`, `bin/Release/`). Do `C:\Edison\` se dostávají pouze přes `dotnet publish` s publish profilem „SingleFileCEdison" (`Properties/PublishProfiles/`) — single-file výstup bez volných DLL.

Nové features pokrývej xUnit testy (`EDControls.Tests/`, `EDISONMAIN2.Tests/`) a Caché
`%UnitTest` testy s base classou `ALVA.EDTesting.MLTestCase`. Pre-push hook (Husky.NET)
spouští oba sety před každým `git push` — viz sekce **Testy** níže.

## Struktura repozitáře

| Složka | Role |
| --- | --- |
| `EDISONMAIN2/dotnet/` | Hlavní WPF aplikace (vstupní bod, okna, služby). |
| `EDControls/dotnet/` | Sdílená WPF knihovna — ovládací prvky (EDFooter, EDToolbar, EDInputForm, EDList, EDActionInProgress) a základní třída pro ViewModely (EDViewModel). |
| `TemplateDesigner/dotnet/` | WPF aplikace — návrhář tiskových šablon (WYSIWYG editor). Vlastní `CLAUDE.md`. |
| `ServiceGtfsRt/` | ASP.NET Core Windows Service — GTFS Realtime feed (vozidla, spoje). Vlastní `CLAUDE.md`. |
| `EDControls.Tests/` | xUnit testy pro EDControls (EDViewModel, EDInputForm parsing, EDList formátování). |
| `EDISONMAIN2.Tests/` | xUnit testy pro EDISONMAIN2 (parsování stromu modulů). |
| `EDTesting/` | Sdílený Caché test framework — base class `MLTestCase` + programový `TestRunner` (volaný z pre-push hooku). |
| `tools/` | Pomocné skripty (PowerShell). `run-cache-tests.ps1` volá `TestRunner.RunPackage` přes MLRest. |
| `.husky/` | Husky.NET git hooks (auto-installed při `dotnet restore` přes target v `EDISONMAIN2.Tests.csproj`). |
| `MLRest.md` | Kompletní referenční dokumentace Caché architektury (namespace, makra, komunikační protokol). |

---

## MLRest — použití v .NET

`MLRest` je HTTP klient, který serializuje .NET hodnoty do Caché process-private globals
(`^||ML("IN",...)`) a deserializuje odpověď (`^||ML("OUT",...)`) zpět do .NET.

DLL je odkazována přímo: `C:\Edison\MLRest.dll`.

### IMLRest a EDServices — architektura

Instance `MLRest` se nikdy nevytváří přímo ve windows ani ViewModelech. Přístup jde přes
abstrakci `IMLRest` (viz `EDControls/IMLRest.cs`) a sdílený kontejner `EDServices`
(singleton `EDServices.Current`, inicializován v `AppBootstrapper`).

- ViewModely: `MLRest` property z `EDViewModel` (čte z `EDServices.Current`)
- Testy: vlastní `EDServices` s mock `IMLRest`
- Ovládací prvky (`EDInputForm`, `EDList`): čtou `MLRest` z `DataContext` rodičovského okna

### Volání Caché metody — používej CVar (silně doporučeno)

`CVar` je idiomatický Caché strukturovaný typ. **Vždy používej `CVar`, pokud nemáš konkrétní
důvod nepoužívat ho.** Vyhni se `object[]` — používá plochý numerický formát subscriptů, který
je hůře rozšiřitelný a méně výrazový než CVar.

```csharp
using EDControls;   // for QueryOrThrow

// Empty input
CVar output = mlRest.QueryOrThrow("ALVA.MY.ClassName", "MyMethod", new CVar());

// Structured input
var input = new CVar();
input["key"] = new CVar("value");
input["count"] = new CVar(42);
CVar output = mlRest.QueryOrThrow("ALVA.MY.ClassName", "MyMethod", input);

// CList input (for ordered tuples — maps to $lb() in Caché)
input["item"] = new CList("TYPE", "field1", "field2");
```

Čtení výstupu:

```csharp
// Single named value
string val = output["key"].Value?.ToString() ?? "";

// Single named CList
CList row = output["key"];

// Iterate indexed rows ("1", "2", ...)
// Skip "ERR" — it's MLRest metadata, not application data.
foreach (KeyValuePair<string, CVar> kvp in output)
{
    if (kvp.Key == "ERR") continue;
    CList row = kvp.Value;            // implicit CVar→CList
    string type = row.GetString(1);   // 1-based indexing
    string name = row.GetString(2);
}
```

`QueryOrThrow` je definováno dvěma způsoby:

- **`IMLRest.QueryOrThrow`** — metoda přímo na rozhraní. Používej ve ViewModelech přes `MLRest!.QueryOrThrow(...)`.
- **Extension metoda na `MLRest`** — pro legacy kód pracující přímo s konkrétní třídou `MLRest`.

Obě varianty volají `Query` + `RestStatus.ThrowIfError()` atomicky.
Volej je zvlášť pouze tehdy, kdy potřebuješ graceful degradation (`continue` ve smyčce,
`return null`).

### Caché wire format pro CVar

CVar se mapuje na string-subscriptované process-private globals:

```text
.NET cVar["key"]          → ^||ML("IN","key")
.NET cVar["1"]            → ^||ML("IN","1")    ← string "1", NOT numeric 1
.NET CList("A","B","C")   → $lb("A","B","C")  in Caché
```

Caché strana čte přes `$li(^||ML("IN","i"), n)` a zapisuje:

```objectscript
s ^||ML("OUT","1") = $lb("TYPE","value1","value2")
s ^||ML("OUT","2") = $lb("OTHER","value3")
```

### Kdy je object[] přijatelný

Používej `object[]` pouze v legacy kódu nebo při integraci s existujícími Caché metodami,
které již používají numerické subscripty (`^||ML("IN",1)`, `^||ML("OUT",1)`). Důvod zdokumentuj.
V novém kódu `object[]` **nepoužívej**.

### Binární data

```csharp
string guid = mlRest.BinarySave(bytes);    // upload byte[] → GUID
byte[] data = mlRest.BinaryLoad(guid);     // download GUID → byte[]
```

Na Caché straně uloženo v `$$$SYSBinary(GUID)`; automaticky se uvolní.

### Klíčové vlastnosti

| Vlastnost | Popis |
| --- | --- |
| `Delimiter` | Oddělovač polí (`char`). Potřebný pouze při práci s legacy protokoly; s CVar/CList není třeba. |
| `Idfa` | Identifikátor aktuální firmy (např. `"CB_DP"`). Mění se při přepnutí firmy uživatelem. |
| `Mlp` | Řetězec ID uživatele. Na Caché straně předáván přes `sys("U","MLP")`. |
| `DataNamespace` | Aktivní datový namespace. Aktualizuje se při přepnutí firmy. |
| `RestStatus` | Potřebný pouze tehdy, kdy se NEPOUŽÍVÁ `QueryOrThrow` (vzor graceful degradation). |

---

## Kostra Caché metody

Viz `MLRest.md §2.2` pro kompletní referenci. Minimální kostra ve formátu CVar:

```objectscript
Include GENERAL

ClassMethod MyMethod(ByRef sys As %String) As %Status
{
    $$$Init
    try {
        $$$FillParams

        // Read named input values
        s name = ^||ML("IN","name")
        s count = ^||ML("IN","count")

        // Write named output values (string key → readable on .NET as output["result"])
        s ^||ML("OUT","result") = "ok"
        s ^||ML("OUT","info") = $lb("TypeA","value1","value2")

        // Write indexed output rows (iterated on .NET with foreach)
        s i = 0
        s ^||ML("OUT",$i(i)) = $lb("ROW","a","b")
        s ^||ML("OUT",$i(i)) = $lb("ROW","c","d")
    }
    catch (ex) { $$$CatchErrRest }
    ret ret
}
```

Odpovídající čtení v .NET viz sekce „Volání Caché metody" výše (CVar příklady).

- `$$$Init` — nastaví `ret=0`, `err=""`
- `$$$FillParams` — naplní `IdFa`, `del`, `dataDB`, `userID`, `GUID`, atd.
- `$$$CatchErrRest` — nastaví `ret="1~message"`, což `ThrowIfError()` vyhodí jako výjimku v .NET

---

## Přidání nového okna

1. Vytvoř složku pod `EDISONMAIN2/dotnet/Windows/MyWindow/`.
2. Přidej `MyWindow.xaml` (Window), `MyWindow.xaml.cs`, `MyWindowViewModel.cs`.
3. ViewModel dědí `EDViewModel`. Caché volání jdou přes `MLRest` (property z `EDServices`) —
   v okně ani ViewModelu se `MLRest` nevytváří ani nepředává. Bezparametrický konstruktor
   `EDViewModel()` čte automaticky z `EDServices.Current`.
4. V code-behind nastav `DataContext` **před** `InitializeComponent()`:

   ```csharp
   public MyWindow()
   {
       DataContext = _vm = new MyWindowViewModel();
       InitializeComponent();
   }
   ```

5. Zaregistruj příkaz v `EdisonMainViewModel` přes `WindowNavigator.Show` (modeless)
   nebo `WindowNavigator.ShowDialog` (modální) — předej **instanci okna**:

   ```csharp
   OpenMyWindowCommand = Command(() => WindowNavigator.Show(new MyWindow()));
   ```

   `Command(...)` je helper na `EDViewModel`, který vytvoří `RelayCommand` s automatickým
   error handlingem přes `PopupDisplayer`.

   Pokud okno potřebuje vstupní data, předej je do konstruktoru:
   `WindowNavigator.Show(new MyWindow(input))`.

6. Přidej tlačítko na toolbar v `EdisonMainWindow.xaml` navázané na příkaz.

Viz `EDFooter/CLAUDE.md` pro kompletní šablonu okna s footerem.
Viz `EDComponentAndViewModelTestWindow` pro funkční referenční implementaci.
Viz `EDControls/CACHE/ALVA/EDControls/EDInputForm/EDIFExample.cls` pro referenční příklad EDInputFormu — při přidání nové EDIF featury přidej ukázku i sem.

---

## EDIcons — sdílená knihovna ikon

Ikony: `EDControls/dotnet/EDIcons/`. Resource dictionary `EDIcons.xaml` mapuje soubory na sémantické klíče.
Aktuální ikony viz `EDIcons.xaml`.

### Přidání nové ikony

1. Zkopíruj PNG do `EDControls/dotnet/EDIcons/png/`.
2. Zaregistruj v `EDControls.csproj` jako `<Resource Include="EDIcons\png\myicon.png"/>`.
   Pouze PNG potřebuje explicitní registraci — `.xaml` soubory SDK zahrnuje automaticky
   (ruční `<Page>` způsobí duplikát).
3. Přidej `BitmapImage` do `EDIcons.xaml` se **sémantickým klíčem** (`EDIcons.MyName`, ne `EDIcons.Myicon`).

---

## EDStrings — sdílené texty tlačítek

Texty: `EDControls/dotnet/EDStrings/EDStrings.xaml`. Aktuální texty viz soubor.
Přidání: vlož `<sys:String x:Key="EDStrings.X">Text</sys:String>`. Žádná změna v `.csproj`.

---

## Obsluha výjimek

`Command(...)` na `EDViewModel` automaticky zachytává výjimky přes `PopupDisplayer`.
Výjimky mimo příkazy se zachytí globálním handlerem.

---

## Povinné nastavení App.xaml pro každý nový projekt

Každý nový aplikační projekt musí:

1. Mergovat resource dictionaries (`EDIcons.xaml`, `EDStrings.xaml`) — bez toho `{StaticResource}` hodí výjimku za běhu.
2. Přidat `DispatcherUnhandledException` handler — bez `e.Handled = true` aplikace po výjimce spadne.

Viz `EDISONMAIN2/dotnet/Startup/App.xaml(.cs)` jako referenční implementaci.

---

## Persistence stavu

Dva čisté layery pro user preferences / state:

### Per-machine (lokální JSON)

`LocalPrefs` (`EDISONMAIN2/dotnet/Startup/LocalPrefs.cs`) — JSON soubor v
`%APPDATA%\m-line\EDISONMAIN2\local.json`. Per Windows-user, sdílí mezi
relacemi na stejné stanici. Přístup přes injektovaný `ILocalPrefs`
(composition root v `AppBootstrapper` vytvoří jednu instanci, propaguje
do consumerů — testy substituují mock).

Aktuálně ukládá: `LastIdfa` (pre-fill firmy při dalším přihlášení) +
2 toggly (`ShowWindowsAsDialogs`, `LoadBoards`).
Pro nové preference přidej nullable property; chybějící klíče v existujícím
souboru zůstanou null (kompatibilní upgrade).

```csharp
// AppBootstrapper (composition root):
var prefs = LocalPrefs.Load();         // vrátí ILocalPrefs naplněný z disku
// předá se dál do CompanySwitcher / EdisonMainViewModel přes ctor

// Consumer (VM, switcher):
private readonly ILocalPrefs _prefs;
var idfa = _prefs.LastIdfa;             // čtení
_prefs.LastIdfa = "CB_DP";              // zápis
_prefs.Save();                          // persist
```

### Per-uživatel × IDFa (Caché server-side)

`WindowStatePersistence` (`EDControls/dotnet/WindowStatePersistence.cs`) —
persistuje pozici, velikost a Maximized stav WPF okna přes existující legacy
endpoint `ALVA.LIBRARY.Form.SaveParams` / `LoadParams`. Storage automaticky
per-MLP × IDFa (server čte sys kontext z MLRest).

**Aktivace**: VM dědí `EDViewModel` → `PersistedFormId` se odvozuje z názvu třídy
(`EdisonMainViewModel` → `"EDISON2.EdisonMainWindow"`). Override v subclass
na `null` pro opt-out. WindowNavigator zavolá `vm.AttachToWindow(window)`
před `Show()` automaticky; pro shell okno volá `AppBootstrapper.BuildShellWindow` ručně.

Prefix `"EDISON2."` odděluje od starých numerických IDs z `EDISFormEnum` (tam
VB moduly ukládají vlastní state).

---

## Spouštění legacy modulů — EDISONMAIN2API

Most do legacy světa: `EDISONMAIN2API.dll` (`c:\MLineRepos\EDISON\EDISONMAIN2API\`,
.NET Framework 4.8, deploy do `C:\Edison\`). `FormToRunGetter.TryGetFormFromArgs`
parsuje 10 cmdline args (sentinel `"EDISONMAIN2"`, server, port, ns, idfa, login,
fullName, mlp, **formName** = FQN třídy WinForms formy, **shellGuid**), reflexí
najde typ ve volajícím assembly a instancuje ho.

**Sdílení ACTIVITY session:** non-prázdný `shellGuid` → modul dostane shellův
GUID a `CacheLocks=1` (server na každém requestu volá `UpdateHistory` +
`LockExist` — zánik shell zámku shodí i moduly). Prázdný `shellGuid` (over-license
retry před `VerifyLic`) → modul si vygeneruje vlastní GUID, bez `CacheLocks`.

Kontrakt pro legacy `.exe`: ve své `Sub Main` zavolat
`FormToRunGetter.TryGetFormFromArgs(args, out form)` a předat do `Application.Run(form)`.
Bez argů původní standalone chování. Reference: `MHD2PlanDopravy/Program.cs`,
`USERS/Users-v4/Program.vb`.

EDISONMAIN2 strana: `ProcessLauncher.Launch(module)` (moduly z module tree)
a `LicenceVerifier.LaunchUsersFormAndWait` (admin override při out-of-license).

---

## Testy

Dvě vrstvy testů, oba spouštěny pre-push hookem (Husky.NET).

### .NET (xUnit)

`EDControls.Tests/`, `EDISONMAIN2.Tests/`. Standardní xUnit + NSubstitute.
Lokálně: `dotnet test EDISON2.sln`.

### Caché (`%UnitTest`)

Testovací třídy dědí z `ALVA.EDTesting.MLTestCase` (viz [EDTesting/CACHE/ALVA/EDTesting/MLTestCase.cls](EDTesting/CACHE/ALVA/EDTesting/MLTestCase.cls)).
Base poskytuje `OnBeforeOneTest` (čistí `^||ML`) a `SetupSys(ByRef sys, idfa, mlp, loginName)`
helper pro `$$$FillParams` boilerplate.

```objectscript
Class ALVA.MYPROJECT.Tests.MyTest Extends ALVA.EDTesting.MLTestCase
{
Method TestSomething()
{
    do ..SetupSys(.sys)
    do ##class(ALVA.MYPROJECT.API.Foo).Bar(.sys)
    do $$$AssertEquals(^||ML("OUT","result"), "expected")
}
}
```

`ALVA.EDTesting.TestRunner.RunPackage` spouští všechny test classes pod daným package
přes MLRest a vrací strukturovaný výsledek (`passed`/`failed`/`total`/`failures`).
Lokálně: `pwsh -File tools/run-cache-tests.ps1 [-Package "ALVA.MYPROJECT.Tests"]`.

### Pre-push hook

`.husky/pre-push` (Husky.NET) spouští před každým `git push`:

1. `dotnet test EDISON2.sln`
2. `tools/run-cache-tests.ps1`

Selhání zablokuje push. Emergency bypass: `git push --no-verify` (nepoužívat jako návyk).

**Onboarding kolegy:** `dotnet restore` v root repu — MSBuild target v
[EDISONMAIN2.Tests.csproj](EDISONMAIN2.Tests/EDISONMAIN2.Tests.csproj) spustí
`dotnet tool restore` + `dotnet husky install`, čímž se hooks aktivují bez extra kroků.

**Caché tests potřebují přístup k dev serveru** (`192.168.16.1:57770`, namespace `MLINE`).
Bez VPN / mimo M-line síť `run-cache-tests.ps1` selže. Hardcoded server a IdFa
v parametrech skriptu; uživatel se odvozuje z `$env:USERNAME` (lze override
`$env:CACHE_USER`).

### Přidání nového Caché testu

1. `extends ALVA.EDTesting.MLTestCase` — automaticky získáš cleanup + SetupSys
2. Test methods `Method TestXxx()` (per %UnitTest konvence)
3. `cache_compile` (MCP) → třída na serveru
4. `pwsh -File tools/run-cache-tests.ps1 -Package "ALVA.MYPROJECT.Tests"` k ověření

Pokud je nový package mimo `ALVA.EDISONMAIN2.Tests`, přidej ho do default `-Package`
listu v `tools/run-cache-tests.ps1` (comma-separated), jinak pre-push hook ho nepokryje.

---

## Komentáře

Komentáře v `EDISONMAIN2/` a `EDControls/` jsou **česky**. Stock VS šablony
(`Interaction logic for ...`) jsou výjimka.

Defaultně nekomentovat. Přidávej jen když:

- WHY je netriviální — skrytá invarianta, workaround konkrétního bugu.
- Sémantika legacy systému (Caché global, ACTIVITY zámek, licenční flag).

**Nekomentovat:** WHAT (pojmenování to říká), reference na task / fix / commit
(rotí, patří do commit message), stock VS šablony.

**Délka:** preferuj 1 řádek, max 2–3. Multi-paragraph `<remarks>` blok je špatně.
