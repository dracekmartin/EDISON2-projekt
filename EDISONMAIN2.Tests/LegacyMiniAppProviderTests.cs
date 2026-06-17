using EDISONMAIN2.LegacyMiniApps;

namespace EDISONMAIN2.Tests;

public class LegacyMiniAppProviderTests
{
    [Fact]
    public void Provides_Dispecink()
    {
        // Factory se neinvokuje (žádné STA/WPF) — kontrolujeme registraci descriptoru.
        var ids = new LegacyMiniAppProvider().GetMiniApps().Select(d => d.Id).ToList();

        Assert.Contains("Mhd.Dispecink", ids);
    }
}
