# Roadmap

> **Stav k 2026-07-31** (předchozí kolo dojeté - #7947, #7955, #7959, #13048 i #10660 zavřené;
> **30. 7. přišlo nové kolo zpětné vazby KOVED/IDZK** - 9 tiketů, viz Vlna 0).
> **Jen otevřená / akční práce.** Cokoli hotového → [`roadmap-archive.md`](roadmap-archive.md),
> průběžný changelog → [`changelog.md`](changelog.md). Hotové věci sem nevracet - patří do archivu.

> **▶ Další session:** **B28** (odpověď BBSK bez vývoje) a dál Vlnou 0 - KOVED kreslí vývěsky
> v návrháři naostro a fronta se znovu naplnila. Linkový JŘ (**B10**, **B13**) jde až po ní.
> Follow-upy „na vyžádání zákazníka" žijí
> v [`customer-requirements §0.4`](customer-requirements-zastavkovy-jr.md).

---

## 🧭 Jak podle roadmapy pracovat

Práce je rozřezaná na **bloky velikosti „jedno sezení"** (`B10`, `B13`, …) seřazené do **vln**.
Velikosti: **S** = do hodiny · **M** = půl dne · **L** = víc dní.

**Nová session:** vezmi nejnižší nezablokovaný blok z nejnižší nedokončené vlny. Po dokončení:
changelog + přesun bloku **kompletně** do archivu (ne odškrtnutý řádek zde).

**Pravidla, aby roadmapa znovu nenarostla:**
0. **Číslo bloku je globální a nikdy se nerecykluje** - před založením grepni `B<č>` v roadmapě
   **i v archivu** (jinak vznikne druhý „B18“ a review se rozejde s tím, co kdo myslel).
