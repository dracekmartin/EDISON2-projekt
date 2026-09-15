using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using AvalonDock;
using AvalonDock.Layout;
using AvalonDock.Layout.Serialization;

namespace EDISONMAIN2.MiniApps;

/// <summary>
/// Reálná implementace <see cref="IMiniAppHost"/> nad AvalonDock <see cref="DockingManager"/>.
/// Mini-app = <see cref="LayoutDocument"/> v centrálním document pane → vyplní celou plochu docku;
/// další otevřené jdou jako taby a lze je přetažením roztáhnout vedle sebe.
/// </summary>
public sealed class AvalonDockHost : IMiniAppHost
{
    private readonly DockingManager _dock;
    private bool _stampScheduled;

    public AvalonDockHost(DockingManager dock)
    {
        _dock = dock;
        // Deserialize při restore vymění celý LayoutRoot → hook na Updated by se ztratil.
        HookLayout();
        dock.LayoutChanged += (_, _) => HookLayout();
    }

    private void HookLayout()
    {
        _dock.Layout.Updated -= OnLayoutUpdated;
        _dock.Layout.Updated += OnLayoutUpdated;
    }

    public IMiniAppPane AddPane(string contentId, string title, ImageSource? icon, object content, Action onClosed,
                               MiniAppPlacement? placement = null, bool canClose = true)
    {
        var document = new LayoutDocument
        {
            ContentId = contentId,
            Title = title,
            IconSource = icon,
            Content = content,
        };
        ApplyClosability(document, canClose);
        document.Closed += (_, _) => onClosed();
        switch (placement?.Kind)
        {
            case MiniAppPlacementKind.IntoTarget when LivePaneOf(placement) is { } target:
                target.Children.Add(document);
                break;
            case MiniAppPlacementKind.NewRow:
                // Řada přes celou šířku pod sloupci (1/3 výšky proti sloupcům 2/3).
                _dock.Layout.RootPanel.Children.Add(new LayoutDocumentPane(document) { DockHeight = new GridLength(1, GridUnitType.Star) });
                break;
            case MiniAppPlacementKind.NewColumn:
                ColumnsPanel.Children.Add(new LayoutDocumentPane(document));
                break;
            default:
                DocumentPane.Children.Add(document);
                break;
        }
        document.IsActive = true;
        return new Pane(document);
    }

    // CanClose=false skryje křížek a blokuje uživatelské zavření; programové document.Close()
    // (přepnutí session, CloseAll) AvalonDock pouští dál. CanFloat/CanMove navíc drží panel
    // v hlavním okně - odtržená Plocha by byla totéž co zavřená.
    private static void ApplyClosability(LayoutDocument document, bool canClose)
    {
        document.CanClose = canClose;
        if (canClose) return;
        document.CanFloat = false;
        document.CanMove = false;
    }

    public IReadOnlyList<MiniAppTarget> GetTargets() =>
    [
        .. DocumentPanes(_dock.Layout).Select(pane => new MiniAppTarget(
            pane,
            [.. pane.Children.OfType<LayoutContent>().Select(c => c.Title ?? "")],
            ReferenceEquals(pane, _dock.Layout.ActiveContent?.Parent))),
    ];

    // Ukotvená pod-okna v pořadí zleva/shora. Descendents() prochází i plovoucí okna, ta mezi
    // cíle nepatří - uživatel je zavírá jako okno, takže by cíl mizel pod rukama.
    internal static IEnumerable<LayoutDocumentPane> DocumentPanes(LayoutRoot layout) =>
        layout.Descendents().OfType<LayoutDocumentPane>().Where(pane => !pane.IsHostedInFloatingWindow);

    // Cíl je snímek z doby otevření pickeru; než uživatel potvrdil, mohl zaniknout (restore
    // vyměňuje celý LayoutRoot, zavření posledního tabu pane odpojí). Odpojený má Root == null.
    private LayoutDocumentPane? LivePaneOf(MiniAppPlacement placement) =>
        placement.Target?.Token is LayoutDocumentPane pane && ReferenceEquals(pane.Root, _dock.Layout) ? pane : null;

