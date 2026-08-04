using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace EDControls.Tests;

/// <summary>
/// Focus ring: themovaný indikátor klávesového focusu (override
/// <see cref="SystemParameters.FocusVisualStyleKey"/> v EDControlStyles.xaml). Regrese, kterou to
/// hlídá: ring nakreslený přes StaticResource by zamrzl v light barvě a v dark modu by byl
/// stejně neviditelný jako systémový tečkovaný rámeček, který nahrazuje.
/// </summary>
public class EDThemeFocusVisualTests
{
    // Testovací host nevytváří WPF Application → pack:// loading vyžaduje ručně triggernout
    // Application type initializer.
    static EDThemeFocusVisualTests() => _ = Application.ResourceAssembly;

    private const string LightUri  = "pack://application:,,,/EDControls;component/EDTheme/EDTheme.xaml";
    private const string DarkUri   = "pack://application:,,,/EDControls;component/EDTheme/EDThemeDark.xaml";
    private const string StylesUri = "pack://application:,,,/EDControls;component/EDControlStyles/EDControlStyles.xaml";

    // ---------- Tokeny ----------

    [Theory]
    [InlineData("EDTheme.Focus.Ring")]
    [InlineData("EDTheme.Focus.Ring.Color")]
    [InlineData("EDTheme.Focus.RingContrast")]
    [InlineData("EDTheme.Focus.RingContrast.Color")]
    public void FocusTokens_ExistInBothThemes(string key)
    {
        RunSta(() =>
        {
            Assert.True(Load(LightUri).Contains(key), $"{key} chybí v EDTheme.xaml");
            Assert.True(Load(DarkUri).Contains(key), $"{key} chybí v EDThemeDark.xaml");
        });
    }

    [Fact]
    public void EveryDarkKey_HasLightCounterpart()
    {
        // Jednosměrně: opačný směr by selhal na záměrných light-only tokenech (typografie,
        // spacing, Accent.Header, OnAccent.*).
        RunSta(() =>
        {
            var light = Load(LightUri);
            var missing = Load(DarkUri).Keys.Cast<object>()
                                            .Where(k => !light.Contains(k))
                                            .Select(k => k.ToString())
                                            .ToList();

            Assert.Empty(missing);
        });
    }

    // ---------- Ring ----------

    [Fact]
    public void SystemFocusVisualStyle_IsOverriddenByEDControlStyles()
    {
        RunSta(() => Assert.NotNull(ResolveStyle(SystemParameters.FocusVisualStyleKey, dark: false)));
    }

    [Theory]
    [InlineData("EDTheme.FocusVisual")]
    [InlineData("EDTheme.FocusVisual.Inset")]
    public void RingStroke_FollowsDarkMode(string styleKey)
    {
        // Jádro testu: ring musí být čtený přes DynamicResource, jinak zůstane v light barvě
        // i po přidání EDThemeDark.xaml na konec MergedDictionaries (což dělá ThemeService).
        RunSta(() =>
        {
            var lightStrokes = RingStrokes(styleKey, dark: false);
            var darkStrokes  = RingStrokes(styleKey, dark: true);

            Assert.Equal(2, lightStrokes.Count);
            Assert.Equal(lightStrokes.Count, darkStrokes.Count);
            Assert.All(lightStrokes.Zip(darkStrokes), pair => Assert.NotEqual(pair.First, pair.Second));
        });
    }

    [Theory]
    [InlineData(typeof(Button))]
    [InlineData(typeof(CheckBox))]
    [InlineData(typeof(TextBox))]
    [InlineData(typeof(ComboBox))]
    [InlineData(typeof(PasswordBox))]
    [InlineData(typeof(DatePicker))]
    [InlineData(typeof(ToggleButton))]
    [InlineData(typeof(RadioButton))]
    [InlineData(typeof(TabItem))]
    [InlineData(typeof(ListBoxItem))]
    [InlineData(typeof(TreeViewItem))]
    [InlineData(typeof(DataGridCell))]
    [InlineData(typeof(DataGridColumnHeader))]
    public void ImplicitStyle_SetsFocusVisualExplicitly(Type controlType)
    {
        // Past, kterou to hlídá: samotný override SystemParameters.FocusVisualStyleKey nestačí,
        // protože theme style Aera nastavuje FocusVisualStyle vlastní hodnotou s vyšší prioritou
        // (u TextBoxu null). Styl, který setter vynechá, tiše spadne na tečkovaný rámeček
        // v barvě SystemColors.ControlText — tedy zpátky na neviditelný focus v dark modu.
        RunSta(() =>
        {
            var style = Load(StylesUri)[controlType] as Style;
            Assert.NotNull(style);

            var setter = FocusVisualSetters(style!).SingleOrDefault();
            Assert.NotNull(setter);
            Assert.IsType<Style>(setter!.Value);
        });
    }

    [Fact]
    public void StylesReferencingFocusVisual_ResolveToRealStyle()
    {
        // Překlep v klíči {StaticResource EDTheme.FocusVisual.Inset} by spadl až za běhu při
        // prvním fokusnutí prvku.
        RunSta(() =>
        {
            var withSetter = Load(StylesUri).Values.OfType<Style>()
                                            .SelectMany(FocusVisualSetters)
                                            .ToList();

            Assert.NotEmpty(withSetter);
            Assert.All(withSetter, s => Assert.IsType<Style>(s.Value));
        });
    }

    // ---------- Helpers ----------

    private static IEnumerable<Setter> FocusVisualSetters(Style style) =>
        style.Setters.OfType<Setter>()
             .Where(s => s.Property == FrameworkElement.FocusVisualStyleProperty);

    private static ResourceDictionary Load(string uri) => new() { Source = new Uri(uri) };

    private static Style? ResolveStyle(object key, bool dark)
    {
        var root = new Grid();
        root.Resources.MergedDictionaries.Add(Load(LightUri));
        root.Resources.MergedDictionaries.Add(Load(StylesUri));
        if (dark) root.Resources.MergedDictionaries.Add(Load(DarkUri));

        return root.TryFindResource(key) as Style;
    }

    /// <summary>
    /// Vykreslené barvy obou linek ringu (halo + accent) po aplikaci templatu v resource
    /// stromu, tedy až po vyhodnocení DynamicResource lookupů.
    /// </summary>
    private static List<Color> RingStrokes(string styleKey, bool dark)
    {
        var root = new Grid();
        root.Resources.MergedDictionaries.Add(Load(LightUri));
        root.Resources.MergedDictionaries.Add(Load(StylesUri));
        if (dark) root.Resources.MergedDictionaries.Add(Load(DarkUri));

        var style = (Style)root.FindResource(styleKey);
        var template = (ControlTemplate)style.Setters.OfType<Setter>()
                                             .Single(s => s.Property == Control.TemplateProperty).Value;

        var host = new ContentControl { Template = template };
        root.Children.Add(host);
        root.Measure(new Size(100, 100));
        root.Arrange(new Rect(0, 0, 100, 100));
        host.ApplyTemplate();

        var ringHost = (Grid)VisualTreeHelper.GetChild(host, 0);
        return ringHost.Children.OfType<Rectangle>()
                       .Select(r => ((SolidColorBrush)r.Stroke).Color)
                       .ToList();
    }

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
