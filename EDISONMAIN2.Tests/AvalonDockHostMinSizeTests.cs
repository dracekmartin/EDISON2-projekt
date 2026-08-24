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
    private static LayoutRoot RootWith(params LayoutDocumentPane[] panes)
    {
        var root = new LayoutRoot();
        var panel = new LayoutPanel();
        foreach (var pane in panes) panel.Children.Add(pane);
        root.RootPanel = panel;
        return root;
    }

    private static LayoutDocumentPane PaneWith(bool canClose) =>
        new(new LayoutDocument { ContentId = canClose ? "x" : "pinned", CanClose = canClose });

    [Fact]
    public void UnpinnedPane_GetsMinSize()
    {
        var pane = PaneWith(canClose: true);
        // Výchozí minimum AvalonDocku je pár pixelů - bez razítka miniaplikace zmizí z očí.
        Assert.True(pane.DockMinWidth < 200);
        Assert.True(pane.DockMinHeight < 120);

        AvalonDockHost.StampMinSizes(RootWith(pane));

        Assert.True(pane.DockMinWidth >= 200);
        Assert.True(pane.DockMinHeight >= 120);
    }

    [Fact]
    public void PinnedPane_KeepsLargerMinimum()
    {
        // Karusel potřebuje víc než ostatní - jednu stránku a hlavičku.
        var pinned = PaneWith(canClose: false);
        var plain = PaneWith(canClose: true);

        AvalonDockHost.StampMinSizes(RootWith(pinned, plain));

        Assert.True(pinned.DockMinWidth > plain.DockMinWidth);
        Assert.True(pinned.DockMinHeight > plain.DockMinHeight);
    }

    [Fact]
    public void EveryPane_IsStamped_NotJustTheFirst()
    {
        var panes = new[] { PaneWith(true), PaneWith(true), PaneWith(true) };

        AvalonDockHost.StampMinSizes(RootWith(panes));

        Assert.All(panes, p => Assert.True(p.DockMinWidth >= 200 && p.DockMinHeight >= 120));
    }
}
