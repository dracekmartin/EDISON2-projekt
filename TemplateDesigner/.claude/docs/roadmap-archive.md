# 🗄️ Archiv - hotové kroky

## `B125` Popisky `od` / `př` u konců šipky - NEDĚLÁ SE (rozhodnutí Jan 7. 9. 2026)

**Ze šipky je směr pochopitelný i beze slov** - popisky konců se dělat nebudou. Šipky samotné
zůstávají bez zásahu: vlevo a na jednosměrné stránce míří dolů, vpravo u obousměrné nahoru, což je
doložené výtiskem ze starého modulu z 28. 8.

Kdyby se k tomu někdo vracel, tohle je změřené a nemusí se hledat znovu:

- Texty jsou zadrátované `"od"` a `"př"` (`JRapiLinPrtWin.mac:3016,3018`), nastavit je nikde nejde.
  Postskript má na tomtéž místě `"od"` / `"pr"`, ale to jsou jména glyfů pro jeho vlastní vektorové
  kreslení, ne slova.
- ⚠️ **Není to „jen dva texty".** Sloupec se šipkou je ve starém tisku **řádkový** - každá zastávka
  má hodnotu `od`, `šb`, `šc`, `še` nebo `př` - takže popisek zabírá krajní řádek a dřík jde jen
  mezi nimi.
- Naše šipka je jedna svislá čára přes všechny řádky. Věrná varianta ji o dva řádky **zkrátí**
  a u linky o třech zastávkách z ní zbyde jediný řádek dříku. Vyzkoušeno 7. 9.: shodilo dva testy
  (na délku šipky a na směr hrotu, protože slovo je ve 4mm sloupci širší než hrot).
- Do toho vstupuje i zastávka s odděleným příjezdem a odjezdem (`P`/`J`), která šipku uprostřed
  tabulky ukončí a znovu začne.

## `B128` Přípoje zrcadleného směru se v obousměrném tisku netiskly (hotovo 7. 9. 2026)

Přípojné řádky se stavěly jen ze spojů hlavního směru, takže zrcadlená strana měla buňky vždy
prázdné a u poloviny spojů chybělo „vyčká / navazuje", i když to data nesla. Přípojný řádek nese
nově časy obou stran; řádek, který mají oba směry, vyjde jednou s časy vlevo i vpravo (slučovací
klíč směr tisku neobsahuje, takže je to důsledek rozhodnutí ze 4. 9.).

Dva testy ze 7. 9. se přepsaly - držely, že zrcadlený přípoj místo nerezervuje, což platilo jen
dokud se nekreslil. Nově hlídají symetrii výšky i zlomů, což je silnější podmínka. Detail
a dvě chybné verze testu na inkoust: `changelog.md` 7. 9. (12).

## `B122` Testy nehlídaly šířku bloku u obousměrné sestavy (hotovo 4. 9. 2026)

Podmínka „každý blok linkové tabulky se vejde do šířky prvku" nebyla u linky tištěné v obou směrech
hlídaná: pomocný výpočet `Rozvrzeni` počítal s počtem **slotů**, kdežto kreslení s počtem
**sloupců** (`naBlok * SloupcuNaSlot`), a u obousměrné sestavy má slot sloupec na každé straně
zastávek. Šířka tedy vycházela na polovinu a přetečený blok se mohl v tisku odříznout, aniž by to
kterýkoli test viděl.

Opraveno přepočtem na sloupce (stejně jako to dělá kreslení i `Vejde`) a novou fixturou obousměrné
linky, která před opravou padá na **338 mm proti prvku 190 mm**. Vedle toho odstraněn mrtvý dělič
`TableLayout.RozdelSpojeDoBloku` i s vlastní sadou testů - nikdo ho nevolal.

Nalezeno při hledání spojů, které chyběly na `CB_GWBUS 0320144` (viz `B120` v analýze §16);
s tou příčinou to nesouviselo. Vytištěná tabulka se nemění, mění se to, co testy chytí.

## `B119` - přípoje jednoho směru na jednom řádku (hotovo 4. 9. 2026)

Detail → changelog 2026-09-04 (10), logika legacy i naše →
[`linkovy-knizni-jr-implementace-analyza.md §15`](linkovy-knizni-jr-implementace-analyza.md).

Vyšlo z výtisku starého modulu, který Jan poslal při dokládání `#11300`: starý tisk slévá
přípoje téhož směru do jednoho řádku („ze směru Trhové Sviny"), my jsme dělali řádek per spoj.

**Rozhodnutí Jan 4. 9.:** šetření místa váží víc než to, aby cestující věděl, kterou linkou přípoj
přijede - potřebuje čas. Proto je výchozí **podle směru**, a to i pro šablony uložené dřív.

⚠️ **Rozhodnutí z 25. 8. „slučování ne, nikdo si o to neřekl" bylo přepsané** - zákazník ten tvar
zná ze starého tisku, jen o tom nenapsal tiket. Druhá polovina odůvodnění („přišli bychom
o popisek") se vyřešila tím, že sloučený řádek má popisek podle směru.

## `B112` - provozovny dopravce: VYHOZENO, nikdo je nechce (4. 9. 2026)

**Rozhodnutí Jan 4. 9.:** bylo to zkoušení s kolegou, ne požadavek - poptávka po tom není žádná.

Skalární pole dopravce (název, adresa, telefon, e-mail, IČO) **fungují a zůstávají** (`B111`).
Kolekce provozoven zůstává **skrytá ze schématu**, aby na ní nikdo nepostavil šablonu; drží to
`ZastavkoveJrAdDopravciTest`. Až si o provozovny někdo řekne, začne se rozhodnutím, čím se kreslí
hierarchie „dopravce → jeho provozovny" - tabulka z dat na ni nesedí a opakovala by údaje firmy.

## `B116` - blikající paritní brána: ZAVŘENO na živá data na devu (4. 9. 2026)

Detail → changelog 2026-09-04 (4). Paritní test linkového JŘ dvakrát spadl a pak bez zásahu do kódu
prošel (legenda poznámek, název zastávky s dvojitou čárkou; linky 0330510, 0330511, 0380780).
**Deset běhů to nezreprodukovalo** a 4. 9. hlásí oracle 0 nedeklarovaných rozdílů na 48 kategorií.

**Rozhodnutí Jan 4. 9.: zavřít.** Test sáhá na **živá data na devu, která mění víc lidí** - to
dostatečně vysvětluje pád, který se pak sám srovnal, a není to co opravovat. Držet to jako položku
znamená jen šum v Backlogu.

**Vyloučené příčiny nesou zdrojáky, ne roadmapa** - odstavec „Když spadne, začni tady" ve třídě
`LinkovyJrAdLegacyTest`: kolize dvou běhů v pracovním globálu starého modulu (index z atomického
`$i(^CacheTempJB)`), chyba starého modulu (přeskočí linku, neshodí test) a závislost na dni běhu
(obě strany berou datum z platnosti linky). Je tam i pokyn, že při dalším pádu se má nejdřív uložit
diagnostika a ručně porovnat se starým modulem - padalo to na hodnotách, které hlídané být mají.

✅ **.NET polovina byla skutečná chyba a je opravená** (3. 9.): `SheetPdfExportTests` padal
paralelním během kolekcí nad procesně globálním stavem, sada teď běží sekvenčně.

## `B117` - poznámky linkového JŘ jsou samostatný prvek (hotovo 4. 9. 2026)

Detail → changelog 2026-09-03 (7). Legenda se do 3. 9. skládala **dynamickou tabulkou** nad kolekcí
poznámek, takže uživatel dostával řádky, ne poznámky. Prvek Poznámky byl v paletě jen u zastávkových -
ne z rozhodnutí, ale protože to tak bylo nejsnazší.

**Záměr (Jan 3. 9.):** dynamická tabulka je na raw data, ne na doménovou logiku - a u poznámek
doménová logika je (negativní značky v inverzním boxu, vyhrazená buňka značky, řazení podle typu).

**Hotovo a doloženo čtením kódu 4. 9.:** prvek je v paletě linkových (tabulka první, poznámky druhé),
čte `DRUH` i `KATEGORIE`, a podle druhu si sáhne po **slovníku značek linkového JŘ** - bez toho by
značka „pokračuje" vyšla jako kufr a návaznosti jako půlkruh s kosočtvercem. Systémová šablona
`linkovy-jr-ad.json` ho používá.

**Nic se přechodem neztratilo** (porovnáno s předchozí verzí šablony, commit `a7ad4001^`): tabulka
nesla dva sloupce, výšku řádku, záhlaví a rámeček; prvek má všechno z toho a navíc rozdělení legendy
do sloupců, směr plnění, řazení a filtry podle typu poznámky.

⚠️ **Počet poznámek na řádek podle druhu NEBYL součástí téhle položky** - je to featura, kterou
jsme neuměli ani předtím, a žije dál jako samostatný bod (`#7487`). Do 4. 9. tu visela jako
„zbývá krok 2", tedy jako by položka nebyla dokončená - byl to chybný zápis záměru (nález Jan 4. 9.).

## `B118` - výšku sekce jde měnit myší i jedním kliknutím (hotovo 4. 9. 2026)

Detail → changelog 2026-09-03 (8) a 2026-09-04 (1). Krok 1 = položka „Vyplnit zbytek stránky"
v místní nabídce sekce, krok 2 = tažení za spodní hranu sekce na plátně.

**Chování (rozhodnutí Jan 3. 9., platí pro obojí):** sekce nad se nehne, sekce pod se posune
a podrží svou výšku. Zadáno příkladem - tři sekce 0-10 %, 10-20 % a 20-30 % stránky, po vyplnění
prostřední vyjde 0-10 %, 10-90 % a 90-100 %.

⚠️ **Dvě věci, na které se přišlo až při práci:**

- **Tlačítko ve formuláři sekce není vidět.** Formulář sekce přichází z Caché
  (`SectionEdit.cls`), takže C# descriptory se u sekce nezobrazují - a akce tam nepatří ani
  věcně. Proto místní nabídka.
- **Výpočet zbývajícího místa odečítal 5 mm za každou sekci** jako prostor pro její modré
  záhlaví na plátně. To je otázka zobrazení, ne rozměru sestavy: u tří sekcí by tím zůstalo
  15 mm papíru nevyužitých. Počítá se podle papíru a stejnou mírou pro obě cesty.

Zadání, se kterým se do toho šlo:

- **`B118` Výšku sekce jde měnit jen číslem** [M] (podnět Jan 3. 9.). ✅ **HOTOVO** - krok 1 (položka „Vyplnit zbytek stránky" v místní nabídce) 3. 9., krok 2 (tažení za spodní hranu sekce) 4. 9. Detail v changelogu 2026-09-03 (8) a 2026-09-04 (1).



  **Dnešní stav:** výška sekce se dá změnit **jedinou cestou** - v panelu vlastností sekce polem

  „Výška" v milimetrech. Uživatel tedy musí vědět, kolik místa na papíře zbývá, a spočítat si to.

  Na plátně má sekce modrý svislý pruh se svým názvem, ale ten umí jen vybrat sekci a dvojklikem

  otevřít vlastnosti; **tahat se za něj nedá**.



  **Dva návrhy (Jan 3. 9.):**



  1. **Tažení za modrý pruh / hranici sekce** - jako u prvků.

  2. **Výška „Vyplnit zbývající místo"** - podívá se, kolik na stránce zbývá, a nastaví to.



  ✅ **Polovina druhého návrhu už existuje:** `CalculateMaxSectionHeight` v panelu vlastností

  spočítá zbývající místo (tisková plocha minus ostatní sekce a jejich záhlaví) a používá se jako

  **strop** zadané hodnoty. Chybí jen způsob, jak tu hodnotu zapsat na jedno kliknutí - tedy

  tlačítko nebo volba u pole výšky, ne nový výpočet.



  **Rozhodnuté chování (Jan 3. 9.) - platí pro vyplnění i pro tažení:** sekce nad ní se nehne,

  sekce pod ní se **posune a podrží svou výšku**. Příklad, kterým je to zadané: tři sekce zabírají

  0-10 %, 10-20 % a 20-30 % stránky; po „vyplnit zbývající místo" na prostřední vyjde **0-10 %,

  10-90 % a 90-100 %** - tedy stránka je plná a poslední sekce si drží svých 10 %.



  ✅ **Prvky se posunou samy** - jejich souřadnice jsou v modelu relativní k sekci (renderer je

  přepočítává přes počátek sekce), takže se vůči své sekci nepohnou a není co dopočítávat.



  ✅ **„Zbývající" počítá s tím, že ostatní sekce si své místo drží** - proto se sekce pod ní

  nevytlačí ze stránky. `CalculateMaxSectionHeight` to tak už dělá (odečítá všechny ostatní

  sekce i jejich záhlaví).



  ⚠️ **Tažení musí mít stejnou mez jako dnes pole Výška** - tam se zadaná hodnota srazí na

  maximum. Bez toho by tatáž věc měla dvě chování: číslem se sekce srazí, tažením přeteče.

  Zarazit a dát to najevo (kurzor / hláška), aby to nevypadalo jako zaseknutí.



  ✅ **Soused se nezužuje, jen posune** (rozhodnuto tímtéž příkladem). Je to jiné než uvnitř

  pevné tabulky, kde se dvojice u hrany o místo dělí - u sekcí jsou ale bloky v toku nad sebou,

  ne v mřížce. Obě pravidla drží `ux-conventions §3`, teď tam patří i tohle.



  ⚠️ **Past:** tažení na plátně dnes mění velikost prvků a hranice sekce slouží jako jejich

  clamp. Nová cesta musí jít rozlišit od tažení prvku - jinak se u okraje sekce nepozná, jestli

  uživatel tahá prvek, nebo sekci (táž třída problému jako vstup do buňky tabulky, kde se

  „druhý klik na vybranou tabulku" 12. 8. zkusil a zahodil).

## `B101` - jedna skupina dnů: ZAVŘENO jako neplatné (3. 9. 2026)

**Rozhodnutí Jan 3. 9.: zavřít.** Vývěska, kde v odjezdech vyjde jediný sloupec, v praxi
nenastává - není tedy co zlepšovat na srozumitelnosti stavu, který uživatel nevidí.

**Zadání navíc přestalo platit** (ověřeno 3. 9. voláním serveru a čtením kódu):

- **Příklad v zadání nesouhlasí s daty.** Linka `0330511` má na **všech 49 stránkách tři skupiny
  dnů**, ne jednu. Tvrzení „všechny spoje jen v pracovní dny" bylo pravda 25. 8., ale rozvržení
  sloupců se od té doby dvakrát měnilo (`B29` 31. 7., `B39` 10. 8., `B82` 14. 8.).
- **Popsané chování v kódu není.** „Prvek pás skupin nekreslí, když vyjde jediná skupina" -
  taková podmínka není ani v prvku odjezdů, ani na serveru. Pás se vypíná **jen** uživatelskou
  volbou „Záhlaví skupin"; jinak se kreslí vždy, i s jednou skupinou.

⚠️ **Poučení pro zadání psaná do zásoby:** blok visel od 25. 8. a za devět dní mu zestaraly oba
pilíře - příklad i popis chování. Kdyby se bral doslova, šlo by se opravovat něco, co se nedělo.
U položky starší než pár dní se má **nejdřív ověřit, že problém pořád existuje**.

Zadání, jak bylo napsané:

- **`B101` Není poznat, jestli je jedna skupina dnů nastavením, nebo daty linky** (z 25. 8.).

  Prvek pás skupin nekreslí, když vyjde jediná skupina - a uživatel pak nemá jak poznat, jestli

  si dělení nastavil špatně, nebo jestli tak prostě vypadá linka (na 0330511 jedou všechny spoje

  jen v pracovní dny). Otázku „mám to vůbec nastavené dobře?" dnes **nejde zodpovědět z aplikace**.

  Drobné zlepšení, ne chyba.

## `B103` - hledání ve výběru linek najde i linku za dvoustovkou (hotovo 3. 9. 2026)

Detail → changelog 2026-09-03 (4). Načítá se celá skupina, ne prvních 200.

**Rozhodnutí Jan 3. 9.:** žádné stránkování, prostě načíst všechno. Změřeno, kolik linek skupina
reálně má - nejvíc **240** (CB_CSADAD skupina 1), pak 228, jinde desítky; jsou to stovky, ne
tisíce. Limit řezal o čtyřicet a hledaná linka byla 233. v pořadí.

⚠️ **Limit nešlo jen smazat** - agendy čtou `i 'limit s limit = 50`, takže „bez limitu" by dalo
padesát řádků. Posílá se strop `MAXINSTANCI = 20000` jako pojistka proti chybě v datech
(poškozený globál by stáhl celý `^JRL`), ne jako omezení pro uživatele.

Zadání, se kterým se do toho šlo:

- **`B103` Hledání ve výběru linek prohledá jen prvních 200** (nález Jan 1. 9.). Dialog si

  načte 200 linek skupiny (`EDList.Instance` posílá `limit = 200`) a hledací pole pak filtruje

  **jen je** - napsané číslo linky, která je až za dvoustovkou, nenajde nic a vypadá to, že linka

  v datech není. Přitom tam je: `380761` v CB_CSADAD skupině 1 se přes výběr vybrat nedá, přes

  server se najde. ⚠️ Netýká se to jen zkoušení - u firem s tisíci linkami je většina linek

  hledáním nedosažitelná. Oprava = poslat hledaný text na server (`GetAvailableInstances` ho už

  přijímá jako `search`), ne filtrovat načtenou stránku.

## `B113` - dvě řady písmen u minut se už nekryjí (hotovo 3. 9. 2026)

Detail → changelog 2026-09-03 (3). Obě sady se berou z nastavení u linky, prázdné pole dá
disjunktní výchozí sady (varianty `ADLMNPTY`, rozdílům tras zůstala jejich legacy sada).
Vlastní zápis se bere doslova, bez filtrování kolizí.

⚠️ **Řešení může být dočasné** (Jan + kolega 3. 9.): nastavení sad u linky se možná ještě
upraví. Dnešní stav zavírá dezinformaci na vývěsce, není to finální tvar nastavení.

**Zlaté odtisky přeraženy s doloženým rozdílem** (R6): počty u všech tří linek shodné do jednoho
a izolovanou kontrolou se sadou vrácenou na abecedu prošly s původním md5, takže rozdíl
nepochází odjinud. Testy disjunktnosti v té konfiguraci naopak spadly - druhá polovina důkazu.

Zadání, se kterým se do toho šlo:

- **`B113` Písmena variant tras a rozdílů tras se kryjí** [M] (nález kolega + Jan 2. 9., z vývěsky
  linky 111). **Zadání rozhodnuté 2. 9., jde vzít rovnou.**

  **Problém.** Varianty tras berou `A, B, C, D, E…` (zadrátovaná abeceda v `PismenoVarianty`),
  rozdíly tras `B, C, E, F, G…` ze sady u linky. Od druhé písmenkované varianty výš se řady
  prolezou, takže **jedno písmeno u minuty má dva významy** a legenda ho vysvětlí cizím textem:
  spoj jedoucí variantou „E" si čtenář spáruje s „jede jen do zast. Babice,Zvěřetice,rozc.1.0".
  Na vývěsce pro cestující je to tichá dezinformace, ne kosmetika. ⚠️ **U jediného písmene navíc
  nejde poznat, které z těch dvou to je** - pořadí u minuty je pevné (rozdíl, pak varianta), ale
  spoj bez rozdílu trasy nese jen variantní písmeno.

  **Kde se ty sady nastavují** (obojí per linka, EDISON → linka → „Nastavení k tisku" → záložka
  „Odjezdník MHD"):
  „Volné značky" = rozdíly tras (`^JRL(sku,lin,"PATCH","mhd","notchar")`, wire klíč
  `ODJEZDNIK_POZNAMKOVAT_ROZDILY_OZNACENI`, pole 32 API linky) a „Volné značky pro minutové trasy
  spojů" = varianty (`…("PATCH","mhd","par",2)`, klíč `ODJEZDNIK_ZNK_CAS_PRUBEHY_TRAS`, pole 74).
  Ze stringu se bere **znak po znaku od začátku** (první rozdíl = první znak); přes délku se
  písmena zdvojují (`BB`, `CC`) - `GetZnk` v legacy i u nás.

  **Rozhodnutí (Jan 2. 9.):** obě sady se čtou **prioritně z nastavení u linky, a doslova** - žádné
  filtrování kolidujících písmen. Kdo si napíše `ABCDE`, dostane to i s kolizí; je to jeho volba
  a tiše mu do ní sahat je horší. Východisko pro takovou linku je, že jsou nastavitelná **obě**
  pole, takže se sada rozdílů přesune mimo abecedu. Prázdné pole → výchozí sada:

  - **rozdíly tras: `BCEFGHIJOQRWeijqsvwxz`** - bez jediné změny, je to 1:1 legacy
    (`JRapiLinPrtMhd.mac:539`), a náš přepis je věrný.
  - **varianty tras: `A, D, L, M, N, P, T, Y`** - vzestupně a disjunktně. Vyškrtané je vše, co už
    na vývěsce jako značka je: sada rozdílů **včetně malých písmen** (velikost nerozlišovat, „V"
    vedle „v" ve výšce písma odjezdů nikdo nerozezná), `R` místenka, `K` kola, `u` pokračující spoj,
    `H`/`F` bezbariér. Přes délku zdvojovat, dnešní „V27" zahodit.

  **Proč vzestupně a ne od konce abecedy** (první návrh byl `Y, T, P, N, M, L, D, A`): `A` v sadě
  rozdílů není, takže první písmenkovaná varianta může zůstat `A` - jak ji lidé vidí dnes. Linka
  se dvěma variantami se tím nezmění vůbec a divergovat začne až třetí varianta (`D` místo `B`),
  což je právě místo, kde dnes kolize vzniká. Zákazník, který si `ABCDE` výslovně přál, tedy
  ve většině případů nemusí nastavovat nic; ta pole jsou per linka a hromadnou úpravu na ně nevidím,
  takže každé ušetřené nastavení se počítá.

  ⚠️ **Vyplněné pole variant zapne písmena i ve starém modulu.** Ten pro ně **nemá žádný výchozí
  obsah** (`JRapiLinPrtMhd1.mac:27`, „UVWXYZ" je jen komentář), takže dnes variantní značky netiskne
  vůbec - na žádné z 325 prověřených linek to pole vyplněné není. Paritu tedy neporušíme ani jednou
  volbou, ale kdo pole vyplní kvůli nám, uvidí písmena i ve staré sestavě.

  **Dotkne se:** `TestPismenoVarianty` (dnes čeká A, Z, V27) a zlatých odtisků sady vývěsek -
  odtisky se změní **záměrně**, takže se rozdíl doloží, ne přebaseline naslepo. Pojistka „bez
  sloupce Varianty tras se písmena netisknou" je hotová (2. 9.), tohle je ta vlastní oprava.

  **Užitečné předem:** změřit, kolik linek má které z těch dvou polí reálně vyplněné - u sady
  rozdílů to zmapované není taky a je to táž otázka.

## `B115` - dlouhý text se zmenší, aby se vešel do prvku (hotovo 3. 9. 2026)

Detail → changelog 2026-09-03 (2). Zmenšuje se bez volby a i v hotových šablonách, ale s **dolní
mezí** (polovina nastavené velikosti, nikdy pod 4 pt); pod mezí se text ořízne a řekne se to.

**Rozhodnutí Jan 3. 9.** - obě otázky ze zadání:

- **Bez volby, včetně hotových šablon.** Nová volba by byla nekonzistentní se zbytkem aplikace
  (buňky tabulek ani prvky jízdních řádů nic nezapínají) a kdo dnes má text tiše oříznutý, si
  toho dál nevšimne. Cena: tisk hotové šablony s oříznutým textem se změní.
- **Mez, pod ní ořez jako dnes** (proti doporučení „bez meze jako všude jinde"). Hodnota
  polovina / 4 pt je odvozená z dolní hranice pole Velikost, ne odhad.

Zadání, se kterým se do toho šlo:

- **`B115` Text v textovém prvku se nezmenší, aby se vešel** [M] (podnět kolega 3. 9.). Dlouhý text
  z prvku vyteče nebo se odřízne; uživatel to musí řešit ručním zmenšováním písma. **U tabulkových
  prvků to už funguje** (buňka si písmo zmenší sama, „přizpůsobit buňce" jako v Excelu), takže je
  to nesourodost mezi prvky, ne nová schopnost - jen ji textový prvek nemá.

  **K rozhodnutí, než se do toho půjde:** má se zmenšovat **vždy**, nebo jen se zapnutou volbou?
  Automatika bez volby změní vzhled hotových šablon (dnes se text odřízne, po změně by se zmenšil),
  takže výchozí stav je vypnuto, nebo je potřeba migrace. Druhá otázka je **dolní mez** - kam se
  písmo smí zmenšit, než je jasnější text zkrátit; tabulkové prvky mez mají a stejnou hodnotu je
  rozumné vzít i sem.

## `B114` - varování „obsah se nevejde" řekne, kde a co (hotovo 3. 9. 2026)

Detail → changelog 2026-09-03 (1). Hláška jmenuje stránku, sekci i prvek, který bere nejvíc
místa; tentýž popis je i v souhrnu tisku.

⚠️ **Při práci se ukázalo, že prvek nemůže přesáhnout svou sekci** - engine jí dává
`max(nastavená výška, obsah)`. Podmínka „prvek přesahuje sekci" v detekci přetečení tedy nemohla
nastat (mrtvý kód) a zadání se tím posunulo: reálné přetečení je vždy „sekce se nevejde na
stránku", takže hláška říká, **kdo ji nafoukl** - prvek, nebo nastavená výška sekce. Náprava je
u každého jinde.

Zadání, se kterým se do toho šlo:

- **`B114` Varování „obsah se nevejde" neřekne, kde** [S] (podnět kolega 3. 9.). Červený rámeček
  kolem stránky náhledu a hláška ve stavovém řádku říkají jen „něco přetéká" - uživatel neví, na
  které stránce ani co ji přetéká, takže nemá kde začít. ⚠️ **Ta informace už existuje a zahazuje
  se**: náhled si počítá přetečení **pro každou stránku zvlášť** (`PreviewCanvas.PageOverflows`
  prochází umístěné sekce) a nahoru posílá jediný `bool` (`OverflowChanged`). Do hlášky tedy jde
  dostat číslo stránky i jméno sekce, aniž by se cokoli počítalo znovu. Vzor, jak má taková hláška
  vypadat, je vedle v témže stavovém řádku - varování o nezařazených odjezdech jmenuje konkrétní
  minuty i cestu k nápravě.

## `B111` - dopravce po jednotlivých polích (hotovo 1. 9. 2026, provozovny → `B112`)

Detail → changelog 2026-09-01 (7). Pět samostatných polí dopravce (Název, Adresa, Telefon, E-mail,
IČO). `POPIS_DOPRAVCE` zůstalo nedotčené. **Fax se nevystavuje** - na vývěsce nemá co dělat
(rozhodnutí Jan 1. 9.); číselník ho drží dál.

⚠️ **Provozovny se nedořešily a pokračují jako `B112` v Backlogu.** Kolekce „Dopravci" je **skrytá
ze schématu**: řádek na dopravce × provozovnu nutně opakuje údaje firmy (tři provozovny = třikrát
týž telefon pod sebou) a tabulka z dat deduplikaci neumí, takže by to vyměnilo jednu duplicitu za
druhou. Kód, data na drátě i testy zůstaly živé, ať se na tom dá stavět.

⚠️ **Příčina duplicity byla v datech, ne v našem skládání** (ověřeno na CB_CSADAD): pole „zkrácený
název pro MHD" obsahuje u ČSAD Jindřichův Hradec celý text včetně telefonů, takže se telefon dostal
do výstupu dvakrát. To pole se **nevystavuje** a nemá se vystavovat.

⚠️ **Adresa firmy je v číselníku ve dvou polích a NEJSOU vždy totožné** - u ČSAD AUTOBUSY České
Budějovice nese plnou adresu s PSČ a obcí teprve druhé. **Bere se druhé, první je záloha.** Odhalilo
to ověření voláním serveru, ne úvaha; první verze brala kratší a zákazníkovi by zmizelo PSČ a obec.
Hlídá `ZastavkoveJrAdDopravciTest`, který porovnává s obsahem starého slepeného pole.

⚠️ **Co se vědomě NEvystavuje:** čtyři pole číselníku s nesourodým významem napříč dopravci (jedno
vypadá na DIČ, jiné na bankovní účet) - pole s popiskem, který u poloviny dopravců lže, je horší než
žádné. Kdyby je někdo potřeboval, význam se musí dohledat u kolegů z jízdních řádů.

⚠️ **Adresa se nedělí na ulici a obec** - v číselníku je to jedno pole a parsování by u tvarů jako
„č.p. 138, 569 07 Radiměř" vyrábělo nesmysly. **Nezkoušet.**

Skalární pole nesou prvního dopravce, víc dopravců se skládá z kolekce (rozhodnutí Jan 1. 9.).
Měření, kolik linek má víc dopravců, se **nedělalo** - tímto nástrojem to nešlo prakticky provést
a návrh je bezpečný bez ohledu na výsledek.

Neuděláno: **MHD agenda** (data má z jiného zdroje, systémový namespace) a **linkový JŘ** - týž
rozklad by tam šel udělat stejným tvarem, až o to někdo požádá.

## `B108` - nadpis poznámek jde vypnout (hotovo 1. 9. 2026)

Detail → changelog 2026-09-01 (6). Volba **„Záhlaví"** ve skupině **Obecné** schová celý pruh (text
i podbarvení) a pruh přestane zabírat výšku. Pole „Nadpis" se přejmenovalo na **„Text záhlaví"** -
projekt drží slovo „záhlaví" a dvě slova pro tutéž věc v jednom formuláři pletla.

⚠️ **Prázdný nadpis pruh NERUŠÍ** - pás bez textu je legitimní, proto samostatný vypínač. Původní
návrh vázal vypnutí na prázdný text; checkbox vyhrál, protože ho má už pět prvků a nemění hotové
šablony nikomu (rozhodnutí Jan 1. 9., jeho námitka proti mému návrhu byla věcně správná).

⚠️ **Nadpisový pruh se reálně tiskne jen u poznámek.** „Hod" u odjezdů a „Linkový JŘ - zastávky ×
spoje" kreslí jen `RenderPlaceholder`, tedy náhled bez dat. Bez vypínače zůstaly jen dvě věci a ani
jedna není nadpis: řádek s hodinami u odjezdů a řádek čísel spojů u linkového JŘ - **nedoplňovat**,
bez nich se JŘ nedá přečíst.

⚠️ **Klíč `ShowHeaderBar`, ne `ShowHeader`** - ten u zastávek a tabulek znamená záhlaví sloupců.
Odhalil to drift test popisků, ne úvaha.

## `B109` - změna velikosti prvku klávesnicí (hotovo 1. 9. 2026)

Detail → changelog 2026-09-01 (5). **Shift+šipky = velikost** (pravá a dolní hrana), **Ctrl+šipky =
posun po mřížce**, samotné šipky = posun o 1 mm. Rozhodnutí Jan 1. 9. podle starého návrháře.

⚠️ **Velký krok posunu zanikl bez ztráty** - visel na Shiftu a počítal se z mřížky, jejíž výchozí
hodnota je 1 mm, takže dělal totéž jako šipka samotná. Jemný krok 0,5 mm na Ctrl ustoupil kroku
mřížky.

⚠️ **Prvek s dynamickou výškou** se svisle nemění a řekne se to ve stavovém řádku; fixace výšky bez
varování byla zavržená (myš mu svislé úchyty ze stejného důvodu nenabízí).

⚠️ **Soused se při rozšíření prvku NEZUŽUJE** - podnět z toho vycházel, ale ověřeno, že tak myš
nikdy nefungovala: sousedi jsou jen přichytávací cíle. Zužování dvojice u hrany platí **jen uvnitř
tabulky**. Kdyby se to mělo zavést pro prvky, je to samostatná featura a musela by změnit i chování
myši, jinak by byly nekonzistentní.

Vedlejší nález: v uživatelské příručce stálo, že sekci přesouvají samotné šipky - je to
Ctrl+Shift+šipky. Opraveno.

Přibyla brána `SipkyMajiPrirazenouAkci` nad `ResolveArrowAction`: mutace ukázala, že odpojení Shiftu
od velikosti nechá sadu zelenou, protože ostatní testy volají `ResizeByKey` přímo.

## `B110` - rozložení poznámek se řídí velikostí písma (hotovo 1. 9. 2026)

Detail → changelog 2026-09-01 (4). Velikost, výška řádku a počet sloupců pohromadě; výška řádku
prázdná = podle písma; při nevejití se písmo zmenší a aplikace to oznámí.

⚠️ **Roztahování na výšku se vědomě NEDĚLÁ** (rozhodnutí Jan 1. 9.). Podnět byl „legenda se nezatáhne
dolů, když je prvek vyšší", ale dvě poznámky mají zůstat nahoře a vyplnit klidně pětinu bloku -
roztahování by tři poznámky rozprostřelo na začátek, doprostřed a konec. **Nezavádět.** Platné
pravidlo je: co je nastavené, to se vytiskne; nevejde-li se to, písmo se zmenší a řekne se to.

⚠️ **Skutečná chyba byla jinde, než podnět mířil:** výška řádku byla pevných 4 mm a nereagovala na
písmo. Ta hodnota je přesně automatická výška pro 7 bodů, takže výchozí stav vypadal správně, ale
zvětšení písma legendu slepilo.

Upozornění na zmenšení platí pro **všechny prvky jízdních řádů** (`ContentShrinkWarnings`), ne jen
pro poznámky - zmenšení bylo neviditelné u všech pěti. Jmenuje prvek i míru, ne obecně „něco se
změnilo" (to byl zrušený `B24`).

Odloženo jako samostatná věc: **dopočítání počtu sloupců** podle výšky prvku. Je to nejlepší odpověď
na dlouhé legendy (patnáct poznámek se ve dvou sloupcích vejde bez zmenšení), ale je to jiná featura
a nikde ji nemáme - vytáhnout, až se ukáže, že zmenšování u dlouhých legend reálně vadí.

## `B107` - vymazané podbarvení záhlaví znamená „nekreslit nic" (hotovo 1. 9. 2026)

Detail → changelog 2026-09-01 (3). Prázdné pole = záhlaví bez podbarvení, a vydrží to i po uložení.

⚠️ **Nešlo o chybu ukládání**, i když tak vypadala: šedá v prvku vůbec nebyla zapsaná, byla zapečená
jako **záloha kaskády** a jako výchozí hodnota pole ve formuláři. Prázdno proto znamenalo „nic
neurčuji, vezmi zálohu" a „bez podbarvení" nebylo jak vyjádřit. Šedá se přesunula do počáteční
hodnoty prvku; existujícím šablonám ji dopsala migrace **1.11.0**, takže se vzhled nikomu nezměnil
(rozhodnutí Jan 1. 9., dvakrát potvrzené včetně ceny za migraci).

Platí u poznámek, zastávek, odjezdů, linkového JŘ, pevné tabulky i tabulky z dat. **Dědění stylů
zůstalo** - prázdno u sloupce dál znamená „vezmi z prvku", kaskáda končí až u prvku.

⚠️ **Vedlejší nález, který za to stál:** migrace vyřazená ze seznamu nechala celou sadu zelenou,
protože testy migrací je volají přímo - zákazníkovi by se přitom šablona nemigrovala vůbec. Přibyla
brána `KazdaMigrace_JeVProdukcnimSeznamu`.

⚠️ **Odtisky kreslení tuhle třídu změn nekryjí** (porovnávají se na vyžádání a jejich prvky mají styl
záhlaví natvrdo). Doklad je v migraci a jejích testech.

## `B106` - desetinná čárka v číselných polích (hotovo 1. 9. 2026)

Detail → changelog 2026-09-01 (2). Čárka i tečka na vstupu, uvnitř i na drátě dál tečka.

⚠️ **Nebylo to o pohodlí, ale o tiché chybě:** kontrola brala čárku jako oddělovač tisíců („1,5" = 15),
takže pole nezčervenalo, ale zápis do prvku si totéž přečetl jako neplatné a použil záložní nulu -
šířka spadla na 0 bez hlášky. „1,500" se čtelo jako 1500, „-2,5" jako -25.

**Čárka je vždy desetinná, oddělovač tisíců se nepřipouští** (rozhodnutí Jan 1. 9.): milimetry ho
nepotřebují a pravidlo „tisíce, když jsou za čárkou tři číslice" by nikdo neuhádl. Celočíselné pole
čárku dál odmítá.

⚠️ **Opraveno v EDControls** (`Field`), tedy i pro EDISONMAIN2 - publikovat knihovnu i oba programy.
Druhá vrstva je v návrháři (`ElementPropertyAccessor`) pro hodnoty mimo formulářovou cestu.

## `B105` - klik trefí prvek, který je vidět navrchu (hotovo 1. 9. 2026)

Detail → changelog 2026-09-01 (1). Hledání prvku pod kurzorem šlo při shodné hladině obráceně než
kreslení, takže se nový prvek nakreslil navrch, ale klik i tažení sáhly na starý pod ním. Nález Pavel
při skládání vývěsky pro BBSK (#13158); vypadalo to jako nespolehlivost, protože rozhodovalo pořadí
vzniku prvků.

⚠️ **Druhá polovina byla výběr ze stromu:** klik na vybraný prvek, který jiný překrývá, výběr zahodil,
takže prvek schovaný pod jiným nebylo jak odtáhnout. Nově má přednost prvek, který je už vybraný
(platí i pro dvojklik, pravé tlačítko a tvar kurzoru).

⚠️ **Do šablony se nezapisuje nic** - hladiny zůstávají. Zvednutí hladiny při vložení bylo zavržené:
měnilo by data šablony, naráželo na strop 0-100 a výběr ze stromu by neopravilo (rozhodnutí Jan
1. 9.).

## `N1` - zóny hraniční zastávky do vlastních sloupců (hotovo 31. 8. 2026)

Detail → changelog 2026-08-31 (15). Volba u prvku, výchozí vypnutá; dělí se čárkou i lomítkem.

⚠️ **Není to parita** - starý modul zóny do sloupců nedělí. Proto je výchozí stav vypnutý: víc zón
má 301 738 zastávek měřených firem a zapnutý rozpad by překreslil hotové sestavy i tomu, kdo o něj
nežádal. Na drátě se nic nemění, rozdělení si dělá prvek sám.

## `T1` - sdílený spouštěč testů si přičítal cizí běhy (hotovo 31. 8. 2026)

Detail → changelog 2026-08-31 (14). Spouštěč bral všechny výsledky, které na dev serveru vznikly po
jeho startu, tedy i ty od kolegy, který si zrovna pouštěl svou sadu. Nově počítá jen třídy, které
sám spustil.

⚠️ **Zbytkové riziko zůstává a je zapsané u kódu:** dva souběžné běhy **téže** třídy filtr neodliší.
Počty to nerozhodí, ale cizí selhání by se pořád ukázalo jako naše; oddělit by to šlo až na úrovni
`%UnitTest`, ne ve výsledcích.

Je to v `EDTesting/`, tedy ve **sdílené infrastruktuře mimo TD** - commit je proto samostatný.

## `L14` - dvě neověřená tvrzení o shodě se starým tiskem (hotovo 31. 8. 2026)

Detail → changelog 2026-08-31 (13). Věty u atributů spoje (zásilky, IDS) doložené porovnáním se
starým modulem na linkách, kde ty atributy v datech skutečně jsou; hlídá je nový test.

**Jedno z těch tvrzení bylo věcně špatně.** V kódu stálo, že starý modul počítá spoje přes celou
linku a my přes stránku. Ve skutečnosti počítá v rozsahu, ve kterém tiskne legendu - a náš model
(stránka na směr s vlastní legendou) tomu odpovídá. Doloženo na `VS_CSAD 24/0941210`.

**Vedlejší nález, který stál za víc než sama položka:** porovnání se starým modulem si vyrábělo
falešný rozdíl (pouštělo starý modul v jiném režimu legendy, než jaký má náš výstup) a na lince
s dlouhou poznámkou celé padalo. Obojí opraveno - jinak by se v tom šumu ztratil příští skutečný
rozdíl.

## `L11` - značky spoje piktogramem podle překladové tabulky legacy (hotovo 31. 8. 2026)

Detail → changelog 2026-08-31 (11) a (12). Rodina atributů spoje (občerstvení, zásilky, radiobus,
samoobslužný, podmínečné jetí) se přestala tisknout zkratkou.

⚠️ **Oprava dřívějšího tvrzení:** roadmapa i ranní zápis tvrdily, že překlad „zkratka → glyf"
v repu **není**, protože žije ve windowsím tiskovém klientovi. Ten klient v repu **je**
(`TiskJR/formTISKJRReportDotNet_Module.vb`), jen se hledalo v serverové části. Tabulka
`ALVA.TISKJR.Main.ConvertSpecialniZnak` překládá id číselníku na kód znaku, klient řeší záporné
hodnoty jako text. Pozice jsou tím **opsané, ne odvozené z tvaru glyfu**, a tabulka potvrdila
i všechno, co se předtím nasadilo podle tvaru.

**Co z toho vzešlo mimo samotnou featuru:**

- **IDS a nízkokapacitní vůz zůstávají textem** - ne z opatrnosti, ale protože je tak tiskne
  legacy. Nízkokapacitní navíc jako **„n"**, ne „NKA".
- **Autobus 119 × 122 není odchylka, ale jiný font** - legacy sází starším fontem „Timetable".
  Čísla v té tabulce se proto u nové značky nesmějí jen opsat.
- **`B99` rozhodnuta faktem** - vývěskový legacy modul loď, wifi ani zásuvku vůbec nezná, takže
  piktogram by tam byl nová featura, ne parita.
- **Změřeno, kde ty atributy v datech vůbec jsou** (sonda `AtributySpojuVyskyt`). Ranní tvrzení
  „není nastavené nic než BEV" neplatilo. **SAO, CBE a zásuvka ale nejsou nikde** - opsané, na
  reálném tisku neviděné.

## `L15` - volby mřížky u prvku odjezdů (hotovo 31. 8. 2026)

Poslední prvek, který obě volby nabízel a nečetl. Zapojeno **podle sémantiky pivotu**, ne mechanicky:
řádek je hodina, sloupec je skupina dnů, a **svislá čára za sloupcem hodin zůstává vždy** - je to osa
mezi hodinou a jejími minutami, ne oddělovač sloupců (rozhodnutí Jan 31. 8., varianta B).

⚠️ Předcházela tomu otázka, jestli se tím neotvírá rozhodnutí „mřížka odjezdů je vyčleněná".
Neotvírá: vyčleněný je **render** (je to pivot, sdílená smyčka „řádek = sekvence sloupců" jeho tvar
neumí popsat), kdežto datovou vrstvu sdílí plně - a důvod toho rozdělení je doslova „z pohledu
uživatele je konzistentní". Zaškrtávátko, které nic nedělá, je přesně ta nekonzistence.

Riziko změřeno sondou přes **všechny uložené šablony**: 18 šablon, 12 prvků odjezdů, 0 s odškrtnutou
volbou → zapojení nikomu tisk nezměnilo. Sonda (`SablonyDiag.MrizkaOdjezduProbe`) zůstala.

Doloženo 6 testy a mutacemi (volby ignorované; osa mizící spolu se sloupci). ⚠️ **Past u fixtury:**
odjezd se ke skupině dnů váže polem `SKUPINA_DNU_ID` - s jiným názvem se nepřipojí, nevzniknou hodiny
a prvek nevykreslí ani pixel. Vypadá to jako „prvek nejde v testu vykreslit"; není to pravda.

## `L12` - rozdělení parametrů tisku linkového JŘ (hotovo 31. 8. 2026)

Tři skupiny místo dvou, jedna z nich s jedinou položkou (`DELENI_SLOUPCU`), protože jí chybělo
`group` - a parametr bez zařazení spadne do vlastní skupiny „Parametry sestavy". „Obecné" navíc
vycházelo prostřední, protože skupiny se řadí podle prvního výskytu v poli.

Nově **Obecné** (tvar názvu zastávky, název pro MHD, obousměrný tisk) a **Tabulka linky** (dělení
spojů, sloupec TČ, zastávky bez zastavení, barvy spojů, zvýraznění zastávky i TČ). Obousměrný tisk
je v „Obecném" vědomě - rozhoduje o složení celé sestavy, ne o vzhledu prvku (rozhodnutí Jan 31. 8.).
Skupina přejmenovaná z „Linkový JŘ" na „Tabulka linky", aby odpovídala logice vývěsek. Názvy voleb
ani hodnoty se nezměnily → uložené uživatelské volby zůstávají.

Přibyla pojistka `AgendaParamGroupsDriftTests` (parametr bez skupiny + „Obecné" první). Našla hned
druhý případ - `FORMAT_CASTKY` v agendě faktur. ⚠️ Nepokrývá parametry zakládané přes `AddBoolParam`
(skupina je tam až pátým argumentem a popis běžně obsahuje čárky, takže regulár na to nestačí).

⚠️ **Poučení k zapamatování:** pořadí pushů jsem nejdřív přepsal skriptem a `.cls` se tím na server
**nenasadil** - skupiny (dělané editačním nástrojem) tam byly, pořadí ne. Testy to nechytily, protože
čtou zdroj z repa; odhalilo to až ověřovací volání `GetParameters`. Přesně ten scénář, na který
upozorňuje `cache-api.md`.

## `L13` - nabídka stránek náhledu pojmenuje i linkový JŘ (hotovo 31. 8. 2026)

`BuildPreviewPageLabel` stavěl popisek výhradně z `POPIS_ZASTAVKA`, což je pole **zastávkového** JŘ -
linkové agendy ho ve schématu nemají. U linkového JŘ proto padal na „Stránka N" u **každé** stránky,
ne jen u obousměrné, jak to vypadalo při prvním nálezu.

Nově má linka vlastní větev: `VYCHOZI_ZASTAVKA` → `KONECNA_ZASTAVKA` („1 - Praha → Sedlčany").
**Obousměrná stránka** se pozná podle prázdného `SMER` (agenda ho u ní vědomě nevyplňuje, nese oba
směry naráz) a dostane obousměrnou šipku - díky tomu nebylo potřeba nové pole na drátě, což byla při
zadání otevřená otázka. Zastávková větev má přednost a je na to vlastní test.

⚠️ Pozn.: původní zápis `L13` mluvil o poli `POPIS_SMER`. To se týž den přejmenovalo na
`KONECNA_ZASTAVKA` (#13158, migrace šablon 1.10.0), takže v kódu i tady figuruje nový název.

## `B104` - změna formátu stránky přepočítá obsah (hotovo 31. 8. 2026)

Změna velikosti nebo orientace ve Vlastnostech stránky šablonu **vymazala** a založila prázdné
plátno. Nově se přepočítá geometrie (pozice, rozměry, písmo, odsazení, výšky řádků, tloušťky čar)
a **nastavení prvků zůstává** - sloupce, styly, volby, podmínky, datové zdroje.

**Poměr z tiskové plochy, ne z papíru** - prvky leží uvnitř okrajů, takže z rozměru stránky by se
souběžná změna okrajů promítla dvakrát. ⚠️ Proto ani při stejném poměru papíru nevyjde přepočet
beze zbytku (okraje se neškálují) a obsah se posune o jednotky milimetrů - je to správně.
Při změně orientace se bere **menší z obou os** (nic nepřeteče) a obsah se **vodorovně vystředí**;
svisle zůstává nahoře, protože sekce jdou odshora jako dokument (rozhodnutí Jan 31. 8.).

Rozměr a okraje se berou **přesně z dialogu**, ne přeškálované - proto se zapisují až po přepočtu
obsahu. Celé je to jeden vratný krok; stránka se vrací z odloženého snímku (přesně), obsah
převráceným poměrem (na desetinu milimetru, jako u ručního škálování).

⚠️ **Tlačítka zůstala Ano/Ne/Storno**, význam nese text otázky - akční popisky by znamenaly sáhnout
do sdíleného dialogu v EDControls a publikovat i druhou aplikaci. Prázdná šablona se neptá vůbec.
**Prvky, které by po přepočtu vyčnívaly, se neposouvají** - to je vědomé, ne opomenutí.

Ruční škálování z nabídky zůstalo beze změny; obě cesty stojí na společném `TemplateScaling`, jen
ruční škáluje i papír. Ověřeno mutacemi: větší poměr místo menšího, vynechané vystředění,
přeškálované okraje místo hodnot z dialogu.

⚠️ **Dodatek z téhož dne:** zkoušení odhalilo, že otočení tam a zpátky obsah scvrklo na 47 %
(poměr „aby se to vešlo" vyjde v obou směrech stejně, takže se dvě otočení násobí). Spraveno tím,
že se poměr počítá od **výchozí tiskové plochy**, kterou si šablona drží po dobu, co je otevřená -
ne od aktuálního stavu. Návrat na původní formát tak vyjde přesně, i přes mezikroky.

## `L10` - volby mřížky u linkové tabulky (hotovo 31. 8. 2026)

`LineTableElement` čte `Grid.ShowRowLines` a `Grid.ShowColumnLines` stejně jako seznam zastávek,
pevná tabulka i tabulka z dat. Do plánu řádků jdou dvě barvy místo jedné (svislé × vodorovné) a `null`
znamená „ten druh čar se nekreslí". Jemnější `PopisneSloupceBezVnitrnichCar` zůstává - `ShowColumnLines`
je nadřazený vypínač, ne jeho náhrada.

**Riziko se změřilo, ne odhadlo.** Volba se ukládá do šablony, i když nic nedělala, takže zapojení
mohlo někomu tiše sebrat mřížku. `API.Sablony.ListTemplates` ale vrátil, že agenda `LINKOVY_JR_AD`
nemá na dev serveru **ani jednu** šablonu (CB_CSADAD má 7, všechny zastávkové), takže migrace nebyla
co dělat. Jediný artefakt s uloženými volbami je vzor **940 (IDZK)** v repu (`showRowLines: false`) -
ten o vodorovné čáry přijde a **nechává se tak** (rozhodnutí Jan 31. 8., odpovídá to záměru).

⚠️ Zjištěno při tom, že **mřížka odjezdů má tutéž vadu** - vyčleněno do `L14`, protože zastávkové
šablony se používají a je u nich navíc otevřená otázka, jestli má jedno zaškrtávátko brát i nosnou
čáru za sloupcem hodin.

Ověřeno mutacemi: volby úplně ignorovat, a přehodit je mezi sebou - obě spadly na nových testech,
které měří čáry táhnoucí se přes celou obsazenou šířku a výšku tabulky (text zastávek takový pruh
neudělá).

## `L6c` - turnusy ke spojům + zbytky ze `L6b` (hotovo 28. 8. 2026)

**Turnusy ke spojům** jako **volba prvku** „Turnusy ke spojům": řádky s označením turnusu pod čísly
spojů, počet řádků podle nejbohatšího spoje, prázdné u spojů s méně turnusy, nad řádkem platnosti.
Turnus je provozní věc dopravce, ne informace pro cestujícího (rozhodnutí Jan 28. 8.), takže patří
k sestavě - stejně jako v legacy, kde to je nastavení sekce v návrháři. Serveru se volba posílá ze
šablony jako `DEFINICE_SLOUPCU`, protože dotaz do modulu turnusů stojí 40-550 ms na linku a nemá ho
platit, kdo turnusy netiskne. **Tiskne se číslo turnusu**, ne označení s variantou po tečce: modul
vrací záznam za každé zpracování × datum, takže jeden turnus jinak vyjde i v osmi řádcích.

⚠️ **Starý engine s naším tvarem čísla linky turnusy nevrátí** - odpověď dotazu filtruje číslem bez
vodicích nul, takže s klíčem z `^JRL` („0108360") nespáruje nic (`TUmax` = 0). Změřeno oběma tvary.
**Co posílá skutečný klient TiskJR, ověřené není** - rozhodne až tisk ze starého modulu. Hodnoty se
proto neověřují oraclem (v našem tvaru není proti čemu), ale proti témuž zdroji, jaký volá legacy;
pozice řádků je odvozená z kontraktu enginu. Zapsáno i v `legacy-nahrada.md §3.3`.

**Zbytky ze `L6b`:** zrcadlený blok má **vlastní šipku směru** - sloupec „Šipka směru" **za** názvy
zastávek ukazuje nahoru (obsluhuje protijedoucí blok), před nimi dolů; legacy na to má druhý typ
sloupce, u nás rozhoduje pozice, takže to nepotřebuje ani nový klíč sloupce, ani migraci šablon.
A **dorovnání prázdnými sloupci** doplňuje i levou stranu, na její **vnější** hraně - jinak se
v posledním bloku posunuly zastávky o celý sloupec (15,8 mm na fixtuře) a svisle nesedly na bloky
nad sebou. Dorovnání se počítá per strana, takže sedí i na linku, kde má jeden směr méně spojů.

Detail v changelogu 28. 8. (8) a (9).

## `L6b` - obousměrný tisk (hotovo 28. 8. 2026)

Oba směry na jedné sestavě: sloupce spojů po obou stranách zastávek. **Nalevo jde směr, jehož pořadí
sleduje seznam zastávek** - doloženo tiskem ze starého modulu 28. 8., ne čtením zdroje (odvození
z indexů sloupců v `MT2A` vyšlo obráceně, o straně rozhoduje typ sloupce). **Parametr sestavy, ne tichý
fallback** (rozhodnutí Jan) - starý modul zrcadlo vypne, když se blok nevejde na stránku bez řezu,
ale ten fallback mít **musel**, protože tiskl naslepo do pevné stránky.

**Server posílá jen fakta, tvar drží klient** (požadavek Jan): jedna stránka, všechny spoje obou
směrů nad společným seznamem zastávek, každý se svým `SMER`. Návrh se dvěma kolekcemi byl zahozen -
propašovával layout do dat.

Detail v changelogu 28. 8. (4), (5) a (7). Zbývá z toho **vlastní šipka směru pro zrcadlený blok**
(legacy má na to druhý sloupec, typ 8) a dorovnání prázdnými sloupci jen vpravo - vedeno u `L6c`.

Původní zadání:

| **L6b** | **Obousměrný tisk** (legacy zrcadlené sloupce spojů, typ 9). Oba směry na jedné stránce: opačný směr dostane sloupce spojů **nalevo od názvů zastávek** plus zrcadlenou šipku. Data už oba směry posílají (stránka per směr), takže dnešní stav = legacy fallback. ✅ **Rozhodnuto (Jan 28. 8.): bude to parametr sestavy, ne tichý fallback** - starý modul zrcadlo tiše vypne, když se společný blok nevejde na stránku bez řezu (`:598-628`), ale ten fallback mít **musel**, protože tiskl naslepo do pevné stránky; náš uživatel vidí náhled a rozhodne se sám. Je to **vědomá odchylka od legacy**, ne dluh. Druhá půlka pravidla (v jednom směru nejsou spoje → zrcadlo ne) platí a vyjde skoro sama z „prázdný sloupec se netiskne". ⚠️ Prvek dnes spoje nalevo neumí - největší strukturální zásah do `LineTableElement` od jeho vzniku; zlevnilo ho soustředění souřadnic do `SloupcovaMrizka` (27. 8.). | M–L |

## `L6a` - šipka směru u zastávek v linkovém JŘ (hotovo 28. 8. 2026)

Sloupec se svislou šipkou přes řádky zastávek. **Kreslí se naší vektorovou šipkou ze zastávkového
JŘ**, ne glyfy ze symbolového fontu jako legacy (rozhodnutí Jan) - kresba se vytáhla do
`DirectionArrowRenderer` a vývěska ji volá odtud, beze změny jediného pixelu.

Z Janova zkoušení vypadly tři věci: **hrot až na poslední stránce** (záměr, shodné s legacy - hrot
znamená konec linky, ne konec stránky; při rozpadu na **bloky** je v každém bloku), **anglický název
sloupce v dialogu** (mapa lidských názvů propadá na klíč) a **barva nastavená v zóně záhlaví šipku
neobarvila** (šipku řídí datová zóna, což u sloupce bez textu nešlo poznat - zóna se u šipky jmenuje
„Styl šipky" a nabízí jen barvu a pozadí).

Detail v changelogu 28. 8. (1)-(3). Původní zadání:

| **L6a** | **Šipka směru u zastávek** (legacy sloupec typu 5, u obousměrného navíc typ 8). Úzký sloupec vedle zastávek, ve kterém běží **svislá šipka přes celý seznam**; v opačném směru se začátek a konec vymění. Obsah je odvozený z pozice zastávky ve směru, žádná nová data nejsou potřeba (`JRapiLinPrtWin.mac:3013-3029`). ⚠️ **Kreslí se naší vektorovou šipkou ze zastávkového JŘ, ne glyfy ze symbolového fontu jako legacy** (rozhodnutí Jan 28. 8.: nemá se duplikovat, když to umíme) - jedna implementace pro obojí, navíc nastavitelná. Sloupec se do existujících šablon doplní migrací (vzor `MigrationRouteVariantsColumn`), výchozí stav skrytý. | S |

## `L9-R4` - prázdný popisek pásu nad popisnými sloupci (rozhodnuto 27. 8. 2026)

**Zvoleno: bez popisku se nad popisnými sloupci nekreslí nic** (ani podbarvení). Rozhodl to legacy,
ne vkus - starý modul tam pás vůbec nemá, protože skupina odjezdů je atribut jen sloupců spojů
(„pouze pro typ sloupce 7,9", `JRapiLinPrtWin.mac:246`). Prázdný popisek = chování starého modulu,
vyplněný = featura nad rámec legacy.

Návrh udělat z toho zaškrtávátko padl: byl podmíněný tím, že si nejsme jistí, a legacy tu nejistotu
odstranil. Detail v changelogu 27. 8. (8).

Původní zadání rozhodnutí:

| # | Rozhodnutí | Stav |
|---|---|---|
| `L9-R4` ⏸ | **Co dělá prázdný popisek pásu nad sloupci?** (a) pás se schová · (b) kreslí se prázdný pruh | **Otevřené**, do rozhodnutí platí **(b)**, tedy dnešní chování. Vzor bude chtít obojí - nad popisnými sloupci nadpis, jinde ne. Týž problém má legenda poznámek |

## `C7` - rozdvojení příjezd/odjezd a padding sloupců: hotové (uzavřeno 27. 8. 2026)

Viselo to jako odložený bod z pilotu, ale **obě jmenované půlky jsou dávno hotové** a jen to nikdo
neodškrtl:

- **Padding sloupců** (legacy `arr,13` „vkladat prazdne sloupce") = volba **„Dorovnat prázdnými
  sloupci"**, hotová 25. 8. u tiketu #8372.
- **Rozdvojení příjezd/odjezd** prvek modeluje - příjezdový řádek má vlastní typ (`prijezd`)
  a popisné sloupce se v něm nechávají prázdné; má na to i testy.

⚠️ Zbytek položky byl **„jemný layout"** bez jediného konkrétního hlášení. To není nedodělek, ale
pocit z doby, kdy featura ještě nebyla. Vytáhnout, až někdo ukáže konkrétní rozdíl na tisku -
do té doby to v roadmapě jen kalilo obrázek toho, co zbývá.

Původní zadání bodu:

| # | Co | Proč odloženo / dopad | Odhad |
|---|---|---|---|
| C7 | **Rozdvojení příjezd/odjezd - jemný layout** + fixed-width padding sloupce (legacy `arr,13`) | kosmetika; TD řeší podřádky, ne padding. Padding sloupce jsou ve `L5` | S |
| C8 | **Vejdi výstup na zadaný počet stran** (podnět POVED #8785 bod 5) - starý modul to odmítl jako principiálně nemožné, náš engine má lepší pozici (`Strih` už umí režim „nejmíň stránek") | podnět, ne závazek - nabídnout až po `L5` | M |
| C9 | **Ručně zadaný „název směru" u linky** jako datové pole (#9088 bod 3, chtěné i pro vývěsky) | vyžaduje **nové pole u linky v jízdních řádech**, ne jen v návrháři - součinnost s kolegy z JŘ | S + cizí |

## `L7` - sdružený JŘ více linek: není naše práce (rozhodnuto 25. 8. 2026)

Viselo to ve vlnách jako přeškrtnutá položka, což není otevřená práce. Rozhodnutí i s důsledkem:

| **L7** | ~~Sdružený JŘ více linek~~ - **není naše práce** (upřesnil Jan 25. 8.). Sdružování linek se řeší **v jízdních řádech**; návrhář dostane až výslednou linku a tiskne ji jako každou jinou. Logiku, o kterou si řekl KOVED (#9283), tady neimplementujeme. ⚠️ **Důsledek pro #9417:** zákazníkův případ (souhrnný JŘ dvou linek s dlouhým výčtem dopravců) se k nám může dostat jako **jedna linka s mnoha dopravci**, tedy bez čekání na cokoli dalšího - až se to stane, filtrování dopravců přestane být odložené. | - |

Živý důsledek pro tiket #9417 zůstává v Backlogu roadmapy.

## `L9` - aby linková tabulka nevypadala jako jedna souvislá mřížka (hotovo 27. 8. 2026)

Tři mezery, které patřily k sobě, všechny hotové: **nadpis popisných sloupců** a **jejich slití bez
vnitřních čar** (26. 8.) a **mezera mezi skupinami sloupců** (27. 8.). Podnět: kolega zkoušel vyrobit
linkový JŘ podle vzoru IDZK 940 ve starém modulu a tohle tam nešlo.

Co z okolí bloku zbylo a žije dál: **`L10`** (dvě mrtvá zaškrtávátka mřížky + oddělená barva čar,
dřív `L9-R3`) a **otevřené `L9-R4`** (prázdný popisek pásu) - obojí v roadmapě.

Původní zadání bloku:

| **L9** | **Aby tabulka nevypadala jako jedna souvislá mřížka.** ✅ **Hotové 26. 8.: nadpis popisných sloupců a jejich slití bez vnitřních čar** (dvě ze tří mezer, detail v changelogu). **Zbývá mezera mezi bloky sloupců** - ⚠️ větší kus, než to vypadá: šířka popisného bloku slouží zároveň jako odsazení, od kterého se počítají sloupce spojů, a vycházejí z něj čtyři ruční kreslicí cesty (řádek platnosti, značky v buňce, piktogram zastávky, dělení na bloky). Vloženou mezerou se ten souřadnicový model rozejde, takže se to nejdřív musí soustředit na jedno místo. Dál pak (podklad Zlín, analýza §15 - kolega to zkoušel ve starém modulu a nešlo to). Tři drobnosti, které patří k sobě: **ohraničení per sloupec** (dnes se svislé čáry kreslí buď všude, nebo nikde, takže km podsloupce nejdou slít do jednoho bloku), **popisek nad info sloupci** (pás nad nimi už existuje, jen má natvrdo prázdný text - vzor tam má „↓ Směr jízdy ↓") a **mezera mezi bloky** info × pracovní dny × víkendy. Volitelně různá barva vodorovných a svislých čar (dnes jedna pro obojí). ⚠️ Šest dalších bodů ze Zlína **návrhář už umí** (pořadí sloupců, vypnutí MHD, barva pásu nadpisů, popisky, rostoucí záhlaví u víc dopravců) - patří do ověření na šabloně, ne do práce. | S–M |

### `L9` - co je změřeno a co čeká na rozhodnutí

📐 **Vzor 940 IDZK je přečtený** (PDF od Jana, 26. 8.), takže tvar tří mezer už není dohad:

- **Uvnitř popisného bloku žádné svislé čáry** - km (má **dva podsloupce**), zastávka a zóny běží slitě.
- **Na pravém okraji popisného bloku jedna** čára, **mezi sloupci spojů tečkované**, mezi pracovními dny
  a víkendem silnější / s mezerou.
- **Popisné sloupce jsou JEDNA skupina** s jedním nadpisem „↓ Směr jízdy ↓“. Celkem tři skupiny:
  popisná × Pracovní dny × Víkendy a svátky - přesně ty, mezi kterými má být mezera.

✅ **Tím se potvrdilo, že ty tři mezery jsou jeden pojem - `skupina sloupců`**: nadpis nad sebou,
potlačené vnitřní svislé čáry s vnějším obrysem, mezera za sebou. Prvek už jednu takovou skupinu
**má** (pás skupin dnů nad sloupci spojů), jen není k dispozici pro popisné sloupce.

⚠️ **Popis vzoru v [`§15.1`](linkovy-knizni-jr-implementace-analyza.md) nesouhlasí s PDF** ve dvou věcech:
tvrdí „jemné vodorovné linky, střídavé podbarvení“, ale vzor **nemá ani jedno**. Zkušební šablona
`templates/vzor-940-idzk.json` je srovnaná podle PDF.

**Rozhodnuto Janem 26. 8. - `R1`-`R3` ano, `R4` zůstává otevřená.** `L9` je tím vzatelná:
jádro stojí na `R1`+`R2`, `R3` se doplánuje až u samé práce a `R4` nechává dnešní chování.

| # | Rozhodnutí | Varianty | Poznámka |
|---|---|---|---|
| `L9-R1` ✅ | **Skupiny sloupců** (rozhodnuto 26. 8.) | zvolena (a) jeden pojem `skupina sloupců` | Vzor potřebuje všechny tři naráz. Samostatné volby by si šlapaly - „ohraničení per sloupec“ i „mezera mezi bloky“ musí vědět, kde skupina končí, a definovalo by se to dvakrát |
| `L9-R2` ✅ | **Spoje daty, popisné uživatelem** (rozhodnuto 26. 8.) | skupiny dnů zůstávají čistě z dat - jejich přeskládávání je featura, kterou nikdo nežádal | Dnes jsou skupiny dnů čistě z dat. Dva zdroje téhož pojmu vedle sebe |
| `L9-R3` ✅ | **U všech tabulkových prvků** (rozhodnuto 26. 8.) | ⚠️ **Tvar se doplánuje společně, až na to přijde řada** (žádost Jan) - jen u linkového by se dvě tabulky v téže šabloně nastavovaly jinak | `GridStyle` je **sdílený** (tabulka z dat, pevná tabulka, vývěskové prvky) a má dnes jednu barvu, tloušťku i styl pro obojí. Aditivní změna, ale jen tady = rozejde se to |
| `L9-R4` ⏸ | **Co dělá prázdný popisek pásu?** – **otevřené** | (a) pás se schová · (b) kreslí se prázdný pruh (dnes). Přeskočeno 26. 8. - do rozhodnutí platí **(b)**, tedy dnešní chování | Vzor bude chtít obojí - nad popisnými sloupci nadpis, jinde ne. Týž problém má legenda poznámek |

**Nad rámec tří mezer se ze vzoru vynořily ještě tři věci** (evidence, ne závazek): km jako **dva
podsloupce**; zóny v **jednom** sloupci oddělené čárkou (takže `#8576` „zóny do více sloupců“ tenhle
vzor **nepotřebuje**); a značka „na znamení“ jako **vlastní úzký sloupec** před názvem zastávky
(my ji dáváme k názvu).


## 2026-08-26 - průchod testovacími linkami linkového JŘ

Osm linek, každá na jedno konkrétní nastavení starého tisku, projitých v návrháři proti starému
tisku. Smysl byl zjistit, jestli se někde liší **data** - dokud se liší, nemá cenu řešit vzhled,
protože u každého nálezu není jasné, jestli je to chyba nebo nedodělek.

**Výsledek: žádný datový rozdíl.** Co se ověřilo:

- **dělení spojů do skupin dnů** včetně toho, že spoj jedící v sobotu i v neděli je **v obou
  skupinách** (0330510), a že linka s jedinou skupinou pás nekreslí (0330511);
- **rozdělení široké linky na bloky** i **dorovnání prázdnými sloupci**;
- **zóny a km se schovají podle linky**, i když je šablona má zapnuté (0345062);
- **sloupec tarifních čísel** ve všech třech stavech - a že **Automaticky** ho na lince
  s „netisknout Tč“ schová (0320144);
- **zastávky, kde nikdo nestaví** (0320157);
- **barvy spojů**: červený font minut je správně - legacy barví **položku v buňce**, a jen čas,
  „projíždí“ a „jede jinudy“ (doloženo řádky zdroje v analýze).

**Jediný nález: chybí obousměrný tisk** (oba směry v jedné tabulce, zrcadlené sloupce). Byla to
známá mezera odložená do `L6`; průchod k ní přidal pravidlo z legacy, že **je to podmíněné, ne
přepínač** - po střihu starý modul tiše spadne na směr zvlášť.

**Co průchod odhalil na sobě samém** (a bylo opraveno rovnou):

- ve výběru linky nešlo přepnout **skupinu linek**, takže dvě z pěti linek CB\_CSADAD nebyly
  z jednoho přihlášení dosažitelné;
- šablona k procházení kreslila **značky poznámek textem** (měla legendu jako obyčejnou
  dynamickou tabulku);
- testovací linka na barvy měla obarvené **dva spoje z půl milionu**, takže jako doklad
  nesloužila - sonda teď hlásí linku s **nejvíc** obarvenými spoji a **vždy i skupinu linek**.

**Nedojeté** (zužené v roadmapě): pořadí sloupců na 0370618 (není co k čemu přiložit, dokud
není obousměrný tisk) a písmena ze zóny na 0771901 (nejsou testovací data pro Vsetín).


## 2026-08-25 - `L5` rozřezání široké tabulky (Strih)

Linka se 100+ spoji se dosud zmáčkla do šířky, až z ní nebylo nic vidět. Nově se **rozřeže na
bloky pod sebou**, každý zopakuje popisné sloupce a stránkování se láme na jejich hranicích.

**Co se udělalo** (v pořadí, v jakém to vznikalo):

- rozdělení sloupců spojů do bloků a jejich kreslení pod sebou;
- **dno šířky sloupce se měří z písma** (šířka času i s odsazením) - dokud bylo pevných 3 mm,
  dělení nasypalo do bloku tolik spojů, kolik se jich při nečitelné šířce vešlo;
- pás skupin dnů, řádek platnosti i negativních značek se rozhodují **ze všech spojů linky**,
  ne ze spojů bloku, takže je má každý blok;
- **zlomy mají dvě síly** - hranice bloku (silná) a hranice řádku zastávky (slabá, pro blok vyšší
  než stránka); silná má přednost, i když je výš;
- pokračovací stránka **zopakuje hlavu bloku** a stránkování jí na to vyhradí místo;
- sloupce spojů jsou **ve všech blocích stejně široké**, neúplný blok nechá místo vpravo;
- prázdná legenda nezamíchá stránkováním (#9088);
- volba **„Dorovnat prázdnými sloupci"** to místo umí vyplnit (#8372) - vlastnost prvku,
  ne parametr sestavy (rozhodnutí Jan);
- výška sekce už **nerezervuje místo na posun, ke kterému nedojde**.

**Krok, který odpadl:** vodorovné opakování sekce. Bloky pod sebou proteče stávající svislé
stránkování - doloženo testem, ne úvahou (200 spojů × 25 zastávek = 9 bloků → 3 stránky).

**Odloženo:** ruční zlom na tarifním čísle → `B100` (3 linky z ~93 tisíc); dopravci v záhlaví jen
za viditelné spoje (#9417) → potřebuje na drátě vazbu dopravce↔spoj a zákazníkův případ stojí
na `L7`. **Uzavřeno jako „ne":** slučování přípojných řádků - nikdo si o to neřekl a u přípojů
na různé linky by se ztratil popisek.

⚠️ **Poučení, které stojí za zapamatování:** čtyři z chyb téhle vlny prošly zelenou sadou testů,
protože se model porovnával **sám se sebou** - výška i místa pro řez se braly z téhož čísla.
Chytily je až testy, které porovnávají výpočet **s vykresleným obrázkem**, a mutační kontrola
(vrátit chybu a ověřit, že test spadne). Několik testů prošlo i s vrácenou chybou.

## 2026-08-25 - dva nálezy z 24. 8. dořešené

1. **Odskok ze záložky Parametry** byl skutečná chyba - pravidlo „netaháme uživatele pryč z tabu,
   kde pracuje" mělo ve výčtu jen Pole agendy a Průvodce; Parametry přibyly později a zapomněly se.
2. **„Přepnutí dělení nezmění náhled" chyba nebyla** - na lince 330511 jedou všechny spoje jen
   v pracovní dny, takže vznikne jediná skupina a pás se nemá podle čeho rozdělit. Změřeno:
   0330511 dá při všech třech volbách 9 spojů a 1 skupinu, 0330510 dá 37/1, 38/2 a 41/3.
   Zbylo z toho drobné zlepšení v backlogu (`B101`).

## 2026-08-24 - `L3` vlastní rozložení sloupců spojů

Linkový JŘ uměl jen tři hrubé možnosti dělení spojů. Nově si jde **nadefinovat vlastní sloupce**
(nadpis, dny, značky, které spoj do sloupce pustí nebo z něj vyřadí) - definici drží šablona
u prvku, parametr ji zrcadlí a umí přepnout mezi vzory.

⚠️ **Vlna byla po projití mnohem menší, než jak byla napsaná.** Původní zadání znělo „rozhodnout
celou sadu voleb"; po probrání voleb starého linkového tisku z nich zbyla **jediná** - tahle.
Ostatní jsou hotové, patří do šablony, nebo přijdou s vlnou, která tu schopnost přinese.
Zapsané pravidlo: **parametry se odvozují od starého linkového tisku, ne od vývěsky** - jsou to
samostatné agendy; vývěska je vzor jen pro pojmenování a chování téže věci.

Vedlejší efekt: význam definice sloupců i její wire tvar mají teď **jedno místo** pro obě rodiny
agend (server v `BaseAgenda`, klient v `ColumnLayoutWire`). Liší se až spotřeba - vývěsce je
skupina jeden sloupec, lince pás nad několika sloupci spojů se zdvojením spoje.

**Otevřené:** osa Období se v lince nevyhodnocuje (není pro ni podklad); editor definice zatím
jen přes vzory v parametru.

**Původní zadání vlny:**

| **L3** | **Vlastní rozložení sloupců spojů.** ⚠️ **Vlna je po projití menší, než byla napsaná** (24. 8.): volby starého linkového tisku jsou rozebrané a zbývá z nich **jediná** - dnešní hrubý přepínač dělení (tři možnosti) nahradit vlastní definicí, jako to má vývěska u odjezdů (množina denních značek + značky, které spoj ze sloupce vyřadí + nadpis, s hotovými vzory). ⚠️ **Nesrovnávat sadu parametrů s vývěskou** - jsou to samostatné agendy, každá s vlastní doménou; vývěska je vzor **jen pro pojmenování a chování téže věci** (rozhodnutí Jan 24. 8.). **Tvar řešení = jako u odjezdů** (rozhodnutí Jan 24. 8., viz `TemplateDesignerViewModel` §Rozdělení sloupců): **definici drží šablona** u prvku, **parametr ji zrcadlí a umí přepnout**; „Vlastní rozložení" je jen ukazatel, ručně vybrat nejde. Vzniklo to z chyby, kdy na „jaké sloupce vzniknou" odpovídala dvě místa a parametr tiše přebíjel vlastní sloupce. ⚠️ **Jen pro AD** - MHD má skupiny v datech jako typ provozu i s nadpisem, vlastní sloupce by byly druhá cesta k témuž. ⚠️ Skupiny ovlivňují, kde se tabulka zalomí → dotýká se `L5`. ⚠️ Popisky voleb jsou po vydání **zamčené** (persistuje se zobrazovaný text). | M |

---

## 2026-08-24 - `L4` symbolový font linkového JŘ

Linková tabulka kreslí symboly: **denní značky** (zkřížená kladiva, kroužkové číslice),
**návaznosti** jako trojúhelníky vedle času, **legendu** i **přestup u zastávky**. Vzniklá
**sdílená mapa značek** je jediný zdroj pravdy pro zastávkové i linkové JŘ; vývěskám se chování
nezměnilo (podmínka Jana), první commit vlny byl proto schválně bez změny chování.

⚠️ **Linkový JŘ má opačné výchozí pravidlo než vývěska** - piktogram jen z ověřeného seznamu,
zbytek textem. Bez toho by „pokračuje" (značka `p`) vyšlo jako **kufr**.

**Co rozhodl tištěný vzor** (stará sestava linky 330511, dodal Jan): návaznosti jsou trojúhelníky -
„vyčká" dutý, „navazuje" plný - a tisknou se **vedle času**, ne místo něj. Tím se zároveň opravilo
naše tvrzení, že starý modul čas přepisuje značkou.

**Doměřeno:** pozice `|`, `<`, `m`, `u`, `^`, `C`, `e`, `f`, `g`, `O` v obou fontech. Padly dvě
dosavadní domněnky (`e`/`f`/`g` nejsou metro, ale číslice v rámečku; `O` není letadlo).

**Otevřené:** „pokračuje" `u` (v jádru sady se nevyskytuje), složené značky (piktogram + číslo)
a `B99` (loď, wifi, zásuvka v tiskovém fontu nejsou).

**Původní zadání vlny:**

| **L4** | **Symbolový font - zbytek** (gate R5, dřív `B10`). ✅ Hotový základ 24. 8.: **sdílená mapa značek** (zastávkové JŘ na ni jen delegují, chování beze změny) a **řádek platnosti** kreslený piktogramy, včetně míchání piktogramu a zkratky v jedné buňce (`SymbolRunRenderer`). Zbývá: **doměřit pozice**, které dnes zůstávají textem - projíždí `\|`, jede jinudy `<`, návaznosti `m`/`u`/`^`, a dřív evidované clo `C`, paragrafy `G`/`a`/`b`/`c`, metro `e`/`f`/`g`, letadlo `O`. ⚠️ **Bez doměření je nepřidávat** - pozice se s písmeny číselníku neshodují (past: „pokračuje" je `p`, ale na `p` je **kufr**) a **CZ a SK font se u dvou pozic liší významem**, takže ověření musí proběhnout v obou; postup je v `elementy-architektura.md`. Pak napojit **další místa se značkami** (legenda poznámek, atributy zastávek, buňky časů) a **složené značky** (piktogram + číslo), vědomě odložené z prvního kola. | S–M |

---

## 2026-08-24 - `L2` linkový JŘ: zbytek vlny (zvýraznění, přípoje, dopravce)

Dokončeno: **zvýraznění zvolené zastávky** i jejího tarifního čísla · **přípojný řádek s vlastním
řezem a velikostí** (#8360) · **pruh s dopravcem dorovnaný na starý tisk**. Cestou opraveno
zdvojování přípojných řádků u rozdvojené zastávky. Detail v `changelog.md`.

**Slučování přípojných řádků** přesunuto do `L5` (mění počet řádků, tedy stránkování; nikdo si
o ně neřekl).

⚠️ **Tři nastavení dopravců, kvůli kterým se to zadávalo, jsou slepá ulička:** `Print_Dopr_First`
a `PS_PrintSouhrnDoprLinYes` čte HTML a PostScript tisk, **ne ten Windows engine**, se kterým se
porovnáváme; `SDOP` nemá nastavené žádná firma. Nešlo tedy o dorovnání proti nastavením, ale proti
samotnému skládání textu - a to porovnání hlavičky do 24. 8. vůbec nedělalo.

**Původní zadání vlny:**

| **L2** | **Zbytek vlny - tři body.** ✅ Hotové 24. 8.: zvýraznění zvolené zastávky i tarifního čísla. Zbývá: **přípojný řádek s vlastním řezem a velikostí** (#8360; vlastnost prvku - dnes se kreslí stylem datového řádku, chce to vlastní styl v kaskádě, výšku řádku z něj a pole ve formuláři prvku na obou stranách) · **dorovnání pruhu s dopravcem na starý modul** (`Print_Dopr_First` a souhrn linek k dopravci jsou nastavené vždy u jedné z šesti firem, takže je proti čemu porovnávat; `SDOP` nemá nikdo → `B98`). ⚠️ **Slučování přípojných řádků z `L2` vypadlo** (rozhodnutí Jan 24. 8.) - přesunuto do `L5`, viz tam. | S–M |

---

## 2026-08-24 - `L2b` linkový JŘ: tvar názvu zastávky (poslední datový rozdíl)

Názvy zastávek se skládají jako ve starém tisku: plný tvar ve sloupci, zkrácený ve větách poznámek,
upravený název pro MHD jen na městských linkách, které ho mají zapnuté. Přibyly volby „Tvar názvu
zastávky" a „Použít název pro MHD"; formátování je sdílené s vývěskou, aby se obě rodiny agend
nerozešly. Detail v `changelog.md`.

Zadání vlny bylo postavené na omylu („převzít dvě volby, které má vývěska") - starý linkový tisk
žádnou takovou volbu nemá, název skládá ze tří polí registru a tvar řídí režim státu. Rozdíl proti
nám byl ve **zdroji** jména, ne ve volbě.

Rozšíření porovnávací sady o dvě linky se značkami POS a IDS našlo dvě věci navíc: chybu
„vyčká příjezdu vlaku 0" (opravena) a rozdíl v zařazení spoje do skupin dnů (dluh do `L3`).

**Původní zadání vlny:**

| **L2b** | **Dorovnat poslední datový rozdíl: tvar názvu zastávky.** Starý modul tiskne plný tvar s obcí („Český Krumlov,,aut. nádraží"), my zkrácený („Autobusové nádraží") - týká se to zhruba **poloviny zastávek** na kontrolních linkách a stejně tak **textů návazností**, kde starý modul u městských linek tiskne ještě jiný, místní tvar. Důsledek pro zákazníka: **táž zastávka má na vývěsce a v linkovém JŘ jiné jméno**. Řídí se to dvěma volbami u tisku, které vývěska má a linkový JŘ ne - **převzít je doslova**, ne navrhovat znovu. Chce to i zákazník (#9088 bod 4). Zavírá dluhy `zast` a `pozn/druh3` v registru odchylek. **Součástí je i rozšíření porovnávací sady o linku se značkami POS/IDS**, aby se potvrdil tvar věty, který je dnes opsaný ze zdroje, ale neověřený. | S–M |

---

## 2026-08-21 - `L2` linkový JŘ: matice nastavení (hotová část) + rozhodnutí před ní

Hotovo: **sedm nastavení, která jsou v datech reálně zapnutá** (zóny × km jako jeden čtyřstavový
přepínač, sloupec tarifních čísel, zastávky bez zastavení, barvy spojů, odstranění písmen ze zóny,
atributy nad spojem, ruční pořadí spojů) a **oba nálezy z `L1`** (pořadí sloupců spojů, věty
atributů spoje). Každé nastavení má řádek v deklarativní matici, která ho zapne i vypne;
co z dat tvrdit nejde, je tam pojmenované jako **neověřené**, ne vydávané za pokryté.
Detail v `changelog.md` (zápisy z 19.-21. 8.).

Do backlogu `B98` šlo, co nemá zapnuté žádná ze šesti měřených firem (93 133 linek) - včetně
**celé vrstvy pod linkou**, kvůli níž se `L2` původně rozšiřovala.

### Původní zadání a rozhodnutí (přesunuto z roadmapy)

> ⚖️ **Jak se nakládá s nastavením starého modulu** (rozhodnutí Jan 19. 8., platí pro `L2` i `L3`):
>
> **Co uměl starý modul, musí umět i nový** - pokud to není nesmysl, na který si někdo stěžoval.
> Nastavení se **neignoruje proto, že je to vzhled**: když si ho zákazník u linky nastavil, tisk
> ho musí respektovat, jinak si právem stěžuje, že to bereme jako by tam nebylo.
>
> Rozdělení podle toho, co nastavení dělá:
>
> - **mění hodnoty, řádky nebo texty** → respektuje se při načítání dat (`L2`)
> - **je to volba uživatele u tisku** → parametr sestavy (`L3`)
> - **něco schovává** → **třístavový parametr** „Automaticky (dle linky) / Tisknout / Netisknout",
>   výchozí je první. **Popisek je zavedený, ne nový** - týž tvar má u vývěsek „Rozdělení sloupců
>   (typy provozu)" (`ROZDELENI_SLOUPCU`), včetně toho, že server u neznámé hodnoty spadne zpátky
>   do „dle linky". ⚠️ **Výchozí hodnota je konstanta, ne hodnota z linky** - parametry se ze
>   serveru načítají bez znalosti instance a cachují se per agenda, takže „default podle linky" by
>   znamenal přenačítání při každé změně linky a souboj s uloženou volbou uživatele (třída chyby
>   `B24`: v panelu jedno, tiskne se druhé). Takhle linku rozhodne **server při tisku**, kde ji zná,
>   a v panelu je poctivě vidět, že to řídí linka. Dokud budou u linek nějaká nastavení, tahle
>   možnost tam musí být - abychom je respektovali a zároveň se daly přebít (rozhodnutí Jan 19. 8.).
> - **je to stránkování nebo celá chybějící featura** → `L5` (ruční zlom, ruční sekce),
>   `L6` (turnusy, obousměrný tisk - přidávají zrcadlené sloupce, nejsou to přepínače)
> - **měřicí řetězce a šířka tiskové plochy** → vědomě nikdy, měříme si sami
>
> **V prvku k tomu patří jedno pravidlo: prázdný sloupec se netiskne.** Když linka řekne
> „netisknout", server hodnoty nepošle a sloupec zmizí sám - prvek to dnes umí u sloupce MHD
> a u kilometrů, rozšíří se to na ostatní (a zmizí tím i sloupec zóny u linky bez zón, jako
> ve starém modulu).

> 🔍 **Inventář nastavení je hotový** (20. 8., §13.2) a přinesl čtyři věci, které mění zadání `L2`:
>
> 1. **Vrstvy jsou čtyři, ne tři.** Nastavení je i **pod** linkou: u tarifního čísla („tenhle řádek
>    v knižním JŘ netiskni", zvlášť pro každý směr) a u spoje (do které umělé skupiny patří).
>    Je to nejtvrdší část matice - ubírá to řádky, ale na lince, kde si to nikdo nenastavil, se to
>    neprojeví vůbec, takže to porovnání celé linky nemá jak najít.
> 2. **Přepnout z testu jde dnes jen vrstva „volby u tisku".** Ostatní tři jsou data na serveru.
>    Bez rozhodnutí, jak je v testu přepínat, vznikne „změřili jsme, co bylo v datech".
> 3. **Starý modul u MHD linek většinu voleb ignoroval** - jeho MHD engine čte tři parametry
>    z jedenácti, které má sám v hlavičce vypsané. „Chudý linkový MHD" tedy není jen naše mezera
>    a u MHD se nemá čím řídit parita; sada voleb je tam **nová featura**, ne dorovnání.
> 4. **Tři položky, které jsme vedli jako dluh, starý modul neumí taky.** Pořadí druhů poznámek
>    klient posbírá a nikdy nepošle; zalamování označení turnusu nečte žádný engine; volba
>    „Vyčká/Navazuje jako řádek místo poznámky" je popsaná, ale natvrdo vypnutá. Přestává to být
>    parita a stává se to rozhodnutím, jestli to chceme.
>
> ✅ **Rozhodnutí před `L2` jsou hotová** (Jan 21. 8., detail a varianty v
> [`§13.4`](linkovy-knizni-jr-implementace-analyza.md)); jedno se vědomě odložilo do MHD kola.
>
> 🧭 **Vůdčí linie, která z nich vyšla:** *to, jak je výstup složený, patří do návrháře; z nastavení
> u linky se bere jen to, co mění data.* Proto ani umístění legendy, ani pořadí druhů poznámek nejsou
> parametry sestavy, ale vlastnosti šablony. **Sporné případy řeš směrem do šablony** - na úroveň
> linky jen to, co tam být opravdu musí (starý modul se přepisuje a část nastavení se může přesunout,
> takže čím méně věcí na lince visí, tím méně se toho bude přetahovat).
>
> 📋 **`L2` je projitá položku po položce** - u každého nastavení z inventáře je určeno, jestli je to
> data, šablona, třístav, parametr, nebo jiná vlna, a v jakém pořadí se to má dělat:
> [`§13.5`](linkovy-knizni-jr-implementace-analyza.md).
>
> 📊 **Krok 1 `L2` hotový: změřen výskyt nastavení v datech** (93 133 linek, 6 firem;
> [`§13.6`](linkovy-knizni-jr-implementace-analyza.md)).
>
> ✂️ **Rozsah `L2` se tím zúžil (rozhodnutí Jan 21. 8.): dělá se jen to, co je v datech reálně
> zapnuté** - sedm nastavení. Co není nastavené nikde, jde do backlogu (`B98`): řada těch nastavení
> vznikla v legacy pro jednoho zákazníka a nikdo jiný je nepoužívá, takže dělat je naslepo a bez
> možnosti ověření je práce s nulovou hodnotou. Ruší to i rozhodnutí „skrývání řádků do `L2`" -
> celá vrstva pod linkou má **nula výskytů** na 93 tisících linek.
>
> Co z nich platí pro `L2`:
>
> - **Nastavení se bere tak, jak je u linky nastavené**; oba stavy pokryje různost zákazníků, testovací
>   linku si nestavíme. ⚠️ Povinně změřit, které přepínače se v datech vyskytují, a u zbytku napsat
>   **neověřeno** - ne nechat to vypadat jako pokryté.
> - **Vrstva pod linkou** (skrývání řádků u tarifního čísla, per směr) **patří do `L2`**; ruční
>   přiřazení spojů do skupin zůstává v `L5`.
> - **Co v EDISONu nastavit jde, bereme v potaz, i když to starý tisk ignoroval.** ⚠️ **Pořadí druhů
>   poznámek ale není nastavení linky** - ve starém modulu se ukládá u **sekce šablony**
>   (`TISKJR/Designer.cls`), takže u nás je to **vlastnost prvku legendy**, ne parametr sestavy;
>   chyba legacy byla, že tu hodnotu tisk nikdy nepoužil. Zalamování označení turnusu až s turnusy
>   v `L6`. Naopak „Vyčká/Navazuje jako řádek" **nastavit nejde** (legacy hodnotu natvrdo nuluje),
>   takže není co respektovat a přestává to být náš dluh.
> - **Texty značek a atributů brát z číselníku EDISONu**, ne z opisu v našem kódu - zákazníkova
>   úprava se pak projeví a slovenština funguje bez další práce (#9088).
> - **Vizuální věci patří do šablony, business logika do dat.** Tvar místenkování a skládání čísla
>   linky = data; podtržení u rozdělených zastávek = šablona, a řeší se, až o něj někdo požádá.
> - **Legenda: kam se tiskne, je nastavení šablony u hlavního prvku, ne parametr sestavy** - v návrháři
>   je legenda samostatný prvek, který si uživatel položí, kam chce.
> - **Sada voleb pro městské linky se teď nerozhoduje** - je to rozhodnutí o MHD variantě a jde
>   po AD. Vstup pro to kolo: starý modul u městských linek většinu voleb ignoroval (jeho MHD engine
>   čte tři z jedenácti), takže parita tam nemá čím se řídit a jakákoli sada voleb je nová featura.

| **L2** | **Matice nastavení: vzít všechno, co je v EDISONu nastavené** (podklad §13.2). Ke každému přepínači ze **čtyř** vrstev test, který ho **zapne i vypne** a porovná obojí s legacy; co vědomě neděláme, se **zapíše jako neděláme**, ne zapomene. V téže vlně opravit tři tiché odchylky, které už jsou změřené: **zóny × km řídí jeden čtyřstavový přepínač** (`linIDS`; čteme ho zjednodušeně a u hodnot 0/3 tiskneme zónu, kterou legacy netiskne, km neřídíme vůbec), **barvy spojů neposíláme vůbec** (agenda nemá jediné `FORE_COLOR`, vývěsková jich má 9; legacy je bere ze spoje a linka je umí vypnout) a **volba „jen tarifní čísla, kde spoje staví" vyhazuje řádky** (legacy ji používá na 5 místech, my ji neznáme, takže zákazník dostane delší tabulku než dřív). **Nálezy z `L1`, které tu mají zmizet:** pořadí sloupců spojů se u dvou linek liší od starého tisku (spoje začínající na různých zastávkách - `SeradSpoje` × legacy `mkPoradiSp`; obsah sedí, liší se pořadí) a generická věta atributu spoje má navíc sloveso („spoj **jede** s bezbariérově přístupným vozidlem" místo „spoj s…") - texty všech třinácti atributů jsou v jazykovém číselníku EDISONu, brát je odtud (rozhodnutí 6 v §13.4), ne opisovat jeden. **Odsouhlaseno 19. 8. jako „uděláme":** zvýraznění zvolené zastávky i konkrétního tarifního čísla, volba tisku čísla linky/vlaku v přípojném řádku, a **dorovnání skládání pruhu s dopravcem na starý modul** (dnes na to máme vlastní logiku, kterou proti třem nastavením nikdo neporovnal). Sem patří i **vlastní texty sloupce MHD** (dnes napevno IDP-P/IDP-Z), **přípojný řádek s vlastním řezem a velikostí** (#8360) a **prázdný přípojný řádek se netiskne** (#11300 - má smysl teprve vůči bloku, viz `L5`). | L |

---

> Vše pod touto čarou je uzavřené: ✅ = implementováno, ⛔ = vědomě se dělat nebude.
> Horní část roadmapy obsahuje jen to, co je potřeba dělat.

## 2026-08-19 ✅ - `L1`: linkový JŘ se poprvé měří proti starému modulu

Detail → changelog 2026-08-19 (10). Linkové agendy neměly **ani jeden test** (2 000 řádků) a
porovnání se starým modulem existovalo jen jako pomocný nástroj mimo repo. Bez sítě by další vlny
(nastavení, parametry, symboly, rozřezání) jely naslepo, proto byla `L1` bezpodmínečně první.

**Co je v repu:**

- **Porovnání se starým modulem** - volá legacy engine `mkPLinWin` (ne překladovou vrstvu
  `TISKJR/Data`, ta měří formát, ne data) i naši agendu nad toutéž linkou a srovnává **pole po poli**:
  pořadí spojů, názvy zastávek, km, zóny, sloupec MHD, časy včetně značek, legendu. Čtyři linky tří
  firem, **~9 500 porovnaných hodnot**.
- **Zlatý odtisk pro linkový tvar dat** - vlastní digest (`SPOJE`/`SKUPINY_DNU` místo `ODJEZDY`),
  navíc počítá časové buňky, aby ztráta celého sloupce nezůstala jen v md5.
- **Smoke test obou agend** (AD i MHD) + kontrola, že schéma popisuje tytéž kolekce, jaké data posílají.
- **Typová sada linek** sestavená měřením 48 linek pěti firem, s pojistkou proti vyhnití fixture.

**Rozhodnutí, která platí dál:**

- **Layout se s legacy neporovnává.** Starému modulu se dá záměrně obrovská tisková plocha, takže
  vyrobí jeden blok na směr; porovnává se **obsah dat**. Rozřezání na stránky je naše vrstva (`L5`).
- **Tři druhy rozdílu, jen jeden je pád.** *Odchylka* = chceme se lišit (s důvodem), *dluh* = zatím
  se lišíme omylem (s vlnou, ve které zmizí), *rozdíl* = nikdo o něm nerozhodl → pád. Bez toho by
  síť zabetonovala starý modul a vylepšení z helpdesku by začínala bojem s vlastními testy
  (požadavek Jan 19. 8.).
- **Deklarovaná výjimka nesmí zůstat mrtvá** - když ji na celé sadě nekryje ani jeden rozdíl, test
  ji nahlásí ke smazání. Jinak by pod ní jednou prošla skutečná chyba.

**Co první běh našel:**

- ✅ **Opraveno hned:** navazující spoj se v legendě označoval značkou pro vyčkání (značka se brala
  z dat, kde je u obou druhů stejná; význam ji přitom určuje jednoznačně).
- ⏳ **Dluh do `L2`:** pořadí sloupců spojů se u dvou linek liší od starého tisku. ⚠️ Dřívější
  vysvětlení „jsou to spoje se shodným časem, obsah 1:1" **neplatí** - ověřeno, že ty dva spoje
  vyjíždějí z různých zastávek a jeden na druhý čeká.
- ⏳ **Dluh do `L2`:** generická věta u atributů spoje má proti starému modulu navíc sloveso.
- ⏳ **Dluh do `L3`:** tvar názvu zastávky (v seznamu i v textech návazností).

**Vedlejší nález pro `L4`:** číselník 66 dopravních značek je opsatelný přímo ze zdroje starého
modulu (`GSId^JRapiLinPrtWin`), nemusí vznikat měřením. Dosavadní podklad měl čísla z překladové
vrstvy klienta, která proti enginu neplatí.

---

## 2026-08-19 ✅ - Distribuce systémových šablon kanálem (hotovo a vyzkoušeno naživo)

Detail → changelog 2026-08-19 (5)-(8). Systémové (M-line) šablony byly zabalené v `.exe`, takže nová
šablona znamenala **vydat program**. Nově je doručuje kanál z úložiště na helpdesku (rozhraní
postavil kolega) a přehled šablon je nabídne ke stažení.

**Jak to funguje:** kanál **vyměnil zdroj** dosavadního řetězu - cíl (větev MLINE v zákaznické
databázi), přehled i chování zůstaly. V přehledu se nad seznamem objeví „K dispozici je 1 nová
šablona od M-line" s tlačítkem Stáhnout, jen když je co stáhnout; stažení doplní šablony **celé
firmě**. Bez sítě nebo bez klíče se nabídka neukáže a přehled funguje jako dřív.

**Rozhodnutí, která platí dál:**

- **U zákazníka jen čtení** (úložiště → klient); nahrávat smíme jen my ve vývoji, proto v aplikaci
  není žádná cesta k zápisu a klíč v ní je čtecí.
- **Identitu šablony určuje GUID uvnitř JSONu**, ne identifikátor v úložišti - kdo publikuje, musí
  vnitřní GUID zachovat, jinak u zákazníka vznikne druhá šablona vedle první.
- **Stav „co už máme" cestuje v šabloně** (`channelOrigin`), takže patří databázi firmy, ne počítači.
- **`.exe` zůstává jako záchrana pro první spuštění** (instalace bez sítě, zavřený firewall);
  aktualizace chodí kanálem a seed z programu už nepřepisuje, co doručil kanál.

**Ověřeno naživo (19. 8.):** nahrání (HTTP 201) i aktualizace (HTTP 200, verze 2) · seznam a stažení
vrací obsah bajt v bajt · v databázi sedí `channelOrigin` · nabídka, stažení i aktualizace projdou
z UI · po aktualizaci dorazil opravdu nový obsah (zmizela hláška o chybějícím fontu).

**Provozní postup pro další šablony:** autorovat v návrháři → vyexportovat do repa
(`SystemTemplates/*.json`, tam je verzované a hlídané drift testy) → publikovat do kanálu **z toho
souboru**, ne kopírováním JSONu rukou. Publikační skript (`tools/`) a „seed jen doplní, co chybí"
se vědomě **nedělaly** - zatím se publikuje ručně a je to v pořádku, dokud šablon není víc
(rozhodnutí Jan 19. 8.).

⚠️ **Dvě věci, které je potřeba mít na paměti při publikování:**

- Kanál doručuje **i zákazníkům se starším programem**; šablona uložená v novějším formátu se u nich
  odmítne otevřít (aplikace to hlídá). Publikovat tedy až ve chvíli, kdy je funkce ve vydané verzi.
- **Zákaznických kopií se aktualizace nikdy nedotkne** (kopie má vlastní identifikátor). Oprava
  systémové šablony se k rozkopírovaným verzím nedostane - ty se musí opravit ručně, nebo si je
  uživatel založí znovu z aktualizované systémové.

## 2026-08-19 ⏸️ - AI integrace: sekce uzavřena, pomocník ověřen

Sekce **AI integrace - v pozadí** je z roadmapy pryč (rozhodnutí Jan 19. 8.). Důvod: co z ní bylo
akční, je hotové, a zbytek jsou nápady bez poptávky.

- **AI průvodce ověřen** 19. 8. proklikáním sady otázek (nové featury, práce uvnitř tabulky,
  složené dotazy přes dvě obrazovky, chytáky na „to zatím neumíme"). Odpovídá správně, nevymýšlí
  si cesty a hlásí, co neumíme. Znalostní bázi průvodce před tím dohnal changelog 2026-08-19 (4);
  katalog vlastností se generuje z prvků, ručně psaný úvod hlídá ban list v testech.
- **Evaluační sada** se dělat nebude - hodnotí se proklikáním, ne skriptem (viz i rozhodnutí
  u importu). Otázky, na kterých se to zkoušelo, jsou v historii konverzace, ne v repu.
- ⛔ **MCP server** (nástroje nad ViewModelem) a **aktivní agent, který mění šablonu** - neděláme.
  MCP dává smysl, až bude něco, co má sahat na návrhář zvenčí; aktivní agent jde proti principu
  **copilot, ne autopilot**. Otevřít znovu, až o to někdo požádá.
- Odložené nápady bez poptávky: onboarding průvodce, návrhy sloupců podle schématu, migrace starých
  Caché sestav, template linting v CI.

Kontrakt pro AI zůstává stabilní (JSON šablona + DataSchema), takže rozšíření kdykoli bez breaking
changes - to byl vždycky hlavní argument, proč to nespěchá.

**Provozní kontroly k vydání** (v roadmapě viselo do 19. 8., pak odstraněno jako neaktuální):
ostrá zkouška cesty přes EDISONAI/mlweb **bez lokálního klíče** u první firmy · potvrzení evidence
spotřeby u autora mlwebu · zveřejnění generátoru šablon z popisu (dnes skrytý). Vytáhnout, až se
budou AI funkce reálně vydávat zákazníkovi.

## 2026-08-19 ⏸️ - Import PDF: sekce uzavřena, otevřené body čekají na moduly

Detail → changelog 2026-08-18 (6)-(18) a 2026-08-19 (1)-(3). Sekce **Import PDF - doladění** je
z roadmapy pryč celá (rozhodnutí Jan 19. 8.): pro zastávkové jízdní řády je import hotový
a **zbylé body nečekají na nás, ale na moduly, které zatím neexistují**.

**Co je hotové (18.-19. 8.):**

- `B42a` drobné vady (prázdný sloupec, popisky, varianty jízdních dob, zděděná agenda)
- `B42c` vzhled prvků z předlohy - seznam zastávek, mřížka odjezdů i legenda; otázka pro model
  i zápis odpovědi se **generují z vlastností prvku**, u každé vlastnosti je příznak
  „na tohle se smí ptát" (pravidlo: *nepřítomnost nic nedokazuje*)
- `B42d` nastavení sestavy z předlohy se ukládá tam, odkud ho čte tisk, a hned se projeví
  v ovladačích; ukládá se jen to, co model poznal (past #12843)
- `B42b` hláška „co si dodělej" ⛔ **nebude** - import je rychlý start, ne hotová šablona, takže
  nejde rozlišit, co v ní chybí záměrně; podrobný protokol zůstal jako vývojářská pomůcka za Debugem
- jednotlivé opravy: font s čárkou v názvu rodiny · čáry místo zamčeného rastru · jízdní řád se bez
  AI raději nenaimportuje · roj samotných čísel a záhlaví tabulky nekončí jako volné texty · měření
  výšky řádku a písma záhlaví jako obecná pravidla · prázdný rámeček pro logo z geometrie předlohy

**Co zůstalo otevřené a proč to čeká:**

- **rozpoznání tabulky z mřížky čar** (obecný import) - týká se nejízdněřádových předloh; faktury
  ani podobné sestavy zatím neexistují, takže by se návrh dělal bez předlohy. ⚠️ Zároveň je to
  jediná dnešní cena za zrušení slévání čar do rastru: u složité nejízdněřádové předlohy vznikne
  pár desítek samostatných čar. Návrat zpátky je jeden řádek (`GraphicsMergeMode` v dialogu importu).
- **ladění na širší sadě jízdních řádů** (jiná města, SK, netypické legendy) - není to vývoj, ale
  zkoušení; z něj vyplyne doladění příznaků u vlastností
- **linkové a knižní JŘ se neimportují** - režim rozpoznávání se zapíná jen u agend zastávkového JŘ

**Kdy to znovu otevřít:** až vznikne linkový/knižní JŘ nebo faktury. Import je na to připravený -
nový prvek se do něj zapojí tím, že se u jeho vlastností označí, které jdou z obrázku poznat;
do samotného importu se nesahá.

## 2026-08-19 ✅ - `B42d`: nastavení sestavy z předlohy se uplatní

Detail → changelog 2026-08-19 (2). Co model z předlohy pozná o nastavení tisku (šipky směru, rozsah
hodin), se zapisovalo vedle místa, ze kterého to čte tisk - vývěska bez šipek se stejně vytiskla se
šipkami. Nově:

- hodnoty jdou tam, odkud je čte tisk, a otevření importované šablony je aplikuje i do ovladačů
  (jinak by se tisklo jiné nastavení, než jaké je vidět)
- ukládá se **jen to, co model opravdu poznal** - hodnoty v šabloně mají přednost před nastavením
  uživatele pro agendu, takže celá sada by ho tiše přebila (past z #12843)
- otázka na volby se skládá **z popisu agendy** (popisek, typ, nabídka, podmínka), ne ze tří
  napevno vypsaných voleb psaných pro MHD - platí tedy i pro AD s patnácti

Zamčeno testy na pravidlo „ukládej jen poznané volby".

## 2026-08-18 ✅ - `B42a`: drobné vady importu z předlohy

Detail → changelog 2026-08-18 (6). První dávka z průchodu importem PDF - jen to, co je jasná vada
a nepotřebuje rozhodnutí:

- **prázdný sloupec „tarifní číslo"** v seznamu zastávek - import ho zakládal pod klíčem, který
  agendy nevydávají (nesou jen interní klíč, ne reálné číslo), takže sloupec zabíral šířku a při
  tisku zůstal prázdný → mapuje se na **pořadí zastávky**
- **popisky sloupců mimo dohodnuté názvosloví** („min.", „Zastávky", „Nást.") → berou se z výchozích
  sloupců prvku, takže se rozejít nemůžou
- **chybějící sloupec variant jízdních dob** - ručně vložený prvek ho má, importovaný ne
- **obecný (nejízdněřádový) import zahazoval agendu** otevřené šablony → šablona ji zdědí
- z otázky na AI zmizel dotaz na šipku směru (odpověď neměl kdo přečíst - šipku řídí nastavení sestavy)

Import dosud neměl ani jeden test, vzniklo prvních devět (sloupce a popisky). Zbytek `B42`
(`B42b` protokol importu, `B42c` nastavení sestavy a vzhled prvků) zůstává v roadmapě.

## 2026-08-17 ✅ - `B76`: pevná tabulka se dá naformátovat a slučovat (4 kroky)

Detail → changelog 2026-08-17 (3), (4), (5), (6) a (9). Cíl prvku **není faktura, ale náhrada
„poskládám to z textových polí"** - hlavička sestavy jako jeden prvek, se kterým se hýbe vcelku.

**Co blok přinesl:**

1. **Formátování buňky** - do té doby šla tabulka nastavit jen jako celek, takže nefungoval ani
   způsob práce, na kterém prvek stojí („záhlaví si udělám z prvního řádku tím, že ho ztuční").
   Formátovací lišta uvnitř tabulky patří buňce, Ctrl+B/I/U funguje i během psaní.
2. **Formulář vybrané buňky** (`TableCellEdit.cls`) - stejná stavba jako u prvků, **jedno schéma
   pro panel i pro okno** pod Alt+Enter.
3. **Výběr oblasti tažením** (Shift, Ctrl+A) a na něm hromadné formátování, mazání a schránka
   čitelná Excelem.
4. **Sloučení buněk vodorovně** z místní nabídky.

⚠️ **Poučení, která stála nejvíc:**

- **Rozsah editace musí být vidět.** Panel měnil celou tabulku, lišta nad ním jednu buňku a nešlo
  je rozlišit - dokud to Jan nezkusil, vypadalo to hotově.
- **O tom, komu patří klávesy, nesmí rozhodovat fokus plátna** - vezme mu ho každé tlačítko lišty,
  takže buňka zůstala zvýrazněná, ale psaní padalo do prázdna („poprvé to jde, po odkliknutí ne").
- **Formulář buňky musí dostat hodnoty z kaskády**, jinak by potvrzení beze změny zapeklo zděděné
  hodnoty a odpojilo buňku od nastavení tabulky. Vymazané pole u zděditelné vlastnosti znamená
  „nenastaveno" - opraveno rovnou pro všechny formuláře, dřív to platilo jen pro barvy.
- **Výběr celého řádku a sloupce se zkusil a zrušil** (zkratky i položky nabídky): tažení a Shift
  pokrývají totéž a excelové Shift/Ctrl+mezerník se uživatel nemá kde naučit. Klik u okraje
  tabulky pro to nejde - tam sedí úchyty na změnu velikosti prvku.

**Vědomě odložené:** sloučení přes řádky (míchá se se spanováním výšek, střídáním řádků
a podmíněnou viditelností) a připravené bloky agend, které pohltí `B66` (vlastní bloky prvků).

**Pojistky:** `FixedTableCellFormattingTests`, `FixedTableCellRangeTests`, `FixedTableMergeTests`
(dohromady 44 kontrol) - kaskáda, vratnost podle souřadnic, nezapékání zděděných hodnot, geometrie
výběru a sloučení. Formulář buňky je vědomě mimo driftové testy schémat (jeho výchozí hodnoty se
nikdy nezobrazí) a nahrazuje je vlastní test pokrytí kaskádou.

## 2026-08-18 ✅ - `B97`: chybějící font už není tichý

Detail → changelog 2026-08-18 (2). V programu jsou zabalené jen symbolové fonty, všechno ostatní
písmo se bere ze systému a nabídka jmenuje fonty **toho počítače, na kterém se navrhuje**. Při
tisku se chybějící rodina tiše nahradí (embedded → OS → **Arial** → default), takže šablona sdílená
přes databázi mohla u dvou lidí vypadat jinak, aniž by to kdokoli poznal.

**Neodvoditelné - proč se hledá průchodem serializovanou šablonou:** font sedí na spoustě míst
(styl prvku, sloupce, buňky, výchozí styl dokumentu, zóny JŘ prvků). Ruční výčet by při přidání
další stylové vrstvy tiše zestárnul - přesně jak zestaraly výčty, které řešil `B94`. Průchod JSONem
chytí každé pole `fontFamily`, které v šabloně je nebo kdy bude, včetně `CustomProperties`.

**Neodvoditelné - kde se to spouští:** jediný hák je setter `DesignerState.CurrentTemplate`
(nově volá `TemplateSwapped`). Je to jediné místo, kde se dá zachytit „teď je v návrháři něco
jiného"; volajících je pět a šestý by se zapomněl. `InvalidateAll()` se na to použít nedá - volá
ho jen jedna cesta (`LoadGeneratedTemplate`), ne otevření z DB ani import.

**Psaní vlastního názvu fontu se záměrně nehlídá průběžně:** nabídka jmenuje jen dostupné rodiny
a záměnu je vidět hned na plátně; před výstupem to navíc kontroluje tiskový dialog znovu, protože
tisknout může někdo, kdo šablonu jen otevřel.

## 2026-08-18 ⛔ - tlačítko „Zkontrolovat přetečení" se dělat nebude

**Rozhodnutí Jan 18. 8.** Původní přání (samostatné tlačítko v tiskovém dialogu, které natáhne data
a spočítá přetečení bez generování PDF) je **fakticky splněné jinou cestou**: tiskový dialog kontrolu
přes všechny stránky spouští sám a napíše „⚠ PDF uloženo, ale 5 z 54 stránek přetéká". Liší se jen
pořadí - výsledek dostaneš spolu s PDF, ne před ním. Přetečení navíc značí náhled červeným rámečkem.

## 2026-08-18 ⛔ - `B65`: kalendářový výběr období se dělat nebude

**Rozhodnutí Jan 18. 8.: nice to have, vyhozeno.** Rozsahy období u vlastního sloupce se píšou ručně
(`14.2.-18.2., 1.7.-31.8.`), ale **tvar hlídá validace** (`JePlatneObdobi`) - špatný výstup z toho
vzniknout nemůže, jde čistě o pohodlí při psaní, a jen v úzkém případě (dny školního vyučování).

**Kdyby se to někdy otevíralo znovu:** stavební kámen existuje - EDInputForm má typ pole `DATE`,
který se vykreslí jako skutečný kalendář (`DatePicker`, wire `yyyy-MM-dd`, vlastní validace) a TD ho
už používá u parametrů sestavy typu datum. Chybí jen krok od **jednoho data** k **seznamu rozsahů**.

## 2026-08-18 ⛔ - `B93`: parita tabulkových prvků se dodělávat NEBUDE

**Rozhodnutí Jan 18. 8.: není to fakticky díra, nedělá se.** Zapsáno sem, aby to příští revize
nenašla znovu jako nález - tohle už jednou posouzené bylo.

Co se měřilo (stav 18. 8., po dokončení podmínky v pevné tabulce):

- **střídání řádků** nemá **seznam zastávek** ani **linková tabulka**; má ho tabulka z dat, pevná
  tabulka i mřížka odjezdů. Doplnění je malé a opsatelné (vlastnost + řádek renderu + pole formuláře
  + deskriptor + `Clone`) a bylo by bezpečné (výchozí „nenastaveno" = beze změny výstupu, odtisky
  zůstávají zelené) - **ale nikdo si o to neřekl.**
- **podmínka viditelnosti řádku** v pevné tabulce doplněna souběžně, mimo tento blok.
- **zvýraznění řádku podle dat** v pevné tabulce **nedává smysl** a není to mezera: zvýraznění
  vyhodnocuje výraz nad daty konkrétního řádku (`SourceRow`), jenže řádky pevné tabulky napsal
  uživatel do šablony a žádná řádková data nemají. Viditelnost tam smysl dává, protože se vyhodnotí
  nad celkovými údaji sestavy. Kdyby to někdo chtěl, je to jiná featura (podmíněné formátování
  buňky), ne tahle.

⚠️ **Až se k tomu někdo vrátí:** u seznamu zastávek musí střídání ležet **pod** zvýrazněním aktuální
zastávky a ztišením projetých, jinak aktuální zastávka na sudém řádku o zvýraznění přijde.

## 2026-08-18 ✅ - `B96`: datum platnosti u MHD s tečkami, ne s lomítky

Detail → changelog 2026-08-18. Zastávková vývěska MHD tiskla `01/11/2018`, AD vedle ní
`11.12.2022`. Sjednoceno na český tvar; formátování je nově v jednom sdíleném helperu
`BaseAgenda.FormatDatum` (dva konzumenti → extrakce oprávněná).

**Neodvoditelné - past, kvůli které to vzniklo:** `$zd(h, 4)` vypadá jako evropský formát, ale
dává `dd/mm/yyyy` **s lomítky**. Helper skládá datum z ISO tvaru (`$zd(h, 3)`) po kusech, aby
výstup nezávisel na tom, jaký oddělovač který `$ZDATE` formát zvolí. Varování je u helperu.

**Lokalizace vědomě ne** (rozhodnutí Jan 2026-08-18): tvar je napevno český, protože všichni
zákazníci tisknou česky - lokalizační vrstva by dnes byla bez konzumenta. Až vznikne první
jinojazyčná sestava, nahradí se tělo helperu a volání se nemění.

**Druhá polovina bloku padla s tím:** vzorová data pro náhled už tečky měla, takže náhled
ukazoval jiný tvar než tisk. Teď sedí obojí.

⚠️ **Přebaselinovány tři zlaté odtisky MHD** (`ZastavkoveJrMhdGoldTest`) - doložené, ne odhadnuté:
výstup linky 1003 sejmutý před a po změně se liší **160 znaky a všechny do jednoho jsou
lomítko → tečka**; počty (stránky, zastávky, odjezdy, poznámky) zůstaly ve všech třech odtiscích
shodné do jednoho. Caché sada 110/110.

## 2026-08-17 ✅ - `B94`: reference tabulkových prvků srovnaná s kódem

Blok mluvil o dvou zastaralých tvrzeních v `elementy-architektura.md`; **při čtení proti kódu jich
bylo pět**, všechna v části 1:

1. **Hierarchie prvků měla o dvě vrstvy méně** - chyběl `StopScheduleElementBase` (mezivrstva pro
   zastávkový datový tvar) i `LineTableElement`. Doplněno včetně toho, **proč** ta mezivrstva je:
   `LineTableElement` chce styly a auto-scale, ale zastávkové DataSource klíče a `Build*` helpery ne.
2. **DataSource klíče a `Build*` helpery** byly přiřčené `ScheduleElementBase` - patří na
   `StopScheduleElementBase`. Sekce přejmenovaná.
3. **Ploché vlastnosti** (`FontFamily`, `FontSize`, `GridColor`, `HeaderBackgroundColor`…) na base
   už neexistují, nahradila je kaskáda stylů. `BuildOdjezdyByGroup` se jmenuje `BuildOdjezdyBySkupina`.
4. **§1 tabulka sdíleného jádra** neznala `RowPlanRenderer` (svislá smyčka) a tvrdila, že
   `CellRenderer.DrawCell` volají všechny čtyři přímo - dnes jde přes `ColumnRenderer`, přímo už jen
   pivot. `TableLayout.ResolveCascade` mělo o konzumenta méně.
5. **§5 vedl jako kandidáta na extrakci hotovou věc** - „R1a render-loop orchestrátor" je
   `RowPlanRenderer`. Přesunuto do „hotové, ať se to nenavrhuje znovu", protože právě na plánu řádků
   stojí `B13` (řezání širokých tabulek).

**Neodvoditelné - proč to zestaralo:** dokument nesl **výčty vlastností a signatur**, tedy přesně to,
co `CLAUDE.md` do dokumentace zakazuje. Výčty u seznamu zastávek a mřížky odjezdů jsou nahrazené
popisem doménových odlišností + odkazem na třídu a formulář; v hlavičce je varování, ať se výčty
nezavádějí zpátky. Bez toho by se to rozešlo znovu a nikdo by si toho nevšiml (`.md` nehlídá test).

## 2026-08-17 ✅ - `B92`: výchozí linková šablona se distribuuje jako systémová

Detail → changelog 2026-08-17 (8). Šablona ležela v `templates/` (vývojová složka), takže se
nezabalila do `.exe` a nenalila do MLINE - uživatel by ji musel naimportovat ze souboru, o kterém
neví. Přesunutá do `SystemTemplates/`; kód se měnit nemusel, seeder i `.csproj` jdou přes wildcard.

**Opravené při přesunu** (vidí uživatel v nabídce): jmenovala se „Knižní JŘ (AD)", ačkoli se agenda
jmenuje Linkový JŘ - pojem „knižní" se přejmenoval už s `MigrationKnizniToLinkovy` a na šablonu se
zapomnělo. V názvu byla **dlouhá pomlčka** a popis byl prázdný.

**Neodvoditelné:** šablona se zabalí a nalije i s `Released = 0` agendou. Je to záměr - picker
filtruje podle vydaných agend, takže ji uživatel neuvidí, ale kanál se nedodělává až s vydáním.

**Pojistka:** `SystemTemplateSeedTests` (17 kontrol). Jádro je „soubor ve složce ještě neznamená
soubor v `.exe`" - kontroluje se seznam zabalených resource, k tomu metadata (prázdný nebo
překlepnutý `agendaCode` = šablona, kterou nikdo nenajde; shodné `Id` = jedna přepíše druhou,
seeder je idempotentní přes Id) a že se šablona zmigruje na aktuální verzi. `agendaCode` se
porovnává s kódy, které reálně deklarují `Agendy/*.cls`.

## 2026-08-17 ✅ - `B91`+`B95`: linkový JŘ mluví stejným slovníkem jako vývěska

Detail → changelog 2026-08-17 (3). Obě rodiny agend posílaly stejně pojmenované kolekce, ale
řádky uvnitř měly jiný tvar (`POPIS` × `NAZEV_ZASTAVKY`, `ZONA` × `ZONY`, `PLATNOST_OD` ×
`DATUM_PLATNOSTI_OD`). Srovnáno na zastávkový tvar - měnila se výhradně linková strana.

**Dvě věci, které nová pojistka principiálně nechytí a našly se čtením kódu:**

1. **Číslo linky u MHD** - vývěska dávala do `CISLO_LINKY` licenční číslo, linkový JŘ veřejné.
   Táž šablona tedy tiskla u jedné linky dvě různá čísla. Srovnáno; veřejné přibylo jako
   `LOKALNI_OZNACENI_LINKY`, opět pod týmž názvem jako na vývěsce.
2. **Datum platnosti u MHD** - linkový posílal syrový tvar z databáze (`2018-11-01`), vývěska
   převedený. Srovnáno na tentýž převod.

**Neodvoditelné:** spojovací klíče (`ID` skupiny, `SKUPINA` u spoje) zůstaly ve wire, ale zmizely
z nabídky polí - vývěska je taky nenabízí a v ploché tabulce s nimi uživatel nic neudělá.

⚠️ **`DELENI_SLOUPCU` × `ROZDELENI_SLOUPCU` se NEsjednocuje** - revize to zapsala jako dva názvy
téhož, ale jsou to dvě různá nastavení s jinými volbami. U nich se ukládá zobrazený text, takže
sloučení pod jeden název by při přepnutí agendy tiše shodilo uživateli nastavení na výchozí.

## 2026-08-17 ✅ - `B90`: písmena variant u odjezdů přestala být volba

Detail → changelog 2026-08-17 (2). Vypnutý stav umožňoval vývěsku, ze které nešlo poznat, který
časový sloupec pro odjezd platí (nadpisy `T1`/`T2`, u minut nic). Parametr zrušen, volbou zůstala
jen „První varianta bez písmene".

**Neodvoditelné:** u zbylé volby se musela odstranit i podmínka viditelnosti - odkazovala na
zrušený parametr, takže by z UI vypadla i ona. Ověřeno voláním nasazené agendy, ne čtením zdroje.

⚠️ **Zlaté odtisky do teď pinovaly vypnutou větev** - server čte nenastavený parametr jako `false`
a gold posílá jen číslo linky. Přebaselinováno; počty shodné do jednoho, liší se jen md5.

## 2026-08-17 ✅ - `B89`: vlastní sloupce proklikané, revize `B39` uzavřená

Detail → changelog 2026-08-17. Sedm bodů sady (editor, vzor, sloupec „jen Ne", „jen pracovní dny",
Pá+So+Ne, režim „Automaticky", období se svátkem na neděli) projeté Janem. Nejcennější důkaz:
na lince `0330530` mají spoje **T239**/**T245** (platnost `6+`) ve sloupci Pá+So+Ne značky **⑥⑦**,
dřív jen ⑥ - cestující se nedozvěděl o nedělním provozu. Minut se sedmičkou jsou čtyři, tedy tatáž
čtveřice, kterou vrátí sloupec „jen Ne".

**Doladěno při proklikávání:** „Ne" a „Neděle a svátky (+)" se **vylučují** (rozhodnutí Jan 17. 8.).
Obhajoba dvojice stála na tom, že vzor posílá `7+`; od `B82` posílá samotné `+` a server si „7"
doplní sám, takže dvojice nepřidávala nic - doloženo během, kde sloupce `7`, `+` i `7+` daly
identický výstup. Starší uložený sloupec s `7+` se načte jako `+`.

⚠️ **Nález mimo zadání:** testovací vzor v .NET testech nesl `12345`/`7+`, zatímco server od `B82`
posílá `X`/`+` - testy potvrzovaly neexistující chování. Srovnáno, u fixture je varování; na server
se z .NET testů nedosáhne, takže víc než poznámka to hlídat nemůže.

## 2026-08-14 ✅ - `B87`+`B88`: rozvržení sloupců vývěsky má jeden zdroj pravdy

Detail → changelog 2026-08-14 (4) a (5). Vzniklo z Janova pozorování, že přepnutí parametru
„Rozdělení sloupců" náhledem nehne. Byly to **dvě nezávislé příčiny**:

`B87` - server-side parametr přenačítala data jen jedna ze tří cest. Chybělo to hlavně při
**otevření šablony**: data se tahají při obnovení instance, tedy dřív, než se aplikují uložené
hodnoty parametrů. Ovladač pak ukazoval uloženou volbu, náhled patřil výchozí - a „změnit" volbu
na tu zobrazenou nešlo, vyhodnotilo se to jako beze změny.

`B88` - parametr a vlastní sloupce prvku odpovídaly na tutéž otázku a sloupce parametr tiše
přebíjely. Stav teď drží **jen šablona**, parametr je její pohled i ovladač; přibyla volba
„Vlastní rozložení", na kterou se překlopí sám.

**Neodvoditelné rozhodnutí (Jan 14. 8.):** parametr se odvozuje **z obsahu** sloupců (shoda se
vzorem → název vzoru), ne z pouhé existence vlastních sloupců. Bez toho by hlásil „Vlastní
rozložení" na každé nové AD šabloně - nový prvek odjezdů dostává sloupce **předvyplněné**
(rozhodnutí 10. 8. „nové jedou z návrháře, linka je backup"). Opřít se o vzory jde proto, že jsou
1:1 náhrada pevných rozvržení (živý paritní test přes celý výstup).

⚠️ **Schování sloupce ani přejmenování nadpisu v tabu „Sloupce" parametr nepřeklápí** - to je
vzhled, ne existence sloupců. Kdyby překlápělo, muselo by výběrem rozvržení schované sloupce zase
odkrýt a nadpisy zahodit, tedy mazat práci při nesouvisející akci. Přejmenování nadpisu **uvnitř
vlastních sloupců** naopak shodu se vzorem ruší (nadpis jde na vývěsku).

## 2026-08-14 ✅ - `B85`: nové povinné pole nezablokuje formulář staršímu programu

Detail → changelog 2026-08-14 (3). Nález z revize `B39`, ale ne chyba `B39`: schéma formuláře jde
na server hned, program až s vydáním, takže starší build dostane pole, ke kterému nemá vlastnost -
a `required` u něj znamenalo, že formulář nešel potvrdit vůbec. Projevilo se to u „Rozestupu
odjezdů" (kolega měl program o den starší, než je to pole).

**Neodvoditelné:** povinnost se ruší jen klientsky a jen u pole **bez vlastnosti na cíli** - jeho
hodnotu stejně zahodí `ExtractCommands`, takže povinnost neměla co vynutit. Serverová `Validate`
platí dál. Druhá varianta (nedělat nová pole `required`) by znamenala vzdát se `required` jako
způsobu, jak u výběrového pole potlačit prázdný řádek - ten idiom používají skoro všechny výčty.

## 2026-08-14 ✅ - `B84`: zavřeno bez práce, značky `+` a `X` z MHD vyjít nemůžou

Bod z revize `B39` předpokládal, že se u minuty vytiskne značka `+` / `X`, ke které chybí
vysvětlivka. Ověřeno (zdroj kalendáře i běh nad daty `CB_DP`): `MakeRozpisTplProTypProvozuID`
vrací rozpis **jen pro dny 1-7** - „pracovní dny" rozepíše na 1-5, „neděle a svátky" na 7. Znaky
z vyhlášky tedy MHD neprodukuje a doplňovat pro ně vysvětlivku nemá co obsluhovat.

⚠️ **Značky z vyhlášky jsou téma zastávkového JŘ AD, ne MHD** (upřesnění Jan 14. 8.): v MHD se
platnost bere z **typu provozu**, ne z ručního zadávání dnů, takže se tam kolem `B39` nic nemění.
Ostatní body revize (`B81`, `B82`) se MHD netýkají.

## 2026-08-14 ✅ - `B83`: vysvětlivky dnů na vývěsce AD čtou zákaznický číselník

Detail → changelog 2026-08-14 (1). Nález z revize `B39`: vývěska tiskla texty denních značek
napevno, takže vlastní znění zákazníka ignorovala - přestože starý modul i náš **linkový** JŘ je
z číselníku `^JRSET("NCOME")` berou. Dvě naše sestavy tedy u téže značky tiskly jiný text.

Vestavěná znění zůstala jako záloha a znění `+` se srovnalo s vyhláškou 122/2014
(„jede v neděli a ve státem uznané svátky"). **Neodvoditelné rozhodnutí:** záznam číselníku se
vybírá podle **jazyka sestavy**, ne podle toho, kterou variantu má firma vyplněnou - firmy mají
běžně obě (ověřeno na `CB_CSADAD`), takže legacy detekce „vezmi vyplněnou" by slovenské sestavě
podstrčila české texty. Kvůli tomu se `NactiTextyZnacek` musí volat **až po** `NactiJazyk`.

Ověřeno voláním nad reálnými daty (nový test čte oba jazykové záznamy firmy). Zlaté odtisky se
nehnuly - na kontrolních linkách se `+` do legendy netiskne, protože ji říká už nadpis sloupce.

## 2026-08-14 ✅ - `B82`: dny sloupce v abecedě vyhlášky včetně „pracovních dnů"

Detail → changelog 2026-08-14 (4). Tím je uzavřená celá revize vlastních sloupců od kolegy (13. 8.).

`X` (pracovní dny) nešlo zadat vůbec a týž zápis `12345` znamenal na dvou místech dvě různé věci -
při zařazení spoje kalendářní Po-Pá, na ose Období „pracovní dny". Nově je `X` samostatná volba
v editoru, `12345` znamená kalendářní Po-Pá **včetně svátků**, které na ně padnou, a `X` svátky
nezahrnuje nikdy. Zakázané kombinace z vyhlášky (`X` s Po-Pá, „Svátek" s „Ne") nejdou zaškrtnout -
protikus se zašedne, takže zákaz vyplývá z ovládání, ne z hlášky po potvrzení (rozhodnutí Jan 14. 8.).

⚠️ **Vzory sloupců posílají u „Pracovních dnů" `X`**, takže se změnily zlaté odtisky - **jen md5,
počty shodné**. Ověřeno izolovaně: s vzory vrácenými na `12345` projdou odtisky beze změny, takže
rozdíl nepochází odjinud než z pole `PRESETY_SLOUPCU`. **Naše testovací šablony v DB je potřeba
přeuložit**; zákaznických se to netýká (vlastní sloupce se do vydané verze nikdy nedostaly).

## 2026-08-14 ✅ - `B81`: vlastní sloupec „Neděle" vracel prázdno

Detail → changelog 2026-08-14 (3). Spoj s platností `+` („jede v neděli **a** ve státem uznané
svátky") nespadl do vlastního sloupce s dny `7`, protože se dělal průnik holých znaků - zákazníkovi
tak vyšel sloupec „Neděle" prázdný, přestože nedělní spoje na lince byly. Nově z `+` plyne i neděle;
značka se u minut netiskne, protože spoj sloupec pokrývá celý (potvrdil Jiří Bžoch 14. 8. - značka
patří do opačného případu, kdy spoj sloupec **ne**pokrývá).

Druhá polovina byla na ose Období: svátek patřil jen do sloupce, který vyjmenoval `+`, takže
velikonoční neděle nepatřila do sloupce „Ne". ⚠️ **U dnů 1-6 to vědomě neplatí** - „Pracovní dny"
nesmí tvrdit, že pokrývají 1. květen, a rozlišit je od samostatného „pondělí" umí teprve `X`
z vyhlášky (`B82`).

⚠️ **Vedlejší dopad na značky:** spoj `6+` dostane ve vlastním sloupci Pá-Ne značky `67` místo
dosavadní `6` - dřív se neděle zamlčela. Standardních rozvržení (`12345` / `6` / `7+` / `67+`) se to
netýká.

**Doloženo mutací:** vypnutí odvození neděle shodí end-to-end test hláškou, která je doslova
zákazníkovým příznakem („vlastní sloupec Neděle nevrátil ani jeden odjezd"). Linku vybrala sonda
`AdSloupekProbe.NajdiPlusSpoje` (4 840 spojů s `+` na 3 000 linkách CB_CSADAD) - **sondu nemazat**,
u `B82` se bude hodit.

## 2026-08-13 ✅ - `B78`: kontrola uvozovek chytá i druhou půlku té třídy chyb

Detail → changelog 2026-08-13 (7). Statická kontrola `.cls` hlásila jen řádek, který **končí**
uprostřed textu (lichý počet uvozovek). Uvozovka uvnitř textu, za kterou pokračuje další text
v uvozovkách, je párná a prošla - a přesně ten tvar 13. 8. způsobil Caché test padající **bez
hlášky** (na serveru zůstala nezkompilovaná třída), zatímco `cache-api.md` tvrdilo, že to test hlídá.

⚠️ **Kontrola platí jen uvnitř závorek** a rozšířit ji nejde: mimo ně je „text, mezera, slovo"
legitimní podmíněný příkaz (`s:group'="" param.group = group`) a v repu jich jsou desítky. V praxi
chyba vzniká v argumentu (hlášky testů, helpTexty), kde závorky jsou. Mez je zapsaná v testu jako
`[InlineData]` s očekáváním „projde", ať je vidět, a v `cache-api.md`.

## 2026-08-13 ✅ - `B67`: jemnější zobrazení variant jízdních dob (TRANSDEV #12843)

Detail → changelog 2026-08-12 (10) a 2026-08-13 (1)-(6). Původně `TR7`, podaný podruhé jako #12843
bod 4. Hotové: nestabilní číslování · duplicitní sloupce · volba trasy pro jediný sloupec · oddělení
sloupce „Čas jízdy" od variant · tři stavy `CAS_JIZDY` · jedno pravidlo výběru průběhu pro obě rodiny
sloupců · značka u minuty vedle rozdílové poznámky · dvě volby řazení podle délky jízdy.

**Ze Šimkova odstavce o jízdních dobách je hotové všechno kromě ruční zaškrtávací tabulky variant** -
ta je **odložená po dohodě s kolegou** (13. 8.): není to jednoduchá změna, dá se mu nejdřív k
vyzkoušení a domluví se potom. Evidence u tiketu, ne tady.

⚠️ **Značky variant NEJSOU parita se starým modulem** a nesmí se tak tvrdit. Legacy je bere
z nastavení u linky (`^JRL(sku,lin,"PATCH","mhd","par",2)`, `JRapiLinPrtMhd1.mac:26` a `:297`),
které **nemá vyplněné ani jedna z 325 prověřených linek** - starý modul tedy značky netiskne vůbec.
Dřívější tvrzení roadmapy „legacy má v kódu default UVWXYZ" bylo mylné, `UVWXYZ` je jen komentář.
Naše řada jede od „A" stejně jako písmena rozdílových poznámek, takže se na minutě můžou potkat dvě
stejná písmena v jiném významu - vědomý kompromis (rozhodnutí Jan 13. 8.), řeší se, až na to někdo
narazí. Ta volba se možná časem přestěhuje z nastavení linky do návrháře.

**Zázemí pro další změny variant** (nemazat, bude potřeba): typová sada `ZastavkoveJrAdSadaTest.Sada`
(deset linek ze čtyř firem, u každé zapsané typy, které pokrývá; `Jadro` = pět linek pro drahé
kontroly) a sondy v `AdSloupekProbe` - `NajdiVariantniPripady`, `ProfilyLinek`, `PripadyLinky`
(linka → konkrétní zastávka k proklikání), `ViceStanovistLinky` (kde se zastávka rozpadá na řádky),
`ZnakySloupcu` (kdo má vyplněné znaky sloupců u linky). ⚠️ Caché sada trvá ~1 min 45 s; zkrátit jde
**sloučením průchodů** do jednoho testu, ne zúžením sady.

⚠️ **Poučení z bran:** test na duplicitní sloupce byl napoprvé slepý - porovnával je přes celou
stránku včetně projetých zastávek, kde se varianty legitimně liší, takže duplicitu v úseku za
vývěskou nemohl najít a mutace ho neshodila. Absenci něčeho je potřeba měřit na tom výseku, kde to
má vzniknout.

**Na vyžádání** (výpočet i data budou připravené, není to nová featura): **2. až 4. sloupec Času**
(legacy je v automatickém režimu má; dopad na lince, kde tentýž sled zastávek nese dva časové
profily - Nitra `0000002` směr Zpět, trasy 2/4/6 končí na 35 min, trasa 3 s pobytem `19/5` na 40) ·
**čtení ručně zadaných minut** u linek s vypnutým přepínačem (`^JRL(sku,lin,"J",poradi)` segment 8,
`Tam:Zpět`, kumulativně od začátku směru; Nitra je udržuje, české firmy mají prázdné → starý modul
jim tiskne prázdno, takže náš spočítaný sloupec je přínos, ne odchylka).

## 2026-08-13 ✅ - `B80`: varianty tras jsou položka seznamu sloupců

Detail → changelog 2026-08-13 (3). Podnět Jan při přípravě proklikání `B67`: zapínání variant
zaškrtávátkem v panelu byl atyp. Byl to pozůstatek doby, kdy varianty sloupec „Čas jízdy"
**nahrazovaly**; od 13. 8. stojí vedle sebe, takže šlo o nastavení sloupců schované mimo sloupce.
Jako položka seznamu má poprvé nastavitelnou šířku (dosud zadrátovaných 6 mm) a vlastní styl místo
parazitního dědění ze sloupce „Čas".

⚠️ **Vedlejší past, kterou to ruší:** varianty se rozvíjely ZE sloupce „Čas", takže jeho skrytí je
tiše vyplo - a s nimi i rozpad zastávky na řádky podle stanovišť. Do 12. 8. se to nedalo potkat
(vylučovaly se), po jejich oddělení to byla reálná past.

⚠️ **Zákazník nepozná nic** - migrace 1.7.0 → 1.8.0 zapne položku jen tam, kde se varianty reálně
tiskly (tedy když byl viditelný i sloupec „Čas"), a zkopíruje na ni jeho zarovnání i styl.
Rozhodnutí Jan 13. 8.: věrný převod vzhledu má přednost před zpětnou opravou pasti.

⚠️ **Nasazovací hook se po editaci `.cls` nespustil** a server vracel starou třídu se zrušeným polem.
Chytlo to jen ověření **voláním** (`cache-api.md`) - čtení zdroje v repu by tvrdilo, že je hotovo.

## 2026-08-12 ✅ - `B69`: texty značek se řídí jazykem šablony

Detail → changelog 2026-08-12 (8). TRANSDEV #12843: slovenský zákazník měl správné piktogramy, ale
české popisky u nich. Jazyk textů legendy teď určuje **kultura šablony** (Vlastnosti stránky), ne
firemní příznak starého modulu - rozhodnutí Jan 12. 8., jeden zdroj místo dvou a jedna firma může
mít českou i slovenskou šablonu vedle sebe. Znění je doslova ze starého modulu (`MODE=1`).

⚠️ **Nepřekládá se, co napsal uživatel** (poznámky z číselníku, názvy zastávek, nadpisy sloupců dnů)
a znak bez slovenské varianty padá na češtinu - slovenská sada je override, ne náhrada.

⚠️ **`JAZYK` NENÍ parametr sestavy**, i když ho `GetData` čte (roadmapa předpokládala allowlist
`ServerSideParams`). Uživatel ho v parametrech nenastavuje; re-fetch po přepnutí kultury drží
snapshot vedle definice sloupců. Driftový test si klíč vyžádal sám a je v něm zapsané proč.

Ověřeno voláním nasazené třídy (0365001/CB_CSADAD): `JAZYK=sk` → všech 39 stránek slovensky, bez
klíče česky, počet poznámek shodný. Zbývá porovnání s výtiskem zákazníka.

## 2026-08-12 ✅ - `B73`: šířky sloupců a výšky řádků tažením

Detail → changelog 2026-08-12 (9). Šířky sloupců po zrušení starého dialogu **nešly nastavit vůbec**,
takže to nebyl komfort, ale díra po předchozím kroku. Táhne se za vnitřní hranici, funguje i na
vybrané tabulce; řádek dostal vlastní nepovinnou výšku.

⚠️ **Poučení, které stálo tři kola proklikání:** pro řádky jsem vymýšlel jinou mechaniku než pro
sloupce (škálování obsahu místo rozdělení místa) a pokaždé to znamenalo zpětnou vazbu nebo pohyb
tam, kde se hýbat nemělo. Kolmý protějšek se má počítat **týmž kódem**, ne analogicky. Pravidla
(mění se jen dvojice u hrany; vnější hrana patří prvku) jsou v `ux-conventions.md`.

## 2026-08-12 ✅ - `B74` + `B75`: vkládání z Excelu a našeptávač polí v buňce

Detail → changelog 2026-08-12 (7). Ctrl+V vloží blok ze schránky od vybrané buňky a tabulku
dorovná; schránka uvnitř tabulky pracuje s buňkami, ne s prvkem. Po `{` v buňce vyskočí nabídka
polí agendy (filtr podle názvu i českého popisku, Enter/Tab doplní i s koncovou závorkou).

⚠️ **Doplnění pole spadlo** (nahlásil Jan): zápis do textového pole spustí obsluhu změny textu
synchronně, dřív než se posune kurzor. Kromě opravy z toho zbylo poučení - rozpoznání rozepsaného
`{POLE` odešlo z code-behindu do `Models/PlaceholderToken`, protože jinak se nedalo otestovat.

## 2026-08-12 ✅ - Fáze B krok 2: pevná tabulka se vyplňuje na plátně

Detail → changelog 2026-08-12 (5). Obsah i struktura se dělají v tabulce (Alt+klik do buňky, psaní
jako v Excelu, Tab a Enter posouvají, pravý klik na řádky a sloupce), dialog spadl na jedinou
záložku „Obecné" (`TableElementEdit.cls`) a **starý pětizáložkový editor je smazaný**.

⚠️ **Cesta dovnitř se hledala třikrát** - dvojklik i „druhý klik na vybranou tabulku" se zahodily
(důvody v changelogu a v `ux-conventions.md`), zůstal **Alt+klik**. Nezkoušet znovu.

⚠️ Drift testy schémat chytly tři rozpory nového formuláře proti C# - výchozí hodnoty, „Zdroj dat"
jen v panelu, odlišný popisek téhož pole. Přesně ta třída chyb, kterou proklik neodhalí.

## 2026-08-12 ✅ - Fáze B krok 1: pevná tabulka je zpátky v paletě

Detail → changelog 2026-08-12 (3). Záměr upřesněn Janem: **není to featura pro faktury, je to
náhrada „poskládám to z textových polí"** (jeden prvek místo šesti textů v hlavičce).

Prvek v paletě i v nabídce „Přidat" (vloží mřížku 3 × 3 bez řádku záhlaví), přepínač režimu z panelu
pryč, render převeden na sdílený plán řádků (zmizela svislá čára navíc na vnějším okraji a vrstvení
dvou pozadí), prázdná tabulka je vidět, zmenšení i **roztažení** řádků na výšku prvku.

⚠️ **Nález z proklikání, který platil pro obě tabulky:** čára tenčí než pixel se bez vyhlazení kreslí
jen když trefí střed pixelu → mřížka při posunu prvku blikala (z deseti pozic chyběla v šesti).
Pero mřížky je nově na jednom místě (`GridStyle.CreatePaint`) se stejným pravidlem, jaké měly JŘ
prvky od začátku. Ověřeno proklikem Jana 12. 8.

## 2026-08-11 ✅ - `B51` seznam se plní najednou, ne po jednom řádku

Detail → changelog 2026-08-11 (2). Nález Jan 4. 8., **první pokus vrácen** (`DeferRefresh` shodil
výběr linky - odklad neodkládá oznámení o změně kolekce, jen přepočet pohledu).

Dvě stě řádků znamenalo dvě stě přepočtů navázaného pohledu; vidět to bylo ve chvíli, kdy načtení
dosedlo, zatímco uživatel psal do filtru skupiny. Odkrylo to až async načítání pickeru - dřív to
schoval zámrz synchronního načítání.

Řešeno hromadnou výměnou obsahu (`BulkObservableCollection.ReplaceAll`) = jediný `Reset`, který
pohled dostával i dosud z `Clear`. Past cestou: parsování řádků je **líný iterátor**, takže se musí
zhmotnit před odběrem změn - jinak druhý průchod vyrobí jiné instance a odběr visí na řádcích,
které v seznamu nejsou.

⚠️ Sdílená komponenta (i EDISONMAIN2). **Ověřeno proklikem Jana 11. 8.** - testy tuhle třídu chyb
nechytí, přesně na tom padl pokus ze 4. 8.

## 2026-08-10 ✅ - `B39` vlastní sloupce vývěsky (#13045b), obě etapy

Detail → changelog 2026-08-10 (9) rozhodnutí, (10) etapa 1 = dny + značky, (12) etapa 2 = období.
Pohltilo `TR10` (skrývání spojů o víkendu).

**Etapa 1:** dialog odjezdů má tab „Vlastní definice sloupců" - až 5 sloupců, u každého nadpis, dny
(Po-Ne + svátek), značky „jen s těmito" / „bez těchto" a per-sloupcové „nevypisovat značky"
(přestěhování `B63` z linky na sloupec). Čtyři dnešní rozvržení jako vzory. Nepokryté spoje hlásí
oranžový badge i dovětek u exportu PDF.

**Etapa 2:** třetí podmínka **Období** (víc rozsahů dat; bez roku = každý rok, s rokem jednorázově).
Vyhodnocení „kdy spoj jede" dělá **`ALVA.ISY.ISYLinky.API.LinkyStatistika.jede`** - denní bitmapa
přes období, jedno volání na linku. Spoj v období jede aspoň den → patří do sloupce; jede celé
období → negativní značka se potlačí. Sloupec s obdobím mimo platnost jízdního řádku nebere nic
a hlásí to (jinak by šablona po překlopení JŘ tiše zmlkla).

**Denní značky:** obecné pravidlo „značka jen když spoj nepokrývá celý sloupec" **jen u kombinací
dnů, které dnešní rozvržení nezná**; standardní sloupce beze změny (rozhodnutí Jan po měření -
v „Neděle a svátky" by značku dostalo 86 % spojů, což nerozlišuje nic). Negativní značka to
nahradit nemůže, nese datumové omezení, ne den v týdnu.

**Dluh z etapy 1 splacen:** vzory staví server (`PresetySloupcu` → pole `PRESETY_SLOUPCU`), klient
je jen čte; parita tak měří to, co editor reálně pošle.

Gold digesty přebaselinovány (jen md5, počty beze změny). Sondy k oběma etapám žijí
v `AdSloupekProbe.cls`.

## 2026-08-10 ✅ - `B63` čteme „nevypisovat značku ve sloupci" (per linka, 4 sloty)

Starý modul má u linky **dva různé** per-sloupcové seznamy a my čteli jen jeden:
`MHD_NENZ1_NENZ2` skryje **spoj** z pracovních sloupců (čteme dávno), `MHD_NEZNZ123` (4 sloty, slot N
= sloupec N) nechá minutu vytisknout a **potlačí u ní jen značku**. Druhý jsme ignorovali, takže na
naší vývěsce se tiskly značky, které si zákazník u linky vypnul.

Nově `ZnackaSkrytaVeSloupci` + potlačení v `NacistOdjezdy` per sloupec. **Legenda se zapisuje až
u sloupce, kde se značka reálně tiskne** - když je potlačená ve všech sloupcích spoje, vypadne
i z legendy (1:1 legacy `noNZ2`). Slot N patří sloupci N; ⚠️ u `MHD_NENZ1_NENZ2` má legacy sloty
**prohozené**, tady ne.

**Doloženo před/po na živých datech** (`SKNI_MHDNITRA`, linka 0000001, konfigurace `52~52~53,54,55~`):
značka 52 byla u **34 ze 47 odjezdů** a v legendě → po opravě **0× a v legendě není**. Sonda
`PocetZnacky` postavila stránku vývěsky a spočítala anotace; „před" se změřilo mutací (potlačení
vypnuté), aby bylo vidět, že to opravdu dělá ta nová větev.

⚠️ **České firmy to nastavené nemají** (`CB_CSADAD` 0/31216 linek, `ZL_KOVED` 0/17029) - proto zlaté
odtisky zůstaly netknuté a proto to tak dlouho nikdo nenahlásil. Používá to **slovenská MHD Nitra**,
kde je to na každé lince. Souvisí s #12964 (stížnost na množství značek u vsetínské MHD).

Ověřeno: `TestZnackaSkrytaVeSloupci` (9 assertů, mutace bez hranic slova ho shodí), Caché 75/75
včetně zlatých odtisků, .NET 1885. Koncepčně proklikané (Jan 10. 8.).

🔭 **Bude se to muset předělat s `B39`** (poznámka Jan 10. 8.): tenhle seznam je **poziční** - slot N
patří sloupci N - což drží jen u dnešních čtyř pevných rozvržení sloupců. S vlastními skupinami dnů
index sloupce přestane být stabilní. Domluva s kolegou: přesunout nastavení **rovnou do návrháře**,
aby si každý sloupec nesl svůj seznam. Totéž potká vylučovací seznam u `TR10`. Detail a otevřená
otázka (co s už vyplněnou konfigurací u linek Nitry) jsou u `B39` v roadmapě.

**Poučení:** test class upravená skriptem se **nenasadí** (hook jede jen po Edit/Write) - runner
hlásil 74/74 a nový test vůbec neběžel. Chytlo se to jen tím, že jsem si všiml nezměněného počtu.

---

## 2026-08-10 ✅ - `B64` „<1" místo „0" u jízdy pod minutu (TRANSDEV `TR8`)

TRANSDEV `TR8`. Prvek Tabulka zastávek dostal volbu **„Jízda pod minutu jako <1"** (výchozí vypnuto):
jede-li se na následující zastávku pod minutu, vypíše se ve sloupci časů „<1" místo prázdna.

**Proč to šlo udělat bez kolegů:** premisu vyjasnil Jan 10. 8. - zákazník musí mít na mysli první úsek,
protože časy jsou kumulativní od vývěskové zastávky, takže její vlastní řádek je 0 **a hned následující
zastávka je taky 0**, jede-li se tam pod minutu. Dál v tom sloupci nula pro jízdu nevzniká (součty se
při nulovém úseku jen zopakují, ověřeno na `CB_CSADAD` **skupina 10**, linka 0380520, trasa 2: zastávky 2→3 ve stejné minutě). Tím padla hypotéza
o legacy sloupci „Průběhy" i potřeba per-segment sloupce.

**Jak se pozná skutečná jízda pod minutu od chybějícího údaje:** `CasJizdyZMapy` vrací 0 i pro zastávku,
kterou reprezentativní spoj neobsluhuje. Protože kumulativní časy rostou, taková nula přijde **za**
prvním nenulovým časem - `MarkSubMinuteCells` proto značí jen souvislý běh nul hned za vývěskovou
zastávkou a první reálný čas ten běh ukončí. Projeté zastávky a vývěsková sama se nikdy neznačí
(tam nulová jízda není informace, ale definice). Variantní sloupce mají každý vlastní běh; „<" (varianta
tu nestaví) není čas, takže běh neukončí ani nezaloží.

Ověřeno: 8 nových testů, **obě mutace implementace testy zabily** (`break`→`continue` = 1 pád,
vynechané `continue` u vývěskové zastávky = 5 pádů). Drift brána pole vidí (rozdílný default
v `.cls` a C# ji shodí). Nasazená třída ověřena voláním `Init` - server vydává pole 41
`ShowSubMinuteTravel`. Caché testy 74/74, .NET 1881.

**Vizuální kontrola: odtisk + reálná linka.** Odtisk prvku nad syntetickými daty potvrdil, že „<1"
se do úzkého sloupce časů vejde, vývěskové zastávce zůstane prázdno a řádek s chybějícím údajem
zůstane prázdný taky.

**Reálné nulové úseky existují ve všech čtyřech dev firmách** (sonda 10. 8., projela `W` trasy linek
a hledala dvě sousední zastávky se stejným časem): `CB_CSADAD` 8+, `ZL_KOVED` 6+, `CB_GWBUS` 7+,
`SKNI_MHDNITRA` 6+. Klikací cesta pro KOVED: **skupina 2, linka `0805302`**, vývěska pro
**Uherské Hradiště, Na Rybníku** směr Mařatice - následující zastávka *Mařatice, dol.konec* je ve
stejné minutě (jsou ~130 m od sebe).

⚠️ **Dvě pasti, na které sonda narazila** (kdyby se hledalo znovu): linka `0380520` je ve firmě
`CB_CSADAD` ve **skupině 10**, ne 1 - skupiny nejdou od jedničky po řadě (jsou i 10, 101, 127), takže
„v skupině 1 to není" neznamená „není to tam". A **`+"|"` je nula**: segment `|` (zastávka na trase,
kde spoj nestaví) se bez filtru počítá jako čas 0 a sonda hlásí falešné nulové úseky - první verze
jich takhle našla 16, všechny neplatné.

**Doplněno 10. 8. po vizuální kontrole (Jan):** samotné „<1" odsadí svou číslici doprava a zarovnání
sloupce vpravo to řeší jen zdánlivě (jednociferné časy pak odskočí místo dvouciferných). Prvek proto
**odsadí ostatní čísla o šířku znaku „<"** (měřeno z reálného fontu), takže číslice stojí ve svislici
a „<" z ní vystupuje doleva. Odsazuje se jen při zarovnání vlevo (vpravo se číslice srovnají samy)
a jen ve sloupci, kde „<1" reálně je. **Bez přepínače** - Jan ho po vidění odtisku zrušil, tohle je
jediné chování (nabízený přepínač „Zarovnat číslice pod sebe" existoval jen mezi dvěma commity).

**Vedlejší nález opravený tímtéž: nastavení sloupce „Čas" se nepropisovalo na varianty tras.**
`ExpandTravelTimeColumns` vytvářel variantní sloupce od nuly. ⚠️ **Nejdřív jsem zdědil jen
`Alignment` a bylo to k ničemu** - dialog sloupců píše zarovnání (vodorovné **i svislé**) do
`Style.Data`, takže uživatel po opravě neviděl žádnou změnu a nahlásil to. Dědí se proto **celý
per-sloupcový styl i `Alignment`**; šířku a hlavičku si varianty určují samy. **Poučení:** než
prohlásím „opraveno", ověřit tou cestou, kterou uživatel reálně klikne - dvě cesty k témuž nastavení
znamenají, že oprava jedné z nich nemusí být vidět.

**Seedy:** uživatelská příručka (Tabulka zastávek) + Novinky.

---

## 2026-08-05 ✅ - `B37` pokračující spoje (#13093 bod 3, tiket tím dojetý)

Detail → changelog 2026-08-05 (1). Značka `u1`/`u2`… u odjezdu + vysvětlivka „pokračuje do zast. X
(l. 532)", seskupeno podle (linka, cíl), jeden checkbox v bloku poznámek.

**Poučení, které se nesmí ztratit:** data jsou v uzlu **34** (`u@<náš spoj>:<linka>:<spoj>:<cíl>`),
**ne v 32** - ten je návaznost. `m@<n>` je index záznamu, ne typ vztahu. Zastávka v záznamu **je cíl**
(dokázáno rozdílným vzorkem: spoj 251 → Štěrkoviště = konečná jeho pokračování, ostatní → žel.st.),
takže se trasa pokračujícího spoje nedohledává.

Správný uzel určil kolega v #13098; já jsem předtím dvě session stavěl na 32. Kdyby se to nezeptalo,
značku „pokračuje" by dostaly spoje, které jen čekají na návaznost.

## 2026-08-05 ✅ - `B57` přerostlé skupiny formulářů (zúženo)

**Zadání bloku (nález review 4. 8.):** „Zastávky - specifické" ~23 polí, „Obecné" u dynamické
tabulky 13 a mřížky odjezdů 10, „Filtrování" legendy 10; `TableEditorDialog` 5 tabů (limit 3);
chybějící descriptions u `SCHOVAT_V_ROZSAHU`, `ROZTAHNOUT_NA_VYSKU` a celého formuláře mřížky
odjezdů (žargon „anotace"). Vel. **M**.

**Hotovo zúžené** - detail v changelogu 2026-08-05 (8). Rozpad skupin u tří prvků, žargon „anotace"
→ „značky u odjezdů" (formulář i panel), 13 vysvětlivek. **Dva body ze zadání vyhozeny s důvodem:**
karty pevné tabulky (spící prvek, redukci už řeší Fáze B krok 2 - byla to druhá evidence téže práce)
a popisy parametrů sestavy (parametr nemá pole pro popis → aditivní změna kontraktu, přesunuto
k `B18` follow-up 3). Rozpad skupiny „Filtrování" shodil bránu z `B58`, která hlídá, že nápověda
odkazuje na existující místo - srovnána nápověda, wiki seed i test.

**Proklik 5. 8. (Jan):** rozdělení vyhovuje. **Otevřený kandidát na později:** kdyby zákazníkům
formuláře pořád přišly dlouhé, rozdělit je na **víc tabů** (dnes je celý formulář jeden svitek se
skupinami). Čeká se na jejich reakci, nezakládá se blok - není co řešit, dokud si někdo nestěžuje.

## 2026-08-05 ✅ - `B58` srozumitelnost pro nováčka

**Zadání bloku (nález review 4. 8.; vstup = checklist na proklik):** F1 nápověda nezná featury
posledního měsíce (oddíly seznamu, řazení dynamické tabulky, filtry poznámek - přesně kde budou lidi
hledat zrušené parametry); náhled s ukázkovými daty není nijak označen (badge); Ctrl+N vyrábí šablonu
bez agendy (zdědit aktuální, nebo rovnou otevřít výběr); ChangeAgendaDialog ukazuje interní kódy;
„Použít" s 0 vybranými linkami tiše spadne na mock. Vel. **M**.

**Hotovo** - detail v changelogu 2026-08-05 (6). Ctrl+N **dědí agendu** (rozhodnutí Jan 5. 8.,
alternativa „otevřít výběr" zamítnuta - přidávala klik tomu, kdo dělá deset vývěsek pro tutéž
agendu). Nulový výběr linek se rozlišuje: nic nebylo vybráno → navést, něco bylo → zrušení povolit.
Nad zadání doplněn AI pomocník, který měl stejnou mezeru. Nová brána hlídá, že přepínače, na které
nápověda odkazuje, na tom místě opravdu jsou.

**Zbývá na uživateli:** vizuální posouzení štítku a čitelnost textů nápovědy - `review-proklik-checklist.md`
zůstává otevřený pro proklik (jeho ostatní body strojově posoudit nejde).

## 2026-08-05 ✅ - `B59` výchozí hodnoty děděných stylů proti kaskádě

**Zadání bloku (zbytek `B54`, 5. 8.):** u property, která smí být nenastavená („dědí se"), formulář
vědomě ukazuje hodnotu, na kterou dopadne kaskáda. Drift test to přeskakoval - porovnával surovou
property, a ta je null. Právě tady lež uživatel uvidí. Srovnání muselo jít přes jednotný přístupový
bod, ne reflexi naslepo. Vel. **S-M**.

**Hotovo** - detail v changelogu 2026-08-05 (5). Předpokládaných 8 polí kaskádě odpovídalo, ale
rozšíření parsování o sdílený stylový balíček (padalo na tom, že `_` je v ObjectScriptu operátor
spojení) odhalilo **dvě reálné lži**: zaškrtávátko „Tučné" u záhlaví a u minut ukazovalo
nezaškrtnuto, přitom se ten text tučně tiskne. Opraveno parametrem `defBold` ve `StyleFields`.
Mřížka odjezdů dostala tři přístupové body „z čeho zóna dědí" (konvence `ResolveElement<Zóna>Parent`;
chybějící přístupový bod test hlásí, nepřeskakuje). Poučení: **odtisk pixelů tuhle třídu změn
nezachytí**, protože fixtury mají styly natvrdo - kryjí ji kaskádové unit testy.

## 2026-08-05 ✅ - `B55` testy migrace šablon + parsingu agend

**Zadání bloku (nález review 4. 8.):** `MigrationGridStyle` (1.7.0, nejsložitější migrace - 5 hodnot
borderStyle + přenos rámce), `MigrationHighlightRules` a `MigrationKnizniToLinkovy` nemají unit test
navzdory pravidlu v `template-schema-versions.md` krok 5; `CacheAgendaProvider` parsing
(GetSchema→DataSchema, GetData→multi-page List, přeskok „ERR") bez testu navzdory
`feature-design-principles.md §7`. Vel. **M**.

**Hotovo** - detail v changelogu 2026-08-05 (3). +79 testů (646 → 725), vícestránkové čtení dat
vyčleněno do `ParsePages` (jinak neověřitelné bez serveru), nová pojistka
`KazdaMigrace_MaVlastniTridyTestu`. Sada ověřená **mutacemi** - jeden test byl slepý (přeskočení
`ERR`) a opravil se; poučení zapsané do `template-schema-versions.md` krok 5.

## 2026-08-05 ✅ - `B54` drift test výchozích hodnot formulář ↔ C# + chybějící pole

**Zadání bloku (nález review 4. 8.):** `EDInputFormSchemaDriftTests` hlídá existenci property, ne
defaulty: nalezeno ~10 rozdílů, kde formulář „lže o defaultu". Rozšířit test o porovnání `f.value`
↔ C# default + doplnit do `DepartureGridElementEdit.cls` chybějící pole (barvy negativní značky,
zarovnání záhlaví). Vel. **S-M**.

**Hotovo** - detail v changelogu 2026-08-05 (2). Rozdílů bylo 14 a **nešlo o živou chybu**: formulář
hodnotu ze schématu použije jen u property bez hodnoty, takže se dnes vždy přebije skutečnou -
byla to nastražená past, ne lež. Srovnáno i tak a zamčeno testem. Nad zadání přidána druhá kontrola
„co jde nastavit v panelu, jde nastavit i ve formuláři" (3 vědomé výjimky napříč deseti prvky) -
právě ta chytá třídu, do které patřila chybějící pole mřížky odjezdů. Zbytek → `B59`.

## 2026-08-05 ✅ - `B53` per-frame přepočet u obrázku a čárového kódu

**Zadání bloku (nález review 4. 8.):** `ImageElement.Render` dekóduje Base64 (až 5 MB) + bitmapu
a `BarcodeElement` přepočítává ZXing matici **při každém framu**; porušení pravidla §7 „structured
parsing vždy cachovat". Cache s invalidací per (obsah, velikost) po vzoru
`StopScheduleElementBase.SetCacheRef`; bitmapu držet dekódovanou, dispose při změně. Vel. **S**.

**Hotovo** - detail v changelogu 2026-08-05 (1). Nad zadání šlo jedno zjištění: hlavní cenou nebylo
zakódování, ale kreslení - ZXing vrací matici v **pixelech cílové velikosti, ne v modulech**, takže
se u velkého QR kreslilo ~160 tisíc obdélníčků po jednom. Slití souvislých úseků do jednoho
obdélníku je díky překryvu `+0.5` pixelově identické (potvrzeno odtisky) a přineslo většinu
zrychlení: QR 100 mm 43 → 1,0 ms na frame. Ověřeno odtiskem 15 případů před/po (identický)
+ `CachedContentRenderTests` (13 testů), sada 622/622.

## 2026-08-04 ✅ - vypínání obsahu jen v šabloně (deduplikace parametrů × filtru)

Detail → changelog 2026-08-04 (7). **Rozhodnutí Jan:** rozhoduje, co vidí zákazník, ne co posílá
server - nesmí mít jedno nastavení na dvou místech kvůli interní logice. Poznámky linky, kola,
místenky a souhrn bezbariérů se zapínaly parametrem sestavy **i** filtrem v bloku poznámek.
Server teď posílá vše otagované kategorií, rozhodnutí drží jen šablona, čtyři parametry zrušeny.

**Invariant:** značka u minuty a její vysvětlivka visí na **jedné** volbě (filtr prořezává obojí
podle kategorie) → nemůže vzniknout značka bez vysvětlení ani vysvětlivka bez značky.

**Dvě poučení, obě „ověřoval jsem něco jiného, než co platí":**
1. `.cls` editovaný **skriptem** se nenasadí (hook reaguje jen na editaci nástrojem) a `cache_compile`
   to nespraví - hledá zdroj pod `C:/DEV/EDISON`, takže zkompiluje starou serverovou kopii a řekne
   „up-to-date". Ověření = **zavolat agendu a podívat se, co vrátí**, ne čtení zdroje.
2. Jména kategorií nesou **dvě nezávislé věci** - co se vypíná v šabloně a co se překládá na glyf.
   Po přejmenování zůstal překlad na starém jménu a kolo vyjelo jako cizí piktogram; testy zelené,
   protože si staré jméno držely samy. → jediný zdroj `Models/NoteCategories.cs` + pojistka, že
   markerové kategorie jsou v sémantické sadě.

**Nedodělané vědomě:** cílový model z rozhodnutí o kategoriích (checklist generovaný z metadat).
Blokátor pojmenován: `ElementPropertyAccessor` naváže jen property cesty, **ne mapu** → chce rozšířit
binder nebo přidat typ pole do EDControls. Dnešní stav: ubyly 4 parametry, přidaly se 3 volby.

## 2026-08-04 ✅ - `B44` nabídka datových polí místo psaní `{POLE}`

Detail → changelog 2026-08-04 (16). Vedle pole je tlačítko s hledatelným seznamem polí agendy;
výběr **vloží token na pozici kurzoru**. Zapnuté u obsahu textového prvku, podmínky viditelnosti
(prvek i sekce), viditelnosti řádku tabulek, obsahu čárového kódu a názvu souboru při exportu.

**Rozhodnutí Jan:** (1) vkládat, ne nahrazovat hodnotu - změřeno na šablonách v repu, že třetina
výskytů je věta s tokeny uvnitř, takže výběr celé hodnoty comboboxem by je znemožnil; (2) nabízet
**všechna pole agendy** s popiskem, odkud jsou - uživatel hledá název, nepřemýšlí v kategoriích;
(3) v1 nechat uživatelům vyzkoušet a doladit podle zpětné vazby, ne dopředu (Jan 4. 8.).

**Poučení:** aditivní pole obousměrného kontraktu se musí přidat do **zápisu i čtení na obou
stranách** (4 místa, ne 2) - jinak ho zpáteční cesta zahodí. Statická kontrola to nechytí, protože
se vždy dívá jen na jeden směr; rozhodla sonda logující každý průchod. Nasazení `.cls` se ověřuje
stažením ze serveru.

**Vědomě nedodělané:** obsah buněk tabulek (edituje se vlastním dialogem sloupců, ne formulářem -
samostatná UI cesta, ne jednořádkový příznak) a doplňování při psaní `{` nad týmž seznamem.

## 2026-08-04 ✅ - `B27` řazení řádků v dynamické tabulce

Detail → changelog 2026-08-04 (14). Uživatel vybere sloupec a směr (druhý klíč se odkryje až po
prvním), **typ řazení se neptá** - číslo, datum i text se poznají z hodnot.

**Rozhodnutí Jan:** (1) typ odvodit, ne nabízet - špatně zvolený typ vyrobí výstup, který vypadá
seřazeně a je v nesmyslném pořadí, a nikdo netuší, že textově je „12" před „9"; (2) comboboxy,
ne mini-jazyk typu `ORDER BY` - psaní názvů polí rukou je přesně vada, kterou má řešit `B44`,
takže zavádět další takové místo by šlo proti sobě; (3) jen dynamická tabulka - u zastávek pořadí
určuje trasa a to se řadit nesmí.

**Poučení, které se nesmí ztratit:** „prázdné hodnoty dozadu" **nesmí být návratovou hodnotou
porovnání** - sestupné řazení komparátor obrací, takže by prázdné vyplavaly nahoru. Musí to být
vlastní úroveň řazení před hodnotou. Napsal jsem to nejdřív špatně a chytil až při psaní testu
na obě směry.

Druhé: řazení sedí v `VisibleDataRows`, tedy v **jediném trychtýři**, kterým jde render i výpočet
výšky. Kdyby se řadilo jen v renderu, rozešly by se - táž třída chyby jako asymetrie výšky a fontu
u záhlaví.

## 2026-08-04 ✅ - `B48` gate změřen: kolize podle písmene u MHD nehrozí

Detail → changelog 2026-08-04 (12). Blok se zakládal na obavě, že MHD atribut s kódem 65/76 by se
spletl s přestupem a přeložil na cizí piktogram. **Změřeno v číselníku `CB_DP`: ty kódy tam nejsou**,
a strukturálně je kolize nepravděpodobná - agenda MHD posílá `$c(SpecZnak)`, tedy **pozici ve fontu**,
ne písmeno, takže by firma musela atribut nasměrovat přesně na 65/76 (trojúhelník / jízdní kolo).

**Měření ale našlo jinou věc, než blok předpokládal:** MHD posílá přestupy jako `w`/`x`, kdežto
`IsTransferAttr` je hledá podle písmene `A`/`L` - u MHD je tedy vůbec nepozná. Přepínač „Přestupy
(vlak/autobus)" na ně nemá vliv, řídí je obecný přepínač „Atributy". Tisknou se správně, jen se
ovládají jinak než u AD. Zbytek přesunut do „Bez poptávky nebrat", protože to není vada výstupu.

**Poučení - gate se vyplatilo změřit dřív než programovat.** Blok byl odhadnutý na půl dne práce
kvůli riziku, které v datech neexistuje. Měření stálo pár dotazů a změnilo zadání: z „oprav kolizi"
se stalo „přepínač u MHD neplatí", což je jiná (a menší) věc s jinou naléhavostí.

Vedlejší zisk: číselník atributů `CB_DP` je v `cache-database.md` doplněný na kompletní naměřený
stav (dosud tam byla půlka) + poznámka, že sloupec `Znak` na wire vůbec nejde.

## 2026-08-04 ✅ - `B49` pozice glyfů ve slovenském fontu ověřeny

Detail → changelog 2026-08-04 (11). Obrysy vytažené z obou fontů (postup v `elementy-architektura.md`).
**Co dnes překládáme, je pro SK správně** - `z` autobus, `x` vlak, `L` kolo sedí v obou fontech
(u `z` a `L` je obrys dokonce identický), takže `SymbolGlyph` culture parametr nepotřebuje a žádná
vada se slovenskému zákazníkovi netiskla.

Skutečná vada byla **v komentáři**: `FontResolver.cs` tvrdil „znaky jsou pro CZ i SK stejné, liší se
jen tvary glyfů". Měření to vyvrátilo - `w` je v CZ vagon a v SK **autobus**, `D` je v CZ křížek
a v SK **dlaň**. Komentář opraven, měřená tabulka zapsaná do dokumentace.

**Poučení - dokumentace si protiřečila a nikdo si toho nevšiml.** Testy i `elementy-architektura.md`
měly rozdíl CZ/SK popsaný správně (zapsalo se to při opravě kola), jen `FontResolver.cs` tvrdil opak.
Kdo by se řídil jím, namapoval by autobus na `w` a slovenskému zákazníkovi by tiskl vagon. **Rozpor
mezi dvěma komentáři je horší než chybějící komentář** - vypadá to jako ověřená informace.

## 2026-08-04 ✅ - `B40` mřížka tabulkových prvků jako jedno sdílené API

Detail → changelog 2026-08-04 (10). „Rámeček = vnější, mřížka = vnitřní" (rozhodnutí Jan). Sdílený
objekt `GridStyle` pro všech pět tabulkových prvků + balíček polí `AddGrid` vedle `AddBorder`;
tabulky poprvé dostaly nastavitelnou barvu/tloušťku/styl čar, JŘ prvky možnost čáry vypnout.
Migrace 1.6.0 → 1.7.0 drží vzhled.

**Poučení 1 - průzkum vyvrátil zadání.** Blok tvrdil, že JŘ prvky mají „barvu + přepínače"
a tabulky volbu čar. Realita: JŘ prvky měly barvu **i tloušťku i styl** a chyběla jim jen volba
čar, kdežto tabulky měly volbu čar a vzhled vůbec. **Zadání psané zpaměti se rozešlo se stavem
za čtyři dny** - průzkum před implementací není formalita.

**Poučení 2 - hledej duplicitu konceptu, ne chybějící nastavení.** Skutečný problém nebyl
„uživatel si nenastaví barvu", ale že k vnějšímu obrysu vedly dvě nezávislé cesty. Jakmile se to
rozdělilo podle významu (rámeček = vnější, mřížka = vnitřní), zmizel i důvod, proč tabulky měly
vlastní enum pěti hodnot.

**Poučení 3 - odtisk renderu ověřuje jen to, co kreslí.** Odtisky potvrdily, že čtyři JŘ prvky
vyšly bajt po bajtu stejně (refaktor behavior-preserving) a změnila se jen dynamická tabulka.
Ale odtisk volá `Render()`, ne `RenderBorderPass()`, takže **vnější rámec neověřuje** - u změny,
která obrys přesouvá jinam, je zelený odtisk shodou okolností, ne důkazem. Jan proto prošel
výsledek očima v běžící aplikaci a odsouhlasil.

**Poučení 4 - zelený drift test neznamená nasazeno.** Formuláře na Caché jsem upravil skriptem
z Bashe, takže se nespustil deploy hook (jen Edit/Write) a na serveru zůstaly staré třídy.
Projevilo se to jako „pole je prázdné a uložení nic neudělá", protože klíče polí přestaly
odpovídat vlastnostem v C#. **Drift test to nechytil - kontroluje dvojici repo × C#, ale rozešla
se dvojice server × C#.** Past i postup vynuceného deploye jsou v root `CLAUDE.md`.

## 2026-08-04 🧹 - Revize roadmapy: zrušen ukazatel `L‑1…L‑4` + mrtvé a duplikované záznamy

Roadmapa měla 375 řádků, z toho 35 balastu (rozhodnutí Jan: „jen vyloženě mrtvé nebo duplikované",
tabulku `C5`–`C7` nechat). Odebráno:

- **Ukazatel `L‑1…L‑4`** (rozhodnutí Jan) - měl zajistit, že linkový JŘ postoupí v každé vlně, ale
  reálně je odložený „až po zákaznické frontě", takže mechanismus nefungoval a byl to jen třetí
  opis týchž bloků. Duplikované řádky `C2`/`C3`/`C8`/`C9` zrušené, ale **jejich unikátní detail
  nejdřív přesunut do bloků** (`B10` gate R5, `B13` gate R1/R2, `B14` celý obsah auditů H1+H2) -
  `B14` na ně do té doby jen odkazovala, takže smazání bez přesunu by detail zahodilo.
- **Stale tvrzení:** „`B37` čeká na odpověď, `B45`" - `B45` byla hotová a v archivu a `B37` je od
  4. 8. odblokovaná; hlavička o 200 řádků výš to říkala správně. Dál vysvětlivka k `B38` (hotová)
  a poznámka o vyvráceném nálezu barev zón (historie, patří sem, ne do aktivního plánu).
- **Duplicity:** `B12` byla 3×, `C4` 4×, otázka na KOVED k označníkům 2×.
- **Hlavička** nesla datum 2026-07-31 a přitom popisovala události ze 4. 8., plus výčet ~15
  dokončených bloků - ten drží archiv.

**Poučení, které se nesmí ztratit:** duplikace nevznikla nedbalostí, ale **zakládáním druhé evidence
k témuž bloku** (ukazatel postupu, „prerekvizity", detailní tabulka). Proto je z toho pravidlo 3
v návodu: jedna práce = jeden záznam, priorita se řídí vlnami. `B13` byl v dokumentu 6×, `B10` 5×.

**Druhé kolo téhož dne - zákaznická fronta vyprázdněna** (rozhodnutí Jan): revize odhalila, že
v sekci „Čeká na někoho jiného" visí položky, které už nečekají na nikoho. Odebráno **#12997**
(druhá barva linky - hotové), **#13091a** + **#13091 bod 4** (zóny, vzhled seznamu zastávek -
hotové) a **#13102** (virtuální prodloužení spoje - není u nás). V sekci zůstaly jen `TR2`
(čeká SK ověření kódování na-znamení u kolegy) a `TR8` (park, blokuje potvrzení TRANSDEV) - tedy
opravdu jen věci na někom jiném.

**Poučení:** sekce „čeká na někoho jiného" je nejtišší místo, kde plán hnije - položka se tam dá
odložit a nikdo ji už nikdy neproklepne, protože „to není naše práce". Stálo to za jeden dotaz
a čtyři z šesti položek zmizely.

#13102 zůstává zaevidované na dvou správných místech: v seznamu „mimo návrhář" (je u Petra) a jako
křížový odkaz v `B37`, kde vysvětluje, proč `B37` **není** na #13102 blokovaná.

## 2026-08-04 ✅ - `B47` drift test allowlistu serverových parametrů + `B51` plnění seznamu

Detail → changelog 2026-08-04 (8). Obecné nálezy dojeté po vydání verze.

`B47`: strojová pojistka proti pasti, která se vrátila **čtyřikrát** (parametr čtený v `GetData`
chybí v `ServerSideParams` → jeho přepnutí v UI nespustí re-fetch, takže klik nedělá nic a tisk
vyjede jinak než náhled). **Test našel čtvrtý výskyt sám:** `DELENI_SLOUPCU` v `LinkovyJrAd`.

**Poučení, které se nesmí ztratit:** u ručně držených allowlistů se **testuje oběma směry** -
chybějící klíč (tichá vada) i mrtvý klíč (zbytečný re-fetch). Ty dva testy se navzájem kontrolují:
kdyby parser `.cls` zrezivěl a nevracel nic, „chybějící" projde na prázdném vstupu, ale „mrtvé"
spadne na všech položkách allowlistu. Jednosměrná kontrola tuhle vlastnost nemá.
Druhé poučení: **pojistku ověř mutací** - dočasně odeber klíč a přesvědč se, že test spadne.
Zelený guard test nic nedokazuje.

`B51`: `EDList` plnil řádky po jednom do kolekce navázané na `DataGrid` s filtrem, takže 200 řádků
znamenalo 200 přepočtů pohledu; `DeferRefresh` (včetně nastavení filtru uvnitř) to zkolabuje do
jednoho. Projevilo se to až po přechodu na async načítání - dřív to bylo schované za zámrzem.

## 2026-08-04 ✅ - `B50` export i tisk posílají tytéž parametry co náhled

Detail → changelog 2026-08-04 (4). Export z lišty neposílal do `GetData` parametry sestavy vůbec
a nemergoval je do stránek, takže vyjel s výchozími hodnotami, i když náhled ukazoval zapnuté volby.
Při opravě se ukázalo, že **tisková cesta má zrcadlovou vadu** - zapomínala skupinu linek, takže
u AD (linka klíčovaná dvojicí skupina+linka) mohla vytisknout linku z jiné skupiny.

**Poučení, které se nesmí ztratit:** tři cesty stavěly parametry pro `GetData` každá zvlášť
a každá zapomněla něco jiného. Je z toho jedno místo (`ComposeGetDataParams` + `MergeParamsIntoPages`).
**Když se stejná mapa parametrů skládá na víc místech, je jen otázka času, kdy se rozejdou** -
a projeví se to jako „vytisklo se něco jiného, než jsem viděl", což uživatel nahlásí pozdě nebo vůbec.

## 2026-08-04 ✅ - `B46` filtrovaný ComboBox sjednocen na sdílené komponentě

Detail → changelog 2026-08-04 (3). Do filtru skupiny linek se nedaly napsat dva znaky: první
neodfiltroval nic, druhý přepsal napsaný text zpátky na jinou skupinu a přenačetl linky pro skupinu,
kterou uživatel nevybral. Výběr instancí si držel **vlastní** filtrovaný ComboBox, i když EDControls
má na tentýž účel `EDFilterComboBox`, který tuhle třídu problémů řeší a má na ni testy. Zrušena
i druhá vlastní kopie (nabídka datových polí v panelu vlastností) - v TD už žádná nezůstala.

**Poučení, které se nesmí ztratit:** filtrovat `ICollectionView` nastavený jako `ItemsSource` ComboBoxu
je past - ComboBox si s ním synchronizuje výběr, takže vyfiltrování právě vybrané položky vynutí změnu
výběru a ta přepíše text v editovatelném poli. Sdílená komponenta proto plní `Items` přímo a hodnotu
nese v `Tag`. **Než se v TD napíše filtrovaný ComboBox, hledá se sdílená komponenta** - tady byly
vedle sebe tři implementace téhož a vlastní byla ta rozbitá.

**Druhé poučení - deduplikace odhalila vadu ve sdílené komponentě.** Aby sloučení nebylo regresí,
musela se srovnat jedna odchylka: volný režim bral klidovou hodnotu pole jako filtr, takže šipka
nabídla jen právě nastavenou položku. Když se dvě implementace slévají, **rozdíly se musí projít
kus po kusu** - ne předpokládat, že sdílená je nadmnožina.

## 2026-08-03 ✅ - `B45` glyf kola + oprava autobusu (#13093 bod 2, #13048 bod 1)

Detail → changelog 2026-08-03 (13). Kola se tisknou; cestou se ukázalo, že **ranní oprava přestupů
byla z poloviny špatná** - autobus jsme podle ručně opsaného kódu poslali na `w`, kde je železniční
vagon, takže u přestupu na autobus jezdil vagon (vypadalo plausibilně, prošlo to i vizuální kontrolou).

**Poučení, které se nesmí ztratit:** pozici glyfu **nečíst okem z Mapy znaků**. Ikony jsou drobné,
vagon a autobus sousedí a dvakrát se to přečetlo špatně. Správný postup = vytáhnout obrysy přímo
z `.ttf` a nakreslit kontaktní list s popisky kódů (jména glyfů font nenese, jsou obecná).

**Druhý invariant:** pozice musí platit v CZ **i SK** fontu - `TimeTable2SK.ttf` **není kopie**.
`w` je tam autobus, `x` moderní jednotka. Naše volby (`z` autobus, `x` vlak, `L` kolo) znamenají
v obou totéž; `w` je přesně ta jedna pozice, kde by se vzhled rozešel podle jazyka.

Kola jdou naším markerem `K` (číselník pro ně znak nemá, `L` je vlak). Překlad glyfu se navíc dosud
aplikoval i na značky kreslené normálním fontem - ruční komentář „A" by v legendě vyšel jako „z";
podmínka „je to piktogram" je teď na jednom místě (`JePiktogram`) místo trojmo opsaná.

## 2026-08-03 ✅ - `B36` poznámky z linky + výluky, `B37` bod 2 místenky (#13093, #13088)

Detail → changelog 2026-08-03 (11) a (12).

**Poznámky zadané u linky** (`^JRL(sku,lin,"C",key,idx)`, klíč 1 tarifní / 3 všeobecné /
4 radiobus, klíč 2 legacy přeskakuje) se do zastávkové vývěsky dostávají poprvé. Vydáno obojí,
co klient chtěl: **skalární pole „Výlukové texty"** (ta „databázová položka" z #13088) i **řádky
poznámek s kategorií**, takže si výluky složí zvlášť (prvek poznámek má volbu Včetně / Bez výluk /
Jen výluky). Vše opt-in parametrem - zapnuté by přidalo dlouhý tarifní blok všem existujícím šablonám.

**Emitujeme záznam = jedna poznámka**, legacy je slepuje do jednoho textu; pak nejde oddělit výluku
od místenkového textu ve **stejné** kategorii 3 (**1 648 výluk** v celé firmě KOVED, všechny
v kategorii 3 spolu s „R - k jízdence je možné zakoupit místenku"). Proto filtr podle prefixu,
ne podle kategorie. (Původně tu stálo 166 - vzorek prvních 800 linek, viz oprava v changelogu 4. 8.)

**Bod 2 - místenky hotové, kola čekají na glyf.** „R" u odjezdu + řádek legendy, opt-in. Kolo má
hotové plumbing, ale `ZnakKola()` vrací prázdno: piktogram ve fontu je, jeho **znak neznáme** a
legacy ho na vývěsce netiskl. Test `TestZnakKolaZatimNeni` to drží - odhadnout znak = vykreslit
cizí piktogram, přesně chyba opravovaná téhož dne u přestupů.

**Bod 4 se nedělal - byl už hotový** (ověřeno 3. 8.): prvek poznámek umí `CalculateHeightFromData`
i `ComputeFontScale` a `PageLayoutEngine` u dynamické výšky posouvá prvky pod ním (`cumulativeDeltaY`).
Zbývá to jen klientovi ukázat s poznámkou, že první blok má mít výšku prázdnou.

## 2026-08-03 ✅ - `B43` čtvereček ukáže barvu z dat (#12997)

Detail → changelog 2026-08-03 (10). Pole s `{BARVA_LINKY}` mělo bílý čtvereček, takže vypadalo jako
nenastavená barva. Formulář o datech neví, hodnotu jen zobrazuje - proto **aditivní** vlastnost
`Field.PreviewValue` v EDControls (náhledovou barvu vyplní host, komponenta ji do hodnoty nepřepisuje)
+ šrafování pro token bez náhledu. V návrháři `ColorHex.ResolveDataBoundForUi` a naplnění v
`ElementFormBinder.Prefill`; data pro UI drží `ColorHex.UiData`, které nastavuje kreslení náhledu.

**Invariant:** hodnota zůstává tokenem. Zapsat vyřešenou barvu = šablona přestane brát barvu z dat
a zamrzne na barvě náhodně otevřené linky. Hlídá `ColorHexUiPreviewTests` (5 testů).

**Rozhodnutí (Jan 3. 8.):** cílový stav je **šrafování**, přesná barva je bonus tam, kde náhledový
canvas kreslil stránku s daty. V návrhářském pohledu data nejsou a dohánět je nemá cenu - důležité
bylo, aby čtvereček nevypadal jako nenastavená bílá. `UiData` se maže při každé změně dat náhledu,
protože **cizí barva mate víc než neutrální stav**.

⚠️ Nasazení: dotýká se **EDControls**, takže do produkce jde DLL i .exe.

## 2026-08-03 ✅ - `B35c` CIS čísla zastávek (#13088)

Detail → changelog 2026-08-03 (4). Tři nová pole sestavy (CIS číslo zastávky · výchozí · cílová)
+ totéž jako pole řádku kolekce zastávek. Zdroj = registr zastávek `^JRZ(czst)` `$`-pole **9**.

**Nebylo potřeba se zákazníka na nic ptát** (na což to bylo v roadmapě chybně napsané): šestimístný
údaj v `$`-poli 5 (`419940`), který vypadá jako CIS kód, je „číslo zastávky **pro strojky**" =
CIS × koeficient (`JRapiZst` ř.62/327). Test to hlídá, aby se ta záměna nevrátila.

**Zjištění, které stojí za zapamatování:** na datech KOVED je CIS číslo **shodné s interním číslem**
zastávky, které už tiskneme (42033 = 42033) - jejich číslování je přebrané z CIS. Z toho bodu tiketu
jim tedy reálně chyběla hlavně **výchozí a cílová** zastávka, kde jsme uměli jen název konečné.
U jiné firmy se ty dvě hodnoty rozejít mohou, proto pole smysl má.

Sloupec seznamu zastávek se nepřidával - tiket žádal pole agendy. Přes dynamickou tabulku to jde,
do nabídky sloupců se doplní, až o to někdo řekne (stejná úvaha jako u popisu sloupku).

Ověřeno na ZL_KOVED 0822995 směr Tam (Zlín,Malenovice,ZPS): aktuální 42033, výchozí 41994, cílová 2774.

**#13088 zavřený 3. 8.** (Jan) - dva ze tří bodů dodány (CIS čísla, sloupek), třetí (**výlukové texty**)
přesunutý do **#13093**, kde už poznámky z linky leží - včetně požadavku na **skalární pole**
(„nová databázová položka pro návrhář“). Cesta je rozhodnutá a **nepotřebuje zásah do JRMAIN**:
klient už dnes píše „Výluka: …“ do běžných poznámek linky (1 648 v celé firmě), stačí je začít
číst a oddělit podle prefixu → `B36`.

## 2026-08-03 ✅ - `B35b` logiku stanovišť lze přepnout na sloupek (#13088, #13048 bod 4)

Detail → changelog 2026-08-03 (2) a (3). Režimy „více hodnot", řádkové duplikování, dělení stránek
i titulek rozdělené stránky dosud počítaly jen s označníkem; nový parametr sestavy **Číslo stanoviště
podle** (Označník / Sloupek) je přepne naráz. Data pod tím: per-spoj sloupek (W 13), výčet dle
četnosti, sloupek per varianta trasy a popis sloupku z číselníku.

**Změřeno před stavbou** (400 linek ZL_KOVED): 94 linek plní sloupek i na úrovni spoje, 19 zastávek
má napříč spoji různé sloupky (u označníku 102 / 46). Bez toho měření by se stavěl přepínač, o kterém
nevíme, jestli má nad jejich daty co zobrazit.

**Rozhodnutí:**
- **Default Označník** - dělení stránek si vyžádal jiný zákazník a už podle něj tiskne; přehození
  defaultu by mu změnilo vývěsku bez varování. Zlaté odtisky to potvrzují (mimo nové pole beze změny).
- **Přepínač jen na jednom místě**: parametr sestavy (dělení stránek vzniká na serveru), prvek si
  výsledek čte z dat a vlastní kopii nemá - poučení z `B41`, hlídá to test na neserializaci.
- **Oba sloupce zůstávají nezávislé** - režimy platí pro zvolený údaj, druhý ukazuje hodnotu z trasy.
- **„Stanoviště" je od teď zastřešující pojem** (ne synonymum označníku) - popisky, které platí pro
  obojí, ho používají a ban v drift testu je zrušený. Viz `ux-conventions §8`.

**Pozice obou údajů drží jedna funkce** (`PoziceStanoviste`) a agregace i W-mapa jsou parametrizované
zdrojem, ne zkopírované; cache má zdroj v klíči.

**Poučení (past):** dvě `.cls` jsem upravil skriptem, ten zápisem přidal BOM, upload na server tím
tiše padal a testy běžely proti staré kopii třídy. `.cls` neupravovat skriptem vůbec; ověřit stav
serveru umí `AdSloupekProbe.ZdrojMetody`.

## 2026-08-03 ✅ - `B35a` sloupec Sloupek v seznamu zastávek (#13088, #13048 bod 3)

Detail → changelog 2026-08-03. Půlka bloku `B35` byla založená na špatném předpokladu, že „označník"
a „sloupek" jsou dvě slova pro totéž a bude potřeba se doptat JŘ, co v datech je. **Nejsou to
synonyma a doptávat se nebylo potřeba** - obojí je v datech vedle sebe:

- **Označník** = volný text z trasy linky (J-věta `#`-segment **3** = `nastupisteTam:nastupisteZpet`,
  legacy `dta4`/`dta5`; per spoj W pozice **7**). Bez číselníku, takže si tam každý píše co chce
  (u GWBUS jsou tam texty „JET"/„JETE", u KOVED na jedné zastávce „V").
- **Sloupek** = odkaz do **číselníku sloupků zastávky** `^JRZ(czst,"Sloupek",id)` = `číslo ~ GPS lat ~
  GPS lon ~ popis směru` (J-věta `#`-segment **12** = `sloupekTam:sloupekZpet`, legacy `dta14`; per spoj
  W pozice **13**, přebíjí trasu). Číslo 1-255, legacy ho před exportem do CIS kontroluje na číselnost.
  Zlín aut. nádr. má sloupky 1-8, 21-24, … s popisy „směr Brno přes UH".
- Doloženo v legacy `JRapiLin` ř.651-703 (rozpad J-věty na `dta`), `JRapiSp` ř.311/349 (per-spoj),
  `JRapiExp6` ř.1957-1963 a `JRapiExp10` ř.832-836 (pořadí rozhodování trasa → spoj).

**Sloupek je ten reálně používaný údaj:** ZL_KOVED 0822995 ho má u všech zastávek, označník jen
u části; CB_CSADAD 0365001 ho má u všech a označník nikde. Tiskli jsme tedy dosud to prázdnější pole.

Hotovo: pole agendy `SLOUPEK` (values i řádky ZASTAVKY) + sloupec **Sloupek** v seznamu zastávek
(defaultně vypnutý, existujícím šablonám se nabídne sám, bez migrace). Zlaté odtisky přebaselinovány -
shodné počty, jiné md5.

**Rozhodnutí (Jan 2026-08-03):** názvosloví se mezi AD a MHD **záměrně liší** - v AD jsou označník
a sloupek dvě různá pole, v MHD se témuž fyzickému stanovišti říká označník. MHD proto pole `SLOUPEK`
nezavádí. Pravidlo v `ux-conventions.md` §8 („vždy označník") zúženo na AD nástupiště.

**Zbývá z #13088:** `B35b` (per-spoj sloupek, popis, přepnutí režimů), `B35c` (CIS čísla, čeká na
zákazníka), `B35d` (výluky, čeká na zákazníka).

## 2026-07-31 ✅ - `B29` denní značky dle rozvržení sloupců (#13045a)

Detail → changelog 2026-07-31 (4). Klient hlásil dvě věci (sobotní odjezdy v nedělním sloupci ·
značka „6" navíc) a **byla to jedna chyba**: značku dne jsme emitovali bez ohledu na rozvržení
sloupců dnů, takže spoj s platností „sobota a svátek" nesl „6" i v nedělním sloupci, kam patří
kvůli svátku. **Časy byly celou dobu správně, lhala značka.**

Doportováno 1:1 dle `JRapiLinPrtMhd1` (nová `DenniZnacky`): oddělený sobotní a nedělní sloupec →
u platností 6/7/+ žádná značka · sloučený víkend → značka jen když spoj odlišuje (vylučuje se proti
sobotě) · konkrétní pracovní dny značkované vždy. Doplněna dosud chybějící značka **„svátek"**
(legacy symbol id 15) s legendou „jede ve svátek".

Ověřeno na datech klienta `ZL_KOVED` (linka 0771251, tři sloupce dnů, spoje „6+"), kontrolní vzorek
`CB_CSADAD` 0345067 (zmizelo 15 falešných značek). Zlaté odtisky přebaselinovány - ubyly **jen řádky
legendy** (110 → 96, 183 → 168), počty stránek/zastávek/odjezdů beze změny.

**Zbývá z tiketu #13045:** koncept vlastních skupin dnů s obdobími = `B39` (Vlna 3).
**Otevřený nález mimo tento blok:** u téže stránky se naše písmena rozdílů tras (E, Q, H) rozcházejí
s legacy (J, E, G, O) - samostatná věc, nespojovat s tímhle.

## 2026-07-31 ✅ - `B28` odpověď BBSK #13070 (bez vývoje)

Ptali se na variantní trasy (zacházky) v šabloně a na vývěsku na výšku. **Všechno umíme** - odpověď
byla návod, kde se co zapíná, plus nabídka hovoru; tiket vyřešen bez řádku kódu.

Podstatná zjištění, která z toho vypadla a platí dál:
- **„MHD" v zadání zákazníka obvykle znamená městskou dopravu, ne náš MHD modul** (ten mají jen
  DPCB/DPKV) - ověřovat modul evidence, viz `customer-requirements §0.1`.
- **Poznámky o rozdílech tras se zapínají u linky v JŘ** („Nastavení k tisku" → „Odjezdník MHD" →
  „Poznámkovat rozdíly v trase"), ne v šabloně; sada písmen je tam v poli „Volné značky".
- **Bez vzorové trasy poznámky nezmizí** - odpadne jen věta „jede až do" (spoj nemůže jet za vzor).
  Změřeno: napříč ~38 000 linkami tří firem nemá kombinaci „poznámkování zapnuté + vyplněná vzorová
  trasa" **ani jedna linka**, takže pozitivní formulace („jede přes") je na reálných datech
  neproběhaná - neslibovat ji bez zkoušky.
- Sonda pro výběr ukázkové linky se zacházkami = `AdVariantyProbe.ScanZachazky`.

## 2026-07-31 ✅ - `B33` popis dopravce po řádcích, i pro souhrnný JŘ (#13008)

Detail → changelog 2026-07-31 (8). Nové pole **`DOPRAVCI_SOUHRNNY_JR`**: řádek na dopravce × provoz,
před ním licenční číslo linky, za ním výčet spojů stlačený do rozsahů; bez uvozujícího „Zajišťuje".
`POPIS_DOPRAVCE` zůstalo nedotčené (visí na něm šablony i zlaté odtisky).

- **Blok nebyl gated na #13099, jak roadmapa tvrdila.** #13099 řeší **vytváření** souhrnných linek
  v EDISONu (dnes ručně v Excelu); tisk hlavičky na nich nezávisí. Seskupení podle
  **(licenční linka → dopravce → provoz)** obslouží obojí jedním kódem.
- **Licenční číslo se u souhrnné linky bere od spoje** („Zdroj spoje", `#`-pole 3 = `index~spoj`).
  Index je číslo linky doplněné nulami na 7 znaků → převod `$e(index,2,7)`, 1:1 legacy `cnvIndLin`.
  Bez zdroje (obyčejná linka) platí číslo té linky = dosavadní chování, zlaté odtisky beze změny.
- ⚠️ **Souhrnná větev je ověřená jen syntetickými vstupy** - souhrnná linka v testovacích firmách
  není; v datech KOVED je u spojů *návaznost*, ne zdroj spoje. Reálné potvrzení až od zákazníka.
- **Jméno pole podle účelu** (rozhodnutí Jan): popisuje scénář, ne obsah. Padlo to až ve chvíli, kdy
  byla souhrnná větev hotová - jinak by jméno slibovalo víc, než kód dělá.
- **Poučení k zlatým odtiskům** zapsané přímo do testu: u posunu digestu nejdřív porovnat jeho
  složky. Shodné počty + jiné md5 = přidané pole, ne změna chování (na tuhle past jsem najel).

## 2026-07-31 ✅ - `B32` tvar názvu zastávky jako volba sestavy (#13089)

Detail → changelog 2026-07-31 (7). Dvě volby v **nové skupině parametrů „Obecné"**: tvar názvu
(zkrácený = výchozí · typograficky upravený · plný CIS · plný CIS s okresem) a „Použít název pro MHD".

- **Kde se název skládá:** `NazevZastavky` čte `^JRZ` pole 7 („upravený název pro MHD",
  legacy `JRapiZst` dta13) a padá na pole 2 (CIS); pole 3 je okres. Jedna metoda = pravidlo se propíše
  i do nadpisu vývěsky, cíle a textů poznámek, protože všechny berou název odtud.
- **Proč parametr, ne vlastnost prvku:** mění to **hodnotu dat** (skládá server), a je to volba při
  tisku - klient chce běžně vylepšený tvar a pro export do soutěže plný CIS bez druhé šablony.
  Skupina „Obecné" (ne „Seznam zastávek"), protože název je i v nadpisu, cíli a poznámkách; konvence
  „skupina = prvek, parametry celé sestavy → Obecné" je zapsaná v agendě.
- **Nastavení per požadavek** leží v `^||TiskAd("nazev",…)`, aby se nemuselo protahovat všemi
  voláními jména (`NactiNastaveniNazvu` se volá po úklidu cache v `GetData`/`GetMockData`).
- **Typografické pravidlo jen na názvy zastávek**, ne na volný text - mezera za tečkou rozbije
  `www.idzk.cz`, a ten mají v poznámce k tarifu na každé lince IDZK. Před číslicí se mezera nedoplní
  (`rozc.0.9`, `č.1`). Zamčeno testem včetně toho, že dvojčárka se stahuje **před** typografií.
- **Oba parametry v allowlistu server-side parametrů** - jinak by změna v UI nespustila re-fetch
  a klik by byl no-op (past z `cache-api.md`).
- Ověřeno end-to-end na datech KOVED (`ZL_KOVED`, linka 0822995), ne jen jako čistá funkce.
  Zlaté odtisky se nepohnuly (výchozí = dosavadní tvar).

## 2026-07-31 ✅ - `B41` mrtvé UI odklizeno: co řídí parametr sestavy, není i v prvku

Detail → changelog 2026-07-31 (6). Tři přepínače v prvcích nic nedělaly, protože je parametr sestavy
přebíjel - a agendy JŘ ho posílají vždy. Z téhle dvojkolejnosti vznikla hlášení `B22`/`B24`.

Odstraněno z prvků: **Šipka směru**, **Projeté zastávky** (seznam zastávek), **Roztáhnout na výšku**
(mřížka odjezdů) - včetně tří fallbacků časové osy, které z panelu vypadly už dřív. Nahradily je
konstanty shodné s výchozími hodnotami parametrů agendy. Vzhled (barva/tloušťka/hrot šipky, barva
a čas u projetých) v šabloně zůstává a dostal nápovědu, kde se tisk zapíná.

- **Bezpečnost změny doložena, ne odhadnuta:** všech pět parametrů posílají obě agendy JŘ (AD i MHD),
  a vlastnost prvku se uplatnila jen bez dat - v tom stavu seznam kreslí zástupné řádky (šipka se
  nekreslí, projeté nemají co filtrovat) a mřížka vrací prázdný layout.
- **Bez migrace a bez bumpu verze** - odebrané klíče se při čtení ignorují a hodnota se stejně
  zahazovala (zapsáno v `template-schema-versions`).
- **Vedlejší nález:** distribuční AD šablona měla u mřížky napsanou časovou osu „Vlastní rozsah 4-22",
  ale agenda posílá „Jen hodiny s odjezdy", takže se ten rozsah nikdy netiskl. Kdyby ho distribuce
  měla mít, musí se změnit výchozí hodnota parametru v agendě, ne šablona.
- **Import PDF** si přestal zapisovat detekovanou časovou osu a šipku - stejně se při tisku nepoužily.
- **Pravidlo do budoucna:** když agenda pošle parametr, prvek tu vlastnost **nevystavuje**; vzhled
  patří do šablony, zapínání do parametrů.

## 2026-07-31 ✅ - `B38` rozdělení seznamu zastávek na oddíly na pokračování (#13097)

Detail → changelog 2026-07-31 (4). **Přeceněný blok: v roadmapě visel jako `L` gated na `B13`
s tvrzením „tentýž mechanismus". Není.** `B13` řeže **široké** sloupce linkové tabulky do
stránkových bloků (stránkovací engine, jiný prvek), `B38` rozlévá **řádky** seznamu zastávek do
oddílů vedle sebe **uvnitř rámce prvku** - engine do toho nemluví. Reálná velikost **M**, uděláno
samostatně.

Prvek dostal „Rozdělit na více oddílů" (1-3) a „Mezera mezi oddíly"; každý oddíl nese celou tabulku
znovu (záhlaví + všechny sloupce), řádky se dělí rovnoměrně, výška se počítá z nejdelšího oddílu.
Strop 3 je rozhodnutí Jan 31. 7. - víc oddílů zužuje sloupce tak, že se vývěska nedá přečíst.

- **Hlavní past:** šířky sloupců a automatické zmenšování textu se musí počítat ze šířky **oddílu**,
  ne prvku. Bez toho sloupce z oddílu vylezou (`ResolveColumnWidths`/`ComputeWidthScale` proto berou
  šířku parametrem).
- **Šipka směru pokračuje mezi oddíly** - v oddílu s aktuální zastávkou od ní dolů, v dalších od
  horní hrany. Jinak by končila v půlce seznamu.
- Vedlejší efekt refaktoru: výběr viditelných řádků (projeté, podmínka viditelnosti) a sestavení
  řádku jsou vytažené z kreslicí smyčky do metod, takže smyčka je čitelná.
- Aditivní vlastnost s výchozí hodnotou 1 → žádná migrace, nikomu se vzhled nemění.

## 2026-07-31 ✅ - `B34` zóny: doportovaná logika starého modulu (#13091 bod 4)

Detail → changelog 2026-07-31 (3). **Klíčové zjištění analýzy: nebyla to nová featura, ale
nedoportovaný kus starého modulu** - obě klientovy stížnosti (zóna jen z trasy linky · písmeno
místo čísla) legacy řeší, jen jsme to nepřevzali.

Doportováno 1:1 dle `JRapiExp10`: záskok na registr zastávek (`^JRZ` `$`-pole 11, u hraniční
zastávky i druhá zóna), odstranění písmen dle nastavení (globální `^JRSET("Definice","ZonaIdsDelZnk")`
přebité u linky `PATCH` - v JŘ pole „Zóny - tisk a export do JDF bez písmen"), hodnota `-`
= netisknout. Oddělovač více zón srovnán na čárku (starý modul), dosud lomítko.

- **Písmeno v kódu zóny nese přepravní systém**, ne zónu - do dat se předřazuje (`JRapi4hand`:
  ODIS → „O", IDSOK → „I"), legacy ho pro tisk maže (`delZnk^JRapiLinPrtPS2`). Tím je vysvětlena
  klientova druhá věta („písmeno místo čísla z číselníku").
- **Riziko gold parity změřeno, ne odhadnuto:** zlaté odtisky `CB_CSADAD` se neposunuly (firma
  zóny IDS nemá), paritní testy zón `CB_GWBUS`/`KV_DP` zůstaly zelené. Přidán test, který záskok
  dokazuje na živých datech a čte očekávanou hodnotu ze stejného globálu, takže nepadá na driftu
  zákaznických dat.
- **Nedělá se, dokud klient neodpoví** (visí v „Čeká na někoho jiného"): jestli má zastávka
  přednost i tam, kde trasa zónu má. Legacy dává přednost trase; přehození by nás rozešlo
  s ostatními výstupy (JDF, exporty), takže to není naše rozhodnutí.

## 2026-07-31 ✅ - `B30` vzhled seznamu zastávek (#13091a) + `B31` barva z dat u sloupců (#12997)

Detail → changelog 2026-07-31 (2). Dva bloky fronty KOVED jedním sezením, protože sahají do téhož
místa (seznam zastávek a nastavení jeho sloupců).

**`B30`** - šipka směru jde nastavit (tloušťka, velikost a tvar hrotu, styl čáry), přibyly svislé
čáry mezi sloupci a čas u projetých zastávek má volbu, jak se má chovat.

- **Vedlejší nález, opravený:** tloušťka šipky se zadávala v **pixelech**, takže v tisku z ní byla
  vláskovka - odtud klientovo „šipka je malá". Nově milimetry, výchozí hodnoty drží dosavadní
  vzhled na obrazovce. Milimetrové rozměry šipky i mřížky se škálují se šablonou (test).
- **Záporné časy u projetých zastávek** jsou důsledkem toho, že časy jsou kumulativní od vývěskové
  zastávky. Nově se defaultně skryjí. Řešeno v prvku, ne v agendě - je to volba zobrazení, data
  zůstala beze změny (zlaté odtisky nedotčené, 45/45).
- Tloušťku a styl mřížky dostaly **všechny** JŘ prvky (žijí na společném základu), zapnutí svislých
  čar jen seznam zastávek - ostatní je už měly.
- **Review 31. 7. přineslo dvě zjednodušení:** přepínač „Tučná šipka" zrušen (dělal totéž co
  tloušťka, jen hrubě → migrace šablon na 1.6.0) a volba u projetých časů zúžena ze tří na dvě
  („skrýt jen záporné" se na reálných datech chovalo shodně s „nezobrazovat"). Sjednocení čar
  a mřížky napříč **všemi** tabulkovými prvky vzniklo z téhož review jako blok `B40`.

**`B31`** - barva textu i pozadí sloupce se dá **zapsat textem**, takže tam jde zadat
`{BARVA_LINKY}`; dosud byl jen výběr barvy, který barvu z dat nabídnout neumí. Jeden sdílený editor
sloupců → pokryty zastávky, odjezdy, linková i dynamická tabulka. Nápověda o `{BARVA_LINKY}`
přidaná k barvám záhlaví a buněk (dosud ji měl jen rámeček, text a obdélník).

- **Roadmapa tvrdila něco jiného:** nález „Fáze B" z 30. 7. říkal, že u zastávek a odjezdů pole
  „Pozadí záhlaví / Pozadí buněk" v tabu Obecné **chybí**. Ve skutečnosti tam jsou, jen pod popisky
  „Barva textu / Pozadí" ve skupinách „Styl záhlaví / Styl datových buněk" (sdílený balíček polí
  stylu). Skutečná mezera byla jen ta jedna: nešlo zadat barvu **textem**. Tvrzení z roadmapy
  smazáno, ať se podle něj nepřepisuje něco, co funguje.
- **Zbytek `B31` je mimo návrhář** (viz roadmapa, „Čeká na někoho jiného"): dvě barvy linky
  (zvlášť text, zvlášť pozadí) potřebují druhou hodnotu v jízdním řádu.

## 2026-07-30 ✅ - `B22` „Hodiny od/do" v parametrech - vyřešeno opravou `B24`

Symptom: „Hodiny od/do" byly vidět, i když časová osa nebyla „Vlastní rozsah". Prohledání kódu ale
ukázalo, že **oba** povrchy (záložka Parametry i tiskový dialog) filtr `visibleWhen` aplikují včetně
přepočtu při změně pole, a výchozí osa je „Jen hodiny s odjezdy" → z kódu se to nedalo reprodukovat.
Proto se do toho **nesahalo naslepo** a čekalo se na reprodukci.

Po opravě `B24` (hodnoty a UI jedním průchodem) se problém **přestal projevovat** - Jan ověřil 30. 7.
včetně nejtvrdšího scénáře, tedy zavřít a znovu otevřít šablonu a podívat se, aniž na cokoli klikne
(to bylo jádro `B24`: hodnoty dorazily až po postavení formuláře). Byl to tedy projev téže příčiny.

**Poučení:** dvě hlášení mohou být jeden bug. Než se opravuje druhé, stojí za to ověřit, jestli
nezmizelo s prvním - tady to ušetřilo celou opravu.

## 2026-07-30 ✅ - `B2` zavírací kolo helpdesku dojeté

Zastávkový JŘ nemá otevřený zákaznický tiket. Zavřené 30. 7.: **#7947** (znění poznámky o prodloužení
spoje), **#7955** (potlačení auto-poznámky vlastním komentářem - ověřeno na reálných datech), **#7959**
(skrytí neobsluhované zastávky) a **#13048** (5 bodů KOVED; klient potvrdil i zmenšování textu v buňce,
což byl poslední otevřený bod). Detail měřených stavů → `customer-requirements §0.1/§0.3/§D1/§D2`.

Zbytek helpdeskové fronty jsou položky **čekající na někoho jiného**, ne práce: #10660 (zpřístupnit
build DPKV), TR2 (SK kódování na-znamení - kolega), #13048 bod 4 fáze 2 a TR8 (obojí čeká potvrzení
zákazníka).

## 2026-07-30 ✅ - `B23` atributy zastávky se čtou i z registru zastávek

Detail → changelog 2026-07-30 (4). Na vývěsce chybělo, že zastávka je **na znamení**. Atribut může být
na **dvou** místech - v trase linky (J-věta) a v registru zastávek (`^JRZ`, `$`-pole 8) - a my čteli jen
trasu. Změřeno, že **legacy čte obojí** (vypíše „x" u `CB_CSADAD` 0320680, kde je jen v trase, i u
`CB_GWBUS` 0000001, kde je jen v registru), takže to byla faktická vada výstupu, ne jiné znění.
`AtributyZastavky` teď vrací sjednocení; použito i v souhrnu „všechny zastávky linky jsou na znamení".

**Dopad měřen PŘED zásahem** (poučení z `B26`): v CB_CSADAD je pole registru prázdné u všech 5728
zastávek → tam se nemění nic a zlaté odtisky jsou v bezpečí; v CB_GWBUS přibyly 2 zastávky se „x"
a stránka 0000001/T má nově 10 poznámek **stejně jako legacy**. Znění zůstává naše („na znamení");
legacy má „zastávka pouze na znamení" - rozhodnutí Jan, neměnit.

## 2026-07-30 ✅ - `B25` legenda na rozdělené vývěsce + `B26` uzavřen jako falešný poplach

**Pravidlo (rozhodnutí Jan):** vývěska je **samostatná stránka**, takže vysvětluje jen značky, které
na ní reálně jsou. Ostatní kategorie to měly samy (plní se z vytištěných odjezdů), rozdíly tras ne -
počítají se per zastávka nezávisle na tom, které odjezdy stránka nese. `ProrezatLegenduRozdilu` zahodí
nepoužité řádky; **písmena se nepřečíslovávají**, takže táž značka znamená na všech stránkách téže
zastávky totéž (to byla původní obava v kódu - ukázalo se, že stabilita písmen a prořezání legendy si
neodporují). Ověřeno na rozdělených stránkách `CB_GWBUS` 501/0000001/T: označník „1" → 1 odjezd
a legenda jen `C`+bezbariér, označník „JET" → 3 odjezdy a legenda `B`,`E`,dny,omezení.
Na nerozdělených stránkách **no-op** (čísla inventury identická, zlaté digesty beze změny).

**`B26` byl falešný poplach.** Z hodnot `JET,1` jsem usoudil, že čteme špatné pole. Měření na firmě,
kde označníky fungují (`CB_CSADAD` 0320680), to vyvrátilo: J-věta `#3` nese `11` a W-trasa spoje pole 7
nese `22` - **pole čteme správně**. V GWBUS je v tomtéž poli text `JET`/`JETE`, tedy něco, co označník
není. Žádná heuristika „vypadá to jako označník" se nedělá: rozdělení je opt-in, po `B24` se jeho stav
zobrazuje pravdivě a titulek stránky nese „…, označník JET", takže je to samovysvětlující.
⬜ **Otevřená doménová otázka:** co `JET`/`JETE` v datech GWBUS znamená (kód turnusu? příznak?).

**Poučení:** „vidím v datech nesmysl" ještě neznamená „čteme špatné pole". Ověřovat na firmě, kde ta
featura prokazatelně funguje - rozhodlo to jedním měřením.

## 2026-07-30 ✅ - `B24` Parametr sestavy se v UI tvářil vypnutý, i když zapnutý byl

Detail → changelog 2026-07-30 (2). Uživatel tiskl jiné nastavení, než jaké viděl: uložené
`ROZDELIT_PER_OZNACNIK=true` tiše rozdělovalo vývěsku, takže ze stránky zmizely odjezdy jiného
označníku, ale ovladač v UI ukazoval vypnuto.

**Příčina:** persistované hodnoty se aplikovaly **dvěma cestami**. Otevírací flow dělal celou trojici
(per-agenda → šablona → přenačtení UI), cold-start fallback jen načetl hodnoty do dictu. Opraveno
jednou veřejnou cestou `ApplyPersistedPrintParamsAsync()`.

**Poučení:** když táž věc teče do **dat** a do **UI** dvěma voláními, jedno z nich někdo vynechá.
Diagnostika takových případů: přečti uložený blob
(`^ALVA.EDControls.UserData(IdFa,user,"TemplateDesigner.PrintParams",<AgendaCode>)`) a porovnej s UI -
tady to rozhodlo za pět minut po dnu hádání.

**Číslování bloků:** při této práci vznikly duplikátní `B14`/`B18`/`B19` (čísla už patřila jiné práci) →
přečíslováno na `B22`–`B26` a do pravidel roadmapy přidán bod 0 (číslo je globální, grepni i archiv).

## 2026-07-30 ✅ - Poznámky zastávkového JŘ: audit úplnosti + #7947 UZAVŘEN

> Tiket #7947 zavřen u zákazníka 30. 7. 2026. Odpověď obsahuje prosbu o vyzkoušení na jejich linkách
> (876401/876402), protože na jejich datech jsme ověřit nemohli - jen na lince, kde situace nastává.

Detail → changelog 2026-07-30, měřený stav → `customer-requirements §D1`.

**Dvě reálné chyby v generování rozdílů tras**, obě nalezené měřením proti starému modulu:
značka „spoj v zastávce nestaví" (`|`) se počítala jako obsluhovaná (tvrdili jsme „jede přes" tam, kde
spoj nestaví, a slévali skupiny spojů), a cache W-map se nerozlišovala podle linky (v tisku neškodné,
ale znehodnotilo to předchozí měření). Po opravách je grouping 1:1 se starým modulem.

**Znění věty o prodloužení rozhodnuto:** cíl jde na začátek věty a nese průjezd -
`jede až do zast. <konečná> přes <mezizastávky>`. Důvod: cíl je informace, kvůli které tiket vznikl,
a na konci věty ho legenda ořezávala; bez průjezdu splynou skupiny spojů lišící se jen cestou.
Je to **jediná** vědomá odchylka od starého modulu v celém tomto bloku.

**Kolize značky „H"** (bezbariérový spoj × rozdíl trasy) **se záměrně nechává** - ověřeno zavoláním
starého modulu, že tutéž kolizi má i legacy. Kdyby to zákazník nahlásil, oprava je malá (převzít z MHD
sdílený pool značek stránky) a stojí přebaselinování zlatého digestu 0365005.

**Poučení, které stojí za zapamatování:** dvě „vady" evidované 29. 7. neexistovaly - měření bylo
kontaminované vlastní chybou cache. Sonda, která čte víc linek v jednom procesu, není totéž prostředí
jako tisk jedné linky. A zlatý test je odtisk **našeho** výstupu, ne srovnání s legacy - na parity
otázky se musí volat starý modul (`ALVA.TISKMHD.Data`, větev JRMAIN).

## 2026-07-29 🧹 - Z backlogu vyřazeny dvě položky (rozhodnutí Jan)

- **Výkon MHD tisku** - vyřešeno 20. 7. (odstraněno drahé nepoužívané pole „první/poslední spoj"
  počítané per stránka přes legacy MHD2, ~7 s → ~0,6 s; detail v changelogu 2026-07-20). V roadmapě
  už jen viselo. **Poučení, které stojí za zapamatování:** původní hypotéza (memoizace názvů zastávek
  a dopravce) se **neukázala** - profiling ukázal build zastávek 0,02 s, tedy zanedbatelné. Další
  memoizace až kdyby reálně bolelo, ne preventivně.
- **Konverze legacy PostScript JŘ → šablona** - bude se řešit jinou cestou, teď bezpředmětné.
  **Jediné, co si z toho stojí za odnést** (ať se to nemusí odvozovat znovu): PostScript je
  **vyrenderovaný výstup** (layout + zapečená data), kdežto naše šablona je jen vzhled s daty zvlášť -
  takže to nikdy nebyl převod dat, ale **rekonstrukce layoutu**, tedy tentýž problém jako import PDF.

## 2026-07-29 ✅ - Plán řádků = R1 extrakce řádkového jádra (B11 = L‑3) - UZAVŘENO

Detail → changelog 2026-07-29 (tři části). Poslední chybějící vrstva sdíleného tabulkového jádra:
**druh řádku**. Element už nekreslí - popíše tabulku jako `List<RowSpec>` (druh · výška · buňky ·
pozadí · pera čar) a kreslí ji jedna smyčka `RowPlanRenderer.DrawRows`. Tím řádkové featury
(grouping, mezisoučty, rozdělení přes stránky) přestaly být per-element záležitost.

**Převedeny tři živé tabulky:** linková (prototyp - nejvíc druhů řádků: pás skupin dnů se sloučenými
buňkami, čísla spojů, platnost, negativní značky, zastávky s podřádky přípojů) · dynamická (záhlaví,
data se zvýrazněním i střídáním, souhrny) · zastávkový seznam. **U všech odtisk bit za bitem
identický** proti baseline sejmuté před zásahem.

**Tři věci si vynutila realita, ne návrh dopředu** (všechny jsou v kontraktu zapsané, protože každá
by se projevila až na vytištěném listu):
1. **Pořadí kreslení** pozadí → buňky → čáry (jinak pozadí buňky překryje čáru).
2. **Pera per-řádek** - záhlaví a datové řádky reálně používají různá (rámečkové × oddělovací).
3. **`RowSpec.Decorate`** = dokreslení vlastního obsahu do řádku (po buňkách, před čarami) - značky
   zastávky se kreslí po úsecích symbolovým a normálním fontem a v jedné buňce nejde míchat font
   (#7161 MHD parita). Úzká výjimka pro to, co buňka principiálně neumí.

**Vědomě mimo:** **mřížka odjezdů** (pivot - sloupce = skupiny dnů, řádky = hodiny) · **pevná
tabulka** (uspaná; kreslí svislou čáru za každým sloupcem včetně vnějšího a vrství dvě pozadí
u pevných řádků → sjednocení jí změní výstup, a jako uspaný element není ani v odtiscích;
rozhodnutí Jan: dorovnat až při jejím oživení, kde bude co proklikat, a kde se stejně bude měnit design).

**Odemklo `B13`** (rozřezání široké tabulky) - píše se jednou nad plánem řádků místo pětkrát
v per-element smyčkách. Předpokladem byla **záchytná síť na kreslení** (viz vlastní záznam níže).

## 2026-07-29 ❌ - B21 (varování „skupiny dnů sdílí sloupec") ZAVŘEN jako nerelevantní

Zadání pocházelo z doby **před** fází 2 SMO kolizí (23. 7.). Ta problém vyřešila **automaticky
na serveru**: dominantní typ drží nadpis, každý další kolidující typ dostane značku + legendu
„jede jen 31.12." (`PriraditZnackySmoSkupin`). „Kolize bez značek" tedy v datech nemůže nastat
a klient nemá jak kolizi poznat (u odjezdu je jen id sloupce a značky; 2. řádek nadpisu je běžný
druhý řádek titulku, ne příznak kolize). Doslovná implementace by chtěla nový signál na wire
(Caché zóna, nad S) a varovala by o vyřešené věci.

**Proč ani ta užší varianta ne** (rozhodnutí Jan 29. 7.): zbývající dva scénáře jsou **na straně
šablony a v náhledu vidět** - vypnuté „Zobrazit anotace" (zmizí VŠECHNY symboly, okamžitě patrné)
nebo chybějící legenda poznámek. To není tichá past jako dnešní opravené chyby (oříznuté spoje,
schovaný filtr hodin); je to vědomá volba, kterou náhled ukazuje. Navíc legenda může být vynechaná
záměrně (minimální vývěska) → plané hlášky, které lidi naučí ignorovat i užitečná varování.
Dodávaná výchozí šablona `zastavkovy-jr-ad.json` má legendu i `showAnotace: true`, takže z krabice
je to v pořádku.

**Zapsaný zbytek:** díra, kdy dva kolidující typy mají v číselníku **tutéž ruční značku** (stejný
symbol pro dvě platnosti + zavádějící legenda) - jediný scénář, který se opravdu nikde neprojeví.
Je to konfigurace číselníku (JŘ, ne designer) → zaevidováno v
[`rules/cache-business-logic.md`](../rules/cache-business-logic.md) u té metody.

## 2026-07-29 🧹 - Rozpuštěná hromada „Úklid a drobnosti" + odložené B19/B20

**Sekce byla kategorie, která lhala.** Z osmi položek: jedna duplikát, jedna průběžné pravidlo,
pět čekalo na zákazníka, zbytek nebyla „drobnost" (M–L). Kam se co podělo:

- **5 položek „na poptávku"** (MHD parita rozdělení sloupců / variant tras / zdroje označníku,
  exportní parametry per šablona, per-sloupcové zvýraznění řádků + barva textu / kurzíva pravidla)
  → [`customer-requirements §0.5`](customer-requirements-zastavkovy-jr.md). Není to nedodělek, je to
  práce s konkrétním zákazníkem.
- **Postupná unifikace dialogů** → [`rules/ux-conventions.md`](../rules/ux-conventions.md) jako
  průběžné pravidlo „sjednocuj při dotyku". Způsob práce, ne úkol.
- **Výchozí šablony MHD/linková** → smazáno, potřetí totéž (žije v „Produkční" #3 a `C4`).
- **Migrace `CellStyleEditor` na Caché formulář** → follow-up `B18` (bez deklarativních pravidel
  formuláře nejde - tri-state / reaktivní logika).
- **`B21`** varování „N skupin dnů sdílí sloupec bez rozlišovacích značek" (S) → zůstalo v roadmapě.

**Odložené rozhodnutím Jana 29. 7.** (mimo roadmapu, ne zrušené - kdyby se k nim vracelo):

- **`B19` Dark mode dokončit** (M–L) - `EDThemeDark` + tokenizace hotové 13. 7., plocha plátna už
  na tokenu; zbývalo chrome návrháře (papír, pravítka, záhlaví sekcí) + runtime přepnutí bez restartu.
  Začíná **produktovým rozhodnutím, jak v tmavém režimu vypadá papír** (bílý list na tmavém pozadí
  × ztmavený papír), ne kódem.
- **`B20` Diagnostika připojení** (M) - odlišit „server nedostupný (VPN)" od „není nakonfigurováno"
  + říct to při startu; patřilo sem i „okno naskočí až po ~3 s" při nedostupném připojení (dnes
  uživatel vidí prázdno a čeká). Odložený UX nález 13. 7.

## 2026-07-29 ✅ - Auto-fit tabulky na šířku (B5 = L‑1) - UZAVŘENO

Detail → changelog 2026-07-29. `TableLayout.ComputeWidthScale` = vodorovný protějšek výškové
`ComputeFontScale`; element kombinuje obě přes `MathF.Min` a aplikuje na šířky sloupců, výšky řádků
i fonty → tabulka dosedne na svou šířku místo **tichého oříznutí** (svislé varování šířku nehlídá).
Zbavuje zmenšování textu v buňce přetěžování (dřív jediné, co na šířku reagovalo → různé velikosti
písma vedle sebe; nově zůstává výjimkou pro odchylku). Kde: LineTable (skalární varianta - vlastní
výpočet šířek), StopList + DynamicTable (sdílené `ResolveWidths`), TableElement jen zmenšení šířek
(font-škálu nezná, uspaný). **DepartureGrid záměrně mimo** (pivot, málo sloupců). Behavior-preserving
dokud se tabulka vejde (pokryto testy). `TableWidthFitTests` (9). Proklikáno Janem.

**Hranice:** u linky se 100+ spoji je zmenšování slepá ulička (nečitelné písmo) - správné řešení je
rozřezání na stránkové bloky = `B13`. B5 byl předpoklad `B11` a poctivý mezikrok, který dá vzniknout
signálu „tady už zmenšování nestačí".

Při testování B5 vypadl **nález Jana** (tichý filtr hodin v mřížce odjezdů, opraveno týž den) -
nesouvisel s B5, detail v changelogu.

## 2026-07-29 ✅ - Base pole formulářů z jednoho místa (B4) - UZAVŘENO

Detail → changelog 2026-07-29. Sdílený `BaseElementFields.cls` (rámeček+rohy / pozice a velikost /
podmínky) místo ruční kopie v deseti formulářích; odchylky parametrem (výchozí rozměry, dynamická
výška, čára bez rohů). Spraveny nálezy: tloušťka rámečku 0-10 × 0-5 mm, **dynamická tabulka bez polí
rámečku**, rámeček u tří elementů v cizí skupině, rozdílný popisek výšky. Seskupení sjednoceno
(„Rámeček elementu" u všech; rozhodnutí Jan). **Korekce zadání:** C# strana byla už sdílená
(jedna definice v základní třídě) → reálně 10 × Caché + 1 × C#, ne 10 × 2; C# jen dostal shodné
názvy skupin. **Pojistka:** dva nové drift testy (base pole nesmí být opsaná ve formuláři · popisky
a skupiny musí souhlasit mezi Caché a C#) + rozbalování volání balíčku ve stávajícím testu klíčů;
výjimka pro klíč výšky padla. Ověřeno živě proti serveru na 5 strukturálně odlišných formulářích.

Zbytek §B4: krok „zapsat zúženou roli C# zrcadla" (formátovací lišta / více elementů naráz /
fallback) - zrcadlo **nelze odstranit**, ověřeno v přepínací logice panelu.

## 2026-07-28 ✅ - Rozvržení editoru stylu buňky (B6) - UZAVŘENO

Detail → changelog 2026-07-28. `CellStyleEditor` (sdílený UserControl, tab Sloupce/Styl v 6
dialozích) z rozbíjejícího se `WrapPanel` na dvousloupcovou mřížku ve stylu EDInputFormu (popisky
ve sdíleném sloupci přes `SharedSizeGroup`, prvky pod sebou, per-řádek Visibility). Tri-state /
placeholdery / reset beze změny, x:Name zachovány. Vizuálně ověřeno Janem. Budoucí krok (na poptávku):
migrace na Caché EDInputForm - přesunut do Úklid „Vzhled/téma".

## 2026-07-28 ✅ - Interní nálezy A1 (B16 + B17) - UZAVŘENO

Detail → changelog 2026-07-28. **B16:** mock data náhledu prefetch na pozadí + render jen z cache
(`TryGetCachedMockData`/`PrefetchMockDataAsync`), konec 2-3 s freeze při první kresbě, selhání
fail-soft do F5. **B17:** `TextStyle.ApplyScale` (font+padding, ne LineSpacing) použit všude -
schedule/table/dynamic elementy nově škálují paddingy, per-sloupcové/skupinové styly, `WidthOverride`,
Fixed buňky; `DataRowHeight` přesunut do `ScheduleElementBase` (jednou, ne dvakrát); procenta se
neškálují. UI/UX výjimky (`Click=`, 5 tabů TableEditoru) zapsány do `architecture.md`. Testy
`ApplyScaleTests` (4), .NET 409/409.

## 2026-07-28 ✅ - Roadmap bloky B8 + B9 uzavřeny (varianty tras + vývěska per označník)

B8 (dokončení variant tras): server hotový a vizuálně ověřený 27. 7., #9036 časová osa přes půlnoc
ověřena na 345070 (JH) a resolved 28. 7. B9 (#8828) resolved vč. titulku stránky. Zbytky přesunuty:
MHD parita variant/označníků → roadmap „Na poptávku"; per-písmeno výběr a další follow-upy →
customer-requirements §0.4. Pozn. o `ServerSideParams` allowlistu → `rules/cache-api.md`.

## 2026-07-28 ✅ - Stanovišťový balík UZAVŘEN: #13048 i #8828 resolved, doladění a slovník

Detail → changelog 2026-07-28. Odpovědi odeslány a oba tikety **resolved 28. 7.** (KOVED #13048
všech 5 bodů; TRANSDEV #8828 vč. titulku „…, označník X" a označníku ve vývěskovém řádku rozdělené
stránky). Doladění z Janova testování + nezávislé revize: jednotný slovník **„označník"** (kanon
`ux-conventions §8` + `CLAUDE.md`), parametr přejmenován na „Samostatná stránka pro každý označník",
držení logické stránky náhledu při re-fetchi + resync comboboxu, oprava bool parametrů na wire
(„True" vs „true" - kvůli ní nikdy nezabral žádný bool parametr GetData vč. TR2), spoj bez označníku
při rozdělení na stránku nejčetnějšího (ztráta odjezdu), gate duplikace řádků bez variantních
sloupců. Testy: Caché 45/45 (+TestPismenaVariant, +TestSplitTitulekARadek), xUnit +10 (CellShrink,
PreviewPageMatch, gate). Follow-upy jen na vyžádání - viz vršek roadmapy.

## 2026-07-27 ✅ - #8828 (TRANSDEV) vývěska per označník - UZAVŘENO (= krok 2 / B9 stanovišťového balíku)

Detail → changelog 2026-07-27. Tiskový parametr „Rozdělit vývěsku per označník" (skupina „Seznam
zastávek", default vyp). Server (`ZastavkoveJrAd`): vývěska obsluhovaná z N označníků → N stránek,
`NacistOdjezdy` filtruje spoje per označník (per-spoj sloupek = W pozice 7, fallback J-věta),
`NASTUPISTE` stránky = její označník. Jen označníky se ≥1 spojem. Nezávislé na duplikaci řádku (#13048
bod 4). **Gold beze změny** (default vyp = byte-identický) + `TestSplitPerOznacnik`. Ověřeno 0320680
(ČB aut.nádr. → označníky 11/22, odjezdy 5+1; 53→55 stránek). **Tím je stanovišťový balík dokončen v1.**
Follow-up (na vyžádání): členění exportu na soubory per označník; titulek stránky s označníkem.

## 2026-07-27 ✅ - B6 část (a): šev mezi plátnem a náhledem - UZAVŘENO

Detail → changelog 2026-07-27 („Šev mezi plátnem a náhledem"). Čtyři místa se stejnou rolí (plocha
kolem plátna / kolem náhledu / pruh stránkování / plátno kreslené Skiou) měla barvu z různých zdrojů,
z toho tři natvrdo - sjednoceno na existující token `EDTheme.Bg.Canvas` + `Designer/CanvasTheme` jako
jediný zdroj pro Skia stranu (napojený na `ThemeService.Applied`). Děliče ztratily tečkovaný focus
rámeček. Vizuálně ověřeno Janem. Chrome návrháře (papír, pravítka, záhlaví sekcí) vědomě ponechán
pevný → patří do položky „dark mode". **V B6 zbývá část (b)** - rozvržení `CellStyleEditor`.

## 2026-07-27 ⛔ - B1 pojistka proti přepsání systémové šablony - ZRUŠENO (není problém)

Blok vznikl jako **domněnka** při řešení #13013, ne z nahlášeného případu: systémová šablona se
z `.exe` nalévá do MLINE při každém startu, takže by starší `.exe` mohlo vrátit novější kurátorovanou
šablonu zpět. Prověřeno s Janem: šablonu otevíral kolega **starou verzí na devu**, ne provozní instalací;
zákaznické (firemní) šablony seed nikdy nesahá a nová verze jde do vydání běžným publishem, takže riziko
nemá kde vzniknout. Publish zůstává součástí normálního vydávacího postupu, ne roadmapovou položkou.
Kdyby se přepsání někdy reálně objevilo, správné řešení je **serverová** kontrola v `Sablony.SaveTemplate`
(distribuční zápis) s **vlastním číslem revize obsahu** - verze formátu JSON na to nestačí, ta se při
překurátorování šablony nemění. Nezavádět klientský gate: starší `.exe` ho neobsahuje, tedy nezastaví.

## 2026-07-27 ✅ - B3 drobné korektnostní nálezy - UZAVŘENO

Detail → changelog 2026-07-27 („Drobné korektnostní nálezy").

- **(a) Dialog pevné tabulky mazal per-buňkový `Format`/`Style` + barvu pozadí řádku** (mezi-model
  `RowVM`/`CellVM` je nepřenášel). Bolelo u tabulek ze šablony agendy - Caché posílá `format` a barvy
  per buňku. `TableElement.Clone()` byl v pořádku už dřív (hlídá `CloneCompletenessTests`), problém byl
  jen v cestě přes dialog.
- **(b) Ruční kreslicí/měřicí cesty JŘ elementů ignorovaly řez písma** (typeface natvrdo Normal/Bold).
  Sjednoceno na kaskádu novým helperem `ScheduleElementBase.GetTypeface(ResolvedTextStyle)` - legenda
  poznámek (vč. výpočtu výšky, kde se měření rozcházelo s renderem), měření + wrap záhlaví skupin dnů
  u odjezdů, designerové placeholdery, měření šířky značek zastávky. Legenda navíc začala respektovat
  výchozí font dokumentu.
- 8 nových xUnit (round-trip řádku tabulky, řez z kaskády, výška legendy reagující na tučný řez).
  Blok B11 tím ztratil závislost na B3.

## 2026-07-27 ✅ - KOVED #13048 stanovišťový balík krok 1: duplikace řádku per stanoviště - UZAVŘENO

Detail → changelog 2026-07-27. 4. volba comboboxu „Více nástupišť" (`RowPerPlatform`): zastávku
obsluhovanou z více stanovišť rozvine na N řádků (jeden per stanoviště) na místě v trase, časy jen
variant daného stanoviště. Klientská expanze (`ExpandPlatformRows`) nad aditivním serverovým polem
`NASTUPISTE_VARIANT` (nástupiště per varianta, W pozice 7) - identita variant se neměnila (čistší než
původní plán, gold jen re-baseline). Vývěska/jednostanovištní = 1 řádek. Ověřeno vizuálně na 0320680
(Autobusové nádraží → stanoviště 1 + 8). 6 xUnit + Caché `TestNastupisteVariantEmit`. Známé omezení:
nástupiště z reprezentativního spoje varianty (plné rozlišení = stanoviště do identity variant, vědomě NE).
**Tím jsou všechny body #13048 hotové v1; ze stanovišťového balíku zbývá #8828 (B9).**

## 2026-07-23 ✅ - MHD datová parita na složitých linkách (KV_DP 1073) - UZAVŘENO

Detail → changelog 2026-07-23 (4 záznamy) + artifact „Skupiny dnů na vývěsce - analýza KV_DP 1073".
Celý track od diagnostiky po ověřený stav v jednom dni; vizuálně odsouhlaseno Janem, s kolegou
rozhodnut design „AD vzor".

**Výchozí problém:** `ZastavkoveJrMhd` na složitých linkách neodpovídal legacy (KV_DP 1073:
zastávky 2× = 1317 vs 693, odjezdy +48 %, chyběly rozdílové poznámky). Gold pinoval jen CB_DP.

**Kořeny + fixy (vše commitnuto):**
1. **SMO sloupce** (2026-07-20): typ provozu → tiskový sloupec dle `SMOID` (SMO1-4), typ bez SMO
   se netiskne; sloupec per typ zrušen. `SmoSloupec` + klíč odjezdů `smoCol`.
2. **Příznak „netisknout na zastávkovém JŘ"** (`TisknoutZastavkovyTam/Zpet` per bod trasy): skrývá
   variantní větve v StopListu i sběru atributů (kromě aktuální zastávky); `TisknutaTrasaBitmap`
   (obsluha bez skrytých bodů) jde do alg. 1001 → zastávky 693=693 1:1, texty poznámek správně.
3. **SMO kolize v datech KV** (4 typy → SMO4: prázdniny/24.12./25.12.+1.1./31.12., 210 spojů):
   legacy ztrátově přepisoval (nadpis „poslední vyhrává", minuty kolidují na pozicích) → bit-parity
   vědomě NENÍ cíl. **Rozhodnutí (Jan + kolega): AD vzor** - auto-značky „jede jen …" z volné NZ
   řady (ruční `NegativniZnacka` typu má přednost) + legenda; nadpis = dominantní typ + výčet
   ostatních na 2. řádku; TD nic neztrácí (dep 6352 vs legacy 4295 = vědomá nadmnožina).
4. **Pořadí iterace odjezdů = legacy** (typ provozu → čas): písmena rozdílových poznámek A–K 1:1
   (texty i písmena, `NotesProbe` 11/11). Gold digesty re-baseline (jen pořadí řádků v JSON).
5. **SKUPINY_DNU = všechny SMO sloupce linky i prázdné** (layout konzistentní napříč stránkami);
   **„Max skupin" default 3 → 0** + migrace šablon **1.4.0** (skrytý strop ořezával 4. sloupec).
6. **Rozsah poznámky spoje „od–do bodu"** (`TiskJRMHDZastavkoveBodOd/Do`, 1:1 legacy vč. zpět
   kvirku): „W" se tiskne jen na stránkách v rozsahu.
7. **Dedup buněk uzavřen jako bezpředmětný**: po oznámkování se buňky různých skupin vždy liší
   značkou; duplicity v rámci skupiny tiskne i legacy (CB parita).

**Trvalé pojistky:** parity test `TestParita1073KvDpSmoKolize` (zastávky 1:1 + poznámky pokrývají
legacy [`covered`] + odjezdy nadmnožina) v pre-push; diagnostické sondy `MhdLegacyOracle.SmoProbe`/
`StopProbe`/`NotesProbe`. Vedlejší poučení (paměť): server může mít novější kompilaci než repo -
před kompilací needitované `.cls` změřit baseline.

**Otevřené zbytky přesunuty do backlogu:** SMO fáze 1 (varování v designeru při kolizi bez značek),
fáze 3 (parametr „Rozdělení sloupců" pro MHD - sjednocení s AD).

## 2026-07-23 ✅ - Ukládání velkých šablon (blob po kusech) - dokončeno + ověřeno

Detail → changelog 2026-07-23 (+ 2026-07-21 mechanika). Zákazník neuložil šablonu s velkým inline
obrázkem (`500 <SYNTAX> zStreamToGlobal`). **Skutečný limit = délka řádku při příjmu (~32 656 znaků),
ne max string 3,64 MB** (původní diagnóza mylná → první chunking na kusy po 1 M znacích nepomohl). Fix = velikost
kusu `1 000 000 → 20 000`. Blob jde po kusech (`dataChunkCount` + `data1..N`), server ukládá jako uzly
`Data(n)`, nikdy neslepuje; zpětně kompatibilní, bez migrace. Ověřeno naostro (reálný obrázek round-trip
+ živý probe save→load→delete). 16 unit testů vč. regresního strážce velikosti kusu. Úložná strategie
zůstává inline Base64 (self-contained); binární úložiště přes `BinarySave`/GUID vědomě odloženo.

## Session 2026-07-15 ✅ - Proces vývoje + zákaznická wiki + UX úklid

Detail → changelog 2026-07-15.

**Proces (pasivní pravidla v CLAUDE.md):** vývojová pipeline (agent jede celý cyklus z jednoho
zadání, chytré gatování commitu) · výchozí zdroj úloh = roadmapa · konvence komunikace (srozumitelné
termíny, popis z uživatelského hlediska) · wiki mapování (`.claude/wiki-map.json` allow-list) +
TipTap-safe konvence.

**Zákaznická wiki:** 3 prolinkované články (příručka 569 / IT 570 / novinky 571), seedy v repu =
source of truth. **Release track uzavřen** (verze v produkci, e-mail se nedělá, pre-version-checklist retirován).

**UX úklid:** snap na čtverec při resize · empty-state panelu „Pole agendy" · přesnější hláška DB
(„nejsou databázová pole" místo „nedostupná") · EDList dvojklik = toggle + Shift výběr rozsahu
(sdílené, pro všechny seznamy) · Save dialog nedostupné tlačítko místo popupu · sjednocení
pořadových šipek na ▲▼ (TableEditorDialog ↑↓ → ▲▼, zbytek appky už měl ▲▼).

**Tiskové parametry per šablona (obsahové):** hodnoty obsahových parametrů (časová osa, rozdělení
sloupců, šipky…) teď patří šabloně - sdílené mezi uživateli, priorita nad per-agenda, ukládají se
automaticky s šablonou (Ctrl+S, dirty na user změnu). Aditivní `Template.PrintParameters`, fallback
na agendu u starých šablon; export pole zůstávají per agenda (odloženo). Cesta k funkčnosti si vyžádala
opravu cold-start časování (`RefreshFieldsList` → `Task`, otevírací flow awaituje schéma) + bezpodmínečné
přenačtení parametrového tabu po načtení hodnot (`RefreshParamsUiAfterLoad` - tab se jinak držel defaultů).

**Vědomě NEdělat (rozhodnuto):** „jednotný resize grip" (legacy, nejasné) · toolbar text→ikony
(písmenka záměrně ponechána - u PDF/PNG jasnější než ikona) · footer pattern dialogů (žádné
konkrétní případy) · samostatný splash (start je běžně rychlý; 3 s jen při nedostupném připojení
→ sloučeno s odloženým bodem o připojení).

## Session 2026-07-11 ✅ - Dvouosý model agend: Agenda × Varianta („typ šablony vs agenda")

Rozhodnutí + implementace bodu „🧭 K rozmyšlení" (podnět kolegy). Legacy analýza (RG strom
agenda→oblasti→sestavy; TiskMHD/TiskJR = typ jako aplikace + `VolanoZ` přepínač zdroje) →
**Agenda** = nejvyšší úroveň (Zastávkový JŘ / Linkový JŘ / Faktury / Obecná; JŘ povýšené, budoucí
RG agendy přibudou vedle), **Varianta** = datový zdroj (AD/MHD = legacy `VolanoZ`). Duální role
MHD/AD (agenda provozních tisků × varianta JŘ) je legacy-věrná. Kombinované kódy
(`ZASTAVKOVY_JR_AD`), JSON `agendaCode`, DB index, preference i EDISON launch **beze změny**.
Caché `BaseAgenda`: `AgendaVariantCode` + osy `Agenda`/`Variant`(+labely), registr wire aditivní;
.NET `AgendaRegistryEntry.DisplayName` („Zastávkový JŘ · AD") → chip, titulek, seskupený
ChangeAgendaDialog, filtr pickeru. 340/340. Detail → changelog 2026-07-11.

**Uzavřeno po uživatelském testu (2026-07-11):** chip explicitně „Agenda: X · Varianta: Y";
Ctrl+O sloupce **Agenda + Varianta** jako dvojice hned za názvem (server-side resolve labelů,
`EDList.Sablony.ResolveAgendaVariant`). **Strom v přehledu šablon = vědomě NEdělat teď** -
dva sloupce + dvouosý filtr při současném počtu šablon stačí; strom (EDList grouping / TreeView)
otevřít až s růstem počtu šablon (Fáze F / governance), jako novou položku.

## Session 2026-07-10 ✅ - Imposice N-up (2×A5 → A4) - roadmap bod č. 1 (v1 uzavřena 2026-07-11)

> **v1 uzavřena po uživatelském ověření 2026-07-11** (A5 naležato → 2×1 na A4 na výšku, N/2 listů,
> sekvenční plnění dle očekávání). Rollout opt-in: default „Vypnuto" = dnešní chování, uživatelé
> zkouší dobrovolně, dodělky dle zpětné vazby. Případná v2 (brožura/duplex, mezery/spadávka,
> jiný styl značek) se otevře až s knižním JŘ nebo na poptávku - nová položka, ne reopen této.

Skládání logických stránek na fyzické listy při PDF exportu - vlastnost výstupu, ne šablony
(designer/náhled beze změny, šablona zůstává A5). `Models/Layout/SheetLayout.cs` (`SheetComposer`,
čistá vrstva bez SkiaSharp) + exportní pole `__EXPORT_SHEET*` v PrintDialogu (vzor `__EXPORT_SPLIT`,
persistence per-agenda) + imposiční větev `RenderMultiDataToPdf` (list = 1 PDF stránka, `RenderPage`
s offsetem). Módy: formát listu A4/A3 × orientace, mřížka AUTO (největší 1:1 fit) / explicitní,
opakování stránky (táž vývěska N× pro řezání), ořezové značky. Aplikováno v PrintDialogu (mock +
ostrá data + split módy) i exportu z hlavního okna. 336/336 vč. PdfPig end-to-end smoke testů.
**Odloženo na v2 (knižní JŘ):** brožura/duplex řazení stránek. Detail → changelog 2026-07-10.

## Session 2026-07-10 - MHD-port zbytky uzavřeno (na poptávku / N/A)

Helpdesk featury hotové pro AD agendu, ne pro self-contained MHD modul (`ZastavkoveJrMhd`, `^ALVA.MHD.*`,
používají jen DPCB/DPKV). Odstraněno z aktivní roadmapy - žádný driver.

- **D1** (#7947, „jede až do zast. X") - AD ✅; MHD ⬜ **na poptávku** (jen kdyby DPCB/DPKV měli spoje za základní trasu).
- **D5** (#7161, skrýt legendu „× na znamení" když značka vypnutá) - AD ✅; MHD ⬜ **na poptávku** DPCB/DPKV.
- **D2/D3** (#7955, ruční komentář přebít auto / NZ bug) - **N/A pro MHD**: ruční poznámky jsou AD-only
  (`^JRL`), MHD modul (`^ALVA.MHD.*`) to úložiště nemá. Portovat až kdyby MHD dostal vlastní poznámky.
- Detail: `customer-requirements-zastavkovy-jr.md §D1/D2/D3/D5`.

## Session 2026-07-10 ✅ - AD-interní (legacy-analýza parita) uzavřeno

Přehodnocení AD-N bodů (původ = legacy analýza dialogu „Nastavení pro tisk linky", ne helpdesk) proti kódu.
Většina byla už hotová, jen visela stará zmínka. Podklad kde data jsou: `ad-data-mapa.md`.

- **AD-N3** (řazení poznámek dle délky) ✅ - `NotesLegend.Razeni` = `DleDelkyVzestupne/Sestupne` (descriptor „Řazení").
- **AD-N4** (omezení „nejede" na nový řádek) ✅ - legenda renderuje každou poznámku na vlastní řádek (per-row).
- **AD-N13** (víc variant jízdních časů) ✅ - = L1 multi-varianta TrasaMin (2026-07-02, dynamické variantní sloupce, `ShowAllRouteVariants`).
- **AD-N15** (prázdninové dělení sloupců) ✅ kód (`ROZDELENI_SLOUPCU`, FTL 1/3) - pilot-verify na datech s prázdninovými NZ.
- **Legenda na víc sloupců** ✅ - `NotesLegend.Columns` (1–4) + `WrapSmer`.
- **Vědomě neděláno:** AD-N5 (`MHD_NEZNZ123` - dormant, pole prázdné ve všech firmách), AD-N11 (cizojazyčné poznámky - bez poptávky).
- (AD-N14 = přípoje → linkové JŘ, jiný bucket.)

## Session 2026-07-10 ✅ - TR4: fixed-then-shrink výška řádku StopListu + sdílený helper

TRANSDEV 3. režim výšky řádku. Detail → changelog 2026-07-10. .NET 316/316, rendering-only.

- StopList: pevná `DataRowHeight` se zmenší při přetečení (default auto beze změny) = fixed-then-shrink.
- Sdílený helper `ScheduleElementBase.ScaledDataRowHeight(fontPt, factor)` - pravidlo v jednom místě;
  StopList + DepartureGrid ho sdílí (dřív drift: B2 opravil DepartureGrid, StopList lagoval). DepartureGrid
  přepnut na helper (behavior-preserving). Malý extrakt; plná row-layout unifikace = krok C.

## Session 2026-07-10 ✅ - TR3: nástupiště jako sloupec zastávky (AD + MHD)

TRANSDEV požadavek (SK legislativa). Detail → changelog 2026-07-10. Caché 33/33, .NET 316/316.

- Per-zastávka `NASTUPISTE` do ZASTAVKY: AD z J-věty #3 (nastTam:nastZpet dle směru), MHD z `Oznacnik`.
- `StopFields.Nastupiste` + StopList sloupec „Nástupiště" (opt-in, default skrytý); parse + GetCellText; zámek v ColumnKeysTests.
- Bez mocku/hardcoded dat - prázdné kde označník není (standardně jen terminály). Ověřeno reálně 0320680 (6 zastávek).
- Gold re-baseline AD 2 + MHD 3 (counts identické, jen md5).

## Session 2026-07-10 ✅ - Audit A-AD + B2 (uzavření kontraktu skalárů + overflow)

Dokončení bloku A (AD strana) + B2. Detail → changelog 2026-07-10. Caché 33/33, .NET 316/316.

- **A-AD** - `SMER` + `NASTUPISTE` do `ZastavkoveJrAd` GetData+schéma (AD analog A1/A2); `NEJEDOU_SPOJE`
  deprecated. AD mock jede přes `BuildStrankaZastavky` → automaticky. AD gold re-baseline 2 linky (counts
  identické, jen md5). **Kontrakt skalárů sjednocen napříč MHD/AD/Linkový.**
- **B2** - DepartureGrid scale i overflow ze stejné skutečné výšky řádku (vč. DataRowHeight); odstraněna
  výjimka co nezmenšovala fonty → konzistentní auto-shrink do fit (rozhodnutí uživatele). Bez gold dopadu.

## Session 2026-07-10 ✅ - Audit infra batch (B3, B4, B6, B7)

Robustnost/extenzibilita mimo kolizní zónu paralelní práce. Detail → changelog 2026-07-10.
Build 0 chyb, TemplateDesigner.Tests 316/316 (+11 z B4).

- **B7** - první `GetSchema` (R1) na cold cestě přes `Task.Run` (warm sync), `IsSchemaCached`; `GetAvailableSkupiny` cache. Nulový caller ripple.
- **B4** - `ElementRegistrationDriftTests`: `[JsonDerivedType]` → factory type-match, allowlist uspaných `{Table}`.
- **B3** - `CapabilityCatalog` data-driven přes `AgendaCapabilities`/`ElementKind` (zná LineTable); `GenericTypes` Table→DynamicTable; FriendlyName +LineTable.
- **B6** - reset stránky: sdílené konstanty property-path + log chybějícího klíče; `async void`→`async Task`+try/catch (R2).

## Session 2026-07-10 ✅ - Audit blok A: kontrakt skalárů (MHD strana)

Sjednocení skalárního kontraktu zastávkového JŘ dle vzoru linkové agendy. Detail → changelog 2026-07-10.
MHD strana + schéma + mock hotové; AD strana (SMER, NASTUPISTE) odložená (roadmap, po `PozitivniZnacka`).

- **A1 (MHD)** - `POPIS_SMER` = název konečné (dřív T/Z); nové pole `SMER` = T/Z. Konečná = poslední zastávka směru.
- **A2 (MHD)** - nový `PRVNI_POSLEDNI_SPOJ`; `NEJEDOU_SPOJE` ponecháno duplicitně (deprecated ve schématu).
- **A3** - MHD mock doplněn (SMER, PRVNI_POSLEDNI_SPOJ); POPIS_SMER mock je nyní pravdivý.
- **A4** - schéma: AD deklaruje `POPIS_ZASTAVKA_CIS`/`POPIS_SMER_CIS` (AD-only); `VARIANTY_TRAS`/`TRASA_MIN` wire-only doc.
- **MHD gold re-baseline** 3 linky (counts identické, jen md5 - struktura zachovaná). Determinismus ověřen.

## Session 2026-07-10 ✅ - Audit před produkcí: bezpečný sweep (bloky B/C/D)

Nízkorizikové nálezy auditu 2026-07-09 (behavior-preserving / aditivní / docs). Ověřeno: build 0 chyb,
Caché 33/33, .NET 898/898. Detail → changelog 2026-07-10. Otevřený zbytek (blok A kontrakt, B2/B3/B4/B6/B7,
C-b/C-e, C-f export filtr, D2, D3 rename) zůstává v roadmapě.

- **C-a** - symetrický `k ^||TiskMHD`/`^||TiskAd` za catch (úklid working global i po chybě).
- **C-c** - `AgendaParams.FetchParamsJson` zachovává `ERR` (save/restore okolo resetu counteru; nested chyba se už neztratí).
- **C-d** - `GetParameters` bez zbytečného `$$$FillParams` (ZastavkoveJrAd + LinkovyJrAd).
- **C-f (část)** - `q $$$OK` → `ret $$$OK` (ZastavkoveJrMhd + ZastavkoveJrAd).
- **B1** - DepartureGrid layout fingerprint doplněn o 7 layout properties (mrtvé vlastnosti v náhledu opraveny).
- **B5** - F5 refresh inkrementuje `_previewDataGeneration` (guard proti stale in-flight fetch).
- **D3 (drobné)** - PrintDialog `EDStrings.Close`; AI API klíč `PasswordBox`; AI debug JSON do temp; komentář k prázdnému catch.
- **D1** - tabulka zkratek: sekce = `Ctrl+Shift+↑/↓`, doplněn Shift/Ctrl krok elementu.
- **D4** - roadmap font 42b/C2 přeformulován (zastávkový hotový, zbývá linková část).

## Session 2026-07-09 ✅ - Bug: změna velikosti/orientace stránky = reset s potvrzením

Změna velikosti/orientace stránky se dřív snažila obsah „zachovat", ale jen napůl - přeškálovala
jen výšky body sekcí, pozice/šířky elementů nechala být → elementy visely mimo stránku, překrývaly se.
UX rozhodnutí (potvrzeno s uživatelem, živě ověřeno): změna rozměrů = **čistý reset**, konzistentní
se změnou agendy. Detail → changelog 2026-07-09.

- `ElementEditDialog.FinalValues` - snapshot hodnot polí po OK (čtení zamýšlených rozměrů bez vykonání commandu).
- `OpenPropertiesForPage` detekuje změnu `PageSize.Width/Height` (epsilon 0.1 mm) → reset; jinak běžná undoable změna (okraje/kultura/font).
- `ConfirmPageResetAsync` - vždy se ptá (i u uložené šablony → řeší tichý wipe); dirty = Save/Discard/Cancel. Confirm **před** mutací → Storno = šablona netknutá.
- `ResetForPageChangeAsync` - čisté plátno s novou velikostí, zachová agendu, převezme velikost/okraje/kulturu/font z dialogu. `ReplaceWithBlankTemplate` + `configure` callback.

## Session 2026-07-09 ✅ - Governance šablon v1: doručení systémové šablony (embed → MLINE → picker)

Hybrid model (systémové M-line + zákaznické per-firma) - v1 způsob, jak zákazníkovi dodat výchozí šablonu,
aniž se sáhne na jeho vlastní. UX skořápka finální (nepřeučovat), backend (embed) vyměnitelný za budoucí kanál.

- **Krok 1** (`4770a58`) - kurátorovaná výchozí **zastávková AD** šablona (`dotnet/SystemTemplates/zastavkovy-jr-ad.json`),
  základ z dev artefaktu, pročištěno (název/popis, stabilní id, vyprázdněné test defaulty).
- **Krok 2** (`0a885af`) - `Sablony` API podpora větve **MLINE**: `EDList.Sablony.LoadList` sjednotí firma + MLINE
  (sloupec „Zdroj"), `API.Sablony.LoadTemplate` bere `sourceIdFa`, `SaveTemplate` `targetIdFa`; `CacheTemplateService`
  přetížení + konstanta `MlineIdFa`.
- **Krok 3** (`fddcea7`) - picker **2 sekce** (Zdroj = M-line/vlastní), systémové **read-only** (Přejmenovat/Smazat
  zakázáno); otevřít systémovou → načte z MLINE + `_loadedFromCache=false` → `Ctrl+S` = „Uložit jako" do firmy (nový GUID).
- **Krok 4** (`78af151`) - **embed + seed**: `SystemTemplates\**\*.json` `<EmbeddedResource>` do .exe;
  `SystemTemplateSeeder` při startu nalije do MLINE (fire-and-forget, idempotentní přes stabilní `Template.Id`,
  deserialize projde migratorem → aktuální verze). „Poslat šablonu ve v1" = přidat JSON + rebuild/publish .exe.
- **Zámek MLINE server-side** (`8573547`, governance bod 2) - `SaveTemplate` odmítne zápis do MLINE bez
  `distribution=1` (seed ho nastaví, UI ne). Read-only vynuceno serverem, ne jen klientská iluze. V1 seed běží
  klientsky → příznak je guard proti normálnímu/omylnému zápisu, ne krypto auth (to až s kanálem mimo .exe).
- **Bugfix** (`7bde94c`) - `DesignerState` element cache (klíč = Guid sekce) se invalidovala jen při editaci;
  systémová šablona s pevnými Guidy → po Uložit jako + reopen MLINE stará cache prosákla (posunutý element,
  maskováno restartem). Setter `CurrentTemplate` teď invaliduje cache na každý Open/New/Import.

**Vědomě odloženo** (zůstává v roadmapě): samostatný distribuční kanál mimo .exe (bod 3), další agendy
MHD/linkový (C4/44b), kategorie/popis šablony v pickeru. Per-instance přiřazení + ECL právo „pouštět
designer" se řeší jinde (mimo TD); e-mail/tisk = ad-hoc na poptávku → neevidováno.

## Session 2026-07-08 ✅ - Tabulkové jádro: levné švy před v1 (pětice #1–#5)

Z analýzy připravenosti tabulkového jádra (4 agentní dimenze) - zavřít levné seamy, ať další tabulkový
element/featura neplatí daň znovu. Detail v changelogu (2026-07-08).

- **#3** (`cc11285`) - reflexní test úplnosti `Clone()` napříč všemi `TemplateElement` potomky (mutace →
  serialize-compare; nový element pokrytý automaticky). **Chytil reálný bug:** `CopyScheduleBaseProperties`
  nekopírovala `DataRowHeight` → ztráta při Ctrl+D/undo u LineTable/NotesLegend. Fix v base helperu.
- **#5** (`4c3ed73`) - `SkiaHeightCalculator` deleguje výšku `TableElement` na `CalculateHeightFromData`
  (zrušen inline special-case; nový tabulkový element už nepřidává větev do switche). Behavior-preserving.
- **#4** (`3beee54`) - `AggregationResolver` agreguje nad **výrazem** (`SUM({CENA}*{POCET})`), ne jen holým
  polem. Zpětně kompat.; odemyká per-group subtotaly počítaných sloupců. Zaevidováno v CLAUDE.md.
- **#1** (`655f9d1` jádro + `23b1da3` editor + `bb1d908` review fixy) - řádkové zvýraznění z 1 pravidla na
  **seznam** `HighlightRule[]` („první splněné vyhrává"), migrátor `1.0.0→1.1.0`, editor varianta 3
  (`HighlightRulesEditorDialog`, launch přes `__highlightRules` button v `*Edit.cls` na inline panelu i
  columns dialozích), rozhraní `IRowHighlightable`. Review fixy: section-template migrátor (H1), deferred
  apply v columns dialozích (M2), bezbarevné pravidlo (L5). `TemplateSerializer` nově razítkuje verzi při uložení.
- **#2** (2026-07-08) - extrakce středního švu: sdílený `RowConditions.VisibleRows` (generický + DataContext
  overload) + `RowConditionDescriptors.RowVisibility`. Resolve-wrappery (3a) vědomě NEextrahovány (jádro už je
  `TableLayout.ResolveCascade`, per-element metody jsou jen tenké type-adaptéry).
- **Navazující (aktivní backlog):** per-sloupec render `HighlightRule.TargetColumn` + barva textu/kurzíva
  (aditivní, bez migrace, na poptávku).

## Session 2026-07-08 ✅ - Audit stavu v1 (scan 2026-07-07) uzavřen - konsolidace jádra (P-A..P-D)

Scan stavu v1 (produkční pohled na zastávkové AD+MHD) - konsolidace čistého jádra před růstem plochy
(další agendy). Žádná položka nebyla tvrdý blocker. Celý track vyřešen/rozhodnut:

- **P-A (datová parita) + C1 (legacy 1:1)** - hotovo 2026-07-07 (MHD gold + legacy parita testy; viz
  sesterský archivní záznam / changelog).
- **P-B - zmrazení čistého jádra ✅** (4 commity, 2026-07-08, každý build + 256 testů):
  - **B0** (`a559689`) - drift test EDInputForm `*Edit.cls` ↔ C# property: doplněny páry DynamicTable +
    LineTable (12 párů) + completeness pojistka (nový element neproklouzne bez testu).
  - **B1** (`1177ccd`) - split `ScheduleElementBase` na generickou vrstvu (styly/kaskáda/typeface/render,
    dědí všechny) + `StopScheduleElementBase` zastávkový mixin (DataSource klíče, cache, `Build*`); LineTable
    dědí jen generickou + vlastní klíče → netahá zastávkový balast.
  - **B2** (`d8c3ddb`) - agenda capability metadata (`BaseAgenda` params `InstanceParamName`+`ElementKind` →
    `AgendyRegistr` JSON → `AgendaRegistryEntry` → `AgendaCapabilities` mapa) místo `StartsWith` větvení;
    nová agenda existujícího druhu = 0 změn v UI. (Deploy: Caché + C# spolu.)
  - **B3** (`164a04f`) - `TableElement` → sdílené `TableLayout` jádro (kaskáda + šířky); všechny 4 řádkové
    tabulkové elementy sdílí low-level jádro, liší se jen daty + nádstavbami.
- **P-C - hygiena/drift ✅** - C-a/C-c/C-d/C-f hotové (R4 dva VM, sync-guard komentář, dangling ref, docs);
  C-e (dev artefakt `agendaCode "MHD"`) won't-do; C-b drift test → přesunut do B0.
- **P-D - provozní (na poptávku) → rehomed** do „🚀 Produkční / provozní" v aktivní roadmapě: governance
  šablon + kurátorovaná výchozí šablona (governance Distribuce), headless tisk (A3), merge PDF + e-mail (A4),
  **fyzický tisk na tiskárnu** (nově doplněn do Provozní featury). Nic se archivací neztratilo.

## Datová parita zastávkového JŘ - P-A + C1 uzavřeno (2026-07-07)

Z auditu stavu v1 (P-A) + spike C1. Detail → changelog. `ALVA.TemplateDesigner.Tests` 33/33.

- **Gold regrese (P-A)** - `ZastavkoveJrMhdGoldTest` (MHD dřív 0 gold testů; CB_DP 1001/1003/1006),
  sdílený `GoldUtil.Digest` (anti-drift AD↔MHD), AD gold zpřísněn (namespace je + linka chybí → FAIL,
  ne tichá zelená). Chrání vlastní výstup před tichou regresí.
- **Legacy 1:1 parita (C1)** - `MhdLegacyOracle` + pre-push `ZastavkoveJrMhdLegacyParityTest`: **poziční**
  porovnání (odjezdy zastávka×skupina-dnů×HH:MM + zastávky + poznámky) proti skutečnému legacy
  `ALVA.MHD.LinkyPrintFormatMHD.MakeData` (obchází padající `TISKMHD.Data.LoadData`). CB_DP 1001/1003/1006
  1:1, 0 rozdílů. Skip-if-legacy-absent.
- **Zjištění:** (1) náš engine = legacy s `TiskProjetychZast=1` (projeté zastávky) → vazba na helpdesk
  A2-A6 #7959 „filtr neobsluhovaných" (legacy feature, ne bug); (2) legacy ukládá spec. znaky jako surový
  ASCII kód, konvertuje při renderu - sjednoceno přes `ConvertSpecialniZnak` (ověřilo i náš převod = legacy).
- **Zbývá nice-to-have:** víc linek do gate; AD analog (legacy oracle `$$WinTiskMhd^JRapi`, VolanoZ=JRMAIN)
  - AD už bylo ručně 1:1 ověřené, jen by to zopakovatelnilo (≈ C9).

## Caché Migrace - track uzavřen (2026-07-07)

Sekce odebrána z aktivní roadmapy - nic v ní nebylo nezávisle akční:
- **Wave 1–4** ✅ hotové (per-user state, agenda registry, template storage, form-driven UI všude) - dřívější archiv.
- **Wave 5 (business logic per agenda)** = **absorbováno do Fáze C–E** (nové agendy použijí `AgendaAPI` pattern -
  `GetSchema/GetData/GetMockData/GetTableTemplates/GetTableTemplate`, zdokumentováno v `cache-api.md`).
  W5-5 (extract `AgendaAPIBase` helpery až 2+ konzumenti) = totéž co **C8** (dedup Caché enginů) ve Fázi C+D.
- **W4-11** (smazat PropertyDescriptory) = **neproveditelné/won't-do** - drží je FormatToolbar + multi-edit +
  fallback modál + AI catalog (paralelní systém). **W4-12** (wire `$lb()`→JSON, collapsible groups) = volitelný
  EDControls concern, jen na poptávku.
- **Evergreen pravidlo - kandidát na EDInputForm** (zachováno): ✅ formuláře s pevným server-definovaným
  schématem; ❌ triviální 1-pole confirm, dialogy s živě počítaným náhledem, plně dynamické formy bez schématu.
  Schema-duplikace dluh (Caché schema = manuální mirror) akceptován - memory `project_edinputform_schema_duplication`.

## Session 2026-07-07 ✅ - Výběr skupiny linek v Instance Pickeru (AD + MHD)

Uživatel může vybrat konkrétní skupinu linek (dřív jen default první nezablokovaná → neviděl linky
z ostatních skupin; u MHD default často prázdná testovací). Full stack:

- **Caché `GetAvailableSkupiny`** (nová abstraktní metoda na `BaseAgenda`, vzor `GetAvailableInstances`,
  JSON `[{value,label}]`): AD ze subscriptů `^JRL(sku)`, label = 1. `~`-piece („Vertikála k 1.1. - ŘÁDNÉ");
  MHD z `^ALVA.MHD.ENUMS(IdFa,"SkupinyLinek")` (název `Nazev`, mimo `Blocked`); default prázdné → picker filtr skryje.
- **`EDList.Instance`** - `LoadList`/`FetchInstances` čtou `^||ML("IN","filter","SKUPINA_LINEK_ID")` a předají
  do `GetAvailableInstances` (stejný filter-dict bod jako `agendaCode`; žádný nový mechanismus).
- **.NET:** `AgendaSkupina(Value, Label)` + `Display` „číslo - název" (dvojí search client-side);
  `CacheAgendaProvider.GetAvailableSkupiny`; Instance Picker searchable combobox (filtr přes `ICollectionView`
  → SelectedItem přežije, žádný Items.Clear gotcha); výběr přenačte EDList s filtrem. `_activeSkupinaId`
  v `TemplateDesignerViewModel` (session scope, reset při změně agendy), propagace `SKUPINA_LINEK_ID` do
  GetData (preview i export). Status chip „Skupina: …" v hlavním okně.
- Review: architektura konzistentní (wire formát R3, BaseAgenda override, EDList filter bod, R4 VM bez WPF);
  odstraněn probe + nepoužitý `.NET skupinaId` param. Build + 253/253. Ověřeno na KV_DP (AD i MHD).
- **Follow-up (ne blocker):** skupiny pro knižní JŘ agendy (Fáze C+D); persistence zvolené skupiny per šablona.

## Parkovaný tech dluh (2026-07-07) - YAGNI / oportunistické, mimo aktivní roadmapu

Vyřazeno z aktivní roadmapy (nic z toho není „udělat teď"). Revisit až se spustí konkrétní trigger:

- **T4 - god-object strangler** (`MainWindow.xaml.cs` ~2800 ř. + `TemplateDesignerViewModel` ~2600 ř.).
  Řezy **H3** (dialogy/schránka → `IDesignerDialogService`) a **M4** (undo příkazy do VM) ✅ hotové.
  Zbytek je inkrementální - **řeže se oportunisticky při práci v daném místě**, ne jako plánovaný big-bang
  (např. další řez se přirozeně veze s „výběr skupiny linek", který sahá do instance-picker části).
- **M7 - perf `WMapa`** (`ZastavkoveJrAd.cls:354`): W-mapa se kopíruje celá (`k`+`m`) per lookup, vč. uvnitř
  smyčky zastávek×variant. Čistý YAGNI - AD je už výkonově pročištěná (memoizace), žádná naměřená pomalost.
  **Recept** (až s reálně pomalou velkou linkou): (1) předpočítat variantní mapy jednou před smyčkou zastávek;
  (2) číst `^||TiskAd("wmap",ct,poradi)` přímo místo `k`+`m` (`CasJizdyZMapy` čte 2 uzly).
- **T1b / H2 - robustní gold test** (zmrazit vstup `^JRL`/`^JRZ`/`^JRSET` do izolovaného ns): **žije v Fázi C+D
  jako C9** - má smysl až s ostrým knižním/linkovým JŘ, ne dřív.

## Session 2026-07-07 ✅ - Audit před produkcí UZAVŘEN (blokery + H3–H6 + M + nízká)

**Celý audit před produkcí hotový.** Blokery B1–B3 ✅, vysoká priorita H3–H6 ✅, střední M1–M5 ✅
(M6 won't-do, M7 parkováno), nízká priorita ✅. **H1** (dedup Caché enginů) + **H2** (testy enginů +
gold gate) přesunuty do **Fáze C+D** (dávají smysl až s ostrým knižním/linkovým JŘ). Detaily M/nízká
viz sesterské archivní záznamy níže; H3–H6 zde:

> **Pozitivní baseline auditu (co bylo OK, neřešit):** čistý build (0 chyb), R3 wire formát čistý,
> R1 async čistý, secret gitignore/publish-exclusion OK, start degraduje na GENERAL-only bez Caché,
> Template CRUD chyby jdou přes `IPopupDisplayer`, DepartureGrid cascade anti-pattern NENÍ přítomen,
> typeface caching správně, vrstvy výrazů/layoutu/serializace dobře otestované.

- **H4** ✅ - statický `DocumentDefaults.Style` race při async exportu → backing field `AsyncLocal<TextStyle?>`
  (export na `Task.Run` má izolovanou hodnotu, UI live-preview svou). Regresní test.
- **H5** ✅ - `CacheAgendaProvider` polykal + cachoval chyby → už NEcachuje na error; `OnReadError` callback →
  nemodální „⚠ Databáze … selhalo" ve status baru (thread-safe, R4-safe).
- **H6** ✅ - latentní `<UNDEFINED>err` v catch napříč agenda/dispatch metodami → `s err=""` před `try`
  do všech metod s `$$$CatchErrRest` (21 souborů).

### H3 - ViewModel bez WPF (R4), dialogy přes službu

`TemplateDesignerViewModel`
přímo instancoval WPF okna + `.ShowDialog()` + `OpenFileDialog`/`SaveFileDialog` + systémovou `Clipboard`
(porušení R4, neunit-testovatelný).

- **`Services/IDesignerDialogService`** (nová abstrakce View vrstvy): `ShowInstancePicker`/`ShowChangeAgenda`/
  `ShowCacheTemplatePicker`/`ShowSaveToCache` (dialog dostane hotový dialogový VM), `PickOpenFile`/`PickSaveFile`
  (→ cesta nebo null), `SetClipboardText`.
- **`WpfDesignerDialogService`** (code-behind vedle `MainWindow`, drží owner okno) mapuje VM → konkrétní okno;
  `_vm.Dialogs = new WpfDesignerDialogService(this)` v `MainWindow` konstruktoru.
- VM staví dialogové **ViewModely** (VM→VM, žádné WPF) a jen je nechá zobrazit; výstup čte z dialog VM
  (`SelectedInstances`/`ChosenCode`/`SelectedTemplateId`). Souborové dialogy + clipboard přes službu.
- `using System.Windows` z VM odstraněn (zbývá `System.Windows.Input` kvůli `Key` enum v `MoveByKey` -
  vstupní enum, mimo scope H3). VM je nyní testovatelný přes mock služby.
- Build 0 chyb, 253/253 testů. Plný VM unit-test harness (EDServices/MLRest mock) = širší → T4 strangler.

## Session 2026-07-07 ✅ - Audit před produkcí: nízká priorita (2. dávka)

Pokračování auditu - nízko-prioritní body. Blokery **B1/B2** a vysoká priorita **H1–H5** zůstávají
otevřené v aktivní roadmapě.

- **Renderer (vrstva 4) - testy geometrie ✅.** Pozicovací matematika buňky (padding shrink, H/V
  zarovnání, clamp) vytažena z `CellRenderer.DrawCell` do čistého `Rendering/CellLayout` (behavior-preserving
  refaktor) + `TemplateDesigner.Tests/Rendering/CellLayoutTests.cs` (9 testů). Kreslení na SKCanvas se
  netestuje (SkiaSharp), ale výpočet kotvy ano - deterministicky, bez fontů. **Plný běh: 252/252 zelených.**
- **`App.xaml.cs OnUnhandledException` ✅.** Uživateli už jen `Exception.Message` (ne syrový stack trace),
  plný detail do `Debug.WriteLine`. `MessageBox.Show` v composition-root ponechán záměrně (last-resort net
  nesmí záviset na app službách, které můžou být tou padající věcí).
- **`MainWindow.OnPreferencesLoaded` ✅.** Async tělo obaleno try/catch → výpadek Caché při načítání prefs
  = nenápadný status „⚠ Nepodařilo se načíst uložené parametry" místo propadnutí do globálního handleru.
- **`EDInputForm/PageEdit.cls` deprecated `$$$Init` ✅.** Odstraněn z `Init`+`Validate` (jediný výskyt v TD;
  sladěno se sourozeneckými EDInputForm třídami). Caché → čeká compile v dev flow.
- **Zranitelné závislosti ⛔ akceptováno (mimo TD scope).** Jediná *zranitelná* `Newtonsoft.Json 7.0.1`
  (NU1903 HIGH) je pinnutá kvůli MLRest ABI (bind 7.0.0.0) → nejde bumpnout bez rebuildu MLRest; warning
  NEpotlačovat (memory `project_mlrest_newtonsoft_debt`). `ZXing.Net 0.16.9`/`PdfPig 0.1.13` jsou staré,
  ale ne zranitelné → bump nízká hodnota + riziko API churn. Reálná náprava = rebuild MLRest (EDControls task).

## Session 2026-07-07 ✅ - Audit před produkcí: střední-prioritní tech dluh (M1–M5) + rozhodnutí M6/M7

Z komplexního auditu kvality kódu (4 dimenze: .NET architektura, Caché/ObjectScript, produkční
připravenost, test coverage). Blokery **B1/B2/B3** a vysoká priorita **H1–H5** zůstávají otevřené
v aktivní roadmapě (viz „🔴 Audit před produkcí"). Střední priorita hotová/rozhodnuta:

- **M1 ✅ - prázdný subscript v MHD.** Guard `i (TypProvozuID="")||(SmerID="") continue` v
  `ZastavkoveJrMhd.PromitnutSpoje` - spoj s prázdnou skupinou dnů / směrem se přeskočí místo pádu
  celé linky (`^||TiskMHD(…,"")` = `<SUBSCRIPT>`). Behavior-preserving pro validní data (0/1).
- **M2 ✅ - try/catch kolem 3 živých `GetMockData`.** `ZastavkoveJrAd` (commit 876ada3),
  `KnizniJrAd`, `KnizniJrMhd` - obaleno `try { $$$FillParams … } catch (ex) { $$$CatchErrRest } ret $$$OK`.
  Vadná data na první lince firmy → čistá hláška v preview místo syrového pádu. (Odložený drobný
  follow-up: statický vzorek nezávislý na datech, `ZastavkoveJrAd.cls:1219` - nízká hodnota.)
- **M3 ✅ - únik Skia handles v `DepartureGridElement.Render`.** Alokace fontů/paintů/zebra
  (`CreateFont`/`SKColor.Parse` nad uživatelskými barvami mohou vyhodit) přesunuta dovnitř `try`;
  deklarace zůstaly na method scope (helpery je zachytávají), 4 anotace fonty nullable + `?.Dispose()`.
  Řeší i neuvolněný clip při chybě setupu. Build 0 chyb / 0 nových warningů.
- **M4 ✅ - konstrukce undo příkazů z code-behind do VM.** 4 metody
  `Apply{DepartureGrid,StopColumns,LineTableColumns,DynamicTable}Edit` + helper `ExecuteAsComposite`
  v `TemplateDesignerViewModel` (WPF-free, testovatelné); `OpenXxxDialog` v code-behind už jen otevírá
  dialog + volá VM. Sémantika příkazů identická. (Otevírání dialogu zůstává v code-behind → dořeší H3.)
- **M5 ✅ - unit testy undo/redo.** `TemplateDesigner.Tests/Commands/UndoRedoCommandTests.cs` (14 testů:
  Set/Move/Resize/Add/Delete element+sekce, Move/Reorder sekce, Group/Ungroup, **Composite undo v opačném
  pořadí**, **ScaleTemplate inverzní round-trip**). Všech 14 zelených.
- **M6 ⛔ akceptováno (won't-do).** Import legacy (předrefaktorového) JSON tiše zahodí styling
  (flat text props → `Style` default, `WasMigrated=false` → bez varování). Reálná expozice ≈ nulová:
  DB je primární úložiště od 2026-06-30 a všechny DB šablony jsou už composable; plochý JSON = mrtvý
  dev-era artefakt. Není to pád ani poškození dat (import = nová neuložená šablona, špatný font hned
  vidět). Spolehlivá detekce by false-positivila. YAGNI → neřešit.
- **M7 ⏸️ odloženo (parkovaný perf-item).** `WMapa` kopíruje celou W-mapu (`k`+`m`) při každém lookupu,
  vč. uvnitř smyčky zastávek×variant (`ZastavkoveJrAd.cls:354`), ač `CasJizdyZMapy` čte jen 2 uzly.
  Čistě latence, ne korektnost - výstup gold-ověřený 1:1, engine „výkonově pročištěný", žádná nahlášená
  pomalost. Zásah = riziko parity. Recept (až se objeví pomalá velká linka): (1) předpočítat variantní
  mapy jednou před smyčkou zastávek; (2) číst `^||TiskAd("wmap",ct,poradi)` přímo místo `k`+`m`.
  Zapsáno v backlogu „Tech dluh".

⚠️ **Poznámka:** Caché edity (M1, M2/KnizniJr*) čekají na compile v dev flow - MCP `cache_compile`
míří na `C:\DEV\EDISON`, ne na tento repo. Syntaxe standardní, shodná s `GetData` ve stejných souborech.

## Session 2026-07-06/07 ✅ - Layout tabulek: „sekce vyšší" + DynamicTable varianta A (R2 zrušeno)

**„Sekce vyšší než stránka" ✅** - designové plátno roste přes víc A4 (Slice 1) + dělící čáry stránek
(Slice 2) + fix dvojitého vykreslení rámečku u split sekce. Preview i PDF/PNG dělí přes stránky pixel-ořezem.

**DynamicTable - rozhodnuto: varianta A (chová se jako zastávky).** `Height` = strop, obsah se vejde
auto-scalem, prvek se nerozrůstá přes stránky. Dynamická výška (`Height=null`) roste s obsahem. Sekce
vyšší než stránka → pixel-ořez (jako StopList). Oprava: vnější rámeček tabulky vsazen dovnitř o půl
tahu (`InsetForStroke`) - dřív se hrana ořezávala těsným clipem sekce.

**R2 (svislé řádkové stránkování + opakované záhlaví) - přidáno a zase odstraněno.** Během session
vyzkoušeno (zlom po celých řádcích, opakované záhlaví, respekt Y pozice), funkčně ověřeno renderem.
Po revizi konzistence ale **vědomě zrušeno**: „rostoucí tabulka přes stránky" je nekonzistentní se
zbytkem návrháře (statické prvky i zastávky mají výšku = strop) a čisté „opakované záhlaví na multipagi"
jde jen s rostoucí tabulkou. Rozhodnutí: raději čistě do produkce, R2 přidat zpět jen na **konkrétní
poptávku**. Kód odstraněn kompletně (žádný mrtvý kód): `IRowPaginatedElement`, `LayoutRowPaginated`,
`PlacedSection.StartRowUnit/EndRowUnit/RepeatHeader`, `RenderRowWindow`, R2 větev v `PreviewCanvas`, testy.
**Spec pro budoucí re-implementaci zůstává** v `architektonicka-rozhodnuti-agendy.md`. Detail: [`changelog.md`](changelog.md).

## Session 2026-07-03 ✅ - Wave 4 W4-10: inline panel na EDInputForm

Levý panel „Vlastnosti" nově hostuje **EDInputForm pro jeden element** (všech 9 typů přes `*Edit.cls`
schema + `ElementFormBinder`) místo PropertyDescriptorů → sjednocený „dvojí vizuální jazyk" (panel i
modály jedou na stejném form-driven UI). Detail: [`changelog.md`](changelog.md).

- **Živý write-back:** `EDInputForm.FieldChanged` (nový event) / Enter → `ExtractCommands` → SetPropertyCommand
  (undo) → canvas. Enter v poli commitne (ne otevře dialog); bare klávesy patří poli.
- **EDControls (additivní):** `FieldChanged` event + `CacheClass` setter zahodí controller (runtime
  přepnutí schématu - dřív držel první třídu → StopList ukazoval text pole).
- **Nový `Controls/InlinePropertiesForm`** (embedded host + live state machine: re-Prefill na výběr,
  guard proti rekurzi, ošetřený `Loaded` re-init).

**Wave 4 tím funkčně uzavřena.** Zbytek NE-hotový, ale záměrně:
- **W4-11** (smazat PropertyDescriptory) - neproveditelné: drží je FormatToolbar + multi-element edit +
  fallback modál + AI `CapabilityCatalog`. PropertyDescriptory zůstávají jako paralelní systém.
- **W4-12** (EDInputForm wire `$lb()`→JSON, collapsible groups) - volitelné, EDControls-framework concern.
- **Polish (later):** undo/redo nerefreshne zobrazené hodnoty v panelu (display staleness do reselectu);
  přepnutí typu = 1 Caché reload schématu (cache per typ, když bude lagovat).

## Parked known-limitation - canvas perf při vysokém zoomu (2026-07-03)

Vyřazeno z aktivní roadmapy - na stropu 500 % funguje svižně (ověřeno). Vrátit se k tomu jen když se
ukáže reálný problém na uživatelských PC (poptávka).

- **Podstata:** `SKElement` drží byte buffer celé plochy (`pageW × MmToPx`). Zoom 500 % A4 ≈ 3969×5615 px
  ≈ 89 MB; 1000 % ≈ 365 MB. Hardware-bound bottleneck SkiaSharp.Views.WPF.
- **Workaround (aktivní):** zoom zastropován na 500 % (`ZoomLevels` max 5.0).
- **Proper fix (~4–6 h, rizikové):** virtualizace - SKElement jen ve velikosti viewportu, obsah posouvat
  scroll offsetem, kreslit jen viditelnou část. Riziko: `e.GetPosition(skCanvas)` je dnes absolutní px
  celé stránky → každá interakce (drag/resize/rubber band/ghost/hit-test) by potřebovala scroll-offset
  transformaci. Dělat jako samostatnou session, ne mezi cleanupy.

## Session 2026-07-03 ✅ - Architektonický track: malé T kousky

Prošli jsme T1–T6 a odbavili všechny **malé self-contained** kusy (detail v [`changelog.md`](changelog.md)):
- **T5a** - `EDList/Instance.GetClassForAgenda` deleguje na nový `AgendyRegistr.GetClassForCode`
  (auto-discovery z `%Dictionary`, žádný hardcoded mirror). Ověřeno přes `ListAgendy`. **T5b přeskočeno**
  (živý AD mock ponechán záměrně).
- **T6** - `architecture.md` doplněn o `/Services`+`/PdfImport`; per-frame `new SKPaint` posouzeno = zavřeno
  (nevyplatí se). Celé T6 hotové.
- **T4 malé kousky** - R4 fix `TableEditorDialogViewModel` (inline WPF picker → code-behind přes callback,
  VM 0× System.Windows) + rename `MainWindowViewModel.cs` → `TemplateDesignerViewModel.cs`.
- **StopList kaskáda testy** - `StopListCascadeTests` (9 testů): feature override aktuální/projeté zastávky,
  legacy `col.Alignment` fallback, per-column Style priorita, element-level HeaderStyle.

**Záměrně otevřené (velké, ne blocker):** T1b-robustní (zmrazit vstup do izolovaného namespace) · plný
T4 god-objects strangler (postupné řezy velkých tříd).

## Session 2026-07-03 ✅ - Pre-pilot UX polish (Fable audit, celý blok)

Audit 19 oken proti produktové filozofii („srozumitelnost nad úplností"). **Celý P0 + akční střední/malé
položky hotové**; detail v [`changelog.md`](changelog.md) 2026-07-03.

**🔴 P0 (U1–U7) - vše hotovo:**
- **U1** BUG: „Ano = uložit" v confirm neuložených změn ukládalo do JSON souboru místo DB → teď DB
  (stejná cesta jako Ctrl+S). Celý confirm/close flow převeden na async (`ConfirmUnsavedChangesAsync`,
  `SaveAndContinueAsync`) + fix pádu při zavření čistého okna (Close přes `Dispatcher.BeginInvoke`).
- **U2** context menu „Vlastnosti elementu…" → otevře dialog (dřív jen přepnulo panel).
- **U3** klik na paletu (bez tažení) vloží element do aktivní sekce (fallback v `EndAddingElement`).
- **U4** dvojklik do prázdna už neotevírá vlastnosti stránky - zruší výběr.
- **U5** Ctrl+S bez DB → popup s nabídkou JSON exportu (dřív tiché do status baru).
- **U6** cancel sjednocen na „Storno" přes `{StaticResource EDStrings.Cancel}` (7 dialogů) + pravidlo do `ux-conventions §1`.
- **U7** M/O snap tooltipy - už existovaly (audit stale).

**🟠 Střední - akční hotové:**
- **Skryté featury do context menu** - Seskupit/Rozeskupit (Ctrl+G/Ctrl+Shift+G), „Rozdělit → Vodorovně/Svisle" (Ctrl+Shift+H/J), „Upravit text (F2)".
- **Drift tabulky zkratek** - `ux-conventions §3` přepsáno 1:1 dle KeyDown handleru (Ctrl+Shift+S smazán; doplněno ~10 chybějících).
- **Zoom tlačítko × zkratka konzistentní** - „Fit"+Ctrl+0 = přizpůsobit; „1:1"+Ctrl+1 = 100 %.
- **StatusText segmentace** - status bar rozdělen na „stav výběru" (`SelectionStatusText`, klid) + „akce/progres" (`StatusText`, transient) → nepřepisují se.
- **Escape ruší probíhající drag/resize/rubber band** (restore z init).

**🟢 UX polish (otevřené) - prošlo 2026-07-03, ověřeno proti kódu:**
- **Stylový pod-formulář** ✅ hotové - `StyleFields.cls` (sdílený balíček) pohání stylové zóny ve všech
  EDInputForm „Obecné" tabech (DepartureGrid Data/Minuta/Symbol/Text, DynamicTable + StopList Header/Data);
  `StyleEditDialog` používá sdílený `CellStyleEditor` (tri-state). `StyleEdit.cls` = záměrně parkovaná
  server-side varianta A (dokumentováno v hlavičce, ne mrtvý kód).
- **Sjednotit editor sloupců** ✅ hotové - `ColumnListEditor`/`IColumnRowVM` sdílí DepartureGroup,
  DynamicTable, StopColumns; dormant TableElement vyňat záměrně.
- **Font warning** + **ad-hoc overflow tlačítko** = YAGNI (přetečení se hlásí při exportu; font se vybírá
  z InstalledFamilies) → přesunuto do backlogu.
- **Tabulkové featury** (grouping / computed fields / nested datasource) → backlog (bez customer demand).

**Vědomě odloženo / parkováno:**
- **Ikonový jazyk** (Fluent + glyfy) - necháváme, není zásadní.
- **Rubber band napříč sekcemi** - nepotřebujeme; sahá do selection sémantiky, pilot potvrdí.
- **Dvojí vizuální jazyk = left-panel refaktor** (PropertyDescriptor → EDInputForm, W4-10/11) - vyčleněno
  do backlogu jako samostatný velký track (chceme do budoucna).
- **Layout robustnost:** „sekce vyšší než stránka" (var B, největší koncepční layout dluh) - pilot-gated,
  vytaženo do „Větší dedikované tracky" v roadmapě. „Element drag mezi sekcemi" - **zamítnuto 2026-07-03
  (nepotřebujeme; cut+paste stačí).**
- **🧭 Filozofie:** rozšířit UX mřížku o vrstvu menu/toolbar/zkratka (`feature-design-principles`) - doc, low-prio.

## Session 2026-07-03 ✅ - EDISON launch integrace + úklidy

**EDISON → TD launch integrace (A1 + A2, Martin Hubata, merged do main).** TD se spouští ze stromu
starého EDISONu (uzel „Návrhář sestav (nový)" 76016 v MHD + JŘ; launcher `formTISKJRTemplateDesigner`
+ MHD2 „Linky a oběhy") přes `Process.Start TemplateDesigner.exe` s **EDISONMAIN2 arg kontraktem**:
- **A1 session** - `EDControls.EdisonLaunchArgs` parsuje kontrakt (sentinel `EDISONMAIN2` + server,
  restPort, namespace, idfa, login, fullName, mlp [, formName, shellGuid, inputParam]); `App.OnStartup`
  staví MLRest ze session shellu (`CacheSchemaWiring.CreateMlRest`) místo z appsettings → TD běží pod
  **správnou firmou/uživatelem**. Bez argů = standalone z appsettings (fallback). `shellGuid` se
  nepřebírá (TD má vlastní session, neváže se na ACTIVITY zámek).
- **A2 startup agenda** - `inputParam "Agenda=<kód>"` (JŘ→`ZASTAVKOVY_JR_AD`, MHD→`ZASTAVKOVY_JR_MHD`)
  → nová šablona startuje rovnou nad tou agendou. Ověřeno na devu: DPKV → linky DPKV, CB_CSADAD → linky
  CB_CSADAD. **Open-designer flow per-zákazník funguje**; pro v1 zbývá už jen ruční release deploy (D3/D4).

**T1b gold regrese base** - `ZastavkoveJrAdGoldTest` (2 gold linky 0365001+0365005, digest = počty +
MD5 celého GetData výstupu), v pre-push (27/27). Pevně vůči CB_CSADAD; robustní verze (zmrazit vstup)
odložena.

**Úklidy:** T6 `ScheduleDetectorTest.cs` odstraněn z prod assembly; poslední `MessageBox` v MainWindow
odstraněn spolu s vestigiálním menu „Vlastnosti textu…" (dvojklik/Enter dělá totéž). W4-7 Page potvrzeno
hotové (`PageEdit.cls` přes generický `ElementEditDialog`). DynamicTable unit testy (6). R1a průchod
(vnitřek čistě rozvrstvený; jediný nález = zarovnat render na sdílený `ColumnRenderer.DrawRow`, odloženo).

**AD zastávkový JŘ track uzavřen** - 1:1 hotový; zbytek (vizuál legendy POZNAMKY, AD-N5 `MHD_NEZNZ123`,
AD-N15 prázdninové NZ) záměrně navázán na reálný pilot (odemyká je konkrétní zákaznická konfigurace/data,
kterou teď nemáme). Odložené helpdesk: A2-A5 (#7937 indent), A2-A6 (#7959 filtr neobsluhovaných), A2-B4
(#8828 per-sloupek) - datový průzkum / diskuze.

## Session 2026-07-02 ✅ - DynamicTableElement + AD 1:1 uzávěrka + architektonický track

**DynamicTableElement** - rozdělení tabulek: dynamická (řádky z `DataSource` kolekce) samostatný
element + 2-tab editor (Obecné EDInputForm `DynamicTableElementEdit.cls` + Sloupce přes
`ColumnListEditor`/`CellStyleEditor`); statická `TableElement` uspána (JsonDerivedType jen pro
legacy načtení, z UI pryč). Auto-scale do pevné Height, auto-distribuce šířky, seed sloupců z polí
kolekce centralizovaný v dialogu, dvojklik otevře dialog. **Uzavírá Wave 4 W4-9** (komplexní
elementy: StopList ✅ + DepartureGrid ✅ + dynamická tabulka ✅). Detail changelog 2026-07-02.

**AD zastávkový JŘ = 1:1 kompletní.** Hotové AD-N: N1, N2, N3, N6, N7, N8, N8b, N9, N10, **N13
(multi-varianta TrasaMin / L1, 1:1 gold 0365005)**; číslování omezení `cnz` ✅; pořadí legendy
ověřeno ✅. **AD-N4/N11/N12/N14 přeřazeny do linkových JŘ** (legacy zastávkový je nepoužívá).
`GetSchema` kolekce (ZASTAVKY/SKUPINY_DNU/ODJEZDY/POZNAMKY) synced 1:1 s reálnými `Build*` daty
(doplněna skutečná skalární pole, phantom NADPIS2 odstraněn). Zbývá jen vizuál legendy + N5/N15
(blokované dormant daty).

**Helpdesk A.2-D ✅** - #7947 („jede až do XY"), #7955 (custom komentář přebíjí auto + NZ bug),
#7161 (skrytí „na znamení" + vypnutí agregátu bezbariér) zavřeno pro AD agendu. #11300 (KORID
přípoje) přeřazeno k linkovým JŘ (není zastávkový koncept). Port na MHD agendu neproveden (až bude
potřeba). Odložené A2-A5/A6/B4 zůstávají (datový průzkum / diskuze).

**Architektonický track** - T1a C# testy (164, pre-push) ✅, T1b Caché testy (25/40) ✅ částečně
(zbývá compare-gold skript), T2 async multi-instance export nad snapshotem ✅, T3 migrator guardy
(forward-compat R5) ✅. Rozhodnutí **R1–R7** pro škálování agend zdokumentována
([`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md)).

**Caché Migrace** - Wave 1/2/3 ✅; Wave 4 kroky W4-0..W4-9 ✅ (elementy + Image + Section + **Page**
`PageEdit.cls` + parametry + StopList/DepartureGrid/dynamická tabulka). Zbývá jen W4-10/11 (inline
panel migrace + PropertyDescriptor cleanup - velký refaktor, záměrně na konec).

## Handoff session 2026-06-21 (historický záznam) ✅

> Archivováno 2026-06-30. Tento blok byl původně živý na vršku roadmapy jako handoff pro pokračování
> z druhého PC. Většina je dnes hotová nebo překonaná; ponecháno jako historický kontext.

**Hotovo v session 2026-06-21:** AD nastavení linky + ruční poznámky (Komentáře / nepoznámkovat /
směrové), #7955 přepínač, „jede až do" (#7947), skrytí na znamení + agregát bezbariér (#7161 D5/D6),
layout legendy (sloupce 1-4 + směr wrapu + zarovnaný marker + řazení). Helpdesk zavřeno: **#7955,
#7959, #7947, #7161**.

**DEV tweaky session** (vyřešeno): AD startup šablona revertována zpět na produkční MHD+3 sekce
(commit „revert DEV startup tweak"). Dev firma později přepnuta na **CB_CSADAD** (demo / gold linka
0365001, commit 9ac5577) - aktivní stav viz živá roadmap „Před produkcí".

**Doc-cleanup body z handoffu** (1 = ořez `ad-data-mapa.md §6/§10`, 2 = hlavičkové poznámky,
3 = cross-refs, 4 = changelog archivace) - řešeno průběžně v živé roadmapě. Prezentační prep docs
(osnova, režie demo) konsolidovány 2026-06-30 do
[`legacy-nahrada.md`](legacy-nahrada.md), zdrojové artefakty smazány. **Gap analýza
legacy vs nový ponechána** jako samostatná evergreen reference (`legacy-nahrada.md`).

## Wave 3 - Template storage ✅ (2026-05-25)

Caché-driven persistent storage šablon s per-IDFa izolací + schema versioning framework pro defensive ochranu před budoucími breaking changes.

**Implementováno** (3 commity):

| Krok | Co | Commit |
|---|---|---|
| W3-1 | Caché `ALVA.TemplateDesigner.API.Sablony` - 5 CRUD metod (List, Load, Save, Delete, Rename) | `5ac9dd0` (2026-05-15) |
| W3-2 | .NET `CacheTemplateService` async wrapper + `CacheTemplateException` | `5ac9dd0` |
| W3-3 | `CacheTemplatePickerDialog` + `SaveToCacheDialog` UI | `5ac9dd0` (L5 migration na EDList: `9f5b165`) |
| W3-4 Fáze 1 | Schema versioning + `TemplateMigrator` framework (H-18) | `174d169` (2026-05-25) |

**Storage layout:**
```
^ALVA.TemplateDesigner.Templates(IdFa, "DTA", TemplateID, "Metadata") = $lb(Name, AgendaCode, Author, CreatedAt, ModifiedAt, ModifiedBy, Version)
^ALVA.TemplateDesigner.Templates(IdFa, "DTA", TemplateID, "Data")     = <JSON blob šablony>
^ALVA.TemplateDesigner.Templates(IdFa, "IDX", "ByAgenda", AgendaCode, TemplateID) = ""
```

**Schema versioning framework** (Fáze 1):
- Template JSON má top-level `version` (semver `"1.0.0"`)
- `TemplateMigrator` pipeline aplikuje sekvenci migrací při loadu
- Žádné konkrétní migrace zatím - framework no-op, připraven na budoucí breaking changes
- Convention v [`template-schema-versions.md`](template-schema-versions.md)

**Vědomě skipnuté** (decision rationale):

| Item | Důvod skipu |
|---|---|
| Auto-snapshots history (Fáze 2) | Duplicate templates (Save As) + within-session undo pokrývají 90%+ use cases. Velké tools (Adobe, Sketch, AutoCAD) auto-snapshot taky nemají; jen massové cloud tools (Figma, Canva) ho mají jako defense in depth pro broad audience. TD scope (3-5 customers, 5-10 users/customer) tomu neodpovídá. |
| Per-template named history (Fáze 2 β) | Save As pattern poskytuje stejnou hodnotu - verze viditelné v picker, lze porovnat paralelně |
| Audit log (W3-6) | Bez user requestu. Revisit on real need (compliance, debugging) |
| Thumbnails (W3-7) | Nice-to-have, low priority. Picker list zobrazí metadata (Name, Author, ModifiedAt) - stačí |
| Soft-delete | Hard delete + Caché backup procedures stačí |
| Import wizard (W3-9) | Málo legacy templates, manual Open + Save to Cache stačí |
| Server-side migration | .NET-side migrátor v Fázi 1 stačí (single source of truth při loadu) |

**Klíčová architektonická rozhodnutí:**
- **Per-IDFa storage** - multi-tenant isolation
- **Atomic Metadata + Data split** - list zobrazí jen metadata (rychlé), Data fetch on demand
- **Per-agenda sekundární index** (`IDX/ByAgenda`) - efektivní filter v picker
- **JSON wire formát (R3)** - typed records v .NET
- **Semver versioning** - flexibilita pro budoucí major/minor distinction
- **Idempotentní migrace** - repeated apply = same result
- **Defensive load** - chybějící version v JSON → assume `1.0.0`, neházet exception
- **Single-point wire-up** - `TemplateSerializer.Deserialize` routes through migrator pro file i Caché path

---

## Wave 2 - Shared catalogs ✅ (2026-05-20)

**Scope** po diskusi zúžen ze 4 položek na 1 implementovanou + 3 skipnuté:

| # | Položka | Stav | Důvod |
|---|---|---|---|
| W2-1 | Agenda registry → Caché | ✅ Hotovo | Auto-discovery z `BaseAgenda` subclasses (commit `de0f872`) |
| W2-2 | Section templates → Caché | ⛔ Skipnuto | Funkčně překryto Wave 3 Template starters (Open + Save As pattern). Bez user requestu. Existující file-based zůstává jako side-feature. |
| W2-3 | Element defaults → Caché | ⛔ Skipnuto | Hardcoded v C# je rozumné; Wave 4 InputGrid persist do Template JSON pokrývá use case |
| W2-4 | Company palette → Caché | ⛔ Skipnuto | Wave 1.5 RecentColors pokrývá; brand palette není user request |

### W2-1 Agenda registry - auto-discovery design

**Princip:** „Třída JE registrace". Eliminuje drift mezi registrem a impl (vs JSON config list).

**Caché:**
- `BaseAgenda.cls` - 2 class parametry (`AgendaCode`, `AgendaLabel`); konvence `<TYP_SESTAVY>_<DATOVY_ZDROJ>`
- `ZastavkoveJrMhd.cls` - `AgendaCode = "ZASTAVKOVY_JR_MHD"` (přejmenováno z `"MHD"` - MHD je datový modul, ne typ sestavy)
- `Faktury.cls` - `AgendaCode = "FAKTURY"`
- NEW `API/AgendyRegistr.cls` - `ListAgendy` přes `%Dictionary` SQL nad subclasses BaseAgenda, vrátí JSON pole `[{code, label, cacheClass}, ...]`

**.NET:**
- NEW `Serialization/IAgendaRegistry.cs` - interface + `AgendaRegistryEntry` record
- NEW `Serialization/CacheAgendaRegistry.cs` - impl s Task.Run wrap (R1), synteticky prepend GENERAL agendy, fallback při chybě jen GENERAL
- `Config/CacheSchemaWiring.cs` - `TryCreate(MLRest, IReadOnlyList<AgendaRegistryEntry>)`
- `Config/AppSettings.cs` - odstraněna `Agendas` property + `SchemaAgendaSettings` třída
- `App.xaml.cs` - sync load agendy ze Caché s 3s timeoutem
- `MainWindow(MLRest?, prefs?, agendas?)` - 3. ctor param
- `templatedesigner.appsettings.json` - smazáno `agendas[]`

**Architektonická rozhodnutí:**
- Per-customer kontrola odložena (YAGNI - všichni zákazníci mají stejný kód)
- AgendaOrder parameter neřešen - alfabeticky podle code
- AgendaEnabled neřešeno - pre-release work řešen git branches
- Naming `<TYP_SESTAVY>_<DATOVY_ZDROJ>` - budoucí JŘ: `KNIZNI_JR_MHD`, `MEZIMESTSKY_JR_MHD`, `VOZOVY_JR_MHD`

**Commits:** `7d6704a` (docs scope reduction), `de0f872` (impl).

---

## Wave 1 + 1.5 - Per-user state přes Caché ✅ (2026-05-20)

První dvě dávky Caché Migration - persistence per-user × IDFa state v `^ALVA.EDControls.UserData` přes `IUserPrefsService`. Žádné lokální JSON soubory (TD běží embedded z EDISONMAIN2). Detail dokumentace, registr scope ownerships a 3-typová taxonomie state v [`EDControls/DOCS/05-userdata-scopes.md`](../../../EDControls/DOCS/05-userdata-scopes.md).

**Wave 1 (foundation + UX integrace):**

| Krok | Co | Commit |
|---|---|---|
| W1-0 | `IUserPrefsService` foundation + Caché `ALVA.EDControls.UserData` (generic blob storage per IdFa × userID × scope × key) | F1 (2026-05-15) |
| W1-1 | `TdPreferencesService` wrapper + `MainStatePref` typed record (Typ 1 - UI state, jeden blob) | 7cd39ab |
| W1-2 | Wire-up: `App.xaml.cs` async load + `TdPreferencesService.Loaded` event fallback pro cold-start | 7cd39ab |
| W1-3 | `MainWindowViewModel.ApplyLoadedPreferences` - Zoom/Grid/Snap/SnapGapMm/ShowGrid/ShowSectionHeaders/ShowRuler | 7cd39ab |
| W1-3b | UI-side prefs - splittery (LeftPanelWidthPx, PreviewPanelWidthPx), ActiveLeftTabIndex, ShowPreviewPane | 7cd39ab |
| W1-4 | Recent Files menu (Soubor → Otevřít poslední) + AddRecentFile při Open/SaveAs, missing-file graceful (Typ 2 - activity log) | 7cd39ab |
| W1-6 | Instance defaults per šablona (scope `"TemplateDesigner.InstanceDefaults"`, key = template Guid, Typ 3 - per entity) | 7cd39ab |

**Wave 1.5 (Recent Colors + PrintParams):**

| Krok | Co | Commit |
|---|---|---|
| W1.5-1 | Recent Colors - 8 last-used barev v color pickeru (`"TemplateDesigner"` / `"RecentColors"`, Typ 2). DRY refactor `ColorPickerHelper` jako single entry point - odstranění duplicitního WinForms ColorDialog kódu z `PropertiesPanel` | 06eeda2 |
| W1.5-2 | PrintDialog params per-agenda persist + live sync s preview lištou (`"TemplateDesigner.PrintParams"` / `<agendaCode>`, Typ 3 per agenda). Sdílený dict + cross-notification callback. Crash fix (odstranění sync `.Result` blocking UI thread) + re-entrance guard pro rychlé Ctrl+P. | 06eeda2 |
| W1.5-3 | PropertiesPanel group expanded state | ⛔ Odloženo do Wave 4 (EDInputForm migrace nahradí PropertiesPanel) |
| W1.5-4 | Font override (analogie EDISONMAIN2 LocalPrefs.FontFamily/FontSize) | ⛔ Odloženo (žádný user request) |

**Doc commity:**

| Commit | Obsah |
|---|---|
| ec654e4 | `EDControls/DOCS/05-userdata-scopes.md` - registr UserData scopes |
| ced96fa | 3 typy state taxonomy + Wave 1.5 priorities |

**Klíčová architektonická rozhodnutí:**
- **Žádné lokální JSON soubory** pro persistent state (TD běží embedded z EDISONMAIN2)
- **Async-first pattern** - žádné sync `.Result` block na UI threadu (poučení z crash fixu)
- **Per-agenda granularita pro PrintParams** - nová šablona stejné agendy zdědí poslední hodnoty
- **Sdílený dict + cross-notification** - single source of truth pro params mezi PreviewLišta a PrintDialog
- **3-typová taxonomie state** dokumentována jako závazná pro budoucí konzumenty (Typ 1 UI state, Typ 2 activity log, Typ 3 per entity)
- **Schema evolution backward-compat** - všechna pole records mají defaults v primary ctoru
- **DRY ColorPickerHelper** - jediný entry point pro color picker napříč TD

---

## v1.0 demo balíček ✅ (2026-05-15)

Tři commity tvoří kompletní release readiness pro testovací nasazení - zákazníkovi lze ukázat funkční end-to-end workflow pro zastávkový JŘ MHD. Detaily viz [`changelog.md`](changelog.md) 2026-05-15 entry.

| # | Co | Commit |
|---|---|---|
| R1 | **Agenda + Linka picker UX** - `ChangeAgendaDialog` se 3 entry pointy (menu / Ctrl+Shift+A / klik na status chip); `InstancePickerDialog` (multi-výběr linek, search, batch); Caché `GetAvailableInstances` iteruje skupinu linek; permanentní status indikátor (zelený agenda chip + modrý instance chip s ◀▶ + window title `[MHD]` prefix) | `82b68be` + `fd25008` |
| R2 | **Template storage v Caché** - třída `ALVA.TemplateDesigner.API.Templates` (5 metod, per-IDFa, JSON wire); `CacheTemplateService` async wrapper; `CacheTemplatePickerDialog` (správce: list + filtr + search + Open/Rename/Delete); `SaveToCacheDialog`; menu „Soubor → Otevřít z Caché... / Uložit do Caché [jako...]" + „Nástroje → Šablony v Caché..."; Caché namespace refactor `API/Agendas/Forms`; GENERAL agenda fallback | `5ac9dd0` |
| R4 | **Polish** - status bar chips, ConfirmUnsavedChanges integrace, error reporting přes IPopupDisplayer, BuildAllPreviewDataPages bugfix pro šablony bez agendy, debug logging do `^jm` (po debugu odstraněno) | průběžně |

**Performance:** 600 stránek MHD JŘ za ~9s (5.2s fetch + 3.6s render, ~15ms/stránka) - top decile vs commercial reporting (Crystal Reports 30-60s, SSRS 20-45s, JasperReports 15-30s).

**Architektonická pravidla R1–R5 (async/IPopupDisplayer/JSON/MVVM/versioning) dodržena ve všech 3 commitech.**

## Fáze A.2: Zákaznické úpravy zastávkového JŘ ✅ (2026-05-11 → 2026-05-12)

Suspended tickety od DPKV (#10660) a TRANSDEV (#7937, #7959, #9036) z helpdesku. Detail viz [`customer-requirements-zastavkovy-jr.md §G`](customer-requirements-zastavkovy-jr.md).

| Krok | Tiket | Co | Datum |
|---|---|---|---|
| A2-A1 | #10660 | StopList: `DirectionArrowPosition` (Left/Right asymetricky) | 2026-05-12 |
| A2-A2 | #10660 | StopList: `MarkersPosition` (Before/After name) + auto-fit marker sub-cell | 2026-05-12 |
| A2-A3 | #10660 | StopList: `CurrentStopBgColor` + odstranění master toggle `HighlightCurrentStop` | 2026-05-12 |
| A2-A4 | #10660 | StopList: per-column header font (verifikace, již plně podporováno) | 2026-05-12 |
| A2-B1 | #10660 | DepartureGrid: per-line font v dvouřádkovém headeru (`HeaderLine2Style`) | 2026-05-12 |
| A2-B2 | #10660 | DepartureGrid: fix nestejného podtržení (`DrawTextDecorations` na header) | 2026-05-12 |
| A2-B3 | #10660 + #9036 | DepartureGrid: časová osa přes půlnoc + extended hours + „Roztáhnout na výšku" | 2026-05-12 |
| A2-C1 | #10660 | NotesLegend: DB-driven barvy poznámek (`PoznamkaSpoj.BarvaZnakuRGB/BarvaPozadiRGB`) | 2026-05-12 |
| A2-C2 | #10660 | NotesLegend: konfigurovatelná hlavička (verifikace, již podporováno) | 2026-05-12 |
| A2-E1 | #8772 | VisibilityExpression placeholdery - verifikace funkčnosti | 2026-05-11 |
| A2-A5 | #7937 | ⏸️ StopList per-row indent - odloženo (chybí Caché data flow, mimo TD scope) | - |
| A2-A6 | #7959 | 🔍 Filtr neobsluhovaných zastávek - již implementováno v Caché, vyžaduje verifikaci | - |
| A2-B4 | #8828 | ⏸️ Per-sloupek zastávky - odloženo (Caché datový průzkum + business decision) | - |

## Fáze B0.5 - Code Hygiene Sprint

| # | Co | Hotovo |
|---|---|---|
| H-1 | Smazat `ShowZkraceneOznaceniLinky` - ghost property v `DepartureGridElement` (nikde se nečetla) | 2026-04-21 |
| H-2 | `async void` → `async Task` + `AsyncRelayCommand` (EDControls) + `CommandAsync` helper; Open/Save/SaveAs + bonus GenerateCommand | 2026-04-21 |
| H-3 | `ElementTypes` statické konstanty v `Models/ElementTypes.cs` - nahrazeno ~30 magic strings v JsonDerivedType, `CreateElementByType`, `GetDefaultElementSize` a toolbar menu | 2026-04-21 |
| H-4 | JSON wire format verify - průzkum potvrdil 100% čistý JSON (Caché `obj.%ToJSON()` → .NET `JsonNode.Parse`); opraven zastaralý docstring `GetData` v `AgendaAPI.cls` + `cache-api.md` + přidána sekce "Wire formát pravidlo (R3)" | 2026-04-21 |
| H-5 | Silent catch v `PropertiesPanel.xaml.cs` color picker seed nahrazen za `SKColor.TryParse` - odstranil try/catch jako control flow | 2026-04-21 |
| H-6 | Nový `.claude/rules/new-element-checklist.md` - 10-kroková autoritativní reference (třída, ElementTypes, JsonDerivedType, 2 switches, toolbar menu, paleta, dynamická výška, GetSummary, docs); konsolidace z `mvvm-patterns.md` | 2026-04-21 |
| H-7 | `MessageBox.Show` → `IPopupDisplayer` - 5 VM (AiTemplateAssistant, TableEditor, ElementProperties, MainWindow, PdfImport); PdfImport odstranil 3 callback properties; YesNoCancel kept s TODO (IPopupDisplayer nemá ekvivalent); Warning → Info (WPF rozdíl byl jen ikona) | 2026-04-21 |
| H-8 | Extract `Controls/TemplateTreePanel/` - ~150 řádků ze `MainWindow.xaml.cs` přesunuto do samostatného UserControlu; `MainWindow` jen volá `TreePanel.Rebuild()`; smazán orphan `Windows/TemplateTreeDialog/` (nikdy se neinstanciovalo) | 2026-04-21 |
| H-9 | Extract `Windows/SaveSectionTemplateDialog/` - inline WPF konstrukce dialogu (~60 řádků StackPanel+TextBox+Button) z `MainWindowViewModel` přesunuta do Window+ViewModel+XAML dle závazného standardu; `SaveSectionTemplate(Section, string)` nyní čistá save logika | 2026-04-21 |
| H-10 | **N/A** - `TemplatePickerDialog` v kódu neexistoval. Template picker = `OpenFileDialog` (standardní Windows), section template picker = menu položky v "Add Section" dropdown. Žádný samostatný dialog k extrakci. Uzavřeno bez změn. | 2026-04-21 |
| H-11 | ⏭️ **Skip do potřeby** - `PropertyDescriptor.VisibleWhen` predikát. Aktuální ad-hoc skrývání v dialozích stačí, žádný blocker. | 2026-04-30 |
| H-12 | ⏭️ **Skip do potřeby** - `SharedPropertyDescriptors` helper. Duplikace deskriptorů akceptovatelná za stávající cenu. | 2026-04-30 |
| H-13 | ⏭️ **Skip do potřeby** - rozšíření error flow nad rámec `IPopupDisplayer`. Strukturované logování / telemetrie přidat až bude konkrétní požadavek. | 2026-04-30 |

## Sjednocení patternů + UX vyladění ✅ (2026-04-30 → 2026-05-01)

Kontinuální polish nad rámec původního B0.5 scope, motivovaný přípravou na 1:1 ladění zastávkového JŘ:

| Co | Stručný popis |
|---|---|
| **Sjednocené Header/Data zóny v panelu** | Princip „rychlý přístup × kompletní cascade" napříč StopList/DepartureGrid/NotesLegend/TableElement; helper `GetCellZoneBaseColorDescriptors()` |
| **Per-template editor preferences** | `Template.EditorPreferences` (grid + snap) - designové prostředí přežije reload |
| **Click-vs-drag threshold** | Klik na element nespustí soft-snap (4px threshold) |
| **ImageElement ScaleMode** | Renderer honoruje Fit/Fill/Stretch/None + tlačítko „Přizpůsobit poměru obrázku" |
| **Sub-pixelové rámečky** | Adaptive `RenderBorder` - pod 1px AA fractional, nad 1px pixel-snap |
| **Descriptor sort by group** | Stable sort v PropertiesPanel - bez duplicitních hlaviček |
| **Border v „Obecné" group** | Konzistentní rychlý přístup k základním vlastnostem |
| **DataRowHeight hoist** | Z duplicitních deklarací do `ScheduleElementBase` |
| **Úklid mrtvého UI** | Smazán `LineSpacing` z `CellStyleEditor` (CellRenderer ho ignoroval) |
| **Dokumentace stylingových patternů** | `mvvm-patterns.md` - Pattern 1 (single-zone) vs Pattern 2 (dual-zone) |

## Specializované JŘ elementy - společný základ ✅ (2026-03-18)

| Krok | Co |
|---|---|
| 1 | `ScheduleElementBase` - abstraktní základ (indexy, grouping, společné properties) |
| 2 | `StopListElement` + `DepartureGridElement` + `NotesLegendElement` - zastávkový JŘ (MHD) |
| 3 | Caché `ZastavkoveJrMhdAPI` - `GetData`/`GetMockData` + wire formát (ZASTAVKY, SKUPINY_DNU, ODJEZDY, POZNAMKY) |
| 4 | `SkiaHeightCalculator` - měření výšky JŘ elementů |
| 5 | `PrintDialog` - parametry + export reálných dat z Caché (`GetData`) + multi-data PDF |
| 6 | `CacheAgendaProvider` - ParseMockData vnořená pole, GetData, BuildRealData |

## Fáze A: Odladění MHD zastávkového JŘ ✅ (2026-05-11)

Cíl: hotová první kompletní verze šablony zastávkového JŘ a její review s kolegy.

> **Detailní architektonický dokument:** [`elementy-architektura.md`](elementy-architektura.md)

| Krok | Co | Stav |
|---|---|---|
| A | Hotová 1:1 šablona zastávkového JŘ + review s kolegy → seznam úprav v `customer-requirements-zastavkovy-jr.md` (= Fáze A.2) | ✅ (2026-05-11) |

### Poučení z implementace zastávkového JŘ - co vzít v potaz pro další JŘ typy

| Téma | Popis |
|---|---|
| **Jeden element → tři elementy** | Původní plán počítal s jedním `StopScheduleElement`. Ve skutečnosti se rozpadl na 3 nezávislé elementy - lépe odpovídá filozofii designeru (uživatel skládá ze stavebních bloků). Počítat s tím od začátku u dalších JŘ typů. |
| **Synchronní GetData** | `GetData` běží synchronně na UI thread - pro malé linky OK, pro velké může zamrznout UI. Vozový JŘ bude mít víc dat → async loading s progress barem bude nutný. Viz pravidlo R1. |
| **SKTypeface/SKPaint alokace** | ✅ Opraveno - `ScheduleElementBase` cachuje `SKTypeface` přes statický `ConcurrentDictionary<string, SKTypeface>`; `SKPaint` se vytváří a ukládá per instance cache. |
| **Caché business logika je rozsáhlá** | ZastavkoveJrMhdAPI má ~1200 řádků. Počítat s tím v odhadu pro další JŘ typy. |
| **Definovat layout před implementací** | Aby se neopakovala situace, kde se plán změní v průběhu. |

### Fáze A - hotové kroky

| Krok | Co | Stav |
|---|---|---|
| 7 | Per-column styling framework - CellStyle/ColumnVisualStyle, dva taby v DepartureGroupEditorDialog, InlineTextStyle pro minuty/anotace | ✅ (2026-03-21) |
| 7a | StopListElement - per-column styling analogicky k DepartureGrid (CellStyle pro záhlaví/data sloupců zastávek) + generalizace shared helpers do ScheduleElementBase + ColorPickerHelper + ActionId routing | ✅ (2026-03-23) |
| 7b | Ověření parametrů šablony - funguje přepsání element properties z template parameters (CASOVA_OSA, HODINY_OD, HODINY_DO, TISK_PROJETYCH_ZAST, TISK_SIPKY); fix merge parametrů do real data exportu; auto-open PDF; odebrání BAREVNE_ODLISENI + TISK_TARIFU | ✅ (2026-03-23) |
| 8a | Sjednocení textových nastavení - `TextStyle` (nullable POCO, CSS cascade) + `ResolvedTextStyle` (SkiaSharp helpery). Migrace: TextElement, TableElement, ScheduleElementBase + 3 JŘ elementy, PdfImport, CacheAgendaProvider. Breaking JSON change (flat props → `style`/`headerStyle`/`dataStyle`) | ✅ (2026-03-24) |
| 8b | Proporcionální škálování šablony + UX vylepšení - `ScaleTemplateCommand`, `ApplyScale()` na všech elementech, redistribuce výšek sekcí, výchozí nastavení nových elementů, `HourColumnPerGroup` | ✅ (2026-03-24) |
| 8c | AI-enhanced PDF import - dvoustupňový pipeline (heuristiky → AI enrichment); `AiImportEnricher` s kontextovými prompty; detekce rámečků, loga placeholder, margin fix, TextElement alignment fix | ✅ (2026-03-25) |
| 8d | AI Vision import pipeline - `AiVisionImportService` (composable prompty), `AiGuidedScheduleBuilder` (PdfPig geometrie), `AiVisionImportPipeline` (orchestrace); per-column font wrapping; section headers auto-hide; CellStyleVM font defaults; shared HttpClient | ✅ (2026-04-03) |

## Fáze B0 - UX Sprint ✅ (2026-04-20)

| # | Co |
|---|---|
| B0-1 | Strikethrough na TextStyle + descriptor; oprava tichého nefungování Underline |
| B0-2 | Více barcode typů - EAN-13, EAN-8, PDF417, DataMatrix; konsolidace palety |
| B0-4 | Precision fix - `MmMath.Round(0.1mm)`; resize end, ScaleTemplate, DistributeH/V, properties panel |
| B0-5 | DistributeH/V: přesný gap - zaokrouhlit, poslední element absorbuje zbytek |
| B0-6 | Normalizovat pozice (Ctrl+Shift+N) - dialog s krokem, live preview, CompositeCommand undo |
| B0-8 | Properties panel float formát - `"0.0#"` pro mm pole, round on commit |
| B0-9 | Grid size v UI - ComboBox 1/2/5/10mm v toolbaru, DesignerState.GridSize property |
| B0-11 | Inline validace - červený rámeček + tooltip místo textu pod polem |
| B0-12 | Copy Style / Paste Style (Ctrl+Shift+C/V) - StyleClipboard, kopíruje TextStyle + BorderColor/Width + BackgroundColor |
| B0-13 | Kontextový format mini-toolbar - B/I/U/S + L/C/R/J + ▲/▬/▼ nad properties panelem |
| B0-14 | Rotation property na TemplateElement - float, stupně, JSON serializace |
| B0-15 | Render rotace - canvas.Save/RotateDegrees(cx,cy)/Restore + IsRotated bool helper |
| B0-16 | Hit-testing rotovaných elementů - inverzní rotace test pointu kolem středu |
| B0-17 | Resize handles zakázány pro rotované elementy + rotation badge (angle label) |

---

## Další implementované oblasti

**UX audit - bezpečnost + frustrace + polish** ✅ (2026-03-18)
- Sanitizace VisibilityExpression (whitelist tokenizer + live validace v UI)
- Canvas fixed height + section clamping (sekce nepřesáhnou stránku)
- SectionRole (Header/Body/Footer) - patičky od spodu stránky
- Ctrl+X, Ctrl+0, context menu na canvasu, bound resize, potvrzení mazání sekce
- Nahrávání obrázků s auto-downscale (max 5 MB), SelectAll

**Zastávkový JŘ + Caché integrace** ✅ (2026-03-18)
- ScheduleElementBase + 3 elementy (StopListElement, DepartureGridElement, NotesLegendElement)
- ZastavkoveJrMhdAPI (~1200 řádků business logiky z legacy refaktoru, sloučeno z AgendaJRAPI + TiskMHD + TemplateDesignerMHD)
- CacheAgendaProvider: ParseMockData vnořená pole, GetData, BuildRealData
- PrintDialog: parametry + export reálných dat + multi-data PDF
- SkiaHeightCalculator: schedule element height calculation

**PageLayoutEngine + multi-page** ✅ (2026-03-17)
- Template + DataContext → `List<Page>` s PlacedSections; čistá logika (vrstva 3), žádný SkiaSharp/WPF
- Klasifikace sekcí (headers/footers/body), stránkování (KeepTogether/SplitAcrossPages), dvouprůchodový layout pro OnLastPageOnly
- Flow layout uvnitř sekcí, PerDataRow expanze, PAGE/TOTAL_PAGES injekce
- Multi-page preview (stránky pod sebou) + multi-page PDF export
- IElementHeightCalculator: SkiaHeightCalculator měří TextElement (word wrap) a TableElement

**Flow text v designeru (Word wrap + dynamická výška)** ✅ (2026-03-17)
- TextElement: `WordWrap=true` + `Height=null` → text se zalamuje, výška se dopočítá z obsahu
- `LineSpacing` property, vertikální zarovnání víceřádkového textu, fix `\r\n`
- InfiniteTextElement jako samostatný typ zamítnut - jedno chování řízené properties

**ElementGroup - seskupování elementů** ✅ (2026-03-12)
- `GroupId` property na `TemplateElement` - lehká logická vazba bez nested souřadnic
- Kliknutí na člen skupiny vybere celou skupinu; Alt+klik = jen ten jeden element
- Ctrl+G / Ctrl+Shift+G - seskupit / rozeskupit (plný undo/redo)
- Vizuální indikace: barevný badge v rohu, bounding box skupiny při výběru
- Strom šablony zobrazuje skupiny jako sbalitelné uzly
- Copy/Paste/Duplicate přemapují GroupId (klony nedědí skupinu originálu)
- PDF import automaticky seskupí grafiku podle typu a merge mode

**Locale / CultureInfo v šabloně** ✅ (2026-03-12)
- `Template.CultureCode` - kultura pro formátování čísel a datumů (cs-CZ, sk-SK, en-US, de-DE)
- Protéká celým pipeline: Template → DataContext.Culture → PlaceholderFormatter → TableElement
- Dropdown ve vlastnostech stránky; null = cs-CZ (výchozí); parsování vstupů zůstává InvariantCulture

**Drag & drop při přidávání elementů a sekcí** ✅ (2026-03-12)
- Drag element z toolbaru/palety na canvas - ghost preview, drop target highlight, clamp + snap
- Drag pole z panelu "Pole agendy" na canvas - vytvoří TextElement nebo append k existujícímu
- Drag & drop sekcí v levém panelu - přeřazení tažením, vizuální indikátor, `ReorderSectionCommand`
- Drag element mezi sekcemi - záměrně odloženo (nízká priorita, aktuální UX postačuje)

**Chytré duplikování elementů** ✅ (2026-03-12)
- Ctrl+D → šipka = směrové duplikování (kopie vedle originálu s 4mm mezerou)
- Ctrl+D bez šipky = plain duplikát (+5mm offset); neovlivňuje clipboard
- Multi-select, clamp na bounds sekce, plný undo/redo

**Přichytávání elementů k sobě (smart snap)** ✅ (2026-03-12)
- Edge↔Edge (4 kombinace), Center↔Center, Gap snap (4mm mezera) - bez kategorické priority, vyhraje nejbližší match
- Zvýraznění cílových elementů (šedý dashed rámeček), gap indikátor (černá čárka v mezeře)
- Element snap má přednost před grid snapem; osy X/Y nezávisle
- Toggle v toolbaru + menu; výchozí zapnuto

**Výrazový jazyk** ✅ (2026-03-12)
- Part 1 - podmínky a logické operátory: `{TISK_SIPKY}`, `{POCET} > 0`, `{TYP} == "PD"`, `&&`, `||`, `!`, závorky; VisibilityExpression na elementech a sekcích; ExpressionEvaluator + Parser + AST nodes
- Part 2a - agregace: `SUM({CENA})`, `COUNT()`, `AVG({POLE})`, `MIN({POLE})`, `MAX({POLE})` v TableElement; SummaryRows; AggregationResolver; záložka Souhrn v TableEditorDialog
- Part 2b - formátování placeholderů: `{POLE:N2}`, `{POLE:dd.MM.yyyy}` (.NET formáty); PlaceholderFormatter + DataContext rozšíření
- Part 2c - řetězcové funkce: `{POLE:LEFT(4)}`, `{POLE:UPPER}`, `{POLE:PADLEFT(3,'0')}`, `{POLE:TRIM}`

**UX polish - hotové položky**
- **f. Side panel vlastností** ✅ základ (2026-03-21) - inline záložka "Vlastnosti" v levém panelu; modální dialog zachován jako fallback
- **h. Ikonový toolbar** ✅ (2026-03-21) - 33 Fluent UI ikon, 7 skupin, ToolbarStyles.xaml

---

## 🖨️ PageLayoutEngine + stránkovaný preview (detail) ✅ (2026-03-17)

Preview v designeru = plný engine. "Tisk" = ten samý engine s reálnými daty → PDF.
Architektonické rozhodnutí (2026-03-17): engine je **integrovaný** v TemplateDesigneru, ne samostatný projekt. Oddělení do samostatných .csproj se udělá až bude druhý konzument.

### Vrstvy systému

Viz diagram v `CLAUDE.md` (sekce "Architektura - vrstvy systému").

### PageLayoutEngine - vstup/výstup

```csharp
class PageLayoutEngine {
    LayoutResult Layout(Template template, DataContext? data);
}
class LayoutResult { List<Page> Pages; int TotalPages; }
class Page { int PageNumber; List<PlacedSection> Sections; }
class PlacedSection {
    Section Source;        // odkaz na originální sekci
    float Y, Height;      // absolutní pozice na stránce (mm)
    float? ClipTop, ClipBottom;  // ořez pro SplitAcrossPages
    DataContext? Data;     // obohacená o PAGE/TOTAL_PAGES
}
```

### Implementační kroky ✅ vše hotovo

| Krok | Co | Stav |
|---|---|---|
| 1 | `PageLayoutEngine` - section ordering + repeating (OnEveryPage atd.) | ✅ |
| 2 | Stránkovaný preview - multi-page pod sebou se stíny a rámečky | ✅ |
| 3 | Flow layout - dynamická výška elementů posouvá elementy pod nimi | ✅ |
| 4 | Page breaking - sekce SplitAcrossPages se rozdělí přes stránky (ClipTop/ClipBottom) | ✅ |
| 5 | PerDataRow expanze - sekce se opakuje pro každý řádek dat | ✅ |
| 6 | PAGE/TOTAL_PAGES injekce + VisibilityExpression per stránku | ✅ |
| 7 | Multi-page PDF export - každá stránka = jedna PDF stránka | ✅ |

### Budoucí modularizace (až bude potřeba)

```
TemplateDesigner.Core.csproj      ← vrstvy 1+2+3 (žádný SkiaSharp, žádné WPF)
TemplateDesigner.Rendering.csproj ← vrstva 4 (SkiaSharp, žádné WPF)
TemplateDesigner.csproj           ← vrstva 5 WPF Designer
Edison.PrintService.csproj        ← vrstva 5 headless tisk z EDISONu
```

### Use cases díky oddělitelnosti

| Use case | Vrstvy | Popis |
|---|---|---|
| WPF Designer + preview | 1–5 | Aktuální aplikace |
| Tisk z EDISONu | 1–4 | Print dialog → GetData → engine → PDF |
| Batch tisk | 1–4 | Noční job: 200 faktur → 200 PDF |
| PDF jako služba | 1–4 | EDISON interně: dej PDF pro fakturu #123 |
| REST API | 1–4 | HTTP POST šablona + data → PDF response |
| Web preview | 1–4 | ASP.NET endpoint → PNG/PDF → iframe |
| Email příloha | 1–4 | Auto-generování PDF a připojení k emailu |

---

## Session 2026-07-04/05 ✅ - Architektonická rozhodnutí R1–R7 vyřešena

Celá rozhodovací tabulka R1–R7 uzavřena (přesunuto z aktivní roadmapy). Plný text principů:
[`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md).

| # | Rozhodnutí | Výsledek |
|---|---|---|
| R1 | Sdílené řádkové jádro | ✅ **implementováno** `Rendering/TableLayout.cs` (ResolveWidths + ResolveCascade), napojeno DynamicTable/LineTable/StopList. StopColumn/TableColumn NEsjednoceny (nulové riziko šablon), DepartureGrid mimo (pivot). 180/180 testů + render. Referenční doc `elementy-architektura.md` (návrhový doc smazán, git historie). |
| R1a | Render-loop sjednocení | ✅ **implementováno** - DynamicTable tři ruční smyčky → `ColumnRenderer.DrawRow`. Vyšší orchestrátor záměrně NE (LineTable pásy/přípoje se nevejdou, nízká hodnota). |
| R2 | Partial-fit kontrakt (svislá osa) | ✅ **kontrakt rozhodnut** `architektonicka-rozhodnuti-agendy.md` (`IRowPaginatedElement`); implementace až s paginačním/bin-packing trackem (svislá+vodorovná C3 spolu). |
| R3 | Datový kontrakt / prezentační nápovědy | ✅ **rozhodnuto** `cache-api.md` - v1 = `FORE_COLOR`/`BACK_COLOR` + princip + evoluce jen-aditivně; širší seznam (ZVYRAZNENI…) na F1 dle RG usage. |
| R4 | Headless tisk | ✅ **rozhodnuto** → akční krok „tisk = shortcut z agendy bez návrháře přes PrintEngine fasádu"; impl s pilotem (blok A). |
| R5 | Fonty embedded v .exe | ✅ **rozhodnuto** (jde: `SKTypeface.FromStream`) → akční krok/TODO (základní Helvetica/PostScript s licenční poznámkou + JŘ Timetable symboly = C2); impl s pilotem/C2. |
| R6 | Systémové šablony s buildem | 🟡 rozhodnuto (read-only s releasem, DB jen zákaznické); impl v bloku B. **Navázaný otevřený bod:** koncept centrální správy šablon napříč agendami (viz aktivní kroky). |
| R7 | Pilot-first | obecný track = spustit pilot k zákazníkovi; žije v „Aktivní / nejbližší kroky", ne architektonické rozhodnutí. |

---

## Session 2026-07-05 ✅ - Tabulkové featury A1–A3 + klasifikační pravidlo

Rychlé samostatné položky nad čerstvým sdíleným tabulkovým jádrem (R1). Vše review + testy + Caché
schema. Commity: `ce2fffc`, `3d279c5`, `c1b878d`, `0402c39`, `9313105`, `be873c6`, `d0caccd`.

| # | Featura | Výsledek |
|---|---|---|
| A1 | Podmíněné řádkové featury | ✅ **viditelnost řádku** (`RowVisibilityExpression`) + **zvýraznění řádku** (`RowHighlightExpression`+`RowHighlightColor`, jen pozadí) přes sdílený `Models/Expressions/RowConditions` (výraz nad daty řádku, fail-open). Zvýraznění na **DynamicTable + StopList + LineTable**, viditelnost DynamicTable+StopList+LineTable. DepartureGrid mimo (pivot). Aditivní/default-off = behavior-preserving pro gold StopList i pilot LineTable. Řádkový POCO nese `SourceRow`. |
| A2 | Počítaná pole | ✅ **aritmetika ve výrazovém jádru** (`+ - * /`, unární minus, priorita, závorky; `ExpressionEvaluator.EvaluateToString` - číslo invariantně, `FormatNumber` odstraní IEEE-754 artefakty + NaN/Inf→prázdno). **`TableColumn.Expression`** = počítaný sloupec DynamicTable (`{CENA}*{MNOZSTVI}`, přebíjí Field, formátovatelný). Bonus fix: editor sloupců dřív `Format` neukládal. |
| A3 | Min/max řádků | ✅ **`MinRows`** doplní prázdné řádky do minima (pevná výška boxu - faktura), **`MaxRows`** ořízne. DynamicTable-only. Render 3 typy řádků (placeholder/data/padding), výška↔render parita, kombinuje s filtrem viditelnosti. |
| - | Klasifikační pravidlo | 📐 `elementy-architektura.md §0b` - 3 kategorie featur (nad daty řádku → sdílet / struktura sloupce+počet řádků → DynamicTable / doménové render → element-specifické). Generické featury pro data dostupné přes DynamicTable na tutéž kolekci. NEslučovat StopColumn/TableColumn. |

**Vědomě odloženo (ne dluh):**
- **Rozšíření podmíněného formátování** (seznam pravidel místo 1, barva textu, per-sloupec/buňka) →
  až dle reálné produkce (rozhodnutí uživatele 2026-07-05: „ted bych to nechal takto").
- **A4 sloučené buňky** (`ColSpan`/`RowSpan`) - větší layout kus, zůstává v aktivní Fázi B.
- **Grouping** (`GroupHeader`/`GroupFooter`) + **vnořené datasource** - backlog bez customer demand.

---

## Session 2026-07-05 ✅ - B1: embedding fontů do .exe (mechanismus, R5)

Commit `fd7b267`. `FontResolver` refaktorován na sdílené cachované typefacy + auto-discovery embedded
fontů z manifest resources pod `Fonts/` (glob `<EmbeddedResource>` v .csproj → single-file .exe).
Priorita `embedded → OS → Arial → Skia default`; embedded = deterministický výstup nezávislý na
nainstalovaných fontech. Varianty Bold/Italic = samostatné soubory. Všechny cesty renderu
(text/buňky/JŘ/legenda) už tečou přes `FontResolver.Resolve` → embedding platí všude. Testy 213/213
(4 nové). `dotnet/Fonts/README.md` dokumentuje konvenci + licence.

**Zbývá jen (až bude potřeba):** dropnout konkrétní `.ttf` do `Fonts/` - základní free bezpatkový
(Arimo/Liberation, NE Helvetica/Arial) a **JŘ symbolový Timetable_nove(+SK)** = C2 knižního JŘ
(vyjasnit zdroj/licenci). Mechanismus je připraven, kód se už nemění.

---

## Session 2026-07-06 ✅ - Layout: „sekce vyšší než stránka" (Slice 1+2 + fix)

Návrhové plátno už není omezené na jednu A4. Commity: `a88763a` (Slice 1), `56c1837` (Slice 2),
`12fc8d9` (fix dvojitého rámečku), `da198f3` (review fix footer WYSIWYG + mrtvý kód).

- **Slice 1 - plátno roste:** `DesignerCanvas.GetSectionOrigins` skládá sekce souvisle v pravé
  (neclampnuté) výšce; `TotalPaperHeightPx` (okraje + Σ výšek sekcí, min 1 stránka); `Render` +
  `UpdateCanvasSize` + `RequestCanvasRefresh` na rostoucí výšku; `SectionHeightPx` místo clampu,
  `GetClampedSectionHeightPx`/`GetPageBottomY` odstraněny.
- **Slice 2 - dělící čáry:** `DrawPageBreakLines` - přerušované modré čáry + „Strana N" po výšce
  tisknutelné oblasti, jen když obsah > 1 stránka. Varianta 1 (geometrické vodítko) - vědomě ne
  pixel-přesné vůči preview; přesný stránkovaný výsledek zůstává v preview/PDF.
- **Footer** kotven ke spodku rostoucího papíru (WYSIWYG s preview u 1 stránky).
- **Fix (pre-existing):** split sekce (SplitAcrossPages) kreslila rámeček elementu i na 2. straně -
  `PreviewCanvas.RenderPage` border-pass běžel bez clipu → ořezán na rámec stránky (opraveno i pro
  PDF/PNG, sdílený `RenderPage`). Regres test `SplitSectionPaginationTest`.
- Nezávislý review: bez BLOCKER/MAJOR, bez regrese jednostránkových šablon, hit-testing konzistentní.
  Testy 214/214.

**Navazuje (aktivní track):** R2 (svislé stránkování řádků - zpřesní zlomy na celé řádky) → C3 (vodorovný Strih).

---

## Session 2026-07-07 ✅ - Pre-produkční zámek struktury šablony (blok PA) + úklid roadmapy

Cíl: připravit v1 do produkce tak, aby se **nemusela měnit struktura šablony** (breaking JSON) v příští
verzi. Grounded analýza (4 průzkumy kódu) - doc `produkce-pripravenost-2026-07-07.md` smazán po vytěžení (git historie).
Blok PA vyřešen, vše behavior-preserving (uživatel nepozná, jen zamčená struktura). Commity `23e6a90`
(analýza+checklist), `6dadb66`, `484881c`, `f1e01e9`, `cf43a56`. Testy 218 → 229.

- **PA1 - klíče sloupců → konstanty.** `StopColumn.Key` (`"Zone"`/`"StopName"`/`"Trip"`…) byly
  magic-string literály roztroušené ve switchích, ale persistují se do JSON → přejmenování by tiše
  rozbilo uložené šablony. Centralizováno do `Models/ColumnKeys.cs` (vzor `ElementTypes`) +
  `ColumnKeysTests` zamyká wire hodnoty. Klíč = interní směrovací identifikátor renderu (C#), ne
  datové pole (kontrakt Caché) → proto C#.
- **PA2 - mrtvý `ColorRules`.** Domnělé „dva modely" zvýraznění řádku: `List<ColorRule>` na
  DepartureGrid byl mrtvý kód (nikde nekonzumován) → odstraněn. Živý zůstává 1-pravidlový
  `RowHighlightExpression`, který strukturu nezamyká (multi-pravidlo lze přidat aditivně jako budoucí
  `RowColorRules`). `ColorRule` třída ponechána jako rezervovaný budoucí model.
- **PA3 - Fixed `TableElement` zmražen.** Šel z UI vytvořit jen zdánlivě (paleta/menu = dynamická),
  ale továrna měla mrtvou větev → odebrána; nově ho nic nevyrobí. Tripwire test (`ElementFactoryTests`)
  + docstring. Zachováno: třída, `JsonDerivedType` (legacy read), `TableEditorDialog` (legacy edit) →
  staré šablony se otevřou, Fixed tabulka připravená k oživení (pár řádků zpět).
- **PA4 - verzování dokumentováno.** Nález: migrátor je hotový **i otestovaný** (9 testů) - obava
  „pipeline neběžela" neopodstatněná. „Bump na 1.1.0 teď" **zamítnuto** (proti konvenci *bump jen pro
  breaking change*; navíc falešné WasMigrated). Zapsáno: historická flat→style změna jako
  nemigrovatelná + rozhodnutí NEbumpovat (`template-schema-versions.md`). Kosmetika „1.0"/„1.0.0"
  funkčně irelevantní, neřešena.
- **PA5 - logování neznámých polí = YAGNI (zavřeno).** Zvažováno `[JsonExtensionData]` + `Debug.WriteLine`.
  Zamítnuto jako okrajové: forward-compat už hlídá výjimka migrátoru, budoucí breaking změny mají
  migrace, drift chytají round-trip testy. Cena (trvalá změna 3 model tříd) > přínos. Přídavek je
  ~20 min kdykoli, když vznikne reálná potřeba.

**Úklid roadmapy 2026-07-07:** C3 (2D bin-packing) přesunut z layout tracku do Fáze C+D. Dvě
překrývající se produkční sekce sloučeny do jedné (PB provoz / PC governance). Sekce R1–R7 (dořešena
2026-07-04/05) a produkční done-items (A1+A2 launch, D1 embedded fallback, D2/E1/F1/F2/F5) přesunuty sem.

**PB triáž 2026-07-07 (produkční provoz):** release deploy (build .exe + Caché třídy) uznán jako
**standardní EDISON release proces**, ne TD-specifický úkol → bývalé D3/D4/PB-D1/PB-D2 odstraněny.
`SelfContained=false` / .NET 10 (PB3) neřešíme - runtime je prerekvizita celé suity (EDISONMAIN2 taky
`net10`). Build-vázaný-na-stroj (PB4) - kdo dělá release, má `C:\Edison`; CI je budoucí nice-to-have.
**Hotová drobná hygiena:** PB7 doc drift (CLAUDE.md .NET 9→10, PdfPig 0.1.9→0.1.13); PB6 - `ScheduleDetectorTest.cs`
už byla dávno smazaná (jen stará bin/obj artefakta), smazány mrtvé rozbité scratch soubory
`test-detector.csproj`/`test-detector-main.cs`/`RunDetectorTest.csx`; PB1 - vyprázdněn tracked
`templatedesigner.appsettings.json` (dev creds martin.hubata/MLP 167/interní IP se zapékaly do .exe),
přesunuto do gitignored `local.json`; produkce session stejně bere z launch args, standalone dev z local.json.


---

## Audit 2026-07-07 - Blokery (B1–B3) vyřešeny

Všechny tři produkční blokery z auditu hotové (přesunuto z aktivní audit sekce).

- **B1 - KV hardcode v MHD tiskové cestě.** `POPIS_DOPRAVCE` místo natvrdo „…Karlovy Vary, a.s.……."
  čte reálného dopravce linky z `ALVA.MHD.LSO` (helper `PopisDopravce`: `DruhDopravceID` → vlastní firma
  `$$gIdFa^MlJFapi` / cizí `$$gDopFa^JRapi`, defensivní). `NEJEDOU_SPOJE` napojen na generátor `TiskyFce`
  (gated flagem linky). `TITULEK`/`POZNAMKA_POD_DOPRAVCE`/`INFORMACE` (MHD nemá zdroj) odstraněny ze
  schématu i GetData (AD si je nechává). Schéma ověřeno živě. Commit `ddb1dfa`. Zbývá eyeball: reálné
  jméno dopravce na MHD firmě (defensivní → nejhůř prázdno). Součástí i M1 guard + oprava MHD picker crashe.
- **B2 - export tiše nahrazoval reálná data mock daty při selhání Caché.** `GetData` na `RestStatus.Error`
  /výjimku hází `CacheAgendaException` místo `null`; `null` = jen „legitimně žádná data". Chyba propadne
  přes export/preview/PrintDialog (žádné false-success PDF). 252/252 testů. (Řešeno v paralelní session.)
- **B3 - mrtvý secret + rotace klíče.** Bludný `dotnet/appsettings.local.json` (špatný název, TD ho nečte -
  čte prefixovaný; untracked/gitignored) smazán ručně (bez git změny). Pozor: generický `appsettings.local.json`
  je LEGIT pro EDISONMAIN2 (SettingsLoader ho čte) - nemazat tam. Rotace API klíče = na uživateli.
