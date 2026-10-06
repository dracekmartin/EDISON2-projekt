using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ALWA.SYS;
using EDControls.EDViewModel;

namespace EDControls.EDList;

/// <summary>
/// Stav EDListu bez WPF: sloupce, řádky, zaškrtnutí, řazení, načtení layoutu (zápis odkládá view).
/// Služby čte per volání (<c>EDServices.Current</c> se mění s přepnutím session), hodnoty DP
/// dostává parametrem - čte je view na UI vlákně.
/// </summary>
internal sealed class EDListController(Func<EDServices> services, IRestCallRunner? runner = null)
{
    internal const string PrefsScope = "EDControls.EDList";
    private const string NotConfigured = "EDList: MLRest a CacheClass musí být nastaveny před použitím.";

    // Async dotazy na klonu: sdílenou instanci souběžně používají jiné seznamy i synchronní
    // EDInputForm a RestStatus je per-instance (prohozená chyba / cizí výjimka).
    private readonly IRestCallRunner _runner = runner
        ?? new RestCallRunner(() => services().MLRest ?? throw new InvalidOperationException(NotConfigured));

    // Řadí async dotazy téhož listu: sloupce souběžného InitializeListAsync musí dosednout
    // dřív než řádky LoadListAsync (TD picker + EDFilter).
    private readonly SemaphoreSlim _queryGate = new(1, 1);
    private int _loadGeneration;

    private readonly List<(string Key, ListSortDirection Direction)> _sortSpecs = [];
    private bool _suppressCheckedEvents;

    public IReadOnlyList<ListColumn>? Columns { get; private set; }

    public BulkObservableCollection<EDListRow> Rows { get; } = new();

    /// <summary>Kotva pro Shift+klik výběr rozsahu (poslední řádek, na jehož checkbox se kliklo / dvojklik).</summary>
    public EDListRow? CheckAnchor { get; private set; }

    public IReadOnlyList<(string Key, ListSortDirection Direction)> SortSpecs => _sortSpecs;

    /// <summary>Comparer pro <c>ListCollectionView.CustomSort</c>, null = bez řazení.</summary>
    public IComparer? SortComparer => _sortSpecs.Count == 0
        ? null
        : new EDListRowComparer([.. _sortSpecs.Select(s => (s.Key, s.Direction == ListSortDirection.Descending))]);

    public IReadOnlyList<string> CheckedRowIds => Rows.Where(r => r.IsChecked).Select(r => r.Id).ToList();

    /// <summary>true / false / null = část zaškrtnutá.</summary>
    public bool? HeaderCheckState
    {
        get
        {
            int total = Rows.Count;
            int checkedCount = Rows.Count(r => r.IsChecked);
            return total == 0 || checkedCount == 0 ? false
                 : checkedCount == total ? true
                 : null;
        }
    }

    public event Action? ColumnsChanged;
    public event Action? RowsReplaced;
    public event Action? CheckedRowsChanged;

    // ── Data ──────────────────────────────────────────────────────────────────

    public void InitializeList(string cacheClass) =>
        ApplyColumns(EnsureConfigured(cacheClass).QueryOrThrow(cacheClass, "InitializeList", new CVar()));

    public async Task InitializeListAsync(string cacheClass)
    {
        EnsureConfigured(cacheClass);
        ApplyColumns(await RunQueryAsync(rest => rest.QueryOrThrow(cacheClass, "InitializeList", new CVar())));
    }

    public void LoadList(string cacheClass, IReadOnlyDictionary<string, string>? filter)
    {
        var mlRest = EnsureConfigured(cacheClass);
        EnsureColumns();   // sync cesta frontu dotazů obchází, sloupce už nemůžou dorazit
        _loadGeneration++;   // sync načtení je autoritativní - případná async odpověď v letu se zahodí
        ApplyRows(mlRest.QueryOrThrow(cacheClass, "LoadList", EDListParsing.BuildLoadInput(filter)));
    }

    public async Task LoadListAsync(string cacheClass, IReadOnlyDictionary<string, string>? filter)
    {
        EnsureConfigured(cacheClass);
        var input = EDListParsing.BuildLoadInput(filter);
        var generation = ++_loadGeneration;

        var output = await RunQueryAsync(rest => rest.QueryOrThrow(cacheClass, "LoadList", input));

        if (generation != _loadGeneration) return;   // mezitím přišel novější požadavek
        ApplyRows(output);
    }

    public void SetData(IReadOnlyList<ListColumn> columns, IEnumerable<IReadOnlyDictionary<string, string?>> rows)
    {
        _loadGeneration++;   // data zvenčí jsou autoritativní - async odpověď v letu se zahodí
        Columns = columns;
        ColumnsChanged?.Invoke();
        ApplyRows([.. rows.Select(r => EDListParsing.BuildRow(r, columns)).OfType<EDListRow>()]);
    }

    private IMLRest EnsureConfigured(string cacheClass)
    {
        var mlRest = services().MLRest;
        if (mlRest is null || string.IsNullOrEmpty(cacheClass))
            throw new InvalidOperationException(NotConfigured);
        return mlRest;
    }

    private async Task<CVar> RunQueryAsync(Func<IMLRest, CVar> query)
    {
        await _queryGate.WaitAsync();
        try { return await _runner.RunAsync(query); }
        finally { _queryGate.Release(); }
    }

    private void ApplyColumns(CVar output)
    {
        Columns = EDListParsing.ParseColumns(output);
        ColumnsChanged?.Invoke();
    }

