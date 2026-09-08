using System.IO;
using EDUPDATE2;

namespace EDUPDATE2.Tests;

/// <summary>
/// Scénářové testy "co přesně se dotklo disku": ReleaseScenario deklarativně postaví
/// serverovou instalaci + stanici + manifest; RecordingEngine (sdílený harness) zaznamená
/// FS operace jdoucí přes seamy enginu. Tvrdí se tedy nejen výsledný stav, ale i že se
/// přes seamy NIC navíc nekopírovalo/nemazalo.
/// </summary>
public class UpdateScenarioTests : IDisposable
{
    private readonly ReleaseScenario _scenario = new();

    public void Dispose() => _scenario.Dispose();

    /// <summary>
    /// Server = adresář instalace u zákazníka, stanice = cíl updatu. FinishManifest generuje
    /// EDISONCRC.bin stejně jako legacy EDISONINS: věta pro každý serverový soubor kromě
    /// konfiguračních keyFiles (ty manifest nenese ani v produkci).
    /// </summary>
    private sealed class ReleaseScenario : IDisposable
    {
        private static readonly string[] ManifestExcluded =
            ["MLServerConfig-data.xml", "EDISONini.xml", "VERZE.inf", "EDISONCRC.bin"];

        public string Server { get; }
        public string Station { get; }
        private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        public ReleaseScenario()
        {
            Server = Path.Combine(_root, "server");
            Station = Path.Combine(_root, "station");
            Directory.CreateDirectory(Server);
            Directory.CreateDirectory(Station);
            ServerVersion("260901.1");
            StationVersion("260825.1");
        }

        public void Dispose() => Directory.Delete(_root, recursive: true);

        public void ServerVersion(string version) =>
            File.WriteAllText(Path.Combine(Server, "VERZE.INF"), $"LAST={version}\n");

        public void StationVersion(string version) =>
            File.WriteAllText(Path.Combine(Station, "VERZE.INF"), $"LAST={version}\n");

        public void ServerFile(string relative, string content) => WriteFile(Server, relative, content);

        public void StationFile(string relative, string content) => WriteFile(Station, relative, content);

        /// <summary>Soubor nasazený v obou stromech se shodným obsahem (= nepatchnutý).</summary>
        public void SharedFile(string relative, string content)
        {
            ServerFile(relative, content);
            StationFile(relative, content);
        }

        private static void WriteFile(string root, string relative, string content)
        {
            var path = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public void FinishManifest()
        {
            var records = new List<CrcRecord>();
            foreach (var file in Directory.EnumerateFiles(Server, "*", SearchOption.AllDirectories))
            {
                var name = Path.GetFileName(file);
                var subPath = Path.GetRelativePath(Server, Path.GetDirectoryName(file)!);
                if (subPath == ".") subPath = "";
                if (subPath == "" && ManifestExcluded.Contains(name, StringComparer.OrdinalIgnoreCase))
                    continue;
                records.Add(new CrcRecord(name, subPath, CrcFile.ComputeSha256(file)));
            }
            using var stream = File.Create(Path.Combine(Server, CrcFile.FileName));
            CrcFile.Write(stream, records);
        }

        public (UpdateResult Result, List<FsOp> Ops) Run(bool forced = false, string stampTime = "",
            Func<string, string, bool>? moveShouldFail = null, Func<string, bool>? deleteShouldFail = null,
            Action<string>? afterCopy = null, long freeBytes = long.MaxValue)
        {
            var engine = new RecordingEngine(
                new UpdaterArgs(SourcePath: Server, TargetPath: Station,
                    NucenyUpdate: forced, NucenyUpdateTime: stampTime),
                new UpdateLog(Station))
            {
                MoveShouldFail = moveShouldFail,
                DeleteShouldFail = deleteShouldFail,
                AfterCopy = afterCopy,
                FreeBytes = freeBytes,
            };
            var result = engine.Run();
            return (result, engine.Ops);
        }

        /// <summary>Soubory stažené ze serveru do stagingu, relativně k serveru, lowercase
        /// (keyFiles konstanta má "VERZE.inf", na disku je "VERZE.INF" - case je šum).</summary>
        public IReadOnlyList<string> Staged(List<FsOp> ops) =>
            ops.Where(o => o.Kind == "copy" && o.Target!.Contains(UpdateEngine.StagingDirName))
               .Select(o => Path.GetRelativePath(Server, o.Path).ToLowerInvariant())
               .OrderBy(p => p, StringComparer.Ordinal)
               .ToList();
    }

