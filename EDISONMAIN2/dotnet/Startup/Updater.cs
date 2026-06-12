using System;
using System.Diagnostics;
using System.IO;
using EDControls;

namespace EDISONMAIN2;

/// <summary>
/// Subset legacy <c>ALVA.SETUP.Main.LoadSettings</c> — jen pole potřebná pro klientský
/// update flow. <see cref="SourceDir"/> = legacy LocalClientSourcePath sémantika.
/// </summary>
public record UpdateSettings(bool Enabled, string CacheVersion, string EdisonPath, string LocalSourcePath)
{
    public string SourceDir => string.IsNullOrEmpty(LocalSourcePath) ? EdisonPath : LocalSourcePath;
}

public enum UpdateOutcome
{
    /// <summary>Verze sedí (nebo update vypnutý) — pokračovat ve startu.</summary>
    None,
    /// <summary>Nová verze na serverové instalaci — spawn EDUPDATE + shutdown.</summary>
    UpdateRequired,
    /// <summary>Stav, který klient nespraví (nečitelné verze, Caché mismatch bez nové verze) — popup + shutdown.</summary>
    Broken,
}

public record UpdateCheckResult(UpdateOutcome Outcome, string Message = "", string Version = "");

/// <summary>
/// Klientský (stanicový) update — detekce (porovnání lokální VERZE.INF proti serverové
/// instalaci a Caché verzi) + spuštění EDUPDATE. Mandatory sémantika bez user volby;
/// celý flow viz docs/UPDATE_FLOW.md §7. Virtuální metody kvůli testům (čtení disku, spawn).
/// </summary>
public class Updater
{
    private const string UpdaterExe = "EDUPDATE.EXE";
    private const string ShellExe = "EDISONMAIN2.exe";
    private const string ShellProcessName = "EDISONMAIN2";

    // object[] protokol — LoadSettings je legacy endpoint s numerickými subscripty.
    public virtual UpdateSettings LoadSettings(IMLRest mlRest)
    {
        var output = mlRest.QueryOrThrow("ALVA.SETUP.Main", "LoadSettings", new object[] { });
        char del = mlRest.Delimiter;

        bool enabled = false;
        string version = "", edisonPath = "", localSourcePath = "";
        for (int i = 1; i <= output.GetUpperBound(0); i++)
        {
            var parts = (output[i]?.ToString() ?? "").Split(del);
            if (parts.Length < 2) continue;
            switch (parts[0])
            {
                case "update": enabled = int.TryParse(parts[1], out int e) && e != 0; break;
                case "version": version = parts[1]; break;
                case "edisonPath": edisonPath = parts[1]; break;
                case "localSourcePath": localSourcePath = parts[1]; break;
            }
        }
        return new UpdateSettings(enabled, version, edisonPath, localSourcePath);
    }

    public UpdateCheckResult Check(UpdateSettings settings)
    {
        // Divergence od legacy: Enabled se honoruje vždy (legacy klient ho přes ForceYes
        // ignoruje) — dev server má update~0 a nedosažitelný edisonPath, viz UPDATE_FLOW.md §7.
        if (!settings.Enabled) return new(UpdateOutcome.None);

        var local = GetLocalVersion();
        // Dev build nikdy netriggeruje update ani Broken — vývoj proti libovolnému serveru.
        if (local == VerzeInf.DevVersion) return new(UpdateOutcome.None);
        if (local == "")
            return new(UpdateOutcome.Broken,
                "Nelze zjistit verzi systému EDISON (chybí VERZE.INF). Kontaktujte Vašeho správce IS.");

        var source = GetSourceVersion(settings.SourceDir);
        if (source == "")
            return new(UpdateOutcome.Broken,
                $"Z cesty \"{settings.SourceDir}\" nelze zjistit serverovou verzi. Kontaktujte Vašeho správce IS.");

        if (VerzeInf.IsNewVersion(source, local))
            return new(UpdateOutcome.UpdateRequired, Version: source);

        if (settings.CacheVersion != "" && !VerzeInf.IsSameVersion(settings.CacheVersion, local))
            return new(UpdateOutcome.Broken,
                $"Nesouhlasí verze programů a objektů [{local} -> {settings.CacheVersion}].");

        return new(UpdateOutcome.None);
    }

    protected virtual string GetLocalVersion() => VerzeInf.Current;

    protected virtual string GetSourceVersion(string sourceDir) => VerzeInf.ReadVersion(sourceDir);

    /// <summary>
    /// Self-update updateru — kopii dělá shell, protože EDUPDATE v tu chvíli neběží
    /// (legacy UpdateClientUpdater pattern).
    /// </summary>
    public virtual void CopyUpdater(string sourceDir) =>
        File.Copy(Path.Combine(sourceDir, UpdaterExe),
                  Path.Combine(AppContext.BaseDirectory, UpdaterExe), overwrite: true);

    /// <summary>
    /// Spawn EDUPDATE — stejná sada args jako legacy clsUpdate.StartUpdate klientská větev,
    /// plus WaitProcess/RunExe (úprava UPDATERCL pro EDISONMAIN2).
    /// </summary>
    public virtual void LaunchUpdate(string sourceDir, string version)
    {
        var targetPath = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var args = string.Join(" ", new[]
        {
            "FormMode=\"1\"",
            "Async=\"1\"",
            "RunEdison=\"1\"",
            $"RunExe=\"{ShellExe}\"",
            $"WaitProcess=\"{ShellProcessName}\"",
            $"SourcePath=\"{sourceDir}\"",
            $"TargetPath=\"{targetPath}\"",
            $"Version=\"{version}\"",
            "NucenyUpdate=\"False\"",
            "NucenyUpdateTime=\"\"",
        });
        Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(AppContext.BaseDirectory, UpdaterExe),
            Arguments = args,
            UseShellExecute = true,
            WorkingDirectory = AppContext.BaseDirectory,
        });
    }
}
