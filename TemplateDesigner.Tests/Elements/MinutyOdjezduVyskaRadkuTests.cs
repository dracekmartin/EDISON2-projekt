using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Tests.Rendering;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Výška datového řádku Minut odjezdů se musí počítat ze stejného písma, jakým se kreslí číslice.
/// Do 21. 9. se brala z písma prvku, kdežto číslice mohla zvětšit skupina dnů v dialogu skupin -
/// větší minuty se pak kreslily do nízkého řádku.
/// </summary>
/// <remarks>
/// Týž anti-vzor jako u pruhu záhlaví (<see cref="ZahlaviOdjezduPerSloupecTests"/>), jen na datové
/// straně. Řádek nese i sloupec hodin, proto se výška bere z většího z obou písem - menší písmo
/// minut ho nesmí smrsknout pod hodinu.
/// </remarks>
public class MinutyOdjezduVyskaRadkuTests
{
    private const float SirkaMm = 90f;

    private static DataContext Data() => RenderSnapshot.Data(
        ("SKUPINY_DNU",
        [
            RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní dny"), ("PORADI", "1")),
            RenderSnapshot.Row(("ID", "2"), ("NADPIS", "Sobota"), ("PORADI", "2")),
        ]),
        ("ODJEZDY",
        [
            RenderSnapshot.Row(("SKUPINA_DNU_ID", "1"), ("HODINA", "6"), ("MINUTA", "15")),
            RenderSnapshot.Row(("SKUPINA_DNU_ID", "2"), ("HODINA", "6"), ("MINUTA", "40")),
        ]));

    private static DepartureGridElement Prvek(params DepartureGroupColumnStyle[] sloupce) => new()
    {
        X = 0, Y = 0, Width = SirkaMm,
        HeaderStyle = new TextStyle { FontFamily = "Arial", FontSize = 7 },
        DataStyle = new TextStyle { FontFamily = "Arial", FontSize = 6 },
        ShowSkupinaDnuHeader = true,
        GroupColumns = [.. sloupce],
    };

    private static DepartureGroupColumnStyle Sloupec(string id, TextStyle? minuty = null) => new()
    {
        SkupinaDnuId = id,
        IsVisible = true,
        Style = new ColumnVisualStyle { Data = minuty },
    };

    [Fact]
    public void VetsiPismoMinutJednoSkupiny_ZvysiRadek()
    {
        var data = Data();
        float bezOverride = Prvek(Sloupec("1"), Sloupec("2")).CalculateHeightFromData(data);
        float sVetsim = Prvek(Sloupec("1", new TextStyle { FontSize = 20f }), Sloupec("2"))
            .CalculateHeightFromData(data);

        Assert.True(sVetsim > bezOverride,
            $"řádek se má přizpůsobit největšímu písmu minut (bez={bezOverride:0.##}, s={sVetsim:0.##})");
    }

    [Fact]
    public void MensiPismoMinut_RadekNezmensiPodHodinu()
    {
        // Ve stejném řádku stojí i hodina, která drží písmo prvku - pod ni se výška nesmí propadnout.
        var data = Data();
        float bezOverride = Prvek(Sloupec("1"), Sloupec("2")).CalculateHeightFromData(data);
        float sMensim = Prvek(Sloupec("1", new TextStyle { FontSize = 3f }), Sloupec("2"))
            .CalculateHeightFromData(data);

        Assert.Equal(bezOverride, sMensim, 3);
    }

    [Fact]
    public void PevnaVyskaRadku_PismoSkupinyJiNepohne()
    {
        // Zadaná výška řádku je uživatelova volba - odvozování z písma se má uplatnit jen tam,
        // kde ji nezadal.
        var data = Data();
        var bez = Prvek(Sloupec("1"), Sloupec("2"));
        var sVetsim = Prvek(Sloupec("1", new TextStyle { FontSize = 20f }), Sloupec("2"));
        bez.DataRowHeight = 5f;
        sVetsim.DataRowHeight = 5f;

        Assert.Equal(bez.CalculateHeightFromData(data), sVetsim.CalculateHeightFromData(data), 3);
    }
}
