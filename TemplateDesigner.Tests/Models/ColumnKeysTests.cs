using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// Zámek wire hodnot klíčů sloupců (<see cref="ColumnKeys"/>). Hodnoty se persistují do JSON šablony
/// (<c>StopColumn.Key</c>) — přejmenování tiše rozbije uložené šablony. Když někdo změní konstantu,
/// tento test spadne a donutí ho napsat migraci místo tiché koroze.
/// </summary>
public class ColumnKeysTests
{
    [Fact]
    public void WireHodnoty_JsouZamcene()
    {
        // NEMĚNIT bez migrace šablon — viz ColumnKeys docstring.
        Assert.Equal("StopName", ColumnKeys.StopName);
        Assert.Equal("Tc", ColumnKeys.Tc);
        Assert.Equal("Zone", ColumnKeys.Zone);
        Assert.Equal("Km", ColumnKeys.Km);
        Assert.Equal("Mhd", ColumnKeys.Mhd);
        Assert.Equal("Trip", ColumnKeys.Trip);
        Assert.Equal("TravelTime", ColumnKeys.TravelTime);
        Assert.Equal("RouteLetters", ColumnKeys.RouteLetters);
        Assert.Equal("BaseRoute", ColumnKeys.BaseRoute);
        Assert.Equal("TravelTime#", ColumnKeys.TravelTimeVariantPrefix);
    }

    [Fact]
    public void TripKey_JeAliasNaColumnKeys()
    {
        Assert.Equal(ColumnKeys.Trip, LineTableElement.TripKey);
    }

    [Fact]
    public void VychoziSloupce_StopList_PouzivajiJenZnameKlice()
    {
        string[] known = [ColumnKeys.TravelTime, ColumnKeys.Tc, ColumnKeys.StopName,
                           ColumnKeys.Zone, ColumnKeys.RouteLetters, ColumnKeys.BaseRoute];
        Assert.All(StopListElement.GetDefaultColumns(), c => Assert.Contains(c.Key, known));
    }

    [Fact]
    public void VychoziSloupce_LineTable_PouzivajiJenZnameKlice()
    {
        string[] known = [ColumnKeys.Zone, ColumnKeys.Km, ColumnKeys.Tc,
                          ColumnKeys.StopName, ColumnKeys.Mhd, ColumnKeys.Trip];
        Assert.All(LineTableElement.GetDefaultColumns(), c => Assert.Contains(c.Key, known));
    }
}
