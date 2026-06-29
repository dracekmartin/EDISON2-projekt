using EDISONMAIN2.MiniApps;

namespace EDISONMAIN2.Tests;

public class CoreMiniAppsTests
{
    [Fact]
    public void Provides_NastenkaAndAutoRefresh()
    {
        // Factory se neinvokují (žádný WebView2/STA, žádné EDServices.Current) — jen registrace.
        var apps = new CoreMiniApps().GetMiniApps().ToList();

        Assert.Equal(2, apps.Count);
        Assert.Contains(apps, a => a.Id == "Core.Nastenka" && a.Title == "Nástěnka");
        Assert.Contains(apps, a => a.Id == "Core.AutoRefresh" && a.Title == "Automatický refresh");
    }
}
