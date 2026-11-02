using System.IO;
using System.Text.RegularExpressions;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Test Caché, který nemá co ověřit (firma nebo linka na serveru není), se hlásí jako přeskočený
/// (<c>$$$AssertSkipped</c>), ne jako úspěch. <c>$$$AssertTrue(1, "… přeskočeno")</c> vypadá v souhrnu jako
/// ověřený test: do 30. 10. takhle hlásilo přeskočení 99 míst a tři testy na serveru léta neověřovaly nic,
/// takže si nikdo nevšiml, že jejich očekávání zastarala (<c>B56</c> S13). Informace k testu, který něco
/// ověřil, patří do <c>do ..LogMessage(…)</c>.
/// </summary>
public class PreskoceniTestuCacheTests
{
    private static readonly Regex UspechBezKontroly = new(@"\$\$\$AssertTrue\(\s*1\s*[,)]", RegexOptions.Compiled);

    [Fact]
    public void TestCacheNehlasiPreskoceniJakoUspech()
    {
        var soubory = SouboryCache();
        var nalezy = soubory
            .SelectMany(f => File.ReadAllLines(f).Select((r, i) => (Soubor: f, Radek: i + 1, Text: r)))
            .Where(r => !r.Text.TrimStart().StartsWith("//", StringComparison.Ordinal) && UspechBezKontroly.IsMatch(r.Text))
            .Select(r => $"{Path.GetRelativePath(ZdrojeAgend.Koren(), r.Soubor)}:{r.Radek}: {r.Text.Trim()}")
            .ToList();

        // Hledání musí projít skutečné třídy Caché, jinak by kontrola prošla naprázdno.
        Assert.True(soubory.Count > 100, $"našlo se jen {soubory.Count} tříd Caché");
        Assert.True(nalezy.Count == 0,
            "Test Caché hlásí přeskočení jako úspěch - použij $$$AssertSkipped(\"důvod\"), informaci zapiš přes ..LogMessage:\n"
            + string.Join("\n", nalezy));
    }

    /// <summary>Třídy Caché všech projektů repa (složky <c>CACHE</c> v kořenových projektech).</summary>
    private static List<string> SouboryCache() =>
        Directory.GetDirectories(ZdrojeAgend.Koren())
            .Select(d => Path.Combine(d, "CACHE"))
            .Where(Directory.Exists)
            .SelectMany(d => Directory.GetFiles(d, "*.cls", SearchOption.AllDirectories))
            .Order(StringComparer.Ordinal)
            .ToList();
}
