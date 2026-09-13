using System.Windows;
using EDControls;
using EDControls.MiniApps;
using EDISONMAIN2.MiniApps;

namespace EDISONMAIN2.Windows.MiniAppPickerWindow
{
    public partial class MiniAppPickerWindow : Window
    {
        private readonly MiniAppPickerViewModel _vm;

        public MiniAppPickerWindow(MiniAppCatalog catalog, IReadOnlyList<MiniAppTarget> targets)
        {
            DataContext = _vm = new MiniAppPickerViewModel(catalog, targets);
            InitializeComponent();
            // CenterOwner u maximalizovaného shellu centruje podle restore bounds (vzor ChangelogWindow).
            Loaded += (_, _) => { OwnerWindowCentering.CenterOnOwner(this); AppList.Focus(); };
        }

        /// <summary>Vybraná miniaplikace; volající ji čte po <c>ShowDialog() == true</c>.</summary>
        public MiniAppDescriptor? Picked => _vm.Selected?.Descriptor;

        /// <summary>Vybrané umístění; null = nebylo z čeho vybírat → panel vyplní celou plochu.</summary>
        public MiniAppPlacement? Placed => _vm.SelectedPlacement?.Placement;

        private void AppItem_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
            _vm.OkCommand.Execute(null);
    }
}
