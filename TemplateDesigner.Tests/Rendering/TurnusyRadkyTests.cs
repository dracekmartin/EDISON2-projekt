using System.Text.Json.Nodes;
using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Rendering;

/// <summary>
/// Turnusy ke spojům (`L6c`) - řádky s označením turnusu <b>pod čísly spojů</b>.
/// </summary>
/// <remarks>
/// Řádků je tolik, kolik turnusů má <b>nejbohatší</b> spoj; spoje s méně turnusy mají zbylé řádky
/// prázdné. Pořadí řádků hlavy je stejné jako ve starém modulu: číslo spoje → turnusy → platnost →
/// negativní značky (indexy `1`, `1.001xx`, `1.1`, `1.9`, `JRapiLinPrtWin.mac:2948`).
/// <para>⚠️ Turnusy posílá server jen se zapnutým parametrem „Turnusy ke spojům" (dotaz do modulu
/// turnusů stojí desítky až stovky ms), takže <b>bez něj musí tabulka vypadat jako dosud</b> - to
/// hlídá poslední test.</para>
/// <para>Fixtury jsou schválně skoro prázdné (čísla spojů i časy prázdné), aby „kde je inkoust"
/// znamenalo „kde je turnus". Názvy zastávek ale prázdné být <b>nesmí</b>: prázdný sloupec se
/// netiskne, a s ním by se posunuly i sloupce spojů, které se měří.</para>
/// </remarks>
public class TurnusyRadkyTests
{
    private const float MmToPx = 10f;
    private const float SirkaZastavky = 30f;
    private const float SirkaSpoje = 12f;

    private static LineTableElement Prvek() => new()
    {
        X = 0, Y = 0, Width = 120, Height = null,
        HeaderStyle = new TextStyle { FontFamily = "Arial", FontSize = 7 },
        DataStyle = new TextStyle { FontFamily = "Arial", FontSize = 6 },
        HighlightOddTc = false,
        ShowGroupHeaders = false,
        Grid = new GridStyle { ShowColumnLines = true, ShowRowLines = true, Color = "#FFFFFFFF", Width = 0.2f },
        Columns =
        [
            new StopColumn { Key = ColumnKeys.StopName, Header = "", Width = SirkaZastavky, Visible = true },
            new StopColumn { Key = LineTableElement.TripKey, Header = "", Width = SirkaSpoje,
                Visible = true, Alignment = TextHorizontalAlignment.Center },
            new StopColumn { Key = LineTableElement.TripKey, Header = "", Width = SirkaSpoje,
                Visible = true, Alignment = TextHorizontalAlignment.Center },
        ],
    };

    /// <summary>
    /// Fixtura o dvou spojích. <paramref name="turnusy1"/> / <paramref name="turnusy2"/> = kolik
    /// turnusů má který spoj (0 = klíč `TURNUSY` vůbec nepřijde, jako od serveru s vypnutým
    /// parametrem), <paramref name="platnost1"/> = značka platnosti u prvního spoje.
    /// </summary>
    private static DataContext Data(int turnusy1, int turnusy2,
        bool platnost1 = false, bool prazdnePole = false)
    {
        static DataContext Spoj(int turnusu, bool platnost, bool prazdnePole, string oznaceni)
        {
            var polozky = new List<(string, object?)>
            {
                ("CISLO_SPOJE", ""),
                ("SKUPINA", "1"),
                ("SMER", "T"),
                ("CASY", new JsonArray("", "", "")),
            };
            if (platnost) polozky.Add(("PLATNOST", new JsonArray("X")));
            if (turnusu > 0 || prazdnePole)
            {
                var arr = new JsonArray();
                for (int i = 1; i <= turnusu; i++) arr.Add($"{oznaceni}.0{i}");
                polozky.Add(("TURNUSY", arr));
            }
            return RenderSnapshot.Row([.. polozky]);
        }

        return RenderSnapshot.Data(
            ("ZASTAVKY",
            [
                RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Kroměříž"), ("PORADI", "1")),
                RenderSnapshot.Row(("NAZEV_ZASTAVKY", "Přerov"), ("PORADI", "2")),
            ]),
            ("SPOJE",
            [
                Spoj(turnusy1, platnost1, prazdnePole, "110"),
                Spoj(turnusy2, false, prazdnePole, "220"),
            ]),
            ("SKUPINY_DNU", [RenderSnapshot.Row(("ID", "1"), ("NADPIS", ""), ("PORADI", "1"))]));
    }

