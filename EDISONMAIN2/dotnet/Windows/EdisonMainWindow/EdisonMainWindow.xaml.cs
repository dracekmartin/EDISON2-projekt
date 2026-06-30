using System.Windows;
using System.Windows.Input;
using EDISONMAIN2.MiniApps;
using EDISONMAIN2.Windows.EdisonMainWindow;
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
            }
        }
    }
}
