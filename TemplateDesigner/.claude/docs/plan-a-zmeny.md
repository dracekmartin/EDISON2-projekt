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

### 3. 8. 2026 - Sloupek v seznamu zastávek

- **Nový sloupec Sloupek** v seznamu zastávek u zastávkových jízdních řádů. Bere se z trasy linky
  podle směru jízdy a je to číslo z číselníku sloupků zastávky, tedy jiný údaj než dosavadní sloupec
  **Označník** (ten nese pole „nástupiště" z trasy linky). Zapíná se v nastavení sloupců, ve stávajících
  šablonách se nabídne sám.
- **Nová volba Číslo stanoviště podle** (Označník / Sloupek) v parametrech sestavy. Určuje, který
  z těch dvou údajů se použije tam, kde jde o „stanoviště, ze kterého spoj odjíždí": při více hodnotách
  na jedné zastávce, u samostatné stránky pro každé stanoviště i v titulku takové stránky. Výchozí
  je Označník, takže dosavadní vývěsky vypadají stejně; kdo eviduje sloupky, přepne na Sloupek.
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
