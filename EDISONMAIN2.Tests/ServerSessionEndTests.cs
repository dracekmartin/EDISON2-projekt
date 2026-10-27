using System.Linq;
using System.Reflection;
using ALWA.SYS;
using EDControls;
using EDControls.EDViewModel;
using EDTesting;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Konec session ze serveru: každý další request mrtvé session vyvolá tutéž událost, hláška a ukončení
/// ale proběhnou jednou a session hned přejde na CacheLocks=0 (zavírací zápisy už zámek nekontrolují).
/// </summary>
public class ServerSessionEndTests
{
    private readonly FakeStartupHost _host = new();
    private readonly IPopupDisplayer _popups = Substitute.For<IPopupDisplayer>();
    private readonly ServerSessionEnd _end;

    public ServerSessionEndTests()
    {
        _end = new ServerSessionEnd(_host, new ShellShutdown(_host.Shutdown), () => _popups);
    }

    [Fact]
    public void End_Twice_InformsAndShutsDownOnce()
    {
        var session = MLRestMocks.Create();

        _end.End(session, ServerSessionEnd.ActivityLostMessage);
        _end.End(session, ServerSessionEnd.ActivityLostMessage);

        _popups.Received(1).Info(ServerSessionEnd.ActivityLostMessage);
        Assert.Single(_host.Calls, c => c == "shutdown");
    }

    [Fact]
    public void End_SwitchesSessionToNoLocksBeforeTheMessage()
    {
        var session = MLRestMocks.Create();
        session.CacheLocks = 1;
        int locksDuringMessage = -1;
        _popups.When(p => p.Info(Arg.Any<string>())).Do(_ => locksDuringMessage = session.CacheLocks);

        _end.End(session, ServerSessionEnd.ActivityLostMessage);

        Assert.Equal(0, locksDuringMessage);
    }

    [Fact]
    public void End_AfterShutdownStarted_NoMessageButNoLocks()
    {
        // Uživatel shell zavřel sám a flush při ukončení narazil na ztracený zámek: hláška by přišla po zavření.
        var session = MLRestMocks.Create();
        session.CacheLocks = 1;
        _host.Shutdown();

        _end.End(session, ServerSessionEnd.ActivityLostMessage);

        _popups.DidNotReceiveWithAnyArgs().Info(default!);
        Assert.Equal(0, session.CacheLocks);
        Assert.Single(_host.Calls, c => c == "shutdown");
    }

    [Fact]
    public void End_MessageFails_StillShutsDown()
    {
        // Druhá šance nepřijde (příznak, Locks=0): bez ukončení by shell běžel nad mrtvou session.
        _popups.When(p => p.Info(Arg.Any<string>(), Arg.Any<System.Windows.Window?>()))
               .Do(_ => throw new InvalidOperationException("Dispatcher processing has been suspended"));

        Assert.ThrowsAny<InvalidOperationException>(() => _end.End(MLRestMocks.Create(), ServerSessionEnd.ActivityLostMessage));

        Assert.Single(_host.Calls, c => c == "shutdown");
    }

    [Fact]
    public void End_VersionThenActivity_OnlyVersionMessage()
    {
        var session = MLRestMocks.Create();

        _end.End(session, ServerSessionEnd.VersionMismatchMessage);
        _end.End(session, ServerSessionEnd.ActivityLostMessage);

        _popups.Received(1).Info(ServerSessionEnd.VersionMismatchMessage);
        _popups.DidNotReceive().Info(ServerSessionEnd.ActivityLostMessage);
    }

    [Fact]
    public void Wire_RealSessionRaisesLockLostTwice_OneMessageAndNoLocks()
    {
        var inner = new MLRest("test-host", 9999, "", "") { CacheLocks = 1 };
        _end.Wire(new MLRestAdapter(inner));
        var raise = typeof(MLRest).GetMethod("OnEdisonActivityFailed", BindingFlags.Instance | BindingFlags.NonPublic)!;

        raise.Invoke(inner, ["ACTIVITY: Lock exist failed!"]);
        raise.Invoke(inner, ["ACTIVITY: Lock exist failed!"]);

        _popups.Received(1).Info(ServerSessionEnd.ActivityLostMessage);
        Assert.Equal(0, inner.CacheLocks);
    }
}
