# Review návrháře 2026-09-21

Celkový průchod kódem (kvalita, duplicity, konvence, UI popisky) se zaměřením na to, co
znepříjemní přidávání dalších agend. Podklad k položce `B174` v roadmapě.

**Jednorázová analýza** - jak se body odbaví, mažou se odtud; až bude prázdná, smaž celý soubor
i řádek v [`README.md`](README.md) (hygiena složky). Co z toho má platit dál, patří do
`.claude/rules/` nebo do příslušného trvalého celku, ne sem.

Rozsah: `dotnet/` (217 souborů, ~49k řádků), `CACHE/ALVA/TemplateDesigner/` (66 tříd),
`.claude/rules/`. .NET testy v době review zelené (1858).

## Přehled

| # | Nález | Velikost | Stav |
|---|---|---|---|
| 1 | Kontraktové metody agendy se kopírují mezi variantami | M | ⬜ |
| 2 | „Standardní" schéma JŘ existuje dvakrát a už se rozešlo | M | ⬜ |
| 3 | `TemplateDesignerViewModel` má 4217 řádků | M | ⬜ |
| 4 | Allowlist `ServerSideParams` je ruční a má duplicity | S | ⬜ |
| 5 | Dialog sloupců se zakládá kopií (8 barevných příkazů) | S | ⬜ |
| 6 | Náhled na ukázkových datech ignoruje jazyk šablony | S | ⬜ |
| 7 | Výška řádku minut se počítá z jiného písma, než se kreslí | S | ⬜ |
| 8 | Panel vlastností má barvy natvrdo (tmavý motiv) | S | ✅ 21. 9. |
| 9 | Jméno prvku pro uživatele má čtyři zdroje, jeden rozejitý | S | ⬜ |
| 10 | `dev.firmSwitch` zapnutý ve sledovaném configu | XS | ✅ 21. 9. |
| 11 | `TableElement` a `DynamicTableElement` sdílí opsanou kaskádu | S | ⬜ |
| 12 | Drobnosti: tooltipy, CS0108, dvojité `<summary>` | XS | ✅ 21. 9. |
| 13 | Zastaralá tvrzení v `.claude/rules/` | XS | ✅ 21. 9. |

Doporučené pořadí: **1, 5, 3** (budoucí bolest s agendami), pak **6, 7** (malé cílené opravy).

---

## Bude bolet u dalších agend

### 1. Kontraktové metody agendy se kopírují mezi variantami

`ZastavkoveJrAd.cls` a `ZastavkoveJrMhd.cls` mají **13 bajt po bajtu shodných metod** (~120 řádků),
mezi nimi všechny čtyři, kterými agenda popisuje sama sebe: `GetSchema`, `GetParameters`,
`GetTableTemplates`, `GetTableTemplate`. Dál `BuildZastavka`, `BuildOdjezd`, `BuildSkupinaDnu`,
`BuildPoznamka`, `AddAtribut`, `AddPoznamkaZastavky`, `ConvertSpecialniZnak`, `IntToHex2`,
`RgbToHex`. `LinkovyJrAd` a `LinkovyJrMhd` sdílí `BuildPoznamka`, `BuildSkupina`, `FormatCas`.

Nic nehlídá, že zůstanou shodné, a pátá agenda téže rodiny je zkopíruje potřetí.

Návrh: mezivrstva `ZastavkovyJrBase` a `LinkovyJrBase` mezi `BaseAgenda` a variantami; co je
opravdu obecné (`IntToHex2`, `ConvertSpecialniZnak`) do `BaseAgenda`. ⚠️ `RgbToHex` a
`RgbNaHex` v `BaseAgenda` se **liší chováním** (`0;0;0` → prázdné vs. `#FF000000`), rozdíl je
zdokumentovaný u `BaseAgenda:209` a musí přežít.

### 2. „Standardní" schéma JŘ existuje dvakrát a už se rozešlo

`AddStandardJRFields`, `AddStandardJRCollections` a `AddStandardJRParameters` mají v každé
zastávkové agendě vlastní tělo (AD 52/76/191 řádků, MHD 25/48/56). Část rozdílů je legitimní
(AD má sloupek, CIS čísla, dopravce po polích), ale společná část se udržuje dvakrát.

Ze 87 polí schématu se popiskem liší 3 a rozdíl jde čistě po ose AD × MHD, takže dnes to není
vada: `CISLO_LINKY` („Číslo linky" × „Číslo linky (dle licence)"), `LOKALNI_OZNACENI_LINKY`,
`TYP`. Kontroluje to ale jen tenhle průchod - `EDInputFormSchemaDriftTests` hlídá popisky
formulářů prvků, ne schéma agend.

