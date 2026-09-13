using System;
using System.Windows;
using System.Windows.Controls;
using EDControls.MiniApps;

namespace EDControls.Tests;

/// <summary>
/// Pruh miniaplikace. Regrese, které to hlídá: ztracený default „Načíst" nechá tlačítko bez
/// popisku, prosakující prázdný status ukáže osiřelý popisek a rozbitá ContentProperty
/// zahodí filtrační kontrolky psané jako děti v XAML.
/// </summary>
public class EDMiniAppBarTests
{
    private const string Comp = "pack://application:,,,/EDControls;component/";

    [Fact]
    public void ActionText_DefaultsToNacist()
    {
        var metadata = EDMiniAppBar.ActionTextProperty.GetMetadata(typeof(EDMiniAppBar));
        Assert.Equal("Načíst", metadata.DefaultValue);
    }

    [Theory]
    [InlineData(null, Visibility.Collapsed)]
    [InlineData("", Visibility.Collapsed)]
    [InlineData("Aktualizováno: 13.09.2026", Visibility.Visible)]
    public void Status_VisibleOnlyWithText(string? status, Visibility expected)
    {
        RunSta(() =>
        {
            var bar = Arrange(new EDMiniAppBar { StatusText = status });
            Assert.Equal(expected, Status(bar).Visibility);
        });
    }

    /// <summary>Po dobu načítání status hlásí průběh, i když zatím žádný nemá.</summary>
    [Fact]
    public void Status_ShowsProgressWhileBusy()
    {
        RunSta(() =>
        {
            var bar = Arrange(new EDMiniAppBar { StatusText = null, IsBusy = true });
            var status = Status(bar);
            Assert.Equal(Visibility.Visible, status.Visibility);
            Assert.Equal("Načítám…", status.Text);
        });
    }

    [Fact]
    public void Filters_AreHostedAsChildren()
    {
        RunSta(() =>
        {
            var bar = new EDMiniAppBar();
            var combo = new ComboBox();
            bar.Filters.Add(combo);
            Arrange(bar);

            Assert.Contains(Descendants(bar), d => ReferenceEquals(d, combo));
        });
    }

    [Fact]
    public void ActionButton_HiddenWhenShowActionFalse()
    {
        RunSta(() =>
        {
            var bar = Arrange(new EDMiniAppBar { ShowAction = false });
            Assert.Equal(Visibility.Collapsed, Button(bar).Visibility);
        });
    }

    /// <summary>Styl a triggery se vyhodnotí až v resource stromu s hotovým layoutem.</summary>
    private static EDMiniAppBar Arrange(EDMiniAppBar bar)
    {
        var root = new Grid();
        root.Children.Add(bar);
        root.Measure(new Size(800, 100));
        root.Arrange(new Rect(0, 0, 800, 100));
        root.UpdateLayout();
        return bar;
    }

    private static TextBlock Status(EDMiniAppBar bar) =>
        Descendants(bar).OfType<TextBlock>().First();

    private static Button Button(EDMiniAppBar bar) =>
        Descendants(bar).OfType<Button>().First();

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var d in Descendants(child)) yield return d;
        }
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
        EDTesting.Sta.RunOnAppThread(() => { EnsureApplication(); action(); });
}
