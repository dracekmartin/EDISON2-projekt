using ALVA.LICENCE.EDLicence;
using EDControls;
using EDControls.EDViewModel;
using EDTesting;
using NSubstitute;

namespace EDISONMAIN2.Tests;

public class LicenceVerifierTests
{
    private static void SetupVerifyLic(IMLRest mlRest, params int[] results)
    {
        var outputs = new object[results.Length][];
        for (int i = 0; i < results.Length; i++)
            outputs[i] = MLRestMocks.Output(results[i].ToString());
        MLRestMocks.SetupQuerySequence(mlRest, "ALVA.SYS.Login", "VerifyLic", outputs);
    }

    [Fact]
    public void VerifyOk_ReturnsAccepted_WithLicence()
    {
        var mlRest = MLRestMocks.Create();
        SetupVerifyLic(mlRest, 1);
        var licence = new Lic { CountsLOGIN = 5 };

        var (outcome, returned) = LicenceVerifier.TryAcquireSession(
            mlRest, () => licence, isAdmin: false,
            Substitute.For<IPopupDisplayer>(), Substitute.For<IWindowNavigator>());

        Assert.Equal(LicenceAcquireOutcome.Accepted, outcome);
        Assert.Same(licence, returned);
    }

    [Fact]
    public void Accepted_SetsCacheLocksOne()
    {
        // Parita s legacy EDUsers.VerifyLic: po vzniku ACTIVITY zámku session obnovuje
        // idle timer a detekuje zaniklý zámek (bez toho vypadá aktivní shell pro server jako idle).
        var mlRest = MLRestMocks.Create();
        SetupVerifyLic(mlRest, 1);

        LicenceVerifier.TryAcquireSession(
            mlRest, () => new Lic { CountsLOGIN = 5 }, isAdmin: false,
            Substitute.For<IPopupDisplayer>(), Substitute.For<IWindowNavigator>());

        Assert.Equal(1, mlRest.CacheLocks);
    }

    [Fact]
    public void Refused_LeavesCacheLocksUntouched()
    {
        // Bez zámku by LockExist při Locks=1 shodil každý další request.
        var mlRest = MLRestMocks.Create();
        SetupVerifyLic(mlRest, -1);

        LicenceVerifier.TryAcquireSession(
            mlRest, () => new Lic { CountsLOGIN = 5 }, isAdmin: false,
            Substitute.For<IPopupDisplayer>(), Substitute.For<IWindowNavigator>());

        Assert.Equal(0, mlRest.CacheLocks);
    }

    [Fact]
    public void NonAdmin_OverLimit_ReturnsRefused_NoConfirm()
    {
        var mlRest = MLRestMocks.Create();
        SetupVerifyLic(mlRest, -1);
        var popup = Substitute.For<IPopupDisplayer>();

        var (outcome, _) = LicenceVerifier.TryAcquireSession(
            mlRest, () => new Lic { CountsLOGIN = 5 }, isAdmin: false,
            popup, Substitute.For<IWindowNavigator>());

        Assert.Equal(LicenceAcquireOutcome.Refused, outcome);
        popup.DidNotReceive().Confirm(Arg.Any<string>());
    }