Návrh: shodnou část do základní třídy z bodu 1, zbytek nechat per varianta. Volitelně driftový
test „stejný klíč pole = stejný popisek napříč agendami" s výjimkami na AD × MHD.

### 3. `TemplateDesignerViewModel` má 4217 řádků a 387 členů

Je poctivě rozdělený komentářovými pruhy na ~25 celků (soubor, databáze, schránka, zarovnání,
parametry agendy, náhled dat, rozvržení sloupců). Každá featura nové agendy přistane sem a dva
lidé si v něm překážejí.

Návrh: rozsekat na `partial` soubory přesně podle těch pruhů. Mechanická práce beze změny chování.
Totéž `MainWindow.xaml.cs` (3394 řádků; `MainWindow.TableCells.cs` je precedens, že to jde).

### 4. Allowlist `ServerSideParams` je ruční a má duplicity

`TemplateDesignerViewModel.cs:3173`. `ServerSideParamsDriftTests` past spolehlivě drží (chytil
`DELENI_SLOUPCU`), takže to není akutní - ale autor nové agendy musí ten seznam najít uvnitř
nejdelšího souboru projektu.

Dvě konkrétní věci: `TVAR_NAZVU_ZASTAVKY` a `NAZEV_PRO_MHD` jsou v poli **dvakrát** (test na
duplicity nekouká), a čistší tvar je nechat to říct server. Parametr už nese `group` a
`visibleWhen`, takže `"serverSide": true` je aditivní změna kontraktu podle R3 a allowlist mizí.

### 5. Dialog sloupců se zakládá kopií

`StopColumnsEditorDialog`, `LineTableColumnsEditorDialog`, `DynamicTableEditorDialog`
a `DepartureGroupEditorDialog` mají každý svých **8 barevných příkazů** (Pick/Clear × záhlaví/data
× text/pozadí) plus `RequestColorPicker`, opsané. Mezi dvěma z nich je 110 z 333 řádků identických,
další shodné bloky jsou v code-behindech.

