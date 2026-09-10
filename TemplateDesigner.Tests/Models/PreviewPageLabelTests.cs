using TemplateDesigner;
using TemplateDesigner.Models;
using Xunit;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// Popisky položek v nabídce nad náhledem - podle toho, co na stránce dat doopravdy je.
/// </summary>
/// <remarks>
/// ⚠️ **Popisek pojmenovává, co nabídka přepíná** (`B131`, nález Jan 7. 9.). Nabídka jede po
/// **stránkách dat** (u vývěsky zastávka × směr, u linky směr), kdežto **strany sazby** té jedné
/// položky leží pod sebou ve scrollu. Do 10. 9. se obojí jmenovalo „stránka" a čítač „/ 2" se četl
/// jako celkový počet stran - uživatel pak hledal zpětné spoje a měl je za nevytištěné, protože
/// byly na druhé stránce dat.
///
/// <para>U <b>linkového</b> JŘ to do 31. 8. hlásilo „Stránka N" u každé stránky (`L13`): popisek se
/// stavěl jen z <c>POPIS_ZASTAVKA</c>, které linkové agendy ve schématu nemají. Zastávkový JŘ se
/// těmi opravami nesmí hnout - jeho tvar je odzkoušený.</para>
/// </remarks>
public class PreviewPageLabelTests
{
    private static DataContext Page(params (string Key, object? Value)[] values) =>
        new(values.ToDictionary(v => v.Key, v => v.Value));

    [Fact]
    public void ZastavkaSeSmerem_VratiCisloZastavkuASmer()
    {
        var page = Page(("POPIS_ZASTAVKA", "Malše, točna"), ("KONECNA_ZASTAVKA", "Nádraží"));
        Assert.Equal("Zastávka 3 z 47: Malše, točna → Nádraží",
            TemplateDesignerViewModel.BuildPreviewPageLabel(page, 2, 47));
    }

    [Fact]
    public void ZastavkaBezSmeru_VynechaSipku()
    {
        var page = Page(("POPIS_ZASTAVKA", "Malše, točna"), ("KONECNA_ZASTAVKA", ""));
        Assert.Equal("Zastávka 1 z 12: Malše, točna",
            TemplateDesignerViewModel.BuildPreviewPageLabel(page, 0, 12));
    }

    [Fact]
    public void AgendaBezZastavek_ObecnaCast()
    {
        var page = Page(("CISLO_FAKTURY", "2026001"));
        Assert.Equal("Část 5 z 8", TemplateDesignerViewModel.BuildPreviewPageLabel(page, 4, 8));
    }

    // ── linkový JŘ ────────────────────────────────────────────────────────────

    /// <summary>
    /// Směr se jmenuje, ne čísluje.
    /// </summary>
    /// <remarks>
    /// „Směr tam" / „Směr zpět" je zavedené názvosloví a uživatel z něj pozná, na co kouká;
    /// „Směr 1 z 2" mu neřekne nic (rozhodnutí Jan 10. 9.). Pole <c>SMER</c> nese T/Z.
    /// </remarks>
    [Fact]
    public void LinkaTam_JmenujeSmerSlovem()
    {
        var page = Page(("VYCHOZI_ZASTAVKA", "Praha"), ("KONECNA_ZASTAVKA", "Sedlčany"), ("SMER", "T"));
        Assert.Equal("Směr tam: Praha → Sedlčany",
            TemplateDesignerViewModel.BuildPreviewPageLabel(page, 0, 2));
    }

    [Fact]
    public void LinkaZpet_JmenujeSmerSlovem()
    {
        var page = Page(("VYCHOZI_ZASTAVKA", "Sedlčany"), ("KONECNA_ZASTAVKA", "Praha"), ("SMER", "Z"));
        Assert.Equal("Směr zpět: Sedlčany → Praha",
            TemplateDesignerViewModel.BuildPreviewPageLabel(page, 1, 2));
    }

    /// <summary>Směr se píše i u jediné položky - na rozdíl od čítače něco říká.</summary>
    [Fact]
    public void JedinySmer_SeStalePojmenuje()
    {
        var page = Page(("VYCHOZI_ZASTAVKA", "Praha"), ("KONECNA_ZASTAVKA", "Sedlčany"), ("SMER", "T"));
        Assert.Equal("Směr tam: Praha → Sedlčany",
            TemplateDesignerViewModel.BuildPreviewPageLabel(page, 0, 1));
    }

    /// <summary>Neznámá hodnota <c>SMER</c> spadne zpátky na čítač, ne na prázdno.</summary>
    [Fact]
    public void NeznamySmer_SpadneNaCitac()
    {
        var page = Page(("VYCHOZI_ZASTAVKA", "Praha"), ("KONECNA_ZASTAVKA", "Sedlčany"), ("SMER", "X"));
        Assert.Equal("Směr 1 z 2: Praha → Sedlčany",
            TemplateDesignerViewModel.BuildPreviewPageLabel(page, 0, 2));
    }

