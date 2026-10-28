using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using EDControls.EDViewModel;

namespace TemplateDesigner.Controls;

/// <summary>
/// Logika sdílené záložky Sloupce (<see cref="ColumnsTab"/>) pro okna všech tabulek se sloupci: výběr,
/// posun, přidání a odebrání, hlídání součtu šířek, přizpůsobení šířce prvku a reset stylu. Okno prvku
/// jen dodá řádky a řekne, co u něj platí (init vlastnosti) - do 28. 10. měla každá ze čtyř tabulek
/// vlastní kopii a texty i chování se rozešly (`B194` E13c).
/// </summary>
public sealed class ColumnsTabViewModel : INotifyPropertyChanged
{
    public ColumnsTabViewModel(IEnumerable<ColumnRowVM> rows)
    {
        Rows.CollectionChanged += OnRowsChanged;
        foreach (var row in rows) Rows.Add(row);
        SelectedRow = Rows.FirstOrDefault();

        MoveUpCommand = new RelayCommand(() => Move(-1));
        MoveDownCommand = new RelayCommand(() => Move(+1));
        AddCommand = new RelayCommand(Add);
        RemoveCommand = new RelayCommand(Remove);
        ResetStyleCommand = new RelayCommand(() => { SelectedRow?.ResetStyle(); StylesReset?.Invoke(); });
        ResetAllCommand = new RelayCommand(() => { foreach (var row in Rows) row.ResetStyle(); StylesReset?.Invoke(); });
        FitCommand = new RelayCommand(FitToElementWidth);
    }

    public ObservableCollection<ColumnRowVM> Rows { get; } = [];

    // ── Co platí v okně ──────────────────────────────────────────────────

    /// <summary>Šířka prvku pro hlídání přesahu; null = okno přesah nepočítá (Minuty jsou pivot,
    /// šířky skupin si dopočítají samy).</summary>
    public Func<float>? ElementWidth { get; init; }

    /// <summary>Nový sloupec; null = sloupce nejde přidávat ani odebírat (dává je prvek).</summary>
    public Func<ColumnRowVM>? NewRow { get; init; }

    /// <summary>Nabídnout „Přizpůsobit sloupce k šířce prvku" při přesahu.</summary>
    public bool CanFit { get; init; }

    /// <summary>Záhlaví na víc řádků - jen kde tabulka podle počtu řádků zvětší výšku záhlaví,
    /// jinak by druhý řádek přetekl do buněk.</summary>
    public bool MultilineHeader { get; init; }

    /// <summary>Obecná nápověda pod šířkou; řádek ji může přebít vlastní (<see cref="ColumnRowVM.WidthHint"/>).</summary>
    public string WidthHint { get; init; } = "Prázdná šířka = automaticky: sloupce bez šířky si rozdělí zbývající šířku prvku.";

    /// <summary>Co se stane, když sloupce přesáhnou šířku prvku (věta za hlášením o přesahu).</summary>
    public string OverflowNote { get; init; } = "Tabulka se zmenší, aby se vešla.";

    /// <summary>Styl sloupce ve formuláři se sadou stylu formuláře prvku; false = dosavadní editor
    /// stylu (okna, která na formulář ještě nepřešla).</summary>
    public bool UsesStyleForm { get; init; }
    public bool UsesStyleEditor => !UsesStyleForm;

    /// <summary>Reset stylu sloupce - formulář stylu se musí znovu naplnit, sám změnu nepozná.</summary>
    public event Action? StylesReset;

    public bool CanAddRemove => NewRow is not null;

    public string? HeaderToolTip => MultilineHeader
        ? "Nový řádek záhlaví: Enter nebo \\n (např. dvojjazyčné záhlaví Zastávka\\nStop)."
        : null;

    // ── Výběr ────────────────────────────────────────────────────────────

