using EDISONMAIN2.Windows.EdisonMainWindow.Modules;
using Xunit;

namespace EDISONMAIN2.Tests;

public class AgendaSpritesTests
{
    // Strom nese exe s příponou - mapa musí chytit obě podoby i jiný casing.
    [Theory]
    [InlineData("MZDY2.exe")]
    [InlineData("MZDY2")]
    [InlineData("mzdy2.EXE")]
    [InlineData("ZauMzdy.exe")]
    [InlineData("ESDOKLADY.exe")]
    [InlineData("POKLADNA.exe")]
    public void ForExe_FinancialAgenda_ReturnsMoney(string exe) =>
        Assert.Equal("Money", AgendaSprites.ForExe(exe));

    [Theory]
    [InlineData("ISYJR2.exe")]
    [InlineData("")]
    public void ForExe_UnknownAgenda_ReturnsNull(string exe) =>
        Assert.Null(AgendaSprites.ForExe(exe));
}
