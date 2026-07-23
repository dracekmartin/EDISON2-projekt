using EDISONMAIN2.MiniApps;

namespace EDISONMAIN2.Tests;

public class CoreMiniAppsTests
{
    [Fact]
    public void Provides_WebWidgetsAndAutoRefresh()
    {
        // Factory se neinvokují (žádný WebView2/STA, žádné EDServices.Current) — jen registrace.
        var apps = new CoreMiniApps().GetMiniApps().ToList();

        Assert.Equal("Podpora M-line", new CoreMiniApps().GroupTitle);
        Assert.Equal(5, apps.Count);
        Assert.Contains(apps, a => a.Id == "Core.Podpora" && a.Title == "Podpora M-line");
        Assert.Contains(apps, a => a.Id == "Core.Novinky" && a.Title == "Novinky");
        Assert.Contains(apps, a => a.Id == "Core.Wiki" && a.Title == "Wiki");
        Assert.Contains(apps, a => a.Id == "Core.Skoleni" && a.Title == "Školení");
        Assert.Contains(apps, a => a.Id == "Core.AutoRefresh" && a.Title == "Automatický refresh");
    }

    [Fact]
    public void OnlyCarousel_IsPinned()
    {
        // Karusel = garantovaný komunikační kanál; ostatní widgety musí zůstat zavíratelné.
        var apps = new CoreMiniApps().GetMiniApps().ToList();

        Assert.True(apps.Single(a => a.Id == "Core.Podpora").Pinned);
        Assert.All(apps.Where(a => a.Id != "Core.Podpora"), a => Assert.False(a.Pinned));
    }
}