1. **Nic nevstoupí bez velikosti a čísla bloku** - volný text bez velikosti je přesně to, co se nahromadilo.
2. **Zákaznická položka má uzavírací podmínku** („co ověřit, aby se dala zavřít") - jinak zůstane „hotová, ale otevřená".
3. **Linkový JŘ má ukazatel `L‑1…L‑4`** (Fáze C+D) - velký kus rozřezaný na slotové kroky, aby v každé vlně postoupil.

---

## 🌊 Vlna 0 - zákaznická fronta KOVED/IDZK (kolo 30. 7. 2026)

KOVED (IDZK, Lábsky) začal vývěsky kreslit v návrháři naostro a poslal zpětnou vazbu po blocích.
Většina je doladění toho, co už tiskneme (vzhled, tvar textu, chybějící pole); dva body jsou velké
koncepty a leží ve Vlně 3. Řazeno **od nejrychleji zavíratelného** - cíl je zmenšit počet tiketů,
ne odbavit nejhezčí featuru.

| # | Blok | Vel. | Uzavírací podmínka |
|---|---|---|---|
| **B28** | **#13070 BBSK - jen odpověď** (jak se v šabloně řeší variantní trasy / zacházky a jak přepnout vývěsku na výšku; obojí návrhář umí). Nula vývoje. | S | odpověď s postupem, případně krátký hovor → zavřít |
| **B29** | **#13045a - ověřit ohlášený chybný výstup:** v nedělním sloupci vidí sobotní odjezdy a u spojů značku „6", i když ji nadpis sloupce už říká. Koncept vlastních skupin dnů = `B39`. | S | doložit na jejich lince (oprava, nebo vysvětlení, proč to tak je) + tiket zúžit na `B39` |
| **B30** | **#13091a - vzhled seznamu zastávek:** nastavitelná velikost / tvar / tloušťka a přerušení šipky směru, svislé linky mezi sloupci (tloušťka, přerušení, barva), u projetých zastávek netisknout záporné časy (radši nic). | S | klient vidí ve své vývěsce |
| **B31** | **#12997 - dotáhnout barvu linky z dat:** `{BARVA_LINKY}` půjde od příští verze do textu i pozadí, ale **u zastávek a odjezdů se barva zadat nedá** (editor sloupců nemá textový vstup na barvu) - to je nález Fáze B z 30. 7. Plus chtějí zvlášť barvu čísla linky a zvlášť pozadí. | S–M | klient obarví číslo linky hodnotou z dat |
| **B32** | **#13089 - tvar názvu zastávky jako volba:** plný CIS tvar s dvojčárkou (technické exporty, soutěže) · bez dvojčárky · **typograficky vylepšený** („Zlín, aut. nádr." - mezera za čárkou a tečkou, pokud dál nenásleduje číslice). Plus vypnutelný alternativní MHD název. Jedno pravidlo pro všechna textová pole agendy, ne jen pro seznam zastávek. | S–M | KOVED potvrdí tvar na svých linkách |
| **B33** | **#13008 - popis dopravce:** každý dopravce na vlastní řádek, volitelně výčet spojů, před popisem celé číslo linky dle licence, bez našeho uvozujícího „Zajišťuje". Část pro souhrnné JŘ závisí na #13099 (mimo TD). | S–M | klient si popis složí bez ručních úprav |
| **B34** | **#13091b - zóny:** dnes se plní jen z trasy linky (ne z nastavení konkrétní zastávky) a tiskne se písmeno přepravního systému místo čísla zóny z číselníku. Zóny jsou proti legacy ověřené, tohle je jiný zdroj → nesmí rozbít gold paritu. | S–M | číslo zóny odpovídá číselníku na jejich datech |
| **B35** | **#13088 - nová pole agendy:** CIS čísla zastávek (výchozí / aktuální / cílová) · **číslo sloupku** (pro ně jiný údaj než označník, který už máme - nejdřív s JŘ vyjasnit, co v datech je) · výlukové texty jako oddělené pole (do CIS chodí jako poznámka „Výluka: …", na vývěsce je chtějí zvlášť). | M | pole jsou v nabídce agendy a plní se |
| **B36** | **#13093a - poznámky, snadná část:** textové poznámky zadané ve vlastnostech linky se do zastávkové vývěsky nedostanou vůbec (linkový JŘ je čte) - druhý blok poznámek si klient složí sám, filtr podle typu poznámky legenda umí · automatická výška bloku poznámek se zmenšením textu při přetečení. | M | klient složí dvojici bloků poznámek nad sebou bez ručního dopočítávání výšky |
| **B37** | **#13093b - poznámky, datově náročná část:** cyklobusové poznámky a rezervace u odjezdů (chybí zdroj) · **pokračující spoje** sloučené do jedné poznámky podle stejného cíle a linky, ne podle čísla spoje („pokračuje do zast. …, l. 304"), volitelně zapínatelné. | M–L | klient si zapne a poznámka odpovídá jejich ruční praxi |

**Mimo návrhář (vést jako práci JŘ, u nás jen navázat výstup):** **#13099** souhrnné JŘ (sdružení
linek se stejným koncovým trojčíslím pod vzorovou linku - dnes ručně skriptem v Excelu; pro KOVED
podmínka přesunu tvorby vývěsek do EDISONu, částečně blokuje `B33`) · **#13102** virtuální
prodloužení spoje jen pro vývěsku (zadávání v JŘ, přiřazeno Petrovi).
**#13098** je můj sběrný tiket - dnes v něm visí jen odsazení zastávky per směr = `B12` / #7937.

---

## 🌊 Vlna 1 - zákaznické velké + odemknout linkový

| # | Blok | Vel. |
|---|---|---|
| **B10** | **L‑2: symbolový font - linková část** (`ZNAK` sloupce LineTable: multichar/kompozity, převodní tabulka `GSId` 66+ symbolů, TTF v `C:\DEV\EDISON\TiskJR\_ruzne`). Zastávkový JŘ hotový 9. 7. | M→S |
| **B12** | **#7937 rozšíření** - odsazení zastávky per-směr / per-Tč pásmo (`^JRL` dta7 je single-per-point → změna zdroje + editor; base AD hotový). Jen když TRANSDEV tlačí. | M |
| **B18** | **Deklarativní pravidla EDInputFormu** (kapabilita sdílené komponenty, celý EDISON) - dnes umí formulář reagovat na vyplněné hodnoty jen kolem přes server (`Validate`, ručně per formulář). Zavést deklarativní vrstvu: začít **viditelností** (`visibleWhen`), gramatiku sdílet s tiskovými parametry (`POLE=hodnota` + nerovnost) - nezavádět druhý dialekt. Povinnost/povolení až s poptávkou; **skutečná validace zůstává imperativní** (potřebuje data a dopočty). Rozhodnuto 29. 7.: samostatný blok, ne součást B4. **Follow-up:** migrace `CellStyleEditor` na Caché-driven formulář (S–M; layout sjednocen v B6, háček je tri-state / reaktivní logika - bez B18 nejde). | M |

