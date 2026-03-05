using ALWA.SYS;
using System.Windows;

namespace EDISONMAIN2
{
    public partial class EDListTestWindow : Window
    {
        public EDListTestWindow(MLRest mlRest)
        {
            InitializeComponent();
            TheList.MLRest     = mlRest;
            TheList.CacheClass = "ALVA.EDControls.EDList.MockList";

            TheList.SelectionChanged  += (_, id)  => StatusText.Text = $"Výběr: {id ?? "(žádný)"}";
            TheList.RowDoubleClicked  += (_, id)  => StatusText.Text = $"Dvojklik: {id}";
            TheList.CellLinkClicked   += (_, e)   => LinkText.Text = $"Link: {e.ColKey} = {e.RowId}";
        }

        private void InitializeList_Click(object sender, RoutedEventArgs e)
        {
            TheList.InitializeList();
            int count = TheList.Columns?.Count ?? 0;
            StatusText.Text = $"InitializeList OK — {count} sloupců";
        }

        private void LoadList_Click(object sender, RoutedEventArgs e)
        {
            TheList.LoadList();
            StatusText.Text = "LoadList OK";
        }
    }
}
