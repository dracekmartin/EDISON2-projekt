using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Výběr více buněk pevné tabulky (obdélníková oblast) a operace nad ním - mazání, schránka
/// a hromadné formátování. Výběr se drží jako **kotva + aktivní buňka**, protože rozšiřování
/// (Shift+šipky, tažení) musí kotvu nechat na místě a aktivní buňka zůstává ta, do které se píše.
/// </summary>
public class FixedTableCellRangeTests
{
    public FixedTableCellRangeTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(),
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

    private static TableElement Grid(int cols = 3, int rows = 3)
    {
        var t = TableElement.CreateGrid(cols, rows);
        t.Width = 30f * cols;
        return t;
    }

    private static void Fill(TableElement table)
    {
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                table.EnsureCell(null, r, c)!.Content = $"{r}{c}";
    }

    // ── Geometrie výběru ──────────────────────────────────────────────────────

    [Fact]
    public void VyberSeDrziKotvou_TakzeRozsirovaniZpetJdeZmensit()
    {
        var range = CellRange.Single(1, 1).ExtendTo(2, 2);
        Assert.Equal(4, range.Count);

        // Zpět nahoru: kotva zůstala na (1,1), takže se výběr smrskne, ne přeskočí.
        range = range.ExtendTo(1, 1);
        Assert.True(range.IsSingle);
        Assert.Equal((1, 1), (range.ActiveRow, range.ActiveCol));
    }

    [Fact]
    public void VyberSmeremNahoruVlevo_MaSpravnyObdelnik()
    {
        var range = CellRange.Single(2, 2).ExtendTo(0, 1);

        Assert.Equal((0, 1), (range.Top, range.Left));
        Assert.Equal((2, 2), (range.Bottom, range.Right));
        Assert.Equal(6, range.Count);
        Assert.True(range.Contains(1, 2));
        Assert.False(range.Contains(1, 0));
        // Aktivní buňka je tam, kam uživatel došel - podle ní se píše i vkládají řádky.
        Assert.Equal((0, 1), (range.ActiveRow, range.ActiveCol));
    }

    [Fact]
    public void BunkySeVraciPoRadcich_StejnePorediJakoDoSchranky()
    {
        var cells = CellRange.Single(0, 0).ExtendTo(1, 1).Cells().ToList();

        Assert.Equal([(0, 0), (0, 1), (1, 0), (1, 1)], cells);
    }

    /// <summary>Aktivní buňka jde na začátek úseku - obdélník je daný dvěma rohy, takže ji
    /// uprostřed výběru držet nelze; od začátku se navíc přirozeně píše.</summary>
    [Fact]
    public void CelyRadekASloupec_PokryjiUsekAAktivniBunkaJdeNaJehoZacatek()
    {
        var range = CellRange.Single(1, 2);

        var radek = range.WholeRow(columns: 4);
        Assert.Equal((1, 0), (radek.Top, radek.Left));
        Assert.Equal((1, 3), (radek.Bottom, radek.Right));
        Assert.Equal((1, 0), (radek.ActiveRow, radek.ActiveCol));

        var sloupec = range.WholeColumn(rows: 3);
        Assert.Equal((0, 2), (sloupec.Top, sloupec.Left));
        Assert.Equal((2, 2), (sloupec.Bottom, sloupec.Right));
        Assert.Equal((0, 2), (sloupec.ActiveRow, sloupec.ActiveCol));

        var cela = CellRange.Whole(rows: 3, columns: 4);
        Assert.Equal(12, cela.Count);
        Assert.Equal((0, 0), (cela.ActiveRow, cela.ActiveCol));
    }

    /// <summary>Ctrl+Z jde mimo náš kód, takže po vrácení vloženého řádku může výběr ukazovat
    /// mimo mřížku - tam už by mazání ani psaní nic nedělalo.</summary>
    [Fact]
    public void VyberMimoMrizku_SeSrovnaDoNi()
    {
        var range = new CellRange(5, 7, 9, 9).Clamp(rows: 3, columns: 2);

        Assert.Equal((2, 1), (range.Top, range.Left));
        Assert.Equal((2, 1), (range.Bottom, range.Right));
    }

    // ── Schránka a mazání ─────────────────────────────────────────────────────

    [Fact]
    public void KopieOblasti_MaTvarCitelnyExcelem()
    {
        var table = Grid();
        Fill(table);

        var text = table.CopyRangeToText(null, CellRange.Single(0, 0).ExtendTo(1, 1));

        Assert.Equal("00\t01\r\n10\t11", text);
    }

    [Fact]
    public void KopieJedneBunky_JeProstyText()
    {
        var table = Grid();
        Fill(table);

        Assert.Equal("12", table.CopyRangeToText(null, CellRange.Single(1, 2)));
    }

