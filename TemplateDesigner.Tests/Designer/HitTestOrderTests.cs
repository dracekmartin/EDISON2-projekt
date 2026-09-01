using TemplateDesigner.Designer;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Designer;

/// <summary>
/// Který prvek dostane klik, když se jich na jednom místě překrývá víc. Dvě pravidla, obě vydřená
/// nálezem z testování šablony (#13158): klik trefí ten, který je vidět navrchu, a už vybraný prvek
/// si klik podrží, i když ho jiný překrývá.
/// </summary>
public class HitTestOrderTests
{
    private const float CanvasWidth = 1200f;

    /// <summary>Sekce s prvky na téže pozici a téže hladině - tedy stav po vložení dvou prvků.</summary>
    private static (DesignerState State, DesignerCanvas Canvas, Section Section) Scene(params TemplateElement[] elements)
    {
        var section = new Section { Name = "Test", DesignerHeight = 100f };
        section.Elements.AddRange(elements);
        var template = new Template();
        template.Sections.Clear();
        template.Sections.Add(section);

        var state = new DesignerState { CurrentTemplate = template };
        return (state, new DesignerCanvas(state), section);
    }

    private static TextElement Text(string popis) =>
        new() { Text = popis, X = 10f, Y = 10f, Width = 40f, Height = 20f };

    /// <summary>Bod uprostřed prvku v px na canvasu.</summary>
    private static (float X, float Y) Center(DesignerCanvas canvas, Section section, TemplateElement el)
    {
        var origin = canvas.GetSectionOrigins(CanvasWidth).First(t => t.Section == section);
        return (origin.OriginX + (el.X + el.Width / 2f) * canvas.MmToPx,
                origin.OriginY + (el.Y + (el.Height ?? el.EffectiveDesignerHeight) / 2f) * canvas.MmToPx);
    }

    [Fact]
    public void PriStejneHladine_TrefiKlikTenNovejsi()
    {
        var stary = Text("starý");
        var novy  = Text("nový");
        var (_, canvas, section) = Scene(stary, novy);
        var (x, y) = Center(canvas, section, novy);

        // Kreslení jde od nejstaršího, takže navrch je vidět ten nový - klik musí trefit jeho.
        Assert.Same(novy, canvas.HitTestElement(x, y, CanvasWidth)!.Value.Element);
        Assert.Same(novy, canvas.HitTestElementForClick(x, y, CanvasWidth)!.Value.Element);
    }

    [Fact]
    public void VyssiHladinaPrebijePoradiVlozeni()
    {
        var stary = Text("starý");
        stary.ZIndex = 5;
        var novy = Text("nový");
        var (_, canvas, section) = Scene(stary, novy);
        var (x, y) = Center(canvas, section, novy);

        Assert.Same(stary, canvas.HitTestElement(x, y, CanvasWidth)!.Value.Element);
    }

    [Fact]
    public void VybranyPrvekSiKlikPodrzi_IKdyzHoJinyPrekryva()
    {
        var spodni = Text("spodní");
        var horni  = Text("horní");
        var (state, canvas, section) = Scene(spodni, horni);
        var (x, y) = Center(canvas, section, spodni);

        // Výběr ze stromu šablony - prvek je vybraný, ale na plátně ho druhý překrývá.
        state.SelectedElements.Add(spodni);

        Assert.Same(spodni, canvas.HitTestElementForClick(x, y, CanvasWidth)!.Value.Element);
        Assert.Same(horni,  canvas.HitTestElement(x, y, CanvasWidth)!.Value.Element);
    }

    [Fact]
    public void ZadnyPrekrytyNeniVybrany_VezmeSeTenNavrchu()
    {
        var spodni = Text("spodní");
        var horni  = Text("horní");
        var jinde  = Text("jinde");
        jinde.X = 100f;
        var (state, canvas, section) = Scene(spodni, horni, jinde);
        var (x, y) = Center(canvas, section, spodni);

        state.SelectedElements.Add(jinde);

        Assert.Same(horni, canvas.HitTestElementForClick(x, y, CanvasWidth)!.Value.Element);
    }

    [Fact]
    public void KlikMimoPrvky_NevratiNic()
    {
        var el = Text("prvek");
        var (_, canvas, section) = Scene(el);
        var origin = canvas.GetSectionOrigins(CanvasWidth).First(t => t.Section == section);

        float x = origin.OriginX + 80f * canvas.MmToPx;   // vpravo od prvku (ten končí na 50 mm)
        float y = origin.OriginY + 15f * canvas.MmToPx;

        Assert.Null(canvas.HitTestElement(x, y, CanvasWidth));
        Assert.Null(canvas.HitTestElementForClick(x, y, CanvasWidth));
    }
}
