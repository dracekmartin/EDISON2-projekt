using EDControls;
using EDISONMAIN2.Modules;
using EDISONMAIN2.ViewModels;
using System;
using System.Collections.Generic;
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

        private void TestFormInput_Click(object sender, RoutedEventArgs e)
        {
            var formInput = new FormInput
            {
                Items = new List<FormItem>
                {
                    new FormGroup { GroupName = "Osobní údaje" },
                    new FormField { Label = "Jméno",       FieldType = FieldType.Text },
                    new FormField { Label = "Příjmení",    FieldType = FieldType.Text },
                    new FormField { Label = "Věk",         FieldType = FieldType.Number },
                    new FormGroup { GroupName = "Adresa" },
                    new FormField { Label = "Ulice",       FieldType = FieldType.Text },
                    new FormField { Label = "Město",       FieldType = FieldType.Text },
                    new FormField { Label = "PSČ",         FieldType = FieldType.Number },
                    new FormGroup { GroupName = "Kontakt" },
                    new FormField { Label = "E-mail",      FieldType = FieldType.Text },
                    new FormField { Label = "Telefon",     FieldType = FieldType.Number },
                }
            };

            var win = new Window
            {
                Title = "Test FormInput",
                Width = 450,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Content = formInput
            };
            win.Show();
        }
    }
}
