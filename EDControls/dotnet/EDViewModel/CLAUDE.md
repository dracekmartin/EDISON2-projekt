# EDViewModel - poznámky pro vývojáře

Základní třída ViewModelů + okolní infrastruktura: `EDServices` (DI kontejner, singleton
`Current`), `WindowNavigator`, `PopupDisplayer`, `RelayCommand`/`AsyncRelayCommand`,
`EDAction`. API čti přímo ze zdrojáků, tady jsou jen neodvoditelné invarianty.

## AttachToWindow - volat PŘED Show()/ShowDialog()

`WindowNavigator` ho volá automaticky; **shell okno ho v AppBootstrapperu volá ručně**. Dělá:

1. **Klávesy** z `Actions` + vždy **Ctrl+Enter → OkCommand**. OK tlačítko záměrně nemá
   `IsDefault` - samotný Enter slouží EDInputFormu k navigaci mezi poli.
2. **Persistence stavu okna** - hook na `SourceInitialized` (fíruje synchronně uvnitř `Show()`
   před prvním paintem → žádný flicker). Restore pořadí: size → location → Maximize **jako
   poslední** (jinak se ztratí normal-velikost); save bere `RestoreBounds`. Obojí
   best-effort - výjimka se spolkne, okno se otevře vždy.
3. **`PopupDisplayer` se stane window-scoped** - resolve owner: per-call → okno VM →
   globální default. Agendová okna tak nedostávají popupy nad shellem.

Panel Podpora ani usage telemetrie tady **nejsou** - obojí visí na `Window.Loaded`
z `EDAppearance.Initialize`, protože TemplateDesigner `AttachToWindow` nevolá (viz
[EDSupportPanel/CLAUDE.md](../EDSupportPanel/CLAUDE.md)).

`PersistedFormId` default `"<FormIdPrefix>.<Window>"` (`EDServices.FormIdPrefix`, shell
`EDISON2`, TD `TD`) - prefix odděluje aplikace navzájem i od legacy numerických ID
z `EDISFormEnum`; override na `null` = opt-out (vypne i telemetrii).

## Usage telemetrie (`EDUsageTelemetryHook`)

OPEN + kliky na **pojmenované** controly (ContextMenu/Popup routed event nemusí dorazit),
flush do Caché při `Closed`. Best-effort, nikdy nehazí. Legacy launch trackuje
`WindowNavigator.LaunchLegacy` (OPEN si hostovaná forma trackne sama). Hook bere **jen okna
s `EDViewModel` a nenulovým `PersistedFormId`** - šum (overlay okna, popupy, chrome
AvalonDocku) ViewModel nemá, takže vypadne bez druhé attached property. Guard `IsHooked`:
`Loaded` může fírovat opakovaně.

## Commands

- Vytvářej přes `Command()` / `CommandAsync()` - automatický error handling přes
  `PopupDisplayer`. `CommandAsync` brání double-clicku (`CanExecute=false` během běhu); je
  záměrně `async void` s vlastním guardem.
- **`canExecute` NESMÍ házet** - WPF ho volá při každé UI interakci a výjimka propadne mimo
  náš catch až do `DispatcherUnhandledException`.
- `RelayCommand` bez explicitního displayeru fallbackuje na
  `EDServices.Current.PopupDisplayer` - test, který čeká zachycení výjimky mockem, musí mock
  registrovat přes `EDServices` nebo předat 3. argumentem.

## EDServices

- `EDServices.MLRest` je mutable a sdílená - přepnutí firmy vidí všechny VM automaticky.
  `EDServices.Current` hází před `Initialize` (testy si vytvářejí vlastní instanci s mocky).
  `IMLRest.With(...)` = klon pro jednorázové requesty (poll `CacheLocks=2`, logout jiné
  firmy) - nikdy nemutuj sdílenou instanci (identity copy viz EDControls CLAUDE.md).
- Post-login kód čte `Rest` (na VM) / `EDServices.RequireMLRest` - chybějící session dá
  `InvalidOperationException`, ne NRE. Nullable `MLRest` je pro kód, který musí fungovat
  i před loginem (controls mají vlastní `EnsureConfigured` s hláškou pro hosta).
- `EDViewModel.HasAccess()` vrací `true`, když `AccessTable == null` (testy, pre-login).

## WindowNavigator

- `ShowDialog` ztmavuje ownera přes `WindowDimmer` (refcount), **ne** `IsEnabled=false` - to
  by srazilo controly na 0.5 opacity.
- Legacy launch: sentinel `"EDISONMAIN2"` + 13 polí, viz `BuildLauncherArguments` (poslední =
  Base64 zástupce na ploše, prázdné u běžného launche; `ShowLegacyShortcutAsync`).
  `shareShellSession=true` → modul sdílí shellův ACTIVITY lock (`CacheLocks=1`); pre-login
  admin override (`LicenceVerifier`) musí předat `false`, jinak requesty padají na
  `LockExist`. Legacy `EDISONMAIN.exe` se spouští napřímo, ne přes host - potřebuje
  `ApplyDefaultInput` retrofit, který host neumí.
- `GetActiveModules` vrací i `ExeName`/`FormFqn`/`InputParam` - `DisplayName` legacy modulu je
  doslovný `MainWindowTitle` (agenda si ho formátuje sama), takže rozlišit dva stejně
  pojmenované moduly umí až konzument přes tuhle identitu.
- Modeless `ShowLegacy`/`ShowLegacyAsync` dedupují: běží-li už proces se stejným
  `exe+forma+inputParam`, jen přenesou focus (`TryFocusExistingLegacy`). Dialogové varianty
  ne - ztmavují shell, druhý klik tam nejde.
- `CloseAll()` je záměrně jen na konkrétní třídě `WindowNavigator`, ne na interface; totéž
  jeho fáze `CloseShellWindows()` / `CloseLegacyModules()` (přepnutí firmy je volá odděleně).
  Varianty `TryCloseAll()`/`TryCloseShellWindows()` hlásí veto okna (`e.Cancel` v Closing) -
  výměna session je musí respektovat, jinak přeživší okno mluví s cizí session přes sdílený
  MLRest. Void verze existují kvůli aditivnímu API (sdílená DLL).
- **Owned overlay okna EDControls (`IOverlayWindow`) `CloseAll()` ani „Spuštěné moduly"
  nevidí** - panel Podpora a „Akce probíhá" jsou top-level okna, takže by se jinak nabídly
  jako modul a `CloseAll` by je zavřel (panel nevratně). Nové owned overlay okno tenhle
  marker potřebuje.
- **Okna frameworku (`FrameworkWindows`) `CloseAll()` nezavírá** - `DockingManager` si overlay
  okno cachuje (vzniká skryté už při jeho `Loaded`) a nulluje ho teprve po vlastním
  `Close()`, takže cizí zavření shodí **každý další drag přes dock**. Past:
  `AvalonDock.Controls.IOverlayWindow` je jiný typ téhož krátkého názvu, marker EDControls ho
  nepokryje → predikát jede podle namespace.

## Drobné pasti

- `EDAction.IconElement` vrací **nový** `Image` při každém přístupu - `UIElement` smí mít jen
  jednoho rodiče (nutné pro `MenuItem.Icon` přes `ItemContainerStyle`).
- `OnOk()`: bez `base.OnOk()` / `CloseDialog(true)` zůstane okno otevřené - toho se využívá
  pro validaci.
