using System.Windows;
using System.Windows.Threading;

namespace EDUPDATE2;

public partial class App : Application
{
    private Mutex? _instanceMutex;
    internal static UpdateLog? Log;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log?.Write("Výjimka", args.ExceptionObject.ToString() ?? "");

        // Dva souběžné updatery nad C:\Edison = rozbitá instalace.
        _instanceMutex = new Mutex(initiallyOwned: true, @"Global\EDUPDATE2", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("Aktualizace klienta systému EDISON již běží.", "EDUPDATE2",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            Environment.ExitCode = UpdateEngine.ExitBadParamsOrAborted;
            Shutdown(UpdateEngine.ExitBadParamsOrAborted);
            return;
        }

        var unknownTokens = new List<string>();
        var args = UpdaterArgs.Parse(e.Args, unknownTokens.Add);
        Log = new UpdateLog(args.TargetPath);
        foreach (var token in unknownTokens)
            Log.Write("Parametry", $"Neznámý argument ignorován: {token}");

        MainWindow = new UpdateWindow(args, Log);
        MainWindow.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        Log?.Write("Výjimka", e.Exception.ToString());
        MessageBox.Show($"Neočekávaná chyba aktualizace: {e.Exception.Message}", "EDUPDATE2",
            MessageBoxButton.OK, MessageBoxImage.Error);
        // UI výjimka během swap/rollbacku nesmí zabít proces - engine musí dodělat disk.
        if (MainWindow is UpdateWindow { InCriticalPhase: true }) return;
        Environment.ExitCode = UpdateEngine.ExitBadParamsOrAborted;
        Shutdown(UpdateEngine.ExitBadParamsOrAborted);
    }
}
