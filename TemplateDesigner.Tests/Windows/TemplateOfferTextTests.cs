namespace TemplateDesigner.Tests.Windows;

/// <summary>
/// Skloňování v nabídce nových šablon. „K dispozici je 1 nových šablon" je vidět hned a působí
/// nedodělaně (nález Jan 19. 8.) - čeština má tři tvary podle počtu a hlídá to tenhle test.
/// </summary>
public class TemplateOfferTextTests
{
    private static string Text(int n) =>
        CacheTemplatePickerDialogViewModel.Pocet(n, "nová", "nové", "nových");

    [Theory]
    [InlineData(1, "1 nová šablona")]
    [InlineData(2, "2 nové šablony")]
    [InlineData(4, "4 nové šablony")]
    [InlineData(5, "5 nových šablon")]
    [InlineData(11, "11 nových šablon")]
    public void PocetSeSklonuje(int pocet, string ocekavano)
    {
        Assert.Equal(ocekavano, Text(pocet));
    }

    [Fact]
    public void AktualizovaneMajiVlastniTvary()
    {
        Assert.Equal("1 aktualizovaná šablona",
            CacheTemplatePickerDialogViewModel.Pocet(1, "aktualizovaná", "aktualizované", "aktualizovaných"));
        Assert.Equal("6 aktualizovaných šablon",
            CacheTemplatePickerDialogViewModel.Pocet(6, "aktualizovaná", "aktualizované", "aktualizovaných"));
    }
}
