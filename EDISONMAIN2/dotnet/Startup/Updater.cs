using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using EDControls;
using Microsoft.Win32.SafeHandles;

namespace EDISONMAIN2;

/// <summary>
/// Subset legacy <c>ALVA.SETUP.Main.LoadSettings</c> - jen pole potřebná pro klientský
/// update flow. <see cref="SourceDir"/> = legacy LocalClientSourcePath sémantika.
/// <see cref="ForcedStamp"/> = razítko vynucené aktualizace klientů (globál
/// NucenyUpdateLocalClientu; wire klíč "NucenyUpdatClentu" - překlep je součást kontraktu).
/// <see cref="Pending"/> = na serveru běží instalace verze (legacy EDLOADER <c>UpdateIsRunning</c>).
/// </summary>
public record UpdateSettings(string CacheVersion, string EdisonPath, string LocalSourcePath,
                             string SourceServer = "", int SourcePort = 0, string ForcedStamp = "",
                             bool Pending = false)
{
    public string SourceDir => string.IsNullOrEmpty(LocalSourcePath) ? EdisonPath : LocalSourcePath;

    /// <summary>
    /// Stanice není v seznamu lokálních klientů (legacy <c>clsUpdate.IsServer</c>): server, TS nebo
    /// stanice spouštěná ze share. Prázdný seznam = serverová je každá instalace.
    /// </summary>
    public bool ServerInstallation { get; init; }
}

public enum UpdateOutcome
{
    /// <summary>Verze sedí (nebo server nemá nainstalovaný release) - pokračovat ve startu.</summary>
    None,
    /// <summary>Nová verze na serverové instalaci (nebo vynucená vlna) - spawn updateru + shutdown.</summary>
    UpdateRequired,
    /// <summary>Stav, který klient nespraví (nečitelné verze, Caché mismatch bez nové verze) - popup + shutdown.</summary>
    Broken,
}

/// <summary><c>Forced</c> = update vyvolala vynucená vlna (razítko nesedí), ne novější verze.</summary>
public record UpdateCheckResult(UpdateOutcome Outcome, string Message = "", string Version = "", bool Forced = false);

public enum ReleaseNoticeOutcome
{
    /// <summary>Nelze zjistit - žádný sourceServer, dev build, síťová chyba.</summary>
    Unknown,
    /// <summary>Release server nemá nic novějšího (blokovaná distribuce se počítá jako nic) - běží nejnovější verze.</summary>
    UpToDate,
    /// <summary>Na release serveru je novější verze (ReleaseNoticeResult.Version).</summary>
    NewVersionAvailable,
}

public record ReleaseNoticeResult(ReleaseNoticeOutcome Outcome, string Version = "");

/// <summary>Updater, kterým shell aktualizuje stanici; aktivní volí <see cref="Updater.ActiveKind"/>.</summary>
public enum ClientUpdaterKind
{
    /// <summary>Legacy EDUPDATE (repo EDISON) spuštěný jako ze starého shellu, stopa vlny v CliUp.ini.</summary>
    LegacyEdupdate,
    /// <summary>EDUPDATE2, kontrakt v EDUPDATE2/CLAUDE.md, stopa vlny v CliUp2.ini.</summary>
    Edupdate2,
}

/// <summary>
/// Klientský (stanicový) update - detekce (porovnání lokální VERZE.INF proti serverové
/// instalaci a Caché verzi, razítko vynucené vlny) + spuštění updateru podle <see cref="ActiveKind"/>.
/// Mandatory sémantika bez user volby; virtuální metody kvůli testům (čtení disku, spawn).
/// </summary>
public class Updater
{
    /// <summary>
    /// Přepnutí = změna konstanty a vydání shellu. EDUPDATE2 potřebuje EDUPDATE2.exe ve zdrojovém
    /// adresáři serveru; při razítku vlny v Caché kvůli jinému souboru stopy jednou plně zkopíruje každou stanici.
    /// </summary>
    public const ClientUpdaterKind ActiveKind = ClientUpdaterKind.LegacyEdupdate;

    // Restart přes EDLOADER: o shellu po updatu rozhodne přepínač nového shellu, ne updater.
    private const string RestartExe = "EDISON.exe";
    private const string ShellProcessName = "EDISONMAIN2";

