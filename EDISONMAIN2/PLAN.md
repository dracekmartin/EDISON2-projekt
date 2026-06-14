# EDISONMAIN2 — komprehensivní plán

## Context

EDISONMAIN2 je WPF/.NET 10 shell, který postupně nahrazuje legacy WinForms `EDISONMAIN`. Shell sám je ve VB.NET, ale **většina hostovaných agend je v C#** (jen opravdu prastaré jsou ve VB.NET). Aktuálně pilot-ready: login, výběr firmy, 3 stromy modulů, spouštění legacy `.exe` přes `EDISONMAIN2API.FormToRunGetter`, persistence stavu, 199 testů. Koexistuje s legacy EDISONMAIN proti stejné Caché instanci.

Před plnou produkční migrací máme dluhy v základech aplikace, které je třeba uklidit, než přibyde další funkcionalita. Plán nejdřív popisuje **refactoring backlog** existujících problémů, pak **doplnění stubů**, pak **features v pořadí priority**, na konci se okrajově zmiňuje **miniaplikace** a **port legacy modulů** (obojí v zatím nejasné formě).

Tento plán je živý dokument — budeme ho průběžně upravovat podle nově získaných informací.

### Vztah k existujícím dokumentům

- **[`RESPONSIBILITIES.md`](RESPONSIBILITIES.md) NENÍ kanonická specifikace, je to studnice nápadů.** Obsahuje 1:1 port úvah z legacy systému + aspirativní features bez ověřeného user story. Spousta tam uvedených věcí je irelevantní pro EDISONMAIN2 nebo nemá smysl. Při čerpání z RESPONSIBILITIES vždy ověř, jestli má daná věc reálnou hodnotu — neber to jako automaticky validní.
- **[`docs/archive/MVP_PLAN_v1.md`](docs/archive/MVP_PLAN_v1.md)** je historický záznam pilot rozhodnutí. Archivováno při materializaci tohoto plánu — `PLAN.md` je single source od první minuty, MVP_PLAN_v1.md zůstává jako referenční historie pilot fáze.
- **Tento PLAN** je single source of truth pro to, co reálně děláme.

---

## Vůdčí principy

Kotva pro PR review a budoucí rozhodnutí.

