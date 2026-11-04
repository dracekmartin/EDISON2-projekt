using System.Windows;
using System.Windows.Input;
using EDControls;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2.Windows.CompanyPickerWindow
{
    public partial class CompanyPickerWindow : Window
    {
        private readonly CompanyPickerViewModel _vm;
        private bool _enterConfirms;

        public CompanyPickerWindow(CompanyStruct refused, IReadOnlyList<CompanyStruct> offered)
            : this(new CompanyPickerViewModel(refused, offered)) { }

        internal CompanyPickerWindow(CompanyPickerViewModel vm)
        {
            DataContext = _vm = vm;
            InitializeComponent();
            // CenterOwner u maximalizovaného shellu centruje podle restore bounds (vzor ChangelogWindow).
            Loaded += (_, _) => { OwnerWindowCentering.CenterOnOwner(this); CompanyCombo.Focus(); };
            // Striktní pole Enter spotřebuje na potvrzení výběru a Ctrl+Enter dialog bez WindowNavigator nemá:
            // Enter v poli se zavřeným seznamem potvrdí dialog. Stav seznamu se čte před tím, než ho pole zavře.
            PreviewKeyDown += (_, e) => _enterConfirms = e.Key == Key.Enter && !CompanyCombo.IsDropDownOpen
                                                         && IsInCombo(e.OriginalSource);
            AddHandler(KeyDownEvent, new System.Windows.Input.KeyEventHandler((_, e) =>
            {
                if (e.Key == Key.Enter && _enterConfirms) _vm.OkCommand.Execute(null);
            }), handledEventsToo: true);
        }

        private bool IsInCombo(object source) =>
            source is DependencyObject d && (d == CompanyCombo || CompanyCombo.IsAncestorOf(d));

        /// <summary>Vybraná firma; null = uživatel zrušil.</summary>
        public CompanyStruct? Picked => _vm.Picked;

        /// <summary>Dialog nad <paramref name="owner"/>; null = uživatel zrušil.</summary>
        public static CompanyStruct? Pick(Window? owner, CompanyStruct refused, IReadOnlyList<CompanyStruct> offered)
        {
            var window = new CompanyPickerWindow(refused, offered) { Owner = owner };
            return window.ShowDialog() == true ? window.Picked : null;
        }
    }
}
