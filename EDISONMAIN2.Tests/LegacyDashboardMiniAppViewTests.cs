using System;
using System.Windows;
using System.Windows.Controls;
using EDISONMAIN2.LegacyMiniApps;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Drift guard nad view legacy dashboardu. Barva řádku ze serveru musí jít přes
/// <c>CellStyle</c> s <c>BasedOn</c> - přes <c>RowStyle</c> ji implicitní styl buňky přebije
/// setterem a barvy se tiše přestanou zobrazovat (viz EDControls/CLAUDE.md).
/// </summary>
public class LegacyDashboardMiniAppViewTests
{
    private const string Comp = "pack://application:,,,/EDControls;component/";

    [Fact]
    public void CellStyle_TargetsCellAndKeepsTheming()
    {
        RunSta(() =>
        {
            var style = Assert.IsType<Style>(new LegacyDashboardMiniAppView().Resources["DashboardCell"]);

            Assert.Equal(typeof(DataGridCell), style.TargetType);
            Assert.NotNull(style.BasedOn);
        });
    }

    [Fact]
    public void TableTemplate_ColorsCellsNotRows()
    {
        RunSta(() =>
        {
            var grid = TableGrid();

            Assert.Null(grid.RowStyle);
            Assert.NotNull(grid.CellStyle);
        });
    }

    /// <summary>Rám kreslí karta okolo; grid s vlastním rámem by udělal dvojitou linku.</summary>
    [Fact]
    public void TableTemplate_LeavesBorderToCard()
    {
        RunSta(() =>
        {
            Assert.Equal(new Thickness(0), TableGrid().BorderThickness);
            Assert.Equal(2, TableGrid().AlternationCount);
        });
    }

    /// <summary>Chybějící StaticResource (EDIcons.*) shodí view až při načtení, ne při kliku.</summary>
    [Fact]
    public void View_LoadsWithSharedResources()
    {
        RunSta(() => Assert.NotNull(new LegacyDashboardMiniAppView().Content));
    }

    private static DataGrid TableGrid()
    {
        var view = new LegacyDashboardMiniAppView();
        var template = (DataTemplate)view.Resources[new DataTemplateKey(typeof(DashboardTable))];
        var card = Assert.IsType<Border>(template.LoadContent());
        return Assert.IsType<DataGrid>(card.Child);
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
