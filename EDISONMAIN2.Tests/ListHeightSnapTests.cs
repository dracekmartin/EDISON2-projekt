using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using EDISONMAIN2.Windows.MiniAppPickerWindow;
using EDTesting;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Srážení výšky seznamu na celé položky (picker miniaplikací). Bez něj seznam končí uprostřed
/// položky a z posledního řádku zbyde pár pixelů, které vypadají jako prázdná neklikatelná položka.
/// Potřebuje živý layout, takže STA vlákno + Measure/Arrange.
/// </summary>
public class ListHeightSnapTests
{
    private const double ItemHeight = 20;

    [Fact]
    public void Apply_TrimsViewport_ToWholeItems()
    {
        Sta.Run(() =>
        {
            var host = Measured(itemCount: 20, height: 105);
            var list = (ListBox)host.Child;

            Assert.True(ListHeightSnap.Apply(list, double.PositiveInfinity));
            Relayout(host);

            double viewport = Viewport(list);
            Assert.Equal(0, viewport % ItemHeight, precision: 3);
            Assert.True(viewport > 0, "Seznam se nesmí srazit na nulu.");
        });
    }

    [Fact]
    public void Apply_ViewportAlreadyWhole_ChangesNothing()
    {
        Sta.Run(() =>
        {
            // Výška hostu musí počítat i s chrome seznamu (rámeček/padding šablony), jinak
            // viewport na celé položky nevyjde a "přesný fit" by přesný nebyl.
            var probe = Measured(itemCount: 20, height: 105);
            double chrome = ((ListBox)probe.Child).ActualHeight - Viewport((ListBox)probe.Child);
            var host = Measured(itemCount: 20, height: 5 * ItemHeight + chrome);
            var list = (ListBox)host.Child;

            Assert.False(ListHeightSnap.Apply(list, double.PositiveInfinity));
            Assert.Equal(double.PositiveInfinity, list.MaxHeight);
        });
    }

    [Fact]
    public void Apply_Twice_IsStable()
    {
        // Kdyby druhé kolo sráželo dál, seznam by mezi oběma výškami kmital.
        Sta.Run(() =>
        {
            var host = Measured(itemCount: 20, height: 105);
            var list = (ListBox)host.Child;
            ListHeightSnap.Apply(list, double.PositiveInfinity);
            Relayout(host);
            double snapped = list.MaxHeight;

            Assert.False(ListHeightSnap.Apply(list, double.PositiveInfinity));
            Assert.Equal(snapped, list.MaxHeight, precision: 3);
        });
    }

    [Fact]
    public void Attach_DoesNotLoop_WhenSnapResizesTheList()
    {
        // Regrese: srážení napojené na SizeChanged samotného seznamu se zacyklilo (snap zmenší
        // seznam → další SizeChanged → reset na strop → …) a seznam viditelně kmital.
        Sta.Run(() =>
        {
            var host = Measured(itemCount: 20, height: 105);
            var list = (ListBox)host.Child;
            int resizes = 0;
            list.SizeChanged += (_, _) => resizes++;

            ListHeightSnap.Attach(host, list, double.PositiveInfinity);
            host.Height = 140;          // změna dostupného prostoru → jedno kolo srážení
            Relayout(host);
            PumpLayoutQueue();
            Relayout(host);
            int afterFirstRound = resizes;
            PumpLayoutQueue();
            Relayout(host);

            Assert.Equal(afterFirstRound, resizes);
        });
    }

    [Fact]
    public void Apply_NeverExceedsCap()
    {
        Sta.Run(() =>
        {
            var list = (ListBox)Measured(itemCount: 20, height: 105).Child;

            ListHeightSnap.Apply(list, cap: 60);

            Assert.True(list.MaxHeight <= 60);
        });
    }

    // ListBox bez rámečku a paddingu, položky pevné výšky - výsledek pak nezávisí na themingu.
    private static Border Measured(int itemCount, double height)
    {
        var list = new ListBox
        {
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            ItemsSource = Enumerable.Range(1, itemCount).Select(i => $"Položka {i}").ToList(),
            ItemContainerStyle = new Style(typeof(ListBoxItem))
            {
                Setters = { new Setter(FrameworkElement.HeightProperty, ItemHeight) },
            },
        };
        var host = new Border { Child = list, Width = 200, Height = height };
        Relayout(host);
        return host;
    }

    private static void Relayout(Border host)
    {
        host.Measure(new Size(host.Width, host.Height));
        host.Arrange(new Rect(0, 0, host.Width, host.Height));
        host.UpdateLayout();
    }

    // Vyprázdní dispatcher frontu až po prioritu Loaded - tam snap odkládá svůj přepočet.
    private static void PumpLayoutQueue() =>
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Loaded);

    // Výška plochy, na které se kreslí položky (= ListBox bez chrome šablony).
    private static double Viewport(DependencyObject list) => FindPresenter(list)?.ActualHeight ?? 0;

    private static FrameworkElement? FindPresenter(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ItemsPresenter presenter) return presenter;
            if (FindPresenter(child) is { } nested) return nested;
        }
        return null;
    }
}
