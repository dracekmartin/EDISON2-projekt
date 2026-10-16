using System.Windows;
using EDISONMAIN2.SupportStrip;
using EDTesting;
using static EDISONMAIN2.Tests.EdisonMainViewModelHarness;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Záložky nad stromem modulů zůstanou v jedné řadě i v nejužším levém panelu, ať je aktivní
/// kterákoli. Aktivní záložka nese název, takže potřebná šířka závisí na nejdelším z nich.
/// </summary>
[Collection(SharedStaticStateCollection.Name)]
public class EdisonMainWindowTreeTabsTests
{
    [WindowFact]
    public void NarrowestTreePanel_KeepsTabsOnOneRow_ForEveryActiveTab()
    {
        var screenshotMode = Environment.GetEnvironmentVariable(SupportWidgetHost.ScreenshotModeVariable);
        // Pruh podpory pak nestaví WebView2, které by v testu sahalo na síť.
        Environment.SetEnvironmentVariable(SupportWidgetHost.ScreenshotModeVariable, "1");
        try
        {
            Sta.RunOnAppThread(() =>
            {
                EdisonMainWindowTreeSearchTests.EnsureApplication();
                var vm = CreateVM();
                var window = new EdisonMainWindow
                {
                    DataContext = vm, WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -3000, Top = 0, Width = 1280, Height = 800,
                    WindowStyle = WindowStyle.None, ShowInTaskbar = false,
                };
                window.Show();
                try
                {
                    // Uložená šířka pod minimem - layout ji musí vykreslit na MinWidth sloupce.
                    window.TreeColumn.Width = new GridLength(100);
                    foreach (var tab in vm.TreeTabs)
                    {
                        vm.SelectedTab = tab;
                        window.UpdateLayout();

                        var tops = vm.TreeTabs
                            .Select(t => (FrameworkElement)window.TreeTabControl.ItemContainerGenerator.ContainerFromItem(t))
                            .Select(item => item.TranslatePoint(new Point(0, 0), window.TreeTabControl).Y)
                            .ToList();
                        Assert.True(tops.All(y => Math.Abs(y - tops[0]) < 0.5),
                            $"Aktivní {tab.Key}, sloupec {window.TreeColumn.ActualWidth:F1}: záložky se zalomily, Y = {string.Join(", ", tops.Select(y => y.ToString("F1")))}.");
                    }
                }
                finally
                {
                    vm.SuppressCloseConfirm();
                    window.Close();
                }
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable(SupportWidgetHost.ScreenshotModeVariable, screenshotMode);
        }
    }
}
