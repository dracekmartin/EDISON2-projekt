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
    private bool _normalizeScheduled;

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
                               bool newColumn = false, bool newRow = false, bool canClose = true)
    {
        // CanClose=false skryje křížek a blokuje uživatelské zavření (i v plovoucím okně);
        // programové document.Close() (přepnutí session, CloseAll) AvalonDock pouští dál.
        // CanFloat=false navrch — odtržený pinned by šel schovat za okno / minimalizovat.
        var document = new LayoutDocument
        {
            ContentId = contentId,
            Title = title,
            IconSource = icon,
            Content = content,
            CanClose = canClose,
            CanFloat = canClose,
        };
        document.Closed += (_, _) => onClosed();
        if (newRow)
            // Řada přes celou šířku pod sloupci (1/3 výšky proti sloupcům 2/3).
            _dock.Layout.RootPanel.Children.Add(new LayoutDocumentPane(document) { DockHeight = new GridLength(1, GridUnitType.Star) });
        else if (newColumn)
            ColumnsPanel.Children.Add(new LayoutDocumentPane(document));
        else
            DocumentPane.Children.Add(document);
        document.IsActive = true;
        return new Pane(document);
    }

    // ---- pinned = solo pane ------------------------------------------------
    // Pinned (CanClose=false) dokument nesmí ve svém pane sdílet záložky — jiný aktivní tab
    // by ho překryl a komunikační kanál by zmizel z očí. Po každé změně stromu (drag & drop,
    // restore) se kolizní dokumenty odsunou do jiného/nového sloupce.

    private void OnLayoutUpdated(object? sender, EventArgs e) => ScheduleNormalize();

    private void ScheduleNormalize()
    {
        if (_normalizeScheduled) return;
        _normalizeScheduled = true;
        // Odloženě — Updated letí uprostřed layout operace, přímá mutace stromu by cyklila.
        _dock.Dispatcher.BeginInvoke(new Action(() =>
        {
            _normalizeScheduled = false;
            NormalizeSoloPanes();
        }), DispatcherPriority.Background);
    }

    // Splitter nesmí pinned zmenšit pod čitelnost (jedna stránka karuselu + hlavička).
    private const double PinnedPaneMinWidth = 340;
    private const double PinnedPaneMinHeight = 200;

    private void NormalizeSoloPanes()
    {
        foreach (var pinned in _dock.Layout.Descendents().OfType<LayoutDocument>()
                     .Where(d => !d.CanClose).ToList())
        {
            if (pinned.Parent is not LayoutDocumentPane pane) continue;
            // Razítko po každé změně stromu — drag přesune dokument do jiného (nového) pane.
            pane.DockMinWidth = PinnedPaneMinWidth;
            pane.DockMinHeight = PinnedPaneMinHeight;
            if (pane.ChildrenCount <= 1) continue;
            foreach (var intruder in pane.Children.OfType<LayoutDocument>().Where(d => d != pinned).ToList())
                MoveToOtherPane(intruder, pane);
        }
    }

    private void MoveToOtherPane(LayoutDocument document, LayoutDocumentPane from)
    {
        var target = _dock.Layout.Descendents().OfType<LayoutDocumentPane>()
            .FirstOrDefault(p => p != from && !p.Children.Any(c => c is LayoutDocument { CanClose: false }));
        if (target is null)
        {
            target = new LayoutDocumentPane();
            ColumnsPanel.Children.Add(target);
        }
        from.RemoveChild(document);
        target.Children.Add(document);
        // Odsunutý tab nechat aktivní — uživatel s ním právě pracoval (typicky ho sem dropnul).
        document.IsActive = true;
    }

    public void ResetToDefaultSkeleton()
    {
        // Plně kvalifikované — WPF markup temp projekt (_wpftmp) importuje i System.Windows.Forms,
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

    // První (levý) document pane — cíl pro taby a první sloupec. Když žádný není (všechny dokumenty
    // odtržené do plovoucích oken), vyrobit ho — otevření další mini-app nesmí spadnout.
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
            // Z deskriptoru, ne z uloženého XML — změna pinned sady se retro-aplikuje na staré layouty.
            document.CanClose = resolved.CanClose;
            document.CanFloat = resolved.CanClose;
            e.Content = resolved.Content;
            document.Closed += (_, _) => resolved.OnClosed();
        };
        using var reader = new StringReader(layoutXml);
        serializer.Deserialize(reader);
    }

    private sealed class Pane(LayoutDocument document) : IMiniAppPane
    {
        public void Close() => document.Close();

        public void Activate() => document.IsActive = true;
    }
}
