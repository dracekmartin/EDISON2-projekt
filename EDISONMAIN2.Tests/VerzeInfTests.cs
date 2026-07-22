using System.IO;

namespace EDISONMAIN2.Tests;

public class VerzeInfTests
{
    // ---------- ParseLast ----------

    [Fact]
    public void ParseLast_SingleKey_ReturnsValue()
    {
        Assert.Equal("20260526.1.0", VerzeInf.ParseLast("LAST=20260526.1.0"));
    }

    [Fact]
    public void ParseLast_MultilineWithOtherKeys_ExtractsLast()
    {
        Assert.Equal("20260526.1.0", VerzeInf.ParseLast("TYPE=1\nLAST=20260526.1.0\nV=2.01"));
    }

    [Fact]
    public void ParseLast_WindowsCRLF_StripsCR()
    {
        Assert.Equal("1.2.3", VerzeInf.ParseLast("LAST=1.2.3\r\nBlokovatAktualizace=0"));
    }

    [Fact]
    public void ParseLast_NoLastKey_ReturnsEmpty()
    {
        Assert.Equal("", VerzeInf.ParseLast("TYPE=1\nV=2.01"));
    }

    [Fact]
    public void ParseLast_EmptyContent_ReturnsEmpty()
    {
        Assert.Equal("", VerzeInf.ParseLast(""));
    }

    // ---------- ParseValue ----------

    [Fact]
    public void ParseValue_ArbitraryKey_ReturnsValueOrEmpty()
    {
        var content = "LAST=1.2.3\r\nBlokovatAktualizace=1";
        Assert.Equal("1", VerzeInf.ParseValue(content, VerzeInf.BlockKey));
        Assert.Equal("", VerzeInf.ParseValue(content, "TYPE"));
    }

    // ---------- ReadVersion ----------

    [Fact]
    public void ReadVersion_MissingFile_ReturnsEmpty()
    {
        Assert.Equal("", VerzeInf.ReadVersion(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())));
    }

    [Fact]
    public void ReadVersion_ExistingFile_ReturnsLast()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, VerzeInf.FileName), "LAST=20260601.2\r\nTYPE=1");
            Assert.Equal("20260601.2", VerzeInf.ReadVersion(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // ---------- IsNewVersion ----------

    [Theory]
    [InlineData("20260601.0.0", "20260526.1.0", true)]   // novější datum
    [InlineData("20260526.1.0", "20260601.0.0", false)]  // starší datum
    [InlineData("20260526.1.0", "20260526.1.0", false)]  // stejné
    [InlineData("20260526.2", "20260526.1.5", true)]     // vyšší full segment rozhodne dřív než patch
    [InlineData("1.2.3", "1.2", true)]                   // nová má víc segmentů při shodě společných
    [InlineData("1.2", "1.2.3", false)]                  // stará má víc segmentů — ne-newer
    public void IsNewVersion_Cases(string newVer, string oldVer, bool expected)
    {
        Assert.Equal(expected, VerzeInf.IsNewVersion(newVer, oldVer));
    }

    [Fact]
    public void IsNewVersion_NonNumericSegments_TreatedAsZero()
    {
        // Legacy Val() sémantika: garbage = 0, porovnání padá na další segment.
        Assert.False(VerzeInf.IsNewVersion("abc.0", "xyz.0"));
        Assert.True(VerzeInf.IsNewVersion("abc.1", "xyz.0"));
    }

    // ---------- IsSameVersion ----------

    [Theory]
    [InlineData("20260526.1.0", "20260526.1.0", true)]
    [InlineData("20260526.1.0", "20260526.1.1", false)]
    [InlineData("20260526.1", "20260526.1.0", false)]    // rozdílný počet segmentů = ne-same
    [InlineData("", "", true)]
    public void IsSameVersion_Cases(string a, string b, bool expected)
    {
        Assert.Equal(expected, VerzeInf.IsSameVersion(a, b));
    }
}
