using System.IO;
using System.Text;
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
    public IMiniAppPane AddPane(string contentId, string title, ImageSource? icon, object content, Action onClosed, bool newColumn = false)
    {
        var document = new LayoutDocument
        {
            ContentId = contentId,
            Title = title,
            IconSource = icon,
            Content = content,
            CanClose = true,
        };
        document.Closed += (_, _) => onClosed();
        if (newColumn)
        {
            var column = new LayoutDocumentPane(document);
            dock.Layout.RootPanel.Children.Add(column);
        }
        else
        {
            DocumentPane.Children.Add(document);
        }
        document.IsActive = true;
        return new Pane(document);
    }

    // První (levý) document pane z XAML LayoutRoot; další sloupce se přidávají do RootPanelu.
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
            document.CanClose = true;
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
