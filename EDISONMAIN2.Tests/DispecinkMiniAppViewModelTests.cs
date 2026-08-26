using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.LegacyMiniApps;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Testuje parsování CVar výstupu z dispečink API do VM přes mock <see cref="IMLRest"/> -
/// bez živé Caché. Ověřuje kontrakt (pořadí polí GetJizdy / GetPracoviste).
/// </summary>
public class DispecinkMiniAppViewModelTests
{
    private const string CacheClass = "ALVA.MHD2.PlanDopravy.API.DispecinkMiniApp";

    // InlineRestCallRunner místo produkčního (klon + Task.Run) - volá mock synchronně.
    private static DispecinkMiniAppViewModel NewVm(IMLRest mlRest) =>
        new(new EDServices(mlRest, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()),
            new InlineRestCallRunner(mlRest));

    [Fact]
    public void OnOpened_LoadsPracoviste_AndSelectsFirst()
    {
        var pracoviste = new CVar();
        pracoviste["1"] = new CList("1001", "MHD Karlovy Vary");
        pracoviste["2"] = new CList("1002", "MHD Cheb");
        var mlRest = Substitute.For<IMLRest>();
        mlRest.QueryOrThrow(CacheClass, "GetPracoviste", Arg.Any<CVar>()).Returns(pracoviste);
        var vm = NewVm(mlRest);

        vm.OnOpened();

        Assert.Equal(2, vm.Pracoviste.Count);
        Assert.Equal("1001", vm.SelectedPracoviste!.Id);
        Assert.Equal("MHD Karlovy Vary", vm.SelectedPracoviste.Nazev);
    }

    [Fact]
    public void LoadCommand_ParsesJizdyRows()
    {
        var pracoviste = new CVar();
        pracoviste["1"] = new CList("1001", "MHD Karlovy Vary");

        var jizdy = new CVar();
        jizdy["1"] = new CList("Ranní", "12", "Běžná", "TS1", "06:00", "14:00", "Jan Novák", "1AB2345", "101", "1");

        var mlRest = Substitute.For<IMLRest>();
        mlRest.QueryOrThrow(CacheClass, "GetPracoviste", Arg.Any<CVar>()).Returns(pracoviste);
        mlRest.QueryOrThrow(CacheClass, "GetJizdy", Arg.Any<CVar>()).Returns(jizdy);
        var vm = NewVm(mlRest);
        vm.OnOpened();

        vm.LoadCommand.Execute(null);

        var row = Assert.Single(vm.Jizdy);
        Assert.Equal("Ranní", row.Smena);
        Assert.Equal("12", row.Linka);
        Assert.Equal("Jan Novák", row.Ridic);
        Assert.Equal("1AB2345", row.VuzRz);
        Assert.Equal("101", row.VuzEvc);
        Assert.True(row.Vyjeto);
    }

    [Fact]
    public void LoadCommand_NoSelectedPracoviste_DoesNothing()
    {
        var mlRest = Substitute.For<IMLRest>();
        var vm = NewVm(mlRest);   // OnOpened nezavoláno → SelectedPracoviste null

        vm.LoadCommand.Execute(null);

        Assert.Empty(vm.Jizdy);
        mlRest.DidNotReceive().QueryOrThrow(CacheClass, "GetJizdy", Arg.Any<CVar>());
    }
}
