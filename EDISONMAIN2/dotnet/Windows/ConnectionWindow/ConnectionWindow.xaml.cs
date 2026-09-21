using System.Windows;
using EDControls;

namespace EDISONMAIN2.Windows.ConnectionWindow
{
    public partial class ConnectionWindow : Window
    {
        private readonly ConnectionViewModel _vm;

        public ConnectionWindow(ILocalPrefs prefs, ConnectionChoice defaultConnection)
        {
            DataContext = _vm = new ConnectionViewModel(prefs, defaultConnection);
            InitializeComponent();
            // CenterOwner u maximalizovaného shellu centruje podle restore bounds (vzor MiniAppPickerWindow).
            Loaded += (_, _) => { OwnerWindowCentering.CenterOnOwner(this); ConnectionList.Focus(); };
        }

        /// <summary>Vybrané připojení; volající ho čte po <c>ShowDialog() == true</c>.</summary>
        public ConnectionChoice? Picked => _vm.Selected;

        private void ConnectionItem_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
            _vm.OkCommand.Execute(null);
    }
}
