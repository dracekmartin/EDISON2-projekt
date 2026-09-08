using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Toolbar = EDControls.EDComponentToolbar.EDComponentToolbar;

namespace EDControls.Tests;

/// <summary>
/// Flat režim toolbaru (vsazení do cizí hlavičky, dnes EDInputForm). Regrese, které to hlídá:
/// přepnutý default by ztmavil pruh v EDListu i v shellu; ztracený DataTrigger by nechal
/// brandové pozadí uprostřed světlé hlavičky.
/// </summary>
public class EDComponentToolbarFlatTests
{
    private const string Comp = "pack://application:,,,/EDControls;component/";

    [Fact]
    public void Flat_DefaultsToFalse()
    {
        var metadata = Toolbar.FlatProperty.GetMetadata(typeof(Toolbar));
        Assert.Equal(false, metadata.DefaultValue);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BarBackground_FollowsFlat(bool flat)
    {
        RunSta(() =>
        {
            var toolbar = new Toolbar { Flat = flat };
            var bar = Assert.IsType<Border>(toolbar.Content);

            // Styl s triggerem se vyhodnotí až v resource stromu (DynamicResource lookup).
            var root = new Grid();
            root.Children.Add(toolbar);
            root.Measure(new Size(800, 100));
            root.Arrange(new Rect(0, 0, 800, 100));
            root.UpdateLayout();

            var expected = flat
                ? Brushes.Transparent
                : (Brush)Application.Current!.Resources["EDTheme.Accent.Header"];
            Assert.Equal(expected.ToString(), bar.Background.ToString());
        });
    }

    /// <summary>Ikona nápovědy je v XAML `StaticResource` - chybějící klíč shodí toolbar
    /// hned při načtení, ne až při kliku.</summary>
    [Fact]
    public void HelpIcon_ResourceExists()
    {
        RunSta(() => Assert.True(Application.Current!.Resources.Contains("EDIcons.Help")));
    }

    // Toolbar načítá pack:// resource a StaticResource EDIcons.* - potřebuje tedy Application
    // se stejnými slovníky, jaké merguje reálná appka (viz root CLAUDE.md).
    private static void EnsureApplication()
    {
        if (Application.Current is not null) return;

        var app = new Application();
        foreach (var dict in new[] { "EDTheme/EDTheme.xaml", "EDControlStyles/EDControlStyles.xaml",
                                     "EDIcons/EDIcons.xaml", "EDStrings/EDStrings.xaml" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(Comp + dict) });
    }

    private static void RunSta(Action action) =>
        EDTesting.Sta.Run(() => { EnsureApplication(); action(); });
}