    /// <summary>
    /// Řádků turnusů je tolik, kolik má <b>nejbohatší</b> spoj - ne kolik má první nebo nejhubenější.
    /// </summary>
    /// <remarks>
    /// Měří se přírůstkem výšky, aby test nemusel znát výšku řádku: přírůstek za „nejbohatší spoj
    /// má tři turnusy" musí být přesně trojnásobek přírůstku za jeden. Kdyby se počet bral z prvního
    /// spoje nebo jako minimum, vyjde u obou fixtur stejná výška.
    /// </remarks>
    [Fact]
    public void RadkuJeTolik_KolikMaNejbohatsiSpoj()
    {
        float bez = Prvek().CalculateHeightFromData(Data(0, 0));
        float jeden = Prvek().CalculateHeightFromData(Data(1, 1));
        float tri = Prvek().CalculateHeightFromData(Data(1, 3));

        float vyskaRadku = jeden - bez;
        Assert.True(vyskaRadku > 0.5f,
            $"s turnusy má tabulka narůst o řádek, narostla o {vyskaRadku:0.###} mm "
            + $"(bez={bez:0.###}, jeden={jeden:0.###}, tři={tri:0.###})");
        Assert.Equal(3 * vyskaRadku, tri - bez, 0.05f);
    }

    /// <summary>Turnus se kreslí <b>do sloupce svého spoje</b>, ne přes celou tabulku.</summary>
    /// <remarks>
    /// V obrázku má turnus jen druhý spoj, takže veškerý inkoust napravo od zastávek musí ležet
    /// v jeho sloupci. Kdyby se turnusy kreslily bez ohledu na sloupec, sahal by inkoust i do
    /// prvního sloupce spojů.
    /// </remarks>
    [Fact]
    public void TurnusJeVeSloupciSvehoSpoje()
    {
        using var bmp = Vykresli(Prvek(), Data(0, 1));

        (float od, float @do) = InkoustXRozsahMm(bmp, SirkaZastavky + 0.5f, 120f);

        Assert.True(od >= 0f, "turnus se nenakreslil vůbec");
        Assert.True(od > SirkaZastavky + SirkaSpoje - 0.5f,
            $"inkoust začíná na {od:0.##} mm - zasahuje do sloupce prvního spoje "
            + $"(ten končí na {SirkaZastavky + SirkaSpoje} mm)");
        Assert.True(@do < SirkaZastavky + 2 * SirkaSpoje + 0.5f,
            $"inkoust sahá na {@do:0.##} mm - přetéká za sloupec druhého spoje");
    }

    /// <summary>
    /// Řádek turnusu je <b>nad</b> řádkem platnosti - stejné pořadí jako ve starém modulu.
    /// </summary>
    /// <remarks>
    /// První spoj má značku platnosti, druhý turnus, každý ve svém sloupci - stačí tedy porovnat,
    /// který inkoust je výš. Kdyby se turnusy zařadily za platnost (nebo až za negativní značky),
    /// pořadí se obrátí.
    /// </remarks>
    [Fact]
    public void RadekTurnusu_JeNadRadkemPlatnosti()
    {
        using var bmp = Vykresli(Prvek(), Data(0, 1, platnost1: true));

        float yPlatnost = PrvniInkoustYMm(bmp, SirkaZastavky + 0.5f, SirkaZastavky + SirkaSpoje);
        float yTurnus = PrvniInkoustYMm(bmp, SirkaZastavky + SirkaSpoje + 0.5f,
            SirkaZastavky + 2 * SirkaSpoje);

        Assert.True(yPlatnost >= 0f, "značka platnosti se nenakreslila");
        Assert.True(yTurnus >= 0f, "turnus se nenakreslil");
        Assert.True(yTurnus < yPlatnost - 0.3f,
            $"turnus je na {yTurnus:0.##} mm, značka platnosti na {yPlatnost:0.##} mm - "
            + "turnus musí být nad platností");
    }

