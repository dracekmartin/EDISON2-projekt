using System.ComponentModel;
using ALWA.SYS;
using EDControls.EDList;
using EDControls.EDViewModel;
using EDControls.UserPrefs;
using EDTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace EDControls.Tests;

/// <summary>
/// Stav EDListu bez WPF: lifecycle LoadList (výměna řádků, odhlášení starých, kotva), zaškrtnutí,
/// řazení a layout. Pasti z EDList/CLAUDE.md, které dřív hlídal jen code-behind.
/// </summary>
public class EDListControllerTests
{
    private const string Cls = "ALVA.MockList";

    private readonly IMLRest _mock = MLRestMocks.Create();
    private readonly IUserPrefsService _prefs = Substitute.For<IUserPrefsService>();
    private readonly EDListController _controller;

    public EDListControllerTests()
    {
        var services = new EDServices(_mock, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>())
        { UserPrefs = _prefs };
        _controller = new EDListController(() => services);
        _mock.With(Arg.Any<int?>(), Arg.Any<string?>(), Arg.Any<string?>()).Returns(_mock);   // klon = tentýž mock
        _mock.QueryOrThrow(Cls, "InitializeList", Arg.Any<CVar>()).Returns(Output("""[{"key":"name","label":"Název"}]"""));
    }

    [Fact]
    public void LoadList_WithoutColumns_ThrowsBeforeQuery()
    {
        Assert.Throws<InvalidOperationException>(() => _controller.LoadList(Cls, null));
        _mock.DidNotReceive().QueryOrThrow(Cls, "LoadList", Arg.Any<CVar>());
    }

