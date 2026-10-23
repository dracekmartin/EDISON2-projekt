using TemplateDesigner.Config;

namespace TemplateDesigner.Tests.Services;

/// <summary>
/// Vstup ze spuštění z EDISONu (pole <c>inputParam</c> launch kontraktu). Producent je v jiném repu
/// (<c>ISYJR2/TiskJRHelper.cs</c> v EDISON), proto se tu kontroluje přesně ten řetězec, který posílá.
/// </summary>
public class LaunchInputTests
{
    [Fact]
    public void JenAgenda_JakoDosud()
    {
        var input = LaunchInput.Parse("Agenda=ZASTAVKOVY_JR_AD");

        Assert.Equal("ZASTAVKOVY_JR_AD", input.AgendaCode);
        Assert.Empty(input.Values);
    }

    [Fact]
    public void PorovnaniLinekZModuluJr()
    {
        // Tvar z TiskJRHelper.OpenRozdilovyJR: označená linka je nová verze, z dialogu původní.
        var input = LaunchInput.Parse(
            "Agenda=LINKOVY_JR_AD_ROZDILOVY;SKUPINA_LINEK_ID=307;LINKA_ID=0380760;POROVNAT_SKUPINU=207;POROVNAT_LINKU=0380760;Vystup=PDF");

        Assert.Equal("LINKOVY_JR_AD_ROZDILOVY", input.AgendaCode);
        Assert.Equal("307", input.Get(LaunchInput.SkupinaKey));
        Assert.Equal("0380760", input.Get("LINKA_ID"));
        Assert.Equal("207", input.Get("POROVNAT_SKUPINU"));
        Assert.Equal("0380760", input.Get("POROVNAT_LINKU"));
        Assert.True(input.WantsPdf);
    }

    [Theory]
    [InlineData("Agenda=X;LINKA_ID=1", false)]
    [InlineData("Agenda=X;LINKA_ID=1;Vystup=pdf", true)]
    [InlineData("Agenda=X;LINKA_ID=1;Vystup=PNG", false)]
    public void PdfJenNaVyzadani(string inputParam, bool pdf) =>
        Assert.Equal(pdf, LaunchInput.Parse(inputParam).WantsPdf);

    [Fact]
    public void PrazdnaHodnotaZustane_ChybejiciJeNull()
    {
        var input = LaunchInput.Parse("Agenda=X; POROVNAT_SKUPINU = ;LINKA_ID=1");

        Assert.Equal("", input.Get("POROVNAT_SKUPINU"));
        Assert.Equal("1", input.Get("linka_id"));
        Assert.Null(input.Get("POROVNAT_LINKU"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nesmysl")]
    [InlineData("LINKA_ID=1")]
    [InlineData("Agenda=")]
    public void BezAgendy_NicNeplati(string? inputParam)
    {
        var input = LaunchInput.Parse(inputParam);

        Assert.Null(input.AgendaCode);
        Assert.Empty(input.Values);
    }
}
