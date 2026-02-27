// Explicitní aliasy pro řešení konfliktů mezi WPF a WinForms typy.
// Důvod: projekt používá UseWPF + UseWindowsForms (kvůli ColorDialog).
// Implicitní globální usingy obou SDK by jinak způsobily nejednoznačné reference.

global using Application    = System.Windows.Application;
global using Button         = System.Windows.Controls.Button;
global using CheckBox       = System.Windows.Controls.CheckBox;
global using Color          = System.Windows.Media.Color;
global using Colors         = System.Windows.Media.Colors;
global using ComboBox       = System.Windows.Controls.ComboBox;
global using Cursor         = System.Windows.Input.Cursor;
global using Cursors        = System.Windows.Input.Cursors;
global using FontFamily     = System.Windows.Media.FontFamily;
global using KeyEventArgs   = System.Windows.Input.KeyEventArgs;
global using MessageBox     = System.Windows.MessageBox;
global using MessageBoxButton = System.Windows.MessageBoxButton;
global using MessageBoxImage  = System.Windows.MessageBoxImage;
global using MessageBoxResult = System.Windows.MessageBoxResult;
global using MouseEventArgs = System.Windows.Input.MouseEventArgs;
global using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
global using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
global using TextBox        = System.Windows.Controls.TextBox;
global using UserControl    = System.Windows.Controls.UserControl;
