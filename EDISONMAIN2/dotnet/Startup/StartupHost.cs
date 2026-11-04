using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using EDControls;
using EDControls.EDViewModel;
using EDControls.ErrorReporting;
using EDISONMAIN2.Windows.LoginWindow;
using EDISONMAIN2.Windows.SetupWindow;
using static ALVA.EDISONAPI.Database;
// Alias proti ambient System.Windows.Forms (UseWindowsForms je zapnuté pro WinForms color picker).
using Application = System.Windows.Application;

namespace EDISONMAIN2;

/// <summary>
/// WPF strana startu: okna před shellem, dispečer a konec aplikace. Produkčně
/// <see cref="ApplicationStartupHost"/>, testy bootstrapperu ho nahrazují falešným.
/// </summary>
internal interface IStartupHost
{
    /// <summary>Do <see cref="HandOff"/> ukončuje aplikaci jen <see cref="Shutdown"/>.</summary>
    void HoldShutdown();

    /// <summary>Font a světlý/tmavý režim, ještě před prvním oknem.</summary>
    void InitializeAppearance();

    /// <summary>Setup wizard připojení; false = uživatel zrušil.</summary>
    bool ShowSetup(SetupWizardViewModel vm);

    /// <summary>Login okno; vrátí se po přihlášení (okno zůstává jako progress podklad) nebo po zavření (null).</summary>
    LoginResult? ShowLogin(LoginViewModel vm, IWindowStateStore windowState);

    /// <summary>Dialog vynucené změny hesla nad login oknem; false = uživatel zrušil.</summary>
    bool ShowChangePassword(IMLRest rest, string userName);

    /// <summary>Výběr jiné firmy po odmítnuté licenci nad login oknem; null = uživatel zrušil.</summary>
    CompanyStruct? PickOtherCompany(CompanyStruct refused, IReadOnlyList<CompanyStruct> offered);

    /// <summary>Vykreslí čekající změny hned, další krok zablokuje UI thread.</summary>
    void RenderNow();

    /// <summary>Předá shellu roli hlavního okna; false = ukončení už běží a shell se nemá ukázat.</summary>
    bool HandOff(EdisonMainWindow shell);

    /// <summary>Ukončení už běží: <see cref="Shutdown"/> (force logout, version gate) nebo uživatel zavřel shell.</summary>
    bool ShutdownRequested { get; }

    void Shutdown();

    void OnExit(Action action);

    /// <summary>Spustí akci na UI threadu (handlery serveru chodí z vlákna requestu).</summary>
    void Dispatch(Action action);

    /// <summary>Hlášení chyby startu, dialog nad login oknem, je-li.</summary>
    void ReportFailure(Exception ex);

    /// <summary>Server před přihlášením neodpovídá: hláška místo technického dialogu, do telemetrie jako varování.</summary>
    void ReportUnreachableServer(Exception failure, string server, int port);
}

/// <summary>Start nad skutečnou <see cref="Application"/>.</summary>
internal sealed class ApplicationStartupHost(Application application) : IStartupHost
{
    // Login okno dialog cesty: owner dalších dialogů a podklad, který se zavře až po vykreslení shellu.
    private Window? _loginWindow;
    private LoginViewModel? _login;
    // Čte ho i vlákno requestu (konec session ze serveru).
    private volatile bool _shutdownRequested;

    public void HoldShutdown() => application.ShutdownMode = ShutdownMode.OnExplicitShutdown;

    // Sdílený vzhled čte %APPDATA%\m-line\common a propaguje mezi procesy (TemplateDesigner).
    public void InitializeAppearance() => EDAppearance.Initialize(application);

    public bool ShowSetup(SetupWizardViewModel vm) => new Windows.SetupWindow.SetupWindow(vm).ShowDialog() == true;

