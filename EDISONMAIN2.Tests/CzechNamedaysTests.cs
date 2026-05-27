using EDISONMAIN2.Windows.EdisonMainWindow;

namespace EDISONMAIN2.Tests;

public class CzechNamedaysTests
{
    [Theory]
    [InlineData(2026, 3, 19, "Josef")]
    [InlineData(2026, 5, 5, "Klaudie")]
    [InlineData(2026, 6, 24, "Jan")]
    [InlineData(2026, 7, 5, "Cyril a Metoděj")]
    [InlineData(2026, 12, 31, "Silvestr")]
    [InlineData(2026, 12, 24, "Adam a Eva")]
    public void GetByDate_KnownDay_ReturnsName(int year, int month, int day, string expected)
    {
        var date = new DateOnly(year, month, day);
        Assert.Equal(expected, CzechNamedays.GetByDate(date));
    }

    [Theory]
    [InlineData(2026, 1, 1)]   // Nový rok — státní svátek, bez nameday entry
    [InlineData(2026, 5, 1)]   // Svátek práce
    [InlineData(2026, 12, 25)] // 1. svátek vánoční
    public void GetByDate_StateHoliday_ReturnsNull(int year, int month, int day)
    {
        var date = new DateOnly(year, month, day);
        Assert.Null(CzechNamedays.GetByDate(date));
    }

    [Fact]
    public void GetByDate_LeapDay_ReturnsName()
    {
        // 29.2 je platné jen v přestupném roce; pole února má 29 záznamů, takže se trefí.
        var date = new DateOnly(2024, 2, 29);
        Assert.Equal("Horymír", CzechNamedays.GetByDate(date));
    }

    [Fact]
    public void BuildInformationText_WithNameday_IncludesSvatekMaSuffix()
    {
        var text = EDISONMAIN2.Windows.EdisonMainWindow.EdisonMainViewModel.BuildInformationText(
            new DateOnly(2026, 3, 19));
        Assert.Equal("Dnes je 19.03.2026, svátek má Josef", text);
    }

    [Fact]
    public void BuildInformationText_StateHoliday_DateOnly()
    {
        // Bez nameday → suffix se neuvádí, jen "Dnes je DD.MM.YYYY".
        var text = EDISONMAIN2.Windows.EdisonMainWindow.EdisonMainViewModel.BuildInformationText(
            new DateOnly(2026, 1, 1));
        Assert.Equal("Dnes je 01.01.2026", text);
    }
}
