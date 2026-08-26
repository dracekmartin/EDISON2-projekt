using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Models.Layout;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Agenda posílá v textu poznámky <c>#</c> jako <b>oddělovač odstavců</b>. Klient ho do 26. 8.
/// neinterpretoval, takže se tisklo písmeno uprostřed věty („…u řidiče.#INTEGROVANÉHO SYSTÉMU…").
/// </summary>
/// <remarks>
/// ⚠️ Past, kvůli které to má vlastní test: výška poznámky se počítá **jinou cestou** než render.
/// Kdyby se odstavce dělily až u kreslení, výška by o nich nevěděla, legenda by přetékala - a to
/// je v projektu opakovaná třída chyb. Proto se tu měří **obojí**: že se `#` vůbec zalomí,
/// a že si výška s vykresleným obsahem odpovídá.
/// </remarks>
public class OdstavceVPoznamceTests
{
    private const float MmToPx = 8f;

    private const string DvaOdstavce =
        "Na lince platí tarifní a přepravní podmínky vyhlášené dopravcem."
        + "#Informace o tarifu jsou k nahlédnutí u řidiče.";

    private static NotesLegendElement Legenda() => new()
    {
        X = 0, Y = 0, Width = 120, Height = null,
        HeaderStyle = new TextStyle { FontFamily = "Arial", FontSize = 8, Bold = true },
        DataStyle = new TextStyle { FontFamily = "Arial", FontSize = 7 },
        NoteFontSize = 7,
        NoteLineHeight = 4,
    };

    private static DataContext Data(string text) => RenderSnapshot.Data(
        ("POZNAMKY", [RenderSnapshot.Row(("TYP", "text"), ("ZNAK", ""), ("TEXT", text), ("DRUH", "9"))]));

    /// <summary>
    /// Dva <b>krátké</b> odstavce: spojené se vejdou na jeden řádek, rozdělené zaberou dva.
    /// ⚠️ Realisticky dlouhý text se na tohle <b>nehodí</b> - zalomí se na dva řádky tak i tak,
    /// takže by test neměřil nic. První verze tohohle testu na to naletěla.
    /// </summary>
    private const string KratkeOdstavce = "První odstavec.#Druhý odstavec.";

    [Fact]
    public void MrizkaVTextu_ZalomiOdstavec_NeniToPismeno()
    {
        // Bez zalomení se obě věty vejdou na jeden řádek; se zalomením musí být na dvou.
        var jednoOdstavcem = KratkeOdstavce.Replace("#", " ");

        float sZalomenim = new SkiaHeightCalculator()
            .CalculateHeight(Legenda(), 120f, Data(KratkeOdstavce));
        float bezZalomeni = new SkiaHeightCalculator()
            .CalculateHeight(Legenda(), 120f, Data(jednoOdstavcem));

        Assert.True(sZalomenim > bezZalomeni,
            $"`#` se nezalomil - výška s ním ({sZalomenim:0.0} mm) není větší než bez něj "
            + $"({bezZalomeni:0.0} mm), takže se vykreslí jako znak uprostřed věty");
    }

    [Fact]
    public void MrizkaSeNevykresli_JakoZnak()
    {
        // Přímá kontrola: v textu poznámky už `#` být nesmí, jinak ho render vypíše.
        var prvek = Legenda();
        var poznamky = prvek.FiltrujPoznamkyProTest(NactiPoznamky(prvek, Data(DvaOdstavce)));

        Assert.All(poznamky, p => Assert.DoesNotContain("#", p.Text));
        Assert.Contains(poznamky, p => p.Text.Contains('\n'));
    }

    [Fact]
    public void VyskaSOdstavci_OdpovidaVykreslenemu()
    {
        // Jádro pasti: výpočet výšky a kreslení jdou jinou cestou a musí si odpovídat i tady.
        var prvek = Legenda();
        var data = Data(DvaOdstavce);

        float rekl = new SkiaHeightCalculator().CalculateHeight(prvek, prvek.Width, data);
        float nakreslil = SpodekObsahuMm(prvek, data, rekl);

        Assert.True(nakreslil <= rekl + 0.3f,
            $"kreslí do {nakreslil:0.0} mm, ale říká si jen o {rekl:0.0} mm - druhý odstavec se ořízne");
        Assert.True(nakreslil >= rekl - 4f,
            $"říká si o {rekl:0.0} mm, ale kreslí jen do {nakreslil:0.0} mm - zbyde prázdné místo");
    }

    private static List<PoznamkaData> NactiPoznamky(NotesLegendElement prvek, DataContext data)
    {
        // BuildPoznamky je protected - test jde přes tutéž cestu jako render.
        var m = typeof(StopScheduleElementBase).GetMethod("BuildPoznamky",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        return (List<PoznamkaData>)m.Invoke(prvek, [data])!;
    }

    /// <summary>Kam až prvek doopravdy kreslí - plátno je schválně vyšší, ať je přetečení vidět.</summary>
    private static float SpodekObsahuMm(TemplateElement prvek, DataContext data, float rekl)
    {
        int w = (int)((prvek.X + prvek.Width + 5f) * MmToPx);
        int h = (int)MathF.Ceiling((rekl + 60f + prvek.Y) * MmToPx);
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
