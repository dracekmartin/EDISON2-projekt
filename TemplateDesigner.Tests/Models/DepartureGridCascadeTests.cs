using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// Regresní testy na anti-pattern „scalar property přebíjí kaskádu" - historicky 2× opravený bug
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
        // Explicitní false v kaskádě je hodnota, ne „dědit" - scalar MinuteBold=true nesmí vyhrát
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

    // ── ResolveAnotaceTextStyle: scalar AnotaceFontSize ──────────────────────────

    [Fact]
    public void AnotaceText_DataStyleFontSize_NesmiBytPrebitScalarem()
    {
        // Třetí výskyt téhož bugu (nález review 4. 8.): AnotaceFontSize přebíjel
        // uživatelovu Velikost z tabu Obecné, guard kontroloval jen AnotaceTextStyle.
        var grid = new DepartureGridElement
        {
            DataStyle = new TextStyle { FontSize = 12f },
        };

        var s = grid.ResolveAnotaceTextStyle(grid.ResolveHourDataParent());

        Assert.Equal(12f, s.FontSize);
    }

    [Fact]
    public void AnotaceText_BezKaskady_PouzijeScalarFallback()
    {
        var grid = new DepartureGridElement();

        var s = grid.ResolveAnotaceTextStyle(grid.ResolveHourDataParent());

        Assert.Equal(grid.AnotaceFontSize, s.FontSize);
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

    // ── „Z čeho zóna dědí" - přístupový bod pro formulář ────────────────────────
    // Formulář musí u nenastavené vlastnosti nabídnout to, co dopadne z kaskády (jinak ukazuje
    // jednu hodnotu a tiskne se druhá). Aby to šlo strojově ověřit, má každá zóna metodu
    // „z čeho dědím" - a ta se nesmí rozejít s tím, co si spočítá kreslicí cesta.

    [Fact]
    public void MinutovaZona_JeVychozeTucna()
    {
        // Tohle je ta hodnota, kterou musí formulář nabídnout jako výchozí u „Minuta: Tučné"
        // (do 5. 8. nabízel nezaškrtnuto, přitom se minuty tučně tiskly).
        var grid = new DepartureGridElement();

        Assert.True(grid.ResolveElementMinuteParent().Bold);
        Assert.Equal(grid.MinuteFontSize, grid.ResolveElementMinuteParent().FontSize);
    }

    [Fact]
    public void ZChehoZonaDedi_SeNerozejdeSKreslicíCestou()
    {
        var grid = new DepartureGridElement();

        // Minutová zóna: kreslicí cesta resolvuje MinuteStyle nad tímtéž rodičem.
        Assert.Equal(grid.MinuteStyle.Resolve(grid.ResolveElementMinuteParent()).Bold,
                     grid.ResolveGroupMinuteParent().Bold);
        Assert.Equal(grid.MinuteStyle.Resolve(grid.ResolveElementMinuteParent()).FontSize,
                     grid.ResolveGroupMinuteParent().FontSize);

        // Text značek: dtto, jen se rodič skládá s vlastním skalárním fallbackem velikosti.
        Assert.Equal(grid.ResolveAnotaceTextStyle(grid.ResolveHourDataParent()).FontSize,
                     grid.AnotaceTextStyle.Resolve(grid.ResolveElementAnotaceTextParent()).FontSize);

        // Symboly značek dědí přímo datový styl prvku (velikost si dopočítá z minutové).
        Assert.Equal(grid.ResolveHourDataParent().TextColor,
                     grid.ResolveElementAnotaceSymbolParent().TextColor);
    }

    [Fact]
    public void ZChehoZonaDedi_RespektujeUzivateluvDatovyStyl()
    {
        var grid = new DepartureGridElement
        {
            DataStyle = new TextStyle { FontSize = 12f, Bold = false },
        };

        // Skalární fallback nesmí přebít, co uživatel nastavil v tabu Obecné.
        Assert.Equal(12f, grid.ResolveElementMinuteParent().FontSize);
        Assert.False(grid.ResolveElementMinuteParent().Bold);
        Assert.Equal(12f, grid.ResolveElementAnotaceTextParent().FontSize);
    }
}
