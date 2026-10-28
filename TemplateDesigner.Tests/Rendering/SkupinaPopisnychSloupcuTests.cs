using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Popisné sloupce linkové tabulky jsou <b>jedna skupina</b> (změřeno na vzoru IDZK 940): mají
/// jeden společný nadpis a uvnitř se nekreslí svislé čáry, zatímco mezi sloupci spojů zůstávají.
/// </summary>
/// <remarks>
/// Stížnost, kvůli které to vzniklo: „ohraničení jednotlivých km sloupců nejde odstranit" a
/// „nelze jeden nadpis pro info sloupce". Do 26. 8. šlo vypnout jen <b>celý</b> <c>ShowColumnLines</c>,
/// tedy i čáry mezi spoji.
/// <para>⚠️ Měří se <b>obrázek</b>, ne hodnoty properties - o tom, jestli čára zmizela, rozhoduje
/// render, a ten jde přes čtyři různé cesty skládání buněk (záhlaví, platnost, data, přípoje).</para>
/// </remarks>
public class SkupinaPopisnychSloupcuTests
{
    private const float MmToPx = 10f;

    private static LineTableElement Prvek() => new()
    {
        X = 0, Y = 0, Width = 120, Height = null,
        HeaderStyle = new TextStyle { FontFamily = "Arial", FontSize = 8 },
        DataStyle = new TextStyle { FontFamily = "Arial", FontSize = 7 },
        OddTcBackColor = null,
        Grid = new GridStyle { ShowRowLines = false, ShowColumnLines = true, Color = "#FF000000", Width = 0.3f },
    };

    [Fact]
    public void BezVnitrnichCar_UbereCaryVPopisnemBloku_AleNeMeziSpoji()
    {
        var data = ElementRenderFixtures.LineTableData();

        int sCarami = SvisleCary(Prvek(), data, out var sirkaPopisne);
        var bez = Prvek();
        bez.PopisneSloupceBezVnitrnichCar = true;
        int bezCar = SvisleCary(bez, data, out _);

        Assert.True(bezCar < sCarami,
            $"po zapnutí „bez vnitřních čar\" se počet svislých čar nezmenšil ({bezCar} vs {sCarami})");

        // Druhá polovina stížnosti: čáry mezi spoji musí zůstat, jinak je to totéž jako vypnout
        // ShowColumnLines pro celý prvek - a přesně to dnes nešlo obejít.
        Assert.True(bezCar > 0,
            "zmizely všechny svislé čáry - mezi sloupci spojů měly zůstat");
        Assert.True(CaraVpravoOd(bez, data, sirkaPopisne),
            "za popisným blokem nezůstala žádná svislá čára, takže zmizely i čáry mezi spoji");
    }

    [Fact]
    public void NadpisPopisnychSloupcu_SeVykresli()
    {
        var data = ElementRenderFixtures.LineTableData();

        // Široký prvek a krátký nadpis: v úzké buňce by ho zmenšení mohlo srazit k nule
        // a test by pak měřil něco jiného, než co má.
        var bez = Prvek(); bez.Width = 260;
        var s = Prvek();   s.Width = 260;
        s.PopisneSloupceNadpis = "SMER";

        int inkBez = Pixely(bez, data);
        int inkS = Pixely(s, data);

        Assert.True(inkS > inkBez,
            $"nadpis popisných sloupců se nevykreslil - stejně inkoustu s ním ({inkS}) jako bez ({inkBez})");
    }

    [Fact]
    public void PrazdnyNadpis_JeVychozi()
    {
        var data = ElementRenderFixtures.LineTableData();
        var prvek = Prvek();

        Assert.Equal("", prvek.PopisneSloupceNadpis);
        Assert.False(prvek.PopisneSloupceBezVnitrnichCar);
        Assert.True(Pixely(prvek, data) > 0, "prvek s výchozím nastavením nekreslí vůbec nic");
    }

