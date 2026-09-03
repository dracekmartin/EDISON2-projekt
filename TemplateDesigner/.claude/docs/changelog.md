# Changelog

## 2026-09-03 (7) - 📝 poznámky jsou prvek i v linkovém jízdním řádu (`B117` krok 1)

Legenda linkového jízdního řádu se dosud skládala **dynamickou tabulkou** nad kolekcí poznámek.
Uživatel tím dostal jen sloupce textu - žádný inverzní box u negativních značek, žádné řazení
podle typu, žádné vlastní odsazení značky. Prvek Poznámky přitom existoval, jen byl vázaný na
zastávkové jízdní řády.

V paletě linkového jízdního řádu jsou teď **dva prvky**: tabulka zastávky × spoje a Poznámky.

⚠️ **Rozhodnutí, proč to tak nebylo, nikde nebylo** - vzniklo to tak, že dynamická tabulka
byla nejsnazší cesta (záměr Jan 3. 9.: doménová logika patří do prvku, dynamická tabulka je na
raw data).

**Prvek nad linkovými daty funguje bez úpravy kontraktu.** Agenda posílá u poznámky typ `nz`,
`znak` nebo `text` a prvek to mapuje na negativní značku, speciální znak a text - stejně jako
u zastávkových. Kolekci poznámek posílají obě agendy pod týmž klíčem.

⚠️ **Co u linkových zůstane bez efektu:** filtry na kategorie obsahu (bezbariér,
na znamení, přestupy, písmena variant) - to jsou zastávkové pojmy a linkový posílá kategorii
prázdnou. Nastavení „počet poznámek na řádek per druh", které má starý modul, prvek zatím
neumí vůbec - to je `#7487` a druhý krok `B117`.

**Výchozí šablona linkového jízdního řádu už legendu skládá tím prvkem** (přepsáno po vyzkoušení,
rozhodnutí Jan 3. 9.). Kolekce poznámek se nemění, takže **hotové šablony s dynamickou tabulkou
fungují dál** - jen nová šablona ze Systémových M-line dostane prvek. Zkušební vzor pro linku 940
ten prvek měl už dřív, takže se nemusel měnit.

**V paletě i v nabídce vkládání jde tabulka před poznámkami** - poznámky jsou doplněk. Do teď
u linkových vycházely první, protože jejich tabulka je v tom výčtu za nimi.

Ověřeno: .NET **1463/1463** (9 nových testů na vazbu druh agendy → prvky, na kterou žádný test
nebyl). Mutace „vrátit prvek mimo nabídku" test shodí.

## 2026-09-03 (6) - 🧪 .NET testy běží sekvenčně, falešné pády jsou pryč (`B116` část 1)

`SheetPdfExportTests.Opakovani_KazdaStrankaNaCelyList` padal **občas** - v PDF nebyl žádný text,
přitom stránky tam byly. Izolovaně neselhal nikdy, takže to nebyl test sám.

**Příčinou byl paralelní běh kolekcí.** Sada sdílí procesně globální stav: kořen kaskády
`DocumentDefaults.Style` a cache fontů a typefaces SkiaSharpu. Doloženo měřením - **2 pády ze 14
paralelních běhů, 0 z 8 sekvenčních**.

⚠️ **Kolekci `StyleCascade` to nenahrazuje kosmeticky.** Ta kolekce existovala právě pro sdílený
kořen kaskády, ale udržovat seznam tříd, které do ní patří, je zdroj téhle chyby:
`EDInputFormSchemaDriftTests` do `DocumentDefaults.Style` **zapisuje** a v kolekci nikdy nebyl.
Sekvenční běh drží i testy, na které se při psaní zapomene.

Cena je 1-2 s → 4-11 s pro 1454 testů. ⚠️ Kdyby to začalo vadit, **opravou není vrátit paralelizaci**,
ale zbavit render a kaskádu procesně globálního stavu; je to napsané i v `xunit.runner.json`.

**Caché polovina `B116` zůstává otevřená** - paritní test proti starému modulu se dnes při 7 bězích
(4× izolovaně, 3× celá sada) **nereprodukoval**, takže není co opravovat naslepo. Caché testy navíc
paralelně neběží, takže tam je příčina jiná.

Ověřeno: .NET **1454/1454** osmkrát za sebou, Caché **146/146** třikrát.

## 2026-09-03 (5) - 🚆 linkový JŘ (AD) je zpátky v nabídce a je výchozí

Pokračuje dovývoj linkových jízdních řádů, takže agenda je zase potřeba v aplikaci (rozhodnutí Jan
3. 9.; 1. 9. se kvůli vydání skryla). Nová šablona na ní i startuje.

⚠️ **Před dalším vydáním se musí znovu rozhodnout, jestli má jít k zákazníkům** - `Released = 1`
znamená, že se ukáže všem, jakmile se vydá nová verze.

⚠️ **Skrývá se na dvou místech** a obě se musí změnit spolu: `Parameter Released` v Caché třídě
a konstanta ve výchozí šabloně na klientovi. Jinak skrytí obejde samotný start aplikace - přesně
tak byla 1. 9. linková agenda vidět, i když měla být skrytá.

**Test už nekontroluje konkrétní kód agendy, ale tu vazbu**: výchozí agenda musí mít v `.cls`
`Released = 1`, což se čte ze zdrojáků v repu. Dřív test čekal pevně „ZASTAVKOVY_JR_AD", takže by
ho každé přepnutí rozbilo a nikdo by z pádu nepoznal, jestli je to chyba, nebo záměr. Testovací data
v té sadě navíc přestala používat reálné kódy agend - s nimi stačilo přepnout výchozí agendu na tu,
která je v testu „skrytá", a spadl jiný test na fallbacku.

⚠️ **`Released` je serverová hodnota**, ne součást exe: nasazení `.cls` na dev změní nabídku agend
i těm, kdo mají starší exe. Seznam agend se navíc čte jednou při startu, takže běžící aplikace
změnu nepřebere.

Ověřeno: .NET **1454/1454**, Caché **146/146**, a voláním serveru (`ListAgendy` vrací
`LINKOVY_JR_AD` s `released: true`). Mutace „skrýt agendu a nechat ji jako výchozí" test shodí.

## 2026-09-03 (4) - 🔍 hledání ve výběru linek najde i linku za dvoustovkou (`B103`)

Dialog výběru linek si načetl **prvních 200 linek skupiny** a hledací pole pak filtrovalo jen je.
Napsané číslo linky, která je dál, nevrátilo nic a vypadalo to, že linka v datech není - přitom tam
byla (nález Jan 1. 9.: `380761` v CB_CSADAD skupině 1 se přes výběr vybrat nedalo, přes server se
našlo).

**Načítá se celá skupina** (rozhodnutí Jan 3. 9.). Změřeno na devu, kolik jich reálně je: nejvíc
**240** (CB_CSADAD skupina 1), pak 228, jinde desítky - jsou to stovky, ne tisíce, takže stránkování
nemá co řešit a hledání na klientovi funguje nad úplným seznamem. Limit tedy řezal o čtyřicet
a hledaná linka byla 233. v pořadí.

⚠️ **Limit nešlo jen smazat**: agendy čtou `i 'limit s limit = 50`, takže „bez limitu" by znamenalo
padesát řádků, tedy ještě méně. Posílá se strop `MAXINSTANCI = 20000` - **pojistka proti chybě
v datech** (poškozený globál by jinak stáhl celý `^JRL`), ne omezení pro uživatele; proti naměřeným
stovkám je o dva řády výš.

Ověřeno: Caché **3 nové testy** (celá skupina se vrátí, konkrétní linka z nálezu je ve výsledku,
strop je řádově nad realitou). Mutační kontrola: vrácení limitu na 200 shodí všechny tři s přesnými
čísly („výběr vrátil 200 linek, skupina má 240"). Ověřeno i voláním serveru - `LoadList` vrací 240
řádků a linka `380761` je 233.

## 2026-09-03 (3) - 🔤 dvě řady písmen u minut se už nekryjí (`B113`)

Varianty tras brala zadrátovaná abeceda `A, B, C, D…`, rozdíly tras sadu `BCEFGHIJOQRWeijqsvwxz`.
Od druhé písmenkované varianty výš se řady prolezly, takže **jedno písmeno u minuty mělo dva významy**
a legenda ho vysvětlila cizím textem: spoj jedoucí variantou „E" si čtenář spároval s „jede jen do
zast. Babice". Na vývěsce pro cestující je to dezinformace, ne kosmetika (nález kolega + Jan 2. 9.
na lince 111; pojistka „bez sloupce Varianty tras se písmena netisknou" byla první polovina, tohle
je ta vlastní oprava).

**Obě sady se teď berou z nastavení u linky** (EDISON → linka → Nastavení k tisku → Odjezdník MHD):
„Volné značky" pro rozdíly tras, „Volné značky pro minutové trasy spojů" pro varianty. Znak po znaku
od začátku, po vyčerpání se zdvojují („AA", „DD") - stejná mechanika jako v legacy.

**Prázdné pole → výchozí disjunktní sady:**

- **rozdíly tras `BCEFGHIJOQRWeijqsvwxz`** - bez jediné změny, je to 1:1 legacy a tedy parita.
- **varianty `ADLMNPTY`** - vzestupně a disjunktně. Vyškrtané je vše, co u minuty jako značka už je:
  sada rozdílů **včetně malých písmen** (velikost se nerozlišuje, „V" vedle „v" ve výšce písma
  odjezdů nikdo nerozezná), `R` místenka, `K` kola, `u` pokračující spoj, `H`/`F` bezbariér.

⚠️ **Vlastní zápis se bere DOSLOVA, bez filtrování kolizí** (rozhodnutí Jan 2. 9.). Kdo si napíše
„ABCDE", dostane to i s kolizí - je to jeho volba a tiše mu do ní sahat je horší. Východisko je,
že nastavitelné jsou **obě** sady, takže lze přesunout tu druhou.

⚠️ **Vzestupně, ne od konce abecedy** (první návrh byl `Y, T, P, N, M, L, D, A`): `A` v sadě rozdílů
není, takže první písmenkovaná varianta zůstává `A` jako dnes. **Linka se dvěma variantami se tím
nezmění vůbec** a řady se rozejdou až u třetí (`D` místo `B`), což je právě místo, kde kolize vznikala.

⚠️ **Vyplněné pole variant zapne písmena i ve starém modulu** - ten pro ně nemá žádný výchozí obsah
(„UVWXYZ" v `JRapiLinPrtMhd1.mac:27` je jen komentář), takže dnes variantní značky netiskne vůbec
a na žádné z 325 prověřených linek to pole vyplněné není. Paritu neporušuje ani jedna volba, ale kdo
pole vyplní kvůli nám, uvidí písmena i ve staré sestavě.

**Zlaté odtisky se přerazily záměrně a rozdíl je doložený** (R6). Počty stránek, zastávek, odjezdů
i poznámek jsou u všech tří linek **shodné do jednoho**, takže se neztratil žádný spoj ani řádek.
A hlavně **izolovaná kontrola**: s výchozí sadou dočasně vrácenou na `ABCDEFGHIJKLMNOPQRSTUVWXYZ`
prošly všechny tři odtisky s původním md5, takže rozdíl nepochází odjinud než z té sady. Nové testy
disjunktnosti naopak v té konfiguraci spadly („písmeno B je i v sadě rozdílů tras"), což je druhá
polovina téhož důkazu.

⚠️ **Řešení může být dočasné** (Jan + kolega 3. 9.): nastavení sad u linky se možná ještě upraví.
Dnešní stav je průběžný krok, který zavírá dezinformaci na vývěsce, ne finální tvar nastavení.

Ověřeno: Caché **143/143** (dva nové testy - disjunktnost sad a písmena z vlastní sady).

## 2026-09-03 (2) - 🔤 dlouhý text se zmenší, aby se vešel do prvku (`B115`)

Textový prvek jako jediný text **tvrdě ořezával**: co se do jeho výšky nevešlo, se prostě nenakreslilo
a uživatel o tom nevěděl (podnět kolega 3. 9.). Napříč aplikací přitom platí pravidlo **pevná výška
zmenší písmo, prázdná nechá prvek dorůst** - buňky všech tabulek i prvky jízdních řádů to tak dělají
a nic se u nich nezapíná. Textový prvek měl z toho pravidla jen druhou polovinu.

Nově se text zmenší, aby se vešel: **na výšku** při zalamování, **na šířku** u jednořádkového. Bez
volby a i v hotových šablonách (rozhodnutí Jan 3. 9.) - nová volba by byla nekonzistentní se zbytkem
aplikace a v panelu by přidala zaškrtávátko, jaké jinde není.

⚠️ **Na rozdíl od tabulek a jízdních řádů má textový prvek dolní mez** (rozhodnutí Jan 3. 9.):
**polovina nastavené velikosti, nikdy pod 4 pt**. Pod mezí se text ořízne jako dřív, ale **řekne se
to** - dosud se ořez nehlásil vůbec: *„Text: písmo zmenšeno na 50 % a text se přesto nevejde -
zkraťte ho nebo zvětšete prvek"*. 4 pt je dolní hranice, kterou dovolí zadat pole Velikost, takže mez
nevyrobí písmo, jaké by uživatel nesměl nastavit ručně; polovina drží autorův záměr (nadpis 40 pt se
nezmenší pod 20 pt).

Zmenšení se hlásí stejnou cestou jako u prvků jízdních řádů (stavový řádek i souhrn tisku) - jinak by
šablona tiskla jinou velikost, než jakou panel ukazuje, a nikdo by to nepoznal.

⚠️ **Zalamování řádků je u textového prvku nově VÝCHOZÍ VYPNUTÉ** (rozhodnutí Jan 3. 9. po zkoušení).
Nový textový prvek je typicky nadpis nebo jeden údaj na jednom řádku, kde zalomení rozhodí layout víc,
než pomůže. Obě chování se tím rozdělila čitelně: **bez zalamování** se text zmenšuje na šířku,
**se zalamováním** nejdřív pokračuje na dalším řádku a zmenšuje se teprve, až mu nezbývá místo na výšku.
**Hotové šablony to nepotká** - hodnota se do JSON zapisuje vždy (serializace vynechává jen null),
takže si každý prvek nese tu svou; test to drží. Výchozí hodnotu má i formulář (`TextElementEdit.cls`)
a rozpor mezi ním a C# chytnou driftové testy - ověřeno mutací.

**Na plátně návrháře se nezmenšuje**, jen v náhledu a v tisku: na plátně je text ještě s placeholdery
`{POLE}`, takže by se škála počítala z jiné délky, než jaká se vytiskne.

⚠️ **Past, na kterou se přišlo při testech:** „jak hluboko text v prvku sahá" **nerozliší ořez od
zmenšení** - oříznutý text v plné velikosti zaplní prvek až ke spodní hraně stejně jako zmenšený
(63 z 64 px v obou případech), jen mu chybí konec. A zmenšené písmo nekreslí **víc** řádků, ale méně:
na řádek se vejde víc znaků. Pixelový test proto měří **výšku vykreslených glyfů**.

Ověřeno: .NET **1452/1452** (11 nových testů). Mutační kontrola: odpojení škály od kreslení shodí 2,
zrušení meze 3.

## 2026-09-03 (1) - 📍 varování „obsah se nevejde" řekne, kde a co (`B114`)

Červený rámeček kolem stránky náhledu a hláška ve stavovém řádku říkaly jen **„Obsah se nevejde
na stránku"**. Uživatel z toho věděl, že je problém, ale ne na které stránce ani co ji přetéká -
u dávky dvaceti vývěsek neměl kde začít (podnět kolega 3. 9.).

Nově hláška jmenuje **stránku, sekci a prvek, který zabírá nejvíc místa**, včetně přesahu v mm:
*„sekce „Vývěska" nevejde na stránku, nejvíc místa bere Poznámky (přesah 12,4 mm, strana 2)"*.
Tentýž popis je i v souhrnu tisku, kde dosud stálo jen „3 z 12 stránek přetéká" - tisknout může
jiná linka než v náhledu. Tooltip nese cestu k nápravě (zvětšit stránku, zvětšit sekci, zmenšit
prvek).

**Tentýž prvek přes všechny stránky je jeden záznam**, ne dvacet: u vývěsky se stránkou na zastávku
vypadne stejná chyba na každé z nich, takže výpis po stránkách by z jedné chyby udělal dvacet
řádků. Do tří stránek se vypisují čísla, nad to počet a první výskyt.

⚠️ **Zjištění při práci: prvek nemůže přesáhnout svou sekci.** Engine dává sekci
`max(nastavená výška, obsah)`, takže rostoucí prvek ji nafoukne s sebou. Podmínka „prvek přesahuje
sekci" v detekci přetečení tedy nemohla nastat - byl to mrtvý kód. Reálné přetečení je vždy „sekce
se nevejde na stránku" a užitečná informace je, **kdo ji nafoukl**: hláška rozlišuje prvek
(zmenšit ten prvek) od nastavené výšky sekce (zmenšit sekci nebo zvětšit stránku), protože náprava
je jinde. Ukazovat na prvek i tam, kde se do sekce vejde, by uživatele poslalo opravovat něco,
co za to nemůže.

**Názvy prvků mají teď jedno místo** (`ElementNames`). Byly opsané na třech - hláška o zmenšeném
písmu, import z PDF a paleta - takže přejmenování dvou prvků 1. 9. („Mřížka odjezdů" → „Minuty
odjezdů", „Legenda poznámek" → „Poznámky") se muselo dělat trojmo a v příručce zbyly staré názvy
ještě 2. 9. Test drží, že jméno v hlášce je totéž jako na tlačítku palety.

Ověřeno: .NET **1441/1441** (24 nových testů). Mutační kontrola: návrat k pevnému textu „Obsah se
nevejde na stránku" shodí 10 z nich.

## 2026-09-02 (1) - 🔤 písmeno varianty trasy se tiskne jen tam, kde je čím vysvětlit

Kolega poslal vývěsku linky 111 s dotazem, co znamená „A" za časem odjezdu.
V legendě jsou popsané B, C, E, F, ale A tam není a nemá být: **není to rozdíl trasy, ale označení
variantního sloupce jízdních dob** a vysvětluje ho záhlaví toho sloupce v seznamu zastávek.
Jeho šablona ten sloupec zapnutý nemá, takže písmeno u minuty nemělo na stránce s čím spárovat.

Nově se písmena variant u minut tisknou **jen tehdy, když má aspoň jeden seznam zastávek viditelný
sloupec „Varianty tras"**.
Bez něj (a v šabloně, kde seznam zastávek vůbec není) se nevytisknou.
Je to totéž pravidlo, které už platí u značky „na znamení" a u přestupů: značka a její vysvětlení
visí na jedné volbě, takže nemůže vzniknout značka, které nikdo nerozumí.

⚠️ **Bylo to horší než nevysvětlené písmeno - legenda ho vysvětlovala CIZÍM textem.**
Řady písmen se dnes kryjí: varianty berou A, B, C, D, E…, rozdíly tras B, C, E, F, G… ze sady u linky.
Spoj jedoucí variantou „E" tedy dostal u minuty „E" a čtenář si k němu v legendě přečetl
„jede jen do zast. Babice,Zvěřetice,rozc.1.0", což o tom spoji neplatí.
Na vývěsce pro cestující je to tichá dezinformace, ne kosmetika.
**Oddělení těch dvou řad je samostatná úloha `B113`** - tenhle krok jen zabrání tomu, aby písmeno
vzniklo tam, kde ho nejde přečíst správně.

⚠️ **Nové šablony to nepotká.**
Nově vložený seznam zastávek má sloupec „Varianty tras" viditelný z výroby, takže tisknou písmena dál.
Rozbité byly šablony převedené migrací 1.7.0 → 1.8.0, která sloupec zapnula jen tam, kde bylo dřív
zaškrtnuté obojí (sloupec „Čas jízdy" i staré zaškrtávátko variant).

**Komu chybí písmena a chce je zpátky:** v prvku seznamu zastávek zapnout sloupec „Varianty tras"
a schovat „Čas jízdy" - jinak tam budou časy základní trasy dvakrát (variantní sloupec bez písmene
je při výchozím řazení „dle četnosti ze zastávky" tatáž hlavní trasa).

Ověřeno: .NET **1417/1417** (5 nových testů, jeden z nich reprodukuje přesně ten případ z vývěsky -
písmeno varianty „E" proti legendě „E" jako rozdílu trasy).

## 2026-09-01 (10) - 🙈 linkové jízdní řády skryté před vydáním

Ve vydávané verzi mají být **jen zastávkové jízdní řády** (rozhodnutí Jan). Agenda linkového JŘ se
proto uživatelům nenabízí - v nabídce agend zbyly Zastávkový JŘ (AD) a Zastávkový JŘ (MHD).

Linkový tisk je **funkční a testy na něj běží dál** (Caché 142/142), jen se nenabízí. Zapnutí zpátky
je jeden parametr.

⚠️ **Hotové linkové šablony zůstávají dosažitelné**, což bylo hlavní riziko: výběr agendy
nezveřejněnou agendu nenabízí, ale **aktuální agendu otevřené šablony ukáže vždy**, a v otevírání
z databáze jsou její šablony pod „Všechny agendy". Prvek linkové tabulky zmizel z palety sám - váže
se na druh agendy, ne na seznam prvků.

⚠️ **Samotné skrytí nestačilo a dalo se obejít dvěma cestami** (nález Jan hned při kontrole - agendy
viděl dál):

- **Výchozí šablona měla linkovou agendu napsanou natvrdo.** Nová šablona tedy startovala na skryté
  agendě, ta se stala aktuální agendou šablony a výběr ji ukázal - aktuální agendu totiž zobrazuje
  vždy, aby šly otevřít hotové šablony. Výchozí je teď Zastávkový JŘ (AD).
- **Naposledy použitá agenda se obnovovala bez ohledu na zveřejnění.** Kdo si linkovou agendu kdysi
  vyzkoušel, dostal ji po startu zpátky z uživatelských preferencí. Nově se z paměti obnoví jen
  zveřejněná; kód z launch kontraktu shellu filtr záměrně nemá, tam je skrytá agenda chyba
  konfigurace shellu, ne zapamatovaný stav.

Obě cesty drží testy - kdo tu konstantu přepíše na skrytou agendu, spadne.

⚠️ **Seznam agend se čte jednou při startu**, takže běžící aplikace změnu nepřebere. Při kontrole je
nutné ji zavřít a spustit znovu.

Ověřeno voláním serveru na obou namespacech (i `ZL_KOVED`): registr vrací zveřejněné jen ty dvě
zastávkové. .NET **1410/1410** (4 nové testy).

## 2026-09-01 (9) - 📄 uspořádání na list řekne, když nic neskládá

Zákazník hlásil jako chybu tisku, že mu ze šablony **A5 na výšku leze PDF ve formátu A4**. Chyba to
nebyla, ale past: měl zapnuté **Uspořádání na list = „A4 na výšku"**, kam se dva A5 na výšku
(2 × 148 = 296 mm) do šířky 210 mm nevejdou. Mřížka proto vyšla **1×1** a papír se jen zvětšil -
A4 s jednou vývěskou uprostřed.

⚠️ **Volba se persistuje per agenda**, takže o ní uživatel nemusí vědět vůbec, a z aplikace nebylo
jak zjistit, proč mu z A5 šablony leze A4. Po exportu se dosud vypsalo neutrální „imposice 1×1 na
list 210×297 mm", což nikdo nečte jako problém.

Nově se v témž místě objeví **varování s radou**: *„Uspořádání na list nic neskládá: na list
210×297 mm se vejde jen jedna stránka 148×210 mm. Na list 297×210 mm (otočený) se vejdou 2"*. Rada
na otočený list se ukáže jen tehdy, když by na něj skutečně šlo víc stránek; u stejného formátu
stránky i listu hláška místo toho navrhne uspořádání vypnout.

**Pro dva jízdní řády A5 na jednu A4 se volí „A4 na šířku"** - tam mřížka vyjde 1×2.

⚠️ **Hláška platí pro oba exporty** - s daty z databáze i s ukázkovými. První verze ji měla jen
v tom prvním, takže na prázdné šabloně nebylo vidět nic - a přesně tou cestou jde každý, kdo si
chování chce vyzkoušet bez vybrané linky (nález Jan při zkoušení). Popis se skládá na jednom místě
a test hlídá, že se ty dvě cesty znovu nerozejdou.

Ověřeno: .NET **1406/1406** (7 nových testů, včetně přesně toho kolegova případu).

## 2026-09-01 (8) - 📱 QR kódy jsou čitelné i na malém prvku

Kód se kreslí **ostře v jakémkoli rozlišení** a vyplní celý prvek. Zákazník vyfotil QR, které
vypadalo „zdrcnuté" a nešlo sejmout - byly to **dvě chyby v kreslení, ne malá velikost prvku**:

- **Kódovalo se podle velikosti v pixelech, ne v modulech.** ZXing škáluje jen celým násobkem
  a zbytek doplní vycentrovaným okrajem, takže z 33 modulů na 60 px vyšel kód velký 33 px se 13px
  okrajem po obou stranách - **vyplnil jen 55 % prvku**. Čím menší prvek, tím menší podíl; u velkého
  99 %, proto to na velkých kódech nikdo neviděl.
- **Ke každému modulu se přidávalo pevných 0,5 px** „proti mezerám". U velkého kódu to je pár
  procent, u modulu širokého 1 px je to **poloviční rozlití** - moduly se slily do jedné plochy.

Nově se kóduje v modulech a hrany se zarovnávají na celé pixely, takže se sousedé přesně dotýkají.
Platí pro všechny kódy: čárový kód dostal navíc plnou výšku prvku, dřív mu ZXing vnucoval vlastní.

**Na otázku „jak poznám, že je QR dost velký" odpovídá aplikace sama.** Ve stavovém řádku a v hlášce
po tisku se objeví konkrétní čísla: *„QR kód je 10 mm, pro spolehlivé čtení potřebuje 13,2 mm"*.
Doporučení se počítá z **reálného obsahu** (0,4 mm na modul), takže dlouhá URL si řekne o víc místa
než krátká. Sdílí odznak se zmenšeným písmem - je to táž třída vady „vytiskne se to jinak, než jak
to vypadá" a čtvrtý pruh ve stavovém řádku by se nevešel.

Ověřeno: .NET **1399/1399** (11 nových testů, kreslí a kontrolují poměr černé plochy); mutací obou
příčin doloženo, že testy chybu chytí - po vrácení pixelového kódování spadnou dva testy, po vrácení
rozlití taky dva.

## 2026-09-01 (7) - 🏢 dopravce a provozovny po jednotlivých polích (`B111`)

Údaje dopravce jdou skládat po částech, místo jednoho slepeného bloku. Přibylo pět **samostatných
polí**: Název, Adresa, Telefon, E-mail a IČO dopravce. Šablona si z nich složí, co chce, takže adresa
už se neobjeví dvakrát.

⚠️ **Provozovny zůstaly nedořešené a jejich kolekce je skrytá** (`B112` v backlogu). Řádek na
dopravce × provozovnu totiž **nutně opakuje údaje firmy** - tři provozovny daly třikrát pod sebou týž
telefon a tabulka z dat s tím neumí nic udělat. Vyměnilo by to jednu duplicitu za druhou, a přesně
proti duplicitě celá věc vznikla (nález Jan při zkoušení). Data i kód zůstávají živé, ale uživatel
kolekci v nabídce polí nenajde, aby si na ní nepostavil šablonu.

⚠️ **`POPIS_DOPRAVCE` zůstává nedotčené** - visí na něm hotové šablony. Nová pole jsou přírůstek,
ne náhrada.

⚠️ **Příčina duplicity byla v datech, ne ve skládání.** Číselník sám není atomický: adresa firmy nese
ulici, PSČ i obec v jednom poli, telefon provozovny bývá slitý s webem a e-mailem, a pole „zkrácený
název pro MHD" obsahuje u ČSAD Jindřichův Hradec celý text včetně telefonů - právě odtud se telefon
dostal do výstupu dvakrát. To pole se proto **nevystavuje**. Nevystavují se ani čtyři pole, jejichž
význam je napříč dopravci nesourodý (jedno vypadá na DIČ, jiné na bankovní účet) - pole s popiskem,
který u poloviny dopravců lže, je horší než žádné.

⚠️ **Adresa firmy je v číselníku dvakrát a NEJSOU vždy totožné.** U ČSAD Jindřichův Hradec obě nesou
plný tvar, ale u ČSAD AUTOBUSY České Budějovice má první jen „Hálkova 781/9" a plnou adresu s PSČ
a obcí nese teprve druhé. Odhalilo to ověření voláním serveru, ne úvaha - první verze brala tu
kratší, takže by na vývěsce zmizelo PSČ a obec. Hlídá to nový test proti obsahu starého pole.

**Fax se nevystavuje** (rozhodnutí Jan 1. 9.) - na vývěsce nemá co dělat. Číselník ho drží dál,
takže se dá kdykoli přidat.

Skalární pole nesou **prvního dopravce** linky. Linku s víc dopravci tím pádem zatím nikdo neskládá -
je to součást téhož nedořešeného kusu (`B112`).

Ověřeno: Caché **142/142** (4 nové testy), .NET **1388/1388**, schéma i data ověřeny voláním serveru.
Zlaté odtisky tří linek přeraženy - počty stránek, zastávek, odjezdů i poznámek zůstaly shodné
a lišilo se jen md5, tedy podpis přidaného pole.

## 2026-09-01 (6) - 🙈 nadpis poznámek jde vypnout (`B108`)

U legendy poznámek přibyla ve skupině **Obecné** volba **„Záhlaví"**. Vypnutá schová **celý pruh** -
text i jeho podbarvení - a pruh přestane zabírat výšku, takže poznámky začínají u horní hrany prvku.
Zapínadlo sedí v Obecném a jeho vzhled ve skupině Záhlaví, stejně jako to má prvek zastávek.

⚠️ **Pole „Nadpis" se přejmenovalo na „Text záhlaví"** - v jednom formuláři stála dvě slova pro tutéž
věc a projekt drží „záhlaví" (rozhodnutí Jan 1. 9., doplněno do kanonu `ux-conventions §8`).

⚠️ **Dva prvky se přejmenovaly** (rozhodnutí Jan 1. 9.) - v paletě, v nabídce vkládání, v titulku
vlastností, ve stromu šablony, v hláškách i v nápovědě:

- **„Legenda poznámek" → „Poznámky"**. „Legenda" byla vlastní zkratka, kterou uživatel nikde jinde
  nepotká.
- **„Mřížka odjezdů" → „Minuty odjezdů"**. „Mřížka" je implementační pojem; uživatel na papíře vidí
  minuty.

Přejmenovaly se i **skupiny parametrů sestavy** (Minuty odjezdů, Poznámky), protože konvence je
skupina = název prvku, kterého se parametr týká. ⚠️ To je bezpečné: persistují se **názvy a hodnoty**
parametrů, ne jejich skupina - na rozdíl od labelů voleb, které jsou zamčené. Třídy
`NotesLegendElement` a `DepartureGridElement` se **nepřejmenovávají**, visí na nich `type`
v uložených šablonách.

⚠️ **Prázdný nadpis pruh neruší** a je to záměr: pás bez textu je legitimní. Vypínač je proto
samostatný, stejně jako ho mají zastávky („Záhlaví sloupců"), odjezdy („Záhlaví skupin"), linkový JŘ
(„Nadpisy skupin dnů") a obě tabulky („Záhlaví"). Původní návrh vázal vypnutí na prázdný text; volba
navíc nemění hotové šablony nikomu, kdo dnes prázdný nadpis má (rozhodnutí Jan 1. 9.).

⚠️ **Ostatní prvky vypínač už měly a nadpisový pruh se u nich vůbec netiskne** - „Hod" u odjezdů
a „Linkový JŘ - zastávky × spoje" kreslí jen náhled prázdného plátna v návrháři. Bez vypínače
zůstávají jen dvě věci a ani jedna není nadpis: řádek s hodinami u odjezdů a řádek čísel spojů
u linkového JŘ. Bez nich se jízdní řád nedá přečíst.

⚠️ **Klíč pole je `ShowHeaderBar`, ne `ShowHeader`** - ten už u zastávek a tabulek znamená záhlaví
**sloupců**, které legenda nemá. Drift test popisků to odhalil hned: tentýž klíč by uživateli
v jednom formuláři svítil jako „Záhlaví sloupců" a v druhém jako „Zobrazit nadpis".

Ověřeno: .NET **1388/1388** (5 nových testů), Caché **138/138**, formulář ověřen voláním serveru.
⚠️ **Test kreslí a kontroluje pixel**, ne jen výpočet výšky: mutací se ukázalo, že pruh kreslený
navzdory vypnutí projde přes výškové testy zeleně - a to je horší stav než dnešní, protože by
překryl první poznámku.

## 2026-09-01 (5) - ⌨️ velikost prvku jde měnit klávesnicí (`B109`)

**Shift+šipky mění velikost** prvku o milimetr - hýbe se pravá a dolní hrana, levý horní roh drží
(jako když táhneš pravý dolní úchyt). Myší se rozměr nedoladí a ve starém návrháři to kolega hodně
používal.

Přerozdělení kláves (rozhodnutí Jan 1. 9., vychází ze starého návrháře):

- **šipky** - posun o 1 mm, jako dosud
- **Ctrl+šipky** - posun o **velikost mřížky** (dřív tu byl jemný krok 0,5 mm)
- **Shift+šipky** - **změna velikosti** o 1 mm
- **Ctrl+Shift+↑/↓** - přesun sekce, zůstává a má přednost

⚠️ **Velký krok posunu tím zanikl a nikdo o nic nepřišel:** visel na Shiftu a počítal se z mřížky,
která je výchozích **1 mm** - dělal tedy totéž jako šipka samotná. Rozdíl viděl jen ten, kdo si
mřížku přepnul na 2 nebo 5 mm, a to je teď na Ctrl.

⚠️ **Prvku s dynamickou výškou se svisle nic nestane** a řekne se to ve stavovém řádku. Fixovat mu
výšku by znamenalo přijít o dynamiku bez varování a dostat se z toho jen zpětným krokem; myš mu ze
stejného důvodu svislé úchyty vůbec nenabízí. Šířku měnit jde.

⚠️ **Soused se při rozšíření nezužuje** - to platí jen uvnitř tabulky u hranic sloupců. Tažení úchytu
myší na sousední prvky nesahá, jen se o ně přichytává; ověřeno v kódu.

Opravené i **v nápovědě pod F1 a v uživatelské příručce**. V příručce se přitom našlo staré
nepravdivé tvrzení, že sekci přesouvají samotné šipky - ve skutečnosti Ctrl+Shift+šipky.

⚠️ **Přibyla brána na navázání kláves:** mutace ukázala, že odpojení Shiftu od změny velikosti nechá
sadu zelenou, protože testy volají změnu velikosti přímo. Rozhodnutí „která klávesa co dělá" proto
žije v jedné funkci (`ResolveArrowAction`), kterou test kryje. Táž třída mezery jako u nezapojené
migrace dnes ráno.

Ověřeno: .NET **1383/1383** (14 nových testů); mutacemi doloženo, že testy chytí jak odpojení
zkratky, tak vypnutou logiku.

## 2026-09-01 (4) - 📏 rozložení poznámek se řídí velikostí písma (`B110`)

**Velikost poznámek, výška řádku a počet sloupců jsou pohromadě** ve skupině Rozložení. Dřív byly
velikost a výška řádku mezi barvami buněk, tedy o dvě skupiny dál, a kolega je nenašel.

⚠️ **Výška řádku se nově hýbe s písmem** (prázdná = podle písma). Byla pevných 4 mm, což je přesně
automatická výška pro sedm bodů - výchozí stav tedy vypadal správně, ale **zvětšení písma legendu
slepilo**, protože mezera zůstala na čtyřech milimetrech, i když desetibodové písmo potřebuje 5,6.
Vlastní hodnota zůstává pro toho, kdo chce legendu vzdušnější nebo stlačenější; zadáním hodnoty,
kterou pole ukazuje, se vrátí dědění z písma (týž vzor jako výška řádku u zastávek a odjezdů).

**Nevejdou-li se poznámky, písmo se zmenší a aplikace to řekne.** Oranžový odznak ve stavovém řádku
a hláška po tisku jmenují prvek i míru: „Legenda poznámek: písmo zmenšeno až na 76 % (3 z 12
stránek)". Platí pro **všechny prvky jízdních řádů**, ne jen pro poznámky - zmenšení bylo dosud
neviditelné u všech pěti. ⚠️ Hláška **jmenuje konkrétní prvek a míru**, ne obecné „nastavení mění
vývěsku" - přesně kvůli tomu byl zrušen signál `B24`.

⚠️ **Roztahování na výšku se vědomě nedělá** (rozhodnutí Jan 1. 9.). Kolega si stěžoval, že se legenda
nezatáhne dolů, když je prvek vyšší, ale dvě poznámky mají zůstat nahoře a vyplnit klidně jen pětinu
bloku. Roztahování by u tří poznámek znamenalo jednu na začátku, jednu doprostřed a jednu na konci.

Ověřeno: .NET **1369/1369** (12 nových testů), Caché **138/138**, formulář poznámek ověřen voláním
serveru; mutací obou částí doloženo, že testy chytí jak pevnou výšku řádku, tak chybějící hlášku.

## 2026-09-01 (3) - 🎨 vymazané podbarvení záhlaví znamená „nekreslit nic" (`B107`)

Podbarvení záhlaví jde **vymazat a zůstane vymazané**. Prázdné pole = záhlaví bez podbarvení.

⚠️ **Dosud se vymazaná barva vracela** a vypadalo to jako chyba ukládání. Nebyla: šedá v prvku vůbec
nebyla zapsaná, byla zapečená jako **záloha** v kaskádě a jako výchozí hodnota pole ve formuláři.
Vymazání proto uložilo „nic neurčuji", renderer sáhl po záloze a formulář si při dalším otevření
předvyplnil svou výchozí šedou. „Bez podbarvení" nebylo jak vyjádřit (nález Pavel, #13158).

Šedá se proto přesunula ze zálohy do **počáteční hodnoty prvku**: nově vložený prvek ji má zapsanou,
existujícím ji dopsala migrace šablon (**1.11.0**), takže se vzhled nikomu nezměnil. Od té chvíle je
prázdno jednoznačné. Platí u všech prvků se záhlavím: poznámky, zastávky, odjezdy, linkový JŘ, pevná
tabulka i tabulka z dat.

**Dědění stylů zůstává** - prázdno u sloupce dál znamená „vezmi z prvku", protože kaskáda končí až
u prvku. Kdo chce zrušit podbarvení jednoho sloupce, vymaže ho u toho sloupce.

⚠️ **Přibyla brána, kterou si vyžádala mutace:** migrace vyřazená ze seznamu nechala celou sadu
zelenou, protože testy migrací je volají přímo. Zákazníkovi by se přitom šablona nemigrovala vůbec.
Nově se hlídá, že každá napsaná migrace v seznamu je.

Ověřeno: .NET **1357/1357** (22 nových testů), Caché **138/138**; všech pět formulářů ověřeno
voláním serveru. ⚠️ Odtisky kreslení tuhle změnu nekryjí - porovnávají se jen na vyžádání a jejich
prvky mají styl záhlaví nastavený natvrdo, takže se do zapečené šedé nedostanou.

## 2026-09-01 (2) - 🔢 do číselných polí jde psát desetinnou čárku (`B106`)

Rozměry, velikosti a odsazení se nově zadávají **čárkou i tečkou** - „1,5" znamená totéž jako „1.5".
Uloží i zobrazí se to s tečkou, jak hodnota leží v šabloně.

⚠️ **Nešlo o chybějící pohodlí, ale o tichou chybu.** Kontrola pole brala čárku jako oddělovač tisíců,
takže „1,5" prošlo jako 15 a pole **nezčervenalo**; hostitelská aplikace si ale totéž přečetla jako
neplatné a použila záložní nulu. Kdo napsal „1,5" do šířky, dostal prvek široký 0 mm bez jediné
hlášky. Ze stejné příčiny se „1,500" čtelo jako 1500 a „-2,5" jako -25 (nález Jan, #13158).

**Čárka je vždy desetinná, oddělovač tisíců se nepřipouští vůbec** - rozměr v milimetrech ho
nepotřebuje a pravidlo „čárka je tisíce, když za ní jsou tři číslice" by uživatel neuhádl. V poli pro
**celé číslo** čárka zůstává chybou („Musí být celé číslo"), textová pole se nemění.

⚠️ **Opraveno v EDControls, takže to platí i pro formuláře v EDISONMAIN2** - do produkce musí jít
knihovna i oba programy, které ji používají. Druhá vrstva (převod textu na hodnotu prvku) je
v návrháři, pro hodnoty, které přijdou jinudy než z formuláře.

Ověřeno: EDControls **721/721**, .NET návrháře **1335/1335** (18 nových testů); mutací obou vrstev
doloženo, že spadne 7, respektive 5 z nich.

## 2026-09-01 (1) - 🖱️ klik trefí prvek, který je vidět navrchu (`B105`)

Když se dva prvky na plátně překrývaly, **klik sáhl na jiný, než uživatel viděl**. Nově vložený prvek
se nakreslil navrch, ale uchopit se nedal: klik i tažení dostal ten starší pod ním, takže se nový
prvek musel nejdřív odsunout. Vypadalo to, že to „občas funguje" - záleželo na tom, v jakém pořadí
prvky v sekci vznikly (nález Pavel při skládání vývěsky pro BBSK, #13158).

⚠️ **Prvek vybraný ve stromu šablony si teď klik podrží.** Dosud se výběr při prvním kliknutí přepnul
na prvek, který ho překrývá - prvek schovaný pod jiným tedy nebylo jak odtáhnout ani přes strom.
Nově má přednost ten, který je už vybraný; když pod kurzorem žádný vybraný není, vezme se ten
navrchu. Kurzor to respektuje, takže ukazuje, co se stane. Táž přednost platí i pro dvojklik
a pravé tlačítko, aby nastavení otevřel prvek, na který uživatel míří.

**V šabloně se nic nemění** - hladina prvků (`ZIndex`) zůstává, jak byla, mění se jen hledání prvku
pod kurzorem: jde teď přesně obráceně než kreslení. Zvednutí hladiny při vložení by zapsalo změnu
do šablony, naráželo na strop 0-100 a výběr ze stromu by neopravilo.

Ověřeno: .NET **1326/1326** (5 nových testů); mutací původního řazení doloženo, že spadnou 3 z nich.

## 2026-08-31 (15) - 🗺️ každá zóna hraniční zastávky může mít vlastní sloupec (`N1`)

Zastávka na hranici zón měla dosud všechny své zóny v jednom sloupci za sebou („037,066,051").
Zákazník s IDS (#8576, KU HK) je chce vedle sebe, každou ve vlastním sloupci - jako to už umí
kilometry. V nastavení tabulky linky proto přibyla volba **„Každá zóna ve vlastním sloupci"**
(skupina Sloupce). Sloupec zón se po zapnutí rozšíří na tolik dílů, kolik zón má nejbohatší zastávka
linky; zastávka s jednou zónou nechá zbylé díly prázdné.

⚠️ **Výchozí je vypnuto, a je to to nejdůležitější rozhodnutí celé položky.** Víc zón má **301 738
zastávek** z 1 160 432 v datech šesti měřených firem - zapnutý rozpad by tedy překreslil hotové
sestavy i všem, kdo o něj nežádali. Starý modul tohle neumí vůbec (má na zóny jeden sloupec, který
jen zesílí podle délky obsahu), takže to není dorovnání starého tisku, ale nová věc.

⚠️ **Dělí se čárkou i lomítkem** - a to není opatrnost, ale měření: čárka 301 334×, lomítko 404×
(používá ho KV_DP, „23/5"). Kdyby se dělilo jen čárkou, u té firmy by volba tiše nedělala nic.

Na drátě se **nic nemění** - zóny už se posílají a rozdělit si je umí prvek sám. Nová databázová
položka by ke stejné věci vyrobila druhé pole, které by pak muselo žít navždy.

**Nadpis mají rozpadlé zóny jeden společný** přes všechny své díly, ne „Zóna" nad každým (tvůj nález
při zkoušení). Opakovaný nadpis vypadal jako několik různých sloupců, přestože je to pořád táž věc,
jen rozložená; sloučením zmizí i čáry mezi díly v řádku nadpisu. ⚠️ **Kilometry se záměrně
neslučují** - jejich díly jsou km jednotlivých úseků, tedy různé hodnoty se stejným názvem, a
sloučení by změnilo hotové sestavy všem, kdo km sloupce mají.

Ověřeno: .NET **1321/1321** (9 nových testů) a Caché 138/138; mutacemi tří cest (rozpad natvrdo
zapnutý; dělení jen čárkou; nesloučený nadpis) doloženo, že to testy chytí. Formulář prvku ověřen
voláním serveru.

## 2026-08-31 (14) - 🧪 spouštěč testů si přestal přičítat cizí běhy (`T1`)

Testy na dev serveru si zapisují výsledky do jednoho sdíleného místa, a spouštěč bral **všechno, co
tam po jeho startu přibylo** - tedy i sadu, kterou si zrovna pouštěl kolega. Dva běhy tak hlásily
186 testů místo 128.

⚠️ **Nejde jen o nafouknuté číslo.** Toutéž cestou si běh mohl přivlastnit i **cizí selhání**, takže
by se hledala chyba, která není naše - a to je dražší než špatný součet. Nově se počítají jen třídy,
které si běh sám spustil.

⚠️ **Co tím nezmizelo:** když dva lidé pustí souběžně **tutéž** třídu, filtr je neodliší. Počty
zůstanou správné, ale cizí selhání by se pořád ukázalo jako naše. Je to zapsané u kódu; oddělit by
to šlo až na úrovni testovacího frameworku.

Ověřeno: Caché **173/173** ve dvou balíčcích (35 + 138, každý hlásí přesně své). Že o výsledku
rozhoduje opravdu nový filtr, doloženo jeho obrácením - běh pak nahlásil 0 testů.

## 2026-08-31 (13) - ✅ dvě tvrzení o shodě se starým tiskem doložená, jedno bylo špatně (`L14`)

Dvě věty poznámek u atributů spoje visely v kódu jako **neověřené**: nikdo je neporovnal se starým
modulem, protože se ty atributy na kontrolních linkách nevyskytly. Dnešní měření našlo linky, kde
v datech jsou, takže je konečně bylo čím porovnat.

**Zásilky a IDS sedí.** Obě věty se se starým tiskem shodují na jediných linkách, kde ty atributy
v datech vůbec jsou - včetně toho, že se u nich **nikdy** netiskne obecná věta, ale výčet spojů.

**Věta „všechny spoje…" se počítá správně - a předchozí popis byl mylný.** V kódu stálo, že starý
modul počítá spoje přes celou linku, kdežto my přes stránku, a že se to u obousměrné linky může
rozejít. Neplatí to: starý modul počítá **v tom rozsahu, ve kterém tiskne legendu** - má-li ji na
každý směr, počítá spoje směru. Náš model je přesně ten případ. Doloženo na lince, kde bezbariérové
vozy mají všechny spoje jednoho směru, ale ne celé linky: obě strany tam říkají „všechny spoje".

⚠️ **Přitom se ukázalo, že si porovnání samo vyrábělo falešný rozdíl.** Starý modul se pouštěl
v režimu „jedna legenda za celou linku", zatímco náš výstup má legendu na každé stránce - a věty
atributů se pak počítaly každá v jiném rozsahu. Vypadalo to jako naše chyba, byly to dvě různé
sestavy vedle sebe. Opraveno; **do té doby to bylo horší než chybějící kontrola**, protože příští
skutečný rozdíl by se v tom šumu ztratil.

⚠️ **A druhá vada porovnání: linka s dlouhou poznámkou ho shodila.** Text poznámky se používal jako
klíč a několik odstavců přepravních podmínek se do něj nevejde. Taková linka se pak tvářila jako
neporovnaná. Klíč je nově useknutý a nese délku, takže rozdíl v konci textu se pořád pozná.

Ověřeno: Caché **138/138** (nový test na věty atributů), mutací (vždycky tvar „všechny spoje")
doloženo, že to test chytí.

## 2026-08-31 (12) - 🔤 nízkokapacitní vůz se tiskne „n" a autobus je dořešený

Dva zbytky po značkách spoje, oba k rozhodnutí. Rozhodly se **nálezem ve starém klientovi**, ne
odhadem - našel se i on, ne jen serverová tabulka.

**Nízkokapacitní vůz se nově tiskne jako „n"**, ne zkratkou „NKA" - přesně tak ho vypisuje starý
modul. IDS zůstává „IDS", tam se vstup s výstupem shodují. Ve vývěsce se nic nemění.

**Autobus zůstává, jak byl - a víme proč.** Legacy u něj má jiné číslo než my, což tu viselo jako
nerozhodnutý rozpor. Vysvětlení: starý klient sází **jiným fontem** (starší „Timetable"), a právě na
té jedné pozici se sady rozcházejí - v našem českém fontu je tam vagon, ve slovenském autobus. Naše
pozice je autobus v obou. Přepsat to „kvůli paritě" by českému zákazníkovi vrátilo vagon u přestupu
na autobus, tedy vadu opravenou 3. 8. Hlídá to test, ať to nikdo nespraví zpátky.

⚠️ **Důsledek na příště:** čísla v legacy tabulce jsou pozice ve **starém** fontu. U další značky je
nestačí opsat - musí se ověřit v našem.

⚠️ **Ranní tvrzení „z celé rodiny není v datech nic než BEV" bylo mylné.** Sonda přes šest firem
je našla: bezbariérovost 48 389 linek, kola 4 620, radiobus 168, IDS 60, wifi 59, podmínečné jetí 22,
občerstvení 17, zavazadla 17, nízkokapacitní 9, zásilky 1. **Samoobslužný spoj, částečná
bezbariérovost a zásuvka nejsou nikde** - jejich pozice jsou opsané, ale na reálném tisku je nikdo
neviděl; kdyby se vyskytly, patří ověřit.

⚠️ **Hned se u toho ukázalo, že to nestačí** (nález Jan): v řádku platnosti u spoje už bylo „n",
ale v legendě pod tabulkou zůstalo tučné **„NKA"** - značka a její vlastní vysvětlivka si na jedné
stránce odporovaly. Legenda si totiž kód z dat vypisovala rovnou, mimo překlad. Opraveno; ručně
zadaného komentáře se to netýká.

Ověřeno: .NET **1312/1312**, mutacemi obou cest (návrat „NKA" v tabulce i v legendě).

## 2026-08-31 (11) - 🚻 značky u spojů se přestaly tisknout zkratkou (`L11`)

Občerstvení, zásilky, radiobus, samoobslužný spoj a spoj jedoucí podmínečně se na linkovém JŘ
tiskly zkratkou (OBC, POS, RBU, SAO, RBP), protože nikdo nevěděl, který obrázek k nim patří.

⚠️ **Ta tabulka celou dobu existovala.** Roadmapa tvrdila, že překlad „zkratka → glyf" v repu není,
protože žije ve windowsím tiskovém klientovi. Klient je v repu - jen jinde, než se hledalo:
`ALVA.TISKJR.Main.ConvertSpecialniZnak` překládá id z číselníku na kód znaku ve fontu. Pozice jsou
tedy **opsané, ne hádané**, a je to zapsané v dokumentaci, ať se nehledá potřetí.

Potvrdila i to, co jsme ráno nasadili podle tvaru: loď, wifi, **zásuvka**, kola, zavazadla,
bezbariérovost - všechno sedí.

⚠️ **Dvě značky z té rodiny zůstávají textem, a je to správně:** IDS a nízkokapacitní vůz. Starý
modul je taky netiskne obrázkem (mají v tabulce zápornou hodnotu = text). Bez té tabulky bychom jim
piktogram nasadili - a byla by to chyba, kterou by nikdo nehledal.

**Zbylo k rozhodnutí:** legacy vypisuje nízkokapacitní vůz jako **„n"**, ne „NKA"; a mapuje autobus
na jiný kód než my (u nás 122, legacy 119 - v českém fontu je na 119 vagon, ve slovenském autobus).
Obojí je v roadmapě, neměněno bez rozhodnutí.

⚠️ **`B99` je tím rozhodnutá faktem:** vývěsky mají vlastní legacy tabulku a ta loď, wifi ani zásuvku
**vůbec nezná**. Jejich piktogram ve vývěsce by tedy nebyla parita se starým tiskem, ale nová
featura - proto tam zůstávají textem.

Ověřeno: .NET **1307/1307**, glyfy vykreslené a prohlédnuté v **obou** fontech, mutací (špatný cílový
znak) doloženo, že to testy hlídají.

⚠️ **Jeden piktogram si zaslouží tvoje oko:** zásilky (168) vypadají v obou fontech jako přeškrtnutý
čtverec. Je to podle legacy, ale nečte se to jako „zásilky" - kdyby to na tisku vypadalo divně, je to
tenhle.

## 2026-08-31 (10) - 🧱 volby mřížky fungují i u mřížky odjezdů (`L15`)

Poslední prvek, který „Čáry mezi řádky" a „Čáry mezi sloupci" nabízel, ukládal - a nečetl. Stejná
vada jako u linkové tabulky (`L10`), jen o prvek vedle.

**V pivotu ale ty dvě volby neznamenají totéž co v řádkové tabulce**, a právě to je jádro téhle
změny. Řádek je hodina, sloupec je skupina dnů:

- **Čáry mezi řádky** → vodorovné čáry mezi hodinami
- **Čáry mezi sloupci** → oddělovače mezi skupinami dnů
- **svislá čára za sloupcem hodin zůstává vždycky** - není to oddělovač sloupců, ale **osa**:
  odděluje hodinu od minut, které k ní patří. Mechanicky „konzistentní" zapojení obou voleb by
  udělalo z vývěsky nečitelnou plochu.

⚠️ **Riziko se měřilo, ne odhadovalo.** Na rozdíl od linkové tabulky zastávkové šablony existují
a používají se, takže komu by volby byly odškrtnuté, tomu by mřížka z hotového tisku zmizela.
Read-only sonda prošla **všechny uložené šablony** na dev serveru: 18 šablon, 12 prvků odjezdů,
**ani jeden** s odškrtnutou volbou. Sonda zůstává v diagnostice šablon pro příště.

Ověřeno: .NET **1307/1307** (6 nových testů) a mutacemi obou cest - volby se ignorují, a osa mizí
spolu se sloupci. Riziko navíc změřené sondou, viz výše. Chování potvrdil i Jan v aplikaci.

⚠️ **Past, na kterou jsem u testů najel:** odjezd se ke skupině dnů váže polem `SKUPINA_DNU_ID`.
S jiným názvem se nepřipojí k žádné skupině, nevzniknou žádné hodiny - a prvek pak nevykreslí
**ani pixel**, ne jen neúplný obrázek. Vypadá to jako „tenhle prvek nejde v testu vykreslit",
přitom jde jen o překlep v datech. Chvíli jsem to hledal v renderu pivotu a málem z toho založil
samostatný úkol.

## 2026-08-31 (9) - 🗂 parametry tisku linkového JŘ jsou rozdělené podle toho, čeho se týkají (`L12`)

V nastavení tisku linkového JŘ byly **tři** skupiny místo dvou - a jedna z nich obsahovala jedinou
položku: „Dělení spojů dle platnosti", tedy zrovna nejdůležitější volbu agendy. Nebyl to záměr,
jen jí chybělo zařazení, a parametr bez zařazení dispatch shodí do vlastní skupiny „Parametry
sestavy". „Obecné" navíc vycházelo **doprostřed**, protože skupiny se řadí podle prvního výskytu.

Nově jsou skupiny dvě, podle toho, čeho se volba týká - stejně jako u vývěsek:

- **Obecné** (první): tvar názvu zastávky, název pro MHD, obousměrný tisk
- **Tabulka linky**: dělení spojů, sloupec tarifních čísel, zastávky bez zastavení, barvy spojů,
  zvýraznění zastávky i tarifního čísla

Obousměrný tisk je vědomě v „Obecném": rozhoduje o složení celé sestavy (jedna stránka, nebo dvě),
ne o vzhledu jednoho prvku. Skupina se přejmenovala z „Linkový JŘ" na „Tabulka linky" - uvnitř
linkové agendy je „Linkový JŘ" tautologie, kdežto název prvku odpovídá logice vývěsek („Seznam
zastávek", „Mřížka odjezdů"). **Názvy voleb ani jejich hodnoty se nezměnily**, takže uložené
uživatelské volby zůstávají.

**Přibyla strojová pojistka**, protože tahle vada vzniká zapomenutím, ne rozhodnutím: test hlídá,
že žádný parametr nezůstal bez skupiny a že „Obecné" je první. ⚠️ Hned při zavedení našel **druhý
případ** - `FORMAT_CASTKY` v agendě faktur; opraveno spolu s tím.

Ověřeno: .NET **1303/1303** (3 nové testy), Caché **137/137**, a nasazení ověřeno voláním
`GetParameters` - server vrací dvě skupiny v pořadí Obecné → Tabulka linky.

⚠️ **Málem to skončilo jako tichý nepořádek:** pořadí jsem přepsal skriptem, a `.cls` se skriptem
nenasazuje (hook reaguje jen na editační nástroj). Skupiny na serveru byly, pořadí ne - a testy
o tom nevěděly, protože čtou zdroj z repa. Chytlo to až to ověřovací volání.

## 2026-08-31 (8) - 🧭 nabídka stránek náhledu pojmenuje i linkový JŘ (`L13`)

U linkového JŘ hlásila nabídka stránek jen „Stránka 1" / „Stránka 2", takže nebylo poznat, na který
směr se člověk dívá.

⚠️ **Netýkalo se to jen obousměrného tisku, jak to vypadalo napoprvé.** Popisek se stavěl výhradně
z pole „aktuální zastávka", a to je pole **vývěsky** - linkové agendy ho ve schématu nemají vůbec.
Spadl proto na „Stránka N" u **každé** stránky linkového JŘ, jednosměrné i obousměrné.

Nově se u linky vezme trasa, kterou data nesou odjakživa: **„1 - Praha → Sedlčany"**. Obousměrná
stránka dostane obousměrnou šipku (**„↔"**) - pozná se podle toho, že u ní agenda vědomě nevyplňuje
směr, protože nese oba naráz. Nové pole na drátě to tedy nepotřebovalo.

Vývěska zůstala beze změny; na to je vlastní test, aby se linková větev nikdy neprotlačila před ni.

Ověřeno: .NET **1300/1300** (4 nové testy) a mutacemi obou cest - vypnutá linková větev a nerozlišená
obousměrná šipka.

## 2026-08-31 (7) - 👁 náhled bez vybrané linky ukáže vlastní sloupce (#13158 bod 3)

Kdo si v prvku odjezdů nadefinoval vlastní sloupce a **neměl vybranou konkrétní linku**, neviděl
v návrháři žádnou změnu. Vypadalo to jako nefunkční featura - přitom s vybranou linkou fungovala
odjakživa.

Ukázková data se totiž ze serveru tahala **bez jakýchkoli voleb**, takže server spadl na výchozí
rozvržení podle linky (Pracovní / Sobota / Neděle a svátky). Nově se do nich posílá definice
sloupců ze šablony; server ji uměl číst už dřív, jen mu ji nikdo neposlal.

⚠️ **Druhá polovina té pasti byla cache.** Vzorek se držel pod samotným kódem agendy, takže i po
opravě vstupu by se po změně sloupců vrátil starý obrázek. Definice je proto součástí klíče - jiné
sloupce znamenají nový dotaz, stejné sloupce se pořád berou z cache (načtení je synchronní a trvá
pár vteřin, takže se nesmí opakovat při každém překreslení). A změna definice **bez vybrané
instance** teď náhled přenačte, ne jen ta s instancí jako dosud.

Vzorek tím nezačne odpovídat konkrétní lince - ukazuje **to členění**, které je v šabloně, a o to
šlo (rozhodnutí Jan 31. 8.).

⚠️ **U linkového JŘ zůstává týž vzor otevřený**: `LinkovyJrAd.GetMockData` volby z požadavku nečte
vůbec, staví stránku s napevno zapsanými hodnotami. Tam nestačí přeposlat parametry, musela by se
upravit metoda - je to v roadmapě v „K ověření na reálné sestavě".

Ověřeno: .NET **1296/1296** (6 nových testů) a mutacemi obou pojistek - neposlaná definice
a cache ignorující její změnu.

## 2026-08-31 (6) - 🔎 adresa dopravce i z firmy a seskupená pole agendy (#13158)

Dvě věci, které vyplynuly hned ze zkoušení předchozí změny.

**Adresa se bere i z firmy, nejen z provozovny, a to po položkách.** Sídlo, ulici a telefon jsem
četl výhradně ze záznamu provozovny - jenže provozovna bývá **organizační jednotka bez ulice**.
Na lince 108360 je to „Oblast Písek": má název a telefon, ulici ne. Firma přitom má adresu
kompletní („Hálkova 781/9, 370 04, České Budějovice"). Chybějící ulice i telefon se proto doplní
z firmy **každý zvlášť** - první pokus čekal, až bude provozovna prázdná celá, a u takového
dopravce by se adresa neukázala nikdy.

⚠️ **Dva bloky téhož dopravce nejsou chyba.** Na KOVEDu se zobrazily dva - Uherské Hradiště
a Uherský Brod. Jsou to **dvě provozovny** téhož dopravce, každá se svou adresou, a obě linku
obsluhují. Slučovat je podle názvu by tu informaci zahodilo, takže zůstávají.

**Pole agendy jsou seskupená podle toho, čeho se týkají** - linka, dopravce, platnost, zastávka,
poznámky. Dosud byla zastávková pole rozházená mezi ostatní (mezi „Konečnou zastávkou" a zbytkem
zastávkových polí stál QR kód a typ jízdního řádu) a uživatel je v seznamu hledal po jednom.
Platí pro obě vývěskové agendy. Je to jen pořadí v nabídce - žádný klíč se nezměnil, takže se
hotovým šablonám nic nestalo.

Ověřeno: .NET **1290/1290**, Caché **137/137**, a nasazení ověřeno voláním `GetSchema` na serveru
(vrací nové pořadí i nový popisek). Zlaté odtisky se nezměnily - záloha adresy z firmy se na gold
linkách neuplatní, protože ty mají adresu u provozovny.

## 2026-08-31 (5) - 🚌 dopravce s adresou po řádcích a „Konečná zastávka" místo „POPIS_SMER" (#13158)

Dvě věci ze zákaznické poznámky k vývěskám (BBSK).

**Popis dopravce má adresu a každou položku na svém řádku.** Dosud vyšlo „Zajišťuje: Dopravce A,
Dopravce B" v jednom odstavci - a adresa se neukázala nikdy, i když v datech je. ⚠️ Chyba nebyla
v tom, že by adresa chyběla: skládala se jen tehdy, když dopravce **neměl vyplněný zkrácený název**,
a jakmile ho měl, vrátil se sám a adresa se zahodila. Nově zkracuje jen jméno, adresa přibude tak
jako tak. Výsledek je název, sídlo, ulice a telefon pod sebou; víc dopravců odděluje prázdný řádek,
aby šlo poznat, kde končí adresa jednoho a začíná jméno druhého.

Platí pro **obě vývěskové agendy** (AD i MHD), ať se dvě vývěsky vedle sebe neliší. Linkových
jízdních řádů se to **nedotklo** - jsou v paritní vlně a změna obsahu pole by tam paritu se starým
tiskem rozbila. Městská vývěska tím ztratila uvození „Dopravce: " a rozešla se se vzorem, ze kterého
tvar pocházel; je to vědomé, ne drift. Piktogramy (obálka, sluchátko) se **nedělaly** - textový prvek
kreslí jedním fontem, takže je to jiná úloha.

**Pole „Konečná směru" se jmenuje „Konečná zastávka"** (`POPIS_SMER` → `KONECNA_ZASTAVKA`,
`POPIS_SMER_CIS` → `KONECNA_ZASTAVKA_CIS`). Zákazník hledal cílovou zastávku a nenašel ji, přestože
to pole existovalo - ukazuje totiž **na tutéž zastávku**, ze které se počítá „CIS číslo cílové
zastávky". Přejmenováno ve všech čtyřech agendách i v systémových šablonách.

⚠️ **Přejmenovalo se načisto** - staré pole nezůstalo žít vedle nového (rozhodnutí Jan 31. 8.:
mrtvý klíč držený jen kvůli přejmenování je nepořádek, ne zpětná kompatibilita). Existující šablony
proto srovná **migrace na verzi 1.10.0**, která přepíše i zástupce `{POPIS_SMER}` ve všech textech
a výrazech. Bez ní by na šabloně zůstal zástupce, který už žádná agenda neposílá, a v tisku by z něj
vyšlo prázdno.

Ověřeno: .NET **1290/1290** (7 nových testů migrace), Caché **137/137**. Deset zlatých odtisků se
muselo přerazit - u všech deseti se změnilo **jen md5** a počty stránek, zastávek, odjezdů i poznámek
zůstaly shodné, což je přesně podpis „změněný obsah pole a přejmenování", ne změna chování.

## 2026-08-31 (4) - 🔄 otočení stránky tam a zpátky už šablonu nescvrkne

Nález Jan 31. 8. hned po vyzkoušení předchozí změny: otočil šablonu na šířku, rozmyslel si to,
otočil zpátky na výšku - a sekce se „nenatáhla zpět".

**Bylo to horší, než jak to vypadalo.** Poměr „aby se to vešlo" vyjde u A4 při otočení **v obou
směrech stejně** (0,686), takže se dvě otočení nevyrušila, ale vynásobila: obsah po návratu seděl
na **47 %** původní velikosti a písmo z 12 pt vyšlo jako **5,6 pt**. Po třetím otočení 3,8 pt.
Nešlo tedy o sekci, ale o celou šablonu - jen je to na výšce sekce nejvíc vidět.

**Spraveno tím, že se poměr počítá vždy od výchozí tiskové plochy, ne od aktuálního stavu.**
Šablona si drží, ke které ploše se souřadnice prvků vztahují a jak moc je od ní obsah zvětšený
a posunutý; návrat na původní formát pak vyjde jako poměr 1 a obsah sedne přesně tam, kde byl.
Platí to i přes mezikroky - otočit na šířku, přepnout na A5 a vrátit se na původní formát dá zase
původní velikosti.

⚠️ **Drží se to jen po dobu, co je šablona otevřená** (rozhodnutí Jan 31. 8.): pokrývá to reálné
chování „zkouším formáty při návrhu" a nestojí to změnu formátu souboru šablony ani migraci.
Kdo formát změní, uloží, zavře a druhý den se vrátí, začíná od nového formátu jako od výchozího.
Uložit to do šablony jde kdykoli aditivně.

Vystředění se přitom musí odečíst **před** škálováním a nasadit až po něm - jinak by se posun
z minulé změny přenásobil spolu s obsahem a ten by ujel do strany.

Ověřeno: .NET **1283/1283** (2 nové testy na otočení tam a zpět, jeden i přes mezikrok). Mutací
„počítej od aktuálního stavu místo od výchozí plochy" spadly přesně tyhle dva testy - ostatních
devět prošlo, takže tu vadu opravdu nehlídal nikdo jiný.

## 2026-08-31 (3) - 📐 změna formátu stránky obsah přepočítá, místo aby ho vymazala (`B104`)

Ve Vlastnostech stránky se dala změnit velikost nebo orientace jen za cenu toho, že se **celá
šablona zahodila** a založilo se prázdné plátno - uživatel ji stavěl znovu od nuly. Důvod byl
poctivý (zachovávat prvky přes změnu rozměrů se chovalo nepředvídatelně, visely mimo stránku),
jen se vyřešil tím nejtvrdším způsobem, který byl po ruce.

Nově se **přepočítá geometrie a nastavení prvků zůstane** - sloupce, styly, volby, podmínky
i datové zdroje. Přepočítávají se pozice, rozměry, velikosti písma, odsazení, výšky řádků
a tloušťky čar, takže výsledek vypadá jako táž sestava na jiném papíře, ne jako drobný text na
velké stránce.

**Poměr se počítá z tiskové plochy, ne z papíru.** Prvky leží uvnitř okrajů, takže z rozměru
stránky by se souběžná změna okrajů promítla do výsledku dvakrát. ⚠️ Vedlejší důsledek: ani při
stejném poměru papíru (A5 → A4) nevyjde přepočet úplně beze zbytku, protože okraje se neškálují -
obsah se proto posune o jednotky milimetrů. To je správně, ne chyba zaokrouhlení.

**Při změně orientace** se bere menší z obou os, aby nic nepřeteklo, a obsah se **vodorovně
vystředí**. Svisle zůstává nahoře, protože sekce jdou odshora jako dokument - hlavička plovoucí
uprostřed prázdné stránky by vypadala rozbitě (rozhodnutí Jan 31. 8.). Uživatel musí počítat s tím,
že se mu pozice posunou; podstatné je, že se nic neztratí a dá se s tím dál pracovat.

Hláška o vymazání zmizela, místo ní je otázka **Ano** (přepočítat) / **Ne** (změnit jen stránku) /
**Storno**. ⚠️ Sdílený dialog má tlačítka Ano/Ne/Storno napevno, takže význam nese text otázky -
akční popisky by znamenaly sáhnout do EDControls a publikovat i druhou aplikaci. Prázdná šablona
se neptá vůbec. Celé je to **jeden vratný krok**: Ctrl+Z vrátí rozměr stránky i obsah najednou.

**Prvky, které by po přepočtu vyčnívaly, se nikam neposouvají** - posunout někomu prvek bez jeho
vědomí je horší překvapení než vidět, že něco visí ven.

Ruční škálování z nabídky zůstalo, jak bylo; obě cesty teď stojí na společném výpočtu, jen ruční
škáluje i papír, kdežto změna formátu bere rozměr a okraje přesně podle dialogu.

Ověřeno: .NET **1281/1281** (9 nových testů) a mutacemi tří cest - větší poměr místo menšího,
vynechané vystředění, přeškálované okraje místo hodnot z dialogu.

## 2026-08-31 (2) - 🧱 volby mřížky u linkové tabulky konečně fungují (`L10`)

„Čáry mezi řádky" a „Čáry mezi sloupci" nabízí formulář u **každého** prvku - jsou ve sdílené sadě
základních polí, takže je dostane i prvek, který si je sám nekreslí. Linková tabulka je do dneška
vůbec nečetla: mřížku kreslila vždycky. Uživatel klikl, hodnota se uložila do šablony a nestalo se
nic - nejhorší možná zpětná vazba, protože z toho nepozná, jestli je rozbité nastavení, nebo tisk.

Teď se obě volby čtou stejně, jako je odjakživa čte **seznam zastávek**, **pevná tabulka**
i **tabulka z dat**. Jemnější potlačení svislé čáry mezi popisnými sloupci zůstává - „Čáry mezi
sloupci" je nadřazený vypínač, ne jeho náhrada.

**Proč zrovna teď:** volba se ukládá do šablony, i když nic nedělala, takže si ji někdo mohl
odškrtnout a po opravě by mu čáry z tisku zmizely bez jediného kliknutí. Proto se před zásahem
změřilo, kolika šablon se to týká: na dev serveru **není ani jedna šablona linkového JŘ** (agenda
je zveřejněná od 24. 8.), takže není co migrovat a levnější chvíle na opravu už nebude. Jediné, co
ty volby dnes nese, je zákaznický vzor **940 (IDZK)** v repu - má vypnuté vodorovné čáry a po téhle
změně o ně přijde, což odpovídá záměru toho, kdo je odškrtl (rozhodnutí Jan 31. 8.: nechat tak).

⚠️ **Mřížka odjezdů má tutéž vadu a záměrně zůstala nedotčená.** Zastávkové šablony existují
a používají se, takže tam by zapojení voleb mohlo změnit hotový tisk - má vlastní položku a vlastní
měření. Navíc je tam otevřená otázka návrhu: kreslí tři druhy čar a jedno zaškrtávátko by ovládalo
dvě z nich naráz, přičemž čára za sloupcem hodin je nosná.

Ověřeno: .NET **1272/1272** (4 nové testy) a mutacemi obou cest - volby úplně ignorovat, a přehodit
je mezi sebou.

## 2026-08-31 (1) - 🚏 značky spoje: zalomení po dvou a inverzní negativní značka (`L11` z větší části)

**Značky platnosti se zalamují po dvou na řádek.** Dosud jsme je kreslili všechny do jednoho řádku
jako souvislý řetěz - bez zalomení i bez ořezu, takže spoj s víc značkami je vypsal přes hranici
sloupce do sousedního. Starý modul má strop dvě značky na řádek a zbytek posílá pod ně
(`POCETZNAKUPLATNOSTI`, `JRapiLinPrtWin.mac:25`, skládání `:3752-3775`). Řádků je tolik, kolik jich
potřebuje nejbohatší spoj; ostatní mají zbylé prázdné, stejně jako už to dělají řádky turnusů.

⚠️ **Dělení je mechanické, ne podle významu značek.** Řádek proto může nést vedle sebe den i
vlastnost vozidla. Vypadá to jako významové řádky a na většině spojů to tak i vyjde (dny jsou
v pořadí první), ale je to náhoda - rozhodl o tom tisk ze starého modulu na lince 108360, který
Jan otevřel 31. 8.: spoj **1017** má ⚒ i ♿ **na jednom řádku**, zatímco 4009 a 4025 mají ⑥ ✝
na prvním a ♿ na druhém. Ten rozdíl je jediné, čím se obě vysvětlení odliší, takže na něj míří
vlastní test - bez něj by dělení podle rodin značek prošlo jako správné.

**Negativní značka se tiskne inverzně** - tmavý box se světlým číslem, jako ji má starý tisk
i naše vývěsky. Odlišuje ji to od značek platnosti nad ní, které říkají opak (kdy spoj jede).
Inverze se bere z kaskády, ne z pevných barev: box dostane barvu textu buňky a znak její barvu
pozadí, takže obarvená tabulka zůstane čitelná místo černého obdélníku uprostřed.

**Loď, wifi a nově i zásuvka se kreslí piktogramem, přibyla kola a zavazadla.** Do 31. 8. tu stálo,
že zásuvka ve fontu není - **není to pravda**: tabulka glyfů obou fontů ji našla na 179, hned vedle
lodi (180) a wifi (186) v souvislém bloku ikon vybavení vozidla. Ve vývěskách zůstávají všechny tři
zatím textem, protože přepnout je by změnilo hotový tisk.

⚠️ **Zbytek rodiny atributů (IDS, NKA, OBC, POS, RBU, SAO, RBP) se dál tiskne zkratkou** a parita
téhle části **není doložená**. Překlad „která zkratka na který glyf" má starý modul ve windowsím
tiskovém klientovi.

> **Opraveno později téhož dne (viz zápis 11):** ten klient **v repu je** - jen jinde, než se
> hledalo. Tabulka se našla, atributy se od té doby tisknou piktogramem a IDS s NKA zůstávají
> textem **podle legacy**, ne z opatrnosti. Tvrzení „v repu není" bylo mylné.

Ověřeno: .NET **1268/1268** (19 nových testů) a mutacemi tří cest - strop tří značek místo dvou,
dělení podle významu místo mechanického, nenakreslený box negativní značky.

## 2026-08-28 (10) - 🔁 turnusy: volba prvku místo parametru + jen číslo turnusu

Dvě změny na tvůj podnět, obě k tomu, aby featura odpovídala tomu, co turnus je.

**Zapíná to prvek, ne parametry tisku.** Turnus je provozní věc dopravce, ne informace pro
cestujícího - patří tedy k sestavě, kterou si někdo udělá pro provoz, ne k volbám u tisku. Stejně to
má i legacy: je to nastavení **sekce v návrháři sestav**. Prvek linkové tabulky má proto zaškrtávátko
**„Turnusy ke spojům"** (skupina Řádky) a parametr sestavy zmizel. Serveru se volba posílá dál, jinak
by turnusy v datech nebyly: jde touž cestou jako `DEFINICE_SLOUPCU` (vždy čerstvě ze šablony) a její
přepnutí spouští re-fetch dat přes týž snapshot. Dotaz do modulu turnusů (40-550 ms) se tak pořád
nespustí nikomu, kdo turnusy netiskne. Volba zároveň řídí kreslení, takže dva prvky v šabloně můžou
mít různě.

**Tiskne se číslo turnusu, ne celé označení s variantou.** Označení je `<turnus>.<varianta>` a modul
vrací záznam za každou kombinaci zpracování × datum - na lince 0320625 měl spoj 1 osm označení
`51164.01`-`51164.09`, ale **jediný** turnus 51164 (2 zpracování × 18 dnů). Osm řádků o tomtéž
turnusu čtenáři nic neříká, takže se sjednocuje podle části před tečkou: jeden řádek „51164".
Odchylka od legacy je vědomá - legacy tiskne celé označení jen proto, že nic nesjednocuje (a má na
zkrácení dokonce nedodělanou volbu „zalamovat označení turnusu za tečkou").

⚠️ Nový vstupní parametr obou skládacích metod je **povinný**, ne s výchozí hodnotou - právě proto
se hned ukázala dvě zapomenutá volání v testech. U mapy, kterou musí náhled, export i tisk poslat
**shodně**, je vynucený překlad lepší než tichý default (tak vznikla třída chyb, kde tisk vyjel jinak
než náhled).

Ověřeno: .NET **1249/1249** (3 nové testy na cestu volby z prvku na server), Caché **137/137**
včetně kontroly, že v označení už není tečka a že parametr sestavy zmizel.

## 2026-08-28 (9) - ✨ šipka pro zrcadlený blok a dorovnání i vlevo (`L6c` dokončeno)

Dva zbytky ze `L6b`, oba viditelné jen v obousměrném tisku.

**Šipka směru pro protijedoucí blok.** Sloupec „Šipka směru" **za** názvy zastávek teď ukazuje
**nahoru**, protože obsluhuje blok vpravo, a ten jede proti pořadí zastávek (přesně jak to je na
tisku ze starého modulu: vlevo „od" u první zastávky, vpravo „př"). Před názvy ukazuje dolů jako
dosud a na jednosměrné stránce míří dolů obě. Legacy na to má **druhý typ sloupce**; u nás rozhoduje
**pozice** sloupce vůči názvům zastávek, takže druhá šipka nepotřebuje nový klíč sloupce ani migraci
šablon - uživatel prostě přidá druhý sloupec „Šipka směru" na druhou stranu.

**Dorovnání prázdnými sloupci doplňuje i levou stranu** (#8372 mělo jen pravou). Bez toho se
v posledním, neúplném bloku posunuly zastávky o celý sloupec doleva (na fixtuře 15,8 mm) a svisle
nesedly na bloky nad sebou. Dvě věci, na kterých to stálo:

- **Dorovnání patří na vnější hranu**, tedy před spoje zrcadlené strany - jinak zůstane prázdné
  místo mezi spoji a zastávkami. Ví o něm `SloupcovaMrizka`, protože posouvá souřadnice celé levé
  strany (ručně kreslené značky platnosti a šipka je odtud berou).
- **Počítá se per strana**, ne z počtu slotů bloku: obousměrná linka může mít v jednom směru méně
  spojů, a pak je jedna strana bloku plná a druhá ne.

Ověřeno: .NET **1245/1245**, mutacemi šesti cest (šipka vždy dolů / obrácené strany / jen první
sloupec šipky; nedorovnávat vlevo / dorovnat na vnitřní hraně / dorovnávat i jednosměrně). Poslední
z nich **neprošla, dokud nepřibyl test na jednosměrné dorovnání**: mutace odsune celou tabulku
doprava, a všechny dosavadní testy měřily jen šířku doprava, která tím naopak naroste.

⚠️ **Měření „levá hrana textu" musí brát nejlevější pixel bloku, ne každého řádku.** První a poslední
řádek pixelů písmene začíná jinde než jeho střed, takže rozptyl přes řádky hlásil 5,8 mm i na
zarovnané tabulce - vypadalo to jako polovina neopravené vady.

## 2026-08-28 (8) - ✨ turnusy ke spojům v linkovém JŘ (`L6c`, první část)

> *Volba se v (10) přesunula z parametrů sestavy do prvku a označení se sjednocuje na číslo turnusu.*

Nový parametr sestavy **„Turnusy ke spojům"**: pod čísla spojů se přidají řádky s označením turnusu,
ve kterém spoj jede („110.01"). Řádků je tolik, kolik turnusů má **nejbohatší** spoj, spoje s méně
turnusy mají zbylé řádky prázdné - a jsou **nad** řádkem platnosti, jak to má starý engine v kontraktu
řádků (`1` číslo spoje → `1.001xx` turnusy → `1.1` platnost → `1.9` negativní značky).

**Proč parametr a ne trvalá součást dat:** turnusy nejsou v `^JRL`, tahají se dotazem do modulu
turnusů (`gSplTur` - týmž, jaký volá starý tisk) a ten **stojí 40-550 ms na linku** (změřeno sondou).
Kdo turnusy netiskne, nemá půl sekundy platit. Legacy to zapíná stejnou pákou.

⚠️ **Turnusy starého modulu závisí na tvaru čísla linky a v našem oracle nevyjdou.** Engine předá
do dotazu `lin`, jak ho dostal, ale odpověď filtruje **číslem z turnusového záznamu** (`108360`),
takže s indexovým tvarem s nulami („0108360") se nespáruje nic a `TUmax` zůstane 0. Změřeno oběma
tvary („0108360" → 0 spojů, `108360` → 3 spoje) a doloženo pracovním globalem enginu po běhu.
**Náš oracle (a klíč v `^JRL`) používá tvar s nulami, takže tam turnusy nevyjdou nikdy.**

⚠️ **Neověřeno: co posílá skutečný klient TiskJR.** Komentář v jeho překladové vrstvě ukazuje seznam
„4|820701" **bez** vodicí nuly (`ALVA.TISKJR.Data:125`), takže je možné, že v produkci legacy turnusy
tiskne. Rozhodne to jedině **tisk ze starého modulu se zapnutou volbou** (je to nastavení sekce
„Data linky" v návrháři sestav, ne u linky). Do té doby platí: **netvrdit, že se featura
nepoužívala.** Důsledek pro `R6`: **hodnoty se neověřují oraclem** (v našem tvaru není proti čemu), ale proti
**témuž zdroji, jaký volá legacy** - stejné datum platnosti linky, stejné sjednocení podle označení.
Pozice řádků je odvozená z kontraktu enginu, ne z výstupu. Oracle test zůstává a přeskočí se, dokud
starý modul nic nevytiskne; kdyby se spravil, porovnání se rozjede samo.

**Zmenšení textu na šířku sloupce vyšlo zdarma:** legacy pro turnusy používá „zmenšovaný text na
šířku sloupce" (typ položky 5) a náš `CellRenderer` zmenšuje každou buňku sám. Označení jako
„51164.02" se tedy vejde do úzkého sloupce spoje bez zásahu.

Ověřeno: .NET **1240/1240**, Caché **137/137**, 5 nových testů prvku + 4 datové. Mutacemi: počet
řádků z prvního spoje místo maxima, turnusy za platností, dorovnání opakováním, nepočítat výšku -
každou chytil právě ten test, který ji chytit měl. Caché mutace „parametr ignorovat a tahat vždy"
padne kromě datového testu i **na třech zlatých odtiscích**, takže na neopatrné zapnutí je pojistka
i tam.

⚠️ **Mutační skript musí vynutit rebuild.** `shutil.move` vrátil zdroj s **původním časem**, takže
MSBuild považoval zmutovanou DLL za aktuální a testy pak běžely proti ní - červený test vypadal
zeleně a naopak. Do skriptu proto patří `dotnet build -t:Rebuild` před každým během.

## 2026-08-28 (7) - 🔁 obousměrný tisk: strany prohozené podle tisku ze starého modulu

Jan vytiskl obousměrný JŘ ve starém modulu (linka `0680042`, 1+1 spoje) a papír rozhodl otázku, kterou
**nešlo rozhodnout ze zdroje**: nalevo stojí spoj **71** s časy 07:33 → 07:38 a značkou „od" u první
zastávky, napravo spoj **72** s časy 07:53 → 07:39 a značkou „př".

**Nalevo tedy patří směr, jehož pořadí sleduje seznam zastávek** (časy jdou odshora dolů), napravo
opačný. Měli jsme to obráceně - prohozeno.

⚠️ **Odvození ze zdroje bylo chybné a znělo přesvědčivě**: v obousměrném bloku začínají sloupce spojů
na indexu 5 a popisné mají 1-4 (`JRapiLinPrtWin.mac:3429`), z čehož jsem usoudil „oba směry vpravo za
sebou". Jenže **index sloupce v tom mezikroku není vizuální pořadí** - o straně rozhoduje **typ**
sloupce (7 „Spoj" × 9 „Spoj před zastávkou"). Přesně ten případ, na který je `R6`: paritu doloží
porovnání se starým modulem, ne čtení kódu ani náš vlastní odtisk.

Dva testy polohy stran padly hned po prohození (kódovaly ten obrácený předpoklad) - to je zároveň
doklad, že tu stranu opravdu měří. .NET **1235/1235**.

## 2026-08-28 (6) - 🔍 sonda: nejmenší linky se spoji v obou směrech

Jan zapnul u linky „Tisk obousměrných JŘ" na A4, vytiskl - a **výstup se nezměnil**. Sonda hledá
linku, na které se to musí projevit.

**Nastavení bylo správně.** Ověřeno ve zdroji: rozbalovátko ukládá `SelectedIndex`, takže „Ano" = 1,
a engine čeká právě `1` (`Linky.cls:1825`, `formTISKJRReportDotNet.vb:418`). Uloží se do
`^JRL(sku,lin,"PATCH","prt","win",13)` pro A4 a `…,25)` pro A5 - **dvě samostatná nastavení**, každé
na své záložce „Win tisk JŘ A4/A5"; klient čte jen to, které odpovídá **velikosti papíru sestavy**.

**Nejpravděpodobnější příčina je tichý fallback**: zrcadlo zůstane jen tehdy, když se společný blok
vejde bez jediného řezu (`JRapiLinPrtWin.mac:627`). Na velké lince tedy zapnutá volba nezmění nic
a uživatel nemá jak poznat, že se neuplatnila - **první praktický doklad, proč jsme ten fallback
nepřevzali.**

Sonda proto najde nejmenší linky se spoji v **obou** směrech (bez toho legacy zrcadlo vypne rovnou).
V `CB_CSADAD` vyšly linky s 1+1 spoji a 3-6 zastávkami - na těch se blok vejde vždy a je vidět
jediné, na čem záleží: jestli je opačný směr **nalevo** od zastávek, nebo vpravo za prvním směrem.

⚠️ **Dvě poučení z psaní té sondy** (obojí stálo čas): ObjectScript **neumí zlomit výraz na víc
řádků** a v **postkondici nesmí být mezery** kolem `=` nebo `'<`. A hlavně: chyba kompilace se
projevila jako `<SYNTAX>` **ze staré verze třídy** - server si nechal tu předchozí, takže to vypadalo,
že se změna nenasadila. Vynucený běh hooku vypsal skutečné chyby.


## 2026-08-28 (5) - ✨ obousměrný tisk: spoje po obou stranách zastávek (`L6b` hotovo)

Druhá polovina `L6b` - **kreslení**. Se zapnutým parametrem „Obousměrný tisk" dostane linková tabulka
sloupce spojů **po obou stranách** zastávek (legacy sloupec „Spoj před zastávkou", typ 9). *Která
strana je která, se srovnalo podle tisku ze starého modulu až v (7) - do té chvíle to bylo obráceně.* Zrcadlený blok dostal i vlastní pás skupin dnů, čísla spojů,
řádek platnosti se značkami a značky návaznosti u času - jinak by jeho sloupce neřekly, ve které dny
spoj jede.

**Která strana je která, rozhoduje prvek** (tvůj požadavek), a nenastavuje se to - dvě volby téhož
(pořadí zastávek × strana) by si šlapaly.

⚠️ **Dělení na bloky jde po „slotech", ne po spojích.** Blok bere týž úsek z **obou** stran, takže
je z něj celá tabulka; kdyby se dělilo po jednom seznamu, první blok by vyšel jen se spoji tam
a druhý jen se spoji zpět. Do šířky se počítají sloupce obou stran - jinak zrcadlený blok tabulku
vystrčí za hranu prvku.

Ověřeno: .NET **1235/1235** + 709 + 620, Caché 133/133, mutací čtyř cest.

⚠️ **První verze těchto testů byla bezcenná a odhalila to až mutace: neprošla ani jedna ze čtyř.**
Měřily „je tam víc inkoustu", jenže při dvou spojích má tabulka **stejnou šířku i podobný inkoust**,
ať jsou sloupce vedle sebe, nebo na obou stranách. Přepsané měří **polohu** nad skoro prázdnými
fixturami (v obrázku je vždy jen jedna věc). Dvě mutace navíc nešly pokrýt vůbec, dokud nepřibyly
testy na šipku (posun popisného bloku se pozná jen přes ni a přes piktogram zastávky - buňky si
pozici počítají sekvenčně) a na těsnou šířku prvku.

⚠️ **Druhá past ve fixtuře:** s prázdnými názvy zastávek **zmizí celý sloupec zastávek** („prázdný
sloupec se netiskne"), takže se posune všechno a test měří něco jiného, než si myslí.

**Co ještě chybí** (vědomě, ne dluh z nedbalosti): zrcadlený blok **nemá vlastní šipku směru** -
legacy na to má druhý sloupec (typ 8), u nás je sloupec šipky jeden a ukazuje hlavní směr. A
**dorovnání prázdnými sloupci** (#8372) doplňuje jen pravou stranu.


## 2026-08-28 (4) - ✨ data pro obousměrný tisk: oba směry na jedné stránce (`L6b`, 1/2)

První polovina `L6b` - **datová**. Nový parametr sestavy **„Obousměrný tisk"** (výchozí vypnuto,
jen AD; legacy MHD engine tento parametr nečte vůbec). Se zapnutým vrací agenda **jednu** stránku
se spoji obou směrů nad společným seznamem zastávek a **každý spoj nese `SMER`**.

⚠️ **Server posílá jen fakta, tvar drží klient** (požadavek Jan 28. 8.: layout musí zůstat v C#, ať
nedopadneme jako starý modul, kde konkrétní rozvržení počítá Caché). Původní návrh byl poslat dvě
kolekce (`SPOJE` a `SPOJE_ZPET`) - **zahozen**, protože už tím rozdělením říká „tyhle patří na druhou
stranu tabulky", tedy propašovává layout do dat. Směr u spoje je fakt; strana tabulky je rozhodnutí
prvku.

**Sloučení nestálo skoro nic, a to je nález:** časy se zarovnávají podle **pořadí zastávky**
(`wMap(pp)`), ne podle pozice ve směru, takže spoje opačného směru sednou na tentýž seznam řádků bez
jediného přepočtu. Ověřeno na lince 0330511: zpětný spoj má časy 05:08 → 04:40 **klesající dolů** po
sdíleném seznamu, tedy správně proti směru.

- **Legenda NZ hledá text v obou směrech.** Dosud procházela spoje jen „svého" směru, takže značka
  vidět jen u zpětného spoje by na sloučené stránce zůstala bez textu.
- **`SMER` u stránky je u sloučené prázdný** - stránka nese oba a jeden z nich by šablona vytiskla
  jako by byl jediný. Konce linky drží `VYCHOZI_ZASTAVKA` a `POPIS_SMER`.
- **Pravidlo „v jednom směru nejsou spoje → zrcadlo ne" nepotřebuje výjimku**: taková linka prostě
  nemá co sloučit a vyjde jako dnes.

Ověřeno: Caché **133/133**, .NET 1228/1228, ověřeno mutací (s ignorovaným parametrem padnou dva
testy). ⚠️ **Mutace odhalila slabý test**: „časy sedí na společné řádky" procházelo i bez slévání,
protože po směrech má každá stránka svých 31 zastávek - teď si nejdřív ověří, že stránka je jedna.
Zlaté odtisky linkového JŘ přebaseline-ovány: **mění se jen md5, počty do jednoho shodné** (nové
pole `SMER`), přesně případ, který ten test sám popisuje.

⚠️ **Kreslení zbývá** - prvek dnes sloupce nalevo od zastávek neumí, takže zapnutý parametr se ve
výstupu ještě neprojeví. To je druhá polovina `L6b`.

⚠️ **Nález mimo tuhle práci: sdílený Caché runner sčítá i cizí běhy.** `RunPackage` iteruje všechny
výsledky, které v `^UnitTest.Result` vzniknou po jeho startu, takže když na dev serveru běží současně
jiná sada (typicky kolegův pre-push hook), přičte si její testy. Změřeno: dva běhy dnes hlásily
**186/186** místo 128 - a rozdíl 58 je přesně `EDISONMAIN2` (23) + `EDControls` (35) Caché testů.
Vedeno v roadmapě; dokud to platí, jsou počty „Caché X/X" v changelogu nespolehlivé nahoru.


## 2026-08-28 (3) - 🐞 barva nastavená u šipky ji neobarvila (nešlo poznat, kterou zónou se řídí)

Nález Jan: nastavil sloupci šipky **barvu textu** a šipka zůstala černá; barva se přitom projevila
na textu záhlaví („Šipka"). Mechanicky to fungovalo správně - dialog má **dvě** zóny a šipka se
kreslí přes datové řádky, takže poslouchá „Styl datových buněk", zatímco nastavená byla zóna záhlaví.

Jenže **u sloupce, kde se v datech nekreslí žádný text, se ta cesta nedá uhádnout**, a to je vada
dialogu, ne uživatele. Datová zóna proto u šipky:

- se jmenuje **„Styl šipky"** místo „Styl datových buněk",
- nabízí **jen barvu textu a pozadí** - font, řez, podtržení, zarovnání ani odsazení na vektorovou
  šipku nemají vliv, takže tam jen mátly,
- má pod sebou nápovědu „Šipka se kreslí barvou textu. Font ani zarovnání na ni nemají vliv."

⚠️ Je to **první reálné použití** `CellStyleEditOptions` k per-kontextovému skrytí voleb; do teď byly
všude `AllEnabled` a mechanismus čekal na první případ, kde nemá smysl nabízet všechno
(`mvvm-patterns.md` s tím počítá).

Ověřeno: .NET 1228/1228 + 709 + 620. Testy dialogu jsou v `Windows/`, ne u renderovacích - VM je bez
WPF (R4), takže jde testovat přímo přes mock služeb.


## 2026-08-28 (2) - 🐞 sloupec šipky se v dialogu jmenoval „Arrow"

Nález Jan: v dialogu sloupců svítil anglický kód. Mapa lidských názvů propadá u neznámého klíče
**na klíč sám**, takže se do UI dostane kódový název - a nový klíč se tam musí doplnit ručně.
Sloupec se teď jmenuje **„Šipka směru"**, shodně s termínem u vývěsky („Značky a šipka směru",
„Barva šipky").

⚠️ Ta past zůstává pro každý příští sloupec, proto na to jde test. Sám kód sloupce (`Arrow`) je
**wire hodnota v šablonách** a zůstává anglicky podle konvence „kód anglicky, UI česky".

Ověřeno: .NET 1225/1225 + 709 + 620.


## 2026-08-28 (1) - ✨ šipka směru jízdy v linkovém JŘ (`L6a`)

Sloupec, ve kterém běží **svislá šipka přes řádky zastávek** - první z rozpadlé vlny `L6`
(zbytek repertoáru starého modulu). Zapíná se jako každý jiný sloupec v dialogu sloupců.

⚠️ **Kreslí se naší vektorovou šipkou ze zastávkového JŘ, ne glyfy ze symbolového fontu jako legacy**
(rozhodnutí Jan 28. 8.: co už umíme, se nemá duplikovat). Kresba se proto vytáhla do
`DirectionArrowRenderer` a vývěska ji teď volá odtud - **beze změny jediného pixelu**, což hlídají
její zlaté odtisky. Vedlejší přínos: šipka v linkovém JŘ je nastavitelná (tvar hrotu, styl čáry),
což legacy neumí, protože má tvar zadrátovaný ve fontu.

- **Barvu si bere z textové barvy svého sloupce**, takže nepřidává žádné nové nastavení do panelu.
- **Výchozí stav skrytý**, aby se existujícím šablonám nezměnil vzhled.
- Do starších šablon se sloupec doplní **migrací 1.9.0** - dialog sloupců je umí jen přeskládat
  a skrýt, ne přidat, takže bez migrace by šipka byla dostupná jen v nově zakládaných šablonách.

⚠️ **Šipka má vlastní výjimku z pravidla „prázdný sloupec se netiskne"** - nemá žádný text, takže by
ji to pravidlo schovalo vždycky, i když ji uživatel zapne.

Ověřeno: .NET 1224/1224 + 709 + 620, ověřeno mutací čtyř cest (nekreslit, zadrátovaná barva místo
barvy sloupce, šipka jen přes jeden řádek, chybějící výjimka z auto-hide). ⚠️ Jeden test se musel
přeformulovat: „se skrytým sloupcem tam nic není" **neplatí** - skrytý sloupec z rozvržení zmizí,
takže se na jeho místo posunou zastávky. Tvrdí se proto rovnost obrázků se šablonou, která ten
sloupec vůbec nemá, což je zároveň přesně ta záruka, kterou potřebuje migrace.


## 2026-08-27 (8) - ✨ bez nadpisu se nad popisnými sloupci nekreslí nic (`L9-R4` rozhodnuto)

Poslední otevřené rozhodnutí z `L9`: co má dělat pás nad popisnými sloupci, když je jeho popisek
prázdný. Dosud se kreslila **prázdná šedá krabice**.

**Rozhodl to legacy, ne vkus** (dotaz Jan: „řešilo se tohle nějak v legacy systému?"). Starý modul
nad popisnými sloupci pás **vůbec nemá** - skupina odjezdů je v jeho kontraktu atribut *jen* sloupců
spojů („pouze pro typ sloupce 7,9", `JRapiLinPrtWin.mac:246`), takže z popisných sloupců není z čeho
ho nakreslit. Je to zároveň důvod původní stížnosti kolegy: „nelze mít jeden nadpis pro info
sloupce" nebyla vada nastavení, ten pojem tam neexistoval.

Nové chování je tedy obojí naráz:

- **prázdný popisek = chování starého modulu** (nad popisnými sloupci nic, ani podbarvení),
- **vyplněný popisek = featura nad rámec legacy**, objeví se, když si o ni uživatel řekne textem.

⚠️ **Zaškrtávátko se nezavedlo, i když byl návrh** (Jan: „když si nejsme jistí, dává smysl to udělat
na checkbox"). Podmínka „nejsme jistí" po přečtení legacy nepadla - a byla by to třetí volba k pásu
vedle nadpisu a slití, která nastavuje něco, co se rozhodne samo z toho, jestli je text prázdný.

Ověřeno: .NET 1211/1211 + 709 + 612, ověřeno mutací (s vrácenou šedou krabicí test padá). Případ,
kdy popisek nemá **žádná** skupina, se nemění - tam se pás nekreslil ani dosud.


## 2026-08-27 (7) - ✨ bezbariérové vozidlo se tiskne vozíčkem, ne zkratkou „BEV"

Nález Jan na vzoru 940: „ve starém je ta ikonka vozíčku, teď vypíšeme textem BEV."

Platilo u nás pravidlo „víceznakový kód je zkratka, ne piktogram" - a to je **proti starému
modulu**: ten má celou rodinu atributů spoje v číselníku znaků jako **symboly**
(`JRapiLinPrtWin.mac:1556` a dál; BEV je id 22 „bezbarierovy", CBE id 56 „castecne bezbarierovy
vuz"). Pravidlo tedy padlo; rozhoduje jen povolený seznam a to, jestli je pozice ve fontu ověřená.

- **BEV → vozíček**, **CBE → vozíček s doprovodem** (částečně bezbariérové vozidlo).
- Platí to i v **legendě** poznámek, protože jde přes tutéž mapu značek.
- Vedlejší efekt: v řádku platnosti zabere piktogram **méně místa** než tříznaková zkratka.

Pozice ve fontu ověřená tak, jak to pravidlo u seznamu vyžaduje - vykreslením tabulky glyfů
**v obou** fontech (CZ i SK): 72 je vozíček, 70 vozíček s doprovodem, shodně v obou. ⚠️ Zbytek
rodiny (IDS, NKA, OBC, PCZ, PJI, POS, RBU, SAO, RBP) zůstává textem: starý modul pro ně symboly má,
ale pozici ve fontu jsem u nich neurčil, a hádat ji znamená vytisknout cizí piktogram.

Ověřeno: .NET 1210/1210 + 709 + 612.

⚠️ **Při čtení legacy vypadl další rozdíl, neopravený:** starý modul tiskne **nejvýš dvě značky
na řádek platnosti** a zbytek zalomí na další řádky (`#define POCETZNAKUPLATNOSTI 2`,
`JRapiLinPrtWin.mac:25` a `:3763`). My posíláme značky jako jedno pole a kreslíme je do jednoho
řádku, takže u spoje s víc značkami se řádek přeplní. Vedeno v roadmapě.


## 2026-08-27 (6) - 🐞 značka u minuty vylézala ze sloupce (parita s legacy: sloupec se rozšíří)

Nález Jan na vzoru 940: „když má minuta u sebe nějakou značku, tak ta značka to posune doprava, ale
šířka sloupce se počítá na základě šířky pouze minut, takže značka + minuta pak tu minutu posune ven
mimo šířku sloupce." Přesně tak to bylo - minimální šířka se měřila ze `"00:00"`, značka se do ní
nezapočítala, a značka s časem se navíc kreslí ručně, takže se ani nezmenšuje na buňku.

**Řešení vzaté ze starého modulu, ne vymyšlené** (žádost Jan: nejdřív se podívat do legacy).
Starý modul má pro sloupec spoje **čtyři šířky** a vybírá mezi nimi podle **největšího počtu značek
v tom sloupci** - text tedy nezmenšuje, dělá místo:

- `JRapiLinPrtWin.mac:1793-1798` - volba šířky (0 značek → 1, jedna → 2, dvě → 3, tři a víc → 4)
- `JRapiLinPrtWin.mac:1769-1771` - počet se bere jako maximum přes řádky **toho** sloupce
- `JRapiLinPrtWin.mac:2611` - slot, ze kterého se počítá, je `column-0.01` = „znacky spoje"

⚠️ **Sloupce spojů proto přestaly být stejně široké** (rozhodnutí Jan 27. 8. po přečtení legacy):
širší je jen ten, který značku nese. Šetří to místem, takže se na řádek vejde víc spojů - a starý
tisk tak vypadá taky. Šířky samotné si legacy neměří, dostává je zvenčí (`MS,7,1..4`), takže se
z něj přebírá **pravidlo**, ne čísla; my si značku měříme sami tím fontem, kterým ji kreslíme.

Místo na značky se odečítá ze zbytku šířky **před** jeho rozdělením mezi automatické sloupce, jinak
by tabulka přetekla. Rezerva se bere jako maximum přes okna po sobě jdoucích spojů - základ sloupce
tak zůstává pro všechny bloky stejný (invariant z 25. 8.) a žádný blok nepřeteče.

Ověřeno: .NET 1209/1209 + 709 + 612, **mutací všech čtyř nových cest** (bez místa na značku, bez
odečtení rezervy, značka kreslená do jinak širokého rámu, pás počítající šířku skupiny ze staré
jednotné šířky). ⚠️ Dvě z těch mutací neprošly na první pokus, protože testy měřily moc hrubě:
podbarvení pásu se hledalo v celém svislém pásu, kde ho má i řádek pod ním, a vystředění značky
ve sloupci se nekontrolovalo vůbec.


## 2026-08-27 (5) - 🐞 vodorovné čáry pokračovaly za konec tabulky do volného místa

Nález Jan na vzoru 940: „vpravo, když skončí ta tabulka, tak tam pak takhle pokračují do pravého
okraje stránky ty vodorovné čáry, ale žádné vertikální."

Nebyl to záměr - jen se za poslední sloupec dosud nikdo nepodíval. Vodorovné čáry a podbarvení řádku
se kreslily přes **šířku prvku**, kdežto buňky jen po konec tabulky. Kreslí se teď po **šířku
tabulky**, se stropem na šířce prvku (kdyby se tabulka nevešla, nesmí malovat vedle).

**Volné místo vpravo samo záměr je** a zůstává: blok nemusí vyjít na celou šířku a dorovnání
prázdnými sloupci je samostatná volba (#8372). Jen má být opravdu volné.

⚠️ Je to **stejná třída jako mezera** (mřížka kreslená mimo tabulku) a přišlo se na to jen proto, že
mezery přinutily oko podívat se na okraje. Kontrola zůstává v sadě k mezerám, ať se to nevrátí.

Ověřeno: .NET 1205/1205 + 709 + 612, ověřeno mutací (s šířkou prvku místo šířky tabulky nový test
padne).


## 2026-08-27 (4) - 🐞 hranice skupin sloupců začínala až pod pásem s nadpisy

Nález Jan na vzoru 940: „v té první subtabulce Směr jízdy to má pravý okraj vykreslený, ale levý ne."

Rozpadlo se to na dvě věci. **Chybějící levý okraj je v pořádku** - vnější hrana tabulky patří
rámečku prvku a vzor 940 ho podle PDF nemá (tloušťka 0), takže tam okraj není nikde, nejen u té
skupiny. Svislé čáry jsou mřížka a ta se kreslí jen **mezi** sloupci.

**Vada byla jinde:** hranice skupiny opravdu začínala až **pod** pásem s nadpisy (změřeno: čára
běžela y 51-168 ze 169 px, pás nad ní žádné svislé čáry neměl). Vypnuly se tam kdysi celé, aby
„nerozsekaly nadpis přes několik sloupců spojů" - jenže v pásu je **jedna buňka na skupinu**, takže
čára mezi nimi padne přesně na hranici skupiny a žádný nadpis neprotne. Skupina proto vypadala jako
rámeček s chybějícím horním kusem boku.

⚠️ **Projeví se i v šablonách bez mezer** (rozhodnutí Jan 27. 8.): mezi pásy „Pracovní dny" a
„Soboty" je nově čára, která tam dosud nebyla. Je to změna vzhledu existujících šablon, proto na to
jde vlastní test.

Na hraně skupiny zůstává **jedna** čára i v pásu - nese ji buňka mezery a skupina za ní už svoji
vlevo nekreslí, stejně jako v ostatních řádcích.

Ověřeno: .NET 1204/1204 + 709 + 612, ověřeno mutací obou částí zvlášť (bez čar v pásu padnou
2 testy, bez potlačení druhé čáry 1). ⚠️ Měření v testu hledá čáru v **okně ±2 px**, ne v jednom
sloupci pixelů: svislou čáru kreslí `ColumnRenderer` **před** buňkou, takže pozadí buňky vpravo
překryje její pravou polovinu a vzorkování přesně na hraně půlku čáry mine. První verze testu na to
naletěla a hlásila „čára chybí", i když se kreslila.


## 2026-08-27 (3) - 🐞 mezerou mezi skupinami sloupců procházely vodorovné čáry

Nález Jan na vzoru 940, hned jak mezery zapnul: „zůstane tam spojující úsečka mezi levou částí
pravého okraje a pravou částí levého", a u přípojných řádků „ta mezera udělána není vůbec".

Obojí mělo **jednu příčinu**: vodorovné čáry a pozadí řádku se kreslí přes **celou šířku prvku**,
takže mezeru přemostily. U přípojného řádku, který má čáru nad i pod sebou, se tím mezera zavřela
z obou stran a nebyla vidět vůbec - proto to vypadalo jako dvě různé vady.

Řádkové jádro teď umí buňku označit jako **mezeru** a čáry i pozadí kreslí po úsecích mezi nimi.
Je to ve sdíleném jádru, ne v linkovém prvku: mezery jsou pojem `skupiny sloupců` a ten má podle
`L9-R3` platit pro všechny tabulkové prvky. Ostatní prvky mezery neposílají, takže se jim nic nemění.

Pozadí řádku se odděluje z téhož důvodu jako čáry: podbarvení buněk se v mezeře přeruší samo, takže
průchozí podbarvení celého řádku by proti němu vypadalo jako chyba.

Ověřeno: .NET 1202/1202 + 709 + 612, ověřeno mutací (bez označení buňky i s čarou přes celou šířku
padne nový test), potvrzeno okem na vzoru 940.


## 2026-08-27 (2) - ✨ mezera mezi skupinami sloupců linkového JŘ (`L9` dokončeno)

Poslední ze tří mezer `L9`. Tabulka už nemusí vypadat jako jedna souvislá mřížka: **popisný blok,
Pracovní dny a Víkendy se dají od sebe odsadit volným místem**, tak jak to má vzor IDZK 940.
Nastavuje se v mm („Mezera mezi skupinami sloupců"), v panelu i v Caché formuláři.

- **Výchozí 0 = beze změny.** Existující šablony vypadají stejně jako dosud; nenulová výchozí
  hodnota by zúžila sloupce spojů všem a u linek na hraně šířky ubrala spoj z bloku.
- **Na hraně skupiny zůstane jedna čára**, pak volno - ne dvě čáry kolem mezery. Vzor to tak má
  a dvě čáry těsně u sebe čtou jako chyba tisku.
- **Mezera se bere ze šířky prvku**, takže se do řádku vejde méně spojů; u dlouhé linky se tím
  může přidat blok pod tabulku.

⚠️ **Souřadnice sloupců spojů se soustředily na jedno místo** (`SloupcovaMrizka`). Do teď si
odsazení počítala každá cesta sama jako „šířka popisných + index × šířka sloupce" - buňky, značky
platnosti, značka návaznosti vedle času. Mezera ten vzorec ruší, takže cesta, která by o ní nevěděla,
kreslí značku do sloupce cizího spoje. Proto jedno místo, které o mezerách ví, a cesty, které se ho
jen ptají.

⚠️ **Rezervuje se za celou linku, ne za kreslený blok.** Kolik mezer blok má, závisí na tom, které
skupiny dnů do něj padly - podle bloku by tedy měl každý blok jinak široké sloupce, a to je přesně
vada z 25. 8. (poslední blok se sloupci 58,8 mm proti 6,4 mm v prvním). Cena: blok s méně skupinami
nechá vpravo o mezeru víc volna.

Ověřeno: .NET 1201/1201 + 709 + 612, **mutací všech pěti cest zvlášť** (každá zapomenutá mezera
shodí aspoň jeden test), formulář ověřen voláním serveru. ⚠️ Dvě děravá místa v testech odhalila
až mutace: „v mezeře nesmí být nic" nechytilo značky platnosti (zapomenutá mezera je nehodí do
mezery, ale do cizího sloupce) a zapomenutá rezerva šířky nepřeteče, jen zbytečně přidá blok.

⚠️ **Vodorovné čáry mezerou procházejí** - stejně jako dnes procházejí volným místem vpravo za
poslední tabulkou. Vzor 940 vodorovné linky nemá, takže se to na něm neprojeví; u šablony, která je
má, to chce tvoje oči.


## 2026-08-27 (1) - 🐞 přepínání firmy pro vývojáře se z publikované verze nedalo zapnout

Nástroj **Nástroje → Přepnout firmu (DEV)** je v aplikaci od začátku, ale z `.exe` v `C:\Edison\`
se nikdy neukázal (našel Jan při zkoušení). Visel na neprázdném klíči k AI asistentovi - a ten žije
jen v **lokálním, gitignorovaném** configu vedle buildu ve zdrojích, ne vedle publikovaného exe.
Dvě věci, které spolu nemají nic společného.

Teď to zapíná **vlastní klíč** `dev.firmSwitch` v configu (výchozí vypnuto, aby to zákazník nikdy
neviděl). Zapnuté je v lokálním configu i v tom u publikované verze.

⚠️ **Blokovalo to procházení testovacích linek** - z osmi linek sady leží tři v jiné firmě
(`CB_GWBUS`, `VS_CSAD`), takže bez přepínače na ně nebylo jak se dostat.

⚠️ **`VS_CSAD` v seznamu firem není** (`DevFirms`), takže „písmena ze zóny" zůstávají nedosažitelná
i tak - týká se to ale linky, na kterou v testovacích datech není vzorek, tedy už evidovaného dluhu.

Ověřeno: .NET 1194/1194 (2 nové na slučování nového oddílu configu - zapomenutý oddíl se **tiše
zahodí**, přesně tak zmizel 19. 8. klíč k distribučnímu kanálu). ⚠️ **Samotnou viditelnost menu
test nekryje** - je v code-behind okna; ověřeno spuštěním publikované verze.


## 2026-08-26 (4) - ✨ popisné sloupce linkového JŘ jsou jedna skupina (`L9`)

Kolega si na starém modulu stěžoval, že „ohraničení jednotlivých km sloupců nejde odstranit" a že
„nelze mít jeden nadpis pro info sloupce". Obojí teď jde, a je to **jedna věc**: popisné sloupce
(zóna, km, Tč, zastávka, MHD) se chovají jako **jedna skupina**.

- **Nadpis popisných sloupců** - jeden text nad všemi, vzor IDZK 940 tam má „↓ Směr jízdy ↓".
- **Popisné sloupce bez vnitřních čar** - kreslí se jako jeden slitý blok, ale **čáry mezi sloupci
  spojů zůstanou**. Do teď šlo vypnout jen celou mřížku prvku, tedy i je - a to byla ta stížnost.

Obojí je i v Caché formuláři, tedy v hlavní editační cestě, ne jen v panelu.

⚠️ **Nadpis se odvázal od skupin dnů.** Pás nad sloupci se dosud kreslil jen tehdy, když měla linka
víc než jednu skupinu dnů - takže na lince s jedinou skupinou by se nadpis popisných sloupců vůbec
neobjevil. Nadpis je vlastnost popisné skupiny, ne skupin dnů. **Odhalil to test, ne úvaha.**

⚠️ **Mezera mezi bloky sloupců ve vlně zbývá** a je to větší kus, než to vypadá: šířka popisného
bloku slouží zároveň jako odsazení, od kterého se počítají sloupce spojů, a čtyři ruční kreslicí
cesty (řádek platnosti, značky v buňce, piktogram zastávky, dělení na bloky) z toho čísla vycházejí.
Vloženou mezerou se ten souřadnicový model rozejde, takže tomu patří vlastní krok.

Ověřeno: .NET 1192/1192 + 690 + 595, ověřeno mutací obou vlastností zvlášť, formulář ověřen
voláním serveru. Chování s prázdným nadpisem zůstává jako dřív (`L9-R4` je nerozhodnutá).
**Proklikáno 27. 8. (Jan)** na linkách s jednou i více skupinami dnů, v panelu i ve formuláři prvku,
včetně linek, kde většina popisných sloupců chybí; zapnutí i vypnutí slití.


## 2026-08-26 (3) - 🐞 zlom odstavce v poznámce se tiskl jako znak `#`

V legendě vyšlo „…jsou k nahlédnutí u řidiče.**#**INTEGROVANÉHO SYSTÉMU…" (nahlásil Jan). Agenda
posílá `#` jako **oddělovač odstavců** - má u toho i komentář, že prázdný odstavec starý modul
tiskne jako mezeru, a kvůli tomu ho normalizuje - ale klient ten oddělovač **nikde
neinterpretoval**, takže se vypsal jako písmeno uprostřed věty.

Převod na zlom řádku je při **parsování poznámky**, tedy na jednom místě. Záměrně ne u kreslení:
výška poznámky se počítá jinou cestou než render a obojí jde přes tentýž wrap, který na zlom
řádku lámat umí. Kdyby se odstavce dělily až u kreslení, výška by o nich nevěděla a legenda by
přetékala - a to je v projektu opakovaná třída chyb.

⚠️ **První verze testu neměřila nic** a odhalila to až mutace: na realisticky dlouhém textu se
odstavce zalomí na dva řádky tak i tak, takže výška se zalomením i bez něj vyšla stejně. Test teď
používá dvě krátké věty, které se spojené vejdou na jeden řádek. Je to potřetí za den, co fixtura
vypadala rozumně a neověřovala nic.

Ověřeno: .NET 1189/1189 + 690 + 595, ověřeno mutací (bez opravy padnou 2 testy ze 3).


## 2026-08-26 (2) - 🐞 značky v legendě linkového JŘ se kreslily písmeny

V legendě vyšlo „vyčká“ jako `m`, pracovní dny jako `X` a svátky jako `+` - místo dutého
trojúhelníku, zkřížených kladiv a kříže. V tabulce se přitom ty samé značky kreslily správně, takže
táž značka vypadala jinak v tabulce a jinak ve své vlastní legendě (našel Jan při průchodu).

Příčina byl rozpojený drát: linkové agendy posílají typ poznámky jako `"znak"`, vývěskové jako
`"specialni"`, a klient znal jen to druhé. Neznámé slovo padne na „obyčejný text“, takže se vypsalo
surové písmeno - a **celý aparát piktogramů linkového JŘ se v legendě nikdy nespustil**.

Tolerance k neznámému typu zůstává záměrem (starší agenda nesmí prvek shodit), a proto to teď
hlídá `PoznamkaTypDriftTests`: vyparsuje typy posílané v `.cls` a ověří, že klient zná každý
z nich. **Dosud to nehlídalo nic** - ostatní driftové testy kontrolují jména polí, ne slovník
jejich hodnot.

Ověřeno: .NET 1186/1186 + 690 + 595, ověřeno mutací (bez opravy spadne 1 test ze 4).


## 2026-08-26 (1) - 🐞 ve výběru linky nešlo přepnout skupinu linek

U linkového JŘ nabídl výběr instance vždy jen první skupinu linek a přepnout ji nešlo, takže na
linku z jiné skupiny se uživatel nedostal (nahlásil Jan). Ve firmě CB_CSADAD to znamenalo, že dvě
z pěti testovacích linek byly z jednoho přihlášení nedosažitelné - leží ve skupinách 591 a 801.

Přepínač v dialogu přitom **existoval celou dobu** a schovává se, když agenda žádné skupiny
nevrátí. Chyběl jediný kus: linkové agendy neměly metodu, která seznam skupin dodá, i když filtr
podle skupiny už přijímaly. Doplněna u AD i u městské varianty.

Městská je zatím skrytá, ale dostala to taky - je to stejná chybějící metoda a jinak by se
zveřejnila s tímtéž problémem.

⚠️ **Vědomá kopie ze zastávkových agend** (obě AD čtou týž `^JRL`, obě MHD týž číselník). Zastávkových
se podle CLAUDE.md nedotýkáme, takže sjednocení readerů zůstává na vlnu dedup Caché enginů (`L8`).

Ověřeno: Caché 128/128, .NET 1182/1182, obě změněné třídy ověřené voláním serveru.
⚠️ **Kolik skupin se v dialogu ukáže, stroj neověří** - volání přes vývojový profil vidí jiná data
než přihlášená session (i zastávková agenda v něm vrátí jednu skupinu, i když sonda najde v téže
firmě tři). **Proklikáno 27. 8. (Jan): přepínání skupin funguje.**


## 2026-08-25 (24) - 🧹 výška sekce už nerezervuje místo na posun, ke kterému nedojde

Výpočet výšky sekce rostoucímu prvku přičítal přírůstek k pozici všech prvků pod ním, jenže
kreslení je nechává na jejich souřadnicích. Sekce si tím nárokovala místo pro posun, ke kterému
nikdy nedošlo. Odstraněno; tok patří **mezi sekce** a tam funguje.

Uživatel to pozná jen tam, kde prvek pod dynamickou tabulkou leží níž než její zástupný box -
sekce tam byla o ten rozdíl vyšší.

⚠️ **Test k tomu jsem musel psát třikrát** a pokaždé to odhalila až mutace:
- s textovým prvkem nešlo nic naměřit (zástupnou výšku počítá **týmž vzorcem** jako tu z dat, rozdíl je nula),
- s tabulkou, ale prvkem pod ní na 12 mm, se rozdíl schoval pod vlastní výšku tabulky,
- teprve prvek **pod zástupným boxem tabulky** (60 mm) ten rozdíl ukáže.

Ověřeno: .NET 1182/1182, ověřeno mutací.

## 2026-08-25 (23) - 🧱 dorovnání neúplného bloku prázdnými sloupci (#8372)

Poslední blok bývá neúplný a nechá vpravo volné místo. Nová volba **„Dorovnat prázdnými sloupci"**
ho vyplní, takže mají všechny bloky stejně širokou tabulku a sestava končí rovně.

Kolik dorovnat se nemusí vymýšlet: doplní se na počet sloupců plného bloku. Odpovídá to tomu,
co dělá starý modul - jeho „padding sloupce" sjednocují počet sloupců napříč stránkami.

**Volba je vlastnost prvku** (rozhodnutí Jan 25. 8.), tedy týž vzor jako vlastní rozložení sloupců:
definici vlastní šablona, parametr ji může zrcadlit, až to bude potřeba. Protahovat parametry
sestavy až ke kreslení kvůli jedné volbě by byl velký zásah bez druhého konzumenta.

Ověřeno: .NET 1181/1181, Caché 128/128, formulář ověřen voláním serveru (vrací novou skupinu
„Sloupce" i pole). Test porovnává obě varianty proti sobě - měřit absolutní šířku nejde, poslední
prázdný sloupec nemá vpravo čáru a test by hlídal artefakt měření.

## 2026-08-25 (22) - 🐞 rámeček dynamického prvku vedl přes tabulku

Rámeček, pozadí i ořez zaoblených rohů si braly výšku **z návrháře**, ne tu, do které prvek
narostl s daty. U dynamické tabulky proto rámeček vedl přes ni a zaoblené rohy ji uřízly ve výšce
plochy z plátna (nahlásil Jan).

Nově je „jak je prvek právě teď vysoký" na **jednom místě** a bere ho rámeček, pozadí, ořez rohů
i střed otáčení. Prvek, který svoji výšku umí spočítat, ho přepíše; ostatní zůstávají na výšce
z plátna.

⚠️ **Odtisky to nemohly chytit** - všechny fixtury mají pevnou výšku, kde se obě čísla rovnají.
Nový test proto výslovně pracuje s dynamickou výškou a hledá v obrázku, kam až rámeček sahá.

Ověřeno: .NET 1180/1180, pět nových testů (jeden na prvek), ověřeno mutací.

## 2026-08-25 (21) - 🧱 nová šablona má záhlaví a obsah jako dvě sekce

Prvky uvnitř sekce se navzájem **neposouvají**, takže text, který roste s daty (výčet dopravců),
přetiskne tabulku pod sebou. Změřeno vykreslením: záhlaví s 26 dopravci leželo přes řádek se
zastávkami - přesně to, na co si stěžuje KORID (#9417) slovy „tisknou se skrytě pod JŘ".

Sekce se naopak posouvají správně: ta pod rostoucí se odsune o její výšku. Nová šablona proto
zakládá **„Záhlaví" a „Obsah" zvlášť** a překryv tím mizí bez zásahu do stránkování.

⚠️ **Analýza do dneška tvrdila opak** - že u nás problém nenastane, protože textový prvek naroste
a tabulku odsune. U toho tvrzení stálo „ověřit" a nikdo to neudělal; měření ho vyvrátilo.

⚠️ **Odebrání „fantomového" odsazení z výpočtu výšky se ukázalo jako slepá ulička.** Výpočet sice
předstírá posun, ke kterému při kreslení nedojde, ale na sedmi velikostech linky to nezměnilo počet
stránek ani o jednu. Zůstává jako úklid, ne jako oprava - a překryv neřeší.

Ověřeno: .NET 1174/1174, tři nové testy (dva drží změřené chování, jeden strukturu nové šablony),
všechny ověřené mutací.

## 2026-08-25 (20) - 🐞 stránkování počítalo bloky jinak, než se kreslí

Na konci stránky osiřel samotný pás skupin dnů a na další stránce byla hlavička dvakrát. Řez padl
doprostřed hlavičky dalšího bloku, protože **stránkování si myslelo, že blok začíná jinde, než se
opravdu nakreslí**.

Příčina: přípojné řádky patří ke spoji, takže se kreslí jen ze spojů daného bloku - výška je ale
počítala ze **všech spojů linky**. Bloky se tím ve výpočtu posouvaly dopředu a rozdíl se sčítal.
Výška se proto počítá pro každý blok podle jeho vlastních spojů; **bloky nejsou stejně vysoké**.

⚠️ **Žádný dosavadní test to nemohl chytit** - výška i zlomy se braly z téhož špatného čísla, takže
si odpovídaly. Nový test proto model **porovnává s obrázkem**: vykreslí prvek a hledá, kde v něm
doopravdy začínají hlavičky bloků. A fixtura dostala přípojné řádky jen u některých spojů - bez nich
se rozdíl nemá jak projevit (první verze testu prošla i s vrácenou chybou).

Ověřeno: .NET 1166/1166, oprava ověřená mutací.

## 2026-08-25 (19) - 🐞 pod zopakovanou hlavičkou prosvítal konec předchozí stránky

Zopakovaná hlavička na pokračovací stránce měla pod sebou **poslední řádky předchozí stránky**.
Posunutí obsahu dolů o její výšku okno jen přesunulo, místo aby obsah nad řezem odřízlo - takže
tři řádky zastávek ležely přes hlavičku (nahlásil Jan, potvrzeno vykreslením stránek do obrázků).

Obsah se teď ořezává **až od řezu** a hlavička se kreslí zvlášť nad ním.

⚠️ **První verze testu chybu neviděla.** Hlavička se kreslí až po obsahu, takže prosáklé řádky
překryla svým pozadím a pruh vyšel stejně. Test proto záhlaví **zprůhledňuje** - jinak by hlídal
prázdno (odhaleno mutací).

Ověřeno: .NET 1165/1165, oprava ověřená mutací.

## 2026-08-25 (18) - 🧱 dlouhá linka: řez mezi řádky a zopakovaná hlavička (`L5-6` hotová)

Linka s tolika zastávkami, že jeden blok přeroste stránku, se rozřezat musí. Dosud se řezalo, kde
zrovna došlo místo - tedy **doprostřed řádku zastávky** - a na pokračovací stránce zůstaly řádky
**bez čísel spojů**, takže nešlo poznat, který sloupec je který spoj.

Zlomy proto mají dvě síly: hranice bloku je **silná** (přirozený konec stránky), hranice řádku
**slabá** (náhrada, když se blok celý nevejde). Silná má přednost, i když je výš - jinak by řez padl
doprostřed bloku pokaždé, když v něm zbyl nějaký řádek.

Když řez přesto padne dovnitř bloku, **pokračovací stránka zopakuje jeho hlavu** (pás skupin dnů,
čísla spojů, platnost, negativní značky). Místo na ni vyhradí stránkování, aby ji prvek nepřetiskl
přes obsah; stránka začínající novým blokem si hlavičku přinese sama a neopakuje se.

Ověřeno: .NET 1164/1164, každá ze tří změn ověřená mutací.

## 2026-08-25 (17) - 🧱 sloupce spojů jsou ve všech blocích stejně široké

Neúplný blok si šířku počítal podle **svého** počtu spojů, takže se poslední čtyři spoje roztáhly
na sloupce 58,8 mm proti 6,4 mm v prvním bloku - čas uprostřed šesticentimetrového pole a dvě
tabulky pod sebou, které nevypadaly jako táž tabulka.

Šířky teď platí pro všechny bloky stejné a **neúplný blok nechá místo vpravo**, jako ve starém
tisku. Jestli se to místo má vyplnit prázdnými sloupci, je samostatné přání zákazníka (#8372) -
tenhle krok je jeho předpoklad.

Ověřeno: .NET 1161/1161 (ověřeno mutací).

## 2026-08-25 (16) - 🧱 stránka se láme mezi bloky, ne uprostřed tabulky

Rozřezaná linka se lámala tam, kde zrovna došla stránka. Na A4 na šířku to znamenalo první stránku
s blokem a půl a druhou s **1,4 mm proužkem** - tabulku useknutou pár řádků před koncem.

Prvek teď umí říct, **kudy se řezat smí**, a stránkování se toho drží. Je to obecná vlastnost
(`INedelitelneDily`), ne záplata na linkový JŘ: kterýkoli prvek složený z dílů, které nemají smysl
rozříznuté, ji může nabídnout. Díl vyšší než celá stránka se rozřízne i tak - jinak by se nevešel
nikdy.

⚠️ **Zúžilo to plánovaný krok „zopakovat čísla spojů na pokračovací stránce"**: v běžném případě se
blok už nekrájí, takže čísla spojů má nahoře každý. Zbývá jediný případ - linka s tolika zastávkami,
že jeden blok přeroste stránku.

Ověřeno: .NET 1159/1159 (řez na hranici bloku ověřen mutací - bez přichycení test padá).

## 2026-08-25 (15) - 🧱 široká linka se rozřeže na čitelné bloky (`L5-2`, `L5-3`, `L5-5`)

Linka se 100+ spoji se dosud zmáčkla do šířky, až z ní nebylo nic vidět. Nově se **rozřeže na
bloky pod sebou** a v každém se zopakují popisné sloupce - tak, jak to dělá starý tisk.

**Kolik spojů blok unese, určuje písmo.** Dno šířky sloupce bylo pevné (3 mm) a čas se do 3 mm
nevejde v žádném písmu: bloky sice vycházely, ale slité (nahlásil Jan). Dno se proto měří ze
šířky času „00:00" i s odsazením - zvětšení fontu teď ubere spojů na blok místo aby tabulku
zhustilo. Na zkušební lince to znamená 23 spojů na blok místo ~60.

**Pás skupin dnů je nad každým blokem.** O tom, které řádky tabulka má (pás skupin, platnost,
negativní značky), rozhodují **všechny spoje linky**, ne spoje jednoho bloku - jinak blok, který
padl celý do jedné skupiny, neměl nad sebou nadpis a nešlo poznat, ke kterým dnům patří.

⚠️ **Vodorovné opakování sekce se ukázalo jako zbytečné** (plánovaný `L5-3`). Bloky pod sebou
proteče stávající svislé stránkování, takže druhá osa v enginu nevznikla. Doloženo testem, ne
úvahou: 200 spojů × 25 zastávek = 9 bloků → 3 stránky, které na sebe navazují.

Ověřeno: .NET 1154/1154 (8 nových na rozvržení bloků, ověřené mutací - se starým dnem šířky
padají). Vizuální zkouška na lince 0330510 zbývá.

## 2026-08-24 (14) - 🧱 vlastní rozložení sloupců spojů v šabloně (`L3` dokončena)

Druhá půle. Linková tabulka **drží vlastní definici sloupců** v šabloně a posílá ji serveru, a
**parametr ji zrcadlí** - přesně tím způsobem, jaký má vývěska u odjezdů: definici vlastní šablona,
parametr ukazuje, kterému vzoru odpovídá, a „Vlastní rozložení" je **ukazatel, ne volba**.

Tvar definice i její význam jsou pro obě rodiny agend **jedno a totéž místo**, nabídka vzorů je
naopak vlastní - linka má tři, vývěska čtyři, a jinak pojmenované.

⚠️ **U linky se do volby dělení nesahá, dokud definice neexistuje.** Vývěska má pro ten stav
popisek „Automaticky (dle linky)", linka ne - její dělení jsou tři konkrétní hodnoty a je to volba
uživatele. Přepsat mu ji „automaticky" by byla táž chyba naruby, kvůli které to zrcadlo vzniklo.

Ověřeno: Caché 128/128, .NET 1139/1139. Vizuální zkouška zbývá.

## 2026-08-24 (13) - 🧱 server umí vlastní rozložení sloupců spojů (`L3`, první půle)

Linkový JŘ uměl jen tři hrubé možnosti dělení spojů. Server nově přijme **vlastní definici sloupců
ze šablony** - tutéž, jakou už umí vývěska: nadpis, dny a značky, které spoj do sloupce pustí nebo
z něj vyřadí.

**Význam definice je teď na jednom místě pro obě rodiny agend.** Jinak by táž definice udělala ve
vývěsce jiné sloupce než v lince. Liší se až to, co s výsledkem prvek udělá: vývěsce je skupina
**jeden sloupec**, linkovému JŘ **pás nad několika sloupci spojů** - a spoj patřící do dvou pásů se
vypíše **dvakrát**.

⚠️ **Je to aditivní**: dokud šablona definici neposílá, chová se všechno přesně jako dosud
(ověřeno porovnáním se starým modulem).

⚠️ **Osa Období se v lince nevyhodnocuje** - vývěska k ní má podklad z jízdních řádů, linkový JŘ ne.
Sloupec s obdobím tedy v lince omezí jen dny a značky.

**Zbývá druhá půle**: definici musí umět zadat prvek v šabloně a parametr ji má zrcadlit.

Ověřeno: Caché 128/128.

## 2026-08-24 (12) - 📅 spoji chyběl sloupec ve skupinách dnů (`L3`)

Spoj, který nemá zadanou týdenní platnost, jsme zařazovali do skupin dnů podle **odhadu ze zadaných
datumů omezení**. Vypadalo to chytřeji, ale bylo to horší: na kontrolní lince zařadil odhad spoj do
**dvou** skupin místo tří, takže se v tabulce opakoval jednou míň a **zákazníkovi chyběl sloupec**.

Starý modul to řeší jinak: zkusí dopočítat skutečnou platnost kalendářem, a **když mu to nevyjde,
dá spoj do všech skupin**. Nově to děláme stejně. Náš odhad byl vlastní logika, kterou nic
nedokládalo; tohle je doložené chováním starého tisku.

Porovnání se starým modulem to potvrdilo: na dotčené lince je shoda úplná a **na žádné jiné se nic
nezhoršilo**.

Ověřeno: Caché 127/127, .NET 1139/1139.

## 2026-08-24 (11) - 🚏 zastávka s přestupem má piktogram (`L4` uzavřena)

Poslední kus symbolů. U zastávky, kde jde přestoupit na linkovou dopravu, se **vpravo ve sloupci
zastávek** kreslí piktogram - jako v původním tisku. Na kontrolní lince to sedí na tři zastávky
(Český Krumlov, Velešín, Kaplice), shodně se starou sestavou.

Řídicí atributy (`T`, `P`) se netisknou, protože pro cestujícího nic neznamenají - stejně jako
ve starém modulu.

⚠️ Piktogram je **overlay nad hotovou buňkou**: název se kreslí vlevo a umí se zmenšit, aby se
vešel, kdežto značka sedí u pravého okraje. U hodně dlouhého názvu se můžou potkat; kdyby to
někdo uviděl, řeší se to rezervací místa v buňce.

Ověřeno: .NET 1139/1139, Caché 127/127. Vizuální kontrola zbývá.

## 2026-08-24 (10) - 🔣 legenda linkového JŘ kreslila cizí piktogramy

Legenda používala **vývěsková** pravidla i pro poznámky linkového JŘ, který má jiný slovník značek.
Důsledek: „pokračuje" má značku `p`, na které je ve fontu **kufr**, a návaznosti by vyšly jako
půlkruh a kosočtverec místo trojúhelníků.

Legenda teď pozná, odkud poznámka je, podle **druhu poznámky** - ten posílá jen linkový JŘ - a podle
toho vybere mapu. Vývěskám se nemění nic.

Ověřeno: .NET 1139/1139, Caché 127/127. Vizuální kontrola legendy zbývá.

## 2026-08-24 (9) - 🔺 návaznosti se kreslí trojúhelníky vedle času

Dořešeno tištěným vzorem staré sestavy (linka 330511): u „vyčká" je **dutý** trojúhelník, u
„navazuje" **plný**, a tisknou se **vedle času** v téže buňce. Návrhář to teď dělá stejně.

Který glyf je který, se poznalo počtem obrysů: dutý má dva, plný jeden - v českém i slovenském
fontu shodně. ⚠️ Past, kterou hlídá test: „vyčká" míří na pozici, kterou by vývěskový překlad
poslal dál na **autobus**.

**Projíždí a jede jinudy jsou tím uzavřené jako text** - font ty znaky vůbec nemá (na jejich
pozicích je čtverec a osmiúhelník), takže je znakem kreslí i starý tisk. „Pokračuje" zůstává
textem, dokud se nenajde sestava, kde se vyskytuje.

⚠️ Tištěný vzor navíc opravil naše tvrzení, že starý modul **přepisuje čas značkou**. Netiskne -
tiskne obojí; naše porovnání z buňky čte jen první položku, proto to tak vypadalo. Čas jsme tedy
neztráceli.

Ověřeno: .NET 1139/1139, Caché 127/127. Vizuální kontrola zbývá.

## 2026-08-24 (8) - 🔣 značky platnosti se kreslí piktogramy (`L4`, základ)

Linková tabulka nekreslila ani jeden symbol - značky se vypisovaly jako holá písmena. Nově jde
**řádek platnosti** přes symbolový font, takže pracovní dny jsou zkřížená kladiva, sobota a neděle
kroužkové číslice a svátky kříž, jak se to v jízdních řádech tiskne.

Vzniklá **sdílená mapa značek** je jediný zdroj pravdy pro zastávkové i linkové JŘ. Zastávkovým se
chování **nezměnilo** (podmínka Jana) - jen delegují na mapu; hlídají to jejich testy.

⚠️ **Linkový JŘ má opačné výchozí pravidlo než vývěska: piktogram jen z ověřeného seznamu, zbytek
textem.** Důvod je konkrétní: „pokračuje" má v číselníku značku `p`, ale na pozici `p` je ve fontu
**kufr**. Naivní „jednoznakové = piktogram" by cestujícímu nakreslilo zavazadlo.

⚠️ **Textem proto zatím zůstávají** značky pro projíždí, jede jinudy, vyčká, pokračuje a navazuje -
u prvních dvou je to typografická konvence, u zbytku **není pozice ve fontu změřená**. Doplnit je
smí až někdo, kdo je ověří v obou fontech (CZ i SK se u dvou pozic liší významem). Vypadají tedy
jako dosud, ne hůř.

⚠️ Loď, wifi a zásuvka v tom fontu **nejsou vůbec** - vypisují se textem, provizorně.

⚠️ **Piktogram a zkratka se míchají uvnitř jedné buňky.** První verze přepínala font „buď celá
buňka, nebo nic" a v jednom řádku pak stálo u jednoho spoje kladivo a u vedlejšího písmeno „X",
protože ten měl v buňce navíc „BEV" (nález Jan při vizuální kontrole). Buňka umí jen jeden font,
takže se smíšený obsah kreslí ručně nad hotovým řádkem.

Ověřeno: .NET 1139/1139, Caché 127/127.

## 2026-08-24 (7) - 🏢 pruh s dopravcem se skládá jako ve starém tisku (`L2` hotová)

Poslední bod vlny. U dopravce s provozem bez ulice jsme tiskli **„…,,tel:386100111"** - dvojčárku
a jinou zkratku, než má starý tisk („…, tel. 386100111"). Nově se části lepí stejně: přes čárku
s mezerou, prázdná se přeskočí, telefon má „tel. " a přibyl i **fax**, který jsme neposílali vůbec.

⚠️ **Odhalilo to porovnání hlavičky, které se do dneška nedělalo vůbec** - síť kontrolovala tabulku
a poznámky, ale ne hlavičková pole. Text dopravce se proto teď porovnává taky; na všech šesti
kontrolních linkách sedí.

⚠️ **Tři nastavení, kvůli kterým byl bod zadaný, jsou slepá ulička** a je to zapsané, ať se
nehledají znovu: pořadí dopravců a souhrn linek k dopravci čte HTML a PostScript tisk, **ne ten
engine, se kterým se porovnáváme**, a tisk provozů nemá zapnutý nikdo. Šlo tedy o skládání textu,
ne o nastavení.

Ověřeno: Caché 127/127, .NET 1121/1121. Odtisky tří linek přebaselinovány (počty shodné).

## 2026-08-24 (6) - 🅰️ přípojný řádek jde odlišit vlastním písmem (`L2`, #8360)

Přípoje se kreslily stejným písmem jako časy spojů, takže v tabulce křičely stejně jako odjezdy.
Nastavení prvku má nově skupinu **Přípojné řádky**: velikost, tučné, kurzíva a barva textu.
Nevyplněné se dědí z datových buněk, takže se beze změny nic nehne.

Menší písmo zároveň **zmenší výšku toho řádku** - přípoje tak zaberou míň místa, o což v požadavku
šlo. ⚠️ Výška se počítá na dvou místech (kreslení a výpočet výšky tabulky) a obě musí dát totéž,
proto chodí jedním výpočtem.

Požadavek KU HK (#8360).

Ověřeno: .NET 1121/1121, Caché 127/127.

## 2026-08-24 (5) - 🐛 přípojné řádky se tiskly dvakrát

Na lince s rozdvojenými zastávkami (příjezd zvlášť, odjezd zvlášť) se celý blok přípojů vypsal
**dvakrát** - jednou nad zastávkou a jednou pod ní. Přípoje se totiž párují na **tarifní číslo**,
a obě půlky rozdvojené zastávky mají totéž číslo.

Nově patří **odjezdovému** řádku, protože „navazuje" i „vyčká" se vztahují k odjezdu. Zastávka,
která má jen příjezdový řádek (konečná), si přípoje nechává, aby nezmizely.

⚠️ Opraveno i ve výpočtu výšky tabulky, ne jen v kreslení - kdyby se ty dva rozešly, tabulka by
měla jinou výšku než obsah.

Nahlásil Jan 24. 8. na `CB_CSADAD` 330511, hned po zpřístupnění agendy.

Ověřeno: .NET 1121/1121 a vizuálně na té lince.

## 2026-08-24 (4) - 🐛 výběr linek u linkového JŘ byl prázdný

Po zveřejnění agendy se ukázalo, že **výběr linek nic nenabízí**. Server přitom 200 linek posílal -
jenže bez čísla a názvu. Seznam je vypisuje jako sloupce, takže se otevřel prázdný.

Opraveno u obou linkových agend (městská měla tutéž chybu, jen ji nikdo neviděl, protože je skrytá).
Nově se posílají stejná pole a ve stejném tvaru jako u vývěsky.

Přibyl test, který u každé agendy kontroluje **tvar** odpovědi výběru instancí. Tahle chyba se
z dat nepoznala: ze serveru chodily řádky, jen prázdné, takže testy počtu by prošly.

Ověřeno: Caché 127/127.

## 2026-08-24 (3) - 🔓 linkový JŘ (AD) jde v návrháři vybrat

Agenda se dosud v nabídce vůbec neukazovala, takže na ni nešlo překliknout a linkový tisk nebylo
jak vyzkoušet. Nově je **zveřejněná** a nabízí se v „Změnit agendu" i ve filtru šablon.

⚠️ **Městská varianta zůstává skrytá** - neprošla porovnáním se starým modulem.
⚠️ Zveřejněná agenda se ukáže i zákazníkům, jakmile se vydá nová verze.

Návrhář si pamatuje **naposledy použitou agendu**, takže po prvním přepnutí startuje rovnou v ní.
Na žádost Jana je linkový JŘ navíc **výchozí i pro první spuštění** (dřív zastávkový) - uplatní se
jen tehdy, když uživatel ještě nikdy agendu nepřepnul. ⚠️ Než se to vydá zákazníkům, zvážit vrácení
zpátky: nový uživatel dnes přistane v pilotní agendě, ne v hotové vývěskové.

## 2026-08-24 (2) - 🔦 zvýraznění zastávky, ze které se uživatel dívá (`L2`)

Starý tisk umí zvýraznit řádek zvolené zastávky a zúžit to na konkrétní tarifní číslo; návrhář
z toho neuměl nic. Nově se dá v nastavení sestavy zadat **kus názvu zastávky** (nerozlišují se velká
a malá písmena) a volitelně **tarifní číslo**, které výběr zúží.

Data řádek jen **označí**, jak vypadá zvýraznění si řídí šablona - prvek už podmíněné zvýraznění
řádku uměl, takže stačilo dodat příznak. Pole se jmenuje stejně jako u vývěsky, aby stejné
nastavení šablony fungovalo v obou.

⚠️ Zastávka se nezadává vnitřním číslem jako ve starém modulu (uživatel ho nezná a v panelu není
odkud vybírat). Projíždí-li linka jedno město víckrát, označí se všechny jeho zastávky - přesně to
starý modul dělá výběrem zastávky. Tarifní číslo pak vybere jednu z nich, ale **v každém směru
jednu**, protože tarifní číslo je pro oba směry stejné.

**Nález, který mění zadání zbytku vlny:** přepínač vedený jako „tisk čísla linky v přípojném řádku"
ve skutečnosti řídí něco jiného - jestli se přípoje na různé linky u téže zastávky slijí do jednoho
řádku. Číslo linky se tiskne tak jako tak. Je to tedy nová logika, ne doplnění volby; zapsáno
do roadmapy k rozhodnutí o rozsahu.

Ověřeno: Caché 126/126, .NET 1121/1121. Odtisky přebaselinovány kvůli novému poli.

## 2026-08-24 - 🚏 názvy zastávek v linkovém JŘ jako ve starém tisku (`L2b`)

Poslední datový rozdíl proti starému modulu. Táž zastávka se dosud jmenovala na vývěsce jinak než
v linkovém jízdním řádu, protože jsme název **zkracovali** a **vždycky** brali upravený název
pro MHD. Starý modul to má jinak a teď to má i návrhář:

- **ve sloupci zastávek plný tvar** včetně obce („Český Krumlov,,aut. nádraží"),
- **ve větách poznámek zkrácený** („vyčká v zastávce Strakonice,aut. nádraží"),
- **upravený název pro MHD jen na městských linkách, které ho mají zapnuté** - tak to dělá starý
  tisk. Dosud jsme ho brali všude, což byla polovina rozdílu.

Do voleb u tisku přibyly **Tvar názvu zastávky** a **Použít název pro MHD** - stejné, jaké má
vývěska. ⚠️ Výchozí tvar je ale **plný**, kdežto vývěska má zkrácený: ty dva staré moduly se lišily
a tady je přednější parita - **potvrzeno Janem 24. 8.**: plný tvar je standard. Kdyby to někdo
chtěl jinak, řešilo by se to nastavením šablony, ne jinou výchozí hodnotou.

Formátování názvu je nově **sdílené oběma rodinami agend** - kdyby si ho každá dělala po svém,
zase by se rozešly. Vývěsce se chování nezměnilo.

**Porovnávací sada rozšířena o dvě linky** se značkami, které se na původní čtyřce nevyskytovaly,
a hned to našlo dvě věci:

- **U vlakového přípoje se tisklo „vyčká příjezdu vlaku 0"** místo označení vlaku („R 627").
  Označení se převádělo na číslo, což u nečíselného označení dá nulu. Opraveno.
- **Jeden spoj patří podle starého modulu do tří skupin dnů, u nás do dvou** - v tabulce se
  opakuje jednou méně, takže chybí sloupec. Vedeno jako dluh do vlny, která řeší definici sloupců.

Názvy zastávek proti starému modulu sedí **100 % na třech linkách ze čtyř**; na čtvrté zbývá
32 buněk, kde má starý modul místo jména značku a zatím není jasné proč (vedeno jako dluh, ne jako
parita). Tři značky (`SAO`, `CBE`, `zasu`) nemá v datech nikdo ze šesti firem, takže jejich věty
zůstávají opsané ze zdroje a neověřené.

Ověřeno: Caché 125/125, .NET 1121/1121. Zmrazené odtisky všech čtyř linek přepsány - počty shodné,
změnila se jen jména.

## 2026-08-21 (8) - 🏷️ dvě značky v legendě dostávaly špatný tvar věty

Dořešení včerejšího nálezu. Nastavení „značka atributu nad spoj" neřídí, **jestli** se řádek legendy
vytiskne, ale **jakým tvarem** - buď obecně („spoje s bezbariérově přístupným vozidlem"), nebo
s výčtem („spoj 19 přepravuje…"). Měli jsme to jako „tiskni / netiskni", což u bezbariérového
vozidla vyšlo náhodou správně, ale u dvou značek ne:

- **zásilky za zvláštních podmínek** a **spoj v systému integrované dopravy** mají ve starém tisku
  **vždy výčet spojů** a obecnou větu nikdy. Týká se to konkrétních spojů, ne celé linky, takže to
  tak i dává smysl.

⚠️ **Opsané ze zdroje starého modulu, ale porovnáním neověřené** - na žádné ze čtyř kontrolních linek
se ty dvě značky nevyskytly. Až se objeví linka, která je má, porovnání to potvrdí nebo vyvrátí.

Ověřeno: Caché 125/125 (odtisky se nehnuly, kontrolní linky ty značky nemají).

## 2026-08-21 (7) - 🔢 pořadí spojů se skládá jako ve starém tisku (`L2` krok 4, poslední dluh)

Porovnání se starým modulem hlásilo na dvou linkách jinak seřazené sloupce spojů. Hledání příčiny
odhalilo **čtyři různé odchylky** - a jednu z nich zásadní.

**Ruční pořadí spojů u linky jsme ignorovali úplně.** Uživatel si smí sloupce spojů přeskládat
a starý modul to respektuje; my jsme si pořadí vždy počítali sami. **Nastavené to je na 4 409
linkách** ze šesti měřených firem, takže to nebyla okrajová věc. Nově se ruční pořadí použije -
a stejně jako ve starém modulu se **zahodí celé, jakmile nesedí se skutečnými spoji** (typicky
po přidání spoje); pak se pořadí spočítá.

Tři menší, ale plošné odchylky ve výpočtu pořadí:

- **Tarifními čísly se u směru „tam" jde od konce**, ne od začátku. Měli jsme to obráceně.
- **Pro řazení se bere příjezd, ne odjezd.** U zastávky s pobytem jsme přičítali čekání - pobyt
  v trase má 38 655 měřených linek.
- **Trasa vedená přes víc záznamů** se pro řazení skládá dohromady (3 linky).

Pořadí spojů teď proti starému modulu sedí **na všech čtyřech kontrolních linkách do jednoho spoje**
(7/7), takže poslední dluh z porovnání je splacený.

⚠️ **Přepsány zmrazené odtisky dvou linek**: počty stránek, zastávek, spojů, časů i poznámek zůstaly
shodné, změnilo se jen pořadí sloupců.

Do backlogu (`B98`) šly tři přepínače řazení, které nemá zapnuté žádná ze šesti firem.

Ověřeno: Caché 125/125, .NET 1121/1121.

## 2026-08-21 (6) - 🏷️ legenda atributů spojů se chová jako starý tisk (`L2` krok 3)

Věty v legendě u atributů spoje („spoj s bezbariérově přístupným vozidlem", „spoje 1, 2 přepravují
jízdní kola") jsme měli opsané po svém. Nově se skládají ze **šablon opsaných ze starého modulu**,
a v jeho pořadí. Porovnání se starým tiskem je díky tomu u poznámek na třech ze čtyř kontrolních
linek **beze zbytku** (43/43, 18/18, 20/20).

**Porovnání přitom našlo dvě chyby, o kterých jsme nevěděli:**

- **Legendu o bezbariérovém vozidle jsme tiskli i tam, kde ji starý modul netiskne.** Čtyři atributy
  (bezbariérové vozidlo, kola, samoobslužné odbavení, nízkokapacitní autobus) se tisknou jen podle
  nastavení „značka atributu nad spoj" a u bezbariérového to navíc **linka přebíjí**.
- **Věta má čtyři tvary, ne jeden** - podle toho, jestli atribut nese jeden spoj, několik, nebo
  všechny, a jestli je zapnutá ta značka nad spojem. My posílali vždy generickou větu.

⚠️ **Rozhodnutí „texty brát z číselníku EDISONu" bylo postavené na omylu** a tímto se ruší.
V číselníku ty texty nejsou - starý modul je má zadrátované ve zdroji, zvlášť česky a slovensky,
takže si je zákazník nemůže upravit ani tam. **Slovenská sada je teď opsaná taky**, čímž je vyřešená
stížnost z helpdesku na kostrbatou slovenštinu - jen jinou cestou, než jsme mysleli.

⚠️ **Přepsány zmrazené odtisky tří linek** (rozhodnutí Jan): počty stránek, zastávek, spojů, časů
i poznámek zůstaly do jednoho shodné, změnil se jen text a pořadí řádků legendy. Odtisk se hnul
proto, že jsme se starému modulu **přiblížili**.

Ověřeno: Caché 124/124, .NET 1121/1121.

## 2026-08-21 (5) - 🔢 z označení zóny se dají odstranit písmena (`L2` krok 2)

Zóna může být v datech označená i písmenem (`51A`) a nastavení říká, jestli se má v tisku vypsat
celá, nebo jen číslo. Dosud jsme to nastavení nečetli, takže zákazník viděl písmena i tam, kde je
nechtěl.

⚠️ **Nastavení je globální, ale linka ho přebíjí** - a ukládá se u ní posunuté o jedničku, takže
„1" u linky znamená *neodstraňovat* a „2" *odstraňovat*. Kdo to bude čtít jako obyčejný přepínač,
udělá to naopak.

Ověřeno v obou směrech na reálných linkách: na lince s odstraňováním v zóně po tisku písmeno není,
na lince bez odstraňování zůstává.

Ověřeno: Caché 124/124, .NET 1121/1121.

## 2026-08-21 (4) - 🎨 barvy spojů se konečně tisknou (`L2` krok 5)

Spoj může mít v jízdních řádech nastavenou barvu a my ji v linkovém JŘ **vůbec neposílali** - časy
se tiskly černě, i když si zákazník spoje obarvil. Nově barvu nese datový kontrakt a prvek ji
použije na časy toho spoje.

**Vzniklo to z nedorozumění, které stálo za změření:** vypadalo to, že barvu u spoje zadat nejde
(barva se přidávala u linky). Měření ale našlo **3 926 obarvených spojů v deseti různých barvách**,
takže to nikdo neklikl omylem. Je to necelé procento spojů, ale na lince, kde se to používá, o tu
informaci tisk přicházel celou.

K tomu volba **„Barvy spojů"** (*Automaticky (dle linky)* / *Tisknout* / *Netisknout*) - linka umí
barvy potlačit a uživatel to smí přebít. `0;0;0` se posílá jako „nenastaveno", ne jako černá, jinak
by nešlo poznat obarvený spoj od neobarveného.

**Matice nastavení se přitom naučila novou věc.** Tvrzení „barvy posíláme" nešlo opřít o nastavení
u linky - „žádné barvy" vyjde i na lince, jejíž spoje barvu nemají. Matice proto umí **předpoklad
nad zdrojovými daty**: najdi linku, u které spoje barvu skutečně mají, a teprve na ní tvrď. Je to
odolnější než pevně zadaná linka, protože čísla linek se v jednotlivých skupinách opakují a zákazník
je může upravit.

Ověřeno: Caché 124/124, .NET 1121/1121.

## 2026-08-21 (3) - 🚫 zastávky, kde nikdo nestaví, se dají vyhodit (`L2` krok 6)

Na **362 linkách** má zákazník nastavené, že se do tabulky nemají dostat zastávky, na kterých žádný
spoj linky nestaví - jen je projíždí nebo jedou jinudy. Dosud jsme to ignorovali, takže tabulka byla
delší než ve starém tisku a byly v ní řádky, které tam zákazník nechtěl.

Nově se to bere, a jako u sloupce tarifních čísel je k tomu volba **„Zastávky, kde žádný spoj
nestaví"** se stavy *Automaticky (dle linky)* / *Tisknout* / *Netisknout*.

Ověřeno na lince, kde to zákazník zapnuté má: před opravou tam takové řádky byly, teď nejsou.

Ověřeno: Caché 124/124, .NET 1121/1121, zlaté odtisky bez pohybu.

⚠️ Nový parametr sestavy zase musel do allowlistu server-side parametrů; chytil to týž drift test
jako u předchozí volby. Je to spolehlivá past a spolehlivá pojistka.

## 2026-08-21 (2) - 🔀 sloupec tarifních čísel: uživatel může přebít nastavení linky (`L2` krok 4)

**Nastavení u linky se respektuje, ale nemá poslední slovo.** Na 377 linkách má zákazník nastavené,
že se sloupec tarifních čísel netiskne - dosud jsme to ignorovali. Nově se to bere, a zároveň je
v panelu parametrů volba **„Sloupec tarifních čísel"** se třemi stavy: *Automaticky (dle linky)*,
*Tisknout*, *Netisknout*.

Výchozí je „Automaticky", takže bez zásahu uživatele platí to, co je u linky. Popisek je zavedený,
ne nový - stejný tvar má u vývěsek volba „Rozdělení sloupců (typy provozu)".

**Proč není výchozí hodnota přímo hodnota z linky:** parametry se ze serveru berou bez znalosti
vybrané linky, takže default podle linky by znamenal přenačítání při každé změně linky a souboj
s hodnotou, kterou má uživatel uloženou. Takhle rozhodne linku **server při tisku**, kde ji zná,
a v panelu je poctivě vidět, že to řídí linka.

**V prvku k tomu platí nové pravidlo: prázdný sloupec se netiskne.** Když server hodnoty nepošle,
sloupec zmizí sám - o nastavení tedy nemusí vědět ani šablona, ani prvek. Dosud to platilo jen
pro kilometry a sloupec MHD, teď pro všechny popisové sloupce, stejně jako to dělá starý modul.
Vedlejší efekt, který je správně: u linky bez zón zmizí i sloupec zón.

Ověřeno: Caché 124/124, .NET 1121/1121. Zlaté odtisky se nehnuly, takže se nezměnilo nic na
linkách, kde tohle nastavení není.

⚠️ Nový parametr sestavy musel jít do allowlistu server-side parametrů - jinak by jeho změna v UI
data znovu nenačetla a klik by nic neudělal. Chytil to drift test, ne člověk.

## 2026-08-21 - 🗺️ linkový JŘ: zóny a kilometry podle nastavení linky (`L2` kroky 1-2)

**Krok 1: změřeno, co je vůbec nastavené.** Nová sonda projde všechny linky dostupných firem
a spočítá, které přepínače tisku má někdo reálně zapnuté - **93 133 linek v šesti firmách**. Bez
toho čísla by u nastavení uložených v datech platilo „ověřili jsme, co bylo v datech" a nikdo by
nevěděl, co zůstalo nepokryté.

Výsledek zúžil `L2`: **sedm nastavení má v datech oba stavy** a jde je ověřit hned, zatímco šest
dalších a **celá vrstva pod linkou nejsou nastavené nikde**. Ty jdou do backlogu - řada z nich
vznikla v legacy pro jednoho zákazníka a nikdo jiný je nepoužívá, takže dělat je naslepo a bez
možnosti ověření nemá hodnotu.

**Krok 2: opraven čtyřstavový přepínač zón a kilometrů.** Nastavení u linky říká jednu ze čtyř věcí:
jen kilometry · jen zóna · obojí · ani jedno. Čtli jsme z něj jen „něco je vyplněno, tiskni zónu",
takže na **1 126 linkách**, kde má zákazník nastavené „ani jedno", jsme zónu tiskli - a kilometrové
sloupce jsme neřídili vůbec. Nově se tiskne přesně to, co linka říká, a prázdný sloupec se v tabulce
neukáže.

Ověřeno na reálných datech ve všech čtyřech stavech (čtvrtý stav se v datech nevyskytuje a test to
**hlásí jako neověřený**, netváří se to jako pokryté).

**Cestou se našla mezera v samotné síti proti starému modulu.** Porovnání šlo přes hodnoty, které má
**starý modul**, takže hodnotu, kterou pošleme **navíc**, vůbec nevidělo - sloupec, který u starého
tisku není, by prošel jako zelený. Teď se porovnává sjednocení obou stran; na kontrolní sadě to
rozšířilo pokrytí o zóny a sloupec MHD, a nic se nerozbilo.

Ověřeno: Caché 123/123, .NET 1121/1121.

## 2026-08-19 (10) - 🕸️ linkový JŘ se poprvé měří proti starému modulu

Linkové agendy neměly do dneška **ani jeden test** (2 000 řádků kódu) a porovnání se starým modulem
existovalo jen jako pomocný nástroj mimo repo, takže ho nikdo nespustil. Vlna `L1` to staví do repa.

**Co vzniklo.** Porovnání volá starý engine (`mkPLinWin`) i naši agendu nad toutéž linkou a srovnává
je **pole po poli**: pořadí spojů, názvy zastávek, kilometry, zóny, sloupec MHD, časy v buňkách včetně
značek a legendu poznámek. Běží na čtyřech linkách tří firem, dohromady **~9 500 porovnaných hodnot**.
K tomu zlatý odtisk pro linkový tvar dat a smoke test obou agend (AD i MHD).

**Síť nezabetonovává starý modul.** Rozdíl může být trojího druhu a každý znamená něco jiného:
*odchylka* (chceme se lišit, s důvodem - třeba proto, aby šla návaznost v šabloně stylovat),
*dluh* (nechceme se lišit, ale zatím se lišíme; má přiřazenou vlnu, ve které zmizí) a *rozdíl*
(o kterém nikdo nerozhodl) - a jen ten je pád. Bez toho rozdělení by každé vylepšení podle
požadavků zákazníků začínalo bojem s vlastními testy.

**První běh rovnou našel dvě chyby:**

- **Navazující spoj se v legendě označoval značkou pro vyčkání.** Značku jsme brali z dat, kde je
  u obou druhů návaznosti stejná; význam ji přitom určuje jednoznačně. **Opraveno** - navazuje má
  nově vlastní značku, jak to má i starý tisk.
- **Pořadí sloupců spojů se u některých linek liší od starého tisku** (dvě dvojice na lince
  Karlovarského dopravního podniku). Netýká se to obsahu - časy sedí do jednoho - ale uživatel
  uvidí sloupce v jiném pořadí. Zavedeno jako dluh do další vlny; dřívější vysvětlení „jsou to jen
  spoje se shodným časem" **neplatí**, ty dva spoje vyjíždějí z různých zastávek.

Cestou se ukázalo, že číselník 66 dopravních značek je opsatelný přímo ze zdroje starého modulu -
pro chystaný symbolový font tedy nemusí vznikat měřením.

Ověřeno: Caché 122/122, .NET 1121/1121, build 0 chyb. Test si sám hlídá, že něco reálně změřil,
takže zelená neznamená „přeskočeno".

## 2026-08-19 (9) - 🧹 import: pryč cesta, která slévala grafiku do obrázku

Slévání čar do jednoho obrázku na pozadí se 18. 8. přestalo používat, ale kód pro něj v importu
zůstal - včetně kreslení rastru a tří rozhodovacích větví. Nikdo ho nezapínal; byl to jen kus, který
by příště někdo četl s dojmem, že se používá (analýza stavu 19. 8.).

Odstraněn celý režim slučování grafiky: `PdfToTemplateConverter` **620 → 405 řádků**, z dialogu
importu zmizela mrtvá volba. Chování se nemění - každá čára, obdélník i obrázek předlohy je vlastní
prvek šablony, čáry a obdélníky se automaticky seskupí.

Zrušený test hlídal, že je *výchozí* volba „neslévat". Záruka je teď silnější: taková cesta v kódu
neexistuje.

Ověřeno: build 0 chyb, .NET 1121/1121.

## 2026-08-19 (8) - 🕒 „změněno" u systémových šablon konečně znamená změnu

V přehledu šablon měly **všechny systémové šablony čas změny „před dvěma minutami"**, i když se
jich nikdo nedotkl (nález Jan 19. 8.). Nebyla to náhoda: seed z programu je **přepisoval při každém
spuštění**, takže sloupec ukazoval čas posledního startu, ne změnu obsahu - a nešlo tedy poznat,
co se doopravdy měnilo.

Nově se zapisuje **jen když se liší obsah**. Do porovnání se nepočítají časy uložení ani verze
formátu, protože to jsou razítka, ne obsah.

Cestou se ukázalo, proč to nešlo udělat naivně: **`Serialize` při každém volání přepíše čas změny
na „teď"**, takže porovnáním dvou serializací by rozdíl vycházel vždycky. Přidán tvar bez razítkování,
používaný jen k porovnání.

Ověřeno: build 0 chyb, .NET 1122/1122 (+4 na rozhodnutí „co je změna obsahu").

## 2026-08-19 (7) - 🔤 systémové šablony rozesílaly font „Arial," s čárkou

Při kontrole databáze po prvním stažení kanálem se ukázalo, že **naše vlastní systémové šablony**
mají na 13 místech `fontFamily: "Arial,"` - zbytek po chybě importu z předlohy, opravené 18. 8.
(šablony z ní vznikly). Každý, kdo si takovou šablonu otevřel, dostal hlášku o chybějícím fontu
a tisklo se náhradní písmo.

Opraveno v obou zastávkových šablonách (AD 7×, MHD 6×); linková byla čistá. Opravená AD šablona
je zároveň nahraná do kanálu jako verze 2 - posloužila jako první ostrá zkouška aktualizace.

Ověřeno: build 0 chyb, .NET 1118/1118.

## 2026-08-19 (6) - 🚚 kanál zapojen naživo: klíč, obsah odpovědi, skloňování

Zkouška s reálným klíčem (Jan 19. 8.) vytáhla tři vady, které by jinak vypadaly jako „kanál nic
nenabízí":

- **Klíč z lokálního nastavení se zahazoval.** Lokální konfigurace se slučuje **vyjmenovaně** (Caché
  a AI) a nový oddíl se do slučování nedostal - klíč byl v souboru, ale ne v aplikaci. Nová sekce
  konfigurace tam patří vždycky; hlídá to test.
- **Obsah šablony se v odpovědi jmenuje `data`**, ne `content` - a leží uvnitř obálky, která má taky
  `data`. Stahování by tiše vracelo prázdno.
- **Skloňování**: „K dispozici je 1 nových šablon" → nově „1 nová šablona / 3 nové šablony /
  7 nových šablon", se slovesem podle celku.

Navíc: klient kanálu dostává nastavení **předané**, ne ze statického stavu. Test „bez klíče nic
nevrací" jinak platil podle toho, jestli v procesu někdo načetl konfiguraci s klíčem - tedy podle
pořadí testů, ne podle kódu.

Ověřeno naživo proti helpdesku: nahrání šablony (HTTP 201), seznam i stažení vrací **obsah bajt
v bajt shodný** se souborem v repu. Build 0 chyb, .NET 1118/1118 (+13).

## 2026-08-19 (5) - 🚚 systémové šablony se doručují kanálem z helpdesku

Systémové (M-line) šablony byly zabalené v `.exe`, takže **nová šablona znamenala vydat program**.
Nově je doručuje kanál z úložiště na helpdesku a přehled šablon je nabídne ke stažení.

**Jak to vypadá pro uživatele:** v přehledu (Ctrl+O) se nad seznamem objeví řádek *„K dispozici je
N nových šablon od M-line"* s tlačítkem **Stáhnout** - jen když je opravdu co stáhnout. Stažení
doplní šablony **celé firmě naráz**; chovají se dál jako systémové (read-only, Ctrl+S z nich udělá
kopii do firmy) a vlastní upravené kopie zůstávají nedotčené, protože jsou to samostatné záznamy.
Není to modální otázka a nic neblokuje: bez sítě nebo bez klíče se nabídka prostě neukáže.

**Směr toku:** u zákazníka **jen čtení** (úložiště → klient). Nahrávat a měnit smíme jen my ve
vývoji, proto v aplikaci žádná cesta k zápisu není a klíč v nastavení je čtecí.

**Tři věci, na kterých to stojí:**

- **Identitu šablony určuje GUID uvnitř JSONu**, ne identifikátor v úložišti - podle něj se zapisuje,
  takže opakované stažení nic nezduplikuje. Kdo šablonu nahrává, musí vnitřní GUID zachovat.
- **Stav „co už máme" cestuje v šabloně**, takže patří databázi firmy, ne počítači uživatele - jinak
  by si každý uživatel stahoval totéž znovu.
- **Seed z `.exe` nepřepíše, co doručil kanál.** Dosud se MLINE větev přepisovala při každém startu;
  bez téhle pojistky by se stažená šablona po restartu tiše vrátila na verzi z programu.

Ověřeno: build 0 chyb, .NET 1105/1105 (+5). **Nevyzkoušeno naživo** - chybí klíč do úložiště; do té
doby je kanál mlčky vypnutý a aplikace se chová jako dřív.

## 2026-08-19 (4) - 🤖 AI průvodce dohnal změny: sloučené buňky, rohy, fonty, práce v tabulce

Průvodce tvrdil, že **neumíme sloučit buňky** (umíme od 17. 8.) a že **neumíme zaoblené rohy**
(má je dnes každý prvek, pole „Rohy" a „Velikost rohu" u rámečku). To je nejdražší druh chyby
v nápovědě: uživatel, kterému průvodce řekne „to neumíme", tu funkci přestane hledat.

Dál se doplnilo:

- **práce uvnitř pevné tabulky** - nejmíň objevitelná část aplikace (dovnitř Alt+klikem nebo F2,
  panel vlastností patří vybrané buňce, sloučení a vlastnosti řádku pod pravým tlačítkem, šířky
  tažením vnitřní hranice, `{` vyvolá nabídku polí). Průvodce o ní nevěděl vůbec.
- **fonty** - nabídka jsou rodiny nainstalované v počítači (ne „vestavěné"), font si uživatel může
  doinstalovat do Windows, a když šablona chce font, který na počítači není, aplikace to hlásí.
  Vložit soubor s fontem do šablony nejde - to zůstává mezi „zatím neumíme".
- **generátor šablon z popisu** je u zákazníka skrytý, průvodce ho přestal nabízet jako cestu.

Katalog vlastností prvků se generuje z prvků samotných, takže nedriftuje; hlídaný je nově i ručně
psaný úvod - ban list drží, že se do „zatím neumíme" nevrátí featura, kterou jsme dodělali.

Ověřeno: build 0 chyb, .NET 1100/1100 (+6).

## 2026-08-19 (3) - 📥 import se ptá jen na to, co jde z předlohy potvrdit (`B42c` dokončeno)

Otázka na vzhled prvků posílala modelu **všechny** jejich vlastnosti (přes sedmdesát) a spoléhala
na to, že si nejisté sám odpustí. Nově má každá vlastnost **u své definice** příznak, jestli se na
ni import smí ptát.

**Rozhodovací pravidlo: nepřítomnost nic nedokazuje.** Že na vývěsce nejsou značky přestupů, může
znamenat vypnutou volbu i linku bez přestupů - to z jedné předlohy nerozlišíš a špatný odhad se
vytiskne. Ptáme se proto na strany, počty a barvy (strana šipky, značky před názvem zastávky nebo
za ním, rozdělení seznamu do oddílů, počet sloupců legendy, střídání řádků, čáry), ne na zapnutí
featur závislých na datech.

**Rozměry a písma příznak nemají záměrně** - ty se z PDF měří přesně, takže ptát se na ně modelu
by výsledek jen zhoršilo.

Příznak sedí **u definice vlastnosti**, ne ve zvlášť vedeném seznamu: jinak by se to s prvky
rozešlo, což je přesně důvod, proč import zamrzl v červenci. Nová vlastnost prvku se tím rozhoduje
při vzniku a mlčky se do otázky nedostane.

Protokol (vývojářský) naopak ukazuje **všechny** odchylky včetně změřených hodnot - podle nich se
kontroluje, jestli import měří správně.

Ověřeno: build 0 chyb, .NET 1094/1094 (+8 na hranici „na tohle se ptáme × na tohle ne").

## 2026-08-19 (2) - 📥 nastavení sestavy z předlohy se uplatní, vzhled se čte u všech prvků JŘ (`B42c`/`B42d`)

**`B42d`:** co model z předlohy pozná o nastavení tisku (jestli jsou u zastávek šipky směru, v jakém
rozsahu hodin je mřížka), se dosud zapsalo **vedle místa, ze kterého se to při tisku čte** - vývěska
bez šipek se tedy stejně vytiskla se šipkami. Nově se hodnoty ukládají tam, kam patří, a otevření
importované šablony projde toutéž cestou jako otevření z databáze, takže se změna hned projeví
i v ovladačích nastavení (jinak by se tisklo něco jiného, než co je vidět).

Ukládá se **jen to, co model opravdu poznal** (rozhodnutí Jan 19. 8.). Hodnoty v šabloně mají přednost
před tím, co si uživatel nastavil pro agendu - kdyby import uložil celou sadu, začala by šablona
přebíjet nastavení uživatele (past z #12843). Volbu, kterou agenda nemá, import zahodí.

Otázka na volby se navíc **skládá z popisu agendy** (popisek, typ, nabídka, podmínka platnosti)
místo tří napevno vypsaných. Platí tedy i pro zastávkový JŘ z AD, který má voleb kolem patnácti -
dosud se ho import ptal na tři a psané byly pro MHD.

**`B42c`:** vzhled z předlohy se čte nejen u seznamu zastávek, ale i u **mřížky odjezdů a legendy
poznámek** - otázka i zápis se generují z prvku, takže to jsou další prvky bez další mechaniky.
Do zadání přibylo pravidlo: hodnotu, která je na obrázku **napsaná** (symbol v legendě, nadpis),
má model přečíst, ne dosadit podobný znak.

Ověřeno: build 0 chyb, .NET 1086/1086 (+4 na pravidlo „ukládej jen poznané volby").

## 2026-08-19 - 📥 po importu se nic nehlásí, protokol je nástroj pro vývoj

Hláška po importu („co se z předlohy nastavilo, co se nerozpoznalo") **skončila** - rozhodnutí Jan
19. 8. Stála na předpokladu, že víme, co má být hotovo, jenže **import je rychlý start, ne dokončená
šablona**: aby taková hláška dávala smysl, musel by import rozlišit, co v šabloně chybí záměrně
a co omylem. Výpis by měl čtyřicet položek a uživateli by z něj nic použitelného nevyplynulo.

Podrobný protokol vedle PDF zůstává, ale **jen se zaškrtnutým Debugem** - stejně jako ostatní ladicí
soubory importu. Je to nástroj na porovnání dvou importů při vývoji, ne informace pro zákazníka;
navíc se tím přestane zakládat soubor vedle uživatelova PDF.

Chyby, které uživatel vědět **musí** (nepodařilo se zavolat AI, jízdní řád nelze rozpoznat), hlásí
dál chybová hláška - ty se nemění.

Ověřeno: build 0 chyb, .NET 1082/1082.

## 2026-08-18 (18) - 📥 rámeček pro logo jen v záhlaví, protokol jen o skutečných změnách

Rozpoznávání prázdných rámečků z předlohy vzalo i **box pod legendou poznámek** - v předloze
opravdu je, ale je to vyhrazená plocha rozvržení, ne místo pro logo (nález Jan 18. 8.).

Místo pro obrázek se proto hledá **jen v záhlaví sestavy**, tedy nad jízdním řádem; vedle něj a pod
ním bývají prázdné boxy součástí rozvržení. Cena je vědomá: logo v patičce import nevloží - lepší
než falešné rámečky v každé šabloně.

Druhá úprava: **do protokolu jdou jen skutečné změny.** Model často potvrdí i hodnotu, kterou prvek
stejně má („Rozdělit na více oddílů = 1"), a v soupisu to vypadalo, že se z předlohy přeneslo víc,
než kolik se doopravdy změnilo.

Ověřeno: build 0 chyb, .NET 1082/1082.

## 2026-08-18 (17) - 📥 místo pro logo z rámečku v předloze, ne z odhadu modelu

Po předchozí opravě obrázek z importu **zmizel úplně** - model ho v té odpovědi nehlásil (nález
Jan 18. 8.). Model logo vidí jen někdy a pokaždé jinde, takže se na něj u obrázků spolehnout nedá.

Změřením předlohy se ukázalo, že tam **rámeček doopravdy je**: obdélník s obrysem, bez výplně
a **bez jakéhokoli textu uvnitř**. To je v tiskových sestavách typické místo vyhrazené pro logo -
a má přesnou pozici i velikost, na rozdíl od odhadu. Import z něj proto udělá **prázdný obrázkový
prvek**; stačí nahrát soubor. Rámeček, ve kterém text je, se nebere (to je orámování hodnoty),
stejně jako rámeček uvnitř jízdněřádového prvku nebo přes už vložený obrázek.

Druhá oprava: **pokračovací řádek dlouhé poznámky** (zalomená legenda) spadl těsně pod legendu
a přebýval v šabloně jako samostatný text. K prvku se proto počítá i pás **4 mm pod jeho spodní
hranou** - hranice od modelu je odhad. Je menší než pás nad prvkem záměrně: pod jízdním řádem
bývají legitimní popisky (tarifní doložka), nad ním ne.

Ověřeno: build 0 chyb, .NET 1082/1082.

## 2026-08-18 (16) - 📥 rámeček pro logo se vkládá podle předlohy, ne do vymyšlené mezery

Uživatel si všiml, že **obrázek je po importu vždycky trochu posunutý**, zatímco texty sedí
(nález Jan 18. 8.). Nebyla to nepřesnost: rámeček neměl v předloze žádnou oporu. Import rozdělil
pruh nad jízdním řádem na **šest stejných sloupců**, zjistil, který je prázdný, a doprostřed něj
rámeček položil - pozice tedy vycházela ze šířky stránky, ne z papíru. V testu to bylo vidět
číselně: model logo hlásil na 168 mm, rámeček vznikl na 177 mm (začátek šestého sloupce).

Nově vzniká prázdný rámeček **jen tam, kde model obrázek opravdu vidí**, v jeho rozměrech; když
žádný nehlásí, nevznikne nic. Zároveň je to **prázdný obrázkový prvek**, ne šedý obdélník - stačí
do něj nahrát soubor, není potřeba ho mazat a vkládat obrázek znovu. Do protokolu se připíše
„na pozici X, Y je v předloze logo - vložen prázdný rámeček".

Obrázky nakreslené čarami (typicky logo dopravce) z PDF vytáhnout nejdou, proto rámeček zůstává
prázdný - to je záměr, ne chyba.

Ověřeno: build 0 chyb, .NET 1082/1082.

## 2026-08-18 (15) - 📥 import: obecné zásady měření a mapování místo záplat

Porovnání protokolu se skutečnými rozměry v předloze ukázalo dvě systematické odchylky. Obojí je
opravené jako **pravidlo, ne jako hodnota naladěná na jednu vývěsku** (požadavek Jan 18. 8.):

- **Výška řádku se měří mezi řádky, ne mezi slovy.** Slova jednoho řádku nemají v PDF stejnou
  svislou pozici (jiná velikost písma, dolní dotažnice) a každá odchylka platila za další řádek -
  vycházelo 2,8 mm tam, kde jsou řádky po 4,05 mm, takže by se tiskly natěsno. Slova se teď
  shluknou do řádků a hranice shluku se odvozuje z výšky písma, aby pravidlo sedělo na drobnou
  i velkou sestavu.
- **Písmem oblasti je to, kterým je vysázená většina znaků.** V pásu záhlaví bývá vedle nadpisu
  skupiny několik drobných popisků nad sloupci; těch je početně víc, takže prostřední hodnota
  vybrala nejmenší písmo (6,2 pt místo 11,3 pt) a nadpis se tiskl drobně. Váha podle počtu znaků
  odpovídá tomu, co je na papíře vidět; při shodě vyhrává větší písmo.

Do zadání pro model přibyly **obecné zásady mapování** (platí pro každou agendu i předlohu):
každé datové pole nejvýš jednou · radši žádné pole než špatné (prostý text uživatel přepíše, špatné
pole vytiskne cizí údaj a nikdo si toho nevšimne) · hlásit jen to, co je opravdu vidět · obsah uvnitř
jízdněřádových oblastí nehlásit jako samostatné texty · rozlišovat podle role, ne podle pořadí na
stránce. Vzniklo to z chyb, které se opakovaly napříč běhy - totéž pole přiřazené dvěma textům
a domýšlení obsahu podle toho, jak sestavy obvykle vypadají.

Ověřeno: build 0 chyb, .NET 1082/1082 (+4 na obě měřicí pravidla).

## 2026-08-18 (14) - 📥 import: tři vady, které ukázal protokol

První použití protokolu z předchozího zápisu (test Jan 18. 8.) rovnou vytáhlo tři konkrétní příčiny:

- **Název zastávky ležel jako samostatný text přes tabulku.** Doplňování zapomenutých textů se
  porovnávalo s oblastmi, které vyznačil model, jenže seznam zastávek se **potom ještě natahuje**
  dolů - text, který propadl vedle krátké oblasti, nakonec ležel uvnitř tabulky. Doplňování se
  proto dělá až **za natažením** a porovnává se se skutečnými rozměry prvků.
- **Záhlaví skupiny dnů vyšlo celé černé.** Barva pozadí záhlaví se bere z vyplněného obdélníku
  v předloze a **čára je v PDF taky vyplněný obdélník**, jen tenká. Nově se přeskakují obdélníky
  nižší než 1,5 mm a tmavé výplně.
- **Prázdné šedé čtverce místo obrázků.** Import vkládal zástupný rámeček tam, kde model hlásí
  logo nebo grafiku, ale v předloze žádný obrázek není (v testované vývěsce jich PDF nemá ani
  jeden). Rámeček se už nevkládá - místo něj je v protokolu řádek „Model hlásil obrázek (logo)
  na pozici …, ale v předloze tam žádný není - pokud tam patří, vložte ho ručně."

Ověřeno: build 0 chyb, .NET 1078/1078.

## 2026-08-18 (13) - 📥 protokol importu vedle PDF: co přesně se nastavilo

Hlášku po importu jde jen přečíst a zavřít, takže se nedala porovnat s předlohou ani s předchozím
během - a přitom právě to je jediná cesta, jak poznat, že se rozpoznávání zlepšilo (podnět Jan 18. 8.).

Nově vzniká vedle importovaného PDF soubor **`<název>.import-protokol.txt`** a hláška na něj odkazuje.
Je v něm víc než v hlášce: u **každého vzniklého prvku** soupis, čím se liší od čerstvě vloženého -
tedy i to, co import **změřil z PDF** (písma, výšky řádků, šířky sloupců, barvy), ne jen to, co
odpověděl model. Pozice a velikost se do soupisu nepočítají, ty jsou u každého importu jiné.

Ověřeno: build 0 chyb, .NET 1078/1078 (+4).

## 2026-08-18 (12) - 📥 import: záhlaví tabulky se nesmí rozpadnout na dvacet textů

Model vyznačil oblast jízdněřádového prvku **až od dat**, takže řádek s popisky sloupců („min.",
„Zastávky"), řádek se skupinami dnů a první dvě zastávky zůstaly venku - a doplňování zapomenutých
textů je vložilo jako samostatné textové prvky (nález Jan 18. 8.).

Řeší se to ze dvou stran, protože samotné doladění zadání pro model spolehlivé nebude:

- **v zadání pro model**: oblast prvku musí zahrnovat i své záhlaví a všechny řádky - u seznamu
  zastávek popisky sloupců a první i poslední zastávku, u mřížky řádek se skupinami dnů. Včetně
  důvodu: co zůstane venku, skončí jako samostatné texty.
- **v doplňování textů**: blok, který se vodorovně kryje s prvkem a leží uvnitř nebo v pásu 8 mm
  nad ním, k tomu prvku patří a nevkládá se. Zabere i tehdy, když model oblast vyznačí o kus níž.

Ten pás je vědomý kompromis: nadpis a řádek s dopravcem bývají výš, takže zůstanou, ale záhlaví
tabulky těsně nad daty se už nevloží.

Ověřeno: build 0 chyb, .NET 1074/1074.

## 2026-08-18 (11) - 📥 import: hromada samotných čísel patří do jízdního řádu, ne do textů

Doplňování textů, které model nezmíní (viz zápis (10)), vkládalo i **odjezdy, které se modelu nevešly
do vyznačené mřížky** - v šabloně pak pod tabulkou visely textové prvky „17", „18", „18 40"
(nález Jan 18. 8.).

Nově platí: **zbyde-li mimo vyznačené oblasti tři a víc samostatných čísel, žádné z nich se nevkládá.**
Je to obsah jízdního řádu a data si prvek při tisku načte sám. Jedno až dvě čísla se nechávají -
číslo linky je taky číslo a v šabloně chybět nesmí. V protokolu importu je o tom řádek.

Ověřeno: build 0 chyb, .NET 1074/1074. Ze stejného testu: model poprvé nastavil vzhled seznamu
zastávek podle předlohy (značky před zastávkou, šipka vlevo, čáry mezi řádky, symbol na znamení).

## 2026-08-18 (10) - 📥 import bez AI skončí hláškou, chybějící texty se doplní z předlohy

**Nález z testu (Jan 18. 8.):** import vývěsky KV_DP vynechal většinu textů, poznámky umístil jinam
a záhlaví skupin dnů vysypal jako volné texty. Příčina nebyla v rozpoznávání - **import vůbec neběžel
přes AI**. Klíč se bere nejdřív z lokálního nastavení vývojáře a až pak z mlwebu; ten lokální byl
neplatný (401), volání selhalo a import **tiše přepnul na náhradní rozpoznání bez modelu**. Hláška
o tom šla do stavového řádku dialogu, který se v tu chvíli zavírá, takže se to nikdo nedozvěděl.

Dvě opravy:

- **Jízdní řád se bez AI nedoimportuje.** Import skončí hláškou „Jízdní řád se nepodařilo rozpoznat -
  AI služba neodpověděla: …" a šablona nevznikne. Hrubá náhražka, která vypadá jako výsledek importu
  a přitom v ní chybí zastávky a odjezdy, je horší než žádná. U obecného importu (bez agendy jízdního
  řádu) náhradní cesta zůstává, jen se to napíše do protokolu.
- **Texty, které model nezmíní, doplní import sám.** Geometrii textu má z PDF přesnou (v testované
  vývěsce 97 textových bloků), takže co leží mimo jízdní řád a model to nevrátil, se vloží jako
  obyčejný text se správnou pozicí, písmem a řezem - typicky číslo linky, trasa nebo řádek s dopravcem.
  Doplněné texty jsou bez role a bez napojení na datové pole; význam určuje model, ne my.

Ověřeno: build 0 chyb, .NET 1074/1074. **Kvalita rozpoznání se tím neměří** - předchozí test jel bez
modelu, takže srovnání „dřív to bylo přesnější" zatím nemá s čím.

## 2026-08-18 (9) - 🔤 „Font Arial není nainstalovaný" po importu - byl to font s čárkou v názvu

Po importu z předlohy svítilo upozornění *Font „Arial" není na tomto počítači nainstalovaný -
tiskne se místo něj Arial*. Věta si protiřečila a Arial samozřejmě nainstalovaný je.

**Příčina:** PDF nese řez přímo v názvu fontu („Arial,Bold"). Import z názvu odloupl slovo „Bold",
ale **čárku v něm nechal**, takže do šablony zapsal rodinu **„Arial,"**. Tu systém nezná - a protože
se v hlášce zobrazuje v uvozovkách, vypadala jako obyčejný Arial. Šablona tím držela font, který
neměl nikdo, takže by se to samé stalo i kolegovi po otevření z databáze.

Nově se řez odloupne i s oddělovačem a opakovaně („Arial,BoldItalic" → Arial), takže z běžných
PDF vycházejí rodiny, které systém zná. Zpětně to nespraví už naimportované šablony - tam font
přenastavte, nebo šablonu naimportujte znovu.

Vedle toho **upozornění na chybějící font říká pravdu o náhradě**: jmenuje font, který se opravdu
použije, místo natvrdo napsaného Arialu, a když by náhrada byla týž font, nehlásí nic.

**Protokol importu** navíc nově vypisuje, kolik textů z předlohy se do šablony vložilo a kolik jich
model našel - bez toho čísla nešlo poznat, jestli texty nenašel model, nebo jsme je zahodili sami
(text uvnitř jízdního řádu si kreslí prvek sám, dekorace bez datového pole se nevkládá).

Ověřeno: build 0 chyb, .NET 1074/1074 (+16).

## 2026-08-18 (8) - 📥 import se ptá na vzhled seznamu zastávek a řekne, co nastavil (`B42b`/`B42c`, 1. kolo)

Import dosud uměl z předlohy jen to, co měl **ručně vypsané v otázce pro AI** - tři vlastnosti.
Proto zamrzl v červenci: každá nová vlastnost prvku by se musela dopsat na dvě místa v importu
a nikdo na to nemyslel.

Nově se otázka **skládá z prvku samotného** - z týchž vlastností, ze kterých se staví panel
vlastností, včetně povolených hodnot. Odpověď se zapisuje týmž způsobem. Import tedy nemá vlastní
seznam, který by se s prvky rozešel. **První kolo jde jen na seznam zastávek** (rozhodnutí Jan
18. 8.) - na něm se pozná, co model z obrázku opravdu zvládne: stranu šipky, značky před názvem
zastávky nebo za ním, rozdělení na oddíly vedle sebe, barvy, čáry.

Na co se model **neptáme**: pozice a velikost (ty se z PDF měří přesně) a podmínky viditelnosti
(to jsou data, ne vzhled).

**Po importu vyskočí protokol** - co se z předlohy nastavilo a co se nerozpoznalo. Dosud se tyhle
hlášky ztratily se zavřením dialogu, takže když AI něco vynechala, vypadalo to, že to import
neumí, a nebylo jak zjistit proč.

Nesmysl se do šablony nepustí: barva „tmavě modrá" ani počet oddílů 999 se nezapíšou, jen se
vypíšou v protokolu a zůstane výchozí hodnota. Model odpovídá volným textem, takže se u výběru
z nabídky bere i zkrácený tvar („Vlevo" místo „Vlevo (u levého okraje elementu)"), ale jen když
sedí na jedinou volbu.

Ověřeno: build 0 chyb, .NET 1058/1058 (+13). **Co model reálně pozná, ukáže až import reálné
vývěsky** - v protokolu je vidět, co nastavil, a stačí to porovnat s předlohou.

## 2026-08-18 (7) - 📥 import z předlohy už nedělá z čar zamčený obrázek

Čáry z předlohy (rámečky, linky tabulek) se při obecném importu slévaly do **jednoho obrázku na
pozadí** - zamčeného, neupravitelného a v tisku rozmazaného proti zbytku šablony, protože to byl
rastr 150 DPI. Obrázek má v šabloně vzniknout jen z toho, co je obrázek i v předloze (logo, QR kód).

Nově je z každé čáry čára a z každého obdélníku obdélník, se kterými jde normálně pracovat; čáry
a obdélníky se automaticky seskupí, aby se s nimi dalo hýbat vcelku (rozhodnutí Jan 18. 8.).

⚠️ **U složitější předlohy tak vznikne i pár desítek čar.** Uklidit to má rozpoznání tabulky z jejich
mřížky (roadmapa, sekce Import PDF) - slévání do rastru bylo obcházení téhož problému.
Kód pro slévání zůstává, ale žádná cesta ho už nezapíná.

## 2026-08-18 (6) - 📥 import z předlohy: prázdný sloupec, popisky a zahozená agenda (`B42a`)

Průchod importem PDF proti dnešnímu stavu návrháře. Co import z předlohy nastaví, se naposledy
měnilo v půlce července, zatímco prvky jízdního řádu i nastavení sestavy mezitím povyrostly.
Tahle dávka bere jen to, co je jasná vada a nepotřebuje rozhodnutí:

- **Prázdný sloupec „tarifní číslo"** - když AI v předloze poznala sloupec s tarifním číslem, import
  ho založil pod klíčem, který agendy nevydávají (nese jen interní klíč, ne reálné číslo). Sloupec
  zabíral šířku a při tisku zůstal prázdný. Nově se mapuje na **pořadí zastávky**, což je údaj, který
  poziční potřebu kryje.
- **Popisky sloupců** se rozešly s ručně vloženým seznamem zastávek („min.", „Zastávky", „Nást."
  proti „Čas", „Zastávka", „Označník"). Import je teď bere přímo z výchozích sloupců prvku, takže
  se rozejít nemůžou; „Nást." navíc odporovalo dohodnutému názvosloví.
- **Varianty jízdních dob** - importovaný seznam zastávek sloupec variant neměl, ručně vložený ano.
  U linky s jedinou variantou se nevykreslí, u linky s víc variantami je teď importovaná šablona
  zvládne stejně jako poskládaná ručně.
- **Obecný import zahazoval agendu** - import jiné než jízdněřádové předlohy vracel šablonu bez
  agendy, tedy s prázdným seznamem polí a nutností agendu ručně přiřadit. Nově šablona zdědí agendu,
  ve které import běžel.
- Z otázky na AI zmizel dotaz na šipku směru - **odpověď neměl kdo přečíst** (šipku řídí nastavení
  sestavy, ne vlastnost prvku).

Import dosud neměl **ani jeden test**; s touhle dávkou vzniklo prvních devět (sloupce a jejich
popisky). Ověřeno: build 0 chyb, .NET 1044/1044.

Co zůstává otevřené, drží roadmapa: rozpoznané **nastavení sestavy** se do šablony pořád nepromítne
(`B42c`) a po importu **není kde ukázat, co se nepovedlo rozpoznat** (`B42b`).

## 2026-08-18 (5) - 🔤 upozornění, když šablona chce font, který v počítači není (`B97`)

Když šablona používá písmo, které na daném počítači **není nainstalované**, program ho dosud tiše
nahradil Arialem a nikde to neřekl. Protože se šablony sdílejí přes databázi, mohla **táž šablona
u dvou lidí vytisknout jiné písmo** a nikdo to nepoznal - výstup vypadá v pořádku, jen není takový,
jaký ho autor navrhl.

Nově se to hlásí na dvou místech:

- **ve stavovém řádku návrháře** hned po otevření šablony - oranžové upozornění, které **jmenuje
  konkrétní fonty** a říká, co se místo nich vykreslí
- **v hlášce po uložení PDF** - tisknout může někdo, kdo šablonu jen otevřel z databáze a v návrháři
  ji neprohlížel

Hláška vypadá takto: *Font „Roboto Condensed" není na tomto počítači nainstalovaný - tiskne se místo
něj Arial.* Náprava je buď font doinstalovat, nebo v šabloně vybrat jiný; nabídka fontů vždy jmenuje
jen ty, které k dispozici jsou.

**Najde se i font schovaný hluboko** - ve stylu jednoho sloupce, jedné buňky nebo ve výchozím stylu
dokumentu, nejen v nastavení prvku.

## 2026-08-18 (4) - 🗓️ datum platnosti u MHD s tečkami, ne s lomítky (`B96`)

Zastávková vývěska MHD tiskla datum platnosti jako **`01/11/2018`**, zatímco linková doprava
vedle ní `11.12.2022`. Česky se datum píše s tečkami, takže to byla chyba - jen si jí nikdo
nevšiml, protože každá agenda se dívala jinam. Nově mají obojí tečky.

**Kde se to projeví:** pole „Datum platnosti od / do" a „Popis platnosti" na vyvěskách MHD.
Zákaznickým šablonám se nic nerozbije - mění se jen tvar hodnoty, ne název pole.

**Poznámka k jazykům:** tvar je zatím napevno český. Správně by šel z nastavení jazyka, ale
všichni zákazníci tisknou česky, takže by to dnes byla vrstva, kterou nikdo nepoužije;
formátování je na jednom místě, až bude potřeba, vymění se tam.

## 2026-08-18 (3) - ❓ nápověda F1 dohnala změny od 12. 8.

Nápověda **nevěděla o pevné tabulce vůbec** (ve výčtu prvků byla jen tabulka z dat), takže kdo si ji
vložil, neměl kde zjistit, že se do buňky chodí Alt+klikem. Nová sekce **„Pevná tabulka -
vyplňování"**: vstup do buňky, pohyb, výběr více buněk, Ctrl+B/I/U, vlastnosti buňky i řádku,
sloučení, šířky tažením, vkládání z Excelu a podmínka viditelnosti řádku.

Doplněné i tři letošní změny vývěsky, o kterých nápověda mlčela: **kde se definují sloupce**
(prvek odjezdů → Definice sloupců), že **písmeno varianty u minuty** říká, který sloupec časů pro
spoj platí (volba „První varianta bez písmene" v parametrech), že **rozvržení sloupců drží šablona**
a parametr je jen její pohled, a že v **seznamu zastávek jsou jen zastávky, kam z té vývěsky
opravdu něco jede**.

⚠️ **Tvrzení nápovědy jsou testovaná**, ne jen napsaná: pole vlastností buňky, řádku a rozměru
tabulky i oba parametry vývěsky musí ve schématech reálně existovat. Přejmenování by test shodilo -
přesně proto, aby nápověda neposílala uživatele na místo, kde nic není. Test u toho hned chytil
překlep: parametr se jmenuje „Rozdělení sloupců (typy provozu)", ne „Rozdělení sloupců".

**Brány:** build čistý, **2231 .NET testů** zelených (TD 1033, z toho 3 nové).

## 2026-08-18 (2) - 📐 rozměr pevné tabulky se zadává při vkládání

Tabulka se vkládala **natvrdo jako 3 × 3**, takže kdo chtěl 12 × 10, musel se proklikat devatenácti
vloženími sloupce a řádku (nález Jan 18. 8.). Vložení se nově ptá **rychlým formulářem** na počet
sloupců a řádků, předvyplněným 3 × 3 - kdo rozměr neřeší, jen potvrdí a dostane, co dostával dřív.
Platí pro obě cesty vložení (nabídka „Přidat" i tažení z palety); zrušení formuláře nevloží nic.

**Výška roste s počtem řádků** - u desetiřádkové tabulky by výchozích 18 mm znamenalo, že se obsah
hned po vložení zmenší, aby se vešel.

⚠️ **Bez rychlého okna, ne mřížkovým výběrem jako Word.** Formulář jde přes tutéž generickou cestu
jako ostatní (schéma na serveru + reflexe), takže nevznikla nová kontrolka ani nové okno; mřížkový
výběr by byl hezčí, ale je to nová vlastní komponenta kvůli dvěma číslům.

Řádky a sloupce jde pořád přidávat i později přes pravé tlačítko a **vložení bloku z Excelu tabulku
dorovná samo** - kdo má obsah v sešitu, dostane rovnou správný tvar i s daty.

**Brány:** build čistý, **2221 .NET testů** zelených (TD 1023, z toho 6 nových), Caché 110.
Formulář ověřen voláním nasazené třídy. Driftový test si vynutil popisek „Počet sloupců" - týž klíč
má stejný popisek i u legendy poznámek.

## 2026-08-18 - 👁 podmíněný řádek pevné tabulky jde konečně nastavit

Řádek pevné tabulky umí mít **podmínku viditelnosti** („vytiskni ho, jen když platí") - tisk ji
respektoval odjakživa, ale od zrušení pětizáložkového dialogu 12. 8. **nebylo odkud ji zadat**.
Stejná díra jako u formátování buňky, jen o řádek výš.

**Typický případ, kvůli kterému to má smysl:** buňky pevné tabulky nesou placeholdery, takže řádek
„Variabilní symbol: `{VS}`" nemá co dělat na dokladu, který variabilní symbol nemá - jinak zbyde
popisek s prázdnem za dvojtečkou. Prázdná hodnota je sama o sobě nepravda, takže jako podmínka
stačí napsat `{VS}`.

**Vlastnosti řádku** otevře místní nabídka řádku - kromě podmínky je v nich **barva pozadí řádku**,
která taky žila jen v modelu. Zbytek (výška tažením, styl textu v buňce) zůstává tam, kde byl.

⚠️ **Nic z toho není nová cesta.** Výraz se vyhodnocuje **toutéž funkcí** jako podmínka prvku,
sekce nebo řádků tabulky z dat; pole vzniká ze **sdíleného balíčku** base polí, takže má stejný
popisek i nápovědu jako u prvků (liší se jen slovo „řádek" místo „element"); formulář hostují titíž
dva hostitelé jako u buňky. Na plátně dostane podmíněný řádek **týž odznak**, jaký má podmíněný
prvek - jinak by v návrháři vypadal jako každý jiný a v tisku beze stopy zmizel.

⚠️ **Nález, který to odhalilo dřív, než se na to dalo kliknout:** podmínka se vyhodnocovala i **bez
dat**, takže řádek zmizel z plátna hned, jak mu uživatel podmínku dal - a nešel vybrat ani té
podmínky zbavit. Nově se bez dat nefiltruje: schová se až v náhledu a při tisku, kde je proti čemu
podmínku vyhodnotit.

**Brány:** build čistý, **2212 .NET testů** zelených (TD 1014, z toho 6 nových), Caché 110.
Formulář ověřen voláním nasazené třídy. Mutace „filtruj i bez dat" shodí dva testy.

## 2026-08-17 (9) - 🔗 pevná tabulka: sloučení buněk vodorovně (`B76` hotový)

Poslední kus bloku. Označené buňky v řádku se z místní nabídky **sloučí do jedné** - typicky
nadpis přes celou šířku tabulky. Sloučená buňka se kreslí jako jeden celek (uvnitř nezůstane
svislá čára) a **klik doprostřed vybere ji**, ne překrytou buňku, kterou uživatel na plátně
nevidí. „Rozdělit buňky" to vrátí zpět.

**Obsah se při sloučení spojí mezerou** (rozhodnutí Jan 17. 8.). Excel všechno kromě první buňky
zahodí, Word obsahy spojí - a protože se typicky slučuje na prázdné tabulce, je spojení hlavně
pojistka, aby se text nikdy neztratil bez varování. Rozdělení nechá obsah v první buňce; zpátky
rozdělit text nejde, nešlo by poznat kam.

**Výběr přes víc řádků slučuje po řádcích**, ne do jednoho bloku - sloučení je vodorovné,
takže dva řádky dají dva nadpisy pod sebou.

⚠️ **Sloučení přes řádky vědomě není** - míchá se se spanováním výšek, střídáním řádků
a podmíněnou viditelností; bez konkrétní poptávky to není za tu složitost.

**Aditivní v JSON:** buňka nese počet sloučených sloupců (výchozí 1), takže starší šablony
vypadají přesně jako dřív. Vratnost umí zpětný krok jedním kliknutím - snímek buňky nově nese
i obsah a sloučení, takže Ctrl+Z vrátí i spojený text zpátky do původních buněk.

**Brány:** build čistý, **2203 .NET testů** zelených (TD 1005, z toho 10 nových). Mutace ověřily,
že testy měří: zahození spojeného textu i kreslení po sloupcích místo sloučení své testy shodí.

## 2026-08-17 (8) - 🚆 výchozí linková šablona se doručí sama (`B92`)

Zastávkové výchozí šablony jsou zabalené v programu a při spuštění se samy nalijí do databáze, takže
je uživatel najde připravené. **Výchozí linková šablona ležela mimo tuhle cestu** - existovala jako
soubor ve vývojové složce, ale nikam se nedostala; uživatel by si ji musel naimportovat ze souboru,
o kterém neví. Nebyly to dva záměrné kanály, byl to nedokončený jeden. Nově jde stejnou cestou.

V nabídce se ukáže, **až bude linková agenda zveřejněná** - to je záměr, aby se distribuce
nedodělávala teprve s vydáním.

**Tři věci opravené při přesunu, které uživatel v nabídce vidí:**

- šablona se jmenovala **„Knižní JŘ (AD)"**, ačkoli se agenda jmenuje **Linkový JŘ**. Pojem „knižní"
  se přejmenoval už dřív a na šablonu se zapomnělo.
- v názvu byla **dlouhá pomlčka** místo běžné.
- **popis byl prázdný**, takže by šablona stála v nabídce bez vysvětlení, co to je.

**Aby se to nevrátilo:** nová pojistka hlídá, že každá systémová šablona je opravdu **zabalená
v programu** (soubor ve složce k tomu nestačí), má vyplněný název, popis i kód agendy, má vlastní
identifikátor a načte se v aktuální verzi. Kód agendy se porovnává s tím, co agendy reálně
deklarují - překlep by jinak znamenal šablonu, kterou nikdo nikdy nenajde.

## 2026-08-17 (7) - 🚆 linkový JŘ mluví stejným slovníkem jako vývěska (`B91`+`B95`)

Zastávková vývěska a linkový jízdní řád nabízely **pro tutéž věc jiná pole**. Nebylo to vidět:
skupiny polí se jmenovaly stejně (Zastávky, Skupiny dnů, Poznámky), takže sloupec s polem „Název
zastávky" vypadal, že bude fungovat v obou - a v linkovém JŘ zůstal tiše prázdný. Totéž u zón
a u data platnosti. Srovnáno; měnil se **výhradně linkový JŘ**, protože vývěska je vydaná.

**Dvě věci, které jsou horší než jiný název a nešly poznat z popisku:**

- **Číslo linky u MHD.** Vývěska tiskne pod „Číslo linky" licenční číslo, linkový JŘ tiskl veřejné.
  Táž šablona tedy u jedné linky vytiskla dvě různá čísla podle toho, který jízdní řád se dělal.
  Nově je to v obou licenční číslo a veřejné se nabízí zvlášť jako **„Veřejné označení linky"** -
  opět pod stejným názvem jako na vývěsce.
- **Datum platnosti u MHD.** Linkový JŘ posílal datum v syrovém tvaru z databáze (`2018-11-01`),
  vývěska v převedeném. Srovnáno na tentýž převod.

Obojí se týká **jen MHD**, pro kterou linková šablona zatím neexistuje - takže dnes nikomu nic
nezmění vzhled. Výchozí linková šablona pro AD vypadá po změně stejně jako předtím: AD posílá jediné
číslo linky a datum už dřív s tečkami.

**Co ještě zmizelo z nabídky:** pomocné spojovací klíče (ID skupiny dnů, číslo skupiny u spoje).
Zůstávají v datech, takže tabulka je dál používá - jen se nenabízejí k vložení, protože v ploché
tabulce s nimi uživatel nic neudělá. Stejně to má vývěska.

**Aby se to nevrátilo:** popisky a typy polí agend hlídá nová pojistka (`AgendaSchemaDriftTests`) -
tatáž věc musí mít ve všech agendách stejný název i popisek. Ověřená mutací, ne jen tím, že svítí
zeleně. Doteď hlídala jen formuláře vlastností prvku, ne slovník dat - proto rozchod vznikl.

⚠️ **Jedno tvrzení z revize neplatilo:** „dělení sloupců" se v obou JŘ jmenuje jinak **záměrně** -
jsou to dvě různá nastavení s jinými volbami („Dělení spojů dle platnosti" × „Rozdělení sloupců
(typy provozu)"). Sjednotit název nejde: ukládá se zobrazený text, takže by se uživateli při
přepnutí agendy tiše ztratilo nastavení.

## 2026-08-17 (6) - 🔲 pevná tabulka: výběr více buněk tažením (`B76` krok 3)

Formátovat, mazat a kopírovat šlo dosud jen buňku po buňce. Nově se **tažením myší označí
obdélníková oblast** (Shift+klik a Shift+šipky ji rozšíří od kotvy, Ctrl+A vybere celou tabulku).
Vybraná oblast je podbarvená a **aktivní buňka v ní zůstává rozpoznatelná** silným rámečkem: je to ta, do
které se píše, podle které se vkládají řádky a od které se počítá nová hodnota při formátování.

**Co nad výběrem funguje:** hromadné formátování (lišta, Ctrl+B/I/U i formulář v panelu), **Delete**
vymaže obsah celé oblasti (styl zůstává, na ten je „Resetovat styl") a **Ctrl+C/Ctrl+X** zkopíruje
oblast ve tvaru, který přečte Excel. **Ctrl+V** vloží blok od levého horního rohu výběru a tabulku
dorovná; **jedna hodnota do označené oblasti tu oblast vyplní** - jinak by se dala víc buněk
srovnat jen po jedné.

⚠️ **Výběr celého řádku a sloupce v tom vědomě není** (rozhodnutí Jan po vyzkoušení). Klikem
u okraje tabulky to nejde - tam sedí úchyty pro změnu velikosti prvku, takže by týž klik jednou
vybíral řádek a jindy táhl tabulkou. Excelové Shift/Ctrl+mezerník se zkusily a zase zrušily: tažení
a Shift pokrývají totéž a tuhle zkratku se uživatel nemá kde naučit.

⚠️ **Formulář zapisuje do aktivní buňky, ostatní se dorovnají jen v tom, co se změnilo.** Kdyby se
kopíroval celý styl, hromadná změna barvy by srovnala i písmo a uživatel by o svoje nastavení
přišel - hlídá to test, mutace „dorovnat všechno" ho shodí.

**Ctrl+A posune aktivní buňku na levý horní roh** - obdélník je daný dvěma rohy, takže aktivní
buňku uprostřed výběru držet nejde. Od začátku se ale stejně píše i čte.

**Brány:** build čistý, **2176 .NET testů** zelených (TD 978, z toho 18 nových).

**Zbývá v `B76`:** sloučení buněk vodorovně (krok 4).

## 2026-08-17 (5) - 📝 buňka se edituje standardním formulářem, Ctrl+B/I/U funguje (`B76`)

Dvě věci z proklikání (Jan 17. 8.): panel buňky měl vypadat jako každý jiný formulář v aplikaci
a chyběly zavedené zkratky.

**Vybraná buňka má vlastní formulář** (`TableCellEdit.cls`) - stejná stavba i popisky jako
u prvků, skupiny „Vybraná buňka" (formát hodnoty) a „Styl buňky" (písmo, řez, velikost, řezy textu,
zarovnání, barvy, vnitřní okraje, řádkování). **Jedno schéma, dva hostitelé:** panel i okno pod
Alt+Enter jedou na tomtéž, přesně jako u prvků - dvě různá by se nutně rozešla. Vlastní stylový
pod-formulář pro buňku tím padl, zůstává jen tam, kde byl (zóny mřížky odjezdů).

**Ctrl+B / Ctrl+I / Ctrl+U** ztuční, zkurzíví a podtrhne buňku, a to **i během psaní**. Uvnitř
tabulky přebíjejí globální význam (Ctrl+B jinak sbaluje levý panel) - v tabulce čeká uživatel
tučné a mimo tabulku zkratka funguje dál. Nová hodnota se počítá z aktivní buňky, ne pro každou
zvlášť, ať u budoucího výběru oblasti buňky nezůstanou prohozené.

⚠️ **Aby formulář nelhal ani nezapékal**, dostal binder EDInputFormu dvě věci:

- **hodnoty z kaskády místo výchozích ze schématu** (`ApplyInheritedValues`) - buňka je překryv,
  takže formulář musí ukázat, jak reálně vypadá, ale zapsat jen to, co uživatel opravdu změnil;
- **vymazané pole u zděditelné vlastnosti = „nenastaveno" (null), ne prázdný text.** Do dneška to
  platilo jen pro barvy. Vymazaný font tak nově znamená „vezmi z tabulky", ne „žádný font",
  a vymazaný formát hodnoty se vrátí k formátu sloupce.

Zápis z formuláře jde do historie **souřadnicí buňky**, ne odkazem na ni - stejně jako u lišty.

**Doklad:** formulář ověřen voláním nasazené třídy. Testy hlídají, že otevření a potvrzení beze
změny nic nezapíše, že se zapíše jen změněné pole, že vymazané pole vrátí dědění, že každé pole
schématu existuje jako vlastnost buňky a že **žádné stylové pole nezůstane bez hodnoty z kaskády**
(kdyby zůstalo, formulář by u něj ukázal výchozí hodnotu ze schématu a uživatel by ji potvrzením
zapekl). Formulář buňky je proto vědomě mimo `EDInputFormSchemaDriftTests` - výchozí hodnoty se
u něj nikdy nezobrazí; důvod je zapsaný na obou stranách.

## 2026-08-17 (4) - 🚏 seznam zastávek ukazuje jen zastávky dosažitelné z té vývěsky (#7959)

Zastávka, kam **z dané vývěsky nic nejede**, zabírala v seznamu řádek, kde bylo „<" ve sloupci časů
i ve všech variantách. Cestujícímu to říkalo „tudy se jezdí, ale odtud se tam nedostanete", takže
řádek nešel k ničemu použít. Nově se do seznamu dostane jen zastávka, kde staví **aspoň jeden spoj,
který staví i na té vývěsce**.

**Není to nový požadavek, ale dokončení odloženého.** TRANSDEV to žádal v #7959 („aby zastávka nebyla
v seznamu, pokud není aktuální", školní spoj z druhé zastávky). Filtr z 30. 7. byl **směrově
globální** - stačilo, že tam staví kdokoli ze směru - a u nás byla zapsaná nuance, že pro ten školní
scénář by bylo potřeba **from-current** sémantiku, jakou má MHD. Odložilo se to, protože se nedařilo
najít testovací linku; Jan ji našel 17. 8. při přípravě ukázky pro jiného zákazníka
(`CB_CSADAD` 0330530, zastávka Střítež,žel.st.točna měla „<" v časech i ve všech osmi variantách).
Agendy se tím zároveň srovnávají - MHD to tak dělá odjakživa.

⚠️ **Mění to tištěný výstup všem AD vývěskám**, kde takové zastávky jsou: na dvou kontrolních linkách
ubylo 36 a 53 řádků zastávek. **Odjezdů se to nedotklo** (1236 → 1236 a 498 → 498), což je ten
podstatný důkaz - neztratil se žádný spoj, ubyly jen nepoužitelné řádky.

⚠️ **Důsledek:** seznam zastávek se teď **liší mezi vývěskami téže linky**. Je to cena za to, že
každá vývěska mluví jen o tom, co z ní jede, a přesně to zákazník žádal.

**Ověřeno proklikem Jana 17. 8.** - na lince 0330530 zastávka Střítež,žel.st.točna ze seznamu
zmizela. U změny, která ubírá řádky z tištěného výstupu, je to podstatnější než zelené testy.

**Brány:** **110 Caché testů**, z toho **1 nový invariant**: každá zastávka v seznamu musí mít
aspoň v jednom časovém sloupci číslo. Ověřeno mutací - vrácení filtru na směrově globální ten test
shodí (a s ním i dva zlaté odtisky). **961 .NET** beze změny.

**Přetypovaná testovací sada:** šest linek neslo stavy „<" / prázdno **právě přes ty nedosažitelné
řádky**. Ověřeno měřením, že oba stavy vznikají dál (0330513 má 183 výskytů „<", 0320680 109
prázdných) a že je sada dál pokrývá přes linky KOVEDu - featura tří stavů z `B67` tím není dotčená.

## 2026-08-17 (4) - 🎯 uvnitř tabulky patří panel vybrané buňce (`B76`, nález z proklikání)

Po dopoledním kroku stály proti sobě **dvě zaškrtávátka „Tučné"**: v levém panelu (měnil celou
tabulku) a v liště nad plátnem (měnila vybranou buňku). Rozlišit je nešlo, takže první zkoušení
skončilo otázkou, proč se to jednou propíše všude a podruhé do jedné buňky (nález Jan 17. 8.).

Chování se nemění - **formulář prvku je pořád výchozí styl všech buněk a buňka je odchylka** -
ale rozsah je nově vidět:

- **Panel u vybrané buňky ukazuje tu buňku** (formát hodnoty, font, velikost, tučné/kurzíva/
  podtržení/přeškrtnutí, zarovnání, barvy) a začíná skupinou **„Vybraná buňka"**.
- **Tlačítko „Nastavení celé tabulky…"** je první řádek panelu - dosud se tam člověk dostal jen
  Esc, což z panelu nebylo poznat.
- **Skupina ve formuláři prvku se jmenuje „Styl všech buněk"** (dřív „Styl buněk"). Ověřeno
  voláním nasazené třídy, ne čtením zdrojáku.

⚠️ **Panel musel začít respektovat vlastní vratný příkaz deskriptoru** - jinak by zpětný krok
zapsal buňce **zobrazenou zděděnou hodnotu** místo dědění a buňka by se od tabulky natrvalo
odpojila. Právě tuhle past hlídá nový test.

**Brány:** build bez chyb, TD **961 testů** zelených (4 nové na panel).

## 2026-08-17 (3) - 🅱 pevná tabulka: buňku jde konečně naformátovat (`B76` kroky 1+2)

Pevná tabulka šla do dneška formátovat **jen jako celek** („Styl buněk" a „Styl řádku záhlaví"
v nastavení prvku). Uvnitř se měnil jen obsah a struktura, takže nefungoval ani způsob práce,
na kterém prvek stojí: vkládá se bez řádku záhlaví s tím, že *„záhlaví si uživatel udělá z prvního
řádku tím, že ho ztuční"* - a ztučnit ho nešlo. Per-buňkový styl přitom model umí a kreslení ho
respektuje; byla to **díra po zrušení pětizáložkového dialogu** (12. 8.), ne chybějící featura.

**Formátovací lišta u tabulky přestala být prázdná.** Uvnitř tabulky patří **aktivní buňce**:
tučné, kurzíva, podtržení, přeškrtnutí a zarovnání vodorovně i svisle. Stav tlačítek ukazuje
**vyřešenou** hodnotu (tedy i tu zděděnou z tabulky), ne jen to, co má buňka vlastního.

**Vlastnosti buňky** otevře **Alt+Enter** nebo místní nabídka: písmo, velikost, řez, barvy, vnitřní
okraje, řádkování a **formát hodnoty** (`dd.MM.yyyy`, `N2` u buňky s datovým polem; prázdné = podle
sloupce).

⚠️ **Dvě rozhodnutí, která stojí za zapamatování:**

- **Dialog je sdílený stylový pod-formulář** (týž, jakým se nastavuje styl zóny prvku), ne nový
  formulář ze serveru. Jediný umí stav „dědí se" i u zaškrtávátek: zděděná hodnota je šedě
  a křížkem se dědění vrací. Potvrzení beze změny tak **nezapeče** zděděné hodnoty do buňky - jinak
  by se buňka tiše odpojila od „Stylu buněk" a pozdější změna písma tabulky by ji minula.
- **Cesta k dialogu je Alt+Enter, ne Enter** - Enter uvnitř tabulky potvrzuje a posouvá o buňku níž
  a dvojklik otevírá vlastnosti prvku; obojí muselo zůstat.

**Zápis je psaný nad množinou buněk**, i když je dnes jednoprvková: výběr oblasti tažením (krok 3)
přidá jen jiný zdroj souřadnic. Zpětný krok vrací **snímek pořízený před změnou** a buňky adresuje
**souřadnicí**, ne odkazem - u skupiny tím každá buňka dostane zpět svou vlastní hodnotu a undo
funguje i po přeskládání mřížky, které buňky naklonuje.

Vedlejší nález: pole pro psaní do buňky bralo písmo ze sloupce, ne z buňky - v ztučněné buňce by
text při psaní poskočil. Nově se řídí vyřešeným stylem té buňky (včetně řezu a kurzívy).

**Brány:** build bez chyb, **2155 .NET testů** zelených (TD 957, z toho 12 nových). Dvě mutace
ověřily, že testy měří, co tvrdí: snímek pořízený až po změně shodí 4 testy vratnosti, dialog
zapékající zděděnou tučnost shodí test „potvrzení beze změny nic nezapíše".

**Zbývá v `B76`:** výběr oblasti tažením a celého řádku/sloupce, Delete a schránka nad oblastí,
sloučení buněk vodorovně.

## 2026-08-17 (2) - 🔤 písmena variant u odjezdů přestala být volba (`B90`)

Parametr „Písmena variant u odjezdů" umožňoval vývěsku, ze které **nešlo poznat, který časový
sloupec pro daný odjezd platí**. Vypnutý stav skládal nadpisy sloupců z písmen rozdílů tras, a když
je linka neměla (má vypnuté automatické poznámkování), spadly nadpisy na `T1`/`T2`/`T3` a u minut
nezůstalo nic. Naměřeno na `CB_CSADAD` lince `0330520`: zapnuto → `A` u 5:20 a 22:25, `B` u 7:42
a 11:13; vypnuto → u týchž minut žádné písmeno.

Parametr je zrušený, písmena jsou vždy. Volbou zůstalo **„První varianta bez písmene"** - to je
skutečné redakční rozhodnutí a zároveň to, co drží vývěsku s jedinou variantou bez zbytečného „A"
u každé minuty. Zmizela celá větev, která vyráběla `T1`/`T2`.

⚠️ **Kdo měl parametr vypnutý, uvidí u minut písmena.** Parametr byl vydaný od 27. 7., výchozí stav
zapnuto - riziko je malé, ale patří do popisu vydání.

⚠️ **Zlaté odtisky do teď pinovaly VYPNUTOU větev**: server čte nenastavený parametr jako `false`
a gold testy posílají jen číslo linky, takže měřily chování, které uživatel s aktuálním klientem
nikdy neviděl. Po přebaselinování měří to, co se reálně tiskne. Počty se nezměnily do jednoho
(stránky, zastávky, odjezdy i poznámky), liší se jen md5 - přibyla písmena u minut.

**Brány:** **109 Caché testů**, nabídka parametrů ověřená **voláním** nasazené agendy (klíčové bylo,
že u zbylé volby zmizela podmínka viditelnosti - jinak by z UI vypadla i ona).

**Poučení:** tři testovací třídy jsem upravil skriptem, takže je hook nenasadil a testy dál běžely
proti staré verzi - „aktuální" digest v hlášce byl přesně ten, který jsem si do souboru napsal.
Přesně ta past, na kterou `cache-api.md` varuje. Nasazení se muselo vynutit ručně.

## 2026-08-17 - ✅ vlastní sloupce proklikané, „Ne" a „+" se vylučují (`B89`)

Vizuální kontrola po `B81`/`B82` **hotová** (Jan 17. 8.): editor blokuje neslučitelné dvojice,
vzor nese `X` i `+`, sloupec „jen Ne" vypisuje odjezdy bez denních značek, sloupec „jen pracovní
dny" značí spoj jedoucí jen v pátek, režim „Automaticky" beze změny.

**Klíčový důkaz opravy** (linka 0330530, sloupec Pá+So+Ne): spoje **T239** a **T245** s platností
„sobota + neděle a svátky" mají u minut **⑥⑦**, dřív jen ⑥ - cestující se nedozvěděl, že jedou
i v neděli. Spoje s platností jen „+" (T203, T235) mají ⑦ samotné. Minut se sedmičkou jsou čtyři -
tatáž čtveřice, kterou vrátí sloupec „jen Ne", tedy dvě různě zadané sady sloupců vidí týž nedělní
provoz.

**Doladěno při tom:** „Ne" a „Neděle a svátky (+)" se teď **vylučují** jako „X" s Po-Pá. Dřív šly
zaškrtnout obě s odůvodněním, že vzor posílá `7+` - to od `B82` neplatí, vzor posílá samotné `+`
a server si „7" doplní sám. Ověřeno během: sloupce `7`, `+` i `7+` daly nad toutéž linkou
identický výstup, takže dvojice nic nepřidávala a jen mátla. Starší uložený sloupec s `7+` se
načte jako `+`.

⚠️ **Testovací vzor v .NET testech byl rozešlý se serverem** - nesl `12345` a `7+`, zatímco server
od `B82` posílá `X` a `+`, takže testy potvrzovaly chování, které už neexistuje. Srovnáno; na server
se z .NET testů nedosáhne, takže tvar hlídá jen poznámka u fixture.

**Brány:** **945 .NET testů**. Dva testy, které zakotvovaly staré chování dvojice, přepsané -
změnilo se rozhodnutí, ne že by přestaly platit.

## 2026-08-14 (4) - 🅧 dny sloupce se zadávají v abecedě vyhlášky, včetně „pracovních dnů" (`B82`)

Vlastní sloupce vývěsky měly zaškrtávátka Po-Ne + Svátek a **„pracovní dny" (`X`) nešly zadat vůbec**.
Přitom `X` **není zkratka za Po-Pá**: pracovní dny svátky nezahrnují, kdežto vypsané Po-Pá ano - spoj
„jede v pondělí" jede i o svátečním pondělí, spoj „jede v pracovních dnech" ne. Vyhláška 122/2014 ty
dvě věci výslovně rozlišuje a zakazuje jejich kombinaci.

**V editoru přibylo „Pracovní dny (X)"** a „Svátek" se přejmenoval na **„Neděle a svátky (+)"** -
značka podle vyhlášky pokrývá obojí, takže původní popisek lhal. Obojí je v jedné sekci
**Týdenní platnost** vedle dnů.

**Zaslepuje se jen kombinace, která si odporuje:** „Pracovní dny (X)" s vypsanými Po-Pá (`X` svátky
nikdy, vypsané dny svátky, které na ně padnou, ano). „Ne" a „Neděle a svátky" se **nevylučují** -
zákaz `+` se `7` z vyhlášky platí pro značky u spoje, ne pro sadu dnů sloupce.

**Značka `+` znamená „neděle a svátky" i u sloupce**, ne jen svátky (rozhodnutí Jan 14. 8.). Sloupec
„Neděle a svátky" tak stačí zaškrtnout **jednou volbou** a spoje jedoucí jen v neděli z něj
nevypadnou - vzory ho proto posílají jako samotné `+` (a slitý víkend jako `6+`).

**Dvojznačnost, kvůli které se to rozjíždělo, je pryč.** Týž zápis `12345` dosud znamenal na dvou
místech dvě různé věci: při zařazení spoje kalendářní Po-Pá, na ose Období „pracovní dny" (svátek
do nich nikdy nepadl). Nově:

- **`X`** = pracovní dny. Pro zařazení se chová jako 1-5 (spoj jedoucí jen v pondělí do sloupce
  patří, jen ho nepokrývá celý, takže dostane značku), ale **svátek do něj nepadne nikdy**.
- **`12345`** = kalendářní Po-Pá, tedy **včetně svátků**, které na ně padnou.

**Vzory sloupců proto posílají u „Pracovních dnů" nově `X`.** Kdo má vlastní sloupce postavené na
vypsaném Po-Pá, dostane od teď do nich i svátky - což je to, co ten zápis podle vyhlášky znamená.
⚠️ Zákaznických šablon se to netýká (vlastní sloupce se do vydané verze nikdy nedostaly), ale
**naše testovací šablony v DB je nesou a je potřeba je přeuložit**.

**Brány:** **109 Caché testů** (1 nový na pravidla `X`) a **940 .NET** (7 nových na editor - zápis
`X`, vzájemné zašednění, a že otevřít a zavřít dialog beze změny definici nezmění). Zlaté odtisky
přebaselinované: **mění se jen md5, počty stránek/zastávek/odjezdů/poznámek jsou shodné**, a je to
ověřené izolovaně - s vzory vrácenými na `12345` projdou odtisky beze změny, takže rozdíl nepochází
odjinud než z pole `PRESETY_SLOUPCU`.

## 2026-08-14 (3) - 🗓️ vlastní sloupec „Neděle" už nevrací prázdno (`B81`)

Zákazník si ve vlastních sloupcích vývěsky udělal sloupec **Neděle** a nevyšel v něm **ani jeden
odjezd**, přestože na lince nedělní spoje jsou. Příčina: spoje mají platnost **`+`**, což podle
[vyhlášky 122/2014](https://krajta.slv.cz/2014/122) znamená „jede v neděli **a** ve státem uznané
svátky" - jenže program dělal průnik holých znaků, takže `+` do sloupce `7` nespadlo.

**Nově z `+` plyne i neděle.** Že jsou to dvě různé značky a jedna druhou zahrnuje, potvrzuje sama
příloha vyhlášky zákazem kombinace `+` se `7` - kdyby `+` neděli nezahrnovalo, dávala by ta dvojice
smysl.

**Značka u minut se přitom netiskne** - spoj `+` jede každou neděli, takže sloupec pokrývá celý
a vysvětluje ho nadpis. Potvrdil Jiří Bžoch 14. 8.: značka patří do opačného případu, tedy když spoj
sloupec **nepokrývá** (sloupec „pracovní dny", spoj jedoucí jen v pondělí → u minut `1`).

**Druhá polovina téže chyby byla na ose Období:** svátek patřil jen do sloupce, který vyjmenoval `+`,
takže velikonoční neděle nepatřila do sloupce „Ne" a spoj se `7` z toho vyšel jako „nejede celé
období" - a držel si negativní značku, kterou mít neměl. Nově svátek padnoucí na neděli do nedělního
sloupce patří. U dnů 1-6 to vědomě neplatí: „Pracovní dny" nesmí tvrdit, že pokrývají 1. květen,
a rozlišit je od samostatného „pondělí" umí teprve `X` z vyhlášky (`B82`).

⚠️ **Vedlejší dopad, který je vidět:** spoj `6+` dostane ve vlastním sloupci Pá-Ne značky **`67`**
místo dosavadní `6`. Dřív se neděle zamlčela a značka tvrdila, že spoj jede jen v sobotu.
Standardních rozvržení (`12345` / `6` / `7+` / `67+`) se to netýká, ta si drží dnešní chování.

**Brány:** **108 Caché testů** (2 nové: pravidlo `+` → neděle vč. zařazení a značek, a end-to-end
sloupec „Neděle" na lince `CB_CSADAD 0330530`) a **918 .NET**. Mutace (vypnout odvození neděle) shodí
právě ty dva a e2e hlásí doslova zákazníkův příznak: *„vlastní sloupec Neděle nevrátil ani jeden
odjezd"*. Linku vybrala nová sonda `AdSloupekProbe.NajdiPlusSpoje` - v CB_CSADAD je **4 840 spojů
s platností `+`** na 3 000 linkách, takže to není okrajový tvar dat.

## 2026-08-14 (5) - 🔗 rozvržení sloupců vývěsky se nastavuje z jednoho místa (`B88`)

Na otázku „jaké sloupce vzniknou" odpovídala **dvě** nastavení naráz - parametr sestavy „Rozdělení
sloupců" a vlastní sloupce v prvku odjezdů - a vlastní sloupce parametr **tiše přebíjely**. Kdo je
jednou měl, měl z parametru mrtvé tlačítko; a protože nový prvek odjezdů je dostává **předvyplněné**,
potkávalo to prakticky každou AD šablonu.

Stav teď drží jen šablona a parametr je její **pohled i ovladač**:

- sloupce odpovídají některému vzoru → parametr ukazuje jeho název
- žádné vlastní sloupce → „Automaticky (dle linky)"
- ručně upravené (jiné dny, přejmenovaný nadpis, ubraný nebo přeskládaný sloupec) → nová volba
  **„Vlastní rozložení"**, na kterou se parametr překlopí sám
- výběr pevného rozvržení zapíše ten vzor do šablony (vratné Ctrl+Z)

Vzory jsou pro tenhle účel použitelné proto, že jsou **1:1 náhrada pevných rozvržení** - hlídá to
živý paritní test, který porovnává celý výstup obou režimů.

Cestou se opravily dvě věci, na kterých to při proklikávání stálo:

- **Formulář parametrů se inicializuje při každém zobrazení tabu** a bere hodnoty ze snímku, který
  mu okno předalo naposledy. Ten se neobnovoval, takže se změněná hodnota po přepnutí tabu vrátila
  na starou - a kdo na tab přišel až po změně, neviděl ji vůbec. **Netýkalo se to jen rozvržení:**
  stejně se choval každý parametr měněný v tom tabu, tedy zase „UI ukazuje něco jiného, než se
  tiskne". Snímek se teď obnovuje s každou změnou hodnoty.
- **Rozpoznání „jsou tyhle sloupce některý vzor?" existovalo dvakrát** - editor sloupců ignoroval
  vnitřní identifikátor sloupce, nová logika ne. Editor by ukazoval vzor a parametr nad týmiž
  sloupci „Vlastní rozložení". Sjednoceno do jedné sdílené funkce (identifikátor je vazba pro
  styly, ne obsah).

⚠️ **Rozvržení se přestalo ukládat jako předvolba agendy** - je to vlastnost šablony, uložené napříč
šablonami by u každé další lhalo.

**Brány:** **932 .NET testů** (8 nových na porovnání sloupců se vzorem - každé pole, které jde na
server, musí zakládat rozdíl), **106 Caché**. Nová volba ověřena **voláním** nasazené agendy.

## 2026-08-14 (4) - 🔄 náhled se srovná s parametry sestavy na všech cestách (`B87`)

Parametr, jehož hodnotu zpracovává server (`ROZDELENI_SLOUPCU` a spol.), musí po změně spustit
**nové načtení dat** - jinak náhled ukazuje data předchozího nastavení, zatímco ovladače i tisk už
jedou podle nového. To rozhodnutí uměla jen jedna ze tří cest, kterými se hodnoty mění:

- **otevření šablony** - data se tahají při obnovení instance, tedy **dřív**, než se aplikují uložené
  hodnoty parametrů. Po otevření tak náhled patřil výchozímu nastavení, i když ovladač ukazoval
  uloženou volbu. Navíc pak nešlo „změnit" volbu na tu, kterou ovladač už ukazoval - vyhodnotilo se
  to jako beze změny a náhled zůstal rozejitý.
- **dialog Tisk** - jen překreslil ze starých dat.
- panel parametrů vlevo re-fetch spouštěl správně.

Rozhodování je teď v **jednom místě** (`RefreshPreviewAfterParamChange`), které volají všechny tři.

**Brány:** **924 .NET testů** (3 nové, zdrojové: každá cesta se ptá toho jednoho místa a žádná si
netahá data sama). Ověřeno mutací - vrácení jedné cesty na pouhé překreslení test shodí.

⚠️ **Nezmizí tím druhá polovina**: vlastní sloupce vývěsky parametr pořád tiše přebíjejí. Řeší se
zvlášť (`B88`).

## 2026-08-14 (3) - 🔓 nové povinné pole už nezablokuje formulář staršímu programu (`B85`)

Pole přidané do formuláře jde na server hned, ale do programu až s vydáním. Starší program tedy
dostal schéma s polem, ke kterému nemá vlastnost - a když bylo **povinné**, nešel formulář vůbec
potvrdit: prefill neměl co načíst, uživatel neměl co vyplnit a neuložil ani zbytek. Přesně takhle
se 13. 8. projevil „Rozestup odjezdů" (pole z 12. 8., kolega měl program z 11. 8.).

Nově se povinnost u pole, ke kterému program nemá vlastnost, **ruší**. Není co obcházet: hodnotu
takového pole stejně zahodí zápis do modelu, takže povinnost jen blokovala. Serverová kontrola při
uložení platí dál - ruší se jen klientská hláška.

**Brány:** **921 .NET testů** (3 nové: povinné pole bez vlastnosti přestane být povinné, pole
s vlastností povinné zůstane, totéž pro vnořenou cestu).

## 2026-08-14 (2) - 🔎 `B84` zavřeno bez práce: značky z vyhlášky MHD neprodukuje

`B84` předpokládal, že se u minuty vytiskne značka `+` / `X` bez vysvětlivky. Neplatí:
`ALVA.MHD.KalendarFce.MakeRozpisTplProTypProvozuID` vrací rozpis **jen pro dny 1-7** („pracovní
dny" rozepíše na 1-5, „neděle a svátky" na 7), takže znaky z vyhlášky z MHD vyjít nemůžou.
Ověřeno zdrojem kalendáře **i během nad daty** `CB_DP` (rozpis vrátil klíče `1,2,3,4,5`).

⚠️ **Značky podle vyhlášky jsou téma zastávkového JŘ AD, ne MHD** (upřesnění Jan 14. 8.): v MHD
se platnost bere z **typu provozu**, ne z ručního zadávání dnů. Celá revize `B39` se tedy týká AD
a v MHD se kolem ní nemění nic.

## 2026-08-14 (1) - 📖 vysvětlivky dnů na vývěsce AD čtou zákaznický číselník (`B83`)

Zastávková vývěska tiskla u kroužkových denních značek texty **napevno**, takže vlastní znění
zákazníka (edituje si ho v EDISONu) se ignorovalo. Starý modul i náš vlastní **linkový** JŘ ho
přitom z číselníku berou - dvě naše sestavy tedy u téže značky tiskly jiný text.

Vysvětlivky se teď načtou z číselníku jednou na začátek sestavy a vestavěná znění zůstávají jako
záloha pro instalaci, kde značka v číselníku není. Záznam se vybírá **podle jazyka sestavy**, ne
podle toho, kterou jazykovou variantu má firma vyplněnou: firmy mají běžně obě, takže starší
způsob (vzít tu, která je vyplněná) by slovenské sestavě podstrčil české texty.

Zároveň se srovnalo vestavěné znění značky `+`: **„jede v neděli a ve státem uznané svátky"** místo
dosavadního „jede ve svátek". Značka podle vyhlášky 122/2014 pokrývá oba dny a stejné znění nese
i výchozí číselník starého modulu.

**Brány:** **106 Caché testů** (2 nové: text ze číselníku přebíjí vestavěný; nad reálnými daty se
česká sestava trefí do českého záznamu a slovenská do slovenského), **73 .NET** kontrol schémat.
Zlaté odtisky se nehnuly - na kontrolních linkách se `+` do legendy netiskne, protože ji říká už
nadpis sloupce.

## 2026-08-13 (7) - 🔍 kontrola uvozovek v `.cls` chytá i druhou půlku té třídy chyb (`B78`)

Statická kontrola hlásila jen řádek, který **končí** uprostřed textu (lichý počet uvozovek).
Uvozovka uvnitř textu, za kterou pokračuje další text v uvozovkách, je **párná** a prošla:

```
do $$$AssertEquals(x, 1, "„<" nevzniklo (" _ y _ ")")
```

Tenhle tvar se nezkompiluje, ale test mlčel - a projevilo se to 13. 8. jako **Caché test padající
bez hlášky**, protože na serveru zůstala nezkompilovaná třída. `cache-api.md` přitom tvrdilo, že to
tenhle test hlídá; teď to platí.

Nová část kontroly hledá **zavřenou uvozovku, za kterou stojí holé slovo**. ⚠️ **Uplatní se jen
uvnitř závorek** - mimo ně je „text, mezera, slovo" legitimní podmíněný příkaz
(`s:group'="" param.group = group`) a v repu jich jsou desítky, takže širší pravidlo by hlásilo
falešné poplachy. V praxi chyba vzniká v argumentu (hlášky testů, helpTexty), kde závorky jsou.
Ta mez je zapsaná v testu jako `[InlineData]` s očekáváním „projde", ať je vidět, ne zamlčená.

**Brány:** **2116 .NET testů**; kontrola projela všechny `.cls` v repu bez nálezu. Vyladěno proti
reálným falešným poplachům - první podoba pravidla (bez závorkové podmínky) jich nahlásila 18.

## 2026-08-13 (6) - ↕️ třetí volba řazení variant: od nejrychlejší k nejpomalejší (`B67` krok 4)

K dosavadním dvěma (dle četnosti ze zastávky, stabilní dle spojů) přibylo **„Dle délky jízdy
(nejkratší první)"**. Nejrychlejší varianta pak stojí hned vedle sloupce „Čas" a sloupce se čtou
jako škála, ne jako nahodilé pořadí. Délka = kumulativní čas nejdál od vývěskové zastávky, tedy
**táž hodnota, kterou se vybírá průběh varianty** - řazení a výběr tak nemůžou tvrdit každý něco
jiného. Shoda délek → pořadí výskytu, ať výstup nezávisí na pořadí průchodu daty.

Směr řazení je **v popisku volby**, ne jen v nápovědě: popisky voleb jsou po vydání zamčené
(persistuje se zobrazovaný text), takže „nejkratší první" už nepůjde doplnit přejmenováním.

**Brány:** **103 Caché testů** (1 nový: řazení nesmí ubrat ani přidat stránku, odjezd nebo sloupec -
je to volba zobrazení, ne filtr - a sloupce musí být opravdu seřazené podle dojezdu). Mutace
(seřadit obráceně) test shodí a řekne konkrétní stránku i čísla. Nabídka voleb ověřena **voláním**
nasazené třídy nad druhou firmou.

## 2026-08-13 (5) - 🔤 značka u minuty i vysvětlení trasy naráz (`B67` krok 3)

Slovenský zákazník z callu chtěl u minuty poznat, **kterým sloupcem** spoj jede. Dosud to šlo jen
za cenu ztráty: režim „Písmena variant u odjezdů" značky přidal, ale **vypnul automatické rozdílové
poznámky**, takže z legendy zmizelo vysvětlení „neobsluhuje zast. Lesní hřbitov". A sloupce, které
si hlavičku nevypůjčily od poznámky (náš náhradní název „T1"), neměly ani jedno - četly se
vylučovací metodou.

**Nově běží obojí vedle sebe** a písmena jsou **zapnutá jako výchozí**: značka u minuty říká
**který sloupec**, poznámka **kudy**. Přesně tak to má i starý modul, který u minuty skládá obojí
zvlášť (`JRapiLinPrtMhd1.mac:529`, značka sloupce `:297`).

⚠️ **Tohle NENÍ parita se starým modulem a nesmí se tak tvrdit.** Legacy bere značky sloupců
z **nastavení u linky** (`^JRL(sku,lin,"PATCH","mhd","par",2)`, `JRapiLinPrtMhd1.mac:26`) - i-tý znak
patří i-tému sloupci. Žádný default v kódu není; „UVWXYZ" je jen komentář vedle toho řádku.
**Změřeno: 0 z 325 linek to má vyplněné** (Nitra 0/29, CB_CSADAD 0/240, GW BUS 0/52, KOVED 0/4),
takže starý modul těmhle zákazníkům značky netiskne vůbec a „udělat to jako legacy" by nedodalo nic.
Dosavadní tvrzení roadmapy („legacy má v kódu default UVWXYZ") bylo mylné.

⚠️ **Vědomý kompromis** (rozhodnutí Jan 13. 8.): značky variant jedou od **A** stejně jako písmena
rozdílových poznámek, takže se na jedné minutě můžou potkat dvě stejná písmena v jiném významu.
Starý modul se tomu vyhýbal řadou od konce abecedy. Řeší se, až na to někdo narazí - je možné, že
se ta volba časem přestěhuje z nastavení linky do návrháře.

**Brány:** **102 Caché testů** (1 nový: v režimu písmen musí zůstat značky variant, rozdílové
poznámky u minut i jejich vysvětlení v legendě) a **902 .NET**. Mutace (vrátit vylučování) shodí
právě ten nový test. Parametry ověřeny **voláním** nasazené třídy - `PISMENA_VARIANT` má nový
výchozí stav i popis. K měření přibyla read-only sonda `ZnakySloupcu`.

## 2026-08-13 (4) - 🔢 nula ve sloupci časů se tiskne, prázdno zůstává významu „nejede tudy"

Nález Jana při proklikávání `B67` (linka 9 Nitra, zastávka **Bizetova**): sloupec „Čas" byl na jejím
řádku **prázdný**, zatímco variantní sloupce vedle měly **0**. Jeden údaj, dva zápisy na jednom řádku.

**Proč byl prázdný špatně:** prázdno v seznamu zastávek **něco znamená** - „tudy tenhle průběh
nejede". To jsme právě v kroku 2 doladili, aby šlo odlišit projíždění (`<`) od neobsluhování.
Bizetova je ale hned za vývěskovou zastávkou, jede se tam pod minutu, takže vyjde nula - a ta se
v sloupci „Čas" potlačovala. Cestující tedy četl „sem to nejede" u zastávky, kterou spoj obsluhuje.

**Nově jdou oba sloupce touž cestou.** Nula se tiskne; kdo chce čitelnější tvar, zapne si
„Jízda pod minutu jako <1" (chová se pak stejně v obou rodinách sloupců, jako dosud).

**Řádek vývěskové zastávky zůstává prázdný** - a nově i ve variantních sloupcích, kde se dosud
nula tiskla. Na zastávce, kde stojíš, není „0 minut" údaj, ale definice; vyjde stejně v každém
sloupci, takže je to jen šum. Odpovídá to tvaru, který mají zapnuté „<1" už dnes.

⚠️ **Dopad na MHD prověřen zvlášť** - MHD posílá nulu i pro **projeté zastávky**
(`ZastavkoveJrMhd.cls:1141`; AD tam má záporný čas), takže bez ohledu by se vývěsky DPCB/DPKV nad
vývěskovou zastávkou pokryly nulami. Výchozí „Nezobrazovat" je odfiltruje jako dosud a hlídá to test
pro obě podoby dat.

**Brány:** **2100 .NET testů** (902 TD). Mutace (zrušit výjimku pro řádek vývěskové zastávky) shodí
**6 testů**. Zlaté odtisky se nemění - jde o vykreslení textu buňky, ne o data z agendy.

## 2026-08-13 (3) - 🎚️ varianty tras se zapínají ve sloupcích, ne zaškrtávátkem (`B80`)

Podnět Jan 13. 8. při přípravě proklikání `B67`: „varianty tras se zapínají mimo sloupce, není to
atyp?" Je - a byl to pozůstatek. Dokud varianty sloupec „Čas jízdy" **nahrazovaly**, bylo to
zaškrtávátko přepínačem režimu a do panelu patřilo. Od 13. 8. stojí obě rodiny sloupců vedle sebe,
takže to bylo **nastavení sloupců schované mimo sloupce**.

Nově je to položka **„Varianty tras"** v seznamu sloupců, s vlastní viditelností, šířkou a stylem.
Při tisku se rozvine na tolik sloupců, kolik má linka variant; linka s jedinou variantou žádný
nedostane (to je sloupec „Čas jízdy"). Že se jedna položka rozpadne na víc sloupců, říká dialog
textem - jinak by uživatel nepochopil, proč mu jedno zaškrtnutí udělalo dvanáct sloupců.

**Tři věci, které to spraví jako vedlejší efekt:**

- **Šířka variant šla nastavit poprvé** - dosud byla zadrátovaná na 6 mm. Platí pro **jeden**
  sloupec, ne pro celý blok: počet variant je daný daty, takže dělením jedné šířky by linka
  s dvanácti variantami dostala nečitelné sloupce.
- **Styl a zarovnání si varianty nesly parazitně ze sloupce „Čas"** - obcházení chybějícího modelu,
  na které v kódu ukazoval komentář. Teď mají vlastní.
- **Past: skrytý sloupec „Čas" tiše vypínal varianty** (rozvíjely se z něj), a s nimi i rozpad
  zastávky na řádky podle stanovišť. Do 12. 8. to nešlo potkat, protože se varianty s „Časem"
  vylučovaly; od jejich oddělení to byla reálná past. Obojí je teď nezávislé.

⚠️ **Zákazník nepozná nic** - uloženou volbu převede migrace šablony (1.7.0 → 1.8.0) a zapne položku
jen tam, kde se varianty reálně tiskly, tedy když byl viditelný i sloupec „Čas". Zapnout je podle
samotného zaškrtávátka by lidem se skrytým „Časem" vysypalo do hotové vývěsky až dvanáct sloupců,
aniž by na cokoli klikli (rozhodnutí Jan 13. 8.). Zarovnání a styl se ze sloupce „Čas" zkopírují,
ať se nezmění vzhled.

**Brány:** build 0 chyb, **2093 .NET testů** (895 TD vč. 15 nových na migraci a 11 na rozvinutí
sloupců) a **101 Caché testů**. Mutace migrace (zapnout podle zaškrtávátka bez ohledu na sloupec
„Čas") shodí právě ten test, který věrnost převodu hlídá. Formulář prvku ověřen **voláním serveru** -
a vyplatilo se: nasazovací hook se po editaci `.cls` nespustil a server vracel starou třídu i se
zrušeným polem; po vynuceném nasazení vrací novou.

## 2026-08-13 (1) - 🧩 sloupec časů a varianty tras jsou dvě nezávislé věci (`B67` kroky 1+2)

Dohoda z 13. 8. (Jan + kolega): zachovat legacy rozdělení „Časy" × „varianty", ale počítat obojí
automaticky. Dva kroky naráz, protože sedí na témže kódu.

**1. Sloupec „Čas jízdy" už varianty nevytlačí.** Dosud ho variantní sloupce **nahradily**, takže na
zastávce se dvěma variantami z vývěsky zmizela deklarovaná doba linky. Teď zůstává a varianty se řadí
**za něj** - přesně jako ve starém modulu, kde vedle sebe stojí „Časy" a „Trasa min. 1-15", a ten je
navíc plní **bez ohledu na přepínač u linky** (`JRapiExp10.mac:383-420`), takže naše svázání bylo
vlastní konstrukce, ne parita. Sloupec Času má vlastní viditelnost jako každý jiný.

⚠️ **Existující šablony tím na takových zastávkách dostanou o sloupec víc.** Kdo ho tam nechce, vypne
ho v nastavení prvku. V režimu „Řádek pro každé stanoviště" se vypíše **jen na prvním** z rozvinutých
řádků - na stanoviště vázaný není, stejně jako značka „projíždí".

**2. Sloupec „Čas jízdy" nese tři stavy** jako variantní: číslo, **„<"** (základní trasa zastávkou
projíždí bez stání) a prázdno (mimo základní trasu). Dřív bylo prázdno v obou případech, takže
z jediného sloupce nebylo vidět, že tudy spoj vůbec jede - legacy tam „<" má. Nula a záporný čas se
dál netisknou (nula patří volbě „jízda pod minutu", záporný čas projeté zastávce).

**3. Variantní sloupec je průběh jedné skutečné trasy.** Dosud se skládal jako **maximum per zastávka**
přes spoje varianty, takže mohl nést kombinaci časů, **kterou nejede žádný spoj** - a v datech to nejde
poznat, protože každé číslo samo existuje. Nově se vybere jedna trasa: v rámci varianty vyhrává delší
jízda, při shodě nižší číslo trasy. Legacy dělá totéž („nechám průběh s delším časem"). Z téže trasy
jde i stanoviště varianty a úsek před vývěskou, takže se sloupec nemůže rozejít sám se sebou.

**Jedno pravidlo pro obě rodiny:** skutečný průběh jednoho spoje, kritérium **dosah**, při shodě
**delší jízda**. U variant je dosah z definice shodný, takže tam rozhoduje čas. Tiebreak „delší jízda"
je **naše rozhodnutí, ne parita** - legacy při shodné délce zápisu rozhoduje pořadím zpracování, tedy
nahodile; pro cestujícího je bezpečnější pozdější číslo.

**Brány:** **873 .NET testů** zelených (8 nových: rozdělení sloupců, tři stavy základního sloupce,
základní čas jen na prvním řádku stanoviště) a **98 Caché testů** (4 nové). Gold odtisky přebaselinované:
**počty stránek, zastávek, odjezdů i poznámek jsou shodné** (39/800/844/235 a 38/762/498/236), mění se
jen hodnoty v časových sloupcích - tedy se neztratil žádný spoj ani řádek. Mutační kontrola: +1 minuta
ve variantním sloupci shodí nový invariantní test na **60 z 60** sloupců.

**Testovací sada už není jedna linka.** Gold linka 0365001 stav „<" vůbec nevyrobí (její nejdosažnější
trasa obsluhuje všechno), takže by ta změna prošla jako inertní. Sonda proto prošla čtyři firmy
a invariantní testy teď jedou přes **šest linek ze čtyř firem**, každou s jiným tvarem dat:

- `SKNI_MHDNITRA 0000012` - zákazníkova linka z #12843: 67 stránek, 48 s variantami, až 6 sloupců, **164× „<"**, 48× prázdno
- `CB_GWBUS 0320118` - největší rozsah: 101 stránek, 80 s variantami, až 10 sloupců, 383× „<", 819× prázdno
- `CB_CSADAD 0330530` - **nejvíc sloupců variant (12)**, 18× „<"; přibrána i jako třetí gold odtisk
- `SKNI_MHDNITRA 0000002` - přepínač u linky v ručním režimu a vyplněné ruční minuty
- `ZL_KOVED 0823955` - prázdno v Času bez jediného „<"
- `CB_CSADAD 0365001` - původní gold, kde nevzniká ani jeden z těch stavů

Nové odtisky jsou jen ze `CB_CSADAD` - digest na živých datech jiného zákazníka by padal, kdykoli si
upraví jízdní řád, takže ostatní firmy drží **invarianty**, ne hash. Když některá firma na serveru
chybí, sada ji přeskočí a kontrolu obou stavů vynechá, aby test nepadal podle toho, kde běží.

## 2026-08-13 (2) - 🧪 typová sada linek: každá situace má svou linku

Rozšíření předchozího bodu na to, o co šlo doopravdy: **ne jeden příznak, ale typy situací**. Sonda
změřila profil linek napříč čtyřmi firmami (co v jejich výstupu reálně vzniká - varianty, projíždění,
prázdno, víc stanovišť u zastávky, pokračující spoje, kola, místenky, rozdílové poznámky, týdenní
platnost, poznámky linky, výluky, zóny, přestupy, směrové poznámky, ruční režim přepínače) a z toho
je postavená **sada deseti linek**, kde každý typ někdo drží. Typy jsou u linek zapsané jako značky.

**Nové brány** (`ZastavkoveJrAdSadaTest`):

- **Sada nesmí vyhnít** - u každé linky se kontroluje, že typ, který má pokrývat, na ní **opravdu
  vzniká**. Kdyby zákazník upravil jízdní řád a linka svůj typ přestala nést, ostatní testy by dál
  běžely a nic nekontrolovaly. Test to odhalil hned při psaní: sám jsem měl chybné srovnání (v Caché
  je `=` řetězcové, takže nedefinovaná hodnota se nerovná nule) a kontrola vycházela naopak.
- **Variantní pole musí být zarovnaná s hlavičkami** - časy, stanoviště i sloupky se čtou po indexech,
  takže o jedno kratší pole znamená čas přiřazený cizí variantě, který v tisku vypadá platně. Mutace
  (jedno pole o prvek kratší) shodí test na **7996 z 12084 řádků**.
- **Značka u minuty musí odkazovat na sloupec, který na vývěsce je** - jinak si cestující přečte
  písmeno, ke kterému není co dohledat. Zároveň se přepnutím režimu písmen **nesmí změnit počet
  stránek ani odjezdů** (je to volba zobrazení, ne filtr dat). Mutace (písmeno u minuty o znak jinak)
  shodí test na **2085 z 2085 značek**.

⚠️ **Caché sada se prodloužila z ~40 s na ~1 min 45 s** - každý test je jeden průchod `GetData` nad
deseti linkami a největší z nich má 101 stránek. Vědomá cena za pokrytí; kdyby to začalo vadit,
dá se to zkrátit sloučením průchodů do jednoho testu, ne zúžením sady.

## 2026-08-12 (10) - 🔧 jeden sloupec jízdních dob už nekončí v polovině seznamu (#12843)

Zákazníkův první bod: „ak zvolím zobraziť len jeden variant, je to automaticky prvý nájdený, niekedy
len do polovice trasy". Zase to byla **odchylka od starého modulu**, ne chybějící volba.

**Příčina:** vybírali jsme trasu, kterou na vývěskové zastávce staví **nejvíc spojů**. Nejčetnější
bývá zkrácená trasa, takže sloupec se vyplnil do poloviny seznamu a zbytek zůstal prázdný. Starý
modul v automatickém režimu bere **nejdelší** spočítaný průběh (`JRapiLinPrtMhd1.mac:330` řadí
průběhy od největšího a jedničku dává do sloupce „Časy").

**Teď se vybírá podle pokrytí seznamu** - kolik zastávek trasa od vývěsky dál obslouží; při shodě
delší jízda, pak víc spojů, pak nejnižší číslo trasy (aby výstup nezávisel na pořadí průchodu).
Pokrytí měříme přímo počtem zastávek, legacy ho aproximuje délkou textu průběhu.

**Změřeno na zákazníkově lince (MHD Nitra 12, 67 zastávek):** vyplněných řádků **676 → 946**, což je
**dosažitelné maximum** - sloupec teď sedí na maximu na **všech 67 stránkách** (dřív na 30).
Nejvýraznější zastávka Gorazdova: **17 → 28 z 32 řádků**. Zbylé 4 řádky neobslouží žádná jediná
trasa, takže víc než tohle jedním sloupcem nejde.

⚠️ **Cena, kterou to má:** trasa s největším pokrytím může být vzácný spoj, takže časy nemusí patřit
tomu, čím jede většina. Prázdný řádek ale vypadá jako chyba, o minutu jiný čas ne.

⚠️ **Co to nemění:** rozdílové poznámky („neobsluhuje zast. X") si vzorovou trasu berou z `J`-věty
(pole 10/7), ne z tohoto výběru - ověřeno, `zaklCt` se používá jen na tenhle sloupec.

**Kontext, který u toho vyšel** (a stojí za zapamatování): sloupec „Časy" ve starém modulu **není
žádný „výchozí spoj"**. Přepínač u linky (`PATCH,"mhd","par",1` = „automaticky generovat časové
průběhy tras") rozhoduje, jestli tam jsou **ručně zadané minuty u trasy** (`J`-věta, segment 8), nebo
spočítaný průběh. Nitra má automatický režim, CB_CSADAD ruční s prázdnými minutami - tedy ve starém
tiskne prázdno. My přepínač ignorujeme a vždy počítáme, což je pro oba lepší než starý stav; čtení
ručně zadaných minut zůstává neimplementované, dokud o něj někdo nepožádá.

**Brány:** **94 Caché testů** zelených (1 nový na pravidlo výběru, 5 případů včetně rozhodování při
shodě), zlaté odtisky přebaselinované (mění se hodnoty `CAS_JIZDY`, počty stránek/zastávek/odjezdů/
poznámek beze změny), **2062 .NET testů** zelených. Mutace (výběr podle četnosti) test shodí a zlaté
odtisky pod ní naopak projdou - což je zároveň důkaz, že jejich změna je právě tímto pravidlem.

## 2026-08-12 (10) - ↔️ pevná tabulka: šířky sloupců a výšky řádků tažením (`B73`)

Šířky sloupců **nešly nastavit vůbec** - zadávaly se ve smazaném pětizáložkovém dialogu a od jeho
zrušení k nim nevedla cesta. Přitom nejčastější použití je „úzký sloupec s popiskem, široký
s hodnotou", takže to nebyl komfort, ale díra po předchozím kroku.

Nově se **táhne za vnitřní hranici** - svislá mění šířku sloupce (min. 3 mm), vodorovná výšku řádku
(min. 2 mm). Funguje to na **vybrané** tabulce, nemusí se dovnitř; kurzor se na hranici mění, během
tažení se ukazuje rozměr v mm a Ctrl+Z vrátí celé tažení jedním krokem. Řádek dostal **vlastní
nepovinnou výšku** (bez ní platí společná), takže se změnou jednoho řádku nezabetonuje zbytek.

⚠️ **Tři chyby z proklikání (Jan), všechny stejného původu - vymýšlel jsem pro řádky jinou
mechaniku než pro sloupce:**

- **Nešlo chytit hranici** - kurzor se nastavoval na začátku obsluhy pohybu myši, jenže ta si ho
  na konci přenastavuje podle toho, co je pod myší. Bez kurzoru nebylo poznat, kde je citlivé místo.
- **Tažení nahoru a dolů ustřelovalo** - výška se řešila škálováním obsahu na výšku prvku, což se
  točí samo proti sobě (vyšší řádek → vyšší obsah → větší zmenšení → jiné rozměry pod myší).
  Nově řádky používají **týž rozdělovací výpočet jako sloupce** (`TableLayout.ResolveWidths`).
- **Tažení hýbalo i tím, čeho se nemělo týkat** - tažená položka dostala pevný rozměr a zbytek si
  rozdělily všechny ostatní. Nově se mění **jen dvojice u hrany**, jejich součet zůstává, zbytek
  tabulky se nehne. U sloupců se to neprojevilo, dokud se táhla první hrana.

**Vnější hrana zůstává prvku** (mění velikost tabulky), ne krajní buňce - jinak by uživatel nevěděl,
co zrovna mění. Krajní sloupec se rozšiřuje hranou vedle něj, stejně jako v Excelu a Wordu.

**Brány:** build bez chyb, **2058 testů** zelených (860 TD, z toho 7 nových: tažení nehne ostatními,
minimum drží obě strany dvojice, zvětšení prvku rozdělí výšku mezi řádky, vlastní výška řádku
přežije zvětšení i zpětný krok).


## 2026-08-12 (9) - 🔧 varianty jízdních dob: sloupce s identickými čísly zmizely (#12843)

Zákazník hlásil zbytečnou duplicitu: „spoj má v polovine trasy zachádzku, ale v úseku za ňou idú
všetky spoje rovnako a zobrazujú sa dva rovnaké stĺpce". Měl pravdu a byla to **naše odchylka od
starého modulu**, ne chybějící featura.

**Příčina:** varianta trasy se rozlišovala podle sledu zastávek přes **celý směr linky**, tedy včetně
úseku před zastávkou, na které vývěska visí. Časy se přitom počítají jako rozdíl proti té zastávce,
takže rozdíl ležící před ní na čísla nemá vliv - a dva spoje se zajezdkou v první polovině linky daly
ve druhé polovině dva sloupce s **identickými čísly**. Starý modul staví průběh smyčkou od tištěné
zastávky do konce a ukládá ho pod klíč té zastávky, tedy varianty jsou jejím vlastnictvím.

**Změřeno na gold lince 0365001:** sloupců variant **116 → 60**, z toho **56 byly duplicity**
s identickými čísly v tištěném úseku, po opravě **0**. Počty stránek, zastávek, odjezdů i poznámek
zůstaly shodné - žádný spoj ani řádek se neztratil. Ubylo na 27 z 39 stránek.

⚠️ **Úsek před vývěskou se bere z reprezentativního spoje varianty**, ne jako maximum přes její spoje.
Právě tam se totiž spoje varianty legitimně liší, takže maximum by dalo číslo, které nejede nikdo.
Týká se to jen tisku projetých zastávek (výchozí vypnutý, časy záporné).

**Proč to 20 dní nikdo neviděl:** zlaté odtisky jsou hash **našeho** výstupu, takže chytí regresi
proti včerejšku, ne rozdíl proti starému modulu - pod mutací (vrácená stará logika) prošly zelené.
Legacy oracle máme na rozdílové poznámky, ne na počet sloupců. Proto k opravě přibyl **invariantní
test** `TestZadneDuplicitniVariantniSloupce`, který hlídá přímo tu vadu: žádné dva variantní sloupce
nesmí mít v tištěném úseku identická čísla. Falešné tvrzení „1:1 ověřeno" u variantních sloupců je
v `ad-data-mapa.md` opravené.

**Brány:** **93 Caché testů** zelených (2 nové: úsek variant + ten invariant), zlaté odtisky
přebaselinované s vysvětlením, **2058 .NET testů** zelených. Mutační kontrola: vrácení staré logiky
shodí oba nové testy a invariantní test vypíše přesně `56 z 116, první: stránka 10 (Erbenova, směr T)`
- shodně s nezávislým měřením přes volání serveru.

⏭️ **Vědomě neřešeno:** obsah variantního sloupce (legacy si nechá jeden skutečný průběh, my bereme
maximum per zastávku - rozdíl v jednotkách minut), základ jednosloupcového režimu a řazení variant
podle délky jízdy. Vše čeká na rozhodnutí, sedí na témže místě v kódu.

## 2026-08-12 (8) - 🇸🇰 texty značek se řídí jazykem šablony (`B69`)

Slovenský zákazník má na vývěsce správné piktogramy, ale **popisky u nich česky** - vedle svého
slovenského komentáře z číselníku tak má naši českou větu (#12843). Texty legendy totiž skládá
server a byly napevno české.

**Jazyk teď určuje kultura šablony** (volba ve Vlastnostech stránky), ne firemní nastavení starého
modulu. Rozhodnutí Jan 12. 8.: jeden zdroj místo dvou, drží se stejnou volbou jako symbolový font,
a jedna firma tak může mít českou i slovenskou šablonu vedle sebe - to legacy neumí, protože se
u něj přepíná per firma.

Přeložené je to, co skládáme my: vysvětlivky atributů (na znamenie, lietadlo…), přestupy,
bezbariérovost, kola, místenky, prefixy datumových omezení (`premáva len` / `nepremáva`), názvy dnů
a **rozdíly tras** (`nepremáva cez zast. X`). **Znění se nevymýšlelo** - je doslova ze starého
modulu (`MODE=1` v `JRapiLinPrtMhd`), takže ho zákazník zná z výtisku. Kde starý modul slovenskou
variantu nemá, drží se jeho vzor.

⚠️ **Nepřekládá se, co napsal uživatel** - poznámky z číselníku linky, názvy zastávek ani nadpisy
sloupců dnů. To jsou data, ne naše texty; nadpisy si zákazník přepisuje v šabloně (proto měl
1. sloupec slovensky a další česky).

⚠️ **Znak bez slovenské varianty nezmizí, padá na češtinu.** Slovenská sada je override nad českou,
ne její náhrada - tiskové direktivy (tučně, jen nástup…) se jako značka nekreslí, tak se
nepřekládaly.

**Přepnutí kultury přenačte data.** Bez toho by v náhledu zůstala česká legenda až do výběru jiné
instance. `JAZYK` proto není parametr sestavy (uživatel ho v parametrech nenastavuje) a re-fetch
u něj drží tentýž snapshot jako u vlastních sloupců - driftový test `ServerSideParams` si ho
vyžádal sám a je v něm zapsané, proč tam patří.

**Brány:** build bez chyb, **2051 .NET testů** (6 nových: jazyk v parametrech, přebití persistované
hodnoty, snapshot pro re-fetch) a **91 Caché testů** zelených, 2 nové na přepínač jazyka - jeden
kontroluje, že **žádný tištěný text nezůstal bez slovenské varianty**. Mutační kontrola: zabití
rozpoznání kódu jazyka i vypnutí předání parametru testy shodí. Zlaté odtisky se nezměnily = česká
cesta je beze změny. Ověřeno **voláním nasazené třídy** na lince 0365001 (CB_CSADAD): se `JAZYK=sk`
je všech 39 stránek slovenských, bez něj českých, počet poznámek shodný.

👁 Zbývá porovnat se zákazníkovým starým výtiskem - jestli některou větu formuluje jinak.

## 2026-08-12 (7) - 📋 pevná tabulka: vkládání z Excelu (`B74`) a našeptávač polí (`B75`)

**Vložení bloku ze schránky** (Ctrl+V ve vybrané buňce): řádky se berou po koncích řádku, buňky po
tabulátorech, obsah sedne od vybrané buňky a **tabulka se dorovná**, když je blok větší. Obsah
takových tabulek mají lidé často hotový v Excelu - jinak by ho přepisovali buňku po buňce.

⚠️ **Proč růst a ne ořez:** bez výběru více buněk (`B76`) je to jediná neztrátová možnost; tak to
dělá i tabulka ve Wordu a v Docs. Až výběr oblasti bude, pravidlo se zjemní na excelové „vybraná
oblast = cíl vložení" (poznámka je u `B76`).

**Schránka uvnitř tabulky patří buňkám** - Ctrl+C, Ctrl+X i Ctrl+V pracují s buňkou, ne s prvkem.
Stejná past jako u Delete: bez toho by Ctrl+V uprostřed vyplňování vložil na plátno kopii tabulky.

**Našeptávač polí:** po napsání `{` v buňce vyskočí seznam polí agendy, filtruje se **podle názvu
i českého popisku** (pole si lidi pamatují jako „Číslo linky", ne jako `CISLO_LINKY`), šipky
vybírají, Enter nebo Tab doplní i s koncovou závorkou, Esc zavře nabídku a teprve druhý zahodí
úpravu buňky. Ručně psané `{LOKALNI_OZNACENI_LINKY}` je pozvánka k překlepu, který se pozná až
na výtisku jako vytištěná složená závorka.

⚠️ **Pád při doplnění pole a co z toho plyne** (nahlásil Jan): zápis do textového pole spustí
obsluhu „text se změnil" **synchronně**, ještě než se posune kurzor - přepočet nabídky pak dostal
zápornou délku podřetězce. Opraveno v příčině (přepočet se během doplňování vypne) i pojistkou
(pozice kurzoru se ořízne). **Rozpoznání rozepsaného `{POLE` se přesunulo z okna do
`Models/PlaceholderToken`** - v code-behindu se takový výpočet nedá otestovat jinak než proklikem.
Testy pak rovnou odhalily dvě nepřesnosti: chování dvou závorek za sebou (bere se bližší ke
kurzoru = uživatel píše nové pole) a mrtvou větev, která nemohla nastat.

**Brány:** build bez chyb, **847 testů** zelených (10 nových na rozpoznání pole u kurzoru včetně
stavu, na kterém to spadlo, a 3 na vkládání bloku).


## 2026-08-12 (6) - 🧭 slovenské texty poznámek: rozsah vyjasněný, řídit je bude kultura šablony (`B69`)

TRANSDEV hlásil, že po přepnutí na SK kulturu se mu značky pořád zobrazují česky. Prošli jsme to
a **moje první čtení bylo příliš plošné**.

**Co kultura šablony dnes reálně dělá** (dvě věci, ne jedna): formát čísel a datumů **a výběr
symbolového fontu** - `sk-SK` → `TimeTable_nove_SK`, kde `w` je autobus místo vagonu a `D` dlaň místo
křížku. Takže když Jan přepínal kulturu, správně se měnily **piktogramy** - a to byla i odpověď na jeho
starší bod „přestup na vlak se zobrazuje se symbolem bicykla".

**Co nedělá:** texty legendy. Ty generuje agenda a jsou napevno české. Na jeho vývěsce je to vidět
v jedné legendě vedle sebe - „x **na znamení**" a „**přestup na autobus**" česky (naše), ale
„K — **premáva po zast. Kmeťova**" slovensky (jeho komentář z číselníku linky).

⚠️ **Korekce odhadu:** psal jsem, že to chce vymyslet slovenská znění a poslat serveru jazyk.
První část neplatí - **starý modul má obě varianty napevno** a přepíná je firemním příznakem
`^JRSET("COME","REC")` (0=CZ, 1=SK). `SKNI_MHDNITRA` v něm má **1**, takže Nitra je označená jako
slovenská už dnes a legacy podle toho tiskne. Znění tedy **převezmeme doslova** („zastávka je len na
znamenie" je doslova text z jeho starého tisku) - žádné vymýšlení ani dotazy na formulace.

**Rozhodnutí Jan:** jazyk textů bude řídit **kultura šablony**, ne firemní příznak. Jeden zdroj místo
dvou, konzistentní s tím, jak se už vybírá font, a jde mít v jedné firmě českou i slovenskou šablonu
(což legacy neumí). Cena: kultura musí jít do `GetData` jako parametr **a do allowlistu
`ServerSideParams`** (jinak přepnutí nepřenačte data), a přepnutí kultury tím přenačítá data, ne jen
překresluje. Volitelně předvyplnit kulturu nové šablony podle firemního příznaku, ať to slovenská
firma nemusí hlídat.

→ blok **`B69`** (M). Rozsah je tím mechanický a nízkorizikový, ne návrhový.


## 2026-08-12 (5) - 🔍 projeté zastávky: nedorozumění, ne chyba (+ dohnané testy)

TRANSDEV hlásil, že se v nové verzi **přestaly zobrazovat projeté zastávky**. Prošli jsme cestu hodnot:
parametr „Tisknout projeté zastávky" je **výchozí vypnutý**, hodnoty obsahových parametrů od 15. 7.
patří **šabloně** a ta přebíjí per-agenda i defaulty (`ApplyTemplatePrintParamsToPreview`).

**Reprodukovat se to nepodařilo** - Jan to proklikal: přepnutí funguje, uložení i znovuotevření drží
hodnotu. Takže odpověď zákazníkovi je „zapněte parametr a uložte", ne oprava. Do odpovědi jde
i záchytka: kdyby se mu to překlápělo zpět, je to chyba u nás.

⚠️ **Zaparkované riziko (`B68`, spouštěč = druhá stejná stížnost):** `SyncParametersToTemplate` zapisuje
do šablony **všechny** obsahové parametry, i ty, kterých se uživatel nedotkl. Uložení tím zabetonuje
výchozí hodnotu a ta přebije per-agenda nastavení - uživatel o svou volbu přijde, aniž by něco přepnul.
To je nejpravděpodobnější vysvětlení jeho případu (jednorázově kolem 15. 7.). Neopravuje se teď, protože
samonosná šablona je záměr a bez reprodukce bychom měnili chování, které dnes funguje.

**Dohnané brány k `4d9325a`** (tam testy nešly spustit kvůli cizí rozpracované práci na tabulkách):
15 testů zelených, **mutace zabila 2** (rastr ignorující značky), plná sada **2032** zelených.


## 2026-08-12 (5) - ⌨️ pevná tabulka se vyplňuje na plátně, dialog spadl na jedinou záložku

Fáze B krok 2. Obsah ručně vyplňované tabulky se zadává **přímo v ní**, ne v okně vedle: dovnitř
**Alt+klikem** na buňku (nebo F2 na vybrané tabulce), pak se píše jako v Excelu - psaní přepíše
obsah, F2 upraví stávající, **Tab a Enter potvrdí a posunou se dál**, šipky chodí po buňkách,
Esc jde ven. **Delete uvnitř maže obsah buňky, ne prvek.** Vše přes příkazy, takže Ctrl+Z funguje.

**Řádky a sloupce z pravého kliku** (vložit nad/pod, vlevo/vpravo, smazat) - vložení řádku tabulku
o řádek **zvětší**, aby rostla jako ve Wordu; poslední řádek ani sloupec smazat nejdou. Výběr jde
s obsahem: po vložení nad zůstaneš na svém textu, ne na novém prázdném řádku.

**Dialog má jedinou záložku „Obecné"** postavenou na Caché formuláři (`TableElementEdit.cls`) -
bez tlačítka „Upravit sloupce" a bez záložek Sloupce / Řádky / Styl sloupců / Souhrn. Starý
pětizáložkový editor je **smazaný** (dialog, jeho ViewModel, XAML i mrtvé napojení v panelu):
nedosažitelný kód se dřív nebo později rozejde s tím, co dělá plátno.

⚠️ **Cesta dovnitř se hledala třikrát, ať se to nezkouší znovu:**

- **Dvojklik → do buňky** (první návrh) - zahozen. U všech ostatních prvků otevírá nastavení,
  takže by tabulka byla jediná výjimka **uvnitř naší aplikace**; ta shoda váží víc než zvyk z Wordu.
- **Druhý klik na už vybranou tabulku** - zahozen. První klik dvojkliku ho spustil dřív, než dorazil
  druhý, takže na vybrané tabulce vlezl do buňky a na nevybrané otevřel nastavení. Rozlišit to jde
  jen čekáním na případný druhý klik, což by vstup pokaždé zdrželo.
- **Alt+klik** - zůstává. Alt v aplikaci znamená „o úroveň hloub" (vytáhnout prvek ze skupiny),
  je to jeden klik bez F-klávesy a s dvojklikem se neplete.

**Drift testy schémat chytly tři rozpory** nového formuláře proti C# (výchozí hodnoty záhlaví
a čar mřížky, „Zdroj dat" jen v panelu, odlišný popisek téhož pole) - všechny srovnané, `DataSource`
je vědomá výjimka pro staré šablony v režimu „z dat".

**Brány:** build bez chyb, **2032 testů** zelených (834 TD, z toho 16 nových na trefování buněk,
řádky a sloupce a na to, že editace obsahu nezahodí formát ani styl buňky). Formulář ověřen
**voláním serveru**, ne čtením zdrojáku.

**Zbývá:** tažení šířky sloupce myší, vkládání bloku z Excelu, našeptávač polí po `{`, výběr více
buněk (a na něm slučování). Připravené bloky agend (`GetTableTemplates`) zůstaly bez cesty z UI -
patří k vkládání tabulky, ne do dialogu.


## 2026-08-12 (4) - ✅ #12843: rozestup odjezdů jde srovnat do rastru

Zákazník: *„zakladám na rovnomernom rozostupe medzi spojmi tak, aby prípadná značka pri jednom spoji
nerozhádzala zarovnanie ostatných"*. Šířka slotu odjezdu je dnes `šířka minuty + šířka jeho značek`,
takže označkovaná minuta odsune ty za sebou a mřížka se roztřepí i mezi řádky.

Prvek odjezdů má proto volbu **„Rozestup odjezdů"** (skupina Odjezdy, pod „Šířka minuty"):
*Podle obsahu* (výchozí = dnešní chování, nikomu se nic nezmění) a *Rovnoměrný*.

**Rastr se počítá za celý sloupec, ne za řádek hodiny** (rozhodnutí Jan po projití s odtiskem) -
maximum přes všechny odjezdy skupiny dnů, nejméně nominální šířka minuty. Kdyby se bralo maximum jen
za řádek, hodina se značkami by měla jiný rozestup než hodina bez nich a **čísla by nestála pod sebou**,
což je právě to, co na našem výstupu vadilo. Rovnoměrnost v řádku z toho vyjde sama.

**Proč tenhle tvar a ne zarovnání do bloku:** Jan to nejdřív čekal jako „rozpočítat na šířku řádku
s rovnoměrnými mezerami". Zákazníkův starý tisk to tak ale nemá - řádky se třemi odjezdy končí
v polovině sloupce a nerozprostírají se, zatímco k-tý odjezd je v každém řádku na stejné pozici.
Blokové zarovnání by svislice rozpadlo úplně.

⚠️ **Cena:** jedna hodina s víc značkami zvětší rozestup celému sloupci (i řádkům bez značek), takže
se do řádku vejde méně odjezdů. Proto je to volba, ne nové výchozí chování.

⚠️ **Připlácnutá značka není tento režim** - vzdálenost značky od číslice řídí „Mezera minuta ↔ značka"
(výchozí 0 mm). Zákazníkovi doporučit 0,3-0,5 mm, na jeho starém tisku ta mezera je.

**Vizuálně ověřeno Janem** na `ZL_KOVED` skupina 3, linka `0945607` (vsetínská MHD - táž linka jako
#12964), zastávka „Vsetín, Rybníky": 52 odjezdů, 28 z nich se značkou. Kontrolní linka bez rozdílu:
`0729666`, „Kyjov, aut.st." - 46 odjezdů, ale všechny mají právě jednu značku, takže rastr sedí už dnes.

⚠️ **Testy tohoto commitu nebylo možné spustit** - testovací projekt se v té chvíli nepřeložil kvůli
cizí rozpracované práci na tabulkách (`TableEditorRowRoundTripTests.cs` odkazuje na `RowVM`, který
ještě neexistuje). Aplikační projekt se přeložil, testy jsou napsané (8 asserty na rastr a jeho
uplatnění) a doběhnou, jak bude strom přeložitelný.


## 2026-08-12 (3) - ✅ #12843: nadpis skupiny dnů jde zalomit ručně (Enter i token)

TRANSDEV hlásil, že v záhlaví sloupců minut nefunguje **ani Enter, ani `
`**. Měl pravdu a byly
to **dvě nezávislé věci** - proto nezabralo nic:

- **Pole přepisu nadpisu Enter vůbec nepřijalo** (jednořádkový TextBox → Enter spustil výchozí
  tlačítko dialogu). U nadpisů sloupců zastávek je to pole víceřádkové už dávno, tady se to
  nedotáhlo. Nově `AcceptsReturn` + max 3 řádky + tooltip, takže Enter uvnitř pole dělá řádek,
  ne potvrzení dialogu (rozhodnutí Jan: konzistentní s ostatními formuláři).
- **Vykreslování zlom zahazovalo.** Záhlaví skupin dnů se skládá jinak než ostatní tabulky: dva
  řádky vznikly **jen z dat** (druhý nadpis, vlastní styl), jinak se text zalamoval podle šířky
  (dělení podle mezer). Zlom se do sdílené funkce `CellLayout.SplitLines` nikdy nedostal, i když
  ji buňky a nadpisy sloupců používají.

**Jak to je teď** (`ComposeHeaderLines`): ruční zlom = další řádek **téhož** nadpisu, tedy stejný
styl; druhý nadpis z dat zůstává sekundární s vlastním stylem; bez zlomu se pořád zalamuje podle
šířky. Tři případy, jedno místo.

⚠️ **Past, bez které by to byla polovičatá oprava:** minimální šířka skupiny se počítá z nejdelšího
**slova** nadpisu. Bez úpravy by dvouřádkový nadpis byl jedno dlouhé slovo a **roztáhl sloupec na
součet obou řádků** - zlom proto dělí slova stejně jako mezera (`HeaderWords`).

Ověřeno: 7 nových testů, **obě mutace je zabily** (nerespektování zlomu = 4 pády, nedělení slov
taky). Proklikáno Janem - Enter odentruje a nadpis je na dvou řádcích. .NET 2015 zelených.
Příručka doplněná.


## 2026-08-12 (3) - 🧱 pevná tabulka je zpátky v paletě (Fáze B, krok 1)

Ručně vyplňovanou tabulku aplikace uměla celou dobu, ale nešlo ji vložit - nebyla v paletě ani
v nabídce „Přidat", takže se k ní uživatel nedostal. **Záměr (Jan 12. 8.): není to featura pro
faktury, je to náhrada „poskládám to z textových polí".** Hlavička sestavy i blok v těle faktury
(platební údaje, rekapitulace) má být **jeden** prvek, se kterým se hýbe vcelku. Důkaz, že chybí:
naše vlastní systémové šablony mají hlavičku poskládanou z 5-6 samostatných textů.

**Co je nově:**

- **Vloží se jako mřížka 3 × 3** s viditelnými čarami, prázdná a připravená k vyplnění.
- **Bez řádku záhlaví** (rozhodnutí Jan): záhlaví si uživatel udělá z prvního řádku tím, že ho
  ztuční. Přepínač zůstal jen kvůli šablonám, které ho už mají.
- **Přepínač režimu (pevná / dynamická) z panelu pryč.** Dynamickou tabulku dělá samostatný prvek;
  přepnutí vedlo do stavu, ze kterého se uživatel nedostal zpátky.
- **Zmenší obsah při zmenšení prvku**, jako ostatní tabulky. Dřív se přebytečné řádky jen ořízly -
  po zmenšení prvku o polovinu uživatel tiše přišel o půlku řádků.
- **Prázdná tabulka je vidět.** Bez sloupců se dřív nevykreslilo vůbec nic, takže po vložení nebylo
  poznat, kam se kliklo.

**Vzhled sjednocen se zbytkem** (prvek jel jako jediný mimo sdílený plán řádků): mizí svislá čára
navíc na vnějším okraji, která zdvojovala rámeček, a vlastní barva pozadí řádku už **přebíjí**
střídání řádků místo aby se s ním míchala (u průsvitné barvy prosvítala zebra zespodu).

**Tři chyby z prvního proklikání (Jan), všechny opravené:**

- **Nešlo táhnout do výšky.** Prvek se vkládal bez pevné výšky, což z něj dělalo „dynamický" prvek,
  a těm plátno nabízí jen boční úchyty. Vkládá se s výškou (3 řádky = 18 mm), takže se chová jako
  každý jiný prvek. Cena: přidání řádku obsah zmenší místo aby tabulku zvětšilo - dorovná se
  v kroku 2, kde se řádky budou přidávat přímo na plátně.
- **Po natažení do výšky zbylo dole prázdné bílé místo.** Zmenšování obsahu bylo napsané jen jedním
  směrem („nevejde se → zmenši"); opačný případ u ostatních tabulek nenastává, protože jim počet
  řádků plyne z dat. Nově **řádky vyplní výšku prvku**, ale **písmo se přitom nezvětšuje** (roztažený
  prvek má mít vyšší řádky, ne větší text - jako tabulka na snímku v PowerPointu). Zmenšit se písmo
  umí dál. Starých tabulek v režimu „z dat" se roztahování netýká.
- **Mřížka blikala při posunu po plátně** - a byla to obecná chyba obou tabulek, nejen nové.
  Čára tenčí než pixel se **bez vyhlazení** kreslí jen když trefí střed pixelu, takže při posunu
  o zlomek milimetru mizela a zase se objevovala. Naměřeno: z deseti pozic se nevykreslila v šesti.
  JŘ prvky tohle pravidlo („tenká čára → vyhlazovat") mají od začátku, tabulky ne; nově je pero
  mřížky na jednom místě (`GridStyle.CreatePaint`) a používají ho obě tabulky.

**Brány:** build bez chyb, .NET 810 zelených (799 původních + 11 nových na pevnou tabulku). Dva testy
ověřeny mutací, ať netvrdí víc, než měří: vypnutá škála shodí test na zmenšování obsahu, vypnuté
vyhlazení shodí test na mizející mřížku.

**Dál:** krok 2 = zadávání přímo na plátně (dvojklik do buňky a psát, Tab/Enter posun, pravý klik
na vložení řádku a sloupce, šířky tažením) a s ním zredukování dialogu na jediné „Obecné".
Do té doby se obsah zadává dosavadním dialogem sloupců a řádků.


## 2026-08-12 (2) - 🔌 přepínač vlastních sloupců odstraněn, featura svítí všem

**Rozhodnutí Jan:** vypínač `Features.CustomDepartureColumns` pryč, vlastní sloupce vývěsky jsou
zapnuté napořád. Důvod není „už je to otestované", ale **že ten mechanismus u vydané verze nefunguje**.

⚠️ **Poučení, kvůli kterému to sem patří: feature flag ze souboru vedle exe u našeho single-file
publishe nefunguje.** Config se čte přes `AppContext.BaseDirectory`, což u self-extracting bundlu
(`IncludeNativeLibrariesForSelfExtract`) není složka exe, ale rozbalovací složka - takže
`templatedesigner.appsettings.local.json` položený vedle exe se **nikdy nepřečte**. Lokální build
(`bin/Debug`) ho čte v pořádku, proto to vypadalo funkčně. Deklarovaný záměr „jedna vydaná verze
poslouží zákazníkovi zhasnutá a na devu rozsvícená" tedy **nikdy neplatil** a nikdo to neověřil na
publishnutém exe. Kdyby se příště zaváděl další přepínač, musí se ověřit na publishnuté verzi,
nebo číst config podle `Environment.ProcessPath`, ne `BaseDirectory`.

⚠️ **Druhá korekce:** tvrdil jsem Janovi, že publish local.json přepisuje. **Nepřepisuje** - csproj má
`CopyToPublishDirectory: Never` (kopíruje se jen do `bin/`). To nebyla příčina.

**Odstraněno:** `FeatureSettings` + property + `MergeLocal` větev v `AppSettings`, `ShowCustomColumnsTab`
i vazba `Visibility` na tabu, podmínka v `ShouldPrefillDepartureColumns` a test-pojistka
`TabVlastnichSloupcu_JeVychoziZhasnuty` (splnila účel). Vypínač smazán i z lokálního configu
a z `C:\Edison\`.

**Důsledek, který je potřeba vzít v potaz:** featura jde **do příští vydané verze rozsvícená**, i když
ji kolega ještě neproklikal. Proto je novinka zpátky v seedu `plan-a-zmeny.md` (nahrání na wiki až
s vydáním, jako vždy) a roadmapa už nedrží odstavec o odblokování.

Brány: build bez chyb, .NET 1996 zelených (798 TD, o zrušenou pojistku méně), publish do `C:\Edison\`
proběhl - exe z 12. 8. 10:24 nese featuru samo, bez souboru vedle sebe.


## 2026-08-11 (6) - ✅ výchozí systémová šablona i pro agendu MHD

KV_DP chtěl začít zkoušet, ale jediná zabudovaná šablona byla vázaná na agendu AD, takže se jim
v seznamu vůbec nenabídla. Portováno z AD varianty - **rozvržení, styly ani nastavení prvků se
nemění**, liší se jen to, co agenda MHD neposílá.

**Co bylo potřeba změnit** (zbytek je 1:1):

- **`{POZNAMKA_POD_DOPRAVCE}` pryč** - MHD to pole nemá a nevyřešený zástupný text se tiskne
  **doslova**, takže by na vývěsce svítil název pole. Jediná AD-specifická věc, kterou šablona měla.
- **Parametry sestavy ořezané na šest**, které MHD nabízí (chybí rozdělení sloupců, souhrn
  na znamení, skrytí v rozsahu). Je to jen záloha pro běh bez připojení - s Caché se berou z agendy.
- Nový stabilní `id`, aby seeder aktualizoval týž záznam a nezakládal při každém startu nový.

**Co se ověřovalo předem** (proti schématu MHD, ne od oka): datová pole, čtyři kolekce, všech šest
sloupců seznamu zastávek a to, že legenda nemá zapnuté filtry podle kategorií - ty MHD neposílá.

Zabalení nevyžaduje registraci: projekt bere celou složku `SystemTemplates/` zástupným znakem
a seeder nalije do MLINE větve všechno, co v .exe najde. Ověřeno výpisem zdrojů z sestavené
knihovny (obě šablony) i proklikem nad KV_DP.

## 2026-08-11 (5) - 🔍 #12843 bod 6 (kolo místo vlaku) je už opravený - doloženo

Zákazník hlásil, že „prestup na vlak" kreslí jízdní kolo. **Ověřeno na jeho datech a v aktuálním
buildu to neplatí** - legenda kreslí autobus i vlak správně, a to v české i slovenské lokalizaci.

**Proč to vypadalo právě takhle:** přestup na vlak má v číselníku písmeno `L`, ale na pozici `L`
je **v obou variantách symbolového fontu jízdní kolo**. Musí se tedy překládat na pozici `x`
(lokomotiva). Bez překladu vyjede doslova kolo - přesně ten příznak. Překlad se dnes dělá na obou
místech: u značky vedle zastávky i v legendě (podle kategorie `prestup`, kterou agenda posílá).

**Kde se to stalo:** klient testoval verzi **260804**, tedy z okna, ve kterém se ta série oprav
dělala (3.-4. 8.: „přestupy kreslily cizí piktogram", „kolo se tiskne, autobus byl vagon",
„překlad glyfu podle kategorie, ne podle písmene"). Odepsat mu, ať to zkusí na nové verzi.

**Vedlejší nález - varianty fontu se liší ve třech pozicích** (vykresleno a porovnáno): `na znamení`
je česky **křížek**, slovensky **dlaň**; pozice `w` je česky **vagon**, slovensky **autobus**;
`x` je vlak v obou, jen jinou kresbou. U přestupů to nevadí (`z` i `x` znamenají v obou totéž),
ale kdyby se někdy překládala pozice `D` nebo `w`, **musí to jít přes jazyk** - jinak vyjede
v jedné zemi cizí piktogram.

**Kde to zkoušet:** `SKNI_MHDNITRA`, linka 0000001, první zastávka „Nitra, Železničná stanica"
(atributy `TAL` = tučně + autobus + vlak). Firma přidána do dev přepínače - je to jediná
v seznamu se slovenskými daty a s vyplněnými per-linkovými seznamy „nevypisovat značku".

## 2026-08-11 (4) - ✅ #12843 bod 9: ikona do záhlaví sloupce hodin

Zákazník má na vývěsce v záhlaví hodin obrázek hodin a přes samostatný obrázkový prvek ho tam
nedostal - sloupec hodin se opakuje u každé skupiny dnů, takže by musel ručně umístit tři obrázky
a hlídat je při každé změně rozvržení.

Prvek odjezdů má nově v tabu Obecné pole **„Obrázek v záhlaví hodin"**. Vykreslí se **v každé
skupině dnů** sám, vejde se celý, vystředěný, s okrajem, a škáluje se s buňkou. **Když je obrázek
nastavený, nadpis sloupce se netiskne** - dvě věci v jedné buňce by se překryly a na jejich vývěsce
tam žádný text není; vymazáním obrázku se nadpis vrátí.

Zatím **jeden obrázek pro všechny skupiny** (rozhodnutí Jan: nedělat rovnou per skupinu). Rozšíření
je aditivní, kdyby to někdo chtěl jinak.

Dekódování obrázku z Base64 (s cache) vytaženo do sdílené pomůcky a používá ho i obrázkový prvek -
jinak by v projektu byly dvě cesty k témuž a jedna z nich by časem zaostala.

⚠️ **Past, na kterou jsem šlápl:** buňka záhlaví se počítá v **milimetrech**, plátno kreslí
v **pixelech**. Bez převodu se obrázek vykreslil mimo záběr a vypadalo to, že se hodnota neuložila.
Komentář u metody to teď říká.

Ověřeno proklikem (Jan, 11. 8.). xUnit 799, celkem 1 996.

## 2026-08-11 (3) - ✅ #12843: šipka vlevo si přestala lehat na jízdní doby + barva písma aktuální zastávky

Dva body z podnětů TRANSDEV (Šimko, testoval verzi ze 4. 8. na šabloně „Nitra").

**Šipka směru jízdy vlevo měla pevné odsazení do cizího sloupce.** Kreslila se 1,5 mm nalevo od
sloupce zastávek, tedy **do prostoru sloupce s jízdními dobami**, a spoléhala na to, že tam je
prázdný pravý okraj. Při výchozí šířce to sedělo, po zúžení prvku si lehla přes čísla. Vpravo se
přitom kreslí uvnitř sloupce a rezervu na sebe má - byla to asymetrie, ne záměr.

Nově je pruh pro šipku **součástí rozdělení šířky**: odečte se z šířky prvku, zbytek se rozdělí
mezi sloupce a před sloupec zastávek se vloží prázdné místo. Soused se do něj nedostane bez ohledu
na šířku prvku. Svislice mřížky zůstává na začátku pruhu (kde soused končí), buňka za pruhem si
svou vlastní potlačí, aby tam nebyly dvě. Vpravo se nezměnilo nic - pruh se počítá jen pro levou.

Vedlejší oprava: se sloupcem zastávek na první pozici se šipka dosud kreslila mimo prvek a ořízla
se; teď má pruh i tam.

**Barva písma aktuální zastávky** dosud nešla nastavit - pozadí, podtržení, tučné a velikost ano,
barva písma ne. Doplněná do panelu i do formuláře; prázdná = dědí se z buňky, takže existující
šablony vypadají stejně.

Ověřeno proklikem (Jan, 11. 8.) - render se testy nechytá. xUnit 799, celkem 1 996.

## 2026-08-11 (2) - ✅ `B51` seznam se plní najednou, ne po jednom řádku

Seznamy v dialozích (šablony, instance) se plnily řádek po řádku, takže dvě stě řádků znamenalo
dvě stě přepočtů navázaného pohledu. **Vidět to bylo jako škubnutí**, když načtení dosedlo, zatímco
uživatel psal do filtru skupiny. Nález Jan 4. 8.; odkrylo to až async načítání pickeru - dřív to
schoval zámrz synchronního načítání.

Obsah se teď vymění najednou a pošle se **jediné** oznámení - a je to `Reset`, který pohled
dostával i dosud z `Clear`, takže pro něj nejde o nový stav.

⚠️ **Vědomě to není `DeferRefresh`.** Ten se na tohle zkoušel 4. 8. a hned se vracel: odklad
neodkládá oznámení o změně kolekce, jen přepočet pohledu, takže mutace zdroje pod ním shodí
`ListCollectionView` na currency (spadlo to při výběru linky). Komentář u kódu drží důvod,
proč to znovu nezkoušet.

**Past, na kterou jsem cestou skoro šlápl:** parsování řádků je líný iterátor. Projít ho dvakrát -
jednou kvůli odběru změn, podruhé kvůli naplnění - by vyrobilo dvě různé sady objektů a odběr by
visel na řádcích, které v seznamu nejsou. Musí se zhmotnit předem.

**Ověřeno proklikem** (Jan, 11. 8.) - sdílená komponenta, dotýká se i EDISONMAIN2, a testy tuhle
třídu chyb nechytí. EDControls 647 (+5), celkem 1 996.

## 2026-08-11 (1) - 🔌 vlastní sloupce vývěsky jdou do verze zhasnuté

Rozhodnutí Jan: `B39` (#13045b) se do zítřejší ostré verze dostane, ale uživatel se k ní zatím
nedostane - nejdřív si to má proklikat kolega na dev.

Vypínač je klíč `Features.CustomDepartureColumns` v **`templatedesigner.appsettings.local.json`**
vedle exe (per-user override, gitignored). **Jedna vydaná verze tak poslouží obojímu** - zákazník
ji má zhasnutou, kdo testuje, si vedle exe položí soubor a má ji rozsvícenou. Bez druhého buildu.

```json
{ "Features": { "CustomDepartureColumns": true } }
```

⚠️ **Schovat jen tab v dialogu by nestačilo.** První prvek odjezdů vložený do šablony zastávkového
JŘ AD si výchozí vzor sloupců nastavuje **sám** (rozhodnutí z etapy 1) - uživatel by tedy dostal
šablonu jedoucí novou datovou cestou, aniž by ji viděl nebo mohl vypnout. Vypínač proto zhasíná
**obojí**: tab i to předvyplnění.

Existující definice se skrytím tabu **nemažou** - dialog je načte a OK vrátí beze změny. Serverová
strana je zhasnutím nedotčená a je zpětně kompatibilní sama o sobě: bez definice se chová přesně
jako dřív (drží to zlaté odtisky i živá parita vzorů).

Připnuto testem (`TabVlastnichSloupcu_JeVychoziZhasnuty`) - až featuru testování odblokuje, test
spadne a je to signál flag odstranit, ne ho jen přepnout.

## 2026-08-10 (12) - ✅ `B39` etapa 2: sloupec vývěsky jde omezit obdobím

Tím je `B39` (a s ním tiket #13045b) hotový. Brány: build bez chyb, xUnit 798/798 (+26),
Caché 89/89 (+3), živá parita vzorů prošla.

**Co uživatel dostal:** u vlastního sloupce třetí podmínka **„Období"** - seznam rozsahů dat
(„14.2.-18.2., 1.7.-31.8., 29.10.-30.10."). Datum **bez roku platí každý rok**, takže šablona
přežije překlopení jízdního řádku; datum **s rokem** platí jednou (výluka, mimořádný provoz).
Do sloupce patří spoj, který v období jede aspoň jeden den. **Kdo jede celé období, přijde
o negativní značku** - nemá co vysvětlovat; kdo jen část („v létě nejede, na podzim ano"),
značku si nechá. Zákazníkovy „dny školního vyučování" nepotřebují nic navíc - zadají se
jako rozsahy, protože v datech AD žádný pojem „školní den" není (dopravní kalendář zná jen
den v týdnu, svátek a sudý/lichý týden; slovo „školní" se v poznámkách 600 linek nevyskytuje).

**Kdo rozhoduje, kdy spoj jede:** ne my. Vyhodnocení si vzala existující funkce EDISONu
(`ALVA.ISY.ISYLinky.API.LinkyStatistika.jede`) - vrátí pro každý spoj **denní bitmapu** přes zadané
období a řeší v ní datumová omezení, svátky, sudé/liché týdny i platnost linky. Voláme ji **jednou
na linku** přes souhrn všech rozsahů (cache v `^||TiskAd`), sloupec je pak jen výřez z bitmapy;
365 dní × 22 spojů stojí 0,013 s. Linku předáváme globálem - přes `id` by ji `cisloLinkyDoIndexu`
převedla podruhé.

**Past, kterou to odhalilo:** šablona s pevným datem („27.12.2026") u jízdního řádu platného
2022-2023 vrátí u **všech** spojů „jede 0 dní" a sloupec zmlkne. Proto sloupec, jemuž po ořezu na
platnost nezbyl žádný rozsah, **nebere nic a hlásí to** (`OBDOBI_MIMO_PLATNOST` → oranžový badge
i dovětek u exportu PDF). Bez toho by vývěska tvrdila, že o Vánocích jedou všechny spoje.

**Denní značky - rozhodnutí Jan po měření.** Sloupec, který slije víc druhů dne, musí jít rozlišit
(spoj jedoucí jen v sobotu vypadá ve sloupci „So + Ne" stejně jako víkendový) a **negativní značka
to nahradit nemůže** - nese datumové omezení, ne den v týdnu. Nové pravidlo („značka jen když spoj
nepokrývá celý sloupec", a tiskne se **jen den, který sloupec obsahuje**) se proto zavádí
**jen u kombinací, které dnešní rozvržení nezná**. Standardní sloupce zůstávají beze změny: obecné
pravidlo by v „Neděle a svátky" označkovalo **6 269 ze 7 295 spojů (86 %)** a ve slitém víkendu
4 386 z 8 213 (53 %) - značka na většině řádků nerozlišuje nic a je hlavně příznakem, že sloupec
neodpovídá datům (v CB_CSADAD skoro žádný nedělní spoj o svátcích nejede). Komu to vadí, ať si
sloupec rozdělí na „Neděle" a „Svátky" - od etapy 1 to jde. Konstrukcí přitom **nemůže nastat
chyba `B29`**: značka nikdy nepojmenuje den, který ve sloupci není.

**Dluh z etapy 1 splacen:** vzory sloupců staví server (`PresetySloupcu`, pole `PRESETY_SLOUPCU`)
a klient je jen čte. Paritní test tím měří to, co editor reálně pošle, ne ruční kopii pravidel;
bez načtených dat se vzory nenabízejí, protože prázdninový vzor postavený naslepo by neseděl na
značky konkrétní linky.

**Nález z prokliku (Jan, KOVED linka 824520): potlačení značky se nikdy nezapínalo.** Pokrytí období
se počítalo přes **všechny kalendářní dny**, takže pracovní spoj podmínku „jede celé období" nesplnil
nikdy - o víkendu nejede. Zákazníkův hlavní požadavek tím byl fakticky mrtvý (na živých datech
nesplnil „jede celé období" ani jeden z 55 spojů linky). Nově se pokrytí počítá **jen přes dny, které
sloupec pokrývá**; svátek se počítá jen sloupci, který ho vyjmenoval, aby „Pracovní dny" netvrdily,
že pokrývají 1. květen. Doloženo: na téže lince po opravě mizí značka 18 u spojů jedoucích celé
prázdniny (11 odjezdů na prvních 8 stránkách), předtím nemizela žádná.

**Nález z prokliku (Jan, KOVED linka 802192):** hláška o nezařazených spojích počítala **odjezdy,
ne spoje**. Tentýž spoj vypadne na každé zastávce linky zvlášť, takže tři nezařazené spoje (10, 23,
38) hlásila jako „31 odjezdů" - číslo vypadalo desetkrát hůř, než jaká byla skutečnost, a nešlo
z něj poznat, že jde pořád o totéž. Server nově posílá i čísla spojů (`NEZARAZENE_SPOJE_CISLA`),
klient podle nich počítá spoje a časy nechává jako ukázku, kde je hledat. Bez čísel (starší server)
se počítají časy jako dřív, hláška tedy nezmizí.

⚠️ **Souvislost, která z toho plyne:** stránka vývěsky je jedna zastávka × směr, ale hláška sčítá
celou linku. Že se na právě zobrazené zastávce nic nezmění, zatímco hláška naskočí, je normální -
dotčená zastávka může být kterákoli z padesáti.

**Ověřeno voláním na živých datech** (CB_CSADAD, linka 0365001): sloupec s obdobím „1.7.2023"
(sobota) vzal 5 z 9 odjezdů, sloupec bez podmínky 9; u odjezdu 17:30 zmizela značka 29. Období
„27.12.2026" nechalo sloupec prázdný a stránka nese hlášku. Gold digesty přebaselinovány - **jen
md5**, počty stránek/zastávek/odjezdů/poznámek beze změny (přibylo pole `PRESETY_SLOUPCU`).

## 2026-08-10 (11) - 🧭 `B39` etapa 2: dvě odpovědi od kolegy + upřesnění od zákazníka

Tři nové vstupy (Jan, po implementaci etapy 1) - **nic z toho nemění etapu 1**, všechno se týká
etapy 2 (období):

1. **Prefix `k@`/`L@` je legacy věc - neřešit** (kolega). Dnešní zahazování při formátování textu
   je správně; konzervativní pravidlo „poznámka s prefixem → spoj zařadit" v etapě 2 zůstává,
   ale už není otevřená otázka. Měření výskytu `@` na živých datech tím odpadá.
2. **Vyhodnocovač „kdy spoj skutečně jede" v EDISONu EXISTUJE** (kolega): legacy
   `$$mkStaLin^JRapi(...)` a nová `ALVA.ISY.ISYLinky.API.LinkyStatistika` (metoda „jede");
   statistiky dnes jedou na staré, nové funkce (oběhy) už na nové - **přecházet na novou**.
   Tím **zlevňuje varianta (b)** („spoj v období skutečně jede"), kterou analýza odhadla jako
   vlastní projekt kvůli chybějícímu kalendáři - kalendář/vyhodnocení drží ISY. Před návrhem
   etapy 2 prověřit signaturu a co přesně vrací.
3. **Zákaznický mail (#13045) upřesňuje očekávání od období:** sloupec má umět **víc rozsahů dat**
   („14.2.-18.2., 1.7.-31.8., 29.10.-30.10."); „dny školního vyučování" = tentýž mechanismus;
   a **auto-potlačení negativní značky**, když se omezení spoje kryje s obdobím sloupce celé
   (značka se ukáže jen u spoje s výjimkou uvnitř období - „v létě nejede, na podzim ano").
   To je silnější než ruční per-sloupcový seznam „nevypisovat" z etapy 1 (ten zůstává) a stojí
   na vyhodnocení z bodu 2.

## 2026-08-10 (10) - 🚧 `B39` etapa 1 implementovaná: vlastní sloupce vývěsky (dny + značky)

**Nezacommitováno - blok L čeká na proklik Jana** (viz klikací cesty v reportu/žurnálu). Všechny
brány zelené: build bez chyb, xUnit 772/772 (+10), Caché 86/86 (+1), **živá parita presetů prošla**.

**Co uživatel dostal:** dialog odjezdů má třetí tab **„Definice sloupců"** - přepínač
Automaticky (dle linky) / Vlastní sloupce, až 5 sloupců, u každého nadpis (dvouřádkový), dny
(Po-Ne + Svátek), značky „jen s těmito" / „bez těchto" a per-sloupcové „nevypisovat značky"
(přestěhování `B63` z linky na sloupec). Čtyři dnešní rozvržení jako vzory jedním klikem -
prázdninové se předvyplní čísly značek z konfigurace aktuální linky (nová pole `NZ_KONFIG`
+ `NEVYPISOVAT_ZNACKY_SLOUPCU` ve values). Nepokryté spoje hlásí oranžový badge ve status baru
(konkrétní minuty + tooltip s cestou) a dovětek hlášky exportu PDF. Nový prvek odjezdů z palety
začíná se vzorem Prac/So/Ne (rozhodnutí Jan: návrhář je primární); PDF import zůstává na
Automaticky. `TR10` (skrývání víkendových spojů) tím je pokryté vylučovacím seznamem.

**Technicky:** definice žije na prvku (`CustomColumns`, aditivní JSON - bez bumpu verze dle
konvence, starší build ji ignoruje = přesně backup chování). Na server jde jako JSON parametr
`DEFINICE_SLOUPCU` (infrastrukturní klíč, ne parametr sestavy) skládaný VŽDY čerstvě ze šablony
v `ComposeGetDataParams` - nikdy přes `_previewParams`, aby ho nepřepsala persistovaná hodnota
(třída chyb `B24`). Data nově závisí na obsahu šablony → `RefetchIfColumnsDefinitionChanged`
(dialog OK, undo, redo) porovnává snapshot definice a přenačítá. Server: druhý režim zařazování
(`ParseDefiniceSloupcu`, `ZarazeniSpojeDleDefinice`, `DenniZnackyDleDefinice`,
`ZnackaSkrytaDleDefinice`, `NEZARAZENE_SPOJE`); dnešní kód je nedotčený režim „automaticky".
`X` v platnosti = pracovní dny 1-5; nerozpoznaná platnost → spoj se nikam nezařadí (hlásí se,
legacy ho tiše zahazovala se stejným výsledkem tisku).

**Pojistky:** `ZastavkoveJrAdDefiniceParitaTest` - **preset postavený z konfigurace linky dává na
živých datech (CB_CSADAD gold linky) bajtově stejný výstup jako režim automaticky** (digest celého
GetData). K tomu jednotkové paritní testy napříč FTL × platnost × značky. Gold digesty
přebaselinované (jen md5 - dvě nová pole ve values; počty beze změny). Dvě vědomé odchylky od
legacy, obě dokumentované testem: prázdninové rozvržení bez NZ konfigurace dává pracovní spoj do
obou pracovních sloupců (legacy jen do 1.); víkendové denní značky se u netypických kombinací dnů
(např. sloupec jen So+Ne) netisknou konzervativně jako legacy.

**Doladěno po prokliku (Jan, 10. 8.):**

- Tab se jmenuje **„Vlastní definice sloupců"**; rozdělení vs. tab Sloupce je vědomý atyp -
  definice je **zdroj** sloupců (jako nastavení linky v automatice), tab Sloupce zůstává
  **zobrazovací vrstva** (přepis nadpisu / viditelnost / pořadí / styl) a přepis vyhrává nad
  zdrojem - totéž pravidlo jako u sloupců z linky, žádná synchronizace není potřeba.
  Sloučení tabů zavrženo (přidávání sloupců by bylo atyp ve sdíleném vzoru tabulkových dialogů).
- Combo vzorů **předvybere vzor, kterému aktuální sloupce odpovídají** - prázdné combo vypadalo
  jako neuložený vzor (stav nese seznam sloupců, ne combo).
- Pátá volba **„Prázdná (sloupce si přidáte sami)"** - stavba od nuly bez vzoru.
- **Validace seznamů značek** při OK (jen čísla oddělená čárkou) - „10;20" by server tiše přečetl
  jako samotnou 10, „abc" jako nulu; teď dialog nezavře a jmenuje chybný sloupec.
- Tooltip **„Negativní značky"**: číslo přiděluje JŘ spolu s datovým omezením; vývěska filtruje
  podle čísla jako faktu a platnost nevyhodnocuje (to je etapa 2).
- Nový sloupec **nezdědí styl po smazaném** (uvolněná id s uloženým stylem se přeskakují).
- Kombinace podmínek zůstávají vědomě volné (táž značka v „jen" i „bez" = prázdný sloupec;
  díru hlásí upozornění). Parametr sestavy „Rozdělení sloupců" se při aktivní definici neuplatní
  (definice přebíjí linku celou) - případné schování parametru je kosmetika na později.

**Zapracované nálezy z code review (před commitem):**

- **Sloupec bez zaškrtnutých dnů neomezoval podle kontraktu, ale vyřazoval** - spoj s omezenou
  platností do něj nikdy nespadl (sloupec „jen značka X, kterýkoli den" nešel postavit). Opraveno
  + test.
- **Zastávka, jejíž všechny odjezdy spadly mimo sloupce, zmizela i s varováním** - prázdná stránka
  se zahodila dřív, než se zapsalo NEZARAZENE_SPOJE. Teď stránka vyjde (prázdná + varování).
- **Enter v dialogu ztrácel rozepsaný text** (pole ukládala až při opuštění, OK je výchozí
  tlačítko) - pole definice ukládají průběžně.
- **Validace značek chytá i mezeru místo čárky** („52 53" by server přečetl jen jako 52).
- **Re-fetch při změně definice jde přes centrální vykonání příkazů** - chytá i přidání/smazání/
  vložení prvku odjezdů, ne jen dialog a undo/redo. Snapshot definice se zapisuje až po
  ÚSPĚŠNÉM načtení dat a souběžné fetche kryje generace (spadlý/pomalý fetch nezamaskuje
  potřebu obnovy).
- **Výchozí vzor dostává jen PRVNÍ prvek odjezdů v šabloně a jen u zastávkového JŘ AD** - druhý
  prvek by definicí tiše přepnul data celé šablony a u MHD by dialog tvrdil vlastní sloupce,
  které její server nečte.
- Oranžový badge se čistí při zahození dat (přepnutí agendy/instance) a jeho tooltip vede na
  správný tab („Vlastní definice sloupců").
- Dluh do etapy 2: paritní Caché test staví preset vlastním zrcadlem `DepartureColumnPresets.Build`
  (ručně synchronizované kopie) - při rozšíření presetů sjednotit zdroj, jinak parita hlídá zrcadlo,
  ne to, co klient reálně posílá.

## 2026-08-10 (9) - 🧭 `B39` rozhodnuto: vlastní sloupce vývěsky, zadání ve dvou etapách

Domluva Jan + kolega dokončena - pět otázek z (6) je zodpovězených, blok přechází z „domluva" na
„implementace". Rozhodnutí:

- **Kde žije:** definice sloupců v šabloně; edituje se v dialogu sloupců **prvku mřížky odjezdů**.
  **Jedna definice na šablonu** - ovlivňuje data celé vývěsky včetně legendy, ne jen prvek; kdyby
  šablona měla prvků odjezdů víc, sdílí ji.
- **Přednost (backup režim):** šablona s definicí → linkové seznamy (`MHD_NENZ1_NENZ2`,
  `MHD_NEZNZ123`) se ignorují **celé**; šablona bez definice → dnešní chování beze změny (Nitra se
  nezmění ani o pixel). **Nemíchat** - žádné „dny ze šablony, značky z linky". Nové šablony
  předvyplní výchozí vzor (Prac / So / Ne+svátky), takže se linkové zadání opouští přirozeně,
  bez jednorázové migrace; nová JŘ ho už nabízet nemusí, číst ho ale musíme umět napořád.
- **Model sloupce:** max 5 sloupců, u každého kaskáda podmínek **dny → značky → období**; značky
  jako **dva seznamy** (jen s těmito / bez těchto - vylučovací je podmínka pohlcení `TR10`)
  + per-sloupec seznam **„nevypisovat značku"** (přestěhování `B63` z linky na sloupec).
  Nevyplněná podmínka neomezuje. Překryvy sloupců jsou legitimní (jsou i dnes) - pojistka se staví
  jen na díry.
- **Díra v pokrytí:** viditelné upozornění „N spojů nespadá do žádného sloupce" (a které)
  v návrháři **i při tisku**, ne tvrdý zákaz. Kontrola jde jen nad načtenými daty, proto musí být
  i v tiskové cestě - šablona bez díry u linky A může mít díru u linky B.
- **`X` v týdenní platnosti** = legislativní zkratka pracovních dnů (kolega) → při vyhodnocování
  pravidla se počítá jako 1-5.
- **Číslování negativních značek je konzistentní per skupina linek** (kolega) → konkrétní čísla
  značek v šabloně jsou v pořádku, odkaz „přes roli" není potřeba. Pojistka proti použití šablony
  nad jinou skupinou: editor u zadané značky zobrazí její význam z aktuálních dat.

**Etapa 1 (bez otevřených otázek): dny + značky.** Editor sloupců, presety čtyř dnešních rozvržení,
upozornění na nepokryté spoje, JSON šablony verze + migrace (R5), serverový druhý režim zařazování
(dnešní kód zůstává jako režim „automaticky"), definice do `GetData` jako JSON parametr
(allowlist `ServerSideParams`) a **re-fetch při úpravě sloupců** - data nově závisí na obsahu
šablony, to je jediná nová vazba. Testy: zlaté odtisky beze změny (kryjí backup režim) + „preset
přes nový režim == dnešní výstup" na živých datech.

**Etapa 2 (blokovaná na business odpovědích): období.** Navržená pravidla čekají na odkývání:
„jede jen" = patří, jen když se data protínají s obdobím; „nejede" vyřazuje **jen při pokrytí
celého** období sloupce; „jede"/„jede také" **nikdy nevyřazují** (jen přidávají jízdy → pro
zařazení se ignorují, do legendy jdou jako dnes); sudé/liché týdny neumíme rozhodnout → zařadit
a nechat na uživateli; víc poznámek naráz = platí všechny zároveň (potvrzeno). Otevřené: význam
prefixu `k@`/`L@` v datových omezeních (kolega neví - dohledat; do té doby konzervativně „neumíme
rozhodnout → zařadit", předem změřit výskyt na živých datech). Rozdíl „jede" vs. „jede také" **není
blokující** - pravidlo „nikdy nevyřazuje" ho obchází; bude potřeba, až kdyby „jede také" mělo spoj
do sloupce přitahovat.

**Rozsah:** jen AD. MHD má vlastní typy provozu a nemění se (rozhodnutí Jan).

## 2026-08-10 (8) - Test závislý na stroji už neprojde do repa

Návazně na opt-in baseline níže: aby se opakování téhož nespoléhalo na to, že si na to někdo vzpomene,
hlídá to teď test (`MachineLocalStateGuardTests`). Projde zdrojáky **všech `*.Tests` projektů** v repu
(kolegovi je jedno, který projekt mu sadu shodí) a shodí sadu, když test sahá na soubor mimo repozitář
pod **stálým jménem**, nebo do uživatelského profilu (`GetFolderPath`, `%APPDATA%`…). Unikátní jméno
na běh (`Guid.NewGuid()`) je v pořádku - takový soubor mezi běhy nezůstane a nezestárne.

**Proč to autor sám nepozná:** `pre-push` pouští sadu na jeho stroji, kde artefakt existuje a je
svěží, takže je tam zeleno vždy. CI, které by sadu pustilo na čistém stroji, nemáme - je to tedy
principiálně nevidět, dokud na to nenarazí kolega.

**Ověřeno mutací, ne jen zelenou sadou:** dočasný test s pevnou cestou do `%TEMP%` sadu shodil
a hláška ukázala soubor i řádek. Mutace zároveň odhalila, že první verze hlásila tři řádky místo
jednoho (okno příkazu se otevíralo i na `{`) - zúženo na řádky se spouštěčem. Detekce má vlastní
ověření (rozlomený `Path.Combine`, komentář se nepočítá) a výjimka se musí obhájit: `Vyjimky_JsouStalePotreba`
shodí sadu, když výjimka přestane být potřeba - jinak by mlčky kryla nový prohřešek ve stejném souboru.

Jediná dnešní výjimka je baseline odtisků (opt-in, viz níže). Zbytek repa je čistý: ostatní testy, které
píšou do tempu, používají jméno s `Guid.NewGuid()`, a předvolba fontu v EDControls si cestu injektuje.

**Co to nezachytí:** závislost na fontech z OS, na síti / dev serveru a na obsahu proměnných prostředí.
Na tu třídu je jediná skutečná pojistka běh na čistém stroji, tedy CI. Pravidlo je v `architecture-rules.md`
jako 5. „must not happen" scénář. Sada 762/762 (+11).

## 2026-08-10 (7) - Porovnání odtisků kreslení proti baseline je opt-in

Test `Odtisky_SouhlasiSBaseline` padal kolegovi na sadě, i když byl kód v pořádku. Baseline odtisků
je vázaná na stroj (nemáme zabalené textové fonty, text kreslí Arial z OS), takže **není v gitu** -
ale test si ji při prvním běhu sejmul komukoli do `%TEMP%` a po **první zamýšlené** změně kreslení
začal hlásit „kreslení se rozešlo". Hláška přitom o ničem reálném nevypovídala a nešlo z ní poznat,
že jde o zastaralý soubor v tempu.

Porovnání se teď provede jen s `TD_RENDER_BASELINE=1` - bez proměnné se test souboru vůbec nedotkne.
Nástroj tím zůstává použitelný pro refaktory kreslicí cesty (`B13`), ale nemůže nikomu padnout.
Ověřeno obojí: bez proměnné projde i nad úmyslně rozbitou baseline, s proměnnou nad ní padne.
Zbylé čtyři odtiskové testy (determinismus + citlivost na 0,1 mm a 0,5 pt) jsou na stroji nezávislé
a zůstávají. Harness sám odebrat nelze - `RenderSnapshot` a fixtury používají další tři testy.

## 2026-08-10 (6) - ✅ `B63`: značky, které si zákazník u linky vypnul, se přestaly tisknout

Vývěska teď respektuje per-linkový seznam **„nevypisovat značku ve sloupci"** (`MHD_NEZNZ123`,
4 sloty = 4 sloupce). Doteď jsme ho ignorovali, takže se u minut tiskly značky, které si zákazník
vypnul, a byly i v legendě.

**Doloženo před/po na živých datech:** `SKNI_MHDNITRA`, linka 0000001 (konfigurace `52~52~53,54,55~`)
měla značku 52 u **34 ze 47 odjezdů** - po opravě **0×** a zmizela i z legendy. „Před" jsem změřil
mutací (potlačení vypnuté), aby bylo vidět, že to dělá ta nová větev, ne shoda dat.

**Legenda se zapisuje až u sloupce, kde se značka reálně tiskne** - potlačená ve všech sloupcích spoje
znamená, že v legendě nemá co dělat (1:1 legacy `noNZ2`). Nemíchat s `MHD_NENZ1_NENZ2`, který skrývá
**celý spoj** z pracovních sloupců a má navíc legacy sloty prohozené.

⚠️ **České firmy to vyplněné nemají** (`CB_CSADAD` 0 z 31 216 linek, `ZL_KOVED` 0 z 17 029) - proto
zlaté odtisky nehnuly a proto to nikdo nenahlásil. Používá to slovenská MHD Nitra, tam na každé lince.

**Poučení (podruhé v téhle session):** `.cls` upravená skriptem se **nenasadí**, hook reaguje jen na
Edit/Write. Runner hlásil 74/74 a můj nový test vůbec neběžel; poznal jsem to jen podle nezměněného
počtu testů. Když počet testů po přidání testu neroste, není zelená zelená.

### Pro `B39` (vlastní skupiny dnů) je potřeba nejdřív domluva - pět otázek

1. **Kde nastavení žije** - šablona, nebo linka? Rozsahy dnů jsou na lince nezávislé (→ šablona),
   u seznamu značek to není jisté: čísla se přidělují firemně, ale význam plyne z omezení spojů.
2. **Může sloupec kombinovat obojí** (seznam značek i rozsah dnů), nebo vždy jen jedno pravidlo?
3. **Spoj, který nesedí do žádného sloupce** (nebo naopak do dvou) - co s ním?
4. **Svátky** - podle jakého kalendáře, a platí i pro vlastní skupiny?
5. **Zpětná kompatibilita** - dnešní pevná schémata se musí stát presety, jinak neprojdou zlaté odtisky.
6. **Kam se přesunou poziční seznamy značek** (doplněno 10. 8. po `B63`): `B63` i `TR10` stojí na
   per-linkových seznamech, kde **slot N = sloupec N**. To drží jen u dnešních čtyř pevných rozvržení -
   s vlastními sloupci index přestane být stabilní. Domluva Jan + kolega: **přesunout je do návrháře**,
   aby si každý sloupec nesl svůj seznam potlačených i vylučovaných značek. Zbývá rozhodnout, co
   s existující konfigurací u linek (MHD Nitra ji má vyplněnou na každé lince) - záskok, migrace, nebo obojí.

Sloupec potřebuje **tři nezávislé knoflíky**, které se nesmí slít: zahrnutí (kdo do sloupce patří),
vyloučení (koho vyhodit, i když by patřil - to je `TR10`) a potlačení značky (`B63`, hotové).
Skutečná práce je varianta „rozsah dnů" - potřebuje **vyhodnocovač „jede spoj v datu X"**, který
dnes nemáme (omezení spoje umíme jen formátovat do textu).


## 2026-08-10 (5) - ✅ `B64` uzavřeno: „<1" bez přepínače, sloupce variant dědí celý styl

**Přepínač „Zarovnat číslice pod sebe" zrušen** (rozhodnutí Jan po vidění odtisku) - visící „<"
je jediné chování. Formulář zastávek má z `B64` jedinou položku, `Jízda pod minutu jako <1`.

**Dědění nastavení na varianty tras dotaženo - moje první oprava byla k ničemu.** Zdědil jsem jen
`Alignment`, ale dialog sloupců píše zarovnání (vodorovné **i svislé**) do `Style.Data`. Jan zkoušel
měnit zarovnání a nic se nehýbalo, což to odhalilo. Dědí se proto **celý per-sloupcový styl**
(font, barvy, vnitřní okraje, obě zarovnání) i `Alignment`; šířku a hlavičku si varianty určují samy.

**Poučení:** dvě cesty k témuž nastavení znamenají, že oprava jedné z nich nemusí být vidět - před
tvrzením „opraveno" ověřit tou cestou, kterou uživatel reálně klikne. Minule jsem tuhle druhou cestu
sám pojmenoval jako „per-sloupcový styl se nededí" a odložil ji, přitom to byla ta používaná.

Ověřeno: 12 testů, mutace dědění stylu test shodí. Nasazená třída ověřena voláním `Init` (pole 41
`ShowSubMinuteTravel`, `AlignSubMinuteDigits` už tam není). .NET 1885 zelených.


## 2026-08-10 (4) - 🎯 „<1" zarovnává číslice; zarovnání sloupce se dědí na varianty tras

Vizuální kontrola `B64` (Jan) našla dvě věci.

**„<1" odsadilo svou číslici doprava.** Zarovnání sloupce vpravo to řeší jen zdánlivě - pak odskočí
jednociferné časy místo dvouciferných („9" vedle „11"). Prvek proto **odsadí ostatní čísla o šířku
znaku „<"** (měřeno z reálného fontu, ne odhadem), takže číslice stojí ve svislici a „<" z ní
vystupuje doleva. Přepínač **„Zarovnat číslice pod sebe"** (výchozí zapnuto, hned pod „<1"
ve formuláři zastávek) to umí vrátit na obyčejný text.

Odsazuje se **jen při zarovnání vlevo** (vpravo se číslice srovnají samy a odsazení by je rozhodilo)
a **jen ve sloupci, kde „<1" reálně je** - bez jízdy pod minutu vypadá vývěska jako dřív.

**Zarovnání sloupce „Čas" se nepropisovalo na varianty tras** (nález Jan, opraveno). Rozpad na
variantní sloupce je vytvářel od nuly, takže u linky s víc variantami se nastavené zarovnání **tiše
zahodilo** a podsloupce se kreslily vlevo. Nově se dědí. ⚠️ **Per-sloupcový styl (font, barvy,
vnitřní okraje) se pořád nededí** - stejný vzorec, ale jeho oprava změní vzhled zákazníkům, kteří
styl na „Čase" nastavený mají; proto zvlášť.

Ověřeno: 13 testů (5 nových), **obě mutace je zabily** (vynechané dědění zarovnání i ignorovaný
přepínač). Odtisk tří variant vedle sebe rozhodl vizuál. Nasazená třída ověřena voláním `Init` -
pole 42 `AlignSubMinuteDigits`, default 1. Caché 74/74, .NET 1886.


## 2026-08-10 (3) - ✅ `B64`: krátkou jízdní dobu jde vytisknout jako „<1"

Prvek Tabulka zastávek má volbu **„Jízda pod minutu jako <1"** (výchozí vypnuto). Jede-li se na
následující zastávku pod minutu, vypíše se ve sloupci časů „<1" místo prázdna. TRANSDEV `TR8`.

**Netriviální část je rozlišení „jízda pod minutu" od „chybí údaj"** - obojí je v datech nula.
Časy jsou kumulativní od vývěskové zastávky, takže nula pro *jízdu* může být jen v souvislém běhu
hned za ní; nula za prvním nenulovým časem znamená, že tu zastávku reprezentativní spoj neobsluhuje.
Značí se proto jen ten běh a první reálný čas ho ukončí. Vývěsková zastávka a projeté značku nedostanou.
Variantní sloupce mají každý vlastní běh, „<" (varianta tu nestaví) běh neovlivní.

Ověřeno 8 testy; **obě mutace implementace je zabily** (záměna `break` za `continue` = 1 pád,
vynechané přeskočení vývěskové zastávky = 5 pádů). Drift brána pole vidí. Nasazená třída ověřena
voláním `Init` (server vydává `ShowSubMinuteTravel`, default 0). Caché 74/74, .NET 1881 zelených.

Seedy aktualizované: uživatelská příručka a Novinky (obojí připravené k nahrání, ne nahrané).


## 2026-08-10 (2) - 🎯 „<1" je naše práce (`B64`), obrácené poznámkování berou kolegové

Dvě z nepokrytých věcí se posunuly, obě po rozhovoru Jana s kolegy.

**`TR8` „<1" místo „0" - premisa vyjasněná, kolegy k tomu nepotřebujeme.** Jan přišel s tím, že
zákazník musí mít na mysli **první úsek**, protože nikde jinde to nedává smysl. Ověřeno v kódu a platí:
doba jízdy je kumulativní **od vývěskové zastávky**, takže její vlastní řádek je 0 **a hned následující
zastávka je taky 0**, jede-li se tam pod minutu. Dál v tom sloupci už nula pro jízdu nevzniká - kumulativní
součty se při nulovém úseku jen zopakují (1→1). Tím padá hypotéza o legacy sloupci „Průběhy" i potřeba
per-segment sloupce. → nový blok **`B64`** (S): řádek jiný než vývěskové zastávky s nulovou kumulativní
dobou tiskne „<1", parametr sestavy, default vypnuto.

⚠️ **Past k tomu:** `CasJizdyZMapy` vrací 0 i pro zastávku **mimo trasu** nebo projížděnou - to je
„nemám data", ne nulová jízda. Bez rozlišení by „<1" vyskočilo tam, kde má být prázdno.

**`TR9` obrácená logika poznámkování - berou kolegové.** Rozsah TČ přímo vedle značky místo klikání
u každého spoje; zapracují to do zadávání jízdních řádů. Tisková strana se nemění, u nás žádná práce.

**Tím je sada TRANSDEV celá rozdělená** - nic nezůstalo bez vlastníka: 5 hotových, `TR7` k ověření
u klienta, `TR5`+`TR9`+#7937 u kolegů, `TR10` v #13045, `TR8` u nás jako `B64`.


## 2026-08-10 (1) - 🤝 dohoda s kolegy: sloupce pro odsazení a cíl; co tím pokryté NENÍ

Jan se domluvil s kolegy, že do jízdních řádů přidají **sloupec pro odsazení zastávky** a **sloupec
pro zadání cíle**; velký tiket na vykreslování odjezdů (#13045 „Skupiny dnů - nastavení", KOVED, open)
řeší definici sloupců. Tím se zaplňují **tři** z otevřených položek: `TR5` (cíl), #7937 (odsazení
per směr) a `TR10` (skrývání spojů o víkendu, pohlcené #13045).

**Nepokryté zůstávají dvě věci** a je potřeba, aby to nezmizelo mezi řádky:

- **`TR8` „<1" místo „0"** - nikdo to nemá. Pořád chybí odpověď, ve kterém sloupci zákazník tu nulu
  vidí (u nás nevzniká, časy jsou kumulativní; kandidát je legacy „Průběhy").
- **`TR9` obrácená logika poznámkování** - zadávání poznámek (rozsah TČ vedle značky místo klikání
  u každého spoje). Není součástí ani jednoho ze tří dohodnutých kusů.

**Dvě věci k ověření u dohodnutých sloupců:**

- **cíl musí být per zastávka × směr** (střední varianta). Jen per směr už umíme dnes přes
  `POPIS_SMER`=konečná a zákazník to sám označil za nouzové řešení.
- **odsazení per Tč pásmo vývěsu** byla druhá polovina #7937 a sloupec ji nepokryje - ověřit
  u TRANSDEV, jestli na ní trvá.

Mimo zastávkové vývěsky pořád visí **#11300** (KORID, skrýt prázdné přípojové řádky) - přípoje nejsou
koncept zastávkového JŘ, patří k linkovým. A na naší straně `B63` (nečteme „nevypisovat značku
ve sloupci") - pokud #13045 to nastavení přesune do šablony, `B63` se stane zbytečným, jinak platí.


## 2026-08-07 (3) - ✅ rozhodnutí: skrývání spojů o víkendu pohltí obecné definování sloupců

**Jan 7. 8.:** `TR10` nedělat zvlášť. Až se sloupec bude definovat obecně (seznamem značek nebo
rozsahem dnů), tenhle případ z toho vypadne sám a dnešní pevné schéma se stane presetem.

⚠️ **Podmínka, bez které pohlcený není:** definice sloupce musí nést i **vyloučení**, ne jen zahrnutí.
Kolega to formuloval jako „vypisovat spoje, které ty značky mají obsažené" - TRANSDEV ale potřebuje
opak: spoj podle dat jede a on ho ve víkendovém sloupci nechce. S pouhým zahrnutím by musel označkovat
všechny spoje, které vidět **chce**, tedy víc práce, ne méně. Platí to i pro rozsah dnů (spoj, který
v sobotu jede, se do sloupce spočítá a musí ho přebít vyloučení). Dnešní per-linkový seznam
„nevypisovat spoje se značkou" je přesně ten vylučovací seznam - zpětná kompatibilita zdarma.

**Levná cesta zůstává jako fallback** (dorovnat ten seznam ze 2 slotů na 4 per sloupec), kdyby
TRANSDEV tlačil dřív, než bude `B39` navržená. `B63` (dočtení „nevypisovat značku ve sloupci") na tom
nezávisí a platí dál - je to jiná osa a dnes kvůli němu tiskneme značky, které si zákazník vypnul.


## 2026-08-07 (2) - 🔗 skrývání spojů a vlastní skupiny dnů jsou jedna věc + nález: nečteme jeden seznam

Jan se zeptal, jestli `TR10` (skrývat spoje o So/Ne/svátcích) souvisí s návrhem kolegy k `B39`.
**Souvisí, je to tentýž problém** - a při hledání odpovědi vypadl nález na naší straně.

**Starý modul má u linky DVA různé per-sloupcové seznamy** (`JRapiLinPrtMhd1.mac` ř. 37-45), což se
dosud pletlo do jednoho:

- `MHD_NENZ1_NENZ2` (2 sloty) - **skryje spoj** z pracovních sloupců (mimo prázdniny / o prázdninách).
  Tohle čteme, je to zdroj dnešního prázdninového dělení.
- `MHD_NEZNZ123` (**4 sloty, per sloupec**) - minuta se vytiskne, ale **potlačí se u ní značka**;
  a je-li značka potlačená ve všech sloupcích, kde spoj je, vypadne i z legendy (`noNZ2`, ř. 348).
  **Tenhle ignorujeme.** → nový blok `B63` (S): na naší vývěsce se tisknou značky, které si zákazník
  u linky vypnul. Sousední řádky téže rutiny, ze které jsme portovali `ZarazeniSpoje` - vzali jsme
  37-40 a 41-45 ne. Souvisí s #12964 (stížnost na množství značek).

**Odpověď na Janovu otázku:** „nevypisovat značku" a „nevypisovat spoj" jsou dvě různé potřeby a starý
modul je **záměrně rozděluje**. V novém obecném řešení musí zůstat oddělené - kdyby splynuly do jednoho
seznamu, zákazník přestane umět říct „spoj tiskni, jen u něj neopakuj značku, kterou už říká nadpis
sloupce" (což je přesně to, co jsme řešili v `B29`).

**Návrh kolegy `B39` pohltí `TR10`.** Definovat sloupec buď seznamem značek, nebo rozsahem dnů;
dnešní pevné schéma se stane presetem a skrývání o víkendu z toho vypadne samo. **`TR10` proto
nezakládat jako samostatnou featuru.**

⚠️ **Korekce mého včerejšího tvrzení:** napsal jsem, že „číslo negativní značky je per linka, takže
seznam nemůže být parametrem šablony". **Není to podložené** - rozsah čísel se přiděluje **firemně**
(`^JRSET("Cis","InPlatODDO")`, symboly `InPlatOzn`), jen význam značky plyne z omezení jednotlivých
spojů. Jestli „značka 3" znamená na dvou linkách totéž, je otázka datové disciplíny - a rozhoduje,
kde smí nové per-sloupcové nastavení žít. Otázka na kolegu, ne moje tvrzení. (Zrušení `B62` to
nemění, ten padl z jiného důvodu.)

⚠️ **Skutečná práce v `B39`** je varianta „rozsah dnů": potřebuje vyhodnocovač **„jede spoj v datu X"**.
Dnes omezení spoje umíme jen formátovat do textu, ne jimi počítat.


## 2026-08-07 (1) - ❌ oprava: blok `B62` zrušen, můj návrh by funkci rozbil

Jan se zeptal, **kdo poptal** „vlastní seznam skrývaných spojů per sloupec", který jsem u `TR10`
nechal kolegům. **Nikdo** - vymyslel jsem si ho jako omezení svého návrhu a pak to omezení povýšil
na úkol. Přesně ta poznámka bez poptávky, kterou z roadmapy vyhazujeme.

Kontrola u té otázky odhalila **věcnou chybu v `B62`**. Existující per-linkový seznam „značky
nevypisovat" (`MHD_NENZ1_NENZ2`) míří na sloupce **„Pracovní dny mimo prázdniny"** a **„Pracovní
dny o prázdninách"** - tedy dva **pracovní** sloupce (`NacistSkupinyDnu`), ne „školní", jak jsem
tvrdil. Víkendový je až třetí.

Můj návrh („použij týž seznam i na víkend") by tedy spoj odebral **ze všech sloupců naráz** a nevytiskl
by se vůbec. Zákazník chce opak: nechat ho v pracovních dnech, schovat jen v So/Ne/svátky.
**Blok `B62` zrušen** - byl by to regres vydávaný za featuru.

`TR10` je tím celý na kolegích: potřebuje **vlastní seznam značek pro víkendové sloupce** (nové pole
ve Vlastnostech linky vedle dnešního pro pracovní). Naše část pak bude pár řádků. Je to i to, o co
zákazník doslova žádal - rozšířit dnešní nastavení na další sloupce, ne postavit něco nového.

**Poučení:** včerejší nález „malá varianta jde u nás" jsem si neověřil až na sloupce, kterých se ten
seznam týká. Stačilo přečíst `NacistSkupinyDnu` vedle `ZarazeniSpoje` - dva soubory od sebe.


## 2026-08-06 (5) - 🔍 sada TRANSDEV prověřená v kódu: dva body se posunuly

Poctivý průchod tří bodů, o kterých jsem tvrdil, že „drží vstupní strana". **Dva z nich tak nestály.**

**`TR7` výběr sloupců jízdních dob - v návrháři ten problém vůbec neexistuje.** Klient si stěžuje,
že „je náročné tipovat, který sloupec zapnout, protože číslování odráží pořadí výskytu varianty".
V TD se **žádný sloupec nezapíná**: sloupec jízdních dob se sám rozvine na tolik podsloupců, kolik
je variant, a v záhlaví nejsou pozicová čísla, ale **písmena variant** (kolize `B1`/`B2`, varianta
bez písmene `T1`). Nestabilní pořadí napříč stránkami řeší volba „Řazení variant: Stabilní" od 27. 7.
**Není co implementovat ani na kom to nechat - poslat klientovi k vyzkoušení.** Jeho hlubší přání
(„určit, podle kterých spojů se doby generují") vzniklo jako obklad tohoto problému, takže se má
ptát znovu až po vyzkoušení.

**`TR10` skrývání spojů o So/Ne/svátcích - malá varianta jde čistě u nás** (nový blok `B62`, S).
Potlačení už v kódu je, jen míří na jiné sloupce: `ZarazeniSpoje` čte per-linkový seznam „značky
nevypisovat" a nuluje jím **jen školní / prázdninové** sloupce, víkendové nastaví bez jakékoli
kontroly. Přepínač sestavy může ten **týž seznam**, který klient pro školní sloupce vyplňuje, použít
i na víkend. Default vypnuto - v režimech bez prázdninového dělení by se ten seznam začal poprvé
čítat, tedy chování závislé na datech zadaných k jinému účelu.
⚠️ **Číslo negativní značky je per linka, ne firemní číselník**, takže seznam značek **nemůže** být
parametrem šablony s vypsanými čísly - jediná cesta u nás je „použij ten, co už existuje". Vlastní
seznam per sloupec proto zůstává práce kolegů.

**`TR9` obrácená logika poznámkování - potvrzeno, že my ne.** Rozsah TČ je uložený **per spoj**
(`^JRL(...,"PATCH","MHDSPNPZN",spojId)` = `od~do`), u značky žádný rozsah není. Klient ho chce vedle
značky, aby ho nezadával dvacetkrát - to je nové pole a přepsané zadávání. U nás **není co číst**
a tisková strana se nemění.

**Poučení:** „drží to vstupní strana" jsem u dvou bodů odvodil z textu požadavku, ne z kódu.
U `TR7` to znamenalo nechat na kolegech něco, co je hotové.


## 2026-08-06 (4) - 🚌 sada TRANSDEV přerovnaná: 5 z 10 hotových, zbytek drží vstupní strana

Průchod celého wishlistu TRANSDEV (10 bodů, SK legislativa + MHD Trenčín) proti kódu.

**`TR6` barva linky je hotová** - a šlo to mimo naši evidenci, proto se to sem doplňuje. JŘ modul umí
od 3. 8. víc barev na linku (#12997, Petr Šmíd), agenda je vydává jako `BARVA_LINKY` / `BARVA_LINKY_2`
a jdou napsat do každého barevného pole šablony místo pevné barvy. Klient používá první jako text
a druhou jako pozadí - přesně to, co v požadavku popsal („jedna šablona, ne desítky"). Volba „barva
linky" **záměrně není položkou palety** - přidala by klik všem kvůli menšině případů.

**Hotových je 5 z 10:** TR1 (osa přes půlnoc), TR2 (souhrn na-znamení), TR3 (nástupiště), TR4 (auto
výška řádku), TR6 (barva linky). TR7 zčásti (varianty dle sledu zastávek + stabilní řazení, 27. 7.),
TR5 v minimální variantě (konečná směru).

**Podstatný nález: zbytek sady nedržíme my, drží ho vstupní strana** (editor trasy / editor spojů /
Vlastnosti linky v legacy JŘ). Do §TR přidána tabulka „co potřebuje od kolegů" pro `TR5`, `TR7`, `TR8`,
`TR9`, `TR10` a `B12`/#7937 - u všech je naše část malá a **navazující**, takže pořadí je dané: nejdřív
vstupní strana, pak my. Dřív to bylo rozházené v textu jednotlivých bodů, takže z toho nešlo poznat,
že pět z šesti otevřených věcí čeká na někoho jiného.


## 2026-08-06 (3) - 🧹 fronta zastávkového JŘ: jen naše otevřená práce

**Rozhodnutí Jan:** roadmapa je seznam **naší otevřené práce**, ne zrcadlo helpdesku. Cizí a hotové
věci z ní pryč, i když je „dobré o nich vědět" - poznámka bez akce jen dělá frontu nepřehlednou.

**Ze zastávkového JŘ odstraněno:**

- **#13150** (export blokuje počítač, BUSEM), **#12964** (negativní značky Vsetín), **#13102**
  (virtuální prodloužení spoje) - řeší se v **legacy JŘ modulu** nebo jsou přiřazené jinému člověku.
- **#13099** (souhrnné JŘ) - není naše práce v návrháři.
- **#13048** včetně nezodpovězeného dotazu na označník - tiket je zavřený od 30. 7.; dotaz žije
  v helpdesku, roadmapa ho držet nemusí.
- **`TR2`** (souhrn na-znamení) - **hotové**, SK ověření kódování proběhlo (Jan). V evidenci
  požadavků 🟡→✅.
- Sekce **„Čeká na někoho jiného"** zrušená celá. `TR8` (nulová jízdní doba) přešel do
  „Bez poptávky nebrat", což je totéž zaparkování s jasnou podmínkou.

**Zůstalo:** `B12` (#7937 odsazení per směr, jen když TRANSDEV tlačí) a `B39` (vlastní skupiny dnů,
velký blok) - dvě věci, obě naše. Plus parkoviště „bez poptávky nebrat", které je záměrně parkoviště.

Do budoucna platí pravidlo v hlavičce roadmapy: **tikety mimo návrhář a čekání na cizí lidi se sem
nepíšou.**


## 2026-08-06 (2) - 🧹 průchod roadmapou: pět neplatných tvrzení pryč

`B61` (asynchronní export náhledu) **odstraněn** - rozhodnutí Jan, není to teď priorita. Poučení
o snapshotu ale zůstalo **v kódu u té metody** (`PrintDialogViewModel.ExportPdf`), protože tam se
na něj někdo podívá; roadmapa na to není potřeba. #13150 se navíc týká **starého tisku**, ne našeho.

**Co bylo v roadmapě napsané a neplatilo** (ověřeno v kódu, ne z paměti):

- **Fáze B krok 1** slibovala „opravy nálezů: `Clone()` zapomíná `SummaryRows`, `RowVM.ToModel()`
  zahazuje per-cell `Format`+`Style`". **Obojí je dávno opravené** - `TableElement.Clone()` kopíruje
  `SummaryRows` (ř. 716) a `CellVM.ToModel()` nese `Format` i `Style` (ř. 205-210). Někdo by na to
  plánoval čas, který není potřeba.
- **`B14` tvrdil**, že „`CasHodnota` bere `"|"` jinak v `KnizniJrAd` vs `ZastavkoveJrAd`".
  **Neplatí** - obě verze jsou funkčně identické, lišil se jen koncový komentář (nález analýzy `B56`).
- **Stale názvy tříd:** `KnizniJrAd`/`KnizniJrMhd` se jmenují `LinkovyJrAd`/`LinkovyJrMhd` (od migrace
  šablon 1.2.0). Roadmapa je vedla pod starým jménem na dvou místech.
- **`B14` odhad objemu duplikace** doplněn korekcí z analýzy: ne „~400-500 řádků", ale z 27 stejně
  pojmenovaných helperů je jen 15 doslovných kopií (~97 řádků).

**Sekce Import PDF, AI integrace, Fáze C+D/E/F a provozní čtyřka prošly bez nálezu** - jejich tvrzení
odpovídají stavu kódu.

**Nedotaženo:** jestli KOVED odpověděl na dotaz k označníku (posláno 3. 8. k #13048) - přes API se mi
tiket nepodařilo dohledat, je to na Janovi. Nic to neblokuje.


## 2026-08-06 (1) - 🧹 sekce zastávkového JŘ srovnána, dva čerstvé nálezy z helpdesku

**Sekce byla rozbitá** po dokončení `B37`: zůstal v ní celý **implementační plán hotové featury**
(14 řádků), osiřelé buňky tabulky (`| M | klient si zapne… |`) a dva bloky (`B12`, `B39`) bez hlavičky
tabulky. Plán je historie (drží ho archiv a changelog 5. 8.), takže odešel; hlavička vrácena.

**Prošel jsem otevřené tikety JŘ, ať roadmapa odpovídá skutečnosti** - a dva jsou z dneška:

- **#12964 (ZGROUP, Vsetín)** - zákazníkovi vadí množství negativních značek a text „pokračuje
  **přes** X" místo „pokračuje **do** X". **Není naše:** opravuje se v legacy modulu
  (`JRapiLinPrtMhd.mac:454`, Petr 5. 8.). 💡 Ale **potvrzuje znění, které jsme v `B37` zvolili** -
  my emitujeme „pokračuje do zast. X (l. …)", tedy přesně to, co zákazník chce. Zapsáno, aby to
  MHD agenda v TD jednou nedělala jinak.
- **#13150 (BUSEM)** - export výlepových JŘ do PDF „se dělá na popředí, vyblokuje počítač a když se
  přepneme do jiného programu, vrátí nás to zpátky". **Nejspíš není náš** (TD dělá export reálných dat
  na pozadí s progresem už dnes a krádež fokusu se ničemu v TD nepodobá; „v nových jízdních řádech"
  + přiřazený Petr ukazuje na JŘ modul) - **ale je to potřeba potvrdit**, protože kdyby BUSEM tiskl
  vývěsky z návrháře, je to naše.

**Nový blok `B61` (S)** - při čtení #13150 se ukázalo, že **jednu cestu máme rozbitou stejně**:
`PrintDialogViewModel.ExportPdf()` (export náhledu) staví data, renderuje i otevírá PDF **synchronně
na UI threadu**, bez progresu - porušuje R1. Export reálných dat to má správně, takže je to jedna
zapomenutá cesta. Dnes to nebolí (jedna sada ukázkových dat), u velké šablony ale okno zamrzne.


## 2026-08-05 (11) - proklik: hláška o skrytém obsahu zrušena, popis agendy vrácen na kód

Výsledek prokliku (Jan). Tučnost, dědění agendy, štítek ukázkových dat, výběr linek s nulou,
rozpad skupin, nápověda i plynulost obrázku/kódu **prošly**. Dvě věci zpět:

**1. Signál „Sestava: poznámky skryté nastavením" zrušen celý.** Rozhodnutí Jan: *„jestli to měl být
fix chyby, tak to má opravit ta chyba, ne přidat další hlášku, která je matoucí ve smyslu toho, co má
komunikovat."* Vznikl jako obklad na `B24` (uložený parametr zapnutý, UI ho ukazovalo vypnutý,
z vývěsky tiše zmizely odjezdy) a od té doby nikdo nepoznal, co konkrétně hlásí - i po ranní opravě
falešných poplachů zůstal neakční, protože nepojmenoval nastavení ani cestu k němu. Tu třídu chyb
dnes drží **driftové testy schémat** z `B54`/`B59`, tedy oprava místo obkladu.

Odešlo s ním i `NoteSuppressionCounter` (existoval jen pro tu hlášku) a protahování počitadla přes
`NoteVisibilityFilter` - signatury se tím zjednodušily o parametr. **Testy filtrování zůstaly**,
jen zbavené počitadla: co se schová při vypnuté kategorii v prvku, u „na znamení", u přestupů
a u souhrnu bezbariérovosti, se hlídá dál. Invariant v `CLAUDE.md` přepsán na „nezavádět zpátky
bez pojmenování konkrétního nastavení".

**2. Popis ve výběru agendy vrácen na kód konfigurace.** Rozhodnutí Jan: *„to předchozí, co tam bylo,
mi přišlo pochopitelné, ať tam nevymýšlíme nějaké zbytečné věci."* `ZASTAVKOVY_JR_AD` je zpět místo
mého „Data z databáze - vybírá se linka"; dva testy na ten popis zrušené.

**Poznámka k rozpadu skupin (`B57`):** vyhovuje, ale **čeká se na reakci zákazníků** - kdyby jim
formuláře pořád přišly dlouhé, kandidát je rozdělit je na víc tabů. Zatím se nic nemění.

**Ověřeno:** 739/739 .NET (o 10 méně - zrušené testy hlášky a popisu agendy), build solution čistý.
Caché strany se to netklo.


## 2026-08-05 (10) - 🧹 sekce nálezů review uzavřena, běžná fronta je prázdná

Rozhodnutí Jan 5. 8. po dojetí `B53`-`B59`:

- **`B18`** (deklarativní pravidla EDInputFormu) → **Backlog velkých věcí**. Není to featura
  návrháře, ale kapabilita sdílené komponenty pro celý EDISON; v běžné frontě jen zabírala místo.
  Nese tři follow-upy (migrace editoru stylů buněk, zašednutí polí mrtvých bez parametru, popis
  u parametru sestavy z `B57`).
- **`B60`** → k **linkovému JŘ**, kam patří. **Změřeno, než se rozhodlo:** není to malá věc, jak
  vypadalo. Tvarování názvu zastávky visí na **dvou parametrech sestavy** (`TVAR_NAZVU_ZASTAVKY`
  se 4 volbami, `NAZEV_PRO_MHD`) načítaných do `^||TiskAd`, takže srovnání znamená přidat linkovému
  JŘ dva **uživatelsky viditelné** parametry (popisky enumu jsou po vydání zamčené) + sdílet pět
  helperů - a to bez jakéhokoli testu (linkové agendy mají 0 testů). Nejdřív se musí rozhodnout,
  jestli ty parametry v linkovém JŘ vůbec chceme.
- Sekce „Architektura a nálezy review (4. 8.)" **smazána** - byla prázdná.

**V běžné frontě tím nezůstal žádný blok.** Další krok není další blok, ale **proklikat 5. 8.** -
deset commitů měnících UI, z nichž nic neprošlo očima. Že to není formalita, ukázal `<SYNTAX>`
v seznamu zastávek: našla ho první ruční akce, ne devět zelených sad testů.


## 2026-08-05 (9) - přidání prvku padalo na `<SYNTAX>`, doplněna brána

**Regrese z `B57` (8), nahlášená hned při prokliku:** přidání seznamu zastávek do šablony skončilo
chybovým dialogem `<SYNTAX> zInit+36^…StopListElementEdit`. Ve dvou vysvětlivkách (a jedné u mřížky
odjezdů) jsem uzavřel český citát obyčejnou `"` - ta v ObjectScriptu **ukončí textový literál**,
takže zbytek řádku se rozsypal. Správně se zdvojuje (`""`).

**Proč to prošlo přes všechny brány:** rozbitá třída se na server nasadí, **.NET testy o Caché
zdrojácích nic nevědí** a `%UnitTest` kompiluje jen testovací package - takže 740/740 i 74/74 svítilo
zeleně, zatímco formulář byl rozbitý. Ověřoval jsem nasazení voláním serveru, ale jen u legendy
poznámek; u seznamu zastávek ne, a právě ten byl vadný.

**Opraveno + brána:** `ObjectScriptSyntaxTests` projede **všechny `.cls` v repu** a nahlásí řádek,
který končí uprostřed textového literálu (parsuje znak po znaku, komentáře řeší implicitně).
Prověřeno mutací - vrácení původní chyby test shodí. Do `cache-api.md` přidáno pravidlo o zdvojování
uvozovky a upřesnění, že **voláním se ověřuje každá změněná třída**, ne jedna za všechny.

**Ověřeno:** 749/749 .NET, 74/74 Caché, `StopListElementEdit.Init` i `DepartureGridElementEdit`
volané na serveru vracejí `ok` a všechny nové skupiny s vysvětlivkami.


## 2026-08-05 (8) - `B57` formuláře prvků se daly přečíst

Nález review 4. 8.: skupiny formulářů přerostly rozpočet (~7 položek) a u mřížky odjezdů nebyla
k 93 polím **jediná vysvětlivka**. Změřeno před zásahem: „Zastávky - specifické" 23 polí,
„Filtrování" legendy 11, „Obecné" u dynamické tabulky 11, u mřížky odjezdů 10.

**Rozpad skupin** (klíče polí se NEMĚNÍ - jde jen o zařazení, takže uložené šablony ani panel
vlastností to nepocítí):
- **Seznam zastávek**, 23 → pět skupin: „Zastávky - obsah" (6), „Stanoviště a varianty tras" (2),
  „Rozdělení na oddíly" (2), „Značky a šipka směru" (7), „Aktuální a projeté zastávky" (6).
- **Mřížka odjezdů**, „Obecné" 10 → „Skupiny dnů" (3) a „Rozměry a mřížka" (5); mezery u značek se
  přesunuly ke značkám, kam patří.
- **Legenda poznámek**, „Filtrování" 11 → „Co se tiskne" (3), „Poznámky linky" (4),
  **„Značky u odjezdů"** (4). Poslední skupina je ta, kterou lidi hledají - do července to byly
  parametry tisku.

**Žargon pryč:** „anotace" bylo interní slovo. Nově **„značky u odjezdů"** - v poli, ve skupině
i v panelu vlastností (`Anotace - symboly` → `Značky u odjezdů - symbol`, „Mezera min. ↔ anot." →
„Mezera minuta ↔ značka"). Property v kódu se nepřejmenovaly, jen popisky.

**Vysvětlivky:** doplněno 13 tam, kde z popisku nešlo poznat, co volba dělá (šířka minuty, oddělovač
skupin, hodiny u každé skupiny, max skupin, negativní značka, značky u odjezdů, na znamení, atributy,
všechny varianty tras…). Ne ke všem 93 polím - u „Font" nebo „Tučné" by vysvětlivka byla šum.

**Brána zafungovala sama:** rozpad skupiny „Filtrování" **shodil test** z `B58`, který hlídá, že
nápověda F1 odkazuje na existující místo. Přesně proto vznikl - srovnány nápověda, wiki seed
i test na nové názvy skupin.

**Ze zadání vyhozeno:**
- **`TableEditorDialog` z 5 karet na 3** - pevná tabulka je **spící prvek** (z UI ji nelze vytvořit,
  dialog žije jen pro otevření starých šablon, `TableElement.cs:123-128`), takže dnes se k němu
  uživatel nedostane. A redukce toho dialogu **už v roadmapě je** ve Fázi B kroku 2 - byla to druhá
  evidence téže práce (proti pravidlu 3 roadmapy).
- **Vysvětlivky u parametrů sestavy** (`SCHOVAT_V_ROZSAHU`, `ROZTAHNOUT_NA_VYSKU`) - parametr
  **žádné pole pro popis nemá** (`TemplateParameter`), takže to není „dopsat text", ale aditivní
  rozšíření kontraktu na obou stranách. Přesunuto k `B18`, kde se gramatika parametrů stejně otevírá.

**Ověřeno:** 740/740 .NET, 74/74 Caché, nasazení potvrzené voláním `NotesLegendElementEdit.Init`
(server vrací všechny tři nové skupiny).


## 2026-08-05 (7) - signál „nastavení mění obsah vývěsky" svítil, i když se nic nedělo

Nález Jan 5. 8. při prohlídce `B58`: modrý štítek ve stavovém řádku (*„Sestava: poznámky skryté
nastavením"*) hlásil skrytý obsah na **úplně běžné šabloně**, kde nikdo nic nenastavil.

**Příčina - regrese z přesunu parametrů do prvku (4. 8., changelog (7)):** signál má hlídat jedinou
věc - že **uložený parametr sestavy** tiše mění obsah (vznikl z případu, kdy zapnuté „Rozdělit
vývěsku pro každý označník" zmizelo z UI a s ním i odjezdy jiného stanoviště). Kód si to sám hlídá:
u „na znamení" a „přestupů" je výslovně napsáno, že se do signálu **nepočítají**, protože je
neschovává parametr, ale nastavení prvku, *„takže by signál svítil u běžných šablon"*. Když se ale
kola, místenky a souhrn bezbariérovosti přesunuly z parametrů do prvku poznámek, počítat se
nepřestaly - a protože jsou **vypnuté ve výchozím stavu**, stačilo mít v datech značku, kterou si
uživatel nezapnul, a signál se rozsvítil.

Je to nejdražší třída chyb v projektu naruby: signál, který má varovat, že UI lže, sám lhal.

**Opraveno:** kategorie vypnuté v prvku se do signálu nepočítají (stejně jako „na znamení"
a „přestupy"). Zastaralý tooltip srovnán - jmenuje jen skutečné příčiny a dodává, že značky vypnuté
v prvku nejsou skrytá změna. Dva regresní testy (chyba i její protipól, ať se oprava nepřehnala
a signál nezmlkl tam, kde varovat má), prověřené mutacemi. 740/740.

## 2026-08-05 (6) - `B58` srozumitelnost pro nováčka

Nález review 4. 8.: pět míst, kde se nový uživatel zasekne, protože mu aplikace neřekne, co se děje.

- **Náhled s ukázkovými daty je poznat** - štítek instance ve stavovém řádku říká
  *„Linka: - (ukázková data, klikněte pro výběr)"*. Bez lišty stránkování byl náhled se smyšlenými
  daty k nerozeznání od hotové vývěsky. **Rozhodnutí Jan 5. 8.:** informace patří do štítku, který
  už existuje, ne do nového prvku u náhledu - první verze tam přidávala rámečky navíc a to se
  neujalo. U šablony bez vazby na databázi se nehlásí, tam by lhala.
- **Nová šablona (Ctrl+N) zdědí rozpracovanou agendu** (rozhodnutí Jan 5. 8.). Dosud vznikala bez
  agendy → prázdná nabídka datových polí, prázdný náhled a nováček nevěděl, co dělá špatně.
  Přepnutí zůstává na Ctrl+Shift+A.
- **Výběr agendy neukazuje interní kódy** (`ZASTAVKOVY_JR_AD`) - místo nich popis, co se u agendy
  vybírá („Data z databáze - vybírá se linka", u obecné šablony „Bez vazby na databázi"). Registr
  popis po drátě nenese a doplňovat ho kvůli jednomu dialogu je nepoměr, takže se skládá z toho, co
  registr má.
- **„Použít" bez zaškrtnuté linky poradí**, co udělat, místo tichého propadnutí na ukázková data.
  Když ale výběr **existoval**, je nulový výběr legitimní („chci ho zrušit") a povolí se - výsledek
  pak řekne štítek výše.
- **Nápověda F1 má sekci „Vývěska JŘ - obsah a značky"** s featurami posledního měsíce: filtry
  poznámek, značky u odjezdů (kola, místenky, pokračující spoje), souhrn bezbariérovosti, rozdělení
  seznamu zastávek na oddíly, řazení dynamické tabulky. Součástí je i to, **kam se přesunuly zrušené
  parametry tisku** - přesně tam je lidi budou hledat.
- **Totéž doplněno AI pomocníkovi** (`CapabilityCatalog`) - měl stejnou mezeru, takže by uživatele
  posílal do parametrů sestavy, které už neexistují.

**Nová brána:** testy ověřují, že přepínače, na které nápověda odkazuje, na tom místě **opravdu
jsou** (skupina „Filtrování" prvku poznámek, oddíly u zastávek, řazení u dynamické tabulky). Kdyby
se přesunuly, nápověda by začala lhát a test spadne. Prověřeno mutacemi - všechny tři chytí.
**738/738 .NET.**

**Wiki seed** `uzivatelska-prirucka.md` aktualizovaný (nová šablona dědí agendu, štítek ukázkových
dat) - připravený k nahrání, nesynchronizováno.

## 2026-08-05 (5) - `B59` formulář přestal lhát o tučnosti záhlaví a minut

Dokončení `B54`: u vlastností, které se **dědí z kaskády** (nejsou na prvku nastavené), formulář
vědomě ukazuje hodnotu, na kterou kaskáda dopadne. Kontrola z `B54` je jen přeskakovala, protože
porovnávala surovou vlastnost. Doplněno srovnání proti kaskádě - a hned to našlo dvě **skutečné
lži**, které uživatel vidí (na rozdíl od `B54`, kde šlo jen o nastražené pasti):

- **Zaškrtávátko „Tučné" u stylu záhlaví** (seznam zastávek, dynamická tabulka) bylo nezaškrtnuté,
  přitom se záhlaví tučně tiskne.
- **Totéž „Minuta: Tučné"** v mřížce odjezdů - minuty se tisknou tučně (drží to skalární
  `MinuteBold = true` jako poslední fallback), formulář tvrdil opak.

**Proč to `B54` nenašlo:** tahle pole jdou přes sdílený stylový balíček (`StyleFields`) a z parsování
vypadávala - v ObjectScriptu je `_` **operátor spojení**, ale regex ho bral jako součást názvu, takže
se `prefix_".Bold"` nedosadil a klíč zóny se nepoznal. Opraveno; rozsah kontroly se tím rozšířil
z 8 polí na všechna stylová pole všech zón (včetně podmíněného přiřazení `s:defTextColor'="" f.value`).

**Co se změnilo:**
- `StyleFields.Add` má parametr `defBold` (aditivní, na konci - stará volání se nerozbijí); posílá ho
  seznam zastávek a dynamická tabulka pro záhlaví a mřížka odjezdů pro minuty.
- `DepartureGridElement` dostal tři přístupové body „z čeho zóna dědí" (minuty, symboly a text
  značek) - bez nich nešlo strojově ověřit, co u podzón z kaskády dopadne. Konvence názvu
  `ResolveElement<Zóna>Parent()`; když chybí, **test to nahlásí**, ne přeskočí.
- `ResolveGroupMinuteParent` se o nový přístupový bod opírá, takže skalární fallback tučnosti
  a velikosti minut je na jednom místě, ne na dvou.

**Ověřeno:** 730/730 .NET, 74/74 Caché, nasazení `.cls` potvrzené voláním `StopListElementEdit.Init`
(server vrací `HeaderStyle.Bold` = 1). Nová kontrola prověřena mutacemi (změna barvy i tučnosti
v kaskádě test shodí). Pokrytí hlídá `PorovnaniSKaskadou_NezustaloBezPokryti`, ať kontrola nezůstane
mrtvá, kdyby ta pole z formulářů zmizela.

**⚠️ Poučení k odtiskům:** zjednodušení výpočtu minut jsem chtěl doložit odtiskem pixelů, ale mutace
ukázala, že **odtisk tuhle třídu změn nezachytí** - fixtury mají styly nastavené natvrdo
(`DataStyle = Style(7)`), takže se kaskádové fallbacky vůbec nespustí. Doloženo kaskádovými unit testy
(`DepartureGridCascadeTests`, obě mutace chytí). Poznámka doplněná k `ElementRenderSnapshotTests`.

## 2026-08-05 (4) - pokračující spoje na vývěsce, `B37` hotová (#13093 bod 3 - poslední bod tiketu)

Klient chtěl označit odjezdy, které pokračují dál, jednou poznámkou per cíl: *„u1 - pokračuje do zast.
Uherské Hradiště, žel. st. (l. 304)"*, seskupeno podle cíle a linky, **bez ohledu na číslo spoje**.

**Cesta k datům byla to hlavní na celé položce.** Postavil jsem to nejdřív na uzlu **32** a bylo to
špatně - kolega (#13098) upřesnil, že pokračování je v uzlu **34**, což data okamžitě potvrdila:
záznam začíná `u@`, tedy tou značkou „u", kterou pro pokračování používá legacy
(`JRapiLinPrtWin` ř.1561) a kterou má klient v požadavku. Uzel 32 je návaznost (`m@…`, jiný tvar,
nese i vlaky). Navíc `m@<n>` je **index záznamu, ne typ vztahu** - na tom stál můj původní plán filtru.

Tvar: `u@<náš spoj>:<linka>:<spoj>:<cíl>:1:0`.

- **Zastávka v záznamu je cíl**, ne předávací bod - dokázáno rozdílným vzorkem: spoj 251 linky
  0773165 má v záznamu Otrokovice,Štěrkoviště a trasa jeho pokračování tam končí, kdežto ostatní
  spoje končí na žel.st. a mají v záznamu žel.st. **Nedohledáváme tedy trasu pokračujícího spoje**,
  a tím padá i to, že pokračující linka může ležet v jiné skupině linek.
- **Značky `u1`, `u2`… přiděluje server per stránka** v pořadí prvního výskytu (stabilní,
  reprodukovatelné - stejný princip jako písmena rozdílů tras). Odjezdy se stejným (linka, cíl) mají
  tutéž značku. Číslice se dává vždy, i u jediného cíle.
- **„u + číslice" nekoliduje** s ničím na vývěsce: rozdíly tras a varianty berou A-Z, kroužkové dny
  číslice 1-7, ruční komentáře písmena z číselníku. Značka je víceznaková → kreslí se textem.
- **Jeden checkbox „Pokračující spoje"** v bloku poznámek (výchozí vypnuto), kategorie `pokracovani`.
  Zapíná značku i vysvětlivku naráz, takže nemůže vzniknout jedno bez druhého.

Ověřeno na dvou nezávislých případech: ZL_KOVED sku 2 linka 0773165 zastávka 16 (pokračování po téže
lince) a sku 1 linka 0824955 (pokračování **na jinou** linku) → `[u1] pokračuje do zast. Podhradí
(l. 532)`, u minuty 11:55 značky `Ru1` vedle sebe s místenkou.

Caché 74/74 (+1), .NET 726/726 (+1). **Zlaté odtisky beze změny** - gold linky (CB_CSADAD) spoje
s pokračováním nemají, takže featura je nemohla pohnout.

⚠️ Nedořešená drobnost: poslední dvě pole záznamu (`:1:0`) byla ve všech viděných záznamech
konstantní a nic z nich nečteme. Kdyby `f5=0` znamenalo „netisknout", bude to potřeba filtrovat.

## 2026-08-05 (3) - `B55` testy převodu šablon a čtení dat z databáze

Nález review 4. 8.: tři převody formátu šablony a celé čtení odpovědí agendy neměly test, navzdory
vlastním pravidlům (`template-schema-versions.md` krok 5, `feature-design-principles.md §7`).
Obojí je vrstva, kde se chyba pozná až u zákazníka - buď se změní hotová vývěska, nebo se vytiskne
prázdno. **+79 testů (646 → 725).**

**Převody formátu šablony:**
- `MigrationGridStyleTests` (1.6.0 → 1.7.0, nejsložitější převod) - pět hodnot volby rámce na dva
  přepínače vnitřních čar, přenos dosud zadrátovaného rámce tabulky do rámečku prvku (a **ne**
  u volby „Žádný"), respektování vlastního nastavení uživatele × přepsání fakticky vypnutého
  rámečku (nula / průhledná), skaláry JŘ prvků → objekt, dva vlastní přepínače seznamu zastávek.
- `MigrationHighlightRulesTests` - zvýraznění řádků z jednoho pravidla na seznam; neúplné
  zvýraznění (podmínka bez barvy) pravidlo nevyrobí.
- `MigrationKnizniToLinkovyTests` - přejmenování kódu agendy; ostatní kódy se nesmí dotknout.
- Všechny tři: idempotence + odolnost proti poškozené šabloně (chybějící sekce, číslo místo textu).

**Čtení odpovědí agendy** (`CacheAgendaParsingTests`, běží bez serveru i bez VPN): schéma
(typy polí, starší název kolekcí `groups`, pole bez názvu / kolekce bez klíče), **vícestránkový
výstup** (jedna stránka na zastávku × směr, přeskočení metadat `ERR`, poškozená stránka se zahodí
ale ostatní zůstanou), obsah stránky (čísla a booly na text s desetinnou tečkou, vnořená pole
zůstanou strukturou), předdefinovaný layout tabulky, parametry sestavy.
Kvůli testovatelnosti se vícestránkové čtení vyčlenilo do `ParsePages` - bez toho by šlo ověřit
jen se živým serverem.

**Nová strojová pojistka:** `KazdaMigrace_MaVlastniTridyTestu` reflexí hlídá, že ke každé migraci
existuje třída `<NázevMigrace>Tests`. Na test u nové migrace se tím nedá zapomenout.

**⚠️ Poučení, které stálo za to:** všech 78 testů prošlo hned napoprvé, tak jsem je proti kódu
**ověřil mutacemi** (rozbít podmínku → test musí spadnout). Jeden byl slepý - přeskočení klíče `ERR`
procházelo i s vypnutou podmínkou, protože testovací obsah `ERR` by se stejně neparsoval jako
stránka. Opraveno; zbylých 8 mutací sada chytá. Zapsáno do `template-schema-versions.md` krok 5.

## 2026-08-05 (2) - `B54` formulář prvku říká pravdu o výchozích hodnotách a nabízí, co panel

Nález review 4. 8.: drift test hlídal jen **existenci** property, ne hodnotu, takže se výchozí
hodnoty ve formuláři rozešly s C#. Dvě rozšíření kontroly + srovnání nalezených rozdílů.

**⚠️ Upřesnění nálezu:** zadání tvrdilo, že formulář uživateli ukáže jinou hodnotu, než se vykreslí.
Ve skutečnosti `ElementFormBinder.Prefill` použije hodnotu ze schématu **jen když property žádnou
hodnotu nemá** - u všech 14 rozdílů je vždy vyplněná, takže se přebije a uživatel dnes vidí správně.
Nebyla to živá chyba, ale **nastražená past**: jak vlastnost začne smět být nenastavená (nullable),
lež se aktivuje - a to je přesně třída „UI ukazuje jiné nastavení, než se tiskne".

**Nové strojové kontroly** (`EDInputFormSchemaDriftTests`, +24 testů, celkem 646):
- **Výchozí hodnota ve formuláři == C# default** (per formulář). Parsuje `f.value` i 3. argument
  `AddText/Bool/Number/Decimal` a porovnává s `ElementPropertyAccessor.Get` nad holou instancí.
  Rozbalování sdílených balíčků respektuje jejich přepínače (`AddGrid(form, 0)` u legendy poznámek
  mřížkové přepínače nepřidává) a dosazuje argumenty za parametry.
- **Co jde nastavit v panelu, jde nastavit i ve formuláři.** Od Wave 4 je formulář hlavní editační
  cesta; když v něm vlastnost není, je u jednoho prvku nedosažitelná. Napříč deseti prvky vyšly
  3 vědomé výjimky (dopočítané náhledy čárky/mezery u čáry, šířka sloupce hodin - ta má jediný
  zdroj v tabu Sloupce).

**Srovnané rozdíly (14):** barva pozadí čárového kódu (bílá, ne průhledná - čtečka potřebuje
kontrast) · rámeček a mřížka dynamické tabulky (barva, tloušťka, čáry mezi řádky) · čáry mezi
sloupci u mřížky odjezdů a linkové tabulky · šířka minuty a oddělovač skupin u mřížky odjezdů ·
okraje stránky (0 mm, ne 10) · vodorovné zarovnání textového prvku (na střed).
Sdílené balíčky `AddBorder`/`AddGrid` dostaly parametry pro tyto hodnoty - jedna definice pole,
ale každý prvek řekne svůj skutečný default (aditivní, staré volání se nerozbije).

**Doplněná pole mřížky odjezdů:** zarovnání záhlaví + pozadí a barva negativní značky.
Popisky negativní značky zůstávají rozlišené („Pozadí negativní značky"), i když legenda poznámek
používá u téhož klíče „Pozadí značky" - v mřížce stojí hned vedle obecné barvy symbolu anotace,
takže bez rozlišení by nešlo poznat, co která barva řídí (výjimka v testu s důvodem).

**Ověřeno:** 646/646 .NET, 73/73 Caché, a nasazení `.cls` na server potvrzené voláním
`DepartureGridElementEdit.Init` (odpověď obsahuje nová pole i opravené hodnoty) - testy čtou `.cls`
z repa, takže samy o nasazení nic neříkají.

**Zbývající mezera → `B59`:** u nenastavených („dědí se") stylových barev formulář vědomě ukazuje
hodnotu, na kterou dopadne kaskáda (rozhodnutí 4. 8. u pozadí záhlaví poznámek). Tu tenhle test
nevidí - porovnává surovou property, takže ty případy přeskakuje.

## 2026-08-05 (1) - `B53` obrázek a čárový kód se nepřepočítávají při každém překreslení

Nález review 4. 8.: `ImageElement.Render` dekódoval Base64 (limit 5 MB) a `BarcodeElement`
propočítával matici ZXing **při každém framu** - porušení pravidla „structured parsing vždy
cachovat" (`feature-design-principles.md §7`). Projevovalo se to škubáním při tahání prvku, zoomu
a psaní do vlastností.

**Co se změnilo:**
- **Obrázek** drží dekódovanou bitmapu + `SKImage`, klíč cache = reference stringu `ImageBase64`
  (string je immutable a nahrazuje se celý, takže nové přiřazení i načtení šablony z JSON dají
  novou referenci = invalidaci). `SetImmutable()` před `SKImage.FromBitmap` → pixely se sdílí,
  ne kopírují. **Neúspěch dekódování se cachuje taky** - poškozená data se dřív zkoušela dekódovat
  znovu každý frame. `GetImageAspectRatio()` čte z téže cache (dřív dekódovalo zvlášť).
- **Čárový kód** drží hotovou nahrávku kreslení (`SKPicture`) v lokálních souřadnicích, per frame
  zbyl jeden `DrawPicture` s posunem. Klíč = obsah × velikost v px × typ × barva kódu.
  Nezakódovatelný obsah (např. EAN-13 s jiným počtem číslic) se taky cachuje - dřív ZXing
  vyhazoval výjimku při každém framu.
- **Slití souvislých úseků modulů do jednoho obdélníku** - ZXing vrací matici v **pixelech cílové
  velikosti, ne v modulech kódu**, takže se u velkého QR kreslilo ~160 tisíc obdélníčků po jednom.
  Sousední buňky se kvůli `+0.5` překrývají, takže sjednocení úseku je bez vyhlazování **přesně
  tentýž obrazec** - odtisky to potvrdily. Tohle byla ve skutečnosti hlavní cena, ne zakódování.
  ⚠️ Kdo bude sahat na to `+0.5`, ruší tím i platnost slévání.

**Změřeno** (jedno překreslení plátna, 4 px/mm): QR 100 mm **43 → 1,0 ms**, QR 40 mm 6,4 → 0,3 ms,
Code 128 2,2 → 0,2 ms, obrázek 1400 px 20,2 → 4,6 ms. Drobný obrázek (64 px) se nezlepšil - u něj
nikdy nestálo čas dekódování, ale samo kreslení.

**Ověřeno:** odtisk pixelů 15 případů (4 režimy škálování, poškozený obrázek, 6 typů kódu, vlastní
barva, nezakódovatelný obsah, prázdný obsah, nekvadratický kód na necelých souřadnicích) je před
zásahem a po zásahu **identický**. Nové `CachedContentRenderTests` (13 testů) hlídají, že cache drží
i že se správně zahazuje, a že odtisk vyhřáté instance == odtisk čerstvé. Sada 622/622.

**Bezpečnost cache vůči vláknům:** export na pozadí běží nad `DeepClone` snapshotem šablony
(`MainWindow.xaml.cs`, `TemplateDesignerViewModel`), takže instance elementu nikdy nekreslí dvě
vlákna současně - stejný předpoklad, na kterém stojí i cache prvků JŘ.

## 2026-08-04 (17) - celkové review: mechanické opravy + evidence větších nálezů

Kompletní review sweep aktuálního stavu v 6 dimenzích (UI/UX konzistence, srozumitelnost pro
uživatele, architektura, udržitelnost, Caché strana, výkon+robustnost lightweight) - 6 paralelních
agentů proti vlastním pravidlům projektu. Celkový verdikt: **zdravé** (hranice vrstev drží, R1-R5
vynucené strukturou, wire a err-pattern v Caché ~100 %, em dashe 0, hardcoded barvy 0, zkratky 1:1
s kódem). Mechanické nálezy opraveny hned, větší evidovány jako `B52`-`B58`:

**Opraveno:**
- **Nápověda odkazovala na zrušený parametr** „Poznámky zadané u linky" (2 helpTexty filtrů bloku
  poznámek) - úklid přesunu (7) zůstal v půlce; stejně tak stale kopie zrušeného parametru
  `SCHOVAT_AGREGAT_BEZBARIER` v `SystemTemplates/zastavkovy-jr-ad.json` (offline fallback by ukázal
  mrtvý přepínač).
- **helpText Viditelnosti řádku učil pole zrušená migrací 1.3.0** (`{JE_AKTUALNI} == 1`) - opraven
  na skutečná (`NAZEV_ZASTAVKY`, `AKTUALNI_ZASTAVKA`, `NA_ZNAMENI`…).
- **Scalar override u textu anotací mřížky** - `AnotaceFontSize` přebíjel uživatelovu Velikost
  z tabu Obecné; guard nově kontroluje i `DataStyle` (3. výskyt téže třídy bugu → extrahovaný
  `ResolveAnotaceTextStyle` + 2 regresní testy v `DepartureGridCascadeTests`).
- **Formulář poznámek lhal o pozadí záhlaví** (F0F0F0 vs. tištěné E0E0E0 z kaskády) a o defaultu
  řazení; labely „NZ pozadí/barva/znak" rozepsány („Pozadí značky", „Barva textu značky"…).
- **Tisk bez vybrané linky** - hláška nově navádí na štítek výběru linky (dřív jen „Databáze
  nevrátila žádná data").
- Kanon: „Nahoru/Dolů" → „Nahoře/Dole" v editoru stylů buněk; tykání → vykání (chip instance,
  výběr agendy); F1 zbavena zakázaných tvarů („Zamčeno", „Viditelnost") a odkazu na neexistující
  „lištu nad náhledem".
- Caché: guard prázdného `AtributID` v MHD atributech (2 místa, `<SUBSCRIPT>` z dat bez Itm);
  `quit` s argumentem uvnitř bloků → `ret` (Faktury, LinkovyJrAd, 2 testy); stale komentář
  „KnizniJrAd".
- Docs: checklist nové agendy v `cache-api.md` naváděl na `agendas[]` v appsettings (reálně
  auto-discovery přes `AgendyRegistr`); `mvvm-patterns.md` cituje `Grid` místo zrušeného
  `GridColor` a hierarchii se 3 úrovněmi (StopScheduleElementBase); `feature-design-principles.md`
  §6 mrtvý příklad D5; `ux-conventions.md` §8 stale label; publish cesta v CLAUDE.md.
- Smazány 3 vytěžené sondy (`AdWTrasaProbe`, `AdProdlouzeniProbe`, `AdVariantyProbe`).

**Evidováno:** `B52` 🛑 rozhodnutí - migrace persistovaných voleb zrušených parametrů (kola/
místenky tiše zmizí uživatelům, kteří je měli zapnuté) · `B53` per-frame dekódování obrázku
a čárového kódu · `B54` drift test defaultů formulář↔C# · `B55` testy migrací + parsingu agend ·
`B56` dedup Build* helperů Caché agend (~400-500 řádků) · `B57` přerostlé skupiny formulářů ·
`B58` srozumitelnost pro nováčka. Checklist na ruční proklik: `review-proklik-checklist.md`
(jednorázový, po vytěžení smazat).

## 2026-08-04 (16) - nabídka datových polí místo psaní `{POLE}` (`B44`)

Název pole se psal rukou a překlep se poznal **až v tisku**. Vedle pole je nově tlačítko, které
otevře hledatelný seznam polí agendy a vybrané **vloží na pozici kurzoru** - hodnota bývá věta
s tokeny uvnitř (`Linka {CISLO_LINKY}: {POPIS_LINKY}`), ne jen samotný token, takže výběr celé
hodnoty comboboxem by druhý případ znemožnil. Hledání drží sdílený `EDFilterComboBox`.

Zapnuto tam, kde se `{POLE}` opravdu dosazuje: obsah textového prvku, **podmínka viditelnosti**
(u každého prvku i sekce), viditelnost řádku tabulek, obsah čárového kódu a vzor názvu souboru
při exportu. Vynechaná zůstala pole nesoucí **holý název bez závorek** (datové pole obrázku,
kolekce řádkové tabulky) - tam by vložení tokenu hodnotu rozbilo.

**Dělba práce:** server v `.cls` označí příznakem pole, kam placeholdery patří; **seznam polí zná
jen klient** (ze schématu agendy, Caché agendu nezná).

**Poučení - u obousměrného kontraktu nestačí přidat sloupec na obou stranách:** formulář chodí
tam a zpět (Validate), takže aditivní pole se musí přidat do **zápisu i čtení na obou stranách** -
čtyři místa, ne dvě. Chybějící čtení v `Form.cls` zahazovalo příznak na zpáteční cestě (tlačítko
se po prvním kole schovalo); tentýž tvar chyby pak podruhé vyprázdnil seznam polí. Seznam proto
po drátě nejede vůbec - je to klientský stav (`EDInputForm.PlaceholderFields`).

**Trojí statická kontrola tenhle rozpor odhalit nemohla** (server posílá ✓, parser čte ✓, DLL
čerstvá ✓) - každá se dívala na jeden směr. Rozhodla až sonda logující, co dorazí při **každém**
průchodu. Nasazení `.cls` se ověřuje **stažením ze serveru**, ne čtením zdroje v repu.

## 2026-08-04 (15) - VRÁCENO: `DeferRefresh` v EDListu shazoval výběr linky

Optimalizace plnění seznamu z (8) (`B51`) **shazovala aplikaci** při výběru linky:
`InvalidOperationException` - „Nelze změnit nebo zkontrolovat obsah nebo Current pozici
CollectionView během odkladu Refresh".

**Proč to byl špatný nápad:** `ICollectionView.DeferRefresh` neodkládá `CollectionChanged`.
`Clear`/`Add` nad `ObservableCollection` je posílají dál a `ListCollectionView` při jejich
zpracování sahá na currency - což je během odkladu zakázané. Odklad je určený pro změny
**pohledu** (filtr, řazení), ne pro hromadnou mutaci zdrojové kolekce.

Vráceno na původní plnění po jednom. Škubnutí při psaní do filtru skupiny je menší zlo než pád
při výběru linky; komentář v kódu teď říká, proč to znovu nezkoušet.

⚠️ **Poučení, které bolí:** testy tohle nechytily a nechytí - je to chování WPF za běhu nad reálným
`DataGrid`em. Zelená sada u změny, která sahá na `CollectionView`, negarantuje nic; takovou změnu
je nutné proklikat, a to jsem u (8) neudělal.

## 2026-08-04 (14) - řazení řádků v dynamické tabulce (`B27`)

Dynamická tabulka tiskla řádky v pořadí, v jakém je vrátila databáze, a uživatel s tím nemohl nic
dělat. Nově si vybere **sloupec a směr**; druhý klíč se odkryje, až když je vyplněný první, aby
panel zůstal krátký. Prázdné = dosavadní chování, takže existující šablony se nemění.

**Na typ řazení se neptáme** (rozhodnutí Jan): číslo, datum i text se poznají z hodnot. Volba
„abecedně × číselně" vypadá jako služba uživateli, ale špatná volba vyrobí výstup, který vypadá
seřazeně a je v nesmyslném pořadí - a nikdo netuší, že textově je „12" před „9". Smíšený sloupec
se řadí jako text (kdyby stačilo „něco se naparsovalo", pořadí by záviselo na náhodě), text jde
českou abecedou (aby „Ř" bylo za „R") a **prázdné hodnoty jdou vždy dozadu, i sestupně**.

**Comboboxy, ne mini-jazyk** (rozhodnutí Jan): `ORDER BY CENA DESC` by znamenalo psát názvy polí
rukou - a to je přesně vada, kterou má řešit `B44` (překlep se projeví až v tisku). Nabídku sloupců
plní klient, stejně jako u pole „Zdroj dat"; Caché je nezná, jsou per element.

**Jen dynamická tabulka.** U zastávek a odjezdů určuje pořadí trasa a to se řadit nesmí; pevná
tabulka má pořadí řádků od uživatele.

Počítané sloupce (`{CENA}*{POCET}`) se v1 nenabízejí - bez vyhodnocení výrazu není co porovnat.

Kromě testů řadicí logiky přibyl i test **zapojení** - že prvek `RowSorter` opravdu volá a že se
řazení skládá s filtrem řádků. Izolovaný test algoritmu by „hotové, ale nezapojené" nechytil,
a přesně to 4. 8. nastalo dvakrát (allowlist parametrů, nenasazený formulář).

⚠️ **Otevřeno pro pozdější doladění nad lepšími daty** (rozhodnutí Jan): počítané sloupce v nabídce,
a případné vyladění chování `Max. řádků` v kombinaci s řazením (dnes = seřaď, pak vezmi prvních N,
což je asi to, co člověk čeká, ale nikdo to zatím nezkoušel na reálné tabulce).

.NET 1546/1546 (TD 604, +13).

## 2026-08-04 (13) - repertoár symbolového fontu změřen (příprava `B10`)

`B10` (symboly pro linkový JŘ) měl jako největší neznámou otázku „který kód nese který piktogram" -
katalog 66 symbolů z legacy nese **mnemonické znaky**, ne pozice ve fontu, a doklad chyběl.

Vytaženy obrysy **všech 94 tisknutelných pozic z obou fontů** a zapsány do katalogu
(`elementy-architektura.md`). Zjištění:

- **Pozice z katalogu se s pozicí ve fontu často neshoduje** - letadlo je na `n` (ne `O`), paragraf
  na `P` (ne `G`), metro na `#` (ne `M`). Je to táž past jako autobus `A`→`z`, jen v širším měřítku;
  implementace tedy musí jít přes mapu, ne přes písmeno.
- **CZ a SK se liší přesně ve 3 z 94 pozic** (`D`, `w`, `x`) - zbytek je shodný. Sdílená mapa proto
  bude potřebovat rozlišení jazyka jen pro tyhle tři, ne plošně. (Rozšiřuje `B49`, které měřilo 5 znaků.)
- **Ve fontu není loď, wifi ani zásuvka** (katalog 61/65/66) - ty se z něj nakreslit nedají a při
  implementaci potřebují jiný zdroj nebo text.

Vlastní implementace `B10` se nedělala (rozhodnutí Jan: teď jen ověřit pozice) - blok v roadmapě
má nově změřený popis a rozhodnutý rozsah (sdílená mapa pro všechny prvky).

## 2026-08-04 (12) - gate k přepínači přestupů změřen (`B48`)

Blok se zakládal na obavě, že atribut firmy s modulem MHD s kódem 65/76 by se spletl s přestupem
a přeložil na cizí piktogram. **V číselníku `CB_DP` ty kódy nejsou** a kolize je nepravděpodobná
i strukturálně: agenda MHD posílá `$c(SpecZnak)`, tedy pozici ve fontu, ne písmeno z číselníku.

**Měření našlo jinou věc:** MHD posílá přestupy jako `w`/`x`, kdežto `IsTransferAttr` je hledá podle
písmene `A`/`L`, takže je u MHD nepozná. Přepínač „Přestupy (vlak/autobus)" na ně proto nemá vliv
a řídí je obecný přepínač „Atributy" - tisknou se správně, jen se ovládají jinak než u AD.
Nekonzistence ovládání, ne vada výstupu → přesunuto mezi „Bez poptávky nebrat".

Číselník `CB_DP` v `cache-database.md` doplněn na kompletní naměřený stav + poznámka, že sloupec
`Znak` na wire vůbec nejde (jinak by se srazil s naší mapou glyfů, kde `A`/`L` znamenají něco jiného).

## 2026-08-04 (11) - pozice glyfů ve slovenském fontu ověřeny (`B49`)

Symboly u spojů (autobus, vlak, kolo) se kreslí znaky ze speciálního dopravního fontu a slovenská
verze fontu je vlastní soubor. Mapa pozic byla ale změřená jen proti české verzi - u slovenského
zákazníka tedy hrozilo, že vyjede cizí piktogram. BBSK se na návrhář aktivně ptá (#13070), takže
to nebylo hypotetické.

**Výsledek měření: nic se netisklo špatně.** Kódy, které překládáme, sedí v obou fontech - `z`
autobus, `x` vlak, `L` kolo (u `z` a `L` je obrys dokonce **identický**, u `x` je to lokomotiva
proti moderní jednotce, tedy jiná kresba téhož významu). `SymbolGlyph` proto culture parametr
nepotřebuje.

**Vada byla v komentáři:** `FontResolver.cs` tvrdil, že „znaky jsou pro CZ i SK stejné, liší se jen
tvary glyfů". Měření to vyvrátilo - **`w` je v CZ železniční vagon, ale v SK autobus** a **`D` je
v CZ křížek, v SK dlaň**. Kdo by se tím komentářem řídil a namapoval autobus na `w`, tiskl by
Čechům vagon. Komentář opraven; měřená tabulka CZ × SK je v `elementy-architektura.md`, ať se to
nemusí opakovat.

Přibyl test, že žádná sémantická značka necílí na `w` - jediný z používaných kódů, jehož význam
závisí na jazyce.

.NET 1533/1533 (TD 591, +3).

## 2026-08-04 (10) - mřížka tabulkových prvků jako jedno sdílené API (`B40`)

**Rámeček = vnější obrys, mřížka = vnitřní čáry** (rozhodnutí Jan). Do teď vedly k vnějšímu obrysu
dvě cesty - rámeček, který má **každý** prvek, a vlastní rámec tabulky zadrátovaný v kódu. Nešlo
tedy jen o chybějící nastavení, ale o duplicitu konceptu.

Nový sdílený objekt `GridStyle` (řádky · sloupce · barva · tloušťka mm · styl) drží **jen vnitřní
čáry** a používá ho všech pět tabulkových prvků. Kompozicí, ne děděním - tabulky nejsou
`ScheduleElementBase`, takže společný předek by nepomohl. V Caché je to balíček polí
`BaseElementFields.AddGrid(form)` vedle `AddBorder`, takže formulář prvku je o jeden řádek delší
a popisky se skupinami hlídá drift test (přibyl `PoleMrizky_MajiVCsStejnyPopisekISkupinu`).

**Co to uživateli dá:** pevná i dynamická tabulka mají poprvé nastavitelnou barvu, tloušťku a styl
čar - dosud to bylo zadrátované a nešlo změnit vůbec. Mřížka odjezdů a tabulka linky nově umí čáry
vypnout. A ovládá se to všude stejně jako rámeček.

**Vedlejší oprava:** čáry tabulek byly v **px** (`0.7f` vnější, `0.5f` vnitřní), takže neškálovaly
se zvětšením ani s tiskovým rozlišením, zatímco všechno ostatní ano. Nově mm jako jinde.

**Migrace 1.6.0 → 1.7.0** (`MigrationGridStyle`) drží vzhled: skaláry JŘ prvků 1:1, přepínače
zapnuté (kreslily se vždy), tabulkám se dosavadní zadrátovaný rámec přenese do rámečku elementu.
Nová tabulka si ho přednastaví v konstruktoru, aby vypadala jako dosud.

**Ověření, které za to ručí:** odtisky renderu vyšly u všech čtyř JŘ prvků **bajt po bajtu stejně**,
změnil se jediný - dynamická tabulka, přesně jak bylo zamýšleno. ⚠️ Odtisk volá jen `Render()`, ne
`RenderBorderPass()`, takže vnější rámec z rámečku elementu **neověřuje** - ten Jan proto prošel
očima v běžící aplikaci a odsouhlasil (4. 8.).

⚠️ **Pozor při nasazení:** formuláře na Caché se poprvé nasadily až dodatečně - editace `.cls`
skriptem z Bashe nespustí deploy hook, takže na serveru zůstaly staré formuláře a projevilo se to
jako „pole je prázdné a uložení nic neudělá". Drift testy to nechytnou (čtou `.cls` z repa, ne ze
serveru). Detail a postup vynuceného deploye → root `CLAUDE.md`.

.NET 1530/1530 (TD 588, +1 drift test) · Caché 73/73.

## 2026-08-04 (9) - filtrované seznamy načítají asynchronně + mrtvá legacy větev pryč

Dojetí úklidu po vydání verze (rozhodnutí Jan: jít do sdílené komponenty celé, ne obezličkou).

### Výběr šablon z databáze zamrzal stejně jako picker instancí

Zbývající synchronní cesta: `CacheTemplatePickerDialog` volal `InitializeList()` a data načítal
`EDFilter.Refresh()` → `Target.LoadList()`, obojí na UI vlákně - a filtr to dělá při otevření
**i při každé změně hodnoty pole**. Opravit to jen v návrháři nešlo, protože data tečou přes
`EDFilter`, což je sdílená komponenta.

- `EDFilter` dostal **`RefreshAsync()`**; automatický reload (Loaded + změna hodnoty) jde nově tudy.
  Synchronní `Refresh()` zůstává pro ruční trigger, takže API je aditivní.
- Fire-and-forget z event handleru musí chybu spolknout na místě (`async void` by ji vyhodil mimo
  jakýkoli catch) - jde přes `IPopupDisplayer` z `EDServices`.
- Výběr šablon v návrháři: `InitializeListAsync()` + reload po smazání/přejmenování taky async.

⚠️ **Dotýká se i EDISONu** - všechny filtrované seznamy tam přestanou mrazit UI. Jednotkové testy
prošly, ale je to změna chování ve sdílené knihovně, takže stojí za proklik.

**Pořadí načtení sloupců vs. řádků se nezměnilo** (kdo z Loaded běží první, běží první i teď) - jen
už se to serializuje přes zámek v EDListu. Pokud tam někdy vznikne problém, je to pre-existující
vlastnost, ne důsledek async.

### Mrtvá legacy větev v MHD agendě

`ZastavkoveJrMhd` filtroval atributy spoje podle `^||ML("IN","par_2_"_AtributID)` - klíče, které
**nikdo neposílá**; parametr `NETISKNUTE_ATRIBUTY` z komentáře nikde neexistuje. Podmínka byla vždy
nepravdivá, takže smazání je chováním neutrální. Ověřeno grepem přes celé repo (žádný jiný výskyt)
a Caché testy 73/73.

⚠️ **Na co se tady NEDÁ spolehnout:** zlaté pokrytí MHD agendy je slabé (viz `C9`), takže zelené testy
tu nejsou důkaz. Jistota stojí na tom, že `$d` nad nikdy nenastaveným klíčem je vždy 0.

`ServerSideParamsDriftTests` tím ztratil reálný vzorek dynamického klíče, takže jeho test parseru
**přestal být platný** (procházel by naprázdno) - přepsán na vlastní vstup místo souboru z repa.

.NET 1529/1529 · Caché 73/73.

## 2026-08-04 (8) - pojistka allowlistu parametrů (`B47`) + plnění seznamu (`B51`)

Dva obecné nálezy dojeté po vydání verze.

### `B47` - drift test na `ServerSideParams`

Past, která se vrátila **čtyřikrát**: parametr, jehož hodnotu zpracovává server v `GetData`, musí být
v allowlistu `ServerSideParams`, jinak jeho přepnutí v UI nespustí re-fetch - klik nedělá nic a tisk
vyjede jinak než náhled (tiskový dialog si data tahá čerstvě). Nově to hlídá test: vyparsuje
z agendových `.cls` čtení `^||ML("IN","KLIC")`, odečte infrastrukturní klíče a ověří podmnožinu.

**Test našel čtvrtý výskyt hned:** `DELENI_SLOUPCU` („Dělení spojů dle platnosti") čte `LinkovyJrAd`
v `GetData`, ale v allowlistu nebyl. Latentní, protože linkový JŘ ještě není zveřejněná agenda -
doplněno.

Testuje se **oběma směry** (chybějící klíč = tichá vada, mrtvý klíč = zbytečný re-fetch); ty dva testy
se navzájem hlídají, protože kdyby parser zrezivěl, směr „mrtvé klíče" spadne na celém allowlistu.
Pojistka ověřena mutací - s odebraným klíčem test spadne.

⚠️ **Dynamické klíče parser vynechává**: `ZastavkoveJrMhd` čte `^||ML("IN","par_2_"_AtributID)`, což je
rodina klíčů skládaná za běhu, ne název parametru. Mimochodem je to **mrtvá legacy větev** - klíč nikdo
neposílá a `NETISKNUTE_ATRIBUTY` z komentáře nikde neexistuje. Neřešeno, jen zaznamenáno.

### `B51` - seznam se plnil po jednom řádku

`EDList` přidával řádky jeden po druhém do kolekce navázané na `DataGrid` s filtrem, takže 200 řádků
znamenalo 200 oznámení o změně a 200 přepočtů pohledu, a nakonec ještě celý `RefreshFilter()`.
`DeferRefresh` (s nastavením filtru uvnitř) to zkolabuje do jednoho průchodu.

Projevilo se to až po přechodu pickeru na async: načtení dosedá, zatímco uživatel píše do filtru
skupiny, takže to škubalo. Dřív bylo schované za zámrzem synchronního načítání.

⚠️ **Sdílená komponenta** - dotýká se všech konzumentů EDListu včetně EDISONu. Jednotkové testy prošly,
ale je to změna chování při plnění seznamu, takže stojí za proklik i tam.

.NET 1529/1529 (EDControls 507, TD 587 (+4), EDISONMAIN2 435).

## 2026-08-04 (7) - zobrazení obsahu jen v šabloně, čtyři parametry pryč (rozhodnutí Jan)

**Rozhodnutí Jan 4. 8.:** *„Za mě je důležité, co vidí zákazník, ne co posílá server. Nemělo by to
fungovat tím způsobem, že zákazník uvidí něco duplikovaně kvůli nějaké interní logice."* Poznámky
linky, kola, místenky a souhrn bezbariérových spojů se zapínaly **parametrem sestavy** a zároveň
filtrovaly **v bloku poznámek** - dvě místa pro jednu otázku.

Server teď posílá vše otagované kategorií a rozhodnutí drží **jen šablona** (blok poznámek, sekce
Filtrování). Zrušené parametry: `POZNAMKY_LINKY`, `TISK_MISTENEK`, `TISK_KOLA`,
`SCHOVAT_AGREGAT_BEZBARIER`. V „Poznámky a legenda" zůstal souhrn na znamení (mění, **co se
generuje**) a rozsah nepoznámkovat.

- **Značka a vysvětlivka visí na jedné volbě** - filtr prořezává anotaci u minuty i řádek legendy
  podle téže kategorie, takže nemůže vzniknout značka bez vysvětlení ani vysvětlivka bez značky.
- **Kola a místenky nejdřív sedly na mřížku odjezdů** (prvek, který značku kreslí - vzor na-znamení
  a přestupů), ale **Jan rozhodl přesunout je k ostatním do bloku poznámek**: pro mřížku to per
  kategorii nebyl precedens (měla jen hromadné „Anotace") a přednost má jedno místo pro uživatele.
  ⚠️ Důsledek: šablona **bez bloku poznámek** tyhle značky nezapne vůbec (drží test).
- **Výluky nedostaly čtvrtý vypínač** - jsou to všeobecné poznámky (v datech táž kategorie, poznáme
  je podle prefixu), takže se zapínají s nimi a enum je jen rozděluje mezi bloky. Bez toho by
  naskočily všem existujícím šablonám, protože server je teď posílá vždy. Odhalily to spadlé testy.
- **Výchozí stav všeho nového = vypnuto**, takže existující šablony vypadají stejně. Zlaté odtisky
  se proto změnily **jen v počtu poznámek** (157→235, 198→236); stránky, zastávky ani odjezdy ne -
  to je podpis „server posílá nadmnožinu, klient filtruje".
- **Popisky srovnané s Edisonem:** `JRapiLin1` ř.250-252 pojmenovává ta pole
  `PoznamkyDruhPoznamkyProLinku`**Tarifni**/**Vseobecne**/**Radiobus** a má u nich přepínače
  Tisknout A4/A5. Filtr proto používá stejná slova a prefix „Poznámky linky - ". Klient v zadání
  psal „Poznámky 1, Poznámky 2 apod", ale Edison je nečísluje - čísla jsou interní klíče uzlu.

**Cílový model (checklist z metadat) zatím ne, a blokátor je pojmenovaný:** `ElementPropertyAccessor`
umí navázat jen property cesty (`A.B.C`), **ne mapu** - generovaný checklist by nejdřív chtěl rozšířit
binder nebo přidat typ pole do EDControls. Použité obyčejné vlastnosti: **ubyly 4 parametry, přidaly
se 3 volby** → méně přepínačů a každá kategorie na jednom místě.

### Dvě chyby cestou, obě stejného druhu (ověřoval jsem něco jiného, než co platí)

1. **Odstranění parametrů se nedostalo na server.** Editoval jsem `.cls` **skriptem**, a hook nahrává
   jen editace nástrojem - zdroj v repu parametry neměl, server je měl dál a build je zobrazoval.
   Testy to nemohly zachytit (v odtiscích se parametry neprojevují). `cache_compile` to nespraví:
   hledá zdroj pod `C:\DEV\EDISON`, takže zkompiluje starou serverovou kopii a řekne „up-to-date".
   **Jediné spolehlivé ověření je zavolat agendu a podívat se, co reálně vrátí.**
2. **Kolo se zas kreslilo jako cizí glyf.** Při rozdělení `odjezd-atribut` na `odjezd-kola`
   a `odjezd-mistenka` zůstal seznam sémantických kategorií (těch, jejichž značka se **překládá na
   glyf**) na starém jménu. Testy byly zelené, protože si to staré jméno držely samy.
   → Jména kategorií mají jediný zdroj `Models/NoteCategories.cs`, testy je berou odtud a nová
   pojistka hlídá, že markerové kategorie v sémantické sadě opravdu jsou.

Caché 73/73, .NET 583/583. Release build EDControls i TemplateDesigner bez chyb.

## 2026-08-04 (6) - výběr instancí načítá data asynchronně (zámrz na pomalé lince)

Jan hlásil, že **při prvním otevření výběru linek v AD zamrzne aplikace na ~15 s**, když do filtru
skupiny napíše číslo; podruhé už je to rychlé.

**Kořen byl v už opraveném `B46`** a tohle je jeho druhá polovina. Starý ručně psaný ComboBox volal
při každé změně výběru `ReloadInstances()`, a psaní filtru výběr měnilo jako vedlejší efekt (viz
changelog (3)) - takže **každý napsaný znak spouštěl synchronní dotaz na server**. `B46` to odstranil,
proto zámrz zmizel. Zůstala ale příčina, proč to bolelo tak moc: **ta volání jsou synchronní na UI
vlákně**, takže cokoli pomalého mrazí celou aplikaci.

Serverová strana přitom rychlá je - naměřeno proti dev serveru na `CB_CSADAD`: `GetAvailableSkupiny`
**58 ms**, `GetAvailableInstances` **94 ms**, triviální ping 280 ms, a seznam instancí je navíc
limitovaný na 200 řádků. Šlo tedy o latenci linky, ne o data ani dotaz.

Nově je celý picker async (R1):

- `EDList` dostal **`InitializeListAsync()` a `LoadListAsync(filter)`** (aditivně, synchronní zůstávají
  pro ostatní konzumenty). Dotaz běží na threadpoolu, výsledek dosedne na UI vlákně.
- Async varianty **serializují souběžné dotazy** - sdílený `MLRest` jede přes procesní globály
  `^||ML`, takže dva souběžné by si přepsaly vstup i výstup - a **odpověď staršího dotazu už
  nedosedne** (generace), takže rychlé přepínání skupiny nezamíchá řádky.
- Chyba načítání jde přes `IPopupDisplayer` (R2), což u `async void` handlerů není detail - bez toho
  by výjimka spadla mimo dialog. **Overlay „probíhá akce" tam vědomě NENÍ** (zkoušel jsem ho a Jan ho
  vyhodil): načtení je typicky ~100 ms a překryv z rychlé operace dělá opticky pomalou. Async je tu
  kvůli tomu, aby pomalá linka nezmrazila aplikaci, ne aby se čekání zobrazovalo.
- `GetAvailableSkupiny` před otevřením dialogu jde taky na pozadí (`OpenInstancePickerCommand` je
  nově `AsyncRelayCommand`).

⚠️ **Stejný vzor má i výběr šablon z databáze** (`CacheTemplatePickerDialog` volá `LoadList`
synchronně) - na pomalé lince zamrzne stejně. Neopraveno, není odtud hlášené.

.NET 1525/1525 (EDControls 507, TD 583, EDISONMAIN2 435).

## 2026-08-04 (5) - export i tisk posílají tytéž parametry co náhled (`B50`)

Nález z review (4): **export PDF/PNG z lišty ignoroval parametry sestavy.** Fetchoval `GetData` jen
s instancí a klientské parametry nemergoval do stránek, takže výstup vyjel s výchozími hodnotami -
bez písmen variant, poznámek linky, kol a místenek, bez rozdělení po stanovištích - i když náhled
na obrazovce ukazoval ty volby zapnuté. Uživatel to pozná až z vytištěné vývěsky.

**Při opravě se ukázalo, že tisková cesta má zrcadlovou vadu:** neposílala `SKUPINA_LINEK_ID`.
U AD je linka klíčovaná dvojicí (skupina, linka), takže s vybranou nedefaultní skupinou mohl tisk
vytáhnout linku z jiné skupiny, než kterou si uživatel vybral v pickeru.

Tři cesty (náhled, export, tisk) si tu mapu skládaly každá zvlášť a každá zapomněla něco jiného.
Nově je to **jedno místo**: `ComposeGetDataParams` (parametry sestavy + klíč instance + skupina)
a `MergeParamsIntoPages` (domíchání hodnot do stránek pro C# renderování). Devět testů na obojí,
včetně toho, že instance přebíjí klíč linky z parametrů a že prázdná skupina se neposílá jako
prázdný řetězec (server si má vybrat default).

⚠️ **Chování se mění tam, kde bylo dosud špatně** - export teď respektuje nastavení a tisk sahá
do správné skupiny. Kdo si na starý export zvykl, uvidí jiný (správný) výstup.

.NET 1519/1519 (EDControls 507, TD 577 (+9), EDISONMAIN2 435).

## 2026-08-04 (4) - produkční review týdne: opravené nálezy před verzí

Celkové review změn za ~poslední týden (4 dimenze: business logika poznámek, wiring parametrů,
UI/UX texty, glyfy/render) před vydáním verze. Testy byly zelené už před opravami; review našlo
tři funkční díry a pár textovek. Opraveno:

- **Dva parametry vypadly z `ServerSideParams` allowlistu** - „Souhrn všechny zastávky na znamení"
  a „Výčet spojů u dopravce" jsou server-side (čte je `GetData`), ale jejich změna nespouštěla
  re-fetch: klik byl no-op a **tisk pak vyjel jinak než náhled** (PrintDialog fetchuje čerstvě).
  Třetí výskyt téže pasti → strojová pojistka jako `B47`.
- **Prázdný blok legendy při tisku zmizí celý** - blok „Jen výluky" na lince bez výluk dřív vytiskl
  osiřelý nadpis nad prázdnem a u dynamické výšky si držel 25mm placeholder, který odtlačil elementy
  pod sebou. Šablona se tiskne pro všechny linky a většina výluky nemá, takže by to bylo na většině
  vývěsek. Nově: reálná data + prázdný filtr = nekreslí se nic a výška je 0 (placeholder zůstává
  jen designeru bez dat). Dva regresní testy (výška + odtisk canvasu).
- **Terminologický drift „označník" → „stanoviště"** na 3 místech u #13088: description parametru
  „Číslo stanoviště podle" odkazoval na přejmenovanou volbu („Více označníků" → „Více stanovišť"),
  tooltip chipu a stavový řádek říkaly „rozdělení po označnících" - při volbě Sloupek věcně špatně.
- **Czechglish tooltip** „Smaže per-sloupec overrides" → „Smaže úpravy jednotlivých sloupců" (5 dialogů).
- **Formulář poznámek lhal o defaultu řazení** - fallback pole „Řazení poznámek" tvrdil „Výchozí",
  element defaultuje „Dle typu" (prefill to vždy překryl, jen kosmetika formuláře).

**Nálezy odložené se záznamem:** `B48` (StopList klasifikuje přestupy podle písmene - MHD riziko),
`B49` (SK font neověřené pozice glyfů), `B50` (export PDF/PNG ignoruje parametry sestavy),
`B47` (drift test allowlistu). Na radaru bez bloku: potlačení duplicitní vysvětlivky („X - text")
se rozhoduje na serveru podle parametru, ale viditelnost zákazníkovy poznámky až filtrem prvku -
krajní kombinace umí nechat značku „R"/„K" bez vysvětlivky; a zákazníkova poznámka „K - …" umlčí
piktogram kola, který si čtenář s textovým „K" nespáruje.

.NET TD 570/570 (+2), EDISONMAIN2 435/435, Caché 112/112. K nasazení: týden sahal i do EDControls
(náhled barvy z dat) → do produkce jde DLL + exe.

## 2026-08-04 (3) - hledání skupiny linek přepsáno na sdílený `EDFilterComboBox` (`B46`)

Nahlásil Jan: ve výběru linek se do filtru skupiny nedá napsat víc než jeden znak. Reprodukováno
z kódu, dvě příčiny v `InstancePickerDialog`:

- **Filtr jel o znak pozadu.** Příznak „uživatel píše" se zapínal v `PreviewKeyUp`, který u WPF přichází
  **až po** `TextChanged` - při prvním znaku byl tedy ještě vypnutý a filtr se přeskočil.
- **Druhý znak přepsal napsaný text.** Filtroval se `ICollectionView` nastavený jako `ItemsSource`,
  se kterým si ComboBox synchronizuje výběr. Vyfiltrování právě vybrané skupiny proto vynutilo změnu
  výběru → ComboBox zapsal do pole popis nové položky, handler `SelectionChanged` shodil filtr na
  prázdný a **přenačetl linky pro skupinu, kterou uživatel nevybral** (to bylo na tom nejhorší,
  protože to není vidět).

Oprava = **použít sdílený `EDFilterComboBox`** z EDControls (`StrictSelection`), který přesně tuhle
třídu problémů řeší (plní `Items` přímo, hodnotu drží v `Tag`, výběr commituje z `SelectionChanged`
zachyceného Key) a má na to testy. Vlastních ~35 řádků filtrování i se čtyřmi stavovými příznaky
zmizelo; výběr se teď snímá v `DropDownClosed`, kde ho komponenta fixuje. **Vzhled se nemění** -
výchozí separátor komponenty je `" - "`, což je totéž co `AgendaSkupina.Display`.

**Zrušena i druhá vlastní kopie** (rozhodnutí Jan: čistá deduplikace hned): nabídka datových polí
v panelu vlastností (`PropertiesPanel.AddSuggestRow`) jede na téže komponentě ve volném režimu.
Ta kopie bugem netrpěla (byla postavená správně), ale byla to třetí implementace téhož; její
dvojité mezery v „hodnota - popisek" drží `KeyLabelSeparator`, takže se panel vizuálně nemění.
Undo/redo commit na `LostFocus` zůstal v panelu, kam patří.

**Jeden rozdíl si vyžádal úpravu sdílené komponenty** (jinak by to nebyla deduplikace, ale
regrese): ve volném režimu brala komponenta klidovou hodnotu pole jako filtr, takže šipka nabídla
**jen právě nastavenou položku** a přepnout na jinou šlo až po smazání textu. Nově je filtrem jen
text, který uživatel skutečně napsal (`_textIsUserFilter`); programově nastavená hodnota filtr není.
⚠️ **Dotýká se i EDInputFormu** (tamní volné ComboBoxy měly tutéž vadu, teď nabídnou celý seznam) -
je to oprava, ale je to změna chování ve sdílené knihovně, tedy i v EDISONMAIN2.

.NET 1510/1510 (EDControls 507 (+2), TD 568, EDISONMAIN2 435).

## 2026-08-04 (2) - výběr kategorií poznámek linky (#13093 bod 1, zbytek)

V zadání stálo *„Dají se v šabloně aktivovat? **Příp. filtrovat Poznámky 1, Poznámky 2 apod?** Asi to
bude muset být v nějakém jiném poli než seznam poznámek s NZ, kvůli jinému rozložení sloupců."* -
druhou větu jsme minule přeslechli. Blok poznámek umí vybírat podle **druhu** (negativní značky /
speciální znaky / textové poznámky) a k tomu výluky, ale kategorie poznámek linky vybrat nešly:
„Textové poznámky" vypne poznámky linky **spolu s** ručními komentáři a rozdíly tras, což není filtr.

Nově tři volby v Filtrování: **Tarifní podmínky linky · Všeobecné poznámky linky · Radiobus poznámky
linky**. Kategorie server tagoval už od (11), takže je to práce jen v prvku - filtr, panel, formulář.

- **Vybírá jen mezi poznámkami linky.** Značky, omezení a ruční komentáře jím projdou vždy; jinak by
  vypnutá kategorie sebrala jako vedlejší efekt i je (hlídá test).
- **Výluky zůstávají vlastní volbou** - nefiltrují se podle kategorie zdroje, ale podle prefixu textu,
  takže jsou napříč kategoriemi (v datech leží ve „všeobecných").
- Výchozí vše zapnuté = existujícím šablonám se vzhled nemění.

⚠️ **Popisky jsou naše doménové názvy** (tarifní / všeobecné / radiobus), kdežto klient v zadání píše
„Poznámky 1, Poznámky 2" podle číslování ve vlastnostech linky. Slova jsou srozumitelnější než čísla,
ale pokud jeho záložka v JŘ čísluje, popisky se mají srovnat - **rozhodnutí na Janovi**.

.NET 567/567 (+4), Caché 73/73.

## 2026-08-04 (1) - kolo u odjezdu bylo cizí glyf, dvojí vysvětlivka „R" (#13093 bod 2)

Tři pozorování z reálné vývěsky KOVED. Dvě byly chyby, jedna ne.

- **Kolo u odjezdu se kreslilo jako cizí piktogram** („had"). Příčina: **mřížka odjezdů překlad glyfu
  vůbec nedělala** - kreslila datový znak surově, takže marker kol `K` vyjel jako glyf sedící na
  pozici „K". Legenda přitom překládala správně, takže značka a její vysvětlivka se rozešly.
  Opraveno; překlad teď jde přes `GlyfProKresleni` i při **měření** šířky slotu, aby se render
  a layout nerozjely. Dosud to nikdo neviděl, protože jediné dřívější glyfové anotace byly
  bezbariér `H`/`F`, které na svém písmenu sedí náhodou správně.
- **Legenda vysvětlovala „R" dvakrát** - jednou naše generovaná vysvětlivka, jednou zákazníkova
  vlastní poznámka u linky („R - … i pro přepravu jízdních kol na https://rezervace.idzk.cz").
  Nové pravidlo: **vlastní vysvětlivka zákazníka vyhrává** a naši pro tentýž znak zahodíme.
  Poznává se konvencí „`<znak> - text`" (pomlčka povinná, jinak by „V úseku…" umlčelo značku „V").
  Platí **jen na atributy odjezdu** - rozdílové poznámky tras mají písmena přiřazená námi za běhu,
  takže umlčet je zákazníkovým textem „A - …" by odstranilo vysvětlivku, na kterou ukazuje minuta.
- **Tučné „x" v legendě není chyba ani novinka** - letterové značky tam jdou tučně od první verze
  (`64cf76f`), piktogramy ne (symbolový font tučný řez nemá). Necháno, viz „potřebuje rozhodnutí".

Ověřeno na ZL_KOVED **skupina 2 „Pro CC" / linka 0823141**: v legendě je jedno „R" (zákaznické)
a řádek kol. Caché 73/73 (+1), .NET 563/563 (+1). Zlaté odtisky beze změny.

## 2026-08-03 (13) - kolo se tiskne, a autobus konečně taky autobus (#13093 bod 2, #13048 bod 1)

Glyf kola dohledaný → **kola se tisknou** (zápis (12) o „čeká na glyf" tím neplatí). Cestou se
ukázalo, že **ranní oprava přestupů byla jen z poloviny správná**: písmena jsme si opsali z Mapy
znaků a autobus jsme poslali na `w`, kde je ve skutečnosti **železniční vagon**. U přestupu na
autobus se tedy kreslil vagon - vypadalo to plausibilně, proto to prošlo i vizuální kontrolou.

- **Pozice čtené přímo z fontu, ne okem z Mapy znaků.** Obrysy glyfů vytažené z `TimeTable2.ttf`
  a vykreslené do kontaktního listu s popisky kódů - jediný způsob, jak to přečíst spolehlivě
  (jména glyfů font nenese, jsou obecná). Postup zapsaný v `elementy-architektura.md`.
  Platné pozice: `z` autobus · `x` vlak · `L` kolo · `w` **vagon** · `y` restaurace.
- **Pozice ověřené i ve slovenském fontu** - `TimeTable2SK.ttf` **není kopie**: `w` je tam autobus,
  `x` moderní jednotka. Naše tři volby (`z`, `x`, `L`) znamenají v obou fontech totéž, `w` je
  přesně ta jedna pozice, kde by se vzhled rozešel podle jazyka.
- **Kola jdou markerem `K`**, který render přeloží na glyf. Číselník pro kola znak nemá (je to
  atribut spoje) a poslat rovnou `L` nelze - to je vlak.
- **Překlad glyfu jen na piktogramové cestě.** Dosud se aplikoval i na značky kreslené normálním
  fontem, takže ruční komentář se značkou „A" by v legendě vyšel jako „z". Podmínka „je to
  piktogram" je teď na jednom místě (`JePiktogram`), dřív byla trojmo opsaná.

Caché 72/72, .NET 562/562 (+3). Ověřeno na ZL_KOVED 0823141 (linka s kolem): v legendě jsou obě
vysvětlivky. ⚠️ Piktogramy chce **projít očima** - stroj vidí jen znak, ne co je nakreslené.

## 2026-08-03 (12) - místenky u odjezdů, kola čekají na glyf (#13093 bod 2)

Klient hlásil, že se nezobrazují *„cyklobusové poznámky ani rezervace u času odjezdů (znak kola, R),
ani v poznámkách"*. **Data pro obojí existují** (KOVED, celá firma = 17 029 linek): přeprava kol =
19. pole věty spoje (**3 602 spojů**), místenkování = 6. pole (**6 739 spojů**); typ místenkování
nevyplňují. Není to tedy požadavek na jízdní řády.

⚠️ **Oprava 4. 8.:** původně tu stálo 166 a 310 spojů. To byl **vzorek prvních 800 linek** - sonda má
výchozí strop `MAXLIN`, kterého jsem si nevšiml, takže „změřeno na 800 linkách" ve skutečnosti nebyl
celý rozsah. Skutečná čísla jsou o řád vyšší. Featura je tím podstatně relevantnější, než jsme psali.

- **Místenky hotové:** u odjezdu se vytiskne **„R"** (prostý text, tak to zapisuje i zákazník ve svých
  poznámkách) a do legendy přijde vysvětlivka. Parametr **Místenky u odjezdů**, výchozí vypnuto.
- **Kola: plumbing hotový, ale zatím se netisknou** - v symbolovém fontu je piktogram kola, ale
  **neznáme jeho znak** a legacy ho na vývěsce nikdy netiskl, takže se není od čeho odrazit.
  `ZnakKola()` vrací prázdno a kolo se přeskočí; test `TestZnakKolaZatimNeni` to drží, aby to někdo
  nedoplnil odhadem. Po dohledání v Mapě znaků je to jednořádková změna.
  ⚠️ Hádat znak = vykreslit cizí piktogram, přesně chyba opravovaná dnes u přestupů.
- Legenda těchto atributů má vlastní kategorii `odjezd-atribut` a hodnota nese i **typ** (`typ~text`),
  protože kolo je piktogram a „R" text - jeden seznam, dva render-módy.

Ověřeno na ZL_KOVED 0822995 se zapnutým parametrem: v legendě přibyl řádek „R - k jízdence je možné
zakoupit místenku". Zlaté odtisky **beze změny** (oba parametry vypnuté).

Caché 72/72 (+1), .NET 559/559.

## 2026-08-03 (11) - poznámky zadané u linky, výluky zvlášť (#13093 bod 1, zbytek #13088)

Textové poznámky z vlastností linky se do zastávkové vývěsky nedostaly **vůbec**, i když je linkový JŘ
čte. Nově je vydává i zastávková agenda, s kategorií podle klíče (tarifní / všeobecné / radiobus;
klíč 2 legacy přeskakuje).

- **Záznam = jedna poznámka**, ne slepený blok jako legacy. Bez toho nejde výluka oddělit: KOVED má
  v kategorii „všeobecné" tři výluky **i** text „R - k jízdence je možné zakoupit místenku".
- **Výluky poznáme prefixem „Výluka"** (jejich konvence, tak to posílají do CIS) a dostanou vlastní
  kategorii. Prvek poznámek má novou volbu **Výlukové poznámky: Včetně / Bez výluk / Jen výluky**,
  takže si klient položí dva bloky nad sebe - přesně jak to popsal v tiketu.
- **Nové pole sestavy „Výlukové texty"** (`VYLUKOVE_TEXTY`) spojuje výluky do jednoho textu. To je ta
  „databázová položka pro návrhář" z #13088; pole je inertní, takže se negatuje parametrem.
- Text se pro vývěsku čistí: legacy `$$` pryč, `#` (oddělovač odstavců) na mezeru, protože legenda
  zalamuje sama.

⚠️ **Výchozí vypnuto** (parametr „Poznámky zadané u linky"). Zapnutím se do legendy přidá i dlouhý
tarifní text, takže existujícím šablonám se vzhled nesmí změnit sám. **Zlaté odtisky to potvrzují:
počty poznámek zůstaly shodné**, md5 pohnulo jen nové pole.

**Bod 4 tiketu (automatická výška bloku + zmenšení textu + druhý blok navázaný na první) je hotový
už dnes** - ověřeno 3. 8.: prvek má `CalculateHeightFromData` i `ComputeFontScale` a `PageLayoutEngine`
u dynamické výšky posune elementy pod ním. Není to vývoj, ale vysvětlení: první blok má mít výšku
prázdnou.

Caché 71/71 (+2), .NET 559/559 (+4).

## 2026-08-03 (10) - `B43` čtvereček ukáže barvu z dat, ne bílou (#12997)

Pole s barvou z dat (`{BARVA_LINKY}`) mělo čtvereček **bílý**, takže vypadalo jako nenastavená barva -
uživatele to mátlo (Jan při vizuální kontrole druhé barvy).

- **EDControls (aditivně):** pole formuláře má novou vlastnost **`PreviewValue`** - náhledovou barvu,
  kterou vyplní **host** (ten data má), a čtvereček se řídí `SwatchValue` = náhled, jinak hodnota.
  Komponenta ji do hodnoty **nikdy nepřepisuje**. Konvertor navíc pozná token `{…}` a vykreslí
  **šrafování** místo bílé, takže i bez náhledu je poznat „barva se vezme z dat".
- **Návrhář:** `ColorHex.ResolveDataBoundForUi` vyřeší token pro zobrazení a `ElementFormBinder.Prefill`
  (jediná cesta, kudy se formuláře plní) náhled naplní. Data pro UI drží `ColorHex.UiData`, které
  nastavuje kreslení náhledu - čtvereček tak ukazuje barvu **té stránky, kterou má uživatel před sebou**.

⚠️ **Hodnota zůstává tokenem.** Kdyby se do ní zapsala vyřešená barva, šablona přestane brát barvu
z dat a zamrzne na barvě linky, která byla náhodou otevřená - tichá regrese, kterou by nikdo neodhalil.
Hlídá to pět testů (`ColorHexUiPreviewTests`), včetně případů „bez dat", „linka barvu nemá" a „data
vrátila nesmysl", kde náhled musí vrátit nic a čtvereček padne na šrafování.

⚠️ **Nasazení:** změna je i v **EDControls**, takže do produkce musí jít **DLL i .exe** (viz root
`CLAUDE.md`). Druhé aplikace se to nedotkne - vlastnost je aditivní a nikdo ji nevyplňuje.

**Rozhodnutí (Jan 3. 8.): šrafování je akceptovaný cílový stav, přesná barva je bonus.** V praxi se
náhled naplní jen tam, kde náhledový canvas kreslil stránku s daty (v návrhářském pohledu data
nejsou) - a doháněla by se draho. Podstatné bylo, aby to nevypadalo jako nenastavená bílá.
`UiData` se proto **maže při každé změně dat náhledu**: lepší šrafování než barva staré instance,
protože cizí barva mate víc než neutrální stav.

.NET 555/555 (+5), EDControls 505/505, EDISONMAIN2 435/435.

## 2026-08-03 (9) - druhá barva linky (#12997)

Klient chtěl u linky **dvě barvy** - zvlášť pro číslo linky a zvlášť pro pozadí. Blokovalo to, že
jízdní řády umí jen jednu; **Petr to 3. 8. rozšířil** (`ALVA.ISY.ISYJR2.API.Linky`), takže blokátor
padl a dodělali jsme návrhářskou stranu.

- Nové pole agendy **Barva linky 2** (`BARVA_LINKY_2`), použitelné všude, kde jde barva zadat textem
  (od `B31`): `{BARVA_LINKY_2}` v barvě textu, pozadí, rámečku i u obdélníku.
- Náš helper bere **pořadí barvy**, `GetBarvaLinky(…, poradi)`. Bez pořadí = první barva, takže
  uložené šablony a existující volání zůstávají beze změny.
- **Neutrální pojmenování schválně.** Klient je používá jako „text" a „pozadí", ale JŘ jim význam
  nedává - je to seřazený seznam a Petr počítá s dalšími. Význam tedy určuje šablona, ne pole.
- Infotexty o barvě z dat (formulář prvků i tooltipy v editoru buňky, 6 míst) zmiňují **obě** pole -
  Petr o to výslovně žádal, ptalo se na to víc lidí.
- Klient nepotřeboval změnu: `ColorHex.IsDataBound` přijímá **jakýkoli** token `{…}`, ne pevný seznam.

Ověřeno na reálných datech: gold linka `CB_CSADAD 0365001` má nastavenou jednu barvu (`149;202;100`
→ `#FF95CA64`), druhá vychází prázdná. Test hlídá i to, že se pořadí nesmí ignorovat - jinak by obě
pole nesla tutéž barvu a nikdo by si toho nevšiml.

⚠️ **Dvě barvy naráz jsme neviděli** - v žádné testovací firmě není linka se dvěma barvami. Potřebuje
to nastavit v JŘ (Hromadné úpravy vlastností linky) a projít očima.

Caché 69/69 (+1), .NET 550/550. Zlaté odtisky přebaselinovány (jen md5, počty shodné).

## 2026-08-03 (8) - fix: přestupy kreslily cizí piktogram (#13048 bod 1)

Značka přestupu vypadala náhodně (trojúhelník a kolo místo autobusu a vlaku) - všiml si Jan při
vizuální kontrole. Příčina: do symbolového fontu jsme posílali **písmeno z číselníku** („A", „L")
a doufali, že tam ten piktogram je. Pozice glyfů v Timetable fontu se ale s písmeny číselníku
**neshodují**.

Naměřeno v Mapě znaků: **autobus = `w`** (0x77), **lokomotiva = `x`** (0x78), vagon = `z` (0x7A).
Překlad datového znaku na glyf drží `StopScheduleElementBase.SymbolGlyph` a používají ho **obě**
kreslicí cesty - značka u zastávky i znak v legendě (jinak by se rozešly a legenda by vysvětlovala
jiný symbol, než je vidět u zastávky).

**Potvrzuje to invariant „na znamení jde textem":** na pozici `x` je lokomotiva, takže piktogramová
cesta by u každé zastávky na znamení nakreslila vláček. Textová cesta (`x`/`y`/`z`) zůstala nedotčená
a je teď zamčená testem - to byla výslovná obava Jana a stojí na tom vzhled vývěsky.

⚠️ **Neověřené zůstávají** pozice pro clo, paragrafy, metro, letadlo a loď. Dnes je nemá v datech
žádná firma, ale první, kdo je bude mít, dostane cizí piktogram, dokud je někdo stejně nezkontroluje -
zapsáno v `elementy-architektura.md`, kde ten bod visel jako otevřený od návrhu symbolového fontu.

Caché beze změny (je to čistě render), .NET 550/550 (+3).

## 2026-08-03 (7) - přestupy mají řádek v legendě (#13048 bod 1)

Značku přestupu jsme kreslili, ale nikde nevysvětlili - piktogram autobusu nebo vlaku sám cestujícímu
neřekne, co znamená (upozornil Jan při vizuální kontrole).

- Caché vydává pro přestupy řádky legendy **s vlastní kategorií `prestup`** a textem „přestup na
  autobus" / „přestup na vlak" (samotný text číselníku je jen „autobus" / „vlak", což jako řádek
  legendy nedává smysl).
- **Zobrazí se jen když je prvek zastávek tiskne.** Caché posílá nadmnožinu, prořezává render-time
  filtr - `NoteVisibilityFilter.ShouldHidePrestupLegend` skryje kategorii, když šablona má seznam
  zastávek, ale žádný z nich nemá zapnuté Přestupy (výchozí stav). Přesně stejný mechanismus jako
  u „× na znamení" (#7161 D5), takže žádný nový vzor.
- Do signálu „něco je schované" se to **nepočítá** - neschovává to parametr sestavy, ale nastavení
  prvku; jinak by kontrolka svítila u každé běžné šablony.

⚠️ **Zlaté odtisky se posunuly i v POČTECH**, ne jen v md5 (pozn 96 → 157 a 168 → 198). Je to
záměrná změna výstupu: max dva řádky na stránku pro „A" a „L". Poprvé dnes, kdy re-baseline není
jen podpis přidaného pole - proto je to v testu explicitně rozepsané.

Caché 68/68, .NET 547/547 (+3).

## 2026-08-03 (6) - fix: Newtonsoft.Json 13.0.3 (MLRest ho nenajde a spadne)

Aplikace spuštěná z `bin/Debug` padala při **prvním volání serveru** (nejdřív viditelně na výběru
instance) s `Could not load file or assembly 'Newtonsoft.Json, Version=13.0.0.0'`.

Příčina: `MLRest.dll` v `C:\Edison\` je přebuildovaný proti **Newtonsoft 13** (a 13.0.3 tam vedle
něj leží), kdežto náš csproj měl přišpendlenou **7.0.1** s komentářem „MLRest očekává 7.0.0.0".
Ten komentář byl zastaralý. Z `C:\Edison\` proto aplikace jela (našla 13 vedle sebe), z lokálního
buildu ne (dostala naši 7.0.1).

Bumpnuto na **13.0.3**, tedy přesně na to, co je nasazené. Newtonsoft sami nepoužíváme (naše
serializace jde přes `System.Text.Json`), je tam jen pro MLRest, takže žádná změna kódu.
**Vedlejší efekt: zmizel bezpečnostní warning NU1903** - 7.0.1 má známou zranitelnost a držel se
jen kvůli MLRestu, ten důvod padl.

⚠️ **Pravidlo:** verze musí odpovídat tomu, co leží v `C:\Edison\` vedle `MLRest.dll`. Až MLRest
někdo znovu přebuilduje proti jiné verzi, tohle padne stejně a poznat to bude až za běhu, ne při
buildu.

## 2026-08-03 (5) - tiskneme všechny atributy zastávky pro cestujícího (#13048 bod 1)

Klient psal, že u zastávek nevidí „další atributy jako MHD a další, co umožňuje CIS (paragrafy,
na zavolání atd.)". Kontrola ukázala, že filtr atributů byl **jednoznakový**: propouštěl jen
„na znamení" (`x`), plus přestupy `A`/`L` vlastní cestou. Cokoli jiného se **tiše zahodilo** -
ani značka, ani řádek legendy, bez jakéhokoli signálu.

Filtr je teď rozdělený podle významu:

- **Tiskne se** (informace pro cestujícího): na znamení `x` + varianty `y`/`z`, metro A/B/C `e`/`f`/`g`,
  letadlo `O`, loď `D`, clo `C`, paragrafy `G`/`a`/`b`/`c`.
- **Netiskne se** (legacy tiskové direktivy): tučně `T`, podtržení `p`/`P`/`o`, jen nástup `N`,
  jen výstup `V`, s příjezdem a odjezdem `J`, tisk stanoviště `S`, odjezdová `>` / příjezdová `<` /
  nácestná `E` / cílová pro tablo `H`. **Na AD datech jsou jich tisíce** (`T` 2724×, `E` 2111×,
  `P` 843× jen u KOVED), takže pustit „všechno" by vývěsku zaplevelilo nesmysly.
- Přestupy `A`/`L` zůstávají na svém přepínači.

**Změna je na existujících datech neviditelná úplně** - zlaté odtisky zůstaly **beze změny včetně
md5** (ne jen počtů). Je to čistě pojistka do budoucna.

⚠️ **Korekce mého vlastního tvrzení:** nejdřív jsem napsal, že jedinou viditelnou změnou bude symbol
clo u zastávky **Střelná,,CLO** (jediný výskyt znaku `C` u KOVED, linka 0001101 / skupina 80).
Není to pravda - ten bod trasy **žádný spoj neobsluhuje** (v trasách spojů má značku „projíždí"),
takže se jako řádek neobjeví na vývěsce ani jednoho směru a symbol se nemá kde vytisknout. Vyvodil
jsem to z existence znaku v datech, aniž bych ověřil, že je zastávka obsluhovaná. **Poučení: „znak
je v datech" ≠ „vytiskne se"** - u atributů zastávky to musí projít ještě filtrem obsluhovaných
zastávek (`ObsluhovanePoradi`) a projetých zastávek.

**Vědomě netiskneme „jen nástup" / „jen výstup"**, i když pro cestujícího smysl mají: u KOVED je
`V` na 235 místech a nový symbol by jim viditelně změnil vývěsku. To si má zákazník vyžádat.

„Na zavolání", které klient jmenoval, **není atribut zastávky** - je to bitové nastavení pro
odbavovací zařízení a u spojů typ RadioBus, tedy jiný zdroj. Do tohoto bodu nepatří.

Caché 68/68 (+2 testy vyjmenovávající obě skupiny znaků), .NET beze změny (klient umí nakreslit
libovolný atribut, který dostane - přepínač „Atributy" je zapnutý).

## 2026-08-03 (4) - `B35c` CIS čísla zastávek (#13088)

Poslední bod z „nová pole agendy". Tři nová pole sestavy: **CIS číslo zastávky** (aktuální),
**CIS číslo výchozí zastávky** a **CIS číslo cílové zastávky**, plus totéž jako pole řádku kolekce
zastávek (dostupné v dynamické tabulce).

- Zdroj = registr zastávek `^JRZ(czst)` `$`-pole **9** (legacy `JRapiZst` ř.150 `dta14`,
  „číslo zastávky dle celostátního registru").
- **Pojmenování má příponu `_CIS`** (`CISLO_ZASTAVKA_CIS`, `CISLO_ZASTAVKA_VYCHOZI_CIS`,
  `CISLO_ZASTAVKA_CILOVA_CIS`) - stejně jako `POPIS_ZASTAVKA_CIS` / `POPIS_SMER_CIS`, tedy „totéž,
  ale podle CIS číselníku". Původně jsem to pojmenoval předponou (`CIS_ZASTAVKA`) a vedle
  `CISLO_ZASTAVKA` to vypadalo jako nesouvisející pole, ne jako jeho druhý zdroj (upozornil Jan).
- ⚠️ **Šestimístný údaj v `$`-poli 5** (`419940`), který na první pohled vypadá jako CIS kód, je
  „číslo zastávky **pro strojky**" = CIS × koeficient (`JRapiZst` ř.327). Nepoužívat - test to hlídá.
- **Výchozí a cílová se berou ve směru stránky** (u směru Zpět tedy obráceně proti směru Tam) -
  stránka je vždy o jednom směru, jinak by čísla nesouhlasila s tím, co je na ní vytištěné.
- Na datech KOVED je CIS číslo **shodné s interním číslem zastávky**, které už tiskneme (42033 = 42033)
  - jejich číslování je přebrané z CIS. Skutečně nová hodnota je pro ně tedy hlavně **výchozí a cílová**
  zastávka, u kterých jsme dosud uměli jen název konečné, ne číslo.
- **Nový sloupec seznamu zastávek nepřidán** - tiket žádal pole agendy, ne sloupec (stejná úvaha jako
  u popisu sloupku). Přes dynamickou tabulku to jde, do nabídky sloupců se doplní, až o to někdo řekne.

Ověřeno na ZL_KOVED 0822995 směr Tam, zastávka Zlín,Malenovice,ZPS: aktuální 42033, výchozí 41994
(Zlín,aut.nádr.), cílová 2774 (Brno,ÚAN Zvonařka). Caché 66/66 (+1), .NET beze změny (server-only).
Zlaté odtisky přebaselinovány - shodné počty, jen md5.

## 2026-08-03 (3) - `B35b` dokončeno: logiku stanovišť lze přepnout na sloupek (#13088, #13048 bod 4)

Režimy „více hodnot", řádkové duplikování, dělení stránek na stanoviště i titulek takové stránky
dosud počítaly **jen s označníkem**. U zákazníků, kteří evidují sloupky, to znamenalo, že se ta
logika opírala o prázdnější z obou polí.

- Nový parametr sestavy **Číslo stanoviště podle** (Označník / Sloupek, skupina Seznam zastávek).
  Přepne **všechno naráz**: režimy, řádky, dělení stránek, filtr odjezdů na rozdělené stránce
  i text titulku („…, sloupek 2" místo „…, označník 2").
- **Přepínač je na jednom místě.** Dělení stránek vzniká na serveru, takže volba musí být parametrem
  sestavy; prvek si výsledek přečte z dat a vlastní kopii nemá. Dvě místa pro totéž jsme odklízeli
  v `B41`, tady se to nezopakuje (hlídá to i test - do šablony se ta hodnota neserializuje).
- **Sloupce Označník a Sloupek zůstávají nezávislé** - režimy platí pro ten zvolený, druhý ukazuje
  prostou hodnotu z trasy. Jinak by pod hlavičkou „Sloupek" vyšlo číslo spočítané z označníků.
- **Výchozí hodnota Označník** = dosavadní chování; zlaté odtisky to potvrzují (kromě nového pole
  ve výstupu se nezměnilo nic). Kdo dělení stránek už používá, nic nepozná.
- Popisky, které mluvily o označníku, ale platí pro obojí, se přejmenovaly na **stanoviště**
  („Více stanovišť", „Řádek pro každé stanoviště", „Samostatná stránka pro každé stanoviště").
  „Stanoviště" tím přestalo být zakázaný tvar a je to zastřešující pojem - viz `ux-conventions §8`.

**Past, na kterou jsem najel:** dvě `.cls` jsem upravil skriptem místo editorem. Skript zápisem přidal
**BOM** na začátek souboru, upload na server tím tiše padal (hook chybu jen zapíše na stderr) a testy
pak běžely proti staré kopii třídy. Poznat to šlo jen porovnáním zdrojáku na serveru
(`AdSloupekProbe.ZdrojMetody`). Pravidlo je tedy tvrdší, než co je v `CLAUDE.md`: **`.cls` needitovat
skriptem vůbec**, ne jen „pak to nedeployovat dvakrát".

Caché 65/65, .NET 544/544 (+4). Zlaté odtisky přebaselinovány (jen md5).

## 2026-08-03 (2) - `B35b` první část: sloupek per spoj, výčet a popis (#13088, #13048 bod 4)

Změřeno na 400 linkách KOVED, aby se nestavělo naslepo: **94 linek** má sloupek vyplněný i na úrovni
spoje (označník 102) a **19 zastávek** má napříč spoji různé sloupky (označník 46). Per-spoj hodnota
tedy reálně existuje a režimy „více hodnot" nad sloupkem mají co zobrazit.

- **Pozice obou údajů drží jedna funkce** (`PoziceStanoviste`: označník = W 7 / J-věta 3,
  sloupek = W 13 / J-věta 12). Agregace dle spojů, W-mapa i fallback z trasy si je berou odsud -
  kdyby se rozešly, tiskly by se pod jedním názvem dvě různá čísla.
- Agregace a W-mapa jsou tím **parametrizované zdrojem**, ne zkopírované. Cache má zdroj v klíči,
  aby si označník a sloupek navzájem nepřebíjely spočítané hodnoty.
- Nová pole: **Sloupek (dle spojů)** (`SLOUPEK_SPOJU`, CSV dle četnosti), **sloupek per varianta
  trasy** (`SLOUPEK_VARIANT`) a **Popis sloupku** (`SLOUPEK_POPIS`) z číselníku zastávky
  („směr Brno přes UH") - ten i jako skalár pro hlavičku vývěsky.
- **Popis sloupku se v nabídce sloupců záměrně nenabízí** (rozhodnutí Jan 3. 8.): nikdo si o něj
  neřekl a texty z číselníku jsou zčásti provozní („točna", „výstup"), takže na vývěsce by spíš
  pletly. Data ho nesou dál - jde na něj přes dynamickou tabulku i jako placeholder a do nabídky
  sloupců se dá vrátit jedním řádkem, až o něj někdo řekne. Hlídá to test výchozích sloupců.

Zbývá druhá část `B35b`: parametr sestavy **„Číslo stanoviště podle"**, který na sloupek přepne
režimy více hodnot, řádkové duplikování, dělení stránek (#8828) i titulek stránky.

Caché 65/65 (+2), .NET 540/540 (+1). Zlaté odtisky přebaselinovány - opět shodné počty, jiné md5.

## 2026-08-03 (1) - `B35a` sloupec Sloupek v seznamu zastávek (#13088, #13048 bod 3)

KOVED psal, že „označník je, ale sloupek není, ten je pro nás důležitější". Ukázalo se, že to nejsou
synonyma: **označník** (naše dosavadní pole) je volný text z trasy linky, kdežto **sloupek** je odkaz
do číselníku sloupků zastávky (`^JRZ(czst,"Sloupek",id)` = číslo ~ GPS ~ popis směru, např. „směr Brno
přes UH") a je to údaj, který jde do CIS. Tiskli jsme tedy to prázdnější z obou polí.

- Nové pole agendy **Sloupek** (`SLOUPEK`) - hodnota z trasy linky pro daný směr, tedy J-věta
  `#`-segment **12** (`sloupekTam:sloupekZpet`, legacy `dta14`) proti segmentu 3 u označníku.
- Nový sloupec **Sloupek** v seznamu zastávek, defaultně vypnutý; existující šablony ho dostanou
  nabídnutý v dialogu sloupců (doplňování chybějících známých sloupců), takže bez migrace.
- **Data to potvrzují:** na lince ZL_KOVED 0822995 je sloupek vyplněný u všech zastávek, označník
  jen u části; na CB_CSADAD 0365001 je sloupek vyplněný a označník prázdný úplně.
- Vydává jen AD agenda. V MHD se témuž fyzickému stanovišti doménově říká označník, takže se tam
  pole nezavádí a rozdíl v názvosloví mezi AD a MHD zůstává (rozhodnutí Jan 2026-08-03).

Zlaté odtisky: shodné počty, jiné md5 - podpis přidaného pole (přebaselinováno v tomtéž commitu).
Per-spoj přebití sloupku (W pozice 13), popis sloupku a přepnutí režimů „více hodnot" a dělení
stránek na sloupek zůstávají na `B35b`.

Caché 63/63 (+3), .NET 539/539 (+3).

## 2026-07-31 (8) - `B33` popis dopravce po řádcích, i pro souhrnný JŘ (#13008)

Dosud se dopravci tiskli jako **jeden odstavec** uvozený „Zajišťuje: " a oddělený čárkami. KOVED
potřebuje hlavičku jako na svém vzoru: řádek na dopravce, před ním číslo linky dle licence, za ním
výčet spojů - a bez našeho uvození, protože si úvodní větu píší sami.

Nové pole agendy **Dopravci souhrnného JŘ** (`DOPRAVCI_SOUHRNNY_JR`) skládá:

```
Linka 802170 | ČSAD BUS Uherské Hradiště a.s., …, provoz Uh. Hradiště, tel. …, spoje č. 1-31, 201-204
Linka 802170 | ČSAD BUS Uherské Hradiště a.s., …, provoz Uh. Brod, tel. …, spoje č. 51
Linka 803170 | ARRIVA autobusy a.s., …, provozovna Zlín, tel. …, spoje č. 71-75, 271-273
```

- **Čísla spojů se stlačují do rozsahů** („1-31, 201-204"); dvojice zůstává výčtem („1, 2"), čte se
  lépe. Vypnout je lze parametrem **Výčet spojů u dopravce**.
- **Funguje i pro souhrnný jízdní řád**, kde jednu vývěskovou linku obsluhuje víc dopravců pod
  různými licencemi: řádky se seskupují podle **(licenční linka → dopravce → provoz)** a licenční
  číslo se bere **od spoje** („Zdroj spoje"), ne od linky. Uložený je index linky = číslo doplněné
  nulami na 7 znaků, převod je 1:1 s legacy (`cnvIndLin`).
- **Na obyčejné lince se nic nezměnilo** - zdroj spoje je prázdný, takže vznikne jedna skupina
  a platí číslo té linky. Zlaté odtisky to potvrzují: shodné počty, mění se jen md5 (nové pole).
- `POPIS_DOPRAVCE` zůstává nedotčené, aby existující šablony nezměnily tvar.

⚠️ **Souhrnná větev je ověřená jen syntetickými vstupy** - v žádné testovací firmě souhrnná linka
není (v datech KOVED je u spojů vyplněná *návaznost*, ne zdroj spoje). První reálné potvrzení přijde
od zákazníka.

**Poučení k zlatým odtiskům:** posun digestu jsem nejdřív přečetl jako úbytek poznámek (110 → 96)
a hodinu hledal neexistující chybu. Ta hodnota byla mezitím přebaselinovaná v #13045; skutečná změna
byla jen md5. Do testu proto přidán postup: **nejdřív porovnat složky digestu** - shodné počty +
jiné md5 = přidané/přejmenované pole, ne změna chování.

Caché 60/60 (+9), .NET 536/536.

## 2026-07-31 (7) - `B32` tvar názvu zastávky jako volba sestavy (#13089)

KOVED viděl na vývěsce „Zlín,aut.nádr." a chtěl si vybrat, jak má název vypadat. Nově jsou v
parametrech sestavy dvě volby v **nové skupině „Obecné"** (název zastávky se objevuje i v nadpisu
vývěsky, v cíli a v textech poznámek, takže to není nastavení jednoho prvku):

- **Tvar názvu zastávky:** Zkrácený („Zlín,aut.nádr.", **výchozí = dosavadní stav**) · Typograficky
  upravený („Zlín, aut. nádr.") · Plný CIS („Zlín,,aut.nádr.") · Plný CIS s okresem
  („Zlín,,aut.nádr.,ZL") - poslední dva pro technické exporty a soutěže.
- **Použít název pro MHD:** zastávka může mít v číselníku vedle názvu podle CIS ještě „upravený název
  pro MHD" a ten se tiskl přednostně. Vypnutím se použije CIS název. (Např. v CB je u autobusového
  nádraží MHD název „České Budějovice,AN" místo „České Budějovice,,aut.nádr.".)

**Proč parametr a ne vlastnost prvku:** mění to hodnotu dat, ne vzhled - skládá se na serveru. A je to
volba při tisku: klient chce běžně vylepšený tvar a pro export do soutěže plný CIS, bez druhé šablony.
Oba parametry jsou v allowlistu server-side parametrů, takže jejich změna spustí načtení dat.

- **Typografické pravidlo je vědomě jen pro názvy zastávek**, ne pro libovolný text: mezera za tečkou
  by rozbila webovou adresu („www.idzk.cz" → „www. idzk. cz"), a tu mají v poznámce k tarifu na každé
  lince IDZK. Před číslicí se mezera nedoplňuje, takže „rozc.0.9" a „č.1" zůstávají.
- Ověřeno **end-to-end na datech KOVED** (`ZL_KOVED`, linka 0822995), ne jen jako čistá funkce:
  výchozí tvar je bez dvojčárky, typografický má mezeru za čárkou, plný CIS dvojčárku vrátí.
- Zlaté odtisky se nepohnuly - výchozí hodnota je dosavadní tvar.

Caché 57/57 (+5), .NET 536/536.

## 2026-07-31 (6) - `B41` odklizené mrtvé UI: co řídí parametr sestavy, není i v prvku

Tři přepínače v prvcích **nic nedělaly** - parametr sestavy je přebíjel, a protože ho agendy JŘ
posílají vždy, klik v prvku byl bez efektu. Přesně z téhle dvojkolejnosti vznikla dvojice hlášení
`B22`/`B24` (Jan si toho všiml znovu při klikání 31. 7.).

- Z prvků zmizely: **Šipka směru** a **Projeté zastávky** (seznam zastávek) a **Roztáhnout na výšku**
  (mřížka odjezdů). Zapínají se v parametrech sestavy, což je jediné místo.
- Vzhled zůstává v šabloně, kam patří: barva, tloušťka, hrot a styl čáry šipky · barva a čas
  u projetých zastávek. U nich je nově nápověda, kde se tisk zapíná, aby to uživatel našel.
- Z modelu odešly i tři fallbacky časové osy (`timeAxisMode`, `timeAxisRangeFrom/To`), které z panelu
  vypadly už dřív. Nahradily je konstanty shodné s výchozími hodnotami parametrů agendy.
- **Ověřeno, že to nic nerozbije:** všech pět parametrů posílají obě agendy JŘ (AD i MHD), a vlastnost
  prvku se uplatnila jen bez dat - v tom stavu ale seznam kreslí zástupné řádky (šipka se nekreslí,
  projeté nemají co filtrovat) a mřížka vrací prázdný layout.
- **Uložené šablony bez migrace** - odebrané klíče se při čtení ignorují, hodnota se stejně
  zahazovala. Z distribuční AD šablony jsou vyčištěné.
- **Vedlejší nález:** distribuční šablona měla u mřížky napsanou časovou osu „Vlastní rozsah 4-22",
  ale agenda posílá výchozí „Jen hodiny s odjezdy", takže se ten rozsah nikdy netiskl. Klíče
  odstraněny; kdybychom ten rozsah chtěli mít v distribuci, musí se změnit výchozí hodnota parametru
  v agendě, ne šablona.
- **Import PDF** si přestal do šablony zapisovat detekovanou časovou osu a šipku - stejně se
  při tisku nepoužily. Uživatel si osu nastaví v parametrech.

.NET 536/536, Caché 52/52.

## 2026-07-31 (4) - denní značky se netisknou tam, kde je říká nadpis sloupce (#13045)

Klient hlásil, že u spojů vidí značku „6", i když totéž říká nadpis sloupce, a že v nedělním sloupci
má sobotní odjezdy. **Časy byly správně, lhala značka** - a je to jeden nedoportovaný kus starého
modulu, který způsoboval obojí.

- **Značka dne se nově řídí rozvržením sloupců.** Když má sobota i neděle vlastní sloupec, u platností
  „sobota", „neděle" a „svátek" se značka netiskne vůbec - sloupec ji říká. Když je víkend v jednom
  sloupci, značka zůstává, ale jen když spoj skutečně odlišuje (jede jen v sobotu, jen v neděli,
  jen ve svátek). Konkrétní pracovní dny („jede jen v pátek") se značkují jako dosud.
- **Proč to vypadalo jako sobotní odjezdy v nedělním sloupci:** spoj s platností „sobota a svátek"
  se do nedělního sloupce dostane oprávněně (ve svátek jede), ale nesl u sebe značku „6", takže se
  celý sloupec čte jako sobotní. Teď je bez značky, stejně jako ve starém modulu.
- **Nová značka „svátek"** - dosud jsme ji neumili vůbec, přitom ve sloučeném víkendovém sloupci
  je to ta jediná, která odliší spoj jedoucí jen ve svátek od spoje jedoucího každou neděli.
  Text do legendy „jede ve svátek".

**Ověřeno na datech klienta** (`ZL_KOVED`, linka 0771251): tři sloupce dnů, spoje s platností „6+"
se objevují v sobotním i nedělním sloupci a už u nich není žádná denní značka. Kontrolní vzorek
`CB_CSADAD` 0345067: ze 33 odjezdů zmizelo 15 falešných značek „6" a s nimi řádek legendy.
Zlaté odtisky přebaselinovány - ubyly **jen řádky legendy** (110 → 96 a 183 → 168), počty stránek,
zastávek i odjezdů zůstaly beze změny, což je přesně očekávaný tvar změny.

## 2026-07-31 (4) - `B38` seznam zastávek jde rozdělit na oddíly na pokračování (#13097)

Vysoký seznam zastávek se dá rozřezat na **2 nebo 3 oddíly vedle sebe** (vzor: vývěska PID, kterou
klient přiložil). Každý oddíl nese **celou tabulku znovu** - záhlaví i všechny sloupce, jen s další
porcí zastávek; řádky se dělí rovnoměrně. Prvek je tím o polovinu (třetinu) nižší, což je celý smysl.

- **V roadmapě to bylo přeceněné** jako `L` čekající na `B13` (rozřezání široké tabulky u linkového
  JŘ) s tím, že je to „tentýž mechanismus". Není: `B13` řeže sloupce do stránkových bloků přes
  stránkovací engine, tady se řádky rozlévají do oddílů uvnitř rámce prvku. Reálně **M** a nezávislé.
- **Strop 3 oddíly** (rozhodnutí Jan 31. 7.) - každý oddíl zužuje sloupce, u čtyř a víc už by se
  vývěska s variantními časy nedala přečíst.
- Šipka směru mezi oddíly **pokračuje** (v oddílu s aktuální zastávkou od ní, dál od horní hrany).
- Šířky sloupců i automatické zmenšování textu se počítají ze šířky **oddílu**; kdo má hodně sloupců,
  musí prvek rozšířit nebo část sloupců vypnout.
- Výchozí hodnota 1 oddíl = dosavadní vzhled, **bez migrace**.

.NET 536/536 (+8), Caché 52/52.

## 2026-07-31 (3) - `B34` zóny: doportovaná logika starého modulu (#13091 bod 4)

Klient hlásil dvě věci: zóna se objeví jen tehdy, když je vyplněná u trasy linky, a místo čísla zóny
se tiskne písmeno přepravního systému. **Obojí umí starý modul a my jsme to neměli doportované** -
není to tedy nová featura, ale chybějící kus parity.

- **Záskok na nastavení zastávky:** zóna se bere z trasy linky, a když ji trasa nemá, z registru
  zastávek (u hraniční zastávky včetně druhé zóny). Priorita zůstává jako ve starém modulu -
  trasa vyhrává, zastávka je záskok.
- **Písmeno přepravního systému se odstraní**, pokud je to zapnuté. Písmeno v kódu zóny označuje
  přepravní systém (v datech se předřazuje: ODIS → „O", IDSOK → „I"), ne zónu, takže do tisku patří
  jen číslo z číselníku. Řídí to nastavení JŘ - globálně, nebo u konkrétní linky polem
  **„Zóny - tisk a export do JDF bez písmen"**. Nové UI netřeba, klient si to zapne sám.
- **Hodnota `-` u zóny znamená „netisknout"** - dosud bychom vytiskli pomlčku.
- **Víc zón u zastávky se odděluje čárkou**, ne lomítkem - shodně se starým modulem. Dosud to
  nebylo vidět, protože víc zón přicházelo jako jedna hodnota; se záskokem začnou být samostatné.

**Měřeno, ne odhadnuto:** zlaté odtisky (`CB_CSADAD`) se **neposunuly** - ta firma zóny IDS nemá,
takže záskok tam nic nepřidal. Paritní testy zón vůči starému modulu (`CB_GWBUS`, `KV_DP`) zůstaly
zelené. Nový test dokazuje záskok na živých datech (zastávka s prázdnou zónou u trasy a zónou
`100J` v registru → `100J`, se zapnutým odstraněním písmen → `100`).

Caché 48/48 (+3), .NET 528/528 (+2). **Zbývá dořešit s klientem** (mimo tento blok): jestli má mít
zastávka přednost i tam, kde trasa zónu má (starý modul dává přednost trase), jaké mají nastavení
a na které lince to vidí špatně - to poslední zároveň ověří, že jejich kód zóny je „číslo + písmeno".

## 2026-07-31 (2) - `B30` + `B31`: vzhled seznamu zastávek a barva z dat u sloupců (#13091a, #12997)

První dva bloky zákaznické fronty KOVED z 30. 7. Obojí sahá do téhož místa (seznam zastávek a
nastavení jeho sloupců), proto se dělalo jedním sezením.

**Šipka směru jízdy** je nastavitelná: tloušťka, velikost hrotu, tvar hrotu (plný / otevřený /
bez hrotu) a styl čáry (plná / čárkovaná / tečkovaná).

- **Zároveň opravená vláskovka v tisku:** tloušťka se zadávala v pixelech (0,8), takže na obrazovce
  vypadala rozumně, ale v tisku (jiné rozlišení) z ní byla nitka. Nově je v milimetrech; výchozí
  hodnota 0,2 mm odpovídá tomu, jak šipka vypadala na obrazovce při zobrazení 1:1. Přesně tohle
  klient hlásil jako „šipka je malá".
- **Přepínač „Tučná šipka" zrušen** (rozhodnuto při review 31. 7.) - dělal totéž co tloušťka, jen
  hrubě. Uložené šablony převede migrace na 1.6.0 (tučná → 0,4 mm), takže vypadají stejně jako dřív.

**Svislé čáry mezi sloupci** seznamu zastávek (dosud šly jen vodorovné). Tloušťka, styl i barva jsou
společné vlastnosti mřížky prvku, takže vodorovné a svislé čáry drží stejný vzhled; tloušťku mřížky
šlo dosud řídit jen nepřímo tloušťkou rámečku.

- Uvnitř sloupce zastávek se čára nekreslí, i když je jeho obsah rozdělený na značky a název
  (režim „značky před zastávkou") - jinak by to vypadalo jako sloupec navíc.

**Čas u projetých zastávek** má volbu: nezobrazovat (nově výchozí) · zobrazit (dosavadní chování).

- **Proč byly záporné:** časy jsou počítané od vývěskové zastávky, takže u zastávek, které spoj už
  projel, vycházejí do minulosti. Klient to označil za neplatný údaj a měl pravdu.
- **Třetí volba „skrýt jen záporné" při review vypadla** - u projeté zastávky jiný než záporný čas
  z dat nevznikne (ani v AD, ani v MHD), takže se chovala shodně s „nezobrazovat" a byla to jen
  volba navíc v panelu.
- Značka „projíždí bez stání" zůstává - není to čas.
- Řeší se v prvku, ne na serveru: je to volba zobrazení a data zůstávají beze změny (gold parita).

**Barva z dat u zastávek a odjezdů (#12997).** V editoru sloupců jde barva textu i pozadí zapsat
textem, takže tam lze zadat `{BARVA_LINKY}` - dosud byl jen výběr barvy, který barvu z dat nabídnout
neumí. Sdílené s odjezdy, linkovou i dynamickou tabulkou (jeden editor). Nesmyslný zápis se do
šablony nedostane, stejně jako u formulářových polí.

- U barev záhlaví a buněk se v panelu nově ukazuje nápověda, že jde zadat `{BARVA_LINKY}` - dosud
  ji měl jen rámeček, text a obdélník, takže se o té možnosti u tabulek nedalo dozvědět.
- **Zbývá mimo návrhář:** klient chce v jízdním řádu **dvě** barvy linky (zvlášť text, zvlášť
  pozadí). Dnes je uložená jedna (`GetBarvaLinky` → jedno RGB), takže ji lze použít buď na text,
  nebo na pozadí. Druhá barva je práce na straně JŘ, ne návrháře.

.NET 526/526 (+16), Caché 45/45. Formulář prvku ověřen proti serveru.

**Návrh k rozhodnutí (z review 31. 7.):** čáry a mřížka tabulky by měly být **jedno API napříč všemi
tabulkovými prvky** - dnes jsou dva nesourodé světy (prvky JŘ mají barvu mřížky a přepínače čar,
pevná a dynamická tabulka mají volbu „které čáry", ale barvu i tloušťku zadrátovanou v kódu).
Zaevidováno jako `B40`.

## 2026-07-31 - #10660 DPKV uzavřen (grafické připomínky po importu z legacy)

Poslední otevřený zákaznický tiket zastávkového JŘ. Klient hlásil čtyři oblasti a u tří z nich šlo
o funkce, které existovaly, ale nenašel je - odpověď proto obsahuje i **návod, kde se co nastavuje**.

- Nově volitelné: pozice šipky směru jízdy, pozice značek u zastávek (značka před názvem se
  zarovnanými názvy), vlastní styl 2. řádku nadpisu skupin dnů, rozsah hodin přes půlnoc
  a roztažení řádků na výšku.
- Opraveno: podtržení nadpisu skupin dnů se dřív nekreslilo vůbec, nyní mají oba řádky stejnou
  tloušťku (klient si přesně na nestejnou tloušťku stěžoval z legacy).
- Beze změny kódu, jen návod: podbarvení aktuální zastávky, font jednotlivého sloupce, nadpis sekce
  poznámek, barva znaku poznámky (ta se bere z číselníku poznámek, ne z návrháře).
- **Zjištěný drift v dokumentaci:** interní zápis u 2. řádku nadpisu popisoval čtyři položky
  v properties panelu, ale dnešní UI má na tomtéž místě **tlačítko** do pod-dialogu. Kvůli tomu by
  odpověď zákazníkovi poslala hledat neexistující pole. Opraveno.

## 2026-07-30 (7) - `B22` vyřešena opravou `B24` (dvě hlášení, jeden bug)

„Hodiny od/do" se zobrazovaly, i když časová osa nebyla „Vlastní rozsah". Z kódu to nešlo
reprodukovat (oba povrchy filtr aplikují správně, výchozí osa je „Jen hodiny s odjezdy"), takže se
do toho nesahalo a čekalo se na reprodukci. Po opravě `B24` se problém přestal projevovat - ověřeno
i nejtvrdším scénářem, tedy znovuotevřením šablony bez jediného kliknutí, což bylo jádro `B24`.

Poučení: než se opravuje druhé hlášení, ověřit, jestli nezmizelo s prvním.

## 2026-07-30 (6) - Helpdeskové kolo zastávkového JŘ dojeté

Zastávkový JŘ nemá otevřený zákaznický tiket. Dnes zavřené: **#7947**, **#7955**, **#7959**
a **#13048** - u posledního klient potvrdil, že zmenšování textu v buňce funguje, jak čekal, což byl
jeho poslední otevřený bod. Blok `B2` tím dojel celý → archiv roadmapy.

Zbylé položky helpdeskové fronty čekají na někoho jiného (zpřístupnit build DPKV, SK kódování
na-znamení, dvě potvrzení od zákazníků), nejsou to úkoly.

## 2026-07-30 (5) - #7955 UZAVŘEN - potlačení auto-poznámky vlastním komentářem ověřeno na datech

Tiket byl vedený jako „hotové, ale neověřené" - featura existuje od 20. 6., ale nebylo na čem ji
spustit: `CB_CSADAD` nemá na žádné z 240 linek ani číselník komentářů, ani rozsah „Spoje,
nepoznámkovat". Nastavením testovacích dat v `CB_GWBUS` (skupina 501, linka 0000002, spoj 21) se
podařilo ověřit všechny tři režimy parametru naráz - viz tabulka v `customer-requirements §D2`.

- **D2 splněno:** v režimu „Automatické (rozdíly tras)" u spoje v rozsahu zmizí automatická poznámka
  o rozdílu trasy a zůstane ruční komentář. Přesně to zákazník žádal.
- **D3 vyvráceno jako bug:** negativní značka zůstala ve **všech** režimech, i když je u toho spoje
  označená jako „v rozsahu". Kdyby bug existoval, zmizela by. Zamčeno novým testem.
- **Filtr pracuje per spoj, ne per značku** - kontrolní odjezd mimo rozsah si značku podržel, takže
  nelze omylem odpoznámkovat celou linku.
- **Testovací linka zapsaná do dokumentace a nemazat** - je to jediné místo, kde je featura
  testovatelná. Bez ní by se příště začínalo od nuly.
- .NET 507/507 (+1 test na D3).

## 2026-07-30 (4) - Atributy zastávky se čtou i z registru zastávek (`B23`)

Na vývěsce chybělo, že zastávka je **na znamení** - cestující tak nevěděl, že má dát řidiči znamení.

- **Kde to bylo:** atribut může být na **dvou** místech a my čteli jen jedno. Trasa linky (J-věta) ×
  registr zastávek (`^JRZ`, `$`-pole 8). Změřeno, že **legacy čte obojí**: „x = na znamení" vypíše
  u `CB_CSADAD` 0320680 (kde je jen v trase) i u `CB_GWBUS` 0000001 (kde je jen v registru).
- **Oprava:** `AtributyZastavky` vrací sjednocení obou zdrojů (dedup po znacích, trasa první).
  Registr je per zastávka nezávisle na lince a směru, což je správná sémantika „tato zastávka je
  na znamení". Použito i v souhrnu „všechny zastávky linky jsou na znamení", ať si to neprotiřečí.
- **Dopad měřen předem:** v CB_CSADAD je pole registru prázdné u **všech 5728 zastávek** (240 linek),
  takže tam změna nic nemění a zlaté odtisky jsou v bezpečí. V CB_GWBUS přibyly 2 zastávky se „x"
  (řádků legendy atributů 20 → 32 na 45 stránkách) a stránka 0000001/T má nově **10 poznámek stejně
  jako legacy**. Caché 45/45.
- Znění zůstává naše („na znamení"), legacy má „zastávka pouze na znamení" - vědomě neměněno.
- Vedlejší zjištění: v registru se vyskytují jen znaky `x` a `A`, tedy znaky z číselníku atributů -
  není to kategorie zastávky (což byla obava u „A" na autobusovém nádraží).

## 2026-07-30 (3) - Vývěska vysvětluje jen značky, které na ní jsou (`B25`)

Vývěska je samostatná stránka, kterou cestující čte na jednom sloupku - takže legenda má vysvětlovat
právě to, co na ní je. U rozdělené vývěsky (per označník) to neplatilo: odjezdy se filtrovaly, ale
poznámky rozdílů tras ne, takže tam zůstala vysvětlivka ke značce, jejíž spoj odjíždí z jiného
označníku, tedy je na jiné stránce.

- **Oprava:** `ProrezatLegenduRozdilu` zahodí řádky legendy, ke kterým na dané stránce není žádná
  značka. **Písmena se nepřečíslovávají** - táž značka znamená na všech stránkách téže zastávky totéž.
  Původní obava v kódu (stabilita písmen napříč stránkami) se tím neporušuje; ukázalo se, že si
  s prořezáním legendy neodporují.
- **Platí obecně, ne jen při rozdělení** - stejně se ošetří i spoj, kterému se nerozpozná sloupec dnů.
  Na nerozdělených stránkách je to **no-op** (inventura 9693 stránek vrací identická čísla).
- Ověřeno na `CB_GWBUS` 501/0000001/T: označník „1" → 1 odjezd a legenda jen `C`+bezbariér,
  označník „JET" → 3 odjezdy a legenda `B`,`E`,dny,omezení. Caché 45/45, zlaté digesty beze změny.
- **`B26` uzavřen jako falešný poplach:** pole označníku čteme správně (ověřeno na `CB_CSADAD` 0320680,
  kde nese `11`/`22`); v datech GWBUS je v něm text `JET`/`JETE`, tedy něco jiného. Heuristika se
  nedělá - rozdělení je opt-in a titulek stránky označník ukazuje. Zbývá doménová otázka, co `JET` je.

## 2026-07-30 (2) - Parametr sestavy se v UI tvářil vypnutý, i když zapnutý byl (`B24`)

Uživatel tiskl něco jiného, než co měl v nastavení, a neměl jak na to přijít: uložené
„Rozdělit vývěsku pro každý označník" bylo zapnuté, takže ze stránky zmizely odjezdy jiného
označníku (chyběl konkrétní spoj i s jeho značkami), ale ovladač v UI ukazoval vypnuto.

- **Příčina:** persistované hodnoty se aplikovaly **dvěma cestami**. Otevírací flow dělal celou
  trojici (per-agenda → šablona → přenačtení UI), ale cold-start fallback (když načtení preferencí
  doběhne až po otevření šablony) jen načetl hodnoty do dictu. Ty pak šly do `GetData`, kdežto lišta
  i formulář parametrů zůstaly na defaultech. Cold-start navíc vůbec neaplikoval parametry uložené
  v šabloně.
- **Oprava:** jedna veřejná cesta `ApplyPersistedPrintParamsAsync()` (hodnoty + UI pohromadě), kterou
  volají všechna tři místa. U `OnExternalParamsChanged` je invariant dopsaný do dokumentace, včetně
  důvodu, proč tab neobnovuje sám (jeho `Init()` je synchronní Caché round-trip).
- **Pojistka, aby to bylo vidět příště:** status bar hlásí nastavení, které **tiše mění obsah**
  vývěsky - „poznámky skryté nastavením" · „rozdíly tras nahrazené písmeny variant" · „vývěska
  rozdělená po označnících". Schované poznámky se počítají ze **skutečně** odfiltrovaných položek
  (`NoteSuppressionCounter`), ne ze zapnutého parametru: rozsah „Spoje, nepoznámkovat" nemusí mít
  v datech firmy záznamy a signál z parametru by lhal.
- .NET 487/487 (13 nových: filtr poznámek + skládání hlášky).

Zbývá z téhož nálezu: `B25` (rozdělená vývěska nechává v legendě značky spojů, které na ní nejsou)
a `B26` („označník" může být z dat nesmysl - `JET,1` na CB_GWBUS).

## 2026-07-30 - Audit úplnosti poznámek zastávkového JŘ (AD) + chyba cache W-map

Poznámky jsou na vývěsce klíčové (cestující podle nich pozná, kdy spoj jede a kam), takže proběhl
audit „dorazí každý zdroj poznámek na stránku?". Měřeno na `CB_CSADAD` (240 linek, 9693 stránek)
sondou `Tests/AdProdlouzeniProbe.cls`.

- **Nalezena chyba cache v generátoru** (jádro nálezu): W-mapa trasy se cachovala pod číslem trasy
  **bez linky**, a číslo trasy je lokální v rámci linky → při čtení víc linek v jednom procesu si
  linky mapy navzájem přebíjely a vznikaly **falešné rozdíly tras**. Linka je teď v klíči.
  V produkci se to neprojevovalo (tisk jede jednu linku a cache maže), ale **znehodnotilo to celé
  měření z 29. 7.** - proto se „chyba, že se poznámky nedostanou na stránku" nepotvrdila: dostanou.
- **Inventura zdrojů**: každý zdroj poznámek, který v datech firmy existuje, na stránku dorazí,
  a napříč 9693 stránkami **není ani jeden případ značky u minuty bez řádku legendy**. Ruční
  komentáře spoje a směrové poznámky linky nešly ověřit - firma pro ně nemá žádná data.
  Tabulka s počty je v `customer-requirements-zastavkovy-jr.md §D1`.
- **Nový nález: kolize značky.** Písmena rozdílů tras zahrnují „H" a „F", což jsou zároveň značky
  bezbariérových spojů → na 9 stránkách nese jeden symbol dva významy a legenda k němu má dva různé
  řádky. Čeká na rozhodnutí (oprava změní písmena na gold lince, tedy odchýlí od legacy).
- **Věta o prodloužení spoje zpevněna** (#7947): cíl se hledá přes nejbližší pojmenovanou zastávku
  proti směru jízdy, a bez známého cíle se věta nenapíše (dřív mohla skončit „jede až do zast. ").
- **Reprodukce #7947 nalezena** hledáním přes všechny skupiny a linky: `CB_GWBUS` 501/0000001/T
  (12 výskytů). V `CB_CSADAD` případ nastat nemůže ani na jedné z 31 216 linek - buď chybí vzorová
  trasa (62 422 kombinací), nebo je u zbylých 10 vypnuté auto-poznámkování.
- **Vráceno: vynechání „jede přes zast. …" u prodloužení.** Vypadalo redundantně, ale nese jinou
  informaci než cíl - po vynechání se texty různých skupin spojů staly identickými a dedup je sloučil
  do jedné značky (legacy 3 poznámky → 1). Ponecháno legacy-shodné.
- **#7947 dotaženo - poznámka o prodloužení spoje má cíl na začátku a nese průjezd**
  (`jede až do zast. <konečná> přes <mezizastávky>`, pak „jede přes" / „neobsluhuje").
  Cíl je informace, kvůli které tiket vznikl, a na konci věty ho legenda ořezávala jako první; průjezd
  musí zůstat, jinak splynou skupiny spojů lišící se jen cestou. Informačně tím **dorovnáno legacy**
  (tytéž tři skupiny na ověřovací stránce), odchylka je pouze ve znění. **Bez prodloužení se nemění nic** -
  pořadí i oddělovače zůstávají byte-identické, zlaté digesty beze změny.
- **Oprava: značka „spoj v zastávce nestaví" (`|`) se počítala jako obsluhovaná** (`SpojBitmapa`).
  Jen v rozdílech tras, ale se dvěma viditelnými následky: poznámka tvrdila „jede přes zast. X"
  u zastávky, kde spoj nestaví, a **slévala skupiny spojů**, které se lišily právě jen tímto.
  Změřeno proti starému modulu na `CB_GWBUS` 501/0000001/T: legacy 3 skupiny, my 2, a jiné seznamy
  zastávek. Po opravě **1:1 s legacy** (B/C/E, tytéž seznamy vč. „neobsluhuje zast. Dewetterova ul.").
  Zbytek třídy `|` vylučoval správně (`CasHodnota`, `ObsluhovanePoradi`) - chybělo to na jednom místě.
  Zlaté digesty beze změny (zlaté linky `|` nemají), CB_CSADAD beze změny.
- **Kolize značky „H" je i v legacy** - ověřeno voláním starého modulu (`ALVA.TISKMHD.Data`,
  VolanoZ=JRMAIN) na 0365005: legenda tam nese `3#72#…bezbariérově…` i `1#H#neobsluhuje…`.
  Rozhodnutí: necháváme legacy-shodné (uzavřeno jako „záměrně jinak", detail v archivu roadmapy).
- **Signál o schovaných poznámkách** (status bar): dosud nešlo rozeznat „žádné poznámky nejsou" od
  „jsou, ale nastavení sestavy je schovalo". Počítají se **skutečně** skryté položky, ne zapnutý
  parametr - rozsah „Spoje, nepoznámkovat" v této firmě nemá data, takže signál z parametru by lhal.
  Písmena variant tras jsou výjimka (nahrazují rozdílové poznámky už na serveru) → hlásí se dle parametru.
- Caché 45/45, .NET 446/446 (8 nových testů filtru poznámek - dosud netestovaný). Zlaté digesty
  0365001/0365005 **beze změny** → opravy jsou behavior-preserving, přebaselinování nebylo potřeba.

## 2026-07-30 - Barva linky z databáze jako zdroj barvy v šabloně (ticket 12997, k otestování)

JŘ modul umí lince přiřadit barvu; návrhář ji nově umí použít, takže se dá udělat třeba podbarvení
hlavičky „barvou této linky" místo pevného odstínu.

- **Jak se to zadává:** do barevného pole se napíše `{BARVA_LINKY}` (nápověda je u polí Barva
  rámečku, Barva pozadí u obdélníku a u textu). Mezikrok s výběrem zdroje barvy byl vyzkoušen
  a **zahozen** - přidával klik všem kvůli funkci pro pár prvků; výběr barvy se chová jako dřív.
  Pokrývá to všech 32 barevných polí ve formulářích prvků i panel pro víc prvků naráz.
  **Nepokrývá** dialogy sloupců a pravidla zvýraznění - tam barva textové pole nemá (řešení, až
  bude potřeba: doplnit tam pole na hex, ne vracet mezikrok).
- **V šabloně se ukládá odkaz** `{BARVA_LINKY}`, ne konkrétní odstín, takže se barva vezme aktuální
  při každém tisku. JSON šablony se nemění (barva je pořád string) → žádná migrace.
- **Nenastavená barva = bílá** (i v návrháři, kde data nejsou), aby podbarvení zůstalo čitelné.
  Rozklad odkazu má jedno místo (`ColorHex`) a data se zveřejňují jen na dobu kreslení stránky.
- **Caché:** `BARVA_LINKY` v schématu (nový typ pole `color`, aditivní) i v datech agendy
  `ZastavkoveJrAd`; hodnotu dává JŘ modul (`GetBarvaLinky`, formát „R;G;B" → hex). Volání je
  obalené, takže na serveru bez JŘ modulu se barva nenaplní a tisk jde dál. Ověřeno živě na
  `CB_GWBUS`: schéma i hodnota dorazí (linka 320113 má bílou).
- **Barevné pole se nedává do palety polí** - přetažením by vznikl text s hexem; zadává se do
  barevného pole.
- Barevné pole formuláře odkaz na pole nehlásí jako neplatnou barvu (jinak by se kontrola formátu
  z 13078 bila s touto funkcí).
- Testy: TD 506/506, EDControls 462/462, EDISONMAIN2 425/425 (30 nových případů). **Ostatní agendy
  (linkový, knižní JŘ) záměrně zatím ne** - doplní se přidáním pole do jejich schématu, zadávání
  je společné.

## 2026-07-29 - Vymazaný HEX kód barvy shodil návrhář a „rozbil" šablonu (ticket 13078)

Zákazník vymazal HEX kód barvy, potvrdil enterem a dostal chybovou hlášku; po uložení už šablonu
**nešlo otevřít**.

- **Příčina:** barva se čte přes `SKColor.Parse`, která na neplatné hodnotě **vyhodí výjimku**.
  Do modelu se přitom prázdná hodnota dostala bez jediné kontroly (barevné pole formuláře typovou
  kontrolu nemělo, `ElementPropertyAccessor` bere string „as-is"). Padalo to teprve při kreslení,
  a protože se nesmysl uložil do šablony, padalo to i při každém dalším otevření - odtud „šablona
  zhavarovala".
- **Vrstva 1 - kreslení nikdy nespadne:** nový `Models/ColorHex.cs` je jediné místo, kde se
  uživatelská barva čte; neplatná hodnota sáhne po záložní barvě (černá u textu/čar, průhledná
  u pozadí, „jako nenastaveno" u nepovinných barev). Nahrazeno ve **všech** ~30 místech kreslení.
  Tím se **samo opraví i už uložené šablony** (zákazníkova „IDZK poulička" se otevře).
- **Vrstva 2 - neplatná barva se neuloží:** barevné pole formuláře nově hlásí neplatný tvar samo
  (`Field.GetTypeError` v EDControls → červený rámeček, tooltip, uložení se zastaví - stejně jako
  u čísla nebo datumu) a `ElementFormBinder` neplatnou hodnotu do modelu nezapíše ani při živé
  editaci v panelu. Doplněna i kontrola v ručním HEX poli properties panelu (dřív pustilo `#ZZZZZZ`).
- **Vymazání barvy má konečně srozumitelný význam:** u zděditelné barvy (styly buněk, styl textu)
  znamená „dědit" → null, u ostatních „nic nekreslit" → průhledná. Dřív se uložil prázdný string,
  který kaskádu rozbil.
- Testy: TD 482/482 (29 nových případů), EDControls 458/458, EDISONMAIN2 420/420. Mezi nimi
  **regrese na zákaznický případ**: šablona s rozbitými barvami projde uložením, otevřením,
  stránkováním i vykreslením (dřív tady letěla výjimka) - takže „šablona nejde otevřít" je ověřeně
  pryč, ne jen odvozeně.

## 2026-07-29 - Úzké varianty písma ve výběru fontu (Arial Narrow, ticket 13006)

Zákazník po dodání řezů písma nahlásil, že **Arial Narrow ve výběru nevidí** - ani jako samostatné
písmo, ani jako řez Arialu, přesto že ho ve Windows nainstalovaný má.

- **Příčina:** Windows (DirectWrite) sdružuje rodiny po „šířka-váha-sklon", takže Arial Narrow
  **není samostatná rodina** - je to řez rodiny Arial se šířkou Condensed a v seznamu rodin z OS
  vůbec není. Nabídka řezů ho neukázala taky, protože vyjmenovávala jen **váhy** a úzké řezy mají
  tutéž váhu 400/700 jako obyčejný Arial; liší se šířkou, kterou model neumí (`ToSKFontStyle`
  posílal natvrdo normální šířku s domněnkou „Condensed řešíme rodinou").
- **Řešení:** úzké a rozšířené varianty se do nabídky přidávají jako samostatné položky pod jménem
  odvozeným z reálného názvu řezu v OS (`Arial Narrow`, `Tw Cen MT Condensed`, `Bahnschrift
  SemiCondensed`, …) a `FontResolver` je při kreslení přeloží zpět na rodinu + šířku. Uživatel je
  tak vybírá jako font, stejně jako je zvyklý z Wordu. Na běžném stroji přibylo 11 položek.
- **Bez migrace šablon** - varianta se veze v už existujícím poli rodiny písma, JSON se nemění.
- **Opraven i tisk starých šablon**, které Arial Narrow předepisují: jméno dosud nikdo nerozpoznal,
  takže se tiskl obyčejný Arial (a v samotné Skii by dokonce vyšlo Segoe UI).
- **Nabídka řezů je nově filtrovaná na šířku varianty** - u Arial Narrow tedy jen Normal a Bold,
  ne Black, který existuje jen v normální šířce.
- Jednošířkové rodiny (Impact, Agency FB) odvozené jméno **nedostávají** - vybere je samo jméno
  rodiny a „Impact Condensed" by byl jen duplikát téhož fontu.
- .NET 453/453 (7 nových testů; ověřují i zákazníkův případ vč. toho, že text je opravdu užší).
  Testy se neopírají o konkrétní fonty stroje - varianty si najdou v tom, co OS nabízí.

## 2026-07-29 - Plán řádků: sdílená kreslicí smyčka (B11 / R1, část 1 - linková tabulka)

Nízkoúrovňové tabulkové jádro (buňka, sloupec, kaskáda, šířky) bylo sdílené už dřív; chyběla
poslední vrstva - **druh řádku**. Každý tabulkový element měl vlastní svislou smyčku, takže každá
řádková featura (seskupování, mezisoučty, sloučené buňky, rozdělení přes stránky) se musela napsat
N×. Nově element **popíše tabulku jako plán řádků** (`RowSpec`) a kreslí ji jedna smyčka
(`RowPlanRenderer.DrawRows`).

`RowSpec` nese: **druh** (`RowKind`: Header / GroupHeader / HeaderExtra / Data / SubRow / Summary) ·
výšku · buňky · pozadí celé šířky · pera vodorovných čar nahoře a dole · pero svislých čar.

- **Druh řádku kreslení neovlivňuje** - je to nosič významu, na který se navěsí řádkové featury
  (`B13` rozdělení přes stránky, opakování mezisoučtu). Bez něj je smyčka „nakresli N stejných
  řádků" a featury nemají za co chytit.
- **Dvě věci musely být v kontraktu explicitně**, jinak se rozejdou pixely: (1) **pořadí kreslení**
  pozadí → buňky → čáry (kdyby čáry šly první, pozadí buňky je překryje); (2) **pera jsou
  per-řádek**, protože záhlaví a datové řádky reálně používají různá (rámečkové × oddělovací).
- **Linková tabulka převedena** (prototyp - má nejvíc druhů řádků: pás skupin dnů se sloučenými
  buňkami → čísla spojů → platnost → negativní značky → zastávky s podřádky přípojů). Sloučený pás
  nese příznak „bez svislých čar", aby ho nešlo omylem ztratit.
- **Důkaz:** odtisk linkové tabulky **bit za bitem identický** proti baseline sejmuté před zásahem
  (viz záchytná síť níže). .NET 438/438.

Ve třetím kroku převeden **zastávkový seznam**. Ten si vyžádal doplnit do kontraktu **dokreslení
vlastního obsahu do řádku** (`RowSpec.Decorate`, volá se po buňkách a před čarami): značky zastávky
se kreslí po úsecích symbolovým a normálním fontem, protože v jedné buňce nejde míchat font. Je to
úzká výjimka pro to, co buňka principiálně neumí - ne obecná zadní vrátka.

Ve druhém kroku převedena i **dynamická tabulka** (záhlaví, datové řádky se zvýrazněním i střídáním, souhrnné řádky) - odtisk taktéž identický. Výjimka, která zůstala u elementu: **oddělovač bloku souhrnů** se kreslí pod pozadím prvního souhrnného řádku, tedy v opačném pořadí než řádkové čáry; do plánu proto nejde a je to u něj zdůvodněné.

**Převedeny tři živé tabulky** (linková, dynamická, zastávkový seznam) - u všech odtisk bit za bitem
identický. **Mřížka odjezdů zůstává mimo** (pivot: sloupce = skupiny dnů, řádky = hodiny).

**Pevná tabulka vědomě nepřevedena** - dvě odchylky by změnily její výstup a nemá jak se ověřit:
kreslí svislou čáru **za každým** sloupcem včetně vnějšího (sdílená smyčka kreslí jen mezi sloupci)
a u pevných řádků vrství **dvě pozadí** (střídání + override řádku), což je totožné jen u plně
neprůhledných barev. Navíc je to **uspaný element** (paleta ho nenabízí), takže ani není v odtiscích.
Jeho oživení (Fáze B krok 1) už dorovnání kreslení na sdílenou cestu obsahuje - patří to tam, kde se
to dá proklikat.

## 2026-07-29 - Záchytná síť na kreslení (příprava B11)

Před refaktorem kreslicí smyčky (`B11` / R1 extrakce řádkového jádra) vyšlo najevo, že **ani jeden
z 433 testů nic nevykresluje** - všechno je logika, modely, kaskády. „Nic se nerozbilo" se u pěti
elementů se zákaznickou paritou nedalo tvrdit jinak než z proklikání, které neuchytí posun
o desetiny milimetru (a ten se pozná až u zákazníka na vývěsce).

- **`RenderSnapshot`** vykreslí element mimo WPF do bitmapy (pozadí → obsah → rámeček, stejné pořadí
  jako skutečná pipeline) a udělá z pixelů odtisk. **`ElementRenderFixtures`** = pevná data pro pět
  elementů, schválně **heterogenní** (u linkové tabulky pás skupin dnů, čísla spojů, řádek platnosti,
  negativní značky, příjezdový řádek), aby refaktor neprošel jen proto, že jsou všechny řádky stejné.
- **Baseline není v gitu** - projekt nemá zabalené textové fonty (jen symbolové `TimeTable2`), takže
  text rasterizuje Arial z OS a odtisky jsou vázané na stroj; kolegovi by commitnutá baseline padala
  v pre-push. Ukládá se mimo repozitář, přesnímání `TD_RENDER_REBASELINE=1`. Trvalá pixelová regresní
  sada by šla až se zabalenými textovými fonty.
- **V gitu jsou 4 testy nezávislé na stroji** (porovnávají odtisky z téhož běhu): determinismus
  harnessu · každý element něco nakreslí a všech pět se liší · **posun o 0,1 mm musí odtisk změnit**
  (potřebná citlivost pro B11) · změna velikosti písma o 0,5 pt se musí projevit.
- **Síť rovnou dvakrát zabrala:** (1) baseline se dá omylem sejmout **proti zastaralé binárce** -
  když běží designer, build testů selže na uzamčeném `.exe` a testy jedou proti staré DLL (zapsáno
  jako poučení v harnessu); (2) při tom se ukázalo, že dnešní `B5` změnil kreslení **právě a jen
  u linkové tabulky** a ostatní čtyři elementy nechal bit za bitem stejné - přesně jak měl.
- Testy: .NET 438/438 (5 nových).

## 2026-07-29 - Tichý filtr hodin v mřížce odjezdů (nález Jan)

Meze **„Hodiny od/do"** patří režimu časové osy „Vlastní rozsah", ale kreslení je aplikovalo
**i ve všech ostatních režimech** - a protože jsou v nich schované (podmínka viditelnosti
`CASOVA_OSA=Vlastní rozsah`), vznikal tichý stav: uživatel zadal rozsah, přepnul osu jinam, pole
zmizela z formuláře, ale jejich hodnoty dál řezaly mřížku a **nebylo je jak vidět ani zrušit**.
Spravilo se to až obnovením dat, které parametry přeneslo znovu.

Druhý, horší důsledek téhož: výchozí „od" = **4**, takže podmínka byla splněná **bez jakéhokoli
nastavení uživatele** - režim „Jen hodiny s odjezdy" tiše zahazoval hodiny 0-3 (noční odjezd
ve 3:15 v mřížce prostě nebyl).

- **Oprava:** meze platí jen ve Vlastním rozsahu, kde to jsou jeho hranice, ne globální filtr.
  Pravidlo zapsané v kódu, ať se to nevrátí: **schovaný ovladač nesmí ovlivňovat výstup.**
- **Viditelný důsledek** (rozhodnutí Jan): v režimu „Jen hodiny s odjezdy" se nově zobrazí i hodiny
  pod 4, pokud v nich odjezd je - což ten režim má znamenat.
- **Jak to vzniklo:** cross-mode filtr přišel dřív (`b956ab7`), podmíněná viditelnost až v časové
  ose B3 (`a0c17c7`) - tam se komentář jen přeformuloval a nikdo si nevšiml, že se významy dostaly
  do rozporu.
- Nesouvisí se seznamem `ServerSideParams` - časovou osu řeší výhradně kreslení na klientovi,
  takže re-fetch je správně nepotřebný; přepnutí režimu se má srovnat bez F5.
- Testy: `DepartureTimeAxisTests` (6 - tři režimy se nezužují vč. výchozího 4-23, Vlastní rozsah
  beze změny vč. přechodu přes půlnoc). .NET 433/433.

## 2026-07-29 - Tabulka se přizpůsobí i na šířku (blok B5 / L‑1)

Zmenšování při přetečení znalo jen **svislý** směr: obsah vyšší než pevná výška → jedna škála na
fonty i výšky řádků pro celý element. Vodorovně nic takového neexistovalo, což mělo dva projevy:

- **Rozházené písmo.** Na šířku reagovalo jen zmenšování textu v jednotlivé buňce (à la Excel,
  z tiketu #13048 bodu 2). To si ale **každá buňka počítá sama**, takže u linkové tabulky, kde
  sloupce = spoje, vycházel čas „14:35" menší než „8:05" - vedle sebe různé velikosti.
- **Tiše zmizelé spoje.** Šířka sloupce spoje má spodní mez (pod ní je čas nečitelný); když ani při
  ní součet nevyšel, přebytek se **oříznul** - z pravé strany chyběly odpolední spoje a **nic to
  neřeklo**, protože varování o přetečení hlídá jen svislý směr.

Nově `TableLayout.ComputeWidthScale` = vodorovný protějšek výškové škály. Element ji zkombinuje
s výškovou (`MathF.Min` - zmenšuje ta horší strana) a použije na šířky sloupců, výšky řádků i fonty,
takže tabulka **dosedne přesně na svou šířku** místo oříznutí. Zmenšování v buňce se tím vrací ke své
roli **výjimky pro odchylku** (jeden nezvykle dlouhý název zastávky), ne pravidla pro všechny časy -
skládání už bylo správné: buněčné zmenšování startuje od globální škály a zmenší jen to, co při ní
ještě přetéká, takže nehrozí dvojí zmenšení.

- **Kde:** linková tabulka (vlastní výpočet šířek → skalární varianta), zastávkový seznam a dynamická
  tabulka (přes sdílené `ResolveWidths`). **Mřížka odjezdů záměrně mimo** - je to pivot s málo sloupci
  a vlastní logikou roztažení. **Uspaná pevná tabulka** dostala jen zmenšení šířek (font-škálu vůbec
  nezná, dorovná se při jejím oživení) - i tak je to proti dosavadnímu oříznutí zlepšení.
- **Behavior-preserving:** dokud se tabulka vejde, škála je 1 a šířky jsou spočítané úplně stejně jako
  dřív (pokryto testy, ať se nezmění uložené šablony).
- Testy: `TableWidthFitTests` (9 - vejde se / nevejde, pevné i procentní šířky, mnoho auto sloupců na
  minimu, minimum se neuplatní když je místo, degenerované vstupy). .NET 427/427.

**Pozor na hranice:** u opravdu velké linky (100+ spojů) je zmenšování slepá ulička - písmo by bylo
nečitelné. Správné řešení je rozřezat tabulku na stránkové bloky (`B13`); tohle je poctivý mezikrok,
který zbaví běžné linky rozházenosti a tichého mizení a dá vzniknout signálu „tady už zmenšování
nestačí".

## 2026-07-29 - Base pole formulářů elementů z jednoho místa (blok B4)

Sada polí, kterou má **každý** element (rámeček + rohy · pozice a velikost · vrstva · uzamčení ·
podmínka viditelnosti), byla ručně opsaná v každém z **deseti** formulářů - kolem 200 řádků, které
říkají totéž. A jak se čekalo u kopií, rozešly se:

- **tloušťka rámečku** 0-10 mm u textu × 0-5 mm u zbytku (kanon = 5, souhlasí s C#),
- **dynamická tabulka neměla pole rámečku vůbec** - v panelu se rámeček nastavit dal, ve formuláři ne,
- **rámeček byl u každého jinde**: vlastní skupina (5×), „Barvy a rámeček" (text), namixovaný
  v „Obecné" (zastávky, odjezdy),
- **výška** měla rozdílný popisek, kvůli kterému byl klíč vyjmutý z hlídacího testu,
- nápovědy u vrstvy a uzamčení někde byly, někde ne.

Nově sdílený `BaseElementFields.cls` (dvojče `StyleFields`, který totéž už dělá pro pole stylu textu)
se třemi metodami; formulář si sadu vyžádá třemi řádky a legitimní odchylky jdou parametrem, ne kopií
(výchozí šířka/výška, dynamická výška, elementy bez rohů jako čára, nulová šířka u svislé čáry).
**Sjednoceno i seskupení** - rámeček má u všech elementů vlastní skupinu „Rámeček elementu"
(rozhodnutí Jan: jednorázové přestěhování je menší zlo než trvalá nekonzistence).

- **Druhá strana byla už v pořádku** - oproti původnímu zápisu bloku B4 nejsou base pole v C#
  opsaná desetkrát, ale definovaná jednou v základní třídě elementu. Reálný stav byl tedy
  10 × Caché + 1 × C#, ne 10 × 2 → práce vyšla podstatně menší. C# jen dostal shodné názvy skupin
  (konstanty `PositionGroup`/`BorderGroup`/`ConditionsGroup`), ať obě cesty řadí tytéž vlastnosti
  do stejně pojmenovaných skupin.
- **Pojistka, aby duplikace nemohla vzniknout znovu** (podstatnější než samotný úklid - nulová
  duplikace nejde, jsou to dva běhové světy a dva jazyky, ale jedna definice na stranu + test ano):
  - `ZadnyElementovyFormular_NeopisujeBasePoleRucne` - base klíč napsaný přímo ve formuláři = spadne,
  - `BasePole_MajiVCsStejnyPopisekISkupinu` - rozejití popisku nebo skupiny mezi Caché a C# = spadne,
  - stávající test „každý klíč existuje jako C# property" rozbaluje i volání sdíleného balíčku,
    takže base pole zůstala pokrytá (jinak by kontrola po přesunu tiše přestala platit),
  - výjimka pro klíč výšky padla.
- U dynamické tabulky přejmenováno „Rámeček" (= mřížka mezi buňkami) na **„Čáry tabulky"**, aby se
  to nepralo s novou skupinou rámečku elementu.
- Testy: .NET 418/418 (2 nové), Caché 84/84. Všech pět strukturálně odlišných formulářů ověřeno
  živě proti serveru (text, čára, zastávky, linková tabulka, dynamická tabulka).

## 2026-07-29 - Nabídka řezů písma reaguje na změnu rodiny

Ve formulářích elementů nabízelo pole **„Řez"** řezy té rodiny písma, která tam byla **při otevření
dialogu**. Po přepnutí rodiny se nabídka nepřepočítala - nabízela řezy, které nová rodina nemá, a
naopak chyběly ty, které má; srovnalo se to až zavřením a znovuotevřením. Horší polovina problému:
když se řez vybral **před** změnou rodiny, zůstal v poli viset i po ní, i když ho nová rodina nemá -
formulář vypadal platně, ale renderer při kreslení sáhl po nejbližším dostupném, takže **tisk
nesouhlasil s tím, co bylo v dialogu napsané**, a nikde to nezakřičelo.

Byly to dvě vrstvy: nabídku (a) nikdo nepřepočítával, (b) přepočítat ani nešlo - volby pole byly
ve sdílené komponentě obyčejná property **bez ohlášení změny**, takže dosavadní kód se spoléhal na
naplnění ještě předtím, než WPF dropdown vůbec postaví. Po vykreslení už se obsah vyměnit nedal.

- **EDControls (aditivně):** `Field.ListItems` hlásí změnu (+ `ListItemsForDisplay`) → nabídku lze
  vyměnit za běhu. Formuláře, které volby plní před vykreslením jako dosud, se chovají identicky.
  Dorovnává to konzistenci komponenty - **viditelnost** pole svou změnu hlásila už dřív, volby ne.
- **Návrhář:** změna rodiny přepočítá nabídku řezů sourozeneckého pole a **zahodí řez, který nová
  rodina reálně nemá** (→ prázdno = auto dle Tučné, zapíše se jako krok zpět). Napojeno u všech
  6 hostů formuláře (inline panel + 5 dialogů).
- **Dvě pojistky proti tichému zahození uživatelovy hodnoty:** (1) zápis hodnoty pole hlásí změnu
  i při **programovém plnění** formuláře - bez rozlišení by pouhé otevření dialogu řez zahodilo;
  (2) zahazuje se jen podle **skutečně známých** řezů (nový `FontWeightCatalog.InstalledWeights`
  bez fallbacku) - u neznámé rodiny (překlep) nebo prázdné (dědí se z šablony) je nabídka jen odhad,
  takže hodnota zůstává.
- Testy: `FontWeightSyncTests` (7 - přepočet, zahození neplatného, ponechání platného, obě pojistky,
  ignorace jiných polí, ohlášení změny voleb). .NET 416/416, EDControls 448/448, EDISONMAIN2 420/420.

Vzniklo z revize architektury: původní blok B4 mísil kapabilitu sdílené komponenty s úklidem
v návrháři → rozdělen na **B4** (sdílená base pole), **B18** (deklarativní pravidla formuláře) a
tento fix. Deklarativní cesta (`visibleWhen`) by tenhle přepočet uměla obecně, ale je to jiný
mechanismus (skrývání × obměna obsahu) a čeká na B18.

## 2026-07-28 - Rozvržení editoru stylu buňky (blok B6)

Sdílený `CellStyleEditor` (tab Sloupce/Styl ve všech 6 tabulkových dialozích) měl řádek Font/Řez/
Velikost/styly ve `WrapPanel`, který se v úzkém dialogu (680 px) zalomil doprostřed skupiny.
Přestavěno na **dvousloupcovou mřížku ve stylu EDInputFormu**: popisky ve sdíleném pravém sloupci
(`SharedSizeGroup`), prvky pod sebou; každý řádek samostatný `Grid` s vlastní viditelností (skrytá
volba nezabere výšku, nic se vodorovně nezalomí). Tri-state logika, placeholdery zděděných hodnot
i reset „×" beze změny. Jedno místo → spraveno všech 6 dialogů. Vizuálně ověřeno Janem.
**Budoucí krok** (obecná unifikace dialogů): eventuální migrace samotného editoru na Caché-driven
EDInputForm - háček je bohatá tri-state / reaktivní logika, kterou by EDInputForm musel podpořit.

## 2026-07-28 - Interní nálezy A1: mock data náhledu bez freeze + úplné škálování šablony

Úklidová dávka po vydání verze (Fáze A, blok B16 + B17).

- **B16 - mock data náhledu nezamrzají UI.** Kreslicí cesta náhledu volala `GetMockData`
  **synchronně** (cold-start Caché 2-3 s = freeze při prvním vykreslení) a neúspěch se necachoval
  (→ nový HTTP pokus při každém překreslení). Nově: render čte **jen z cache**
  (`TryGetCachedMockData`), data se **prefetchují na pozadí** při změně agendy
  (`RefreshFieldsListCore` → `PrefetchMockDataAsync`) a po dosednutí překreslí náhled; do doby
  načtení se ukáží syntetická data ze schématu. Selhání/prázdno se **cachuje** (fail-soft),
  čistí ho „Obnovit data" (F5).
- **B17 - úplné škálování šablony (`ApplyScale`).** Nový `TextStyle.ApplyScale` (font v pt +
  paddingy v mm; `LineSpacing` bezrozměrný → NE) použit všude. Opraveno: schedule elementy
  škálovaly jen `FontSize`, ne paddingy; `DataRowHeight` se u LineTable/StopList/DepartureGrid
  neškáloval (nebo dvakrát - přesunuto do `ScheduleElementBase`, jednou); **per-sloupcové**
  (`ColumnVisualStyle`/`TableColumn`) a **per-skupinové** styly + `WidthOverride` skupin dnů +
  Fixed buňky `TableElement` + element-level styly Table/DynamicTable se dřív neškálovaly vůbec.
  Procentní šířky sloupců se (správně) neškálují.
- **Kodifikace UI/UX výjimek** do `architecture.md` (OK přes `Click=` v 6 EDInputForm dialozích
  kvůli `GeneralForm.Save()`, 5 tabů TableEditoru = uspaný element, ~59 `Click=` v MainWindow
  = postupná migrace při dotyku).
- Testy: `ApplyScaleTests` (4 - font+padding, per-sloupec/skupina, DataRowHeight jednou, % beze
  změny, LineSpacing beze změny). .NET 409/409.

## 2026-07-28 - Parita voleb legendy + nápovědy parametrů variant (do verze)

Dvě malé věci na přání Jana před publishem:

- **NotesLegend „Dle typu" doplněno do dialogu** - výchozí režim řazení poznámek v EDInputForm
  dialogu úplně chyběl (šel nastavit jen v panelu); volby sjednoceny 1:1 s panelem. Druhá půlka
  nálezu (TextElement „Do bloku" jen v dialogu) byla zastaralá - v panelu už je.
- **Nápovědy parametrů sestavy** - aditivní klíč `description` v GetParameters → helpText bublina
  ve formuláři (mapuje `AgendaParams.AddParamField`, `AddBoolParam` má nový volitelný parametr).
  Vyplněno u čtveřice variant/označník parametrů - hlavně vysvětlení, že písmena variant nahrazují
  automatické poznámky rozdílů tras. Starší agendy bez description beze změny.
- Testy .NET 405/405, Caché 45/45.

## 2026-07-28 - Konec dlouhých pomlček (em dash) v celém projektu

Rozhodnutí Jan: žádné em dashe - působí jako generovaný text. Hromadná náhrada za běžnou
pomlčku „-" napříč TemplateDesigner + testy (270 souborů, 5354 výskytů: kód, komentáře, UI texty,
dokumentace; rámečkové čáry ─ v komentářích nedotčené). Pravidlo zapsáno do TD i root CLAUDE.md,
UI labely nově hlídá ban v `EDInputFormSchemaDriftTests`. Ostatní projekty (EDControls,
EDISONMAIN2) záměrně nedotčené - mimo TD scope. Testy .NET 405/405, Caché 45/45 (gold beze
změny - em dash nebyl v žádném datovém výstupu).

## 2026-07-28 - Drobné nálezy revize: tichá selhání, guardy, kosmetika + 8 testů

Druhá dávka z nezávislé revize (po názvoslovném kanonu).

- **NotesLegend tučný symbol** - bezpodmínečný override `Bold = true` přebíjel uživatelovu volbu
  (komentář sliboval opak); teď default tučně JEN když `DataStyle.Bold` nenastaveno (anti-pattern
  scalar override z `mvvm-patterns.md`).
- **Tichý catch při načtení obrázku** (EDInputForm image field, App.xaml.cs) - selhání čtení/enkódování
  souboru nově hlásí přes `IPopupDisplayer.Error` (R2), dřív jen tiše nic.
- **Sjednocení skupiny „Podmínky"** - 7 formulářů mělo „Pokročilé" (s Uzamčeno uvnitř); nyní všude
  „Podmínky" a **Uzamčeno přesunuto do „Pozice a velikost"** (vzor novějších dialogů). Ban sken drift
  testu rozšířen i o labely skupin (`AddGroup`).
- **Kosmetika:** volby „Pozice šipky" v dialogu = plné znění jako v panelu („Vpravo (u pravého okraje
  elementu)"…), tooltip přetečení bez kódového „PageSize", „v návrháři" místo „v designeru", elipsy
  „…" v ButtonLabelech, komentáře vědomých výjimek (2× White v MainWindow, IsDefault+IsCancel na
  Zavřít v PrintDialogu).
- **Testy (+8):** `GroupColumnIndexResolverTests` (4 - poziční `__INDEX_` klíče, priorita reálného ID),
  `StopListCellTextTests` (4 - režimy sloupce Označník vč. fallbacku). `InternalsVisibleTo` pro testy
  (konec zveřejňování helperů kvůli testům). .NET 405/405.
- **Pozn.:** nález „GetMockData bez cache" z revize je zastaralý - cache existuje; skutečný zbytek R1
  (synchronní HTTP z kreslicí cesty při prvním načtení/selhání) řešen samostatně.

## 2026-07-28 - Názvoslovný kanon zamčený drift testy + hromadné sjednocení labelů

Z nezávislé revize (druhá session). Nejdřív pojistka, pak hromadné přejmenování - v tomto pořadí.

- **Drift testy rozšířeny** (`EDInputFormSchemaDriftTests`): (a) „stejný field key = stejný label
  napříč všemi EDInputForm .cls" (výjimky `Height`/`StrokeWidth` s komentářem), (b) ban list
  zakázaných tvarů v .cls labelech i volbách, (c) tentýž ban list nad panelovými PropertyDescriptors
  všech elementů. + oprava hledání kořene repa (fallback z cesty zdrojáku) - drift testy už neběží
  falešně červené z izolovaného výstupního adresáře.
- **Rozhodnutí Jan (kanon):** „Uzamčeno" (ne Zamčeno) · „Podmínka viditelnosti" (ne Viditelnost) ·
  „Tloušťka rámečku/okraje" (ne Šířka rámečku, Tl. okraje). Zapsáno do `ux-conventions §8`.
- **Hromadné sjednocení** (panel + dialogy + .cls): Tučné/Podtržení/Přeškrtnutí (ne Tučně/Podtržené/
  Přeškrtnuté - i CellStyleEditor a FormatToolbar), Vodorovně/Svisle + Nahoře/Dole, „Střídání řádků"
  (ne Zebra), „Velikost aktuální" (ne Font aktuální), „Řádek negativních značek" (ne Řádek NZ),
  „Přestupy (vlak/autobus)", „Barva pozadí" jednotně (ne Pozadí/Výplň), kontextové menu „Vrstva"
  (ne Z-pořadí), LineTable „Výška řádku (prázdné = auto)", + stale komentáře. Wire klíče beze změny.
- Testy: .NET 397/397 (drift 17/17), Caché 45/45.

## 2026-07-28 - Mechanické opravy z celkové revize (UI/UX, názvosloví, docs)

Quick wins z celkového auditu stavu (4 paralelní revize: UI/UX, názvosloví, testy, architektura).

- **Dark mode:** hlavička „Strom šablony" měla natvrdo `#555555` (nečitelné na tmavém panelu) →
  `EDTheme.Text.Secondary`. Jediný hardcoded hex brush v celém XAML.
- **Názvosloví:** „Povolit split" → **„Povolit rozdělení"** (přetečení sekce, panel i EDInputForm);
  „Velikost písma pozn." → **„Velikost poznámek"** (NotesLegend, panel i dialog - kanon §8);
  helpText „Legacy JŘ" → „Původní JŘ" (přestupy StopListu).
- **F1 nápověda:** smazán stale řádek „Sdílená hranice - tah za hranu mění i souseda" (funkce
  odstraněna už dřív, nápověda ji dál slibovala).
- **CLAUDE.md:** smazáno stale tvrzení o přepínači `InvertovatNegativniZnacky` (v kódu neexistuje,
  inverze je vždy zap); aktuální focus doplněn o stanovišťový balík #13048/#8828; datum aktualizace.

## 2026-07-28 - Doladění stanovišťového balíku: slovník „označník", titulek stránky, držení stránky, nálezy revize

Kolo zpětné vazby Jana nad 320680/330510 + nezávislá revize. Vše doladění #13048/#8828, gold beze změny.

- **Jednotný slovník „označník"** (rozhodnutí Jan): všechny UI texty (parametr, combobox elementu,
  sloupec, pole agend AD/MHD/linková, F1 nápověda) sjednoceny na „označník" - konec míchání
  nástupiště/stanoviště/sloupek; UI texty bez cizích výrazů („per"). Kanon v `ux-conventions §8`
  + `CLAUDE.md`; wire názvy (`NASTUPISTE`…) beze změny. Výjimka: legacy legenda „tisk stanoviště".
- **Titulek rozdělené stránky (#8828):** `POPIS_ZASTAVKA` += „, označník X" (jen při rozdělení) -
  propíše se do titulku i názvů exportních souborů; parametr přejmenován na **„Samostatná stránka
  pro každý označník"** (zrcadlí volbu elementu „Řádek pro každý označník"); **řádek vývěskové
  zastávky** na rozdělené stránce nese označník stránky (jinak stránky 11/22 vizuálně identické).
- **Stránkování náhledu:** re-fetch dat (změna server parametru, F5) **drží logickou stránku**
  (zastávka×směr, přednost shodě označníku - `FindMatchingPageIndex` + testy), ne pořadové číslo;
  deferovaný resync comboboxu stránek (WPF po výměně ItemsSource nechává prázdný zobrazovací box).
  Seznam server-side parametrů zobecněn (`ServerSideParams`).
- **Oprava bool parametrů na wire:** klient posílal `ToString()` „True", server srovnával „true" →
  bool parametry GetData nikdy nezabraly (i TR2). Fix obou stran (normalizace + tolerantní čtení).
- **Nálezy nezávislé revize (28. 7.):** (1) spoj úplně bez označníku při rozdělení spadne na stránku
  nejčetnějšího označníku - dřív zmizel ze všech (ztráta odjezdu); (2) „Řádek pro každý označník"
  se neaplikuje bez zapnutých variantních sloupců (duplikáty by nesly identický čas) + helpText
  navádí na sloupec Označník; (3) záměr „varianty se na rozdělené stránce nefiltrují" zdokumentován
  (stejná písmena napříč stránkami téže zastávky); (4) labely enum parametrů = zamčené (persistuje
  se text - `ux-conventions §8`); spodní mez zmenšení textu evidována k B2.
- **Testové pokrytí doplněno:** Caché `TestPismenaVariant` (end-to-end písmena variant, 0330510)
  + `TestSplitTitulekARadek` (titulek + vývěskový řádek, 0320680); xUnit `CellShrinkTests` (4),
  `PreviewPageMatchTests` (5), gate testu duplikace. Caché 45/45, dotčené .NET sady zelené.

## 2026-07-27 - Vývěska per označník (#8828 TRANSDEV) - stanovišťový balík dokončen

Zastávka obsluhovaná z více označníků (stanovišť), kde různé spoje jedou z různých sloupků, se dá
vytisknout jako **samostatná vývěska per označník** - každá jen s odjezdy z toho označníku (TRANSDEV
linky 876420/863383; referenční podoba = KOVED XLSX 802366).

- **Tiskový parametr** „Rozdělit vývěsku per označník" (skupina „Seznam zastávek", default vyp = dnešní
  jedna vývěska s odjezdy smíchanými). Server-side (věc generování výstupu, ne element).
- **Server (`ZastavkoveJrAd`)** - při zapnutí vývěska obsluhovaná z N označníků (dle `NastupisteSpoju`)
  → **N stránek**; `NacistOdjezdy` filtruje spoje na daný označník (per-spoj sloupek = W pozice 7,
  fallback J-věta); `NASTUPISTE` stránky = její označník. Jen označníky, ze kterých ≥1 spoj. Nezávislé
  na duplikaci řádku (bod 4).
- **Gold beze změny** (parametr default vyp → byte-identický výstup, žádný re-baseline). Nový
  `TestSplitPerOznacnik` (0320680: ČB aut.nádr. → označníky 11/22, odjezdy 5+1). 43/43 Caché.
- **Ověřeno:** 0320680 OFF=53 stránek, ON=55; ČB aut.nádr. (T) → 2 stránky (označník 11 = 5 odjezdů,
  22 = 1 odjezd).
- **Follow-up (odloženo):** členění exportu na samostatné soubory per označník (dnes lze rozlišit
  tokenem `{NASTUPISTE}` v názvu). Titulek/label výše než sloupec Nástupiště = levné doplnění.

## 2026-07-27 - Šev mezi plátnem a náhledem (blok B6, část a)

Na hranici návrháře a náhledu byl vidět „zdvojený pruh". Příčina nebyla jedna: **čtyři místa, která
mají mít shodnou barvu plochy, ji měla různou** - plocha kolem plátna brala špatný token
(`Border.Light`, skoro bílá), plocha kolem náhledu a pruh se stránkováním měly `#E4E4E4` natvrdo
v XAML, plátno stejnou barvu natvrdo v kódu. Správný token (`EDTheme.Bg.Canvas`, vč. tmavé varianty)
přitom existoval a nikdo ho nepoužíval. Druhá složka švu = tečkovaný zaostřovací rámeček děličů,
který zůstával po tažení.

- **Jeden token pro celou plochu** - oba `ScrollViewer`y, pruh stránkování i oba `GridSplitter`y na
  `EDTheme.Bg.Canvas`; děliče `FocusVisualStyle = null`, takže mezi plátnem a náhledem není žádná
  linka (oddělení dělá rámeček panelu náhledu).
- **Skia strana má jediný zdroj** - `Designer/CanvasTheme.Background` nahradilo dvě nezávislé
  konstanty v `DesignerCanvas`/`PreviewCanvas`; plní ho `MainWindow` z téhož tokenu, takže se WPF
  a Skia barva nemůže rozejít. Napojeno na `ThemeService.Applied` → plocha reaguje na přepnutí tématu
  (odregistrace v `OnWindowClosing` - statická událost by zavřené okno držela naživu).
- **Vědomě mimo:** ostatní chrome návrháře (papír, pravítka, záhlaví sekcí, výběr) zůstává pevný -
  patří do samostatné položky „dark mode", která potřebuje rozhodnutí, jak má v tmavém režimu vypadat
  *papír* (tiskne se na bílý). Export se nezměnil - PDF/PNG si plochu čistí bílou zvlášť.
- Vizuálně ověřeno Janem (šev i tečkovaný rámeček po tažení děliče zmizely). Zbývá část (b) -
  rozvržení editoru stylu buněk.

## 2026-07-27 - Drobné korektnostní nálezy (blok B3)

Dva nezávislé tiché nálezy z předchozí revize kódu, oba „nic nespadne, jen se ztratí nastavení".

- **Dialog pevné tabulky mazal per-buňkový formát a styl.** Řádkový mezi-model dialogu
  (`RowVM`/`CellVM`) nesl jen text buňky → otevřít + OK zahodilo `Format`, per-buňkový `Style`
  a barvu pozadí řádku. Nejvíc to bolelo u tabulek načtených ze šablony agendy - Caché posílá
  `format` (např. `dd.MM.yyyy`) a barvy per buňku, dialog je nikdy needitoval, ale smazal.
  Nově se přenášejí (styl se klonuje, ne sdílí). `TableElement.Clone()` byl v pořádku už dřív
  (drží ho reflexní `CloneCompletenessTests`) - problém byl jen v cestě přes dialog.
- **Ruční kreslicí / měřicí cesty JŘ elementů ignorovaly řez písma.** Legenda poznámek a měření
  záhlaví odjezdů si stavěly typeface natvrdo jako Normal/Bold, takže uživatelova váha písma
  (a kurzíva) se do nich nepropsala - a u legendy se navíc rozešlo měření s renderem (počet
  zalomených řádků → špatná výška boxu). Sjednoceno na kaskádu: nový helper
  `ScheduleElementBase.GetTypeface(ResolvedTextStyle)` (rodina + řez z `ToSKFontStyle`), použitý
  v legendě (text, záhlaví, zvýrazněný symbol, výpočet výšky), v měření a wrapu záhlaví skupin dnů
  u odjezdů a v designerových placeholderech odjezdů / linkové tabulky / seznamu zastávek + v měření
  šířky značek zastávky. Legenda tím zároveň začala respektovat **výchozí font dokumentu** (dřív
  padala na Arial).
- **Testy:** 8 nových xUnit (`TableEditorRowRoundTripTests` 4× round-trip řádku, `ScheduleFontCutTests`
  3× řez z kaskády + výška legendy reagující na tučný řez - ověřeno, že bez fixu padá). 384/384 xUnit.

## 2026-07-27 - Duplikace řádku per stanoviště (#13048 KOVED, bod 4)

Zastávka obsluhovaná z více stanovišť (sloupků) se v seznamu vypíše jako **N řádků, jeden per
stanoviště** (KOVED „duplikace řádku ... příklad Medlovice"). 4. volba comboboxu „Více nástupišť"
vedle „nejčetnější" / „výčet".

- **Server (aditivně, `ZastavkoveJrAd`)** - nové wire-pole `NASTUPISTE_VARIANT` per zastávka
  (nástupiště per varianta, zarovnané s `TRASA_MIN`; z reprezentativní trasy varianty, W pole 7,
  fallback J-věta). Identita variant se **nemění** → gold jen re-baseline (počty stejné, přibylo pole).
- **Klient (`StopListElement.ExpandPlatformRows`)** - režim `RowPerPlatform`: zastávku se ≥2 stanovišti
  rozvine na N řádků **na jejím místě v trase** (pořadí = první výskyt stanoviště napříč variantami,
  netřídí se). Každý řádek nese své stanoviště + **časy jen variant, které z něj jedou** (ostatní
  prázdné - řeší bod 5 „jiné stanoviště = jiná varianta" na úrovni zobrazení). Vývěska a jednostanovištní
  zastávka = 1 řádek; značky (atributy / na znamení / poznámky) jen na prvním řádku.
- **Testy:** 6 xUnit (`StopListPlatformDuplicationTests`, vč. zachování značky „<" projíždějící varianty) +
  Caché `TestNastupisteVariantEmit` (data lock na 0330510). Gold 0365001/0365005 re-baseline. 42/42 Caché + 6/6 xUnit.
- **Známé omezení:** nástupiště per varianta = z **reprezentativního spoje** varianty (jako TRASA_MIN).
  Dva spoje se **shodným sledem zastávek**, ale různým stanovištěm, se slijí do jedné varianty → zobrazí se
  jen stanoviště reprezentanta. Rozlišení i tohoto případu = stanoviště do **identity varianty** (vědomě NE -
  dávalo vizuálně identické sloupce, viz bod 5 / `c6d661d`); reálná data (0320680) mají různá stanoviště
  napříč různými variantami, kde to funguje. Plné rozlišení až na reálnou potřebu KOVED.

## 2026-07-27 - Varianty tras dle trasy + písmena u odjezdů (#13048 KOVED, bod 5)

Návrat k legacy sémantice variantních časových sloupců + KOVED rozšíření. Jen server
(`ZastavkoveJrAd`), .NET beze změny (anotace/hlavičky/pořadí jedou z dat).

- **Seskupování variant PŘEDĚLÁNO (bezpodmínečně):** varianta = **sled obsluhovaných zastávek**
  (1:1 legacy dedup dle `prutmbmp`, `JRapiLinPrtMhd1` ř.266-300 - „ponechá se delší čas"),
  NE časový profil jako dřív. Spoje stejné trasy s jinými časy se slévají; čas sloupce =
  **maximum přes spoje** („nejpozději za X min"). Efekt 0330510/T: 37 spojů, 33 sloupců →
  **11 variant tras** (hlavní 21 spojů; sonda `AdVariantyProbe.Scan`). **Nástupiště do identity
  NEJDE** (první verze ho měla - dalo vizuálně identické duplicitní časové sloupce, vidět na
  smoke testu; KOVED „jiné stanoviště = jiná varianta" pokryje vývěska per sloupek #8828,
  helper `WNastupisteMapa` připraven).
- **Oprava `|` ve W-trase = spoj nestaví** (nález ze smoke testu - „časy 0"): `|` znamená
  „zastávka na trase, ale spoj v ní nestaví" (legacy `JRapiExp10` ř.272-275 přeskakuje
  `<`/`|`/prázdné shodně); `CasHodnota` ho dřív četl přes `+"|"` jako čas 0 → falešné nuly
  v časových sloupcích, falešné „obsloužené" zastávky a slité varianty. Nyní: `CasHodnota("|")=""`
  (spoj s `|` na vývěskové zastávce nedostane odjezd), `ObsluhovanePoradi` `|`-only zastávky
  nezařadí, ve variantních sloupcích se `|` ukáže jako `<`. Gold beze změny (0365001/0365005
  `|` neobsahují), unit test rozšířen.
- **Řazení sloupců** - parametr „Řazení variant tras": *Dle četnosti ze zastávky* (default, KOVED -
  nejčastější první) / *Stabilní (pořadí spojů)* (TR7/TRANSDEV kontinuita). Parametry ve skupině
  „Seznam zastávek" (týkají se variantních sloupců StopListu). **.NET:** re-fetch trigger
  zobecněn z hardcoded `ROZDELENI_SLOUPCU` na seznam `ServerSideParams` (snapshot porovnání) -
  změna kteréhokoli variantního parametru přenačte data.
- **Oprava bool parametrů na wire (latentní, nález ze smoke testu):** klient posílal C# bool
  přes `ToString()` = **„True"**, server porovnává `= "true"` → bool parametry GetData nikdy
  nezabraly (týkalo se i TR2 `SOUHRN_NA_ZNAMENI` - proto „čekal ověření"). Fix obou stran:
  `CacheAgendaProvider.GetData` normalizuje bool → `"true"/"false"` (wire kontrakt),
  server čte tolerantně přes `$zcvt(...,"L")` (funguje i se starším klientem).
- **Písmena variant u odjezdů** - parametr „Písmena variant u odjezdů" (default **vyp** = beze změny
  pro stávající zákazníky): hlavičky sloupců = písmena A–Z (`PismenoVarianty` + unit test), u minuty
  odjezdu anotace s písmenem varianty (kategorie `varianta-trasy` - mimo filtry #7955), volba
  „První varianta bez písmene" (default zap - hlavní trasa čistá). Písmena **nahrazují automatické
  rozdílové poznámky** (`RozdilyTras` se nepočítá - jinak dvě abecedy na vývěsce); ruční poznámky
  zákazníka nedotčeny. Vyp = původní hlavičky (písmena rozdílů / „T1"…).
- **Ověřeno:** sonda `AdVariantyProbe.Stranka` (ostrá `BuildStrankaZastavky`) na 0330510: hlavičky
  `["", "A" … "J"]`, 16 anotací písmen = přesně počet spojů mimo hlavní variantu (37−21),
  `rozdil-trasy` v anotacích nahrazeno; default režim = kategorie beze změny. Gold: 0365001 **beze
  změny**, 0365005 re-baseline (jen pořadí 6 sloupců dle četnosti; počty 1:1). Caché 41/41,
  .NET 370/370.

Zákazník: spoje jedou z různých sloupků jedné zastávky → volba chování sloupce „Nástupiště".

- **Server (`ZastavkoveJrAd`):** nové pole řádku zastávky **`NASTUPISTE_SPOJU`** - nástupiště
  posbíraná ze spojů směru dle legacy pravidla „**nástupiště ze spoje, není-li, tak z linky**"
  (`JRapiExp10:1222-1226`): per-spoj hodnota z W-trasy, fallback J-věta `#3`. CSV seřazené dle
  četnosti (první = nejčetnější; shoda → menší hodnota, čistý helper `NastupisteCsv` + unit test).
  Agregace cachovaná per linka×směr (`^||TiskAd("nastCsv")`). ⚠️ **Past ověřená v legacy zdrojích:
  pole `trs(8)` = W-segment pozice 7** - legacy čas/čekání rozpadá W pozici 2 na dvě pole
  (`JRapiSp:234-241`), indexy jsou posunuté (první pokus s pozicí 8 četl zóny).
- **Element (StopList):** přepínač **„Více nástupišť"** (`MultiPlatformMode`): *Z trasy linky*
  (default = dnešní chování) / *Nejčetnější dle spojů* / *Výčet všech („2, 3")*. Panel + dialog
  (EDInputForm). Sloupec „Nástupiště" zůstává opt-in.
- **Data ověřena:** sonda `AdWTrasaProbe.ScanNastupiste` - per-spoj sloupky plní stovky linek
  (CB_GWBUS 541, CB_CSADAD 339) a multi-sloupkových zastávek jsou desítky (197/153; vč. gold
  0320993, 0320680, 0330510). End-to-end na 0330510: ČB aut.nádr. → „1,2,28,3" (fallback z trasy
  správně mezi četnostmi). Uzavírá i datový průzkum pro #8828 (vývěska per sloupek - zbývá stránkování).
- Gold: re-baseline 0365001/0365005 (jen md5 - nové pole v JSON; počty beze změny). Caché 40/40,
  .NET 370/370.

## 2026-07-27 - Přetékající text buněk se zmenšuje na šířku - vestavěné chování (#13048 KOVED, bod 2)

Zákazník chtěl u přetékajících názvů zastávek / seznamu zón zmenšení fontu jako v Excelu
(„přizpůsobit buňce"). Po UX diskusi **bez konfigurace - vestavěné chování všech buněk tabulek**:
dosavadní „chování" při přetečení bylo, že text podlezl pozadí vedlejší buňky (useknutá půlka
slova) - to není nikdy záměr. Žádný checkbox; opt-out doplníme až na reálnou poptávku.

- **`CellRenderer`**: text širší než buňka → proporcionální zmenšení fontu; **trigger až skutečné
  přetečení** (sloupce s auto šířkou na míru textu se nemění), cíl = šířka minus rezerva 0,6 mm
  (nekončit na hraně). Víceřádkový text dle nejširšího řádku (jednotná velikost). Hot path bez
  alokace navíc (`ComputeShrinkScale` overload s existujícím fontem).
- **StopList + značky za názvem**: zmenšení názvu počítá i s šířkou značek (`TrailingWidthMm`
  v `CellSpec` - obsah škálující se s textem); značky se posouvají i zmenšují se jménem (sdílený
  výpočet škály = pozice se nerozjedou). **Rezerva dle šipky směru**: u sloupce názvů se šipkou
  vpravo = odsazení svislice (0,6) + vodorovná část šipky (1,5) + buffer (0,5) = 2,6 mm, aby
  zmenšený text nepodlézal vodorovnou část šipky; ostatní buňky jen obecných 0,6 mm.
- **Rozsah:** buňky kreslené přes `CellRenderer` (StopList, DynamicTable/TableElement, LineTable,
  záhlaví DepartureGrid). Legenda poznámek (zalamuje) a minuty odjezdů (vlastní layout) beze změny.
- Bod 3 (#13048) ověřen: „čísla sloupků z trasy" = hotový sloupec „Nástupiště" (TR3); „z detailu
  spoje" (per-spoj W-trasa) splývá s bodem 4.

## 2026-07-27 - Šablony v DB: kompatibilita se staršími klienty (#13013 - „Šablona nebyla nalezena nebo je porušená")

Kolega s produkční verzí klienta (release 2026-07-14, bez podpory blobů po kusech) nemohl otevřít
systémovou šablonu z DB - nový build ji při seedu uložil po kusech a starý klient čte jen jednoklíčové
`data` → prázdno → chyba. Diagnóza vyloučila poškozená data (`SablonyDiag.IntegrityProbe`: žádná
utržená šablona, blob existoval - jen ve formátu, který starý klient neumí).

- **Fix (`Sablony.SaveTemplate`):** malý blob (vejde se do 1 kusu, tj. do ~20 000 znaků - drtivá
  většina šablon) se ukládá **jednoklíčově** = formát čitelný i staršími klienty. Po kusech jen
  vícekusé bloby (velké obrázkové šablony) - ty starší klient stejně nikdy neuložil (padaly ještě
  před chunking fixem). Kanonizace legacy→chunked z 2026-07-23 tím zrušena (bránila mixu verzí).
- **Testy:** `TestMalyBlobJednoklicovyProStarehoKlienta` + `TestVelkyBlobPoKusech` zamykají oba
  formáty; 39/39 zeleně.
- **Nový diag nástroj `Tests/SablonyDiag.IntegrityProbe`** (read-only): projde celé úložiště šablon
  a per šablona hlásí stav blobu (jednoklíčový / po kusech / CHYBÍ = utržená). Pro budoucí „nejde
  otevřít šablonu" případy.
- Systémová šablona v MLINE už je po re-seedu v kompatibilním formátu (ověřeno probe).
- **Pozn. k druhé výtce z tiketu** („okno chybového hlášení vypadá jinak"): jde o standardní
  EDControls `EDErrorPopup` (scrollovatelný dialog s plnou výjimkou, R2) - liší se od starého
  EDISON okna záměrně (nový stack). Případné sjednocení vzhledu / čistá hláška bez stacku pro
  doménové chyby = týmové rozhodnutí, ne bug.

## 2026-07-24 - Přestupy u zastávek (vlak/autobus) - nad rámec legacy (#13048 KOVED, bod 1)

Zákazník chtěl u zastávek zobrazit přestupy na vlak/autobus. Data je mají (znak „L"=vlak, „A"=autobus
v atributech zastávky, číselník 16), ale legacy StopList je záměrně netiskl (jen „x"=na znamení).

- **Server (`ZastavkoveJrAd`)** - `JePrestupovyAtribut` (V1: „A"/„L"); přestupy se **vždy posílají jako
  data zastávky** (ATRIBUTY), ale **do legendy nejdou** (jen symbol u zastávky). Ostatní znaky číselníku
  (formátovací P/T/N/V…) se dál netisknou.
- **Klient (`StopListElement`)** - nový přepínač **„Přestupy (vlak/bus)"** (`ShowTransferAttributes`,
  **default vyp = jako legacy**); render filtr rozlišuje na-znamení / přestup / ostatní atribut. Přepínač
  v panelu i EDInputform (`StopListElementEdit.cls`). Symbol se kreslí u názvu zastávky (piktogram).
- **Gold re-baseline** (0365001/0365005) - počty beze změny (`zast/odj/pozn` stejné, pozn potvrzuje že
  přestupy nejdou do legendy), změnil se jen md5 (přestupy přibyly do ATRIBUTY). Vědomé, ověřené.
- **Nový test `TestPrestupyEmit`** (0330510 ČB AN - autobus na terminálech) zamyká emisi. 37/37 zeleně.
- Přestupy sedí hlavně na **terminálech** (nádraží na koncích trasy) - tam je přestup logický.

## 2026-07-24 - Gold pojistka zón vůči legacy (AD zastávkový JŘ, #13048 KOVED)

Zákazník hlásil prázdný sloupec zón. Ověřeno, že náš `ZastavkoveJrAd.GetData` vrací zóny **1:1 shodně
se starým modulem** - a shoda zamčena testem, aby ji budoucí změny nerozbily.

- **Nová test třída `ALVA.TemplateDesigner.Tests.ZastavkoveJrAdLegacyTest`** - porovná řetězec zón per
  zastávka z našeho `GetData` proti **legacy producentovi** `ALVA.TISKMHD.Data.LoadData` (VolanoZ=JRMAIN,
  tentýž generátor, který krmil starý modul). 1:1 assert na CB_GWBUS 320113 (`101/104/109`) a KV_DP
  410101 (vč. vícezónové `11,12`). Namespace / legacy třída / prázdné zóny → skip (jiný server).
- Diagnostika mimo runner: `ZonyProbe` (IN DB/SKU/LIN/PAGE → OUT LEG/OUR/MATCH).
- **Pasti legacy volání zdokumentovány** (`ad-data-mapa.md §9`): `LoadData` čte IN jako řetězce
  „KLÍČ~HODNOTA" (ne pojmenované subscripty), OUT bez count node (iterovat `$order`), `$$$MakroGlobal`
  přepíná IN/OUT globál dle `sys("C","App")` (nutno `ALWA.SYS.MLRest`, jinak čte `^CacheTempMLSrv`).
- **Výsledek: zóny nejsou rozbité** - prázdný sloupec u KOVED = jejich data/šablona, ne regrese.
  Řezy 36/36 Caché testů zeleně.

## 2026-07-24 - Řezy písma (váhy) - výběr reálných řezů fontu (#13006 KOVED)

Zákazník chtěl používat víc řezů písma (TT Hoves Light/Medium/SemiBold/Heavy…), ne jen Tučně/Kurzíva.

- **Model:** `TextStyle.FontWeight` (100–900, nullable; null = auto dle Tučně, aditivní bez migrace);
  `ResolvedTextStyle.ToSKFontStyle` staví váhu (explicitní přebíjí Bold) → teče do renderu.
- **Sdílený `FontWeightCatalog`** - jediné místo „jaké řezy rodina reálně má" (z `SKFontManager.GetFontStyles`,
  jen vzpřímené) + názvosloví. **Nabízí jen skutečně nainstalované řezy** (ne fixní seznam) - chybějící font
  nemá, tak se nenabídne (dřívější fixní seznam mátl: nabízel váhy, co font nemá).
- **UI „Řez" dropdown** všude, kde se nastavuje font:
  - **EDInputForm** (Text, StyleFields zóny DepartureGrid/StopList/DynamicTable, NotesLegend) -
    klient plní z katalogu (`ElementFormBinder.PopulateFontWeightFields`) **při otevření**.
  - **Per-sloupec `CellStyleEditor`** (WPF) - tri-state `CellStyleVM.FontWeight` + **živě reaktivní**
    nabídka (změna rodiny → přepočet řezů). Layout řádku fontu změněn na WrapPanel (zalamování → nepřetéká).
- **Render:** řez respektují buňky tabulek (`CellRenderer`→`ToSKFontStyle`) + TextElement.
- **Odloženo do architektonického tracku** (viz roadmap): reaktivita EDInputForm dialogů (živý přepočet
  bez znovuotevření) + ruční render cesty schedule elementů (minuty DepartureGridu, text legendy) + čistý
  layout editoru sloupců.

## 2026-07-24 - Rohy elementu (zaoblení / zkosení) obecně na base (#12998 KOVED)

Zákazník chtěl seříznuté/zaoblené rohy barevných tvarů (odznaky linky). Uděláno **obecně na úrovni
každého elementu, jako rámeček** (architektura i UX).

- **Base `TemplateElement`:** `CornerStyle` {Ostré/Zaoblené/Zkosené} + `CornerSize` (mm). Serializace,
  `CopyBaseTo`, `ApplyScale`, descriptory („Rohy" + „Velikost rohu") jednotně.
- **Render centralizovaný:** rámeček (`RenderBorder`) i nově **pozadí** (`RenderBackgroundPass`,
  symetrické k border pass, volané canvasem před obsahem) kreslí tvar přes jediný helper
  `DrawElementShape` (Ostré=DrawRect, Zaoblené=DrawRoundRect, Zkosené=SKPath se seříznutými rohy).
  Z Barcode/Image/Rectangle odstraněna vlastní výplň pozadí → jedno místo.
- **UI:** „Rohy" + „Velikost rohu" ve skupině „Rámeček elementu" u všech boxových elementů
  (Rectangle, Text, Image, Barcode, StopList, DepartureGrid, NotesLegend, LineTable). Čára vynechána.
- **`RectangleElement`:** vlastní `CornerRadius` sjednoceno do base + **migrace 1.4.0 → 1.5.0**
  (`cornerRadius` → `cornerStyle=Rounded`/`cornerSize`); staré šablony 1:1. Rectangle si drží vlastní
  výplň/okraj (fill přes bg pass, okraj přes `DrawElementShape`).
- Testy: 370/370 (vč. 3× migrace). **Známý drobný nedostatek:** „Velikost rohu" je vidět i při Ostré
  (bez efektu) - EDInputForm nemá deklarativní `visibleWhen`; čisté skrytí je součástí tracku níže.
- **Fix (2026-07-24):** obsah elementu (výplně buněk DepartureGridu, pozadí Textu, obrázky…) přetékal
  přes zaoblený/zkosený roh. Řešeno **obecně na jednom místě**: render loop (náhled i plátno) volá
  `ApplyCornerClip` kolem obsahu každého elementu (no-op bez rohů) → ořez na tvar rohů plošně, obdoba
  rámečku. Base `GetContentClipRect` (element dodá skutečnou výšku; TextElement přepisuje).

## 2026-07-23 - Časová osa odjezdů jen v parametrech sestavy (#13009 KOVED)

Zákazník hlásil, že časová osa jde nastavit vždy jen na hodiny s odjezdy - změna nastavení nedělala nic.

- **Root cause:** časová osa šla nastavit na **dvou místech** - property elementu (dialog „Odjezdy")
  *i* parametr sestavy „Časová osa odjezdů" (`CASOVA_OSA`/`HODINY_OD`/`DO`). Preview ale do dat vždy
  vlévá **defaulty všech parametrů** (`LoadAgendaParameters`), takže `CASOVA_OSA="Jen hodiny s odjezdy"`
  je přítomen vždy a v renderu **přebíjí** property elementu → nastavení na elementu bylo mrtvé. Stejné
  v AD i MHD (ne AD-specific).
- **Fix:** časová osa je per-šablona parametr (sdílený, autoritativní) - odebrána z element dialogu
  (`DepartureGridElementEdit.cls`) i z descriptorů (Časová osa / Hodiny od / Hodiny do). Nastavuje se
  jen v parametrech sestavy (jako v MHD). Property `TimeAxisMode/RangeFrom/RangeTo` zůstávají jako render
  fallback (když param chybí), ale needitovatelné. Bez regrese - parametr element vždy přebíjel.

## 2026-07-23 - Legenda poznámek: nastavení se propisují (#13011 KOVED) + sjednocení termínu „Font"

Zákazník hlásil, že v legendě poznámek nejde nastavit barva textu, oddělovač ani font.

- **Barva textu buněk** - legenda kreslí text přímo přes `SchedulePaints.Text` (ne přes `CellRenderer`),
  který má barvu zapečenou natvrdo → `DataStyle.TextColor` se ignorovala. Nově se barva textu poznámek
  bere z „Barva textu buněk" (přebití paintu po `CreateStandardPaints`). Symboly bez vlastní barvy z dat
  ji přebírají taky; NZ boxy a barevné symboly z dat mají svoje barvy (nedotčené).
- **Font (nadpis i poznámky)** - přidán **editovatelný ComboBox** plněný z Caché standardní cestou
  (`$$$FieldTypeComboBox`, klíč `…FontFamily` → `ElementFormBinder.PopulateFontFields` = nainstalované
  fonty z `FontResolver`). Samostatně pro záhlaví (`HeaderStyle.FontFamily`) i data (`DataStyle.FontFamily`);
  render záhlaví nově bere font ze záhlaví (fallback na font poznámek → Arial). Doteď font pole úplně chybělo.
- **Oddělovač** - funguje dle návrhu jen pro **vlastní poznámky linky** (`Text` typ se značkou → „značka
  oddělovač popis"); u ikonových atributů (na znamení, bezbariér) se záměrně nepoužívá. Doplněn `helpText`
  a výchozí oddělovač změněn z em dash „-" na normální pomlčku „-" (default pro nové elementy).
- **Terminologie sjednocena na „Font"** napříč kódem (dřív „Rodina písma"/„Písmo") - TextElement,
  NotesLegend, jejich EDInputForm `.cls`. Konvence zapsána do `ux-conventions.md` §8 (glosář UI popisků +
  pravidlo „label je zdvojený .cs↔.cls, měň obě místa").

## 2026-07-23 - Šedé úsečky v PDF (#13004 KOVED): oddělovač sekcí jen v editačním plátně

Zákazník hlásil tenké šedé úsečky „v místě konce sekce" v návrhu, které se přenášely i do PDF/tisku,
i když neměl zapnutý žádný rámeček.

- **Příčina:** `PreviewCanvas.RenderPage` kreslila jemnou čáru (`#DDDDDD`) mezi sekcemi jako orientační
  pomůcku - jenže tuto metodu sdílí živý náhled **i** exporty (PDF, imposiční PDF, PNG). Pomůcka tak
  prosakovala do výstupu. Projevilo se to hlavně na hranici před **patičkou** (kotví se ke spodnímu
  okraji → čára „visela" uprostřed stránky nad bílou mezerou). U jediné sekce se nekreslila (jen mezi
  sekcemi), proto to nešlo reprodukovat na triviální šabloně.
- **Fix:** oddělovač z `PreviewCanvas` odstraněn úplně - **náhled teď zrcadlí výstup 1:1** (WYSIWYG).
  Hranice sekcí zůstávají jako vizuální pomůcka jen v **editačním plátně** (`DesignerCanvas` kreslí
  rámeček každé sekce sám), kde se edituje a dává smysl. PDF/PNG/tisk beze změny čisté.


Poslední dva body priority; celý track přesunut do roadmap-archive.

- **Rozsah platnosti ruční poznámky spoje** (`TiskJRMHDZastavkoveBodOd/Do`, helper
  `PoznamkaSpojeVRozsahu`): spec. znaky i textové poznámky se tisknou jen na stránkách zastávek
  v rozsahu - 1:1 legacy vč. jejich zvláštní „zpět" větve (brána přes BodDo, obrácené porovnání,
  `LinkyPrintFormatMHD:1047-1051`). Řeší „W - nejede do zastávky Globus" tištěné u nás všude;
  legenda 1073 = legacy 1:1 + jen auto-značky „jede jen…".
- **Parity test `TestParita1073KvDpSmoKolize`** (pre-push): zastávky 1:1 + poznámky pokrývají
  legacy (nový příznak `covered` v `MhdLegacyOracle.Summarize` = každý legacy klíč přítomen aspoň
  v legacy počtu) + odjezdy nadmnožina. Plná 1:1 na 1073 vědomě není cíl (SMO kolize, legacy
  ztrácí data). Ověřeno: test běží v package runu (výsledek 972) a prochází; „34/34" počítadlo
  runneru je multi-suite kuriozita, ne vynechaný test.
- Roadmap: priorita „MHD datová parita" KOMPLETNĚ → `roadmap-archive.md`; zbytky (varování
  v designeru, parametr „Rozdělení sloupců" pro MHD) → backlog.

## 2026-07-23 - MHD sloupce dle SMO konzistentně: prázdné sloupce + zrušen skrytý strop „Max skupin"

Nálezy z vizuálního smoke testu KV_DP 1073 (starý vs nový tisk):

- **SKUPINY_DNU = všechny SMO sloupce linky, i prázdné na dané stránce** (se záhlavím) - dřív se
  sloupec bez odjezdů na zastávce vynechal a zbytek se posunul (nekonzistentní layout napříč
  stránkami, neodpovídal SMO). Jako legacy. Gold 1001 re-baseline (jen md5, počty beze změny).
- **„Max skupin" default 3 → 0 (bez limitu)** - skrytý strop z éry před SMO seskupováním tiše
  ořezával 4. skupinu dnů v rozporu s checkboxy dialogu sloupců. Viditelnost řídí checkboxy;
  strop zůstává jen jako volitelná pojistka. **Migrace šablon 1.3.0 → 1.4.0**
  (`MigrationDepartureGridMaxGroups`: uložené `maxGroupCount: 3` = starý default → 0; vědomé
  hodnoty zůstávají) + testy, `template-schema-versions.md` doplněn o přehled verzí 1.1–1.4.
- **Dedup „stejné minuty" (fáze 4 SMO plánu) uzavřen jako bezpředmětný:** „00" + „00¹²" v 5:00
  jsou dva různé odjezdy (prázdninový vs. jen 31.12.) - sloučení by ztratilo informaci jako legacy;
  duplicity v rámci jedné skupiny tiskne i legacy. Čtení: bez značky = dle nadpisu sloupce,
  se značkou = dle legendy.
- Testy: .NET 367/367, Caché 34/34.
- **Nadpis sloučeného sloupce = dominantní typ + výčet ostatních na 2. řádku** („pracovní dny
  o prázdninách" / „24.12., 25.12. a 1.1., 31.12.") - čtenář vidí, co sloupec pokrývá, bez čtení
  legendy (návrh Jan); přepsatelné per šablona přes override nadpisu v dialogu sloupců. CB beze
  změny (sloupce bez sloučení nadpis nemění), gold 34/34 bez re-baseline.

## 2026-07-23 - MHD parita KV_DP dotažena: zastávky 1:1 + rozdílové poznámky A–K 1:1

Dokončení priority „MHD datová parita na složitých linkách". KV_DP 1073: **zast 693=693 ✓,
stránky 42=42 ✓, poznámky = legacy 1:1 (texty i písmena) + auto-značky (záměr)**; CB_DP 1001 vše 1:1.
Testy 34/34.

- **Příznak „netisknout na zastávkovém JŘ"** (`TisknoutZastavkovyTam/Zpet`, per bod trasy - KV_DP jím
  skrývá variantní větve): `PripravitTrasu` → `("trasa",SmerID,"netisknout",BodID)`; StopList i sběr
  atributů zastávek bod skryjí (kromě aktuální zastávky, 1:1 legacy `LinkyPrintFormatMHD:713-720`).
  Řeší zastávky 2× (1317→693) i duplicitní „na znamení" v legendě.
- **Alg. 1001 (texty rozdílů tras)** dostává `TisknutaTrasaBitmap` = obsluha bez skrytých bodů
  (1:1 legacy `:874`) → texty „zajíždí/nezajíždí/končí v…" 1:1.
- **Pořadí iterace odjezdů = legacy** (typ provozu vzestupně → hodina → minuta → spoj; dřív po
  sloupcích): na pořadí závisí přidělování písmen poznámek per stránka - teď A–K sedí s legacy
  vývěskou. Vedlejší efekt: jiné pořadí řádků `ODJEZDY` v JSON (element řadí sám) → re-baseline
  gold digestů 1001/1003/1006 (počty beze změny, jen md5).
- **Diagnostika:** `MhdLegacyOracle.StopProbe`/`NotesProbe` - výpis stop-listu/legendy jedné stránky
  legacy vs TD vedle sebe (trvalé nástroje pro budoucí parity práci).
- **SablonyTest opraven na chunk kontrakt** - od blob fixu (df22fe6) Save kanonizuje i jednoklíčová
  data na kusy a Load vrací `data1..N`; test četl staré `OUT("data")` → padal. Teď kusy skládá.
- Zbývá: gold pokrytí 1073 (roadmap bod 5) + drobná poznámka „W" (bod 6, rozsah BodOd/Do).

## 2026-07-23 - MHD: auto-značky sloučených skupin dnů (fáze 2 rozhodnutí „AD vzor")

Když v jednom SMO sloupci skončí víc typů provozu (KV_DP kolize), TD nově zajistí korektnost
sloupce sám - po vzoru AD varianty (auto-generovaná omezení), místo spoléhání na ruční datovou kázeň.

- **`PriraditZnackySmoSkupin`** (nová fáze 4b v `GetData`): dominantní typ (nejvíc spojů) dává nadpis
  sloupce (místo „poslední zapsaný vyhrává" → na 1073 už ne lživé „31.12."); každý ostatní typ dostane
  značku + legendu „jede jen <nadpis typu>". Značka: ruční `NegativniZnacka` typu z číselníku má
  přednost, jinak první volný znak z NZ řady (`RozsahNegativnichZnacekOd`, fallback 10 mimo denní 1–7).
  Značky jdou existujícím NZ kanálem (anotace „nz" + `nzPrirazen`) - žádný nový rendering.
- Podklad: `PromitnutSpoje` nově plní `spojTyp`/`smoTypy`; anotace se přidává v `SestavitJednuStranku`.
- **Ověřeno:** CB_DP 1001 dep/zast/pozn **1:1 match** (bez kolize = beze změny chování, gold zelený);
  KV_DP 1073: +115 řádků legendy „jede jen…" napříč 42 stránkami, nadpis sloupce 4 = dominantní typ.
- **⚠️ Vedlejší zjištění (experiment s čistým HEAD):** ranní „zastávky 693=693 vyřešeno" bylo měření
  proti serverové verzi třídy, která v gitu neexistuje - HEAD prokazatelně dává 1317 (2×) a poznámky
  A–K negeneruje. Bod znovu otevřen v roadmapě (ověřit u kolegy původ té verze, jinak fix napsat).
  Poučení zapsáno do paměti: před kompilací needitované `.cls` změřit baseline (server může být napřed).

## 2026-07-23 - MHD parita KV_DP 1073: diagnostika uzavřena - odjezdy +48 % = SMO kolize v datech

Analýza priority „MHD datová parita na složitých linkách" (bod 3 roadmapy). Nový oracle probe
`MhdLegacyOracle.SmoProbe` (stránky legacy vs TD + rozpad typů provozu linky na SMO sloupce).

- **Zjištění:** linka 1073 (KV_DP) má 7 typů provozu, z toho **4 mapované do SMO4** („pracovní dny
  o prázdninách" 77 spojů + „24.12." 37 + „25.12. a 1.1." 32 + „31.12." 64 = 210 spojů v jednom
  tiskovém sloupci). Stránky 42=42 → variantní stránky vyloučeny. Všech 12 vzorků dep-diffu má
  hlavičku „31.12.|" = kolizní sloupec.
- **Legacy kolizi neumí:** nadpis sloupce přepíše poslední typ (`LinkyPrintFormatMHD.cls:849`),
  minuty se per typ×hodina přepisují (`inxmm` nulován `:853`, zápis `:924`) → legacy **ztrácí
  odjezdy** (4295) a tiskne směs pod nadpisem „31.12.". TD sloučí bez ztráty (6352) → +48 %.
  **Bit-parity na 1073 není smysluplný cíl** (reprodukce ztrátového přepisu).
- **Rozdílové poznámky F–K (225 vs 213) mají jiný kořen:** legacy předává do alg 1001
  `TisknutaTrasaBitmap` respektující per-bod příznak `TisknoutZastavkovyTam/Zpet` („MH pro KV_DP",
  `:713-720`, `:874-875`); TD příznak nečte - zbývající kódový bod.
- **Výstup pro diskusi** (kolegové/DPKV): artifact „Skupiny dnů na vývěsce - analýza KV_DP 1073"
  s variantami řešení A (oprava dat u zákazníka, doporučeno) / B (warning v designeru) / C, D (zamítnuto).

Dokončení fixu z 2026-07-21 (chunking) - první pokus **neopravil pád**, protože měl špatně
identifikovaný limit. Ověřeno naostro na reálném obrázku (uložení + znovuotevření) + živý probe
proti dev serveru (chunkovaný save→load→delete round-trip).

- **Skutečný root cause (přepis mylné diagnózy „max string 3,64 MB"):** limit NENÍ délka Caché řetězce,
  ale délka **jednoho řádku při příjmu requestu**. MLRest server (`ALWA.SYS.Utility.StreamToGlobal`) čte
  vstup po záznamech přes `stream.ReadLine()` **bez délkového argumentu** → `%Stream` vrací nejvýš
  ~32 656 znaků na volání a řádek přes oddělovač **neslepuje** (ignoruje `eol`). Záznam delší než limit
  se rozpůlí, pokračování se rozparsuje jako „klíč" plný JSON/base64 znaků → neplatná indirekce
  `s @ref=val` → `<SYNTAX>` (proto ne `<MAXSTRING>`). Malé šablony (< ~32 KB) projdou v jednom čtení.
- **Vlastní fix:** velikost kusu `DefaultMaxChunkChars` **1 000 000 → 20 000 znaků** (bezpečně pod limitem
  řádku s rezervou). Kus o 1 MB byl 30× nad limitem → původní chunking nepomohl. Server `Sablony.cls`
  beze změny logiky (čte libovolný počet kusů) - jen zmenšení na klientovi.
- **Mechanika (z 07-21, platí dál):** blob jde **po kusech** (`dataChunkCount` + `data1..N`), server ukládá
  jako samostatné uzly `Data(n)`, **nikdy** neslepuje. Bez změny JSON schématu; legacy jednoklíčový `Data`
  se při loadu pozná (chybí `Data(1)`) a při dalším uložení kanonizuje. Zpětně kompatibilní obousměrně.
- **Testy:** `CacheBlobChunkingTests` (16×) - Split→Join round-trip vč. ~6 MB blobu, hranice/počty kusů,
  + regresní strážce velikosti kusu (`DefaultMaxChunkChars <= 30 000`, aby se limit znovu nevyšponoval).
- **Kapacita:** libovolný rozumný počet obrázků (reálně 2–5 na naše šablony); šablona roste lineárně, žádný
  zbylý single-string chokepoint na serveru → pro realistické velikosti nepadá (extrémní desítky MB by se
  jen zpomalily). Úložná strategie zůstává inline Base64 (self-contained JSON, ladí s Export/Import do souboru);
  přechod na binární úložiště přes `BinarySave`/GUID je vědomě odložený (dává smysl, až kdyby velké obrázky
  byly pravidlo).

## 2026-07-20 - MHD parita: sloupce odjezdů podle SMO (sekce minut odjezdu) - krok 1

MHD zastávkový JŘ na složitých linkách (mnoho typů provozu) datově nesedí legacy - gold pinuje jen
jednoduché CB_DP linky, takže to prošlo. Root cause #1: **dělení sloupců odjezdové mřížky.** Legacy
mapuje typ provozu → tiskový sloupec přes číselníkové pole `SMOID` (SMO1–4) a slučuje mnoho jemných
typů do ≤4 sloupců; TD dělal sloupec **per typ provozu** → na KV_DP desítky sloupců, rozsypané záhlaví.

- **Fix:** `SmoSloupec()` helper (typ → sloupec 1–4 dle `SMOID`, mimo SMO1–4 = netiskne se, jako legacy);
  `PromitnutSpoje` klíčuje odjezdy `smoCol` místo `TypProvozuID` + registruje `smoSloupce`/`smoNadpis`;
  `SestavitJednuStranku` staví `SKUPINY_DNU` i iteruje odjezdy po `smoCol`. Per-spoj logika (platnosti/NZ/
  rozdíly tras) beze změny (na `SpojID`). Uživateli se nic „SMO" neukazuje - jen čitelné nadpisy sloupců.
- **Ověřeno:** **CB_DP 1001 stále 1:1** (gold oracle: odjezdy 6597, zastávky 58, poznámky 1543 - vše match)
  → beze změny pinované gold, pre-push zelený. KV_DP sloupce vizuálně sedí s legacy.
- **Zbývá (viz roadmap „🔴 PRIORITA"):** #2 počty na složitých linkách (odjezdy 1,5×/zastávky 2× - které
  zastávky se v seznamu zobrazí, handling variantních tras) + #3 rozdílové poznámky A–K. Pravděpodobně
  společný kořen (variantní trasy). Krok 1 (sloupce) je hotový a bezpečný; #2/#3 jsou samostatné.

## 2026-07-20 - MHD tisk: odstraněno drahé nepoužívané pole „první/poslední spoj" (perf)

Přepínání instancí MHD trvalo ~5–7 s (velké linky), zatímco AD skoro okamžitě. Profilingem
(dočasné per-fáze `$zh` časovače) lokalizováno: **~91 % času `GetData` spolklo pole
`PRVNI_POSLEDNI_SPOJ`/`NEJEDOU_SPOJE`** - počítalo se **per stránka** (linka 1073 KV_DP: 6,8 s ze 7 s,
42 stránek) přes legacy MHD2 `CalculatePrvniAndPosledniSpojeText` (full sken spojů + `MakeTableSpoj`
za každý spoj + smyčka den-po-dni přes celé období platnosti).

- **Fix:** pole **odstraněno** (výpočet, oba scalary, pole ve schématu, mock, osiřelý helper `NejedouSpoje`).
  Nepoužívané (žádná šablona v repu ho nereferuje; per-linka flag `GenerovatNEJEDOU_SPOJE` ho jen zbytečně
  zapínal). MHD-only - AD `NEJEDOU_SPOJE` (= nástupiště, jiný levný význam) netknuto.
- **Výsledek:** `GetData` velké MHD linky z ~7 s na ~0,6 s (zbytek fází: trasa/spoje/bitmapy/stránky ~0,5 s).
- **Kdyby to někdo chtěl zpět:** reimplementovat „první/poslední spoj" v TD nad daty fáze 4 v `^||TiskMHD`
  (levné, bez legacy MHD2 volání per stránka) - poznámka v kódu na místě.
- ⚠️ **Gold:** odebrání scalaru mění MHD gold digesty (md5; počty pages/zast/odj/pozn beze změny) → re-baseline.

## 2026-07-20 - StopList: značky přes symbolový font (MHD „na znamení" konzistentní s legendou)

Značky u zastávek (na znamení, atributy, poznámky) se ve StopListu kreslily **normálním fontem**, takže
MHD „na znamení" (drátový kód SpecZnak 68) se ukázalo jako **surové písmeno „D"** místo piktogramu -
zatímco legenda a mřížka odjezdů (které symbolový Timetable font už používaly) ukázaly správný piktogram.
Nekonzistence hlášená z DPKV/MHD (U Jara „D", Panorama „D"; legenda „× na znamení").

- **Fix:** značky se kreslí **po úsecích s vlastním fontem** (`BuildStopMarkerRuns` + `DrawStopMarkers`):
  jednoznakový SpecZnak, který není textové „x/y/z", jde symbolovým Timetable fontem (piktogram);
  textové značky (AD „x") a poznámky normálním fontem. V jedné buňce nejde míchat font → značky
  oddělené od názvu a kreslené zvlášť, přes sdílené `CellLayout` helpery (zarovnání beze změny).
  Platí pro obě pozice značek (před/za názvem) i auto-šířku sub-buňky.
- **Agenda-agnostické:** žádná AD/MHD větev - rozdíl je jen v datech (SpecZnak číselníku firmy).
  AD „x" zůstává „x", MHD „D" → piktogram; StopList teď jede stejnou symbolovou cestou jako
  NotesLegend/DepartureGrid (uzavírá mezeru symbolového fontu C2 pro StopList). Relates #10660 (DPKV).

## 2026-07-15 - Tiskové parametry per šablona (obsahové)

Hodnoty **obsahových** tiskových parametrů (časová osa odjezdů, rozdělení sloupců, tisk šipek /
projetých zastávek, souhrny…) teď **patří šabloně** - sdílené mezi uživateli, priorita nad
per-agenda „naposledy použité", ukládají se **automaticky** se šablonou (změna → dirty → Ctrl+S).

- **Model:** aditivní `Template.PrintParameters` (`Dictionary<string,string>`, JSON `printParameters`).
  Staré šablony ho nemají → fallback na agendu (bez migrace).
- **Uložení:** `SyncParametersToTemplate` zapíše hodnoty obsahových parametrů (iteruje
  `_agendaParameters` → **export pole** název souboru/členění/imposice se záměrně nezachytí,
  zůstávají per agenda; převod exportu na per-šablonu odložen).
- **Načtení (priorita):** `Template.PrintParameters` > per-agenda > tvrdé defaulty.
- **Dirty:** `OnAgendaParamChanged` nastaví dirty jen při user změně (načtení jde přes
  `OnExternalParamsChanged`); `SetAgendaParamValue` je change-aware (žádný falešný dirty/re-fetch
  při reloadu EDInputform tabu).
- **Opravy z ladění:** cold-start časování - `RefreshFieldsList` nově vrací `Task`, otevírací flow
  ho awaituje (dřív async schéma přepsalo hodnoty defaulty); parametrový tab se po načtení hodnot
  **bezpodmínečně** přenačte z finálního stavu (`RefreshParamsUiAfterLoad`) - dřív se držel defaultů.

## 2026-07-15 - „Na znamení" řídí značku i legendu (#7161) + dev přepínač firem

- **StopList: „Na znamení" schová značku i legendu (#7161 D5).** Přepínač „Na znamení"
  (`ShowSignalStopIndicator`) teď řídí značku „na znamení" u zastávky **i** řádek legendy -
  vypnutím zmizí obojí (dřív mizela jen legenda, značka zůstávala). Příčina: ostrá AD i MHD data
  posílají „na znamení" jako **atribut** (ne přes pole `NaZnameni`), takže značku dřív řídil
  generický přepínač „Atributy". Nově se atribut „na znamení" rozpozná podle popisu
  (CZ „na znamení" / SK „na znamenie" - symbol se mezi agendami liší: AD „x", MHD „D") a řídí ho
  „Na znamení"; „Atributy" řídí jen ostatní atributy. **Značka** (TD render) funguje v AD i MHD.
  **Skrytí řádku legendy** vyžadovalo v MHD tag kategorie - `ZastavkoveJrMhd.BuildPoznamka` nově
  emituje `KATEGORIE` a řádek „na znamení" (dle popisu) je `atribut-zastavky`, takže sdílený
  `NoteVisibilityFilter` ho schová stejně jako v AD. Default beze změny (žádná regrese); MHD gold
  digesty re-baselinovány (jen md5, počty pages/zast/odj/pozn shodné → additivní tag, LegacyParity zelená).
- **Dev přepínač firem (jen pro vývojáře).** Nástroje → „Přepnout firmu (DEV)" - pevný seznam
  (CB_CSADAD, CB_GWBUS, KV_DP), přepne `Idfa` + datový namespace za běhu a znovu načte schéma/data
  (bez editace appsettings a restartu). Gate = lokální dev klíč (jako AI asistent), v produkci skryté;
  bez licenčního verify. Slouží k testování agend proti více firmám.

## 2026-07-15 - UX úklid (empty-state, hlášky DB, EDList výběr, Save dialog)

- **Empty-state panelu „Pole agendy"** - u agendy bez databázových polí (Obecná / bez připojení)
  se místo prázdna zobrazí hláška „Tato agenda nemá databázová pole…" (`ShowFieldsEmptyState`).
- **Přesnější hláška DB v dolní liště** - místo zavádějícího „○ Databáze: nedostupná" (evokovalo
  výpadek Caché) nově „○ Databáze: pro tuto agendu nejsou databázová pole" (týká se hlavně Obecné agendy).
- **EDList výběr (sdílené, EDControls)** - dvojklik na řádek = přepnout zaškrtnutí; Shift+klik =
  výběr rozsahu od kotvy. Platí pro všechny EDList seznamy (dřív bylo jen ad-hoc v jednom dialogu).
- **Uložení do databáze - nedostupné tlačítko místo popupu** - dialog „Uložit do databáze" má
  tlačítko „Uložit" nedostupné, dokud není vyplněné jméno (přes `EDInputForm.FieldChanged`); popup
  ze `Save()` zůstává jako záloha pro ostatní validace.
- **Sjednocení pořadových šipek** - `TableEditorDialog` (pořadí sloupců/řádků) měl tenké ↑↓,
  zbytek appky trojúhelníky ▲▼ → sjednoceno na ▲▼. (Šipky odsazení textu v CellStyleEditoru =
  směr, ne pořadí - ponechány ↑↓.)
- Roadmap: „jednotný resize grip" smazán (legacy, nejasné). **Rozhodnutí: toolbar písmenka
  („M"/„O"/„Fit"/„PDF"/„PNG"…) záměrně ponechána** - text je u PDF/PNG jasnější než ikona a drží
  styl; bod „toolbar text→ikony" z roadmapy odebrán.

## 2026-07-15 - Zákaznická wiki: 3 články (post-verze)

Dokumentace Návrháře na podpora.m-line.cz rozdělena na 3 prolinkované články (hub = příručka):

- **569** Uživatelská příručka (hub) - `90511fc5-10e5-476d-b7c1-3cd70672f26d`, zdroj `uzivatelska-prirucka.md`
- **570** Technické požadavky (IT) - `3a07cae1-1198-4ffa-8875-856e3fc36094`, zdroj `prirucka-it.md` (nový)
- **571** Novinky a plán - `6d48ca01-ce81-45d8-a396-76fcf0f982db`, zdroj `plan-a-zmeny.md`

Vše `internal` (zveřejnění + link do Edison widgetu „Novinka týdne" řeší uživatel). Zdroj pravdy =
`.md` v repu, wiki = zrcadlo (na wiki needitovat ručně). Název sjednocen **„Návrhář sestav"**
(dřív seed „Návrhář šablon" - kolidovalo s menu/e-mailem/názvy článků). Starý článek #6
(„uživatelský manuál", legacy nástroj) zatím ponechán.

**Release track uzavřen (2026-07-15):** verze v produkci (2026-07-14), dokumentace na wiki hotová.
Zákaznický e-mail se **dělat nebude** (rozhodnutí). `pre-version-checklist.md` **retirován/smazán**
(účel splněn - jeho instrukce v hlavičce to předpokládala); eval sada AI pomocníka z něj přesunuta
do `roadmap.md` (sekce AI). Nálezy byly průběžně přesouvány do roadmapy.

> ⚠️ **Learning - wiki markdown konvertor (TipTap) NEUMÍ tabulky**: GFM tabulka se slije do
> jednoho run-on odstavce. Do wiki článků psát tabulky jako **odrážky / definiční seznamy**,
> ne `| … |`. Nadpisy, seznamy (ordered/bullet), odkazy, tučné/kurzíva, blockquote, `---` fungují.

## 2026-07-15 - Snap na čtverec při resize (post-verze úklid)

- **Snap na čtverec při resize obdélníku** (poslední otevřený UX bod z pre-version §5) - u rohového
  úchytu tvarového elementu (Rectangle/Image/Barcode) se W≈H dorovná na přesný čtverec, když se
  strany přiblíží (práh 6 px přepočtený na mm dle zoomu). Protilehlý roh (kotva) zůstává, dorovnaná
  strana snapuje na grid, clamp na hranice sekce. Ctrl snap dočasně vypíná. Měřící badge při snapu
  vypíše „◻ čtverec". Uživatel nemusí rovné strany dopočítávat ručně. `MainWindow.xaml.cs` resize
  větev. Tím jsou nálezy pre-version checklistu §5 uzavřené (zbytek ověřen jako už hotový).

## 2026-07-13 - Vizuální audit UI + rychlé výhry (pre-produkce)

Audit modernosti vzhledu: základ zdravý (EDControlStyles flat theme, moderní paleta EDTheme -
Fluent/VS Code styl). Opraveny nálezy, kde se theme nedodržoval:

- **Barvy sjednoceny na EDTheme tokeny** - 44 hardcoded barev ve 14 souborech (v UI koexistovaly
  3 šedé, 2 modré, 2 červené): `#888`→`Text.Muted`, `#CCCCCC`→`Border.Medium`,
  `#C62828`/`OrangeRed`/`Firebrick`/`DarkRed`/`#D32F2F`→`Semantic.Danger`, `#2196F3`→`Accent.Blue`,
  `#F8F8F8`/`#DDDDDD`→`Bg.Footer`/`Border.Light`. Zároveň prerekvizita dark mode (EDThemeDark existuje).
- **TableEditorDialog: opraven skrytý bug** - lokální implicitní Button styl bez `BasedOn` přebíjel
  flat theme → tlačítka v dialogu měla výchozí šedý WPF 3D vzhled. Jednořádkový `BasedOn`.
- **Záložky levého panelu s ikonami** - 5 nesourodých Unicode dingbatů (▣⊞✎≣✦) nahrazeno
  EDIcons ikonami (Panels/Grid/Edit/Module/Help) + text.
- **Emoji pryč** - 💡 → „Tip:", ⚠ v červeném overflow bloku → jen text (blok sám je signál).
- **Status bar chipy zaobleny** - agenda/instance/overflow `CornerRadius="3"` + svislý margin
  (dřív ostré hrany na výšku celého baru).
- **Vědomě po verzi** (→ roadmapa): toolbar textová tlačítka („M"/„O"/„Fit"/„PDF") → ikony ·
  sjednocení ▲▼/↑↓ tlačítek · footer pattern + spacing tokeny v dialozích · dark mode zapojení ·
  dialog header pattern.

## 2026-07-13 - UX audit kritických flow + opravy (pre-produkce)

Dva fan-out audity (kritická flow prvouživatele + konzistence dialogů). Základ zdravý
(potvrzení destruktivních akcí, async vzory, jednotné Storno/Esc, 0× MessageBox). Opraveno:

- **Recent files** - jediná cesta otevírající šablonu bez potvrzení neuložených změn (tichá
  ztráta práce) → `ConfirmUnsavedChangesAsync` jako Ctrl+O/import.
- **Startovní agenda kontextově** - priorita: EDISON kontrakt (`Agenda=<kód>`) → **poslední
  použitá agenda uživatele** (`MainStatePref.LastAgendaCode`, capture při save stavu) → AD
  fallback. Odstraněn zafixovaný dev „NECOMMITOVAT" default.
- **PrintDialog „Tisk všech" už nemrazí** - fetch (HTTP per linka) + render na threadpoolu
  (`AsyncRelayCommand` s vestavěným guardem), snapshot parametrů (sdílený dict se dá měnit
  během fetche), render nad `DeepClone` šablony (nezávodí s preview renderem UI threadu),
  overflow check na UI (levný, sdílí element cache); dvoufázový progress ve StatusText.
- **AI klíč z menu s indikátorem** - wait-cursor + „Ověřuji dostupnost AI služby…" + guard
  proti opakovanému kliku (cold start mlweb resolve = sekundy, timeout až 60 s ticha).
- **„Uložit jako" kontroluje kolizi názvu** - stejné jméno v DB → potvrzení (dřív tichá
  nerozlišitelná duplicita); best-effort, výpadek listu neblokuje uložení.
- **PrintDialog**: `IsDefault` na Zavřít (Enter dřív nedělal nic), `ShowInTaskbar=False`.
- **Pomocník**: Enter ve vstupním poli odešle dotaz (`IsDefault` v UserControlu mimo dialog
  scope nefunguje → explicitní KeyDown).
- Dříve týž den (UI kosmetika): titulek „Návrhář sestav", vykání v Pomocníkovi, „z databáze"
  místo „z Caché", menu „soubor" místo „JSON soubor".
- **Vědomě odloženo** (→ roadmapa): VPN-down hláška „není nakonfigurována", empty-state Pole
  agendy, splash při startu, validace Save dialogů disabled tlačítkem, theme tokeny v editorech.

## 2026-07-13 - Nápověda F1: datově řízená + fulltext hledání (Ctrl+F)

- Obsah přesunut ze 500 řádků statického XAML do `HelpDialogViewModel.BuildContent()`
  (přidání zkratky = 1 řádek); XAML jen šablony (kategorie → řádky akce/zkratka → tip).
- **Hledání**: pole v hlavičce (Ctrl+F fokus, Esc smaže filtr), filtruje kategorie, akce,
  zkratky i tipy, **bez diakritiky** („razeni" najde „řazení"); „Nic nenalezeno" stav.
- Obsah srovnán s autoritativní tabulkou zkratek (ux-conventions) + doplněny nové sekce:
  Tisk a export (členění, imposice, ořezové značky, varování přetečení), Data/agenda/instance
  (Agenda×Varianta, štítky, F5), Výrazy a datová pole (formáty, aritmetika, podmínky, agregace,
  {PAGE}); opraveny šipky (1 mm / Shift = mřížka / Ctrl = 0,5 mm), doplněny Ctrl+1/Ctrl+B/
  copy-paste stylu; pryč uspaná Fixed tabulka. Texty bez anglicismů (chip→štítek,
  canvas→plocha, toolbar→lišta nástrojů) - zákaznický jazyk.

## 2026-07-13 - PDF import: Sonnet 5, robustnost proti halucinacím, standardní dialog

Průchod PDF importu před produkcí (živě testováno na KV_DP A5 vývěsce - vizuálně
i sémanticky nadprůměrný výsledek). Build 0 chyb, .NET 341/341.

**Model + prompty:**
- Vision import na `claude-sonnet-5` (adaptivní myšlení - percepční úloha; `max_tokens`
  16000, myšlení se počítá do limitu). `ModelSupportsTemperature` guard - Sonnet 5+
  temperature nedostane (jinak 400), starší modely dál 0.2.
- Sdílený `Services/AiPromptHelpers.AppendDataSchemaSection` - enricher i vision měly
  zkopírovaný identický blok formátování DataSchema (drift risk).
- Vision umí sloupec „Platform" (nástupiště, TR3) → `StopFields.Nastupiste`.

**Robustnost proti stochastice modelu (každý symptom z živého testu měl v kódu díru):**
- *Přetečení*: model vrátil rel. bottom 1.015 → element vyšší než stránka. Clamp AI
  souřadnic do 0.0–1.0 + pojistka v pipeline (pevné rozměry se clampnou do tiskové
  plochy; JŘ elementy obsah vejdou auto-scalem). NotesLegend nově s pevným boxem
  z AI bboxu (dynamická výška přetékala sekci s reálnými daty).
- *Falešný QR*: BarcodeElement vznikne jen s důkazem v PdfPig geometrii
  (`LooksLikeQrRegion` - shluk ≥15 malých čtverečků) + prompt „qrCode nevymýšlej".
- *Překryv obrázků*: dedup PdfPig obrázků (krok 5 vs 6 přidával týž obrázek 2×),
  placeholder se nepokládá přes existující element, krok 7 počítá obrázky do pokrytí.
- *Chybějící StopList*: retry - když v odpovědi chybí seznam zastávek/mřížka odjezdů,
  jeden opakovaný pokus; pokud ani pak, viditelné varování „doplňte ručně z palety"
  (dřív tichá díra ve výsledku).

**Standardní dialog (odstraněny volby, které uživateli nepatří):**
- Zůstává: PDF soubor + stránky. Vše ostatní odvozeno: **typ importu z otevřené agendy**
  (zastávková → AI JŘ import, jiná → obecný layout), AI vždy zapnuté, agenda = aktuální,
  sloučení grafiky pevně „pouze čáry". Bez dostupné AI je JŘ import zablokovaný
  (červená hláška s důvodem z mlweb - limit / chybí konfigurace).
- **JŘ import z vícestránkového PDF = právě jedna stránka** (výběr „Stránka: N z M");
  extrahuje/rasterizuje se jen ta jedna (dřív mlčky první + extrakce všech).
- Debug checkbox + debug soubory vedle PDF jen pro vývojáře (lokální dev klíč),
  bez popupu po importu. Mrtvá větev „AI obohacení textem" (haiku enricher) odstraněna
  z VM (vision ji nahradil; služba zůstává pro případný fallback).
- Generátor šablon: pole pro ruční API klíč jen pro vývojáře; hlášky nedostupnosti AI
  jednotně nesou důvod z mlweb (pomocník / generátor / import). Menu „AI Asistent
  šablon" před produkcí skryté (vidí jen vývojář); Import PDF zůstává pro všechny.

## 2026-07-13 - EDISONAI/mlweb napojení: spravovaný API klíč + hlášení spotřeby

Zákazníci nebudou mít vlastní Anthropic klíč - M-line jim ho spravuje přes mlweb
(podpora.m-line.cz), stejně jako u EDISONAI chatu. Build 0 chyb, .NET 341/341.

- **`Services/EdisonAiService.cs`** - replikace HTTP kontraktu EDISONAI (ne DLL reference -
  WinForms/.NET 4.8 vs WPF/.NET 10): (1) mlweb config z Caché `ALVA.EDISONAI.API.Config
  .GetMLWebConfig` (X-API-Key, baseUrl, isDev, devApiKey; jednou per session), (2) resolve
  `POST /api/mline/agent/resolve {idfa, loginName}` → allowed + apiKey, **fail-closed**
  (bez spojení/povolení/limitu AI nefunguje, hláška s reason), cache 60 s; jen provider
  `claude`, (3) spotřeba `POST /api/mline/usage {..., source: "templatedesigner"}` -
  best-effort na pozadí, odpověď `allowed` = gate pro další volání.
- **Pořadí klíče:** lokální `AiAssistant.AnthropicApiKey` = vývojářský bypass (mlweb se nevolá,
  spotřeba se nehlásí - dosavadní chování beze změny) → DEV server devApiKey → mlweb resolve.
- **Model zůstává v režii TD** (AssistantModel/VisionModel) - resolvovaný `model` z mlweb je
  konfigurace EDISONAI chatu; limity jsou tokenové, volba modelu je neobchází.
- **`AnthropicApiClient`** čte `usage` z každé odpovědi (text i vision) → `ReportUsage`.
- Call sites: pomocník fail-closed s hláškou; generátor + PDF import přes `TryGetAiApiKeyAsync`
  (AI tam je volitelný krok - bez klíče dál fungují bez AI).
- ⚠️ **Koordinace s autorem mlweb:** usage se hlásí se `source: "templatedesigner"` - ověřit,
  že endpoint neznámý source přijme (jinak se TD spotřeba nezapočítá do limitu); limit je
  sdílený s EDISONAI chatem per IdFa (záměr).

## 2026-07-13 - AI vrstva: Sonnet 5 + audit promptů (pomocník i generátor)

Před nasazením k zákazníkům (checklist „projít pomocníka"). Build 0 chyb, .NET 341/341.

- **Model:** pomocník (průvodce) i generátor šablon na `claude-sonnet-5`, konfigurovatelné přes
  `AiAssistant.AssistantModel` v appsettings. Vision import zůstává na `VisionModel`.
- **Sonnet 5 API specifika ošetřená v `AnthropicApiClient`:** temperature se neposílá (nedefaultní
  hodnoty S5 odmítá 400); pomocník volá s **explicitně vypnutým myšlením** (`thinking: disabled` -
  jinak je na S5 adaptivně zapnuté → latence), generátor s adaptivním (kvalita JSON); parsování
  odpovědi přes `ExtractText` = první `text` blok (thinking bloky můžou předcházet); generátor
  `max_tokens` 16000 (myšlení se počítá do limitu).
- **Generátor - opravy SystemPromptu proti reálnému modelu** (serializer tiše zahazuje neznámé
  properties → ploché fontSize/bold se dřív ZTRATILY): TextElement vnořený `style` objekt;
  Rectangle `backgroundColor` (ne `fillColor`); `TableElement` nahrazen `DynamicTableElement`
  (dataSource povinné, `headerStyle`/`dataStyle`, minRows/maxRows/expression/rowVisibility);
  plný výčet BarcodeType (+Ean13/Ean8/Pdf417/DataMatrix); přepsaný finální příklad.
- **Pomocník - znalostní vrstva v `CapabilityCatalog`:** trojstavové pravidlo nejistoty (v podkladech
  → přesná cesta / vím že chybí → „zatím neumíme" / nejistý → odkázat na F1/podporu) + grounding
  (každý krok cituje cestu v UI); `GeneralIntro` přepsán - **UI mapa Co→Kde** (tisk/imposice/členění,
  data/instance, vzhled, šablony v DB, navigace), Agenda×Varianta, výrazový jazyk, kompletní
  zkratky dle autoritativní tabulky (odstraněn neexistující Ctrl+Shift+S), sekce „co ZATÍM neumí";
  `ElementNote` pro DynamicTable (výrazy, zvýraznění, min/max, agregace) + LineTable, mrtvá Table
  větev pryč.
- **Eval sada** ~21 otázek (14 lookup + 6 chytáků na neexistující featury + 1 nejistota) v
  `pre-version-checklist.md` pro ruční otestování.

## 2026-07-13 - UX fixy z pre-version průchodu (blok A checklistu)

Nálezy z ručního testování před úterní verzí. Build 0 chyb, .NET 340/340.

- **Zoom ke kurzoru** - Ctrl+kolečko drží bod pod myší na místě (přepočet scroll offsetů po
  layout passu); dřív zoom „utíkal" doleva a uživatel se musel doscrollovat.
- **Snap rozměrů při vzniku elementu** - se zapnutým snapem se na krok mřížky zaokrouhlí
  i šířka/výška (výchozí velikosti 80×8, 190×25… nebyly násobky kroku → spodní hrana nikdy
  neseděla na mřížce). Element menší než krok (čára) se nezvětšuje.
- **Nové defaulty designeru:** SnapToGrid ZAP + krok mřížky **1 mm** (dřív VYP + 5 mm - hrubý krok
  byl hlavní zdroj pocitu „nepřesného" snapu). Per-user MainState / per-template EditorPreferences
  mají přednost - stávající uživatelé svůj stav neztratí.
- **Imposice viditelná ve statusu** - každý export s aktivní imposicí vypíše „… · imposice 2×1
  na list 210×297 mm" (per-agenda persistovaná volba jinak překvapila „proč mám půlku stránek").
- **Paleta/menu:** Kód (QR…) před Tabulkou. **Šipka směru:** labely voleb vysvětlují kreslení
  u okraje elementu (u širokého elementu je vpravo „daleko" - vypadalo jako nefunkční).
- Do backlogu (roadmap): snap/indikace čtverce při resize · per-šablona hodnoty tiskových parametrů.
- **`Released` flag agend (blok B průchodu):** výběr agendy nabízí jen zveřejněné - Obecná +
  Zastávkový JŘ (AD/MHD); Linkový JŘ (pilot v1) a Faktury (vzorová) skryté. Data-driven:
  `BaseAgenda.Released` parametr (default 0 - nová agenda „neuteče" omylem), registr wire aditivně
  (`released` bool, starší server → default true). Aktuální agenda šablony vidět vždy;
  existující šablony + EDISON launch nezveřejněných fungují dál.

## 2026-07-12 - UI doladění preview: sbalitelná lišta parametrů + skok na stránku dat + export volby dle agendy

Před-produkční UX úklid (analýza + implementace na žádost uživatele). Ověřeno: build 0 chyb,
.NET 344/344 (+4), Caché wire živě (GetParameters `group`, AgendaParams skupiny + split volby).

- **Sbalitelná lišta parametrů nad preview** - dřív WrapPanel (pole se zalamovala „náhodně" dle šířky
  panelu a tlačila preview dolů). Nově default sbaleno na 1 řádek „⚙ Parametry ▾" + **souhrn hodnot
  odchylných od defaultu** (uživatel vidí, že má něco přepnuté); klik kamkoli do hlavičky rozbalí
  svislý formulář. Stav persistován v `MainStatePref.ParamsBarExpanded`.
- **Seskupení parametrů `group`** (aditivní klíč kontraktu, R3) - `GetParameters` posílá skupinu;
  **konvence: skupina = UI název elementu, který parametr ovlivňuje** („Seznam zastávek" /
  „Mřížka odjezdů" / „Poznámky a legenda"), celo-šablonové parametry → „Obecné" (feedback
  uživatele - dřív tematické „Obsah"/„Vzhled"). `AddBoolParam`/`AddNumberParam` volitelný 5. arg,
  enumy `p.group`. Lišta groupuje přes CollectionViewSource (hlavičky skupin, prázdná skupina bez
  hlavičky), **PrintDialog** přes EDInputForm skupiny (`AgendaParams.AddParamGroups`, pořadí skupin =
  první výskyt v GetParameters - proto SOUHRN_NA_ZNAMENI přesunut k poznámkovým parametrům).
- **Skok na stránku dat v preview** - text „3 / 40" nahrazen ComboBoxem s popisky stránek
  „N - zastávka → směr" (`BuildPreviewPageLabel` z POPIS_ZASTAVKA/POPIS_SMER, fallback „Stránka N")
  + „/ celkem"; ◀▶ zůstávají. Konec proklikávání 37 stránek na konkrétní zastávku; combobox zároveň
  ukazuje, CO aktuální stránka zobrazuje.
- **Export volby dle capability agendy** (audit C-f) - `AgendaParams.AddExportFields` čte class
  parametry agendy: `PER_STOP` („Jeden PDF na zastávku/směr") jen pro `ElementKind=STOP`, `PER_LINE`
  label z `InstanceLabel` v akuzativu („Jeden PDF na linku" / „…na fakturu"; helper `Akuzativ`).
  Uložená hodnota, kterou agenda nenabízí → fallback SINGLE.
- **Oprava latentního bugu `TISK_SIPKY` default** - call-sites předávají string `"true"`, který COS
  v `$s(defaultVal:...)` vyhodnotí numericky jako 0 → wire `defaultValue:"false"` a šipky směru
  defaultně VYPNUTÉ proti záměru (element default `ShowDirectionArrow=true`, docs „true").
  `AddBoolParam` nyní bere `1` i `"true"`. ⚠️ Behavior change pro uživatele bez uložených PrintParams:
  šipky nově defaultně zapnuté (= původní záměr).
- **Instance chip** - šipky ◀▶ skryté při 0–1 aktivní instanci (dřív jen disabled, matoucí).
- **Stabilní pozice ◀▶ při proklikávání** (feedback z testu) - různě dlouhé názvy stránek/linek
  posouvaly tlačítka pod myší (navigace je centrovaná → šířka obsahu hýbe layoutem). Combobox
  stránek pevná šířka 230px (dropdown se obsahu přizpůsobí), text instance chipu pevná šířka
  260px + ellipsis + tooltip **jen při více instancích** (DataTrigger - u 0–1 instance chip
  zůstává kompaktní).
- **Fix prázdného comboboxu stránek po výměně dat** (feedback z testu „stránkování zmizelo") -
  výměna ItemsSource synchronně pushne SelectedIndex=-1 do VM a zpětná korekce se během
  reentrantního binding update zahodí → combobox zůstal prázdný. `NotifyPreviewPagesChanged`
  notifikuje v pořadí PreviewPageItems → RealPreviewIndex (index srovná po rebindingu).
  + Lišta stránkování nově viditelná i pro **jedinou** stránku dat (`HasPageNavigation` -
  combobox říká, CO preview zobrazuje; skrytá jen v mock módu).

## 2026-07-11 - Dvouosý model agend: Agenda × Varianta (názvosloví dle legacy analýzy)

Roadmap „typ šablony vs agenda" (podnět kolegy). Analýza legacy (RG strom `agenda→oblasti→sestavy`
z číselníku `ALVA.EDISON.Oblast`; TiskMHD/TiskJR = typ jako aplikace + skrytý `VolanoZ` JRMAIN×MHD,
šablony sdílené oběma zdroji) → rozhodnutí: **Agenda** = nejvyšší úroveň („Zastávkový JŘ", „Linkový JŘ",
„Faktury"; JŘ povýšené jako důležité, budoucí RG agendy přibudou vedle), **Varianta** = datový zdroj
(AD/MHD, ekvivalent legacy `VolanoZ`; předvolená launch kontextem). Duální role MHD (agenda provozních
tisků × varianta JŘ) odpovídá legacy. Ověřeno: build 0 chyb, .NET 340/340 (+4), Caché wire živě.

- **Caché:** `BaseAgenda` parametry - kombinovaný klíč `AgendaVariantCode` (dřív `AgendaCode`,
  hodnoty beze změny) + nové osy `Agenda`/`Variant` + `VariantLabel`; `AgendaLabel` nově jen label
  agendy („Zastávkový JŘ"). 5 agend + `AgendyRegistr` (wire aditivně: nové klíče `agenda`/`agendaLabel`/
  `variant`/`variantLabel`; staré `code`+`label` (kombinovaný formát „Zastávkový JŘ (AD)") zůstávají
  pro nasazené klienty).
- **.NET:** `AgendaRegistryEntry` + `Agenda`/`AgendaLabel`/`Variant`/`VariantLabel` (defaulty =
  fallback pro starší server), computed `DisplayName` („Zastávkový JŘ · AD") + `EffectiveAgendaLabel`.
- **UI:** ChangeAgendaDialog seskupený podle agendy (GroupStyle; řádky = varianty), staví z registru
  místo AgendaInfo; chip ve status baru + titulek okna `[Zastávkový JŘ · AD]` místo syrového kódu;
  filtr v přehledu šablon dvouosé popisky řazené po agendách.
- **Beze změny (vědomě):** hodnoty kódů `ZASTAVKOVY_JR_AD`… (uložené šablony, DB index ByAgenda,
  per-agenda preference, EDISON launch `Agenda=<kód>`), JSON pole `agendaCode`,
  `Template.AgendaCode` v C# (kombinovaný klíč - název sedí s wire; ambiguita byla jen v Caché
  třídě vedle parametru `Agenda`, tam vyřešeno). Slovo „agenda" v UI textech zůstává - nově znamená
  první osu, což je v souladu s firemním/legacy významem.
- **Doladění po uživatelském testu (týž den):** chip explicitně „Agenda: Zastávkový JŘ ·
  Varianta: AD" (učí názvosloví; bez varianty jen „Agenda: Faktury"); Ctrl+O sloupec „Agenda"
  (syrový kód) rozdělen na dva - „Agenda" + „Varianta" s labely resolvovanými server-side
  (`EDList.Sablony.ResolveAgendaVariant`, per-request cache, GENERAL → „Obecná").
- **Follow-up (roadmap):** strom v přehledu šablon (Ctrl+O) - EDList → seskupení per agenda.

## 2026-07-10 - Imposice: skládání stránek na fyzické listy (N-up, 2×A5 → A4)

Roadmap bod č. 1. Vlastnost VÝSTUPU, ne šablony: šablona vývěsky zůstává A5 (designer i náhled
beze změny), teprve PDF export skládá stránky 1:1 na listy (blok centrovaný, bez zmenšování).
Ověřeno: build 0 chyb, .NET 336/336 (+20: SheetComposer unit + PdfPig end-to-end smoke).

- **`Models/Layout/SheetLayout.cs`** - čistá vrstva bez SkiaSharp/WPF: `SheetLayoutOptions`
  (list, mřížka, opakování, značky + geometrie bloku), `SheetComposer` (`FromParamValues` parsování
  wire hodnot + validace, `ResolveGrid` AUTO = největší 1:1 fit, `Compose` stránky → listy),
  `SheetLayoutKeys` (wire kontrakt `__EXPORT_SHEET*`).
- **UI = exportní pole v PrintDialogu** (vzor `__EXPORT_SPLIT`): „Uspořádání na list"
  (Vypnuto/A4/A3 × výška/šířka), „Mřížka" (Auto/1×2/2×1/2×2/2×3/3×2/3×3), „Každá stránka na celý
  list (pro řezání)" - táž vývěska N× - a „Ořezové značky" (tenké šedé čáry na hranicích řezu).
  Detailní pole vidět jen při zapnutém listu (`ApplyVisibility`). Caché
  `AgendaParams.AddExportFields` + persistence per-agenda přes PrintParams (`ExportKeys` rozšířeno
  → restore při reopenu zdarma). Nevalidní mřížka (nevejde se 1:1) → česká hláška ve status baru.
- **Render:** `RenderMultiDataToPdf`/`RenderToPdf` volitelný `SheetLayoutOptions` - imposiční
  větev nalayoutuje logické stránky, `SheetComposer.Compose`, každý list = 1 PDF stránka,
  `RenderPage` s offsetem (už uměl). Aplikuje se v PrintDialogu (mock i ostrá data, split módy -
  imposice uvnitř každého souboru) i v exportu z hlavního okna (options zachyceny na UI threadu
  se snapshotem). PNG export záměrně bez imposice (rastr per stránka). Lichý počet → poslední
  pozice prázdné.
- Brožura/duplex řazení vědomě v2 (potřebuje až knižní JŘ) - model je na rozšíření připravený
  (options record + composer).

## 2026-07-10 - TR4: fixed-then-shrink výška řádku StopListu + sdílený helper

TRANSDEV požadavek (3. režim výšky řádku „zastávka"). Ověřeno: .NET 316/316, build 0 chyb. Rendering-only.

- **StopList:** pevná `DataRowHeight` se nově **zmenší při přetečení** pevné Height (dřív zůstala → obsah
  přetekl/ořízl). **Default (auto) beze změny** - mění se jen chování ručně nastavené pevné výšky při
  nedostatku místa (přeteče → zmenší). = přesně „fixed-then-shrink", který TRANSDEV chce.
- **Sdílený helper** `ScheduleElementBase.ScaledDataRowHeight(fontPt, factor)` - pravidlo fixed-then-shrink
  v **jednom místě**; StopList i DepartureGrid ho sdílí. (Dřív se rozešly: B2 opravil DepartureGrid, StopList
  lagoval - přesně ten drift.) DepartureGrid přepnut na helper (behavior-preserving). Malý extrakt; **plná
  unifikace řádkového layoutu = krok C** (R1/row-plan, gated).

## 2026-07-10 - TR2: souhrn „všechny zastávky na znamení" (AD, opt-in) - čeká na SK ověření

TRANSDEV požadavek - když jsou všechny zastávky na znamení, místo × u každé jeden souhrn.
Implementováno, ověřeno gold-safe; verifikace triggeru čeká na potvrzení SK kódování (kolega).

- **AD-only, opt-in** param `SOUHRN_NA_ZNAMENI` (default off = per-zastávka × jako dnes). Když všechny
  obsluhované neprojeté zastávky mají „x" (na znamení) → potlačí per-zastávka × + agregátní řádek legendy
  „× všechny zastávky linky jsou na znamení". Mechanika 1:1 analogická bezbariér agregátu (`allBBP`).
  **Čistě Caché** (žádná .NET změna - potlačení = absence atributu v datech, agregát přes POZNAMKY legendu).
- **Gold-safe:** Caché 33/33 (default off → gold beze změny). Scan potvrdil no-op na CB_CSADAD i SKNI
  (ani jedna linka není all-„x").
- ⚠️ **Čeká ověření (kolega):** `SKNI_MHDNITRA` (1308 linek) **nepoužívá „x"** pro na-znamení
  (dta2 znaky = A/E/L/P/T/o/p). Nutno potvrdit, jestli SK data používají „x" (pak feature funguje) nebo
  jiné kódování (pak rozšířit detekci `JeViditelnyAtribut` + scan). Do té doby na SK datech netriggeruje.

## 2026-07-10 - TR3: nástupiště jako sloupec zastávky (AD + MHD)

Zákaznický požadavek TRANSDEV (SK legislativa) - zobrazit nástupiště/označník u zastávek. Ověřeno:
Caché TD 33/33, TemplateDesigner.Tests 316/316, build 0 chyb.

- **Caché:** per-zastávka `NASTUPISTE` do kolekce ZASTAVKY - **AD** z J-věty `#3` (`nastTam:nastZpet` dle
  směru), **MHD** z `Oznacnik` (`ALVA.MHD.ZST`). Bez mocku/hardcoded dat - kde označník není zadaný,
  zůstává prázdné (standardně jen terminály/uzly). Gold re-baseline AD 2 + MHD 3 (counts identické, jen md5).
- **TD:** nový `StopFields.Nastupiste = "NASTUPISTE"` (sdílený kontrakt AD≡MHD zachován, zámek v
  `ColumnKeysTests`), parse do `ZastavkaData`, **nový StopList sloupec „Nástupiště"** (default skrytý = opt-in),
  `GetCellText`.
- **Reálná data ověřena:** CB_CSADAD linka **0320680** má nástupiště na 6 zastávkách (por 1/8/12/15/18/20,
  hodnoty 11, 1:4, 5:6, 1:1, 10:10, 9:9) - formule `#3` funguje. Většina zastávek prázdná (normální).

## 2026-07-10 - Audit A-AD + B2 (uzavření kontraktu skalárů + overflow konzistence)

Dokončení bloku A (AD strana, odblokováno po dojezdu paralelní font/značky práce) + B2.
Ověřeno: Caché TD 33/33, TemplateDesigner.Tests 316/316, build 0 chyb.

- **A-AD** - `SMER` (T/Z) + `NASTUPISTE` do `ZastavkoveJrAd` GetData + schéma (AD analog A1/A2; AD už měl
  `POPIS_SMER`=konečná). `NEJEDOU_SPOJE` ponecháno duplicitně (deprecated ve schématu). AD `GetMockData`
  jede přes `BuildStrankaZastavky` → nová pole dostane automaticky (žádný hardcoded mock). AD gold
  re-baseline 2 linky (0365001, 0365005 - counts identické, jen md5 = přidané skaláry, struktura zachovaná).
  Tím je **kontrakt skalárů sjednocen napříč MHD/AD/Linkový** (SMER=T/Z, POPIS_SMER=konečná všude).
- **B2** - DepartureGrid: `scale` i overflow/`CalculateHeightFromData` počítají ze **stejné skutečné výšky
  řádku** (vč. explicitní `DataRowHeight`). Odstraněna výjimka, co u nastavené `DataRowHeight` nezmenšovala
  fonty (obsah pak tiše přetékal). Nově se řádek škáluje konzistentně (shrink i grow) → **auto-shrink do fit
  jako ostatní JŘ elementy** (rozhodnutí: držet konzistentní shrink pattern). Bez gold dopadu (jen rendering).

## 2026-07-10 - Zastávkový JŘ: kroužkové denní značky týdenní platnosti (1-7) - dotažení 1:1 legacy

Dokončení font-bloku značek. Legacy (`mkPLinWin`/`GSId`) emituje týdenní platnost spoje jako kroužkové
číslice `1`-`7` (GSId id 8-16); náš kód je buď neemitoval (AD), nebo jako text „Po"/„Ne" (MHD).

- **AD** - z `plk` (`$p(hlava,":",7)`) se vytáhnou konkrétní dny (číslice 1-7) → anotace u minuty
  (`specialni` číslice → symbolový font ji kroužkuje) + legenda „① jede v pondělí" (`TextDneVTydnu`,
  plumbing přes `dnyLegenda` jako `bezbarLegenda`). Prázdné/`.`/`X`/`+` = skupinový vzor → bez značky
  (1:1 legacy: značka jen u konkrétních dnů). Ověřeno živě (spoj „jede v sobotu" → kroužková ⑥).
- **MHD** - emituje den jako **číslici** (`denZnak`) místo `ConvertSpecialniZnak` → „Po" (konzistentní
  s legendou, která už číslici používala). Gold-safe (CB_DP gold linky denní spoje nemají → no-op).
- Gold re-baseline AD (0365001 pozn 96→110, 0365005 168→183 = přidaná denní legenda; odj/zast beze změny).
  Caché 48/48, .NET 316/316.

Tím je **font-blok značek kompletní**: bezbariér piktogram, na-znamení „x" text, NZ box, datová omezení
box, týdenní platnost kroužkové 1-7. Vše locale-aware (CZ/SK).

## 2026-07-10 - Audit infra batch (B3, B4, B6, B7)

Robustnost/extenzibilita mimo kolizní zónu paralelní schedule-rendering práce. Ověřeno:
build 0 chyb, TemplateDesigner.Tests 316/316 (+11 z B4).

- **B7 - první `GetSchema` (R1) mimo UI thread.** `RefreshFieldsList` na cold cestě (uncached schéma)
  odbaví `GetSchema` přes `Task.Run` + doplní strom polí po návratu (`RefreshFieldsListCore`, marshalováno
  `TaskScheduler.FromCurrentSynchronizationContext`); warm (cachovaná) cesta zůstává synchronní. Nový
  `CacheAgendaProvider.IsSchemaCached`. Signatura `RefreshFieldsList()` beze změny → nulový caller ripple.
  Odstraňuje zamrznutí UI při otevření šablony cizí agendy na pomalé síti. + **`GetAvailableSkupiny` cache**
  (`_skupinyCache`, invalidace v `InvalidateCaches`) - sourozenci ji měli, tahle ne.
- **B4 - drift test registrace elementů.** `ElementRegistrationDriftTests`: reflexe `[JsonDerivedType]`
  na `TemplateElement` → pro každý (mimo allowlist uspaných `{Table}`) ověří, že `CreateElementByType`
  vrátí přesně ten typ (odhalí `_ => TextElement` fallback maskující zapomenutý case). Factory-only scope
  (paleta/Skia mimo). Allowlist se hlídá proti zplesnivění.
- **B3 - `CapabilityCatalog` data-driven.** AI průvodce už nebere JŘ elementy z string heuristiky
  (`Contains("JR")…`) + hardcoded seznamu, ale z `AgendaCapabilities.ScheduleElementTypes(ElementKind)` -
  jediný zdroj pravdy shodný s paletou/menu (zná LineTable). `ElementKind` protažen do `AiGuideContext`.
  `GenericTypes` opraveno `Table`→`DynamicTable` (AI katalog popisoval uspanou tabulku), `FriendlyName` +LineTable.
- **B6 - hardening resetu stránky.** String klíče property-path → sdílené konstanty `PagePathWidth/Height`;
  log když `FinalValues` klíč rozměru chybí (tichý výpadek detekce resetu se zviditelní); `OpenPropertiesForPage`
  `async void` → `async Task` s try/catch (chyby přes `IPopupDisplayer`, R2), callery `_ = …`.

## 2026-07-10 - Audit blok A: kontrakt skalárů zastávkového JŘ (MHD strana)

Sjednocení skalárního kontraktu podle vzoru linkové agendy (SMER = T/Z, POPIS_SMER = konečná).
Aditivní (R3). MHD strana + schéma + mock hotové; AD strana (přidat SMER, NASTUPISTE) odložená až
po commitu paralelní `PozitivniZnacka` práce (dotýká se AD gold testu → aby se neklobrovala).

- **A1 (MHD)** - `POPIS_SMER` už netiskne „T"/„Z", ale **název konečné** zastávky směru (jako AD/Linkový).
  T/Z je nově v samostatném poli **`SMER`**. Konečná = poslední zastávka ve směru (zachyceno ve smyčce
  ZASTAVKY, `konecnaNazev`). ⚠️ Breaking pro pilotní MHD šablony s `{POPIS_SMER}` pro T/Z → přejít na `{SMER}`.
- **A2 (MHD)** - `NEJEDOU_SPOJE` (legacy misnomer, u MHD = text prvního/posledního spoje) → nový klíč
  **`PRVNI_POSLEDNI_SPOJ`**; `NEJEDOU_SPOJE` ponecháno duplicitně pro kompatibilitu, ve schématu označeno „(zastaralé)".
- **A3 (MHD mock)** - mock doplněn o `SMER`=„T", `PRVNI_POSLEDNI_SPOJ`; `POPIS_SMER`=„Bystrc, zoo" je nyní
  pravdivý (dřív mock ukazoval konečnou, ostrý tisk „T").
- **A4 (schéma)** - AD deklaruje dříve nedeklarovaná pole: `POPIS_ZASTAVKA_CIS`, `POPIS_SMER_CIS`
  (AD-specifické, MHD je zatím neprodukuje); `VARIANTY_TRAS` + `TRASA_MIN` zdokumentovány jako wire-only
  (pole, ne skalár → plochý binding je nekonzumuje). AD `POPIS_SMER` label sjednocen na „Konečná směru".
- **MHD gold re-baseline** (3 linky) - `pages/zast/odj/pozn` **identické**, změnil se jen md5 (= jen skalární
  hodnoty, struktura zachovaná). Ověřeno determinismem (2. běh = shodný digest). MHD 32/33 (1 fail = AD 0365005
  je pre-existing z paralelní WIP, ne z této změny).

**Odloženo:** A1/A2 **AD strana** (přidat `SMER`, `NASTUPISTE`) - po commitu `PozitivniZnacka`; MHD `_CIS`
varianty (až bude potřeba, dohledat zdroj).

## 2026-07-10 - Audit před produkcí: bezpečný sweep (bloky B/C/D)

Odbaveny nízkorizikové nálezy auditu 2026-07-09 (behavior-preserving / aditivní / docs). Blok A
(kontrakt skalárů) a položky vyžadující UX rozhodnutí / gold data zůstávají otevřené v roadmapě.
Ověřeno: build 0 chyb, Caché 33/33 (gold+smoke+helpery), .NET 898/898 (TD 305 + EDControls 331 + EDISONMAIN2 262).

**Caché (behavior-preserving):**
- **C-a - symetrický úklid working global.** `k ^||TiskMHD` (MHD) přesunut z konce try za catch;
  `k ^||TiskAd` (AD) doplněn za catch. Working global se uklidí i po chybě (u poolovaných procesů
  nedrží data mezi requesty). Self-healing kill na začátku zůstává.
- **C-c - `AgendaParams.FetchParamsJson` zachovává `ERR`.** Wholesale `k ^||ML("OUT")` po nested
  `GetParameters` mazal i chybový uzel → selhání parametrů se tiše ztratilo (prázdný seznam místo
  hlášky). Nyní save/restore `ERR` okolo resetu counteru (counter se resetovat musí - `GetParameters`
  píše přes `$i(^||ML("OUT"))`, ne fixní `OUT("1")` jako `Instance.cls`).
- **C-d - `GetParameters` bez zbytečného `$$$FillParams`** (ZastavkoveJrAd + LinkovyJrAd). Parametry
  IdFa/dataDB nečtou → FillParams mimo try/catch byl jen riziko pádu mimo chybovou konvenci.
- **C-f (část) - `q $$$OK` → `ret $$$OK`** v ZastavkoveJrMhd + ZastavkoveJrAd (konvence TD).
  Export-split filtr dle `ElementKind` odložen (není mechanický - chce plumbing ElementKind do AgendaParams).

**.NET:**
- **B1 - DepartureGrid layout fingerprint doplněn** o `DataRowHeight`, `AnotaceGap`, `OdjezdGap`,
  `GroupSeparatorWidth`, `HourColumnPerGroup`, `ShowSkupinaDnuHeader`, `SkupinaDnuId`. Změna těchto
  vlastností v panelu se dřív v náhledu neprojevila (cache se neinvalidovala) - vypadalo to jako „mrtvé" pole.
- **B5 - F5 refresh inkrementuje `_previewDataGeneration`.** Guard proti dosednutí staršího in-flight
  fetche (souběh F5 × přepnutí instance).
- **D3 - drobné úklidy:** PrintDialog „Zavřít" → `{StaticResource EDStrings.Close}`; AI API klíč
  `TextBox` → `PasswordBox` (sync přes code-behind, Password není bindovatelný); AI debug JSON do
  `Path.GetTempPath()` místo instalačního adresáře; komentář k prázdnému `catch` v CellStyleEditor converteru.

**Docs (D1, D4):**
- **D1 - tabulka zkratek** (`ux-conventions.md`): sekce = `Ctrl+Shift+↑/↓` (ne holé ↑/↓); doplněn
  Shift=velký / Ctrl=jemný krok posunu elementu.
- **D4 - font 42b/C2** v roadmapě přeformulován (zastávkový JŘ hotový 9.7., zbývá linková část LineTable `ZNAK`).

**Odloženo (zůstává v roadmapě):** B2 (DepartureGrid overflow vs auto-scale - **nese UX rozhodnutí**,
ne mechanický fix: Render záměrně počítá scale z font-driven výšky, aby velká `DataRowHeight`
nezmenšovala fonty, kdežto overflow detekce bere `layout.RowH` - sjednocení musí rozhodnout, co má
u velké `DataRowHeight` vyhrát), celý blok A (kontrakt skalárů), B3/B4/B6/B7, C-b/C-e, D2, agenda-rename sjednocení.

## 2026-07-09 - Komplexní audit před produkcí (architektura + agendy + UI/UX)

Tři paralelní průzkumy kódu (.NET architektura/extenzibilita, Caché agendy MHD↔AD, UI/UX +
pravidla R1–R5) + revize dokumentace. Výstup:

- **[`audit-pred-produkci-2026-07-09.md`](audit-pred-produkci-2026-07-09.md)** - detail všech
  nálezů (file:line + lidské vysvětlení + doporučený fix).
- **Roadmap sekce „🔍 Audit před produkcí"** - rozhodovací checklist (bloky A–D) k projití.
- Nejzávažnější: **kontraktní rozkol skalárů zastávkového JŘ** (`POPIS_SMER` = „T/Z" v MHD vs
  název konečné v AD; `NEJEDOU_SPOJE` = první/poslední spoje v MHD vs nástupiště v AD; MHD mock
  drift). Dále DepartureGrid stale layout-cache, CapabilityCatalog drift (nezná LineTable),
  první `GetSchema` sync na UI threadu (R1), Caché hygiena (working global kill, ERR polykání).
- Pozitivní: vrstvy drží, wire čistý JSON, versioning s guardy, dirty-flag kompletní, žádný
  MessageBox ve VM, API klíč mimo git. Žádný kód neměněn - jen analýza + zápis.

Změna velikosti/orientace stránky se dřív snažila obsah „zachovat", ale jen napůl - přeškálovala jen
výšky body sekcí (`ScaleBodySections`), pozice a šířky elementů nechala být. Výsledek po A4→A5 / otočení:
elementy visely mimo stránku, překrývaly se, sekce squeezené. UX rozhodnutí (potvrzeno s uživatelem):
změna rozměrů = **čistý reset**, konzistentní se změnou agendy.

- **`ElementEditDialog.FinalValues`** - snapshot finálních hodnot polí po OK (klíč = property path).
  Volající přečte zamýšlené rozměry **bez** vykonání commandu (obecné, i pro jiné editory).
- **`OpenPropertiesForPage` detekuje změnu `PageSize.Width/Height`** (epsilon 0.1 mm). Změna →
  `ResetForPageChangeAsync`; jinak běžná undoable změna (okraje, kultura, font) přes `Execute`.
- **`TemplateDesignerViewModel.ConfirmPageResetAsync`** - vždy se ptá (i u uložené šablony): dirty →
  Save/Discard/Cancel, čistá → prosté potvrzení. Řeší tichý wipe (dřív by `ConfirmUnsavedChanges`
  u čisté šablony vrátil true bez ptaní).
- **`ResetForPageChangeAsync`** - čisté plátno s novou velikostí; zachová agendu (PageEdit ji needituje),
  převezme velikost/okraje/kulturu/font z dialogu přes `ElementPropertyAccessor` (reflexe, klíč = path).
  `ReplaceWithBlankTemplate` rozšířen o `configure` callback (PageSize nastaven před přidáním sekce).
- Odstraněna půlka řešení (`ScaleBodySections` v deskriptorové cestě už není use-site - descriptor
  editor stránky je legacy fallback). 305/305 .NET, build čistý. Živě ověřeno (reset / okraje bez wipe / Storno).

## 2026-07-09 - Zastávkový JŘ: symbolový font značek (Timetable, locale-aware) + katalog

Speciální symboly (bezbariér, na znamení…) se tiskly jako holé písmeno „H"/„x" místo piktogramu -
chyběl symbolový JŘ font (dlouho odložený bod „C2"). Odblokováno dodáním fontu.

- **Embedded `TimeTable2.ttf` (CZ) + `TimeTable2SK.ttf` (SK)** do `Fonts/` (M-line vlastní EDISON font;
  csproj glob je zabalí). Rodiny `Timetable_nove` / `TimeTable_nove_SK`.
- **`FontResolver.SymbolFontFamily(culture)`** - locale→font dle `Template.CultureCode` (`sk-*` → SK),
  1:1 s legacy `formTISKJRReportDotNet_Module` (ř. 1197).
- **Symbolový font pro jednoznakové `SpecialniZnak`** v legendě (NotesLegend) i u minut (DepartureGrid
  `anotGlyphFont`) - `H`/`F` → piktogram (vozíček). Uživatelský `AnotaceSymbolStyle` font má přednost.
- **Na znamení `x`/`y`/`z` = prostý text**, ne piktogram (helper `SymbolJeText`, dle konvence) - jinak
  font na pozici `x` kreslí vláček. Bezbariér zůstává piktogram.
- **NZ** (negativní) beze změny - ruční inverzní box (už bylo správně).
- **Katalog symbolů** (`.claude/docs/elementy-architektura.md`) - převzatá mapa z legacy `GSId` (66 symbolů:
  id·znak·význam·render-mód) + zdroje, jako reference pro další dotažení (text-multichar, kompozity).
- Čistě render vrstva → gold netknuté, 305/305 .NET. Živě ověřen render (vozíček, na-znamení x, SK přepnutí).

**Známý zbytek (samostatný Caché track):** pozitivní datová omezení („jede jen/také") se dnes slepují
s negativními do jedné NZ značky (box) - ve starém jsou pozitivní **kroužkovaná**. Fix = rozdělit
pozitivní/negativní značky + kroužek v renderu + gold re-baseline. Plus kroužkové dny v týdnu (emise dat).

## 2026-07-09 - Zastávkový JŘ: konzistentní řazení legendy poznámek dle typu (nový default)

Legenda poznámek se dřív vypisovala v pořadí, v jakém ji stavěla agenda („náhodně"; legacy stejně).
Helpdesk námět kolegy: seřadit konzistentně. Element už měl hook `LegendRazeni`, tak přidán mód:

- **`LegendRazeni.DleTypu`** - stabilní priorita `NegativniZnacka → SpecialniZnak → Text`
  (= značky → atributy → text). Uvnitř tieru zachová pořadí z dat.
- **Nový default** elementu (`Razeni = DleTypu`, dřív `Vychozi`). „Výchozí (dle dat)" zůstává jako opt-out.
- Dropdown „Řazení" rozšířen; sloupce / směr plnění beze změny - typ jen řídí lineární pořadí,
  které se pak zalomí do sloupců (seřaď → naplň, stávající architektura).
- Čistě .NET (render vrstva, „šablona říká JAK") → žádný Caché/gold dopad. Používá `PoznamkaData.Typ`
  (už v POCO). 305/305 .NET. Additivní enum + default → bez migrace (staré explicitní `Vychozi` se ctí).

## 2026-07-09 - Zastávkový JŘ (AD): dotažení odsazení názvu zastávky z ^JRL

Element odsazení názvu zastávky uměl vždy (`z.Odsazeni` → N mezer přes `ShowStopIndentation`), ale AD
posílalo natvrdo `ODSAZENI=0`. Dotaženo z legacy dat:

- **Zdroj:** `^JRL(sku,lin,"J",poradi)` - **5. `#`-segment** = počet znaků odsazení (legacy `mkPLinWin`
  „odrazeni nazvu zastavky" = `gTrsLin dta 7`, ověřeno v `JRapiLin.mac` parseru + `JRapiLinPrtWin.mac`).
- **Fix (1 řádek):** `ZastavkoveJrAd` stop-loop čte `s odsazeni = +$p(veta,"#",5)` a předá do `BuildZastavka`
  místo hardcoded `0`. C# strana beze změny (parse + render byly připravené).
- Behavior-preserving pro linky bez odsazení (0 = beze změny) → **gold netknuté** (33/33 TD zelené).
  Živě ověřen render odsazených podzastávek.

## 2026-07-09 - Zastávkový JŘ: revize kontraktu SKUPINY_DNU (schéma) - trojice+1 uzavřena

Čtvrtá (poslední) řádková kolekce zastávkového JŘ. Wire konzistentní (AD i MHD `ID, NADPIS, NADPIS2,
PORADI`), ale schéma se rozcházelo: AD `ID, NADPIS, PORADI` (bez NADPIS2), MHD `ID, NADPIS, NADPIS2`
(bez PORADI), obě navíc vystavovaly `ID` (join/pivot klíč).

- **Schéma SKUPINY_DNU sjednoceno na `NADPIS, NADPIS2, PORADI`** (AD i MHD). `ID` skryto (zůstává ve
  wire jako pivot klíč, konzistentní se skrytým ZASTAVKY.ID / ODJEZDY join klíči). **Jen GetSchema →
  gold netknuté** (33/33 TD).

Tím je zrevidovaný **kompletní datový kontrakt zastávkového JŘ**: skalární pole + všechny 4 řádkové
kolekce (ZASTAVKY, SKUPINY_DNU, ODJEZDY, POZNAMKY). Princip všude: honest flat kontrakt (jen
user-facing pole), join/pivot klíče + prezentační nápovědy zůstávají ve wire ale nejsou sloupce,
AD ≡ MHD, mrtvá pole pryč.

## 2026-07-09 - Zastávkový JŘ: revize kontraktu POZNAMKY (schéma) + zaflagovaná MHD KATEGORIE mezera

Poslední z trojice (ZASTAVKY/ODJEZDY/POZNAMKY). Názvy čitelné, žádné mrtvé pole ve wire (vše se
konzumuje: TYP/ZNAK/TEXT render, FORE_COLOR barví znak, KATEGORIE čte filtr viditelnosti, BACK_COLOR
rezervováno). Řešena jen schéma-nekonzistence:

- **Ploché schéma POZNAMKY sjednoceno na `TYP, ZNAK, TEXT`** - AD dřív vystavovalo 6 (vč. interní
  `KATEGORIE` + hintů `FORE/BACK_COLOR`), MHD 3. AD strimováno na 3 (MHD už mělo). `KATEGORIE`
  (filtr-tag, jako join klíč) + barevné hinty zůstávají ve wire, jen se nenabízejí jako sloupce.
  **Jen GetSchema → gold netknuté** (33/33 TD zelené beze změny digestů).

**Zaflagováno (samostatný ticket, ne úklid):** MHD poznámky nenesou `KATEGORIE` → kategorie-logika
`NoteVisibilityFilter` (#7161 D5 „× na znamení", D6 agregát bezbariér, rozsahové skrytí) je fakticky
**jen AD**. Fix není triviální tag: AD má atributovou legendu jen „na znamení", MHD emituje všechny
atributy (`ZastavkoveJrMhd` ř. 1219) → nutná doménová klasifikace + gold re-baseline + test filtru.

## 2026-07-09 - Zastávkový JŘ: revize kontraktu ODJEZDY (mrtvé pole + schéma)

Analogická revize jako u ZASTAVKY, teď pro ODJEZDY (zdroj DepartureGridu). ODJEZDY je čistě
**pivot-zdroj** (sloupce mřížky = skupiny dnů, ne pole) → žádné „sloupec = field" sjednocení jako
u StopListu; AD ≡ MHD navíc už bylo symetrické a názvy čitelné. Řešily se jen mrtvé/nekonzistentní pole:

- **`ZKRACENE_OZNACENI_LINKY` odstraněno** - v obou agendách vždy `""` (call sites posílaly jen 4 argy) a .NET ho nikde nerenderoval (doubly-dead, analog `TarifniCislo`). Pryč z wire (`BuildOdjezd` + param), schématu i POCO `OdjezdData.ZkraceneOznaceniLinky`.
- **Ploché schéma ODJEZDY sjednoceno na `HODINA, MINUTA`** (AD i MHD). Skryté ze schématu (zůstávají ve wire): `ZASTAVKA_ID`/`SKUPINA_DNU_ID` (join/pivot klíče - konzistentní se skrytým `ZASTAVKY.ID`) + `FORE_COLOR`/`BACK_COLOR` (prezentační nápovědy, čte je jen render, hex nedává smysl jako sloupec). Dřív AD vystavovalo 7 polí, MHD 4 → teď obě 2.
- **Ponecháno vědomě:** odjezdové `FORE_COLOR`/`BACK_COLOR` (R3 hint, renderer je čte - dormant, ne dead) a anotační `SPOJ` (AD plní reálné ID spoje z DB - rezerva pro budoucí per-spoj filtr, ne hardcoded test).
- ⚠️ Gold re-baseline (ZKRACENE odebrání změnilo `GetData` - jen `md5`, počty řádků beze změny). Ověřeno na serveru: **48/48** Caché, 305/305 .NET.

## 2026-07-09 - Zastávkový JŘ: gold re-baseline po sjednocení kontraktu

Přegenerování zmrazených digestů po záměrné změně `GetData` (fáze 1 + úklid):

- **Gold digesty přepsány** (AD 0365001/0365005, MHD 1001/1003/1006) - změnil se **jen `md5`**, počty řádků (`pages/zast/odj/pozn`) **beze změny** → potvrzení, že šlo čistě o přejmenování klíčů + zmizení `TC`/`ZAKLADNI_TRASA`/`PRUBEHY`, ne o datovou regresi.
- **`MhdLegacyOracle`** čte název zastávky z našeho výstupu nově přes `NAZEV_ZASTAVKY` (byl `POPIS`) → legacy parita zpět 1:1.
- Ověřeno na živém serveru: **48/48** Caché testů zelených (vč. 33 TD: gold + parita).

## 2026-07-09 - Zastávkový JŘ: úklid mrtvých polí zastávek (wire + .NET)

Dočištění po sjednocení kontraktu - odstranění write-only mrtvých polí, která se nikde nečetla:

- **`ZAKLADNI_TRASA`** (bool „na základní trase") - v `BuildZastavka` napevno `"true"` u každé zastávky → nulová informace; skutečná logika žije v `ZakladniCisloTrasy` (řídí variantní časové sloupce). Emit + POCO `z.ZakladniTrasa` pryč (AD+MHD).
- **`PRUBEHY`** (písmena rozdílů tras u zastávky) - v ostrém `GetData` **nikdy** neplněné (jen MHD mock); účel pokrývají anotace rozdílů tras v DepartureGridu + multi-variantní časové sloupce. Emit + `AddPrubeh` + POCO `z.Prubehy` pryč (AD+MHD).
- **`TarifniCislo`** POCO - osiřelé po odebrání TČ (fáze 1), pryč.
- ⚠️ Mění `GetData` výstup (2 klíče pryč) → součást téže gold re-baseline jako fáze 1/2. Testy 305/305, Caché kompiluje čistě.

## 2026-07-09 - Zastávkový JŘ: StopList sloupce field-based 1:1 s DynamicTable (sjednocení kontraktu, fáze 2/2)

Dokončení sjednocení - **identita sloupce StopListu = field ID kolekce ZASTAVKY**, takže je 1:1 s poli, která nabízí DynamicTable nad stejným zdrojem. Uživatel už nemusí luštit, proč jedna cesta zná `POPIS` a druhá `NAZEV_ZASTAVKY`.

- **`StopFields`** (`Models/StopFields.cs`) - nový jednotný field-ID kontrakt (`NAZEV_ZASTAVKY, CAS_JIZDY, PORADI, AKTUALNI_ZASTAVKA, ZONY` + interní `ID`, variant prefix `CAS_JIZDY#`). Sdílený mezi StopList render (`GetCellText`/anchory/varianty), `.NET` parse (`BuildZastavky`) a PDF import buildery.
- **StopList sloupce překlíčované** ze směrovacích kódů (`StopName`/`TravelTime`/…) na field ID. `GetDefaultColumns`, `GetCellText`, editor `FriendlyName` → `StopFields`. **Mrtvé sloupce `RouteLetters`/`BaseRoute` (průběhy/ZT) odstraněny** (uživatel je označil jako mrtvé).
- **`ColumnKeys` zúženo na linkový JŘ** - obsahuje už jen klíče `LineTableElement` (`StopName/Tc/Zone/Km/Mhd/Trip`). Zastávkový a linkový JŘ mají různý datový tvar → kontrakt nesdílí (vědomě).
- **Migrace `1.2.0 → 1.3.0`** (`MigrationUnifyStopColumns`): StopList `columns[].key` staré→field ID + drop `Tc`/`RouteLetters`/`BaseRoute`; DynamicTable/Table `columns[].field` `POPIS`→`NAZEV_ZASTAVKY`, `JE_AKTUALNI`→`AKTUALNI_ZASTAVKA`; placeholder tokeny ve všech textech/výrazech (`{POPIS}`→`{NAZEV_ZASTAVKY}`, `{JE_AKTUALNI}`→`{AKTUALNI_ZASTAVKA}`, `{TC}` pryč) - hranice tokenu chrání skalární `{POPIS_DOPRAVCE}`.
- Testy 305/305 (+6: `StopFields` lock, StopList default = field ID, 5× migrace vč. idempotence a ochrany hranice tokenu).
- **Specials zůstávají StopList-only** (šipka směru, inline značky/na-znamení, zvýraznění aktuální, rozvinutí variant tras) - nejsou součástí sdíleného sloupcového kontraktu, jak bylo zadáno.

## 2026-07-09 - Zastávkový JŘ: čitelná ID polí + TČ pryč všude (sjednocení kontraktu, fáze 1/2)

Sjednocení datového/sloupcového kontraktu ZASTAVKY mezi StopList elementem a DynamicTable - **čistá varianta A**: přejmenování drátových klíčů všude tak, aby ID odpovídalo labelu (`SLOVO1_SLOVO2`), ne kryptickému Caché klíči. Cíl: uživatel v podmíněném renderu / počítaných polích píše `{NAZEV_ZASTAVKY}`, ne `{POPIS}`.

- **Přejmenování klíčů (AD+MHD, wire + schéma + .NET parse):** `POPIS → NAZEV_ZASTAVKY`, `JE_AKTUALNI → AKTUALNI_ZASTAVKA`. `BuildZastavka` emituje nové klíče, `GetSchema` je vystavuje, `StopScheduleElementBase.BuildZastavky` je čte.
- **`TC` odebráno úplně (AD i MHD)** - v AD nesl jen J-klíč (= interní `ID`), v MHD `ZST.Cislo` (interní CIS číslo, pletlo by se uživateli). Poziční potřebu pokrývá `PORADI`. StopList sloupec „TČ" odebrán (`ColumnKeys.Tc` konstanta zůstává - sdílí LineTable).
- **Finální plochý kontrakt (AD ≡ MHD, identický):** `NAZEV_ZASTAVKY, CAS_JIZDY, PORADI, AKTUALNI_ZASTAVKA, ZONY`. `ID` zůstává v datech jako interní join-klíč (`ODJEZDY.ZASTAVKA_ID`), ne ve schématu.
- ⚠️ **Gold digesty se mění** (změněn `GetData` výstup - klíče přejmenované, TČ pryč) → nutná re-baseline na živém serveru (`ZastavkoveJrAdGoldTest`/`ZastavkoveJrMhdGoldTest`). .NET testy 299/299 zelené.
- **Zbývá (fáze 2):** StopList sloupce plně field-based (identita sloupce = field ID, jako DynamicTable) + oba editory čerpají dostupné sloupce ze schématu + migrace uložených šablon (`{POPIS}→{NAZEV_ZASTAVKY}` atd.).

## 2026-07-09 - Zastávkový JŘ: AD TČ odebráno + ZONY vystaveno (plné 1:1, gold-safe)

Dokončení 1:1 ZASTAVKY mezi StopList a DynamicTable.

- **AD `TC` odebráno ze schématu** - v AD `TC` nese jen J-klíč (= interní `ID`), ne reálné tarifní
  číslo (`PORADI` = zobrazovací čítač je to smysluplné). MHD `TC` (skutečné číslo zastávky) zůstává →
  schéma se AD↔MHD vědomě rozchází podle reálného významu dat.
- **`ZONY` vystaveno ve schématu** (AD+MHD, „Zóny") - bez změny `GetData` (**gold digesty netknuté**):
  `ZONY` zůstává v datech vnořené pole, ale `DataContext.GetString` skalární pole **spojí „/"** (např.
  „1/2"), takže ho zobrazí i plochá buňka DynamicTable. StopList beze změny (parsuje si pole sám).
  Objektová pole (ATRIBUTY {ZNAK,POPIS}) join přeskočí → do schématu se nevystavují.
- **Výsledek:** ploché pole ZASTAVKY jsou teď **1:1** - StopList i DynamicTable nabízí `POPIS, CAS_JIZDY,
  PORADI, JE_AKTUALNI, ZONY` (+ MHD `TC`). Jediný doménový rozdíl = StopListové značky/šipka/aktuální
  (inline render, ne sloupce) - inherentní specializovanému elementu.
- +4 testy `DataContextTests` (GetString: skalár / fallback / skalární array join / objektový array skip).

## 2026-07-09 - Zastávkový JŘ: pole ZASTAVKY co nejvíc 1:1 mezi StopList a DynamicTable

Pokračování „jeden zdroj = schéma ZASTAVKY". Cíl: uživatele nemate, že jedna cesta (StopList element)
nabízí jiná pole než druhá (DynamicTable nad zdrojem ZASTAVKY).

- **Schéma AD↔MHD sjednoceno** - obě agendy vystavují stejnou plochou sadu: `POPIS, TC, CAS_JIZDY,
  PORADI, JE_AKTUALNI`. MHD doplněno o `PORADI`+`JE_AKTUALNI` (data je emitují). `ID` odebráno z obou
  (interní join klíč `ODJEZDY.ZASTAVKA_ID`→`ZASTAVKY.ID`, uživatele mátl; data se emitovat nechají).
- **StopList doplněn o skryté sloupce `Pořadí` + `Aktuální zastávka`** (`ColumnKeys.Poradi`/`JeAktualni`,
  default `Visible=false`) - data v `ZastavkaData` už byla. StopList tak nabízí stejná plochá pole jako
  DynamicTable (+ navíc `Zóna`).
- **Výsledek:** ploché sloupce/pole jsou 1:1 (StopList i DynamicTable: POPIS/TC/CAS_JIZDY/PORADI/JE_AKTUALNI).
  Jediný zbylý rozdíl = **`Zóna` (vnořené `ZONY`)** má jen StopList - plochá DynamicTable vnořené pole neumí.
  Úplné sjednocení `ZONY` = zploštit na string (koordinovaná Caché+.NET změna + re-baseline gold digestů),
  vědomě odloženo (nízké ROI - DynamicTable nad ZASTAVKY je fallback, ostrý nástroj je StopList).

## 2026-07-09 - Zastávkový JŘ: jeden zdroj pravdy = schéma ZASTAVKY (úklid mrtvých polí)

Odstranění mrtvých / hardcoded polí, ať schéma ZASTAVKY = jediný zdroj pravdy sdílený DynamicTable
(nabídka sloupců) i StopList/výrazy. Dřív matoucí: DynamicTable nad zdrojem ZASTAVKY nabízel pole,
která vždy vracela konstantu (uživatel jimi nemohl nic rozumného udělat).

- **Schéma agendy (GetSchema, ZASTAVKY):** odebrána mrtvá pole - AD: `NA_ZNAMENI` (natvrdo 0),
  `ODSAZENI` (0), `ZAKLADNI_TRASA` (true); MHD: `NA_ZNAMENI`. Živá zůstala: `ID, POPIS, TC, CAS_JIZDY,
  PORADI, JE_AKTUALNI` (AD) / `ID, POPIS, TC, CAS_JIZDY` (MHD). DynamicTable je tak už nenabízí.
- **StopListElement:** z výchozích sloupců vyhozeny mrtvé sloty `Trasa` (`RouteLetters` → `PRUBEHY`,
  v živých datech prázdné) + `ZT` (`BaseRoute` → `ZAKLADNI_TRASA`, vždy „1"). Zbyly živé: Čas, TČ,
  Zastávka, Zóna. (Klíče v `ColumnKeys` ponechány kvůli zpětné kompat. uložených šablon.)
- **StopList helptext viditelnosti řádku:** vypisoval mrtvá pole a měl nefunkční příklad `{NA_ZNAMENI}==1`
  → nově živá pole (`POPIS, TC, CAS_JIZDY, PORADI, JE_AKTUALNI`) + příklad `{JE_AKTUALNI} == 1`.
- **Pozn.:** pro výrazy (highlight/viditelnost) je zdroj polí už sdílený (StopList i DynamicTable čtou
  celý řádek dat). Doplnění živých plochých polí (`PORADI`/`JE_AKTUALNI`) jako *sloupců* i do StopListu
  (default skryté) je možný navazující krok - dnes jsou dostupné ve výrazech, ne jako sloupce.

## 2026-07-09 - Zastávkový JŘ: poctivější labely skalárních polí agendy (per-agenda)

Validace „název pole ↔ co reálně vrací z Caché" (levý panel „Pole agendy"). Jen **labely** (zobrazení),
klíče beze změny → **nerozbíjí datový kontrakt** (R3). Labely smí divergovat AD↔MHD, ať každý vidí svou
doménovou terminologii.

- **AD `NEJEDOU_SPOJE`** „Nejedou spoje" → **„Nástupiště"** - v AD to pole reálně nese nástupiště vývěskové
  zastávky (zdokumentovaný legacy misnomer), ne „nejedou spoje". (MHD label ponechán - tam skutečně nese
  text „nejedou spoje".)
- **MHD `CISLO_LINKY`** „Číslo linky" → **„Číslo linky (dle licence)"** + **`LOKALNI_OZNACENI_LINKY`**
  „Označení linky" → **„Veřejné označení linky"** - v MHD je `CISLO_LINKY` = `CisloLinkyDleLicence`
  (licenční), veřejné číslo je v `LOKALNI_OZNACENI_LINKY` (`CisloVerejne`); labely to teď rozlišují.
  (AD ponecháno - `CISLO_LINKY` = `lin` z `^JRL` je tam prostě číslo linky, nemá licenční/veřejný rozštěp.)
- **Pozn.:** stejný `CISLO_LINKY` label mají i linkové agendy (`LinkovyJr*`) - sjednotit až při jejich
  review (jiná agenda + aktivně se na ní pracuje). Klíč `NEJEDOU_SPOJE` zůstává (rename = breaking) -
  label-key nesoulad v AD je vědomý; čistý fix (samostatný `NASTUPISTE` klíč, aditivní) je možný pozdější krok.

## 2026-07-08 - Víceřádkové záhlaví buňky přes `\n` (dvojjazyčné záhlaví sloupce)

Buňka/záhlaví tabulky může mít víc řádků - typicky dvouřádkové (CZ/EN) záhlaví sloupce.
Dřív šlo jen u DepartureGrid (datově z `Nadpis`/`Nadpis2`), autorské záhlaví sloupce ne.

- **`CellRenderer.DrawCell`** - multi-line větev: rozdělí text (`CellLayout.SplitLines`), blok N řádků,
  VAlign umístí blok, HAlign zarovná každý řádek zvlášť + decorations. Jednořádkový = rychlá cesta.
  **Jedno místo → všechny tabulkové elementy** (StopList, TableElement, LineTable, DepartureGrid) i header/data buňky.
- **Syntaxe:** literální token `\n` (autorská zkratka, `CellLayout` ho normalizuje na skutečný zlom)
  **nebo** Enter v poli (`AcceptsReturn` na poli „Záhlaví" v editoru sloupců zastávek + hint).
- **`StopListElement`** - výška záhlaví line-aware (`× MaxHeaderLines()`) v Render i CalculateHeightFromData (parita).
- TextElement `\n` už uměl (`TextWrapHelper`) - beze změny. TableElement pevná `HeaderHeight` (multi-line se vykreslí, výšku řídí uživatel); DepartureGrid/LineTable datově řízená záhlaví - bez změny výšky.
- Bez změny JSON schématu / migrace (`Header` je string s `\n`), zpětně kompatibilní. +9 testů `CellLayoutTests`, 295/295.
- Dokumentace: uživatelská příručka (obecná §8 + zastávkový JŘ §4).

## 2026-07-08 - Tabulkové jádro: levné švy před v1 (z analýzy architektury)

- **Krok 2 - extrakce středního švu (row-feature).** Po #1 už byla highlight logika sdílená přes
  `RowConditions` + `HighlightRules`; zbývala duplikovaná visibility trojice. Vytaženo: sdílený filtr
  `RowConditions.VisibleRows` (generický `<T>` přes `rowOf` selektor + `DataContext` overload, bez
  alokace při prázdném výrazu) - používá DynamicTable i LineTable; sdílený descriptor
  `RowConditionDescriptors.RowVisibility(get, set)` - nahradil 3× kopii 6řádkového `StringPropertyDescriptor`.
  **Resolve-wrappery (3a) vědomě NEextrahovány** - sdílené jádro je už `TableLayout.ResolveCascade`; per-element
  metody jsou jen tenké adaptéry na různé typy sloupců (TableColumn vs StopColumn) a baked parenty, genericky
  by to stálo víc než ušetří. **Pětice levných švů uzavřena** (#1,#3,#4,#5 + #2). +3 testy (VisibleRows).

- **Krok 1 (jádro) - řádkové zvýraznění: 1 pravidlo → seznam `HighlightRule[]`.** Model `HighlightRule`
  `{ Condition, BackgroundColor, TargetColumn? }`, sdílený DynamicTable/StopList/LineTable jako
  `List<HighlightRule>?` (nahradil skalární `RowHighlightExpression`/`RowHighlightColor`). Sémantika
  **první splněné pravidlo vyhrává**; `RowConditions.HighlightColor(rules, row)`. `TargetColumn`
  (per-sloupec) je v modelu rezervovaný, render zatím barví celý řádek (per-sloupec render = #2).
  **Breaking JSON → migrátor** (`MigrationHighlightRules`, 1.0.0 → 1.1.0): staré pole každého elementu
  se překlopí na `highlightRules:[{…}]` a odstraní; `TemplateMigrator.CurrentVersion` bump na `1.1.0`.
  `TemplateSerializer` nově razítkuje verzi při uložení (uložení = zápis aktuálního formátu). Odstraněn
  mrtvý rezervovaný `ColorRule` (nahrazen `HighlightRule`). Kryto testy (RowConditions, migrátor,
  round-trip, Clone completeness).
- **Krok 1 (editor) - `HighlightRulesEditorDialog` (varianta 3).** Custom dialog (DataGrid Add/Remove/▲▼,
  podmínka + swatch barvy, barva pozadí vybraného pravidla přes color picker) - konzistentní se vzorem
  editoru sloupců. Launch přes `FieldTypeButton "__highlightRules"` v `*Edit.cls` (EDInputForm), zapojený
  na **dvou plochách**: inline panel „Vlastnosti" (`InlinePropertiesForm.ButtonClicked` passthrough →
  `MainWindow.OpenHighlightRulesDialog`) i tab „Obecné" columns dialogů (StopColumns/DynamicTable/LineTable
  `GeneralForm.ButtonClicked` → `RequestApplyHighlight`). Apply přes `ApplyHighlightRulesEdit` (undoable
  `SetPropertyCommand`, prázdný seznam → null). Úzké rozhraní `IRowHighlightable` drží dialog + apply
  generické nad 3 elementy. `__`-prefix klíče drift test přeskakuje. **#1 hotové.**
- **Krok 1 (review fixy).** Z adversariálního review commitů #1: **(H1)** `TemplateSerializer.DeserializeSection`
  protažen migrátorem (obalí sekci do mini-šablony 1.0.0) - cesta šablon sekcí byla trvale migrátor-slepá,
  tiše by ztrácela data při každé budoucí breaking migraci; **(M2)** highlight z columns dialogů (StopColumns/
  DynamicTable/LineTable) se aplikuje až na OK dialogu (deferred `ResultHighlightRules`/`HighlightEdited`),
  ne okamžitě - Storno teď změnu zahodí, konzistentní se vzorem `style:` (inline panel zůstává okamžitý,
  tam je to správně); **(L5)** migrace nevytvoří bezbarevné pravidlo (staré zvýraznění vyžadovalo podmínku
  i barvu); **button** dostal `f.value = "Upravit pravidla…"` (jinak prázdný/úzký - `FieldTypeButton` renderuje
  `Value`, ne `Label`). Kryto 2 novými testy (DeserializeSection migrace, bezbarevné pravidlo).


Navazuje na analýzu připravenosti tabulkového jádra (4 agentní dimenze). Postupně se zavírají levné
seamy, které jinak platí daň při každém dalším tabulkovém elementu / featuře.

- **Krok 3 - reflexní test úplnosti `Clone()` (`CloneCompletenessTests`).** Enumeruje **všechny**
  konkrétní `TemplateElement` potomky přes `[JsonDerivedType]`, každou perzistentní property zmutuje na
  ne-default a porovná `serialize(original)` vs `serialize(clone)` (mimo přegenerované `Id`). Jeden test
  pokryje i budoucí elementy + meta-test hlídá, že enumerace = přesně konkrétní potomci.
  **Chytil reálný bug:** `CopyScheduleBaseProperties` nekopírovala `DataRowHeight` → při `Clone()`
  (Ctrl+D / undo) se ztrácela u `LineTableElement` a `NotesLegendElement` (StopList/DepartureGrid ji měly
  vypsanou explicitně, proto neselhaly). Fix: doplněna do base helperu, redundantní explicitní výpisy ve
  StopList/DepartureGrid odstraněny (base vlastní base property).
- **Krok 5 - `SkiaHeightCalculator` deleguje výšku `TableElement` na element.** Kalkulátor už
  nemá inline `CalculateTableElementHeight` (znal interní strukturu tabulky) - nově `TableElement`
  vystavuje `CalculateHeightFromData(data)` jako ostatní tabulkové elementy a kalkulátor jen deleguje.
  Behavior-preserving (logika 1:1, jen přesunutá; magic „3 ukázkové řádky" → existující `DesignerPreviewRows`).
  Nový tabulkový element už nepřidává větev do switche kalkulátoru.
- **Krok 4 - agregace nad výrazem (uživatelská featura).** Souhrnné řádky tabulky uměly jen
  `SUM({POLE})` (holé pole); nově `SUM({CENA}*{POCET})` - argument je **výraz** vyhodnocený per řádek
  přes `ExpressionEvaluator`, takže lze sečíst i počítaný sloupec (dřív šel počítaný sloupec vytvořit,
  ale ne agregovat). Zpětně kompatibilní (holé pole beze změny), aditivní. Omezení: bez vnořených
  závorek v argumentu. Zaevidováno v CLAUDE.md (výrazový jazyk).

## 2026-07-07 - Úklid z auditu v1 (P-C: R4 dluh + drobnosti)

Jasné (bezrozhodovací) hygienické body z auditu. Build 0 chyb, test projekt 0 chyb.

- **C-a - dokončení H3 (R4): 2 zapomenuté ViewModely bez WPF.** `ElementPropertiesWindowViewModel`
  už nekonstruuje `TableEditorDialog` (přes `Application.Current.MainWindow`) ani `OpenFileDialog` -
  nově callbacky `RequestOpenTableEditorDialog` + `RequestPickFile`, View je drátuje (MainWindow
  `OpenTableEditorDialog` + `_vm.ApplyTableEdit`, stejný vzor jako ostatní tabulkové editory). Odstraněn
  `using System.Windows` + nepoužitý `_tableTemplateProvider` (code-behind bere `_vm.TableTemplateProvider`).
  `CacheTemplatePickerDialogViewModel` už nekonstruuje `SaveSectionTemplateDialog` pro rename - callback
  `RequestRenameInput`.
- **C-c** - `CacheAgendaProvider` doplněn explicitní komentář-guard: metody jsou záměrně sync, pomalé
  operace (`GetData`, `GetAvailableInstances`) musí volající obalit `Task.Run` (R1).
- **C-d** - dangling odkaz na `tools/compare-gold.ps1` v smoke testu → odkaz na gold testy. Mrtvý stub
  `TemplateDesigner.Tests/Layout/RenderR2ToPngTest.cs` (untracked) k ručnímu smazání.
- **C-f** - `new-element-checklist.md` opraven odkaz `MainWindow/MainWindowViewModel.cs` → `TemplateDesignerViewModel.cs`.
- **Zbývá (vyžaduje rozhodnutí):** C-b (drift test PropertyDescriptor↔`*Edit.cls`), C-e (`agendaCode` „MHD" alias/migrace).

## 2026-07-07 - Gold regresní testy zastávkového JŘ (P-A z auditu v1)

Datová parita zastávkového JŘ nově krytá zmrazenými gold digest testy - pojistka „nerozbili jsme
vlastní výstup GetData". (Legacy-oracle parita = jiná věc, zůstává otevřená C1/C9.)

- **`ZastavkoveJrMhdGoldTest`** (nový) - MHD engine (dřív **0** gold testů) pokryt na CB_DP linkách
  1001/1003/1006. MHD data jsou multi-tenant (`^[dataDB]ALVA.MHD.LSO(IdFa,...)`) → gold přibitý na
  IdFa×NS CB_DP; pre-push runner jede právě pod CB_DP → gold reálně běží. Digesty vygenerovány z živých
  dat, determinismus ověřen (2× identický md5).
- **`GoldUtil.Digest()`** (nový, sdílený) - jedno místo výpočtu digestu (pages/zast/odj/pozn/md5) pro
  AD i MHD → nemohou divergovat (anti-drift, roadmap C8). AD gold přepojen z vlastní kopie `GoldDigest`.
- **AD gold zpřísněn** - dřív `digest=""` → `AssertTrue(1)` (tichá zelená i když gold linka zmizí).
  Nově: namespace chybí → legitimní skip; namespace **je** ale linka chybí → **FAIL** (rozbitý fixture).
- Úklid: dangling odkaz na neexistující `tools/compare-gold.ps1` v smoke testu → odkaz na gold testy;
  mrtvý stub `TemplateDesigner.Tests/Layout/RenderR2ToPngTest.cs` (untracked) k smazání.
- Ověřeno: `ALVA.TemplateDesigner.Tests` 30/30 green (vč. AD gold po refactoru - byte-identický).

**C1 - MHD 1:1 vůči legacy oracle ✅ HOTOVO (posun z „regrese" na „legacy parita", vč. pre-push gate).**
`ALVA.TemplateDesigner.Tests.MhdLegacyOracle` volá **skutečný legacy producent**
`ALVA.MHD.LinkyPrintFormatMHD.MakeData` (obchází padající formátovač `TISKMHD.Data.LoadData`) a **pozičně**
porovnává proti našemu `GetData`. `ZastavkoveJrMhdLegacyParityTest` = pre-push gate (skip jen když legacy
třída/namespace na serveru chybí).

- **CB_DP 1001/1003/1006: odjezdy + zastávky + poznámky 1:1** (poziční, 0 rozdílů). `ALVA.TemplateDesigner.Tests` 33/33.
- **Poziční klíče:** odjezd = zastávka × skupina-dnů (nadpis) × HH:MM; zastávka = název; poznámka = typ × znak × text.
  Ne line-global multiset - pins každý odjezd na správnou buňku.
- **Kontrakt `MakeData in`** minimální: `in("par",1)`=TiskProjetychZast, zbytek volitelný. „Neúplné `in()`"
  bylo o `LoadData`, ne producentovi.
- **Zjištění 1:** náš engine = legacy s **TiskProjetychZast=1** (tiskne projeté zastávky). `projete=0` legacy
  filtruje (832 vs 1543 na 1001) - náš engine ten toggle nemá (vazba na helpdesk A2-A6 #7959 „filtr neobsluhovaných").
- **Zjištění 2:** legacy ukládá speciální znaky jako surový ASCII kód (120/123), konvertuje až při renderu;
  náš engine v datech. Sjednoceno přes `ConvertSpecialniZnak` → shoda finálního glyfu zároveň ověřuje, že
  náš převod = legacy převod.
- **Zbývá (nice-to-have):** víc linek; AD analog (legacy oracle `$$WinTiskMhd^JRapi`, VolanoZ=JRMAIN).

## 2026-07-07 - Výběr skupiny linek v Instance Pickeru (AD + MHD)

InstancePicker dřív bral jen **default** skupinu (první nezablokovaná) → uživatel neviděl linky
z ostatních skupin (u MHD default často prázdná „pokusná"). Nově **searchable filtr skupin**.

- **Caché `GetAvailableSkupiny`** (nová metoda, vzor GetAvailableInstances): AD ze subscriptů `^JRL(sku)`,
  název = 1. `~`-piece hodnoty (např. „Vertikála k 1.1. - ŘÁDNÉ"); MHD z `ENUMS "SkupinyLinek"` (název
  `Nazev`, mimo `Blocked`); BaseAgenda default prázdné → agendy bez skupin (GENERAL/faktury) filtr skryjí.
- **`EDList.Instance`** (`LoadList`/`FetchInstances`) čte `filter","SKUPINA_LINEK_ID"` a předá do
  GetAvailableInstances - stejný rozšiřovací bod jako stávající `agendaCode` (žádný nový wire mechanismus).
- **.NET:** `AgendaSkupina(Value, Label)` POCO + `Display` = „číslo - název" (search najde podle ID i názvu,
  client-side); `CacheAgendaProvider.GetAvailableSkupiny`; Instance Picker dostal **searchable combobox**
  (filtr přes `ICollectionView`, ne Items.Clear → SelectedItem přežije) → výběr přenačte EDList s filtrem.
- **Propagace zvolené skupiny do GetData** (preview i export/tisk) - u AD je linka klíčovaná (skupina, linka),
  jinak by se fetchla z jiné skupiny. Status chip v hlavním okně „Skupina: číslo - název".
- **Vědomě odloženo (ne blocker):** skupiny pro knižní JŘ agendy (Fáze C+D); persistence zvolené skupiny
  per šablona (dnes session scope - `TryLoadInstanceDefaults` obnovuje z default skupiny).
- Build + 253/253 testů. Ověřeno na KV_DP (AD i MHD na jedné firmě).

## 2026-07-07 - Audit H3: ViewModel bez WPF (R4) - dialogy přes `IDesignerDialogService`

Poslední otevřený vysoko-prioritní audit bod. `TemplateDesignerViewModel` přímo instancoval WPF okna
(`new *Dialog(...) { Owner = Application.Current.MainWindow }` + `.ShowDialog()`), souborové dialogy
(`OpenFileDialog`/`SaveFileDialog`) a systémovou `Clipboard` → porušení R4, VM neunit-testovatelný.

- **Nový `Services/IDesignerDialogService`** - abstrakce View vrstvy: `ShowInstancePicker`/`ShowChangeAgenda`/
  `ShowCacheTemplatePicker`/`ShowSaveToCache` (nad dialogovým VM) + `PickOpenFile`/`PickSaveFile` + `SetClipboardText`.
- **`WpfDesignerDialogService`** (code-behind, drží owner okno) mapuje na konkrétní okna; napojeno v `MainWindow`.
- VM staví dialogové **ViewModely** (VM→VM, žádné WPF) a jen je nechá zobrazit; výsledek čte z dialogového VM.
- `using System.Windows` z VM pryč (zůstal jen `System.Windows.Input` kvůli `Key` enum v `MoveByKey`).
- VM je nyní testovatelný přes mock služby. Build + 253/253 testů. Detail → [`roadmap-archive.md`](roadmap-archive.md).

**Tím jsou všechny audit body H3–H6 + blokery hotové** - audit před produkcí uzavřen (H1/H2 přesunuty do Fáze C+D).

## 2026-07-07 - MHD zastávkový JŘ: B1 (dopravce z DB) + oprava crashe pickeru

**B1 (blocker) - pryč s KV hardcode v MHD tiskové cestě** (`ZastavkoveJrMhd.cls`):
- `POPIS_DOPRAVCE` - místo natvrdo „…Karlovy Vary, a.s.……." čte **reálného dopravce linky** z
  `ALVA.MHD.LSO` (helper `PopisDopravce`: `DruhDopravceID` → vlastní firma `$$gIdFa^MlJFapi` /
  cizí `$$gDopFa^JRapi`, 1:1 vzor MHD2 Export; defensivní try/catch → prázdno při selhání).
- `NEJEDOU_SPOJE` - napojen na generátor `TiskyFce.CalculatePrvniAndPosledniSpojeText` (gated flagem
  linky `GenerovatNEJEDOU_SPOJE`).
- `TITULEK` / `POZNAMKA_POD_DOPRAVCE` / `INFORMACE` - MHD pro ně nemá zdroj → **odstraněny ze
  schématu i GetData** (per-agenda poctivé schéma; AD si je nechává, tam data jsou). Schéma ověřeno živě.

**Oprava crashe MHD instance pickeru** (pre-existující, nesouvisí s B1):
- `<UNDEFINED>err` - `GetAvailableInstances` catch bez inicializovaného `err` (`$$$CatchErrRest` ho čte).
  Init `s err=""`. Širší latentní vzor napříč agendami → finding H6.
- Throw „Žádná aktivní skupina linek" i pro legitimní „firma bez MHD dat" → error popup. Teď **prázdný
  picker** místo chyby (přepnutí na MHD u AD-only firmy nevyhazuje popup). Ověřeno (`[]`, ok:true).

Součástí commitu i M1 (guard prázdného `TypProvozuID`/`SmerID` v `PromitnutSpoje`, z auditu).

## 2026-07-07 - AD zastávkový JŘ: oprava hlaviček variantních časových sloupců

Fix tří anomálií ve variantních časových sloupcích (`TRASA_MIN`) zastávkového JŘ z AD dat
(`ZastavkoveJrAd.cls`), nalezených při testování na lince 0365005:

- **Fallback hlavičky „ZT" → „T1"/„T2"…** - varianta bez písmene rozdílu trasy dřív spadla na „ZT"
  (základní trasa); víc takových variant → „ZT ZT ZT" (kolize). Teď inkrementální „T1"/„T2".
- **Dedup identických sloupců** - signatura varianty se počítá jen přes **zobrazené zastávky**
  (`servedPoradi`) místo celé W-mapy. Dvě varianty lišící se MIMO tuto vývěsku dřív daly dva
  identicky vypadající sloupce („C C", „E E"); teď se sloučí.
- **Unikátní hlavičky při kolizi písmene** - dvě různé varianty se stejným písmenem rozdílu trasy
  (liší se jen časy) dřív obě „B" s jinými hodnotami; teď „B1"/„B2". Unikátní písmeno zůstává „B".
- Gold `TestGold0365001`/`TestGold0365005` re-pinnuté (změnil se jen md5 - pages/zast/odj/pozn
  identické). Caché testy 27/27. Ověřeno v preview (0365005: `B B C C E E` → `B1 B2 C E`).

## 2026-07-07 - DynamicTable = varianta A (jako zastávky); R2 (řádkové stránkování) odstraněno

Po revizi konzistence (viz níže „⚠️ zjištění"): DynamicTable se má chovat **stejně jako zastávky**
(`ScheduleElementBase`) - **`Height` je strop**, obsah se do něj vejde auto-scalem, prvek se
NErozrůstá přes stránky. Vícestránkové řádkové stránkování s opakovaným záhlavím (R2) se ukázalo
jako nekonzistentní se zbytkem návrháře (statické prvky mají výšku = strop) a **vědomě odloženo** -
přidá se zpět jen na konkrétní poptávku, čistě, ne jako poloviční řešení.

- **Odstraněno kompletně (žádný mrtvý kód):** `IRowPaginatedElement`, `PageLayoutEngine.LayoutRowPaginated`
  + `RowPaginatedElement`, `PlacedSection.StartRowUnit/EndRowUnit/RepeatHeader`, R2 větev v `PreviewCanvas`,
  `DynamicTableElement.RenderRowWindow` + `RowUnitCount/HeaderBandHeightMm/RowUnitHeightMm`, testy R2.
- **Zůstává:** auto-scale do pevné Height (jako zastávky), dynamická výška (`Height=null`),
  pixel-ořez u „sekce vyšší než stránka" (beze změny).
- **Oprava rámečku:** vnější rámeček tabulky se vsazuje dovnitř o půl tahu (`InsetForStroke`), aby ho
  těsný clip sekce neukrojil na hraně (dřív poloviční/neviditelná horní/pravá/dolní hrana).
- Testy 214/214.

> Předchozí R2 záznamy (2026-07-06 níže) jsou tímto **superseded** - feature byla přidána a po
> validaci konzistence zase odstraněna, aby kód šel do produkce čistě.

## 2026-07-06 - Fix(R2): stránkování i pro odsazenou tabulku (Y > 0)

- 🐛 **R2 se nespouštělo, když byla tabulka odsazená od vrchu sekce** (uživatel ji posunul na
  Y=160.7 „aby přetékala"). Trigger vyžadoval `Y ≤ 20`. Ověřeno proti reálně uloženému JSONu.
- **Fix:** Y offset se pro řádkové stránkování **ignoruje** - paginující tabulka plní stránky od
  vrchu sekce (`RenderRowWindow` kreslí od 0, ne od `element.Y`). R2 tak funguje bez ohledu na pozici.
- Ověřeno render-do-PNG na přesné uživatelově konfiguraci (Y=160.7, výška prázdná, řádek 10, 36 zastávek):
  strana 2 = **zopakované záhlaví + řádky 29–36**. R2 potvrzeno end-to-end. Testy 218/218.

## 2026-07-06 - Fix: dynamická DynamicTable se v renderu ořezávala na ~40mm placeholder

- 🐛 **Dynamická (Height=null) DynamicTable ukazovala v preview/PDF jen ~5 řádků, zbytek uříznutý.**
  Kořen: `Render` používal pro clip `EffectiveDesignerHeight` (= placeholder ~40mm), místo skutečné
  výšky z dat. Ověřeno render-do-PNG: 36 řádků → viditelných jen 5.
- **Fix:** `tableHeight = Height ?? (ctx.Data is not null ? CalculateHeightFromData(ctx.Data) :
  EffectiveDesignerHeight)` - dynamická tabulka s daty se kreslí na plnou výšku obsahu. Po opravě
  render ukáže všech 36 řádků.
- Souvisí s R2: kvůli tomuto bugu nešlo R2 vyzkoušet (dynamická výška se ořízla, pevná výška ≤ stránka
  zase blokovala trigger - viz předchozí fix). Teď: dynamická tabulka s obsahem &gt; stránka → R2
  (row-break + opakované záhlaví, ověřeno render-do-PNG); s obsahem ≤ stránka → celá na jedné stránce.

## 2026-07-06 - R2: svislé stránkování řádků (DynamicTable) - láme po řádcích + opakuje záhlaví

- ✅ **Dlouhá tabulka se láme mezi celými řádky (nikdy vejpůl) a záhlaví se opakuje na každé stránce.**
  Nahrazuje dřívější pixel-ořez sekce (uřízl řádek + hlavička jen na 1. straně).
- **Rozhraní `IRowPaginatedElement`** (`Models/Layout/`): `RowUnitCount` / `HeaderBandHeightMm` /
  `RowUnitHeightMm` (mm, čistá logika) + `RenderRowWindow`. Implementuje **DynamicTable** (jednotka =
  datový řádek; souhrnné řádky = koncové jednotky; záhlaví = opakovaný band).
- **Engine** (`PageLayoutEngine.LayoutRowPaginated`): pro Split/AllowSplit sekci s jedním
  `IRowPaginatedElement` greedy plní jednotky do oken stránek (aspoň 1/stránku), `PlacedSection` nese
  `StartRowUnit`/`EndRowUnit`/`RepeatHeader` místo pixel-clipu. Fallback = dnešní pixel-ořez (nic nerozbito).
- **Renderer** (`PreviewCanvas.RenderPage`): u row-window PlacedSection volá `RenderRowWindow` (header +
  okno řádků + vlastní rámeček) místo clip-offsetu. Sdílený `RenderPage` → platí i pro PDF/PNG export.
- Trigger v reálné appce: **dynamická** (Height=null) DynamicTable naroste s obsahem → sekce přesáhne
  stránku (i výchozí SplitAcrossPages) → stránkuje po řádcích. Zebra/zvýraznění dle absolutního indexu
  řádku (spojité přes stránky). StopList/LineTable navážou (zatím jen DynamicTable).
- Testy 215/215 (`RowPaginationTest` - 50 řádků → souvislá okna, opakované záhlaví, chunky se vejdou).

## 2026-07-06 - Review fixes layout tracku (footer WYSIWYG + mrtvý kód)

- **Footer kotven ke spodku rostoucího papíru** (`GetSectionOrigins`) místo „za tělem" - u jednostránkové
  šablony tak patička v návrháři sedí s preview/tiskem (dole), u vysokého obsahu vyjde hned za tělem.
  Opravuje WYSIWYG regresi z Slice 1 (krátké tělo → patička dřív plavala uprostřed). Render i hit-test
  konzistentní (obojí přes GetSectionOrigins).
- Odstraněn mrtvý `DesignerCanvas.CalculatePageHeightPx(Template)` (nahradilo `TotalPaperHeightPx`).
- Nezávislý review tracku „sekce vyšší": žádný BLOCKER/MAJOR; ověřeno bez regrese jednostránkových
  šablon, hit-testing konzistentní, border-fix správný i pro PDF/PNG, bez rekurze v refreshi. Testy 214/214.

## 2026-07-06 - Fix: dvojité vykreslení rámečku u split sekce (preview/PDF/PNG)

- 🐛 **Sekce vyšší než stránka (SplitAcrossPages) kreslila rámeček elementu i na druhé straně.** Kořen:
  `PreviewCanvas.RenderPage` druhý průchod „rámečky mimo clip" běžel **zcela bez clipu**, takže u split
  chunku (kde má `ctx` origin posunutý o `ClipTop`) prosákl rámeček elementu z jiného chunku na tuto
  stránku. Nové elementy mají default rámeček → „dvojité vykreslení". Engine dělil správně (ověřeno).
- **Fix:** border-pass ořezán na **rámec stránky** (ne úplně bez clipu) - rámečky na hranici sekce
  zůstávají vidět, ale elementy z jiného chunku (posunuté mimo stránku) se ořežou. Jeden `RenderPage`
  = sdílený preview + PDF (`RenderMultiDataToPdf`) + PNG → opraveno všude. Pre-existing bug (Slice 1
  ho jen zpřístupnil). Testy 214/214 (nový `SplitSectionPaginationTest` na strukturu splitu).

## 2026-07-06 - Layout track / „sekce vyšší než stránka" - Slice 2: dělící čáry stránek

- ✅ **`DesignerCanvas.DrawPageBreakLines`** - přerušované modré čáry + popisek „Strana N" přes souvislé
  plátno, po výšce tisknutelné oblasti (`PageSize.Height − okraje`). Kreslí se jen když obsah přesahuje
  jednu stránku. Orientační vodítko „kudy půjde zlom" při editaci.
- **Varianta 1 (geometrické), vědomě NE pixel-přesné** vůči preview - nezná opakování hlaviček/patiček
  ani `KeepTogether`. Přesný stránkovaný výsledek je a zůstává v preview/PDF. (Varianta 2 „přesně dle
  enginu" zamítnuta - návrhář ukazuje autorské výšky, engine datové → stejně by neseděly.)
- Build 0 chyb (canvas = UI, ověřuje se vizuálně). Tím je track „sekce vyšší než stránka" hotový;
  navazuje R2 (svislé stránkování řádků - zpřesní i tyto zlomy).

## 2026-07-06 - Layout track / „sekce vyšší než stránka" - Slice 1: plátno roste

- ✅ **Návrhové plátno už není omezené na jednu A4.** `DesignerCanvas.GetSectionOrigins` skládá sekce
  souvisle shora dolů v jejich **pravé (neclampnuté) výšce**, bez zahazování za přehybem; hlavička+tělo
  v pořadí, patičky za tělem (autorský pohled - per-stránkové opakování řeší preview/engine).
- Nové `TotalPaperHeightPx` (horní okraj + Σ výšek sekcí + spodní okraj, min = 1 stránka); `Render`
  kreslí papír/rámeček/okraje/grid/pravítka na tuto rostoucí výšku. `UpdateCanvasSize` + `RequestCanvasRefresh`
  přepočítávají velikost plátna po každé změně (sekce/element).
- `GetClampedSectionHeightPx` → `SectionHeightPx` (pravá výška); hit-testing i render jedou přes tentýž
  zdroj → automaticky konzistentní. Odstraněn single-page strop `GetPageBottomY`.
- **Zatím bez dělících čar** (Slice 2) - plátno roste souvisle. **Preview/PDF beze změny** (už multi-page).
  Build 0 chyb, testy 213/213 (canvas = UI, ověřuje se vizuálně).

## 2026-07-05 - B1: embedding fontů do .exe (mechanismus, R5)

- ✅ **`FontResolver` embedding mechanismus** - `.ttf`/`.otf` hozené do `dotnet/Fonts/` se glob-em
  v .csproj zabalí jako `<EmbeddedResource>` do assembly (a single-file .exe), `FontResolver` je
  **auto-načte** z manifest resources podle názvu rodiny (bez úpravy kódu). Priorita
  `embedded → OS → Arial → Skia default`; embedded má přednost = deterministický výstup nezávislý
  na nainstalovaných fontech (důležité pro headless/server). Varianty (Bold/Italic) = samostatné
  soubory, vybírá se nejbližší dle stylu.
- Refaktor `FontResolver.Resolve` na **sdílené cachované typefacy** (app-lifetime, nedisposovat) -
  všechny cesty renderu (text/buňky/JŘ) přes něj už tečou, takže embedding platí všude. Bonus: mizí
  dřívější re-`FromFamilyName` per volání. `InstalledFamilies` = OS ∪ embedded (i v UI výběru fontu).
- `dotnet/Fonts/README.md` + `.csproj` komentář: **licence** - jen redistribuovatelné fonty
  (OFL/Apache - Arimo, Liberation Sans), NE komerční (Helvetica/Arial). Základní text je nepotřebuje
  (OS Arial). **Fonty samotné nepřidány** - mechanismus je připravený, uživatel dropne font do Fonts/.
- JŘ symbolový font (Timetable, C2) až po vyjasnění zdroje/licence. Testy 213/213 (4 nové - resolve/cache/fallback).

## 2026-07-05 - A3: min/max řádků DynamicTable + klasifikační pravidlo featur

- ✅ **`DynamicTableElement.MinRows` / `MaxRows`** - `MinRows` doplní prázdné řádky do minima (pevná
  výška boxu položek, např. faktura - součet/patička sedí na stejném místě bez ohledu na počet
  položek); `MaxRows` ořízne přebytečné datové řádky. 0 = vypnuto. Render rozlišuje 3 typy řádků
  (návrhový `{POLE}` placeholder / datový / prázdný padding); prázdné padding řádky drží mřížku,
  rámeček i zebru, bez zvýraznění. Výška↔render parita (`EffectiveRowCount` = Max ořez + Min padding),
  kombinuje se s filtrem viditelnosti. DynamicTable-only (kategorie 2, viz níže).
- 📐 **Klasifikační pravidlo featur** zapsáno do `elementy-architektura.md §0b` - rozhodnutí z
  diskuze 2026-07-05: (1) featury nad daty řádku → sdílený helper do všech tabulek; (2) struktura
  sloupce / počet řádků → jen generická DynamicTable (počítaná pole, min/max); (3) doménové
  renderování → element-specifické. Generické featury jsou pro daná data dostupné přes DynamicTable
  napojenou na tutéž kolekci (StopList = volba „doménový vzhled", ne „featura nejde"). NEslučovat
  StopColumn/TableColumn (R1).
- Testy 209/209 (4 nové - min padding, max ořez, kombinace s filtrem). Caché schema zkompilováno.

## 2026-07-05 - A2 review fixes

- **MAJOR:** `EvaluateToString` číslo teď přes `FormatNumber` - zaokrouhlení na 10 des. míst
  (odstraní IEEE-754 artefakty `0.1*3 → 0.30000000000000004`), oříznuté koncové nuly, invariantně.
  `NaN`/`±Infinity` → prázdno (nešíří „NaN" do buňky). Bez `Format` tak počítaný sloupec neukáže
  plovoucí smetí - důležité pro peníze/množství.
- Konkatenace v `+` (nečíselné) formátuje případný double invariantně (ne CurrentCulture).
- NIT: statický `TableEditorDialog` round-trip zachovává `Expression` (konzistence, dormant dialog).
- Nezávislý review: žádná regrese existující viditelnosti, priorita/asociativita/fail-soft ověřeny.
  Ponecháno vědomě: `ToDouble` = `NumberStyles.Any` (pre-existing, sdílené s Compare - tightening
  by riskoval regresi viditelnosti; data jsou invariantní). Testy 205/205 (2 nové - FP, NaN/Inf).

## 2026-07-05 - A2: počítaná pole (aritmetika ve výrazovém jádru + počítaný sloupec DynamicTable)

- ✅ **Aritmetika ve výrazovém jádru** - `ExpressionParser` rozšířen o `+ - * /`, unární minus,
  prioritu (`* /` > `+ -` > porovnání) a závorky (už byly). Nové gramatické úrovně additive/mult/unary.
  `ExpressionEvaluator`: `+` (čísla → součet, jinak konkatenace), `- * /` (nečíselný operand nebo
  dělení nulou → null = prázdná buňka), unární `-`. Funguje i v podmínkách (`{A}*2 > 10`).
- ✅ **`ExpressionEvaluator.EvaluateToString`** - hodnotové vyhodnocení výrazu (číslo invariantně,
  ať navazující `Format` přeparsuje dle kultury); prázdný/chybný výraz → prázdno (fail-soft).
- ✅ **`TableColumn.Expression`** - počítaný sloupec DynamicTable: výraz nad poli řádku
  (`{CENA} * {POCET}`), přebíjí `Field`, výsledek formátovatelný přes `Format` (např. „N2").
  Editace ve Sloupce tabu (nové pole „Výraz" + „Formát").
- 🐛 **Fix při této příležitosti:** editor sloupců DynamicTable dosud `Format` vůbec neukládal
  (ToModel/FromModel ho vynechávaly → při editaci sloupce se ztrácel). Teď round-trip včetně Format.
- Testy 203/203 (9 nových - aritmetika, priorita, dělení nulou, konkatenace, round-trip sloupce).
  Bez Caché změny (sloupce = WPF dialog, ne EDInputForm).

## 2026-07-05 - Review fixes podmíněných řádkových featur

- **DynamicTable:** placeholder `{POLE}` řádky se kreslí **jen v návrháři** (`ctx.Data is null`);
  s DataContextem (preview/tisk) - i když je vše odfiltrované nebo kolekce prázdná - se vykreslí
  reálně prázdná tabulka, ne fake řádky v tiskovém výstupu. Výška konzistentní (0 řádků). Opravuje
  pre-existing kvartét, který row-visibility filtr zpřístupnil.
- **LineTable:** sjednocen výpočet přípojných řádků ve výšce (prázdná i aktivní větev teď počítají
  jen pripoje matched na `Poradi` viditelné zastávky - přesně jako Render); odstraněna asymetrie
  a nepoužitá `PripojeRowCount`. Výška↔render parita ve všech případech.
- Review (nezávislý): žádný BLOCKER/MAJOR, invarianty (behavior-preserving, parita, index zarovnání,
  cache/SourceRow, precedence, fail-open) ověřeny. Zbývá NIT: `SKColor.Parse` na nevalidní barvě =
  latentní cross-cutting risk (sdílený s OddTcBackColor/AlternatingRowColor/…), řešit případně
  sdíleným TryParseColor napříč elementy. Testy 194/194.

## 2026-07-05 - Viditelnost řádku na StopList + LineTable + discoverability polí

- ✅ **Viditelnost řádku rozšířena na StopList + LineTable** (`RowVisibilityExpression`) přes sdílený
  `RowConditions`. StopList: skrytá zastávka se nevykreslí (skip po určení current/passed, ať se
  nerozbije sledování). LineTable: skip **in-place** (iterace přes plný index `si` → zarovnání časů
  spojů `Casy[si]` zůstává; skryje se řádek zastávky i její přípojné řádky).
- **Výška je guarded** - při prázdném výrazu původní kód beze změny (behavior-preserving pro gold-1:1
  StopList i pilot LineTable); při aktivním výrazu se počet řádků spočítá **přesně jako v Render**
  (parita výška↔render): StopList mirror logiky projetých zastávek, LineTable jen viditelné zastávky
  + jejich přípojné řádky.
- **Discoverability polí (dotaz uživatele):** helpText podmínkových polí teď vypisuje dostupné názvy
  polí řádku - StopList (POPIS, TC, PORADI, NA_ZNAMENI…), LineTable (POPIS, TC, ZONA, MHD, TYP…),
  DynamicTable („názvy = mapování Field ze sloupců"). Panel „Pole agendy" nadále ukazuje jen skalární
  pole (kolekční řádková pole tam vědomě nejsou - draggable by mátla).
- Testy 193/193 (beze změny - nová logika default-off). Caché StopList/LineTable/DynamicTable schema
  zkompilováno.

## 2026-07-05 - Podmíněné zvýraznění řádku: sdílený `RowConditions` + rozšíření na StopList + LineTable

- ✅ **`Models/Expressions/RowConditions.cs`** - sdílená řádková podmínková logika (výraz nad daty
  jednoho řádku → viditelnost / barva zvýraznění pozadí) nad `ExpressionEvaluator` (fail-open).
  R1 filozofie: sdílíme **algoritmus**, ne datovou třídu - každý element drží svá 2 pole
  (`RowHighlightExpression` + `RowHighlightColor`) i svůj řádkový POCO, do helperu posílá jen
  DataContext řádku.
- ✅ **Zvýraznění (podbarvení) rozšířeno na StopList + LineTable** - řádek/zastávka splňující podmínku
  dostane pozadí. StopList: priorita nad podbarvením aktuální zastávky. LineTable: celá šířka řádku
  zastávky. Per-řádek DataContext nese nové `[JsonIgnore] SourceRow` na `ZastavkaData`/`LineStopData`.
- **Zvýraznění je aditivní (jen pozadí) → žádná interakce s výškou** → default vypnuto = **nulová změna**
  → bezpečné pro gold-1:1 StopList i pilot LineTable (behavior-preserving při prázdném výrazu).
- DynamicTable přepnut na sdílený helper (dřív měl vlastní inline logiku). UI: pole ve skupině
  „Podmínky" (EDInputForm) u všech tří + zrcadlící PropertyDescriptory.
- **DepartureGrid vědomě mimo** - je pivot (hodina × skupina dnů), „řádek" = agregace odjezdů, ne
  jeden záznam; per-odjezd barvu řeší prezentační nápovědy `FORE_COLOR`/`BACK_COLOR` z dat.
- **Viditelnost řádku** zůstává zatím jen v DynamicTable (na StopListu se proplétá s logikou projetých
  zastávek a výškou - vědomý další krok, ne teď).
- Testy: 193/193 (7 nových - RowConditions helper). Caché StopList/LineTable schema zkompilováno.

## 2026-07-05 - DynamicTable: viditelnost řádku (`RowVisibilityExpression`) - PoC podmíněného renderingu

- ✅ **`DynamicTableElement.RowVisibilityExpression`** - výraz nad daty řádku (`{CENA} > 0`);
  nesplněné datové řádky se nevykreslí a **nezapočítají** (výška i agregace v souhrnných řádcích).
  Analogie element-level `VisibilityExpression`, jen per řádek. Prázdné = všechny řádky.
- Vyhodnocení přes sdílený `ExpressionEvaluator.IsVisible` proti DataContextu řádku (fail-open).
  Jednotný helper `VisibleDataRows` filtruje před render/výška/souhrn → skryté řádky se nesčítají.
- UI: pole v tabu „Obecné" (EDInputForm `DynamicTableElementEdit.cls`, skupina „Podmínky", pod
  viditelností elementu) + zrcadlící PropertyDescriptor. Binder = reflexe (žádná extra vazba).
- Testy: 183/183 (3 nové - filtr do výšky, prázdný výraz, vše skryté → náhledová fallback větev).
- **PoC pro navazující podmíněné formátování** (obarvení řádku dle podmínky) - to bude seznam
  pravidel (podmínka→barva) přes sdílený helper, ať ho vezme i StopList/LineTable.

## 2026-07-04 - R1: sdílené tabulkové jádro `TableLayout` (extrakce hotová)

- ✅ **`Rendering/TableLayout.cs`** - sdílené algoritmy řádkových tabulek nad `CellRenderer`/
  `ColumnRenderer`: `ResolveWidths` (pevné + auto-split zbytku + mm/%) a `ResolveCascade`
  (kaskáda column→element→baked + legacy alignment fallback). **Záměrně nesjednocuje datovou
  třídu sloupce** - StopColumn i TableColumn zůstávají, elementy jen promítnou neutrální tvar
  (`ColWidth`, per-column TextStyle) → nulové riziko pro uložené šablony.
- ✅ **Napojeno do DynamicTable, LineTable, StopList** - kaskádový vzorec 3 kopie → 1, výpočet
  šířek 2 kopie → 1 (StopList mm-only, DynamicTable mm/%; LineTable šířky ponechány - Km-expanze +
  trip-pool jsou element-specifické, migrovaná jen kaskáda). Pátá tabulková sestava dostane
  šířky+kaskádu zdarma.
- **DepartureGrid mimo jádro** (potvrzeno analýzou - je pivot hodina × skupina dnů, ne řádková
  tabulka; sdílí jen datovou/nastavovací vrstvu).
- Behavior-preserving: **180/180 testů** (vč. StopListCascadeTests/DepartureGridCascadeTests) +
  render diff LineTable AD (0320680) i MHD identický. Analýza + návrh:
  `.claude/docs/r1-sdilene-tabulkove-jadro-navrh.md`.
- Zbývá volitelně (R1a): render-loop orchestrátor (header+řádky+separátory jako 1 metoda) -
  DynamicTable dnes kreslí vlastní per-buňka smyčkou; nízká hodnota / vyšší riziko, odloženo.

## 2026-07-04 - LineTableElement: kolekce sloupců + 2-tab dialog (Obecné + Sloupce)

- ✅ **`LineTableElement.Columns : List<StopColumn>`** - element přešel z pevných `Show*Column`/
  `*ColumnWidth` properties na sdílený `StopColumn` model (klíče Zone/Km/Tč/StopName/Mhd + „Trip"
  pro časové sloupce spojů). Render iteruje viditelné sloupce v pořadí, resolvuje per-column
  header/data styl přes kaskádu `column.Style → element → CellDefaults` (`ResolveHeaderStyle`/
  `ResolveDataStyle`, vzor StopList), Km auto-expand na N sub-sloupců, MHD auto-remove když prázdné,
  auto šířky (null) sdílí zbytek se spoji. Render výstup beze změny (ověřeno na 0320680 i CB_DP 1001).
- ✅ **2-tab dialog `LineTableColumnsEditorDialog`** (Obecné = EDInputForm `LineTableElementEdit`,
  Sloupce = sdílený `ColumnListEditor` + `CellStyleEditor` + `LineTableColumnVM`) - dvojklik/Enter
  na LineTable teď otevře stejný dvoutabový editor jako StopList/DepartureGrid: reorder ▲▼,
  viditelnost, šířky, per-column styl záhlaví/dat, overflow warning. Konzistentní se sloupcovým API
  ostatních tabulkových elementů. EDInputForm schéma zbaveno sloupcových polí (přesunuta do tabu).
- Pozn.: přesně na architektuře R1 - sdílený column model + `ColumnListEditor`/`CellStyleEditor` je
  ta část, která je společná; extrahuje se až samotný layout algoritmus (později).

## 2026-07-04 - Knižní JŘ z MHD dat: agenda KnizniJrMhd

- ✅ **`ALVA.TemplateDesigner.Agendy.KnizniJrMhd`** (`KNIZNI_JR_MHD`) - MHD analogie `KnizniJrAd`
  (legacy větev `VolanoZ="MHD"` = `ALVA.MHD.LinkyPrintFormatJR.MakeData`): stejný knižní wire
  formát → konzumuje tentýž `LineTableElement` (kontextová paleta `KNIZNI_*` už platí). Data z
  `ALVA.MHD.LSO/ZST/ENUMS` + helpery `*Fce` (MakeTableSpoj, GetNazevHrl, GetPoznamkySpoj…):
  trasa s filtrem kategorie 1001 + slučování sousedních shodných zastávek, zdvojené řádky
  odjezd/příjezd (atribut trasy 1011), zóny přes IDSID match, skupiny = TypProvozu (nadpisy
  z ENUMS NadpisSMOPrvni/Druhy, řazení dle Poradi), markery `|` projíždí / `<` jede jinudy,
  NZ + auto-poznámkování rozdílů platností (GetJede/MakeOmezeniTXT), spec. znaky a atributy
  B→BEV/C→CBE v řádku platnosti. Bez Km/Tč/MHD sloupců a vyčká/navazuje (MHD zdroj nemá, 1:1).
  Ověřeno na CB_DP lince 1001 (29 zastávek × 159 spojů × 3 typy provozu, oba směry + render PNG).
- Pozn.: gold-oracle diff pro MHD odložen - legacy `LoadData` MHD větev na dev serveru padá
  (ztrap po MakeData); ověření zatím strukturální + vizuální. Bin-packing (159 sloupců → bloky)
  = krok 43 layout rozšíření.
- ✅ **EDInputForm pro LineTableElement** - `ALVA.TemplateDesigner.EDInputForm.LineTableElementEdit`
  (skupiny Sloupce/Řádky/Záhlaví/Datové buňky/Rámeček/Pozice/Pokročilé) + registrace v
  `EDInputFormSchema` → Enter/dvojklik otevře standardní form-driven dialog „Vlastnosti knižního JŘ"
  a inline panel jede na tomtéž schématu (konzistentní s ostatními elementy). Per-column tab
  „Sloupce" (ColumnListEditor) přijde až s R1 extrakcí jádra, až element dostane column kolekci
  (per-spoj overridy) - v prvním řezu jsou popisové sloupce pevné properties (vzor NotesLegend).

## 2026-07-04 - Knižní/linkový JŘ: agenda KnizniJrAd + LineTableElement (roadmap kroky 42/43/44 - první implementace)

- ✅ **Caché agenda `ALVA.TemplateDesigner.Agendy.KnizniJrAd`** (`KNIZNI_JR_AD`) - čistá reimplementace
  datového jádra legacy `mkPLinWin^JRapiLinPrtWin` nad `^JRL`/`^JRZ`/`^JRSET`: 1 stránka per linka×směr,
  sémantický JSON wire (ZASTAVKY svislá osa vč. zdvojených řádků příjezd/odjezd, SPOJE sloupce s CASY
  markery `|`/`<`, plný řádek platnosti X..+/místenka/u/p/atributy, PRIPOJE s gate typVýstupu/typZobrazení,
  SKUPINY_DNU z WinMHD DSC, POZNAMKY druhů 1-10 z NCOME/STALCOME, TiskDtm režimy datumů, mkPoradiSp
  řazení, zóny jen u IDS linek, MHD sloupec s auto-remove).
- ✅ **`LineTableElement`** - nový standalone element (ScheduleElementBase): matice zastávky × spoje,
  pás skupin dnů, řádky platnosti/NZ, přípojné řádky (RADEK=1), zdvojené podřádky, MHD/zóna/km/Tč
  sloupce s auto šířkou spojů, podbarvení lichých Tč.
- ✅ **Ověření 1:1 proti gold oracle** (`ALVA.TISKJR.Data/LoadData`, `VOLANO_Z~JRMAIN`): multi-linkový
  diff harness - **22/24 linek napříč CB_CSADAD/CB_GWBUS/KV_DP/VS_CSAD zcela beze změn**; zbylé
  odchylky kosmetické a zdokumentované (`linkovy-knizni-jr-implementace-analyza.md` §11-12).
- ✅ **Výchozí šablona** `templates/knizni-jr-ad-vychozi.json` (hlavička, JŘ tabulka, legenda poznámek).
- Vědomě odloženo: symbolový font Timetable (krok 42b), 2D bin-packing/Strih (layout řeší TD),
  turnusy, obousměrný tisk, konfigurovatelné pořadí poznámek.
- ✅ **Kontextová paleta JŘ elementů dle agendy** - `UpdatePaletteForAgenda()` (napojené na
  RequestTitleUpdate, který se volá při každé změně agendy/šablony): agendy `ZASTAVKOVY_*` ukazují
  StopList/DepartureGrid/NotesLegend, `KNIZNI_*` ukazuje LineTable (nové tlačítko palety
  `BtnPalLineTable`), agenda bez JŘ kontextu (FAKTURY/GENERAL) ukáže vše. Stejný filtr v menu
  „Přidat element". Instance picker pro `KNIZNI_JR_AD` posílá `LINKA_ID` (InstanceParamName).

## 2026-07-03 - Wave 4 W4-10: inline panel na EDInputForm (Wave 4 uzavřena)

- ✅ **Levý panel „Vlastnosti" hostuje EDInputForm** pro jeden vybraný element (všech 9 typů přes
  `*Edit.cls` schema + `ElementFormBinder`) místo PropertyDescriptorů. Sjednocuje „dvojí vizuální jazyk"
  - panel i modály jedou na stejném form-driven UI. Nový `Controls/InlinePropertiesForm` (embedded host).
- ✅ **Živý write-back:** nový `EDInputForm.FieldChanged` event / Enter → `ElementFormBinder.ExtractCommands`
  → `SetPropertyCommand` (undo) → canvas. Enter v poli commitne pole (ne otevře dialog); bare klávesy
  (Delete/šipky) patří poli (+ fix latentního bugu: Delete v poli mazal vybraný element).
- ✅ **EDControls (additivní):** `FieldChanged` event; `CacheClass` setter zahodí controller při změně →
  runtime přepnutí schématu naloadí správnou třídu (dřív držel první → StopList ukazoval textová pole).
- **Wave 4 tím funkčně uzavřena** (form-driven UI všude, kde uživatel přímo edituje). Záměrně nedokončeno:
  W4-11 (smazat PropertyDescriptory) neproveditelné - drží je FormatToolbar + multi-edit + fallback modál
  + AI CapabilityCatalog (paralelní systém); W4-12 (wire `$lb()`→JSON, collapsible groups) volitelné.
- Multi-select + FormatToolbar + AI catalog beze změny (na PropertyDescriptorech). Testy: TD 180/180, EDControls 331/331.

## 2026-07-03 - T4 malé kousky (R4 fix + rename) + StopList kaskáda testy

- ✅ **R4 fix - `TableEditorDialogViewModel` bez WPF.** Inline WPF picker šablon tabulky (Window + ListBox
  + Buttons stavěný ve ViewModelu) přesunut do code-behind `TableEditorDialog.xaml.cs` přes callback
  `RequestPickTableTemplate` (VM zná jen výsledný klíč). VM je teď 0× `System.Windows` (R4). Behavior-preserving.
- ✅ **Rename `MainWindowViewModel.cs` → `TemplateDesignerViewModel.cs`** - soubor teď odpovídá názvu třídy
  (`git mv`, obsah beze změny, SDK globbing).
- ✅ **`StopListCascadeTests` (9 testů)** - regresní pokrytí StopList-specifického cascade wiring: feature
  override aktuální/projeté zastávky (`ResolveRowStyle`), legacy `col.Alignment` fallback jen když kaskáda
  HAlign nenastaví, per-column `Style` priorita, element-level `HeaderStyle`. `ResolveHeaderStyle/DataStyle/
  RowStyle` zpřístupněny `public` (stejný vzor jako DepartureGrid Resolve*). Tím je uzavřen **malý T track**
  (T5a, T6, T4 R4+rename, StopList testy); velké T1b-robustní + plný T4 strangler zůstávají záměrně.

## 2026-07-03 - T5a: agenda kód→třída lookup bez hardcoded mirroru

- ✅ **`AgendyRegistr.GetClassForCode(agendaCode)`** - nový sdílený lookup (auto-discovery z `%Dictionary`,
  stejná cesta jako `ListAgendy`). `EDList/Instance.GetClassForAgenda` dřív hardcoduje mapování kód→FQN
  třídy (mirror Wave 2) → teď deleguje na `GetClassForCode`. Přidání nové agendy = jen nová `BaseAgenda`
  subclass, žádný druhý seznam k údržbě. Ověřeno: `ListAgendy` (stejný mechanismus) vrací správné mapování
  pro AD/MHD/FAKTURY.
- ⏭️ **T5b (`ZastavkoveJrAd.GetMockData` → statický mock) přeskočeno** - živý náhled z reálné linky
  ponechán záměrně (není zásadní; na rozdíl od MHD/Faktury zůstává AD design-time preview z živých dat).

## 2026-07-03 - T6 dokončeno (architektonický track drobnosti)

- ✅ **`architecture.md` doplněn o `/Services` (6 souborů) a `/PdfImport` (12 souborů)** - dřív v mapě
  složek chyběly (~7k LOC neviditelných v doc). Doplněn stručný popis každého souboru.
- ✅ **Per-frame `new SKPaint` v Render() běžných elementů - posouzeno a zavřeno** (nebude se dělat):
  painty jsou disposované (`using var`), per-element (ne per-buňka, jen 1–5/element/repaint), a závislé
  na měnitelných barvách elementu → cache by vyžadovala invalidaci za zanedbatelný zisk. Schedule elementy
  s 100+ buňkami painty pre-createují už dnes. Tím je **T6 celé hotové**.

## 2026-07-03 - StatusText segmentace: „stav výběru" × „akce/progres"

- ✅ **Status bar rozdělen na dvě zóny**, aby se nepřepisovaly. Dřív jeden `StatusText` míchal stav
  výběru (element/sekce), transientní feedback (uloženo/chyba) i progres (export N/N) - akční hláška se
  ztratila hned, jak uživatel klikl jinam. Nově: nová property **`SelectionStatusText`** (klidová zóna,
  píše ji jen `UpdateStatusForSelection`) + stávající **`StatusText`** repurposed jako transientní
  „akce/progres" zóna (~14 zápisů: export, uložení, chyby, načítání dat, migrace, schránka). XAML:
  levý `*` sloupec = výběr (šedý, ellipsis), pravý Auto sloupec = akce (tučný, primární barva),
  overflow blok posunut. Akční hláška zůstává viditelná i po změně výběru.

## 2026-07-03 - Escape ruší probíhající operaci (drag / resize / rubber band)

- ✅ **Escape během tažení / změny velikosti vrátí element na začátek** (`EndDrag`/`EndResize` s
  `commit: false` - už existující restore-z-init cesty). **Escape během rubber bandu** ho zahodí bez
  změny výběru. Bez operace = zruší výběr (beze změny), během přidávání z palety = zruší přidávání
  (beze změny). `OnWindowKeyDown` je na `PreviewKeyDown` okna → Escape dorazí i při mouse-capture dragu.

## 2026-07-03 - Discoverability: skryté featury do context menu

- ✅ **Context menu (pravý klik na element)** dostal položky, které dřív šly jen zkratkou:
  **Seskupit (Ctrl+G)** - jen při 2+ vybraných; **Rozeskupit (Ctrl+Shift+G)** - jen když je výběr
  ve skupině; **Rozdělit → Vodorovně (Ctrl+Shift+H) / Svisle (Ctrl+Shift+J)**; **Upravit text (F2)** -
  jen u TextElementu. Umístění: za „Z-pořadí", před „Vlastnosti elementu…", obklopeno separátory.
  Zviditelňuje featury bez nutnosti znát zkratku.

## 2026-07-03 - Zoom: tlačítko × zkratka konzistentní + přepis tabulky zkratek

- ✅ **Zoom - tlačítko a zkratka se shodují.** Dřív „1:1" tlačítko dělalo reset na 100 %, ale Ctrl+0
  dělal fit-to-page → nekonzistentní. Nově dvě konzistentní dvojice: **„Fit" tlačítko + Ctrl+0** =
  přizpůsobit stránce; **„1:1" tlačítko + Ctrl+1** = skutečná velikost 100 %. `ResetZoomTo100()` sdílený
  helper, `BtnFitPage_Click` → `FitToPage()`. (Dva nezávislé combo-zoomy designer/náhled ponechány -
  jsou to různé pohledy.)
- ✅ **`ux-conventions.md §3` přepsáno 1:1 dle KeyDown handleru** (byl drift). Smazán neexistující
  Ctrl+Shift+S (Uložit jako nemá zkratku); doplněno ~10 chybějících: F2 (inline text), Ctrl+P (tisk),
  Ctrl+Shift+P (vlastnosti stránky), Ctrl+Shift+A (změna agendy), Ctrl+B (levý panel),
  Ctrl+PageUp/PageDown (Z-order), šipky (posun elementu / sekce), Escape, Ctrl+0/Ctrl+1 (zoom).

## 2026-07-03 - Pre-pilot UX P0 dokončeno: U2 (context menu) + U6 (labely tlačítek)

- ✅ **U2 - context menu „Vlastnosti elementu…" otevře dialog.** Dřív jen vybralo element + přepnulo
  inline panel - u stejně pojmenované akce „Vlastnosti sekce…" (dialog) nekonzistentní, a „…" v názvu
  dialog implikuje. Nově volá `OpenPropertiesForElement(el)` (stejná cesta jako dvojklik).
- ✅ **U6 - sjednocení labelů tlačítek.** Cancel byl v TD rozjetý („Zrušit" 5× vs „Storno" 2×). Vše
  převedeno na `{StaticResource EDStrings.Cancel}` = „Storno" (firemní standard - EDStrings + EDFooter);
  „OK"/„Uložit" literály na `EDStrings.Ok`/`EDStrings.Save` (7 dialogů: CacheTemplatePicker, ChangeAgenda,
  InstancePicker, SaveSectionTemplate, SaveToCache, ElementEdit, NormalizePositions). Pravidlo (cancel =
  „Storno" přes EDStrings, primární = `EDStrings.Ok`/akční sloveso) zapsáno do `ux-conventions.md §1`.
- 🏁 **Tím je celé pre-pilot P0 (U1–U7) hotové.**

## 2026-07-03 - Pre-pilot UX P0: U3 (klik na paletu) + U4 (dvojklik do prázdna) + U7 ověřeno

- ✅ **U3 - klik na paletu vkládá element.** Dřív palette tlačítko jen zahájilo drag-to-canvas;
  prostý klik (bez tažení) nic nevložil (drag-only = neobjevitelné). Fallback v `EndAddingElement`:
  když se pustí bez cílové sekce (`_addingTargetSection == null`) a jde o element z palety, vloží se
  do `State.ActiveSection` na výchozí pozici 5×5 mm. Drag-to-canvas beze změny.
- ✅ **U4 - dvojklik do prázdna neotevírá vlastnosti stránky.** Fallback dvojkliku na prázdnou plochu
  (`OpenPropertiesForPage`) nahrazen zrušením výběru - dialog do prázdna byl překvapení. Vlastnosti
  stránky zůstávají na Ctrl+Shift+P / menu. Dvojklik na element / záhlaví sekce beze změny.
- ✅ **U7 ověřeno jako hotové** (audit byl stale): M/O snap toggly už tooltipy mají.

## 2026-07-03 - Pre-pilot UX P0: U1 (bug ukládání) + U5 + async confirm/close

- ✅ **U1 - BUG opraveno:** volba **„Ano = uložit"** v potvrzení neuložených změn (Nový / Otevřít /
  změna agendy / zavření okna) dřív ukládala do **JSON souboru** (`SaveAndContinue` → `SaveFileDialog` +
  `TemplateSerializer.Save`), ne do DB. Z pohledu uživatele ztráta dat - šablona nebyla v DB, kde ji čeká.
  Teď `SaveAndContinueAsync` volá **stejnou DB cestu jako Ctrl+S** (`SaveToCacheAsync`).
- ✅ **U5 - Ctrl+S bez DB** už netiše nepíše do status baru - `OfferJsonExportFallbackAsync` zobrazí
  popup („Databáze není dostupná - uložit jako JSON?") a na potvrzení spustí export.
- ✅ **Celý confirm/close flow async** (uložení do DB je async přes MLRest): `ConfirmUnsavedChanges()` →
  `ConfirmUnsavedChangesAsync()`, `SaveAndContinue()` → `SaveAndContinueAsync()`; `SaveToCacheAsync`/
  `SaveToCacheAsAsync`/`ExportToJsonAsync`/`OfferJsonExportFallbackAsync` vrací `Task<bool>` (true = reálně
  uloženo → volající pokračuje). `NewCommand`/`ChangeAgendaCommand` převedeny na `CommandAsync`;
  `MenuAiAssistant_Click`/`MenuImportPdf_Click` na `async void`.
- ✅ **Fix pádu při zavření okna** (`InvalidOperationException: nelze volat Close při zavírání`):
  nové async `Window_Closing` u čistého (nedirty) okna zavíralo synchronně uvnitř běžícího Closing eventu.
  Fix: nedirty okno → zavřít normálně (žádný cancel); dirty → `e.Cancel=true` + async potvrzení + `Close()`
  odložený přes `Dispatcher.BeginInvoke` (až po doběhnutí eventu).

## 2026-07-03 - Úklid T6: odstranění vestigiálního menu + MessageBox

- ✅ Odstraněno skryté menu **„Nástroje → Vlastnosti textu… (alternativa k dvojkliku)"** (+ handler
  `MenuTestTextEdit_Click`) - redundantní leftover z vývoje editoru; dvojklik i Enter dělají totéž
  (`OpenPropertiesForElement`). Tím zmizel i poslední `MessageBox` v `MainWindow.xaml.cs` (R2).
  `MessageBox` v `App.xaml.cs` (startup config warning + `OnUnhandledException`) ponechány - legitimní
  bootstrap/crash fallbacky (popup infra tam nemusí být dostupná). Dřív (t.d.) odstraněn i
  `ScheduleDetectorTest.cs` z produkční assembly.

## 2026-07-03 - Gold regresní test AD zastávkového JŘ (T1b základ)

- ✅ **`ALVA.TemplateDesigner.Tests.ZastavkoveJrAdGoldTest`** - základní 1:1 regresní pojistka:
  `GetData` na gold linkách **0365001 + 0365005**, digest = hrubé počty (ZASTAVKY/ODJEZDY/POZNAMKY)
  + MD5 celého spojeného výstupu, zmrazený jako baseline. Běží v pre-push Caché sadě (**27/27**).
  Smysl: zachytit, že se kódem nerozbil datový výstup.
- ⚠️ **Záměrně pevně vůči CB_CSADAD datům** (AD je per-firma namespace): drift JŘ dat = falešný pád
  (přegeneruj digest z hlášky „aktuální: …"), jiný server bez CB_CSADAD → test se přeskočí. Robustní
  verze (zmrazit vstupní `^JRL`/`^JRZ`/`^JRSET` do izolovaného namespace, data-drift-proof) odložena -
  viz roadmap T1b.

## 2026-07-03 - Architektonická rozhodnutí: R1 rozhodnuto, R2/R3 upřesněno, stav do roadmapy

- ✅ **Nová roadmap sekce „🧭 Architektonická rozhodnutí R1–R7 - stav a nejbližší akce"** -
  stavová tabulka všech rozhodnutí na jednom místě (plný text zůstává
  v `architektonicka-rozhodnuti-agendy.md`, kam doplněny poznámky z diskuze).
- ✅ **R1 rozhodnuto (diskuze):** zárodek sdíleného řádkového jádra = `DynamicTableElement`;
  extrakce až u LineTable (3 konzumenti ukážou, co je společné - dřívější extrakce nad dvěma
  by abstrakci tvarovala podle StopList domény). Nová akce **R1a**: hodinový kontrolní průchod
  DynamicTable (čisté rozvrstvení model sloupců / měření / render). Ujasněno: jádro ≠
  PageLayoutEngine (vnitřek elementu × sekce/stránky); domov `Rendering/` vedle CellRendereru.
- ✅ **R2 model potvrzen** (= dnešní architektura, nic se nemění); zbývá jen návrh partial-fit
  rozhraní („vykreslil jsem řádky 1–40, zbývá 12, pokračuj odtud") před Fází C+D.
- ✅ **R3 upřesněno:** základ kontraktu už existuje (`cache-api.md` + `01-wire-formats.md`);
  doplnit (a) prezentační nápovědy v1 (sémantické: `ZVYRAZNENI` 0–5, typ řádku,
  opakuj-na-další-straně, přes-celou-šířku; `FORE_COLOR`/`BACK_COLOR` legalizovat jen pro
  legacy paritu) + (b) pravidlo jen-aditivní evoluce kontraktu. Do `cache-api.md` před F2.

## 2026-07-02 - DynamicTableElement: samostatná dynamická tabulka + 2-tab editor

- ✅ **Rozdělení tabulek** - dynamická tabulka (řádky z `DataSource` kolekce) je samostatný
  element `DynamicTableElement`; statická `TableElement` **uspána** (`JsonDerivedType` zůstává
  pro načtení legacy šablon, z palety/menu odstraněna). Ztráta zpětné kompatibility akceptována.
- ✅ **`DynamicTableEditorDialog`** (2-tab, konzistentní se StopList/DepartureGrid): tab Obecné =
  EDInputForm (`ALVA.TemplateDesigner.EDInputForm.DynamicTableElementEdit`) + tab Sloupce
  (`ColumnListEditor` + `CellStyleEditor`, Add/Remove, mapování `Field`). Dvojklik na element
  otevře dialog rovnou (jako StopList).
- ✅ **Auto-scale do pevné Height** (jako JŘ elementy) + auto-distribuce šířky sloupců (`Width≤0`
  = rozdělí zbývající šířku); `BuildColumnsFromFields` helper; placeholder u prázdné tabulky.
- ✅ **Zdroj dat = EDInputForm List** plněný klientsky (kolekce agendy); seed sloupců
  **centralizovaný v dialogu** (undoable přes příkaz) + seed při otevření. Reapply výběru po
  re-Initu (EDInputForm auto-Init na `Loaded` = re-Init při návratu na tab). **Nemíchat
  EDInputForm + nativní WPF** - transient stav držet stranou (memory / temp global).
- ✅ **`ZastavkoveJrAd.GetSchema` synced 1:1 s reálnými `Build*` daty** - kolekce
  ZASTAVKY/SKUPINY_DNU/ODJEZDY/POZNAMKY doplněny o skutečná skalární pole (PORADI, JE_AKTUALNI,
  ODSAZENI, ZAKLADNI_TRASA, ZKRACENE_OZNACENI_LINKY, KATEGORIE, FORE/BACK_COLOR); phantom
  NADPIS2 odstraněn. (Vnořená pole ZONY/PRUBEHY/ATRIBUTY/… zůstávají doménou StopListu.)

## 2026-07-02 - Architektonická rozhodnutí R1–R7 pro škálování agend (nový doc)

- ✅ **`architektonicka-rozhodnuti-agendy.md`** - plain-text zápis 7 rozhodnutí, která drží
  model „stabilní tiskový engine + postupně přidávané agendy": R1 sdílené tabulkové jádro
  (gate u LineTableElementu), R2 rozdělení layout zodpovědností engine×element, R3 prezentační
  nápovědy v datovém kontraktu + aditivní evoluce, R4 kde běží headless tisk + PrintEngine
  fasáda, R5 fonty embedded v aplikaci (Timetable = sémantika), R6 systémové šablony s release
  (ne DB → žádná verzní matice), R7 pilot-first. Každé: co znamená / jaký problém řeší /
  co se rozhoduje / doporučení / nejpozději kdy. Úvodní slovník „co je engine, co je agenda".
- Roadmap: ⚠️ gate odkazy na R1/R2/R5 (Fáze C+D), R4/R7 (blok A), R6 (blok B), R3 (Fáze F);
  CLAUDE.md reference („číst před startem každé nové fáze").

## 2026-07-02 - T1b (část 1): Caché testy - SablonyTest + Mhd helpery (8 testů, pre-push)

- ✅ **Nový package `ALVA.TemplateDesigner.Tests`** (`CACHE/ALVA/TemplateDesigner/Tests/`,
  extends `ALVA.EDTesting.MLTestCase`) - přidán do default listu `tools/run-cache-tests.ps1`
  → pre-push hook kryje i TD Caché stranu. Celá Caché sada: 23/23.
- ✅ **`SablonyTest`** (5 testů) - CRUD primárního úložiště šablon, vzor „round-trip nad
  vlastními daty": izolovaná IdFa větev `EDTESTING` globálu (žádná závislost na živých
  firemních datech, cleanup v OnBeforeOneTest). Save→Load byte-identický blob + metadata,
  Delete + prázdný List + Load smazané, update zachová CreatedAt + přeindexuje ByAgenda,
  Rename, validace povinných polí (nevalidní Save nic nezapíše).
- ✅ **`ZastavkoveJrMhdHelperyTest`** (3 testy) - deterministický `ConvertSpecialniZnak`
  (dny 1–7, ASCII >31: 72→H/70→F/120→x/123→{, ≤31 beze změny). Mhd třída - mimo AD WIP.
- **Poznatek o kontraktu (zdokumentován v testu):** Sablony metody vrací `$$$OK` i po chybě -
  `$$$CatchErrRest` plní `ret`, který zvedá až MLRest HTTP vrstva; in-process se chyba
  detekuje přes observable stav (chybějící OUT klíče, nezměněný globál).
- **Kompilace:** root hook `compile-objectscript.sh` (Atelier PUT + compile) - v TD session
  volán ručně (hook je vázaný na root project dir). MCP `cache_compile` hledá zdroje pod
  `C:\DEV\EDISON` → pro TD soubory nepoužitelný (pozor: hlásí „up-to-date" i bez uploadu).
- **Část 2 (tentýž den - AD engine je commitnutý, WIP blocker pominul): +17 testů → 25/25
  (celá Caché sada 40/40):**
  - `ZastavkoveJrAdHelperyTest` (12) - čisté helpery se syntetickými vstupy: `ZarazeniSpoje`
    (FTL 0/1/2/3 vč. prázdninového dělení dle NZ konfigurace), `BitmapNaSkupiny`, `CasHodnota` +
    `CasJizdyZMapy` (W-mapa, čekání, projíždí), `PrefixOmezeni`/`FormatDatumyOmezeni`/`UpravDatum`
    (1:1 txtNZ/UprDat), `JeViditelnyAtribut`/`TextAtributu`, `GetZnk` (zdvojení po vyčerpání),
    `ZiskejMarker` (dedup), `NormalizLinku`.
  - `ZastavkoveJrAdSmokeTest` (3) - strukturální smoke `GetData` nad živými daty: linka hledaná
    dynamicky přes `PrvniLinka` (žádná hardcoded gold linka → přežije editace dat), asserty na
    strukturu wire formátu (values/rows, ZASTAVKY/SKUPINY_DNU neprázdné, HODINA/MINUTA rozsahy,
    vazby odjezdů); chybové cesty (bez LINKA_ID, neexistující linka) → žádné stránky.
  - **Nález zdokumentovaný testem:** `ZarazeniSpoje` s PRÁZDNOU NZ konfigurací řadí pracovní
    spoj jen do sloupce „mimo prázdniny" (prázdné cnz == prázdné nzftl1) - gold-ověřené chování
    (0320144, AD-N15: `MHD_NZ1_NZ2` v praxi prázdné), ne bug.
- **Zbývá z T1b:** už jen `tools/compare-gold.ps1` (1:1 vs legacy jako skript mimo hook).

## 2026-07-02 - T2: Async export PDF/PNG nad snapshotem šablony (UI zůstává plně použitelné)

- ✅ **Export PDF/PNG neblokuje UI (R1) a uživatel může dál editovat** - dřív sync fetch +
  render na UI threadu (~9 s „Not Responding" u 600 stránek). Nově orchestrace ve VM
  (`ExportPreviewPdfAsync`/`PngAsync` přes `CommandAsync`): fetch (`BuildAllPreviewDataPagesAsync`,
  `Task.Run` + `IProgress<string>`) i render na threadpoolu.
- ✅ **Snapshot šablony** - `TemplateSerializer.DeepClone` (JSON round-trip, bez razítkování
  ModifiedAt, bez migratoru) při kliknutí na export; `PreviewCanvas.RenderMultiDataToPdf/Png`
  dostaly `Template?` parametr → renderují snapshot, ne živý stav. **Editace během exportu
  výstup neovlivní** (exportuje se stav v okamžiku kliknutí). Žádný blokující overlay -
  jen `_exportRunning` guard (canExecute obou export commandů) proti souběžnému exportu.
  Pokryto testem (`DeepClone_JeNezavislaKopieANemeniModifiedAt`).
- ✅ **Progress ve status baru ve dvou fázích** - „Načítám data X/N: {instance}…" (per instance
  při fetchi) → „Generuji PDF: X/N datových stránek…" / „Generuji PNG: X/N stránek…"
  (`IProgress<(done,total)>` z render smyčky).
- ✅ **První řez god-object úklidu (T4)** - export logika z code-behind do VM; view wiring
  = 2 callbacky (`RenderPdfToFile`/`RenderPngToFile`, nahrazují `RequestExportPdf/Png`);
  `OpenExportedFile` (Process.Start) ve VM - bez WPF, R4 OK.
- Známý krajní případ: ambientní `DocumentDefaults.Style` se při renderu nastavuje ze snapshot
  šablony, ale je procesně-globální - změna *výchozího fontu šablony* přesně během běžícího
  exportu by mohla prosáknout do části stránek. Ostatní editace jsou plně izolované snapshotem.
  Follow-up mimo scope: první `GetSchema` hit v `RefreshFieldsList` zůstává sync (mitigováno cache).

## 2026-07-02 - Roadmap: merge akčních položek z analýzy legacy náhrady

- ✅ **Akční položky z `legacy-nahrada.md` zamergovány do `roadmap.md`**
  (jedno místo pravdy, analýza zůstává jako evergreen referenční detail se status poznámkou):
  - Nová sekce **„Architektonický track T1–T6"** (T1a testy ✅, T3 ✅, T2 async export ⬅️ další,
    T1b Caché testy, T4 strangler, T5 duplikace, T6 drobnosti) + 3. aktivní track nahoře.
  - **Blok A** doplněn o legacy vzory (RG headless CLI AUTOEXPORT/AUTOPRINT pro A3) + **A4**
    (merge PDF + e-mail - společný požadavek všech 3 legacy systémů).
  - **Blok B** doplněn o legacy „MlineSestava" vzor + **B4** (per-instance přiřazení šablony,
    linka → šablona A4/A5) + **B5** (audit log + ECL právo na designer).
  - **Fáze C + D sloučeny** (legacy TiskJR generuje vývěsní i knižní z téhož enginu; oracle
    `mkPLinWin`, prerekvizity: Timetable font → agenda → LineTableElement + layout rozšíření).
  - **Fáze E** rozšířena o Relace (TiskJRRELACE, čeká validace use case).
  - **Nová Fáze F: Náhrada REPORTGENERATOR** (F1 usage statistika, F2 bridge vs čisté agendy,
    F3 TableElement dorovnání, F4 výstupní kanály, F5 jazykové varianty, F6 neportovat bez poptávky).
  - **Otevřené otázky Q1–Q6** (Epson, bridge, Relace, grafy/AZTEC, jazykové varianty, V1 soubory).

## 2026-07-02 - T3: TemplateMigrator guardy (forward-compat + hlášení chyb migrace)

- ✅ **Forward-compat guard (R5)** - šablona s verzí novější než `CurrentVersion` se odmítne
  `TemplateDeserializationException` („aktualizujte TemplateDesigner") místo tichého přerazítkování
  dolů (= ztráta dat novější verze při následném uložení). Chyba doteče uživateli existujícími
  kanály (soubor: `PopupDisplayer.Error`, DB: `CacheTemplateException`) - žádná změna UI.
- ✅ **Selhání migrace = chyba, ne tiché pokračování** - dřívější `Debug.WriteLine + continue`
  („partial migration") nahrazeno výjimkou s popisem migrace; částečně zmigrovaná šablona se
  neotevře (a tedy ani neuloží poškozená).
- ✅ **Sémantické porovnání verzí** - early-out přes `VersionComparer` místo string equality;
  „1.0" == „1.0.0" už nehlásí falešnou migraci.
- ✅ **Testovatelnost pipeline** - `TemplateMigrator(IEnumerable<ITemplateMigration>?)` ctor;
  testy s fake migracemi (transformace, pořadí, skip novějších, propagace chyby). Skip test
  `NovejsiVerze_SeOdmitne` odskipnut → **164 testů, 0 skipů**.
- **H-19 (V1→V2 migrace) - ověřeno v DB:** šablony v `^ALVA.TemplateDesigner.Templates`
  (CB_CSADAD, CB_GWBUS) mají `version: 1.0.0`; DB úložiště (Wave 3, 2026-05-25) vzniklo až po
  TextStyle breaking change (2026-03-24) → V1 v DB neexistují. Zbytkové riziko: staré JSON
  soubory na disku z éry před 2026-03-24 (bez `version` pole = nerozlišitelné od aktuálních);
  pokud se objeví, dopsat migraci + heuristiku detekce flat-TextStyle.

## 2026-07-02 - AD: multi-varianta časů tras (dynamické TrasaMin) - L1

- ✅ **Dynamické variantní časové sloupce ve StopListu** (nahrazuje legacy fixních 15 slotů TrasaMin).
  Agenda `ZastavkoveJrAd` seskupí obsluhující spoje **podle časového profilu W-mapy** (prutmbmp) →
  emituje `TRASA_MIN[]` per zastávka + `VARIANTY_TRAS[]` (hlavičky = písmena rozdílů tras / „ZT").
  N = skutečný počet variant (ne fixních 15, bez capu). StopList rozvine kolonku „Čas" na N sub-sloupců.
- ✅ **1:1 ověřeno proti gold** (`ALVA.TISKMHD.Data`, 0365005): 6 variant B,C,E,F,G,H, časy + `<`
  (projíždí) / prázdno (konec varianty) přesně jako legacy. Grupování 12 `cisloTrasy` → 6 profilů.
- ✅ **Přepínač `ShowAllRouteVariants`** (default zap) - vyp = jeden sloupec = výchozí trasa
  (`ZakladniCisloTrasy`, nejvíc spojů). V panelu i v Caché schématu `StopListElementEdit`.
- ✅ **Fix `CacheAgendaProvider`** - page-level pole/objekty (VARIANTY_TRAS) se zachovají jako JsonNode
  (dřív stringifikovány → zahozeny). Řádkové pole (ZONY…) fungovaly už dřív.
- **Ověřeno 1:1 s legacy i koncepčně:** „2 báze" (nejvíc-spojů pro časy / dta12 `W_POZ` pro odchylky-
  poznámky) odpovídá starému `JRapiLinPrtMhd` (ř.108-127 vs `OmTrasa` VzorTr). Zbývá: šířka variantních
  sloupců je zatím pevná (`VariantColumnWidthMm`), jemnější výběr variant (per-písmeno) = budoucí v2b.

## 2026-07-02 - TemplateDesigner.Tests: první sada automatizovaných testů (krok 0–1 arch. tracku)

- ✅ **Nový projekt `TemplateDesigner.Tests/`** (xUnit, zrcadlo EDControls.Tests) přidán do
  `EDISON2.sln` → pre-push hook TD od teď kryje automaticky. **104 testů (103 ✅ / 1 skip)**:
  - `Layout/PageLayoutEngineTests` (15) - stránkování, KeepTogether/SplitAcrossPages + ClipTop/Bottom,
    PrintBehavior matice (OnEveryPage/OnFirstPageOnly/OnLastPageOnly dvouprůchod/ExceptFirst),
    patička od spodu, PAGE/TOTAL_PAGES injekce, PerDataRow expanze, VisibilityExpression,
    dynamická výška elementu, propagace CultureCode. `FixedHeightCalculator` test double (bez SkiaSharp).
  - `Expressions/*Tests` (~60) - ExpressionEvaluator (porovnání numericky vs. lexikograficky,
    logické operátory, truthiness, fail-open, Validate), PlaceholderFormatter (string funkce,
    N2 cs-CZ×en-US, datum, TimeSpan, fail-safe), AggregationResolver (SUM/COUNT/AVG/MIN/MAX, prázdné řádky).
  - `Serialization/TemplateSerializerRoundTripTests` - round-trip idempotence pro všech 9 typů
    elementů (Serialize→Deserialize→Serialize, `modifiedAt` neutralizován - Serialize razítkuje UtcNow),
    tolerance neznámé property, RoundedFloatConverter, legacy „MHD"→„ZASTAVKOVY_JR_MHD" remap.
  - `Serialization/TemplateMigratorTests` - charakterizace současného chování + 1 Skip test
    (žádoucí forward-compat guard, odskipovat s T3 opravou).
- **Nálezy z psaní testů:** (a) early-out v `TemplateMigrator.Migrate` porovnává verze string
  equality, ne VersionComparerem → „1.0" se hlásí jako migrovaná (neškodné, zvážit při T3);
  (b) `dotnet sln add` vygeneroval kolizní solution folder „TemplateDesigner" → ručně odstraněn ze sln.
- Kontext + celý plán: [`legacy-nahrada.md`](legacy-nahrada.md)
  (technický audit T1–T6 + legacy REPORTGENERATOR/TiskMHD/TiskJR + roadmapa náhrady).
- **Druhá vlna (tentýž den): TextStyle kaskáda + helpery → celkem 150 testů (149 ✅ / 1 skip):**
  - `Models/TextStyleTests` - CSS-like cascade (dědění, override, 3vrstvá kaskáda, trim fontu,
    idempotence, IsEmpty, Clone); `Models/DocumentDefaultsTests` - dokumentový default (overlay,
    prázdný font = no-override, propagace do Resolve), vždy s finally reset globálního stavu.
  - `Models/DepartureGridCascadeTests` - **regresní testy na anti-pattern „scalar přebíjí kaskádu"**
    (2× opravený bug, mvvm-patterns.md): `ResolveGroupMinuteParent` (MinuteFontSize/MinuteBold jen
    když MinuteStyle+DataStyle mlčí), `ResolveGroupHeaderParent` (SkupinaDnuHeaderLineHeight fallback,
    VAlign), padding 0 u minut, HourData dědění.
  - `Models/MmMathTests` (Round 0.1mm AwayFromZero, nullable, Snap), `Rendering/ColumnHeaderTests`
    (override vs. zdroj), `Elements/TextWrapHelperTests` (headless SkiaSharp - normalizace \r\n,
    extrémní šířky, prázdné řádky; bez assertů na pixelové metriky).
  - Testy stylů sdílejí xUnit kolekci `StyleCascade` - `DocumentDefaults.Style` je procesně-globální
    stav, kolekce vynucuje sekvenční běh (žádné vzájemné ovlivnění).
- **Třetí vlna (tentýž den): driftová pojistka EDInputForm schémat → celkem 161 testů (160 ✅ / 1 skip):**
  - `Serialization/EDInputFormSchemaDriftTests` - parsuje `CACHE/.../EDInputForm/*Edit.cls` přímo
    z repa (offline, bez serveru), vytáhne field keys (= property paths pro ElementFormBinder
    reflexi) a reflexí ověří existenci na cílovém C# typu. Mapa 10 schémat (8 elementů + Section
    + Page/Template). `StyleFields.Add` bloky expanduje podle suffixů čtených ze `StyleFields.cls`
    (self-updating). Virtuální klíče: `Format`/`Orientation` (PageEdit), `style:<prefix>` tlačítka
    pod-formuláře → validuje se path za prefixem. Uzavírá driftové riziko Wave 4 duplikace
    (memory `project_edinputform_schema_duplication.md`) - přejmenování property bez úpravy .cls
    od teď zablokuje push.

## 2026-07-02 - Startovní agenda z inputParam kontraktu

- ✅ **`inputParam` "Agenda=<kód>" → startovní agenda šablony.** `App.OnStartup` vytáhne kód
  z `EdisonLaunchArgs.InputParam`, `TemplateDesignerViewModel` ho ověří proti registru agend
  (case-insensitive) a nová šablona startuje rovnou s ní. Zdroj: atribut `input` uzlu stromu
  legacy shellu (JŘ uzel = `Agenda=ZASTAVKOVY_JR_AD`, MHD uzel = `Agenda=ZASTAVKOVY_JR_MHD`)
  a MHD2 tlačítko „Linky a oběhy". Neznámý/chybějící kód → dosavadní default.

## 2026-07-02 - Převzetí session shellu přes EDISONMAIN2 launch kontrakt

- ✅ **`App.OnStartup` parsuje EDISONMAIN2 argumentový kontrakt** (`EDControls.EdisonLaunchArgs`,
  zrcadlí `EDISONMAIN2API.FormToRunGetter`): sentinel `EDISONMAIN2` + server, restPort, namespace,
  idfa, login, fullName, mlp [, formName, shellGuid, inputParam]. Při spuštění ze shellu (starý
  EDISON přes wrapper formu `ALVA.TISKJR.formTISKJRTemplateDesigner`, MHD2 „Linky a oběhy") se
  MLRest staví ze session shellu (`CacheSchemaWiring.CreateMlRest`) místo z appsettings - TD běží
  pod správnou firmou a uživatelem.
- Bez argů beze změny (standalone z appsettings). Sentinel s nevalidními argy → varování + fallback
  standalone. `shellGuid` se záměrně nepřebírá - TD má vlastní session GUID, neváže se na ACTIVITY
  zámek shellu.

## 2026-06-30 - Ověření: pořadí poznámek v legendě 1:1 s legacy (živý gold oracle)

- ✅ **Default řazení legendy potvrzeno 1:1** proti gold oracle (`ALVA.TISKMHD.Data`, VOLANO_Z=JRMAIN)
  na CB_CSADAD lince **0365005** (page 1, Smrkovice → Dobešice). Pořadí **bezbariér → rozdíly tras
  (B,C,E,F,G,H) → omezení (29,68)** se shoduje včetně sub-pořadí a textů. `NotesLegendElement.Razeni
  = Vychozi` zachová Caché emit order → **žádný reorder není potřeba**.
- Vyvrací dřívější (retraktovanou) domněnku o nutném reorderu „typ1→2→3" - ta by pořadí naopak
  rozbila. **Neprověřeno na této lince:** pozice `atributy-zastavky` / `ruční-komentáře` / `směrové`
  vůči těmto třem (na stránce nebyly); emit order je dle kódu atributy → bezbariér → rozdíly →
  komentáře → omezení → směrové.

## 2026-06-30 - Font combobox plošně (sjednocení výběru fontu)

- ✅ **Jednotný font picker všude** - editovatelný ComboBox s nainstalovanými fonty (`SKFontManager`
  přes `FontResolver`) napříč všemi místy výběru fontu, ne jen v PageEdit:
  - `ElementFormBinder.PopulateFontFields` (sdílené, přesun z ElementEditDialog) - voláno ze **3 hostů**
    EDInputformu: `ElementEditDialog` + embedded form v `StopColumnsEditorDialog` / `DepartureGroupEditorDialog`.
  - `TextElementEdit.cls`: font List se 6 natvrdo hodnotami → **ComboBox** (všechny nainstalované,
    editovatelný, volitelný → prázdné dědí dokumentový default).
  - `StyleFields.cls`: font `AddText` → **ComboBox** (propíše se do všech tabulkových/JŘ style zón).
  - `CellStyleEditor` (per-column C#): font TextBox → **editovatelný ComboBox** (`FontFamilyOptions` DP,
    default `FontResolver.InstalledFamilies`); tri-state placeholder „dědit" zachován.
- Filtrování/omezení na podmnožinu fontů půjde později (změna jednoho seznamu). Varování „font není
  nainstalovaný" stále odložené (Arial fallback to mitiguje).

## 2026-06-30 - Výchozí font na úrovni šablony (dokumentový default + font combobox)

- ✅ **`Template.DefaultTextStyle: TextStyle?`** - dokumentový výchozí styl (UI zatím jen font, model
  rozšiřitelný o velikost/barvu). Kořen kaskády: elementy bez vlastního fontu ho zdědí.
- ✅ **Ambient `DocumentDefaults`** - `Apply()` overlayuje výchozí styl na zapečené dno kaskády
  (`ResolvedTextStyle.Default`, `CellDefaults.Default` + schedule/table rodiče převedeni na gettery;
  ze schedule rodičů odstraněn natvrdo `FontFamily="Arial"`; `CreateDefault` už font nenastavuje).
  Sync přes `PreviewCanvas.Layout()` wrapper (chokepoint před každým layoutem - pokrývá preview,
  PDF/PNG export, tisk i měření přetečení) + `DesignerCanvas.Render` + `RebuildSections`.
- ✅ **UI font combobox** - `PageEdit.cls` font pole = editovatelný ComboBox; klient ho po načtení
  schématu naplní nainstalovanými fonty z `SKFontManager` (`ElementEditDialog.OnFormInitialized`).
- ✅ **`FontResolver`** - jediný zdroj fontů (`SKFontManager`) + Arial fallback: neznámý/nenainstalovaný
  font → **Arial** (≈ Helvetica), ne náhodný systémový default. Vstup shovívavý (trim mezer, prázdné=default).
- 🐛 **Fix `ElementPropertyAccessor.Exists`** - traverzace podle typu (ne hodnoty) → zápis přes EDInputForm
  s null mezičlánkem (`DefaultTextStyle.FontFamily`) se už nezahazuje. (Týkalo se obecně vnořených cest.)
- 🐛 **Fix měření↔glyf asymetrie** - měření šířky v `DepartureGridElement`/`StopListElement` používá
  resolved font (vč. dokumentového defaultu), ne raw → sloupce se neořezávají při custom fontu.

## 2026-06-30 - Databáze jako primární úložiště šablon (JSON = import/export)

- ✅ **Sjednocení modelu ukládání** - Otevřít (Ctrl+O) / Uložit (Ctrl+S) / Uložit jako míří do
  **databáze**. Soubor degradován na **Importovat z JSON** / **Exportovat do JSON** (export nemění
  původ - kopie ven, obdoba PDF/PNG). Odstraněny duplicitní menu položky „Otevřít/Uložit do databáze".
- ✅ **Titulek ukazuje původ** `[DB]` / `[neuloženo]` (kam míří Uložit). Nová/importovaná šablona →
  Ctrl+S ji založí do DB (SaveAs formulář s jménem); existující → tichý update (bez confirm).
- **Pohlcuje S2** (asymetrie file/DB původu) - export už původ nemění, problém zaniká. Správce
  „Nástroje → Šablony v databázi" beze změny. Versioning prerekvizita (R5) splněna `TemplateMigrator`em.

## 2026-06-24 - Evidence nových zákaznických požadavků z prezentací (sekce P)

- 📝 **Zaznamenány 3 nové požadavky** z živých prezentací 2026-06-24 do
  `customer-requirements-zastavkovy-jr.md §P` (evidence, neanalyzováno; před implementací založit ticket):
  **P1** ruční poznámky do patičky, **P2** sloučení linek se stejným posledním trojčíslím (i různá Tč /
  více dopravců) do jednoho výstupu, **P3** logická imposice 2×A5 oboustranně na A4.

## 2026-06-23 - AD: datumová omezení jako negativní značky (cnz) + drobné opravy

- ✅ **Číslování omezení = číslo NZ (`cnz`) invertovaně** (1:1 legacy). Marker datumového omezení je
  nově `cnz` spoje (`$p(spoj,"#",7)`) renderovaný jako typ `nz` (invertovaný box), ne sekvenční „1".
  Text dál z per-spoj `SestavTextOmezeni`. Dedup per-cnz. `cnz` prázdné → fallback na staré sekvenční.
  Ověřeno gold oracle (`ALVA.TISKMHD.Data`) na **obou** firmách (CB_CSADAD „29", KV_DP 28/30/31/40).
  **Korekce dřívějších domněnek:** žádný firemní TYPPOZ gate (`MHDnewOM` prázdné u obou firem),
  KV_DP nebyl 1:1 ani předtím, text jde z per-spoj (ne ze zastaralého InPlatDef seedu). Detail §10.
- ✅ **Toggle `InvertovatNegativniZnacky` odstraněn** z `DepartureGridElement` - inverze NZ je teď
  napevno vždy zapnutá (AD i MHD, jako legacy); barvy `NegativniZnacka{Back,Fore}Color` ponechány.
- 🐛 **Drag DB pole nemělo výchozí rámeček** - `AddElementAtPosition` tvořil holý `TextElement` mimo
  `CreateElementByType`. Nově jde přes `CreateElementByType(Text)` → stejné defaulty (rámeček, rozměry)
  jako element z palety.

## 2026-06-23 - AD: konfigurovatelné rozdělení sloupců (typy provozu / FTL override)

- ✅ **Nový parametr „Rozdělení sloupců (typy provozu)"** v AD agendě - override FTL (default
  „Automaticky (dle linky)" = `MHD_TYP_R`) se 4 módy pokrývajícími FTL0-3 (= kombinace
  prázdninový split prac. dnů × sloučení víkendu). **Reuse existujícího `NacistSkupinyDnu` +
  `ZarazeniSpoje`** (engine beze změny, jen `$case` override `ftl`). Uživatel si tak může zvolit
  rozdělení/sloučení sloupců místo napevno dle dat linky.
- ✅ **Server-side param flow** - `RefreshDataForCurrentInstanceAsync` posílá print params do
  `GetData` (dřív jen instanci); změna server-paramu spustí **re-fetch** dat z Caché (client-side
  params jen překreslí, beze změny perf).
- **Datově:** víkend merge/split jede z Plk dat (funkční vždy); prázdninový split potřebuje NZ
  konfig (`MHD_NZ1_NZ2`, dormant ve všech firmách) → sloupec „o prázdninách" prázdný (očekávané).
- **Follow-up:** (1) generalizovat „server-side" flag parametru (zatím hardcoded
  `ROZDELENI_SLOUPCU` v C# re-fetch checku); (2) poznámka „jede v neděli" chybí u jednoho spoje
  0365005 (date-based omezení vs dateless denní note) - k dohledání; (3) jemné custom rozdělení
  (libovolné dny → sloupec) + multi-varianta tras ve StopListu = větší redesign.

## 2026-06-23 - DepartureGrid: adaptivní rozteč minut s anotacemi

- ✅ **Minuta s anotacemi se už nepřekrývá s další minutou** - slot odjezdu v `DrawMinuteCell` je
  nově adaptivní: nominální `scaledMinuteW` + naměřená šířka anotací (zachová nominální mezeru).
  Blok minut se zarovnává ze součtu adaptivních šířek (správný center/right align). Dřív se
  posouvalo o pevnou `MinuteWidth` → husté buňky s poznámkami se slévaly. Šířka sloupce
  (ComputeLayout) zatím beze změny - s redistribucí na šířku elementu bývá slack; měření šířky
  sloupce dle anotací = follow-up, pokud by dense buňka přetékala do sousedního sloupce.

## 2026-06-23 - NotesLegend: zalamování dlouhých poznámek

- ✅ **NotesLegend wrapuje text poznámek** na šířku sloupce (`DrawWrappedBody` přes `TextWrapHelper`)
  - dlouhá poznámka už nepřetéká doprava, ale zalomí se na další řádky (visutý odsazený blok pod
  tělem). Layout přešel z pevné mřížky řádků na **akumulaci Y per sloupec** (víceřádková poznámka
  posune další dolů). `CalculateHeightFromData` zrcadlí (výška = nejvyšší sloupec dle zalomených
  řádků → auto-scale u pevné výšky). Wrap je vždy zapnutý (overflow nikdy nechceme).

## 2026-06-23 - AD StopList: projeté zastávky + oprava dvojitého „na znamení" + názvy sloupců

- ✅ **AD: tisk projetých zastávek** (`ZastavkoveJrAd.BuildStrankaZastavky`) - agenda nyní emituje
  **celou trasu** ve směru (vč. projetých, analogicky MHD): `JE_AKTUALNI` na aktuální, projeté mají
  `casJizdy=0` a bez atributů. StopList je defaultně skryje (`ShowPassedStops=false` → výchozí výstup
  1:1 zachován); „tisk projetých zastávek" je zobrazí ztlumeně. Dřív AD emitovalo jen úsek od aktuální
  → toggle byl inertní (na rozdíl od MHD).
- ✅ **AD: oprava dvojitého „na znamení" (× x)** - agenda nastavovala `NA_ZNAMENI=true` (→ syntetický
  `SignalStopSymbol` „×") **i** přidávala atribut „x" → znak se tiskl dvakrát. Nyní `NaZnameni=0`,
  „na znamení" jde jen přes atribut „x" (1:1 legacy `UVOD^JRapiLinPrtMhd`).
- ✅ **Editor sloupců StopListu: čitelné názvy** - `StopColumnVM.DisplayName` odvozen z `Key`
  („Tarifní číslo (TČ)", „Základní trasa (ZT)", „Čas jízdy", „Průběhy trasy"…); tištěná zkratka
  v poli „Nadpis" zůstává. Uživatel už nemusí luštit „TČ"/„ZT".

## 2026-06-23 - Genuine page-overflow detekce (dokončení P0-2)

- ✅ **`PreviewCanvas.PageOverflows`: detekce sekce vyšší než stránka** - u `KeepTogether` sekcí
  (nelze rozdělit) hlásí přetečení, když `placed.Y + placed.Height > PrintableHeight` → obsah pod
  spodní hranou tiskové plochy se ořízne. Doplňuje dnešní #8 fix: ten odstranil false-positive u
  dělitelných sekcí, ale tím se ukázalo, že element-vs-sekce kontrola fakticky nikdy nenaskočí
  (sekce se sama roztáhne na obsah) → varování bylo dormantní. Teď chytá reálný případ.
- **Ověřeno live:** KeepTogether 400 mm → červená; přepnutí na SplitAcrossPages → bez červené
  (fix #8 drží); KeepTogether 100 mm + čistá šablona → bez false-positive.

## 2026-06-23 - Negativní značky invertovaně v mřížce odjezdů (legacy shoda)

- ✅ **DepartureGrid: NZ u minuty invertovaně** - negativní značka (`TypAnotace.NegativniZnacka`) se
  v mřížce kreslí jako **černý box + bílý znak**, jako legacy (`formTISKMHDReportDotNet_Module.vb`
  `FillRectangle(Color.Black)` + bílý text). Dřív se kreslila plain. Tři nové property:
  `InvertovatNegativniZnacky` (default zap), `NegativniZnackaBackColor` (černá), `NegativniZnackaForeColor`
  (bílá) - editovatelné v panelu (skupina „Anotace - symboly"). Backward-compat: staré šablony dostanou
  invert default = legacy shoda.
- **Ověřeno** na MHD agendě (mock NZ „22" v 6:45 → invertované). **AD záměrně bez NZ u minut** -
  v JRMAIN je CNZ jen klasifikátor sloupců dnů (omezení = textové anotace), ověřeno 1:1 proti gold
  oracle; přidání by AD od legacy odchýlilo.

## 2026-06-22 - Stylový pod-formulář přes CellStyleEditor (Fáze 2 - prototyp, 1 zóna)

- ✅ **`StyleEditDialog` + `StyleEditDialogViewModel`** - znovupoužitelný pod-formulář pro editaci
  JEDNOHO TextStyle (zóny) libovolného elementu přes sdílený **`CellStyleEditor`** (tri-state →
  zděděné hodnoty jako **šedý placeholder**, rozlišuje dědí/explicit, reset). VM čte/zapisuje styl
  reflexí dle prefixu; OnOk → `SetPropertyCommand` (undo). **Nikdy nezapíše null** (nenullovatelný
  HeaderStyle by spadl) - prázdná zóna = nový prázdný TextStyle / žádný command.
- ✅ **EDInputForm `ButtonClicked` event** (EDControls, aditivní) - klik na BUTTON pole dá hostu klíč
  → client-side akce (otevřít pod-formulář). Doplňuje server-side Validate.
- ✅ **Odjezdy „Styl záhlaví" → tlačítka** - HeaderStyle + 2. řádek balíčky (~30 polí) nahrazeny
  tlačítky „Upravit styl…" → pod-formulář. Hlavní „Obecné" se odlehčí. Commandy keyované prefixem
  (opakované otevření zóny nahradí) → aplikují se v parent OK jako jeden undo krok.
- **Prototyp:** jen zóna Styl záhlaví u Odjezdů; ostatní zóny + StopList zatím inline. `StyleEdit.cls`
  (Caché varianta A) ponechán jako nepoužitý server-side fallback. **Dál:** rozšířit na všechny zóny
  + StopList, pak uklidit. Detaily v roadmap (Fáze 2 - stylový pod-formulář).

## 2026-06-22 - Odjezdy „Obecné" → EDInputForm (Fáze 1, krok 4 - C# rewire) ✅ Fáze 1 hotová

- ✅ **Tab „Obecné" dialogu Odjezdů → `<eif:EDInputForm>`** (schéma `DepartureGridElementEdit`) místo
  ručních CellStyleEditorů + PropertiesPanel. Flow změněn z **live** (PropertiesPanel + executeCommand
  + undo na cancel) na **batch na OK** (jako StopList): code-behind `OkButton_Click` → `GeneralForm.Save()`
  → `ElementFormBinder.ExtractCommands` → `GeneralCommand`. Cancel = nic se neaplikuje.
- ✅ **VM odlehčen** - odebrány `ElementHeaderStyle`/`ElementDataStyle` + 9 element-level commandů +
  `PickElementColor` + `GeneralDescriptors` + ctor param; OnOk už nezapisuje HeaderStyle/DataStyle.
- ✅ **`ElementPropertyAccessor.Set` lazy-alokuje null mezičlánky** na cestě (generické) - pro nullable
  zóny jako `HeaderLine2Style`; jen při zápisu (Get/Exists nealokují → Cancel element nemutuje).
- ✅ **MainWindow** - undoDepth/cancel-undo zahozen; OK aplikuje GeneralCommand + GroupColumns +
  HourColumnStyle jako **jeden `CompositeCommand`** (single undo, jako StopList).
- Fix: `HourColumnWidth` jen v tabu Sloupce (řádek Hodiny) - odebráno z Obecné, ať nekoliduje (dvojí zdroj).
- **Fáze 1 (sjednocení tabulkových editorů) HOTOVÁ:** Zastávky i Odjezdy mají kostru Obecné(EDInputForm) /
  Styl záhlaví / Styl datových buněk / [element]-specifické. Známé low: hour header/width direct-write
  není undo; lazy-alloc po undo nechá prázdný `HeaderLine2Style`. **Navazuje:** extrahovat stylový balíček
  do pod-formuláře (StyleEdit + StyleEditDialog) - odlehčí oba editory (5 zón × 15 polí inline).

## 2026-06-22 - EDInputForm 4-skupinová kostra + sdílený StyleFields helper (Fáze 1, krok 4 - Caché část)

- ✅ **Sdílený `ALVA.TemplateDesigner.EDInputForm.StyleFields.Add`** - jediný zdroj „balíčku" polí
  stylu textu (Font/Velikost/Tučné/Kurzíva/Podtržení/Přeškrtnutí/Zarovnání H+V/Barvy/Padding/Řádkování)
  pro daný property-path prefix. Volitelně append do poslední skupiny + prefix labelů (víc zón
  v jedné skupině). Nové pole stylu stačí přidat zde → propíše se do všech zón i elementů.
- ✅ **`StopListElementEdit` přeskupeno** do 4-skupinové kostry: **Obecné · Styl záhlaví · Styl
  datových buněk · [Element]-specifické** (+ Pozice/Podmínky). Stylové skupiny přes helper.
  Stejné property-path klíče → chování zachované (jen seskupení); „Řádky" sloučeno do Obecné,
  „Zastávky/Směr/Zvýraznění" do „Zastávky - specifické".
- ✅ **`DepartureGridElementEdit`** - nové schéma ve stejné kostře (Obecné / Styl záhlaví +2.řádek /
  Styl datových buněk = minuty / Odjezdy - specifické = anotace symboly+text přes prefix labelů).
  `HeaderStyle.VAlign` primární (HeaderVerticalAlignment fallback), `TimeAxisMode` = enum názvy.
  **Zatím nenapojené** na dialog - čeká na C# rewire (lazy HeaderLine2 alloc, collapse legacy
  scalárů, SetHeaderFontSize cleanup v Init/OnOk).
- Standard: „Obecné = EDInputForm" pro jednoduché elementy; TableElement (DataGrid) je výjimka.

## 2026-06-22 - Auto + override šířka sloupců (Fáze 1, krok 2b)

- ✅ **StopList - nullable šířka (`StopColumn.Width` → `float?`)** - prázdné = **auto** (rovnoměrně
  rozdělí zbývající šířku elementu po pevných sloupcích), hodnota = pevná. Jediné místo:
  `StopListElement.ResolveColumnWidths` (Dictionary<StopColumn,float>); napojeno do layoutu
  (pozice šipky, BuildDataRowSpecs/BuildHeaderRowSpecs/RenderColumnHeaders), ApplyScale (škáluje
  jen pevné). Editor: `WidthText` (prázdné = auto), fit-to-width + overflow ošetřeno na null.
  **Default `Width = null`** (ne 10f) - jinak by se auto po uložení ztratilo (serializer WhenWritingNull).
- ✅ **DepartureGrid - per-group `WidthOverride`** (float?) - prázdné = auto (z obsahu + proporční
  dofill, jako dosud), hodnota = **pin** (vyloučen z přerozdělení zbytku). Sloupec Hodin edituje
  `HourColumnWidth`. Layout cache hashuje `WidthOverride`.
- ✅ **Sdílený `Controls.ColumnWidthText.Parse/Format`** - locale-robustní (přijímá tečku i čárku,
  InvariantCulture); použito oběma editory (odstraňuje duplikaci + tichý fail při desetinné čárce).
- Oba editory mají detail „Záhlaví + Šířka" se **stejným modelem** (prázdné = auto, hodnota = pevná).
  Existující šablony beze změny (mají šířky vyplněné = pevné). **Zbývá (krok 4):** Odjezdy
  „Obecné" → EDInputForm. **Později (krok 5):** content-aware auto šířka (dle délky dat).

## 2026-06-22 - Override nadpisu sloupce (AD-N6) + sdílený editor sloupců (Fáze 1, krok 1+2a)

- ✅ **Override nadpisu sloupce/skupiny** - `Rendering/ColumnHeader.Resolve(override, source)` = jediné
  místo sémantiky „override přebíjí zdroj, prázdné → zdroj". Napojeny **všechny tabulkové komponenty**:
  StopList + TableElement (zdroj = autorský `Header`) i DepartureGrid (`DepartureGroupColumnStyle.HeaderOverride`
  přebíjí nadpis skupiny dnů z dat). Koncept se neimplementuje per komponenta - liší se jen ČÍM je zdroj.
- ✅ **Sloupec Hodin přepisovatelný** - `DepartureGridElement.HourColumnHeader` (prázdné → „Hod"); render
  přes `ColumnHeader.Resolve`, Clone. Editor: override jde nastavit pro **všechny** sloupce vč. Hodin.
  Uzavírá legacy AD-N6 („Nadpisy sekcí pro rozvržení").
- ✅ **Sdílený seznam sloupců** `Controls/ColumnListEditor` + `Controls/IColumnRowVM`
  (`DisplayName`/`Visible`/`HasAnyOverride`/`ColumnWidth?`) - levý seznam jednotně pro Zastávky i Odjezdy
  (checkbox + název + override-tečka + „X mm" jen u pevné šířky + ▲▼). `StopColumnVM` i
  `DepartureGroupColumnVM` implementují rozhraní (v DepartureGroup přejmenováno `IsVisible`→`Visible`).
- ✅ **Pořadí skupin dle dialogu** - `ResolveSkupiny` (render) i ctor editoru řadí skupiny dle
  `GroupColumns` (dřív datové pořadí) → přeskládání ▲▼ se projeví ve výstupu a přežije reopen dialogu.
- ✅ **Zarovnání jednotně ve stylu** - odebráno duplicitní inline „Zarovnání" combo ze Zastávek
  (HAlign v `CellStyleEditor` mají oba). Detail „Struktura sloupce" sjednocen (Záhlaví; Šířka jen u Zastávek).
- Layout cache DepartureGridu hashuje override + pořadí (`GroupHeaderOverridesHash`, order-sensitive).
  JSON zpětně kompatibilní (chybějící = null = z dat). **Zbývá (krok 2b):** proporční auto šířka v obou
  (sjednotí i poslední rozdíl „X mm"); pak Obecné→EDInputForm u Odjezdů (krok 4).

## 2026-06-22 - Tisk s daty: členění výstupu + název souboru z dat

- ✅ **Členění výstupu** (`__EXPORT_SPLIT`) - při tisku s daty (více instancí zastávka × směr) volba:
  **Vše do jednoho PDF** (default, dnešní chování) / **Jeden PDF na linku** / **Jeden PDF na
  zastávku × směr** (legacy vývěska per zastávka). Single → `SaveFileDialog`; split →
  `OpenFolderDialog` + N souborů do složky přes existující `RenderMultiDataToPdf` (1 volání per
  skupina, žádná nová PreviewCanvas metoda). Seskupení `GroupBy("CISLO_LINKY")` (zachovává pořadí
  z Caché). Bez ZIP (volné soubory ve složce = přímo použitelné pro tisk vývěsek).
- ✅ **Název souboru z dat** (`__EXPORT_FILENAME`) - pattern s `{POLE}` tokeny vyhodnocený nad první
  instancí skupiny přes `DataContext.ResolvePlaceholders` (konzistentní s `{POLE}` všude jinde).
  Default `{CISLO_LINKY}_{POPIS_ZASTAVKA}_{POPIS_SMER}` (= legacy „NázevZastávky_ČísloLinky_…",
  funguje pro AD i MHD). Sanitizace neplatných znaků + kolize → přípona `_2`, `_3`. Pole viditelné
  jen ve split režimu (`PrintDialog.ApplyVisibility`).
- ✅ **Jako EDInputForm pole, ne bespoke WPF** - obě volby přidává **`AgendaParams.cls`** jako
  universální skupinu „Export" (společná všem agendám, NEjsou v `GetParameters` → neukáží se
  v preview liště). Hodnoty tečou přes sdílený `_paramValues` → **persistence per-agenda zadarmo**
  (přes existující `PrintParams`, líp než per-session). PrintDialog se nově staví i pro agendu bez
  `GetParameters` (`HasForm`, ne `HasParameters` - kvůli export skupině).
- **TODO** (roadmap): extrahovat na úroveň `Template`/agenda-config (firemní default jako legacy
  „Tisk → Linky-MHD"). Dorovnává legacy firemní „Název souboru pro export do pdf" + per-linka split.

## 2026-06-21 - Legenda poznámek: layout (sloupce / řazení / zarovnaný marker)

- ✅ **Víc-sloupcová legenda** - `NotesLegendElement.Columns` (1-4) + `WrapSmer` (po sloupcích / po
  řádcích). Render distribuuje poznámky do sloupců, výška = záhlaví + `ceil(N/Columns)` × řádek.
  Dorovnává legacy „Počet sloupců" (`stPOZ_PocetSloupcu`), bez 1/2 stropu.
- ✅ **Zarovnaný marker** - `MarkerSirka` (0 = inline jako dnes, >0 = pevná levá buňka se znakem →
  svisle zarovnané markery). Dorovnává legacy `stPOZ_SirkaSloupceTextVlevo`.
- ✅ **Řazení poznámek** - `Razeni` (výchozí / podle délky). Dorovnává legacy „Trid_delK".
- Vystaveno v EDInputformu (`NotesLegendElementEdit.cls`, skupina „Rozložení") i inline panelu.
  Default (1 sloupec, inline, výchozí řazení) = beze změny existujících šablon. Reference:
  legacy návrhář `formTISKMHDSekcePoznamkyEdit` (viz `ad-data-mapa.md §1b`).

## 2026-06-21 - AD #7161 D5 (skrýt legendu „× na znamení") + D6 ověřeno

- ✅ **#7161 D5** - `NoteVisibilityFilter.ShouldHideNaZnameniLegend(template)`: když šablona má StopList,
  ale žádný nemá zapnuté `ShowSignalStopIndicator` („Na znamení"), skryje se z legendy kategorie
  `atribut-zastavky` („× na znamení"). Legenda následuje viditelnost značky, žádné nové ovládání,
  žádná Caché změna. Test: CB_GWBUS 0320144. **#7161 uzavřen (D5+D6).**
- ✅ **#7161 D6 ověřeno** na CB_CSADAD 0365004 (agregát „všechny spoje bezbariérové" se parametrem skryje).

## 2026-06-21 - AD #7947 + #7161 D6 (rozdíly tras + bezbariér agregát)

- ✅ **#7947 „jede až do XY"** - `RozdilyTras` větev prodloužení vypisuje „jede až do zast. <konečná>"
  místo matoucího legacy „pokračuje přes". Záměrná odchylka (klient), kód analogický k „jede jen do".
- ✅ **#7161 D6 - skrýt agregát bezbariér** - parametr `SCHOVAT_AGREGAT_BEZBARIER` (bool). Caché taguje
  agregátní legendu (`allBBP` → kat. `bezbarier-agregat`), `NoteVisibilityFilter` ji skryje. Filtr-model
  jako #7955. Neověřeno na datech (žádná linka 100% bezbariér v KV_DP/CB_CSADAD - latentní).
- 🔧 **MCP `edison-cache` opraven** - deployovaný exe byl neúplný (chyběl runtime); rebuild +
  `.mcp.json` cesta na podsložku `C:\Edison\EdisonCacheMcp\`. Ověřeno proti gold oraclu (CB_CSADAD).
- 📄 **AD analýzy sloučeny** do `ad-data-mapa.md`; přípoje/km přesunuty do linkových JŘ.

## 2026-06-20 - AD #7955: co schovat v rozsahu „nepoznámkovat" (Fáze 2)

- ✅ **Parametr sestavy `SCHOVAT_V_ROZSAHU`** (enum: Ruční komentáře [default] / Automatické (rozdíly
  tras) / Obojí) - řeší #7955 (klient chce u spojů s vlastním komentářem schovat auto, ne komentář).
  Nad preview i v Ctrl+P (nativní param infra).
- **Model nadmnožina + TD filtr** (viz `feature-design-principles.md §5.2`): Caché už **nedropuje**,
  emituje všechny anotace/legendu otagované `KATEGORIE` + `SPOJ` + `V_ROZSAHU_NEPOZNAMKOVAT`. Schování
  je render-time `NoteVisibilityFilter` v TD → změna se projeví překreslením, bez Caché re-fetch.
- **Filtr** schová jen anotace v rozsahu „nepoznámkovat" (`V_ROZSAHU=1`) dané kategorie; legendu prořeže
  podle kategorie + přítomnosti znaku (sdílený znak mimo rozsah zůstane; „x na znamení" se neprořezává).
  Napojeno na všechny 3 datové cesty (preview, export, tisk reálných dat). Default = legacy (bez regrese).
- Caché legacy chování zachované (nepřeklopena sémantika „nepoznámkovat") - flip je čistě TD volba.

## 2026-06-20 - AD ruční poznámky linky z tabu MHD (Fáze 1)

- ✅ **AD-N7 Komentáře** - `ZastavkoveJrAd` čte ruční komentáře spoje (`MhD,"SeT_SpeC"` přiřazené přes
  per-spoj `MhD_SpeC`) → znak jako anotace u minuty + znak+text do legendy. 1:1 `JRapiLinPrtMhd1` ř.211-216.
- ✅ **AD-N8 Spoje, nepoznámkovat** - helper `NepoznamkovatSpoj` (1:1 `noPoz`): `MHDSPNPZN`=odTČ~doTČ,
  směrová logika dle parity spoje, fallback na konec trasy. Potlačí **ruční komentář** v rozsahu TČ
  (auto-poznámky běží dál - legacy sémantika; flip = Fáze 2 / #7955).
- ✅ **AD-N9 Směrové poznámky** - `MhD,"SeT_MhD"` tam/zpět/vše filtrované dle směru stránky
  (tam→`>`+`<>`, zpět→`<`+`<>`) → legenda. 1:1 ř.440-447.
- Caché-only (C# renderer beze změny - anotace + POZNAMKY existující cestou). Čitelné názvy proměnných
  (ne legacy zkratky). Zbývá: umístění směrových poznámek (legacy nahoru jako směrové záhlaví vs dole),
  Fáze 2 (#7955 opt-in přepínač).

## 2026-06-20 - AD nastavení linky 1:1 (track PRIORITA 2b)

- 📄 **Business analýza nastavení tisku na úrovni linky** - `ad-data-mapa.md`:
  podchyceno, jak legacy čte nastavení linky (taby MHD / MHD-další / dialog Nastavení tisku) přes 3
  readery (`gPrpLin`/`gPrpLinMhd`/`gSetLinPrt^JRapiLin*`), per-feature business význam + storage v
  `^JRL` + zda to máme/nemáme. Klíč: `ALVA.TISKMHD.Data` je jen formátovač, logika je v MAC `JRapi*`.
  Roadmap track **PRIORITA 2b (AD-N1..N15)** namapován 1:1 na §-čísla analýzy.
- ✅ **AD-N1 - Poznámka nad linku** - `ZastavkoveJrAd` nově čte `PATCH,"Head"` → placeholder
  `POZNAMKA_NAD_LINKU` (+ ve schématu). Legacy gPrpLin dta4.
- ✅ **AD-N2 - Typ JR** - `TYP_JR` (dosud prázdné) naplněno z `PATCH,"OBJ"` (1→„VÝLUKOVÝ", 2→„návrh
  jízdního řádu", znění 1:1 `JRapiLinPrtPS3`). Pozn.: zastávková větev OBJ nečte → volitelný
  placeholder, ne automatické razítko.

## 2026-06-17 - Preview: oprava falešného varování o přetečení

- 🐛 **Fix falešného červeného varování o přetečení** - `PreviewCanvas.PageOverflows` přestal hlásit
  přetečení pro JŘ element s pevnou výškou, který se do svého boxu vejde **auto-scalem** (zmenšením písma).
  Dřív: nový StopList (pevných 60 mm) na MHD mock datech (>~12 zastávek) → přirozená výška > box →
  `ContentOverflows=true` → červený rámeček celé stránky + „nevejde na stránku", i když element vizuálně
  sedl (auto-scale bez spodní meze ho vždy zmenší na box). Teď se hlásí **jen reálné geometrické
  přetečení** (element přesahuje spodní hranu sekce/stránky). `ContentOverflows` zůstává jako helper
  („box < obsah"), ale neřídí varování.
- ✅ **Agregovaná kontrola přetečení v tisku** - `PreviewCanvas.CountOverflowingPages(dataPages)` (layout-only,
  bez renderu) projede stránky všech instancí; `PrintDialog` při exportu reálných dat (data už fetchnutá
  z Caché → levné) vypíše „⚠ PDF uloženo, ale X z N stránek přetéká". Žádný fetch navíc, žádné nové UI.
  Ad-hoc kontrola bez generování PDF se případně doplní později na žádost zákazníků.

## 2026-06-17 - UI: font tokeny (škálování přes shell) + auto-width comboboxy + DEV cleanup

- ✅ **UI fonty na EDTheme tokeny** - natvrdo zadané `FontSize`/`FontFamily` v XAML (12 souborů) nahrazeny
  `{DynamicResource EDTheme.FontSize.*}` / `EDTheme.FontFamily`. TD nyní respektuje sdílenou user font
  preferenci z EDISONMAIN2 (FontService už TD inicializuje) a proporcionálně škáluje. Mapování
  9,10→Small · 11→Caption · 12→Body · 13,14→Header · 16,18→Large; `Consolas` (výrazy/glyfy) ponecháno.
- ✅ **Comboboxy v hlavním okně na `Width="Auto"`** (CmbGridSize/CmbSnapGap/CmbZoom/CmbPreviewZoom) +
  `MinWidth="64"` - text se neuřízne a roztáhne se i při zvětšeném fontu.
- ✅ **DEV cleanup** - odstraněn `#if DEBUG` auto-open `kv_ad_test.json` po startu (hardcoded osobní cesta;
  AD agenda hotová) v `MainWindow.xaml.cs` + `--test-detector` blok v `App.xaml.cs` (hardcoded `Downloads\*.PDF`).
  `PdfImport.ScheduleDetectorTest` zůstává jako ruční debug utilita (mimo app flow).

## 2026-06-15 - Wave 4: EDInputForm Image field type + migrace ImageElement (W4-6)

- ✅ **Nový `EFieldType.Image`** v EDControls EDInputForm - upload → base64 + náhled přes
  `Base64ToImageSourceConverter`. Image hodnota se **nikdy neposílá na server** (velký base64;
  `EDInputFormController` ji drží client-side a po Validate echu obnoví).
- ✅ **ImageElement migrován** do generického `ElementEditDialog` - Caché schema `ImageElementEdit.cls`;
  `EDInputFormSchema` switch v MainWindow mapuje `ImageElement → ImageElementEdit`. Uzavírá W4-6.
- Předtím (samostatné commity): EDInputForm Color field type + ComboBox field type (editable + autocomplete).

## 2026-06-12 - Wave 4: parametry tisku přes EDInputForm (Caché-driven)

- ✅ **Tiskový dialog → EDInputForm** - parametry sestavy se renderují přes EDInputForm, jehož schéma
  řídí agenda v Caché (`GetParameters`). Generická dispatch třída `ALVA.TemplateDesigner.EDInputForm.AgendaParams`
  (jedna pro všechny agendy) nested-callem získá param JSON a převede ho na `Form` (typy bool/number/enum/date/text);
  prefill aktuálními hodnotami přes `IN["values"]`. Žádná per-agenda EDInputForm třída - `GetParameters` zůstává
  jediný zdroj definic parametrů.
- ✅ **visibleWhen v C#** - `TemplateParameter.IsVisible(values)` (nový, jediný zdroj logiky) sdílí preview lišta
  i tiskový dialog; viditelnost (např. Hodiny od/do při „Vlastní rozsah") se přepíná živě na polích EDInputformu.
  `ParameterControlVM.UpdateVisibility` zredukováno na delegaci.
- ✅ **enum = required** v `AgendaParams` → List dropdown bez úvodního prázdného řádku (musí být vybráno).
- Odstraněn `ParameterControlVM` z tiskového dialogu + duplicitní nadpis; `CacheAgendaProvider.GetCacheClass` accessor.

## 2026-06-12 - Wave 4: „Vlastnosti sekce" přes EDInputForm + fix prázdného řádku v List

- ✅ **Vlastnosti sekce → EDInputForm** (W4-7 část) - nová Caché schema `ALVA.TemplateDesigner.EDInputForm.SectionEdit`
  (zrcadlí `BuildSectionDescriptors`: Název, Výška, Tisk/Umístění/Přetečení, rámeček, viditelnost).
  `ElementEditDialog` generalizován z `TemplateElement` na `object` (umí teď i sekci/stránku);
  `OpenPropertiesForSection` routuje na generický EDInputForm dialog místo `ElementPropertiesWindow`.
  Vědomé odchylky od starého panelu: vynechané „Datový zdroj"/„Výška řádku" (jen legacy PerDataRow,
  EDInputForm neumí conditional fields), bez clampu výšky, bez live validace výrazu viditelnosti.
- ✅ **EDInputForm List bez prázdného řádku u povinných polí** (EDControls `Field.cs`) - `ListItemsForDisplay`
  přidává implicitní prázdnou položku jen pro **nepovinná** pole (umožní vymazat na „nevybráno");
  u povinných (enumy) ji vynechá. Předtím se prázdný řádek zobrazoval ve všech strict List dropdownech.

## 2026-06-12 - Preview: varování o přetečení stránky + cap roztažení DepartureGrid

Dva nálezy z vizuálního testování na velkých linkách (0480202, 78 stránek):

- ✅ **Varování o přetečení v preview** - když se obsah stránky nevejde (přirozená výška obsahu >
  pevná Height boxu), přetékající stránka dostane **červený rámeček** (2,5px) a status bar zobrazí
  **červený blok** „⚠ Obsah se nevejde na stránku" (tooltip s radou zvětšit PageSize / zmenšit obsah).
  Detekce přes novou abstract `ScheduleElementBase.CalculateHeightFromData` (3 JŘ elementy = `override`)
  + `ContentOverflows` (`natural > Height + 0.5mm`, tj. **jakékoli** přetečení, ne práh). Běží v
  `PreviewCanvas.PageOverflows` **před** renderem (pre-detekce) → `OverflowChanged` callback →
  `HasPreviewOverflow` ve VM → status bar binding. Jen preview, ne v PDF/PNG exportu.
  (Iterace na vzhledu: zkusili jsme nádech přes obsah → podbarvení okolí → celé pozadí → finálně
  jen rámeček + blok v liště, což uživateli přišlo dostatečně výrazné a nejmíň rušivé.)
- ✅ **Cap roztažení DepartureGrid** - `AutoExpandToFillHeight` dostal strop `MaxAutoExpandFactor=1.5`.
  Řídká mřížka (např. linka s odjezdy jen v 1 hodinu) se jinak roztáhla na bizarně obří řádky;
  teď se vyplní max 1,5× přirozené výšky, zbytek místa zůstane prázdný.

## 2026-06-12 - ZastavkoveJrAd: dokončení 1:1 vůči legacy AD + performance

Agenda zastávkového JŘ z dat hlavního JŘ modulu (AD/JRMAIN) dotažena na **datové 1:1**
proti legacy gold oracle (`ALVA.TISKMHD.Data` VolanoZ=JRMAIN) + pročištěna výkonově.

**Funkční 1:1 (vše ověřeno proti gold oracle):**
- ✅ **Rozdíly tras** - `RozdilyTras`, 1:1 `OmTrasa^JRapiLinPrtMhd`: „neobsluhuje zast. X" /
  „jede přes…" / „jede jen do…" / „pokračuje…", marker z písmenné sady od „B", per zastávku.
  (Nejdřív zkusil MHD alg 1002 - ověřením proti gold se ukázal jako špatný, revertován; AD
  používá `OmTrasa`/NZ mechanismus.) Ověřeno 0365001.
- ✅ **POPIS_DOPRAVCE - adresa provozovny** - `NazevDopravce` čte i `$2` (ulice) z `^JRSET("DOP")`.
- ✅ **Filtr atributů zastávek** - `JeViditelnyAtribut` zúžen na jen „na znamení" (`x`); legacy
  `UVOD^JRapiLinPrtMhd` jiné atributy (A=autobus/L=vlak…) do StopListu netiskne. Odstraněna over-emise.
- ✅ **Atributy spoje (bezbariér)** - `BezbarierKonfig` (isinv/iscbe z `^JRSET("PS","LIMES")`, allBBP);
  `inv`=hlava:15→„H" (SpecZnak 72), `cbe`=hlava:22→„F" (70); anotace „specialni" + legenda.
- ✅ **Časy jízdy ve StopListu (TrasaMin)** - jeden sloupec = gold `TrasaMin1` (kumulativní min od
  vývěskové zastávky, jako MHD). Multi-varianta (`TrasaMin1-15`) odložena - chce zásah do C# rendereru.

**Performance (behavior-preserving, výstup byte-identický - 0365001: 680373 znaků, 39 stránek):**
- ✅ Per-linka/per-směr invarianty (`ObsluhovanePoradi`, `BezbarierKonfig`) memoizovány do `^||TiskAd`
  místo full-scanu spojů per stránka.
- ✅ W-trasa cachovaná mapou `poradi→cas` per `cisloTrasy` (`WMapa`) - nahradila lineární `$p` sken
  při každém lookupu; W-helpery (offset, časy, bitmapy, základní trasa) čtou z mapy.
- ✅ Bug fix: prázdná `cisloTrasy` (spoj bez trasy) padala na `<SUBSCRIPT>` - `^||` globály neberou
  prázdný subscript; přidán guard. Stejný `$d()` guard na `served` cache (merge z undef = `<UNDEFINED>`).

**Zbývá k úplnému 1:1 (zdokumentováno §10):** multi-varianta TrasaMin (renderer), číslování omezení
TYPPOZ=1 (firm-config), šířka/layout legendy.

---

> **Starší záznamy (2026-05 a dříve)** → [`changelog-archive-2026-05.md`](changelog-archive-2026-05.md).
