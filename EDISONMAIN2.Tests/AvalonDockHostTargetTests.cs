using AvalonDock.Layout;
using EDISONMAIN2.MiniApps;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Výčet pod-oken nabízených v pickeru jako cíl umístění. Stejně jako razítkování minim
/// je to čistá funkce nad layout modelem - jde ověřit bez živého DockingManageru a STA vlákna.
/// </summary>
public class AvalonDockHostTargetTests
{
    private static LayoutDocumentPane Pane(params string[] titles)
    {
        var pane = new LayoutDocumentPane();
        foreach (var title in titles)
            pane.Children.Add(new LayoutDocument { ContentId = title, Title = title });
        return pane;
    }

    private static LayoutRoot RootWith(params LayoutDocumentPane[] panes)
    {
        var root = new LayoutRoot();
        var panel = new LayoutPanel();
        foreach (var pane in panes) panel.Children.Add(pane);
        root.RootPanel = panel;
        return root;
    }

    [Fact]
    public void Panes_AreListed_InLayoutOrder()
    {
        var layout = RootWith(Pane("Plocha"), Pane("Dispečink", "Vozidla"));

        var titles = AvalonDockHost.DocumentPanes(layout)
            .Select(p => string.Join(",", p.Children.OfType<LayoutContent>().Select(c => c.Title)));

        Assert.Equal(["Plocha", "Dispečink,Vozidla"], titles);
    }

    [Fact]
    public void FloatingPanes_AreNotTargets()
    {
        // Odtržené okno uživatel zavírá jako okno - cíl by mu mizel pod rukama.
        var layout = RootWith(Pane("Plocha"));
        var floating = new LayoutDocumentFloatingWindow { RootPanel = new LayoutDocumentPaneGroup(Pane("Odtržená")) };
        layout.FloatingWindows.Add(floating);

        var titles = AvalonDockHost.DocumentPanes(layout)
            .SelectMany(p => p.Children.OfType<LayoutContent>().Select(c => c.Title));

        Assert.Equal(["Plocha"], titles);
    }

    [Fact]
    public void LastPane_SurvivesClosingItsOnlyDocument_AsEmptyTarget()
    {
        // AvalonDock poslední document pane neodstraní - po zavření všeho zbude prázdný
        // přes celou plochu. Picker ho pozná podle prázdného seznamu titulků.
        var pane = Pane("Plocha");
        var layout = RootWith(pane);

        ((LayoutDocument)pane.Children[0]).Close();

        var only = Assert.Single(AvalonDockHost.DocumentPanes(layout));
        Assert.Empty(only.Children);
    }
}
