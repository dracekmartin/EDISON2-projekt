# Živá ukázka — podrobná režie (zastávkový JŘ)

> **Jeden scroll shora dolů = celý seminář.** Formát: **AKCE** (co udělat) · **ŘÍKÁŠ** (mluvený bod) ·
> **▸ Lze tu ukázat/zmínit** (kompletní výčet — vyber co chceš, zbytek přeskoč) · **POZOR**.
>
> **Stav featur:** počítá s tím, že **vykreslování negativních značek** a **FTL nastavení sloupců
> odjezdů** (sloupce jako ve starém) **jsou hotové** — v Části 3 je ukazujeme.
>
> **Rozhodni před zkouškou (defaulty):** délka ~45–55 min + Q&A · zdroj **AD JŘ** (1:1) + krátké MHD ·
> demo linka **`0365001`** (rozdíly tras + bezbariér + časy) nebo `0320144` (FTL3, víc sloupců).

---

# ČÁST 0 — Než začneš (přečíst předem)

### Příprava (10 min předem)
- [ ] **Vrátit DEV tweaky** na produkční default (firma, startup šablona) — gap analýza P0-1.
- [ ] **Opravit / ověřit #8** false-positive přetečení — jinak u nové šablony bliká červený rámeček. Pokud neopraveno → **nezakládat naživo úplně novou prázdnou šablonu**.
- [ ] Připojení na **databázi (dev / VPN)** živé (data, šablony, tisk).
- [ ] **Pushnuto z druhého PC + pullnuto sem** — jinak NZ vykreslení a FTL sloupce běží ve staré verzi.
- [ ] Otevřené **2 šablony**: (a) hotová zastávková JŘ šablona, (b) jednoduchá na ukázku tvorby.
- [ ] Vybraná **demo linka** s: rozdíly tras (A/B), bezbariér (H/F), omezeními platnosti, NZ.
- [ ] **Zvětšené UI písmo** pro projektor · **zálohní PDF** (kdyby vypadlo připojení) · zavřené notifikace.

### ⚠️ Čeho se při živém demu vyvarovat
- **Nezakládat novou prázdnou šablonu** (#8 false-positive), dokud není opraveno.
- **Neslibovat** multi-variantu časů tras jako hotovou — je to roadmapa.
- **Nezačínat** improvizace, které neznáš naživo — drž se nacvičené sekvence.
- Výpadek připojení → **zálohní PDF**.
- Nepoužívat interní pojmy k publiku (FTL, Caché, gold oracle) → říkat „rozvržení sloupců dnů", „databáze", „ověřeno proti původnímu tisku".

### Rychlý nácvik (na sucho)
- [ ] Projít celou sekvenci 1× na demo lince, stopnout čas.
- [ ] Ověřit, že **FTL sloupce** i **NZ vykreslení** v demu fungují (vyžaduje push z druhého PC).
- [ ] Vyzkoušet PDF export + PrintDialog s reálnými daty.
- [ ] Připravit 1 linku, kde je rozdíl tras + bezbariér + omezení **najednou**.

---

# ČÁST 1 — Úvod (3–4 min, mluvit)

> Mluvená verze — odříkej nebo parafrázuj. **Body 4, 5, 5b NEVYNECHAT** (callout níže).

**1) Přivítání + co dnes uvidí (15 s)**
> „Dobrý den, jmenuju se Jan Miesbauer z M-LINE. Dnes vám ukážu **Návrhář šablon** — nový nástroj, ve kterém se vizuálně navrhují tiskové sestavy, dnes konkrétně **zastávkové jízdní řády**. Bude to z velké části **živá ukázka přímo v aplikaci**, ne slidy."

**2) Hook — co to je a co nahrazuje (30 s)**
> „Návrhář nahrazuje starý nástroj, kde se sestava skládala přes formuláře a čísla — **bez náhledu**, výsledek jste viděli až po tisku. Tady navrhujete vizuálně a **okamžitě vidíte, co se vytiskne**. Stejný engine dělá náhled i reálný tisk — co vidíte na obrazovce, to dostanete na papíře. A protože je to **nová aplikace, máme prostor přidávat nové funkce** — což u starého nástroje prakticky nešlo."

