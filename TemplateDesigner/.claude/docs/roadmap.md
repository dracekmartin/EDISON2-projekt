# Roadmap

> **Stav k 2026-08-04.** **Jen otevřená / akční práce** - co je hotové, patří do
> [`roadmap-archive.md`](roadmap-archive.md) (průběžný changelog →
> [`changelog.md`](changelog.md)); **výčet dokončených bloků sem nepiš**, drží ho archiv.
> Follow-upy „na vyžádání zákazníka" žijí
> v [`customer-requirements §0.4`](customer-requirements-zastavkovy-jr.md).

> **▶ Další session:** **B18** z Vlny 1 (deklarativní pravidla EDInputFormu, M). Vlna 0 je dojetá,
> jediný její zbytek **B37** (poznámka „pokračuje do…") je od 4. 8. odblokovaný a celý u nás.
> Verze šla 4. 8. do produkce (#13093 body 1, 2, 4 jsou v ní).
> Linkový JŘ (**B10**, **B13**) jde až po zákaznické frontě.

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
3. **Jedna práce = jeden záznam.** Priorita se řídí vlnami; nezakládej k témuž bloku druhou evidenci
   (ukazatel `L‑1…L‑4` takhle vznikl a 4. 8. se zrušil - linkový JŘ byl v dokumentu 6×).

---

## 🌊 Vlna 0 - zákaznická fronta KOVED/IDZK (kolo 30. 7. 2026)

KOVED (IDZK, Lábsky) začal vývěsky kreslit v návrháři naostro a poslal zpětnou vazbu po blocích.
Většina je doladění toho, co už tiskneme (vzhled, tvar textu, chybějící pole); dva body jsou velké
koncepty a leží ve Vlně 3. Řazeno **od nejrychleji zavíratelného** - cíl je zmenšit počet tiketů,
ne odbavit nejhezčí featuru.

| # | Blok | Vel. | Uzavírací podmínka |
|---|---|---|---|
| **B37** | **#13093 bod 3 - poznámka „pokračuje do…" u odjezdu** (4. 8.: **není zablokované, je to celé u nás**). Klient chce seskupit odjezdy se stejným pokračováním do stejného cíle po stejné lince pod jedno písmeno a vysvětlivku („u1 - pokračuje do zast. Uherské Hradiště, žel. st. (l. 304)"), plus přepínač zobrazení. **Data máme:** uzel `32`, payload `m@33#linka:spoj:zastávka:…`; legacy to popisuje jako „spoj pokračuje po lince, spoji do zastávky" (`JRapiNZUtl2` ř.539), takže **ta zastávka je cíl**, ne přestupní bod. **Značka je „u"** (`JRapiLinPrtWin` ř.1561) - zákazníkovo „u1" opisuje starý modul. ⚠️ **Filtrovat typ vztahu:** je to rodina *vyčká / navazuje / pokračuje* (`JRapiLinPrtWin` ř.293; v JŘ druh poznámky `PoznamkyDruhVyckaNavazujePokracuje` s tiskem A4/A5), takže z 10 578 spojů s tím uzlem se smí označit jen „pokračuje" - jinak dostane značku i spoj, který jen čeká na návaznost. Přepínač patří do bloku poznámek (jako ostatní obsah). **Datová mapa (změřeno 4. 8. na 3 000 linkách KOVED):** `m@<n>` je **index záznamu, ne typ vztahu** -
to byl můj chybný předpoklad. Vztahy leží ve dvou uzlech s **různým layoutem**:
· uzel **31** (26 226 záznamů) = `m@i#<typ>#A:linka:spoj:2:zastávka:čas::dopravce:<typ>:cíl:::` - nese
**dvě** zastávky (přípoj + jeho cíl), hodně záznamů na spoj → odpovídá *navazuje / vyčká*;
· uzel **32** (1 930 v tomtéž rozsahu) = `m@i#linka:spoj:zastávka:2::A::čas:::dopravce` - **jedna**
zastávka, řídké → odpovídá legacy popisu *„spoj pokracuje po lince, spoji do zastavky"*
(`JRapiNZUtl2` ř.539), takže **ta zastávka je cíl**. U vlaku je layout `…:V:` a místo spoje číslo vlaku.
⚠️ **Zbývá potvrdit jediné:** že uzel 32 nese **výhradně** „pokračuje" (legacy má tři sekce - vyčká 322,
navazuje 482, pokračuje 539 - a kandidátem na třetí je uzel **34**, 6 105 záznamů). Ověřit na jejich
příkladu linky 304, ať značku nedostane spoj, který jen čeká na návaznost.

**Vztah k #13102** (virtuální prodloužení u Petra): jiná featura nad stejným jevem - tam je přínos „ubude varianta ve sloupcích minut" a zadává se ručně, tady je poznámka **automatická** z existujících dat. Nečeká jedno na druhé; u spojů, kde jednou prodloužení zadají, bude naše poznámka nadbytečná (detail zobrazení, ne blokátor). | M | klient si zapne a poznámka odpovídá jejich ruční praxi |

**Otázka na KOVED - odeslaná 3. 8. v odpovědi na #13048, nic neblokuje:** je u nich označník ještě
k něčemu? Kdyby ne, mohl by přepínač „Číslo stanoviště podle" zmizet a zůstat jen sloupek - ale je to
breaking pro zákazníka, který dělení stránek na označník používá (#8828), takže ne bez potvrzení obou.
**Nezeptáno (a nemusí se):** chtěli by **popis sloupku** („směr Brno přes UH")? Data ho nesou, sloupec
je jen schovaný z nabídky - viz rozhodnutí v changelogu 2026-08-03 (2).

**Mimo návrhář (vést jako práci JŘ, u nás jen navázat výstup):** **#13099** souhrnné JŘ (sdružení
linek se stejným koncovým trojčíslím pod vzorovou linku - dnes ručně skriptem v Excelu; pro KOVED
podmínka přesunu tvorby vývěsek do EDISONu; tisk hlavičky dopravců pro souhrnné JŘ je hotový `B33`) · **#13102** virtuální
prodloužení spoje jen pro vývěsku (zadávání v JŘ, přiřazeno Petrovi).
**#13098** je můj sběrný tiket - dnes v něm visí jen odsazení zastávky per směr = `B12` / #7937.

---

## 🌊 Vlna 1 - zákaznické velké + sdílené komponenty

| # | Blok | Vel. |
|---|---|---|
| **B12** | **#7937 rozšíření** - odsazení zastávky per-směr / per-Tč pásmo (`^JRL` dta7 je single-per-point → změna zdroje + editor; base AD hotový). Jen když TRANSDEV tlačí. | M |
| **B18** | **Deklarativní pravidla EDInputFormu** (kapabilita sdílené komponenty, celý EDISON) - dnes umí formulář reagovat na vyplněné hodnoty jen kolem přes server (`Validate`, ručně per formulář). Zavést deklarativní vrstvu: začít **viditelností** (`visibleWhen`), gramatiku sdílet s tiskovými parametry (`POLE=hodnota` + nerovnost) - nezavádět druhý dialekt. Povinnost/povolení až s poptávkou; **skutečná validace zůstává imperativní** (potřebuje data a dopočty). Rozhodnuto 29. 7.: samostatný blok, ne součást B4. **Follow-up:** migrace `CellStyleEditor` na Caché-driven formulář (S–M; layout sjednocen v B6, háček je tri-state / reaktivní logika - bez B18 nejde). | M |

---

## 🌊 Vlna 2 - velký kus linkového (dedikovaně)

> Bere se **vcelku**, ne po kusech (rozhodnutí Jan 4. 8.): `B10` sám o sobě nic nedoručí, protože
> bez `B13` se široká tabulka stejně nevytiskne. Podklady k `B10` jsou hotové, čeká na svou vlnu.

| # | Blok | Vel. |
|---|---|---|
| **B10** | **Symbolový font - linková část** (gate R5). Linková tabulka dnes symboly **nekreslí vůbec** - agenda posílá legacy klíče (`|`, `<`, `m`, `u`…) a chybí převod na pozice ve fontu. ✅ **Podklady hotové:** katalog 66 symbolů, render-módy i **změřený repertoár fontu** (94 pozic z obou fontů) jsou v `elementy-architektura.md`, takže neznámá „který kód nese který piktogram" je pryč. ⚠️ Pozice `znak` z katalogu se s pozicí ve fontu často **neshoduje** (letadlo `n`, paragraf `P`, metro `#`) - musí jít přes mapu, ne přes písmeno. **Rozsah (rozhodnutí Jan 4. 8.):** sdílená mapa `značka → (render-mód, hodnota)` pro všechny prvky, nahradí i dnešní `SymbolGlyph` u zastávkových vývěsek → dotkne se běžícího zastávkového JŘ, chce zlaté odtisky. Culture rozlišení jen pro **3 pozice** (`D`, `w`, `x`). Ve fontu chybí loď, wifi a zásuvka. | M→S |
| **B13** | **2D bin-packing / `Strih`** (gate R1/R2) - široká tabulka (MHD linka = 100+ spojů) se dnes zmáčkne na šířku místo rozřezání do stránkových bloků; + flow paketů, „dotisk zbytku legendy na další stránce". Nutné pro reálný tisk MHD linek; zastávkový JŘ to nepotřebuje. | L |
| **B14** | **Dedup Caché tiskových enginů + jejich testy** - doprovod ostrého knižního JŘ, ne dopředu. (a) **Dedup** (*audit H1*): `CasHodnota` bere `"|"` jinak v `KnizniJrAd` vs `ZastavkoveJrAd`, různé signatury `AddAnotace`/`BuildPoznamka`, ~15 helperů MHD↔AD + AD `^JRL` readery → vyextrahovat sdílené readery + `Build*` do `BaseAgenda`/`JrShared`; je to **Caché analog R1 extrakce jádra** (`B13` řeší .NET render-side, tohle Caché data-side). Korektnostní riziko. (b) **Testy** (*audit H2*, = `T1b`): `KnizniJr*` (~1975 ř.) 0 testů, `ZastavkoveJrMhd` 1 helper; AD gold gate tiše projde bez `CB_CSADAD`, křehký digest nad živými daty (pinuje 2/22 linek), `tools/compare-gold.ps1` neexistuje → smoke + frozen-input gold testy, bezpodmínečný gate (zmrazit vstup do izolovaného ns). | L + M |

`B13` stojí na hotovém `B11`: řádková vrstva existuje, takže rozřezání se píše **jednou** nad plánem
řádků, ne pětkrát v per-element smyčkách.

---

## 🌊 Vlna 3 - velké koncepty z kola KOVED

Body z fronty Vlny 0, které nejsou doladění - chtějí vlastní plánování.

| # | Blok | Vel. | Pozn. |
|---|---|---|---|
| **B39** | **#13045b vlastní skupiny dnů** - dnes pevné pracovní dny / soboty / neděle. Klient chce vlastní skupiny s obdobími („Vánoční provoz 27.-29. 12.", „Prázdniny" s několika rozsahy, „Dny školního vyučování"), spoje se do sloupce vyberou podle období. Mění datový model + chce editor. **Zbytek tiketu po `B29`** - ohlášená chybná značka je opravená 31. 7. (`B29`, archiv), takže část „netiskni značku, kterou už říká nadpis sloupce" je hotová a pro vlastní skupiny se jen musí dodržet i u nich. | L | koncept skupin + editor; pravidlo značek už existuje (`DenniZnacky`) |

---

### 📥 Backlog / budoucí velké tracky

- **Zastávkový JŘ business** (parkované helpdesk A2-A5/A6/B4 + TRANSDEV sada) → viz sekce „Zastávkový JŘ - business logika" níže
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
- **`B44` Nabídka datových polí místo psaní `{…}`** (M; podnět Jan 3. 8., návrh + rozhodnutí 4. 8.).
  Dnes musí uživatel název pole znát a napsat bez překlepu; chyba se projeví **až v tisku**.
  **Změřeno na šablonách v repu:** všech 18 výskytů `{POLE}` je v **téže vlastnosti** (text textového
  prvku), z toho 12× je celá hodnota jeden token a 6× věta s tokeny uvnitř (`Linka {CISLO_LINKY}:
  {POPIS_LINKY}`). ⚠️ **Proto NELZE nahradit pole ComboBoxem** - je to totéž pole, jednou token,
  podruhé věta se dvěma tokeny; výběr celé hodnoty by druhý případ znemožnil.
  **Rozhodnutý návrh (Jan 4. 8.):** pole zůstane textové a vedle něj bude **„vložit pole"** -
  hledatelný seznam (`EDFilterComboBox`, žádná nová vyhledávací logika), výběr **vloží token na
  pozici kurzoru**. Je to standardní vzor z hromadné korespondence a report designerů, je **vidět**
  (na rozdíl od doplňování skrytého za napsáním `{`) a funguje i pro větu s víc tokeny.
  **Kdo co dodává:** server v `.cls` označí pole, kam placeholdery patří (aditivní sloupec na wire -
  čtečka má délkové pojistky, takže EDISON to nerozbije); **seznam polí dodá klient** ze schématu
  agendy, protože Caché agendu nezná - týž mechanismus jako „Zdroj dat" a řazení sloupců. Tokeny se
  vezou v `Field.ListItems` (u textového pole dnes nevyužité), takže nový je jen ten příznak.
  **Rozsah (rozhodnutí Jan):** připojitelné chování v EDControls použité **oběma cestami** -
  formulářem z Caché i vlastními editory návrháře; jinak vzniknou dvě implementace téhož.
  V prvním kroku nabízet **všechna pole agendy** s popiskem, odkud jsou (uživatel hledá název,
  nepřemýšlí v kategoriích); kontextový filtr až kdyby vadil.
  Doplňování při psaní `{` se dá přidat později nad týmž seznamem - není to buď/anebo.

- **`B51` EDList plní řádky po jednom - škubnutí při psaní** (S; nález Jan 4. 8., **první pokus
  vrácen**). `ApplyRows` přidává řádky jeden po druhém do `ObservableCollection` navázané na
  `DataGrid` s filtrem → 200 řádků = 200 přepočtů pohledu. Projeví se, když načtení dosedne, zatímco
  uživatel píše do filtru skupiny. ⚠️ **`ICollectionView.DeferRefresh` NENÍ řešení** - zkoušeno
  4. 8. a shodilo výběr linky (`CollectionChanged` chodí dál a view při něm sahá na currency, což je
  během odkladu zakázané; detail v changelogu 2026-08-04 (15)). Schůdné cesty: odpojit `ItemsSource`
  na dobu mutace a zase připojit, nebo kolekce s hromadnou výměnou (jeden `Reset` místo N `Add`).
  **Podmínka: proklikat**, testy tuhle třídu chyb nechytí.
- AI (v pozadí) · font warning + ad-hoc overflow (YAGNI)

**Otevřené otázky k Fázi E/F** (Epson, RG bridge, Relace, grafy/AZTEC, jazykové varianty) → evidované
v [`legacy-nahrada.md §7`](legacy-nahrada.md); vyřeší se
při scope té fáze, ne teď.

---

## 🚀 Produkční / provozní - otevřené

1. **Headless tisk (A3)** - „vytiskni linku X" z modulu přes PrintEngine fasádu (šablona+data→PDF bez stavu editoru). Dnes tisk jen v designeru. Gate R4/R7.
2. **Samostatný distribuční kanál** systémových šablon mimo .exe - v1 doručuje embed→MLINE; „jiný kanál" (server push / admin) jen vymění zdroj, picker/UX beze změny.
3. **Další výchozí šablony** (MHD + linkový) = **`C4`** - jediná evidence tohoto bodu; linková
   varianta je prerekvizita Fáze C+D. Mechanismus embed→MLINE hotový (governance v1, zastávková AD),
   zbývá kurátorovat + přesunout JSON do `SystemTemplates/`. Vel. **S**.
4. **Zákaznická dokumentace na wiki (post-release)** - seedy hotové (`uzivatelska-prirucka.md`,
   `plan-a-zmeny.md`); zbývá **obrázky + publikace**.

---

## 🅿️ Zastávkový JŘ - zákaznická fronta

> **Plný stav** (hotové featury + co otestovat pro uzavření helpdesk tiketů) →
> [`customer-requirements-zastavkovy-jr.md §0`](customer-requirements-zastavkovy-jr.md). Zde jen **otevřená
> akční práce**. Vel.: S = do hodiny, M = půl dne, L = víc dní.

### Čeká na někoho jiného - postrčit, není to práce

- **TR2** implementováno (opt-in `SOUHRN_NA_ZNAMENI`), ⚠️ čeká SK ověření kódování na-znamení - kolega
- **TR8 („<1" místo „0") park** (analýza 2026-07-10): naše časy jsou kumulativní, 0-segment se tiskne
  jako opakované číslo, ne „0". Per-segment sloupec TD nemá. Blokuje potvrzení od TRANSDEV
  (který sloupec - legacy „Průběhy"?). Detail → `customer-requirements §TR/8`.

### Bez poptávky nebrat - chce analýzu / datový model / business rozhodnutí

| # | Co | Vel. | Co potřebuje |
|---|---|---|---|
| B48 | Přepínač „Přestupy (vlak/autobus)" u MHD nefunguje - MHD posílá přestupy jako pozici ve fontu (`w`/`x`), ne jako písmeno, takže je `IsTransferAttr` nepozná a řídí je obecný přepínač „Atributy". Tisknou se správně, jen se ovládají jinak než u AD. Fix = tagovat ATRIBUTY kategorií `prestup` na wire. Měření gate 4. 8. → archiv | M | poptávku od firmy s modulem MHD (dnes DPCB/DPKV) |
| TR10 | Skrýt spoje o So/Ne/svátcích | S–M | Caché filtr |
| TR7 | Per-písmeno výběr sloupců variant jízdních dob | S–M | jen pokud TRANSDEV potvrdí nad novým chováním (varianty dle trasy+nástupiště s max časy jsou od 27. 7. hotové) |
| TR6 | Barva linky (bg/text ve vlastnostech linky → volba v šabloně) | M | nový datový model |
| TR9 | Obrácená logika poznámkování (rozsah TČ v 1. tabulce) | M | Caché-input (Studio JŘ) |
| TR5 | Definice cíle - střední (per zast.×směr) + ideál (DB cílů) | M–L | datový model; *(min „cíl per směr" = hotovo přes `POPIS_SMER`=konečná)* |

*(Follow-upy uzavřených tiketů „na vyžádání zákazníka" → [`customer-requirements §0.4`](customer-requirements-zastavkovy-jr.md).)*

---

## 🟠 Fáze B: Element flexibility - jen odložené body

Z gap analýzy vůči komerčním generátorům. Nezávislé na JŘ (faktury i obecné šablony).

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

**Zbývající prerekvizity jsou zaevidované jako bloky:** `B10` (symbolový font - linková část),
`B13` (layout rozšíření: flow paketů, „dotisk poznámek z minulé stránky"), `C4` (per-linka šablony,
vedený v provozní čtyřce). Datové jádro (`KnizniJrAd`/`KnizniJrMhd`), `LineTableElement`, plán řádků
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
