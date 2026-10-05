using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;

namespace EDUPDATE2;

public enum UpdatePhase
{
    /// <summary>Kontrola parametrů, cest a volného místa; cíl nedotčen.</summary>
    Validation,
    /// <summary>Čekání na ukončení shellu a procesů, které drží vyměňované soubory (Detail = jméno procesu).</summary>
    WaitingForProcess,
    /// <summary>Výběr souborů: diff proti manifestu EDISONCRC.bin, bez něj nebo při vynucení plná kopie.</summary>
    Selection,
    /// <summary>Stažení do EDUPDATE2_Staging s kontrolou hashe; cíl nedotčen, zavření okna update přeruší.</summary>
    Staging,
    /// <summary>Výměna souborů v cíli se zálohou do EDUPDATE2_Backup; okno nejde zavřít.</summary>
    Swap,
    /// <summary>Obnova cíle ze zálohy po chybě výměny; okno nejde zavřít.</summary>
    Rollback,
    /// <summary>Konec běhu - úspěch, no-op i chyba; výsledek nese UpdateResult.</summary>
    Done,
}

public record UpdateProgress(UpdatePhase Phase, string Detail = "", int Percent = -1);

public record UpdateResult(int ExitCode, string Message);

/// <summary>
/// Vlastní logika updatu bez UI. Pořadí fází: validace → čekání na procesy → výběr souborů
/// (CRC diff / plná kopie) → staging do EDUPDATE2_Staging (cíl nedotčen) → swap se zálohou
/// do EDUPDATE2_Backup → při chybě rollback. Exit kódy drží legacy sémantiku 0/1/100/200.
/// FS/process operace jsou virtual kvůli testům.
/// </summary>
public class UpdateEngine
{
    public const int ExitOk = 0;
    public const int ExitBadParamsOrAborted = 1;
    public const int ExitFailedRestored = 100;
    public const int ExitFailedRestoreFailed = 200;

    internal const string OwnExe = "EDUPDATE2.exe";
    internal const string StagingDirName = "EDUPDATE2_Staging";
    internal const string BackupDirName = "EDUPDATE2_Backup";
    private const string DiffInfSuffix = "_DIFF.INF";
    // Kromě shellů i procesy, které drží vyměňované soubory: TemplateDesigner načítá EDControls.dll a shell
    // ho při exitu nezavírá, když ho spustila legacy agenda; EDISONMAIN2API hostuje agendy z téhož adresáře.
    // Se zamčeným souborem by swap selhal a update skončil rollbackem.
    private static readonly string[] FixedWaitProcesses = ["EDISONMAIN", "EDUPDATE", "TemplateDesigner", "EDISONMAIN2API"];
    private static readonly string[] SourceSubDirs = ["EDFiles", "PS", "locales"];
    // Kopírují se vždy - v CRC manifestu nemají větu (ověřeno na produkci), diff by je nikdy nevzal:
    // - EDISONini.xml: adresa Caché serveru + REST port pro legacy svět; serverová kopie je autoritativní,
    // - MLServerConfig-data.xml: konfigurace MLServer připojení, čtou ji hostované legacy agendy,
    // - VERZE.inf: nositel verze - updater verzi nepíše, zkopírováním se stanice stane "aktualizovanou"
    //   (bez toho by shell detekoval update donekonečna),
    // - EDISONCRC.bin: manifest samotný, lokální kopie pro diagnostiku (legacy parita).
    private static readonly string[] KeyFiles = ["MLServerConfig-data.xml", "EDISONini.xml", "VERZE.inf", "EDISONCRC.bin"];
    // Http zdroj nemá výpis adresáře, klíčové soubory se stahují naslepo; tyhle dva legacy
    // InstallEdisonFiles při jakékoli chybě stažení přeskočí, chybějící zbytek je vadný zdroj.
    private static readonly string[] OptionalHttpKeyFiles = ["MLServerConfig-data.xml", "EDISONini.xml"];
    private const long RequiredFreeBytes = 300L * 1024 * 1024;
    private const int CopyRetries = 5;
    // Timeout klienta hlídá jen hlavičky (ResponseHeadersRead), tělo hlídá BodyIdleTimeoutMs.
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    internal int WaitTimeoutMs { get; init; } = 120_000;
    internal int WaitPollMs { get; init; } = 250;
    /// <summary>Nejdelší pauza bez dat při stahování těla souboru, pak pokus selže.</summary>
    internal int BodyIdleTimeoutMs { get; init; } = 60_000;

