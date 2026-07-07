# Produkční připravenost — analýza + rizika nasazení (2026-07-07)

> **Cíl analýzy:** sepsat vše otevřené k produkčnímu nasazení TD a najít věci, na které jsme
> zatím nenarazili — hlavně **co zamyká strukturu šablony** tak, že bychom ji museli měnit
> (breaking) v příští verzi. Grounded na skutečném stavu kódu (4 paralelní průzkumy, ne jen roadmapa).
>
> **Optika priorit:** **A = zamyká strukturu šablony** (udělat PŘED prvním produkčním uložením,
> jinak nás to donutí k breaking změně + migraci později) · **B = provozní nasazení** (nutné, ale
> strukturu nezamyká — dá se dohnat kdykoli) · **C = governance/distribuce** (odloženo, jen rozhodnout
> scope pro v1). Do produkce jde nejdřív **zastávkový JŘ (AD)**; knižní/linkový je pilot.

---

## 🔴 A. Zamyká strukturu šablony — vyřešit PŘED produkcí

Tyto věci **nejsou** v roadmapě a jsou hlavním přínosem této analýzy. Pokud natečou reálné
zákaznické šablony do DB, jejich odložení = budoucí breaking change + nutná (a v některých
případech neproveditelná) migrace.

### A1. Klíče sloupců (`StopColumn.Key`) jsou magic-string literály — ✅ VYŘEŠENO 2026-07-07
- `StopColumn.Key` se **persistuje do JSON** šablony, ale hodnoty (`"Zone"`, `"Km"`, `"Tc"`,
  `"StopName"`, `"Mhd"`, `"TravelTime"`, `"RouteLetters"`, `"BaseRoute"`, `"TravelTime#i"`) jsou
  literály roztroušené ve `switch` (`LineTableElement.cs:38-43,208,214,237-259`,
  `StopListElement.cs:22-28,531`). Jen `TripKey="Trip"` je konstanta.
- **Dopad:** jakékoli přejmenování klíče → uložená šablona s tím klíčem se **tiše rozbije**
  (neznámý klíč → prázdná hodnota/header, žádná chyba, žádná migrace).
- **Náprava (levná, teď):** povýšit klíče na konstanty (vzor `ElementTypes.cs`) + krátký unit test,
  který zamkne mapu klíč→význam. Tím se klíče stanou stabilním kontraktem.

### A2. Domnělé dva modely zvýraznění — ✅ VYŘEŠENO 2026-07-07 (byl to mrtvý kód)
- **Nález po průzkumu:** `DepartureGridElement.ColorRules` (`List<ColorRule>`) byl **mrtvý kód** —
  deklarovaný a klonovaný, ale **nikde nekonzumovaný** (žádný render, žádný editor). Nebyl to živý
  konkurenční model, jen pozůstatek. Odstraněno (pole + Clone řádek).
- Živý je jen **jednopravidlový** `RowHighlightExpression` + `RowHighlightColor` (DynamicTable/StopList/
  LineTable). Ten **strukturu nezamyká**: multi-pravidlo lze přidat **aditivně** jako budoucí
  `List<ColorRule> RowColorRules` vedle stávajících polí → staré šablony se nerozbijí.
- **Provedeno:** dead-field cleanup + docstringy fixnuté (jedno pravidlo = dodávaný kontrakt,
  multi = budoucí aditivní, odloženo 2026-07-05); `ColorRule` třída ponechána jako rezervovaný
  budoucí model. Žádná migrace nutná.

### A3. `TableElement` (dormant) × `DynamicTableElement` — ✅ VYŘEŠENO 2026-07-07
- `TableElement` je „dočasně mimo, dormant" (`DynamicTableElement.cs:18`), ale drží skoro identickou
  JSON tvář (`RowHeight/ShowHeader/HeaderHeight/HeaderStyle/DataStyle/Columns/SummaryRows/…`).
  `DynamicTableElement` má navíc `MinRows/MaxRows/RowVisibilityExpression/RowHighlightExpression`.
- **Dopad:** až se Fixed tabulka „oživí", buď dvě schémata divergují, nebo se slučují = breaking.
  Riziko, že vzniknou produkční šablony s „mrtvým" typem `TableElement`.
- **Provedeno (varianta demarkace):** z UI už `TableElement` nešel (paleta i menu = dynamická);
  navíc odebrány mrtvé větve `ElementTypes.Table` z továrny (`CreateElementByType`/`GetDefaultElementSize`)
  → nově ho nic nevyrobí. Tripwire test (`ElementFactoryTests`) + docstring označující spící stav.
  **Zachováno:** třída, `JsonDerivedType` (legacy read), `TableEditorDialog` (legacy edit) + budoucí
  oživení Fixed tabulky = pár řádků zpět. Behavior-preserving, bez migrace.

### A4. Verzování šablon — ✅ VYŘEŠENO 2026-07-07 (jen dokumentace; NEbumpovat)

