using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.Windows.LoginWindow;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Login workflow bez WPF: ověření, politika zapamatovaného hesla a progress fáze startupu.
/// Odpověď <c>ALVA.SYS.Login.Login</c> v pořadí serveru (viz <see cref="UserAuthenticatorTests"/>).
/// </summary>
public class LoginViewModelTests
{
    private const char D = '~';

    private static EDServices Services() =>
        new(null, Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>());

    private static string Accepted(bool changePassword = false) =>
        string.Join(D, "201", "Jan Novák", "1", "C:\\dir", changePassword ? "1" : "0", "1", "x", "jan@x.cz", "a", "b", "jan.novak");

    private static IMLRest Server(params string[] responses)
    {
        var ml = Substitute.For<IMLRest>();
        ml.Delimiter.Returns(D);
        ml.Idfa.Returns("CB_DP");
        var outputs = responses.Select(r => new object[] { "", r }).ToArray();
        ml.QueryOrThrow("ALVA.SYS.Login", "Login", Arg.Any<object[]>()).Returns(outputs[0], outputs[1..]);
        return ml;
    }

    private static TransientLocalPrefs RememberedPrefs(string name, string password) =>
        new() { LastLoginName = name, RememberedPassword = PasswordProtector.Protect(name, password) };

    private static LoginViewModel Startup(IMLRest rest, ILocalPrefs? prefs = null) =>
        LoginViewModel.ForStartup(Services(), rest, prefs ?? new TransientLocalPrefs());

    private static List<bool?> CloseRequests(LoginViewModel vm)
    {
        var requests = new List<bool?>();
        vm.RequestClose += requests.Add;
        return requests;
    }

    [Fact]
    public void Startup_Success_StaysOpenInProgressAndSignalsBootstrapper()
    {
        var vm = Startup(Server(Accepted()));
        var closes = CloseRequests(vm);
        int succeeded = 0;
        vm.LoginSucceeded += (_, _) => succeeded++;
        vm.UserName = "jan";

        Assert.Equal(LoginAttempt.Succeeded, vm.Login("heslo"));

        Assert.Equal("jan.novak", vm.Result?.UserName);
        Assert.Equal(1, succeeded);
        // Progress podklad pod dobíhajícím bootstrapem - uživatel ho nezavře ani nepřepíše.
        Assert.True(vm.IsInProgress);
        Assert.False(vm.IsEditable);
        Assert.Equal("Přihlašování…", vm.ProgressMessage);
        Assert.Empty(closes);
    }

    [Fact]
    public void Login_SendsTypedNameAndPassword()
    {
        var rest = Server(Accepted());
        var vm = Startup(rest);
        vm.UserName = "jan";

        vm.Login("heslo");

        rest.Received(1).QueryOrThrow("ALVA.SYS.Login", "Login",
            Arg.Is<object[]>(a => a.Length == 2 && (string)a[0] == "jan" && (string)a[1] == "heslo"));
    }

    [Fact]
    public void Reauth_Success_ClosesDialogConfirmed()
    {
        var vm = LoginViewModel.ForReauth(Services(), Server(Accepted()));
        var closes = CloseRequests(vm);
        vm.UserName = "jan";

        vm.Login("heslo");

        Assert.Equal([true], closes);
        Assert.False(vm.IsInProgress);
        Assert.NotNull(vm.Result);
    }

    [Fact]
    public void ServerRejects_ShowsMessageAndStaysEditable()
    {
        var vm = Startup(Server("-107"));
        int succeeded = 0;
        vm.LoginSucceeded += (_, _) => succeeded++;
        vm.UserName = "jan";

        Assert.Equal(LoginAttempt.Failed, vm.Login("spatne"));

        Assert.Equal("Špatné heslo.", vm.ErrorMessage);
        Assert.True(vm.HasError);
        Assert.Null(vm.Result);
        Assert.True(vm.IsEditable);
        Assert.Equal(0, succeeded);
    }

