# CLAUDE.md - Kontext pro AI agenta

## Co je tento projekt

**TemplateDesigner** je WPF desktopová aplikace pro tvorbu šablon tiskových sestav.
Náhrada staršího nástroje postaveného v InterSystems Caché/Globals.
Při navrhování nových featur, bez v potaz, že v budoucnu by mohl být nástroj, alespoň částečně, ovládán pomocí AI.Dobre

Systém: **1. Designer** (tento projekt) - vizuální návrh šablon → **2. Print Engine** (integrovaný) - šablona + data = PDF.

---

## Aktuální stav

Kde jsme a co dál drží **[`roadmap.md`](.claude/docs/roadmap.md)** (vršek = priorita), hotová práce
**[`changelog.md`](.claude/docs/changelog.md)**, otevřené problémy roadmapa nebo tiket. Sem se stav
**nepíše** - platil by jen do dalšího commitu a stál by kontext v každé session.

## Neodvoditelné invarianty

Tři věci, které se z kódu nepoznají a jejichž porušení něco rozbije uživateli:

- **`AgendaVariantCode` je stabilní persistovaný klíč** (`ZASTAVKOVY_JR_AD`, `KNIZNI_JR_MHD`…) -
  hodnoty se **NIKDY** nemění. Visí na nich `agendaCode` v JSON šablonách, DB index `ByAgenda`,
  uživatelské preference a EDISON launch kontrakt; přejmenování = zničené šablony u zákazníka.
  Dvouosý model: **Agenda** (členění) × **Varianta** (datový zdroj, ekvivalent legacy `VolanoZ`).
  Detail v [`architektonicka-rozhodnuti-agendy.md`](.claude/docs/architektonicka-rozhodnuti-agendy.md).
- **Databáze je primární úložiště šablon** (od 2026-06-30). Otevřít / Uložit / Uložit jako → DB;
  soubor je jen Importovat / Exportovat JSON (export nemění původ šablony). Ctrl+S u nové zakládá
  v DB pod jménem, u existující tiše updatuje.
- **Agenda `GENERAL`** (`cacheClass = ""`) = šablona bez DB vazby, jen statický obsah.
  Auto-discovery ji nenajde (nemá Caché třídu), proto se do registru injektuje synteticky
  a `CacheAgendaProvider` ji short-circuituje ve všech metodách.
- **Launch ze shellu předvolí agendu** - `inputParam` kontraktu ve tvaru `Agenda=<kód>`
  (JŘ uzel stromu → `ZASTAVKOVY_JR_AD`, MHD uzel a MHD2 tlačítko → `ZASTAVKOVY_JR_MHD`),
  neznámý kód → default. Bez argumentů běží TD standalone. Obecný kontrakt (`EdisonLaunchArgs`,
  MLRest ze session shellu) viz `EDControls/CLAUDE.md`; `shellGuid` se záměrně nepřebírá,
  TD má vlastní session GUID bez vazby na ACTIVITY zámek shellu.

## Technologický stack

| Technologie | Použití |
|---|---|
| .NET 10, WPF | UI framework |
| SkiaSharp 3.119.2 | Renderování canvasu i PDF |
| SkiaSharp.Views.WPF 3.119.2 | SKElement WPF kontrola |
| System.Text.Json | Serializace šablon |
| EDControls (ProjectReference) | EDViewModel, RelayCommand |
| ZXing.Net 0.16.9 | BarcodeElement |
| PdfPig 0.1.13 | PDF import (extrakce textu, čar, obrázků) |

---

## Pravidla pro psaní kódu

- **Jazyk kódu: anglicky** (třídy, metody, proměnné); komentáře a UI texty: **česky**
- **UI texty bez cizích výrazů** („per", „split"…) a s jednotným slovníkem - fyzické stanoviště
  zastávky = vždy **„označník"** (ne nástupiště/stanoviště/sloupek); kanonické termíny viz
  `ux-conventions.md §8`, wire názvy polí (`NASTUPISTE`) se nemění
- **Žádné dlouhé pomlčky (em dash)** - v kódu, komentářích, UI textech, dokumentaci i odpovědích
  vždy běžná pomlčka „-" (rozhodnutí Jan 2026-07-28; UI labely hlídá drift test)
