using TemplateDesigner.Models;

namespace TemplateDesigner.Tests.Models;

/// <summary>
/// Náhledová barva pro čtvereček u pole s barvou z dat (#12997). Bez ní vypadá pole s tokenem
/// <c>{BARVA_LINKY}</c> jako nenastavená bílá a mate uživatele.
/// ⚠️ Klíčový invariant: je to **jen zobrazení**. Kdyby se vyřešená barva zapsala do hodnoty pole,
/// šablona přestane barvu brát z dat a zamrzne na barvě linky, která byla náhodou otevřená.
/// </summary>
public class ColorHexUiPreviewTests : IDisposable
{
    public void Dispose() => ColorHex.UiData = null;

    private static DataContext Data(string barva) =>
        new(new Dictionary<string, object?> { ["BARVA_LINKY"] = barva }, new Dictionary<string, IReadOnlyList<DataContext>>());

    [Fact]
    public void SDaty_VyresiTokenNaBarvu()
    {
        ColorHex.UiData = Data("#FF95CA64");

        Assert.Equal("#FF95CA64", ColorHex.ResolveDataBoundForUi("{BARVA_LINKY}"));
    }

    [Fact]
    public void BezDat_VratiNull()
    {
        ColorHex.UiData = null;

        Assert.Null(ColorHex.ResolveDataBoundForUi("{BARVA_LINKY}"));
    }

    /// <summary>Linka barvu nemá (prázdná hodnota) nebo pole neexistuje - náhled nemá co ukázat,
    /// takže se nesmí vrátit nesmysl; komponenta pak nakreslí šrafování „z dat".</summary>
    [Fact]
    public void PrazdnaNeboNeznamaHodnota_VratiNull()
    {
        ColorHex.UiData = Data("");
        Assert.Null(ColorHex.ResolveDataBoundForUi("{BARVA_LINKY}"));

        ColorHex.UiData = Data("#FF95CA64");
        Assert.Null(ColorHex.ResolveDataBoundForUi("{NEEXISTUJE}"));
    }

    /// <summary>Hodnota, která barvu nese přímo, náhled nepotřebuje - čtvereček se řídí sám hodnotou.</summary>
    [Fact]
    public void PevnaBarva_NemaNahled()
    {
        ColorHex.UiData = Data("#FF95CA64");

        Assert.Null(ColorHex.ResolveDataBoundForUi("#FF102030"));
        Assert.Null(ColorHex.ResolveDataBoundForUi(""));
        Assert.Null(ColorHex.ResolveDataBoundForUi(null));
    }

    /// <summary>Kdyby data vrátila něco, co barva není, náhled to nesmí pustit dál - jinak by
    /// čtvereček ukázal náhodnou barvu z parsovacího fallbacku.</summary>
    [Fact]
    public void NeplatnaHodnotaZDat_VratiNull()
    {
        ColorHex.UiData = Data("zelena");

        Assert.Null(ColorHex.ResolveDataBoundForUi("{BARVA_LINKY}"));
    }
}