    // object[] protokol - LoadSettings je legacy endpoint s numerickými subscripty.
    public virtual UpdateSettings LoadSettings(IMLRest mlRest)
    {
        var output = mlRest.QueryOrThrow("ALVA.SETUP.Main", "LoadSettings", new object[] { });
        char del = mlRest.Delimiter;

        bool pending = false;
        string version = "", edisonPath = "", localSourcePath = "", sourceServer = "", forcedStamp = "";
        int sourcePort = 0;
        var localClients = new List<string>();
        for (int i = 1; i <= output.GetUpperBound(0); i++)
        {
            var parts = (output[i]?.ToString() ?? "").Split(del);
            if (parts.Length < 2) continue;
            switch (parts[0])
            {
                case "pending": pending = int.TryParse(parts[1], out int p) && p != 0; break;
                case "version": version = parts[1]; break;
                case "edisonPath": edisonPath = parts[1]; break;
                case "localSourcePath": localSourcePath = parts[1]; break;
                case "localPath": localClients.Add(parts[1]); break; // localPath~<Pc>~<poznámka>
                case "sourceServer": sourceServer = parts[1]; break;
                case "sourcePort": int.TryParse(parts[1], out sourcePort); break;
                case "NucenyUpdatClentu": forcedStamp = parts[1]; break;
            }
        }
        return new UpdateSettings(version, edisonPath, localSourcePath, sourceServer, sourcePort, forcedStamp, pending)
        {
            ServerInstallation = !IsLocalClient(localClients),
        };
    }

    protected virtual bool IsLocalClient(IReadOnlyList<string> localClients) =>
        localClients.Count > 0 && IsListedClient(localClients, Dns.GetHostName(), LocalIPv4());

