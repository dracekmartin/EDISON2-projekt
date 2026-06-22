# Návrhář šablon — uživatelská příručka

> Vizuální editor tiskových sestav v EDISONu. Navrhnete vzhled šablony, systém k ní
> doplní data a vytiskne PDF. Tato příručka pokrývá celou práci od založení šablony
> až po tisk jízdních řádů.

---

## Obsah

1. [Co je Návrhář šablon](#1-co-je-návrhář-šablon)
2. [Orientace v aplikaci](#2-orientace-v-aplikaci)
3. [Práce se šablonou](#3-práce-se-šablonou)
4. [Sekce a struktura stránky](#4-sekce-a-struktura-stránky)
5. [Práce s prvky (elementy)](#5-práce-s-prvky-elementy)
6. [Vlastnosti a styly](#6-vlastnosti-a-styly)
7. [Datová pole a výrazový jazyk](#7-datová-pole-a-výrazový-jazyk)
8. [Tabulky](#8-tabulky)
9. [Náhled, vícestránkové sestavy a export](#9-náhled-vícestránkové-sestavy-a-export)
10. [Zastávkové jízdní řády](#10-zastávkové-jízdní-řády)
11. [Klávesové zkratky](#11-klávesové-zkratky)
12. [Časté otázky](#12-časté-otázky)

---

## 1. Co je Návrhář šablon

Návrhář šablon je **vizuální editor tiskových sestav** typu WYSIWYG („co vidíte, to dostanete").
Slouží jako náhrada staršího nástroje, kde se sestavy nastavovaly přes formuláře a číselné
parametry bez náhledu — výsledek byl vidět až po tisku.

Systém má dvě vrstvy:

1. **Návrhář** — zde vizuálně navrhnete šablonu (rozložení stránky, texty, tabulky, jízdní řády).
2. **Tiskový engine** — vezme šablonu + reálná data a vytvoří PDF.

Klíčová myšlenka: **šablona popisuje jen vzhled**. Konkrétní hodnoty (čísla linek, časy, názvy
zastávek) doplní systém z databáze při tisku. Stejný engine se používá pro náhled v Návrháři
i pro reálný tisk — proto náhled odpovídá výslednému tisku.

**Co Návrhář není:**
- Není to Word ani Excel — pracuje se strukturou stránky, ne s volným textem.
- Stránka je rozdělená na **sekce** (povinná struktura), ne volné plátno.
- V této verzi neimportuje staré šablony 1:1 — nové šablony se navrhnou znovu (díky vizuálnímu
  editoru rychle).

---

## 2. Orientace v aplikaci

Okno Návrháře má čtyři hlavní části:

| Část | Kde | K čemu |
|---|---|---|
| **Toolbar** | nahoře | ikonová tlačítka — soubor, úpravy, přidání prvků, vrstvy, zarovnání, zobrazení |
| **Levý panel** | vlevo | přepínatelné záložky: **Sekce**, **Strom**, **Pole agendy**, **Vlastnosti** |
| **Plátno (canvas)** | uprostřed | samotná editace šablony |
| **Náhled (preview)** | vpravo | živý náhled výsledného tisku s reálnými daty |

**Status bar** dole zobrazuje:
- aktuální **zoom** a velikost mřížky,
- **počet stránek** sestavy,
- **agendu** (zelený štítek — nad jakými daty šablona běží),
- **aktivní instance** (vybrané linky),
- případné **varování o přetečení** obsahu.

Levý panel — záložky:
- **Sekce** — seznam sekcí šablony, řazení, přidávání, mazání.
- **Strom** — hierarchický přehled celé šablony (sekce → skupiny → prvky) pro rychlou navigaci.
- **Pole agendy** — databázová pole dostupná pro aktuální agendu (lze je tahat na plátno).
- **Vlastnosti** — nastavení právě vybraného prvku (okamžitá editace).

---

## 3. Práce se šablonou

### Nová šablona
`Soubor → Nová` (Ctrl+N). Nová šablona obsahuje jednu výchozí sekci „Obsah" přes celou
tiskovou plochu. Sekce a prvky pak přidáváte podle potřeby.

### Agenda — nad jakými daty šablona běží
Každá šablona je vázaná na **agendu** — zdroj dat (např. zastávkový JŘ z MHD, zastávkový JŘ
z hlavního JŘ modulu, faktury). Agenda určuje, jaká **databázová pole** máte k dispozici.

- Aktuální agenda je vidět jako **zelený štítek** ve status baru a v titulku okna.
- Změna agendy: `Nástroje → Změnit agendu…`, klávesa **Ctrl+Shift+A**, nebo klik na štítek.
- Změna agendy založí novou prázdnou šablonu pro zvolenou agendu (po uložení/zahození rozpracované).

### Otevření a uložení
Šablony se ukládají **do databáze** (centrálně, podle firmy), ne do lokálních souborů na stanici:

- `Soubor → Otevřít z databáze…` — správce šablon: filtr podle agendy, vyhledávání, otevřít,
  přejmenovat, smazat.
- `Soubor → Uložit do databáze` / `Uložit jako…`
- Výhoda oproti lokálním souborům: šablony jsou sdílené, centrální a dohledatelné — ne roztroušené
  po jednotlivých počítačích.

Šablonu lze také uložit jako **soubor (JSON)** a poslat e-mailem kolegovi nebo dopravci.

### Vlastní uživatelská nastavení
Zoom, mřížka, naposledy otevřené šablony, naposledy použité barvy a parametry tisku se ukládají
**per uživatel a firma** — po znovuotevření aplikace se obnoví.

---

## 4. Sekce a struktura stránky

Stránka se skládá ze **sekcí**. Sekce je vodorovný pruh, do kterého umisťujete prvky. Každá
sekce má nastavené **chování při tisku** — určuje, kdy a jak často se tiskne:

| Chování | Význam | Typické použití |
|---|---|---|
| **Jednou** | vytiskne se jednou v toku obsahu | tělo sestavy, pevný obsah |
| **Každá stránka** | opakuje se na každé stránce | hlavička / patička |
| **Jen první stránka** | jen na první stránce | titulní blok |
| **Jen poslední stránka** | jen na poslední stránce | závěrečný podpis, souhrn |
| **Kromě první** | na všech kromě první | průběžná hlavička |

**Chování při přetečení** (když se obsah nevejde na stránku):
- **Nepřerušovat** — sekce se celá přesune na další stránku.
- **Rozdělit přes stránky** — dlouhý obsah (tabulky) se rozdělí a pokračuje na další stránce.

**Práce se sekcemi:**
- Přidat: tlačítko `+ Přidat sekci` (nabídne i vlastní uložené šablony sekcí).
- Řadit: přetažením v levém panelu (nebo šipkami).
- Výška se při přidání/smazání sekce proporcionálně přerozdělí mezi ostatní.
- **Vlastní šablony sekcí** — často používanou sekci uložíte (pravý klik → „Uložit jako šablonu")
  a vkládáte ji opakovaně do dalších šablon.

---

## 5. Práce s prvky (elementy)

Prvek je samostatný objekt na plátně. Dostupné typy:

| Prvek | K čemu |
|---|---|
| **Text** | statický text nebo text s databázovými poli |
| **Obrázek** | logo, piktogram |
| **Čára** | oddělovač (plná, čárkovaná, tečkovaná) |
| **Obdélník** | rámeček, podbarvení, pozadí |
| **Kód** | čárový / QR kód (QR, Code128, EAN-13, EAN-8, PDF417, DataMatrix) |
| **Tabulka** | sloupcová tabulka (pevná nebo z dat) — viz [kap. 8](#8-tabulky) |
| **Prvky JŘ** | specializované prvky jízdního řádu — viz [kap. 10](#10-zastávkové-jízdní-řády) |

### Přidání prvku
- Z **palety** v toolbaru — klik vloží prvek do aktivní sekce.
- **Přetažením z palety** přímo na plátno — během tažení vidíte náhled (ghost) a zvýrazní se cílová
  sekce.
- **Přetažením databázového pole** z panelu „Pole agendy" — vznikne textový prvek s polem `{POLE}`.

### Výběr, přesun, velikost
- **Klik** vybere prvek; **tažení** ho přesune; za **úchyty** na okrajích měníte velikost.
- Výběr více prvků: tažení rámečku (rubber band) nebo Ctrl+klik.
- Prvek se při přesunu i změně velikosti **nevejde mimo sekci** (automatické oříznutí na hranice).

### Pomocníci pozicování
- **Mřížka** — zapínatelná, volitelná velikost (1 / 2 / 5 / 10 mm).
- **Magnetické přichytávání (snap)** — prvek se přichytí k hranám a středům jiných prvků a k mezeře
  4 mm; zobrazí se vodítka a indikátor mezery.
- **Normalizace pozic** (Ctrl+Shift+N) — srovná drobné nepřesnosti pozic na zvolený krok mřížky.

### Duplikování, seskupování, vrácení akcí
- **Chytré duplikování** — Ctrl+D + šipka umístí kopii vedle originálu s mezerou 4 mm; samotné
  Ctrl+D vytvoří kopii s malým posunem.
- **Seskupování** — Ctrl+G spojí prvky do skupiny (klik pak vybere celou skupinu, Alt+klik jeden
  prvek); Ctrl+Shift+G skupinu zruší.
- **Zpět / Vpřed** — Ctrl+Z / Ctrl+Y. Každá akce je vratná.
  *(Oproti starému nástroji: omyl v parametru = znovu projít celý formulář, žádné „zpět".)*

---

## 6. Vlastnosti a styly

Vybraný prvek nastavíte v záložce **Vlastnosti** levého panelu (okamžitá editace). Stejné
vlastnosti najdete i v dialogu po stisku **Enter** nebo dvojkliku na prvek.

### Styly textu
Font, velikost, **tučné** / *kurzíva* / podtržení / přeškrtnutí, barva textu a pozadí,
vodorovné i svislé zarovnání.

### Rámeček a pozadí
Každý prvek může mít rámeček (barva + tloušťka) a barvu pozadí. Když dva prvky sdílejí hranu,
vykreslí se jako **jeden** rámeček (ne dvojitý).

### Hromadné úpravy
Vyberte více prvků a stiskněte **Enter** — otevře se dialog **společných vlastností**, změna se
aplikuje na všechny najednou.

### Kopírování stylu
Ctrl+Shift+C zkopíruje styl (font + rámeček + pozadí) vybraného prvku, Ctrl+Shift+V ho aplikuje
na jiný prvek.

### Kaskáda stylů u tabulek
U tabulek a prvků JŘ funguje **kaskáda**: nejčastější barvy (záhlaví / data) nastavíte rychle
přímo v panelu, detailní styl po jednotlivých sloupcích (font, zarovnání, odsazení) v dialogu
sloupců. Co nenastavíte na sloupci, **zdědí se** z nastavení celého prvku.

---

## 7. Datová pole a výrazový jazyk

### Placeholdery (databázová pole)
Kdekoli v textu napíšete pole ve složených závorkách, např. `Linka {CISLO_LINKY}`. Při tisku se
`{CISLO_LINKY}` nahradí skutečnou hodnotou. Dostupná pole pro aktuální agendu vidíte v panelu
„Pole agendy" a můžete je tahat přímo na plátno.

### Formátování hodnot
V placeholderu lze hodnotu naformátovat:

| Zápis | Výsledek |
|---|---|
| `{CENA:N2}` | číslo na 2 desetinná místa |
| `{NAZEV:UPPER}` | velká písmena |
| `{KOD:LEFT(4)}` | první 4 znaky |
| datum / čas | podle jazyka šablony (viz níže) |

### Podmíněná viditelnost
Prvek nebo sekci lze zobrazit jen za podmínky — pole **Podmíněná viditelnost**, např.:
- `{POCET} > 0` — zobrazit jen když je počet kladný,
- `{PAGE} > 1` — jen od druhé stránky.

Podporuje operátory `&&` (a zároveň), `||` (nebo), `!` (negace). Pokud je výraz neplatný,
zobrazí se u pole **červené upozornění** — opravte zápis.

### Agregace v tabulkách
V souhrnných řádcích tabulky lze počítat: `SUM` (součet), `COUNT` (počet), `AVG` (průměr),
`MIN`, `MAX`.

### Jazyk šablony (lokalizace)
Šablona má nastavený **jazyk** (čeština, slovenština, angličtina, němčina). Formát čísel a
datumů se řídí jazykem šablony — **nezávisle na nastavení Windows** dané stanice. Tisk tak vypadá
stejně na každém počítači.

---

## 8. Tabulky

Tabulka má dva režimy podle toho, **kdo dodá řádky**:

| Režim | Strukturu definujete | Řádky při tisku |
|---|---|---|
| **Pevná (Fixed)** | sloupce **i** řádky ručně v editoru | buňky se naplní databázovými poli `{POLE}` ze samostatných hodnot; počet řádků je pevný |
| **Dynamická (Dynamic)** | jen sloupce | řádky se **automaticky vygenerují z dat** (ze seznamu/kolekce) — počet řádků odpovídá datům |

- **Pevná** tabulka se hodí pro bloky jako záhlaví linky nebo platnost/dopravce — pevný počet
  řádků, hodnoty z polí.
- **Dynamická** tabulka se hodí pro **seznam položek**, jejichž počet předem neznáte (řádky se
  vytvoří z datové kolekce). U dynamické tabulky vyberete **zdroj dat** (kterou kolekci vypsat).

V obou režimech nastavujete sloupce (pořadí, šířky, záhlaví, viditelnost) a styl po sloupcích.

---

## 9. Náhled, vícestránkové sestavy a export

### Živý náhled
Pravý panel ukazuje **výsledný tisk** s reálnými ukázkovými daty agendy. Na rozdíl od plátna
respektuje podmíněnou viditelnost — vidíte přesně to, co se vytiskne.

### Vícestránkové sestavy
Stránkování řeší systém automaticky:
- hlavičky a patičky se opakují podle nastaveného chování sekcí,
- dlouhé tabulky se dělí přes stránky,
- v náhledu jsou stránky pod sebou — proklikáte se konkrétní stránkou.

### Varování o přetečení
Pokud se obsah na stránku nevejde, stránka dostane **červený rámeček** a status bar zobrazí
upozornění. Slouží jako kontrola, že se nic „neztratí".

### Export a tisk
- **Export do PDF** nebo **PNG**.
- **Tisk** přímo z Návrháře s reálnými daty z databáze (dialog Tisk — parametry + výběr dat).
- **Jedna šablona × více dat = jeden PDF** — např. linka × všechny zastávky × oba směry.
- **Členění výstupu** při dávkovém tisku: vše do jednoho souboru / soubor per linka / soubor per
  zastávka a směr; **název souboru** lze sestavit z datových polí (např. `{CISLO_LINKY}_{POPIS_ZASTAVKA}`).

### Import z PDF *(rozpracovaná pomůcka)*
Z existujícího PDF dokáže Návrhář vytáhnout texty, čáry a obrázky jako prvky a předpřipravit
základ šablony (volitelně s pomocí AI). Berte zatím jako pomůcku pro rychlý start, ne jako
záruku přesné shody.

---

## 10. Zastávkové jízdní řády

Zastávkový jízdní řád se skládá ze tří **specializovaných prvků**, které rozumí datům JŘ a samy
řeší většinu logiky:

| Prvek | Co zobrazuje |
|---|---|
| **Tabulka zastávek** | seznam zastávek — názvy, časy jízdy, atributy, šipka směru jízdy |
| **Mřížka odjezdů** | minuty odjezdů po skupinách dnů (sloupce = pracovní den / sobota / neděle …) |
| **Legenda poznámek** | vysvětlivky pod mřížkou |

Každý prvek má desítky nastavení; při pevné výšce se písmo automaticky přizpůsobí, aby se obsah
vešel.

### Dva zdroje dat
- **Zastávkový JŘ z MHD modulu.**
- **Zastávkový JŘ z hlavního JŘ modulu** — datově ověřený **1:1** proti původnímu tisku na
  konkrétních linkách.

Mezi zdroji přepnete změnou agendy (Ctrl+Shift+A nebo štítek ve status baru). **Výběr linek**
(instance) — vyhledávací dialog s možností vybrat více linek najednou pro dávkový tisk (stovky
stránek).

### Tabulka zastávek — co nastavíte
- **Sloupce** — pořadí, šířky, záhlaví, viditelnost, styl po sloupcích.
- **Atributy zastávek** vedle názvu (na znamení, jízdenkový automat, přestup) — jen relevantní.
- **Časy jízdy** ve sloupci (kumulativní).
- **Šipka směru jízdy** a zvýraznění aktuální zastávky.

### Mřížka odjezdů — co nastavíte
- **Skupiny dnů** = sloupce (pracovní den, sobota, neděle …).
- Nastavení po skupinách (viditelnost, barvy, font, zarovnání).
- **Sloupec hodin** — vlevo nebo u každé skupiny.
- **Anotace u minut** (horní index): týdenní platnost, speciální značky spoje, negativní značky
  (spoj nestaví všude), bezbariérovost, rozdíly tras (písmena A/B).

### Co systém řeší automaticky
Tohle **nemusíte psát ručně** — vypočítá se z dat:
- **Omezení platnosti** „jede / nejede" — textová omezení podle data.
- **Rozdíly tras** — spojům s odlišnou trasou se přiřadí písmena + text do legendy (např.
  „neobsluhuje zast. X", „končí v zast. Y").
- **Bezbariérovost** spojů.
- **Dopravce** — popis a adresa provozovny.
- **Legenda poznámek** — naplní se z toho, co je na stránce; více sloupců, řazení, zarovnaný
  marker.

**Shrnutí:** prvky JŘ rozumí datům — vy řešíte **vzhled**, systém řeší **business logiku**.

---

## 11. Klávesové zkratky

| Zkratka | Akce |
|---|---|
| Ctrl+N / Ctrl+O | Nová šablona / Otevřít |
| Ctrl+S / Ctrl+Shift+S | Uložit / Uložit jako |
| Ctrl+Z / Ctrl+Y | Zpět / Vpřed |
| Ctrl+X / Ctrl+C / Ctrl+V | Vyjmout / Kopírovat / Vložit |
| Ctrl+D | Duplikovat |
| Ctrl+D + šipka | Duplikovat vedle (s mezerou) |
| Ctrl+Shift+C / Ctrl+Shift+V | Kopírovat / Vložit styl |
| Delete | Smazat výběr |
| Ctrl+A | Vybrat vše v sekci |
| Enter / dvojklik | Vlastnosti výběru |
| Ctrl+G / Ctrl+Shift+G | Seskupit / Rozeskupit |
| Alt+klik | Vybrat jeden prvek ze skupiny |
| Ctrl+Shift+N | Normalizovat pozice |
| Ctrl+0 | Přizpůsobit zobrazení stránce |
| Ctrl+Shift+A | Změnit agendu |
| F1 | Nápověda |

---

## 12. Časté otázky

**Můžeme si šablonu upravit sami?**
Ano — to je hlavní smysl Návrháře. Vizuálně upravíte a uložíte do databáze.

**Co se starými šablonami?**
Tato verze je neimportuje 1:1. Nové šablony se navrhnou znovu — díky vizuálnímu editoru rychle.

**Bude tisk vypadat stejně jako dosud?**
U zastávkového JŘ z hlavního JŘ modulu je výstup ověřený 1:1 proti původnímu tisku na konkrétních
linkách.

**Jak se Návrhář dostane k nám?**
Je integrovaný v EDISONu; tisk probíhá stejným enginem jako náhled, takže výsledek odpovídá tomu,
co vidíte při návrhu.

**Liší se formát čísel/datumů podle počítače?**
Ne. Řídí se jazykem nastaveným v šabloně, ne nastavením Windows dané stanice.
