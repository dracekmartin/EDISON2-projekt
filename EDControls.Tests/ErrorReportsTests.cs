using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using EDControls.ErrorReporting;

namespace EDControls.Tests;

/// <summary>
/// Vstupní bod hlášení (<see cref="ErrorReportingPipeline"/> za fasádou <see cref="ErrorReports"/>): co se ještě před odesláním zahodí
/// (vypnuto, deduplikace, prázdné doplnění) a čím se liší fatální cesta. Vrstvy pod ním
/// mají vlastní testy - tady jde o rozhodování, ne o tvar payloadu. Odesílání odchytává
/// fake HTTP handler; snímky oken tu nevznikají (bez Application vrací Capture prázdno).
/// </summary>
public class ErrorReportsTests : IDisposable
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly object _sync = new();
        private readonly List<string> _bodies = [];

        public int Count { get { lock (_sync) return _bodies.Count; } }
        public List<string> Bodies { get { lock (_sync) return [.. _bodies]; } }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            lock (_sync) _bodies.Add(body);
            return new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("{}") };
        }
    }

    private readonly FakeHandler _handler = new();
    private readonly string _fallbackPath = Path.Combine(Path.GetTempPath(), $"ed-reports-{Guid.NewGuid():N}.log");
    private readonly ErrorReportingPipeline _pipeline;
    private bool _internal;

    public ErrorReportsTests()
    {
        // Vlastní instance bez EDServices a snímků oken: statiku procesu test nemění, běží paralelně.
        _pipeline = new ErrorReportingPipeline(() => new HttpClient(_handler), new ErrorFallbackLog(_fallbackPath),
            () => _internal, () => null, () => []);
    }

    public void Dispose()
    {
        if (File.Exists(_fallbackPath)) File.Delete(_fallbackPath);
    }

    /// <summary>Vyhozená a odchycená výjimka - throttle klíč bere rámce ze stacku.</summary>
    private static Exception Thrown(string message = "boom")
    {
        try { throw new InvalidOperationException(message); }
        catch (Exception ex) { return ex; }
    }

    /// <summary>
    /// Send je fire-and-forget: Task.Run z vlákna threadpoolu jde do lokální fronty téhož vlákna,
    /// takže čekat se musí přes await. Blokující čekání by odeslání pod zátěží nepustilo k běhu.
    /// </summary>
    private async Task WaitForRequests(int expected)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (_handler.Count < expected && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.Equal(expected, _handler.Count);
    }

    /// <summary>Krátká lhůta pro důkaz, že se nic neodeslalo.</summary>
    private Task AssertNothingSent() => AssertNothingMoreSent(0);

    private JsonObject Body(int index) => JsonNode.Parse(_handler.Bodies[index])!.AsObject();

    [Fact]
    public async Task Report_Disabled_ReturnsNullAndSendsNothing()
    {
        _pipeline.Enabled = false;

        Assert.Null(_pipeline.Report(Thrown()));
        await AssertNothingSent();
    }

    [Fact]
    public async Task Report_NullException_ReturnsNull()
    {
        Assert.Null(_pipeline.Report(null!));
        await AssertNothingSent();
    }

    [Fact]
    public async Task ReportBackground_Disabled_SendsNothing()
    {
        _pipeline.Enabled = false;

        _pipeline.ReportBackground(Thrown());

        await AssertNothingSent();
    }

    [Fact]
    public void ReportFatal_Disabled_SendsNothing()
    {
        _pipeline.Enabled = false;

        _pipeline.ReportFatal(Thrown());

        Assert.Equal(0, _handler.Count);
    }

    [Fact]
    public async Task ReportFollowUp_BlankRefIdOrText_SendsNothing()
    {
        _pipeline.ReportFollowUp("", "něco");
        _pipeline.ReportFollowUp("xabc", "   ");

        await AssertNothingSent();
    }

    [Fact]
    public async Task Report_FirstOccurrence_ReturnsRefIdAndSends()
    {
        var refId = _pipeline.Report(Thrown());

        Assert.False(string.IsNullOrWhiteSpace(refId));
        await WaitForRequests(1);
        Assert.Equal(refId, Body(0)["data"]!["refId"]!.GetValue<string>());
    }

    [Fact]
    public async Task Report_SecondIdenticalOccurrence_IsSuppressed()
    {
        // Chyba v OnPaint / timeru se umí opakovat stokrát za minutu; druhý výskyt uvnitř okna
        // se jen počítá a číslo se nevrací (číslo, které nikam nedorazilo, plete podporu).
        var first = _pipeline.Report(Thrown());
        await WaitForRequests(1);

        var second = _pipeline.Report(Thrown());

        Assert.NotNull(first);
        Assert.Null(second);
        await AssertNothingMoreSent(1);
    }

    [Fact]
    public async Task ReportBackground_SecondIdenticalOccurrence_IsSuppressed()
    {
        _pipeline.ReportBackground(Thrown());
        await WaitForRequests(1);

        _pipeline.ReportBackground(Thrown());

        await AssertNothingMoreSent(1);
    }

    [Fact]
    public async Task ReportBackground_SendsErrorWithoutAttachments()
    {
        // Běží na cizím vlákně - snímek okna by tam nefungoval, takže se ani nezkouší.
        _pipeline.ReportBackground(Thrown());

        await WaitForRequests(1);
        var body = Body(0);
        Assert.Equal("error", body["type"]!.GetValue<string>());
        Assert.True(body["attachments"] is null || body["attachments"]!.AsArray().Count == 0);
    }

    [Fact]
    public void ReportFatal_SendsSynchronouslyAndMarksFatal()
    {
        // Po AppDomain.UnhandledException runtime proces vzápětí ukončí - fire-and-forget by
        // se nestihl, takže request musí být odeslaný ještě před návratem z metody.
        _pipeline.ReportFatal(Thrown());

        Assert.Equal(1, _handler.Count);
        Assert.True(Body(0)["data"]!["fatal"]!.GetValue<bool>());
    }

    [Fact]
    public void ReportFatal_IgnoresThrottle()
    {
        // Fatální chyba je poslední, co z běhu uvidíme - deduplikace ji zahodit nesmí.
        _pipeline.ReportFatal(Thrown());
        _pipeline.ReportFatal(Thrown());

        Assert.Equal(2, _handler.Count);
    }

    [Fact]
    public async Task ReportFollowUp_SendsInfoRecordWithSameRefId()
    {
        _pipeline.ReportFollowUp("xabc", "  klikl jsem na Ulozit  ");

        await WaitForRequests(1);
        var body = Body(0);
        Assert.Equal("info", body["type"]!.GetValue<string>());
        Assert.Equal("xabc", body["data"]!["refId"]!.GetValue<string>());
        Assert.Equal("klikl jsem na Ulozit", body["data"]!["userText"]!.GetValue<string>());
    }

    [Fact]
    public async Task ReportFollowUp_NotThrottled()
    {
        // Doplnění posílá uživatel ručně; deduplikace patří automatickým hlášením.
        _pipeline.ReportFollowUp("xabc", "první");
        _pipeline.ReportFollowUp("xabc", "druhé");

        await WaitForRequests(2);
    }

    [Fact]
    public async Task Report_WithoutEDServices_FallsBackToEdison2Source()
    {
        // Hlášení musí odejít i před inicializací EDServices (chyba v bootstrapu).
        _pipeline.Report(Thrown());

        await WaitForRequests(1);
        Assert.Equal("EDISON2", Body(0)["source"]!.GetValue<string>());
    }

    [Fact]
    public async Task Report_InternalMachine_MarksSourceWithSuffix()
    {
        _internal = true;

        _pipeline.Report(Thrown());

        await WaitForRequests(1);
        Assert.EndsWith("-internal", Body(0)["source"]!.GetValue<string>());
    }

    [Fact]
    public async Task ReportLegacyExit_ModuleIdentityAndExitCodeInPayload()
    {
        _pipeline.ReportLegacyExit("USERS.exe", "ALVA.USERS.formUSERSActivity", 3);

        await WaitForRequests(1);
        var body = Body(0);
        // Přehled seskupuje podle module/name - musí to být modul, ne shell.
        Assert.Equal("ALVA.USERS.formUSERSActivity", body["module"]!.GetValue<string>());
        Assert.Equal("USERS.exe", body["name"]!.GetValue<string>());
        Assert.Equal("error", body["type"]!.GetValue<string>());
        Assert.Equal(3, body["data"]!["exitCode"]!.GetValue<int>());
        Assert.Equal("0x00000003", body["data"]!["exitCodeHex"]!.GetValue<string>());
        Assert.True(body["data"]!["legacyHost"]!.GetValue<bool>());
        Assert.Contains("USERS.exe", body["description"]!.GetValue<string>());
    }

    [Fact]
    public async Task ReportLegacyExit_HostDetail_CarriesCauseAndHostStack()
    {
        const string detail = "\r\nSystem.InvalidOperationException: CRASHTEST bg\r\n   at ALVA.USERS.formUSERSActivity.OnShown()";

        _pipeline.ReportLegacyExit("USERS.exe", "ALVA.USERS.formUSERSActivity", 3, detail);

        await WaitForRequests(1);
        var body = Body(0);
        Assert.Contains("Příčina: System.InvalidOperationException: CRASHTEST bg", body["description"]!.GetValue<string>());
        Assert.Contains("CRASHTEST bg", body["data"]!["message"]!.GetValue<string>());
        Assert.Equal(detail, body["data"]!["stack"]!.GetValue<string>());
        Assert.Equal(3, body["data"]!["exitCode"]!.GetValue<int>());
    }

    [Fact]
    public async Task ReportLegacyExit_WithoutHostDetail_KeepsExitCodeOnly()
    {
        _pipeline.ReportLegacyExit("USERS.exe", "ALVA.USERS.formUSERSActivity", 3, "  ");

        await WaitForRequests(1);
        Assert.DoesNotContain("Příčina", Body(0)["description"]!.GetValue<string>());
        Assert.Contains(nameof(LegacyHostCrashedException), Body(0)["data"]!["stack"]!.GetValue<string>());
    }

    [Fact]
    public async Task ReportLegacyExit_SameModuleAndCode_SecondIsSuppressed()
    {
        _pipeline.ReportLegacyExit("USERS.exe", "ALVA.USERS.formUSERSActivity", -532462766);
        _pipeline.ReportLegacyExit("USERS.exe", "ALVA.USERS.formUSERSActivity", -532462766);

        await WaitForRequests(1);
        await AssertNothingMoreSent(1);
    }

    [Fact]
    public async Task ReportLegacyExit_DifferentModules_NotThrottledTogether()
    {
        // Syntetická výjimka nemá stack - klíč musí nést modul, jinak by druhý pád spadl do prvního.
        _pipeline.ReportLegacyExit("USERS.exe", "ALVA.USERS.formUSERSActivity", 3);
        _pipeline.ReportLegacyExit("MZDY2.exe", "ALVA.MZDY2.formMZDY2Zam", 3);

        await WaitForRequests(2);
    }

    [Fact]
    public async Task ReportLegacyExit_Disabled_SendsNothing()
    {
        _pipeline.Enabled = false;

        _pipeline.ReportLegacyExit("USERS.exe", "ALVA.USERS.formUSERSActivity", 3);

        await AssertNothingSent();
    }

    private async Task AssertNothingMoreSent(int expected)
    {
        await Task.Delay(150);
        Assert.Equal(expected, _handler.Count);
    }
}