1. **Module tree má jeden source-of-truth** — `Tree.cls` (ObjectScript). Žádný hardcoded duplikát v .NET. Runtime editovatelnost zatím nepotřebujeme.
2. **Compile-time safety preferovat tam, kde je technicky možná** (in-process WPF okna a služby v jednom procesu). Cross-process kontrakty (FormToRunGetter cmdline args, jména tříd jako stringy pro legacy WinForms) jsou explicitní výjimka a zůstanou string-based — legacy `.exe` budou existovat dlouho.
3. **Bootstrapping = testovatelný workflow**, ne procedurální `App.xaml.cs`.
4. **`EDServices.Current` čte JEN composition root** (`AppBootstrapper`) a `WindowNavigator` (XAML interop body). VM dostávají services přes ctor a používají property accessory na `EDViewModel` (`MLRest`, `WindowNavigator`, `PopupDisplayer`). Nový kód, který volá `EDServices.Current.XYZ` napřímo, je code-review smell.
5. **Cross-process side effects (legacy spawn) jdou přes `IWindowNavigator`.** Žádný `Process.Start` ve VM.
6. **Komentáře = WHY, ne WHAT** (viz [CLAUDE.md](CLAUDE.md)).
7. **Backward compat s legacy EDISONMAIN** během transition. Sdílený Caché backend, žádný breaking change ve `^||ML`, ACTIVITY zámku, business locks, licenčním formátu.
8. **YAGNI hlídka.** Před přidáním abstrakce: existuje 2+ konkrétní use case dnes? Pokud ne, ne.
9. **Testovatelnost je first-class.** Každá netriviální logika (services, switchers, bootstrap kroky, parsery wire formátů) musí mít xUnit pokrytí (`EDControls.Tests`, `EDISONMAIN2.Tests`). VM logika přes mock `IMLRest` / `IPopupDisplayer` / `IWindowNavigator` (NSubstitute), ne přes integraci proti Caché. Když nový refactor zavádí abstrakci kvůli testovatelnosti (např. extrakce do service), test pokrývající dříve nemožný edge case musí být součástí stejné PR. Bez něj refactor neplní svůj účel.
10. **Předprodukční fáze — neřešit migrace ani backward-compat pro vlastní data.** EDISONMAIN2 dnes nikde reálně neběží (vyvíjíme ve dvou lidech, žádní uživatelé). Když refactor mění lokaci / formát / klíče naší vlastní persistence (`LocalPrefs`, `UserFontPreference`, custom JSON soubory, nově zaváděné Caché globály), **NEPSAT** migrační code path — kontrolu starých formátů, jednorázový import, fallback. Stačí čistá nová cesta. Když po prvním deployu (pilot) přibyde reálný uživatel, migrace se naopak stanou povinnou součástí refactoru. **Backward-compat s legacy EDISONMAIN** (princip #7) je jiná věc — produkce, povinná.

---

## Refactoring backlog

Existující návrhové dluhy, které je třeba uklidit než přibyde další funkcionalita. Sloupce: **Stav** (`Open` / `In progress` / `Done` / `Done, but discuss` / `Deferred`); **CO** krátce shrne problém; **CO + PROČ** je detailní popis včetně dopadu; **KDE** je odkaz do kódu; **JAK** je vysokoúrovňový návrh směru (konkrétní design diskutujeme až při implementaci).

| ID | Sev | Stav | CO (krátce) | CO + PROČ (detail) | KDE | JAK (návrh, vysokoúrovňově) |
|----|-----|------|-------------|---------------------|-----|------------------------------|
| C1 | CRIT | Done | Magic-string + reflexe v `WindowNavigator.Show(...)` pro in-process WPF okna | Volání mělo tvar `WindowNavigator.Show("EDInputFormTestWindow")` — string jméno třídy, které se reflexí dohledalo v entry assembly. Když okno přejmenujeme, kompilátor mlčel; Find Usages na třídě okna nenašel callsite; typo se projevilo až runtime. Pozn.: cross-process legacy spawn (`ShowLegacy*`) je explicitní výjimka — jméno třídy tam musí zůstat string (cílový proces ho najde ve své vlastní assembly). | [WindowNavigator.cs](../EDControls/dotnet/EDViewModel/WindowNavigator.cs) | První iterace: strongly-typed registry (string klíče, registrace v AppBootstrapper) — reflection pryč. **Druhá iterace (M7):** registry odstraněn → direct types `Show(Window)`. |
| C2 | CRIT | Done | `App.xaml.cs` = procedurální bootstrap v jedné metodě | `Application_Startup` má ~100 řádků procedurálního kódu: load LocalPrefs → load appsettings → fetch companies → pick start company → login (s #DEBUG větví) → vytvořit MLRest → set IsAdmin/MLRest do EDServices → VerifyLic (s retry loop pro admina) → wire EdisonActivityFailed → vytvořit ModuleTreeGetter → vytvořit VM → show window → apply font. Žádný step není testovatelný v izolaci; změna pořadí nebo přidání nového kroku je riziková. Logika se navíc duplikuje s F-A5/CompanySwitcher (společný flow). | [App.xaml.cs:21-121](dotnet/Startup/App.xaml.cs#L21) | Vytáhnout do třídy `AppBootstrapper` s pojmenovanými kroky; každý krok je samostatně testovatelný. App.xaml.cs jen zavolá bootstrapper a ukáže okno. |
| C3 | LOW | Done | `AccessLoader.ApplyLegacyExpansionRules` = dead code z 1:1 portu legacy | Metoda obsahuje 8 hardcoded pravidel (`AURIS=38001→30+24`, `BCD=58001..→17/18/19/20/27+24`, `FSSOFT=87001→32+24`, `CHAPS=95001→33+24`, alias `11001→35`, alias `34→1.1`, mini-apps `15→52001..52999`). V legacy mají smysl, protože tam strom uchovává elementId i pro FAMILY/kategorie a access table se kontroluje na všechny uzly. V EDISONMAIN2 má elementId jen EXE uzel (FAMILY se gatuje rekurzivně podle dětí), Tree.cls navíc neobsahuje žádný z cílových IDs (`35`, `1.1`, `52001..52999`). Všechny entries co tato metoda přidává nikdo nečte. Komentáře u kódu navíc matou budoucího čtenáře. | [AccessLoader.cs:75-94](dotnet/Startup/AccessLoader.cs#L75) | Smazat metodu `ApplyLegacyExpansionRules` + její volání. Při doplňování S7 / mini-apps (feature E) ad-hoc ověřit, jestli některé nově přidávané moduly potřebují podobné mapování — řešit tehdy a konkrétně. |
| H1 | HIGH | Done | `Tree.cls` hard-coduje 3 stromy v ObjectScript | `Tree.cls` má `FillMain/FillSystem/FillPartners` metody s explicitními `s ^\|\|ML("OUT",$i(i))=$lb("EXE",...)` voláními. Aktuálně dostatečné (kód = source-of-truth, git history pokrývá změny, žádný migrační skript), ale pokud by někdy přišel požadavek aby admin/zákazník mohl měnit obsah stromu bez deploye, byl by potřeba přesun do Caché global. **Po diskusi: ponecháme `Tree.cls` dokud nebude reálný požadavek na runtime editability.** | [Tree.cls:61-100+](CACHE/ALVA/EDISONMAIN2/API/Tree.cls#L61) | Nedělat nic (decision rozhodnut: ponechat). Princip "single source-of-truth" zajistí, že nikdo přidá hardcoded duplikát ani do .NET. |
| H2 | HIGH | Done | `SelectedCompanyIndex` setter dělá moc věcí | Setter obsahuje confirm popup → close all windows → swap `MLRest.Idfa`/`DataNamespace` → re-volat `LicenceVerifier.TryAcquireSession` → při Refused/Cancelled rollback (restore Idfa, namespace, licence, selected index, raise PropertyChanged) → při Accepted update LocalPrefs. ~50 řádků. VM tak ví o `LicenceVerifieru`, `MLRestu`, `LocalPrefs`, `PopupDisplayeru`, `WindowNavigatoru` — všechno bok po boku. Stejný flow potřebuje i feature "Přihlásit jiného uživatele" (reconnect = re-login + re-acquire). | [EdisonMainViewModel.cs:209-258](dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs#L209) | Vytáhnout do služby `CompanySwitcher` (nebo podobně), která zapouzdří swap + rollback. VM jen volá `_switcher.Switch(target)` a reaguje na výsledek. Stejnou službu pak konzumuje i Reconnect feature. |
| H3 | HIGH | Done | `PopupDisplayer` hledá owner přes `Application.Current.Windows.IsActive` | Aktivní okno se vyhledává pro každý popup dynamicky přes LINQ nad `Application.Current.Windows`. Funguje na UI threadu, ale je to flaky v background threadu (`Application.Current` může být null nebo `IsActive` se může změnit mezi checkem a `ShowDialog`). | [PopupDisplayer.cs:8-9](../EDControls/dotnet/EDViewModel/PopupDisplayer.cs#L8) | Ctor přijímá `defaultOwner` (typicky shell okno, nastavené z AppBootstrapperu); každá metoda navíc volitelný per-call override. IsActive search se ruší — pokud owner == null po obou krocích, popup se zobrazí jako top-level (WPF to zvládne). |
| H4 | HIGH | Done | `ExecutableModule` má všechny `public set` properties | `DisplayedName`, `ExecutablePath`, `FormName`, `ElementId`, `IsLicensed`, `InputParam` — všechno mutable po konstrukci, žádná validace. Pattern v `ModuleTreeGetter.ParseTree` konstruuje přes object initializer, takže immutability je realisticky možná. Mutace po konstrukci se nikde neděje, ale nic to neforsuje. | [ExecutableModule.cs:5-22](dotnet/Windows/EdisonMainWindow/Modules/ExecutableModule.cs#L5) | Převést na `record` nebo `init`-only properties. `ParseTree` konstruuje přes `new(...)` nebo `with`. |
| H5 | HIGH | Done | Duplicate company selection logic — startup vs. runtime switch | `App.xaml.cs:42-45` vybírá výchozí firmu při startu (priorita: LocalPrefs.LastIdfa → IsDefault → companies[0]). `EdisonMainViewModel.SelectedCompanyIndex` setter (H2) řeší runtime switch. Dvě cesty se snadno rozdrátují — drift mezi nimi vede na bugy (např. startup nezná retry s admin USERS.exe spawnem; runtime ho dělá). | [App.xaml.cs:42-45](dotnet/Startup/App.xaml.cs#L42) vs. [EdisonMainViewModel.cs:209-258](dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs#L209) | Sloučit do `CompanySwitcher` z H2 — service nabídne metody `PickInitial(companies, prefs)` a `Switch(target)` se sdílenou logikou retry/rollback. |
| H6 | HIGH | Done | Post-user-switch stale references | `EdisonMainViewModel._licenceLoader` (capture v ctor), `CompanySwitcher.mlRest`/`licenceLoader`/`isAdmin` (capture v ctor) byly bound na **initial** MLRest. Po S2 user switch `services.MLRest` ukazoval na novou instanci, ale CompanySwitcher.Switch mutoval OLD MLRest a `RefreshLicense`/CompanySwitcher.Switch volaly OLD licenceLoader. | EdisonMainViewModel.cs, CompanySwitcher.cs | Pattern "services = source of truth": přidán `EDServices.LicenceLoader` field (composition root nastavuje v `AppBootstrapper.EstablishSession`). CompanySwitcher ctor zúžen na `(EDServices, ILocalPrefs, Action)` — MLRest/LicenceLoader/IsAdmin/Popup/Nav čte lazily z services. VM dropl `Func<Lic?> licenceLoader` ctor param, `RefreshLicense` čte `services.LicenceLoader`. Bonus: ctor signatury hubenější (CompanySwitcher 7→3 args, VM 10→9). |
| M1 | MED | Done | `RelayCommand` fallback `new PopupDisplayer()` bez owneru | Když `RelayCommand` nedostal `popupDisplayer` v ctoru a handler vyhodí výjimku, vytvoří `new PopupDisplayer()` na místě a zavolá `.Error(ex)`. Popup nemá ownera. | [RelayCommand.cs:16](../EDControls/dotnet/EDViewModel/RelayCommand.cs#L16) | Fallback změněn z `new PopupDisplayer()` na `EDServices.Current.PopupDisplayer` (composition root → owner z `H3` wiringu). `popupDisplayer` ctor parameter zůstává optional. VM callsites v TD migrovány na `Command(...)` factory (per-window scoped owner). |
| M2 | MED | Done | `async void` v `EDComponentAndViewModelTestViewModel.SimulateAction` | `async void` znamená, že výjimka jde přes `SynchronizationContext.Post` a obejde RelayCommand catch blok. Pro test VM ne kritické, ale jako vzor pro budoucí port modulů (kde se může objevit async operace) je špatný precedent. | [EDComponentAndViewModelTestViewModel.cs:36-40](dotnet/Windows/EDComponentAndViewModelTestWindow/EDComponentAndViewModelTestViewModel.cs#L36) | Použít `CommandAsync(...)` (existuje na `EDViewModel`) místo `async void`. |
| M3 | MED | Deferred | `ShowLegacyAsDialog` cross-process bounce — fragile | Pattern: shell deaktivuje `IsEnabled` ownerové okno, zaregistruje `Activated` handler co volá `SetForegroundWindow` na child window, čeká na `Exited` event (na thread pool), pak v dispatcheru reenabluje a aktivuje. Funguje, ale je závislý na timing eventů. Pokud shell padne během dialogového child procesu, ownerové okno zůstane disabled. | [WindowNavigator.cs:51-87](../EDControls/dotnet/EDViewModel/WindowNavigator.cs#L51) | Zatím nedělat — funguje. Až bude reálný race incident, přepsat na čistší Win32 (např. `EnableWindow`/`SetParent`) nebo zvážit jiný model. |
| M4 | MED | Done | `WindowNavigator.ResolveExecutablePath` fallback `C:\Edison` hardcoded | Když relativní path nenajde v `AppContext.BaseDirectory`, fallback je `Path.Combine(@"C:\Edison", relativePath)`. Funguje, protože deploy chodí do `C:\Edison\`, ale je to konstanta v kódu — když se cílový adresář někdy změní, hledá se těžko. | [WindowNavigator.cs](../EDControls/dotnet/EDViewModel/WindowNavigator.cs) | `EdisonDeployRoot` jako `public const` v `WindowNavigator` (reused i v `OpenTemplateDesigner` fallback). `ResolveExecutablePath` přesunut z `private static` → `internal static` + 2 testy pokrývající obě větve (local exists → local; not found → C:\Edison fallback). Stretch (čtení z appsettings) zatím YAGNI — jediný target zůstává `C:\Edison\`. |
| M5 | MED | Done | `ApplyFontToMainWindow` static + font se aplikuje jen někde | Dva spojené problémy. (a) Static metoda na VM volaná z App.xaml.cs sahá na `Application.Current.MainWindow` — VM propojená s globálním WPF stavem, není testovatelné. (b) Reálně **font se aplikuje jen na část UI** (`TreeView` modulů přes WPF inheritance), ale **ne na horní `Menu` ani `StatusBar`** — implicit styly těchto kontrolek nastavují vlastní `FontFamily`/`FontSize`, což přebije inheritance z `Window`. | [EdisonMainViewModel.cs:381-396](dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs#L381) | Vytáhnout do `FontService` (volaná z `AppBootstrapper`). Pro propagaci na Menu/StatusBar přidat implicit `Style` pro `MenuItem` a `StatusBarItem` se `Setter` na `FontFamily`/`FontSize` bound na `Window` (případně v `App.xaml` jako globální merged resource). Refaktorovat v rámci F-A2 (společně s AppBootstrapperem). |
| M6 | MED | Done | Orphan legacy procesy po hard-kill shellu | Když VS killne shell process (Stop debugging) nebo shell crashne, `Process.Start` v `LaunchLegacy` nevolá `CloseAll` — kernel terminate bypassuje WPF Closed event. Legacy okna otevřená ze stromu (např. MHD2PlanDopravy) zůstanou viset a uživatel je musí zavírat ručně. V produkci se projeví při crashi, v dev je to denní pain při VS Stop. | [KillOnExit.cs](../EDControls/dotnet/EDViewModel/KillOnExit.cs) | `KillOnExit.Bind(process)` — Win32 Job Object s `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE` schovaný za 1-řádkové API. `WindowNavigator.LaunchLegacy` bindne spawned process hned po `Process.Start`. Když parent zemře jakkoli, kernel auto-killne celý Job. Unit testy skipped (integration territory — vyžaduje reálné procesy), verifikace smoke-testem: pusť shell ve VS, otevři legacy modul ze stromu, klikni Stop debugging → child proces by měl zaniknout. |
| M7 | MED | Done | `WindowRegistry` přidává string klíče bez reálného use case | C1 první iterace nahradila reflexi za `WindowRegistry` (klíč → factory). Tím se odstranila reflexe, ale přibyl boilerplate `RegisterShellWindows` v AppBootstrapper a string klíče v callsites (`Show("...")`). Typo až runtime, Find Usages na třídě okna nenajde callsite. Cross-agenda cycle riziko (Mzdy ↔ Řidiči) je dnes hypotetické — separátní agenda projekty s bidirectional navigation neexistují, port modulů je v PLAN.md "strategie zatím není rozhodnutá". | [WindowNavigator.cs](../EDControls/dotnet/EDViewModel/WindowNavigator.cs), [EdisonMainViewModel.cs:84-86](dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs#L84) | Smazat `WindowRegistry` + `RegisterShellWindows` + `Show(string)` overloady → `Show(Window)` přijímá instanci. Caller dělá `new EDInputFormTestWindow()`. Compile-time safety. Pokud cross-agenda use case přijde, registry / interfaces / events se přidají aditivně (~30 řádků) bez touch existujících callsitů. |
| L1 | LOW | Done | Komentář-na-WHY audit | Některé komentáře v `EdisonMainViewModel` popisují WHAT (co kód dělá) místo WHY (proč zrovna takhle), což je v rozporu s [CLAUDE.md](CLAUDE.md) pravidlem. Není kritické — jen sjednotit. | EdisonMainViewModel a okolí | Audit hotov (2026-05-22): komentáře v `EdisonMainViewModel`, `AppBootstrapper`, `CompanySwitcher` jsou v drtivé většině WHY (FontDialog jediný standard picker; shared font.json kvůli TD sync; `Application.Current.MainWindow` interop; legacy reference k `frmNoMDI.vb` line numbers; tradeoff OpenTemplateDesigner standalone). Opraveny stale reference na `App.xaml.cs` → `AppBootstrapper` po C2 refactoru (3 místa v VM, CLAUDE.md, CompanySwitcher docstring). Žádné WHAT komentáře k mazání. |
| L2 | LOW | Deferred | `StandardActionsAccessor.FindString` hardcoded český fallback | Fallback texty "Přidat", "Upravit", "Smazat" jsou hardcoded v `EDViewModel.StandardActionsAccessor`. Pokud `EDStrings.xaml` resource není dostupný, použije se čeština — což pro pilot OK, ale brání budoucí lokalizaci do jiného jazyka bez úpravy kódu. | [EDViewModel.cs:217-221](../EDControls/dotnet/EDViewModel/EDViewModel.cs#L217) | **Deferred:** po review (2026-05-22) — řešení dává smysl až v rámci lokalizační infrastruktury (feature #4 v sekci Features). Samostatný refactor jen na fallback by zaváděl polovičatou abstrakci bez konzumenta. Otevřít až feature #4 začne. |
| L3 | LOW | Deferred | Inkonzistentní Window title `[Idfa]` suffix | `WindowNavigator.CreateWindow` appenduje `[Idfa]` do `window.Title` při Show/ShowDialog, ale shell okno (`EdisonMainWindow`) si title bind-uje sám přes `{Binding WindowTitle}`. | `WindowNavigator.cs:146-147` + `EdisonMainWindow.xaml:13` | **Deferred:** po review (2026-05-22) — user vidí `[Idfa]` na každém okně (shell přes binding, ostatní přes navigator append), žádná visible inkonzistence. "Forced chokepoint" by zaváděl infrastrukturu pro neexistující problém. Otevřít teprve když někdo začne přidávat okna mimo `WindowNavigator` (porušení konvence) NEBO když přibude požadavek na jednotný formát title napříč okny. |

---

## Komprehensivní review po dořešení backlogu

Po uzavření všech `Open` položek z refactoring backlogu (případně i stubs S1–S9) udělat jednu vědomou pauzu před přechodem na features. Cíl: ověřit, že refactory nepřinesly regression, že VŠECHNA stávající funkcionalita pořád chodí, a že nově vzniklé services / chokepointy mají test coverage odpovídající principu #9.

**Co projít:**

- **Smoke-test funkcionalita** — projít každou existující featuru shellu manuálně proti aktuálnímu buildu (per `Co je hotové` v [CLAUDE.md](CLAUDE.md)): login, výběr firmy + přepnutí, spouštění legacy `.exe`, persistence stavu oken, ACTIVITY zámek + force-logout flow, font apply, menu pohyby, error popupy. Aspoň happy path každé feature. Cíl: žádný refactor nesmí nevědomky shodit existující práci.
- **Test coverage audit** — projít všechny services / parsery / switchery vzniklé v rámci backlogu (`AccessLoader`, `ModuleTreeGetter`, `CompanySwitcher`, `LicenceVerifier`, případně `AppBootstrapper` po C2, `FontService` po M5) a zkontrolovat, že každá netriviální větev má xUnit test. Chybějící doplnit. Cíl: refactor zavádí abstrakce kvůli testovatelnosti — neměl by skončit bez testu.
- **Edge cases test plán** — sepsat seznam edge cases které dnes nemají test (např. `CompanySwitcher.Switch` při Refused/Cancelled outcome + rollback; `LicenceVerifier` retry loop s admin cancel; `AccessLoader` se starým apId; persist/restore okno se starým form ID). Pro každý buď přidat test nebo zdůvodnit proč netřeba (typicky external integration test out of scope).
- **Dead code sweep** — `grep` po `NotYetImplemented`, `TODO`, `FIXME`, `[Obsolete]`, `#if false`, prázdných catch blocích. Vyhodnotit co je legit (stuby čekající na implementaci → P (PLAN.md)) a co je sirotek po refactoru → smazat.
- **CLAUDE.md / RESPONSIBILITIES.md sync** — projít, jestli `Co je hotové` v `CLAUDE.md` odpovídá realitě a `RESPONSIBILITIES.md` má disclaimer "idea pool, ne canonical spec" (TODO z Context sekce tohoto plánu).

**Kdy:** po posledním uzavřeném backlog itemu, před přechodem na první **Feature** z dolní části plánu (#1 Updater). Materializuje se jako checklist v PR popisu / commit zprávě, ne jako další refactor sprint.

**Výstup:** krátký zápis (1-2 odstavce) v commitu / poznámkách: co projito, co opraveno, co odloženo s důvodem. Slouží jako audit trail před tím, než začneme stavět nové features na (předpokládaně) zdravé základně.

### Provedeno (2026-06-12)

Statická část review (vše kromě manuálního smoke-testu běžící app):

- **Test coverage audit** — services/parsery/switchery projity proti test souborům. Plně pokryté: `CompanySwitcher`, `LicenceVerifier`, `AccessLoader`, `ModuleTreeGetter`, `Updater`, `VerzeInf`, `AppBootstrapper.Run` (4 exit pathy + update). Doplněno 10 testů na nalezené P1 díry: `AppBootstrapper.SwitchUser` mapování 3 outcome armů (Success/Refused/Cancelled), `EdisonMainViewModel.IsAllowed`/`FilterFamily` 3. vrstva (aplikační `AccessTable` — dřív netestováno), `SelectedCompanyIndex` Refused→rollback indexu + popup. Testy 139 → 149.
- **Dead-code sweep** — 0 kritických nálezů. Žádný orphan, `[Obsolete]`, `#if false`, zakomentovaný kód. `NotYetImplemented` (S1) a `ShowMiniApps`/`ShowWindowsAsDialogs` bez consumera (S3) jsou vědomé stuby v tomto plánu. Odloženo s důvodem: `SettingsLoader` catch polyká i rozbitý `appsettings.json` (→ tichý fallback na dev default) a `WindowNavigator` kill-error jen do `Console.WriteLine` — obojí best-effort, oprava = vlastní rozhodnutí o vyústění chyby, ne checkpoint drive-by; pre-production bez reálných uživatelů.
- **CLAUDE.md sync** — doplněn Updater do „Co je hotové" + `Updater`/`VerzeInf` do struktury; update check přidán do startup workflow popisu.

Neprovedeno: **manuální smoke-test** existujících features proti běžícímu buildu (login, přepnutí firmy, spuštění legacy `.exe`, persistence oken, force-logout, font, menu) — vyžaduje běžící WPF app, na uživateli. Plný build zelený, 390 testů (149 EDISONMAIN2 + 241 EDControls).

---

## Doplnění chybějících části shellu (stuby)

Existující kód má placeholder položky (`NotYetImplemented`, `IsEnabled=False`, prázdné menu). Tyto věci je třeba doplnit, většinou jednoduché. V rozsahu několika hodin až 1 dne každá.

**K diskusi při řešení S7+S8+S9:** tvar Tree.cls zadávání modulů — současný formát `$lb("EXE", nazev, exe, form, elementId, licensed, inputParam)` je ukecaný a každé další pole (hidden, color) ho zhoršuje. Možnosti: zachovat $lb pozice s lepšími komentáři / přejít na jmenované klíče / DSL helper metoda (`..AddExe(name, exe, form, id, ...)`).

| ID | Stav | CO | KDE | PROČ |
|----|------|----|-----|------|
| S1 | Open | "Připojit k databázi" command → `NotYetImplemented` | [VM.cs:79](dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs#L79) | Legacy `frmConnection.vb` ekvivalent — admin/uživatel může v běhu přepnout připojení k jiné Caché instanci (jiný server / port). Doteď v EDISONMAIN2 jen přes restart s úpravou `appsettings.json`. Bude potřeba WPF dialog s connection listem. |
| S2 | Done | "Přihlásit jiného uživatele" — reconnect bez restartu | [AppBootstrapper.SwitchUser](dotnet/Startup/AppBootstrapper.cs), [VM.LoginAsAnotherUser](dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs) | `AppBootstrapper.SwitchUser` paralela startup `Run`: prompt login (prázdný — uživatel zadá od začátku) → closeAllWindows → sdílený `EstablishSession(ConnectionInfo, login, services)` (nový MLRest, services.MLRest/IsAdmin/UserPrefs/LicenceLoader reset) → wire EdisonActivityFailed → LicenceVerifier. VM dostane `Func<SwitchUserOutcome>` v ctoru. Cancel → return; Refused/Cancelled po swapu → Shutdown (původní session už zničena closeAllWindows). Sjednocení s initial bootstrap eliminuje drift. H6 (stale references) doposupně vyřešen pattern "services = source of truth". |
| S3 | Open | `ShowMiniApps` / `ShowWindowsAsDialogs` persistují bez consumera | [VM.cs:306-328](dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs#L306) | Uživatelské toggles uložené do `LocalPrefs`, ale nikdo je nečte. `ShowMiniApps` čeká na miniapps feature, `ShowWindowsAsDialogs` na případnou logiku dialog vs. modeless launchování. |
| S4 | Done | INFO label "Dnes je dd.MM.yyyy, svátek má X" | [EdisonMainWindow.xaml:92](dotnet/Windows/EdisonMainWindow/EdisonMainWindow.xaml#L92) | Statický český nameday seznam (`CzechNamedays`) v .NET — bez Caché roundtripu. Státní svátky bez jmenného nositele (Nový rok, Svátek práce, ...) mají null entry → suffix se vynechá. `BuildInformationText` testovatelný v izolaci, `InformationText` property na VM. Bez timer refreshe — typicky shell se denně restartuje. |
| S5 | Done | "Spuštěné moduly" menu — legacy procesy + WPF okna | [EdisonMainWindow.xaml:65-78](dotnet/Windows/EdisonMainWindow/EdisonMainWindow.xaml#L65) | `WindowNavigator.GetActiveModules()` sjednocuje legacy child procesy (`_legacyProcesses` rozšířen na `LegacyProcessEntry` se exe/formFqn pro fallback label) a WPF okna z `Application.Current.Windows` (kromě MainWindow). Display label: `Process.MainWindowTitle` → last FQN segment → exe filename. Activate pro WPF dělá restore z minimized + Focus, pro legacy `SetForegroundWindow`. Dynamický submenu přes `SubmenuOpened` event trigger. |
| S6 | Done | Skryté klávesové zkratky (Ctrl+W+W chord + Ctrl+Shift+B) | [EdisonMainWindow.xaml.cs](dotnet/Windows/EdisonMainWindow/EdisonMainWindow.xaml.cs), retrofit [Utilities/Program.vb](../../EDISON/Utilities/Utilities/Program.vb) | Per RESPONSIBILITIES §15.1: dvě skryté zkratky. **Ctrl+W+W** — chord detection v `EdisonMainWindow.PreviewKeyDown`, stav v VM (`IsChordWaiting` + `ChordHintText` status bar binding), po druhém Ctrl+W volá `WindowNavigator.ShowLegacy("UTILITIES.exe", "ALVA.UTILITIES.formUTILITIESMain")` — UTILITIES.exe spustí vlastní `FrmPassword` (legacy date-dependent vzorec) jako single source of truth pro auth + retry-on-wrong-password built-in. **Ctrl+Shift+B** — KeyBinding na `ShowBgTasksCommand` (už wired). UTILITIES.exe retrofittěn (Program.vb + FormToRunGetter + EDISONMAIN2API ref + `MySubMain=false`). |
| S7 | Done | Hlavní nabídka — 28 modulů chybí | [Tree.cls](CACHE/ALVA/EDISONMAIN2/API/Tree.cls) | Ručně doplněno 28 stub agend kolem MHD (abecedně dle legacy `edisonnavbar.xml` id=-2). Každá stub = top-level EXE entry s name + elementId (z navbar tree id), prázdné exe/form, `licensed=0` (pro viditelnost v dev), color `#FFA500` (oranžová stub konvence). Sub-tree každé stub agendy přijde s jejím portováním. Skript pro re-sync nepsán — per YAGNI, jednorázový dump stačí. |
| S8 | Done | `hidden` atribut filtrování stromů (4. vrstva [RESPONSIBILITIES § 5.2](RESPONSIBILITIES.md)) | [Tree.cls](CACHE/ALVA/EDISONMAIN2/API/Tree.cls), [ModuleTreeGetter.cs](dotnet/Windows/EdisonMainWindow/Modules/ModuleTreeGetter.cs), [EdisonMainViewModel.cs](dotnet/Windows/EdisonMainWindow/EdisonMainViewModel.cs) | `$lb("EXE", ...)` rozšířen o 8. pozici `hidden`. `ExecutableModule.Hidden` property, `ModuleTreeGetter.ParseTree` defensive čte pozici 8. `EdisonMainViewModel.IsAllowed` přidává Hidden check (vždy filtruje, nezávisle na licenci / accessu). Tree.cls refaktorován na DSL helpers `..AddFamily / .AddFamilyEnd / .AddExe`. |
| S9 | Done | Předávání barev modulů do WPF rendering | [Tree.cls](CACHE/ALVA/EDISONMAIN2/API/Tree.cls), [HexToBrushConverter.cs](../EDControls/dotnet/EDViewModel/HexToBrushConverter.cs), [EdisonMainWindow.xaml](dotnet/Windows/EdisonMainWindow/EdisonMainWindow.xaml) | `$lb("EXE", ...)` rozšířen o 9. pozici `color` (hex `"#RRGGBB"`). `ExecutableModule.Color` property. `HexToBrushConverter` v EDControls (prázdná hodnota → `DependencyProperty.UnsetValue` → fallback na default theme Foreground). TreeView DataTemplate binduje `TextBlock.Foreground` přes converter. Konvence: `"#FFA500"` (oranžová) = modul je stub bez retrofitnuté `.exe`. |

---

## Features v pořadí priority

Funkční rozšíření, která EDISONMAIN2 ještě nemá. Pořadí podle priority — můžeme změnit podle pilot feedbacku nebo zákaznických požadavků. Ke každé feature je krátký popis CO + PROČ + případně směr JAK; konkrétní design diskutujeme až při implementaci. Každá feature má **Stav** (`Open` / `In progress` / `Done` / `Deferred`).

### 1. Updater

**Stav:** Done — reuse legacy infrastruktury; kompletní popis flow + research v [docs/UPDATE_FLOW.md](docs/UPDATE_FLOW.md).

**Co se udělalo:**

- **Startup check (klientská větev)** — [Updater.cs](dotnet/Startup/Updater.cs) + [VerzeInf.cs](dotnet/Startup/VerzeInf.cs), hook `AppBootstrapper.CheckForUpdate` po loginu + licenci. Porovnání lokální `VERZE.INF` proti serverové instalaci (`localSourcePath`/`edisonPath` z `ALVA.SETUP.Main.LoadSettings`) + Caché verzi. Mandatory — info popup bez volby, kopie čerstvé `EDUPDATE.EXE`, spawn, shutdown.
- **Runtime gate** — `EstablishSession` nastavuje `MLRest.EdisonVersion` z lokální `VERZE.INF` → server-side per-request kontrola (date+full segmenty, patch tolerován); `EdisonVersionFailed` handler → popup + shutdown. Na dev neaktivní (prázdný version global).
- **Úprava EDUPDATE** (legacy repo `UPDATERCL`, zpětně kompatibilní) — nové args `WaitProcess=` (čekání na zánik EDISONMAIN2 procesu) a `RunExe=` (restart EDISONMAIN2.exe místo EDISON.exe).
- **Verze ve status baru** (`Verze: X` jako legacy slbVersion) — `VerzeInf.Current`: DEBUG build = `999999.9` (dev verze, netriggeruje update — dev guard v `Updater.Check` přeskočí i Caché mismatch), RELEASE = `VERZE.INF` vedle exe.

**Vědomé divergence od legacy:** `Enabled` se honoruje vždy (legacy klient ho přes ForceYes ignoruje) — dev server má `update~0` a nedosažitelný `edisonPath`. Žádná optional větev (HTTP check release.m-line.cz je serverová/admin role, ne klientská).

**Deployment prerekvizity (ops, ruční):** `EDISONMAIN2.exe` přidat do `FILES_DESCRIPTION.INF` na Cloud2; upravený `EDUPDATE.EXE` vyjde s příští verzí.

**Mimo scope (zatím):** `NucenyUpdate`/`CliUp.ini`, maSeUkoncit (= feature #2), save-and-exit integrace (= feature #3), server-side cokoliv.

**Co NEPOTŘEBUJEME:** Server-coordinated `UpdateLock.cls`, optional HTTP větev na stanici, two-tier server/local rozlišení, XMLPatch granularita na klientu, AutoDownload/AutoInstall.

**Tests:** [VerzeInfTests](../EDISONMAIN2.Tests/VerzeInfTests.cs), [UpdaterTests](../EDISONMAIN2.Tests/UpdaterTests.cs), [AppBootstrapperTests](../EDISONMAIN2.Tests/AppBootstrapperTests.cs) (CheckForUpdate flow).

**Stav ověření:**

- ✅ **Status bar (DEBUG)** — shell ukazuje `Verze: 999999.9`, start neblokován (dev `update~0`).
- ✅ **EDUPDATE sandbox** — nové `WaitProcess=`/`RunExe=` ověřeny izolovaně proti temp adresářům: updater čeká na zánik procesu `EDISONMAIN2`, teprve pak kopíruje, a restartuje `EDISONMAIN2.exe` (ne `EDISON.exe`). Log potvrdil pořadí wait→copy→restart.
- ⏳ **Plný E2E** (Release publish, stará verze na stanici × novější serverová instalace, dočasné `update~1` + `localSourcePath` na dev Caché) — neprovedeno; vyžaduje koordinaci s kolegou (sdílený dev server) + revert globálů.
- ⏳ **Runtime gate E2E** (dočasné `^EDISON("System","Update",2)` ≠ klient → `EdisonVersionFailed`) — neprovedeno; nejrizikovější pro běžící legacy klienty, jen domluvené okno + okamžitý revert.

Reference: [docs/UPDATE_FLOW.md](docs/UPDATE_FLOW.md) (kompletní legacy research s file:line refs), legacy [clsUpdate.vb](../../EDISON/EDISONMAIN/clsUpdate.vb), [UPDATERCL](../../EDISON/UPDATERCL/).

### 2. Cooperative logout countdown

**Stav:** Done — pre-warning countdown před server force-logoutem.

**Co se udělalo:**

- **`LogoutWatcher`** ([Startup/LogoutWatcher.cs](dotnet/Startup/LogoutWatcher.cs)) — `DispatcherTimer` 60 s pollující `ALVA.SYSTEM.Main.maSeUkoncit` přes `IMLRest`. Při `CacheLocks=2` po dobu volání (poll nesmí obnovit ACTIVITY historii, jinak by se server-side idle timer resetoval a logout by nikdy nenastal). Parse `ukoncit~{0|1}~{sekundy}` (floor 60). `Poll`/`Parse` static = testovatelné přes mock `IMLRest`.
- **`LogoutCountdownWindow`** ([Windows/LogoutCountdownWindow](dotnet/Windows/LogoutCountdownWindow/)) — non-modální top-most okno v rohu, odpočet po sekundách, po vypršení `Application.Shutdown`. Informativní (server rozhodl), bez možnosti zrušit — věrné legacy.
- **Wiring** — `AppBootstrapper.BuildShellWindow` spustí watcher (poll přes `services.MLRest` → následuje swap při switch user), `wnd.Closed` → `Stop`.
- **`IMLRest.CacheLocks`** — přidán na abstrakci (general MLRest property jako `Delimiter`) + `MLRestAdapter`.

**Producent flagu:** server-side idle timeout (`Activity.cls` → `nastavUkonceniUzivatele`). Admin remote-logout cesta v Caché existuje, ale není volaná; pre-update ukončení je oddělený mechanismus.

**Mimo scope (vědomě):** save-and-exit integrace (= feature #3), tlačítko „Pokračovat v práci" (extend přes real request), Windows idle detection na klientu (legacy ho nedělá — idle se počítá server-side), zobrazení `cerpani` (legacy ho taky nezobrazoval).

**Tests:** [LogoutWatcherTests](../EDISONMAIN2.Tests/LogoutWatcherTests.cs) — Parse matrix + Poll (CacheLocks=2 během volání + restore i při výjimce).

Reference: [RESPONSIBILITIES § 3.5, 3.9](RESPONSIBILITIES.md), legacy `frmNoMDI.MainTimerProc`/`WaitFortimeToClose`, `EDSystem\CACHE\ALVA\SYSTEM\Main.cls:91-150`.

### 3. Reconnect UX + save-and-exit dialog + `OnBeforeLogout` event

**Stav:** Open

**CO:** Když server odhlásí klienta (force-logout od admina, timeout, výpadek spojení, expirace zámku), dnešní EDISONMAIN2 reaguje hláškou *"Spojení se serverem bylo přerušeno"* a okamžitě `Shutdown()`. Uživatel ztratí rozdělanou práci ve všech otevřených modulech.

**PROČ:** Největší UX hole dnes. Legacy `EDISONMAIN` má sice tray countdown notifikaci u cooperative logoutu (viz feature #2), ale ztrátu rozdělané práce taky neřeší. Po reálném produkčním nasazení bude tohle jeden z prvních stížnostních bodů.

**Co to obnáší (high-level):**

- Modální dialog při ztrátě spojení s volbami "uložit / prodloužit / zahodit".
- Mechanismus, jakým moduly dostanou šanci uložit rozdělanou práci (forma kontraktu k diskusi).
- Persistence drafts (kam — Caché global / lokální FS / per-modul, k diskusi).
- Resume session — po re-loginu nabídnout obnovu otevřených oken a drafts.

**Priorita #3 protože:** největší pilot user pain, ale velké úsilí (~2-4 týdny) — proto je až za #1/#2 (rychlé wins).

**Otevřené otázky:** kontrakt `OnBeforeLogout` (sync vs. async)? Kam ukládat drafts? Jak detekovat ztrátu připojení proaktivně, ne až při dalším requestu? Vázáno na feature #2.

Reference: [RESPONSIBILITIES § 2.6, 3.9-3.10](RESPONSIBILITIES.md) (idea pool).

### 4. Lokalizační infrastructure (Caché-driven)

**Stav:** Open

**CO:** UI stringy tažené z Caché backendu (existuje legacy `Localization.cls`, `LangItem.cls`, `LangTranslation.cls` co lze reusovat) místo z hardcoded `EDStrings.xaml`. Připravenost pro více jazyků.

**PROČ:** Dnes `EDStrings.xaml` má drobnou sadu hardcoded českých stringů, většina textů je rozsypaná po VM/XAML jako string literály. Každá iterace prohlubuje vázanost na češtinu (např. string concatenation s českou flexí). Když přijde non-CZ zákazník, je to dlouhý refactor.

**Scope první implementace (rozhodnuto):** Postavit **jen infrastrukturu** — service co stahuje stringy z Caché, fallback logiku, integraci s WPF resource systémem. **Obsahově dodat jen CZ** (přesun ze stávajícího `EDStrings.xaml` + dosbírání hardcoded textů). Druhý jazyk (např. EN) + editor pro správce přijdou až s konkrétním zákaznickým požadavkem.

Důsledek: nový kód se píše už správně (přes resource klíče místo string literálů). Pokud zákazník přijde, je to "doplnit překlad", ne "refactorovat aplikaci".

**Priorita #4 protože:** dělat dřív než se rozšíří hardcoded čeština víc. Po #3 (save-and-exit) hodně přibyde stringů (dialog texty, error messages atd.) — chceme aby se psaly už proti i18n infrastruktuře.

**Volby k diskusi při implementaci:** konkrétní API tvar service, jak řešit `EDFooter` / `EDToolbar` / dynamic stringy v VM, jestli reuse legacy backendu nebo nový endpoint.

Reference: [RESPONSIBILITIES § 10](RESPONSIBILITIES.md) (idea pool).

### 5. Setup wizard (first-launch detection + `EDISONini.xml` write)

**Stav:** Open

**CO:** Při prvním spuštění (nebo na stanici bez konfigurace) zobrazit setup wizard, který vyplní `EDISONini.xml` (server, port, REST port, database, idfa, IRIS mode, secured mode, jazyk) a vytvoří první admin uživatele.

**PROČ:** Adoption blocker pro produkční nasazení. Dnes konfiguruje technik M-line ručně přes `appsettings.json` — neúnosné pro hromadnější nasazení. Legacy `EDISONMAIN` má `frmSetup` wizard.

**Co to obnáší (high-level):**

- Detekce first-launch (legacy používá `EDISONini.xml` s `initialized` flagem; pro EDISONMAIN2 forma k diskusi).
- Wizard formuláře pokrývající: connection / database / first user / activate license.
- Validace inputů (test connectivity, formát IdFa, aktivace licence).
- Re-vyvolání za běhu pro admina (přes menu).

**Priorita #5 protože:** adoption blocker pro >5 stanic, ale pro pilot fázi (~3-5 stanic) lze řešit manuální konfigurací. Záleží jak rychle pilot expanduje.

Reference: [RESPONSIBILITIES § 1.1, 11.1](RESPONSIBILITIES.md) (idea pool).

### 6. Per-user timeout UI (feature #30 editor)

**Stav:** Open

**CO:** UI pro nastavení per-uživatel timeoutu (feature #30 `$$$F030UkoncitSystem` v Caché `^[MLINE]ALVA.USERS(...)`). Aktuálně se nastavuje jen manuálně v Caché terminálu — feature je definovaná, ale editor neexistuje (ani v legacy).

**PROČ:** Některé uživatele (kiosk, dispečer 24/7) je potřeba mít s jiným timeoutem než běžné účty. Bez UI to není správa-friendly.

**Co to obnáší (high-level):**

- Pole v user editoru (legacy `formEDISONMAINUserEdit` nebo jeho WPF budoucnost).
- Validace rozsahu.
- Vázáno na feature #2 (sdílí infrastrukturu timeout polling).

**Priorita #6 protože:** závisí na #2 infrastruktuře, sám o sobě je drobná polish.

Reference: [RESPONSIBILITIES § 3.5, 3.9](RESPONSIBILITIES.md) (idea pool).

### 7. LDAP UI / mode awareness

**Stav:** Open

**CO:** Když je uživatel konfigurovaný jako `LDAP` nebo `LDAP-AUTO` login mód (feature #23), UI musí adekvátně reagovat — `LDAP-AUTO` skryje pole pro heslo (jen jméno + auto-login z Windows username), `LDAP` ho zobrazí ale neumožní "změnit heslo" (heslo spravuje AD).

**PROČ:** Dnešní pilot uživatelé jsou v módu `EDISON` (lokální heslo), takže LDAP UI není kritické. Až bude první zákazník s LDAP požadavkem, je to nutnost.

**Co to obnáší (high-level):**

- Login dialog se adaptuje podle login módu uživatele (z backend feature #23).
- Auto-login pro `LDAP-AUTO` (Windows username match).
- Admin UI pro správu LDAP konfigurace (legacy backend již existuje).

**Priorita #7 protože:** customer-driven. Bez konkrétního požadavku není důvod začít.

Reference: [RESPONSIBILITIES § 2.1, 2.10](RESPONSIBILITIES.md) (idea pool).

### 8. Kick-and-login dialog v EDISONMAIN2 (nikoliv spawn USERS.exe)

**Stav:** Open

**CO:** Dnes při překročení licence (over-license retry pro admina) EDISONMAIN2 spawnuje legacy `USERS.exe formUSERSActivity` přes `LicenceVerifier.TryAcquireSession` + `ShowLegacyAndWait`. Funguje, ale je to legacy WinForms okno otevřené v cross-process kontextu.

**PROČ:** UX polish — admin by mohl mít přímo v EDISONMAIN2 WPF dialog s seznamem aktivních sessions + "kick & login" tlačítkem. Aktuální spawn funguje, takže low priority.

**Co to obnáší (high-level):**

- Server-side podpora pro vynucené odhlášení cizí session (s autorizací a audit logem; legacy `VzdaleneUkonceniEdisona` existuje, ale není perfektní — viz [RESPONSIBILITIES § 2.9](RESPONSIBILITIES.md), idea pool).
- WPF dialog se seznamem aktivních sessions + tlačítkem kick.
- Potvrzovací prompt s varováním o ztrátě cizí rozdělané práce.

---

## Miniaplikace

Miniaplikace = uživatelem konfigurovatelné dockable widgety v hlavním okně shellu (KPI tile, dashboard, real-time view, ...). Legacy `EDISONMAIN` má `EDMiniApplication.vb`, `clsMiniApplication.vb`, `EDDockingPanel.vb`, `frmMiniAppList.vb` — známé typy (MZDY, REPORTING, VOZIDLA, …), persist user preferences (pin, order, width).

Pro EDISONMAIN2 je miniapps **důležitá feature po MVP**, ale **rozhodnutí o architektuře (in-process WPF plugin / out-of-process embed / Caché-driven deklarativní / hybrid) zatím odkládáme**. Budeme řešit až dojde řada — v té chvíli rozhodneme na základě prvního konkrétního use case.

Související věci, které se k tomu váží: `ShowMiniApps` toggle v menu (existuje, ale nemá consumera — viz stub S3), licenční gating (element 15 + rozsah 52001-52999 v access table), docking panel v `EdisonMainWindow.xaml` (XAML už má placeholder Grid).

---

## Port legacy modulů

Postupný přepis legacy WinForms agend (MHD, MZDY, JR, Lístky, Reporting, …) — většinou v C#, jen prastaré ve VB.NET — do WPF v rámci EDISONMAIN2 shellu. Legacy `.exe` zůstanou spustitelné paralelně přes `FormToRunGetter` až než portovaný modul bude ekvivalentní.

Konkrétní strategie (deployment model, plugin DLL vs. sub-projekt v EDISONMAIN2, kde modul žije, jak sdílí EDServices) **zatím není rozhodnutá**. Vázáno na rozhodnutí o miniapp architektuře (sdílí stejnou otázku in-process plugin vs. out-of-process). Otevřeme až bude první konkrétní kandidát.

Pravidla, která musí port respektovat: shellův MLRest GUID, sdílený ACTIVITY zámek, stejný Caché backend, žádný breaking change pro paralelně běžící legacy `EDISONMAIN`. Plus všechny vůdčí principy.

### Vědomý dluh: dva zdroje navigačního stromu během transition

EDISONMAIN2 `Tree.cls` (Caché) **není migrace** legacy `EDCORE/Resources/edisontree.xml` — je to paralelní, ručně stavěný strom (S7 = 28 stub agend). Legacy plný XML strom dál existuje a čte ho legacy shell + **4 samostatné nástroje mimo shell**: USERS tiskové sestavy práv (`formUSERSUsersPrintPrehledPrav*.vb`), `EDUsers.GetLicensedForms`, a EDLicence `CTrees` → konzumují **Licence** a **LicenceInfo** app. Tyto nástroje čtou legacy XML (který zůstává, princip #7) → **dnes nejsou rozbité, žádné přepojení pro MVP netřeba**. Agenda `.exe` moduly strom nečtou vůbec; enumy (`EDISFormEnum` atd.) jsou nezávislý kontrakt. Reconnect těchto 4 konzumentů na Caché strom je relevantní teprve při (a) jejich portu do WPF, nebo (b) decommissionu legacy XML — obojí post-MVP. Pozor jen na drift: legacy plný strom × Caché stub strom se rozcházejí, ale slouží různým shellům.

---

## Co explicitně NEZAHRNOVAT (overengineering hlídka)

Bez této sekce každá iterace přidá "potřebné" abstrakce, které ale nemají reálný use case.

- **DI container** (Microsoft.Extensions.DependencyInjection, Autofac). `EDServices` postačí. Revisit až >12 servisů + reálné test configs.
- **Event aggregator / pub-sub bus.** Žádný cross-VM eventing aktuálně neexistuje. Nepřidávat preventivně.
- **MVVM framework** (Prism, CommunityToolkit.Mvvm). `EDViewModel` postačí.
- **Reactive Extensions** (Rx.NET). Žádný streaming use case.
- **Source generators** kromě jasných případů (např. INotifyPropertyChanged auto).
- **Visitor pattern pro `IModule`** dokud nepřibyde 3.+ subtyp nebo 2.+ operace.
- **Kompletní plugin systém** dokud miniapps nedospějí.
- **Mocking framework jiný než NSubstitute.**
- **Code generation pro Caché ↔ .NET DTOs.** CVar/CList je flexible.
- **Změna logging framework.** `EDPopup` + `Debug.WriteLine` postačí.
- **C1 reporting náhrada** (Telerik, DevExpress, custom). Out-of-scope shellu.
