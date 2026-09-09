# EDUPDATE2 - klientský updater pro EDISONMAIN2

Samostatný exe: vymění soubory EDISONu na stanici za novější ze serverové instalace
a spustí shell znovu. Volá ho jen EDISONMAIN2 (`Updater.LaunchUpdate`) - zkopíruje si ho
ze serveru k sobě, spustí a ukončí se; updater počká na zánik procesů, vymění soubory,
restartuje shell. Legacy `EDUPDATE.exe` (repo EDISON) zůstává starému shellu.
Kontext: `docs/analyzy/update-mechanismus.md`.

## Invariant: žádné závislosti

Žádná reference na EDControls, MLRest, EDLicence ani nic z `C:\Edison` - tyhle soubory
updater vyměňuje a načtenou assembly Windows zamyká. Proto hardcoded barvy v okně
(výjimka z theme pravidel) a `VerzeInf.cs` source-linkem z EDISONMAIN2, ne referencí.

## Argumenty

Tokeny `Key=value`, staví je `Updater.BuildArguments` v EDISONMAIN2 přes `ArgumentList`.
Neznámý klíč se zaloguje a ignoruje.

| Klíč | Default | Význam |
|---|---|---|
| `SourcePath` | povinný | adresář serverové instalace - síťová cesta `\\server\...` nebo lokální; `http*` odmítne validace |
| `TargetPath` | adresář EDUPDATE2.exe | kam instalovat (shell updater kopíruje k sobě, default = instalace) |
| `Version` | `VERZE.INF` ze zdroje | jen log a no-op pojistka: není-li novější než cíl, skončí se úspěchem bez kopírování |
| `WaitProcess` | `EDISONMAIN2` | jméno procesu bez cesty (`.exe` se ořízne); navíc se čeká fixně na `EDISONMAIN` a `EDUPDATE` |
| `RunExe` | nespouštět nic | jen jméno souboru bez cesty; spustí se z TargetPath po úspěchu i no-op, po chybě nikdy (smyčka detekce) |
| `NucenyUpdate` | `0` | `1` = plná kopie bez ohledu na manifest i verze |
| `NucenyUpdateTime` | nezapisovat stopu | razítko vynucené vlny (viz poslední sekce) |

Stará verze se čte vždy z `VERZE.INF` v cíli - legacy `OldVersion` neexistuje,
shellová `VerzeInf.Current` je v DEBUG fake.

## Průběh a exit kódy

Soubory se nejdřív stáhnou do přechodné složky (cíl nedotčen), pak se vymění se zálohou;
chyba výměny → rollback ze zálohy. Read-only atributy se při výměně čistí (RO ze
serverového share by shodilo příští update). Zdroj bez čitelného `VERZE.INF` se u běžného
updatu odmítne - jinak by se verze neposunula a update opakoval donekonečna. Zavření okna
mimo výměnu update bezpečně přeruší (exit 1, cíl beze změny); během výměny okno zavřít
nejde. Log `_LOG_UPDATER2.txt` - vlastní soubor, legacy si `_LOG_UPDATER.txt` při startu maže.

Exit kódy (legacy sémantika, volající je zatím nečte): `0` úspěch/no-op, `1` chyba před
instalací (cíl beze změny), `100` chyba + rollback uspěl, `200` chyba + rollback selhal.

## Výběr souborů - manifest EDISONCRC.bin

Manifest kontrolních součtů (SHA-256 per soubor), na serveru zákazníka ho generuje
legacy instalátor EDISONINS. Kopírují se jen soubory s neshodným hashem; bez manifestu
nebo při vynucené aktualizaci všechno (root + `EDFiles`, `PS`, `locales`). Konfigurační
soubory (`MLServerConfig-data.xml`, `EDISONini.xml`, `VERZE.inf`, `EDISONCRC.bin`)
se kopírují vždy - manifest je nenese (legacy parita).

Binární formát je zděděný z legacy a čtení musí zůstat bit-kompatibilní - popis po
bajtech je v hlavičce `dotnet/CrcFile.cs`. Dvě pasti: šifrovací klíč je kvůli legacy
bugu odvozený z doslovného textu `"System.Security.SecureString"` + pořadí věty,
a jména souborů se netrimují. Bit-kompatibilitu jistí zlatý vektor z produkce
v `CrcFileTests`.

## Vynucená aktualizace - stopa CliUp2.ini

Hotfix nástroj: opravené binárky se nahrají na server zákazníka a v Caché se nastaví
razítko (`^EDISON("System","Update","NucenyUpdateLocalClientu")`). Stanice s jinou
lokální stopou spustí updater s `NucenyUpdate=1`; po úspěchu se razítko zapíše do
`CliUp2.ini` vedle binárek. Per-instalace, ne per-user (jinak vlna běží za každého
Windows uživatele zvlášť); vlastní soubor, ne legacy `CliUp.ini` (relativní cesta,
čtení bez ochrany).
