using EDControls;
using EDControls.EDViewModel;
using NSubstitute;
using TemplateDesigner.Models;
using TemplateDesigner.Models.Elements;

namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Dialog sloupců linkového JŘ pro <b>sloupec šipky směru</b> - jak se jmenuje a co u něj dává smysl
/// nastavovat.
/// </summary>
/// <remarks>
/// Oba nálezy Jana z 28. 8. při zkoušení `L6a`: v dialogu svítil anglický klíč „Arrow", a barva
/// nastavená ve „Stylu záhlaví" šipku neobarvila - u sloupce bez textu v datech se nedalo poznat,
/// že šipku řídí zóna datových buněk.
/// <para>Řádek sloupce nese, co jeho zóna nabízí - okno ho jen zobrazí. EDViewModel okna čte
/// EDServices.Current → mock v konstruktoru.</para>
/// </remarks>
public class LineTableSloupecSipkyTests
{
    public LineTableSloupecSipkyTests() =>
        EDServices.Initialize(new EDServices(Substitute.For<IMLRest>(),
            Substitute.For<IWindowNavigator>(), Substitute.For<IPopupDisplayer>()));

    private static LineTableColumnVM Sloupec(string klic) =>
        new LineTableColumnsEditorDialogViewModel(new LineTableElement(), LineTableElement.GetDefaultColumns())
            .Columns.Rows.Cast<LineTableColumnVM>().First(c => c.Key == klic);

    /// <summary>
    /// Sloupec se jmenuje <b>česky</b>. Mapa lidských názvů propadá u neznámého klíče na klíč sám,
    /// takže se nový sloupec musí doplnit i tam - jinak se do UI dostane kódový název.
    /// </summary>
    [Fact]
    public void SeJmenujeCesky() => Assert.Equal("Šipka směru", Sloupec(ColumnKeys.Arrow).DisplayName);

    /// <summary>
    /// V datové zóně jde nastavit <b>jen barva a pozadí</b> - font, řez, zarovnání ani odsazení na
    /// vektorovou šipku nemají vliv, takže tam jen mátly. Zóna se jmenuje „Šipka" místo „Datové buňky".
    /// </summary>
    [Fact]
    public void VDatoveZone_JdeJenBarvaAPozadi()
    {
        var sipka = Sloupec(ColumnKeys.Arrow);

        Assert.Equal("Šipka", sipka.DataZoneTitle);
        Assert.True(sipka.HasDataZoneHint);
        Assert.True(sipka.DataEditOptions.AllowTextColor);
        Assert.True(sipka.DataEditOptions.AllowBackgroundColor);
        Assert.False(sipka.DataEditOptions.AllowFontFamily);
        Assert.False(sipka.DataEditOptions.AllowFontSize);
        Assert.False(sipka.DataEditOptions.AllowHAlign);
        Assert.False(sipka.DataEditOptions.AllowPadding);
    }

    /// <summary>U běžného sloupce zůstává datová zóna úplná - nic se nemá skrývat.</summary>
    [Fact]
    public void UBezenehoSloupce_ZustavaZonaUplna()
    {
        var zastavka = Sloupec(ColumnKeys.StopName);

        Assert.Equal("Datové buňky", zastavka.DataZoneTitle);
        Assert.False(zastavka.HasDataZoneHint);
        Assert.True(zastavka.DataEditOptions.AllowFontFamily);
        Assert.True(zastavka.DataEditOptions.AllowPadding);
    }
}