    /// <summary>
    /// Bez turnusů (i s prázdným polem) je tabulka <b>vysoká jako dosud</b> - žádný prázdný řádek.
    /// </summary>
    /// <remarks>
    /// Tohle je ta podstatná pojistka: turnusy chodí jen se zapnutým parametrem, takže naprostá
    /// většina sestav klíč `TURNUSY` nikdy neuvidí a nesmí se jim nic změnit. Prázdné pole
    /// (spoj bez turnusu na lince, která je má) se musí chovat stejně jako chybějící klíč.
    /// </remarks>
    [Fact]
    public void BezTurnusu_ZadnyRadekNevznikne()
    {
        var prvek = Prvek();
        float bezKlice = prvek.CalculateHeightFromData(Data(0, 0));
        float prazdnePole = prvek.CalculateHeightFromData(Data(0, 0, prazdnePole: true));

        Assert.Equal(bezKlice, prazdnePole, 0.001f);
    }

    /// <summary>
    /// Spoj s méně turnusy má zbylé řádky <b>prázdné</b> - turnus se v nich neopakuje.
    /// </summary>
    /// <remarks>
    /// První spoj má jeden turnus, druhý dva. Ve druhém řádku turnusů tedy smí být inkoust jen
    /// ve sloupci druhého spoje - kdyby se dorovnávalo opakováním, měly by oba sloupce inkoust
    /// stejně hluboko.
    /// </remarks>
    [Fact]
    public void SpojSMeneTurnusy_MaZbylyRadekPrazdny()
    {
        using var bmp = Vykresli(Prvek(), Data(1, 2));

        float dolniHrana1 = PosledniInkoustYMm(bmp, SirkaZastavky + 0.5f, SirkaZastavky + SirkaSpoje);
        float dolniHrana2 = PosledniInkoustYMm(bmp, SirkaZastavky + SirkaSpoje + 0.5f,
            SirkaZastavky + 2 * SirkaSpoje);

        Assert.True(dolniHrana1 >= 0f, "turnus prvního spoje se nenakreslil");
        Assert.True(dolniHrana2 > dolniHrana1 + 0.3f,
            $"spoj s jedním turnusem má inkoust do {dolniHrana1:0.##} mm, spoj se dvěma do "
            + $"{dolniHrana2:0.##} mm - druhý řádek turnusů má být prázdný jen u prvního spoje");
    }

    // ── měření ────────────────────────────────────────────────────────────────

    private static bool JeInkoust(SKColor c) => c.Red < 100 && c.Green < 100 && c.Blue < 100;

    /// <summary>Vodorovný rozsah inkoustu (mm) v zadaném pásu; `(-1, -1)` když tam žádný není.</summary>
    private static (float Od, float Do) InkoustXRozsahMm(SKBitmap bmp, float odMm, float doMm)
    {
        float od = -1f, @do = -1f;
        int x1 = Math.Max(0, (int)(odMm * MmToPx));
        int x2 = Math.Min(bmp.Width - 1, (int)(doMm * MmToPx));
        for (int x = x1; x <= x2; x++)
            for (int y = 0; y < bmp.Height; y++)
                if (JeInkoust(bmp.GetPixel(x, y)))
                {
                    if (od < 0f) od = x / MmToPx;
                    @do = x / MmToPx;
                    break;
                }
        return (od, @do);
    }

    /// <summary>Y prvního tmavého pixelu (mm) ve svislém pásu daném X rozsahem; -1 když žádný.</summary>
    private static float PrvniInkoustYMm(SKBitmap bmp, float odMm, float doMm)
    {
        int x1 = Math.Max(0, (int)(odMm * MmToPx));
        int x2 = Math.Min(bmp.Width - 1, (int)(doMm * MmToPx));
        for (int y = 0; y < bmp.Height; y++)
            for (int x = x1; x <= x2; x++)
                if (JeInkoust(bmp.GetPixel(x, y))) return y / MmToPx;
        return -1f;
    }

    /// <summary>Y posledního tmavého pixelu (mm) ve svislém pásu daném X rozsahem; -1 když žádný.</summary>
    private static float PosledniInkoustYMm(SKBitmap bmp, float odMm, float doMm)
    {
        int x1 = Math.Max(0, (int)(odMm * MmToPx));
        int x2 = Math.Min(bmp.Width - 1, (int)(doMm * MmToPx));
        for (int y = bmp.Height - 1; y >= 0; y--)
            for (int x = x1; x <= x2; x++)
                if (JeInkoust(bmp.GetPixel(x, y))) return y / MmToPx;
        return -1f;
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
