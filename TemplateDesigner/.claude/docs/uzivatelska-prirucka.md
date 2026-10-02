# Návrhář sestav - uživatelská příručka

⚠️ **Důležité - které jízdní řády už umíme:** **zastávkové** (AD i MHD) a nově **linkové**
(zatím jen AD, MHD se připravuje). Ostatní sestavy zatím tiskne Report Generátor.

> Vizuální editor tiskových sestav v EDISONu. Navrhnete vzhled šablony, systém k ní doplní
> data a vytiskne PDF. **Část I** pokrývá obecné ovládání aplikace - práci se šablonou,
> sekcemi, prvky, styly, daty a exportem - bez ohledu na typ sestavy. **Část II** popisuje
> specifika jednotlivých agend (zastávkový a linkový jízdní řád; další agendy přibudou jako
> nové kapitoly).

**📦 Než začnete (pro IT):** [Technické požadavky a instalace](https://podpora.m-line.cz/support/wiki/3a07cae1-1198-4ffa-8875-856e3fc36094) - instalace .NET Desktop Runtime, kde nástroj v EDISONu najdete.
**🆕 Co je nového:** [Novinky a plán rozvoje](https://podpora.m-line.cz/support/wiki/6d48ca01-ce81-45d8-a396-76fcf0f982db) - přehled novinek a co chystáme.

---

# ČÁST I - Obecné ovládání

---

## Obsah

1. [Co je Návrhář sestav](#1-co-je-návrhář-sestav)
2. [Orientace v aplikaci](#2-orientace-v-aplikaci)
3. [Práce se šablonou](#3-práce-se-šablonou)
4. [Sekce a struktura stránky](#4-sekce-a-struktura-stránky)
5. [Práce s prvky (elementy)](#5-práce-s-prvky-elementy)
6. [Editační nástroje](#6-editační-nástroje)
7. [Vlastnosti a styly](#7-vlastnosti-a-styly)
8. [Datová pole a výrazový jazyk](#8-datová-pole-a-výrazový-jazyk)
9. [Náhled, export a tisk](#9-náhled-export-a-tisk)
10. [Import z PDF](#10-import-z-pdf)
11. [Uložená nastavení](#11-uložená-nastavení)
12. [Klávesové zkratky](#12-klávesové-zkratky)

---

## 1. Co je Návrhář sestav

Návrhář sestav je **vizuální editor tiskových sestav** typu WYSIWYG („co vidíte, to dostanete").
Nahrazuje starší nástroj, kde se sestavy nastavovaly přes formuláře a číselné parametry bez
náhledu - výsledek byl vidět až po vytištění.

Systém má dvě vrstvy:

1. **Návrhář** - zde vizuálně navrhnete šablonu (rozložení stránky, texty, tabulky, jízdní řády).
2. **Tiskové jádro** - vezme šablonu a reálná data a vytvoří PDF.

Klíčová myšlenka: **šablona popisuje jen vzhled**. Konkrétní hodnoty (čísla linek, časy, názvy
zastávek) doplní systém z databáze až při tisku. Stejné jádro se používá pro živý náhled
v Návrháři i pro reálný tisk - proto náhled odpovídá výslednému výstupu.

---

## 2. Orientace v aplikaci

Okno Návrháře má čtyři hlavní části: **lištu nástrojů** nahoře, **levý panel** se záložkami, **plátno**
uprostřed a **náhled** vpravo.

### Lišta nástrojů

Tlačítka jsou seskupená a oddělená svislými čarami:

- **Soubor** - Nová šablona, Otevřít, Uložit
- **Upravit** - Zpět, Vpřed
- **Stránka** - Vlastnosti stránky
- **Sekce** - Přidat sekci (rozbalovací nabídka presetů)
- **Přidat / Elementy** - paleta prvků: Text, Obdélník, Čára, Obrázek, Tabulka, Kód (QR, čárový) (u agend jízdních řádů se navíc objeví specializované JŘ prvky)
- **Vrstva** - Přenést dopředu / Odeslat dozadu / Přenést zcela dopředu / Odeslat zcela dozadu
- **Zarovnat** - Zarovnat vlevo / na střed vodorovně / vpravo, nahoru / na střed svisle / dolů, rozložit rovnoměrně vodorovně/svisle (min. 3 vybrané prvky)
- **Data** - obnovení dat náhledu (↻)
- **Tisk** - kontrola vybraných linek (lupa) a Tisk; PDF i PNG se vytváří v okně Tisk
- **Mřížka a přichytávání** - přepínač mřížky, přepínač přichytávání k objektům, velikost mřížky, velikost mezery pro přichytávání
- **Zoom** - zoom, tlačítka „Fit" a „1:1"
- **Panely** - skrytí panelu sekcí, přepínač živého náhledu

### Levý panel - 6 záložek

- **Sekce** - seznam sekcí šablony, přidávání, řazení, mazání
- **Datová pole** - pole aktuální agendy a pole stránky (číslo stránky, počet stran, pokračování) (dvojklik nebo tažení vloží `{POLE}` na plátno); hlavička zobrazuje název aktuální agendy a je klikatelná - otevře přepnutí agendy
- **Vlastnosti** - nastavení právě vybraného prvku nebo sekce - okamžitá editace bez nutnosti otevírat dialog
- **Strom** - hierarchický přehled celé šablony (sekce → skupiny prvků → prvky) pro rychlou navigaci
- **Pomocník** - AI pomocník: zeptáte se vlastními slovy, kde v aplikaci co najdete nebo jak něco udělat (vyžaduje aktivovanou AI službu u M-line)
- **Parametry** - parametry sestavy pro aktuální agendu - nastavení ovlivňující obsah (např. časová osa odjezdů, tisk šipek směru). Jsou seskupené po blocích podle prvku, kterého se týkají (Seznam zastávek / Minuty odjezdů / Poznámky) a jejich změna se hned promítne do náhledu. Tytéž hodnoty jsou i v dialogu tisku. U šablon bez parametrů: „Tato šablona nemá parametry."

### Status bar (dole)

Zleva doprava:
- zelený štítek s aktuální **agendou a variantou** - klik otevře dialog přepnutí agendy,
- modrý štítek **aktivní instance** (např. vybrané linky) s tlačítky ◀ / ▶ pro přepínání mezi
  vybranými instancemi a formátem „Instance: X - Y (N/M)",
- informace o aktuálním výběru,
- vpravo **blok hlášek** k tisku (viz kap. 9, Hlášky a kontrola před tiskem).

### Hlavní menu

- **Soubor** - Nová šablona · Otevřít z databáze… · Uložit do databáze · Uložit do databáze
  jako… · Importovat ze souboru… · Exportovat do souboru… · Otevřít poslední soubor
  (podnabídka) · Tisk… · Zkontrolovat vybrané linky… · Vlastnosti stránky… · Konec
- **Upravit** - Zpět · Vpřed · Smazat · pořadí vrstev (dopředu / dozadu / zcela) · zarovnání
  (vlevo, vpravo, na střed, nahoru, dolů) · Rozložit vodorovně / svisle · Normalizovat pozice… ·
  Přepočítat šablonu na jinou velikost (podnabídka procent) · Kopírovat styl · Vložit styl ·
  Použít výchozí font u všech prvků
- **Zobrazit** - Mřížka, Přichytit na mřížku, Přichytit k elementům, Záhlaví sekcí, Pravítka,
  Panel sekcí, Živý náhled (zaškrtávací položky)
- **Nástroje** - Změnit agendu… · Šablony v databázi… · AI Asistent šablon… · Import PDF…
- **Nápověda** - Klávesové zkratky a ovládání (F1) - s fulltextovým hledáním (Ctrl+F)

Titulek okna má formát `[KÓD_AGENDY] [DB nebo neuloženo] Název šablony [*]` - hvězdička značí
neuložené změny.

---

## 3. Práce se šablonou

### Nová šablona
`Soubor → Nová` (Ctrl+N). Nová šablona obsahuje jednu výchozí sekci přes celou tiskovou plochu;
sekce a prvky pak přidáváte podle potřeby. **Převezme agendu té šablony, kterou máte právě
otevřenou** - když děláte víc vývěsek pro tutéž agendu, nemusíte ji vybírat znovu; přepnout jde
kdykoli přes `Nástroje → Změnit agendu` (Ctrl+Shift+A).

### Agenda a varianta - nad jakými daty šablona běží
Každá šablona je vázaná na **agendu** (jakou sestavu tvoříte - např. Zastávkový jízdní řád,
Faktury, Obecná) a u jízdních řádů navíc na **variantu dat** (odkud data jsou - **AD** = linková
doprava z hlavního JŘ modulu, **MHD** = městský modul). Kombinace určuje, jaká **databázová pole**
a jaké **specializované prvky** máte k dispozici.

- Aktuální agenda a varianta je vidět jako **zelený štítek** ve status baru (např. „Zastávkový
  JŘ · AD"), v titulku okna a v hlavičce panelu „Datová pole".
- Změna: `Nástroje → Změnit agendu…`, klávesa **Ctrl+Shift+A**, nebo klik na zelený štítek -
  dialog nabízí agendy seskupené s variantami pod sebou.
- **Přepnutí agendy vždy založí novou prázdnou šablonu** pro zvolenou agendu - nejde o konverzi
  obsahu stávající šablony. Rozdělanou práci vás systém před přepnutím vyzve uložit nebo zahodit.

### Otevření a uložení
Šablony se ukládají **do databáze** (centrálně, podle firmy), ne do lokálních souborů na stanici:

- `Soubor → Otevřít z databáze…` - dialog „Šablony v databázi": filtr podle agendy, sloupce
  **Agenda | Varianta** hned za názvem, sekce **Systémové M-line** (dodané předlohy, jen ke
  čtení - upravenou verzi uložíte jako vlastní šablonu) a **Moje šablony**; tlačítka
  Přejmenovat…, Smazat, Otevřít.
- `Soubor → Uložit do databáze` - u šablony již uložené v databázi provede tichý update.
- `Soubor → Uložit do databáze jako…` - dialog s polem pro název a popis; nová/importovaná
  šablona se takto poprvé zařadí do databáze.
- Výhoda oproti lokálním souborům: šablony jsou sdílené, centrální a dohledatelné - ne
  roztroušené po jednotlivých počítačích.

### Import a export do souboru
Šablonu lze také uložit jako **soubor JSON** - ne náhrada
databázového úložiště:

- `Soubor → Importovat z JSON souboru…` - otevře soubor; šablona se pak chová jako nová
  neuložená.
- `Soubor → Exportovat do JSON souboru…` - uloží aktuální stav do souboru, ale **nemění**
  původ šablony (pořád zůstává vázaná na databázi, pokud odtamtud byla otevřená).
- Hodí se pro poslání šablony e-mailem kolegovi nebo dopravci.

---

## 4. Sekce a struktura stránky

Stránka se skládá ze **sekcí**. Sekce je vodorovný pruh, do kterého umisťujete prvky. Každá
sekce má nastavené chování, které určuje **kdy a jak často** se tiskne a **co se stane při
přetečení** obsahu.

### Přidání sekce
Tlačítko `+ Přidat sekci` nabízí presety:

- **Hlavička stránky** - každá stránka, 25 mm
- **Patička stránky** - každá stránka, 15 mm
- **Oddělovač** - jednou, 5 mm
- **Hlavička sestavy - jen 1. str.** - jen první stránka, 30 mm
- **Patička sestavy - jen posl. str.** - jen poslední stránka, 20 mm
- **Hlavička kromě 1. str.** - všechny kromě první, 25 mm
- **Prázdná sekce** - jednou, 30 mm
- *(vlastní uložené šablony sekcí, značené 💾)* - podle uložené šablony

### Vlastnosti sekce (dialog)
Pole v dialogu vlastností sekce: **Název**, **Výška** (mm), **Typ sekce**, **Tisknout na**, **Přetečení**,
**Rámeček**, **Barva rámečku**, **Tloušťka rámečku**, **Podmínka viditelnosti**. Volby
v rozbalovacích seznamech:

- **Typ sekce**: Hlavička stránky · Obsah · Pod obsahem stránky · Patička stránky. Obsah se tiskne
  jednou v pořadí podle šablony; ostatní typy se opakují nahoře, hned pod obsahem, nebo dole na stránce
- **Tisknout na** (jen u hlavičky a patičky): Každé stránce · Jen první stránce · Jen poslední
  stránce - patička s „Jen poslední stránce" je dole jen na posledním listu. Pod obsahem stránky
  se tiskne vždy na každé stránce
- **Přetečení** (jen u obsahu): Držet pohromadě · Rozdělit přes stránky
- **Začít na nové stránce** (jen u obsahu): sekce začne na novém listu - např. poznámky na
  samostatné stránce za tabulkou. S přetečením „Rozdělit přes stránky" dlouhé poznámky pokračují
  na dalším listu. Na listu, kde ještě nic není, se nový nezakládá

**Doklad o více stránkách** (stazka s lícem a rubem, faktura s podmínkami na zadní straně): tlačítko
**„+ Přidat stránku"** pod seznamem sekcí přidá stránku šablony. Nad plátnem se pak objeví přepínač
stránek („Stránka 1 z 2") a plátno ukazuje vždy jednu stránku přesně velikosti papíru. Hlavička a patička jsou společné - jsou na každé
stránce, kde se tisknou, a upravit je jde z kterékoli (mají jinak barevný štítek). Obsah, který se
na stránku nevejde, je pod listem vidět v šrafovaném pásu: při tisku přeteče na další stránku.
Druhou stránku proto dělejte vždy přidáním stránky, ne roztažením sekce. Náhled vpravo ukazuje
všechny stránky za sebou; po vypnutí „Náhled všech stránek" nad ním jen otevřenou stránku.

Pole, která v dané kombinaci nic nedělají, formulář schová - barva a tloušťka rámečku jsou vidět
jen se zapnutým Rámečkem.

### Práce se sekcemi
- **Výška** - tři cesty. Ve vlastnostech sekce ji zadáte **číslem v mm**; na plátně ji **utáhnete
  za spodní hranu sekce** (kurzor se tam změní na svislé šipky, mřížka platí a Ctrl ji vypne,
  Esc tažení zruší); a pravým klikem na sekci vyberete **„Vyplnit zbytek stránky"**, což
  spočítá, kolik na papíře zbývá, a sekci na to roztáhne.

  Ve všech třech případech platí totéž: **sekce nad ní se nehne, sekce pod ní se posunou** a svou
  výšku si podrží. Prvky se posunou se svou sekcí, takže se vůči ní nikam nepohnou. Sekci nejde
  roztáhnout tak, aby ostatní vytlačila ze stránky - na hranici se tažení zastaví a stavový řádek
  to napíše. Sekce s **prázdnou výškou** se netahá, tu si dopočítá tiskové jádro z obsahu.

  **Zmenšit sekci pod její obsah nejde** - tažení se zastaví na spodní hraně nejnižšího prvku.
  Kdyby to šlo, prvek by v návrháři zmizel pod okrajem, ale na papíře by se vytiskl celý, protože
  tiskové jádro sekci nafoukne na obsah. Potřebujete-li sekci opravdu zmenšit, přesuňte nebo
  smažte prvek, který ji drží.

  **Patičku taháte za HORNÍ hranu** - je ukotvená u spodního okraje stránky, takže roste vzhůru.
  U ostatních sekcí je to spodní hrana. Hranu mezi dvěma sekcemi netřeba hledat dvakrát: je to
  spodní hrana té horní z nich.
- **Mazání** - poslední zbývající sekci nelze smazat; sekce s prvky vyžaduje potvrzení. Uvolněná
  výška se proporcionálně rozdělí mezi zbývající sekce (a naopak při přidání nové).
- **Řazení** - přetažením v levém panelu, nebo šipkami v kontextovém menu.
- **Vlastní šablony sekcí** - pravý klik na sekci → „Uložit jako šablonu…", zadáte název. Sekce
  se pak nabízí v menu „+ Přidat sekci" (💾) a lze ji opakovaně vkládat do dalších šablon -
  vkládá se vždy jako nezávislá kopie.

---

## 5. Práce s prvky (elementy)

Prvek je samostatný objekt na plátně. Dostupné typy a jejich hlavní vlastnosti:

- **Text** - text (víceřádkový), rodina a velikost písma (4–200 pt), tučné/kurzíva/podtržení/přeškrtnutí, zalamování (výchozí vypnuté), řádkování, zarovnání vodorovně (vlevo/na střed/vpravo/do bloku) i svisle, barva textu a pozadí
- **Obrázek** - nahrání souboru (limit 5 MB, automatické zmenšení větších), volitelné načtení z datového pole, škálování: vejít celý / vyplnit (oříznout) / roztáhnout / bez úpravy
- **Obdélník** - výplň, barva a tloušťka okraje (0–10 mm), zaoblení rohů (0–50 mm)
- **Čára** - orientace (vodorovná/svislá), barva, tloušťka, styl (plná/čárkovaná/tečkovaná), délka čárky a mezery
- **Kód (QR, čárový)** - obsah, typ kódu (**QR kód, Code 128, EAN-13, EAN-8, PDF417, DataMatrix**), barva kódu a pozadí.
  Kód vždy vyplní celý prvek. **Musí být dost velký, aby se dal sejmout z papíru** - potřebná
  velikost roste s délkou obsahu, takže dlouhý odkaz potřebuje víc místa než krátký. Když je
  prvek malý, aplikace po tisku napíše konkrétní čísla („QR kód je 10 mm, pro spolehlivé čtení
  potřebuje 13,2 mm")
- **Tabulka (dynamická)** - řádky se generují automaticky z datové kolekce (viz níže)
- **Tabulka (pevná)** - mřížku vyplňujete ručně přímo na plátně; náhrada za skládání hlaviček
  a bloků z jednotlivých textových prvků (viz níže)
- **Prvky jízdního řádu** - specializované prvky pro tisk JŘ - objeví se v paletě jen u agend jízdních řádů; viz část II

### Dynamická tabulka
Sloupce definujete jednou, řádky doplní systém z vybrané datové kolekce (počet řádků se odvíjí
od dat). Vlastnosti: **Upravit sloupce…**, **Zdroj dat**, **Výška řádku**, **Záhlaví**, **Výška
záhlaví**, **Mřížka** (čáry mezi řádky / mezi sloupci, barva, tloušťka, styl), **Střídání řádků**, **Tučné záhlaví**,
barvy pozadí a textu záhlaví i dat. Buňka může obsahovat souhrnný výraz - viz kap. 8.

Editor sloupců (dialog „Tabulka - nastavení") má záložky **Obecné** a **Sloupce**: přidání/
odebrání sloupce, varování při přesahu šířky + tlačítko „Přizpůsobit sloupce k šířce elementu",
a pro každý sloupec záhlaví, šířku, mapované datové pole a plný editor stylu (viz kap. 7).

### Pevná tabulka
Mřížka, kterou vyplňujete sami - typicky hlavička sestavy nebo blok údajů, se kterým chcete hýbat
jako s jedním prvkem. Při vložení se návrhář zeptá na **počet sloupců a řádků** (předvyplněno 3 × 3,
přepište třeba na 12 × 10); přidávat je jde i později přes pravé tlačítko. Tabulka se vkládá bez
řádku záhlaví - záhlaví si uděláte z prvního řádku tím, že ho ztučníte.

**Vyplňování:** dovnitř se dostanete **Alt+klikem** na buňku, klávesou **F2** nebo položkou
„Vyplnit tabulku" v místní nabídce. Uvnitř se píše jako v Excelu - psaní přepíše obsah, F2 upraví
stávající, **Tab** a **Enter** potvrdí a posunou dál, šipky chodí po buňkách, **Esc** vede ven.
**Delete** uvnitř maže obsah buňky, ne prvek. Ctrl+C/X/V pracují s buňkou; vložit jde i blok
zkopírovaný z Excelu (tabulka se dorovná). Po napsání `{` se nabídnou datová pole.

**Řádky a sloupce** se přidávají a mažou z místní nabídky; šířku sloupce a výšku řádku změníte
tažením vnitřní hranice (vnější hrana mění velikost celého prvku).

**Výběr více buněk:** tažením myší označíte obdélníkovou oblast, Shift+klik a Shift+šipky ji
rozšíří a **Ctrl+A** vybere celou tabulku. Nad výběrem pak funguje formátování najednou, **Delete**
vymaže obsah všech vybraných buněk a **Ctrl+C** je zkopíruje ve tvaru, který přečte Excel. Vložíte-li
do označené oblasti jedinou hodnotu, vyplní se jí celá oblast.

**Formátování buňky:** jakmile jste uvnitř, **levý panel ukazuje vybranou buňku** - písmo
a velikost, řez, tučné, kurzívu, podtržení, přeškrtnutí, zarovnání, barvu textu a pozadí, vnitřní
okraje, řádkování a **formát hodnoty** (např. `dd.MM.yyyy` nebo `N2` u buňky s datovým polem).
Totéž otevřete v samostatném okně přes **Alt+Enter** nebo místní nabídkou „Vlastnosti buňky".
Nejčastější úpravy jsou i ve **formátovací liště** nad plátnem a na **Ctrl+B / Ctrl+I / Ctrl+U**
(tučné, kurzíva, podtržení - fungují i během psaní).

**Vlastnosti řádku** (místní nabídka): **podmínka viditelnosti** a **barva pozadí řádku**.
Podmínka znamená „vytiskni tenhle řádek, jen když platí" - typicky u řádku s datovým polem, které
někdy chybí: `{VS}` jako podmínka schová řádek „Variabilní symbol: {VS}" na dokladu, který variabilní
symbol nemá. V návrháři takový řádek vidíte pořád (jinak byste ho nemohli upravit) a pozná se podle
odznaku v rohu; zmizí až v náhledu a v tisku.

**Sloučení buněk:** označte buňky v řádku a v místní nabídce zvolte **Sloučit buňky** - vznikne
jedna buňka přes celou šířku výběru, typicky nadpis. Případné texty ze slučovaných buněk se spojí
mezerou, aby se nic neztratilo. **Rozdělit buňky** to vrátí zpět a obsah nechá v první buňce.

**Prázdné pole znamená „dědí se" z nastavení celé tabulky.** Panel proto u nevyplněného pole
ukazuje hodnotu, kterou buňka reálně má; vymazáním pole se dědění vrátí. Nastavení celé tabulky
je za tlačítkem **„Nastavení celé tabulky"** v prvním řádku panelu (nebo klávesou Esc).

### Přidání prvku
- Klik na tlačítko v paletě vloží prvek do aktivní sekce (souřadnice 5, 5 mm).
- **Tažení z palety** přímo na plátno - během tažení vidíte náhled a zvýrazní se cílová sekce.
- **Tažení databázového pole** z panelu „Datová pole" - tažení do prázdna vytvoří nový textový
  prvek s `{POLE}`; tažení na existující text pole připojí k jeho obsahu. Dvojklik na pole
  funguje jako alternativa k tažení.

### Výběr, přesun, velikost
- Klik vybere prvek, tažení ho přesune, úchyty na okrajích mění velikost.
- Velikost jde měnit i klávesnicí: **Shift+šipky** (mění se pravá a dolní hrana, levý horní roh
  zůstává na místě).
- **Když se prvky překrývají, klik patří tomu, který je vidět navrchu.** Prvek, který máte vybraný,
  si klik i tažení podrží, i kdyby ho jiný překrýval - dá se tedy odtáhnout zpod něj, když si ho
  vyberete ve stromu šablony.
- Výběr více prvků: tažení rámečku (rubber band), nebo Ctrl+klik.
- Prvek se nevejde mimo hranice své sekce - automatické oříznutí.
- Klik bez pohybu myší nespustí přesun - teprve po překročení malého prahu (pár pixelů) se prvek
  začne hýbat, takže omylem nedojde k drobnému posunu při pouhé selekci.

### Seskupování
Ctrl+G spojí vybrané prvky do skupiny; Ctrl+Shift+G skupinu zruší. Klik vybere celou skupinu,
Alt+klik jen jeden prvek uvnitř. Skupina má vizuální odznak a ohraničující rámeček, ve stromu se
zobrazí jako samostatná úroveň. Import z PDF prvky automaticky seskupuje.

---

## 6. Editační nástroje

### Přichytávání
Tři nezávislé vrstvy, ovládané z lišty nástrojů:
- **Mřížka** - zapínatelná tlačítkem „M", velikost 0,2–10 mm (výchozí **1 mm**, přichytávání
  na mřížku je nově výchozí **zapnuté** - jemný krok drží pozice čisté, aniž by omezoval).
- **Přichytávání k objektům** - tlačítko „O" (výchozí zapnuto); prvek se přichytává k hranám a
  středům jiných prvků i k okrajům sekce, s vizuálními vodítky.
- **Mezera pro přichytávání** - 0–10 mm (výchozí 2 mm); prvek se umí přichytit
  i v definované mezeře od souseda, se zobrazením indikátoru mezery.

### Duplikování
- **Ctrl+D** bez směru - kopie s malým posunem (+5 mm v obou osách).
- **Ctrl+D + šipka** - kopie umístěná ve zvoleném směru s mezerou rovnou aktuálnímu nastavení
  mezery pro přichytávání (výchozí 2 mm).
- Duplikování neovlivňuje schránku (clipboard).

### Kopírování stylu
**Ctrl+Shift+C** zkopíruje styl (font, rámeček, pozadí) z prvního vybraného prvku, **Ctrl+Shift+V**
ho aplikuje na vybrané prvky.

### Hromadné úpravy
Výběr více prvků + **Enter** otevře dialog společných vlastností: Barva rámečku, Tloušťka rámečku,
Pořadí vrstvy (ZIndex), Uzamčeno - změna se aplikuje na všechny vybrané najednou.

### Normalizace pozic
**Ctrl+Shift+N** - dialog s volbou kroku zaokrouhlení (0,1 / 0,5 / 1 / 2 / 5 mm) a živým náhledem
počtu prvků, které se změnou upraví. Opravuje drobné nepřesnosti pozic vzniklé např. přesným
tažením myší.

### Škálování šablony
Rozbalovací nabídka v liště nástrojů (50–200 %) nebo menu `Úpravy → Škálovat šablonu` (50/75/90/100/110/125/150/
200 %). Přepočítá rozměr stránky, okraje, výšky sekcí, pozice a rozměry prvků i jejich
font/padding vlastnosti - proporcionálně zvětší nebo zmenší celou šablonu najednou.

### Vrátit / opakovat akci
**Ctrl+Z** / **Ctrl+Y** - každá akce (přesun, změna vlastnosti, přidání/smazání prvku či sekce…)
je vratná.

---

## 7. Vlastnosti a styly

Vybraný prvek nastavíte v záložce **Vlastnosti** levého panelu (okamžitá editace) nebo v dialogu
po stisku **Enter** či dvojkliku na prvek - obsah je stejný, panel je jen rychlejší cesta.

### Výchozí písmo celé šablony
V **Vlastnostech stránky** (Ctrl+Shift+P) lze nastavit **Výchozí font** - písmo, které zdědí
všechny prvky bez vlastního nastavení. Je to kořen kaskády stylů; prázdné pole = vestavěné
výchozí písmo.

### Rámeček a pozadí
Každý prvek může mít rámeček (barva + tloušťka) a barvu pozadí. Když dva prvky sdílejí hranu,
vykreslí se jako **jeden** společný rámeček, ne zdvojeně.

**Vymazané barevné pole znamená „nekreslit nic".** Platí to pro podbarvení - u záhlaví i u buněk.
Barvu tedy jde odebrat tak, že obsah pole smažete, a zůstane to tak i po uložení šablony.

**Do číselných polí (rozměry, velikosti, odsazení) jde psát desetinnou čárku i tečku** - „1,5"
znamená totéž jako „1.5". Ukládá se to s tečkou.

### Automatické zmenšení textu
Když se text nevejde, písmo se automaticky zmenší tak, aby se vešlo - obdoba funkce „přizpůsobit
buňce" v Excelu. Je to vestavěné chování, nemusíte nic zapínat:

- **Buňky tabulek** (dlouhý název zastávky, výčet zón…) - zmenší se na šířku buňky.
- **Prvky jízdního řádu** s pevnou výškou - písmo se zmenší, aby se obsah vešel do výšky.
- **Textový prvek** s pevnou výškou - **bez zalamování** (nově výchozí stav) se zmenší na šířku,
  **se zalamováním** text nejdřív pokračuje na dalším řádku a zmenšuje se teprve, až mu nezbývá
  místo na výšku. Tady je **dolní mez**: na polovinu nastavené velikosti, nikdy pod 4 pt.
  Delší text se pod mezí ořízne a stavový řádek to řekne, ať víte, že máte text zkrátit
  nebo prvek zvětšit.

Prvek s **prázdnou výškou** se nezmenšuje - ten si místo doroste podle obsahu.

Zmenšení není tiché: stavový řádek a souhrn po tisku vypíšou, kterému prvku se písmo zmenšilo
a na kolik procent („Poznámky: písmo zmenšeno na 42 %"). Bez toho byste to poznali až z papíru -
a u vývěsky se stránkou na zastávku vyjde každá jinak podle toho, kolik má poznámek.

### Kaskáda stylů u tabulkových prvků
U tabulek a specializovaných prvků jízdního řádu funguje **kaskáda stylů**: nejčastěji měněné
barvy (pozadí a text záhlaví/dat) nastavíte rychle přímo v panelu Vlastnosti; detailní styl po
jednotlivých sloupcích (font, velikost, řez, zarovnání, odsazení, dekorace) se nastavuje
v dialogu sloupců.

- Co v dialogu sloupce nenastavíte, se **zdědí** z nastavení celého prvku - v dialogu se to
  zobrazí jako šedý kurzívou psaný náznak zděděné hodnoty.
- Pokud hodnotu explicitně přepíšete, objeví se u pole tlačítko „×" pro rychlý návrat ke
  zděděné hodnotě.
- Tlačítka „Resetovat styl" (jeden sloupec) a „Resetovat všechny styly" v dialogu vrátí
  nastavení do výchozího stavu.

---

## 8. Datová pole a výrazový jazyk

### Zástupné symboly (databázová pole)
Kdekoli v textu napíšete pole ve složených závorkách, např. `Linka {CISLO_LINKY}`. Při tisku se
`{CISLO_LINKY}` nahradí skutečnou hodnotou. Dostupná pole pro aktuální agendu vidíte v panelu
„Datová pole" a můžete je přetáhnout přímo na plátno.

### Pole stránky
Ve skupině **Stránka** panelu „Datová pole" jsou hodnoty, které nejsou v datech agendy, ale doplní
je sazba pro každou stránku zvlášť:

- `{STRANA}` - číslo stránky, `{POCET_STRAN}` - počet stran,
- `{POKRACOVANI}` - nápis jako ve starém tisku: na první stránce směru tam nic, na dalších
  „pokračování", na první stránce směru zpět „opačný směr", na dalších „opačný směr - pokračování".
  Slovenská šablona je vypíše slovensky.

⚠️ Pole stránky patří do **sekce, která se tiskne na každé stránce** (hlavička nebo patička stránky).
V sekci obsahu, která se tiskne jednou, by ukázala jen hodnotu první stránky. Vzhled nápisu (pozadí,
rámeček, písmo) nastavíte textovému prvku. Pokud nechcete na první stránce prázdný podbarvený pruh,
dejte prvku podmínku viditelnosti `{STRANA} > 1 || {SMER} == "Z"`.

Starší zápisy `{PAGE}` a `{TOTAL_PAGES}` fungují dál.

Název pole nemusíte psát ručně - u polí, kam se data dosazují, je vpravo tlačítko `{ }`. Otevře
seznam polí agendy, ve kterém lze hledat, a vybrané pole vloží **na místo, kde máte kurzor**.
Hodí se i doprostřed rozepsané věty. Najdete ho u obsahu textového prvku a čárového kódu,
u podmínky viditelnosti prvku i sekce, u viditelnosti řádku tabulek a u názvu souboru při exportu.

### Formátování hodnot
V zástupném symbolu lze hodnotu naformátovat zápisem `{POLE:FORMÁT}`:

- `UPPER` - velká písmena
- `LOWER` - malá písmena
- `TRIM` - oříznutí mezer
- `LEFT(n)` - prvních n znaků
- `RIGHT(n)` - posledních n znaků
- `PADLEFT(n)` / `PADLEFT(n,'znak')` - doplnění zleva na délku n (výchozí mezerou)
- `PADRIGHT(n)` / `PADRIGHT(n,'znak')` - doplnění zprava na délku n
- `N2`, `C2`, `P1` - standardní číselné formáty (2 des. místa, měna, procenta…)
- `dd.MM.yyyy`, `HH:mm` - formát data / času

Formátování čísel a datumů respektuje **jazyk nastavený v šabloně** (viz níže), ne systémové
nastavení Windows dané stanice. Chybný formát se tiše ignoruje - vypíše se nezformátovaná
hodnota, tisk tedy kvůli překlepu ve formátu nespadne.

### Zalomení řádku v textu (`\n`) - víceřádkové záhlaví
Text prvku i buňky/záhlaví tabulky může mít **víc řádků**. Nový řádek zapíšete dvěma způsoby:

- napíšete `\n` (zpětné lomítko + písmeno n) přímo do textu, např. `Zastávka\nStation`,
- nebo stisknete **Enter** v příslušném poli.

Typické využití je **dvouřádkové (dvojjazyčné) záhlaví sloupce** - první řádek česky, druhý
cizojazyčně (např. „Zastávka" / „Station"). U tabulkových prvků se **výška řádku záhlaví
automaticky přizpůsobí** počtu řádků, takže se text vždy vejde. Funguje v textových prvcích
i v záhlaví a buňkách tabulek a specializovaných prvků jízdního řádu.

### Podmíněná viditelnost
Prvek nebo sekci lze zobrazit jen za podmínky - pole **Podmínka viditelnosti**, např.:
- `{POCET} > 0` - zobrazit jen když je počet kladný,
- `{STRANA} > 1` - jen od druhé stránky.

Podporované operátory: `&&` (a zároveň), `||` (nebo), `!` (negace), `==`, `!=`, `>`, `<`, `>=`,
`<=`, závorky, textové literály, čísla, `true`/`false`. Prázdný nebo neplatný výraz znamená
„vždy zobrazit" (bezpečné chování) - pokud je zápis chybný, zobrazí se u pole červené
upozornění, ať víte, že to potřebuje opravit.

### Agregace v tabulkách
V obsahu buňky dynamické tabulky lze počítat souhrny: `SUM({POLE})`, `COUNT()`, `AVG({POLE})`,
`MIN({POLE})`, `MAX({POLE})`.

### Jazyk šablony (lokalizace)
Šablona má nastavený **jazyk** - dialog `Vlastnosti stránky…` (Ctrl+Shift+P), pole „Formát čísel
a datumů": Čeština (cs-CZ), Slovenština (sk-SK), Angličtina (en-US), Němčina (de-DE). Formát
čísel a datumů se řídí tímto nastavením nezávisle na tom, na jakém počítači se tiskne - tisk tak
vypadá stejně na každé stanici.

U jízdních řádů se tímtéž nastavením řídí i **jazyk vysvětlivek v legendě** (u slovenštiny tedy
„premáva len", „na znamenie") a **znaková sada symbolů**. Nepřekládá se to, co jste napsali sami -
poznámky zadané u linky, názvy zastávek ani vlastní nadpisy sloupců dnů zůstávají tak, jak jsou
uložené.

---

## 9. Náhled, export a tisk

### Živý náhled
Pravý panel ukazuje **výsledný tisk** - stejným jádrem, jaké se použije při reálném tisku.
Na rozdíl od plátna respektuje podmíněnou viditelnost, takže vidíte přesně to, co se vytiskne.

Dokud nemáte vybranou linku, kreslí náhled **ukázková data** - štítek linky ve stavovém řádku to
říká (*Linka: - (ukázková data, klikněte pro výběr)*). S vybranou linkou se nad náhledem objeví pruh
pro procházení stránek dat, takže je vždy poznat, na co se koukáte. Pruh se neukáže, když by
přepnutí nic nezměnilo - šablona nemá prvek plněný z dat ani pole, které se mezi stránkami dat liší.

### Vícestránkové sestavy
Stránkování řeší systém automaticky: hlavičky/patičky se opakují podle nastaveného chování sekcí,
dlouhé tabulky se dělí přes stránky, v náhledu jsou stránky zobrazené pod sebou se stíny a
rámečky.

### Procházení stránek dat
Nad náhledem je pruh pro přepínání mezi jednotlivými **stránkami dat** (u zastávkového JŘ např.
zastávka × směr): šipky ◀ / ▶, rozbalovací seznam pro přímý skok na konkrétní stránku a počítadlo
„/ N". Rychle tak proklikáte, co se reálně vytiskne pro jednotlivé vybrané instance.

### Hlášky a kontrola před tiskem
Vpravo ve stavovém řádku je **blok hlášek**: počty hlášek podle závažnosti. Klik na blok rozbalí
seznam, klik na řádek seznamu vybere prvek, kterého se hláška týká. Každá hláška má tvar
*kde · co se stane na papíře → rada*, např. „Poznámky · písmo zmenšeno na 85 %".

Hlášky mají tři úrovně a žádná tisk nezastaví:

- **Informace** (šedé kolečko) - jen oznámení, že návrhář něco upravil, např. zmenšil písmo, aby se obsah
  vešel. Není třeba nic dělat.
- **Upozornění** (trojúhelník) - výsledek se může lišit od očekávání, např. sloupec, jehož období leží
  mimo platnost jízdního řádu, zůstane prázdný. Doporučujeme zkontrolovat.
- **Problém** (červené kolečko) - část obsahu se na papír nedostane, např. text se ořízne nebo sekce se
  nevejde na stránku. Stránka má v náhledu navíc červený rámeček. Rada u hlášky říká, co upravit.

Náhled a blok hlášek se týkají jen linky, která je právě v náhledu. Tlačítkem **„Zkontrolovat"**
v okně Tisk, lupou na liště nebo `Soubor → Zkontrolovat vybrané linky…` projdete **všechny vybrané
linky** bez tisku. Okno kontroly ukazuje průběh a klik na hlášku přepne na její linku a vybere
prvek. Stejný souhrn se ukáže i po vytvoření PDF.

Tentýž prvek na víc stránkách je **jedna hláška**, ne výpis po stránkách. U sekcí nastavených na
„Rozdělit přes stránky" se přetečení nehlásí - sekce se rozdělí a nic se neztratí.

### Tisk
`Soubor → Tisk…` (Ctrl+P) otevře dialog „Tisk šablony" s parametry, které se generují dynamicky
podle zvolené agendy (bez agendy: „Šablona nemá žádné parametry."). Dialog obsahuje i výběr
instancí (např. linek) k tisku. PDF i PNG vzniká jen tady, jedním tlačítkem **„Vytvořit PDF"**
(„Vytvořit PNG") vedle volby **Formát**: z vybraných linek, a když žádná vybraná není, z ukázkových
dat. PNG je obrázek za každou stránku - neskládá se na list a nečlení do souborů, takže se tyto
volby u něj schovají.

Nastavení výstupu je univerzální napříč agendami:
- **Členění výstupu** - Vše do jednoho PDF / Jeden PDF na instanci / (u agend s více úrovní dat
  i jemnější dělení, viz dokument dané agendy).
- **Měřítko tisku** - hotová sestava se před tiskem celá zvětší nebo zmenší (1 = skutečná
  velikost); počet stránek se nemění. Stavový řádek okna řekne, jaký rozměr papíru z toho vyjde.
- **Název souboru (tokeny {POLE})** - vlastní kombinace datových polí pro pojmenování
  výsledných souborů; při rozdělení výstupu na víc souborů se vybírá cílová složka, kolize jmen
  se řeší automaticky příponou `_2`, `_3`…
- **Uspořádání na list** (panel pod formulářem) - víc stránek na jeden papír, který pak
  rozříznete; jinak ho nechte vypnuté. Panel je vidět jen u šablony o víc stránkách;
  jednostránková šablona se tiskne vždy stránka na list. Zvolíte jen **papír** - kolik stránek se na něj vejde bez
  zmenšování, spočítá návrhář sám (A5 na výšku: na A4 na šířku dvě, na A3 na výšku čtyři).
  Stránky jdou na místa po řadě. U šablony o víc stránkách se k tomu volí, kam jde **druhá stránka
  dokladu**:
  - **Na stejnou stranu listu, vedle první** - např. první stránka faktury vlevo a druhá vpravo,
  - **Na zadní stranu listu, za první** - na přední stranu přijdou první stránky dokladů, na zadní
    jejich druhé stránky, po rozříznutí má každý kus obě strany (např. dva doklady A5 na A4). Tiskněte
    oboustranně s převracením na delší straně.
  Nákres pod volbami ukazuje, co kam padne. Kdo měl z dřívějška uloženou volbu „Kopie přes celý
  list" nebo zapnuté ořezové značky, uvidí je v panelu i dál. Uspořádání si návrhář
  pamatuje sám - příště nabídne poslední použité u agendy.
  (Brožurové řazení zůstává na tiskárně.)


---

## 10. Import z PDF

`Nástroje → Import PDF…` - z existujícího PDF vytvoří editovatelnou šablonu. Dialog je záměrně
jednoduchý: vyberete **PDF soubor** (a u vícestránkového stránku) - všechno ostatní se odvodí
z právě otevřené agendy:

- **Agenda jízdního řádu** → import s **umělou inteligencí**: AI rozpozná strukturu předlohy
  a rovnou namapuje datová pole agendy - výsledkem je živá šablona napojená na data, ne statická
  kopie. Importuje se vždy **jedna stránka**. Vyžaduje aktivovanou AI službu u M-line; bez ní je
  import u JŘ agend nedostupný.
  - **Zastávkový JŘ** - seznam zastávek, minuty odjezdů, poznámky, texty a loga; šablona je jedna
    pevná stránka jako vývěska.
  - **Linkový JŘ** - Tabulka spojů (pořadí popisných sloupců a písmo podle předlohy), poznámky
    a texty kolem. Šablona má rozvržení jako výchozí šablony linkového JŘ: **hlavičku na každé
    stránce**, pod ní tabulku, která pokračuje přes stránky, a poznámky pod ní nebo vedle ní -
    podle předlohy. Poznámky jsou v šabloně vždy, i když na importované stránce nejsou; na další
    stránku je tisk přesune sám, když se nevejdou. Má-li předloha pod sebou oba směry, převezme
    se horní tabulka; druhý směr sestava vytiskne na další stránce. Nastavení sestavy (obousměrný
    tisk, stránka pro každou zastávku…) se z předlohy nepřebírá - nastavte ho v okně tisku.
    Barvy tabulky a poznámek se převezmou z předlohy. Grafiku pod tabulkou (loga) nahradí prázdné
    obrázky o její velikosti, stačí do nich nahrát soubor; co je u spodku stránky, skončí
    v patičce na každé stránce.
- **Jiná agenda (Obecná, Faktury…)** → obecný import: texty, čáry a obrázky se převezmou 1:1
  jako prvky šablony (bez AI).

**Berte to jako rychlý start, ne záruku přesné shody s originálem** - výsledek zkontrolujte
a dolaďte; AI rozpoznání se může u netypických rozvržení splést (import lze spustit znovu).

---

## 11. Uložená nastavení

Následující se ukládá centrálně (podle firmy a přihlášeného uživatele), ne jen lokálně na
stanici - po přihlášení na jiném počítači je nastavení stejné:

- **Naposledy otevřené soubory** (import JSON) - až 10 položek, submenu „Otevřít poslední JSON".
- **Naposledy použité barvy** - až 8 položek, sdílené napříč všemi barevnými výběry v aplikaci.
- **Nastavení editoru** - velikost mřížky, mezera pro přichytávání, zapnuté přichytávání,
  viditelnost panelů, zoom, aktivní záložka levého panelu - obnoví se při příštím spuštění.
- **Výchozí hodnoty instance** - naposledy zvolené parametry/instance se pamatují per šablona a
  předvyplní se při dalším otevření stejné šablony.
- **Parametry tisku** - pamatují se per agenda (sdílené napříč šablonami stejné agendy).

Ukládání běží na pozadí; případná chyba uložení se uživateli aktivně nehlásí (nejde o kritickou
operaci).

---

## 12. Klávesové zkratky

- **Ctrl+N** - Nová šablona
- **Ctrl+O** - Otevřít z databáze
- **Ctrl+S** - Uložit (do databáze)
- **Ctrl+Z / Ctrl+Y** - Zpět / Vpřed
- **Ctrl+X / Ctrl+C / Ctrl+V** - Vyjmout / Kopírovat / Vložit
- **Ctrl+D** - Duplikovat (kopie s malým posunem)
- **Ctrl+D + šipka** - Duplikovat vedle (s mezerou)
- **Šipky** - Posun prvku o 1 mm
- **Ctrl+šipky** - Posun o velikost mřížky (podle volby v liště)
- **Shift+šipky** - Změna velikosti prvku o 1 mm; mění se pravá a dolní hrana, levý horní roh
  zůstává na místě. U prvku, jehož výšku počítá engine z dat, jde měnit jen šířka
- **Ctrl+Shift+C / Ctrl+Shift+V** - Kopírovat / Vložit styl
- **Delete** - Smazat výběr
- **Ctrl+A** - Vybrat vše v aktivní sekci
- **Enter / dvojklik** - Vlastnosti výběru (1 prvek = dialog; víc prvků = dialog společných vlastností)
- **F2** - Rychlá editace textu (textový prvek), nebo vstup do buňky pevné tabulky
- **Alt+klik na buňku** - Vstup do pevné tabulky rovnou do té buňky
- **Alt+Enter (v buňce)** - Vlastnosti buňky (styl, formát hodnoty)
- **Ctrl+B / Ctrl+I / Ctrl+U (v buňce)** - Tučné / kurzíva / podtržení buňky
- **Ctrl+G / Ctrl+Shift+G** - Seskupit / Rozeskupit
- **Alt+klik** - Vybrat jeden prvek ze skupiny
- **Ctrl+Shift+H / Ctrl+Shift+J** - Rozdělit prvek vodorovně / svisle
- **Ctrl+Shift+N** - Normalizovat pozice
- **Ctrl+PageUp / Ctrl+PageDown** - Vrstva dopředu / dozadu (+Shift = zcela na kraj)
- **Ctrl+Shift+↑/↓** - Přesun aktivní sekce nahoru / dolů
- **Ctrl+0** - Přizpůsobit zobrazení stránce („Fit")
- **Ctrl+1** - Skutečná velikost 100 % („1:1")
- **Ctrl + kolečko myši** - Plynulý zoom (k pozici kurzoru)
- **Ctrl+B** - Sbalit/rozbalit levý panel (uvnitř tabulky ztuční buňku)
- **Ctrl+P** - Tisk
- **Ctrl+Shift+P** - Vlastnosti stránky
- **Ctrl+Shift+A** - Změnit agendu
- **Ctrl+Shift+I** - Výběr instancí (linek)
- **F5** - Obnovit data z databáze
- **F1** - Nápověda
- **Escape** - Zrušit probíhající operaci (např. přidávání prvku)

Poznámka: „Uložit jako" (do databáze) je dostupné jen z menu `Soubor → Uložit do databáze jako…`,
bez vlastní klávesové zkratky.

---


# ČÁST II - Agendy

> Tato část popisuje jednotlivé agendy - pro každou její specifika a to, jaká data nabízí.
> Obecné ovládání (šablony, sekce, prvky, styly, export) je v části I a tady se neopakuje.
> Nyní jsou popsané **zastávkový** a **linkový jízdní řád**; další agendy (relace…) budeme
> přidávat jako samostatné kapitoly.

---

## Zastávkový jízdní řád

Vývěska na zastávce: seznam odjezdů z jedné zastávky v jednom směru. Šablona se skládá ze tří
specializovaných prvků, které samy umí velkou část logiky JŘ - vy nastavujete hlavně vzhled
(sloupce, barvy, fonty), systém dopočítá obsah.

### Obsah (zastávkový jízdní řád)

1. [Dva zdroje dat](#1-dva-zdroje-dat)
2. [Tři specializované prvky](#2-tři-specializované-prvky)
3. [Editory sloupců](#3-editory-sloupců)
4. [Výběr linek k tisku](#4-výběr-linek-k-tisku)
5. [Tisk a export](#5-tisk-a-export)
6. [Auto-přizpůsobení a přetečení](#6-auto-přizpůsobení-a-přetečení)
7. [Postup krok za krokem](#7-postup-krok-za-krokem)

---


### 1. Dva zdroje dat

Šablona zastávkového JŘ může čerpat data ze dvou různých agend. Obě produkují stejný typ dat
(zastávky, skupiny dnů, odjezdy, poznámky), takže tytéž tři prvky fungují nad oběma - liší se jen
zdroj a pár doplňkových nastavení.

- **Zastávkový JŘ MHD** - modul MHD
- **Zastávkový JŘ AD** - hlavní modul Jízdní řády

Mezi agendami se přepíná stejně jako mezi jakýmikoli jinými - `Nástroje → Změnit agendu…`
(Ctrl+Shift+A) nebo klik na zelený štítek. Přepnutí založí novou prázdnou šablonu (viz část I, kap. 3).

#### Co je navíc jen u varianty AD

- **Automatické rozvržení sloupců (skupin dnů)** - podle konfigurace linky systém sám zvolí
  jedno ze čtyř možných rozvržení: Pracovní / Sobota / Neděle+svátky (3 sloupce); Pracovní mimo
  prázdniny / Pracovní o prázdninách / Sobota+Neděle+svátky (3 sloupce); Pracovní /
  Sobota+Neděle+svátky (2 sloupce); Pracovní mimo prázd. / Pracovní o prázd. / Sobota /
  Neděle+svátky (4 sloupce).
- **Parametr „Rozdělení sloupců (typy provozu)"** (záložka Parametry / tiskový dialog) - je to
  **pohled na definici sloupců uložených v šabloně a zároveň její ovladač**: ukazuje název vzoru,
  kterému vaše definice odpovídá, „Automaticky (dle linky)", když žádnou nemáte, nebo „Vlastní
  sloupce", když jste si ji poskládali po svém. Přepnutím parametru se definice v šabloně přepíše
  na zvolený vzor. Vzory: Pracovní-Sobota-Neděle+svátky / Pracovní-víkend / Prázdniny (mimo+o
  prázdninách)-víkend / Prázdniny (mimo+o prázdninách)-Sobota-Neděle+svátky.
- **Varianty tras** (sloupec „Varianty tras" na prvku Tabulka zastávek - zapíná se v jeho nastavení
  na záložce Sloupce; **z výroby je vypnutý**) - když spoje jedou z dané zastávky různými trasami,
  dostane každá trasa vlastní časový sloupec. Varianta = jedna trasa (sled obsluhovaných zastávek);
  v jejím sloupci je **průběh jedné skutečné trasy** - té s delší jízdní dobou, takže údaj odpovídá
  spoji, který opravdu jede. Počet sloupců je dynamický (tolik, kolik je tras v datech), takže
  **jedna položka v seznamu sloupců se v tisku rozpadne na víc sloupců**; šířka a styl, které u ní
  nastavíte, platí pro každý z nich. **Sloupec dostane i linka s jedinou variantou** - stejná trasa
  všech spojů je taky varianta, takže vývěska postavená na variantách vypadá všude stejně.
  Řazení a písmenné označení variant se nastavuje parametry sestavy (viz níže).
  ⚠️ Sloupce „Varianty tras" a „Čas jízdy" ukazují **týž údaj dvěma způsoby**, takže zapnutý má být
  vždy jen jeden z nich. Proto je variantní z výroby vypnutý; ve vašich hotových šablonách zůstává
  tak, jak jste si ho nastavili.
- **CIS názvy** - alternativní, zkrácený název zastávky/směru z centrálního číselníku (CIS) vedle
  plného lokálního názvu.
- **Dopravce vč. adresy** - pokud pro dopravce není nastavený zkrácený název, vypíše se plný tvar
  „obchodní název, sídlo provozovny, tel: číslo". Tohle je pole **Popis dopravce**, které slepí
  všechno dohromady.
- **Údaje dopravce po jednotlivých polích** - *Název dopravce*, *Adresa dopravce*, *Telefon
  dopravce*, *E-mail dopravce* a *IČO dopravce*. Použijte je, když chcete o obsahu rozhodovat sami:
  poskládáte si jen to, co na vývěsce chcete, a nic se neopakuje dvakrát. Adresa je jedno pole
  včetně PSČ a obce - tak je uložená v číselníku dopravců. U linky s více dopravci nesou tato pole
  **prvního** z nich.
- **Varianty tras - parametry sestavy** (skupina „Seznam zastávek"):
  - **Řazení variant tras** - *Dle četnosti ze zastávky* (výchozí; nejčastější trasa z dané
    zastávky je vždy první) nebo *Stabilní (pořadí spojů)* (stejné pořadí sloupců napříč stránkami).
  - **Písmena variant tras** - varianty se označí písmeny v záhlaví sloupců a totéž písmeno se
    vytiskne u minut odjezdů; podle písmena tak dohledáte, kterou trasou spoj jede. Výchozí stav
    je zapnuto. Vypnutím zmizí písmena **v záhlaví i u minut** naráz a sloupce s časy zůstanou;
    vypínejte je jen tam, kde varianty vysvětlujete jinak, protože jinak se u minuty nedá poznat,
    který sloupec časů pro ni platí. Bez zapnutého sloupce „Varianty tras" se u minut netisknou
    vůbec, protože by je nebylo čím vysvětlit. Písmen **rozdílů tras** se to netýká.
  - **Popisek celého bloku místo písmen** - v editoru sloupců zadejte položce „Varianty tras"
    záhlaví. S vypnutými písmeny se vytiskne jako jeden společný popisek přes všechny variantní
    sloupce (na střed), ne jako tentýž text v každém z nich. Se zapnutými písmeny se nepoužije.
  - **Písmena variant a písmena rozdílů tras jsou dvě různé věci a tisknou se vedle sebe** -
    u minuty je nejdřív rozdíl trasy, pak varianta („17BA" = rozdíl B, varianta A).
    Rozdíl trasy říká **kudy** spoj jede jinak a vysvětluje ho text v legendě; varianta říká,
    **podle kterého sloupce časů** se řídí, a vysvětluje ji záhlaví toho sloupce.
  - **Která písmena se použijí, si určíte u linky** (EDISON → linka → Nastavení k tisku → záložka
    Odjezdník MHD). „Volné značky" jsou písmena rozdílů tras, „Volné značky pro minutové trasy
    spojů" písmena variant; bere se z nich znak po znaku od začátku a po vyčerpání se zdvojují
    („AA", „DD"). Nechte-li pole prázdné, použijí se **výchozí sady, které se nepřekrývají** -
    varianty `A, D, L, M, N, P, T, Y`, rozdílům tras zůstává jejich dosavadní sada.
    ⚠️ Vlastní zápis se bere doslova: napíšete-li do obou polí totéž písmeno, vytiskne se
    u minuty pro obě věci a legenda k němu bude mít jiný text. Řešení je přesunout jednu ze sad,
    protože nastavitelné jsou obě.
  - **První varianta bez písmene** - nejčastější trasa zůstane bez písmene, odchylky se značí od A.
    Vypnuto = písmenkuje se už od první varianty.
  - **Nejvýš sloupců variant tras** - kolik sloupců variant se nejvýš vytiskne; prázdné = všechny.
    Vezmou se první podle řazení variant. Spoje ostatních variant pak ve sloupcích nemají svou
    trasu ani písmeno varianty - seznam zastávek se zúží za cenu této informace.
  - **Slučovat varianty, které jedou jen část jiné** - varianta jedoucí **souvislý úsek** jiné
    trasy (od - do) dostane sloupec té delší. Výchozí vypnuto. Hodí se na lince, kde část spojů
    jezdí jen kus trasy a sloupců je moc.
    ⚠️ **Varianta, která uprostřed zajíždí jinam a část zastávek vynechá, si sloupec nechá** -
    jinak by z vývěsky zmizelo, že tam spoj nejede.
    ⚠️ Slití něco stojí: u slitých spojů jsou **časy té delší varianty**. U dvou variant se shodnými
    časy to nevadí, u variant s jinými jízdními dobami se minuty posunou. Zapíná se to na celou
    sestavu, ne po jednotlivých variantách - před zapnutím si vývěsku prohlédněte.
- **Samostatná stránka pro každý označník** (parametr „Seznam zastávek") - zastávka obsluhovaná
  z více označníků se vytiskne jako **samostatná vývěska pro každý označník**, vždy jen s odjezdy
  spojů, které z daného sloupku jedou; číslo označníku je i v titulku stránky. Hodí se, když
  vývěsku fyzicky věšíte na konkrétní sloupek. Výchozí vypnuto (jedna vývěska se všemi odjezdy).
- Doplňkové tiskové parametry: **„V rozsahu nepoznámkovat schovat"** (Ruční komentáře /
  Automatické rozdíly tras / Obojí) a **„Skrýt souhrn 'všechny spoje bezbariérové'"**.

---

### 2. Tři specializované prvky

#### Tabulka zastávek

Levý sloupec sestavy - seznam zastávek s časy a atributy. Do seznamu se dostane jen zastávka, kde
staví **aspoň jeden spoj, který staví i na té vývěsce** - zastávka, kam odsud nic nejede, by měla
u všech časů jen „<" a cestujícímu by neřekla nic.

- **Upravit sloupce…** - otevře editor sloupců (kap. 3)
- **Varianty tras** (sloupec v editoru sloupců, výchozí vypnutý) - zap. = časové sloupce podle skutečných variant tras (jen AD; viz „Varianty tras" níže). Je to **týž údaj jako sloupec „Čas"**, takže zapnuté má být vždy jen jedno z toho: kdo chce varianty, vypne si „Čas". Linka, kde jedou všechny spoje stejnou trasou, dostane jeden variantní sloupec (dřív žádný, takže na takové lince nebylo vidět nic)
- **Oddělovací čáry** - vodorovné čáry mezi řádky zastávek
- **Odsazení** - odsadí název zastávky mezerami podle jejího odsazení v trase (vizuální hierarchie)
- **Sloupec Označník** - volitelný sloupec s číslem označníku (sloupku, ze kterého spoj odjíždí); zapíná se v editoru sloupců, tiskne se jen tam, kde jsou data vyplněná
- **Více označníků** - jak sloupec Označník naloží se zastávkou, ze které různé spoje odjíždějí z různých sloupků: **Z trasy linky** (výchozí, číslo z trasy) / **Nejčetnější dle spojů** / **Výčet všech** (např. „2, 3") / **Řádek pro každý označník** (zastávku vypíše jako víc řádků, jeden na každý sloupek, s časy jen variant, které z něj jedou)
- **Na znamení + Symbol na znamení** - zobrazí značku „na znamení" u zastávky a definuje, jaký znak se použije (výchozí „×")
- **Atributy** - zobrazí atributy zastávky vedle názvu
- **Přestupy (vlak/autobus)** - zobrazí u zastávky symbol přestupu na vlak („L") / autobus („A"); výchozí vypnuto
- **Poznámky zastávky** - zobrazí poznámkové značky u zastávky
- **Jízda pod minutu jako <1** - jede-li se na následující zastávku pod minutu, vypíše se ve sloupci časů „<1" místo prázdna; výchozí vypnuto. Zastávka, kterou spoj neobsluhuje, zůstane prázdná
  Znak „<" se přitom vysune doleva, aby číslice zůstaly ve svislici s ostatními časy (při zarovnání sloupce vpravo se srovnají samy, tam se nic nevysouvá)
- **Nastavení sloupce Čas se dědí na varianty** - když má linka víc variant tras a sloupec se rozpadne na víc časových sloupců, platí pro všechny zarovnání i styl (font, barvy, okraje) nastavený u sloupce „Čas"
- **Pozice značek** - Za zastávkou (výchozí) / Před zastávkou
- **Šipka směru + Pozice šipky + Barva šipky + Tučná šipka** - grafická šipka ukazující směr jízdy od aktuální zastávky dál po trase. Při pozici vlevo má šipka vyhrazený vlastní pruh, takže se ani u úzkého prvku nepřekryje s jízdními dobami
- **Záhlaví sloupců** - zap/vyp řádek záhlaví
- **Podtržení aktuální / Tučně aktuální / Velikost aktuální / Pozadí aktuální / Barva textu aktuální** - zvýraznění řádku aktuální zastávky (vlastní velikost písma, podbarvení přes celou šířku, vlastní barva písma; prázdná barva = stejná jako ostatní řádky)
- **Projeté zastávky + Barva projetých** - zda tisknout i zastávky, které vozidlo od aktuální zastávky již projelo (ztlumeně)
- **Čas u projetých** (*Nezobrazovat* / *Zobrazit*) - jízdní doby se počítají od vývěskové zastávky,
  takže u projetých vycházejí záporné. *Nezobrazovat* vyprázdní u projetých zastávek **celou buňku**
  časového sloupce, tedy i značku „<" (spoj tudy projíždí); dřív zmizelo jen číslo, takže sloupec
  „Čas" byl prázdný a variantní sloupec vedle něj plný „<". Na neprojetých zastávkách zůstává „<"
  beze změny - tam je to platná informace.

#### Minuty odjezdů

Mřížka minut po skupinách dnů (sloupce = pracovní den / sobota / neděle…).

- **Odjezdy** - Upravit sloupce…, Záhlaví skupin, Velikost záhlaví, Min. výška řádku záhlaví, Zarovnání záhlaví, Šířka sloupce hodin, Rozestup odjezdů (Bez zarovnání / Zarovnat dle nejširšího odjezdu / Zarovnat na pevnou šířku - viz níže), Hodiny u každé skupiny (sloupec hodin se opakuje u každé skupiny dnů), Obrázek v záhlaví hodin (vykreslí se v záhlaví hodinového sloupce u každé skupiny dnů; je-li nastavený, nadpis sloupce se netiskne), Max. skupin (0 = bez limitu), Šířka minuty, Oddělovač skupin
- **Časová osa** - Jen hodiny s odjezdy / Souvislá od první do poslední / Vlastní rozsah (+ pole Hodiny od/do); Roztáhnout na výšku - když je mřížka nižší, než by odpovídalo datům, řádky (a font) se proporcionálně zvětší (strop 1,5× přirozené výšky)
- **Rozestup odjezdů** - jak daleko od sebe stojí minuty v řádku hodiny. *Bez zarovnání*: každý odjezd zabere jen to, co potřebuje, takže minuty pod sebou nestojí a minuta se značkou odsune odjezdy za sebou. *Zarovnat dle nejširšího odjezdu* (výchozí): jedna rozteč pro celý prvek podle nejvíc označkovaného spoje - minuty stojí ve svislici napříč hodinami i sloupci, ale jeden takový spoj zředí celou vývěsku, i řádky a sloupce bez značek. *Zarovnat na pevnou šířku*: rozteč se řídí nastavením („Šířka minuty", nejméně však minuta a jedna značka s odstupem), takže ji data nezmění a přidaný označkovaný spoj zbytek vývěsky nepřesází. Spoj s víc značkami se do rozteče vejde tím, že se jeho značky úměrně zmenší - nejvýš na polovinu a nikdy pod 4 body; nevejdou-li se ani tak, zvětšete Šířku minuty
- **Řádky dat** - Výška řádku, Střídavé podbarvení (sudé řádky)
- **Záhlaví - 2. řádek** - Velikost, Tučné, Kurzíva, Barva textu pro druhý řádek nadpisu skupiny (např. „den" v „Pracovní / den") - pokud nevyplněno, dědí z prvního řádku
- **Styl minuty** - Font, Velikost, Tučné, Kurzíva, Barva textu
- **Anotace - symboly** a **Anotace - text** - samostatný styl (font/velikost/řez/barva) pro horní index u minut - symboly/negativní značky vs. textová písmena/dny; u symbolů navíc NZ pozadí a NZ znak (barvy invertovaného boxu negativní značky)
- **Anotace** - zap/vyp zobrazování anotací u minut
- **Mezery** - Mezera minuta↔anotace, Mezera anotace↔anotace, Mezera mezi odjezdy

#### Poznámky

Vysvětlivky pod odjezdy.

- **Záhlaví** (skupina Obecné) - vypnutím zmizí celý pruh záhlaví i s podbarvením a přestane
  zabírat výšku, takže poznámky začínají u horní hrany prvku. Prázdný text záhlaví pruh nezruší -
  pás bez textu může být záměr
- **Text záhlaví** - co je v pruhu napsané (výchozí „Poznámky")
- **Velikost** (skupina Záhlaví) - velikost písma v pruhu záhlaví; prázdné = 9 bodů. Výška pruhu
  se počítá z ní, takže větší písmo pruh zvýší. Poznámky pod ním mají velikost vlastní
- **Oddělovač** - text mezi značkou a popisem (výchozí „ - ")
- **Velikost poznámek** - velikost písma. Nevejdou-li se poznámky do prvku, písmo se zmenší
  a aplikace to oznámí ve stavovém řádku
- **Výška řádku** - prázdné = podle velikosti písma (mezera pak roste s písmem). Vlastní hodnotu
  zadejte, jen když chcete legendu vzdušnější nebo stlačenější
- **Počet sloupců (1–4) + Směr plnění** - „Po sloupcích" (jako noviny) nebo „Po řádcích"
- **Řazení** - Dle typu (dny platnosti → značky → atributy → text; výchozí) / Výchozí (pořadí z dat) / Délka vzestupně / Délka sestupně
- **Šířka buňky značky** - pevná šířka sloupce se značkou, aby byly texty svisle zarovnané (0 = značka inline před textem)
- **Negativní značky / Speciální znaky / Textové poznámky** - přepínače, které typy poznámek se v legendě vůbec zobrazí
- **NZ pozadí / NZ barva** - barvy invertovaného boxu u negativních značek

**Co se na vývěsce ukáže** (jen zastávkový JŘ z AD dat) - všechno se zapíná tady, v jednom seznamu:

- **Poznámky linky - tarifní / všeobecné / radiobus** - texty zadané ve vlastnostech linky. Jsou to
  tytéž druhy, které v jízdním řádu volíte pro tisk A4/A5. Výchozí stav je vypnuto.
- **Poznámky linky - výluky** - *Včetně* / *Bez výluk* / *Jen výluky*. Výluky jsou všeobecné poznámky
  začínající slovem „Výluka", takže se zapínají spolu s nimi; tato volba je jen **rozděluje mezi
  bloky**. Chcete-li je zvlášť, položte dva bloky poznámek nad sebe: v horním *Bez výluk*, ve spodním
  *Jen výluky* (a hornímu nechte prázdnou výšku, aby se spodní sám posunul).
- **Souhrn bezbariérových spojů** - souhrnná vysvětlivka „všechny spoje mají bezbariérově přístupné
  vozidlo" místo značky u každého odjezdu. Výchozí stav zapnuto.
- **Přeprava kol u odjezdů** a **Místenky u odjezdů** - zapnou **značku u času odjezdu i vysvětlivku
  v legendě naráz**, aby nevznikla značka, které nikdo nerozumí. Kola se kreslí symbolem kola,
  místenka písmenem „R". Výchozí stav vypnuto.
- **Pokračující spoje** - spoje, které pokračují dál po jiné lince, dostanou u odjezdu značku „u1",
  „u2"… a v legendě vysvětlivku „pokračuje do zast. … (l. 304)" s posledním trojčíslím linky.
  Odjezdy se stejným cílem po stejné lince mají tutéž značku, čísla spojů se neřeší. Výchozí vypnuto.

> Značky kol a místenek se kreslí u minut, ale zapínají se tady - aby bylo vypínání obsahu na jednom
> místě. Šablona **bez bloku poznámek** je proto nemá kde zapnout.

Všechny tři prvky mohou mít pevnou nebo dynamickou (automatickou) výšku a při pevné výšce
uplatňují **auto-zmenšení písma** (kap. 6).

---

### 3. Editory sloupců

#### Editor sloupců zastávek
Dvoutabový dialog: **Obecné** (vlastnosti prvku) a **Sloupce**:

- Seznam sloupců s checkboxem viditelnosti a tlačítky ▲/▼ pro pořadí; sloupce chybějící
  v uložené šabloně se doplní automaticky (skryté, ale kdykoli zapnutelné).
- Pro vybraný sloupec: **šířka** v mm (prázdné = automatické rozdělení zbylého prostoru),
  **zarovnání**, a plný per-sloupec styl (font, velikost, řez, barvy, zarovnání, odsazení) zvlášť
  pro záhlaví a data - viz kaskáda stylů v části I, kap. 7.
- **Dvouřádkové záhlaví** - do pole „Záhlaví" lze zadat víc řádků (např. český a cizojazyčný
  název sloupce, „Zastávka" / „Station"): napište `\n` nebo stiskněte Enter. Výška záhlaví se
  přizpůsobí automaticky. Detail viz část I, kap. 8.
- Varování při přesahu šířky (s přesnými čísly) + tlačítko „Přizpůsobit šířky" pro proporční
  přeškálování.
- Tlačítka „Resetovat styl" (vybraný sloupec) a „Resetovat všechny styly".

#### Vlastní definice sloupců vývěsky

Které sloupce na vývěsce vzniknou, dosud určovala konfigurace linky. V editoru prvku **Minuty
odjezdů** je proto záložka **„Definice sloupců"** s přepínačem **Automaticky (dle linky)** ×
**Vlastní sloupce** - vlastních jde nadefinovat až pět a **uloží se do šablony**, takže platí
pro každou linku, kterou tou šablonou vytisknete.

U každého sloupce nastavíte:

- **Nadpis** - i dvouřádkový (Enter uvnitř pole).
- **Týdenní platnost** - jednotlivé dny **Po až Ne** plus dvě samostatné volby podle vyhlášky
  122/2014:
  - **Pracovní dny (X)** - pracovní dny **bez svátků**. Není to zkratka za Po-Pá: spoj „jede
    v pondělí" jede i o svátečním pondělí, spoj „jede v pracovních dnech" ne. Proto se „Pracovní
    dny (X)" a vypsané Po-Pá **vzájemně vylučují** a editor tu kombinaci zašedne.
  - **Neděle a svátky (+)** - značka podle vyhlášky pokrývá obojí, takže sloupec „Neděle a svátky"
    zaškrtnete jednou volbou. S „Ne" se **nevylučuje**.
- **Značky** - *jen s těmito* / *bez těchto* negativními značkami. Tím se dá vyřešit i „tenhle spoj
  na vývěsce o víkendu nechci".
- **Období** - seznam rozsahů dat („14. 2.-18. 2., 1. 7.-31. 8."). Datum **bez roku platí každý
  rok** (šablona přežije překlopení jízdního řádu), datum **s rokem** platí jednou (výluka,
  mimořádný provoz). Do sloupce patří spoj, který v období jede aspoň jeden den.
- **Nevypisovat značky** - které negativní značky se v tom sloupci u minut netisknou.

Čtyři dosavadní automatická rozvržení jsou připravená **jako vzory na jedno kliknutí**, takže
obvykle stačí vzor vybrat a doladit; u prázdninových vzorů se čísla značek předvyplní z konfigurace
aktuální linky. Když sloupce nedefinujete, řídí se vývěska konfigurací linky jako dosud. Pokud
definici zadáte, seznamy značek u linky se pro vývěsku ignorují (jinak by proti sobě stála dvě
nastavení). Totéž nastavení je vidět a přepínatelné i parametrem **„Rozdělení sloupců (typy
provozu)"** - viz výše.

#### Editor skupin dnů (sloupec hodin + skupiny)
Analogický dvoutabový dialog:

- První řádek je vždy pevně **„Hodiny"** (nelze přesunout), následují jednotlivé skupiny dnů
  podle skutečných dat.
- Pro skupinu: viditelnost, pořadí (▲/▼), **přepis nadpisu** (jinak se použije nadpis z dat, u
  sloupce hodin výchozí „Hod"), pevná šířka (prázdné = automatický výpočet + rovnoměrné dofill),
  vlastní **střídání řádků** pro danou skupinu.
- **Barevné pruhy podle skupin dnů** - když každá skupina dostane vlastní barvu záhlaví, převezme
  ji i **sloupec hodin** u té skupiny (platí při zapnuté volbě *Hodiny u každé skupiny*), takže
  celý pruh vyjde jednobarevný sám a nemusíte ho barvit zvlášť. Buňka nad hodinami zůstane
  neutrální - je to místo pro ikonku hodin.
- **Víc řádků v nadpisu skupiny** - v přepisu nadpisu stiskněte **Enter** (nebo napište `\n`)
  a nadpis se rozdělí na dva řádky se **stejným** stylem. Výška záhlaví se přizpůsobí. Když má
  jízdní řád u skupiny vyplněný i druhý nadpis, přidá se pod ně jako další řádek s vlastním
  stylem (viz *Záhlaví - 2. řádek* ve vlastnostech prvku).
- Per-sloupec styl barev a zarovnání zvlášť pro záhlaví a data. **Typografie záhlaví (font,
  velikost, řez) je ale jednotná pro celý prvek** - nedá se nastavit odlišně po jednotlivých
  skupinách, aby šla velikost písma měnit z jednoho místa v panelu.

---

### 4. Výběr linek k tisku

Dialog výběru instancí - vyhledávací pole (Enter spustí filtr), seznam linek s checkboxy pro
výběr více najednou, tlačítka „Vybrat vše" / „Odznačit vše". Seznam obsahuje **všechny linky
zvolené skupiny**, takže hledání najde i linku, která je v seznamu až daleko dole. Otevře se klikem na modrý štítek
„Instance: X - Y (N/M)" ve status baru.

Nad seznamem je **skupina linek** - seznam ukazuje linky zvolené skupiny a přepnutím se
načtou linky jiné. Linky téže firmy bývají rozdělené do víc skupin, takže bez přepnutí část z nich
v seznamu není. U agend, které skupiny linek nemají, se volba nezobrazuje.

Po výběru více linek se mezi nimi v hlavním okně přepínáte šipkami ◀ / ▶ vedle štítku - každá
změna přenačte data pro danou instanci a živě aktualizuje náhled. Vybrané linky slouží i pro
dávkový export/tisk (kap. 5).

---


### 5. Tisk a export

#### Parametry sestavy v tiskovém dialogu
Generují se dynamicky podle zvolené agendy. Společné pro obě agendy:

- **Tisknout šipky směru**, **Tisknout projeté zastávky**
- **Pořadí stránek** - v jakém pořadí jdou vývěsky v exportu. *Po zastávkách (oba směry za sebou)*
  je výchozí: linka se projde jednou a u každé zastávky jsou obě její vývěsky vedle sebe, takže se
  vytisknou a pověsí jedním výjezdem. *Po směrech (tam vzestupně, zpět sestupně)* vytiskne nejdřív
  celý směr Tam od první zastávky k poslední a pak celý směr Zpět od poslední k první - cestující
  tak projde trasu tam a zase zpátky
- **Časová osa odjezdů** (Jen hodiny s odjezdy / Souvislá od první do poslední / Vlastní rozsah +
  Hodiny od/do). Vlastní rozsah **umí přejít přes půlnoc**: nastavíte-li např. od 22 do 5, osa
  jde 22, 23, 0, 1…5. Pro celodenní linku s nočními spoji zvolte např. od 4 do 0 - osa končí
  řádkem 0 za třiadvacátou hodinou, takže půlnoční odjezdy jsou na konci provozního dne a nic
  z denního provozu se neztratí.
- **Roztáhnout na výšku**

Navíc jen u AD: **Rozdělení sloupců (typy provozu)**, **Řazení variant tras**, **Písmena variant
u odjezdů** (+ **První varianta bez písmene**), **Nejvýš sloupců variant tras**, **Samostatná stránka pro každý označník** (vše
viz kap. 1), **V rozsahu nepoznámkovat schovat** a **Souhrn „všechny zastávky na znamení"** - místo
značky × u každé zastávky jeden souhrnný řádek v legendě (má smysl na linkách, kde jsou na znamení
všechny zastávky).

> **Co se zobrazí, se nastavuje v šabloně, ne tady.** Poznámky zadané u linky, přeprava kol,
> místenky a souhrn bezbariérových spojů se zapínají u **prvku Poznámky** (kap. 2) - aby bylo
> jedno místo, kde si vyberete, co vývěska ukazuje. V parametrech sestavy zůstává to, co mění
> **obsah nebo rozvržení** dat, ne jen jejich zobrazení.

Většina parametrů má u sebe **nápovědu** (bublinu s vysvětlením), co přesně dělá.

#### Export - členění a pojmenování
Tato pole se nastavují jen v tiskovém dialogu (nejsou to parametry sestavy, takže nejsou
v záložce Parametry v levém panelu):

- **Členění výstupu** - Vše do jednoho PDF / Jeden PDF na linku / Jeden PDF na zastávku/směr.
- **Název souboru (tokeny {POLE})** - výchozí `{CISLO_LINKY}_{POPIS_ZASTAVKA}_{POPIS_SMER}`,
  lze upravit na libovolnou kombinaci datových polí.

U šablony o víc stránkách je k dispozici i **Uspořádání na list** (víc stránek na jeden papír);
detail viz část I, kap. 9.

„Vše do jednoho" vybere jeden cílový soubor; ostatní dvě volby vybírají cílovou složku a vytvoří
tolik souborů, kolik odpovídá volbě (kolize jmen se řeší automaticky `_2`, `_3`…). Export běží
asynchronně nad snímkem stavu šablony pořízeným při kliknutí - mezitím lze dál v šabloně
pracovat, výsledek odpovídá stavu v okamžiku exportu. Po dokončení se otevře výsledný PDF nebo
cílová složka a status bar zobrazí souhrn (počet souborů, celkový počet stránek, případné
varování o přetékajících stránkách).

---

### 6. Auto-přizpůsobení a přetečení

Pokud má některý ze tří prvků JŘ nastavenou **pevnou výšku** a skutečný obsah (počet zastávek,
odjezdů, poznámek) by potřeboval víc místa, systém automaticky **proporcionálně zmenší
velikost písma**, aby se všechno vešlo do vymezeného prostoru. Pokud je výška prvku dynamická
(automatická), tohle se neuplatňuje - prostor prostě roste podle potřeby.

Varování o přetečení (červený rámeček stránky v náhledu) se u JŘ elementů řídí stejným
pravidlem jako v části I (kap. 9): objeví se jen u sekcí nastavených
jako „Držet pohromadě", a jen když se prvek fyzicky nevejde i po auto-zmenšení písma - pokud
zmenšení pomůže, k varování nedojde.

---

### 7. Postup krok za krokem

1. **Založte šablonu se správnou agendou** - nová šablona přes `Nástroje → Změnit agendu`
   (Ctrl+Shift+A) a výběr Zastávkový JŘ AD nebo MHD. Alternativa: otevřete výchozí šablonu
   ze sekce **Systémové M-line** (Ctrl+O), nebo naimportujte existující PDF vývěsku
   s AI rozpoznáním (`Nástroje → Import PDF…`) a jen ji dolaďte.
2. **Přidejte prvky** - z palety nebo lišty nástrojů vložte Tabulku zastávek (typicky vlevo), jednu
   nebo víc prvků Minuty odjezdů (typicky vedle sebe, rozdělené podle skupiny dnů) a volitelně
   Poznámky (pod odjezdy).
3. **Nastavte sloupce** - tlačítkem „Upravit sloupce…" na každém prvku otevřete příslušný editor
   a zapnete/přeuspořádáte sloupce, doladíte šířky a vzhled.
4. **Vyberte linky** - přes dialog výběru instancí (kap. 4) zvolte jednu nebo víc linek; náhled
   procházíte šipkami mezi vybranými.
5. **Doladíte parametry tisku** - v tiskovém dialogu (Ctrl+P) nastavíte parametry specifické pro
   agendu a způsob exportu (členění, název souboru).
6. **Vytvořte PDF** - tlačítkem „Vytvořit PDF" v tiskovém dialogu; vezmou se všechny vybrané
   linky (bez vybrané linky ukázková data) a výsledek se automaticky otevře. Předtím můžete
   tlačítkem „Zkontrolovat" projít hlášky za všechny linky.

Pro rychlý start je v přehledu šablon (Ctrl+O) sekce **Systémové M-line** s výchozí
zastávkovou šablonou (AD) dodávanou a aktualizovanou M-line.

---

## Linkový jízdní řád

Celá linka na jedné sestavě: **zastávky jsou řádky, spoje sloupce**. Data jdou z hlavního modulu
Jízdní řády (varianta AD) a jednou instancí je jedna linka - vyberete ji stejně jako u vývěsky.
Většinu obsahu si sestava dopočítá sama (které zastávky, které spoje, co k nim patří za značky);
vy nastavujete, co všechno se má tisknout a jak to má vypadat.

### Obsah (linkový jízdní řád)

1. [Z čeho se sestava skládá](#1-z-čeho-se-sestava-skládá)
2. [Prvek Tabulka spojů](#2-prvek-tabulka-spojů)
3. [Editor sloupců](#3-editor-sloupců-tabulky-spojů)
4. [Jak se tabulka vejde na papír](#4-jak-se-tabulka-vejde-na-papír)
5. [Parametry sestavy](#5-parametry-sestavy-linkového-jř)
6. [Postup krok za krokem](#6-postup-krok-za-krokem-linkový-jř)

---

### 1. Z čeho se sestava skládá

Obvyklá šablona má tři sekce pod sebou:

- **Hlavička** (opakuje se na každé stránce) - textové prvky s poli `{CISLO_LINKY}`,
  `{POPIS_LINKY}`, `{DATUM_PLATNOSTI_OD}`, `{DATUM_PLATNOSTI_DO}`, `{TYP_JR}`,
  `{POPIS_DOPRAVCE}` a `{POZNAMKA_NAD_LINKU}`. `{POPIS_DOPRAVCE}` uvádí jen dopravce spojů na dané
  stránce; jezdí-li na stránce víc dopravců, připíše ke každému čísla jeho spojů.
- **Jízdní řád** - jediný prvek **Tabulka spojů**. Sekce má být nastavená na „Rozdělit přes
  stránky", jinak se dlouhá linka nevytiskne celá.
- **Poznámky** - prvek **Poznámky**, tentýž jako u vývěsky: vysvětlí značky, které tabulka
  použila. Bez něj se značky v tabulce vytisknou, ale čtenář se nedozví, co znamenají.

Data, se kterými tabulka pracuje: zastávky (zóna, kilometry, tarifní číslo, název, MHD, atributy
trasy), spoje (číslo, časy na zastávkách, značky platnosti, negativní značky, barva), skupiny dnů,
přípojné řádky a texty poznámek.

---

### 2. Prvek Tabulka spojů

Nastavení má v panelu vlastností (a ve stejném okně po Alt+Enter) šest skupin.

**Řádky** - co se tiskne navíc k časům:

- **Nadpisy skupin dnů** - pás nad sloupci spojů s označením skupiny (pracovní dny, sobota…).
- **Čísla spojů** - řádek s čísly spojů nad časy. Vypnuté = řádek zůstane (nese nadpisy popisných
  sloupců), jen nad spoji je prázdný.
- **Řádek platnosti** - pod číslem spoje řádek se značkami platnosti (denní značky, místenka,
  atributy vozidla).
- **Řádek negativních značek** - značky, které pro spoj **neplatí** (bílý text v černém poli).
- **Turnusy ke spojům** - k číslu spoje se dotáhne turnus. Je to provozní údaj dopravce, ne
  informace pro cestujícího, proto je z výroby vypnutý; zapnutí stojí čas navíc při načítání dat.
- **Přípojné řádky (vyčká/navazuje)** - pod zastávkou řádky o přípojích.
- **Výška řádku** - prázdné = podle velikosti písma.
- **Velikost značek** - kolikrát větší je písmo značek než písmo buňky (starý tisk 1,5×, aby byly
  na výtisku čitelné).

**Šířka sloupců** - viz kapitola 4.

**Popisné sloupce** (levý blok se zastávkami):

- **Každá zóna ve vlastním sloupci** - zastávka na hranici zón má víc zón; buď jsou za sebou
  v jednom sloupci, nebo každá ve vlastním jako kilometry.
- **Nadpis popisných sloupců** - jeden společný text nad zónou, km, Tč, zastávkou a MHD.
- **Sloučit popis u příjezdu a odjezdu** - zastávka s příjezdem i odjezdem zabírá dva řádky.
  Vypnuté zopakuje název a km na obou (tvar windowsového tisku), zapnuté je vypíše jednou
  přes oba řádky.
- **Popisné sloupce bez vnitřních čar** - levý blok se kreslí jako jeden celek, svislé čáry mezi
  spoji zůstanou.

**Záhlaví** a **Datové buňky** - velikost písma, pozadí a barva textu obou zón, vnitřní okraje
buňky a tyhle volby:

- **Formát času** - *Minuty menším písmem (hh mm)* jako starý tisk, nebo *Celý čas stejně (hh:mm)*.
  **Velikost minut** říká, o kolik jsou minuty menší.
- **Tečky v prázdných buňkách** - buňka bez času se vyplní tečkami, takže tabulka drží svislé
  vodicí pruhy jako starý tisk. Dostanou je i prázdné sloupce dorovnání.
- **Tučné zastávky dle trasy** a **Čáry u zastávek dle trasy** - v trase linky jde u zastávky zadat
  „tučně" a „podtrženo"; zapnuté znamená, že je návrhář respektuje. Vypíná se to tady, ne
  v parametrech sestavy - je to údaj z jízdních řádů.
- **Tečky za názvem zastávky** - tečky od názvu k pravému okraji sloupce, aby oko dojelo k času.
- **Podbarvit lichá Tč** a **Barva lichých Tč** - střídavé podbarvení podle tarifního čísla.

**Přípojné řádky** - **Slučovat přípoje** (*Ne* / *Podle směru* / *Podle směru a linky*) a vlastní
písmo řádku. Sloučený řádek nese časy víc spojů a v popisku má směr („ze směru Trhové Sviny"),
takže šetří místo.

**Data** - kolekce, ze kterých prvek čte, **Viditelnost řádku** (výraz nad poli zastávky, např.
`{ZONA} == "1"`) a **Zvýraznění řádků** (seznam pravidel podmínka → barva pozadí, první splněné
vyhrává).

---

### 3. Editor sloupců tabulky spojů

Tlačítkem **Upravit sloupce…** se otevře editor se dvěma záložkami: **Obecné** (totéž co panel)
a **Sloupce**. V seznamu jsou popisné sloupce - šipka směru, zóna, kilometry, tarifní číslo,
název zastávky, MHD - a jako poslední položka **Spoj**.

U každého sloupce nastavíte záhlaví, šířku v milimetrech, zarovnání a celý styl záhlaví i buněk
(písmo, barvy, okraje). Sloupce se zapínají a vypínají - seznam je daný agendou.

⚠️ **Sloupec Spoj šířku nemá.** Je to jedna položka, ze které se v tisku stane tolik sloupců,
kolik má linka spojů, a jejich šířku počítá sazba z obsahu (viz kapitola 4). Styl, který u položky
nastavíte, platí pro všechny.

---

### 4. Jak se tabulka vejde na papír

Tohle je u linkového JŘ to hlavní: spojů bývá víc, než kolik se vejde vedle sebe.

**Šířka sloupce spoje se počítá z obsahu.** Sazba změří, co v sloupci opravdu stojí - čas, značky
nad ním, negativní značku - a k tomu přidá vnitřní okraje buňky. Žádné ruční číslo se nezadává;
kdo chce sloupce užší, ubere z toho, co je uvnitř:

- **Vnitřní okraj vlevo / vpravo** (skupina Datové buňky) - hlavní páka na vzduch kolem času.
  Nulou se sloupec stáhne až k obsahu.
- **Mezera mezi značkami** - odstup dvou značek nad časem (třeba kladívko a vozík). Dvojice
  značek bývá ve sloupci to nejširší, takže utažením téhle mezery se tabulka zúží nejvíc.
- **Mezera mezi značkou a časem** a **Mezera mezi hodinou a minutou** - totéž uvnitř buňky.
  Prázdné pole u všech tří znamená „mezera z písma".

**Stejně široké sloupce spojů** - vypnuté (výchozí) znamená, že každý sloupec je tak široký, jak
potřebuje jeho obsah; sloupec se značkami je pak širší než ostatní, přesně jako ve starém tisku.
Zapnuté srovná všechny na nejširší z nich - tabulka je pravidelná, ale zabere víc místa.

**Dorovnat prázdnými sloupci** - co se stane s volným místem vpravo. Zapnuté ho rozkreslí
prázdnými sloupci, takže tabulka končí rovně; vypnuté ho nechá bílé.

**Mezera mezi skupinami sloupců** - volné místo za popisným blokem a mezi skupinami dnů.
Bere se ze šířky prvku, takže se do řádku vejde méně spojů.

**Co se nevejde, jde pod tabulku.** Když je spojů víc, než kolik se vejde do šířky prvku, sestaví
se tabulka na několik **částí pod sebou**; každá zopakuje celý seznam zastávek se záhlavím a má
**vlastní rámeček**, aby bylo vidět, že jde o samostatnou tabulku. Když se část nevejde na stránku,
láme se a na další stránce se zopakuje její hlava (pás skupin dnů, čísla spojů, platnost).

**Hláška ve stavovém řádku** upozorní, když je sloupec spoje užší, než se jeho obsah vejde -
jmenuje konkrétní spoj a rozdíl v milimetrech. Řešením je buď širší prvek, nebo menší písmo buněk,
nebo utažené mezery výše.

---

### 5. Parametry sestavy linkového JŘ

V záložce Parametry (a v tiskovém dialogu) jsou dvě skupiny.

**Obecné:**

- **Obousměrný tisk** - oba směry v jedné tabulce, spoje opačného směru na druhé straně od
  zastávek. Vypnuté = každý směr na vlastní stránce.
- **Stránka pro každou zastávku** - výlepy na zastávky: každá zastávka, ze které spoje odjíždějí,
  dostane vlastní stránku za každý směr (i u obousměrného tisku) a její řádek je v tabulce
  inverzní (bílé písmo na černém), jako ve starém tisku.
  Konečná ani zastávka, kde se jen vystupuje, výlep nemá. **Zvýraznit zastávku** a **Zvýraznit
  tarifní číslo** pak vyberou jen některé zastávky.
- **Tvar názvu zastávky** - *Plný CIS* (výchozí, zachová dvojčárku podle číselníku, tak to tiskne
  starý modul), *Plný CIS s okresem*, *Zkrácený*, *Typograficky upravený* (mezera za čárkou a tečkou).
- **Použít název pro MHD** - zastávka může mít v číselníku vedle názvu podle CIS ještě upravený
  název pro MHD; vypnutím se všude použije název podle CIS.
- **Počet stran** - *1 strana* / *2 strany*: na kolik stran se má vejít každý směr linky. Když se
  nevejde, zmenší se nejdřív poznámky, pak tabulka; nejde-li to ani tak, ozve se hláška.
  *Automaticky (dle linky)* zatím počet stran neomezuje.

**Tabulka linky:**

- **Dělení spojů dle platnosti** - podle čeho se spoje rozdělí do skupin sloupců: *Bez rozlišení*,
  *Pracovní dny / víkend+svátky*, *Pracovní dny / Soboty / Neděle+svátky*. Hodnota *Vlastní
  rozložení* se vybrat nedá - jen hlásí, že si šablona rozvržení drží vlastní.
- **Sloupec tarifních čísel**, **Zastávky, kde žádný spoj nestaví**, **Písmeno systému u zóny**
  a **Barvy spojů** - všechny čtyři mají volbu **Automaticky (dle linky)**, tedy podle nastavení
  u linky v jízdních řádech, a pak *Tisknout* / *Netisknout*, kterými to pro tuhle sestavu přebijete.
- **Zvýraznit zastávku** a **Zvýraznit tarifní číslo** - kus názvu zastávky (nerozlišují se velká
  a malá písmena) označí její řádek jako aktuální; vzhled zvýraznění si řeší šablona pravidlem
  nad polem „Zvýrazněná zastávka". Projíždí-li linka město víckrát, označí se všechny jeho
  zastávky - jednu z nich vybere tarifní číslo.

**Výběr linek k tisku** i **tisk a export** fungují stejně jako u zastávkového JŘ (viz kapitoly
„Výběr linek k tisku" a „Tisk a export" výše).

---

### 6. Postup krok za krokem (linkový JŘ)

1. **Založte šablonu se správnou agendou** - `Nástroje → Změnit agendu` (Ctrl+Shift+A), Linkový JŘ.
   Rychlejší start: otevřete výchozí šablonu ze sekce **Systémové M-line** (Ctrl+O) - je v podobě
   starého postskriptového tisku na A5 na šířku.
2. **Postavte hlavičku** - textové prvky s poli linky; sekci nechte „na každé stránce".
3. **Vložte Tabulku spojů** do vlastní sekce nastavené na rozdělení přes stránky a roztáhněte ji
   přes celou šířku stránky.
4. **Nastavte sloupce** - tlačítkem „Upravit sloupce…" zapněte, co se má tisknout vlevo, a dolaďte
   šířky; sloupci Spoj nastavte jen styl.
5. **Dolaďte, kolik spojů je vedle sebe** - vnitřními okraji buněk a mezerami u značek (kap. 4).
   Náhled ukazuje výsledek rovnou.
6. **Přidejte Poznámky** pod tabulku, jinak nebude čím značky vysvětlit.
7. **Vyberte linky** (Ctrl+Shift+I) a v tiskovém dialogu (Ctrl+P) **vytvořte PDF**.