    private ColumnRowVM? _selectedRow;
    public ColumnRowVM? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (ReferenceEquals(_selectedRow, value)) return;
            _selectedRow = value;
            OnChanged();
            OnChanged(nameof(HasSelection));
            OnChanged(nameof(SelectedWidthHint));
        }
    }

    public bool HasSelection => _selectedRow is not null;

    public string SelectedWidthHint => _selectedRow?.WidthHint ?? WidthHint;

    // ── Šířka ────────────────────────────────────────────────────────────

    /// <summary>Součet pevných šířek viditelných sloupců, které se do šířky prvku počítají (mm).</summary>
    public float TotalWidth
    {
        get
        {
            float element = ElementWidth?.Invoke() ?? 0f;
            return Rows.Where(r => r.Visible && r.CountsTowardWidth).Sum(r => r.FixedWidthMm(element));
        }
    }

    public bool HasColumnOverflow => ElementWidth is { } width && TotalWidth > width() + 0.01f;

    public string ColumnOverflowText => ElementWidth is { } width
        ? $"⚠ Součet šířek sloupců ({TotalWidth:0.#} mm) je větší než šířka prvku ({width():0.#} mm). {OverflowNote}"
        : "";

    public bool ShowFit => CanFit && HasColumnOverflow;

    // ── Příkazy ──────────────────────────────────────────────────────────

    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }
    public RelayCommand AddCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand ResetStyleCommand { get; }
    public RelayCommand ResetAllCommand { get; }
    public RelayCommand FitCommand { get; }

    /// <summary>Nahradí řádky (Tabulka z dat po výběru zdroje dat).</summary>
    public void ReplaceRows(IEnumerable<ColumnRowVM> rows)
    {
        // Clear hlásí Reset bez starých položek, odhlásit je proto musíme sami.
        foreach (var row in Rows) row.PropertyChanged -= OnRowChanged;
        Rows.Clear();
        foreach (var row in rows) Rows.Add(row);
        SelectedRow = Rows.FirstOrDefault();
    }

    /// <summary>Prohodí vybraný sloupec se sousedním posouvatelným. Pevné řádky (hodiny, spoje) zůstávají
    /// na svém místě a nejde je přeskočit na začátek ani na konec seznamu.</summary>
    private void Move(int direction)
    {
        if (_selectedRow is not { CanMove: true } row) return;
        int from = Rows.IndexOf(row);
        int to = from + direction;
        while (to >= 0 && to < Rows.Count && !Rows[to].CanMove) to += direction;
        if (to < 0 || to >= Rows.Count) return;

        var other = Rows[to];
        Rows[to] = row;
        Rows[from] = other;
        SelectedRow = row;
    }

    private void Add()
    {
        if (NewRow is null) return;
        var row = NewRow();
        Rows.Add(row);
        SelectedRow = row;
    }

    private void Remove()
    {
        if (NewRow is null || _selectedRow is not { } row) return;
        int index = Rows.IndexOf(row);
        Rows.Remove(row);
        SelectedRow = Rows.Count > 0 ? Rows[Math.Min(index, Rows.Count - 1)] : null;
    }

    /// <summary>Zmenší nebo zvětší pevné šířky ve stejném poměru, aby se součet rovnal šířce prvku.
    /// Automatické šířky zůstávají automatické.</summary>
    private void FitToElementWidth()
    {
        if (ElementWidth is not { } width) return;
        float total = TotalWidth;
        if (total <= 0.01f) return;
        float scale = width() / total;
        foreach (var row in Rows.Where(r => r.Visible && r.CountsTowardWidth))
            if (row.Width is float w) row.Width = (float)Math.Round(w * scale, 2);
    }

    // ── Změny řádků ──────────────────────────────────────────────────────

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (ColumnRowVM row in e.OldItems) row.PropertyChanged -= OnRowChanged;
        if (e.NewItems is not null)
            foreach (ColumnRowVM row in e.NewItems) row.PropertyChanged += OnRowChanged;
        RaiseWidth();
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ColumnRowVM.Width) or nameof(ColumnRowVM.Visible))
            RaiseWidth();
    }

    private void RaiseWidth()
    {
        OnChanged(nameof(TotalWidth));
        OnChanged(nameof(HasColumnOverflow));
        OnChanged(nameof(ColumnOverflowText));
        OnChanged(nameof(ShowFit));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