    private static string[] Sorted(params string[] items) =>
        items.Select(p => p.ToLowerInvariant()).OrderBy(p => p, StringComparer.Ordinal).ToArray();

    [Fact]
    public void Patch_ChangedSubset_CopiesOnlyChangedPlusKeyFiles()
    {
        // "Vydání patche": z pěti binárek se změnily dvě
        _scenario.SharedFile("Beze1.dll", "stejny");
        _scenario.SharedFile("Beze2.dll", "stejny2");
        _scenario.SharedFile("Beze3.exe", "stejny3");
        _scenario.ServerFile("Patched1.dll", "novy");
        _scenario.StationFile("Patched1.dll", "stary");
        _scenario.ServerFile("Patched2.exe", "novy2");
        _scenario.StationFile("Patched2.exe", "stary2");
        _scenario.FinishManifest();

        var (result, ops) = _scenario.Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal(
            Sorted("Patched1.dll", "Patched2.exe", "VERZE.INF", CrcFile.FileName),
            _scenario.Staged(ops));
        Assert.Equal("novy", File.ReadAllText(Path.Combine(_scenario.Station, "Patched1.dll")));
        Assert.Equal("stejny", File.ReadAllText(Path.Combine(_scenario.Station, "Beze1.dll")));
        Assert.False(Directory.Exists(Path.Combine(_scenario.Station, UpdateEngine.StagingDirName)));
    }

    [Fact]
    public void Patch_NothingChanged_InstallsOnlyKeyFiles()
    {
        _scenario.SharedFile("A.dll", "stejny");
        _scenario.SharedFile("B.dll", "stejny2");
        _scenario.FinishManifest();

        var (result, ops) = _scenario.Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal(Sorted("VERZE.INF", CrcFile.FileName), _scenario.Staged(ops));
    }

    [Fact]
    public void SameVersion_NothingTouchedAtAll()
    {
        _scenario.StationVersion("260901.1"); // == serverová
        _scenario.ServerFile("A.dll", "novy");
        _scenario.FinishManifest();

        // stampTime u no-opu: razítko se nesmí zapsat - vynucená vlna chce reálné kopírování
        var (result, ops) = _scenario.Run(stampTime: "66234,60449");

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Empty(ops);
        Assert.False(Directory.Exists(Path.Combine(_scenario.Station, UpdateEngine.StagingDirName)));
        Assert.Equal("", ForcedStamp.Read(_scenario.Station));
    }

    [Fact]
    public void Patch_SubdirChange_InstallsIntoSubdirOnly()
    {
        _scenario.SharedFile(@"EDFiles\beze.edf", "stejny");
        _scenario.ServerFile(@"PS\vyves.ps", "novy");
        _scenario.StationFile(@"PS\vyves.ps", "stary");
        _scenario.FinishManifest();

        var (result, ops) = _scenario.Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal(Sorted(@"PS\vyves.ps", "VERZE.INF", CrcFile.FileName), _scenario.Staged(ops));
        Assert.Equal("novy", File.ReadAllText(Path.Combine(_scenario.Station, "PS", "vyves.ps")));
        Assert.Equal("stejny", File.ReadAllText(Path.Combine(_scenario.Station, "EDFiles", "beze.edf")));
    }

    [Fact]
    public void NewServerFile_MissingOnStation_Installed()
    {
        _scenario.ServerFile("Novy.dll", "obsah");
        _scenario.FinishManifest();

        var (result, ops) = _scenario.Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Contains("novy.dll", _scenario.Staged(ops));
        Assert.Equal("obsah", File.ReadAllText(Path.Combine(_scenario.Station, "Novy.dll")));
    }

