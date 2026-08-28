using System.Text.Json.Nodes;
using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Šipka směru jízdy v <b>linkové</b> tabulce - vlastní sloupec, ve kterém běží svislá šipka přes
/// řádky zastávek (legacy sloupec typu 5).
/// </summary>
/// <remarks>
/// ⚠️ Kreslí se <b>naší vektorovou šipkou ze zastávkového JŘ</b>, ne glyfy ze symbolového fontu jako
/// legacy (rozhodnutí Jan 28. 8.) - jedna implementace pro obojí, <c>DirectionArrowRenderer</c>.
/// <para>Měří se obrázek, protože jde o kresbu přes víc řádků, kterou žádná buňka nedělá.</para>
/// </remarks>
public class SipkaSmeruLinkovaTests
{
    private const float MmToPx = 10f;
    private const float SirkaSipky = 4f;
    private const float SirkaZastavky = 30f;

    private static LineTableElement Prvek(bool sipkaViditelna, string? barvaSipky = null) => new()
    {
        X = 0, Y = 0, Width = 80, Height = null,
        HeaderStyle = new TextStyle { FontFamily = "Arial", FontSize = 7 },
        DataStyle = new TextStyle { FontFamily = "Arial", FontSize = 6 },
        HighlightOddTc = false,
        Grid = new GridStyle { ShowColumnLines = true, ShowRowLines = true, Color = "#FFFFFFFF", Width = 0.2f },
        Columns =
        [
            new StopColumn
            {
                Key = ColumnKeys.Arrow, Header = "", Width = SirkaSipky, Visible = sipkaViditelna,
                Style = barvaSipky is null ? null : new ColumnVisualStyle { Data = new TextStyle { TextColor = barvaSipky } },
            },
            new StopColumn { Key = ColumnKeys.StopName, Header = "Zastávka", Width = SirkaZastavky, Visible = true },
            new StopColumn { Key = LineTableElement.TripKey, Header = "Spoj", Width = 11f, Visible = true },
        ],
    };

