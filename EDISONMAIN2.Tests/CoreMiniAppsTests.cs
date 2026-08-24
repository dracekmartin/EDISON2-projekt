using EDISONMAIN2.MiniApps;

namespace EDISONMAIN2.Tests;

public class CoreMiniAppsTests
{
    [Fact]
    public void ProvidesOnlyAutoRefresh()
    {
        // Widgety podpora.m-line.cz už tu nejsou - kanál nese fixní pruh v shellu, mimo dock.
        // Factory se neinvokuje (žádný WebView2/STA, žádné EDServices.Current) - jen registrace.
        var provider = new CoreMiniApps();

        var app = Assert.Single(provider.GetMiniApps());

        Assert.Equal("Nástroje", provider.GroupTitle);
        // Id zůstává i po přejmenování titulku - je v persistenci layoutu.
        Assert.Equal("Core.AutoRefresh", app.Id);
        Assert.Equal("Automatická obnova", app.Title);
    }
}
