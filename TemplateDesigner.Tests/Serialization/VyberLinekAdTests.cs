using System.IO;
using System.Text.RegularExpressions;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Okno výběru linek se má ve všech agendách nad daty AD chovat stejně (Jan 30. 10., <c>B56</c> S6). Do 30. 10.
/// měla vývěska AD i linkový JŘ AD vlastní kopii výběru skupin linek a linek a kopie MHD se do 23. 10. rozešla
/// v příznaku zablokované skupiny. Agenda nad AD proto výběr jen přesměruje do <c>Jr/DataAd.cls</c> - i budoucí
/// relace nebo vozový JŘ.
/// </summary>
public class VyberLinekAdTests
{
    private static readonly Regex VariantaAd = new(@"^Parameter Variant\b[^=]*=\s*""AD", RegexOptions.Multiline);

    [Theory]
    [InlineData("GetAvailableSkupiny")]
    [InlineData("GetAvailableInstances")]
    public void AgendaNadAd_BereVyberLinekZDataAd(string metoda)
    {
        var agendyAd = ZdrojeAgend.Agendy().Where(f => VariantaAd.IsMatch(File.ReadAllText(f))).ToList();
        var presmerovani = new Regex(
            $@"^ClassMethod {metoda}\(ByRef sys As %String\) As %Status\s*\{{\s*q ##class\(ALVA\.TemplateDesigner\.Agendy\.Jr\.DataAd\)\.{metoda}\(\.sys\)\s*\}}",
            RegexOptions.Multiline);

        var jinak = agendyAd.Where(f => !presmerovani.IsMatch(File.ReadAllText(f).Replace("\r\n", "\n")))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(agendyAd.Count >= 3, $"Našlo se jen {agendyAd.Count} agend nad AD - změnil se zápis parametru Variant?");
        Assert.True(jinak.Count == 0,
            $"Agenda nad daty AD má vlastní {metoda}, výběr linek by se choval jinak než ve zbytku AD. " +
            "Přesměruj ho do Jr.DataAd:\n" + string.Join("\n", jinak));
    }
}
