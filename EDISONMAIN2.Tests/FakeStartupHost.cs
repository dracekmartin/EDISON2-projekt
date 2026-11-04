using System;
using System.Collections.Generic;
using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.Windows.LoginWindow;
using EDISONMAIN2.Windows.SetupWindow;
using static ALVA.EDISONAPI.Database;

namespace EDISONMAIN2.Tests;

/// <summary>
/// WPF strana startu bez oken: odpovědi dialogů nastavuje test, kroky zapisuje do <see cref="Calls"/>
/// (sdílený seznam, ať se proloží s kroky openeru).
/// </summary>
internal sealed class FakeStartupHost(List<string>? calls = null) : IStartupHost
{
    public List<string> Calls { get; } = calls ?? [];

    public bool SetupCompleted { get; set; }
    public SetupWizardViewModel? ShownSetup { get; private set; }
    /// <summary>Výsledek login okna; null = uživatel okno zavřel. Čte se až při zobrazení okna.</summary>
    public Func<LoginResult?> DialogLogin { get; set; } = () => null;
    public bool PasswordChanged { get; set; } = true;
    public (IMLRest LoginRest, string UserName)? PasswordChange { get; private set; }
    public LoginViewModel? ShownLogin { get; private set; }
    public IWindowStateStore? ShownLoginWindowState { get; private set; }
    public EdisonMainWindow? HandedOff { get; private set; }
    public bool ShutdownCalled { get; private set; }
    public Exception? ReportedFailure { get; private set; }
    public List<Action> ExitHandlers { get; } = [];

    public void HoldShutdown() => Calls.Add("hold");

    public void InitializeAppearance() => Calls.Add("appearance");

    public bool ShowSetup(SetupWizardViewModel vm)
    {
        Calls.Add("setup");
        ShownSetup = vm;
        return SetupCompleted;
    }

    /// <summary>Co uživatel v okně udělá, než ho zavře nebo se přihlásí (např. <c>vm.Login(heslo)</c>).</summary>
    public Action<LoginViewModel> InDialog { get; set; } = _ => { };

    public LoginResult? ShowLogin(LoginViewModel vm, IWindowStateStore windowState)
    {
        Calls.Add("login dialog");
        ShownLogin = vm;
        ShownLoginWindowState = windowState;
        InDialog(vm);
        return DialogLogin();
    }

    public bool ShowChangePassword(IMLRest rest, string userName)
    {
        Calls.Add("password");
        PasswordChange = (rest, userName);
        return PasswordChanged;
    }

    /// <summary>Volba ve výběru jiné firmy; null = uživatel zrušil.</summary>
    public Func<IReadOnlyList<CompanyStruct>, CompanyStruct?> OtherCompany { get; set; } = _ => null;

    public CompanyStruct? PickOtherCompany(CompanyStruct refused, IReadOnlyList<CompanyStruct> offered)
    {
        Calls.Add($"pick company {refused.Id}");
        return OtherCompany(offered);
    }

    public void RenderNow() { }

    public bool HandOff(EdisonMainWindow shell)
    {
        Calls.Add("handoff");
        HandedOff = shell;
        return !ShutdownCalled;
    }

    public bool ShutdownRequested => ShutdownCalled;

    public void Shutdown()
    {
        Calls.Add("shutdown");
        ShutdownCalled = true;
    }

    public void OnExit(Action action)
    {
        Calls.Add("logout hook");
        ExitHandlers.Add(action);
    }

    public void Dispatch(Action action) => action();

    public void ReportFailure(Exception ex) => ReportedFailure = ex;

    public Exception? UnreachableFailure { get; private set; }

    public void ReportUnreachableServer(Exception failure, string server, int port)
    {
        Calls.Add($"unreachable {server}:{port}");
        UnreachableFailure = failure;
    }
}
