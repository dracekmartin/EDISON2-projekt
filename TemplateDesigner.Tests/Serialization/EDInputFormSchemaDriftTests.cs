using System.IO;
using System.Reflection;
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

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName,
            "TemplateDesigner", "CACHE", "ALVA", "TemplateDesigner", "EDInputForm");
    }

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