    /// <summary>
    /// Obousměrná stránka dostane obousměrnou šipku a nejmenuje se „Směr" - nese oba.
    /// </summary>
    /// <remarks>
    /// Pozná se podle prázdného <c>SMER</c>: u obousměrné stránky ho agenda vědomě nevyplňuje.
    /// Žádné pole „tohle je obousměrná stránka" na drátě není a díky tomuhle znaku ho není potřeba
    /// zavádět.
    /// </remarks>
    [Fact]
    public void LinkaObousmerna_JmenujeSeLinkaAMaObousmernouSipku()
    {
        var page = Page(("VYCHOZI_ZASTAVKA", "Praha"), ("KONECNA_ZASTAVKA", "Sedlčany"), ("SMER", ""));
        Assert.Equal("Linka 2 z 3: Praha ↔ Sedlčany",
            TemplateDesignerViewModel.BuildPreviewPageLabel(page, 1, 3));
    }

    /// <summary>Když linka nese jen jeden konec trasy, vypíše se aspoň ten.</summary>
    [Fact]
    public void LinkaSJednimKoncem_VypiseAsponJeden()
    {
        var page = Page(("VYCHOZI_ZASTAVKA", "Praha"), ("SMER", "T"));
        Assert.Equal("Směr tam: Praha",
            TemplateDesignerViewModel.BuildPreviewPageLabel(page, 0, 2));
    }

    /// <summary>
    /// Zastávkový JŘ má přednost i tehdy, když by na stránce byla i výchozí zastávka.
    /// </summary>
    /// <remarks>
    /// Pojistka proti tomu, aby se linková větev někdy protlačila před zastávkovou a změnila
    /// odzkoušený tvar popisku vývěsky.
    /// </remarks>
    [Fact]
    public void ZastavkovyJr_MaPrednostPredLinkovym()
    {
        var page = Page(("POPIS_ZASTAVKA", "Malše, točna"), ("KONECNA_ZASTAVKA", "Nádraží"),
            ("VYCHOZI_ZASTAVKA", "Praha"), ("SMER", "T"));
        Assert.Equal("Zastávka 1 z 9: Malše, točna → Nádraží",
            TemplateDesignerViewModel.BuildPreviewPageLabel(page, 0, 9));
    }

    [Fact]
    public void PrazdnaZastavka_ObecnaCast()
    {
        var page = Page(("POPIS_ZASTAVKA", "  "));
        Assert.Equal("Část 1 z 4", TemplateDesignerViewModel.BuildPreviewPageLabel(page, 0, 4));
    }

    // ── jediná položka: není co přepínat ──────────────────────────────────────

    /// <summary>
    /// U jediné položky se čítač vynechá.
    /// </summary>
    /// <remarks>
    /// Typicky linka s obousměrným tiskem: „Linka 1 z 1: Praha ↔ Sedlčany" nic neříká a čítač
    /// svádí k tomu číst ho jako počet vytištěných stran - přesně ta záměna, kvůli které `B131`
    /// vznikla.
    /// </remarks>
    [Fact]
    public void JedinaPolozka_BezCitace()
    {
        var page = Page(("VYCHOZI_ZASTAVKA", "Praha"), ("KONECNA_ZASTAVKA", "Sedlčany"), ("SMER", ""));
        Assert.Equal("Praha ↔ Sedlčany", TemplateDesignerViewModel.BuildPreviewPageLabel(page, 0, 1));
    }

    [Fact]
    public void JedinaPolozkaBezPopisu_VratiAsponNazev()
    {
        Assert.Equal("Část",
            TemplateDesignerViewModel.BuildPreviewPageLabel(Page(("CISLO_FAKTURY", "1")), 0, 1));
    }

    // ── název položky pro popisky tlačítek ◀▶ ─────────────────────────────────

    [Theory]
    [InlineData("POPIS_ZASTAVKA", "Malše", "Zastávka")]
    [InlineData("VYCHOZI_ZASTAVKA", "Praha", "Linka")]     // bez SMER = obousměrná
    [InlineData("CISLO_FAKTURY", "2026001", "Část")]
    public void NazevPolozky_PodleObsahuStranky(string klic, string hodnota, string ocekavany)
    {
        Assert.Equal(ocekavany, TemplateDesignerViewModel.NazevPolozky(Page((klic, hodnota))));
    }

    [Fact]
    public void NazevPolozky_SmerJenKdyzJeVyplneny()
    {
        var page = Page(("VYCHOZI_ZASTAVKA", "Praha"), ("SMER", "T"));
        Assert.Equal("Směr", TemplateDesignerViewModel.NazevPolozky(page));
    }
}