**3) Formát a co očekávat (30 s)**
> „Hodina zhruba na dvě půlky: první **obecně editor** — jak se s ním pracuje — a druhá to nejdůležitější pro vás, **zastávkové jízdní řády** a jak z dat vznikne konkrétní tisk. Na konci roadmapa, vyřešené podněty z helpdesku a prostor na dotazy."

> ### ⚠️ NEZAPOMENOUT — body 4, 5, 5b (jediné, co od publika aktivně chceme)
>
> **4) Co mají dělat VY (řekni POMALU, 45 s):**
> > „Mám na vás dvě prosby, ať z toho vytěžíme maximum:
> > - **Ptejte se hned** během ukázky na *„co a jak umíme nastavit"* — funkční dotazy berem rovnou.
> > - Když narazíte na něco **zásadního** — co vám chybí, co je blokující — **zapište si to a pošlete mi mailem**. Ať to nezdržuje hovor a my to vezmeme strukturovaně.
> > - A během ukázky **si všímejte rozdílů** oproti tomu, jak to děláte teď ve starém nástroji."
>
> **5) Žádost o jejich nastavení (30 s):**
> > „Budu rád, když mi po semináři **sepíšete, co z nastavení ve starém AD reálně používáte** a co je pro vás klíčové. Máme funkční úvodní verzi a technické detaily doplníme přesně podle vašich potřeb."
>
> **5b) ⭐ „Nedávejte požadavky do zásoby" (30 s):**
> > „A jedna prosba k feedbacku: **nedávejte nám prosím požadavky „do zásoby"** — nastavení, které by se *teoreticky* někdy mohlo hodit. Záměrně to stavíme **odlehčeně** a přidáváme jen to, co reálně potřebujete. Starý nástroj měl tolik voleb, že se v něm nakonec sami těžko vyznáte. Chceme, aby nástroj **zůstal srozumitelný** — řekněte nám, co **opravdu používáte**, ne co by *šlo* mít. Začneme s podstatným a budeme přidávat podle skutečné potřeby."
>
> *(Body 4, 5, 5b zopakuj i na konci — viz ČÁST 4.)*

**6) Očekávání poctivě (20 s)**
> „Důležité: tohle je **úvodní verze**. Zastávkový JŘ je **datově ověřený 1:1** proti původnímu výstupu na konkrétních linkách. Méně časté konfigurace ještě dolaďujeme — proto váš feedback vítáme."

**7) Přechod:** „Tak pojďme rovnou do aplikace."

**POZOR / tipy:** nezačínej omluvou (poctivost o úvodní verzi až bod 6) · klíčová věta na zopakování: *„Stejná data, stejný výsledný tisk — ale návrh je teď vizuální, okamžitý a ve vašich rukou."* · ~3–4 min, pak hned do aplikace.

---

# ČÁST 2 — Designer obecně (~20 min)

> Cíl: návrh je **vizuální, okamžitý, vratný**. Tempo svižné. Drag&drop/snap/undo si okomentuj sám.
> Pod každou scénou je **▸ výčet** co všechno tam jde ukázat — vyber, zbytek přeskoč.

**2a) Orientace** — toolbar · panely (Sekce / Strom / Pole agendy / Vlastnosti) · canvas · náhled · status bar (štítek agendy + linky).
▸ strom šablony · panel „Pole agendy" · UI font škálování · tooltipy.