    [Fact]
    public void InitializeList_WithoutCacheClass_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _controller.InitializeList(""));
    }

    [Fact]
    public void LoadList_ReplacesRows_OldRowsNoLongerRaiseCheckedRowsChanged()
    {
        var old = Load("1", "2");
        Load("3");
        int raised = 0;
        _controller.CheckedRowsChanged += () => raised++;

        old[0].IsChecked = true;

        Assert.Equal(0, raised);
        Assert.Equal(["3"], _controller.Rows.Select(r => r.Id));
    }

    [Fact]
    public void LoadList_RaisesCheckedThenRowsReplaced_AndResetsAnchor()
    {
        var rows = Load("1", "2");
        _controller.CheckFromClick(rows[0], true, null);
        var events = new List<string>();
        _controller.CheckedRowsChanged += () => events.Add("checked");
        _controller.RowsReplaced += () => events.Add("rows");

        Load("1", "2");

        Assert.Equal(["checked", "rows"], events);
        Assert.Null(_controller.CheckAnchor);
        Assert.Empty(_controller.CheckedRowIds);
    }

    [Fact]
    public async Task SetData_DropsAsyncResponseInFlight()
    {
        Init();
        var release = new TaskCompletionSource();
        _mock.QueryOrThrow(Cls, "LoadList", Arg.Any<CVar>()).Returns(_ =>
        {
            release.Task.Wait(TimeSpan.FromSeconds(10));
            return RowsOutput("stara");
        });

        var load = _controller.LoadListAsync(Cls, null);
        _controller.SetData([new ListColumn { Key = "name" }], [new Dictionary<string, string?> { ["id"] = "zvenci" }]);
        release.SetResult();
        await load;

        Assert.Equal(["zvenci"], _controller.Rows.Select(r => r.Id));
    }

    [Fact]
    public void ToggleAllFromHeader_RaisesOnce()
    {
        var rows = Load("1", "2", "3");
        int raised = 0;
        _controller.CheckedRowsChanged += () => raised++;

        _controller.ToggleAllFromHeader(rows);

        Assert.Equal(1, raised);
        Assert.Equal(["1", "2", "3"], _controller.CheckedRowIds);
    }

    [Fact]
    public void HeaderCheckState_NoneSomeAll()
    {
        var rows = Load("1", "2");
        Assert.False(_controller.HeaderCheckState(rows));
        rows[0].IsChecked = true;
        Assert.Null(_controller.HeaderCheckState(rows));
        rows[1].IsChecked = true;
        Assert.True(_controller.HeaderCheckState(rows));
    }

    [Fact]
    public void HeaderCheckState_CountsOnlyDisplayed()
    {
        var rows = Load("1", "2", "3");
        rows[2].IsChecked = true;   // skrytý hledáním
        Assert.False(_controller.HeaderCheckState([rows[0], rows[1]]));

        rows[0].IsChecked = true;
        rows[1].IsChecked = true;
        Assert.True(_controller.HeaderCheckState([rows[0], rows[1]]));
    }

    [Fact]
    public void ToggleAllFromHeader_AllChecked_UnchecksAll_OtherwiseChecksAll()
    {
        var rows = Load("1", "2");
        rows[0].IsChecked = true;

        _controller.ToggleAllFromHeader(rows);
        Assert.True(_controller.HeaderCheckState(rows));

        _controller.ToggleAllFromHeader(rows);
        Assert.False(_controller.HeaderCheckState(rows));
    }

    [Fact]
    public void ToggleAllFromHeader_ChecksOnlyDisplayed_KeepsHiddenChecked()
    {
        var rows = Load("1", "2", "3", "4");
        rows[3].IsChecked = true;   // skrytý hledáním
        IReadOnlyList<EDListRow> displayed = [rows[0], rows[1]];

        _controller.ToggleAllFromHeader(displayed);
        Assert.Equal(["1", "2", "4"], _controller.CheckedRowIds);

        _controller.ToggleAllFromHeader(displayed);
        Assert.Equal(["4"], _controller.CheckedRowIds);
    }

    [Fact]
    public void CheckFromClick_WithDisplayedRows_ChecksRangeFromAnchorInDisplayedOrder()
    {
        var rows = Load("1", "2", "3", "4");
        IReadOnlyList<EDListRow> displayed = [rows[3], rows[2], rows[1], rows[0]];   // seřazeno sestupně
        _controller.CheckFromClick(rows[3], true, null);

        _controller.CheckFromClick(rows[1], true, displayed);

        Assert.Equal(["2", "3", "4"], _controller.CheckedRowIds);
        Assert.Same(rows[1], _controller.CheckAnchor);
    }

    [Fact]
    public void CheckFromClick_AnchorOutsideDisplayed_ChecksOnlyClickedRow()
    {
        var rows = Load("1", "2", "3");
        _controller.CheckFromClick(rows[0], true, null);

        _controller.CheckFromClick(rows[2], true, [rows[1], rows[2]]);   // kotvu skryl filtr

        Assert.Equal(["1", "3"], _controller.CheckedRowIds);
    }

    [Fact]
    public void ToggleSort_WithoutShift_ReplacesSort_WithShift_AddsColumn()
    {
        Assert.Equal(ListSortDirection.Ascending, _controller.ToggleSort("a", null, additive: false));
        Assert.Equal(ListSortDirection.Descending, _controller.ToggleSort("a", ListSortDirection.Ascending, additive: false));
        _controller.ToggleSort("b", null, additive: true);
        Assert.Equal([("a", ListSortDirection.Descending), ("b", ListSortDirection.Ascending)], _controller.SortSpecs);

        _controller.ToggleSort("c", null, additive: false);

        Assert.Equal([("c", ListSortDirection.Ascending)], _controller.SortSpecs);
        Assert.NotNull(_controller.SortComparer);
    }

    [Fact]
    public void RestoreSort_SkipsMissingColumn_PruneSortDropsVanished()
    {
        _controller.RestoreSort(
            [new SortPref { Key = "a", Direction = "desc" }, new SortPref { Key = "zmizel" }, new SortPref { Key = "b" }],
            key => key != "zmizel");
        Assert.Equal([("a", ListSortDirection.Descending), ("b", ListSortDirection.Ascending)], _controller.SortSpecs);

        Assert.True(_controller.PruneSort(key => key == "b"));
        Assert.False(_controller.PruneSort(key => key == "b"));
        Assert.Equal([("b", ListSortDirection.Ascending)], _controller.SortSpecs);
    }

    [Fact]
    public async Task LoadLayoutAsync_WithoutHostId_DoesNotTouchPrefs()
    {
        Assert.Null(await _controller.LoadLayoutAsync(null));
        Assert.Null(await _controller.LoadLayoutAsync(""));
        await _prefs.DidNotReceiveWithAnyArgs().LoadAsync(default!, default!);
    }

    /// <summary>Výpadek transportu (CacheUserPrefsService.Load hází) nesmí shodit seznam.</summary>
    [Fact]
    public async Task LoadLayoutAsync_PrefsThrow_ReturnsNull()
    {
        _prefs.LoadAsync(EDListController.PrefsScope, "Host").ThrowsAsync(new InvalidOperationException("síť"));

        Assert.Null(await _controller.LoadLayoutAsync("Host"));
    }

    private void Init() => _controller.InitializeList(Cls);

    private List<EDListRow> Load(params string[] ids)
    {
        if (_controller.Columns is null) Init();
        _mock.QueryOrThrow(Cls, "LoadList", Arg.Any<CVar>()).Returns(RowsOutput(ids));
        _controller.LoadList(Cls, null);
        return [.. _controller.Rows];
    }

    private static CVar RowsOutput(params string[] ids)
    {
        var output = new CVar();
        for (int i = 0; i < ids.Length; i++)
            output[(i + 1).ToString()] = new CVar($$"""{"id":"{{ids[i]}}","name":"radek {{ids[i]}}"}""");
        return output;
    }

    private static CVar Output(string json)
    {
        var output = new CVar();
        output["1"] = new CVar(json);
        return output;
    }
}
