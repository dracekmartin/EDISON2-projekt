# Zastávkový JŘ - požadavky zákazníků z helpdesku

Snapshot z Podpory M-line k **2026-05-11**, **konsolidováno 2026-07-10, revize 2026-07-20**
(sekce 0 = stav pro uzavírání tiketů + dispatch; P3 imposice hotová, malí TR kandidáti odbaveni;
2026-07-20 ověřeno proti živému HD - #8772/#7161 resolved, skupina „ready-to-close" vytipována,
neevidované tiskové tikety protříděny = všechny linkové/mimo, viz „Mimo scope" níže).
Slouží jako vstup pro plánování dalšího rozvoje TemplateDesigneru a Caché agend
(`ZastavkoveJrMhd` / `ZastavkoveJrAd`). Aktualizuj při dalším procházení tiketů.

> **Pozn. 2026-07-10:** od snapshotu přibyla **AD agenda** (`ZastavkoveJrAd`, TRANSDEV jede primárně AD)
> a většina helpdesk featur je vyřešená právě pro AD. Dnešní kontraktní práce (A-AD: skaláry `SMER` +
> `POPIS_SMER`=konečná + `NASTUPISTE`) a B2 (overflow konzistence) posouvají některé TR body - viz sekce 0.

> **Postup pro AI agenta:**
> 1. Otevři odpovídající tiket přes `mcp__claude_ai_Podpora_M-line__get_ticket` s UUID z tabulky.
> 2. Pokud řešíš jednu z featur → ověř aktuální stav v UI a v `ZastavkoveJrMhdAPI`.
> 3. Po implementaci přesuň položku do sekce „Hotovo" a doplň datum + commit.
> 4. Při interpretaci nezjednodušuj - některé požadavky jsou jemné rozdíly proti tomu, co TD dnes dělá (např. „máme šipku směru, ale klient ji chce v opačné pozici").

---

## Aktivní tickety (otevřené + pozastavené, agenda Jízdní řády / MHD)

| # | UUID | Stav | Zákazník (kontakt) | Téma |
|---|---|---|---|---|
| 10660 | f3d3a7d8-0c3d-4076-93d1-c12bb25e851d | **closed 31. 7. 2026** | **DPKV** (Jan Slavík) | 4 oblasti grafických připomínek po importu z legacy |
| 11300 | 653b5c65-b84f-49e8-803f-329febe5629e | suspended | **KORID** (Jaroslav Smištík) | Skrýt prázdné přípojové řádky (PD vs. víkend) |
| 8828  | 02063e8a-e2c1-476d-a527-77e77e0f263e | suspended | **TRANSDEV** (Michal Podloučka, Jakub Holík) | Per-sloupek zastávky - samostatná vývěska |
| 8772  | b62718c6-e0aa-478a-be31-bdfb8c7b9917 | **resolved 15.7.** | **TRANSDEV** (Jakub Holík) | Návrhář sestav - podmíněný tisk sekcí |
| 7947  | 585158b0-d063-40d9-87de-eb076a236a87 | **closed 30. 7. 2026** | **TRANSDEV** (Michal Podloučka) | Logika auto-poznámky u poslední/předposlední zastávky |
| 7959  | 45fdc840-ce70-4436-a331-c5a61732d9d6 | **closed 30. 7. 2026** | **TRANSDEV** (Michal Podloučka) | Skrýt zastávku v seznamu, pokud z tištěné zastávky není obsluhována |
| 7955  | dd0f7bd3-0efa-4e2d-947d-ed16559952ce | **closed 30. 7. 2026** | **TRANSDEV** (Michal Podloučka) | Vlastní komentář spoje má přebít auto-poznámku + 2 buga |
| 7937  | 64fd847c-7090-47d1-bc91-bc581b0aea42 | suspended | **TRANSDEV** (Michal Podloučka) | Odsazení/odrazení zastávky per směr, jen pro zastávkové JŘ |
| 7161  | 44e0a446-b05b-4c44-9497-3c96fbb945a2 | **resolved 15.7.** | **TRANSDEV** (Vladimír Šimko) | Vypínatelné automatické poznámky (na znamení, bezbariér.) |
| 9036  | 02f8fcda-25c5-4a2e-8784-79bc84860811 | suspended | **TRANSDEV** (Vladimír Šimko) | Nočné spoje - časová osa cez polnoc (22-5h) → **pokryto B3** |

Otevřené tikety #12434 (BBSK - „Export Tisk JŘ") a #12260 (BUSLINE - „úprava šablony") jsou obecné tracker / dotazy, ne konkrétní featury - neuvedeny.

Mimo scope zastávkového JŘ (protříděno proti živému HD 2026-07-20, znovu 15. 9. - všechny tiskové
„suspended" tikety mimo tabulku výše jsou linkové/knižní JŘ, nebo úplně mimo TD; **žádný zastávkový
nechybí**). ⚠️ Na Janovi je jich pozastavených **jedenáct** a všechny jsou níž; seznam odpovídá
helpdesku k 15. 9.

**Linkové / knižní JŘ (řeší se s jejich trackem, ne sem):**

> Co v nich zákazník konkrétně chce, je rozepsané v
> [`linkovy-knizni-jr-implementace-analyza.md`](linkovy-knizni-jr-implementace-analyza.md) §14
> (projito 2026-08-19, navázané na vlny `L1`-`L8` roadmapy). Sem se to nekopíruje.

✅ **Hotové, čekají jen na odpověď a zavření** (prověřeno proti helpdesku 15. 9.):

- **#8372** (KORID) - „vkládat prázdné sloupce" do nastavení tisku (roztažení řídkých linek na A4).
  Volba **„Dorovnat prázdnými sloupci"** u tabulky linky; dorovnává se i **levá** strana, kterou
  tiket nežádal, ale u obousměrné sestavy bez ní zůstala díra.

🟡 **Čeká práce nebo odpověď:**

- **#7487** (KORID) - NZ + týdenní platnost 4 na řádek.
- **#8360** (KU HK) - přípojové řádky: kurzíva / barva / velikost písma, aby čas přípoje nesplýval s časem spoje.
- **#9417** (KORID) - dopravci v záhlaví souhrnného JŘ jen pro spoje na dané stránce (velké A4 linky).
- **#11373** (KAD) - negativní značky a poznámky **vedle** tabulky s časy. Umíme; zbývá výtisk.
- **#9088** (TRANSDEV) - linkový cestovný poriadok · **#9283** (KOVED) - sdružený JŘ IDZK.
- **#7937** (TRANSDEV) - odsazení zastávky. Půlku pokryje sloupec, druhá (odsazení per tarifní
  pásmo) čeká na kolegy z jízdních řádů - detail v tabulce §TR.
- **#13070** (BBSK) - vypadá jako linkový, ale předloha je **odjezdová relace**; patří tracku
  Relace, kde zatím nic není.

**Úplně mimo TD (Studio JŘ / editace dat, ne tisk):** #12803 (KOVED - výběr změněných km úseků),
#7929/#8067/#8175/#8457/#9481/#9483/#10442/#12854 (editace spojů, MHD linek, stažky) - součinnost kolegů z JŘ.

**Obecné tracker / dotazy (ne konkrétní featura):** #12434 (BBSK), #12260 (BUSLINE).

---

## 0. Konsolidovaný stav - pro uzavírání tiketů a dispatch (2026-07-10)

> Jedno místo: co je hotové (→ otestovat → zavřít na helpdesku), co malé/velké zbývá. Detaily v sekcích níže.
> **Legenda:** ✅ hotovo (AD) · 🧪 hotovo, ověřit u klienta · 🟢 malé zbývá · 🟠 velké zbývá (analýza/data) ·
> ⬜ MHD-port (AD hotové, MHD ne) · ➖ mimo TD / mimo scope.
> ⚠️ Featury jsou vyřešené primárně pro **AD** agendu; MHD-port jen na reálnou potřebu (DPCB/DPKV).
> **Past na routování práce** (upřesněno Janem 31. 7. 2026): self-contained MHD modul mají **jen DPCB
> a DPKV**, všichni ostatní vedou jízdní řády v modulu **JR (= AD)** - i městskou dopravu. Zákazník
> proto napíše „MHD", protože jde o městskou dopravu v nějakém městě, ale datově je to AD. Než se něco
> označí za MHD práci, **ověř, ve kterém modulu zákazník data vede** (např. #13070 BBSK psal
> „TISK JŘ MHD" a jde o AD).

### 0.1 Tikety (helpdesk)

> **Pořadí projití (2026-07-28):** ▶ **1. ready-to-close** = ověřit v buildu + resolve odpovědí zavřít
(prázdné - #9036 resolved 28. 7., #7947 i #7955 zavřeny 30. 7.). ▶ **2. testování u zákazníka**
> (prázdné - #10660 DPKV zavřen 31. 7. s návodem, kde se co nastavuje). ▶ **3. feature tracky, nezavírat**
> (#7937, #8828 - čeká práce v TD). Hotové zavřené: #8772, #7161 (15.7.), #7959 (20.7., zavřen 30.7.), #7947 + #7955 (30.7.).

| Ticket | Zákazník | Feature | Stav | Otestovat před zavřením | Detail |
|---|---|---|---|---|---|
| #13158 | BBSK | Vývěska na výšku dle vzoru - poznámky ke 4 bodům (31. 8.) | 🔄 **body 0-3 hotové 31. 8.** (formát stránky se dá změnit bez ztráty obsahu; dopravce s adresou po řádcích; „Konečná zastávka") · **bod 3 hotový 31. 8.** - náhled bez vybrané linky nově ukáže vlastní sloupce (posílá se definice ze šablony a cache je klíčovaná i jí); u linkového JŘ zůstává týž vzor otevřený, viz roadmapa „K ověření" · **bod 4 odložen** (piktogramy u zastávky) | Ověřit u zákazníka na jeho šabloně, hlavně tvar adresy dopravce | changelog 31. 8. (5) |
| #8772 | TRANSDEV | Podmíněný tisk sekcí | ✅ **resolved 15.7.** | - (zavřeno) | §E1 |
| #7161 | TRANSDEV | Skrýt na-znamení legendu (D5) + vypnout agregát bezbariér (D6) | ✅ **resolved 15.7.** | - (zavřeno) | §D5/D6 |
| #7959 | TRANSDEV | Skrýt neobsluhovanou zastávku | ✅ **ZAVŘENO 30. 7. 2026** | Filtr `servedPoradi` = defaultně, bez přepínače (jiné než „projeté zastávky") | §A6 |
| #7947 | TRANSDEV | „jede až do XY" u poslední zastávky | ✅ **ZAVŘENO 30. 7. 2026** - znění „jede až do zast. \<konečná\> přes \<zastávky\>", ověřeno proti starému modulu i v běžícím buildu na `CB_GWBUS` 501/0000001/T. Zákazníkovi odpovězeno s prosbou o vyzkoušení na 876401/876402, až bude mít nový návrhář (na jejich datech jsme ověřit nemohli). | - | §D1 |
| #7955 | TRANSDEV | Vlastní komentář přebije auto (D2) + NZ bug (D3) | ✅ **ZAVŘENO 30. 7. 2026** - ověřeno na reálných datech (CB_GWBUS 501/0000002, spoj 21), viz tabulka v §D2. V odpovědi zdůrazněno, že jde o **volbu** parametru (výchozí = legacy) a že negativní značky i platnost dnů zůstávají. | - | §D2/D3 |
| #7955 | TRANSDEV | Výběr spoje celým číslem (D4) | ➖ mimo TD | UI bug ve Studiu JŘ (EDISON) - do odpovědi se vědomě nepsalo, řeší se jiným kanálem | §D4 |
| #9036 | TRANSDEV | Časová osa přes půlnoc (22→5) | ✅ **RESOLVED 2026-07-28** - ověřeno Janem na nočním provozu MHD JH (345070/CB_CSADAD): celodenní osa „od 4 do 0" (půlnoc na konci) i noční výřez „22–5"; odpověď s oběma recepty odeslána | - | §B3 |
| #10660 | DPKV | 4 body: seznam zastávek (šipka/značky/podbarvení/font sloupce) + záhlaví mřížky (2. řádek, podtržení) + časová osa přes půlnoc + poznámky (barva znaku, nadpis) | ✅ **ZAVŘENO 31. 7. 2026** - odpověď obsahuje i **návod, kde se co nastavuje** (tři body existovaly, jen je klient nenašel) | - | §G |
| #7937 | TRANSDEV | Odsazení zastávky per směr / per Tč | 🤝 **DOHODNUTO s kolegy 10. 8.** | Do jízdních řádů přidají **sloupce pro oba směry** (dohodnuto 10. 8.), tím padá per-směr; naše část = přečíst hodnotu podle směru. **Tiket je u zákazníka pozastavený**, takže z aktivní roadmapy odebráno - stav drží tato evidence. ⚠️ **Druhá polovina požadavku (per Tč pásmo vývěsu) tím pokrytá není** - ověřit u TRANSDEV, jestli na ní trvá. Základ (odsazení z `^JRL` dta7) hotový. | §A5 |
| #8828 | TRANSDEV | Označníky → samostatná vývěska | ✅ **RESOLVED 2026-07-28** - parametr „Samostatná stránka pro každý označník" (+ titulek „…, označník X", spoj bez označníku na stránku nejčetnějšího); odpověď odeslána, klient ověří na 876420/863383 (znovuotevření = záchytka). MHD zdroj označníku až u MHD zákazníka. | changelog 27.–28. 7. |
| #11300 | KORID | Skrýt prázdné přípojové řádky | ➖ linkové JŘ | Přípoje nejsou koncept zastávkového JŘ (řešit s linkovými) | §D7 |
| #13048 | KOVED (IDZK/Lábsky) | Poznámky/sloupce zastávek (5 bodů) | ✅ **ZAVŘENO 30. 7. 2026** - všech 5 bodů v1; klient potvrdil i zmenšování textu (bod 2), což byl poslední otevřený bod | - | §0.3 |

| #13045 | KOVED (IDZK/Lábsky) | Skupiny dnů - vlastní sloupce vývěsky | ⚠️ **13. 8. částečně znovu otevřeno revizí kolegy** (`B81`-`B84` v roadmapě): zadávání dnů neodpovídá abecedě značek z vyhlášky 122/2014 a sloupec „Ne" nebere spoje s platností `+`, ačkoli `+` znamená „jede v neděli a ve státem uznané svátky". **Nezavírat, dokud `B81` nebude opravená** - rada „nedělní a svátkový provoz rozliš rozdělením sloupce" dnes vede na prázdný sloupec. ✅ **HOTOVO 10. 8. 2026, obě části** - (a) denní značky dle rozvržení sloupců (`B29`, 31. 7.: spoj „sobota a svátek" nesl „6" i v nedělním sloupci); (b) vlastní definice sloupců = dny + značky + období (`B39`, 10. 8.) | Vlastní sloupce: dny, „jen/bez značek", „nevypisovat značky", období jako rozsahy dat (bez roku = každý rok). Ověřit na jejich lince, že vývěska sedí, a poradit jim, že nedělní a svátkový provoz se rozliší **rozdělením sloupce**, ne značkami | changelog 2026-07-31 (4) + 2026-08-10 (12) |

| #12843 | TRANSDEV (Šimko) | Podněty k zastávkovým JŘ - 9 bodů z testování šablony „Nitra" (MHD) | 🟡 **rozpracováno** - psáno 10. 8. proti verzi ze **4. 8.**, takže část už je hotová a jen se k němu nedostala | **Hotové, dostane s verzí:** negativní značky dle nastavení linky (body 5 a 8 = `B63`). **12. 8.:** ruční zlom v nadpisu skupiny dnů (Enter i token) a volba „Rozestup odjezdů = Rovnoměrný" (rastr za celý sloupec, aby značka nerozhodila svislice). **Slovenské texty značek** - jazyk legendy se teď řídí kulturou šablony (Vlastnosti stránky), znění je doslova ze starého modulu; nepřekládá se, co napsal on sám (poznámky z číselníku, nadpisy sloupců dnů). Ověřeno voláním serveru, **zbývá porovnat s jeho starým výtiskem**. **Projeté zastávky = nedorozumění, ne chyba** - parametr „Tisknout projeté zastávky" je výchozí vypnutý; ukládání i načítání ověřeno Janem 12. 8., reprodukce se nepodařila. Odpovědět: zapnout parametr + uložit; časy u nich zvlášť přes „Čas u projetých". ⚠️ **Kdyby se to překlápělo zpět, tady je diagnóza** (z roadmapy 13. 8. jako `B68`, odebráno - bez druhé stížnosti není co opravovat): uložení šablony zapíše hodnoty **všech** parametrů sestavy, i těch, kterých se uživatel nedotkl (`SyncParametersToTemplate`), a hodnota v šabloně **má přednost** před per-agenda nastavením. To je **záměr** - samonosná šablona se má kolegovi vytisknout stejně. Vedlejšek je, že se zabetonuje i to, co uživatel u té šablony vůbec neřešil, takže druhý člověk dostane jeho volbu. Oprava by byla „zapisovat jen hodnoty odlišné od výchozí" (nebo sledovat „dotčeno" per parametr), ale tím šablona přestane být plně samonosná - **je to rozhodnutí o produktu, ne oprava chyby**, a bez reprodukce by se dělalo naslepo. **Opraveno 11. 8.:** šipka vlevo přes jízdní doby (bod 1), barva písma aktuální zastávky (bod 2), ikona do záhlaví sloupce hodin (bod 9). **Odpadlo:** zalomení textu v záhlaví jde Enterem (bod 3). **Doloženo jako opravené (bod 6):** kolo místo vlaku už neplatí - ověřeno na jejich datech (Nitra, linka 0000001, zastávka „Nitra, Železničná stanica") v české i slovenské lokalizaci; klient testoval verzi z okna, kdy se ta série oprav teprve dělala. **Čeká na jeho upřesnění:** přesné kroky k vynulovanému poli výluk (bod 7 - u nás se nechová). **Otázka:** výběr variant tras (4) = **tentýž požadavek jako `TR7`** z června, jen podaný podruhé nad novým chováním; vést dál jako jeden bod (detail u TR7). ✅ **13. 8. odstavec o jízdních dobách uzavřen** (`B67` → archiv): jeden variant končící v půli trasy · zbytečně mnoho variant · **neukazovat shodné varianty za zachádzkou** (doloženo bránou přes pět linek, ne jen gold) · **řazení podle délky jízdy** obojím směrem. ⏸️ **Ruční zaškrtávací tabulka variant odložena** (dohoda Jan + kolega 13. 8.): není to jednoduchá změna, dá se mu nejdřív hotové k vyzkoušení a domluví se potom. ✅ **Celý tiket je odpovězený, nic z něj nezbývá** (Jan 3. 9.). Rozestupy mezi spoji i souhrn zastávek na znamení se vyřešily 12. 8. a zákazníkovi bylo odpovězeno. ⚠️ **Tady přesto visely jako „zbývá" ještě 3. 9.**, takže je agent nabídl k práci jako novou - stejně jako zalomení záhlaví (věta níž). **Odstraněno; kdo hledá práci, nesmí ji brát z tohoto odstavce, ale z tiketu na helpdesku.** Šlo o **zákaznický tiket TRANSDEV**, ne o podněty kolegy - i to se v evidenci posunulo a agent to pak takhle popisoval. ✅ **Zalomení záhlaví sloupců s minutami je HOTOVÉ** (ověřeno 3. 9.: sedm testů `DepartureHeaderLinesTests`, mezi nimi `TokenZpetneLomitkoN_SeChovaJakoEnter`). Jeho stížnost byla z **12. 8.**, tedy z téhož dne, kdy se to opravovalo - psal proti starší verzi. ⚠️ Záznam si proto devět dní **protiřečil**: výše stojí „12. 8. ruční zlom v nadpisu skupiny dnů (Enter i token)" a „Odpadlo: zalomení textu v záhlaví jde Enterem", a přesto to viselo jako zbývající práce (nález Jan 3. 9. - agent položku nabídl k práci, hotovou). **Poučení: než se z evidence vezme položka, přečti CELÝ záznam, ne jen odstavec „Zbývá".** | changelog 2026-08-11 (3), 2026-08-12 (10), 2026-08-13 (1)-(6) |

**Poznámka k uzavírání:** ready-to-close jsou u klientů `suspended` (M-line slíbil „nový tiskový modul"
polovina 2026 - viz §H). Zavírat po ověření dané featury v releasu TD + odkazem na konkrétní funkci
(vzor odpovědi = #8772/#7161: „vyřešeno v novém Návrháři sestav, viz wiki").

### 0.2 Nové požadavky bez samostatného tiketu (před uzavřením dohledat/založit ticket)

**TRANSDEV konsolidovaná sada (2026-07-03) - §TR:**

| # | Co | Vel. | Stav (2026-07-10) |
|---|---|---|---|
| TR1 | Časová osa přes půlnoc | - | ✅ = #9036 **resolved 2026-07-28** |
| TR2 | Na-znamení „všechny" souhrn (místo značek u každé) | S–M | ✅ **HOTOVO** (AD, opt-in `SOUHRN_NA_ZNAMENI`, gold-safe) - implementováno 2026-07-10, uzavřeno 2026-08-06 (Jan: SK ověření kódování proběhlo) |
| TR3 | Nástupiště jako sloupec vedle názvu zastávky | S | ✅ **HOTOVO 2026-07-10** (AD J-věta #3 + MHD Oznacnik; StopList sloupec „Nástupiště" opt-in). Ověřeno 0320680. |
| TR4 | Explicitní „fixed-then-shrink" výška řádku StopList | S–M | ✅ **HOTOVO 2026-07-10** - pevná `DataRowHeight` se zmenší při přetečení (default auto beze změny); sdílený helper `ScaledDataRowHeight` (StopList + DepartureGrid). |
| TR5 | Definice cíle referencovatelná v šabloně | M–L | 🤝 **DOHODNUTO s kolegy 10. 8.** - do jízdních řádů přidají **sloupec pro zadání cíle**; naše část pak je pole přečíst a nabídnout v šabloně. ⚠️ Ověřit, že je **per zastávka × směr** (střední varianta) - jen per směr už umíme dnes přes `POPIS_SMER`=konečná. (min) HOTOVO, ideál = číselník cílů zůstává mimo |
| TR6 | Barva linky (bg/text ve vlastnostech linky → volba v šabloně) | M | ✅ **HOTOVO 2026-08-03** (#12997): JŘ umí víc barev na linku (Petr), agenda je vydává jako `BARVA_LINKY` / `BARVA_LINKY_2` a jdou zapsat do každého barevného pole šablony. TRANSDEV jede AD (§A6), takže i jejich MHD Trenčín je pokryté. |
| TR7 | Výběr sloupců variant jízdních dob (stabilní číslování, per-písmeno) | S–M | 🟡 **půl hotové, druhá půlka POTVRZENA zákazníkem 10. 8. jako #12843 bod 4 - je to týž požadavek dvakrát, vést dál jako jeden.** ✅ Hotová část: nestabilní číslování je pryč (#13048 bod 5, 27. 7.) - sloupce se nezapínají ručně, varianty se seskupují dle sledu zastávek (0330510: 33 → 11 sloupců), rozvinou se samy a mají **písmena variant** místo pozičních čísel; + parametr „Řazení variant tras: Stabilní". Tipování a tisk po úsecích tím odpadá. 🔴 **Zbývá to, na co se ptal znovu: vybrat, KTERÉ varianty tisknout** („jeden alebo všetky" je dnes jediná volba). ⚠️ Dřívější formulace „v návrháři ten problém neexistuje" (6. 8.) byla nepřesná - platila jen pro číslování. Před návrhem se doptat: ruční výběr per linka × pravidlo (nejčastější / obsluhující daný úsek), a jak často se volba mění |
| TR8 | „<1" místo „0" u nulové jízdní doby (konfigurovatelné) | S | 🟢 **premisa vyjasněná 10. 8. (Jan) - je to NAŠE práce, blok `B64`.** Zákazník musí mít na mysli **první úsek**: doba jízdy se počítá od vývěskové zastávky, takže její vlastní řádek je 0 a hned následující zastávka je taky 0, jede-li se tam pod minutu. Jinde v tom sloupci nula pro jízdu nevzniká. Detail a past → §TR/8 |
| TR9 | Obrácená logika poznámkování (rozsah TČ v 1. tabulce) | M | 🤝 **DOHODNUTO s kolegy 10. 8.** - zapracují do zadávání jízdních řádů; tisková strana se nemění, u nás žádná práce |
| TR10 | Skrýt spoje o So/Ne/svátcích | S | ✅ **HOTOVO - pohlceno `B39`** (rozhodnutí Jan 7. 8., dokončeno 10. 8.): vlastní sloupce umí „bez spojů se značkami" per sloupec, takže samostatný přepínač nad linkovým seznamem se nedělal. Dřívější zápis „malá varianta jde u nás" tím padá |

**Z prezentací zákazníkům - §P:**

| # | Co | Vel. | Stav |
|---|---|---|---|
| P3 | Imposice 2×A5 → A4 | - | ✅ **HOTOVO 2026-07-12** (v1: N-up „Uspořádání na list" v tiskovém dialogu, mřížka AUTO, opakování pro řezání, ořezové značky; per-agenda persistence). Brožura/duplex řazení = vědomě v2 (knižní JŘ). Detail §P3. |

> **P1 a P2 vyřazeny (2026-07-10, dohoda):** P1 (poznámky do patičky) - nejasné přesné zadání, neodbavovat;
> P2 (sloučit více linek) - řeší se jinde (mimo tuto agendu). Detail v §P ponechán jako evidence.

### 0.3 #13048 KOVED - poznámky a sloupce zastávek (5 bodů, procházeno 2026-07-24→26)

Excel od IDZK (Lábsky) k zastávkovému JŘ z našeho návrháře. Body:

| # | Co | Vel. | Stav |
|---|---|---|---|
| 1a | Sloupec zón prázdný - ověřit, že něco vracíme | S | ✅ **HOTOVO 2026-07-24** - zóny 1:1 vs legacy (gold oracle `ZastavkoveJrAdLegacyTest`); prázdno u KOVED = jejich data/šablona, ne regrese |
| 1b | Přestupy (na znamení / vlak / bus) nejsou vidět | S–M | ✅ **HOTOVO 2026-07-26** - přepínač „Přestupy (vlak/bus)" na StopListu (vlak L / autobus A), default vyp = jako legacy; symbol u zastávky, ne v legendě. Na-znamení funguje beze změny |
| 2 | Per-buňka zmenšení fontu na šířku (shrink-to-fit) | S–M | 🟡 **IMPLEMENTOVÁNO 2026-07-27 jako vestavěné chování** (bez checkboxu, rozhodnutí Jan) - přetékající text buňky se v `CellRenderer` zmenší na šířku (trigger až skutečné přetečení; rezerva 0,6 mm, u názvů zastávek dle geometrie šipky 2,6 mm; značky za názvem se posouvají a škálují s textem). Platí pro buňky přes CellRenderer (StopList/DynamicTable/LineTable/záhlaví); NE legenda a minuty (vlastní render). Čeká finální vizuální ověření |
| 3 | Sloupec nástupiště | S | ✅ **OVĚŘENO 2026-07-27** - KOVED chce „čísla sloupků z trasy" = přesně TR3 (J-věta `#3` per směr, sloupec „Nástupiště" opt-in). Část „příp. z detailu spoje" = per-spoj nástupiště z **W-trasy** (zatím nečteme) → splývá s bodem 4 |
| 4 | Zastávka s více nástupišti (více řádků/hodnot) | M | ✅ **HOTOVO 2026-07-27 - všechny 3 režimy.** Přepínač „Více nástupišť" na StopListu: nejčetnější / výčet „2,3" / **duplikace řádku per stanoviště** (`RowPerPlatform`). Duplikace: klientská expanze `ExpandPlatformRows` nad aditivním serverovým polem `NASTUPISTE_VARIANT` (nástupiště per varianta, W pozice 7). Zastávka se ≥2 stanovišti → N řádků na místě v trase, časy jen variant daného stanoviště, vývěska/jednostanovištní = 1 řádek. Ověřeno daty 0330510 (`TestNastupisteVariantEmit` + 5 xUnit). Vývěsky per sloupek (samostatné stránky) = #8828 |
| 5 | Varianty tras (řazení dle četnosti, max čas, písmena, MHD parita) | M–L | ✅ **HOTOVO 2026-07-27, vizuálně ověřeno Janem na 0330510** - seskupování variant předěláno na **sled zastávek s max časy** (= legacy `prutmbmp` dedup; 0330510: 33 sloupců → 11; smoke test odhalil i letitou chybu `|`=„čas 0" a bool parametry na wire). Parametry (skupina Seznam zastávek): „Řazení variant tras" (od 13. 8. čtyři volby - dle četnosti, stabilní, dle délky jízdy oběma směry) a „První varianta bez písmene" (default zap). ⚠️ **Zápis „Písmena variant u odjezdů (default vyp; písmeno u minuty nahrazuje rozdílové poznámky)" už neplatí dvakrát:** od 13. 8. se písmeno a rozdílová poznámka tisknou **vedle sebe** (`B67`) a 17. 8. ten parametr **zrušen** - písmena jsou vždy, protože bez nich nešlo minutu spárovat se sloupcem (`B90`). **Nástupiště v identitě varianty vědomě NE** (duplicitní vizuálně identické sloupce) - „jiné stanoviště" řeší duplikace řádku (bod 4 `RowPerPlatform`) + #8828 vývěska per označník. MHD parita variant = mimo scope v1. Symbol „↓" u výchozí zastávky = odložený drobný vizuál |

Detaily hotových: changelog 2026-07-24 (zóny) + 2026-07-26 (přestupy) + 2026-07-27/28 (body 2, 4, 5, titulek, slovník „označník"). **Tiket ZAVŘEN - klient 30. 7. potvrdil, že zmenšování textu funguje, jak čekal** (poslední otevřený bod 2). Reklamace = znovuotevření tiketu.

**Dispatch:** malí kandidáti odbaveni (TR3 ✅, TR4 ✅, TR2 ✅, TR8 ⏸️ park do potvrzení TRANSDEV).
Zbývají jen 🟠 velké body (analýza/data).

### 0.4 Follow-upy uzavřených tiketů - JEN na vyžádání zákazníka (nezakládat dřív)

Vzniklé při stanovišťovém balíku a #9036 (27.-28. 7.); tikety jsou resolved, tohle jsou připravené
odpovědi na případné znovuotevření/dotaz:

- **Členění exportu na samostatné soubory per označník** - dnes rozdělené vývěsky rozliší titulek
  i `{POPIS_ZASTAVKA}` v názvu souboru (#8828)
- **Symbol „↓" u výchozí zastávky** ve variantních sloupcích (KOVED XLSX vizuál, bod 5)
- **Agregace označníku varianty přes všechny spoje** - dnes reprezentativní spoj; projeví se jen
  u dvou spojů se zcela shodnou trasou a různým označníkem (bod 4/5, přiznáno v odpovědi KOVED)
- **Duplikace řádku - fáze 2 dle zpětné vazby KOVED** (bod 4)
- **Per-písmeno výběr variantních sloupců** (TR7 zbytek - až TRANSDEV potvrdí nad novým chováním)
- **Vizuální oddělení přelomu půlnoci v mřížce** (#9036 v1 = jen přetočení osy; kandidát silnější
  čára mezi 23 a 0, automaticky jen když osa přetéká)
- **Zvýraznění ne-výchozích hodnot v liště parametrů** (per-agenda dědění hodnot mate - rozhodnutí
  Jan 28. 7.: zatím nechat)
- **Spodní mez zmenšení textu buňky** (extrém se zmenší bez limitu - zvážit při vizuálu bodu 2 v B2)
- **Přenos uložených voleb zrušených parametrů kolo / místenky / souhrn bezbariérovosti** do vlastností
  prvku poznámek (bývalý `B52`, nález review 4. 8.; **rozhodnutí Jan 5. 8.: neřešit** - je to v pilotním
  testování, počet dotčených uživatelů je malý). Co se stane, když si někdo stěžuje: blob
  `^ALVA.EDControls.UserData(IdFa,user,"TemplateDesigner.PrintParams",<AgendaCode>)` s `TISK_KOLA=true`
  se od přesunu do prvku ignoruje → značky mu z tisku zmizely; `SCHOVAT_AGREGAT_BEZBARIER=true` naopak
  znamená, že se souhrn vrátil. Náprava = zapnout to znovu ve vlastnostech prvku poznámek (jednorázově
  per šablona). Automatická migrace blobu je možná, ale mapování není 1:1 - parametr byl per
  uživatel × agenda, vlastnost je per šablona.

### 0.5 Parita a rozšíření - JEN na poptávku (přesunuto z roadmapy 29. 7.)

- **Loď, wifi a zásuvka jako piktogram i ve vývěsce** (dřív `B99`, z roadmapy odstraněno 31. 8.).
  Ve fontu jsou (180 / 186 / 179) a **linkový** JŘ je piktogramem kreslí. ⚠️ Vývěska je ale jiný
  případ: její starý modul (`ALVA.TISKMHD.Main.ConvertSpecialniZnak`) zná jen dny, vozíček, částečnou
  bezbariérovost a svátky - loď, wifi ani zásuvku **vůbec nemá**. Piktogram by tam tedy nebyl
  dorovnáním starého tisku, ale novou featurou nad jeho rámec, a změnil by hotový tisk zákazníkům,
  kteří o to nežádali. Práce je malá (jeden řádek v mapě značek), rozhodnutí není.

- **Oddělená barva a tloušťka vodorovných × svislých čar mřížky** (dřív `L9-R3`, z roadmapy
  odstraněno 31. 8. - rozhodnutí Jan). Dnes má mřížka jednu barvu, tloušťku i styl pro obojí;
  zapnout/vypnout jednotlivé druhy čar už jde (`L10`, `L15`). ⚠️ **Nikdo o rozdělení nepožádal**
  a vzor, kvůli kterému vlna `L9` vznikla (IDZK 940), ho nepotřebuje - má jen svislé čáry jednou
  barvou, což umí. Kdyby se poptávka objevila, ví se rovnou i tvar řešení: `GridStyle` je sdílený
  se všemi tabulkovými prvky, takže se to musí udělat pro všechny naráz, jinak se dvě tabulky
  v téže šabloně nastavují jinak (rozhodnuto 26. 8.).

Není to nedodělek, ale práce, která se vyplatí až s konkrétním zákazníkem - v aktivní roadmapě
jen zabírala místo (pravidlo „roadmapa = akční práce"). Velikosti: S = do hodiny, M = půl dne.

- **Odlišit písmena variant tras od písmen rozdílů tras** (S–M, zjištěno 17. 8.) - varianty berou
  `A, B, C…`, rozdíly tras sadu `BCEFGHIJOQRW…`, takže **od „B" se překrývají**: na lince, která má
  obojí, může „B" u jedné minuty znamenat sloupec a u jiné rozdíl trasy. Starý modul na to měl
  číselník. **Rozhodnutí Jan 17. 8.: čekat, až to někdo nahlásí jako reálný problém** - projeví se
  tiše a jen tam, kde se obojí sejde na jedné vývěsce
- **Tiskový parametr „Rozdělení sloupců" pro MHD agendu** (M) - sjednocení s AD, kde už je
  (MHD SMO fáze 3)
- **Parita variant tras + písmen u odjezdů pro MHD agendu** (S–M) - AD hotové 27. 7. (#13048 bod 5),
  MHD až u zákazníka, který to bude chtít
- **MHD zdroj označníku pro samostatné stránky per označník** (S–M) - AD hotové, dtto
- **Exportní parametry per šablona** (S–M) - název souboru / členění / imposice jsou dnes per agenda;
  převod na šablonu má smysl, až někdo bude mít dvě šablony téže agendy s jiným exportem
- **Per-sloupcové zvýraznění řádků** (`HighlightRule.TargetColumn` - model to pole nese, render
  barví celý řádek) + **barva textu / kurzíva** pravidla zvýraznění
- **Přepínač „Přestupy (vlak/autobus)" u MHD neovládá to, co má** (M; z roadmapy 13. 8. jako `B48`,
  odebráno stejným rozhodnutím jako `B79`). MHD posílá přestupy jako **pozici ve fontu** (`w`/`x`),
  ne jako písmeno, takže je `IsTransferAttr` nepozná a řídí je obecný přepínač „Atributy". **Tisknou
  se správně**, jen se ovládají jinak než u AD. Fix = tagovat ATRIBUTY kategorií `prestup` na wire.
  Dopadne to na firmu s MHD modulem (dnes DPCB/DPKV), která bude chtít přestupy vypnout zvlášť.
- **Jazyk tří automatických vět v MHD agendě** (S; z roadmapy 13. 8. jako `B79`, odebráno na
  rozhodnutí Jan - žádná slovenská firma dnes MHD modul nemá). Dny v týdnu u kroužkových značek
  („jede v pondělí"), automatická poznámka typu provozu („jede jen &lt;typ&gt;") a automatické rozdíly
  tras („končí v zast. X") jsou napevno česky; parametr `JAZYK` se do MHD agendy posílá, ale
  ignoruje se. Fix je stejný vzor jako `B69` u AD (přepínač jazyka + texty). Dopadne to teprve na
  slovenského zákazníka s MHD modulem - dnes ho mají DPCB a DPKV, MHD Nitra jede na AD agendě.

---

## A. StopListElement - seznam zastávek vlevo

### A1. ✅ HOTOVO (2026-05-12) - viz §G

### A2. ✅ HOTOVO (2026-05-12) - viz §G

### A3. ✅ HOTOVO (2026-05-12) - viz §G

### A4. ✅ HOTOVO (2026-05-12) - viz §G

### A5. 🟡 Základ hotový pro AD (2026-07-09) - per-směr / per-Tč otevřené

- **Zákazník:** TRANSDEV (Michal Podloučka), #7937 - TRANSDEV jede primárně **AD** (viz A6), takže cílová agenda = `ZastavkoveJrAd`.
- **Citace:** *„potřebuji mít možnost odstadit/odrazit zastávku o určitý počet bodů, ale pouze pro zastávkové JŘ a pro jednotlivé směry (v jednom směru ano, v druhém ne)"*
- **Dodatek:** *„zda by šlo odražení nastavit tak, že se bude odrážet zastávka v nastavených Tč vývěsu"* (per časové pásmo)
- **✅ Hotovo - základní odsazení (AD):** datový flow dotažen. `ZastavkoveJrAd` čte odsazení z
  `^JRL(sku,lin,"J",poradi)` **5. `#`-segment** (legacy `mkPLinWin` „odrazeni nazvu zastavky" = `gTrsLin dta 7`,
  ověřeno `JRapiLin.mac`/`JRapiLinPrtWin.mac`) → `BuildZastavka ODSAZENI` → element `z.Odsazeni` → N mezer.
  Živě ověřen render. Behavior-preserving (gold netknuté). Commit `62852f2`.
  - ✅ TD model `ZastavkaData.Odsazeni` + `ShowStopIndentation` toggle + render (prefix spaces).
- **⚠️ Otevřené (rozšíření nad legacy - potřebují datový model + UI rozhodnutí):**
  - **Per-směr** („v jednom směru ano, v druhém ne") - `^JRL` dta7 je **jedna hodnota per bod trasy**,
    sdílená pro tam i zpět (i legacy má single-per-point). Odsazení se teď projeví v **obou** směrech.
    Per-směr = nutná změna zdroje (per-směr pole) + editor.
  - **Per-Tč pásmo vývěsu** - neřešeno.
  - **MHD strana** - `ZastavkoveJrMhdAPI` má pořád hardcoded `s odsazeni = 0` („legacy: dta 10" v `ALVA.MHD.ZST`).
    Relevantní jen pro self-contained MHD (DPCB/DPKV), ne TRANSDEV.
- **Render caveat:** mezery jsou proporcionální (počet mezer = počet znaků); kdyby vizuál nesedel s legacy, přepočet na pevnou šířku.

### A6. ✅ Hotovo (2026-06-20) - ověřeno na úrovni kódu pro AD; klient potvrdí na svých datech

- **Zákazník:** TRANSDEV (Michal Podloučka), #7959
- **Upřesnění agendy:** TRANSDEV jezdí primárně **linkovou (AD)** + lokální MHD; **self-contained MHD
  modul mají jen DPCB/DPKV**. Tiket se proto týká **AD agendy** (`ZastavkoveJrAd`), ne MHD modulu.
- **Citace:** *„Mám linku, která má druhou zastávku pro školní spoj... Ovšem z první zastávky na tuto druhou zastávku žádný spoj nepojede. Potřeboval bych, aby zastávka nebyla v seznamu zastávek, pokud není aktuální."*
- **Stav v AD - obě chování implementovaná a aktivní:**
  - **(a) StopList filtr** - neobsluhovaná zastávka se nepřidá do seznamu: `ZastavkoveJrAd:296`
    `i '$d(servedPoradi(poradi)) continue` (`ObsluhovanePoradi`).
  - **(b) Stránka se negeneruje** - zastávka bez odjezdů: `:333` `odjezdy.%Size()=0 q ""` + `:278` terminus-skip.
- **✅ Dořešeno 17. 8. - AD přepnuto na from-current sémantiku.** Dřív tu stálo, že AD filtr je
  **směrově-globální** (staví tam aspoň 1 spoj směru), kdežto MHD je **from-current**, takže pro
  školní scénář mohl AD zastávku ponechat - a že se to nechává, protože se těžko hledá testovací
  linka. **Linka se našla** (`CB_CSADAD` 0330530, zastávka Střítež,žel.st.točna měla „<" ve sloupci
  časů i ve všech osmi variantách) a AD teď bere jen zastávky, kde staví aspoň jeden spoj stavící
  i na té vývěsce (`ObsluhovanePoradiOd`). Agendy jsou tím srovnané.
  ⚠️ Seznam zastávek se proto **liší mezi vývěskami téže linky**; hlídá to invariant
  `TestSeznamNemaNedosazitelneZastavky` (každá zastávka v seznamu musí mít aspoň v jednom časovém
  sloupci číslo). Detail → changelog 2026-08-17 (4).
- **Opt-out** (kdyby chtěl vidět vše bez filtru) → param `TISK_VSECH_ZASTAVEK` (paralelní k `TISK_PROJETYCH_ZAST`), zatím YAGNI.

---

## B. DepartureGridElement - mřížka minut odjezdu

### B1. ✅ HOTOVO (2026-05-12) - viz §G

### B2. ✅ HOTOVO (2026-05-12) - viz §G

### B3. ✅ HOTOVO (2026-05-12) - viz §G (auto-fit zvětšení odloženo)

### B4. Per-sloupek zastávky - samostatná vývěska ⏸️ Odloženo (2026-05-12)
- **Zákazník:** TRANSDEV (Jakub Holík přes Michala Podloučku), #8828
- **Konkrétní příklad:** Linka 876420, zastávka „Těrlicko,,obecní úřad" má 2 sloupky. Většina spojů obsluhuje sloupek 2 (oba směry), ale vybrané spoje obsluhují sloupek 1. Druhý příklad: linka 863383, zastávka „Šenov,,Škrbeň" má dva sloupky v každém směru - koordinátor (KORID) generuje vývěsky **per sloupek** odděleně.
- **Citace klienta po prvním M-line návrhu** (rozdělit zastávku na 2 v trase linky): *„Vámi navržené nás s kolegy samozřejmě napadlo, ale asi to vždy není vhodné. (...) V JŘ je pouze jedna zastávka Šenov,,Škrbeň (...) Proto dále prosím o zvážení jiného způsobu řešení, než popisujete."*
- **Co máme dnes:** Multi-data PDF export generuje 1 PDF stránku per `(zastávka × směr)`. Nezná pojem „sloupek" - atribut sloupku není ani v `ZastavkoveJrMhdAPI.GetData`.
- **Co klient chce:** Pro zastávku s více sloupky vygenerovat **samostatnou vývěsku per sloupek** - jen spoje obsluhující daný sloupek se vytisknou.
- **Gap:**
  - Caché: rozšířit `BuildOdjezd` (nebo `PromitnutSpoje` fáze 4) o filtr per sloupek - předtím rozšířit working globální klíč o `sloupekID` subscript: `^||TiskMHD("odjezdy",SmerID,BodID,TypProvozuID,sloupekID,hh,mm,SpojID)`.
  - `GetData` API: pro zastávku s více sloupky iterovat `(Smer × Bod × Sloupek)` → `OUT[N]` per sloupek. Skalární info per stránka rozšířit o `SLOUPEK_CISLO` / `SLOUPEK_POPIS`.
  - TD: **žádná změna kódu** - multi-data export už zvládá libovolný počet OUT stránek. Volitelně placeholder `{SLOUPEK_CISLO}` v šabloně.
  - Pro: zachovat single zastávku v trase linky (klient výslovně odmítá duplikaci v trase).
- **Důvod odložení:** Téměř 100 % práce je v Caché a vyžaduje:
  - **Datový průzkum** - zjistit, kde v `ALVA.MHD.*` globálech leží info o sloupcích (atribut spoje? per-bod v trase `("ModelTrasaBodDetail",BodID,"Val","SloupekTam")`? jinde?). Není dokumentováno v `cache-database.md`.
  - **Business decision s kolegy** - identifikace sloupku (číslo 1/2/3, písmeno A/B, nebo popis?), default chování (opt-in přes parametr `PER_SLOUPEK` vs vždy zapnuté), chování pro zastávky s jedním sloupkem.
  - Po vyjasnění obojího je vlastní implementace odhad **~půl dne až den** (rozšíření working globálu + iterace v `SestavitStranky` + nový parametr v `AddStandardJRParameters`).

---

## C. NotesLegendElement - legenda poznámek pod mřížkou

### C1. ✅ HOTOVO (2026-05-12) - viz §G (DB-driven barvy z PoznamkaSpoj)

### C2. ✅ HOTOVO (2026-05-12) - viz §G (verifikace, žádný kód)

### ~~C3. NZ + týdenní platnost - 4 na řádek (úspora místa)~~
**Mimo scope zastávkového JŘ** (přesunuto, 2026-05-12). Tiket #7487 (KORID) se týká **knižních/linkových** JŘ, ne zastávkových. Patří do roadmap knižních JŘ až bude existovat.

---

## D. Generování poznámek a obsluhových údajů (Caché `ZastavkoveJrMhdAPI`)

### D1. Text „spoj jede až do XY" pro spoje za základní trasu - ✅ ZAVŘENO 30. 7. 2026
- **Zákazník:** TRANSDEV (Michal Podloučka), #7947
- **Výsledné znění:** `jede až do zast. <konečná spoje> přes <zastávky za koncem základní trasy>`,
  pak teprve „jede přes" / „neobsluhuje". **Cíl je konečná daného spoje, ne konec základní trasy** -
  věta vzniká právě tehdy, když spoj obsluhuje zastávky za koncem základní trasy. Zrcadlová věta
  pro opačný případ („jede jen do zast. X") jmenuje rovněž konečnou spoje.
- **Zdroj základní trasy:** příznak per zastávka × směr v J-větě (segment #10; fallback tištěná trasa
  #7; když není ani ta, spočítá se jako sjednocení obsluhovaných zastávek → **pak tato věta nikdy
  nevznikne**, což je případ 62 422 z 62 432 kombinací linka×směr v CB_CSADAD).
- **Dosah změny:** jen větev prodloužení; ostatní poznámky byte-identické (zlaté digesty beze změny).
  Starého tiskového modulu se netýká vůbec.
- **Citace (zjednodušeno z dlouhého popisu):** Pro linky 876402, 876401, kde málo spojů jede až do koncové (Stonava, Nový svět / Horní Suchá, Pašůvka):
  - Pokud základní trasa končí v předposlední → spoje do poslední dostanou poznámku „jede přes zast. XY" (XY = předposlední) - **nesprávně formulováno, popisuje co bus míjí, ne kam dojede**.
  - Pokud základní trasa končí v poslední → všechny spoje se písmenkují (přebytečné info, když do koncové jezdí 2 spoje).
  - Pokud obě poslední mimo základní trasu → ještě horší.
- **Co klient chce:** *„Asi bych potřeboval, aby program uměl vypsat u poslední zastávky poznámku, že spoj jede až do."*
- **Co máme dnes:** `MakeOmezTrasaTxMH...` legacy helper + algoritmy 1001 (písmenková: a, b, c) a 1002 (písmena rozdílů tras: A, B, C) v `ZastavkoveJrMhdAPI`. Texty popisují, **kde spoj končí** (zkracuje trasu), ne **kam pokračuje** (prodlužuje).
- **✅ VYŘEŠENO PRO AD** (2026-06-21) - `ZastavkoveJrAd.RozdilyTras` větev prodloužení za vzor nově
  vypisuje **„jede až do zast. <konečná spoje>"** místo legacy matoucího „pokračuje přes <mezizastávky>"
  (znění A dle dohody - klient chce vědět KAM dojede). Záměrná odchylka od legacy (legacy „pokračuje"
  bylo špatné - to je důvod ticketu, proto bez gold oracle; kód analogický k ověřené větvi „jede jen do").
  **Pozn.:** prodloužení za základní trasu je v datech **vzácné** (J-věta #10 = obvykle nejdelší trasa);
  ověřeno proti CB_CSADAD 0345061/0365001 že běžné rozdíly (neobsluhuje/jede jen do) fungují 1:1 a změna
  je nedotkla. „jede až do" se projeví na lince, kde spoj reálně jede za vzor.
- **⬜ Zbývá pro MHD agendu** (`ZastavkoveJrMhd`) - stejná úprava neportována (až bude potřeba).

#### Změřený stav 30. 7. - měření z 29. 7. bylo neplatné (sonda `Tests/AdProdlouzeniProbe.cls`)

Ověřováno na `CB_CSADAD`, skupina 1 (240 linek, 9693 stránek).

**Měření z 29. 7. bylo kontaminované chybou cache v samotném generátoru.** `WMapa` /
`WNastupisteMapa` cachovaly W-mapu pod klíčem `^||TiskAd("wmap",cisloTrasy)` **bez linky** -
a `cisloTrasy` je číslo lokální v rámci linky. Sonda čte víc linek v jednom procesu, takže si linky
mapy navzájem přebíjely → cizí trasy vyráběly falešné rozdíly tras. **Opraveno** (linka je v klíči);
v produkci se to neprojevovalo, protože `GetData` jede jednu linku a cache na začátku maže.

Co z toho plyne pro tři „vady" evidované 29. 7.:

1. **Poznámky rozdílů tras se na stránku DOSTANOU** - původní vada 1 neexistuje. Doloženo:
   0365005 / Smrkovice / T má 6 textových poznámek (B, C, E, F, G, H), 0365001 / Vlakové nádraží / T
   jednu (B). Linka 0365003 z původní reprodukce žádné rozdíly tras nemá (po opravě cache
   `legendCelkem=0`) - „ztrácení" byl artefakt.
2. **Věta „jede až do zast. " bez cíle** byl týž artefakt (cizí bitmapa ukázala na pozici, kterou
   J-trasa nepojmenovává). Přesto **zpevněno**: nový `KonecnaNazev` vezme nejbližší pojmenovanou
   obsluhovanou pozici proti směru jízdy a bez známého cíle se věta nenapíše.
3. **„jede přes zast. X" + „jede až do" současně** - zkusili jsme „jede přes" u prodloužení vynechat,
   ale **měření to vyvrátilo a změna byla vrácena**: obě věty nesou různou informaci a po vynechání
   se texty různých skupin spojů staly identickými → dedup je sloučil do jedné značky. Doloženo na
   `CB_GWBUS` 501/0000001/T (viz níže): legacy tam má tři poznámky, po vynechání zbyla jedna.

#### ✅ Reprodukce nalezena - `CB_GWBUS`, skupina 501, linka 0000001, směr T

Hledání přes **všechny** skupiny a linky (ne jen skupinu 1): v `CB_CSADAD` 309 skupin / 31 216 linek /
62 432 kombinací linka×směr → **0 nálezů**, a to ze dvou nezávislých důvodů: 62 422 kombinací nemá
vzorovou trasu vůbec (fallback „union obsluhy", proti němuž nelze jet *za* vzor) a u zbylých 10, které
ji mají, je **vypnuté** auto-poznámkování. V téhle firmě tedy případ nastat nemůže.

V `CB_GWBUS` (2190 linek) ale ano - **12 výskytů** na okružní lince 0000001/T. Na stránce zastávky
Mikrotechna dorazí do legendy:

- `B` = „jede přes zast. Hlinecké sídl. , neobsluhuje zast. Dewetterova ul., **jede až do zast.
  Autobusové nádraží**"
- `C` = „jede přes zast. Hlinecké sídl., Poliklinika , neobsluhuje zast. Dewetterova ul., **jede až do
  zast. Autobusové nádraží**"

Věta má cíl, dorazí na stránku, má značku i řádek legendy → **funkčnost #7947 je ověřená**.

**✅ Skupiny spojů srovnány s legacy (oprava `|`).** Nejdřív jsme na té stránce měli **dvě** rozdílové
poznámky proti legacy **třem** a lišily se i seznamy zastávek. Příčina: `SpojBitmapa` počítala značku
`|` („zastávka je na trase, ale spoj v ní nestaví") jako obsluhovanou, zatímco `CasHodnota`
i `ObsluhovanePoradi` ji správně vylučují. Důsledky byly dva: falešné „jede přes zast. X" u zastávek,
kde spoj nestaví, a slévání skupin spojů, které se lišily právě jen `|`. Po opravě je grouping **1:1
s legacy** (B/C/E, tytéž seznamy, tatáž věta „neobsluhuje zast. Dewetterova ul.") - jediný rozdíl je
dohodnuté znění konce věty. Zlaté digesty se nezměnily (zlaté linky `|` nemají), CB_CSADAD beze změny,
v CB_GWBUS správně přibyly rozlišené skupiny (60 → 66 řádků legendy).

**✅ Znění dohodnuto a nasazeno (rozhodnutí Jan 30. 7.):** cíl jde **na začátek věty** a nese
**průjezd** - `jede až do zast. <konečná> přes <mezizastávky>`, pak teprve „jede přes" / „neobsluhuje".
Dva důvody: informace, kvůli které tiket vznikl, nesmí být na konci věty (legenda ořezává konec), a bez
průjezdu splývaly skupiny spojů lišící se jen cestou. Výsledek na ověřovací stránce:

- `B` „jede až do zast. Autobusové nádraží **přes Náměstí**, jede přes zast. Hlinecké sídl., Jiráskova
  ul., Milevská ul., Sokolovna, Poliklinika"
- `C` „jede až do zast. Autobusové nádraží **přes Náměstí, Hlinecké sídl.**, jede přes zast. Sokolovna,
  Poliklinika , neobsluhuje zast. Dewetterova ul."
- `E` „jede až do zast. Autobusové nádraží **přes Náměstí**, jede přes zast. Hlinecké sídl., Jiráskova
  ul., Sokolovna, Poliklinika"

Informačně tedy 1:1 s legacy (tytéž tři skupiny, tytéž zastávky), odchylka je pouze ve znění. **Bez
prodloužení se nemění nic** - pořadí i oddělovače (vč. legacy tvaru „ , " před „neobsluhuje") zůstávají
byte-identické, zlaté digesty beze změny.

**✅ Ověřeno i v běžícím buildu** (30. 7.): na téže stránce legenda **souhlasí se sloupci časů**
v zastávkovém seznamu - `C` říká „neobsluhuje zast. Dewetterova ul." a sloupec C tam má `<`, `E`
nezmiňuje Milevskou a sloupec E tam má `<`, `B` ji vyjmenovává a čas tam je. Před opravou značky
„spoj nestaví" si tohle protiřečilo. Užitečná uzavírací kontrola pro příště: **legenda × sloupce
variantních časů se musí shodovat** - je to vidět na jednom snímku bez nástrojů.

**✅ Vysvětleno - chybějící bezbariérová poznámka i chybějící odjezd byly tiše zapnuté „Rozdělit
vývěsku pro každý označník"** (30. 7.): stránka nesla jen odjezdy svého označníku, takže zmizel odjezd
`11:20` (značky `C` + bezbariér) a s ním celý řádek legendy `H` (plní se jen z vytištěných odjezdů).
Legenda `C` zůstala, protože rozdílové poznámky se per označník nefiltrují. **Nesouvisí s #7947** -
rozdíly tras se vykreslily všechny tři. Uložená hodnota parametru byla `true`, ale UI ukazovalo vypnuto;
opraveno (`B24`, jedna cesta pro hodnoty i UI + signál ve status baru). Zbylé vady rozdělení → `B25`, `B26`.

**⬜ Chybí zdroj atributů zastávky (tentýž blok `B23`):** na téže stránce má legacy řádek
„x = zastávka pouze na znamení", náš výstup ne. **Doměřeno:** není to filtr ani rozsah zastávek -
na té lince nemá „x" **žádná** zastávka v J-větě (jediné atributy jsou `TS` na nádraží). Legacy ho
bere odjinud; ve W-záznamu trasy jsou vidět atributy **per spoj** (např. `#7:6:1::V` = jen výstup),
které nečteme vůbec. Chybí nám tedy celý zdroj. Pozor i na znění: legacy „zastávka pouze na znamení"
× naše „na znamení".

**Nový nález z auditu - kolize značky (nezavírat s tiketem):** písmenná sada rozdílů tras (default
`BCEFGHIJOQRWeijqsvwxz`) obsahuje **H** a **F**, což jsou zároveň značky bezbariérových spojů →
na stránce se stejný symbol objeví ve dvou významech a legenda má dva různé řádky pod jedním znakem.
Změřeno na **9 stránkách** (0365005, 0365006), např. „H = spoj má bezbariérově přístupné vozidlo"
+ „H = neobsluhuje zast. Obchodní, jede jen do zast. Autobusové nádraží". MHD tuto třídu chyb nemá -
přiděluje písmena ze sdíleného poolu stránky a už použitý znak přeskočí. Oprava v AD je snadná
(vynechat obsazené znaky), ale **změní písmena na gold lince 0365005** → čeká na rozhodnutí, jestli
jít proti legacy paritě a přebaselinovat.

**Inventura zdrojů poznámek (240 linek / 9693 stránek, po opravě cache):**

| Zdroj (kategorie) | Značky u minut | Řádky legendy | Dorazí? |
|---|---|---|---|
| Datumová omezení (`omezeni`) | 24 062 | 14 924 | ✅ |
| Bezbariér spoje (`bezbarier`) | 17 490 | 4 546 | ✅ |
| Týdenní platnost (`tydenni-platnost`) | 12 299 | 7 536 | ✅ |
| Rozdíly tras (`rozdil-trasy`) | 946 | 190 | ✅ (jen 5 linek) |
| Atributy zastávek (`atribut-zastavky`) | 0 (záměr - symbol u zastávky) | 3 788 | ✅ |
| Bezbariér souhrnně (`bezbarier-agregat`) | 0 (záměr - bez značky) | 1 058 | ✅ |
| Ruční komentáře spoje (`rucni-komentar`) | 0 | 0 | ⬜ nelze ověřit - firma nemá data (0 linek s `SeT_SpeC`/`MhD_SpeC`) |
| Směrové poznámky linky (`smerove`) | 0 | 0 | ⬜ nelze ověřit - 0 linek s `SeT_MhD` `>`/`<`/`<>` |
| Písmena variant tras (`varianta-trasy`) | 0 | - | ⬜ opt-in parametr, default vypnutý |

**Křížová kontrola úplnosti prošla:** 0 případů „značka u minuty bez řádku legendy" napříč všemi
9693 stránkami. Rozsah „Spoje, nepoznámkovat" (`MHDSPNPZN`) nemá v této firmě data → parametr
„V rozsahu nepoznámkovat schovat" je zde no-op (proto signál o schovaných poznámkách počítá
skutečně skryté položky, ne zapnutý parametr).

### D2. Vlastní komentář spoje musí přebít auto-poznámku - ✅ ZAVŘENO 30. 7. 2026
- **Zákazník:** TRANSDEV (Michal Podloučka), #7955
- **Příklad:** Linka 402, spoj 20 jede jen v části trasy. Auto generuje „spoj jede pouze do zastávky XY". Klient chce vlastní text „ze zastávky XY pokračuje jako linka X do zastávky YX".
- **Pokus klienta:** zapnutí „Komentáře" → funguje, doplní písmeno + text. Současně ale nezmizí auto-poznámka.
- **Pokus o vypnutí přes „Spoje, nepoznámkovat":** chová se opačně - nechá auto-poznámku, schová vlastní komentář.
- **Citace:** *„Měl jsem za to, že když mám zapnuté automatické poznámkování, u vybraných spojů si v Komentářích zapnu vlastní poznámkování, a zadám do Spoje, nepoznámkovat stejné spoje a rozpětí Tč, budu mít u těchto spojů pouze svůj komentář. Toto mi ale teď nefunguje a je používána automatika a potlačen ruční komentář."*
- **Co máme dnes:** Auto-poznámka (rozdíly tras) a vlastní komentář se generují nezávisle ve fázi 6. Funkce „nepoznámkovat" má aktuálně sémantiku „vypnout vše kromě auto-rozdílů".
- **Co klient chce:** Pokud má spoj **vlastní komentář**, auto-poznámka rozdílů se pro tento spoj **NEgeneruje**. (Plus překlopit sémantiku „nepoznámkovat" - viz M-line interní odpověď: aktuální chování je dokumentované, ale neintuitivní.)
- **✅ VYŘEŠENO PRO AD agendu** (2026-06-20, commit `0b5838d`) - nový parametr sestavy
  **`SCHOVAT_V_ROZSAHU`** (Ruční komentáře [default] / Automatické (rozdíly tras) / Obojí). V rozsahu
  „Spoje, nepoznámkovat" volba „Automatické" schová auto-rozdíl trasy a nechá ruční komentář = přesně
  klientův požadavek. Řešeno **modelem nadmnožina + render-time TD filtr** (`NoteVisibilityFilter`),
  ne auto-suppress v Caché - legacy sémantika „nepoznámkovat" zachovaná (default), flip je opt-in volba.
  Viz `ad-data-mapa.md §5` + `feature-design-principles.md §5.2`.
- **✅ OVĚŘENO NA REÁLNÝCH DATECH 30. 7. 2026** (Jan v buildu + sonda na datové straně).
  **Testovací linka:** `CB_GWBUS`, skupina **501**, linka **0000002**, směr **Tam**, stránka
  **Autobusové nádraží**. Spoj **21** (odjezd **7:00**, negativní značka `43`) má přiřazený komentář
  `G` („spoj pokračuje ke Gymnásiu") **a** je v rozsahu „Spoje, nepoznámkovat". Kontrolní vzorek =
  odjezd **7:38** (spoj 27), který v rozsahu **není**. Data to potvrzují: u spoje 21 nesou příznak
  „v rozsahu" **všechny tři** značky (`B` rozdíl trasy, `43` omezení, `G` komentář).

  | Parametr „V rozsahu nepoznámkovat schovat" | 7:00 (v rozsahu) | 7:38 (mimo) |
  |---|---|---|
  | Ruční komentáře (výchozí = legacy) | `43` + `B`, bez `G` | `B` |
  | **Automatické (rozdíly tras)** | `43` + `G`, **bez `B`** ← požadavek zákazníka | `B` |
  | Obojí | jen `43` | `B` |

  Kontrolní vzorek dokazuje, že filtr pracuje **per spoj**, ne per značku - nelze omylem odpoznámkovat
  celou linku. **Tato linka je jediné místo, kde je featura testovatelná** (CB_CSADAD nemá `SeT_SpeC`
  ani `MHDSPNPZN` na žádné z 240 linek; GWBUS má jinak jen prázdný číselník) → **nemazat nastavení**.
- **Pozn. k MHD agendě:** tyto ruční poznámky jsou **AD-only** - `^JRL` konstrukty (`SeT_SpeC`/
  `MHDSPNPZN`/`SeT_MhD`). MHD agenda (`ZastavkoveJrMhd`) čte jiný zdroj (`^ALVA.MHD.*`) a tyto pole
  **nemá** (ověřeno 2026-06-20). Není co portovat, dokud se nepotvrdí, že MHD modul má vlastní
  ekvivalentní úložiště (dle kolegy pravděpodobně ne - nastavuje se jen v AD).

### D3. Bug - „Spoje, nepoznámkovat" maže i inverzní znaménko (NZ) - ✅ ZAVŘENO 30. 7. 2026
- **Zákazník:** TRANSDEV (Michal Podloučka), #7955
- **Citace:** *„obecně Spoje, nepoznámkovat ruší i označení inverzním znaménkem, což nevím, zda je záměr."*
- **Co máme dnes:** Funkce „nepoznámkovat" patrně omylem maže i `spojNZ` přidělení.
- **Gap:** Bug - NZ je v jiném datovém toku než ostatní poznámky, „nepoznámkovat" se ho dotýkat nemá. Opravit v 6. fázi `ZastavkoveJrMhdAPI`.
- **✅ V AD agendě bug NEvzniká** (2026-06-20) - `NepoznamkovatSpoj` + filtr se dotýká jen kategorie
  `rucni-komentar` / `rozdil-trasy` (dle volby); `omezeni`/`bezbarier` (kam patří NZ-style značky)
  filtr nikdy neschovává. Zůstává relevantní jen pro MHD agendu (legacy datový tok).
- **✅ OVĚŘENO NA DATECH 30. 7. 2026** - a to nejsilnějším možným způsobem: značka `43` u spoje 21
  (testovací linka viz §D2) **je** označená jako „v rozsahu", a přesto zůstala ve **všech třech**
  režimech parametru. Kdyby bug existoval, zmizela by. Zamčeno testem
  `NoteVisibilityFilterTests.Apply_NegativniZnackaVRozsahu_NezmiziAniVRezimuOboji`.

### D4. Bug - výběr spoje jen prvním číslem
- **Zákazník:** TRANSDEV (Michal Podloučka), #7955
- **Citace:** *„Výběr mezi spoji není možný zadáním celého čísla spoje, jde pouze první číslo."*
- **Co máme dnes:** UI vstup pro filtr spojů akceptuje jen jednociferné číslo.
- **Gap:** UI bug v EDISONu (Studio JŘ), ne přímo v TD ani v Caché API. Zaznamenáno - předat dál.

### D5. Skrýt poznámku „x - zastávka je na znamení", pokud sloupec se značkou je hidden
- **Zákazník:** TRANSDEV (Vladimír Šimko), #7161
- **Citace:** *„aby sa negenerovala poznámka »x - zastávka je na znamenie«, ak je v zostave zakázané zobrazenie stĺpca »Pracovní 2« (zaisťuje zobrazovanie značky »x« pred názvami príslušných zastávok)"*
- **Co máme dnes:** Atribut zastávek se vždy přidá do legendy přes `AddAtribut` ve fázi 6 - bez ohledu na to, zda je sloupec s atributem v šabloně viditelný.
- **Co klient chce:** Pokud sloupec, který zobrazuje značku `x` (v TD ekvivalent: `ShowSignalStopIndicator` na StopListu), je vypnutý → vynechat odpovídající popis z legendy.
- **✅ VYŘEŠENO PRO AD** (2026-06-21) - `NoteVisibilityFilter.ShouldHideNaZnameniLegend(template)`:
  když šablona má StopList, ale **žádný** nemá zapnuté `ShowSignalStopIndicator` („Na znamení"),
  filtr skryje řádky legendy kategorie `atribut-zastavky` („× na znamení"). Legenda automaticky
  následuje viditelnost značky - žádné nové ovládání (uživatel jen odškrtne „Na znamení" na StopListu).
  Žádná Caché změna (legenda už je tagovaná). **Test:** CB_GWBUS **0320144** (má „x - zastávka pouze
  na znamení") - odškrtnout „Na znamení" na StopListu → značky „×" i řádek legendy zmizí.
- **✅ MHD agenda** (2026-07-15) - značka fungovala přes sdílený element už dřív; skrytí legendy
  doplněno tagem kategorie v `ZastavkoveJrMhd.BuildPoznamka` (řádek „na znamení" = `atribut-zastavky`,
  rozpoznán dle popisu). Sdílený `NoteVisibilityFilter` ho schová stejně jako v AD. MHD gold
  re-baselinováno (jen md5, additivní tag). Rozpoznání „na znamení" je popisové → symbol se mezi
  agendami/firmami liší (AD „x", MHD „D"), popis „na znamení"/„na znamenie" je konzistentní.

### D6. Vypnutí auto-poznámky „všechny spoje s bezbariérovým vstupem"
- **Zákazník:** TRANSDEV (Vladimír Šimko), #7161
- **Citace:** *„Možnosť používateľsky nastaviť, aby sa automaticky negenerovala poznámka »všetky spoje s bezbariérovým vstupom do vozidla«."*
- **Co máme dnes:** `AddAtribut` v `ZastavkoveJrMhdAPI` zřejmě generuje shrnující text, když 100% spojů má daný atribut (např. bezbariérovost). Vypínání není.
- **Co klient chce:** User option v parametrech tisku - „negenerovat agregovanou poznámku" / per-atribut switch.
- **✅ VYŘEŠENO PRO AD** (2026-06-21) - parametr sestavy **`SCHOVAT_AGREGAT_BEZBARIER`** (bool). Agregát
  „všechny spoje mají bezbariérově přístupné vozidlo" (vzniká při `allBBP` = 100 % spojů bezbariér) je
  v legendě tagován kategorií `bezbarier-agregat`; TD `NoteVisibilityFilter` ho při zapnutém parametru
  skryje. Per-spoj „spoj má bezbariérově…" zůstává. Filtr-model jako #7955.
  **✅ Ověřeno na CB_CSADAD 0365004** (2026-06-21) - má agregát „všechny spoje mají bezbariérově přístupné
  vozidlo"; zapnutí parametru řádek korektně skryje, per-spoj bezbariér (0365001/0365005) zůstává.

### D7. Skrýt prázdné přípojové řádky ➖ přeřazeno k linkovým JŘ
> **Stav (2026-06-30):** přípoje/návaznosti **nejsou koncept zastávkového JŘ** - patří k linkovým
> JŘ (konzistentní s AD-N14). Řeší se až s agendou linkových JŘ, ne v zastávkovém. Ponecháno zde
> jen jako evidence tiketu #11300.
- **Zákazník:** KORID (Jaroslav Smištík), #11300
- **Citace:** *„zobrazujeme přípoje v příbojových řádcích, občas jsou uzly, kde je provoz celotýdenní, ale přípoje jsou jen v PD nebo naopak jen o víkendech, uvítali bychom, kdyby byla možnost prázdný přípojový řádek (v situacích kdy je PD a víkend na zvláštní tabulce/listu) netiskl a šetřil místo."*
- **Z M-line reply:** „Aktuálně není možné skrývat prázdné přípojové řádky. Pro tuto možnost bude nutné upravit tisk JŘ (...) jeho řešení pravděpodobně spojíme s novým tiskovým modulem pro JŘ" (2025-12-08, Jiří Bžoch).
- **Co máme dnes:** Přípojové řádky **nejsou** v TemplateDesigneru implementované - je to legacy fička.
- **Co klient chce:** Přípojové řádky obecně + filter na neprázdné. Pokud zastávka má v dané vývěsce (skupina dnů = PD nebo víkend) prázdný přípojový seznam, nevykreslit řádek.
- **Gap:**
  - Nová doménová struktura: `Pripoj` (data per uzel × skupina dnů) v `ZastavkoveJrMhdAPI.GetData`.
  - Nový element v TD (např. `ConnectionsListElement`) s `HideEmptyRows: true`.

---

## E. Návrhář sestav (TemplateDesigner UI)

*(Žádné aktivní položky - E1 přesunuto do §G Hotovo.)*

---

## P. Nové požadavky z prezentací zákazníkům

> Zaznamenáno z živých prezentací/seminářů (ne z helpdesk tiketů). Neanalyzováno - evidence, že se na to zákazníci ptali. Před implementací založit/dohledat ticket a ověřit přesné zadání.

### ~~P1. Poznámky ručně do patičky~~ ❌ Vyřazeno (2026-07-10)

**Neodbavovat** - dohoda: nejasné přesné zadání (volný `TextElement` v Footer už funguje; jestli chce klient
dedikované pole „Poznámky", není potvrzeno). Až přijde konkrétní požadavek/ticket, znovu založit.

### ~~P2. Sloučení více linek se stejným posledním trojčíslím~~ ❌ Vyřazeno (2026-07-10)

**Neodbavovat zde** - dohoda: řeší se jinde (mimo tuto agendu / úroveň, ne v rámci zastávkového JŘ dispatch).

### P3. Logická imposice - 2× A5 na jednu A4 ✅ HOTOVO v1 (2026-07-12)

- **Zdroj:** prezentace zákazníkům 2026-06-24; požadavek: N-up (2×A5 → A4), ideálně i duplex/brožura.
- **Řešení (zvolena varianta C - sheet layout vrstva):** `Models/Layout/SheetLayout.cs`
  (`SheetComposer` čistá logika), exportní pole v tiskovém dialogu „Uspořádání na list"
  (Vypnuto / A4 / A3 × orientace) + mřížka AUTO (největší 1:1 fit, bez zmenšování) + „Každá
  stránka na celý list" (táž vývěska N× pro řezání) + ořezové značky; persistence per agenda;
  platí pro tiskový dialog i export z hlavního okna (PNG bez imposice). Detail → changelog 2026-07-12.
- **Vědomě v2 (s knižním JŘ):** brožurové řazení stran + duplex - PDF duplex nekóduje,
  přední/zadní zůstává na tiskárně.

---

## F. Co je v TemplateDesigneru NAVÍC oproti legacy

Při komunikaci se zákazníky uvádět jako benefity nového řešení:

- **CellStyle kaskáda** - `column.Style → element.HeaderStyle/DataStyle → element defaults → CellDefaults`. Vše viditelně v dialozích sloupců s tri-state „dědit / přepsat" stylingem (placeholder zděděné hodnoty).
- **Per-column** styling napříč StopList / DepartureGrid / NotesLegend / TableElement.
- **WYSIWYG editor** s live preview a okamžitým náhledem mock-dat (per agenda).
- **Multi-data PDF export** - jeden tisk vygeneruje N PDF stránek (1 per zastávka × směr).
- **AI Vision import pipeline** - automatické rozpoznání struktury z existujícího PDF JŘ.
- **VisibilityExpression** na sekcích i elementech (pokrývá #8772).
- **Element groupy** + drag&drop z palety, snap, undo/redo, per-template editor preferences.

---

## G. Hotovo / vyřízeno

### B1. Per-line font v dvouřádkovém záhlaví skupin dnů (DepartureGrid)
- **Tiket:** #10660 (DPKV, Jan Slavík); **uzavřen 31. 7. 2026**.
- **Datum:** 2026-05-12
- **Zjištění:** Data jsou **už rozdělená na 2 řádky** napříč všemi vrstvami (Caché `NadpisSMOPrvni`/`NadpisSMODruhy` → JSON `NADPIS`/`NADPIS2` → TD `SkupinaDnuData.Nadpis`/`Nadpis2`). TD je ale dříve před wrap fází konkatenovala do single string → ztracená hranice mezi nadpisy.
- **Implementace:**
  - Nový enum-record `HeaderLine(Text, IsSecondary)` zachová hranici Nadpis vs Nadpis2 až do render fáze.
  - Nová property `HeaderLine2Style: TextStyle?` na `DepartureGridElement` s cascade `HeaderLine2Style → HeaderStyle → ScheduleHeaderDefaults`.
  - Wrap logika: pokud `Nadpis2 != ""` → 2 distinct řádky bez word-wrap (každý si vejde sám); pokud prázdné → word-wrap pouze Nadpis (současné chování).
  - Height computation: per-line heights (Line 1 vs Line 2 font size), součet per group, max přes groupy.
  - Render: per-line style/font/paint selection podle `IsSecondary`; cumulative `runningY` pro různé výšky řádků; decorations (Underline/Strikethrough) per-line.
  - Pre-create `groupHeaderStyles2/Fonts2/TextPaints2` cache - vytváří se jen pokud `HeaderLine2Style != null` (žádná overhead při default).
  - UI (stav 31. 7. 2026): ve formuláři prvku, skupina **„Styl záhlaví"**, tlačítko **„Styl 2. řádku
    nadpisu" → „Upravit styl…"** (velikost, tučné, kurzíva, barva jsou v pod-dialogu). Lazy-init helper
    `EnsureHeaderLine2Style()`. ⚠️ Původně to byly 4 samostatné položky ve skupině „Záhlaví - 2. řádek"
    v properties panelu; po migraci editace na formuláře z Caché je to tlačítko. **Kdo popisuje UI
    zákazníkovi, ať si popisky ověří ve `*Edit.cls`, ne v tomto zápisu** - tady to jednou svedlo
    odpověď na neexistující pole.
- **Default chování zachované:** `HeaderLine2Style = null` → `hasLine2Override = false` → Line 2 použije stejný styl jako Line 1, žádná regrese pro existující šablony.
- **Pattern:** plně extensible přes TextStyle cascade - uživatel může v budoucnu doplnit per-column override Line 2, HAlign, padding atd. bez schema změny.

### A2. Konfigurovatelná pozice značek u zastávek (StopList)
- **Tiket:** #10660 (DPKV, Jan Slavík); **uzavřen 31. 7. 2026** (layout řešen i pro CBDP).
- **Datum:** 2026-05-12
- **Diagnóza:** Stávající TD renderoval značky (na znamení `×`, atributy, poznámky) vždy **za názvem** zastávky (CBDP layout). DPKV chce na znamení jako **prefix** před názvem (např. „× Globus") + chtějí, aby názvy zůstaly **zarovnané** i u zastávek bez značky.
- **Implementace:**
  - Nový enum `StopMarkersSide { AfterName, BeforeName }` + property `MarkersPosition` (default `AfterName` = CBDP layout).
  - Když `BeforeName`: render rozdělí sloupec zastávek na **2 sub-cells**:
    - **Marker sub-cell** (úzký, right-aligned) - značky
    - **Name sub-cell** (zbytek šířky, left-aligned) - pouze název
  - Šířka marker sub-cell je **auto-computed** podle nejširšího marker textu v datech přes `SKFont.MeasureText` + padding `MarkersSubCellPaddingMm = 1.0mm`. Pro běžné single-char markery (`×`, `D`) ~2.5mm.
  - Total width sloupce zastávek se nemění → žádný overflow.
  - Všechny 3 typy značek (na znamení + atributy + poznámky) se přepínají najednou jedním přepínačem. Per principles - minimum, per-marker per request later.
  - UI: `EnumPropertyDescriptor` „Pozice značek" v group „Zastávky" s labely „Za zastávkou" / „Před zastávkou".
- **Architektonický learning:** první návrh prepended markery do textu buňky (`„× Globus"`). To posouvalo názvy zastávek per řádek podle přítomnosti značky → nezarovnané. Druhá iterace s fixed 4mm sub-cell přidala vzdušnost ale byla zbytečně široká. Třetí (final) s auto-fit measure se přizpůsobí datům přesně. **Symetricky paralelní s A1 (DirectionArrowSide) - bounded enum, ne template variant.**

### A1. Konfigurovatelná pozice šipky směru jízdy (StopList)
- **Tiket:** #10660 (DPKV, Jan Slavík); **uzavřen 31. 7. 2026** (CBDP chtěli šipku vlevo, proto je volitelná).
- **Datum:** 2026-05-12
- **Diagnóza:** Stávající TD šipku kreslil VŽDY vpravo (DPKV preference). CBDP a podobní zákazníci, kteří dříve používali legacy se šipkou vlevo, neměli volbu.
- **Implementace:**
  - Nový enum `DirectionArrowSide { Right, Left }` + property `DirectionArrowPosition` (default `Right`, zachová zpětnou kompatibilitu).
  - Asymetrická pozice šipky (architektonicky čistá):
    - **Vpravo:** šipka UVNITŘ sloupce zastávek u pravé hrany. Funguje, protože text je left-aligned a k pravé hraně nezasahuje.
    - **Vlevo:** šipka MIMO sloupec na levé straně (1.5mm vlevo od levé hrany). Bracket vede zpět k hraně sloupce. Tento posun nezpůsobuje overflow šířky elementu.
  - `topY` posunut z `startY + 0.2 * rowH` (kousek pod horní hranou) na `startY + 0.5 * rowH` (uprostřed písmen aktuální zastávky).
  - Drobný padding `ArrowInsetFromEdgeMm = 0.6mm` mezi koncem bracketu a textem pro vizuální vzdušnost.
  - `EnumPropertyDescriptor` „Pozice šipky" v group „Směr jízdy".
- **Architektonický learning:** první návrh přidával gutter MIMO sloupec → způsobilo overflow Width elementu (clip ořezal arrow). Druhá iterace dala arrow UVNITŘ column → na Vlevo to kolidovalo s left-aligned textem. Třetí (final) iterace asymetricky - Vpravo inside, Vlevo outside - funguje bez overflow i bez kolize s textem. **Per-customer divergence řešena přes property, ne template variant** (per `feature-design-principles.md` §5 - 2 bounded hodnoty, ne kombinace settings).

### A3. CurrentStopBackgroundColor + cleanup master toggle
- **Tiket:** #10660 (DPKV, Jan Slavík); **uzavřen 31. 7. 2026**.
- **Datum:** 2026-05-12
- **Výsledek:** ✅ `CurrentStopBackgroundColor` property + render path + properties panel descriptor („Pozadí aktuální") v group „Zvýraznění" - vše už existovalo. Customer use case (podbarvení místo podtržení) je plně podporovaný - uživatel vypne `CurrentStopUnderline` a nastaví barvu `CurrentStopBackgroundColor`.
- **Bonus cleanup:** Odstraněn master toggle `HighlightCurrentStop` (matoucí UX - vypadal jako sourozenec sub-options, ale choval se jako rodič). Highlight se teď aktivuje implicitně podle toho, jestli je některá z highlight properties nastavená. Šipka směru jízdy teď navíc vždy ukazuje na aktuální zastávku (dřív závisela na master toggle).
- **Minor regression:** Staré šablony s explicitně vypnutým `HighlightCurrentStop=false` po načtení dostanou defaultní Bold+Underline (protože System.Text.Json ignoruje smazaný field). Per principles („no backward compat shims") neřeším migrací.

### A4. Per-column header font ve StopList - již plně podporováno
- **Tiket:** #10660 (DPKV, Jan Slavík); **uzavřen 31. 7. 2026**.
- **Datum ověření:** 2026-05-12
- **Výsledek:** ✅ Funkčně pokryto. Klient nenašel UI cestu - funkce existuje, jen je v `StopColumnsEditorDialog` (tab „Sloupce") přes per-column `CellStyleEditor`, ne v properties panelu.
- **Code path:** `StopListElement.ResolveHeaderStyle(col)` aplikuje `col.Style.Header.Resolve(elementResolved)` → per-column přebíjí element-level v 4-vrstvé kaskádě. `RenderHeader` vytváří `ColumnRenderer.CellSpec` s **per-column ResolvedTextStyle**, takže každý sloupec má vlastní FontSize.
- **Tri-state placeholder** ✅ - sloupec bez override zobrazí šedý italic placeholder hodnoty zděděné z elementu; „×" reset jen při explicit override.
- **UI test ověřeno:** dialog sloupců → per-column FontSize → různé velikosti záhlaví v náhledu.

### B2. Underline a Strikethrough v záhlaví skupin dnů (DepartureGrid)
- **Tiket:** #10660 (DPKV, Jan Slavík); **uzavřen 31. 7. 2026**.
- **Datum:** 2026-05-12
- **Diagnóza:** V `DepartureGridElement.cs:626` se text headeru skupiny dnů renderuje přes `Canvas.DrawText` napřímo, **bez** volání `DrawTextDecorations`. Důsledek: `HeaderStyle.Underline = true` ani `Strikethrough = true` v UI nic neměnily - decorations se nevykreslovaly vůbec. Bold/Italic fungovaly, protože jdou přes SKFont.
- **Customer complaint vs. reality:** DPKV v legacy systému viděl podtržení s nestejnou tloušťkou. V novém TD by viděli žádné podtržení. Fix pokrývá oba symptomy.
- **Fix:** 1 řádek za `DrawText` - volání `hs.DrawTextDecorations(ctx.Canvas, pt.X, pt.Y, textW * ctx.MmToPx, hFont, hTextPaint)`. Tloušťka linky je automaticky konstantní napříč řádky, protože `hFont` je sdílený a `metrics.UnderlineThickness` je per-font deterministická.
- **Otestováno:** Manuálně v běžícím TD - Underline + Strikethrough nyní fungují na single-line i multi-line headeru.
- **Pozn.:** Stejný pattern (`DrawText` bez `DrawTextDecorations`) je na 5 dalších místech v souboru (minuty 780, anotace 803, hour header 899/901, designer placeholder 886/888). **Záměrně neopraveno** - nedotčeno reálným klientovým use casem; opravit až přijde konkrétní request.

### B3. Rozsah hodin přes půlnoc + sjednocené parametrové UI (DepartureGrid)
- **Tikety:** #10660 (DPKV, Jan Slavík; **uzavřen 31. 7. 2026**) + **#9036** (TRANSDEV, Vladimír Šimko - MHD Trenčín, nočné spoje 22-5h; resolved 28. 7.).
- **Datum:** 2026-05-12
- **Tiket #9036 - explicitní pokrytí:** TRANSDEV potřeboval pro MHD Trenčín (provoz cca 22-5h) časovou osu „cez polnoc" (22, 23, 0, 1, 2, 3, 4, 5). Naše implementace **přesně toto** umí - `TimeAxisMode = Range`, `HODINY_OD = 22`, `HODINY_DO = 5` → wrap přes půlnoc s extended hours (data hodina 24-29 = pokračování). Spoje se přiřazují do sloupců podle `TypProvozuID` (skupiny dnů) beze změny - což TRANSDEV výslovně přijal („v noci z piatka na sobotu spoje pred polnocou budú obsiahnuté v stĺpci pracovný deň a spoje po polnoci v stĺpci sobota, nedeľa").
- **Diagnóza:** Stávající `RangeHoursWithWrap` clampovalo `to ≤ 23` a vůbec neumělo wrap přes půlnoc. Caché používá **extended hours konvenci** (data hour 24..47 = pokračování přes půlnoc následujícího provozního dne) - z fáze 4 `PromitnutSpoje` přicházejí kumulativní hodiny po chronometráži (např. spoj startuje 23:30, jede 90 min → poslední zastávka má `hh=25, mm=00`). Nová .NET pipeline ale očekávala 0..23 → noční odjezdy „mizely" v rozsahu 4→3.
- **Implementace:**
  - `TimeAxisMode` rozšířen o `DataBounded` (souvislá od první do poslední data hodiny - předtím chybělo). Plný den vědomě odebrán z UI (lze docílit přes Vlastní rozsah 0..23).
  - `RangeHoursExtended(from, to)` (přejmenováno z `RangeHoursWithWrap`): když `to < from`, vrací **extended hours** `[from..23, 24..to+24]`. Tj. 22→3 = `[22, 23, 24, 25, 26, 27]`.
  - `DrawHourDataCell` rendruje header label jako `H % 24` (data hour 27 → „3"). ByHour lookup zůstává raw - Caché dataset s extended hours se napáruje 1:1.
  - Non-Range filter (Compact/DataBounded/Full + HODINY_OD/DO): když to < from, používá `[from..to+24]` místo `OR` filteru → data hour 27 projde filtrem 22→3.
  - `ParseTimeAxisMode` - 3-vrstvý backward compat (popisné CZ + legacy CZ + EN názvy z předchozích iterací).
  - Caché `AddStandardJRParameters`: české popisné labely pro CASOVA_OSA („Jen hodiny s odjezdy" / „Souvislá od první do poslední" / „Vlastní rozsah"); HODINY_OD/DO mají nově `visibleWhen = "CASOVA_OSA=Vlastní rozsah"`.
- **Nová infrastruktura `VisibleWhen`:**
  - `PropertyDescriptor.VisibleWhen: Func<bool>?` (in-process, lazy) - `PropertiesPanel` filtruje skryté řádky, po commitu rebuilduje panel pokud existuje aspoň jeden VisibleWhen.
  - `TemplateParameter.VisibleWhen: string?` (JSON-friendly „NAME=VAL") - `ParameterControlVM.IsVisible` + `UpdateVisibility()`, parser v `CacheAgendaProvider`.
- **Sjednocení parametrového UI:**
  - Nové `Controls/ParameterTemplates.xaml` (shared ResourceDictionary se 4 DataTemplates) + `ParameterTemplateSelector` ve vlastním souboru.
  - `ParameterControlVM.Init(target, parameters, values, onChanged, commitFactory)` - sjednocuje wiring (clear → loop → auto-refresh visibility → initial check) mezi MainWindow preview lištou a PrintDialog.
- **Klientova „auto-fit zvětšení řádků" část:**
  - Nová property `AutoExpandToFillHeight: bool` + Caché parametr `ROZTAHNOUT_NA_VYSKU` (default `false`, opt-in).
  - Když `Height` fixní + obsah se vejde + flag = true → `dataGrow = (Height − headerH) / dataNaturalH` aplikuje se proporcionálně na: data řádky, minute fonty, anotace symbol/text fonty, padding minutových buněk, `scaledMinuteW`/`scaledOdjezdGap`, hour data buňka. Header (skupiny dnů + „Hod") zůstává na původním scale.
  - Render kód rozdělen na 2 scale factory: `scale` (header) a `dataScale = scale × dataGrow` (data) - symetrický k existujícímu shrink, ale selektivně.
  - Edge cases: `Height==null` → ignorováno; shrink + AutoExpand současně → shrink wins.
  - Override priority (jako u CASOVA_OSA): designer property → PrintDialog parametr override per print job.
- **Architektonický learning:** první iterace (Option A: `hodina # 24` v Caché) by ztratila informaci „toto je následující den" → konflikt pokud spoj jede ve 3:00 ráno **tento den i následující den**. Final fix (Option B) zachovává raw service hour v ByHour klíči, mod-24 jen na header label → distinct columns pro distinct service hours, žádné collisions.

### C1. Barevné odlišení značky vs. text poznámky (DB-driven)
- **Tiket:** #10660 (DPKV, Jan Slavík); **uzavřen 31. 7. 2026**.
- **Datum:** 2026-05-12
- **Citace:** *„Kdyby bylo možné barevně odlišit značku (písmeno) poznámky, zpřehlednilo by to celý zastávkový jízdní řád."*
- **Architektonické rozhodnutí - DB-driven místo designer SymbolStyle:** Původní plán byl přidat designer-level `SymbolStyle` property (statická barva v šabloně). Při průzkumu DB se ale ukázalo, že DPKV **už má barvy uložené** per-poznámka v `^[dataDB]ALVA.MHD.ENUMS(IdFa,"PoznamkaSpoj","DTA",ID,"TiskJRMHDVozoveBarvaZnakuRGB")` (a `TiskJRMHDVozoveBarvaPozadiRGB`) - historicky pro vozové JŘ. Přeneseme stejnou barvu i do zastávkového JŘ - žádné zdvojené nastavování per-šablona.
- **Implementace:**
  - **Caché:** `IntToHex2` + `RgbToHex` helpery (převod „R;G;B" → „#FFRRGGBB" pro SkiaSharp). `BuildPoznamka` rozšířen o `foreColor`/`backColor` parametry → JSON `FORE_COLOR`/`BACK_COLOR`. `AddAnotace` rozšířen analogicky (pro konzistenci legenda ↔ anotace v odjezdech). Color lookup z `PoznamkaSpoj` ENUMS na 4 místech (poznámky trasy, speciální znaky spoje, textové poznámky spoje - vždy stejný pattern).
  - **.NET:** `PoznamkaData` + `AnotaceData` rozšířeny o `ForeColor`/`BackColor` properties (nullable). Parser v `BuildPoznamky` + `BuildOdjezdyBySkupina` čte `FORE_COLOR`/`BACK_COLOR` z JSON. Render v `NotesLegend.RenderSymbolWithText` aplikuje barvu na znak (tělo zůstává default). Render v `DepartureGrid` aplikuje barvu na anotaci nad minutou (konzistence s legendou).
  - **NZ vyloučeno** - `NegativniZnacka` má vlastní invertovaný styl (`NegativniZnackaBackColor`/`ForeColor` v elementu), color override z DB se neaplikuje.
  - **BackColor přenášeno přes wire, ale render zatím ignoruje** - klient explicitně chce zatím jen barvu znaku. Připraveno pro budoucí rozšíření bez schema break.
- **Stav v aktuálních DPKV datech (KV_DP):**
  - 1071 záznamů `PoznamkaSpoj`, z toho 69 má barvy (1003-1071) ale `TiskJRMHDZastavkove=0` (jen vozové). Záznamy 1001-1002 mají `TiskJRMHDZastavkove=1` ale žádnou barvu.
  - **Žádný současný záznam nemá současně zapnutý zastávkový tisk AND barvu** - infrastruktura připravená, klient si nastaví v DB.
  - Quick test pro ověření: dočasně `s ^ALVA.MHD.ENUMS("KV_DP","PoznamkaSpoj","DTA",1002,"TiskJRMHDVozoveBarvaZnakuRGB") = "255;0;0"`, najít linku se spojem používajícím 1002, render → znak „W" červený v legendě i nad minutou.
- **Co bylo doprovodně opraveno:** `<SUBSCRIPT>` pád v iteraci `^||TiskMHD("trasa",SmerID,...)` při linkách se sloučenými zastávkami (např. linka 1073). Příčina: `SloucitTotozneZastavky` zapisovala pomocný klíč `"slouceniInv"`, ale 7 iterací jen skipovalo `"slouceni"`. Fix: sjednoceno na `i 'BodID continue` (numeric coercion filtruje všechny ne-numerické subscripty, future-proof).
- **Auto-generované poznámky** (rozdíl tras 1001/1002 - „končí v zast. X", týdenní platnost) zůstávají default černé. Klient explicitně nepožadoval; pokud někdy přijde request, bude to drobné rozšíření (per-linka color v `LSO` nebo per-algorithm static).

### C2. Hlavička sekce poznámek (NotesLegend) - již plně podporováno
- **Tiket:** #10660 (DPKV, Jan Slavík); **uzavřen 31. 7. 2026**.
- **Datum ověření:** 2026-05-12
- **Výsledek:** ✅ Funkčně pokryto. `NotesLegendElement` má `HeaderText` property (default „Poznámky") + volá `RenderSectionHeader` (background + text + linka pod) - stejný pattern jako StopList. UI exponuje text v properties panelu (group „Záhlaví"), barvy pozadí + textu přes `GetCellZoneBaseColorDescriptors` (skupina „Záhlaví"). Klient si může nastavit libovolný text + barvy v designeru.
- **Diagnóza klientovy zmatky:** DPKV šablona `kv.json` má `HeaderText = "na znamení"` (omylem nastaveno z dřívější iterace, kdy NotesLegend obsahoval jen tu poznámku). Po opravě na „Poznámky" header funguje jak DPKV chtěl.
- **Code path:** `NotesLegendElement.Render:66-67` → `RenderSectionHeader` v `ScheduleElementBase`. Property descriptor v `GetPropertyDescriptors` (Label = „Nadpis", Group = „Záhlaví").

### E1. Podmíněný tisk sekcí - VisibilityExpression
- **Tiket:** #8772 (TRANSDEV, Jakub Holík); zůstává **suspended** (interní rozhodnutí, ne uzavřen klientovi).
- **Datum ověření:** 2026-05-11
- **Výsledek verifikace:** ✅ Funkčně pokryto. `VisibilityExpression` na sekci i elementu se vyhodnocuje na 4 místech v `PageLayoutEngine` (elementy, sekce, page headers, page footers). `ExpressionEvaluator` podporuje `==`, `!=`, `>`, `<`, `>=`, `<=`, `&&`, `||`, `!`, numerické i string porovnání s auto-detekcí, závorky, literály (`"..."`, čísla), placeholdery `{POLE}` plus `{PAGE}`, `{TOTAL_PAGES}`. Live validace v UI přes `Validate()` v PropertyDescriptoru, AST cache, fail-open runtime.
- **Pokrytí klientova seznamu polí** ze schématu `ZastavkoveJrMhdAPI.GetSchema`:
  - ✅ `CISLO_LINKY`, `TYP_JR`
  - ⚠️ „typ linky" - částečně přes `LOKALNI_OZNACENI_LINKY`
  - ❌ `KOD_DOPRAVY` (CIS) chybí, ale v praxi nahraditelné `POPIS_DOPRAVCE`
- **Interní status note** (sepsáno 2026-05-11): kompaktní popis featury, syntaxe a možných rozšíření. Tiket zůstává suspended - na rozšíření čekáme až bude konkrétní potřeba od klienta.
- **Možná rozšíření** (až bude konkrétní request, ne preventivně): doplnění chybějících placeholderů, `IN` operátor, string funkce (`CONTAINS`, `STARTSWITH`), pattern matching, aritmetika, date typování v `Compare()`, `IS NULL` test.

---

## TR. TRANSDEV - konsolidovaná sada požadavků (2026-07-03, k analýze)

Souhrnný wishlist k zastávkovému CP (SK legislativa + praxe MHD Trenčín). U každého bodu stav na
naší straně. **TRANSDEV jede AD agendu** (§A6) - i jejich MHD Trenčín, takže „AD-only" je pro ně OK.

**Stav sady k 2026-08-11: 7 z 10 hotových** (TR1 osa přes půlnoc, TR2 souhrn na-znamení, TR3
nástupiště, TR4 auto výška řádku, TR6 barva linky, **TR8 „<1"** hotové 10. 8. jako `B64`,
**TR10 skrývání spojů** vyřešené obecným definováním sloupců v `B39` - ale to jde do verze
**zhasnuté**, dokud neprojde testováním). TR5 v minimální variantě (zbytek kolegové), TR9 kolegové.

⚠️ **TR7 je jediný, který u nás zůstává otevřený** - a zákazník ho 10. 8. potvrdil znovu
(#12843 bod 4). Hotová je jen půlka (nestabilní číslování), chybí výběr variant.

**Kdo drží zbytek** (poctivě prověřeno v kódu 6. 8. - dvě věci se posunuly proti prvnímu odhadu).
Tabulka je podklad pro #13098:

| Bod | Kdo to drží | Proč |
|---|---|---|
| **TR5** cíl | **kolegové** | pole „cíl" v trase linky per zastávka × směr (jako nástupiště/sloupek) neexistuje; my umíme jen konečnou směru (`POPIS_SMER`). Ideál = číselník cílů + odkaz id |
| **TR9** poznámkování | **kolegové** | rozsah TČ je uložený **per spoj** (`^JRL(...,"PATCH","MHDSPNPZN",spojId)` = `od~do`), u značky žádný rozsah není. Chtějí ho vedle značky = nové pole + přepsané zadávání. U nás **není co číst** a tisková strana se nemění |
| **TR8** „<1" | **kolegové + klient** | co je legacy sloupec „Průběhy" (je per-segment?) + screenshot, kde tu „0" vidí. Naše časy jsou kumulativní, „0" u nás nevzniká |
| **B12** / #7937 | **kolegové** | odsazení per směr - `^JRL` dta7 je jedna hodnota na bod trasy (i legacy), sdílená pro tam i zpět; per-směr chce nové pole + editor |
| **TR10** skrývání | **pohlceno `B39`** | rozhodnutí Jan 7. 8. - nedělat zvlášť, vyřeší to obecné definování sloupců. ⬇️ podmínka: sloupec musí umět i vyloučení |
| **TR7** sloupce jízdních dob | **nikdo, jen ověřit** | ⬇️ viz níže - v návrháři ten problém neexistuje |

**`TR7` - stížnost se návrháře netýká** (ověřeno 6. 8. v kódu). Klient si stěžuje, že „v editore
šablóny je náročné tipovať, ktorý stĺpec zapnúť, lebo číslovanie odráža poradie výskytu variantu".
V TD se **žádný sloupec nezapíná**: `StopListElement` si sloupec jízdních dob sám rozvine na N
variantních podsloupců (`ExpandTravelTimeColumns`, výchozí „Všechny varianty tras" = zapnuto)
a záhlaví nejsou pozicová čísla, ale **písmena variant** (`PISMENA_VARIANT`) nebo písmena rozdílů
tras s rozlišením kolizí (`B1`/`B2`/`T1`). Nestabilní pořadí napříč stránkami řeší
`RAZENI_VARIANT = Stabilní (pořadí spojů)` od 27. 7. **Není co implementovat - poslat klientovi
k vyzkoušení.** Hlubší přání („určit, podle kterých spojů se doby generují") vzniklo jako obklad
tohoto problému; až po vyzkoušení má smysl se ptát, jestli ještě platí.

**`TR10` - u nás to nejde, a první návrh byl chybný** (opraveno 7. 8.). Potlačení v kódu existuje
(`ZarazeniSpoje` čte per-linkový seznam „značky nevypisovat", `MHD_NENZ1_NENZ2`), ale míří na sloupce
**„Pracovní dny mimo prázdniny"** a **„Pracovní dny o prázdninách"** - tedy dva **pracovní** sloupce.
Víkendový je až třetí (`NacistSkupinyDnu`) a nastaví se na 1 bez jakékoli kontroly.

- ❌ **Zavržený návrh (blok `B62`, zrušen):** „přepínač sestavy použije týž seznam i na víkend."
  Rozbilo by to funkci, ne dodalo: značka v tom seznamu už dnes spoj odebírá z pracovních sloupců,
  takže rozšířením na víkend by spoj zmizel **ze všech** a nevytiskl se vůbec.
- ✅ **ROZHODNUTO 7. 8. (Jan): pohlceno `B39`** - nedělat zvlášť. Obecné definování sloupců (podle
  značek / rozsahu dnů) tenhle případ pokryje a dnešní pevné schéma se stane presetem.
  ⚠️ **Podmínka, bez které pohlcené není:** definice sloupce musí nést i **vyloučení**, ne jen
  zahrnutí. Kolega to formuloval jako „vypisovat spoje, které ty značky mají obsažené"; TRANSDEV
  ale potřebuje opak - spoj podle dat jede a on ho ve víkendovém sloupci nechce. S pouhým zahrnutím
  by musel označkovat všechny spoje, které vidět **chce**, tedy víc práce, ne méně. Platí to i pro
  rozsah dnů: spoj, který v sobotu jede, se do sloupce spočítá a musí ho přebít vyloučení.
  Dnešní per-linkový seznam „nevypisovat spoje se značkou" je přesně ten vylučovací seznam.
- 📦 **Odložený fallback** (kdyby TRANSDEV tlačil dřív, než bude `B39` navržená): dorovnat seznam
  „nevypisovat spoje se značkou" ze **2 slotů na 4 per sloupec** - tvar, který vedle už existuje
  u `MHD_NEZNZ123`. Kolegové přidají pole, my přečteme sloty 3/4. Pár řádků na obou stranách.
- ⚠️ **Neplést s `B63`.** Nastavení „nevypisovat **značku** ve sloupci" spoj **neskryje** - minuta se
  vytiskne, zmizí jen značka u ní. Ve starém modulu je to rozdělené **záměrně po chybě**: zakomentovaný
  blok (`JRapiLinPrtMhd1.mac` ř. 246-251) ukazuje, že se tím spoj dřív přeskakoval celý a „NETISKLY SE
  ČASY", takže čas se dnes ukládá vždy a vynechá se jen značka. `B63` tedy `TR10` **nevyřeší**.
- ⚠️ **Číslo negativní značky:** rozsah čísel se přiděluje **firemně** (`^JRSET("Cis","InPlatODDO")`,
  symboly `InPlatOzn`), ale význam plyne z omezení jednotlivých spojů - takže není jisté, že „značka 3"
  znamená na dvou linkách totéž. **Otázka na kolegu**, ne naše tvrzení; rozhoduje, jestli seznam značek
  smí žít v šabloně, nebo musí zůstat u linky. (Rozsahy dnů jsou na lince nezávislé, ty do šablony patří.)
  ⚠️ Dřívější tvrzení „je per linka" bylo nepodložené.
- 🔎 **Nález 7. 8.:** `MHD_NEZNZ123` (4 sloty per sloupec, „nevypisovat značku") **vůbec nečteme** →
  blok `B63`. Na naší vývěsce se tisknou značky, které si zákazník u linky vypnul. Sousední řádky téže
  rutiny, ze které jsme portovali `ZarazeniSpoje` (ř. 37-40 vzato, 41-45 ne). Souvisí s #12964.

1. **Časová os cez polnoc** (noční linky 22→5h) nebo aspoň manuální přeuspořádání hodin.
   → ✅ **Už hotové (B3, #9036)**: `TimeAxisMode=Range`, `HODINY_OD=22`/`HODINY_DO=5` → wrap přes půlnoc
   (extended hours). **Klient nejspíš netestoval na své verzi** - ověřit u něj, netřeba implementovat.
2. **Zastávky na znamení - „všechny"**: když jsou VŠECHNY zastávky na znamení, místo značek u každé
   vygenerovat souhrn „× všechny zastávky linky jsou na znamení" (analogie k bezbariér souhrnu). Konfigurovatelné
   (CZ legislativa může chtít značky). 🟡 **IMPLEMENTOVÁNO 2026-07-10 (AD, opt-in), čeká SK ověření:**
   - Opt-in param `SOUHRN_NA_ZNAMENI` (default off = per-zastávka × jako dnes). Když všechny obsluhované
     neprojeté zastávky mají „x" → potlačí per-zastávka × + agregátní řádek legendy. Mechanika 1:1 jako
     bezbariér `allBBP`. Čistě Caché (AD-only; MHD-port později, bezbariér agregát je taky jen AD). Gold-safe (33/33).
   - ✅ **Kódování potvrzeno „x"** (kolega 2026-07-10 + naše CB_CSADAD data) → detekce je **správná**,
     feature na reálných SK datech bude fungovat. `SKNI_MHDNITRA` má ale **0× „x" ve 27985 záznamech**
     (prohledána celá J-věta, ne jen dta2) = **stará/nekompletní data** (žádné na-znamení nastavené), NE jiné kódování.
   - ⏳ **Zbývá jen ukázka spuštění agregátu:** potřebuje linku, kde jsou VŠECHNY zastávky „x" (vzácné -
     ani CB_CSADAD nemá all-„x" linku). Stejná datová řídkost jako TR3; ověří se na reálné all-request-stop
     lince u zákazníka. Feature je jinak korektní a opt-in gold-safe.
3. **Zobrazení nástupiště**: nový sloupec vedle názvu zastávky (SK legislativa). ✅ **HOTOVO 2026-07-10:**
   per-zastávka `NASTUPISTE` v kolekci ZASTAVKY - **AD** z J-věty `#3` (`nastTam:nastZpet` dle směru),
   **MHD** z `Oznacnik` (`ALVA.MHD.ZST`). Nový `StopFields.Nastupiste` + StopList sloupec „Nástupiště"
   (default skrytý = opt-in). Bez mocku - kde označník není, prázdné (standardně jen terminály/uzly).
   Ověřeno reálně na CB_CSADAD **0320680** (nástupiště na 6 zastávkách). Gold re-baseline AD 2 + MHD 3.
4. **Výška řádku „zastávka" - auto (3. režim)**: pevná výška, při nedostatku místa se začne zmenšovat.
   ✅ **HOTOVO 2026-07-10:** StopList pevná `DataRowHeight` se nově zmenší při přetečení pevné Height
   (dřív zůstala → přeteklo/ořízlo). Default (auto, `DataRowHeight` prázdné) beze změny. Realizováno
   sdíleným helperem `ScheduleElementBase.ScaledDataRowHeight(fontPt, factor)` (fixed-then-shrink pravidlo
   v jednom místě; sdílí StopList + DepartureGrid - dřív se rozešly, B2 vs StopList). Rendering-only, .NET 316/316.
   Plná unifikace řádkového layoutu (row-plan) = krok C.
5. **Definice směru/cíle** - položka „cíl" odkazovatelná v šabloně. 3 varianty: (min) univerzální cíl per směr;
   (střední) cíl textem per zastávka × směr v trase (jako nástupiště/sloupík); (ideál) DB cílů + id per zastávka×směr.
   Dnes hackují přes `informace`/`titulek`. NEW, datový model, M–L. *(Odbourá i speciální cíle pro tabule.)*
6. **Barva linky**: ve vlastnostech linky bg + text barva; v šabloně u objektu volba „farba linky" místo pevné
   z palety → aplikuje se při tisku. Šetří hodiny u grafických změn (MHD Trenčín).
   ✅ **HOTOVO 2026-08-03 (#12997):** JŘ modul umí od 3. 8. **víc barev na linku** (Petr Šmíd,
   `^JRL(sku,lin,"PATCH","BARVA")`); modul jim význam nedává, klient první používá jako text a druhou
   jako pozadí - přesně jak si to představoval. Agenda je vydává jako `BARVA_LINKY` / `BARVA_LINKY_2`
   (`ZastavkoveJrAd.BarvaLinky`) a **jdou napsat do každého barevného pole šablony** místo pevné barvy,
   takže jedna šablona obslouží všechny linky. Nápovědu k tomu nese barevné pole samo.
   - **Záměrně bez mezikroku v paletě** (volba „barva linky" jako položka color pickeru) - přidal by klik
     všem uživatelům kvůli menšině případů; místo toho se placeholder píše do pole (`ColorPickerHelper.cs:35`).
   - **Jen AD agenda.** `ZastavkoveJrMhd` pole nemá - relevantní pro DPCB/DPKV, ne pro TRANSDEV.
   - **Zbývá ověřit u klienta** (nic neblokuje): že mu to v jeho verzi opravdu ušetří to ruční přepisování.
7. **Jízdní doby - výběr sloupců variant**: víc variant trasy → víc sloupců jízdních dob; číslování dnes odráží
   pořadí výskytu variantu v seznamu spojů (nestabilní - Sloupec 3 na začátku = Sloupec 2 uprostřed). Chce
   explicitní kontrolu (které spoje / úsek řídí generování). → **Relates L1 multi-varianta TrasaMin** (`VARIANTY_TRAS`
   + `ShowAllRouteVariants`); je to ta „jemnější výběr variant (per-písmeno)" z `ad-data-mapa.md §10`. M.
8. **Jízdní doby - „<1" místo „0"**: nulová jízdní doba mezi zastávkami → zobrazit „<1" (méně než minuta).
   Konfigurovatelné. 🟢 **PREMISA VYJASNĚNÁ 10. 8. (Jan) - je to naše práce, blok `B64` (S):**
   - Zákazník musí mít na mysli **první úsek** (jízdu z vývěskové zastávky na následující). Doba jízdy
     je **kumulativní od vývěskové zastávky** (`CasJizdyZMapy(fromPoradi = vývěsková, poradi)`), takže
     její vlastní řádek je 0 **a následující zastávka je taky 0**, jede-li se tam pod minutu. Nikde jinde
     v tom sloupci nula pro jízdu nevznikne - dál jsou to kumulativní součty, které se při nulovém úseku
     jen zopakují (ověřeno na `CB_CSADAD` **skupina 10**, linka 0380520, trasa 2: zastávky 2→3 ve stejné minutě). Tím padá dřívější hypotéza o legacy
     sloupci „Průběhy" i potřeba per-segment sloupce; **kolegy k tomu nepotřebujeme**.
   - **Pravidlo:** řádek **jiný než vývěskové zastávky** s nulovou kumulativní dobou → „<1". Vlastní
     řádek vývěskové zastávky zůstává 0 (stojím tam, nulová jízda je definice, ne informace).
   - **Parametr sestavy, default vypnuto** - zákazník sám psal „konfigurovatelné" a je to praxe části
     dopravců v SR; ČR to takhle nemusí chtít.
   - ⚠️ **Past:** `CasJizdyZMapy` vrací 0 i když zastávka **není na trase** nebo ji spoj projíždí (`<`) -
     to je „nemám data", ne nulová jízda. Bez rozlišení by „<1" vyskočilo tam, kde má být prázdno.
     Rozlišit u zdroje (chybějící údaj → prázdno, ne 0).

9. **Poznámkování spojů - obrácená logika**: dnes horní tabulka (značka/text/spoje) + spodní tabulka (kde
   NEpoznámkovat). Chce: rozsah TČ přímo v první tabulce vedle značky/spojů. Legacy-input workflow (tab „MHD").
   Zjednodušení, méně chyb. M (Caché-input strana).
10. **Zobrazování spojů - skrýt v So/Ne/svátky**: dnes „spoje s NZ se nezobrazí" jen pro sloupce školní
    výuka/prázdniny; chtějí i skrýt spoje o So/Ne/svátcích. → rozšíření filtru (`MHD - další`). NEW, S–M (Caché).

## H. Status komunikace s klienty

M-line zákazníkům slíbil:

- **„Nový tiskový modul" / „Studio JŘ"** - release **polovina 2026**.
- Citace z `2024-10-04` na #7937, #7947, #7955, #7959 (TRANSDEV): *„S ohledem na množství podnětů týkajících se specifických možností nastavení a chování tisku jízdních řádů pracujeme na vývoji nového nástroje v našem systému (...) nový nástroj bude uvolněn přibližně v polovině příštího roku."*
- DPKV (#10660) z `2025-07-10` (Dita Chabičovská): *„momentálně se musíme soustředit především na obsahové aktualizace - jak zastávkových, tak i knižních jízdních řádů. Současně ještě nemáme uzavřenou část projektu týkající se plánování směn (...) vrátíme se k němu po dokončení klíčových etap implementace MHD ve Vašem dopravním podniku."*
- KORID (#11300) z `2025-12-08` (Jiří Bžoch): *„úpravu již »bohužel« nestihneme udělat v letošním roce. Problému se ale budeme věnovat příští rok, jeho řešení pravděpodobně spojíme s novým tiskovým modulem pro JŘ."*

**Z toho plyne:** roadmapa TemplateDesigneru je **viditelný závazek** vůči TRANSDEV, KORID a DPKV. Při uzavírání jednotlivých tiketů odkazovat na konkrétní featuru v TD + verzi releaseu.
