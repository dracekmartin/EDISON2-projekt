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
[Collection(SharedStaticStateCollection.Name)]
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

    public ErrorReporterTests()
    {
        ErrorReporter.ClientOverride = new HttpClient(_handler);
        ErrorFallbackLog.PathOverride = _fallbackPath;
    }

    public void Dispose()
    {
        ErrorReporter.ClientOverride = null;
        ErrorFallbackLog.PathOverride = null;
        ErrorFallbackLog.MaxBytes = 1_000_000;
        if (File.Exists(_fallbackPath)) File.Delete(_fallbackPath);
        if (File.Exists(_fallbackPath + ".old")) File.Delete(_fallbackPath + ".old");
    }

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
        await ErrorReporter.SendAsync([Report()]);

        var request = Assert.Single(_handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(ErrorReporter.IngestUrl, request.RequestUri?.ToString());
        Assert.Equal(ErrorReporter.ApiKey, Assert.Single(request.Headers.GetValues("X-API-Key")));
    }

    [Fact]
    public async Task SingleReport_IsSentAsObject()
    {
        await ErrorReporter.SendAsync([Report()]);

        var node = JsonNode.Parse(_handler.Bodies[0]);
        Assert.IsType<JsonObject>(node);
        Assert.Equal("EDISON2", node!["source"]?.GetValue<string>());
    }

    [Fact]
    public async Task MultipleReports_AreSentAsArray()
    {
        await ErrorReporter.SendAsync([Report("x1"), Report("x2")]);

        var node = JsonNode.Parse(_handler.Bodies[0]);
        var array = Assert.IsType<JsonArray>(node);
        Assert.Equal(2, array.Count);
    }

    [Fact]
    public async Task OverBatchLimit_IsSplitIntoSeveralRequests()
    {
        // Server odmítne dávku nad MAX_BATCH_SIZE=100 chybou 400.
        var reports = Enumerable.Range(0, ErrorReporter.MaxBatchSize + 5).Select(i => Report($"x{i}")).ToList();

        await ErrorReporter.SendAsync(reports);

        Assert.Equal(2, _handler.Requests.Count);
        Assert.Equal(ErrorReporter.MaxBatchSize, JsonNode.Parse(_handler.Bodies[0])!.AsArray().Count);
        Assert.Equal(5, JsonNode.Parse(_handler.Bodies[1])!.AsArray().Count);
    }

    [Fact]
    public async Task NetworkFailure_DoesNotThrow()
    {
        _handler.ThrowOnSend = new HttpRequestException("VPN je dole");

        var ok = await ErrorReporter.SendAsync([Report()]);

        Assert.False(ok);
    }

    [Fact]
    public async Task ServerError_DoesNotThrow()
    {
        ErrorReporter.ClientOverride = new HttpClient(new FakeHandler(HttpStatusCode.InternalServerError));

        var ok = await ErrorReporter.SendAsync([Report()]);

        Assert.False(ok);
    }

    [Fact]
    public async Task EmptyBatch_SendsNothing()
    {
        await ErrorReporter.SendAsync([]);

        Assert.Empty(_handler.Requests);
    }

    // ---------- lokální fallback log ----------

    [Fact]
    public async Task NetworkFailure_WritesFallbackLog()
    {
        _handler.ThrowOnSend = new HttpRequestException("VPN je dole");

        await ErrorReporter.SendAsync([Report("xfall")]);

        var line = Assert.Single(File.ReadAllLines(_fallbackPath));
        var node = JsonNode.Parse(line)!.AsObject();
        Assert.Equal("EDISON2", node["source"]?.GetValue<string>());
        Assert.Contains("xfall", node["description"]?.GetValue<string>());
        Assert.NotNull(node["loggedAt"]);
    }

    [Fact]
    public async Task ServerError_WritesFallbackLog()
    {
        ErrorReporter.ClientOverride = new HttpClient(new FakeHandler(HttpStatusCode.InternalServerError));

        await ErrorReporter.SendAsync([Report()]);

        Assert.True(File.Exists(_fallbackPath));
    }

    [Fact]
    public async Task Success_DoesNotWriteFallbackLog()
    {
        await ErrorReporter.SendAsync([Report()]);

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

        await ErrorReporter.SendAsync([report]);

        var node = JsonNode.Parse(File.ReadAllLines(_fallbackPath)[0])!.AsObject();
        Assert.False(node.ContainsKey("attachments"));
    }

    [Fact]
    public async Task FallbackLog_OverLimit_RotatesToOld()
    {
        _handler.ThrowOnSend = new HttpRequestException("offline");
        ErrorFallbackLog.MaxBytes = 10;
        File.WriteAllText(_fallbackPath, new string('x', 50));

        await ErrorReporter.SendAsync([Report("xrot")]);

        Assert.Equal(new string('x', 50), File.ReadAllText(_fallbackPath + ".old"));
        var line = Assert.Single(File.ReadAllLines(_fallbackPath));
        Assert.Contains("xrot", line);
    }
}
