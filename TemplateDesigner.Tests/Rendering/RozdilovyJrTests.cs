using SkiaSharp;
using TemplateDesigner.Config;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Rozdílový linkový JŘ (`B192`): server pošle změněný spoj dvakrát (původní a nový) a u každého
/// řekne, které buňky se liší. Tabulka podbarví jen ty buňky - barvou podle verze ze šablony.
/// </summary>
public class RozdilovyJrTests
{
    private const float MmToPx = 96f / 25.4f;

    private static DataContext Linka(bool sVerzemi) => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("ID", "1"), ("NAZEV_ZASTAVKY", "Strakonice"), ("PORADI", "1")),
            RenderSnapshot.Row(("ID", "2"), ("NAZEV_ZASTAVKY", "Blatná"), ("PORADI", "2")),
        ]),
        ("SPOJE",
        [
            Spoj("21", ["14:15", "14:20"], sVerzemi ? "puvodni" : null),
            Spoj("21", ["14:15", "14:22"], sVerzemi ? "nova" : null),
        ]),
        ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní dny"), ("PORADI", "1"))]));

    private static DataContext Spoj(string cislo, string[] casy, string? verze)
    {
        var pole = new List<(string, object?)>
        {
            ("CISLO_SPOJE", cislo), ("SKUPINA", "1"), ("SMER", "T"),
            ("CASY", new System.Text.Json.Nodes.JsonArray([.. casy.Select(c => (System.Text.Json.Nodes.JsonNode?)c)])),
        };
        if (verze is not null)
        {
            pole.Add(("VERZE", verze));
            pole.Add(("ROZDILY", System.Text.Json.Nodes.JsonNode.Parse("[0,1]")));
        }
        return RenderSnapshot.Row([.. pole]);
    }

    private static LineTableElement Prvek() => new()
    {
        X = 0, Y = 0, Width = 120,
        DataRowHeight = 5f,
        ShowNzRow = false,
        ShowPlatnostRow = false,
        ShowGroupHeaders = false,
    };

    /// <summary>Kolik pixelů má (zhruba) barvu <paramref name="hex"/>.</summary>
    private static int Pixelu(LineTableElement prvek, DataContext data, string hex)
    {
        var barva = SKColor.Parse(hex);
        float vyska = prvek.CalculateHeightFromData(data);
        using var bitmap = new SKBitmap((int)(120 * MmToPx), (int)((vyska + 2) * MmToPx), SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            prvek.Render(new RenderContext(canvas, MmToPx, data));
        }
        int n = 0;
        for (int x = 0; x < bitmap.Width; x++)
            for (int y = 0; y < bitmap.Height; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (Math.Abs(p.Red - barva.Red) < 8 && Math.Abs(p.Green - barva.Green) < 8 && Math.Abs(p.Blue - barva.Blue) < 8) n++;
            }
        return n;
    }

    [Fact]
    public void OdlisnaBunkaMaBarvuSveVerze()
    {
        var prvek = Prvek();
        var data = Linka(sVerzemi: true);

        // Jedna buňka (sloupec spoje × řádek 5 mm) je stovky pixelů; obě verze ji mají.
        Assert.True(Pixelu(prvek, data, prvek.RozdilPuvodniBackColor) > 200, "původní verze není podbarvená");
        Assert.True(Pixelu(prvek, data, prvek.RozdilNovaBackColor) > 200, "nová verze není podbarvená");
    }

    /// <summary>Prázdný sloupec dorovnání za spojem nepatří spoji - podbarvení rozdílu nepřebírá
    /// (nález Jan 9. 10.: zelený prázdný sloupec za novou verzí spoje).</summary>
    [Fact]
    public void PrazdnySloupecDorovnaniSeNepodbarvi()
    {
        // Nová verze spoje je poslední sloupec a za ní jsou prázdné sloupce až k okraji tabulky:
        // zelená musí skončit u spoje, ne u okraje.
        var prvek = Prvek();
        prvek.PadEmptyColumns = true;
        var (zelenaVpravo, tabulkaVpravo) = PraveOkraje(prvek, Linka(sVerzemi: true), prvek.RozdilNovaBackColor);

        Assert.True(tabulkaVpravo - zelenaVpravo > 3 * MmToPx,
            $"zelená končí {zelenaVpravo} px, tabulka {tabulkaVpravo} px - prázdný sloupec je podbarvený");
    }

    /// <summary>Nejpravější pixel barvy <paramref name="hex"/> a nejpravější tmavý pixel (čáry, text).</summary>
    private static (int Barva, int Tabulka) PraveOkraje(LineTableElement prvek, DataContext data, string hex)
    {
        var barva = SKColor.Parse(hex);
        float vyska = prvek.CalculateHeightFromData(data);
        using var bitmap = new SKBitmap((int)(120 * MmToPx), (int)((vyska + 2) * MmToPx), SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            prvek.Render(new RenderContext(canvas, MmToPx, data));
        }
        int barvaX = -1, tabulkaX = -1;
        for (int x = 0; x < bitmap.Width; x++)
            for (int y = 0; y < bitmap.Height; y++)
            {
                var p = bitmap.GetPixel(x, y);
                if (Math.Abs(p.Red - barva.Red) < 8 && Math.Abs(p.Green - barva.Green) < 8 && Math.Abs(p.Blue - barva.Blue) < 8) barvaX = x;
                else if (p.Red < 230 && p.Green < 230 && p.Blue < 230) tabulkaX = Math.Max(tabulkaX, x);
            }
        return (barvaX, tabulkaX);
    }

    [Fact]
    public void BezVerziSeNicNepodbarvi()
    {
        var prvek = Prvek();
        var data = Linka(sVerzemi: false);

        Assert.Equal(0, Pixelu(prvek, data, prvek.RozdilPuvodniBackColor));
        Assert.Equal(0, Pixelu(prvek, data, prvek.RozdilNovaBackColor));
    }

    [Fact]
    public void BarvaJeZeSablony()
    {
        var prvek = Prvek();
        prvek.RozdilNovaBackColor = "#FF4060FF";

        Assert.True(Pixelu(prvek, Linka(sVerzemi: true), "#FF4060FF") > 200);
    }

    [Theory]
    [InlineData(true, nameof(LineTableElement.RozdilNovaBackColor), true)]
    [InlineData(false, nameof(LineTableElement.RozdilNovaBackColor), false)]
    [InlineData(false, nameof(LineTableElement.RozdilPuvodniBackColor), false)]
    [InlineData(false, nameof(LineTableElement.OddTcBackColor), true)]
    public void BarvyRozdiluJenVRozdiloveAgende(bool rozdilovy, string klic, bool viditelne) =>
        Assert.Equal(viditelne, LineTableElement.PoleFormulareViditelne(klic, rozdilovy));

    [Fact]
    public void KopiePrvkuNeseBarvy()
    {
        var prvek = Prvek();
        prvek.RozdilPuvodniBackColor = "#FF112233";

        Assert.Equal("#FF112233", ((LineTableElement)prvek.Clone()).RozdilPuvodniBackColor);
    }

    /// <summary>Nezveřejněné agendy ukazuje výběr jen vývojáři - klíčem v lokálním configu.</summary>
    [Fact]
    public void SkryteAgendyJenLokalnimKlicem()
    {
        var target = new AppSettings();
        var local = new AppSettings();
        AppSettings.MergeLocal(target, local);
        Assert.False(target.Dev.HiddenAgendas);

        local.Dev.HiddenAgendas = true;
        AppSettings.MergeLocal(target, local);
        Assert.True(target.Dev.HiddenAgendas);
    }
}
