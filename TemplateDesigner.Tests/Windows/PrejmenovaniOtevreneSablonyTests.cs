using EDControls;
using EDControls.EDViewModel;
using NSubstitute;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Přejmenování otevřené šablony v okně šablon (Jan 3. 11.): uložení posílá název z otevřené šablony,
/// takže bez převzetí nového názvu by Ctrl+S přejmenování tiše vrátil a titulek by ukazoval starý název.
/// </summary>
public class PrejmenovaniOtevreneSablonyTests
{
    public PrejmenovaniOtevreneSablonyTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(), Substitute.For<IWindowNavigator>(),
            Substitute.For<IPopupDisplayer>()));

    [Fact]
    public void OtevrenaSablonaPrevezmeNovyNazev()
    {
        var vm = new TemplateDesignerViewModel();
        vm.State.CurrentTemplate.Name = "Vývěska";

        vm.ApplyRenameOfOpenTemplate(new Dictionary<Guid, string> { [vm.State.CurrentTemplate.Id] = "Vývěska 2026" });

        Assert.Equal("Vývěska 2026", vm.State.CurrentTemplate.Name);
        Assert.False(vm.IsDirty, "nový název je v databázi, není to neuložená práce");
    }

    [Fact]
    public void JinaPrejmenovanaSablonaOtevrenouNemeni()
    {
        var vm = new TemplateDesignerViewModel();
        vm.State.CurrentTemplate.Name = "Vývěska";

        vm.ApplyRenameOfOpenTemplate(new Dictionary<Guid, string> { [Guid.NewGuid()] = "Jiná" });

        Assert.Equal("Vývěska", vm.State.CurrentTemplate.Name);
    }
}
