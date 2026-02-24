using ALWA.SYS;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace EDISONMAIN2
{
    /// <summary>
    /// Interaction logic for EdisonWindow.xaml
    /// </summary>
    public partial class EdisonMainWindow : Window
    {
        private List<Process> _processes = new List<Process>();

        public EdisonMainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            
        }

        

        private void tvApps_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            MainViewModel mainViewModel = this.DataContext as MainViewModel;

            DependencyObject clickedItem = e.OriginalSource as DependencyObject;

            while (clickedItem != null && !(clickedItem is TreeViewItem))
            {
                clickedItem = VisualTreeHelper.GetParent(clickedItem);
            }

            if (clickedItem is TreeViewItem item)
            {
                item.IsSelected = true;

                if (item.DataContext is ModuleFamily moduleFamily)
                {
                    string name = moduleFamily.DisplayedName;

                    MessageBox.Show($"You double-clicked {item.DataContext.GetType().Name}: {name}");
                }
                else if (item.DataContext is ExecutableModule executableModule)
                {
                    if (!string.IsNullOrEmpty(executableModule.ExecutablePath))
                    {

                        Process? prc = Process.Start(new ProcessStartInfo()
                        {
                            UseShellExecute = true,
                            FileName = Path.Combine("C:\\EDISON", executableModule.ExecutablePath),
                            Arguments = $"EDISONMAIN2 \"{mainViewModel.Server}\" \"{mainViewModel.RestPort}\" \"{mainViewModel.Namespace}\" \"{mainViewModel.Idfa}\" \"{mainViewModel.UserName}\" \"{mainViewModel.FullName}\" \"{mainViewModel.Mlp}\" \"{executableModule.FormName}\"",

                        });
                        if (prc != null) _processes.Add(prc);
                    }
                }
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            foreach (var process in _processes)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error closing process: {ex.Message}");
                }
            }
        }

        

        private void mlrestTestButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MainViewModel mainViewModel = this.DataContext as MainViewModel;

                

                /*
                CVar cvar = mlRest.Query("ALVA.MHD2.PlanDopravy.Forms.formMHD2PlanDopravyMain", "Test", new CVar());
                mlRest.RestStatus.ThrowIfError();

                MessageBox.Show($"testkey: {cvar["testkey"]}", "", MessageBoxButton.OK, MessageBoxImage.Information);
                */
            }
            catch (Exception ex)
            {
                MessageBox.Show($"MLRest error: {ex.Message}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
