# Analýza: rozdíly tras spojů v zastávkovém JŘ — legacy vs nové + návrh

> **Účel.** Velké průřezové téma: jak se v zastávkovém JŘ zobrazují **rozdíly v trasách spojů**
> (spoj jede jinudy / nestaví / pokračuje dál / má jiné jízdní časy). Dokument popisuje (1) co
> přesně dělal legacy systém (15 hardcoded variant + sloupce Pracovní/Trasa min.), (2) co umíme dnes
> v AD i MHD, (3) vazbu na ticket #7947 („jede až do XY"), (4) kritiku legacy návrhu a (5) návrh
> lepšího, přehlednějšího systému.
>
> **Zdroje:** legacy MAC `C:\DEV\EDISON\JRMAIN\CACHE\JRapiLinPrtMhd1.mac` (ř.267-300 ověřeno přímo),
> `JRapiLinPrtMhd.mac` (`OmTrasa`), formátovač `ALVA.TISKMHD.Data`; náš `ZastavkoveJrAd.cls`,
> `ZastavkoveJrMhd.cls`, `StopListElement.cs`, `DepartureGridElement.cs`. Konkrétní řádky z MAC
> formátovače (`Data.cls`) jsou orientační (čteny agentem), koncept ověřen.
>
> **Datum:** 2026-06-20.

---

## 0. Klíčové rozlišení — DVA různé mechanismy, ne jeden

Pod „rozdíly tras" se v legacy skrývají **dvě nezávislé věci**, které se často pletou. Pochopit je
odděleně je půlka řešení:

| | **A) Anotace odlišnosti** (písmena u minut) | **B) Variantní časové sloupce** (Trasa min. 1-15) |
|---|---|---|
| **Co vyjadřuje** | „tento spoj jede jinudy než ostatní" — textem | „tato varianta trasy má tyto jízdní časy" — sloupcem čísel |
| **Kde se zobrazí** | horní index u minuty + řádek legendy | samostatný sloupec ve StopListu (seznam zastávek) |
| **Příklad** | `7:15ᴮ` + legenda „B = neobsluhuje zast. Lesní hřbitov" | sloupec s časy `0, 3, 7, <, 12` (`<` = nestaví) |
| **Legacy** | `OmTrasa^JRapiLinPrtMhd` | `prutmbmp` detekce + sloupce TrasaMin1-15 |
| **Máme dnes** | ✅ **plně** (AD i MHD) | 🟡 **jen 1 sloupec** (jízdní čas hlavní trasy) |

**Klíčový závěr:** rozdíly v tom, **které zastávky spoj obsluhuje**, dnes řešíme **čistě anotacemi (A)**
— a to funguje 1:1 s legacy. Mezera je jen v (B): legacy uměl ukázat **víc variant jízdních časů
vedle sebe jako sloupce**, my ukážeme jen jednu variantu.

---

## 1. Co znamenají legacy sloupce (dekódování screenshotu)

Legacy „Sekce seznam zastávek → Sloupce" nabízel tyto sloupce StopListu:

| Sloupec | Význam | Máme? |
|---|---|---|
| **Seznam zastávek** | názvy zastávek | ✅ `StopName` |
| **Časy** | jízdní čas hlavní trasy (= Trasa min. 1) | ✅ `TravelTime` (CAS_JIZDY) |
| **Zóny** | tarifní zóny IDS | ✅ `Zone` |
| **Průběhy** | index/odkaz, která varianta platí na které zastávce | ⬜ |
| **Základní trasa** | čas v základní (referenční) trase | 🟡 `BaseRoute` (jen příznak, ne čas) |
| **Pracovní 2-5** | doplňkové sloupce — Pracovní 2 = značka „x na znamení" před názvem; 3-5 ruční | ⬜ (značky „x" řešíme jinak — viz §6) |
| **Trasa min. 1-15** | **až 15 variant jízdních časů**, každá svůj sloupec | 🟡 jen Trasa min. 1 |

