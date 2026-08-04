# Plocha se zástupci - model a chybějící kanály

Proč tento dokument: v legacy EDISONu jsou **oblíbené** a **plocha se zástupci** dvě různé
věci a plocha je nadmnožina. EDISONMAIN2 je zpočátku sloučil do jedné dlaždicové plochy
klíčované `ElementId`, takže položka mimo hlavní nabídku (tisková sestava, pohled DS) neměla
jak vzniknout. Model už je obecný (`DesktopShortcut`), ale **kanály k legacy modulům chybí** -
tady je jejich návrh, ať se legacy kontrakty nemusí dohledávat znovu.

## Model položky

| legacy `DesktopShortcutItemType` | `ShortcutKind` | legacy `CallType` | odkud vzniká |
| --- | --- | --- | --- |
| `FormularHlavniNabidky` | `MenuModule` | `Formular` | ★ ve stromu / DnD na plochu (**hotovo**) |
| `FormularDanehoBaliku` | `PackageForm` | `Formular` | modul |
| `SestavaTiskova` | `PrintReport` | `Procedura` | modul (`ProcedureIO.GetDesktopShortcut`) |
| `SestavaVyberova` | `SelectReport` | `Formular` + `ReportShortcut` | správce sestav (ESREPORT, HIM, SKLADYREPORT) |
| `PohledDatovehoSkladu` | `DwhView` | `Formular` + `ReportShortcut` | DSVIEWER |

Identita zástupce je legacy čtveřice `(Form, ID, CallType, ItemType)`
(`EDISONMAIN/frmMain.vb` `DesktopShortcut.IsEqual`) - v EDISON2 `DesktopShortcut.Key`
(`Kind|ElementId|Id`; `CallType` je odvoditelný z `Kind`). Payload sestav
(`Agenda/Oblast/TypSestavy/Sestava/Predvolba/JeVerejny`) drží `ReportPayload`, protože z něj
server dohledává uložené parametry tisku (`TISK/frmReportShow.vb`
`GetIndexGlobaluPodleUlozenychParametru`).

**Katalog nabízených tisků není v DB.** Je napevno v kódu modulu (`GetDesktopShortcut` /
`MakeShortCut` - `ADSERVIS/Struktury.vb`, `VOZIDLA2/ShortCut/ProcedureIO.cs`) nebo v Caché
`*.VYSTUPY.Api.List`, kde pole 12 `parentForm` existuje právě proto, aby zástupce věděl,
kterou formu spustit. „Umístit na plochu" je jen per-user zaškrtnutí v dialogu
`formEDISONMAINShortcutList` (remove-all-then-recreate nad zaškrtnutými).

## Kanál registrace (modul → shell)

Legacy: modul běžící v procesu shellu volá `clsForm.CreateShortcut`; samostatně spuštěný
posílá `SerializableShortcut` .NET Remotingem (`EDLIBRARY/clsForm.vb`, případy 4 = create,
5 = remove). V EDISONMAIN2 jsou moduly **vždy** samostatné procesy a Remoting neexistuje.

Doporučení: **modul zapíše zástupce rovnou do Caché** - `ALVA.EDControls.UserData`, scope
`EDISONMAIN2`, klíč `Shortcuts` (týž blob, který shell čte; schema viz
`EDControls/DOCS/05-userdata-scopes.md`). MLRest má modul k dispozici, přežije to pád procesu
a nevzniká žádný živý kanál. Shell blob přenačte při ukončení legacy procesu -
`WindowNavigator` spuštěné procesy už sleduje.

Zbytek: `clsForm.CreateShortcut`/`RemoveShortcut` potřebují větev „běžím pod EDISONMAIN2"
(dnes s `MainEdisonForm == null` tiše nic nedělají) a dialog `formEDISONMAINShortcutList`
je forma legacy shellu, kterou EDISONMAIN2 nenasazuje - viz níže.

## Kanál spuštění (shell → modul)

Legacy: `frmNoMDI.mFrmMain_ShowFormDemand` větví na `CallType`; u `Procedura` volá
`RunProcedure`, který reflexí načte `ALVA.<Balik>.ProcedureIO` a předá `ProcedureIO.Input`
s celým zástupcem (`ReasonEnum.ShortcutCall`).

Návrh: rozšířit arg kontrakt `EDISONMAIN2API` (dnes sentinel `"EDISONMAIN2"` + 12 polí,
`EDISON/EDISONMAIN2API/FormToRunGetter.cs`; zrcadlí ho `EDControls/EdisonLaunchArgs`) o 14.
pole = serializovaný zástupce. Host pak:

- `Kind = PrintReport` → poskládat legacy `DesktopShortcut` (`CallType=Procedura`),
  `EDConversion.Form.GetPackage(form)` → reflexí `ProcedureIO.RunProcedure`, po dokončení
  proces ukončit (nezobrazuje se okno);
- ostatní druhy → forma jako dnes, ale `EDParamIn` = `DesktopShortcut` / `ReportShortcut`
  (ne string) - legacy formy (`formESREPORTParams`, `frmReportShow`) to tak čekají.

## Práva

Legacy filtruje zástupce **při načtení** dle licence + `accessTable` podle `shortcut.Form`
(`frmMain.vb` load user settings) a `IsHidden` staví modul sám (`ReportIsAllowed`).
V EDISON2 to pro `MenuModule` zajišťuje strom (server ho vrací už profiltrovaný), u ostatních
druhů musí příznak dorazit s položkou - `Kind`-specifické právo shell nezná.

Pozor na past, kterou už řeší `ModuleShortcutsService`: položka, kterou aktuální strom nezná
(odebraná licence, `hidden`, `mlineOnly`), se **nezobrazí, ale uloží zpátky**. Filtrovat při
ukládání by uživateli tiše smazalo zástupce, které jen v této session nevidí.

## Katalog pro picker

`formEDISONMAINShortcutList` žije v `EDISONMAIN.exe` (legacy shell) a EDISONMAIN2 ho
nenasazuje. Varianty:

1. vlastní WPF picker v EDISON2 plněný out-of-process voláním
   `ProcedureIO.GetDesktopShortcut` (host by seznam vypsal strojově čitelně);
2. `VYSTUPY.Api.List` z Caché tam, kde existuje (dnes jen `ISYADNasCis`, `ISYZpracovani`);
3. ponechat picker v legacy modulu a jen dovolit zápis do blobu (nejmenší zásah, ale dialog
   se pak spouští z modulu, ne z plochy).
