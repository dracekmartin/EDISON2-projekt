# EDISON2.IntegrationTests - šev .NET ↔ Caché

Testy volají sdílený dev server skutečným MLRest a odpověď předávají produkční třídě, která ji
v shellu parsuje. Hlídají, co mocky ani Caché testy nevidí: oddělovač `'\0'` (mocky parserů
i `SetupSys` jedou s `'~'`), skutečné kódování CVar / `$lb` přes HTTP a produkční parser nad
skutečnou odpovědí.

- **Běh**: bez dostupného serveru (`DevServer.IsReachable`, TCP 2 s) jsou testy Skipped,
  `EDISON_IT_REQUIRED=1` (pre-push při změně `.cls`/`.inc`) z nedostupnosti dělá chybu.
  Server `EDISON_IT_SERVER` / `EDISON_IT_PORT`, default `192.168.16.1:57770`. Třídy nesou
  `[Trait("Category", "Integration")]` (hlavní .NET běh je vyfiltruje) a `[DevServerFact]` / `[DevServerTheory]`.
- **Identita je pevná** (`DevServer`), ne z `CACHE_USER`: čtení pod `CB_DP` / MLP 1, zápisy jen
  pod `TEST_EDISON2_IT` / MLP 999 s úklidem ve `finally`. Výjimky: `LogLogin` (záznam sestavy
  přihlášení úklid přes MLRest nemá, každý běh přidá řádek pod touto firmou) a `Login` testovacího
  účtu (nuluje jeho uživatelské flagy 17 a 20). Stav, který přes MLRest uklidit nejde, zapisují a mažou helpery
  `ALVA.EDTesting.IT*` jen pod `TEST_EDISON2_IT`: Přehledy (`ITDashboard`), ACTIVITY zámek a požadavek na ukončení
  session (`ITSession`, jen GUID volajícího), usage telemetrie (`ITTelemetry`). Data agend (MHD, ...)
  leží v datovém namespace firmy: `Connect(dataNamespace: DevServer.ReadDataNamespace)`, výchozí je nemá.
- **Testovací účet** (`TestAccount` v `EDTesting`, jde použít i z E2E): jméno a heslo nejsou v gitu, drží je DEV
  v globálu `^EDTesting("ITAccount","login")` a `("password")` v namespace MLINE (nastavuje vlastník v terminálu
  Caché, čte `ALVA.EDTesting.ITAccount`). Proměnné `EDISON_IT_LOGIN` + `EDISON_IT_PASSWORD` ho přepíšou,
  jen jiným vyhrazeným účtem: testy s ním volají `Login`. Chybí = `[TestAccountFact]` Skipped. Heslo
  nelogovat, `TestAccount.ToString()` ho neukazuje. Účet musí mít celé jméno jiné než přihlašovací, osobní
  adresář se zaškrtnutým použitím a e-mail: s výchozími hodnotami by testy `Login` a `LoadUser` posun pole nepoznaly.
- **Tolerantní parser** (chybějící řádek nebo pole = default) ověřuj i na surovém výstupu, jinak
  přejmenovaný řádek nebo posunuté pole projde. Vzor `ModuleTreeTests`.
- **Surový výstup nelogovat** ani nedávat do assert zpráv: `Login` vrací heslo zpět, oba
  `LoadSettings` jméno a heslo admina.
- **Nevolat** (mění session nebo stav celého serveru):
  - `ALVA.SYS.Login.Login` s reálným účtem - resetuje uživatelské flagy 17 a 20 (neexistující jméno
    a testovací účet smí, `LoginContractTests`),
  - `ALVA.MZDY2.MLAPI.gPocZam` - přepočítá a zapíše počet zaměstnanců do dat firmy a telemetrie,
  - `ALVA.EDISON.MiniAplikaceAPI.LoadData` s `refresh=1` - přepočítá a přepíše Přehledy (miniaplikace),
    test si data zapíše přes `ALVA.EDTesting.ITDashboard` pod testovací firmu,
  - `VerifyLic` - zakládá ACTIVITY zámek a počítá IP do licence,
  - `UkonciUzivatele` - bez řádku GUID odhlásí všechny shelly na serveru. Požadavek na ukončení vlastní
    session zapisuje a uklízí `ALVA.EDTesting.ITSession` (`RequestEnd`, `CleanEndRequest`),
  - `LockClearExt` s prázdným nebo cizím GUID - prázdný smaže všechny zámky na serveru,
  - `ChangeLoginState` - příznak přihlašování pro celý server,
  - `ResetSettings` / `SaveParams` / `LoadParams` s prázdným MLP - pracují s uživatelem `*`
    (sdílené defaulty),
  - `ALVA.USERS.ApiEdison.SetUserDefaultForm` s MLP 999 - zapíše pod uživatele systémové databáze uzel
    bez jména a smyčky přes uživatele v `ALVA.SYS.Login` na něm padají. Jen MLP testovacího účtu (`SessionDataTests`),
  - `ChangePassword` s platným starým heslem testovacího účtu - změní heslo, ze kterého testy berou přihlášení.
- Server má nasazený stav pracovního stromu (compile hook při editaci `.cls`), ne pushovaný commit.
