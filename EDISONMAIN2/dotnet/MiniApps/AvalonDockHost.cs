using System.IO;
using System.Text;
using System.Windows.Media;
using AvalonDock;
using AvalonDock.Layout;
using AvalonDock.Layout.Serialization;

namespace EDISONMAIN2.MiniApps;

/// <summary>
/// Reálná implementace <see cref="IMiniAppHost"/> nad AvalonDock <see cref="DockingManager"/>.
/// Mini-app = <see cref="LayoutAnchorable"/> (dockovatelný/plovoucí panel). <c>CanHide=false</c> →
/// křížek panel zavře (vyvolá <c>onClosed</c>), ne jen schová do auto-hide.
/// </summary>
public sealed class AvalonDockHost(DockingManager dock) : IMiniAppHost
{
    public IMiniAppPane AddPane(string contentId, string title, ImageSource? icon, object content, Action onClosed)
    {
        var anchorable = new LayoutAnchorable
        {
            ContentId = contentId,
            Title = title,
            IconSource = icon,
            Content = content,
            CanClose = true,
            CanHide = false,
        };
        anchorable.Closed += (_, _) => onClosed();
        anchorable.AddToLayout(dock, AnchorableShowStrategy.Right | AnchorableShowStrategy.Most);
        return new Pane(anchorable);
    }

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
            // Hostujeme jen anchorable mini-apps; cokoli jiného (kdyby v XML bylo) zahodit.
            if (e.Model is not LayoutAnchorable anchorable ||
                resolve(e.Model.ContentId, new Pane(anchorable)) is not { } resolved)
            {
                e.Cancel = true;
                return;
            }
            anchorable.CanClose = true;
            anchorable.CanHide = false;
            e.Content = resolved.Content;
            anchorable.Closed += (_, _) => resolved.OnClosed();
        };
        using var reader = new StringReader(layoutXml);
        serializer.Deserialize(reader);
    }

    private sealed class Pane(LayoutAnchorable anchorable) : IMiniAppPane
    {
        public void Close() => anchorable.Close();
    }
}