    private readonly UpdaterArgs _args;
    private readonly UpdateLog _log;
    private readonly IProgress<UpdateProgress>? _progress;
    private readonly CancellationToken _cancel;
    // Autoritativní fáze pro okno - Progress<T> doručuje přes Dispatcher se zpožděním,
    // rozhodnutí "smí se zavřít?" nesmí stát na opožděné kopii.
    private volatile int _currentPhase;
    // Soubory kořene http zdroje podle manifestu (normalizovaná jména) - jiný seznam zdroje neexistuje.
    private HashSet<string>? _httpRootFiles;

    private sealed record Item(string Filename, string SubPath, string? ExpectedCrc);

    public UpdateEngine(UpdaterArgs args, UpdateLog log, IProgress<UpdateProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _args = args;
        _log = log;
        _progress = progress;
        _cancel = cancellationToken;
    }

    public UpdatePhase CurrentPhase => (UpdatePhase)_currentPhase;

    /// <summary>Zdroj jako URL (legacy <c>PathIsUri</c>): soubory se stahují, seznam dává jen manifest.</summary>
    private bool HttpSource => UpdaterContract.IsUri(_args.SourcePath);

    public UpdateResult Run()
    {
        _log.Write("Parametry", $"SourcePath={_args.SourcePath} TargetPath={_args.TargetPath} " +
            $"Version={_args.Version} WaitProcess={_args.WaitProcess} RunExe={_args.RunExe} " +
            $"NucenyUpdate={(_args.NucenyUpdate ? 1 : 0)} NucenyUpdateTime={_args.NucenyUpdateTime}");

        var validationError = Validate();
        if (validationError != null) return Fail(ExitBadParamsOrAborted, validationError);

        string stagingDir = Path.Combine(_args.TargetPath, StagingDirName);
        List<Item> items;
        try
        {
            var busyProcess = WaitForProcesses();
            if (busyProcess != null)
                return Fail(ExitBadParamsOrAborted,
                    $"Stále běží proces [{busyProcess}]. Zavřete ho a spusťte EDISON znovu.");

            string sourceInfVersion = ReadSourceVersion();
            // Bez zdrojové VERZE.INF by se lokální verze neposunula a shell by update
            // detekoval donekonečna; vynucenou vlnu ukončuje razítko, ta projít smí.
            if (!_args.NucenyUpdate && sourceInfVersion == "")
                return Fail(ExitBadParamsOrAborted,
                    $"Ve zdroji \"{_args.SourcePath}\" chybí čitelný {VerzeInf.FileName}.");

            string sourceVersion = _args.Version != "" ? _args.Version : sourceInfVersion;
            string localVersion = ReadVersion(_args.TargetPath);
            if (!_args.NucenyUpdate && !VerzeInf.IsNewVersion(sourceVersion, localVersion))
            {
                // Razítko se nezapisuje - vynucená vlna chce reálné kopírování, ne no-op.
                _log.Write("Verze", $"Aktualizaci nebylo nutné provést [{localVersion} -> {sourceVersion}].");
                Report(UpdatePhase.Done, percent: 100);
                return new(ExitOk, "Aktualizaci nebylo nutné provést.");
            }

            items = SelectFiles();
            StageFiles(items, stagingDir);
        }
        // Timeout http přichází taky jako OperationCanceledException - zrušení je jen zavření okna.
        catch (OperationCanceledException) when (_cancel.IsCancellationRequested)
        {
            TryDeleteDirectory(stagingDir);
            return Fail(ExitBadParamsOrAborted, "Aktualizace byla přerušena, EDISON zůstal beze změny.");
        }
        catch (Exception e)
        {
            TryDeleteDirectory(stagingDir);
            return Fail(ExitBadParamsOrAborted,
                $"Stažení souborů selhalo, instalace nezačala a EDISON zůstal beze změny. {e.Message}");
        }

        string backupDir = Path.Combine(_args.TargetPath, BackupDirName);
        var swapped = new List<(string Target, string? Backup)>();
        try
        {
            SwapFiles(items, stagingDir, backupDir, swapped);
        }
        catch (Exception e)
        {
            _log.Write("Chyba", $"Instalace selhala: {e.Message}");
            Report(UpdatePhase.Rollback);
            bool restored = Rollback(swapped);
            TryDeleteDirectory(stagingDir);
            return restored
                ? Fail(ExitFailedRestored,
                    "Aktualizace selhala, původní verze byla obnovena. Spusťte EDISON ručně; pokud se chyba opakuje, kontaktujte správce IS.")
                : Fail(ExitFailedRestoreFailed,
                    $"Aktualizace selhala a obnovu původních souborů se nepodařilo dokončit. Kontaktujte správce IS. Podrobnosti: {_log.FilePath}");
        }

        TryDeleteDirectory(stagingDir);
        if (_args.NucenyUpdateTime != "")
        {
            try
            {
                WriteForcedStamp(_args.TargetPath, _args.NucenyUpdateTime);
            }
            catch (Exception e)
            {
                // Neúspěch jen znamená, že příští start spustí vynucenou vlnu znovu.
                _log.Write("Varování", $"Zápis {ForcedStamp.FileName} selhal: {e.Message}");
            }
        }
        _log.Write("UPDATER", "Aktualizace proběhla úspěšně.");
        Report(UpdatePhase.Done, percent: 100);
        return new(ExitOk, "Aktualizace proběhla úspěšně.");
    }

