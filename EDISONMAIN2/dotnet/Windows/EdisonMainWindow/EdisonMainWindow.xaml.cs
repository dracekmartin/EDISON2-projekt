using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EDControls.MiniApps;
using EDISONMAIN2.MiniApps;
using EDISONMAIN2.Windows.EdisonMainWindow;
// Alias proti ambient System.Windows.Forms (UseWindowsForms je zapnuté pro FontDialog).
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace EDISONMAIN2
{
    /// <summary>
    /// Interaction logic for EdisonWindow.xaml
    /// </summary>
    public partial class EdisonMainWindow : Window
    {
        // MiniAppManager žije ve view vrstvě — pracuje s DockingManagerem (view element).
        private readonly MiniAppManager _miniAppManager;

        public EdisonMainWindow()
        {
            InitializeComponent();
            _miniAppManager = new MiniAppManager(new AvalonDockHost(DockManager));
            PreviewKeyDown += OnPreviewKeyDown;
            Closed += (_, _) => _miniAppManager.CloseAll();
            Loaded += WireMiniAppSession;
        }

        // SessionReplaced lze napojit až po nastavení DataContextu (BuildShellWindow ho nastaví
        // po konstruktoru) — proto na Loaded, a jen jednou.
        private void WireMiniAppSession(object sender, RoutedEventArgs e)
        {
            Loaded -= WireMiniAppSession;
            if (DataContext is EdisonMainViewModel vm)
                vm.SessionReplaced += _miniAppManager.OnSessionReplaced;
        }

        // Dočasný dev launcher (commit 4). Nahradí ho launcher z katalogu + ShowMiniApps (commit 5).
        private void OpenTestMiniApp_Click(object sender, RoutedEventArgs e)
        {
            _miniAppManager?.Open(new MiniAppDescriptor("Dev.Sample",
                () => new SimpleMiniAppViewModel("Test miniapp",
                    () => new TextBlock { Text = "Ahoj z miniapp", Margin = new Thickness(8) })));
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
