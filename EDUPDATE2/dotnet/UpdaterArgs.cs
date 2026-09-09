using System.IO;

namespace EDUPDATE2;

/// <summary>
/// Argumenty updateru - tokeny <c>Key=value</c>, quoting řeší ArgumentList volajícího.
/// Kontrakt v <c>EDUPDATE2/CLAUDE.md</c>; plní ho <c>EDISONMAIN2.Updater.BuildArguments</c>.
/// </summary>
public record UpdaterArgs(
    string SourcePath = "",
    string TargetPath = "",
    string Version = "",
    string WaitProcess = "EDISONMAIN2",
    string RunExe = "",
    bool NucenyUpdate = false,
    string NucenyUpdateTime = "")
{
    /// <summary>
    /// Token bez '=' nebo s neznámým klíčem se hlásí přes onUnknown a ignoruje (dopředná
    /// kompatibilita). Prázdný TargetPath se doplní adresářem exe - shell updater kopíruje
    /// k sobě do instalace, default se uplatní jen při ručním diagnostickém spuštění.
    /// </summary>
    public static UpdaterArgs Parse(IReadOnlyList<string> tokens, Action<string>? onUnknown = null)
    {
        var args = new UpdaterArgs();
        foreach (var token in tokens)
        {
            int eq = token.IndexOf('=');
            if (eq < 0)
            {
                onUnknown?.Invoke(token);
                continue;
            }
            string key = token[..eq];
            string value = token[(eq + 1)..];
            switch (key)
            {
                case "SourcePath": args = args with { SourcePath = value }; break;
                case "TargetPath": args = args with { TargetPath = value }; break;
                case "Version": args = args with { Version = value }; break;
                case "WaitProcess": args = args with { WaitProcess = value }; break;
                case "RunExe": args = args with { RunExe = value }; break;
                case "NucenyUpdate": args = args with { NucenyUpdate = value == "1" }; break;
                case "NucenyUpdateTime": args = args with { NucenyUpdateTime = value }; break;
                default: onUnknown?.Invoke(token); break;
            }
        }
        if (args.TargetPath == "")
            args = args with { TargetPath = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar) };
        return args;
    }
}