**2b) Editace + prvky** — drag z palety · drag pole agendy → `{POLE}` · snap 4 mm · Ctrl+D+šipka · Ctrl+G (Alt+klik = jeden) · Ctrl+Z/Y.
▸ Prvky: text · obrázek · obdélník · čára · **kód** (QR/Code128/EAN-13/EAN-8/PDF417/DataMatrix) · tabulka · prvky JŘ.
▸ Navíc: rubber-band · Ctrl+A · rozdělení Ctrl+Shift+H/J · zarovnání/rozmístění · z-order · kopírování stylu Ctrl+Shift+C/V · context menu.
**ŘÍKÁŠ** (jen u undo): „Každá akce vratná — ve starém nástroji omyl = projít formulář znovu."
**POZOR:** nezakládat novou prázdnou šablonu (#8).

**2c) Vlastnosti, styly, tabulky** — panel Vlastnosti (font/barvy/zarovnání, hned v náhledu) · Enter = hromadná editace výběru.
▸ Styly: tučné/kurzíva/podtržení/přeškrtnutí · H+V zarovnání · rámeček (sdílené hrany = jeden) · kaskáda (dědění + „×" reset).
▸ Tabulky: **Fixed** (`{POLE}`) vs **Dynamic** (řádky z kolekce) · editor sloupců · **agregace** SUM/COUNT/AVG/MIN/MAX.

**2d) Výrazy** — `{POLE}` · formátování `{CENA:N2}` / `{:UPPER}` / `{:LEFT(4)}` · podmíněná viditelnost (`{PAGE}>1`) · chybný výraz = červené upozornění · operátory `&&` `||` `!`.

**2e) Sekce, layout, stránka** — sekce + drag řazení · chování tisku (hlavička/patička) · multi-page náhled.
▸ Sekce: role Header/Body/Footer · vlastní šablony sekcí · skrytí záhlaví.
▸ Stránka: PageSize (A2–A6) · škálování 50–200 % · normalizace Ctrl+Shift+N · mřížka 1/2/5/10 mm.
**ŘÍKÁŠ** (nečekaný plus): „Jazyk šablony (CZ/SK/EN/DE) řídí formát čísel a dat — **nezávisle na Windows**, tisk vypadá stejně všude."

**2f) Náhled, export, šablony** — Export PDF (+ PNG, PrintDialog) · Otevřít z databáze → správce (filtr, search).
▸ živý náhled · multi-page · Fit-to-Page Ctrl+0 · varování o přetečení · **šablona = JSON** (poslat dopravci) · per-uživatel stav.
**ŘÍKÁŠ:** „Šablony centrálně podle firmy — ne roztroušené po počítačích."
**POZOR:** varování o přetečení — hlídej false-positive #8.

**2g) Import z PDF + AI** *(volitelné, výhled)* — Import z PDF (3 režimy grafiky) → prvky. AI: enrichment (auto-přiřazení polí) + Vision (struktura JŘ z obrázku).
**POZOR:** AI naživo riziko (síť/klíč) — měj zálohu, klidně přeskoč.

**Most do Části 3:** „To byl obecný editor. Teď to hlavní — jak z dat vznikne konkrétní zastávkový tisk."

---

# ČÁST 3 — Zastávkové JŘ a business logika (~23 min)

> **Jádro pro provozní uživatele.** Obě nové věci: **vykreslení NZ** a **FTL sloupce odjezdů**.

**3a) Tři prvky** — tabulka zastávek · mřížka odjezdů · legenda poznámek.
**ŘÍKÁŠ:** „Rozumí datům JŘ — vy řešíte vzhled, logiku spočítá systém."

**3b) Zdroj + linka** — Ctrl+Shift+A → agenda (AD JŘ) · výběr linek (multi-výběr) · ◀▶ přepnout instanci.
**ŘÍKÁŠ:** „Dva zdroje (linkové i MHD); linku přepnu a hned vidím jiná data."

**3c) Tabulka zastávek** — editor sloupců (pořadí/šířky/styl) · atributy u názvu · šipka směru · zvýraznění aktuální.
▸ Nastavím: pozice značek **za** (CB_DP) / **před** názvem (DPKV) · šipka vlevo/vpravo · projeté zastávky.
▸ Auto: časy jízdy, atributy, filtr „na znamení". Atributy: × na znamení · { automat · přestup.

