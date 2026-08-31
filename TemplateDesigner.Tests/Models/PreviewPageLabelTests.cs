using TemplateDesigner;
using TemplateDesigner.Models;
using Xunit;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// Popisky stránek v nabídce stránkování náhledu - podle toho, co na stránce doopravdy je.
/// </summary>
/// <remarks>
/// U <b>linkového</b> JŘ to do 31. 8. hlásilo „Stránka N" u každé stránky (`L13`): popisek se stavěl
/// jen z <c>POPIS_ZASTAVKA</c>, které linkové agendy ve schématu nemají. Uživatel proto nepoznal,
/// na který směr kouká. Zastávkový JŘ se tou opravou nesmí hnout - jeho tvar je odzkoušený.
/// </remarks>
public class PreviewPageLabelTests
{
    private static DataContext Page(params (string Key, object? Value)[] values) =>
        new(values.ToDictionary(v => v.Key, v => v.Value));

    [Fact]
    public void ZastavkaSeSmerem_VratiCisloZastavkuASmer()
    {
        var page = Page(("POPIS_ZASTAVKA", "Malše, točna"), ("KONECNA_ZASTAVKA", "Nádraží"));
        Assert.Equal("3 - Malše, točna → Nádraží", TemplateDesignerViewModel.BuildPreviewPageLabel(page, 2));
    }

    [Fact]
    public void ZastavkaBezSmeru_VynechaSipku()
    {
        var page = Page(("POPIS_ZASTAVKA", "Malše, točna"), ("KONECNA_ZASTAVKA", ""));
        Assert.Equal("1 - Malše, točna", TemplateDesignerViewModel.BuildPreviewPageLabel(page, 0));
    }

    [Fact]
    public void AgendaBezZastavek_FallbackStrankaN()
    {
        var page = Page(("CISLO_FAKTURY", "2026001"));
        Assert.Equal("Stránka 5", TemplateDesignerViewModel.BuildPreviewPageLabel(page, 4));
    }

    // ── linkový JŘ ────────────────────────────────────────────────────────────

    /// <summary>Jednosměrná stránka linky nese trasu se šipkou po směru jízdy.</summary>
    [Fact]
    public void LinkaJednosmerna_VratiTrasuSeSipkou()
    {
        var page = Page(("VYCHOZI_ZASTAVKA", "Praha"), ("KONECNA_ZASTAVKA", "Sedlčany"), ("SMER", "T"));
        Assert.Equal("1 - Praha → Sedlčany", TemplateDesignerViewModel.BuildPreviewPageLabel(page, 0));
    }

    /// <summary>
    /// Obousměrná stránka dostane obousměrnou šipku.
    /// </summary>
    /// <remarks>
    /// Pozná se podle prázdného <c>SMER</c> - u obousměrné stránky ho agenda vědomě nevyplňuje,
    /// protože nese oba směry naráz. Žádné pole „tohle je obousměrná stránka" na drátě není a díky
    /// tomuhle znaku ho není potřeba zavádět.
    /// </remarks>
    [Fact]
    public void LinkaObousmerna_VratiObousmernouSipku()
    {
        var page = Page(("VYCHOZI_ZASTAVKA", "Praha"), ("KONECNA_ZASTAVKA", "Sedlčany"), ("SMER", ""));
        Assert.Equal("2 - Praha ↔ Sedlčany", TemplateDesignerViewModel.BuildPreviewPageLabel(page, 1));
    }

    /// <summary>Když linka nese jen jeden konec trasy, vypíše se aspoň ten.</summary>
    [Fact]
    public void LinkaSJednimKoncem_VypiseAsponJeden()
    {
        var page = Page(("VYCHOZI_ZASTAVKA", "Praha"), ("SMER", "T"));
        Assert.Equal("1 - Praha", TemplateDesignerViewModel.BuildPreviewPageLabel(page, 0));
    }

    /// <summary>
    /// Zastávkový JŘ má přednost i tehdy, když by na stránce byla i výchozí zastávka.
    /// </summary>
    /// <remarks>
    /// Pojistka proti tomu, aby se nová linková větev někdy protlačila před zastávkovou a změnila
    /// odzkoušený tvar popisku vývěsky.
    /// </remarks>
    [Fact]
    public void ZastavkovyJr_MaPrednostPredLinkovym()
    {
        var page = Page(("POPIS_ZASTAVKA", "Malše, točna"), ("KONECNA_ZASTAVKA", "Nádraží"),
            ("VYCHOZI_ZASTAVKA", "Praha"), ("SMER", "T"));
        Assert.Equal("1 - Malše, točna → Nádraží", TemplateDesignerViewModel.BuildPreviewPageLabel(page, 0));
    }

    [Fact]
    public void PrazdnaZastavka_FallbackStrankaN()
    {
        var page = Page(("POPIS_ZASTAVKA", "  "));
        Assert.Equal("Stránka 1", TemplateDesignerViewModel.BuildPreviewPageLabel(page, 0));
    }
}
