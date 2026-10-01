using EDControls.EDViewModel;
using EDControls.ErrorReporting;
using EDISONMAIN2.MiniApps;

namespace EDISONMAIN2.Tests;

public class TestRunOptionsTests
{
    [Theory]
    [InlineData("e2e=1")]
    [InlineData("E2E=1")]
    public void Parse_TokenAmongConnectionArgs_ReturnsOptions(string token)
    {
        Assert.NotNull(TestRunOptions.Parse(["server=host", "restport=57770", token]));
    }

    [Fact]
    public void Parse_WithoutToken_ReturnsNull()
    {
        Assert.Null(TestRunOptions.Parse(["server=host", "restport=57770", "e2e=0", "idfa=CB_DP"]));
    }

    [Fact]
    public void Parse_IdfaToken_SetsStartCompany()
    {
        Assert.Equal("CB_DP", TestRunOptions.Parse(["e2e=1", "idfa=CB_DP"])!.StartIdfa);
        Assert.Null(TestRunOptions.Parse(["e2e=1"])!.StartIdfa);
    }

    [Fact]
    public void ApplyProcessIsolation_DisablesSideEffectsAndSeparatesSession()
    {
        var reports = ErrorReports.Enabled;
        var telemetry = EDUsageTelemetry.Enabled;
        var tag = SessionFactory.SessionTag;
        var screenshot = Environment.GetEnvironmentVariable(SupportWidgetHost.ScreenshotModeVariable);
        try
        {
            TestRunOptions.Parse(["e2e=1"])!.ApplyProcessIsolation();

            Assert.False(ErrorReports.Enabled);
            Assert.False(EDUsageTelemetry.Enabled);
            Assert.Equal(TestRunOptions.WindowMarker, SessionFactory.SessionTag);
            Assert.True(SupportWidgetHost.ScreenshotMode);
        }
        finally
        {
            ErrorReports.Enabled = reports;
            EDUsageTelemetry.Enabled = telemetry;
            SessionFactory.SessionTag = tag;
            Environment.SetEnvironmentVariable(SupportWidgetHost.ScreenshotModeVariable, screenshot);
        }
    }
}
