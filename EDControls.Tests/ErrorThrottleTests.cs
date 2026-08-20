using EDControls.ErrorReporting;

namespace EDControls.Tests;

/// <summary>
/// Deduplikace hlášení. Suppression list vědomě nemáme, takže tohle je jediná pojistka
/// proti tomu, aby chyba v timeru nebo v <c>OnPaint</c> zaplavila podporu.
/// </summary>
public class ErrorThrottleTests
{
    private static readonly DateTime T0 = new(2026, 8, 4, 10, 0, 0, DateTimeKind.Utc);

    private static Exception Thrown(string message = "boom")
    {
        try { throw new InvalidOperationException(message); }
        catch (Exception ex) { return ex; }
    }

    [Fact]
    public void FirstOccurrence_IsSent()
    {
        var throttle = new ErrorThrottle();

        var decision = throttle.Register("key", T0);

        Assert.Equal(ThrottleAction.Send, decision.Action);
        Assert.Equal(1, decision.RepeatCount);
    }

    [Fact]
    public void RepeatInsideWindow_IsSuppressed()
    {
        var throttle = new ErrorThrottle(TimeSpan.FromMinutes(5));
        throttle.Register("key", T0);

        var second = throttle.Register("key", T0.AddSeconds(1));
        var third = throttle.Register("key", T0.AddMinutes(4));

        Assert.Equal(ThrottleAction.Suppress, second.Action);
        Assert.Equal(ThrottleAction.Suppress, third.Action);
        Assert.Equal(1, throttle.SentCount);   // pořád jen ten první
    }

    [Fact]
    public void AfterWindow_SendsSummaryWithCount()
    {
        var throttle = new ErrorThrottle(TimeSpan.FromMinutes(5));
        throttle.Register("key", T0);
        throttle.Register("key", T0.AddSeconds(1));
        throttle.Register("key", T0.AddSeconds(2));

        var decision = throttle.Register("key", T0.AddMinutes(6));

        Assert.Equal(ThrottleAction.SendSummary, decision.Action);
        Assert.Equal(3, decision.RepeatCount);   // 2 potlačené + tenhle výskyt
    }

    [Fact]
    public void AfterWindow_WithoutRepeats_IsPlainSend()
    {
        var throttle = new ErrorThrottle(TimeSpan.FromMinutes(5));
        throttle.Register("key", T0);

        var decision = throttle.Register("key", T0.AddMinutes(6));

        Assert.Equal(ThrottleAction.Send, decision.Action);
        Assert.Equal(1, decision.RepeatCount);
    }

    [Fact]
    public void DifferentKeys_DoNotShareWindow()
    {
        var throttle = new ErrorThrottle();

        Assert.Equal(ThrottleAction.Send, throttle.Register("a", T0).Action);
        Assert.Equal(ThrottleAction.Send, throttle.Register("b", T0).Action);
    }

    [Fact]
    public void RunCap_StopsSendingButKeepsCounting()
    {
        var throttle = new ErrorThrottle(TimeSpan.FromMinutes(5), maxReportsPerRun: 2);

        Assert.Equal(ThrottleAction.Send, throttle.Register("a", T0).Action);
        Assert.Equal(ThrottleAction.Send, throttle.Register("b", T0).Action);
        Assert.Equal(ThrottleAction.Suppress, throttle.Register("c", T0).Action);
        Assert.Equal(2, throttle.SentCount);
    }

    [Fact]
    public void Key_SameExceptionSamePlace_IsStable()
    {
        var first = ErrorThrottle.BuildKey(Thrown(), "EDISON2.MainWindow");
        var second = ErrorThrottle.BuildKey(Thrown(), "EDISON2.MainWindow");

        Assert.Equal(first, second);
    }

    [Fact]
    public void Key_DiffersByExceptionType()
    {
        Exception other;
        try { throw new TimeoutException("boom"); }
        catch (Exception ex) { other = ex; }

        Assert.NotEqual(ErrorThrottle.BuildKey(Thrown(), "F"), ErrorThrottle.BuildKey(other, "F"));
    }

    [Fact]
    public void Key_DiffersByForm()
    {
        // Táž výjimka na jiném okně je jiný problém.
        Assert.NotEqual(ErrorThrottle.BuildKey(Thrown(), "EDISON2.A"),
                        ErrorThrottle.BuildKey(Thrown(), "EDISON2.B"));
    }

    [Fact]
    public void Key_HandlesExceptionWithoutStack()
    {
        // Nevyhozená výjimka nemá StackTrace - nesmí to spadnout.
        var key = ErrorThrottle.BuildKey(new InvalidOperationException("nikdy nevyhozena"), "F");

        Assert.False(string.IsNullOrEmpty(key));
    }
}
