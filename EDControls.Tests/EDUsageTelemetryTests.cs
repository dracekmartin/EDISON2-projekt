using System.Text.RegularExpressions;
using EDControls.EDViewModel;
using NSubstitute;

namespace EDControls.Tests;

/// <summary>
/// <see cref="EDUsageTelemetry"/> sdílí statický buffer — testy běží v jedné kolekci
/// bez paralelismu, aby si stav nepřepisovaly, a každý začíná resetem.
/// </summary>
[CollectionDefinition(nameof(EDUsageTelemetryTests), DisableParallelization = true)]
public class EDUsageTelemetryCollection : ICollectionFixture<object>;

[Collection(nameof(EDUsageTelemetryTests))]
public class EDUsageTelemetryTests
{
    private readonly IMLRest _mlRest;

    public EDUsageTelemetryTests()
    {
        EDUsageTelemetry.ResetForTests();
        _mlRest = Substitute.For<IMLRest>();
        _mlRest.Delimiter.Returns('~');
    }

    private object[]? CaptureFlush()
    {
        object[]? captured = null;
        _mlRest.QueryOrThrow("ALVA.ISY.ISYCloud.APIdotNet.Telemetry", "LogUsageBatch",
                Arg.Do<object[]>(x => captured = x))
            .Returns([]);
        EDUsageTelemetry.Flush(_mlRest);
        return captured;
    }

    [Fact]
    public void TrackAndFlush_SendsBatchInWireFormat()
    {
        EDUsageTelemetry.Track("EDISON2.TestWindow", "OPEN");
        EDUsageTelemetry.Track("EDISON2.TestWindow", "BtnSave");

        var batch = CaptureFlush();

        Assert.NotNull(batch);
        Assert.Equal(2, batch!.Length);
        // "usage" ~ form ~ control ~ ts(yyyy-MM-dd HH:mm:ss) — kontrakt LogUsageBatch
        Assert.Matches(new Regex(@"^usage~EDISON2\.TestWindow~OPEN~\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$"),
            (string)batch[0]);
        Assert.Matches(new Regex(@"^usage~EDISON2\.TestWindow~BtnSave~\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$"),
            (string)batch[1]);
    }

    [Fact]
    public void Flush_ClearsBuffer_SecondFlushSendsNothing()
    {
        EDUsageTelemetry.Track("EDISON2.TestWindow", "OPEN");

        EDUsageTelemetry.Flush(_mlRest);
        EDUsageTelemetry.Flush(_mlRest);

        _mlRest.Received(1).QueryOrThrow("ALVA.ISY.ISYCloud.APIdotNet.Telemetry", "LogUsageBatch",
            Arg.Any<object[]>());
        Assert.Equal(0, EDUsageTelemetry.BufferCountForTests);
    }

    [Fact]
    public void Flush_NullMlRest_KeepsBuffer()
    {
        EDUsageTelemetry.Track("EDISON2.TestWindow", "OPEN");

        EDUsageTelemetry.Flush(null);

        Assert.Equal(1, EDUsageTelemetry.BufferCountForTests);
    }

    [Fact]
    public void Flush_WhenQueryThrows_SwallowsAndDropsRecords()
    {
        _mlRest.QueryOrThrow(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object[]>())
            .Returns(_ => throw new InvalidOperationException("server down"));
        EDUsageTelemetry.Track("EDISON2.TestWindow", "OPEN");

        EDUsageTelemetry.Flush(_mlRest);   // nesmí vyhodit
        EDUsageTelemetry.Flush(_mlRest);

        // Záznamy se při chybě zahodí (best-effort) — druhý flush už nevolá server.
        _mlRest.Received(1).QueryOrThrow(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Fact]
    public void Track_WhenDisabled_RecordsNothing()
    {
        EDUsageTelemetry.Enabled = false;
        EDUsageTelemetry.Track("EDISON2.TestWindow", "OPEN");
        EDUsageTelemetry.Enabled = true;

        Assert.Equal(0, EDUsageTelemetry.BufferCountForTests);
    }

    [Theory]
    [InlineData(null, "OPEN")]
    [InlineData("", "OPEN")]
    [InlineData("EDISON2.TestWindow", null)]
    [InlineData("EDISON2.TestWindow", "")]
    public void Track_MissingFormOrControl_Ignored(string? form, string? control)
    {
        EDUsageTelemetry.Track(form, control);

        Assert.Equal(0, EDUsageTelemetry.BufferCountForTests);
    }

    [Fact]
    public void Track_BeyondCap_StopsRecording()
    {
        for (int i = 0; i < 10_001; i++)
            EDUsageTelemetry.Track("EDISON2.TestWindow", $"Btn{i}");

        Assert.Equal(10_000, EDUsageTelemetry.BufferCountForTests);
    }
}
