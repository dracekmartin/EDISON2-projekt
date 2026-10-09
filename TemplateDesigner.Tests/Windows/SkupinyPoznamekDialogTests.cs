using EDControls;
using EDControls.EDInputForm;
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
        vm.Vybrana.Sloupcu = 1;

        vm.NahoruCommand.Execute(null);
        vm.OkCommand.Execute(null);

        Assert.Equal(SkupinaPoznamek.NegativniZnacky, vm.Vysledek[0].Skupina);
        Assert.Equal(1, vm.Vysledek[0].Sloupcu);
        Assert.All(vm.Vysledek.Skip(1), s => Assert.Null(s.Sloupcu));
    }

    /// <summary>Okno nabízí jen čísla; skupina bez vlastního počtu ukáže počet sloupců prvku
    /// a když ho uživatel nechá, dál ho sleduje (změna Počtu sloupců prvku ji posune s sebou).</summary>
    [Fact]
    public void BezVlastnihoPoctu_UkazeASledujePocetSloupcuPrvku()
    {
        var vm = new SkupinyPoznamekDialogViewModel(new NotesLegendElement().EfektivniSkupiny(), 3);

        Assert.Equal([1, 2, 3, 4], vm.VolbySloupcu);
        Assert.All(vm.Skupiny, s => Assert.Equal(3, s.Sloupcu));

        vm.Skupiny[1].Sloupcu = 1;
        vm.OkCommand.Execute(null);

        Assert.Equal(1, vm.Vysledek[1].Sloupcu);
        Assert.Null(vm.Vysledek[0].Sloupcu);
    }

    [Theory]
    [InlineData(true, nameof(NotesLegendElement.ShowPoznamkyLinkyRadiobus), false)]
    [InlineData(true, nameof(NotesLegendElement.Vyluky), false)]
    [InlineData(true, nameof(NotesLegendElement.ShowZnackaKola), false)]
    [InlineData(false, nameof(NotesLegendElement.ShowPoznamkyLinkyRadiobus), true)]
    [InlineData(true, NotesLegendElement.SkupinyPoznamekKlic, true)]
    [InlineData(false, NotesLegendElement.SkupinyPoznamekKlic, false)]
    [InlineData(true, nameof(NotesLegendElement.ShowNegativniZnacky), false)]
    [InlineData(true, nameof(NotesLegendElement.ShowTextovePoznamky), false)]
    [InlineData(false, nameof(NotesLegendElement.ShowSpecialniZnaky), true)]
    [InlineData(true, nameof(NotesLegendElement.Columns), true)]
    public void FormularUkazeJenTo_CoAgendaPouzije(bool linkovy, string klic, bool viditelne) =>
        Assert.Equal(viditelne, NotesLegendElement.PoleFormulareViditelne(klic, linkovy));

    /// <summary>Skupina formuláře, ve které pravidlo schová všechna pole, zmizí i s nadpisem -
    /// u linkového JŘ by jinak zůstaly prázdné „Poznámky linky" a „Značky u odjezdů".</summary>
    [Fact]
    public void SkupinaBezViditelnehoPoleZmizi()
    {
        var obsah = new Group { Key = "filter" };
        var linka = new Group { Key = "filterline" };
        List<Item> polozky =
        [
            obsah, new Field { Key = nameof(NotesLegendElement.ShowNegativniZnacky) },
            new Field { Key = NotesLegendElement.SkupinyPoznamekKlic },
            linka, new Field { Key = nameof(NotesLegendElement.Vyluky) },
            new Field { Key = nameof(NotesLegendElement.ShowPoznamkyLinkyRadiobus) },
        ];

        ElementFormBinder.PouzijViditelnost(polozky, (k, _) => NotesLegendElement.PoleFormulareViditelne(k, true));
        Assert.Equal(ItemVisibility.Visible, obsah.Visibility);
        Assert.Equal(ItemVisibility.ProgrammerHidden, linka.Visibility);

        // Přepnutí na vývěsku (týž formulář v panelu) skupinu zase ukáže.
        ElementFormBinder.PouzijViditelnost(polozky, (k, _) => NotesLegendElement.PoleFormulareViditelne(k, false));
        Assert.Equal(ItemVisibility.Visible, linka.Visibility);
    }
}
