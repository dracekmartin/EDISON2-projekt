using EDControls;
using EDControls.EDViewModel;
using EDISONMAIN2.Windows.LoginWindow;
using EDTesting;
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
        ml.Server.Returns(Dev.Server);
        ml.Port.Returns(Dev.Port);
        var outputs = responses.Select(r => new object[] { "", r }).ToArray();
        ml.QueryOrThrow("ALVA.SYS.Login", "Login", Arg.Any<object[]>()).Returns(outputs[0], outputs[1..]);
        return ml;
    }

    private static readonly (string Server, int Port) Dev = ("DEV-host", 57772), Cloud = ("cloud1-host", 57773);

    private static TransientLocalPrefs RememberedPrefs(string name, string password, (string Server, int Port)? server = null)
    {
        var (host, port) = server ?? Dev;
        var prefs = new TransientLocalPrefs();
        prefs.SetLoginName(host, port, name);
        prefs.SetRememberedPassword(host, port, PasswordProtector.Protect(name, password));
        return prefs;
    }

    private static string? DevPassword(ILocalPrefs prefs) => prefs.RememberedPasswordFor(Dev.Server, Dev.Port);

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
    public void ServerUnreachable_ShowsMessageKeepsRememberedPasswordAndReports()
    {
        // Výpadek spojení není odmítnuté heslo: zapamatované se nesmí zapomenout a globální dialog chyby nepřijde.
        var prefs = RememberedPrefs("jan.novak", "heslo");
        var remembered = DevPassword(prefs);
        var rest = Server(Accepted());
        rest.QueryOrThrow("ALVA.SYS.Login", "Login", Arg.Any<object[]>())
            .Returns(_ => throw new Exception(MLRestErrors.Transport()));
        var reported = new List<Exception>();
        var vm = LoginViewModel.ForStartup(Services(), rest, prefs, reported.Add);
        int succeeded = 0;
        vm.LoginSucceeded += (_, _) => succeeded++;

        Assert.Equal(LoginAttempt.Failed, vm.Login(vm.InitialPassword));
        vm.ApplyRememberedPassword();

        Assert.Equal(LoginViewModel.UnreachableMessage, vm.ErrorMessage);
        Assert.False(vm.RememberedPasswordRejected);
        Assert.Equal(remembered, DevPassword(prefs));
        Assert.Single(reported);
        Assert.True(vm.IsEditable);
        Assert.Equal(0, succeeded);
    }

    [Fact]
    public void ServerErrorOtherThanTransport_StillPropagates()
    {
        var rest = Server(Accepted());
        rest.QueryOrThrow("ALVA.SYS.Login", "Login", Arg.Any<object[]>())
            .Returns(_ => throw new Exception(MLRestErrors.ServerError("ERROR #5001: chyba v metodě")));
        var vm = LoginViewModel.ForStartup(Services(), rest, new TransientLocalPrefs(), _ => { });
        vm.UserName = "jan";

        Assert.Throws<Exception>(() => vm.Login("heslo"));
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
        Assert.Null(DevPassword(prefs));
    }

    [Fact]
    public void Login_LeavesPrefsUntilApplied()
    {
        // Geometrie login okna ukládá celé prefs ještě před SavePostLoginState; kdyby tam už byl blob
        // nového jména a start skončil (licence, změna hesla), přišel by předchozí uživatel o heslo.
        var prefs = RememberedPrefs("jan.novak", "heslo");
        var remembered = DevPassword(prefs);
        var vm = Startup(Server(Accepted()), prefs);
        vm.UserName = "petr";

        vm.Login("petrovo");

        Assert.Equal(remembered, DevPassword(prefs));
    }

    [Fact]
    public void TypedPasswordRejected_KeepsRememberedOne()
    {
        var prefs = RememberedPrefs("jan.novak", "stare");
        var remembered = DevPassword(prefs);
        var vm = Startup(Server("-107"), prefs);

        Assert.Equal(LoginAttempt.Failed, vm.Login("prepsane"));
        vm.ApplyRememberedPassword();

        Assert.False(vm.RememberedPasswordRejected);
        Assert.Equal(remembered, DevPassword(prefs));
    }

    [Fact]
    public void Success_RememberChecked_StoresPasswordUnderServerUserName()
    {
        // Předvyplnění čte blob pod uloženým jménem = jméno ze serveru, ne napsané.
        var prefs = new TransientLocalPrefs();
        var vm = Startup(Server(Accepted()), prefs);
        vm.UserName = "jan";
        vm.RememberPassword = true;

        vm.Login("heslo");
        vm.ApplyRememberedPassword();

        Assert.Equal("heslo", PasswordProtector.Unprotect(DevPassword(prefs), "jan.novak"));
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

        Assert.Null(DevPassword(prefs));
    }

    [Fact]
    public void Success_RememberUnchecked_ForgetsStoredPassword()
    {
        var prefs = RememberedPrefs("jan.novak", "heslo");
        var vm = Startup(Server(Accepted()), prefs);
        vm.RememberPassword = false;

        vm.Login(vm.InitialPassword);
        vm.ApplyRememberedPassword();

        Assert.Null(DevPassword(prefs));
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
        prefs.SetLoginName(Dev.Server, Dev.Port, "jan.novak");

        var vm = Startup(Server(Accepted()), prefs);

        Assert.Equal("", vm.InitialPassword);
        Assert.False(vm.RememberPassword);
        Assert.Equal(LoginFocus.Password, vm.InitialFocus);
    }

    [Fact]
    public void ForStartup_LoginOfOtherServer_IsNotPrefilled()
    {
        // Dvě instalace na stanici: přihlášení na CLOUD1 se nesmí nabízet na DEV.
        var vm = Startup(Server(Accepted()), RememberedPrefs("jan.novak", "heslo", Cloud));

        Assert.Equal("", vm.UserName);
        Assert.Equal("", vm.InitialPassword);
        Assert.False(vm.RememberPassword);
    }

    [Fact]
    public void ForStartup_ServerKeyIgnoresCase()
    {
        var vm = Startup(Server(Accepted()), RememberedPrefs("jan.novak", "heslo", (Dev.Server.ToLowerInvariant(), Dev.Port)));

        Assert.Equal("jan.novak", vm.UserName);
        Assert.Equal("heslo", vm.InitialPassword);
    }

    [Fact]
    public void Success_RememberChecked_LeavesOtherServerLogin()
    {
        var prefs = RememberedPrefs("petr", "cloudove", Cloud);
        var vm = Startup(Server(Accepted()), prefs);
        vm.UserName = "jan";
        vm.RememberPassword = true;

        vm.Login("heslo");
        vm.ApplyRememberedPassword();

        Assert.Equal("heslo", PasswordProtector.Unprotect(DevPassword(prefs), "jan.novak"));
        Assert.Equal("cloudove", PasswordProtector.Unprotect(prefs.RememberedPasswordFor(Cloud.Server, Cloud.Port), "petr"));
    }

