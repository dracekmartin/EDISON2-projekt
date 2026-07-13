using System.Windows.Media;

namespace EDControls.Tests;

/// <summary>Testy RefCountRegistry (jádro WindowDimmer) + invariant TintBrush.</summary>
public class WindowDimmerTests
{
    private readonly RefCountRegistry _refs = new();
    private int _first;
    private int _last;

    private IDisposable Acquire(object key) =>
        _refs.Acquire(key, () => _first++, () => _last++);

    [Fact]
    public void Acquire_First_FiresOnFirstOnly()
    {
        Acquire("w");
        Assert.Equal(1, _first);
        Assert.Equal(0, _last);
    }

    [Fact]
    public void Acquire_Nested_FiresNothing()
    {
        Acquire("w");
        Acquire("w");
        Assert.Equal(1, _first);
        Assert.Equal(0, _last);
    }

    [Fact]
    public void Dispose_Inner_KeepsOuterActive()
    {
        var outer = Acquire("w");
        var inner = Acquire("w");
        inner.Dispose();
        Assert.Equal(0, _last);
        outer.Dispose();
        Assert.Equal(1, _last);
    }

    [Fact]
    public void Dispose_Double_Idempotent()
    {
        var scope = Acquire("w");
        scope.Dispose();
        scope.Dispose();
        Assert.Equal(1, _last);
    }

    [Fact]
    public void Acquire_IndependentKeys_IndependentCounts()
    {
        var a = Acquire("a");
        Acquire("b");
        Assert.Equal(2, _first);
        a.Dispose();
        Assert.Equal(1, _last);
    }

    [Fact]
    public void Acquire_AfterFullRelease_FiresOnFirstAgain()
    {
        Acquire("w").Dispose();
        Acquire("w");
        Assert.Equal(2, _first);
        Assert.Equal(1, _last);
    }

    [Fact]
    public void ForceRelease_FiresOnLast_LaterDisposesAreNoOp()
    {
        var scope = Acquire("w");
        _refs.ForceRelease("w");
        Assert.Equal(1, _last);
        scope.Dispose();
        Assert.Equal(1, _last);
    }

    [Fact]
    public void ForceRelease_UnknownKey_NoOp()
    {
        _refs.ForceRelease("w");
        Assert.Equal(0, _last);
    }

    [Fact]
    public void TintBrush_IsFrozenTenPercentBlack()
    {
        Assert.Equal(Color.FromArgb(0x1A, 0x00, 0x00, 0x00), WindowDimmer.TintBrush.Color);
        Assert.True(WindowDimmer.TintBrush.IsFrozen);
    }
}
