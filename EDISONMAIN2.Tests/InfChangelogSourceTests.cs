using System.IO;
using System.Text;
using EDISONMAIN2.Windows.ChangelogWindow;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Čte reálné soubory z temp adresářů (žádné mocky) - kryje mapování modul → INF klíč,
/// discovery .INF/_DIFF.INF, sniff kódování (UTF-8 BOM vs. ANSI CP1250) a hash change-setu.
/// </summary>
public class InfChangelogSourceTests : IDisposable
{
    private readonly DirectoryInfo _primary = Directory.CreateTempSubdirectory("inf-test-");
    private readonly DirectoryInfo _fallback = Directory.CreateTempSubdirectory("inf-test-fb-");

    public void Dispose()
    {
        _primary.Delete(recursive: true);
        _fallback.Delete(recursive: true);
    }

    private InfChangelogSource Source => new(_primary.FullName, _fallback.FullName);

    private void Write(DirectoryInfo dir, string file, string text) =>
        File.WriteAllText(Path.Combine(dir.FullName, file), text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

    // ---------- mapování modul → INF klíč (zrcadlo legacy clsConversion.ToInformationFile) ----------

    [Theory]
    // AD* catch-all (Left 2) - per-agenda ADSETTINGS.INF na disku legacy runtime nečetl
    [InlineData("ADSETTINGS.exe", "AD")]
    [InlineData("ADZPRACOVANI.exe", "AD")]
    [InlineData("ADCALCULATOR.exe", "AD")]
    // JR* + TISKMHD kolabují na JR
    [InlineData("JRMAIN.exe", "JR")]
    [InlineData("JRZAST.exe", "JR")]
    [InlineData("TISKMHD.exe", "JR")]
    // DS* a MHD*/MHD2*
    [InlineData("DSINPUT.exe", "DS")]
    [InlineData("DSVIEWER.exe", "DS")]
    [InlineData("MHD2PlanDopravy.exe", "MHD")]
    [InlineData("MHDLINKY.exe", "MHD")]
    // ND: NDMAIN je v legacy tabulce záměrně vynechaný (má vlastní NDMAIN.INF)
    [InlineData("NAKLADNIDOPRAVA.exe", "ND")]
    [InlineData("NDOBJEDNAVKY.exe", "ND")]
    [InlineData("NDMAIN.exe", "NDMAIN")]
    // explicitní aliasy
    [InlineData("POKLADNAEX.exe", "POKLADNA")]
    [InlineData("VOZIDLA2.exe", "VOZIDLA")]
    [InlineData("AUTOOPRAVNA2.exe", "AUTOOPRAVNA")]
    [InlineData("ZAKAZNICI2.exe", "ZAKAZNICI")]
    [InlineData("ZAUCTOVANIMzdy.exe", "ZAUCTOVANI")]
    [InlineData("DISPECINK2.exe", "DISPECINK")]
    [InlineData("ISYCiselniky.exe", "ISY")]
    // ISY balíky (mimo Ciselniky) mapují samy na sebe, jen uppercase
    [InlineData("ISYObehy.exe", "ISYOBEHY")]
    // default: vlastní jméno
    [InlineData("MZDY2.exe", "MZDY2")]
    [InlineData("USERS.exe", "USERS")]
    [InlineData("EDISONMAIN", "EDISONMAIN")]
    // plná cesta se normalizuje na basename
    [InlineData(@"C:\Edison\ADSETTINGS.exe", "AD")]
    public void InfKeyFor_MirrorsLegacyAliasTable(string moduleKey, string expected) =>
        Assert.Equal(expected, InfChangelogSource.InfKeyFor(moduleKey));

    // ---------- discovery ----------

    [Fact]
    public async Task Load_FullAndDiff_ReturnsBoth()
    {
        Write(_primary, "MZDY2.INF", "celá historie");
        Write(_primary, "MZDY2_DIFF.INF", "nové změny");

        var content = await Source.LoadAsync("MZDY2.exe");

        Assert.NotNull(content);
        Assert.Equal("MZDY2", content.ChangeSetKey);
        Assert.Equal("nové změny", content.ChangesText);
        Assert.Equal("celá historie", content.FullText);
        Assert.Equal(InfChangelogSource.ComputeChangeSetId("nové změny"), content.ChangeSetId);
    }

    [Fact]
    public async Task Load_FullOnly_NoChanges()
    {
        Write(_primary, "MZDY2.INF", "celá historie");

        var content = await Source.LoadAsync("MZDY2.exe");

        Assert.NotNull(content);
        Assert.Null(content.ChangesText);
        Assert.Equal("", content.ChangeSetId);
    }

    [Fact]
    public async Task Load_DiffOnly_FullFallsBackToDiff()
    {
        Write(_primary, "MZDY2_DIFF.INF", "nové změny");

        var content = await Source.LoadAsync("MZDY2.exe");

        Assert.NotNull(content);
        Assert.Equal("nové změny", content.ChangesText);
        Assert.Equal("nové změny", content.FullText);
    }

    [Fact]
    public async Task Load_NoFiles_ReturnsNull() =>
        Assert.Null(await Source.LoadAsync("MZDY2.exe"));

    [Fact]
    public async Task Load_WhitespaceDiff_CountsAsNoChanges()
    {
        Write(_primary, "MZDY2.INF", "celá historie");
        Write(_primary, "MZDY2_DIFF.INF", " \r\n \r\n");

        var content = await Source.LoadAsync("MZDY2.exe");

        Assert.NotNull(content);
        Assert.Null(content.ChangesText);
    }

    [Fact]
    public async Task Load_AliasedModule_ReadsSharedFile()
    {
        Write(_primary, "AD.INF", "AD historie");
        Write(_primary, "AD_DIFF.INF", "AD změny");

        var content = await Source.LoadAsync("ADSETTINGS.exe");

        Assert.NotNull(content);
        Assert.Equal("AD", content.ChangeSetKey);
        Assert.Equal("AD změny", content.ChangesText);
    }

    // ---------- pořadí adresářů ----------

    [Fact]
    public async Task Load_FirstDirWins()
    {
        Write(_primary, "MZDY2.INF", "lokální");
        Write(_fallback, "MZDY2.INF", "deploy");

        var content = await Source.LoadAsync("MZDY2.exe");

        Assert.Equal("lokální", content!.FullText);
    }

    [Fact]
    public async Task Load_DiffFromOtherDirIsIgnored()
    {
        // DIFF se bere jen z adresáře, kde se našel full - mix dev kopie a produkčního diffu by lhal.
        Write(_primary, "MZDY2.INF", "lokální");
        Write(_fallback, "MZDY2_DIFF.INF", "deploy změny");

        var content = await Source.LoadAsync("MZDY2.exe");

        Assert.NotNull(content);
        Assert.Null(content.ChangesText);
    }

    // ---------- kódování ----------

    [Fact]
    public async Task Load_Utf8Bom_RoundTripsDiacritics()
    {
        Write(_primary, "MZDY2.INF", "Příliš žluťoučký kůň");

        var content = await Source.LoadAsync("MZDY2.exe");

        Assert.Equal("Příliš žluťoučký kůň", content!.FullText);
    }

    [Fact]
    public async Task Load_AnsiCp1250WithoutBom_RoundTripsDiacritics()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var text = "Příliš žluťoučký kůň";
        File.WriteAllBytes(Path.Combine(_primary.FullName, "MZDY2.INF"),
            Encoding.GetEncoding(1250).GetBytes(text));

        var content = await Source.LoadAsync("MZDY2.exe");

        Assert.Equal(text, content!.FullText);
    }

    // ---------- hash change-setu ----------

    [Fact]
    public void ComputeChangeSetId_DeterministicAndContentSensitive()
    {
        var a1 = InfChangelogSource.ComputeChangeSetId("změny A");
        var a2 = InfChangelogSource.ComputeChangeSetId("změny A");
        var b = InfChangelogSource.ComputeChangeSetId("změny B");

        Assert.Equal(a1, a2);
        Assert.NotEqual(a1, b);
    }
}
