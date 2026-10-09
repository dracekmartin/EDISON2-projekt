using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using EDControls.EDViewModel;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner;

/// <summary>Volba počtu sloupců skupiny; <c>Hodnota = null</c> = jako celý prvek.</summary>
public sealed record VolbaSloupcu(int? Hodnota, string Popis);

/// <summary>Řádek okna „Druhy poznámek" - jedna skupina druhů.</summary>
public class SkupinaPoznamekVM : INotifyPropertyChanged
{
    private bool _tisknout;
    private VolbaSloupcu _sloupcu;

    public SkupinaPoznamekVM(NastaveniSkupinyPoznamek model, IReadOnlyList<VolbaSloupcu> volby)
    {
        Skupina = model.Skupina;
        Nazev = NotesLegendElement.NazevSkupiny(model.Skupina);
        _tisknout = model.Tisknout;
        _sloupcu = volby.FirstOrDefault(v => v.Hodnota == model.Sloupcu) ?? volby[0];
    }

    public SkupinaPoznamek Skupina { get; }
    public string Nazev { get; }

    public bool Tisknout
    {
        get => _tisknout;
        set { _tisknout = value; OnChanged(); }
    }

    public VolbaSloupcu Sloupcu
    {
        get => _sloupcu;
        set { _sloupcu = value; OnChanged(); }
    }

    public NastaveniSkupinyPoznamek ToModel() => new() { Skupina = Skupina, Tisknout = Tisknout, Sloupcu = Sloupcu.Hodnota };

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
    public IReadOnlyList<VolbaSloupcu> VolbySloupcu { get; }

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
    /// <param name="sloupcuPrvku">Počet sloupců prvku - ukáže se u výchozí volby.</param>
    public SkupinyPoznamekDialogViewModel(IReadOnlyList<NastaveniSkupinyPoznamek> skupiny, int sloupcuPrvku)
    {
        VolbySloupcu =
        [
            new(null, $"Výchozí ({sloupcuPrvku})"),
            .. Enumerable.Range(1, 4).Select(n => new VolbaSloupcu(n, n.ToString())),
        ];
        foreach (var s in skupiny)
            Skupiny.Add(new SkupinaPoznamekVM(s, VolbySloupcu));
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
