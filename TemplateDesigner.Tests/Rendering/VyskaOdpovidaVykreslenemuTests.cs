using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Layout;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Výška, kterou si prvek řekne pro stránkování, musí odpovídat tomu, co doopravdy nakreslí.
/// </summary>
/// <remarks>
/// <para>Vzniklo po chybě z 25. 8., kdy linková tabulka počítala přípojné řádky ze všech spojů
/// linky, ale kreslila je jen ze spojů bloku. Výška vycházela větší než render, stránkování podle
/// ní řezalo a řez padl doprostřed hlavičky dalšího bloku.</para>
/// <para>⚠️ <b>Model se porovnává s obrázkem, ne sám se sebou.</b> Tu chybu nemohl chytit žádný
/// dosavadní test - výška i místa pro řez se braly z téhož špatného čísla, takže si odpovídaly.</para>
/// </remarks>
public class VyskaOdpovidaVykreslenemuTests
{
    private const float MmToPx = 8f;

    public static TheoryData<string> Prvky() => ["StopList", "DepartureGrid", "NotesLegend", "LineTable", "DynamicTable"];

    [Theory]
    [MemberData(nameof(Prvky))]
    public void NakreslenaVyskaSediSVypoctem(string druh)
    {
        var (prvek, data) = Fixtura(druh);
        prvek.Height = null;   // dynamická výška - o té je řeč

        float rekl = new SkiaHeightCalculator().CalculateHeight(prvek, prvek.Width, data);
        float nakreslil = SpodekObsahuMm(prvek, data, rekl);

        Assert.True(nakreslil <= rekl + 0.3f,
            $"{druh}: kreslí do {nakreslil:0.0} mm, ale říká si jen o {rekl:0.0} mm - obsah se ořízne");
        Assert.True(nakreslil >= rekl - 3f,
            $"{druh}: říká si o {rekl:0.0} mm, ale kreslí jen do {nakreslil:0.0} mm - zbyde prázdné místo");
    }

    private static (TemplateElement Prvek, DataContext Data) Fixtura(string druh) => druh switch
    {
        "StopList"      => (ElementRenderFixtures.StopList(), ElementRenderFixtures.StopScheduleData()),
        "DepartureGrid" => (ElementRenderFixtures.DepartureGrid(), ElementRenderFixtures.StopScheduleData()),
        "NotesLegend"   => (ElementRenderFixtures.NotesLegend(), ElementRenderFixtures.StopScheduleData()),
        "LineTable"     => (ElementRenderFixtures.LineTable(), ElementRenderFixtures.LineTableData()),
        "DynamicTable"  => (ElementRenderFixtures.DynamicTable(), ElementRenderFixtures.DynamicTableData()),
        _ => throw new ArgumentOutOfRangeException(nameof(druh))
    };

    /// <summary>Odsazení posledního nebílého řádku obrázku - tedy kam až prvek doopravdy kreslí.
    /// Plátno je schválně vyšší, aby přetečení bylo vidět a neořízlo se.</summary>
    private static float SpodekObsahuMm(TemplateElement prvek, DataContext data, float rekl)
    {
        float platnoMm = (rekl + 60f) + prvek.Y;
        int w = (int)((prvek.X + prvek.Width + 5f) * MmToPx), h = (int)MathF.Ceiling(platnoMm * MmToPx);
        using var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            prvek.Render(new RenderContext(canvas, MmToPx, data));
        }

        for (int y = h - 1; y >= 0; y--)
            for (int x = 0; x < w; x++)
                if (bmp.GetPixel(x, y) != SKColors.White)
                    return (y + 1) / MmToPx - prvek.Y;
        return 0f;
    }
}
