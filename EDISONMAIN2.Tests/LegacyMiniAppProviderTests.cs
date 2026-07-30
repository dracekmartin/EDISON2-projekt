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

    [Fact]
    public void Sample_IsDebugOnly()
    {
        var ids = new LegacyMiniAppProvider().GetMiniApps().Select(d => d.Id).ToList();

#if DEBUG
        Assert.Contains("Mhd.Sample", ids);
#else
        Assert.DoesNotContain("Mhd.Sample", ids);
#endif
    }
}
