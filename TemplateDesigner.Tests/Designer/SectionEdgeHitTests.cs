using TemplateDesigner.Designer;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Designer;

/// <summary>
/// Chytání spodní hrany sekce, za kterou se táhne její výška (`B118` krok 2).
/// </summary>
/// <remarks>
/// Výšku sekce šlo do 3. 9. změnit jen číslem, do 4. 9. navíc jen položkou v místní nabídce
/// (podnět Jan 3. 9.). Tažení potřebuje vědět, kdy je kurzor na hraně - a **kdy naopak ne**,
/// protože prvek, který se hrany dotýká, musí zůstat roztažitelný.
/// </remarks>
public class SectionEdgeHitTests
{
    private const float CanvasWidth = 1200f;

    private static (DesignerCanvas Canvas, DesignerState State, List<Section> Sections) Scena(params float?[] vysky)
    {
        var template = new Template
        {
            PageSize = new PageSize
            {
                Width = 210, Height = 297,
                MarginTop = 10, MarginBottom = 10, MarginLeft = 10, MarginRight = 10,
            }
        };
        template.Sections.Clear();
        for (int i = 0; i < vysky.Length; i++)
            template.Sections.Add(new Section { Name = $"Sekce {i + 1}", DesignerHeight = vysky[i] });

        var state = new DesignerState { CurrentTemplate = template };
        return (new DesignerCanvas(state), state, template.Sections);
    }

    /// <summary>Bod na spodní hraně dané sekce, uprostřed její šířky.</summary>
    private static (float X, float Y) BodNaHrane(DesignerCanvas canvas, Section section, float odchylkaPx = 0f)
    {
        var (_, ox, oy) = canvas.GetSectionOrigins(CanvasWidth).First(t => t.Section == section);
        return (ox + 100f, oy + canvas.SectionHeightPx(section) + odchylkaPx);
    }

    [Fact]
    public void KurzorNaSpodniHrane_ChytiTuSekci()
    {
        var (canvas, _, sekce) = Scena(50f, 50f, 50f);
        var (x, y) = BodNaHrane(canvas, sekce[1]);

        Assert.Same(sekce[1], canvas.HitTestSectionBottomEdge(x, y, CanvasWidth));
    }

    [Fact]
    public void KurzorDalOdHrany_NechytiNic()
    {
        // ⚠️ Odchylka je ABSOLUTNÍ, ne násobek pásma - jinak by test rostl s konstantou
        // a nechytil by její rozšíření (ověřeno mutací: pásmo 4 → 40 px prošlo).
        // 20 px je při výchozím zoomu ~5 mm, tedy jasně uvnitř sekce, kde se vybírají prvky.
        var (canvas, _, sekce) = Scena(50f, 50f, 50f);
        var (x, y) = BodNaHrane(canvas, sekce[1], odchylkaPx: -20f);

        Assert.Null(canvas.HitTestSectionBottomEdge(x, y, CanvasWidth));
    }

    [Fact]
    public void PasmoChytaniJeUzke()
    {
        // Široké pásmo by hranu chytalo skoro v celé sekci a nešlo by uchopit prvky u jejího
        // dolního okraje. Osm px je horní hranice, kterou má smysl ještě považovat za „hranu".
        Assert.InRange(DesignerCanvas.SectionEdgeGrabPx, 2f, 8f);
    }

    [Theory]
    [InlineData(-3f)]
    [InlineData(0f)]
    [InlineData(3f)]
    public void HranaSeChytaZObouStran(float odchylka)
    {
        var (canvas, _, sekce) = Scena(50f, 50f);
        var (x, y) = BodNaHrane(canvas, sekce[0], odchylka);

        Assert.Same(sekce[0], canvas.HitTestSectionBottomEdge(x, y, CanvasWidth));
    }

    [Fact]
    public void HranaSeChytaIVLevemStitkuSekce()
    {
        // Aby šla uchopit i tam, kde na hraně stojí prvek přes celou šířku.
        var (canvas, state, sekce) = Scena(50f, 50f);
        state.ShowSectionHeaders = true;
        var (_, ox, oy) = canvas.GetSectionOrigins(CanvasWidth).First(t => t.Section == sekce[0]);

        float vLevoOdPapiru = ox - state.CurrentTemplate.PageSize.MarginLeft * canvas.MmToPx - 2f;
        float naHrane = oy + canvas.SectionHeightPx(sekce[0]);

        Assert.Same(sekce[0], canvas.HitTestSectionBottomEdge(vLevoOdPapiru, naHrane, CanvasWidth));
    }

    [Fact]
    public void DynamickaVyska_SeNechyta()
    {
        // Prázdná výška = engine ji počítá z obsahu; tažením by se ta dynamika tiše zrušila.
        var (canvas, _, sekce) = Scena(50f, null);
        var (x, y) = BodNaHrane(canvas, sekce[1]);

        Assert.Null(canvas.HitTestSectionBottomEdge(x, y, CanvasWidth));
    }

    [Fact]
    public void UchytPrvkuNaHrane_MaPrednost()
    {
        // ⚠️ Přednost řeší volající (mouse down se ptá nejdřív na úchyt prvku), ale test drží,
        // že se obojí na tom místě opravdu potká - jinak by ta přednost byla zbytečná pojistka
        // a prvek u dolní hrany sekce by nešel roztáhnout.
        var (canvas, state, sekce) = Scena(50f, 50f);
        sekce[0].Elements.Add(new TextElement { X = 10f, Y = 0f, Width = 40f, Height = 50f });
        state.SelectedElements.Add(sekce[0].Elements[0]);

        var (_, ox, oy) = canvas.GetSectionOrigins(CanvasWidth).First(t => t.Section == sekce[0]);
        float x = ox + 30f * canvas.MmToPx;          // uprostřed spodní hrany prvku
        float y = oy + canvas.SectionHeightPx(sekce[0]);

        Assert.NotNull(canvas.HitTestSectionBottomEdge(x, y, CanvasWidth));
        Assert.NotNull(canvas.HitTestHandle(x, y, CanvasWidth));
    }
}
