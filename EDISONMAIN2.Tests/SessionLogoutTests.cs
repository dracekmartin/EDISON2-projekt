using EDControls;
using EDTesting;
using NSubstitute;

namespace EDISONMAIN2.Tests;

public class SessionLogoutTests
{
    [Fact]
    public void Logout_CurrentSession_CallsLoginLogoutOnCloneWithCacheLocksZero()
    {
        var mlRest = MLRestMocks.Create();
        var clone = MLRestMocks.Create();
        mlRest.With(cacheLocks: 0, idfa: null, dataNamespace: null).Returns(clone);

        SessionLogout.Logout(mlRest);

        // Logout jde na klon (CacheLocks=0), ne na sdílenou instanci.
        clone.Received(1).QueryOrThrow("ALVA.SYS.Login", "Logout", Arg.Any<object[]>());
        mlRest.DidNotReceive().QueryOrThrow("ALVA.SYS.Login", "Logout", Arg.Any<object[]>());
    }

    /// <summary>Switch-user: dávka na klonu staré session dostane po logoutu zánik zámku. MLRestAdapter ho přepošle
    /// jen, když má instance pořád CacheLocks klonu - proto se session označí ukončenou ještě PŘED requestem.</summary>
    [Fact]
    public void Logout_CurrentSession_MarksSessionEndedBeforeLogoutRequest()
    {
        var mlRest = MLRestMocks.Create();
        mlRest.CacheLocks = 1;
        var clone = MLRestMocks.Create();
        mlRest.With(cacheLocks: 0, idfa: null, dataNamespace: null).Returns(clone);
        int? locksDuringLogout = null;
        clone.QueryOrThrow("ALVA.SYS.Login", "Logout", Arg.Any<object[]>())
             .Returns(_ => { locksDuringLogout = mlRest.CacheLocks; return MLRestMocks.Output(); });

        SessionLogout.Logout(mlRest);

        Assert.Equal(0, locksDuringLogout);
        Assert.Equal(0, mlRest.CacheLocks);
    }

    /// <summary>Klon nevznikl (DNS, konfigurace) = logout neproběhl a session dál žije - musí dál hlídat zámek.</summary>
    [Fact]
    public void Logout_CloneFails_KeepsCacheLocks()
    {
        var mlRest = MLRestMocks.Create();
        mlRest.CacheLocks = 1;
        mlRest.With(cacheLocks: 0, idfa: null, dataNamespace: null).Returns(_ => throw new InvalidOperationException("dns"));

        SessionLogout.Logout(mlRest);

        Assert.Equal(1, mlRest.CacheLocks);
    }

    /// <summary>Company switch: sdílená instance pokračuje pod novou firmou, režim zámku jí řídí CompanySwitcher.</summary>
    [Fact]
    public void Logout_OtherFirm_LeavesSharedInstanceCacheLocks()
    {
        var mlRest = MLRestMocks.Create();
        mlRest.CacheLocks = 1;
        var clone = MLRestMocks.Create();
        mlRest.With(cacheLocks: 0, idfa: "CB_DP", dataNamespace: "CB_DP").Returns(clone);

        SessionLogout.Logout(mlRest, "CB_DP", "CB_DP");

        Assert.Equal(1, mlRest.CacheLocks);
    }

    [Fact]
    public void Logout_WithIdfaAndNamespace_ClonesToTargetFirm()
    {
        var mlRest = MLRestMocks.Create();
        var clone = MLRestMocks.Create();
        mlRest.With(cacheLocks: 0, idfa: "CB_DP", dataNamespace: "CB_DP").Returns(clone);

        SessionLogout.Logout(mlRest, "CB_DP", "CB_DP");

        clone.Received(1).QueryOrThrow("ALVA.SYS.Login", "Logout", Arg.Any<object[]>());
    }

    [Fact]
    public void Logout_ClearsBusinessLocksByGuid()
    {
        var mlRest = MLRestMocks.Create();
        var clone = MLRestMocks.Create();
        clone.Guid.Returns("g-1");
        mlRest.With(cacheLocks: 0, idfa: null, dataNamespace: null).Returns(clone);

        SessionLogout.Logout(mlRest);

        // Parita s legacy EDUsers.Logout: po ACTIVITY logoutu spadnou i business zámky session
        // (pod shell GUID zamykají i killnuté hostované moduly).
        clone.Received(1).QueryOrThrow("ALWA.SYS.Lock", "LockClearExt",
            Arg.Is<object[]>(a => a.Length == 1 && (string)a[0] == "g-1"));
    }

    [Fact]
    public void Logout_ActivityLogoutThrows_LockClearStillRuns()
    {
        // Selhání ACTIVITY logoutu nesmí přeskočit úklid business zámků - ty žádný
        // server-side reaper neuklidí.
        var mlRest = MLRestMocks.Create();
        var clone = MLRestMocks.Create();
        mlRest.With(cacheLocks: 0, idfa: null, dataNamespace: null).Returns(clone);
        clone.QueryOrThrow("ALVA.SYS.Login", "Logout", Arg.Any<object[]>())
             .Returns(_ => throw new InvalidOperationException("boom"));

        SessionLogout.Logout(mlRest);

        clone.Received(1).QueryOrThrow("ALWA.SYS.Lock", "LockClearExt", Arg.Any<object[]>());
    }

    [Fact]
    public void Logout_Null_NoThrow()
    {
        SessionLogout.Logout(null);
    }

    [Fact]
    public void Logout_EverythingThrows_Swallowed()
    {
        // Best-effort až do konce: i když selže ACTIVITY logout i úklid zámků,
        // ven nesmí nic propadnout (blokovalo by zavření/přepnutí).
        var mlRest = MLRestMocks.Create();
        var clone = MLRestMocks.Create();
        mlRest.With(cacheLocks: 0, idfa: null, dataNamespace: null).Returns(clone);
        clone.QueryOrThrow("ALVA.SYS.Login", "Logout", Arg.Any<object[]>())
             .Returns(_ => throw new InvalidOperationException("boom"));
        clone.QueryOrThrow("ALWA.SYS.Lock", "LockClearExt", Arg.Any<object[]>())
             .Returns(_ => throw new InvalidOperationException("boom2"));

        SessionLogout.Logout(mlRest);

        clone.Received(1).QueryOrThrow("ALWA.SYS.Lock", "LockClearExt", Arg.Any<object[]>());
    }
}
