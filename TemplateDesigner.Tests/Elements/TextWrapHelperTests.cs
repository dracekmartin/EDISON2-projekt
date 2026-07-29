using SkiaSharp;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Elements;

/// <summary>
/// TextWrapHelper měří přes SKFont (headless SkiaSharp, bez WPF). Testy záměrně
/// neassertují přesné pixelové šířky (závisí na fontech stroje) - testují chování
/// nezávislé na metrice: normalizaci zlomů, extrémní šířky, prázdný vstup.
/// </summary>
public class TextWrapHelperTests
{
    private static SKFont Font() => new(SKTypeface.Default, 12f);

    [Fact]
    public void VelkaSirka_KazdyOdstavecJedenRadek()
    {
        using var font = Font();
        var lines = TextWrapHelper.WrapText("První odstavec\nDruhý odstavec", font, 100_000f);

        Assert.Equal(["První odstavec", "Druhý odstavec"], lines);
    }

    [Fact]
    public void WindowsZlomyRadku_SeNormalizuji()
    {
        using var font = Font();
        var lines = TextWrapHelper.WrapText("A\r\nB\rC", font, 100_000f);

        Assert.Equal(["A", "B", "C"], lines);
    }

    [Fact]
    public void MalaSirka_ZalomiKazdeSlovoNaVlastniRadek()
    {
        using var font = Font();
        var lines = TextWrapHelper.WrapText("jedna dva tři", font, 1f);

        Assert.Equal(["jedna", "dva", "tři"], lines);
    }

    [Fact]
    public void PrazdnyText_VratiJedenPrazdnyRadek()
    {
        using var font = Font();

        Assert.Equal([""], TextWrapHelper.WrapText("", font, 100f));
    }

    [Fact]
    public void PrazdnyRadekUprostred_SeZachova()
    {
        using var font = Font();
        var lines = TextWrapHelper.WrapText("A\n\nB", font, 100_000f);

        Assert.Equal(["A", "", "B"], lines);
    }

    [Fact]
    public void DlouheSlovoPresahujiciSirku_SeNedeliAniNeztrati()
    {
        using var font = Font();
        var lines = TextWrapHelper.WrapText("Nezalomitelnéslovo", font, 1f);

        Assert.Equal(["Nezalomitelnéslovo"], lines);
    }
}
