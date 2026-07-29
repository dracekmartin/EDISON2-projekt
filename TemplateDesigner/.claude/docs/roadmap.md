# Roadmap

> **Stav k 2026-07-28** (stanovišťový balík KOVED/TRANSDEV uzavřen, verze připravena k publish).
> **Jen otevřená / akční práce.** Cokoli hotového → [`roadmap-archive.md`](roadmap-archive.md),
> průběžný changelog → [`changelog.md`](changelog.md). Hotové věci sem nevracet - patří do archivu.

> **▶ Další session:** po publishi vzít **B2** (zavírací kolo helpdesku - poslední 2 tikety + vizuál
> zmenšování), pak **B10** (symbolový font linkové části) nebo **B13** (rozřezání široké tabulky,
> odemčené hotovým B11). Follow-upy „na vyžádání zákazníka" žijí
> v [`customer-requirements §0.4`](customer-requirements-zastavkovy-jr.md).

---

## 🧭 Jak podle roadmapy pracovat

Práce je rozřezaná na **bloky velikosti „jedno sezení"** (`B2`, `B10`, …) seřazené do **vln**. Každý blok
má **zónu** = oblast, kde si dvě sezení šlapou po prstech (viz „Paralelizace" níže).
Velikosti: **S** = do hodiny · **M** = půl dne · **L** = víc dní.

**Nová session:** vezmi nejnižší nezablokovaný blok z nejnižší nedokončené vlny; když běží jiná session,
zkontroluj zónu. Po dokončení: changelog + přesun bloku **kompletně** do archivu (ne odškrtnutý řádek zde).

**Tři pravidla, aby roadmapa znovu nenarostla:**
1. **Nic nevstoupí bez velikosti a čísla bloku** - volný text bez velikosti je přesně to, co se nahromadilo.
2. **Zákaznická položka má uzavírací podmínku** („co ověřit, aby se dala zavřít") - jinak zůstane „hotová, ale otevřená".
3. **Linkový JŘ má ukazatel `L‑1…L‑4`** (Fáze C+D) - velký kus rozřezaný na slotové kroky, aby v každé vlně postoupil.

---

## 🌊 Vlna 1 - vyčistit frontu

| # | Blok | Vel. | Zóna | Závisí na |
|---|---|---|---|---|
| **B2** | **Zavírací kolo helpdesku** - ověřit v buildu a zavřít **#7947, #7955** + finální vizuál #13048 bodu 2 (u něj zvážit spodní mez zmenšení textu / vazbu na overflow varování - extrémní název se dnes zmenší bez limitu); postrčit čekající (viz „Zákaznická fronta" níže) | M | 5 | aktuální publish |

Po vlně 1 zůstává v zákaznické frontě jediná reálná práce (`B12`) + položky čekající na zákazníky.

---

## 🌊 Vlna 2 - zákaznické velké + odemknout linkový

| # | Blok | Vel. | Zóna | Závisí na |
|---|---|---|---|---|
| **B10** | **L‑2: symbolový font - linková část** (`ZNAK` sloupce LineTable: multichar/kompozity, převodní tabulka `GSId` 66+ symbolů, TTF v `C:\DEV\EDISON\TiskJR\_ruzne`). Zastávkový JŘ hotový 9. 7. | M→S | 3 (izolovaně) | - |
| **B12** | **#7937 rozšíření** - odsazení zastávky per-směr / per-Tč pásmo (`^JRL` dta7 je single-per-point → změna zdroje + editor; base AD hotový). Jen když TRANSDEV tlačí. | M | 1 | - |
| **B18** | **Deklarativní pravidla EDInputFormu** (kapabilita sdílené komponenty, celý EDISON) - dnes umí formulář reagovat na vyplněné hodnoty jen kolem přes server (`Validate`, ručně per formulář). Zavést deklarativní vrstvu: začít **viditelností** (`visibleWhen`), gramatiku sdílet s tiskovými parametry (`POLE=hodnota` + nerovnost) - nezavádět druhý dialekt. Povinnost/povolení až s poptávkou; **skutečná validace zůstává imperativní** (potřebuje data a dopočty). Rozhodnuto 29. 7.: samostatný blok, ne součást B4. **Follow-up:** migrace `CellStyleEditor` na Caché-driven formulář (S–M; layout sjednocen v B6, háček je tri-state / reaktivní logika - bez B18 nejde). | M | 2 | - |

---

## 🌊 Vlna 3 - velký kus linkového (dedikovaně)

| # | Blok | Vel. | Zóna | Závisí na |
|---|---|---|---|---|
| **B13** | **L‑4: 2D bin-packing / `Strih`** - široká tabulka (MHD linka = 100+ spojů) se dnes zmáčkne na šířku místo rozřezání do stránkových bloků; + flow paketů, „dotisk zbytku legendy na další stránce" | L | 3 výhradně | - (B11 hotov) |
| **B14** | **Dedup Caché tiskových enginů (C8) + testy tiskových enginů (C9)** - doprovod ostrého knižního JŘ, detail v tabulce Fáze C+D | L + M | 1 | - |

L‑4 stojí na hotovém `B11`: řádková vrstva existuje, takže rozřezání se píše **jednou** nad plánem
řádků, ne pětkrát v per-element smyčkách.

---

## 🔀 Paralelizace - zóny a pravidla

**Zóna = oblast, kde si dvě sezení šlapou po prstech. Pravidlo: v jedné zóně jedno sezení.**

| Zóna | Co obsahuje | Kolik sezení naráz |
|---|---|---|
| **1** | Caché data agend (`ZastavkoveJrAd`/`Mhd`, `KnizniJr*`) | **1** |
| **2** | Caché infrastruktura (`API.Sablony`, seed, registr agend, EDInputForm `.cls`) | **1** |
| **3** | Kreslicí jádro + elementy (`Rendering/*`, `ScheduleElementBase`, elementy) | **1**; během B13 **výhradně** - nikdo jiný na elementy |
| **4** | Okna, dialogy, XAML, vzhled | 1–2, pokud jsou to jiné dialogy |
| **5** | Dokumentace, helpdesk, ověřování v buildu (bez kódu) | libovolně |

**Dva mechanické limity, které tohle nastavují:**

1. **Vývojový Caché server je jeden** - kompilace tříd je globální akce; dvě kompilující sezení si přepisují
   stav a zlaté testy dostanou falešná selhání. **Jen jedno sezení smí kompilovat Caché**, i kdyby pracovala
   v oddělené kopii repozitáře → zóny 1 a 2 se **nedají paralelizovat, jen střídat**.
2. **.NET paralelizovat lze, ale pracovní kopie je společná** - pro víc než jedno .NET sezení je potřeba
   **oddělený worktree pro každé**, jinak si mění soubory pod rukama (platí i pro paralelní session kolegy).
   Bez worktree: jedno .NET sezení + libovolně „papírových" (zóna 5).

**Bezpečné kombinace:**
- ✅ **B12 + B10 + zavírání tiketů (B2)** - agenda / fontová cesta / papír
- ✅ **B10 + B12** - fontová cesta je izolovaná od agendy
- ❌ **cokoli na elementech během B13**
- ❌ **B18 + jakýkoli element dialog** - B18 sahá na pole ve všech `.cls`
- ⚠️ **publish je výhradní vždy**

**Dokumentace:** každé sezení dopisuje záznam na začátek changelogu → při paralelním běhu tam vznikne drobná
kolize. Je triviální; případně ať docs uzavírá poslední dobíhající sezení.

---

### 📥 Backlog / budoucí velké tracky

> Audit stavu v1 (scan 2026-07-07) - P-A/P-B/P-C hotové, P-D rehomed do produkční sekce →
> **detail v [`roadmap-archive.md`](roadmap-archive.md)** (Session 2026-07-08). Konsolidace jádra
> hotová, plocha může růst.

- **Zastávkový JŘ business** (parkované helpdesk A2-A5/A6/B4 + TRANSDEV sada) → viz sekce „Zastávkový JŘ - business logika" níže
- **Fáze B - odložené:** **oživení fixní tabulky** (paleta + Fixed editor) + **merged cells** (gated na ni).
  (Conditional formatting seznam pravidel = HOTOVO #1, 2026-07-08.)
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
4. **Zákaznická dokumentace na wiki (post-release)** - 2 dokumenty, seedy HOTOVÉ a aktuální
   (2026-07-13): `uzivatelska-prirucka.md` (část I obecné ovládání + část II zastávkový JŘ;
   nové agendy = nové části) + `plan-a-zmeny.md` („Novinky a plány"). Zbývá: obrázky + publikace.

---

## 🅿️ Zastávkový JŘ - zákaznická fronta

> **Plný stav** (hotové featury + co otestovat pro uzavření helpdesk tiketů) →
> [`customer-requirements-zastavkovy-jr.md §0`](customer-requirements-zastavkovy-jr.md). Zde jen **otevřená
> akční práce**. Vel.: S = do hodiny, M = půl dne, L = víc dní.

### Čeká jen na doběhnutí - blok B2

| Tiket | Uzavírací podmínka (co ověřit, aby se dal zavřít) |
|---|---|
| #7947 | ⛔ **na CB_CSADAD ověřit NELZE** (změřeno 30. 7.): žádná ze 480 kombinací linka×směr nemá vzorovou trasu → větev prodloužení je nedosažitelná, „jede až do" nevznikne ani jednou. Kód je hotový a zpevněný; chce data TRANSDEV (876401/876402) nebo umělou vzorovou trasu. Detail → `customer-requirements §D1` |
| #7955 | `SCHOVAT_V_ROZSAHU`=Automatické → auto-rozdíl zmizí, ruční zůstane; NZ nemizí |
| #13048 bod 2 | finální vizuál zmenšování textu (názvy zastávek, zóny, rezerva u šipky směru) |

### Čeká na někoho jiného - postrčit, není to práce

- **#10660 DPKV** - hotové, zpřístupnit build + proklikat jejich obrázky
- **#7959** - ověření klientem na 876401/876402
- **TR2** implementováno (opt-in `SOUHRN_NA_ZNAMENI`), ⚠️ čeká SK ověření kódování na-znamení - kolega
- **#13048 bod 4 fáze 2** (duplikace řádku u víc nástupišť) - čeká potvrzení KOVED
- **TR8 („<1" místo „0") park** (analýza 2026-07-10): naše časy jsou kumulativní, 0-segment se tiskne
  jako opakované číslo, ne „0". Per-segment sloupec TD nemá. Blokuje potvrzení od TRANSDEV
  (který sloupec - legacy „Průběhy"?). Detail → `customer-requirements §TR/8`.

### Reálná zbývající práce

`B12` #7937 per-směr / per-Tč odsazení (jen když TRANSDEV tlačí)

**`B14` Kolize značky poznámky - čeká na tvoje rozhodnutí** (S, zóna 5; z auditu 30. 7.)
Písmena rozdílů tras zahrnují „H" a „F" = zároveň značky bezbariérových spojů, takže jeden symbol
u minuty nese dva významy a legenda má pod ním dva různé řádky. Změřeno na **9 stránkách**
(0365005, 0365006). MHD tuto chybu nemá - bere písmeno ze sdíleného poolu stránky a obsazený znak
přeskočí; AD by to udělal stejně (malá změna). **Rozhodnutí:** opravit = odchýlit se od legacy
a přebaselinovat zlatý digest 0365005, nebo nechat legacy-shodné. Detail → `customer-requirements §D1`.

**`B15` Podmíněná viditelnost parametrů v náhledové liště** (S, zóna 5; nález 30. 7.)
V parametrové liště nad náhledem jsou vidět „Hodiny od/do", i když časová osa není „Vlastní rozsah" -
lišta neaplikuje podmíněnou viditelnost parametru (v tiskovém dialogu se to chová možná jinak, chce
ověřit obě cesty). Kosmetika, ale mate.

### Bez poptávky nebrat - chce analýzu / datový model / business rozhodnutí

| # | Co | Vel. | Co potřebuje |
|---|---|---|---|
| TR10 | Skrýt spoje o So/Ne/svátcích | S–M | Caché filtr |
| TR7 | Výběr sloupců variant jízdních dob (per-písmeno výběr) | S–M | **zčásti vyřešeno 2026-07-27** (#13048 bod 5): varianty nově dle trasy+nástupiště s max časy (málo sloupců) + parametr „Řazení variant tras: Stabilní" = stabilní pořadí. Zbývá jen případný per-písmeno výběr, až ho TRANSDEV potvrdí nad novým chováním |
| TR6 | Barva linky (bg/text ve vlastnostech linky → volba v šabloně) | M | nový datový model |
| TR9 | Obrácená logika poznámkování (rozsah TČ v 1. tabulce) | M | Caché-input (Studio JŘ) |
| TR5 | Definice cíle - střední (per zast.×směr) + ideál (DB cílů) | M–L | datový model; *(min „cíl per směr" = hotovo přes `POPIS_SMER`=konečná)* |

*(#7937 = blok `B12`. Follow-upy uzavřených tiketů „na vyžádání zákazníka" →
[`customer-requirements §0.4`](customer-requirements-zastavkovy-jr.md).)*

---

## 🟠 Fáze B: Element flexibility - jen odložené body

Z gap analýzy vůči komerčním generátorům. Nezávislé na JŘ (faktury i obecné šablony). Hotová část
(conditional formatting, počítaná pole, min/max, versioning framework) → archiv.

**Fixní (statická) tabulka - analýza + plán HOTOVY (2026-07-12), implementace odložena na poptávku faktur/F3:**

> Rozhodnutí z analýzy: **koncept implementovat** (FastReport/Stimulsoft svět našich uživatelů Table
> objekt čeká; tabulka dává logiku, kterou skupina elementů principiálně neumí - **reflow podmíněných
> řádků** (skrytý řádek zkolabuje, díra nezůstane), **auto výška řádku** (obsah odtlačí řádky pod sebou),
> strukturální editace (insert řádku, sloupcový resize), sémantika pro Caché distribuci + AI).
> Pro JŘ není potřeba (bloky záhlaví = 2×3 bez podmínek) → **nedělat hned, ale s poptávkou faktur / F3**.
> Průzkum stavu: model+render Fixed módu KOMPLETNÍ a na sdíleném jádru (kaskáda, CellRenderer,
> ResolveWidths), 5-tab `TableEditorDialog` funkční vč. „Ze šablony…" na hotový `GetTableTemplate`
> kontrakt (agendy už nabízejí ZAHLAVI_LINKY/PLATNOST_DOPRAVCE/FAKTURA_*).

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

**Prerekvizity (pořadí):** (1) symbolový font Timetable_nove(+SK) do Skia - **základ hotový 9.7.** (embed
TTF + locale-aware `FontResolver.SymbolFontFamily`, single-char symboly zastávkového JŘ); pro linkovou část
zbývá převodní tabulka `GSId` (66+ symbolů, TTF v `C:\DEV\EDISON\TiskJR\_ruzne`) na multichar/kompozity; (2) agenda
`KnizniJrAd`; (3) `LineTableElement` + layout rozšíření (flow paketů, „dotisk poznámek z minulé stránky",
**auto-fit fontu na šířku** - dnes je ComputeFontScale jen výškový); (4) per-linka šablony (viz C4).

> Pilotní část hotová (`KnizniJrAd`/`KnizniJrMhd` datové jádro, element, editor, paleta) → archiv
> 2026-07-04 + detail `linkovy-knizni-jr-implementace-analyza.md` §11-12.

### 🎯 Ukazatel postupu `L‑1…L‑4` - velký kus rozřezaný na slotové kroky

**Smysl:** aby linkový JŘ postoupil v **každé vlně**, i když běží helpdesk. Každý krok končí něčím
dokončeným; velký `L‑4` se otevírá až nad hotovým `L‑3`.

| Krok | Blok | Co | Vel. | Stav |
|---|---|---|---|---|
| **L‑1** | `B5` | Auto-fit fontu na šířku u linkové tabulky | S–M | ✅ 29. 7. → archiv |
| **L‑2** | `B10` | Symbolový font - linková část (`ZNAK` sloupce, multichar/kompozity, `GSId` 66+) | M→S | ⬜ |
| **L‑3** | `B11` | Row-plan vrstva = jedna sdílená kreslicí smyčka (**R1 extrakce řádkového jádra**) | M | ✅ 29. 7. → archiv |
| **L‑4** | `B13` | 2D bin-packing / `Strih` - rozřezání široké tabulky do stránkových bloků | L | ⬜ **odemčeno** (L‑3 hotový) |
| - | `C4` | Linková výchozí šablona do distribuce (dá se přilepit ke kterémukoli slotu) | S | ⬜ |

**Mapování na staré číslování kroků:** 42b = L‑2 · 43 = L‑1 + L‑3 + L‑4 · 44b = C4.
Odložené body C2–C9 níže zůstávají jako detailní evidence (C2 = L‑2, C3 = L‑3 + L‑4,
C8 + C9 = `B14`).

### Odložené body knižního/linkového JŘ (z pilotu, seřazené dle hodnoty pro ostré nasazení)

> Pilot uzavřen 2026-07-04 jako „testovací v1" (detail → archiv). Dál dle priorit, ne nutně hned zde.

| # | Co | Proč odloženo / dopad | Odhad |
|---|---|---|---|
| C2 | **Symbolový font Timetable_nove(+SK)** do Skia (krok 42b, gate R5). **Zastávkový JŘ hotový 9.7.** - embed `TimeTable2.ttf`/`TimeTable2SK.ttf`, locale-aware `FontResolver.SymbolFontFamily`, single-char `SpecialniZnak` → piktogram (NotesLegend + DepartureGrid); na-znamení `x/y/z` = text. **Zbývá linková část**: `ZNAK` sloupce LineTable (multichar/kompozitní symboly, převodní tabulka `GSId` 66+ symbolů) | vizuální parita; bez linkové části knižní/výlepový JŘ „tiše lže" u dopravních symbolů (R5) | M→S |
| C3 | **2D bin-packing / `Strih`** (krok 43 + gate R1/R2, *přesunuto z layout tracku 2026-07-07*) - široká tabulka (MHD linka = 100+ spojů) se dnes zmáčkne na šířku místo rozřezání do stránkových bloků. Součást: **auto-fit fontu na šířku** (dnes jen výškový `ComputeFontScale`), flow paketů, „dotisk zbytku legendy na další stránce". **Zde proběhne R1 extrakce sdíleného řádkového jádra** = zavedení **row-plan vrstvy** (`List<RowSpec>`: druh řádku data/header/groupHeader/groupFooter/subrow + buňky → **jedna sdílená kreslicí smyčka** nad `ColumnRenderer.DrawRow`), ať „řádkový layout" (grouping, page-spanning, per-typ-řádku, merged) přestane být per-element `Render` smyčka (dnes N míst → pak 1 datové tvarování). **LineTable = prototyp** - už heterogenní řádky skládá inline. Pozn.: sdílené low-level (buňka/sloupec/kaskáda/šířky) i řádkové podmínky (viditelnost/zvýraznění) jsou **hotové** (pětice levných švů 2026-07-08); row-plan je poslední zbývající řádková vrstva. Zastávkový JŘ to nepotřebuje (DepartureGrid = málo sloupců). | nutné pro reálný tisk MHD linek; největší kus, dělá se jednou pořádně | L |
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

> Pre-produkční průchod HOTOVÝ 2026-07-13 (Sonnet 5, anti-halucinace, retry, standardní dialog,
> jednostránkový JŘ import) → changelog. Zde jen dlouhodobé ladění:

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
