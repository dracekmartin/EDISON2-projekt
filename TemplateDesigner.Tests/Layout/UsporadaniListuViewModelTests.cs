using TemplateDesigner.Models;
using TemplateDesigner.Models.Layout;

namespace TemplateDesigner.Tests.Layout;

/// <summary>
/// Panel „Uspořádání na list" tiskového okna: papír a co se skládá, počet míst se spočítá sám
/// (rozhodnutí Jan 28. 9.).
/// </summary>
public class UsporadaniListuViewModelTests
{
    private static readonly PageSize A5 = new() { Width = 148, Height = 210 };

    private static (UsporadaniListuViewModel Vm, Dictionary<string, object?> Hodnoty) Panel(int stranek,
        Dictionary<string, object?>? hodnoty = null)
    {
        hodnoty ??= [];
        return (new UsporadaniListuViewModel(hodnoty, stranek, A5), hodnoty);
    }

    private static VolbaListu Papir(UsporadaniListuViewModel vm, string klic) => vm.Formaty.Single(f => f.Klic == klic);

    [Fact]
    public void Papir_NabiziJenTo_KamSeVejdouAsponDveStranky()
    {
        var (vm, _) = Panel(1);

        // A5 na výšku: A4 na výšku jen 1 místo (#13158) - nenabízí se.
        Assert.Equal(["NONE", "A4_LANDSCAPE", "A3_PORTRAIT", "A3_LANDSCAPE"], vm.Formaty.Select(f => f.Klic));
    }

    [Fact]
    public void MistaSeSpocitajiZPapiru_AMeritka()
    {
        var (vm, _) = Panel(1);
        vm.VybranyFormat = Papir(vm, "A3_PORTRAIT");
        Assert.Equal((2, 2), (vm.Radku, vm.Sloupcu));

        vm.NastavMeritko(1.3f);   // 192×273 mm - na A3 na výšku se vejde jen jedna do šířky
        Assert.Equal((1, 1), (vm.Radku, vm.Sloupcu));
    }

    [Fact]
    public void StaraUlozenaMensiMrizka_SeNahradiNejvetsi()
    {
        var hodnoty = new Dictionary<string, object?> { [SheetLayoutKeys.Sheet] = "A3_PORTRAIT", [SheetLayoutKeys.Grid] = "1x2" };
        var (vm, _) = Panel(1, hodnoty);

        Assert.Equal(SheetLayoutKeys.GridAuto, hodnoty[SheetLayoutKeys.Grid]);
        Assert.Equal(4, vm.PredniMista.Count);
    }

    [Fact]
    public void JednostrankovaSablona_OboustranneNenabizi()
    {
        var (vm, _) = Panel(1, new() { [SheetLayoutKeys.Sheet] = "A4_LANDSCAPE", [SheetLayoutKeys.Oboustranne] = "true" });

        Assert.False(vm.NabiziOboustranne);
        Assert.Equal(RezimListu.ZaSebou, vm.Rezim);
    }

    [Fact]
    public void Rezimy_ZapisujiStareKlice()
    {
        var (vm, hodnoty) = Panel(2);
        vm.VybranyFormat = Papir(vm, "A4_LANDSCAPE");

        vm.Oboustranne = true;
        Assert.Equal(("false", "true"), (hodnoty[SheetLayoutKeys.Repeat], hodnoty[SheetLayoutKeys.Oboustranne]));
        vm.Kopie = true;
        Assert.Equal(("true", "false"), (hodnoty[SheetLayoutKeys.Repeat], hodnoty[SheetLayoutKeys.Oboustranne]));
    }

    [Fact]
    public void Nakres_ZaSebou_DvoustrankovyDoklad()
    {
        var (vm, _) = Panel(2);
        vm.VybranyFormat = Papir(vm, "A4_LANDSCAPE");

        Assert.Equal(["1. doklad · str. 1", "1. doklad · str. 2"], vm.PredniMista.Select(m => m.Popis));
    }

    /// <summary>Stazky KV (#12854): A4 na šířku, líce vedle sebe, ruby na tatáž místa.</summary>
    [Fact]
    public void Nakres_Oboustranne_A4NaSirku()
    {
        var (vm, _) = Panel(2);
        vm.VybranyFormat = Papir(vm, "A4_LANDSCAPE");
        vm.Oboustranne = true;

        Assert.Equal(["1. doklad · líc", "2. doklad · líc"], vm.PredniMista.Select(m => m.Popis));
        Assert.Equal(["1. doklad · rub", "2. doklad · rub"], vm.ZadniMista.Select(m => m.Popis));
    }

    /// <summary>A3 na výšku se převrací kolem svislé osy - ruby mají prohozené sloupce.</summary>
    [Fact]
    public void Nakres_Oboustranne_A3NaVysku_ZrcadliSloupce()
    {
        var (vm, _) = Panel(2);
        vm.VybranyFormat = Papir(vm, "A3_PORTRAIT");
        vm.Oboustranne = true;

        Assert.Equal(["2. doklad · rub", "1. doklad · rub", "4. doklad · rub", "3. doklad · rub"],
            vm.ZadniMista.Select(m => m.Popis));
    }

    [Fact]
    public void OrezoveZnacky_NoveSeNenabizeji_UlozeneZustanou()
    {
        Assert.False(Panel(2).Vm.NabiziOrezoveZnacky);
        Assert.True(Panel(2, new() { [SheetLayoutKeys.Sheet] = "A4_LANDSCAPE", [SheetLayoutKeys.Marks] = "true" }).Vm.NabiziOrezoveZnacky);
    }

    [Fact]
    public void Kopie_NovaSeNenabizi_UlozenaZustane()
    {
        Assert.False(Panel(2).Vm.NabiziKopie);

        var (vm, _) = Panel(1, new() { [SheetLayoutKeys.Sheet] = "A4_LANDSCAPE", [SheetLayoutKeys.Repeat] = "true" });
        Assert.True(vm.NabiziKopie);
        vm.ZaSebou = true;
        Assert.True(vm.NabiziKopie);   // přepnutí volbu nezahodí, dá se k ní vrátit
    }

    [Fact]
    public void Panel_JenKdyzJeCoSkladat()
    {
        var a4 = new PageSize { Width = 210, Height = 297 };
        Assert.False(new UsporadaniListuViewModel([], 1, a4).UkazatPanel);                     // běžná A4
        Assert.True(new UsporadaniListuViewModel([], 2, a4).UkazatPanel);                      // doklad o dvou stránkách
        Assert.False(new UsporadaniListuViewModel([], 1, A5).UkazatPanel);                     // jednostránková i u A5
        Assert.True(new UsporadaniListuViewModel(new() { [SheetLayoutKeys.Sheet] = "A3_LANDSCAPE" }, 1, a4).UkazatPanel);   // už zapnuté
    }
}
