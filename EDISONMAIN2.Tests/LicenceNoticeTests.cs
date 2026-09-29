using ALVA.LICENCE.EDLicence;
using EDTesting;
using NSubstitute;

namespace EDISONMAIN2.Tests;

/// <summary>Upozornění nástěnky starého shellu (legacy <c>frmMain.vb:813-906</c>), texty doslovně.</summary>
public class LicenceNoticeTests
{
    private static readonly DateTime Today = new(2026, 9, 29);
    private const string CurrentVersion = "260920.1";

    private static Lic ValidTo(DateTime date, int countsRz = 0, int countsZam = 0) =>
        new() { ValidityTo = date.ToString("yyyy-MM-dd HH:mm:ss"), CountsRZ = countsRz, CountsZAM = countsZam };

    [Theory]
    [InlineData(0, "Licence skončí dnes", "Licence EDISON skončí dnes.")]
    [InlineData(1, "Licence skončí za 1 den", "Licence EDISON skončí za 1 den.")]
    [InlineData(2, "Licence skončí za 2 dny", "Licence EDISON skončí za 2 dny.")]
    [InlineData(4, "Licence skončí za 4 dny", "Licence EDISON skončí za 4 dny.")]
    [InlineData(5, "Licence skončí za 5 dnů", "Licence EDISON skončí za 5 dnů.")]
    [InlineData(7, "Licence skončí za 7 dnů", "Licence EDISON skončí za 7 dnů.")]
    public void EndingWithinSevenDays_Warns(int daysLeft, string expectedShort, string expectedFull)
    {
        var notice = LicenceNotice.Build(ValidTo(Today.AddDays(daysLeft)), Today, CurrentVersion, null, null);

        Assert.Equal(new LicenceNotice.Notice(expectedShort, expectedFull), notice);
    }

    [Fact]
    public void EndingInEightDays_NoNotice()
    {
        Assert.Null(LicenceNotice.Build(ValidTo(Today.AddDays(8)), Today, CurrentVersion, null, null));
    }

    [Fact]
    public void Ended_StopsFurtherChecks()
    {
        // Legacy „dalsi info neresime": po konci licence se počty už nehlásí.
        var notice = LicenceNotice.Build(ValidTo(Today.AddDays(-1), countsRz: 5), Today, CurrentVersion, vehicles: 50, null);

        Assert.Equal(new LicenceNotice.Notice("Licence skončila", "Licence EDISON skončila, není možné vkládat data."), notice);
    }

    [Fact]
    public void CzechDateFormat_Parsed()
    {
        var lic = new Lic { ValidityTo = "2.10.2026 0:00:00" };

        Assert.Equal("Licence skončí za 3 dny", LicenceNotice.Build(lic, Today, CurrentVersion, null, null)?.Short);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("neplatné datum")]
    public void UnparsableValidity_NoNotice(string? validityTo)
    {
        Assert.Null(LicenceNotice.Build(new Lic { ValidityTo = validityTo! }, Today, CurrentVersion, null, null));
    }

    [Fact]
    public void OldVersion_OnlyVersionNotice()
    {
        // Legacy: stará verze přebije vše ostatní, i skončenou licenci.
        // 29. 7. = přesně dva měsíce: legacy srovnává s Now včetně času, takže už je stará.
        var notice = LicenceNotice.Build(ValidTo(Today.AddDays(-5)), Today, "260729.3", null, null);

        Assert.Equal(new LicenceNotice.Notice("Stará verze EDISON",
            "Verze EDISON je příliš stará, funkčnost může být omezena, proveďte aktualizaci."), notice);
    }

    [Theory]
    [InlineData("260730.1")]            // den před hranicí dvou měsíců
    [InlineData(VerzeInf.DevVersion)]   // DEBUG build
    [InlineData("")]                    // chybí VERZE.INF
    [InlineData("nejaka.verze")]
    public void VersionNotOld_NoNotice(string version)
    {
        Assert.Null(LicenceNotice.Build(null, Today, version, null, null));
    }

    [Fact]
    public void VehiclesOverLicence_Warns()
    {
        var notice = LicenceNotice.Build(ValidTo(Today.AddYears(1), countsRz: 10), Today, CurrentVersion, vehicles: 11, employees: null);

        Assert.Equal(new LicenceNotice.Notice("Překročen počet vozidel",
            "Je překročen počet aktivních VOZIDEL oproti licenci, funkčnost EDISON může být omezena."), notice);
    }

