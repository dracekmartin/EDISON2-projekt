using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Ruční render/měřicí cesty JŘ elementů (legenda poznámek, měření záhlaví odjezdů) stavěly typeface
/// natvrdo jako Normal/Bold → uživatelův **řez písma** (váha, kurzíva) se ztratil. Testy hlídají, že
/// (a) kaskáda řez vyrábí, (b) měření legendy ho vidí (zalomení sedí s tím, co se opravdu nakreslí).
/// </summary>
public class ScheduleFontCutTests
{
    // ── (a) Řez z kaskády ────────────────────────────────────────────────────

    [Fact]
    public void ToSKFontStyle_ExplicitniVahaPrebijeBold()
    {
        var st = new TextStyle { FontWeight = 300, Bold = true }.Resolve();

        Assert.Equal(300, st.ToSKFontStyle().Weight);
    }

    [Fact]
    public void ToSKFontStyle_BezVahy_OdvodiZBold()
    {
        Assert.Equal((int)SKFontStyleWeight.Bold, new TextStyle { Bold = true }.Resolve().ToSKFontStyle().Weight);
        Assert.Equal((int)SKFontStyleWeight.Normal, new TextStyle { Bold = false }.Resolve().ToSKFontStyle().Weight);
    }

    [Fact]
    public void ToSKFontStyle_KurzivaDaSklon()
    {
        Assert.Equal(SKFontStyleSlant.Italic, new TextStyle { Italic = true }.Resolve().ToSKFontStyle().Slant);
    }

    // ── (b) Měření legendy poznámek respektuje řez ───────────────────────────

    private static NotesLegendElement Legend(TextStyle dataStyle) => new()
    {
        Width       = 40f,      // úzký box → dlouhý text se zalomí na víc řádků
        Height      = null,     // dynamická výška = počítá se z obsahu
        NoteFontSize = 9f,
        DataStyle   = dataStyle,
    };

    private static DataContext Poznamka(string text) => new(
        rows: new Dictionary<string, IReadOnlyList<DataContext>>
        {
            ["POZNAMKY"] =
            [
                new DataContext(new Dictionary<string, object?>
                {
                    ["TYP"] = "text", ["ZNAK"] = "", ["TEXT"] = text,
                }),
            ],
        });

    [Fact]
    public void VyskaLegendy_TucnyTextSeZalomiNaVicRadku()
    {
        // Tučný text je širší → do stejné šířky se vejde méně slov → víc řádků → větší výška.
        // Když měření ignoruje řez (bug), obě výšky vyjdou stejně.
        // Dlouhý text = víc řádků → ~7% šířkový rozdíl tučného řezu se spolehlivě projeví na počtu řádků.
        const string text =
            "Spoj nezastavuje v zastávce Lipová u Šternberka rozcestí a jede odklonem přes " +
            "Hlubočky mimo zastávky Mariánské Valy, dále nezastavuje v zastávce Dolany " +
            "u Olomouce škola a v zastávce Pohořany rozcestí, v úseku Bělkovice jede " +
            "po objízdné trase a v zastávce Samotišky staví pouze na znamení";
        var data = Poznamka(text);

        float normalni = Legend(new TextStyle { Bold = false }).CalculateHeightFromData(data);
        float tucne    = Legend(new TextStyle { Bold = true }).CalculateHeightFromData(Poznamka(text));

        Assert.True(tucne > normalni,
            $"tučná legenda má být vyšší než normální (tučně {tucne} mm, normálně {normalni} mm)");
    }
}
