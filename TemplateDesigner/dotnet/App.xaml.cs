using System.Windows;
using System.Windows.Threading;
using TemplateDesigner.Config;

namespace TemplateDesigner;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnUnhandledException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var settings = AppSettings.Load();
        var mlRest   = CacheSchemaWiring.TryCreateMlRest(settings);
        new MainWindow(mlRest).Show();
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Neošetřená výjimka:\n\n{e.Exception}",
            "Chyba aplikace",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }
}