    // Kontrola až po dotazu, ne před ním: host smí pustit InitializeListAsync a LoadListAsync
    // souběžně (TD picker + EDFilter), pořadí odpovědí drží _queryGate.
    // ToList: ParseRows je líný iterátor - druhý průchod by vyrobil jiné instance řádků
    // a odběr změn by visel mimo seznam.
    private void ApplyRows(CVar output)
    {
        EnsureColumns();
        ApplyRows(EDListParsing.ParseRows(output, Columns).ToList());
    }

    // Text nesmí vinit hosta: k tomuhle stavu vede i selhaný InitializeListAsync.
    private void EnsureColumns()
    {
        if (Columns is null)
            throw new InvalidOperationException("EDList: sloupce nejsou načtené (InitializeList neproběhl nebo selhal), seznam nejde naplnit.");
    }

    private void ApplyRows(List<EDListRow> newRows)
    {
        foreach (var oldRow in Rows) oldRow.PropertyChanged -= OnRowPropertyChanged;
        CheckAnchor = null;

        foreach (var row in newRows) row.PropertyChanged += OnRowPropertyChanged;
        Rows.ReplaceAll(newRows);

        CheckedRowsChanged?.Invoke();
        RowsReplaced?.Invoke();
    }

    // ── Zaškrtnutí ────────────────────────────────────────────────────────────

    public void SetChecked(string rowId, bool isChecked)
    {
        var row = Rows.FirstOrDefault(r => r.Id == rowId);
        if (row is not null) row.IsChecked = isChecked;
    }

    public void SetAllChecked(bool value) => CheckBatch(Rows, value);

    /// <summary>Klik na checkbox v záhlaví: všechno zaškrtnuté → odškrtnout, jinak zaškrtnout vše.</summary>
    public void ToggleAllFromHeader() => SetAllChecked(HeaderCheckState != true);

    /// <summary>
    /// Klik na checkbox řádku. S <paramref name="displayedRows"/> (Shift) zaškrtne rozsah od kotvy
    /// v zobrazeném pořadí - respektuje řazení i filtr; kotva mimo pohled = jen tenhle řádek.
    /// </summary>
    public void CheckFromClick(EDListRow row, bool target, IReadOnlyList<EDListRow>? displayedRows)
    {
        int a = CheckAnchor is null || displayedRows is null ? -1 : IndexOf(displayedRows, CheckAnchor);
        int b = displayedRows is null ? -1 : IndexOf(displayedRows, row);
        if (a >= 0 && b >= 0)
        {
            if (a > b) (a, b) = (b, a);
            CheckBatch(displayedRows!.Skip(a).Take(b - a + 1), target);
        }
        else row.IsChecked = target;
        CheckAnchor = row;
    }

    /// <summary>Dvojklik v Check mode přepne řádek (rychlejší než trefit úzký checkbox).</summary>
    public void ToggleChecked(EDListRow row)
    {
        row.IsChecked = !row.IsChecked;
        CheckAnchor = row;
    }

    private void CheckBatch(IEnumerable<EDListRow> rows, bool value)
    {
        _suppressCheckedEvents = true;
        try
        {
            foreach (var row in rows) row.IsChecked = value;
        }
        finally { _suppressCheckedEvents = false; }
        CheckedRowsChanged?.Invoke();
    }

    private static int IndexOf(IReadOnlyList<EDListRow> rows, EDListRow row)
    {
        for (int i = 0; i < rows.Count; i++)
            if (ReferenceEquals(rows[i], row)) return i;
        return -1;
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressCheckedEvents) return;
        if (e.PropertyName == nameof(EDListRow.IsChecked)) CheckedRowsChanged?.Invoke();
    }

    // ── Řazení ────────────────────────────────────────────────────────────────

    /// <summary>Klik na záhlaví sloupce: obrátí jeho směr; bez <paramref name="additive"/> (Shift) zahodí ostatní.</summary>
    public ListSortDirection ToggleSort(string key, ListSortDirection? current, bool additive)
    {
        var direction = current == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;
        if (!additive) _sortSpecs.Clear();
        _sortSpecs.RemoveAll(s => s.Key == key);
        _sortSpecs.Add((key, direction));
        return direction;
    }

    /// <summary>Řazení z uloženého layoutu nahradí aktuální; klíče bez sloupce přeskočí.</summary>
    public void RestoreSort(IEnumerable<SortPref> prefs, Func<string, bool> hasColumn)
    {
        _sortSpecs.Clear();
        foreach (var pref in prefs)
            if (hasColumn(pref.Key))
                _sortSpecs.Add((pref.Key, pref.Direction == "desc" ? ListSortDirection.Descending : ListSortDirection.Ascending));
    }

    /// <summary>Po přestavbě sloupců zahodí řazení podle sloupce, který zmizel. True = řazení se změnilo.</summary>
    public bool PruneSort(Func<string, bool> hasColumn) => _sortSpecs.RemoveAll(s => !hasColumn(s.Key)) > 0;

    // ── Layout (UserPrefs) ────────────────────────────────────────────────────

    /// <summary>Uložený layout, nebo null (bez HostId, nic uloženého, chyba). Best-effort.</summary>
    public async Task<ColumnLayout?> LoadLayoutAsync(string? hostId)
    {
        if (string.IsNullOrEmpty(hostId)) return null;
        try
        {
            return ColumnLayoutSerializer.Deserialize(await services().UserPrefs.LoadAsync(PrefsScope, hostId));
        }
        catch { return null; /* best-effort */ }
    }
}
