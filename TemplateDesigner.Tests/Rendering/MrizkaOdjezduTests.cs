using System.Text.Json.Nodes;
using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Volby mřížky u prvku odjezdů (`L15`) - „Čáry mezi řádky" a „Čáry mezi sloupci".
/// </summary>
/// <remarks>
/// <para>Prvek obě volby nabízel a ukládal, ale nečetl - mřížku kreslil vždycky. Táž vada, jakou
/// měla do 31. 8. linková tabulka (`L10`).</para>
/// <para><b>V pivotu ale ty dvě volby neznamenají totéž co v řádkové tabulce.</b> Řádek je hodina,
/// sloupec je skupina dnů - a svislá čára za sloupcem hodin **není oddělovač sloupců, ale osa**:
/// odděluje hodinu od minut, které k ní patří. Zůstává proto i s obojím vypnutým a hlídá to
/// <see cref="OsaZaSloupcemHodin_ZustaneIKdyzJeVseVypnute"/> - to je tady ta nejdůležitější
/// pojistka, protože bez ní by „konzistence s ostatními prvky" znamenala nečitelnou vývěsku.</para>
/// <para>Měří se čáry táhnoucí se přes celou šířku či výšku mřížky; minuty ani popisky takový pruh
/// neudělají, takže se do měření nepletou.</para>
/// </remarks>
public class MrizkaOdjezduTests
{
    private const float MmToPx = 10f;
    private const float SirkaMm = 90f;
    private const float VyskaMm = 60f;

    private static DepartureGridElement Prvek(bool radky, bool sloupce) => new()
    {
        X = 0, Y = 0, Width = SirkaMm, Height = VyskaMm,
        HeaderStyle = new TextStyle { FontFamily = "Arial", FontSize = 7 },
        DataStyle = new TextStyle { FontFamily = "Arial", FontSize = 6 },
        ShowSkupinaDnuHeader = true,
        Grid = new GridStyle
        {
            ShowRowLines = radky,
            ShowColumnLines = sloupce,
            Color = "#FF000000",
            Width = 0.2f,
        },
    };

    /// <summary>Dvě skupiny dnů, ať vůbec vznikne oddělovač mezi nimi.</summary>
    private static DataContext Data() => RenderSnapshot.Data(
        ("SKUPINY_DNU",
        [
            RenderSnapshot.Row(("ID", "1"), ("NADPIS", "Pracovní dny"), ("PORADI", "1")),
            RenderSnapshot.Row(("ID", "2"), ("NADPIS", "Sobota"), ("PORADI", "2")),
        ]),
        ("ODJEZDY",
        [
            RenderSnapshot.Row(("SKUPINA_DNU_ID", "1"), ("HODINA", "6"), ("MINUTA", "15")),
            RenderSnapshot.Row(("SKUPINA_DNU_ID", "1"), ("HODINA", "7"), ("MINUTA", "05")),
            RenderSnapshot.Row(("SKUPINA_DNU_ID", "2"), ("HODINA", "6"), ("MINUTA", "40")),
            RenderSnapshot.Row(("SKUPINA_DNU_ID", "2"), ("HODINA", "7"), ("MINUTA", "50")),
        ]));

    /// <summary>Zapnuté volby = mřížka jako dosud.</summary>
    [Fact]
    public void ObeVolbyZapnute_KresliObojiCary()
    {
        var (vodorovne, svisle) = Zmer(true, true);

        Assert.True(vodorovne > 0, "se zapnutými volbami chybí vodorovné čáry mezi hodinami");
        Assert.True(svisle > 0, "se zapnutými volbami chybí svislé čáry");
    }

    /// <summary>Vypnuté „Čáry mezi řádky" odeberou vodorovné čáry mezi hodinami.</summary>
    [Fact]
    public void VypnuteCaryMeziRadky_ZmiziVodorovne()
    {
        var (vodorovne, svisle) = Zmer(false, true);

        Assert.Equal(0, vodorovne);
        Assert.True(svisle > 0, "vypnutí vodorovných čar sebralo i svislé");
    }

