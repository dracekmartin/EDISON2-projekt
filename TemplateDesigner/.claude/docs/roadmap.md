# Roadmap

> **Jen otevřená / akční práce.** Hotové → [`roadmap-archive.md`](roadmap-archive.md), průběžný
> changelog → [`changelog.md`](changelog.md), stav tiketů a věci „jen na poptávku" →
> [`customer-requirements §0`](customer-requirements-zastavkovy-jr.md).
>
> **Nejblíž na řadě:** zbytek sady `B93`, `B94`, `B96` níže (revize 17. 8.). Pak vybrat mezi
> **pevnou tabulkou** (`B76`) a **linkovými JŘ** (`B10` + `B13`) jako dedikovaným slotem.
> Zastávkový JŘ otevřený bod nemá - revize `B39` je uzavřená a proklikaná (17. 8.).
>
> ⚠️ **Nevydané a jen zčásti proklikané je všechno od 12. 8. včetně** - poslední vydaná verze je
> z 11. 8. Seedy novinek čekají na vydání, dřív se nenahrávají.
>
> ⚠️ **Naše testovací šablony v DB s vlastními sloupci je potřeba přeuložit** - „Pracovní dny" se
> od `B82` zapisují jako `X` (viz archiv). Zákaznických šablon se to netýká, ty vlastní sloupce
> nenesou.

---

## 🧹 Revize napříč vrstvami (17. 8.)

> ⚠️ **Pravidlo pro celou sadu: zastávkové JŘ se nesmí rozbít.** Jsou vydané a kryté zlatými
> odtisky. Kde se něco rozchází, **mění se linková strana** - linkové agendy mají `Released = 0`
> a jsou zatím úvodní draft. Dotyk vydaného prvku je přípustný jen **aditivně** (nová vlastnost
> defaultně vypnutá, odtisky beze změny).

**Nález, ze kterého sada vychází** (`B91`+`B95` hotové 17. 8., detail v archivu): obě rodiny agend
posílaly stejně pojmenované kolekce, ale **řádky uvnitř měly jiný tvar**, takže to vypadalo
kompatibilně a nebylo.

⚠️ **Jedno tvrzení z revize neplatilo:** `DELENI_SLOUPCU` × `ROZDELENI_SLOUPCU` **nejsou dva názvy
téhož**. Jsou to dvě různá nastavení sestavy - „Dělení spojů dle platnosti" (linkový, 3 volby)
a „Rozdělení sloupců (typy provozu)" (vývěska, 6 voleb). Sloučit je pod jeden název nejde: u těchhle
voleb se ukládá zobrazený text, takže by přepnutí agendy tiše shodilo nastavení na výchozí.

