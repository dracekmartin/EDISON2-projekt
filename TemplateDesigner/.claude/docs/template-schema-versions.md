# Template schema verze - convention

Tento dokument popisuje verzování Template JSON formátu a jak přidat migraci při breaking change. Detail implementace v `dotnet/Serialization/TemplateMigrator.cs`.

## Princip

Template JSON má top-level `version` field (semver string, např. `"1.0.0"`). Při loadu `TemplateMigrator` detekuje verzi a aplikuje pipeline migrací do aktuální verze.

```json
{
  "version": "1.0.0",
  "id": "...",
  "name": "...",
  "sections": [...]
}
```

**Aktuální verze:** definovaná v `TemplateMigrator.CurrentVersion`.

## Pravidla pro vývojáře

### Kdy bumpnout verzi?

**Bumpni verzi POUZE pro breaking change** - tj. change ve schématu JSON, který by způsobil:
- ❌ Deserializace staré šablony do nového modelu selže (exception)
- ❌ Deserializace projde, ale data jsou semanticky špatná (silent corruption)

**Příklady breaking changes:**
- Rename pole (`textColor` → `foregroundColor`)
- Change typu pole (`fontSize: int` → `fontSize: float`)
- Restructure (flat fields → nested object, např. `TextStyle` flat → nested)
- Remove pole které je required
- Change sémantiky (např. souřadnice mm → palce)

**Příklady NON-breaking changes (NE bumpovat):**
- Add nové optional pole s default hodnotou (System.Text.Json default behavior)
- Add nový element type (`JsonDerivedType` rozhoduje, neexistující typ jen failuje to který element)
- Remove pole které je unused (silently ignored)

### Jak verzi bumpnout

1. **Update `TemplateMigrator.CurrentVersion`** - bumpni minor verzi (např. `"1.0.0"` → `"1.1.0"`)
2. **Vytvoř `ITemplateMigration` implementaci** v `Serialization/Migrations/`:
   ```csharp
   public class MigrationFrom1_0_0To1_1_0 : ITemplateMigration
   {
       public string FromVersion => "1.0.0";
       public string Description => "Rename textColor → foregroundColor in TextElement";

       public JsonNode Migrate(JsonNode root)
       {
           // Transformace - iteruj sections/elements, rename pole
           foreach (var section in root["sections"]?.AsArray() ?? [])
           foreach (var element in section?["elements"]?.AsArray() ?? [])
               if (element?["type"]?.GetValue<string>() == "TextElement"
                   && element["textColor"] is { } oldColor)
               {
                   element["foregroundColor"] = oldColor.DeepClone();
                   element.AsObject().Remove("textColor");
               }
           return root;
       }
   }
   ```
3. **Register v `TemplateMigrator._migrations`** list (sorted by FromVersion):
   ```csharp
   private readonly List<ITemplateMigration> _migrations = [
       new MigrationFrom1_0_0To1_1_0(),
   ];
   ```
4. **Aktualizuj sekci [Verze](#verze) níže** s popisem co se změnilo
5. **Unit test** - old-schema JSON → migrator → new-schema validní deserialize

## Verzování semver

- **Major** (1.x.x → 2.x.x): velká breaking change s rizikem dat ztratit. V budoucnu zatím nepoužíváme.
- **Minor** (x.1.x → x.2.x): běžná breaking change s migrací bez ztráty.
- **Patch** (x.x.1 → x.x.2): non-breaking (TD aktuálně neeviduje patch - bumpovat pouze pro breaking).

V praxi pro TD: bumpovat **minor** při každé breaking change.

## Migration safety rules

1. **Idempotentní** - opakovaná aplikace migrace = stejný výsledek. Migrátor může běžet vícekrát při ladění.
2. **Defensive** - chybějící pole, neočekávaná struktura → fallback na default, neházet exception.
3. **Per-migration scope** - jedna migrace = jedna change. Nedělej „big bang" migraci přes 5 verzí najednou.
4. **Backward read-only** - migrace pouze **čte** starou strukturu a píše novou. Žádné side effects (DB calls, file I/O).

## Co se stane bez migrátoru

Bez `TemplateMigrator` by breaking change v `Models/Template.cs` znamenala:
- Staré šablony (před changem) → deserializace failuje nebo dá silent corrupt data
- User otevře starou šablonu → crash nebo ztracené hodnoty
- **Nelze opravit retroaktivně** - staré JSON soubory uložené v Caché/disk nejdou load

S `TemplateMigrator` (this design): user otevře starou šablonu → migrator detekuje verzi → aplikuje transformaci → deserialize succeeds → user vidí migrovaný stav + status bar warning „uložením přepíšete starou verzi".

## Verze

### V1.0.0 (initial, 2026-05-25)

Baseline schema. Současný tvar Template.cs k 2026-05-25:
- `version` (string semver), `id` (Guid), `name`, `description`, `pageSize`, `sections` (s `elements`)
- Per-element flat properties + `style: TextStyle` nested object (po refaktoru z 03/2026)
- `parameters` array (TemplateParameter), `editorPreferences` object
- `agendaCode` (string nebo null pro GENERAL)

Žádné migrace zatím - framework je no-op.

#### ⚠️ Pre-baseline breaking change: flat text props → composable `style` (03/2026, NEMIGROVATELNÁ)

Refaktor `TextStyle` (03/2026) přesunul ploché text properties (`fontSize`, `textColor`, `fontWeight`…)
do kompozitního `style`/`headerStyle`/`dataStyle` objektu. Proběhl **před** baseline V1.0.0 a **bez
bumpu verze** → hodnota `"1.0.0"` je **dvojznačná**: může jít o pre-refactor plochou šablonu i o
současnou composable. Proto tato změna **není a nebude retroaktivně migrovatelná** - nešlo by
spolehlivě poznat, které schéma šablona má, a migrace by druhé poškodila.

**Přijaté riziko + mitigace:** DB je primární úložiště až od 2026-06-30 (dlouho po refaktoru), takže
**všechny šablony v produkční DB jsou composable**. Riziko se týká jen starých souborových `.json`
z doby před refaktorem - ty se do produkce nedostávají. Stará plochá šablona by se načetla „tiše
ochuzená o styling" (`System.Text.Json` neznámá pole ignoruje - stejný mechanismus, kvůli kterému
musí AI generátor produkovat přesné názvy properties).

