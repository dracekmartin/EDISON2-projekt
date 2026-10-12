using System.Text.Json.Nodes;
using EDControls.UserPrefs;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace EDControls.Tests;

/// <summary>
/// Odložený zápis preferencí sdílený EDListem a EDFilterem: série změn = jeden zápis s posledním
/// stavem, flush jen čekající změny, chyba zápisu nesmí shodit UI. Odpočet řídí test.
/// </summary>
public class DebouncedUserPrefsWriterTests
{
    private readonly IUserPrefsService _prefs = Substitute.For<IUserPrefsService>();
    private readonly List<TaskCompletionSource> _delays = [];
    private string? _key = "Host";
    private int _state;

    // Bez kontextu doběhne pokračování po odpočtu synchronně v SetResult - test nemusí čekat.
    private void Run(Action body)
    {
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try { body(); }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }

    private DebouncedUserPrefsWriter Writer(Func<JsonNode>? snapshot = null) =>
        new(() => _prefs, "Scope", () => _key, snapshot ?? (() => JsonValue.Create(_state)), _ =>
        {
            var delay = new TaskCompletionSource();
            _delays.Add(delay);
            return delay.Task;
        });

    [Fact]
    public void TwoSchedules_SaveOnce_WithLastState()
    {
        Run(() =>
        {
            var writer = Writer();
            _state = 1;
            writer.Schedule();
            _state = 2;
            writer.Schedule();
            _state = 3;   // snapshot se bere až při zápisu, ne při Schedule

            _delays[0].SetResult();
            _prefs.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!, default!);
            _delays[1].SetResult();

            _prefs.Received(1).SaveAsync("Scope", "Host", Arg.Is<JsonNode>(n => n.GetValue<int>() == 3));
        });
    }

    /// <summary>Snapshot čte DataGrid a pole filtru - musí běžet na kontextu volajícího (UI vlákno),
    /// ne na threadpoolu. ConfigureAwait(false) v odpočtu by to tiše rozbil, chybu by spolkl catch.</summary>
    [Fact]
    public void SaveAfterDelay_RunsSnapshotOnSchedulingContext()
    {
        var context = new QueueingContext();
        SynchronizationContext? seen = null;
        var writer = Writer(() => { seen = SynchronizationContext.Current; return JsonValue.Create(1); });
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try { writer.Schedule(); }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }

        _delays[0].SetResult();
        Assert.Null(seen);   // pokračování se jen zařadilo do kontextu
        context.RunPosted();

        Assert.Same(context, seen);
    }

    private sealed class QueueingContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _posted = new();

        public override void Post(SendOrPostCallback d, object? state) => _posted.Enqueue((d, state));

        public void RunPosted()
        {
            var previous = Current;
            SetSynchronizationContext(this);
            try
            {
                while (_posted.TryDequeue(out var item)) item.Callback(item.State);
            }
            finally { SetSynchronizationContext(previous); }
        }
    }

    private (DebouncedUserPrefsWriter Writer, GatedMLRest Shared) CompanyWriter()
    {
        var shared = new GatedMLRest { Idfa = "A", DataNamespace = "NS-A" };
        var prefs = new CacheUserPrefsService(() => shared, _ => { });
        var writer = new DebouncedUserPrefsWriter(() => prefs, "Scope", () => _key, () => JsonValue.Create(_state), _ =>
        {
            var delay = new TaskCompletionSource();
            _delays.Add(delay);
            return delay.Task;
        });
        return (writer, shared);
    }

    // Přepnutí firmy přepisuje sdílenou instanci na místě (CompanySwitcher).
    private static void SwitchCompany(GatedMLRest shared)
    {
        shared.Idfa = "B";
        shared.DataNamespace = "NS-B";
    }

    private static string[] SavedUnder(GatedMLRest shared) =>
        [.. SavedWith(shared).Select(c => c.Idfa).Order()];

    private static (string Idfa, int CacheLocks)[] SavedWith(GatedMLRest shared)
    {
        Assert.True(BackgroundWrites.WaitPending(TimeSpan.FromSeconds(10)));
        return [.. shared.Calls.Where(c => c.Method == "Save").Select(c => (c.Idfa, c.CacheLocks))];
    }

    [Fact]
    public void ChangeBeforeCompanySwitch_DelayedSave_GoesToCompanyOfChangeWithoutLockCheck()
    {
        // Zámek staré firmy přepnutí odhlásí, se CacheLocks=1 by server zápis odmítl.
        Run(() =>
        {
            var (writer, shared) = CompanyWriter();
            writer.Schedule();
            SwitchCompany(shared);

            _delays[0].SetResult();

            Assert.Equal([("A", 0)], SavedWith(shared));
        });
    }

    /// <summary>WPF pustí Unloaded zavřeného okna až po návratu CompanySwitcher.Switch.</summary>
    [Fact]
    public void ChangeBeforeCompanySwitch_FlushFromUnloaded_GoesToCompanyOfChange()
    {
        Run(() =>
        {
            var (writer, shared) = CompanyWriter();
            writer.Schedule();
            SwitchCompany(shared);

            writer.FlushAsync().GetAwaiter().GetResult();

            Assert.Equal(["A"], SavedUnder(shared));
        });
    }

    [Fact]
    public void ChangesUnderTwoCompanies_EachSavedUnderItsOwn()
    {
        Run(() =>
        {
            var (writer, shared) = CompanyWriter();
            writer.Schedule();
            SwitchCompany(shared);
            writer.Schedule();

            _delays[1].SetResult();

            Assert.Equal(["A", "B"], SavedUnder(shared));
        });
    }

    /// <summary>Zavření okna, kde se nic nezměnilo, nemá posílat request.</summary>
    [Fact]
    public void FlushAsync_WithoutPending_SavesNothing()
    {
        Run(() =>
        {
            Writer().FlushAsync().GetAwaiter().GetResult();

            _prefs.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!, default!);
        });
    }

    [Fact]
    public void FlushAsync_WithPending_SavesNow_AndDelayDoesNotSaveAgain()
    {
        Run(() =>
        {
            var writer = Writer();
            writer.Schedule();

            writer.FlushAsync().GetAwaiter().GetResult();
            _delays[0].SetResult();

            _prefs.ReceivedWithAnyArgs(1).SaveAsync(default!, default!, default!);
        });
    }

    [Fact]
    public void EmptyKey_Schedule_IsNoOp()
    {
        Run(() =>
        {
            _key = "";
            var writer = Writer();

            writer.Schedule();
            writer.FlushAsync().GetAwaiter().GetResult();

            Assert.Empty(_delays);
            _prefs.DidNotReceiveWithAnyArgs().SaveAsync(default!, default!, default!);
        });
    }

    [Fact]
    public void SaveOrSnapshotFailure_IsSwallowed()
    {
        Run(() =>
        {
            _prefs.SaveAsync("Scope", "Host", Arg.Any<JsonNode>()).ThrowsAsync(new InvalidOperationException("síť"));
            var writer = Writer();
            writer.Schedule();
            writer.FlushAsync().GetAwaiter().GetResult();

            var broken = Writer(() => throw new InvalidOperationException("snapshot"));
            broken.Schedule();
            broken.FlushAsync().GetAwaiter().GetResult();
        });
    }
}
