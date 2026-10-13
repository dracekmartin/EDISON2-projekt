using EDTesting;

namespace EDISON2.E2ETests;

/// <summary>Fact, který převezme plochu: běží jen s <c>EDISON_E2E=1</c>, jinak Skipped.</summary>
public sealed class E2EFactAttribute : FactAttribute
{
    internal const string Disabled = "UI E2E test spouští shell na ploše - zapíná se EDISON_E2E=1.";

    public E2EFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("EDISON_E2E") != "1")
            Skip = Disabled;
    }
}

/// <summary>E2E, které se přihlašuje testovacím účtem DEV (<see cref="TestAccount"/>): bez účtu Skipped.</summary>
public sealed class E2EAccountFactAttribute : FactAttribute
{
    /// <summary>Jeden dotaz na DEV za běh - atribut se konstruuje při každé discovery. Výjimku Lazy drží taky.</summary>
    internal static readonly Lazy<TestAccount?> Account = new(TestAccount.TryGet);

    public E2EAccountFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("EDISON_E2E") != "1")
            Skip = E2EFactAttribute.Disabled;
        else if (!DevServerConnection.IsReachable)
            Skip = DevServerConnection.Unreachable;
        else
            Skip = MissingAccount();
    }

    // Volá se při discovery: výjimka by shodila celý běh, test ji pak dostane z Account sám.
    private static string? MissingAccount()
    {
        try
        {
            return Account.Value is null
                ? $"Testovací účet na DEV není nastavený (^EDTesting(\"ITAccount\") v MLINE, nebo {TestAccount.LoginVariable}/{TestAccount.PasswordVariable})."
                : null;
        }
        catch
        {
            return null;
        }
    }
}