    /// <summary>
    /// Okno se ukazuje bez ShowDialog a čeká se na <see cref="DispatcherFrame"/>: po přihlášení zůstává
    /// otevřené jako progress podklad, ShowDialog by se vrátil až po jeho zavření.
    /// </summary>
    public LoginResult? ShowLogin(LoginViewModel vm, IWindowStateStore windowState)
    {
        var loginWindow = new Windows.LoginWindow.LoginWindow(vm);
        // Geometrii sdílí se shellem, aby obě okna naskočila na monitoru, kde uživatel pracuje.
        // Okno ji jen přebírá - jediný zápis je níž, hned po úspěšném přihlášení.
        WindowStatePersistence.Attach(loginWindow, new ReadOnlyWindowStateStore(windowState));

        var frame = new DispatcherFrame();
        vm.LoginSucceeded += (_, _) => frame.Continue = false;
        loginWindow.Closed += (_, _) => frame.Continue = false;
        loginWindow.Show();
        Dispatcher.PushFrame(frame);

        if (vm.Result is not { } result) return null;
        _loginWindow = loginWindow;
        _login = vm;
        RenderNow();
        // Uložit tady: login okno se zavírá až po vykreslení shellu, který si geometrii čte dřív,
        // a v progress fázi ho uživatel může minimalizovat (stav při zavření už nic neříká).
        WindowStatePersistence.Save(loginWindow, windowState);
        return result;
    }

    public bool ShowChangePassword(IMLRest rest, string userName) =>
        new ChangePasswordWindow(rest, userName) { Owner = _loginWindow }.ShowDialog() == true;

    public CompanyStruct? PickOtherCompany(CompanyStruct refused, IReadOnlyList<CompanyStruct> offered) =>
        Windows.CompanyPickerWindow.CompanyPickerWindow.Pick(_loginWindow, refused, offered);

    public void RenderNow() => application.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);

    /// <summary>
    /// MainWindow se musí přiřadit explicitně - v dialog cestě ho drží otevřené login okno (WPF ho dá oknu
    /// vytvořenému, když žádný MainWindow není) a shell by roli nezískal. Pořadí (MainWindow →
    /// OnMainWindowClose) chrání před shutdownem při pozdějším zavření login okna, to se zavře až po prvním
    /// vykreslení shellu, aby v přechodu nebyla vidět plocha. Po Shutdown už WPF ShutdownMode nastavit nedovolí.
    /// </summary>
    public bool ShutdownRequested => _shutdownRequested;

    public bool HandOff(EdisonMainWindow shell)
    {
        if (_shutdownRequested) return false;
        application.MainWindow = shell;
        application.ShutdownMode = ShutdownMode.OnMainWindowClose;
        // Zavřením shellu WPF ukončí aplikaci samo, bez Shutdown: pozdní konec session pak hlášku neukáže.
        shell.Closed += (_, _) => _shutdownRequested = true;
        if (_login is { } login)
            shell.ContentRendered += (_, _) => login.CompleteHandoff();
        // Host žije celou session (ukončení přes ShellShutdown), login okno držet nemá.
        _login = null;
        _loginWindow = null;
        return true;
    }

    public void Shutdown()
    {
        _shutdownRequested = true;
        application.Shutdown();
    }

    public void OnExit(Action action) => application.Exit += (_, _) => action();

    public void Dispatch(Action action)
    {
        if (application.Dispatcher.HasShutdownStarted) return;
        // Zrušené volání by se v MLRest.Query ukázalo jako TaskCanceledException místo chyby serveru.
        try { application.Dispatcher.Invoke(action); }
        catch (OperationCanceledException) { }
    }

    public void ReportFailure(Exception ex) => new PopupDisplayer(_loginWindow).Error(ex);

    public void ReportUnreachableServer(Exception failure, string server, int port)
    {
        ErrorReports.ReportBackground(failure);
        new PopupDisplayer(_loginWindow).InfoSystem(AppBootstrapper.UnreachableServerMessage(server, port));
    }
}
