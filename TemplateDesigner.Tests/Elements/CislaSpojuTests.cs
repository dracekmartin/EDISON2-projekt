using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models;
using TemplateDesigner.Serialization;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Volba „Čísla spojů" u tabulky linky (#9088: starý Win tisk ji nabízel, ale čísla neskryl).
/// </summary>
public class CislaSpojuTests
{
    private static LineTableElement.LineTripData Spoj() => new() { CisloSpoje = "25" };

    [Fact]
    public void Vychozi_CislaSeTisknou()
    {
        Assert.Equal("25", new LineTableElement().TextCislaSpoje(Spoj()));
    }

    [Fact]
    public void Vypnuto_NadSpojemZustanePrazdno()
    {
        Assert.Equal("", new LineTableElement { ShowTripNumbers = false }.TextCislaSpoje(Spoj()));
    }

    /// <summary>Šablona uložená před zavedením volby ji v JSON nemá - čísla se musí tisknout dál.</summary>
    [Fact]
    public void StaraSablonaBezVolby_CislaTiskne()
    {
        var sekce = new Section { Elements = [new LineTableElement { ShowTripNumbers = false }] };
        string json = TemplateSerializer.SerializeSection(sekce);
        Assert.Contains("showTripNumbers", json);
        json = System.Text.RegularExpressions.Regex.Replace(json, "\"showTripNumbers\"\\s*:\\s*false\\s*,", "");

        var nacteny = Assert.IsType<LineTableElement>(Assert.Single(TemplateSerializer.DeserializeSection(json)!.Elements));

        Assert.True(nacteny.ShowTripNumbers);
    }
}
