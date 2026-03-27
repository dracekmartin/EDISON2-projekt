using System.Linq;
using System.Windows;
using EDControls.EDInputForm;

namespace EDISONMAIN2
{
    public partial class EDInputFormTestWindow : Window
    {
        private FormInputTestWindowViewModel? _vm;

        public EDInputFormTestWindow()
        {
            DataContext = _vm = new FormInputTestWindowViewModel();
            InitializeComponent();
            TheInputForm.CacheClass = "ALVA.EDControls.EDInputForm.EDIFExample";
        }

        private void Init_Click(object sender, RoutedEventArgs e)
        {
            TheInputForm.Init();
            int count = TheInputForm.Items?.Count() ?? 0;
            if (_vm != null) _vm.Status = $"Init OK — {count} položek načteno";
        }

        private void Validate_Click(object sender, RoutedEventArgs e)
        {
            bool ok = TheInputForm.Validate();
            int errors = TheInputForm.Items?.OfType<Field>().Count(f => f.HasError) ?? 0;
            if (_vm != null) _vm.Status = ok
                ? "Validate OK — žádné chyby"
                : $"Validate: {errors} chyba(y) — viz červené pole";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            bool ok = TheInputForm.Save();
            if (_vm == null) return;
            if (!ok)
            {
                int errors = TheInputForm.Items?.OfType<Field>().Count(f => f.HasError) ?? 0;
                _vm.Status = $"Validate: {errors} chyba(y) — viz červené pole";
                return;
            }
            _vm.Status = "Save OK";
        }
    }
}
