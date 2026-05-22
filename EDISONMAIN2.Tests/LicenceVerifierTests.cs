using ALVA.LICENCE.EDLicence;
using EDControls;
using EDControls.EDViewModel;
using NSubstitute;

namespace EDISONMAIN2.Tests;

public class LicenceVerifierTests
{
    private static IMLRest MockMLRest() => Substitute.For<IMLRest>();

    private static void SetupVerifyLic(IMLRest mlRest, params int[] results)
    {
        var returns = new object[results.Length][];
        for (int i = 0; i < results.Length; i++)
            returns[i] = new object[] { "", results[i].ToString() };

        if (returns.Length == 1)
            mlRest.QueryOrThrow("ALVA.SYS.Login", "VerifyLic", Arg.Any<object[]>())
                  .Returns(returns[0]);
        else
            mlRest.QueryOrThrow("ALVA.SYS.Login", "VerifyLic", Arg.Any<object[]>())
                  .Returns(returns[0], returns[1..]);
    }

    [Fact]
    public void VerifyOk_ReturnsAccepted_WithLicence()
    {
        var mlRest = MockMLRest();
        SetupVerifyLic(mlRest, 1);
        var licence = new Lic { CountsLOGIN = 5 };

        var (outcome, returned) = LicenceVerifier.TryAcquireSession(
            mlRest, () => licence, isAdmin: false,
            Substitute.For<IPopupDisplayer>(), Substitute.For<IWindowNavigator>());

        Assert.Equal(LicenceAcquireOutcome.Accepted, outcome);
        Assert.Same(licence, returned);
    }

    [Fact]
    public void NonAdmin_OverLimit_ReturnsRefused_NoConfirm()
    {
        var mlRest = MockMLRest();
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
        var mlRest = MockMLRest();
        SetupVerifyLic(mlRest, 99);
        var nav = Substitute.For<IWindowNavigator>();

        var (outcome, _) = LicenceVerifier.TryAcquireSession(
            mlRest, () => new Lic { CountsLOGIN = 5 }, isAdmin: true,
            Substitute.For<IPopupDisplayer>(), nav);

        Assert.Equal(LicenceAcquireOutcome.Refused, outcome);
        nav.DidNotReceive().ShowLegacyAndWait(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public void UnknownErrorCode_NonAdmin_ReturnsRefused()
    {
        var mlRest = MockMLRest();
        SetupVerifyLic(mlRest, 99);

        var (outcome, _) = LicenceVerifier.TryAcquireSession(
            mlRest, () => new Lic { CountsLOGIN = 5 }, isAdmin: false,
            Substitute.For<IPopupDisplayer>(), Substitute.For<IWindowNavigator>());

        Assert.Equal(LicenceAcquireOutcome.Refused, outcome);
    }

    [Fact]
    public void AdminOverLimit_ConfirmDenied_ReturnsCancelled()
    {
        var mlRest = MockMLRest();
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
        var mlRest = MockMLRest();
        SetupVerifyLic(mlRest, -1, 1);
        var popup = Substitute.For<IPopupDisplayer>();
        popup.Confirm(Arg.Any<string>()).Returns(true);
        var nav = Substitute.For<IWindowNavigator>();

        var (outcome, _) = LicenceVerifier.TryAcquireSession(
            mlRest, () => new Lic { CountsLOGIN = 5 }, isAdmin: true, popup, nav);

        Assert.Equal(LicenceAcquireOutcome.Accepted, outcome);
        // shareShellSession=false je kritické — shell ještě nemá ACTIVITY zámek pod aktuální Idfa.
        nav.Received(1).ShowLegacyAndWait("USERS.exe", "ALVA.USERS.formUSERSActivity", shareShellSession: false);
    }

    [Fact]
    public void AdminRetry_MultipleIterations_LaunchesUsersExeEachTime()
    {
        var mlRest = MockMLRest();
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
        var mlRest = MockMLRest();
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
        var mlRest = MockMLRest();
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
        var mlRest = MockMLRest();
        SetupVerifyLic(mlRest, 1);

        LicenceVerifier.TryAcquireSession(
            mlRest, () => new Lic { CountsLOGIN = 42 }, isAdmin: false,
            Substitute.For<IPopupDisplayer>(), Substitute.For<IWindowNavigator>());

        mlRest.Received().QueryOrThrow("ALVA.SYS.Login", "VerifyLic",
            Arg.Is<object[]>(a => a.Length == 1 && (int)a[0] == 42));
    }
}
