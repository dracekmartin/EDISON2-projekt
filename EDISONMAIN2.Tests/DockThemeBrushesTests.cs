using System.Windows;
using System.Windows.Controls;
using AvalonDock.Themes;
using EDISONMAIN2.MiniApps;
using Brush = System.Windows.Media.Brush;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Drift testy přebarvení tab stripu docku. Obě strany mapy selhávají tiše: neznámý token
/// EDTheme se nenajde a barva zůstane VS modrá, klíč chybějící v paletě VS2013 override
/// jen zahodí.
/// </summary>
public class DockThemeBrushesTests
{
    // Testovací host nevytváří WPF Application → pack:// loading vyžaduje ručně triggernout
    // Application type initializer.
    static DockThemeBrushesTests() => _ = Application.ResourceAssembly;

    private const string LightUri = "pack://application:,,,/EDControls;component/EDTheme/EDTheme.xaml";
    private const string DarkUri  = "pack://application:,,,/EDControls;component/EDTheme/EDThemeDark.xaml";

    [Fact]
    public void EveryThemeKey_ExistsInBothPalettes()
    {
        EDTesting.Sta.Run(() =>
        {
            var light = Load(LightUri);
            var dark = Load(DarkUri);

            foreach (var (_, themeKey) in DockThemeBrushes.Map)
            {
                Assert.True(light.Contains(themeKey), $"{themeKey} chybí v EDTheme.xaml");
                Assert.True(dark.Contains(themeKey), $"{themeKey} chybí v EDThemeDark.xaml");
            }
        });
    }

    [Fact]
    public void EveryDockKey_ExistsInVs2013Palette()
    {
        // Upgrade balíčku může klíč přejmenovat - override by pak tiše mířil do prázdna.
        EDTesting.Sta.Run(() =>
        {
            var theme = new ResourceDictionary { Source = new Vs2013LightTheme().GetResourceUri() };

            foreach (var (dockKey, _) in DockThemeBrushes.Map)
                Assert.True(ContainsDeep(theme, dockKey), $"{dockKey} chybí v paletě VS2013");
        });
    }

    [Fact]
    public void Apply_FillsEveryDockKeyWithBrush()
    {
        EDTesting.Sta.Run(() =>
        {
            var host = new Grid();
            host.Resources.MergedDictionaries.Add(Load(LightUri));

            DockThemeBrushes.Apply(host);

            foreach (var (dockKey, themeKey) in DockThemeBrushes.Map)
                Assert.True(host.Resources[dockKey] is Brush, $"{dockKey} nedostal brush z {themeKey}");
        });
    }

    /// <summary>Paleta VS2013 drží klíče ve vnořených merged dictionaries, kam Contains nevidí.</summary>
    private static bool ContainsDeep(ResourceDictionary dict, object key) =>
        dict.Contains(key) || dict.MergedDictionaries.Any(d => ContainsDeep(d, key));

    private static ResourceDictionary Load(string uri) => new() { Source = new Uri(uri) };
}