**3d) ⭐ Mřížka odjezdů — FTL sloupce + NZ** *(NOVÉ, klíčový moment — nech prostor)*
nastavení sloupců odjezdů → **FTL** (skupiny dnů jako ve starém) · přenastav sloupce · anotace u minut (NZ ×/⊘ inverzně, H/F, A/B).
▸ Nastavím: sloupec hodin (vlevo / per skupina) · časová osa (hodiny s odjezdy / pevný rozsah / 24 h) · zebra.
**ŘÍKÁŠ:** „FTL — tohle šlo nastavit ve starém nástroji, a teď to jde taky, ale vizuálně a s živým náhledem. A negativní značky se vykreslují přesně jako v originále."

**3e) Typy poznámek + auto-logika** *(jádro „co se počítá samo")*

| Typ anotace u minuty | Příklad | Co znamená |
|---|---|---|
| Týdenní platnost | Po, So, Ne | jede jen v určité dny |
| **Negativní značka** | ×, ⊘ | spoj nestaví všude / omezená trasa (inverzně) |
| **Rozdíl trasy** | A, B, C | jiná trasa → „neobsluhuje / končí v / jede přes / jede až do zast. X" |
| Bezbariérovost | H, F | H = bezbariérové, F = částečně |
| Speciální znaky | ikony | zvláštní režim spoje |
| Textové poznámky | text | volná poznámka u spoje |
| Ruční poznámky (AD) | komentář | komentáře, směrové (tam/zpět/vše) |

▸ **Auto (uživatel nepíše):** časy jízdy · rozdíly tras + texty · NZ · bezbariér (souhrn / per spoj) · omezení platnosti („nejede 24.–31. 12.") · dopravce (popis+adresa) · skupiny dnů → sloupce · naplnění + dedup legendy.
**ŘÍKÁŠ (klíčová věta):** *„Vy řešíte vzhled. Co značka znamená, jakou trasou spoj jede, kdy nejede — spočítá systém z dat. Ověřeno 1:1 proti původnímu tisku."*

**3f) Legenda** — víc sloupců (1–4) · řazení dle délky · zarovnaný marker · filtr typů (NZ / spec / text).
**ŘÍKÁŠ:** „Naplní se sama z toho, co je na stránce."

**3g) Dávkový tisk + ⭐ rychlost** — PrintDialog · členění výstupu (vše / per linka / per zastávka×směr) · název ze polí.
**ŘÍKÁŠ:** „Jedna šablona × stovky stránek do jednoho běhu. A rychle — **~100 stran za sekundu** (render); stovky stran i s daty za pár sekund." *(reálně ~600 stran/~9 s vč. dat)*

---

# ČÁST 4 — Roadmapa a závěr (5 min, mluvit)

**Co je hotové (krátce):** zastávkový JŘ z AD dat 1:1 · šablony v databázi per firma · dávkový tisk.

**Co se chystá (upřímně, NEslibovat termín):**
- **Multi-varianta časů tras** (víc časových sloupců vedle sebe) — *jediná velká chybějící věc, na roadmapě*.
- Barevné odlišení časů dle prahu, cizojazyčné poznámky, drobnosti.

