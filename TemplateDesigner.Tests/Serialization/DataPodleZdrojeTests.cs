using System.IO;
using System.Text.RegularExpressions;

namespace TemplateDesigner.Tests.Serialization;

/// <summary>
/// Data, která agendy nad týmž zdrojem čtou stejně, jsou v pomocné třídě zdroje - <c>Jr/DataAd.cls</c>
/// a <c>Jr/DataMhd.cls</c> (<c>B56</c> S5 až S12, R8), ne v jedné z agend, kterou by ostatní volaly.
/// Do 30. 10. se agendy volaly navzájem (linkový JŘ MHD bral výběr linek, dopravce a typ JŘ z vývěsky MHD)
/// a kopie výběru linek se do 23. 10. rozešla v příznaku zablokované skupiny.
/// </summary>
public class DataPodleZdrojeTests
{
    private static readonly Regex Varianta = new(@"^Parameter Variant\b[^=]*=\s*""(?<varianta>[A-Z]+)", RegexOptions.Multiline);
    private static readonly Regex VolaniTridy = new(@"##class\(ALVA\.TemplateDesigner\.Agendy\.(?<trida>\w+)\)\.(?<metoda>\w+)", RegexOptions.Compiled);

    /// <summary>Agenda postavená nad jinou agendou, s důvodem.</summary>
    private static readonly Dictionary<string, string> PovolenaVolani = new(StringComparer.Ordinal)
    {
        ["LinkovyJrAdRozdilovy>LinkovyJrAd"] = "porovnání verzí skládá dvě stránky linkového JŘ AD a jen je slučuje",
    };

    /// <summary>
    /// Okno výběru linek se má chovat stejně ve všech agendách nad týmž zdrojem (Jan 30. 10.), i v budoucí
    /// relaci nebo vozovém JŘ. Agenda proto výběr jen přesměruje do pomocné třídy svého zdroje.
    /// </summary>
    [Theory]
    [InlineData("AD", "DataAd", "GetAvailableSkupiny", 3)]
    [InlineData("AD", "DataAd", "GetAvailableInstances", 3)]
    [InlineData("MHD", "DataMhd", "GetAvailableSkupiny", 2)]
    [InlineData("MHD", "DataMhd", "GetAvailableInstances", 2)]
    public void Agenda_BereVyberLinekZPomocneTridyZdroje(string zdroj, string trida, string metoda, int nejmeneAgend)
    {
        var agendy = ZdrojeAgend.Agendy()
            .Where(f => Varianta.Match(File.ReadAllText(f)) is { Success: true } m && m.Groups["varianta"].Value == zdroj)
            .ToList();
        var presmerovani = new Regex(
            $@"^ClassMethod {metoda}\(ByRef sys As %String\) As %Status\s*\{{\s*q ##class\(ALVA\.TemplateDesigner\.Agendy\.Jr\.{trida}\)\.{metoda}\(\.sys\)\s*\}}",
            RegexOptions.Multiline);

        var jinak = agendy.Where(f => !presmerovani.IsMatch(File.ReadAllText(f).Replace("\r\n", "\n")))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(agendy.Count >= nejmeneAgend, $"Našlo se jen {agendy.Count} agend nad {zdroj} - změnil se zápis parametru Variant?");
        Assert.True(jinak.Count == 0,
            $"Agenda nad daty {zdroj} má vlastní {metoda}, výběr linek by se choval jinak než v ostatních. " +
            $"Přesměruj ho do Jr.{trida}:\n" + string.Join("\n", jinak));
    }

    /// <summary>
    /// Základ agend je kontrakt a obecné věci pro všechny agendy, i budoucí z report generátoru (R8) - data
    /// AD ani MHD nečte. Do 30. 10. v něm bylo 14 metod jen pro jízdní řády, které je četly (<c>B56</c> S13).
    /// </summary>
    [Fact]
    public void ZakladAgendNectaData()
    {
        var zaklad = File.ReadAllLines(Path.Combine(ZdrojeAgend.Slozka, "BaseAgenda.cls"));
        var cteni = zaklad
            .Select((r, i) => (Radek: i + 1, Text: r))
            .Where(r => !r.Text.TrimStart().StartsWith("///", StringComparison.Ordinal) && r.Text.Contains("^[", StringComparison.Ordinal))
            .Select(r => $"BaseAgenda.cls:{r.Radek}: {r.Text.Trim()}")
            .ToList();

        Assert.True(cteni.Count == 0,
            "Základ agend čte data - patří do Jr.DataAd / Jr.DataMhd (R8):\n" + string.Join("\n", cteni));
    }

    /// <summary>
    /// Agenda nevolá jinou agendu: co potřebují obě, patří do pomocné třídy zdroje nebo druhu výstupu (R8).
    /// Kdyby agenda brala data z druhé, změna v jedné by tiše změnila výstup té druhé.
    /// </summary>
    [Fact]
    public void AgendaNevolaJinouAgendu()
    {
        var agendy = ZdrojeAgend.Agendy().Select(Path.GetFileNameWithoutExtension).ToHashSet(StringComparer.Ordinal);
        var nalezena = new List<string>();
        var povolenaNalezena = new HashSet<string>(StringComparer.Ordinal);

        foreach (var soubor in ZdrojeAgend.Agendy())
        {
            var volajici = Path.GetFileNameWithoutExtension(soubor);
            foreach (Match m in VolaniTridy.Matches(File.ReadAllText(soubor)))
            {
                var volana = m.Groups["trida"].Value;
                if (volana == volajici || volana == "BaseAgenda" || !agendy.Contains(volana)) continue;
                var dvojice = $"{volajici}>{volana}";
                if (PovolenaVolani.ContainsKey(dvojice)) { povolenaNalezena.Add(dvojice); continue; }
                nalezena.Add($"{volajici} volá {volana}.{m.Groups["metoda"].Value}");
            }
        }

        // Povolené volání se musí najít - jinak hledání nefunguje a test projde naprázdno.
        Assert.Equal(PovolenaVolani.Keys.OrderBy(k => k), povolenaNalezena.OrderBy(k => k));
        Assert.True(nalezena.Count == 0,
            "Agenda volá jinou agendu - společné čtení dat patří do Jr.DataAd / Jr.DataMhd, společný výstup do " +
            "Jr.VystupZastavkovy / Jr.VystupLinkovy (R8):\n" + string.Join("\n", nalezena.Distinct()));
    }
}
