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

    [Fact]
    public void AllSets_DistinctAndAllLabeled()
    {
        Assert.Equal(AgendaSprites.AllSets.Distinct(), AgendaSprites.AllSets);
        foreach (var set in AgendaSprites.AllSets)
            Assert.False(string.IsNullOrEmpty(AgendaSprites.LabelFor(set)));
    }

    // Nabídka Test funkcí musí nést obě sady. Dřív se skládala z distinct hodnot mapy agend,
    // a protože ta mapuje všechno na Money, „Dopravní prostředky" se do ní nikdy nedostaly.
    [Fact]
    public void AllSets_CoversBothSpriteSets()
    {
        Assert.Equal(["Vehicles", "Money"], AgendaSprites.AllSets);
        Assert.Equal("Dopravní prostředky", AgendaSprites.LabelFor("Vehicles"));
    }

    [Fact]
    public void LabelFor_Money_UnknownFallsBackToKey()
    {
        Assert.Equal("Peníze", AgendaSprites.LabelFor("Money"));
        Assert.Equal("NEZNAMA", AgendaSprites.LabelFor("NEZNAMA"));
    }
}
