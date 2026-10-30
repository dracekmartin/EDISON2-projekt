using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Tests.Elements;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Dialog odjezdů - tab „Definice sloupců": otevření (Automaticky vs. Vlastní podle elementu),
/// aplikace vzoru a výsledek po OK. VM je bez WPF (R4), takže jde testovat přímo;
/// EDViewModel base ale čte EDServices.Current → mock v konstruktoru (Reset je internal
/// pro jiné test projekty; žádný jiný TD test služby nečte, stav mezi testy nevadí).
/// </summary>
public class DepartureGroupEditorCustomColumnsTests
{
    public DepartureGroupEditorCustomColumnsTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(),
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

    /// <summary>Nabídka vzorů, jak ji posílá server u voleb parametru „Rozdělení sloupců".</summary>
    private static readonly DepartureColumnPresets.Preset[] Nabidka =
        [new("PRAC_SO_NE", "Pracovní / Sobota / Neděle+svátky"), new("PRAC_VIKEND", "Pracovní / víkend (So+Ne+svátky)")];

    private static DepartureGroupEditorDialogViewModel Vm(DepartureGridElement el) =>
        new(el, [], el.GroupColumns, el.HourColumnStyle, el.CustomColumns,
            DepartureColumnDefinitionTests.PresetyJson, presetOffer: Nabidka);

    /// <summary>Sloupce vzoru tak, jak je posílá server (obsah vzoru se měří v Caché paritě).</summary>
    private static List<DepartureColumnDefinition> Vzor(string klic = "PRAC_SO_NE") =>
        [.. DepartureColumnPresets.Parse(DepartureColumnDefinitionTests.PresetyJson)[klic].Select(c => c.Clone())];

    [Fact]
    public void ElementBezDefinice_SeOtevreJakoAutomaticky()
    {
        var vm = Vm(new DepartureGridElement());

        Assert.False(vm.UseCustomColumns);
        Assert.True(vm.AutomaticColumns);
        Assert.Empty(vm.CustomColumnVMs);
    }

    [Fact]
    public void ElementSDefinici_SeOtevreJakoVlastniSVyplnenymiSloupci()
    {
        var el = new DepartureGridElement { CustomColumns = Vzor() };
        var vm = Vm(el);

        Assert.True(vm.UseCustomColumns);
        Assert.Equal(3, vm.CustomColumnVMs.Count);
        Assert.Equal("Pracovní dny", vm.CustomColumnVMs[0].Header);
        // Sloupce odpovídají vzoru → combo ho ukáže (prázdné combo vypadá jako neuložený vzor)
        Assert.Equal("PRAC_SO_NE", vm.SelectedPreset?.Key);
    }

    [Fact]
    public void RucneUpravenaDefinice_NeukazujeZadnyVzor()
    {
        var cols = Vzor();
        cols[0].ExcludeMarks = "52";   // odchylka od vzoru
        var vm = Vm(new DepartureGridElement { CustomColumns = cols });

        Assert.Null(vm.SelectedPreset);
    }

    [Fact]
    public void PouzitVzorAOk_VratiSloupceVzoru()
    {
        var vm = Vm(new DepartureGridElement());

        vm.UseCustomColumns = true;
        vm.SelectedPreset = vm.Presets.First(p => p.Key == "PRAC_SO_NE");
        vm.ApplyPresetCommand.Execute(null);
        vm.OkCommand.Execute(null);

        Assert.NotNull(vm.ResultCustomColumns);
        Assert.Equal(3, vm.ResultCustomColumns!.Count);
        Assert.Equal(["X", "6", "+"], vm.ResultCustomColumns.Select(c => c.Days));
    }

    [Fact]
    public void PrazdnyVzor_VyprazdniSloupcePruStavbuOdZacatku()
    {
        var el = new DepartureGridElement { CustomColumns = Vzor() };
        var vm = Vm(el);

        vm.SelectedPreset = vm.Presets.First(p => p.Key == "PRAZDNA");
        vm.ApplyPresetCommand.Execute(null);

        Assert.Empty(vm.CustomColumnVMs);
        Assert.True(vm.UseCustomColumns);
        Assert.True(vm.CanAddCustomColumn);
    }

    [Fact]
    public void NevalidniSeznamZnacek_OkNezavreAOznaciSloupec()
    {
        var vm = Vm(new DepartureGridElement());
        vm.UseCustomColumns = true;
        vm.AddCustomColumnCommand.Execute(null);
        vm.SelectedCustomVM!.Header = "Test";
        vm.SelectedCustomVM.ExcludeMarks = "52 53";   // mezera místo čárky - server by 53 tiše zahodil

        vm.OkCommand.Execute(null);

        Assert.NotNull(vm.CustomColumnsError);
        Assert.Contains("Test", vm.CustomColumnsError);
        Assert.Null(vm.ResultCustomColumns);   // dialog se nezavřel, nic se nevrací

        vm.SelectedCustomVM.ExcludeMarks = "10, 20";  // oprava → OK projde
        vm.OkCommand.Execute(null);
        Assert.Null(vm.CustomColumnsError);
        Assert.NotNull(vm.ResultCustomColumns);
    }

    [Fact]
    public void PrepnutiNaAutomatickyAOk_VratiNull()
    {
        var el = new DepartureGridElement { CustomColumns = Vzor() };
        var vm = Vm(el);

        vm.AutomaticColumns = true;   // uživatel přepne radio zpět na Automaticky
        vm.OkCommand.Execute(null);

        Assert.Null(vm.ResultCustomColumns);
    }
}