    [Fact]
    public void MissingManifest_FullCopy_AllServerFiles()
    {
        _scenario.SharedFile("A.dll", "stejny"); // i beze změny - bez manifestu není diff
        _scenario.ServerFile(@"locales\cs.pak", "lokalizace");
        // manifest se záměrně negeneruje

        var (result, ops) = _scenario.Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal(Sorted("A.dll", @"locales\cs.pak", "VERZE.INF"), _scenario.Staged(ops));
        Assert.Equal("lokalizace", File.ReadAllText(Path.Combine(_scenario.Station, "locales", "cs.pak")));
    }

    [Fact]
    public void Forced_CopiesEverything_DespiteMatchingHashesAndVersions()
    {
        _scenario.StationVersion("260901.1"); // verze shodné - bez forced by byl no-op
        _scenario.SharedFile("A.dll", "stejny");
        _scenario.FinishManifest();

        var (result, ops) = _scenario.Run(forced: true, stampTime: "66234,1");

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal(Sorted("A.dll", "VERZE.INF", CrcFile.FileName), _scenario.Staged(ops));
        Assert.Equal("66234,1", ForcedStamp.Read(_scenario.Station));
    }

    [Fact]
    public void StationOnlyFiles_NeverTouched()
    {
        // Lokální soubory stanice (config nového shellu, stopa vlny, cizí log) update nesmí zajímat
        _scenario.StationFile("appsettings.json", "lokalni config");
        _scenario.StationFile("CliUp2.ini", "Spuštěn=65995,1");
        _scenario.ServerFile("Patched.dll", "novy");
        _scenario.FinishManifest();

        var (result, ops) = _scenario.Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.DoesNotContain(ops, o =>
            (o.Path.Contains("appsettings.json") || o.Target?.Contains("appsettings.json") == true)
            || (o.Path.Contains("CliUp2.ini") || o.Target?.Contains("CliUp2.ini") == true));
        Assert.Equal("lokalni config", File.ReadAllText(Path.Combine(_scenario.Station, "appsettings.json")));
    }

    [Fact]
    public void DiffUpdate_WritesOnlyIntoWorkDirsAndExpectedTargets()
    {
        // Op-level guard: každý zápis míří jen do stagingu, zálohy, nebo na očekávaný cíl
        _scenario.SharedFile("Beze.dll", "stejny");
        _scenario.ServerFile("Patched.dll", "novy");
        _scenario.StationFile("Patched.dll", "stary");
        _scenario.FinishManifest();

        var (result, ops) = _scenario.Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        var expectedTargets = new[] { "Patched.dll", "VERZE.INF", CrcFile.FileName };
        foreach (var op in ops.Where(o => o.Kind is "copy" or "move"))
        {
            var target = op.Target!;
            bool intoWorkDir = target.Contains(UpdateEngine.StagingDirName)
                || target.Contains(UpdateEngine.BackupDirName);
            bool ontoExpected = expectedTargets.Contains(
                Path.GetRelativePath(_scenario.Station, target), StringComparer.OrdinalIgnoreCase);
            Assert.True(intoWorkDir || ontoExpected, $"Neočekávaný zápis: {op.Kind} -> {target}");
        }
        Assert.DoesNotContain(ops, o => o.Kind == "delete" && !o.Path.Contains(UpdateEngine.StagingDirName));
    }

    [Fact]
    public void ReplacedFiles_HaveBackupCopies()
    {
        _scenario.ServerFile("Patched.dll", "novy");
        _scenario.StationFile("Patched.dll", "stary");
        _scenario.FinishManifest();

        var (result, _) = _scenario.Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal("stary", File.ReadAllText(
            Path.Combine(_scenario.Station, UpdateEngine.BackupDirName, "Patched.dll")));
    }

