using TemplateDesigner.Models;
using TemplateDesigner.Models.Layout;

namespace TemplateDesigner.Tests.Layout;

/// <summary>
/// Měřítko tisku - hotová sazba se před tiskem celá zvětší (`B132`).
/// </summary>
/// <remarks>
/// Parita s `MeritkoVyves` starého postskriptu: sází na logickou stránku 208 × 145 mm a při tisku
/// ji zvětší 1,3× (kniha 1×). Bez toho znamená jiná velikost výstupu druhou šablonu.
///
/// <para>⚠️ **Je to vlastnost výstupu, ne šablony.** Menu „Přepočítat šablonu na jinou velikost"
/// sahá na souřadnice prvků a původní velikost tím zaniká; tohle se šablony nedotkne.</para>
/// </remarks>
public class MeritkoTiskuTests
{
    private static Dictionary<string, object?> Hodnoty(params (string Key, object? Value)[] v) =>
        v.ToDictionary(x => x.Key, x => x.Value);

    private static readonly PageSize A5NaSirku = new() { Width = 210, Height = 148 };

    // ── čtení hodnoty ────────────────────────────────────────────────────────

    [Fact]
    public void ChybejiciHodnota_ZnamenaSkutecnouVelikost()
    {
        Assert.Equal(1f, SheetComposer.MeritkoZParamValues(Hodnoty()));
    }

    [Theory]
    [InlineData("1.3", 1.3f)]
    [InlineData("1,3", 1.3f)]      // formulář může poslat českou desetinnou čárku
    [InlineData("2", 2f)]
    [InlineData("0.5", 0.5f)]
    public void HodnotaZFormulare_SeNacte(string napsano, float ocekavano)
    {
        Assert.Equal(ocekavano,
            SheetComposer.MeritkoZParamValues(Hodnoty((SheetLayoutKeys.Meritko, napsano))), 3);
    }

    /// <summary>
    /// Nesmyslná hodnota spadne na skutečnou velikost, ne na nulu.
    /// </summary>
    /// <remarks>
    /// Měřítko 0 by dalo stránku o nulovém rozměru a prázdné PDF - uživatel by z toho nepoznal,
    /// že si sám zadal nesmysl.
    /// </remarks>
    [Theory]
    [InlineData("0")]
    [InlineData("-2")]
    [InlineData("100")]
    [InlineData("nesmysl")]
    [InlineData("")]
    public void NesmyslnaHodnota_SpadneNaJednicku(string napsano)
    {
        Assert.Equal(1f, SheetComposer.MeritkoZParamValues(Hodnoty((SheetLayoutKeys.Meritko, napsano))));
    }

    [Fact]
    public void MeritkoJeMeziExportnimiKlici_AtSeObnoviSNimi()
    {
        // Exportní pole nejsou mezi parametry agendy, takže se obnovují podle tohohle seznamu.
        Assert.Contains(SheetLayoutKeys.Meritko, SheetLayoutKeys.All);
    }

    // ── skládá se s uspořádáním na list ──────────────────────────────────────

    [Fact]
    public void NaListSeSklada_AzZVETSENA_stranka()
    {
        // ⚠️ Nejdřív měřítko, pak skládání. A5 na šířku (210×148) při 1,3× vyjde 273×192 mm,
        // takže se na A4 na šířku (297×210) vejde jen jedna - kdežto v návrhové velikosti dvě.
        var listy = SheetComposer.FromParamValues(Hodnoty(
            (SheetLayoutKeys.Sheet, "A4_LANDSCAPE"),
            (SheetLayoutKeys.Meritko, "1.3")), A5NaSirku);

        Assert.NotNull(listy);
        Assert.Equal(273f, listy!.PageWidth, 1);
        Assert.Equal(192.4f, listy.PageHeight, 1);
        Assert.Equal(1, listy.SlotsPerSheet);
    }

    [Fact]
    public void BezMeritka_SeSkladaJakoDriv()
    {
        var listy = SheetComposer.FromParamValues(Hodnoty(
            (SheetLayoutKeys.Sheet, "A3_LANDSCAPE")), A5NaSirku);

        Assert.NotNull(listy);
        Assert.Equal(210f, listy!.PageWidth, 1);
        Assert.Equal(148f, listy.PageHeight, 1);
        Assert.True(listy.SlotsPerSheet > 1, "na A3 na šířku se má vejít víc A5 stránek");
    }

    /// <summary>
    /// Zvětšená stránka, která se na list nevejde, se ohlásí jako chyba nastavení.
    /// </summary>
    /// <remarks>
    /// Bez toho by mřížka vyšla 0 × 0 a z tisku by nevylezlo nic; uživatel by hledal chybu
    /// v šabloně, ne v měřítku.
    /// </remarks>
    [Fact]
    public void PrilisZvetsenaStranka_SeNaListNevejde_aRekneSeTo()
    {
        var ex = Assert.Throws<ArgumentException>(() => SheetComposer.FromParamValues(Hodnoty(
            (SheetLayoutKeys.Sheet, "A4_LANDSCAPE"),
            (SheetLayoutKeys.Meritko, "3")), A5NaSirku));

        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }
}
