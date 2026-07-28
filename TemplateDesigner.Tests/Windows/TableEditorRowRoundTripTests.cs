using TemplateDesigner;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Dialog sloupců pevné tabulky needituje per-buňkový formát ani styl (styl se dělá per sloupec),
/// ale musí je <b>přenést</b>. Historicky je open+OK tiše smazal - nejvíc bolelo u tabulek načtených
/// ze šablony agendy (Caché posílá <c>format</c>/<c>textColor</c> per buňku).
/// </summary>
public class TableEditorRowRoundTripTests
{
    private static readonly List<TableColumn> Columns =
    [
        new() { Header = "Položka" },
        new() { Header = "Hodnota" },
    ];

    private static TableRow SourceRow() => new()
    {
        BackgroundColor      = "#FFEEEEEE",
        VisibilityExpression = "{POCET} > 0",
        Cells =
        [
            new TableCell { Content = "Platnost od" },
            new TableCell
            {
                Content = "{DATUM_OD}",
                Format  = "dd.MM.yyyy",
                Style   = new TextStyle { Bold = true, TextColor = "#FFFF0000" },
            },
        ],
    };

    [Fact]
    public void RoundTrip_ZachovaFormatIStylBunky()
    {
        var vysledek = RowVM.FromModel(SourceRow(), Columns).ToModel();

        Assert.Equal("dd.MM.yyyy", vysledek.Cells[1].Format);
        Assert.True(vysledek.Cells[1].Style?.Bold);
        Assert.Equal("#FFFF0000", vysledek.Cells[1].Style?.TextColor);
    }

    [Fact]
    public void RoundTrip_ZachovaPozadiRadkuIPodminku()
    {
        var vysledek = RowVM.FromModel(SourceRow(), Columns).ToModel();

        Assert.Equal("#FFEEEEEE", vysledek.BackgroundColor);
        Assert.Equal("{POCET} > 0", vysledek.VisibilityExpression);
        Assert.Equal("Platnost od", vysledek.Cells[0].Content);
    }

    /// <summary>Styl se musí klonovat, ne sdílet - jinak by editace klonu měnila původní řádek (a naopak).</summary>
    [Fact]
    public void RoundTrip_StylJeKopie_NeSdilenaInstance()
    {
        var zdroj = SourceRow();
        var vysledek = RowVM.FromModel(zdroj, Columns).ToModel();

        vysledek.Cells[1].Style!.Bold = false;

        Assert.True(zdroj.Cells[1].Style?.Bold);
    }

    /// <summary>Nový sloupec (řádek ho ještě nemá) → prázdná buňka bez formátu, ne výjimka.</summary>
    [Fact]
    public void RoundTrip_ChybejiciBunkaProNovySloupec_JePrazdna()
    {
        var radek = new TableRow { Cells = [new TableCell { Content = "A" }] };

        var vysledek = RowVM.FromModel(radek, Columns).ToModel();

        Assert.Equal(2, vysledek.Cells.Count);
        Assert.Equal("", vysledek.Cells[1].Content);
        Assert.Null(vysledek.Cells[1].Format);
        Assert.Null(vysledek.Cells[1].Style);
    }
}
