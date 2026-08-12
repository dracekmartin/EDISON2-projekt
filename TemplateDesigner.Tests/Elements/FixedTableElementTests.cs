using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Tests.Rendering;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Pevná (ručně vyplňovaná) tabulka po oživení z palety. Hlídá to, co se při převodu na sdílený
/// plán řádků reálně změnilo v chování - ne odtisk pixelů (ten je vázaný na fonty stroje, viz
/// <see cref="ElementRenderSnapshotTests"/>).
/// </summary>
public class FixedTableElementTests
{
    private const float MmToPx = 4f;

    /// <summary>Vykreslí prvek na bílé plátno a vrátí bitmapu k ohledání jednotlivých pixelů.</summary>
    private static SKBitmap Render(TemplateElement element, DataContext? data = null,
        float widthMm = 120f, float heightMm = 60f)
    {
        var bitmap = new SKBitmap((int)(widthMm * MmToPx), (int)(heightMm * MmToPx),
            SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        var ctx = new RenderContext(canvas, MmToPx, data);
        element.RenderBackgroundPass(ctx);
        element.Render(ctx);
        element.RenderBorderPass(ctx);
        return bitmap;
    }

    private static SKColor At(SKBitmap b, float xMm, float yMm) =>
        b.GetPixel((int)(xMm * MmToPx), (int)(yMm * MmToPx));

    private static int NonWhitePixels(SKBitmap b)
    {
        int n = 0;
        for (int y = 0; y < b.Height; y++)
            for (int x = 0; x < b.Width; x++)
                if (b.GetPixel(x, y) != SKColors.White) n++;
        return n;
    }

    // ── Vložení z palety ──────────────────────────────────────────────────────

    /// <summary>Tabulka bez sloupců se dřív nevykreslila vůbec - uživatel po vložení neviděl nic
    /// a nevěděl, kam kliknul. Nově kreslí naváděcí rámeček s textem.</summary>
    [Fact]
    public void TabulkaBezSloupcu_JePrestoVidet()
    {
        var el = new TableElement { X = 5, Y = 5, Width = 80, Height = 20, Mode = TableMode.Fixed };

        using var bitmap = Render(el);

        Assert.True(NonWhitePixels(bitmap) > 0, "prázdná tabulka musí být na plátně vidět");
    }

    /// <summary>Nová mřížka z palety má co kreslit hned - bez řádků a sloupců by byla prázdná plocha.</summary>
    [Fact]
    public void NovaMrizka_MaBunkyKVyplneni()
    {
        var el = TableElement.CreateGrid(columns: 3, rows: 3);

        Assert.Equal(TableMode.Fixed, el.Mode);
        Assert.All(el.Rows!, r => Assert.Equal(3, r.Cells.Count));
        Assert.All(el.Columns, c => Assert.Equal(0f, c.Width));   // auto = rovnoměrné rozdělení šířky
    }

    // ── Obsah buňky ───────────────────────────────────────────────────────────

    /// <summary>Buňka se vyhodnocuje jako text s poli: „{CISLO_LINKY}" musí dát tentýž obraz jako
    /// natvrdo napsaná hodnota. Kdyby se dosazování ztratilo, uživateli se vytiskne složená závorka.</summary>
    [Fact]
    public void PoleVBunce_SeDosadiZDat()
    {
        var data = new DataContext(new Dictionary<string, object?> { ["CISLO_LINKY"] = "42" });

        var sPolem = TableElement.CreateGrid(2, 1);
        sPolem.X = 5; sPolem.Y = 5; sPolem.Width = 80;
        sPolem.Rows![0].Cells[0].Content = "{CISLO_LINKY}";

        var sHodnotou = TableElement.CreateGrid(2, 1);
        sHodnotou.X = 5; sHodnotou.Y = 5; sHodnotou.Width = 80;
        sHodnotou.Rows![0].Cells[0].Content = "42";

        using var a = Render(sPolem, data);
        using var b = Render(sHodnotou, data);

        Assert.Equal(NonWhitePixels(b), NonWhitePixels(a));
    }

    // ── Pozadí řádku ──────────────────────────────────────────────────────────

    /// <summary>
    /// Vlastní pozadí řádku **přebíjí** střídání řádků. Dřív se kreslila obě přes sebe, takže
    /// průsvitná vlastní barva propouštěla zebru pod sebou a výsledek byl jiný, než jaký si
    /// uživatel nastavil.
    /// </summary>
    [Fact]
    public void VlastniPozadiRadku_PrebijiStridaniRadku()
    {
        var el = TableElement.CreateGrid(1, 2);
        el.X = 0; el.Y = 0; el.Width = 100;
        el.AlternatingRowColor = "#FFFF0000";              // sytě červená zebra na lichém řádku
        el.Rows![1].BackgroundColor = "#800000FF";         // poloprůhledná modrá přes něj

        using var bitmap = Render(el);

        // Střed druhého řádku (RowHeight 6 mm): modrá přes bílou, ne přes červenou.
        var px = At(bitmap, xMm: 50f, yMm: 9f);
        Assert.True(px.Blue > 200, $"pozadí řádku má být modré (bylo {px})");
        Assert.True(px.Green > 100, $"pod modrou nesmí prosvítat červená zebra (bylo {px})");
    }

    // ── Mřížka pro editaci na plátně ──────────────────────────────────────────

    /// <summary>Trefování buňky myší musí sedět na to, co je vidět - tedy i po zmenšení a
    /// roztažení. Proto počítá totéž rozvržení jako kreslení.</summary>
    [Fact]
    public void TrefeniBunky_SediNaRoztazenouTabulku()
    {
        var el = TableElement.CreateGrid(2, 3);
        el.Width = 100; el.Height = 36f;   // 3 řádky po 6 mm roztažené na 12 mm

        Assert.Equal((0, 0), el.HitTestCell(null, 10f, 1f));
        Assert.Equal((1, 0), el.HitTestCell(null, 10f, 13f));
        Assert.Equal((2, 1), el.HitTestCell(null, 60f, 30f));
        Assert.Null(el.HitTestCell(null, 10f, 50f));    // pod tabulkou
    }

    /// <summary>Obdélník buňky je tentýž, který se kreslí - jinak by textové pole při psaní
    /// nesedělo na buňku.</summary>
    [Fact]
    public void ObdelnikBunky_OdpovidaRozvrzeni()
    {
        var el = TableElement.CreateGrid(2, 2);
        el.Width = 100; el.Height = 12f;

        var rect = el.CellRectMm(null, 1, 1);

        Assert.NotNull(rect);
        Assert.Equal(50f, rect!.Value.Left, 3);
        Assert.Equal(6f,  rect.Value.Top, 3);
        Assert.Equal(100f, rect.Value.Right, 3);
    }

    /// <summary>
    /// Editace obsahu nesmí zahodit formát ani styl buňky. Dřív se řádky při potvrzení dialogu
    /// skládaly znovu a tyhle vlastnosti se tiše ztrácely (nejvíc u tabulek z připravených bloků
    /// agendy, kde Caché posílá formát per buňku). Psaní na plátně edituje **tutéž instanci**,
    /// takže se ztratit nemají z principu - tenhle test to drží.
    /// </summary>
    [Fact]
    public void EditaceObsahu_NezahodiFormatAniStylBunky()
    {
        var el = TableElement.CreateGrid(2, 1);
        el.Rows![0].Cells[1].Format = "dd.MM.yyyy";
        el.Rows[0].Cells[1].Style = new TextStyle { Bold = true, TextColor = "#FFFF0000" };

        var cell = el.EnsureCell(null, 0, 1);
        cell!.Content = "{DATUM_OD}";

        Assert.Same(el.Rows[0].Cells[1], cell);
        Assert.Equal("dd.MM.yyyy", el.Rows[0].Cells[1].Format);
        Assert.True(el.Rows[0].Cells[1].Style?.Bold);
        Assert.Equal("#FFFF0000", el.Rows[0].Cells[1].Style?.TextColor);
    }

    /// <summary>Zpětný krok po změně struktury musí zachovat i formát a styl buněk - snímek se
    /// dělá klonem, takže by se dal snadno pokazit mělkou kopií.</summary>
    [Fact]
    public void SnimekStruktury_ZachovaFormatIStylBunky()
    {
        var el = TableElement.CreateGrid(2, 1);
        el.Rows![0].Cells[1].Format = "N2";
        el.Rows[0].Cells[1].Style = new TextStyle { Bold = true };

        var pred = el.CaptureStructure();
        el.InsertRow(0);
        el.RestoreStructure(pred);

        Assert.Equal("N2", el.Rows[0].Cells[1].Format);
        Assert.True(el.Rows[0].Cells[1].Style?.Bold);
    }

    /// <summary>Staré šablony mají řádky kratší než počet sloupců - do posledních sloupců by pak
    /// nešlo psát, kdyby se buňka nedoplnila.</summary>
    [Fact]
    public void ChybejiciBunka_SeDoplniPredEditaci()
    {
        var el = TableElement.CreateGrid(3, 1);
        el.Rows![0].Cells.RemoveAt(2);

        var cell = el.EnsureCell(null, 0, 2);

        Assert.NotNull(cell);
        Assert.Equal(3, el.Rows[0].Cells.Count);
    }

    // ── Řádky a sloupce ───────────────────────────────────────────────────────

    /// <summary>Přidání řádku tabulku **zvětší**, ne aby zmenšilo obsah - roste jako ve Wordu.</summary>
    [Fact]
    public void VlozeniRadku_ZvetsiTabulku()
    {
        var el = TableElement.CreateGrid(2, 3);
        el.Height = 18f;

        el.InsertRow(1);

        Assert.Equal(4, el.Rows!.Count);
        Assert.Equal(24f, el.Height);
        Assert.Equal(2, el.Rows[1].Cells.Count);   // nový řádek má buňky pro všechny sloupce
    }

    [Fact]
    public void SmazaniRadku_ZmensiTabulku()
    {
        var el = TableElement.CreateGrid(2, 3);
        el.Height = 18f;

        Assert.True(el.RemoveRow(0));
        Assert.Equal(2, el.Rows!.Count);
        Assert.Equal(12f, el.Height);
    }

    /// <summary>Poslední řádek ani sloupec nejde smazat - z prázdné tabulky by nebylo co vyplnit
    /// a uživatel by ji musel smazat a vložit znovu.</summary>
    [Fact]
    public void PosledniRadekASloupec_NejdouSmazat()
    {
        var el = TableElement.CreateGrid(1, 1);

        Assert.False(el.RemoveRow(0));
        Assert.False(el.RemoveColumn(0));
    }

    /// <summary>Vložený sloupec posune buňky, ne aby se obsah rozjel o jednu doleva.</summary>
    [Fact]
    public void VlozeniSloupce_PosuneObsahBunek()
    {
        var el = TableElement.CreateGrid(2, 1);
        el.Rows![0].Cells[0].Content = "vlevo";
        el.Rows[0].Cells[1].Content  = "vpravo";

        el.InsertColumn(1);

        Assert.Equal(3, el.Columns.Count);
        Assert.Equal("vlevo",  el.Rows[0].Cells[0].Content);
        Assert.Equal("",       el.Rows[0].Cells[1].Content);
        Assert.Equal("vpravo", el.Rows[0].Cells[2].Content);
    }

    /// <summary>Zpětný krok po změně struktury musí vrátit řádky, sloupce **i výšku** najednou -
    /// jinak by Ctrl+Z vrátil půl operace.</summary>
    [Fact]
    public void SnimekStruktury_VratiIVysku()
    {
        var el = TableElement.CreateGrid(2, 2);
        el.Height = 12f;
        el.Rows![0].Cells[0].Content = "text";

        var pred = el.CaptureStructure();
        el.InsertRow(0);
        el.InsertColumn(0);
        el.RestoreStructure(pred);

        Assert.Equal(2, el.Rows.Count);
        Assert.Equal(2, el.Columns.Count);
        Assert.Equal(12f, el.Height);
        Assert.Equal("text", el.Rows[0].Cells[0].Content);
    }

    // ── Vkládání ze schránky ──────────────────────────────────────────────────

    /// <summary>Excel dává do schránky řádky po koncích řádku a buňky po tabulátorech.</summary>
    [Fact]
    public void TextZeSchranky_SeRozsekaNaBunky()
    {
        var block = TableElement.ParseClipboardBlock("Linka\t42\r\nSměr\tBrno\r\n");

        Assert.Equal(2, block.Count);            // koncová prázdná řádka se zahodí
        Assert.Equal(["Linka", "42"], block[0]);
        Assert.Equal(["Směr", "Brno"], block[1]);
    }

    /// <summary>Vložený blok se nesmí tiše oříznout - tabulka se dorovná na jeho velikost.</summary>
    [Fact]
    public void VlozeniBloku_DorovnaTabulku()
    {
        var el = TableElement.CreateGrid(2, 2);
        el.Height = 12f;

        el.PasteBlock(1, 1, [["a", "b", "c"], ["d", "e", "f"]]);

        Assert.Equal(3, el.Rows!.Count);      // 1 + 2 řádky bloku
        Assert.Equal(4, el.Columns.Count);    // 1 + 3 sloupce bloku
        Assert.Equal("a", el.Rows[1].Cells[1].Content);
        Assert.Equal("f", el.Rows[2].Cells[3].Content);
        Assert.Equal(18f, el.Height);         // dorovnaný řádek tabulku zvětšil
    }

    /// <summary>Blok přepíše jen to, kam dosáhne - okolní buňky zůstanou.</summary>
    [Fact]
    public void VlozeniBloku_NechaOkolniBunkyBytNaPokoji()
    {
        var el = TableElement.CreateGrid(3, 2);
        el.Rows![0].Cells[2].Content = "zůstat";

        el.PasteBlock(0, 0, [["x"]]);

        Assert.Equal("x", el.Rows[0].Cells[0].Content);
        Assert.Equal("zůstat", el.Rows[0].Cells[2].Content);
    }

    // ── Čáry mřížky ───────────────────────────────────────────────────────────

    /// <summary>
    /// Čáry mřížky nesmí mizet podle toho, kam prvek zrovna padne. Tah užší než pixel se bez
    /// vyhlazení kreslí jen když trefí střed pixelu, takže při posunu prvku po plátně mřížka
    /// blikala (nahlášeno 12. 8.). Test posouvá tabulku po desetinách milimetru - na každé pozici
    /// musí být čar stejně.
    /// </summary>
    [Fact]
    public void CaryMrizky_NemiziPriPosunuPrvku()
    {
        var pocty = new List<int>();
        for (float posun = 0f; posun < 1f; posun += 0.1f)
        {
            var el = TableElement.CreateGrid(3, 3);
            el.X = 10f + posun; el.Y = 10f + posun; el.Width = 60; el.Height = 18;
            el.BorderWidth = 0f;   // ať měříme jen mřížku, ne rámeček prvku

            using var bitmap = Render(el);
            pocty.Add(NonWhitePixels(bitmap));
        }

        Assert.All(pocty, p => Assert.True(p > 0, "mřížka musí být vidět na každé pozici"));
    }

    // ── Výška a zmenšení ──────────────────────────────────────────────────────

    /// <summary>Bez pevné výšky (staré šablony, dopočet pro tisk) roste tabulka s počtem řádků.</summary>
    [Fact]
    public void BezPevneVysky_RostesPoctemRadku()
    {
        var el = TableElement.CreateGrid(2, 3);
        float triRadky = el.EffectiveDesignerHeight;

        el.Rows!.Add(new TableRow { Cells = [new TableCell(), new TableCell()] });

        Assert.Equal(triRadky + el.RowHeight, el.EffectiveDesignerHeight);
    }

    /// <summary>
    /// Roztažení prvku do výšky **natáhne řádky**, ne aby dole zbylo prázdné bílé místo
    /// (nahlášeno 12. 8.). 3 řádky po 6 mm ve výšce 30 mm = řádek 10 mm.
    /// </summary>
    [Fact]
    public void RoztazeniDoVysky_NatahneRadky()
    {
        var el = TableElement.CreateGrid(1, 3);
        el.X = 0; el.Y = 0; el.Width = 100; el.Height = 30f;
        el.Grid = new GridStyle { ShowRowLines = true, ShowColumnLines = true, Color = "#FF000000", Width = 0.5f };
        el.BorderWidth = 0f;

        using var bitmap = Render(el, heightMm: 60f);

        // Čára pod prvním řádkem je v 10 mm; v 6 mm (neroztažená výška) nesmí být nic.
        bool caraNaDesiti = false;
        for (float y = 9.6f; y <= 10.4f; y += 0.2f)
            if (At(bitmap, xMm: 50f, yMm: y) != SKColors.White) caraNaDesiti = true;
        Assert.True(caraNaDesiti, "řádky se mají roztáhnout na výšku prvku");

        for (float y = 5.6f; y <= 6.4f; y += 0.2f)
            Assert.Equal(SKColors.White, At(bitmap, xMm: 50f, yMm: y));
    }

    /// <summary>Písmo se roztažením prvku **nezvětšuje** - vyšší tabulka má mít vyšší řádky,
    /// ne větší text.</summary>
    [Fact]
    public void RoztazeniDoVysky_NezvetsujePismo()
    {
        static SKBitmap SText(float? vyska)
        {
            var el = TableElement.CreateGrid(1, 1);
            el.X = 0; el.Y = 0; el.Width = 100; el.Height = vyska;
            el.Grid = new GridStyle { ShowRowLines = false, ShowColumnLines = false, Color = "#FF000000" };
            el.BorderWidth = 0f;
            el.Rows![0].Cells[0].Content = "Nádraží";
            return Render(el, heightMm: 60f);
        }

        using var prirozena = SText(null);
        using var roztazena = SText(30f);

        Assert.Equal(NonWhitePixels(prirozena), NonWhitePixels(roztazena));
    }

    /// <summary>Skryté řádky (podmínka viditelnosti) se nepočítají do výšky - render i výpočet
    /// výšky jdou stejnou cestou, takže se nemůžou rozejít.</summary>
    [Fact]
    public void SkrytyRadek_SeNepocitaDoVysky()
    {
        var el = TableElement.CreateGrid(1, 2);
        el.Rows![1].VisibilityExpression = "{POCET} > 0";
        var data = new DataContext(new Dictionary<string, object?> { ["POCET"] = "0" });

        Assert.Equal(el.RowHeight, el.CalculateHeightFromData(data));
    }

    /// <summary>
    /// Při pevné výšce menší, než obsah potřebuje, se obsah **zmenší** (jako u ostatních tabulek).
    /// Dřív se přebytečné řádky jen ořízly, takže uživatel po zmenšení prvku o polovinu řádků
    /// tiše přišel. Měřeno na čarách mřížky: 6 řádků ve 12 mm = řádek 2 mm vysoký.
    /// </summary>
    [Fact]
    public void PriPevneVysce_SeObsahZmensi_MistoOrezu()
    {
        var el = TableElement.CreateGrid(1, 6);
        el.X = 0; el.Y = 0; el.Width = 100; el.Height = 12f;   // 6 řádků po 6 mm do 12 mm
        // Výrazná čára mřížky - vlásková by na hranici pixelu vypadla a test by měřil rasterizaci,
        // ne škálování.
        el.Grid = new GridStyle { ShowRowLines = true, ShowColumnLines = true, Color = "#FF000000", Width = 0.5f };
        foreach (var row in el.Rows!) row.Cells[0].Content = "Text";

        using var bitmap = Render(el);

        // Vodorovná čára pod prvním řádkem musí být ve 2 mm (neškálovaně by byla až v 6 mm).
        // x mimo text i mimo svislý rámeček, ať měříme opravdu jen čáru mřížky.
        bool caraNaDvouMm = false;
        for (float y = 1.6f; y <= 2.4f; y += 0.2f)
            if (At(bitmap, xMm: 95f, yMm: y) != SKColors.White) caraNaDvouMm = true;
        Assert.True(caraNaDvouMm, "obsah se má zmenšit, aby se všech 6 řádků vešlo do 12 mm");

        // A nic nesmí přetéct pod dolní hranu prvku.
        for (float y = 13f; y < 20f; y += 0.5f)
            Assert.Equal(SKColors.White, At(bitmap, xMm: 50f, yMm: y));
    }
}
