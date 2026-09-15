using System.Windows;
using System.Windows.Controls;
using EDControls.EDList;
using EDTesting;
using List = EDControls.EDList.EDList;

namespace EDControls.Tests;

/// <summary>
/// Drift guard nad brandovým pruhem EDListu. Pruh je bezpodmínečný, stejně jako u EDInputFormu -
/// dokud byl opt-in přes Title, hostitelé bez titulku (legacy miniaplikace) ho tiše neměli.
/// </summary>
public class EDListHeaderTests
{
    private const string Comp = "pack://application:,,,/EDControls;component/";

    [Fact]
    public void Header_IsVisibleWithoutTitle()
    {
        RunSta(() =>
        {
            var list = new List();

            Assert.Equal(Visibility.Visible, list.HeaderToolbar.Visibility);
            Assert.True(list.HeaderToolbar.ShowSearch);
        });
    }

    /// <summary>EDList nemá per-komponentu help ani nastavení - ikony musí zůstat vypnuté.</summary>
    [Fact]
    public void Header_HidesHelpAndSettings()
    {
        RunSta(() =>
        {
            var list = new List();

            Assert.False(list.HeaderToolbar.ShowHelp);
            Assert.False(list.HeaderToolbar.ShowSettings);
        });
    }

    [Fact]
    public void Title_ForwardsToToolbar()
    {
        RunSta(() =>
        {
            var list = Layout(new List());

            list.Title = "Motohodiny vozidel";

            Assert.Equal("Motohodiny vozidel", list.HeaderToolbar.Title);
        });
    }

    // Binding toolbaru na Title hostitele dosedne až po layoutu v resource stromě.
    private static T Layout<T>(T control) where T : FrameworkElement
    {
        var root = new Grid();
        root.Children.Add(control);
        root.Measure(new Size(800, 600));
        root.Arrange(new Rect(0, 0, 800, 600));
        root.UpdateLayout();
        return control;
    }

    private static void EnsureApplication()
    {
        if (Application.Current is not null) return;

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        foreach (var dict in new[] { "EDTheme/EDTheme.xaml", "EDControlStyles/EDControlStyles.xaml",
                                     "EDIcons/EDIcons.xaml", "EDStrings/EDStrings.xaml" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(Comp + dict) });
    }

    private static void RunSta(Action action) =>
        Sta.RunOnAppThread(() => { EnsureApplication(); action(); });
}