**Postup do budoucna (rámec „tohle je začátek, poroste to s vámi"):**
- **Integrace do EDISONu** — spuštění Návrháře přímo z modulů JŘ/MHD nad konkrétní linkou.
- **Postupné doplňování agend** — linkový / knižní / vozový JŘ, faktury…
- **Průběžné řešení helpdesku** — vaše podněty zapracováváme (viz ČÁST 5).

**▸ NEZAPOMEŇ na závěr zopakovat:** body **4, 5, 5b** z úvodu (ptejte se · blokující věci mailem · sepište co reálně používáte · **nedávejte do zásoby**) + **klíčovou větu** *„Stejná data, stejný tisk — ale návrh je teď vizuální, okamžitý a ve vašich rukou."*

---

# ČÁST 5 — Vyřešené helpdesk tickety (zmínit před koncem)

> **Jak podat:** **nebudu ukazovat naživo na datech** (vyžadují konkrétní linky/data zákazníka), ale
> **máme je vyřešené** a budeme je **uzavírat na helpdesku**. Cíl: dát najevo, že podněty reálně řešíme.
>
> **Návrh formulace:**
> > „Část vašich podnětů z helpdesku už máme hotovou. Nebudu je ukazovat naživo — vyžadují konkrétní
> > linky a data — ale vyřešené jsou a budeme je na helpdesku **uzavírat**. Pokud budete chtít, doladíme
> > je ještě na vašich reálných datech."

**Vyřešeno (převážně AD agenda, zákazník TRANSDEV):**

| Ticket | Zákazník | Požadavek (krátce) | Jak je vyřešeno |
|---|---|---|---|
| **#7947** | TRANSDEV (Podloučka) | matoucí „pokračuje přes zast. X" + zbytečné písmenkování | text **„jede až do zast. X"** + odstraněno zbytečné písmenkování |
| **#7955** | TRANSDEV (Podloučka) | u vybraných spojů chtěl **jen vlastní komentář** (ne automatiku zároveň) | přepínač **„co schovat v rozsahu"** (Ruční / Automatické / Obojí) + komentáře + směrové |
| **#7161 D5** | TRANSDEV (Šimko) | negenerovat „× — na znamení" do legendy, když je sloupec se značkou vypnutý | **auto-skrytí** řádku legendy dle viditelnosti značky |
| **#7161 D6** | TRANSDEV (Šimko) | vypínač pro souhrn „všechny spoje bezbariérové" | parametr **„skrýt agregát bezbariér"** |
| **#7959** | TRANSDEV (Podloučka) | zastávka, kam z aktuální nejede spoj, nemá být v seznamu (školní spoj) | **filtr neobsluhovaných** + přeskočení stránky — *(„máme připravené, doladíme na vašich datech")* |
| **#10660** | DPKV (Slavík) | podbarvení aktuální zastávky místo podtržení | **už podporováno** (vypnout podtržení + barva pozadí); ticket jinak suspended |

**⚠️ Poctivé výhrady (NEpodávat jako 100% hotové):**
- **#7955** pod-položka „výběr spojů jen jednociferně" = bug v **EDISON Studiu JŘ**, ne v Návrháři → předat zvlášť.
- **#7959** funguje, ale „ověřit na reálných TRANSDEV datech" → formulovat jako „připraveno k ověření".
- Vše vyřešeno v **AD agendě** (TRANSDEV jezdí linkově); MHD modul může mít drobné odlišnosti.

**❌ NEzmiňovat jako vyřešené:** #7937 (odsazení per směr — odloženo) · #7487 (KORID — knižní/linkové JŘ) · #11300, #8828, #8772, #9036 (nepotvrzeno).

> **Pozn. nácvik:** hotové v kódu, ale **nepushnuté** (stranded na druhém PC) → pro uzavření na HD nejdřív pushnout.

---

# ČÁST 6 — Q&A (otázky publika)

**Připravené odpovědi:**
- *„Můžeme si šablonu upravit sami?"* → Ano, to je celý smysl — vizuálně + uložení do databáze.
- *„Co se starými šablonami?"* → V1 neimportuje 1:1; nové se navrhnou znovu (rychle, WYSIWYG).
- *„Bude to vypadat stejně?"* → U AD JŘ datově ověřeno 1:1; vizuál je nový engine, obsah a rozložení sedí.
- *„Uměl to starý nástroj?"* (sloupce dnů) → Ano — a my to teď umíme taky, ukázal jsem to (FTL).
- *„Jak se to k nám dostane?"* → Integrované v EDISONu, tisk stejným enginem jako náhled.
- *„A umí to ještě…?"* → většina nastavení je v demu; co nepoužíváte, schválně nezahlcujeme (viz prosba 5b).
