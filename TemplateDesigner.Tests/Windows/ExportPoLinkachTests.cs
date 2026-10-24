using TemplateDesigner.Models;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Tisk s daty, „Jeden PDF na linku": jedno PDF za každou linku vybranou v seznamu a název souboru
/// z polí, která agenda opravdu posílá.
/// </summary>
public class ExportPoLinkachTests
{
    private static DataContext Stranka(string linka, Dictionary<string, object?> hodnoty) =>
        new DataContext(hodnoty).WithExtraValues(new Dictionary<string, object?> { [KontrolaVystupu.LinkaKlic] = linka });

    /// <summary>Dvě linky MHD se stejným licenčním číslem (tady prázdným) jsou dvě PDF, ne jedno.</summary>
    [Fact]
    public void JedenPdfNaLinku_DeliPodleVybraneLinky()
    {
        var stranky = new[]
        {
            Stranka("5 - Líšeň", new() { ["CISLO_LINKY"] = "", ["SMER"] = "T" }),
            Stranka("5 - Líšeň", new() { ["CISLO_LINKY"] = "", ["SMER"] = "Z" }),
            Stranka("7 - Bystrc", new() { ["CISLO_LINKY"] = "", ["SMER"] = "T" }),
        };

        var skupiny = PrintDialogViewModel.GroupForExport(stranky, ExportSplitMode.PerLine).ToList();

        Assert.Equal([2, 1], skupiny.Select(s => s.Count));
    }

    /// <summary>Linkový JŘ aktuální zastávku nemá - výchozí název ji nesmí chtít.</summary>
    [Fact]
    public void LinkovyJr_VychoziNazevJeCisloVychoziAKonecna()
    {
        var stranka = Stranka("380724 - Opava - Ostrava", new()
        {
            ["CISLO_LINKY"] = "380724", ["VYCHOZI_ZASTAVKA"] = "Opava", ["KONECNA_ZASTAVKA"] = "Ostrava",
        });

        var nazev = PrintDialogViewModel.BuildFileName(stranka, PrintDialogViewModel.NazevSouboruVzor(null, "LINE"), []);

        Assert.Equal("380724_Opava_Ostrava", nazev);
    }

    [Fact]
    public void ZastavkovyJr_VychoziNazevBezeZmeny()
    {
        var stranka = Stranka("380724 - Opava - Ostrava", new()
        {
            ["CISLO_LINKY"] = "380724", ["POPIS_ZASTAVKA"] = "Opava, nádraží", ["KONECNA_ZASTAVKA"] = "Ostrava",
        });

        var nazev = PrintDialogViewModel.BuildFileName(stranka, PrintDialogViewModel.NazevSouboruVzor("", "STOP"), []);

        Assert.Equal("380724_Opava, nádraží_Ostrava", nazev);
    }

    /// <summary>Dřívější výchozí název uložený beze změny u linkového JŘ nefungoval - platí nový.
    /// Vlastní vzor uživatele zůstává.</summary>
    [Theory]
    [InlineData("{CISLO_LINKY}_{POPIS_ZASTAVKA}_{KONECNA_ZASTAVKA}", "{CISLO_LINKY}_{VYCHOZI_ZASTAVKA}_{KONECNA_ZASTAVKA}")]
    [InlineData("JR_{CISLO_LINKY}", "JR_{CISLO_LINKY}")]
    public void UlozenyVzor(string ulozeny, string ocekavany) =>
        Assert.Equal(ocekavany, PrintDialogViewModel.NazevSouboruVzor(ulozeny, "LINE"));

    /// <summary>Agenda bez polí JŘ (Faktury): soubor se jmenuje podle vybrané instance, ne „CISLO_LINKY_…".</summary>
    [Fact]
    public void AgendaBezLinek_NazevPodleVybraneInstance()
    {
        var stranka = Stranka("2026/001 - Dopravní podnik", new() { ["CASTKA"] = "100" });

        var nazev = PrintDialogViewModel.BuildFileName(stranka, PrintDialogViewModel.NazevSouboruVzor(null, ""), []);

        Assert.Equal("2026_001 - Dopravní podnik", nazev);
    }
}
