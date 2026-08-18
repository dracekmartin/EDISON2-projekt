# Plocha se zástupci - model a kanály k legacy modulům

Proč tento dokument: v legacy EDISONu jsou **oblíbené** a **plocha se zástupci** dvě různé
věci a plocha je nadmnožina. EDISONMAIN2 je zpočátku sloučil do jedné dlaždicové plochy
klíčované `ElementId`, takže položka mimo hlavní nabídku (tisková sestava, pohled DS) neměla
jak vzniknout. Dnes je model obecný (`DesktopShortcut`) a oba kanály k legacy modulům stojí;
tady jsou jejich kontrakty a pasti, ať se legacy strana nemusí dohledávat znovu.

## Model položky

| legacy `DesktopShortcutItemType` | `ShortcutKind` | legacy `CallType` | odkud vzniká |
| --- | --- | --- | --- |
| `FormularHlavniNabidky` | `MenuModule` | `Formular` | ★ ve stromu / DnD na plochu (**hotovo**) |
| `FormularDanehoBaliku` | `PackageForm` | `Formular` | modul (registrační kanál) |
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

## Kanál registrace (agenda → plocha) - HOTOVO

Legacy: modul běžící v procesu shellu volá `clsForm.CreateShortcut`; samostatně spuštěný
posílá `SerializableShortcut` .NET Remotingem (`EDLIBRARY/clsForm.vb`, případy 4 = create,
5 = remove). V EDISONMAIN2 jsou moduly **vždy** samostatné procesy a Remoting neexistuje,
takže dialog „Sestavy na ploše" (v agendě pod tlačítkem tisku) se sice otevřel, ale OK
**tiše neudělalo nic**: celé tělo těch metod je uvnitř `If Me.MainEdisonForm IsNot Nothing`,
bez else a bez chyby, a `MainEdisonForm` je pod EDISONMAIN2 vždy `Nothing` (getter hledá MDI
kontejner, který samostatné okno nemá).

Řešení: **agenda zapisuje rovnou na server**, ne shellu. Modul má vlastní MLRest session pod
týmž uživatelem, takže píše do stejného blobu, který shell čte. Mezi procesy tak neexistuje
žádné živé spojení, které by mohlo vypadnout.

- `ALVA.EDControls.DesktopShortcuts` (`Add` / `Remove` / `Exists`) je **doménová operace nad
  blobem** `UserData(scope "EDISONMAIN2", key "Shortcuts")`. Read-modify-write proběhne
  v jednom Caché jobu - přes generické `UserData.Save` by si souběžný zápis modulu a shellu
  přepsal celý seznam.
- **Slot server nepřiděluje** (klient chybějící pozici doplní v `Seed`), ale při přepisu
  existujícího klíče ho **zachová** - jinak by aktualizace z modulu přestěhovala dlaždici.
- `EDLIBRARY` dostala **jen aditivní seam** - `EDFormInterface.IShortcutSink` a
  `EDForm.ShortcutSink` (shared), plus **první** větev ve všech třech metodách (do stávajícího
  těla se pod EDISONMAIN2 stejně nedojde). O úložišti neví nic. Aditivní proto, že
  `EDLibrary.dll` referencuje 185 projektů bez strong name a s pevnou `AssemblyVersion` -
  dokud se nemění existující signatury, jde o drop-in výměnu a agendy se nepřebuildovávají.
- Implementace je `EDISONMAIN2API/CacheShortcutSink`: doplní **cestu k agendě** (`args[11]`)
  a **FQN hostitelské formy** (`EDConversion.Form.GetTypeString`, což umí jen legacy strana),
  JSON skládá ručně, aby host nepotřeboval JSON knihovnu.

**Cena: dlaždice nenaskočí okamžitě.** Shell si blob přenačte, až agenda skončí
(`IWindowNavigator.LegacyModuleExited` → `ReloadShortcutsAsync`).

### Proč ne named pipe

Zkoušeno a zahozeno. Roura fungovala v testech, ale v provozu **zmrazila dialog v agendě**:
`ShortcutExists` se volá pro každý řádek seznamu (VOZIDLA2 jich nabízí přes třicet), takže
každý výpadek kanálu se násobil. Postupně padly tři různé příčiny (dlouhý connect timeout,
accept smyčka, která po výjimce nechala viset rouru bez acceptoru, a čtení odpovědi bez
timeoutu) a po každé opravě se zaseklo jinde. Živý kanál mezi procesy je tu prostě víc
pohyblivých dílů, než featura unese - MLRest je request/response s vlastním timeoutem
a stejnou cestou agenda mluví se serverem celou dobu.

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
