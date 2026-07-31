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

public partial class FavoritesMiniAppView : UserControl
{
    private Point _dragStart;
    private ExecutableModule? _dragModule;
    private bool _suppressClick;   // po reorder tažení potlačí následný klik (jinak by dlaždice i spustila)

    public FavoritesMiniAppView() => InitializeComponent();

    private FavoritesMiniAppViewModel? Vm => DataContext as FavoritesMiniAppViewModel;

    // Klik na dlaždici → spuštění modulu (přeskočeno, pokud právě proběhlo přetažení).
    private void Tile_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_suppressClick) { _suppressClick = false; return; }
        if (((FrameworkElement)sender).DataContext is ExecutableModule m) Vm?.Shortcuts.RequestLaunch(m);
    }

    private void Tile_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragModule = ((FrameworkElement)sender).DataContext as ExecutableModule;
    }

    private void Tile_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragModule is null) return;
        var p = e.GetPosition(null);
        if (Math.Abs(p.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(p.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        var module = _dragModule;
        _dragModule = null;
        _suppressClick = true;
        DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(ModuleDrag.Reorder, module), DragDropEffects.Move);
    }

    private void Favorites_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(ModuleDrag.Reorder) ? DragDropEffects.Move
                  : e.Data.GetDataPresent(ModuleDrag.FromTree) ? DragDropEffects.Copy
                  : DragDropEffects.None;
        e.Handled = true;
    }

    private void Favorites_Drop(object sender, DragEventArgs e)
    {
        if (Vm?.Shortcuts is not { } svc) return;

        // Cílový slot z pozice dropu (mřížka; -1 když panel není realizovaný = prázdné oblíbené).
        var panel = FindPanel();
        int slot = panel is not null ? panel.SlotAt(e.GetPosition(panel)) : -1;

        if (e.Data.GetData(ModuleDrag.Reorder) is ExecutableModule dragged)
        {
            if (slot >= 0 && svc.Contains(dragged.ElementId)) svc.PlaceAt(dragged, slot);
        }
        else if (e.Data.GetData(ModuleDrag.FromTree) is ExecutableModule fromTree)
        {
            svc.Add(fromTree);                       // přidá (assign next free slot) — idempotentní
            if (slot >= 0) svc.PlaceAt(fromTree, slot);   // …a přesune na místo dropu
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
