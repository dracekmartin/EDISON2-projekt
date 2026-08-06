using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// Zámek wire hodnot klíčů sloupců (<see cref="ColumnKeys"/> = linkový JŘ, <see cref="StopFields"/> =
/// zastávkový JŘ). Hodnoty se persistují do JSON šablony (<c>StopColumn.Key</c>) - přejmenování tiše
/// rozbije uložené šablony. Když někdo změní konstantu, tento test spadne a donutí ho napsat migraci
/// místo tiché koroze.
/// </summary>
public class ColumnKeysTests
{
    [Fact]
    public void WireHodnoty_LineTable_JsouZamcene()
    {
        // NEMĚNIT bez migrace šablon - viz ColumnKeys docstring.
        Assert.Equal("StopName", ColumnKeys.StopName);
        Assert.Equal("Tc", ColumnKeys.Tc);
        Assert.Equal("Zone", ColumnKeys.Zone);
        Assert.Equal("Km", ColumnKeys.Km);
        Assert.Equal("Mhd", ColumnKeys.Mhd);
        Assert.Equal("Trip", ColumnKeys.Trip);
    }

    [Fact]
    public void WireHodnoty_StopFields_JsouZamcene()
    {
        // StopList sloupce = field ID kolekce ZASTAVKY (1:1 s DynamicTable). NEMĚNIT bez migrace.
        Assert.Equal("NAZEV_ZASTAVKY", StopFields.StopName);
        Assert.Equal("CAS_JIZDY", StopFields.TravelTime);
        Assert.Equal("PORADI", StopFields.Poradi);
        Assert.Equal("AKTUALNI_ZASTAVKA", StopFields.JeAktualni);
        Assert.Equal("ZONY", StopFields.Zone);
        Assert.Equal("NASTUPISTE", StopFields.Nastupiste);
        Assert.Equal("SLOUPEK", StopFields.Sloupek);
        Assert.Equal("SLOUPEK_POPIS", StopFields.SloupekPopis);
        Assert.Equal("ID", StopFields.Id);
        Assert.Equal("CAS_JIZDY#", StopFields.TravelTimeVariantPrefix);
    }

    [Fact]
    public void TripKey_JeAliasNaColumnKeys()
    {
        Assert.Equal(ColumnKeys.Trip, LineTableElement.TripKey);
    }

    [Fact]
    public void VychoziSloupce_StopList_PouzivajiFieldId()
    {
        string[] known = [StopFields.TravelTime, StopFields.StopName, StopFields.Zone,
                          StopFields.Poradi, StopFields.JeAktualni, StopFields.Nastupiste,
                          StopFields.Sloupek];
        // SloupekPopis tu schválně NENÍ - do nabídky sloupců nepatří, dokud si ho někdo nevyžádá
        // (rozhodnutí Jan 2026-08-03). Kdyby ho někdo do výchozích sloupců vrátil, tento test spadne
        // a donutí ho to rozhodnutí zopakovat.
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
