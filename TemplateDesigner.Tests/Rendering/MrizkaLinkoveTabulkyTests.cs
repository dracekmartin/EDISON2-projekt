using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Volby mřížky u linkové tabulky (`L10`) - „Čáry mezi řádky" a „Čáry mezi sloupci" musí
/// <b>opravdu něco dělat</b>.
/// </summary>
/// <remarks>
/// <para>Obě zaškrtávátka nabízí formulář u každého prvku (jsou ve sdílené sadě základních polí,
/// <c>BaseElementFields.cls</c>) a hodnota se ukládá do šablony - jenže linková tabulka je do
/// 31. 8. vůbec nečetla a mřížku kreslila vždycky. Uživatel klikl, uložilo se to a nic se
/// nestalo. Seznam zastávek, pevná tabulka i tabulka z dat je čtou odjakživa, takže je to
/// dorovnání, ne nová volba.</para>
/// <para>Měří se <b>čáry přes celou tabulku</b>, ne „kde je inkoust": vodorovná čára je řádek
/// pixelů táhnoucí se přes celou šířku, svislá sloupec přes celou výšku. Text zastávek ani
/// značky takový pruh nikdy neudělají, takže se do měření nepletou.</para>
/// </remarks>
public class MrizkaLinkoveTabulkyTests
{
    private const float MmToPx = 10f;
    private const float SirkaMm = 90f;
    private const float SirkaZastavky = 30f;
    private const float SirkaSpoje = 15f;

    /// <summary>Šířka, kterou tabulka opravdu zabere = součet sloupců (prvek je širší).</summary>
    private const float ObsazenaSirkaMm = SirkaZastavky + 2 * SirkaSpoje;

    private static LineTableElement Prvek(bool radky, bool sloupce) => new()
    {
        X = 0, Y = 0, Width = SirkaMm, Height = null,
        HeaderStyle = new TextStyle { FontFamily = "Arial", FontSize = 7 },
        DataStyle = new TextStyle { FontFamily = "Arial", FontSize = 6 },
        OddTcBackColor = null,
        ShowGroupHeaders = false,
        Grid = new GridStyle
        {
            ShowRowLines = radky,
            ShowColumnLines = sloupce,
            Color = "#FF000000",
            Width = 0.2f,
        },
        Columns =
        [
            new StopColumn { Key = ColumnKeys.StopName, Header = "", Width = SirkaZastavky, Visible = true },
            new StopColumn { Key = LineTableElement.TripKey, Header = "", Width = SirkaSpoje,
                Visible = true, Alignment = TextHorizontalAlignment.Center },
            new StopColumn { Key = LineTableElement.TripKey, Header = "", Width = SirkaSpoje,
                Visible = true, Alignment = TextHorizontalAlignment.Center },
        ],
    };

