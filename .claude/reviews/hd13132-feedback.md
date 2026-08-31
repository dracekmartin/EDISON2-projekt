# Feedback backlog: HD 13132 (nový spouštěč EDISONMAIN2)

Produktový/UX backlog z interního testování shellu - helpdesk ticket **HD 13132**
(příspěvky #1-#20, 4. 8. - 19. 8. 2026; Kaska, Zeman, Marek, Mráz). Sestaveno 2026-08-20.
Nemíchat s [arch-backlog.md](arch-backlog.md) - ten má uzavřený scope nálezů z review
2026-07-29; tenhle dokument sbírá uživatelský feedback.

**Pozor na číslování:** požadavek byl 20. 8. 2026 převeden na projekt a uzavřen jako
vyřešený (post #25 projektu); posty se tím přečíslovaly (starý #20 = nový #23, posun
cca +3). Odkazy `#N` v tomto dokumentu = **původní** číslování ticketu; nové posty
projektu jsou značené explicitně („nový post #N").

## Jak s dokumentem pracovat

- ID řada `F*`, aby nekolidovala s `R*` (arch-backlog) a `B*` (TD roadmapa).
- Jeden úkol = jeden `/wt` worktree = jeden chat.
- Hotovo: odškrtni `- [x]` a přepiš `**Stav:**` na `Done (YYYY-MM-DD) - co se reálně udělalo`.
- Odložení: `**Deferred:** po review (YYYY-MM-DD) - důvod`.
- 🔸 = vyžaduje produktové rozhodnutí, ne jen technickou práci. Nezačínej bez odpovědi.
- Pořadí sekce Otevřené = doporučené pořadí; priorita P1-P3 u položky.

## Otevřené úkoly (EDISONMAIN2 / EDControls)

### F1 - Centrální widget podpory + fixní horní pruh

- [x] **Stav:** Done (2026-08-24) - fixní pruh `SupportStrip` v shellu (řada 1 root gridu)
  s jedním widgetem `edison2`, mimo dock. Karusel i descriptory Novinky/Wiki/Školení zrušeny
  (rozhodnuto: všechny tři), s nimi padl **celý pinned mechanismus** - garanci kanálu teď nese
  pruh. Výchozí rozložení docku = jen Plocha. Ubyly 3 WebView2 procesy → viz F7.
- **P1 · L**
- **Pozor při ověření:** ostrá URL, ne mock - když endpoint na podpoře ještě neběží, pruh
  ukáže chybu widgetu.
- **Zdroj:** HD 13132 #4 + #15 (Zeman), #18 (Kaska)

### F2 - Okno miniaplikací s jednoúrovňovým seznamem + vizuál

- [x] **Stav:** Done (2026-08-24) - `Windows/MiniAppPickerWindow` s plochým seznamem (řádek =
  název + skupina jako popisek), dvojklik i OK otvírají. Menu „Miniaplikace" na okno jen
  odkazuje, seskupené submenu i jeho `CompositeCollection`/`BindingProxy` zmizely.
- **P2 · M**
- **Deferred:** po review (2026-08-24) - „upravit vizuál miniaplikace" z původního zadání;
  rozsah není v ticketu specifikován a rozhodnuto bylo udělat zatím jen okno výběru. Pokud jde
  o vzhled panelů v docku, je to retemplate chrome AvalonDocku - vlastní položka.
- **Zdroj:** HD 13132 #4 (Zeman)

### F3 - Upravit updater

- [x] **Stav:** Vyřazeno (2026-08-28) - updater se řeší obecně, evidence mimo tento dokument.
- **P2 · ?** 🔸
- **Co udělat:** V ticketu bez detailu („Upravit updater"). Vyžádat od DZ, co konkrétně -
  bez toho nezačínat.
- **Hotovo když:** Zadání upřesněno a rozpadnuto na konkrétní položku/y.
- **Zdroj:** HD 13132 #4 (Zeman)

### F4 - Fokus oken: nejde se překliknout na nejaktuálnější okno

- [x] **Stav:** Won't fix (2026-08-28) - problém starého formuláře (legacy okno hostované
  v EDISONMAIN2API), ne shellu; v EDISON2 se neřeší.
- **P1 · M**
- **Co udělat:** Reprodukovat podle videa
  `\\192.168.16.1\CacheIns\HD\Záznam 2026-08-19 113102.mp4` a opravit. Kaska se musí na
  nejnovější okno dostávat přes Win+Tab. Aktivace běžících modulů jede přes
  `SetForegroundWindow` na `Process.MainWindowTitle`/handle
  (`EDControls/dotnet/EDViewModel/WindowNavigator.cs`, infrastruktura z `88c16b0`, 21. 7.).
- **Pozor:** `SetForegroundWindow` má známá Windows omezení (foreground lock) - může chtít
  `AttachThreadInput`/restore kombinaci; příčinou může být i owned-window řetěz shellu.
- **Hotovo když:** Scénář z videa funguje - klik/aktivace přenese fokus na správné okno.
- **Zdroj:** HD 13132 #19 (Kaska)

### F5 - Menu se po startu přepne na záložku Oblíbené

- [x] **Stav:** Done (2026-08-24) - `DefaultTreeTab()` vrací vždy „Hlavní nabídka"; oblíbené
  default nepřebíjejí, uložená volba má dál přednost.
- **P3 · S** 🔸
- **Odesláno do ticketu (2026-08-28):** „Po startu se nově vždy otevře Hlavní nabídka. Dřív shell
  skočil na Oblíbené, pokud jste nějaké měl - to jsme zrušili. Poslední otevřenou záložku
  si shell pamatuje per uživatel a firmu, takže si ji stačí jednou přepnout."
- **Zdroj:** HD 13132 #20 (Kaska)

### F6 - Miniaplikaci jde zúžit až není nic vidět

- [x] **Stav:** Done (2026-08-24) - nebyla to regrese červencového fixu (ten hlídal jen plochu
  docku proti stromu). `AvalonDockHost` razítkoval `DockMinWidth/Height` pouze na pane
  s pinned dokumentem, ostatní AvalonDock pouštěl na svých 25 px - teď se razítkuje každému
  (pinned 340×200, ostatní 200×120), tedy i splitter mezi miniaplikacemi a řádek s Plochou.
- **P3 · S**
- **Zdroj:** HD 13132 #18 (Kaska)

### F7 - CPU/RAM špička po přidání miniaplikací

- [x] **Stav:** Done (2026-08-24) - změřeno po F1 (`tools/measure-shell-processes.ps1`,
  shell 12 min v běhu, idle): 7 procesů, ustálená RAM ~250 MB, **historický peak 921 MB**
  (z toho 467 MB samotný WPF proces, zbytek WebView2), idle CPU ~3 %.
  Závěr **won't fix** u paměti: špička je startovní (načtení XAML/BAML a stromů + init
  Chromia) a srovná se, jak Kaska popsal. **Miniaplikace s tím po F1 nemají nic společného** -
  žádná už WebView2 nepoužívá, přidání Plochy/Dispečinku/Přehledu je čistý WPF.
- **P3 · S**
- **Vypadl z toho nový nález → F16:** idle CPU ~3 % nejde z naší strany (WPF proces 0,2 %),
  ale z WebView2 rendereru + GPU procesu, tedy widget v pruhu podpory něco animuje **pořád**.
  Na statické stránce by to bylo ~0. Souvisí s výtkou #18 „věčný progress".
- **Zdroj:** HD 13132 #18 (Kaska)

### F8 - „Automatický refresh" přejmenovat česky

- [x] **Stav:** Done (2026-08-24) - „Automatická obnova" v descriptoru i v `Title` VM
  (objevuje se ve výpisu „Poslední obnova"); Id `Core.AutoRefresh` zůstalo. V UI shellu
  už anglické „refresh" není.
- **P3 · S**
- **Zdroj:** HD 13132 #16 (Zeman)

### F9 - Kapybara v okně „Akce probíhá"

- [x] **Stav:** Won't fix (2026-08-28) - rozhodnuto neřešit, sprity zůstávají jak jsou.
- **P3 · S** 🔸
- **Co udělat:** Kaska: „v prosím čekejte běhá prase :-) - nežádoucí". Jde o pixel-art
  kapybaru - sprity `Sprite.Capybara` + `Sprite.Bus`, náhodný výběr per okno
  (`EDControls/dotnet/EDActionInProgress/ActionInProgressWindow.xaml(.cs)`). Produktové
  rozhodnutí: odstranit zvíře / nechat jen autobus / nechat jak je.
- **Hotovo když:** Rozhodnuto a případně upraven výběr spritů.
- **Zdroj:** HD 13132 #18 (Kaska)

### F10 - Reconnect po výpadku spojení

- [ ] **Stav:** Open
- **P2 · L**
- **Deferred:** (2026-08-28) - zatím neřešit; až se otevře, podmínka téhož GUID platí dál.
- **Co udělat:** Definovat a implementovat chování při výpadku spojení. Dnes reconnect
  neexistuje (`ConnectToDatabaseCommand = Command(NotYetImplemented)`,
  `EdisonMainViewModel.cs:271`); zánik ACTIVITY zámku → řízený shutdown přes event
  `EdisonActivityFailed` (`AppBootstrapper.BuildActivityFailedWire`); `LogoutWatcher` je
  jen kooperativní kanál (`maSeUkoncit`, poll na klonu s `CacheLocks=2`, aktivitu
  nerefreshuje). Session GUID je stabilní po celé přihlášení (vzniká jen
  v `SessionFactory.Establish` - start a přihlášení jiného uživatele), takže obnova na
  témže GUID je proveditelná; `9feb252` už drží server-side idle timer živý.
- **Pozor:** Kauza ARRIVA (ztráta pracovních dat v ^MZR po novém GUID) je přesně to, co
  reconnect nesmí zopakovat - GUID se při obnově nesmí měnit.
- **Hotovo když:** Krátký výpadek sítě nezabije session (obnova na témže GUID), delší
  vede na srozumitelný řízený konec.
- **Zdroj:** HD 13132 #17/1 (Kaska)

### F11 - Zombie zámky po hard-killu klienta

- [ ] **Stav:** Open
- **P2 · M**
- **Co udělat:** Korektní logout/přepnutí firmy už čistí ACTIVITY i business zámky
  (`6e49feb`, `SessionLogout.cs`) a zavírání oken honoruje veto. Po hard-killu ale zámky
  visí do server idle timeoutu (známé, `EDISONMAIN2/CLAUDE.md`). Zvážit zkrácení
  timeoutu / heartbeat / úklid při dalším startu klienta pod týmž uživatelem.
- **Pozor:** server sklízí jen ACTIVITY (`Activity.LockDel` v timeout+3 min), business
  zámky nikdy; s vypnutým idle timeoutem po hard-killu visí **neomezeně**. Reaper
  `Activity.Update()` navíc není serverová úloha - běží jen při requestu s `Locks=2`
  (`ALWA.SYS.Rest`) a při `Login`/`Logout`/`VerifyLic`, tedy jen dokud nějaký klient pollí
  nebo se někdo přihlašuje; `UpdateTimeout` při `exitOnTimeout=0`
  (`^EDISON("System","Public","Settings")` piece 2/3, per-user `UserTimeout` přebíjí) nedělá
  nic. Business zámky (`^CacheTempSYSLock`) maže jen `Lock.LockClear(guid)` a po pádu ho
  nikdo nezavolá.
- **Kandidát řešení:** při startu shellu `Activity.List` → záznamy téže IP + loginName
  s jiným než aktuálním GUID → `Login.Logout` na klonu s tím GUID + `LockClearExt(guid)`.
  Bez serverové změny, řeší typický scénář „po pádu znovu spustím". Serverový úklid business
  zámků při zániku ACTIVITY zámku patří do téhož zásahu v `Activity.UpdateTimeout` jako F17.
- **Hotovo když:** Zámky po pádu klienta zaniknou v řádu minut bez zásahu admina
  (LockClearExt ručně už není potřeba).
- **Zdroj:** HD 13132 #17/3 (Kaska)

### F12 - Pády hostovaných legacy oken: dialog + telemetrie

- [x] **Stav:** Done (2026-08-28) - host neměl žádný handler běhových výjimek (generický WinForms
  dialog / tichý pád), přímý `EDISONMAIN.exe` v args módu končil tiše s kódem 0. Repo EDISON:
  `HostedErrorReporter` (ThreadException + AppDomain + try/catch kolem `ShowDialog`) volá legacy
  `EDForm.MLError.ShowError` - dialog „Neočekávaná situace" s telemetrií na tlačítko, po fatal
  `Environment.Exit(3)`, launch-fail exit 2 s českým textem bez výpisu args; `ApplicationEvents.vb`
  totéž. Repo EDISON2: shell hlásí nenulový exit kód hosta (`TrackExit` → `ReportLegacyExit`,
  seskupení podle formy, stabilní throttle klíč), opraveno pořadí subscribe/`EnableRaisingEvents`
  (okamžitý pád se dřív ztratil). Ověřeno Debug hostem s `EDISONMAIN2API_CRASHTEST=ui|bg` na
  Aktivitě uživatelů i Jazykovém nastavení (přímý EDISONMAIN): dialog + exit 3. **Limit:** výjimku
  z `Form.Load` na x64 spolkne Windows, nedostane se k žádnému handleru.
- **Odesláno do ticketu (2026-08-28):** „Pád okna staré agendy pod novým spouštěčem už neprobliká: zobrazí
  se stejný chybový dialog jako ve starém EDISONu (referenční číslo, tlačítko Odeslat hlášení)
  a tvrdý pád procesu navíc nahlásí spouštěč sám do telemetrie podpory."
- **P2 · M**
- **Co udělat:** Nový shell EDPROCESS nepoužívá - legacy okna hostuje generický
  `EDISONMAIN2API.exe` (`WindowNavigator.cs:26`, žije v repu `c:\MLineRepos\EDISON`).
  Ověřit, co udělá neošetřená výjimka v hostu: zobrazí se dialog, nebo okno „jen
  problikne" jako u EDPROCESS? Napojit host na stejný error reporting jako shell
  (`EDControls/dotnet/ErrorReporting/`, crash dialog + telemetrie z `3cd3fc7`+`77fb84e`).
- **Pozor:** Hlášený pád „Aktivita uživatelů" (#5) může být přesně tenhle projev - ověřit
  spolu (viz Mimo EDISON2).
- **Hotovo když:** Pád legacy okna zobrazí chybový dialog a dorazí do telemetrie podpory.
- **Zdroj:** HD 13132 #17/4 (Kaska), souvisí #5 (Zeman)

### F13 - Odpovědět do ticketu: #17 (Kaska, nový #20) + doplňující ticket DZ

- [x] **Stav:** Done (2026-08-28) - odpověď odeslána do ticketu (text níže, prověřen proti kódu
  2026-08-25). Nálezy z prověrky → F17, F18.
- **P1 · S**
- **Co udělat:** Odeslat do HD 13132 jednu odpověď pokrývající #17 (= nový #20) i tři
  stěžejní problémy z doplňujícího ticketu DZ (aktivita v multioknech, sdílení GUID,
  pomalá nástěnka):
  1. **Aktivita v režimu více oken (odhlašování aktivních uživatelů, ARRIVA):** všechna
     okna jednoho přihlášení - shell i hostované staré agendy - běží na jednom session
     GUID a s aktivními kontrolami (`CacheLocks=1`; host je nastavuje v
     `FormToRunGetter.cs:106-110`, repo EDISON). Aktivitu serveru hlásí každý request
     kteréhokoli okna, server vidí jedinou session s jedinou historií aktivity. Původní
     příčina (každá agenda si vytvořila vlastní GUID bez ACTIVITY zámku, server ji
     vyhodnotil jako neaktivní a ukončil uživatele uprostřed práce) zaniká konstrukčně -
     není co agregovat.
  2. **Sdílení GUID mezi okny:** GUID vzniká jen při startu a při přihlášení jiného
     uživatele (`SessionFactory.Establish`); přepnutí firmy ho **nemění** (mění se jen
     Idfa na téže instanci, `CompanySwitcher`). Hostovaným agendám ho shell předává
     v launch argumentech (`WindowNavigator.cs:354`), agenda má v sysu MLRestu totéž GUID
     jako shell. Mezimodulové předávání přes `^MZR("FiltrZam2",...)` funguje dál - a čtení
     má nově fallback na per-user kopii (dual-write GUID + userID, repo EDISON), takže ani
     změna GUID při novém přihlášení rozpracovaná data nezahodí.
  3. **Pomalé načítání nástěnky / stahování knihoven ze serveru:** nový shell je jeden exe
     (~4 MB) + jedna DLL - při startu se nestahují ani lazy nenačítají stovky knihoven
     jako u starého klienta. Nástěnka po startu je jen Plocha (žádné serverové volání),
     okno se vykreslí hned a data se donačítají asynchronně v jedné paralelní vlně;
     kontrola verze proběhne před startem shellu s viditelným průběhem. Staré agendy si
     své knihovny berou z instalace jako dosud - startu shellu se to netýká.
  4. **Zámky:** korektní odhlášení/přepnutí firmy uklízí ACTIVITY i business zámky
     (LockClearExt dle GUID) a zavírání oken respektuje veto okna (`6e49feb`).
     Hard-kill → server idle timeout (→ F11).
  5. **Výpadek spojení:** reconnect zatím neexistuje - až bude, poběží na témže GUID
     (→ F10), takže se kauza ARRIVA nezopakuje ani tudy.
  6. **Pády:** EDPROCESS se nepoužívá, hostem je EDISONMAIN2API.exe; shell má crash
     dialog + telemetrii do podpory (`3cd3fc7`, `77fb84e`); pokrytí hostovaných oken → F12.
- **Pozor:** původní návrh sliboval „dotaz na neuložená data" při zavírání - vypuštěno,
  není implementováno (veto mechanismus existuje a honoruje se, ale žádné okno shellu
  prompt nezvedá; legacy moduly se zavírají `Process.Kill()` bez veta).
- **Hotovo když:** Odpověď odeslána v ticketu.
- **Zdroj:** HD 13132 #17 (Kaska) + doplňující ticket DZ (2026-08-25)

### F14 - Titulky oken: `[IdFa]` suffix a nekonzistence CKSCHWAN/CK_SCHWAN

- [x] **Stav:** Done (2026-08-25) - rozhodnuto „IdFa jen v shellu": `WindowNavigator` už
  titulek nerazítkuje, okna EDISON2 mají čistý název, `M-line EDISON [IdFa]` zůstalo jen
  hlavnímu oknu (jedno sezení = jedna firma, firma je i ve status baru). Legacy hostovaná
  okna suffix dál mají - přidává si ho base forma sama (`clsForm.ShowIDFA`, repo EDISON),
  vědomě nesaháno; `StripIdfaSuffix` proto ve „Spuštěných modulech" zůstává.
- **P3 · M** 🔸
- **Nekonzistence CKSCHWAN/CK_SCHWAN nebyla chyba titulků:** `TestIdFa` (`apiFIRMA.vb`)
  vyžaduje v IdFa právě jeden `_`, takže `CKSCHWAN` platné IdFa být nemůže - je to jméno
  namespace nebo ručně vyplněná hodnota z legacy setupu (`frmSetup.vb`, registry).
  Dohledání u zákazníka → viz Mimo EDISON2.
- **Odesláno do ticketu (2026-08-28):** „Firmu už v titulcích jednotlivých oken neuvidíte - je
  jednou v titulku hlavního okna a ve stavovém řádku, protože v jednom přihlášení pracujete
  vždy v jedné firmě. Okna starých agend si zkratku firmy zatím přidávají sama."
- **Zdroj:** HD 13132 #3 bod 6 (Kaska), zbytek po `52c95c9`/`66138dc`

### F15 - Enter v poli „Hledat modul" spustí první výsledek

- [x] **Stav:** Done (2026-08-24) - Enter označí první nález v aktivní záložce a dá mu fokus
  ve stromu; spustí ho až druhý Enter (parita s legacy `frmMain`, žádný nechtěný start
  agendy). Vybráno z varianty „pouze fokus", ne „rovnou spustit".
- **P3 · S**
- **Zdroj:** vlastní nález při triáži HD 13132

### F16 - Widget podpory žere ~3 % CPU trvale

- [x] **Stav:** Won't fix (2026-08-28) - rozhodnuto neřešit; je to obsah podpory, ne náš kód.
- **P3 · S** 🔸
- **Co udělat:** Měření z F7: renderer + GPU proces WebView2 drží ~3 % CPU i na nečinném
  shellu, WPF strana 0,2 %. Statická stránka by byla na nule, takže widget `edison2` běží
  v nekonečné animaci (Kaskův „věčný progress" z #18). Běží to celý pracovní den.
- **Pozor:** Je to obsah `podpora.m-line.cz`, ne náš kód - opravit to musí strana podpory.
  Ověřit lze `tools/measure-shell-processes.ps1` (rozpad po procesech).
- **Hotovo když:** Widget na nečinné stránce neanimuje a idle CPU shellu spadne k nule.
- **Zdroj:** vlastní nález při měření F7

### F17 - Modul s vlastním GUID negeneruje aktivitu shell session

- [x] **Stav:** Done (2026-08-28) - rozhodnuto a implementováno: TD přebírá shell GUID
  (varianta 1). Shell ho spouští přes `WindowNavigator.ShowLegacy` (launch args, KillOnExit,
  „Spuštěné moduly", kill při switch firmy), `CacheSchemaWiring` při neprázdném guid nastaví
  `GUID` + `CacheLocks=1` a App ukončí TD při `EdisonActivityFailed`. Legacy launchery TD
  (ISYJR2, MHD2Linky, TISKJR - repo EDISON) propagují guid jen při `CacheLocks==1`, pod starým
  shellem beze změny. Timeout měřený jen na requestech (čtení = idle) je vědomé chování.
- **P2 · M**
- **Co udělat:** TemplateDesigner záměrně nepřebírá shell GUID
  (`TemplateDesigner/dotnet/Config/CacheSchemaWiring.cs:13-24`) a shell ho navíc spouští plain
  `Process.Start(path)` **bez launch argumentů** (`EdisonMainViewModel.OpenTemplateDesigner`),
  takže TD běží standalone z `templatedesigner.appsettings.json` s vlastním GUID
  a `CacheLocks=0` - jeho requesty nepíšou ACTIVITY historii nikomu. Uživatel hodinu
  pracující jen v TD → idle timeout shellu → řízený konec shellu uprostřed práce. TD samotný
  přežije (není v `KillOnExit` jobu, ten váže jen `WindowNavigator`). Stejný tvar chyby jako
  kauza ARRIVA, jinými dveřmi; platí pro každý budoucí modul s vlastním MLRest.
- **Server to už umí:** `Activity.UpdateTimeout` zná podřízené procesy - idle rodiče
  = `min(idle shell, idle dětí)`, `LockDel` rodiče smaže i děti, osiřelé dítě se sklidí samo.
  Vazbu zakládá `ALVA.EDISON.Main.UpdateSeznamProcesu` (legacy `frmNoMDI.ProcesuBylPridanGuid`
  ji volal při každém přidání modulu, nový shell nikdy). Podmínka: dítě musí posílat
  `Locks=1` (jen tehdy jde `UpdateHistory`), tedy potřebuje vlastní ACTIVITY zámek
  (`VerifyLic` pod jeho GUID; `CountIdfa` nepočítá vlastní IP, licenci navíc nespotřebuje).
- **Varianty:** (1) TD převezme shell GUID - předat launch kontrakt, v `CreateMlRest`
  přestat ignorovat `ShellGuid`, `Locks=1`; nejmenší práce, TD sdílí ACTIVITY i business zámky
  shellu. (2) TD jako podřízený proces - vlastní GUID + vlastní zámek + `UpdateSeznamProcesu`;
  věrné legacy, funguje pro každý budoucí modul. (3) Timeout měřit jen na shellu
  a zdokumentovat. 🔸 Rozhodnutí produktové.
- **Pozor:** shell nemá žádný keepalive - legacy 60s poll běžel s `Locks=1` a byl
  nechtěný keepalive (timeout byl prakticky nedosažitelný), dnes poll běží s `Locks=2`.
  Nový shell je první klient, kde timeout **reálně funguje** - u zákazníků se zapnutým
  timeoutem čekat nová hlášení „odhlásilo mě to" i bez TD (čtení dashboardu = žádný request).
- **Hotovo když:** Práce v TD (a obecně v modulu s vlastním GUID) drží shell session
  živou, nebo je rozhodnuto, že timeout se má měřit jen na shellu, a je to zdokumentováno.
- **Zdroj:** vlastní nález při prověrce F13 (2026-08-25), rozšířeno 2026-08-28

### F18 - Legacy dashboardy v layoutu: blokující load při startu + ztracené dávkování

- [x] **Stav:** Done (2026-08-25) - sdílený `LegacyDashboardLoader`: požadavky panelů, které se
  přihlásí v téže dispatcher operaci, odejdou jako **jeden** `MiniAplikaceAPI.LoadData` s N
  parametry `appType` (vrácená parita s `clsMiniApplication.LoadData`) na klonu mimo UI vlákno.
  Vrátila se i druhá ztracená legacy sémantika: otevření panelu jede `refresh=0` (data
  předpočítává serverová úloha `TaskTypeALVAEDISONMiniAppRefresh`), přepočet `refresh=1` jde jen
  z „Načíst" a z hromadné obnovy - server-side recompute při každém startu byl větší brzda než
  počet round-tripů. Panel má stavy „Načítám" / chyba v těle (popup by se u dávky násobil).
  Zasynchronněn i **Dispečink** (táž vada v `OnOpened`, dávkovat nejde - jiná Caché třída);
  `IRestCallRunner` se kvůli sdílení přesunul do EDControls.
- **P2 · M**
- **Dopad na F13:** bod 3 doplňujícího ticketu (pomalá nástěnka) je tímhle pokrytý i pro
  uložený layout s dashboardy, nejen pro Plochu.
- **Zdroj:** vlastní nález při prověrce doplňujícího ticketu (2026-08-25)

## Mimo EDISON2 (jiné repo / jiný vlastník)

- **Pád „Aktivita uživatelů" z menu** (#5, Zeman) - `ShowUsersActivityCommand` spouští
  legacy `USERS.exe` přes hosta; šetřit v repu `c:\MLineRepos\EDISON`. Pravděpodobně
  stejný projev jako F12 (tichý pád hosta) - řešit spolu s F12. Vlastník: MH.
  **2026-08-28:** přes host s účtem MH/CB_DP se otevře bez pádu (nereprodukováno); po F12 by
  případný pád ukázal dialog a dorazil do telemetrie - čekat na další výskyt.
- **Chyba nastavení jazyka** (#18, Kaska - „odesláno") - legacy forma
  `ALVA.EDISONMAIN.formEDISONMAINLanguageList` z `EDISONMAIN.exe`. Dohledat Kaskův error
  report v telemetrii podpory. Vlastník: MH. **2026-08-28:** přímý launch se otevře bez pádu
  (nereprodukováno); před F12 by tam pád skončil tichým zavřením s kódem 0 - Kaskovo „odesláno"
  tedy šlo z dialogu uvnitř formy, ne z neošetřené výjimky.
- **CKSCHWAN vs CK_SCHWAN u zákazníka** (#3 bod 6, Kaska) - `CKSCHWAN` není platné IdFa
  (chybí `_`), takže jde o jméno namespace nebo ručně vyplněnou hodnotu v legacy setupu
  (`frmSetup.vb` → registry). Dohledat skutečnou konfiguraci instance. Vlastník: MH.
- **IdFa výjimky do legacy EDISON.exe** (#4, Zeman) - pro zákazníky testující
  EDISONMAIN2; repo `c:\MLineRepos\EDISON`. Vlastník: MH. Až budou hotové připomínky.
- **Ikona autobusu - upravit** (#4, Zeman) - bez detailu; 🔸 upřesnit, které ikony se
  týká (strom/dlaždice shellu, nebo legacy?), pak zařadit.
- **Partnerská řešení** (#4, #6-#13) - řeší PM/MM mimo tento repo, stav z konverzace:
  - KARAT → Správce úloh: **hotovo v masteru** (MM, #12), licence Karat odebrány (PM, #13);
    do verze patří EDCore.dll, TM.exe, TRANSDEV.exe, TRANSDEVAPI.dll, ALVA_TM.
  - AURIS: v Polkost vypnut po domluvě s p. Holubem (20. 8., nový post #24) - neví,
    jestli to používají, zjistí se z reakcí.
  - FS: zakáže PM.
  - BC: licence odebrány vč. EKOBUS/UH (PM, #9, #11); 🔸 zbývá rozhodnout odebrání ze
    stromu + souborů verze.
  - EmTest: vyhodit z EDISONu, nikdo nepoužívá (PM, #9).
  - CHAPS, T-mapy: přesun do AD (MH se domluví s XK).
  - FSsoft (CHOD_DP, denně): předělat okno do AD, zachovat licencování (PM, #9).

## Hotovo (srpen 2026, s commity)

Podklad pro odpověď do ticketu - body z #3 (Kaska) a #4 (Zeman):

- **Login s tečkou v uživatelském jménu** (LDAP-AUTO) - `24c8c78`: auto-login se zkouší
  vždy, přesné chybové hlášky (test odkazuje „HD 13132/1").
- **Kurzor v placeholderu „Hledat modul"** - `df69c49`: placeholder se skryje při fokusu.
- **Výběr firmy: šipky + Enter po filtru** - `df69c49` + `114755c` (navigace, Enter
  commit, filtr bez diakritiky).
- **Enter ve stromu spustí modul** a **nalezená skupina není slepá ulička** (zpřístupní
  celý podstrom) - `0d12d5a`.
- **Spuštěné moduly: rozlišení duplicit** - `52c95c9` (cesta agendou místo IdFa) a
  `66138dc` (dlouhý název `nameEx`); IdFa zůstal jen v titulku shellu (F14).
- **Nastavení: Font/Rodina → Písmo** - `ac9b0d1`.
- **Ikony na ploše: dlouhý název** (rozliší „Zaměstnanci" MZDY/ZAM) - `66138dc` a
  `f62ca99` (auto-shrink labelu).
- **Min šířka dock area miniaplikací** - `ca3f5b3` + `e248164` (28. 7.); nové hlášení → F6.
- **Karusel podpory: pomalejší cyklus, pauza na hover** - `04a4c1c` (29. 7.); definitivně
  řeší F1.
- **Záložka menu se persistuje per uživatel** - `a4c86bc`; výchozí je vždy Hlavní nabídka (F5).
- **Business zámky při logout + veto zavírání oken** - `6e49feb`.
- **Crash telemetrie + dialog + fallback log** - `3cd3fc7`, `77fb84e`.
- **Server idle timer / CacheLocks=1** (session nevypadá idle) - `9feb252`.

Poznámky bez úkolu: „prase" = kapybara (→ F9); tmavý režim - pochvala od Kasky (#18),
bez úkolu.
