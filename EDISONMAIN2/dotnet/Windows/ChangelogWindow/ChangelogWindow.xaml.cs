using System.Windows;

namespace EDISONMAIN2.Windows.ChangelogWindow
{
    public partial class ChangelogWindow : Window
    {
        private readonly ChangelogViewModel _vm;

        public ChangelogWindow(string moduleTitle, ChangelogContent content)
        {
            DataContext = _vm = new ChangelogViewModel(moduleTitle, content);
            InitializeComponent();
            Title = _vm.ModuleTitle;
        }

        /// <summary>Stav „nezobrazovat do příští změny" - presenter ho čte po Closed.</summary>
        public bool SnoozeChecked => _vm.SnoozeChecked;
    }
}
