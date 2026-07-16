using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EDISONMAIN2.MiniApps;
using EDISONMAIN2.Windows.EdisonMainWindow;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
// Alias proti ambient System.Windows.Forms (UseWindowsForms je zapnuté pro FontDialog).
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using DataObject = System.Windows.DataObject;
using Point = System.Windows.Point;
using DragDrop = System.Windows.DragDrop;
using DragDropEffects = System.Windows.DragDropEffects;

namespace EDISONMAIN2
{
    /// <summary>
    /// Interaction logic for EdisonMainWindow.xaml
    /// </summary>
    public partial class EdisonMainWindow : Window
    {
        public EdisonMainWindow()
        {
            InitializeComponent();
            PreviewKeyDown += OnPreviewKeyDown;
            Loaded += WireMiniAppHost;
        }

        // MiniAppManager (ve VM) potřebuje DockingManager z view → host injektneme po načtení okna,
        // kdy už je DataContext nastaven (BuildShellWindow ho nastaví po konstruktoru). Jen jednou.
        private void WireMiniAppHost(object sender, RoutedEventArgs e)
        {
            Loaded -= WireMiniAppHost;
            if (DataContext is EdisonMainViewModel vm)
                vm.AttachMiniAppHost(new AvalonDockHost(DockManager));
        }

        // Single-click ve stromu: složka toggle expand, modul spustí. Event bubluje přes
        // vnořené TreeViewItemy — Handled=true zastaví rodiče, aby klik na modul
        // zároveň nesbalil jeho složku. Chevron neprojde (ToggleButton si MouseUp zpracuje sám).
        private void TreeItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var item = (TreeViewItem)sender;
            e.Handled = true;
            // Právě proběhl drag modulu → nespouštět ho jako klik.
            if (_treeDragActive) { _treeDragActive = false; return; }
            switch (item.DataContext)
            {
                case ModuleFamily:
                    item.IsExpanded = !item.IsExpanded;
                    break;
                case ExecutableModule module:
                    OpenModule(module);
                    break;
            }
        }

        private Point _treeDragStart;
        private ExecutableModule? _treeDragModule;
        private bool _treeDragActive;

        // Zapamatuje modul + bod stisku (tunneling: rodič-family nastaví null, list-modul přepíše).
        private void TreeItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _treeDragStart = e.GetPosition(null);
            _treeDragModule = ((TreeViewItem)sender).DataContext as ExecutableModule;
        }

        // Po překročení prahu spustí drag modulu (do miniaplikace Oblíbené). Práh odliší od kliku.
        private void TreeItem_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _treeDragModule is null) return;
            var p = e.GetPosition(null);
            if (Math.Abs(p.X - _treeDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(p.Y - _treeDragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;

            var module = _treeDragModule;
            _treeDragModule = null;
            _treeDragActive = true;
            DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(ModuleDrag.FromTree, module), DragDropEffects.Copy);
        }

        // Pravý klik defaultně selekci nemění → kontextové menu by se vizuálně nevztahovalo
        // ke kliknuté položce. Bublání: vnitřní item se označí první, Handled zastaví rodiče.
        // ContextMenu se otevírá až na MouseUp, takže Handled ho neblokuje.
        private void TreeItem_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            ((TreeViewItem)sender).IsSelected = true;
            e.Handled = true;
        }

        private void UsedModuleItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (((ListBoxItem)sender).DataContext is ExecutableModule module)
                OpenModule(module);
        }

        // CanExecute guard — AsyncRelayCommand během běhu vrací false, brání dvojímu spuštění.
        private void OpenModule(ExecutableModule module)
        {
            if (DataContext is EdisonMainViewModel vm && vm.OpenModuleCommand.CanExecute(module))
                vm.OpenModuleCommand.Execute(module);
        }

        // Ctrl+W+W chord detection. PreviewKeyDown (ne KeyDown) — chytá klávesy předtím,
        // než je child controls (menu, TextBox) zpracují. Bez Preview by Ctrl+W v Menu
        // zavřel popup místo aby spustil chord.
        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not EdisonMainViewModel vm) return;

            bool isCtrlW = e.Key == Key.W && (Keyboard.Modifiers & ModifierKeys.Control) != 0;
            if (isCtrlW)
            {
                vm.HandleCtrlW();
                e.Handled = true;
            }
            else
            {
                // Ne-Ctrl+W klávesa → reset chord (uživatel pokračuje jinde).
                // Pouze pokud chord právě čeká, jinak žádná akce.
                vm.ResetChord();

                if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    TreeSearchBox.Focus();
                    TreeSearchBox.SelectAll();
                    e.Handled = true;
                }
            }
        }
    }
}
