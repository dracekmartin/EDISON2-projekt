# Návrhář šablon — uživatelská příručka

> Vizuální editor tiskových sestav v EDISONu. Navrhnete vzhled šablony, systém k ní doplní
> data a vytiskne PDF. Příručka pokrývá celou práci od založení šablony po tisk jízdních řádů.

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
13. [Zastávkový jízdní řád](#13-zastávkový-jízdní-řád)
14. [Knižní a linkový jízdní řád](#14-knižní-a-linkový-jízdní-řád)
15. [Časté otázky](#15-časté-otázky)

---

## 1. Co je Návrhář šablon

Návrhář šablon je vizuální editor tiskových sestav — co vidíte při návrhu, to dostanete na
výstupu. Nahrazuje starší nástroj, kde se sestavy nastavovaly přes formuláře a číselné parametry
bez náhledu a výsledek byl vidět až po vytištění.

Systém má dvě vrstvy:

1. **Návrhář** — zde vizuálně navrhnete šablonu (rozložení stránky, texty, tabulky, jízdní řády).
2. **Tiskový engine** — vezme šablonu a reálná data a vytvoří PDF.

Šablona popisuje jen **vzhled**. Konkrétní hodnoty (čísla linek, časy, názvy zastávek) doplní
systém z databáze až při tisku. Živý náhled v Návrháři používá tentýž tiskový engine jako reálný
tisk, takže náhled odpovídá výslednému výstupu.

### Základní pojmy

Pár termínů, které se v příručce opakují a mají tu konkrétní, ne úplně samozřejmý význam:

| Pojem | Význam |
|---|---|
| **Šablona** | Popis vzhledu sestavy (rozložení, texty, tabulky) uložený v databázi. Bez dat — ta se doplní až při tisku. |
| **Sekce** | Vodorovný pruh stránky, do kterého se umisťují prvky; má nastavené chování, kdy a jak často se tiskne (kap. 4). |
| **Prvek (element)** | Jednotlivý objekt na plátně — text, obrázek, tabulka, čára, kód nebo specializovaný prvek jízdního řádu (kap. 5). |
| **Agenda** | Zdroj dat, nad kterým šablona pracuje — např. zastávkový jízdní řád nebo faktury. Určuje, jaká datová pole a prvky máte k dispozici (kap. 3). |
| **Datové pole / placeholder** | Zápis `{POLE}` v textu, který se při tisku nahradí skutečnou hodnotou z databáze (kap. 8). |
| **Instance** | Konkrétní položka dat k vytištění v rámci agendy — např. jedna linka (kap. 9, 13). |

---

## 2. Orientace v aplikaci

Okno má čtyři hlavní části: **toolbar** nahoře, **levý panel** se záložkami, **plátno** uprostřed
a **náhled** vpravo.

### Toolbar

| Skupina | Obsah |
|---|---|
| Soubor | Nová šablona, Otevřít, Uložit, Tisk |
| Upravit | Zpět, Vpřed |
| Sekce | Přidat sekci (nabídka připravených typů) |
| Přidat | paleta prvků — Text, Obdélník, Čára, Obrázek, Tabulka, Kód; u jízdních řádů navíc specializované prvky JŘ (nabídka se přizpůsobí zvolené agendě, viz kap. 13–14) |
| Vrstva | Přenést dopředu / Odeslat dozadu / Přenést zcela dopředu / Odeslat zcela dozadu |
| Zarovnat | zarovnání vlevo/na střed/vpravo, nahoru/na střed/dolů, rovnoměrné rozložení (min. 3 vybrané prvky) |
| Zobrazení | mřížka, přichytávání k objektům, velikost mřížky, velikost mezery pro přichytávání, zoom, tlačítka Fit a 1:1, skrytí panelu sekcí, přepínač živého náhledu |

### Levý panel — záložky

| Záložka | K čemu |
|---|---|
| Sekce | seznam sekcí šablony — přidávání, řazení, mazání |
| Pole agendy | dostupná datová pole pro aktuální agendu; dvojklik nebo přetažení vloží pole na plátno; hlavička zobrazuje název agendy a klikem otevře její přepnutí |
| Vlastnosti | nastavení právě vybraného prvku nebo sekce s okamžitou editací |
| Strom | přehled celé šablony (sekce → skupiny prvků → prvky) pro rychlou navigaci |
| Pomocník | asistent, který pomáhá s návrhem šablony |

### Status bar

Zleva: štítek s aktuální **agendou** (klik otevře její přepnutí), štítek **vybrané instance**
(např. konkrétní linky) s šipkami pro přepínání mezi víc vybranými položkami, informace
o aktuálním výběru, průběžné hlášky a — pokud obsah nesedí na stránku — upozornění na přetečení.

### Hlavní menu

- **Soubor** — Nová šablona · Otevřít z databáze… · Uložit do databáze · Uložit do databáze
  jako… · Importovat z JSON souboru… · Exportovat do JSON souboru… · Otevřít poslední JSON ·
  Exportovat náhled jako PDF… · Exportovat náhled jako PNG… · Tisk… · Vlastnosti stránky… · Konec
- **Úpravy** — Zpět · Vpřed · Smazat · Pořadí vrstev · Zarovnat · Rozložit · Normalizovat pozice… ·
  Škálovat šablonu · Kopírovat styl · Vložit styl
- **Zobrazit** — Mřížka, Přichytit na mřížku, Přichytit k elementům, Záhlaví sekcí, Pravítka,
  Panel sekcí, Live náhled
- **Nástroje** — Změnit agendu… · Šablony v databázi… · AI Asistent šablon… · Import PDF…
- **Nápověda** — Klávesové zkratky a ovládání

Titulek okna ukazuje agendu, zda je šablona uložená v databázi, její název a hvězdičku při
neuložených změnách.

---

## 3. Práce se šablonou

### Nová šablona
`Soubor → Nová` (Ctrl+N) založí šablonu s jednou výchozí sekcí přes celou tiskovou plochu; sekce
a prvky pak přidáváte podle potřeby.

### Agenda — nad jakými daty šablona pracuje
Každá šablona je vázaná na **agendu**, tedy zdroj dat (např. zastávkový jízdní řád, knižní jízdní
řád, faktury). Agenda určuje, jaká datová pole a jaké specializované prvky máte k dispozici.

- Aktuální agenda je vidět ve status baru, v titulku okna a v hlavičce panelu „Pole agendy".
- Změna agendy: `Nástroje → Změnit agendu…`, klávesa **Ctrl+Shift+A**, nebo klik na štítek agendy
  ve status baru.
- Přepnutí agendy vždy založí **novou prázdnou šablonu** — obsah stávající šablony se nepřevádí.
  Rozdělanou práci vás systém před přepnutím vyzve uložit nebo zahodit.

### Otevření a uložení
Šablony se ukládají do centrální databáze podle firmy, ne do souborů na jednotlivých počítačích:

- `Soubor → Otevřít z databáze…` — dialog se seznamem šablon, filtrem podle agendy a možností
  přejmenovat nebo smazat.
- `Soubor → Uložit do databáze` — u již uložené šablony provede tichou aktualizaci.
- `Soubor → Uložit do databáze jako…` — dialog s polem pro název a popis; takto se nová nebo
  importovaná šablona poprvé uloží do databáze.

Díky databázi jsou šablony sdílené a dohledatelné napříč firmou.

### Import a export do souboru
Šablonu lze uložit i jako soubor JSON — doplňková možnost vedle exportu do PDF/PNG, ne náhrada
databázového úložiště. Hodí se například pro poslání šablony e-mailem.

- `Soubor → Importovat z JSON souboru…` — otevře soubor; šablona se dál chová jako nová
  neuložená.
- `Soubor → Exportovat do JSON souboru…` — uloží aktuální stav do souboru; pokud šablona pochází
  z databáze, tam zůstává uložená i po exportu.

---

## 4. Sekce a struktura stránky

Stránka je rozdělená na **sekce** — vodorovné pruhy, do kterých se umisťují prvky. Sekce má
nastavené chování, které určuje, **kdy a jak často** se tiskne a co se stane **při přetečení**
obsahu.

### Přidání sekce
Tlačítko „+ Přidat sekci" nabízí připravené typy:

| Typ | Chování | Výchozí výška |
|---|---|---|
| Hlavička stránky | každá stránka | 25 mm |
| Patička stránky | každá stránka | 15 mm |
| Oddělovač | jednou | 5 mm |
| Hlavička sestavy — jen 1. str. | jen první stránka | 30 mm |
| Patička sestavy — jen posl. str. | jen poslední stránka | 20 mm |
| Hlavička kromě 1. str. | všechny kromě první | 25 mm |
| Prázdná sekce | jednou | 30 mm |
| vlastní uložené šablony sekcí | podle uložené šablony | podle uložené šablony |

### Vlastnosti sekce
Dialog vlastností obsahuje: **Název**, **Výška**, **Tisk**, **Umístění**, **Přetečení**,
**Rámeček**, **Barva rámečku**, **Tloušťka rámečku**, **Viditelnost**.

- **Tisk**: Jednou · Každá stránka · Jen první · Jen poslední · Kromě první
- **Umístění**: Obsah · Hlavička · Patička (patičky se umisťují od spodního okraje stránky)
- **Přetečení**: Nepřerušovat · Přes stránky · Povolit split

Sekce s opakujícím se řádkem dat má navíc pole **Datový zdroj** a **Výška řádku**.

### Práce se sekcemi
- Poslední zbývající sekci nelze smazat; sekce s prvky vyžaduje potvrzení smazání. Uvolněná
  výška se proporcionálně rozdělí mezi zbývající sekce (a naopak při přidání nové).
- Řazení přetažením v levém panelu nebo šipkami v kontextovém menu.
- Často používanou sekci lze uložit jako vlastní šablonu (pravý klik → „Uložit jako šablonu…") a
  opakovaně vkládat do dalších šablon.

---

## 5. Práce s prvky (elementy)

| Prvek | Hlavní vlastnosti |
|---|---|
| Text | text (i víceřádkový), rodina a velikost písma, tučné/kurzíva/podtržení/přeškrtnutí, zalamování, řádkování, zarovnání vodorovně i svisle, barva textu a pozadí |
| Obrázek | nahrání souboru (limit 5 MB, větší se automaticky zmenší), volitelné načtení z datového pole, škálování: vejít celý / vyplnit (oříznout) / roztáhnout / bez úpravy |
| Obdélník | výplň, barva a tloušťka okraje, zaoblení rohů |
| Čára | orientace, barva, tloušťka, styl (plná/čárkovaná/tečkovaná), délka čárky a mezery |
| Kód | obsah a typ — QR kód, Code 128, EAN-13, EAN-8, PDF417, DataMatrix — barva kódu a pozadí |
| Tabulka | řádky se generují automaticky z vybrané datové kolekce (viz níže) |
| Prvky jízdního řádu | specializované prvky pro tisk JŘ, viz kap. 13–14 |

### Tabulka
Sloupce definujete jednou, řádky doplní systém z dat. Vlastnosti: tlačítko „Upravit sloupce…",
zdroj dat, výška řádku, záhlaví a jeho výška, rámeček (Žádný/Vnější/Vše/Řádky/Sloupce), střídání
řádků, tučné záhlaví, barvy pozadí a textu záhlaví i dat.

Doplňkové možnosti sloupců:

- **Počítaný sloupec** — hodnota se dopočítá výrazem nad ostatními poli řádku, například
  `{CENA}*{POCET}`, a dá se naformátovat stejně jako běžné pole.
- **Minimální a maximální počet řádků** — pro tabulky s pevným vzhledem (např. faktura) lze
  nastavit, že se prázdné řádky doplní do minimálního počtu, nebo že se výpis ořízne na maximální
  počet.
- **Podmíněná viditelnost řádku** — řádek, který nesplňuje zadanou podmínku, se v tisku vůbec
  neobjeví.
- **Podmíněné zvýraznění řádku** — řádky splňující zadanou podmínku (např. záporná částka) se
  automaticky podbarví zvolenou barvou.

Editor sloupců (dialog „Tabulka — nastavení") má záložky **Obecné** a **Sloupce**: přidání/
odebrání sloupce, upozornění při přesahu šířky s tlačítkem pro automatické přizpůsobení, a pro
každý sloupec záhlaví, šířku, mapované pole a plný editor stylu (viz kap. 7).

### Přidání prvku
- Klik na tlačítko v paletě vloží prvek do aktivní sekce.
- Přetažením z palety přímo na plátno — během tažení se zvýrazní cílová sekce.
- Přetažením datového pole z panelu „Pole agendy" na plátno vznikne nový textový prvek s daným
  polem; přetažením na existující text se pole připojí k jeho obsahu. Dvojklik na pole funguje
  jako alternativa k přetažení.

### Výběr, přesun, velikost
- Klik prvek vybere, tažení ho přesune, úchyty na okrajích mění velikost.
- Víc prvků vyberete tažením výběrového rámečku nebo Ctrl+klikem.
- Prvek se nepřesune ani nezvětší mimo hranice své sekce.

### Seskupování
Ctrl+G spojí vybrané prvky do skupiny, Ctrl+Shift+G skupinu zruší. Klik vybere celou skupinu,
Alt+klik jen jeden prvek uvnitř. Import z PDF prvky automaticky seskupuje podle bloků textu.

---

## 6. Editační nástroje

### Přichytávání
Tři nezávislé pomůcky pro přesné umístění, zapínané z toolbaru:
- **Mřížka** — nastavitelná velikost (0,2–10 mm, výchozí 5 mm).
- **Přichytávání k objektům** (výchozí zapnuto) — prvek se přichytává k hranám a středům jiných
  prvků i k okrajům sekce.
- **Mezera pro přichytávání** — prvek se umí přichytit i v definované mezeře od souseda
  (0–10 mm, výchozí 2 mm), s vizuálním indikátorem.

### Duplikování
- **Ctrl+D** vytvoří kopii s malým posunem.
- **Ctrl+D + šipka** umístí kopii ve zvoleném směru s mezerou podle aktuálního nastavení
  přichytávání.

### Kopírování stylu
**Ctrl+Shift+C** zkopíruje styl (font, rámeček, pozadí) vybraného prvku, **Ctrl+Shift+V** ho
aplikuje na jiné vybrané prvky.

### Hromadné úpravy
Výběr více prvků a **Enter** otevřou dialog společných vlastností (barva a šířka rámečku, pořadí
vrstvy, zamčení) — změna se aplikuje na všechny vybrané najednou.

### Normalizace pozic
**Ctrl+Shift+N** — dialog s volbou kroku zaokrouhlení a náhledem počtu ovlivněných prvků; opravuje
drobné nepřesnosti pozic.

### Škálování šablony
Dropdown v toolbaru nebo `Úpravy → Škálovat šablonu` (50–200 %) proporcionálně přepočítá rozměr
stránky, okraje, sekce i všechny prvky včetně jejich fontů a odsazení.

### Zpět a znovu
**Ctrl+Z** / **Ctrl+Y** — každá úprava je vratná.

---

## 7. Vlastnosti a styly

Vybraný prvek nastavíte v záložce **Vlastnosti** levého panelu, nebo v dialogu po stisku
**Enter** či dvojkliku na prvek — obsah je stejný, panel je jen rychlejší cesta.

### Rámeček a pozadí
Prvek může mít rámeček (barva a tloušťka) a barvu pozadí. Když dva prvky sdílejí hranu, vykreslí
se jako jeden společný rámeček, ne zdvojeně.

### Styl u tabulek a jízdních řádů
U tabulek a prvků jízdního řádu se nejčastěji měněné barvy (pozadí a text záhlaví/dat) nastavují
přímo v panelu Vlastnosti; detailní styl jednotlivých sloupců (font, velikost, řez, zarovnání,
odsazení) se nastavuje v dialogu sloupců. Co u konkrétního sloupce nenastavíte, se převezme
z celkového nastavení prvku; pokud hodnotu přepíšete, dá se jedním tlačítkem vrátit zpět na
převzatou.

---

## 8. Datová pole a výrazový jazyk

### Placeholdery (databázová pole)
Kdekoli v textu napíšete pole ve složených závorkách, například `Linka {CISLO_LINKY}`. Při tisku
se nahradí skutečnou hodnotou. Dostupná pole vidíte v panelu „Pole agendy" a lze je přetáhnout
přímo na plátno.

### Formátování hodnot
Zápisem `{POLE:FORMÁT}` lze hodnotu naformátovat:

| Formát | Význam |
|---|---|
| `UPPER` | velká písmena |
| `LOWER` | malá písmena |
| `TRIM` | oříznutí mezer |
| `LEFT(n)` | prvních n znaků |
| `RIGHT(n)` | posledních n znaků |
| `PADLEFT(n)` / `PADLEFT(n,'znak')` | doplnění zleva na délku n |
| `PADRIGHT(n)` / `PADRIGHT(n,'znak')` | doplnění zprava na délku n |
| `N2`, `C2`, `P1` | číselné formáty (desetinná místa, měna, procenta) |
| `dd.MM.yyyy`, `HH:mm` | formát data a času |

Formátování čísel a datumů respektuje jazyk nastavený v šabloně (viz níže). Pokud je zápis
formátu chybný, vypíše se nezformátovaná hodnota, aby tisk kvůli překlepu nespadl.

### Podmíněná viditelnost
Prvek nebo sekci lze zobrazit jen za splnění podmínky, například `{POCET} > 0` nebo `{PAGE} > 1`.
Podporované operátory: `&&` (a zároveň), `||` (nebo), `!` (negace), `==`, `!=`, `>`, `<`, `>=`,
`<=`, závorky, textové literály, čísla, `true`/`false`. Prázdný nebo neplatný výraz znamená
„vždy zobrazit"; u chybného zápisu se navíc zobrazí upozornění, ať víte, že to potřebuje opravu.

### Aritmetika a agregace
V obsahu buňky tabulky lze počítat s hodnotami řádku (např. `{CENA}*{POCET}`) i sčítat přes
všechny řádky: `SUM({POLE})`, `COUNT()`, `AVG({POLE})`, `MIN({POLE})`, `MAX({POLE})`.

### Jazyk šablony
Dialog `Vlastnosti stránky…` (Ctrl+Shift+P), pole „Formát čísel a datumů": Čeština, Slovenština,
Angličtina, Němčina. Formát čísel a datumů se řídí tímto nastavením bez ohledu na to, na jakém
počítači se tiskne — výstup tak vypadá stejně na každé stanici.

---

## 9. Náhled, export a tisk

### Živý náhled
Pravý panel ukazuje výsledný tisk s ukázkovými daty — stejným způsobem, jakým se sestaví reálný
tisk, včetně vyhodnocení podmíněné viditelnosti.

### Vícestránkové sestavy
Stránkování řeší systém automaticky: hlavičky a patičky se opakují podle nastavení sekcí, dlouhé
tabulky se dělí přes stránky, náhled zobrazuje stránky pod sebou.

### Upozornění na přetečení
Pokud se obsah nevejde na stránku, dostane stránka v náhledu červený rámeček a status bar
zobrazí upozornění s doporučením zvětšit prostor nebo zmenšit obsah. Týká se to jen sekcí
nastavených jako „Nepřerušovat" — sekce, které lze rozdělit přes stránky, se prostě rozdělí, takže
tam k upozornění nedochází.

### Export
`Soubor → Exportovat náhled jako PDF…` / `…jako PNG…` — export aktuálního náhledu (i
vícestránkového).

### Tisk
`Soubor → Tisk…` (Ctrl+P) otevře dialog s parametry, které se přizpůsobí zvolené agendě, výběrem
tiskových instancí (např. konkrétních linek) a tlačítky pro tisk vybraných položek nebo export
náhledu.

Nastavení výstupu platí napříč agendami:
- **Členění výstupu** — vše do jednoho souboru, nebo samostatný soubor na každou tiskovou
  jednotku (linku, případně zastávku a směr — podle agendy).
- **Název souboru** — lze sestavit z datových polí, výchozí kombinace se liší podle agendy.

Pokud se výstup dělí do víc souborů, vybírá se cílová složka a případná shoda jmen se řeší
automatickým číslováním.

---

## 10. Import z PDF

`Nástroje → Import PDF…` umí z existujícího PDF vytáhnout texty, čáry a obrázky a předpřipravit
základ nové šablony — volitelně s pomocí umělé inteligence pro lepší rozpoznání struktury
dokumentu. Berte to jako pomůcku pro rychlejší start, ne jako záruku přesné shody s originálem —
výsledek je vždy potřeba v Návrháři zkontrolovat a doladit.

---

## 11. Uložená nastavení

Nastavení se ukládá centrálně podle firmy a přihlášeného uživatele, takže je stejné i po
přihlášení na jiném počítači:

- naposledy otevřené soubory (import JSON),
- naposledy použité barvy,
- nastavení editoru — velikost mřížky, zapnuté přichytávání, viditelnost panelů, zoom,
- naposledy zvolené parametry a instance pro konkrétní šablonu,
- parametry tisku pro danou agendu.

---

## 12. Klávesové zkratky

| Zkratka | Akce |
|---|---|
| Ctrl+N | Nová šablona |
| Ctrl+O | Otevřít z databáze |
| Ctrl+S | Uložit do databáze |
| Ctrl+Z / Ctrl+Y | Zpět / Vpřed |
| Ctrl+X / Ctrl+C / Ctrl+V | Vyjmout / Kopírovat / Vložit |
| Ctrl+D | Duplikovat |
| Ctrl+D + šipka | Duplikovat vedle (s mezerou) |
| Ctrl+Shift+C / Ctrl+Shift+V | Kopírovat / Vložit styl |
| Delete | Smazat výběr |
| Ctrl+A | Vybrat vše v aktivní sekci |
| Enter / dvojklik | Vlastnosti výběru |
| F2 | Rychlá editace textu (jeden vybraný textový prvek) |
| Ctrl+G / Ctrl+Shift+G | Seskupit / Rozeskupit |
| Alt+klik | Vybrat jeden prvek ze skupiny |
| Ctrl+Shift+H / Ctrl+Shift+J | Rozdělit prvek vodorovně / svisle |
| Ctrl+Shift+N | Normalizovat pozice |
| Šipky | Posun vybraného prvku o krok |
| Ctrl+PageUp / Ctrl+PageDown | Vrstva dopředu / dozadu (se Shift zcela na kraj) |
| Šipka ↑/↓ (bez výběru prvku) | Přesun aktivní sekce nahoru / dolů |
| Ctrl+0 | Přizpůsobit zobrazení stránce |
| Ctrl+1 | Skutečná velikost (100 %) |
| Ctrl + kolečko myši | Plynulý zoom |
| Ctrl+B | Sbalit/rozbalit levý panel |
| Ctrl+P | Tisk |
| Ctrl+Shift+P | Vlastnosti stránky |
| Ctrl+Shift+A | Změnit agendu |
| F1 | Nápověda |
| Escape | Zrušit probíhající operaci |

„Uložit do databáze jako…" nemá vlastní klávesovou zkratku, je dostupné jen z menu Soubor.

---

## 13. Zastávkový jízdní řád

Zastávkový jízdní řád je sestava, která se vylepuje na konkrétní zastávku — obsahuje seznam
zastávek trasy a mřížku minut odjezdů **z jedné zastávky v jednom směru**. Skládá se ze tří
specializovaných prvků, které samy zvládají velkou část logiky jízdního řádu — vy nastavujete
hlavně vzhled, obsah systém dopočítá z dat.

### Dva zdroje dat
Agendu lze založit nad dvěma zdroji dat, které používají stejné tři prvky, jen s drobnými
rozdíly v dostupných možnostech:

- **Zastávkový JŘ MHD** — data z modulu městské hromadné dopravy.
- **Zastávkový JŘ AD** — data z hlavního modulu Jízdní řády (typicky příměstská/regionální
  doprava).

Speciálně u zdroje AD je navíc k dispozici:
- **Automatické rozdělení sloupců podle typu provozu** (pracovní den / sobota / neděle a svátky,
  případně s rozlišením prázdnin) s možností ho v tiskovém dialogu ručně přepsat na jinou
  kombinaci.
- **Zobrazení více variant času jízdy**, pokud různé spoje jedou stejným úsekem různě dlouho —
  přepínač na prvku Tabulka zastávek.
- **Zkrácené názvy zastávek a směru** z centrálního číselníku vedle plného lokálního názvu.
- **Údaje o dopravci včetně adresy** provozovny, pokud pro něj není nastavený vlastní zkrácený
  název.

### Tabulka zastávek
Levý sloupec sestavy — seznam zastávek s časy a atributy. Nastavuje se v ní mimo jiné:
- pořadí a vzhled sloupců (tlačítko „Upravit sloupce…"), zobrazení variant tras,
- oddělovací čáry mezi řádky, odsazení názvu podle pozice v trase,
- značka „na znamení", zobrazení atributů a poznámek zastávky a jejich umístění před nebo za
  názvem,
- grafická šipka směru jízdy,
- zvýraznění aktuální zastávky (tučně, podtržením, vlastní velikostí písma, podbarvením celého
  řádku),
- volba, zda tisknout i zastávky, které vozidlo od aktuální zastávky už projelo,
- **podmíněná viditelnost** a **podmíněné zvýraznění** řádku zastávky podle výrazu nad jejími
  daty — stejný princip jako u obecné tabulky (kap. 5).

### Mřížka odjezdů
Minuty odjezdů rozdělené do sloupců podle skupiny dnů (pracovní den, sobota, neděle…). Nastavuje
se v ní:
- pořadí a vzhled sloupců,
- zobrazení a zarovnání záhlaví skupin, opakování sloupce hodin u každé skupiny,
- časová osa (jen hodiny s odjezdy / souvislý rozsah / vlastní rozsah hodin),
- volitelné proporční zvětšení, pokud je dat méně, než odpovídá výšce mřížky,
- střídavé podbarvení řádků,
- samostatný styl pro horní indexy u minut (týdenní platnost, speciální značky, negativní
  značky, rozdíly tras).

### Legenda poznámek
Vysvětlivky pod mřížkou — nastavuje se:
- nadpis, oddělovač mezi značkou a textem,
- počet sloupců a směr jejich plnění, řazení poznámek,
- které typy poznámek se vůbec zobrazí (negativní značky, speciální znaky, textové poznámky).

Všechny tři prvky mohou mít pevnou nebo automatickou výšku; při pevné výšce se písmo v případě
potřeby přiměřeně zmenší, aby se obsah vešel.

### Výběr linek k tisku
Ve status baru je štítek s aktuální instancí (linkou) — kliknutím se otevře vyhledávací dialog
s možností vybrat víc linek najednou pro dávkový tisk. Mezi vybranými linkami se pak přepíná
šipkami přímo ve status baru, náhled se při přepnutí okamžitě aktualizuje.

### Co systém řeší automaticky
Následující se nenastavuje ručně, spočítá se z dat:
- textové omezení platnosti spoje („jede / nejede"),
- rozdíly v trase mezi spoji (odlišná konečná, vynechané zastávky) — automatické písmenné
  značky a vysvětlující text v legendě,
- relevantní atributy zastávek (např. „na znamení"),
- bezbariérovost spoje,
- popis dopravce včetně adresy, pokud je potřeba,
- zkrácené názvy z centrálního číselníku,
- značky u spojů, které nestaví na všech zastávkách, s vysvětlením v legendě,
- pořadí a bez duplicit sestavená legenda poznámek,
- vynechání stránky pro zastávku, za kterou už v daném směru nikam nejede.

### Tisk a export
Tiskový dialog nabízí parametry podle zvoleného zdroje dat (např. tisk šipek směru, tisk
projetých zastávek, nastavení časové osy, u zdroje AD navíc rozdělení sloupců podle typu
provozu) a nastavení výstupu — členění (vše do jednoho / po lince / po zastávce a směru) a
pojmenování výsledných souborů podle datových polí.

---

## 14. Knižní a linkový jízdní řád

Vedle zastávkového jízdního řádu je k dispozici i agenda pro **knižní/linkový jízdní řád** —
sestavu celé linky na jednu stránku, kde řádky tvoří jednotlivé zastávky a sloupce jednotlivé
spoje s časy. Tento typ výstupu se používá pro knižní jízdní řády a vývěsky s celou trasou linky,
na rozdíl od zastávkového jízdního řádu, který ukazuje jen odjezdy z jedné konkrétní zastávky.

**Tato agenda je v testovacím provozu** — funguje a data odpovídají realitě, ale některé vizuální
detaily se ještě dolaďují.

### Dva zdroje dat
Stejně jako u zastávkového jízdního řádu jsou k dispozici dvě agendy podle zdroje dat — z modulu
městské hromadné dopravy a z hlavního modulu Jízdní řády. Paleta prvků se přizpůsobí zvolené
agendě.

### Nastavení tabulky linky
Prvek nabízí zapnutí/vypnutí jednotlivých typů řádků a vzhledových doplňků:
- řádek platnosti spoje,
- řádek s negativní značkou,
- přípojné řádky (informace o návaznosti na jiné spoje),
- nadpisy skupin dnů,
- podbarvení sloupců s lichým tarifním číslem.

Stejně jako u zastávek (kap. 13) lze i tady nastavit **podmíněnou viditelnost** a **podmíněné
zvýraznění** celého řádku zastávky podle výrazu nad jejími daty.

Sloupce (jednotlivé spoje) se nastavují obdobně jako u ostatních tabulkových prvků — přes editor
sloupců s možností nastavit pořadí, šířky a styl zvlášť pro záhlaví a data.

### Na čem se ještě pracuje
- U linek s velkým počtem spojů se sloupce mohou zúžit, aby se vešly na šířku stránky —
  rozdělení široké tabulky na víc stránek zatím není podporované.
- Grafické symboly (např. bezbariérovost, „projíždí bez zastavení") se zatím zobrazují jako
  textová zkratka, ne jako obrázková ikona.
- Pořadí poznámek v legendě je pevně dané, zatím ho nejde přizpůsobit.
- Tisk turnusů a obousměrný tisk (oba směry na jedné stránce) zatím nejsou k dispozici.

Pokud pro vaši linku některé z těchto omezení vadí, ozvěte se podpoře — pomůže to určit prioritu
dalšího vývoje.

---

## 15. Časté otázky

**Můžeme si šablonu upravit sami?**
Ano — to je hlavní smysl Návrháře. Vizuálně upravíte a uložíte do databáze, změna je hned vidět
v náhledu.

**Co se starými šablonami?**
Tato verze je neimportuje jedna ku jedné. Nové šablony se navrhnou znovu — díky vizuálnímu
editoru rychle; pomůckou může být import z existujícího PDF (kap. 10).

**Liší se formát čísel a datumů podle počítače?**
Ne. Řídí se jazykem nastaveným v šabloně, ne nastavením Windows dané stanice.

**Kam se ukládají šablony?**
Do centrální databáze sdílené v rámci firmy. Export do souboru JSON je jen doplňková možnost,
například pro poslání e-mailem, ne hlavní úložiště.

**Co když omylem smažu sekci nebo prvek?**
Ctrl+Z vrátí prakticky jakoukoli akci. Smazání celé sekce s obsahem navíc vyžaduje potvrzení.

**Jak poznám, že se obsah nevejde na stránku?**
Náhled zobrazí červený rámeček stránky a upozornění ve status baru. Řešením je zvětšit prostor
(stránku nebo sekci), nebo zmenšit obsah — méně sloupců, menší písmo.

**Musím u jízdního řádu ručně zadávat, že spoj nestaví na všech zastávkách?**
Ne — negativní značky a rozdíly tras se počítají automaticky z dat trasy a platnosti spojů,
včetně vysvětlujícího textu do legendy.

**Bude tisk vypadat stejně jako dosud?**
U zastávkového jízdního řádu z hlavního modulu Jízdní řády je výstup ověřený proti dosavadnímu
tisku na řadě konkrétních linek. Knižní/linkový jízdní řád je zatím v testovacím provozu — viz
kap. 14.
