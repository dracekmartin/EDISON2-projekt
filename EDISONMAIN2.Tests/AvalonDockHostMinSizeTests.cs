using AvalonDock.Layout;
using EDISONMAIN2.MiniApps;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Minimální rozměry pane v docku miniaplikací (HD 13132/F6). Zbytek <see cref="AvalonDockHost"/>
/// potřebuje živý DockingManager a netestuje se; razítkování minim je čistá funkce nad layout
/// modelem, takže jde ověřit bez STA vlákna.
/// </summary>
public class AvalonDockHostMinSizeTests
{
    private const double MinWidth = 200;
    private const double MinHeight = 120;

    private static LayoutRoot RootWith(params LayoutDocumentPane[] panes)
    {
        var root = new LayoutRoot();
        var panel = new LayoutPanel();
        foreach (var pane in panes) panel.Children.Add(pane);
        root.RootPanel = panel;
        return root;
    }

    private static LayoutDocumentPane Pane() => new(new LayoutDocument { ContentId = "x" });

    [Fact]
    public void Pane_GetsMinSize()
    {
        var pane = Pane();
        // Výchozí minimum AvalonDocku je pár pixelů - bez razítka miniaplikace zmizí z očí.
        Assert.True(pane.DockMinWidth < MinWidth);
        Assert.True(pane.DockMinHeight < MinHeight);

        AvalonDockHost.StampMinSizes(RootWith(pane));

        Assert.True(pane.DockMinWidth >= MinWidth);
        Assert.True(pane.DockMinHeight >= MinHeight);
    }

    [Fact]
    public void EveryPane_IsStamped_NotJustTheFirst()
    {
        var panes = new[] { Pane(), Pane(), Pane() };

        AvalonDockHost.StampMinSizes(RootWith(panes));

        Assert.All(panes, p => Assert.True(p.DockMinWidth >= MinWidth && p.DockMinHeight >= MinHeight));
    }
}
