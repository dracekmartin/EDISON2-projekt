namespace EDUpdateContract;

/// <summary>Updater, kterým shell aktualizuje stanici; aktivní volí <c>EDISONMAIN2.Updater.ActiveKind</c>.</summary>
public enum ClientUpdaterKind
{
    /// <summary>Legacy EDUPDATE (repo EDISON) spuštěný jako ze starého shellu, stopa vlny v CliUp.ini.</summary>
    LegacyEdupdate,
    /// <summary>EDUPDATE2, kontrakt v EDUPDATE2/CLAUDE.md, stopa vlny v CliUp2.ini.</summary>
    Edupdate2,
}

/// <summary>
/// Spuštění klientského updateru: exe, soubor stopy vynucené vlny a argumenty obou variant.
/// Argumenty legacy EDUPDATE a formát CliUp.ini určuje repo EDISON (UPDATERCL).
/// </summary>
public static class UpdaterContract
{
    /// <summary>Restart přes EDLOADER: o shellu po updatu rozhodne přepínač nového shellu, ne updater.</summary>
    public const string RestartExe = "EDISON.exe";
    public const string ShellProcessName = "EDISONMAIN2";

    public static string ExeFor(ClientUpdaterKind kind) => kind switch
    {
        ClientUpdaterKind.LegacyEdupdate => "EDUPDATE.exe",
        ClientUpdaterKind.Edupdate2 => "EDUPDATE2.exe",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static string StampFileFor(ClientUpdaterKind kind) => kind switch
    {
        ClientUpdaterKind.LegacyEdupdate => ForcedStamp.LegacyFileName,
        ClientUpdaterKind.Edupdate2 => ForcedStamp.FileName,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>Zdroj stanic jako URL (legacy <c>clsUpdate.PathIsUri</c>) - oba updatery soubory stahují.</summary>
    public static bool IsUri(string path) =>
        path.StartsWith("http:", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("https:", StringComparison.OrdinalIgnoreCase);

    /// <summary>URL souboru ve zdroji http; zpětná lomítka (cesta zapsaná jako na share) se převedou na '/'.</summary>
    public static string FileUrl(string sourceDir, string fileName, string subPath = "")
    {
        var url = sourceDir.Replace('\\', '/').TrimEnd('/');
        if (subPath != "") url += "/" + subPath.Replace('\\', '/').Trim('/');
        return url + "/" + fileName;
    }

    /// <summary>Tokeny <c>Key=value</c> pro daný updater; quoting (mezery v cestách) řeší ArgumentList.</summary>
    public static IReadOnlyList<string> BuildArguments(ClientUpdaterKind kind, string sourceDir, string targetPath,
        string version, bool forced, string forcedStamp) => kind switch
    {
        // Sada legacy shellu (EDISONMAIN clsUpdate.vb) + WaitProcess/RunExe: bez FormMode=1 legacy
        // čeká na klik OK, bez RunEdison=1 po updatu nic nespustí.
        ClientUpdaterKind.LegacyEdupdate =>
        [
            "FormMode=1",
            "Async=1",
            "RunEdison=1",
            $"SourcePath={sourceDir}",
            $"TargetPath={targetPath}",
            $"Version={version}",
            $"NucenyUpdate={(forced ? "True" : "False")}",
            $"NucenyUpdateTime={forcedStamp}",
            $"WaitProcess={ShellProcessName}",
            $"RunExe={RestartExe}",
        ],
        ClientUpdaterKind.Edupdate2 => new UpdaterArgs(sourceDir, targetPath, version, ShellProcessName, RestartExe,
            forced, forcedStamp).ToTokens(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
