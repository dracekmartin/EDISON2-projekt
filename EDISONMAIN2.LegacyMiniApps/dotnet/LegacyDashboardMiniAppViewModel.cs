using System.Collections.ObjectModel;
using System.Windows.Input;
using EDControls.EDViewModel;
using EDControls.MiniApps;

namespace EDISONMAIN2.LegacyMiniApps;

/// <summary>
/// Generická legacy dashboardová miniaplikace (labely + tabulky). Parametrizovaná <c>appType</c> -
/// všech 15 legacy miniaplikací sdílí tuto třídu (titulek + appType dodá descriptor). Data čte přes
/// <see cref="LegacyDashboardLoader"/>, který požadavky panelů spojuje do jednoho requestu.
/// </summary>
public sealed class LegacyDashboardMiniAppViewModel : EDMiniAppViewModel, IRefreshableMiniApp
{
    private readonly LegacyDashboardLoader _loader;
    private readonly string _appType;
    private readonly string _title;
    // Otevření panelu čte předpočítaná data (refresh=0), „Načíst" si vynutí přepočet - dvě cesty,
    // dvě instance AsyncRelayCommandu, proti souběhu je guard IsLoading.
    private readonly ICommand _openCommand;

    public LegacyDashboardMiniAppViewModel(LegacyDashboardLoader loader, string appType, string title)
        : this(EDServices.Current, loader, appType, title) { }

    /// <summary>DI/test overload - předá vlastní <c>EDServices</c> místo <c>EDServices.Current</c>.</summary>
    public LegacyDashboardMiniAppViewModel(EDServices services, LegacyDashboardLoader loader, string appType, string title)
        : base(services)
    {
        _loader = loader;
        _appType = appType;
        _title = title;
        RefreshCommand = CommandAsync(() => LoadAsync(refresh: true), () => !IsLoading);
        _openCommand = CommandAsync(() => LoadAsync(refresh: false));
    }

    public override string Title => _title;

    public ObservableCollection<DashboardSection> Sections { get; } = [];

    private string? _lastUpdated;
    public string? LastUpdated { get => _lastUpdated; private set => Set(ref _lastUpdated, value); }

    private bool _isLoading;
    public bool IsLoading { get => _isLoading; private set { if (Set(ref _isLoading, value)) OnPropertyChanged(nameof(HasNoData)); } }

    /// <summary>Chyba načtení se ukazuje v panelu, ne popupem - dávka o N panelech by jich zvedla N.</summary>
    private string? _errorText;
    public string? ErrorText { get => _errorText; private set { if (Set(ref _errorText, value)) OnPropertyChanged(nameof(HasNoData)); } }

    /// <summary>Pro prázdný stav ve View (firma danou agendu nemá / žádná data).</summary>
    public bool HasNoData => !IsLoading && ErrorText is null && Sections.Count == 0;

    public ICommand RefreshCommand { get; }

    public override void OnOpened() => _openCommand.Execute(null);

    /// <summary>Hromadný refresh ze shellu (<see cref="IRefreshableMiniApp"/>) - stejná cesta jako „Načíst".</summary>
    public void Refresh() => RefreshCommand.Execute(null);

    private async Task LoadAsync(bool refresh)
    {
        if (IsLoading) return;
        IsLoading = true;
        ErrorText = null;
        try
        {
            var data = await _loader.LoadAsync(_appType, refresh);
            Sections.Clear();
            foreach (var section in data.Sections) Sections.Add(section);
            LastUpdated = data.LastUpdated;
        }
        catch (Exception ex)
        {
            Sections.Clear();
            LastUpdated = null;
            ErrorText = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
