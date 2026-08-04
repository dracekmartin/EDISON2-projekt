# Principy návrhu nových featur - kód i UX

Závazná reference pro **každý nový request** (od zákazníka, od kolegy, od AI). Slouží proti dvěma protichůdným pastím:

1. **Premature concrete** - všechno hardcoded, každý request = ad-hoc hack, technický dluh roste neudržitelně.
2. **Premature abstraction** - všechno generické dopředu, ale podle špatných os variace; špatná abstrakce se opravuje hůř než duplikace.

Cíl: **střední cesta s disciplinou** - kdy zobecňovat, kdy ne, jak změnu rozdělit mezi kód, UI panel, dialog a šablonu.

---

## 1. Kódová rozhodovací mřížka

### Když přijde nový request, polož si 3 otázky **v tomto pořadí**

**Q1: Kolik míst v kódu je dnes dotčeno?**
- 1 element → implementuj **uvnitř toho elementu**, neabstrahuj.
- 2+ elementy se stejným chováním → **už teď** zvaž extrakci. Není to spekulace, máš data.

**Q2: Mám 3 reálné use cases, ze kterých vidím skutečné osy variace?**
- Ne → implementuj specifické, neabstrahuj. Spekulativní abstrakce je horší než duplikace.
- Ano → osy variace jsou pozorované, ne vymyšlené.

**Q3: Je to známý pattern z venku (layout engine, style cascade, observer)?**
- Ano → riziko špatné abstrakce je nižší (vzor je ověřený). Můžeš abstrahovat dřív.
- Ne (vlastní doménový vynález) → drž specifické, dokud neuvidíš tlak z 3 stran.

### "První varianta platí architektonickou cenu"

