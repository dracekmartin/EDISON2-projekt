# Governance a distribuce šablon — koncept (2026-07-07)

> Model pro hybrid: **M-line distribuuje systémové šablony** + **zákazníci tvoří vlastní**. Koncept
> odsouhlasen 2026-07-07 (Jan). Odvozeno z legacy REPORTGENERATOR („MlineSestava"), modernizováno pro
> JSON/GUID svět TD. Toto je **návrh (krok 1 governance z roadmapy)** — implementace až se M-line
> distribuce reálně spustí. Legacy detail viz [`analyza-stav-a-legacy-nahrada-2026-07.md`](analyza-stav-a-legacy-nahrada-2026-07.md).

## Mentální model — dvě zásuvky v jednom globálu

Šablony žijí v `^ALVA.TemplateDesigner.Templates(IdFa, ...)`. Governance = dvě větve dle IdFa:

- **Zásuvka `MLINE`** (IdFa = `"MLINE"`) — **naše systémové šablony.** Jedna sdílená sada pro všechny
  firmy (ne kopírovaná do každé). Zákazník **read-only** — vidí, otevře, tiskne; needituje/nemaže.
- **Zásuvka firmy** (IdFa = reálná firma, např. `CB_DP`) — **zákaznické šablony.** Soukromé per firma,
  plně editovatelné.

Picker („Šablony v databázi") **spojí obě větve**: nahoře systémové (odznak „od M-line", zamčené),
pod tím vlastní (editovatelné).

## Životní cyklus systémové šablony

1. **Autorování (u nás).** M-line návrhář otevře TD na **dev/master** systému „jako MLINE" a šablonu
   navrhne + uloží → jde do MLINE zásuvky masteru. (Master = náš dev/release; **rozhodnutí 2026-07-07:
   zatím žádný separátní master systém, stačí dev/release.**)
2. **Zabalení do release.** Obsah MLINE zásuvky se vyexportuje jako **JSON** a přibalí do release
   (stejnou cestou jako Caché třídy — žádný zvláštní kanál, **ne** legacy GOF/SMB).
3. **Nasazení (u zákazníka).** Release naleje naše šablony do zákazníkovy **MLINE zásuvky**.
   **Klíčové pravidlo: aktualizace sáhne JEN na větev `MLINE`, nikdy na zákaznickou** (izolace při
   update — přebráno z legacy, které se osvědčilo).

## Zákaznický flow „kopie → úprava"

1. Zákazník otevře systémovou (MLINE) šablonu → **zamčená** (view/tisk, ne přepis).
2. Chce vlastní variantu → **„Uložit jako"** → nová šablona s novým GUID **pod jeho firmou**.
3. Od té chvíle je kopie jeho a nezávislá — příští aktualizace našeho originálu se jí nedotkne.

## Co už existuje vs co je nová práce

| Prvek | Stav |
|---|---|
| „Uložit jako" → nový GUID → zásuvka firmy (`SaveToCacheAsAsync`, `Guid.NewGuid()`) | ✅ existuje |
| Storage per-IdFa | ✅ existuje |
| Picker ukáže i MLINE větev (dnes `ListTemplates` čte jen session IdFa) | 🔧 sjednotit firmu + MLINE |
| Otevřít MLINE šablonu (dnes `LoadTemplate` bere jen session IdFa) | 🔧 zdrojová IdFa |
| **Zámek** MLINE — server odmítne zápis/mazání v MLINE mimo distribuční cestu; Ctrl+S na MLINE → vynutí „Uložit jako" | 🔧 nová kontrola server-side |
| **Distribuce** — export/import MLINE šablon jako JSON v release | 🔧 nová (malá) |

## Přehled / UI komponenta (EDList + EDFilter)

Picker („Šablony v databázi") **už je** `EDList` + `EDFilter` (data přes `ALVA.TemplateDesigner.EDList.Sablony`,
čte `^ALVA.TemplateDesigner.Templates(IdFa,...)` s filtrem agendy + hledáním) — ne monolit v TD. Díky tomu
je to **jedna komponenta, tři použití** dle scope/filtru:

| Použití | Scope | Kde běží |
|---|---|---|
| Zákaznický picker | firma + MLINE | dnes TD dialog; **vložitelné i do EDISON shellu** (bez spuštění celého návrháře) |
| M-line autorování systémových | session = `MLINE` | náš dev — stejný EDList, přihlášeni „jako MLINE" |
| M-line cross-firma admin přehled *(odloženo)* | celý globál + filtr firmy | admin nástroj — **tentýž EDList**, širší scope + sloupec/filtr firmy |

**Extrahovatelnost:** vytažení z TD dialogu do jiného hostu = obalit stejný `EDList`+`EDFilter` jiným
oknem, ne psát znovu (list + filtr + data jsou už oddělené od návrháře). Není to „headless" (pořád UI
seznam), ale reuse mimo celý TD. Řádek → klik otevře návrhář nad šablonou (stejná UX jako legacy list sestavy).

**Flat list vs strom (rozhodnutí):** legacy měl **strom** (USER/MLINE → oblasti → sestavy) — oblasti byly
uživatelské složky na organizaci **stovek** sestav. My máme **flat list** a horní osy stromu pokrýváme
filtry: firma/MLINE = scope+odznak, modul = AgendaCode. **Flat stačí pro v1** (pár šablon per agenda).
Funkci legacy „oblastí" nahrazuje **filtr + kategorie** (roadmap PC5 „Kategorie + popis" = pole v metadatech,
podle něj EDList grupuje/filtruje) — aditivní, nerozbije nic. **Full folder-tree = YAGNI**, do budoucna lze
z listu udělat složitější hierarchii, až to objem vyžádá.

## Rozhodnutí 2026-07-07

- **Master autorování** = náš dev/release, žádný separátní master systém (zatím).
- **Verzování** = **neřešit zatím.** Naše šablony se při aktualizaci přepíšou celé (jejich se nesáhne).
  „Přišla novější, aktualizovat?" + verze/otisk = až bude potřeba.
- **Read-only** = **vynutit server-side** (na rozdíl od legacy, kde `MlineReadOnly` flag backend nikdy
  nekontroloval — byla to jen klientská iluze).
- **Přehled = flat `EDList`** (ne legacy strom); firma/MLINE + agenda = filtry, legacy „oblasti" →
  kategorie (PC5). Složitější hierarchie později, až to objem vyžádá.

## Vědomě NEkopírovat z legacy

- **GOF export přes `%Library.Global.Export` na SMB share** + staging globály → nahrazeno JSON v release.
- **Fragmentace distribuce do desítek per-agenda tříd** (`ADREPORTGEN.Api` atd.) → jedna cesta.
- **Trik ID `<1000` / `≥1000`** jako kolizní ochrana → v JSON světě řeší GUID/stabilní klíče.
- **Detekce větve podle prefixu stringu** `SestavaId` (`"MLINE-..."`) → čistě přes IdFa subscript.
- **Verzování** — legacy ho nemělo; my ho zatím taky odkládáme (viz výše), ale je to vědomé, ne opomenutí.

## Navazuje (samostatné, ne součást distribuce)

- **Přiřazení šablony k tisku** (roadmap PC „navazující featury"): „linka X → výchozí šablona Y".
  O používání, ne o distribuci — řešit zvlášť.
- **Audit log** (kdo/kdy/co změnil šablonu) + **ECL právo „pouštět designer"** — legacy paralela
  (`^AlvaReportGeneratorLog`, ECL 360080002), mapovat 1:1 až s implementací governance.
