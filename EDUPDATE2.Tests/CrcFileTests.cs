using System.IO;
using System.Text;
using EDUPDATE2;

namespace EDUPDATE2.Tests;

public class CrcFileTests
{
    // Reálná první věta produkčního EDISONCRC.bin - jistí bit-kompatibilitu s legacy producentem
    // (EDISONAPI\clsUpdater.vb), ne jen konzistenci vlastní inverze.
    private const string GoldenHeader = "00120";
    private const string GoldenBase64 =
        "SMr39blJC8a2ioZrXJj/gU1qSXCt0lg1X1121801yxWYjuYDD3LRtQNMPlYELBHTTSif5mEpYtADjEHRZafkhDLOQrSnQxGnDJ63OFqge/BzJ/YTFeF//w==";

    [Fact]
    public void Read_GoldenVector_DecodesLegacyRecord()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(GoldenHeader + GoldenBase64));

        var records = CrcFile.Read(stream);

        var record = Assert.Single(records);
        Assert.Equal("EDTRANSFER.exe", record.Filename);
        Assert.Equal("", record.SubPath);
        Assert.Equal("4A783EF6BB80736850BF2D12C5CD53DBAA53E1A05C23CCCEEBC04180630978D7", record.Crc);
    }

    [Fact]
    public void WriteRead_Roundtrip_PreservesRecordsIncludingTrailingSpace()
    {
        var records = new[]
        {
            new CrcRecord("A.dll", "", new string('A', 64)),
            // Trailing space ve jménu je legální - v produkčním CRC reálně existuje
            new CrcRecord("B.dll ", "EDFiles", new string('B', 64)),
            new CrcRecord("c.ps", "PS", new string('C', 64)),
        };
        using var stream = new MemoryStream();
        CrcFile.Write(stream, records);
        stream.Position = 0;

        var read = CrcFile.Read(stream);

        Assert.Equal(records, read);
    }

    [Fact]
    public void Read_SwappedRecordOrder_Throws_KeyIsIndexBased()
    {
        using var stream = new MemoryStream();
        CrcFile.Write(stream, new[]
        {
            new CrcRecord("A.dll", "", new string('A', 64)),
            new CrcRecord("B.dll", "", new string('B', 64)),
        });
        var bytes = stream.ToArray();
        int firstLength = 5 + int.Parse(Encoding.ASCII.GetString(bytes, 0, 5));
        var swapped = bytes[firstLength..].Concat(bytes[..firstLength]).ToArray();

        Assert.Throws<InvalidDataException>(() => CrcFile.Read(new MemoryStream(swapped)));
    }

    [Fact]
    public void Read_NonNumericHeader_Throws()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes("ABCDEwhatever"));
        Assert.Throws<InvalidDataException>(() => CrcFile.Read(stream));
    }

    [Fact]
    public void Read_TruncatedPayload_Throws()
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(GoldenHeader + GoldenBase64[..30]));
        Assert.Throws<InvalidDataException>(() => CrcFile.Read(stream));
    }

    [Fact]
    public void Read_PathTraversalSubPath_Throws()
    {
        using var stream = new MemoryStream();
        CrcFile.Write(stream, new[] { new CrcRecord("A.dll", @"..\..\Windows", new string('A', 64)) });
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => CrcFile.Read(stream));
    }

    [Theory]
    [InlineData(@"..\evil.dll")]
    [InlineData(@"C:\evil.dll")]
    [InlineData("evil.dll:ads")]
    [InlineData("..")]
    [InlineData(".. ")]
    public void Read_DangerousFilename_Throws(string filename)
    {
        using var stream = new MemoryStream();
        CrcFile.Write(stream, new[] { new CrcRecord(filename, "", new string('A', 64)) });
        stream.Position = 0;

        Assert.Throws<InvalidDataException>(() => CrcFile.Read(stream));
    }

    [Fact]
    public void Read_EmptyStream_ReturnsEmptyList()
    {
        Assert.Empty(CrcFile.Read(new MemoryStream()));
    }
}
