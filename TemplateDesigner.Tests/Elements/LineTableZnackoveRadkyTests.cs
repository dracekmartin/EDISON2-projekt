using System.Text.Json.Nodes;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Tests.Rendering;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Kolik řádků značek nad časy tabulka vyhradí.
/// </summary>
/// <remarks>
/// Řádky platnosti, turnusů a negativních značek se počítají podle spojů <b>toho bloku</b>, ne
/// celé linky (od 22. 9.). Na lince 330510 měly čtyři spoje ze skupin 2 a 3 tři značky platnosti,
/// takže druhý řádek dostaly i části se skupinou 1 - a tam zůstal přes celou šířku prázdný.
/// Tisk vývěsek je o úspoře místa, takže prázdný pás je ztráta, kterou nikdo nezadal.
/// <para>⚠️ Pás skupin dnů se naopak řídí celou linkou - musí být nad každým blokem, jinak nad
/// druhým není poznat, ke kterým dnům patří (nález Jan 25. 8.).</para>
/// </remarks>
public class LineTableZnackoveRadkyTests
{
    /// <summary>
    /// ⚠️ Jádro: spoj se třemi značkami sedí v jednom bloku, takže druhý řádek platnosti dostane
    /// jen ten blok. Dokud se počty braly z celé linky, zvedl se každý - a v ostatních blocích
    /// zůstal pás přes celou šířku prázdný.
    /// </summary>
    [Fact]
    public void RadekNavicNeseJenBlokKteryHoPotrebuje()
    {
        var prvek = Prvek();
        Assert.True(prvek.Rozvrzeni(Data(tretiZnacka: Nikdo)).Count > 1,
            "fixtura se musí rozdělit aspoň na dva bloky, jinak test nic neměří");

        float bezNi = prvek.CalculateHeightFromData(Data(tretiZnacka: Nikdo));
        float uJednoho = prvek.CalculateHeightFromData(Data(tretiZnacka: 0));
        float uVsech = prvek.CalculateHeightFromData(Data(tretiZnacka: Kazdy));

        Assert.True(uJednoho > bezNi, "řádek navíc se do výšky nepropsal");
        Assert.True(uJednoho < uVsech - 0.01f,
            $"s jedním bohatým spojem {uJednoho:0.00} mm, se všemi {uVsech:0.00} mm - "
            + "řádek dostaly všechny bloky, ne jen ten, který ho potřebuje");
    }

    /// <summary>Hlava k zopakování na pokračovací straně musí stačit i nejbohatšímu bloku,
    /// jinak by se na ní řádek značek ořízl.</summary>
    [Fact]
    public void OpakovanaHlavickaStaciNejbohatsimuBloku()
    {
        var prvek = Prvek();
        var data = Data(tretiZnacka: 0);

        Assert.Equal(prvek.OpakovanaHlavickaMm(Data(tretiZnacka: Kazdy)),
            prvek.OpakovanaHlavickaMm(data), precision: 2);
    }

    /// <summary>Značku má buď každý spoj, nebo nikdo - fixtura je čitelnější než dvě čísla.</summary>
    private const int Kazdy = -1;
    private const int Nikdo = -2;

    private static LineTableElement Prvek()
    {
        var prvek = new LineTableElement { X = 0, Y = 0, Width = 60, Height = null };
        prvek.DataStyle.FontSize = 5f;
        return prvek;
    }

    /// <summary>Linka se dvěma značkami platnosti u každého spoje; spoj na zadaném indexu má tři,
    /// takže si vyžádá druhý řádek. <see cref="Kazdy"/> = mají ji všichni, <see cref="Nikdo"/> = nikdo.</summary>
    private static DataContext Data(int tretiZnacka) => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Kroměříž"), ("PORADI", "1")),
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Hulín"), ("PORADI", "2")),
        ]),
        ("SPOJE",
        [.. Enumerable.Range(0, 20).Select(i => RenderSnapshot.Row(
            ("CISLO_SPOJE", (i + 1).ToString()), ("SKUPINA", "1"), ("SMER", "T"),
            ("PLATNOST", tretiZnacka == Kazdy || i == tretiZnacka
                ? new JsonArray("X", "BEV", "6")
                : new JsonArray("X", "BEV")),
            ("CASY", new JsonArray("8:00", "8:20"))))]),
        ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", ""), ("PORADI", "1"))]));
}