    private static DataContext Data() => RenderSnapshot.Data(
        ("ZASTAVKY",
        [
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Kroměříž"), ("PORADI", "1")),
            RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Přerov"), ("PORADI", "2")),
        ]),
        ("SPOJE",
        [
            RenderSnapshot.Row(("CISLO_SPOJE", ""), ("SKUPINA", "1"), ("SMER", "T"),
                ("CASY", new System.Text.Json.Nodes.JsonArray("", ""))),
            RenderSnapshot.Row(("CISLO_SPOJE", ""), ("SKUPINA", "1"), ("SMER", "T"),
                ("CASY", new System.Text.Json.Nodes.JsonArray("", ""))),
        ]),
        ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", ""), ("PORADI", "1"))]));

    /// <summary>Zapnuté volby = mřížka jako dosud, obojí čáry.</summary>
    [Fact]
    public void ObeVolbyZapnute_KresliObojiCary()
    {
        var (vodorovne, svisle) = Zmer(true, true);

        Assert.True(vodorovne > 0, "se zapnutými volbami chybí vodorovné čáry");
        Assert.True(svisle > 0, "se zapnutými volbami chybí svislé čáry");
    }

    /// <summary>Vypnuté „Čáry mezi řádky" odeberou <b>jen</b> vodorovné - svislé zůstanou.</summary>
    [Fact]
    public void VypnuteCaryMeziRadky_ZmiziJenVodorovne()
    {
        var (vodorovne, svisle) = Zmer(false, true);

        Assert.Equal(0, vodorovne);
        Assert.True(svisle > 0, "vypnutí vodorovných čar sebralo i svislé");
    }

    /// <summary>Vypnuté „Čáry mezi sloupci" odeberou <b>jen</b> svislé - vodorovné zůstanou.</summary>
    [Fact]
    public void VypnuteCaryMeziSloupci_ZmiziJenSvisle()
    {
        var (vodorovne, svisle) = Zmer(true, false);

        Assert.Equal(0, svisle);
        Assert.True(vodorovne > 0, "vypnutí svislých čar sebralo i vodorovné");
    }

    /// <summary>Obojí vypnuté = žádná mřížka. Text zastávek zůstává.</summary>
    [Fact]
    public void ObeVypnute_ZadnaMrizka()
    {
        var (vodorovne, svisle) = Zmer(false, false);

        Assert.Equal(0, vodorovne);
        Assert.Equal(0, svisle);

        using var bmp = Vykresli(Prvek(false, false));
        Assert.True(MaInkoust(bmp), "bez mřížky zmizel i text zastávek - to je moc");
    }

    // ── měření ────────────────────────────────────────────────────────────────

    private static bool JeInkoust(SKColor c) => c.Red < 100 && c.Green < 100 && c.Blue < 100;

    private static (int Vodorovne, int Svisle) Zmer(bool radky, bool sloupce)
    {
        using var bmp = Vykresli(Prvek(radky, sloupce));
        return (PocetVodorovnychCar(bmp), PocetSvislychCar(bmp));
    }

    /// <summary>Kolik řádků pixelů se táhne tmavě přes (skoro) celou šířku tabulky.</summary>
    /// <remarks>⚠️ Měří se přes <b>obsazenou</b> šířku (součet sloupců), ne přes šířku prvku -
    /// tabulka je užší než prvek a čáry končí za posledním sloupcem.</remarks>
    private static int PocetVodorovnychCar(SKBitmap bmp)
    {
        int sirka = (int)(ObsazenaSirkaMm * MmToPx);
        int prah = (int)(sirka * 0.9f);
        int pocet = 0;
        for (int y = 0; y < bmp.Height; y++)
        {
            int tmavych = 0;
            for (int x = 0; x < sirka; x++)
                if (JeInkoust(bmp.GetPixel(x, y))) tmavych++;
            if (tmavych >= prah) pocet++;
        }
        return pocet;
    }

    /// <summary>Kolik sloupců pixelů se táhne tmavě přes (skoro) celou výšku tabulky.</summary>
    private static int PocetSvislychCar(SKBitmap bmp)
    {
        int vyska = VyskaTabulkyPx(bmp);
        int prah = (int)(vyska * 0.9f);
        int pocet = 0;
        for (int x = 0; x < (int)(ObsazenaSirkaMm * MmToPx); x++)
        {
            int tmavych = 0;
            for (int y = 0; y < vyska; y++)
                if (JeInkoust(bmp.GetPixel(x, y))) tmavych++;
            if (tmavych >= prah) pocet++;
        }
        return pocet;
    }

    private static int VyskaTabulkyPx(SKBitmap bmp) => bmp.Height - (int)(6f * MmToPx);

    private static bool MaInkoust(SKBitmap bmp)
    {
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
                if (JeInkoust(bmp.GetPixel(x, y))) return true;
        return false;
    }

    private static SKBitmap Vykresli(LineTableElement prvek)
    {
        var data = Data();
        int w = (int)((prvek.Width + 4f) * MmToPx);
        int h = (int)((prvek.CalculateHeightFromData(data) + 6f) * MmToPx);
        var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        prvek.Render(new RenderContext(canvas, MmToPx, data));
        return bmp;
    }
}
