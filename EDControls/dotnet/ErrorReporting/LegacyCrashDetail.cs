using System.IO;

namespace EDControls.ErrorReporting;

/// <summary>
/// Výjimka, kterou host legacy modulu (EDISONMAIN2API i EDISONMAIN v args módu) zapíše před pádem
/// do <c>%TEMP%\m-line\legacy-crash\&lt;pid&gt;.txt</c>. Shell z pádu jinak zná jen exit kód.
/// Cestu zrcadlí <c>HostedErrorReporter.WriteCrashDetail</c> v repu EDISON, měnit jen spolu.
/// </summary>
internal static class LegacyCrashDetail
{
    internal static string PathFor(int pid) =>
        Path.Combine(Path.GetTempPath(), "m-line", "legacy-crash", $"{pid}.txt");

    /// <summary>
    /// Přečte a smaže detail. Soubor zapsaný před <paramref name="startedUtc"/> zbyl po dřívějším procesu
    /// se stejným pid (pád, který shell nepřečetl), proto se jen smaže. Nikdy nehází.
    /// </summary>
    internal static string? Take(int pid, DateTime startedUtc)
    {
        string? text;
        string path;
        try
        {
            path = PathFor(pid);
            if (!File.Exists(path)) return null;
            text = File.GetLastWriteTimeUtc(path) >= startedUtc ? File.ReadAllText(path) : null;
        }
        catch
        {
            return null;
        }

        // Soubor může držet antivir; přečtený detail se kvůli tomu zahodit nesmí.
        try { File.Delete(path); }
        catch { /* zbytek odfiltruje příští Take podle času startu */ }
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
