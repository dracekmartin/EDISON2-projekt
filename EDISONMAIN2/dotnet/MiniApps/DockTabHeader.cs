using System.Windows;
using System.Windows.Controls;
using AvalonDock.Controls;

namespace EDISONMAIN2.MiniApps;

/// <summary>
/// Záložka dokumentu má v tématu VS2013 pevnou výšku 19 px a větší písmo uživatele se do ní
/// nevejde. Výška se uvolní na Auto, hodnota ze šablony zůstane jako minimum.
/// </summary>
internal static class DockTabHeader
{
    private const string HeaderPartName = "Header";

    private static bool _registered;

    // Class handler na Border, ne na záložku: výměna tématu i plovoucí okno staví šablonu znovu,
    // záložka zůstává. SizeChanged, ne Loaded: Loaded WPF nepošle do větve bez instančního handleru.
    public static void Register()
    {
        if (_registered) return;
        _registered = true;

        EventManager.RegisterClassHandler(typeof(Border), FrameworkElement.SizeChangedEvent,
            new SizeChangedEventHandler(OnBorderSizeChanged));
    }

    private static void OnBorderSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is Border { TemplatedParent: LayoutDocumentTabItem, Name: HeaderPartName } header)
            Fit(header);
    }

    internal static void Fit(Border header)
    {
        if (double.IsNaN(header.Height)) return;
        header.MinHeight = header.Height;
        header.Height = double.NaN;
    }
}
