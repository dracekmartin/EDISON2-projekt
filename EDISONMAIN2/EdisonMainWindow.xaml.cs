using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace EDISONMAIN2
{
    /// <summary>
    /// Interaction logic for EdisonWindow.xaml
    /// </summary>
    public partial class EdisonMainWindow : Window
    {
        public EdisonMainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void tvApps_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DependencyObject? clickedItem = e.OriginalSource as DependencyObject;
            while (clickedItem != null && !(clickedItem is TreeViewItem))
                clickedItem = VisualTreeHelper.GetParent(clickedItem);

            if (clickedItem is TreeViewItem item && item.DataContext is IModule module)
            {
                item.IsSelected = true;
                if (DataContext is MainViewModel vm)
                    vm.OpenModuleCommand.Execute(module);
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.CloseAllProcesses();
        }

        private void mlrestTestButton_Click(object sender, RoutedEventArgs e)
        {
            /*
            CVar cvar = mlRest.Query("ALVA.MHD2.PlanDopravy.Forms.formMHD2PlanDopravyMain", "Test", new CVar());
            mlRest.RestStatus.ThrowIfError();
            MessageBox.Show($"testkey: {cvar["testkey"]}", "", MessageBoxButton.OK, MessageBoxImage.Information);
            */
        }
    }
}
