using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using AvalonDock;
using AvalonDock.Controls;
using AvalonDock.Layout;
using AvalonDock.Themes;
using EDISONMAIN2.MiniApps;
using EDTesting;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Záložka dokumentu v tématu VS2013 má pevnou výšku, kterou <see cref="DockTabHeader"/> uvolňuje.
/// Oprava míří na jméno části šablony, takže upgrade balíčku ji může tiše minout.
/// </summary>
public class DockTabHeaderTests
{
    // Testovací host nevytváří WPF Application → pack:// loading vyžaduje ručně triggernout
    // Application type initializer.
    static DockTabHeaderTests() => _ = Application.ResourceAssembly;

    // 18 pt, největší nabízená velikost písma.
    private const double LargeFontPx = 24;

    // Application z jiného testu dává TextBlocku implicitní styl s FontSize z tokenu (jako shell),
    // bez ní TextBlock dědí FontSize. Test proto zvětšuje obojí.
    private const string BodyFontToken = "EDTheme.FontSize.Body";

    [Fact]
    public void Vs2013Theme_TabHeaderHasFixedHeight()
    {
        Sta.Run(() =>
        {
            var header = HeaderOf(CreateTab());

            Assert.False(double.IsNaN(header.Height), "Header už nemá pevnou výšku - DockTabHeader smazat");
        });
    }

    [Fact]
    public void Fit_TabGrowsWithLargeFont()
    {
        Sta.Run(() =>
        {
            // Dvě záložky: druhé Measure se stejným omezením by vrátilo výsledek z cache,
            // zneplatnění od potomka zpracuje až layout manager živého stromu.
            var plain = CreateTab();
            var fitted = CreateTab();
            DockTabHeader.Fit(HeaderOf(fitted));

            double before = MeasureHeight(plain);
            double after = MeasureHeight(fitted);
            Assert.True(after > before + 4, $"záložka před {before} px, po {after} px");
        });
    }

    [Fact]
    public void Fit_Twice_KeepsOriginalMinimum()
    {
        Sta.Run(() =>
        {
            var header = HeaderOf(CreateTab());
            double fixedHeight = header.Height;

            DockTabHeader.Fit(header);
            DockTabHeader.Fit(header);

            Assert.True(double.IsNaN(header.Height));
            Assert.Equal(fixedHeight, header.MinHeight);
        });
    }

    [WindowFact]
    public void LiveDock_TabGrowsWithFont_AlsoAfterThemeSwap()
    {
        // Jen živý strom ověří hook: Loaded i nové sestavení šablony po výměně tématu.
        var heights = new List<double>();
        Sta.RunOnAppThread(() =>
        {
            DockTabHeader.Register();
            var dock = new DockingManager { Theme = new Vs2013LightTheme(), FontSize = LargeFontPx };
            dock.Resources[BodyFontToken] = LargeFontPx;
            dock.Layout.RootPanel.Children.Add(new LayoutDocumentPane(new LayoutDocument { Title = "Plocha" }));
            // Mimo viditelnou plochu a bez chrome - test nesmí blikat přes obrazovku.
            var window = new Window
            {
                Content = dock, Width = 600, Height = 400,
                WindowStyle = WindowStyle.None, ShowInTaskbar = false, Left = -3000,
            };
            window.Show();
            Settle(window);
            heights.Add(LiveHeader(window).ActualHeight);

            dock.Theme = new Vs2013DarkTheme();
            Settle(window);
            heights.Add(LiveHeader(window).ActualHeight);
            window.Close();
        });

        Assert.All(heights, h => Assert.True(h > 23, $"záložka má {h} px"));
    }

    // Loaded chodí s prioritou Loaded, Invoke na vlastním vlákně pustí frontu až po ni.
    private static void Settle(Window window)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        window.UpdateLayout();
    }

    private static Border LiveHeader(DependencyObject root)
    {
        var tab = Descendants(root).OfType<LayoutDocumentTabItem>().Single();
        return HeaderOf(tab);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var nested in Descendants(child))
                yield return nested;
        }
    }

    private static LayoutDocumentTabItem CreateTab()
    {
        var theme = new ResourceDictionary { Source = new Vs2013LightTheme().GetResourceUri() };
        var tab = new LayoutDocumentTabItem { FontSize = LargeFontPx };
        tab.Resources.MergedDictionaries.Add(theme);
        tab.Resources[BodyFontToken] = LargeFontPx;
        tab.Style = (Style)tab.FindResource(typeof(LayoutDocumentTabItem));
        // Šablonu titulku dodává až DockingManager, bez něj se ukáže ToString() modelu. Na výšku textu to stačí.
        tab.Model = new LayoutDocument();
        tab.ApplyTemplate();
        return tab;
    }

    private static Border HeaderOf(LayoutDocumentTabItem tab) =>
        Assert.IsType<Border>(tab.Template.FindName("Header", tab));

    private static double MeasureHeight(FrameworkElement element)
    {
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return element.DesiredSize.Height;
    }
}
