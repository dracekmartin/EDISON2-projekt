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
- **Pevná (Fixed) tabulka** - tabulka s ručně zadanými řádky a pevným počtem (typicky záhlaví
  dokladu nebo faktura); dnes je k dispozici tabulka s řádky generovanými z dat.
- **Sloučené buňky v tabulce** - spojení více buněk do jedné (navazuje na pevnou tabulku).
- **Mezisoučty ve skupinách řádků** - souhrnný řádek za skupinu dat.
- **Oboustranný / brožurový tisk** při skládání více vývěsek na list - dnes se skládá jednostranně,
  duplex řeší tiskárna.

---

## Novinky

### 10. 8. 2026 - Sloupce vývěsky si nadefinujete v šabloně

Dosud byly sloupce vývěsky (Pracovní dny / Soboty / Neděle a svátky…) dané napevno nastavením
linky. Nově si je můžete nadefinovat přímo v šabloně: v dialogu prvku odjezdů přibyl tab
**Vlastní definice sloupců**, kde každému sloupci (nejvýše pěti) určíte:

- **nadpis** (i dvouřádkový),
- **dny v týdnu** včetně svátku,
- **značky** - „jen spoje se značkami" nebo naopak „bez spojů se značkami" (tím jde třeba schovat
  vybrané spoje z víkendového sloupce),
- **nevypisovat značky** - spoj se vytiskne, jen se u minuty neopakuje značka, kterou už říká
  nadpis sloupce.

Čtyři dosavadní rozvržení jsou připravená jako **vzory** jedním klikem (prázdninové vzory se
předvyplní čísly značek z aktuální linky) a pátá volba **Prázdná** nechá stavbu sloupců úplně
na vás. Existující šablony se nemění - dokud definici nezapnete, sloupce se dál řídí nastavením
linky.

Když nadefinované sloupce nepokryjí všechny spoje, návrhář to ukáže ve stavovém řádku
(„N odjezdů nespadá do žádného sloupce") a totéž zopakuje při exportu do PDF - žádný odjezd
tak nezmizí bez povšimnutí.

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
