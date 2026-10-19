using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Legenda rozdílového linkového JŘ (`B192b`): server pošle poznámky obou verzí a u každé řekne,
/// ke které patří. Poznámka jen v jedné verzi má pod sebou pruh v barvě verze, jako odlišné buňky
/// v Tabulce spojů (rozhodnutí Jan 19. 10., do té doby přeškrtnutí a barevné písmo podle vzoru).
/// </summary>
public class RozdilovaLegendaTests
{
    private const float MmToPx = 96f / 25.4f;

    private static DataContext Poznamky(params (string Druh, string Typ, string Znak, string Text, string? Verze)[] poznamky) =>
        new(new Dictionary<string, object?>(),
            new Dictionary<string, IReadOnlyList<DataContext>>
            {
                ["POZNAMKY"] = [.. poznamky.Select(p => new DataContext(new Dictionary<string, object?>
                {
                    ["DRUH"] = p.Druh, ["TYP"] = p.Typ, ["ZNAK"] = p.Znak, ["TEXT"] = p.Text, ["VERZE"] = p.Verze ?? "",
                }))],
            });

    private static NotesLegendElement Prvek() => new() { X = 0, Y = 0, Width = 100, ShowHeaderBar = false };

    private static SKBitmap Vykresli(NotesLegendElement prvek, DataContext data)
    {
        float vyska = prvek.CalculateHeightFromData(data);
        var bitmap = new SKBitmap((int)(100 * MmToPx), (int)((vyska + 2) * MmToPx), SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        prvek.Render(new RenderContext(canvas, MmToPx, data));
        return bitmap;
    }

    private static int Pixelu(SKBitmap bitmap, Func<SKColor, bool> podminka)
    {
        int n = 0;
        for (int x = 0; x < bitmap.Width; x++)
            for (int y = 0; y < bitmap.Height; y++)
                if (podminka(bitmap.GetPixel(x, y))) n++;
        return n;
    }

    private static Func<SKColor, bool> Barva(string hex)
    {
        var barva = SKColor.Parse(hex);
        return p => Math.Abs(p.Red - barva.Red) < 30 && Math.Abs(p.Green - barva.Green) < 30 && Math.Abs(p.Blue - barva.Blue) < 30;
    }

    private static bool Tmava(SKColor p) => p.Red < 128 && p.Green < 128 && p.Blue < 128;

    /// <summary>Pruh přes celou šířku sloupce (100 mm) a výšku řádku je tisíce pixelů.</summary>
    [Theory]
    [InlineData("nova")]
    [InlineData("puvodni")]
    public void PoznamkaJenVJedneVerziMaPodbarveni(string verze)
    {
        var prvek = Prvek();
        string barva = verze == "nova" ? prvek.RozdilNovaBackColor : prvek.RozdilPuvodniBackColor;
        using var sVerzi = Vykresli(prvek, Poznamky(("2", "nz", "17", "jede od 10.7.26 do 31.8.26", verze)));
        using var bezVerze = Vykresli(prvek, Poznamky(("2", "nz", "17", "jede od 10.7.26 do 31.8.26", null)));

        Assert.True(Pixelu(sVerzi, Barva(barva)) > 2000, $"poznámka verze {verze} nemá podbarvení");
        Assert.Equal(0, Pixelu(bezVerze, Barva(barva)));
    }

    /// <summary>Text i značka zůstanou na pruhu vidět a nic se nepřeškrtává - tmavých pixelů je
    /// stejně jako bez verze.</summary>
    [Fact]
    public void PodbarvenaPoznamkaMaTextNavrchuBezPreskrtnuti()
    {
        var prvek = Prvek();
        const string text = "Na lince platí tarif IDS Jihočeského kraje.";
        using var puvodni = Vykresli(prvek, Poznamky(("9", "text", "", text, "puvodni")));
        using var bezVerze = Vykresli(prvek, Poznamky(("9", "text", "", text, null)));

        int tmavych = Pixelu(bezVerze, Tmava);
        Assert.True(tmavych > 50, "pojistka: text se vůbec nevykreslil");
        Assert.InRange(Pixelu(puvodni, Tmava), tmavych - 20, tmavych + 20);
    }

    /// <summary>Výšku poznámky zná až kreslení - pruh musí pokrýt všechny její řádky, ne jen první.</summary>
    [Fact]
    public void PruhPokryjeVsechnyRadkyPoznamky()
    {
        var prvek = Prvek();
        prvek.Width = 40;
        string text = string.Join(" ", Enumerable.Repeat("nejede ve dnech státních svátků", 4));
        using var bitmapa = Vykresli(prvek, Poznamky(("9", "text", "", text, "nova")));

        var barva = Barva(prvek.RozdilNovaBackColor);
        int radkuSPruhem = Enumerable.Range(0, bitmapa.Height).Count(y => barva(bitmapa.GetPixel(2, y)));
        float jedenRadekPx = prvek.EffectiveNoteLineHeight * MmToPx;
        Assert.True(radkuSPruhem > 2.5f * jedenRadekPx,
            $"pruh je vysoký {radkuSPruhem} px, jeden řádek poznámky má {jedenRadekPx:0} px");
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void BarvyVerziJenVRozdiloveAgende(bool rozdilovy, bool viditelne)
    {
        Assert.Equal(viditelne, NotesLegendElement.PoleFormulareViditelne(nameof(NotesLegendElement.RozdilNovaBackColor), true, rozdilovy));
        Assert.Equal(viditelne, NotesLegendElement.PoleFormulareViditelne(nameof(NotesLegendElement.RozdilPuvodniBackColor), true, rozdilovy));
    }

    [Fact]
    public void KopiePrvkuNeseBarvyVerzi()
    {
        var prvek = Prvek();
        prvek.RozdilPuvodniBackColor = "#FF112233";
        prvek.RozdilNovaBackColor = "#FF445566";
        var kopie = (NotesLegendElement)prvek.Clone();

        Assert.Equal("#FF112233", kopie.RozdilPuvodniBackColor);
        Assert.Equal("#FF445566", kopie.RozdilNovaBackColor);
    }
}
