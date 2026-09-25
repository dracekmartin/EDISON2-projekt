using TemplateDesigner.Models;
using TemplateDesigner.Models.Layout;

namespace TemplateDesigner.Tests.Layout;

/// <summary>Panel „Uspořádání na list" tiskového okna (`B186`).</summary>
public class UsporadaniListuViewModelTests
{
    private static readonly PageSize A5 = new() { Width = 148, Height = 210 };
    private static readonly VolbaListu A4NaSirku = UsporadaniListuViewModel.Formaty.Single(f => f.Klic == "A4_LANDSCAPE");

    private static (UsporadaniListuViewModel Vm, List<UsporadaniListu> Zmeny) Panel(UsporadaniListu? plan, int stranek)
    {
        var zmeny = new List<UsporadaniListu>();
        return (new UsporadaniListuViewModel(plan, stranek, A5, zmeny.Add), zmeny);
    }

    /// <summary>Doklad o dvou stránkách začne režimem „jeden doklad": stránky vedle sebe.</summary>
    [Fact]
    public void DveStranky_VychoziJedenDoklad()
    {
        var (vm, zmeny) = Panel(null, 2);

        vm.VybranyFormat = A4NaSirku;

        Assert.True(vm.JedenDoklad);
        Assert.Equal(["Stránka 1", "Stránka 2"], vm.PredniPole.Select(p => p.Popis));
        Assert.Equal("Na list 1 doklad, 1 strana PDF.", vm.Souhrn);
        Assert.Single(zmeny);
    }

    /// <summary>Přepnutí na „víc dokladů" vyplní tvar pro stazky (#12854).</summary>
    [Fact]
    public void PrepnutiNaVicDokladu_Stazky()
    {
        var (vm, _) = Panel(null, 2);
        vm.VybranyFormat = A4NaSirku;

        vm.VicDokladu = true;

        Assert.Equal(["Stránka 1 · 1. doklad", "Stránka 1 · 2. doklad"], vm.PredniPole.Select(p => p.Popis));
        Assert.Equal(["Stránka 2 · 1. doklad", "Stránka 2 · 2. doklad"], vm.ZadniPole.Select(p => p.Popis));
        Assert.Equal("Na list 2 doklady, 2 strany PDF.", vm.Souhrn);
    }

    /// <summary>Jednostránkový doklad (vývěska) začne režimem „víc dokladů" - dvě vývěsky na A4.</summary>
    [Fact]
    public void JednaStranka_VychoziVicDokladu()
    {
        var (vm, _) = Panel(null, 1);

        vm.VybranyFormat = A4NaSirku;

        Assert.True(vm.VicDokladu);
        Assert.Equal(["1x1", "1x2"], vm.Mrizky.Select(m => m.Klic));
        Assert.All(vm.ZadniPole, p => Assert.True(p.JePrazdne));
    }

    [Fact]
    public void Vypnuti_PosleFormatVypnuto()
    {
        var (vm, zmeny) = Panel(UsporadaniListu.Vychozi(1, false, "A4_LANDSCAPE", 1, 2), 1);

        vm.VybranyFormat = UsporadaniListuViewModel.Formaty[0];

        Assert.False(vm.JeZapnuto);
        Assert.Equal(SheetLayoutKeys.SheetNone, zmeny[^1].Format);
    }

    /// <summary>Uložená volba z dřívějších polí se převede na nákres, neztratí se.</summary>
    [Fact]
    public void StareVolby_PrevedouSeNaNakres()
    {
        var hodnoty = new Dictionary<string, object?>
        {
            [SheetLayoutKeys.Sheet] = "A4_LANDSCAPE",
            [SheetLayoutKeys.Grid] = "AUTO",
            [SheetLayoutKeys.Oboustranne] = "true",
        };

        var plan = PrintDialogViewModel.PocatecniUsporadani(new Template { PageSize = A5 }, hodnoty, pocetStranek: 2)!;

        Assert.False(plan.JedenDoklad);
        Assert.Equal([0, 0], plan.Predni);
        Assert.Equal([1, 1], plan.Zadni);
    }

    [Fact]
    public void UsporadaniZeSablony_MaPrednost()
    {
        var zeSablony = UsporadaniListu.Vychozi(2, false, "A4_LANDSCAPE", 1, 2);
        var sablona = new Template { PageSize = A5, PrintParameters = new() { [SheetLayoutKeys.Plan] = zeSablony.ToJson() } };
        var hodnoty = new Dictionary<string, object?> { [SheetLayoutKeys.Plan] = UsporadaniListu.Vychozi(1, false, "A3_LANDSCAPE", 2, 2).ToJson() };

        var plan = PrintDialogViewModel.PocatecniUsporadani(sablona, hodnoty, 2)!;

        Assert.Equal("A4_LANDSCAPE", plan.Format);
    }
}
