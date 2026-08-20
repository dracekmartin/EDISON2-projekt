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
/// Vstupní bod hlášení (<see cref="ErrorReports"/>): co se ještě před odesláním zahodí
/// (vypnuto, deduplikace, prázdné doplnění) a čím se liší fatální cesta. Vrstvy pod ním
/// mají vlastní testy - tady jde o rozhodování, ne o tvar payloadu. Odesílání odchytává
/// fake HTTP handler; snímky oken tu nevznikají (bez Application vrací Capture prázdno).
/// </summary>
[Collection(SharedStaticStateCollection.Name)]
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

    public ErrorReportsTests()
    {
        ErrorReporter.ClientOverride = new HttpClient(_handler);
        ErrorFallbackLog.PathOverride = _fallbackPath;
        MlineEnvironment.OverrideForTests(false);
        ErrorReports.Throttle = new ErrorThrottle();   // statický stav by prosakoval mezi testy
        ErrorReports.Enabled = true;
    }

    public void Dispose()
    {
        ErrorReporter.ClientOverride = null;
        ErrorFallbackLog.PathOverride = null;
        MlineEnvironment.OverrideForTests(null);
        ErrorReports.Throttle = new ErrorThrottle();
        ErrorReports.Enabled = true;
        if (File.Exists(_fallbackPath)) File.Delete(_fallbackPath);
    }

    /// <summary>Vyhozená a odchycená výjimka - throttle klíč bere rámce ze stacku.</summary>
    private static Exception Thrown(string message = "boom")
    {
        try { throw new InvalidOperationException(message); }
        catch (Exception ex) { return ex; }
    }

    /// <summary>Send je fire-and-forget (Task.Run), takže se na dorazivší request čeká.</summary>
    private void WaitForRequests(int expected)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (_handler.Count < expected && DateTime.UtcNow < deadline) Thread.Sleep(10);
        Assert.Equal(expected, _handler.Count);
    }

    /// <summary>Krátká lhůta pro důkaz, že se nic neodeslalo.</summary>
    private void AssertNothingSent()
    {
        Thread.Sleep(150);
        Assert.Equal(0, _handler.Count);
    }

    private JsonObject Body(int index) => JsonNode.Parse(_handler.Bodies[index])!.AsObject();

    // ---------- co se zahodí ----------

    [Fact]
    public void Report_Disabled_ReturnsNullAndSendsNothing()
    {
        ErrorReports.Enabled = false;

        Assert.Null(ErrorReports.Report(Thrown()));
        AssertNothingSent();
    }

    [Fact]
    public void Report_NullException_ReturnsNull()
    {
        Assert.Null(ErrorReports.Report(null!));
        AssertNothingSent();
    }

    [Fact]
    public void ReportBackground_Disabled_SendsNothing()
    {
        ErrorReports.Enabled = false;

        ErrorReports.ReportBackground(Thrown());

        AssertNothingSent();
    }

    [Fact]
    public void ReportFatal_Disabled_SendsNothing()
    {
        ErrorReports.Enabled = false;

        ErrorReports.ReportFatal(Thrown());

        Assert.Equal(0, _handler.Count);
    }

    [Fact]
    public void ReportFollowUp_BlankRefIdOrText_SendsNothing()
    {
        ErrorReports.ReportFollowUp("", "něco");
        ErrorReports.ReportFollowUp("xabc", "   ");

        AssertNothingSent();
    }

    // ---------- deduplikace ----------

    [Fact]
    public void Report_FirstOccurrence_ReturnsRefIdAndSends()
    {
        var refId = ErrorReports.Report(Thrown());

        Assert.False(string.IsNullOrWhiteSpace(refId));
        WaitForRequests(1);
        Assert.Equal(refId, Body(0)["data"]!["refId"]!.GetValue<string>());
    }

    [Fact]
    public void Report_SecondIdenticalOccurrence_IsSuppressed()
    {
        // Chyba v OnPaint / timeru se umí opakovat stokrát za minutu; druhý výskyt uvnitř okna
        // se jen počítá a číslo se nevrací (číslo, které nikam nedorazilo, plete podporu).
        var first = ErrorReports.Report(Thrown());
        WaitForRequests(1);

        var second = ErrorReports.Report(Thrown());

        Assert.NotNull(first);
        Assert.Null(second);
        Thread.Sleep(150);
        Assert.Equal(1, _handler.Count);
    }

    [Fact]
    public void ReportBackground_SecondIdenticalOccurrence_IsSuppressed()
    {
        ErrorReports.ReportBackground(Thrown());
        WaitForRequests(1);

        ErrorReports.ReportBackground(Thrown());

        Thread.Sleep(150);
        Assert.Equal(1, _handler.Count);
    }

    [Fact]
    public void ReportBackground_SendsErrorWithoutAttachments()
    {
        // Běží na cizím vlákně - snímek okna by tam nefungoval, takže se ani nezkouší.
        ErrorReports.ReportBackground(Thrown());

        WaitForRequests(1);
        var body = Body(0);
        Assert.Equal("error", body["type"]!.GetValue<string>());
        Assert.True(body["attachments"] is null || body["attachments"]!.AsArray().Count == 0);
    }

    // ---------- fatální cesta ----------

    [Fact]
    public void ReportFatal_SendsSynchronouslyAndMarksFatal()
    {
        // Po AppDomain.UnhandledException runtime proces vzápětí ukončí - fire-and-forget by
        // se nestihl, takže request musí být odeslaný ještě před návratem z metody.
        ErrorReports.ReportFatal(Thrown());

        Assert.Equal(1, _handler.Count);
        Assert.True(Body(0)["data"]!["fatal"]!.GetValue<bool>());
    }

    [Fact]
    public void ReportFatal_IgnoresThrottle()
    {
        // Fatální chyba je poslední, co z běhu uvidíme - deduplikace ji zahodit nesmí.
        ErrorReports.ReportFatal(Thrown());
        ErrorReports.ReportFatal(Thrown());

        Assert.Equal(2, _handler.Count);
    }

    // ---------- dobrovolné doplnění ----------

    [Fact]
    public void ReportFollowUp_SendsInfoRecordWithSameRefId()
    {
        ErrorReports.ReportFollowUp("xabc", "  klikl jsem na Ulozit  ");

        WaitForRequests(1);
        var body = Body(0);
        Assert.Equal("info", body["type"]!.GetValue<string>());
        Assert.Equal("xabc", body["data"]!["refId"]!.GetValue<string>());
        Assert.Equal("klikl jsem na Ulozit", body["data"]!["userText"]!.GetValue<string>());
    }

    [Fact]
    public void ReportFollowUp_NotThrottled()
    {
        // Doplnění posílá uživatel ručně; deduplikace patří automatickým hlášením.
        ErrorReports.ReportFollowUp("xabc", "první");
        ErrorReports.ReportFollowUp("xabc", "druhé");

        WaitForRequests(2);
    }

    // ---------- kontext ----------

    [Fact]
    public void Report_WithoutEDServices_FallsBackToEdison2Source()
    {
        // Hlášení musí odejít i před inicializací EDServices (chyba v bootstrapu).
        ErrorReports.Report(Thrown());

        WaitForRequests(1);
        Assert.Equal("EDISON2", Body(0)["source"]!.GetValue<string>());
    }

    [Fact]
    public void Report_InternalMachine_MarksSourceWithSuffix()
    {
        MlineEnvironment.OverrideForTests(true);

        ErrorReports.Report(Thrown());

        WaitForRequests(1);
        Assert.EndsWith("-internal", Body(0)["source"]!.GetValue<string>());
    }
}