    [Fact]
    public void ForcedSwapFailure_StampNotWritten()
    {
        // Selhaná vynucená vlna se nesmí tvářit jako splněná - jinak stanice opravu nikdy nedostane
        _scenario.ServerFile("Patched.dll", "novy");
        _scenario.StationFile("Patched.dll", "stary");
        _scenario.FinishManifest();

        var (result, _) = _scenario.Run(forced: true, stampTime: "66234,1",
            moveShouldFail: (_, target) => target.EndsWith("Patched.dll", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(UpdateEngine.ExitFailedRestored, result.ExitCode);
        Assert.Equal("", ForcedStamp.Read(_scenario.Station));
    }

    [Fact]
    public void SuccessfulUpdate_AdvancesStationVersion()
    {
        // Invariant proti smyčce detekce: po updatu je verze stanice == serverová
        _scenario.ServerFile("Patched.dll", "novy");
        _scenario.FinishManifest();

        var (result, _) = _scenario.Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal(EDISONMAIN2.VerzeInf.ReadVersion(_scenario.Server),
            EDISONMAIN2.VerzeInf.ReadVersion(_scenario.Station));
    }

    [Fact]
    public void SecondRunAfterSuccess_IsNoOp()
    {
        _scenario.ServerFile("Patched.dll", "novy");
        _scenario.StationFile("Patched.dll", "stary");
        _scenario.FinishManifest();
        var (first, _) = _scenario.Run();
        Assert.Equal(UpdateEngine.ExitOk, first.ExitCode);

        var (second, ops) = _scenario.Run();

        Assert.Equal(UpdateEngine.ExitOk, second.ExitCode);
        Assert.Empty(ops);
    }

    [Fact]
    public void LeftoverWorkDirsFromCrashedRun_RecoveredAndReplaced()
    {
        // Zbytky po padlém běhu nesmí nový update rozbít ani přežít v záloze
        _scenario.StationFile(Path.Combine(UpdateEngine.StagingDirName, "odpad.tmp"), "x");
        _scenario.StationFile(Path.Combine(UpdateEngine.BackupDirName, "stara-zaloha.dll"), "x");
        _scenario.ServerFile("Patched.dll", "novy");
        _scenario.FinishManifest();

        var (result, _) = _scenario.Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.False(Directory.Exists(Path.Combine(_scenario.Station, UpdateEngine.StagingDirName)));
        Assert.False(File.Exists(Path.Combine(_scenario.Station, UpdateEngine.BackupDirName, "stara-zaloha.dll")));
    }

    [Fact]
    public void SwapFailure_RestoresSubdirFileIntoSubdir()
    {
        _scenario.ServerFile(@"EDFiles\form.edf", "novy");
        _scenario.StationFile(@"EDFiles\form.edf", "stary");
        _scenario.ServerFile("Patched.dll", "novy");
        _scenario.StationFile("Patched.dll", "stary");
        _scenario.ServerFile("Novy.dll", "novy"); // na stanici není - rollback ho musí smazat, ne obnovit
        _scenario.FinishManifest();

        // KeyFiles (VERZE.inf) se swapují jako poslední - selhání na nich zaručí, že oba
        // patchnuté soubory už jsou vyměněné a rollback je musí vracet, subdir do subdiru
        var (result, _) = _scenario.Run(
            moveShouldFail: (_, target) => target.EndsWith("VERZE.inf", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(UpdateEngine.ExitFailedRestored, result.ExitCode);
        Assert.Equal("stary", File.ReadAllText(Path.Combine(_scenario.Station, "EDFiles", "form.edf")));
        Assert.Equal("stary", File.ReadAllText(Path.Combine(_scenario.Station, "Patched.dll")));
        Assert.False(File.Exists(Path.Combine(_scenario.Station, "Novy.dll")));
        Assert.Equal("LAST=260825.1\n", File.ReadAllText(Path.Combine(_scenario.Station, "VERZE.INF")));
    }

    [Fact]
    public void DeletedStaleDiffInf_RestoredOnRollback()
    {
        // Dva stale DIFFy: A_DIFF se smaže (se zálohou), delete B_DIFF selže → rollback
        // musí už smazaný A_DIFF vrátit ze zálohy
        _scenario.ServerFile("A.exe", "novy");
        _scenario.StationFile("A.exe", "stary");
        _scenario.ServerFile("B.exe", "novy");
        _scenario.StationFile("B.exe", "stary");
        _scenario.StationFile("A_DIFF.INF", "novinky A");
        _scenario.StationFile("B_DIFF.INF", "novinky B");
        _scenario.FinishManifest();

        var (result, _) = _scenario.Run(
            deleteShouldFail: path => path.EndsWith("B_DIFF.INF", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(UpdateEngine.ExitFailedRestored, result.ExitCode);
        Assert.Equal("novinky A", File.ReadAllText(Path.Combine(_scenario.Station, "A_DIFF.INF")));
        Assert.Equal("novinky B", File.ReadAllText(Path.Combine(_scenario.Station, "B_DIFF.INF")));
        Assert.Equal("stary", File.ReadAllText(Path.Combine(_scenario.Station, "A.exe")));
    }

    [Fact]
    public void SeededRandomPatchSubset_StagesExactlyChangedFiles()
    {
        // Pevný seed = jeden vygenerovaný objemný případ (není to fuzz); 100 souborů napříč root/EDFiles/PS
        var random = new Random(20260908);
        var changed = new List<string>();
        for (int i = 0; i < 100; i++)
        {
            string dir = (i % 3) switch { 0 => "", 1 => "EDFiles", _ => "PS" };
            string relative = dir == "" ? $"F{i:D3}.dll" : Path.Combine(dir, $"F{i:D3}.dll");
            if (random.Next(2) == 0)
            {
                _scenario.ServerFile(relative, $"novy{i}");
                _scenario.StationFile(relative, $"stary{i}");
                changed.Add(relative);
            }
            else
            {
                _scenario.SharedFile(relative, $"obsah{i}");
            }
        }
        _scenario.FinishManifest();

        var (result, ops) = _scenario.Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal(
            Sorted([.. changed, "VERZE.INF", CrcFile.FileName]),
            _scenario.Staged(ops));
    }

    [Fact]
    public void InsufficientDiskSpace_Exit1_NothingTouched()
    {
        _scenario.ServerFile("A.dll", "novy");
        _scenario.FinishManifest();

        var (result, ops) = _scenario.Run(freeBytes: 10);

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
        Assert.Contains("300 MB", result.Message);
        Assert.Empty(ops);
    }

    [Fact]
    public void SourceVanishesMidStaging_Exit1_TargetUntouched()
    {
        // Výpadek síťového sdílení (\\server\Edison) uprostřed stahování:
        // po první stagované kopii zmizí zbytek zdroje
        _scenario.ServerFile("A.dll", "novy");
        _scenario.ServerFile("B.dll", "novy");
        _scenario.FinishManifest();
        bool vanished = false;

        var (result, _) = _scenario.Run(afterCopy: _ =>
        {
            if (vanished) return;
            vanished = true;
            File.Delete(Path.Combine(_scenario.Server, "B.dll"));
        });

        Assert.Equal(UpdateEngine.ExitBadParamsOrAborted, result.ExitCode);
        Assert.False(File.Exists(Path.Combine(_scenario.Station, "A.dll")));
        Assert.False(File.Exists(Path.Combine(_scenario.Station, "B.dll")));
        Assert.False(Directory.Exists(Path.Combine(_scenario.Station, UpdateEngine.StagingDirName)));
    }

    [Fact]
    public void LongPathOver260Chars_Handled()
    {
        // Hluboko zanořené síťové cesty (\\server\...\Edison) reálně přesahují 260 znaků;
        // .NET 10 File API je umí a updater je nesmí rozbít
        string longName = new string('d', 240) + ".dll";
        try
        {
            _scenario.ServerFile(longName, "novy");
        }
        catch (IOException)
        {
            return; // stanice bez LongPathsEnabled - dlouhou cestu nevytvoří ani fixture
        }
        _scenario.FinishManifest();

        var (result, _) = _scenario.Run();

        Assert.Equal(UpdateEngine.ExitOk, result.ExitCode);
        Assert.Equal("novy", File.ReadAllText(Path.Combine(_scenario.Station, longName)));
    }
}
