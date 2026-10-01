using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Toolbar = EDControls.EDComponentToolbar.EDComponentToolbar;

namespace EDControls.Tests;

/// <summary>
/// Sdílený toolbar komponentových hlaviček. Regrese, které to hlídá: rozejití brandového
/// pruhu (EDList i EDInputForm stojí na témže podkladu) a akumulace Ctrl+F bindingů.
/// </summary>
public class EDComponentToolbarTests
{
    private const string Comp = "pack://application:,,,/EDControls;component/";

    [Fact]
    public void BarBackground_IsBrandHeader()
    {
        RunSta(() =>
        {
            var toolbar = new Toolbar();
            var bar = Assert.IsType<Border>(toolbar.Content);

            // DynamicResource se vyhodnotí až v resource stromu.
            var root = new Grid();
            root.Children.Add(toolbar);
            root.Measure(new Size(800, 100));
            root.Arrange(new Rect(0, 0, 800, 100));
            root.UpdateLayout();

            var expected = (Brush)Application.Current!.Resources["EDTheme.Accent.Header"];
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

    [Fact]
    public void CtrlFBinding_DoesNotAccumulateAcrossReloads()
    {
        // Dokumentovaná past (EDControls/CLAUDE.md): window-level Ctrl+F binding z OnLoaded
        // se musí v OnUnloaded odregistrovat, jinak se při re-loadu akumulují kopie.
        RunSta(() =>
        {
            var toolbar = new Toolbar();
            var window = new Window { Content = toolbar };
            try
            {
                int CtrlFCount() => window.InputBindings.OfType<KeyBinding>()
                    .Count(b => b.Key == Key.F && b.Modifiers == ModifierKeys.Control);

                RaiseOn(toolbar, FrameworkElement.LoadedEvent);
                RaiseOn(toolbar, FrameworkElement.LoadedEvent);   // re-load bez Unloaded
                Assert.Equal(1, CtrlFCount());

                RaiseOn(toolbar, FrameworkElement.UnloadedEvent);
                Assert.Equal(0, CtrlFCount());

                RaiseOn(toolbar, FrameworkElement.LoadedEvent);   // návrat do stromu
                Assert.Equal(1, CtrlFCount());
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>Nápověda „(Ctrl+F)" v placeholderu jen u toolbaru, který zkratku v okně obsluhuje -
    /// v shellu jich jsou desítky a ostatním by lhala.</summary>
    [Fact]
    public void Placeholder_CtrlFHintOnlyOnShortcutOwner()
    {
        RunSta(() =>
        {
            var prvni = new Toolbar();
            var druhy = new Toolbar();
            var window = new Window { Content = new StackPanel { Children = { prvni, druhy } } };
            try
            {
                Assert.Equal("Hledat…", prvni.EffectiveSearchPlaceholder);

                RaiseOn(prvni, FrameworkElement.LoadedEvent);
                RaiseOn(druhy, FrameworkElement.LoadedEvent);
                Assert.Equal("Hledat… (Ctrl+F)", prvni.EffectiveSearchPlaceholder);
                Assert.Equal("Hledat…", druhy.EffectiveSearchPlaceholder);

                prvni.SearchPlaceholder = "Hledat pole…";
                Assert.Equal("Hledat pole… (Ctrl+F)", prvni.EffectiveSearchPlaceholder);

                RaiseOn(prvni, FrameworkElement.UnloadedEvent);
                Assert.Equal("Hledat pole…", prvni.EffectiveSearchPlaceholder);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void RaiseOn(Toolbar toolbar, RoutedEvent routedEvent) =>
        toolbar.RaiseEvent(new RoutedEventArgs(routedEvent, toolbar));

    // Toolbar načítá pack:// resource a StaticResource EDIcons.* - potřebuje tedy Application
    // se stejnými slovníky, jaké merguje reálná appka (viz root CLAUDE.md).
    private static void EnsureApplication()
    {
        if (Application.Current is not null) return;

        // OnExplicitShutdown - jiné testy (EDFooter) vytvářejí okna; default OnLastWindowClose
        // by sdílenou Application shodil a druhá se v AppDomain vytvořit nedá.
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        foreach (var dict in new[] { "EDTheme/EDTheme.xaml", "EDControlStyles/EDControlStyles.xaml",
                                     "EDIcons/EDIcons.xaml", "EDStrings/EDStrings.xaml" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(Comp + dict) });
    }

    // Trvalé app vlákno: Application vytvořená na jednorázovém STA vlákně občas zanikne
    // se svým dispatcherem a druhá se v AppDomain vytvořit nedá.
    private static void RunSta(Action action) =>
        EDTesting.Sta.RunOnAppThread(() => { EnsureApplication(); action(); });
}
