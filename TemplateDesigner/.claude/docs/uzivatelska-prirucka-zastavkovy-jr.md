# Návrhář šablon — zastávkový jízdní řád

> Navazuje na [`uzivatelska-prirucka-obecna.md`](uzivatelska-prirucka-obecna.md) — obecné
> ovládání (šablony, sekce, prvky, styly, export) tady neopakujeme. Tento dokument popisuje
> jen to, co je specifické pro agendu **zastávkový jízdní řád** (vývěska na zastávce:
> seznam odjezdů z jedné zastávky v jednom směru).

---

## Obsah

1. [Co je zastávkový jízdní řád](#1-co-je-zastávkový-jízdní-řád)
2. [Dva zdroje dat](#2-dva-zdroje-dat)
3. [Tři specializované prvky](#3-tři-specializované-prvky)
4. [Editory sloupců](#4-editory-sloupců)
5. [Výběr linek k tisku](#5-výběr-linek-k-tisku)
6. [Co systém řeší automaticky](#6-co-systém-řeší-automaticky)
7. [Tisk a export](#7-tisk-a-export)
8. [Auto-přizpůsobení a přetečení](#8-auto-přizpůsobení-a-přetečení)
9. [Postup krok za krokem](#9-postup-krok-za-krokem)
10. [Známá omezení](#10-známá-omezení)
11. [Časté otázky](#11-časté-otázky)

---

## 1. Co je zastávkový jízdní řád

Zastávkový jízdní řád je typ sestavy, který se vylepuje na konkrétní zastávku — obsahuje seznam
zastávek trasy s časy a mřížku minut odjezdů z **této jedné zastávky v jednom směru**. Šablona se
skládá ze tří specializovaných prvků (kap. 3), které samy umí velkou část logiky JŘ — vy
nastavujete hlavně **vzhled** (sloupce, barvy, fonty), systém dopočítá **obsah** (kap. 6).

---

## 2. Dva zdroje dat

Šablona zastávkového JŘ může čerpat data ze dvou různých agend. Obě produkují stejný typ dat
(zastávky, skupiny dnů, odjezdy, poznámky), takže tytéž tři prvky fungují nad oběma — liší se jen
zdroj a pár doplňkových nastavení.

| Agenda | Zdroj dat | Typicky pro |
|---|---|---|
| **Zastávkový JŘ MHD** | modul MHD | městská hromadná doprava |
| **Zastávkový JŘ AD** | hlavní modul Jízdní řády | příměstská/regionální doprava (i některé MHD linky vedené v tomto modulu) |

Mezi agendami se přepíná stejně jako mezi jakýmikoli jinými — `Nástroje → Změnit agendu…`
(Ctrl+Shift+A) nebo klik na zelený štítek. Přepnutí založí novou prázdnou šablonu (viz obecná
příručka kap. 3).

### Co je navíc jen u varianty AD

- **FTL 0–3 layouty** — čtyři možná rozvržení sloupců (skupin dnů), odvozená automaticky
  z konfigurace linky: FTL0 = Pracovní / Sobota / Neděle+svátky (3 sloupce), FTL1 = Pracovní mimo
  prázdniny / Pracovní o prázdninách / Sobota+Neděle+svátky (3 sloupce), FTL2 = Pracovní /
  Sobota+Neděle+svátky (2 sloupce), FTL3 = Pracovní mimo prázd. / Pracovní o prázd. / Sobota /
  Neděle+svátky (4 sloupce).
- **Parametr „Rozdělení sloupců (typy provozu)"** v tiskovém dialogu — ruční přepsání
  automatické volby FTL: Automaticky (dle linky) / Pracovní-Sobota-Neděle+svátky /
  Pracovní-víkend / Prázdniny (mimo+o prázdninách)-víkend / Prázdniny (mimo+o prázdninách)
  -Sobota-Neděle+svátky.
- **Multi-varianta časů jízdy** (přepínač „Všechny varianty tras" na prvku Tabulka zastávek) —
  když různé spoje jedou z dané zastávky s odlišnou dobou jízdy do cíle, zapnutím se zobrazí
  dynamicky tolik sloupců, kolik je skutečně variant v datech (ne fixní počet). Vypnuto = jeden
  sloupec se základní trasou.
- **CIS názvy** — alternativní, zkrácený název zastávky/směru z centrálního číselníku (CIS) vedle
  plného lokálního názvu.
- **Dopravce vč. adresy** — pokud pro dopravce není nastavený zkrácený název, vypíše se plný tvar
  „obchodní název, sídlo provozovny, tel: číslo".
- Doplňkové tiskové parametry: **„V rozsahu nepoznámkovat schovat"** (Ruční komentáře /
  Automatické rozdíly tras / Obojí) a **„Skrýt souhrn 'všechny spoje bezbariérové'"**.

---

## 3. Tři specializované prvky

### Tabulka zastávek

Levý sloupec sestavy — seznam zastávek s časy a atributy.

| Vlastnost | Co dělá |
|---|---|
| Upravit sloupce… | otevře editor sloupců (kap. 4) |
| Všechny varianty tras | zap. = víc časových sloupců podle skutečných variant tras (jen AD); vyp. = jeden sloupec základní trasy |
| Oddělovací čáry | vodorovné čáry mezi řádky zastávek |
| Odsazení | odsadí název zastávky mezerami podle jejího odsazení v trase (vizuální hierarchie) |
| Sloupec Nástupiště | volitelný sloupec s číslem nástupiště/stanoviště (zapíná se v editoru sloupců; tiskne se jen tam, kde jsou data vyplněná) |
| Na znamení + Symbol na znamení | zobrazí značku „na znamení" u zastávky a definuje, jaký znak se použije (výchozí „×") |
| Atributy | zobrazí atributy zastávky vedle názvu |
| Poznámky zastávky | zobrazí poznámkové značky u zastávky |
| Pozice značek | Za zastávkou (výchozí) / Před zastávkou |
| Šipka směru + Pozice šipky + Barva šipky + Tučná šipka | grafická šipka ukazující směr jízdy od aktuální zastávky dál po trase |
| Záhlaví sloupců | zap/vyp řádek záhlaví |
| Podtržení aktuální / Tučně aktuální / Font aktuální (pt) / Pozadí aktuální | zvýraznění řádku aktuální zastávky (vlastní velikost písma, podbarvení přes celou šířku) |
| Projeté zastávky + Barva projetých | zda tisknout i zastávky, které vozidlo od aktuální zastávky již projelo (ztlumeně) |

### Mřížka odjezdů

Mřížka minut po skupinách dnů (sloupce = pracovní den / sobota / neděle…).

| Skupina nastavení | Co obsahuje |
|---|---|
| **Odjezdy** | Upravit sloupce…, Záhlaví skupin, Velikost záhlaví, Min. výška řádku záhlaví, Zarovnání záhlaví, Šířka sloupce hodin, Hodiny u každé skupiny (sloupec hodin se opakuje u každé skupiny dnů), Max. skupin (0 = bez limitu), Šířka minuty, Oddělovač skupin |
| **Časová osa** | Jen hodiny s odjezdy / Souvislá od první do poslední / Vlastní rozsah (+ pole Hodiny od/do); Roztáhnout na výšku — když je mřížka nižší, než by odpovídalo datům, řádky (a font) se proporcionálně zvětší (strop 1,5× přirozené výšky) |
| **Řádky dat** | Výška řádku, Zebra barva (podbarvení sudých řádků) |
| **Záhlaví — 2. řádek** | Velikost, Tučné, Kurzíva, Barva textu pro druhý řádek nadpisu skupiny (např. „den" v „Pracovní / den") — pokud nevyplněno, dědí z prvního řádku |
| **Styl minuty** | Font, Velikost, Tučné, Kurzíva, Barva textu |
| **Anotace — symboly** a **Anotace — text** | samostatný styl (font/velikost/řez/barva) pro horní index u minut — symboly/negativní značky vs. textová písmena/dny; u symbolů navíc NZ pozadí a NZ znak (barvy invertovaného boxu negativní značky) |
| **Anotace** | zap/vyp zobrazování anotací u minut |
| **Mezery** | Mezera minuta↔anotace, Mezera anotace↔anotace, Mezera mezi odjezdy |

### Legenda poznámek

Vysvětlivky pod mřížkou.

| Vlastnost | Co dělá |
|---|---|
| Nadpis | text záhlaví legendy (výchozí „Poznámky") |
| Oddělovač | text mezi značkou a popisem (výchozí „ — ") |
| Velikost písma pozn. / Výška řádku | typografie legendy |
| Počet sloupců (1–4) + Směr plnění | „Po sloupcích" (jako noviny) nebo „Po řádcích" |
| Řazení | Výchozí (pořadí z dat) / Délka vzestupně / Délka sestupně |
| Šířka buňky značky | pevná šířka sloupce se značkou, aby byly texty svisle zarovnané (0 = značka inline před textem) |
| Negativní značky / Speciální znaky / Textové poznámky | přepínače, které typy poznámek se v legendě vůbec zobrazí |
| NZ pozadí / NZ barva | barvy invertovaného boxu u negativních značek |

Všechny tři prvky mohou mít pevnou nebo dynamickou (automatickou) výšku a při pevné výšce
uplatňují **auto-zmenšení písma** (kap. 8).

---

## 4. Editory sloupců

### Editor sloupců zastávek
Dvoutabový dialog: **Obecné** (vlastnosti prvku) a **Sloupce**:

- Seznam sloupců s checkboxem viditelnosti a tlačítky ▲/▼ pro pořadí; sloupce chybějící
  v uložené šabloně se doplní automaticky (skryté, ale kdykoli zapnutelné).
- Pro vybraný sloupec: **šířka** v mm (prázdné = automatické rozdělení zbylého prostoru),
  **zarovnání**, a plný per-sloupec styl (font, velikost, řez, barvy, zarovnání, odsazení) zvlášť
  pro záhlaví a data — viz kaskáda stylů v obecné příručce kap. 7.
- **Dvouřádkové záhlaví** — do pole „Záhlaví" lze zadat víc řádků (např. český a cizojazyčný
  název sloupce, „Zastávka" / „Station"): napište `\n` nebo stiskněte Enter. Výška záhlaví se
  přizpůsobí automaticky. Detail viz obecná příručka kap. 8.
- Varování při přesahu šířky (s přesnými čísly) + tlačítko „Přizpůsobit šířky" pro proporční
  přeškálování.
- Tlačítka „Resetovat styl" (vybraný sloupec) a „Resetovat všechny styly".

### Editor skupin dnů (sloupec hodin + skupiny)
Analogický dvoutabový dialog:

- První řádek je vždy pevně **„Hodiny"** (nelze přesunout), následují jednotlivé skupiny dnů
  podle skutečných dat.
- Pro skupinu: viditelnost, pořadí (▲/▼), **přepis nadpisu** (jinak se použije nadpis z dat, u
  sloupce hodin výchozí „Hod"), pevná šířka (prázdné = automatický výpočet + rovnoměrné dofill),
  vlastní barva zebry pro danou skupinu.
- Per-sloupec styl barev a zarovnání zvlášť pro záhlaví a data. **Typografie záhlaví (font,
  velikost, řez) je ale jednotná pro celý prvek** — nedá se nastavit odlišně po jednotlivých
  skupinách, aby šla velikost písma měnit z jednoho místa v panelu.

---

## 5. Výběr linek k tisku

Dialog výběru instancí — vyhledávací pole (Enter spustí filtr), seznam linek s checkboxy pro
výběr více najednou, tlačítka „Vybrat vše" / „Odznačit vše". Otevře se klikem na modrý štítek
„Instance: X — Y (N/M)" ve status baru.

Po výběru více linek se mezi nimi v hlavním okně přepínáte šipkami ◀ / ▶ vedle štítku — každá
změna přenačte data pro danou instanci a živě aktualizuje náhled. Vybrané linky slouží i pro
dávkový export/tisk (kap. 7).

---

## 6. Co systém řeší automaticky

Toto **nemusíte nastavovat ručně** — spočítá se z dat:

- **Omezení platnosti („jede / nejede")** — textové omezení se generuje automaticky z bitmapy
  platnosti spoje (výjimky v kalendáři).
- **Rozdíly tras** — pokud různé spoje ze stejné zastávky jedou po odlišné trase (jiná konečná,
  vynechané zastávky), systém automaticky přiřadí písmenné značky (A/B/C…) k odjezdům a
  vygeneruje vysvětlující text do legendy poznámek (např. „zastávky obsluhují pouze shodně
  označené spoje", „končí v zast. …", u AD navíc „neobsluhuje zast. X").
- **Atributy zastávek** (např. „na znamení") — zobrazí se jen relevantní atributy; ostatní jsou
  dostupné, ale ve výchozím stavu se netisknou.
- **Bezbariérovost spoje** — pokud spoj obsluhuje bezbariérové vozidlo (plně nebo částečně),
  automaticky se zobrazí **piktogram** u odjezdu a vysvětlivka do legendy.
- **Dopravní symboly** — značky (bezbariérovost, denní číslice ①–⑦ v kroužku ap.) se tisknou
  standardními piktogramy shodnými s dosavadními jízdními řády (vestavěný symbolový font),
  česká i slovenská sada.
- **Dopravce a jeho adresa** — sestaví se ze všech dopravců obsluhujících linku (deduplikovaně).
- **CIS názvy** — dotáhnou se z centrálního číselníku jako alternativa k lokálnímu názvu.
- **Negativní značky** — spoj, který nestaví na všech zastávkách, automaticky dostane přiřazenou
  značku (např. ×) vykreslenou invertovaně, s vysvětlením v legendě.
- **Řazení poznámek v legendě** — legenda automaticky sbírá poznámky zastávky, poznámky posbírané
  z odjezdů na stránce a atributy zobrazených zastávek, řadí je ve fixním pořadí (bezbariér →
  rozdíly tras → omezení) a odstraňuje duplicity.
- **Konečná zastávka** — pro zastávku, za kterou už v daném směru nikam nejede, se stránka vůbec
  nevygeneruje (přeskočí se, ne že by se vytiskla prázdná).
- **Popis směru** — pole `{POPIS_SMER}` obsahuje **název konečné zastávky** daného směru
  (např. „Ostrov, Terminál"); technické označení směru tam/zpět je v poli `{SMER}` (T/Z).

---

## 7. Tisk a export

### Parametry sestavy v tiskovém dialogu
Generují se dynamicky podle zvolené agendy. Společné pro obě agendy:

- **Tisknout šipky směru**, **Tisknout projeté zastávky**
- **Časová osa odjezdů** (Jen hodiny s odjezdy / Souvislá od první do poslední / Vlastní rozsah +
  Hodiny od/do)
- **Roztáhnout na výšku**

Navíc jen u AD: **Rozdělení sloupců (typy provozu)**, **V rozsahu nepoznámkovat schovat**,
**Skrýt souhrn „všechny spoje bezbariérové"** (viz kap. 2) a **Souhrn „všechny zastávky na
znamení"** — místo značky × u každé zastávky jeden souhrnný řádek v legendě (má smysl na
linkách, kde jsou na znamení všechny zastávky).

### Export — členění a pojmenování
Tato pole se nastavují v tiskovém dialogu (nejsou to parametry sestavy, takže se neukazují v liště
živého náhledu):

- **Členění výstupu** — Vše do jednoho PDF / Jeden PDF na linku / Jeden PDF na zastávku a směr.
- **Název souboru (tokeny {POLE})** — výchozí `{CISLO_LINKY}_{POPIS_ZASTAVKA}_{POPIS_SMER}`,
  lze upravit na libovolnou kombinaci datových polí.

K dispozici je i **Uspořádání na list (imposice)** — např. dvě A5 vývěsky na jeden list A4,
včetně opakování téže vývěsky pro řezání a ořezových značek; detail viz obecná příručka kap. 9.

„Vše do jednoho" vybere jeden cílový soubor; ostatní dvě volby vybírají cílovou složku a vytvoří
tolik souborů, kolik odpovídá volbě (kolize jmen se řeší automaticky `_2`, `_3`…). Export běží
asynchronně nad snímkem stavu šablony pořízeným při kliknutí — mezitím lze dál v šabloně
pracovat, výsledek odpovídá stavu v okamžiku exportu. Po dokončení se otevře výsledný PDF nebo
cílová složka a status bar zobrazí souhrn (počet souborů, celkový počet stránek, případné
varování o přetékajících stránkách).

---

## 8. Auto-přizpůsobení a přetečení

Pokud má některý ze tří prvků JŘ nastavenou **pevnou výšku** a skutečný obsah (počet zastávek,
odjezdů, poznámek) by potřeboval víc místa, systém automaticky **proporcionálně zmenší
velikost písma**, aby se všechno vešlo do vymezeného prostoru. Pokud je výška prvku dynamická
(automatická), tohle se neuplatňuje — prostor prostě roste podle potřeby.

Varování o přetečení (červený rámeček stránky v náhledu) se u JŘ elementů řídí stejným
pravidlem jako v obecné příručce (kap. 9 obecné příručky): objeví se jen u sekcí nastavených
jako „Nepřerušovat", a jen když se prvek fyzicky nevejde i po auto-zmenšení písma — pokud
zmenšení pomůže, k varování nedojde.

---

## 9. Postup krok za krokem

1. **Založte šablonu se správnou agendou** — nová šablona přes `Nástroje → Změnit agendu`
   (Ctrl+Shift+A) a výběr Zastávkový JŘ AD nebo MHD. Alternativa: otevřete výchozí šablonu
   ze sekce **Systémové M-line** (Ctrl+O), nebo naimportujte existující PDF vývěsku
   s AI rozpoznáním (`Nástroje → Import PDF…`) a jen ji dolaďte.
2. **Přidejte prvky** — z palety nebo toolbaru vložte Tabulku zastávek (typicky vlevo), jednu
   nebo víc Mřížek odjezdů (typicky vedle sebe, rozdělené podle skupiny dnů) a volitelně Legendu
   poznámek (pod mřížku).
3. **Nastavte sloupce** — tlačítkem „Upravit sloupce…" na každém prvku otevřete příslušný editor
   a zapnete/přeuspořádáte sloupce, doladíte šířky a vzhled.
4. **Vyberte linky** — přes dialog výběru instancí (kap. 5) zvolte jednu nebo víc linek; náhled
   procházíte šipkami mezi vybranými.
5. **Doladíte parametry tisku** — v tiskovém dialogu (Ctrl+P) nastavíte parametry specifické pro
   agendu a způsob exportu (členění, název souboru).
6. **Exportujte nebo vytiskněte** — export náhledu (ukázková data), nebo dávkový export reálných
   dat pro všechny vybrané linky s automatickým otevřením výsledku.

Pro rychlý start je v přehledu šablon (Ctrl+O) sekce **Systémové M-line** s výchozí
zastávkovou šablonou (AD) dodávanou a aktualizovanou M-line.

---

## 10. Známá omezení

Podle aktuálního stavu vývoje (porovnáno s předchozím tiskovým nástrojem) zatím zbývá dořešit:

- **Šířka a layout legendy** — drobné vizuální doladění.
- **Vlastní rozdělení skupin dnů** — jemnější než čtyři přednastavené režimy (FTL 0–3), tj.
  libovolná kombinace dnů nad rámec toho, co nabízí parametr „Rozdělení sloupců".
- **Jemnější výběr variant tras** — dnes je přepínač „Všechny varianty tras" jen zapnuto/vypnuto
  za celou zastávku, ne výběr konkrétní varianty (písmena).

---

## 11. Časté otázky

**Jak poznám, ze kterého modulu šablona čerpá data?**
Podle zvolené agendy (zelený štítek): „Zastávkový JŘ MHD" čte z modulu MHD, „Zastávkový JŘ AD"
z hlavního modulu Jízdní řády. Volba se dělá při založení šablony a nedá se dodatečně přepnout
beze ztráty rozpracovaného obsahu.

**Proč se u některé linky objeví jiný počet sloupců skupin dnů, než jsem čekal?**
U AD agendy se rozdělení sloupců (FTL layout) odvozuje automaticky z konfigurace linky. Pokud
potřebujete jiné rozdělení, zkuste parametr „Rozdělení sloupců (typy provozu)" v tiskovém
dialogu — nabízí několik pevných alternativ k automatice.

**Musím ručně zadávat, které spoje nestaví na všech zastávkách?**
Ne — negativní značky a rozdíly tras se počítají automaticky z dat trasy a platnosti spojů,
včetně textu do legendy.

**Proč se pro některou zastávku nevygenerovala stránka?**
Pokud je zastávka v daném směru konečná (dál v tomto směru už žádná linka nejede), stránka se
pro ni záměrně negeneruje.

**Bude tisk vypadat stejně jako dosud?**
U zastávkového JŘ z hlavního JŘ modulu (AD) je výstup ověřený 1:1 proti původnímu tisku na
konkrétních referenčních linkách; drobné vizuální rozdíly viz kap. 10.
