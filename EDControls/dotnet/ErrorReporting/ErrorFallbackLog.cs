using System.IO;

namespace EDControls.ErrorReporting;

/// <summary>
/// Lokální záchyt hlášení, která se nepodařilo odeslat na ingest (offline, VPN, 500).
/// NDJSON bez příloh v <c>%APPDATA%\m-line\common\error-reports.log</c> - nic se zpětně
/// neodesílá, podpora si soubor vyžádá ručně. Best-effort: nikdy nehází.
/// </summary>
internal sealed class ErrorFallbackLog
{
    internal const long DefaultMaxBytes = 1_000_000;

    // Soubor sdílí všechna okna procesu; zámek přes instance, kdyby jich na stejnou cestu vzniklo víc.
    private static readonly object Sync = new();

    private readonly Func<string> _path;
    private readonly long _maxBytes;

    /// <param name="path">Cesta se vyhodnotí až při zápisu, uvnitř ochrany - výchozí instance vzniká ve statickém inicializátoru.</param>
    internal ErrorFallbackLog(Func<string> path, long maxBytes = DefaultMaxBytes)
    {
        _path = path;
        _maxBytes = maxBytes;
    }

    internal ErrorFallbackLog(string path, long maxBytes = DefaultMaxBytes) : this(() => path, maxBytes) { }

    internal void Append(IReadOnlyList<ErrorReport> reports)
    {
        try
        {
            lock (Sync)
            {
                var path = _path();
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                Rotate(path);
                using var writer = File.AppendText(path);
                foreach (var report in reports)
                {
                    var json = report.ToJson();
                    // Base64 snímky oken by log nafoukly o stovky KB na záznam.
                    json.Remove("attachments");
                    json["loggedAt"] = DateTime.UtcNow.ToString("o");
                    writer.WriteLine(json.ToJsonString());
                }
            }
        }
        catch
        {
            // Záložní log nesmí být třetí chyba nad neodeslaným hlášením.
        }
    }

    /// <summary>Nad práh → přetočit do `.old` (přepíše starší). Drží historii mezi 1× a 2× prahu.</summary>
    private void Rotate(string path)
    {
        var info = new FileInfo(path);
        if (info.Exists && info.Length > _maxBytes)
            File.Move(path, path + ".old", overwrite: true);
    }
}
