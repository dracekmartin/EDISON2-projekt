using EDISONMAIN2.MiniApps;

namespace EDISONMAIN2.Tests;

public class CoreMiniAppsTests
{
    [Fact]
    public void Provides_Nastenka()
    {
        // Factory se neinvokuje (žádný WebView2/STA) — kontrolujeme jen registraci descriptoru.
        var nastenka = Assert.Single(new CoreMiniApps().GetMiniApps());

        Assert.Equal("Core.Nastenka", nastenka.Id);
        Assert.Equal("Nástěnka", nastenka.Title);
    }
}