- Každá akce uživatele = `IDesignerCommand` (undo/redo podporováno)
- Pozice elementů jsou **relativní k sekci**, jednotky = **milimetry**
- `Height = null` = dynamická výška (print engine počítá); `IsLocked = true` = nelze editovat
- `VisibilityExpression = null` = vždy viditelný; neprázdný = výraz vyhodnocuje ExpressionEvaluator
- `SKElement.PaintSurface` registrovat **jen v code-behind**, nikdy v XAML
- **Vždy `CVar`** pro MLRest volání; nikdy `object[]` v novém kódu
- Data binding syntaxe: `{NAZEV_POLE}` - konzistentní napříč celým systémem
- ObjectScript: **nikdy `_` v dot-notation** (`obj.KEY_NAME`) - `_` je operátor konkatenace; použij `obj.%Set("KEY_NAME", v)`
- Nová okna vždy v `dotnet/Windows/<Název>/` se vzorem `Window + ViewModel : EDViewModel`

---

## Komunikace s uživatelem (závazné)

1. **Standardní, srozumitelná terminologie.** Nevymýšlej „czechglish" žargon ani interní zkratky.
   Zavedený český termín použij česky, obecné programátorské pojmy anglicky - konzistentně a
   pochopitelně (drží se to code-style konvence pojmenování).
2. **Popisuj položky z uživatelského hlediska, high-level.** Když spolu procházíme roadmapu / body /
   úkoly, řekni **o co jde a proč to uživatele zajímá** - i u technické věci vysvětli **podstatu**
   (deduplikace, výkonový problém, konzistence UI…), **ne interní identifikátory / názvy tříd /
   kódové termíny**, do kterých uživatel v kódu nevidí. Konkrétní kódový detail jen když si o něj řekne.

## Vývojová pipeline (agent ji jede sám z jednoho zadání)

Tohle je **pasivní pravidlo, ne skill k ručnímu vyvolání** - platí pro každou úlohu automaticky.
Uživatel dodá **featuru + volitelný kontext** a dělá **prioritizační a architektonická rozhodnutí**;
mechaniku (kód → testy → docs → wiki → commit) veze agent sám, bez čekání na dílčí pokyny.

### Výchozí zdroj úloh - roadmapa

Když uživatel neurčí konkrétní úlohu, **ber práci z [`roadmap.md`](.claude/docs/roadmap.md)** (vršek = priorita) - to je výchozí vstup pro pipelinu níže:

- Vyber **nejbližší rozumnou položku** (respektuj pořadí, závislosti, gates R1–R7, velikost). **Oznam, co beru**, a pokračuj - neptej se na svolení u každé, ale nech prostor to přesměrovat.
- **Explicitní zadání od uživatele vždy přebíjí** roadmapu.
- U **velkých / architektonických** položek se zastav a nech rozhodnutí (co / jak / priorita) na uživateli - ne jen tak spustit velký track.
- Po dokončení položky (krok 8) rovnou navrhni **další dle roadmapy** → plynulý tok bez opakování zadání.

Pipeline pro každou vybranou úlohu:

0. **Zadání & kontext** - ujasnit konkrétní featuru, vstřebat extra kontext od uživatele, zařadit do scope/roadmapy.
1. **Projít kód** - přečíst relevantní existující kód a pochopit současný stav (ne psát naslepo).
2. **Navrhnout testy** - čím se featura ověří (xUnit / Caché `%UnitTest` / ruční golden path), ideálně před implementací.
3. **Implementovat** - dle pravidel (`.claude/rules/`, R1–R5, kód EN / komentáře CZ, CVar, MVVM). Auto-build hook hlásí chyby.
4. **Otestovat & iterovat** - spustit testy, u UX ověřit v běžícím buildu; body 3–4 opakovat, než to vypadá dobře.
5. **Lokální docs** - changelog vždy; roadmap (hotové → archiv, posun šipky); `CLAUDE.md` **jen když
   se změnil invariant nebo pravidlo** - výčet featur se tam nevede.
6. **Wiki** - jen pokud featura mění user-facing chování **a projekt má linknutý článek**: aktualizovat seed + sync (TipTap-safe, mapa seed↔UUID). Jinak přeskočit.
7. **Commit** - Janův scope, granular, `TD -` prefix. Gating viz níže.
8. **Další krok** - po commitu navrhnout **další logický krok v roadmapě** nebo jiný smysluplný postup.

**Gating commitu (chytré):**
- **Jednoduchá / mechanická změna, kterou nedává smysl ručně proklikat** (a testy projdou) → **commit ihned**, nečekat.
- **Vizuální / nová / větší featura, kterou má uživatel proklikat a vyzkoušet** → **necommitovat**, ukázat
  výsledek a **počkat na potvrzení**; teprve po potvrzení commit + krok 8.

**Co zůstává na uživateli:** co se dělá (priority), architektonická rozhodnutí, high-level kontext aplikace.
Ne mechanika kroků 1–8.

