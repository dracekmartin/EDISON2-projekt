using System.IO;

namespace EDControls.Tests;

/// <summary>Em dash je v EDISONMAIN2/EDControls zakázaný (root CLAUDE.md, „Typografie a
/// komentáře"). Vlastní zdroják skenuje taky - proto je znak níž jen jako escape.</summary>
public class TypographyGuardTests
{
    private const char EmDash = '\u2014';

    [Fact]
    public void ZdrojeNeobsahujiEmDash()
    {
        var root = RepoRoot();
        var hits = Sources(root)
            .SelectMany(f => File.ReadAllLines(f)
                .Select((line, i) => (line, no: i + 1))
                .Where(x => x.line.Contains(EmDash))
                .Select(x => $"{Path.GetRelativePath(root.FullName, f)}:{x.no}"))
            .ToList();

        Assert.True(hits.Count == 0,
            $"Em dash (U+2014) nahraď běžnou pomlčkou:{Environment.NewLine}{string.Join(Environment.NewLine, hits)}");
    }

    private static DirectoryInfo RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EDISON2.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!;
    }

    /// <summary>Jen EDISONMAIN2* a EDControls* - TemplateDesigner drží em dash záměrně jako
    /// zakázaný token ve svých drift testech a EDTesting je legacy framework.</summary>
    private static List<string> Sources(DirectoryInfo root)
    {
        var files = new[] { "EDISONMAIN2*", "EDControls*" }
            .SelectMany(pattern => root.GetDirectories(pattern))
            .SelectMany(dir => new[] { "*.cs", "*.xaml", "*.cls" }
                .SelectMany(ext => dir.GetFiles(ext, SearchOption.AllDirectories)))
            .Where(f => !f.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Select(f => f.FullName)
            .ToList();

        Assert.NotEmpty(files);   // prázdno = špatná cesta, ne že je vše v pořádku
        return files;
    }
}
