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

# ČÁST 2 — Designer obecně (~22 min, klikat + mluvit)

> Cíl: návrh je **vizuální, okamžitý, vratný**. Tempo svižné — rozjezd.

### 2a) Orientace v okně (2 min)
**AKCE:** toolbar → levý panel (záložky **Sekce / Strom / Pole agendy / Vlastnosti**) → canvas → pravý náhled → status bar. Ukázat **zelený štítek agendy** + **štítek instance (linka)**.
**ŘÍKÁŠ:** „Vlevo struktura a data, uprostřed návrh, vpravo živý náhled toho, co se vytiskne."
**▸ Lze tu ukázat/zmínit:** ikonový toolbar (7 skupin) · **strom šablony** (hierarchie sekce→skupiny→prvky) · panel „Pole agendy" (databázová pole) · UI font škálování · tooltipy u ikon.

### 2b) Základní editace + nabídka prvků (6 min)
**AKCE:** 1. přetáhni z palety **text** na canvas (ghost preview) · 2. přetáhni **pole agendy** (`CISLO_LINKY`) → `{CISLO_LINKY}` · 3. **snap** (vodítka, mezera 4 mm) · 4. **Ctrl+D + šipka** (kopie vedle) · 5. **Ctrl+G** skupina (Alt+klik = jeden prvek) · 6. **Ctrl+Z/Y**.
**ŘÍKÁŠ (u undo):** „Každá akce je vratná. Ve starém nástroji omyl v parametru = projít celý formulář znovu — tady jen Ctrl+Z."
**▸ Nabídka prvků (vyber · přeskoč):** text (word-wrap, dynamická výška) · obrázek (auto-downscale) · obdélník · čára (čárkovaná/tečkovaná) · **kód** (QR, Code128, EAN-13, EAN-8, PDF417, DataMatrix) · tabulka · prvky JŘ.
**▸ Manipulace navíc:** rubber-band výběr · Ctrl+A · snap i při resize · Ctrl+D (offset) · rozdělení prvku Ctrl+Shift+H/J · zarovnání + rozmístění · z-order · cut/copy/paste · **kopírování stylu** Ctrl+Shift+C/V · context menu (pravý klik).
**POZOR:** nezakládej novou prázdnou šablonu (#8), edituj v připravené.

### 2c) Vlastnosti, styly, tabulky (4 min)
**AKCE:** vyber prvek → záložka **Vlastnosti** → změň font/velikost/tučné/barvu/zarovnání → hned v náhledu. Vyber víc prvků → **Enter** → společné vlastnosti najednou.
**ŘÍKÁŠ:** „Styl se mění okamžitě, výsledek vidíte hned."
**▸ Lze tu ukázat/zmínit:** tučné/kurzíva/podtržení/přeškrtnutí · barva textu + pozadí · zarovnání H+V · řádkování · **rámeček** (sdílené hrany = jeden rámeček) · **kaskáda stylů** (dědění s šedým placeholderem + „×" reset) · **kopírování stylu**.
**▸ Tabulky:** **Fixed** (řádky ručně, buňky z `{POLE}`) vs **Dynamic** (řádky z datové kolekce) · editor sloupců (pořadí/šířky/viditelnost/styl) · **agregace** SUM/COUNT/AVG/MIN/MAX v souhrnných řádcích.

### 2d) Výrazy a podmíněný obsah (3 min) — *„šablona reaguje na data"*
**AKCE:** placeholder `{NAZEV}` → formátování `{CENA:N2}` / `{NAZEV:UPPER}`. Nastav **podmíněnou viditelnost** (`{PAGE}>1`) → na 1. straně zmizí. Napiš **chybný výraz** → červené upozornění.
**ŘÍKÁŠ:** „Jedna šablona pokryje víc variant podle dat — dřív bylo vše napevno."
**▸ Lze tu zmínit:** formátování `{POLE:LEFT(4)}`, datum/čas dle jazyka · operátory `&&` / `||` / `!` · agregace v tabulce · validace výrazu v UI.

### 2e) Sekce, layout, stránka (4 min)
**AKCE:** ukázat **sekce** (levý panel), **drag&drop řazení**, chování tisku (hlavička každá stránka / patička). V náhledu **více stránek pod sebou**.
**ŘÍKÁŠ:** „Hlavičky, patičky a stránkování řeší engine sám."
**▸ Sekce:** role Header/Body/Footer · chování tisku (Jednou / Každá stránka / Jen první / Jen poslední / Kromě první) · přetečení (Nepřerušovat / Rozdělit přes stránky) · **vlastní šablony sekcí** (uložit a vkládat) · skrytí záhlaví sekcí · redistribuce výšek.
**▸ Stránka/šablona:** PageSize (A2–A6, orientace, okraje) · **škálování šablony 50–200 %** · **normalizace pozic** (Ctrl+Shift+N) · mřížka 1/2/5/10 mm · parametry sestavy.
**ŘÍKÁŠ (lokalizace):** „Šablona má **jazyk** (CZ/SK/EN/DE) — formát čísel a dat se řídí jím, **ne nastavením Windows**. Tisk vypadá stejně na každém počítači."

### 2f) Náhled, export, šablony (3 min)
**AKCE:** **Export PDF** → otevři výsledek (zmiň PNG + PrintDialog). `Soubor → Otevřít z databáze` → **správce šablon** (filtr agendy, search).
**ŘÍKÁŠ:** „Šablony jsou uložené **centrálně podle firmy** — ne roztroušené po počítačích."
**▸ Lze tu ukázat/zmínit:** živý náhled (filtruje viditelnost) · multi-page · Fit-to-Page (Ctrl+0) · **varování o přetečení** (červený rámeček) · **strom šablony** · **šablona = soubor (JSON)** → přenositelná, poslat kolegovi/dopravci · per-uživatel stav (zoom, mřížka, naposledy otevřené, parametry tisku).
**POZOR:** varování o přetečení — hlídej false-positive #8.

### 2g) Import z PDF + AI asistence (volitelné / experimentální, 4 min)
> Prezentuj jako **pomůcku a výhled**, ne produkční slib.

**AKCE (PDF import):** `Soubor → Import z PDF` → **3 režimy grafiky** (žádná / jen čáry / vše), detekce okrajů, auto-seskupení textů → z PDF vzniknou prvky jako základ šablony.
**AKCE (AI — pokud je klíč):** zapnout **AI enrichment** + agendu → heuristika (geometrie) → AI (sémantika) → **auto-přiřazení DB polí**. Zmínit **AI Vision** (rozpoznání struktury JŘ z obrázku stránky).
**ŘÍKÁŠ:** „Z existujícího PDF umíme předpřipravit šablonu. S AI navíc pozná, co je co. Bez klíče funguje aspoň heuristika. Akcelerátor návrhu, dolaďujeme."
**POZOR:** AI naživo je riziko (síť/klíč) — měj **zálohu**; klidně přeskoč.

**Most do Části 3:** „To byl obecný editor. Teď to nejdůležitější — jak z dat jízdního řádu vznikne konkrétní zastávkový tisk."

---

# ČÁST 3 — Zastávkové JŘ a business logika (~23 min)

> **Jádro pro provozní uživatele.** Sem patří obě nové věci: **vykreslení NZ** a **FTL sloupce odjezdů**.

### 3a) Tři specializované prvky (3 min)
**AKCE:** ukázat 3 prvky: **tabulka zastávek**, **mřížka odjezdů**, **legenda poznámek**.
**ŘÍKÁŠ:** „Tyhle tři prvky rozumí datům JŘ — vy řešíte vzhled, logiku spočítá systém."

### 3b) Zdroj dat a výběr linky (3 min)
**AKCE:** **Ctrl+Shift+A** (nebo klik na štítek) → agendy → zůstaň na **AD JŘ**. Otevřít **výběr linek** → vyhledat, **multi-výběr**. Přepnout instanci (◀▶) → náhled se přerenderuje.
**ŘÍKÁŠ:** „Stejný designer, **dva zdroje dat** (linkové i MHD). Linku přepnu a hned vidím jiná data — bez znovuotevírání."

### 3c) Tabulka zastávek (4 min)
**AKCE:** otevřít **editor sloupců** → pořadí, šířky, viditelnost, styl per sloupec. Ukázat **atributy u názvu**, **šipku směru**, zvýraznění aktuální zastávky.
**ŘÍKÁŠ:** „Sloupce si poskládáte, jak potřebujete pro danou linku."
**▸ Co vidím / nastavím / co je auto:**
- *Vidím:* zastávky v pořadí trasy, časy jízdy, atributy, šipku směru.
- *Nastavím:* které sloupce + pořadí/šířky/styl · pozici značek **za názvem** (CB_DP) / **před názvem** (DPKV) · šipku (vlevo/vpravo, barva) · zvýraznění aktuální zastávky (tučně/podtržení/podbarvení) · zobrazit projeté zastávky.
- *Auto:* kumulativní časy jízdy, atributy z číselníku, filtr „na znamení", umístění šipky.
**▸ Atributy u názvu zastávky:** × = na znamení · { = jízdenkový automat · (ikona) = přestup na železnici.

### 3d) ⭐ Mřížka odjezdů — FTL sloupce + NZ (6 min) — *NOVÉ, klíčový moment*
**AKCE:** 1. otevři **nastavení sloupců odjezdů** → **FTL** — sloupce skupin dnů (Prac / So / Ne+svátky / prázdniny…) **jako ve starém nástroji** · 2. přenastav sloupce (pořadí / viditelnost / nadpis) → náhled · 3. v mřížce ukaž **anotace u minut**: týdenní platnost, **negativní značky (×, ⊘) — správně vykreslené inverzně**, bezbariér (H/F), rozdíly tras (A/B).
**ŘÍKÁŠ:**
- u FTL: *„Tohle je přesně to, co šlo nastavit ve starém nástroji — rozvržení sloupců dnů — a teď to jde taky, ale vizuálně a s živým náhledem."*
- u NZ: *„Negativní značky se vykreslují přesně jako v originále."*
**▸ Co vidím / nastavím / co je auto:**
- *Vidím:* sloupce = skupiny dnů, řádky = hodiny, v buňkách minuty s horními indexy.
- *Nastavím:* rozvržení sloupců dnů (FTL) · pořadí/nadpisy/viditelnost sloupců · sloupec hodin (vlevo / u každé skupiny) · **časová osa** (jen hodiny s odjezdy / pevný rozsah / 24 h / od první do poslední) · zebra · styl písma.
- *Auto:* rozvržení dle FTL, všechny anotace (viz 3e), redistribuce šířek, výška řádků.
**POZOR:** tohle je highlight — nech tomu prostor, je to přímá odpověď na „uměl to starý nástroj?".

### 3e) Typy poznámek + co engine řeší automaticky (5 min)
**ŘÍKÁŠ:** „Značky u minut nepíšete ručně — počítají se z dat. Tady je co která znamená:"
**▸ Typy anotací u minut:**

| Typ | Příklad | Co znamená |
|---|---|---|
| **Týdenní platnost** | Po, So, Ne | spoj jede jen v určité dny |
| **Negativní značka** | ×, ⊘ | spoj **nestaví všude** / omezená trasa (inverzně) |
| **Rozdíl trasy** | A, B, C | jiná trasa → legenda „neobsluhuje zast. X", „končí v zast. Y", „jede přes…", „jede až do…" |
| **Bezbariérovost** | H, F | H = bezbariérové, F = částečně (nutná dopomoc) |
| **Speciální znaky spoje** | ikony | zvláštní režim spoje |
| **Textové poznámky** | text | volná poznámka u spoje |
| **Ruční poznámky** (AD) | komentář | uživatelské — komentáře, směrové (tam/zpět/vše) |

**▸ Co systém počítá automaticky (uživatel nepíše):** časy jízdy · rozdíly tras + texty · negativní značky · bezbariér (souhrn „všechny spoje bezbariérové" / per spoj) · omezení platnosti („nejede 24.–31. 12.") · dopravce (popis + adresa) · skupiny dnů → sloupce · naplnění + deduplikace legendy.
**ŘÍKÁŠ (klíčová věta):** *„Vy řešíte vzhled — sloupce, font, kde značky. Co která značka znamená, jakou trasou spoj jede, kdy nejede — spočítá systém z dat. Ověřeno 1:1 proti původnímu tisku."*

### 3f) Legenda poznámek (2 min)
**AKCE:** ukázat **víc sloupců**, **řazení dle délky**, zarovnaný marker.
**ŘÍKÁŠ:** „Legenda se sestaví sama z toho, co je na stránce, a uspořádá se, aby byla čitelná."
**▸ Nastavím:** počet sloupců (1–4) · řazení dle délky · směr plnění · zarovnaný marker · filtr co zobrazit (NZ / spec. znaky / textové).

### 3g) Dávkový tisk + ⭐ rychlost (2 min)
**AKCE:** **PrintDialog** s reálnými daty → **členění výstupu** (vše / per linka / per zastávka×směr) + název souboru z polí.
**ŘÍKÁŠ:**
- „Jedna šablona × stovky stránek — linka × zastávky × oba směry — do jednoho běhu."
- ⭐ **Rychlost:** „Tisk je rychlý — řádově **~100 stran PDF za sekundu** (render); **stovky stránek** i s načtením dat za **pár sekund**." *(reálně ~600 stran za ~9 s vč. dat)*
**ŘÍKÁŠ (závěr Části 3):** „Vy řešíte vzhled, logiku spočítá systém — a vytiskne se to dávkově a rychle."

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
