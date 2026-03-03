using ALWA.SYS;
using System.Linq;
using System.Windows;
using EDControls.EDInputForm;

namespace EDISONMAIN2
{
    public partial class InputFormTestWindow : Window
    {
        public InputFormTestWindow(MLRest mlRest)
        {
            InitializeComponent();
            TheInputForm.MLRest      = mlRest;
            TheInputForm.CacheClass  = "ALVA.EDISONMAIN2.test";
        }

        private void Init_Click(object sender, RoutedEventArgs e)
        {
            TheInputForm.Init();
            int count = TheInputForm.Items?.Count() ?? 0;
            StatusText.Text = $"Init OK — {count} položek načteno";
        }

        private void Validate_Click(object sender, RoutedEventArgs e)
        {
            bool ok = TheInputForm.Validate();
            int errors = TheInputForm.Items?.OfType<Field>().Count(f => f.HasError) ?? 0;
            StatusText.Text = ok
                ? "Validate OK — žádné chyby"
                : $"Validate: {errors} chyba(y) — viz červené pole";
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            bool ok = TheInputForm.Save();
            if (!ok)
            {
                int errors = TheInputForm.Items?.OfType<Field>().Count(f => f.HasError) ?? 0;
                StatusText.Text = $"Validate: {errors} chyba(y) — viz červené pole";
                return;
            }
            StatusText.Text = "Save OK";
        }
    }
}
