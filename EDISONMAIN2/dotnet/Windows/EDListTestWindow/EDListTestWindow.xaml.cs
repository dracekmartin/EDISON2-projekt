using System.Windows;
using System.Windows.Controls;
using EDControls.EDList;

namespace EDISONMAIN2
{
    public partial class EDListTestWindow : Window
    {
        public EDListTestWindow()
        {
            DataContext = new EDControls.EDViewModel.EDViewModel();
            InitializeComponent();
            TheList.CacheClass = "ALVA.EDControls.EDList.MockList";
            TheList.HostId = "EDISONMAIN2.EDListTestWindow";  // persist layoutu sloupců
            TheList.Title  = "Test EDList - mock data";       // header + vyhledávání

            TheList.SelectionChanged    += (_, id) => StatusText.Text  = $"Výběr: {id ?? "(žádný)"}";
            TheList.RowDoubleClicked    += (_, id) => StatusText.Text  = $"Dvojklik: {id}";
            TheList.CellLinkClicked     += (_, e)  => LinkText.Text    = $"Link: {e.ColKey} = {e.RowId}";
            TheList.CheckedRowsChanged  += (_, ids) => CheckedText.Text = $"Checked: {string.Join(", ", ids)}";
        }

        private void InitializeList_Click(object sender, RoutedEventArgs e)
        {
            TheList.InitializeList();
            int count = TheList.Columns?.Count ?? 0;
            StatusText.Text = $"InitializeList OK - {count} sloupců";
        }

        private void LoadList_Click(object sender, RoutedEventArgs e)
        {
            TheList.LoadList();
            StatusText.Text = "LoadList OK";
        }

        private void ModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TheList is null) return;
            TheList.SelectionMode = ModeCombo.SelectedIndex == 1
                ? EListSelectionMode.Check
                : EListSelectionMode.Single;
        }

        private void CheckAll_Click(object sender, RoutedEventArgs e)   => TheList.CheckAll();
        private void UncheckAll_Click(object sender, RoutedEventArgs e) => TheList.UncheckAll();
    }
}
