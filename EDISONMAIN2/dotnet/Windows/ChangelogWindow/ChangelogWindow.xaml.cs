using System.Windows;
using EDControls;

namespace EDISONMAIN2.Windows.ChangelogWindow
{
    public partial class ChangelogWindow : Window
    {
        private readonly ChangelogViewModel _vm;

        public ChangelogWindow(string moduleTitle, ChangelogContent content, bool snoozed = false)
        {
            DataContext = _vm = new ChangelogViewModel(moduleTitle, content, snoozed);
            InitializeComponent();
            Title = _vm.ModuleTitle;
            // CenterOwner u maximalizovaného shellu centruje podle restore bounds - oprava
            // přes skutečnou klientskou oblast ownera (vzor EDPopup).
            Loaded += (_, _) => OwnerWindowCentering.CenterOnOwner(this);
        }

        /// <summary>Stav „nezobrazovat do příští změny" - presenter ho čte po Closed.</summary>
        public bool SnoozeChecked => _vm.SnoozeChecked;
    }
}
