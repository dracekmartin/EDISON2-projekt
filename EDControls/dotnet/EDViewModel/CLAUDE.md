# EDViewModel — poznámky pro vývojáře

Základní třída ViewModelů + okolní infrastruktura: `EDServices` (DI kontejner, singleton
`Current`), `WindowNavigator`, `PopupDisplayer`, `RelayCommand`/`AsyncRelayCommand`,
`EDAction`. API čti přímo ze zdrojáků — tady jsou jen neodvoditelné invarianty.

## AttachToWindow — volat PŘED Show()/ShowDialog()

`WindowNavigator` ho volá automaticky; **shell okno ho v AppBootstrapperu volá ručně**.
Dělá pět věcí:

1. **Klávesy** z `Actions` + vždy **Ctrl+Enter → OkCommand**. OK tlačítko záměrně nemá
   `IsDefault` — samotný Enter slouží EDInputFormu k navigaci mezi poli.
2. **Persistence stavu okna** — hook na `SourceInitialized` (fíruje synchronně uvnitř
   `Show()` před prvním paintem → žádný flicker). Restore pořadí: size → location →
   Maximize **jako poslední** (jinak se ztratí normal-velikost); save bere `RestoreBounds`.
   Obojí best-effort — výjimka se spolkne, okno se otevře vždy.
3. **`PopupDisplayer` se stane window-scoped** — resolve owner: per-call → okno VM →
   globální default. Agendová okna tak nedostávají popupy nad shellem.
4. **Usage telemetrie** (`EDUsageTelemetry`) — OPEN + kliky na **pojmenované** controly
   (ContextMenu/Popup routed event nemusí dorazit), flush dávky do Caché při `Closed`
   (server legacy `...Telemetry.LogUsageBatch`). Vše best-effort, nikdy nehazí. Legacy
   launch trackuje `WindowNavigator.LaunchLegacy` (OPEN si hostovaná forma trackne sama).

5. **Panel Podpora** — `EDSupportPanel` se připojí ke každému oknu (obdoba legacy
   `BaseForm.AttachSideMenu`), opt-out `ShowSupportPanel = false`. Best-effort v try/catch,
   nikdy nesmí zabránit otevření okna. Viz [EDSupportPanel/CLAUDE.md](../EDSupportPanel/CLAUDE.md).

`PersistedFormId` default `"EDISON2.<Window>"` — prefix odděluje od legacy numerických ID
z `EDISFormEnum`; override na `null` = opt-out (vypne i telemetrii).

## Commands

- Vytvářej přes `Command()` / `CommandAsync()` — automatický error handling přes
  `PopupDisplayer`. `CommandAsync` brání double-clicku (`CanExecute=false` během běhu);
  je záměrně `async void` s vlastním guardem.
- **`canExecute` NESMÍ házet** — WPF ho volá při každé UI interakci a výjimka propadne
  mimo náš catch až do `DispatcherUnhandledException`.
- `RelayCommand` bez explicitního displayeru fallbackuje na
  `EDServices.Current.PopupDisplayer` — test, který čeká zachycení výjimky mockem,
  musí mock registrovat přes `EDServices` nebo předat 3. argumentem.

## EDServices

- `EDServices.MLRest` je mutable a sdílená — přepnutí firmy vidí všechny VM automaticky.
  `EDServices.Current` hází před `Initialize` (testy si vytvářejí vlastní instanci s mocky).
- `EDViewModel.HasAccess()` vrací `true`, když `AccessTable == null` (testy, pre-login).
- `IMLRest.With(...)` klon pro jednorázové requesty (poll `CacheLocks=2`, logout jiné
  firmy) — nikdy nemutuj sdílenou instanci (identity copy viz EDControls CLAUDE.md).

## WindowNavigator

- `ShowDialog` ztmavuje ownera přes `WindowDimmer` (refcount), **ne** `IsEnabled=false`
  — to by srazilo controly na 0.5 opacity.
- Legacy launch: sentinel `"EDISONMAIN2"` + 12 polí (server…mlp, formFqn, shellGuid,
  inputParam, agendaPath, licensedElements) — viz `BuildLauncherArguments`.
- Modeless `ShowLegacy`/`ShowLegacyAsync` dedupují: běží-li už proces se stejným
  `exe+forma+inputParam`, jen přenesou focus (`SetForegroundWindow`), nový nespustí
  (`TryFocusExistingLegacy`). Dialogové/blokující varianty ne — ztmavují shell, druhý
  klik tam nejde.
- `shareShellSession=true` → modul sdílí shellův ACTIVITY lock (`CacheLocks=1`).
  Pre-login admin override (`LicenceVerifier`) musí předat `false`, jinak requesty
  padají na `LockExist`.
- Legacy `EDISONMAIN.exe` se spouští napřímo, ne přes host — potřebuje
  `ApplyDefaultInput` retrofit, který host neumí.
- `CloseAll()` je záměrně jen na konkrétní třídě `WindowNavigator`, ne na interface.
- **Owned overlay okna EDControls (`IOverlayWindow`) `CloseAll()` ani „Spuštěné moduly"
  nevidí** — panel Podpora a „Akce probíhá" jsou top-level okna, takže by se jinak nabídly
  jako modul a `CloseAll` by je zavřel (panel nevratně). Nové owned overlay okno tenhle
  marker potřebuje.

## Drobné pasti

- `EDAction.IconElement` vrací **nový** `Image` při každém přístupu — `UIElement` smí mít
  jen jednoho rodiče (nutné pro `MenuItem.Icon` přes `ItemContainerStyle`).
- `OnOk()`: bez `base.OnOk()` / `CloseDialog(true)` zůstane okno otevřené — toho se
  využívá pro validaci.
