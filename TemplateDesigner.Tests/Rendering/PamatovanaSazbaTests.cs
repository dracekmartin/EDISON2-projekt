using TemplateDesigner.Designer;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Layout;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Náhled návrháře si pamatuje poslední sazbu, aby jeden krok zoomu nesázel stránky třikrát.
/// Pamatovaná sazba ale nesmí přežít nic, co ji mění: změnu šablony (hlásí hostitel), jiná data
/// ani jinou sazbu mezitím - ta prvkům přepisuje sdílený stav (zmenšení, mezery).
/// </summary>
/// <remarks>Počítadlo sazeb je společné všem testům, proto tahle třída neběží souběžně s nimi.</remarks>
[Collection(nameof(PamatovanaSazbaKolekce))]
public class PamatovanaSazbaTests
{
    private static (PreviewCanvas Nahled, DataContext Data) Nahled(bool pamatovat)
    {
        var sablona = new Template
        {
            Sections = [new Section { Name = "Tělo", Elements = [new TextElement { Text = "{POPIS_LINKY}", Width = 50, Height = 5 }] }],
        };
        var nahled = new PreviewCanvas(new DesignerState { CurrentTemplate = sablona }) { PamatovatSazbu = pamatovat };
        return (nahled, new DataContext(new Dictionary<string, object?> { ["POPIS_LINKY"] = "Linka 1" }));
    }

    [Fact]
    public void TataData_SazbaSePouzijeZnovu()
    {
        var (nahled, data) = Nahled(pamatovat: true);

        var prvni = nahled.SazbaProTest(data);
        Assert.Same(prvni, nahled.SazbaProTest(data));
    }

    [Fact]
    public void ZmenaSablony_JineData_JinaSazba_SazbaSeSpustiZnovu()
    {
        var (nahled, data) = Nahled(pamatovat: true);
        var prvni = nahled.SazbaProTest(data);

        nahled.ZapomenSazbu();
        var poZmeneSablony = nahled.SazbaProTest(data);
        Assert.NotSame(prvni, poZmeneSablony);

        Assert.NotSame(poZmeneSablony, nahled.SazbaProTest(new DataContext()));

        var znovu = nahled.SazbaProTest(data);
        new PageLayoutEngine(new SkiaHeightCalculator()).Layout(new Template(), null);   // export, jiná strana dat
        Assert.NotSame(znovu, nahled.SazbaProTest(data));
    }

    [Fact]
    public void BezPamatovani_SazbaPokazde()
    {
        var (nahled, data) = Nahled(pamatovat: false);

        Assert.NotSame(nahled.SazbaProTest(data), nahled.SazbaProTest(data));
    }
}

[CollectionDefinition(nameof(PamatovanaSazbaKolekce), DisableParallelization = true)]
public class PamatovanaSazbaKolekce;
