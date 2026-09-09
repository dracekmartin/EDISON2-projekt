using System.IO;
using EDUPDATE2;

namespace EDUPDATE2.Tests;

public class ForcedStampTests
{
    [Fact]
    public void WriteRead_Roundtrip()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            ForcedStamp.Write(dir, "66234,60449");
            Assert.Equal("66234,60449", ForcedStamp.Read(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Read_MissingFile_ReturnsEmpty()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Assert.Equal("", ForcedStamp.Read(dir));
    }

    [Fact]
    public void Read_CorruptedContent_ReturnsEmptyWithoutThrowing()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, ForcedStamp.FileName), "rozbitý řádek bez klíče\n\n=\n");
            Assert.Equal("", ForcedStamp.Read(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