Když přijde **první varianta** chování (např. „šipka vpravo místo vlevo"), nepřidávej jen if/else. Refaktoruj na strukturovaný layout (`ComputeArrowBox()` + `ComputeContentBox()`). Pak druhá, třetí varianta jsou **levné** (jiný výpočet boxu). Bez refaktoru je každá nová varianta copypasted hack.

**Cena per varianta:**

| Stav | 2. varianta | 3. varianta | N-tá varianta |
|---|---|---|---|
| Bez refaktoru | drahá (duplikace) | velmi drahá | exponenciální |
| Po refaktoru | levná (nový case) | levná | lineární |

### Anti-signály - **NEABSTRAHUJ**, i když to láká

- „Někdy v budoucnu by se to mohlo hodit."
- „Když to udělám obecně, ušetřím si práci u příštího requestu."
- Generická verze vyžaduje vymyslet osy variace, které **dnes nikdo neuviděl**.
- Generická verze je „čistší" než specifická (estetická preference, ne engineering).

### Pozitivní signály - **JE ČAS** abstrahovat

- Kopíruješ podobnou logiku z 2.→3. místa.
- Jeden konceptuální change vyžaduje modifikaci v 3+ souborech.
- Requesty od různých klientů opakovaně narážejí na **stejnou osu variace**.
- Refaktor by **odstranil** existující kód (subtraktivní), ne přidal nový (aditivní).

→ Pokud vidíš 2+ z těchhle signálů, abstrahuj. Pokud žádný, drž specifické.

### Reálný příklad z TD codebase

**Dobře načasovaný refaktor:** `Rendering/CellRenderer.cs` + `CellDefaults.cs` + 4-vrstvá CellStyle kaskáda. Extrahovaná **až když** 3 elementy (`TableElement`, `StopListElement`, `DepartureGridElement`) měly duplicitní cell styling. Pattern (CSS-like cascade) je ověřený zvenku. ✅

**Špatně načasovaný refaktor (kterému se vyhnout):** `IPositionableSidekick<T>` framework pro „element vedle elementu na 4 pozicích" implementovaný proto, že DPKV chce šipku vpravo. ❌ - 1 use case, vymyšlené osy variace.

---

## 2. UX rozhodovací mřížka

### Tři vrstvy, kam property může jít

| Úroveň | Co tam patří | Příklad v TD dnes |
|---|---|---|
| **1. Properties panel** | Nejčastěji měněné věci, vždy viditelné | 4 base barvy Header/Data bg/text |
| **2. Modal dialog** | Kompletní detail s kaskádou, otevírá se zřídka | `CellStyleEditor` přes Enter / dvojklik |
| **3. Šablona / preset** | Per-customer / per-use-case kombinace, nepatří do elementu | `section-templates/*.json` |

**Pravidlo:** každá nová property si **musí** vybrat vrstvu předem. „Jen to hodím na konec panelu" je akumulační anti-pattern.

### Rozhodovací mřížka pro umístění property

1. **Mění uživatel tuhle hodnotu v 50%+ šablon?**
   - Ano → panel (rychlý přístup).
   - Ne → dialog.

2. **Je to per-řádek / per-sloupec / per-buňka (granularita)?**
   - Ano → dialog (sloupcový editor).
   - Ne → panel nebo dialog podle bodu 1.

3. **Mění to fundamentální chování elementu (ne styling)?**
   - Ano → panel, vrchní část.
   - Ne (jen styling) → CellStyle kaskáda v dialogu.

4. **Existuje rozumný default, který pokrývá 80% případů?**
   - Ano → schovej za „pokročilé" / dialog.
   - Ne → panel (musí být lehce přístupné).

5. **Lze to nastavit přímou manipulací na canvasu?**
   - Ano → preferuj direct manipulation, property je read-only zrcadlo.
   - Ne → panel/dialog.

### Existující patterny TD - držet disciplinovaně

- **Rychlý přístup × cascade** - 4 base barvy v panelu, kompletní cascade v dialogu.
- **Tri-state placeholder pro dědění** - `null = dědit`, XAML zobrazí *italic šedý* placeholder zděděné hodnoty; „×" reset jen při explicit override. Pattern z `CellStyleVM`.
- **Promoted shortcut (max 1 per element)** - záměrná výjimka pro „nejčastější toggle" (např. `Tučné záhlaví` v TableElement). Pokud máš 2+, vrátil ses k akumulaci.
- **Multi-element editing** - Enter na multi-select otevře dialog společných vlastností. Snižuje tlak na panel.
- **PropertyDescriptor `Group` field** - skupiny v deskriptorech existují; render je dnes flat, ale infrastruktura připravená pro collapsible groups.

### Anti-patterns - vyhýbat se

- „Hodím to na konec panelu" - akumulační anti-pattern.
- Property, která vypíná druhou, **bez** vizuálního schování té druhé.
- **Maskovaný enum** - dva booly, kde druhý má smysl jen při zapnutém prvním
  (`PISMENA_VARIANT` + `PRVNI_BEZ_PISMENE`). Příště **jeden enum se 3 volbami** - vzor
  „Výlukové poznámky: Včetně / Bez výluk / Jen výluky". Existující dvojice se nechávají
  (`visibleWhen` to drží snesitelné), nové nevznikají.
- **Nový bool „zobrazit kategorii obsahu X"** - viz stopka v §5.3.
- Modal dialog s 10+ taby - preferuj méně tabů s lepší organizací.
- Stejná property na 2 místech s nejasným pořadím priority.
- Per-customer hacky uvnitř elementu (`IsForDpkv: true`) → patří do šablony.

---

## 3. Checklist před implementací **každého** nového requestu

Kódová strana:

```
[ ] Q1: Kolik míst dnes dotčeno? (1 → uvnitř elementu; 2+ → zvažuj extrakci)
[ ] Q2: Mám 3 reálné use cases pro abstrakci? (ne → drž specifické)
[ ] Q3: Je to známý pattern zvenku? (ano → abstrahuj dřív; ne → drž specifické)
[ ] Vyžaduje to layout vocabulary refactor v elementu? (první varianta → ano)
[ ] Faktorováno tak, aby šlo později promote výš? (clean boundaries, no leakage)
```

UX strana:

```
[ ] Vrstva: panel / dialog / šablona? (rozhodovací mřížka výš)
[ ] Group: do které skupiny v panelu/dialogu?
[ ] Conditional: zobrazit jen když je něco jiného true?
[ ] Default: 80% případů má tuto hodnotu?
[ ] Cascade: dědit z parent style? (pokud styling)
[ ] Direct manipulation: lze vizuálně místo property?
[ ] Multi-edit safe: rozumně funguje při výběru více elementů?
[ ] Není to maskovaný enum? (druhý bool závislý na prvním → jeden enum, §2 anti-patterns)
[ ] Není to nový bool „zobrazit kategorii X"? (→ STOPKA §5.3, trigger = kategorie-checklist)
```

Pokud na něco z toho neumíš odpovědět **před implementací**, je to signál, že request je nedomyšlený. Vrať se k zadání.

---

## 4. Klasifikace requestu - typy variací

Když přijde request, klasifikuj ho do jednoho z **3 typů**. Cena implementace je v každém typu odlišná.

### Typ 1 - Nová hodnota v existující ose variace (LEVNÉ)

Nová enum hodnota, nová barva, nový padding. Element už má strukturu pro tuto osu - stačí přidat case.

**Příklady:**
- Přidat `TextHorizontalAlignment.Justify` (osa „zarovnání" už existuje).
- Přidat nový barcode typ EAN-8 (osa „BarcodeType" už existuje).

**Cena:** 1× případ implementace, žádný refaktor.

### Typ 2 - Nová osa variace v existujícím elementu (STŘEDNÍ)

Element zatím nemá strukturu pro tuto osu - **první varianta vyvolá lokální refaktor**. Subsequent variations pak levné.

**Příklady:**
- DPKV chce pozici šipky vpravo (osa „pozice šipky" zatím neexistuje, šipka je hardcoded vlevo).
- Per-row padding ve StopList (osa „per-row override" zatím neexistuje, vše je per-column).

**Cena:** 1× layout refaktor + N× cheap case implementations.

### Typ 3 - Nová topologie / fundamentálně jiné chování (DRAHÉ)

Request nepasuje do **žádné existující** layout vocabulary daného elementu - musíš element rozšířit o nový režim, nebo komponovat z primitivů.

**Příklady:**
- Šipka „obíhající všechny zastávky po obvodu" - nezapadá do box layoutu.
- Tabulka s nested expand/collapse řádky - fundamentálně jiná topologie.

**Cena:** výrazné rozšíření elementu, nový element, nebo escape přes kompozici/šablonu. **Před implementací zvaž, zda nejde řešit per-customer šablonou** místo featury v jádře.

### Pravidlo při plánování

U každého requestu **rozlišuj typ 1 / 2 / 3** předem. Bez toho jsi překvapen složitostí a odhad rozsahu je špatný. Typ 2 vyžaduje **explicitní krok „layout refactor"** v plánu, ne implicitní „nějak to dáme".

---

## 5. Per-customer divergence - kdy patří do šablony, ne do elementu

Když dva klienti chtějí výrazně jiný vzhled, **necpe se to do jedné šablony** s 50 přepínači. Mají **dvě šablony**.

**Signály, že request patří do šablony, ne do elementu:**

- Je to per-klient (DPKV chce X, TRANSDEV chce Y, KORID chce Z).
- Kombinace nastavení dohromady tvoří „styl" - fontové páry, barvy, layout.
- Bez té kombinace by request nedával smysl (žádný klient by nechtěl 50% z nich).
- Tlak na element by vedl k záměrným výjimkám / promoted shortcuts.

**Infrastruktura, kterou TD má:**
- `section-templates/*.json` - custom šablony sekcí.
- `Template.EditorPreferences` per template.
- `CustomProperties` bag na `TemplateElement` a `Section`.

**Co by se mělo doplnit (viz roadmap):**
- Per-customer **celé šablony** (DPKV-zastavkovy.json, TRANSDEV-zastavkovy.json) jako shipped defaults.
- `CustomProperties` UI editor - aby klient mohl per-element custom data nastavit z editoru.

### 5.1 Kam patří nastavení - osa SCOPE (ne vizuál/generace)

**Rozhodující osa NENÍ „vizuál vs generace"** (vizuální věc může být klidně parametr, např. časová
osa). Rozhodující je **scope = jak stabilní nastavení je:**

| Scope | Domov | Proč |
|---|---|---|
| **Fixní pro daného zákazníka** (pokaždé stejné) | **šablona** (vlastnost elementu/sekce/šablony) | šablona JE zákazníkova fixní konfigurace |
| **Mění se napříč šablonami / per tisk** | **parametr** | jeden šablona + N voleb místo N skoro stejných šablon (proti exponenciálnímu množení) |

Vizuál ↔ generace určuje jen **mechanické doručení**, ne kde to uživatel zadá:
- **Rendering** nastavení → TD renderer čte přímo z elementu/šablony (Caché se neúčastní).
- **Generační** nastavení → musí dojít do Caché `GetData` jako IN parametr (Caché nevidí šablonu).

### 5.2 Uživatelské přepínače poznámek = nadmnožina + TD filtr (NE Caché parametr)

Pro **show/hide přepínače** (např. #7955 „ruční komentář přebíjí auto") platí preferovaný model:

- **Caché emituje nadmnožinu** - všechny poznámky/anotace, každou **otagovanou** (`KATEGORIE`
  rozdíl-trasy/omezení/bezbariér/ruční, + `SPOJ`). Generuje vždy stejně, parametr-agnosticky.
- **Přepínač = render-time filtr v TD** (jako `VisibilityExpression`) → schová/zobrazí. Změna =
  překreslení, **žádný Caché re-fetch**. Filtr po sobě **prořeže + přečísluje legendu**.
- Přepínač pak může bydlet ve **šabloně** (fixní zákazník) i **parametru** (proměnné) jednotně -
  obojí jen krmí filtr. Tím odpadá generační parametr a PrintDialog nebobtná o zákaznické konstanty.

**Hranice modelu (kde NEplatí):**
- **Mění strukturu dat** (FTL sloupce dnů, které zastávky/stránky vzniknou) → zůstává v Caché generování.
- **Deterministické z dat, bez volby** (nepoznámkovat `MHDSPNPZN`, texty omezení) → zůstává v Caché
  (není to přepínač).

Doplňuje §6 „Caché ↔ TD kontrakt o legendě": pokud TD potřebuje filtrovat → Caché posílá **raw
otagovaná data**, ne hotový předfiltrovaný text.

### 5.3 Kategorie obsahu - STOPKA na nové show/hide booly (rozhodnutí Jan 2026-08-04)

Rodina „zobrazit/schovat kategorii obsahu" má ~11 členů: parametry (místenky, kola, poznámky
linky, souhrn bezbariér, souhrn na znamení) + filtry prvku poznámek (3 typy záznamů, výluky,
3 kategorie poznámek linky). Každý vznikl stejně: tiket → nový bool + C# property + pole
formuláře + test. Řada je **otevřená** (klient kategorie čísluje „Poznámky 1, 2…"), osa variace
je pozorovaná z mnoha stran - podle Q2 je zralá na abstrakci, ale **bez nového požadavku se
nepřestavuje** (funguje, testy drží, přestavba by byla náklad bez užitku).

**Pravidlo:**
- **Žádný nový bool „zobrazit kategorii X"** jako samostatná property nebo parametr.
- **Trigger:** první další request tohoto typu (další kategorie poznámek, další značka
  u odjezdů…) platí architektonickou cenu - zavede kategorie-checklist níže a sveze se
  jako jeho první přírůstek.

**Cílový model (kategorie-checklist):**
- Server taguje každý záznam kategorií (**už dělá** - `odjezd-atribut`, tarifní/všeobecné/
  radiobus, výluky) a metadata kategorie nesou **default viditelnosti** („místenky: default
  schované").
- Prvek drží jen **mapu overrides** (např. `HiddenCategories`), ne bool per kategorie.
  Nová kategorie = jen serverová změna; JSON schéma šablony, C# model ani formulář se nemění.
- UI: skupina „Filtrování" = **checklist generovaný z kategorií** (labely z metadat),
  místo ručně psaných polí. Samovysvětlující, žádný helpText na interakce.
- PrintDialog přestane bobtnat: show/hide volby nové generace **nejdou do parametrů**
  (jsou to render filtry - jen překreslení, žádný re-fetch). Parametr zůstává jen pro věci
  měnící **co se vygeneruje** (zdroj stanoviště, rozdělení sloupců).
- Známé náklady do plánu triggeru: stávající `Show*` properties nechat žít jako legacy čtení
  (aditivně) nebo zmigrovat; EDInputForm nemá checklist field typ → buď generovaná bool pole
  z metadat kategorií, nebo malý aditivní field typ v EDControls.

**Stav po 4. 8. (rozhodnutí Jan):** čtyři parametry sestavy (`POZNAMKY_LINKY`, `TISK_MISTENEK`,
`TISK_KOLA`, `SCHOVAT_AGREGAT_BEZBARIER`) **zrušeny** a vypínání obsahu je jen v šabloně - server
posílá nadmnožinu otagovanou kategorií. Rodina tím **neroste, ale ani se nepřestavěla**: ubyly
4 parametry, přidaly se 3 volby na prvku. Cílový checklist z metadat naráží na konkrétní blokátor -
`ElementPropertyAccessor` naváže jen property cesty (`A.B.C`), **ne mapu**, takže by chtěl rozšířit
binder nebo přidat typ pole do EDControls. Do té doby jsou to obyčejné vlastnosti.

⚠️ **Kategorie nese dvě nezávislé věci** - (1) co se vypíná v šabloně a (2) jestli se značka
**překládá na glyf** symbolového fontu. Po přejmenování kategorie 4. 8. zůstalo druhé místo na starém
jménu a kolo vyjelo jako cizí piktogram (testy zelené, držely si staré jméno samy). Proto mají jména
jediný zdroj `Models/NoteCategories.cs` a testy je berou odtud, ne z literálů.

**Mikropravidlo hned:** závislost prvek→parametr **zviditelnit** - pole prvku, která bez
zapnutého parametru nic nedělají (kategorie linky vs. `POZNAMKY_LINKY`), zašednout/schovat,
ne jen popsat v helpTextu.

---

## 6. Existující debt - kontext při rozhodování

Některé patterny v TD mají známé výjimky / smell. Při rozhodování o novém requestu počítej s nimi:

### Scalar property override v `Resolve*Parent()` - anti-pattern

Element má scalar property (`MinuteFontSize`, …) jako legacy fallback, a v `Resolve*Parent()` ji unconditionally aplikuje přes `with { ... }` po Resolve. **Přebíjí user-set hodnotu z kaskády.**

Historicky **2× opraveno** v DepartureGrid. Při přidávání nové cascade vrstvy se to znovu objeví, pokud nedáš pozor.

**Pravidlo:** scalar fallback se aplikuje **jen** když je v kaskádě **všude** null. Viz `mvvm-patterns.md §Anti-pattern: scalar property override`.

### Multi-zone elements (5+ zón)

`DepartureGridElement` má 5 zón a **záměrné výjimky** z 2-zone patternu (MinuteStyle, AnotaceSymbolStyle flat v panelu). Není to bug, je to dluh.

**Pravidlo:** pokud budeš dělat další element s 5+ zónami, vrať se sem a dohodni shared pattern. Nepřidávej **další** záměrné výjimky bez review.

### Caché ↔ TD kontrakt o legendě / atributech

Caché posílá hotovou legendu poznámek včetně atributů. TD nemůže filtrovat podle viditelnosti sloupců (request D5 v `customer-requirements-zastavkovy-jr.md`).

**Pravidlo:** při přidávání novy generovaných textů v Caché si vyjasni, **kdo filtruje**. Pokud TD potřebuje filtrovat → posílej raw data, ne hotový text.

---

## 7. Quality background concerns

Neprimární, ale na radaru. Drž si je v hlavě při implementaci, neoptimalizuj preventivně.

### Performance

- **Render frame target < 16ms** (60 fps). Měřit jen když uživatel reportuje lag, ne preventivně.
- **Structured data parsing vždy cachované** - per-frame re-parse `DataContext` v elementech je zakázané. Cache invalidovat při změně dat / properties (memory `feedback_schedule_cache_perf`).
- **Async-only Caché volání** (R1) - žádné synchronní `MLRest` v UI threadu.
- **Žádné optimalizace bez profileru** - code clarity > teoretická rychlost. Optimalizovat až po měření, ne podle dojmu.

### Bezpečnost (basic)

- **User-provided JSON / PDF / AI prompt = untrusted** → sanitize at boundary, ne uvnitř.
- **`VisibilityExpression` whitelist tokenizer** ✅ existuje, držet (žádný eval-style přístup k novým výrazům).
- **File paths z user input** - `Path.GetFullPath` + ověření že výsledek je pod očekávaným rootem (anti path-traversal).
- **Velikostní limity** na imports - image upload 5 MB ✅, PDF import zatím neomezený (doplnit ~50 MB cap).
- **AI prompty** - user data jen jako data (`{POLE}` placeholder), ne přímou string interpolací do prompt template.

### Testování

- **Engine logika** (`Models/Layout`, `Expressions`, `Serialization`, `MmMath`, `CacheAgendaProvider` parsing) - xUnit test povinný při změně.
- **UI / rendering / drag&drop / SkiaSharp / WPF dialogy** - manuální test v běžícím buildu, ne automated.
- **Opravený bug z anti-patternu** (např. scalar override, idempotence VM↔Model) - regression test povinný, aby se to nevracelo.
- **Kdy založit `TemplateDesigner.Tests/` projekt:** až přijde první feature, která se týká testovatelné oblasti. Ne preventivně.
- **CI/CD:** jednorázový setup až bude testů 20+.

---

## 8. Reference

- [`customer-requirements-zastavkovy-jr.md`](customer-requirements-zastavkovy-jr.md) - konkrétní zákaznické požadavky, na kterých se tyhle principy reálně aplikují.
- [`elementy-architektura.md`](elementy-architektura.md) - architektura specializovaných JŘ elementů.
- `.claude/rules/mvvm-patterns.md` - Pattern 1 (single-zone) / Pattern 2 (dual-zone) styling.
- `skill `novy-element`` - 10-kroková reference pro přidání nového typu elementu.
- `.claude/rules/architecture-rules.md` - závazná pravidla R1–R5 (async, errors, JSON, MVVM, versioning) + „must-not-happen" scénáře.

---

## 9. TL;DR

> **Kód:** Implementuj **minimum**, faktoruj **pro pozdější promotion**, abstrahuj **až tě k tomu vyžene pozorovaná duplikace ze 3 míst** nebo známý pattern zvenku. První varianta nové osy si vynutí lokální refaktor (typ 2) - *to je v pořádku*, je to ta správná cena. Spekulativní abstrakce stojí víc než duplikace.
>
> **UX:** Panel = rychlý přístup (50%+ usage). Dialog = kompletní granularita. Šablona = per-customer presety. Každý nový property musí vědět, kam patří, **předem**. Tri-state placeholder, multi-edit a per-customer šablony jsou ventily, které tlak na panel významně snižují.
