using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace EDISONMAIN2;

internal enum UpdaterRunOutcome { Unknown, Succeeded, NotNeeded, Failed }

/// <summary>Poslední běh legacy updateru podle jeho logu.</summary>
internal sealed record UpdaterRun(
    UpdaterRunOutcome Outcome,
    string RunKey,
    DateTime? StartedAt,
    DateTime? FinishedAt,
    string FailureMessage,
    bool? Restored,
    int? ExitCode,
    string SourcePath,
    string TargetPath,
    string Version,
    bool? Forced,
    IReadOnlyList<string> Tail);

/// <summary>
/// Čte <c>_LOG_UPDATER.txt</c> legacy EDUPDATE (repo EDISON, <c>UPDATERCL/clsAPI.vb</c>). Řádek
/// <c>[dd.MM.yyyy HH:mm:ss] akce: obsah</c>, běh začíná <c>Parametr: UpdateType=</c>. Build od 10/2026 končí
/// běh řádkem <c>UPDATER: Konec [ExitCode=N]</c>, starší jen výsledkem (úspěch, nic k aktualizaci, chyba).
/// Vzorky obou z reálných běhů jsou v <c>EDISONMAIN2.Tests/Fixtures/updater-logs</c>.
/// </summary>
internal static class UpdaterLog
{
    internal const string FileName = "_LOG_UPDATER.txt";
    internal const string PreviousFileName = "_LOG_UPDATER_bkp.txt";
    internal const string LegacyParametersFailed = "Kontrola parametrů neprošla, starší updater důvod do logu nezapisuje.";

    private static readonly Regex Record = new(@"^\[(?<ts>[^\]]+)\] (?<action>[^:]+): (?<content>.*)$", RegexOptions.Compiled);
    private static readonly Regex ExitCodeLine = new(@"^Konec \[ExitCode=(?<code>-?\d+)\]", RegexOptions.Compiled);
    // Oddělovač času bere legacy z kultury stanice.
    private static readonly string[] TimestampFormats = ["dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy HH.mm.ss", "d.M.yyyy H:mm:ss"];

    private sealed record Entry(string Timestamp, string Action, string Content, List<string> Lines);

    public static UpdaterRun Parse(string text, int tailLines = 50)
    {
        var entries = new List<Entry>();
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.Length == 0) continue;
            var match = Record.Match(line);
            if (match.Success)
                entries.Add(new Entry(match.Groups["ts"].Value, match.Groups["action"].Value, match.Groups["content"].Value, [line]));
            else if (entries.Count > 0)
            {
                // Starší build zapsal víceřádkovou zprávu, pokračování patří k předchozímu záznamu.
                var last = entries[^1];
                last.Lines.Add(line);
                entries[^1] = last with { Content = last.Content + " " + line.Trim() };
            }
        }

        var start = entries.FindLastIndex(e => e.Action == "Parametr" && e.Content.StartsWith("UpdateType=", StringComparison.Ordinal));
        var run = start >= 0 ? entries.Skip(start).ToList() : entries;

        var outcome = UpdaterRunOutcome.Unknown;
        Entry? finished = null;
        string failure = "";
        bool? restored = null;
        int? exitCode = null;
        bool notNeeded = false;
        foreach (var e in run)
        {
            if (e.Action == "RestoreFiles")
                restored = e.Content == "OK" ? true : e.Content.StartsWith("Chyba", StringComparison.Ordinal) ? false : restored;
            else if (e.Action == "KontrolaParametru" && e.Content.StartsWith("Chyba [", StringComparison.Ordinal))
                failure = Bracketed(e.Content);
            if (e.Action != "UPDATER") continue;

            if (ExitCodeLine.Match(e.Content) is { Success: true } end)
            {
                exitCode = int.Parse(end.Groups["code"].Value, CultureInfo.InvariantCulture);
                finished = e;
            }
            else if (e.Content.StartsWith("Aktualizace proběhla úspěšně", StringComparison.Ordinal))
            {
                outcome = UpdaterRunOutcome.Succeeded;
                finished ??= e;
            }
            else if (e.Content.StartsWith("Aktualizaci nebylo nutné provést", StringComparison.Ordinal))
            {
                notNeeded = true;
                outcome = UpdaterRunOutcome.NotNeeded;
                finished ??= e;
            }
            else if (e.Content.StartsWith("Chyba", StringComparison.Ordinal))
            {
                outcome = UpdaterRunOutcome.Failed;
                failure = Bracketed(e.Content);
                finished ??= e;
            }
        }

        if (exitCode is { } code)
            outcome = code != 0 ? UpdaterRunOutcome.Failed : notNeeded ? UpdaterRunOutcome.NotNeeded : UpdaterRunOutcome.Succeeded;
        else if (outcome == UpdaterRunOutcome.Failed)
            exitCode = restored switch { true => 100, false => 200, null => 1 };
        else if (start >= 0 && run.All(e => e.Action == "Parametr"))
        {
            // Starší build chybu kontroly parametrů nezapíše (nedostupný zdroj, právo zápisu), běh skončí za parametry.
            outcome = UpdaterRunOutcome.Failed;
            failure = LegacyParametersFailed;
            exitCode = 1;
        }

        string Param(string name) => run
            .Where(e => e.Action == "Parametr" && e.Content.StartsWith(name + "=", StringComparison.Ordinal))
            .Select(e => e.Content[(name.Length + 1)..]).LastOrDefault() ?? "";
        var forced = Param("Nucenyupdate");

        var first = run.FirstOrDefault();
        return new UpdaterRun(
            outcome,
            RunKey: first?.Timestamp ?? finished?.Timestamp ?? "",
            StartedAt: ParseTimestamp(first?.Timestamp),
            FinishedAt: ParseTimestamp(finished?.Timestamp),
            FailureMessage: failure,
            Restored: restored,
            ExitCode: exitCode,
            SourcePath: Param("SourcePath"),
            TargetPath: Param("TargetPath"),
            Version: Param("Version"),
            Forced: forced.Length == 0 ? null : string.Equals(forced, "True", StringComparison.OrdinalIgnoreCase),
            Tail: run.SelectMany(e => e.Lines).TakeLast(tailLines).ToList());
    }

    // Důvod je v hranatých závorkách a sám může závorky obsahovat (cesty, zámky).
    private static string Bracketed(string content)
    {
        var open = content.IndexOf('[');
        var close = content.LastIndexOf(']');
        return open >= 0 && close > open ? content[(open + 1)..close] : content;
    }

    private static DateTime? ParseTimestamp(string? text) =>
        text is not null && DateTime.TryParseExact(text, TimestampFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)
            ? at
            : null;
}