→ Většina sloupců máme nebo je řešíme jinak. **Jádro mezery = „Trasa min. 2-15"** (multi-varianta).

---

## 2. Mechanismus A — anotace odlišnosti (máme ✅)

### Jak to dělá legacy (`OmTrasa^JRapiLinPrtMhd`)
Pro každý spoj z aktuální zastávky porovná jeho **bitmapu obsluhy** (které zastávky staví) se
**vzorovou trasou** (základní/nejčastější). Z rozdílu sestaví text:
- „**neobsluhuje** zast. X" (spoj přeskakuje zastávku, kterou vzor obsluhuje)
- „**jede přes** zast. X" (spoj staví navíc)
- „**jede jen do** zast. X" (spoj končí dřív)
- „**pokračuje přes** X" (spoj jede dál za vzor)

Každému unikátnímu textu přidělí **písmeno** (sada z `PATCH,"mhd","notchar"`, default začíná „B…").
Písmeno se tiskne jako horní index u minut spoje + do legendy „B = …". Dedup: stejný text → stejné písmeno.

### Co máme (AD `RozdilyTras`, MHD alg. 1002)
- **AD** (`ZastavkoveJrAd.RozdilyTras`): 1:1 reimplementace `OmTrasa` — stejné 4 texty, písmena od „B",
  dedup, marker u minut (`pismenoSpoj`) + legenda (kategorie `rozdil-trasy`). **Ověřeno 1:1** na 0365001.
- **MHD** (`ZastavkoveJrMhd`, alg. 1002): bitmapová detekce (`VypocitatBitmapyObsluhy`), písmena a-z.

**Stav: hotovo a ověřené v obou agendách.** Pokrývá „spoj jede jinudy" pro drtivou většinu případů.

---

## 3. Mechanismus B — variantní časové sloupce (mezera 🟡)

### Jak to dělá legacy (`prutmbmp`, ověřeno `JRapiLinPrtMhd1.mac:267-300`)
Pro každý spoj se počítá **časový profil** `prutm` (jízdní časy mezi zastávkami od aktuální) +
**bitmapa** `prutmbmp` (`1` = staví, `<` = projíždí). Pak:
1. Porovná `prutmbmp` proti už existujícím variantám.
2. **Shodná bitmapa → stejný sloupec** (drží se delší čas). **Nová bitmapa → nový sloupec** (varianta).
3. Každá varianta dostane sloupec **Trasa min. N** (N=1..15) a písmeno; spoj → varianta (`pmin(2,Spoj)`).
4. Ve StopListu se varianta tiskne jako **sloupec časů**; zastávky, které varianta projíždí, mají `<`.

Tj. legacy uměl ukázat **vedle sebe víc tras s jejich časy** — čtenář vidí „pracovní spoje jedou trasou
A (časy …), víkendové trasou B (časy …, jiné zastávky `<`)".

### Co máme
- **Jeden časový sloupec** `CAS_JIZDY` (AD `ZakladniCisloTrasy` = nejčastější trasa mezi spoji stavícími
  na vývěsce + `CasJizdyZMapy`). Wire formát zná **jen `CAS_JIZDY`**, žádné `CAS_JIZDY_2..15`.
- `StopListElement` má **jeden** `TravelTime` sloupec (hardcoded čte `CasJizdy`). Žádná podpora víc časových sloupců.

**Důsledek:** linka s jednou trasou → 1:1. Linka s víc variantami tras → ukážeme jen jednu variantu
(hlavní), ostatní varianty časů se ztratí (rozdíly v obsluze ale pořád vyjádří anotace A).

---

## 4. Vazba na #7947 „spoj jede až do XY"

`OmTrasa` (legacy i naše `RozdilyTras`) text **„pokračuje přes X"** existuje, ale klient (#7947) si
stěžoval, že chování u **prodloužení za základní trasu** není dobré. Z analýzy:
- Legacy detekuje hlavně **zkrácení** („jede jen do") dobře; **prodloužení** řeší jen přes „pokračuje
  přes", což je formulačně slabé (popisuje průjezd, ne cíl).
- **Je to součást mechanismu A** (anotace odlišnosti), ne sloupců. Takže #7947 = vylepšit/přidat text
  pro spoje, které jedou **ZA** koncovou základní trasy → „jede až do zast. XY".

→ #7947 patří do `RozdilyTras` (malé-střední rozšíření), **nezávisí** na multi-variantě sloupců.

---

## 5. Kritika legacy návrhu (proč ho neportovat 1:1)

- **15 hardcoded sloupců** — pevný strop, magické indexy, široká tabulka. Když je variant >15 →
  zdvojování písmen, degraduje.
- **Dva paralelní systémy písmen** — varianta (U/V/W… z `mhd,"par",2`) **a** odlišnost (A/B/C z
  `notchar`) se tisknou současně → čtenář vidí dvě sady písmen s různým významem. Matoucí.
- **Pracovní 2-5** — neortogonální mix (Pracovní 2 = „x na znamení", 3-5 ruční) naroubovaný na stejný
  sloupcový mechanismus jako časy. Nečitelné názvy, nejasná sémantika.
- **Working global `^CacheTempJB`** — komplikovaná dedup/komprese/sort logika přes mnoho uzlů.

**Závěr:** mechanismus A (anotace) je dobrý a moderní — držíme. Mechanismus B (15 sloupců) je ta
přebujelá část; **neportovat 1:1**, navrhnout čistěji.

---

## 6. Kde jsou „x na znamení" (vyjasnění Pracovní 2)

Legacy „Pracovní 2" sloupec = značky „x" (na znamení) před názvy. **My to neřešíme sloupcem** — máme
`StopListElement.ShowSignalStopIndicator` (značka „×" u názvu zastávky) + atribut do legendy. Funkčně
ekvivalentní, čistší. (Souvisí s #7161 D5 — skrýt „x" z legendy když je indikátor vypnutý.)

---

## 7. Návrh lepšího systému

Princip: **zachovat mechanismus A jako primární** (textové anotace pokrývají „kdo jede jinudy"
přehledně), a **multi-variantu časů (B) přidat moderně a volitelně**, ne jako 15 hardcoded sloupců.

### 7.1 Návrh datový (Caché wire)
- Místo `CAS_JIZDY` (skalár) emitovat **`CASY_VARIANT`** = pole variant: `[{ "ZNAK":"A", "CASY":[0,3,7,null,12] }, …]`
  (null/`<` = varianta zde nestaví). `null` varianta = hlavní (dnešní `CAS_JIZDY`).
- Caché už variantu spoje umí spočítat (bitmapa obsluhy + W-mapa časů) — reuse `SpojBitmapa`/`WMapa`.
  Dedup variant přes bitmapu (jako legacy `prutmbmp`, ale bez 15-stropu — kolik vyjde, tolik).
- Zpětná kompatibilita: ponechat `CAS_JIZDY` (hlavní varianta) → staré šablony fungují beze změny.

### 7.2 Návrh renderovací (TD StopList)
- `StopListElement` podporuje **N časových sloupců** (dnes 1) — dynamicky dle počtu variant v datech.
  Sloupec = `TravelTime[i]`, hlavička = písmeno varianty. `<`/prázdno u nestavících.
- **Strop přehlednosti** (ne 15): když je variant moc (>3-4), nedávat každou jako sloupec — fallback
  na mechanismus A (anotace „jede jinudy"). Tj. **sloupce jen pro málo variant, jinak text.** To řeší
  legacy bolístku „15 sloupců = nečitelná tabulka".
- Per-column styling už máme (CellStyle kaskáda) → varianty se dají vizuálně odlišit.

### 7.3 Sjednocení písmen
- **Jedna sada písmen** pro variantu i odlišnost (ne dvě jako legacy). Varianta trasy = odlišnost
  trasy; vyjádřit konzistentně (sloupec NEBO anotace téhož písmene), ne dvěma systémy.

### 7.4 #7947 samostatně
- „jede až do XY" = rozšířit `RozdilyTras` o text pro prodloužení za vzor. Nezávislé na 7.1-7.3,
  menší, udělatelné dřív.

---

## 8. Co umíme teď vs legacy — shrnutí

| Schopnost | Legacy | Nové (AD+MHD) |
|---|---|---|
| Anotace „spoj jede jinudy" (A/B/C + legenda) | ✅ | ✅ 1:1 |
| „neobsluhuje / jede přes / jede jen do / pokračuje" | ✅ | ✅ |
| „jede až do XY" (prodloužení) | 🟡 slabé „pokračuje" | ✅ „jede až do zast. X" (#7947, 2026-06-21) |
| Jeden časový sloupec (hlavní trasa) | ✅ | ✅ |
| Multi-variantní časové sloupce (Trasa min. 2-15) | ✅ (15 hardcoded) | ⬜ |
| Sloupec Průběhy (která varianta kde) | ✅ | ⬜ |
| „x na znamení" | ✅ sloupec Pracovní 2 | ✅ jinak (indikátor + legenda) |

---

## 9. Doporučení / fázování

1. **#7947 „jede až do XY"** ✅ **HOTOVO (2026-06-21)** — `RozdilyTras` větev prodloužení vypisuje
   „jede až do zast. <konečná>" místo „pokračuje přes". Pozn.: prodloužení vzácné (vzor=nejdelší trasa).
2. **Multi-varianta časů (7.1-7.3)** (🔴 velké) — datový + rendererový zásah. Udělat, **až přijde
   reálný zákazník** s variantní linkou. Navrhnout **moderně** (dynamický počet sloupců + strop +
   fallback na anotace), ne 15 hardcoded.
3. **Sjednocení písmen + Průběhy** — součást bodu 2, ne zvlášť.

**Mezitím:** držet mechanismus A (anotace) jako primární — pokrývá „kdo jede jinudy" přehledně i bez
sloupců. Pro běžné linky (jedna trasa) jsme 1:1; variantní linky ukážou hlavní variantu + anotace.

## 10. Ověřeno na reálných datech (2026-06-21, MCP gold oracle CB_CSADAD)

- **KV_DP:** AutoPozn (`mhd,"not"`) **vypnuté na všech linkách** → rozdíly tras se nepoužívají. Mřížka
  FTL (`MHD_TYP_R`) jen ve skupinách 42/429/tmp (0425017), i tam bez AutoPozn.
- **CB_CSADAD má AutoPozn zapnuté** (0365001, 0345061, …). Reálné příklady:
  - **0345061** (J.Hradec): **8 variant tras** (markery B-J), texty „neobsluhuje zast. …" + „jede jen
    do zast. …". Multi-varianta (B): TrasaMin1-9 sloupce, sdílené zastávky různé časy + `<`.
  - **0365001** (Písek): 2 varianty — TrasaMin1 přeskakuje „Lesní hřbitov" (`<`) s posunutými časy
    (16/18/…) vs TrasaMin2 staví (15/19/…). Marker B = „neobsluhuje zast. Lesní hřbitov".
- **Potvrzeno §0:** přeskočení zastávky **posune i následující časy** → mechanismus B (časové sloupce)
  nese reálnou kvantitativní informaci, ne jen vizuál. My ukazujeme jen hlavní variantu (časy vedlejší
  varianty jsou ±pár min nepřesné). Anotace (A) topologii ukazuje správně.
- **„pokračuje"/prodloužení nenalezeno** v 0365001 ani 0345061 — potvrzuje, že prodloužení za vzor je
  vzácné (vzor = nejdelší trasa). #7947 je tedy latentní oprava znění, projeví se na lince s prodloužením.

> **Pozn. k prioritě bodu 2:** gold oracle pro variantní linky **máme** (0345061 = 8 variant, 0365001
> = 2). Až se rozhodneme dělat multi-variantu časů, je 0345061 ideální referenční linka.
