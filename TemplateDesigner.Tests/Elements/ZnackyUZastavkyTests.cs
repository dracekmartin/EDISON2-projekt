using System.Text.Json.Nodes;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Tests.Rendering;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Značky u zastávky a spoje („jen nástup", paragraf) - `B165`, nález kolegy z 18. 9.
/// </summary>
/// <remarks>
/// Server je posílal jen do vysvětlivek, takže legenda popisovala značku, která v tabulce nikde
/// nebyla. Starý tisk je kreslí vlevo od času v buňce toho spoje (`JRapiLinPrtWin.mac:3838`).
/// </remarks>
public class ZnackyUZastavkyTests
{
    /// <summary>
    /// Značka v buňce si musí vzít místo, jinak přeteče přes hranu prvku.
    /// </summary>
    /// <remarks>Měří se při <b>zadané šířce sloupce</b>: sloupec, do kterého se obsah nevejde,
    /// zůstane širší, takže se rozdíl projeví právě na něm (od 22. 9.). Táž třída chyb jako
    /// u značek platnosti 8. 9. - kreslí se něco, na co se nepočítalo místo.</remarks>
    [Fact]
    public void ZnackaVBunceSiVezmeMistoVeSloupci()
    {
        var bez = SirkaSloupce(Data(znacky: null));
        var se = SirkaSloupce(Data(znacky: new JsonArray("N", "", "")));

        Assert.True(se > bez,
            $"se značkou {se:0.00} mm, bez ní {bez:0.00} mm - značka si místo nevzala");
    }

    /// <summary>Značka bez ověřeného piktogramu se zahazuje, nevypisuje se písmenem.</summary>
    /// <remarks>Stejně to filtruje starý tisk - co číselník nemá jako symbol, nekreslí.</remarks>
    [Fact]
    public void NeznamaZnackaSloupecNerozsiri()
    {
        var bez = SirkaSloupce(Data(znacky: null));
        var se = SirkaSloupce(Data(znacky: new JsonArray("Q", "", "")));

        Assert.Equal(bez, se, 3);
    }

    /// <summary>
    /// Legenda musí značku přeložit týmž číselníkem jako tabulka, jinak u ní stojí písmeno,
    /// zatímco v tabulce je obrázek.
    /// </summary>
    [Theory]
    [InlineData("N")]   // jen nástup
    [InlineData("G")]   // paragraf
    public void StalaZnackaVLegendeJePiktogram(string znak)
    {
        var pozn = new PoznamkaData { Typ = TypPoznamky.SpecialniZnak, Druh = "7", Znak = znak };

        Assert.Equal(SymbolRenderMode.FontGlyf, NotesLegendElement.ZnackaLinkoveho(pozn).Mode);
        Assert.Equal(SymbolMap.ResolveZastavka(znak).Value,
                     NotesLegendElement.ZnackaLinkoveho(pozn).Value);
    }

    /// <summary>Značky spojů zůstávají na svém číselníku - „X" jsou zkřížená kladiva.</summary>
    [Fact]
    public void ZnackaSpojeZustavaNaSvemCiselniku()
    {
        var pozn = new PoznamkaData { Typ = TypPoznamky.SpecialniZnak, Druh = "1", Znak = "X" };

        Assert.Equal(SymbolMap.ResolveLinkovy("X"), NotesLegendElement.ZnackaLinkoveho(pozn));
    }

    private static float SirkaSloupce(DataContext data)
    {
        var prvek = new LineTableElement
        { X = 0, Y = 0, Width = 200, Height = 100, SirkaSloupceSpojeMm = 4f };
        prvek.DataStyle.FontSize = 5f;
        return prvek.Rozvrzeni(data)[0].SirkySloupcu[0];
    }

    private static DataContext Data(JsonArray? znacky)
    {
        var spoj = new List<(string, object)>
        {
            ("CISLO_SPOJE", "1"), ("SKUPINA", "1"), ("SMER", "T"),
            ("CASY", new JsonArray("8:00", "8:20", "8:40")),
        };
        if (znacky is not null) spoj.Add(("ZNACKY", znacky));

        return RenderSnapshot.Data(
            ("ZASTAVKY",
            [
                RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Kroměříž"), ("PORADI", "1")),
                RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Hulín"), ("PORADI", "2")),
                RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Přerov"), ("PORADI", "3")),
            ]),
            ("SPOJE", [RenderSnapshot.Row([.. spoj])]),
            ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", ""), ("PORADI", "1"))]));
    }
}
