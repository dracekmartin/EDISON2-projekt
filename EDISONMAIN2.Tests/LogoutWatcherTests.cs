using EDControls;
using EDTesting;
using NSubstitute;

namespace EDISONMAIN2.Tests;

public class LogoutWatcherTests
{
    private const char Del = '~';

    private static object[] Rows(params string[] rows)
    {
        // output[0] = ERR slot (legacy konvence), data od indexu 1.
        var result = new object[rows.Length + 1];
        result[0] = "";
        for (int i = 0; i < rows.Length; i++) result[i + 1] = rows[i];
        return result;
    }

    // ---------- Parse ----------

    [Fact]
    public void Parse_UkoncitOne_ShouldClose_WithSeconds()
    {
        var signal = LogoutWatcher.Parse(Rows("cerpani~(50)", "ukoncit~1~120"), Del);

        Assert.True(signal.ShouldClose);
        Assert.Equal(120, signal.Seconds);
    }

    [Fact]
    public void Parse_UkoncitZero_DoesNotClose()
    {
        var signal = LogoutWatcher.Parse(Rows("ukoncit~0~60"), Del);

        Assert.False(signal.ShouldClose);
    }

    [Fact]
    public void Parse_NoUkoncitRow_DoesNotClose()
    {
        var signal = LogoutWatcher.Parse(Rows("cerpani~(50)"), Del);

        Assert.False(signal.ShouldClose);
    }

    [Fact]
    public void Parse_SecondsBelowFloor_ClampedTo60()
    {
        var signal = LogoutWatcher.Parse(Rows("ukoncit~1~10"), Del);

        Assert.Equal(60, signal.Seconds);
    }

    [Fact]
    public void Parse_MissingSecondsField_DefaultsTo60()
    {
        var signal = LogoutWatcher.Parse(Rows("ukoncit~1"), Del);

        Assert.True(signal.ShouldClose);
        Assert.Equal(60, signal.Seconds);
    }

    [Fact]
    public void Parse_EmptyOutput_DoesNotClose()
    {
        var signal = LogoutWatcher.Parse(Rows(), Del);

        Assert.False(signal.ShouldClose);
    }

    // ---------- Poll ----------

    // Poll volá maSeUkoncit na klonu s CacheLocks=2 (mlRest.With(2)); shared instance se nemutuje.
    private static IMLRest MockPoll(params string[] rows)
    {
        var mock = MLRestMocks.Create();
        mock.Delimiter.Returns(Del);
        var clone = MLRestMocks.Create();
        clone.QueryOrThrow("ALVA.SYSTEM.Main", "maSeUkoncit", Arg.Any<object[]>())
             .Returns(Rows(rows));
        mock.With(cacheLocks: 2, idfa: null, dataNamespace: null).Returns(clone);
        return mock;
    }

    [Fact]
    public void Poll_UsesCloneWithCacheLocks2()
    {
        var mock = MockPoll("ukoncit~0~60");

        LogoutWatcher.Poll(mock);

        mock.Received().With(cacheLocks: 2, idfa: null, dataNamespace: null);
    }

    [Fact]
    public void Poll_ReturnsParsedSignal()
    {
        var signal = LogoutWatcher.Poll(MockPoll("ukoncit~1~90"));

        Assert.True(signal.ShouldClose);
        Assert.Equal(90, signal.Seconds);
    }
}
