# EDControls — Přehled

> WPF knihovna sdílených komponent pro EDISON ekosystém. Postavena nad MLRest pro Caché komunikaci.
> Verzování: monorepo. Distribuce: ProjectReference z konzumujícího projektu.

## Kdy EDControls použít

- Pišu nový WPF projekt v EDISON ekosystému a chci sdílené komponenty (list, formulář, toolbar, footer, action-in-progress overlay)
- Potřebuju standardizovanou komunikaci s Caché přes MLRest
- Chci jednotný error handling, persist user state, error popupy
- Píšu okna která drží pozici/velikost přes restarty

## Komponenty — co je k dispozici

| Komponenta | Co dělá | Stav | Doc |
|---|---|---|---|
| **`EDList`** | Caché-driven list / picker / multi-select s persist sloupců a client-side search | Stabilní | [`02-EDList.md`](02-EDList.md) |
| **`EDFilter`** | Deklarativní filter komponenta pro EDList (auto-trigger reload, typed filter fields) | Stabilní (v1) | [`03-EDFilter.md`](03-EDFilter.md) |
| **`EDInputForm`** | Caché-driven editovatelný formulář (analogie legacy BIG) | Refactor v plánu (Tier 3) | `03-EDInputForm.md` *(později)* |
| **`EDViewModel`** | Base třída pro VM s `Command(...)`, `CommandAsync(...)`, `IPopupDisplayer` integrací | Stabilní | — |
| **`EDFooter`** | Footer s tlačítky OK/Storno/Aplikovat (standardní rozložení) | Stabilní | — |
| **`EDToolbar`** | Toolbar s ikonami a tooltipy | Stabilní | — |
| **`EDActionInProgress`** | Loading overlay pro long-running operace | Stabilní | — |
| **`IUserPrefsService`** | Per-user × IDFa persist state (zoom, sort, recent files…) | Stabilní | — |
| **`IPopupDisplayer`** | Error/Confirm/Info/ConfirmClose dialogy přes Popups | Stabilní | — |
| **`WindowStatePersistence`** | Pozice/velikost/maximized okna per (MLP × IDFa) | Stabilní | — |
| **`EDIcons`** | Resource dictionary 33 Fluent UI System Icons | Stabilní | — |
| **`EDStrings`** | Sdílené texty tlačítek (Ok / Cancel / Apply / Yes / No / Close) | Stabilní | — |

## Klíčové dokumenty

| Dokument | O čem je |
|---|---|
| [`01-wire-formats.md`](01-wire-formats.md) | Standard pro Caché ↔ .NET komunikaci přes MLRest — co je IN, co je OUT, kdy CVar vs JSON, jak na type safety v .NET, filter konvence |
| [`02-EDList.md`](02-EDList.md) | Kompletní reference EDList komponenty — properties, events, Caché contract, příklady |
| [`03-EDFilter.md`](03-EDFilter.md) | EDFilter spec — deklarativní filter nad EDListem, FilterCombo, wire formát, extensibility |

## Jak začít

### 1. Přidat ProjectReference

```xml
<ProjectReference Include="..\..\EDControls\dotnet\EDControls.csproj"/>
```

Plus odkaz na `MLRest.dll` (legacy DLL):
```xml
<Reference Include="MLRest">
    <HintPath>C:\Edison\MLRest.dll</HintPath>
</Reference>
```

### 2. Inicializovat `EDServices`

V `App.xaml.cs` při startu aplikace:

```csharp
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var mlRest = new MLRest(server, port, namespace) { Idfa = idfa, Mlp = mlp };
        EDServices.Current = new EDServices
        {
            MLRest = new MLRestAdapter(mlRest),
            UserPrefs = new CacheUserPrefsService(new MLRestAdapter(mlRest)),
            PopupDisplayer = new WpfPopupDisplayer(),
        };
    }
}
```

### 3. VM dědí `EDViewModel`

```csharp
public class MyDialogViewModel : EDViewModel
{
    public RelayCommand DoSomethingCommand { get; }

    public MyDialogViewModel()
    {
        // Command(...) automaticky chytá výjimky přes IPopupDisplayer.Error()
        DoSomethingCommand = Command(DoSomething);
    }

    private void DoSomething()
    {
        // Přístup k MLRest přes property z base třídy
        var result = MLRest!.QueryOrThrow("ALVA.X.Y", "Method", new CVar());
    }
}
```

### 4. V okně přístup ke komponentám

```xml
<Window xmlns:el="clr-namespace:EDControls.EDList;assembly=EDControls">
    <el:EDList x:Name="TheList"/>
</Window>
```

```csharp
public MainWindow(MyDialogViewModel vm)
{
    DataContext = vm;
    InitializeComponent();
    TheList.CacheClass = "ALVA.X.YList";
    TheList.HostId = "MyApp.MainList";
}
```

## Architektonická pravidla

EDControls vznikla jako modernizace části legacy EDISON `EDFORMS` infrastruktury. Drží 5 pravidel:

| # | Pravidlo |
|---|---|
| R1 | **Async-only Caché volání** — žádný synchronní MLRest v UI threadu |
| R2 | **Chyby přes `IPopupDisplayer`** — žádný `MessageBox.Show` ani silent catch |
| R3 | **CVar named IN + 4 OUT styly podle obsahu** — viz [`01-wire-formats.md`](01-wire-formats.md) |
| R4 | **ViewModely bez WPF referencí** — testovatelné, MVVM clean |
| R5 | **Persistence schémat versionable** — JSON struktura s `version` polem |

## Konvence pro autora EDControls

- **Změna API komponenty → update odpovídajícího `0X-název.md`** ve stejném commitu (tahle DOCS složka je source of truth)
- **Nová komponenta → nový `0X-název.md`** + zápis do tabulky v `00-overview.md`
- **Breaking change → upozornění** v changelogu + migrate guide v doc

## Out-of-scope

Tyto komponenty z legacy EDISON **vědomě nemigrujeme do EDControls**:

- BDG (BaseDataGrid) — komerční C1FlexGrid, 4500+ LOC
- Atomické kontrolky (EDButton, EDCheckedComboBox, EDDatum, …) — nativní WPF stačí
- BaseDocument framework — mimo TD scope
- Codebook centrální katalog — overhead se nevyplatí
- EDInputBox, EDMessageBox, EDScheduler — pokryto `IPopupDisplayer`

Viz `.claude/docs/legacy-component-research.md` pro detaily.
