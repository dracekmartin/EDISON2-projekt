# EDViewModel - poznámky pro vývojáře

Základní třída ViewModelů + infrastruktura (`EDServices` - DI singleton `Current`, `WindowNavigator`,
`PopupDisplayer`, `RelayCommand`/`AsyncRelayCommand`, `EDAction`). API čti ze zdrojáků, tady jen invarianty.

## AttachToWindow - volat PŘED Show()/ShowDialog()

`WindowNavigator` ho volá automaticky, shell okno ručně v AppBootstrapperu. Dělá:
1. klávesy z `Actions` + vždy **Ctrl+Enter → OkCommand** (OK záměrně bez `IsDefault` -
   samotný Enter slouží EDInputFormu k navigaci mezi poli);
2. persistenci stavu okna - úložiště drží `IWindowStateStore` (default Caché pod
   `PersistedFormId`, override `WindowStateStore`); hook `SourceInitialized` (fíruje synchronně uvnitř `Show()`
   před prvním paintem → žádný flicker); **okno pustí do `Show()` vždy jako `Normal`** - HWND
   s `WS_MAXIMIZE` Win32 zvětšit nenechá a uložená velikost by skončila jen v restore bounds;
   restore pak size → location → Maximize **poslední**, save z `RestoreBounds`; **pozice mimo dnešní plochu** (odpojený
   monitor) se zahodí (kam pak okno jde: `MonitorPlacement`); best-effort vždy. Caché úložiště čte na klonu nejvýš 3 s (bez odpovědi save vynechá), ukládá na pozadí;
3. window-scoped `PopupDisplayer` (owner: per-call → okno VM → globální default) -
   agendová okna nedostávají popupy nad shellem.

Panel Podpora ani usage telemetrie tu nejsou - visí na `Window.Loaded` z `EDAppearance.Initialize`
(proč: [EDSupportPanel/CLAUDE.md](../EDSupportPanel/CLAUDE.md)). Telemetrie (`EDUsageTelemetryHook`)
bere kliky jen na **pojmenované** controly a jen okna s `EDViewModel` a nenulovým `PersistedFormId`;
guard `IsHooked` (`Loaded` fíruje opakovaně); legacy launch trackuje `LegacyModuleHost.Launch`. Záznam nese
firmu, ve které vznikl, flush při zavření okna jde na pozadí.
`PersistedFormId` default `"<FormIdPrefix>.<Window>"` (shell `EDISON2`, TD `TD`) - odděluje
aplikace navzájem i od legacy ID z `EDISFormEnum`; override na `null` = opt-out (vypne i telemetrii).

## Commands, EDServices, EDAction

- Příkazy přes `Command()` / `CommandAsync()` - error handling přes `PopupDisplayer`;
  `CommandAsync` brání double-clicku (záměrně `async void` s vlastním guardem).
- **`canExecute` NESMÍ házet** - WPF ho volá při každé UI interakci a výjimka propadne
  mimo náš catch až do `DispatcherUnhandledException`.
- `RelayCommand` bez explicitního displayeru fallbackuje na `EDServices.Current.PopupDisplayer` -
  test s mockem ho musí registrovat přes `EDServices` nebo předat 3. argumentem.
- `EDServices.MLRest` je mutable a sdílená (přepnutí firmy vidí všechny VM); `Current` hází před `Initialize`
  (testy: vlastní instance s mocky); `With(...)` = klon, sdílenou instanci nikdy nemutuj (proč: root CLAUDE.md;
  identity copy viz EDControls CLAUDE.md). Post-login kód čte `Rest` / `RequireMLRest` (`InvalidOperationException`,
  ne NRE), nullable `MLRest` je pro kód před loginem; `HasAccess()` = `true` při `AccessTable == null` (testy).
- `EDAction.IconElement` vrací **nový** `Image` při každém přístupu - `UIElement` smí mít
  jen jednoho rodiče (nutné pro `MenuItem.Icon` přes `ItemContainerStyle`).
- `OnOk()`: bez `base.OnOk()` / `CloseDialog(true)` zůstane okno otevřené - využívá se pro validaci.

## WindowNavigator

- Fasáda nad internal `LegacyModuleHost` (závislosti ctorem, testy s fake `ILegacyProcess`), TD bere `WpfOnly()`.
  Konec procesu jen přes `ILegacyProcess.OnExited`: `Process.Exited` fíruje jednou, pozdní odběratel by ho minul.
- `ShowDialog` ztmavuje ownera přes `WindowDimmer` (refcount), **ne** `IsEnabled=false` (controly na 0.5 opacity).
- Legacy launch kontrakt viz `EdisonLaunchArgs`; pre-login admin override předává `shareShellSession=false` (jinak `LockExist`).
- Modeless `ShowLegacy*` dedupují (`TryFocusExisting`, klíč `exe+forma+inputParam`), dialogové ne. Okno modulu se hledá čerstvě
  mezi viditelnými okny procesu (`ProcessWindows`, i dialog se skrytým vlastníkem), minimalizované se obnoví. Proces bez okna po 20 s
  shell nabídne zabít (legacy proces umí přežít formulář). `DisplayName` = `MainWindowTitle`, shodné rozliší `ExeName`/`FormFqn`/`InputParam`.
- `CloseAll()` a fáze `CloseShellWindows()`/`CloseLegacyModules()` jsou záměrně jen na konkrétní
  třídě, ne interface; `Try*` varianty hlásí veto okna (`e.Cancel`) - výměna session ho musí
  respektovat, jinak přeživší okno mluví s cizí session.
- `CloseAll()` ani „Spuštěné moduly" nevidí owned overlay okna (`IOverlayWindow`: panel
  Podpora, „Akce probíhá" - jinak by se nabídly jako modul a zavřely; nové
  owned overlay okno marker potřebuje) a nezavírá okna frameworku (`FrameworkWindows`:
  AvalonDock si overlay okno cachuje a cizí zavření shodí každý další drag přes dock;
  jeho `AvalonDock.Controls.IOverlayWindow` je jiný typ téhož názvu → predikát podle namespace).
