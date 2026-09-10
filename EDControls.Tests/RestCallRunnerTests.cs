using EDControls;
using EDTesting;
using NSubstitute;

namespace EDControls.Tests;

/// <summary>
/// Invariant runneru: každé volání jde na **klon** (RestStatus je per-instance, souběžná
/// volání na téže instanci si stavy prohodí) a instance se snapshotuje na volajícím threadu,
/// takže rozjetá dávka drží svou session i po přepnutí firmy / uživatele.
/// </summary>
public class RestCallRunnerTests
{
    [Fact]
    public async Task RunAsync_CallsOnClone_NotOnSharedInstance()
    {
        var shared = MLRestMocks.Create();
        var clone = MLRestMocks.Create();
        shared.With().Returns(clone);
        var runner = new RestCallRunner(() => shared);

        var seen = await runner.RunAsync(rest => rest);

        Assert.Same(clone, seen);
        Assert.NotSame(shared, seen);
    }

    [Fact]
    public async Task RunAsync_ReturnsValueFromCall()
    {
        var shared = MLRestMocks.Create();
        var clone = MLRestMocks.Create();
        shared.With().Returns(clone);
        var runner = new RestCallRunner(() => shared);

        Assert.Equal(42, await runner.RunAsync(_ => 42));
    }

    [Fact]
    public async Task RunAsync_SnapshotsInstanceBeforeAwait()
    {
        // Factory podvržená až po vstupu do RunAsync se do běžící dávky nesmí propsat -
        // jinak by druhá polovina dávky mluvila s cizí firmou.
        var first = MLRestMocks.Create();
        var firstClone = MLRestMocks.Create();
        first.With().Returns(firstClone);
        var second = MLRestMocks.Create();
        var secondClone = MLRestMocks.Create();
        second.With().Returns(secondClone);

        var current = first;
        var runner = new RestCallRunner(() => current);

        var task = runner.RunAsync(rest => rest);
        current = second;

        Assert.Same(firstClone, await task);
        second.DidNotReceive().With();
    }

    [Fact]
    public async Task RunAsync_CallThrows_PropagatesThroughTask()
    {
        var shared = MLRestMocks.Create();
        var clone = MLRestMocks.Create();
        shared.With().Returns(clone);
        var runner = new RestCallRunner(() => shared);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.RunAsync<IMLRest>(_ => throw new InvalidOperationException("boom")));
    }

    [Fact]
    public async Task RunAsync_EachCallGetsItsOwnClone()
    {
        var shared = MLRestMocks.Create();
        var cloneA = MLRestMocks.Create();
        var cloneB = MLRestMocks.Create();
        shared.With().Returns(cloneA, cloneB);
        var runner = new RestCallRunner(() => shared);

        var a = await runner.RunAsync(rest => rest);
        var b = await runner.RunAsync(rest => rest);

        Assert.NotSame(a, b);
        shared.Received(2).With();
    }
}