    [Fact]
    public void UnknownErrorCode_ReturnsRefused()
    {
        // Server vrátil neznámý non-1, non-(-1) kód → Refused bez retry.
        var mlRest = MLRestMocks.Create();
        SetupVerifyLic(mlRest, 99);
        var nav = Substitute.For<IWindowNavigator>();

        var (outcome, _) = LicenceVerifier.TryAcquireSession(
            mlRest, () => new Lic { CountsLOGIN = 5 }, isAdmin: true,
            Substitute.For<IPopupDisplayer>(), nav);

        Assert.Equal(LicenceAcquireOutcome.Refused, outcome);
        nav.DidNotReceive().ShowLegacyAndWait(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public void AdminOverLimit_ConfirmDenied_ReturnsCancelled()
    {
        var mlRest = MLRestMocks.Create();
        SetupVerifyLic(mlRest, -1);
        var popup = Substitute.For<IPopupDisplayer>();
        popup.Confirm(Arg.Any<string>()).Returns(false);
        var nav = Substitute.For<IWindowNavigator>();

        var (outcome, _) = LicenceVerifier.TryAcquireSession(
            mlRest, () => new Lic { CountsLOGIN = 5 }, isAdmin: true, popup, nav);

        Assert.Equal(LicenceAcquireOutcome.Cancelled, outcome);
        nav.DidNotReceive().ShowLegacyAndWait(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public void AdminRetry_LaunchesUsersExe_WithoutShellSession()
    {
        var mlRest = MLRestMocks.Create();
        SetupVerifyLic(mlRest, -1, 1);
        var popup = Substitute.For<IPopupDisplayer>();
        popup.Confirm(Arg.Any<string>()).Returns(true);
        var nav = Substitute.For<IWindowNavigator>();

        var (outcome, _) = LicenceVerifier.TryAcquireSession(
            mlRest, () => new Lic { CountsLOGIN = 5 }, isAdmin: true, popup, nav);

        Assert.Equal(LicenceAcquireOutcome.Accepted, outcome);
        // shareShellSession=false je kritické - shell ještě nemá ACTIVITY zámek pod aktuální Idfa.
        nav.Received(1).ShowLegacyAndWait("USERS.exe", "ALVA.USERS.formUSERSActivity", shareShellSession: false);
    }

    [Fact]
    public void AdminRetry_MultipleIterations_LaunchesUsersExeEachTime()
    {
        var mlRest = MLRestMocks.Create();
        SetupVerifyLic(mlRest, -1, -1, -1, 1);
        var popup = Substitute.For<IPopupDisplayer>();
        popup.Confirm(Arg.Any<string>()).Returns(true);
        var nav = Substitute.For<IWindowNavigator>();

        var (outcome, _) = LicenceVerifier.TryAcquireSession(
            mlRest, () => new Lic { CountsLOGIN = 5 }, isAdmin: true, popup, nav);

        Assert.Equal(LicenceAcquireOutcome.Accepted, outcome);
        nav.Received(3).ShowLegacyAndWait("USERS.exe", "ALVA.USERS.formUSERSActivity", shareShellSession: false);
    }

    [Fact]
    public void LicenceLoader_CalledFreshEachIteration()
    {
        // Důvod: mezi iteracemi mohl admin odhlásit jiného usera → CountsLOGIN se mohlo zvýšit,
        // chceme čerstvou hodnotu pro další VerifyLic call.
        var mlRest = MLRestMocks.Create();
        SetupVerifyLic(mlRest, -1, 1);
        var popup = Substitute.For<IPopupDisplayer>();
        popup.Confirm(Arg.Any<string>()).Returns(true);
        var loader = Substitute.For<Func<Lic?>>();
        loader.Invoke().Returns(new Lic { CountsLOGIN = 5 });

        LicenceVerifier.TryAcquireSession(
            mlRest, loader, isAdmin: true, popup, Substitute.For<IWindowNavigator>());

        loader.Received(2).Invoke();
    }

    [Fact]
    public void NullLicence_PassesZeroCountsLogin()
    {
        var mlRest = MLRestMocks.Create();
        SetupVerifyLic(mlRest, 1);

        LicenceVerifier.TryAcquireSession(
            mlRest, () => null, isAdmin: false,
            Substitute.For<IPopupDisplayer>(), Substitute.For<IWindowNavigator>());

        mlRest.Received().QueryOrThrow("ALVA.SYS.Login", "VerifyLic",
            Arg.Is<object[]>(a => a.Length == 1 && (int)a[0] == 0));
    }

    [Fact]
    public void LicenceLoaderResult_PassedAsCountsLogin()
    {
        var mlRest = MLRestMocks.Create();
        SetupVerifyLic(mlRest, 1);

        LicenceVerifier.TryAcquireSession(
            mlRest, () => new Lic { CountsLOGIN = 42 }, isAdmin: false,
            Substitute.For<IPopupDisplayer>(), Substitute.For<IWindowNavigator>());

        mlRest.Received().QueryOrThrow("ALVA.SYS.Login", "VerifyLic",
            Arg.Is<object[]>(a => a.Length == 1 && (int)a[0] == 42));
    }
}
