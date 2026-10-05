using EDControls;
using EDISONMAIN2.Windows.EdisonMainWindow;
using EDTesting;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Hlídání, zda výsledek práce na pozadí patří session, ve které začala: přestavba (i obnova licence)
/// zneplatní rozběhnutou práci, ale tutéž session (uživatel + firma) nemění.
/// </summary>
public class SessionEpochTests
{
    private IMLRest _rest = MLRestMocks.Create();
    private string _idfa = "CB_DP";

    private SessionEpoch Epoch() => new(() => _rest, () => _idfa);

    [Fact]
    public void WithoutChange_IsCurrentAndSameSession()
    {
        var epoch = Epoch();

        Assert.True(epoch.IsCurrent(epoch.Capture()));
        Assert.True(epoch.IsSameSession(epoch.CaptureSession()));
    }

    [Fact]
    public void Invalidate_MakesStampStale_ButKeepsSameSession()
    {
        // Obnova licence přestaví stav (počty v chipu jsou cizí), seznam povolených firem ale platí dál.
        var epoch = Epoch();
        var stamp = epoch.Capture();
        var session = epoch.CaptureSession();

        epoch.Invalidate();

        Assert.False(epoch.IsCurrent(stamp));
        Assert.True(epoch.IsSameSession(session));
    }

    [Fact]
    public void UserSwitch_NewMLRestInstance_IsAnotherSession()
    {
        var epoch = Epoch();
        var session = epoch.CaptureSession();

        _rest = MLRestMocks.Create();   // SessionFactory.Establish dává novou instanci i pro tutéž firmu

        Assert.False(epoch.IsSameSession(session));
    }

    [Fact]
    public void CompanySwitch_OtherIdfa_IsAnotherSession()
    {
        var epoch = Epoch();
        var session = epoch.CaptureSession();

        _idfa = "JI_DP";

        Assert.False(epoch.IsSameSession(session));
    }

    [Fact]
    public void Capture_DoesNotReadSession()
    {
        // Práce hlídaná jen generací (chip, sync) nesmí sahat na firmu - index firmy může být uprostřed výměny.
        var epoch = new SessionEpoch(() => throw new InvalidOperationException("rest"), () => throw new InvalidOperationException("idfa"));

        var ex = Record.Exception(() => epoch.IsCurrent(epoch.Capture()));

        Assert.Null(ex);
    }
}
