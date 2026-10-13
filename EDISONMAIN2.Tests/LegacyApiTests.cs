using System.Reflection;
using ALWA.SYS;
using EDControls;
using EDTesting;

namespace EDISONMAIN2.Tests;

/// <summary>Hranice k legacy DLL: konkrétní MLRest jen z adaptéru session, jiná implementace skončí čitelnou výjimkou.</summary>
public class LegacyApiTests
{
    public static TheoryData<Action<IMLRest>> LegacyCalls => new()
    {
        rest => LegacyApi.CompanyList(rest, checkAccess: true),
        rest => LegacyApi.ActiveLicenceLoader(rest, _ => { }),
        rest => LegacyApi.SyncFromMline(rest),
        rest => LegacyApi.OnSessionEnd(rest, () => { }, () => { }),
    };

    [Theory]
    [MemberData(nameof(LegacyCalls))]
    public void LegacyCall_WithoutSessionAdapter_ThrowsNamingTheConnection(Action<IMLRest> invoke)
    {
        // Mock místo session nesmí skončit InvalidCastException ani tiše nenapojeným force-logoutem.
        var ex = Assert.Throws<InvalidOperationException>(() => invoke(MLRestMocks.Create()));

        Assert.Contains("MLRestAdapter", ex.Message);
    }

    [Theory]
    [InlineData("OnEdisonActivityFailed", "ACTIVITY: Lock exist failed!", 1, 0)]
    [InlineData("OnEdisonVersionFailed", "EdisonVersionFailed!", 0, 1)]
    public void OnSessionEnd_ServerEndsSession_CallsMatchingHandler(string serverEvent, string serverError,
                                                                    int activityLost, int versionMismatch)
    {
        var inner = new MLRest("test-host", 9999, "", "") { CacheLocks = 1 };
        int activity = 0, version = 0;

        LegacyApi.OnSessionEnd(new MLRestAdapter(inner), () => activity++, () => version++);
        // MLRest událost zvenku vyvolat nedovolí, serverovou chybu hlásí privátní metoda.
        typeof(MLRest).GetMethod(serverEvent, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(inner, [serverError]);

        Assert.Equal((activityLost, versionMismatch), (activity, version));
    }
}
