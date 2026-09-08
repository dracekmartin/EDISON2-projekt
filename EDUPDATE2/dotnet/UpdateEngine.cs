using System.Diagnostics;
using System.IO;
using EDISONMAIN2;

namespace EDUPDATE2;

public enum UpdatePhase
{
    Validation,
    WaitingForProcess,
    Selection,
    Staging,
    Swap,
    Rollback,
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
    private static readonly string[] FixedWaitProcesses = ["EDISONMAIN", "EDUPDATE"];
    private static readonly string[] SourceSubDirs = ["EDFiles", "PS", "locales"];
    // Kopírují se vždy - v CRC manifestu nemají větu (ověřeno na produkci), diff by je nikdy nevzal:
    // - EDISONini.xml: adresa Caché serveru + REST port pro legacy svět; serverová kopie je autoritativní,
    // - MLServerConfig-data.xml: konfigurace MLServer připojení, čtou ji hostované legacy agendy,
    // - VERZE.inf: nositel verze - updater verzi nepíše, zkopírováním se stanice stane "aktualizovanou"
    //   (bez toho by shell detekoval update donekonečna),
    // - EDISONCRC.bin: manifest samotný, lokální kopie pro diagnostiku (legacy parita).
    private static readonly string[] KeyFiles = ["MLServerConfig-data.xml", "EDISONini.xml", "VERZE.inf", "EDISONCRC.bin"];
    private const long RequiredFreeBytes = 300L * 1024 * 1024;
    private const int CopyRetries = 5;

    internal int WaitTimeoutMs { get; init; } = 120_000;
    internal int WaitPollMs { get; init; } = 250;

    private readonly UpdaterArgs _args;
    private readonly UpdateLog _log;
    private readonly IProgress<UpdateProgress>? _progress;
    private readonly CancellationToken _cancel;
    // Autoritativní fáze pro okno - Progress<T> doručuje přes Dispatcher se zpožděním,
    // rozhodnutí "smí se zavřít?" nesmí stát na opožděné kopii.
    private volatile int _currentPhase;

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

            string sourceInfVersion = ReadVersion(_args.SourcePath);
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
        catch (OperationCanceledException)
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
                ForcedStamp.Write(_args.TargetPath, _args.NucenyUpdateTime);
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
        if (_args.SourcePath.StartsWith("http:", StringComparison.OrdinalIgnoreCase)
            || _args.SourcePath.StartsWith("https:", StringComparison.OrdinalIgnoreCase))
            return "HTTP zdroj není podporován - SourcePath musí být adresář (UNC nebo lokální).";
        if (!DirectoryExists(_args.SourcePath))
            return $"Zdrojová cesta \"{_args.SourcePath}\" neexistuje nebo není dostupná.";
        if (!DirectoryExists(_args.TargetPath))
            return $"Cílová cesta \"{_args.TargetPath}\" neexistuje.";
        string fullSource = Path.GetFullPath(_args.SourcePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string fullTarget = Path.GetFullPath(_args.TargetPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(fullSource, fullTarget, StringComparison.OrdinalIgnoreCase))
            return "Zdroj a cíl jsou tentýž adresář - serverová instalace se tímto updaterem neaktualizuje.";
        if (fullTarget.StartsWith(fullSource + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || fullSource.StartsWith(fullTarget + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return "Zdroj a cíl jsou vnořené adresáře - pracovní složky updateru by poškodily zdroj.";
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
                if (FileExists(target) && ComputeSha256(target) == record.Crc) continue;
                items.Add(new Item(record.Filename, record.SubPath, record.Crc));
            }
            foreach (var keyFile in KeyFiles)
            {
                if (!FileExists(Path.Combine(_args.SourcePath, keyFile))) continue;
                if (items.Any(i => i.SubPath == "" && NameEquals(i.Filename, keyFile))) continue;
                items.Add(new Item(keyFile, "", null));
            }
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

    private void StageFiles(List<Item> items, string stagingDir)
    {
        if (DirectoryExists(stagingDir)) DeleteDirectory(stagingDir);
        for (int i = 0; i < items.Count; i++)
        {
            _cancel.ThrowIfCancellationRequested();
            var item = items[i];
            string source = Path.Combine(_args.SourcePath, item.SubPath, item.Filename);
            string staged = Path.Combine(stagingDir, item.SubPath, item.Filename);
            Directory.CreateDirectory(Path.GetDirectoryName(staged)!);
            Report(UpdatePhase.Staging, item.Filename, Percent(i, items.Count, firstHalf: true));
            CopyWithRetry(source, staged);
            // Read-only ze serveru (robocopy/ACL konvence) by se jinak propagovalo do cíle
            // a příští update by na něm spadl.
            ClearReadOnly(staged);
            if (item.ExpectedCrc != null && ComputeSha256(staged) != item.ExpectedCrc)
                throw new IOException($"Soubor {item.Filename} nesouhlasí s kontrolním součtem zdroje.");
            _log.Write("Stahování", $"{source} -> {staged}");
        }
    }

    private void SwapFiles(List<Item> items, string stagingDir, string backupDir,
        List<(string Target, string? Backup)> swapped)
    {
        if (DirectoryExists(backupDir)) DeleteDirectory(backupDir);
        Directory.CreateDirectory(backupDir);
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            string target = Path.Combine(_args.TargetPath, item.SubPath, item.Filename);
            string staged = Path.Combine(stagingDir, item.SubPath, item.Filename);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            Report(UpdatePhase.Swap, item.Filename, Percent(i, items.Count, firstHalf: false));
            string? backup = null;
            if (FileExists(target))
            {
                backup = Path.Combine(backupDir, item.SubPath, item.Filename);
                Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
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
            if (FileExists(Path.Combine(_args.SourcePath, name))) continue;
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
    protected virtual int GetProcessCount(string name) => Process.GetProcessesByName(name).Length;
    protected virtual void Sleep(int milliseconds) => Thread.Sleep(milliseconds);
    protected virtual long GetFreeBytes(string path) =>
        new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!).AvailableFreeSpace;
}
