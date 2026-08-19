using System.IO;

namespace EDControls.ErrorReporting;

/// <summary>
/// Lokální záchyt hlášení, která se nepodařilo odeslat na ingest (offline, VPN, 500).
/// NDJSON bez příloh v <c>%APPDATA%\m-line\common\error-reports.log</c> - nic se zpětně
/// neodesílá, podpora si soubor vyžádá ručně. Best-effort: nikdy nehází.
/// </summary>
internal static class ErrorFallbackLog
{
    /// <summary>Práh rotace. Mutable jen kvůli testům (rotaci nelze testovat přes 1 MB zápisů).</summary>
    internal static long MaxBytes = 1_000_000;

    private static readonly object Sync = new();

    /// <summary>Přebití cílového souboru pro testy.</summary>
    internal static string? PathOverride { get; set; }

    private static string LogPath => PathOverride ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "m-line", "common", "error-reports.log");

    internal static void Append(IReadOnlyList<ErrorReport> reports)
    {
        try
        {
            lock (Sync)
            {
                var path = LogPath;
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
    private static void Rotate(string path)
    {
        var info = new FileInfo(path);
        if (info.Exists && info.Length > MaxBytes)
            File.Move(path, path + ".old", overwrite: true);
    }
}