Vyžaduje to i `mvvm-patterns.md` („Host dialog musí poskytnout 8 commands"), takže je to
zabudovaná daň za každý další dialog.

Návrh: příkazy patří do `CellStyleVM` (už drží hodnoty), hostitel dodá jen picker. Pravidlo
v `mvvm-patterns.md` se pak přepíše.

---

## Chyby a podezřelé věci

### 6. Náhled na ukázkových datech ignoruje jazyk šablony

`CacheAgendaProvider.MockCacheKey` i `MockDataInput` znají jen kód agendy a definici sloupců,
ale `ZastavkoveJrAd.GetMockData` volá `NactiJazyk()` a čte `^||ML("IN","JAZYK")`.

Projev: přepnutí šablony na slovenštinu **bez vybrané linky** neudělá nic. `CurrentWireSnapshot`
se změní, `RefetchIfColumnsDefinitionChanged` spustí prefetch a ten se okamžitě vrátí ze staré
cache, protože klíč je stejný. S vybranou linkou to funguje, takže to vypadá jako náhodná
nespolehlivost.

Oprava: poslat `JAZYK` do `MockDataInput` a přidat ho do klíče cache.
⚠️ `TURNUSY_KE_SPOJUM` do mock dat **nepatří** - `LinkovyJrAd.GetMockData` to má zdokumentované
jako záměr (mock je ukázka bez parametrů tisku).

### 7. Výška řádku minut se počítá z jiného písma, než se kreslí

`DepartureGridElement.cs:446` a `:682` berou výšku datového řádku z `DataStyle.FontSize ?? 8f`,
zatímco číslice kreslí `ResolveGroupMinuteStyle`, kam se promítá velikost písma nastavená
**u jedné skupiny dnů** (`DepartureGroupColumnStyle.Style.Data`; dialog skupin má
`DataEditOptions = AllEnabled`, takže je to pole v UI). Větší písmo v jedné skupině tedy zvětší
číslice, ale ne řádek.

Je to přesně anti-vzor „asymetrie výška ↔ text" z `mvvm-patterns.md`. U **záhlaví** téhož prvku
je opravený (maximum přes skupiny), datová strana zůstala.

Oprava: výška řádku z maxima `ResolveGroupMinuteStyle(...).FontSize` přes viditelné skupiny,
shodně v `Render` i `CalculateHeightFromData`.

### 8. Panel vlastností má barvy natvrdo

`PropertiesPanel.xaml.cs` staví řádky kódem s pevnými barvami: popisek `#444444` (`:589`),
nadpis skupiny `#2196F3` (`:116`), jednotka `#888888` (`:356`), prázdný stav `#999999` (`:100`),
chybový rámeček `#CC0000` (`:546`). K tomu `FontSize = 11` natvrdo.

XAML je jinak vzorný (nula pevných barev, nula pevných velikostí písma v celém `dotnet/`), takže
je to jediné takové místo. `EDAppearance.Initialize` se volá (`App.xaml.cs:48`), motiv se tedy
přepíná za běhu a v tmavém režimu popisky splynou s pozadím. Pevná velikost navíc obchází
uživatelské škálování písma.

Oprava: `EDTheme.*` a `EDTheme.FontSize.*` přes `DynamicResource` (`TryFindResource` v code-behindu).

### 9. Jméno prvku pro uživatele má čtyři zdroje

`ElementNames.ForUser` je deklarovaný jako jediný zdroj a `ElementNamesTests` ho hlídá proti
paletě. Totéž rozhodování ale dělají ještě `TemplateElement.GetSummary:434`,
`AiVisionImportPipeline.ElementNazev:316` a `CapabilityCatalog.FriendlyName:322`.

`CapabilityCatalog` jde do AI asistenta a **je rozejitý**: říká „Legenda poznámek" a „Tabulka
linky", tedy jména přejmenovaná 1. 9. na „Poznámky" a „Linkový JŘ". Nápověda k tomu přidává pátou
variantu u tabulek („Dynamická tabulka" / „Pevná tabulka" proti „Tabulka (z dat)" /
„Tabulka (pevná)" v paletě).

Oprava: tři kopie nahradit voláním `ElementNames.ForUser`; kde je potřeba dovětek („(zastávkový
JŘ)"), skládat ho k němu. `ElementNamesTests` rozšířit o `CapabilityCatalog` a nápovědu.

### 10. `dev.firmSwitch` zapnutý ve sledovaném configu

V pracovní kopii je nezacommitnutá změna `templatedesigner.appsettings.json`, která přidává
`"dev": { "firmSwitch": true }`. Ten přepínač odemyká přepínání datové firmy, které se zákazníkovi
ukázat nemá; `AppSettings.Merge:129` ho umí zapnout z gitignorovaného `.local.json`, kam patří.
Ve stejném diffu přibyl BOM na začátek `PostScriptParitaProbe.cs`.

---

## Duplicity a drobnosti

### 11. `TableElement` a `DynamicTableElement` sdílí opsanou kaskádu

39 shodných devítiřádkových bloků, mezi nimi celé `TableHeaderParent` / `TableDataParent` /
`TableSummaryParent` / `ResolveColumn*` včetně komentářů a **dvě veřejné konstanty**
`NoveZahlaviPozadi` se stejnou hodnotou.

⚠️ Nesjednocovat naslepo - `elementy-architektura.md` říká, co se vědomě nesdílí. Tohle ale není
render, je to výchozí kaskáda, a ta je opravdu táž.

Související, ne bug: `TableElement.Mode` má výchozí hodnotu `Dynamic`, ačkoli `TableMode.Dynamic`
je jen kvůli starým šablonám. Dnes to nevadí (`CreateGrid` nastaví `Fixed`), měnit to kvůli
deserializaci starých šablon nemá cenu.

### 12. Drobnosti

- Šipky ▲▼ ve sdíleném `ColumnListEditor.xaml:46,48` nemají `ToolTip` (`ux-conventions §2`).
  Jsou ve všech třech dialozích sloupců.
- Dvě varování v buildu: `CacheTemplatePickerDialogViewModel.DeleteCommand` a
  `HighlightRulesEditorDialogViewModel.AddCommand` zakrývají členy `EDViewModel` (CS0108).
  Funguje to, chce to `new` nebo jiné jméno.
- Na 21 místech jsou dva `<summary>` bloky za sebou, takže jeden popis je mrtvý. Nejvíc
  v `LineTableElement` (4×) a `TemplateDesignerViewModel` (5×).

### 13. Zastaralá tvrzení v `.claude/rules/`

Načítá se to v každé session, takže stará tvrzení stojí kontext a matou.

- `mvvm-patterns.md` §TableElement popisuje Fixed a Dynamic jako dva uživatelské režimy jednoho
  prvku. Kód říká, že `Dynamic` je jen kvůli starým šablonám a dynamická tabulka je vlastní prvek.
- Tamtéž §Pattern 2 vyjmenovává dvouzónové prvky bez `DynamicTableElement` a `LineTableElement`.
- `TemplateDesigner/CLAUDE.md` mluví o „závazných pravidlech R1-R5", `architecture-rules.md`
  jich má šest (R6 = paritu doloží porovnání s legacy, ne náš odtisk).
