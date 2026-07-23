using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using EDControls.EDViewModel;
using EDControls.MiniApps;

namespace EDISONMAIN2.MiniApps;

/// <summary>Jedna stránka karuselu — titulek + továrna na obsah (invokuje se lazy, viz
/// <see cref="SupportCarouselMiniAppViewModel.VisiblePages"/>).</summary>
public sealed record CarouselPage(string Title, Func<FrameworkElement> ContentFactory);

/// <summary>Viditelná stránka pro view (sloupec: popisek + živý obsah).</summary>
public sealed record CarouselPageView(string Title, FrameworkElement Content);

/// <summary>
/// Pinned karusel „Podpora M-line" — garantovaný komunikační kanál IT → uživatelé. Responzivní:
/// podle šířky zobrazí 1–N stránek (Novinky/Wiki/Školení) vedle sebe; vejdou-li se všechny,
/// cyklení se vypne (mirror starých 3 sloupců). Jinak šipky pro ruční posun a lišta, která se
/// za <see cref="PageDuration"/> naplní a přeskočí na další stránku. Obsah stránky se vytváří
/// až při prvním zobrazení a pak zůstává živý (WebView se při cyklení nerefreshuje).
/// </summary>
public sealed class SupportCarouselMiniAppViewModel : EDMiniAppViewModel
{
    internal static readonly TimeSpan PageDuration = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);
    /// <summary>Minimální šířka jednoho sloupce — pod ní se stránky stackují do cyklení.</summary>
    internal const double MinPageWidth = 420;

    private readonly IReadOnlyList<CarouselPage> _pages;
    private readonly FrameworkElement?[] _contents;
    private readonly DispatcherTimer _timer = new() { Interval = TickInterval };

    public SupportCarouselMiniAppViewModel(IReadOnlyList<CarouselPage> pages)
        : this(EDServices.Current, pages) { }

    /// <summary>DI/test overload — předá vlastní <c>EDServices</c> místo <c>EDServices.Current</c>.</summary>
    public SupportCarouselMiniAppViewModel(EDServices services, IReadOnlyList<CarouselPage> pages)
        : base(services)
    {
        if (pages.Count == 0)
            throw new ArgumentException("Karusel potřebuje aspoň jednu stránku.", nameof(pages));
        _pages = pages;
        _contents = new FrameworkElement?[pages.Count];
        _timer.Tick += (_, _) => Tick();
        NextCommand = Command(() => MoveBy(1));
        PrevCommand = Command(() => MoveBy(-1));
    }

    public override string Title => "Podpora M-line";

    public ICommand NextCommand { get; }
    public ICommand PrevCommand { get; }

    private int _currentIndex;
    public int CurrentIndex
    {
        get => _currentIndex;
        private set
        {
            if (!Set(ref _currentIndex, value)) return;
            OnPropertyChanged(nameof(PageIndicator));
            OnPropertyChanged(nameof(VisiblePages));
        }
    }

    private int _visibleCount = 1;
    /// <summary>Kolik stránek se vedle sebe vejde (1–počet stránek); plní view ze SizeChanged.</summary>
    public int VisibleCount => _visibleCount;

    /// <summary>Cyklení (šipky, lišta, indikátor) má smysl, jen když nejsou vidět všechny stránky.</summary>
    public bool ShowCycleControls => _visibleCount < _pages.Count;

    /// <summary>Pozice pro hlavičku, např. „2/3".</summary>
    public string PageIndicator => $"{_currentIndex + 1}/{_pages.Count}";

    /// <summary>Okno viditelných stránek od <see cref="CurrentIndex"/> (s wrapem). Počítá se
    /// v getteru — obsah se materializuje až přístupem (binding), ctor továrny nevolá.</summary>
    public IReadOnlyList<CarouselPageView> VisiblePages =>
        [.. Enumerable.Range(0, _visibleCount).Select(offset =>
        {
            int i = (_currentIndex + offset) % _pages.Count;
            return new CarouselPageView(_pages[i].Title, _contents[i] ??= _pages[i].ContentFactory());
        })];

    private double _progress;
    /// <summary>Naplnění lišty auto-posunu, 0–1.</summary>
    public double Progress { get => _progress; private set => Set(ref _progress, value); }

    /// <summary>Přepočet počtu sloupců podle aktuální šířky panelu (volá view ze SizeChanged).</summary>
    public void UpdateViewportWidth(double width)
    {
        int count = Math.Clamp((int)(width / MinPageWidth), 1, _pages.Count);
        if (count == _visibleCount) return;
        _visibleCount = count;
        // Při plném zobrazení srovnat na přirozené pořadí stránek a zastavit lištu.
        if (!ShowCycleControls)
        {
            CurrentIndex = 0;
            Progress = 0;
        }
        OnPropertyChanged(nameof(VisibleCount));
        OnPropertyChanged(nameof(ShowCycleControls));
        OnPropertyChanged(nameof(VisiblePages));
    }

    public override void OnOpened() => _timer.Start();

    public override void OnClosed()
    {
        _timer.Stop();
        // Vytvořený obsah může držet browser proces (WebView2) — uklidit při zavření panelu.
        foreach (var content in _contents)
            (content as IDisposable)?.Dispose();
    }

    // internal kvůli unit testům (DispatcherTimer v testech netiká).
    internal void Tick()
    {
        if (!ShowCycleControls) return;
        double next = Progress + TickInterval / PageDuration;
        if (next >= 1) MoveBy(1);
        else Progress = next;
    }

    // Posun (ruční i automatický) resetuje lištu.
    private void MoveBy(int delta)
    {
        CurrentIndex = (CurrentIndex + delta + _pages.Count) % _pages.Count;
        Progress = 0;
    }
}
