using EDISONMAIN2.Modules;
using EDISONMAIN2.ViewModels;
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

        private void Tree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DependencyObject? clickedItem = e.OriginalSource as DependencyObject;
            while (clickedItem != null && clickedItem is not TreeViewItem)
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
    }
}
