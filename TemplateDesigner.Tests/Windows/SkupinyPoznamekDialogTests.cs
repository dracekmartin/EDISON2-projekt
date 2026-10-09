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
            el.EfektivniSkupiny().Select(s => (s.Skupina, s.Tisknout, (int?)3)),
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
        Assert.All(vm.Vysledek.Skip(1), s => Assert.Equal(3, s.Sloupcu));
    }

    /// <summary>Okno nabízí jen čísla a skupinu bez vlastního počtu předvyplní počtem sloupců
    /// prvku. Uloží se napevno - společný počet formulář u linkového neukazuje, takže by na něm
    /// skupina visela neviditelně.</summary>
    [Fact]
    public void BezVlastnihoPoctu_PredvyplniPocetPrvkuAUloziHoNapevno()
    {
        var vm = new SkupinyPoznamekDialogViewModel(new NotesLegendElement().EfektivniSkupiny(), 3);

        Assert.Equal([1, 2, 3, 4], vm.VolbySloupcu);
        Assert.All(vm.Skupiny, s => Assert.Equal(3, s.Sloupcu));

        vm.Skupiny[1].Sloupcu = 1;
        vm.OkCommand.Execute(null);

        Assert.Equal(1, vm.Vysledek[1].Sloupcu);
        Assert.Equal(3, vm.Vysledek[0].Sloupcu);
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
    [InlineData(true, nameof(NotesLegendElement.Columns), false)]
    [InlineData(false, nameof(NotesLegendElement.Columns), true)]
    [InlineData(true, nameof(NotesLegendElement.WrapSmer), true)]
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

    [Theory]
    [InlineData("DleTypu", "DleTypu")]
    [InlineData("Vychozi", "Vychozi")]
    [InlineData("DleDelkyVzestupne", "DleTypu")]
    public void Linkovy_RazeniBezDleTypu_PoleNezustanePrazdne(string ulozene, string hodnotaDleDat)
    {
        var volby = NotesLegendElement.VolbyRazeni(linkovyJr: true, ulozene);

        Assert.Equal([hodnotaDleDat, "DleDelkyVzestupne", "DleDelkySestupne"], volby.Select(v => v.Hodnota));
        Assert.Equal("Výchozí (dle dat)", volby[0].Popis);
        Assert.Contains(volby, v => v.Hodnota == ulozene);
    }

    [Fact]
    public void Vyveska_MaVsechnyVolbyRazeni()
    {
        var volby = new Field { Key = nameof(NotesLegendElement.Razeni), Value = "DleTypu" };

        TemplateDesigner.MainWindow.NastavVolbyRazeni([volby], linkovyJr: false);

        Assert.Equal(["DleTypu", "Vychozi", "DleDelkyVzestupne", "DleDelkySestupne"], volby.ListItems.Select(i => i.Key));
        Assert.Equal("DleTypu", volby.Value);
    }
}