> ⚠️ **`roadmap.md` drž ČISTOU - jen aktivní úkoly.** Když je něco hotové (i rozhodnutí), **přesuň
> to KOMPLETNĚ do [`roadmap-archive.md`](.claude/docs/roadmap-archive.md)** - nenechávej to v roadmapě
> jako odškrtnuté „✅ hotovo" řádky. V aktivní roadmapě nesmí viset starý nepořádek; zůstává jen to,
> co se má dělat (+ krátký pointer do archivu, kde detaily jsou). Platí i pro rozhodovací tabulky.

### Wiki - mapování a sync (krok 6 pipeline)

Linkované wiki články drží **`.claude/wiki-map.json`** (allow-list). Pravidla:

- **Synchni JEN články z mapy.** Co v mapě není, se nikdy netkne (folder-level: platí nejbližší `wiki-map.json` nad měněným souborem; dnes jedna na úrovni TD).
- Krok 6: pokud featura mění **user-facing chování** pokryté některým `seed`em z mapy → aktualizuj ten seed a synchni jeho článek (MCP `update_wiki_article`, `id` z mapy). Jinak wiki přeskoč.
- **TipTap-safe markdown:** konvertor **NEUMÍ tabulky** (slije je do jednoho odstavce) → piš odrážky / definiční seznamy. OK: nadpisy, seznamy, odkazy, tučné/kurzíva, blockquote, `---`.
- **Seed = source of truth, wiki = zrcadlo** - na wiki needitovat ručně.
- Ověření po syncu drž **lehké** (nečti celý obsah zpět - velký článek přesáhne limit odpovědi; ověř `number`/`updatedAt`).
- Budoucí tvrdá pojistka (až se v praxi ukáže, že se na sync zapomíná): pre-push guard čtoucí `wiki-map.json`, případně CI přes REST.

---

## Architektura - vrstvy systému

```
5. Aplikace         WPF Designer, EDISON tisk, Server API, CLI batch
4. Renderer         SkiaSharp → WPF canvas / PDF soubor
3. PageLayoutEngine Section repeating, flow layout, page breaking (žádné UI/SkiaSharp)
2. Expressions      ExpressionEvaluator, PlaceholderFormatter, AggregationResolver
1. Models           Template, Section, TemplateElement, DataContext, DataSchema
```

**Klíčové hranice:** `PageLayoutEngine` (vrstva 3) nezná SkiaSharp - vstup: `Template + DataContext`, výstup: `List<Page>`.
`PageRenderer` (vrstva 4) nezná WPF - kreslí na `SKCanvas`. Díky tomu jde systém rozdělit:

- **TemplateDesigner** = vrstvy 1–5 (vše pohromadě, jeden .csproj)
- **EDISON tisk** = vrstvy 1–4 (bez WPF designeru, headless PDF)
- **Server/API** = vrstvy 1–4 (HTTP endpoint: šablona + data → PDF)

Preview v designeru = plný engine s mock daty. "Tisk" = tentýž engine s reálnými daty → PDF.

---

## Co projekt není

- Není to WYSIWYG Word/Excel editor
- Sekce jsou povinné - ne free-form canvas bez struktury
- V1 nepodporuje import starých šablon z InterSystems Globals

### Záměrně jinak - neřešit

Vědomá rozhodnutí, která se znovu neotevírají (ne mezery):

- **Databáze jako source of truth pro konfiguraci a šablony** - dává smysl pro doménovou logiku (Wave 1–3)
- **Univerzální Section třída** - jednodušší než pevná hierarchie (Crystal Reports)
- **Absolutní pozicování jako default** - správné pro tiskové sestavy s pevným layoutem
- **PropertyDescriptor a EDInputForm paralelně** - ne cut-over, per element migrace (Wave 4)
- **Server-side migrace šablon** - autoritativní, ne klientská (Wave 3)

---

## Publish - distribuce build .exe

Single-file `.exe` se generuje skriptem `publish.ps1` v rootu projektu:

```powershell
cd C:\Users\miesb\source\EDISONMAIN2\TemplateDesigner
.\publish.ps1
```

Skript volá `dotnet publish` s profilem `SingleFileCEdison` → výstup do `C:\Edison\TemplateDesigner.exe` (~20 MB, self-extracting bundle s `templatedesigner.appsettings.json` jako embedded resource).

**Před spuštěním** - zavři běžící TemplateDesigner.exe (skript hned na začátku ověří lock a friendly errne, pokud je soubor uzamčený).

**Pozn.:** Publish přes Visual Studio GUI je občas rozbitý (silent skip GenerateSingleFileBundle, build prošel ale single-file se nevyrobil). CLI variantou tohoto skriptu se to obchází.

