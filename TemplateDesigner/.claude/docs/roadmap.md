# Roadmap

> **Jen otevřená / akční práce.** Hotové → [`roadmap-archive.md`](roadmap-archive.md), průběžný
> changelog → [`changelog.md`](changelog.md), stav tiketů a věci „jen na poptávku" →
> [`customer-requirements §0`](customer-requirements-zastavkovy-jr.md).
>
> **Nejblíž na řadě:** **linkové JŘ** - `L1` (síť) hotová, inventář nastavení hotový (20. 8.),
> **rozhodnutí před `L2` hotová** (21. 8.). `L2` je vzatelná celá.

---

## 🚆 Linkové a knižní JŘ (náhrada TiskJR)

> ⚠️ **Gate: R1 (sdílené tabulkové jádro), R2 (layout zodpovědnosti), R5 (embedded fonty)** -
> [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md).

Legacy `TiskJR` generuje **vývěsní i knižní JŘ z téhož enginu**, takže je to jeden track. Formát:
sloupcová tabulka celé linky (svislý seznam zastávek × sloupce spojů s časy; řádek platností se
symboly/NZ; Vyčká/Navazuje; skupinová záhlaví PD/So/Ne; 10 druhů poznámek).

**Hotové:** datové jádro (`LinkovyJrAd`/`LinkovyJrMhd`), `LineTableElement`, plán řádků a auto-fit
fontu; **síť pro porovnání se starým modulem** (`L1`, oracle `mkPLinWin`); výchozí šablona pro AD
se distribuuje jako systémová. **Pro MHD výchozí šablona zatím není.**

⚠️ **Agendy jsou úvodní draft** (`Released = 0`), takže se v nich smí měnit i to, co je jinde
zamčené - zastávkové se přitom nesmí dotknout. Slovník polí je srovnaný s vývěskou a hlídá ho
`AgendaSchemaDriftTests`; nové pole přidávej pod týmž názvem, jaký pro tutéž věc používá vývěska.
Do budoucí **MHD** šablony: `{CISLO_LINKY}` je **licenční** číslo, veřejné je
`{LOKALNI_OZNACENI_LINKY}`.

**Vlny se berou v pořadí a každá se poměřuje se starým modulem** (rozhodnutí Jan 19. 8.), **nejdřív
AD, MHD až po něm**: porovnání pro AD běží, AD má výchozí šablonu a oba zákaznické vzory jsou AD.
⚠️ **„Dodělám AD a MHD spadne samo" neplatí** - AD je nadmnožina v obsahu tabulky, ne v konceptech
(MHD má licenční × veřejné číslo linky, typ provozu jako datový pojem a vlastní vrstvu nastavení),
takže MHD si nese **vlastní inventář a vlastní oracle**.

📄 **Zkušební vzor pro celý track: linka 940 Kroměříž-Přerov (IDZK)** - reálné zadání zákazníka,
které starý modul nedokázal vyrobit (analýza §15). Až bude šablona stát, ověřuj proti němu.

⚖️ **Jak se nakládá s nastavením starého modulu** (platí dál pro `L3`+): **co uměl starý modul, musí
umět i nový**, a nastavení se **neignoruje proto, že je to vzhled** - když si ho zákazník u linky
nastavil, tisk ho musí respektovat. Podle toho, co dělá: **mění data** → načtení dat · **volba
uživatele** → parametr sestavy · **něco schovává** → třístav „Automaticky (dle linky)"
([`ux-conventions §8`](../rules/ux-conventions.md), i s důvodem, proč je výchozí hodnota konstanta)
· **stránkování / chybějící featura** → `L5`/`L6` · **měřicí řetězce a šířka plochy** → vědomě nikdy.
V prvku k tomu patří pravidlo **prázdný sloupec se netiskne**.