    /// <summary>
    /// Bez nadpisu se nad popisnými sloupci nekreslí <b>nic</b>, ani podbarvení pásu.
    /// </summary>
    /// <remarks>
    /// `L9-R4`, rozhodnuto 27. 8. podle legacy: starý modul nad popisnými sloupci pás vůbec nemá -
    /// skupina odjezdů je atribut <b>jen</b> sloupců spojů („pouze pro typ sloupce 7,9",
    /// <c>JRapiLinPrtWin.mac:246</c>). Prázdný nadpis = chování starého modulu, vyplněný = featura
    /// nad rámec legacy. Měří se <b>podbarvení</b>, protože prázdná buňka nemá žádný inkoust.
    /// </remarks>
    [Fact]
    public void BezNadpisu_SeNadPopisnymiSloupciNekresliAniPodbarveni()
    {
        var data = ElementRenderFixtures.LineTableData();
        // Šedý pás musí být čím poznat, a nadpisy skupin dnů ho drží nahoře i bez našeho nadpisu.
        var bez = Prvek();  bez.HeaderStyle = new TextStyle { FontFamily = "Arial", FontSize = 8, BackgroundColor = "#FFE0E0E0" };
        var s = Prvek();    s.HeaderStyle = bez.HeaderStyle.Clone();
        s.PopisneSloupceNadpis = "SMER";

        int bezPodbarveni = PodbarveneVPasu(bez, data);
        int sPodbarvenim = PodbarveneVPasu(s, data);

        Assert.True(bezPodbarveni == 0,
            $"nad popisnými sloupci je bez nadpisu podbarvení ({bezPodbarveni} px) - má tam být nic");
        Assert.True(sPodbarvenim > 0,
            "s nadpisem se pás nad popisnými sloupci nekreslí vůbec");
    }

    /// <summary>Podbarvené pixely v levé části <b>prvního</b> řádku prvku, tedy v pásu nad
    /// popisnými sloupci - ne inkoust, prázdná buňka žádný nemá.</summary>
    private static int PodbarveneVPasu(LineTableElement prvek, DataContext data)
    {
        using var bmp = Vykresli(prvek, data);
        int n = 0;
        for (int y = (int)(0.2f * MmToPx); y <= (int)(1.0f * MmToPx); y++)
            for (int x = (int)(2f * MmToPx); x <= (int)(20f * MmToPx); x++)
            {
                var c = bmp.GetPixel(x, y);
                bool bila = c.Red > 240 && c.Green > 240 && c.Blue > 240;
                bool inkoust = c.Red < 100 && c.Green < 100 && c.Blue < 100;
                if (!bila && !inkoust) n++;
            }
        return n;
    }

    /// <summary>Počet svislých čar v řádku záhlaví - hledá se ve výšce, kde už je tabulka.</summary>
    private static int SvisleCary(LineTableElement prvek, DataContext data, out float sirkaPopisneMm)
    {
        using var bmp = Vykresli(prvek, data);
        int y = bmp.Height / 2;
        int cary = 0;
        bool predchoziCerna = false;
        sirkaPopisneMm = 0f;

        for (int x = 0; x < bmp.Width; x++)
        {
            bool cerna = JeCara(bmp.GetPixel(x, y));
            if (cerna && !predchoziCerna) cary++;
            predchoziCerna = cerna;
        }
        return cary;
    }

    private static bool CaraVpravoOd(LineTableElement prvek, DataContext data, float _)
    {
        // Sloupce spojů jsou v pravé části prvku - stačí, že tam nějaká svislá čára je.
        using var bmp = Vykresli(prvek, data);
        int y = bmp.Height / 2;
        for (int x = bmp.Width / 2; x < bmp.Width; x++)
            if (JeCara(bmp.GetPixel(x, y))) return true;
        return false;
    }

    /// <summary>Počet <b>tmavých</b> pixelů = inkoust textu a čar.</summary>
    /// <remarks>
    /// ⚠️ Ne „nebílých": pás skupin má <b>šedé pozadí</b>, takže jeho pixely nebílé už jsou
    /// a černý nadpis na nich by počet nebílých nezměnil. První verze testu na to naletěla
    /// a hlásila „nadpis se nevykreslil", i když se kreslil.
    /// </remarks>
    private static int Pixely(LineTableElement prvek, DataContext data)
    {
        using var bmp = Vykresli(prvek, data);
        int n = 0;
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
                if (JeCara(bmp.GetPixel(x, y))) n++;
        return n;
    }



    private static bool JeCara(SKColor c) => c.Red < 100 && c.Green < 100 && c.Blue < 100;

    private static SKBitmap Vykresli(LineTableElement prvek, DataContext data)
    {
        int w = (int)((prvek.Width + 2f) * MmToPx);
        int h = (int)((prvek.CalculateHeightFromData(data) + 10f) * MmToPx);
        var bmp = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.White);
        prvek.Render(new RenderContext(canvas, MmToPx, data));
        return bmp;
    }
}