---

## Pravidla (`.claude/rules/`) - načítají se vždy

⚠️ **Načte se celá složka, ne jen soubor odpovídající globu** (ověřeno 2026-07-29; dřívější
tvrzení „načítá se pro `dotnet/**`" nebylo pravdivé). Proto sem patří **jen to, co má být
v každé session** - referenční mapy a procedury ne, ty jdou do `docs/` nebo na skill.

| Soubor | Obsah |
|---|---|
| `architecture-rules.md` | **Závazná pravidla R1-R5** (async Caché, IPopupDisplayer, CVar wire, MVVM bez WPF, versioning) + „must-not-happen" scénáře |
| `cache-api.md` | Wire pravidlo R3, prezentační nápovědy, aditivní evoluce kontraktu, ObjectScript konvence |
| `implementation-patterns.md` | Pasti: barvy, RenderContext, ComboBox event ordering, SkiaSharp quirks, debugging stylů |
| `mvvm-patterns.md` | Styling patterny, kaskáda stylů buněk + anti-patterny, CellStyleEditor |
| `ux-conventions.md` | Dialogová tlačítka, tooltipy, zkratky, kanonické termíny UI popisků |

## Reference (`.claude/docs/`) - čti, když ji potřebuješ

Nenačítá se sama. Sáhni po ní podle toho, co děláš:

| Soubor | Kdy |
|---|---|
| `architecture.md` | struktura projektu, mapa zodpovědností, vzor okna/dialogu, souřadnice, DesignerState |
| `data-model.md` | C# datové třídy, JSON formát šablony a schématu |
| `cache-database.md` | mapa globálů MHD (LSO, ZST, CHR, ENUMS, SETTINGS) |
| `cache-business-logic.md` | fáze MHD tiskového enginu, working global `^||TiskMHD`, SpecZnak |
| `ad-data-mapa.md` | kde leží AD data (`^JRL`/`^JRZ`/`^JRSET`) |
| `elementy-architektura.md` | tabulkové jádro, sdílené vs. per-element |
| `architektonicka-rozhodnuti-agendy.md` | R1-R7 - **před novou fází agend** |
| `feature-design-principles.md` | rozhodovací mřížka - **u nového requestu na featuru** |

Nový typ elementu = skill **`novy-element`** (10 kroků + drift brána), ne dokument.

## Dokumentace v `.claude/docs/` - kam co evidovat

> ⚠️ **NEZAKLÁDAT nový soubor na každou novou věc.** Složka je konsolidovaná do logických celků
> (2026-07-13, 26 → 18 souborů) - nový záznam patří téměř vždy do jednoho z nich. Index
> s kategoriemi: [`.claude/docs/README.md`](.claude/docs/README.md).

**Směrovací tabulka - nová informace → kam patří:**

| Co zapisuji | Kam |
|---|---|
| Dokončená práce | `changelog.md` (+ hotový bod z roadmapy KOMPLETNĚ do `roadmap-archive.md`) |
| Otevřená/plánovaná práce, backlog | `roadmap.md` (sekce dle fáze/tracku) |
| Zákaznický požadavek, stav tiketu | `customer-requirements-zastavkovy-jr.md` §0 |
| Architektonické rozhodnutí pro agendy (R1–R7 a jejich detaily) | `architektonicka-rozhodnuti-agendy.md` - **závazné, číst před novou fází** |
| Jak fungují elementy / tabulkové jádro / symboly JŘ | `elementy-architektura.md` |
| Kde jsou AD data (globály, nastavení linky, poznámky) | `ad-data-mapa.md` |
| Zjištění o legacy systémech (TiskMHD/TiskJR/RG), gap vs TD | `legacy-nahrada.md` |
| Breaking změna JSON šablony / migrace | `template-schema-versions.md` |
| Text pro zákazníky (novinky, návody) | wiki seedy: `plan-a-zmeny.md`, `uzivatelska-prirucka-*.md` |
| Pravidlo, které má platit v každé session | `.claude/rules/*.md` (pozor: načítá se celá složka) |

**Nový soubor je oprávněný jen pro:** (a) jednorázovou analýzu, která se po vytěžení do celků výše
**smaže** (git historie stačí), (b) nový velký trvalý celek (≈ nová fáze/doména) - pak přidat řádek
do README indexu. Před založením se vždy zeptat: „nepatří to do existujícího celku?"

Kdy po které referenci sáhnout viz tabulka **Reference (`.claude/docs/`)** výše. Aktivní track
C+D drží `linkovy-knizni-jr-implementace-analyza.md` (§11-12 odložené body).