    [Fact]
    public void EmployeesOverLicence_Warns()
    {
        var notice = LicenceNotice.Build(ValidTo(Today.AddYears(1), countsZam: 10), Today, CurrentVersion, null, employees: 11);

        Assert.Equal(new LicenceNotice.Notice("Překročen počet zaměstnanců",
            "Je překročen počet aktivních ZAMĚSTNANCŮ oproti licenci, funkčnost EDISON může být omezena."), notice);
    }

    [Fact]
    public void BothCountsOver_SingleCombinedSentence()
    {
        var notice = LicenceNotice.Build(ValidTo(Today.AddYears(1), countsRz: 10, countsZam: 10), Today, CurrentVersion, 11, 11);

        Assert.Equal(new LicenceNotice.Notice("Překročen počet vozidel a zaměstnanců",
            "Je překročen počet aktivních VOZIDEL a ZAMĚSTNANCŮ oproti licenci, funkčnost EDISON může být omezena."), notice);
    }

    [Theory]
    [InlineData(10, 10)]     // rovnost není překročení
    [InlineData(0, 500)]     // licence počet neomezuje - legacy kontrolu přeskočí
    public void CountsNotOver_NoNotice(int countsRz, int vehicles)
    {
        Assert.Null(LicenceNotice.Build(ValidTo(Today.AddYears(1), countsRz: countsRz), Today, CurrentVersion, vehicles, null));
    }

    [Fact]
    public void CountUnknown_NoNotice()
    {
        // Balík VOZIDLA u zákazníka chybí → počet null, varování se nevymýšlí.
        Assert.Null(LicenceNotice.Build(ValidTo(Today.AddYears(1), countsRz: 10), Today, CurrentVersion, null, null));
    }

    [Fact]
    public void EndingAndCountsOver_BothSentences_ChipShowsEnding()
    {
        // Odchylka od legacy (ten větu o konci licence přepsal textem o počtech): obě věty v tooltipu.
        var notice = LicenceNotice.Build(ValidTo(Today.AddDays(3), countsRz: 10), Today, CurrentVersion, 11, null);

        Assert.Equal("Licence skončí za 3 dny", notice?.Short);
        Assert.Equal("Licence EDISON skončí za 3 dny. Je překročen počet aktivních VOZIDEL oproti licenci, funkčnost EDISON může být omezena.",
                     notice?.Full);
    }

    [Fact]
    public void ParseNamedCount_FindsKeyedRow_SkipsErrSlot()
    {
        const char D = '~';
        var raw = new object[] { $"celkem{D}99", $"dta{D}A{D}3", $"celkem{D}12" };

        Assert.Equal(12, LicenceNotice.ParseNamedCount(raw, D, "celkem"));
        Assert.Null(LicenceNotice.ParseNamedCount(raw, D, "PocZam"));
        Assert.Null(LicenceNotice.ParseNamedCount(new object[] { "", $"PocZam{D}x" }, D, "PocZam"));
    }

    [Fact]
    public void LoadVehicleCount_SendsIdfaAndParsesTotal()
    {
        var mlRest = MLRestMocks.Create(idfa: "CB_DP");
        var d = mlRest.Delimiter;
        mlRest.QueryOrThrow("ALVA.VOZIDLA.LCC", "PocetProvozovanychVozidel", Arg.Any<object[]>())
              .Returns(MLRestMocks.Output($"celkem{d}42", $"dta{d}BUS{d}40"));

        Assert.Equal(42, LicenceNotice.LoadVehicleCount(mlRest));
        mlRest.Received(1).QueryOrThrow("ALVA.VOZIDLA.LCC", "PocetProvozovanychVozidel",
            Arg.Is<object[]>(a => a.Length == 1 && (string)a[0] == $"pro{d}CB_DP"));
    }

    [Fact]
    public void LoadEmployeeCount_ServerError_Null()
    {
        // Balík MZDY2 u zákazníka chybí (třída neexistuje) - legacy chybu spolkl.
        var mlRest = MLRestMocks.Create();
        mlRest.QueryOrThrow("ALVA.MZDY2.MLAPI", "gPocZam", Arg.Any<object[]>())
              .Returns(_ => throw new InvalidOperationException("CLASS DOES NOT EXIST"));

        Assert.Null(LicenceNotice.LoadEmployeeCount(mlRest));
    }
}
