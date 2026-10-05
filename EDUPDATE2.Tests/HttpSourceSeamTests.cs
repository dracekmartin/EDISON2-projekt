using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace EDUPDATE2.Tests;

/// <summary>
/// Šev http zdroje: skutečný HttpClient enginu proti lokálnímu HTTP serveru (statusy, escapování
/// jmen, stream těla). Harness tu jen vypíná čekání na procesy, stahování neobchází.
/// </summary>
public class HttpSourceSeamTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly string _src;
    private readonly string _tgt;
    private readonly LocalHttpServer _server;

    public HttpSourceSeamTests()
    {
        _src = Path.Combine(_root, "src");
        _tgt = Path.Combine(_root, "tgt");
        Directory.CreateDirectory(_src);
        Directory.CreateDirectory(_tgt);
        File.WriteAllText(Path.Combine(_src, "VERZE.INF"), "LAST=260825.2\n");
        File.WriteAllText(Path.Combine(_tgt, "VERZE.INF"), "LAST=260825.1\n");
        _server = new LocalHttpServer(_src);
    }

    public void Dispose()
    {
        _server.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    private void WriteSrc(string relative, string content)
    {
        var path = Path.Combine(_src, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private void WriteManifest(params string[] relativePaths)
    {
        var records = relativePaths.Select(relative => new CrcRecord(
            Path.GetFileName(relative), Path.GetDirectoryName(relative) ?? "",
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(_src, relative)))))).ToArray();
        using var stream = File.Create(Path.Combine(_src, CrcFile.FileName));
        CrcFile.Write(stream, records);
    }

    private RecordingEngine Engine(int bodyIdleTimeoutMs = 60_000, CancellationToken cancellationToken = default) =>
        new(new UpdaterArgs(SourcePath: _server.BaseUrl, TargetPath: _tgt), new UpdateLog(_tgt),
            cancellationToken: cancellationToken)
        {
            BodyIdleTimeoutMs = bodyIdleTimeoutMs,
        };

    [Fact]
    public void Run_RealHttpServer_InstallsManifestFilesAndSkipsMissingConfig()
    {
        WriteSrc("A.dll", "newA");
        WriteSrc(Path.Combine("EDFiles", "Ucetni sestava.txt"), "sestava");
        WriteManifest("A.dll", Path.Combine("EDFiles", "Ucetni sestava.txt"));

        var result = Engine().Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal("newA", File.ReadAllText(Path.Combine(_tgt, "A.dll")));
        Assert.Equal("sestava", File.ReadAllText(Path.Combine(_tgt, "EDFiles", "Ucetni sestava.txt")));
        Assert.Equal("LAST=260825.2\n", File.ReadAllText(Path.Combine(_tgt, "VERZE.INF")));
        Assert.False(File.Exists(Path.Combine(_tgt, "EDISONini.xml"))); // 404 na zdroji = přeskočeno
    }

    [Fact]
    public void Run_RealHttpServerError_Exit1_TargetUntouched()
    {
        WriteSrc("A.dll", "newA");
        WriteManifest("A.dll");
        _server.StatusFor["A.dll"] = HttpStatusCode.InternalServerError;

        var result = Engine().Run();

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
        Assert.False(File.Exists(Path.Combine(_tgt, "A.dll")));
        Assert.Equal("LAST=260825.1\n", File.ReadAllText(Path.Combine(_tgt, "VERZE.INF")));
    }

    [Fact]
    public void Run_BodyStalls_IdleTimeoutRetriedThenExit1()
    {
        WriteSrc("A.dll", new string('x', 10_000));
        WriteManifest("A.dll");
        _server.StallAfterBytes["A.dll"] = 1024;

        var result = RunWithin(Engine(bodyIdleTimeoutMs: 300));

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
        Assert.Contains("Stažení souborů selhalo", result.Message);
        Assert.False(File.Exists(Path.Combine(_tgt, "A.dll")));
    }

    [Fact]
    public void Run_BodyStalls_WindowClosed_AbortsWithoutWaitingForTimeout()
    {
        // Zavření okna musí ukončit i čtení zaseknutého těla - jinak updater visí po skončení shellu navždy.
        WriteSrc("A.dll", new string('x', 10_000));
        WriteManifest("A.dll");
        using var cts = new CancellationTokenSource();
        _server.StallAfterBytes["A.dll"] = 1024;
        _server.Stalled = () => cts.CancelAfter(300);

        var result = RunWithin(Engine(cancellationToken: cts.Token));

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
        Assert.Contains("přerušena", result.Message);
    }

    // Zaseknutý engine má test shodit, ne zablokovat celý běh (server stall uvolní až Dispose).
    private static UpdateResult RunWithin(UpdateEngine engine)
    {
        var run = Task.Run(engine.Run);
        Assert.True(run.Wait(TimeSpan.FromSeconds(20)), "Engine po 20 s pořád stahuje zaseknuté tělo.");
        return run.Result;
    }

    /// <summary>Minimální HTTP/1.1 server nad adresářem (GET, Connection: close) - bez http.sys a admin práv.</summary>
    private sealed class LocalHttpServer : IDisposable
    {
        private const string Prefix = "/edison/";
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly string _root;

        /// <summary>Vynucený status podle relativní cesty (místo obsahu souboru).</summary>
        public readonly Dictionary<string, HttpStatusCode> StatusFor = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>Pošle hlavičky s plnou délkou a jen prvních N bajtů těla, pak spojení drží bez dat.</summary>
        public readonly Dictionary<string, int> StallAfterBytes = new(StringComparer.OrdinalIgnoreCase);
        public Action? Stalled;
        public string BaseUrl { get; }

        public LocalHttpServer(string root)
        {
            _root = root;
            _listener.Start();
            BaseUrl = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}{Prefix.TrimEnd('/')}";
            _ = Task.Run(AcceptLoopAsync);
        }

        private async Task AcceptLoopAsync()
        {
            while (true)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(_stop.Token);
                }
                catch (Exception e) when (e is OperationCanceledException or SocketException or ObjectDisposedException)
                {
                    return;
                }
                _ = Task.Run(() => Serve(client));
            }
        }

        private void Serve(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            {
                using var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
                var requestLine = reader.ReadLine() ?? "";
                while (!string.IsNullOrEmpty(reader.ReadLine())) { }

                var target = requestLine.Split(' ') is [_, var path, ..] ? Uri.UnescapeDataString(path) : "";
                var relative = target.StartsWith(Prefix, StringComparison.Ordinal) ? target[Prefix.Length..] : "";
                var file = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));

                var status = StatusFor.TryGetValue(relative, out var forced) ? forced
                    : relative != "" && File.Exists(file) ? HttpStatusCode.OK
                    : HttpStatusCode.NotFound;
                byte[] body = status == HttpStatusCode.OK ? File.ReadAllBytes(file) : [];
                var header = $"HTTP/1.1 {(int)status} {status}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n";
                stream.Write(Encoding.ASCII.GetBytes(header));
                if (StallAfterBytes.TryGetValue(relative, out int sent))
                {
                    stream.Write(body, 0, Math.Min(sent, body.Length));
                    stream.Flush();
                    Stalled?.Invoke();
                    _stop.Token.WaitHandle.WaitOne();
                    return;
                }
                stream.Write(body);
            }
        }

        public void Dispose()
        {
            _stop.Cancel();
            _listener.Stop();
        }
    }
}
