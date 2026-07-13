using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EDISONMAIN2.MiniApps;
using EDISONMAIN2.Windows.EdisonMainWindow;
using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
// Alias proti ambient System.Windows.Forms (UseWindowsForms je zapnuté pro FontDialog).
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

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
