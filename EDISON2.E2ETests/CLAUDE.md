# EDISON2.E2ETests - UI E2E shellu

FlaUI (UI Automation) ovládá skutečný DEBUG `EDISONMAIN2.exe` spuštěný s `server=… restport=… e2e=1
idfa=…`. Přepínač (parsuje `TestRunOptions`, efekty `EDISONMAIN2/dotnet/Startup/StartupOptions.cs`) dává
běhu vlastní session GUID, vypne hlášení chyb, telemetrii a widget podpory, auto-login, changelog
a výchozí okno a drží preference jen v paměti. `preshellpopup=1` navíc ukáže po přihlášení popup před
shellem (`ShellSession.StartBeforeShell`).

- **Běh ručně**: `EDISON_E2E=1 dotnet test EDISON2.E2ETests` (bez proměnné Skipped). Pre-push je pustí
  sám, jen když pushovaný rozsah mění shell (`EDISONMAIN2/`, `EDISONMAIN2.LegacyMiniApps/`, `EDControls/`,
  `EDUpdateContract/`, `EDISON2.E2ETests/`, vždy mimo `CACHE/` a `.md`: `Tree.cls` pushuje úloha tree-sync);
  předtím sestaví DEBUG shell. Bez VPN, na zamčené ploše nebo při běžícím jiném E2E je přeskočí.
  Potřebuje VPN, přihlášenou plochu a předem sestavený DEBUG shell. ProjectReference na EDISONMAIN2 záměrně není: build by padal na zamčených DLL, když
  z `bin\Debug` běží vývojářův shell (`EDTesting` smí, staví do vlastního `bin`). Přepsání:
  `EDISON_E2E_EXE`, `EDISON_IT_SERVER` / `EDISON_IT_PORT`, `EDISON_E2E_IDFA` (výchozí `CB_DP`; výchozí
  firma DEV serveru má pro DEBUG uživatele prázdný strom).
- **Pokrývá jen DEBUG kompozici**: identita je DEBUG login `martin.hubata` (MLP 201, admin), bez
  login okna, update checku, version gate a publish. Jen „Přihlásit jiného uživatele" se přihlašuje
  testovacím účtem DEV (`TestAccount` z `EDTesting`, bez účtu Skipped). Heslo do výpisu nepatří.
- **Starý build bez přepínače** se pozná podle chybějícího ItemStatus `E2E` na hlavním okně a
  harness ho zabije. Čistý konec by s DEV GUID přes `LockClearExt` smazal zámky vývojářova shellu.
- **Neklikat** (zapisuje Martinova data nebo stav celého serveru): moduly a uzly stromu, přepínání
  karet, přepínač firmy, OK v profilu (`SaveUser`), „Vyvolat chybu"/„Vyvolat výjimku" (skutečný
  report), „Spustit další EDISON"/„Připojit k databázi" (`e2e=1` nepředávají), „Ano" u brány
  přihlášení (povolí přihlašování celému serveru). Storno profilu uloží jen geometrii dialogu.
- **Ovládání přes `Invoke()`/`Expand()`**, ne `Click()`: bez myši a fokusu. Okna hledat jen
  v procesu (vývojářův shell má stejný titulek); modální okna bývají dětmi ownera i top-level,
  podmenu je vlastní popup procesu.
- **Selhání**: nečekané okno s titulkem shodí čekání s výpisem textů. Snímek okna (PrintWindow,
  i zakrytého) a strom UIA jdou do `bin/…/e2e-artifacts`. Proces se zabije, nezavírá (WM_CLOSE
  vyvolá potvrzení konce); zámek pod E2E GUID uklidí příští čistý běh. Souběhu brání mutex
  `Local\EDISON2.E2E`.
