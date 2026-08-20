using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EDControls.Tests;

/// <summary>
/// ProgressBar nemá v Aeru themovatelný default - stock šablona kreslí přes indikátor pevně
/// danou světlou "glass" vrstvu, takže v dark modu zůstane světlý flek. Regrese, kterou to
/// hlídá: vypadlý implicitní styl (nebo jeho barvy čtené StaticResource) vrátí stock vzhled.
/// </summary>
public class EDThemeProgressBarTests
{
    static EDThemeProgressBarTests() => _ = Application.ResourceAssembly;

    private const string LightUri  = "pack://application:,,,/EDControls;component/EDTheme/EDTheme.xaml";
    private const string DarkUri   = "pack://application:,,,/EDControls;component/EDTheme/EDThemeDark.xaml";
    private const string StylesUri = "pack://application:,,,/EDControls;component/EDControlStyles/EDControlStyles.xaml";

    [Fact]
    public void ImplicitProgressBarStyle_Exists()
    {
        RunSta(() => Assert.NotNull(Load(StylesUri)[typeof(ProgressBar)] as Style));
    }

    [Theory]
    [InlineData("Background")]
    [InlineData("Foreground")]
    [InlineData("BorderBrush")]
    public void BrushSetters_UseDynamicResource(string propertyName)
    {
        // StaticResource by se vyhodnotil při načtení slovníku a zamrzl v light barvě -
        // EDThemeDark.xaml se přidává až za běhu na konec MergedDictionaries.
        RunSta(() =>
        {
            var style = (Style)Load(StylesUri)[typeof(ProgressBar)]!;
            var setter = style.Setters.OfType<Setter>()
                              .Single(s => s.Property.Name == propertyName);

            Assert.IsType<DynamicResourceExtension>(setter.Value);
        });
    }

    [Fact]
    public void TrackAndIndicator_ChangeWithDarkMode()
    {
        // Jádro testu: track i indikátor musí po přidání dark slovníku vykreslit jinou barvu.
        RunSta(() =>
        {
            var light = RenderedBrushes(dark: false);
            var dark  = RenderedBrushes(dark: true);

            Assert.NotEqual(light.Track, dark.Track);
            Assert.NotEqual(light.Indicator, dark.Indicator);
        });
    }

    [Fact]
    public void Template_KeepsRequiredPartNames()
    {
        // PART_Track/PART_Indicator si ProgressBar hledá jménem; bez nich nespočítá šířku
        // indikátoru a lišta zůstane prázdná (statická kontrola XAML to nepozná).
        RunSta(() =>
        {
            var (_, indicator) = Realize(dark: false);
            Assert.NotNull(indicator);
        });
    }

    private static ResourceDictionary Load(string uri) => new() { Source = new Uri(uri) };

    private static (Color Track, Color Indicator) RenderedBrushes(bool dark)
    {
        var (bar, indicator) = Realize(dark);
        return (((SolidColorBrush)bar.Background).Color,
                ((SolidColorBrush)indicator!.Background).Color);
    }

    private static (ProgressBar Bar, Border? Indicator) Realize(bool dark)
    {
        var root = new Grid();
        root.Resources.MergedDictionaries.Add(Load(LightUri));
        root.Resources.MergedDictionaries.Add(Load(StylesUri));
        if (dark) root.Resources.MergedDictionaries.Add(Load(DarkUri));

        var bar = new ProgressBar { Minimum = 0, Maximum = 1, Value = 0.5, Width = 120 };
        root.Children.Add(bar);
        root.Measure(new Size(200, 200));
        root.Arrange(new Rect(0, 0, 200, 200));
        bar.ApplyTemplate();

        return (bar, bar.Template.FindName("PART_Indicator", bar) as Border);
    }

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
