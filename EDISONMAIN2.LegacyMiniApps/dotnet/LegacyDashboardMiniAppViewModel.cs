using System.Collections.ObjectModel;
using System.Windows.Input;
using ALWA.SYS;
using EDControls.EDViewModel;
using EDControls.MiniApps;

namespace EDISONMAIN2.LegacyMiniApps;

/// <summary>
/// Generická legacy dashboardová miniaplikace (labely + tabulky). Parametrizovaná <c>appType</c> —
/// všech 15 legacy miniaplikací sdílí tuto třídu (titulek + appType dodá descriptor). Data čte přes
/// legacy <c>ALVA.EDISON.MiniAplikaceAPI.LoadData</c>, které vrací delimited <c>object[]</c>
/// (object[] = dokumentovaně povolené pro integraci s legacy delimited metodou; parsing v
/// <see cref="DashboardData.Parse"/>).
/// </summary>
public sealed class LegacyDashboardMiniAppViewModel : EDMiniAppViewModel, IRefreshableMiniApp
{
    private const string CacheClass = "ALVA.EDISON.MiniAplikaceAPI";

    private readonly string _appType;
    private readonly string _title;

    public LegacyDashboardMiniAppViewModel(string appType, string title)
    {
        _appType = appType;
        _title = title;
        RefreshCommand = Command(Load);
    }

    public LegacyDashboardMiniAppViewModel(EDServices services, string appType, string title) : base(services)
    {
        _appType = appType;
        _title = title;
        RefreshCommand = Command(Load);
    }

    public override string Title => _title;

    public ObservableCollection<DashboardSection> Sections { get; } = [];

    private string? _lastUpdated;
    public string? LastUpdated { get => _lastUpdated; private set => Set(ref _lastUpdated, value); }

    /// <summary>Pro prázdný stav ve View (firma danou agendu nemá / žádná data).</summary>
    public bool HasNoData => Sections.Count == 0;

    public ICommand RefreshCommand { get; }

    public override void OnOpened() => Load();

    /// <summary>Hromadný refresh ze shellu (<see cref="IRefreshableMiniApp"/>) — stejná cesta jako „Načíst".</summary>
    public void Refresh() => Load();

    private void Load()
    {
        // Legacy delimited API: object[] vstup (refresh + appType) i výstup. refresh=1 přepočítá data
        // server-side (PrepareData). Oddělovač polí = MLRest.Delimiter; parsing viz DashboardData.
        char d = MLRest!.Delimiter;
        object[] output = MLRest.QueryOrThrow(CacheClass, "LoadData",
            [$"refresh{d}1", $"appType{d}{_appType}"]);

        var data = DashboardData.Parse(output, d, _appType);
        Sections.Clear();
        foreach (var section in data.Sections) Sections.Add(section);
        LastUpdated = data.LastUpdated;
        OnPropertyChanged(nameof(HasNoData));
    }
}
