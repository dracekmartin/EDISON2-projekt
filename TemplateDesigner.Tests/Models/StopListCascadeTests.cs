using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// Regresní testy na StopList-specifické cascade wiring (analogie DepartureGridCascadeTests, ale
/// StopList má jiné rizikové konstrukce než DepartureGrid - proto vlastní testy, ne sdílené):
///  1. feature override aktuální / projeté zastávky (ResolveRowStyle) - musí přebít styl JEN když
///     je řádek current/passed (aktivováno runtime stavem, ne konfigurací),
///  2. legacy col.Alignment fallback (ResolveHeaderStyle/DataStyle) - platí jen když kaskáda HAlign
///     nenastaví,
///  3. element-level HeaderStyle/DataStyle kaskáda.
/// </summary>
[Collection("StyleCascade")]
public class StopListCascadeTests
{
    private static StopColumn Col(TextHorizontalAlignment align = TextHorizontalAlignment.Left,
        ColumnVisualStyle? style = null) =>
        new() { Key = "NAZEV", Header = "Zastávka", Alignment = align, Style = style };

    // ── ResolveRowStyle: feature override aktuální zastávky ──────────────────────

    [Fact]
    public void CurrentStop_PrebijeBold_KdyzCurrentStopBold()
    {
        var el = new StopListElement { CurrentStopBold = true };
        var col = Col();

        // Aktuální zastávka → Bold vždy true (CurrentStopBold || resolved.Bold).
        Assert.True(el.ResolveRowStyle(col, isCurrent: true, isPassed: false).Bold);
    }

    [Fact]
    public void CurrentStopBoldOff_NechaBoldNaKaskade()
    {
        var el = new StopListElement { CurrentStopBold = false };
        var col = Col();

        // CurrentStopBold=false → override nenutí Bold; padne na hodnotu z kaskády (jako non-current).
        var expected = el.ResolveDataStyle(col).Bold;
        Assert.Equal(expected, el.ResolveRowStyle(col, isCurrent: true, isPassed: false).Bold);
    }

    [Fact]
    public void CurrentStopFontSize_PrebijeKdyzNastaveno_JinakDedi()
    {
        var col = Col();

        var withOverride = new StopListElement { CurrentStopFontSize = 14f };
        Assert.Equal(14f, withOverride.ResolveRowStyle(col, isCurrent: true, isPassed: false).FontSize);

        var noOverride = new StopListElement { CurrentStopFontSize = null };
        Assert.Equal(noOverride.ResolveDataStyle(col).FontSize,
                     noOverride.ResolveRowStyle(col, isCurrent: true, isPassed: false).FontSize);
    }

    [Fact]
    public void NonCurrentNonPassed_NeprebijeNic()
    {
        var el = new StopListElement();
        var col = Col();

        var data = el.ResolveDataStyle(col);
        var row  = el.ResolveRowStyle(col, isCurrent: false, isPassed: false);

        // Bez runtime stavu (current/passed) = žádný override → shodné s ResolveDataStyle.
        Assert.Equal(data.Bold, row.Bold);
        Assert.Equal(data.Underline, row.Underline);
        Assert.Equal(data.FontSize, row.FontSize);
        Assert.Equal(data.TextColor, row.TextColor);
    }

    [Fact]
    public void PassedStop_PrebijeBarvu()
    {
        var el = new StopListElement { PassedStopsForeColor = "#FF808080" };
        var col = Col();

        Assert.Equal("#FF808080", el.ResolveRowStyle(col, isCurrent: false, isPassed: true).TextColor);
    }

    // ── ResolveHeaderStyle/DataStyle: legacy col.Alignment fallback ──────────────

    [Fact]
    public void ColAlignment_FallbackKdyzKaskadaHAlignNenastavi()
    {
        var el = new StopListElement();               // HeaderStyle.HAlign = null (default)
        var col = Col(TextHorizontalAlignment.Right); // col.Style = null

        Assert.Equal(TextHorizontalAlignment.Right, el.ResolveHeaderStyle(col).HAlign);
        Assert.Equal(TextHorizontalAlignment.Right, el.ResolveDataStyle(col).HAlign);
    }

    [Fact]
    public void ColAlignment_NeprebijeElementHAlign()
    {
        var el = new StopListElement();
        el.HeaderStyle.HAlign = TextHorizontalAlignment.Center;   // kaskáda nastaví HAlign
        var col = Col(TextHorizontalAlignment.Right);             // legacy col.Alignment se ignoruje

        Assert.Equal(TextHorizontalAlignment.Center, el.ResolveHeaderStyle(col).HAlign);
    }

    [Fact]
    public void ColStyleHAlign_MaPrednostPredColAlignment()
    {
        var el = new StopListElement();
        var col = Col(TextHorizontalAlignment.Right,
            new ColumnVisualStyle { Header = new TextStyle { HAlign = TextHorizontalAlignment.Center } });

        // Per-column Style.Header.HAlign je nejspecifičtější vrstva → přebije col.Alignment.
        Assert.Equal(TextHorizontalAlignment.Center, el.ResolveHeaderStyle(col).HAlign);
    }

    // ── Element-level kaskáda ────────────────────────────────────────────────────

    [Fact]
    public void HeaderStyleFontSize_PromitneSeDoResolveColumnHeaderParent()
    {
        var el = new StopListElement();
        el.HeaderStyle.FontSize = 13f;

        Assert.Equal(13f, el.ResolveColumnHeaderParent().FontSize);
    }
}
