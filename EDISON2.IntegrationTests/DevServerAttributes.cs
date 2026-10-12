using EDTesting;

namespace EDISON2.IntegrationTests;

/// <summary>Fact proti dev serveru: bez serveru Skipped, s <c>EDISON_IT_REQUIRED=1</c> běží a selže v <c>Connect</c>.</summary>
public sealed class DevServerFactAttribute : FactAttribute
{
    public DevServerFactAttribute()
    {
        if (!DevServer.Required && !DevServer.IsReachable) Skip = DevServer.Unreachable;
    }
}

/// <summary>Theory proti dev serveru, pravidla jako <see cref="DevServerFactAttribute"/>.</summary>
public sealed class DevServerTheoryAttribute : TheoryAttribute
{
    public DevServerTheoryAttribute()
    {
        if (!DevServer.Required && !DevServer.IsReachable) Skip = DevServer.Unreachable;
    }
}

/// <summary>
/// Fact s testovacím účtem (<see cref="DevServer.Account"/>): server jako <see cref="DevServerFactAttribute"/>,
/// chybějící účet = Skipped s návodem. Výjimku z načtení účtu nechá na testu, ať se neschová do Skip.
/// </summary>
public sealed class TestAccountFactAttribute : FactAttribute
{
    public static readonly string MissingAccount =
        "Testovací účet na DEV není nastavený: global ^EDTesting(\"ITAccount\",\"login\") a (\"password\") " +
        $"v namespace MLINE, nebo proměnné {TestAccount.LoginVariable} a {TestAccount.PasswordVariable}.";

    public TestAccountFactAttribute() =>
        Skip = SkipReason(DevServer.IsReachable, DevServer.Required, () => DevServer.Account);

    /// <summary>Důvod přeskočení, null = test běží.</summary>
    public static string? SkipReason(bool reachable, bool required, Func<TestAccount?> account)
    {
        if (!reachable) return required ? null : DevServer.Unreachable;

        // Volá se při discovery: výjimka tady by shodila celý dotnet test, ne jeden test.
        try
        {
            return account() is null ? MissingAccount : null;
        }
        catch
        {
            // Bez Skip: test výjimku vyhodí z DevServer.Account.
            return null;
        }
    }
}
