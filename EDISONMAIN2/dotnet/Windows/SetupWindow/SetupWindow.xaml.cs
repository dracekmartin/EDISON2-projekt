using System.Windows;

namespace EDISONMAIN2.Windows.SetupWindow
{
    /// <summary>
    /// First-launch setup: parita polí s legacy frmSetup - připojení (název, server, oba
    /// porty, IRIS), po testu připojení databáze (seznam ze serveru) a IDFa firmy; zápis
    /// <c>EDISONini.xml</c>. Logiku drží <see cref="SetupWizardViewModel"/>, okno jen binduje.
    /// Greenfield server-init (firma/licence/admin) je mimo scope - zůstává na legacy frmSetup.
    /// </summary>
    public partial class SetupWindow : Window
    {
        private readonly SetupWizardViewModel _vm;

        internal SetupWindow(SetupWizardViewModel vm)
        {
            DataContext = _vm = vm;
            InitializeComponent();
            _vm.RequestClose += result => DialogResult = result;
        }
    }
}
