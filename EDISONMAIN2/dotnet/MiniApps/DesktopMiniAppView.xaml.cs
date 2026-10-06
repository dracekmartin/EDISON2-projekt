using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
// Aliasy proti ambient System.Windows.Forms (UseWindowsForms zapnuté pro WinForms color picker; _wpftmp importuje WinForms).
using UserControl = System.Windows.Controls.UserControl;
using DragEventArgs = System.Windows.DragEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using DataObject = System.Windows.DataObject;
using DragDrop = System.Windows.DragDrop;
using DragDropEffects = System.Windows.DragDropEffects;

namespace EDISONMAIN2.MiniApps;

public partial class DesktopMiniAppView : UserControl
{
    private const double TileCellWidth = 140;   // shodné s TileGridPanel.CellWidth (default v šabloně panelu)
    private const double TilesHostMargin = 12;  // Margin="6" ItemsControlu na obou stranách

    // Přeuspořádání dlaždic tažením; klik hned po tažení dlaždici nespustí.
    private readonly DesktopDragGesture<DesktopShortcut> _tileDrag = new();

    public DesktopMiniAppView() => InitializeComponent();

    private DesktopMiniAppViewModel? Vm => DataContext as DesktopMiniAppViewModel;

    private void Tile_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_tileDrag.TakeClick()) return;
        if (((FrameworkElement)sender).DataContext is DesktopShortcut s) Vm?.Shortcuts.RequestLaunch(s);
    }

    private void Tile_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _tileDrag.Press(e.GetPosition(null), ((FrameworkElement)sender).DataContext as DesktopShortcut);

    private void Tile_MouseMove(object sender, MouseEventArgs e)
    {
        if (_tileDrag.Move(e.GetPosition(null), e.LeftButton == MouseButtonState.Pressed) is { } tile)
            DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(DesktopDragFormats.ReorderTile, tile), DragDropEffects.Move);
    }

    private void Desktop_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DesktopDragFormats.ReorderTile) ? DragDropEffects.Move
                  : e.Data.GetDataPresent(DesktopDragFormats.FromTree) ? DragDropEffects.Copy
                  : DragDropEffects.None;
        e.Handled = true;
    }

    private void Desktop_Drop(object sender, DragEventArgs e)
    {
        if (Vm?.Shortcuts is not { } svc) return;

        // Cílová buňka z pozice dropu; na prázdné ploše je ScrollViewer collapsed a souřadnice
        // vůči nezobrazenému panelu nic neříkají - položka pak zůstane na první volné buňce.
        var panel = FindPanel();
        (int Column, int Row)? cell = panel is { IsVisible: true } ? panel.CellAt(e.GetPosition(panel)) : null;

        if (e.Data.GetData(DesktopDragFormats.ReorderTile) is DesktopShortcut dragged)
        {
            if (cell is { } target) svc.PlaceAt(dragged, target.Column, target.Row);
        }
        else if (e.Data.GetData(DesktopDragFormats.FromTree) is ExecutableModule fromTree)
        {
            svc.Add(fromTree);                       // přidá (na první volnou buňku) - idempotentní
            // …a přesune na místo dropu; instanci dlaždice zná až služba (Add ji vytvořila).
            if (cell is { } target
                && svc.Items.FirstOrDefault(i => ReferenceEquals(i.Module, fromTree)) is { } tile)
                svc.PlaceAt(tile, target.Column, target.Row);
        }
        e.Handled = true;
    }

    // Nové dlaždice (★ ve stromu) mají padat do viditelné části - rozložení samo šířku nečte.
    // Šířka z eventu, ne z TilesHost: ten je ve scrollujícím ScrollVieweru široký jako obsah.
    private void Desktop_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (Vm?.Shortcuts is { } svc)
            svc.GridColumns = TileGridPanel.ColumnsFor(e.NewSize.Width - TilesHostMargin, TileCellWidth);
    }

    private TileGridPanel? FindPanel() => FindVisualChild<TileGridPanel>(TilesHost);

    private static T? FindVisualChild<T>(DependencyObject? root) where T : DependencyObject
    {
        if (root is null) return null;
        if (root is T hit) return hit;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (FindVisualChild<T>(VisualTreeHelper.GetChild(root, i)) is { } found) return found;
        return null;
    }
}
