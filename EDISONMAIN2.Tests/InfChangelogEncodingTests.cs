using System.IO;
using System.Text;
using EDISONMAIN2.Windows.ChangelogWindow;

namespace EDISONMAIN2.Tests;

/// <summary>
/// INF soubory roznáší legacy instalátor v ANSI (CP1250, bez BOM); novější nástroje
/// mohou zapsat UTF-8 s BOM. ReadInfText musí přečíst obojí bez rozbité diakritiky.
/// </summary>
public class InfChangelogEncodingTests : IDisposable
{
    private const string Text = "Změněno: žluťoučký kůň číslo 5";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public InfChangelogEncodingTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void ReadInfText_LegacyCp1250WithoutBom_DecodesDiacritics()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var path = Path.Combine(_dir, "AD_DIFF.INF");
        File.WriteAllBytes(path, Encoding.GetEncoding(1250).GetBytes(Text));

        Assert.Equal(Text, InfChangelogSource.ReadInfText(path));
    }

    [Fact]
    public void ReadInfText_Utf8WithBom_BomDetectionWins()
    {
        var path = Path.Combine(_dir, "AD.INF");
        File.WriteAllText(path, Text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        Assert.Equal(Text, InfChangelogSource.ReadInfText(path));
    }
}
