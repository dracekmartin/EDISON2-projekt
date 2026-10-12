using EDControls;
using EDTesting;

namespace EDISON2.IntegrationTests;

/// <summary>
/// Připojení ke sdílenému dev serveru přes skutečné MLRest - stejný wire jako shell.
/// Adresu a spojení drží <see cref="DevServerConnection"/> v EDTesting, co se na serveru volat nesmí, <c>CLAUDE.md</c> projektu.
/// </summary>
internal static class DevServer
{
    /// <summary><c>EDISON_IT_REQUIRED=1</c>: nedostupný server je chyba, ne Skipped.</summary>
    public static bool Required => Environment.GetEnvironmentVariable("EDISON_IT_REQUIRED") == "1";

    public const string ReadIdfa = DevServerConnection.ReadIdfa;
    public const string ReadMlp = DevServerConnection.ReadMlp;

    // Data agend (MHD, ...) leží v datovém namespace firmy, výchozí namespace MLRest je nemá.
    public const string ReadDataNamespace = "CB_DP";

    // Zápisy jen pod vymyšlenou firmou a uživatelem, ať test nepřepíše nic sdíleného.
    public const string WriteIdfa = "TEST_EDISON2_IT";
    public const string WriteMlp = "999";

    public const string LoginName = DevServerConnection.LoginName;

    public static bool IsReachable => DevServerConnection.IsReachable;

    public static string Unreachable => DevServerConnection.Unreachable;

    // Lazy drží i výjimku: chybu Caché při discovery (TestAccountFact) test vyhodí znovu, nepřeskočí se.
    private static readonly Lazy<TestAccount?> LazyAccount = new(TestAccount.TryGet);

    /// <summary>Testovací účet DEV (<see cref="TestAccount"/>), null = není nastavený nebo server nedostupný.</summary>
    public static TestAccount? Account => LazyAccount.Value;

    public static IMLRest Connect(string idfa = ReadIdfa, string mlp = ReadMlp, string? dataNamespace = null)
    {
        if (!IsReachable)
            throw new InvalidOperationException($"{Unreachable} EDISON_IT_REQUIRED=1, test se nepřeskakuje.");

        return DevServerConnection.Connect(idfa, mlp, dataNamespace: dataNamespace);
    }
}
