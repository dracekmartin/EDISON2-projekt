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

**Dnes to tiše nedělá nic** a to je horší než chybějící featura: dialog „Sestavy na ploše"
(v agendě pod tlačítkem tisku) se normálně otevře, zaškrtávátka jsou prázdná a OK neudělá nic.
`ShortcutExists`/`CreateShortcut`/`RemoveShortcut` mají celé tělo uvnitř
`If Me.MainEdisonForm IsNot Nothing`, bez else a bez chyby, a `MainEdisonForm` je pod
EDISONMAIN2 vždy `Nothing` (getter hledá MDI kontejner, který samostatné okno nemá).

Zvolený směr: **named pipe** (jeden server na shell session, jméno odvozené od `shellGuid`,
který modul už dostává v `args[9]`, takže nový argument netřeba). Transport patří do
`EDISONMAIN2API`, `EDLIBRARY` dostane jen aditivní seam - interface `IShortcutSink` a
`Public Shared ShortcutSink` na `EDForm`, nová **první** větev ve všech třech metodách.
Aditivní proto, že `EDLibrary.dll` referencuje 185 projektů bez strong name a s pevnou
`AssemblyVersion` - dokud se nemění existující signatury, jde o drop-in výměnu dvou souborů
a agendy se nepřebuildovávají.

Varianta „modul zapíše rovnou do Caché" byla zvažovaná a zamítnutá: `UserData.Save` přepisuje
celý blob pro daný klíč, takže by si zápis modulu a přesouvání dlaždic v shellu navzájem
přepisovaly, a shell by se o novince dozvěděl až při dalším čtení. Pipe navíc umožní
`ShortcutExists` (zaškrtávátka v dialogu odpovídají skutečnosti), což přes blob jde taky,
ale s tím rozdílem, že dlaždice naskočí okamžitě.

## Kanál spuštění (shell → modul) - HOTOVO

Legacy: `frmNoMDI.mFrmMain_ShowFormDemand` větví na `CallType`; u `Procedura` volá
`RunProcedure`, který reflexí načte `ALVA.<Balik>.ProcedureIO` a předá `ProcedureIO.Input`
s celým zástupcem (`ReasonEnum.ShortcutCall`).

V EDISONMAIN2 nese zástupce **14. pole arg kontraktu** (`args[13]`, za licencovanými elementy,
takže starší host ho jen ignoruje). Obsah je **Base64 nad XML** legacy
`ALVA.LIBRARY.Remote.SerializableShortcut`:

- **Base64** proto, že `WindowNavigator.BuildLauncherArguments` hodnoty jen obaluje uvozovkami
  a neescapuje je (má na to vlastní test) - syrové XML by rozbilo příkazový řádek.
- **`SerializableShortcut`** proto, že je to `XmlSerializer` nad veřejnými poli (ne
  BinaryFormatter), takže projde mezi .NET Frameworkem a .NET 10. Host ho deserializuje toutéž
  třídou → **jména polí jsou zmrazená**; shellová strana je zrcadlí v `ShortcutLaunchPayload`.

Skládá ho `ShortcutLaunchPayloadCodec` (EDISONMAIN2), posílá `IWindowNavigator.ShowLegacyShortcutAsync`
(EDControls o typu zástupce nic neví - dostane neprůhledný string), zpracuje
`EDISONMAIN2API/ShortcutRunner`:

- `CallType=Procedura` (tisková sestava) → `EDForm.RunProcedure` nad **nezobrazenou**
  hostitelskou formou;
- ostatní druhy → forma jako dnes, ale `EDParamIn` = `DesktopShortcut` / `ReportShortcut`
  **objekt**, ne string - legacy formy (`formESREPORTParams`, `frmReportShow`) to tak čekají.

Dvě pasti, obě ověřené naživo:

1. **ID sestavy musí být číslo.** `GC<T>.FromTypeToString` u enumu dělá `((int)obj).ToString()`
   a zpětný převod jde přes `Conversion.Val`, takže nečíselný text spadne na 0, `PrintController.Tisk`
   nenajde větev a **tiskne se ticho bez chyby**. Hlídá to test `Encode_KeepsReportIdVerbatim`.
2. **Kotva musí přežít start prohlížeče.** `PrintController.Tisk` končí u
   `ShowEdisonFormStandAloneEx`, které spustí `EDPROCESS.EXE` a obsluhuje ho remotingem
   hostovaným v procesu hosta. Když host skončí dřív, zůstane `EDPROCESS` viset **bez okna**.
   Čekat přes `DenyFormClose` nejde - legacy ho nastavuje na `BaseForm` vyrobený uvnitř
   `ProcedureIO` agendy, ke kterému se host nedostane. `ShortcutRunner` proto hlídá samotné
   procesy prohlížeče, které vznikly až po něm. Hotový prohlížeč ukončení hosta přežije,
   rozjetý ne.

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
