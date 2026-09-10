using System.IO;
using EDUPDATE2;

namespace EDUPDATE2.Tests;

/// <summary>Invariant z hlavičky UpdateLog: zápis (ani rotace) nesmí nikdy shodit update.</summary>
public class UpdateLogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private string LogPath => Path.Combine(_dir, UpdateLog.FileName);
    private string BakPath => Path.ChangeExtension(LogPath, ".bak");

    public UpdateLogTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Ctor_RotatesExistingLogToBak()
    {
        File.WriteAllText(LogPath, "minulý běh");

        var log = new UpdateLog(_dir);
        log.Write("UPDATER", "nový běh");

        Assert.Equal("minulý běh", File.ReadAllText(BakPath));
        Assert.Contains("nový běh", File.ReadAllText(LogPath));
        Assert.DoesNotContain("minulý běh", File.ReadAllText(LogPath));
    }

    [Fact]
    public void Ctor_OverwritesOlderBak()
    {
        File.WriteAllText(BakPath, "předminulý běh");
        File.WriteAllText(LogPath, "minulý běh");

        _ = new UpdateLog(_dir);

        Assert.Equal("minulý běh", File.ReadAllText(BakPath));
    }

    [Fact]
    public void Ctor_LockedLogFile_DoesNotThrow()
    {
        File.WriteAllText(LogPath, "zamčený");
        using var @lock = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.None);

        _ = new UpdateLog(_dir); // rotace selže, ctor to spolkne
    }

    [Fact]
    public void Write_MissingDirectory_DoesNotThrow()
    {
        var log = new UpdateLog(Path.Combine(_dir, "neexistuje"));

        log.Write("UPDATER", "do neexistujícího adresáře");
    }

    [Fact]
    public void Write_LockedFile_DoesNotThrow()
    {
        var log = new UpdateLog(_dir);
        log.Write("UPDATER", "první");
        using var @lock = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.None);

        log.Write("UPDATER", "druhý"); // append selže, spolkne se
    }

    [Fact]
    public void Write_AppendsTimestampedLine_FilePathInTargetDir()
    {
        var log = new UpdateLog(_dir);

        log.Write("Fáze", "zpráva");

        Assert.Equal(LogPath, log.FilePath);
        Assert.Contains("\tFáze\tzpráva", File.ReadAllText(LogPath));
    }
}
