using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using EDControls.EDViewModel;
using EDControls.MiniApps;

namespace EDISONMAIN2.MiniApps;

/// <summary>Volba intervalu auto-refreshe (popisek + doba).</summary>
public sealed record RefreshInterval(string Label, TimeSpan Duration);

/// <summary>
/// Řídicí mini-app: podle nastaveného intervalu periodicky obnovuje ostatní otevřené mini-apps
/// (ty, které implementují <see cref="IRefreshableMiniApp"/>). Obdoba hromadného refreshe ve starém
/// shellu. Hromadný refresh deleguje na <c>RefreshAll</c>, který jí <c>MiniAppManager</c> předá při
/// mountu (<see cref="IMiniAppRefreshController"/>) a který vrací názvy obnovených mini-apps.
/// </summary>
public sealed class PeriodicRefreshMiniAppViewModel : EDMiniAppViewModel, IMiniAppRefreshController
{
    private readonly DispatcherTimer _timer = new();
    private Func<IReadOnlyList<string>>? _refreshAll;

    public PeriodicRefreshMiniAppViewModel() : this(EDServices.Current) { }

    public PeriodicRefreshMiniAppViewModel(EDServices services) : base(services)
    {
        _selectedInterval = Intervals.First(i => i.Duration == TimeSpan.FromMinutes(5));
        RefreshNowCommand = Command(RefreshNow);
        _timer.Tick += (_, _) => RefreshNowCommand.Execute(null);
    }

    public void AttachRefresher(Func<IReadOnlyList<string>> refreshAll) => _refreshAll = refreshAll;

    public override string Title => "Automatická obnova";

    public IReadOnlyList<RefreshInterval> Intervals { get; } =
    [
        new("10 s", TimeSpan.FromSeconds(10)),
        new("30 s", TimeSpan.FromSeconds(30)),
        new("1 min", TimeSpan.FromMinutes(1)),
        new("2 min", TimeSpan.FromMinutes(2)),
        new("5 min", TimeSpan.FromMinutes(5)),
        new("10 min", TimeSpan.FromMinutes(10)),
        new("15 min", TimeSpan.FromMinutes(15)),
        new("30 min", TimeSpan.FromMinutes(30)),
        new("60 min", TimeSpan.FromMinutes(60)),
    ];

    private bool _isEnabled = true;
    public bool IsEnabled
    {
        get => _isEnabled;
        set { if (Set(ref _isEnabled, value)) Reschedule(); }
    }

    private RefreshInterval _selectedInterval;
    public RefreshInterval SelectedInterval
    {
        get => _selectedInterval;
        set { if (Set(ref _selectedInterval, value)) Reschedule(); }
    }

    private string _lastRefresh = "zatím neproběhla";
    public string LastRefresh { get => _lastRefresh; private set => Set(ref _lastRefresh, value); }

    public ICommand RefreshNowCommand { get; }

    /// <summary>internal kvůli testům - DispatcherTimer v testech netiká, stav ale vypovídá.</summary>
    internal bool TimerRunning => _timer.IsEnabled;
    internal TimeSpan TimerInterval => _timer.Interval;

    public override void OnOpened() => Reschedule();

    public override void OnClosed() => _timer.Stop();

    private void Reschedule()
    {
        _timer.Stop();
        if (!_isEnabled) return;
        _timer.Interval = _selectedInterval.Duration;
        _timer.Start();
    }

    private void RefreshNow()
    {
        var refreshed = _refreshAll?.Invoke() ?? [];
        LastRefresh = refreshed.Count == 0
            ? $"{DateTime.Now:HH:mm:ss} - nic k obnovení"
            : $"{DateTime.Now:HH:mm:ss} - {string.Join(", ", refreshed)}";
    }
}
