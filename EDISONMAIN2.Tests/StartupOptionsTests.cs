using EDControls;
using EDControls.EDViewModel;
using EDControls.ErrorReporting;
using EDISONMAIN2.Modules;
using EDISONMAIN2.SupportStrip;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Režim startu: obě varianty jdou ověřit v jakémkoli buildu, <c>#if DEBUG</c> v App jen vybírá.
/// </summary>
[Collection(SharedStaticStateCollection.Name)]
public class StartupOptionsTests
{
    [Fact]
    public void Development_UsesDevLoginDeterministicGuidAndNoVersion()
    {
        var options = StartupOptions.Development([], devLoginSwitch: null);

        Assert.NotNull(options.DevLogin);
        Assert.Equal("DEV", options.SessionGuidTag);
        Assert.False(options.SendsVersion);
        Assert.True(options.DevMiniApps);
        Assert.True(options.UseDevDefaults);
        Assert.True(options.InternalDiagnosticsEnabled);
        Assert.Null(options.TestRun);
    }

    [Fact]
    public void Development_TestRun_GetsOwnSessionTagAndStartCompany()
    {
        // Logout maže zámky podle GUID - E2E instance nesmí sdílet GUID s vývojářovým shellem.
        var options = StartupOptions.Development(["server=host", "e2e=1", "idfa=CB_DP"], devLoginSwitch: null);

        Assert.Equal(TestRunOptions.WindowMarker, options.SessionGuidTag);
        Assert.Equal("CB_DP", options.TestRun?.StartIdfa);
        Assert.NotNull(options.DevLogin);   // identitu E2E dává DevLogin
    }

    [Theory]
    [InlineData("0", false)]
    [InlineData("1", true)]
    [InlineData("", true)]
    public void Development_DevLoginSwitchZero_LeavesLoginToDialog(string value, bool devLogin)
    {
        var options = StartupOptions.Development([], value);

        Assert.Equal(devLogin, options.DevLogin is not null);
        Assert.Equal("DEV", options.SessionGuidTag);   // zbytek vývojového režimu zůstává
    }

    [Fact]
    public void Development_PreShellNotice_OnlyForTestRunWithSwitch()
    {
        Assert.Equal(TestRunOptions.PreShellPopupText,
            StartupOptions.Development(["e2e=1", "preshellpopup=1"], devLoginSwitch: null).PreShellNotice);
        Assert.Null(StartupOptions.Development(["e2e=1"], devLoginSwitch: null).PreShellNotice);
        Assert.Null(StartupOptions.Development(["preshellpopup=1"], devLoginSwitch: null).PreShellNotice);
        Assert.Null(StartupOptions.Production().PreShellNotice);
    }

    [Fact]
    public void Development_TestRun_IgnoresDevLoginSwitch()
    {
        // Harness předává shellu prostředí terminálu; login okno by E2E shodilo na „nečekané okno".
        var options = StartupOptions.Development(["e2e=1"], devLoginSwitch: "0");

        Assert.NotNull(options.DevLogin);
    }

    [Fact]
    public void Development_TestRunWithRealLogin_LeavesLoginToDialog()
    {
        var options = StartupOptions.Development(["e2e=1", "login=1"], devLoginSwitch: null);

        Assert.Null(options.DevLogin);
        Assert.Equal(TestRunOptions.WindowMarker, options.SessionGuidTag);   // pořád E2E běh s vlastním GUID
        // Bez e2e=1 token nic neznamená: běžný vývojový start s DevLogin.
        Assert.NotNull(StartupOptions.Development(["login=1"], devLoginSwitch: null).DevLogin);
    }

    [Fact]
    public void Production_HasNoDevBehaviour()
    {
        var options = StartupOptions.Production();

        Assert.Null(options.DevLogin);
        Assert.Null(options.SessionGuidTag);
        Assert.True(options.SendsVersion);
        Assert.False(options.DevMiniApps);
        Assert.False(options.UseDevDefaults);
        Assert.Null(options.TestRun);
        Assert.Equal(ModuleTreeStation.IsMlineDomainStation, options.InternalDiagnosticsEnabled);
    }

    [Fact]
    public void Default_IsReleaseBehaviour()
    {
        // Zapomenutá volba nesmí z Release udělat dev build.
        Assert.Equal(StartupOptions.Production() with { InternalDiagnosticsEnabled = false }, new StartupOptions());
    }

    [Fact]
    public void ApplyToProcess_TestRun_DisablesSideEffects()
    {
        using var _ = new ProcessStatics();

        StartupOptions.Development(["e2e=1"], devLoginSwitch: null).ApplyToProcess();

        Assert.False(ErrorReports.Enabled);
        Assert.False(EDUsageTelemetry.Enabled);
        Assert.True(SupportWidgetHost.ScreenshotMode);
        Assert.True(InternalDiagnostics.Enabled);
        Assert.Equal(TestRunOptions.CommonFolder, MlineCommonPaths.Redirected);
    }

    [Fact]
    public void ApplyToProcess_Production_KeepsReportingAndSetsReleaseStatics()
    {
        using var _ = new ProcessStatics();
        ErrorReports.Enabled = true;
        EDUsageTelemetry.Enabled = true;
        InternalDiagnostics.Enabled = true;

        (StartupOptions.Production() with { InternalDiagnosticsEnabled = false }).ApplyToProcess();

        Assert.True(ErrorReports.Enabled);
        Assert.True(EDUsageTelemetry.Enabled);
        Assert.False(InternalDiagnostics.Enabled);
        // Zákazníkům se téma a písmo čtou dál z %APPDATA%\m-line\common.
        Assert.Null(MlineCommonPaths.Redirected);
    }

    /// <summary>Snímek procesových statik, které <see cref="StartupOptions.ApplyToProcess"/> přepisuje.</summary>
    private sealed class ProcessStatics : IDisposable
    {
        private readonly bool _reports = ErrorReports.Enabled;
        private readonly bool _telemetry = EDUsageTelemetry.Enabled;
        private readonly bool _diagnostics = InternalDiagnostics.Enabled;
        private readonly string? _screenshot = Environment.GetEnvironmentVariable(SupportWidgetHost.ScreenshotModeVariable);
        private readonly string? _commonFolder = MlineCommonPaths.Redirected;
        private readonly bool _commonHandedOut = MlineCommonPaths.HandedOut;

        // Jiné testy procesu už sdílenou složku použily, přesměrování by jinak odmítlo.
        public ProcessStatics() => MlineCommonPaths.Restore(redirected: null, handedOut: false);

        public void Dispose()
        {
            ErrorReports.Enabled = _reports;
            EDUsageTelemetry.Enabled = _telemetry;
            InternalDiagnostics.Enabled = _diagnostics;
            Environment.SetEnvironmentVariable(SupportWidgetHost.ScreenshotModeVariable, _screenshot);
            MlineCommonPaths.Restore(_commonFolder, _commonHandedOut);
        }
    }
}