    /// <summary>
    /// Vypnuté „Čáry mezi sloupci" odeberou oddělovač skupin dnů, ne osu.
    /// </summary>
    /// <remarks>
    /// Se dvěma skupinami jsou svislé čáry dvě - osa za sloupcem hodin a oddělovač mezi skupinami.
    /// Po vypnutí má zbýt právě jedna, ne žádná.
    /// </remarks>
    [Fact]
    public void VypnuteCaryMeziSloupci_ZmiziJenOddelovacSkupin()
    {
        var (_, sZapnutymi) = Zmer(true, true);
        var (vodorovne, sVypnutymi) = Zmer(true, false);

        Assert.True(sZapnutymi > sVypnutymi,
            $"po vypnutí svislých čar jich má ubýt, je {sVypnutymi} × {sZapnutymi}");
        Assert.True(sVypnutymi > 0, "vypnutí sloupců sebralo i osu za sloupcem hodin");
        Assert.True(vodorovne > 0, "vypnutí svislých čar sebralo i vodorovné");
    }

    /// <summary>
    /// <b>Osa za sloupcem hodin zůstává i s obojím vypnutým.</b>
    /// </summary>
    /// <remarks>
    /// Tohle je jádro rozhodnutí u `L15`: osa není oddělovač sloupců, ale hranice mezi hodinou
    /// a jejími minutami. Kdyby zmizela spolu se sloupci, byla by vývěska nečitelná - a přesně to
    /// by udělalo „mechanicky konzistentní" zapojení obou voleb.
    /// </remarks>
    [Fact]
    public void OsaZaSloupcemHodin_ZustaneIKdyzJeVseVypnute()
    {
        var (vodorovne, svisle) = Zmer(false, false);

        Assert.Equal(0, vodorovne);
        Assert.True(svisle > 0,
            "s obojím vypnutým zmizela i osa za sloupcem hodin - hodina se slije s minutami");
    }

    // ── měření ────────────────────────────────────────────────────────────────

    private static bool JeInkoust(SKColor c) => c.Red < 100 && c.Green < 100 && c.Blue < 100;

    private static (int Vodorovne, int Svisle) Zmer(bool radky, bool sloupce)
    {
        using var bmp = Vykresli(Prvek(radky, sloupce));
        return (PocetVodorovnychCar(bmp), PocetSvislychCar(bmp));
    }

    /// <summary>
    /// Obsazená plocha mřížky v pixelech - odvozená z obrázku, ne z rozměru prvku.
    /// </summary>
    /// <remarks>
    /// ⚠️ Mřížka nezabere celý prvek: šířku si dopočítá ze sloupců a výšku z počtu hodin. Kdyby se
    /// práh počítal z rozměru prvku, nevyšel by ani základní případ se zapnutými volbami.
    /// </remarks>
    private static (int Sirka, int Vyska) ObsazenaPlocha(SKBitmap bmp)
    {
        int maxX = 0, maxY = 0;
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
                if (JeInkoust(bmp.GetPixel(x, y)))
                {
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
        return (maxX + 1, maxY + 1);
    }

    /// <summary>Kolik řádků pixelů se táhne tmavě přes (skoro) celou obsazenou šířku.</summary>
    private static int PocetVodorovnychCar(SKBitmap bmp)
    {
        var (sirka, _) = ObsazenaPlocha(bmp);
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

    /// <summary>Kolik sloupců pixelů se táhne tmavě přes (skoro) celou obsazenou výšku.</summary>
    private static int PocetSvislychCar(SKBitmap bmp)
    {
        var (sirka, vyska) = ObsazenaPlocha(bmp);
        int prah = (int)(vyska * 0.8f);
        int pocet = 0;
        for (int x = 0; x < sirka; x++)
        {
            int tmavych = 0;
            for (int y = 0; y < vyska; y++)
                if (JeInkoust(bmp.GetPixel(x, y))) tmavych++;
            if (tmavych >= prah) pocet++;
        }
        return pocet;
    }

    private static SKBitmap Vykresli(DepartureGridElement prvek)
    {
        var data = Data();
        int w = (int)((SirkaMm + 4f) * MmToPx);
        int h = (int)((VyskaMm + 6f) * MmToPx);
        var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        prvek.Render(new RenderContext(canvas, MmToPx, data));
        return bmp;
    }
}
