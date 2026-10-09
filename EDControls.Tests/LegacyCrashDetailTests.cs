using System.IO;
using EDControls.ErrorReporting;

namespace EDControls.Tests;

/// <summary>
/// Soubor, kterým host legacy modulu předá shellu výjimku před pádem. Skutečné soubory v %TEMP%,
/// pid mimo rozsah běžných procesů, ať testy nesáhnou na soubor živého hosta.
/// </summary>
public class LegacyCrashDetailTests : IDisposable
{
    private readonly int _pid = Random.Shared.Next(1_000_000_000, int.MaxValue);

    private string WriteDetail(string text, DateTime? writtenUtc = null)
    {
        var path = LegacyCrashDetail.PathFor(_pid);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        if (writtenUtc is { } t) File.SetLastWriteTimeUtc(path, t);
        return path;
    }

    public void Dispose()
    {
        var path = LegacyCrashDetail.PathFor(_pid);
        if (File.Exists(path)) File.Delete(path);
    }

    [Fact]
    public void Take_WrittenAfterStart_ReturnsTextAndDeletesFile()
    {
        var started = DateTime.UtcNow.AddMinutes(-1);
        var path = WriteDetail("System.InvalidOperationException: boom");

        Assert.Equal("System.InvalidOperationException: boom", LegacyCrashDetail.Take(_pid, started));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Take_WrittenBeforeStart_IgnoresAndDeletesStaleFile()
    {
        // Pád dřívějšího procesu se stejným pid, který shell nepřečetl, nesmí skončit u cizího modulu.
        var started = DateTime.UtcNow;
        var path = WriteDetail("System.Exception: old", started.AddHours(-1));

        Assert.Null(LegacyCrashDetail.Take(_pid, started));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Take_FileLockedAgainstDelete_StillReturnsText()
    {
        // Antivir nebo zálohovač drží handle bez FILE_SHARE_DELETE: mazání selže, detail ne.
        WriteDetail("System.InvalidOperationException: boom");
        using (new FileStream(LegacyCrashDetail.PathFor(_pid), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            Assert.Equal("System.InvalidOperationException: boom", LegacyCrashDetail.Take(_pid, DateTime.UtcNow.AddMinutes(-1)));
    }

    [Fact]
    public void Take_NoFile_ReturnsNull()
    {
        Assert.Null(LegacyCrashDetail.Take(_pid, DateTime.UtcNow));
    }
}