    [Fact]
    public void NextAttempt_ClearsPreviousError()
    {
        var vm = Startup(Server("-107", Accepted()));
        vm.UserName = "jan";
        vm.Login("spatne");

        vm.Login("heslo");

        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.HasError);
    }

    [Fact]
    public void RememberedPasswordRejected_ForgetsItInPrefs()
    {
        // Heslo změněné jinde: předvyplněné neplatí, příště se vyplnit nesmí.
        var prefs = RememberedPrefs("jan.novak", "stare");
        var vm = Startup(Server("-107"), prefs);

        Assert.Equal(LoginAttempt.RememberedPasswordRejected, vm.Login(vm.InitialPassword));
        vm.ApplyRememberedPassword();

        Assert.True(vm.RememberedPasswordRejected);
        Assert.Null(prefs.RememberedPassword);
    }

    [Fact]
    public void Login_LeavesPrefsUntilApplied()
    {
        // Geometrie login okna ukládá celé prefs ještě před SavePostLoginState; kdyby tam už byl blob
        // nového jména a start skončil (licence, změna hesla), přišel by předchozí uživatel o heslo.
        var prefs = RememberedPrefs("jan.novak", "heslo");
        var remembered = prefs.RememberedPassword;
        var vm = Startup(Server(Accepted()), prefs);
        vm.UserName = "petr";

        vm.Login("petrovo");

        Assert.Equal(remembered, prefs.RememberedPassword);
    }

    [Fact]
    public void TypedPasswordRejected_KeepsRememberedOne()
    {
        var prefs = RememberedPrefs("jan.novak", "stare");
        var remembered = prefs.RememberedPassword;
        var vm = Startup(Server("-107"), prefs);

        Assert.Equal(LoginAttempt.Failed, vm.Login("prepsane"));
        vm.ApplyRememberedPassword();

        Assert.False(vm.RememberedPasswordRejected);
        Assert.Equal(remembered, prefs.RememberedPassword);
    }

    [Fact]
    public void Success_RememberChecked_StoresPasswordUnderServerUserName()
    {
        // Předvyplnění čte blob pod LastLoginName = jméno ze serveru, ne napsané.
        var prefs = new TransientLocalPrefs();
        var vm = Startup(Server(Accepted()), prefs);
        vm.UserName = "jan";
        vm.RememberPassword = true;

        vm.Login("heslo");
        vm.ApplyRememberedPassword();

        Assert.Equal("heslo", PasswordProtector.Unprotect(prefs.RememberedPassword, "jan.novak"));
    }

    [Fact]
    public void Success_ChangePasswordNeeded_DoesNotRemember()
    {
        // Feature #17: heslo se za chvíli změní, zapamatované by bylo neplatné.
        var prefs = new TransientLocalPrefs();
        var vm = Startup(Server(Accepted(changePassword: true)), prefs);
        vm.UserName = "jan";
        vm.RememberPassword = true;

        vm.Login("heslo");
        vm.ApplyRememberedPassword();

        Assert.Null(prefs.RememberedPassword);
    }

    [Fact]
    public void Success_RememberUnchecked_ForgetsStoredPassword()
    {
        var prefs = RememberedPrefs("jan.novak", "heslo");
        var vm = Startup(Server(Accepted()), prefs);
        vm.RememberPassword = false;

        vm.Login(vm.InitialPassword);
        vm.ApplyRememberedPassword();

        Assert.Null(prefs.RememberedPassword);
    }

    [Fact]
    public void ForStartup_PrefillsRememberedLogin()
    {
        var vm = Startup(Server(Accepted()), RememberedPrefs("jan.novak", "heslo"));

        Assert.Equal("jan.novak", vm.UserName);
        Assert.Equal("heslo", vm.InitialPassword);
        Assert.True(vm.RememberPassword);
        Assert.True(vm.ShowRemember);
        // Předvyplněné heslo → fokus na jméno, ať přihlášení snadno převezme jiný uživatel.
        Assert.Equal(LoginFocus.Username, vm.InitialFocus);
    }

    [Fact]
    public void ForStartup_PasswordOfOtherUser_IsNotPrefilled()
    {
        var prefs = RememberedPrefs("petr", "heslo");
        prefs.LastLoginName = "jan.novak";

        var vm = Startup(Server(Accepted()), prefs);

        Assert.Equal("", vm.InitialPassword);
        Assert.False(vm.RememberPassword);
        Assert.Equal(LoginFocus.Password, vm.InitialFocus);
    }

    [Fact]
    public void ForReauth_StartsEmptyWithoutRemember()
    {
        var vm = LoginViewModel.ForReauth(Services(), Server(Accepted()));

        Assert.Equal("", vm.UserName);
        Assert.Equal("", vm.InitialPassword);
        Assert.False(vm.ShowRemember);
        Assert.Equal(LoginFocus.Default, vm.InitialFocus);
        Assert.Equal("CB_DP", vm.Idfa);
    }

    [Fact]
    public void Cancel_StartupBeforeLogin_ClosesWindow()
    {
        var vm = Startup(Server(Accepted()));
        var closes = CloseRequests(vm);

        vm.Cancel();

        Assert.Equal([null], closes);
    }

    [Fact]
    public void Cancel_InProgress_IsIgnored()
    {
        var vm = Startup(Server(Accepted()));
        vm.UserName = "jan";
        vm.Login("heslo");
        var closes = CloseRequests(vm);

        vm.Cancel();

        Assert.Empty(closes);
        Assert.True(vm.IsInProgress);
    }

    [Fact]
    public void Cancel_Reauth_LeavesClosingToModalDialog()
    {
        var vm = LoginViewModel.ForReauth(Services(), Server(Accepted()));
        var closes = CloseRequests(vm);

        vm.Cancel();

        Assert.Empty(closes);
    }

    [Fact]
    public void ShowProgress_UpdatesBoundText()
    {
        var vm = Startup(Server(Accepted()));
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.ShowProgress("Ověřuji licenci…");

        Assert.Equal("Ověřuji licenci…", vm.ProgressMessage);
        Assert.True(vm.HasProgress);
        Assert.Contains(nameof(LoginViewModel.ProgressMessage), changed);
        Assert.Contains(nameof(LoginViewModel.HasProgress), changed);
    }

    [Fact]
    public void CompleteHandoff_EndsProgressAndClosesWindow()
    {
        // Jediná cesta ven z progress fáze: zavírací veto okna čte IsInProgress.
        var vm = Startup(Server(Accepted()));
        vm.UserName = "jan";
        vm.Login("heslo");
        var closes = CloseRequests(vm);

        vm.CompleteHandoff();

        Assert.False(vm.IsInProgress);
        Assert.Equal([null], closes);
    }

    [Fact]
    public void ShowError_ShowsMessage()
    {
        var vm = Startup(Server(Accepted()));

        vm.ShowError("Nepodařilo se otevřít prohlížeč.");

        Assert.Equal("Nepodařilo se otevřít prohlížeč.", vm.ErrorMessage);
        Assert.True(vm.HasError);
    }
}
