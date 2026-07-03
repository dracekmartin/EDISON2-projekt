namespace EDControls.Tests;

public class EdisonLaunchArgsTests
{
    private static string[] ValidArgs(params string[] extra) =>
    [
        "EDISONMAIN2", "192.168.16.1", "57770", "CB_DP", "CB_DP",
        "jan.novak", "Jan Novák", "167", "ALVA.TISKJR.formTISKJRSeznamSestav", "guid-123",
        .. extra
    ];

    [Fact]
    public void TryParse_ValidArgs_MapsAllFields()
    {
        var launch = EdisonLaunchArgs.TryParse(ValidArgs());

        Assert.NotNull(launch);
        Assert.Equal("192.168.16.1", launch.Server);
        Assert.Equal(57770, launch.RestPort);
        Assert.Equal("CB_DP", launch.Namespace);
        Assert.Equal("CB_DP", launch.Idfa);
        Assert.Equal("jan.novak", launch.LoginName);
        Assert.Equal("Jan Novák", launch.FullName);
        Assert.Equal("167", launch.Mlp);
        Assert.Equal("ALVA.TISKJR.formTISKJRSeznamSestav", launch.FormName);
        Assert.Equal("guid-123", launch.ShellGuid);
        Assert.Equal("", launch.InputParam);
    }

    [Fact]
    public void TryParse_OptionalInputParam_IsRead()
    {
        var launch = EdisonLaunchArgs.TryParse(ValidArgs("AdminMode=1"));

        Assert.NotNull(launch);
        Assert.Equal("AdminMode=1", launch.InputParam);
    }

    [Fact]
    public void TryParse_MissingSentinel_ReturnsNull()
    {
        var args = ValidArgs();
        args[0] = "NECO_JINEHO";

        Assert.Null(EdisonLaunchArgs.TryParse(args));
    }

    [Fact]
    public void TryParse_EmptyArgs_ReturnsNull()
    {
        Assert.Null(EdisonLaunchArgs.TryParse([]));
    }

    [Fact]
    public void TryParse_TooFewArgs_ReturnsNull()
    {
        // Sentinel + 8 hodnot = 9 argů — kontrakt vyžaduje minimálně 10.
        var args = ValidArgs()[..9];

        Assert.Null(EdisonLaunchArgs.TryParse(args));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-1")]
    public void TryParse_InvalidPort_ReturnsNull(string port)
    {
        var args = ValidArgs();
        args[2] = port;

        Assert.Null(EdisonLaunchArgs.TryParse(args));
    }

    [Theory]
    [InlineData("")]
    [InlineData("xyz")]
    public void TryParse_InvalidMlp_ReturnsNull(string mlp)
    {
        var args = ValidArgs();
        args[7] = mlp;

        Assert.Null(EdisonLaunchArgs.TryParse(args));
    }

    [Fact]
    public void TryParse_EmptyServer_ReturnsNull()
    {
        var args = ValidArgs();
        args[1] = "  ";

        Assert.Null(EdisonLaunchArgs.TryParse(args));
    }

    [Fact]
    public void IsEdisonLaunch_DetectsSentinelEvenWhenArgsIncomplete()
    {
        // Rozliší "spuštěno ze shellu s vadnými argy" (→ varování) od čistého standalone.
        Assert.True(EdisonLaunchArgs.IsEdisonLaunch(["EDISONMAIN2"]));
        Assert.False(EdisonLaunchArgs.IsEdisonLaunch([]));
        Assert.False(EdisonLaunchArgs.IsEdisonLaunch(["TemplateDesigner"]));
    }
}
