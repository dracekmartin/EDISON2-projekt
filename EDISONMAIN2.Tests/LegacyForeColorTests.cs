using System.Windows.Media;
using EDISONMAIN2.LegacyMiniApps;

namespace EDISONMAIN2.Tests;

/// <summary>
/// Legacy barvy řádků jsou navržené na bílé pozadí. V tmavém režimu se zesvětlují, nikdy
/// neinvertují - inverze by z červené udělala azurovou a zahodila sémantiku „červená = problém".
/// </summary>
public class LegacyForeColorTests
{
    private static readonly Color DarkSurface = Color.FromRgb(0x2D, 0x2D, 0x30);
    private static readonly Color White = Colors.White;

    [Theory]
    [InlineData(0xFF, 0x00, 0x00)]   // červená - chyba
    [InlineData(0x00, 0x80, 0x00)]   // zelená - v pořádku
    [InlineData(0x00, 0x00, 0xFF)]   // modrá - odkaz
    public void Adapt_ReachesReadableContrastOnDarkSurface(byte r, byte g, byte b)
    {
        var adapted = LegacyForeColor.Adapt(Color.FromRgb(r, g, b), DarkSurface);

        Assert.True(LegacyForeColor.Contrast(adapted, DarkSurface) >= 4.5,
                    $"kontrast {LegacyForeColor.Contrast(adapted, DarkSurface):F2} je pod 4,5:1");
    }

    /// <summary>Signál nese odstín - ten se zesvětlením měnit nesmí.</summary>
    [Fact]
    public void Adapt_KeepsRedChannelDominant()
    {
        var adapted = LegacyForeColor.Adapt(Color.FromRgb(0xFF, 0x00, 0x00), DarkSurface);

        Assert.True(adapted.R > adapted.G && adapted.R > adapted.B);
    }

    /// <summary>Inverze by u červené vyrobila azurovou - právě to tenhle převod dělat nesmí.</summary>
    [Fact]
    public void Adapt_DoesNotInvert()
    {
        var adapted = LegacyForeColor.Adapt(Color.FromRgb(0xFF, 0x00, 0x00), DarkSurface);

        Assert.NotEqual(Color.FromRgb(0x00, 0xFF, 0xFF), adapted);
    }

    /// <summary>Co je čitelné, zůstane beze změny - zbytečné posuny by rozjely paritu s legacy.</summary>
    [Fact]
    public void Adapt_LeavesSufficientContrastAlone()
    {
        var pale = Color.FromRgb(0xFF, 0xC0, 0xC0);

        Assert.Equal(pale, LegacyForeColor.Adapt(pale, DarkSurface));
    }

    [Fact]
    public void Contrast_IsSymmetric()
    {
        Assert.Equal(LegacyForeColor.Contrast(White, DarkSurface),
                     LegacyForeColor.Contrast(DarkSurface, White), 6);
    }

    /// <summary>Černá na tmavém pozadí nemá kam jinam než ke světlé šedi - nesmí se zaseknout.</summary>
    [Fact]
    public void Adapt_HandlesUnsaturatedColor()
    {
        var adapted = LegacyForeColor.Adapt(Colors.Black, DarkSurface);

        Assert.True(LegacyForeColor.Contrast(adapted, DarkSurface) >= 4.5);
        Assert.Equal(adapted.R, adapted.G);
        Assert.Equal(adapted.G, adapted.B);
    }
}
