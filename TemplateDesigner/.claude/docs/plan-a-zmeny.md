# Návrhář sestav - novinky a plán rozvoje

> Přehled toho, co je v Návrháři sestav nového a co chystáme.
> Hledáte návod k použití? → [Návrhář sestav - uživatelská příručka](https://podpora.m-line.cz/support/wiki/90511fc5-10e5-476d-b7c1-3cd70672f26d)

---

## Co chystáme

Hlavní směry, kterými se Návrhář sestav bude dál rozvíjet:

- **Řešení požadavků z helpdesku** - průběžně dopracováváme a uzavíráme konkrétní přání
  a připomínky, které nám posíláte. Aktuálně jde hlavně o pozastavené požadavky
  ze zastávkových jízdních řádů.
- **Tisk přímo z agendy** - vytisknout sestavu jedním tlačítkem přímo u linky/dokladu
  v EDISONu, bez otevírání celého Návrháře.
- **Linkové jízdní řády** - samostatná agenda pro linkové (knižní) jízdní řády.
- **Relace** - podpora tisku relací.
- **Další agendy z Report Generátoru** - postupná migrace dalších typů sestav ze stávajícího
  generátoru do nového Návrháře.

---

## Náměty, o kterých uvažujeme

Dílčí vylepšení a náměty.

- **Napojení na externí zdroje dat** - možnost tisknout i z dat mimo EDISON (např. z tabulky
  nebo databáze). Zvažujeme jako budoucí směr; konkrétní zdroje a rozsah zatím nejsou dané.
- **Mezisoučty ve skupinách řádků** - souhrnný řádek za skupinu dat.
- **Oboustranný / brožurový tisk** při skládání více vývěsek na list - dnes se skládá jednostranně,
  duplex řeší tiskárna.

---

## Novinky

### 17. 8. 2026 - Pevná tabulka: vyplňování na plátně a formátování buněk

Do palety přibyla **pevná tabulka** - mřížka, kterou vyplňujete sami. Je určená hlavně jako náhrada
za skládání hlaviček a bloků údajů z jednotlivých textových polí: místo šesti textů máte jeden
prvek, se kterým se hýbe vcelku.

**Vyplňuje se přímo v ní.** Dovnitř Alt+klikem na buňku nebo klávesou F2, pak se píše jako
v Excelu - Tab a Enter posouvají, šipky chodí po buňkách, Esc vede ven. Řádky a sloupce se přidávají
z místní nabídky, šířky a výšky se táhnou myší za vnitřní hranici. Vložit jde i blok zkopírovaný
z Excelu a po napsání `{` se nabídnou datová pole.

**Buňky jde sloučit** - označené buňky v řádku spojíte místní nabídkou do jedné, například na
nadpis přes celou šířku tabulky; texty se přitom nezahazují, ale spojí. Rozdělit je jde stejnou
cestou zpátky.

**Buněk jde označit víc najednou** - tažením myší přes ně, Shift+klikem, Shift+šipkami, nebo
Ctrl+A pro celou tabulku. Označenou oblast pak
naformátujete jedním krokem, Delete v ní vymaže obsah a Ctrl+C ji zkopíruje ve tvaru, který přečte
Excel; vložení jedné hodnoty do označené oblasti ji celou vyplní.

**Buňku jde nově naformátovat samostatně.** Tučné, kurzívu, podtržení, přeškrtnutí a zarovnání
máte ve formátovací liště nad plátnem; kompletní nastavení (písmo, velikost, barvy, vnitřní okraje,
formát hodnoty) otevřete přes Alt+Enter nebo místní nabídkou „Vlastnosti buňky". Prázdné pole
znamená, že se hodnota dědí z nastavení celé tabulky - proto si tabulku můžete nastavit jednou
a odchýlit jen to, co potřebujete. Záhlaví si uděláte z prvního řádku tím, že ho ztučníte.

### 14. 8. 2026 - Vysvětlivky značek dnů podle vašeho číselníku

Vysvětlivky u kroužkových značek dnů (např. „jede v sobotu") se na zastávkové vývěsce braly
z pevného seznamu v programu. Nově se berou **z číselníku popisů v EDISONu** - tedy odtud, odkud je
bere i dosavadní tisk jízdních řádků. Když si v číselníku znění upravíte, projeví se to i ve vývěsce;
pokud v něm značka není, použije se vestavěné znění.

Zároveň se srovnalo vestavěné znění značky **„+"**: nově **„jede v neděli a ve státem uznané
svátky"** místo dřívějšího „jede ve svátek". Značka podle vyhlášky pokrývá oba dny a stejné znění
má i výchozí číselník v EDISONu.

### 13. 8. 2026 - Jízdní doby variant tras: přehlednější a lépe čitelné

Sloupce s jízdními dobami u zastávkových vývěsek se dočkaly několika úprav najednou.

**Sloupec „Čas jízdy" už nezmizí.** Dosud ho na zastávkách s více variantami trasy variantní sloupce
nahradily, takže z vývěsky zmizela deklarovaná doba jízdy linky. Nově zůstává a varianty se řadí
za něj. Pokud ho na takové vývěsce nechcete, vypnete si ho jako kterýkoli jiný sloupec.

**Nový znak „&lt;" v časech** znamená, že tudy spoj jede, ale nestaví. Dříve tam bylo prázdno stejně
jako u trasy, kudy spoj vůbec nejede - tyhle dvě různé situace teď jdou rozeznat. Nula (jízda kratší
než minuta) se vypisuje; kdo chce čitelnější tvar, zapne si v nastavení prvku „Jízda pod minutu
jako &lt;1".

**U minuty odjezdu je vidět, kterým sloupcem spoj jede.** Písmena variant se nově tisknou i u minut,
a to **současně s poznámkami o rozdílech tras** - dosud se obojí vylučovalo, takže jste měli buď
označení sloupce, nebo vysvětlení, kudy spoj jede. Značka říká „který sloupec", poznámka „kudy".

**Řazení variant si volíte.** K dosavadnímu řazení podle četnosti a stabilnímu pořadí přibyly dvě
volby podle délky jízdy - od nejkratší, nebo od nejdelší.

**Zapínání variant se přesunulo mezi sloupce.** Dosud to bylo zaškrtávátko v nastavení prvku; nově
je to položka **„Varianty tras"** v seznamu sloupců, takže má vlastní šířku, styl i pořadí jako
každý jiný sloupec. Jedna položka se v tisku rozpadne na tolik sloupců, kolik má linka variant.
**Vaše stávající šablony se převedou samy** a vytisknou se stejně jako dosud - nastavení, které jste
měli, zůstává zachované.

### 12. 8. 2026 - Slovenské vysvětlivky podle jazyka šablony

Vysvětlivky, které Návrhář skládá sám (na znamenie, prestup, bezbariérovosť, „premáva len",
„nepremáva cez zast. …", názvy dnů), se teď tisknou **v jazyce nastaveném v šabloně** - dialog
`Vlastnosti stránky…`, pole „Formát čísel a datumů". Tím samým nastavením se řídí i znaková sada
symbolů, takže texty a piktogramy drží jednu volbu.

Nepřekládá se to, co jste napsali sami: poznámky zadané u linky, názvy zastávek ani vlastní nadpisy
sloupců dnů. Jedna firma tak může mít českou i slovenskou šablonu vedle sebe.

### 12. 8. 2026 - Sloupce vývěsky si můžete nadefinovat sami

Dosud rozvržení sloupců mřížky odjezdů určovala konfigurace linky - jedno ze čtyř pevných schémat
(pracovní dny / sobota / neděle a svátky, případně s rozpadem na prázdniny). Nově si sloupce můžete
**nadefinovat v šabloně**, a to až pět.

U prvku odjezdů v `Upravit sloupce…` je záložka **Vlastní definice sloupců** s přepínačem
**Automaticky (dle linky)** / **Vlastní sloupce**. U každého sloupce nastavíte:

- **Nadpis** (i dvouřádkový)
- **Dny** - Po až Ne a Svátek
- **Značky** - *jen s těmito* / *bez těchto* negativními značkami. Tím se dá vyřešit i „tenhle spoj
  na vývěsce o víkendu nechci", což dosud nešlo.
- **Období** - seznam rozsahů dat („14. 2.-18. 2., 1. 7.-31. 8."). Datum **bez roku platí každý rok**,
  takže šablona přežije překlopení jízdního řádu; datum **s rokem** platí jednou (výluka, mimořádný
  provoz). Do sloupce patří spoj, který v období jede aspoň jeden den.
- **Nevypisovat značky** - které negativní značky se v tom sloupci u minut netisknou

Čtyři dosavadní rozvržení jsou připravená **jako vzory na jedno kliknutí**, takže obvykle stačí vzor
vybrat a doladit. U prázdninových vzorů se čísla značek předvyplní z konfigurace aktuální linky.

**Když sloupce nedefinujete, nic se nemění** - šablona se řídí konfigurací linky jako dosud.
Pokud naopak definici zadáte, seznamy značek u linky se pro vývěsku ignorují, aby nebylo nastavení
na dvou místech. Nově přidaný prvek odjezdů začíná se vzorem pracovní dny / sobota / neděle a svátky.

**Spoj, který nespadne do žádného sloupce, se neztratí potichu** - návrhář i tisk upozorní, kolik
takových spojů je a které to jsou.

> **Tip k zadávání:** nedělní a sváteční provoz rozlište **rozdělením na dva sloupce**, ne značkou.
> V datech skoro žádný nedělní spoj o svátcích nejede, takže by značka padla téměř na všechny odjezdy.

### 11. 8. 2026 - Drobnosti ze zastávkových vývěsek

Tři věci z vašich podnětů k zastávkovým jízdním řádům.

- **Šipka směru jízdy vlevo už nepřekrývá jízdní doby.** Dosud si brala místo od sousedního
  sloupce a při užším prvku se do něj vešly i číslice. Nově má vyhrazený vlastní pruh, se kterým
  počítá rozdělení šířky, takže se překryv nemůže stát bez ohledu na velikost prvku.
- **Barva písma aktuální zastávky** - dosud šlo nastavit jen podbarvení. Najdete ji u seznamu
  zastávek ve skupině **Zvýraznění** jako *Barva textu aktuální*; prázdná hodnota znamená
  „stejná jako ostatní řádky", takže se stávající šablony nemění.
- **Obrázek v záhlaví sloupce hodin** - u prvku odjezdů v záložce *Obecné* vyberete obrázek
  (typicky ikonu hodin) a ten se vykreslí v záhlaví hodinového sloupce **u každé skupiny dnů**
  sám, takže ho neumisťujete ručně a drží i při změně rozvržení. Když je obrázek nastavený,
  nadpis sloupce se netiskne.

### 10. 8. 2026 - Vývěska respektuje, které značky jste u linky vypnuli

U linky lze určit, **které negativní značky se v kterém sloupci nemají vypisovat**. Toto nastavení
jsme dosud nečetli, takže se na vývěsce tiskly značky, které jste měli vypnuté - a byly i v legendě.

Nově se respektuje: značka potlačená ve všech sloupcích spoje zmizí i z legendy, protože tam nemá
co vysvětlovat. Spoj se přitom tiskne dál - potlačuje se jen značka u minuty, ne odjezd.

Nastavení zůstává tam, kde bylo, u linky - v návrháři není co zapínat.

### 10. 8. 2026 - Krátká jízdní doba se dá vytisknout jako „<1"

Časy jízdy se na vývěsce počítají od zastávky, pro kterou se tiskne. Když se na následující zastávku
jede pod minutu, vycházela nula a ve sloupci nezůstalo nic. Nově se tam dá vytisknout **„<1"**, tedy
méně než minuta.

Zapíná se u prvku Tabulka zastávek volbou **Jízda pod minutu jako <1** (výchozí vypnuto, takže bez
zásahu se nic nemění). Zastávka, kterou spoj neobsluhuje, zůstane prázdná i se zapnutou volbou.

Aby kvůli tomu neodskočila čísla, znak „<" se vysouvá doleva a číslice zůstávají ve svislici
s ostatními časy.

**Navíc oprava:** nastavení sloupce „Čas" - zarovnání i styl - se teď propíše i na časové sloupce
variant tras. Když měla linka víc variant, sloupce se rozpadly a vaše nastavení se tiše nepoužilo.

### 5. 8. 2026 - Pokračující spoje

Spoje, které v místě konečné pokračují dál už jako jiný spoj, jde na vývěsce označit: u času odjezdu
se vytiskne značka „u1“, „u2“… a v legendě přijde vysvětlivka **„pokračuje do zast. Podhradí (l. 532)“**
s posledním trojčíslím linky, po které spoj pokračuje.

Odjezdy, které pokračují do **stejného cíle po stejné lince**, dostanou tutéž značku - čísla spojů se
neřeší, taková legenda zůstane krátká. Zapíná se u prvku Legenda poznámek volbou **Pokračující spoje**
(výchozí vypnuto) a bere se z údaje o pokračování, který v jízdních řádech už zadáváte.

### 4. 8. 2026 - Datová pole se vybírají ze seznamu, nemusíte je psát

Název databázového pole jste dosud museli znát a napsat bez překlepu - a chyba se projevila
až na vytištěné sestavě. Nově je u takových polí vpravo tlačítko `{ }`, které otevře seznam
polí aktuální agendy s možností hledat.

- **Vkládá na místo kurzoru** - takže pole doplníte i doprostřed rozepsané věty
  (`Linka {CISLO_LINKY}: {POPIS_LINKY}`), nejen do prázdného pole.
- **Kde ho najdete** - obsah textového prvku a čárového kódu, podmínka viditelnosti prvku
  i sekce, viditelnost řádku tabulek, název souboru při exportu.
- Psát názvy polí ručně jde dál, nic se tím nemění.

### 4. 8. 2026 - Řádky dynamické tabulky lze seřadit

U dynamické tabulky vyberete **sloupec a směr** řazení; druhý sloupec se nabídne, až vyplníte
první. Čísla, data i text se poznají podle hodnot, takže se neptáme, jak se má řadit - textově
by „12" bylo před „9". Prázdné hodnoty jdou vždy nakonec. Bez nastavení se pořadí nemění.

### 4. 8. 2026 - Poznámky zadané u linky, kola a místenky na vývěsce

Zastávková vývěska umí zobrazit texty, které máte zadané ve **vlastnostech linky**, a označit spoje
přepravující jízdní kola nebo umožňující místenku.

- **Poznámky zadané u linky** - tarifní, všeobecné i radiobusové texty. Vybíráte je po druzích, tedy
  stejně jako v jízdním řádu u tisku A4/A5, a to zvlášť pro každý blok poznámek.
- **Výlukové texty zvlášť** - poznámky začínající slovem „Výluka" jde od ostatních oddělit. Buď dvěma
  bloky poznámek nad sebou (v jednom *Bez výluk*, ve druhém *Jen výluky*), nebo jako **samostatné pole
  „Výlukové texty"**, které si položíte kamkoli na stránku jako běžný text.
- **Výška bloku se dopočítá sama** - když prvnímu bloku necháte prázdnou výšku, druhý se pod ním
  posune podle toho, kolik textu vyšlo. Při pevné výšce se text zmenší, aby nepřetekl.
- **Přeprava kol a místenky u odjezdů** - u času odjezdu se vytiskne symbol kola nebo písmeno „R"
  a do legendy přijde vysvětlivka. Zapíná se to jednou volbou, takže nemůže zůstat značka bez
  vysvětlení. Pokud si význam značky vysvětlujete vlastní poznámkou u linky, má přednost ta vaše.
- **Přestupy na vlak a autobus** mají v legendě vysvětlivku a správné piktogramy.

**Kde to najdete:** u prvku **Legenda poznámek** v sekci **Značky u odjezdů**. Všechna volba „co se na vývěsce
ukáže" je nově na jednom místě v šabloně - z parametrů sestavy tato nastavení zmizela, takže je
nemusíte hledat na dvou místech. Ve výchozím stavu jsou nové položky vypnuté, takže se vzhled
stávajících šablon nemění, dokud si je nezapnete.

### 3. 8. 2026 - Sloupek v seznamu zastávek

- **Nový sloupec Sloupek** v seznamu zastávek u zastávkových jízdních řádů. Bere se z trasy linky
  podle směru jízdy a je to číslo z číselníku sloupků zastávky, tedy jiný údaj než dosavadní sloupec
  **Označník** (ten nese pole „nástupiště" z trasy linky). Zapíná se v nastavení sloupců, ve stávajících
  šablonách se nabídne sám.
- **Nová volba Číslo stanoviště podle** (Označník / Sloupek) v parametrech sestavy. Určuje, který
  z těch dvou údajů se použije tam, kde jde o „stanoviště, ze kterého spoj odjíždí": při více hodnotách
  na jedné zastávce, u samostatné stránky pro každé stanoviště i v titulku takové stránky. Výchozí
  je Označník, takže dosavadní vývěsky vypadají stejně; kdo eviduje sloupky, přepne na Sloupek.
- **Druhá barva linky** - linka může mít v jízdních řádech víc barev a v návrháři je použijete zápisem
  `{BARVA_LINKY}` a `{BARVA_LINKY_2}` v barvě textu, pozadí i rámečku. Typicky jedna pro číslo linky
  a druhá pro pozadí.
- **Přestupy mají vysvětlení v legendě** - k symbolu přestupu se do legendy tiskne „přestup na autobus"
  a „přestup na vlak". Řádek se objeví jen když máte přestupy u seznamu zastávek zapnuté.
- **Další atributy zastávky na vývěsce** - kromě „na znamení" se nově tisknou i jeho časové varianty,
  metro A/B/C, letadlo, loď, clo a paragrafy, včetně řádku legendy. Technické značky pro tisk
  (tučně, podtrženě, jen nástup / jen výstup a podobně) se jako symbol u zastávky záměrně netisknou.
- **CIS čísla zastávek** - nová pole sestavy: CIS číslo zastávky, CIS číslo výchozí zastávky a CIS
  číslo cílové zastávky (bere se ve směru, o kterém vývěska je). Stejný údaj je i u každého řádku
  seznamu zastávek pro použití v tabulce.
- Volby, které dřív mluvily o označníku, ale platí pro obojí, se jmenují **Více stanovišť**,
  **Řádek pro každé stanoviště** a **Samostatná stránka pro každé stanoviště**.

### 31. 7. 2026 - Vzhled seznamu zastávek a barva linky ve sloupcích

- **Šipka směru jízdy** - nastavitelná tloušťka, velikost hrotu, tvar hrotu (plný / otevřený /
  bez hrotu) a styl čáry (plná / čárkovaná / tečkovaná).
- **Opravená tloušťka šipky v tisku** - dosud vycházela mnohem tenčí než na obrazovce.
- **Svislé čáry mezi sloupci** seznamu zastávek. Tloušťku a styl čar mřížky (svislých i vodorovných)
  lze nastavit u všech tabulkových prvků jízdního řádu.
- **Časy u projetých zastávek** - u zastávek, které spoj už projel, se čas jízdy nově netiskne
  (dosud tam vycházel záporný). Volbou **Čas u projetých** lze vrátit původní chování.
- **Barva linky z jízdního řádu i v jednotlivých sloupcích** - v editoru sloupců jde barva textu
  i pozadí zapsat textem, takže lze zadat `{BARVA_LINKY}` a barva se vezme z jízdního řádu.
  Platí pro zastávky, odjezdy i tabulky.
- **Dopravci po řádcích** - nové pole **Dopravci souhrnného JŘ**: každý dopravce (a provoz) na
  vlastním řádku, před ním číslo linky dle licence, za ním výčet spojů, které jezdí („spoje č. 1-31,
  201-204"). Určené pro souhrnné jízdní řády, kde jednu linku obsluhuje více dopravců; použitelné
  i na běžné lince. Výčet spojů lze vypnout parametrem.
- **Tvar názvu zastávky** - nová volba v parametrech sestavy (skupina Obecné): zkrácený (dosavadní),
  typograficky upravený s mezerami za čárkou a tečkou („Zlín, aut. nádr."), plný tvar podle CIS
  s dvojčárkou, případně i s okresem - poslední dva pro technické exporty. Druhou volbou lze vypnout
  použití upraveného názvu pro MHD, pokud jej zastávka v číselníku má.
- **Seznam zastávek na pokračování** - dlouhý seznam lze rozdělit na dva nebo tři oddíly vedle sebe.
  Každý oddíl má vlastní záhlaví a všechny sloupce, jen s další částí zastávek, takže je vývěska nižší.
- **Tarifní zóny** - když zónu nemá vyplněná trasa linky, vezme se ze **nastavení zastávky**
  (u hraniční zastávky obě zóny). Volbou u linky **„Zóny - tisk a export do JDF bez písmen"** lze
  z kódu zóny odstranit písmeno přepravního systému, takže se tiskne jen číslo zóny.

### 28. 7. 2026 - Zastávkové jízdní řády

- **Označníky** - nový sloupec **Označník** v seznamu zastávek a volba **Více označníků** pro
  zastávky obsluhované z více sloupků (číslo z trasy / nejčetnější / výčet / řádek pro každý
  označník). Nově lze vytisknout i **samostatnou vývěsku pro každý označník**.
- **Přehlednější varianty tras** - časové sloupce se sdružují podle trasy (méně sloupců), u každé
  varianty se uvádí nejdelší jízdní doba. Varianty lze **označit písmeny A-Z** u odjezdů.
- **Přestupy na vlak a autobus** - volitelný symbol přestupu u zastávky.
- **Časová osa přes půlnoc** - vlastní rozsah hodin umí přejít přes půlnoc (noční linky).
- **Automatické zmenšení textu do buňky** - dlouhý text se sám zmenší, aby se vešel.
- Sjednocení názvosloví a doplnění nápověd k parametrům tisku.
