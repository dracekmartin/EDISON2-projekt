using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Cesta volby „Řádky turnusů" z <b>prvku</b> na server (`L6c`).
/// </summary>
/// <remarks>
/// Turnus je provozní věc dopravce, takže volba patří k prvku, ne k parametrům tisku (rozhodnutí
/// Jan 28. 8.). Serveru ale dojít musí: turnusy nejsou v datech linky a jejich dotaz do jiného
/// modulu stojí 40-550 ms na linku, takže se posílají jen tomu, kdo je tiskne. Jde to touž cestou
/// jako `DEFINICE_SLOUPCU` - vždy čerstvě ze šablony, mimo persistované hodnoty parametrů.
/// <para>⚠️ Bez druhého testu (snapshot) by zaškrtnutí volby <b>nic neudělalo</b>, dokud by
/// uživatel nezmáčkl obnovení dat: náhled by kreslil ze starých dat, ve kterých turnusy nejsou.
/// Je to táž past, kterou u parametrů hlídá `ServerSideParamsDriftTests`.</para>
/// </remarks>
public class TurnusyDoServeruTests
{
    public TurnusyDoServeruTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(),
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

    [Fact]
    public void ZapnutaVolba_JdeNaServer()
    {
        var zapnuto = TemplateDesignerViewModel.ComposeGetDataParams(
            new Dictionary<string, object?>(), "LINKA_ID", "42", null, new TemplateDataInputs(null, true, ""));
        var vypnuto = TemplateDesignerViewModel.ComposeGetDataParams(
            new Dictionary<string, object?>(), "LINKA_ID", "42", null, new TemplateDataInputs(null, false, ""));

        Assert.Equal("true", zapnuto["TURNUSY_KE_SPOJUM"]);
        Assert.Equal("false", vypnuto["TURNUSY_KE_SPOJUM"]);
    }

    /// <summary>
    /// Volbu hlásí šablona, když ji má <b>některý</b> prvek - z jednoho prvku se pozná celá sestava.
    /// Přepnutí musí změnit údaje ze šablony, podle nich se pozná, že je nutné znovu načíst data.
    /// </summary>
    [Fact]
    public void SablonaHlasiVolbuPodlePrvku()
    {
        var vm = new TemplateDesignerViewModel();
        var sekce = vm.State.CurrentTemplate.Sections[0];
        var prvek = new LineTableElement();
        sekce.Elements.Add(prvek);
        var pred = vm.CurrentDataInputs();

        Assert.False(pred.TurnusyKeSpojum, "prvek turnusy nechce, šablona je hlásit nemá");

        prvek.TurnusyKeSpojum = true;
        Assert.True(vm.CurrentDataInputs().TurnusyKeSpojum, "prvek turnusy chce, šablona to musí hlásit");
        Assert.NotEqual(pred, vm.CurrentDataInputs());
    }
}
