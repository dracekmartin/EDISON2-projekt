using TemplateDesigner;
using TemplateDesigner.Models;
using Xunit;

namespace TemplateDesigner.Tests.Models;

/// <summary>Popisky stránek dat pro combobox stránkování v preview (přímý skok na zastávku×směr).</summary>
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

    [Fact]
    public void PrazdnaZastavka_FallbackStrankaN()
    {
        var page = Page(("POPIS_ZASTAVKA", "  "));
        Assert.Equal("Stránka 1", TemplateDesignerViewModel.BuildPreviewPageLabel(page, 0));
    }
}
