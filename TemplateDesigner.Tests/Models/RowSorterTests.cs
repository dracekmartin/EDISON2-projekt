using System.Globalization;
using TemplateDesigner.Models;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// Řazení řádků dynamické tabulky (`B27`). Typ řazení se neptá uživatele - odvozuje se z hodnot,
/// protože špatně zvolený typ vyrobí výstup, který vypadá seřazeně a je v nesmyslném pořadí.
/// </summary>
public class RowSorterTests
{
    private static readonly CultureInfo Cs = new("cs-CZ");

    private static IReadOnlyList<DataContext> Rows(params string[] values) =>
        [.. values.Select(v => new DataContext(new Dictionary<string, object?> { ["POLE"] = v }, culture: Cs))];

    private static List<string> Sorted(IReadOnlyList<DataContext> rows, bool desc = false,
        string field = "POLE") =>
        [.. RowSorter.Sort(rows, Cs, (field, desc)).Select(r => r.GetString(field))];

    [Fact]
    public void CiselnySloupec_RadiCiselne_NeAbecedne()
    {
        // Jádro featury: textově by „12" bylo před „9" a uživatel nemá jak poznat, že je to špatně.
        Assert.Equal(["2", "9", "12", "100"], Sorted(Rows("12", "100", "2", "9")));
    }

    [Fact]
    public void DatumSeRadiChronologicky()
    {
        // Textově by „10.1." bylo před „1.2." a „3.3." až poslední - chronologicky je pořadí jiné.
        Assert.Equal(["10.1.2026", "1.2.2026", "3.3.2026"],
            Sorted(Rows("3.3.2026", "10.1.2026", "1.2.2026")));
    }

    [Fact]
    public void TextRadiCeskouAbecedou()
    {
        // „Ř" patří za „R", ne až za „Z" (ordinální porovnání by ho hodilo úplně jinam).
        Assert.Equal(["Radotín", "Řepy", "Smíchov"], Sorted(Rows("Smíchov", "Řepy", "Radotín")));
    }

    [Fact]
    public void SmisenySloupec_RadiJakoText()
    {
        // Kdyby stačilo „něco se naparsovalo", pořadí by záviselo na tom, které hodnoty to byly.
        var result = Sorted(Rows("10", "2", "A"));
        Assert.Equal(["10", "2", "A"], result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PrazdneHodnotyJdouVzdyDozadu(bool descending)
    {
        var result = Sorted(Rows("2", "", "1"), descending);

        Assert.Equal("", result[^1]);   // i sestupně - jinak by vyplavaly nahoru a vypadalo to jako chyba
    }

    [Fact]
    public void SestupneOtociPoradiHodnot()
    {
        Assert.Equal(["12", "9", "2"], Sorted(Rows("2", "12", "9"), desc: true));
    }

    [Fact]
    public void PriShodeKlicuZustanePoradiZDat()
    {
        // Stabilita: uživatel nesmí vidět, že se mu řádky „přeskládaly samy".
        var rows = new List<DataContext>
        {
            new(new Dictionary<string, object?> { ["POLE"] = "A", ["ID"] = "prvni" }, culture: Cs),
            new(new Dictionary<string, object?> { ["POLE"] = "A", ["ID"] = "druhy" }, culture: Cs),
            new(new Dictionary<string, object?> { ["POLE"] = "A", ["ID"] = "treti" }, culture: Cs),
        };

        var result = RowSorter.Sort(rows, Cs, ("POLE", false)).Select(r => r.GetString("ID")).ToList();

        Assert.Equal(["prvni", "druhy", "treti"], result);
    }

    [Fact]
    public void DruhyKlicRozhodujePriShodePrvniho()
    {
        var rows = new List<DataContext>
        {
            new(new Dictionary<string, object?> { ["A"] = "1", ["B"] = "b" }, culture: Cs),
            new(new Dictionary<string, object?> { ["A"] = "1", ["B"] = "a" }, culture: Cs),
            new(new Dictionary<string, object?> { ["A"] = "0", ["B"] = "z" }, culture: Cs),
        };

        var result = RowSorter.Sort(rows, Cs, ("A", false), ("B", false))
            .Select(r => r.GetString("A") + r.GetString("B")).ToList();

        Assert.Equal(["0z", "1a", "1b"], result);
    }

    [Fact]
    public void BezKlice_NechaPoradiBezeZmeny()
    {
        var rows = Rows("c", "a", "b");

        Assert.Same(rows, RowSorter.Sort(rows, Cs, (null, false), ("", true)));
    }
}
