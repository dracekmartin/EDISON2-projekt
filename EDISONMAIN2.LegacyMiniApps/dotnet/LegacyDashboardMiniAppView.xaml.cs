using System.Windows;
using System.Windows.Controls;
using EDControls.EDTheme;

namespace EDISONMAIN2.LegacyMiniApps;

public partial class LegacyDashboardMiniAppView : UserControl
{
    public LegacyDashboardMiniAppView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // LegacyForeColorConverter čte motiv v okamžiku konverze, takže přepnutí dark za běhu
    // by binding samo nepřehodnotilo. Refresh sekcí postaví kontejnery znovu. Odhlásit se
    // musí v Unloaded - panel se při dock/float re-parentuje a handlery by se kumulovaly.
    private bool _hooked;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_hooked) return;
        _hooked = true;
        ThemeService.Applied += RefreshSections;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_hooked) return;
        _hooked = false;
        ThemeService.Applied -= RefreshSections;
    }

    private void RefreshSections() => SectionsHost.Items.Refresh();
}