    private void OnLayoutUpdated(object? sender, EventArgs e) => ScheduleMinSizeStamp();

    private void ScheduleMinSizeStamp()
    {
        if (_stampScheduled) return;
        _stampScheduled = true;
        // Odloženě - Updated letí uprostřed layout operace, přímá mutace stromu by cyklila.
        _dock.Dispatcher.BeginInvoke(new Action(() =>
        {
            _stampScheduled = false;
            StampMinSizes(_dock.Layout);
        }), DispatcherPriority.Background);
    }

    // Pane musí zůstat aspoň tak velký, aby byla vidět hlavička s tabem a pruh obsahu - bez
    // razítka ho AvalonDock pustí na svých 25 px a miniaplikace fakticky zmizí (uživatel ji
    // pak nenajde ani myší).
    private const double PaneMinWidth = 200;
    private const double PaneMinHeight = 120;

    // Razítko po každé změně stromu - drag i restore vyrábějí nové pane a ty by šly zúžit na nic.
    internal static void StampMinSizes(LayoutRoot layout)
    {
        foreach (var pane in layout.Descendents().OfType<LayoutDocumentPane>().ToList())
        {
            pane.DockMinWidth = PaneMinWidth;
            pane.DockMinHeight = PaneMinHeight;
        }
    }

    public void ResetToDefaultSkeleton()
    {
        // Plně kvalifikované - WPF markup temp projekt (_wpftmp) importuje i System.Windows.Forms,
        // takže holé `Orientation` je tam dvojznačné (CS0104), i když hlavní compile projde.
        var columns = new LayoutPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, DockHeight = new GridLength(2, GridUnitType.Star) };
        columns.Children.Add(new LayoutDocumentPane());
        var root = new LayoutPanel { Orientation = System.Windows.Controls.Orientation.Vertical };
        root.Children.Add(columns);
        _dock.Layout.RootPanel = root;
    }

    // Vodorovný pás sloupců (výchozí skeleton). Fallback na root, kdyby skeleton nebyl (po restore).
    private LayoutPanel ColumnsPanel =>
        _dock.Layout.RootPanel.Children.OfType<LayoutPanel>().FirstOrDefault() ?? _dock.Layout.RootPanel;

    // První (levý) document pane - cíl pro taby a první sloupec. Když žádný není (všechny dokumenty
    // odtržené do plovoucích oken), vyrobit ho - otevření další mini-app nesmí spadnout.
    private LayoutDocumentPane DocumentPane
    {
        get
        {
            if (_dock.Layout.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault() is { } pane)
                return pane;
            var created = new LayoutDocumentPane();
            ColumnsPanel.Children.Add(created);
            return created;
        }
    }

    public string SaveLayout()
    {
        var sb = new StringBuilder();
        using (var writer = new StringWriter(sb))
            new XmlLayoutSerializer(_dock).Serialize(writer);
        return sb.ToString();
    }

    public void RestoreLayout(string layoutXml, Func<string, IMiniAppPane, RestoredContent?> resolve)
    {
        var serializer = new XmlLayoutSerializer(_dock);
        serializer.LayoutSerializationCallback += (_, e) =>
        {
            // Hostujeme jen document mini-apps; cokoli jiného (kdyby v XML bylo) zahodit.
            if (e.Model is not LayoutDocument document ||
                resolve(e.Model.ContentId, new Pane(document)) is not { } resolved)
            {
                e.Cancel = true;
                return;
            }
            ApplyClosability(document, resolved.CanClose);
            e.Content = resolved.Content;
            document.Closed += (_, _) => resolved.OnClosed();
        };
        using var reader = new StringReader(layoutXml);
        serializer.Deserialize(reader);
    }

    private sealed class Pane(LayoutDocument document) : IMiniAppPane
    {
        public void Close() => document.Close();
    }
}
