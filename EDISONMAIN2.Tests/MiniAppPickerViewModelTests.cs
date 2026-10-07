using EDControls;
using EDControls.EDViewModel;
using EDControls.MiniApps;
using EDISONMAIN2.MiniApps;
using EDISONMAIN2.Windows.MiniAppPickerWindow;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Výběr miniaplikace v okně (HD 13132/F2) - katalog se sloučí do jednoúrovňového seznamu,
/// skupina zůstává popiskem řádku.
/// </summary>
public class MiniAppPickerViewModelTests
{
    private static EDServices MockServices() =>
        new(null, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>());

    private sealed class Provider(string group, params MiniAppDescriptor[] apps) : IMiniAppProvider
    {
        public string GroupTitle => group;
        public IEnumerable<MiniAppDescriptor> GetMiniApps() => apps;
    }

    private static MiniAppDescriptor App(string id, string title) =>
        new(id, title, () => null!);

    private static MiniAppPickerViewModel Build(params IMiniAppProvider[] providers) =>
        BuildWith([], providers);

    private static MiniAppPickerViewModel BuildWith(IReadOnlyList<MiniAppTarget> targets,
                                                    params IMiniAppProvider[] providers) =>
        new(MockServices(), MiniAppCatalog.Build(providers), targets);

    private static MiniAppTarget Target(bool isActive, params string[] titles) =>
        new(new object(), titles, isActive);

    [Fact]
    public void Flattens_AllGroups_SortedByTitle()
    {
        var vm = Build(new Provider("MHD", App("a", "Dispečink"), App("b", "Vozidla")),
                       new Provider("Nástroje", App("c", "Obnova")));

        Assert.Equal(["Dispečink", "Obnova", "Vozidla"], vm.Choices.Select(c => c.Title));
        Assert.Equal(["MHD", "Nástroje", "MHD"], vm.Choices.Select(c => c.GroupTitle));
    }

    /// <summary>Řadí se česky bez ohledu na kulturu stanice: Ch až za H, Š až za S.</summary>
    [Fact]
    public void Choices_UseCzechAlphabet()
    {
        var vm = Build(new Provider("Přehledy",
            App("a", "Školení zaměstnanců"), App("b", "Chyby"), App("c", "Sklady MTZ"),
            App("d", "Hlášení"), App("e", "Údržba vozidel"), App("f", "Cesty")));

        Assert.Equal(["Cesty", "Hlášení", "Chyby", "Sklady MTZ", "Školení zaměstnanců", "Údržba vozidel"],
                     vm.Choices.Select(c => c.Title));
    }

    [Fact]
    public void PreselectsFirst_SoOkWorksWithoutClicking()
    {
        var vm = Build(new Provider("MHD", App("a", "Dispečink")));

        Assert.Equal("Dispečink", vm.Selected?.Title);
    }

    [Fact]
    public void EmptyCatalog_HasNoSelection()
    {
        var vm = Build();

        Assert.Empty(vm.Choices);
        Assert.Null(vm.Selected);
    }

    [Fact]
    public void Ok_WithSelection_ClosesDialogAsConfirmed()
    {
        var vm = Build(new Provider("MHD", App("a", "Dispečink")));
        bool? result = null;
        vm.RequestClose += r => result = r;

        vm.OkCommand.Execute(null);

        Assert.True(result);
    }

    [Fact]
    public void Ok_WithoutSelection_DoesNotClose()
    {
        // Prázdný katalog / zrušený výběr - volající by jinak otevíral null.
        var vm = Build();
        bool closed = false;
        vm.RequestClose += _ => closed = true;

        vm.OkCommand.Execute(null);

        Assert.False(closed);
    }

    [Fact]
    public void Placements_ListOccupiedPanes_ThenNewColumnAndRow()
    {
        var vm = BuildWith([Target(false, "Plocha"), Target(false, "Dispečink", "Vozidla")],
                           new Provider("MHD", App("a", "Dispečink")));

        Assert.Equal(["Plocha", "Dispečink, Vozidla", "Nový sloupec vpravo", "Nová řada dole"],
                     vm.Placements.Select(p => p.Label));
        Assert.True(vm.HasPlacements);
    }

    [Fact]
    public void Placements_ManyTitles_AreTruncated()
    {
        var vm = BuildWith([Target(false, "A", "B", "C", "D")], new Provider("MHD", App("a", "X")));

        Assert.Equal("A, B + 2 další", vm.Placements[0].Label);
    }

    [Fact]
    public void Placements_PreselectsActivePane()
    {
        var vm = BuildWith([Target(false, "Plocha"), Target(true, "Dispečink")],
                           new Provider("MHD", App("a", "X")));

        Assert.Equal("Dispečink", vm.SelectedPlacement?.Label);
    }

    [Fact]
    public void Placements_NoActivePane_PreselectsFirst()
    {
        var vm = BuildWith([Target(false, "Plocha"), Target(false, "Dispečink")],
                           new Provider("MHD", App("a", "X")));

        Assert.Equal("Plocha", vm.SelectedPlacement?.Label);
    }

    [Fact]
    public void Placements_OnlyEmptyPane_OffersNothing()
    {
        // Prázdné pod-okno je jen to poslední zbylé - každá volba by skončila stejně
        // (panel vyplní plochu), takže se sekce v okně vůbec neukáže.
        var vm = BuildWith([Target(false)], new Provider("MHD", App("a", "X")));

        Assert.Empty(vm.Placements);
        Assert.False(vm.HasPlacements);
        Assert.Null(vm.SelectedPlacement);
    }
}