> **Korekce po průzkumu:** migrátor je hotový **i důkladně otestovaný** (9 testů: transformace,
> přerazítkování, řazení verzí, forward-compat guard, selhání→výjimka). Doporučení „bumpnout na
> 1.1.0 teď" **zamítnuto** — jde proti vlastní konvenci projektu (*bump jen pro breaking change +
> migrace*) a bez migrace by falešně označilo každou „1.0.0" šablonu jako migrovanou. Kosmetický
> rozdíl „1.0"/„1.0.0" je funkčně irelevantní (VersionComparer je srovnává jako rovné). Jediná reálná
> mezera = dokumentace flat→style jako nemigrovatelné → zapsáno v `template-schema-versions.md`.
> Původní analýza (níže) ponechána jako záznam; závěr viz tento box.


- `TemplateMigrator` framework existuje, ale **pipeline je prázdná** (žádná reálná migrace;
  `CurrentVersion = "1.0.0"`, `TemplateMigrator.cs:26,39-41`). Migrator se **volá při každém loadu**
  (`TemplateSerializer.cs:107`, souborová i Caché cesta), ale pro `"1.0.0"` skončí hned na `cmp==0`.
- **Nesoulad verzí mezi vrstvami:** C# = `"1.0.0"` (`Template.cs:13`), Caché ukládá `"1.0"`
  (`Sablony.cls:112`, `CacheTemplateService.cs:191,218`). Funkčně to `VersionComparer` sjednotí,
  ale metadata mají dvě „pravdy".
- **Historická breaking změna „flat text props → composable style" NENÍ migrovaná a je
  neproveditelná** — proběhla bez bumpu verze, takže staré i nové šablony sdílejí `"1.0.0"` → nelze
  je odlišit → nešlo by poznat, kdy migraci spustit. Stará souborová `.json` z doby před refaktorem
  (03/2026) by při načtení **tiše ztratila styling** (`System.Text.Json` neznámá pole ignoruje).
  *Zmírnění:* DB je primární úložiště od 2026-06-30, tj. dlouho po refaktoru → šablony v DB jsou
  v novém formátu. Riziko se týká jen starých souborových šablon (pokud nějaké v produkci budou).
- **Náprava (teď):** (1) sjednotit default verzi na jednu hodnotu napříč C# i Caché;
  (2) **bumpnout `CurrentVersion` na `1.1.0` teď**, aby všechny nově uložené produkční šablony
  nesly odlišitelnou verzi → příští breaking změna má čistý výchozí bod pro migraci;
  (3) zdokumentovat, že flat→style je nemigrovatelná (akceptované riziko).

### A5. Deserializace tiše polyká neznámá pole
- `JsonSerializerOptions` (`TemplateSerializer.cs:154-168`) nemá `UnmappedMemberHandling` →
  neznámé/odstraněné property zmizí bez chyby (silent corruption).
- **Náprava (levná):** aspoň **logovat** neznámá pole při loadu (přes `OnDeserialized` / custom
  handling). Změní tichou ztrátu na detekovatelný signál. Nemusí failovat, stačí zaznamenat.

> **Poznámka k extensibility bagu:** `TemplateElement.CustomProperties` a `Section.CustomProperties`
> existují (`TemplateElement.cs:51`, `Section.cs:73`), ale fakticky se nepoužívají — nové featury
> jdou jako plné nullable properties (aditivní, non-breaking, OK). Bag je jen `Dictionary<string,string>`
> → strukturovaná data stejně vynutí plnou property. Bag tedy **není** pojistka proti A1–A3.

---

## 🟠 B. Provozní připravenost nasazení — nutné, ale strukturu NEzamyká

Dá se dohnat kdykoli po produkci bez dopadu na uložené šablony. Zčásti už v roadmapě (D3/D4, T6).

### B1. Committed dev credentials + MLP token v trackovaném appsettings
- `dotnet/templatedesigner.appsettings.json` obsahuje **commitnuté** připojení k dev serveru:
  `Server 192.168.16.1`, login `martin.hubata`, **`Mlp "167"`** (auth token), interní IP. Navíc se
  **zapéká do .exe** jako embedded baseline. Git historie to nese opakovaně.
- Není to heslo, ale zbytečná expozice. Reálná session v produkci stejně jde z launch kontraktu.
- **Náprava:** baseline v trackovaném souboru vyprázdnit/neutralizovat; nechat plnit z launch args
  / lokálního `*.local.json`. (Anthropic klíč je řešen správně — prázdný v tracked, reálný v
  gitignored local, nikdy necommitnut.)

### B2. Neexistuje ověřený Caché deploy proces
- Třídy (agendy, `API.Sablony`, EDInputForm schémata) se na server dostávají **jen ručním** Studio/
  Atelier importem. MCP `cache_compile` na TD zdroje nefunguje a **hlásí „up-to-date" i bez uploadu**
  (tichý false-positive → riziko, že se změna nedostane na server). Žádná pipeline/skript.
