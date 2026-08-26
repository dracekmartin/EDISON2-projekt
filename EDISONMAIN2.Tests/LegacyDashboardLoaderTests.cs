using EDControls;
using EDISONMAIN2.LegacyMiniApps;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Scheduler pro testy dávkovače: flush se nespustí sám, test si ho pustí až se přihlásí
/// všichni žadatelé (v produkci to dělá dispatcher na konci probíhající operace).
/// </summary>
internal sealed class ManualFlushScheduler
{
    private Action? _pending;

    public Action<Action> Schedule => action => _pending = action;

    public bool HasPending => _pending is not null;

    public void Flush()
    {
        var action = _pending;
        _pending = null;
        action?.Invoke();
    }
}

/// <summary>
/// Dávkovač legacy dashboardů: N panelů přihlášených v jedné dispatcher operaci = jeden
/// request s N parametry <c>appType</c> (parita s legacy <c>clsMiniApplication.LoadData</c>).
/// </summary>
public class LegacyDashboardLoaderTests
{
    private const char D = '\0';
    private const string CacheClass = "ALVA.EDISON.MiniAplikaceAPI";

    private static object[] Block(params string[] appTypes) =>
        ["", .. appTypes.SelectMany<string, object>(a => new object[]
        {
            $"appType{D}{a}",
            $"param{D}1{D}21.06.2026 09:0{a[^1]}",
            $"data{D}1{D}C1{D}1{D}Data {a}",
        })];

    private static IMLRest RestReturning(object[] output)
    {
        var rest = Substitute.For<IMLRest>();
        rest.Delimiter.Returns(D);
        rest.QueryOrThrow(CacheClass, "LoadData", Arg.Any<object[]>()).Returns(output);
        return rest;
    }

    private static LegacyDashboardLoader Loader(IMLRest rest, ManualFlushScheduler scheduler) =>
        new(new InlineRestCallRunner(rest), scheduler.Schedule);

    [Fact]
    public void OneFlush_SendsSingleRequestWithAllAppTypes()
    {
        var rest = RestReturning(Block("52001", "52005"));
        var scheduler = new ManualFlushScheduler();
        var loader = Loader(rest, scheduler);

        var first = loader.LoadAsync("52001", refresh: false);
        var second = loader.LoadAsync("52005", refresh: false);
        scheduler.Flush();

        rest.Received(1).QueryOrThrow(CacheClass, "LoadData", Arg.Any<object[]>());
        rest.Received().QueryOrThrow(CacheClass, "LoadData", Arg.Is<object[]>(a =>
            a.Length == 3 && (string)a[0] == $"refresh{D}0"
            && a.Contains($"appType{D}52001") && a.Contains($"appType{D}52005")));
        Assert.True(first.IsCompletedSuccessfully);
        Assert.True(second.IsCompletedSuccessfully);
    }

    [Fact]
    public void Flush_DistributesBlocksToTheirRequesters()
    {
        var scheduler = new ManualFlushScheduler();
        var loader = Loader(RestReturning(Block("52001", "52005")), scheduler);

        var first = loader.LoadAsync("52001", refresh: false);
        var second = loader.LoadAsync("52005", refresh: false);
        scheduler.Flush();

        Assert.Equal("Data 52001", Assert.IsType<DashboardLabel>(Assert.Single(first.Result.Sections)).Text);
        Assert.Equal("Data 52005", Assert.IsType<DashboardLabel>(Assert.Single(second.Result.Sections)).Text);
    }

    [Fact]
    public void Flush_AppTypeWithoutBlock_GetsEmpty()
    {
        // Server blok nevrátí, když pro appType nejsou data (firma agendu nemá).
        var scheduler = new ManualFlushScheduler();
        var loader = Loader(RestReturning(Block("52001")), scheduler);

        var missing = loader.LoadAsync("52014", refresh: false);
        scheduler.Flush();

        Assert.Empty(missing.Result.Sections);
        Assert.Null(missing.Result.LastUpdated);
    }

    [Fact]
    public void Refresh_IsOredOverBatch()
    {
        var rest = RestReturning(Block("52001", "52005"));
        var scheduler = new ManualFlushScheduler();
        var loader = Loader(rest, scheduler);

        loader.LoadAsync("52001", refresh: false);
        loader.LoadAsync("52005", refresh: true);
        scheduler.Flush();

        rest.Received().QueryOrThrow(CacheClass, "LoadData",
            Arg.Is<object[]>(a => (string)a[0] == $"refresh{D}1"));
    }

    [Fact]
    public void SameAppTypeTwice_ShareSingleParameterAndResult()
    {
        var rest = RestReturning(Block("52005"));
        var scheduler = new ManualFlushScheduler();
        var loader = Loader(rest, scheduler);

        var first = loader.LoadAsync("52005", refresh: false);
        var second = loader.LoadAsync("52005", refresh: false);
        scheduler.Flush();

        rest.Received().QueryOrThrow(CacheClass, "LoadData", Arg.Is<object[]>(a => a.Length == 2));
        Assert.Same(first.Result, second.Result);
    }

    [Fact]
    public async Task FailedBatch_FaultsEveryRequester()
    {
        var rest = Substitute.For<IMLRest>();
        rest.Delimiter.Returns(D);
        rest.QueryOrThrow(CacheClass, "LoadData", Arg.Any<object[]>())
            .Returns<object[]>(_ => throw new InvalidOperationException("boom"));
        var scheduler = new ManualFlushScheduler();
        var loader = Loader(rest, scheduler);

        var first = loader.LoadAsync("52001", refresh: false);
        var second = loader.LoadAsync("52005", refresh: false);
        scheduler.Flush();

        Assert.Equal("boom", (await Assert.ThrowsAsync<InvalidOperationException>(() => first)).Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => second);
    }

    [Fact]
    public void RequestAfterFlush_StartsNewBatch()
    {
        var rest = RestReturning(Block("52001", "52005"));
        var scheduler = new ManualFlushScheduler();
        var loader = Loader(rest, scheduler);

        loader.LoadAsync("52001", refresh: false);
        scheduler.Flush();
        loader.LoadAsync("52005", refresh: false);
        Assert.True(scheduler.HasPending);
        scheduler.Flush();

        rest.Received(2).QueryOrThrow(CacheClass, "LoadData", Arg.Any<object[]>());
    }
}
