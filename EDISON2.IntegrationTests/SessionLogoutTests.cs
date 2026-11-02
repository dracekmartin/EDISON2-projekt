using ALWA.SYS;
using EDControls;
using EDISONMAIN2;

namespace EDISON2.IntegrationTests;

/// <summary>
/// Odhlášení session: <see cref="SessionLogout"/> selhání polyká, takže jiný sys kontext (firma, IP, GUID) nebo
/// přejmenovaná třída by nechaly ACTIVITY zámek viset až do idle timeoutu (obsazená licence). Zámek zakládá helper
/// pod testovací firmou a vlastním GUIDem, <c>LockClearExt</c> proto nesáhne na cizí session.
/// </summary>
[Trait("Category", "Integration")]
public class SessionLogoutTests
{
    private const string Fixture = "ALVA.EDTesting.ITSession";

    [DevServerFact]
    public void Logout_ShellSession_ReleasesActivityLock()
    {
        var rest = DevServer.Connect(DevServer.WriteIdfa, DevServer.WriteMlp);
        try
        {
            rest.QueryOrThrow(Fixture, "LockSet", new CVar());
            Assert.True(IsLocked(rest), "ITSession.LockSet zámek nezaložil, test by nic neověřil.");

            SessionLogout.Logout(rest);

            Assert.False(IsLocked(rest), "Zámek session po SessionLogout zůstal.");
        }
        finally
        {
            rest.QueryOrThrow(Fixture, "LockDel", new CVar());
        }
    }

    // SessionLogout po Logout volá LockClearExt, který zámek smaže taky: rozbité Logout by bez tohoto testu prošlo.
    [DevServerFact]
    public void SysLoginLogout_WithoutLockClear_ReleasesActivityLock()
    {
        var rest = DevServer.Connect(DevServer.WriteIdfa, DevServer.WriteMlp);
        try
        {
            rest.QueryOrThrow(Fixture, "LockSet", new CVar());
            Assert.True(IsLocked(rest), "ITSession.LockSet zámek nezaložil, test by nic neověřil.");

            rest.QueryOrThrow("ALVA.SYS.Login", "Logout", new object[] { });

            Assert.False(IsLocked(rest), "ALVA.SYS.Login.Logout zámek session neuvolnil.");
        }
        finally
        {
            rest.QueryOrThrow(Fixture, "LockDel", new CVar());
        }
    }

    private static bool IsLocked(IMLRest rest) =>
        rest.QueryOrThrow(Fixture, "LockIs", new CVar())["locked"].Value?.ToString() == "1";
}