- **Náprava:** aspoň **dokumentovaný manuální checklist** deploye Caché tříd na zákaznický server
  (které třídy, pořadí, ověření). Automatizace později.

### B3. `SelfContained=false` → produkce vyžaduje předinstalovaný .NET 10 runtime
- Bundle runtime neobsahuje (`SingleFileCEdison.pubxml`). Žádný instalátor to neřeší.
- **Náprava:** rozhodnout — buď `SelfContained=true` (větší .exe, žádná závislost), nebo runtime jako
  prerekvizita release (dokumentovat).

### B4. Build vázaný na konkrétní stroj
- `MLRest.dll`/`EDISONAPI.dll` přes `HintPath C:\Edison\*.dll`, ruční `publish.ps1`, žádná CI →
  build není reprodukovatelný na čistém stroji.
- **Náprava:** dokumentovat build prerekvizity; případně vendored DLL do repo/lib.

### B5. Newtonsoft.Json 7.0.1 (NU1903) — akceptované riziko
- Vynuceno legacy `MLRest.dll` (očekává 7.0.0.0). Nelze povýšit bez rebuildu MLRest.
- **Stav:** vědomý dluh (memory `project_mlrest_newtonsoft_debt.md`), warning nepotlačovat. Jen evidovat.

### B6. Test třídy v produkční assembly (T6)
- Např. `PdfImport.ScheduleDetectorTest` se kompiluje do produkční assembly.
- **Náprava:** úklid před release (přesun do test projektu / `#if DEBUG`).

### B7. Dokumentační drift
- `CLAUDE.md` tvrdí .NET 9 (reálně **.NET 10**, `net10.0-windows7.0`) a PdfPig 0.1.9 (reálně **0.1.13**).
- **Náprava:** sjednotit tabulku stacku v CLAUDE.md.

---

## 🟡 C. Governance a distribuce šablon — odloženo, rozhodnout scope pro v1

**Nezamyká JSON strukturu šablony** (případné `IsSystem`/`IsLocked`/vendor-ID jde do Caché **metadat**,
což je aditivní a bezpečné). Ale zamyká **provozní model**, tak ať víme, do čeho jdeme.

### Stav dnes (ověřeno v `Sablony.cls`)
- Storage `^ALVA.TemplateDesigner.Templates(IdFa,"DTA",TemplateID,"Metadata"|"Data")` — **čistě
  per-IDfa (per firma)**, sdíleno mezi uživateli firmy. Metadata: `Name/AgendaCode/Author/CreatedAt/
  ModifiedAt/ModifiedBy/Version`. `Author`/`ModifiedBy` jsou **jen informativní, ne access control**.
- **Žádné rozlišení systémové (M-line) vs zákaznické šablony**, žádné zámky, žádné právo „kdo smí
  editovat/mazat". Kdokoli ve firmě může šablonu smazat/přepsat.
- **Distribuce výchozích šablon = jen ruční Import JSON** (`TemplateDesignerViewModel.cs:796`).
  V repu je `templates/knizni-jr-ad-vychozi.json` (AD; MHD chybí).

### Klíčové provozní riziko
**M-line dnes NEMŮŽE bezpečně aktualizovat systémovou šablonu u zákazníka.** Storage je per-IDfa;
aktualizace přes Import JSON buď přepíše (stejný GUID) i zákaznické úpravy, nebo vytvoří duplicitu
(nový GUID). Není vrstva „systémové read-only × zákaznické". Chybí i centrální přehled „co kde je".

### Rozhodnutí pro v1 (návrh)
- Pokud v1 **nedistribuuje** žádnou M-line šablonu, kterou by bylo třeba centrálně aktualizovat
  (zákazník si tvoří vlastní) → governance lze odložit, ruční Import JSON stačí.
- Jakmile M-line bude dodávat a udržovat systémové šablony → nutné aspoň minimum z **B1/R6**
  (příznak systémová + read-only u zákazníka) **před** tím, než u zákazníků vzniknou úpravy, které
  by se přepsaly. Roadmap B1/B2/B5 gated přes R6 — vše ⬜.

---

## 📋 Doporučené pořadí

1. **A1–A5 (zámek struktury) — teď, před prvním produkčním uložením.** Levné, jednorázové, chrání
   před breaking změnou. A1 (klíče→konstanty) a A4 (verzní baseline) mají nejlepší poměr cena/riziko.
2. **B1 (credentials) + B7 (doc drift) — teď, triviální hygiena.**
3. **B2/B3/B6 — před release deploy** (D3/D4 v roadmapě).
4. **C (governance) — rozhodnout scope pro v1**, implementace až s reálnou potřebou distribuce.

**Mimo scope produkční v1 (potvrzeno):** C3 2D bin-packing (jen knižní/linkový JŘ), Fáze E/F,
merged cells, layout-reservation.
