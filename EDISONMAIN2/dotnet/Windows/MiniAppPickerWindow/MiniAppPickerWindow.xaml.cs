using System.Windows;
using System.Windows.Controls;
using EDControls;
using EDControls.MiniApps;

namespace EDISONMAIN2.Windows.MiniAppPickerWindow
{
    public partial class MiniAppPickerWindow : Window
    {
        private readonly MiniAppPickerViewModel _vm;

        public MiniAppPickerWindow(MiniAppCatalog catalog)
        {
            DataContext = _vm = new MiniAppPickerViewModel(catalog);
            InitializeComponent();
            // CenterOwner u maximalizovaného shellu centruje podle restore bounds (vzor ChangelogWindow).
            Loaded += (_, _) => { OwnerWindowCentering.CenterOnOwner(this); AppList.Focus(); };
        }

        /// <summary>Vybraná miniaplikace; volající ji čte po <c>ShowDialog() == true</c>.</summary>
        public MiniAppDescriptor? Picked => _vm.Selected?.Descriptor;

        private void AppItem_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
            _vm.OkCommand.Execute(null);
    }
}
