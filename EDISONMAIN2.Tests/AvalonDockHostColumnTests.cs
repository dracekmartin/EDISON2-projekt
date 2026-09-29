using System.Windows;
using AvalonDock.Layout;
using EDISONMAIN2.MiniApps;
using Orientation = System.Windows.Controls.Orientation;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Umístění „Nový sloupec vpravo" nad různými tvary layoutu. Obnovený layout nemusí mít výchozí
/// skeleton, sloupec ale musí vzniknout vedle horního pod-okna, nikdy jako řada pod ním.
/// </summary>
public class AvalonDockHostColumnTests
{
    private static LayoutDocumentPane Pane(string title) =>
        new(new LayoutDocument { ContentId = title, Title = title });

    private static LayoutRoot RootWith(Orientation orientation, params ILayoutPanelElement[] children)
    {
        var panel = new LayoutPanel { Orientation = orientation };
        foreach (var child in children) panel.Children.Add(child);
        return new LayoutRoot { RootPanel = panel };
    }

    private static string Titles(ILayoutContainer container) =>
        string.Join(",", container.Children.Select(c => c switch
        {
            LayoutDocumentPane pane => string.Join("+", pane.Children.Select(d => d.Title)),
            ILayoutContainer nested => $"[{Titles(nested)}]",
            _ => "?",
        }));

    [Fact]
    public void DefaultSkeleton_AppendsToColumnsStrip()
    {
        var strip = new LayoutPanel(Pane("Plocha")) { Orientation = Orientation.Horizontal };
        var layout = RootWith(Orientation.Vertical, strip);

        AvalonDockHost.InsertColumn(layout, Pane("Nový"));

        Assert.Equal("[Plocha,Nový]", Titles(layout.RootPanel));
    }

    [Fact]
    public void MissingStrip_WrapsTopPane_InsteadOfAddingRowBelow()
    {
        // Tvar po restore starého layoutu: prázdný pás AvalonDock smazal, Plocha sedí přímo ve svislém rootu.
        var plocha = Pane("Plocha");
        plocha.DockHeight = new GridLength(2, GridUnitType.Star);
        var layout = RootWith(Orientation.Vertical, plocha);

        AvalonDockHost.InsertColumn(layout, Pane("Nový"));

        Assert.Equal("[Plocha,Nový]", Titles(layout.RootPanel));
        var strip = Assert.IsType<LayoutPanel>(Assert.Single(layout.RootPanel.Children));
        Assert.Equal(Orientation.Horizontal, strip.Orientation);
        Assert.Equal(new GridLength(2, GridUnitType.Star), strip.DockHeight);
    }

    [Fact]
    public void MissingStrip_WithRowBelow_ColumnGoesNextToTopPane()
    {
        var layout = RootWith(Orientation.Vertical, Pane("Plocha"), Pane("Řada"));

        AvalonDockHost.InsertColumn(layout, Pane("Nový"));

        Assert.Equal("[Plocha,Nový],Řada", Titles(layout.RootPanel));
    }

    [Fact]
    public void HorizontalPaneGroupOnTop_IsTheStrip()
    {
        // Drag & drop AvalonDocku staví LayoutDocumentPaneGroup, ne LayoutPanel.
        var group = new LayoutDocumentPaneGroup(Pane("Plocha")) { Orientation = Orientation.Horizontal };
        var layout = RootWith(Orientation.Vertical, group);

        AvalonDockHost.InsertColumn(layout, Pane("Nový"));

        Assert.Equal("[Plocha,Nový]", Titles(layout.RootPanel));
        Assert.Same(group, Assert.Single(layout.RootPanel.Children));
    }

    [Fact]
    public void VerticalPaneGroupOnTop_IsWrapped()
    {
        var group = new LayoutDocumentPaneGroup(Pane("Plocha")) { Orientation = Orientation.Vertical };
        group.Children.Add(Pane("Pod Plochou"));
        var layout = RootWith(Orientation.Vertical, group);

        AvalonDockHost.InsertColumn(layout, Pane("Nový"));

        Assert.Equal("[[Plocha,Pod Plochou],Nový]", Titles(layout.RootPanel));
    }

    [Fact]
    public void HorizontalRoot_AppendsToRoot()
    {
        var layout = RootWith(Orientation.Horizontal, Pane("Plocha"));

        AvalonDockHost.InsertColumn(layout, Pane("Nový"));

        Assert.Equal("Plocha,Nový", Titles(layout.RootPanel));
    }
}