---

## 🌊 Vlna 2 - velký kus linkového (dedikovaně)

| # | Blok | Vel. |
|---|---|---|
| **B13** | **L‑4: 2D bin-packing / `Strih`** - široká tabulka (MHD linka = 100+ spojů) se dnes zmáčkne na šířku místo rozřezání do stránkových bloků; + flow paketů, „dotisk zbytku legendy na další stránce" | L |
| **B14** | **Dedup Caché tiskových enginů (C8) + testy tiskových enginů (C9)** - doprovod ostrého knižního JŘ, detail v tabulce Fáze C+D | L + M |

L‑4 stojí na hotovém `B11`: řádková vrstva existuje, takže rozřezání se píše **jednou** nad plánem
řádků, ne pětkrát v per-element smyčkách.

---

## 🌊 Vlna 3 - velké koncepty z kola KOVED

Dva body z fronty Vlny 0, které nejsou doladění - každý chce vlastní plánování.

| # | Blok | Vel. | Pozn. |
|---|---|---|---|
| **B38** | **#13097 rozdělení seznamu zastávek na pokračování** - seznam vč. všech sloupců rozřezat do několika oddílů. Je to **tentýž mechanismus jako `B13`** (rozřezání široké tabulky u linkového JŘ) → gated na něj, ať se nepíše dvakrát. | L | dělat s `B13`, ne zvlášť |
| **B39** | **#13045b vlastní skupiny dnů** - dnes pevné pracovní dny / soboty / neděle. Klient chce vlastní skupiny s obdobími („Vánoční provoz 27.-29. 12.", „Prázdniny" s několika rozsahy, „Dny školního vyučování"), spoje se do sloupce vyberou podle období, a značky, které nadpis sloupce už říká, se netisknou; výjimka u spoje značku zobrazit má. Mění datový model + chce editor. | L | nejdřív `B29` (ověřit dnešní chybu), pak koncept |

---

### 📥 Backlog / budoucí velké tracky

- **Zastávkový JŘ business** (parkované helpdesk A2-A5/A6/B4 + TRANSDEV sada) → viz sekce „Zastávkový JŘ - business logika" níže
- **Fáze B - odložené:** **oživení fixní tabulky** (paleta + Fixed editor) + **merged cells** (gated na ni).
- **Tabulkové featury (bez customer demand):** grouping (`GroupHeader`/`GroupFooter`/`Summary`) ·
  vnořené datasource (`_rows` v `_rows`).
- **`B27` Řazení řádků v dynamické tabulce** (S–M; podnět kolegy, bez zákaznické poptávky) - dnes se
  řádky tisknou v pořadí, v jakém je vrátí agenda; uživatel je nemá jak přeřadit. Rozsah prvního
  kroku: **primární sloupec + volitelně sekundární**, vzestupně/sestupně, jako vlastnost prvku
  (aditivní JSON, bez migrace). Dvě věci rozhodnout dřív než se to začne psát: (1) řadit podle
  **hodnoty pole**, ne podle vykreslené buňky - jinak formát a placeholdery pořadí rozhodí, a typ brát
  ze schématu (číslo/datum/čas ≠ text); (2) **klientsky v prvku**, ne v agendě - serverové řazení by
  se muselo dělat per agenda. Platí jen pro **dynamickou tabulku**; JŘ prvky mají pořadí dané trasou
  a ta se řadit nesmí.
- **Fáze C+D** linkové/knižní JŘ · **Fáze E** vozový + Relace · **Fáze F** REPORTGENERATOR - velké
  tracky, každý gated rozhodnutími R1–R7 (viz [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md))
