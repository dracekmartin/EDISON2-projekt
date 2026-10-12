using System.Net.Sockets;
using ALWA.SYS;
using EDControls;

namespace EDTesting;

/// <summary>
/// Sdílený dev server pro testy proti živé Caché: adresa, sonda dostupnosti a spojení skutečným
/// MLRest. Co se na serveru volat nesmí, drží <c>EDISON2.IntegrationTests/CLAUDE.md</c>.
/// </summary>
public static class DevServerConnection
{
    public static readonly string Host =
        Environment.GetEnvironmentVariable("EDISON_IT_SERVER") is { Length: > 0 } host ? host : "192.168.16.1";

    public static readonly int Port =
        int.TryParse(Environment.GetEnvironmentVariable("EDISON_IT_PORT"), out var port) ? port : 57770;

    // MLRest má timeout 8 h - bez předběžné kontroly by každý test bez VPN visel na TCP timeoutu OS.
    private static readonly Lazy<bool> Reachable = new(Probe);

    public static bool IsReachable => Reachable.Value;

    public static string Unreachable => $"Dev server {Host}:{Port} není dostupný (VPN?).";

    // Čtení: firma se skutečnými daty, identita shodná s MLTestCase.SetupSys.
    public const string ReadIdfa = "CB_DP";
    public const string ReadMlp = "1";
    public const string LoginName = "test";

    /// <param name="dataNamespace">Datový namespace firmy jako v shellu po výběru firmy, null = výchozí MLRest.</param>
    public static IMLRest Connect(string idfa = ReadIdfa, string mlp = ReadMlp, string loginName = LoginName,
        string? dataNamespace = null)
    {
        // false = bez GetConfig: vadný MLRestConfig-data.xml by ukázal WinForms MessageBox a běh by visel.
        // CacheLocks zůstává 0 - s 1 server spouští version gate a kontrolu ACTIVITY zámku.
        var rest = new MLRest(Host, Port, false)
        {
            Idfa = idfa,
            Mlp = mlp,
            LoginName = loginName,
            FullName = "EDISON2 IntegrationTests",
        };
        if (dataNamespace is not null) rest.DataNamespace = dataNamespace;
        return new MLRestAdapter(rest);
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