**Důsledek pro budoucí migrace:** jakákoli migrace `1.0.0 → x` MUSÍ předpokládat **composable**
schéma (produkční realita). Ploché pre-refactor soubory nejsou podporovaný vstup.

#### Rozhodnutí 2026-07-07: NEbumpovat na „1.1.0" jako baseline

Zvažováno preventivní bumpnutí `CurrentVersion` na „1.1.0" pro „čistý produkční mezník". **Zamítnuto**
- jde proti pravidlu výše (*bump jen pro breaking change + migrace*): žádný breaking change nenastal,
a bez migrace by se navíc **každá existující „1.0.0" šablona při načtení označila jako migrovaná**
(WasMigrated=true → zbytečné varování). Baseline zůstává „1.0.0"; příští **skutečná** breaking změna
bumpne na „1.1.0" **spolu s** `ITemplateMigration`. (Kosmetický rozdíl „1.0" ve fallback defaultech
Caché metadat vs „1.0.0" v C# je funkčně irelevantní - `VersionComparer` je srovnává jako rovné a
metadata verze se k migraci nepoužívá; neřešeno záměrně.)

### V1.1.0–V1.6.0 (přehled; detail = XML doc migračních tříd v `Serialization/`)

- **1.1.0** `MigrationHighlightRules` - řádkové zvýraznění: skalární pár → `highlightRules[]` seznam.
- **1.2.0** `MigrationKnizniToLinkovy` - přejmenování knižní → linkový (agenda kódy/typy).
- **1.3.0** `MigrationUnifyStopColumns` - StopList klíče sloupců → sdílená field ID, zrušené sloupce pryč.
- **1.4.0** `MigrationDepartureGridMaxGroups` (2026-07-23) - DepartureGrid `maxGroupCount`: starý
  implicitní default **3 → 0** (bez limitu). Strop 3 tiše ořezával 4. SMO sloupec v rozporu
  s checkboxy dialogu; vědomé hodnoty (1, 2, 4…) zůstávají. Class default změněn na 0.
- **1.5.0** `MigrationRectangleCorners` - rohy sjednocené na base (`cornerRadius` → `cornerStyle`
  + `cornerSize`).
- **Bez bumpu (2026-07-31, `B41`):** z prvků odebrány klíče, které řídí parametr sestavy a prvek je
  tedy nikdy neuplatnil - `showDirectionArrow`, `showPassedStops` (seznam zastávek), `timeAxisMode`,
  `timeAxisRangeFrom`, `timeAxisRangeTo`, `autoExpandToFillHeight` (mřížka odjezdů). Uložené šablony
  je nesou dál, čtení je ignoruje; podle pravidla výše je odstranění nepoužívaného pole nezlomová
  změna, takže **bez migrace a bez bumpu**. Chování se nemění: hodnota se už dnes zahazovala,
  protože parametr sestavy má přednost.
- **1.6.0** `MigrationArrowWidth` (2026-07-31) - šipka směru: přepínač `directionArrowBold` zrušen
  ve prospěch tloušťky v mm (`directionArrowWidth`). Převod drží vzhled: tučná → 0,4 mm, jinak
  0,2 mm (dřívějších 1,5 / 0,8 **px**, což v tisku dávalo vláskovku). Dvě cesty k jedné vlastnosti
  byly zbytečné a přepínač neuměl konkrétní tloušťku, kterou zákazník chce.

---

## Operational notes

**UI signalizace migrace:**
- Status bar po loadu: „⚠ Šablona migrována na nový formát (Vx.y.z). Uložením přepíšete starou verzi."
- `IsDirty = true` po migraci - user musí explicitně uložit, aby se nová verze propsala do souboru/Caché

**Wire-up:**
- `TemplateSerializer.Deserialize(json)` - používá interně `TemplateMigrator`. Volá ji Caché path i file path.
- `TemplateSerializer.LoadAsync(filePath)` - wrapper kolem `Deserialize`.
- `CacheTemplateService.LoadAsync` - používá `TemplateSerializer.Deserialize` → automaticky routes through migrator.
