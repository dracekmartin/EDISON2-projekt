using System.Collections.ObjectModel;
using System.Windows.Input;
using ALWA.SYS;
using EDControls.EDViewModel;
using EDControls.MiniApps;

namespace EDISONMAIN2.LegacyMiniApps;

/// <summary>Jeden řádek dispečinku (jedna jízda/doklad). Kontrakt odpovídá pořadí polí v
/// <c>ALVA.MHD2.PlanDopravy.API.DispecinkMiniApp.GetJizdy</c>.</summary>
public sealed record DispecinkRow(
    string Smena, string Linka, string TypSmeny, string Ts,
    string Nastup, string Konec, string Ridic, string VuzRz, string VuzEvc, bool Vyjeto);

/// <summary>Položka selektoru pracoviště.</summary>
public sealed record PracovisteItem(string Id, string Nazev)
{
    public override string ToString() => Nazev;
}

/// <summary>
/// MHD dispečink — read-only seznam jízd pro zvolené pracoviště a den. Čte přes MLRest z
/// <c>ALVA.MHD2.PlanDopravy.API.DispecinkMiniApp</c> (GetPracoviste / GetJizdy).
/// </summary>
public sealed class DispecinkMiniAppViewModel : EDMiniAppViewModel, IRefreshableMiniApp
{
    private const string CacheClass = "ALVA.MHD2.PlanDopravy.API.DispecinkMiniApp";

    public DispecinkMiniAppViewModel() => LoadCommand = Command(LoadJizdy);

    public DispecinkMiniAppViewModel(EDServices services) : base(services) => LoadCommand = Command(LoadJizdy);

    public override string Title => "Dispečink";

    public ObservableCollection<PracovisteItem> Pracoviste { get; } = [];

    private PracovisteItem? _selectedPracoviste;
    public PracovisteItem? SelectedPracoviste { get => _selectedPracoviste; set => Set(ref _selectedPracoviste, value); }

    private DateTime _datum = DateTime.Today;
    public DateTime Datum { get => _datum; set => Set(ref _datum, value); }

    public ObservableCollection<DispecinkRow> Jizdy { get; } = [];

    public ICommand LoadCommand { get; }

    // Po otevření panelu naplň selektor pracovišť (a vyber první).
    public override void OnOpened() => LoadPracoviste();

    /// <summary>Hromadný refresh ze shellu (<see cref="IRefreshableMiniApp"/>) — znovu načte jízdy.</summary>
    public void Refresh() => LoadJizdy();

    private void LoadPracoviste()
    {
        CVar output = MLRest!.QueryOrThrow(CacheClass, "GetPracoviste", new CVar());
        Pracoviste.Clear();
        foreach (KeyValuePair<string, CVar> kvp in output)
        {
            if (kvp.Key == "ERR") continue;
            CList row = kvp.Value;
            Pracoviste.Add(new PracovisteItem(row.GetString(1), row.GetString(2)));
        }
        SelectedPracoviste = Pracoviste.FirstOrDefault();
    }

    private void LoadJizdy()
    {
        if (SelectedPracoviste is null) return;

        var input = new CVar();
        input["pracoviste"] = new CVar(SelectedPracoviste.Id);
        input["datum"] = new CVar(Datum.ToString("yyyy-MM-dd"));

        CVar output = MLRest!.QueryOrThrow(CacheClass, "GetJizdy", input);
        Jizdy.Clear();
        foreach (KeyValuePair<string, CVar> kvp in output)
        {
            if (kvp.Key == "ERR") continue;
            CList r = kvp.Value;
            Jizdy.Add(new DispecinkRow(
                r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4),
                r.GetString(5), r.GetString(6), r.GetString(7), r.GetString(8), r.GetString(9),
                r.GetString(10) == "1"));
        }
    }
}
