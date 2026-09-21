using TemplateDesigner.Models.Schema;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Rozbalovací seznam vybraných linek ve stavovém chipu (`B168`).
/// </summary>
/// <remarks>
/// Hledání smí seznam jen zúžit. Kdyby položka nesla svoje pořadí v zúženém seznamu místo
/// původního indexu, klik na vyhledanou linku by přepnul na úplně jinou.
/// </remarks>
public class SeznamVybranychInstanciTests
{
    private static readonly AgendaInstance[] Linky =
    [
        new("1", "320913 České Budějovice", "CB_CSADAD"),
        new("2", "330511 Tábor - Písek", null),
        new("3", "340366 Budec", "skupina B"),
    ];

    [Fact]
    public void BezHledani_JsouVsechnyAAktualniJeOznacena()
    {
        var volby = TemplateDesignerViewModel.SestavVolby(Linky, 1, "");

        Assert.Equal(3, volby.Count);
        Assert.Equal([false, true, false], volby.Select(v => v.JeAktualni));
    }

    [Fact]
    public void Hledani_ZuziSeznamAlePodrziPuvodniPoradi()
    {
        var volby = TemplateDesignerViewModel.SestavVolby(Linky, 0, "budec");

        var jedina = Assert.Single(volby);
        Assert.Equal(2, jedina.Index);
        Assert.False(jedina.JeAktualni);
    }

    /// <summary>Seznam ukazuje jen „číslo - název", takže se ani nehledá v doplňujícím popisu.</summary>
    [Fact]
    public void Hledani_NejdeDoDoplnujicihoPopisu()
    {
        Assert.Empty(TemplateDesignerViewModel.SestavVolby(Linky, 0, "CSADAD"));
    }

    [Fact]
    public void Hledani_BezShody_VratiPrazdno()
    {
        Assert.Empty(TemplateDesignerViewModel.SestavVolby(Linky, 0, "neexistuje"));
    }
}
