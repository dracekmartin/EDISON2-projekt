using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace EDControls.EDViewModel;

/// <summary>
/// Napojení <see cref="EDUsageTelemetry"/> na okna: class handler na <c>Window.Loaded</c>
/// registrovaný z <c>EDAppearance.Initialize</c>, stejný vzor jako panel Podpora. Na
/// <see cref="EDViewModel.AttachToWindow"/> viset nemůže - TemplateDesigner ho volá jen pro
/// hlavní okno (dialogy otevírá přímo) a telemetrie by z nich nesbírala nic.
/// </summary>
internal static class EDUsageTelemetryHook
{
    private static bool _initialized;

    /// <summary>Okno už hooknuté - <c>Loaded</c> může fírovat opakovaně a handlery by se hromadily.</summary>
    private static readonly DependencyProperty IsHookedProperty =
        DependencyProperty.RegisterAttached("IsHooked", typeof(bool), typeof(Window),
            new PropertyMetadata(false));

    internal static void AttachToAllWindows()
    {
        if (_initialized) return;
        _initialized = true;

        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnWindowLoaded));
    }

    private static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Window window || !ShouldTrack(window)) return;
        Hook(window, ((EDViewModel)window.DataContext).UsageFormId!);
    }

    /// <summary>
    /// Trackujeme jen okna s <see cref="EDViewModel"/> a nenulovým form ID (opt-out přes
    /// <c>PersistedFormId = null</c>). Tím vypadne i šum, který by class handler jinak chytil:
    /// vlastní overlay okna EDControls, popupy a chrome AvalonDocku ViewModel nemají.
    /// (Rozhodnutí odděleno od eventu, aby šlo testovat.)
    /// </summary>
    internal static bool ShouldTrack(Window window) =>
        window.DataContext is EDViewModel { UsageFormId: not null }
        && !(bool)window.GetValue(IsHookedProperty);

    /// <summary>
    /// OPEN + kliky na <b>pojmenované</b> controly (obdoba WinForms <c>Name</c> v legacy EDForm;
    /// nepojmenované se přeskočí), flush dávky při zavření okna. ContextMenu/Popup stromy nemusí
    /// routovat až na okno - telemetrie je best-effort, přesnost tam neřešíme.
    /// </summary>
    internal static void Hook(Window window, string formId)
    {
        window.SetValue(IsHookedProperty, true);
        EDUsageTelemetry.Track(formId, "OPEN");

        RoutedEventHandler onClick = (_, e) =>
        {
            var name = ((e.OriginalSource ?? e.Source) as FrameworkElement)?.Name;
            if (!string.IsNullOrEmpty(name)) EDUsageTelemetry.Track(formId, name);
        };
        window.AddHandler(ButtonBase.ClickEvent, onClick, handledEventsToo: true);
        window.AddHandler(MenuItem.ClickEvent, onClick, handledEventsToo: true);

        // CurrentOrNull: infrastruktura na každé okno nesmí spadnout kvůli pořadí startupu.
        window.Closed += (_, _) => EDUsageTelemetry.Flush(EDServices.CurrentOrNull?.MLRest);
    }
}
