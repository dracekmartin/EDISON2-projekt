using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using EDISONMAIN2.Modules;
// Aliasy proti ambient System.Windows.Forms (UseWindowsForms zapnuté pro WinForms color picker; _wpftmp importuje WinForms).
using UserControl = System.Windows.Controls.UserControl;
using DragEventArgs = System.Windows.DragEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using DataObject = System.Windows.DataObject;
using DragDrop = System.Windows.DragDrop;
using DragDropEffects = System.Windows.DragDropEffects;

namespace EDISONMAIN2.Desktop;

public partial class DesktopMiniAppView : UserControl
{
    private const double TileCellWidth = 140;   // shodné s TileGridPanel.CellWidth (default v šabloně panelu)
    private const double TilesHostMargin = 12;  // Margin="6" ItemsControlu na obou stranách

    // Přeuspořádání dlaždic tažením; dlaždici spustí jen klik bez tažení (stisk i uvolnění na ní).
    private static DesktopDragGesture Gesture => DesktopDragGesture.Shared;

    public DesktopMiniAppView() => InitializeComponent();

    private DesktopMiniAppViewModel? Vm => DataContext as DesktopMiniAppViewModel;

    // Plovoucí nebo vysunutá plocha není v hlavním okně, její stisky by hlavní okno neviděla.
    private void Desktop_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => Gesture.Reset();

    private void Tile_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is DesktopShortcut s && Gesture.TakeClick(s.Key))
            Vm?.Shortcuts.RequestLaunch(s);
    }

    private void Tile_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is DesktopShortcut s) Gesture.Press(e.GetPosition(null), s, s.Key);
    }

    private void Tile_MouseMove(object sender, MouseEventArgs e)
    {
        if (Gesture.Move<DesktopShortcut>(e.GetPosition(null), e.LeftButton == MouseButtonState.Pressed) is { } tile)
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
            svc.Add(fromTree, cell);
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
