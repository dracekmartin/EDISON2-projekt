using TemplateDesigner;
using TemplateDesigner.Models;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Barva buňky se v editoru sloupců od 7/2026 dá zapsat i textem (KOVED #12997) - jinak nešlo do
/// sloupce zastávek ani odjezdů dostat barvu z dat (<c>{BARVA_LINKY}</c>), protože nativní výběr
/// barvy ji nabídnout neumí. Textový zápis přináší i překlepy, takže platí totéž pravidlo jako
/// u formulářových polí (<see cref="ColorFieldWriteTests"/>): nesmysl se do modelu nedostane.
/// </summary>
public class CellStyleColorInputTests
{
    private static CellStyleVM Vm() => CellStyleVM.FromModel(null, ResolvedTextStyle.Default);

    [Fact]
    public void BarvaZDatSeZapise()
    {
        var vm = Vm();

        vm.TextColor = "{BARVA_LINKY}";
        vm.BgColor = "{BARVA_LINKY}";

        Assert.Equal("{BARVA_LINKY}", vm.ToModel()!.TextColor);
        Assert.Equal("{BARVA_LINKY}", vm.ToModel()!.BackgroundColor);
    }

    [Theory]
    [InlineData("#ZZZZZZ")]
    [InlineData("blbost")]
    [InlineData("#12345")]
    [InlineData("#FF33{BARVA_LINKY}")]   // míchanina pevné barvy a pole = neplatná hodnota
    public void NeplatnaBarva_NechaPredchozi(string typed)
    {
        var vm = Vm();
        vm.TextColor = "#FF3366CC";

        vm.TextColor = typed;

        Assert.Equal("#FF3366CC", vm.TextColor);
    }

    [Fact]
    public void PrazdnaHodnotaZnamenaDedit()
    {
        var vm = Vm();
        vm.BgColor = "#FFFF0000";

        vm.BgColor = "";

        Assert.Null(vm.BgColor);
        Assert.Null(vm.ToModel());   // nic explicitního → styl se celý dědí
    }

    [Fact]
    public void PlatnyHexSeZapise()
    {
        var vm = Vm();

        vm.TextColor = "#3366CC";

        Assert.Equal("#3366CC", vm.TextColor);
    }
}
