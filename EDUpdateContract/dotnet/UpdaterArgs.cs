using System.IO;

namespace EDUpdateContract;

/// <summary>
/// Argumenty EDUPDATE2 - tokeny <c>Key=value</c>, quoting řeší ArgumentList volajícího.
/// Kontrakt v <c>EDUPDATE2/CLAUDE.md</c>; shell je skládá přes <see cref="UpdaterContract.BuildArguments"/>.
/// </summary>
public record UpdaterArgs(
    string SourcePath = "",
    string TargetPath = "",
    string Version = "",
    string WaitProcess = UpdaterContract.ShellProcessName,
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
                case nameof(SourcePath): args = args with { SourcePath = value }; break;
                case nameof(TargetPath): args = args with { TargetPath = value }; break;
                case nameof(Version): args = args with { Version = value }; break;
                case nameof(WaitProcess): args = args with { WaitProcess = value }; break;
                case nameof(RunExe): args = args with { RunExe = value }; break;
                case nameof(NucenyUpdate): args = args with { NucenyUpdate = value == "1" }; break;
                case nameof(NucenyUpdateTime): args = args with { NucenyUpdateTime = value }; break;
                default: onUnknown?.Invoke(token); break;
            }
        }
        if (args.TargetPath == "")
            args = args with { TargetPath = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar) };
        return args;
    }

    /// <summary>Opak <see cref="Parse"/>: tokeny pro ArgumentList volajícího.</summary>
    public IReadOnlyList<string> ToTokens() =>
    [
        $"{nameof(SourcePath)}={SourcePath}",
        $"{nameof(TargetPath)}={TargetPath}",
        $"{nameof(Version)}={Version}",
        $"{nameof(WaitProcess)}={WaitProcess}",
        $"{nameof(RunExe)}={RunExe}",
        $"{nameof(NucenyUpdate)}={(NucenyUpdate ? "1" : "0")}",
        $"{nameof(NucenyUpdateTime)}={NucenyUpdateTime}",
    ];
}