- **Externí datové zdroje** (analýza 2026-07-12, na poptávku) - architektura připravená (engine zná jen
  `Template + DataContext`; DataContext je zdrojově agnostický JSON). Tři švy: Caché gateway agenda
  (0 změn .NET) · .NET provider vedle Caché (vzor `JsonFileDataSchemaProvider` + Composite) · headless
  A3 („POST šablona+JSON → PDF" = externí zdroj bez adaptéru; **A3 je nejpravděpodobnější nosič**).
  První krok s nejlepším poměrem cena/hodnota: **generický JSON zdroj (soubor/URL)** - tržní minimum
  (CSV/Excel/JSON/REST), náš wire formát už JSON je; využilo by i AI + testování šablon. Dořešit:
  credentials, schéma z dat, změkčení předpokladu „běží nad Caché".
- AI (v pozadí) · font warning + ad-hoc overflow (YAGNI)

**Otevřené otázky k Fázi E/F** (Epson, RG bridge, Relace, grafy/AZTEC, jazykové varianty) → evidované
v [`legacy-nahrada.md §7`](legacy-nahrada.md); vyřeší se
při scope té fáze, ne teď.

---

## 🚀 Produkční / provozní - otevřené

1. **Headless tisk (A3)** - „vytiskni linku X" z modulu přes PrintEngine fasádu (šablona+data→PDF bez stavu editoru). Dnes tisk jen v designeru. Gate R4/R7.
2. **Samostatný distribuční kanál** systémových šablon mimo .exe - v1 doručuje embed→MLINE; „jiný kanál" (server push / admin) jen vymění zdroj, picker/UX beze změny.
3. **Další výchozí šablony** (MHD + linkový) = **krok 44b / C4** (linková varianta v L‑tracku Fáze C+D);
   mechanismus embed→MLINE hotový, zbývá kurátorovat + přesunout JSON do `SystemTemplates/`.
4. **Zákaznická dokumentace na wiki (post-release)** - seedy hotové (`uzivatelska-prirucka.md`,
   `plan-a-zmeny.md`); zbývá **obrázky + publikace**.

---

## 🅿️ Zastávkový JŘ - zákaznická fronta

> **Plný stav** (hotové featury + co otestovat pro uzavření helpdesk tiketů) →
> [`customer-requirements-zastavkovy-jr.md §0`](customer-requirements-zastavkovy-jr.md). Zde jen **otevřená
> akční práce**. Vel.: S = do hodiny, M = půl dne, L = víc dní.

### Čeká na někoho jiného - postrčit, není to práce

- **TR2** implementováno (opt-in `SOUHRN_NA_ZNAMENI`), ⚠️ čeká SK ověření kódování na-znamení - kolega
- **#13048 bod 4 fáze 2** (duplikace řádku u víc nástupišť) - čeká potvrzení KOVED
- **TR8 („<1" místo „0") park** (analýza 2026-07-10): naše časy jsou kumulativní, 0-segment se tiskne
  jako opakované číslo, ne „0". Per-segment sloupec TD nemá. Blokuje potvrzení od TRANSDEV
  (který sloupec - legacy „Průběhy"?). Detail → `customer-requirements §TR/8`.

### Reálná zbývající práce

`B12` #7937 per-směr / per-Tč odsazení (jen když TRANSDEV tlačí)

**Kolo KOVED z 30. 7. drží Vlna 0** (`B28`-`B37`) **+ Vlna 3** (`B38`, `B39`) - sem se nekopíruje.

### Bez poptávky nebrat - chce analýzu / datový model / business rozhodnutí

| # | Co | Vel. | Co potřebuje |
|---|---|---|---|
| TR10 | Skrýt spoje o So/Ne/svátcích | S–M | Caché filtr |
| TR7 | Per-písmeno výběr sloupců variant jízdních dob | S–M | jen pokud TRANSDEV potvrdí nad novým chováním (varianty dle trasy+nástupiště s max časy jsou od 27. 7. hotové) |
| TR6 | Barva linky (bg/text ve vlastnostech linky → volba v šabloně) | M | nový datový model |
| TR9 | Obrácená logika poznámkování (rozsah TČ v 1. tabulce) | M | Caché-input (Studio JŘ) |
| TR5 | Definice cíle - střední (per zast.×směr) + ideál (DB cílů) | M–L | datový model; *(min „cíl per směr" = hotovo přes `POPIS_SMER`=konečná)* |

*(Follow-upy uzavřených tiketů „na vyžádání zákazníka" → [`customer-requirements §0.4`](customer-requirements-zastavkovy-jr.md).)*

---

## 🟠 Fáze B: Element flexibility - jen odložené body

Z gap analýzy vůči komerčním generátorům. Nezávislé na JŘ (faktury i obecné šablony).

**Nález 2026-07-30 (S, bez poptávky nebrat):** u **zastávek a odjezdů** se pozadí a barva textu
záhlaví/buněk nedají nastavit jinde než po jednotlivých sloupcích - pole „Pozadí záhlaví" / „Pozadí
buněk" jim v tabu Obecné chybí (legenda poznámek i linková tabulka je mají). Podle `CLAUDE.md` mají
být 4 základní barvy zón dostupné napříč tabulkovými prvky, takže to vypadá na drift z migrace na
formuláře. Důsledek navíc: barvu z dat (`{BARVA_LINKY}`) tam nelze zadat vůbec, protože editor
sloupců textový vstup na barvu nemá (rozhodnuto 30. 7. nechat - doplnit až s poptávkou).

**Fixní (statická) tabulka - implementace odložena na poptávku faktur / F3.** Analýza 2026-07-12
rozhodla „koncept implementovat" (tabulka dává reflow podmíněných řádků, auto výšku řádku a
strukturální editaci, což skupina elementů principiálně neumí). Model i render Fixed módu jsou hotové
a na sdíleném jádru; pro JŘ to potřeba není.

- **Krok 1 - střízlivé oživení (S–M):** paleta „Tabulka (pevná)" + factory (`Mode=Fixed`, Mode v UI
  zamčený - dynamickou dělá `DynamicTableElement`); opravy nálezů: `Clone()` zapomíná `SummaryRows`,
  `RowVM.ToModel()` zahazuje per-cell `Format`+`Style`; render dorovnat na **plán řádků** (`RowSpec`; `B11` ji vědomě vynechal - kreslí svislou čáru za
  každým sloupcem včetně vnějšího a vrství dvě pozadí u pevných řádků, takže sjednocení jí výstup
  změní a jako uspaný element není v odtiscích)
  (sjednotí mřížku, auto-scale, **předpoklad pro merge**); drift-test allowlist zúžit.
- **Krok 2 - UX modernizace + merge (M, s poptávkou):** inline editace buněk na plátně (dvojklik,
  à la F2 na TextElementu - standardní pocit z FastReportu, řeší „5 tabů je moc"), redukce dialogu
  na 2-tab vzor, **ColSpan** merged cells (aditivní JSON, span-aware DrawRow). **RowSpan vědomě
  odložen** (vzácný, těžký - spanování výšek × zebra × VisibilityExpression) - až na poptávku / F3.

---

## 🟠 Fáze C + D: Výlepový/knižní JŘ linky (náhrada TiskJR) - pravděpodobně JEDEN track

> ⚠️ **Gate: R1 (sdílené tabulkové jádro), R2 (layout zodpovědnosti), R5 (embedded fonty)** -
> [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md). Krok 43 začíná
> designem sdíleného jádra (StopList + DynamicTable + LineTable), ne kódem elementu.

Legacy `TiskJR` generuje vývěsní i knižní JŘ z téhož enginu → C + D je nejspíš jeden track. Formát:
sloupcová tabulka celé linky (svislý seznam zastávek × sloupce spojů s časy; řádek platností se
symboly/NZ; Vyčká/Navazuje; skupinová záhlaví PD/So/Ne; 10 druhů poznámek). **Gold oracle `mkPLinWin`
(ALVA.JRMAIN.MLAPI)**, metodika = čistá reimplementace à la `ZastavkoveJrAd` (~1 měsíc).

**Zbývající prerekvizity:** (1) symbolový font - **linková část** (`GSId` 66+ symbolů na
multichar/kompozity, TTF v `C:\DEV\EDISON\TiskJR\_ruzne`) = `B10`; (2) layout rozšíření (flow paketů,
„dotisk poznámek z minulé stránky") = `B13`; (3) per-linka šablony = `C4`.
Datové jádro (`KnizniJrAd`/`KnizniJrMhd`), `LineTableElement`, plán řádků a auto-fit fontu jsou hotové.

### 🎯 Ukazatel postupu `L‑1…L‑4` - velký kus rozřezaný na slotové kroky

**Smysl:** aby linkový JŘ postoupil v **každé vlně**, i když běží helpdesk. Každý krok končí něčím
dokončeným; velký `L‑4` se otevírá až nad hotovým `L‑3`.

| Krok | Blok | Co | Vel. | Stav |
|---|---|---|---|---|
| **L‑2** | `B10` | Symbolový font - linková část (`ZNAK` sloupce, multichar/kompozity, `GSId` 66+) | M→S | ⬜ |
| **L‑4** | `B13` | 2D bin-packing / `Strih` - rozřezání široké tabulky do stránkových bloků | L | ⬜ **odemčeno** |
| - | `C4` | Linková výchozí šablona do distribuce (dá se přilepit ke kterémukoli slotu) | S | ⬜ |

Odložené body `C2`–`C9` níže jsou detailní evidence týchž kroků (`C2` = L‑2, `C3` = L‑4, `C8`+`C9` = `B14`).

### Odložené body knižního/linkového JŘ (z pilotu, seřazené dle hodnoty pro ostré nasazení)

| # | Co | Proč odloženo / dopad | Odhad |
|---|---|---|---|
| C2 | **Symbolový font - linková část** (gate R5): `ZNAK` sloupce LineTable, multichar/kompozitní symboly, převodní tabulka `GSId` (66+). Zastávkový JŘ hotový. | bez toho knižní/výlepový JŘ „tiše lže" u dopravních symbolů | M→S |
| C3 | **2D bin-packing / `Strih`** (gate R1/R2) - široká tabulka (MHD linka = 100+ spojů) se dnes zmáčkne na šířku místo rozřezání do stránkových bloků; + flow paketů, „dotisk zbytku legendy na další stránce". Staví na hotovém plánu řádků, takže se píše **jednou**. Zastávkový JŘ to nepotřebuje. | nutné pro reálný tisk MHD linek; největší kus | L |
| C4 | **Rozšíření výchozích šablon na další agendy** - mechanismus embed→MLINE hotový (governance v1, zastávková AD); přidat MHD + linkový JSON do `SystemTemplates/` | provozní; malý | S |
| C5 | **Konfigurovatelné pořadí poznámek** (`PoznamkyPoradiDruh`) - legacy AD parametr; TD má pevné pořadí druhů | per-zákaznická variace, řešit až s poptávkou | S |
| C6 | **Turnusy, obousměrný tisk, souhrnné JŘ (řádek zdrojové linky)** - legacy volby `in(arr,6/7)`, MHD `MakeData` je nemá | mimo scope pilotu; validovat, zda je zákazník chce, než portovat | - |
| C7 | **Rozdvojení příjezd/odjezd - jemný layout** + fixed-width padding sloupce (legacy `arr,13`) | kosmetika; TD řeší podřádky, ne padding | S |
| C8 | **Dedup Caché tiskových enginů** (*z auditu H1*) - `CasHodnota` bere `"|"` jinak v `KnizniJrAd` vs `ZastavkoveJrAd`, různé signatury `AddAnotace`/`BuildPoznamka`, ~15 helperů MHD↔AD + AD `^JRL` readery. Vyextrahovat sdílené `^JRL` readery + `Build*` do `BaseAgenda`/`JrShared`. **Caché analog R1 extrakce jádra** (C3 řeší .NET render-side, C8 Caché data-side). | korektnostní riziko; dělat s ostrým knižním JŘ, ne dopředu | L |
| C9 | **Testy Caché tiskových enginů** (*z auditu H2*) - `KnizniJr*` (~1975 ř.) 0 testů, `ZastavkoveJrMhd` 1 helper; AD gold gate tiše projde bez `CB_CSADAD`, křehký digest nad živými daty (pinuje 2/22 linek), `tools/compare-gold.ps1` neexistuje. Smoke + frozen-input gold testy; bezpodmínečný gold gate (zmrazit vstup do izolovaného ns). **= T1b.** | důvěra v datovou paritu; s ostrým knižním JŘ | M |

Detail datových odchylek a mezer viz [`linkovy-knizni-jr-implementace-analyza.md`](linkovy-knizni-jr-implementace-analyza.md) §11–12.

---

## 🟠 Fáze E: Vozový JŘ + Relace

Layout TBD - hlavička oběhu, tabulka spojů, poznámky (pravděpodobně víc elementů). Legacy:
`ALVA.MHD.ReportGenerator` (MHD) + `AD.ADREPORTGEN` (linky), viz
[`legacy-nahrada.md`](legacy-nahrada.md). **Relace (TiskJRRELACE)** - vývěsky
nádraží (SK zákazník, fork TiskJR ~50k LOC); **nejdřív ověřit živý use case (Q3)**.

| Krok | Co | Stav |
|---|---|---|
| 48 | Analýza referenčních vozových JŘ - layout + wire formát | ⬜ |
| 49 | `VehicleScheduleElement` (pravděpodobně víc elementů) | ⬜ |
| 50 | Caché `VozovyJrAPI` - GetData/GetMockData + async (velký objem dat) | ⬜ |

---

## 🟠 Fáze F: Náhrada REPORTGENERATOR (univerzální sestavy)

> ⚠️ **Gate před F2: R3 (prezentační nápovědy v datovém kontraktu + aditivní evoluce)** -
> [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md).

Největší kus náhrady - ne kvůli featurám (TD je napřed), ale kvůli **ekosystému ~28 agend** s RG
kontraktem (`GetSeznamSad`/`GetSadu`/`GetData`, `~`-tokenový stream). Nasazení: 33k textových elementů,
774 tabulek. **Ne big-bang** - etapizovat. Detail:
[`legacy-nahrada.md`](legacy-nahrada.md) §3.1 + §5 etapa 4.

| Krok | Co | Stav |
|---|---|---|
| F1 | **Prioritizace podle usage** - legacy `Main.cls:161-188` drží reálná čísla použití; seřadit agendy | ⬜ první (levný) |
| F2 | **Bridge vs čisté agendy (Q2)** - bridge `LegacyRgAgenda` (překlad `~`-protokolu na TD JSON, všech 28 najednou s omezenou věrností) vs čisté reimplementace; doporučení: **bridge nejdřív** | ⬜ rozhodnutí |
| F3 | **TableElement na RG úroveň** (navazuje na dynamickou tabulku + Fáze B): per-řádek zvýraznění 0-5 z dat, custom barvy per řádek/buňka, overflow módy (zalamování → klonování řádků), opakování mezisoučtu na další stránce, datové typy sloupců (datum/minuty/záporné červeně/skrýt nuly), řádek-jako-layout | ⬜ L |
| F4 | **Výstupní kanály fáze 2** - Excel export, PDF heslo + el. podpis (PDFSIGNER), Epson znakový tisk (Q1) | ⬜ dle poptávky |
| F5 | **Jazykové varianty šablon** - per-jazyk kopie provázaná na CS zdroj (Q5) | ⬜ |
| F6 | **Vědomě neportovat bez poptávky** - grafy (12×), AZTEC (2×), calculated (2× - výrazy TD nadmnožina), checkbox (8×); ověřit + škrtnout (Q4) | ⬜ validace |

---

## 🟡 Import PDF - doladění

| Krok | Co | Stav |
|---|---|---|
| 32 | **Obecný import: detekce tabulek z mřížky čar** - místo 50 čar + 20 textů jeden tabulkový element. ⚠️ **Gated na oživení fixní tabulky (Fáze B)** - cíl importu statické tabulky je Fixed mód, který je dnes uspaný; do té doby nedělat | ⬜ |
| 33 | JŘ import: **ladění na širší sadě JŘ** (jiné layouty než KV_DP vzor - jiná města, SK, netypické legendy) | 🟡 |

Křivky/Bézier v obecném importu - low-prio, bez poptávky.

---

## 🤖 AI integrace - v pozadí

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
  „vrátit smazaný prvek" → Ctrl+Z · „písmo celé tabulky" → dialog sloupců · „šablona pro kolegu" → Ctrl+S do DB.
- *Chytáky* (očekává „zatím neumíme", ne vymyšlený návod): sloučení buněk · export do Excelu · PDF e-mailem ·
  brožura/duplex · vlastní font · import staré šablony z EDISONu.
- *Nejistota* (odkázat na F1/podporu, ne halucinovat): „špatný čas u 3. zastávky linky 12" (datový problém, ne UI).

**Budoucí:** MCP server (`get_template`/`apply_template`/`add_element`/… tenká vrstva nad ViewModelem, každý
tool → `_vm.Execute(command)`) · onboarding průvodce · smart suggestions (TableElement → sloupce z DataSchema) ·
migrace starých Caché sestav · template linting v CI/CD.

---

## 🗄️ Hotová práce

Detail dokončených etap → [`roadmap-archive.md`](roadmap-archive.md). Průběžný changelog → [`changelog.md`](changelog.md).
