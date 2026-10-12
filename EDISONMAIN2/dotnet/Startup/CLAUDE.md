# Startup shellu - poznámky pro vývojáře

Bootstrap, login, připojení a session shellu; zbytek shellu v [../../CLAUDE.md](../../CLAUDE.md).

## Bootstrap, login, připojení

`AppBootstrapper` nastavuje vzhled/font záměrně před loginem, ať je login dialog themovaný. `Run`
má jednu chybovou hranici → `Shutdown()`: bez ní výjimka skončí v `DispatcherUnhandledException`
(Handled) a app žije dál s `OnExplicitShutdown` a login oknem, které během progress fáze zavření
ruší. Logout hook (`WireSessionLogout`) se zapojuje hned po `Accepted`, ne až se shellem, aby
zámek uvolnil i exit kvůli updatu. **Po návratu z `Run` bootstrapper dožil** - co běží dál, má
vlastní session-scoped objekt (`SessionSwitcher`, `ShellShutdown`); session obou cest skládá
`SessionFactory.Establish`. Debug vs. Release rozhoduje **jen `StartupOptions`** z `App` (nový rozdíl
patří tam, ne do `#if`); `EDISON2_DEVLOGIN=0` pustí v Debug skutečný login dialog (E2E ho ignoruje).

Login okno (jen dialog cesta, ne `DevLogin`) po OK zůstává jako progress podklad, zavírá se až na shell
`ContentRendered`; `HandOffLoginWindow` musí dát `Application.MainWindow` na shell **před**
restore `OnMainWindowClose` (implicitní je login okno) - jinak app umře, nebo proces nemá konec.
`LocalPrefs.ShellWindowBounds` je **jedna geometrie shellu i login okna** (před loginem není
session, do které by šlo ukládat): login ji jen **čte**, zapisuje ji shell a jednou bootstrapper
hned po přihlášení (při zavření je pozdě, shell ji čte dřív). Jméno a heslo (DPAPI blob) se pamatují
**per server** (`LoginNames`/`RememberedPasswords`, klíč `server:port`), heslo se předvyplní jen ke jménu z blobu.

`SettingsLoader` čte `EDISONini.xml` vedle exe (sdílený s legacy, EDUPDATE ho roznáší ze
serveru): **`connection@restport`, ne `@port`** (to je superserver); wizard zapisuje plnou legacy
paritu (`initialized`/`forceChange`=1), uzly mimo ni (`securedMode`) zachovává. Argumenty
`server=<host> restport=<port>` (`ConnectionOverride`) mají přednost a wizard přeskočí: „Připojit
k databázi" jimi spouští **novou instanci** (session v procesu je na jeden server); uložená
připojení žijí mimo `EDISONini.xml`, ten zůstává centrálně řízený (legacy `forceChange`).
Start bez značky `reason=2` od EDLOADERu (pin na liště, zástupce na exe) `App` přesměruje přes
`EDISON.exe` kvůli uzlu NewShell (`EDISONMAIN2_REDIRECTED` brání smyčce); výjimky (připojení, `e2e=1`, `bin\`) kryjí brány `PassesPreLoginGates`.
Update check a vynucená vlna (`Updater`): [EDUPDATE2/CLAUDE.md](../../../EDUPDATE2/CLAUDE.md).

## Session / ACTIVITY zámek

- Zámek vytváří `VerifyLic` při loginu; je v **temp globálu** - hard-kill ho neuvolní (uklidí až idle
  timeout). V dev ho re-run přepíše díky deterministickému GUID (`StartupOptions.SessionGuidTag`); E2E běh
  (`e2e=1`) má vlastní tag, jinak by jeho logout (maže zámky podle GUID) shodil vývojářův shell.
- Po `Accepted` nastaví `LicenceVerifier` sdílené session **`CacheLocks=1`** - requesty obnovují
  idle timer (jinak aktivní shell vypadá serveru jako idle) a detekují zaniklý zámek/verzi.
  Přepnutí firmy jede s `CacheLocks=0` po dobu verify **i logoutu staré firmy** (zámek nové
  firmy ještě/už neexistuje, LockExist by session shodil; `LicenceLoader` je taky request na
  sdílené instanci). Version gate jen při Locks=1: update check proto jede na klonu s `Locks=0`
  (po FULL vydání by jinak `LoadSettings` odmítl a updater se nespustil), DEBUG posílá prázdnou
  `EdisonVersion` (dev verze `999999.9` by gate shodila).
- `SessionLogout.Logout` jede na **klonu** `IMLRest.With(...)`. Kromě ACTIVITY zámku uklízí
  i **business zámky session** (`ALWA.SYS.Lock.LockClearExt` dle GUID, parita s legacy
  `EDUsers.Logout` - pod shell GUID zamykají i hostované moduly, zavírané killem). Úklid je
  **GUID-wide, ne per-IdFa** (`^CacheTempSYSLock` drží i `ACTIVITY/<idfa>~…`) a shell GUID
  přepnutí firmy přežije, proto `CompanySwitcher` po logoutu obnovuje zámek nové firmy
  (`LicenceVerifier.ReacquireLock`); guard `Tests/SessionLockTest`.
- `LogoutWatcher` pollí `maSeUkoncit` s `CacheLocks=2`, aby poll neobnovoval idle timer.
- `CompanySwitcher.Switch` je synchronní (UI thread na dialogy licenčního retry). Zavírání
  dvoufázově: WPF okna shellu **před** swapem Idfa (volají server přes sdílený MLRest), legacy
  moduly až po `Accepted`. Veto okna (`e.Cancel` v Closing) switch ruší - přeživší okno by
  mluvilo s cizí firmou; totéž switch user; confirm jen s moduly.
- Zavření shellu (= konec aplikace i všech agend) potvrzuje v `Closing` `EdisonMainViewModel.ConfirmShellClose`,
  takže **každé programové ukončení po vzniku shellu musí jít přes `ShellShutdown.Force` / `SuppressCloseConfirm`
  na VM**, jinak force logout nebo version gate uvízne na dialogu, který smí uživatel odmítnout.
- `LogLogin` jde po `Accepted` startu a switch user, **ne** po přepnutí firmy (parita). `SyncFromMline` smí
  na klon mimo UI thread: EDLicence `Update` na rozdíl od `Activation` nesahá na modulový globál.
