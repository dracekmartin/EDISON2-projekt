using System.Text.Json;
using System.Text.Json.Nodes;
using EDControls.UserPrefs;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Kontrakty persistence stavu stromu: chybějící blob vs. uložené prázdno (na tom stojí
/// "ponech serverové výchozí rozbalení"), degradace staré hodnoty a polykání chyb.
/// </summary>
public class ModulePreferencesServiceTests
{
    private const string Scope = "EDISONMAIN2";

    private readonly IUserPrefsService _prefs = Substitute.For<IUserPrefsService>();
    private readonly ModulePreferencesService _service;

    public ModulePreferencesServiceTests() => _service = new ModulePreferencesService(_prefs);

    private void PrefsReturn(string key, JsonNode? node) =>
        _prefs.LoadAsync(Scope, key).Returns(Task.FromResult(node));

    [Fact]
    public async Task LoadExpansion_NeverSaved_ReturnsNull()
    {
        // null = nikdy neuloženo → caller ponechá serverové výchozí rozbalení
        PrefsReturn("TreeExpansion", null);

        Assert.Null(await _service.LoadExpansionAsync());
    }

    [Fact]
    public async Task LoadExpansion_SavedEmptyList_ReturnsEmptyNotNull()
    {
        // Uživatel vše sbalil a uložil - prázdný list je platný stav, ne "nikdy neuloženo"
        PrefsReturn("TreeExpansion", new JsonArray());

        var expansion = await _service.LoadExpansionAsync();

        Assert.NotNull(expansion);
        Assert.Empty(expansion);
    }

    [Fact]
    public async Task SaveExpansion_WritesListUnderTreeExpansionKey()
    {
        await _service.SaveExpansionAsync(["main|0", "main|0|2"]);

        await _prefs.Received(1).SaveAsync(Scope, "TreeExpansion",
            Arg.Is<JsonNode>(n => n.ToJsonString(null) == """["main|0","main|0|2"]"""));
    }

    [Fact]
    public async Task LoadShortcutsUsageSnooze_MissingBlobs_DegradeToEmpty()
    {
        PrefsReturn("Shortcuts", null);
        PrefsReturn("ModuleUsage", null);
        PrefsReturn("ChangelogSnooze", null);

        Assert.Empty((await _service.LoadShortcutsAsync())!);
        Assert.Empty(await _service.LoadUsageAsync());
        Assert.Empty(await _service.LoadChangelogSnoozeAsync());
    }

    [Fact]
    public async Task LoadSelectedTab_OldNumericValue_DegradesToNull()
    {
        // Dřív se ukládal index záložky - stará číselná hodnota nesmí spadnout, jen se zahodí
        PrefsReturn("SelectedTab", JsonValue.Create(2));

        Assert.Null(await _service.LoadSelectedTabAsync());
    }

    [Fact]
    public async Task LoadSelectedTab_StoredKey_ReturnsIt()
    {
        PrefsReturn("SelectedTab", JsonValue.Create("desktop"));

        Assert.Equal("desktop", await _service.LoadSelectedTabAsync());
    }

    [Fact]
    public async Task SaveUsage_RoundTripsThroughLoad()
    {
        JsonNode? saved = null;
        await _prefs.SaveAsync(Scope, "ModuleUsage", Arg.Do<JsonNode>(n => saved = n));
        var stamp = new DateTime(2026, 9, 8, 10, 0, 0, DateTimeKind.Utc);
        await _service.SaveUsageAsync(new Dictionary<string, UsageEntry>
        {
            ["66001"] = new(Count: 3, LastUsedUtc: stamp),
        });
        PrefsReturn("ModuleUsage", JsonNode.Parse(saved!.ToJsonString(null)));

        var usage = await _service.LoadUsageAsync();

        Assert.Equal(new UsageEntry(3, stamp), usage["66001"]);
    }

    [Fact]
    public async Task Load_PrefsThrow_DegradesInsteadOfThrowing()
    {
        _prefs.LoadAsync(Scope, Arg.Any<string>())
            .Returns<Task<JsonNode?>>(_ => throw new InvalidOperationException("server down"));

        // Plocha je výjimka: null hlásí selhání, aby ji VM nepřepsala prázdnem; zbytek degraduje.
        Assert.Null(await _service.LoadShortcutsAsync());
        Assert.Empty(await _service.LoadUsageAsync());
        Assert.Null(await _service.LoadExpansionAsync());
        Assert.Null(await _service.LoadSelectedTabAsync());
    }

    // Zápisy jdou na klonech v různých Caché procesech: dva souběžné téhož klíče nemají pořadí
    // a starší hodnota by mohla doběhnout poslední.
    [Fact]
    public async Task Save_WhileWriteInFlight_SendsOnlyLatestAfterIt()
    {
        var writes = HoldWrites();

        var first = _service.SaveExpansionAsync(["v1"]);
        var second = _service.SaveExpansionAsync(["v2"]);
        var third = _service.SaveExpansionAsync(["v3"]);

        Assert.Equal(["""["v1"]"""], writes.Sent("TreeExpansion"));

        writes.Complete(0);
        await first;

        Assert.Equal(["""["v1"]""", """["v3"]"""], writes.Sent("TreeExpansion", atLeast: 2));
        Assert.False(second.IsCompleted);   // jeho hodnotu nese až zápis v3

        writes.Complete(1);
        await Task.WhenAll(second, third);

        Assert.Equal(2, writes.Sent("TreeExpansion").Count);
    }

    [Fact]
    public void Save_OtherKeyWhileWriteInFlight_IsNotBlocked()
    {
        var writes = HoldWrites();

        _ = _service.SaveExpansionAsync(["v1"]);
        _ = _service.SaveSelectedTabAsync("desktop");
        _ = _service.SaveUsageAsync(new Dictionary<string, UsageEntry>());

        Assert.Single(writes.Sent("TreeExpansion"));
        Assert.Single(writes.Sent("SelectedTab"));
        Assert.Single(writes.Sent("ModuleUsage"));
    }

    [Fact]
    public async Task Save_FailedWriteInFlight_StillSendsPending()
    {
        var writes = HoldWrites();

        var first = _service.SaveSelectedTabAsync("main");
        var second = _service.SaveSelectedTabAsync("desktop");
        writes.Fail(0);
        await first;

        Assert.Equal(["\"main\"", "\"desktop\""], writes.Sent("SelectedTab", atLeast: 2));
        writes.Complete(1);
        await second;
    }

    // MLRest čeká na odpověď hodiny: po výpadku sítě by visící zápis zablokoval klíč do konce session.
    [Fact]
    public async Task Save_HungWriteInFlight_PendingGoesAfterLimit()
    {
        var writes = HoldWrites(holdOnlyFirst: true);
        var service = new ModulePreferencesService(_prefs) { InFlightLimit = TimeSpan.FromMilliseconds(50) };

        _ = service.SaveSelectedTabAsync("main");
        var second = service.SaveSelectedTabAsync("desktop");
        await second.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(["\"main\"", "\"desktop\""], writes.Sent("SelectedTab"));
    }

    // Při zavření shellu UI vlákno už nepumpuje, čekající zápis na něm nesmí záviset.
    [Fact]
    public void Save_PendingWrite_DoesNotNeedCallerContext()
    {
        var writes = HoldWrites();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new StoppedContext());
        try
        {
            _ = _service.SaveSelectedTabAsync("main");
            _ = _service.SaveSelectedTabAsync("desktop");
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        writes.Complete(0);

        Assert.Equal(["\"main\"", "\"desktop\""], writes.Sent("SelectedTab", atLeast: 2));
    }

    // VM staví instanci znovu při každé přestavbě session nad týmž úložištěm (obnova licence, přepnutí firmy).
    [Fact]
    public void Save_RebuiltInstanceOverSameStore_KeepsOrder()
    {
        var writes = HoldWrites();
        var rebuilt = new ModulePreferencesService(_prefs);

        _ = _service.SaveExpansionAsync(["v1"]);
        _ = _service.SaveExpansionAsync(["v2"]);
        _ = rebuilt.SaveExpansionAsync(["v3"]);
        Assert.Single(writes.Sent("TreeExpansion"));
        writes.Complete(0);

        Assert.Equal(["""["v1"]""", """["v3"]"""], writes.Sent("TreeExpansion", atLeast: 2));
    }

    private sealed class StoppedContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state) { }
    }

    // SaveAsync substitutu vrací task, který dokončí až test - zápis tak zůstane „v letu".
    private HeldWrites HoldWrites(bool holdOnlyFirst = false)
    {
        var held = new HeldWrites(holdOnlyFirst);
        _prefs.SaveAsync(Scope, Arg.Any<string>(), Arg.Any<JsonNode>())
            .Returns(call => held.Add(call.ArgAt<string>(1), call.ArgAt<JsonNode>(2)));
        return held;
    }

    private sealed class HeldWrites(bool holdOnlyFirst)
    {
        private readonly List<(string Key, string Json, TaskCompletionSource Done)> _writes = [];

        public Task Add(string key, JsonNode node)
        {
            var done = new TaskCompletionSource();
            lock (_writes)
            {
                if (holdOnlyFirst && _writes.Count > 0) done.SetResult();
                _writes.Add((key, node.ToJsonString(null), done));
            }
            return done.Task;
        }

        // Další zápis odchází z pokračování na poolu, ne synchronně v Complete.
        public List<string> Sent(string key, int atLeast = 0)
        {
            SpinWait.SpinUntil(() => Snapshot(key).Count >= atLeast, TimeSpan.FromSeconds(10));
            return Snapshot(key);
        }

        private List<string> Snapshot(string key)
        {
            lock (_writes) return [.. _writes.Where(w => w.Key == key).Select(w => w.Json)];
        }

        public void Complete(int index) => At(index).TrySetResult();

        public void Fail(int index) => At(index).TrySetException(new InvalidOperationException("server down"));

        private TaskCompletionSource At(int index)
        {
            lock (_writes) return _writes[index].Done;
        }
    }

    [Fact]
    public async Task Save_PrefsThrow_Swallowed()
    {
        _prefs.SaveAsync(Scope, Arg.Any<string>(), Arg.Any<JsonNode>())
            .Returns(_ => throw new InvalidOperationException("server down"));

        await _service.SaveSelectedTabAsync("desktop");
        await _service.SaveExpansionAsync([]);
        await _service.SaveChangelogSnoozeAsync(new Dictionary<string, string> { ["AD"] = "hash" });
    }
}
