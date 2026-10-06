using System.Windows;
using System.Windows.Controls;

namespace EDISONMAIN2.Windows.EdisonMainWindow.SupportStrip;

/// <summary>
/// Plní hostitelský <see cref="Border"/> webovým widgetem podpora.m-line.cz - jediný funnel
/// pro pruh podpory v shellu i widgety přihlašovací obrazovky. V screenshot módu
/// (<c>EDISON_SCREENSHOT_MODE=1</c>, anonymizované obrazovky) místo webu ukáže neutrální popisek:
/// WebView2 vůbec nevznikne, takže nic nesahá na síť ani na dark-mode Hidden logiku widgetu.
/// </summary>
internal static class SupportWidgetHost
{
    internal const string ScreenshotModeVariable = "EDISON_SCREENSHOT_MODE";

    // Aktivace = nastavit env před spuštěním exe (v produkci per proces konstantní);
    // čte se při přístupu, aby šel mód ověřit testem bez restartu procesu.
    internal static bool ScreenshotMode =>
        IsEnabled(Environment.GetEnvironmentVariable(ScreenshotModeVariable));

    internal static bool IsEnabled(string? value)
    {
        var v = value?.Trim();
        return v is not null
            && (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Vrací widget k Dispose při zavření okna (drží statické Font/Theme eventy);
    /// v screenshot módu null - placeholder nic nedrží.</summary>
    internal static IDisposable? Fill(Border host, string url, string placeholder)
    {
        if (ScreenshotMode)
        {
            host.Child = CreatePlaceholder(placeholder);
            return null;
        }
        var widget = new FontZoomWebView();
        host.Child = widget;
        widget.Navigate(url);
        return widget;
    }

    internal static TextBlock CreatePlaceholder(string text)
    {
        var block = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            Margin = new Thickness(16),
        };
        // Ekvivalent DynamicResource - přežije swap theme za běhu.
        block.SetResourceReference(TextBlock.ForegroundProperty, "EDTheme.Text.Secondary");
        return block;
    }
}
