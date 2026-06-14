using System.IO;
using EDControls;

namespace EDControls.Tests;

public class AssemblyReferencesTests
{
    // EDControls.Tests.dll má ProjectReference na EDControls → AssemblyRef "EDControls" je jistý.
    private static string ThisAssembly => typeof(AssemblyReferencesTests).Assembly.Location;

    [Fact]
    public void References_ReferencedAssembly_True()
    {
        Assert.True(AssemblyReferences.References(ThisAssembly, "EDControls"));
    }

    [Fact]
    public void References_CaseInsensitive_True()
    {
        Assert.True(AssemblyReferences.References(ThisAssembly, "edcontrols"));
    }

    [Fact]
    public void References_NotReferencedAssembly_False()
    {
        Assert.False(AssemblyReferences.References(ThisAssembly, "ThisAssemblyIsNotReferenced__"));
    }

    [Fact]
    public void References_MissingFile_False()
    {
        Assert.False(AssemblyReferences.References(@"Z:\nonexistent\missing.dll", "EDControls"));
    }

    [Fact]
    public void References_NonPeFile_False()
    {
        var tmp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".dll");
        File.WriteAllText(tmp, "tohle není PE soubor");
        try
        {
            Assert.False(AssemblyReferences.References(tmp, "EDControls"));
        }
        finally
        {
            File.Delete(tmp);
        }
    }
}
