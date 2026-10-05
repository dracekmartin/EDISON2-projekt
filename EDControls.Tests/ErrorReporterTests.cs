using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using EDControls.ErrorReporting;

namespace EDControls.Tests;

/// <summary>
/// Odeslání na ingest endpoint proti fake handleru - hlídá hlavičku, tvar dávky
/// a to, že se výpadek sítě spolkne (hlášení chyby nesmí být druhá chyba).
/// </summary>
public class ErrorReporterTests : IDisposable
{
    private sealed class FakeHandler(HttpStatusCode status = HttpStatusCode.Created) : HttpMessageHandler
    {
        public readonly List<HttpRequestMessage> Requests = [];
        public readonly List<string> Bodies = [];
        public Exception? ThrowOnSend { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));
            if (ThrowOnSend is not null) throw ThrowOnSend;
            return new HttpResponseMessage(status) { Content = new StringContent("{}") };
        }
    }

    private readonly FakeHandler _handler = new();
    private readonly string _fallbackPath = Path.Combine(Path.GetTempPath(), $"ed-fallback-{Guid.NewGuid():N}.log");

    public void Dispose()
    {
        if (File.Exists(_fallbackPath)) File.Delete(_fallbackPath);
        if (File.Exists(_fallbackPath + ".old")) File.Delete(_fallbackPath + ".old");
    }

    private ErrorReportingPipeline Pipeline(HttpMessageHandler? handler = null, long maxBytes = ErrorFallbackLog.DefaultMaxBytes) =>
        new(() => new HttpClient(handler ?? _handler), new ErrorFallbackLog(_fallbackPath, maxBytes),
            () => false, () => null, () => []);

    private static ErrorReport Report(string refId = "xabc") => new()
    {
        RefId = refId,
        Source = "EDISON2",
        Type = "error",
        Module = "EDISONMAIN2.Windows.TestWindow",
        Name = "Test",
        Description = $"{refId} | test",
        Data = new JsonObject { ["refId"] = refId },
    };

    [Fact]
    public async Task Send_PostsToIngestWithApiKeyHeader()
    {
        await Pipeline().SendAsync([Report()]);

        var request = Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(ErrorReporter.IngestUrl, request.RequestUri?.ToString());
        Assert.Equal(ErrorReporter.ApiKey, Assert.Single(request.Headers.GetValues("X-API-Key")));
    }

    [Fact]
    public async Task SingleReport_IsSentAsObject()
    {
        await Pipeline().SendAsync([Report()]);

        var node = JsonNode.Parse(_handler.Bodies[0]);
        Assert.IsType<JsonObject>(node);
        Assert.Equal("EDISON2", node!["source"]?.GetValue<string>());
    }

    [Fact]
    public async Task MultipleReports_AreSentAsArray()
    {
        await Pipeline().SendAsync([Report("x1"), Report("x2")]);

        var node = JsonNode.Parse(_handler.Bodies[0]);
        var array = Assert.IsType<JsonArray>(node);
        Assert.Equal(2, array.Count);
    }

    [Fact]
    public async Task OverBatchLimit_IsSplitIntoSeveralRequests()
    {
        // Server odmítne dávku nad MAX_BATCH_SIZE=100 chybou 400.
        var reports = Enumerable.Range(0, ErrorReporter.MaxBatchSize + 5).Select(i => Report($"x{i}")).ToList();

        await Pipeline().SendAsync(reports);

        Assert.Equal(2, _handler.Requests.Count);
        Assert.Equal(ErrorReporter.MaxBatchSize, JsonNode.Parse(_handler.Bodies[0])!.AsArray().Count);
        Assert.Equal(5, JsonNode.Parse(_handler.Bodies[1])!.AsArray().Count);
    }

    [Fact]
    public async Task NetworkFailure_DoesNotThrow()
    {
        _handler.ThrowOnSend = new HttpRequestException("VPN je dole");

        var ok = await Pipeline().SendAsync([Report()]);

        Assert.False(ok);
    }

    [Fact]
    public async Task ServerError_DoesNotThrow()
    {
        var ok = await Pipeline(new FakeHandler(HttpStatusCode.InternalServerError)).SendAsync([Report()]);

        Assert.False(ok);
    }

    [Fact]
    public void ClientOrLogPathFailing_NeverThrows_FallbackStillWritten()
    {
        // Klient i cesta logu vznikají až při odeslání: výjimka při jejich tvorbě nesmí z hlášení
        // (ani z crash handleru) udělat druhou chybu. Výchozí instance je proto jen z delegátů.
        var pipeline = new ErrorReportingPipeline(() => throw new InvalidOperationException("klient"),
            new ErrorFallbackLog(_fallbackPath), () => false, () => null, () => []);
        var brokenLog = new ErrorReportingPipeline(() => throw new InvalidOperationException("klient"),
            new ErrorFallbackLog(() => throw new InvalidOperationException("cesta")), () => false, () => null, () => []);

        Assert.Null(Record.Exception(() => pipeline.ReportFatal(new InvalidOperationException("boom"))));
        Assert.Null(Record.Exception(() => brokenLog.ReportFatal(new InvalidOperationException("boom"))));
        Assert.Null(Record.Exception(ErrorReportingPipeline.CreateDefault));
        Assert.True(File.Exists(_fallbackPath));
    }

    [Fact]
    public async Task EmptyBatch_SendsNothing()
    {
        await Pipeline().SendAsync([]);

        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task NetworkFailure_WritesFallbackLog()
    {
        _handler.ThrowOnSend = new HttpRequestException("VPN je dole");

        await Pipeline().SendAsync([Report("xfall")]);

        var line = Assert.Single(File.ReadAllLines(_fallbackPath));
        var node = JsonNode.Parse(line)!.AsObject();
        Assert.Equal("EDISON2", node["source"]?.GetValue<string>());
        Assert.Contains("xfall", node["description"]?.GetValue<string>());
        Assert.NotNull(node["loggedAt"]);
    }

    [Fact]
    public async Task ServerError_WritesFallbackLog()
    {
        await Pipeline(new FakeHandler(HttpStatusCode.InternalServerError)).SendAsync([Report()]);

        Assert.True(File.Exists(_fallbackPath));
    }

    [Fact]
    public async Task Success_DoesNotWriteFallbackLog()
    {
        await Pipeline().SendAsync([Report()]);

        Assert.False(File.Exists(_fallbackPath));
    }

    [Fact]
    public async Task FallbackLog_StripsAttachments()
    {
        // Base64 snímky oken by log nafoukly o stovky KB na záznam.
        _handler.ThrowOnSend = new HttpRequestException("offline");
        var report = new ErrorReport
        {
            RefId = "xatt",
            Source = "EDISON2",
            Type = "error",
            Module = "M",
            Name = "N",
            Description = "xatt | test",
            Attachments = [new ErrorAttachment("w.png", "image/png", "AAAA")],
        };

        await Pipeline().SendAsync([report]);

        var node = JsonNode.Parse(File.ReadAllLines(_fallbackPath)[0])!.AsObject();
        Assert.False(node.ContainsKey("attachments"));
    }

    [Fact]
    public async Task FallbackLog_OverLimit_RotatesToOld()
    {
        _handler.ThrowOnSend = new HttpRequestException("offline");
        File.WriteAllText(_fallbackPath, new string('x', 50));

        await Pipeline(maxBytes: 10).SendAsync([Report("xrot")]);

        Assert.Equal(new string('x', 50), File.ReadAllText(_fallbackPath + ".old"));
        var line = Assert.Single(File.ReadAllLines(_fallbackPath));
        Assert.Contains("xrot", line);
    }
}
