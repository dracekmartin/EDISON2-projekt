using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// `B145` - pozadí těla sloupce hodin, když se opakuje u každé skupiny dnů. Vzor je vývěska
/// Nitra linka 4: záhlaví i pruh hodin plnou barvou skupiny, minuty pod tím bílé a střídání řádků
/// světlejším odstínem. Záhlaví hodinové buňky barvu skupiny přebíralo už dřív, tělo ne.
/// </summary>
public class DepartureHourColumnTests
{
    private static ResolvedTextStyle Styl(string? pozadi) =>
        ResolvedTextStyle.Default with { BackgroundColor = pozadi ?? "" };

    /// <summary>Podbarvené záhlaví skupiny obarví i tělo hodin - jinak by plný pruh hodin
    /// šel jen za cenu obarvení minut.</summary>
    [Fact]
    public void PodbarveneZahlavi_ObarviITeloHodin()
    {
        var vysledek = DepartureGridElement.HourBodyBackground(Styl("#FF9DC3E6"), Styl("#00FFFFFF"));

        Assert.Equal("#FF9DC3E6", vysledek);
    }

    /// <summary>Bez podbarveného záhlaví se nic nemění - tělo hodin jde po pozadí minut jako dosud.
    /// Průhledná buňka nechá projít zebru, takže šablony bez barev vypadají stejně.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("#00FFFFFF")]
    public void NepodbarveneZahlavi_TeloHodinJdePoMinutach(string zahlavi)
    {
        var vysledek = DepartureGridElement.HourBodyBackground(Styl(zahlavi), Styl("#FFEEEEEE"));

        Assert.Equal("#FFEEEEEE", vysledek);
    }

    /// <summary>Poloprůhledné záhlaví je pořád barva - rozhoduje alfa, ne prázdnost řetězce.</summary>
    [Fact]
    public void PoloprusvitneZahlavi_SePovazujeZaBarvu()
    {
        var vysledek = DepartureGridElement.HourBodyBackground(Styl("#809DC3E6"), Styl("#FFEEEEEE"));

        Assert.Equal("#809DC3E6", vysledek);
    }
}
