# Roadmap

> **Jen otevřená / akční práce** - co je hotové, patří do
> [`roadmap-archive.md`](roadmap-archive.md) (průběžný changelog →
> [`changelog.md`](changelog.md)); **výčet dokončených bloků sem nepiš**, drží ho archiv.
> Follow-upy „na vyžádání zákazníka" žijí
> v [`customer-requirements §0.4`](customer-requirements-zastavkovy-jr.md).

> **Stav k 2026-08-12.** Verze vydaná 11. 8.; `B39` od 12. 8. **rozsvícená pro všechny**
> (přepínač odstraněn - viz changelog 2026-08-12).
>
> **▶ Zastávkový JŘ má jedinou otevřenou věc a ta je blokovaná** - `B67` (výběr variant jízdních
> dob) čeká na odpovědi od TRANSDEV. `B39` (vlastní sloupce vývěsky, #13045b) je hotová oběma
> etapami 10. 8., takže **fronta KOVED je dojetá**. **Další krok si vyber:** **Fáze B** (tabulkové
> elementy, po nálezu 6. 8. menší, než vypadala, dá se vzít vcelku) nebo linkový JŘ
> (**B10** + **B13**) jako dedikovaný slot.
>
> **Sada TRANSDEV: 7 z 10 hotových** (11. 8.). Cíl a obrácené poznámkování berou **kolegové**
> (proto tady nejsou), odsazení per směr taky. Otevřený u nás zůstává jediný - **výběr variant
> jízdních dob** (`TR7`), který zákazník 10. 8. potvrdil znovu jako #12843 bod 4; hotová je jen
> půlka (nestabilní číslování). Odložené velké věci **`B18`** a **`B56`** leží v Backlogu,
> **`B60`** u linkového JŘ.
>
> **Zákaznická fronta KOVED je dojetá** - `B37` hotová 5. 8., takže **#13093 je celý uzavřený**.
> **Verze z 11. 8. je vydaná** a nese všechno od 5. 8. včetně (pokračující spoje, značky dle
> nastavení linky, „<1", podněty #12843). Vlastní sloupce vývěsky v ní ještě zhasnuté byly;
> od 12. 8. je přepínač odstraněný, takže **příští vydání je ponese rozsvícené** - i proto je novinka
> zpátky v seedu (rada „nedělní a sváteční provoz rozdělit na dva sloupce, ne značkou" je v ní taky)
> a nahraje se s vydáním.
>
> **Co sem nepatří** (rozhodnutí Jan 6. 8.): tikety řešené mimo návrhář a čekání na cizí lidi.
> Roadmapa je seznam **naší otevřené práce**, ne zrcadlo helpdesku.

---

## 🧭 Jak podle roadmapy pracovat

Práce je rozřezaná na **bloky velikosti „jedno sezení"** (`B10`, `B13`, …). Sekce jsou seřazené
podle priority (**vršek dokumentu = priorita**), uvnitř sekce platí pořadí řádků.
Velikosti: **S** = do hodiny · **M** = půl dne · **L** = víc dní.

**Nová session:** vezmi první nezablokovaný blok odshora. Po dokončení:
changelog + přesun bloku **kompletně** do archivu (ne odškrtnutý řádek zde).

**Pravidla, aby roadmapa znovu nenarostla:**
0. **Číslo bloku je globální a nikdy se nerecykluje** - před založením grepni `B<č>` v roadmapě
   **i v archivu** (jinak vznikne druhý „B18“ a review se rozejde s tím, co kdo myslel).
1. **Nic nevstoupí bez velikosti a čísla bloku** - volný text bez velikosti je přesně to, co se nahromadilo.
2. **Zákaznická položka má uzavírací podmínku** („co ověřit, aby se dala zavřít") - jinak zůstane „hotová, ale otevřená".
3. **Jedna práce = jeden záznam.** Priorita se řídí pořadím v dokumentu; nezakládej k témuž bloku
   druhou evidenci (ukazatel `L‑1…L‑4` takhle vznikl a 4. 8. se zrušil - linkový JŘ byl v dokumentu 6×).

---

## 🚌 Zastávkový JŘ - známé věci

KOVED (IDZK, Lábsky) kreslí vývěsky v návrháři naostro; fronta z kola 30. 7. je **dojetá**.
**Plný stav** (hotové featury + co otestovat pro uzavření helpdesk tiketů) →
[`customer-requirements-zastavkovy-jr.md §0`](customer-requirements-zastavkovy-jr.md).

`B37` (pokračující spoje) dokončena 5. 8., čímž je **#13093 celý hotový** (body 1, 2 a 4 šly do verze
4. 8.). `B39` (vlastní sloupce vývěsky) hotová 10. 8. → **z kola KOVED nezbývá nic**.

| # | Blok | Vel. | Uzavírací podmínka |
|---|---|---|---|
| **B67** | **Výběr variant jízdních dob** (TRANSDEV `TR7` = #12843 bod 4, zákazník potvrdil 10. 8.) - dnes je jediná volba „jedna varianta, nebo všechny". Hotová je půlka: nestabilní číslování je pryč (varianty se sdružují dle sledu zastávek a mají písmena, ne poziční čísla). Zbývá **vybrat, KTERÉ varianty se tisknou**. ⛔ **Blokované na odpovědích** (Jan se ptal 12. 8.): ruční výběr per linka, nebo pravidlo (nejčastější / obsluhující daný úsek)? Jak často se ta volba mění? ⚠️ **Podmínka návrhu:** varianty se počítají per zastávku a výchozí řazení je „dle četnosti ze zastávky", takže **písmeno A znamená na každé stránce jinou variantu** - jakýkoli výběr „tiskni jen A" vyžaduje zapnuté „Stabilní (pořadí spojů)", nebo změnu výchozí hodnoty. | S–M | TRANSDEV potvrdí nad výsledkem |

Tiketů, které se řeší **mimo návrhář** (legacy JŘ modul, cizí přiřazení), se roadmapa nedrží -
patří do helpdesku, ne sem (rozhodnutí Jan 6. 8.).

### Bez poptávky nebrat - chce analýzu / datový model / business rozhodnutí

| # | Co | Vel. | Co potřebuje |
|---|---|---|---|
| B68 | **Do šablony se zapisují i parametry, kterých se uživatel nedotkl** (`SyncParametersToTemplate` iteruje všechny obsahové parametry). Uložení tím zabetonuje **výchozí** hodnotu a ta pak přebíjí per-agenda nastavení - uživatel o svou volbu přijde, aniž by něco přepnul. Podezření u #12843 (TRANSDEV: „projeté zastávky se přestaly zobrazovat"), ale **reprodukovat se to nepodařilo** - u Jana se hodnota ukládá i načítá správně, takže to nejspíš byl jednorázový dopad přesunu parametrů do šablony 15. 7. Oprava = zapisovat jen hodnoty odlišné od výchozí (nebo sledovat „dotčeno" per parametr). | S | **druhou stejnou stížnost** - pak je to reprodukce a opravit; jinak nechat, protože samonosná šablona je záměr |
| B48 | Přepínač „Přestupy (vlak/autobus)" u MHD nefunguje - MHD posílá přestupy jako pozici ve fontu (`w`/`x`), ne jako písmeno, takže je `IsTransferAttr` nepozná a řídí je obecný přepínač „Atributy". Tisknou se správně, jen se ovládají jinak než u AD. Fix = tagovat ATRIBUTY kategorií `prestup` na wire. Měření gate 4. 8. → archiv | M | poptávku od firmy s modulem MHD (dnes DPCB/DPKV) |

*(Follow-upy uzavřených tiketů „na vyžádání zákazníka" → [`customer-requirements §0.4`](customer-requirements-zastavkovy-jr.md).)*

---

## 🚆 Linkový / knižní JŘ - velký kus (dedikovaně)

> Bere se **vcelku**, ne po kusech (rozhodnutí Jan 4. 8.): `B10` sám o sobě nic nedoručí, protože
> bez `B13` se široká tabulka stejně nevytiskne. Podklady k `B10` jsou hotové, čeká na svůj slot.

| # | Blok | Vel. |
|---|---|---|
| **B10** | **Symbolový font - linková část** (gate R5). Linková tabulka dnes symboly **nekreslí vůbec** - agenda posílá legacy klíče (`|`, `<`, `m`, `u`…) a chybí převod na pozice ve fontu. ✅ **Podklady hotové:** katalog 66 symbolů, render-módy i **změřený repertoár fontu** (94 pozic z obou fontů) jsou v `elementy-architektura.md`, takže neznámá „který kód nese který piktogram" je pryč. ⚠️ Pozice `znak` z katalogu se s pozicí ve fontu často **neshoduje** (letadlo `n`, paragraf `P`, metro `#`) - musí jít přes mapu, ne přes písmeno. **Rozsah (rozhodnutí Jan 4. 8.):** sdílená mapa `značka → (render-mód, hodnota)` pro všechny prvky, nahradí i dnešní `SymbolGlyph` u zastávkových vývěsek → dotkne se běžícího zastávkového JŘ, chce zlaté odtisky. Culture rozlišení jen pro **3 pozice** (`D`, `w`, `x`). Ve fontu chybí loď, wifi a zásuvka. | M→S |
| **B13** | **2D bin-packing / `Strih`** (gate R1/R2) - široká tabulka (MHD linka = 100+ spojů) se dnes zmáčkne na šířku místo rozřezání do stránkových bloků; + flow paketů, „dotisk zbytku legendy na další stránce". Nutné pro reálný tisk MHD linek; zastávkový JŘ to nepotřebuje. | L |
| **B14** | **Dedup Caché tiskových enginů + jejich testy** - doprovod ostrého knižního JŘ, ne dopředu. (a) **Dedup** (*audit H1*): různé signatury `AddAnotace`/`BuildPoznamka`, ~15 helperů MHD↔AD + AD `^JRL` readery → vyextrahovat sdílené readery + `Build*` do `BaseAgenda`/`JrShared`; je to **Caché analog R1 extrakce jádra** (`B13` řeší .NET render-side, tohle Caché data-side). Korektnostní riziko. Menší výsek Build\* helperů = `B56` (nález review, **odložen** - viz Backlog); při realizaci jednoho zredukovat druhý. ⚠️ **Korekce z analýzy 5. 8.:** dřívější tvrzení „`CasHodnota` bere `"|"` jinak" **neplatí** - obě verze jsou funkčně identické, lišil se jen komentář. A objem duplikace je menší, než audit odhadoval: z 27 stejně pojmenovaných helperů je jen 15 doslovných kopií (~97 řádků), zbytek sdílí jen název a dělá jinou věc. Podklad v Backlogu u `B56`. (b) **Testy** (*audit H2*, = `T1b`): `LinkovyJr*` (~2 000 ř.) 0 testů, `ZastavkoveJrMhd` 1 helper; AD gold gate tiše projde bez `CB_CSADAD`, křehký digest nad živými daty (pinuje 2/22 linek), `tools/compare-gold.ps1` neexistuje → smoke + frozen-input gold testy, bezpodmínečný gate (zmrazit vstup do izolovaného ns). | L + M |
| **B60** | **Název zastávky se v linkovém JŘ tvoří jinak než na vývěsce** (nález analýzy `B56`, 5. 8.) - vývěska respektuje volbu tvaru názvu (#13089, 4 volby) i přepínač „použít upravený název pro MHD"; linkový JŘ bere název natvrdo, takže **táž zastávka může mít na dvou výstupech jiné jméno**. ⚠️ **Změřeno 5. 8. - není to malá věc:** tvarování visí na **dvou parametrech sestavy** (`TVAR_NAZVU_ZASTAVKY`, `NAZEV_PRO_MHD`) načítaných do `^||TiskAd`, takže srovnání = přidat linkovému JŘ dva uživatelsky viditelné parametry (popisky enumu jsou po vydání zamčené) + sdílet 5 helperů (`FormatNazvuZastavky`, `MezeryPoInterpunkci`, `TvarNazvuZastavky`, `PouzitNazevProMhd`, `NactiNastaveniNazvu`) - a to **bez jakéhokoli testu** (linkové agendy 0 testů, síť staví `B14b`). **Nejdřív rozhodnout, jestli ty parametry v linkovém JŘ chceme.** | M |

`B13` stojí na hotovém `B11`: řádková vrstva existuje, takže rozřezání se píše **jednou** nad plánem
řádků, ne pětkrát v per-element smyčkách.

---

### 📥 Backlog / budoucí velké tracky

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
  z palety, nebo z nabídky „Přidat".

- **`B18` Deklarativní pravidla EDInputFormu** (M, odloženo - rozhodnutí Jan 5. 8.: patří k velkým
  věcem do budoucna, ne do běžné fronty; je to **kapabilita sdílené komponenty pro celý EDISON**,
  ne featura návrháře). **Deklarativní pravidla EDInputFormu** (kapabilita sdílené komponenty, celý EDISON) - dnes umí formulář reagovat na vyplněné hodnoty jen kolem přes server (`Validate`, ručně per formulář). Zavést deklarativní vrstvu: začít **viditelností** (`visibleWhen`), gramatiku sdílet s tiskovými parametry (`POLE=hodnota` + nerovnost) - nezavádět druhý dialekt. Povinnost/povolení až s poptávkou; **skutečná validace zůstává imperativní** (potřebuje data a dopočty). Rozhodnuto 29. 7.: samostatný blok, ne součást B4. **Follow-up:** migrace `CellStyleEditor` na Caché-driven formulář (S–M; layout sjednocen v B6, háček je tri-state / reaktivní logika - bez B18 nejde). **Follow-up 2 (review 4. 8.):** rozšířit gramatiku o čtení **parametru sestavy**, aby šlo zašednout pole prvku mrtvá bez parametru (6 polí šipky vs. `TISK_SIPKY`, projeté zastávky vs. `TISK_PROJETYCH_ZAST`) - mikropravidlo §5.3 dnes plní jen helpTexty. **Follow-up 3 (z `B57`, 5. 8.):** parametr sestavy nemá pole pro **popis** (`TemplateParameter` má jen název/popisek/typ/default/volby/skupinu/viditelnost), takže krkolomné labely (`SCHOVAT_V_ROZSAHU`, `ROZTAHNOUT_NA_VYSKU`) nejde vysvětlit. Aditivní klíč na wire + vykreslení jako tooltip - dělat spolu s gramatikou, ne zvlášť kvůli dvěma tooltipům.

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

- **Zastávkový JŘ business** (parkované helpdesk A2-A5/A6/B4 + TRANSDEV sada) → viz sekce „Zastávkový JŘ - známé věci" výše
- **Fáze B - odložené:** **oživení fixní tabulky** (paleta + Fixed editor) + **merged cells** (gated na ni).
- **Tabulkové featury (bez customer demand):** grouping (`GroupHeader`/`GroupFooter`/`Summary`) ·
  vnořené datasource (`_rows` v `_rows`).

- **Fáze C+D** linkové/knižní JŘ · **Fáze E** vozový + Relace · **Fáze F** REPORTGENERATOR - velké
  tracky, každý gated rozhodnutími R1–R7 (viz [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md))
- **Externí datové zdroje** (analýza 2026-07-12, na poptávku) - architektura připravená (engine zná jen
  `Template + DataContext`; DataContext je zdrojově agnostický JSON). Tři švy: Caché gateway agenda
  (0 změn .NET) · .NET provider vedle Caché (vzor `JsonFileDataSchemaProvider` + Composite) · headless
  A3 („POST šablona+JSON → PDF" = externí zdroj bez adaptéru; **A3 je nejpravděpodobnější nosič**).
  První krok s nejlepším poměrem cena/hodnota: **generický JSON zdroj (soubor/URL)** - tržní minimum
  (CSV/Excel/JSON/REST), náš wire formát už JSON je; využilo by i AI + testování šablon. Dořešit:
  credentials, schéma z dat, změkčení předpokladu „běží nad Caché".
- **`B42` Co všechno umí import z předlohy nastavit** (M, nejdřív analýza; podnět Jan 31. 7. při `B41`).
  Import dnes umí **pozice a prvky** plus část vzhledu (fonty, výšky řádků, barvy záhlaví, sloupce).
  Otevřená otázka: umí z předlohy rozumně odvodit i **parametry sestavy** a **další nastavení prvků**
  (pozice značek, šipka, sloupce jako zóna/označník, varianty tras, skupiny dnů)? Je to velká
  a netriviální část importu, takže se to má projít vědomě, ne po jedné vlastnosti.
  ⚠️ **Klíčové omezení, na které to narazí:** hodnoty parametrů sestavy jsou uložené **per uživatel ×
  agenda**, ne per šablona - kdyby je import nastavil, přepíše nastavení pro všechny ostatní šablony
  té agendy. Buď se tedy musí zavést per-šablonové hodnoty parametrů (velké rozhodnutí, dotýká se
  `ApplyPersistedPrintParamsAsync`), nebo import smí jen **navrhnout** (viz krok níže).
  **Malý krok, který jde udělat hned (S):** detekci časové osy z předlohy vrátit a její výsledek
  **napsat do protokolu importu** („předloha má osu 5-23, nastavte ji v parametrech"), ať se informace
  neztratí a dostane se k tomu, kdo s ní může něco udělat. Původní detekce (`timeAxisRangeFrom/To`
  v `AiGuidedScheduleBuilder`) je v gitu před commitem `B41`.
- AI (v pozadí) · font warning + ad-hoc overflow (YAGNI)

**Otevřené otázky k Fázi E/F** (Epson, RG bridge, Relace, grafy/AZTEC, jazykové varianty) → evidované
v [`legacy-nahrada.md §7`](legacy-nahrada.md); vyřeší se
při scope té fáze, ne teď.

---

## 🚀 Produkční / provozní - otevřené

1. **Headless tisk (A3)** - „vytiskni linku X" z modulu přes PrintEngine fasádu (šablona+data→PDF bez stavu editoru). Dnes tisk jen v designeru. Gate R4/R7.
2. **Samostatný distribuční kanál** systémových šablon mimo .exe - v1 doručuje embed→MLINE; „jiný kanál" (server push / admin) jen vymění zdroj, picker/UX beze změny.
3. **Další výchozí šablony** = **`C4`** - jediná evidence tohoto bodu. Zastávková **AD i MHD**
   hotové (MHD 11. 8. - port AD varianty, KV_DP chtěl začít zkoušet). Zbývá **linková**, která je
   prerekvizita Fáze C+D. Mechanismus embed→MLINE hotový; nový soubor v `SystemTemplates/`
   stačí přidat, zabalí i nalije se sám. Vel. **S**.
4. **Zákaznická dokumentace na wiki (post-release)** - seedy hotové (`uzivatelska-prirucka.md`,
   `plan-a-zmeny.md`); zbývá **obrázky + publikace**.

---

## 🟠 Fáze B: Element flexibility - jen odložené body

Z gap analýzy vůči komerčním generátorům. Nezávislé na JŘ (faktury i obecné šablony).

**Pevná (ručně vyplňovaná) tabulka - kroky 1 a 2 hotové 12. 8.** (prvek v paletě, obsah se
vyplňuje na plátně, dialog má jedinou záložku) → archiv. Cíl **není faktura, ale náhrada
„poskládám to z textových polí"**. Model = **objekt na stránce s excelovskými klávesami**
(Word/PowerPoint), ne tabulkový procesor - žádný řádek vzorců ani odkazy typu A1.

**Co zbývá - rozhodnout, co ještě do první verze** (nic z toho neblokuje používání):

| # | Co | Vel. |
|---|---|---|
| B73 | **Šířka sloupce a výška řádku tažením** za dělicí čáru. Dnes se šířky zadávají jen číslem v mm a výška řádku je jedna společná pro celou tabulku - výška **per řádek** je aditivní změna modelu | S–M |
| B76 | **Výběr více buněk tažením** a na něm hromadné formátování + **ColSpan** slučování buněk (aditivní JSON, span-aware DrawRow). **RowSpan vědomě odložen** - spanování výšek × střídání řádků × podmíněná viditelnost. ⚠️ Zjemní i vkládání ze schránky: dnes se vkládá od vybrané buňky a tabulka se dorovná (Word/Docs chování, jediné neztrátové bez výběru oblasti); s výběrem má platit **vybraná oblast = cíl vložení** (Excel), jedna buňka = dnešní chování | M |
| B77 | **Připravené bloky agend při vkládání tabulky** (`GetTableTemplates`: „Záhlaví linky", „Shrnutí faktury"). Na serveru existují pro každou agendu, ale od zrušení starého dialogu k nim **nevede z UI cesta**. Patří k vkládání prvku („prázdná / z připraveného bloku"), ne do dialogu | S |

---

## 🟠 Fáze C + D: Výlepový/knižní JŘ linky (náhrada TiskJR) - pravděpodobně JEDEN track

> ⚠️ **Gate: R1 (sdílené tabulkové jádro), R2 (layout zodpovědnosti), R5 (embedded fonty)** -
> [`architektonicka-rozhodnuti-agendy.md`](architektonicka-rozhodnuti-agendy.md). Krok 43 začíná
> designem sdíleného jádra (StopList + DynamicTable + LineTable), ne kódem elementu.

Legacy `TiskJR` generuje vývěsní i knižní JŘ z téhož enginu → C + D je nejspíš jeden track. Formát:
sloupcová tabulka celé linky (svislý seznam zastávek × sloupce spojů s časy; řádek platností se
symboly/NZ; Vyčká/Navazuje; skupinová záhlaví PD/So/Ne; 10 druhů poznámek). **Gold oracle `mkPLinWin`
(ALVA.JRMAIN.MLAPI)**, metodika = čistá reimplementace à la `ZastavkoveJrAd` (~1 měsíc).

**Zbývající prerekvizity jsou zaevidované jako bloky:** `B10` (symbolový font - linková část),
`B13` (layout rozšíření: flow paketů, „dotisk poznámek z minulé stránky"), `C4` (per-linka šablony,
vedený v provozní čtyřce). Datové jádro (`LinkovyJrAd`/`LinkovyJrMhd`), `LineTableElement`, plán řádků
a auto-fit fontu jsou hotové.

### Odložené body knižního/linkového JŘ (z pilotu, seřazené dle hodnoty pro ostré nasazení)

| # | Co | Proč odloženo / dopad | Odhad |
|---|---|---|---|
| C5 | **Konfigurovatelné pořadí poznámek** (`PoznamkyPoradiDruh`) - legacy AD parametr; TD má pevné pořadí druhů | per-zákaznická variace, řešit až s poptávkou | S |
| C6 | **Turnusy, obousměrný tisk, souhrnné JŘ (řádek zdrojové linky)** - legacy volby `in(arr,6/7)`, MHD `MakeData` je nemá | mimo scope pilotu; validovat, zda je zákazník chce, než portovat | - |
| C7 | **Rozdvojení příjezd/odjezd - jemný layout** + fixed-width padding sloupce (legacy `arr,13`) | kosmetika; TD řeší podřádky, ne padding | S |

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
