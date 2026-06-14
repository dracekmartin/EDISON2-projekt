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
    public void Logout_Null_NoThrow()
    {
        SessionLogout.Logout(null);
    }

    [Fact]
    public void Logout_QueryThrows_Swallowed()
    {
        var mlRest = MLRestMocks.Create();
        var clone = MLRestMocks.Create();
        mlRest.With(cacheLocks: 0, idfa: null, dataNamespace: null).Returns(clone);
        clone.QueryOrThrow("ALVA.SYS.Login", "Logout", Arg.Any<object[]>())
             .Returns(_ => throw new InvalidOperationException("boom"));

        SessionLogout.Logout(mlRest);   // best-effort — výjimka se spolkne
    }
}