    private string? Validate()
    {
        Report(UpdatePhase.Validation);
        if (_args.SourcePath == "") return "Chybí parametr SourcePath.";
        if (!HttpSource && !DirectoryExists(_args.SourcePath))
            return $"Zdrojová cesta \"{_args.SourcePath}\" neexistuje nebo není dostupná.";
        if (!DirectoryExists(_args.TargetPath))
            return $"Cílová cesta \"{_args.TargetPath}\" neexistuje.";
        if (!HttpSource)
        {
            string fullSource = Path.GetFullPath(_args.SourcePath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullTarget = Path.GetFullPath(_args.TargetPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(fullSource, fullTarget, StringComparison.OrdinalIgnoreCase))
                return "Zdroj a cíl jsou tentýž adresář - serverová instalace se tímto updaterem neaktualizuje.";
            if (fullTarget.StartsWith(fullSource + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || fullSource.StartsWith(fullTarget + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return "Zdroj a cíl jsou vnořené adresáře - pracovní složky updateru by poškodily zdroj.";
        }
        if (_args.RunExe != "" && (_args.RunExe.IndexOfAny(['\\', '/', ':']) >= 0))
            return "RunExe musí být jen jméno souboru bez cesty.";
        if (_args.WaitProcess.IndexOfAny(['\\', '/', ':']) >= 0)
            return "WaitProcess musí být jméno procesu bez cesty.";
        try
        {
            if (GetFreeBytes(_args.TargetPath) < RequiredFreeBytes)
                return "Na disku není alespoň 300 MB volného místa.";
        }
        catch (Exception e)
        {
            _log.Write("Varování", $"Kontrola volného místa selhala, pokračuji: {e.Message}");
        }
        return null;
    }

    private string? WaitForProcesses()
    {
        // GetProcessesByName chce jméno bez přípony - "EDISONMAIN2.exe" by tiše nikdy nematchlo.
        string waitProcess = Path.GetFileNameWithoutExtension(_args.WaitProcess);
        var names = FixedWaitProcesses
            .Concat(waitProcess != "" ? new[] { waitProcess } : Array.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        int waitedMs = 0;
        while (true)
        {
            _cancel.ThrowIfCancellationRequested();
            string? busy = names.FirstOrDefault(n => GetProcessCount(n) > 0);
            if (busy == null) return null;
            if (waitedMs >= WaitTimeoutMs)
            {
                _log.Write("Čekání", $"Proces [{busy}] neskončil do {WaitTimeoutMs / 1000} s.");
                return busy;
            }
            Report(UpdatePhase.WaitingForProcess, busy);
            Sleep(WaitPollMs);
            waitedMs += WaitPollMs;
        }
    }

    private List<Item> SelectFiles()
    {
        Report(UpdatePhase.Selection);
        if (HttpSource) return SelectHttpFiles();
        var items = new List<Item>();
        string crcPath = Path.Combine(_args.SourcePath, CrcFile.FileName);
        IReadOnlyList<CrcRecord>? records = null;
        if (!_args.NucenyUpdate && FileExists(crcPath))
        {
            try
            {
                records = ReadCrcRecords(crcPath);
            }
            catch (InvalidDataException e)
            {
                _log.Write("Varování", $"{e.Message} Přecházím na plnou kopii.");
            }
        }

        if (records != null)
        {
            AddManifestItems(items, records, useHashes: true);
            AddKeyFiles(items, keyFile => FileExists(Path.Combine(_args.SourcePath, keyFile)));
            _log.Write("Výběr", $"Diferenční aktualizace, {items.Count} souborů.");
        }
        else
        {
            foreach (var file in EnumerateFiles(_args.SourcePath))
                if (!IsOwnExe(file))
                    items.Add(new Item(file, "", null));
            foreach (var subDir in SourceSubDirs)
            {
                string dir = Path.Combine(_args.SourcePath, subDir);
                if (!DirectoryExists(dir)) continue;
                foreach (var file in EnumerateFiles(dir))
                    items.Add(new Item(file, subDir, null));
            }
            _log.Write("Výběr", $"Plná kopie, {items.Count} souborů.");
        }
        return items;
    }

    /// <summary>
    /// Http zdroj nemá výpis adresáře: bez manifestu update skončí (legacy parita) a vynucená vlna
    /// vezme všechny jeho věty bez hashe (legacy bere ListFiles.txt z release serveru, tentýž seznam vydání).
    /// Hotfix se nahrává na server bez nového manifestu, hash by opravený soubor odmítl.
    /// </summary>
    private List<Item> SelectHttpFiles()
    {
        using var manifest = new MemoryStream();
        if (!DownloadWithRetry(SourceUrl("", CrcFile.FileName), manifest))
            throw new InvalidDataException(
                $"Ve zdroji chybí {CrcFile.FileName} - bez něj nejde přes http zjistit seznam souborů.");
        manifest.Position = 0;
        var records = CrcFile.Read(manifest);
        _httpRootFiles = records.Where(r => r.SubPath == "").Select(r => NormalizeName(r.Filename))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var items = new List<Item>();
        AddManifestItems(items, records, useHashes: !_args.NucenyUpdate);
        AddKeyFiles(items, _ => true);
        _log.Write("Výběr", _args.NucenyUpdate
            ? $"Plná kopie podle manifestu, {items.Count} souborů."
            : $"Diferenční aktualizace, {items.Count} souborů.");
        return items;
    }

    /// <summary>useHashes: přeskočit soubory shodné s cílem a stažené ověřit; bez něj vezme a nechá projít všechno.</summary>
    private void AddManifestItems(List<Item> items, IReadOnlyList<CrcRecord> records, bool useHashes)
    {
        // Dedup přes Win32 normalizaci ("B.dll" vs "B.dll " je na disku tentýž soubor) -
        // druhá věta by ve swapu přesouvala už přesunutý staged soubor.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records)
        {
            if (IsOwnExe(record.Filename)) continue;
            if (!seen.Add(record.SubPath + "|" + NormalizeName(record.Filename)))
            {
                _log.Write("Varování", $"Duplicitní věta manifestu přeskočena: {record.SubPath}\\{record.Filename}");
                continue;
            }
            string target = Path.Combine(_args.TargetPath, record.SubPath, record.Filename);
            if (useHashes && FileExists(target) && ComputeSha256(target) == record.Crc) continue;
            items.Add(new Item(record.Filename, record.SubPath, useHashes ? record.Crc : null));
        }
    }

    private static void AddKeyFiles(List<Item> items, Func<string, bool> onSource)
    {
        foreach (var keyFile in KeyFiles)
        {
            if (!onSource(keyFile)) continue;
            if (items.Any(i => i.SubPath == "" && NameEquals(i.Filename, keyFile))) continue;
            items.Add(new Item(keyFile, "", null));
        }
    }

    private void StageFiles(List<Item> items, string stagingDir)
    {
        if (DirectoryExists(stagingDir)) DeleteDirectory(stagingDir);
        var missingOnSource = new List<Item>();
        for (int i = 0; i < items.Count; i++)
        {
            _cancel.ThrowIfCancellationRequested();
            var item = items[i];
            string source = HttpSource
                ? SourceUrl(item.SubPath, item.Filename)
                : Path.Combine(_args.SourcePath, item.SubPath, item.Filename);
            string staged = Path.Combine(stagingDir, item.SubPath, item.Filename);
            CreateDirectory(Path.GetDirectoryName(staged)!);
            Report(UpdatePhase.Staging, item.Filename, Percent(i, items.Count, firstHalf: true));
            if (!HttpSource)
            {
                CopyWithRetry(source, staged);
            }
            else if (!DownloadToStaging(item, source, staged))
            {
                missingOnSource.Add(item);
                continue;
            }
            // Read-only ze serveru (robocopy/ACL konvence) by se jinak propagovalo do cíle
            // a příští update by na něm spadl.
            ClearReadOnly(staged);
            if (item.ExpectedCrc != null && ComputeSha256(staged) != item.ExpectedCrc)
                throw new IOException($"Soubor {item.Filename} nesouhlasí s kontrolním součtem zdroje.");
            // Chybová stránka proxy s HTTP 200 místo binárky: bez hashe (vynucená vlna) by se nainstalovala,
            // razítko by vlnu splnilo a stanice by zůstala rozbitá.
            if (HttpSource && item.ExpectedCrc == null && IsBinaryName(item.Filename) && !HasPeHeader(staged))
                throw new InvalidDataException($"Ze zdroje nepřišel spustitelný soubor {item.Filename}.");
            _log.Write("Stahování", $"{source} -> {staged}");
        }
        items.RemoveAll(missingOnSource.Contains);
    }

    private void SwapFiles(List<Item> items, string stagingDir, string backupDir,
        List<(string Target, string? Backup)> swapped)
    {
        if (DirectoryExists(backupDir)) DeleteDirectory(backupDir);
        CreateDirectory(backupDir);
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            string target = Path.Combine(_args.TargetPath, item.SubPath, item.Filename);
            string staged = Path.Combine(stagingDir, item.SubPath, item.Filename);
            CreateDirectory(Path.GetDirectoryName(target)!);
            Report(UpdatePhase.Swap, item.Filename, Percent(i, items.Count, firstHalf: false));
            string? backup = null;
            if (FileExists(target))
            {
                backup = Path.Combine(backupDir, item.SubPath, item.Filename);
                CreateDirectory(Path.GetDirectoryName(backup)!);
                CopyFile(target, backup);
                ClearReadOnly(backup);
                // Read-only cíl (historicky rozkopírovaný atribut) by shodil přepsání.
                ClearReadOnly(target);
            }
            MoveFile(staged, target);
            swapped.Add((target, backup));
            _log.Write("Instalace", target);
        }
        RemoveStaleDiffInf(items, backupDir, swapped);
    }

    /// <summary>
    /// Balíček dostal nové soubory → jeho starý _DIFF.INF bez protějšku na zdroji je stale
    /// a changelog okno by z něj hlásilo staré novinky.
    /// </summary>
    private void RemoveStaleDiffInf(List<Item> items, string backupDir,
        List<(string Target, string? Backup)> swapped)
    {
        var packages = items
            .Select(i => Path.GetFileNameWithoutExtension(i.Filename).ToUpperInvariant())
            .ToHashSet();
        foreach (var name in EnumerateFiles(_args.TargetPath).ToList())
        {
            if (!name.EndsWith(DiffInfSuffix, StringComparison.OrdinalIgnoreCase)) continue;
            if (!packages.Contains(name[..^DiffInfSuffix.Length].ToUpperInvariant())) continue;
            if (SourceHasRootFile(name)) continue;
            string target = Path.Combine(_args.TargetPath, name);
            string backup = Path.Combine(backupDir, name);
            CopyFile(target, backup);
            ClearReadOnly(backup);
            ClearReadOnly(target);
            DeleteFile(target);
            swapped.Add((target, backup));
            _log.Write("Instalace", $"Odstraněn neplatný {name}");
        }
    }

    private bool Rollback(List<(string Target, string? Backup)> swapped)
    {
        bool ok = true;
        for (int i = swapped.Count - 1; i >= 0; i--)
        {
            var (target, backup) = swapped[i];
            try
            {
                ClearReadOnly(target);
                if (backup != null) CopyFile(backup, target);
                else DeleteFile(target);
            }
            catch (Exception e)
            {
                ok = false;
                _log.Write("Obnova", $"Nelze obnovit {target}: {e.Message}");
            }
        }
        _log.Write("Obnova", ok ? "Původní soubory obnoveny." : "Obnova se nezdařila.");
        return ok;
    }

    private void CopyWithRetry(string source, string target)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                CopyFile(source, target);
                return;
            }
            catch (Exception e) when (attempt < CopyRetries && e is IOException or UnauthorizedAccessException)
            {
                Sleep(1000);
            }
        }
    }

    // Přes http na stanici dojde jen to, co nese manifest; dotaz na server během výměny by ji zdržel.
    private bool SourceHasRootFile(string name) => HttpSource
        ? _httpRootFiles?.Contains(NormalizeName(name)) == true
        : FileExists(Path.Combine(_args.SourcePath, name));

    private string ReadSourceVersion()
    {
        if (!HttpSource) return ReadVersion(_args.SourcePath);
        try
        {
            using var content = new MemoryStream();
            if (!DownloadWithRetry(SourceUrl("", VerzeInf.FileName), content)) return "";
            content.Position = 0;
            // StreamReader kvůli BOM - klíč LAST by jinak nesedl.
            using var reader = new StreamReader(content);
            return VerzeInf.ParseLast(reader.ReadToEnd());
        }
        catch (Exception e) when (IsDownloadFailure(e))
        {
            _log.Write("Varování", $"{VerzeInf.FileName} ze zdroje nelze stáhnout: {e.Message}");
            return "";
        }
    }

    private string SourceUrl(string subPath, string fileName) =>
        UpdaterContract.FileUrl(_args.SourcePath, fileName, subPath);

    /// <summary>False = volitelný konfigurační soubor se nepodařilo stáhnout a přeskakuje se.</summary>
    private bool DownloadToStaging(Item item, string url, string staged)
    {
        bool optional = item.SubPath == "" && OptionalHttpKeyFiles.Any(k => NameEquals(k, item.Filename));
        try
        {
            if (DownloadToFile(url, staged)) return true;
            if (!optional) throw new FileNotFoundException($"Ve zdroji chybí soubor {url}.");
            _log.Write("Stahování", $"{url} na zdroji není, přeskočeno.");
        }
        catch (Exception e) when (optional && IsDownloadFailure(e))
        {
            _log.Write("Stahování", $"{url} nelze stáhnout, přeskočeno: {e.Message}");
        }
        return false;
    }

    private static bool IsBinaryName(string fileName) =>
        NormalizeName(fileName).EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
        || NormalizeName(fileName).EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

    // Každý PE soubor (nativní i .NET) začíná DOS hlavičkou "MZ".
    private static bool HasPeHeader(string path)
    {
        using var file = File.OpenRead(path);
        return file.ReadByte() == 'M' && file.ReadByte() == 'Z';
    }

    private bool DownloadToFile(string url, string target)
    {
        bool found;
        using (var file = new FileStream(target, FileMode.Create, FileAccess.Write))
            found = DownloadWithRetry(url, file);
        if (!found) DeleteFile(target);
        return found;
    }

    private bool DownloadWithRetry(string url, Stream destination)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                destination.SetLength(0);
                return TryDownload(url, destination);
            }
            catch (Exception e) when (attempt < CopyRetries && IsDownloadFailure(e))
            {
                Sleep(1000);
            }
        }
    }

    // Timeout klienta se hlásí jako zrušení - od zavření okna ho dělí jen stav tokenu.
    private bool IsDownloadFailure(Exception e) =>
        e is HttpRequestException or IOException
        || (e is OperationCanceledException && !_cancel.IsCancellationRequested);

    private UpdateResult Fail(int exitCode, string message)
    {
        _log.Write("Chyba", $"{message} [exit {exitCode}]");
        Report(UpdatePhase.Done);
        return new(exitCode, message);
    }

    private void TryDeleteDirectory(string path)
    {
        try
        {
            if (DirectoryExists(path)) DeleteDirectory(path);
        }
        catch (Exception e)
        {
            _log.Write("Varování", $"Úklid {path} selhal: {e.Message}");
        }
    }

    // Win32 při otevření souboru koncové mezery/tečky zahazuje - "EDUPDATE2.exe " je na disku
    // tentýž soubor jako běžící exe, porovnávat se musí normalizovaně.
    private static string NormalizeName(string filename) => filename.TrimEnd(' ', '.');

    private static bool NameEquals(string a, string b) =>
        string.Equals(NormalizeName(a), NormalizeName(b), StringComparison.OrdinalIgnoreCase);

    private static bool IsOwnExe(string filename) => NameEquals(filename, OwnExe);

    private static int Percent(int index, int count, bool firstHalf)
    {
        int half = count == 0 ? 50 : index * 50 / count;
        return firstHalf ? half : 50 + half;
    }

    private void Report(UpdatePhase phase, string detail = "", int percent = -1)
    {
        _currentPhase = (int)phase;
        _progress?.Report(new UpdateProgress(phase, detail, percent));
    }

    protected virtual void ClearReadOnly(string path)
    {
        if (!FileExists(path)) return;
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReadOnly) != 0)
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
    }

    protected virtual bool FileExists(string path) => File.Exists(path);
    protected virtual bool DirectoryExists(string path) => Directory.Exists(path);
    protected virtual void CreateDirectory(string path) => Directory.CreateDirectory(path);
    protected virtual void CopyFile(string source, string target) => File.Copy(source, target, overwrite: true);
    protected virtual void MoveFile(string source, string target) => File.Move(source, target, overwrite: true);
    protected virtual void DeleteFile(string path) => File.Delete(path);
    protected virtual void DeleteDirectory(string path) => Directory.Delete(path, recursive: true);
    protected virtual IEnumerable<string> EnumerateFiles(string directory) =>
        Directory.EnumerateFiles(directory).Select(p => Path.GetFileName(p)!);
    protected virtual string ComputeSha256(string path) => CrcFile.ComputeSha256(path);
    protected virtual IReadOnlyList<CrcRecord> ReadCrcRecords(string path)
    {
        using var stream = File.OpenRead(path);
        return CrcFile.Read(stream);
    }
    protected virtual string ReadVersion(string directory) => VerzeInf.ReadVersion(directory);

    /// <summary>Soubor http zdroje do <paramref name="destination"/>; false = 404, soubor na zdroji není.</summary>
    protected virtual bool TryDownload(string url, Stream destination)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = Http.Send(request, HttpCompletionOption.ResponseHeadersRead, _cancel);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();
        using var body = response.Content.ReadAsStream(_cancel);
        // Synchronní čtení těla token ignoruje: zaseknuté spojení by neukončilo zavření okna ani timeout.
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(_cancel);
        var buffer = new byte[81920];
        while (true)
        {
            idle.CancelAfter(BodyIdleTimeoutMs);
            int read = body.ReadAsync(buffer, idle.Token).AsTask().GetAwaiter().GetResult();
            if (read == 0) return true;
            destination.Write(buffer, 0, read);
        }
    }
    protected virtual void WriteForcedStamp(string targetPath, string stamp) => ForcedStamp.Write(targetPath, stamp);
    protected virtual int GetProcessCount(string name) => Process.GetProcessesByName(name).Length;
    protected virtual void Sleep(int milliseconds) => Thread.Sleep(milliseconds);
    protected virtual long GetFreeBytes(string path) =>
        new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!).AvailableFreeSpace;
}
