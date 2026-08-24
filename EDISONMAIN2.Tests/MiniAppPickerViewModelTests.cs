using EDControls;
using EDControls.EDViewModel;
using EDControls.MiniApps;
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
        new(MockServices(), MiniAppCatalog.Build(providers));

    [Fact]
    public void Flattens_AllGroups_KeepingRegistrationOrder()
    {
        var vm = Build(new Provider("MHD", App("a", "Dispečink"), App("b", "Vozidla")),
                       new Provider("Nástroje", App("c", "Obnova")));

        Assert.Equal(["Dispečink", "Vozidla", "Obnova"], vm.Choices.Select(c => c.Title));
        Assert.Equal(["MHD", "MHD", "Nástroje"], vm.Choices.Select(c => c.GroupTitle));
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
}
