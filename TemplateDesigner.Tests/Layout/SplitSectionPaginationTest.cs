using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Layout;

namespace TemplateDesigner.Tests.Layout;

/// <summary>
/// Regrese k „dvojité vykreslení" u sekce vyšší než stránka: SplitAcrossPages sekce 400 mm se
/// rozdělí na 2 chunky (page1 = 0–297 ClipTop=0, page2 = 297–400 ClipTop=297). Element na Y=20
/// patří jen do 1. chunku. Engine dělí správně; artefakt byl v renderu (border-pass mimo clip
/// kreslil rámeček i na 2. straně) - opraveno v PreviewCanvas.RenderPage (border-pass clip na stránku).
/// </summary>
public class SplitSectionPaginationTest
{
    [Fact]
    public void SplitSection400mm_RozdeliNaDvaChunky_SpravneClipTop()
    {
        var text = new TextElement { X = 10, Y = 20, Width = 100, Height = 10, Text = "AHOJ" };
        var section = new Section
        {
            Name = "Obsah",
            PrintBehavior = SectionPrintBehavior.Once,
            OverflowBehavior = SectionOverflowBehavior.SplitAcrossPages,
            DesignerHeight = 400,
            Elements = [text]
        };
        var tpl = new Template { PageSize = PageSize.A4Portrait, Sections = [section] };  // 297 mm plocha, okraje 0

        var result = new PageLayoutEngine(new FixedHeightCalculator(null)).Layout(tpl, null);

        Assert.Equal(2, result.TotalPages);

        var p1 = Assert.Single(result.Pages[0].Sections);
        Assert.Equal(0f, p1.ClipTop);
        Assert.Equal(297f, p1.ClipBottom);
        Assert.Equal(297f, p1.Height);

        var p2 = Assert.Single(result.Pages[1].Sections);
        Assert.Equal(297f, p2.ClipTop);      // 2. chunk začíná až za 1. stranou → element na Y=20 sem nepatří
        Assert.Equal(400f, p2.ClipBottom);
        Assert.Equal(103f, p2.Height);
    }
}
