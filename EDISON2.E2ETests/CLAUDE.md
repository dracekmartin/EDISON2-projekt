# EDISON2.E2ETests - UI E2E shellu

FlaUI (UI Automation) ovládá skutečný DEBUG `EDISONMAIN2.exe` spuštěný s `server=… restport=… e2e=1
idfa=…`. Přepínač (parsuje `TestRunOptions`, efekty `EDISONMAIN2/dotnet/Startup/StartupOptions.cs`) dává
běhu vlastní session GUID, vypne hlášení chyb, telemetrii a widget podpory, auto-login, changelog
a výchozí okno a drží preference i geometrii oken jen v paměti. Téma a písmo čte z `%TEMP%\EDISON2.E2E\common`,
kterou harness před startem smaže (výchozí vzhled, vývojářův `m-line\common` zůstane). `preshellpopup=1` navíc
ukáže po přihlášení popup před shellem (`ShellSession.StartBeforeShell`), `login=1` místo DevLogin přihlašovací
okno (`ShellSession.StartWithLogin`).

- **Běh ručně**: `EDISON_E2E=1 dotnet test EDISON2.E2ETests` (bez proměnné Skipped). Pre-push je pustí
  sám, jen když pushovaný rozsah mění shell (`EDISONMAIN2/`, `EDISONMAIN2.LegacyMiniApps/`, `EDControls/`,
  `EDUpdateContract/`, `EDISON2.E2ETests/`, `EDTesting/dotnet/`, vždy mimo `CACHE/`, `.md` a seznamy API `PublicAPI.*.txt`: `Tree.cls`
  pushuje úloha tree-sync);
  předtím sestaví DEBUG shell. Bez VPN, na zamčené ploše nebo při běžícím jiném E2E je přeskočí.
  Potřebuje VPN, přihlášenou plochu a předem sestavený DEBUG shell. ProjectReference na EDISONMAIN2 záměrně není: build by padal na zamčených DLL, když
  z `bin\Debug` běží vývojářův shell (`EDTesting` smí, staví do vlastního `bin`). Přepsání:
  `EDISON_E2E_EXE`, `EDISON_IT_SERVER` / `EDISON_IT_PORT`, `EDISON_E2E_IDFA` (výchozí `CB_DP`; výchozí
  firma DEV serveru má pro DEBUG uživatele prázdný strom).
- **Pokrývá jen DEBUG kompozici**: identita je DEBUG login `martin.hubata` (MLP 201, admin), bez
  update checku, version gate a publish. Testovacím účtem DEV (`TestAccount` z `EDTesting`, bez účtu Skipped)
  se přihlašují jen testy login okna a „Přihlásit jiného uživatele". Chybné přihlášení zkoušet jen s neexistujícím
  jménem. Heslo do výpisu nepatří.
- **Konec session** simuluje `ShellSession.DropActivityLock` (`ALVA.SYS.Login.Logout` pod E2E GUID a startovní
  firmou). `LockClearExt` ani `UkonciUzivatele` nepoužívat: s prázdným GUID zasáhnou celý server.
- **Starý build bez přepínače** se pozná podle chybějícího ItemStatus `E2E` na hlavním okně a
  harness ho zabije. Čistý konec by s DEV GUID přes `LockClearExt` smazal zámky vývojářova shellu.
- **Neklikat** (zapisuje Martinova data nebo stav celého serveru): moduly a uzly stromu, přepínání
  karet, přepínač firmy, OK v profilu (`SaveUser`), „Vyvolat chybu"/„Vyvolat výjimku" (skutečný
  report), „Spustit další EDISON"/„Připojit k databázi" (`e2e=1` nepředávají), „Ano" u brány
  přihlášení (povolí přihlašování celému serveru). Předvolby stromu a sloupců se čtou pod vývojářem.
- **Ovládání přes `Invoke()`/`Expand()`**, ne `Click()`: bez myši a fokusu. Okna hledat jen
  v procesu (vývojářův shell má stejný titulek); modální okna bývají dětmi ownera i top-level,
  podmenu je vlastní popup procesu.
- **Selhání**: nečekané okno s titulkem shodí čekání s výpisem textů. Snímek okna (PrintWindow,
  i zakrytého) a strom UIA jdou do `bin/…/e2e-artifacts`. Proces se zabije, nezavírá (WM_CLOSE
  vyvolá potvrzení konce); zámek pod E2E GUID uklidí příští čistý běh. Souběhu brání mutex
  `Local\EDISON2.E2E`.
