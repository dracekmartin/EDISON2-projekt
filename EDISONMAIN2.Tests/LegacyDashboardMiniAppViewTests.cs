using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
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

    /// <summary>Barva se čte ze skrytého sloupce EDListu - přejmenovaný klíč by ji tiše vypnul.</summary>
    [Fact]
    public void CellStyle_BindsForegroundToHiddenColorColumn()
    {
        RunSta(() =>
        {
            var style = Assert.IsType<Style>(new LegacyDashboardMiniAppView().Resources["DashboardCell"]);

            var setter = Assert.Single(style.Setters.OfType<Setter>(),
                s => s.Property == DataGridCell.ForegroundProperty);
            var binding = Assert.IsType<Binding>(setter.Value);
            Assert.Equal($"[{DashboardListMapper.ForeColorKey}]", binding.Path.Path);
        });
    }

    /// <summary>Rám i pozadí kreslí DataGrid uvnitř EDListu; karta okolo by udělala dvojitou linku.</summary>
    [Fact]
    public void TableTemplate_IsBareEDListWithCellColors()
    {
        RunSta(() => Assert.NotNull(TableList().CellStyle));
    }

    /// <summary>Tabulka se do EDListu dostane attached property, ne bindingem - data jdou metodou SetData.</summary>
    [Fact]
    public void TableTemplate_BindsTableIntoList()
    {
        RunSta(() =>
        {
            var table = new DashboardTable(
                [new DashboardColumn("Rz", 'T', ""), new DashboardColumn("MTH", 'N', "###0.00")],
                [new DashboardRow(["1A2 3456", "  123.45"], "#FF0000", 0)],
                "EDISONMAIN2.LegacyMiniApp.52015.0");

            var list = TableList();
            list.DataContext = table;
            // Binding mimo vizuální strom dosedne až na DataBind prioritě dispatcheru.
            list.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);

            Assert.Equal("EDISONMAIN2.LegacyMiniApp.52015.0", list.HostId);
            var row = Assert.Single(list.Rows);
            Assert.Equal(123.45.ToString("###0.00", CultureInfo.CurrentCulture), row["c1"]);
            Assert.Equal("#FF0000", row[DashboardListMapper.ForeColorKey]);
        });
    }

    /// <summary>Chybějící StaticResource (EDIcons.*) shodí view až při načtení, ne při kliku.</summary>
    [Fact]
    public void View_LoadsWithSharedResources()
    {
        RunSta(() => Assert.NotNull(new LegacyDashboardMiniAppView().Content));
    }

    private static EDControls.EDList.EDList TableList()
    {
        var view = new LegacyDashboardMiniAppView();
        var template = (DataTemplate)view.Resources[new DataTemplateKey(typeof(DashboardTable))];
        return Assert.IsType<EDControls.EDList.EDList>(template.LoadContent());
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
