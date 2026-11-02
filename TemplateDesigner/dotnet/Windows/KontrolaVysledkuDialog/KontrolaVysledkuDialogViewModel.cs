using EDControls.EDViewModel;
using TemplateDesigner.Models;

namespace TemplateDesigner;

/// <summary>Výsledek kontroly vybraných linek (`B178`) - hlášky ve vlastním okně, aby nezabíraly
/// místo formuláři (rozhodnutí Jan 24. 9.). Z návrháře se otevírá hned a ukazuje průběh.</summary>
public class KontrolaVysledkuDialogViewModel : EDViewModel
{
    public KontrolaVysledkuDialogViewModel(string nadpis, IReadOnlyList<Hlaseni> hlaseni)
    {
        _nadpis = nadpis;
        _hlaseni = hlaseni;
    }

    /// <summary>Název instance agendy pro titulek okna.</summary>
    public NazevInstance Nazev { get; init; } = NazevInstance.Obecny;

    /// <summary>„Kontrola vybraných linek".</summary>
    public string Titulek => $"Kontrola vybraných {Nazev.GenitivMnozne}";

    private string _nadpis;
    /// <summary>„Zkontrolováno 12 linek: 1 chyba, 2 upozornění", během kontroly její průběh.</summary>
    public string Nadpis { get => _nadpis; set => Set(ref _nadpis, value); }

    private IReadOnlyList<Hlaseni> _hlaseni;
    public IReadOnlyList<Hlaseni> Hlaseni { get => _hlaseni; set => Set(ref _hlaseni, value); }

    private bool _probiha;
    public bool Probiha { get => _probiha; set => Set(ref _probiha, value); }

    private double _prubeh;
    /// <summary>Hotová část 0-1.</summary>
    public double Prubeh { get => _prubeh; set => Set(ref _prubeh, value); }

    /// <summary>Klik na řádek - návrhář přepne na linku hlášky a vybere prvek. Null = řádky
    /// se neproklikávají (tiskové okno leží nad návrhářem).</summary>
    public Action<Hlaseni>? RequestVybrat { get; set; }

    private RelayCommand<Hlaseni>? _vybrat;
    public RelayCommand<Hlaseni> VybratCommand => _vybrat ??= Command<Hlaseni>(h =>
    {
        if (h is not null) RequestVybrat?.Invoke(h);
    });
}
