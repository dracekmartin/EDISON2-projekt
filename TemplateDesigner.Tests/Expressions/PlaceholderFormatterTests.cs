using System.Globalization;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Expressions;

namespace TemplateDesigner.Tests.Expressions;

public class PlaceholderFormatterTests
{
    private static readonly CultureInfo Cs = CultureInfo.GetCultureInfo("cs-CZ");
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    // ── Řetězcové funkce ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("praha", "UPPER", "PRAHA")]
    [InlineData("PRAHA", "LOWER", "praha")]
    [InlineData("  x  ", "TRIM", "x")]
    [InlineData("123456", "LEFT(4)", "1234")]
    [InlineData("12", "LEFT(4)", "12")]           // kratší než n → beze změny
    [InlineData("123456", "RIGHT(2)", "56")]
    [InlineData("7", "PADLEFT(3,'0')", "007")]
    [InlineData("7", "PADRIGHT(3)", "7  ")]
    public void RetezcoveFunkce(string value, string format, string expected)
    {
        Assert.Equal(expected, PlaceholderFormatter.Format(value, format));
    }

    // ── Čísla × kultura ──────────────────────────────────────────────────────

    [Fact]
    public void CiselnyFormat_CeskaKultura_DesetinnaCarkaAMezeraTisicu()
    {
        // NormalizeWhitespace nahrazuje nezlomitelnou mezeru běžnou
        Assert.Equal("1 234,50", PlaceholderFormatter.Format("1234.5", "N2", Cs));
    }

    [Fact]
    public void CiselnyFormat_AnglickaKultura_DesetinnaTecka()
    {
        Assert.Equal("1,234.50", PlaceholderFormatter.Format("1234.5", "N2", En));
    }

    // ── Datum a čas ──────────────────────────────────────────────────────────

    [Fact]
    public void DatumovyFormat()
    {
        Assert.Equal("02.07.2026", PlaceholderFormatter.Format("2026-07-02", "dd.MM.yyyy", Cs));
    }

    [Fact]
    public void CasovyFormat_TimeSpanHodnota()
    {
        Assert.Equal("08:00", PlaceholderFormatter.Format("8:00", "HH:mm", Cs));
    }

    // ── Fail-safe ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("abc", "N2")]          // nenumerická hodnota s číselným formátem
    [InlineData("abc", "dd.MM.yyyy")]  // nedatumová hodnota s datumovým formátem
    public void NeaplikovatelnyFormat_VraciPuvodniHodnotu(string value, string format)
    {
        Assert.Equal(value, PlaceholderFormatter.Format(value, format, Cs));
    }

    [Fact]
    public void PrazdnyFormat_VraciHodnotuBezeZmeny()
    {
        Assert.Equal("42", PlaceholderFormatter.Format("42", null));
        Assert.Equal("42", PlaceholderFormatter.Format("42", ""));
    }

    // ── Integrace přes DataContext.ResolvePlaceholders ───────────────────────

    [Fact]
    public void ResolvePlaceholders_NahradiPoleSFormatem_DleKulturyKontextu()
    {
        var data = new DataContext(
            new Dictionary<string, object?> { ["JMENO"] = "Jan", ["CENA"] = "1234.5" },
            culture: Cs);

        Assert.Equal("Řidič: Jan", data.ResolvePlaceholders("Řidič: {JMENO}"));
        Assert.Equal("Cena: 1 234,50", data.ResolvePlaceholders("Cena: {CENA:N2}"));
    }

    [Fact]
    public void ResolvePlaceholders_NeznamePole_ZustavaJakoPlaceholder()
    {
        var data = new DataContext(new Dictionary<string, object?> { ["A"] = "1" });

        Assert.Equal("X: {NEZNAME}", data.ResolvePlaceholders("X: {NEZNAME}"));
    }
}
