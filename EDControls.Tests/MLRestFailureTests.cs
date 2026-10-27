using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using ALWA.SYS;
using EDTesting;

namespace EDControls.Tests;

/// <summary>
/// Pozná výpadek spojení a konec session z textu legacy MLRestu. Texty z <see cref="MLRestErrors"/>
/// drží tvar <c>MakeRequest</c>, šev se skutečným MLRestem ověří, že ho knihovna pořád skládá stejně.
/// </summary>
public class MLRestFailureTests
{
    public static TheoryData<Exception> TransportFailures() => new()
    {
        new Exception(MLRestErrors.Transport()),
        new Exception(MLRestErrors.Transport("NameResolutionFailure", "Není znám žádný takový hostitel. (cloud2:57772)")),
        new Exception(MLRestErrors.Transport("UnknownError",
            "Pokus o připojení selhal, protože připojená strana v časovém intervalu řádně neodpověděla. [::ffff:185.111.99.173]:56662 (public.m-line.cz:56662)")),
        new InvalidOperationException(MLRestErrors.LegacyWrapped(MLRestErrors.Transport())),
        new AggregateException(new Exception(MLRestErrors.Transport())),
        new TargetInvocationException(new Exception(MLRestErrors.Transport())),
        // Legacy MLRest bere 503 jako výpadek spojení (EdisonConnectionFailed), stránka IIS je výpadek za IIS.
        new Exception(MLRestErrors.StatusWithoutBody()),
        new Exception(MLRestErrors.IisErrorPage()),
    };

    public static TheoryData<Exception?> NotTransport() => new()
    {
        new Exception(MLRestErrors.SessionEnd()),
        new Exception(MLRestErrors.ServerError("ERROR #5001: Chyba v metodě")),
        new Exception(MLRestErrors.StatusWithoutBody("NotFound", "(404) Not Found")),
        new InvalidOperationException("Testovací výjimka z toolbaru."),
        null,
    };

    [Fact]
    public void FailedEndpoint_FromConnectMessage_OtherwiseNull()
    {
        Assert.Equal("cloud2:57772", MLRestFailure.FailedEndpoint(
            new Exception(MLRestErrors.Transport("NameResolutionFailure", "Není znám žádný takový hostitel. (cloud2:57772)"))));
        Assert.Equal("public.m-line.cz:56662", MLRestFailure.FailedEndpoint(new Exception(MLRestErrors.Transport("UnknownError",
            "Pokus o připojení selhal. [::ffff:185.111.99.173]:56662 (public.m-line.cz:56662)"))));
        Assert.Null(MLRestFailure.FailedEndpoint(new Exception(MLRestErrors.Transport())));
    }

    [Theory]
    [MemberData(nameof(TransportFailures))]
    public void IsTransport_ConnectionFailedWithoutResponse(Exception ex) =>
        Assert.True(MLRestFailure.IsTransport(ex));

    [Theory]
    [MemberData(nameof(NotTransport))]
    public void IsTransport_ServerResponseOrOtherError_False(Exception? ex) =>
        Assert.False(MLRestFailure.IsTransport(ex));

    [Fact]
    public void IsSessionEnd_LockLostOrVersionMismatch()
    {
        Assert.True(MLRestFailure.IsSessionEnd(new Exception(MLRestErrors.SessionEnd())));
        Assert.True(MLRestFailure.IsSessionEnd(new Exception(MLRestErrors.VersionMismatch())));
        Assert.True(MLRestFailure.IsSessionEnd(new AggregateException(new Exception(MLRestErrors.SessionEnd()))));
    }

    [Fact]
    public void IsSessionEnd_LockSetAtLoginOrTransport_False()
    {
        // Lock set failed! vzniká ve VerifyLic při přihlášení, konec běžící session to není.
        Assert.False(MLRestFailure.IsSessionEnd(new Exception(MLRestErrors.ServerError(
            "ERROR #5001: ACTIVITY: Lock set failed! IDFA [CB_DP] / IP [10.0.0.1] / GUID [x]!"))));
        Assert.False(MLRestFailure.IsSessionEnd(new Exception(MLRestErrors.Transport())));
        Assert.False(MLRestFailure.IsSessionEnd(null));
    }

    [Fact]
    public void Seam_RealMLRestOnClosedPort_IsTransport()
    {
        // Skutečný MLRest proti zavřenému portu: hlídá, že knihovna text pořád skládá s [WebException.Status].
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var adapter = new MLRestAdapter(new MLRest("127.0.0.1", port, "MLINE", "CB_DP"));

        var ex = Record.Exception(() => adapter.QueryOrThrow("ALVA.EDISON.Main", "Ping", new object[] { "" }));

        Assert.NotNull(ex);
        Assert.True(MLRestFailure.IsTransport(ex), ex.Message);
        Assert.False(MLRestFailure.IsSessionEnd(ex));
    }
}
