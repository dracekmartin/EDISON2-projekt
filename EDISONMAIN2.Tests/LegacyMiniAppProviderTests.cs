using EDISONMAIN2.LegacyMiniApps;

namespace EDISONMAIN2.Tests;

public class LegacyMiniAppProviderTests
{
    [Fact]
    public void Provides_MhdSample()
    {
        // Factory se neinvokuje (žádné STA/WPF) — kontrolujeme registraci descriptoru.
        var mhd = Assert.Single(new LegacyMiniAppProvider().GetMiniApps());

        Assert.Equal("Mhd.Sample", mhd.Id);
        Assert.Equal("MHD (ukázka)", mhd.Title);
    }
}
