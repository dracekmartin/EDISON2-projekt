using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.LegacyMiniApps;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>Testy provideru 15 legacy dashboardových miniaplikací (id + titulek + factory).</summary>
[Collection(SharedStaticStateCollection.Name)]
public class LegacyDashboardMiniAppProviderTests
{
    [Fact]
    public void GetMiniApps_Returns15WithUniqueIds()
    {
        var apps = new LegacyDashboardMiniAppProvider().GetMiniApps().ToList();

        Assert.Equal(15, apps.Count);
        Assert.Equal(15, apps.Select(a => a.Id).Distinct().Count());
        Assert.All(apps, a => Assert.StartsWith("Legacy.520", a.Id));
        Assert.All(apps, a => Assert.False(string.IsNullOrWhiteSpace(a.Title)));
    }

    [Fact]
    public void GetMiniApps_FactoryBuildsLegacyDashboardViewModel()
    {
        var descriptor = new LegacyDashboardMiniAppProvider().GetMiniApps()
            .Single(a => a.Id == "Legacy.52005");

        Assert.Equal("Aktuální období", descriptor.Title);

        // Factory volá bezparametrický base ctor → čte EDServices.Current.
        var svc = new EDServices(Substitute.For<IMLRest>(), Substitute.For<IWindowNavigator>(),
            Substitute.For<IPopupDisplayer>());
        EDServices.Initialize(svc);
        try
        {
            var vm = descriptor.CreateViewModel();
            Assert.IsType<LegacyDashboardMiniAppViewModel>(vm);
            Assert.Equal("Aktuální období", vm.Title);
        }
        finally
        {
            EDServices.Reset();
        }
    }
}
