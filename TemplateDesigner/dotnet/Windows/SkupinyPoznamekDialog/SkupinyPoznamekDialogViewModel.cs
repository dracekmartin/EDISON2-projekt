using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using EDControls.EDViewModel;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner;

/// <summary>Řádek okna „Druhy poznámek" - jedna skupina druhů.</summary>
public class SkupinaPoznamekVM : INotifyPropertyChanged
{
    private bool _tisknout;
    private int _sloupcu;
    private readonly int _sloupcuPrvku;

    /// <param name="sloupcuPrvku">Počet sloupců prvku - tolik ukáže skupina bez vlastního počtu.</param>
    public SkupinaPoznamekVM(NastaveniSkupinyPoznamek model, int sloupcuPrvku)
    {
        Skupina = model.Skupina;
        Nazev = NotesLegendElement.NazevSkupiny(model.Skupina);
        _tisknout = model.Tisknout;
        _sloupcuPrvku = sloupcuPrvku;
        _sloupcu = model.Sloupcu ?? sloupcuPrvku;
    }

    public SkupinaPoznamek Skupina { get; }
    public string Nazev { get; }

    public bool Tisknout
    {
        get => _tisknout;
        set { _tisknout = value; OnChanged(); }
    }

    public int Sloupcu
    {
        get => _sloupcu;
        set { _sloupcu = value; OnChanged(); }
    }

    /// <summary>Počet shodný s prvkem se uloží jako „jako prvek", takže skupina dál sleduje jeho
    /// Počet sloupců; odlišný počet se uloží napevno.</summary>
    public NastaveniSkupinyPoznamek ToModel() => new()
    {
        Skupina = Skupina, Tisknout = Tisknout, Sloupcu = Sloupcu == _sloupcuPrvku ? null : Sloupcu,
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? p = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}

/// <summary>
/// Okno „Druhy poznámek" (`#7487`): pořadí skupin druhů poznámek linkového JŘ, které se z nich
/// tisknou a kolik má každá sloupců.
/// </summary>
public class SkupinyPoznamekDialogViewModel : EDViewModel
{
    public ObservableCollection<SkupinaPoznamekVM> Skupiny { get; } = [];
    public IReadOnlyList<int> VolbySloupcu { get; } = [1, 2, 3, 4];

    private SkupinaPoznamekVM? _vybrana;
    public SkupinaPoznamekVM? Vybrana
    {
        get => _vybrana;
        set => Set(ref _vybrana, value);
    }

    public RelayCommand NahoruCommand { get; }
    public RelayCommand DoluCommand { get; }

    /// <summary>Výsledné nastavení po OK, všechny skupiny v pořadí tisku.</summary>
    public List<NastaveniSkupinyPoznamek> Vysledek { get; private set; } = [];

    /// <param name="skupiny">Všechny skupiny v pořadí tisku (<see cref="NotesLegendElement.EfektivniSkupiny"/>).</param>
    /// <param name="sloupcuPrvku">Počet sloupců prvku - předvyplní se skupinám bez vlastního počtu.</param>
    public SkupinyPoznamekDialogViewModel(IReadOnlyList<NastaveniSkupinyPoznamek> skupiny, int sloupcuPrvku)
    {
        foreach (var s in skupiny)
            Skupiny.Add(new SkupinaPoznamekVM(s, sloupcuPrvku));
        Vybrana = Skupiny.FirstOrDefault();

        NahoruCommand = Command(() => Posun(-1));
        DoluCommand   = Command(() => Posun(+1));
    }

    private void Posun(int o)
    {
        if (Vybrana is null) return;
        int idx = Skupiny.IndexOf(Vybrana);
        int cil = idx + o;
        if (idx >= 0 && cil >= 0 && cil < Skupiny.Count)
            Skupiny.Move(idx, cil);
    }

    protected override void OnOk()
    {
        Vysledek = [.. Skupiny.Select(s => s.ToModel())];
        CloseDialog(true);
    }
}