    private static DataContext Data() => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Kroměříž"), ("PORADI", "1")),
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Hulín"), ("PORADI", "2")),
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Přerov"), ("PORADI", "3")),
        ]),
        ("SPOJE",
        [
            RenderSnapshot.Row(("CISLO_SPOJE", "1"), ("SKUPINA", "1"),
                ("CASY", new JsonArray("8:00", "8:20", "8:40"))),
        ]),
        ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", "PD"), ("PORADI", "1"))]));

    [Fact]
    public void ZapnutySloupec_KresliSipkuVeSvemSloupci()
    {
        using var bmp = Vykresli(Prvek(sipkaViditelna: true), Data());

        // Šipka je svislá čára uprostřed svého sloupce, takže inkoust musí být kolem 2 mm.
        Assert.True(InkoustVeSloupci(bmp, Sloupec(SirkaSipky / 2)) > 0,
            "ve sloupci šipky se nic nenakreslilo");
    }

    /// <summary>
    /// Šipka jde přes <b>řádky zastávek</b>, ne jen o pár pixelů - jinak by to nebyla šipka směru.
    /// </summary>
    [Fact]
    public void SipkaJdePresRadkyZastavek()
    {
        var data = Data();
        var prvek = Prvek(sipkaViditelna: true);
        using var bmp = Vykresli(prvek, data);

        int x = Sloupec(SirkaSipky / 2);
        int prvni = -1, posledni = -1;
        for (int y = 0; y < bmp.Height; y++)
        {
            var c = bmp.GetPixel(x, y);
            if (c.Red >= 100 || c.Green >= 100 || c.Blue >= 100) continue;
            if (prvni < 0) prvni = y;
            posledni = y;
        }

        float vyskaMm = prvek.CalculateHeightFromData(data);
        float delkaMm = (posledni - prvni) / MmToPx;
        // Hlava tabulky (pás skupin + čísla spojů) šipku nemá, takže se čeká „velká část", ne celek.
        Assert.True(delkaMm > vyskaMm * 0.4f,
            $"šipka je dlouhá {delkaMm:0.#} mm z {vyskaMm:0.#} mm výšky tabulky - nejde přes řádky zastávek");
    }

    /// <summary>
    /// Se skrytým sloupcem se tabulka kreslí <b>úplně stejně</b>, jako kdyby sloupec vůbec neměla.
    /// </summary>
    /// <remarks>
    /// ⚠️ Nejde tvrdit „v tom místě není nic": skrytý sloupec ze rozvržení zmizí, takže se na jeho
    /// x-pozici posunou zastávky a inkoust tam je. Tvrdí se proto rovnost obrázků - to je zároveň
    /// přesně ta záruka, kterou potřebuje migrace: sloupec se doplní do hotových šablon a nesmí jim
    /// změnit vzhled.
    /// </remarks>
    [Fact]
    public void SkrytySloupec_KresliStejneJakoKdybyVubecNebyl()
    {
        var data = Data();
        var bezSloupce = Prvek(sipkaViditelna: false);
        bezSloupce.Columns.RemoveAll(c => c.Key == ColumnKeys.Arrow);

        using var skryty = Vykresli(Prvek(sipkaViditelna: false), data);
        using var chybejici = Vykresli(bezSloupce, data);

        Assert.Equal(chybejici.Width, skryty.Width);
        Assert.Equal(chybejici.Height, skryty.Height);
        for (int y = 0; y < skryty.Height; y++)
            for (int x = 0; x < skryty.Width; x++)
                if (skryty.GetPixel(x, y) != chybejici.GetPixel(x, y))
                    Assert.Fail($"skrytý sloupec šipky mění obrázek na {x / MmToPx:0.#}, {y / MmToPx:0.#} mm");
    }

    /// <summary>
    /// Sloupec se v dialogu jmenuje <b>česky</b>, ne kódovým klíčem.
    /// </summary>
    /// <remarks>
    /// Nález Jan 28. 8.: v dialogu svítilo „Arrow". Mapa lidských názvů (`FriendlyName`) propadá na
    /// klíč sloupce, takže **každý nový klíč se tam musí doplnit** - jinak se do UI dostane anglický
    /// kód. Termín „šipka směru" je shodný s vývěskou („Značky a šipka směru", „Barva šipky").
    /// </remarks>
    [Fact]
    public void SloupecSeVDialoguJmenujeCesky()
    {
        var vm = new TemplateDesigner.LineTableColumnVM
        {
            Key = ColumnKeys.Arrow,
            Header = "",
        };

        Assert.Equal("Šipka směru", vm.DisplayName);
    }

    /// <summary>Výchozí stav je skrytý, aby migrace nezměnila vzhled hotových šablon.</summary>
    [Fact]
    public void VeVychozichSloupcich_JeSipkaSkryta() =>
        Assert.False(LineTableElement.GetDefaultColumns()
            .First(c => c.Key == ColumnKeys.Arrow).Visible);

    /// <summary>
    /// Barvu si šipka bere z <b>textové barvy svého sloupce</b>, takže nepřidává další nastavení
    /// do panelu - nastaví se v dialogu sloupců jako u každého jiného sloupce.
    /// </summary>
    [Fact]
    public void BarvaSipky_JdeZeStyluSloupce()
    {
        using var bmp = Vykresli(Prvek(sipkaViditelna: true, barvaSipky: "#FFFF0000"), Data());

        int cervenych = 0;
        int x = Sloupec(SirkaSipky / 2);
        for (int y = 0; y < bmp.Height; y++)
        {
            var c = bmp.GetPixel(x, y);
            if (c.Red > 150 && c.Green < 100 && c.Blue < 100) cervenych++;
        }

        Assert.True(cervenych > 0,
            "šipka se nenakreslila barvou sloupce - kreslí se zadrátovanou barvou");
    }

    // ── měření ────────────────────────────────────────────────────────────────

    private static int Sloupec(float mm) => (int)(mm * MmToPx);

    private static int InkoustVeSloupci(SKBitmap bmp, int x)
    {
        if (x < 0 || x >= bmp.Width) return 0;
        int n = 0;
        for (int y = 0; y < bmp.Height; y++)
        {
            var c = bmp.GetPixel(x, y);
            if (c.Red < 100 && c.Green < 100 && c.Blue < 100) n++;
        }
        return n;
    }

    private static SKBitmap Vykresli(LineTableElement prvek, DataContext data)
    {
        int w = (int)((prvek.Width + 4f) * MmToPx);
        int h = (int)((prvek.CalculateHeightFromData(data) + 6f) * MmToPx);
        var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        prvek.Render(new RenderContext(canvas, MmToPx, data));
        return bmp;
    }
}
