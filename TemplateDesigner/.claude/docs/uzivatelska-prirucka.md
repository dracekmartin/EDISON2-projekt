# Návrhář šablon — uživatelská příručka

> Vizuální editor tiskových sestav v EDISONu. Navrhnete vzhled šablony, systém k ní doplní
> data a vytiskne PDF. **Část I** pokrývá obecné ovládání aplikace — práci se šablonou,
> sekcemi, prvky, styly, daty a exportem — bez ohledu na typ sestavy. **Část II** popisuje
> specifika jednotlivých agend (nyní zastávkový jízdní řád; další agendy přibudou jako
> nové kapitoly).

---

# ČÁST I — Obecné ovládání

---

## Obsah

1. [Co je Návrhář šablon](#1-co-je-návrhář-šablon)
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
13. [Časté otázky](#13-časté-otázky)

---

## 1. Co je Návrhář šablon

Návrhář šablon je **vizuální editor tiskových sestav** typu WYSIWYG („co vidíte, to dostanete").
Nahrazuje starší nástroj, kde se sestavy nastavovaly přes formuláře a číselné parametry bez
náhledu — výsledek byl vidět až po vytištění.

Systém má dvě vrstvy:

1. **Návrhář** — zde vizuálně navrhnete šablonu (rozložení stránky, texty, tabulky, jízdní řády).
2. **Tiskový engine** — vezme šablonu a reálná data a vytvoří PDF.

Klíčová myšlenka: **šablona popisuje jen vzhled**. Konkrétní hodnoty (čísla linek, časy, názvy
zastávek) doplní systém z databáze až při tisku. Stejný engine se používá pro živý náhled
v Návrháři i pro reálný tisk — proto náhled odpovídá výslednému výstupu.

**Co Návrhář není:**
- Není to Word ani Excel — pracuje se strukturou stránky (sekce), ne s volným plátnem.
- Stránka je vždy rozdělená na **sekce** — to je povinná struktura, ne volitelná.
- Staré šablony se v této verzi neimportují 1:1 — nové šablony se navrhnou znovu (díky
  vizuálnímu editoru rychle), případně s pomocí importu z PDF s AI rozpoznáním (kap. 10).

---

## 2. Orientace v aplikaci

Okno Návrháře má čtyři hlavní části: **toolbar** nahoře, **levý panel** se záložkami, **plátno**
uprostřed a **náhled** vpravo.

### Toolbar

Tlačítka jsou seskupená a oddělená svislými čarami:

| Skupina | Obsah |
|---|---|
| **Soubor** | Nová šablona, Otevřít, Uložit, Tisk |
| **Upravit** | Zpět, Vpřed |
| **Sekce** | Přidat sekci (rozbalovací nabídka presetů) |
| **Přidat / Elementy** | paleta prvků — Text, Obdélník, Čára, Obrázek, Tabulka, Kód (u agend jízdních řádů se navíc objeví specializované JŘ prvky) |
| **Vrstva** | Přenést dopředu / Odeslat dozadu / Přenést zcela dopředu / Odeslat zcela dozadu |
| **Zarovnat** | Zarovnat vlevo / na střed vodorovně / vpravo, nahoru / na střed svisle / dolů, rozložit rovnoměrně vodorovně/svisle (min. 3 vybrané prvky) |
| **Zobrazení** | přepínač mřížky, přepínač přichytávání k objektům, velikost mřížky, velikost mezery pro přichytávání, zoom, tlačítka „Fit" a „1:1", skrytí panelu sekcí, přepínač živého náhledu |

### Levý panel — 5 záložek

| Záložka | K čemu |
|---|---|
| **Sekce** | seznam sekcí šablony, přidávání, řazení, mazání |
| **Pole agendy** | databázová pole dostupná pro aktuální agendu (dvojklik nebo tažení vloží `{POLE}` na plátno); hlavička zobrazuje název aktuální agendy a je klikatelná — otevře přepnutí agendy |
| **Vlastnosti** | nastavení právě vybraného prvku nebo sekce — okamžitá editace bez nutnosti otevírat dialog |
| **Strom** | hierarchický přehled celé šablony (sekce → skupiny prvků → prvky) pro rychlou navigaci |
| **Pomocník** | AI pomocník — zeptáte se vlastními slovy, kde v aplikaci co najdete nebo jak něco udělat (vyžaduje aktivovanou AI službu u M-line) |

### Status bar (dole)

Zleva doprava:
- zelený štítek s aktuální **agendou a variantou** — klik otevře dialog přepnutí agendy,
- modrý štítek **aktivní instance** (např. vybrané linky) s tlačítky ◀ / ▶ pro přepínání mezi
  vybranými instancemi a formátem „Instance: X — Y (N/M)",
- informace o aktuálním výběru,
- obecné stavové hlášky,
- při přetečení obsahu stránky červené upozornění „⚠ Obsah se nevejde na stránku".

### Hlavní menu

- **Soubor** — Nová šablona · Otevřít z databáze… · Uložit do databáze · Uložit do databáze
  jako… · Importovat z JSON souboru… · Exportovat do JSON souboru… · Otevřít poslední JSON
  (submenu) · Exportovat náhled jako PDF… · Exportovat náhled jako PNG… · Tisk… · Vlastnosti
  stránky… · Konec
- **Úpravy** — Zpět · Vpřed · Smazat · Z-order (podnabídka) · Zarovnat (podnabídka) · Rozložit
  (podnabídka) · Normalizovat pozice… · Škálovat šablonu (podnabídka procent) · Kopírovat styl ·
  Vložit styl
- **Zobrazit** — Mřížka, Přichytit na mřížku, Přichytit k elementům, Záhlaví sekcí, Pravítka,
  Panel sekcí, Live náhled (zaškrtávací položky)
- **Nástroje** — Změnit agendu… · Šablony v databázi… · Import PDF…
- **Nápověda** — Klávesové zkratky a ovládání (F1) — s fulltextovým hledáním (Ctrl+F)

Titulek okna má formát `[KÓD_AGENDY] [DB nebo neuloženo] Název šablony [*]` — hvězdička značí
neuložené změny.

---

## 3. Práce se šablonou

### Nová šablona
`Soubor → Nová` (Ctrl+N). Nová šablona obsahuje jednu výchozí sekci přes celou tiskovou plochu;
sekce a prvky pak přidáváte podle potřeby.

### Agenda a varianta — nad jakými daty šablona běží
Každá šablona je vázaná na **agendu** (jakou sestavu tvoříte — např. Zastávkový jízdní řád,
Faktury, Obecná) a u jízdních řádů navíc na **variantu dat** (odkud data jsou — **AD** = linková
doprava z hlavního JŘ modulu, **MHD** = městský modul). Kombinace určuje, jaká **databázová pole**
a jaké **specializované prvky** máte k dispozici.

- Aktuální agenda a varianta je vidět jako **zelený štítek** ve status baru (např. „Zastávkový
  JŘ · AD"), v titulku okna a v hlavičce panelu „Pole agendy".
- Změna: `Nástroje → Změnit agendu…`, klávesa **Ctrl+Shift+A**, nebo klik na zelený štítek —
  dialog nabízí agendy seskupené s variantami pod sebou.
- **Přepnutí agendy vždy založí novou prázdnou šablonu** pro zvolenou agendu — nejde o konverzi
  obsahu stávající šablony. Rozdělanou práci vás systém před přepnutím vyzve uložit nebo zahodit.

### Otevření a uložení
Šablony se ukládají **do databáze** (centrálně, podle firmy), ne do lokálních souborů na stanici:

- `Soubor → Otevřít z databáze…` — dialog „Šablony v databázi": filtr podle agendy, sloupce
  **Agenda | Varianta** hned za názvem, sekce **Systémové M-line** (dodané předlohy, jen ke
  čtení — upravenou verzi uložíte jako vlastní šablonu) a **Moje šablony**; tlačítka
  Přejmenovat…, Smazat, Otevřít.
- `Soubor → Uložit do databáze` — u šablony již uložené v databázi provede tichý update.
- `Soubor → Uložit do databáze jako…` — dialog s polem pro název a popis; nová/importovaná
  šablona se takto poprvé zařadí do databáze.
- Výhoda oproti lokálním souborům: šablony jsou sdílené, centrální a dohledatelné — ne
  roztroušené po jednotlivých počítačích.

### Import a export do souboru
Šablonu lze také uložit jako **soubor JSON** — obdoba exportu do PDF/PNG, ne náhrada
databázového úložiště:

- `Soubor → Importovat z JSON souboru…` — otevře soubor; šablona se pak chová jako nová
  neuložená.
- `Soubor → Exportovat do JSON souboru…` — uloží aktuální stav do souboru, ale **nemění**
  původ šablony (pořád zůstává vázaná na databázi, pokud odtamtud byla otevřená).
- Hodí se pro poslání šablony e-mailem kolegovi nebo dopravci.

---

## 4. Sekce a struktura stránky

Stránka se skládá ze **sekcí**. Sekce je vodorovný pruh, do kterého umisťujete prvky. Každá
sekce má nastavené chování, které určuje **kdy a jak často** se tiskne a **co se stane při
přetečení** obsahu.

### Přidání sekce
Tlačítko `+ Přidat sekci` nabízí presety:

| Preset | Chování | Výchozí výška |
|---|---|---|
| Hlavička stránky | každá stránka | 25 mm |
| Patička stránky | každá stránka | 15 mm |
| Oddělovač | jednou | 5 mm |
| Hlavička sestavy — jen 1. str. | jen první stránka | 30 mm |
| Patička sestavy — jen posl. str. | jen poslední stránka | 20 mm |
| Hlavička kromě 1. str. | všechny kromě první | 25 mm |
| Prázdná sekce | jednou | 30 mm |
| *(vlastní uložené šablony sekcí, značené 💾)* | podle uložené šablony | podle uložené šablony |

### Vlastnosti sekce (dialog)
Pole v dialogu vlastností sekce: **Název**, **Výška** (mm), **Tisk**, **Umístění**, **Přetečení**,
**Rámeček**, **Barva rámečku**, **Tloušťka rámečku**, **Viditelnost**. Volby v rozbalovacích
seznamech:

- **Tisk**: Jednou · Každá stránka · Jen první · Jen poslední · Kromě první
- **Umístění**: Obsah · Hlavička · Patička (patičky se umisťují od spodního okraje stránky)
- **Přetečení**: Nepřerušovat · Přes stránky · Povolit split

Pokud sekce opakuje řádek dat (např. pro dynamický seznam), přibudou pole **Datový zdroj** a
**Výška řádku**.

### Práce se sekcemi
- **Mazání** — poslední zbývající sekci nelze smazat; sekce s prvky vyžaduje potvrzení. Uvolněná
  výška se proporcionálně rozdělí mezi zbývající sekce (a naopak při přidání nové).
- **Řazení** — přetažením v levém panelu, nebo šipkami v kontextovém menu.
- **Vlastní šablony sekcí** — pravý klik na sekci → „Uložit jako šablonu…", zadáte název. Sekce
  se pak nabízí v menu „+ Přidat sekci" (💾) a lze ji opakovaně vkládat do dalších šablon —
  vkládá se vždy jako nezávislá kopie.

---

## 5. Práce s prvky (elementy)

Prvek je samostatný objekt na plátně. Dostupné typy a jejich hlavní vlastnosti:

| Prvek | Hlavní vlastnosti |
|---|---|
| **Text** | text (víceřádkový), rodina a velikost písma (4–200 pt), tučné/kurzíva/podtržení/přeškrtnutí, zalamování, řádkování, zarovnání vodorovně (vlevo/na střed/vpravo/do bloku) i svisle, barva textu a pozadí |
| **Obrázek** | nahrání souboru (limit 5 MB, automatické zmenšení větších), volitelné načtení z datového pole, škálování: vejít celý / vyplnit (oříznout) / roztáhnout / bez úpravy |
| **Obdélník** | výplň, barva a tloušťka okraje (0–10 mm), zaoblení rohů (0–50 mm) |
| **Čára** | orientace (vodorovná/svislá), barva, tloušťka, styl (plná/čárkovaná/tečkovaná), délka čárky a mezery |
| **Kód** | obsah, typ kódu — **QR kód, Code 128, EAN-13, EAN-8, PDF417, DataMatrix** — barva kódu a pozadí |
| **Tabulka (dynamická)** | řádky se generují automaticky z datové kolekce (viz níže); nabízí se jako jediná varianta tabulky v paletě |
| **Prvky jízdního řádu** | specializované prvky pro tisk JŘ — objeví se v paletě jen u agend jízdních řádů; viz část II |

### Dynamická tabulka
Sloupce definujete jednou, řádky doplní systém z vybrané datové kolekce (počet řádků se odvíjí
od dat). Vlastnosti: **Upravit sloupce…**, **Zdroj dat**, **Výška řádku**, **Záhlaví**, **Výška
záhlaví**, **Rámeček** (Žádný/Vnější/Vše/Řádky/Sloupce), **Střídání řádků**, **Tučné záhlaví**,
barvy pozadí a textu záhlaví i dat. Buňka může obsahovat souhrnný výraz — viz kap. 8.

Editor sloupců (dialog „Tabulka — nastavení") má záložky **Obecné** a **Sloupce**: přidání/
odebrání sloupce, varování při přesahu šířky + tlačítko „Přizpůsobit sloupce k šířce elementu",
a pro každý sloupec záhlaví, šířku, mapované datové pole a plný editor stylu (viz kap. 7).

### Přidání prvku
- Klik na tlačítko v paletě vloží prvek do aktivní sekce (souřadnice 5, 5 mm).
- **Tažení z palety** přímo na plátno — během tažení vidíte náhled a zvýrazní se cílová sekce.
- **Tažení databázového pole** z panelu „Pole agendy" — tažení do prázdna vytvoří nový textový
  prvek s `{POLE}`; tažení na existující text pole připojí k jeho obsahu. Dvojklik na pole
  funguje jako alternativa k tažení.

### Výběr, přesun, velikost
- Klik vybere prvek, tažení ho přesune, úchyty na okrajích mění velikost.
- Výběr více prvků: tažení rámečku (rubber band), nebo Ctrl+klik.
- Prvek se nevejde mimo hranice své sekce — automatické oříznutí.
- Klik bez pohybu myší nespustí přesun — teprve po překročení malého prahu (pár pixelů) se prvek
  začne hýbat, takže omylem nedojde k drobnému posunu při pouhé selekci.

### Seskupování
Ctrl+G spojí vybrané prvky do skupiny; Ctrl+Shift+G skupinu zruší. Klik vybere celou skupinu,
Alt+klik jen jeden prvek uvnitř. Skupina má vizuální odznak a ohraničující rámeček, ve stromu se
zobrazí jako samostatná úroveň. Import z PDF prvky automaticky seskupuje.

---

## 6. Editační nástroje

### Přichytávání (snap)
Tři nezávislé vrstvy, ovládané z toolbaru:
- **Mřížka** — zapínatelná tlačítkem „M", velikost 0,2–10 mm (výchozí **1 mm**, přichytávání
  na mřížku je nově výchozí **zapnuté** — jemný krok drží pozice čisté, aniž by omezoval).
- **Přichytávání k objektům** — tlačítko „O" (výchozí zapnuto); prvek se přichytává k hranám a
  středům jiných prvků i k okrajům sekce, s vizuálními vodítky.
- **Mezera pro přichytávání (gap snap)** — 0–10 mm (výchozí 2 mm); prvek se umí přichytit
  i v definované mezeře od souseda, se zobrazením indikátoru mezery.

### Duplikování
- **Ctrl+D** bez směru — kopie s malým posunem (+5 mm v obou osách).
- **Ctrl+D + šipka** — kopie umístěná ve zvoleném směru s mezerou rovnou aktuálnímu nastavení
  gap snapu (výchozí 2 mm).
- Duplikování neovlivňuje schránku (clipboard).

### Kopírování stylu
**Ctrl+Shift+C** zkopíruje styl (font, rámeček, pozadí) z prvního vybraného prvku, **Ctrl+Shift+V**
ho aplikuje na vybrané prvky.

### Hromadné úpravy
Výběr více prvků + **Enter** otevře dialog společných vlastností: Barva rámečku, Šířka rámečku,
Pořadí vrstvy (ZIndex), Zamčeno — změna se aplikuje na všechny vybrané najednou.

### Normalizace pozic
**Ctrl+Shift+N** — dialog s volbou kroku zaokrouhlení (0,1 / 0,5 / 1 / 2 / 5 mm) a živým náhledem
počtu prvků, které se změnou upraví. Opravuje drobné nepřesnosti pozic vzniklé např. přesným
tažením myší.

### Škálování šablony
Dropdown v toolbaru (50–200 %) nebo menu `Úpravy → Škálovat šablonu` (50/75/90/100/110/125/150/
200 %). Přepočítá rozměr stránky, okraje, výšky sekcí, pozice a rozměry prvků i jejich
font/padding vlastnosti — proporcionálně zvětší nebo zmenší celou šablonu najednou.

### Vrátit / opakovat akci
**Ctrl+Z** / **Ctrl+Y** — každá akce (přesun, změna vlastnosti, přidání/smazání prvku či sekce…)
je vratná.

---

## 7. Vlastnosti a styly

Vybraný prvek nastavíte v záložce **Vlastnosti** levého panelu (okamžitá editace) nebo v dialogu
po stisku **Enter** či dvojkliku na prvek — obsah je stejný, panel je jen rychlejší cesta.

### Rámeček a pozadí
Každý prvek může mít rámeček (barva + tloušťka) a barvu pozadí. Když dva prvky sdílejí hranu,
vykreslí se jako **jeden** společný rámeček, ne zdvojeně.

### Kaskáda stylů u tabulkových prvků
U tabulek a specializovaných prvků jízdního řádu funguje **kaskáda stylů**: nejčastěji měněné
barvy (pozadí a text záhlaví/dat) nastavíte rychle přímo v panelu Vlastnosti; detailní styl po
jednotlivých sloupcích (font, velikost, řez, zarovnání, odsazení, dekorace) se nastavuje
v dialogu sloupců.

- Co v dialogu sloupce nenastavíte, se **zdědí** z nastavení celého prvku — v dialogu se to
  zobrazí jako šedý kurzívou psaný náznak zděděné hodnoty.
- Pokud hodnotu explicitně přepíšete, objeví se u pole tlačítko „×" pro rychlý návrat ke
  zděděné hodnotě.
- Tlačítka „Resetovat styl" (jeden sloupec) a „Resetovat všechny styly" v dialogu vrátí
  nastavení do výchozího stavu.

---

## 8. Datová pole a výrazový jazyk

### Placeholdery (databázová pole)
Kdekoli v textu napíšete pole ve složených závorkách, např. `Linka {CISLO_LINKY}`. Při tisku se
`{CISLO_LINKY}` nahradí skutečnou hodnotou. Dostupná pole pro aktuální agendu vidíte v panelu
„Pole agendy" a můžete je přetáhnout přímo na plátno.

### Formátování hodnot
V placeholderu lze hodnotu naformátovat zápisem `{POLE:FORMÁT}`:

| Formát | Význam |
|---|---|
| `UPPER` | velká písmena |
| `LOWER` | malá písmena |
| `TRIM` | oříznutí mezer |
| `LEFT(n)` | prvních n znaků |
| `RIGHT(n)` | posledních n znaků |
| `PADLEFT(n)` / `PADLEFT(n,'znak')` | doplnění zleva na délku n (výchozí mezerou) |
| `PADRIGHT(n)` / `PADRIGHT(n,'znak')` | doplnění zprava na délku n |
| `N2`, `C2`, `P1` | standardní číselné formáty (2 des. místa, měna, procenta…) |
| `dd.MM.yyyy`, `HH:mm` | formát data / času |

Formátování čísel a datumů respektuje **jazyk nastavený v šabloně** (viz níže), ne systémové
nastavení Windows dané stanice. Chybný formát se tiše ignoruje — vypíše se nezformátovaná
hodnota, tisk tedy kvůli překlepu ve formátu nespadne.

### Zalomení řádku v textu (`\n`) — víceřádkové záhlaví
Text prvku i buňky/záhlaví tabulky může mít **víc řádků**. Nový řádek zapíšete dvěma způsoby:

- napíšete `\n` (zpětné lomítko + písmeno n) přímo do textu, např. `Zastávka\nStation`,
- nebo stisknete **Enter** v příslušném poli.

Typické využití je **dvouřádkové (dvojjazyčné) záhlaví sloupce** — první řádek česky, druhý
cizojazyčně (např. „Zastávka" / „Station"). U tabulkových prvků se **výška řádku záhlaví
automaticky přizpůsobí** počtu řádků, takže se text vždy vejde. Funguje v textových prvcích
i v záhlaví a buňkách tabulek a specializovaných prvků jízdního řádu.

### Podmíněná viditelnost
Prvek nebo sekci lze zobrazit jen za podmínky — pole **Podmíněná viditelnost**, např.:
- `{POCET} > 0` — zobrazit jen když je počet kladný,
- `{PAGE} > 1` — jen od druhé stránky.

Podporované operátory: `&&` (a zároveň), `||` (nebo), `!` (negace), `==`, `!=`, `>`, `<`, `>=`,
`<=`, závorky, textové literály, čísla, `true`/`false`. Prázdný nebo neplatný výraz znamená
„vždy zobrazit" (bezpečné chování) — pokud je zápis chybný, zobrazí se u pole červené
upozornění, ať víte, že to potřebuje opravit.

### Agregace v tabulkách
V obsahu buňky dynamické tabulky lze počítat souhrny: `SUM({POLE})`, `COUNT()`, `AVG({POLE})`,
`MIN({POLE})`, `MAX({POLE})`.

### Jazyk šablony (lokalizace)
Šablona má nastavený **jazyk** — dialog `Vlastnosti stránky…` (Ctrl+Shift+P), pole „Formát čísel
a datumů": Čeština (cs-CZ), Slovenština (sk-SK), Angličtina (en-US), Němčina (de-DE). Formát
čísel a datumů se řídí tímto nastavením nezávisle na tom, na jakém počítači se tiskne — tisk tak
vypadá stejně na každé stanici.

---

## 9. Náhled, export a tisk

### Živý náhled
Pravý panel ukazuje **výsledný tisk** s ukázkovými daty agendy — stejným enginem, jaký se použije
při reálném tisku. Na rozdíl od plátna respektuje podmíněnou viditelnost, takže vidíte přesně to,
co se vytiskne.

### Vícestránkové sestavy
Stránkování řeší systém automaticky: hlavičky/patičky se opakují podle nastaveného chování sekcí,
dlouhé tabulky se dělí přes stránky, v náhledu jsou stránky zobrazené pod sebou se stíny a
rámečky.

### Varování o přetečení
Pokud se obsah nevejde na stránku, dostane stránka v náhledu **červený rámeček** a status bar
zobrazí „⚠ Obsah se nevejde na stránku" (tooltip navrhne zvětšit stránku nebo zmenšit obsah).
Varování se týká jen sekcí nastavených jako „Nepřerušovat" — sekce, které lze rozdělit přes
stránky, se prostě rozdělí a nic se neztratí, takže tam varování nedává smysl.

### Export
- `Soubor → Exportovat náhled jako PDF…` / `…jako PNG…` — export aktuálního náhledu
  (víceřánkově), běží asynchronně nad snímkem šablony pořízeným v okamžiku kliknutí (další
  editace mezitím výsledek neovlivní).

### Tisk
`Soubor → Tisk…` (Ctrl+P) otevře dialog „Tisk šablony" s parametry, které se generují dynamicky
podle zvolené agendy (bez agendy: „Šablona nemá žádné parametry."). Dialog obsahuje i výběr
instancí (např. linek) k tisku a tlačítka „Tisk všech vybraných", „Export náhledu", „Zavřít".

Nastavení výstupu je univerzální napříč agendami:
- **Členění výstupu** — Vše do jednoho PDF / Jeden PDF na instanci / (u agend s více úrovní dat
  i jemnější dělení, viz dokument dané agendy).
- **Název souboru (tokeny {POLE})** — vlastní kombinace datových polí pro pojmenování
  výsledných souborů; při rozdělení výstupu na víc souborů se vybírá cílová složka, kolize jmen
  se řeší automaticky příponou `_2`, `_3`…
- **Uspořádání na list (imposice)** — více vývěsek na jeden fyzický list: cílový list
  (Vypnuto / A4 / A3, na výšku i na šířku), **mřížka** (AUTO = největší počet, který se vejde
  bez zmenšování, nebo pevně 2×1, 2×2…), **„Každá stránka na celý list"** (táž vývěska
  vícekrát — pro rozřezání) a **ořezové značky**. Typicky: dvě A5 vývěsky na jeden list A4.
  Volba se pamatuje pro každou agendu zvlášť; návrat k běžnému tisku = „Vypnuto".
  (Export PNG imposici nepoužívá; brožurové řazení a oboustranný tisk zůstávají na tiskárně.)

---

## 10. Import z PDF

`Nástroje → Import PDF…` — z existujícího PDF vytvoří editovatelnou šablonu. Dialog je záměrně
jednoduchý: vyberete **PDF soubor** (a u vícestránkového stránku) — všechno ostatní se odvodí
z právě otevřené agendy:

- **Agenda jízdního řádu** → import s **umělou inteligencí**: AI rozpozná strukturu vývěsky
  (seznam zastávek, mřížku odjezdů, legendu poznámek, texty, loga) a rovnou namapuje datová
  pole agendy — výsledkem je živá šablona napojená na data, ne statická kopie. Importuje se
  vždy **jedna stránka** (jedna vývěska jako předloha). Vyžaduje aktivovanou AI službu u M-line;
  bez ní je import u JŘ agend nedostupný.
- **Jiná agenda (Obecná, Faktury…)** → obecný import: texty, čáry a obrázky se převezmou 1:1
  jako prvky šablony (bez AI).

**Berte to jako rychlý start, ne záruku přesné shody s originálem** — výsledek zkontrolujte
a dolaďte; AI rozpoznání se může u netypických layoutů splést (import lze spustit znovu).

---

## 11. Uložená nastavení

Následující se ukládá centrálně (podle firmy a přihlášeného uživatele), ne jen lokálně na
stanici — po přihlášení na jiném počítači je nastavení stejné:

- **Naposledy otevřené soubory** (import JSON) — až 10 položek, submenu „Otevřít poslední JSON".
- **Naposledy použité barvy** — až 8 položek, sdílené napříč všemi barevnými výběry v aplikaci.
- **Nastavení editoru** — velikost mřížky, zapnuté přichytávání, viditelnost panelů, zoom, aktivní
  záložka levého panelu — obnoví se při příštím spuštění.
- **Výchozí hodnoty instance** — naposledy zvolené parametry/instance se pamatují per šablona a
  předvyplní se při dalším otevření stejné šablony.
- **Parametry tisku** — pamatují se per agenda (sdílené napříč šablonami stejné agendy).

Ukládání běží na pozadí; případná chyba uložení se uživateli aktivně nehlásí (nejde o kritickou
operaci).

---

## 12. Klávesové zkratky

| Zkratka | Akce |
|---|---|
| Ctrl+N | Nová šablona |
| Ctrl+O | Otevřít z databáze |
| Ctrl+S | Uložit (do databáze) |
| Ctrl+Z / Ctrl+Y | Zpět / Vpřed |
| Ctrl+X / Ctrl+C / Ctrl+V | Vyjmout / Kopírovat / Vložit |
| Ctrl+D | Duplikovat (kopie s malým posunem) |
| Ctrl+D + šipka | Duplikovat vedle (s mezerou) |
| Ctrl+Shift+C / Ctrl+Shift+V | Kopírovat / Vložit styl |
| Delete | Smazat výběr |
| Ctrl+A | Vybrat vše v aktivní sekci |
| Enter / dvojklik | Vlastnosti výběru (1 prvek = dialog; víc prvků = dialog společných vlastností) |
| F2 | Rychlá editace textu (jen textový prvek, jeden vybraný) |
| Ctrl+G / Ctrl+Shift+G | Seskupit / Rozeskupit |
| Alt+klik | Vybrat jeden prvek ze skupiny |
| Ctrl+Shift+H / Ctrl+Shift+J | Rozdělit prvek vodorovně / svisle |
| Ctrl+Shift+N | Normalizovat pozice |
| Šipky | Posun prvku o 1 mm (Shift = o krok mřížky, Ctrl = jemně 0,5 mm) |
| Ctrl+PageUp / Ctrl+PageDown | Vrstva dopředu / dozadu (+Shift = zcela na kraj) |
| Šipka ↑/↓ (bez výběru prvku) | Přesun aktivní sekce nahoru / dolů |
| Ctrl+0 | Přizpůsobit zobrazení stránce („Fit") |
| Ctrl+1 | Skutečná velikost 100 % („1:1") |
| Ctrl + kolečko myši | Plynulý zoom (k pozici kurzoru) |
| Ctrl+B | Sbalit/rozbalit levý panel |
| Ctrl+P | Tisk |
| Ctrl+Shift+P | Vlastnosti stránky |
| Ctrl+Shift+A | Změnit agendu |
| F5 | Obnovit data z databáze |
| F1 | Nápověda |
| Escape | Zrušit probíhající operaci (např. přidávání prvku) |

Poznámka: „Uložit jako" (do databáze) je dostupné jen z menu `Soubor → Uložit do databáze jako…`,
bez vlastní klávesové zkratky.

---

## 13. Časté otázky

**Můžeme si šablonu upravit sami?**
Ano — to je hlavní smysl Návrháře. Vizuálně upravíte a uložíte do databáze, změna je hned vidět
v náhledu.

**Co se starými šablonami?**
Tato verze je neimportuje 1:1. Nové šablony se navrhnou znovu — díky vizuálnímu editoru rychle;
pomůckou je import z existujícího PDF s AI rozpoznáním (kap. 10).

**Liší se formát čísel/datumů podle počítače?**
Ne. Řídí se jazykem nastaveným v šabloně, ne nastavením Windows dané stanice.

**Kam se ukládají šablony?**
Do centrální databáze, sdílené v rámci firmy — ne na jednotlivé počítače. Export do souboru JSON
je jen doplňková možnost (např. pro poslání e-mailem), ne hlavní úložiště.

**Co když omylem smažu sekci nebo prvek?**
Ctrl+Z vrátí prakticky jakoukoli akci. Mazání celé sekce s obsahem navíc vyžaduje potvrzení,
takže k němu nedojde omylem jedním kliknutím.

**Jak poznám, že se obsah nevejde na stránku?**
Náhled zobrazí červený rámeček stránky a status bar hlášku „⚠ Obsah se nevejde na stránku" —
řešením je buď zvětšit prostor (stránku/sekci), nebo zmenšit obsah (méně sloupců, menší písmo).

---

# ČÁST II — Zastávkový jízdní řád

> Navazuje na část I — obecné ovládání (šablony, sekce, prvky, styly, export) tady
> neopakujeme. Tato část popisuje jen to, co je specifické pro agendu **zastávkový jízdní
> řád** (vývěska na zastávce: seznam odjezdů z jedné zastávky v jednom směru).

---

## Obsah části II

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
(Ctrl+Shift+A) nebo klik na zelený štítek. Přepnutí založí novou prázdnou šablonu (viz část I, kap. 3).

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
  pro záhlaví a data — viz kaskáda stylů v části I, kap. 7.
- **Dvouřádkové záhlaví** — do pole „Záhlaví" lze zadat víc řádků (např. český a cizojazyčný
  název sloupce, „Zastávka" / „Station"): napište `\n` nebo stiskněte Enter. Výška záhlaví se
  přizpůsobí automaticky. Detail viz část I, kap. 8.
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
včetně opakování téže vývěsky pro řezání a ořezových značek; detail viz část I, kap. 9.

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
pravidlem jako v části I (kap. 9): objeví se jen u sekcí nastavených
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
