using EDControls;
using EDTesting;
using NSubstitute;

namespace EDControls.Tests;

/// <summary>
/// Invariant runneru: každé volání jde na **klon** (RestStatus je per-instance, souběžná
/// volání na téže instanci si stavy prohodí). Instanci i její Idfa, DataNamespace a CacheLocks
/// čte na volajícím threadu, takže rozjetá dávka drží svou session i po přepnutí firmy / uživatele.
/// </summary>
public class RestCallRunnerTests
{
    [Fact]
    public async Task RunAsync_CompanySwitchBeforeCallRuns_CloneKeepsSessionFromCallTime()
    {
        // Přepnutí firmy přepisuje sdílenou instanci na místě - zápis založený před ním musí odejít pod starou firmou.
        var adapter = new MLRestAdapter(new ALWA.SYS.MLRest("srv", 1234, "NS-A", "IDFA-A") { CacheLocks = 1 });
        var gate = new GateScheduler();
        var runner = new RestCallRunner(() => adapter, gate);

        var task = runner.RunAsync(rest => (rest.Idfa, rest.DataNamespace, rest.CacheLocks));
        adapter.CacheLocks = 0;
        adapter.Idfa = "IDFA-B";
        adapter.DataNamespace = "NS-B";
        gate.RunQueued();

        Assert.Equal(("IDFA-A", "NS-A", 1), await task);
    }

    [Fact]
    public async Task RunAsync_RealAdapter_BuildsCloneInTaskNotOnCaller()
    {
        // Konstruktor MLRest dělá DNS dotaz, na UI threadu nemá co dělat. Pole mimo snímek proto čte až úloha.
        var inner = new ALWA.SYS.MLRest("srv", 1234, "NS", "IDFA") { LoginName = "early" };
        var gate = new GateScheduler();
        var runner = new RestCallRunner(() => new MLRestAdapter(inner), gate);

        var task = runner.RunAsync(rest => rest.LoginName);
        inner.LoginName = "late";
        gate.RunQueued();

        Assert.Equal("late", await task);
    }

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

    /// <summary>Úlohy čekají ve frontě, dokud je test nepustí - mezi vznikem a během volání jde přepnout firmu.</summary>
    private sealed class GateScheduler : TaskScheduler
    {
        private readonly List<Task> _queued = [];

        public void RunQueued()
        {
            foreach (var task in _queued.ToArray()) TryExecuteTask(task);
            _queued.Clear();
        }

        protected override void QueueTask(Task task) => _queued.Add(task);

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;

        protected override IEnumerable<Task> GetScheduledTasks() => _queued;
    }
}
