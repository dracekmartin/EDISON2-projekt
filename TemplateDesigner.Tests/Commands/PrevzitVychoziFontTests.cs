using TemplateDesigner.Commands;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using Xunit;

namespace TemplateDesigner.Tests.Commands;

/// <summary>
/// `B153`: příkaz „Použít výchozí font u všech prvků" smaže font zapsaný u prvků, aby převzaly
/// výchozí font dokumentu.
/// </summary>
/// <remarks>
/// Šablony založené do 30. 6. 2026 mají font u každého prvku (tehdy ho tak dostal každý nový),
/// takže v nich volba „Výchozí font" nic nedělá. Týká se to i jejich kopií.
/// <para>Klíčové je, že se hledá **reflexí**: font sedí i ve stylech jednotlivých sloupců, a ruční
/// výčet by při přidání dalšího stylu tiše zastaral.</para>
/// </remarks>
public class PrevzitVychoziFontTests
{
    private static LineTableElement TabulkaSFonty()
    {
        var p = new LineTableElement
        {
            X = 0, Y = 0, Width = 100,
            HeaderStyle = new TextStyle { FontFamily = "Arial", FontSize = 5, Bold = true },
            DataStyle = new TextStyle { FontFamily = "Arial Narrow", FontSize = 4.8f },
            PripojStyle = new TextStyle { FontFamily = "Arial", FontSize = 4.6f },
        };
        // Font zanořený ve sloupci - na tohle ruční výčet zapomíná. Prvek si sloupce
        // předvyplňuje, takže ten svůj hledáme od konce.
        p.Columns.Add(new StopColumn
        {
            Key = "StopName",
            Style = new ColumnVisualStyle { Data = new TextStyle { FontFamily = "Tahoma" } }
        });
        return p;
    }

    [Fact]
    public void SmazeFontVcetneToho_CoJeVeSloupci()
    {
        var prvek = TabulkaSFonty();
        var prikaz = new PrevzitVychoziFontCommand([prvek]);

        prikaz.Execute();

        Assert.Null(prvek.HeaderStyle.FontFamily);
        Assert.Null(prvek.DataStyle.FontFamily);
        Assert.Null(prvek.PripojStyle.FontFamily);
        Assert.Null(prvek.Columns[^1].Style!.Data!.FontFamily);
        Assert.True(prikaz.PocetZmenenych >= 4, $"změnilo se jen {prikaz.PocetZmenenych} stylů");
    }

    /// <summary>Mění se jen rodina písma - velikost a tučnost zůstávají.</summary>
    [Fact]
    public void OstatniVlastnostiZustanou()
    {
        var prvek = TabulkaSFonty();

        new PrevzitVychoziFontCommand([prvek]).Execute();

        Assert.Equal(5, prvek.HeaderStyle.FontSize);
        Assert.True(prvek.HeaderStyle.Bold);
        Assert.Equal(4.8f, prvek.DataStyle.FontSize);
    }

    /// <summary>Ctrl+Z musí vrátit původní fonty - proto se na nic neptáme.</summary>
    [Fact]
    public void ZpetnyKrokVratiPuvodniFonty()
    {
        var prvek = TabulkaSFonty();
        var prikaz = new PrevzitVychoziFontCommand([prvek]);

        prikaz.Execute();
        prikaz.Undo();

        Assert.Equal("Arial", prvek.HeaderStyle.FontFamily);
        Assert.Equal("Arial Narrow", prvek.DataStyle.FontFamily);
        Assert.Equal("Tahoma", prvek.Columns[^1].Style!.Data!.FontFamily);
    }

    /// <summary>Opakované provedení (redo po zpětném kroku) musí zabrat znovu.</summary>
    [Fact]
    public void ZnovuProvedeniZaberezZnovu()
    {
        var prvek = TabulkaSFonty();
        var prikaz = new PrevzitVychoziFontCommand([prvek]);

        prikaz.Execute();
        prikaz.Undo();
        prikaz.Execute();

        Assert.Null(prvek.HeaderStyle.FontFamily);
        Assert.Null(prvek.Columns[^1].Style!.Data!.FontFamily);
    }

    /// <summary>Prvek bez vlastního fontu se nemá čeho vzdát - počet zůstane nula.</summary>
    [Fact]
    public void BezVlastnihoFontuSeNicNemeni()
    {
        var prvek = new TextElement { X = 0, Y = 0, Width = 50, Style = new TextStyle { FontSize = 9 } };
        var prikaz = new PrevzitVychoziFontCommand([prvek]);

        prikaz.Execute();

        Assert.Equal(0, prikaz.PocetZmenenych);
        Assert.Equal(9, prvek.Style.FontSize);
    }
}
