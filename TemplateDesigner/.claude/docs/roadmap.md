# Roadmap

> **Jen otevřená / akční práce.** Hotové → [`roadmap-archive.md`](roadmap-archive.md), průběžný
> changelog → [`changelog.md`](changelog.md), stav tiketů a věci „jen na poptávku" →
> [`customer-requirements §0`](customer-requirements-zastavkovy-jr.md).

> **Nejblíž na řadě:** **`L9`** - aby linková tabulka nevypadala jako jedna souvislá mřížka.
> Průchod testovacími linkami je za námi a datový rozdíl proti starému tisku nenašel žádný.

---

## 🚆 Linkové a knižní JŘ (náhrada TiskJR)

> ⚠️ **Gate: R1 (sdílené tabulkové jádro), R2 (layout zodpovědnosti), R5 (embedded fonty)** -
> [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md).

Legacy `TiskJR` generuje **vývěsní i knižní JŘ z téhož enginu**, takže je to jeden track. Formát:
sloupcová tabulka celé linky (svislý seznam zastávek × sloupce spojů s časy; řádek platností se
symboly/NZ; Vyčká/Navazuje; skupinová záhlaví PD/So/Ne; 10 druhů poznámek).

**Na čem zbývající vlny stojí** (kontext, ne seznam úkolů): datové jádro
(`LinkovyJrAd`/`LinkovyJrMhd`), `LineTableElement` s plánem řádků, síť pro porovnání se starým
modulem (oracle `mkPLinWin`) a systémová výchozí šablona pro AD. ⚠️ **Pro MHD výchozí šablona
zatím není.**

⚠️ **Linková AD agenda je od 24. 8. zveřejněná** (`Released = 1`, žádost Jan) - bez toho na ni
nešlo v návrháři ani překliknout, takže nebylo jak zkoušet. **Městská zůstává skrytá**, neprošla
porovnáním se starým modulem. Pozor: zveřejněná agenda se ukáže i zákazníkům, jakmile se vydá.

⚠️ **Agendy jsou pořád úvodní draft**, takže se v nich smí měnit i to, co je jinde
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
každý nález by mohl být „ještě to neumíme" místo „takhle to má být". Pořadí vln je proto
**testovací linky → `L9` → `L6`**.

✅ **Průchod testovacími linkami je hotový** (26. 8., detail v archivu). **Datový rozdíl proti
starému tisku nenašel žádný** - jediný nález je chybějící **obousměrný tisk** (patří do `L6`).
Zbývají z něj **dva drobné dluhy**:

- **Pořadí sloupců spojů na `CB_CSADAD 1 / 0370618` neprojité** - mělo se srovnat sloupec po
  sloupci proti starému tisku, ale ten tiskne oba směry v jedné tabulce, takže není co k čemu
  přiložit. **Vyjde až s obousměrným tiskem** (`L6`), nebo na jiné lince s ručním pořadím.
- **Písmena ze zóny (`VS_CSAD 5 / 0771901`) neověřená** - v testovacích datech pro Vsetín není,
  na čem to zkusit. Vzít, až ta data budou.

| # | Vlna | Vel. |
|---|---|---|
| **L6** | **Zbytek repertoáru starého modulu.** Sloupce, které prvek nemá: **šipky směru** (legacy typy 5 a 8) a **zrcadlené sloupce spojů** pro obousměrný tisk (typ 9). ⚠️ **Obousměrný tisk je podmíněný, ne přepínač** (změřeno čtením legacy 26. 8.): starý modul ho vypne, když v jednom směru nejsou spoje, a znovu po střihu - jakmile se zrcadlený blok nevejde na stránku bez řezu, tiše spadne na „každý směr zvlášť“. Bez toho pravidla bychom zrcadlili i tam, kde legacy nezrcadlí; zdrojové řádky v [`§13.2 arr,7`](linkovy-knizni-jr-implementace-analyza.md). Dál **zóny do více sloupců** u hraniční zastávky (#8576, jako km sloupce), **turnusy** a **výchozí šablona pro MHD**. ⚠️ **Vícesloupcová legenda (#7487) z vlny odpadla** - prvek legendy umí 1-4 sloupce včetně zalamování textu a výšky (zjištěno 26. 8., výchozí hodnota je 1, proto si toho nikdo nevšiml). Zbývá jen ověřit na reálné sestavě. | M |
| **L7** | ~~Sdružený JŘ více linek~~ - **není naše práce** (upřesnil Jan 25. 8.). Sdružování linek se řeší **v jízdních řádech**; návrhář dostane až výslednou linku a tiskne ji jako každou jinou. Logiku, o kterou si řekl KOVED (#9283), tady neimplementujeme. ⚠️ **Důsledek pro #9417:** zákazníkův případ (souhrnný JŘ dvou linek s dlouhým výčtem dopravců) se k nám může dostat jako **jedna linka s mnoha dopravci**, tedy bez čekání na cokoli dalšího - až se to stane, filtrování dopravců přestane být odložené. | - |
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

- **`B101` Není poznat, jestli je jedna skupina dnů nastavením, nebo daty linky** (z 25. 8.).
  Prvek pás skupin nekreslí, když vyjde jediná skupina - a uživatel pak nemá jak poznat, jestli
  si dělení nastavil špatně, nebo jestli tak prostě vypadá linka (na 0330511 jedou všechny spoje
  jen v pracovní dny). Otázku „mám to vůbec nastavené dobře?" dnes **nejde zodpovědět z aplikace**.
  Drobné zlepšení, ne chyba.

- **`B100` Ruční zlom stránky na tarifním čísle** (`PATCH,ZLOM`, z `L5` 25. 8.).
  U linky jde v jízdních řádech nastavit tarifní číslo, na kterém se má tisk rozdělit; starý modul
  to respektuje, my ne, takže se takové linky tisknou jinak. **Odloženo kvůli frekvenci**
  (rozhodnutí Jan 25. 8.): nastaveno na **3 linkách z ~93 tisíc** (měření 21. 8.), což je za cenu
  zásahu do serveru i klienta slabý důvod. ⚠️ **Neblokuje nic dalšího** - klientská strana pro
  zlomy existuje (`INedelitelneDily`, silné a slabé zlomy) a vynucený zlom by k ní jen přibyl;
  na drátě by šlo o jedno pole navíc, tedy aditivní změna. Vytáhnout, až si o to někdo řekne,
  nebo až se bude stejně sahat na přenos nastavení linky.

- **`B99` Tři značky, které v tiskovém fontu nejsou** (loď, wifi, zásuvka; z `L4` 24. 8.).
  Dnes se vypisují **textem** - provizorium podle rozhodnutí Jana, ať se informace aspoň neztratí,
  ale v tisku vypadají jinak než ostatní značky. Pořádné řešení znamená buď je do fontu doplnit
  (zásah do `TimeTable2.ttf` i `TimeTable2SK.ttf`), nebo je kreslit jako vlastní obrázek. Vytáhnout,
  až o ně někdo zakopne v reálné sestavě - dnes je v datech nemá žádná z měřených firem.

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
