using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Okno „Druhy poznámek" (`#7487`) a co z formuláře prvku Poznámky vidí uživatel u které agendy.
/// </summary>
public class SkupinyPoznamekDialogTests
{
    public SkupinyPoznamekDialogTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(),
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

    [Fact]
    public void PotvrzeniBezeZmeny_VratiTotez()
    {
        var el = new NotesLegendElement { Columns = 3, ShowNegativniZnacky = false };
        var vm = new SkupinyPoznamekDialogViewModel(el.EfektivniSkupiny(), el.Columns);

        vm.OkCommand.Execute(null);

        Assert.Equal(
            el.EfektivniSkupiny().Select(s => (s.Skupina, s.Tisknout, s.Sloupcu)),
            vm.Vysledek.Select(s => (s.Skupina, s.Tisknout, s.Sloupcu)));
    }

    [Fact]
    public void PosunAPocetSloupcu_SePropisi()
    {
        var vm = new SkupinyPoznamekDialogViewModel(new NotesLegendElement().EfektivniSkupiny(), 3);
        vm.Vybrana = vm.Skupiny.Single(s => s.Skupina == SkupinaPoznamek.NegativniZnacky);
        vm.Vybrana.Sloupcu = vm.VolbySloupcu.Single(v => v.Hodnota == 1);

        vm.NahoruCommand.Execute(null);
        vm.OkCommand.Execute(null);

        Assert.Equal(SkupinaPoznamek.NegativniZnacky, vm.Vysledek[0].Skupina);
        Assert.Equal(1, vm.Vysledek[0].Sloupcu);
        Assert.All(vm.Vysledek.Skip(1), s => Assert.Null(s.Sloupcu));
    }

    [Fact]
    public void VychoziVolbaUkazePocetSloupcuPrvku()
    {
        var vm = new SkupinyPoznamekDialogViewModel(new NotesLegendElement().EfektivniSkupiny(), 3);

        Assert.Equal("Výchozí (3)", vm.VolbySloupcu[0].Popis);
        Assert.Null(vm.VolbySloupcu[0].Hodnota);
    }

    [Theory]
    [InlineData(true, NotesLegendElement.SkupinyPoznamekKlic, true)]
    [InlineData(false, NotesLegendElement.SkupinyPoznamekKlic, false)]
    [InlineData(true, nameof(NotesLegendElement.ShowNegativniZnacky), false)]
    [InlineData(true, nameof(NotesLegendElement.ShowTextovePoznamky), false)]
    [InlineData(false, nameof(NotesLegendElement.ShowSpecialniZnaky), true)]
    [InlineData(true, nameof(NotesLegendElement.Columns), true)]
    public void FormularUkazeJenTo_CoAgendaPouzije(bool linkovy, string klic, bool viditelne) =>
        Assert.Equal(viditelne, NotesLegendElement.PoleFormulareViditelne(klic, linkovy));
}
