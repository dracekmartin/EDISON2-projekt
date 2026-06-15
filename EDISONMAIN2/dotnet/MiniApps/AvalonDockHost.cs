using System.Windows.Media;
using AvalonDock;
using AvalonDock.Layout;

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

    private sealed class Pane(LayoutAnchorable anchorable) : IMiniAppPane
    {
        public void Close() => anchorable.Close();
    }
}
