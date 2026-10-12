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
  pod `TEST_EDISON2_IT` / MLP 999 s úklidem ve `finally`. Výjimka `LogLogin`: záznam sestavy
  přihlášení úklid přes MLRest nemá, každý běh přidá řádek pod touto firmou.
- **Testovací účet** (`TestAccount` v `EDTesting`, jde použít i z E2E): jméno a heslo nejsou v gitu, drží je DEV
  v globálu `^EDTesting("ITAccount","login")` a `("password")` v namespace MLINE (nastavuje vlastník v terminálu
  Caché, čte `ALVA.EDTesting.ITAccount`). Proměnné `EDISON_IT_LOGIN` + `EDISON_IT_PASSWORD` ho přepíšou.
  Chybí = `[TestAccountFact]` Skipped. Heslo nelogovat, `TestAccount.ToString()` ho neukazuje.
- **Tolerantní parser** (chybějící řádek nebo pole = default) ověřuj i na surovém výstupu, jinak
  přejmenovaný řádek nebo posunuté pole projde. Vzor `ModuleTreeTests`.
- **Surový výstup nelogovat** ani nedávat do assert zpráv: `Login` vrací heslo zpět, oba
  `LoadSettings` jméno a heslo admina.
- **Nevolat** (mění session nebo stav celého serveru):
  - `ALVA.SYS.Login.Login` s reálným účtem - resetuje uživatelské flagy 17 a 20 (neexistující jméno
    smí, `LoginContractTests`),
  - `ALVA.MZDY2.MLAPI.gPocZam` - přepočítá a zapíše počet zaměstnanců do dat firmy a telemetrie,
  - `VerifyLic` - zakládá ACTIVITY zámek a počítá IP do licence,
  - `UkonciUzivatele` bez řádku GUID - odhlásí všechny shelly na serveru,
  - `LockClearExt` s prázdným nebo cizím GUID - prázdný smaže všechny zámky na serveru,
  - `ChangeLoginState` - příznak přihlašování pro celý server,
  - `ResetSettings` / `SaveParams` / `LoadParams` s prázdným MLP - pracují s uživatelem `*`
    (sdílené defaulty).
- Server má nasazený stav pracovního stromu (compile hook při editaci `.cls`), ne pushovaný commit.