    /// <summary>
    /// Párování jako legacy <c>clsUpdate.IsServer</c>: záznam je jméno PC, <c>\\PC\...</c> nebo IP.
    /// Záznam s tečkou se porovná s první IPv4 stanice, takže FQDN nikdy neprojde (legacy parita).
    /// </summary>
    internal static bool IsListedClient(IEnumerable<string> localClients, string hostName, string ipv4)
    {
        foreach (var entry in localClients)
        {
            var pc = entry.StartsWith(@"\\", StringComparison.Ordinal) ? entry[2..].Split('\\')[0] : entry;
            if (string.Equals(pc, pc.Contains('.') ? ipv4 : hostName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string LocalIPv4()
    {
        try
        {
            return Dns.GetHostAddresses(Dns.GetHostName())
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)?.ToString() ?? "";
        }
        catch (SocketException)
        {
            return ""; // bez IP zbývá párování jménem PC
        }
    }

    public UpdateCheckResult Check(UpdateSettings settings)
    {
        // Parita s EDLOADER UpdateIsRunning: během serverové instalace se klient nepřihlásí (rozjeté verze).
        if (settings.Pending)
            return new(UpdateOutcome.Broken, "Probíhá aktualizace systému, aplikace bude ukončena.");

        // Server bez nainstalovaného release (prázdné Update,2 - dev) nemá stanicím co nabídnout. Flag
        // "update" legacy klient ignoruje: stanice musí jít za serverem, jinak ji po FULL shodí verzová brána.
        if (settings.CacheVersion == "") return new(UpdateOutcome.None);

        var local = GetLocalVersion();
        // Dev build nikdy netriggeruje update ani Broken - vývoj proti libovolnému serveru.
        if (local == VerzeInf.DevVersion) return new(UpdateOutcome.None);
        if (local == "")
            return new(UpdateOutcome.Broken,
                "Nelze zjistit verzi systému EDISON (chybí VERZE.INF). Kontaktujte Vašeho správce IS.");

        // Serverovou instalaci aktualizuje serverový instalátor, ne EDUPDATE: zdroj stanic tu nemusí
        // být čitelný (edisonPath je cesta na serveru) a kopie by šla do instalace, ze které běží ostatní.
        if (settings.ServerInstallation)
            return VerzeInf.IsSameVersion(settings.CacheVersion, local)
                ? new(UpdateOutcome.None)
                : CacheMismatch(local, settings.CacheVersion);

        var source = GetSourceVersion(settings.SourceDir);
        if (source == "")
            return new(UpdateOutcome.Broken,
                $"Z cesty \"{settings.SourceDir}\" nelze zjistit serverovou verzi. Kontaktujte Vašeho správce IS.");

        // Vynucená vlna: razítko z Caché nesedí s lokální stopou → plné stažení. Záměrně až
        // po dev guardu a Broken checcích a před Caché mismatchem (plná kopie ho může spravit).
        // Klient spuštěný přímo ze zdroje ne (legacy parita): kopie updateru sama na sebe by shell ukončila při každém startu.
        bool forcedPending = settings.ForcedStamp != "" && GetForcedStampSeen() != settings.ForcedStamp
            && !RunsFromSourceDir(settings.SourceDir);

        // I s novou verzí plná kopie: diff jde podle manifestu vydání a hotfix nahraný po instalaci by vynechal.
        if (VerzeInf.IsNewVersion(source, local))
            return new(UpdateOutcome.UpdateRequired, Version: source, Forced: forcedPending);

        if (forcedPending)
            return new(UpdateOutcome.UpdateRequired, Version: source, Forced: true);

        if (!VerzeInf.IsSameVersion(settings.CacheVersion, local))
            return CacheMismatch(local, settings.CacheVersion);

        return new(UpdateOutcome.None);
    }

    private static UpdateCheckResult CacheMismatch(string local, string cacheVersion) =>
        new(UpdateOutcome.Broken, $"Nesouhlasí verze programů a objektů [{local} -> {cacheVersion}].");

    protected virtual string GetLocalVersion() => VerzeInf.Current;

    protected virtual string GetSourceVersion(string sourceDir)
    {
        if (!IsUri(sourceDir)) return VerzeInf.ReadVersion(sourceDir);
        try
        {
            // StreamReader kvůli BOM - Encoding.GetString ho nechá a klíč LAST by pak nesedl.
            using var reader = new StreamReader(new MemoryStream(DownloadSourceFile(SourceFileUrl(sourceDir, VerzeInf.FileName))));
            return VerzeInf.ParseLast(reader.ReadToEnd());
        }
        // IOException: přerušené spojení při čtení těla (HttpIOException) HttpRequestException nezabalí.
        catch (Exception e) when (e is HttpRequestException or IOException or TaskCanceledException
                                      or UriFormatException or InvalidOperationException)
        {
            return ""; // → Broken "nelze zjistit serverovou verzi" (legacy parita)
        }
    }

    /// <summary>Zdroj stanic jako URL (legacy <c>clsUpdate.PathIsUri</c>) - legacy EDUPDATE ho umí stahovat sám.</summary>
    internal static bool IsUri(string path) =>
        path.StartsWith("http:", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("https:", StringComparison.OrdinalIgnoreCase);

    internal static string SourceFileUrl(string sourceDir, string fileName) =>
        sourceDir.Replace('\\', '/').TrimEnd('/') + "/" + fileName;

    protected virtual byte[] DownloadSourceFile(string url)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        return client.GetByteArrayAsync(url).GetAwaiter().GetResult();
    }

    /// <summary>Shell běží přímo ze zdrojového adresáře stanic (legacy <c>clsUpdate.UpdateNeeded</c> sourcePath = targetPath).</summary>
    protected virtual bool RunsFromSourceDir(string sourceDir) =>
        IsSameInstallation(AppContext.BaseDirectory, sourceDir);

    /// <summary>
    /// Tentýž adresář textově (normalizace jako legacy <c>clsUpdate.NormalizePath</c>), nebo fyzicky:
    /// VERZE.INF je v obou cestách tentýž soubor, i když server vidí <c>D:\Edison</c> a zdroj je <c>\\SERVER\Edison</c>.
    /// </summary>
    internal static bool IsSameInstallation(string installDirectory, string sourceDir)
    {
        if (string.IsNullOrWhiteSpace(sourceDir) || IsUri(sourceDir)) return false;
        try
        {
            if (string.Equals(NormalizeDirectory(installDirectory), NormalizeDirectory(sourceDir),
                              StringComparison.OrdinalIgnoreCase))
                return true;
            return FileIdentity(Path.Combine(installDirectory, VerzeInf.FileName)) is { } local
                && FileIdentity(Path.Combine(sourceDir, VerzeInf.FileName)) is { } source
                && local == source;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException
                                      or NotSupportedException)
        {
            return false;
        }
    }

    private static string NormalizeDirectory(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static (uint Volume, ulong Index)? FileIdentity(string path)
    {
        if (!File.Exists(path)) return null;
        using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (!GetFileInformationByHandle(handle, out var info)) return null;
        return (info.VolumeSerialNumber, ((ulong)info.FileIndexHigh << 32) | info.FileIndexLow);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out ByHandleFileInformation info);

    /// <summary>Lokální stopa poslední splněné vynucené vlny vedle exe. Legacy CliUp.ini čte i starý shell,
    /// takže vlnu splněnou kterýmkoli shellem druhý neopakuje.</summary>
    protected virtual string GetForcedStampSeen() => ReadForcedStampSeen(AppContext.BaseDirectory, ActiveKind);

    internal static string ReadForcedStampSeen(string installDirectory, ClientUpdaterKind kind) =>
        EDUPDATE2.ForcedStamp.Read(installDirectory, StampFileFor(kind));

    internal static string UpdaterExeFor(ClientUpdaterKind kind) => kind switch
    {
        ClientUpdaterKind.LegacyEdupdate => "EDUPDATE.exe",
        ClientUpdaterKind.Edupdate2 => "EDUPDATE2.exe",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    internal static string StampFileFor(ClientUpdaterKind kind) => kind switch
    {
        ClientUpdaterKind.LegacyEdupdate => EDUPDATE2.ForcedStamp.LegacyFileName,
        ClientUpdaterKind.Edupdate2 => EDUPDATE2.ForcedStamp.FileName,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>
    /// Self-update updateru - kopii dělá shell, protože updater v tu chvíli neběží
    /// (legacy UpdateClientUpdater pattern).
    /// </summary>
    public virtual void CopyUpdater(string sourceDir) => CopyUpdaterTo(sourceDir, AppContext.BaseDirectory);

    internal void CopyUpdaterTo(string sourceDir, string targetDir)
    {
        var exe = UpdaterExeFor(ActiveKind);
        var target = Path.Combine(targetDir, exe);
        if (!IsUri(sourceDir))
        {
            File.Copy(Path.Combine(sourceDir, exe), target, overwrite: true);
            return;
        }
        var content = DownloadSourceFile(SourceFileUrl(sourceDir, exe));
        // Chybová stránka portálu/proxy s HTTP 200 by jinak přepsala updater a rozbila každý další start.
        if (content.Length < 2 || content[0] != (byte)'M' || content[1] != (byte)'Z')
            throw new InvalidDataException($"Ze zdroje nepřišel spustitelný soubor {exe}.");
        // Přes dočasný soubor: přerušený zápis nesmí zničit lokální updater.
        var partial = target + ".download";
        File.WriteAllBytes(partial, content);
        File.Move(partial, target, overwrite: true);
    }

    /// <summary>
    /// Spawn updateru. Razítko se posílá vždy, když ho známe - i úspěšný běžný update
    /// splní čekající vynucenou vlnu (legacy parita).
    /// </summary>
    public virtual void LaunchUpdate(string sourceDir, string version, bool forced, string forcedStamp)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(AppContext.BaseDirectory, UpdaterExeFor(ActiveKind)),
            UseShellExecute = true,
            // Legacy EDUPDATE zapisuje CliUp.ini relativní cestou - musí skončit tam, odkud ho čte GetForcedStampSeen.
            WorkingDirectory = AppContext.BaseDirectory,
        };
        foreach (var arg in BuildArguments(ActiveKind, sourceDir, version, forced, forcedStamp))
            startInfo.ArgumentList.Add(arg);
        Process.Start(startInfo);
    }

    /// <summary>Tokeny <c>Key=value</c> pro daný updater; quoting (mezery v cestách) řeší ArgumentList.</summary>
    internal static IReadOnlyList<string> BuildArguments(ClientUpdaterKind kind, string sourceDir, string version,
        bool forced, string forcedStamp)
    {
        var targetPath = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        return kind switch
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
            // Kontrakt EDUPDATE2/CLAUDE.md.
            ClientUpdaterKind.Edupdate2 =>
            [
                $"SourcePath={sourceDir}",
                $"TargetPath={targetPath}",
                $"Version={version}",
                $"WaitProcess={ShellProcessName}",
                $"RunExe={RestartExe}",
                $"NucenyUpdate={(forced ? "1" : "0")}",
                $"NucenyUpdateTime={forcedStamp}",
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    /// <summary>
    /// Notice-only porovnání s release serverem M-line (legacy frmMain info panel). Nikdy
    /// nespouští update - instalace release je serverová role (EDTRANSFER2/EDISONINS).
    /// </summary>
    public ReleaseNoticeResult GetReleaseNotice(UpdateSettings settings, string localVersion)
    {
        if (settings.SourceServer == "" || settings.SourcePort <= 0
            || localVersion is "" or VerzeInf.DevVersion)
            return new(ReleaseNoticeOutcome.Unknown);
        try
        {
            // Legacy clsUpdate.SourcePath - VERZE.INF v kořeni release složky na IIS.
            var url = $"http://{settings.SourceServer}:{settings.SourcePort}/EDISON/RELEASE/{VerzeInf.FileName}";
            return EvaluateReleaseNotice(DownloadReleaseVersionFile(url), localVersion);
        }
        catch
        {
            return new(ReleaseNoticeOutcome.Unknown); // notice-only - nedostupný release server nesmí nic rozbít
        }
    }

    public static ReleaseNoticeResult EvaluateReleaseNotice(string content, string localVersion)
    {
        var remote = VerzeInf.ParseLast(content);
        if (remote == "") return new(ReleaseNoticeOutcome.Unknown);
        if (!VerzeInf.IsNewVersion(remote, localVersion)) return new(ReleaseNoticeOutcome.UpToDate);
        // M-line pozastavil distribuci → navenek jako by novější verze neexistovala.
        long.TryParse(VerzeInf.ParseValue(content, VerzeInf.BlockKey), out long blocked);
        return blocked != 0
            ? new(ReleaseNoticeOutcome.UpToDate)
            : new(ReleaseNoticeOutcome.NewVersionAvailable, remote);
    }

    protected virtual string DownloadReleaseVersionFile(string url)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        return client.GetStringAsync(url).GetAwaiter().GetResult();
    }
}
