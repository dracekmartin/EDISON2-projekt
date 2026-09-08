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

### Týden od 31. 8. 2026 - Písmena u odjezdů se berou z nastavení linky

Písmena, kterými se u času odjezdu označuje varianta trasy, se nově berou **z nastavení u linky** -
z týchž polí, kde si určujete značky pro poznámky o rozdílech tras. Když je pole prázdné, použije se
výchozí sada z programu.

Důvod je konkrétní: obě řady písmen se dosud mohly potkat na témže znaku, takže **jedno písmeno
u odjezdu mohlo znamenat dvě různé věci** a legenda k němu pak vypsala vysvětlení, které pro ten spoj
neplatilo. Výchozí sady se proto nově nepřekrývají. Kdo si písmena zadá sám, dostane přesně to, co
napsal - i kdyby se obě sady kryly; přesunout jde kterákoli z nich.

⚠️ **Vývěska linky se třemi a více variantami tras bude mít u odjezdů jiná písmena než dosud.**
Linky s jednou nebo dvěma variantami se nemění.

### Týden od 31. 8. 2026 - Údaje dopravce po jednotlivých polích

Název, adresa, telefon, e-mail a IČO dopravce jsou nově samostatná datová pole. Dosud šlo použít
jen jedno pole, které všechny údaje slepilo dohromady a adresu vypsalo dvakrát - jednou u firmy
a jednou u provozovny. Teď si na vývěsku poskládáte právě ty údaje, které chcete.

Adresa je jedno pole včetně PSČ a obce, protože tak je uložená v číselníku dopravců. U linky
s více dopravci nesou pole prvního z nich.

### 17. 8. 2026 - Písmeno varianty je u odjezdu vždy

Když ze zastávky jedou spoje různými trasami, má každá trasa vlastní sloupec s časy a písmeno,
které se tiskne i u minuty odjezdu - podle něj cestující pozná, který sloupec pro spoj platí. Dosud
to byla volba a vypnutá vývěska u minut neměla nic, takže volba zmizela. Zůstává rozhodnutí, jestli
má písmeno i první, nejčastější varianta.

⚠️ Pokud jste volbu měli vypnutou, uvidíte u minut písmena, která tam dřív nebyla.

### 17. 8. 2026 - V seznamu zastávek jsou jen zastávky, kam se odsud dostanete

Zastávka, kam z dané vývěsky nejede žádný spoj, se v seznamu už neobjeví. Dosud zabírala řádek,
ve kterém byly u všech časů jen znaky „<".

### 17. 8. 2026 - Pevná tabulka

Do palety přibyla **pevná tabulka** - mřížka, kterou vyplňujete sami. Je určená hlavně jako náhrada
za skládání hlaviček a bloků údajů z jednotlivých textových polí: místo šesti textů máte jeden
prvek, se kterým se hýbe vcelku.

Rozměr zadáte při vkládání, obsah píšete přímo v tabulce jako v Excelu (vložit jde i blok ze sešitu)
a jednotlivé buňky jde formátovat, slučovat do nadpisů a podmiňovat, aby se prázdný řádek netiskl.
Ovládání a všechna nastavení popisuje příručka.

### 14. 8. 2026 - Rozvržení sloupců vývěsky drží šablona

Jaké sloupce na vývěsce vzniknou, určuje jen definice uložená v šabloně; parametr sestavy
**„Rozdělení sloupců (typy provozu)"** je jejím pohledem i ovladačem. Dosud šlo totéž nastavit ze
dvou míst a jedno druhé tiše přebíjelo, takže parametr býval mrtvé tlačítko.

### 14. 8. 2026 - Vysvětlivky značek dnů podle vašeho číselníku

Vysvětlivky u kroužkových značek dnů se berou **z číselníku popisů v EDISONu**, ne z pevného
seznamu v programu - když si v číselníku znění upravíte, projeví se i na vývěsce. Zároveň se
opravilo vestavěné znění značky **„+"** na „jede v neděli a ve státem uznané svátky".

### 13. 8. 2026 - Jízdní doby variant tras: přehlednější a lépe čitelné

Sloupec **„Čas jízdy" už na zastávkách s více variantami nezmizí**, znak **„&lt;"** nově odlišuje
„spoj tudy jede, ale nestaví" od „tudy vůbec nejede" a **písmena variant jdou dohromady s poznámkami
o rozdílech tras** - dosud se obojí vylučovalo. Přibyly dvě volby řazení variant podle délky jízdy.

Zapínání variant se přesunulo mezi sloupce, takže má vlastní šířku, styl i pořadí jako každý jiný
sloupec. **Vaše stávající šablony se převedou samy** a vytisknou se stejně jako dosud.

### 12. 8. 2026 - Slovenské vysvětlivky podle jazyka šablony

Vysvětlivky, které Návrhář skládá sám (na znamenie, prestup, bezbariérovosť, názvy dnů), se tisknou
**v jazyce nastaveném v šabloně**; stejnou volbou se řídí i znaková sada symbolů. Nepřekládá se to,
co jste napsali sami - poznámky u linky, názvy zastávek ani vlastní nadpisy sloupců. Jedna firma
tak může mít českou i slovenskou šablonu vedle sebe.

### 12. 8. 2026 - Sloupce vývěsky si můžete nadefinovat sami

Rozvržení sloupců mřížky odjezdů dosud určovala konfigurace linky - jedno ze čtyř pevných schémat.
Nově si můžete **v šabloně nadefinovat až pět vlastních sloupců**: dny podle vyhlášky, značky,
období platnosti a nadpis. Tím se dá vyřešit i „tenhle spoj na vývěsce o víkendu nechci".

Čtyři dosavadní rozvržení jsou připravená jako **vzory na jedno kliknutí**, takže obvykle stačí vzor
vybrat a doladit. Když sloupce nedefinujete, nic se nemění. Spoj, který by nespadl do žádného
sloupce, se neztratí potichu - návrhář i tisk upozorní, kolik jich je a které to jsou. Podrobnosti
k zadávání najdete v příručce.

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
