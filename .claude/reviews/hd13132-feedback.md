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

- [ ] **Stav:** Open
- **P1 · L**
- **Co udělat:** Místo tří widgetů (Novinky/Wiki/Školení v karuselu `Core.Podpora`) jeden
  centrální widget `https://podpora.m-line.cz/api/widgets/edison2?key=<WidgetKey>`
  a umístit ho **mimo dockovací komponentu** - fixní pruh nahoře v shellu (DZ: zprvu
  klidně mock). Klíč už je v `EDControls/dotnet/EDSupportPanel/SupportActions.cs:20`
  (shodný s ticketem), URL skládá `EDISONMAIN2/dotnet/MiniApps/WidgetUrls.cs`.
- **Pozor:** Id `Core.Podpora` je v persistenci layoutu (pinned pane,
  `AvalonDockHost.cs:84`); karusel má Win32 hover workaround kvůli HWND airspace
  (`SupportCarouselMiniAppView.xaml.cs`). 🔸 Co s descriptory Novinky/Wiki/Školení -
  zrušit, nebo nechat jako samostatné miniaplikace?
- **Hotovo když:** V shellu je nahoře fixní pruh s jedním widgetem `edison2`, karusel
  zmizel; tím odpadají i výtky z #18 (věčný progress, střídání bloků 1/3, „záložka
  nezapadá do kontextu").
- **Zdroj:** HD 13132 #4 + #15 (Zeman), #18 (Kaska)

### F2 - Okno miniaplikací s jednoúrovňovým seznamem + vizuál

- [ ] **Stav:** Open
- **P2 · M**
- **Co udělat:** Výběr miniaplikací přesunout z menu (dnes `EdisonMainWindow.xaml:137-170`,
  CompositeCollection seskupená per `IMiniAppProvider.GroupTitle`) do samostatného okna
  s **jednoúrovňovým** seznamem. Katalog je `MiniAppCatalog.cs`. Součást zadání i „upravit
  vizuál miniaplikace".
- **Pozor:** 🔸 „Vizuál" není v ticketu specifikován - vyžádat upřesnění od DZ.
- **Hotovo když:** Miniaplikace se vybírají z okna s plochým seznamem; menu „Miniaplikace"
  na něj jen odkazuje (nebo zmizí).
- **Zdroj:** HD 13132 #4 (Zeman)

### F3 - Upravit updater

