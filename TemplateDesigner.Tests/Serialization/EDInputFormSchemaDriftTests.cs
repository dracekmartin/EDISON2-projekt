using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Driftová pojistka Wave 4 duplikace schémat: Caché <c>EDInputForm/*Edit.cls</c> třídy jsou
/// manuální mirror C# properties (field key = property path, čte/zapisuje ElementFormBinder
/// reflexí). Přejmenování property v C# bez úpravy .cls = runtime chyba u zákazníka.
///
/// Test parsuje .cls soubory přímo z repa (offline, bez Caché serveru), vytáhne field keys
/// a reflexí ověří, že každá property path na cílovém C# typu existuje. Bloky
/// <c>StyleFields.Add(form, "Prefix", …)</c> expanduje podle suffixů vyčtených ze
/// <c>StyleFields.cls</c> - při přidání pole stylu se test aktualizuje sám.
/// </summary>
public class EDInputFormSchemaDriftTests
{
    /// <summary>Mapa .cls souboru na cílový C# typ - mirror `EDInputFormSchema` switche v MainWindow
    /// + dialogy StopList/DepartureGrid (tab Obecné) + Section/Page dialogy.</summary>
    public static TheoryData<string, Type> SchemaMap => new()
    {
        { "TextElementEdit.cls",          typeof(TextElement) },
        { "LineElementEdit.cls",          typeof(LineElement) },
        { "RectangleElementEdit.cls",     typeof(RectangleElement) },
        { "BarcodeElementEdit.cls",       typeof(BarcodeElement) },
        { "ImageElementEdit.cls",         typeof(ImageElement) },
        { "NotesLegendElementEdit.cls",   typeof(NotesLegendElement) },
        { "StopListElementEdit.cls",      typeof(StopListElement) },
        { "DepartureGridElementEdit.cls", typeof(DepartureGridElement) },
        { "DynamicTableElementEdit.cls",  typeof(DynamicTableElement) },
        { "LineTableElementEdit.cls",     typeof(LineTableElement) },
        { "SectionEdit.cls",              typeof(Section) },
        { "PageEdit.cls",                 typeof(Template) },
    };

    /// <summary>Virtuální klíče, které záměrně nejsou property path (binder je řeší speciálně).</summary>
    private static readonly Dictionary<string, string[]> VirtualKeys = new()
    {
        // Format/Orientation jsou odvozené volby (A4/landscape…) přeložené na PageSize.Width/Height
        ["PageEdit.cls"] = ["Format", "Orientation"],
    };

    // Field key = 1. string argument Add* metod (AddGroup má jako 1. arg group id - vynechat)
    private static readonly Regex DirectKeyRx = new(
        @"form\.Add(?:Text|Bool|Number|Decimal|FieldToLastGroup)\(\s*""([^""]+)""",
        RegexOptions.Compiled);

    private static readonly Regex StyleFieldsCallRx = new(
        @"StyleFields\)\.Add\(form,\s*""([^""]+)""",
        RegexOptions.Compiled);

    private static readonly Regex StyleFieldsSuffixRx = new(
        @"prefix_""\.(\w+)""",
        RegexOptions.Compiled);

    // BaseElementFields = sdílený balíček base polí (rámeček/rohy, pozice, podmínky). Volání
    // v elementu se rozbalí na klíče té konkrétní metody, ať base pole zůstanou drift-testovaná
    // i po přesunu do balíčku.
    private static readonly Regex BaseFieldsCallRx = new(
        @"BaseElementFields\)\.(Add\w+)\(form",
        RegexOptions.Compiled);

    private static readonly Regex BaseFieldsMethodRx = new(
        @"ClassMethod (Add\w+)\([^)]*\)\s*\{(.*?)\n\}",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private const string BaseFieldsFile = "BaseElementFields.cls";

    [Theory]
    [MemberData(nameof(SchemaMap))]
    public void VsechnyFieldKeys_ExistujiJakoPropertyNaCilovemTypu(string clsFile, Type targetType)
    {
        var keys = ExtractKeys(clsFile);
        Assert.NotEmpty(keys);   // prázdný výsledek = regex zrezivěl, ne čistý soubor

        var skip = VirtualKeys.GetValueOrDefault(clsFile, []);
        var missing = keys
            .Where(k => !k.StartsWith("__"))          // defenzivně: __KEY = ne-property field
            .Where(k => !skip.Contains(k))
            // "style:<prefix>" = tlačítko pod-formuláře stylu (StyleEditDialog);
            // část za dvojtečkou je property path → validovat taky
            .Select(k => k.StartsWith("style:") ? k["style:".Length..] : k)
            .Where(k => !PropertyPathExists(targetType, k))
            .ToList();

        Assert.True(missing.Count == 0,
            $"{clsFile}: field keys bez odpovídající C# property na {targetType.Name}: " +
            $"{string.Join(", ", missing)}. Buď přejmenuj zpět, uprav .cls, nebo doplň VirtualKeys.");
    }

    [Fact]
    public void StyleFieldsSuffixy_SeNaparsovaly()
    {
        // Pojistka regexu - StyleFields.cls definuje balíček polí stylu (FontFamily, FontSize, …)
        var suffixes = StyleFieldsSuffixes();

        Assert.True(suffixes.Count >= 10, $"Načteno jen {suffixes.Count} suffixů - regex nesedí na StyleFields.cls?");
        Assert.Contains("FontSize", suffixes);
        Assert.Contains("TextColor", suffixes);
    }

    /// <summary>Pojistka pokrytí: každý EDInputForm `*Edit.cls` musí být buď v SchemaMap
    /// (drift-testovaný), nebo ve výjimkách (ne-elementové formy). Nový element se svým
    /// `*Edit.cls` se tak nemůže proklouznout bez drift testu.</summary>
    [Fact]
    public void VsechnyEditClsFormy_JsouPokryteNeboVyjimka()
    {
        // Ne-elementové formy - needitují property elementu/sekce/šablony:
        //   StyleEdit = pod-formulář stylu (jeho pole ověřuje StyleFields expanze v rodičích).
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "StyleEdit.cls" };

        var covered = SchemaMap.Select(row => (string)row[0]!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var uncovered = Directory.GetFiles(EDInputFormDir(), "*Edit.cls")
            .Select(Path.GetFileName)
            .Cast<string>()
            .Where(f => !covered.Contains(f) && !excluded.Contains(f))
            .ToList();

        Assert.True(uncovered.Count == 0,
            $"EDInputForm `*Edit.cls` bez drift testu: {string.Join(", ", uncovered)}. " +
            "Přidej do SchemaMap (element ↔ C# typ), nebo do `excluded` (ne-elementová forma).");
    }

    // ── Base pole: jedno místo na každé straně ───────────────────────────────
    // Base sada (rámeček/rohy · pozice a velikost · podmínky) byla do 29. 7. opsaná ručně v každém
    // z deseti formulářů → rozešly se rozsahy (tloušťka rámečku 0-10 × 0-5 mm), dynamické tabulce
    // pole rámečku chybělo úplně a u tří elementů byl rámeček v cizí skupině. Tyhle dva testy drží
    // stav „jedna definice na straně Caché + jedna v C#, a obě říkají totéž".

    [Fact]
    public void ZadnyElementovyFormular_NeopisujeBasePoleRucne()
    {
        var baseKeys = BaseFieldsKeysPerMethod().Values.SelectMany(k => k).ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(baseKeys);   // prázdno = regex zrezivěl, ne že je vše v pořádku

        var hits = new List<string>();
        foreach (var row in SchemaMap)
        {
            var file = (string)row[0]!;
            if (!typeof(TemplateElement).IsAssignableFrom((Type)row[1]!)) continue;

            string text = File.ReadAllText(Path.Combine(EDInputFormDir(), file));
            foreach (var key in DirectKeyRx.Matches(text).Select(m => m.Groups[1].Value).Distinct())
                if (baseKeys.Contains(key)) hits.Add($"{file}: \"{key}\"");
        }

        Assert.True(hits.Count == 0,
            "Base pole se v elementovém formuláři definují ručně - musí jít přes sdílený "
            + $"{BaseFieldsFile} (jinak se rozsahy a skupiny znovu rozejdou):\n{string.Join("\n", hits)}");
    }

    [Fact]
    public void BasePole_MajiVCsStejnyPopisekISkupinu()
    {
        var expected = BaseFieldsLabelToGroup();
        Assert.NotEmpty(expected);

        // Obdélník = element s rohy i pevnou výškou → pokrývá celou base sadu.
        var descriptors = new RectangleElement().GetPropertyDescriptors()
            .Where(d => !string.IsNullOrEmpty(d.Label))
            .ToDictionary(d => d.Label!, d => d.Group ?? "", StringComparer.Ordinal);

        var problems = expected
            .Where(kv => !descriptors.TryGetValue(kv.Key, out var group) || group != kv.Value)
            .Select(kv => descriptors.TryGetValue(kv.Key, out var g)
                ? $"\"{kv.Key}\": Caché skupina \"{kv.Value}\" × C# \"{g}\""
                : $"\"{kv.Key}\": v C# deskriptorech chybí (Caché skupina \"{kv.Value}\")")
            .ToList();

        Assert.True(problems.Count == 0,
            $"Base pole se rozešla mezi {BaseFieldsFile} a C# GetPositionDescriptors "
            + $"(dvě editační cesty téže sady):\n{string.Join("\n", problems)}");
    }

    [Fact]
    public void PoleMrizky_MajiVCsStejnyPopisekISkupinu()
    {
        // `AddGrid` je volitelný balíček (tabulkové prvky), takže se neověřuje na obdélníku jako
        // zbytek base sady, ale na prvku, který mřížku má.
        var expected = BaseFieldsLabelToGroup("AddGrid");
        Assert.NotEmpty(expected);

        var descriptors = new StopListElement().GetPropertyDescriptors()
            .Where(d => !string.IsNullOrEmpty(d.Label))
            .ToDictionary(d => d.Label!, d => d.Group ?? "", StringComparer.Ordinal);

        var problems = expected
            .Where(kv => !descriptors.TryGetValue(kv.Key, out var group) || group != kv.Value)
            .Select(kv => descriptors.TryGetValue(kv.Key, out var g)
                ? $"\"{kv.Key}\": Caché skupina \"{kv.Value}\" × C# \"{g}\""
                : $"\"{kv.Key}\": v C# deskriptorech chybí (Caché skupina \"{kv.Value}\")")
            .ToList();

        Assert.True(problems.Count == 0,
            $"Pole mřížky se rozešla mezi {BaseFieldsFile} a C# GetGridDescriptors:\n{string.Join("\n", problems)}");
    }

    // ── Názvoslovný drift (labely + volby) ───────────────────────────────────
    // Zamyká kanon z ux-conventions §8: tentýž field key musí mít všude tentýž label
    // a zakázané tvary termínů se nesmí objevit v labelech, volbách ani panelových deskriptorech.

    /// <summary>Zakázané tvary (kanon §8 + rozhodnutí Jan 2026-07-28). Exact = celý label.</summary>
    private static readonly string[] BannedExactLabels =
    [
        "Zamčeno",          // → Uzamčeno
        "Viditelnost",      // → Podmínka viditelnosti (samostatná „Viditelnost řádku" je jiný pojem a je OK)
        "Tučně",            // → Tučné
        "Podtržené",        // → Podtržení
        "Přeškrtnuté",      // → Přeškrtnutí
        "Horizontálně",     // → Vodorovně
        "Vertikálně",       // → Svisle
        "Nahoru",           // → Nahoře (zarovnání je stav, ne pohyb)
        "Dolů",             // → Dole
        "Pokročilé",        // skupina → Podmínky
    ];

    /// <summary>Zakázané podřetězce v labelech/volbách (case-sensitive).</summary>
    private static readonly string[] BannedLabelSubstrings =
    [
        "Šířka rámečku",    // → Tloušťka rámečku (Šířka patří elementu)
        "Zebra",            // → Střídání řádků
        "Tl. ",             // → Tloušťka …
        "Řádek NZ",         // → Řádek negativních značek
        "Font aktuální",    // → Velikost aktuální (je to velikost písma, ne rodina)
        " per ",            // UI texty česky (kanon §8)
        "(vlak/bus)",       // → (vlak/autobus)
        "nástupiště",       // → označník
        "Nástupiště",       // → Označník
        // „stanoviště" už zakázané NENÍ (2026-08-03, #13088): označník a sloupek se ukázaly být dvě
        // různá pole a „stanoviště" je zastřešující pojem pro to z nich, které je zvolené parametrem
        // „Číslo stanoviště podle". Jako synonymum pro označník se dál nepoužívá - viz ux-conventions §8.
        "—",           // em dash → běžná pomlčka "-" (rozhodnutí Jan 2026-07-28)
    ];

    private static readonly Regex LabeledFieldRx = new(
        @"form\.Add(?:Text|Bool|Number|Decimal|FieldToLastGroup)\(\s*""([^""]+)"",\s*""([^""]+)""",
        RegexOptions.Compiled);

    private static readonly Regex ListItemRx = new(
        @"AddListItem\(\s*""[^""]*"",\s*""([^""]+)""",
        RegexOptions.Compiled);

    private static readonly Regex GroupLabelRx = new(
        @"AddGroup\(\s*""[^""]*"",\s*""([^""]+)""",
        RegexOptions.Compiled);

    private static IEnumerable<string> AllEditClsFiles() =>
        Directory.GetFiles(EDInputFormDir(), "*.cls").Select(Path.GetFileName).Cast<string>();

    [Fact]
    public void StejnyFieldKey_MaVsudeStejnyLabel()
    {
        // Legitimní rozdíly labelu téhož klíče mezi formuláři (kontextové upřesnění) - vědomé výjimky:
        //   StrokeWidth - u čáry je to „Tloušťka" (čáry samotné), u obdélníku „Tloušťka okraje"
        // („Height" tu byl do 29. 7. kvůli rozdílnému popisku výšky - sdílený BaseElementFields
        //  ho sjednotil na „Výška" + nápovědu, takže výjimka padla.)
        var exempt = new HashSet<string>(StringComparer.Ordinal) { "StrokeWidth" };

        var labelsPerKey = new Dictionary<string, Dictionary<string, List<string>>>();
        foreach (var file in AllEditClsFiles())
        {
            string text = File.ReadAllText(Path.Combine(EDInputFormDir(), file));
            foreach (Match m in LabeledFieldRx.Matches(text))
            {
                var key = m.Groups[1].Value;
                if (key.StartsWith("__") || exempt.Contains(key)) continue;
                var perLabel = labelsPerKey.TryGetValue(key, out var d) ? d : labelsPerKey[key] = new();
                (perLabel.TryGetValue(m.Groups[2].Value, out var files) ? files : perLabel[m.Groups[2].Value] = new()).Add(file);
            }
        }

        var drift = labelsPerKey
            .Where(kv => kv.Value.Count > 1)
            .Select(kv => $"{kv.Key}: " + string.Join(" × ", kv.Value.Select(l => $"\"{l.Key}\" ({string.Join(",", l.Value.Distinct())})")))
            .ToList();

        Assert.True(drift.Count == 0,
            "Tentýž field key má v různých .cls různý label - sjednoť (kanon ux-conventions §8):\n" +
            string.Join("\n", drift));
    }

    [Fact]
    public void ZadneZakazaneTerminy_VClsLabelechAVolbach()
    {
        var hits = new List<string>();
        foreach (var file in AllEditClsFiles())
        {
            string text = File.ReadAllText(Path.Combine(EDInputFormDir(), file));
            var labels = LabeledFieldRx.Matches(text).Select(m => m.Groups[2].Value)
                .Concat(ListItemRx.Matches(text).Select(m => m.Groups[1].Value))
                .Concat(GroupLabelRx.Matches(text).Select(m => m.Groups[1].Value));
            foreach (var label in labels)
            {
                if (BannedExactLabels.Contains(label)) hits.Add($"{file}: \"{label}\"");
                else foreach (var sub in BannedLabelSubstrings)
                    if (label.Contains(sub, StringComparison.Ordinal)) hits.Add($"{file}: \"{label}\" (obsahuje \"{sub}\")");
            }
        }
        Assert.True(hits.Count == 0,
            "Zakázané termíny v .cls labelech/volbách (kanon ux-conventions §8):\n" + string.Join("\n", hits.Distinct()));
    }

    [Fact]
    public void ZadneZakazaneTerminy_VPanelovychDescriptorech()
    {
        var hits = new List<string>();
        foreach (var row in SchemaMap)
        {
            var type = (Type)row[1]!;
            if (!typeof(TemplateElement).IsAssignableFrom(type)) continue;
            var element = (TemplateElement)Activator.CreateInstance(type)!;
            foreach (var d in element.GetPropertyDescriptors())
            {
                var texts = new List<string?> { d.Label, d.Group };
                if (d is EnumPropertyDescriptor e) texts.AddRange(e.Options.Select(o => o.Label));
                foreach (var t in texts.Where(t => !string.IsNullOrEmpty(t)))
                {
                    if (BannedExactLabels.Contains(t)) hits.Add($"{type.Name}: \"{t}\"");
                    else foreach (var sub in BannedLabelSubstrings)
                        if (t!.Contains(sub, StringComparison.Ordinal)) hits.Add($"{type.Name}: \"{t}\" (obsahuje \"{sub}\")");
                }
            }
        }
        Assert.True(hits.Count == 0,
            "Zakázané termíny v panelových deskriptorech (kanon ux-conventions §8):\n" + string.Join("\n", hits.Distinct()));
    }

    // ── Parsování .cls ────────────────────────────────────────────────────────

    private static List<string> ExtractKeys(string clsFile)
    {
        string text = File.ReadAllText(Path.Combine(EDInputFormDir(), clsFile));
        var keys = DirectKeyRx.Matches(text).Select(m => m.Groups[1].Value).ToList();

        // StyleFields.Add(form, "HeaderStyle", …) → HeaderStyle.FontFamily, HeaderStyle.FontSize, …
        var suffixes = StyleFieldsSuffixes();
        foreach (Match m in StyleFieldsCallRx.Matches(text))
            keys.AddRange(suffixes.Select(s => $"{m.Groups[1].Value}.{s}"));

        // BaseElementFields.AddPosition(form, …) → X, Y, Width, Height, Rotation, ZIndex, IsLocked
        var baseKeys = BaseFieldsKeysPerMethod();
        foreach (Match m in BaseFieldsCallRx.Matches(text))
            keys.AddRange(baseKeys.GetValueOrDefault(m.Groups[1].Value, []));

        return keys;
    }

    /// <summary>Klíče polí, které přidává každá metoda sdíleného balíčku (AddBorder → BorderColor…).</summary>
    private static Dictionary<string, List<string>> BaseFieldsKeysPerMethod()
    {
        string text = File.ReadAllText(Path.Combine(EDInputFormDir(), BaseFieldsFile));
        return BaseFieldsMethodRx.Matches(text).ToDictionary(
            m => m.Groups[1].Value,
            m => DirectKeyRx.Matches(m.Groups[2].Value).Select(k => k.Groups[1].Value).Distinct().ToList());
    }

    /// <summary>Popisky base polí ze sdíleného balíčku, mapované na skupinu, do které patří
    /// (skupinu určuje AddGroup na začátku metody).</summary>
    /// <param name="onlyMethod">Null = celý balíček; jinak jen jedna metoda (balíček `AddGrid`
    /// je volitelný - má ho tabulkový prvek, ne každý element).</param>
    private static Dictionary<string, string> BaseFieldsLabelToGroup(string? onlyMethod = null)
    {
        string text = File.ReadAllText(Path.Combine(EDInputFormDir(), BaseFieldsFile));
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in BaseFieldsMethodRx.Matches(text))
        {
            if (onlyMethod is not null && m.Groups[1].Value != onlyMethod) continue;
            if (onlyMethod is null && m.Groups[1].Value == "AddGrid") continue;
            var body = m.Groups[2].Value;
            var group = GroupLabelRx.Match(body) is { Success: true } g ? g.Groups[1].Value : "";
            foreach (Match f in LabeledFieldRx.Matches(body))
                result[f.Groups[2].Value] = group;
        }
        return result;
    }

    private static List<string> StyleFieldsSuffixes()
    {
        string text = File.ReadAllText(Path.Combine(EDInputFormDir(), "StyleFields.cls"));
        return StyleFieldsSuffixRx.Matches(text).Select(m => m.Groups[1].Value).Distinct().ToList();
    }

    private static string EDInputFormDir()
    {
        // Od bin/Debug/... nahoru k rootu repa (kde leží EDISON2.sln)
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EDISON2.sln")))
            dir = dir.Parent;

        // Běh z izolovaného výstupu (dotnet test -o mimo repo) → root odvodit z cesty tohoto zdrojáku
        dir ??= new FileInfo(ThisFile()).Directory!.Parent!.Parent;

        Assert.True(File.Exists(Path.Combine(dir!.FullName, "EDISON2.sln")),
            "Nenalezen root repa (EDISON2.sln) - drift testy potřebují zdrojáky .cls");
        return Path.Combine(dir.FullName,
            "TemplateDesigner", "CACHE", "ALVA", "TemplateDesigner", "EDInputForm");
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;

    // ── Reflexe property path ─────────────────────────────────────────────────

    private static bool PropertyPathExists(Type type, string path)
    {
        var current = type;
        foreach (var segment in path.Split('.'))
        {
            var prop = current.GetProperty(segment, BindingFlags.Public | BindingFlags.Instance);
            if (prop is null) return false;
            current = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
        }
        return true;
    }
}
