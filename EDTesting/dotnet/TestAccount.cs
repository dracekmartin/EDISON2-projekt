using ALWA.SYS;
using EDControls;

namespace EDTesting;

/// <summary>
/// Testovací účet DEV pro testy, které se musí skutečně přihlásit (<c>ALVA.SYS.Login.Login</c> s reálným
/// účtem resetuje uživatelské flagy). Jméno a heslo drží DEV v globálu, který čte <c>ALVA.EDTesting.ITAccount</c>,
/// proměnné <c>EDISON_IT_LOGIN</c> a <c>EDISON_IT_PASSWORD</c> ho přepíšou.
/// </summary>
public sealed class TestAccount
{
    public const string LoginVariable = "EDISON_IT_LOGIN";
    public const string PasswordVariable = "EDISON_IT_PASSWORD";

    private const string CacheClass = "ALVA.EDTesting.ITAccount";

    private TestAccount(string login, string password)
    {
        Login = login;
        Password = password;
    }

    public string Login { get; }

    public string Password { get; }

    // Heslo se nesmí dostat do výpisu testu ani do assert zprávy.
    public override string ToString() => $"testovací účet {Login}";

    /// <summary>
    /// Účet z proměnných, jinak z DEV. Null = server nedostupný nebo účet na něm není nastavený.
    /// Server, který přijme spojení a neodpoví, skončí po 10 s výjimkou <see cref="TimeoutException"/>.
    /// </summary>
    public static TestAccount? TryGet() =>
        TryGet(Environment.GetEnvironmentVariable,
            () => DevServerConnection.IsReachable ? DevServerConnection.Connect() : null,
            TimeSpan.FromSeconds(10));

    /// <param name="env">Čtení proměnné prostředí.</param>
    /// <param name="connect">Spojení s DEV, null = server nedostupný.</param>
    /// <param name="timeout">Nejdelší čekání na server.</param>
    public static TestAccount? TryGet(Func<string, string?> env, Func<IMLRest?> connect, TimeSpan timeout)
    {
        if (env(LoginVariable) is { Length: > 0 } login && env(PasswordVariable) is { Length: > 0 } password)
            return new TestAccount(login, password);
        if (connect() is not { } rest) return null;

        // MLRest čeká až 8 h: volá se i při discovery testů, zaseknutý server by zablokoval celý dotnet test.
        var request = Task.Run(() => FromServer(rest));
        if (Task.WhenAny(request, Task.Delay(timeout)).GetAwaiter().GetResult() != request)
            throw new TimeoutException($"{CacheClass}.Get neodpověděl do {timeout.TotalSeconds:0} s.");
        return request.GetAwaiter().GetResult();
    }

    /// <summary>Účet z globálu na serveru, prázdné jméno nebo heslo = null. Chyba Caché (třída nenasazená) projde výjimkou.</summary>
    public static TestAccount? FromServer(IMLRest rest)
    {
        CVar output = rest.QueryOrThrow(CacheClass, "Get", new CVar());
        var login = output["login"]?.Value?.ToString();
        var password = output["password"]?.Value?.ToString();
        return string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password) ? null : new TestAccount(login, password);
    }
}
