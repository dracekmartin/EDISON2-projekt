using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
// Aliasy proti ambient System.Windows.Forms (UseWindowsForms zapnuté pro WinForms color picker; _wpftmp importuje WinForms).
using UserControl = System.Windows.Controls.UserControl;
using Point = System.Windows.Point;
using DragEventArgs = System.Windows.DragEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using DataObject = System.Windows.DataObject;
using DragDrop = System.Windows.DragDrop;
using DragDropEffects = System.Windows.DragDropEffects;

namespace EDISONMAIN2.MiniApps;

public partial class DesktopMiniAppView : UserControl
{
    private Point _dragStart;
    private DesktopShortcut? _dragTile;
    private bool _suppressClick;   // po reorder tažení potlačí následný klik (jinak by dlaždice i spustila)

    public DesktopMiniAppView() => InitializeComponent();

    private DesktopMiniAppViewModel? Vm => DataContext as DesktopMiniAppViewModel;

    // Klik na dlaždici → spuštění zástupce (přeskočeno, pokud právě proběhlo přetažení).
    private void Tile_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_suppressClick) { _suppressClick = false; return; }
        if (((FrameworkElement)sender).DataContext is DesktopShortcut s) Vm?.Shortcuts.RequestLaunch(s);
    }

    private void Tile_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragTile = ((FrameworkElement)sender).DataContext as DesktopShortcut;
    }

    private void Tile_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragTile is null) return;
        var p = e.GetPosition(null);
        if (Math.Abs(p.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(p.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        var tile = _dragTile;
        _dragTile = null;
        _suppressClick = true;
        DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(ModuleDrag.ReorderTile, tile), DragDropEffects.Move);
    }

    private void Desktop_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(ModuleDrag.ReorderTile) ? DragDropEffects.Move
                  : e.Data.GetDataPresent(ModuleDrag.FromTree) ? DragDropEffects.Copy
                  : DragDropEffects.None;
        e.Handled = true;
    }

    private void Desktop_Drop(object sender, DragEventArgs e)
    {
        if (Vm?.Shortcuts is not { } svc) return;

        // Cílový slot z pozice dropu (mřížka; -1 když panel není realizovaný = prázdná plocha).
        var panel = FindPanel();
        int slot = panel is not null ? panel.SlotAt(e.GetPosition(panel)) : -1;

        if (e.Data.GetData(ModuleDrag.ReorderTile) is DesktopShortcut dragged)
        {
            if (slot >= 0) svc.PlaceAt(dragged, slot);
        }
        else if (e.Data.GetData(ModuleDrag.FromTree) is ExecutableModule fromTree)
        {
            svc.Add(fromTree);                       // přidá (assign next free slot) - idempotentní
            // …a přesune na místo dropu; instanci dlaždice zná až služba (Add ji vytvořila).
            if (slot >= 0 && svc.Items.FirstOrDefault(i => ReferenceEquals(i.Module, fromTree)) is { } tile)
                svc.PlaceAt(tile, slot);
        }
        e.Handled = true;
    }

    private SlotGridPanel? FindPanel() => FindVisualChild<SlotGridPanel>(TilesHost);

    private static T? FindVisualChild<T>(DependencyObject? root) where T : DependencyObject
    {
        if (root is null) return null;
        if (root is T hit) return hit;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (FindVisualChild<T>(VisualTreeHelper.GetChild(root, i)) is { } found) return found;
        return null;
    }
}
