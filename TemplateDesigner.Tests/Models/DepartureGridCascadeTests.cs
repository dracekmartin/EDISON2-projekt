using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// Regresní testy na anti-pattern „scalar property přebíjí kaskádu" — historicky 2× opravený bug
/// (viz mvvm-patterns.md §Anti-pattern: scalar property override). Pravidlo: legacy scalar fallback
/// (MinuteFontSize, MinuteBold, SkupinaDnuHeaderLineHeight) smí platit JEN když žádná vrstva
/// kaskády (MinuteStyle + DataStyle / HeaderStyle) hodnotu nenastavila.
/// </summary>
[Collection("StyleCascade")]
public class DepartureGridCascadeTests
{
    // ── ResolveGroupMinuteParent: scalar MinuteFontSize/MinuteBold ───────────────

    [Fact]
    public void MinutyDefault_PouzijeScalarFallback()
    {
        var grid = new DepartureGridElement();   // MinuteFontSize=7, MinuteBold=true

        var p = grid.ResolveGroupMinuteParent();

        Assert.Equal(grid.MinuteFontSize, p.FontSize);
        Assert.Equal(grid.MinuteBold, p.Bold);
    }

    [Fact]
    public void DataStyleFontSize_NesmiBytPrebitScalarem()
    {
        // Přesně tenhle bug se vrátil 2×: user nastaví font v tabu Obecné (DataStyle),
        // ale render ho ignoroval, protože scalar MinuteFontSize přebil kaskádu.
        var grid = new DepartureGridElement
        {
            DataStyle = new TextStyle { FontSize = 12f },
            MinuteFontSize = 20f,   // scalar nesmí vyhrát
        };

        Assert.Equal(12f, grid.ResolveGroupMinuteParent().FontSize);
    }

    [Fact]
    public void MinuteStyleFontSize_MaNejvyssiPrioritu()
    {
        var grid = new DepartureGridElement
        {
            MinuteStyle = new TextStyle { FontSize = 9f },
            DataStyle = new TextStyle { FontSize = 12f },
            MinuteFontSize = 20f,
        };

        Assert.Equal(9f, grid.ResolveGroupMinuteParent().FontSize);
    }

    [Fact]
    public void DataStyleBoldFalse_NesmiBytPrebitScalarem()
    {
        // Explicitní false v kaskádě je hodnota, ne „dědit" — scalar MinuteBold=true nesmí vyhrát
        var grid = new DepartureGridElement
        {
            DataStyle = new TextStyle { Bold = false },
        };

        Assert.False(grid.ResolveGroupMinuteParent().Bold);
    }

    [Fact]
    public void MinutovePaddingy_DefaultNula_HustaMrizka()
    {
        var p = new DepartureGridElement().ResolveGroupMinuteParent();

        Assert.Equal(0f, p.PaddingLeft);
        Assert.Equal(0f, p.PaddingTop);
    }

    // ── ResolveGroupHeaderParent: scalar SkupinaDnuHeaderLineHeight ──────────────

    [Fact]
    public void HeaderStyleFontSize_PrebijeLegacyOdvozeniZLineHeight()
    {
        var grid = new DepartureGridElement
        {
            HeaderStyle = new TextStyle { FontSize = 11f },
        };

        Assert.Equal(11f, grid.ResolveGroupHeaderParent().FontSize);
    }

    [Fact]
    public void HeaderBezFontSize_OdvodiZeSkupinaDnuHeaderLineHeight()
    {
        var grid = new DepartureGridElement();
        float expected = grid.SkupinaDnuHeaderLineHeight * 0.65f / ResolvedTextStyle.PtToMm;

        Assert.Equal(expected, grid.ResolveGroupHeaderParent().FontSize, precision: 3);
    }

    [Fact]
    public void HeaderVAlign_ScalarFallbackJenKdyzKaskadaNenastavi()
    {
        var explicitVAlign = new DepartureGridElement
        {
            HeaderStyle = new TextStyle { VAlign = TextVerticalAlignment.Top },
            HeaderVerticalAlignment = TextVerticalAlignment.Bottom,
        };
        Assert.Equal(TextVerticalAlignment.Top, explicitVAlign.ResolveGroupHeaderParent().VAlign);

        var inherited = new DepartureGridElement
        {
            HeaderVerticalAlignment = TextVerticalAlignment.Bottom,
        };
        Assert.Equal(TextVerticalAlignment.Bottom, inherited.ResolveGroupHeaderParent().VAlign);
    }

    // ── Element-level parent (tab Obecné → resolve) ──────────────────────────────

    [Fact]
    public void HourData_DediZDataStyle()
    {
        var grid = new DepartureGridElement
        {
            DataStyle = new TextStyle { TextColor = "#FF336699" },
        };

        Assert.Equal("#FF336699", grid.ResolveHourDataParent().TextColor);
    }
}
