using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AvalonDock;
using AvalonDock.Layout;
using AvalonDock.Layout.Serialization;

namespace EDISONMAIN2.MiniApps;

/// <summary>
/// Reálná implementace <see cref="IMiniAppHost"/> nad AvalonDock <see cref="DockingManager"/>.
/// Mini-app = <see cref="LayoutDocument"/> v centrálním document pane → vyplní celou plochu docku;
/// další otevřené jdou jako taby a lze je přetažením roztáhnout vedle sebe.
/// </summary>
public sealed class AvalonDockHost(DockingManager dock) : IMiniAppHost
{
    public IMiniAppPane AddPane(string contentId, string title, ImageSource? icon, object content, Action onClosed,
                               bool newColumn = false, bool newRow = false, bool canClose = true)
    {
        // CanClose=false skryje křížek a blokuje uživatelské zavření (i v plovoucím okně);
        // programové document.Close() (přepnutí session, CloseAll) AvalonDock pouští dál.
        var document = new LayoutDocument
        {
            ContentId = contentId,
            Title = title,
            IconSource = icon,
            Content = content,
            CanClose = canClose,
        };
        document.Closed += (_, _) => onClosed();
        if (newRow)
            // Řada přes celou šířku pod sloupci (1/3 výšky proti sloupcům 2/3).
            dock.Layout.RootPanel.Children.Add(new LayoutDocumentPane(document) { DockHeight = new GridLength(1, GridUnitType.Star) });
        else if (newColumn)
            ColumnsPanel.Children.Add(new LayoutDocumentPane(document));
        else
            DocumentPane.Children.Add(document);
        document.IsActive = true;
        return new Pane(document);
    }

    public void ResetToDefaultSkeleton()
    {
        // Plně kvalifikované — WPF markup temp projekt (_wpftmp) importuje i System.Windows.Forms,
        // takže holé `Orientation` je tam dvojznačné (CS0104), i když hlavní compile projde.
        var columns = new LayoutPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, DockHeight = new GridLength(2, GridUnitType.Star) };
        columns.Children.Add(new LayoutDocumentPane());
        var root = new LayoutPanel { Orientation = System.Windows.Controls.Orientation.Vertical };
        root.Children.Add(columns);
        dock.Layout.RootPanel = root;
    }

    // Vodorovný pás sloupců (výchozí skeleton). Fallback na root, kdyby skeleton nebyl (po restore).
    private LayoutPanel ColumnsPanel =>
        dock.Layout.RootPanel.Children.OfType<LayoutPanel>().FirstOrDefault() ?? dock.Layout.RootPanel;

    // První (levý) document pane — cíl pro taby a první sloupec.
    private LayoutDocumentPane DocumentPane =>
        dock.Layout.Descendents().OfType<LayoutDocumentPane>().First();

    public string SaveLayout()
    {
        var sb = new StringBuilder();
        using (var writer = new StringWriter(sb))
            new XmlLayoutSerializer(dock).Serialize(writer);
        return sb.ToString();
    }

    public void RestoreLayout(string layoutXml, Func<string, IMiniAppPane, RestoredContent?> resolve)
    {
        var serializer = new XmlLayoutSerializer(dock);
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
