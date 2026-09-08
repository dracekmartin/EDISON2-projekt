using System.Text.Json.Nodes;
using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Přípojné řádky v <b>obousměrné</b> tabulce (`B128`): „vyčká / navazuje" musí být vidět u obou
/// směrů, ne jen u toho vpravo.
/// </summary>
/// <remarks>
/// <para>Do 7. 9. se přípojný řádek stavěl jen ze spojů hlavního směru a zrcadlená strana měla
/// buňky vždy prázdné - u druhého směru tedy cestující nevěděl, že spoj vyčká, i když to data
/// nesla.</para>
/// <para>⚠️ <b>Počet řádků a jejich výška musí zůstat v souladu s kreslením.</b> Tahle třída chyb
/// se u prvku opravovala třikrát (25. 8. počítáním ze všech spojů linky, 7. 9. z obou směrů, když
/// se kreslil jen jeden) a pokaždé skončila řezem stránky uprostřed hlavičky dalšího bloku. Proto
/// se tu měří **obojí**: kolik řádků vznikne (přes výšku prvku) i že se čas opravdu nakreslí na
/// tu správnou stranu (přes inkoust).</para>
/// </remarks>
public class PripojeObouSmeruTests
{
    private const float MmToPx = 10f;
    private const float VyskaRadku = 5f;
    private const float SirkaSpoje = 12f;
    private const float SirkaZastavky = 30f;

    private static (string, object?) Pripoj(string smer, string cas) =>
        ("PRIPOJE", RenderSnapshot.Nested(
            [[("TYP", "vycka"), ("PORADI", "2"), ("RADEK", "1"),
              ("CAS", cas), ("LINKA", "320163"), ("SPOJ", "21"), ("TEXT", smer), ("AVL", "A")]]));

