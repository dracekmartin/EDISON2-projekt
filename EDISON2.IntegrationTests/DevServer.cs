using System.Net.Sockets;
using ALWA.SYS;
using EDControls;

namespace EDISON2.IntegrationTests;

/// <summary>
/// Připojení ke sdílenému dev serveru přes skutečné MLRest - stejný wire jako shell.
/// Co se na serveru volat nesmí, drží <c>CLAUDE.md</c> projektu.
/// </summary>
internal static class DevServer
{
    public static readonly string Host =
        Environment.GetEnvironmentVariable("EDISON_IT_SERVER") is { Length: > 0 } host ? host : "192.168.16.1";

    public static readonly int Port =
        int.TryParse(Environment.GetEnvironmentVariable("EDISON_IT_PORT"), out var port) ? port : 57770;

    /// <summary><c>EDISON_IT_REQUIRED=1</c>: nedostupný server je chyba, ne Skipped.</summary>
    public static bool Required => Environment.GetEnvironmentVariable("EDISON_IT_REQUIRED") == "1";

    // Čtení: firma se skutečnými daty, identita shodná s MLTestCase.SetupSys.
    public const string ReadIdfa = "CB_DP";
    public const string ReadMlp = "1";

    // Zápisy jen pod vymyšlenou firmou a uživatelem, ať test nepřepíše nic sdíleného.
    public const string WriteIdfa = "TEST_EDISON2_IT";
    public const string WriteMlp = "999";

    public const string LoginName = "test";

    // MLRest má timeout 8 h - bez předběžné kontroly by každý test bez VPN visel na TCP timeoutu OS.
    private static readonly Lazy<bool> Reachable = new(Probe);

    public static bool IsReachable => Reachable.Value;

    public static string Unreachable => $"Dev server {Host}:{Port} není dostupný (VPN?).";

    public static IMLRest Connect(string idfa = ReadIdfa, string mlp = ReadMlp)
    {
        if (!IsReachable)
            throw new InvalidOperationException($"{Unreachable} EDISON_IT_REQUIRED=1, test se nepřeskakuje.");

        // false = bez GetConfig: vadný MLRestConfig-data.xml by ukázal WinForms MessageBox a běh by visel.
        // CacheLocks zůstává 0 - s 1 server spouští version gate a kontrolu ACTIVITY zámku.
        return new MLRestAdapter(new MLRest(Host, Port, false)
        {
            Idfa = idfa,
            Mlp = mlp,
            LoginName = LoginName,
            FullName = "EDISON2 IntegrationTests",
        });
    }

    private static bool Probe()
    {
        try
        {
            using var client = new TcpClient();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            client.ConnectAsync(Host, Port, timeout.Token).AsTask().GetAwaiter().GetResult();
            return true;
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            return false;
        }
    }
}