    [Fact]
    public void VymazaniOblasti_SmazeObsahAleNechaStyl()
    {
        var table = Grid();
        Fill(table);
        table.EnsureCell(null, 0, 0)!.Style = new TextStyle { Bold = true };

        bool zmena = table.ClearRangeContent(null, CellRange.Single(0, 0).ExtendTo(1, 1));

        Assert.True(zmena);
        Assert.Equal("", table.EnsureCell(null, 0, 0)!.Content);
        Assert.Equal("", table.EnsureCell(null, 1, 1)!.Content);
        Assert.Equal("02", table.EnsureCell(null, 0, 2)!.Content);   // mimo výběr
        Assert.True(table.EnsureCell(null, 0, 0)!.Style!.Bold);      // styl zůstává
    }

    [Fact]
    public void VymazaniPrazdneOblasti_NehlasiZmenu()
    {
        Assert.False(Grid().ClearRangeContent(null, CellRange.Single(0, 0).ExtendTo(2, 2)));
    }

    [Fact]
    public void VlozeniJedneHodnotyDoOblasti_JiVyplni()
    {
        var table = Grid();
        Fill(table);

        table.FillRange(null, CellRange.Single(0, 0).ExtendTo(1, 1), "X");

        Assert.Equal("X", table.EnsureCell(null, 0, 0)!.Content);
        Assert.Equal("X", table.EnsureCell(null, 1, 1)!.Content);
        Assert.Equal("02", table.EnsureCell(null, 0, 2)!.Content);
    }

    // ── Hromadné formátování ──────────────────────────────────────────────────

    private static TableCellFormatting Formatting(TableElement table, CellRange range) =>
        new(table, () => null, [.. range.Cells()]);

    [Fact]
    public void FormatovaniNadVyberem_PlatiProVsechnyBunkyOblasti()
    {
        var table = Grid();
        var range = CellRange.Single(0, 0).ExtendTo(1, 1);

        Formatting(table, range).Apply("Tučné", s => s.Bold = true);

        foreach (var (row, col) in range.Cells())
            Assert.True(table.EnsureCell(null, row, col)!.Style!.Bold);
        Assert.Null(table.EnsureCell(null, 2, 2)!.Style);   // mimo výběr
    }

    /// <summary>
    /// Formulář zapisuje do aktivní buňky, ale s označenou oblastí čeká uživatel změnu na celé
    /// oblasti. Dorovnat se smí **jen to, co se reálně změnilo** - jinak by změna barvy srovnala
    /// i písmo a uživatel by o svoje nastavení přišel.
    /// </summary>
    [Fact]
    public void ZmenaVeFormulari_SeDorovnaNaVyber_AleJenTaZmenena()
    {
        var table = Grid();
        var range = CellRange.Single(0, 0).ExtendTo(0, 1);
        table.EnsureCell(null, 0, 1)!.Style = new TextStyle { FontSize = 20f };

        var formatting = Formatting(table, range);
        var before = formatting.Capture();

        // Tohle udělá formulář nad aktivní buňkou.
        var active = table.EnsureCell(null, 0, 0)!;
        active.Style = new TextStyle { TextColor = "#FFAA0000" };
        formatting.ApplyActiveChangesToOthers(before);

        var druha = table.EnsureCell(null, 0, 1)!.Style!;
        Assert.Equal("#FFAA0000", druha.TextColor);   // změna se dorovnala
        Assert.Equal(20f, druha.FontSize);            // vlastní velikost zůstala
    }

    [Fact]
    public void ZmenaFormatuHodnotyVeFormulari_SeDorovnaTaky()
    {
        var table = Grid();
        var range = CellRange.Single(0, 0).ExtendTo(0, 2);
        var formatting = Formatting(table, range);
        var before = formatting.Capture();

        table.EnsureCell(null, 0, 0)!.Format = "N2";
        formatting.ApplyActiveChangesToOthers(before);

        Assert.Equal("N2", table.EnsureCell(null, 0, 2)!.Format);
    }

    [Fact]
    public void ZpetnyKrokNadVyberem_VratiKazdeBunceJejiVlastniStav()
    {
        var table = Grid();
        var range = CellRange.Single(0, 0).ExtendTo(0, 1);
        table.EnsureCell(null, 0, 1)!.Style = new TextStyle { FontSize = 20f };

        var command = Formatting(table, range).Apply("Tučné", s => s.Bold = true)!;
        command.Undo();

        Assert.Null(table.EnsureCell(null, 0, 0)!.Style);
        Assert.Equal(20f, table.EnsureCell(null, 0, 1)!.Style!.FontSize);
        Assert.Null(table.EnsureCell(null, 0, 1)!.Style!.Bold);
    }
}