- [ ] **Stav:** Open
- **P2 · ?** 🔸
- **Co udělat:** V ticketu bez detailu („Upravit updater"). Vyžádat od DZ, co konkrétně -
  bez toho nezačínat.
- **Hotovo když:** Zadání upřesněno a rozpadnuto na konkrétní položku/y.
- **Zdroj:** HD 13132 #4 (Zeman)

### F4 - Fokus oken: nejde se překliknout na nejaktuálnější okno

- [ ] **Stav:** Open
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
- **Zbývá odeslat do ticketu:** „Po startu se nově vždy otevře Hlavní nabídka. Dřív shell
  skočil na Oblíbené, pokud jste nějaké měl - to jsme zrušili. Poslední otevřenou záložku
  si shell pamatuje per uživatel a firmu, takže si ji stačí jednou přepnout."
- **Zdroj:** HD 13132 #20 (Kaska)

### F6 - Miniaplikaci jde zúžit až není nic vidět

- [ ] **Stav:** Open
- **P3 · S**
- **Co udělat:** Reprodukovat. Min šířka dock area se řešila 28. 7. (`ca3f5b3`, `e248164`)
  a min rozměry má jen pinned pane (`AvalonDockHost.cs:84`) - Kaska to hlásí 18. 8.,
  takže buď regrese, nebo jiný panel (splitter mezi miniaplikacemi navzájem).
- **Hotovo když:** Žádný panel miniaplikace nejde zúžit pod čitelné minimum.
- **Zdroj:** HD 13132 #18 (Kaska)

### F7 - CPU/RAM špička po přidání miniaplikací

- [ ] **Stav:** Open
- **P3 · S**
- **Co udělat:** Změřit (pravděpodobně inicializace WebView2 procesů). Kaska: po sekundách
  se to srovná - možná stačí zdokumentovat/odpovědět, možná zanikne s F1 (méně WebView).
- **Hotovo když:** Změřeno a rozhodnuto (fix / won't fix s odpovědí do ticketu).
- **Zdroj:** HD 13132 #18 (Kaska)

### F8 - „Automatický refresh" přejmenovat česky

- [x] **Stav:** Done (2026-08-24) - „Automatická obnova" v descriptoru i v `Title` VM
  (objevuje se ve výpisu „Poslední obnova"); Id `Core.AutoRefresh` zůstalo. V UI shellu
  už anglické „refresh" není.
- **P3 · S**
- **Zdroj:** HD 13132 #16 (Zeman)

### F9 - Kapybara v okně „Akce probíhá"

- [ ] **Stav:** Open
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
- **Co udělat:** Definovat a implementovat chování při výpadku spojení. Dnes reconnect
  neexistuje (`ConnectToDatabaseCommand = Command(NotYetImplemented)`,
  `EdisonMainViewModel.cs:274`); zánik ACTIVITY zámku → řízený shutdown
  (`LogoutWatcher.cs`). Session GUID je stabilní po celé přihlášení (vzniká jen
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
- **Hotovo když:** Zámky po pádu klienta zaniknou v řádu minut bez zásahu admina
  (LockClearExt ručně už není potřeba).
- **Zdroj:** HD 13132 #17/3 (Kaska)

### F12 - Pády hostovaných legacy oken: dialog + telemetrie

- [ ] **Stav:** Open
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

### F13 - Odpovědět Kaskovi do ticketu na #17

- [ ] **Stav:** Open
- **P1 · S**
- **Co udělat:** Odeslat do HD 13132 připravené odpovědi:
  1. **Session GUID:** vzniká jen při startu a při přihlášení jiného uživatele
     (`SessionFactory.Establish`); přepnutí firmy ho **nemění** (mění se jen Idfa na téže
     instanci, `CompanySwitcher`). Obnova spojení zatím neexistuje - až bude, poběží na
     témže GUID (→ F10). Kauza ARRIVA se tedy nezopakuje.
  2. **Sdílení mezi okny:** všechna okna jednoho přihlášení sdílí shell GUID - předává se
     hostu v launch argumentech (`WindowNavigator.cs:354`), mezimodulové předávání přes
     `^MZR("FiltrZam2",...)` funguje dál.
  3. **Zámky:** korektní odhlášení/přepnutí firmy uklízí ACTIVITY i business zámky
     (LockClearExt dle GUID) a zavírání oken jde přes standardní Closing s vetem
     a dotazem na neuložená data (`6e49feb`). Hard-kill → server idle timeout (→ F11).
  4. **Pády:** EDPROCESS se nepoužívá, hostem je EDISONMAIN2API.exe; shell má crash
     dialog + telemetrii do podpory (`3cd3fc7`, `77fb84e`); pokrytí hostovaných oken → F12.
- **Hotovo když:** Odpověď odeslána v ticketu.
- **Zdroj:** HD 13132 #17 (Kaska)

### F14 - Titulky oken: `[IdFa]` suffix a nekonzistence CKSCHWAN/CK_SCHWAN

- [ ] **Stav:** Open
- **P3 · M** 🔸
- **Co udělat:** Štítky ve „Spuštěných modulech" už IdFa nemají (`52c95c9` + `66138dc` -
  dlouhý název/cesta agendou), ale **titulky oken** si `[idfa]` drží
  (`WindowNavigator.cs:410`), shell okno má vlastní mechanismus
  (`EdisonMainViewModel.cs:962`) a legacy out-of-process okna title neřídí vůbec -
  odtud i nekonzistence CKSCHWAN vs CK_SCHWAN. Rozhodnout: sjednotit (jeden mechanismus,
  jednotný tvar), nebo IdFa z titulků odstranit úplně.
- **Pozor:** Je to bývalý deferred `L3` ze starého PLAN.md (archiv).
- **Hotovo když:** Titulky všech oken jednoho sezení mají jednotný tvar.
- **Zdroj:** HD 13132 #3 bod 6 (Kaska), zbytek po `52c95c9`/`66138dc`

### F15 - Enter v poli „Hledat modul" spustí první výsledek

- [x] **Stav:** Done (2026-08-24) - Enter označí první nález v aktivní záložce a dá mu fokus
  ve stromu; spustí ho až druhý Enter (parita s legacy `frmMain`, žádný nechtěný start
  agendy). Vybráno z varianty „pouze fokus", ne „rovnou spustit".
- **P3 · S**
- **Zdroj:** vlastní nález při triáži HD 13132

## Mimo EDISON2 (jiné repo / jiný vlastník)

- **Pád „Aktivita uživatelů" z menu** (#5, Zeman) - `ShowUsersActivityCommand` spouští
  legacy `USERS.exe` přes hosta; šetřit v repu `c:\MLineRepos\EDISON`. Pravděpodobně
  stejný projev jako F12 (tichý pád hosta) - řešit spolu s F12. Vlastník: MH.
- **Chyba nastavení jazyka** (#18, Kaska - „odesláno") - legacy forma
  `ALVA.EDISONMAIN.formEDISONMAINLanguageList` z `EDISONMAIN.exe`. Dohledat Kaskův error
  report v telemetrii podpory. Vlastník: MH.
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
  `66138dc` (dlouhý název `nameEx`); v titulcích oken IdFa zatím zůstává → F14.
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
