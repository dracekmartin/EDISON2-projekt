using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Tlačítko „Upravit styl…" formuláře prvku (klíč <c>style:&lt;vlastnost&gt;</c>) otevírá okno stylu
/// v panelu vlastností i v okně nastavení (<see cref="ElementStyleButton"/>). Obsluha umí jen Minuty
/// odjezdů, takže tlačítko jinde nebo s vlastností, kterou prvek nemá, by tiše nic nedělalo - tak
/// v panelu do 23. 10. dopadla obě tlačítka Minut odjezdů.
/// </summary>
public class ElementStyleButtonTests
{
    private static readonly Regex StyleKey = new("\"style:(\\w+)\"", RegexOptions.Compiled);

    [Fact]
    public void TlacitkoStylu_JenVeFormulariMinutOdjezdu()
    {
        var formulare = Directory.GetFiles(EDInputFormDir(), "*.cls")
            .Where(f => StyleKey.IsMatch(File.ReadAllText(f)))
            .Select(Path.GetFileName);

        Assert.Equal(["DepartureGridElementEdit.cls"], formulare);
    }

    [Fact]
    public void KazdeTlacitkoStylu_MiriNaZonuMinutOdjezdu()
    {
        var vlastnosti = StyleKey.Matches(File.ReadAllText(Path.Combine(EDInputFormDir(), "DepartureGridElementEdit.cls")))
            .Select(m => m.Groups[1].Value).ToList();
        var prvek = new DepartureGridElement();

        Assert.NotEmpty(vlastnosti);
        Assert.All(vlastnosti, v =>
        {
            var typ = typeof(DepartureGridElement).GetProperty(v)?.PropertyType;
            Assert.True(typ == typeof(TextStyle), $"Minuty odjezdů nemají styl {v}");
            Assert.NotNull(prvek.ResolveStyleParent(v));
            Assert.Equal(v, ElementStyleButton.StyleProperty(ElementStyleButton.KeyPrefix + v));
        });
        Assert.Null(ElementStyleButton.StyleProperty("__highlightRules"));
    }

    private static string EDInputFormDir()
    {
        // Kořen repa od výstupní složky nahoru, jinak podle cesty k tomuhle souboru (test závisí jen na repu).
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EDISON2.sln"))) dir = dir.Parent;
        dir ??= new FileInfo(ThisFile()).Directory!.Parent!.Parent;
        return Path.Combine(dir.FullName, "TemplateDesigner", "CACHE", "ALVA", "TemplateDesigner", "EDInputForm");
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;
}
