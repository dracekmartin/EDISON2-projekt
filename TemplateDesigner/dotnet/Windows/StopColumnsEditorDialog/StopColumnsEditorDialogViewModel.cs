using System.Collections.ObjectModel;
using System.ComponentModel;
using EDControls.EDViewModel;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner;

// ─── ViewModel pro řádek DataGridu ──────────────────────────────────────────

public class StopColumnVM : INotifyPropertyChanged
{
    private string _key = "";
    private string _header = "";
    private float  _width = 10f;
    private bool   _visible = true;
    private string _alignmentLabel = "Vlevo";

    public string Key            { get => _key;            set { _key = value;            OnChanged(); } }
    public string Header         { get => _header;         set { _header = value;         OnChanged(); } }
    public float  Width          { get => _width;          set { _width = value;          OnChanged(); } }
    public bool   Visible        { get => _visible;        set { _visible = value;        OnChanged(); } }
    public string AlignmentLabel { get => _alignmentLabel; set { _alignmentLabel = value; OnChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([System.Runtime.CompilerServices.CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

    public StopColumn ToModel() => new()
    {
        Key       = Key,
        Header    = Header,
        Width     = Width,
        Visible   = Visible,
        Alignment = AlignmentLabel switch
        {
            "Střed"  => TextHorizontalAlignment.Center,
            "Vpravo" => TextHorizontalAlignment.Right,
            _        => TextHorizontalAlignment.Left
        }
    };

    public static StopColumnVM FromModel(StopColumn c) => new()
    {
        Key            = c.Key,
        Header         = c.Header,
        Width          = c.Width,
        Visible        = c.Visible,
        AlignmentLabel = c.Alignment switch
        {
            TextHorizontalAlignment.Center => "Střed",
            TextHorizontalAlignment.Right  => "Vpravo",
            _                              => "Vlevo"
        }
    };
}

// ─── Dialog ViewModel ───────────────────────────────────────────────────────

class StopColumnsEditorDialogViewModel : EDViewModel
{
    public ObservableCollection<StopColumnVM> ColumnVMs { get; } = [];

    public string[] AlignmentOptions { get; } = ["Vlevo", "Střed", "Vpravo"];

    /// <summary>Výsledné sloupce po OK — čteny volajícím.</summary>
    public List<StopColumn> ResultColumns { get; private set; } = [];

    // ── Výběr ──────────────────────────────────────────────────────────────

    private StopColumnVM? _selectedColumnVM;
    public StopColumnVM? SelectedColumnVM
    {
        get => _selectedColumnVM;
        set => Set(ref _selectedColumnVM, value);
    }

    // ── Callbacks ──────────────────────────────────────────────────────────

    public Action? RequestCommitEdits { get; set; }

    // ── Commands ────────────────────────────────────────────────────────────

    public RelayCommand MoveUpCommand   { get; }
    public RelayCommand MoveDownCommand { get; }
    public RelayCommand ResetCommand    { get; }

    // ── Konstruktor ─────────────────────────────────────────────────────────

    public StopColumnsEditorDialogViewModel(List<StopColumn> columns)
    {
        foreach (var col in columns)
            ColumnVMs.Add(StopColumnVM.FromModel(col));

        MoveUpCommand   = new RelayCommand(MoveUp);
        MoveDownCommand = new RelayCommand(MoveDown);
        ResetCommand    = new RelayCommand(Reset);
    }

    // ── Přesouvání ──────────────────────────────────────────────────────────

    private void MoveUp()
    {
        if (SelectedColumnVM is null) return;
        int idx = ColumnVMs.IndexOf(SelectedColumnVM);
        if (idx > 0) ColumnVMs.Move(idx, idx - 1);
    }

    private void MoveDown()
    {
        if (SelectedColumnVM is null) return;
        int idx = ColumnVMs.IndexOf(SelectedColumnVM);
        if (idx >= 0 && idx < ColumnVMs.Count - 1) ColumnVMs.Move(idx, idx + 1);
    }

    private void Reset()
    {
        ColumnVMs.Clear();
        foreach (var col in StopListElement.GetDefaultColumns())
            ColumnVMs.Add(StopColumnVM.FromModel(col));
    }

    // ── OK ──────────────────────────────────────────────────────────────────

    protected override void OnOk()
    {
        RequestCommitEdits?.Invoke();
        ResultColumns = ColumnVMs.Select(vm => vm.ToModel()).ToList();
        CloseDialog(true);
    }
}