    /// <summary>
    /// Obousměrná linka o třech zastávkách; přípoj u prostřední dostane podle parametrů levá
    /// strana, pravá, nebo obě.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Vlevo se kreslí směr PRVNÍHO spoje</b> (prvek to tak rozhoduje - `RozdelPodleSmeru`),
    /// tedy tady `T`. Parametry jsou proto pojmenované podle strany tisku, ne podle směru jízdy;
    /// první verze testu je měla obráceně a „zrcadlený" případ testovala na hlavní straně.
    /// </remarks>
    private static DataContext Data(bool pripojVlevo, bool pripojVpravo,
        string smerVlevo = "Trhové Sviny", string smerVpravo = "Trhové Sviny", bool nazvyZastavek = true)
    {
        var tam = new List<(string, object?)>
        {
            ("CISLO_SPOJE", "2"), ("SKUPINA", "1"), ("SMER", "T"),
            ("CASY", new JsonArray("8:00", "8:20", "8:40")),
        };
        if (pripojVlevo) tam.Add(Pripoj(smerVlevo, "10:25"));

        var zpet = new List<(string, object?)>
        {
            ("CISLO_SPOJE", "3"), ("SKUPINA", "1"), ("SMER", "Z"),
            ("CASY", new JsonArray("9:40", "9:20", "9:00")),
        };
        if (pripojVpravo) zpet.Add(Pripoj(smerVpravo, "11:25"));

        string Nazev(string n) => nazvyZastavek ? n : "";
        return RenderSnapshot.Data(
            ("ZASTAVKY",
            [
                RenderSnapshot.Row(("NAZEV_ZASTAVKY", Nazev("Kroměříž")), ("PORADI", "1")),
                RenderSnapshot.Row(("NAZEV_ZASTAVKY", Nazev("Hulín")), ("PORADI", "2")),
                RenderSnapshot.Row(("NAZEV_ZASTAVKY", Nazev("Přerov")), ("PORADI", "3")),
            ]),
            ("SPOJE", [RenderSnapshot.Row([.. tam]), RenderSnapshot.Row([.. zpet])]),
            ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", ""), ("PORADI", "1"))]));
    }

    private static LineTableElement Prvek() => new()
    {
        X = 0, Y = 0, Width = 120, Height = null,
        DataRowHeight = VyskaRadku,
        HeaderStyle = new TextStyle { FontFamily = "Arial", FontSize = 7 },
        DataStyle = new TextStyle { FontFamily = "Arial", FontSize = 6 },
        HighlightOddTc = false,
        ShowGroupHeaders = false,
        ShowNzRow = false,
        ShowPlatnostRow = false,
        ShowPripojeRows = true,
        Grid = new GridStyle { ShowColumnLines = true, ShowRowLines = true, Color = "#FFFFFFFF", Width = 0.2f },
        Columns =
        [
            new StopColumn { Key = ColumnKeys.StopName, Header = "", Width = SirkaZastavky, Visible = true },
            new StopColumn { Key = LineTableElement.TripKey, Header = "", Width = SirkaSpoje,
                Visible = true, Alignment = TextHorizontalAlignment.Center },
        ],
    };

    /// <summary>Kolik přípojných řádků vznikne - rozdíl výšky proti téže tabulce bez přípojů.</summary>
    private static float PocetPripojnychRadku(DataContext data)
    {
        var prvek = Prvek();
        var bez = Prvek();
        bez.ShowPripojeRows = false;
        return (prvek.CalculateHeightFromData(data) - bez.CalculateHeightFromData(data)) / VyskaRadku;
    }

    /// <summary>
    /// ⚠️ Jádro `B128`: přípoj má jen <b>zrcadlený</b> směr. Do 7. 9. z toho nevznikl žádný řádek,
    /// takže informace z tabulky tiše zmizela.
    /// </summary>
    [Fact]
    public void PripojJenUZrcadlenehoSmeru_PrestoDostaneRadek()
    {
        Assert.Equal(1f, PocetPripojnychRadku(Data(pripojVlevo: true, pripojVpravo: false)), precision: 2);
    }

    /// <summary>Přípoj jen u hlavního směru se nezměnil - kontrola, že se oprava netýká jen půlky.</summary>
    [Fact]
    public void PripojJenUHlavnihoSmeru_DostaneRadek()
    {
        Assert.Equal(1f, PocetPripojnychRadku(Data(pripojVlevo: false, pripojVpravo: true)), precision: 2);
    }

    /// <summary>
    /// Týž přípoj u obou směrů = <b>jeden</b> řádek s časy na obou stranách (rozhodnutí 7. 9.).
    /// Klíč slučování směr tisku neobsahuje, takže dva řádky by byly proti volbě „Slučovat přípoje".
    /// </summary>
    [Fact]
    public void TyzPripojUObouSmeru_JeJedenRadek()
    {
        Assert.Equal(1f, PocetPripojnychRadku(Data(pripojVlevo: true, pripojVpravo: true)), precision: 2);
    }

    /// <summary>Různé směry přípoje se neslučují ani přes strany - dva řádky, jako u jednoho směru.</summary>
    [Fact]
    public void RozdilneSmeryPripoje_DostanouDvaRadky()
    {
        var data = Data(pripojVlevo: true, pripojVpravo: true,
            smerVlevo: "Trhové Sviny", smerVpravo: "České Budějovice");

        Assert.Equal(2f, PocetPripojnychRadku(data), precision: 2);
    }

    /// <summary>
    /// Čas přípoje zrcadleného směru se opravdu <b>nakreslí</b>, v jeho sloupci vlevo.
    /// </summary>
    /// <remarks>
    /// <para>Počet řádků sám o sobě nedokazuje nic: řádek může vzniknout a zrcadlenou buňku nechat
    /// prázdnou - a to je přesně stav do 7. 9.</para>
    /// <para>⚠️ Měří se <b>proti vypnutým přípojným řádkům nad týmiž daty</b>, ne dvěma různými
    /// fixturami. Dvě fixtury (s přípojem a bez) se totiž liší i <b>značkou návaznosti v buňce
    /// spoje</b>, která leží v témže pásu - první verze testu tím měřila značku, ne čas, a mutací
    /// „nekreslit zrcadlenou stranu" prošla. Se stejnými daty je rozdíl v levém pásu právě to,
    /// co do něj přidaly přípojné řádky.</para>
    /// </remarks>
    [Fact]
    public void CasPripojeZrcadlenehoSmeru_SeNakresli()
    {
        var data = Data(pripojVlevo: true, pripojVpravo: true);
        var bezRadku = Prvek();
        bezRadku.ShowPripojeRows = false;

        using var bez = Vykresli(bezRadku, data);
        using var se = Vykresli(Prvek(), data);

        int inkoustBez = InkoustVPasu(bez, 0f, SirkaSpoje);
        int inkoustSe = InkoustVPasu(se, 0f, SirkaSpoje);

        Assert.True(inkoustSe > inkoustBez,
            $"v levém pásu je s přípojnými řádky {inkoustSe} a bez nich {inkoustBez} bodů inkoustu - "
            + "čas přípoje zrcadleného směru se nenakreslil");
    }

    /// <summary>Kolik bodů inkoustu leží ve svislém pásu <paramref name="odMm"/>-<paramref name="doMm"/>.</summary>
    private static int InkoustVPasu(SKBitmap bmp, float odMm, float doMm)
    {
        int od = (int)(odMm * MmToPx), do_ = Math.Min((int)(doMm * MmToPx), bmp.Width);
        int pocet = 0;
        for (int x = od; x < do_; x++)
            for (int y = 0; y < bmp.Height; y++)
                if (JeInkoust(bmp.GetPixel(x, y))) pocet++;
        return pocet;
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

    private static bool JeInkoust(SKColor c) => c.Red < 100 && c.Green < 100 && c.Blue < 100;
}