🧭 **Vůdčí linie:** *to, jak je výstup složený, patří do návrháře; z nastavení u linky se bere jen
to, co mění data.* **Sporné případy řeš směrem do šablony** - starý modul se přepisuje a část
nastavení se může přesunout. Rozhodnutí položku po položce, jejich důvody a změřený výskyt
nastavení v datech: [`§13.4`-`§13.6`](linkovy-knizni-jr-implementace-analyza.md); pozastavené
tikety helpdesku (8 požadavků se slíbeným „vyřešíme v novém návrháři") [`§14`](linkovy-knizni-jr-implementace-analyza.md).

⚠️ **Sada voleb pro městské linky se rozhoduje až po AD** - starý modul u nich většinu voleb
ignoroval (jeho MHD engine čte tři z jedenácti), takže parita tam nemá čím se řídit a jakákoli
sada voleb je **nová featura**, ne dorovnání.

🎯 **Pořadí prací (rozhodnutí Jan 24. 8.): nejdřív dotáhnout paritu se starým tiskem, teprve pak
uživatelské volby a vzhled.** Důvod: dokud se data liší, není co procházet z business hlediska -
každý nález by mohl být „ještě to neumíme" místo „takhle to má být". Proto se pořadí vln mění na
**`L2b` (hotovo 24. 8.) → `L2` zbytek → testovací linky → `L4` → `L3` → `L5` → `L9` → `L6`**.

✅ **`L2b` hotová** (24. 8., detail v archivu) - parita dat je tím uzavřená až na tři deklarované
dluhy, které si nesou vlnu.

📋 **Po `L2b` a zbytku `L2` přijde sada testovacích linek** - pojmenovaný seznam linek, kde je vidět
každé nastavení, aby šlo procházet business logiku v aplikaci a sledovat, jak přibývá. Dnes to
vypisuje sonda `LinkovyJrNastaveniProbe.KdeToZkouset`, ale bez šablony a bez popisu, co se má na
které lince ukázat.

| # | Vlna | Vel. |
|---|---|---|
| **L2** | **Zbytek matice nastavení - tři věci odsouhlasené 19. 8. jako „uděláme".** Sedm nastavení, která jsou v datech reálně zapnutá, i oba nálezy z `L1` jsou hotové (detail v archivu) - zůstává: **zvýraznění zvolené zastávky i konkrétního tarifního čísla**, **volba tisku čísla linky/vlaku v přípojném řádku** a **dorovnání skládání pruhu s dopravcem na starý modul** (dnes na to máme vlastní logiku, kterou proti třem nastavením nikdo neporovnal). Sem patří i **přípojný řádek s vlastním řezem a velikostí** (#8360). ⚠️ **Prázdný přípojný řádek se netiskne** (#11300) má smysl teprve vůči bloku → `L5`. | S |
| **L3** | **Volby uživatele = parametry sestavy.** Linkový AD má **jeden** parametr, linkový MHD **žádný**, proti sedmi u zastávkového - uživatel dnes dostane tabulku, se kterou nemůže nic udělat. Rozhoduje se **celá sada najednou** (co má být stejné jako u vývěsky, co jinak), ne jeden parametr po druhém; kandidáti jsou v §13.2 A. **Pohlcuje `B60`** (tvar názvu zastávky a „název pro MHD" jsou dva z těch parametrů; totéž chce #9088 bod 4; `L1` to změřila - na jedné lince 38 z 62 názvů, a týká se to i textů návazností, kde starý modul tiskne u městských linek zkrácený místní tvar) a `C5` (pořadí poznámek). ⚠️ **Nový dluh z 24. 8.:** jeden spoj patří podle starého modulu do **tří** skupin dnů, u nás do dvou, takže se v tabulce opakuje jednou míň a zákazníkovi chybí sloupec (`ZarazeniSpojeKnizni` × legacy dělení dle platnosti; doloženo na VS_CSAD 0883637). **Součástí je i flexibilní zadávání sloupců** jako u vývěsek: dnešní tříhodnotový přepínač dělení je chudší varianta téhož, co tam řeší vlastní sloupce (množina denních značek + značky, které spoj ze sloupce vyřadí + nadpis, se čtyřmi hotovými vzory ze `PresetySloupcu`). **Definice je přenositelná beze zbytku, spotřeba ne** - u vývěsky je skupina jeden sloupec, v lince je to pás nad několika sloupci spojů a spoj ve dvou skupinách se vypisuje dvakrát; navíc skupiny ovlivňují rozřezání na stránky (`L5`). ⚠️ **Jen pro AD** - MHD má skupiny v datech jako typ provozu i s nadpisem z číselníku, vlastní sloupce by tam byly druhá cesta k témuž. ⚠️ Popisky enum voleb jsou po vydání **zamčené** (persistuje se zobrazovaný text). ⚠️ Sáhne to do `TemplateDesignerViewModel` (3 543 ř.) - role „parametry sestavy a načítání dat" je jeho přirozený první řez, rozřezat **při dotyku**. | M |
| **L4** | **Symbolový font** (gate R5, dřív `B10`). Linková tabulka symboly **nekreslí vůbec** - agenda posílá legacy klíče a chybí převod na pozice ve fontu. ✅ Podklady hotové: katalog 66 symbolů, render-módy i **změřený repertoár fontu** (94 pozic) v `elementy-architektura.md`. ⚠️ **Z `L1`:** mapa značka → číslo je **opsatelná přímo ze zdroje starého modulu** (`GSId^JRapiLinPrtWin`), nemusí vznikat měřením - a dosavadní čísla „74 = projíždí, 75 = jede jinudy" platí jen pro překladovou vrstvu starého klienta, proti enginu je to 1 a 2. Hotový opis je v `LinkovyJrLegacyOracle` (parametr `ZNACKY`). ⚠️ Pozice `znak` z katalogu se s pozicí ve fontu často **neshoduje** (letadlo `n`, paragraf `P`, metro `#`) - přes mapu, ne přes písmeno. **Rozsah:** sdílená mapa značka → (render-mód, hodnota) pro všechny prvky, nahradí i dnešní `SymbolGlyph` u zastávkových vývěsek → dotkne se běžícího zastávkového JŘ, chce zlaté odtisky. Culture rozlišení jen pro **3 pozice** (`D`, `w`, `x`). Ve fontu chybí loď, wifi a zásuvka. | M→S |
| **L5** | **Rozřezání široké tabulky / `Strih`** (gate R1/R2, dřív `B13`). Široká tabulka (MHD linka = 100+ spojů) se dnes zmáčkne na šířku místo rozřezání do stránkových bloků; + flow paketů, „dotisk zbytku legendy na další stránce". Stojí na hotovém `B11`: rozřezání se píše **jednou** nad plánem řádků. Vlna nese tři zákaznické závazky: **hlavička per blok, ne per směr** (#9417 - dnes se vypisují všichni dopravci linky a hlavička přeteče pod tabulku), **dorovnání šířky prázdnými sloupci jako parametr sestavy, ne vlastnost šablony** (#8372 - u řídkých linek se navíc nesmí roztáhnout „do absurdna") a **ruční zlom na zvoleném tarifním čísle** (`ZLOM` z §13.2 C). Testovací případ z #9088: prázdná legenda nesmí zamíchat stránkováním. | L |
| **L6** | **Zbytek repertoáru starého modulu.** Sloupce, které prvek nemá: **šipky směru** (legacy typy 5 a 8) a **zrcadlené sloupce spojů** pro obousměrný tisk (typ 9). Dál **zóny do více sloupců** u hraniční zastávky (#8576, jako km sloupce), **vícesloupcová legenda s přetékáním** (#7487 - dnes jeden sloupec, chtějí čtyři), **turnusy** a **výchozí šablona pro MHD**. | M |
| **L7** | **Sdružený JŘ více linek** (dřív část `C6`) - jedna sestava přes víc linek s řádkem zdrojové linky, včetně výlukové varianty. ⚠️ **Přestalo být „ověřit, jestli to zákazník chce":** KOVED si o to řekl (#9283) a čeká kalkulaci, tedy placená práce. Rozsah a cenu rozhodnout s Pavlem, ne odvodit z kódu. | L |
| **L8** | **Dedup Caché tiskových enginů** (dřív `B14a`) - doprovod ostrého knižního JŘ, ne dopředu. Různé signatury `AddAnotace`/`BuildPoznamka`, ~15 helperů MHD↔AD + AD `^JRL` readery → sdílené readery a `Build*` do `BaseAgenda`/`JrShared`; Caché analog R1 extrakce jádra (`L5` řeší .NET render-side). Korektnostní riziko, proto **až nad hotovou sítí z `L1`**. Menší výsek = `B56` v Backlogu; při realizaci jednoho zredukovat druhý. ⚠️ Korekce z analýzy 5. 8.: z 27 stejně pojmenovaných helperů je jen 15 doslovných kopií (~97 řádků), a tvrzení o odlišném chování `CasHodnota` **neplatí**. | L |
| **L9** | **Aby tabulka nevypadala jako jedna souvislá mřížka** (podklad Zlín, analýza §15 - kolega to zkoušel ve starém modulu a nešlo to). Tři drobnosti, které patří k sobě: **ohraničení per sloupec** (dnes se svislé čáry kreslí buď všude, nebo nikde, takže km podsloupce nejdou slít do jednoho bloku), **popisek nad info sloupci** (pás nad nimi už existuje, jen má natvrdo prázdný text - vzor tam má „↓ Směr jízdy ↓") a **mezera mezi bloky** info × pracovní dny × víkendy. Volitelně různá barva vodorovných a svislých čar (dnes jedna pro obojí). ⚠️ Šest dalších bodů ze Zlína **návrhář už umí** (pořadí sloupců, vypnutí MHD, barva pásu nadpisů, popisky, rostoucí záhlaví u víc dopravců) - patří do ověření na šabloně, ne do práce. | S–M |

### Odložené body z pilotu

| # | Co | Proč odloženo / dopad | Odhad |
|---|---|---|---|
| C7 | **Rozdvojení příjezd/odjezd - jemný layout** + fixed-width padding sloupce (legacy `arr,13`) | kosmetika; TD řeší podřádky, ne padding. Padding sloupce jsou ve `L5` | S |
| C8 | **Vejdi výstup na zadaný počet stran** (podnět POVED #8785 bod 5) - starý modul to odmítl jako principiálně nemožné, náš engine má lepší pozici (`Strih` už umí režim „nejmíň stránek") | podnět, ne závazek - nabídnout až po `L5` | M |
| C9 | **Ručně zadaný „název směru" u linky** jako datové pole (#9088 bod 3, chtěné i pro vývěsky) | vyžaduje **nové pole u linky v jízdních řádech**, ne jen v návrháři - součinnost s kolegy z JŘ | S + cizí |

⚠️ **Hotovo bez plánování:** #11373 (KAD - poznámky **vedle** tabulky, ne pod ní) v TD **umíme dnes**,
legenda je samostatný prvek. Ověřit na reálné šabloně a tiket zavřít.

---

## 📥 Backlog

### Ostatní

- **`B98` Nastavení starého tisku, která nikdo nemá zapnuté** (evidence, ne práce; z měření 21. 8.,
  [`§13.6`](linkovy-knizni-jr-implementace-analyza.md)). Na 93 133 linkách šesti firem nejsou nikde
  nastavené: **tvar tisku místenkování**, **vlastní texty sloupce MHD** místo „IDP-P"/„IDP-Z",
  **tisk provozů dopravce**, čtyři přepínače **schovávání u linky** (`MHD2,netisknout*`) a **celá
  vrstva pod linkou** (řádek se v knižním JŘ netiskne, i zvlášť per směr; umělá sekce u spoje)
  a **tři přepínače řazení spojů** (`PS_PrintPorSpTR`, `PS_PrintSouhrnPorSpPri`,
  `PS_PrintSouhrnPorSpTR` - řadit jen podle odjezdu, resp. jen podle příjezdu u souhrnných linek;
  souhrnných linek je 33, ale bez těch přepínačů se chovají stejně jako ostatní).
  Řada z nich vznikla v legacy **pro jednoho zákazníka** a nikdo jiný je nepoužívá. Vytáhnout, až
  si o to někdo řekne - a pak počítat s tím, že **doklad z dat nebude** a půjde jen o implementaci
  proti čtení legacy zdroje. ⚠️ U skrývání řádků platí, že **ubírá řádky** - zákazník to pozná na
  první pohled, takže se k tomu má vyrobit vlastní testovací případ, ne to udělat naslepo.

- **`B66` Vlastní bloky prvků („moje komponenta")** (M, podnět Jan 12. 8.). Uživatel si vybere pár
  prvků (typicky svou hlavičku sestavy), uloží je pod jménem a pak je vkládá jedním kliknutím do
  dalších šablon. **Co už existuje:** uložení **celé sekce** jako vlastní šablony a seskupení prvků
  (Ctrl+G). Chybí mezistupeň mezi nimi - pojmenovaný blok menší než sekce. **Proč to čeká na pevnou
  tabulku:** dokud je hlavička poskládaná z 6 samostatných textů, musí blok řešit relativní pozice a
  vazby uvnitř skupiny; jako **jeden** prvek (pevná tabulka) je uložení skoro zadarmo. Otevřené:
  kde bloky žijí (per uživatel × firma jako šablony sekcí, nebo sdílené v DB) a jestli se vkládají
  z palety, nebo z nabídky „Přidat". **Pohltí i připravené bloky agend** (dřív `B77`): server pro
  každou agendu drží hotové sestavy tabulek („Záhlaví linky", „Shrnutí faktury"), ale od zrušení
  starého dialogu k nim z UI nevede cesta - je to titíž „pojmenovaný blok k vložení", jen dodaný
  námi místo uživatelem, takže samostatnou cestu do UI nedostane (rozhodnutí Jan 17. 8.).

- **`B18` Deklarativní pravidla EDInputFormu** (M, odloženo - rozhodnutí Jan 5. 8.: patří k velkým
  věcem do budoucna, ne do běžné fronty; je to **kapabilita sdílené komponenty pro celý EDISON**,
  ne featura návrháře). Dnes umí formulář reagovat na vyplněné hodnoty jen oklikou přes server
  (`Validate`, ručně per formulář). Zavést deklarativní vrstvu: začít **viditelností** (`visibleWhen`),
  gramatiku sdílet s tiskovými parametry (`POLE=hodnota` + nerovnost) - nezavádět druhý dialekt.
  Povinnost/povolení až s poptávkou; **skutečná validace zůstává imperativní** (potřebuje data
  a dopočty). **Follow-up:** migrace `CellStyleEditor` na Caché-driven formulář (S–M; háček je
  tri-state / reaktivní logika - bez `B18` nejde). **Follow-up 2:** rozšířit gramatiku o čtení
  **parametru sestavy**, aby šlo zašednout pole prvku mrtvá bez parametru (6 polí šipky vs.
  `TISK_SIPKY`, projeté zastávky vs. `TISK_PROJETYCH_ZAST`) - dnes to plní jen helpTexty.
  **Follow-up 3:** parametr sestavy nemá pole pro **popis**, takže krkolomné labely
  (`SCHOVAT_V_ROZSAHU`, `ROZTAHNOUT_NA_VYSKU`) nejde vysvětlit. Aditivní klíč na wire + tooltip -
  dělat spolu s gramatikou, ne zvlášť kvůli dvěma tooltipům.

- **`B56` Sjednocení opsaných pomocných funkcí agend** (odloženo - **rozhodnutí Jan 5. 8.**:
  nedělat předčasnou deduplikaci, dokud se tvar výstupních dat neustálí; sjednocením by se zabetonoval
  stav, který se ještě hýbe). **Podklad z analýzy 5. 8., aby se nemusela dělat znovu:** 27 pomocných
  funkcí se stejným názvem ve 2+ agendách, ale jen **15 jsou doslovné kopie** (~97 řádků k odstranění,
  ne 400-500, jak tvrdil původní odhad). 4 mají stejnou signaturu a rozešlé tělo - a to **záměrně**
  (`AddStandardJRFields` se liší v uživatelských popiscích polí: AD „Číslo linky" × MHD „Číslo linky
  (dle licence)"; `AddStandardJRParameters` nabízí jiné parametry sestavy). 8 sdílí jen název a dělá
  jinou věc - `BuildPoznamka` má dvě rodiny s **jiným datovým kontraktem** (zastávková posílá
  `KATEGORIE`+barvy, linková `DRUH`), `PopisDopravce` existuje 3× se třemi signaturami. Sloučit jde
  bezpečně **jen skupinu doslovných kopií a jen u zastávkových agend**, které kryjí zlaté odtisky
  a 46 testů helperů; **linkové agendy nemají ani jeden test** (2 000 řádků), takže tam by zásah jel
  bez sítě. Vzor existuje - `BaseAgenda` už 4 sdílené helpery hostuje.

- **Headless tisk (A3)** - „vytiskni linku X" z modulu přes PrintEngine fasádu (šablona+data→PDF
  bez stavu editoru). Dnes tisk jen v návrháři. Gate R4/R7.

- **Externí datové zdroje** (analýza 2026-07-12, na poptávku) - architektura připravená (engine zná jen
  `Template + DataContext`; DataContext je zdrojově agnostický JSON). Tři švy: Caché gateway agenda
  (0 změn .NET) · .NET provider vedle Caché (vzor `JsonFileDataSchemaProvider` + Composite) · headless
  A3 („POST šablona+JSON → PDF" = externí zdroj bez adaptéru; **A3 je nejpravděpodobnější nosič**).
  První krok s nejlepším poměrem cena/hodnota: **generický JSON zdroj (soubor/URL)** - tržní minimum
  (CSV/Excel/JSON/REST), náš wire formát už JSON je; využilo by i AI + testování šablon. Dořešit:
  credentials, schéma z dat, změkčení předpokladu „běží nad Caché".


---


## 🚋 Relace a vozové JŘ

Layout TBD - hlavička oběhu, tabulka spojů, poznámky (pravděpodobně víc elementů). Legacy:
`ALVA.MHD.ReportGenerator` (MHD) + `AD.ADREPORTGEN` (linky), viz
[`legacy-nahrada.md`](legacy-nahrada.md). **Relace (TiskJRRELACE)** - vývěsky
nádraží (SK zákazník, fork TiskJR ~50k LOC); **nejdřív ověřit živý use case**.

| Co | Stav |
|---|---|
| Analýza referenčních vozových JŘ - layout + wire formát | ⬜ |
| `VehicleScheduleElement` (pravděpodobně víc elementů) | ⬜ |
| Caché `VozovyJrAPI` - GetData/GetMockData + async (velký objem dat) | ⬜ |

---

## 📊 Report generátor (náhrada REPORTGENERATOR)

> ⚠️ **Gate před `F2`: R3 (prezentační nápovědy v datovém kontraktu + aditivní evoluce)** -
> [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md).

Největší kus náhrady - ne kvůli featurám (TD je napřed), ale kvůli **ekosystému ~28 agend** s RG
kontraktem (`GetSeznamSad`/`GetSadu`/`GetData`, `~`-tokenový stream). Nasazení: 33k textových elementů,
774 tabulek. **Ne big-bang** - etapizovat. Detail:
[`legacy-nahrada.md`](legacy-nahrada.md) §3.1 + §5 etapa 4.

| # | Co | Stav |
|---|---|---|
| F1 | **Prioritizace podle usage** - legacy `Main.cls:161-188` drží reálná čísla použití; seřadit agendy | ⬜ první (levný) |
| F2 | **Bridge vs čisté agendy** - bridge `LegacyRgAgenda` (překlad `~`-protokolu na TD JSON, všech 28 najednou s omezenou věrností) vs čisté reimplementace; doporučení: **bridge nejdřív** | ⬜ rozhodnutí |
| F3 | **TableElement na RG úroveň** (navazuje na dynamickou tabulku + `B76`): per-řádek zvýraznění 0-5 z dat, custom barvy per řádek/buňka, overflow módy (zalamování → klonování řádků), opakování mezisoučtu na další stránce, datové typy sloupců (datum/minuty/záporné červeně/skrýt nuly), řádek-jako-layout, **seskupení řádků** (nadpis a mezisoučet za skupinu) a **vnořená data** (doklad → jeho položky). ⚠️ Poslední dvě visely samostatně v backlogu; patří sem, protože mají smysl jen nad daty (pevná tabulka je vyplňovaná ručně) a jejich tvar se má odvodit **z reálných sestav RG**, ne navrhnout dopředu naslepo. Poptávku nemá ani jedna | ⬜ L |
| F4 | **Výstupní kanály fáze 2** - Excel export, PDF heslo + el. podpis (PDFSIGNER), Epson znakový tisk | ⬜ dle poptávky |
| F5 | **Jazykové varianty šablon** - per-jazyk kopie provázaná na CS zdroj | ⬜ |
| F6 | **Vědomě neportovat bez poptávky** - grafy (12×), AZTEC (2×), calculated (2× - výrazy TD nadmnožina), checkbox (8×); ověřit + škrtnout | ⬜ validace |

---

## 🗄️ Hotová práce

Detail dokončených etap → [`roadmap-archive.md`](roadmap-archive.md). Průběžný changelog → [`changelog.md`](changelog.md).
