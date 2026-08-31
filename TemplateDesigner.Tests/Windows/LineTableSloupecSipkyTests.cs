using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Dialog sloupců linkového JŘ pro <b>sloupec šipky směru</b> - jak se jmenuje a co u něj dává smysl
/// nastavovat.
/// </summary>
/// <remarks>
/// Oba nálezy Jana z 28. 8. při zkoušení `L6a`: v dialogu svítil anglický klíč „Arrow", a barva
/// nastavená ve „Stylu záhlaví" šipku neobarvila - u sloupce bez textu v datech se nedalo poznat,
/// že šipku řídí zóna datových buněk.
/// <para>VM je bez WPF (R4), takže jde testovat přímo; EDViewModel base ale čte EDServices.Current
/// → mock v konstruktoru.</para>
/// </remarks>
public class LineTableSloupecSipkyTests
{
    public LineTableSloupecSipkyTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(),
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

    private static LineTableColumnsEditorDialogViewModel Vm(string klicVybranehoSloupce)
    {
        var vm = new LineTableColumnsEditorDialogViewModel(
            new LineTableElement(), LineTableElement.GetDefaultColumns());
        vm.SelectedColumnVM = vm.ColumnVMs.First(c => c.Key == klicVybranehoSloupce);
        return vm;
    }

    /// <summary>
    /// Sloupec se jmenuje <b>česky</b>. Mapa lidských názvů propadá u neznámého klíče na klíč sám,
    /// takže se nový sloupec musí doplnit i tam - jinak se do UI dostane kódový název.
    /// </summary>
    [Fact]
    public void SeJmenujeCesky() => Assert.Equal("Šipka směru", Vm(ColumnKeys.Arrow).SelectedColumnVM!.DisplayName);

    /// <summary>
    /// V datové zóně jde nastavit <b>jen barva a pozadí</b> - font, řez, zarovnání ani odsazení na
    /// vektorovou šipku nemají vliv, takže tam jen mátly.
    /// </summary>
    [Fact]
    public void VDatoveZone_JdeJenBarvaAPozadi()
    {
        var vm = Vm(ColumnKeys.Arrow);

        Assert.Equal("Styl šipky", vm.NazevZonyDat);
        Assert.True(vm.MaNapoveduZonyDat);
        Assert.True(vm.DataEditOptions.AllowTextColor);
        Assert.True(vm.DataEditOptions.AllowBackgroundColor);
        Assert.False(vm.DataEditOptions.AllowFontFamily);
        Assert.False(vm.DataEditOptions.AllowFontSize);
        Assert.False(vm.DataEditOptions.AllowHAlign);
        Assert.False(vm.DataEditOptions.AllowPadding);
    }

    /// <summary>U běžného sloupce zůstává datová zóna úplná - nic se nemá skrývat.</summary>
    [Fact]
    public void UBezenehoSloupce_ZustavaZonaUplna()
    {
        var vm = Vm(ColumnKeys.StopName);

        Assert.Equal("Styl datových buněk", vm.NazevZonyDat);
        Assert.False(vm.MaNapoveduZonyDat);
        Assert.True(vm.DataEditOptions.AllowFontFamily);
        Assert.True(vm.DataEditOptions.AllowPadding);
    }

    /// <summary>Přepnutí sloupce musí zónu přepočítat - jinak by v UI zůstala z předchozího.</summary>
    [Fact]
    public void PrepnutiSloupce_ZonuPrepocita()
    {
        var vm = Vm(ColumnKeys.StopName);
        Assert.Equal("Styl datových buněk", vm.NazevZonyDat);

        vm.SelectedColumnVM = vm.ColumnVMs.First(c => c.Key == ColumnKeys.Arrow);

        Assert.Equal("Styl šipky", vm.NazevZonyDat);
        Assert.False(vm.DataEditOptions.AllowFontFamily);
    }
}