| # | Co | Vel. |
|---|---|---|
| **B93** | **Parita tabulkových prvků - nejdřív rozhodnout, pak kód.** Střídání řádků má tabulka z dat, pevná tabulka i mřížka odjezdů, **nemá ho seznam zastávek ani linková tabulka** (dvě nejdelší a nejhůř čitelné tabulky); podmínku viditelnosti a zvýraznění řádku nemá pevná tabulka. Podle klasifikace v [`elementy-architektura.md §0b`](elementy-architektura.md) jsou obě **kategorie 1** (čte jen data řádku) → patří do všech řádkových tabulek, takže je to drift, ne rozhodnutí. Projít všech pět tabulkových prvků × generické featury a rozhodnout per featura. ⚠️ Doplnění sáhne na **vydaný** seznam zastávek - jen aditivně a defaultně vypnuté. Vedlejší efekt: co skončí v jádru, dostane linková tabulka zadarmo. | analýza S + kód S |
| **B94** | **Dvě zastaralá tvrzení v docs.** Hierarchie prvků v `elementy-architektura.md` neuvádí `StopScheduleElementBase` ani `LineTableElement`; §5 vede jako kandidáta na extrakci něco hotového (tabulka z dat už `RowPlan` používá). (Třetí - „chybí výchozí linková šablona" - padlo s `B92`.) | XS |
| **B96** | **Datum platnosti: MHD tiskne lomítka, AD tečky** (nález při `B95`, 17. 8.). Zastávková vývěska MHD dá do „Datum platnosti od" `01/11/2018`, AD `11.12.2022`. Česky se datum píše s tečkami, takže MHD tvar je pravděpodobně chyba - jen **je ve vydané verzi**, proto to `B95` nechalo být a linkový MHD srovnalo na tentýž (nesprávný) tvar, aby výstupy nešly proti sobě. Druhá polovina: zastávkový MHD má ve **vzorových datech pro náhled** tečky, ale s ostrými daty vyjde lomítko - náhled tedy ukazuje jiný tvar než tisk. ⚠️ **Nejdřív rozhodnout:** oprava se dotkne vydané vývěsky (zlaté odtisky se přebaselinují) a zákazník uvidí změnu tvaru data. | S + rozhodnutí |

**Vědomě mimo tuhle sadu:** testy linkových agend (`B14b`) - dělají se průběžně, až se na linkové
půjde, ne dopředu. Rozřezání `TemplateDesignerViewModel` (3 543 ř., 11 rolí) - **při dotyku**, ne
jako položka; první přirozený řez je role „parametry sestavy a načítání dat", kam sáhne `B60`.

---

## 🚌 Zastávkové JŘ

Stav tiketů, parkované položky i věci „jen na poptávku" drží
[`customer-requirements §0`](customer-requirements-zastavkovy-jr.md).

---

## 📥 Backlog

### Tabulkové prvky

Nezávislé na JŘ (faktury i obecné šablony). Cíl pevné tabulky **není faktura, ale náhrada
„poskládám to z textových polí"**. Model = **objekt na stránce s excelovskými klávesami**
(Word/PowerPoint), ne tabulkový procesor - žádný řádek vzorců ani odkazy typu A1.

#### `B76` Sloučení buněk vodorovně (S, poslední krok bloku)

**Hotové jsou první tři kroky** (17. 8., detail v changelogu): formátování buňky, standardní
formulář vybrané buňky (panel i Alt+Enter) a **výběr obdélníkové oblasti tažením** se vším, co nad
ním jede - hromadné formátování, Delete a schránka čitelná Excelem.

**Zbývá sloučení vodorovně** (ColSpan): ze tří buněk jeden nadpis přes celou šířku. Aditivně
v JSON, kreslení řádku musí umět span. Otevřené k rozhodnutí: **co s obsahem** slučovaných buněk
(návrh: spojit neprázdné mezerou, ať se nic tiše neztratí; Excel zahazuje, Word spojuje) a že
rozdělení zpátky vrátí obsah do první buňky.

⚠️ **Sloučení přes řádky vědomě odložené** - míchá se se spanováním výšek, střídáním řádků
a podmíněnou viditelností.

### Import PDF - doladění

| Co | Stav |
|---|---|
| **Obecný import: detekce tabulek z mřížky čar** - místo 50 čar + 20 textů jeden tabulkový element. Cílem je **pevná tabulka**, která od 12. 8. žije (prvek v paletě, obsah se vyplňuje na plátně), takže gate padl a záleží jen na prioritě | ⬜ |
| **JŘ import: ladění na širší sadě JŘ** (jiné layouty než KV_DP vzor - jiná města, SK, netypické legendy) | 🟡 |

Křivky/Bézier v obecném importu - low-prio, bez poptávky.

### AI integrace - v pozadí

> **Sekundární, ne core.** Aktuální funkce (pomocník, generátor šablon, AI Vision import PDF - vše Sonnet 5
> přes EDISONAI/mlweb spravovaný klíč) zůstávají funkční; další rozšíření až budou reální uživatelé
> s potřebou. JSON šablona + DataSchema jsou stabilní kontrakt pro AI - rozšíření kdykoli bez breaking
> changes. **Princip: copilot, ne autopilot.**

**Otevřené (post-release):** ostrá zkouška EDISONAI cesty bez lokálního klíče u první firmy ·
potvrzení `source: "templatedesigner"` u autora mlwebu · zveřejnění generátoru šablon
(menu dnes jen pro vývojáře) až po otestování na reálných zadáních.

**Eval sada AI pomocníka** (průběžné doškrtání; pomocník je už uživatelsky ověřen, tohle je regresní
sada) - u každé otázky hodnotit: správná cesta v UI / žádné vymyšlené featury / rychlost odpovědi.
- *Lookup:* „dvě vývěsky na A4" → Ctrl+P Uspořádání na list · „linka do vlastního PDF" → Ctrl+P Členění + Název souboru ·
  „stránka na A5" → Ctrl+Shift+P · „kopírovat styl" → Ctrl+Shift+C/V · „sloupec cena×počet" → výraz `{CENA}*{POCET}` ·
  „zvýraznit řádky >1000" → zvýraznění řádků · „víc linek k tisku" → chip Instance · „změnit agendu" → Ctrl+Shift+A ·
  „červený rámeček náhledu" → přetečení obsahu · „součet pod tabulku" → `SUM(...)` · „schovat sekci na 1. str." → VisibilityExpression ·
  „vrátit smazaný prvek" → Ctrl+Z · „písmo celé tabulky" → nastavení prvku, „Styl buněk" · „šablona pro kolegu" → Ctrl+S do DB.
- *Chytáky* (očekává „zatím neumíme", ne vymyšlený návod): sloučení buněk · export do Excelu · PDF e-mailem ·
  brožura/duplex · vlastní font · import staré šablony z EDISONu.
- *Nejistota* (odkázat na podporu, ne halucinovat): „špatný čas u 3. zastávky linky 12" (datový problém, ne UI).

**Budoucí:** MCP server (`get_template`/`apply_template`/`add_element`/… tenká vrstva nad ViewModelem, každý
tool → `_vm.Execute(command)`) · onboarding průvodce · smart suggestions (TableElement → sloupce z DataSchema) ·
migrace starých Caché sestav · template linting v CI/CD.

### Ostatní

- **`B65` Kalendářový výběr období u sloupce vývěsky** (S, rozhodnutí Jan 10. 8.: do první verze
  stačí text). Dnes se rozsahy dat píšou ručně („14.2.-18.2., 1.7.-31.8.") a tvar hlídá validace
  při OK. Až se featura usadí, nahradit vybíráním v kalendáři - u „dnů školního vyučování" je ruční
  psaní desítek rozsahů otravné. Tvar zápisu na wire se tím nemění, jde čistě o zadávání.

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

- **`B42` Co všechno umí import z předlohy nastavit** (M, nejdřív analýza; podnět Jan 31. 7. při `B41`).
  Import dnes umí **pozice a prvky** plus část vzhledu (fonty, výšky řádků, barvy záhlaví, sloupce).
  Otevřená otázka: umí z předlohy rozumně odvodit i **parametry sestavy** a **další nastavení prvků**
  (pozice značek, šipka, sloupce jako zóna/označník, varianty tras, skupiny dnů)? Je to velká
  a netriviální část importu, takže se to má projít vědomě, ne po jedné vlastnosti.
  ⚠️ **Omezení už neplatí tak, jak se dřív psalo:** hodnoty parametrů sestavy se od 15. 7. ukládají
  **i do šablony** a mají přednost před per-agenda nastavením (`SyncParametersToTemplate`), takže
  import by je nastavit mohl, aniž rozhodí ostatní šablony té agendy. Zůstává druhá past: uloží se
  i hodnoty, kterých se nikdo nedotkl - detail u #12843 v `customer-requirements §0.1`.
  **Malý krok, který jde udělat hned (S):** detekci časové osy z předlohy vrátit a její výsledek
  **napsat do protokolu importu** („předloha má osu 5-23, nastavte ji v parametrech"), ať se informace
  neztratí a dostane se k tomu, kdo s ní může něco udělat. Původní detekce (`timeAxisRangeFrom/To`
  v `AiGuidedScheduleBuilder`) je v gitu před commitem `B41`.

- **Headless tisk (A3)** - „vytiskni linku X" z modulu přes PrintEngine fasádu (šablona+data→PDF
  bez stavu editoru). Dnes tisk jen v návrháři. Gate R4/R7.

- **Samostatný distribuční kanál** systémových šablon mimo `.exe` - v1 doručuje embed→MLINE;
  „jiný kanál" (server push / admin) jen vymění zdroj, picker/UX beze změny.

- **Externí datové zdroje** (analýza 2026-07-12, na poptávku) - architektura připravená (engine zná jen
  `Template + DataContext`; DataContext je zdrojově agnostický JSON). Tři švy: Caché gateway agenda
  (0 změn .NET) · .NET provider vedle Caché (vzor `JsonFileDataSchemaProvider` + Composite) · headless
  A3 („POST šablona+JSON → PDF" = externí zdroj bez adaptéru; **A3 je nejpravděpodobnější nosič**).
  První krok s nejlepším poměrem cena/hodnota: **generický JSON zdroj (soubor/URL)** - tržní minimum
  (CSV/Excel/JSON/REST), náš wire formát už JSON je; využilo by i AI + testování šablon. Dořešit:
  credentials, schéma z dat, změkčení předpokladu „běží nad Caché".

- **Tabulkové featury (bez customer demand):** grouping (`GroupHeader`/`GroupFooter`/`Summary`) ·
  vnořené datasource (`_rows` v `_rows`).

- Font warning + ad-hoc overflow (YAGNI).

---

## 🚆 Linkové a knižní JŘ (náhrada TiskJR)

> ⚠️ **Gate: R1 (sdílené tabulkové jádro), R2 (layout zodpovědnosti), R5 (embedded fonty)** -
> [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md). Začíná se **designem
> sdíleného jádra** (StopList + DynamicTable + LineTable), ne kódem elementu.
>
> Bere se **vcelku**, ne po kusech (rozhodnutí Jan 4. 8.): `B10` sám o sobě nic nedoručí, protože
> bez `B13` se široká tabulka stejně nevytiskne.

Legacy `TiskJR` generuje **vývěsní i knižní JŘ z téhož enginu**, takže je to jeden track. Formát:
sloupcová tabulka celé linky (svislý seznam zastávek × sloupce spojů s časy; řádek platností se
symboly/NZ; Vyčká/Navazuje; skupinová záhlaví PD/So/Ne; 10 druhů poznámek). **Gold oracle `mkPLinWin`
(ALVA.JRMAIN.MLAPI)**, metodika = čistá reimplementace à la `ZastavkoveJrAd` (~1 měsíc).

Datové jádro (`LinkovyJrAd`/`LinkovyJrMhd`), `LineTableElement`, plán řádků a auto-fit fontu jsou
hotové. Výchozí šablona pro **AD** se od `B92` distribuuje jako systémová (nalije se sama, v nabídce
se ukáže až s `Released = 1`). **Pro MHD výchozí šablona zatím není** - až se bude dělat, pozor na
dvě věci níže.

⚠️ **Agendy jsou úvodní draft** (`Released = 0`), takže se v nich smí měnit i to, co je jinde
zamčené. Zastávkové se přitom nesmí dotknout - viz pravidlo u revizní sady nahoře. Slovník polí
je od 17. 8. srovnaný se zastávkovým (`B95`, archiv) a hlídá ho `AgendaSchemaDriftTests`; nová
pole přidávej pod týmž názvem, jaký pro tutéž věc používá vývěska.

**Pro budoucí linkovou šablonu MHD** (z `B95`): `{CISLO_LINKY}` je **licenční** číslo, protože tak
to má vývěska - veřejné označení je pod `{LOKALNI_OZNACENI_LINKY}`. A `{DATUM_PLATNOSTI_OD}` vyjde
u MHD s lomítky (`01/11/2018`), viz `B96`. Existující AD šablony se ani jedno netýká: AD posílá
jediné číslo linky a datum už dřív s tečkami.

| # | Blok | Vel. |
|---|---|---|
| **B10** | **Symbolový font - linková část** (gate R5). Linková tabulka dnes symboly **nekreslí vůbec** - agenda posílá legacy klíče (`|`, `<`, `m`, `u`…) a chybí převod na pozice ve fontu. ✅ **Podklady hotové:** katalog 66 symbolů, render-módy i **změřený repertoár fontu** (94 pozic z obou fontů) jsou v `elementy-architektura.md`, takže neznámá „který kód nese který piktogram" je pryč. ⚠️ Pozice `znak` z katalogu se s pozicí ve fontu často **neshoduje** (letadlo `n`, paragraf `P`, metro `#`) - musí jít přes mapu, ne přes písmeno. **Rozsah (rozhodnutí Jan 4. 8.):** sdílená mapa `značka → (render-mód, hodnota)` pro všechny prvky, nahradí i dnešní `SymbolGlyph` u zastávkových vývěsek → dotkne se běžícího zastávkového JŘ, chce zlaté odtisky. Culture rozlišení jen pro **3 pozice** (`D`, `w`, `x`). Ve fontu chybí loď, wifi a zásuvka. | M→S |
| **B13** | **2D bin-packing / `Strih`** (gate R1/R2) - široká tabulka (MHD linka = 100+ spojů) se dnes zmáčkne na šířku místo rozřezání do stránkových bloků; + flow paketů, „dotisk zbytku legendy na další stránce". Nutné pro reálný tisk MHD linek; zastávkový JŘ to nepotřebuje. Stojí na hotovém `B11`: řádková vrstva existuje, takže rozřezání se píše **jednou** nad plánem řádků, ne pětkrát v per-element smyčkách. | L |
| **B14** | **Dedup Caché tiskových enginů + jejich testy** - doprovod ostrého knižního JŘ, ne dopředu. (a) **Dedup** (*audit H1*): různé signatury `AddAnotace`/`BuildPoznamka`, ~15 helperů MHD↔AD + AD `^JRL` readery → vyextrahovat sdílené readery + `Build*` do `BaseAgenda`/`JrShared`; je to **Caché analog R1 extrakce jádra** (`B13` řeší .NET render-side, tohle Caché data-side). Korektnostní riziko. Menší výsek Build\* helperů = `B56` (v Backlogu); při realizaci jednoho zredukovat druhý. ⚠️ **Korekce z analýzy 5. 8.:** dřívější tvrzení „`CasHodnota` bere `"|"` jinak" **neplatí** - obě verze jsou funkčně identické, lišil se jen komentář. A objem duplikace je menší, než audit odhadoval: z 27 stejně pojmenovaných helperů je jen 15 doslovných kopií (~97 řádků). (b) **Testy** (*audit H2*): `LinkovyJr*` (~2 000 ř.) 0 testů, `ZastavkoveJrMhd` 1 helper; AD gold gate tiše projde bez `CB_CSADAD`, křehký digest nad živými daty (pinuje 2/22 linek), `tools/compare-gold.ps1` neexistuje → smoke + frozen-input gold testy, bezpodmínečný gate (zmrazit vstup do izolovaného ns). | L + M |
| **B60** | **Název zastávky se v linkovém JŘ tvoří jinak než na vývěsce** (nález analýzy `B56`, 5. 8.) - vývěska respektuje volbu tvaru názvu (#13089, 4 volby) i přepínač „použít upravený název pro MHD"; linkový JŘ bere název natvrdo, takže **táž zastávka může mít na dvou výstupech jiné jméno**. ⚠️ **Změřeno 5. 8. - není to malá věc:** tvarování visí na **dvou parametrech sestavy** (`TVAR_NAZVU_ZASTAVKY`, `NAZEV_PRO_MHD`) načítaných do `^||TiskAd`, takže srovnání = přidat linkovému JŘ dva uživatelsky viditelné parametry (popisky enumu jsou po vydání zamčené) + sdílet 5 helperů (`FormatNazvuZastavky`, `MezeryPoInterpunkci`, `TvarNazvuZastavky`, `PouzitNazevProMhd`, `NactiNastaveniNazvu`) - a to **bez jakéhokoli testu** (linkové agendy 0 testů, síť staví `B14b`). **Nejdřív rozhodnout, jestli ty parametry v linkovém JŘ chceme.** ⚠️ **Širší kontext z revize 17. 8.:** není to jen o názvu zastávky - linkový AD má **jeden** parametr sestavy a linkový MHD **žádný**, proti sedmi u zastávkového AD. Uživatel dnes dostane tabulku, se kterou nemůže nic udělat. Rozhodovat se má celá sada parametrů najednou, ne dva zvlášť; sveze se s `B95` (týž kód, totéž rozhodnutí „co má být stejné jako u zastávkového"). | M |

### Odložené body z pilotu (seřazené dle hodnoty pro ostré nasazení)

| # | Co | Proč odloženo / dopad | Odhad |
|---|---|---|---|
| C5 | **Konfigurovatelné pořadí poznámek** (`PoznamkyPoradiDruh`) - legacy AD parametr; TD má pevné pořadí druhů | per-zákaznická variace, řešit až s poptávkou | S |
| C6 | **Turnusy, obousměrný tisk, souhrnné JŘ (řádek zdrojové linky)** - legacy volby `in(arr,6/7)`, MHD `MakeData` je nemá | mimo scope pilotu; validovat, zda je zákazník chce, než portovat | - |
| C7 | **Rozdvojení příjezd/odjezd - jemný layout** + fixed-width padding sloupce (legacy `arr,13`) | kosmetika; TD řeší podřádky, ne padding | S |

Detail datových odchylek a mezer viz [`linkovy-knizni-jr-implementace-analyza.md`](linkovy-knizni-jr-implementace-analyza.md) §11–12.

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
| F3 | **TableElement na RG úroveň** (navazuje na dynamickou tabulku + `B76`): per-řádek zvýraznění 0-5 z dat, custom barvy per řádek/buňka, overflow módy (zalamování → klonování řádků), opakování mezisoučtu na další stránce, datové typy sloupců (datum/minuty/záporné červeně/skrýt nuly), řádek-jako-layout | ⬜ L |
| F4 | **Výstupní kanály fáze 2** - Excel export, PDF heslo + el. podpis (PDFSIGNER), Epson znakový tisk | ⬜ dle poptávky |
| F5 | **Jazykové varianty šablon** - per-jazyk kopie provázaná na CS zdroj | ⬜ |
| F6 | **Vědomě neportovat bez poptávky** - grafy (12×), AZTEC (2×), calculated (2× - výrazy TD nadmnožina), checkbox (8×); ověřit + škrtnout | ⬜ validace |

---

## 🗄️ Hotová práce

Detail dokončených etap → [`roadmap-archive.md`](roadmap-archive.md). Průběžný changelog → [`changelog.md`](changelog.md).
