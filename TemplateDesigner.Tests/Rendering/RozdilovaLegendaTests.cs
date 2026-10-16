using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Legenda rozdílového linkového JŘ (`B192b`): server pošle poznámky obou verzí a u každé řekne,
/// ke které patří. Původní se přeškrtne a její značka dostane barvu původní verze, nová má barvu
/// nové verze - barvy jsou ze šablony, jako ve vzoru z CIS JŘ.
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

    [Fact]
    public void NovaPoznamkaMaBarvuNoveVerze()
    {
        var prvek = Prvek();
        using var nova = Vykresli(prvek, Poznamky(("2", "nz", "17", "jede od 10.7.26 do 31.8.26", "nova")));
        using var bezVerze = Vykresli(prvek, Poznamky(("2", "nz", "17", "jede od 10.7.26 do 31.8.26", null)));

        Assert.True(Pixelu(nova, Barva(prvek.RozdilNovaColor)) > 100, "nová poznámka nemá barvu nové verze");
        Assert.Equal(0, Pixelu(bezVerze, Barva(prvek.RozdilNovaColor)));
    }

    [Fact]
    public void PuvodniZnackaMaBarvuPuvodniVerze()
    {
        var prvek = Prvek();
        using var puvodni = Vykresli(prvek, Poznamky(("2", "nz", "93", "nejede 24.12.22", "puvodni")));

        Assert.True(Pixelu(puvodni, Barva(prvek.RozdilPuvodniColor)) > 30, "značka původní poznámky nemá barvu původní verze");
    }

    /// <summary>Přeškrtnutí přidá přes text vodorovnou čáru - tmavých pixelů je víc než bez něj.</summary>
    [Fact]
    public void PuvodniTextJePreskrtnuty()
    {
        var prvek = Prvek();
        const string text = "Na lince platí tarif IDS Jihočeského kraje.";
        using var puvodni = Vykresli(prvek, Poznamky(("9", "text", "", text, "puvodni")));
        using var bezVerze = Vykresli(prvek, Poznamky(("9", "text", "", text, null)));

        Assert.True(Pixelu(puvodni, Tmava) > Pixelu(bezVerze, Tmava) + 60, "původní text není přeškrtnutý");
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void BarvyVerziJenVRozdiloveAgende(bool rozdilovy, bool viditelne)
    {
        Assert.Equal(viditelne, NotesLegendElement.PoleFormulareViditelne(nameof(NotesLegendElement.RozdilNovaColor), true, rozdilovy));
        Assert.Equal(viditelne, NotesLegendElement.PoleFormulareViditelne(nameof(NotesLegendElement.RozdilPuvodniColor), true, rozdilovy));
    }

    [Fact]
    public void KopiePrvkuNeseBarvyVerzi()
    {
        var prvek = Prvek();
        prvek.RozdilPuvodniColor = "#FF112233";
        prvek.RozdilNovaColor = "#FF445566";
        var kopie = (NotesLegendElement)prvek.Clone();

        Assert.Equal("#FF112233", kopie.RozdilPuvodniColor);
        Assert.Equal("#FF445566", kopie.RozdilNovaColor);
    }
}
