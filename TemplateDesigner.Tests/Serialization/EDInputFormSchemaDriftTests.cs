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
/// <c>StyleFields.cls</c> — při přidání pole stylu se test aktualizuje sám.
/// </summary>
public class EDInputFormSchemaDriftTests
{
    /// <summary>Mapa .cls souboru na cílový C# typ — mirror `EDInputFormSchema` switche v MainWindow
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

    // Field key = 1. string argument Add* metod (AddGroup má jako 1. arg group id — vynechat)
    private static readonly Regex DirectKeyRx = new(
        @"form\.Add(?:Text|Bool|Number|Decimal|FieldToLastGroup)\(\s*""([^""]+)""",
        RegexOptions.Compiled);

    private static readonly Regex StyleFieldsCallRx = new(
        @"StyleFields\)\.Add\(form,\s*""([^""]+)""",
        RegexOptions.Compiled);

    private static readonly Regex StyleFieldsSuffixRx = new(
        @"prefix_""\.(\w+)""",
        RegexOptions.Compiled);

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
        // Pojistka regexu — StyleFields.cls definuje balíček polí stylu (FontFamily, FontSize, …)
        var suffixes = StyleFieldsSuffixes();

        Assert.True(suffixes.Count >= 10, $"Načteno jen {suffixes.Count} suffixů — regex nesedí na StyleFields.cls?");
        Assert.Contains("FontSize", suffixes);
        Assert.Contains("TextColor", suffixes);
    }

    /// <summary>Pojistka pokrytí: každý EDInputForm `*Edit.cls` musí být buď v SchemaMap
    /// (drift-testovaný), nebo ve výjimkách (ne-elementové formy). Nový element se svým
    /// `*Edit.cls` se tak nemůže proklouznout bez drift testu.</summary>
    [Fact]
    public void VsechnyEditClsFormy_JsouPokryteNeboVyjimka()
    {
        // Ne-elementové formy — needitují property elementu/sekce/šablony:
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
        "stanoviště",       // → označník (výjimka legacy legendy je mimo EDInputForm)
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
        // Legitimní rozdíly labelu téhož klíče mezi formuláři (kontextové upřesnění) — vědomé výjimky:
        //   Height     — DynamicTable má nápovědu „(prázdné = auto)" (dynamická výška), statické elementy ne
        //   StrokeWidth — u čáry je to „Tloušťka" (čáry samotné), u obdélníku „Tloušťka okraje"
        var exempt = new HashSet<string>(StringComparer.Ordinal) { "Height", "StrokeWidth" };

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
            "Tentýž field key má v různých .cls různý label — sjednoť (kanon ux-conventions §8):\n" +
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

        return keys;
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
            "Nenalezen root repa (EDISON2.sln) — drift testy potřebují zdrojáky .cls");
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