#pragma warning disable CS0618 // převzetí přihlášení starší verze
    private static TransientLocalPrefs LegacyPrefs(string name, string password) =>
        new() { LastLoginName = name, RememberedPassword = PasswordProtector.Protect(name, password) };

    [Fact]
    public void ForStartup_ServerWithoutOwnLogin_AdoptsLegacyLogin()
    {
        // Po aktualizaci uživatel jméno a heslo znovu nezadává.
        var prefs = LegacyPrefs("jan.novak", "heslo");

        var vm = Startup(Server(Accepted()), prefs);

        Assert.Equal("jan.novak", vm.UserName);
        Assert.Equal("heslo", vm.InitialPassword);
        Assert.Equal("jan.novak", prefs.LoginNameFor(Dev.Server, Dev.Port));
    }

    [Fact]
    public void ForStartup_ServerWithOwnLogin_IgnoresLegacyLogin()
    {
        // Na tomhle serveru už se přihlásil petr bez zapamatování - staré heslo se vracet nesmí.
        var prefs = LegacyPrefs("jan.novak", "heslo");
        prefs.SetLoginName(Dev.Server, Dev.Port, "petr");

        var vm = Startup(Server(Accepted()), prefs);

        Assert.Equal("petr", vm.UserName);
        Assert.Equal("", vm.InitialPassword);
    }

    [Fact]
    public void ForStartup_OtherServerHasOwnLogin_DoesNotAdoptLegacyLogin()
    {
        // Převzetí jen jednou na stanici: jinak by „Připojit k databázi" vrátilo heslo, které uživatel zapomněl.
        var prefs = LegacyPrefs("jan.novak", "heslo");
        prefs.SetLoginName(Cloud.Server, Cloud.Port, "jan.novak");

        var vm = Startup(Server(Accepted()), prefs);

        Assert.Equal("", vm.UserName);
        Assert.Equal("", vm.InitialPassword);
    }

    [Fact]
    public void LegacyPasswordRejected_IsNotOfferedAgainOnThatServer()
    {
        var prefs = LegacyPrefs("jan.novak", "stare");
        var vm = Startup(Server("-107"), prefs);
        vm.Login(vm.InitialPassword);
        vm.ApplyRememberedPassword();

        var next = Startup(Server(Accepted()), prefs);

        Assert.Equal("jan.novak", next.UserName);
        Assert.Equal("", next.InitialPassword);
        // Staré klíče zůstávají jiným serverům a starší verzi shellu.
        Assert.NotNull(prefs.RememberedPassword);
    }
#pragma warning restore CS0618

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
