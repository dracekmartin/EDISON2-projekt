using SkiaSharp;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;
using TemplateDesigner.Tests.Rendering;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// Sloučení buněk pevné tabulky vodorovně - z několika buněk v řádku jedna, typicky nadpis přes
/// celou šířku. Hlídá tři věci: že se **obsah neztratí** (spojí se), že sloučená buňka je široká
/// přes své sloupce i pro myš, a že to jde jedním krokem vrátit.
/// </summary>
public class FixedTableMergeTests
{
    private const float MmToPx = 4f;

    private static TableElement Grid(int cols = 3, int rows = 2)
    {
        var t = TableElement.CreateGrid(cols, rows);
        t.Width  = 30f * cols;
        t.Height = 20f * rows;
        return t;
    }

    private static CellRange Range(int row, int fromCol, int toCol) =>
        new(row, fromCol, row, toCol);

    // ── Obsah ─────────────────────────────────────────────────────────────────

    [Fact]
    public void SlouceniSpojiNeprazdneObsahy_AbySeNicNeztratilo()
    {
        var table = Grid();
        table.EnsureCell(null, 0, 0)!.Content = "Jízdní";
        table.EnsureCell(null, 0, 2)!.Content = "řád";

        Assert.True(table.MergeRange(null, Range(0, 0, 2)));

        Assert.Equal("Jízdní řád", table.EnsureCell(null, 0, 0)!.Content);
        Assert.Equal(3, table.EnsureCell(null, 0, 0)!.ColSpan);
        Assert.Equal("", table.EnsureCell(null, 0, 1)!.Content);
        Assert.Equal("", table.EnsureCell(null, 0, 2)!.Content);
    }

    [Fact]
    public void SlouceniPresViceRadku_SloucujeKazdyRadekZvlast()
    {
        var table = Grid(cols: 3, rows: 2);
        table.EnsureCell(null, 0, 0)!.Content = "A";
        table.EnsureCell(null, 1, 0)!.Content = "B";

        table.MergeRange(null, new CellRange(0, 0, 1, 1));   // dva řádky × dva sloupce

        Assert.Equal(2, table.EnsureCell(null, 0, 0)!.ColSpan);
        Assert.Equal(2, table.EnsureCell(null, 1, 0)!.ColSpan);
        Assert.Equal("A", table.EnsureCell(null, 0, 0)!.Content);
        Assert.Equal("B", table.EnsureCell(null, 1, 0)!.Content);
        Assert.Equal(1, table.EnsureCell(null, 0, 2)!.ColSpan);   // třetí sloupec zůstal
    }

    [Fact]
    public void SlouceniJedineBunky_SeNekona()
    {
        Assert.False(Grid().MergeRange(null, Range(0, 1, 1)));
    }

    [Fact]
    public void RozdeleniVratiSloupce_ObsahZustaneVPrvniBunce()
    {
        var table = Grid();
        table.EnsureCell(null, 0, 0)!.Content = "Nadpis";
        table.MergeRange(null, Range(0, 0, 2));

        Assert.True(table.SplitRange(null, Range(0, 0, 2)));

        Assert.Equal(1, table.EnsureCell(null, 0, 0)!.ColSpan);
        Assert.Equal("Nadpis", table.EnsureCell(null, 0, 0)!.Content);
    }

    [Fact]
    public void RozdeleniBezSloucenych_NehlasiZmenu()
    {
        Assert.False(Grid().SplitRange(null, Range(0, 0, 2)));
        Assert.False(Grid().HasMergedCell(null, Range(0, 0, 2)));
    }

    // ── Geometrie a myš ───────────────────────────────────────────────────────

    [Fact]
    public void SloucenaBunkaJeSirokaPresSveSloupce()
    {
        var table = Grid();
        float sirkaJedne = table.CellRectMm(null, 0, 0)!.Value.Width;

        table.MergeRange(null, Range(0, 0, 2));

        var rect = table.CellRectMm(null, 0, 0)!.Value;
        Assert.Equal(sirkaJedne * 3, rect.Width, 3);
    }

    /// <summary>Klik doprostřed sloučeného nadpisu musí vybrat nadpis, ne buňku, kterou překryl -
    /// tu uživatel na plátně vůbec nevidí.</summary>
    [Fact]
    public void KlikDoPrekrityBunky_VybereTuSloucenou()
    {
        var table = Grid();
        table.MergeRange(null, Range(0, 0, 2));

        var stred = table.CellRectMm(null, 0, 1)!.Value;
        var hit = table.HitTestCell(null, stred.MidX, stred.MidY);

        Assert.Equal((0, 0), hit);
        Assert.Equal(3, table.SpanAt(null, 0, 0));
        Assert.Equal(0, table.SpanAnchorColumn(null, 0, 2));
    }

    [Fact]
    public void NesloucenaTabulka_MaKazdouBunkuSamaSobeKotvou()
    {
        var table = Grid();

        Assert.Equal(2, table.SpanAnchorColumn(null, 0, 2));
        Assert.Equal(1, table.SpanAt(null, 0, 2));
    }

    // ── Vratnost a kreslení ───────────────────────────────────────────────────

    [Fact]
    public void ZpetnyKrok_VratiSloupceIRozdelenyText()
    {
        var table = Grid();
        table.EnsureCell(null, 0, 0)!.Content = "Jízdní";
        table.EnsureCell(null, 0, 1)!.Content = "řád";

        var formatting = new TableCellFormatting(table, () => null, [.. Range(0, 0, 2).Cells()]);
        var before = formatting.Capture();
        table.MergeRange(null, Range(0, 0, 2));
        var command = formatting.Command("Sloučení buněk", before);

        command.Undo();

        Assert.Equal("Jízdní", table.EnsureCell(null, 0, 0)!.Content);
        Assert.Equal("řád", table.EnsureCell(null, 0, 1)!.Content);
        Assert.Equal(1, table.EnsureCell(null, 0, 0)!.ColSpan);
    }

    /// <summary>Sloučený řádek se kreslí jako jedna buňka - uvnitř nadpisu tedy nesmí zůstat
    /// svislá čára mřížky, jinak by sloučení nebylo vidět.</summary>
    [Fact]
    public void SloucenyRadek_NemaUvnitrSvislouCaru()
    {
        var table = Grid(cols: 3, rows: 1);
        table.Grid.ShowColumnLines = true;
        table.Grid.Color = "#FF000000";

        int predSloucenim = SvisleCary(table);
        table.MergeRange(null, Range(0, 0, 2));
        int poSlouceni = SvisleCary(table);

        Assert.True(predSloucenim > poSlouceni,
            $"čar uvnitř: před {predSloucenim}, po {poSlouceni} - sloučení je nemá překreslit");
        Assert.Equal(0, poSlouceni);
    }

    /// <summary>Počet svislých čar uvnitř tabulky (mimo vnější rámeček) v polovině výšky řádku.</summary>
    private static int SvisleCary(TableElement table)
    {
        var bitmap = new SKBitmap((int)(table.Width * MmToPx), (int)((table.Height ?? 20f) * MmToPx),
            SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        table.Render(new RenderContext(canvas, MmToPx, null));

        int y = bitmap.Height / 2, cary = 0;
        bool vCare = false;
        for (int x = 2; x < bitmap.Width - 2; x++)
        {
            bool tmavy = bitmap.GetPixel(x, y).Red < 200;
            if (tmavy && !vCare) cary++;
            vCare = tmavy;
        }
        return cary;
    }
}
