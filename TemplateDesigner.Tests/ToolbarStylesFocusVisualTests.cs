using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace TemplateDesigner.Tests;

/// <summary>
/// ToolbarStyles.xaml sahá přes {StaticResource} na EDTheme.FocusVisual z EDControlStyles.xaml, což
/// drží jen pořadí merge v App.xaml (EDControlStyles před ToolbarStyles). Rozbité pořadí by build
/// prošel a spadlo by až při startu aplikace na XamlParseException.
/// </summary>
public class ToolbarStylesFocusVisualTests
{
    // Testovací host nevytváří WPF Application → pack:// loading vyžaduje ručně triggernout
    // Application type initializer.
    static ToolbarStylesFocusVisualTests() => _ = Application.ResourceAssembly;

    [Theory]
    [InlineData("ToolbarButton", false)]
    [InlineData("ToolbarToggle", true)]
    [InlineData("PaletteButton", false)]
    public void ToolbarStyle_ResolvesFocusVisualInAppMergeOrder(string styleKey, bool isToggle)
    {
        RunSta(() =>
        {
            var app = new ResourceDictionary();
            // Stejné pořadí jako App.xaml.
            app.MergedDictionaries.Add(Load("pack://application:,,,/EDControls;component/EDTheme/EDTheme.xaml"));
            app.MergedDictionaries.Add(Load("pack://application:,,,/EDControls;component/EDControlStyles/EDControlStyles.xaml"));
            app.MergedDictionaries.Add(Load("pack://application:,,,/TemplateDesigner;component/Styles/ToolbarStyles.xaml"));

            // Setter.Value drží u cross-dictionary StaticResource odloženou referenci, ne Style -
            // ověřuje se proto až aplikovaná hodnota na prvku v resource stromu.
            var host = new Grid { Resources = app };
            Control button = isToggle ? new ToggleButton() : new Button();
            button.Style = (Style)app[styleKey];
            host.Children.Add(button);
            host.Measure(new Size(100, 100));

            var focusVisual = button.FocusVisualStyle as Style;
            Assert.NotNull(focusVisual);
            Assert.NotEmpty(focusVisual!.Setters);
        });
    }

    private static ResourceDictionary Load(string uri) => new() { Source = new Uri(uri) };

    // Spustí akci na dedikovaném STA vlákně (WPF resource loading to vyžaduje) a propaguje
    // výjimku zpět.
    private static void RunSta(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { captured = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (captured != null) throw captured;
    }
}
